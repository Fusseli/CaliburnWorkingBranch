using System;
using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// Base class for all MimicNPC ReGoap goals
    /// Provides common functionality for role-based goal evaluation and dynamic priority calculation
    /// Goals define "what" the mimic wants to achieve, while actions define "how" to achieve it
    /// </summary>
    /// <remarks>
    /// Design Principles:
    /// - Goals are role-specific (healer, tank, DPS, CC, puller) and shared across classes with same role
    /// - Priority is dynamically calculated based on world state (from sensors reading Body/Brain)
    /// - Multiple goals can be active; planner selects highest-priority goal for planning
    /// - Goal satisfaction is evaluated by checking world state against goal state
    ///
    /// Priority Calculation (from requirements.md 11.15):
    /// - Healer: base = 5.0 * (injured / groupSize) * avgDeficit; ×10 if <50% HP, ×50 if <25% HP
    /// - Tank: base = 3.0 + (enemiesNotOnTank * 2.0); ×3 if losing threat (<75%)
    /// - DPS: base = 2.0; ×0.3 if wrong target (not MainAssist's target)
    /// - CC: base = (unmezzedAdds - 1) * 4.0; ×2 if adds > 3
    /// - Puller: base = 1.0 when no combat, 0.0 when in combat
    /// - Defensive: base = 0.5; ×3 if out of combat 10+ seconds
    ///
    /// Reference: See design.md "Component 4: Role-Based Goals" for DAoC-specific implementations
    /// </remarks>
    public abstract class MimicGoal : ReGoapGoal<string, object>
    {
        protected readonly MimicNPC _body;
        protected readonly MimicBrain _brain;

        /// <summary>
        /// Gets the MimicNPC body for accessing game state
        /// Used to read health, mana, target, group information via sensors
        /// </summary>
        public MimicNPC Body => _body;

        /// <summary>
        /// Gets the MimicBrain for accessing AI state
        /// Used to read aggro lists, role assignments, combat status
        /// </summary>
        public MimicBrain Brain => _brain;

        /// <summary>
        /// Constructs a new MimicGoal with references to body and brain
        /// </summary>
        /// <param name="body">The MimicNPC body for game state access</param>
        /// <param name="brain">The MimicBrain for AI state access</param>
        protected MimicGoal(MimicNPC body, MimicBrain brain) : base()
        {
            _body = body ?? throw new ArgumentNullException(nameof(body));
            _brain = brain ?? throw new ArgumentNullException(nameof(brain));
        }

        /// <summary>
        /// Calculates the priority of this goal based on current world state
        /// Must be implemented by derived classes with role-specific priority formulas
        ///
        /// Priority Guidelines:
        /// - Higher priority = more urgent goal
        /// - Priority 0.0 = goal not applicable (e.g., puller goal when in combat)
        /// - Use multipliers for emergency conditions (e.g., ×10 for critical health)
        /// - Read world state values populated by sensors (no direct Body/Brain access in priority calc)
        ///
        /// Common Patterns:
        /// - Emergency response: Check critical thresholds, apply high multipliers
        /// - Resource management: Scale priority with resource deficit
        /// - Role coordination: Adjust priority based on group state (MainAssist target, etc.)
        /// - Combat phase: Different priorities for in-combat vs out-of-combat
        /// </summary>
        /// <param name="currentState">Current world state populated by sensors</param>
        /// <returns>Priority value (higher = more urgent)</returns>
        public abstract override float GetPriority(ReGoapState<string, object> currentState);

        /// <summary>
        /// Defines the desired world state when this goal is satisfied
        /// Must be implemented by derived classes
        ///
        /// Goal State Examples:
        /// - HealerGoal: { "groupFullHealth": true }
        /// - TankGoal: { "hasHighestThreat": true, "enemiesOnTank": true }
        /// - DPSGoal: { "targetDead": true }
        /// - CCGoal: { "addsControlled": true }
        ///
        /// The planner uses this to determine which actions satisfy the goal
        /// </summary>
        /// <returns>Goal state representing desired world state</returns>
        public abstract override ReGoapState<string, object> GetGoalState();

        /// <summary>
        /// Checks if this goal is currently satisfied by world state
        /// Default implementation checks if current state meets goal state
        /// Override for custom satisfaction logic
        /// </summary>
        /// <param name="currentState">Current world state from sensors</param>
        /// <returns>True if goal is satisfied, false otherwise</returns>
        public override bool IsGoalSatisfied(ReGoapState<string, object> currentState)
        {
            return currentState.MeetsGoal(GetGoalState());
        }

        #region Helper Methods for Priority Calculation

        /// <summary>
        /// Safely gets a value from world state with default fallback
        /// Prevents null reference exceptions when world state is incomplete
        /// </summary>
        /// <typeparam name="T">Type of value to retrieve</typeparam>
        /// <param name="state">World state to query</param>
        /// <param name="key">World state key</param>
        /// <param name="defaultValue">Default value if key not found</param>
        /// <returns>Value from world state or default</returns>
        protected T GetStateValue<T>(ReGoapState<string, object> state, string key, T defaultValue)
        {
            if (state == null)
                return defaultValue;

            try
            {
                var value = state.Get(key);
                if (value == null)
                    return defaultValue;

                if (value is T typedValue)
                    return typedValue;

                // Attempt conversion for numeric types
                if (typeof(T) == typeof(int) && value is double doubleValue)
                    return (T)(object)(int)doubleValue;

                if (typeof(T) == typeof(float) && value is double doubleToFloat)
                    return (T)(object)(float)doubleToFloat;

                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Gets boolean value from world state (safe accessor)
        /// </summary>
        protected bool GetBool(ReGoapState<string, object> state, string key, bool defaultValue = false)
        {
            return GetStateValue(state, key, defaultValue);
        }

        /// <summary>
        /// Gets integer value from world state (safe accessor)
        /// </summary>
        protected int GetInt(ReGoapState<string, object> state, string key, int defaultValue = 0)
        {
            return GetStateValue(state, key, defaultValue);
        }

        /// <summary>
        /// Gets float value from world state (safe accessor)
        /// </summary>
        protected float GetFloat(ReGoapState<string, object> state, string key, float defaultValue = 0.0f)
        {
            return GetStateValue(state, key, defaultValue);
        }

        /// <summary>
        /// Checks if mimic is in combat based on world state
        /// </summary>
        protected bool IsInCombat(ReGoapState<string, object> state)
        {
            return GetBool(state, MimicWorldStateKeys.IN_COMBAT, false);
        }

        /// <summary>
        /// Checks if mimic has a valid target based on world state
        /// </summary>
        protected bool HasTarget(ReGoapState<string, object> state)
        {
            return GetBool(state, MimicWorldStateKeys.HAS_TARGET, false);
        }

        /// <summary>
        /// Gets the number of enemies from world state
        /// </summary>
        protected int GetNumEnemies(ReGoapState<string, object> state)
        {
            return GetInt(state, MimicWorldStateKeys.NUM_ENEMIES, 0);
        }

        /// <summary>
        /// Gets the number of injured group members from world state
        /// </summary>
        protected int GetNumInjured(ReGoapState<string, object> state)
        {
            return GetInt(state, MimicWorldStateKeys.NUM_NEED_HEALING, 0);
        }

        /// <summary>
        /// Gets the number of group members needing emergency healing (<50% HP)
        /// </summary>
        protected int GetNumEmergency(ReGoapState<string, object> state)
        {
            return GetInt(state, MimicWorldStateKeys.NUM_EMERGENCY_HEALING, 0);
        }

        /// <summary>
        /// Gets the number of group members at critical health (<25% HP)
        /// </summary>
        protected int GetNumCritical(ReGoapState<string, object> state)
        {
            return GetInt(state, MimicWorldStateKeys.NUM_CRITICAL_HEALTH, 0);
        }

        /// <summary>
        /// Gets the group size from world state
        /// </summary>
        protected int GetGroupSize(ReGoapState<string, object> state)
        {
            return GetInt(state, MimicWorldStateKeys.GROUP_SIZE, 1);
        }

        /// <summary>
        /// Gets seconds since last combat from world state
        /// </summary>
        protected float GetOutOfCombatTime(ReGoapState<string, object> state)
        {
            return GetFloat(state, MimicWorldStateKeys.OUT_OF_COMBAT_TIME, 0.0f);
        }

        /// <summary>
        /// Checks if current target matches Main Assist's target (DAoC assist train)
        /// </summary>
        protected bool IsTargetingMainAssistTarget(ReGoapState<string, object> state)
        {
            return GetBool(state, MimicWorldStateKeys.TARGET_MATCHES_MAIN_ASSIST, false);
        }

        #endregion

        /// <summary>
        /// Gets a debug string representation of this goal
        /// Includes priority for diagnostics
        /// </summary>
        public virtual string GetDebugInfo(ReGoapState<string, object> currentState)
        {
            float priority = GetPriority(currentState);
            bool satisfied = IsGoalSatisfied(currentState);
            return $"{GetName()} (Priority: {priority:F2}, Satisfied: {satisfied})";
        }

        /// <summary>
        /// Gets a string representation of this goal
        /// </summary>
        public override string ToString()
        {
            return GetName();
        }
    }
}
