using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS.ReGoap.Core
{
    /// <summary>
    /// GOAP planner using A* pathfinding to generate action sequences
    /// Finds the lowest-cost path from current state to goal state
    /// </summary>
    public class ReGoapPlanner<TKey, TValue>
    {
        private readonly AStar<TKey, TValue> astar;

        public ReGoapPlanner()
        {
            astar = new AStar<TKey, TValue>();
        }

        /// <summary>
        /// Plans a sequence of actions to achieve the goal state
        /// </summary>
        /// <param name="agent">The agent requesting the plan</param>
        /// <param name="currentState">Current world state</param>
        /// <param name="goalState">Desired goal state</param>
        /// <param name="earlyExit">Optional early exit condition</param>
        /// <returns>Queue of actions to execute, or null if no plan found</returns>
        public Queue<IReGoapAction<TKey, TValue>> Plan(
            IReGoapAgent<TKey, TValue> agent,
            ReGoapState<TKey, TValue> currentState,
            ReGoapState<TKey, TValue> goalState,
            Func<IReGoapNode<TKey, TValue>, bool> earlyExit = null)
        {
            var actions = agent.GetActions();
            var result = astar.Run(currentState, goalState, actions, agent, earlyExit);

            if (result == null)
                return null;

            var plan = new Queue<IReGoapAction<TKey, TValue>>();
            var node = result;

            // Build plan by walking back through nodes
            while (node != null && node.Action != null)
            {
                plan.Enqueue(node.Action);
                node = node.Parent;
            }

            // Reverse the plan (we built it backwards)
            var reversedPlan = new Queue<IReGoapAction<TKey, TValue>>(plan.Reverse());
            return reversedPlan;
        }
    }

    /// <summary>
    /// Node used in A* search
    /// Represents a state along with the action taken to reach it
    /// </summary>
    public interface IReGoapNode<TKey, TValue>
    {
        ReGoapState<TKey, TValue> State { get; }
        IReGoapAction<TKey, TValue> Action { get; }
        IReGoapNode<TKey, TValue> Parent { get; }
        float GCost { get; }
        float HCost { get; }
        float FCost { get; }
    }

    public class ReGoapNode<TKey, TValue> : IReGoapNode<TKey, TValue>
    {
        public ReGoapState<TKey, TValue> State { get; private set; }
        public IReGoapAction<TKey, TValue> Action { get; private set; }
        public IReGoapNode<TKey, TValue> Parent { get; private set; }
        public float GCost { get; set; } // Cost from start to this node
        public float HCost { get; set; } // Heuristic cost to goal
        public float FCost => GCost + HCost; // Total estimated cost

        public ReGoapNode(
            ReGoapState<TKey, TValue> state,
            IReGoapAction<TKey, TValue> action,
            IReGoapNode<TKey, TValue> parent,
            float gCost,
            float hCost)
        {
            State = state;
            Action = action;
            Parent = parent;
            GCost = gCost;
            HCost = hCost;
        }
    }

    /// <summary>
    /// A* pathfinding implementation for GOAP
    /// </summary>
    public class AStar<TKey, TValue>
    {
        private readonly List<IReGoapNode<TKey, TValue>> openList;
        private readonly HashSet<ReGoapState<TKey, TValue>> closedSet;

        public AStar()
        {
            openList = new List<IReGoapNode<TKey, TValue>>();
            closedSet = new HashSet<ReGoapState<TKey, TValue>>();
        }

        public IReGoapNode<TKey, TValue> Run(
            ReGoapState<TKey, TValue> startState,
            ReGoapState<TKey, TValue> goalState,
            List<IReGoapAction<TKey, TValue>> actions,
            IReGoapAgent<TKey, TValue> agent,
            Func<IReGoapNode<TKey, TValue>, bool> earlyExit = null,
            int maxIterations = 1000)
        {
            openList.Clear();
            closedSet.Clear();

            var startNode = new ReGoapNode<TKey, TValue>(
                new ReGoapState<TKey, TValue>(startState),
                null,
                null,
                0,
                CalculateHeuristic(startState, goalState)
            );

            openList.Add(startNode);

            int iterations = 0;
            while (openList.Count > 0 && iterations < maxIterations)
            {
                iterations++;

                // Get node with lowest F cost
                var currentNode = GetLowestFCostNode();
                openList.Remove(currentNode);

                // Check if we reached the goal
                if (currentNode.State.MeetsGoal(goalState))
                    return currentNode;

                // Early exit condition
                if (earlyExit != null && earlyExit(currentNode))
                    return currentNode;

                closedSet.Add(currentNode.State);

                // Expand neighbors
                foreach (var action in actions)
                {
                    var preconditions = action.GetPreconditions(agent);

                    // Check if preconditions are met
                    if (!currentNode.State.MeetsGoal(preconditions))
                        continue;

                    // Calculate new state after applying action
                    var newState = new ReGoapState<TKey, TValue>(currentNode.State);
                    var effects = action.GetEffects(agent);

                    // Apply effects to create new state
                    foreach (var key in effects.Keys)
                    {
                        newState.Set(key, effects.Get(key));
                    }

                    // Skip if already evaluated
                    if (closedSet.Contains(newState))
                        continue;

                    float actionCost = action.GetCost(agent, currentNode.State);
                    float newGCost = currentNode.GCost + actionCost;
                    float hCost = CalculateHeuristic(newState, goalState);

                    var neighborNode = new ReGoapNode<TKey, TValue>(
                        newState,
                        action,
                        currentNode,
                        newGCost,
                        hCost
                    );

                    // Check if this path to neighbor is better
                    var existingNode = FindNodeInOpenList(newState);
                    if (existingNode != null)
                    {
                        if (newGCost < existingNode.GCost)
                        {
                            openList.Remove(existingNode);
                            openList.Add(neighborNode);
                        }
                    }
                    else
                    {
                        openList.Add(neighborNode);
                    }
                }
            }

            // No plan found
            return null;
        }

        private IReGoapNode<TKey, TValue> GetLowestFCostNode()
        {
            IReGoapNode<TKey, TValue> lowest = openList[0];
            for (int i = 1; i < openList.Count; i++)
            {
                if (openList[i].FCost < lowest.FCost)
                    lowest = openList[i];
            }
            return lowest;
        }

        private IReGoapNode<TKey, TValue> FindNodeInOpenList(ReGoapState<TKey, TValue> state)
        {
            return openList.FirstOrDefault(n => StateEquals(n.State, state));
        }

        private bool StateEquals(ReGoapState<TKey, TValue> a, ReGoapState<TKey, TValue> b)
        {
            if (a.Count != b.Count)
                return false;

            foreach (var key in a.Keys)
            {
                if (!b.Has(key))
                    return false;

                if (!EqualityComparer<TValue>.Default.Equals(a.Get(key), b.Get(key)))
                    return false;
            }

            return true;
        }

        private float CalculateHeuristic(ReGoapState<TKey, TValue> currentState, ReGoapState<TKey, TValue> goalState)
        {
            // Heuristic: count of unsatisfied goal conditions
            return currentState.MissingDifference(goalState);
        }
    }
}
