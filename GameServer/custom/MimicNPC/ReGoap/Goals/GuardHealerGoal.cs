using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// MainTank priority goal for guarding the primary healer using the Guard ability
    /// Highest priority tank goal (10.0) based on DAoC RvR 8v8 meta analysis
    /// Shared across all tank classes with Guard ability (Armsman, Warrior, Hero, etc.)
    /// </summary>
    /// <remarks>
    /// Design Principles (from design.md Component 4: Role-Based Goals):
    /// - GuardHealerGoal is the #1 priority for MainTank role in RvR scenarios
    /// - Guard ability redirects damage from healer to tank (critical for healer survival)
    /// - Goal maintains guard uptime at all times (extremely low cost = always active)
    /// - Multiple tank classes share same goal definition (all MainTanks use GuardHealerGoal)
    ///
    /// Priority Formula (from design.md DAoC-Specific Design Decisions):
    /// Base Priority: 10.0 (highest tank priority)
    /// - Guard active: 10.0 (maintain guard)
    /// - Guard inactive + healer present: 10.0 * 100 = 1000.0 (emergency re-guard)
    /// - No healer in group: 0.0 (goal not applicable)
    ///
    /// DAoC Guard Mechanic (from daoc-role-analysis.md):
    /// - Guard ability redirects 50% of damage from guarded target to tank
    /// - Range: 2000 units (must stay relatively close to healer)
    /// - No cooldown, but drops on death or distance
    /// - Critical for healer survival in RvR (healers are primary targets)
    /// - Tank loses guard if too far from healer or if tank/healer dies
    ///
    /// DAoC RvR 8v8 Context (from daoc-role-analysis.md Priority #1):
    /// - Healer survival is #1 priority in group fights (group wipes without healing)
    /// - Enemy DPS/assassins target healers first (squishy, high value)
    /// - Guard significantly increases healer effective HP (50% damage reduction)
    /// - Guarding healer is more important than threat generation in RvR
    /// - Tank positioning must balance: guard healer + control adds + maintain threat
    ///
    /// Organic Behavior Patterns (from requirements.md 11.27-11.28):
    /// - When guard active: Tank maintains guard (low cost 0.1 keeps it checked frequently)
    /// - When guard drops: Emergency priority (1000.0) forces immediate re-guard
    /// - Action cost calculation (design.md GuardCostCalculator): base 0.1, x0.01 if dropped
    /// - Result: Tank never lets guard drop (always highest priority action when inactive)
    ///
    /// World State Dependencies (populated by GuardSensor reading Body.GuardTarget):
    /// - GUARD_ACTIVE: Guard ability currently active (Body.GuardTarget != null)
    /// - GUARD_TARGET: Current guard target GameObject (should be primary healer)
    /// - HEALER_NEEDS_GUARD: Healer found in group but guard not active
    /// - IS_MAIN_TANK: Role assignment (goal only active for MainTank role)
    /// - GROUP_SIZE: Number of group members (goal only applies in group scenarios)
    ///
    /// Goal State: { "healerGuarded": true }
    /// Satisfied when: Guard active AND guarding primary healer (GUARD_ACTIVE && correct target)
    ///
    /// Example Scenarios:
    /// - Tank in group with healer, guard active: Priority = 10.0 (maintain guard)
    /// - Tank in group with healer, guard inactive: Priority = 1000.0 (emergency re-guard)
    /// - Tank solo or no healer in group: Priority = 0.0 (no healer to guard)
    /// - Tank dies and respawns: Priority = 1000.0 (immediately re-guard healer)
    /// - Tank moves too far from healer: Guard drops, Priority = 1000.0 (close gap and re-guard)
    ///
    /// Interaction with Other Goals:
    /// - GuardHealerGoal (10.0) > InterruptEnemyCasterGoal (9.0) > EmergencyHealGoal (8.0 @ <25% HP)
    /// - GuardHealerGoal (1000.0 emergency) > All other goals (forces immediate action)
    /// - ProtectGroupGoal (peel enemies) has lower priority than guard maintenance
    /// - Guard must stay active even during aggressive threat generation
    ///
    /// Result: Tank always protects healer with guard, re-applies immediately if dropped
    /// </remarks>
    public class GuardHealerGoal : MimicGoal
    {
        /// <summary>
        /// Base priority for maintaining guard on healer
        /// Value of 10.0 makes this highest priority tank goal (from daoc-role-analysis.md)
        /// Higher than all other tank goals (threat=3.0, protect=4.0, pull=1.0)
        /// </summary>
        private const float BASE_PRIORITY = 10.0f;

        /// <summary>
        /// Emergency multiplier when guard is inactive but healer needs guarding
        /// 100x multiplier (10.0 * 100 = 1000.0) forces immediate re-guard action
        /// Ensures guard is never down for more than one think cycle (500ms)
        /// </summary>
        private const float EMERGENCY_GUARD_MULTIPLIER = 100.0f;

        /// <summary>
        /// Goal state key for healer guarded
        /// </summary>
        private const string HEALER_GUARDED = "healerGuarded";

        /// <summary>
        /// Constructs a new GuardHealerGoal for a MainTank mimic
        /// </summary>
        /// <param name="body">The MimicNPC body for game state access (via GuardSensor reading Body.GuardTarget)</param>
        /// <param name="brain">The MimicBrain for AI state access (role assignments)</param>
        public GuardHealerGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        /// <summary>
        /// Calculates dynamic priority based on guard status and healer presence
        /// Priority extremely high when guard inactive, consistent when guard active
        /// </summary>
        /// <param name="currentState">Current world state populated by GuardSensor reading Body.GuardTarget</param>
        /// <returns>Priority value (10.0 = maintain, 1000.0 = emergency re-guard, 0.0 = not applicable)</returns>
        /// <remarks>
        /// Priority Calculation Logic:
        /// 1. Check prerequisites:
        ///    - Must be MainTank role (IS_MAIN_TANK from world state)
        ///    - Must be in a group (GROUP_SIZE > 1)
        ///    - Must have healer in group (HEALER_NEEDS_GUARD or GUARD_ACTIVE)
        /// 2. If guard inactive AND healer needs guard:
        ///    - Return 10.0 * 100 = 1000.0 (emergency priority, forces immediate action)
        /// 3. If guard active:
        ///    - Return 10.0 (maintain guard, highest baseline tank priority)
        /// 4. If no healer or solo:
        ///    - Return 0.0 (goal not applicable)
        ///
        /// Example Scenarios:
        /// - Tank with active guard on healer: 10.0 (maintain)
        /// - Tank without guard, healer present: 1000.0 (emergency)
        /// - Tank solo: 0.0 (no healer to guard)
        /// - Tank in group, no healer class: 0.0 (no healer to guard)
        ///
        /// Result: Guard is always maintained (never drops for more than 500ms think cycle)
        /// Combined with GuardCostCalculator (base 0.1, x0.01 if dropped), guard action
        /// becomes overwhelmingly preferred when inactive, ensuring 100% uptime.
        /// </remarks>
        public override float GetPriority(ReGoapState<string, object> currentState)
        {
            // Read world state values populated by GuardSensor and role sensors
            bool isMainTank = GetBool(currentState, MimicWorldStateKeys.IS_MAIN_TANK, false);
            int groupSize = GetGroupSize(currentState);
            bool guardActive = GetBool(currentState, MimicWorldStateKeys.GUARD_ACTIVE, false);
            bool healerNeedsGuard = GetBool(currentState, MimicWorldStateKeys.HEALER_NEEDS_GUARD, false);

            // Goal only applies to MainTank role in group scenarios with healer present
            if (!isMainTank || groupSize <= 1)
                return 0.0f;

            // If no healer in group, guard not applicable
            if (!guardActive && !healerNeedsGuard)
                return 0.0f;

            // CRITICAL: If guard dropped and healer needs guard, emergency priority
            // Multiplier of 100x (10.0 * 100 = 1000.0) forces immediate re-guard action
            // This ensures guard is never down for more than one think cycle (500ms)
            if (!guardActive && healerNeedsGuard)
            {
                return BASE_PRIORITY * EMERGENCY_GUARD_MULTIPLIER; // 1000.0 emergency
            }

            // Guard active: maintain guard at highest baseline tank priority
            // Priority 10.0 ensures guard maintenance is prioritized over:
            // - MaintainThreatGoal (3.0-9.0 range)
            // - ProtectGroupGoal (4.0-20.0 range, but only when allies attacked)
            // - DealDamageGoal (2.0)
            // - PullTargetGoal (1.0)
            return BASE_PRIORITY; // 10.0 maintain
        }

        /// <summary>
        /// Defines the desired world state when guard goal is satisfied
        /// Goal: Primary healer is guarded by tank's Guard ability
        /// </summary>
        /// <returns>Goal state with "healerGuarded" = true</returns>
        /// <remarks>
        /// The planner will search for action sequences that set "healerGuarded" to true.
        /// Guard actions (UseGuardAbilityAction) set "healerGuarded" effect when executed on healer target.
        ///
        /// Goal satisfaction checked by IsGoalSatisfied() against GUARD_ACTIVE and correct target.
        ///
        /// Note: Guard is a toggled ability in DAoC - once activated, it persists until:
        /// - Tank moves out of range (>2000 units from healer)
        /// - Tank or healer dies
        /// - Tank manually drops guard (unlikely for AI)
        /// - Tank uses another guard/protect ability (changes target)
        ///
        /// Therefore, re-guard actions only needed when guard drops, not continuous casting.
        /// Action cost calculation (GuardCostCalculator) ensures guard action is cheapest when inactive.
        /// </remarks>
        public override ReGoapState<string, object> GetGoalState()
        {
            var goalState = new ReGoapState<string, object>();

            // Desired end state: healer protected by guard ability
            goalState.Set(HEALER_GUARDED, true);

            return goalState;
        }

        /// <summary>
        /// Checks if guard goal is currently satisfied
        /// Satisfied when guard active and guarding correct target (primary healer)
        /// </summary>
        /// <param name="currentState">Current world state from GuardSensor</param>
        /// <returns>True if healer guarded, false if guard inactive or wrong target</returns>
        /// <remarks>
        /// Override default satisfaction check to use GUARD_ACTIVE directly.
        /// More efficient than checking goal state match (avoids unnecessary comparisons).
        ///
        /// GUARD_ACTIVE populated by GuardSensor reading Body.GuardTarget property,
        /// which is maintained by existing game systems (no data duplication).
        ///
        /// Satisfaction Logic:
        /// - GUARD_ACTIVE == true: Guard ability active → goal satisfied
        /// - GUARD_ACTIVE == false: Guard dropped or never activated → goal not satisfied
        /// - No healer in group: Goal satisfied (no healer to guard, N/A scenario)
        ///
        /// GuardSensor Implementation (from design.md Component 3: Sensor Framework):
        /// - Reads Body.GuardTarget (existing property)
        /// - Identifies primary healer from group (GetPrimaryHealer() helper)
        /// - Sets GUARD_ACTIVE = (Body.GuardTarget != null)
        /// - Sets HEALER_NEEDS_GUARD = (healer found AND guard not active)
        ///
        /// Edge Cases:
        /// - Guard active but guarding wrong target (tank manually changed): Not satisfied (re-guard healer)
        /// - No healer in group: Satisfied (goal N/A, prevent unnecessary planning)
        /// - Tank solo: Satisfied (goal N/A)
        /// </remarks>
        public override bool IsGoalSatisfied(ReGoapState<string, object> currentState)
        {
            bool isMainTank = GetBool(currentState, MimicWorldStateKeys.IS_MAIN_TANK, false);
            int groupSize = GetGroupSize(currentState);
            bool guardActive = GetBool(currentState, MimicWorldStateKeys.GUARD_ACTIVE, false);
            bool healerNeedsGuard = GetBool(currentState, MimicWorldStateKeys.HEALER_NEEDS_GUARD, false);

            // Goal satisfied if not MainTank or solo (goal not applicable)
            if (!isMainTank || groupSize <= 1)
                return true;

            // Goal satisfied if no healer in group (nothing to guard)
            if (!guardActive && !healerNeedsGuard)
                return true;

            // Goal satisfied ONLY if guard active (healer is protected)
            return guardActive;
        }

        /// <summary>
        /// Gets the name of this goal for debugging and logging
        /// </summary>
        /// <returns>Goal name string</returns>
        public override string GetName()
        {
            return "GuardHealerGoal";
        }

        /// <summary>
        /// Gets debug information including current priority, guard status, and healer presence
        /// Used by /mimic debug command and logging
        /// </summary>
        /// <param name="currentState">Current world state from GuardSensor</param>
        /// <returns>Debug string with priority, satisfaction, and guard details</returns>
        public override string GetDebugInfo(ReGoapState<string, object> currentState)
        {
            float priority = GetPriority(currentState);
            bool satisfied = IsGoalSatisfied(currentState);
            bool isMainTank = GetBool(currentState, MimicWorldStateKeys.IS_MAIN_TANK, false);
            int groupSize = GetGroupSize(currentState);
            bool guardActive = GetBool(currentState, MimicWorldStateKeys.GUARD_ACTIVE, false);
            bool healerNeedsGuard = GetBool(currentState, MimicWorldStateKeys.HEALER_NEEDS_GUARD, false);
            var guardTarget = GetStateValue<object>(currentState, MimicWorldStateKeys.GUARD_TARGET, null);

            return $"{GetName()} (Priority: {priority:F2}, Satisfied: {satisfied}, " +
                   $"GuardActive: {guardActive}, HealerNeedsGuard: {healerNeedsGuard}, " +
                   $"GuardTarget: {guardTarget?.ToString() ?? "none"}, GroupSize: {groupSize}, IsMainTank: {isMainTank})";
        }
    }
}
