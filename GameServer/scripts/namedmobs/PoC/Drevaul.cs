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
        public static readonly int DD_ID = 40022;         // Cold DD
        public static readonly int Debuff_ID = 4385;      // Str/Con Debuff
        public static readonly int DoT_ID = 32109;        // Cold DoT-like
        public static readonly int SpecialBurstDD_ID = 32125; // Strong Cold DD
    }

    public class Drevaul : GameNPC
    {
        private HashSet<int> burstUsedAt = new HashSet<int>();

        public override bool AddToWorld()
        {
            if (!base.AddToWorld()) return false;

            Level = 80;
            Name = "Drevaul";
            Model = 645;
            Size = 120;
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
                    var spell = SkillBase.GetSpellByID(DrevaulConfig.SpecialBurstDD_ID);
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
        }

        public override bool CheckSpells(eCheckSpellType type)
        {
            if (Body.TargetObject == null || !(Body.TargetObject is GameLiving target))
                return false;

            if (nextCast < Environment.TickCount)
            {
                // Randomly choose DD, Debuff, or DoT
                int choice = rng.Next(3);
                int spellId = DrevaulConfig.DD_ID;
                if (choice == 1) spellId = DrevaulConfig.Debuff_ID;
                else if (choice == 2) spellId = DrevaulConfig.DoT_ID;

                var spell = SkillBase.GetSpellByID(spellId);
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
            foreach (var effect in Body.EffectList)
            {
                if (effect is GameSpellEffect gse && gse.SpellHandler != null)
                {
                    if (gse.SpellHandler.Spell.SpellType == eSpellType.Mez)
                    {
                        Body.Health = Body.MaxHealth;
                        Body.Say("Drevaul shrugs off the mez and fully restores his strength!");
                        break;
                    }
                }
            }

            // Mana drain aura every 10s
            if (nextManaDrain < Environment.TickCount)
            {
                foreach (GamePlayer player in Body.GetPlayersInRadius(1500))
                {
                    if (player != null && player.IsCasting && player.TargetObject == Body.TargetObject)
                    {
                        player.Mana = 0;
                        Body.SayTo(player, "Drevaul drains all your magical energy!");
                    }
                }
                nextManaDrain = Environment.TickCount + 10000;
            }
        }
    }
}
