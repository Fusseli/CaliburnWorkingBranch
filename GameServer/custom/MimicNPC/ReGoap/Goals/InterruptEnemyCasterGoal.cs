using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// Universal combat goal for interrupting enemy casters to prevent damage/healing/CC spells
    /// Used by ALL roles - melee DPS, ranged DPS, caster DPS, tanks, healers, support
    /// Priority is very high (9.0) when enemy is actively casting and in interrupt range
    /// </summary>
    /// <remarks>
    /// Design Principles (from design.md Component 4: Role-Based Goals):
    /// - InterruptEnemyCasterGoal is a UNIVERSAL combat mechanic evaluated by ALL roles
    /// - Classes with instant cast abilities are most effective at interrupting
    /// - Melee classes interrupt via melee attacks (damage-based)
    /// - Caster classes interrupt via instant spells (bolts, instant damage)
    /// - All classes evaluate interrupt priority when enemy is casting
    /// - Priority dynamically calculated based on enemy casting status and interrupt capability
    ///
    /// DAoC Interrupt Mechanics Context:
    /// - Interrupts are game-winning mechanics in DAoC RvR
    /// - Prevent enemy healing (keeping targets alive), damage (killing allies), mezz (disabling group)
    /// - ANY class can interrupt - instant spells, melee attacks, abilities
    /// - Classes with many instant casts are naturally better interrupters
    /// - Melee classes interrupt via fast melee attacks (damage-based interrupts)
    /// - Caster classes interrupt via instant damage spells (bolts, instant nukes)
    ///
    /// Priority Formula (from daoc-role-analysis.md RvR Priority Hierarchy):
    /// - If enemy is casting AND in interrupt range: priority = 9.0 (critical priority)
    /// - Else: priority = 0.0 (goal not applicable)
    ///
    /// World State Dependencies:
    /// - ENEMY_CASTING: Whether target enemy is currently casting (from EnemyCastingSensor)
    /// - SHOULD_INTERRUPT: Whether enemy is in interrupt range and casting (from EnemyCastingSensor)
    /// - HAS_TARGET: Whether mimic has valid enemy target
    /// - CAN_CAST: Whether mimic can use interrupt abilities (not stunned/mezzed/silenced)
    ///
    /// Goal State: { "ENEMY_INTERRUPTED": true }
    /// Satisfied when: Enemy is not casting (ENEMY_CASTING == false)
    ///
    /// Example Scenarios:
    /// - Enemy Cleric casting heal: Priority = 9.0 (interrupt to prevent heal - kill window)
    /// - Enemy Sorcerer casting mezz: Priority = 9.0 (interrupt to prevent group disable)
    /// - Enemy Wizard casting nuke: Priority = 9.0 (interrupt to prevent damage spike)
    /// - No enemy casting: Priority = 0.0 (no interrupt needed)
    /// - Enemy casting but out of range: Priority = 0.0 (cannot interrupt yet - close gap)
    /// - Enemy finished cast: Priority = 0.0 (too late to interrupt)
    ///
    /// Coordination with Other Goals:
    /// - Second highest priority after GuardHealerGoal (10.0) - only protecting healer is more critical
    /// - Higher than EmergencyHealingGoal (8.0) - prevent damage is better than heal damage
    /// - Much higher than MezzPriorityGoal (7.0) - interrupt active threat before mezzing adds
    /// - Far higher than DPS goals (2.0-4.0) - shutting down casters > dealing damage
    ///
    /// Interrupt Strategy:
    /// - Instant interrupts (amnesia, shouts): Used when enemy starts cast
    /// - Damage interrupts (melee, bolt spells): Used when instant interrupts on cooldown
    /// - Repeated interrupts: Force enemy to use quickcast or flee (interrupt meta)
    /// - Target prioritization: Healers > CC casters > Nukers (prevent healing first)
    ///
    /// Result: PacHealers/Support automatically shut down enemy casters, preventing crucial spells
    /// </remarks>
    public class InterruptEnemyCasterGoal : MimicGoal
    {
        /// <summary>
        /// Critical priority for interrupting enemy casters (game-winning mechanic)
        /// Value of 9.0 places interrupts as second-highest priority after GuardHealerGoal (10.0)
        /// </summary>
        private const float CRITICAL_PRIORITY = 9.0f;

        /// <summary>
        /// Goal state key for enemy caster being interrupted
        /// </summary>
        private const string ENEMY_INTERRUPTED = "enemyInterrupted";

        /// <summary>
        /// Constructs a new InterruptEnemyCasterGoal for universal interrupt behavior
        /// </summary>
        /// <param name="body">The MimicNPC body for game state access (via sensors)</param>
        /// <param name="brain">The MimicBrain for AI state access (via sensors)</param>
        public InterruptEnemyCasterGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        /// <summary>
        /// Calculates dynamic priority based on enemy casting status and interrupt capability
        /// Critical priority (9.0) when enemy is casting and in interrupt range
        /// </summary>
        /// <param name="currentState">Current world state populated by sensors reading Body/Brain</param>
        /// <returns>Priority value (higher = more urgent; 0.0 = not applicable)</returns>
        /// <remarks>
        /// Priority Calculation Logic:
        /// 1. Check if enemy is actively casting (ENEMY_CASTING == true)
        /// 2. Check if enemy is in interrupt range (SHOULD_INTERRUPT == true)
        /// 3. Check if mimic has valid target (HAS_TARGET == true)
        /// 4. If all conditions met: return 9.0 (critical priority interrupt)
        /// 5. Else: return 0.0 (goal not applicable)
        ///
        /// Example Scenarios:
        /// - Enemy Cleric casting Greater Heal on tank: Priority = 9.0 (interrupt NOW - prevent tank surviving)
        /// - Enemy Sorcerer starting mezz cast on healer: Priority = 9.0 (interrupt NOW - prevent healer disable)
        /// - Enemy Wizard channeling nuke on ally: Priority = 9.0 (interrupt NOW - prevent damage spike)
        /// - No enemies casting: Priority = 0.0 (no interrupt target)
        /// - Enemy casting but 2000 units away: Priority = 0.0 (out of interrupt range - close distance first)
        /// - Enemy cast completed: Priority = 0.0 (too late - spell already fired)
        /// - Multiple enemies casting: Priority = 9.0 (EnemyCastingSensor selects highest priority target)
        ///
        /// Coordination Context:
        /// - 9.0 priority competes with:
        ///   - GuardHealerGoal (10.0) - only protecting healer is more important than interrupts
        ///   - EmergencyHealingGoal (8.0) - interrupt slightly higher (prevent damage > heal damage)
        ///   - BreakEnemyMezzGoal (8.0) - interrupt slightly higher (prevent new mezz > cure old mezz)
        ///   - MezzPriorityGoal (7.0) - interrupt much higher (shut down active caster > mezz inactive add)
        ///   - All DPS goals (2.0-4.0) - interrupt far more important than damage
        ///
        /// DAoC Interrupt Meta (Critical for RvR 8v8):
        /// - Interrupting enemy heal = kill window (tank dies instead of being saved)
        /// - Interrupting enemy mezz = keep healer active (group survives)
        /// - Interrupting enemy nuke = damage prevention (better than healing)
        /// - Repeated interrupts = force enemy to waste quickcast (limited uses)
        /// - Interrupt timing = requires fast reaction (< 2 second cast windows)
        /// - Priority 9.0 ensures ALL classes drop current actions to interrupt when enemy casting
        ///
        /// Interrupt Capability (ALL classes can interrupt):
        /// - Instant damage spells (bolts, instant nukes): 1500+ range, instant cast
        /// - Instant abilities (shouts, amnesia): 1500+ range, instant cast
        /// - Melee attacks: 200 range, requires melee contact, fast attack speed
        /// - Action selection via cost calculation: cheapest interrupt method chosen automatically
        ///
        /// Result: ALL classes instinctively interrupt enemy casters when detected, shutting down crucial spells
        /// </remarks>
        public override float GetPriority(ReGoapState<string, object> currentState)
        {
            // Check if enemy is currently casting a spell
            bool enemyCasting = GetBool(currentState, MimicWorldStateKeys.ENEMY_CASTING, false);
            if (!enemyCasting)
                return 0.0f; // No enemy casting = no interrupt needed

            // Check if enemy is in interrupt range and we should interrupt
            bool shouldInterrupt = GetBool(currentState, MimicWorldStateKeys.SHOULD_INTERRUPT, false);
            if (!shouldInterrupt)
                return 0.0f; // Cannot interrupt (out of range or not valid target)

            // Check if we have a valid target
            bool hasTarget = HasTarget(currentState);
            if (!hasTarget)
                return 0.0f; // No target to interrupt

            // All conditions met: enemy casting, in range, have target
            // This is a critical priority situation - drop everything to interrupt
            return CRITICAL_PRIORITY;
        }

        /// <summary>
        /// Defines the desired world state when interrupt goal is satisfied
        /// Goal: Enemy caster is interrupted and not casting
        /// </summary>
        /// <returns>Goal state with "enemyInterrupted" = true</returns>
        /// <remarks>
        /// The planner will search for action sequences that set "enemyInterrupted" to true.
        /// Interrupt actions (CastSpellAction for amnesia/shouts, MeleeAttackAction for melee interrupts,
        /// UseAbilityAction for interrupt abilities) will set "enemyInterrupted" effect when executed.
        ///
        /// Goal State Satisfaction:
        /// - Interrupt spell/ability action sets "enemyInterrupted" = true
        /// - Damage-based interrupt (melee/bolt) sets "enemyInterrupted" = true when hits
        /// - Planner recognizes interrupt actions satisfy goal state
        /// - Cost calculation determines which interrupt method to use
        ///
        /// Interrupt Action Priority (via cost calculation):
        /// - Instant interrupts (amnesia): Lowest cost (0.1x) when enemy casting
        /// - Bolt spells: Low cost when instant interrupts on cooldown
        /// - Melee attacks: Low cost if already in melee range
        /// - High cost (10x) when enemy NOT casting (don't waste interrupt cooldowns)
        ///
        /// Goal satisfaction checked by IsGoalSatisfied() against ENEMY_CASTING == false.
        /// Once enemy cast is interrupted, EnemyCastingSensor updates ENEMY_CASTING to false.
        /// </remarks>
        public override ReGoapState<string, object> GetGoalState()
        {
            var goalState = new ReGoapState<string, object>();

            // Desired end state: enemy caster is interrupted and stopped
            goalState.Set(ENEMY_INTERRUPTED, true);

            return goalState;
        }

        /// <summary>
        /// Checks if interrupt goal is currently satisfied
        /// Satisfied when no enemy is actively casting
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if no enemy casting, false otherwise</returns>
        /// <remarks>
        /// Override default satisfaction check to use ENEMY_CASTING directly.
        /// More efficient than checking goal state match (avoids unnecessary world state comparisons).
        ///
        /// ENEMY_CASTING populated by EnemyCastingSensor reading target.IsCasting,
        /// which is updated by existing casting system (no duplication).
        ///
        /// Satisfaction Logic:
        /// - ENEMY_CASTING == false: No enemy casting → goal satisfied
        /// - ENEMY_CASTING == true: Enemy casting → goal not satisfied (interrupt needed)
        ///
        /// Edge Cases:
        /// - Enemy cast interrupted: ENEMY_CASTING becomes false → goal satisfied
        /// - Enemy cast completed: ENEMY_CASTING becomes false → goal satisfied (too late, but goal done)
        /// - Enemy starts new cast: ENEMY_CASTING becomes true → goal re-activates with priority 9.0
        /// - Enemy switches to quickcast: Cast completes instantly → goal satisfied (enemy used cooldown)
        /// - No enemy target: ENEMY_CASTING == false → goal satisfied (no one to interrupt)
        ///
        /// Goal Re-Evaluation:
        /// - EnemyCastingSensor updates ENEMY_CASTING every think tick (500ms)
        /// - If enemy starts casting: goal re-activates with priority 9.0
        /// - If cast interrupted: goal satisfaction triggers, planner requests new plan
        /// - If new enemy starts casting: goal re-activates for new target
        ///
        /// Interrupt Success Detection:
        /// - Mimic casts interrupt → enemy.IsCasting checked next tick
        /// - If enemy.IsCasting == false: interrupt succeeded
        /// - If enemy.IsCasting == true: interrupt failed (resist, immune, out of range)
        /// - Failed interrupts trigger replanning with updated world state
        /// </remarks>
        public override bool IsGoalSatisfied(ReGoapState<string, object> currentState)
        {
            bool enemyCasting = GetBool(currentState, MimicWorldStateKeys.ENEMY_CASTING, false);
            return !enemyCasting; // No enemy casting = goal satisfied
        }

        /// <summary>
        /// Gets the name of this goal for debugging and logging
        /// </summary>
        /// <returns>Goal name string</returns>
        public override string GetName()
        {
            return "InterruptEnemyCasterGoal";
        }

        /// <summary>
        /// Gets debug information including current priority, satisfaction, and interrupt status
        /// Used by /mimic debug command and logging
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>Debug string with priority, satisfaction, and interrupt details</returns>
        public override string GetDebugInfo(ReGoapState<string, object> currentState)
        {
            float priority = GetPriority(currentState);
            bool satisfied = IsGoalSatisfied(currentState);
            bool enemyCasting = GetBool(currentState, MimicWorldStateKeys.ENEMY_CASTING, false);
            bool shouldInterrupt = GetBool(currentState, MimicWorldStateKeys.SHOULD_INTERRUPT, false);
            bool hasTarget = HasTarget(currentState);
            bool canCast = GetBool(currentState, MimicWorldStateKeys.CAN_CAST, false);

            // Get enemy cast target for debugging
            var enemyCastTarget = GetStateValue<object>(currentState, MimicWorldStateKeys.ENEMY_CAST_TARGET, null);
            string castTargetName = enemyCastTarget?.ToString() ?? "None";

            return $"{GetName()} (Priority: {priority:F2}, Satisfied: {satisfied}, " +
                   $"EnemyCasting: {enemyCasting}, " +
                   $"ShouldInterrupt: {shouldInterrupt}, HasTarget: {hasTarget}, " +
                   $"CanCast: {canCast}, EnemyCastTarget: {castTargetName})";
        }
    }
}
