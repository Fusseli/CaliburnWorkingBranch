using DOL.Database;
using DOL.GS.Spells;
using System.Collections.Generic;

namespace DOL.GS.CustomBosses
{
    /// <summary>
    /// Builds boss-friendly variants of DB spells: mostly instant or short cast,
    /// and mostly uninterruptible so the PoC bosses can actually cast while
    /// being melee'd (pattern inspired by CaliburnRandomBoss).
    /// </summary>
    public static class BossSpellHelper
    {
        private static readonly Dictionary<int, DbSpell> m_spellCache = new Dictionary<int, DbSpell>();

        /// <summary>
        /// Returns a clone of the DB spell with boss-friendly cast times.
        /// ~40% of the time the spell is instant, otherwise 0.5 - 1.5s cast,
        /// with ~85% chance of being uninterruptible.
        /// Damage and heal/debuff values are scaled by the mob's level
        /// (CaliburnRandomBoss style) unless explicitly overridden.
        /// </summary>
        public static Spell GetSpell(int spellId, int level = 50, int? damage = null, int? value = null)
        {
            if (!m_spellCache.TryGetValue(spellId, out DbSpell dbSpell))
            {
                dbSpell = GameServer.Database.FindObjectByKey<DbSpell>(spellId);
                if (dbSpell == null)
                    return null;

                m_spellCache[spellId] = dbSpell;
            }

            bool instant = Util.Chance(40);
            double castTime = instant ? 0 : 0.5 + Util.RandomDouble() * 1.0;
            bool uninterruptible = instant || Util.Chance(85);

            // Scale damage by mob level so high level bosses actually hurt
            if (damage == null && dbSpell.Damage > 0)
                damage = level * Util.Random(4, 6);

            // Scale heals and debuffs by mob level too
            if (value == null && dbSpell.Value > 0)
                value = IsDebuffSpellType(dbSpell.Type) ? level * Util.Random(1, 2) : level * Util.Random(4, 6);

            DbSpell clone = new DbSpell
            {
                AllowAdd = dbSpell.AllowAdd,
                CastTime = castTime,
                ClientEffect = dbSpell.ClientEffect,
                Damage = damage ?? dbSpell.Damage,
                DamageType = dbSpell.DamageType,
                Description = dbSpell.Description,
                Duration = dbSpell.Duration,
                Frequency = dbSpell.Frequency,
                Icon = dbSpell.Icon,
                Name = dbSpell.Name,
                Range = dbSpell.Range,
                Radius = dbSpell.Radius,
                RecastDelay = dbSpell.RecastDelay,
                Target = dbSpell.Target,
                TooltipId = dbSpell.TooltipId,
                Type = dbSpell.Type,
                Uninterruptible = uninterruptible,
                Value = value ?? dbSpell.Value,
                SpellID = dbSpell.SpellID
            };

            return new Spell(clone, level);
        }

        /// <summary>
        /// Debuff types scale more slowly than heals/damage so they stay debuffs.
        /// </summary>
        private static bool IsDebuffSpellType(string type)
        {
            switch (type)
            {
                case "StrengthConstitutionDebuff":
                case "DexterityQuicknessDebuff":
                case "CombatSpeedDebuff":
                case "DamageSpeedDecrease":
                case "SpeedDecrease":
                case "Snare":
                    return true;
                default:
                    return false;
            }
        }
    }
}