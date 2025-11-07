using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// Kiting goal for caster DPS when interrupted or being meleed
    /// Activates when caster is vulnerable to melee pressure or has been repeatedly interrupted
    /// Creates distance to enable safe casting and reduces interrupt frequency
    /// </summary>
    /// <remarks>
    /// Design Principles (from design.md Component 4: Role-Based Goals):
    /// - CasterDPS flees when interrupted or under melee pressure
    /// - Creates distance to re-establish safe casting position
    /// - Binary activation: either high priority (8.0) when threatened or inactive (0.0)
    /// - Prevents caster from standing in melee range and eating repeated interrupts
    ///
    /// Priority System (from requirements.md 11 and daoc-role-analysis.md):
    /// - Base Priority: 8.0 (emergency priority when triggered)
    /// - Triggers when:
    ///   1. wasJustInterrupted = true (caster interrupted while casting)
    ///   2. beingMeleed = true (enemy in melee range AND attacking caster)
    /// - Else: return 0.0 (goal not applicable)
    ///
    /// DAoC Context (from daoc-role-analysis.md):
    /// - Casters are extremely vulnerable in melee range (paper armor, no block/parry/evade)
    /// - Interrupts reset cast timers and waste mana (major DPS loss)
    /// - Repeated interrupts can lock casters out of combat entirely
    /// - Kiting is essential survival skill for RvR casters
    /// - List casters (Theurgist, Cabalist, Spiritmaster, Bonedancer) rely on kiting
    /// - After kiting to safety, QuickcastRecoveryGoal helps get spells off
    ///
    /// Organic Behavior Patterns (from requirements.md 11.29):
    /// - When kite goal activates, caster immediately flees from nearest enemy
    /// - MoveAction creates distance (500+ units) to get out of melee/interrupt range
    /// - Once safe, caster can resume casting (KiteGoal deactivates, RangedDamageGoal takes over)
    /// - If repeatedly interrupted, QuickcastRecoveryGoal (priority 7.0) follows KiteGoal
    ///
    /// World State Dependencies (from MimicWorldStateKeys):
    /// - WAS_JUST_INTERRUPTED: Recent interrupt detected by InterruptSensor
    /// - TARGET_IN_MELEE_RANGE: Enemy within 200 units (CombatStatusSensor)
    /// - IS_ATTACKING: Currently being attacked (CombatStatusSensor)
    /// - IN_COMBAT: Combat state (only kite during active combat)
    ///
    /// Goal State: { "atSafeDistance": true }
    /// Satisfied when: Caster is 500+ units from nearest enemy (out of interrupt/melee range)
    ///
    /// Example Scenarios:
    /// - Theurgist casting bolt spell, enemy assassin interrupts: KiteGoal activates (8.0), flee to 600 units
    /// - Cabalist being meleed by warrior in melee range: KiteGoal activates (8.0), create distance immediately
    /// - After reaching safe distance: KiteGoal deactivates (0.0), RangedDamageGoal resumes (3.0)
    /// - If interrupted 2+ times: QuickcastRecoveryGoal (7.0) activates after kiting succeeds
    /// </remarks>
    public class KiteGoal : MimicGoal
    {
        /// <summary>
        /// Emergency priority when kiting is required
        /// 8.0 ensures this goal overrides normal DPS goals (3.0) and buff maintenance (0.5)
        /// But lower than EmergencyHealGoal (100.0) and InterruptEnemyCasterGoal (9.0)
        /// </summary>
        private const float KITE_PRIORITY = 8.0f;

        /// <summary>
        /// Safe distance in units (500+ units = out of melee and most interrupt range)
        /// Melee range: 200 units, Shout interrupts: 350 units, Amnesia: 1500 units
        /// 500 units provides safety from melee and most interrupts while maintaining spell range
        /// </summary>
        private const float SAFE_DISTANCE = 500.0f;

        /// <summary>
        /// Goal state key for kiting completion
        /// </summary>
        private const string AT_SAFE_DISTANCE = "atSafeDistance";

        /// <summary>
        /// Constructs a new KiteGoal for a caster DPS mimic
        /// </summary>
        /// <param name="body">The MimicNPC body for game state access (via sensors)</param>
        /// <param name="brain">The MimicBrain for AI state access (via sensors)</param>
        public KiteGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        /// <summary>
        /// Calculates priority based on interrupt/melee threat status
        /// Binary decision: emergency priority if threatened, zero otherwise
        /// </summary>
        /// <param name="currentState">Current world state populated by sensors reading Body/Brain</param>
        /// <returns>8.0 if threatened (interrupted or being meleed), 0.0 otherwise</returns>
        /// <remarks>
        /// Priority Logic:
        /// 1. Check WAS_JUST_INTERRUPTED (InterruptSensor detected recent interrupt)
        /// 2. Check if being meleed (TARGET_IN_MELEE_RANGE AND IS_ATTACKING)
        /// 3. If either condition true: Return 8.0 (emergency kiting priority)
        /// 4. If neither condition true: Return 0.0 (goal not applicable, continue normal DPS)
        ///
        /// Why Emergency Priority (8.0)?
        /// - Standing in melee range = continued interrupts (DPS loss)
        /// - Multiple interrupts = caster locked out of combat (ineffective)
        /// - Kiting must override normal DPS rotation to re-establish safe position
        /// - Lower than healer emergencies (100.0) and interrupt meta (9.0) but higher than standard DPS (3.0)
        ///
        /// Example Scenarios:
        /// - Theurgist casting, assassin interrupts: wasJustInterrupted=true → Kite (8.0) beats RangedDamage (3.0)
        /// - Cabalist being meleed by warrior: targetInMeleeRange=true, isAttacking=true → Kite (8.0) activates
        /// - After kiting 600 units away: wasJustInterrupted=false, not being meleed → Kite (0.0), RangedDamage (3.0) resumes
        ///
        /// Result: Caster flees to safety, then resumes casting from safe distance
        /// </remarks>
        public override float GetPriority(ReGoapState<string, object> currentState)
        {
            // Only kite during combat (don't flee out of combat)
            bool inCombat = IsInCombat(currentState);
            if (!inCombat)
                return 0.0f;

            // Read world state values populated by sensors
            bool wasJustInterrupted = GetBool(currentState, MimicWorldStateKeys.WAS_JUST_INTERRUPTED, false);
            bool targetInMeleeRange = GetBool(currentState, MimicWorldStateKeys.TARGET_IN_MELEE_RANGE, false);
            bool isAttacking = GetBool(currentState, MimicWorldStateKeys.IS_ATTACKING, false);

            // Being meleed = enemy in melee range AND actively attacking
            bool beingMeleed = targetInMeleeRange && isAttacking;

            // If interrupted or being meleed, activate emergency kiting
            if (wasJustInterrupted || beingMeleed)
            {
                return KITE_PRIORITY; // 8.0 - emergency priority
            }

            // No threat - goal not applicable
            // RangedDamageGoal (3.0) will handle normal DPS
            return 0.0f;
        }

        /// <summary>
        /// Defines the desired world state when kite goal is satisfied
        /// Goal: Caster is at safe distance from enemies (500+ units)
        /// </summary>
        /// <returns>Goal state with "atSafeDistance" = true</returns>
        /// <remarks>
        /// The planner will search for action sequences that set "atSafeDistance" to true.
        /// MoveAction (flee from enemy) sets "atSafeDistance" effect when distance > 500 units.
        ///
        /// Goal satisfaction checked by IsGoalSatisfied() against TARGET_DISTANCE >= SAFE_DISTANCE.
        ///
        /// Note: This goal focuses on creating immediate distance, not killing the enemy.
        /// Once safe distance achieved, RangedDamageGoal takes over for damage dealing.
        /// </remarks>
        public override ReGoapState<string, object> GetGoalState()
        {
            var goalState = new ReGoapState<string, object>();

            // Desired end state: caster at safe distance from enemies
            goalState.Set(AT_SAFE_DISTANCE, true);

            return goalState;
        }

        /// <summary>
        /// Checks if kite goal is currently satisfied
        /// Satisfied when caster is at safe distance (500+ units) from nearest enemy
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if at safe distance, false if still in danger zone</returns>
        /// <remarks>
        /// Override default satisfaction check to use TARGET_DISTANCE directly.
        /// More efficient than checking goal state match.
        ///
        /// TARGET_DISTANCE populated by TargetSensor reading Body.GetDistanceTo(target),
        /// which uses existing game distance calculation (no duplication).
        ///
        /// Satisfaction Logic:
        /// - TARGET_DISTANCE >= 500: Safe distance achieved → goal satisfied
        /// - TARGET_DISTANCE < 500: Still in danger zone → goal not satisfied, continue kiting
        /// - No target: Assume safe → goal satisfied
        /// </remarks>
        public override bool IsGoalSatisfied(ReGoapState<string, object> currentState)
        {
            bool hasTarget = HasTarget(currentState);
            if (!hasTarget)
                return true; // No target = safe (nothing to kite from)

            float targetDistance = GetFloat(currentState, MimicWorldStateKeys.TARGET_DISTANCE, 0.0f);
            return targetDistance >= SAFE_DISTANCE; // Safe distance = goal satisfied
        }

        /// <summary>
        /// Gets the name of this goal for debugging and logging
        /// </summary>
        /// <returns>Goal name string</returns>
        public override string GetName()
        {
            return "KiteGoal";
        }

        /// <summary>
        /// Gets debug information including current priority and threat status
        /// Used by /mimic debug command and logging
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>Debug string with priority, satisfaction, and threat indicators</returns>
        public override string GetDebugInfo(ReGoapState<string, object> currentState)
        {
            float priority = GetPriority(currentState);
            bool satisfied = IsGoalSatisfied(currentState);
            bool wasInterrupted = GetBool(currentState, MimicWorldStateKeys.WAS_JUST_INTERRUPTED, false);
            bool targetInMelee = GetBool(currentState, MimicWorldStateKeys.TARGET_IN_MELEE_RANGE, false);
            bool isAttacking = GetBool(currentState, MimicWorldStateKeys.IS_ATTACKING, false);
            float targetDistance = GetFloat(currentState, MimicWorldStateKeys.TARGET_DISTANCE, 0.0f);
            bool inCombat = IsInCombat(currentState);

            return $"{GetName()} (Priority: {priority:F2}, Satisfied: {satisfied}, " +
                   $"Interrupted: {wasInterrupted}, InMelee: {targetInMelee}, BeingAttacked: {isAttacking}, " +
                   $"Distance: {targetDistance:F0}, InCombat: {inCombat})";
        }
    }
}
