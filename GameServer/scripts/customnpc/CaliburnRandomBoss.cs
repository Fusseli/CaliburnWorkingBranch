using DOL.AI.Brain;
using DOL.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DOL.GS
{
    public class RandomBoss: GameNPC
    {
        /*
        public override int RespawnInterval
        {
            get
            {
                return 3000;
            }
        }*/
        static List<DbSpell> m_AllSpells = null;

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

                while (this.Spells.Count < 2)
                {
                    int rand = Util.Random(1, 100);
                    if (rand < 40)
                    {
                        if (this.Spells.Exists(a => a.SpellType == eSpellType.DirectDamageNoVariance)) continue;
                        var dd = CreateDD(this.Level);
                        if (dd == null) continue;
                        this.Spells.Add(dd);
                    } else if (rand < 55)
                    {
                        if (this.Spells.Exists(a => a.SpellType == eSpellType.Stun)) continue;
                        var stun = CreateStun(this.Level);
                        if (stun == null) continue;
                        this.Spells.Add(stun);
                    }
                    else if (rand < 70)
                    {
                        if (this.Spells.Exists(a => a.SpellType == eSpellType.Bolt)) continue;
                        var bolt = CreateBolt(this.Level);
                        if (bolt == null) continue;
                        this.Spells.Add(bolt);
                    }
                    else if (rand < 90)
                    {
                        if (this.Spells.Exists(a => a.SpellType == eSpellType.DamageOverTime)) continue;
                        var dot = CreateDoT(this.Level);
                        if (dot == null) continue;
                        this.Spells.Add(dot);
                    }
                }

                this.SortSpells();
                return true;
            }
            return false;
        }

        public Spell CreateStun(int level)
        {
            for (int i = 1; i <= 5; i++)
            {
                bool instant = Util.Chance(30);
                int duration = instant ? Util.Random(5, 10) : Util.Random(8, 14);
                DbSpell spell = new DbSpell();
                spell.AllowAdd = false;
                spell.CastTime = instant ? 0 : Util.RandomDouble() * 3;
                spell.Range = 2000;
                DbSpell effectSpell = m_AllSpells.Where(a =>
                {
                    return a.Type == eSpellType.Stun.ToString() && ((a.CastTime == 0 && instant) || (a.CastTime > 0 && !instant))
                    && (spell.IsRangedAoE() == a.IsRangedAoE() && spell.IsSingleTarget() == a.IsSingleTarget());

                }).OrderBy(a => System.Guid.NewGuid().ToString()).FirstOrDefault();

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
                    return a.Type == eSpellType.DirectDamage.ToString() && ((a.CastTime == 0 && instant) || (a.CastTime > 0 && !instant))
                    && (spell.IsRangedAoE() == a.IsRangedAoE() && spell.IsPBAoE() == a.IsPBAoE() && spell.IsSingleTarget() == a.IsSingleTarget());

                }).OrderBy(a => System.Guid.NewGuid().ToString()).FirstOrDefault();
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
                    return a.Type == eSpellType.Bolt.ToString() && ((a.CastTime == 0 && instant) || (a.CastTime > 0 && !instant))
                    && (spell.IsRangedAoE() == a.IsRangedAoE() && spell.IsSingleTarget() == a.IsSingleTarget());

                }).OrderBy(a => System.Guid.NewGuid().ToString()).FirstOrDefault();
                if (effectSpell == null) continue;
                spell.ClientEffect = effectSpell.ClientEffect;
                spell.Icon = spell.ClientEffect;
                spell.TooltipId = (ushort)spell.ClientEffect;
                spell.Damage = Util.Random(level * 4, level * 6);
                spell.Description = "Damage";
                spell.Name = "Bolt";
                spell.SpellID = 11890;
                spell.Target = "Enemy";
                spell.Type = eSpellType.Bolt.ToString();
                spell.Uninterruptible = spell.CastTime == 0 || Util.Chance(10);
                spell.RecastDelay = Util.Random(5, 20);
                return new Spell(spell, level);
            }
            return null;
        }
        public Spell CreateDoT(int level)
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
                if (spell.Range == 0 && spell.Radius == 0) continue;
                DbSpell effectSpell = m_AllSpells.Where(a =>
                {
                    return a.Type == eSpellType.DamageOverTime.ToString() && ((a.CastTime == 0 && instant) || (a.CastTime > 0 && !instant))
                    && (spell.IsRangedAoE() == a.IsRangedAoE() && spell.IsSingleTarget() == a.IsSingleTarget());

                }).OrderBy(a => System.Guid.NewGuid().ToString()).FirstOrDefault();
                if (effectSpell == null) continue;
                spell.ClientEffect = effectSpell.ClientEffect;
                spell.Icon = spell.ClientEffect;
                spell.TooltipId = (ushort)spell.ClientEffect;
                spell.Damage = Util.Random(level * 4, level * 6);
                spell.Description = "Damage";
                spell.Name = "DoT";
                spell.SpellID = 11890;
                spell.Target = "Enemy";
                spell.Frequency = Util.Random(10, 40);
                spell.Duration = (spell.Frequency * Util.Random(6, 10)) / 10;
                spell.Type = eSpellType.DamageOverTime.ToString();
                spell.Uninterruptible = true;
                spell.RecastDelay = instant ? Util.Random(5, 20) : 0;
                return new Spell(spell, level);
            }
            return null;
        }
        public override bool Interact(GamePlayer player)
        {
            string text = $"\nThese are the spells I will be using:\n\n";
            List<string> spellLines = new List<string>();
            foreach (var spell in this.Spells)
            {
                text += $"{spell.SpellType.ToString()}:\n";
                if (spell.Duration > 0) text += $"     {(spell.Duration / 1000)}s duration\n";
                if (spell.CastTime > 0) text += $"     {(spell.CastTime / 1000)}s cast time\n";
                if (spell.CastTime == 0) text += $"     instant cast time\n";
                if (spell.Damage > 0) text += $"     {spell.Damage} damage\n";
                if (spell.Frequency > 0) text += $"     {spell.Frequency / 1000}s frequency\n";
                if (spell.Radius > 0) text += $"     {spell.Radius} radius\n";
                if (spell.Range > 0) text += $"     {spell.Range} range\n";
                if (spell.CastTime > 0) text += $"     {(spell.RecastDelay / 1000)}s recast time\n";
                text += "\n\n";
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
