using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS.ReGoap.Core
{
    /// <summary>
    /// Represents a world state or goal state as a collection of key-value pairs
    /// Used for preconditions, effects, and goal definitions in GOAP planning
    /// </summary>
    public class ReGoapState<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> _state;

        public ReGoapState()
        {
            _state = new Dictionary<TKey, TValue>();
        }

        public ReGoapState(ReGoapState<TKey, TValue> old)
        {
            _state = new Dictionary<TKey, TValue>(old._state);
        }

        public void Set(TKey key, TValue value)
        {
            _state[key] = value;
        }

        public TValue Get(TKey key)
        {
            return _state.TryGetValue(key, out var value) ? value : default;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return _state.TryGetValue(key, out value);
        }

        public bool Has(TKey key)
        {
            return _state.ContainsKey(key);
        }

        public void Remove(TKey key)
        {
            _state.Remove(key);
        }

        public void Clear()
        {
            _state.Clear();
        }

        public int Count => _state.Count;

        public IEnumerable<TKey> Keys => _state.Keys;

        public IEnumerable<TValue> Values => _state.Values;

        /// <summary>
        /// Checks if this state satisfies the goal state
        /// For each key in goalState, the value must match this state's value
        /// </summary>
        public bool MeetsGoal(ReGoapState<TKey, TValue> goalState)
        {
            foreach (var pair in goalState._state)
            {
                if (!_state.TryGetValue(pair.Key, out var value))
                    return false;

                if (!EqualityComparer<TValue>.Default.Equals(value, pair.Value))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Adds all differences between current state and goal state
        /// Used to determine missing preconditions
        /// </summary>
        public void AddMissingDifferences(ReGoapState<TKey, TValue> goalState, ReGoapState<TKey, TValue> result)
        {
            foreach (var pair in goalState._state)
            {
                if (!_state.TryGetValue(pair.Key, out var value) ||
                    !EqualityComparer<TValue>.Default.Equals(value, pair.Value))
                {
                    result.Set(pair.Key, pair.Value);
                }
            }
        }

        /// <summary>
        /// Returns the number of differences between this state and another
        /// Used for heuristic estimation in A* planning
        /// </summary>
        public int MissingDifference(ReGoapState<TKey, TValue> otherState, int stopAt = int.MaxValue)
        {
            int count = 0;
            foreach (var pair in otherState._state)
            {
                if (!_state.TryGetValue(pair.Key, out var value) ||
                    !EqualityComparer<TValue>.Default.Equals(value, pair.Value))
                {
                    count++;
                    if (count >= stopAt)
                        return count;
                }
            }
            return count;
        }

        public override string ToString()
        {
            return string.Join(", ", _state.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        }

        public Dictionary<TKey, TValue> GetValues()
        {
            return new Dictionary<TKey, TValue>(_state);
        }
    }
}
