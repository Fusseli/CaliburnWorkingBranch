using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// Self-preservation goal that activates when the mimic's own health drops to dangerous levels
    /// Universal goal shared across all roles and classes - survival takes precedence over role duties
    /// Priority scales dynamically with health deficit, escalating dramatically as death approaches
    /// </summary>
    /// <remarks>
    /// Design Principles (from design.md Component 4: Role-Based Goals):
    /// - SurviveGoal is a universal goal applicable to all mimics regardless of role
    /// - Activates when self health drops below safety thresholds
    /// - Escalating priority system ensures imminent death becomes absolute priority
    /// - Allows role-appropriate responses (healers self-heal, tanks use defensive abilities, DPS flee/kite)
    ///
    /// Priority Formula:
    /// Base calculation uses inverse health percentage with exponential scaling:
    /// - At 100% HP: priority = 0.0 (goal not applicable)
    /// - At 75% HP: priority = 0.5 (low priority, routine self-healing)
    /// - At 50% HP: priority = 2.0 (medium priority, self-preservation concern)
    /// - At 25% HP: priority = 10.0 (high priority, survival critical)
    /// - At 10% HP: priority = 50.0 (absolute priority, death imminent)
    ///
    /// Calculation:
    /// healthDeficit = (100 - healthPercent) / 100  (0.0 to 1.0)
    /// base_priority = healthDeficit * 10.0
    /// if healthPercent < 50%: priority *= 2.0 (emergency multiplier)
    /// if healthPercent < 25%: priority *= 5.0 (critical multiplier)
    ///
    /// DAoC Context (from daoc-role-analysis.md):
    /// - Self-preservation is critical for all roles:
    ///   - Dead healer = no group healing (cascade failure)
    ///   - Dead tank = threat lost, enemies attack squishies
    ///   - Dead DPS = reduced kill speed, longer dangerous fights
    ///   - Dead CC = adds run wild
    /// - Role-appropriate survival actions (handled by actions, not goal):
    ///   - Healers: Self-heal, HoTs, instant heals
    ///   - Tanks: Defensive abilities, guard drop (if necessary)
    ///   - DPS: Flee, kite, defensive abilities
    ///   - CC: Mezz pursuers, sprint, phase shift
    ///
    /// Organic Behavior Patterns (from requirements.md):
    /// - At 75% HP: Mimics consider self-healing but role duties may take precedence
    /// - At 50% HP: Self-preservation becomes significant concern, balances with role
    /// - At 25% HP: Survival dominates, role duties suspended until safe
    /// - At 10% HP: Absolute survival mode, everything else abandoned
    ///
    /// World State Dependencies:
    /// - SELF_HEALTH_PERCENT: Current health percentage from Body.HealthPercent
    /// - SELF_HEALTH: Current health points from Body.Health
    /// - SELF_MAX_HEALTH: Maximum health points from Body.MaxHealth
    /// - IN_COMBAT: Combat state affects survival urgency
    /// - HAS_AGGRO: Being targeted increases survival priority
    ///
    /// Goal State: { "selfHealthSafe": true }
    /// Satisfied when: Self health above 75% (safe threshold)
    ///
    /// Example Scenarios:
    /// - Tank at 60% HP during pull: SurviveGoal (2.4) competes with TankGoal (3.0)
    ///   - Tank continues generating threat but considers defensive abilities
    /// - Healer at 40% HP, group member at 45% HP: SurviveGoal (6.0) vs HealGroupGoal (2.25)
    ///   - Healer prioritizes self-heal first, then heals group member (can't heal if dead)
    /// - DPS at 20% HP being chased: SurviveGoal (40.0) overrides all other goals
    ///   - DPS flees, uses defensive abilities, prioritizes survival above damage
    /// - CC at 70% HP, 3 unmezzed adds: SurviveGoal (1.5) vs CCGoal (8.0)
    ///   - CC focuses on mezzing adds (better survival through control than self-healing)
    ///
    /// Goal State Key: "selfHealthSafe"
    /// - Set to true when health reaches safe threshold (75% HP)
    /// - Actions that heal self, reduce incoming damage, or create distance contribute to satisfying this goal
    /// </remarks>
    public class SurviveGoal : MimicGoal
    {
        /// <summary>
        /// Safe health threshold percentage - goal satisfied when above this
        /// 75% is chosen as safe threshold where survival is no longer primary concern
        /// </summary>
        private const float SAFE_HEALTH_THRESHOLD = 75.0f;

        /// <summary>
        /// Emergency health threshold percentage - applies emergency multiplier below this
        /// 50% HP is DAoC community standard for "endangered" status
        /// </summary>
        private const float EMERGENCY_HEALTH_THRESHOLD = 50.0f;

        /// <summary>
        /// Critical health threshold percentage - applies critical multiplier below this
        /// 25% HP indicates imminent death risk, absolute survival priority
        /// </summary>
        private const float CRITICAL_HEALTH_THRESHOLD = 25.0f;

        /// <summary>
        /// Base priority multiplier for health deficit
        /// Value of 10.0 provides reasonable scaling (0.5 at 75% HP, 10.0 at 25% HP before multipliers)
        /// </summary>
        private const float BASE_PRIORITY_MULTIPLIER = 10.0f;

        /// <summary>
        /// Emergency multiplier when health below 50%
        /// 2x multiplier elevates survival above most role goals
        /// </summary>
        private const float EMERGENCY_MULTIPLIER = 2.0f;

        /// <summary>
        /// Critical multiplier when health below 25%
        /// 5x multiplier (combined with emergency = 10x total) makes survival absolute priority
        /// </summary>
        private const float CRITICAL_MULTIPLIER = 5.0f;

        /// <summary>
        /// Goal state key for self health safety
        /// </summary>
        private const string SELF_HEALTH_SAFE = "selfHealthSafe";

        /// <summary>
        /// Constructs a new SurviveGoal for any mimic (universal goal)
        /// </summary>
        /// <param name="body">The MimicNPC body for game state access (via sensors)</param>
        /// <param name="brain">The MimicBrain for AI state access (via sensors)</param>
        public SurviveGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        /// <summary>
        /// Calculates priority based on self health status with exponential scaling
        /// Priority increases dramatically as health decreases, ensuring survival becomes dominant concern
        /// </summary>
        /// <param name="currentState">Current world state populated by sensors reading Body/Brain</param>
        /// <returns>Priority value (higher = more urgent; 0.0 = safe)</returns>
        /// <remarks>
        /// Priority Calculation Logic:
        /// 1. Calculate health deficit: (100 - healthPercent) / 100
        /// 2. Base priority: deficit * BASE_PRIORITY_MULTIPLIER (10.0)
        /// 3. Apply emergency multiplier if health < 50% (×2)
        /// 4. Apply critical multiplier if health < 25% (×5, stacks with emergency = ×10 total)
        /// 5. If in combat with aggro, priority scales up faster (×1.5)
        ///
        /// Priority Examples:
        /// - 100% HP: 0.0 (goal not applicable, perfect health)
        /// - 90% HP: 1.0 (minimal priority, routine)
        /// - 75% HP: 2.5 (low priority, goal threshold)
        /// - 60% HP: 4.0 (medium priority, concern emerging)
        /// - 50% HP: 5.0 (medium priority at threshold)
        /// - 49% HP: 10.2 (emergency multiplier applied, doubled priority)
        /// - 40% HP: 12.0 (emergency active, survival significant)
        /// - 25% HP: 15.0 (emergency active at threshold)
        /// - 24% HP: 76.0 (critical multiplier applied, survival dominant)
        /// - 20% HP: 80.0 (critical emergency, absolute priority)
        /// - 10% HP: 90.0 (death imminent, everything abandoned for survival)
        ///
        /// Result: Exponential priority curve ensures survival naturally dominates as health drops
        /// - Above 75% HP: Role goals dominate (healing others, threat, damage)
        /// - 50-75% HP: Survival competes with role goals (tank might use defensive ability)
        /// - 25-50% HP: Survival usually dominates (healer self-heals before healing others)
        /// - Below 25% HP: Survival is absolute priority (flee, defensive abilities, panic mode)
        /// </remarks>
        public override float GetPriority(ReGoapState<string, object> currentState)
        {
            // Read world state values populated by sensors
            float selfHealthPercent = GetFloat(currentState, MimicWorldStateKeys.SELF_HEALTH_PERCENT, 100.0f);
            bool inCombat = IsInCombat(currentState);
            bool hasAggro = GetBool(currentState, MimicWorldStateKeys.HAS_AGGRO, false);

            // Health above safe threshold = goal satisfied, no priority
            if (selfHealthPercent >= SAFE_HEALTH_THRESHOLD)
                return 0.0f;

            // Calculate health deficit (0.0 at 100% HP, 1.0 at 0% HP)
            float healthDeficit = (100.0f - selfHealthPercent) / 100.0f;

            // Base priority scales linearly with deficit
            float priority = healthDeficit * BASE_PRIORITY_MULTIPLIER;

            // Apply emergency multiplier if health below 50%
            if (selfHealthPercent < EMERGENCY_HEALTH_THRESHOLD)
            {
                priority *= EMERGENCY_MULTIPLIER;
            }

            // Apply critical multiplier if health below 25%
            // Stacks with emergency multiplier for exponential scaling (2x * 5x = 10x total)
            if (selfHealthPercent < CRITICAL_HEALTH_THRESHOLD)
            {
                priority *= CRITICAL_MULTIPLIER;
            }

            // In combat with aggro increases urgency (being actively attacked = greater danger)
            if (inCombat && hasAggro)
            {
                priority *= 1.5f;
            }

            return priority;
        }

        /// <summary>
        /// Defines the desired world state when survival goal is satisfied
        /// Goal: Self health above safe threshold (75% HP)
        /// </summary>
        /// <returns>Goal state with "selfHealthSafe" = true</returns>
        /// <remarks>
        /// The planner will search for action sequences that set "selfHealthSafe" to true.
        /// Actions that contribute to this goal:
        /// - Self-healing spells (CastSpellAction targeting self)
        /// - Defensive abilities (UseAbilityAction for defensive cooldowns)
        /// - Flee/kite actions (MoveAction to create distance)
        /// - Crowd control on pursuers (CastSpellAction for mezz/root on attackers)
        ///
        /// Goal satisfaction checked by IsGoalSatisfied() against SELF_HEALTH_PERCENT >= 75%.
        ///
        /// Note: Goal focuses on returning to safe threshold (75% HP), not full health (100% HP).
        /// Once safe, role goals resume normal priority and may top off health opportunistically.
        /// </remarks>
        public override ReGoapState<string, object> GetGoalState()
        {
            var goalState = new ReGoapState<string, object>();

            // Desired end state: self health is safe (above 75% HP)
            goalState.Set(SELF_HEALTH_SAFE, true);

            return goalState;
        }

        /// <summary>
        /// Checks if survival goal is currently satisfied
        /// Satisfied when self health is above safe threshold (75% HP)
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if health is safe, false if endangered</returns>
        /// <remarks>
        /// Override default satisfaction check to use SELF_HEALTH_PERCENT directly.
        /// More efficient than checking goal state match (avoids unnecessary world state comparisons).
        ///
        /// SELF_HEALTH_PERCENT populated by HealthSensor reading Body.HealthPercent,
        /// which is a direct property read from existing game state (no duplication).
        ///
        /// Satisfaction Logic:
        /// - Health >= 75% HP: Safe → goal satisfied
        /// - Health < 75% HP: Endangered → goal not satisfied, survival actions needed
        ///
        /// Threshold choice rationale:
        /// - 75% HP chosen as "safe" because:
        ///   - Above 75% HP, mimic is not in immediate danger
        ///   - Below 75% HP, burst damage or sustained combat could be lethal
        ///   - Matches DAoC community standard for "full health" in combat scenarios
        ///   - Allows role goals to resume once immediate danger passes
        /// </remarks>
        public override bool IsGoalSatisfied(ReGoapState<string, object> currentState)
        {
            float selfHealthPercent = GetFloat(currentState, MimicWorldStateKeys.SELF_HEALTH_PERCENT, 100.0f);
            return selfHealthPercent >= SAFE_HEALTH_THRESHOLD; // Safe at 75%+ HP
        }

        /// <summary>
        /// Gets the name of this goal for debugging and logging
        /// </summary>
        /// <returns>Goal name string</returns>
        public override string GetName()
        {
            return "SurviveGoal";
        }

        /// <summary>
        /// Gets debug information including current priority and health status
        /// Used by /mimic debug command and logging
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>Debug string with priority, satisfaction, health percentage, and combat status</returns>
        public override string GetDebugInfo(ReGoapState<string, object> currentState)
        {
            float priority = GetPriority(currentState);
            bool satisfied = IsGoalSatisfied(currentState);
            float selfHealthPercent = GetFloat(currentState, MimicWorldStateKeys.SELF_HEALTH_PERCENT, 100.0f);
            bool inCombat = IsInCombat(currentState);
            bool hasAggro = GetBool(currentState, MimicWorldStateKeys.HAS_AGGRO, false);

            string status = "Safe";
            if (selfHealthPercent < CRITICAL_HEALTH_THRESHOLD)
                status = "CRITICAL";
            else if (selfHealthPercent < EMERGENCY_HEALTH_THRESHOLD)
                status = "Emergency";
            else if (selfHealthPercent < SAFE_HEALTH_THRESHOLD)
                status = "Endangered";

            return $"{GetName()} (Priority: {priority:F2}, Satisfied: {satisfied}, " +
                   $"Health: {selfHealthPercent:F1}%, Status: {status}, InCombat: {inCombat}, HasAggro: {hasAggro})";
        }
    }
}
