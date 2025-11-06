using System;
using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;

namespace DOL.GS.ReGoap.Mimic.Sensors
{
    /// <summary>
    /// Base class for all MimicNPC ReGoap sensors
    /// Provides common functionality for reading game state from Body/Brain and updating world state
    /// Sensors are thin wrappers - they perform zero logic, only read existing properties
    /// </summary>
    /// <remarks>
    /// Design Principles (from design.md Component 3: Sensor Framework):
    /// - Sensors are thin wrappers that read existing game state from MimicNPC.Body and MimicBrain properties
    /// - NO data duplication or logic replication - sensors simply translate game state into ReGoap format
    /// - All sensors read from existing cached data (group health from MimicGroup, aggro from Brain.AggroList)
    /// - Sensors update world state memory within the 500ms think interval
    /// - Thread-safe access to shared state via existing concurrent collections
    ///
    /// Usage Pattern:
    /// 1. Init() called once when sensor is added to agent
    /// 2. UpdateSensor() called every think tick (500ms) to refresh world state
    /// 3. Sensors read directly from Body/Brain properties (no calculation)
    /// 4. World state keys defined in MimicWorldStateKeys
    ///
    /// Reference: See design.md "Sensor → Existing Game Properties" for integration examples
    /// Requirements: 2.1, 2.3, 2.4
    /// </remarks>
    public abstract class MimicSensor : ReGoapSensor<string, object>
    {
        protected MimicNPC _body;
        protected MimicBrain _brain;
        protected ReGoapState<string, object> _worldState;

        /// <summary>
        /// Gets the MimicNPC body for reading game state
        /// Direct access to health, mana, target, combat status, etc.
        /// </summary>
        public MimicNPC Body => _body;

        /// <summary>
        /// Gets the MimicBrain for reading AI state
        /// Direct access to aggro lists, target selection, role information
        /// </summary>
        public MimicBrain Brain => _brain;

        /// <summary>
        /// Gets the world state being updated by this sensor
        /// Sensors write values to this state, which is then used by goals/actions
        /// </summary>
        public ReGoapState<string, object> WorldState => _worldState;

        /// <summary>
        /// Initializes the sensor with agent reference and extracts Body/Brain
        /// Called once when sensor is added to the agent
        /// </summary>
        /// <param name="agent">The ReGoap agent (must be MimicReGoapAgent)</param>
        public override void Init(IReGoapAgent<string, object> agent)
        {
            base.Init(agent);

            // Extract Body and Brain from the agent
            if (agent is DOL.GS.ReGoap.Mimic.MimicReGoapAgent mimicAgent)
            {
                _body = mimicAgent.Body;
                _brain = mimicAgent.Brain;
            }
            else
            {
                throw new InvalidOperationException(
                    $"MimicSensor requires MimicReGoapAgent, but received {agent?.GetType().Name ?? "null"}");
            }

            // Get world state from memory
            _worldState = memory.GetWorldState();

            // Validate references
            if (_body == null)
                throw new ArgumentNullException(nameof(_body), "MimicNPC Body is null");

            if (_brain == null)
                throw new ArgumentNullException(nameof(_brain), "MimicBrain is null");

            if (_worldState == null)
                throw new InvalidOperationException("World state is null - memory not initialized");
        }

        /// <summary>
        /// Updates world state with current game state from Body/Brain
        /// Must be implemented by derived classes
        /// Should read existing properties only - no calculations or logic duplication
        ///
        /// Examples (from design.md):
        /// - HealthSensor: worldState.Set("selfHealthPercent", _body.HealthPercent)
        /// - ManaSensor: worldState.Set("selfMana", _body.Mana)
        /// - AggroSensor: worldState.Set("hasAggro", _brain.AggroList.Count > 0)
        /// - GroupHealthSensor: worldState.Set("groupHealthDeficit", _body.Group.MimicGroup.AmountToHeal)
        ///
        /// Performance:
        /// - Reuse existing cached data (MimicGroup.CheckGroupHealth() results)
        /// - No recalculation of aggro, health, or target logic
        /// - Simple property reads only
        /// </summary>
        public abstract override void UpdateSensor();

        #region Helper Methods for Safe World State Updates

        /// <summary>
        /// Safely sets a value in world state
        /// Handles null checks and type validation
        /// </summary>
        /// <param name="key">World state key (use MimicWorldStateKeys constants)</param>
        /// <param name="value">Value to set</param>
        protected void SetWorldState(string key, object value)
        {
            if (_worldState == null)
            {
                // Log error but don't crash - sensor may be in initialization
                Console.WriteLine($"[MimicSensor] Cannot set world state '{key}' - world state is null");
                return;
            }

            if (string.IsNullOrEmpty(key))
            {
                Console.WriteLine("[MimicSensor] Cannot set world state - key is null or empty");
                return;
            }

            try
            {
                _worldState.Set(key, value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MimicSensor] Error setting world state '{key}': {ex.Message}");
            }
        }

        /// <summary>
        /// Sets a boolean value in world state (type-safe convenience method)
        /// </summary>
        protected void SetBool(string key, bool value)
        {
            SetWorldState(key, value);
        }

        /// <summary>
        /// Sets an integer value in world state (type-safe convenience method)
        /// </summary>
        protected void SetInt(string key, int value)
        {
            SetWorldState(key, value);
        }

        /// <summary>
        /// Sets a float value in world state (type-safe convenience method)
        /// </summary>
        protected void SetFloat(string key, float value)
        {
            SetWorldState(key, value);
        }

        /// <summary>
        /// Sets an object reference in world state (for GameObjects, targets, etc.)
        /// </summary>
        protected void SetObject(string key, object value)
        {
            SetWorldState(key, value);
        }

        #endregion

        #region Helper Methods for Reading Body/Brain State

        /// <summary>
        /// Checks if Body reference is valid and alive
        /// Prevents null reference exceptions when reading Body properties
        /// </summary>
        protected bool IsBodyValid()
        {
            return _body != null && _body.IsAlive && _body.ObjectState == GameObject.eObjectState.Active;
        }

        /// <summary>
        /// Checks if Brain reference is valid and active
        /// Prevents null reference exceptions when reading Brain properties
        /// </summary>
        protected bool IsBrainValid()
        {
            return _brain != null && _brain.IsActive;
        }

        /// <summary>
        /// Checks if mimic is in a valid group
        /// Used by group-related sensors (GroupHealthSensor, etc.)
        /// </summary>
        protected bool IsInGroup()
        {
            return _body != null && _body.Group != null && _body.Group.MemberCount > 1;
        }

        /// <summary>
        /// Gets the MimicGroup instance (for group state reading)
        /// Returns null if not in a group or group is not a mimic group
        /// </summary>
        protected MimicGroup GetMimicGroup()
        {
            if (!IsInGroup())
                return null;

            return _body.Group?.MimicGroup;
        }

        #endregion

        #region Validation and Debugging

        /// <summary>
        /// Validates that sensor has required references before updating
        /// Called internally before UpdateSensor() to ensure safe operation
        /// </summary>
        protected virtual bool ValidateSensor()
        {
            if (_body == null)
            {
                Console.WriteLine($"[{GetType().Name}] Body reference is null");
                return false;
            }

            if (_brain == null)
            {
                Console.WriteLine($"[{GetType().Name}] Brain reference is null");
                return false;
            }

            if (_worldState == null)
            {
                Console.WriteLine($"[{GetType().Name}] World state reference is null");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets debug information about this sensor
        /// Shows sensor name and key state values
        /// </summary>
        public virtual string GetDebugInfo()
        {
            return $"{GetType().Name} (Body: {_body?.Name ?? "null"}, Valid: {ValidateSensor()})";
        }

        #endregion

        /// <summary>
        /// Gets string representation of this sensor
        /// </summary>
        public override string ToString()
        {
            return GetType().Name;
        }
    }
}
