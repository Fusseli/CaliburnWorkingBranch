using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// Role-based goal for healer mimics to maintain group health
    /// Shared across all healing roles (PacHealer, AugHealer) regardless of class
    /// Priority scales dynamically based on group injury severity and emergency conditions
    /// </summary>
    /// <remarks>
    /// Design Principles (from design.md Component 4: Role-Based Goals):
    /// - Healer goal prioritizes keeping group members above health thresholds (50% emergency, 75% normal)
    /// - Multiple classes share same goal definitions (all healers use HealGroupGoal)
    /// - Priority dynamically calculated based on world state from sensors
    ///
    /// Priority Formula (from requirements.md 11.15-11.17):
    /// base_priority = 5.0 * (number_of_injured / group_size) * avg_health_deficit_percentage
    /// - If any member below 50% health: multiply by 10.0 (emergency response)
    /// - If any member below 25% health: multiply by 50.0 (critical emergency)
    ///
    /// DAoC Role Context (from daoc-role-analysis.md):
    /// - Aug Healer: Primary healing role, focuses on keeping group alive
    /// - Pac Healer: Secondary heals + mezz/interrupts (still evaluates this goal but lower base priority)
    ///
    /// Organic Behavior Patterns (from requirements.md 11.27-11.28):
    /// - Small efficient heals preferred for 60-75% health targets (via action cost calculation)
    /// - Large instant heals preferred for <50% health targets (emergency multiplier + action cost)
    /// - Multiple healers coordinate via group flags (AlreadyCastInstantHeal, AlreadyCastingHoT)
    ///
    /// World State Dependencies:
    /// - NUM_NEED_HEALING: Number of injured members (<75% HP) from MimicGroup.NumNeedHealing
    /// - NUM_EMERGENCY_HEALING: Number of emergency members (<50% HP) from MimicGroup.NumNeedEmergencyHealing
    /// - NUM_CRITICAL_HEALTH: Number of critical members (<25% HP)
    /// - GROUP_SIZE: Total group member count
    /// - AVG_HEALTH_DEFICIT_PERCENT: Average health deficit across injured members
    /// - IN_COMBAT: Combat state affects out-of-combat healing priorities
    ///
    /// Goal State: { "groupFullHealth": true }
    /// Satisfied when: All group members at full health (NUM_NEED_HEALING == 0)
    /// </remarks>
    public class HealGroupGoal : MimicGoal
    {
        /// <summary>
        /// Base priority multiplier for healing (balances against other role priorities)
        /// Value of 5.0 places healing at medium-high priority when multiple injured
        /// </summary>
        private const float BASE_PRIORITY = 5.0f;

        /// <summary>
        /// Emergency multiplier when any group member below 50% health
        /// 10x multiplier elevates healing above most other goals
        /// </summary>
        private const float EMERGENCY_MULTIPLIER = 10.0f;

        /// <summary>
        /// Critical emergency multiplier when any group member below 25% health
        /// 50x multiplier makes saving critical members absolute top priority
        /// </summary>
        private const float CRITICAL_MULTIPLIER = 50.0f;

        /// <summary>
        /// Emergency health threshold percentage (members below this trigger emergency response)
        /// </summary>
        private const float EMERGENCY_HEALTH_THRESHOLD = 50.0f;

        /// <summary>
        /// Critical health threshold percentage (members below this trigger critical response)
        /// </summary>
        private const float CRITICAL_HEALTH_THRESHOLD = 25.0f;

        /// <summary>
        /// Minimum priority floor to ensure healing goal is always considered
        /// Prevents priority from being zero when group is mostly healthy
        /// </summary>
        private const float MINIMUM_PRIORITY = 0.1f;

        /// <summary>
        /// Constructs a new HealGroupGoal for a healer mimic
        /// </summary>
        /// <param name="body">The MimicNPC body for game state access (via sensors)</param>
        /// <param name="brain">The MimicBrain for AI state access (via sensors)</param>
        public HealGroupGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        /// <summary>
        /// Calculates dynamic priority based on group health status from world state
        /// Priority scales with injury severity and applies emergency multipliers
        /// </summary>
        /// <param name="currentState">Current world state populated by sensors reading Body/Brain</param>
        /// <returns>Priority value (higher = more urgent; 0.0 = not applicable)</returns>
        /// <remarks>
        /// Priority Calculation Logic:
        /// 1. Base: 5.0 * (injured_ratio) * (avg_deficit_percent)
        ///    - injured_ratio = numInjured / groupSize (scales with group injury percentage)
        ///    - avg_deficit_percent = average health deficit (0.0-1.0) across injured members
        /// 2. Emergency Conditions:
        ///    - Any member <25% HP: multiply by 50 (critical emergency)
        ///    - Any member <50% HP: multiply by 10 (emergency)
        /// 3. Out of Combat: Normal priority (no reduction)
        ///
        /// Example Scenarios:
        /// - 1/8 members at 70% HP (5% deficit): priority = 5.0 * 0.125 * 0.05 = 0.03 (low)
        /// - 3/8 members at 60% HP (13% deficit): priority = 5.0 * 0.375 * 0.13 = 0.24 (low-medium)
        /// - 2/8 members at 45% HP (18% deficit): priority = 5.0 * 0.25 * 0.18 * 10 = 2.25 (high emergency)
        /// - 1/8 members at 20% HP (30% deficit): priority = 5.0 * 0.125 * 0.30 * 50 = 9.38 (critical emergency)
        ///
        /// Result: Organic triage behavior - routine healing is low priority, emergencies dominate
        /// </remarks>
        public override float GetPriority(ReGoapState<string, object> currentState)
        {
            // Read world state values populated by sensors
            int numInjured = GetNumInjured(currentState); // <75% HP
            int numEmergency = GetNumEmergency(currentState); // <50% HP
            int numCritical = GetNumCritical(currentState); // <25% HP
            int groupSize = GetGroupSize(currentState);
            float avgDeficitPercent = GetFloat(currentState, MimicWorldStateKeys.AVG_HEALTH_DEFICIT_PERCENT, 0.0f);
            bool inCombat = IsInCombat(currentState);

            // No injured members = goal already satisfied
            if (numInjured == 0 || groupSize == 0)
                return 0.0f;

            // Calculate base priority from injury ratio and average deficit
            // injured_ratio scales priority with group-wide injury (1 injured vs 4 injured)
            // avg_deficit scales priority with severity (5% deficit vs 30% deficit)
            float injuredRatio = (float)numInjured / (float)groupSize;
            float basePriority = BASE_PRIORITY * injuredRatio * avgDeficitPercent;

            // Apply emergency multipliers based on lowest health member
            float finalPriority = basePriority;

            if (numCritical > 0)
            {
                // Critical emergency: someone dying (<25% HP)
                // 50x multiplier makes this absolute top priority (dominates all other goals)
                finalPriority *= CRITICAL_MULTIPLIER;
            }
            else if (numEmergency > 0)
            {
                // Emergency: someone in danger (<50% HP)
                // 10x multiplier elevates above most goals (tank threat, DPS, CC)
                finalPriority *= EMERGENCY_MULTIPLIER;
            }

            // Out of combat healing still important (no reduction)
            // Ensures healers top off group between pulls

            // Enforce minimum priority to ensure goal is always evaluated
            // Prevents starvation when group is mostly healthy but 1-2 members need healing
            if (finalPriority > 0.0f && finalPriority < MINIMUM_PRIORITY)
                finalPriority = MINIMUM_PRIORITY;

            return finalPriority;
        }

        /// <summary>
        /// Defines the desired world state when healing goal is satisfied
        /// Goal: All group members at full health
        /// </summary>
        /// <returns>Goal state with "groupFullHealth" = true</returns>
        /// <remarks>
        /// The planner will search for action sequences that set "groupFullHealth" to true.
        /// Healing actions (CastSpellAction for healing spells) set "targetHealed" effects,
        /// which when applied to all injured members, eventually satisfy "groupFullHealth".
        ///
        /// Goal satisfaction checked by IsGoalSatisfied() against NUM_NEED_HEALING == 0.
        /// </remarks>
        public override ReGoapState<string, object> GetGoalState()
        {
            var goalState = new ReGoapState<string, object>();

            // Desired end state: group is at full health
            goalState.Set(MimicWorldStateKeys.GROUP_FULL_HEALTH, true);

            return goalState;
        }

        /// <summary>
        /// Checks if healing goal is currently satisfied
        /// Satisfied when no group members need healing (all above 75% HP)
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if no one needs healing, false otherwise</returns>
        /// <remarks>
        /// Override default satisfaction check to use NUM_NEED_HEALING directly.
        /// More efficient than checking goal state match (avoids unnecessary world state comparisons).
        ///
        /// NUM_NEED_HEALING populated by GroupHealthSensor reading MimicGroup.NumNeedHealing,
        /// which is calculated by existing CheckGroupHealth() logic (no duplication).
        /// </remarks>
        public override bool IsGoalSatisfied(ReGoapState<string, object> currentState)
        {
            int numInjured = GetNumInjured(currentState);
            return numInjured == 0; // No one needs healing = goal satisfied
        }

        /// <summary>
        /// Gets the name of this goal for debugging and logging
        /// </summary>
        /// <returns>Goal name string</returns>
        public override string GetName()
        {
            return "HealGroupGoal";
        }

        /// <summary>
        /// Gets debug information including current priority and satisfaction status
        /// Used by /mimic debug command and logging
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>Debug string with priority, satisfaction, and injury counts</returns>
        public override string GetDebugInfo(ReGoapState<string, object> currentState)
        {
            float priority = GetPriority(currentState);
            bool satisfied = IsGoalSatisfied(currentState);
            int numInjured = GetNumInjured(currentState);
            int numEmergency = GetNumEmergency(currentState);
            int numCritical = GetNumCritical(currentState);
            int groupSize = GetGroupSize(currentState);

            return $"{GetName()} (Priority: {priority:F2}, Satisfied: {satisfied}, " +
                   $"Injured: {numInjured}/{groupSize}, Emergency: {numEmergency}, Critical: {numCritical})";
        }
    }
}
