using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// Support role goal for removing enemy mezz effects from friendly group members
    /// Used by classes with cure mezz spells (Clerics, Healers, Bards, Druids, Shamans, etc.)
    /// Priority is high when friendly is mezzed and mimic has cure mezz available
    /// </summary>
    /// <remarks>
    /// Design Principles (from design.md Component 4: Role-Based Goals):
    /// - BreakEnemyMezzGoal is a support/healer goal for counter-CC via cure mezz spells
    /// - Only applicable to classes that have cure mezz spell capability
    /// - Priority dynamically calculated based on friendly mezz status and cure mezz availability
    ///
    /// DAoC Mezz Mechanics Context:
    /// - Mezz (Mesmerize) disables friendly from combat (cannot act)
    /// - ONLY way to free a mezzed friendly is to cast a cure mezz spell on them
    /// - Damage does NOT break friendly mezz (only breaks enemy mezz)
    /// - Cure mezz spells are limited to specific support/healer classes
    ///
    /// Priority Formula (from tasks.md Task 32):
    /// - If any friendly is mezzed AND mimic has cure mezz spell: priority = 8.0 (high priority)
    /// - Else: priority = 0.0 (goal not applicable)
    ///
    /// World State Dependencies:
    /// - MEMBER_TO_CURE_MEZZ: Friendly group member needing mezz cure (from MimicGroup.MemberToCureMezz)
    /// - CAN_CAST_CURE_MEZZ: Whether mimic has cure mezz spell available (from Body.CureMezz)
    /// - CAN_CAST: Whether mimic can currently cast (not stunned/mezzed/silenced)
    /// - ALREADY_CASTING_CURE_MEZZ: Coordination flag to prevent duplicate casts
    ///
    /// Goal State: { "FRIENDLY_TARGET_FREE": true }
    /// Satisfied when: No friendly group members are mezzed (MEMBER_TO_CURE_MEZZ == null)
    ///
    /// Example Scenarios:
    /// - Enemy Sorcerer mezzes friendly tank, Cleric has cure mezz: Priority = 8.0 (cure tank immediately)
    /// - Enemy Bard mezzes friendly healer, Healer has cure mezz: Priority = 8.0 (cure healer - critical)
    /// - No friendlies mezzed: Priority = 0.0 (goal not active)
    /// - Friendly mezzed but mimic has no cure mezz spell: Priority = 0.0 (cannot help)
    /// - Multiple friendlies mezzed: Priority = 8.0 (cure highest priority target from MEMBER_TO_CURE_MEZZ)
    /// - Another healer already casting cure mezz: Action will check ALREADY_CASTING_CURE_MEZZ flag
    ///
    /// Coordination with Other Goals:
    /// - Higher priority than normal DPS goals (2.0-4.0) to prioritize ally rescue
    /// - Lower priority than critical healing (<25% HP = 9.38+) - save dying allies first
    /// - Competes with InterruptEnemyCasterGoal (9.0) - interrupt may prevent additional mezzes
    /// - Similar priority to emergency healing (8.0) - both are urgent support actions
    ///
    /// Coordination with Other Healers:
    /// - ALREADY_CASTING_CURE_MEZZ flag prevents multiple healers from curing same target
    /// - First healer to start cast sets flag, others skip this target
    /// - Similar to ALREADY_CAST_INSTANT_HEAL coordination for healing
    ///
    /// Result: Healers/support classes automatically cure mezzed allies when they have the spell
    /// </remarks>
    public class BreakEnemyMezzGoal : MimicGoal
    {
        /// <summary>
        /// High priority for curing friendly mezz (support rescue behavior)
        /// Value of 8.0 places cure mezz above normal combat but below critical healing
        /// </summary>
        private const float HIGH_PRIORITY = 8.0f;

        /// <summary>
        /// Goal state key for friendly target being freed from mezz
        /// </summary>
        private const string FRIENDLY_TARGET_FREE = "friendlyTargetFree";

        /// <summary>
        /// Constructs a new BreakEnemyMezzGoal for cure mezz support behavior
        /// </summary>
        /// <param name="body">The MimicNPC body for game state access (via sensors)</param>
        /// <param name="brain">The MimicBrain for AI state access (via sensors)</param>
        public BreakEnemyMezzGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        /// <summary>
        /// Calculates dynamic priority based on friendly mezz status and cure mezz availability
        /// High priority (8.0) when friendly mezzed and mimic has cure mezz spell
        /// </summary>
        /// <param name="currentState">Current world state populated by sensors reading Body/Brain</param>
        /// <returns>Priority value (higher = more urgent; 0.0 = not applicable)</returns>
        /// <remarks>
        /// Priority Calculation Logic:
        /// 1. Check if any friendly is mezzed (MEMBER_TO_CURE_MEZZ != null)
        /// 2. Check if mimic has cure mezz spell (CAN_CAST_CURE_MEZZ)
        /// 3. Check if mimic can cast (not stunned/mezzed/silenced)
        /// 4. If all conditions met: return 8.0 (high priority cure)
        /// 5. Else: return 0.0 (goal not applicable)
        ///
        /// Example Scenarios:
        /// - Friendly tank mezzed, Cleric has cure mezz: Priority = 8.0 (cure tank)
        /// - Friendly healer mezzed, Healer has cure mezz: Priority = 8.0 (cure healer - critical)
        /// - No friendlies mezzed: Priority = 0.0 (no cure needed)
        /// - Friendly mezzed, but mimic has no cure mezz: Priority = 0.0 (cannot help)
        /// - Friendly mezzed, mimic has cure mezz, but mimic is stunned: Priority = 0.0 (cannot cast)
        /// - Multiple friendlies mezzed: Priority = 8.0 (MEMBER_TO_CURE_MEZZ is highest priority)
        ///
        /// Coordination Context:
        /// - 8.0 priority competes with:
        ///   - InterruptEnemyCasterGoal (9.0) - interrupt slightly higher to prevent more mezzes
        ///   - PrimaryHealingGoal (emergency 8.0) - similar urgency for critical support
        ///   - HealGroupGoal (critical 9.38+) - saving dying allies is more urgent
        ///   - AssistTrainGoal (4.0) - cure mezz far more important than coordinated damage
        ///   - DealDamageGoal (2.0) - cure mezz far more important than general DPS
        ///
        /// DAoC Support Mechanics:
        /// - Cure mezz is instant or very fast cast (1-2 seconds)
        /// - Removes mezz effect immediately on cast completion
        /// - Critical for RvR 8v8 where enemy CC can disable key group members
        /// - Priority ensures support classes prioritize cure mezz over damage
        ///
        /// Action Coordination:
        /// - CastSpellAction for cure mezz will check ALREADY_CASTING_CURE_MEZZ flag
        /// - Prevents multiple healers from curing same target simultaneously
        /// - First healer to start cast sets flag, others skip
        ///
        /// Result: Support classes instinctively cure mezzed allies when able
        /// </remarks>
        public override float GetPriority(ReGoapState<string, object> currentState)
        {
            // Check if any friendly group member is mezzed
            var memberToCureMezz = GetStateValue<object>(currentState, MimicWorldStateKeys.MEMBER_TO_CURE_MEZZ, null);
            bool friendlyMezzed = memberToCureMezz != null;

            // Goal only applies when friendly is mezzed
            if (!friendlyMezzed)
                return 0.0f;

            // Check if mimic has cure mezz spell available
            bool canCastCureMezz = GetBool(currentState, MimicWorldStateKeys.CAN_CAST_CURE_MEZZ, false);
            if (!canCastCureMezz)
                return 0.0f; // Cannot cure - this mimic doesn't have the spell

            // Check if mimic can currently cast (not stunned/mezzed/silenced)
            bool canCast = GetBool(currentState, MimicWorldStateKeys.CAN_CAST, false);
            if (!canCast)
                return 0.0f; // Cannot cast right now - disabled

            // All conditions met: friendly mezzed, mimic has cure mezz, mimic can cast
            return HIGH_PRIORITY;
        }

        /// <summary>
        /// Defines the desired world state when cure mezz goal is satisfied
        /// Goal: Friendly target is freed from mezz effect via cure mezz spell
        /// </summary>
        /// <returns>Goal state with "friendlyTargetFree" = true</returns>
        /// <remarks>
        /// The planner will search for action sequences that set "friendlyTargetFree" to true.
        /// CastSpellAction for cure mezz spells will set "friendlyTargetFree" effect when executed.
        ///
        /// Goal State Satisfaction:
        /// - Cure mezz spell action sets "friendlyTargetFree" = true
        /// - Planner recognizes cure mezz action satisfies goal state
        /// - Cost calculation determines which cure mezz spell to use (if multiple available)
        ///
        /// Goal satisfaction checked by IsGoalSatisfied() against MEMBER_TO_CURE_MEZZ == null.
        /// Once cure mezz cast completes, GroupHealthSensor updates MEMBER_TO_CURE_MEZZ to null.
        /// </remarks>
        public override ReGoapState<string, object> GetGoalState()
        {
            var goalState = new ReGoapState<string, object>();

            // Desired end state: friendly target is freed from mezz via cure spell
            goalState.Set(FRIENDLY_TARGET_FREE, true);

            return goalState;
        }

        /// <summary>
        /// Checks if cure mezz goal is currently satisfied
        /// Satisfied when no friendly group members are mezzed
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if no friendlies mezzed, false otherwise</returns>
        /// <remarks>
        /// Override default satisfaction check to use MEMBER_TO_CURE_MEZZ directly.
        /// More efficient than checking goal state match (avoids unnecessary world state comparisons).
        ///
        /// MEMBER_TO_CURE_MEZZ populated by GroupHealthSensor reading MimicGroup.MemberToCureMezz,
        /// which is calculated by existing CheckGroupHealth() logic (no duplication).
        ///
        /// Satisfaction Logic:
        /// - MEMBER_TO_CURE_MEZZ == null: No friendlies mezzed → goal satisfied
        /// - MEMBER_TO_CURE_MEZZ != null: Friendly mezzed → goal not satisfied (cure needed)
        ///
        /// Edge Cases:
        /// - Multiple friendlies mezzed: MEMBER_TO_CURE_MEZZ contains highest priority target
        /// - Friendly mezz expires naturally: MEMBER_TO_CURE_MEZZ becomes null → goal satisfied
        /// - Cure mezz spell cast: MEMBER_TO_CURE_MEZZ becomes null after cure → goal satisfied
        /// - Enemy re-mezzes friendly: MEMBER_TO_CURE_MEZZ updates with new target → goal re-activates
        ///
        /// Goal Re-Evaluation:
        /// - GroupHealthSensor updates MEMBER_TO_CURE_MEZZ every think tick (500ms)
        /// - If new friendly gets mezzed: goal re-activates with priority 8.0
        /// - If mezz cured: goal satisfaction triggers, planner requests new plan
        /// </remarks>
        public override bool IsGoalSatisfied(ReGoapState<string, object> currentState)
        {
            var memberToCureMezz = GetStateValue<object>(currentState, MimicWorldStateKeys.MEMBER_TO_CURE_MEZZ, null);
            return memberToCureMezz == null; // No friendlies mezzed = goal satisfied
        }

        /// <summary>
        /// Gets the name of this goal for debugging and logging
        /// </summary>
        /// <returns>Goal name string</returns>
        public override string GetName()
        {
            return "BreakEnemyMezzGoal";
        }

        /// <summary>
        /// Gets debug information including current priority, satisfaction, and mezz status
        /// Used by /mimic debug command and logging
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>Debug string with priority, satisfaction, and cure mezz details</returns>
        public override string GetDebugInfo(ReGoapState<string, object> currentState)
        {
            float priority = GetPriority(currentState);
            bool satisfied = IsGoalSatisfied(currentState);
            var memberToCureMezz = GetStateValue<object>(currentState, MimicWorldStateKeys.MEMBER_TO_CURE_MEZZ, null);
            bool canCastCureMezz = GetBool(currentState, MimicWorldStateKeys.CAN_CAST_CURE_MEZZ, false);
            bool canCast = GetBool(currentState, MimicWorldStateKeys.CAN_CAST, false);

            // Get member name for debugging
            string mezzedMemberName = memberToCureMezz?.ToString() ?? "None";

            return $"{GetName()} (Priority: {priority:F2}, Satisfied: {satisfied}, " +
                   $"MezzedMember: {mezzedMemberName}, " +
                   $"CanCureMezz: {canCastCureMezz}, CanCast: {canCast})";
        }
    }
}
