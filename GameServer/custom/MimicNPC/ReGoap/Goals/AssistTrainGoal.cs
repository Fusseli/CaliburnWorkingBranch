using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// MeleeDPS role goal for coordinated focus fire via DAoC assist train mechanics
    /// Shared across melee DPS classes (Armsman, Blademaster, Berserker, Hero, etc.)
    /// Priority scales dynamically based on assist train adherence (on-target vs off-target)
    /// </summary>
    /// <remarks>
    /// Design Principles (from design.md Component 4: Role-Based Goals):
    /// - AssistTrainGoal enforces strict adherence to Main Assist's target selection
    /// - Multiple melee DPS classes share same goal definition (coordinated burst damage)
    /// - Priority dynamically calculated based on world state from sensors
    /// - Works in coordination with Main Assist role for target calling
    ///
    /// Priority Formula (from requirements.md 11.21 and design.md Component 4):
    /// base_priority = 4.0 (higher than general DPS goal for melee coordination)
    /// - If MainAssist's target is different from current target: multiply by 0.3 (discourage off-target damage)
    /// - If not in combat or no target: return 0.0 (goal not applicable)
    ///
    /// Difference from DealDamageGoal:
    /// - DealDamageGoal: General DPS goal for all damage dealers (base priority 2.0)
    /// - AssistTrainGoal: Specific melee coordination goal with stricter assist train enforcement (base priority 4.0)
    /// - AssistTrainGoal used for MeleeDPS roles requiring tight coordination (RvR 8v8)
    /// - DealDamageGoal used for general damage output (PvE, solo, ranged DPS)
    ///
    /// DAoC Assist Train Context (from daoc-role-analysis.md):
    /// - RvR 8v8 Meta: Main Assist calls target, all melee train on that target for burst kills
    /// - Coordinated Burst Damage: 3-4 melee focusing same target can kill in 2-3 seconds
    /// - Off-Target Damage: Wastes cooldowns, splits healer attention, delays kills
    /// - Train Discipline: Melee must stick to assist train for maximum effectiveness
    /// - Positional Styles: Melee need to coordinate positioning (back/side attacks) on same target
    ///
    /// Organic Behavior Patterns (from requirements.md 11.29):
    /// - When melee DPS evaluates actions: Instant damage abilities preferred when movement required
    /// - Action cost calculation naturally handles positional style setup (+50% cost for back/side)
    /// - AssistTrainCostCalculator provides additional coordination: 2x priority on-target, 3x penalty off-target
    ///
    /// World State Dependencies:
    /// - HAS_TARGET: Whether DPS has a valid target (from TargetSensor reading Body.TargetObject)
    /// - IN_COMBAT: Combat state (goal only active in combat)
    /// - TARGET_MATCHES_MAIN_ASSIST: Whether current target matches Main Assist's target (critical)
    /// - IS_MAIN_ASSIST: Role assignment (Main Assist sets the target, doesn't follow)
    /// - MAIN_ASSIST_TARGET: The actual target selected by Main Assist
    /// - NUM_ENEMIES: Total number of enemies on aggro list
    ///
    /// Goal State: { "targetDead": true, "followingAssistTrain": true }
    /// Satisfied when: Current target is dead or no valid target exists
    ///
    /// Example Scenarios:
    /// - MeleeDPS on Main Assist's target: Priority = 4.0 (full coordination priority)
    /// - MeleeDPS attacking different target: Priority = 4.0 * 0.3 = 1.2 (heavily discouraged)
    /// - Main Assist selecting new target: Priority = 4.0 (sets the target for train)
    /// - MeleeDPS out of combat: Priority = 0.0 (no damage goal active)
    /// - MeleeDPS with no valid target: Priority = 0.0 (need to acquire target first)
    /// - Multiple melee on same target: Priority = 4.0 each (coordinated burst damage)
    ///
    /// Result: Melee DPS naturally forms assist train without explicit commands, maximizing group kill speed
    /// </remarks>
    public class AssistTrainGoal : MimicGoal
    {
        /// <summary>
        /// Base priority for assist train coordination
        /// Value of 4.0 places assist train higher than general DPS (2.0) to enforce melee coordination
        /// </summary>
        private const float BASE_PRIORITY = 4.0f;

        /// <summary>
        /// Multiplier when targeting wrong enemy (not Main Assist's target)
        /// 0.3x multiplier heavily discourages off-target damage (breaks assist train discipline)
        /// </summary>
        private const float OFF_TARGET_MULTIPLIER = 0.3f;

        /// <summary>
        /// Minimum priority floor when in combat with valid target
        /// Ensures assist train goal is always evaluated during combat
        /// </summary>
        private const float MINIMUM_PRIORITY = 0.1f;

        /// <summary>
        /// Goal state key for target elimination
        /// </summary>
        private const string TARGET_DEAD = "targetDead";

        /// <summary>
        /// Goal state key for assist train adherence
        /// </summary>
        private const string FOLLOWING_ASSIST_TRAIN = "followingAssistTrain";

        /// <summary>
        /// Constructs a new AssistTrainGoal for a melee DPS mimic
        /// </summary>
        /// <param name="body">The MimicNPC body for game state access (via sensors)</param>
        /// <param name="brain">The MimicBrain for AI state access (via sensors)</param>
        public AssistTrainGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        /// <summary>
        /// Calculates dynamic priority based on assist train adherence
        /// Priority scales with Main Assist target matching (on-target vs off-target)
        /// </summary>
        /// <param name="currentState">Current world state populated by sensors reading Body/Brain</param>
        /// <returns>Priority value (higher = more urgent; 0.0 = not applicable)</returns>
        /// <remarks>
        /// Priority Calculation Logic:
        /// 1. Base: 4.0 (higher priority than general DPS for melee coordination)
        /// 2. Assist Train Adherence:
        ///    - If targeting Main Assist's target: 4.0 (full priority - coordinated burst)
        ///    - If targeting different enemy: 4.0 * 0.3 = 1.2 (heavy penalty - breaks coordination)
        /// 3. Not in Combat: Return 0.0 (no combat, no damage needed)
        /// 4. No Target: Return 0.0 (can't deal damage without target)
        /// 5. Main Assist Role: Exempt from penalty (they SET the target for others)
        ///
        /// Example Scenarios:
        /// - MeleeDPS #1 on Main Assist's target: 4.0 (coordinated burst damage)
        /// - MeleeDPS #2 on Main Assist's target: 4.0 (stacking burst damage)
        /// - MeleeDPS #3 on Main Assist's target: 4.0 (overwhelming burst damage)
        /// - MeleeDPS attacking add while Main Assist on different target: 1.2 (heavily discouraged)
        /// - Main Assist selecting new target: 4.0 (calls the target for train)
        /// - MeleeDPS out of combat: 0.0 (no damage goal active)
        /// - MeleeDPS with no valid target: 0.0 (need to acquire target first)
        ///
        /// DAoC Assist Train Mechanic (Critical for RvR 8v8):
        /// - Main Assist calls target (usually enemy healer or caster)
        /// - All melee DPS immediately switch to Main Assist's target
        /// - Coordinated burst: 3-4 melee with positional styles = 2-3 second kill
        /// - Off-target damage is wasteful: delays kills, splits healing, wastes peels
        /// - Priority penalty (0.3x) strongly enforces train discipline
        /// - AssistTrainCostCalculator provides additional enforcement at action level (2x on-target, 3x off-target)
        ///
        /// Coordination with Other Goals:
        /// - ProtectGroupGoal (4.0 base): Can compete when allies under attack (reactive peeling)
        /// - DealDamageGoal (2.0 base): Lower priority, defers to AssistTrainGoal for melee
        /// - AssistTrainGoal prioritizes proactive coordinated damage over reactive protection
        ///
        /// Result: Melee DPS naturally forms assist train, maximizing group kill speed and coordination
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

            // Base assist train coordination priority
            float priority = BASE_PRIORITY;

            // Apply off-target penalty if not following assist train
            // Main Assist is exempt from penalty (they SET the target for the train)
            if (!isMainAssist && !targetMatchesMainAssist)
            {
                // Heavy penalty for off-target damage (breaks assist train discipline)
                // 0.3x = 70% reduction in priority, making on-target damage 3.3x more attractive
                priority *= OFF_TARGET_MULTIPLIER;
            }

            // Enforce minimum priority when in combat with target
            // Ensures assist train goal is always considered even with off-target penalty
            if (priority > 0.0f && priority < MINIMUM_PRIORITY)
                priority = MINIMUM_PRIORITY;

            return priority;
        }

        /// <summary>
        /// Defines the desired world state when assist train goal is satisfied
        /// Goal: Current target is dead AND following assist train discipline
        /// </summary>
        /// <returns>Goal state with "targetDead" = true and "followingAssistTrain" = true</returns>
        /// <remarks>
        /// The planner will search for action sequences that set both goal state flags to true.
        /// Damage actions (MeleeAttackAction, CastSpellAction for melee abilities) set "targetDamaged" effects,
        /// which when repeatedly applied to Main Assist's target, eventually satisfy "targetDead" and "followingAssistTrain".
        ///
        /// Goal satisfaction checked by IsGoalSatisfied() against target existence and assist train adherence.
        ///
        /// Dual Goal State Requirements:
        /// - "targetDead": Ensures damage is being dealt effectively
        /// - "followingAssistTrain": Ensures coordination with Main Assist's target selection
        ///
        /// Note: This goal focuses on killing Main Assist's target specifically, not just any target.
        /// Once Main Assist's target is dead, TargetSensor will update to next target,
        /// and AssistTrainGoal will re-activate if Main Assist has selected a new target.
        ///
        /// Multi-Enemy Scenarios (RvR 8v8):
        /// - Main Assist calls enemy healer: All melee train on healer (assist train forms)
        /// - Healer dies: Main Assist calls next target (enemy caster), train switches seamlessly
        /// - Enemy caster dies: Main Assist calls next target (enemy tank), train continues
        /// - All priority targets dead: AssistTrainGoal defers to cleanup (lower priority targets)
        ///
        /// Assist Train Coordination:
        /// - AssistTrainSensor tracks Main Assist's target in real-time
        /// - Goal priority ensures melee switches immediately when Main Assist retargets
        /// - Action cost calculation (AssistTrainCostCalculator) reinforces on-target preference
        /// - Result: Natural assist train formation without explicit group commands
        /// </remarks>
        public override ReGoapState<string, object> GetGoalState()
        {
            var goalState = new ReGoapState<string, object>();

            // Desired end state: target is dead AND following assist train
            goalState.Set(TARGET_DEAD, true);
            goalState.Set(FOLLOWING_ASSIST_TRAIN, true);

            return goalState;
        }

        /// <summary>
        /// Checks if assist train goal is currently satisfied
        /// Satisfied when no valid target exists (dead or no target selected) OR not following assist train
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if no target or target dead, false if target alive and on assist train</returns>
        /// <remarks>
        /// Override default satisfaction check to use HAS_TARGET and TARGET_MATCHES_MAIN_ASSIST directly.
        /// More efficient than checking goal state match (avoids unnecessary world state comparisons).
        ///
        /// HAS_TARGET populated by TargetSensor reading Brain.CalculateNextAttackTarget(),
        /// TARGET_MATCHES_MAIN_ASSIST populated by AssistTrainSensor reading Main Assist's target,
        /// both use existing target selection logic (no duplication).
        ///
        /// Satisfaction Logic:
        /// - HAS_TARGET == false: No target selected or target dead → goal satisfied
        /// - HAS_TARGET == true && TARGET_MATCHES_MAIN_ASSIST == false: Off-target (goal partially satisfied, low priority)
        /// - HAS_TARGET == true && TARGET_MATCHES_MAIN_ASSIST == true: On-target (goal not satisfied, continue damage)
        ///
        /// Edge Cases:
        /// - No aggro (NUM_ENEMIES == 0): Goal considered satisfied (no enemies to damage)
        /// - Not in combat: Goal considered satisfied (damage only matters in combat)
        /// - Main Assist has no target: Goal considered satisfied (no train to follow)
        /// - Target switches mid-combat: Goal re-evaluates for new target (seamless retargeting)
        ///
        /// Assist Train Target Switching:
        /// - AssistTrainSensor detects Main Assist retargets
        /// - TARGET_MATCHES_MAIN_ASSIST updates to false (mimic on wrong target)
        /// - Priority drops to 1.2 (off-target penalty)
        /// - IsGoalSatisfied returns false (need to switch targets)
        /// - Planner generates new plan to acquire Main Assist's new target
        /// - Mimic switches targets, priority returns to 4.0 (on-target)
        /// - Result: Seamless assist train following without explicit commands
        ///
        /// Target Death Handling:
        /// - TargetSensor detects target death (hasTarget = false)
        /// - IsGoalSatisfied returns true (current target dead)
        /// - Planner requests new plan for next highest priority goal
        /// - If Main Assist selects new target: AssistTrainGoal re-activates with new target
        /// - If no new target: DefensiveGoal or other non-combat goals take over
        /// </remarks>
        public override bool IsGoalSatisfied(ReGoapState<string, object> currentState)
        {
            bool hasTarget = HasTarget(currentState);
            bool inCombat = IsInCombat(currentState);
            bool targetMatchesMainAssist = IsTargetingMainAssistTarget(currentState);

            // Goal satisfied if not in combat or no target exists
            if (!inCombat || !hasTarget)
                return true;

            // If off-target (not following assist train), goal is partially satisfied (low priority to switch)
            // Goal not satisfied when on-target (continue damage to kill target)
            // This creates natural assist train following: switching is low priority, staying on-target is high priority
            return false;
        }

        /// <summary>
        /// Gets the name of this goal for debugging and logging
        /// </summary>
        /// <returns>Goal name string</returns>
        public override string GetName()
        {
            return "AssistTrainGoal";
        }

        /// <summary>
        /// Gets debug information including current priority, target status, and assist train coordination
        /// Used by /mimic debug command and logging
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>Debug string with priority, satisfaction, and assist train details</returns>
        public override string GetDebugInfo(ReGoapState<string, object> currentState)
        {
            float priority = GetPriority(currentState);
            bool satisfied = IsGoalSatisfied(currentState);
            bool hasTarget = HasTarget(currentState);
            bool targetMatchesMainAssist = IsTargetingMainAssistTarget(currentState);
            bool inCombat = IsInCombat(currentState);
            bool isMainAssist = GetBool(currentState, MimicWorldStateKeys.IS_MAIN_ASSIST, false);
            int numEnemies = GetNumEnemies(currentState);

            // Get Main Assist's target for debugging
            var mainAssistTarget = GetStateValue<object>(currentState, MimicWorldStateKeys.MAIN_ASSIST_TARGET, null);
            string mainAssistTargetName = mainAssistTarget?.ToString() ?? "None";

            return $"{GetName()} (Priority: {priority:F2}, Satisfied: {satisfied}, " +
                   $"HasTarget: {hasTarget}, OnAssistTrain: {targetMatchesMainAssist}, " +
                   $"MainAssistTarget: {mainAssistTargetName}, InCombat: {inCombat}, " +
                   $"IsMainAssist: {isMainAssist}, Enemies: {numEnemies})";
        }
    }
}
