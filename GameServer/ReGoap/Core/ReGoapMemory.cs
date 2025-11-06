using System;

namespace DOL.GS.ReGoap.Core
{
    /// <summary>
    /// Memory component for ReGoap agents that stores the current world state
    /// Acts as a blackboard for sensors to write to and actions to read from
    /// </summary>
    public class ReGoapMemory<TKey, TValue>
    {
        private readonly ReGoapState<TKey, TValue> _worldState;

        public ReGoapMemory()
        {
            _worldState = new ReGoapState<TKey, TValue>();
        }

        /// <summary>
        /// Gets the current world state
        /// This represents the agent's understanding of the game world
        /// </summary>
        public ReGoapState<TKey, TValue> GetWorldState()
        {
            return _worldState;
        }

        /// <summary>
        /// Sets a value in the world state
        /// Called by sensors during their update cycle
        /// </summary>
        public void SetValue(TKey key, TValue value)
        {
            _worldState.Set(key, value);
        }

        /// <summary>
        /// Gets a value from the world state
        /// </summary>
        public TValue GetValue(TKey key)
        {
            return _worldState.Get(key);
        }

        /// <summary>
        /// Checks if a key exists in the world state
        /// </summary>
        public bool HasValue(TKey key)
        {
            return _worldState.Has(key);
        }

        /// <summary>
        /// Removes a value from the world state
        /// </summary>
        public void RemoveValue(TKey key)
        {
            _worldState.Remove(key);
        }

        /// <summary>
        /// Clears all world state values
        /// </summary>
        public void Clear()
        {
            _worldState.Clear();
        }
    }
}
