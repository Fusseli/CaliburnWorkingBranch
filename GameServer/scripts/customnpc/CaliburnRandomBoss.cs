using DOL.AI.Brain;
using DOL.Database;
using DOL.GS.SkillHandler;
using DOL.GS.Spells;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using System.Reflection;

namespace DOL.GS
{
    public class CaliburnRandomBoss : GameNPC
    {
        private static new readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
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
                try
                {
                    m_AllSpells = GameServer.Database.SelectAllObjects<DbSpell>().ToList();
                    if (m_AllSpells == null || m_AllSpells.Count == 0)
                    {
                        log.Error("CaliburnRandomBoss: Failed to load spells from database!");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"CaliburnRandomBoss: Error loading spells from database: {ex.Message}");
                    return false;
                }
            }

            SetOwnBrain(new CaliburnRandomBossBrain());

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
                spell.Radius = Util.Chance(30) ? 300 : 0; // Allow AoE stuns
                spell.Range = 2000; // Always have range for stuns
                DbSpell effectSpell = m_AllSpells.Where(a =>
                    a.Type == eSpellType.Stun.ToString() &&
                    ((a.CastTime == 0 && instant) || (a.CastTime > 0 && !instant)) &&
                    (spell.IsRangedAoE() == a.IsRangedAoE() && spell.IsPBAoE() == a.IsPBAoE() && spell.IsSingleTarget() == a.IsSingleTarget())
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
                spell.Duration = Math.Max(1, (spell.Frequency * Util.Random(6, 10)) / 10); // Ensure minimum 1 second duration
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

    public class CaliburnRandomBossBrain : StandardMobBrain
    {
        private const int HEAL_THRESHOLD = 75; // Heal when HP drops below 75%
        private const long CAST_TIME_BUFFER = 500; // Buffer in milliseconds to start casting before effect expires
        private bool m_buffsAppliedOnSpawn = false;
        private long m_lastBuffCheck = 0;
        private const long BUFF_CHECK_INTERVAL = 2000; // Check buffs every 2 seconds

        public override int AggroRange { get => 1000; set => base.AggroRange = value; }
        public override int AggroLevel { get => 100; set => base.AggroLevel = value; }

        public override void Think()
        {
            base.Think();

            if (Body == null || !Body.IsAlive)
                return;

            long currentTime = GameLoop.GameLoopTime;

            // Apply buffs on spawn (only once)
            if (!m_buffsAppliedOnSpawn)
            {
                if (Body.IsAlive)
                {
                    ApplyBuffsOnSpawn();
                }
                m_buffsAppliedOnSpawn = true; // Set flag even if Body is dead to prevent repeated checks
            }

            // Periodically check and refresh buffs
            if (currentTime - m_lastBuffCheck > BUFF_CHECK_INTERVAL)
            {
                CheckAndRefreshBuffs();
                m_lastBuffCheck = currentTime;
            }
        }

        /// <summary>
        /// Apply self-buffs on spawn (like damage shields, stat buffs, etc.)
        /// </summary>
        private void ApplyBuffsOnSpawn()
        {
            if (Body?.Spells == null || Body.IsCasting)
                return;

            foreach (Spell spell in Body.Spells)
            {
                // Only apply self-targeted buffs that aren't heals
                if (spell.Target == eSpellTarget.SELF && 
                    spell.SpellType != eSpellType.Heal && 
                    spell.SpellType != eSpellType.HealOverTime)
                {
                    // Check if the buff is already applied
                    if (!LivingHasEffect(Body, spell))
                    {
                        // Check if spell is on cooldown
                        if (Body.GetSkillDisabledDuration(spell) > 0)
                            continue;
                        
                        Body.TargetObject = Body;
                        if (Body.CastSpell(spell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells)))
                        {
                            // Only cast one spell at a time, break after successful cast
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Check buffs and heals (HoTs) and refresh them if they're about to expire (accounting for cast time)
        /// </summary>
        private void CheckAndRefreshBuffs()
        {
            if (Body?.Spells == null || !Body.IsAlive || Body.IsCasting)
                return;

            foreach (Spell spell in Body.Spells)
            {
                // Refresh self-targeted buffs and HoTs (but not instant heals)
                if (spell.Target == eSpellTarget.SELF && 
                    spell.SpellType != eSpellType.Heal && // Skip instant heals (they're handled in CheckSpells)
                    spell.Duration > 0)
                {
                    // Check if buff/HoT exists and is about to expire
                    eEffect spellEffect = EffectService.GetEffectFromSpell(spell);
                    if (spellEffect != eEffect.Unknown)
                    {
                        ECSGameSpellEffect effect = EffectListService.GetSpellEffectOnTarget(Body, spellEffect);
                        if (effect != null && !effect.IsDisabled && effect.ExpireTick > GameLoop.GameLoopTime)
                        {
                            // Calculate remaining duration in milliseconds
                            long remainingDuration = effect.ExpireTick - GameLoop.GameLoopTime;
                            
                            // Get cast time in milliseconds (already stored in milliseconds)
                            long castTime = spell.CastTime;
                            
                            // Start casting when remaining duration <= cast time + buffer
                            // This ensures the buff/HoT is refreshed right as it expires (or just before)
                            if (remainingDuration > 0 && remainingDuration <= (castTime + CAST_TIME_BUFFER))
                            {
                                // Refresh the buff or HoT
                                // For HoTs, only refresh if HP is below threshold
                                if (spell.SpellType == eSpellType.HealOverTime && Body.HealthPercent >= HEAL_THRESHOLD)
                                    continue;
                                
                                // Check if spell is on cooldown
                                if (Body.GetSkillDisabledDuration(spell) > 0)
                                    continue;
                                
                                bool canCast = spell.SpellType == eSpellType.HealOverTime 
                                    ? Body.CanCastHealSpells 
                                    : Body.CanCastMiscSpells;
                                
                                if (canCast && !Body.IsCasting)
                                {
                                    Body.TargetObject = Body;
                                    if (Body.CastSpell(spell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells)))
                                    {
                                        // Only cast one spell at a time
                                        break;
                                    }
                                }
                            }
                        }
                        else if (!LivingHasEffect(Body, spell))
                        {
                            // Buff/HoT is missing, reapply it
                            // For HoTs, only reapply if HP is below threshold
                            if (spell.SpellType == eSpellType.HealOverTime)
                            {
                                if (Body.HealthPercent < HEAL_THRESHOLD && Body.CanCastHealSpells && !Body.IsCasting)
                                {
                                    // Check if spell is on cooldown
                                    if (Body.GetSkillDisabledDuration(spell) > 0)
                                        continue;
                                    
                                    Body.TargetObject = Body;
                                    if (Body.CastSpell(spell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells)))
                                    {
                                        // Only cast one spell at a time
                                        break;
                                    }
                                }
                            }
                            // For buffs: apply combat buffs during combat, others when not in combat
                            else if (spell.SpellType == eSpellType.CombatSpeedBuff && Body.InCombat)
                            {
                                // Apply combat buffs during combat
                                if (Body.CanCastMiscSpells && !Body.IsCasting)
                                {
                                    // Check if spell is on cooldown
                                    if (Body.GetSkillDisabledDuration(spell) > 0)
                                        continue;
                                    
                                    Body.TargetObject = Body;
                                    if (Body.CastSpell(spell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells)))
                                    {
                                        // Only cast one spell at a time
                                        break;
                                    }
                                }
                            }
                            else if (!Body.InCombat)
                            {
                                // Apply non-combat buffs when not in combat
                                if (Body.CanCastMiscSpells && !Body.IsCasting)
                                {
                                    // Check if spell is on cooldown
                                    if (Body.GetSkillDisabledDuration(spell) > 0)
                                        continue;
                                    
                                    Body.TargetObject = Body;
                                    if (Body.CastSpell(spell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells)))
                                    {
                                        // Only cast one spell at a time
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public override bool CheckSpells(eCheckSpellType type)
        {
            if (Body == null || Body.Spells == null || Body.Spells.Count == 0)
                return false;

            // Handle defensive spells (heals, self-buffs)
            if (type == eCheckSpellType.Defensive)
            {
                // Check for healing spells first (priority)
                if (Body.HealthPercent < HEAL_THRESHOLD)
                {
                    foreach (Spell spell in Body.Spells)
                    {
                        if ((spell.SpellType == eSpellType.Heal || spell.SpellType == eSpellType.HealOverTime) &&
                            spell.Target == eSpellTarget.SELF)
                        {
                            if (Body.CanCastHealSpells && !Body.IsCasting)
                            {
                                // For HoT: check if it exists and needs refresh (accounting for cast time)
                                if (spell.SpellType == eSpellType.HealOverTime)
                                {
                                    eEffect spellEffect = EffectService.GetEffectFromSpell(spell);
                                    if (spellEffect != eEffect.Unknown)
                                    {
                                        ECSGameSpellEffect effect = EffectListService.GetSpellEffectOnTarget(Body, spellEffect);
                                        if (effect != null && !effect.IsDisabled && effect.ExpireTick > GameLoop.GameLoopTime)
                                        {
                                            // Calculate remaining duration in milliseconds
                                            long remainingDuration = effect.ExpireTick - GameLoop.GameLoopTime;
                                            
                                            // Get cast time in milliseconds
                                            long castTime = spell.CastTime;
                                            
                                            // Refresh HoT when remaining duration <= cast time + buffer
                                            if (remainingDuration > 0 && remainingDuration <= (castTime + CAST_TIME_BUFFER))
                                            {
                                                // Check if spell is on cooldown
                                                if (Body.GetSkillDisabledDuration(spell) == 0)
                                                {
                                                    Body.TargetObject = Body;
                                                    if (Body.CastSpell(spell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells)))
                                                        return true;
                                                }
                                            }
                                            // If HoT still has time, don't cast another one
                                            continue;
                                        }
                                    }
                                    // If HoT doesn't exist, we'll cast it below
                                }

                                // For regular Heal or missing HoT, cast it
                                // Check if spell is on cooldown
                                if (Body.GetSkillDisabledDuration(spell) == 0)
                                {
                                    Body.TargetObject = Body;
                                    if (Body.CastSpell(spell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells)))
                                        return true;
                                }
                            }
                        }
                    }
                }

                // Check for combat buffs during combat
                if (Body.InCombat)
                {
                    foreach (Spell spell in Body.Spells)
                    {
                        if (spell.Target == eSpellTarget.SELF && 
                            spell.SpellType == eSpellType.CombatSpeedBuff &&
                            !LivingHasEffect(Body, spell))
                        {
                            if (Body.CanCastMiscSpells && !Body.IsCasting)
                            {
                                // Check if spell is on cooldown
                                if (Body.GetSkillDisabledDuration(spell) == 0)
                                {
                                    Body.TargetObject = Body;
                                    if (Body.CastSpell(spell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells)))
                                        return true;
                                }
                            }
                        }
                    }
                }
            }

            // Call base implementation for offensive spells and other defensive spells
            return base.CheckSpells(type);
        }

        protected override GameLiving FindTargetForDefensiveSpell(Spell spell)
        {
            // Handle self-targeted defensive spells
            if (spell.Target == eSpellTarget.SELF)
            {
                // Check if spell is on cooldown first
                if (Body.GetSkillDisabledDuration(spell) > 0)
                    return null;
                
                // Heals: only cast when HP is below threshold
                if (spell.SpellType == eSpellType.Heal || spell.SpellType == eSpellType.HealOverTime)
                {
                    if (Body.HealthPercent < HEAL_THRESHOLD)
                    {
                        // For HoT: check if it needs refresh (accounting for cast time)
                        if (spell.SpellType == eSpellType.HealOverTime)
                        {
                            eEffect spellEffect = EffectService.GetEffectFromSpell(spell);
                            if (spellEffect != eEffect.Unknown)
                            {
                                ECSGameSpellEffect effect = EffectListService.GetSpellEffectOnTarget(Body, spellEffect);
                                if (effect != null && !effect.IsDisabled && effect.ExpireTick > GameLoop.GameLoopTime)
                                {
                                    // Calculate remaining duration in milliseconds
                                    long remainingDuration = effect.ExpireTick - GameLoop.GameLoopTime;
                                    
                                    // Get cast time in milliseconds
                                    long castTime = spell.CastTime;
                                    
                                    // Refresh HoT when remaining duration <= cast time + buffer
                                    if (remainingDuration > 0 && remainingDuration <= (castTime + CAST_TIME_BUFFER))
                                        return Body;
                                    
                                    // HoT still has time, don't cast another one
                                    return null;
                                }
                            }
                        }
                        
                        // For regular Heal or missing HoT, cast it if not present
                        if (!LivingHasEffect(Body, spell))
                            return Body;
                    }
                    return null;
                }

                // Combat buffs: apply during combat
                if (spell.SpellType == eSpellType.CombatSpeedBuff)
                {
                    if (Body.InCombat && !LivingHasEffect(Body, spell))
                        return Body;
                    return null;
                }

                // Other self-buffs: apply if not present
                if (!LivingHasEffect(Body, spell))
                    return Body;
                
                return null;
            }

            // For non-self spells, use base implementation
            return base.FindTargetForDefensiveSpell(spell);
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
