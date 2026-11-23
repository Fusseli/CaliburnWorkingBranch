using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// Good-samaritan rez (PR17b, opt-in via "/mrez outside"): a dead same-realm
    /// stranger in rez range is brought back, but only when the own group is
    /// fully calm — no dead mates, no emergencies, no combat, no aggro.
    /// Priority 0.8 sits below DealDamage (2.0) so real work always wins; it only
    /// beats off-train chip damage (0.6), which is the definition of idle.
    /// </summary>
    public class ResurrectOutsiderGoal : MimicGoal
    {
        private const float OUTSIDER_REZ_PRIORITY = 0.8f;
        private const string OUTSIDER_REZZED = "outsiderRezzed";

        public ResurrectOutsiderGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        public override float GetPriority(ReGoapState<string, object> currentState)
        {
            if (IsInCombat(currentState))
                return 0.0f;

            // Own group first: any dead mate suspends stranger duty.
            if (GetInt(currentState, MimicWorldStateKeys.NUM_DEAD, 0) > 0)
                return 0.0f;

            // Nobody bleeding: emergencies and criticals suspend stranger duty.
            if (GetNumEmergency(currentState) > 0 || GetNumCritical(currentState) > 0)
                return 0.0f;

            var outsider = GetStateValue<object>(currentState, MimicWorldStateKeys.OUTSIDER_TO_REZ, null);
            if (outsider == null)
                return 0.0f;

            if (!GetBool(currentState, MimicWorldStateKeys.CAN_CAST_REZ, false))
                return 0.0f;

            if (!GetBool(currentState, MimicWorldStateKeys.CAN_CAST, false))
                return 0.0f;

            if (!GetBool(currentState, Sensors.TacticalSensor.RezOutsideAvailable, false))
                return 0.0f;

            return OUTSIDER_REZ_PRIORITY;
        }

        public override ReGoapState<string, object> GetGoalState()
        {
            var goalState = new ReGoapState<string, object>();
            goalState.Set(OUTSIDER_REZZED, true);
            return goalState;
        }

        public override bool IsGoalSatisfied(ReGoapState<string, object> currentState)
        {
            var outsider = GetStateValue<object>(currentState, MimicWorldStateKeys.OUTSIDER_TO_REZ, null);
            return outsider == null;
        }

        public override string GetName()
        {
            return "ResurrectOutsiderGoal";
        }
    }
}
