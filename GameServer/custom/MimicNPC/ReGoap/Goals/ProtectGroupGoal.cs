using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// Tank role goal for protecting group members from enemy attacks ("peeling")
    /// Shared across all tank classes (Armsman, Warrior, Hero, etc.) regardless of realm
    /// Priority scales dynamically based on number of enemies attacking non-tank group members
    /// </summary>
    /// <remarks>
    /// Design Principles (from design.md Component 4: Role-Based Goals):
    /// - Tank goal prioritizes protecting group members by peeling enemies off squishies
    /// - Multiple tank classes share same goal definition (all MainTanks can use ProtectGroupGoal)
    /// - Priority dynamically calculated based on world state from sensors
    /// - Works in coordination with MaintainThreatGoal (threat generation) but focuses on reactive peeling
    ///
    /// Priority Formula (derived from design.md Component 4):
    /// base_priority = 4.0 + (number_of_enemies_attacking_group * 3.0)
    /// - If healer is being attacked: multiply by 2.0 (healers are highest priority to protect)
    /// - If multiple enemies attacking backline: urgency escalates quickly
    ///
    /// DAoC Tank Context (from daoc-role-analysis.md):
    /// - MainTank: Secondary role is peeling enemies off vulnerable group members
    /// - Guard ability: Tank should guard healer (separate from threat management)
    /// - Peel mechanics: Tank uses taunts, stuns, or snares to pull enemies off allies
    /// - If enemies kill healers/DPS: Group wipes due to lost healing or insufficient damage
    ///
    /// Difference from MaintainThreatGoal:
    /// - MaintainThreatGoal: Proactive threat generation to hold initial aggro
    /// - ProtectGroupGoal: Reactive protection when enemies break threat and attack allies
    /// - MaintainThreatGoal focuses on current target, ProtectGroupGoal focuses on loose adds
    ///
    /// Organic Behavior Patterns (from requirements.md 11.31-11.32):
    /// - When all enemies on tank: ProtectGroupGoal has low priority (protection not needed)
    /// - When adds attack healer: ProtectGroupGoal spikes to high priority (critical protection)
    /// - Action cost calculation favors taunt/peel abilities when protection goal is active
    ///
    /// World State Dependencies:
    /// - NUM_ENEMIES_NOT_ON_TANK: Enemies targeting someone other than the tank (from AggroSensor)
    /// - NUM_ENEMIES: Total number of enemies on aggro list from Brain.AggroList.Count
    /// - HAS_AGGRO: Whether tank has any enemies on aggro list
    /// - IN_COMBAT: Combat state (goal only active in combat)
    /// - IS_MAIN_TANK: Role assignment (goal only active for MainTank role)
    /// - HEALER_UNDER_ATTACK: Boolean flag indicating healer is being targeted
    ///
    /// Goal State: { "groupProtected": true, "enemiesPeeled": true }
    /// Satisfied when: All enemies are targeting the tank (NUM_ENEMIES_NOT_ON_TANK == 0)
    ///
    /// Example Scenarios:
    /// - Tank pulls 1 enemy, solid threat: Priority = 0.0 (no protection needed)
    /// - Tank pulls 3 enemies, 1 breaks to attack DPS: Priority = 4.0 + (1 * 3.0) = 7.0 (high priority)
    /// - Tank pulls 3 enemies, 2 attack healer: Priority = (4.0 + (2 * 3.0)) * 2.0 = 20.0 (critical priority)
    /// - Tank pulls 5 enemies, 3 attack backline: Priority = 4.0 + (3 * 3.0) = 13.0 (very high priority)
    ///
    /// Result: Tank naturally focuses on peeling enemies when allies are in danger
    /// </remarks>
    public class ProtectGroupGoal : MimicGoal
    {
        /// <summary>
        /// Base priority for group protection
        /// Value of 4.0 places protection at medium-high priority baseline (higher than general threat)
        /// </summary>
        private const float BASE_PRIORITY = 4.0f;

        /// <summary>
        /// Priority added per enemy attacking group members
        /// Value of 3.0 per enemy makes protection urgency escalate quickly
        /// </summary>
        private const float PRIORITY_PER_ATTACKING_ENEMY = 3.0f;

        /// <summary>
        /// Multiplier when healer is under attack
        /// 2x multiplier makes protecting healers top priority (healers keep group alive)
        /// </summary>
        private const float HEALER_UNDER_ATTACK_MULTIPLIER = 2.0f;

        /// <summary>
        /// Minimum priority floor when enemies are attacking group
        /// Ensures protection goal is always evaluated when allies are endangered
        /// </summary>
        private const float MINIMUM_PRIORITY = 1.0f;

        /// <summary>
        /// Goal state key for group protection
        /// </summary>
        private const string GROUP_PROTECTED = "groupProtected";

        /// <summary>
        /// Goal state key for enemies peeled off allies
        /// </summary>
        private const string ENEMIES_PEELED = "enemiesPeeled";

        /// <summary>
        /// Constructs a new ProtectGroupGoal for a tank mimic
        /// </summary>
        /// <param name="body">The MimicNPC body for game state access (via sensors)</param>
        /// <param name="brain">The MimicBrain for AI state access (via sensors)</param>
        public ProtectGroupGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        /// <summary>
        /// Calculates dynamic priority based on enemies attacking group members
        /// Priority scales with number of enemies attacking non-tank targets
        /// </summary>
        /// <param name="currentState">Current world state populated by sensors reading Body/Brain</param>
        /// <returns>Priority value (higher = more urgent; 0.0 = not applicable)</returns>
        /// <remarks>
        /// Priority Calculation Logic:
        /// 1. Base: 4.0 + (numEnemiesAttackingGroup * 3.0)
        ///    - Base of 4.0 represents baseline protection priority (higher than general maintenance)
        ///    - Each enemy attacking allies adds 3.0 priority (rapid escalation)
        /// 2. Healer Under Attack Condition:
        ///    - If healer being attacked: multiply by 2.0 (healers are highest protection priority)
        /// 3. No Enemies Attacking Group: Return 0.0 (protection not needed)
        /// 4. Not in Combat: Return 0.0 (protection only matters in combat)
        ///
        /// Example Scenarios:
        /// - All enemies on tank: 0.0 (no protection needed)
        /// - 1 enemy attacks DPS: 4.0 + (1 * 3.0) = 7.0 (high priority)
        /// - 1 enemy attacks healer: 7.0 * 2.0 = 14.0 (critical priority)
        /// - 2 enemies attack DPS: 4.0 + (2 * 3.0) = 10.0 (very high priority)
        /// - 2 enemies attack healer: 10.0 * 2.0 = 20.0 (emergency priority)
        /// - 3 enemies loose on backline: 4.0 + (3 * 3.0) = 13.0 (critical situation)
        ///
        /// Result: Tank prioritizes peeling when allies are endangered, especially healers
        /// </remarks>
        public override float GetPriority(ReGoapState<string, object> currentState)
        {
            // Read world state values populated by sensors
            bool inCombat = IsInCombat(currentState);
            bool hasAggro = GetBool(currentState, MimicWorldStateKeys.HAS_AGGRO, false);
            int numEnemies = GetNumEnemies(currentState);
            int numEnemiesNotOnTank = GetInt(currentState, MimicWorldStateKeys.NUM_ENEMIES_NOT_ON_TANK, 0);
            bool healerUnderAttack = GetBool(currentState, MimicWorldStateKeys.HEALER_UNDER_ATTACK, false);
            bool isMainTank = GetBool(currentState, MimicWorldStateKeys.IS_MAIN_TANK, false);

            // Goal only applies to MainTank role in combat with enemies
            if (!inCombat || !hasAggro || !isMainTank || numEnemies == 0)
                return 0.0f;

            // If all enemies are on tank, no protection needed
            if (numEnemiesNotOnTank == 0)
                return 0.0f;

            // Calculate base priority from enemies attacking group members
            // Base 4.0 + 3.0 per enemy attacking allies
            float priority = BASE_PRIORITY + (numEnemiesNotOnTank * PRIORITY_PER_ATTACKING_ENEMY);

            // Apply healer protection multiplier if healer is under attack
            // Healers are highest priority to protect (group wipes without healing)
            if (healerUnderAttack)
            {
                priority *= HEALER_UNDER_ATTACK_MULTIPLIER; // 2x multiplier
            }

            // Enforce minimum priority when enemies attacking group
            // Ensures protection is always considered when allies are endangered
            if (priority > 0.0f && priority < MINIMUM_PRIORITY)
                priority = MINIMUM_PRIORITY;

            return priority;
        }

        /// <summary>
        /// Defines the desired world state when protection goal is satisfied
        /// Goal: All enemies targeting tank, no allies under attack
        /// </summary>
        /// <returns>Goal state with "groupProtected" = true and "enemiesPeeled" = true</returns>
        /// <remarks>
        /// The planner will search for action sequences that set both goal state flags to true.
        /// Peel actions (taunt abilities, stuns, snares, interrupts) set "enemyPeeled" effects,
        /// which when applied to all loose enemies, eventually satisfy "groupProtected" and "enemiesPeeled".
        ///
        /// Goal satisfaction checked by IsGoalSatisfied() against NUM_ENEMIES_NOT_ON_TANK == 0.
        ///
        /// Note: This goal focuses on immediate protection (peeling), not sustained threat management.
        /// Once enemies are peeled back to tank, MaintainThreatGoal takes over for long-term threat.
        /// </remarks>
        public override ReGoapState<string, object> GetGoalState()
        {
            var goalState = new ReGoapState<string, object>();

            // Desired end state: all enemies peeled back to tank, group protected
            goalState.Set(GROUP_PROTECTED, true);
            goalState.Set(ENEMIES_PEELED, true);

            return goalState;
        }

        /// <summary>
        /// Checks if protection goal is currently satisfied
        /// Satisfied when no enemies are attacking group members (NUM_ENEMIES_NOT_ON_TANK == 0)
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if no enemies attacking group, false if any enemies on allies</returns>
        /// <remarks>
        /// Override default satisfaction check to use NUM_ENEMIES_NOT_ON_TANK directly.
        /// More efficient than checking goal state match (avoids unnecessary world state comparisons).
        ///
        /// NUM_ENEMIES_NOT_ON_TANK populated by AggroSensor analyzing Brain.AggroList,
        /// which tracks threat values for all enemies (existing game system, no duplication).
        ///
        /// Satisfaction Logic:
        /// - NUM_ENEMIES_NOT_ON_TANK == 0: All enemies on tank → goal satisfied
        /// - NUM_ENEMIES_NOT_ON_TANK > 0: Some enemies attacking group → goal not satisfied, peel enemies
        ///
        /// Edge Cases:
        /// - No aggro (NUM_ENEMIES == 0): Goal considered satisfied (no enemies to peel)
        /// - Not in combat: Goal considered satisfied (protection only matters in combat)
        /// </remarks>
        public override bool IsGoalSatisfied(ReGoapState<string, object> currentState)
        {
            bool inCombat = IsInCombat(currentState);
            bool hasAggro = GetBool(currentState, MimicWorldStateKeys.HAS_AGGRO, false);
            int numEnemiesNotOnTank = GetInt(currentState, MimicWorldStateKeys.NUM_ENEMIES_NOT_ON_TANK, 0);

            // Goal satisfied if not in combat, no aggro, or all enemies on tank
            if (!inCombat || !hasAggro)
                return true;

            return numEnemiesNotOnTank == 0; // All enemies on tank = group protected
        }

        /// <summary>
        /// Gets the name of this goal for debugging and logging
        /// </summary>
        /// <returns>Goal name string</returns>
        public override string GetName()
        {
            return "ProtectGroupGoal";
        }

        /// <summary>
        /// Gets debug information including current priority, protection status, and enemy counts
        /// Used by /mimic debug command and logging
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>Debug string with priority, satisfaction, and protection details</returns>
        public override string GetDebugInfo(ReGoapState<string, object> currentState)
        {
            float priority = GetPriority(currentState);
            bool satisfied = IsGoalSatisfied(currentState);
            int numEnemies = GetNumEnemies(currentState);
            int numEnemiesNotOnTank = GetInt(currentState, MimicWorldStateKeys.NUM_ENEMIES_NOT_ON_TANK, 0);
            bool healerUnderAttack = GetBool(currentState, MimicWorldStateKeys.HEALER_UNDER_ATTACK, false);
            bool inCombat = IsInCombat(currentState);
            bool isMainTank = GetBool(currentState, MimicWorldStateKeys.IS_MAIN_TANK, false);

            return $"{GetName()} (Priority: {priority:F2}, Satisfied: {satisfied}, " +
                   $"Enemies: {numEnemies}, AttackingGroup: {numEnemiesNotOnTank}, " +
                   $"HealerUnderAttack: {healerUnderAttack}, InCombat: {inCombat}, IsMainTank: {isMainTank})";
        }
    }
}
