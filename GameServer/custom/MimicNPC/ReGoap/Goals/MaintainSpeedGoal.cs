using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// Support role goal for maintaining group speed buffs (Speed 5/6) during out-of-combat movement
    /// Priority #5 (6.0) based on DAoC RvR 8v8 meta analysis
    /// Critical for mobility between fights, positioning, and escape/chase scenarios
    /// Shared across support classes (Minstrel, Bard, Skald, etc.)
    /// </summary>
    /// <remarks>
    /// Design Principles (from design.md Component 4: Role-Based Goals):
    /// - MaintainSpeedGoal is critical for OUT OF COMBAT mobility in RvR
    /// - Speed 5/6 buffs provide 25% movement speed increase (positioning, escape, chase)
    /// - Speed typically drops when entering combat or taking damage
    /// - Goal maintains speed uptime when OUT OF COMBAT
    /// - IN COMBAT: Speed is low priority (focus on combat actions, let speed drop)
    /// - KITING SCENARIO: If regaining out-of-combat status, speed becomes critical again
    ///
    /// Priority Formula (from design.md + DAoC mechanics):
    /// Out of Combat:
    /// - Speed inactive: 6.0 * 100 = 600.0 (critical re-cast for mobility)
    /// - Speed active: 6.0 (maintain uptime)
    /// In Combat:
    /// - Speed inactive: 1.0 (low priority, don't waste GCDs)
    /// - Speed active: 0.5 (very low, let it fall off naturally)
    ///
    /// DAoC Speed Mechanic (actual behavior):
    /// - Speed 5/6 buffs: 25% movement speed increase
    /// - Duration: 10-15 minutes depending on spec
    /// - Drops on: Combat initiation, taking damage (implementation-dependent)
    /// - Primary use: Moving between fights, positioning, escape, chase
    /// - Combat use: Minimal (drops immediately, wastes cast time)
    /// - Kiting use: If you can kite long enough to drop combat, speed becomes critical
    ///
    /// DAoC RvR 8v8 Context (from daoc-role-analysis.md):
    /// - Pre-fight positioning: Speed allows group to choose engagement location
    /// - Escape: Speed enables group to disengage and flee after losing fight
    /// - Chase: Speed allows group to pursue fleeing enemies
    /// - In-fight: Speed irrelevant (drops on combat, focus on damage/heals/CC)
    /// - Kiting: Casters kite until dropping combat, then speed becomes critical
    ///
    /// Organic Behavior Patterns:
    /// - Out of combat without speed: Support immediately casts (600.0 emergency)
    /// - Out of combat with speed: Support maintains (6.0 periodic check)
    /// - In combat without speed: Support ignores (1.0 low priority, focus on combat)
    /// - In combat with speed: Support lets it drop (0.5 very low, don't maintain)
    /// - Kiting scenario: If dropping combat while kiting, speed becomes critical
    ///
    /// World State Dependencies (populated by SpeedSensor reading Body.EffectList):
    /// - GROUP_SPEED_ACTIVE: Speed buff currently active on group (Body.EffectList check)
    /// - GROUP_HAS_MELEE: Group contains melee classes (count > 0)
    /// - SPEED_CRITICAL: Speed inactive AND melee present (emergency condition)
    /// - IN_COMBAT: Combat state (primary driver for priority scaling)
    /// - OUT_OF_COMBAT_TIME: Seconds since last combat (kiting scenario detection)
    ///
    /// Goal State: { "speedMaintained": true }
    /// Satisfied when: (Speed active AND out of combat) OR (in combat - don't care)
    ///
    /// Example Scenarios:
    /// - Support out of combat, no speed: Priority = 600.0 (emergency re-cast)
    /// - Support out of combat, speed active: Priority = 6.0 (maintain)
    /// - Support in combat, no speed: Priority = 1.0 (low, focus on combat)
    /// - Support in combat, speed active: Priority = 0.5 (very low, let it drop)
    /// - Support kiting, drops combat: Priority = 600.0 (emergency re-cast for escape)
    ///
    /// Interaction with Other Goals:
    /// Out of Combat:
    /// - MaintainSpeedGoal (600.0 emergency) > All other out-of-combat goals
    /// - MaintainSpeedGoal (6.0) > BuffMaintenanceGoal (1.5)
    /// In Combat:
    /// - EmergencyHealGoal (50.0) > MaintainSpeedGoal (1.0)
    /// - DealDamageGoal (3.0) > MaintainSpeedGoal (1.0)
    /// - MaintainSpeedGoal (1.0) yields to all combat actions
    ///
    /// Result: Support maintains speed out of combat, ignores it during combat
    /// </remarks>
    public class MaintainSpeedGoal : MimicGoal
    {
        /// <summary>
        /// Base priority for maintaining speed buffs (out of combat)
        /// Value of 6.0 makes this high priority for out-of-combat scenarios
        /// </summary>
        private const float BASE_PRIORITY = 6.0f;

        /// <summary>
        /// Emergency multiplier when speed is inactive while out of combat
        /// 100x multiplier (6.0 * 100 = 600.0) forces immediate re-cast for mobility
        /// Ensures speed is cast immediately when leaving combat
        /// </summary>
        private const float EMERGENCY_SPEED_MULTIPLIER = 100.0f;

        /// <summary>
        /// In-combat priority when speed is inactive
        /// Low priority (1.0) - don't waste GCDs casting speed during combat
        /// Combat actions (damage, heals, CC) take precedence
        /// </summary>
        private const float IN_COMBAT_PRIORITY_INACTIVE = 1.0f;

        /// <summary>
        /// In-combat priority when speed is active
        /// Very low priority (0.5) - let speed drop naturally, don't maintain
        /// Speed will drop on damage anyway, no point maintaining it
        /// </summary>
        private const float IN_COMBAT_PRIORITY_ACTIVE = 0.5f;

        /// <summary>
        /// Goal state key for speed maintained
        /// </summary>
        private const string SPEED_MAINTAINED = "speedMaintained";

        /// <summary>
        /// Constructs a new MaintainSpeedGoal for a Support mimic
        /// </summary>
        /// <param name="body">The MimicNPC body for game state access (via SpeedSensor reading Body.EffectList)</param>
        /// <param name="brain">The MimicBrain for AI state access (combat status, group composition)</param>
        public MaintainSpeedGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        /// <summary>
        /// Calculates dynamic priority based on combat status and speed buff status
        /// OUT OF COMBAT: Speed is critical (600.0 if inactive, 6.0 if active)
        /// IN COMBAT: Speed is low priority (1.0 if inactive, 0.5 if active)
        /// </summary>
        /// <param name="currentState">Current world state populated by SpeedSensor reading Body.EffectList</param>
        /// <returns>Priority value (600.0 = emergency out-of-combat, 6.0 = maintain, 1.0 = in-combat low, 0.0 = N/A)</returns>
        /// <remarks>
        /// Priority Calculation Logic:
        ///
        /// OUT OF COMBAT (primary speed usage):
        /// 1. Speed inactive (SPEED_CRITICAL): 600.0 emergency
        ///    - Must cast speed immediately for mobility
        ///    - Group needs speed for positioning, escape, chase
        /// 2. Speed active: 6.0 maintain
        ///    - Keep checking to prevent expiration
        ///    - Maintain uptime for mobility
        ///
        /// IN COMBAT (speed mostly irrelevant):
        /// 1. Speed inactive: 1.0 low priority
        ///    - Don't waste cast time on speed during combat
        ///    - Focus on damage, heals, CC instead
        /// 2. Speed active: 0.5 very low priority
        ///    - Let speed drop naturally (will drop on damage anyway)
        ///    - Don't try to maintain it during combat
        ///
        /// KITING SCENARIO (special case):
        /// - If dropping combat while kiting (outOfCombatTime increasing):
        ///   Speed inactive → 600.0 (critical for escape)
        /// - This is automatically handled by the out-of-combat logic
        ///
        /// Example Scenarios:
        /// - Support traveling between fights, no speed: 600.0 (emergency cast)
        /// - Support traveling between fights, speed active: 6.0 (maintain)
        /// - Support in combat, no speed: 1.0 (low, focus on combat)
        /// - Support in combat, speed active: 0.5 (very low, let it drop)
        /// - Caster kiting, drops combat, no speed: 600.0 (emergency cast for escape)
        ///
        /// Result: Speed maintained out of combat, ignored during combat (correct DAoC behavior)
        /// </remarks>
        public override float GetPriority(ReGoapState<string, object> currentState)
        {
            // Read world state values populated by SpeedSensor and combat sensors
            int groupSize = GetGroupSize(currentState);
            bool groupSpeedActive = GetBool(currentState, MimicWorldStateKeys.GROUP_SPEED_ACTIVE, false);
            bool speedCritical = GetBool(currentState, MimicWorldStateKeys.SPEED_CRITICAL, false);
            bool inCombat = IsInCombat(currentState);

            // Note: We don't strictly require melee for speed goal
            // Speed is useful for ALL group members (mobility, positioning, escape)
            // Even caster-only groups benefit from speed out of combat

            // Solo scenarios: Still want speed for mobility
            // (Could add check for groupSize > 1 if we only want group speed)

            // PRIMARY BRANCH: OUT OF COMBAT
            // Speed is CRITICAL for mobility (positioning, escape, chase)
            if (!inCombat)
            {
                // Speed inactive: EMERGENCY priority for mobility
                if (speedCritical || !groupSpeedActive)
                {
                    return BASE_PRIORITY * EMERGENCY_SPEED_MULTIPLIER; // 600.0 emergency
                }
                // Speed active: HIGH priority to maintain uptime
                else
                {
                    return BASE_PRIORITY; // 6.0 maintain
                }
            }

            // SECONDARY BRANCH: IN COMBAT
            // Speed is LOW priority (drops on damage, wastes GCDs)
            else
            {
                // Speed inactive: Low priority (don't waste time casting it)
                if (speedCritical || !groupSpeedActive)
                {
                    return IN_COMBAT_PRIORITY_INACTIVE; // 1.0 low priority
                }
                // Speed active: Very low priority (let it drop naturally)
                else
                {
                    return IN_COMBAT_PRIORITY_ACTIVE; // 0.5 very low priority
                }
            }
        }

        /// <summary>
        /// Defines the desired world state when speed maintenance goal is satisfied
        /// Goal: Group has active speed buff when OUT OF COMBAT
        /// </summary>
        /// <returns>Goal state with "speedMaintained" = true</returns>
        /// <remarks>
        /// The planner will search for action sequences that set "speedMaintained" to true.
        /// Speed buff actions (CastSpeedSpellAction) set "speedMaintained" effect when executed on group.
        ///
        /// Goal satisfaction is context-dependent:
        /// - Out of combat: Satisfied only if speed active
        /// - In combat: Always satisfied (don't try to maintain speed during combat)
        ///
        /// This allows the goal to have high priority out of combat but effectively disable
        /// itself during combat (other goals will be higher priority and this goal is satisfied).
        /// </remarks>
        public override ReGoapState<string, object> GetGoalState()
        {
            var goalState = new ReGoapState<string, object>();

            // Desired end state: speed buff maintained (when out of combat)
            goalState.Set(SPEED_MAINTAINED, true);

            return goalState;
        }

        /// <summary>
        /// Checks if speed maintenance goal is currently satisfied
        /// OUT OF COMBAT: Satisfied only if speed active
        /// IN COMBAT: Always satisfied (don't maintain speed during combat)
        /// </summary>
        /// <param name="currentState">Current world state from SpeedSensor</param>
        /// <returns>True if speed maintained (or in combat), false if speed needed out of combat</returns>
        /// <remarks>
        /// Satisfaction Logic:
        ///
        /// OUT OF COMBAT:
        /// - Speed active: Satisfied (goal met)
        /// - Speed inactive: NOT satisfied (need to cast speed)
        ///
        /// IN COMBAT:
        /// - Always satisfied (don't care about speed during combat)
        /// - This allows combat goals to take precedence
        ///
        /// This creates the correct behavior:
        /// - Out of combat: Goal unsatisfied → plan to cast speed (high priority 600.0)
        /// - In combat: Goal satisfied → don't plan speed actions (focus on combat)
        /// - Kiting scenario: Drop combat → goal unsatisfied → cast speed immediately (600.0)
        ///
        /// Edge Cases:
        /// - Speed about to expire: Still satisfied (sensor doesn't predict future)
        /// - Entering combat: Goal becomes satisfied (stop maintaining speed)
        /// - Leaving combat: Goal becomes unsatisfied if no speed (cast immediately)
        /// </remarks>
        public override bool IsGoalSatisfied(ReGoapState<string, object> currentState)
        {
            bool groupSpeedActive = GetBool(currentState, MimicWorldStateKeys.GROUP_SPEED_ACTIVE, false);
            bool inCombat = IsInCombat(currentState);

            // IN COMBAT: Always satisfied (don't maintain speed during combat)
            if (inCombat)
                return true;

            // OUT OF COMBAT: Satisfied only if speed active
            return groupSpeedActive;
        }

        /// <summary>
        /// Gets the name of this goal for debugging and logging
        /// </summary>
        /// <returns>Goal name string</returns>
        public override string GetName()
        {
            return "MaintainSpeedGoal";
        }

        /// <summary>
        /// Gets debug information including current priority, speed status, and combat state
        /// Used by /mimic debug command and logging
        /// </summary>
        /// <param name="currentState">Current world state from SpeedSensor</param>
        /// <returns>Debug string with priority, satisfaction, and speed details</returns>
        public override string GetDebugInfo(ReGoapState<string, object> currentState)
        {
            float priority = GetPriority(currentState);
            bool satisfied = IsGoalSatisfied(currentState);
            int groupSize = GetGroupSize(currentState);
            bool groupSpeedActive = GetBool(currentState, MimicWorldStateKeys.GROUP_SPEED_ACTIVE, false);
            bool groupHasMelee = GetBool(currentState, MimicWorldStateKeys.GROUP_HAS_MELEE, false);
            bool speedCritical = GetBool(currentState, MimicWorldStateKeys.SPEED_CRITICAL, false);
            bool inCombat = IsInCombat(currentState);
            float outOfCombatTime = GetOutOfCombatTime(currentState);

            return $"{GetName()} (Priority: {priority:F2}, Satisfied: {satisfied}, " +
                   $"SpeedActive: {groupSpeedActive}, HasMelee: {groupHasMelee}, " +
                   $"SpeedCritical: {speedCritical}, GroupSize: {groupSize}, " +
                   $"InCombat: {inCombat}, OutOfCombatTime: {outOfCombatTime:F1}s)";
        }
    }
}
