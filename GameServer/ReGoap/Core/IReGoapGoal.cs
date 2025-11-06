namespace DOL.GS.ReGoap.Core
{
    /// <summary>
    /// Interface for GOAP goals
    /// Goals represent desired world states with dynamic priorities
    /// </summary>
    public interface IReGoapGoal<TKey, TValue>
    {
        /// <summary>
        /// Gets the name of this goal for debugging
        /// </summary>
        string GetName();

        /// <summary>
        /// Gets the desired world state that satisfies this goal
        /// The planner will find actions to achieve this state
        /// </summary>
        ReGoapState<TKey, TValue> GetGoalState();

        /// <summary>
        /// Gets the priority of this goal based on current world state
        /// Higher priority goals are planned for first
        /// Priority can be dynamic based on game state
        /// </summary>
        float GetPriority(ReGoapState<TKey, TValue> currentState);

        /// <summary>
        /// Checks if the goal is currently satisfied
        /// If true, the goal is complete and no planning is needed
        /// </summary>
        bool IsGoalSatisfied(ReGoapState<TKey, TValue> currentState);
    }
}
