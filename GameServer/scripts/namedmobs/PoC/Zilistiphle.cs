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
using DOL.GS.PacketHandler;
using DOL.GS.Spells;

namespace DOL.GS.CustomBosses
{
    public static class ZilistiphleConfig
    {
        // Stronger final boss spells
        public static readonly int EnergyDD_ID = 50000;   // energy nuke
        public static readonly int BodyDoT_ID = 14358;   // strong body DoT
        public static readonly int Debuff_ID = 4387;   // str/con debuff

        // Aura shield visual
        public static readonly int AuraSpellID = 4309;   // Greater Powerguard

        // Guardians
        public static readonly string Guardian1 = "Rheton";
        public static readonly string Guardian2 = "Busiv";
        public static readonly string Guardian3 = "Drevaul";
    }

    public class Zilistiphle : GameNPC
    {
        private ZilistiphleBrain _brain;
        private bool _invulnerable = false;
        private bool _phase2Entered = false;

        public override bool AddToWorld()
        {
            if (!base.AddToWorld()) return false;

            Level = 80;
            Name = "Zilistiphle";
            Model = 697;
            Size = 255;
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
            {
                if (source is GamePlayer player)
                    player.Out.SendMessage("Zilistiphle's barrier absorbs your attack!", eChatType.CT_System, eChatLoc.CL_SystemWindow);

                return;
            }

            base.TakeDamage(source, damageType, damageAmount, criticalAmount);

            if (!_invulnerable && HealthPercent <= 40)
            {
                EnterPhaseTwo();
            }
        }

        internal void EnterPhaseTwo()
        {
            // Phase 2 can only happen once per fight, otherwise the boss would
            // flip in and out of invulnerability whenever his health drops below 40%.
            if (_phase2Entered)
                return;

            _phase2Entered = true;
            _invulnerable = true;
            Flags |= GameNPC.eFlags.GHOST;
            AttackState = false;
            StopAttack();

            // Stop re-aggroing during the phase (like Fadrin's barrier), so he
            // can't fight back while the guardians are up.
            _brain.AggroLevel = 0;
            _brain.AggroRange = 0;

            Say("You are not yet worthy! Face my guardians first!");
            _brain.OnEnterPhaseTwo();
        }

        internal void ExitPhaseTwo()
        {
            _invulnerable = false;
            Flags &= ~GameNPC.eFlags.GHOST;
            _brain.AggroLevel = 100;
            _brain.AggroRange = 1400;
            Say("You have proven yourselves... now face me once more!");
        }

        public override void Die(GameObject killer)
        {
            // Send still-pulled guardians back to their spawn points so they
            // can be fought (or killed) there later.
            _brain?.ReturnGuardiansHome();

            Say("This... is not the end...");
            base.Die(killer);
        }

        /// <summary>
        /// Full encounter reset after a wipe/flee: the boss is back to full
        /// strength and can enter Phase 2 again on the next attempt.
        /// </summary>
        internal void ResetEncounter()
        {
            _phase2Entered = false;
            _invulnerable = false;
            Flags &= ~GameNPC.eFlags.GHOST;
            AttackState = false;
            StopAttack();
            Health = MaxHealth;
            Say("Zilistiphle regains his full strength as his guardians return home!");
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
        private const int PLAYER_RESET_RADIUS = 2500;
        private const long PLAYER_RESET_DELAY = 15000;

        private readonly Zilistiphle _owner;
        private readonly Random _rng = new Random();
        private long _nextCast = 0;
        private long _nextAuraTick = 0;
        private bool _inPhaseTwoWait = false;
        private long _noPlayerSince = 0;
        private readonly List<GameNPC> _pulledGuardians = new List<GameNPC>();
        private readonly Dictionary<GameNPC, (ushort Region, Point3D Pos)> _guardianHomes = new Dictionary<GameNPC, (ushort, Point3D)>();

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
            _noPlayerSince = 0;
            _pulledGuardians.Clear();
            _guardianHomes.Clear();

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
                    return;
                }

                // Wipe/flee detection: no players around the boss for a while
                // resets the whole encounter (guardians go home, full heal).
                if (_owner.GetPlayersInRadius(PLAYER_RESET_RADIUS).Count == 0)
                {
                    if (_noPlayerSince == 0)
                        _noPlayerSince = GameLoop.GameLoopTime;

                    if (GameLoop.GameLoopTime - _noPlayerSince > PLAYER_RESET_DELAY)
                    {
                        ResetEncounter();
                        return;
                    }
                }
                else
                {
                    _noPlayerSince = 0;
                }

                return;
            }
        }

        private void ResetEncounter()
        {
            ReturnGuardiansHome();
            _inPhaseTwoWait = false;
            _noPlayerSince = 0;
            _nextAuraTick = 0;
            AggroLevel = 100;
            AggroRange = 1400;
            _owner.ResetEncounter();
        }

        /// <summary>
        /// Teleports all pulled guardians back to their original spawn points.
        /// </summary>
        public void ReturnGuardiansHome()
        {
            foreach (GameNPC guardian in _pulledGuardians)
            {
                if (guardian == null || !guardian.IsAlive)
                    continue;

                if (_guardianHomes.TryGetValue(guardian, out (ushort Region, Point3D Pos) home))
                {
                    try
                    {
                        guardian.MoveTo(home.Region, home.Pos.X, home.Pos.Y, home.Pos.Z, guardian.Heading);
                    }
                    catch
                    {
                        guardian.CurrentRegionID = home.Region;
                        guardian.X = home.Pos.X;
                        guardian.Y = home.Pos.Y;
                        guardian.Z = home.Pos.Z;
                    }
                }
            }

            _pulledGuardians.Clear();
            _guardianHomes.Clear();
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

            // Remember where the guardian came from so he can be sent back
            // home when the encounter resets.
            if (!_guardianHomes.ContainsKey(npc))
                _guardianHomes[npc] = ((ushort)npc.CurrentRegionID, new Point3D(npc.X, npc.Y, npc.Z));

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
