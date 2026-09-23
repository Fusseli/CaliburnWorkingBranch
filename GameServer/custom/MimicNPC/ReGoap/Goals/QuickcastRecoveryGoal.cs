using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.ReGoap.Mimic.Sensors;
using DOL.GS.Scripts;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>Interrupted caster pops Quickcast to force the current cast through.
    /// Instant ability, existing cooldown and uptime checks; satisfied once the
    /// buff is up or the pressure ends. Outranks kiting: no position lost.</summary>
    public sealed class QuickcastRecoveryGoal : MimicGoal
    {
        public QuickcastRecoveryGoal(MimicNPC body, MimicBrain brain) : base(body, brain) { }
        public override float GetPriority(ReGoapState<string, object> state)
            => GetBool(state, TacticalSensor.QuickcastReady) ? 8f : 0f;
        public override bool IsGoalSatisfied(ReGoapState<string, object> state)
            => !GetBool(state, TacticalSensor.QuickcastReady);
        public override ReGoapState<string, object> GetGoalState()
        {
            var state = new ReGoapState<string, object>();
            state.Set("castSecured", true);
            return state;
        }
    }
}
