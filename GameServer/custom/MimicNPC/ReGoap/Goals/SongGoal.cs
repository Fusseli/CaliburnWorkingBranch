using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.ReGoap.Mimic.Sensors;
using DOL.GS.Scripts;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// PR31/PR34: play a song. Bards and Minstrels are their realm's speed class, and the
    /// group-wide songs (speed, endurance, mana) are pulsing buffs that must be kept up
    /// between fights and through the regen window - not combat abilities.
    ///
    /// One priority for both classes. PR31 split bards (45) from minstrels (11) on the
    /// assumption that minstrels should mostly swing instead - but the split was based on
    /// player-class instincts, not on what these bots can actually do. MinstrelSpec grants
    /// only a melee weapon spec, Specs.Instruments and Specs.Stealth: no Nurture, no
    /// Regrowth, no cure line. A minstrel therefore has no heal, cure or rez to displace,
    /// so ranking its songs at 11 below the calm-rez at 15 only cost it its speed buff
    /// right after a fight, when a corpse is exactly what is lying around.
    ///
    /// If a minstrel ever gains heal or rez specs, split the priority again then.
    ///
    /// Sits below emergency heal (100) and group heal (50), so real healing always wins.
    /// </summary>
    public sealed class SongGoal : MimicGoal
    {
        /// <summary>For both singers: below heals, above cures, rez, interrupts and all
        /// routine work, so keeping the group speed buff up is not leftover effort.</summary>
        public const float SONG_PRIORITY = 45f;

        public SongGoal(MimicNPC body, MimicBrain brain) : base(body, brain)
        {
        }

        public override float GetPriority(ReGoapState<string, object> state)
            => GetBool(state, TacticalSensor.SongAvailable) ? SONG_PRIORITY : 0f;

        public override bool IsGoalSatisfied(ReGoapState<string, object> state)
            => !GetBool(state, TacticalSensor.SongAvailable);

        public override ReGoapState<string, object> GetGoalState()
        {
            var state = new ReGoapState<string, object>();
            state.Set(SongCastEffect, true);
            return state;
        }

        /// <summary>Shared with the "CastSong" action so the planner can satisfy the goal.</summary>
        public const string SongCastEffect = "songCast";
    }
}
