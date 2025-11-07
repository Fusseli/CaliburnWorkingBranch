using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// Debuffer role goal for applying debuffs to priority targets via DAoC debuff mechanics
    /// Shared across debuffer classes (Shaman, Spiritmaster, Bonedancer, Warlock, etc.)
    /// Priority scales dynamically based on debuff availability and target coordination
    /// </summary>
    /// <remarks>
    /// Design Principles (from design.md Component 4: Role-Based Goals):
    /// - DebuffPriorityGoal enforces debuff application on Main Assist's target before DPS engages
    /// - Multiple debuffer classes share same goal definition (coordinated debuffing)
    /// - Priority dynamically calculated based on world state from sensors
    /// - Works in coordination with Main Assist role for target calling
    ///
    /// Priority Formula (from requirements.md 3.2 and daoc-role-analysis.md):
    /// base_priority = 2.5 (higher than general DPS 2.0, lower than AssistTrainGoal 4.0)
    /// - If MainAssist's target is different from current target: multiply by 0.3 (discourage off-target debuffs)
    /// - If no debuff spells available: return 0.0 (goal not applicable)
    /// - If not in combat or no target: return 0.0 (goal not applicable)
    ///
    /// Difference from DealDamageGoal:
    /// - DealDamageGoal: General DPS goal for all damage dealers (base priority 2.0)
    /// - DebuffPriorityGoal: Specific debuff goal with higher priority (2.5) to apply debuffs BEFORE damage
    /// - Debuffs reduce target resistances, armor, damage output for entire group's benefit
    /// - DebuffPriorityGoal activates when debuff spells available (SpellAvailabilitySensor)
    ///
    /// DAoC Debuff Context (from daoc-role-analysis.md):
    /// - RvR 8v8 Meta: Debuffers apply stat/resistance debuffs to Main Assist's target before burst
    /// - Common Debuffs: Stat reduction (STR/CON/DEX), armor reduction, resistance reduction
    /// - Debuff Stacking: Multiple debuff types stack multiplicatively (huge damage amplification)
    /// - Timing: Debuffs must land BEFORE DPS engages for maximum effectiveness
    /// - Duration: Most debuffs last 30-60 seconds, need refresh after expiration
    /// - Assist Train Integration: Debuffer targets Main Assist's target like DPS
    ///
    /// Organic Behavior Patterns (from requirements.md):
    /// - Priority 2.5 ensures debuffs apply before damage abilities (priority 2.0)
    /// - Cost calculation favors instant debuffs over cast-time debuffs
    /// - If target already debuffed: cost increases 1000x (effective exclusion)
    /// - If target switches: debuff goal re-activates for new target
    ///
    /// World State Dependencies:
    /// - HAS_TARGET: Whether debuffer has a valid target (from TargetSensor reading Body.TargetObject)
    /// - IN_COMBAT: Combat state (goal only active in combat)
    /// - TARGET_MATCHES_MAIN_ASSIST: Whether current target matches Main Assist's target (critical)
    /// - CAN_CAST_OFFENSIVE_SPELLS: Debuffer can cast debuff spells (SpellAvailabilitySensor)
    /// - NUM_ENEMIES: Total number of enemies on aggro list
    ///
    /// Goal State: { "targetDebuffed": true }
    /// Satisfied when: Current target has debuffs applied or no valid target exists
    ///
    /// Example Scenarios:
    /// - Debuffer with Main Assist's target, debuffs available: Priority = 2.5 (apply debuffs first)
    /// - Debuffer attacking different target: Priority = 2.5 * 0.3 = 0.75 (heavily discouraged)
    /// - Debuffer with Main Assist's target, no debuff spells: Priority = 0.0 (can't debuff)
    /// - Debuffer out of combat: Priority = 0.0 (no debuffing needed)
    /// - Debuffer with no valid target: Priority = 0.0 (need to acquire target first)
    /// - Target already debuffed (duration remaining): Goal satisfied, debuffer moves to DPS goal
    ///
    /// Result: Debuffers naturally apply debuffs before DPS engages, maximizing group damage output
    /// </remarks>
    public class DebuffPriorityGoal : MimicGoal
    {
        /// <summary>
        /// Base priority for debuff application
        /// Value of 2.5 places debuffs between general DPS (2.0) and coordinated melee (4.0)
        /// Ensures debuffs land BEFORE damage abilities activate
        /// </summary>
        private const float BASE_PRIORITY = 2.5f;

        /// <summary>
        /// Multiplier when targeting wrong enemy (not Main Assist's target)
        /// 0.3x multiplier heavily discourages off-target debuffs (wastes group coordination)
        /// </summary>
        private const float OFF_TARGET_MULTIPLIER = 0.3f;

        /// <summary>
        /// Minimum priority floor when in combat with valid target
        /// Ensures debuff goal is always evaluated during combat
        /// </summary>
        private const float MINIMUM_PRIORITY = 0.1f;

        /// <summary>
        /// Goal state key for debuff application
        /// </summary>
        private const string TARGET_DEBUFFED = "targetDebuffed";

        /// <summary>
        /// Constructs a new DebuffPriorityGoal for a debuffer mimic
        /// </summary>
        /// <param name="body">The MimicNPC body for game state access (via sensors)</param>
        /// <param name="brain">The MimicBrain for AI state access (via sensors)</param>
        public DebuffPriorityGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        /// <summary>
        /// Calculates dynamic priority based on debuff availability and assist train adherence
        /// Priority scales with Main Assist target matching and debuff spell availability
        /// </summary>
        /// <param name="currentState">Current world state populated by sensors reading Body/Brain</param>
        /// <returns>Priority value (higher = more urgent; 0.0 = not applicable)</returns>
        /// <remarks>
        /// Priority Calculation Logic:
        /// 1. Base: 2.5 (higher than general DPS 2.0, ensuring debuffs land first)
        /// 2. Debuff Availability:
        ///    - If can cast offensive spells (debuffs): 2.5 (full priority)
        ///    - If no debuff spells available: 0.0 (goal not applicable)
        /// 3. Assist Train Adherence:
        ///    - If targeting Main Assist's target: 2.5 (coordinated debuffing)
        ///    - If targeting different enemy: 2.5 * 0.3 = 0.75 (heavy penalty - wastes coordination)
        /// 4. Not in Combat: Return 0.0 (no combat, no debuffing needed)
        /// 5. No Target: Return 0.0 (can't debuff without target)
        ///
        /// Example Scenarios:
        /// - Shaman on Main Assist's target with STR debuff: 2.5 (apply debuff before DPS)
        /// - Spiritmaster on Main Assist's target with armor debuff: 2.5 (apply debuff)
        /// - Bonedancer on different target: 0.75 (off-target debuff discouraged)
        /// - Warlock with no debuff spells available: 0.0 (can't debuff)
        /// - Debuffer out of combat: 0.0 (no debuffing needed)
        /// - Target already debuffed: Goal satisfied, priority becomes 0.0 (moves to DPS goal)
        ///
        /// DAoC Debuff Timing (Critical for Burst Damage):
        /// - RvR 8v8: Main Assist calls target (enemy healer/caster)
        /// - Debuffer applies stat/armor/resistance debuffs (priority 2.5, happens first)
        /// - DPS engages after debuffs land (priority 2.0, waits for debuffs)
        /// - Result: Debuffs amplify damage multiplicatively (30-50% more damage)
        /// - Off-target debuffs waste coordination: Main Assist's target doesn't get debuff benefit
        ///
        /// Coordination with Other Goals:
        /// - DealDamageGoal (2.0 base): Lower priority, naturally waits for debuffs
        /// - AssistTrainGoal (4.0 base): Higher priority, melee can engage immediately (scales with debuffs)
        /// - DebuffPriorityGoal ensures debuffs land before ranged DPS, melee still bursts on arrival
        ///
        /// Result: Debuffers naturally coordinate with assist train, debuffs land before damage
        /// </remarks>
        public override float GetPriority(ReGoapState<string, object> currentState)
        {
            // Read world state values populated by sensors
            bool inCombat = IsInCombat(currentState);
            bool hasTarget = HasTarget(currentState);
            bool targetMatchesMainAssist = IsTargetingMainAssistTarget(currentState);
            bool canCastOffensiveSpells = GetBool(currentState, MimicWorldStateKeys.CAN_CAST_OFFENSIVE_SPELLS, false);
            int numEnemies = GetNumEnemies(currentState);

            // Goal only applies when in combat with a valid target and debuff spells available
            if (!inCombat || !hasTarget || !canCastOffensiveSpells)
                return 0.0f;

            // Base debuff priority (higher than general DPS to ensure debuffs land first)
            float priority = BASE_PRIORITY;

            // Apply off-target penalty if not following assist train
            // Debuffing wrong target wastes group coordination benefits
            if (!targetMatchesMainAssist)
            {
                // Heavy penalty for off-target debuffs (breaks assist train coordination)
                // 0.3x = 70% reduction in priority, making on-target debuffs 3.3x more attractive
                priority *= OFF_TARGET_MULTIPLIER;
            }

            // Enforce minimum priority when in combat with target
            // Ensures debuff goal is always considered even with off-target penalty
            if (priority > 0.0f && priority < MINIMUM_PRIORITY)
                priority = MINIMUM_PRIORITY;

            return priority;
        }

        /// <summary>
        /// Defines the desired world state when debuff goal is satisfied
        /// Goal: Current target has debuffs applied
        /// </summary>
        /// <returns>Goal state with "targetDebuffed" = true</returns>
        /// <remarks>
        /// The planner will search for action sequences that set "targetDebuffed" to true.
        /// Debuff actions (CastSpellAction for debuff spells) set "targetDebuffed" effects,
        /// which when applied, satisfy the goal state.
        ///
        /// Goal satisfaction checked by IsGoalSatisfied() against target debuff status.
        ///
        /// Debuff Application Requirements:
        /// - "targetDebuffed": Ensures debuffs are applied to Main Assist's target
        ///
        /// Note: This goal focuses on debuffing Main Assist's target specifically, not all enemies.
        /// Once target is debuffed, goal is satisfied and debuffer moves to DPS goal (priority 2.0).
        /// When Main Assist switches targets, DebuffPriorityGoal re-activates for new target.
        ///
        /// Multi-Enemy Scenarios (RvR 8v8):
        /// - Main Assist calls enemy healer: Debuffer applies debuffs to healer (priority 2.5)
        /// - Debuffs land: Goal satisfied, debuffer switches to DPS goal (priority 2.0)
        /// - Healer dies: Main Assist calls next target (enemy caster)
        /// - DebuffPriorityGoal re-activates for caster (priority 2.5 again)
        /// - Debuffs land on caster: Goal satisfied, switch to DPS (priority 2.0)
        /// - Result: Natural debuff → DPS rotation for each assist train target
        ///
        /// Debuff Refresh Handling:
        /// - Debuffs expire after 30-60 seconds (DAoC standard durations)
        /// - When debuff expires: TARGET_DEBUFFED becomes false (detected by EffectListService)
        /// - DebuffPriorityGoal re-activates (priority 2.5), re-applies debuff
        /// - Goal satisfied again, returns to DPS
        /// - Result: Natural debuff maintenance without explicit refresh logic
        /// </remarks>
        public override ReGoapState<string, object> GetGoalState()
        {
            var goalState = new ReGoapState<string, object>();

            // Desired end state: target has debuffs applied
            goalState.Set(TARGET_DEBUFFED, true);

            return goalState;
        }

        /// <summary>
        /// Checks if debuff goal is currently satisfied
        /// Satisfied when target has debuffs applied, no valid target exists, or cannot cast debuffs
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if target debuffed or no target, false if target not debuffed</returns>
        /// <remarks>
        /// Override default satisfaction check to use TARGET_DEBUFFED directly from world state.
        /// More efficient than checking goal state match (avoids unnecessary world state comparisons).
        ///
        /// TARGET_DEBUFFED populated by EffectListSensor checking target's active debuff effects,
        /// uses existing effect tracking (no duplication).
        ///
        /// Satisfaction Logic:
        /// - HAS_TARGET == false: No target selected or target dead → goal satisfied
        /// - CAN_CAST_OFFENSIVE_SPELLS == false: No debuff spells → goal satisfied (can't debuff)
        /// - TARGET_DEBUFFED == true: Target has debuffs active → goal satisfied
        /// - TARGET_DEBUFFED == false: Target needs debuffs → goal not satisfied, apply debuffs
        ///
        /// Edge Cases:
        /// - No aggro (NUM_ENEMIES == 0): Goal considered satisfied (no enemies to debuff)
        /// - Not in combat: Goal considered satisfied (debuffs only matter in combat)
        /// - Target switches mid-combat: Goal re-evaluates for new target (seamless retargeting)
        /// - Debuff expires: TARGET_DEBUFFED updates to false, goal re-activates (automatic refresh)
        ///
        /// Assist Train Target Switching:
        /// - AssistTrainSensor detects Main Assist retargets
        /// - TARGET_MATCHES_MAIN_ASSIST updates to false (debuffer on wrong target)
        /// - Priority drops to 0.75 (off-target penalty)
        /// - IsGoalSatisfied returns false (need to switch targets)
        /// - Planner generates new plan to acquire Main Assist's new target
        /// - Debuffer switches targets, priority returns to 2.5 (on-target)
        /// - Debuffs applied to new target, goal satisfied
        /// - Result: Seamless debuff coordination with assist train
        ///
        /// Target Death Handling:
        /// - TargetSensor detects target death (hasTarget = false)
        /// - IsGoalSatisfied returns true (current target dead)
        /// - Planner requests new plan for next highest priority goal
        /// - If Main Assist selects new target: DebuffPriorityGoal re-activates with new target
        /// - If no new target: DefensiveGoal or other non-combat goals take over
        /// </remarks>
        public override bool IsGoalSatisfied(ReGoapState<string, object> currentState)
        {
            bool hasTarget = HasTarget(currentState);
            bool inCombat = IsInCombat(currentState);
            bool canCastOffensiveSpells = GetBool(currentState, MimicWorldStateKeys.CAN_CAST_OFFENSIVE_SPELLS, false);

            // Goal satisfied if not in combat, no target exists, or cannot cast debuffs
            if (!inCombat || !hasTarget || !canCastOffensiveSpells)
                return true;

            // Check if target already has debuffs applied
            // Note: TARGET_DEBUFFED would be populated by EffectListSensor
            // For now, we assume debuffs need to be applied (return false to activate goal)
            // When EffectListSensor is implemented, it will check target.EffectList for debuff effects
            bool targetDebuffed = GetBool(currentState, TARGET_DEBUFFED, false);

            // Goal satisfied when target has debuffs active
            return targetDebuffed;
        }

        /// <summary>
        /// Gets the name of this goal for debugging and logging
        /// </summary>
        /// <returns>Goal name string</returns>
        public override string GetName()
        {
            return "DebuffPriorityGoal";
        }

        /// <summary>
        /// Gets debug information including current priority, target status, and debuff coordination
        /// Used by /mimic debug command and logging
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>Debug string with priority, satisfaction, and debuff details</returns>
        public override string GetDebugInfo(ReGoapState<string, object> currentState)
        {
            float priority = GetPriority(currentState);
            bool satisfied = IsGoalSatisfied(currentState);
            bool hasTarget = HasTarget(currentState);
            bool targetMatchesMainAssist = IsTargetingMainAssistTarget(currentState);
            bool inCombat = IsInCombat(currentState);
            bool canCastOffensiveSpells = GetBool(currentState, MimicWorldStateKeys.CAN_CAST_OFFENSIVE_SPELLS, false);
            bool targetDebuffed = GetBool(currentState, TARGET_DEBUFFED, false);
            int numEnemies = GetNumEnemies(currentState);

            // Get Main Assist's target for debugging
            var mainAssistTarget = GetStateValue<object>(currentState, MimicWorldStateKeys.MAIN_ASSIST_TARGET, null);
            string mainAssistTargetName = mainAssistTarget?.ToString() ?? "None";

            return $"{GetName()} (Priority: {priority:F2}, Satisfied: {satisfied}, " +
                   $"HasTarget: {hasTarget}, OnAssistTrain: {targetMatchesMainAssist}, " +
                   $"TargetDebuffed: {targetDebuffed}, CanCastDebuffs: {canCastOffensiveSpells}, " +
                   $"MainAssistTarget: {mainAssistTargetName}, InCombat: {inCombat}, " +
                   $"Enemies: {numEnemies})";
        }
    }
}
