using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.ReGoap.Mimic.Sensors;
using DOL.GS.Scripts;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>Walk the flank for positional styles: melee weapon, attack range,
    /// victim standing still and busy elsewhere. Same gates as the FSM flank loop,
    /// so GOAP and fallback never disagree about when flanking pays. Outranks plain
    /// damage, yields to debuffs, assist discipline and everything support.</summary>
    public sealed class PositionalStyleGoal : MimicGoal
    {
        public PositionalStyleGoal(MimicNPC body, MimicBrain brain) : base(body, brain) { }
        public override float GetPriority(ReGoapState<string, object> state)
            => GetBool(state, TacticalSensor.FlankAvailable) ? 3.5f : 0f;
        public override bool IsGoalSatisfied(ReGoapState<string, object> state)
            => !GetBool(state, TacticalSensor.FlankAvailable);
        public override ReGoapState<string, object> GetGoalState()
        {
            var state = new ReGoapState<string, object>();
            state.Set("inOptimalPosition", true);
            return state;
        }
    }
}
