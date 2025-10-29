// Zilistiphle.cs
// Standalone final boss script for OpenDAoC
// - Phase 1: normal fight (100% -> 40%)
// - Phase 2: Invulnerable, shield aura loop, summons guardians
// - Phase 3: Fight resumes at 40% with faster spells
// Author: ChatGPT custom rewrite

using System;
using System.Collections.Generic;
using System.Linq;
using DOL.AI.Brain;
using DOL.GS;
using DOL.GS.Spells;

namespace DOL.GS.CustomBosses
{
    public static class ZilistiphleConfig
    {
        // Stronger final boss spells
        public static readonly int EnergyDD_ID = 9550;   // energy nuke
        public static readonly int BodyDoT_ID = 9570;   // strong body DoT
        public static readonly int Debuff_ID = 9560;   // str/con debuff

        // Aura shield visual
        public static readonly int AuraSpellID = 4309;   // Greater Powerguard

        // Guardians
        public static readonly string Guardian1 = "Pheton";
        public static readonly string Guardian2 = "Busiv";
        public static readonly string Guardian3 = "Drevaul";
    }

    public class Zilistiphle : GameNPC
    {
        private ZilistiphleBrain _brain;
        private bool _invulnerable = false;

        public override bool AddToWorld()
        {
            if (!base.AddToWorld()) return false;

            Level = 85;
            Name = "Zilistiphle";
            Model = 660;
            Size = 150;
            MaxSpeedBase = 200;
            Realm = 0;

            _brain = new ZilistiphleBrain(this);
            SetOwnBrain(_brain);

            Say("You dare challenge me? Foolish mortals!");
            return true;
        }

        public override int MaxHealth => base.MaxHealth * 8;

        public override void TakeDamage(GameObject source, eDamageType damageType, int damageAmount, int criticalAmount)
        {
            if (_invulnerable)
                return;

            base.TakeDamage(source, damageType, damageAmount, criticalAmount);

            if (!_invulnerable && HealthPercent <= 40)
            {
                EnterPhaseTwo();
            }
        }

        internal void EnterPhaseTwo()
        {
            _invulnerable = true;
            Flags |= GameNPC.eFlags.GHOST;
            AttackState = false;
            StopAttack();
            Say("You are not yet worthy! Face my guardians first!");
            _brain.OnEnterPhaseTwo();
        }

        internal void ExitPhaseTwo()
        {
            _invulnerable = false;
            Flags &= ~GameNPC.eFlags.GHOST;
            Say("You have proven yourselves... now face me once more!");
        }

        public override void Die(GameObject killer)
        {
            Say("This... is not the end...");
            base.Die(killer);
        }

        public override int GetResist(eDamageType damageType)
        {
            switch (damageType)
            {
                case eDamageType.Slash: return 30;
                case eDamageType.Crush: return 30;
                case eDamageType.Thrust: return 40;
                case eDamageType.Heat: return 45;
                case eDamageType.Cold: return 45;
                case eDamageType.Matter: return 30;
                case eDamageType.Body: return 35;
                case eDamageType.Spirit: return 40;
                case eDamageType.Energy: return 40;
                default: return 0;
            }
        }
    }

    public class ZilistiphleBrain : StandardMobBrain
    {
        private readonly Zilistiphle _owner;
        private readonly Random _rng = new Random();
        private long _nextCast = 0;
        private long _nextAuraTick = 0;
        private bool _inPhaseTwoWait = false;
        private readonly List<GameNPC> _pulledGuardians = new List<GameNPC>();

        public ZilistiphleBrain(Zilistiphle owner)
        {
            _owner = owner;
            AggroLevel = 100;
            AggroRange = 1400;
        }

        public override bool CheckSpells(eCheckSpellType type)
        {
            if (_owner == null || !_owner.IsAlive || _inPhaseTwoWait) return false;
            if (Body.TargetObject == null || !(Body.TargetObject is GameLiving)) return false;

            int minDelay = 4000;
            int maxDelay = 7000;
            if (!_owner.Flags.HasFlag(GameNPC.eFlags.GHOST) && _owner.IsAlive && _owner.HealthPercent <= 40)
            {
                minDelay = 3000;
                maxDelay = 5000;
            }

            if (_nextCast < Environment.TickCount)
            {
                int choice = _rng.Next(3);
                int spellId = ZilistiphleConfig.EnergyDD_ID;
                if (choice == 1) spellId = ZilistiphleConfig.Debuff_ID;
                else if (choice == 2) spellId = ZilistiphleConfig.BodyDoT_ID;

                var spell = SkillBase.GetSpellByID(spellId);
                if (spell != null)
                {
                    Body.CastSpell(spell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells));
                    _nextCast = Environment.TickCount + Util.Random(minDelay, maxDelay);
                    return true;
                }
            }
            return false;
        }

        public void OnEnterPhaseTwo()
        {
            if (_inPhaseTwoWait) return;
            _inPhaseTwoWait = true;
            _pulledGuardians.Clear();

            TryPullGuardian(ZilistiphleConfig.Guardian1);
            TryPullGuardian(ZilistiphleConfig.Guardian2);
            TryPullGuardian(ZilistiphleConfig.Guardian3);

            if (_pulledGuardians.Count == 0)
            {
                _inPhaseTwoWait = false;
                _owner.ExitPhaseTwo();
                return;
            }

            _nextAuraTick = Environment.TickCount + 1000;
        }

        public override void Think()
        {
            base.Think();

            if (_inPhaseTwoWait)
            {
                if (_nextAuraTick < Environment.TickCount)
                {
                    var aura = SkillBase.GetSpellByID(ZilistiphleConfig.AuraSpellID);
                    if (aura != null)
                    {
                        _owner.CastSpell(aura, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells));
                    }
                    _nextAuraTick = Environment.TickCount + 5000;
                }

                _pulledGuardians.RemoveAll(g => g == null || !g.IsAlive);

                if (_pulledGuardians.Count == 0)
                {
                    _inPhaseTwoWait = false;
                    _owner.ExitPhaseTwo();
                }

                return;
            }
        }

        private void TryPullGuardian(string guardianName)
        {
            if (string.IsNullOrEmpty(guardianName)) return;

            try
            {
                var npcs = WorldMgr.GetNPCsByName(guardianName, eRealm.None);
                if (npcs != null && npcs.Any())
                {
                    foreach (var npc in npcs)
                    {
                        if (npc != null && npc.IsAlive)
                        {
                            MoveNPCToOwner(npc);
                            _pulledGuardians.Add(npc);
                            _owner.Say($"{guardianName}, come forth!");
                            return;
                        }
                    }
                }
            }
            catch { }

            try
            {
                foreach (GameNPC npc in _owner.GetNPCsInRadius(50000))
                {
                    if (npc != null && npc.IsAlive && npc.Name == guardianName)
                    {
                        MoveNPCToOwner(npc);
                        _pulledGuardians.Add(npc);
                        _owner.Say($"{guardianName}, come forth!");
                        return;
                    }
                }
            }
            catch { }
        }

        private void MoveNPCToOwner(GameNPC npc)
        {
            if (npc == null) return;

            int tx = _owner.X + Util.Random(-50, 50);
            int ty = _owner.Y + Util.Random(-50, 50);
            int tz = _owner.Z;
            ushort region = (ushort)_owner.CurrentRegionID;
            ushort heading = (ushort)_owner.Heading;

            try { npc.StopAttack(); } catch { }
            try { npc.TargetObject = null; } catch { }

            try
            {
                npc.MoveTo(region, tx, ty, tz, heading);
            }
            catch
            {
                npc.CurrentRegionID = region;
                npc.X = tx;
                npc.Y = ty;
                npc.Z = tz;
                npc.Heading = heading;
            }
        }
    }
}
