using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.ReGoap.Mimic.Sensors;
using DOL.GS.Scripts;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>Main assist calls the focus target: actively casting enemies first,
    /// then weakest valid targets. The train follows via AssistTrainGoal; tanks keep
    /// threat duty. Selection runs before sensing, so this goal is normally already
    /// satisfied and only fires when a better focus target appeared afterwards.</summary>
    public sealed class TargetCallerGoal : MimicGoal
    {
        public TargetCallerGoal(MimicNPC body, MimicBrain brain) : base(body, brain) { }
        public override float GetPriority(ReGoapState<string, object> state)
            => GetBool(state, TacticalSensor.CallAvailable) ? 4f : 0f;
        public override bool IsGoalSatisfied(ReGoapState<string, object> state)
            => !GetBool(state, TacticalSensor.CallAvailable);
        public override ReGoapState<string, object> GetGoalState()
        {
            var state = new ReGoapState<string, object>();
            state.Set("focusCalled", true);
            return state;
        }
    }
}
