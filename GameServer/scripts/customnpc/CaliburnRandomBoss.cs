using DOL.AI.Brain;
using DOL.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DOL.GS
{
    public class RandomBoss : GameNPC
    {
        static List<DbSpell> m_AllSpells = null;

        private class SpellChoice
        {
            public string Type;
            public int Weight;
            public Func<int, Spell> CreateSpell;

            public SpellChoice(string type, int weight, Func<int, Spell> creator)
            {
                Type = type;
                Weight = weight;
                CreateSpell = creator;
            }
        }

        public override bool AddToWorld()
        {
            if (m_AllSpells == null)
            {
                m_AllSpells = GameServer.Database.SelectAllObjects<DbSpell>().ToList();
            }

            SetOwnBrain(new RandomBossBrain());

            if (base.AddToWorld())
            {
                this.Spells = new List<Spell>();
                var spellChoices = new List<SpellChoice>
                {
                    new SpellChoice("DirectDamage", 40, CreateDD),
                    new SpellChoice("Stun", 15, CreateStun),
                    new SpellChoice("Bolt", 15, CreateBolt),
                    new SpellChoice("DoT", 20, CreateDoT),
                    // Add new spells here:
                    // new SpellChoice("Debuff", 10, CreateDebuff),
                    // new SpellChoice("Snare", 10, CreateSnare),
                    new SpellChoice("Disease", 15, CreateDisease),
                    new SpellChoice("DamageSpeedDecrease", 40, CreateDamageSpeedDecrease),
                    new SpellChoice("Heal", 15, CreateHeal),
                    new SpellChoice("Lifedrain", 30, CreateLifedrain),
                    new SpellChoice("StrengthConstitutionDebuff", 20, CreateStrengthConstitutionDebuff),
                    new SpellChoice("DexterityQuicknessDebuff", 20, CreateDexterityQuicknessDebuff),
                    new SpellChoice("HealOverTime", 15, CreateHealOverTime),
                    new SpellChoice("Mesmerize", 15, CreateMesmerize),
                    new SpellChoice("CombatSpeedDebuff", 20, CreateCombatSpeedDebuff),
                    new SpellChoice("CombatSpeedBuff", 15, CreateCombatSpeedBuff),
                };

                var selectedTypes = new HashSet<string>();

                int spellsToChoose = Util.Chance(70) ? 2 : 3; // Randomly choose 2 or 3 spells
                while (this.Spells.Count < spellsToChoose)
                {
                    var choice = GetRandomWeightedChoice(spellChoices.Where(s => !selectedTypes.Contains(s.Type)).ToList());
                    if (choice == null) break;

                    var spell = choice.CreateSpell(this.Level);
                    if (spell == null) continue;

                    this.Spells.Add(spell);
                    selectedTypes.Add(choice.Type);
                }

                this.SortSpells();
                return true;
            }

            return false;
        }

        private SpellChoice GetRandomWeightedChoice(List<SpellChoice> choices)
        {
            int totalWeight = choices.Sum(c => c.Weight);
            int rand = Util.Random(1, totalWeight);
            int current = 0;

            foreach (var choice in choices)
            {
                current += choice.Weight;
                if (rand <= current)
                    return choice;
            }

            return null;
        }

        public Spell CreateStun(int level)
        {
            for (int i = 0; i < 5; i++)
            {
                bool instant = Util.Chance(30);
                int duration = instant ? Util.Random(5, 10) : Util.Random(8, 14);
                DbSpell spell = new DbSpell();
                spell.AllowAdd = false;
                spell.CastTime = instant ? 0 : Util.RandomDouble() * 3;
                spell.Range = 2000;
                DbSpell effectSpell = m_AllSpells.Where(a =>
                    a.Type == eSpellType.Stun.ToString() &&
                    ((a.CastTime == 0 && instant) || (a.CastTime > 0 && !instant)) &&
                    (spell.IsRangedAoE() == a.IsRangedAoE() && spell.IsSingleTarget() == a.IsSingleTarget())
                ).OrderBy(a => Guid.NewGuid()).FirstOrDefault();

                if (effectSpell == null) continue;

                spell.ClientEffect = effectSpell.ClientEffect;
                spell.Icon = spell.ClientEffect;
                spell.TooltipId = (ushort)spell.ClientEffect;
                spell.Duration = duration;
                spell.Description = "Target is stunned and cannot move or take any other action for the duration of the spell.";
                spell.Name = "Stun";
                spell.SpellID = 11890;
                spell.Target = "Enemy";
                spell.Type = eSpellType.Stun.ToString();
                spell.Uninterruptible = spell.CastTime == 0 || Util.Chance(10);

                return new Spell(spell, level);
            }
            return null;
        }

        public Spell CreateDD(int level)
        {
            for (int i = 0; i < 5; i++)
            {
                bool instant = Util.Chance(30);
                DbSpell spell = new DbSpell();
                spell.AllowAdd = false;
                spell.CastTime = instant ? 0 : Math.Max(0.5, Util.RandomDouble() * 3);
                spell.RecastDelay = instant ? Util.Random(3, 12) : 0;
                spell.Radius = Util.Chance(50) ? 300 : 0;
                spell.Range = Util.Chance(80) ? 2000 : 0;

                DbSpell effectSpell = m_AllSpells.Where(a =>
                    a.Type == eSpellType.DirectDamage.ToString() &&
                    ((a.CastTime == 0 && instant) || (a.CastTime > 0 && !instant)) &&
                    (spell.IsRangedAoE() == a.IsRangedAoE() && spell.IsPBAoE() == a.IsPBAoE() && spell.IsSingleTarget() == a.IsSingleTarget())
                ).OrderBy(a => Guid.NewGuid()).FirstOrDefault();

                if (effectSpell == null) continue;

                spell.ClientEffect = effectSpell.ClientEffect;
                spell.Icon = spell.ClientEffect;
                spell.TooltipId = (ushort)spell.ClientEffect;
                spell.Damage = Util.Random(level * 3, level * 5);
                spell.Description = "Damage";
                spell.Name = "DirectDamage";
                spell.SpellID = 11890;
                spell.Target = "Enemy";
                spell.Type = eSpellType.DirectDamageNoVariance.ToString();
                spell.Uninterruptible = spell.CastTime == 0 || Util.Chance(10);

                return new Spell(spell, level);
            }
            return null;
        }

        public Spell CreateBolt(int level)
        {
            for (int i = 0; i < 5; i++)
            {
                bool instant = Util.Chance(30);
                DbSpell spell = new DbSpell();
                spell.AllowAdd = false;
                spell.CastTime = instant ? 0 : Math.Max(0.5, Util.RandomDouble() * 3);
                spell.RecastDelay = instant ? Util.Random(3, 12) : 0;
                spell.Radius = Util.Chance(50) ? 300 : 0;
                spell.Range = Util.Chance(80) ? 2000 : 0;

                DbSpell effectSpell = m_AllSpells.Where(a =>
                    a.Type == eSpellType.Bolt.ToString() &&
                    ((a.CastTime == 0 && instant) || (a.CastTime > 0 && !instant)) &&
                    (spell.IsRangedAoE() == a.IsRangedAoE() && spell.IsSingleTarget() == a.IsSingleTarget())
                ).OrderBy(a => Guid.NewGuid()).FirstOrDefault();

                if (effectSpell == null) continue;

                spell.ClientEffect = effectSpell.ClientEffect;
                spell.Icon = spell.ClientEffect;
                spell.TooltipId = (ushort)spell.ClientEffect;
                spell.Damage = Util.Random(level * 4, level * 6);
                spell.Description = "Bolt";
                spell.Name = "Bolt";
                spell.SpellID = 11890;
                spell.Target = "Enemy";
                spell.Type = eSpellType.Bolt.ToString();
                spell.Uninterruptible = spell.CastTime == 0 || Util.Chance(10);

                return new Spell(spell, level);
            }
            return null;
        }

        public Spell CreateDoT(int level)
        {
            for (int i = 0; i < 5; i++)
            {
                bool instant = Util.Chance(30);
                DbSpell spell = new DbSpell();
                spell.AllowAdd = false;
                spell.CastTime = instant ? 0 : Math.Max(0.5, Util.RandomDouble() * 3);
                spell.RecastDelay = instant ? Util.Random(3, 12) : 0;
                spell.Radius = Util.Chance(50) ? 300 : 0;
                spell.Range = Util.Chance(80) ? 2000 : 0;
                if (spell.Range == 0 && spell.Radius == 0) continue;

                DbSpell effectSpell = m_AllSpells.Where(a =>
                    a.Type == eSpellType.DamageOverTime.ToString() &&
                    ((a.CastTime == 0 && instant) || (a.CastTime > 0 && !instant)) &&
                    (spell.IsRangedAoE() == a.IsRangedAoE() && spell.IsSingleTarget() == a.IsSingleTarget())
                ).OrderBy(a => Guid.NewGuid()).FirstOrDefault();

                if (effectSpell == null) continue;

                spell.ClientEffect = effectSpell.ClientEffect;
                spell.Icon = spell.ClientEffect;
                spell.TooltipId = (ushort)spell.ClientEffect;
                spell.Damage = Util.Random(level * 4, level * 6);
                spell.Description = "DoT";
                spell.Name = "DoT";
                spell.SpellID = 11890;
                spell.Target = "Enemy";
                spell.Frequency = Util.Random(10, 40);
                spell.Duration = (spell.Frequency * Util.Random(6, 10)) / 10;
                spell.Type = eSpellType.DamageOverTime.ToString();
                spell.Uninterruptible = true;

                return new Spell(spell, level);
            }
            return null;
        }

        public Spell CreateDisease(int level)
        {
            for (int i = 1; i <= 5; i++)
            {
                bool instant = Util.Chance(30);
                int duration = instant ? Util.Random(20, 30) : Util.Random(30, 60);
                DbSpell spell = new DbSpell();
                spell.AllowAdd = false;
                spell.CastTime = instant ? 0 : Util.RandomDouble() * 3;
                spell.Radius = Util.Chance(50) ? 300 : 0;
                spell.Range = 2000;
                DbSpell effectSpell = m_AllSpells.Where(a =>
                {
                    return a.Type == eSpellType.Disease.ToString() && ((a.CastTime == 0 && instant) || (a.CastTime > 0 && !instant))
                    && (spell.IsRangedAoE() == a.IsRangedAoE() && spell.IsPBAoE() == a.IsPBAoE() && spell.IsSingleTarget() == a.IsSingleTarget());

                }).OrderBy(a => System.Guid.NewGuid().ToString()).FirstOrDefault();

                if (effectSpell == null) continue;
                spell.ClientEffect = effectSpell.ClientEffect;
                spell.Icon = spell.ClientEffect;
                spell.TooltipId = (ushort)spell.ClientEffect;
                spell.Duration = duration;
                spell.Description = "Disease";
                spell.Message1 = "You are diseased!";
                spell.Message2 = "{0} is diseased!";
                spell.Message3 = "You look healthy.";
                spell.Message4 = "{0} looks healthy again.";
                spell.Name = "Disease";
                spell.SpellID = 11890;
                spell.Target = "Enemy";
                spell.Type = eSpellType.Disease.ToString();
                spell.Uninterruptible = spell.CastTime == 0 || Util.Chance(10);
                return new Spell(spell, level);
            }
            return null;
        }

        public Spell CreateDamageSpeedDecrease(int level)
        {
            for (int i = 1; i <= 5; i++)
            {
                bool instant = Util.Chance(30);


                DbSpell spell = new DbSpell();
                spell.AllowAdd = false;
                spell.CastTime = instant ? 0 : Math.Max(0.5, Util.RandomDouble() * 3);
                spell.RecastDelay = instant ? Util.Random(3, 12) : 0;
                spell.Radius = Util.Chance(50) ? 300 : 0;
                spell.Range = Util.Chance(80) ? 2000 : 0;
                DbSpell effectSpell = m_AllSpells.Where(a =>
                {
                    return a.Type == eSpellType.DamageSpeedDecrease.ToString() && ((a.CastTime == 0 && instant) || (a.CastTime > 0 && !instant))
                    && (spell.IsRangedAoE() == a.IsRangedAoE() && spell.IsPBAoE() == a.IsPBAoE() && spell.IsSingleTarget() == a.IsSingleTarget());

                }).OrderBy(a => System.Guid.NewGuid().ToString()).FirstOrDefault();
                if (effectSpell == null) continue;

                spell.ClientEffect = effectSpell.ClientEffect;
                spell.Icon = spell.ClientEffect;
                spell.TooltipId = (ushort)spell.ClientEffect;
                spell.Damage = Util.Random(level * 3, level * 5);
                spell.Duration = 30;
                spell.Value = 40;
                spell.Description = "Damage";
                spell.Name = "DamageSpeedDecrease";
                spell.SpellID = 11890;
                spell.Target = "Enemy";
                spell.Type = eSpellType.DamageSpeedDecrease.ToString();
                spell.Uninterruptible = spell.CastTime == 0 || Util.Chance(10);
                spell.RecastDelay = Util.Random(5, 20);
                return new Spell(spell, level);
            }
            return null;
        }

        public Spell CreateHeal(int level)
        {
            for (int i = 1; i <= 5; i++)
            {
                bool instant = Util.Chance(30);
                DbSpell spell = new DbSpell();
                spell.AllowAdd = false;
                spell.CastTime = instant ? 0 : Util.RandomDouble() * 3;
                spell.Range = 2000;
                DbSpell effectSpell = m_AllSpells.Where(a =>
                {
                    return a.Type == eSpellType.Heal.ToString() && ((a.CastTime == 0 && instant) || (a.CastTime > 0 && !instant))
                    && (spell.IsRangedAoE() == a.IsRangedAoE() && spell.IsSingleTarget() == a.IsSingleTarget());

                }).OrderBy(a => System.Guid.NewGuid().ToString()).FirstOrDefault();

                if (effectSpell == null) continue;
                spell.ClientEffect = effectSpell.ClientEffect;
                spell.Icon = spell.ClientEffect;
                spell.TooltipId = (ushort)spell.ClientEffect;
                spell.RecastDelay = 8;
                spell.Value = Util.Random(level * 3, level * 5);
                spell.Description = "Heal";
                spell.Name = "Heal";
                spell.SpellID = 11890;
                spell.Target = "Self";
                spell.Type = eSpellType.Heal.ToString();
                spell.Uninterruptible = spell.CastTime == 0 || Util.Chance(10);
                return new Spell(spell, level);
            }
            return null;
        }

        public Spell CreateLifedrain(int level)
        {
            for (int i = 1; i <= 5; i++)
            {
                bool instant = Util.Chance(30);


                DbSpell spell = new DbSpell();
                spell.AllowAdd = false;
                spell.CastTime = instant ? 0 : Math.Max(0.5, Util.RandomDouble() * 3);
                spell.RecastDelay = instant ? Util.Random(3, 12) : 0;
                spell.Radius = Util.Chance(50) ? 300 : 0;
                spell.Range = Util.Chance(80) ? 2000 : 0;
                DbSpell effectSpell = m_AllSpells.Where(a =>
                {
                    return a.Type == eSpellType.Lifedrain.ToString() && ((a.CastTime == 0 && instant) || (a.CastTime > 0 && !instant))
                    && (spell.IsRangedAoE() == a.IsRangedAoE() && spell.IsPBAoE() == a.IsPBAoE() && spell.IsSingleTarget() == a.IsSingleTarget());

                }).OrderBy(a => System.Guid.NewGuid().ToString()).FirstOrDefault();
                if (effectSpell == null) continue;

                spell.ClientEffect = effectSpell.ClientEffect;
                spell.Icon = spell.ClientEffect;
                spell.TooltipId = (ushort)spell.ClientEffect;
                spell.Damage = Util.Random(level * 3, level * 5);
                spell.Value = -30;
                spell.LifeDrainReturn = 30;
                spell.Description = "Damage";
                spell.Name = "Lifedrain";
                spell.SpellID = 11890;
                spell.Target = "Enemy";
                spell.Type = eSpellType.Lifedrain.ToString();
                spell.Uninterruptible = spell.CastTime == 0 || Util.Chance(10);
                spell.RecastDelay = Util.Random(5, 20);
                return new Spell(spell, level);
            }
            return null;
        }

        public Spell CreateStrengthConstitutionDebuff(int level)
        {
            for (int i = 1; i <= 5; i++)
            {
                bool instant = Util.Chance(30);


                DbSpell spell = new DbSpell();
                spell.AllowAdd = false;
                spell.CastTime = instant ? 0 : Math.Max(0.5, Util.RandomDouble() * 3);
                spell.Radius = Util.Chance(50) ? 300 : 0;
                spell.Range = Util.Chance(80) ? 2000 : 0;
                DbSpell effectSpell = m_AllSpells.Where(a =>
                {
                    return a.Type == eSpellType.StrengthConstitutionDebuff.ToString() && ((a.CastTime == 0 && instant) || (a.CastTime > 0 && !instant))
                    && (spell.IsRangedAoE() == a.IsRangedAoE() && spell.IsSingleTarget() == a.IsSingleTarget());

                }).OrderBy(a => System.Guid.NewGuid().ToString()).FirstOrDefault();
                if (effectSpell == null) continue;
                spell.ClientEffect = effectSpell.ClientEffect;
                spell.Icon = spell.ClientEffect;
                spell.TooltipId = (ushort)spell.ClientEffect;
                spell.RecastDelay = 60;
                spell.Duration = 60;
                spell.Value = Util.Random(level * 1, level * 2);
                spell.Description = "StrengthConstitutionDebuff";
                spell.Name = "StrengthConstitutionDebuff";
                spell.SpellID = 11890;
                spell.Target = "Enemy";
                spell.Type = eSpellType.StrengthConstitutionDebuff.ToString();
                spell.Uninterruptible = spell.CastTime == 0 || Util.Chance(10);
                return new Spell(spell, level);
            }
            return null;
        }

        public Spell CreateDexterityQuicknessDebuff(int level)
        {
            for (int i = 1; i <= 5; i++)
            {
                bool instant = Util.Chance(30);


                DbSpell spell = new DbSpell();
                spell.AllowAdd = false;
                spell.CastTime = instant ? 0 : Math.Max(0.5, Util.RandomDouble() * 3);
                spell.Radius = Util.Chance(50) ? 300 : 0;
                spell.Range = Util.Chance(80) ? 2000 : 0;
                DbSpell effectSpell = m_AllSpells.Where(a =>
                {
                    return a.Type == eSpellType.DexterityQuicknessDebuff.ToString() && ((a.CastTime == 0 && instant) || (a.CastTime > 0 && !instant))
                    && (spell.IsRangedAoE() == a.IsRangedAoE() && spell.IsSingleTarget() == a.IsSingleTarget());

                }).OrderBy(a => System.Guid.NewGuid().ToString()).FirstOrDefault();
                if (effectSpell == null) continue;
                spell.ClientEffect = effectSpell.ClientEffect;
                spell.Icon = spell.ClientEffect;
                spell.TooltipId = (ushort)spell.ClientEffect;
                spell.RecastDelay = 60;
                spell.Duration = 60;
                spell.Value = Util.Random(level * 1, level * 2);
                spell.Description = "DexterityQuicknessDebuff";
                spell.Name = "DexterityQuicknessDebuff";
                spell.SpellID = 11890;
                spell.Target = "Enemy";
                spell.Type = eSpellType.DexterityQuicknessDebuff.ToString();
                spell.Uninterruptible = spell.CastTime == 0 || Util.Chance(10);
                return new Spell(spell, level);
            }
            return null;
        }

        public Spell CreateHealOverTime(int level)
        {
            for (int i = 1; i <= 5; i++)
            {
                bool instant = Util.Chance(30);
                DbSpell spell = new DbSpell();
                spell.AllowAdd = false;
                spell.CastTime = instant ? 0 : Util.RandomDouble() * 3;
                spell.Range = 2000;
                DbSpell effectSpell = m_AllSpells.Where(a =>
                {
                    return a.Type == eSpellType.HealOverTime.ToString() && ((a.CastTime == 0 && instant) || (a.CastTime > 0 && !instant))
                    && (spell.IsRangedAoE() == a.IsRangedAoE() && spell.IsSingleTarget() == a.IsSingleTarget());

                }).OrderBy(a => System.Guid.NewGuid().ToString()).FirstOrDefault();

                if (effectSpell == null) continue;
                spell.ClientEffect = effectSpell.ClientEffect;
                spell.Icon = spell.ClientEffect;
                spell.TooltipId = (ushort)spell.ClientEffect;
                spell.Duration = 10;
                spell.Frequency = 20;
                spell.RecastDelay = 45;
                spell.Value = Util.Random(level * 3, level * 5);
                spell.Description = "HealOverTime";
                spell.Name = "HealOverTime";
                spell.Message1 = "You start healing faster.";
                spell.Message2 = "{0} starts healing faster.";
                spell.SpellID = 11890;
                spell.Target = "Self";
                spell.Type = eSpellType.HealOverTime.ToString();
                spell.Uninterruptible = spell.CastTime == 0 || Util.Chance(10);
                return new Spell(spell, level);
            }
            return null;
        }

        public Spell CreateMesmerize(int level)
        {
            for (int i = 1; i <= 5; i++)
            {
                bool instant = Util.Chance(30);
                int duration = instant ? Util.Random(15, 35) : Util.Random(35, 65);
                DbSpell spell = new DbSpell();
                spell.AllowAdd = false;
                spell.CastTime = instant ? 0 : Util.RandomDouble() * 3;
                spell.Radius = Util.Chance(50) ? 300 : 0;
                spell.Range = 2000;
                DbSpell effectSpell = m_AllSpells.Where(a =>
                {
                    return a.Type == eSpellType.Mesmerize.ToString() && ((a.CastTime == 0 && instant) || (a.CastTime > 0 && !instant))
                    && (spell.IsRangedAoE() == a.IsRangedAoE() && spell.IsSingleTarget() == a.IsSingleTarget());

                }).OrderBy(a => System.Guid.NewGuid().ToString()).FirstOrDefault();

                if (effectSpell == null) continue;
                spell.ClientEffect = effectSpell.ClientEffect;
                spell.Icon = spell.ClientEffect;
                spell.TooltipId = (ushort)spell.ClientEffect;
                spell.Duration = duration;
                spell.Description = "Target is mesmerized and cannot move or take any other action for the duration of the spell.";
                spell.Name = "Mesmerize";
                spell.SpellID = 11890;
                spell.Target = "Enemy";
                spell.Type = eSpellType.Mesmerize.ToString();
                spell.Uninterruptible = spell.CastTime == 0 || Util.Chance(10);
                return new Spell(spell, level);
            }
            return null;
        }

        public Spell CreateCombatSpeedDebuff(int level)
        {
            for (int i = 1; i <= 5; i++)
            {
                bool instant = Util.Chance(30);


                DbSpell spell = new DbSpell();
                spell.AllowAdd = false;
                spell.CastTime = instant ? 0 : Math.Max(0.5, Util.RandomDouble() * 3);
                spell.Radius = Util.Chance(50) ? 300 : 0;
                spell.Range = Util.Chance(80) ? 2000 : 0;
                DbSpell effectSpell = m_AllSpells.Where(a =>
                {
                    return a.Type == eSpellType.CombatSpeedDebuff.ToString() && ((a.CastTime == 0 && instant) || (a.CastTime > 0 && !instant))
                    && (spell.IsRangedAoE() == a.IsRangedAoE() && spell.IsSingleTarget() == a.IsSingleTarget());

                }).OrderBy(a => System.Guid.NewGuid().ToString()).FirstOrDefault();
                if (effectSpell == null) continue;
                spell.ClientEffect = effectSpell.ClientEffect;
                spell.Icon = spell.ClientEffect;
                spell.TooltipId = (ushort)spell.ClientEffect;
                spell.RecastDelay = 30;
                spell.Duration = 60;
                spell.Value = level * (0.3 + Util.RandomDouble() * 0.2);
                spell.Description = "CombatSpeedDebuff";
                spell.Name = "CombatSpeedDebuff";
                spell.SpellID = 11890;
                spell.Target = "Enemy";
                spell.Type = eSpellType.CombatSpeedDebuff.ToString();
                spell.Uninterruptible = spell.CastTime == 0 || Util.Chance(10);
                return new Spell(spell, level);
            }
            return null;
        }

        public Spell CreateCombatSpeedBuff(int level)
        {
            for (int i = 1; i <= 5; i++)
            {
                bool instant = Util.Chance(30);
                DbSpell spell = new DbSpell();
                spell.AllowAdd = false;
                spell.CastTime = instant ? 0 : Util.RandomDouble() * 3;
                spell.Range = 2000;
                DbSpell effectSpell = m_AllSpells.Where(a =>
                {
                    return a.Type == eSpellType.CombatSpeedBuff.ToString() && ((a.CastTime == 0 && instant) || (a.CastTime > 0 && !instant))
                    && (spell.IsRangedAoE() == a.IsRangedAoE() && spell.IsSingleTarget() == a.IsSingleTarget());

                }).OrderBy(a => System.Guid.NewGuid().ToString()).FirstOrDefault();

                if (effectSpell == null) continue;
                spell.ClientEffect = effectSpell.ClientEffect;
                spell.Icon = spell.ClientEffect;
                spell.TooltipId = (ushort)spell.ClientEffect;
                spell.Duration = 20;
                spell.RecastDelay = 35;
                spell.Value = level * (0.3 + Util.RandomDouble() * 0.2);
                spell.Description = "CombatSpeedBuff";
                spell.Name = "CombatSpeedBuff";
                spell.Message2 = "{0} begins attacking faster!";
                spell.Message4 = "{0}'s attacks return to normal.";
                spell.SpellID = 11890;
                spell.Target = "Self";
                spell.Type = eSpellType.CombatSpeedBuff.ToString();
                spell.Uninterruptible = spell.CastTime == 0 || Util.Chance(10);
                return new Spell(spell, level);
            }
            return null;
        }

        public override bool Interact(GamePlayer player)
        {
            string text = $"\nThese are the spells I will be using:\n\n";

            foreach (var spell in this.Spells)
            {
                text += $"{spell.SpellType}:\n";
                if (spell.Duration > 0) text += $"     {(spell.Duration / 1000)}s duration\n";
                if (spell.CastTime > 0) text += $"     {(spell.CastTime / 1000)}s cast time\n";
                if (spell.CastTime == 0) text += $"     instant cast time\n";
                if (spell.Damage > 0) text += $"     {spell.Damage} damage\n";
                if (spell.Frequency > 0) text += $"     {spell.Frequency / 1000}s frequency\n";
                if (spell.Radius > 0) text += $"     {spell.Radius} radius\n";
                if (spell.Range > 0) text += $"     {spell.Range} range\n";
                if (spell.RecastDelay > 0) text += $"     {(spell.RecastDelay / 1000)}s recast time\n";
                text += "\n";
            }

            SayTo(player, text);
            return base.Interact(player);
        }
    }

    public class RandomBossBrain : StandardMobBrain
    {
        public override int AggroRange { get => 1000; set => base.AggroRange = value; }
        public override int AggroLevel { get => 100; set => base.AggroLevel = value; }
        public override bool CheckSpells(eCheckSpellType type)
        {
            return base.CheckSpells(type);
        }
    }

    public static class BlackthornExtensions
    {
        public static bool IsPBAoE(this DbSpell spell)
        {
            return spell.Radius > 0 && spell.Range == 0;
        }
        public static bool IsRangedAoE(this DbSpell spell)
        {
            return spell.Radius > 0 && spell.Range > 0;
        }
        public static bool IsSingleTarget(this DbSpell spell)
        {
            return spell.Radius == 0 && spell.Range > 0;
        }
    }
}
