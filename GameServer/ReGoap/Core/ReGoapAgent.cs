using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS.ReGoap.Core
{
    /// <summary>
    /// Base GOAP agent implementation
    /// Manages goals, actions, sensors, memory, and plan execution
    /// </summary>
    public class ReGoapAgent<TKey, TValue> : IReGoapAgent<TKey, TValue>
    {
        protected ReGoapMemory<TKey, TValue> memory;
        protected List<IReGoapGoal<TKey, TValue>> goals;
        protected List<IReGoapAction<TKey, TValue>> actions;
        protected List<IReGoapSensor<TKey, TValue>> sensors;

        protected Queue<IReGoapAction<TKey, TValue>> currentPlan;
        protected IReGoapAction<TKey, TValue> currentAction;
        protected IReGoapGoal<TKey, TValue> currentGoal;
        protected bool actionRunning;

        public ReGoapAgent()
        {
            memory = new ReGoapMemory<TKey, TValue>();
            goals = new List<IReGoapGoal<TKey, TValue>>();
            actions = new List<IReGoapAction<TKey, TValue>>();
            sensors = new List<IReGoapSensor<TKey, TValue>>();
            currentPlan = new Queue<IReGoapAction<TKey, TValue>>();
        }

        public virtual void Initialize()
        {
            // Initialize all sensors
            foreach (var sensor in sensors)
            {
                sensor.Init(this);
            }
        }

        public ReGoapMemory<TKey, TValue> GetMemory()
        {
            return memory;
        }

        public List<IReGoapGoal<TKey, TValue>> GetGoals()
        {
            return goals;
        }

        public List<IReGoapAction<TKey, TValue>> GetActions()
        {
            return actions;
        }

        public List<IReGoapSensor<TKey, TValue>> GetSensors()
        {
            return sensors;
        }

        public void SetPlan(Queue<IReGoapAction<TKey, TValue>> plan)
        {
            currentPlan = plan ?? new Queue<IReGoapAction<TKey, TValue>>();
            currentAction = null;
            actionRunning = false;
        }

        /// <summary>
        /// Sets the current goal being pursued
        /// This should be called when a new plan is generated for a goal
        /// </summary>
        public void SetCurrentGoal(IReGoapGoal<TKey, TValue> goal)
        {
            currentGoal = goal;
        }

        public IReGoapAction<TKey, TValue> GetCurrentAction()
        {
            return currentAction;
        }

        public IReGoapGoal<TKey, TValue> GetCurrentGoal()
        {
            return currentGoal;
        }

        public bool HasPlan()
        {
            return currentPlan != null && currentPlan.Count > 0;
        }

        public bool IsActionRunning()
        {
            return actionRunning;
        }

        public void ClearPlan()
        {
            currentPlan?.Clear();
            currentAction = null;
            currentGoal = null;
            actionRunning = false;
        }

        public void AddGoal(IReGoapGoal<TKey, TValue> goal)
        {
            if (!goals.Contains(goal))
                goals.Add(goal);
        }

        public void RemoveGoal(IReGoapGoal<TKey, TValue> goal)
        {
            goals.Remove(goal);
        }

        public void AddAction(IReGoapAction<TKey, TValue> action)
        {
            if (!actions.Contains(action))
                actions.Add(action);
        }

        public void RemoveAction(IReGoapAction<TKey, TValue> action)
        {
            actions.Remove(action);
        }

        public void AddSensor(IReGoapSensor<TKey, TValue> sensor)
        {
            if (!sensors.Contains(sensor))
            {
                sensors.Add(sensor);
                sensor.Init(this);
            }
        }

        public void RemoveSensor(IReGoapSensor<TKey, TValue> sensor)
        {
            sensors.Remove(sensor);
        }

        /// <summary>
        /// Updates all sensors with current game state
        /// Should be called every think tick before execution
        /// </summary>
        public virtual void UpdateSensors()
        {
            foreach (var sensor in sensors)
            {
                sensor.UpdateSensor();
            }
        }

        /// <summary>
        /// Gets the highest priority goal that isn't already satisfied
        /// </summary>
        public virtual IReGoapGoal<TKey, TValue> GetHighestPriorityGoal()
        {
            var worldState = memory.GetWorldState();

            IReGoapGoal<TKey, TValue> bestGoal = null;
            float highestPriority = float.MinValue;

            foreach (var goal in goals)
            {
                if (goal.IsGoalSatisfied(worldState))
                    continue;

                float priority = goal.GetPriority(worldState);
                if (priority > highestPriority)
                {
                    highestPriority = priority;
                    bestGoal = goal;
                }
            }

            return bestGoal;
        }

        /// <summary>
        /// Executes the current action in the plan
        /// Advances to next action when current completes
        /// </summary>
        public virtual void ExecuteCurrentAction()
        {
            // If no action is running, try to start the next one
            if (!actionRunning)
            {
                if (currentPlan == null || currentPlan.Count == 0)
                    return;

                currentAction = currentPlan.Dequeue();

                // Verify preconditions before starting
                var worldState = memory.GetWorldState();
                if (!currentAction.CheckPreconditions(this, worldState))
                {
                    OnActionFailed(currentAction);
                    return;
                }

                actionRunning = true;
            }

            // Run the current action
            if (currentAction != null && actionRunning)
            {
                bool completed = currentAction.Run(this, OnActionComplete, OnActionFailed);

                // If action returned true, it completed this tick
                if (completed)
                {
                    actionRunning = false;
                }
            }
        }

        protected virtual void OnActionComplete(IReGoapAction<TKey, TValue> action)
        {
            actionRunning = false;
            currentAction = null;

            // Check if plan is complete
            if (currentPlan.Count == 0)
            {
                OnPlanComplete();
            }
        }

        protected virtual void OnActionFailed(IReGoapAction<TKey, TValue> action)
        {
            actionRunning = false;
            currentAction = null;
            ClearPlan();
            OnPlanFailed();
        }

        protected virtual void OnPlanComplete()
        {
            ClearPlan();
        }

        protected virtual void OnPlanFailed()
        {
            // Override to handle plan failure (e.g., request replanning)
        }
    }
}
