using System.Collections.Generic;

namespace DOL.GS.ReGoap.Core
{
    /// <summary>
    /// Interface for GOAP agents
    /// Agents own goals, actions, sensors, and memory
    /// </summary>
    public interface IReGoapAgent<TKey, TValue>
    {
        /// <summary>
        /// Gets the agent's memory (world state)
        /// </summary>
        ReGoapMemory<TKey, TValue> GetMemory();

        /// <summary>
        /// Gets all goals for this agent
        /// </summary>
        List<IReGoapGoal<TKey, TValue>> GetGoals();

        /// <summary>
        /// Gets all available actions for this agent
        /// </summary>
        List<IReGoapAction<TKey, TValue>> GetActions();

        /// <summary>
        /// Gets all sensors for this agent
        /// </summary>
        List<IReGoapSensor<TKey, TValue>> GetSensors();

        /// <summary>
        /// Sets a new plan for the agent to execute
        /// </summary>
        void SetPlan(Queue<IReGoapAction<TKey, TValue>> plan);

        /// <summary>
        /// Gets the currently executing action
        /// </summary>
        IReGoapAction<TKey, TValue> GetCurrentAction();

        /// <summary>
        /// Gets the current goal being pursued
        /// </summary>
        IReGoapGoal<TKey, TValue> GetCurrentGoal();

        /// <summary>
        /// Checks if the agent has a valid plan
        /// </summary>
        bool HasPlan();

        /// <summary>
        /// Checks if the agent is currently executing an action
        /// </summary>
        bool IsActionRunning();

        /// <summary>
        /// Clears the current plan
        /// </summary>
        void ClearPlan();
    }
}
