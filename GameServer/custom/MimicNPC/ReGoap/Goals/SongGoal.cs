using DOL.AI.Brain;
using DOL.GS.ReGoap.Core;
using DOL.GS.ReGoap.Mimic.Sensors;
using DOL.GS.Scripts;

namespace DOL.GS.ReGoap.Mimic.Goals
{
    /// <summary>
    /// PR31: play a song. Bards and Minstrels are their realm's speed class, and the
    /// group-wide songs (speed, endurance, mana) are pulsing buffs that must be kept up
    /// between fights and through the regen window - not combat abilities.
    ///
    /// Ranking is deliberately blunt, because a song that sits mid-table simply never
    /// fires: the goal has to outrank the routine work it competes with, while still
    /// yielding to anything that saves a life.
    ///
    ///   SONG_PRIORITY_SUPPORT  45 - below emergency and group heals (100/50) and CC
    ///                                (40), above adds, purge, protect and resurrection.
    ///                                A bard is a healer/buffer/CC, so singing IS its
    ///                                main job and it should rarely be idle.
    ///   SONG_PRIORITY_DAMAGE   11 - a minstrel is a damage class that happens to heal,
    ///                                so its songs must beat routine damage (2) without
    ///                                displacing interrupts (9), guard (7) or kiting (6).
    ///
    /// The health regen song is included by GetBestSong but is a slow over-time, never a
    /// substitute for a real heal, so it is deliberately the lowest-ranked song.
    /// </summary>
    public sealed class SongGoal : MimicGoal
    {
        /// <summary>For a support-leaning singer (bard): singing is the main job.</summary>
        public const float SONG_PRIORITY_SUPPORT = 45f;
        /// <summary>For a damage-leaning singer (minstrel): songs supplement the swing.</summary>
        public const float SONG_PRIORITY_DAMAGE = 11f;

        private readonly float _priority;

        public SongGoal(MimicNPC body, MimicBrain brain, bool supportLeaning)
            : base(body, brain)
        {
            _priority = supportLeaning ? SONG_PRIORITY_SUPPORT : SONG_PRIORITY_DAMAGE;
        }

        public override float GetPriority(ReGoapState<string, object> state)
            => GetBool(state, TacticalSensor.SongAvailable) ? _priority : 0f;

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
