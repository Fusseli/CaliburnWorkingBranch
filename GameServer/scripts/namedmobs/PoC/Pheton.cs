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
        public static readonly int ColdDD_ID = 161;   // Cold DD
        public static readonly int Stun_ID = 3379;   // Stun
        public static readonly int Heal_ID = 3067;   // Heal
        public static readonly int Debuff_ID = 4387;    // Str/Con Debuff

        // Pool of spells for adds (random pick per add)
        public static readonly int[] AddSpellPool =
        {
            360,   // Fire DD
            31114, // Cold DD
            4906,  // Heal
            894,   // Debuff
            3326   // Stun
        };
    }

    public class Pheton : GameNPC
    {
        private int _nextWaveAt = 90;

        public override bool AddToWorld()
        {
            if (!base.AddToWorld()) return false;

            _nextWaveAt = 90; // Reset so a respawned boss spawns his waves again
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

            // Spawn adds progressively at every 10% step: 1 add at 90%, 2 at 80%,
            // ... 9 at 10%. If one hit skips several steps, all crossed waves fire.
            while (_nextWaveAt > 0 && HealthPercent <= _nextWaveAt)
            {
                int numAdds = (100 - _nextWaveAt) / 10;
                for (int i = 0; i < numAdds; i++)
                    SpawnAdd();

                _nextWaveAt -= 10;
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
            if (Body.TargetObject == null)
                return false;

            // Cold DD (scaled)
            if (nextCold < Environment.TickCount)
            {
                var spell = BossSpellHelper.GetSpell(PhetonConfig.ColdDD_ID, Body.Level, damage: Body.Level * 6);
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
                var spell = BossSpellHelper.GetSpell(PhetonConfig.Stun_ID, Body.Level);
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
                var spell = BossSpellHelper.GetSpell(PhetonConfig.Heal_ID, Body.Level, value: m_owner.MaxHealth / 5);
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
                var spell = BossSpellHelper.GetSpell(PhetonConfig.Debuff_ID, Body.Level);
                if (spell != null)
                {
                    Body.CastSpell(spell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells));
                    nextDebuff = Environment.TickCount + 15000;
                    return true;
                }
            }

            return false;
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
