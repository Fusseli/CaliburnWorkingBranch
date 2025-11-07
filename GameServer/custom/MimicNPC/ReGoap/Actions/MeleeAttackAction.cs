using System;
using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Actions
{
    /// <summary>
    /// Melee attack action for MimicNPC melee classes
    /// Extends MimicAction with melee-specific cost calculation and attack execution
    /// Leverages existing MimicAttackAction and AttackService for combat mechanics
    /// </summary>
    /// <remarks>
    /// Design: Integrates with existing melee combat system (Body.StartAttack, AttackService)
    /// Cost calculation follows melee formula based on target distance and positional opportunity
    /// No spell casting involved - pure physical combat via existing attack component
    ///
    /// Reference: design.md "Component 5: Class-Specific Actions - MeleeAttackAction"
    /// Requirements: 4.1, 4.3, 7.3
    /// Leverage: Existing MimicAttackAction component, AttackService, Body.StartAttack()
    /// </remarks>
    public class MeleeAttackAction : MimicAction
    {
        private readonly ICostCalculator _costCalculator;

        /// <summary>
        /// Constructs a new MeleeAttackAction
        /// </summary>
        /// <param name="body">The MimicNPC body that will perform melee attacks</param>
        /// <param name="brain">The MimicBrain for AI state access</param>
        /// <param name="costCalculator">Cost calculator for melee DPS (optional, uses MeleeCostCalculator if null)</param>
        public MeleeAttackAction(MimicNPC body, MimicBrain brain, ICostCalculator costCalculator = null)
            : base(body, brain)
        {
            _costCalculator = costCalculator ?? new MeleeCostCalculator();

            // Set action name for debugging
            name = "MeleeAttack";

            // Set preconditions (checked against world state from sensors)
            preconditions.Set(MimicWorldStateKeys.HAS_TARGET, true);
            preconditions.Set(MimicWorldStateKeys.IN_COMBAT, true);
            preconditions.Set(MimicWorldStateKeys.TARGET_IN_MELEE_RANGE, true);

            // Set effects (predicted outcome)
            effects.Set("targetDamaged", true);
            effects.Set("targetHealthReduced", true);
        }

        /// <summary>
        /// Checks if preconditions are satisfied for melee attack
        /// Validates against world state populated by sensors reading Brain/Body
        /// </summary>
        /// <param name="agent">The agent executing this action</param>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if melee attack can be performed, false otherwise</returns>
        public override bool CheckPreconditions(IReGoapAgent<string, object> agent, ReGoapState<string, object> currentState)
        {
            // Check base preconditions (hasTarget, inCombat, targetInMeleeRange)
            if (!base.CheckPreconditions(agent, currentState))
                return false;

            // Check target validity (read from TargetSensor)
            object targetObj = currentState.Get(MimicWorldStateKeys.CURRENT_TARGET);
            GameObject target = targetObj as GameObject;
            if (target == null)
                return false;

            // Check that target is a living entity (can't attack objects)
            GameLiving livingTarget = target as GameLiving;
            if (livingTarget == null || !livingTarget.IsAlive)
                return false;

            // Check target is hostile (don't attack friendlies)
            if (!GameServer.ServerRules.IsAllowedToAttack(_body, livingTarget, true))
                return false;

            // Check melee range (read from TargetSensor which reads Body.IsWithinRadius)
            object inMeleeRangeObj = currentState.Get(MimicWorldStateKeys.TARGET_IN_MELEE_RANGE);
            bool inMeleeRange = inMeleeRangeObj != null && Convert.ToBoolean(inMeleeRangeObj);
            if (!inMeleeRange)
                return false;

            return true;
        }

        /// <summary>
        /// Calculates the base cost of this melee attack action
        /// Delegates to melee-specific cost calculator
        /// Lower cost = higher priority for planner
        /// </summary>
        /// <param name="worldState">Current world state from sensors</param>
        /// <returns>Base action cost before failure penalties</returns>
        protected override float CalculateBaseCost(ReGoapState<string, object> worldState)
        {
            return _costCalculator.Calculate(null, worldState);
        }

        /// <summary>
        /// Executes the melee attack action
        /// Delegates to existing game system via Body.StartAttack() and MimicAttackAction component
        /// Monitors attack state via Body.IsAttacking property
        /// </summary>
        /// <param name="agent">The agent executing this action</param>
        /// <param name="doneCallback">Callback to invoke on successful completion</param>
        /// <param name="failCallback">Callback to invoke on failure</param>
        /// <returns>True if action is complete (success or failure), false if still running</returns>
        public override bool Run(IReGoapAgent<string, object> agent,
                                  Action<IReGoapAction<string, object>> doneCallback,
                                  Action<IReGoapAction<string, object>> failCallback)
        {
            // Read Body.TargetObject directly (populated by Brain.CalculateNextAttackTarget)
            var target = _body.TargetObject as GameLiving;

            // Validate target is still valid
            if (target == null || !target.IsAlive)
            {
                OnFailure();
                failCallback(this);
                return true; // Complete (failed)
            }

            // Check if mimic can attack (not stunned, mezzed, casting)
            if (_body.IsStunned || _body.IsMezzed || _body.IsCasting)
            {
                OnFailure();
                failCallback(this);
                return true; // Complete (failed due to control effects)
            }

            // Use existing melee attack system via Body.StartAttack
            // This delegates to MimicAttackAction component and AttackService
            if (!_body.IsAttacking)
            {
                _body.StartAttack(target);
            }

            // Verify attack state started successfully
            if (_body.attackComponent?.AttackState == true)
            {
                // Melee attacks are continuous - mark as success once initiated
                // The attack component will continue to tick and execute attacks
                OnSuccess();
                doneCallback(this);
                return true; // Complete (successfully initiated)
            }
            else
            {
                // Failed to start attack (out of range, invalid target, etc.)
                OnFailure();
                failCallback(this);
                return true; // Complete (failed)
            }
        }

        /// <summary>
        /// Returns string representation for debugging
        /// Includes target name and attack state
        /// </summary>
        public override string ToString()
        {
            var target = _body.TargetObject;
            string targetName = target?.Name ?? "None";
            bool isAttacking = _body.IsAttacking;
            return $"{GetName()} [Target: {targetName}, Attacking: {isAttacking}, Failures: {_failureCount}]";
        }
    }

    /// <summary>
    /// Cost calculator for melee attack actions
    /// Implements melee-specific cost formula based on distance and positional opportunity
    /// </summary>
    /// <remarks>
    /// Design: Strategy pattern for cost calculation
    /// Melee DPS prioritize target availability and positioning
    ///
    /// Cost Formula (Req 7.3):
    /// - base_cost = 1.0 (default melee action cost)
    /// - Lower cost if target in melee range (readily attackable)
    /// - Higher cost if target distant (requires movement)
    /// - Positional styles get cost adjustment based on position opportunity
    ///
    /// Reference: design.md "Component 7: Cost Calculators - MeleeCostCalculator"
    /// Requirements: 4.3, 7.3
    /// </remarks>
    public class MeleeCostCalculator : ICostCalculator
    {
        /// <summary>
        /// Calculates the cost of a melee attack action
        /// Lower cost = higher priority for planner
        /// </summary>
        /// <param name="action">The action object (not used for basic melee)</param>
        /// <param name="worldState">Current world state from sensors</param>
        /// <returns>Action cost (lower = higher priority)</returns>
        public float Calculate(object action, ReGoapState<string, object> worldState)
        {
            // Base cost for melee attack
            float baseCost = 1.0f;

            // Check if target is in melee range (from TargetSensor)
            object inMeleeRangeObj = worldState.Get(MimicWorldStateKeys.TARGET_IN_MELEE_RANGE);
            bool inMeleeRange = inMeleeRangeObj != null && Convert.ToBoolean(inMeleeRangeObj);

            if (!inMeleeRange)
            {
                // Target not in melee range - higher cost (requires movement)
                baseCost *= 2.0f;
            }

            // Check target distance for fine-tuned cost adjustment
            object distanceObj = worldState.Get(MimicWorldStateKeys.TARGET_DISTANCE);
            if (distanceObj != null)
            {
                int distance = Convert.ToInt32(distanceObj);

                // Very close targets (within 150 units) are optimal
                if (distance <= 150)
                {
                    baseCost *= 0.8f; // -20% cost for optimal range
                }
                // Targets at edge of melee range (150-200) are acceptable
                else if (distance <= 200)
                {
                    baseCost *= 1.0f; // No adjustment
                }
                // Targets outside melee range require closing distance
                else if (distance <= 500)
                {
                    baseCost *= 1.5f; // +50% cost for short sprint needed
                }
                else
                {
                    baseCost *= 3.0f; // +200% cost for long distance
                }
            }

            // Check for positional style opportunity
            // If mimic has positional styles and is correctly positioned, reduce cost
            object canUseSideStylesObj = worldState.Get(MimicWorldStateKeys.CAN_USE_SIDE_STYLES);
            object canUseBackStylesObj = worldState.Get(MimicWorldStateKeys.CAN_USE_BACK_STYLES);
            bool canUseSideStyles = canUseSideStylesObj != null && Convert.ToBoolean(canUseSideStylesObj);
            bool canUseBackStyles = canUseBackStylesObj != null && Convert.ToBoolean(canUseBackStylesObj);

            // TODO: Future enhancement - check actual position relative to target
            // For now, just check if positional styles are available
            if (canUseSideStyles || canUseBackStyles)
            {
                // Has positional styles available - slight cost reduction to encourage their use
                baseCost *= 0.9f; // -10% cost when positional opportunity exists
            }

            // Check if this is the Main Assist's target (DAoC assist train)
            object targetMatchesMainAssistObj = worldState.Get(MimicWorldStateKeys.TARGET_MATCHES_MAIN_ASSIST);
            bool targetMatchesMainAssist = targetMatchesMainAssistObj != null && Convert.ToBoolean(targetMatchesMainAssistObj);

            if (targetMatchesMainAssist)
            {
                // Attacking Main Assist's target - reduce cost (coordinated focus fire)
                baseCost *= 0.7f; // -30% cost for assist train coordination
            }
            else
            {
                // Off-target attack - increase cost to discourage
                baseCost *= 1.3f; // +30% cost penalty for breaking assist train
            }

            return baseCost;
        }
    }
}
