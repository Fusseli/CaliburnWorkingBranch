using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// Role-based goal for PacHealer mimics to provide backup healing when Aug Healer is overwhelmed
    /// Secondary healing goal with lower priority than primary healing (AugHealer responsibility)
    /// Activates when group members are injured and primary healer cannot keep up with healing demand
    /// </summary>
    /// <remarks>
    /// Design Principles (from design.md Component 4: Role-Based Goals):
    /// - PacHealer provides backup heals when Aug Healer is overwhelmed
    /// - Lower priority than PrimaryHealingGoal (Aug Healer's main goal)
    /// - Enables PacHealer to contribute healing during high-damage phases
    /// - Still allows PacHealer to prioritize CC/interrupts when healing is not critical
    ///
    /// Priority Formula (from daoc-role-analysis.md BackupHealGoal):
    /// base_priority = 3.0
    /// - Only activates when group members are injured (NUM_NEED_HEALING > 0)
    /// - Lower base priority than PrimaryHealingGoal (5.0) to avoid interfering with Aug Healer
    /// - Multipliers apply based on emergency conditions:
    ///   - If any member below 50% health: multiply by 10.0 (emergency response)
    ///   - If any member below 25% health: multiply by 50.0 (critical emergency)
    ///
    /// DAoC Role Context (from daoc-role-analysis.md):
    /// - PacHealer: Primary mezz/interrupt role + backup healing
    /// - AugHealer: Primary healing role (PrimaryHealingGoal with priority 5.0)
    /// - BackupHealGoal allows PacHealer to assist when damage exceeds Aug Healer's throughput
    /// - Example: Tank takes critical damage, both healers respond with emergency heals
    ///
    /// Organic Behavior Patterns:
    /// - PacHealer focuses on CC/interrupts when group is healthy (BackupHealGoal priority = 0)
    /// - When group takes moderate damage: Aug Healer heals (priority 5.0), PacHealer continues CC (priority 7.0+)
    /// - When group takes heavy damage: Both healers heal (BackupHealGoal × 10 = 30.0, overrides CC goals)
    /// - Emergency heals coordinated via group flags (AlreadyCastInstantHeal) to prevent duplicate casts
    ///
    /// World State Dependencies:
    /// - NUM_NEED_HEALING: Number of injured members (<75% HP) from MimicGroup.NumNeedHealing
    /// - NUM_EMERGENCY_HEALING: Number of emergency members (<50% HP) from MimicGroup.NumNeedEmergencyHealing
    /// - NUM_CRITICAL_HEALTH: Number of critical members (<25% HP)
    /// - GROUP_SIZE: Total group member count
    /// - IN_COMBAT: Combat state affects healing priorities
    ///
    /// Goal State: { "groupFullHealth": true }
    /// Satisfied when: All group members at full health (NUM_NEED_HEALING == 0)
    ///
    /// Integration Points:
    /// - GroupHealthSensor: Tracks group health via MimicGroup.NumNeedHealing (existing cached results)
    /// - GroupRoleSensor: Identifies Aug Healer status (future enhancement)
    /// - Healing actions: CastSpellAction for healing spells
    ///
    /// Reference: daoc-role-analysis.md "PacHealer Role - Backup Healing"
    /// Requirements: 3.2 (Role-Based Goals), 11.15-11.17 (Priority Formulas)
    /// Task: 43 - Create BackupHealGoal in custom/MimicNPC/ReGoap/Goals/BackupHealGoal.cs
    /// </remarks>
    public class BackupHealGoal : MimicGoal
    {
        /// <summary>
        /// Base priority for backup healing (lower than primary healing to avoid conflict)
        /// Value of 3.0 places backup healing below CC/interrupts but above general DPS
        /// </summary>
        /// <remarks>
        /// Priority Hierarchy (from daoc-role-analysis.md):
        /// - InterruptEnemyCasterGoal: 9.0 (highest priority for PacHealer)
        /// - MezzPriorityGoal: 7.0 (primary CC responsibility)
        /// - PrimaryHealingGoal: 5.0 (Aug Healer main goal)
        /// - BackupHealGoal: 3.0 (PacHealer backup healing)
        /// - DPS goals: 2.0-4.0 (damage dealing)
        ///
        /// Emergency multipliers elevate BackupHealGoal above other goals when critical:
        /// - Emergency (×10): 3.0 × 10 = 30.0 (overrides all non-emergency goals)
        /// - Critical (×50): 3.0 × 50 = 150.0 (absolute top priority)
        /// </remarks>
        private const float BASE_PRIORITY = 3.0f;

        /// <summary>
        /// Emergency multiplier when any group member below 50% health
        /// 10x multiplier elevates backup healing to high priority
        /// </summary>
        private const float EMERGENCY_MULTIPLIER = 10.0f;

        /// <summary>
        /// Critical emergency multiplier when any group member below 25% health
        /// 50x multiplier makes backup healing absolute top priority
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
        /// Constructs a new BackupHealGoal for a PacHealer mimic
        /// </summary>
        /// <param name="body">The MimicNPC body for game state access (via sensors)</param>
        /// <param name="brain">The MimicBrain for AI state access (via sensors)</param>
        public BackupHealGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        /// <summary>
        /// Calculates dynamic priority based on group health status from world state
        /// Priority is constant base (3.0) with emergency multipliers
        /// Lower than PrimaryHealingGoal to avoid interfering with Aug Healer
        /// </summary>
        /// <param name="currentState">Current world state populated by sensors reading Body/Brain</param>
        /// <returns>Priority value (higher = more urgent; 0.0 = not applicable)</returns>
        /// <remarks>
        /// Priority Calculation Logic:
        /// 1. Base: 3.0 (constant, lower than PrimaryHealingGoal's 5.0)
        /// 2. Emergency Conditions:
        ///    - Any member <25% HP: multiply by 50 (critical emergency)
        ///    - Any member <50% HP: multiply by 10 (emergency)
        /// 3. No injured members: priority = 0.0 (goal not applicable)
        ///
        /// Example Scenarios:
        /// - No injured members: priority = 0.0 (PacHealer focuses on CC/interrupts)
        /// - 1/8 members at 70% HP: priority = 3.0 (low, Aug Healer handles it)
        /// - 2/8 members at 45% HP: priority = 3.0 × 10 = 30.0 (emergency, PacHealer assists)
        /// - 1/8 members at 20% HP: priority = 3.0 × 50 = 150.0 (critical, all hands on deck)
        ///
        /// Result: PacHealer automatically assists when damage overwhelms Aug Healer
        ///         Normal healing → Aug Healer (priority 5.0+)
        ///         Emergency healing → Both healers (BackupHeal × 10 = 30.0 overrides CC)
        ///         Critical healing → All healers respond immediately (BackupHeal × 50 = 150.0)
        ///
        /// Organic Behavior Pattern:
        /// - Light damage: Aug Healer solo heals, PacHealer continues CC (BackupHeal 3.0 < MezzGoal 7.0)
        /// - Heavy AoE damage: Both healers respond (BackupHeal 30.0 > MezzGoal 7.0)
        /// - Tank spiked: All healers emergency heal (BackupHeal 150.0 > all other goals)
        /// </remarks>
        public override float GetPriority(ReGoapState<string, object> currentState)
        {
            // Read world state values populated by sensors
            int numInjured = GetNumInjured(currentState); // <75% HP
            int numEmergency = GetNumEmergency(currentState); // <50% HP
            int numCritical = GetNumCritical(currentState); // <25% HP

            // No injured members = goal not applicable
            // PacHealer should focus on CC/interrupts when group is healthy
            if (numInjured == 0)
                return 0.0f;

            // Base priority (constant, lower than primary healing)
            float priority = BASE_PRIORITY;

            // Apply emergency multipliers based on lowest health member
            if (numCritical > 0)
            {
                // Critical emergency: someone dying (<25% HP)
                // 50x multiplier makes this absolute top priority (150.0)
                // Overrides all CC, interrupts, and DPS goals
                priority *= CRITICAL_MULTIPLIER;
            }
            else if (numEmergency > 0)
            {
                // Emergency: someone in danger (<50% HP)
                // 10x multiplier elevates to high priority (30.0)
                // Overrides CC and interrupts, focuses on saving lives
                priority *= EMERGENCY_MULTIPLIER;
            }

            // Normal injured members (75-100% HP): priority = 3.0
            // Aug Healer's PrimaryHealingGoal (5.0+) will handle routine healing
            // PacHealer continues CC/interrupts (priority 7.0+)

            return priority;
        }

        /// <summary>
        /// Defines the desired world state when backup healing goal is satisfied
        /// Goal: All group members at full health
        /// </summary>
        /// <returns>Goal state with "groupFullHealth" = true</returns>
        /// <remarks>
        /// The planner will search for action sequences that set "groupFullHealth" to true.
        /// Healing actions (CastSpellAction for healing spells) set "targetHealed" effects,
        /// which when applied to all injured members, eventually satisfy "groupFullHealth".
        ///
        /// Goal satisfaction checked by IsGoalSatisfied() against NUM_NEED_HEALING == 0.
        ///
        /// Note: Same goal state as PrimaryHealingGoal - both healers work toward same outcome.
        /// Priority difference ensures Aug Healer handles routine healing, PacHealer handles emergencies.
        /// </remarks>
        public override ReGoapState<string, object> GetGoalState()
        {
            var goalState = new ReGoapState<string, object>();

            // Desired end state: group is at full health
            goalState.Set(MimicWorldStateKeys.GROUP_FULL_HEALTH, true);

            return goalState;
        }

        /// <summary>
        /// Checks if backup healing goal is currently satisfied
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
            return "BackupHealGoal";
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
