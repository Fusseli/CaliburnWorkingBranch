using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// Self-purge (PR19b): a hard-CC'd bot (mezz/stun/root) with a ready Purge RA
    /// purges itself instead of waiting out the full duration. Priority 30 sits
    /// below a living emergency (100, which a CC'd bot cannot serve anyway) and
    /// above everything routine: purging restores agency.
    /// </summary>
    public class PurgeGoal : MimicGoal
    {
        private const float PURGE_PRIORITY = 30.0f;
        private const string PURGED = "purged";

        public PurgeGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        public override float GetPriority(ReGoapState<string, object> currentState)
        {
            if (!GetBool(currentState, Sensors.TacticalSensor.PurgeAvailable, false))
                return 0.0f;

            return PURGE_PRIORITY;
        }

        public override ReGoapState<string, object> GetGoalState()
        {
            var goalState = new ReGoapState<string, object>();
            goalState.Set(PURGED, true);
            return goalState;
        }

        public override bool IsGoalSatisfied(ReGoapState<string, object> currentState)
        {
            return !GetBool(currentState, Sensors.TacticalSensor.PurgeAvailable, false);
        }

        public override string GetName()
        {
            return "PurgeGoal";
        }
    }
}
