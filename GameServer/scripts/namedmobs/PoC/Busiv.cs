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
        public static readonly int DD_ID = 1678;       // Smite DD
        public static readonly int Debuff_ID = 2627;   // Dex/Qui Debuff
        public static readonly int DoT_ID = 4431;      // Body DoT

        // Healer add spells
        public static readonly int Heal_ID = 3067;     // Single-target heal
        public static readonly int GroupHeal_ID = 4964; // Group heal
    }

    public class Busiv : GameNPC
    {
        private HashSet<int> addsSpawnedAt = new HashSet<int>();
        private readonly List<Underling> _Underlings = new List<Underling>();

        public override bool AddToWorld()
        {
            if (!base.AddToWorld()) return false;

            Level = 75;
            Name = "Busiv";
            Model = 440;
            Size = 150;
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
                    int numAdds = Array.IndexOf(thresholds, t) + 1; // 1 add at 75%, 2 at 50%, 3 at 25%
                    for (int i = 0; i < numAdds; i++)
                    {
                        SpawnHealerAdd();
                    }
                    Say($"Busiv calls forth {numAdds} Underling{(numAdds > 1 ? "s" : "")} to aid him!");
                }
            }
        }

        private void SpawnHealerAdd()
        {
            var add = new Underling(this)
            {
                X = X + Util.Random(-100, 100),
                Y = Y + Util.Random(-100, 100),
                Z = Z,
                CurrentRegion = CurrentRegion,
                Heading = Heading,
                Level = 55,
                Realm = 0,
                Name = "Underling",
                Model = 2043,
                Size = 60,
            };

            add.SetOwnBrain(new UnderlingBrain(this));
            add.AddToWorld();
            _Underlings.Add(add);
        }

        public override void Die(GameObject killer)
        {
            foreach (var add in _Underlings)
            {
                if (add == null)
                    continue;

                add.RemoveFromWorld();
                add.Delete();
            }
            _Underlings.Clear();

            base.Die(killer);
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
            if (Body.TargetObject == null)
                return false;

            if (nextCast < Environment.TickCount)
            {
                int choice = rng.Next(3);
                int spellId = BusivConfig.DD_ID;
                if (choice == 1) spellId = BusivConfig.Debuff_ID;
                else if (choice == 2) spellId = BusivConfig.DoT_ID;

                var spell = BossSpellHelper.GetSpell(spellId, Body.Level);
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
            if ((Body.IsMezzed || Body.IsStunned) && Body.Health < Body.MaxHealth)
            {
                Body.Health = Body.MaxHealth;
                Body.Say("Busiv resists control magic and restores himself!");
            }
        }
    }

    public class Underling : GameNPC
    {
        private readonly Busiv m_master;

        public Underling(Busiv master)
        {
            m_master = master;
        }
    }

    public class UnderlingBrain : StandardMobBrain
    {
        private readonly Busiv m_master;
        private long nextHeal;
        private Random rng = new Random();

        public UnderlingBrain(Busiv master)
        {
            m_master = master;
            AggroLevel = 0; // Healers don't attack
            AggroRange = 0;
            nextHeal = Environment.TickCount + Util.Random(6000, 9000);
        }

        public override void Think()
        {
            base.Think();

            if (nextHeal < Environment.TickCount)
            {
                if (m_master != null && m_master.IsAlive && m_master.HealthPercent < 100)
                {
                    int spellId = rng.Next(2) == 0 ? BusivConfig.Heal_ID : BusivConfig.GroupHeal_ID;
                    var spell = BossSpellHelper.GetSpell(spellId, Body.Level);
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
