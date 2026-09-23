using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>Plan a damage/engagement step, not an impossible boolean accumulation
    /// from targetDamaged to targetDead. Health and target validity are sensed each tick.</summary>
    public class DealDamageGoal : MimicGoal
    {
        public DealDamageGoal(MimicNPC body, MimicBrain brain) : base(body, brain) { }
        public override float GetPriority(ReGoapState<string, object> state)
        {
            if (!IsInCombat(state) || !HasTarget(state)) return 0;
            return GetBool(state, MimicWorldStateKeys.IS_MAIN_ASSIST) || IsTargetingMainAssistTarget(state) ? 2f : 0.6f;
        }
        public override bool IsGoalSatisfied(ReGoapState<string, object> state) => GetPriority(state) <= 0;
        public override ReGoapState<string, object> GetGoalState()
        {
            var state = new ReGoapState<string, object>();
            state.Set("targetDamaged", true);
            return state;
        }
    }
}
