using System;
using System.Linq;
using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.ReGoap.Mimic.Actions;
using DOL.GS.ReGoap.Mimic.Goals;
using DOL.GS.ReGoap.Mimic.Sensors;
using DOL.GS.Scripts;

namespace DOL.GS.ReGoap.Mimic
{
    public enum MimicDecisionContext { Healing, Support, Combat }

    /// <summary>
    /// Tactical GOAP on the brain's owning thread. The FSM supplies the travel/combat
    /// lifecycle and resumes its legacy decision on a false result. Actions issue
    /// one tick of work; the next tick senses actual results and reprioritizes.
    /// Predicted effects are never written into live memory.
    /// </summary>
    public class MimicReGoapAgent : ReGoapAgent<string, object>
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(typeof(MimicReGoapAgent));
        private readonly ReGoapPlanner<string, object> _planner = new();
        private readonly System.Diagnostics.Stopwatch _phaseWatch = new();
        private bool _initialized;
        private bool _enabled = true;
        private long _retryAt;
        public MimicNPC Body { get; }
        public MimicBrain Brain { get; }
        public MimicDecisionContext Context { get; private set; }
        public long Decisions { get; private set; }
        public long Fallbacks { get; private set; }
        public string LastGoal { get; private set; } = "None";
        public string LastAction { get; private set; } = "None";
        public string Status { get; private set; } = "Not yet ticked";
        /// <summary>Last think's milliseconds in sensors / planner. Diagnostics only;
        /// decisions never depend on them. Reset to 0 when a think does no work.</summary>
        public long LastSenseMs { get; private set; }
        public long LastPlanMs { get; private set; }

        public MimicReGoapAgent(MimicNPC body, MimicBrain brain)
        {
            Body = body ?? throw new ArgumentNullException(nameof(body));
            Brain = brain ?? throw new ArgumentNullException(nameof(brain));
        }

        public override void Initialize()
        {
            if (_initialized) return;
            sensors.Clear();
            goals.Clear();
            actions.Clear();
            AddSensor(new HealthSensor());
            AddSensor(new ManaSensor());
            AddSensor(new CombatStatusSensor());
            AddSensor(new TargetSensor());
            AddSensor(new SpellAvailabilitySensor());
            AddSensor(new TacticalSensor());
            AddGoal(new EmergencyHealGoal(Body, Brain));
            AddGoal(new HealGroupGoal(Body, Brain));
            AddGoal(new CureGroupGoal(Body, Brain));
            AddGoal(new ResurrectGoal(Body, Brain));
            AddGoal(new ResurrectOutsiderGoal(Body, Brain));
            AddGoal(new PurgeGoal(Body, Brain));
            AddGoal(new ControlAddsGoal(Body, Brain));
            AddGoal(new InterruptEnemyCasterGoal(Body, Brain));
            AddGoal(new AssistTrainGoal(Body, Brain));
            AddGoal(new TargetCallerGoal(Body, Brain));
            AddGoal(new ProtectGroupGoal(Body, Brain));
            AddGoal(new GuardHealerGoal(Body, Brain));
            AddGoal(new QuickcastRecoveryGoal(Body, Brain));
            AddGoal(new KiteGoal(Body, Brain));
            AddGoal(new DebuffPriorityGoal(Body, Brain));
            AddGoal(new PositionalStyleGoal(Body, Brain));
            AddGoal(new ArrowTypeGoal(Body, Brain));
            // PR31/PR34: singing is a main job for both bards and minstrels - they are
            // their realm's speed class and hold the only instrument songs in the game.
            // One priority for both; see SongGoal for why the old split was dropped.
            AddGoal(new SongGoal(Body, Brain));
            AddGoal(new DealDamageGoal(Body, Brain));
            AddGoal(new BuffMaintenanceGoal(Body, Brain));
            MimicTacticalAction.Register(this);
            _initialized = true;
        }

        public bool TryThink(MimicDecisionContext context)
        {
            LastSenseMs = 0;
            LastPlanMs = 0;
            if (!_enabled || GameLoop.GameLoopTime < _retryAt)
                return Fallback(_enabled ? "Error retry delay" : "Disabled");
            try
            {
                Initialize();
                if (sensors.Count == 0 || goals.Count == 0 || actions.Count == 0)
                    return Fallback("Missing sensors, goals or actions");
                if (!Brain.IsActive) return Fallback("Inactive body");
                Context = context;
                if (context == MimicDecisionContext.Combat && !Brain.PreventCombat && !Brain.IsHealer)
                    Brain.SelectGoapAttackTarget();
                // Fail closed on sensor exceptions: never use stale data.
                _phaseWatch.Restart();
                UpdateSensors();
                _phaseWatch.Stop();
                LastSenseMs = _phaseWatch.ElapsedMilliseconds;
                LastPlanMs = 0;
                ClearPlan();
                var available = actions.ToArray();
                try
                {
                    // Failed commands are excluded for this decision. Bounded retries
                    // allow a failed spell to yield to engagement without recursion.
                    for (int attempt = 0; attempt < available.Length; attempt++)
                    {
                        _phaseWatch.Restart();
                        bool planned = TryPlan(_planner);
                        _phaseWatch.Stop();
                        LastPlanMs += _phaseWatch.ElapsedMilliseconds;
                        if (!planned)
                        {
                            // PR40: a spell in flight is our work and the tick stays here.
                            if (!ShouldFallbackToFsm(planned, IsCasting))
                            {
                                HoldTickForCast();
                                return true;
                            }
                            return Fallback("No achievable active goal");
                        }
                        LastGoal = currentGoal.GetName();
                        var action = currentPlan.Peek();
                        LastAction = action.GetName();
                        ExecuteCurrentAction();
                        if (!LastActionFailed)
                        {
                            Decisions++;
                            Status = "GOAP";
                            return true;
                        }
                        actions.Remove(action);
                        UpdateSensors();
                    }
                    // PR40: same rule at the second exit. Every action failed, but if a
                    // spell is running, handing the tick to the FSM would destroy it.
                    if (!ShouldFallbackToFsm(false, IsCasting))
                    {
                        HoldTickForCast();
                        return true;
                    }
                    return Fallback("All eligible actions failed");
                }
                finally
                {
                    actions.Clear();
                    actions.AddRange(available);
                }
            }
            catch (Exception ex)
            {
                _retryAt = GameLoop.GameLoopTime + 5000;
                Log.Warn($"GOAP failed for {Body.Name}; using FSM and retrying in 5 seconds", ex);
                return Fallback($"{ex.GetType().Name}: {ex.Message}");
            }
        }

        private bool Fallback(string reason)
        {
            ClearPlan();
            Fallbacks++;
            Status = "FSM: " + reason;
            LastGoal = "None";
            LastAction = "None";
            return false;
        }

        /// <summary>
        /// PR40: is a spell running?
        ///
        /// Read through the casting component rather than Body.IsCasting: the casting
        /// component is a plain field on GameLiving, so it is null on a body that was
        /// never constructed, and the agent's own test double is exactly that.
        /// </summary>
        private bool IsCasting => Body?.castingComponent?.IsCasting == true;

        /// <summary>
        /// PR40: should the agent hand this tick to the FSM?
        ///
        /// "No plan" means the planner found no goal whose action can run. If a spell is
        /// in flight at that moment, the GOAP still owns the tick, because **every FSM
        /// fallback destroys a cast**:
        ///
        ///  - a weapon switch cancels the spell - <c>MimicNPC.SwitchWeapon</c> calls
        ///    <c>StopCurrentSpellcast()</c>
        ///  - sitting cancels it - the <c>IsSitting</c> setter calls
        ///    <c>CurrentSpellHandler?.CasterMoves()</c>
        ///  - moving cancels it - <c>SpellHandler.CheckDuringCast</c> calls
        ///    <c>CasterMoves()</c> when the caster is moving
        ///
        /// PR38 added <c>!IsCasting</c> to <c>OffenseAvailable</c>, which was correct -
        /// it broke the queued-cast chain - but it also meant that while a spell was
        /// running there was no action left for the planner, so the agent fell back to
        /// the FSM and the FSM's weapon flip destroyed the spell. An archer's shot was
        /// started and cancelled every think tick, and a sorcerer's self buff was
        /// interrupted the same way.
        ///
        /// Before PR38 the fallback never happened during a cast, because
        /// <c>OffenseAvailable</c> was still true and <c>CheckSpells</c> returned
        /// <c>casted || Body.IsCasting</c> - the action "succeeded" without doing
        /// anything, which is how the GOAP used to keep the tick.
        ///
        /// Pure, so the decision can be pinned without a body.
        /// </summary>
        public static bool ShouldFallbackToFsm(bool hasPlan, bool isCasting)
            => !hasPlan && !isCasting;

        /// <summary>
        /// PR40: take the tick while a spell is running. The status says so, so
        /// <c>/mgoap status</c> shows "GOAP: casting" instead of an FSM reason - which
        /// is how you tell "the bot is busy casting" from "the bot had nothing to do".
        /// </summary>
        private void HoldTickForCast()
        {
            Status = "GOAP: casting";
            LastGoal = "None";
            LastAction = "None";
        }

        public bool IsEnabled() => _enabled;
        public void Enable() { _enabled = true; _retryAt = 0; }
        public void Disable() { _enabled = false; ClearPlan(); Status = "FSM: Disabled"; }
        public string GetDebugInfo() =>
            $"AI: {Status}\nEnabled: {_enabled}\nGOAP decisions: {Decisions}; fallback decisions: {Fallbacks}\n" +
            $"Last goal: {LastGoal}\nLast action: {LastAction}\n" +
            $"Last think: sense {LastSenseMs}ms; plan {LastPlanMs}ms\n" +
            // PR38: the three numbers that explain why control is or is not chosen.
            // "adds" is the ACTIONABLE count, so it must fall to zero the moment control
            // is on - if it sits, the bot is trying to control something it cannot.
            $"Control: adds {Brain.GetCcCandidates().Count}; ccPrimary {Brain.IsCcPrimary}; " +
            $"underPressure {Brain.IsUnderPressure()}\n" +
            $"Sensors: {sensors.Count}; goals: {goals.Count}; actions: {actions.Count}";
    }
}
