using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.ReGoap.Mimic.Sensors;
using DOL.GS.Scripts;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>New-archery damage type: apply the resist-beating chooser buff
    /// (Blunt/Thrusting/Slashing Arrows) for the current target. One self-buff tick,
    /// near-permanent until replaced; stealth-safe. Yields to interrupts, assist
    /// discipline and support; outranks plain damage. Dormant under old archery,
    /// where shots take type from ammo instead.</summary>
    public sealed class ArrowTypeGoal : MimicGoal
    {
        public ArrowTypeGoal(MimicNPC body, MimicBrain brain) : base(body, brain) { }
        public override float GetPriority(ReGoapState<string, object> state)
            => GetBool(state, TacticalSensor.ArrowTypeAvailable) ? 2.5f : 0f;
        public override bool IsGoalSatisfied(ReGoapState<string, object> state)
            => !GetBool(state, TacticalSensor.ArrowTypeAvailable);
        public override ReGoapState<string, object> GetGoalState()
        {
            var state = new ReGoapState<string, object>();
            state.Set("arrowTypeChosen", true);
            return state;
        }
    }
}
