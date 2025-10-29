using System;
using System.Collections.Generic;
using System.Linq;
using DOL.AI.Brain;
using DOL.Database;
using DOL.Events;
using DOL.GS;
using DOL.GS.Spells;

namespace DOL.GS.CustomBosses
{
    public static class PhetonConfig
    {
        // Pheton main spells
        public static readonly int ColdDD_ID = 40022;   // Cold DD
        public static readonly int Stun_ID = 61049;   // Stun
        public static readonly int Heal_ID = 32193;   // Heal
        public static readonly int Debuff_ID = 4385;    // Str/Con Debuff

        // Pool of spells for adds (random pick per add)
        public static readonly int[] AddSpellPool =
        {
            32119, // Fire DD
            32125, // Cold DD
            32155, // Heal
            4385,  // Debuff
            61049  // Stun
        };
    }

    public class Pheton : GameNPC
    {
        private HashSet<int> spawnedAt = new HashSet<int>();

        public override bool AddToWorld()
        {
            if (!base.AddToWorld()) return false;

            Level = 85;
            Name = "Pheton";
            Model = 642;
            Size = 100;
            MaxSpeedBase = 200;
            Realm = 0;

            SetOwnBrain(new PhetonBrain(this));
            return true;
        }

        public override int MaxHealth => base.MaxHealth * 5;

        public override void TakeDamage(GameObject source, eDamageType damageType, int damageAmount, int criticalAmount)
        {
            base.TakeDamage(source, damageType, damageAmount, criticalAmount);

            // Spawn adds progressively every 10%
            int threshold = (HealthPercent / 10) * 10;
            if (!spawnedAt.Contains(threshold) && threshold > 0)
            {
                spawnedAt.Add(threshold);
                int numAdds = (100 - threshold) / 10; // progressive scaling
                for (int i = 0; i < numAdds; i++)
                    SpawnAdd();
            }
        }

        private void SpawnAdd()
        {
            var add = new PhetonAdd
            {
                X = X + Util.Random(-100, 100),
                Y = Y + Util.Random(-100, 100),
                Z = Z,
                CurrentRegion = CurrentRegion,
                Heading = Heading,
                Level = 55,
                Name = "Squelete",
                Model = 16,
                Size = 40,
                Realm = 0
            };

            // Assign random spell from pool
            int spellId = PhetonConfig.AddSpellPool[Util.Random(PhetonConfig.AddSpellPool.Length - 1)];
            var brain = new AddBrain(spellId);
            add.SetOwnBrain(brain);
            add.AddToWorld();
        }

        public override void Die(GameObject killer)
        {
            // Despawn adds
            foreach (var npc in GetNPCsInRadius(2000))
            {
                if (npc.Name == "Squelete")
                    npc.Delete();
            }
            base.Die(killer);
        }

        public override int GetResist(eDamageType damageType)
        {
            switch (damageType)
            {
                case eDamageType.Slash: return 30;
                case eDamageType.Crush: return 20;
                case eDamageType.Thrust: return 40;
                case eDamageType.Heat: return 50;
                case eDamageType.Cold: return 60;
                case eDamageType.Matter: return 25;
                case eDamageType.Body: return 10;
                case eDamageType.Spirit: return 35;
                case eDamageType.Energy: return 15;
                default: return 0;
            }
        }
    }

    public class PhetonAdd : GameNPC
    {
        public override int GetResist(eDamageType damageType)
        {
            switch (damageType)
            {
                case eDamageType.Slash: return 15;  // weaker than boss
                case eDamageType.Crush: return 10;
                case eDamageType.Thrust: return 20;
                case eDamageType.Heat: return 25;
                case eDamageType.Cold: return 30;
                case eDamageType.Matter: return 12;
                case eDamageType.Body: return 5;
                case eDamageType.Spirit: return 18;
                case eDamageType.Energy: return 8;
                default: return 0;
            }
        }
    }

    public class PhetonBrain : StandardMobBrain
    {
        private readonly Pheton m_owner;
        private long nextCold;
        private long nextStun;
        private long nextHeal;
        private long nextDebuff;

        public PhetonBrain(Pheton owner)
        {
            m_owner = owner;
            AggroLevel = 100;
            AggroRange = 1200;
        }

        public override bool CheckSpells(eCheckSpellType type)
        {
            if (Body.TargetObject == null || !(Body.TargetObject is GameLiving target))
                return false;

            // Cold DD (scaled)
            if (nextCold < Environment.TickCount)
            {
                var spell = ScaleDamage(PhetonConfig.ColdDD_ID, Body.Level * 6);
                if (spell != null)
                {
                    Body.CastSpell(spell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells));
                    nextCold = Environment.TickCount + 5000;
                    return true;
                }
            }

            // Stun (every 20s)
            if (nextStun < Environment.TickCount && Util.Chance(20))
            {
                var spell = SkillBase.GetSpellByID(PhetonConfig.Stun_ID);
                if (spell != null)
                {
                    Body.CastSpell(spell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells));
                    nextStun = Environment.TickCount + 20000;
                    return true;
                }
            }

            // Heal (scaled, every 30s)
            if (nextHeal < Environment.TickCount && Body.HealthPercent < 80)
            {
                var spell = ScaleHeal(PhetonConfig.Heal_ID, m_owner.MaxHealth / 5);
                if (spell != null)
                {
                    Body.CastSpell(spell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells));
                    nextHeal = Environment.TickCount + 30000;
                    return true;
                }
            }

            // Debuff (every 15s)
            if (nextDebuff < Environment.TickCount)
            {
                var spell = SkillBase.GetSpellByID(PhetonConfig.Debuff_ID);
                if (spell != null)
                {
                    Body.CastSpell(spell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells));
                    nextDebuff = Environment.TickCount + 15000;
                    return true;
                }
            }

            return false;
        }

        private Spell ScaleDamage(int spellId, int dmg)
        {
            var dbSpell = GameServer.Database.FindObjectByKey<DbSpell>(spellId);
            if (dbSpell == null) return null;

            var clone = new DbSpell
            {
                AllowAdd = dbSpell.AllowAdd,
                CastTime = dbSpell.CastTime,
                ClientEffect = dbSpell.ClientEffect,
                DamageType = dbSpell.DamageType,
                Description = dbSpell.Description,
                Duration = dbSpell.Duration,
                Icon = dbSpell.Icon,
                Name = dbSpell.Name,
                Range = dbSpell.Range,
                Radius = dbSpell.Radius,
                RecastDelay = dbSpell.RecastDelay,
                Target = dbSpell.Target,
                Type = dbSpell.Type,
                Uninterruptible = dbSpell.Uninterruptible,
                TooltipId = dbSpell.TooltipId,
                SpellID = dbSpell.SpellID,
                Damage = dmg
            };

            return new Spell(clone, 50);
        }

        private Spell ScaleHeal(int spellId, int value)
        {
            var dbSpell = GameServer.Database.FindObjectByKey<DbSpell>(spellId);
            if (dbSpell == null) return null;

            var clone = new DbSpell
            {
                AllowAdd = dbSpell.AllowAdd,
                CastTime = dbSpell.CastTime,
                ClientEffect = dbSpell.ClientEffect,
                Description = dbSpell.Description,
                Duration = dbSpell.Duration,
                Icon = dbSpell.Icon,
                Name = dbSpell.Name,
                Range = dbSpell.Range,
                Radius = dbSpell.Radius,
                RecastDelay = dbSpell.RecastDelay,
                Target = dbSpell.Target,
                Type = dbSpell.Type,
                Uninterruptible = dbSpell.Uninterruptible,
                TooltipId = dbSpell.TooltipId,
                SpellID = dbSpell.SpellID,
                Value = value
            };

            return new Spell(clone, 50);
        }
    }

    public class AddBrain : StandardMobBrain
    {
        private readonly int spellId;
        private long nextCast;

        public AddBrain(int id)
        {
            spellId = id;
            AggroLevel = 100;
            AggroRange = 800;
        }

        public override bool CheckSpells(eCheckSpellType type)
        {
            if (Body.TargetObject == null || !(Body.TargetObject is GameLiving target))
                return false;

            if (nextCast < Environment.TickCount)
            {
                var spell = SkillBase.GetSpellByID(spellId);
                if (spell != null)
                {
                    Body.CastSpell(spell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells));
                    nextCast = Environment.TickCount + Util.Random(8000, 12000);
                    return true;
                }
            }

            return false;
        }
    }
}
