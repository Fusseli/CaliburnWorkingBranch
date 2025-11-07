using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// CC (Crowd Control) role goal for controlling additional enemies (adds) to prevent group overwhelm
    /// Shared across all CC roles (PacHealer, Bard, dedicated CC classes) regardless of realm
    /// Priority scales dynamically based on number of uncontrolled adds and combat danger level
    /// </summary>
    /// <remarks>
    /// Design Principles (from design.md Component 4: Role-Based Goals):
    /// - CC goal prioritizes controlling additional enemies in aggro list (adds beyond main target)
    /// - Multiple CC classes share same goal definition (all MainCC/PacHealers use ControlAddsGoal)
    /// - Priority dynamically calculated based on world state from sensors
    /// - CRITICAL: Must avoid breaking existing mezzes (mezz breaks on ANY damage in DAoC)
    ///
    /// Priority Formula (from requirements.md 11.22-11.23):
    /// base_priority = (number_of_uncontrolled_adds - 1) * 4.0
    /// - If uncontrolled adds exceed 3: multiply by 2.0 (escalating urgency)
    /// - If no adds need controlling: return 0.0 (goal not applicable)
    ///
    /// DAoC CC Context (from daoc-role-analysis.md):
    /// - Crowd Control (Mezz): Primary method of managing multiple enemies in DAoC
    /// - Mezz Duration: Up to 60 seconds, but breaks on ANY damage (including AoE, DoT ticks)
    /// - PacHealer Role: Primary CC + secondary healing + interrupts (critical for RvR 8v8)
    /// - Add Management: Tank holds main target, CC controls adds (prevents group wipe)
    /// - Mezz Breaking: Catastrophic error - re-mezzing same target costs time, has diminishing returns
    /// - CC Priority: Control unmezzed adds > refresh expiring mezzes > never break existing mezzes
    ///
    /// Organic Behavior Patterns (from requirements.md 11.33):
    /// - When CC has multiple add options: Longest-duration CC preferred (cost calculation favors duration)
    /// - Action cost calculation heavily penalizes damaging mezzed targets (500% cost increase)
    /// - CCCostCalculator applies diminishing returns penalty (200% cost increase after re-mezz)
    /// - Result: Natural CC priority - mezz unmezzed adds first, avoid breaking existing mezzes
    ///
    /// World State Dependencies:
    /// - NUM_CONTROLLABLE_ADDS: Number of adds that can be controlled (unmezzed AND not immune)
    /// - NUM_UNMEZZED_ADDS: Number of adds not currently mezzed (includes immune targets)
    /// - NUM_UNCONTROLLED_ADDS: Same as NUM_UNMEZZED_ADDS (adds needing control)
    /// - UNMEZZED_ADDS: List of specific add targets needing mezz (may include immune)
    /// - CC_IMMUNE_ENEMIES: List of enemies known to be immune to CC (from failed attempts or level checks)
    /// - MEZZED_ENEMIES: List of currently mezzed enemies (NEVER damage these!)
    /// - NUM_ENEMIES: Total enemies on aggro list (context for add count)
    /// - HAS_AGGRO: Whether mimic has any enemies on aggro list
    /// - IN_COMBAT: Combat state (goal only active in combat)
    /// - IS_MAIN_CC: Role assignment (goal active for MainCC/PacHealer roles)
    /// - CAN_CAST_CROWD_CONTROL: Whether mimic has CC spells available
    ///
    /// Goal State: { "addsControlled": true }
    /// Satisfied when: All controllable adds are mezzed (NUM_CONTROLLABLE_ADDS == 0)
    ///
    /// Immunity Handling:
    /// - MezzStatusSensor tracks CC_IMMUNE_ENEMIES list (targets that returned explicit immunity messages)
    /// - NUM_CONTROLLABLE_ADDS = adds that are NOT already controlled AND NOT immune
    /// - Immune targets excluded from priority calculation and action planning
    /// - Immunity detection methods:
    ///   1. Explicit immunity: Spell effect returns "immune to this effect" message (mark as immune)
    ///   2. Already controlled: Target has active control effect (IsMezzed, IsStunned, IsRooted) - can't CC again
    /// - NO level-based assumptions: High-level mobs are not automatically assumed immune (try and get real feedback)
    /// - Once marked immune via explicit message, target remains in CC_IMMUNE_ENEMIES for duration of encounter
    /// - Already-controlled targets (mezzed/stunned/rooted) excluded because re-CC would break existing control
    ///
    /// Example Scenarios:
    /// - Tank pulls 1 enemy: Priority = (0 * 4.0) = 0.0 (no adds, no CC needed)
    /// - Tank pulls 3 enemies (2 adds): Priority = (2 - 1) * 4.0 = 4.0 (moderate - control adds)
    /// - Tank pulls 5 enemies (4 adds): Priority = (4 - 1) * 4.0 = 12.0 (high - multiple adds)
    /// - Tank pulls 5 enemies (4 adds > 3): Priority = 12.0 * 2.0 = 24.0 (critical - escalating danger)
    /// - Tank pulls 3 enemies (2 adds, 1 immune): Priority = (1 - 1) * 4.0 = 0.0 (only 1 controllable add, optional CC)
    /// - Tank pulls 5 enemies (4 adds, 2 immune): Priority = (2 - 1) * 4.0 = 4.0 (2 controllable adds)
    /// - All controllable adds mezzed: Priority = 0.0 (goal satisfied, ignore immune targets)
    /// - 1 mezz expiring in <10s: MezzStatusSensor detects, priority increases for refresh
    ///
    /// Result: CC naturally prioritizes controlling unmezzed adds, with escalating urgency for multiple adds
    /// </remarks>
    public class ControlAddsGoal : MimicGoal
    {
        /// <summary>
        /// Priority multiplier per uncontrolled add (minus 1 for main target)
        /// Value of 4.0 makes each add significant (1 add = 4.0, 2 adds = 8.0, 3 adds = 12.0)
        /// </summary>
        private const float PRIORITY_PER_ADD = 4.0f;

        /// <summary>
        /// Multiplier when uncontrolled adds exceed critical threshold
        /// 2x multiplier represents escalating danger from multiple uncontrolled adds
        /// </summary>
        private const float MANY_ADDS_MULTIPLIER = 2.0f;

        /// <summary>
        /// Threshold for "many adds" that triggers escalating priority
        /// 3 adds = moderate danger, 4+ adds = critical danger requiring immediate CC
        /// </summary>
        private const int MANY_ADDS_THRESHOLD = 3;

        /// <summary>
        /// Minimum priority floor to ensure CC goal is evaluated when adds are present
        /// </summary>
        private const float MINIMUM_PRIORITY = 0.5f;

        /// <summary>
        /// Goal state key for add control completion
        /// </summary>
        private const string ADDS_CONTROLLED = "addsControlled";

        /// <summary>
        /// Constructs a new ControlAddsGoal for a CC mimic
        /// </summary>
        /// <param name="body">The MimicNPC body for game state access (via sensors)</param>
        /// <param name="brain">The MimicBrain for AI state access (via sensors)</param>
        public ControlAddsGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        /// <summary>
        /// Calculates dynamic priority based on uncontrolled add count from world state
        /// Priority scales with number of unmezzed adds and applies escalation multiplier
        /// </summary>
        /// <param name="currentState">Current world state populated by sensors reading Body/Brain</param>
        /// <returns>Priority value (higher = more urgent; 0.0 = not applicable)</returns>
        /// <remarks>
        /// Priority Calculation Logic:
        /// 1. Base: (numControllableAdds - 1) * 4.0
        ///    - Uses NUM_CONTROLLABLE_ADDS (not already controlled AND not immune)
        ///    - Subtract 1 because main target doesn't need CC (tank handles it)
        ///    - Each controllable add adds 4.0 priority
        /// 2. Escalation Condition:
        ///    - If controllable adds > 3: multiply by 2.0 (escalating danger)
        ///    - Represents overwhelming add count requiring urgent CC
        /// 3. Not in Combat: Return 0.0 (CC only matters in combat)
        /// 4. No Controllable Adds: Return 0.0 (no valid targets to control)
        /// 5. No CC Spells: Return 0.0 (can't satisfy goal without CC abilities)
        ///
        /// Example Scenarios:
        /// - 1 enemy (0 adds): (0) * 4.0 = 0.0 (no CC needed)
        /// - 2 enemies (1 add): (1 - 1) * 4.0 = 0.0 (tank can handle 1 add, optional CC)
        /// - 3 enemies (2 adds): (2 - 1) * 4.0 = 4.0 (moderate - CC recommended)
        /// - 4 enemies (3 adds): (3 - 1) * 4.0 = 8.0 (high - CC needed)
        /// - 5 enemies (4 adds): (4 - 1) * 4.0 * 2.0 = 24.0 (critical - escalating danger)
        /// - 6 enemies (5 adds): (5 - 1) * 4.0 * 2.0 = 32.0 (emergency - overwhelming adds)
        /// - 4 adds, 2 already mezzed (2 controllable): (2 - 1) * 4.0 = 4.0 (moderate - finish CC)
        /// - 5 adds, 2 immune, 1 mezzed (2 controllable): (2 - 1) * 4.0 = 4.0 (ignore immune, control remaining)
        ///
        /// Immunity Handling in Practice:
        /// - Action attempts CC on mob → returns "immune to this effect" → MezzStatusSensor adds to CC_IMMUNE_ENEMIES
        /// - NUM_CONTROLLABLE_ADDS automatically excludes immune targets (no wasted CC attempts)
        /// - Already-controlled targets (IsMezzed) excluded from NUM_CONTROLLABLE_ADDS (can't CC twice)
        /// - High-level boss NOT assumed immune → tries CC → gets feedback → adapts naturally
        ///
        /// DAoC Add Management Examples:
        /// - PvE: Tank pulls 3-mob pack → CC mezzes 2 adds while tank holds skull (main target)
        /// - RvR 8v8: Enemy group charges → Tank holds enemy tank, CC mezzes enemy assassin + archer
        /// - PvE Boss: Boss summons 4 adds → CC mezzes 3 adds (priority escalates to 24.0), tank holds boss + 1 add
        /// - Boss immune: CC tries mezz → "immune" → adds to CC_IMMUNE_ENEMIES → priority recalculates without boss
        ///
        /// Result: CC goal priority scales naturally with danger level (more controllable adds = higher priority)
        /// </remarks>
        public override float GetPriority(ReGoapState<string, object> currentState)
        {
            // Read world state values populated by sensors
            bool inCombat = IsInCombat(currentState);
            bool hasAggro = GetBool(currentState, MimicWorldStateKeys.HAS_AGGRO, false);
            int numEnemies = GetNumEnemies(currentState);
            int numControllableAdds = GetInt(currentState, MimicWorldStateKeys.NUM_CONTROLLABLE_ADDS, 0);
            bool isMainCC = GetBool(currentState, MimicWorldStateKeys.IS_MAIN_CC, false);
            bool canCastCC = GetBool(currentState, MimicWorldStateKeys.CAN_CAST_CROWD_CONTROL, false);

            // Goal only applies when in combat with controllable adds
            // NUM_CONTROLLABLE_ADDS = unmezzed adds that are NOT immune and NOT already controlled
            if (!inCombat || !hasAggro || numEnemies <= 1 || numControllableAdds == 0)
                return 0.0f;

            // Goal not applicable if mimic can't cast CC spells
            if (!canCastCC)
                return 0.0f;

            // Calculate base priority from controllable adds
            // Subtract 1 because main target (skull) doesn't need CC - tank handles it
            // This means: 2 enemies (1 add) = 0 priority, 3 enemies (2 adds) = 4.0 priority
            // Immune targets already excluded by MezzStatusSensor in NUM_CONTROLLABLE_ADDS
            float addsNeedingControl = numControllableAdds - 1;
            if (addsNeedingControl < 0)
                addsNeedingControl = 0;

            float priority = addsNeedingControl * PRIORITY_PER_ADD;

            // Apply escalation multiplier when adds exceed critical threshold
            // 4+ controllable adds = escalating danger requiring urgent CC
            if (numControllableAdds > MANY_ADDS_THRESHOLD)
            {
                priority *= MANY_ADDS_MULTIPLIER; // 2x multiplier
            }

            // Enforce minimum priority when adds are present
            // Ensures CC goal is always considered even with low add count
            if (priority > 0.0f && priority < MINIMUM_PRIORITY)
                priority = MINIMUM_PRIORITY;

            return priority;
        }

        /// <summary>
        /// Defines the desired world state when CC goal is satisfied
        /// Goal: All adds are crowd controlled (mezzed)
        /// </summary>
        /// <returns>Goal state with "addsControlled" = true</returns>
        /// <remarks>
        /// The planner will search for action sequences that set "addsControlled" to true.
        /// CC actions (CastSpellAction for mezz spells) set "targetControlled" effects,
        /// which when applied to all unmezzed adds, eventually satisfy "addsControlled".
        ///
        /// Goal satisfaction checked by IsGoalSatisfied() against NUM_UNMEZZED_ADDS <= 1.
        ///
        /// Note: This goal focuses on controlling adds (enemies beyond main target).
        /// Main target (skull) is handled by tank, so goal satisfied when only 1 enemy unmezzed.
        ///
        /// Mezz Maintenance:
        /// - Once all adds are mezzed, goal becomes satisfied (priority drops to 0.0)
        /// - MezzStatusSensor monitors mezz expiration times
        /// - If mezz expiring soon (<10s remaining), sensor updates world state
        /// - Priority recalculates, goal may reactivate to refresh expiring mezz
        /// - Result: Natural mezz maintenance without explicit refresh goal
        ///
        /// Critical DAoC Mechanic: NEVER break existing mezzes
        /// - CCCostCalculator applies 500% cost penalty to damaging mezzed targets
        /// - DPS goals check MEZZED_ENEMIES list and exclude those targets
        /// - Result: Group naturally avoids breaking mezzes, maximizing CC effectiveness
        /// </remarks>
        public override ReGoapState<string, object> GetGoalState()
        {
            var goalState = new ReGoapState<string, object>();

            // Desired end state: all adds are crowd controlled
            goalState.Set(ADDS_CONTROLLED, true);

            return goalState;
        }

        /// <summary>
        /// Checks if CC goal is currently satisfied
        /// Satisfied when all controllable adds are controlled (NUM_CONTROLLABLE_ADDS == 0)
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if all controllable adds controlled, false if controllable adds remain</returns>
        /// <remarks>
        /// Override default satisfaction check to use NUM_CONTROLLABLE_ADDS directly.
        /// More efficient than checking goal state match (avoids unnecessary world state comparisons).
        ///
        /// NUM_CONTROLLABLE_ADDS populated by MezzStatusSensor reading Brain.AggroList,
        /// checking IsMezzed status, and excluding CC_IMMUNE_ENEMIES list.
        /// Uses existing game effect tracking (no duplication).
        ///
        /// Satisfaction Logic:
        /// - NUM_CONTROLLABLE_ADDS == 0: All controllable adds controlled → goal satisfied
        /// - NUM_CONTROLLABLE_ADDS > 0: Controllable adds remain → goal not satisfied, need CC
        /// - Immune targets don't count (can't control them anyway)
        /// - Already-controlled targets don't count (already satisfied)
        ///
        /// Edge Cases:
        /// - No aggro (NUM_ENEMIES == 0): Goal considered satisfied (no adds to control)
        /// - Not in combat: Goal considered satisfied (CC only matters in combat)
        /// - Only 1 enemy total: Goal considered satisfied (no adds, only main target)
        /// - All adds mezzed: Goal satisfied (NUM_CONTROLLABLE_ADDS == 0)
        /// - All adds immune: Goal satisfied (NUM_CONTROLLABLE_ADDS == 0, can't CC them)
        /// - Mix of mezzed + immune: Goal satisfied if no controllable adds remain
        ///
        /// Immunity Interaction:
        /// - Immune targets excluded from NUM_CONTROLLABLE_ADDS
        /// - Goal satisfied even if immune adds remain (can't control them)
        /// - Focus only on adds that CAN be controlled
        ///
        /// Mezz Expiration Handling:
        /// - MezzStatusSensor monitors mezz expiration times
        /// - If mezz expires, target moves back to NUM_CONTROLLABLE_ADDS
        /// - Goal automatically re-activates (simple, effective refresh mechanism)
        ///
        /// Result: Natural CC goal satisfaction when all controllable adds are controlled
        /// </remarks>
        public override bool IsGoalSatisfied(ReGoapState<string, object> currentState)
        {
            bool inCombat = IsInCombat(currentState);
            bool hasAggro = GetBool(currentState, MimicWorldStateKeys.HAS_AGGRO, false);
            int numEnemies = GetNumEnemies(currentState);
            int numControllableAdds = GetInt(currentState, MimicWorldStateKeys.NUM_CONTROLLABLE_ADDS, 0);

            // Goal satisfied if not in combat, no aggro, or only 1 enemy (main target, no adds)
            if (!inCombat || !hasAggro || numEnemies <= 1)
                return true;

            // Goal satisfied when no controllable adds remain
            // This means: all adds are either controlled OR immune (can't control immune ones anyway)
            return numControllableAdds == 0;
        }

        /// <summary>
        /// Gets the name of this goal for debugging and logging
        /// </summary>
        /// <returns>Goal name string</returns>
        public override string GetName()
        {
            return "ControlAddsGoal";
        }

        /// <summary>
        /// Gets debug information including current priority, add counts, and CC status
        /// Used by /mimic debug command and logging
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>Debug string with priority, satisfaction, and CC details</returns>
        public override string GetDebugInfo(ReGoapState<string, object> currentState)
        {
            float priority = GetPriority(currentState);
            bool satisfied = IsGoalSatisfied(currentState);
            int numEnemies = GetNumEnemies(currentState);
            int numUnmezzedAdds = GetInt(currentState, MimicWorldStateKeys.NUM_UNMEZZED_ADDS, 0);
            int numUncontrolledAdds = GetInt(currentState, MimicWorldStateKeys.NUM_UNCONTROLLED_ADDS, 0);
            bool inCombat = IsInCombat(currentState);
            bool isMainCC = GetBool(currentState, MimicWorldStateKeys.IS_MAIN_CC, false);
            bool canCastCC = GetBool(currentState, MimicWorldStateKeys.CAN_CAST_CROWD_CONTROL, false);

            // Get mezzed/unmezzed lists for detailed debug info
            var mezzedEnemies = GetStateValue<object>(currentState, MimicWorldStateKeys.MEZZED_ENEMIES, null);
            var unmezzedAdds = GetStateValue<object>(currentState, MimicWorldStateKeys.UNMEZZED_ADDS, null);

            int numMezzed = mezzedEnemies != null ? (mezzedEnemies as System.Collections.ICollection)?.Count ?? 0 : 0;

            return $"{GetName()} (Priority: {priority:F2}, Satisfied: {satisfied}, " +
                   $"Enemies: {numEnemies}, UnmezzedAdds: {numUnmezzedAdds}, UncontrolledAdds: {numUncontrolledAdds}, " +
                   $"Mezzed: {numMezzed}, InCombat: {inCombat}, IsMainCC: {isMainCC}, CanCastCC: {canCastCC})";
        }
    }
}
