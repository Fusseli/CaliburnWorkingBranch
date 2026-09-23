using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>Emergency healing always outranks routine healing, CC and damage.
    /// The shared healing routine chooses instant/group/single-target spells.</summary>
    public class EmergencyHealGoal : MimicGoal
    {
        public EmergencyHealGoal(MimicNPC body, MimicBrain brain) : base(body, brain) { }
        public override float GetPriority(ReGoapState<string, object> state) => GetNumEmergency(state) > 0 ? 100f : 0;
        public override bool IsGoalSatisfied(ReGoapState<string, object> state) => GetNumEmergency(state) == 0;
        public override ReGoapState<string, object> GetGoalState()
        {
            var state = new ReGoapState<string, object>();
            state.Set("targetHealed", true);
            return state;
        }
    }
}
