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
        /// <summary>
        /// PR38: control is worth a flat 3.8, not four points per add.
        ///
        /// The old score was Min(4 x adds, 40). With two adds that is 8, which outranks
        /// KiteGoal (6) and every support goal below it, and with ten it is 40 - so a bot in
        /// a large fight would queue control ahead of everything, including getting away.
        /// Nothing else in the ladder scales with how many enemies there are, because the
        /// question "is control worth a tick" has the same answer whether there are two adds
        /// or twelve: one cast is one cast. The count belongs to whether there is work, which
        /// ControlAvailable already answers.
        ///
        /// 3.8 sits directly where the ladder needs it: above DealDamageGoal (2.0) and
        /// PositionalStyleGoal (3.5), and below KiteGoal (6), InterruptEnemyCasterGoal (9)
        /// and the 4.0 pair AssistTrainGoal and TargetCallerGoal. Choosing an off-assist
        /// add to mez should not beat following the assist onto the focus target.
        ///
        /// The adds count is still read, but only as a zero guard - the sensor already sets
        /// ControlAvailable only when the actionable count is above zero, so this is the
        /// belt-and-braces version of the same statement: no add, no work. It deliberately
        /// does not multiply, which is the whole change.
        /// </summary>
        public const float Priority = 3.8f;

        public ControlAddsGoal(MimicNPC body, MimicBrain brain) : base(body, brain) { }
        public override float GetPriority(ReGoapState<string, object> state)
            => GetBool(state, TacticalSensor.ControlAvailable)
                && GetInt(state, MimicWorldStateKeys.NUM_CONTROLLABLE_ADDS) > 0
                ? Priority : 0f;
        public override bool IsGoalSatisfied(ReGoapState<string, object> state) => GetPriority(state) <= 0;
        public override ReGoapState<string, object> GetGoalState()
        {
            var state = new ReGoapState<string, object>();
            state.Set(MimicWorldStateKeys.ADDS_CONTROLLED, true);
            return state;
        }
    }
}
