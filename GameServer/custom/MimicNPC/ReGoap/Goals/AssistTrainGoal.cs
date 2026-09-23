using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.ReGoap.Mimic.Sensors;
using DOL.GS.Scripts;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>Non-assist members take the main assist target for focused fire.
    /// One tick of target switching, then re-sense: success is observed as
    /// targeting the assist train. Outranks routine damage, yields to interrupts,
    /// healing, curing and crowd control.</summary>
    public sealed class AssistTrainGoal : MimicGoal
    {
        public AssistTrainGoal(MimicNPC body, MimicBrain brain) : base(body, brain) { }
        public override float GetPriority(ReGoapState<string, object> state)
            => GetBool(state, TacticalSensor.AssistAvailable) ? 4f : 0f;
        public override bool IsGoalSatisfied(ReGoapState<string, object> state)
            => !GetBool(state, TacticalSensor.AssistAvailable);
        public override ReGoapState<string, object> GetGoalState()
        {
            var state = new ReGoapState<string, object>();
            state.Set("onAssistTrain", true);
            return state;
        }
    }
}
