using System;
using System.Collections.Generic;
using System.Linq;
using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.ReGoap.Manager;
using DOL.GS.Scripts;

namespace DOL.GS.ReGoap.Mimic
{
    /// <summary>
    /// Mimic-specific ReGoap agent implementation
    /// Bridges ReGoap planning with MimicNPC body and brain
    /// Owns all decision-making logic and state transitions for mimics
    /// </summary>
    public class MimicReGoapAgent : ReGoapAgent<string, object>
    {
        private readonly MimicNPC _body;
        private readonly MimicBrain _brain;
        private int _replanningCount = 0;
        private long _lastReplanTime = 0;
        private const int MAX_REPLANNING_PER_SECOND = 5;
        private bool _isEnabled = true;

        /// <summary>
        /// Gets the MimicNPC body associated with this agent
        /// </summary>
        public MimicNPC Body => _body;

        /// <summary>
        /// Gets the MimicBrain associated with this agent
        /// </summary>
        public MimicBrain Brain => _brain;

        /// <summary>
        /// Constructs a new MimicReGoapAgent for the given MimicNPC
        /// </summary>
        /// <param name="body">The MimicNPC body this agent controls</param>
        /// <param name="brain">The MimicBrain associated with the body</param>
        public MimicReGoapAgent(MimicNPC body, MimicBrain brain) : base()
        {
            _body = body ?? throw new ArgumentNullException(nameof(body));
            _brain = brain ?? throw new ArgumentNullException(nameof(brain));
        }

        /// <summary>
        /// Initializes the agent with sensors, goals, and actions
        /// Must be called after construction to set up the agent
        /// </summary>
        public override void Initialize()
        {
            // Initialize sensors - will be added by sensor framework
            base.Initialize();

            // Note: Sensors, goals, and actions will be added by the initialization system
            // based on the mimic's class and role configuration
        }

        /// <summary>
        /// Updates all sensors with current game state from Body/Brain properties
        /// Should be called every think tick (500ms) before execution
        /// </summary>
        public override void UpdateSensors()
        {
            if (!_isEnabled)
                return;

            try
            {
                // Update all sensors - they read directly from Body/Brain properties
                base.UpdateSensors();
            }
            catch (Exception ex)
            {
                // Log sensor update failure
                Console.WriteLine($"[MimicReGoapAgent] Sensor update failed for {_body.Name}: {ex.Message}");
                // Don't crash - continue with stale world state
            }
        }

        /// <summary>
        /// Executes the current action in the plan
        /// Advances to next action when current completes
        /// Integrates with MimicBrain's Think() cycle
        /// </summary>
        public override void ExecuteCurrentAction()
        {
            if (!_isEnabled)
                return;

            try
            {
                base.ExecuteCurrentAction();
            }
            catch (Exception ex)
            {
                // Log execution failure
                Console.WriteLine($"[MimicReGoapAgent] Action execution failed for {_body.Name}: {ex.Message}");
                // Trigger replanning on error
                OnActionFailed(currentAction);
            }
        }

        /// <summary>
        /// Requests a new plan from the centralized planner manager
        /// Called when no plan exists or current plan fails
        /// </summary>
        public void RequestNewPlan()
        {
            if (!_isEnabled)
                return;

            // Check replanning frequency to prevent infinite loops
            long currentTime = GameLoop.GameLoopTime;
            if (currentTime - _lastReplanTime < 1000)
            {
                _replanningCount++;
                if (_replanningCount >= MAX_REPLANNING_PER_SECOND)
                {
                    // Excessive replanning detected
                    Console.WriteLine($"[MimicReGoapAgent] Excessive replanning detected for {_body.Name}. " +
                                    $"Temporarily disabling ReGoap for 10 seconds.");
                    DisableTemporarily(10000);
                    return;
                }
            }
            else
            {
                // Reset counter after 1 second
                _replanningCount = 0;
                _lastReplanTime = currentTime;
            }

            // Get highest priority goal
            var goal = GetHighestPriorityGoal();
            if (goal == null)
            {
                // No goals available or all satisfied
                ClearPlan();
                return;
            }

            // Request plan from centralized manager
            ReGoapPlannerManager.Instance.RequestPlan(this, goal, OnPlanReceived);

            // Store current goal
            SetCurrentGoal(goal);
        }

        /// <summary>
        /// Callback invoked when a plan is received from the planner manager
        /// </summary>
        private void OnPlanReceived(Queue<IReGoapAction<string, object>> plan)
        {
            if (plan != null && plan.Count > 0)
            {
                SetPlan(plan);
            }
            else
            {
                // No valid plan found - use fallback behavior
                Console.WriteLine($"[MimicReGoapAgent] No valid plan found for {_body.Name} goal {currentGoal?.GetName() ?? "Unknown"}");
                UseFallbackBehavior();
            }
        }

        /// <summary>
        /// Called when current plan completes successfully
        /// Triggers replanning for next goal
        /// </summary>
        protected override void OnPlanComplete()
        {
            base.OnPlanComplete();

            // Request new plan for next goal
            RequestNewPlan();
        }

        /// <summary>
        /// Called when current plan fails (action preconditions fail, action fails)
        /// Triggers immediate replanning with updated world state
        /// </summary>
        protected override void OnPlanFailed()
        {
            base.OnPlanFailed();

            // Update sensors to get fresh world state
            UpdateSensors();

            // Request new plan immediately
            RequestNewPlan();
        }

        /// <summary>
        /// Adds a goal to this agent's goal list
        /// Goals are evaluated by priority each think tick
        /// </summary>
        public new void AddGoal(IReGoapGoal<string, object> goal)
        {
            base.AddGoal(goal);
        }

        /// <summary>
        /// Removes a goal from this agent's goal list
        /// </summary>
        public new void RemoveGoal(IReGoapGoal<string, object> goal)
        {
            base.RemoveGoal(goal);
        }

        /// <summary>
        /// Refreshes goals based on the mimic's current role
        /// Called when role changes (e.g., MainTank, MainCC, healer, DPS)
        /// </summary>
        /// <param name="role">The role identifier</param>
        public void RefreshGoalsForRole(string role)
        {
            // Clear existing goals
            goals.Clear();

            // Add role-specific goals
            // Note: Goal instances will be created by the goal factory based on role
            // This is a placeholder for the goal management system
            Console.WriteLine($"[MimicReGoapAgent] Refreshing goals for role: {role} (mimic: {_body.Name})");

            // Goals will be added by external initialization based on role
            // e.g., HealerGoal, TankGoal, DPSGoal, etc.
        }

        /// <summary>
        /// Registers an action for this agent
        /// Actions are generated based on available spells/abilities
        /// </summary>
        public void RegisterAction(IReGoapAction<string, object> action)
        {
            AddAction(action);
        }

        /// <summary>
        /// Clears all actions from this agent
        /// Used when regenerating action set for class/spec changes
        /// </summary>
        public void ClearActions()
        {
            actions.Clear();
        }

        /// <summary>
        /// Generates actions for the mimic's class
        /// Enumerates available spells/abilities from Body and creates corresponding actions
        /// </summary>
        /// <param name="mimicClass">The mimic's class</param>
        public void GenerateActionsForClass(eMimicClass mimicClass)
        {
            // Clear existing actions
            ClearActions();

            // Actions will be generated by ActionFactory based on class
            // ActionFactory will enumerate Body.GetSpellLines(), Body.GetSpellList()
            // and create CastSpellAction, MeleeAttackAction, etc.
            Console.WriteLine($"[MimicReGoapAgent] Generating actions for class: {mimicClass} (mimic: {_body.Name})");

            // Action generation will be handled by external factory
            // This method serves as the entry point for action initialization
        }

        /// <summary>
        /// Uses fallback behavior when ReGoap cannot generate a plan
        /// Falls back to legacy AI or safe default behavior
        /// </summary>
        public void UseFallbackBehavior()
        {
            // For now, clear plan and let legacy AI take over
            // In future, could implement safe default actions (idle, follow leader, defensive buffs)
            ClearPlan();

            Console.WriteLine($"[MimicReGoapAgent] Using fallback behavior for {_body.Name}");

            // Could trigger legacy AI here if configured
            // For now, mimic will use legacy AI on next think tick if no ReGoap plan exists
        }

        /// <summary>
        /// Checks if ReGoap is enabled for this agent
        /// </summary>
        public bool IsEnabled()
        {
            return _isEnabled;
        }

        /// <summary>
        /// Enables ReGoap AI for this agent
        /// </summary>
        public void Enable()
        {
            _isEnabled = true;
        }

        /// <summary>
        /// Disables ReGoap AI for this agent
        /// Falls back to legacy AI
        /// </summary>
        public void Disable()
        {
            _isEnabled = false;
            ClearPlan();
        }

        /// <summary>
        /// Temporarily disables ReGoap for a specified duration
        /// Used when infinite replanning loops are detected
        /// </summary>
        private void DisableTemporarily(int durationMs)
        {
            _isEnabled = false;
            ClearPlan();

            // Re-enable after duration
            var timer = new ECSGameTimer(_body, new ECSGameTimer.ECSTimerCallback((_) =>
            {
                _isEnabled = true;
                _replanningCount = 0;
                Console.WriteLine($"[MimicReGoapAgent] Re-enabling ReGoap for {_body.Name}");
                return 0; // Don't repeat
            }), durationMs);
        }

        /// <summary>
        /// Validates agent state integrity
        /// Returns true if state is valid, false if recovery needed
        /// </summary>
        public bool ValidateState()
        {
            // Check for null memory
            if (memory == null)
            {
                Console.WriteLine($"[MimicReGoapAgent] Invalid state: null memory for {_body.Name}");
                return false;
            }

            // Check for empty sensor list (should have at least health/mana sensors)
            if (sensors == null || sensors.Count == 0)
            {
                Console.WriteLine($"[MimicReGoapAgent] Invalid state: no sensors for {_body.Name}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Attempts to recover from invalid state
        /// Reinitializes sensors and world state
        /// </summary>
        public bool RecoverFromInvalidState()
        {
            try
            {
                Console.WriteLine($"[MimicReGoapAgent] Attempting state recovery for {_body.Name}");

                // Reinitialize memory if null
                if (memory == null)
                {
                    memory = new ReGoapMemory<string, object>();
                }

                // Clear corrupted data
                memory.Clear();
                ClearPlan();

                // Reinitialize sensors
                foreach (var sensor in sensors)
                {
                    sensor.Init(this);
                }

                // Update sensors to populate fresh world state
                UpdateSensors();

                Console.WriteLine($"[MimicReGoapAgent] State recovery successful for {_body.Name}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MimicReGoapAgent] State recovery failed for {_body.Name}: {ex.Message}");
                // Fall back to disabling ReGoap
                Disable();
                return false;
            }
        }

        /// <summary>
        /// Gets debug information about current agent state
        /// Used for debugging and diagnostics
        /// </summary>
        public string GetDebugInfo()
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine($"=== MimicReGoapAgent Debug Info: {_body.Name} ===");
            sb.AppendLine($"Enabled: {_isEnabled}");
            sb.AppendLine($"Current Goal: {currentGoal?.GetName() ?? "None"}");

            if (currentGoal != null)
            {
                var worldState = memory.GetWorldState();
                sb.AppendLine($"Goal Priority: {currentGoal.GetPriority(worldState)}");
            }

            sb.AppendLine($"Has Plan: {HasPlan()}");
            sb.AppendLine($"Plan Length: {currentPlan?.Count ?? 0}");
            sb.AppendLine($"Current Action: {currentAction?.GetName() ?? "None"}");
            sb.AppendLine($"Action Running: {actionRunning}");
            sb.AppendLine($"Total Goals: {goals.Count}");
            sb.AppendLine($"Total Actions: {actions.Count}");
            sb.AppendLine($"Total Sensors: {sensors.Count}");
            sb.AppendLine($"Replanning Count: {_replanningCount}");

            sb.AppendLine("\nWorld State:");
            var ws = memory.GetWorldState();
            if (ws != null)
            {
                foreach (var kvp in ws.GetValues())
                {
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                }
            }

            sb.AppendLine("\nActive Goals:");
            var worldStateForGoals = memory.GetWorldState();
            foreach (var goal in goals)
            {
                bool satisfied = goal.IsGoalSatisfied(worldStateForGoals);
                float priority = goal.GetPriority(worldStateForGoals);
                sb.AppendLine($"  {goal.GetName()}: Priority={priority:F2}, Satisfied={satisfied}");
            }

            if (currentPlan != null && currentPlan.Count > 0)
            {
                sb.AppendLine("\nPlanned Actions:");
                foreach (var action in currentPlan)
                {
                    sb.AppendLine($"  {action.GetName()}");
                }
            }

            return sb.ToString();
        }
    }
}
