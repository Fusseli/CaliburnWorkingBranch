using System;
using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>Includes self-healing while solo. Request incremental healing and
    /// let the next sensor update decide whether more is necessary.</summary>
    public class HealGroupGoal : MimicGoal
    {
        public HealGroupGoal(MimicNPC body, MimicBrain brain) : base(body, brain) { }
        public override float GetPriority(ReGoapState<string, object> state)
        {
            if (GetNumInjured(state) == 0) return 0;
            float deficit = GetFloat(state, MimicWorldStateKeys.AVG_HEALTH_DEFICIT_PERCENT);
            return Math.Clamp(5f + deficit, 5f, 50f);
        }
        public override bool IsGoalSatisfied(ReGoapState<string, object> state) => GetNumInjured(state) == 0;
        public override ReGoapState<string, object> GetGoalState()
        {
            var state = new ReGoapState<string, object>();
            state.Set("targetHealed", true);
            return state;
        }
    }
}
