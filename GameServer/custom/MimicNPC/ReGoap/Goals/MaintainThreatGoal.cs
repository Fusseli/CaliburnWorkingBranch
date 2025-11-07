using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// Tank role goal for maintaining threat on all enemies in aggro list
    /// Shared across all tank classes (Armsman, Warrior, Hero, etc.) regardless of realm
    /// Priority scales dynamically based on enemies not targeting the tank and threat loss
    /// </summary>
    /// <remarks>
    /// Design Principles (from design.md Component 4: Role-Based Goals):
    /// - Tank goal prioritizes maintaining threat on current target and protecting group members
    /// - Multiple tank classes share same goal definition (all MainTanks use MaintainThreatGoal)
    /// - Priority dynamically calculated based on world state from sensors
    ///
    /// Priority Formula (from requirements.md 11.18-11.19):
    /// base_priority = 3.0 + (number_of_enemies_not_on_tank * 2.0)
    /// - If tank's threat on current target drops below 75% of highest threat: multiply by 3.0
    ///
    /// DAoC Tank Context (from daoc-role-analysis.md):
    /// - MainTank: Primary role is threat generation and damage mitigation
    /// - Guard ability: Redirects healer damage to tank (must maintain at all times)
    /// - Threat management: Tank must maintain highest threat to prevent enemies attacking squishies
    /// - If enemies attack healers/DPS: Group wipes due to burst damage on unarmored targets
    ///
    /// Organic Behavior Patterns (from requirements.md 11.31-11.32):
    /// - When tank has solid aggro: Offensive abilities preferred to maximize threat lead
    /// - When tank is losing threat: High-threat abilities and taunts strongly preferred
    /// - Action cost calculation favors high-threat abilities (base_cost = 100 / (threat + 1))
    ///
    /// World State Dependencies:
    /// - NUM_ENEMIES: Total number of enemies on aggro list from Brain.AggroList.Count
    /// - NUM_ENEMIES_NOT_ON_TANK: Enemies targeting someone other than the tank
    /// - THREAT_PERCENT_OF_HIGHEST: Tank's threat as percentage of highest threat on current target
    /// - HAS_AGGRO: Whether tank has any enemies on aggro list
    /// - IN_COMBAT: Combat state (goal only active in combat)
    /// - IS_MAIN_TANK: Role assignment (goal only active for MainTank role)
    ///
    /// Goal State: { "threatEstablished": true, "enemiesOnTank": true }
    /// Satisfied when: Tank has highest threat on all enemies (NUM_ENEMIES_NOT_ON_TANK == 0)
    ///
    /// Example Scenarios:
    /// - Tank pulls 3 enemies: Priority = 3.0 + (3 * 2.0) = 9.0 (high priority to establish threat)
    /// - Tank loses threat on 1 enemy: Priority = 3.0 + (1 * 2.0) = 5.0 (moderate priority)
    /// - Tank's threat at 70% of DPS: Priority = 5.0 * 3.0 = 15.0 (critical priority to regain threat)
    /// - Tank has solid aggro (all enemies on tank): Priority = 3.0 (low priority, maintain status quo)
    ///
    /// Result: Tank naturally focuses on threat generation when losing aggro, relaxes when secure
    /// </remarks>
    public class MaintainThreatGoal : MimicGoal
    {
        /// <summary>
        /// Base priority for tank threat management
        /// Value of 3.0 places threat management at medium priority baseline
        /// </summary>
        private const float BASE_PRIORITY = 3.0f;

        /// <summary>
        /// Priority added per enemy not targeting the tank
        /// Value of 2.0 per enemy makes threat loss escalate quickly
        /// </summary>
        private const float PRIORITY_PER_LOOSE_ENEMY = 2.0f;

        /// <summary>
        /// Multiplier when tank is losing threat on current target
        /// 3x multiplier makes regaining threat a high priority
        /// </summary>
        private const float LOSING_THREAT_MULTIPLIER = 3.0f;

        /// <summary>
        /// Threat percentage threshold below which tank is considered "losing threat"
        /// 75% means tank must maintain at least 75% of the highest threat to feel secure
        /// </summary>
        private const float THREAT_LOSS_THRESHOLD = 0.75f;

        /// <summary>
        /// Minimum priority floor to ensure threat goal is always evaluated when in combat
        /// </summary>
        private const float MINIMUM_PRIORITY = 0.5f;

        /// <summary>
        /// Goal state key for threat establishment
        /// </summary>
        private const string THREAT_ESTABLISHED = "threatEstablished";

        /// <summary>
        /// Goal state key for enemies targeting tank
        /// </summary>
        private const string ENEMIES_ON_TANK = "enemiesOnTank";

        /// <summary>
        /// Constructs a new MaintainThreatGoal for a tank mimic
        /// </summary>
        /// <param name="body">The MimicNPC body for game state access (via sensors)</param>
        /// <param name="brain">The MimicBrain for AI state access (via sensors)</param>
        public MaintainThreatGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        /// <summary>
        /// Calculates dynamic priority based on threat status from world state
        /// Priority scales with number of enemies not on tank and threat loss on current target
        /// </summary>
        /// <param name="currentState">Current world state populated by sensors reading Body/Brain</param>
        /// <returns>Priority value (higher = more urgent; 0.0 = not applicable)</returns>
        /// <remarks>
        /// Priority Calculation Logic:
        /// 1. Base: 3.0 + (numEnemiesNotOnTank * 2.0)
        ///    - Base of 3.0 represents minimum threat management priority
        ///    - Each enemy not on tank adds 2.0 priority (escalating urgency)
        /// 2. Threat Loss Condition:
        ///    - If tank's threat < 75% of highest threat on current target: multiply by 3.0
        ///    - Represents critical threat loss requiring immediate action
        /// 3. Not in Combat: Return 0.0 (threat only matters in combat)
        /// 4. No Aggro: Return 0.0 (no enemies to generate threat on)
        ///
        /// Example Scenarios:
        /// - Pull 1 enemy, solid threat: 3.0 + (0 * 2.0) = 3.0 (baseline)
        /// - Pull 3 enemies, all on tank: 3.0 + (0 * 2.0) = 3.0 (stable)
        /// - Pull 3 enemies, 1 attacking healer: 3.0 + (1 * 2.0) = 5.0 (moderate urgency)
        /// - Pull 3 enemies, 2 attacking DPS: 3.0 + (2 * 2.0) = 7.0 (high urgency)
        /// - Tank at 70% threat (below 75%): 5.0 * 3.0 = 15.0 (critical - losing threat)
        /// - Tank at 80% threat (above 75%): 5.0 (normal - maintaining lead)
        ///
        /// Result: Tank prioritizes threat generation when enemies attack group members or when losing threat
        /// </remarks>
        public override float GetPriority(ReGoapState<string, object> currentState)
        {
            // Read world state values populated by sensors
            bool inCombat = IsInCombat(currentState);
            bool hasAggro = GetBool(currentState, MimicWorldStateKeys.HAS_AGGRO, false);
            int numEnemies = GetNumEnemies(currentState);
            int numEnemiesNotOnTank = GetInt(currentState, MimicWorldStateKeys.NUM_ENEMIES_NOT_ON_TANK, 0);
            float threatPercent = GetFloat(currentState, MimicWorldStateKeys.THREAT_PERCENT_OF_HIGHEST, 1.0f);
            bool isMainTank = GetBool(currentState, MimicWorldStateKeys.IS_MAIN_TANK, false);

            // Goal only applies to MainTank role in combat with enemies
            if (!inCombat || !hasAggro || !isMainTank || numEnemies == 0)
                return 0.0f;

            // Calculate base priority from loose enemies
            // Base 3.0 + 2.0 per enemy not on tank
            float priority = BASE_PRIORITY + (numEnemiesNotOnTank * PRIORITY_PER_LOOSE_ENEMY);

            // Apply threat loss multiplier if losing threat on current target
            // Below 75% threat = critical threat loss requiring immediate action
            if (threatPercent < THREAT_LOSS_THRESHOLD)
            {
                priority *= LOSING_THREAT_MULTIPLIER; // 3x multiplier
            }

            // Enforce minimum priority when in combat with aggro
            // Ensures threat management is always considered even when secure
            if (priority > 0.0f && priority < MINIMUM_PRIORITY)
                priority = MINIMUM_PRIORITY;

            return priority;
        }

        /// <summary>
        /// Defines the desired world state when threat goal is satisfied
        /// Goal: Tank has highest threat on all enemies
        /// </summary>
        /// <returns>Goal state with "threatEstablished" = true and "enemiesOnTank" = true</returns>
        /// <remarks>
        /// The planner will search for action sequences that set both goal state flags to true.
        /// Threat-generating actions (taunt styles, high-threat abilities, defensive stance) set "threatGenerated" effects,
        /// which when applied to all enemies, eventually satisfy "threatEstablished" and "enemiesOnTank".
        ///
        /// Goal satisfaction checked by IsGoalSatisfied() against NUM_ENEMIES_NOT_ON_TANK == 0.
        ///
        /// Note: This goal focuses on threat establishment, not damage output.
        /// Once threat is secure, DPS goals can take precedence for faster kills.
        /// </remarks>
        public override ReGoapState<string, object> GetGoalState()
        {
            var goalState = new ReGoapState<string, object>();

            // Desired end state: tank has established threat on all enemies
            goalState.Set(THREAT_ESTABLISHED, true);
            goalState.Set(ENEMIES_ON_TANK, true);

            return goalState;
        }

        /// <summary>
        /// Checks if threat goal is currently satisfied
        /// Satisfied when all enemies are targeting the tank (NUM_ENEMIES_NOT_ON_TANK == 0)
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if all enemies on tank, false if any enemies attacking group members</returns>
        /// <remarks>
        /// Override default satisfaction check to use NUM_ENEMIES_NOT_ON_TANK directly.
        /// More efficient than checking goal state match (avoids unnecessary world state comparisons).
        ///
        /// NUM_ENEMIES_NOT_ON_TANK populated by AggroSensor analyzing Brain.AggroList,
        /// which tracks threat values for all enemies (existing game system, no duplication).
        ///
        /// Satisfaction Logic:
        /// - NUM_ENEMIES_NOT_ON_TANK == 0: All enemies targeting tank → goal satisfied
        /// - NUM_ENEMIES_NOT_ON_TANK > 0: Some enemies attacking group members → goal not satisfied, generate threat
        ///
        /// Edge Cases:
        /// - No aggro (NUM_ENEMIES == 0): Goal considered satisfied (no threat to maintain)
        /// - Not in combat: Goal considered satisfied (threat only matters in combat)
        /// </remarks>
        public override bool IsGoalSatisfied(ReGoapState<string, object> currentState)
        {
            bool inCombat = IsInCombat(currentState);
            bool hasAggro = GetBool(currentState, MimicWorldStateKeys.HAS_AGGRO, false);
            int numEnemiesNotOnTank = GetInt(currentState, MimicWorldStateKeys.NUM_ENEMIES_NOT_ON_TANK, 0);

            // Goal satisfied if not in combat, no aggro, or all enemies on tank
            if (!inCombat || !hasAggro)
                return true;

            return numEnemiesNotOnTank == 0; // All enemies on tank = goal satisfied
        }

        /// <summary>
        /// Gets the name of this goal for debugging and logging
        /// </summary>
        /// <returns>Goal name string</returns>
        public override string GetName()
        {
            return "MaintainThreatGoal";
        }

        /// <summary>
        /// Gets debug information including current priority, threat status, and enemy counts
        /// Used by /mimic debug command and logging
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>Debug string with priority, satisfaction, and threat details</returns>
        public override string GetDebugInfo(ReGoapState<string, object> currentState)
        {
            float priority = GetPriority(currentState);
            bool satisfied = IsGoalSatisfied(currentState);
            int numEnemies = GetNumEnemies(currentState);
            int numEnemiesNotOnTank = GetInt(currentState, MimicWorldStateKeys.NUM_ENEMIES_NOT_ON_TANK, 0);
            float threatPercent = GetFloat(currentState, MimicWorldStateKeys.THREAT_PERCENT_OF_HIGHEST, 1.0f);
            bool inCombat = IsInCombat(currentState);
            bool isMainTank = GetBool(currentState, MimicWorldStateKeys.IS_MAIN_TANK, false);

            return $"{GetName()} (Priority: {priority:F2}, Satisfied: {satisfied}, " +
                   $"Enemies: {numEnemies}, NotOnTank: {numEnemiesNotOnTank}, " +
                   $"ThreatPercent: {threatPercent:F2}, InCombat: {inCombat}, IsMainTank: {isMainTank})";
        }
    }
}
