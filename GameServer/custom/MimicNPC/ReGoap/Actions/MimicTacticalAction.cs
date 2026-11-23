using System;
using DOL.GS.ReGoap.Core;
using DOL.GS.ReGoap.Mimic.Sensors;
using DOL.GS.Scripts.ReGoap;
using DOL.AI.Brain;

namespace DOL.GS.ReGoap.Mimic.Actions
{
    /// <summary>Uses the live class-aware mechanics instead of maintaining a second
    /// copy of spell selection, power/cooldown checks, styles and healing coordination.</summary>
    public sealed class MimicTacticalAction : ReGoapAction<string, object>
    {
        private readonly Func<bool> _execute;
        private readonly float _cost;

        private MimicTacticalAction(string actionName, string requirement, string effect,
            Func<bool> execute, float cost = 1f)
        {
            name = actionName;
            preconditions.Set(requirement, true);
            effects.Set(effect, true);
            _execute = execute;
            _cost = cost;
        }

        public static void Register(MimicReGoapAgent agent)
        {
            var brain = agent.Brain;
            agent.AddAction(new MimicTacticalAction("HealOrCure", TacticalSensor.SupportAvailable,
                "targetHealed", () => brain.CheckHeals()));
            agent.AddAction(new MimicTacticalAction("MaintainBuffs", TacticalSensor.BuffsAvailable,
                MimicWorldStateKeys.BUFFS_MAINTAINED,
                () => brain.ExecuteGoapSpell(MimicBrain.eCheckSpellType.Defensive)));
            agent.AddAction(new MimicTacticalAction("ControlAdd", TacticalSensor.ControlAvailable,
                MimicWorldStateKeys.ADDS_CONTROLLED,
                () => brain.ExecuteGoapSpell(MimicBrain.eCheckSpellType.CrowdControl)));
            agent.AddAction(new MimicTacticalAction("CastOffensiveSpell", TacticalSensor.OffenseAvailable,
                "targetDamaged", () => brain.ExecuteGoapSpell(MimicBrain.eCheckSpellType.Offensive)));
            agent.AddAction(new MimicTacticalAction("ApplyDebuff", TacticalSensor.DebuffAvailable,
                "targetDebuffed", () => brain.ExecuteGoapDebuff(), 1f));
            agent.AddAction(new MimicTacticalAction("ChooseArrowType", TacticalSensor.ArrowTypeAvailable,
                "arrowTypeChosen", () => brain.ExecuteGoapArrowType(), 1f));
            agent.AddAction(new MimicTacticalAction("EngageTarget", TacticalSensor.AttackAvailable,
                "targetDamaged", () => brain.ExecuteGoapEngagement(), 2f));
            agent.AddAction(new MimicTacticalAction("MoveToFlank", TacticalSensor.FlankAvailable,
                "inOptimalPosition", () => brain.ExecuteGoapFlank(), 1f));
            agent.AddAction(new MimicTacticalAction("InterruptCaster", TacticalSensor.InterruptAvailable,
                "enemyInterrupted", () => brain.ExecuteGoapInterrupt(), 0.5f));
            agent.AddAction(new MimicTacticalAction("FollowAssist", TacticalSensor.AssistAvailable,
                "onAssistTrain", () => brain.ExecuteGoapAssist(), 0.5f));
            agent.AddAction(new MimicTacticalAction("CallFocusTarget", TacticalSensor.CallAvailable,
                "focusCalled", () => brain.ExecuteGoapCallTarget(), 0.5f));
            agent.AddAction(new MimicTacticalAction("PeelEnemy", TacticalSensor.PeelAvailable,
                "enemiesPeeled", () => brain.ExecuteGoapPeel(), 1f));
            agent.AddAction(new MimicTacticalAction("AssignGuard", TacticalSensor.GuardAvailable,
                "guardAssigned", () => brain.ExecuteGoapGuard(), 1f));
            agent.AddAction(new MimicTacticalAction("KiteToSafety", TacticalSensor.KiteAvailable,
                "atSafeDistance", () => brain.ExecuteGoapKite(), 1f));
            agent.AddAction(new MimicTacticalAction("SecureCast", TacticalSensor.QuickcastReady,
                "castSecured", () => brain.ExecuteGoapQuickcast(), 0.5f));
            agent.AddAction(new MimicTacticalAction("CastRez", TacticalSensor.RezAvailable,
                "corpseRezzed", () => brain.ExecuteGoapRez(), 1f));
            agent.AddAction(new MimicTacticalAction("CastRezOutside", TacticalSensor.RezOutsideAvailable,
                "outsiderRezzed", () => brain.ExecuteGoapRezOutside(), 1f));
            agent.AddAction(new MimicTacticalAction("UsePurge", TacticalSensor.PurgeAvailable,
                "purged", () => brain.ExecuteGoapPurge(), 1f));
        }

        public override float GetCost(IReGoapAgent<string, object> agent, ReGoapState<string, object> state)
            => _cost;

        public override bool Run(IReGoapAgent<string, object> agent,
            Action<IReGoapAction<string, object>> done, Action<IReGoapAction<string, object>> fail)
        {
            if (_execute()) done(this);
            else fail(this);
            return true;
        }
    }
}
