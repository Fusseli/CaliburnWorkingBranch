using System;
using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;

namespace DOL.GS.ReGoap.Mimic.Actions
{
    /// <summary>
    /// Base class for all MimicNPC ReGoap actions
    /// Provides common functionality for action execution, failure tracking, and cost calculation
    /// Bridges ReGoap action system with MimicNPC game systems (casting, combat, abilities)
    /// </summary>
    public abstract class MimicAction : ReGoapAction<string, object>
    {
        protected readonly MimicNPC _body;
        protected readonly MimicBrain _brain;
        protected int _failureCount = 0;
        protected const int MAX_FAILURES = 3;

        /// <summary>
        /// Gets the MimicNPC body this action operates on
        /// Used to access game systems: CastSpell(), StartAttack(), UseAbility(), etc.
        /// </summary>
        public MimicNPC Body => _body;

        /// <summary>
        /// Gets the MimicBrain this action uses for AI state
        /// Used to access aggro lists, target selection, role information
        /// </summary>
        public MimicBrain Brain => _brain;

        /// <summary>
        /// Gets the current failure count for this action
        /// Used to track repeated failures and increase cost for problematic actions
        /// </summary>
        public int FailureCount => _failureCount;

        /// <summary>
        /// Constructs a new MimicAction with references to body and brain
        /// </summary>
        /// <param name="body">The MimicNPC body this action controls</param>
        /// <param name="brain">The MimicBrain for AI state access</param>
        protected MimicAction(MimicNPC body, MimicBrain brain) : base()
        {
            _body = body ?? throw new ArgumentNullException(nameof(body));
            _brain = brain ?? throw new ArgumentNullException(nameof(brain));
        }

        /// <summary>
        /// Calculates the cost of this action based on world state
        /// Implements failure penalty: actions that fail repeatedly become more expensive
        /// Override CalculateBaseCost() in derived classes to provide role-specific cost logic
        /// </summary>
        /// <param name="agent">The agent executing this action</param>
        /// <param name="currentState">Current world state (populated by sensors reading Body/Brain)</param>
        /// <returns>Action cost (lower = higher priority for planner)</returns>
        public override float GetCost(IReGoapAgent<string, object> agent, ReGoapState<string, object> currentState)
        {
            float baseCost = CalculateBaseCost(currentState);

            // Increase cost if action is repeatedly failing
            // After MAX_FAILURES (3), cost doubles to discourage continued attempts
            if (_failureCount >= MAX_FAILURES)
            {
                baseCost *= 2.0f;
            }

            return baseCost;
        }

        /// <summary>
        /// Calculates the base cost of this action before failure penalties
        /// Must be implemented by derived classes with role-specific cost formulas
        ///
        /// Examples:
        /// - Healer: (castTime / healAmount) * 100 (time efficiency)
        /// - DPS: (manaCost + castTime * 10) / damage (resource efficiency)
        /// - Tank: 100 / (threatGeneration + 1) (inverse of threat)
        /// - CC: castTime + (60 / duration) (favors long-duration CC)
        /// </summary>
        /// <param name="worldState">Current world state from sensors</param>
        /// <returns>Base action cost before failure penalties</returns>
        protected abstract float CalculateBaseCost(ReGoapState<string, object> worldState);

        /// <summary>
        /// Called when action completes successfully
        /// Resets failure counter
        /// </summary>
        protected void OnSuccess()
        {
            _failureCount = 0;
        }

        /// <summary>
        /// Called when action fails (preconditions fail, execution fails, interrupted)
        /// Increments failure counter
        /// After MAX_FAILURES, action cost doubles to discourage repeated attempts
        /// </summary>
        protected void OnFailure()
        {
            _failureCount++;
        }

        /// <summary>
        /// Resets the failure count for this action
        /// Can be called externally to clear failure state (e.g., after target changes, combat resets)
        /// </summary>
        public void ResetFailureCount()
        {
            _failureCount = 0;
        }

        /// <summary>
        /// Checks if preconditions are valid against current world state
        /// Default implementation checks if world state meets all preconditions
        /// Override in derived classes for custom precondition logic
        /// </summary>
        /// <param name="agent">The agent executing this action</param>
        /// <param name="currentState">Current world state (from sensors reading Body/Brain)</param>
        /// <returns>True if preconditions are satisfied, false otherwise</returns>
        public override bool CheckPreconditions(IReGoapAgent<string, object> agent, ReGoapState<string, object> currentState)
        {
            // Default implementation: check if all preconditions are met by current world state
            return currentState.MeetsGoal(preconditions);
        }

        /// <summary>
        /// Executes the action
        /// Must be implemented by derived classes
        /// Should:
        /// 1. Verify preconditions are still valid
        /// 2. Execute action via game systems (Body.CastSpell, Body.StartAttack, etc.)
        /// 3. Monitor execution state (Body.IsCasting, Body.IsAttacking, etc.)
        /// 4. Invoke doneCallback on success or failCallback on failure
        /// 5. Call OnSuccess() or OnFailure() appropriately
        /// </summary>
        /// <param name="agent">The agent executing this action</param>
        /// <param name="doneCallback">Callback to invoke on successful completion</param>
        /// <param name="failCallback">Callback to invoke on failure</param>
        /// <returns>True if action is complete, false if still running</returns>
        public abstract override bool Run(IReGoapAgent<string, object> agent,
                                          Action<IReGoapAction<string, object>> doneCallback,
                                          Action<IReGoapAction<string, object>> failCallback);

        /// <summary>
        /// Gets a debug string representation of this action
        /// Includes failure count for diagnostics
        /// </summary>
        public override string ToString()
        {
            return $"{GetName()} (Failures: {_failureCount})";
        }
    }
}
