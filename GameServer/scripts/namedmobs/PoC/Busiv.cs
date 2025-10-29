using System;
using System.Collections.Generic;
using System.Linq;
using DOL.AI.Brain;
using DOL.Database;
using DOL.Events;
using DOL.GS;
using DOL.GS.Spells;
using DOL.GS.Effects;

namespace DOL.GS.CustomBosses
{
    public static class BusivConfig
    {
        // Offensive spells
        public static readonly int DD_ID = 9300;       // Smite DD
        public static readonly int Debuff_ID = 9345;   // Dex/Qui Debuff
        public static readonly int DoT_ID = 9360;      // Body DoT

        // Healer add spells
        public static readonly int Heal_ID = 8772;     // Single-target heal
        public static readonly int GroupHeal_ID = 8776; // Group heal
    }

    public class Busiv : GameNPC
    {
        private HashSet<int> addsSpawnedAt = new HashSet<int>();

        public override bool AddToWorld()
        {
            if (!base.AddToWorld()) return false;

            Level = 82;
            Name = "Busiv";
            Model = 650;
            Size = 130;
            MaxSpeedBase = 200;
            Realm = 0;

            SetOwnBrain(new BusivBrain(this));
            return true;
        }

        public override int MaxHealth => base.MaxHealth * 7;

        public override void TakeDamage(GameObject source, eDamageType damageType, int damageAmount, int criticalAmount)
        {
            base.TakeDamage(source, damageType, damageAmount, criticalAmount);

            int[] thresholds = { 75, 50, 25 };
            foreach (var t in thresholds)
            {
                if (HealthPercent <= t && !addsSpawnedAt.Contains(t))
                {
                    addsSpawnedAt.Add(t);
                    int numAdds = thresholds.Length - Array.IndexOf(thresholds, t); // 1 add at 75%, 2 at 50%, 3 at 25%
                    for (int i = 0; i < numAdds; i++)
                    {
                        SpawnHealerAdd();
                    }
                    Say($"Busiv calls forth {numAdds} Soigneur{(numAdds > 1 ? "s" : "")} to aid him!");
                }
            }
        }

        private void SpawnHealerAdd()
        {
            var add = new Soigneur(this)
            {
                X = X + Util.Random(-100, 100),
                Y = Y + Util.Random(-100, 100),
                Z = Z,
                CurrentRegion = CurrentRegion,
                Heading = Heading,
                Level = 55,
                Realm = 0,
                Name = "Soigneur",
                Model = 605,
                Size = 60,
            };

            add.SetOwnBrain(new SoigneurBrain(this));
            add.AddToWorld();
        }

        public override int GetResist(eDamageType damageType)
        {
            switch (damageType)
            {
                case eDamageType.Slash: return 20;
                case eDamageType.Crush: return 25;
                case eDamageType.Thrust: return 25;
                case eDamageType.Heat: return 35;
                case eDamageType.Cold: return 35;
                case eDamageType.Matter: return 25;
                case eDamageType.Body: return 40;
                case eDamageType.Spirit: return 30;
                case eDamageType.Energy: return 20;
                default: return 0;
            }
        }
    }

    public class BusivBrain : StandardMobBrain
    {
        private readonly Busiv m_owner;
        private long nextCast;
        private Random rng = new Random();

        public BusivBrain(Busiv owner)
        {
            m_owner = owner;
            AggroLevel = 100;
            AggroRange = 1200;
        }

        public override bool CheckSpells(eCheckSpellType type)
        {
            if (Body.TargetObject == null || !(Body.TargetObject is GameLiving target))
                return false;

            if (nextCast < Environment.TickCount)
            {
                int choice = rng.Next(3);
                int spellId = BusivConfig.DD_ID;
                if (choice == 1) spellId = BusivConfig.Debuff_ID;
                else if (choice == 2) spellId = BusivConfig.DoT_ID;

                var spell = SkillBase.GetSpellByID(spellId);
                if (spell != null)
                {
                    Body.CastSpell(spell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells));
                    nextCast = Environment.TickCount + Util.Random(4000, 7000);
                    return true;
                }
            }

            return false;
        }

        public override void Think()
        {
            base.Think();

            // Anti-mezz or stun: heal to full
            foreach (var effect in Body.EffectList)
            {
                if (effect is GameSpellEffect gse && gse.SpellHandler != null)
                {
                    if (gse.SpellHandler.Spell.SpellType == eSpellType.Mez || gse.SpellHandler.Spell.SpellType == eSpellType.Stun)
                    {
                        Body.Health = Body.MaxHealth;
                        Body.Say("Busiv resists control magic and restores himself!");
                        break;
                    }
                }
            }
        }
    }

    public class Soigneur : GameNPC
    {
        private readonly Busiv m_master;

        public Soigneur(Busiv master)
        {
            m_master = master;
        }
    }

    public class SoigneurBrain : StandardMobBrain
    {
        private readonly Busiv m_master;
        private long nextHeal;
        private Random rng = new Random();

        public SoigneurBrain(Busiv master)
        {
            m_master = master;
            AggroLevel = 0; // Healers don’t attack
            AggroRange = 0;
        }

        public override void Think()
        {
            base.Think();

            if (nextHeal < Environment.TickCount)
            {
                if (m_master != null && m_master.IsAlive && m_master.HealthPercent < 100)
                {
                    int spellId = rng.Next(2) == 0 ? BusivConfig.Heal_ID : BusivConfig.GroupHeal_ID;
                    var spell = SkillBase.GetSpellByID(spellId);
                    if (spell != null)
                    {
                        Body.TargetObject = m_master;
                        Body.CastSpell(spell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells));
                    }
                }
                nextHeal = Environment.TickCount + Util.Random(6000, 9000);
            }
        }
    }
}
