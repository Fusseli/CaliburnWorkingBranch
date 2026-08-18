using System;
using System.Collections.Generic;
using System.Linq;
using DOL.AI.Brain;
using DOL.Database;
using DOL.Events;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.Spells;

namespace DOL.GS.CustomBosses
{
    public static class DrevaulConfig
    {
        public static readonly int DD_ID = 161;         // Cold DD
        public static readonly int Debuff_ID = 4387;      // Str/Con Debuff
        public static readonly int DoT_ID = 14358;        // Cold DoT-like
        public static readonly int SpecialBurstDD_ID = 31114; // Strong Cold DD
    }

    public class Drevaul : GameNPC
    {
        private HashSet<int> burstUsedAt = new HashSet<int>();

        public override bool AddToWorld()
        {
            if (!base.AddToWorld()) return false;

            Level = 75;
            Name = "Drevaul";
            Model = 605;
            Size = 150;
            MaxSpeedBase = 200;
            Realm = 0;

            SetOwnBrain(new DrevaulBrain(this));
            return true;
        }

        public override int MaxHealth => base.MaxHealth * 6;

        public override void TakeDamage(GameObject source, eDamageType damageType, int damageAmount, int criticalAmount)
        {
            base.TakeDamage(source, damageType, damageAmount, criticalAmount);

            // Trigger burst nukes at 70%, 50%, 20%
            int[] thresholds = { 70, 50, 20 };
            foreach (var t in thresholds)
            {
                if (HealthPercent <= t && !burstUsedAt.Contains(t))
                {
                    burstUsedAt.Add(t);
                    var spell = BossSpellHelper.GetSpell(DrevaulConfig.SpecialBurstDD_ID, Level);
                    if (spell != null)
                    {
                        CastSpell(spell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells));
                    }
                }
            }
        }

        public override int GetResist(eDamageType damageType)
        {
            switch (damageType)
            {
                case eDamageType.Slash: return 25;
                case eDamageType.Crush: return 25;
                case eDamageType.Thrust: return 35;
                case eDamageType.Heat: return 40;
                case eDamageType.Cold: return 50;
                case eDamageType.Matter: return 20;
                case eDamageType.Body: return 15;
                case eDamageType.Spirit: return 30;
                case eDamageType.Energy: return 20;
                default: return 0;
            }
        }
    }

    public class DrevaulBrain : StandardMobBrain
    {
        private readonly Drevaul m_owner;
        private long nextCast;
        private long nextManaDrain;
        private Random rng = new Random();

        public DrevaulBrain(Drevaul owner)
        {
            m_owner = owner;
            AggroLevel = 100;
            AggroRange = 1200;
            nextManaDrain = Environment.TickCount + 10000;
        }

        public override bool CheckSpells(eCheckSpellType type)
        {
            if (Body.TargetObject == null)
                return false;

            if (nextCast < Environment.TickCount)
            {
                // Randomly choose DD, Debuff, or DoT
                int choice = rng.Next(3);
                int spellId = DrevaulConfig.DD_ID;
                if (choice == 1) spellId = DrevaulConfig.Debuff_ID;
                else if (choice == 2) spellId = DrevaulConfig.DoT_ID;

                var spell = BossSpellHelper.GetSpell(spellId, Body.Level);
                if (spell != null)
                {
                    Body.CastSpell(spell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells));
                    nextCast = Environment.TickCount + Util.Random(5000, 8000);
                    return true;
                }
            }

            return false;
        }

        public override void Think()
        {
            base.Think();

            // Anti-mezz: if Drevaul is mezzed, heal to full
            if (Body.IsMezzed && Body.Health < Body.MaxHealth)
            {
                Body.Health = Body.MaxHealth;
                Body.Say("Drevaul shrugs off the mez and fully restores his strength!");
            }

            // Mana drain aura every 10s
            if (nextManaDrain < Environment.TickCount)
            {
                int drained = 0;

                foreach (GamePlayer player in Body.GetPlayersInRadius(1500))
                {
                    // Any caster in range gets drained, no matter what he targets
                    if (player != null && player.IsCasting)
                    {
                        player.Mana = 0;
                        Body.SayTo(player, "Drevaul drains all your magical energy!");
                        drained++;
                    }
                }

                if (drained > 0)
                    Body.Say($"Drevaul drains the magical energy of {drained} caster{(drained > 1 ? "s" : "")}!");

                nextManaDrain = Environment.TickCount + 10000;
            }
        }
    }
}
