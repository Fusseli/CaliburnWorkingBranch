using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.ReGoap.Mimic.Sensors;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>Service one queued add, then re-sense. One remaining add still needs CC.</summary>
    public class ControlAddsGoal : MimicGoal
    {
        public ControlAddsGoal(MimicNPC body, MimicBrain brain) : base(body, brain) { }
        public override float GetPriority(ReGoapState<string, object> state)
            => GetBool(state, TacticalSensor.ControlAvailable)
                ? System.Math.Min(4f * GetInt(state, MimicWorldStateKeys.NUM_CONTROLLABLE_ADDS), 40f) : 0f;
        public override bool IsGoalSatisfied(ReGoapState<string, object> state) => GetPriority(state) <= 0;
        public override ReGoapState<string, object> GetGoalState()
        {
            var state = new ReGoapState<string, object>();
            state.Set(MimicWorldStateKeys.ADDS_CONTROLLED, true);
            return state;
        }
    }
}
