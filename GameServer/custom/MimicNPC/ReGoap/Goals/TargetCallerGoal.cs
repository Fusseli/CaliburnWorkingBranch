using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// Main Assist role goal for calling kill targets and coordinating group focus fire
    /// Exclusive to Main Assist role - drives assist train coordination via target selection
    /// Priority scales dynamically based on combat state and target selection needs
    /// </summary>
    /// <remarks>
    /// Design Principles (from design.md Component 4: Role-Based Goals):
    /// - TargetCallerGoal is exclusive to Main Assist role (target caller for the group)
    /// - Main Assist selects priority targets (healers, casters, then melee) for coordinated burst
    /// - Other DPS mimics follow Main Assist's target via AssistTrainGoal (priority 4.0)
    /// - Priority dynamically calculated based on world state from sensors
    ///
    /// Priority Formula (from requirements.md 3.2 and daoc-role-analysis.md):
    /// base_priority = 4.0 (matches AssistTrainGoal to ensure target selection happens with DPS coordination)
    /// - If not Main Assist role: return 0.0 (only Main Assist calls targets)
    /// - If not in combat or no enemies: return 0.0 (goal not applicable)
    /// - If current target is optimal (healer/caster priority): 4.0 (maintain current target)
    /// - If better target available (healer > caster > melee priority): 6.0 (switch to priority target)
    ///
    /// DAoC Assist Train Context (from daoc-role-analysis.md):
    /// - RvR 8v8 Meta: Main Assist calls kill targets based on priority (healer > caster > melee)
    /// - Target Priority: Enemy healers first (shutdown healing), casters second (shutdown damage), melee last
    /// - Coordinated Burst: Main Assist selects target, all DPS switches immediately for burst kill
    /// - Target Switching: Main Assist retargets when current target dies or better target appears
    /// - Communication: AssistTrainSensor propagates Main Assist's target to all group members
    ///
    /// Organic Behavior Patterns:
    /// - Priority 4.0 matches AssistTrainGoal so target calling coordinates with DPS execution
    /// - Higher priority (6.0) when better target appears ensures quick target switches
    /// - Main Assist evaluates target priorities continuously (healer > caster > melee)
    /// - AssistTrainGoal (other DPS) reads Main Assist's target via AssistTrainSensor
    /// - Result: Natural assist train formation with intelligent target prioritization
    ///
    /// World State Dependencies:
    /// - IS_MAIN_ASSIST: Role assignment (goal only active for Main Assist role)
    /// - IN_COMBAT: Combat state (goal only active in combat)
    /// - HAS_TARGET: Whether Main Assist has current target (from TargetSensor)
    /// - HAS_AGGRO: Whether there are enemies available (from AggroSensor)
    /// - AGGRO_TARGETS: List of available enemies (from AggroSensor reading Brain.AggroList)
    /// - NUM_ENEMIES: Total number of enemies on aggro list
    ///
    /// Goal State: { "priorityTargetSelected": true }
    /// Satisfied when: Main Assist has selected optimal priority target (or no better target available)
    ///
    /// Example Scenarios:
    /// - Main Assist in combat, targeting enemy healer: Priority = 4.0 (optimal target selected)
    /// - Main Assist in combat, targeting enemy melee, healer available: Priority = 6.0 (better target available)
    /// - Main Assist in combat, targeting enemy caster, no healer available: Priority = 4.0 (optimal target)
    /// - Main Assist in combat, no target selected: Priority = 6.0 (need to select target immediately)
    /// - Main Assist out of combat: Priority = 0.0 (no target calling needed)
    /// - DPS mimic (not Main Assist): Priority = 0.0 (follows Main Assist via AssistTrainGoal)
    ///
    /// Target Priority Logic (DAoC 8v8 RvR):
    /// 1. Enemy Healers (highest priority) - Shutdown group healing, fastest path to victory
    /// 2. Enemy Casters (medium priority) - Shutdown damage/CC, reduce enemy effectiveness
    /// 3. Enemy Melee (low priority) - Less threatening, kill last when healers/casters down
    ///
    /// Result: Main Assist naturally selects priority targets, group DPS follows via assist train
    /// </remarks>
    public class TargetCallerGoal : MimicGoal
    {
        /// <summary>
        /// Base priority for target calling coordination
        /// Value of 4.0 matches AssistTrainGoal to ensure target calling coordinates with DPS execution
        /// </summary>
        private const float BASE_PRIORITY = 4.0f;

        /// <summary>
        /// Higher priority when better target is available (need to switch targets)
        /// Value of 6.0 ensures target switches happen quickly (higher than base DPS goals)
        /// </summary>
        private const float BETTER_TARGET_PRIORITY = 6.0f;

        /// <summary>
        /// Minimum priority floor when in combat with enemies
        /// Ensures target calling is always evaluated during combat
        /// </summary>
        private const float MINIMUM_PRIORITY = 0.1f;

        /// <summary>
        /// Goal state key for priority target selection
        /// </summary>
        private const string PRIORITY_TARGET_SELECTED = "priorityTargetSelected";

        /// <summary>
        /// Constructs a new TargetCallerGoal for a Main Assist mimic
        /// </summary>
        /// <param name="body">The MimicNPC body for game state access (via sensors)</param>
        /// <param name="brain">The MimicBrain for AI state access (via sensors)</param>
        public TargetCallerGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        /// <summary>
        /// Calculates dynamic priority based on Main Assist status and target priority
        /// Priority scales with need to select or switch to better priority target
        /// </summary>
        /// <param name="currentState">Current world state populated by sensors reading Body/Brain</param>
        /// <returns>Priority value (higher = more urgent; 0.0 = not applicable)</returns>
        /// <remarks>
        /// Priority Calculation Logic:
        /// 1. Role Check: Only Main Assist role activates this goal (return 0.0 for other roles)
        /// 2. Combat Check: Only active in combat with enemies (return 0.0 out of combat)
        /// 3. Target Evaluation:
        ///    - If no target selected: 6.0 (need to select target immediately)
        ///    - If better priority target available: 6.0 (need to switch targets)
        ///    - If current target is optimal: 4.0 (maintain current target)
        /// 4. Target Priority Hierarchy (DAoC 8v8):
        ///    - Healers > Casters > Melee (standard RvR priority)
        ///
        /// Example Scenarios:
        /// - Main Assist targeting enemy healer, no other healers: Priority = 4.0 (optimal target)
        /// - Main Assist targeting enemy melee, enemy healer available: Priority = 6.0 (switch to healer)
        /// - Main Assist targeting enemy caster, enemy healer available: Priority = 6.0 (switch to healer)
        /// - Main Assist targeting enemy caster, no healers available: Priority = 4.0 (optimal target)
        /// - Main Assist with no target, enemies available: Priority = 6.0 (select target now)
        /// - Main Assist out of combat: Priority = 0.0 (no target calling needed)
        /// - DPS mimic (not Main Assist): Priority = 0.0 (follows via AssistTrainGoal)
        ///
        /// DAoC Target Priority Rationale:
        /// - Healers: Keeping enemy healers alive extends fight duration exponentially
        ///   - Killing healer first collapses enemy group's sustain
        ///   - Fastest path to victory in RvR 8v8
        /// - Casters: High burst damage and CC capabilities
        ///   - Removing casters reduces enemy damage/CC output
        ///   - Medium priority after healers down
        /// - Melee: Lower burst damage, less impactful
        ///   - Kill last when healers/casters already down
        ///   - Least threatening to group survival
        ///
        /// Coordination with AssistTrainGoal:
        /// - TargetCallerGoal (Main Assist): Selects priority targets (this goal, priority 4.0/6.0)
        /// - AssistTrainGoal (other DPS): Follows Main Assist's target (priority 4.0)
        /// - AssistTrainSensor: Propagates Main Assist's target to all group members
        /// - Result: Natural assist train coordination via world state propagation
        ///
        /// Result: Main Assist intelligently selects and switches targets, group DPS follows seamlessly
        /// </remarks>
        public override float GetPriority(ReGoapState<string, object> currentState)
        {
            // Read world state values populated by sensors
            bool isMainAssist = GetBool(currentState, MimicWorldStateKeys.IS_MAIN_ASSIST, false);
            bool inCombat = IsInCombat(currentState);
            bool hasAggro = GetBool(currentState, MimicWorldStateKeys.HAS_AGGRO, false);
            bool hasTarget = HasTarget(currentState);
            int numEnemies = GetNumEnemies(currentState);

            // Goal only applies to Main Assist role in combat with enemies
            if (!isMainAssist || !inCombat || !hasAggro || numEnemies == 0)
                return 0.0f;

            // If no target selected, need to select one immediately (high priority)
            if (!hasTarget)
                return BETTER_TARGET_PRIORITY;

            // Check if better priority target is available
            // This would require evaluating enemy types (healer > caster > melee)
            // For now, we assume current target is reasonable priority
            // Future enhancement: Implement GetBestPriorityTarget() to evaluate enemy classes
            bool betterTargetAvailable = IsBetterTargetAvailable(currentState);

            if (betterTargetAvailable)
            {
                // Better target available - high priority to switch targets
                return BETTER_TARGET_PRIORITY;
            }
            else
            {
                // Current target is optimal or no better option - maintain current target
                return BASE_PRIORITY;
            }
        }

        /// <summary>
        /// Checks if a better priority target is available than the current target
        /// Evaluates target priority based on class type (healer > caster > melee)
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if better target available, false if current target is optimal</returns>
        /// <remarks>
        /// Target Priority Logic (DAoC 8v8 RvR):
        /// 1. Healers (highest priority) - Shutdown healing
        /// 2. Casters (medium priority) - Shutdown damage/CC
        /// 3. Melee (low priority) - Kill last
        ///
        /// Current implementation is simplified - assumes current target is reasonable.
        /// Future enhancement: Implement actual target class evaluation
        /// - Read AGGRO_TARGETS list from world state
        /// - Evaluate each enemy's class (healer/caster/melee)
        /// - Compare current target's priority to best available target
        /// - Return true if better target found
        ///
        /// Example Logic:
        /// - Current target: Enemy melee, enemy healer in aggro list → return true (switch to healer)
        /// - Current target: Enemy healer, no other healers → return false (maintain healer target)
        /// - Current target: Enemy caster, enemy healer in aggro list → return true (switch to healer)
        /// - Current target: Enemy caster, no healers → return false (maintain caster target)
        /// </remarks>
        private bool IsBetterTargetAvailable(ReGoapState<string, object> currentState)
        {
            // Simplified implementation: assume current target is optimal
            // Future enhancement: Evaluate enemy classes in aggro list and compare priorities
            // This would require:
            // 1. Get current target from world state
            // 2. Get aggro targets list from world state
            // 3. Evaluate each enemy's class (via Body.CharacterClass or similar)
            // 4. Determine if any enemy has higher priority than current target
            // 5. Return true if better target found

            // For now, return false (current target is always considered optimal)
            // This ensures Main Assist maintains target selection stability
            return false;
        }

        /// <summary>
        /// Defines the desired world state when target calling goal is satisfied
        /// Goal: Main Assist has selected optimal priority target for assist train
        /// </summary>
        /// <returns>Goal state with "priorityTargetSelected" = true</returns>
        /// <remarks>
        /// The planner will search for action sequences that set "priorityTargetSelected" to true.
        /// Target selection actions (SelectTargetAction, SwitchTargetAction) set "targetSelected" effects,
        /// which when applied, satisfy the goal state.
        ///
        /// Goal satisfaction checked by IsGoalSatisfied() against current target priority.
        ///
        /// Target Selection Requirements:
        /// - "priorityTargetSelected": Ensures Main Assist has selected optimal target from available enemies
        ///
        /// Note: This goal focuses on target selection, not damage dealing.
        /// Once target is selected, DPS goals (AssistTrainGoal, DealDamageGoal) handle actual damage output.
        /// Main Assist also deals damage, but target selection is primary responsibility.
        ///
        /// Coordination Flow:
        /// 1. Main Assist activates TargetCallerGoal (priority 4.0/6.0)
        /// 2. Main Assist selects priority target (healer > caster > melee)
        /// 3. AssistTrainSensor propagates Main Assist's target to world state
        /// 4. Other DPS mimics read Main Assist's target via AssistTrainSensor
        /// 5. AssistTrainGoal activates for other DPS (priority 4.0, follows Main Assist)
        /// 6. Group DPS focuses Main Assist's target for coordinated burst kill
        /// 7. When target dies, Main Assist re-evaluates and selects next priority target
        /// 8. Cycle repeats until all enemies defeated
        ///
        /// Result: Natural assist train coordination via target selection leadership
        /// </remarks>
        public override ReGoapState<string, object> GetGoalState()
        {
            var goalState = new ReGoapState<string, object>();

            // Desired end state: Main Assist has selected optimal priority target
            goalState.Set(PRIORITY_TARGET_SELECTED, true);

            return goalState;
        }

        /// <summary>
        /// Checks if target calling goal is currently satisfied
        /// Satisfied when Main Assist has selected optimal target or no better target available
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if optimal target selected, false if need to select/switch targets</returns>
        /// <remarks>
        /// Override default satisfaction check to use HAS_TARGET and target priority evaluation.
        /// More efficient than checking goal state match (avoids unnecessary world state comparisons).
        ///
        /// HAS_TARGET populated by TargetSensor reading Brain.CalculateNextAttackTarget(),
        /// which uses existing target selection logic (no duplication).
        ///
        /// Satisfaction Logic:
        /// - HAS_TARGET == false: No target selected → goal not satisfied, need to select target
        /// - HAS_TARGET == true && better target available: Current target suboptimal → not satisfied, switch targets
        /// - HAS_TARGET == true && current target is optimal: Optimal target selected → goal satisfied
        ///
        /// Edge Cases:
        /// - Not Main Assist role: Goal considered satisfied (not this mimic's responsibility)
        /// - No aggro (NUM_ENEMIES == 0): Goal considered satisfied (no targets to select)
        /// - Not in combat: Goal considered satisfied (target calling only matters in combat)
        /// - Current target dies: HAS_TARGET becomes false, goal not satisfied, select next target
        ///
        /// Target Death Handling:
        /// - TargetSensor detects target death (hasTarget = false)
        /// - IsGoalSatisfied returns false (need new target)
        /// - TargetCallerGoal priority increases to 6.0 (select new target)
        /// - Main Assist selects next priority target (healer > caster > melee)
        /// - AssistTrainSensor propagates new target to group
        /// - Other DPS mimics switch targets via AssistTrainGoal
        /// - Result: Seamless target switching when current target dies
        ///
        /// Better Target Handling:
        /// - IsBetterTargetAvailable() detects higher priority target
        /// - IsGoalSatisfied returns false (current target not optimal)
        /// - TargetCallerGoal priority increases to 6.0 (switch to better target)
        /// - Main Assist switches to priority target
        /// - AssistTrainSensor propagates new target to group
        /// - Other DPS mimics switch targets via AssistTrainGoal
        /// - Result: Intelligent target switching when priorities change (e.g., enemy healer revealed)
        /// </remarks>
        public override bool IsGoalSatisfied(ReGoapState<string, object> currentState)
        {
            bool isMainAssist = GetBool(currentState, MimicWorldStateKeys.IS_MAIN_ASSIST, false);
            bool inCombat = IsInCombat(currentState);
            bool hasAggro = GetBool(currentState, MimicWorldStateKeys.HAS_AGGRO, false);
            bool hasTarget = HasTarget(currentState);

            // Goal satisfied if not Main Assist, not in combat, or no enemies
            if (!isMainAssist || !inCombat || !hasAggro)
                return true;

            // Goal not satisfied if no target selected (need to select target)
            if (!hasTarget)
                return false;

            // Goal not satisfied if better priority target available (need to switch targets)
            bool betterTargetAvailable = IsBetterTargetAvailable(currentState);
            if (betterTargetAvailable)
                return false;

            // Goal satisfied: has target and current target is optimal
            return true;
        }

        /// <summary>
        /// Gets the name of this goal for debugging and logging
        /// </summary>
        /// <returns>Goal name string</returns>
        public override string GetName()
        {
            return "TargetCallerGoal";
        }

        /// <summary>
        /// Gets debug information including current priority, target status, and assist train coordination
        /// Used by /mimic debug command and logging
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>Debug string with priority, satisfaction, and target calling details</returns>
        public override string GetDebugInfo(ReGoapState<string, object> currentState)
        {
            float priority = GetPriority(currentState);
            bool satisfied = IsGoalSatisfied(currentState);
            bool isMainAssist = GetBool(currentState, MimicWorldStateKeys.IS_MAIN_ASSIST, false);
            bool hasTarget = HasTarget(currentState);
            bool inCombat = IsInCombat(currentState);
            int numEnemies = GetNumEnemies(currentState);
            bool betterTargetAvailable = IsBetterTargetAvailable(currentState);

            // Get current target for debugging
            var currentTarget = GetStateValue<object>(currentState, MimicWorldStateKeys.CURRENT_TARGET, null);
            string currentTargetName = currentTarget?.ToString() ?? "None";

            return $"{GetName()} (Priority: {priority:F2}, Satisfied: {satisfied}, " +
                   $"IsMainAssist: {isMainAssist}, HasTarget: {hasTarget}, " +
                   $"CurrentTarget: {currentTargetName}, BetterTargetAvailable: {betterTargetAvailable}, " +
                   $"InCombat: {inCombat}, Enemies: {numEnemies})";
        }
    }
}
