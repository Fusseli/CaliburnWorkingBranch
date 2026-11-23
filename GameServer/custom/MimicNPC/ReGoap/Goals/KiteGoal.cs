using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.ReGoap.Mimic.Sensors;
using DOL.GS.Scripts;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>Pressured caster gains distance to cast again: interrupted mid-cast
    /// or a live enemy sitting in melee on us. Casters only; melee stands its ground.
    /// One tick of flight per decision, then re-sense: arrival or safety ends it.</summary>
    public sealed class KiteGoal : MimicGoal
    {
        public KiteGoal(MimicNPC body, MimicBrain brain) : base(body, brain) { }
        public override float GetPriority(ReGoapState<string, object> state)
            => GetBool(state, TacticalSensor.KiteAvailable) ? 6f : 0f;
        public override bool IsGoalSatisfied(ReGoapState<string, object> state)
            => !GetBool(state, TacticalSensor.KiteAvailable);
        public override ReGoapState<string, object> GetGoalState()
        {
            var state = new ReGoapState<string, object>();
            state.Set("atSafeDistance", true);
            return state;
        }
    }
}
