using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// DPS role goal for dealing damage to enemies
    /// Shared across all DPS roles (MeleeDPS, CasterDPS, Hybrid) regardless of class
    /// Priority scales dynamically based on combat state and target selection
    /// </summary>
    /// <remarks>
    /// Design Principles (from design.md Component 4: Role-Based Goals):
    /// - DPS goal prioritizes dealing damage to the MainAssist's target
    /// - Multiple DPS classes share same goal definition (all DPS use DealDamageGoal)
    /// - Priority dynamically calculated based on world state from sensors
    /// - Works in coordination with assist train mechanics for focused fire
    ///
    /// Priority Formula (from requirements.md 11.20-11.21):
    /// base_priority = 2.0 (default combat priority)
    /// - If MainAssist's target is different from current target: multiply by 0.3 (discourage off-target damage)
    /// - If not in combat or no target: return 0.0 (goal not applicable)
    ///
    /// DAoC DPS Context (from daoc-role-analysis.md):
    /// - MeleeDPS: Focuses on assist train, uses positional styles (back/side attacks)
    /// - CasterDPS: Nukes from distance, uses quickcast recovery after interrupts
    /// - Assist Train: All DPS should focus Main Assist's target for coordinated burst damage
    /// - Off-target damage wastes resources and splits focus in RvR/PvE
    ///
    /// Organic Behavior Patterns (from requirements.md 11.29-11.30):
    /// - When DPS evaluates actions: Instant damage abilities preferred when movement required (kiting, positioning)
    /// - When DPS evaluates actions at range: Cast-time spells preferred over melee (due to positioning cost)
    /// - Action cost calculation naturally handles DPS optimization (resource efficiency formula)
    ///
    /// World State Dependencies:
    /// - HAS_TARGET: Whether DPS has a valid target (from TargetSensor reading Body.TargetObject)
    /// - IN_COMBAT: Combat state (goal only active in combat)
    /// - TARGET_MATCHES_MAIN_ASSIST: Whether current target matches Main Assist's target (assist train)
    /// - IS_MAIN_ASSIST: Role assignment (Main Assist has slightly different priority)
    /// - NUM_ENEMIES: Total number of enemies on aggro list
    ///
    /// Goal State: { "targetDead": true }
    /// Satisfied when: Current target is dead or no valid target exists
    ///
    /// Example Scenarios:
    /// - DPS in combat, targeting Main Assist's target: Priority = 2.0 (baseline DPS priority)
    /// - DPS in combat, targeting wrong target: Priority = 2.0 * 0.3 = 0.6 (heavily discouraged)
    /// - DPS out of combat: Priority = 0.0 (no damage dealing needed)
    /// - Main Assist calling target: Priority = 2.0 (sets the target for others)
    /// - Multiple enemies, focused fire: Priority = 2.0 (coordination via assist train)
    ///
    /// Result: DPS naturally focuses fire on Main Assist's target, optimizing group damage output
    /// </remarks>
    public class DealDamageGoal : MimicGoal
    {
        /// <summary>
        /// Base priority for DPS damage dealing
        /// Value of 2.0 places DPS at medium priority (below healing/tanking emergencies, above buffs)
        /// </summary>
        private const float BASE_PRIORITY = 2.0f;

        /// <summary>
        /// Multiplier when targeting wrong enemy (not Main Assist's target)
        /// 0.3x multiplier heavily discourages off-target damage (splits focus, wastes cooldowns)
        /// </summary>
        private const float OFF_TARGET_MULTIPLIER = 0.3f;

        /// <summary>
        /// Minimum priority floor when in combat with valid target
        /// Ensures DPS goal is always evaluated during combat
        /// </summary>
        private const float MINIMUM_PRIORITY = 0.1f;

        /// <summary>
        /// Goal state key for target elimination
        /// </summary>
        private const string TARGET_DEAD = "targetDead";

        /// <summary>
        /// Constructs a new DealDamageGoal for a DPS mimic
        /// </summary>
        /// <param name="body">The MimicNPC body for game state access (via sensors)</param>
        /// <param name="brain">The MimicBrain for AI state access (via sensors)</param>
        public DealDamageGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        /// <summary>
        /// Calculates dynamic priority based on combat state and target selection
        /// Priority scales with assist train coordination (on-target vs off-target)
        /// </summary>
        /// <param name="currentState">Current world state populated by sensors reading Body/Brain</param>
        /// <returns>Priority value (higher = more urgent; 0.0 = not applicable)</returns>
        /// <remarks>
        /// Priority Calculation Logic:
        /// 1. Base: 2.0 (medium priority for damage dealing)
        /// 2. Off-Target Penalty:
        ///    - If targeting Main Assist's target: 2.0 (full priority - assist train)
        ///    - If targeting different enemy: 2.0 * 0.3 = 0.6 (heavy penalty - split focus)
        /// 3. Not in Combat: Return 0.0 (no combat, no damage needed)
        /// 4. No Target: Return 0.0 (can't deal damage without target)
        ///
        /// Example Scenarios:
        /// - MeleeDPS on Main Assist's target: 2.0 (coordinated burst damage)
        /// - CasterDPS on Main Assist's target: 2.0 (coordinated nuking)
        /// - DPS attacking add while Main Assist on different target: 0.6 (discouraged)
        /// - Main Assist selecting new target: 2.0 (sets the focus target)
        /// - DPS out of combat: 0.0 (no damage goal active)
        /// - DPS with no valid target: 0.0 (need to acquire target first)
        ///
        /// DAoC Assist Train Mechanic:
        /// - In RvR 8v8: Main Assist calls target, all DPS focus fire for quick kills
        /// - Off-target damage is wasteful: splits healer attention, delays kills, wastes cooldowns
        /// - Priority penalty (0.3x) strongly encourages assist train discipline
        /// - Action cost calculation further optimizes: AssistTrainCostCalculator applies 2x priority for on-target, 3x penalty for off-target
        ///
        /// Result: DPS naturally coordinates via assist train without explicit commands
        /// </remarks>
        public override float GetPriority(ReGoapState<string, object> currentState)
        {
            // Read world state values populated by sensors
            bool inCombat = IsInCombat(currentState);
            bool hasTarget = HasTarget(currentState);
            bool targetMatchesMainAssist = IsTargetingMainAssistTarget(currentState);
            bool isMainAssist = GetBool(currentState, MimicWorldStateKeys.IS_MAIN_ASSIST, false);
            int numEnemies = GetNumEnemies(currentState);

            // Goal only applies when in combat with a valid target
            if (!inCombat || !hasTarget)
                return 0.0f;

            // Base DPS priority
            float priority = BASE_PRIORITY;

            // Apply off-target penalty if not following assist train
            // Main Assist is exempt from penalty (they SET the target for others)
            if (!isMainAssist && !targetMatchesMainAssist)
            {
                // Heavy penalty for off-target damage (splits focus, wastes resources)
                priority *= OFF_TARGET_MULTIPLIER; // 0.3x = 70% reduction
            }

            // Enforce minimum priority when in combat with target
            // Ensures DPS goal is always considered even with off-target penalty
            if (priority > 0.0f && priority < MINIMUM_PRIORITY)
                priority = MINIMUM_PRIORITY;

            return priority;
        }

        /// <summary>
        /// Defines the desired world state when damage goal is satisfied
        /// Goal: Current target is dead
        /// </summary>
        /// <returns>Goal state with "targetDead" = true</returns>
        /// <remarks>
        /// The planner will search for action sequences that set "targetDead" to true.
        /// Damage actions (CastSpellAction for damage spells, MeleeAttackAction for melee) set "targetDamaged" effects,
        /// which when repeatedly applied, eventually satisfy "targetDead".
        ///
        /// Goal satisfaction checked by IsGoalSatisfied() against target existence and health.
        ///
        /// Note: This goal focuses on killing current target, not necessarily all enemies.
        /// Once target is dead, TargetSensor will select next target from aggro list,
        /// and DealDamageGoal will re-activate for the new target.
        ///
        /// Multi-Enemy Scenarios:
        /// - Tank pulls 3 enemies: DPS focuses Main Assist's target (enemy 1)
        /// - Enemy 1 dies: TargetSensor selects next target (enemy 2), goal re-activates
        /// - Enemy 2 dies: TargetSensor selects last target (enemy 3), goal re-activates
        /// - All enemies dead: Goal satisfied, defensive goals take over (buffs, following, etc.)
        /// </remarks>
        public override ReGoapState<string, object> GetGoalState()
        {
            var goalState = new ReGoapState<string, object>();

            // Desired end state: current target is dead
            goalState.Set(TARGET_DEAD, true);

            return goalState;
        }

        /// <summary>
        /// Checks if damage goal is currently satisfied
        /// Satisfied when no valid target exists (dead or no target selected)
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if no target or target dead, false if target alive</returns>
        /// <remarks>
        /// Override default satisfaction check to use HAS_TARGET directly from world state.
        /// More efficient than checking goal state match (avoids unnecessary world state comparisons).
        ///
        /// HAS_TARGET populated by TargetSensor reading Brain.CalculateNextAttackTarget(),
        /// which uses existing target selection logic (prioritizes aggro list, distance, etc.).
        ///
        /// Satisfaction Logic:
        /// - HAS_TARGET == false: No target selected or target dead → goal satisfied
        /// - HAS_TARGET == true: Target alive and selected → goal not satisfied, continue damage
        ///
        /// Edge Cases:
        /// - No aggro (NUM_ENEMIES == 0): Goal considered satisfied (no enemies to damage)
        /// - Not in combat: Goal considered satisfied (damage only matters in combat)
        /// - Target switches mid-combat: Goal re-evaluates for new target (seamless retargeting)
        ///
        /// Target Death Handling:
        /// - TargetSensor detects target death (hasTarget = false)
        /// - IsGoalSatisfied returns true (current target dead)
        /// - Planner requests new plan for next highest priority goal
        /// - If more enemies exist: TargetSensor selects next target, DealDamageGoal re-activates
        /// - If no enemies remain: DefensiveGoal or other non-combat goals take over
        /// </remarks>
        public override bool IsGoalSatisfied(ReGoapState<string, object> currentState)
        {
            bool hasTarget = HasTarget(currentState);
            bool inCombat = IsInCombat(currentState);

            // Goal satisfied if not in combat or no target exists
            if (!inCombat || !hasTarget)
                return true;

            // Target exists and mimic in combat = goal not satisfied, continue damage
            return false;
        }

        /// <summary>
        /// Gets the name of this goal for debugging and logging
        /// </summary>
        /// <returns>Goal name string</returns>
        public override string GetName()
        {
            return "DealDamageGoal";
        }

        /// <summary>
        /// Gets debug information including current priority, target status, and assist train coordination
        /// Used by /mimic debug command and logging
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>Debug string with priority, satisfaction, and damage dealing details</returns>
        public override string GetDebugInfo(ReGoapState<string, object> currentState)
        {
            float priority = GetPriority(currentState);
            bool satisfied = IsGoalSatisfied(currentState);
            bool hasTarget = HasTarget(currentState);
            bool targetMatchesMainAssist = IsTargetingMainAssistTarget(currentState);
            bool inCombat = IsInCombat(currentState);
            bool isMainAssist = GetBool(currentState, MimicWorldStateKeys.IS_MAIN_ASSIST, false);
            int numEnemies = GetNumEnemies(currentState);

            return $"{GetName()} (Priority: {priority:F2}, Satisfied: {satisfied}, " +
                   $"HasTarget: {hasTarget}, OnMainAssistTarget: {targetMatchesMainAssist}, " +
                   $"InCombat: {inCombat}, IsMainAssist: {isMainAssist}, Enemies: {numEnemies})";
        }
    }
}
