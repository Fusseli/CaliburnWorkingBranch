using System;

namespace DOL.GS.ReGoap.Core
{
    /// <summary>
    /// Interface for GOAP actions that can be planned and executed
    /// Actions represent state transitions with preconditions and effects
    /// </summary>
    public interface IReGoapAction<TKey, TValue>
    {
        /// <summary>
        /// Gets the name of this action for debugging
        /// </summary>
        string GetName();

        /// <summary>
        /// Gets the preconditions required for this action to be valid
        /// These must be satisfied in the current world state
        /// </summary>
        ReGoapState<TKey, TValue> GetPreconditions(IReGoapAgent<TKey, TValue> agent);

        /// <summary>
        /// Gets the effects this action will have on the world state
        /// These represent the predicted outcome of executing the action
        /// </summary>
        ReGoapState<TKey, TValue> GetEffects(IReGoapAgent<TKey, TValue> agent);

        /// <summary>
        /// Gets the cost of executing this action
        /// Lower costs are preferred by the planner
        /// Cost can be dynamic based on current world state
        /// </summary>
        float GetCost(IReGoapAgent<TKey, TValue> agent, ReGoapState<TKey, TValue> currentState);

        /// <summary>
        /// Checks if the action's preconditions are currently satisfied
        /// Called before execution to validate the action is still valid
        /// </summary>
        bool CheckPreconditions(IReGoapAgent<TKey, TValue> agent, ReGoapState<TKey, TValue> currentState);

        /// <summary>
        /// Executes the action
        /// Returns true if the action completed (success or failure)
        /// Returns false if the action is still running (will be called again next tick)
        /// </summary>
        /// <param name="agent">The agent executing this action</param>
        /// <param name="doneCallback">Callback to invoke when action succeeds</param>
        /// <param name="failCallback">Callback to invoke when action fails</param>
        bool Run(IReGoapAgent<TKey, TValue> agent,
                 Action<IReGoapAction<TKey, TValue>> doneCallback,
                 Action<IReGoapAction<TKey, TValue>> failCallback);

        /// <summary>
        /// Checks if this action is interruptible
        /// Non-interruptible actions cannot be cancelled mid-execution
        /// </summary>
        bool IsInterruptible();
    }
}
