using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.ReGoap.Mimic.Sensors;
using DOL.GS.Scripts;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>Interrupt a casting enemy target. One tick of damage pressure,
    /// then re-sense: success is observed as the enemy no longer casting.</summary>
    public sealed class InterruptEnemyCasterGoal : MimicGoal
    {
        public InterruptEnemyCasterGoal(MimicNPC body, MimicBrain brain) : base(body, brain) { }
        public override float GetPriority(ReGoapState<string, object> state)
            => GetBool(state, TacticalSensor.EnemyCasting) ? 9f : 0f;
        public override bool IsGoalSatisfied(ReGoapState<string, object> state)
            => !GetBool(state, TacticalSensor.EnemyCasting);
        public override ReGoapState<string, object> GetGoalState()
        {
            var state = new ReGoapState<string, object>();
            state.Set("enemyInterrupted", true);
            return state;
        }
    }
}
