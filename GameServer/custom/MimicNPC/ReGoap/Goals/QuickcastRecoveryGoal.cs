using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// Quickcast recovery goal for caster DPS after repeated interrupts
    /// Activates when caster has been interrupted 2+ times and quickcast ability is available
    /// Uses quickcast to guarantee next spell lands despite interrupt pressure
    /// </summary>
    /// <remarks>
    /// Design Principles (from design.md Component 4: Role-Based Goals):
    /// - CasterDPS uses quickcast ability after repeated interrupts (2+ times)
    /// - Instant-cast spell guaranteed to land even under heavy interrupt pressure
    /// - Strategic cooldown management: save quickcast for emergencies (long cooldown)
    /// - Binary activation: either high priority (7.0) when needed or inactive (0.0)
    ///
    /// Priority System (from requirements.md 11 and daoc-role-analysis.md):
    /// - Base Priority: 7.0 (high priority when triggered)
    /// - Triggers when:
    ///   1. shouldUseQuickcast = true (2+ interrupts detected by InterruptSensor)
    ///   2. quickcastAvailable = true (not on cooldown, has ability)
    /// - Else: return 0.0 (goal not applicable, save quickcast for emergencies)
    ///
    /// DAoC Context (from daoc-role-analysis.md):
    /// - Quickcast: Realm ability that makes next spell instant-cast (0s cast time)
    /// - Long cooldown: 10+ minutes (must be used strategically, not wasted)
    /// - Interrupt meta: Enemy groups focus on interrupting key casters
    /// - After 2+ interrupts, caster is effectively locked out without quickcast
    /// - Quickcast allows caster to get critical spell off despite interrupt pressure
    /// - Common quickcast uses: Emergency heals, key debuffs, damage spells under pressure
    /// - List casters (Theurgist, Cabalist, Spiritmaster, Bonedancer) rely on quickcast
    ///
    /// Organic Behavior Patterns (from requirements.md 11.30 and design.md):
    /// - When quickcast goal activates, caster immediately uses quickcast ability
    /// - Next spell cast will be instant (0s cast time, cannot be interrupted)
    /// - After successful quickcast spell, goal deactivates (interruptCount reset)
    /// - Quickcast saved for emergencies: 100.0 cost when not needed (QuickcastCostCalculator)
    /// - Only used when interrupt count >= 2 (threshold from InterruptSensor)
    ///
    /// World State Dependencies (from MimicWorldStateKeys):
    /// - SHOULD_USE_QUICKCAST: InterruptSensor sets true when interruptCount >= 2
    /// - INTERRUPT_COUNT: Number of recent interrupts (from InterruptSensor)
    /// - QUICKCAST_AVAILABLE: Quickcast ability off cooldown and available (AbilityAvailabilitySensor)
    /// - HAS_QUICKCAST: Has the quickcast ability learned (AbilityAvailabilitySensor)
    /// - IN_COMBAT: Only use quickcast during active combat
    ///
    /// Goal State: { "spellCastSuccessfully": true }
    /// Satisfied when: Quickcast ability used and next spell successfully cast without interrupt
    ///
    /// Example Scenarios:
    /// - Theurgist interrupted twice by enemy assassin: QuickcastRecoveryGoal (7.0) activates, use quickcast
    /// - After quickcast used, next bolt spell instant-cast: lands successfully, goal satisfied
    /// - Cabalist interrupted 3 times, quickcast on cooldown: Goal priority 0.0 (can't use), KiteGoal takes over
    /// - Out of combat, interrupted once: Goal priority 0.0 (save cooldown, not emergency yet)
    /// - In combat, not interrupted: Goal priority 0.0 (save quickcast for when needed)
    /// </remarks>
    public class QuickcastRecoveryGoal : MimicGoal
    {
        /// <summary>
        /// High priority when quickcast recovery is needed
        /// 7.0 ensures this goal overrides normal DPS goals (3.0) but lower than KiteGoal (8.0)
        /// Priority hierarchy: Emergency heals (100.0) > Kite (8.0) > Quickcast (7.0) > Normal DPS (3.0)
        /// </summary>
        private const float QUICKCAST_RECOVERY_PRIORITY = 7.0f;

        /// <summary>
        /// Interrupt count threshold for activating quickcast
        /// 2+ interrupts = caster is being heavily pressured, needs quickcast to recover
        /// InterruptSensor sets SHOULD_USE_QUICKCAST when interruptCount >= 2
        /// </summary>
        private const int INTERRUPT_THRESHOLD = 2;

        /// <summary>
        /// Goal state key for quickcast recovery completion
        /// </summary>
        private const string SPELL_CAST_SUCCESSFULLY = "spellCastSuccessfully";

        /// <summary>
        /// Constructs a new QuickcastRecoveryGoal for a caster DPS mimic
        /// </summary>
        /// <param name="body">The MimicNPC body for game state access (via sensors)</param>
        /// <param name="brain">The MimicBrain for AI state access (via sensors)</param>
        public QuickcastRecoveryGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        /// <summary>
        /// Calculates priority based on interrupt status and quickcast availability
        /// Binary decision: high priority if needed and available, zero otherwise
        /// </summary>
        /// <param name="currentState">Current world state populated by sensors reading Body/Brain</param>
        /// <returns>7.0 if quickcast needed and available, 0.0 otherwise</returns>
        /// <remarks>
        /// Priority Logic:
        /// 1. Check IN_COMBAT (only use quickcast during active combat, save cooldown otherwise)
        /// 2. Check SHOULD_USE_QUICKCAST (InterruptSensor sets true when interruptCount >= 2)
        /// 3. Check QUICKCAST_AVAILABLE (not on cooldown, AbilityAvailabilitySensor)
        /// 4. Check HAS_QUICKCAST (mimic has learned the ability)
        /// 5. If all conditions true: Return 7.0 (high priority recovery)
        /// 6. If any condition false: Return 0.0 (goal not applicable, don't waste cooldown)
        ///
        /// Why High Priority (7.0)?
        /// - After 2+ interrupts, caster is effectively locked out of combat
        /// - Quickcast is the only way to guarantee spell lands under interrupt pressure
        /// - Without quickcast, caster must kite (8.0) or wait for interrupts to stop
        /// - Lower than KiteGoal (8.0) because kiting away from danger is more urgent
        /// - Higher than RangedDamageGoal (3.0) because recovery is more important than normal DPS
        ///
        /// Strategic Cooldown Management:
        /// - Quickcast has 10+ minute cooldown (600+ seconds)
        /// - Should NOT be used casually (QuickcastCostCalculator: 100.0 cost when not needed)
        /// - Only trigger when interrupt count >= 2 (emergency threshold)
        /// - Only trigger during active combat (don't waste on trivial encounters)
        ///
        /// Example Scenarios:
        /// - Theurgist interrupted twice, quickcast available, in combat: shouldUse=true, available=true → Priority 7.0
        /// - After quickcast used: Goal priority 0.0, RangedDamageGoal (3.0) resumes normal DPS
        /// - Cabalist interrupted 3 times, quickcast on cooldown: shouldUse=true, available=false → Priority 0.0, KiteGoal (8.0) takes over
        /// - Out of combat, interrupted twice: inCombat=false → Priority 0.0 (save cooldown)
        /// - In combat, interrupted once: shouldUse=false → Priority 0.0 (not emergency yet)
        ///
        /// Result: Caster uses quickcast strategically only when truly needed (2+ interrupts + combat)
        /// </remarks>
        public override float GetPriority(ReGoapState<string, object> currentState)
        {
            // Only use quickcast during combat (save cooldown for important fights)
            bool inCombat = IsInCombat(currentState);
            if (!inCombat)
                return 0.0f;

            // Read world state values populated by sensors
            bool shouldUseQuickcast = GetBool(currentState, MimicWorldStateKeys.SHOULD_USE_QUICKCAST, false);
            bool quickcastAvailable = GetBool(currentState, MimicWorldStateKeys.QUICKCAST_AVAILABLE, false);
            bool hasQuickcast = GetBool(currentState, MimicWorldStateKeys.HAS_QUICKCAST, false);

            // Check interrupt count directly as backup (shouldUseQuickcast set by InterruptSensor when >= 2)
            int interruptCount = GetInt(currentState, MimicWorldStateKeys.INTERRUPT_COUNT, 0);

            // If interrupted repeatedly (2+ times) AND quickcast available AND has ability, activate high priority
            // shouldUseQuickcast is the primary trigger (set by InterruptSensor)
            // interruptCount >= INTERRUPT_THRESHOLD is backup check
            if ((shouldUseQuickcast || interruptCount >= INTERRUPT_THRESHOLD) && quickcastAvailable && hasQuickcast)
            {
                return QUICKCAST_RECOVERY_PRIORITY; // 7.0 - high priority recovery
            }

            // Conditions not met - goal not applicable
            // Either: not interrupted enough, quickcast on cooldown, or no quickcast ability
            // RangedDamageGoal (3.0) or KiteGoal (8.0 if still being interrupted) will take over
            return 0.0f;
        }

        /// <summary>
        /// Defines the desired world state when quickcast recovery goal is satisfied
        /// Goal: Successfully cast spell using quickcast (instant cast, no interrupt)
        /// </summary>
        /// <returns>Goal state with "spellCastSuccessfully" = true</returns>
        /// <remarks>
        /// The planner will search for action sequences that set "spellCastSuccessfully" to true.
        /// UseAbilityAction (quickcast) sets "quickcastActive" effect.
        /// Following CastSpellAction with quickcast active sets "spellCastSuccessfully" effect.
        ///
        /// Goal satisfaction checked by IsGoalSatisfied() against successful spell cast.
        ///
        /// Action Sequence:
        /// 1. UseAbilityAction(Quickcast) - activates quickcast buff
        /// 2. CastSpellAction(DamageSpell) - spell becomes instant-cast, guaranteed to land
        /// 3. Goal satisfied after spell lands successfully
        ///
        /// Note: This goal focuses on using quickcast to recover from interrupt pressure.
        /// Once spell lands, goal is satisfied and normal DPS rotation resumes.
        /// InterruptSensor resets interrupt count after successful cast, preventing repeated quickcast use.
        /// </remarks>
        public override ReGoapState<string, object> GetGoalState()
        {
            var goalState = new ReGoapState<string, object>();

            // Desired end state: spell cast successfully using quickcast
            goalState.Set(SPELL_CAST_SUCCESSFULLY, true);

            return goalState;
        }

        /// <summary>
        /// Checks if quickcast recovery goal is currently satisfied
        /// Satisfied when quickcast has been used and spell successfully cast
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if recovery complete, false if still need to use quickcast</returns>
        /// <remarks>
        /// Override default satisfaction check to use interrupt status directly.
        /// More efficient than checking goal state match.
        ///
        /// InterruptSensor tracks interrupt count and resets after successful cast.
        /// SHOULD_USE_QUICKCAST populated by InterruptSensor based on interruptCount >= 2.
        ///
        /// Satisfaction Logic:
        /// - SHOULD_USE_QUICKCAST == false: Interrupt count reset, recovery complete → goal satisfied
        /// - INTERRUPT_COUNT < 2: Below threshold, no longer need quickcast → goal satisfied
        /// - QUICKCAST_AVAILABLE == false: Just used quickcast (on cooldown) → goal satisfied
        /// - Otherwise: Still need to use quickcast → goal not satisfied
        ///
        /// Alternative Satisfaction Criteria:
        /// If quickcast was just used (now on cooldown), assume goal satisfied even if still in combat.
        /// This prevents goal from staying active when quickcast is unavailable.
        /// </remarks>
        public override bool IsGoalSatisfied(ReGoapState<string, object> currentState)
        {
            bool shouldUseQuickcast = GetBool(currentState, MimicWorldStateKeys.SHOULD_USE_QUICKCAST, false);
            int interruptCount = GetInt(currentState, MimicWorldStateKeys.INTERRUPT_COUNT, 0);
            bool quickcastAvailable = GetBool(currentState, MimicWorldStateKeys.QUICKCAST_AVAILABLE, false);

            // Goal satisfied if:
            // 1. No longer need quickcast (interrupt count below threshold)
            // 2. Quickcast was just used (now on cooldown)
            if (!shouldUseQuickcast || interruptCount < INTERRUPT_THRESHOLD || !quickcastAvailable)
            {
                return true; // Recovery complete or quickcast used
            }

            // Still need to use quickcast
            return false;
        }

        /// <summary>
        /// Gets the name of this goal for debugging and logging
        /// </summary>
        /// <returns>Goal name string</returns>
        public override string GetName()
        {
            return "QuickcastRecoveryGoal";
        }

        /// <summary>
        /// Gets debug information including current priority and quickcast status
        /// Used by /mimic debug command and logging
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>Debug string with priority, satisfaction, and quickcast status indicators</returns>
        public override string GetDebugInfo(ReGoapState<string, object> currentState)
        {
            float priority = GetPriority(currentState);
            bool satisfied = IsGoalSatisfied(currentState);
            bool shouldUseQuickcast = GetBool(currentState, MimicWorldStateKeys.SHOULD_USE_QUICKCAST, false);
            int interruptCount = GetInt(currentState, MimicWorldStateKeys.INTERRUPT_COUNT, 0);
            bool quickcastAvailable = GetBool(currentState, MimicWorldStateKeys.QUICKCAST_AVAILABLE, false);
            bool hasQuickcast = GetBool(currentState, MimicWorldStateKeys.HAS_QUICKCAST, false);
            bool inCombat = IsInCombat(currentState);

            return $"{GetName()} (Priority: {priority:F2}, Satisfied: {satisfied}, " +
                   $"ShouldUseQC: {shouldUseQuickcast}, Interrupts: {interruptCount}, " +
                   $"QCAvailable: {quickcastAvailable}, HasQC: {hasQuickcast}, InCombat: {inCombat})";
        }
    }
}
