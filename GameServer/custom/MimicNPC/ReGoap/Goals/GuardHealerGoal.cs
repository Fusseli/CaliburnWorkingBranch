using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.ReGoap.Mimic.Sensors;
using DOL.GS.Scripts;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>Keep the engine Guard effect on the neediest group member
    /// (attacked healer/caster first, never just the healer by habit).
    /// Assign once per need: the effect holds until death or group leave.
    /// Manual /mguard (or whisper) locks the agent out while its target stands.
    /// Outranks routine damage and assist train, yields to interrupts, healing,
    /// curing, crowd control and peel.</summary>
    public sealed class GuardHealerGoal : MimicGoal
    {
        public GuardHealerGoal(MimicNPC body, MimicBrain brain) : base(body, brain) { }
        public override float GetPriority(ReGoapState<string, object> state)
        {
            if (!GetBool(state, TacticalSensor.GuardAvailable)) return 0f;
            return IsInCombat(state) ? 7f : 3f;
        }
        public override bool IsGoalSatisfied(ReGoapState<string, object> state)
            => !GetBool(state, TacticalSensor.GuardAvailable);
        public override ReGoapState<string, object> GetGoalState()
        {
            var state = new ReGoapState<string, object>();
            state.Set("guardAssigned", true);
            return state;
        }
    }
}
