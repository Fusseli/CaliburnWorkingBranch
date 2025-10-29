//Author: BluRaven Date 5/30/2010
//Notes: The following things from the database are required: Ylyssan's Spells.
//How it's supposed to work: It's a big snake, you have to be close to it to hit it.
//It's tethered and it randomly does fire DD's (spits fire).  It's on a long respawn timer.
//It grants credit when it dies.
//Adjustable settings: See Respawn mins below.

#region Using Statements

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.AI.Brain;
using DOL.Events;
using DOL.GS.Effects;
using log4net;
using System.Reflection;
using DOL.GS.Atlantis;
using DOL.Database;
using DOL.Language;
using DOL.GS.Spells;
using System.Linq; // Needed for FirstOrDefault

#endregion Using Statements

namespace DOL.GS.Atlantis
{
    public class GameYlyssan : TetheredEncounterMob
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            SpawnYlyssan(Albion);
            SpawnYlyssan(Midgard);
            SpawnYlyssan(Hibernia);
            Spell firedd = SkillBase.GetSpellByID(8205);
            Spell mezz = SkillBase.GetSpellByID(8206);
            Spell fireball = SkillBase.GetSpellByID(8207);
            SkillBase.AddScriptedSpell(GlobalSpellsLines.Mob_Spells, firedd);
            SkillBase.AddScriptedSpell(GlobalSpellsLines.Mob_Spells, mezz);
            SkillBase.AddScriptedSpell(GlobalSpellsLines.Mob_Spells, fireball);
            log.Info("Spawning Artifact Encounter: Maddening Scalars.");
        }
        public static int Respawnmins = 3600000; //One Hour
        public static int Albion = 73;
        public static int Midgard = 30;
        public static int Hibernia = 130;

        public override bool Interact(GamePlayer player)
        {
            return false;
        }
        public override bool WhisperReceive(GameLiving source, string str)
        {
            return false;
        }
        public override void Die(GameObject killer)
        {
            EncounterMgr.GrantEncounterCredit(killer, true, true, "Maddening Scalars");
            base.Die(killer);
        }
        public override int MaxHealth
        {
            get { return 12000; }
        }

        public static void SpawnYlyssan(int region)
        {
            //Create and spawn Ylyssan
            GameYlyssan ylyssan = new GameYlyssan();
            ylyssan.Model = 1234;
            ylyssan.Size = 200;
            ylyssan.Level = 65;
            ylyssan.Name = "Ylyssan";
            ylyssan.CurrentRegionID = (ushort)region;
            ylyssan.Heading = 2013;
            ylyssan.Realm = 0;
            ylyssan.CurrentSpeed = 0;
            ylyssan.MaxSpeedBase = 200;
            ylyssan.GuildName = "";
            ylyssan.X = 466782;
            ylyssan.Y = 569057;
            ylyssan.Z = 9856;
            ylyssan.RoamingRange = 0;
            ylyssan.RespawnInterval = Respawnmins;
            ylyssan.TetherRange = 700;
            ylyssan.BodyType = 0;
            ylyssan.Flags ^= GameNPC.eFlags.GHOST;
            ylyssan.Spells.Add(SkillBase.GetSpellByID(8205));
            ylyssan.Spells.Add(SkillBase.GetSpellByID(8206));
            ylyssan.Spells.Add(SkillBase.GetSpellByID(8207));
            YlyssanBrain brain = new YlyssanBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 700;
            ylyssan.SetOwnBrain(brain);
            ylyssan.AutoSetStats();
            ylyssan.AddToWorld();
            return;
        }
    }
}

namespace DOL.AI.Brain
{
    public class YlyssanBrain : StandardMobBrain
    {
        private long m_nextMezz;
        private long m_nextFireball;

        public YlyssanBrain() : base()
        {
            m_nextMezz = 0;
            m_nextFireball = 0;
        }

        public override bool CheckSpells(eCheckSpellType type)
        {
            if (Body.TargetObject == null || !(Body.TargetObject is GameLiving target))
                return false;

            // Fireball (8207) – stronger nuke, every 15–20s
            if (m_nextFireball < Environment.TickCount)
            {
                var fireball = Body.Spells.FirstOrDefault(s => s.ID == 8207);
                if (fireball != null)
                {
                    Body.CastSpell(fireball, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells));
                    m_nextFireball = Environment.TickCount + Util.Random(15000, 20000);
                    return true;
                }
            }

            // Mezz (8206) – rare CC, every 30–45s with 20% chance
            if (m_nextMezz < Environment.TickCount && Util.Chance(20))
            {
                var mezz = Body.Spells.FirstOrDefault(s => s.ID == 8206);
                if (mezz != null)
                {
                    Body.CastSpell(mezz, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells));
                    m_nextMezz = Environment.TickCount + Util.Random(30000, 45000);
                    return true;
                }
            }

            // Default fallback – Fire DD (8205)
            var dd = Body.Spells.FirstOrDefault(s => s.ID == 8205);
            if (dd != null && Util.Chance(60)) // 60% chance to cast instead of melee
            {
                Body.CastSpell(dd, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells));
                return true;
            }

	    // Otherwise, fall through → melee this tick
            return false;
        }

        public override void Think()
        {
            base.Think();
        }
    }
}
