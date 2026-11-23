using System.Collections.Generic;

namespace DOL.GS.Scripts
{
    /// <summary>What kind of crowd control a spell is.</summary>
    public enum CcKind
    {
        /// <summary>Not crowd control at all.</summary>
        None,
        /// <summary>Hard control: the victim cannot act at all.</summary>
        Mezzer,
        /// <summary>Hard control delivered by the stun family.</summary>
        Stun,
        /// <summary>A speed decrease to the point of immobility.</summary>
        Root,
        /// <summary>A partial speed decrease. The victim still moves, slowly.</summary>
        Snare,
    }

    /// <summary>
    /// PR37: the single place that decides what counts as crowd control.
    ///
    /// Before this, the only definition lived inline in MimicNPC.SortSpells and it read
    /// <c>SpellType == Mesmerize || (SpellType == SpeedDecrease &amp;&amp; Value >= 99)</c>.
    /// That is the whole reason "tanks should be snared" was not implementable: a snare is
    /// a SpeedDecrease with a value below 99, so it was filed under HarmfulSpells and the
    /// AI never saw that the bot was carrying one. Every spell type that is real CC was
    /// invisible too - they all landed in HarmfulSpells and were picked by Util.Random instead.
    ///
    /// Deliberately NOT classified yet: Fear, Silence, Amnesia, Nearsight, Charm. Each would
    /// turn CanCastCrowdControlSpells true for any bot carrying it, which wakes the CC goal
    /// for that bot - a behaviour change that belongs to PR39, where the spell is actually
    /// chosen by kind, rather than to PR37, where it is only classified.
    ///
    /// Pure on purpose: it takes primitives, so every rule here is unit-testable without a
    /// live body. Classify(Spell) is the convenience overload the brain calls.
    /// </summary>
    public static class CcClassifier
    {
        /// <summary>A speed decrease at or above this value is a hard root, not a snare.</summary>
        public const int ROOT_VALUE_THRESHOLD = 99;

        private static readonly HashSet<eSpellType> Mezzers = new HashSet<eSpellType>
        {
            eSpellType.Mesmerize,
            eSpellType.Mez,
            eSpellType.MesmerizeDurationBuff,
            eSpellType.CeremonialBracerMezz,
        };

        private static readonly HashSet<eSpellType> Stuns = new HashSet<eSpellType>
        {
            eSpellType.Stun,
            eSpellType.StyleStun,
            eSpellType.UnrresistableNonImunityStun,
            eSpellType.UnresistableStun,
            eSpellType.CeremonialBracerStun,
        };

        /// <summary>
        /// The whole speed-decrease family. Roots are not a type of their own in this data -
        /// eSpellType has no Root or Snare member at all - so a root is a SpeedDecrease that
        /// reached 100%. That is why the old test needed its Value >= 99 clause, and also why
        /// anything below it silently stopped counting as control.
        /// </summary>
        private static readonly HashSet<eSpellType> SpeedDecreases = new HashSet<eSpellType>
        {
            eSpellType.SpeedDecrease,
            eSpellType.DamageSpeedDecrease,
            eSpellType.DamageSpeedDecreaseNoVariance,
            eSpellType.UnbreakableSpeedDecrease,
            eSpellType.VampSpeedDecrease,
            eSpellType.WarlockSpeedDecrease,
            eSpellType.StyleSpeedDecrease,
        };

        /// <summary>Is this spell type one of the speed-decrease family?</summary>
        public static bool IsSpeedDecrease(eSpellType type) => SpeedDecreases.Contains(type);

        /// <summary>Is this spell type a hard control, whatever its magnitude?</summary>
        public static bool IsHardControl(eSpellType type) => Mezzers.Contains(type) || Stuns.Contains(type);

        /// <summary>Does this spell type control movement at all?</summary>
        public static bool IsMovementControl(eSpellType type) => IsHardControl(type) || IsSpeedDecrease(type);

        /// <summary>
        /// Classify a spell from its type and magnitude. <paramref name="value"/> is
        /// Spell.Value, which only matters for the speed-decrease family.
        /// </summary>
        public static CcKind Classify(eSpellType type, double value)
        {
            if (Mezzers.Contains(type))
                return CcKind.Mezzer;
            if (Stuns.Contains(type))
                return CcKind.Stun;
            if (SpeedDecreases.Contains(type))
                return value >= ROOT_VALUE_THRESHOLD ? CcKind.Root : CcKind.Snare;
            return CcKind.None;
        }

        /// <summary>Classify a spell, or None when there is no spell.</summary>
        public static CcKind Classify(Spell spell) => spell == null ? CcKind.None : Classify(spell.SpellType, spell.Value);

        /// <summary>Is this spell control at all?</summary>
        public static bool IsCrowdControl(eSpellType type, double value) => Classify(type, value) != CcKind.None;

        /// <summary>
        /// Does this spell land on more than one target? PR37 asks the question so PR40 can
        /// count who is standing in it; the answer today is only ever used to choose between
        /// two of this bot's own spells.
        /// </summary>
        public static bool IsAreaControl(eSpellTarget target, int radius) => target == eSpellTarget.AREA || radius > 0;

        /// <summary>Does this spell land on more than one target?</summary>
        public static bool IsAreaControl(Spell spell) => spell != null && IsAreaControl(spell.Target, spell.Radius);
    }
}