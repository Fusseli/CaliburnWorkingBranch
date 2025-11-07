using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// Emergency healing goal that overrides all other goals when group members are critically injured
    /// Activates when any group member drops below 50% health (emergency threshold)
    /// Shared across all healing roles (PacHealer, AugHealer) regardless of class
    /// </summary>
    /// <remarks>
    /// Design Principles (from design.md Component 4: Role-Based Goals):
    /// - Emergency goal has absolute priority over all other goals (including DPS, tank, CC)
    /// - Binary activation: either highest priority (100.0) or inactive (0.0)
    /// - Focuses healer attention exclusively on saving endangered group members
    /// - Multiple healers coordinate via group flags to prevent duplicate emergency heals
    ///
    /// Priority System (from requirements.md 11.16-11.17):
    /// - If NUM_EMERGENCY_HEALING > 0 (any member <50% HP): return 100.0 (absolute priority)
    /// - Else: return 0.0 (goal not applicable)
    ///
    /// Difference from HealGroupGoal:
    /// - HealGroupGoal: Graduated priority based on injury severity (routine healing)
    /// - EmergencyHealGoal: Binary absolute priority for life-threatening situations
    /// - EmergencyHealGoal takes precedence when both are active
    ///
    /// DAoC Context (from daoc-role-analysis.md):
    /// - 50% HP threshold is DAoC community standard for "emergency" status
    /// - Below 50% HP, players are vulnerable to burst damage combos
    /// - Emergency heals must land immediately to prevent deaths
    /// - Aug Healer: Primary emergency response (instant heals, large heals)
    /// - Pac Healer: Secondary emergency response (can assist if Aug Healer overwhelmed)
    ///
    /// Organic Behavior Patterns (from requirements.md 11.28):
    /// - When emergency goal activates, healers immediately prioritize instant/large heals
    /// - Action cost calculation favors fast, powerful heals for <50% HP targets (0.3x cost multiplier)
    /// - Emergency goal pre-empts all other activities (DPS stops, CC pauses, tank focuses defense)
    ///
    /// World State Dependencies:
    /// - NUM_EMERGENCY_HEALING: Number of members <50% HP from MimicGroup.NumNeedEmergencyHealing
    /// - NUM_CRITICAL_HEALTH: Number of members <25% HP (even more urgent)
    /// - GROUP_SIZE: Total group member count
    /// - IN_COMBAT: Combat state (emergencies more critical in combat)
    ///
    /// Goal State: { "noCriticalInjuries": true }
    /// Satisfied when: All group members above 50% health (NUM_EMERGENCY_HEALING == 0)
    ///
    /// Example Scenarios:
    /// - Tank drops to 48% HP during pull: EmergencyHealGoal activates (priority 100.0)
    /// - Healer immediately stops buffing, casts instant large heal
    /// - Tank restored to 65% HP: EmergencyHealGoal deactivates, HealGroupGoal takes over for topping off
    /// - Multiple members at 45% HP: Both healers activate emergency mode, coordinate via group flags
    /// </remarks>
    public class EmergencyHealGoal : MimicGoal
    {
        /// <summary>
        /// Absolute priority when emergency conditions exist
        /// 100.0 ensures this goal overrides all other goals (tank threat, DPS, CC, buffs)
        /// </summary>
        private const float EMERGENCY_PRIORITY = 100.0f;

        /// <summary>
        /// Emergency health threshold percentage (members below this trigger emergency response)
        /// DAoC standard: 50% HP is vulnerable to burst damage
        /// </summary>
        private const float EMERGENCY_HEALTH_THRESHOLD = 50.0f;

        /// <summary>
        /// Goal state key for emergency healing completion
        /// </summary>
        private const string NO_CRITICAL_INJURIES = "noCriticalInjuries";

        /// <summary>
        /// Constructs a new EmergencyHealGoal for a healer mimic
        /// </summary>
        /// <param name="body">The MimicNPC body for game state access (via sensors)</param>
        /// <param name="brain">The MimicBrain for AI state access (via sensors)</param>
        public EmergencyHealGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        /// <summary>
        /// Calculates priority based on emergency status
        /// Binary decision: absolute priority if emergencies exist, zero otherwise
        /// </summary>
        /// <param name="currentState">Current world state populated by sensors reading Body/Brain</param>
        /// <returns>100.0 if emergencies exist, 0.0 otherwise</returns>
        /// <remarks>
        /// Priority Logic:
        /// 1. Check NUM_EMERGENCY_HEALING (members below 50% HP)
        /// 2. If > 0: Return 100.0 (absolute priority, overrides all other goals)
        /// 3. If == 0: Return 0.0 (goal not applicable, defer to HealGroupGoal for routine healing)
        ///
        /// Why Absolute Priority (100.0)?
        /// - Tank deaths cause wipes (threat lost, enemies attack healers/DPS)
        /// - Healer deaths eliminate healing capacity (cascade failure)
        /// - DPS deaths reduce kill speed (longer fights = more danger)
        /// - Emergency healing must pre-empt everything else
        ///
        /// Example Scenarios:
        /// - Tank at 48% HP, 3 adds on aggro list: Emergency goal (100.0) beats tank threat goal (3.0)
        /// - Healer at 45% HP, enemy casting: Emergency goal (100.0) beats interrupt goal (9.0)
        /// - DPS at 40% HP, Main Assist calling target: Emergency goal (100.0) beats assist train goal (4.0)
        ///
        /// Result: Healers focus exclusively on saving endangered members until all above 50% HP
        /// </remarks>
        public override float GetPriority(ReGoapState<string, object> currentState)
        {
            // Read world state values populated by sensors
            int numEmergency = GetNumEmergency(currentState); // <50% HP
            int numCritical = GetNumCritical(currentState); // <25% HP (even more urgent)

            // If any group member is in emergency status (<50% HP), activate absolute priority
            if (numEmergency > 0)
            {
                return EMERGENCY_PRIORITY; // 100.0 - overrides all other goals
            }

            // No emergencies - goal not applicable
            // HealGroupGoal will handle routine healing (>50% HP members)
            return 0.0f;
        }

        /// <summary>
        /// Defines the desired world state when emergency goal is satisfied
        /// Goal: All group members above emergency threshold (50% HP)
        /// </summary>
        /// <returns>Goal state with "noCriticalInjuries" = true</returns>
        /// <remarks>
        /// The planner will search for action sequences that set "noCriticalInjuries" to true.
        /// Healing actions (CastSpellAction for healing spells) set "targetHealed" effects,
        /// which when applied to all emergency members, eventually satisfy "noCriticalInjuries".
        ///
        /// Goal satisfaction checked by IsGoalSatisfied() against NUM_EMERGENCY_HEALING == 0.
        ///
        /// Note: This goal focuses on emergency threshold (50% HP), not full health (100% HP).
        /// Once all members are above 50% HP, HealGroupGoal takes over for topping off.
        /// </remarks>
        public override ReGoapState<string, object> GetGoalState()
        {
            var goalState = new ReGoapState<string, object>();

            // Desired end state: no group members in emergency status (<50% HP)
            goalState.Set(NO_CRITICAL_INJURIES, true);

            return goalState;
        }

        /// <summary>
        /// Checks if emergency goal is currently satisfied
        /// Satisfied when no group members are in emergency status (all above 50% HP)
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if no emergencies, false if any member below 50% HP</returns>
        /// <remarks>
        /// Override default satisfaction check to use NUM_EMERGENCY_HEALING directly.
        /// More efficient than checking goal state match (avoids unnecessary world state comparisons).
        ///
        /// NUM_EMERGENCY_HEALING populated by GroupHealthSensor reading MimicGroup.NumNeedEmergencyHealing,
        /// which is calculated by existing CheckGroupHealth() logic (no duplication).
        ///
        /// Satisfaction Logic:
        /// - NUM_EMERGENCY_HEALING == 0: All members above 50% HP → goal satisfied
        /// - NUM_EMERGENCY_HEALING > 0: At least one member below 50% HP → goal not satisfied, continue healing
        /// </remarks>
        public override bool IsGoalSatisfied(ReGoapState<string, object> currentState)
        {
            int numEmergency = GetNumEmergency(currentState);
            return numEmergency == 0; // No emergencies = goal satisfied
        }

        /// <summary>
        /// Gets the name of this goal for debugging and logging
        /// </summary>
        /// <returns>Goal name string</returns>
        public override string GetName()
        {
            return "EmergencyHealGoal";
        }

        /// <summary>
        /// Gets debug information including current priority and emergency counts
        /// Used by /mimic debug command and logging
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>Debug string with priority, satisfaction, and emergency/critical counts</returns>
        public override string GetDebugInfo(ReGoapState<string, object> currentState)
        {
            float priority = GetPriority(currentState);
            bool satisfied = IsGoalSatisfied(currentState);
            int numEmergency = GetNumEmergency(currentState);
            int numCritical = GetNumCritical(currentState);
            int groupSize = GetGroupSize(currentState);
            bool inCombat = IsInCombat(currentState);

            return $"{GetName()} (Priority: {priority:F2}, Satisfied: {satisfied}, " +
                   $"Emergency: {numEmergency}/{groupSize}, Critical: {numCritical}/{groupSize}, " +
                   $"InCombat: {inCombat})";
        }
    }
}
