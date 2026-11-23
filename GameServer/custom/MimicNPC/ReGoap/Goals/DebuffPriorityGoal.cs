using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.ReGoap.Mimic.Sensors;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>Debuff before damage: a valid target missing any major debuff while we
    /// hold debuff spells. Outranks routine damage, yields to interrupts, assist
    /// discipline and everything support. Off-train damage is discouraged as usual.</summary>
    public class DebuffPriorityGoal : MimicGoal
    {
        public DebuffPriorityGoal(MimicNPC body, MimicBrain brain) : base(body, brain) { }
        public override float GetPriority(ReGoapState<string, object> state)
        {
            if (!GetBool(state, TacticalSensor.DebuffAvailable)) return 0f;
            return GetBool(state, MimicWorldStateKeys.IS_MAIN_ASSIST) || IsTargetingMainAssistTarget(state) ? 3f : 0.9f;
        }
        public override bool IsGoalSatisfied(ReGoapState<string, object> state) => GetPriority(state) <= 0;
        public override ReGoapState<string, object> GetGoalState()
        {
            var state = new ReGoapState<string, object>();
            state.Set("targetDebuffed", true);
            return state;
        }
    }
}
