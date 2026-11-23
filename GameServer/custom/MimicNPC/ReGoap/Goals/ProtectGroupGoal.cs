using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.ReGoap.Mimic.Sensors;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>Main tank takes aggroed enemies off attacked group members.
    /// One tick of engagement on a peel candidate, then re-sense. CC'd enemies
    /// are never peel candidates so the tank cannot break its own group's control.
    /// Outranks routine damage and assist train, yields to interrupts, healing,
    /// curing and crowd control.</summary>
    public sealed class ProtectGroupGoal : MimicGoal
    {
        public ProtectGroupGoal(MimicNPC body, MimicBrain brain) : base(body, brain) { }
        public override float GetPriority(ReGoapState<string, object> state)
        {
            if (!GetBool(state, TacticalSensor.PeelAvailable)) return 0f;
            float priority = System.Math.Min(6f + 3f * GetInt(state, MimicWorldStateKeys.NUM_ENEMIES_NOT_ON_TANK), 30f);
            if (GetBool(state, MimicWorldStateKeys.HEALER_UNDER_ATTACK)) priority *= 2f;
            return priority;
        }
        public override bool IsGoalSatisfied(ReGoapState<string, object> state)
            => !GetBool(state, TacticalSensor.PeelAvailable);
        public override ReGoapState<string, object> GetGoalState()
        {
            var state = new ReGoapState<string, object>();
            state.Set("enemiesPeeled", true);
            return state;
        }
    }
}
