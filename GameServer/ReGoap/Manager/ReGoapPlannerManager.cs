using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using DOL.GS.ReGoap.Core;

namespace DOL.GS.ReGoap.Manager
{
    /// <summary>
    /// Centralized planner manager for all ReGoap agents
    /// Handles plan requests and provides planning services
    /// Thread-safe for concurrent access from multiple agents
    /// </summary>
    public class ReGoapPlannerManager
    {
        private static ReGoapPlannerManager _instance;
        private static readonly object _lock = new object();

        private readonly ConcurrentQueue<PlanRequest> _planQueue;
        private readonly Dictionary<object, Queue<IReGoapAction<string, object>>> _activePlans;
        private readonly ReGoapPlanner<string, object> _planner;

        private ReGoapPlannerManager()
        {
            _planQueue = new ConcurrentQueue<PlanRequest>();
            _activePlans = new Dictionary<object, Queue<IReGoapAction<string, object>>>();
            _planner = new ReGoapPlanner<string, object>();
        }

        /// <summary>
        /// Gets the singleton instance of the planner manager
        /// </summary>
        public static ReGoapPlannerManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ReGoapPlannerManager();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Requests a plan for the given agent and goal
        /// Plan will be generated asynchronously
        /// </summary>
        public void RequestPlan(
            IReGoapAgent<string, object> agent,
            IReGoapGoal<string, object> goal,
            Action<Queue<IReGoapAction<string, object>>> callback = null)
        {
            var request = new PlanRequest
            {
                Agent = agent,
                Goal = goal,
                Callback = callback,
                RequestTime = DateTime.UtcNow
            };

            _planQueue.Enqueue(request);
        }

        /// <summary>
        /// Processes pending plan requests
        /// Should be called periodically (e.g., once per server tick)
        /// </summary>
        /// <param name="maxProcessingTimeMs">Maximum time to spend processing plans</param>
        /// <returns>Number of plans processed</returns>
        public int ProcessPlanRequests(int maxProcessingTimeMs = 100)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int plansProcessed = 0;

            while (_planQueue.TryDequeue(out var request) && stopwatch.ElapsedMilliseconds < maxProcessingTimeMs)
            {
                try
                {
                    var plan = GeneratePlan(request.Agent, request.Goal);

                    if (plan != null && plan.Count > 0)
                    {
                        request.Agent.SetPlan(plan);
                        _activePlans[request.Agent] = plan;
                        request.Callback?.Invoke(plan);
                    }
                    else
                    {
                        // No valid plan found
                        LogPlanFailure(request);
                        request.Callback?.Invoke(null);
                    }

                    plansProcessed++;
                }
                catch (Exception ex)
                {
                    LogPlanError(request, ex);
                    request.Callback?.Invoke(null);
                    plansProcessed++;
                }
            }

            return plansProcessed;
        }

        /// <summary>
        /// Generates a plan for the agent to achieve the goal
        /// Uses A* pathfinding to find optimal action sequence
        /// </summary>
        private Queue<IReGoapAction<string, object>> GeneratePlan(
            IReGoapAgent<string, object> agent,
            IReGoapGoal<string, object> goal)
        {
            var currentState = agent.GetMemory().GetWorldState();
            var goalState = goal.GetGoalState();

            return _planner.Plan(agent, currentState, goalState, null);
        }

        /// <summary>
        /// Gets the active plan for an agent
        /// </summary>
        public Queue<IReGoapAction<string, object>> GetActivePlan(IReGoapAgent<string, object> agent)
        {
            return _activePlans.TryGetValue(agent, out var plan) ? plan : null;
        }

        /// <summary>
        /// Clears the active plan for an agent
        /// </summary>
        public void ClearActivePlan(IReGoapAgent<string, object> agent)
        {
            _activePlans.Remove(agent);
        }

        /// <summary>
        /// Gets the number of pending plan requests
        /// </summary>
        public int GetPendingRequestCount()
        {
            return _planQueue.Count;
        }

        private void LogPlanFailure(PlanRequest request)
        {
            // Log plan failure details
            var goalName = request.Goal?.GetName() ?? "Unknown";
            var worldState = request.Agent?.GetMemory()?.GetWorldState();
            var actions = request.Agent?.GetActions();

            Console.WriteLine($"[ReGoap] Plan failed for goal '{goalName}'. " +
                            $"WorldState: {worldState}, " +
                            $"AvailableActions: {actions?.Count ?? 0}");
        }

        private void LogPlanError(PlanRequest request, Exception ex)
        {
            var goalName = request.Goal?.GetName() ?? "Unknown";
            Console.WriteLine($"[ReGoap] Error planning for goal '{goalName}': {ex.Message}");
        }

        /// <summary>
        /// Internal class representing a plan request
        /// </summary>
        private class PlanRequest
        {
            public IReGoapAgent<string, object> Agent { get; set; }
            public IReGoapGoal<string, object> Goal { get; set; }
            public Action<Queue<IReGoapAction<string, object>>> Callback { get; set; }
            public DateTime RequestTime { get; set; }
        }
    }
}
