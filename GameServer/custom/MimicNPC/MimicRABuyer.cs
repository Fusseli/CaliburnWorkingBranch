using System;
using System.Collections.Generic;
using System.Linq;
using DOL.GS.RealmAbilities;

namespace DOL.GS.Scripts
{
    /// <summary>
    /// Server-side realm-ability buyer for MimicNPCs (PR19a). Players spend earned
    /// ranks through the trainer window; bots have no client, so this spends the
    /// same rank budget automatically from a curated priority table.
    ///
    /// Design notes:
    /// - Only RAs in <see cref="Priority"/> are ever bought (class list membership
    ///   still required). Unknown RAs, RR5s and unlisted actives are never touched;
    ///   RR5 arrives free via the RA spec at RL40 like for players.
    /// - Live set needs no prerequisites (only RR5 gates on rank, and it is not
    ///   bought). Atlas chains encode theirs in <see cref="Offer.PrereqKey"/>.
    /// - Grants go through MimicNPC.AddRealmAbility, so passives apply through the
    ///   normal Activate/AbilityBonus path. No persistence: mimics are transient,
    ///   the buyer re-runs on spawn and on every realm-rank up.
    /// </summary>
    public static class MimicRABuyer
    {
        [Flags]
        public enum Archetype
        {
            Melee = 1,
            Caster = 2,
            Healer = 4,
            Archer = 8,
            All = Melee | Caster | Healer | Archer,
        }

        public sealed class Entry
        {
            public string KeyName;
            public Archetype Who;
            public string PrereqKey;
            public int PrereqLevel;
            public bool StealthOnly;
        }

        /// <summary>Buy order = list order. Stat augments come first so Atlas
        /// prereq chains (AugX >= N) resolve in a single pass.</summary>
        public static readonly Entry[] Priority = new Entry[]
        {
            new Entry { KeyName = "Purge", Who = Archetype.All },
            new Entry { KeyName = "Toughness", Who = Archetype.All },
            new Entry { KeyName = "Augmented Constitution", Who = Archetype.All },
            new Entry { KeyName = "Determination", Who = Archetype.All },
            new Entry { KeyName = "Avoidance of Magic", Who = Archetype.All },
            new Entry { KeyName = "Augmented Strength", Who = Archetype.Melee },
            new Entry { KeyName = "Augmented Dexterity", Who = Archetype.Melee | Archetype.Archer },
            new Entry { KeyName = "Augmented Quickness", Who = Archetype.Melee },
            new Entry { KeyName = "Augmented Acuity", Who = Archetype.Caster | Archetype.Healer },
            new Entry { KeyName = "Mastery of Pain", Who = Archetype.Melee },
            new Entry { KeyName = "Mastery of Blocking", Who = Archetype.Melee },
            new Entry { KeyName = "Mastery of Parrying", Who = Archetype.Melee },
            new Entry { KeyName = "Physical Defense", Who = Archetype.Melee },
            new Entry { KeyName = "Dual Threat", Who = Archetype.Melee },
            new Entry { KeyName = "Mastery of Stealth", Who = Archetype.Melee, StealthOnly = true },
            new Entry { KeyName = "Mastery of Magery", Who = Archetype.Caster },
            new Entry { KeyName = "Mastery of Focus", Who = Archetype.Caster },
            new Entry { KeyName = "Serenity", Who = Archetype.Caster | Archetype.Healer },
            new Entry { KeyName = "Wild Power", Who = Archetype.Caster },
            new Entry { KeyName = "Falcons Eye", Who = Archetype.Archer },
            new Entry { KeyName = "Mastery of Healing", Who = Archetype.Healer },
            new Entry { KeyName = "Wild Healing", Who = Archetype.Healer },
            // Atlas counterparts (OF set). Prereqs encoded where the handlers gate.
            new Entry { KeyName = "Avoid Pain", Who = Archetype.All, PrereqKey = "Augmented Constitution", PrereqLevel = 3 },
        };

        public sealed class Offer
        {
            public string KeyName;
            public int MaxLevel;
            public Func<int, int> CostForUpgrade;
            public string PrereqKey;
            public int PrereqLevel;
        }

        /// <summary>Pure purchase planner: fills levels in offer order while the
        /// budget lasts. Prereqs resolve against levels owned (including levels
        /// bought earlier in the same run). Terminates after a full pass with no
        /// purchase or <paramref name="maxIterations"/> total buys.</summary>
        public static List<(string KeyName, int NewLevel)> PlanPurchases(
            IDictionary<string, int> owned, int budget, IList<Offer> offers, int maxIterations = 200)
        {
            var result = new List<(string KeyName, int NewLevel)>();
            var level = new Dictionary<string, int>(owned);
            int spent = 0;
            foreach (var kv in owned)
            {
                var offer = offers.FirstOrDefault(o => o.KeyName == kv.Key);
                if (offer == null)
                    continue;
                for (int l = 0; l < kv.Value; l++)
                    spent += Math.Max(0, offer.CostForUpgrade(l));
            }

            for (int iter = 0; iter < maxIterations; iter++)
            {
                bool bought = false;
                foreach (var offer in offers)
                {
                    int cur = level.TryGetValue(offer.KeyName, out int v) ? v : 0;
                    if (cur >= offer.MaxLevel)
                        continue;
                    if (!string.IsNullOrEmpty(offer.PrereqKey))
                    {
                        int pre = level.TryGetValue(offer.PrereqKey, out int pv) ? pv : 0;
                        if (pre < offer.PrereqLevel)
                            continue;
                    }
                    int cost = Math.Max(0, offer.CostForUpgrade(cur));
                    if (spent + cost > budget)
                        continue;
                    spent += cost;
                    level[offer.KeyName] = cur + 1;
                    result.Add((offer.KeyName, cur + 1));
                    bought = true;
                }
                if (!bought)
                    break;
            }
            return result;
        }

        /// <summary>Points already spent, for display (/mra). RAs outside the
        /// offer list count as free (unknown cost).</summary>
        public static int SpentPoints(IDictionary<string, int> owned, IList<Offer> offers)
        {
            int spent = 0;
            foreach (var kv in owned)
            {
                var offer = offers.FirstOrDefault(o => o.KeyName == kv.Key);
                if (offer == null)
                    continue;
                for (int l = 0; l < kv.Value; l++)
                    spent += Math.Max(0, offer.CostForUpgrade(l));
            }
            return spent;
        }

        /// <summary>Mirror of the player rank budget (AbstractServerRules).
        /// Pure in level/rank for unit tests.</summary>
        public static int RealmPointBudget(int level, int realmLevel)
            => level > 19 ? Math.Max(1, realmLevel) : realmLevel;

        public static Archetype ArchetypeOf(MimicNPC mimic, bool isHealer)
        {
            if (isHealer)
                return Archetype.Healer;
            if (mimic.CharacterClass != null && mimic.CharacterClass.ClassType == eClassType.ListCaster)
                return Archetype.Caster;
            var weapon = mimic.ActiveWeapon;
            if (weapon != null && weapon.Item_Type == (int)eInventorySlot.DistanceWeapon)
                return Archetype.Archer;
            return Archetype.Melee;
        }

        /// <summary>Spends the bot's rank budget. Idempotent: re-running with an
        /// unchanged rank buys nothing.</summary>
        public static void CheckRealmAbilities(MimicNPC mimic)
        {
            if (mimic == null || mimic.CharacterClass == null)
                return;
            bool isHealer = mimic.MimicBrain != null && mimic.MimicBrain.IsHealer;
            Archetype arch = ArchetypeOf(mimic, isHealer);
            List<RealmAbility> classRAs;
            try
            {
                classRAs = SkillBase.GetClassRealmAbilities(mimic.CharacterClass.ID);
            }
            catch
            {
                return;
            }
            if (classRAs == null)
                return;

            var owned = new Dictionary<string, int>();
            foreach (var ab in mimic.GetRealmAbilities())
            {
                if (ab != null && !string.IsNullOrEmpty(ab.KeyName))
                    owned[ab.KeyName] = ab.Level;
            }

            var offers = new List<Offer>();
            foreach (var entry in Priority)
            {
                if ((entry.Who & arch) == 0)
                    continue;
                if (entry.StealthOnly && !mimic.HasAbility("Stealth"))
                    continue;
                var ra = classRAs.FirstOrDefault(r => r != null && r.KeyName == entry.KeyName && !(r is RR5RealmAbility));
                if (ra == null)
                    continue;
                var inst = ra;
                offers.Add(new Offer
                {
                    KeyName = inst.KeyName,
                    MaxLevel = Math.Max(0, inst.MaxLevel),
                    CostForUpgrade = lvl => inst.CostForUpgrade(lvl),
                    PrereqKey = entry.PrereqKey,
                    PrereqLevel = entry.PrereqLevel,
                });
            }

            var plan = PlanPurchases(owned, RealmPointBudget(mimic.Level, mimic.RealmLevel), offers);
            foreach (var (key, newLevel) in plan)
            {
                var inst = classRAs.FirstOrDefault(r => r != null && r.KeyName == key);
                if (inst == null)
                    continue;
                inst.Level = newLevel;
                mimic.AddRealmAbility(inst, false);
            }
        }
    }
}
