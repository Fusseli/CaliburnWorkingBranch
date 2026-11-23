using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.Scripts;
using DOL.GS.Scripts.ReGoap;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// Group rez: a dead group member (PR16 corpse) is brought back with the
    /// first applicable rez spell. Out of combat the corpse waits its turn
    /// (15, between CureGroup and Interrupt). Mid-fight (PR18) it goes at 25 —
    /// below a living emergency (100) but above cure/ peel/ damage — and only
    /// when the sensor certifies a safe cast (caster clear, corpse nearly alone).
    /// </summary>
    public class ResurrectGoal : MimicGoal
    {
        private const float REZ_PRIORITY = 15.0f;
        private const float BATTLE_REZ_PRIORITY = 25.0f;
        private const string CORPSE_REZZED = "corpseRezzed";

        public ResurrectGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        public override float GetPriority(ReGoapState<string, object> currentState)
        {
            var memberToRez = GetStateValue<object>(currentState, MimicWorldStateKeys.MEMBER_TO_REZ, null);
            if (memberToRez == null)
                return 0.0f;

            if (!GetBool(currentState, MimicWorldStateKeys.CAN_CAST_REZ, false))
                return 0.0f;

            if (!GetBool(currentState, MimicWorldStateKeys.CAN_CAST, false))
                return 0.0f;

            if (!GetBool(currentState, Sensors.TacticalSensor.RezAvailable, false))
                return 0.0f;

            // PR18: a corpse under fire outranks calm routine, but only a safe cast.
            if (IsInCombat(currentState))
                return GetBool(currentState, MimicWorldStateKeys.REZ_COMBAT_SAFE, false) ? BATTLE_REZ_PRIORITY : 0.0f;

            return REZ_PRIORITY;
        }

        public override ReGoapState<string, object> GetGoalState()
        {
            var goalState = new ReGoapState<string, object>();
            goalState.Set(CORPSE_REZZED, true);
            return goalState;
        }

        public override bool IsGoalSatisfied(ReGoapState<string, object> currentState)
        {
            var memberToRez = GetStateValue<object>(currentState, MimicWorldStateKeys.MEMBER_TO_REZ, null);
            return memberToRez == null;
        }

        public override string GetName()
        {
            return "ResurrectGoal";
        }
    }
}
