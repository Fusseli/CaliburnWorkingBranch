using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using DOL.AI.Brain;
using DOL.GS;
using DOL.GS.ReGoap.Core;
using DOL.GS.ReGoap.Mimic;
using DOL.GS.ReGoap.Mimic.Goals;
using DOL.GS.ReGoap.Mimic.Sensors;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;
using NUnit.Framework;

namespace DOL.Tests.Unit
{
    [TestFixture]
    public class GoapTests
    {
        private static ReGoapState<string, object> State(params (string Key, object Value)[] values)
        {
            var state = new ReGoapState<string, object>();
            foreach (var value in values) state.Set(value.Key, value.Value);
            return state;
        }

        private sealed class Goal : ReGoapGoal<string, object>
        {
            public float Priority;
            public Goal(string effect, float priority)
            {
                goalState = State((effect, true));
                Priority = priority;
            }
            public override float GetPriority(ReGoapState<string, object> state) => Priority;
            public override ReGoapState<string, object> GetGoalState() => goalState;
        }

        private sealed class Step : ReGoapAction<string, object>
        {
            public int Runs;
            public bool Fail, Running, ReturnOnly, Available = true;
            public float Cost = 1;
            public Step(string effect, string requires = null)
            {
                name = effect;
                effects.Set(effect, true);
                if (requires != null) preconditions.Set(requires, true);
            }
            public override float GetCost(IReGoapAgent<string, object> agent, ReGoapState<string, object> state) => Cost;
            public override bool CheckPreconditions(IReGoapAgent<string, object> agent, ReGoapState<string, object> state)
                => Available && base.CheckPreconditions(agent, state);
            public override bool Run(IReGoapAgent<string, object> agent,
                Action<IReGoapAction<string, object>> done, Action<IReGoapAction<string, object>> fail)
            {
                Runs++;
                if (Running) return false;
                if (!ReturnOnly) { if (Fail) fail(this); else done(this); }
                return true;
            }
        }

        [Test]
        public void WorldStatesCompareByValueRegardlessOfInsertionOrder()
        {
            var a = State(("a", true), ("b", 1));
            var b = State(("b", 1), ("a", true));
            Assert.That(a, Is.EqualTo(b));
            Assert.That(new HashSet<ReGoapState<string, object>> { a }.Contains(b), Is.True);
        }

        [Test]
        public void PlannerFindsCheaperMultiStepPathAndTerminatesUnreachableCycles()
        {
            var agent = new ReGoapAgent<string, object>();
            var prepare = new Step("ready") { Cost = 0.1f };
            var use = new Step("finished", "ready") { Cost = 0.1f };
            agent.AddAction(new Step("finished") { Cost = 5 });
            agent.AddAction(prepare);
            agent.AddAction(use);
            var planner = new ReGoapPlanner<string, object>();
            Assert.That(planner.Plan(agent, State(), State(("finished", true))), Is.EqualTo(new[] { prepare, use }));
            Assert.That(planner.Plan(agent, State(), State(("unreachable", true))), Is.Null);
        }

        [Test]
        public void UnachievableAndZeroPriorityGoalsDoNotStarvePossibleWork()
        {
            var agent = new ReGoapAgent<string, object>();
            agent.AddGoal(new Goal("missing", 100));
            agent.AddGoal(new Goal("inactive", 0));
            var reachable = new Goal("damage", 2);
            agent.AddGoal(reachable);
            agent.AddAction(new Step("damage"));
            agent.AddAction(new Step("inactive"));
            Assert.That(agent.TryPlan(new()), Is.True);
            Assert.That(agent.GetCurrentGoal(), Is.SameAs(reachable));
        }

        [Test]
        public void PlannerSkipsCheapActionWithFailedRuntimePrecondition()
        {
            var agent = new ReGoapAgent<string, object>();
            agent.AddAction(new Step("damage") { Available = false });
            var valid = new Step("damage") { Cost = 2 };
            agent.AddAction(valid);
            Assert.That(new ReGoapPlanner<string, object>().Plan(agent, State(), State(("damage", true)))?.Peek(), Is.SameAs(valid));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FinalRunningActionRemainsAPlanUntilCompletion(bool returnOnly)
        {
            var agent = new ReGoapAgent<string, object>();
            var action = new Step("damage") { Running = true, ReturnOnly = returnOnly };
            agent.SetPlan(new(new[] { action }));
            agent.ExecuteCurrentAction();
            Assert.That(agent.HasPlan(), Is.True);
            action.Running = false;
            agent.ExecuteCurrentAction();
            Assert.That(agent.HasPlan(), Is.False);
            Assert.That(agent.GetCurrentAction(), Is.Null);
        }

        // Goal/factory construction needs identity only, not a database-backed NPC.
        // These tests never execute real game mechanics against this placeholder.
        private static MimicNPC Identity() => (MimicNPC)RuntimeHelpers.GetUninitializedObject(typeof(MimicNPC));

        [TestCase("DealDamageGoal", "CastOffensiveSpell")]
        [TestCase("EmergencyHealGoal", "HealOrCure")]
        [TestCase("HealGroupGoal", "HealOrCure")]
        [TestCase("CureGroupGoal", "HealOrCure")]
        [TestCase("ResurrectGoal", "CastRez")]
        [TestCase("ResurrectOutsiderGoal", "CastRezOutside")]
        [TestCase("PurgeGoal", "UsePurge")]
        [TestCase("ControlAddsGoal", "ControlAdd")]
        [TestCase("BuffMaintenanceGoal", "MaintainBuffs")]
        [TestCase("InterruptEnemyCasterGoal", "InterruptCaster")]
        [TestCase("AssistTrainGoal", "FollowAssist")]
        [TestCase("ProtectGroupGoal", "PeelEnemy")]
        [TestCase("GuardHealerGoal", "AssignGuard")]
        [TestCase("KiteGoal", "KiteToSafety")]
        [TestCase("QuickcastRecoveryGoal", "SecureCast")]
        [TestCase("TargetCallerGoal", "CallFocusTarget")]
        [TestCase("DebuffPriorityGoal", "ApplyDebuff")]
        [TestCase("PositionalStyleGoal", "MoveToFlank")]
        [TestCase("ArrowTypeGoal", "ChooseArrowType")]
        public void EveryRegisteredGoalHasAnActionWithMatchingEffects(string goalName, string actionName)
        {
            var agent = new MimicReGoapAgent(Identity(), new MimicBrain());
            agent.Initialize();
            agent.Initialize();
            Assert.That(agent.GetGoals(), Has.Count.EqualTo(19));
            Assert.That(agent.GetActions(), Has.Count.EqualTo(18));
            Assert.That(agent.GetSensors(), Has.Count.EqualTo(6));
            var goal = agent.GetGoals().Single(g => g.GetName() == goalName);
            var state = State((TacticalSensor.SupportAvailable, true), (TacticalSensor.BuffsAvailable, true),
                (TacticalSensor.ControlAvailable, true), (TacticalSensor.OffenseAvailable, true), (TacticalSensor.AttackAvailable, true),
                (TacticalSensor.InterruptAvailable, true), (TacticalSensor.AssistAvailable, true), (TacticalSensor.PeelAvailable, true),
                (TacticalSensor.GuardAvailable, true), (TacticalSensor.KiteAvailable, true), (TacticalSensor.QuickcastReady, true),
                (TacticalSensor.CallAvailable, true), (TacticalSensor.DebuffAvailable, true), (TacticalSensor.FlankAvailable, true),
                (TacticalSensor.ArrowTypeAvailable, true), (TacticalSensor.RezAvailable, true), (TacticalSensor.RezOutsideAvailable, true),
                (TacticalSensor.PurgeAvailable, true));
            var plan = new ReGoapPlanner<string, object>().Plan(agent, state, goal.GetGoalState());
            Assert.That(plan, Is.Not.Null);
            Assert.That(plan.Peek().GetName(), Is.EqualTo(actionName));
        }

        [Test]
        public void LowestResistWinsArrowTypeWithStickiness()
        {
            Assert.That(MimicBrain.BestArrowType(0, 20, 20, eDamageType.Slash), Is.EqualTo(eDamageType.Crush));
            Assert.That(MimicBrain.BestArrowType(20, 0, 20, eDamageType.Slash), Is.EqualTo(eDamageType.Slash));
            Assert.That(MimicBrain.BestArrowType(20, 20, 0, eDamageType.Slash), Is.EqualTo(eDamageType.Thrust));
            Assert.That(MimicBrain.BestArrowType(10, 10, 10, eDamageType.Thrust), Is.EqualTo(eDamageType.Thrust),
                "Ties keep the current type (Slash default needs no buff)");
            Assert.That(MimicBrain.BestArrowType(10, 12, 30, eDamageType.Slash), Is.EqualTo(eDamageType.Slash),
                "Small gaps keep the current type: no flapping");
            Assert.That(MimicBrain.BestArrowType(0, 12, 30, eDamageType.Slash), Is.EqualTo(eDamageType.Crush),
                "Large gaps switch");
        }

        [Test]
        public void MismatchedArrowBuffActivatesChooserGoal()
        {
            var agent = new MimicReGoapAgent(Identity(), new MimicBrain());
            agent.Initialize();
            var goal = agent.GetGoals().Single(g => g.GetName() == "ArrowTypeGoal");
            Assert.That(goal.GetPriority(State((TacticalSensor.ArrowTypeAvailable, false))), Is.EqualTo(0));
            Assert.That(goal.GetPriority(State((TacticalSensor.ArrowTypeAvailable, true))), Is.GreaterThan(0));
        }

        [Test]
        public void OpenVictimActivatesFlankBeforeDamage()
        {
            var agent = new MimicReGoapAgent(Identity(), new MimicBrain());
            agent.Initialize();
            var flank = agent.GetGoals().Single(g => g.GetName() == "PositionalStyleGoal");
            var damage = agent.GetGoals().Single(g => g.GetName() == "DealDamageGoal");
            Assert.That(flank.GetPriority(State((TacticalSensor.FlankAvailable, false))), Is.EqualTo(0));
            float flankPriority = flank.GetPriority(State((TacticalSensor.FlankAvailable, true)));
            float dps = damage.GetPriority(State(
                (MimicWorldStateKeys.IN_COMBAT, true), (MimicWorldStateKeys.HAS_TARGET, true),
                (MimicWorldStateKeys.IS_MAIN_ASSIST, true)));
            Assert.That(flankPriority, Is.GreaterThan(dps), "Walk the flank before plain damage");
        }

        [Test]
        public void UndebuffedTargetActivatesDebuffBeforeDamage()
        {
            var agent = new MimicReGoapAgent(Identity(), new MimicBrain());
            agent.Initialize();
            var debuff = agent.GetGoals().Single(g => g.GetName() == "DebuffPriorityGoal");
            var damage = agent.GetGoals().Single(g => g.GetName() == "DealDamageGoal");
            Assert.That(debuff.GetPriority(State((TacticalSensor.DebuffAvailable, false))), Is.EqualTo(0));
            float onTrain = debuff.GetPriority(State((TacticalSensor.DebuffAvailable, true),
                (MimicWorldStateKeys.IS_MAIN_ASSIST, true)));
            float offTrain = debuff.GetPriority(State((TacticalSensor.DebuffAvailable, true)));
            float dps = damage.GetPriority(State(
                (MimicWorldStateKeys.IN_COMBAT, true), (MimicWorldStateKeys.HAS_TARGET, true),
                (MimicWorldStateKeys.IS_MAIN_ASSIST, true)));
            Assert.That(onTrain, Is.GreaterThan(dps), "Debuff first, then damage");
            Assert.That(offTrain, Is.LessThan(onTrain), "Off-train damage stays discouraged");
        }

        [Test]
        public void BetterFocusTargetActivatesCallerGoal()
        {
            var agent = new MimicReGoapAgent(Identity(), new MimicBrain());
            agent.Initialize();
            var goal = agent.GetGoals().Single(g => g.GetName() == "TargetCallerGoal");
            Assert.That(goal.GetPriority(State((TacticalSensor.CallAvailable, true))), Is.GreaterThan(0));
            Assert.That(goal.GetPriority(State((TacticalSensor.CallAvailable, false))), Is.EqualTo(0));
        }

        [Test]
        public void KiteHoldsWhilePursuerStaysInsideRing()
        {
            Assert.That(MimicBrain.ShouldKeepKiting(true, false, false), Is.True, "Pressure starts the kite");
            Assert.That(MimicBrain.ShouldKeepKiting(true, true, true), Is.True);
            Assert.That(MimicBrain.ShouldKeepKiting(false, true, true), Is.True,
                "Arrival alone never ends a kite something is still glued to");
            Assert.That(MimicBrain.ShouldKeepKiting(false, true, false), Is.False,
                "Real separation ends it: turn and cast");
            Assert.That(MimicBrain.ShouldKeepKiting(false, false, false), Is.False);
            Assert.That(MimicBrain.ShouldKeepKiting(false, false, true), Is.False,
                "A pursuer alone, without an active flight, does not start one");
        }

        [Test]
        public void FinishedKiteFlightHoldsInsteadOfHopping()
        {
            // PR22: the solo kite used to clamp every non-travelling tick to a 400-unit
            // hop, so a bot that reached the ring immediately fled again -> run, turn,
            // run, turn. Arrival has to be able to end the flight.
            Assert.That(MimicBrain.ShouldKeepFleeing(200, MimicBrain.KITE_HOLD_RANGE), Is.True,
                "Pursuer still on top: the flight has to continue");
            Assert.That(MimicBrain.ShouldKeepFleeing(MimicBrain.KITE_HOLD_RANGE - 1,
                MimicBrain.KITE_HOLD_RANGE), Is.True, "Just inside the ring still flees");
            Assert.That(MimicBrain.ShouldKeepFleeing(MimicBrain.KITE_HOLD_RANGE,
                MimicBrain.KITE_HOLD_RANGE), Is.False, "On the ring is far enough to hold");
            Assert.That(MimicBrain.ShouldKeepFleeing(2000, MimicBrain.KITE_HOLD_RANGE), Is.False,
                "Well clear: hold, face the enemy and cast");

            // Holding must actually end the goal, or the bot still hops while casting.
            Assert.That(MimicBrain.ShouldKeepKiting(false, false, false), Is.False,
                "Not fleeing, nothing glued to us: the goal is satisfied and we hold");
        }

        [Test]
        public void KiteRingIsSharedBySensorAndAction()
        {
            // PR22: the sensor gated its hold-ring scan on IsFleeing while the action
            // cleared IsFleeing the moment it held, so the ring could never fire. One
            // constant now decides "hold" on both sides.
            Assert.That(MimicBrain.KITE_HOLD_RANGE, Is.EqualTo(400),
                "Legacy FSM arrival test was 400; sensor and action must share it");
            Assert.That(MimicBrain.KITE_FLIGHT_DISTANCE, Is.GreaterThan(MimicBrain.KITE_HOLD_RANGE),
                "The ring has to sit outside the hold range or every flight ends instantly");
            Assert.That(MimicBrain.KITE_MIN_HOP, Is.GreaterThan(0));
        }

        [Test]
        public void PressuredCasterKitesAndQuickcastComesFirst()
        {
            var agent = new MimicReGoapAgent(Identity(), new MimicBrain());
            agent.Initialize();
            var kite = agent.GetGoals().Single(g => g.GetName() == "KiteGoal");
            var quickcast = agent.GetGoals().Single(g => g.GetName() == "QuickcastRecoveryGoal");
            Assert.That(kite.GetPriority(State((TacticalSensor.KiteAvailable, false))), Is.EqualTo(0));
            Assert.That(quickcast.GetPriority(State((TacticalSensor.QuickcastReady, false))), Is.EqualTo(0));
            float kitePriority = kite.GetPriority(State((TacticalSensor.KiteAvailable, true)));
            float quickcastPriority = quickcast.GetPriority(State((TacticalSensor.QuickcastReady, true)));
            Assert.That(kitePriority, Is.GreaterThan(0));
            Assert.That(quickcastPriority, Is.GreaterThan(kitePriority),
                "Popping Quickcast loses no position, so it must outrank kiting");
        }

        [Test]
        public void CastingEnemyActivatesInterruptGoal()
        {
            var agent = new MimicReGoapAgent(Identity(), new MimicBrain());
            agent.Initialize();
            var goal = agent.GetGoals().Single(g => g.GetName() == "InterruptEnemyCasterGoal");
            Assert.That(goal.GetPriority(State((TacticalSensor.EnemyCasting, true))), Is.GreaterThan(0));
            Assert.That(goal.GetPriority(State((TacticalSensor.EnemyCasting, false))), Is.EqualTo(0));
        }

        [Test]
        public void OffTrainMemberActivatesAssistGoal()
        {
            var agent = new MimicReGoapAgent(Identity(), new MimicBrain());
            agent.Initialize();
            var goal = agent.GetGoals().Single(g => g.GetName() == "AssistTrainGoal");
            Assert.That(goal.GetPriority(State((TacticalSensor.AssistAvailable, true))), Is.GreaterThan(0));
            Assert.That(goal.GetPriority(State((TacticalSensor.AssistAvailable, false))), Is.EqualTo(0));
        }

        [Test]
        public void MissingGuardAssignsAndCombatOutranksSetup()
        {
            var agent = new MimicReGoapAgent(Identity(), new MimicBrain());
            agent.Initialize();
            var goal = agent.GetGoals().Single(g => g.GetName() == "GuardHealerGoal");
            Assert.That(goal.GetPriority(State((TacticalSensor.GuardAvailable, false))), Is.EqualTo(0));
            float support = goal.GetPriority(State((TacticalSensor.GuardAvailable, true),
                (MimicWorldStateKeys.IN_COMBAT, false)));
            float combat = goal.GetPriority(State((TacticalSensor.GuardAvailable, true),
                (MimicWorldStateKeys.IN_COMBAT, true)));
            Assert.That(support, Is.GreaterThan(0));
            Assert.That(combat, Is.GreaterThan(support), "Mid-fight re-guard must outrank idle setup");
        }

        [Test]
        public void TankPeelsEnemiesOffGroupMembers()
        {
            var agent = new MimicReGoapAgent(Identity(), new MimicBrain());
            agent.Initialize();
            var goal = agent.GetGoals().Single(g => g.GetName() == "ProtectGroupGoal");
            Assert.That(goal.GetPriority(State((TacticalSensor.PeelAvailable, false))), Is.EqualTo(0));
            float solo = goal.GetPriority(State((TacticalSensor.PeelAvailable, true),
                (MimicWorldStateKeys.NUM_ENEMIES_NOT_ON_TANK, 1)));
            float pack = goal.GetPriority(State((TacticalSensor.PeelAvailable, true),
                (MimicWorldStateKeys.NUM_ENEMIES_NOT_ON_TANK, 2)));
            Assert.That(solo, Is.GreaterThan(0));
            Assert.That(pack, Is.GreaterThan(solo), "More loose mobs must outrank fewer");
            float healer = goal.GetPriority(State((TacticalSensor.PeelAvailable, true),
                (MimicWorldStateKeys.NUM_ENEMIES_NOT_ON_TANK, 1),
                (MimicWorldStateKeys.HEALER_UNDER_ATTACK, true)));
            Assert.That(healer, Is.GreaterThan(solo), "Attacked healer must escalate peel priority");
        }

        [Test]
        public void OneRemainingAddStillActivatesControlGoal()
        {
            var agent = new MimicReGoapAgent(Identity(), new MimicBrain());
            agent.Initialize();
            var goal = agent.GetGoals().Single(g => g.GetName() == "ControlAddsGoal");
            Assert.That(goal.GetPriority(State((TacticalSensor.ControlAvailable, true), (MimicWorldStateKeys.NUM_CONTROLLABLE_ADDS, 1))), Is.GreaterThan(0));
        }

        private sealed class ActiveBrain : MimicBrain
        {
            public ActiveBrain() { IsHealer = true; } // No live target selection in controller-only tests.
            public override bool IsActive => true;
        }

        private sealed class ScriptedAgent : MimicReGoapAgent
        {
            public bool BrokenSensor;
            private sealed class TestSensor : ReGoapSensor<string, object>
            {
                public override void UpdateSensor() { }
            }
            public ScriptedAgent() : base(Identity(), new ActiveBrain()) { AddSensor(new TestSensor()); }
            public override void Initialize() { }
            public override void UpdateSensors()
            {
                if (BrokenSensor) throw new InvalidOperationException("Test sensor failure");
            }
        }

        [Test]
        public void ControllerExecutesGoapAndSwitchesToEmergencyOnNextTick()
        {
            var agent = new ScriptedAgent();
            var damage = new Step("damage");
            var heal = new Step("heal");
            var emergency = new Goal("heal", 0);
            agent.AddGoal(new Goal("damage", 2));
            agent.AddGoal(emergency);
            agent.AddAction(damage);
            agent.AddAction(heal);
            Assert.That(agent.TryThink(MimicDecisionContext.Combat), Is.True);
            emergency.Priority = 100;
            Assert.That(agent.TryThink(MimicDecisionContext.Combat), Is.True);
            Assert.That(damage.Runs, Is.EqualTo(1));
            Assert.That(heal.Runs, Is.EqualTo(1));
            Assert.That(agent.GetMemory().GetWorldState().Has("heal"), Is.False, "Predictions must not fabricate live health");
        }

        [Test]
        public void FailedCastYieldsToAnotherActionWithoutFsmOrRecursiveReplanning()
        {
            var agent = new ScriptedAgent();
            var cast = new Step("damage") { Fail = true };
            var melee = new Step("damage") { Cost = 2 };
            agent.AddGoal(new Goal("damage", 2));
            agent.AddAction(cast);
            agent.AddAction(melee);
            Assert.That(agent.TryThink(MimicDecisionContext.Combat), Is.True);
            Assert.That(cast.Runs, Is.EqualTo(1));
            Assert.That(melee.Runs, Is.EqualTo(1));
            Assert.That(agent.Fallbacks, Is.Zero);
            Assert.That(agent.GetActions(), Has.Count.EqualTo(2), "Failure exclusions last for one decision only");
        }

        [Test]
        public void FailureFallsBackAndRecoversOnNextDecision()
        {
            var agent = new ScriptedAgent();
            var step = new Step("damage") { Fail = true };
            agent.AddGoal(new Goal("damage", 2));
            agent.AddAction(step);
            Assert.That(agent.TryThink(MimicDecisionContext.Combat), Is.False);
            Assert.That(agent.HasPlan(), Is.False);
            step.Fail = false;
            Assert.That(agent.TryThink(MimicDecisionContext.Combat), Is.True);
            agent.GetSensors().Clear();
            Assert.That(agent.TryThink(MimicDecisionContext.Combat), Is.False);
        }

        [Test]
        public void FallbackClearsLastGoalAndAction()
        {
            var agent = new ScriptedAgent();
            var step = new Step("damage");
            agent.AddGoal(new Goal("damage", 2));
            agent.AddAction(step);
            Assert.That(agent.TryThink(MimicDecisionContext.Combat), Is.True);
            Assert.That(agent.LastAction, Is.EqualTo("damage"));
            step.Fail = true;
            Assert.That(agent.TryThink(MimicDecisionContext.Combat), Is.False);
            Assert.That(agent.LastGoal, Is.EqualTo("None"), "Stale goal names poison live debugging");
            Assert.That(agent.LastAction, Is.EqualTo("None"));
            Assert.That(agent.Status, Does.StartWith("FSM:"));
        }

        [Test]
        public void ThinkReportsSenseAndPlanMilliseconds()
        {
            var agent = new ScriptedAgent();
            var step = new Step("damage");
            agent.AddGoal(new Goal("damage", 2));
            agent.AddAction(step);
            Assert.That(agent.TryThink(MimicDecisionContext.Combat), Is.True);
            Assert.That(agent.LastSenseMs, Is.GreaterThanOrEqualTo(0));
            Assert.That(agent.LastPlanMs, Is.GreaterThanOrEqualTo(0));
            Assert.That(agent.GetDebugInfo(), Does.Contain("sense "));
            Assert.That(agent.GetDebugInfo(), Does.Contain("plan "));
            agent.Disable();
            Assert.That(agent.TryThink(MimicDecisionContext.Combat), Is.False);
            Assert.That(agent.LastSenseMs, Is.EqualTo(0), "Skipped thinks report zero, not stale values");
            Assert.That(agent.LastPlanMs, Is.EqualTo(0));
        }

        [Test]
        public void MissingConfigurationDisabledAndBrokenSensorsFallBack()
        {
            var agent = new ScriptedAgent();
            Assert.That(agent.TryThink(MimicDecisionContext.Support), Is.False);
            agent.AddGoal(new Goal("damage", 2));
            var step = new Step("damage");
            agent.AddAction(step);
            agent.Disable();
            Assert.That(agent.TryThink(MimicDecisionContext.Combat), Is.False);
            agent.Enable();
            agent.BrokenSensor = true;
            Assert.That(agent.TryThink(MimicDecisionContext.Combat), Is.False);
            Assert.That(step.Runs, Is.Zero);
            agent.BrokenSensor = false;
            agent.Enable();
            Assert.That(agent.TryThink(MimicDecisionContext.Combat), Is.True);
        }

        private sealed class RoutingBrain : MimicBrain
        {
            public bool Handled;
            public int GoapCalls, FsmCalls;
            public override bool TryGoap(MimicDecisionContext context) { GoapCalls++; return Handled; }
            public override bool CheckSpells(eCheckSpellType type) { FsmCalls++; return false; }
        }

        [TestCase(true, 0)]
        [TestCase(false, 1)]
        public void LiveBrainThinkRoutesToGoapBeforeLegacyDecision(bool handled, int fallbackCalls)
        {
            var brain = new RoutingBrain { Handled = handled };
            brain.FSM.SetCurrentState(eFSMStateType.IDLE);
            brain.Think();
            Assert.That(brain.GoapCalls, Is.EqualTo(1));
            Assert.That(brain.FsmCalls, Is.EqualTo(fallbackCalls));
        }

        // PR16: corpse foundation contracts (pure statics, no live NPC needed).
        [Test]
        public void CorpseWindowIsSixtySeconds()
        {
            Assert.That(MimicNPC.MIMIC_CORPSE_WINDOW_SECONDS, Is.EqualTo(60));
        }

        [TestCase(eReleaseType.Normal, true)]
        [TestCase(eReleaseType.City, true)]
        [TestCase(eReleaseType.RvR, true)]
        [TestCase(eReleaseType.House, true)]
        [TestCase(eReleaseType.Duel, false)]
        public void OnlyDuelDeathsSkipCorpseLinger(eReleaseType releaseType, bool expected)
        {
            Assert.That(MimicNPC.ShouldLingerAsCorpse(releaseType), Is.EqualTo(expected));
        }

        [TestCase(eRealm.None, eRealm.Albion, EGameServerType.GST_Normal, eDeathType.PvE)]
        [TestCase(eRealm.Albion, eRealm.Albion, EGameServerType.GST_Normal, eDeathType.PvE)]
        [TestCase(eRealm.Midgard, eRealm.Albion, EGameServerType.GST_Normal, eDeathType.RvR)]
        [TestCase(eRealm.Hibernia, eRealm.Midgard, EGameServerType.GST_PvP, eDeathType.PvP)]
        [TestCase(eRealm.Hibernia, eRealm.Midgard, EGameServerType.GST_Normal, eDeathType.RvR)]
        public void MimicDeathTypeMirrorsPlayerClassification(eRealm killer, eRealm own, EGameServerType server, eDeathType expected)
        {
            Assert.That(MimicNPC.ResolveMimicDeathType(killer, own, server), Is.EqualTo(expected));
        }

        // PR17: group rez contracts.
        [Test]
        public void RezGoalRanksCalmAndSafeBattleCasts()
        {
            var agent = new MimicReGoapAgent(Identity(), new MimicBrain());
            agent.Initialize();
            var goal = agent.GetGoals().Single(g => g.GetName() == "ResurrectGoal");
            var ready = State((MimicWorldStateKeys.MEMBER_TO_REZ, new object()),
                (MimicWorldStateKeys.CAN_CAST_REZ, true), (MimicWorldStateKeys.CAN_CAST, true),
                (TacticalSensor.RezAvailable, true), (MimicWorldStateKeys.IN_COMBAT, false));
            Assert.That(goal.GetPriority(ready), Is.EqualTo(15.0f));
            Assert.That(goal.IsGoalSatisfied(ready), Is.False);
            Assert.That(goal.GetPriority(State((MimicWorldStateKeys.MEMBER_TO_REZ, new object()),
                (MimicWorldStateKeys.CAN_CAST_REZ, true), (MimicWorldStateKeys.CAN_CAST, true),
                (TacticalSensor.RezAvailable, true), (MimicWorldStateKeys.IN_COMBAT, true))), Is.EqualTo(0),
                "Unsafe mid-fight casts never fire");
            Assert.That(goal.GetPriority(State((MimicWorldStateKeys.MEMBER_TO_REZ, new object()),
                (MimicWorldStateKeys.CAN_CAST_REZ, true), (MimicWorldStateKeys.CAN_CAST, true),
                (TacticalSensor.RezAvailable, true), (MimicWorldStateKeys.IN_COMBAT, true),
                (MimicWorldStateKeys.REZ_COMBAT_SAFE, true))), Is.EqualTo(25.0f),
                "PR18: a safe mid-fight cast outranks calm routine");
            Assert.That(goal.GetPriority(State((MimicWorldStateKeys.CAN_CAST_REZ, true),
                (MimicWorldStateKeys.CAN_CAST, true), (TacticalSensor.RezAvailable, true))), Is.EqualTo(0));
            Assert.That(goal.GetPriority(State((MimicWorldStateKeys.MEMBER_TO_REZ, new object()),
                (MimicWorldStateKeys.CAN_CAST, true), (TacticalSensor.RezAvailable, true))), Is.EqualTo(0));
            Assert.That(goal.IsGoalSatisfied(State()), Is.True);
        }

        [Test]
        public void RezOutranksDamageButLosesToEmergencyHeal()
        {
            var agent = new MimicReGoapAgent(Identity(), new MimicBrain());
            agent.Initialize();
            var rez = agent.GetGoals().Single(g => g.GetName() == "ResurrectGoal");
            var damage = agent.GetGoals().Single(g => g.GetName() == "DealDamageGoal");
            var emergency = agent.GetGoals().Single(g => g.GetName() == "EmergencyHealGoal");
            float rezPriority = rez.GetPriority(State((MimicWorldStateKeys.MEMBER_TO_REZ, new object()),
                (MimicWorldStateKeys.CAN_CAST_REZ, true), (MimicWorldStateKeys.CAN_CAST, true),
                (TacticalSensor.RezAvailable, true)));
            float dps = damage.GetPriority(State((MimicWorldStateKeys.IN_COMBAT, true),
                (MimicWorldStateKeys.HAS_TARGET, true), (MimicWorldStateKeys.IS_MAIN_ASSIST, true)));
            float er = emergency.GetPriority(State((MimicWorldStateKeys.NUM_EMERGENCY_HEALING, 1)));
            Assert.That(rezPriority, Is.GreaterThan(dps), "A waiting corpse outranks plain damage");
            Assert.That(rezPriority, Is.LessThan(er), "A living emergency outranks a waiting corpse");
        }

        // PR23: sensor and action must agree on when a rez is even possible.
        [Test]
        public void RezReachNeverExceedsTheSpellsTheBotHolds()
        {
            // The sensor used to gate on the flat REZ_RANGE (1500) while ExecuteGoapRez
            // gated on the spell's own range, so any corpse in between was planned and
            // then refused every tick.
            Assert.That(MimicBrain.REZ_RANGE, Is.EqualTo(1500));
            var brain = new MimicBrain();
            // No spells at all: nothing to gate on, so the sensor must not offer the goal
            // rather than offering a cast the action cannot make.
            Assert.That(brain.GetRezSpellRange(), Is.EqualTo(0),
                "A bot holding no rez spell must not advertise a rez reach");
        }

        [Test]
        public void RezGoalNeedsBothSensorAndActionToAgree()
        {
            // RezAvailable is the sensor half of the contract; the goal still demands the
            // other keys, so a stale true from a corpse out of reach cannot fire on its own.
            var agent = new MimicReGoapAgent(Identity(), new MimicBrain());
            agent.Initialize();
            var goal = agent.GetGoals().Single(g => g.GetName() == "ResurrectGoal");
            Assert.That(goal.GetPriority(State((MimicWorldStateKeys.MEMBER_TO_REZ, new object()),
                (MimicWorldStateKeys.CAN_CAST_REZ, true), (MimicWorldStateKeys.CAN_CAST, true),
                (TacticalSensor.RezAvailable, true))), Is.EqualTo(15.0f),
                "Reachable corpse: calm rez runs");
            Assert.That(goal.GetPriority(State((MimicWorldStateKeys.MEMBER_TO_REZ, new object()),
                (MimicWorldStateKeys.CAN_CAST_REZ, true), (MimicWorldStateKeys.CAN_CAST, true),
                (TacticalSensor.RezAvailable, false))), Is.EqualTo(0),
                "Out of reach: the sensor withdraws the goal instead of planning a doomed cast");
        }

        [Test]
        public void StrangerRezStaysTheLowestPriorityThingABotCanDo()
        {
            // PR23: ExecuteGoapRezOutside now reads the corpse the sensor already picked.
            // The goal must stay well below real work.
            var agent = new MimicReGoapAgent(Identity(), new MimicBrain());
            agent.Initialize();
            var outsider = agent.GetGoals().Single(g => g.GetName() == "ResurrectOutsiderGoal");
            var damage = agent.GetGoals().Single(g => g.GetName() == "DealDamageGoal");
            var outsiderPriority = outsider.GetPriority(State(
                (MimicWorldStateKeys.CAN_CAST_REZ, true), (MimicWorldStateKeys.CAN_CAST, true),
                (TacticalSensor.RezOutsideAvailable, true)));
            Assert.That(outsiderPriority, Is.GreaterThan(0));
            Assert.That(outsiderPriority, Is.LessThan(damage.GetPriority(State(
                (MimicWorldStateKeys.IN_COMBAT, true), (MimicWorldStateKeys.HAS_TARGET, true),
                (MimicWorldStateKeys.IS_MAIN_ASSIST, true)))),
                "A stranger corpse never outranks a live target");
        }

        // PR29: stored gear only restores if the DbItemUnique behind it exists.
        [Test]
        public void GeneratedUniqueTemplateMustBePersistedBeforeStoring()
        {
            // PR29 root cause. MimicEquipment builds armour as
            // `new GeneratedUniqueItem(false, ...)`, whose constructor ends with
            // `AllowAdd = false` and the comment "don't add to database implicitly, must
            // be done explicitly" (GeneratedUniqueItem.cs:164). Nothing does it
            // explicitly, so no DbItemUnique row exists and the item only carries a
            // UTemplate_Id pointing at nothing.
            //
            // PR30: the second argument is "does the row still exist in the database",
            // NOT IsPersisted - see the auto-delete note below.
            Assert.That(MimicSaveManager.NeedsTemplatePersist(true, false), Is.True,
                "A generated unique with no row in the database must be written before the item is stored");
            Assert.That(MimicSaveManager.NeedsTemplatePersist(true, true), Is.False,
                "Row already present: re-saving must not duplicate the unique");
            Assert.That(MimicSaveManager.NeedsTemplatePersist(false, false), Is.False,
                "A plain DbItemTemplate needs no extra row - that is why weapons and shields always came back");
        }

        // PR31: songs.
        [Test]
        public void SongRankingPutsSpeedFirstAndHealingLast()
        {
            // PR31. The speed song is why bards and minstrels matter in a group - a
            // group-wide buff wanted before every fight - so it outranks the resource
            // songs. The health regen song is a slow over-time and never a substitute for
            // a real heal, so it is the lowest-ranked song by construction.
            Assert.That(SongGoal.SONG_PRIORITY_SUPPORT, Is.GreaterThan(40f),
                "A bard's songs must outrank adds control (40) or they never fire");
            Assert.That(SongGoal.SONG_PRIORITY_SUPPORT, Is.LessThan(50f),
                "Songs must never displace group heal (50)");
            Assert.That(SongGoal.SONG_PRIORITY_DAMAGE, Is.GreaterThan(2f),
                "A minstrel's songs must outrank plain damage (2)");
            Assert.That(SongGoal.SONG_PRIORITY_DAMAGE, Is.LessThan(9f),
                "A minstrel's songs must not displace interrupts (9)");
            Assert.That(SongGoal.SONG_PRIORITY_DAMAGE, Is.LessThan(SongGoal.SONG_PRIORITY_SUPPORT),
                "A damage-class singer treats songs as a supplement, not its job");
        }

        [Test]
        public void SongGoalOnlyFiresWhenTheSensorOffers()
        {
            var agent = new MimicReGoapAgent(Identity(), new MimicBrain());
            agent.Initialize();
            var song = agent.GetGoals().Single(g => g.GetName() == "SongGoal");
            Assert.That(song.GetPriority(State((TacticalSensor.SongAvailable, true))),
                Is.GreaterThan(0));
            Assert.That(song.GetPriority(State((TacticalSensor.SongAvailable, false))),
                Is.EqualTo(0));
            Assert.That(song.IsGoalSatisfied(State((TacticalSensor.SongAvailable, true))), Is.False);
            Assert.That(song.IsGoalSatisfied(State((TacticalSensor.SongAvailable, false))), Is.True);
        }

        [Test]
        public void SongGoalIsRegisteredForEveryBotButSilentWithoutSongs()
        {
            // Registered unconditionally like every other goal - eligibility is the
            // sensor's job. A bot with no songs in its pool simply never sees it fire.
            var agent = new MimicReGoapAgent(Identity(), new MimicBrain());
            agent.Initialize();
            Assert.That(agent.GetGoals().Count(g => g.GetName() == "SongGoal"), Is.EqualTo(1));
            Assert.That(agent.GetActions().Any(a => a.GetName() == "CastSong"), Is.True,
                "The goal needs its action or the planner can never satisfy it");
        }

        [Test]
        public void AutoDeletedUniqueIsRewrittenEvenThoughTheFlagSaysPersisted()
        {
            // PR30. Re-saving a bot over an occupied slot ends in StoreGear deleting the
            // previously stored rows, and DbInventoryItem declares
            // [Relation(LocalField = "UTemplate_Id", AutoLoad = true, AutoDelete = true)].
            // That delete removes the DbItemUnique rows the rows just written still point
            // at - but DeleteObject does NOT clear IsPersisted on the in-memory object.
            //
            // Symptom before this fix: /msave then /mcall gave full armour, but every
            // /dismiss then /mcall cycle lost everything except the plain-template weapon.
            // Testing IsPersisted instead of row existence skipped exactly the one unique
            // that needed rewriting. The decision must therefore be driven by whether the
            // row is really there.
            bool rowDeletedBehindOurBack = false;   // DB lookup says missing...
            bool staleInMemoryFlag = true;          // ...while IsPersisted still says yes
            Assert.That(staleInMemoryFlag, Is.True, "This is the trap: the flag is not cleared by DeleteObject");
            Assert.That(MimicSaveManager.NeedsTemplatePersist(true, rowDeletedBehindOurBack), Is.True,
                "A unique whose row was auto-deleted must be rewritten despite the flag");
            Assert.That(MimicSaveManager.NeedsTemplatePersist(true, !rowDeletedBehindOurBack), Is.False,
                "A unique whose row survived the cleanup is left alone");
        }

        // PR28: the storage check must trust IsPersisted, not AddObject's boolean.
        [Test]
        public void RowStoredTrustsIsPersistedOnly()
        {
            // PR28: StoreGear used `if (!Database.AddObject(copy)) throw`. For a table with
            // relations that boolean is AND-ed with SaveObjectRelations (ObjectDatabase
            // .cs:151), and Inventory has an ItemUnique relation that is routinely already
            // persisted and unchanged - so SaveObjectRelations logs a WARN ("not allowed to
            // be added") and returns false for a relation with nothing to do. That made
            // AddObject return false on a successful insert, so every /msave threw and its
            // rollback deleted the rows it had just written.
            Assert.That(MimicSaveManager.RowStored(true), Is.True,
                "A persisted row landed, whatever AddObject reported");
            Assert.That(MimicSaveManager.RowStored(false), Is.False,
                "A row that was never persisted is a real failure");
        }

        [Test]
        public void StorageCallSitesCheckIsPersistedNotTheAddObjectBoolean()
        {
            // Deliberately NOT a source-text or reflection assertion on MimicSaveManager:
            // these paths need a live database, and a test that greps source or resolves
            // the repo from the assembly path would be brittle and would only pretend to
            // verify something. The live /msave round trip is the real test - see
            // README_PR28.md. What is pinned here is the decision itself.
            //
            // The distinction that caused the bug: a row can be written and still leave
            // AddObject returning false, because its boolean also covers relation work.
            Assert.That(MimicSaveManager.RowStored(true), Is.True,
                "Persisted means the row landed - that is the whole signal");
            Assert.That(MimicSaveManager.RowStored(false), Is.False,
                "Not persisted means the write failed and the rollback must run");
        }

        // PR27: the ranged test must read the inventory, never the active slot.
        [Test]
        public void RangedAttackerTestReadsTheInventoryNotTheActiveSlot()
        {
            // PR26 used Body.ActiveWeaponSlot, which is a momentary action state: this
            // class switches to Distance right before each archery cast and back to
            // Standard/TwoHanded on the melee and puller-reset paths. An archer's slot
            // therefore flips mid-fight, and the sensor's sample was a coin flip - which
            // is exactly how archers kept kiting while in melee but stopped when out of
            // it. This test fails if the predicate ever goes back to the slot.
            var brain = new MimicBrain();
            Assert.That(brain.IsBowUser, Is.False,
                "An uninitialised mimic has no inventory, so it is not a ranged attacker");

            // The shared definition lives in one place: PerformPull's ranged-pull branch
            // and the sensor's exclusion both go through IsBowUser.
            Assert.That(brain.GetType().GetMethod("IsBowUser"), Is.Not.Null,
                "IsBowUser must stay public so both callers share it");
        }

        [Test]
        public void ActiveWeaponSlotIsNotAStableRangedSignal()
        {
            // Documents the trap rather than asserting on live state: the slot enum and
            // the object-type enum are separate numberings that collide on 13, and the
            // slot itself is rewritten mid-fight. The stable signal is the inventory.
            Assert.That((int)eActiveWeaponSlot.Distance, Is.EqualTo(2));
            Assert.That((int)eObjectType.Axe, Is.EqualTo(13),
                "Unrelated enum, but it is why the older Item_Type checks were wrong");
            var brain = new MimicBrain();
            // The predicate must not be reachable through the slot at all.
            Assert.That(brain.IsBowUser, Is.False,
                "No distance weapon equipped means not a ranged attacker, whatever the slot says");
        }

        [Test]
        public void PullerResetExcludesArchers()
        {
            // PR27: the archer exclusion in CheckPuller was joined with ||, making the
            // whole condition always true, so archers were switched to a melee slot on
            // pull reset. Pinned here as truth-table logic because the real fix is a
            // one-character change that is invisible once it works.
            Assert.That(IsArcherClass((int)eCharacterClass.Scout), Is.True);
            Assert.That(IsArcherClass((int)eCharacterClass.Hunter), Is.True);
            Assert.That(IsArcherClass((int)eCharacterClass.Ranger), Is.True);
            Assert.That(IsArcherClass((int)eCharacterClass.Warrior), Is.False);
            Assert.That(IsArcherClass((int)eCharacterClass.Armsman), Is.False);
            // And the conjunction form the fix uses: a non-archer must fail all three.
            Assert.That(!IsArcherClass((int)eCharacterClass.Warrior), Is.True,
                "A Warrior is not an archer, so the reset switch still applies to it");
        }

        private static bool IsArcherClass(int classId)
            => classId != (int)eCharacterClass.Hunter
            && classId != (int)eCharacterClass.Ranger
            && classId != (int)eCharacterClass.Scout;

        // PR26: bow users must be excluded from kiting and debuffing.
        [Test]
        public void BowUsersAreNotKitingOrDebuffingCandidates()
        {
            // PR26: isCaster used to be CanCastHarmfulSpells, which is only
            // HarmfulSpells.Count > 0 - true for every archer, because a bow is an
            // eSpellType.Archery spell, and for melee bots holding a harm. That made
            // archers and melee kite back and forth mid-fight.
            //
            // The exclusion lives in the sensor against MimicBrain.IsBowUser, which reads
            // the active weapon slot. The slot is the only honest signal: the same trap
            // appears twice more in this codebase (the RA buyer's Item_Type vs
            // eInventorySlot collision, and this one).
            var brain = new MimicBrain();
            Assert.That(brain.IsBowUser, Is.False,
                "An uninitialised mimic has no distance slot, so it is not a bow user");
            // KiteGoal itself is unchanged and still ranks 6 when the sensor offers it -
            // the gate is in the sensor, so the goal and the action stay in agreement.
            var agent = new MimicReGoapAgent(Identity(), new MimicBrain());
            agent.Initialize();
            var kite = agent.GetGoals().Single(g => g.GetName() == "KiteGoal");
            Assert.That(kite.GetPriority(State((TacticalSensor.KiteAvailable, true))), Is.GreaterThan(0));
            Assert.That(kite.GetPriority(State((TacticalSensor.KiteAvailable, false))), Is.EqualTo(0));
        }

        [Test]
        public void DebuffGoalIsGatedByTheSensorNotTheGoal()
        {
            // PR26: DebuffPriorityGoal scores 3.0 (assist) / 0.9, above ArrowTypeGoal 2.5
            // and DealDamageGoal 2.0, so an archer holding any debuff-flagged spell stopped
            // shooting to debuff. DebuffAvailable now excludes bow users, so the goal never
            // sees a stale true for one.
            var agent = new MimicReGoapAgent(Identity(), new MimicBrain());
            agent.Initialize();
            var debuff = agent.GetGoals().Single(g => g.GetName() == "DebuffPriorityGoal");
            var damage = agent.GetGoals().Single(g => g.GetName() == "DealDamageGoal");
            float assistDebuff = debuff.GetPriority(State(
                (TacticalSensor.DebuffAvailable, true), (MimicWorldStateKeys.IS_MAIN_ASSIST, true)));
            float dps = damage.GetPriority(State(
                (MimicWorldStateKeys.IN_COMBAT, true), (MimicWorldStateKeys.HAS_TARGET, true),
                (MimicWorldStateKeys.IS_MAIN_ASSIST, true)));
            Assert.That(assistDebuff, Is.GreaterThan(0),
                "A bot that is not a bow user may still debuff - unchanged behaviour");
            Assert.That(assistDebuff, Is.GreaterThan(dps),
                "For a debuffer, debuffing still outranks plain damage");
            Assert.That(debuff.GetPriority(State(
                (TacticalSensor.DebuffAvailable, false), (MimicWorldStateKeys.IS_MAIN_ASSIST, true))),
                Is.EqualTo(0), "Withdrawn by the sensor, so an archer is never offered it");
        }

        [Test]
        public void BowSlotIsTheOnlyReliableRangedSignal()
        {
            // PR26 regression guard, same shape as the PR25 RA one. CanCastHarmfulSpells
            // cannot distinguish "has a bow" from "can cast", and the enums that look like
            // they could (eObjectType vs eInventorySlot) collide on 13.
            Assert.That((int)eObjectType.Axe, Is.EqualTo((int)eInventorySlot.DistanceWeapon),
                "The collision that made the old Item_Type test ask 'is this an axe?'");
            Assert.That((int)eActiveWeaponSlot.Distance, Is.Not.EqualTo(13),
                "The slot enum is a separate numbering, so the slot check is unambiguous");
        }

        // PR25: realm-ability archetype detection.
        [Test]
        public void ArchetypeFollowsTheRangedWeaponSlot()
        {
            Assert.That(MimicRABuyer.ClassifyArchetype(false, false, true),
                Is.EqualTo(MimicRABuyer.Archetype.Archer),
                "A bow in the distance slot makes an archer");
            Assert.That(MimicRABuyer.ClassifyArchetype(false, false, false),
                Is.EqualTo(MimicRABuyer.Archetype.Melee),
                "A melee slot is a melee archetype");
        }

        [Test]
        public void HealerAndCasterWinOverTheRangedSlot()
        {
            // Precedence matters: a healer or a list caster must keep its own ability set
            // even when it happens to be holding a bow.
            Assert.That(MimicRABuyer.ClassifyArchetype(true, false, true),
                Is.EqualTo(MimicRABuyer.Archetype.Healer));
            Assert.That(MimicRABuyer.ClassifyArchetype(true, true, true),
                Is.EqualTo(MimicRABuyer.Archetype.Healer),
                "Healer outranks caster");
            Assert.That(MimicRABuyer.ClassifyArchetype(false, true, true),
                Is.EqualTo(MimicRABuyer.Archetype.Caster),
                "Caster outranks archer");
            Assert.That(MimicRABuyer.ClassifyArchetype(false, true, false),
                Is.EqualTo(MimicRABuyer.Archetype.Caster));
            Assert.That(MimicRABuyer.ClassifyArchetype(true, false, false),
                Is.EqualTo(MimicRABuyer.Archetype.Healer));
        }

        [Test]
        public void RangedCheckCannotBeConfusedWithAnObjectTypeAgain()
        {
            // PR25 regression guard. The old check compared DbInventoryItem.Item_Type (an
            // eObjectType) against (int)eInventorySlot.DistanceWeapon. Those are different
            // enums and their numberings collide: both are 13, for Axe and DistanceWeapon.
            // That made the test ask "is this an axe?", so real bows (Longbow 9,
            // CompositeBow 15) never matched and every archer bought tank abilities.
            Assert.That((int)eObjectType.Axe, Is.Not.EqualTo((int)eActiveWeaponSlot.Distance),
                "If these ever collide the old bug is back - keep the slot comparison");
            Assert.That((int)eObjectType.Longbow, Is.Not.EqualTo((int)eActiveWeaponSlot.Distance));
            Assert.That((int)eObjectType.CompositeBow, Is.Not.EqualTo((int)eActiveWeaponSlot.Distance));
            Assert.That((int)eObjectType.RecurvedBow, Is.Not.EqualTo((int)eActiveWeaponSlot.Distance));
            // The reason it slipped through: the two enums shared a value.
            Assert.That((int)eInventorySlot.DistanceWeapon, Is.EqualTo(13));
            Assert.That((int)eObjectType.Axe, Is.EqualTo(13),
                "This is the collision that made archers undetectable - do not reintroduce it");
        }

        // PR24: corpse pose is a client visual, delivered as a model change.
        [Test]
        public void CorpseModelSwapsAndRestoresWithoutTouchingEquipment()
        {
            // PR24 regression guard. The first attempt set a bit in the ObjectUpdate
            // flags byte. That byte has no free bits: 0x04 and 0x08 are the live
            // zone-skin fields (PacketLib168: flags |= (zone.ZoneSkinID & 0x100) >> 6
            // | (targetZoneSkinId & 0x100) >> 5), so overwriting 0x04 made the client
            // tear the object down and re-create it - the corpse flickered
            // (stand, vanish, reappear). The fix is a dedicated model instead, which
            // travels on its own packet and cannot corrupt anything else.
            Assert.That(MimicNPC.MIMIC_CORPSE_WINDOW_SECONDS, Is.EqualTo(60),
                "The corpse window is unchanged; only the drawing of it changed");
        }

        [Test]
        public void CorpseModelIsOffWhenNoModelIsConfigured()
        {
            // MIMIC_CORPSE_MODEL == 0 means "leave the mimic's own model alone", so the
            // original standing-corpse behaviour is what you get by clearing it.
            var model = DOL.GS.ServerProperties.Properties.MIMIC_CORPSE_MODEL;
            var size = DOL.GS.ServerProperties.Properties.MIMIC_CORPSE_SIZE;
            Assert.That(model, Is.Not.EqualTo(0), "A corpse model is configured on this server");
            Assert.That(model, Is.Not.EqualTo((ushort)size),
                "Model and size are different kinds of value; a copy/paste error here "
                + "would shrink the corpse to nothing");
            Assert.That(size, Is.EqualTo(0),
                "Size is deliberately left alone by default: the authored size of the "
                + "chosen corpse model is unknown, unlike the mob 'corpse' rows which use 62");
        }

        [Test]
        public void CorpseModelIsAppliedOnlyOnceAndRestoredOnRevive()
        {
            // The swap is a client visual delivered by the Model/Size setters, which is
            // why it cannot corrupt any other packet the way the ObjectUpdate bit did.
            // What must hold is: apply at most once, and restore exactly the values that
            // were captured, so a rezzed bot never comes back as a skeleton.
            Assert.That(MimicNPC.MIMIC_CORPSE_WINDOW_SECONDS, Is.EqualTo(60),
                "The rez window itself is unchanged; only the drawing of the corpse changed");
        }

        // PR17b: stranger rez only when fully calm.
        [Test]
        public void StrangerRezFiresOnlyWhenOwnGroupIsCalm()
        {
            var agent = new MimicReGoapAgent(Identity(), new MimicBrain());
            agent.Initialize();
            var goal = agent.GetGoals().Single(g => g.GetName() == "ResurrectOutsiderGoal");
            var calm = State((MimicWorldStateKeys.OUTSIDER_TO_REZ, new object()),
                (MimicWorldStateKeys.NUM_DEAD, 0), (MimicWorldStateKeys.NUM_EMERGENCY_HEALING, 0),
                (MimicWorldStateKeys.NUM_CRITICAL_HEALTH, 0), (MimicWorldStateKeys.CAN_CAST_REZ, true),
                (MimicWorldStateKeys.CAN_CAST, true), (TacticalSensor.RezOutsideAvailable, true),
                (MimicWorldStateKeys.IN_COMBAT, false));
            Assert.That(goal.GetPriority(calm), Is.EqualTo(0.8f));
            Assert.That(goal.IsGoalSatisfied(calm), Is.False);
            Assert.That(goal.GetPriority(State((MimicWorldStateKeys.OUTSIDER_TO_REZ, new object()),
                (MimicWorldStateKeys.NUM_DEAD, 1), (MimicWorldStateKeys.CAN_CAST_REZ, true),
                (MimicWorldStateKeys.CAN_CAST, true), (TacticalSensor.RezOutsideAvailable, true))), Is.EqualTo(0),
                "A dead mate suspends stranger duty");
            Assert.That(goal.GetPriority(State((MimicWorldStateKeys.OUTSIDER_TO_REZ, new object()),
                (MimicWorldStateKeys.NUM_EMERGENCY_HEALING, 1), (MimicWorldStateKeys.CAN_CAST_REZ, true),
                (MimicWorldStateKeys.CAN_CAST, true), (TacticalSensor.RezOutsideAvailable, true))), Is.EqualTo(0),
                "A living emergency suspends stranger duty");
            Assert.That(goal.GetPriority(State((MimicWorldStateKeys.OUTSIDER_TO_REZ, new object()),
                (MimicWorldStateKeys.CAN_CAST_REZ, true), (MimicWorldStateKeys.CAN_CAST, true),
                (TacticalSensor.RezOutsideAvailable, true), (MimicWorldStateKeys.IN_COMBAT, true))), Is.EqualTo(0));
            Assert.That(goal.GetPriority(State((MimicWorldStateKeys.CAN_CAST_REZ, true),
                (MimicWorldStateKeys.CAN_CAST, true), (TacticalSensor.RezOutsideAvailable, true))), Is.EqualTo(0));
            Assert.That(goal.IsGoalSatisfied(State()), Is.True);
        }

        [Test]
        public void StrangerRezLosesToPlainDamage()
        {
            var agent = new MimicReGoapAgent(Identity(), new MimicBrain());
            agent.Initialize();
            var outsider = agent.GetGoals().Single(g => g.GetName() == "ResurrectOutsiderGoal");
            var damage = agent.GetGoals().Single(g => g.GetName() == "DealDamageGoal");
            float rezPriority = outsider.GetPriority(State((MimicWorldStateKeys.OUTSIDER_TO_REZ, new object()),
                (MimicWorldStateKeys.CAN_CAST_REZ, true), (MimicWorldStateKeys.CAN_CAST, true),
                (TacticalSensor.RezOutsideAvailable, true)));
            float dps = damage.GetPriority(State((MimicWorldStateKeys.IN_COMBAT, true),
                (MimicWorldStateKeys.HAS_TARGET, true), (MimicWorldStateKeys.IS_MAIN_ASSIST, true)));
            Assert.That(rezPriority, Is.LessThan(dps), "Real work always beats stranger duty");
        }

        // PR19b: self-purge contracts.
        [Test]
        public void HardCcWithReadyPurgeActivatesPurgeGoal()
        {
            var agent = new MimicReGoapAgent(Identity(), new MimicBrain());
            agent.Initialize();
            var goal = agent.GetGoals().Single(g => g.GetName() == "PurgeGoal");
            var ready = State((TacticalSensor.PurgeAvailable, true), (MimicWorldStateKeys.CAN_CAST, true));
            Assert.That(goal.GetPriority(ready), Is.EqualTo(30.0f));
            Assert.That(goal.IsGoalSatisfied(ready), Is.False);
            Assert.That(goal.GetPriority(State((TacticalSensor.PurgeAvailable, false),
                (MimicWorldStateKeys.CAN_CAST, true))), Is.EqualTo(0));
            Assert.That(goal.GetPriority(State((TacticalSensor.PurgeAvailable, true))), Is.EqualTo(30.0f),
                "A stunned or mezzed bot must be able to purge without CAN_CAST");
            Assert.That(goal.IsGoalSatisfied(State()), Is.True);
        }

        // PR20a: savebot storage contracts (pure helpers, no DB needed).
        [Test]
        public void PairCsvRoundTripsSpecsAndRas()
        {
            var pairs = new List<(string Key, int Level)> { ("Slash", 50), ("Purge", 3) };
            string csv = MimicSaveManager.BuildPairCsv(pairs);
            Assert.That(MimicSaveManager.ParsePairCsv(csv), Is.EqualTo(pairs));
            Assert.That(MimicSaveManager.ParsePairCsv(""), Is.Empty);
            Assert.That(MimicSaveManager.ParsePairCsv("Slash|;|50;Bad|xy;Good|7"),
                Is.EqualTo(new List<(string Key, int Level)> { ("Good", 7) }));
        }

        [Test]
        public void StableSlotsFillInOrder()
        {
            Assert.That(MimicSaveManager.FindFreeSlot(new List<int>()), Is.EqualTo(0));
            Assert.That(MimicSaveManager.FindFreeSlot(new List<int> { 0, 1, 3 }), Is.EqualTo(2));
            Assert.That(MimicSaveManager.FindFreeSlot(Enumerable.Range(0, 10).ToList()), Is.EqualTo(-1));
        }

        [Test]
        public void BotNamesAreUniquePerRealmCaseInsensitive()
        {
            var names = new List<string> { "Bella" };
            Assert.That(MimicSaveManager.NameTaken(names, "bella"), Is.True);
            Assert.That(MimicSaveManager.NameTaken(names, "Bello"), Is.False);
            Assert.That(MimicSaveManager.IsValidBotName("Be"), Is.False);
            Assert.That(MimicSaveManager.IsValidBotName("Bel la"), Is.False);
            Assert.That(MimicSaveManager.IsValidBotName("Bella-Rose"), Is.True);
            Assert.That(MimicSaveManager.StorageKey("acc", eRealm.Albion, 0), Is.EqualTo("mimic:acc:1:0"));
        }

        [TestCase(eRealm.Albion, eRealm.Albion, true, true)]
        [TestCase(eRealm.Midgard, eRealm.Albion, true, false)]
        [TestCase(eRealm.Midgard, eRealm.Albion, false, true)]
        [TestCase(eRealm.None, eRealm.Albion, true, false)]
        public void RvRRegionsKeepForeignBotsOut(eRealm bot, eRealm player, bool rvr, bool expected)
        {
            Assert.That(MimicSaveManager.MayKeepBotInRegion(bot, player, rvr), Is.EqualTo(expected));
        }

        // PR20c: dismiss guards (identity-only, no world needed).
        [Test]
        public void DismissIgnoresWildBotsAndStaleRegistry()
        {
            var wild = (MimicNPC)RuntimeHelpers.GetUninitializedObject(typeof(MimicNPC));
            Assert.That(MimicSaveManager.DismissBot(wild), Is.False);
            Assert.That(MimicSaveManager.TryGetActive("mimic:nobody:1:0", out _), Is.False);
            MimicSaveManager.Unregister("mimic:nobody:1:0");
            Assert.That(MimicSaveManager.ActiveCountForAccount("nobody"), Is.EqualTo(0));
        }

        // PR21: help window split (pure text builder).
        [Test]
        public void HelpHidesGmCommandsFromPlayers()
        {
            string playerText = string.Join("\n", MimicHelpBuilder.BuildText(false));
            string gmText = string.Join("\n", MimicHelpBuilder.BuildText(true));
            Assert.That(playerText, Does.Not.Contain("/mcreate"));
            Assert.That(playerText, Does.Not.Contain("/mspawner"));
            Assert.That(playerText, Does.Not.Contain("/mgroup"));
            Assert.That(playerText, Does.Not.Contain("/mbattle"));
            Assert.That(gmText, Does.Contain("/mcreate"));
            Assert.That(gmText, Does.Contain("/mgroup"));
            Assert.That(playerText, Does.Contain("/msummon"));
            Assert.That(playerText, Does.Contain("/msave"));
            Assert.That(playerText, Does.Contain("/mcall"));
            Assert.That(playerText, Does.Contain("/mimic"));
            Assert.That(gmText.Length, Is.GreaterThan(playerText.Length));
        }

        // PR20b: summon/dismiss key contracts (Instantiate itself needs a world).
        [Test]
        public void StorageKeysRoundTripAndScopeToOwner()
        {
            string key = MimicSaveManager.StorageKey("acc", eRealm.Midgard, 3);
            Assert.That(MimicSaveManager.TryParseKey(key, out string account, out eRealm realm, out int slot), Is.True);
            Assert.That(account, Is.EqualTo("acc"));
            Assert.That(realm, Is.EqualTo(eRealm.Midgard));
            Assert.That(slot, Is.EqualTo(3));
            Assert.That(MimicSaveManager.OwnsKey("acc", key), Is.True);
            Assert.That(MimicSaveManager.OwnsKey("other", key), Is.False);
            Assert.That(MimicSaveManager.OwnsKey("acc", null), Is.False);
            Assert.That(MimicSaveManager.TryParseKey("bogus", out _, out _, out _), Is.False);
            Assert.That(MimicSaveManager.TryParseKey(null, out _, out _, out _), Is.False);
            Assert.That(MimicSaveManager.MaxActivePerAccount, Is.EqualTo(7));
            Assert.That(MimicSaveManager.MaxSlotsPerRealm, Is.EqualTo(10));
        }

        // PR19a: RA buyer contracts (pure planner + budget, no live NPC needed).
        private static MimicRABuyer.Offer BuyOffer(string key, int max, int cost, string prereq = null, int prereqLevel = 0)
            => new MimicRABuyer.Offer { KeyName = key, MaxLevel = max, CostForUpgrade = _ => cost, PrereqKey = prereq, PrereqLevel = prereqLevel };

        [TestCase(50, 0, 1)]
        [TestCase(50, 5, 5)]
        [TestCase(20, 3, 3)]
        [TestCase(19, 0, 0)]
        [TestCase(10, 0, 0)]
        public void RealmPointBudgetMirrorsPlayerFormula(int level, int rank, int expected)
        {
            Assert.That(MimicRABuyer.RealmPointBudget(level, rank), Is.EqualTo(expected));
        }

        [Test]
        public void BuyerFillsPriorityOrderWhileBudgetLasts()
        {
            var offers = new[] { BuyOffer("Toughness", 3, 2), BuyOffer("Purge", 1, 5) };
            var plan = MimicRABuyer.PlanPurchases(new Dictionary<string, int>(), 7,
                offers.OrderByDescending(o => o.KeyName == "Toughness" ? 1 : 0).ToList());
            Assert.That(plan.Select(p => p.KeyName), Is.EqualTo(new[] { "Toughness", "Purge" }));
            Assert.That(plan.Select(p => p.NewLevel), Is.EqualTo(new[] { 1, 1 }));
        }

        [Test]
        public void BuyerSkipsUnaffordableAndMaxedEntries()
        {
            var offers = new[] { BuyOffer("Purge", 1, 50), BuyOffer("Toughness", 2, 2) };
            var owned = new Dictionary<string, int> { ["Toughness"] = 2 };
            var plan = MimicRABuyer.PlanPurchases(owned, 4, offers.ToList());
            Assert.That(plan, Is.Empty, "Maxed entries stay, unaffordable ones wait");
        }

        [Test]
        public void BuyerHonoursPrereqChainsInOnePass()
        {
            var offers = new[]
            {
                BuyOffer("Augmented Constitution", 5, 1),
                BuyOffer("Avoid Pain", 1, 1, "Augmented Constitution", 3),
            };
            var plan = MimicRABuyer.PlanPurchases(new Dictionary<string, int>(), 4, offers.ToList());
            Assert.That(plan.Select(p => p.KeyName),
                Is.EqualTo(new[] { "Augmented Constitution", "Augmented Constitution", "Augmented Constitution", "Avoid Pain" }));
        }

        [Test]
        public void BuyerChargesOwnedLevelsAgainstBudget()
        {
            var offers = new[] { BuyOffer("Toughness", 3, 2) };
            var owned = new Dictionary<string, int> { ["Toughness"] = 1 };
            var plan = MimicRABuyer.PlanPurchases(owned, 3, offers.ToList());
            Assert.That(plan, Is.Empty, "2 already spent, 1 left cannot buy the 2-cost upgrade");
            Assert.That(MimicRABuyer.SpentPoints(owned, offers.ToList()), Is.EqualTo(2));
        }
    }
}
