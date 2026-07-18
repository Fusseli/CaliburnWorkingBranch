/*Author: BluRaven Date Started 3/16/2010 - Date Finished: 5/29/2010
 * Notes: The following things from the database are required:
 * NPCTemplates and NPCEquipments and path's.
 * Adjustable settings: See respawnmins and hammerchance and maxwaves below.
 * How it's supposed to work: Players interact with the Sturdy
 * Smith Hammers in the forge pits.  They will interact with a hammer
 * which will instantly mez all the players, which triggers a wave of
 * mobs to come down one of two ramps after them.  If they survive it,
 * they get to try another hammer, repeating this process until they
 * find the winning hammer which grants them Bruiser credit.
 * Each wave of mobs get's a little thougher then the last and the
 * duration of the instant mez increases by two seconds each time.
*/
using System;
using System.Collections.Generic;
using System.Text;
using DOL.GS;
using log4net;
using System.Reflection;
using DOL.GS.PacketHandler;
using DOL.GS.Atlantis;
using DOL.Events;
using DOL.Database;
using System.Collections;
using DOL.AI.Brain;
using DOL.GS.Spells;

namespace DOL.GS.Atlantis
{
    public class BruiserEncounter
    {
        public ushort Region;
        public static int Albion = 73;
        public static int Midgard = 30;
        public static int Hibernia = 130;
        public static BruiserEncounter BEAlbion;
        public static BruiserEncounter BEMidgard;
        public static BruiserEncounter BEHibernia;
        public static int maxwaves = 25;
        public static int respawnmins = 20;
        public static int hammerchance = 25;
        public static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public long lastCompletedTime = 0;
        public long lastTriedTime = 0;
        public bool encounterStarted = false;
        public int WaveNumber = 1;
        public int MezSeconds = 0;
        public List<InstantMezzer> Mezzerlist = new List<InstantMezzer>();
        public static int[,] HammerPosition = {
            {523161, 538831, 10686, 3766},
            {523139, 538860, 10686, 955},
            {523276, 538777, 10675, 978},
            {523026, 538838, 10686, 193},
            {522904, 537504, 10675, 1922},
            {523141, 538758, 10677, 1035},
            {522870, 538777, 10675, 841},
            {522897, 538705, 10637, 546},
            {523136, 537431, 10686, 1024},
            {523120, 537431, 10686, 1024},
            {523154, 537421, 10686, 2662},
        };
        public static int[,] MezzerPosition = {
            {523232, 537795, 10680, 2207},
            {522609, 537983, 10635, 1046},
            {524102, 538137, 10664, 1024},
            {523030, 538504, 10660, 3094},
        };

        public static int[,] Ramp1Position = {
            {523403, 539596, 11476, 1033},
            {523311, 539595, 11409, 1033},
            {523187, 539592, 11319, 1033},
            {523065, 539592, 11229, 1033},
            {522961, 539592, 11153, 1033},
            {522839, 539592, 11064, 1033},
            {522722, 539592, 10978, 1033},
            {522598, 539592, 10887, 1033},
        };

        public static int[,] Ramp2Position = {
            {523407, 536683, 11479, 1030},
            {523290, 536683, 11394, 1030},
            {523150, 536683, 11291, 1030},
            {523012, 536683, 11190, 1030},
            {522893, 536683, 11103, 1030},
            {522764, 536683, 11009, 1030},
            {522644, 536683, 10921, 1030},
            {522548, 536683, 10850, 1030},
        };


        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Info("Spawning Artifact Encounter: Bruiser.");
            BruiserEncounter BEAlbion = new BruiserEncounter((ushort)Albion);
            BruiserEncounter.BEAlbion = BEAlbion;
            BruiserEncounter BEMidgard = new BruiserEncounter((ushort)Midgard);
            BruiserEncounter.BEMidgard = BEMidgard;
            BruiserEncounter BEHibernia = new BruiserEncounter((ushort)Hibernia);
            BruiserEncounter.BEHibernia = BEHibernia;
        }

        public BruiserEncounter(ushort region)
        {
            Region = region;
            SpawnHammers();
        }

        public void SpawnHammers()
        {
            for (int i = 0; i < 11; i++)
            {
                SturdyHammer hammer = new SturdyHammer();
                hammer.Name = "Sturdy Smith Hammer";
                hammer.Model = 1671;
                hammer.X = HammerPosition[i, 0];
                hammer.Y = HammerPosition[i, 1];
                hammer.Z = HammerPosition[i, 2];
                hammer.Heading = (ushort)HammerPosition[i, 3];
                hammer.CurrentRegionID = Region;
                hammer.Encounter = this;
                hammer.AddToWorld();
            }
            for (int i = 0; i < 4; i++)
            {
                InstantMezzer mezzer = new InstantMezzer();
                mezzer.Name = "instant mez on hammer interact";
                mezzer.Model = 1;
                mezzer.Level = 50;
                mezzer.Flags |= GameNPC.eFlags.DONTSHOWNAME;
                mezzer.Flags |= GameNPC.eFlags.FLYING;
            mezzer.X = MezzerPosition[i, 0];
                mezzer.Y = MezzerPosition[i, 1];
                mezzer.Z = MezzerPosition[i, 2];
                mezzer.Heading = (ushort)MezzerPosition[i, 3];
                mezzer.MaxSpeedBase = 0;
                mezzer.CurrentRegionID = this.Region;
                this.Mezzerlist.Add(mezzer);
                mezzer.Encounter = this;
                mezzer.AddToWorld();
            }

        }

        public void MezAndSendWave(BruiserEncounter Encounter, GamePlayer interacter)
        {

            EncounterMgr.BroadcastMsg(interacter, "An invisible force removes the sturdy smith hammer from your grasp; could this hammer be the legendary Bruiser?", 1800, false);
            Encounter.lastTriedTime = interacter.CurrentRegion.Time;
                foreach (InstantMezzer mezzer in Encounter.Mezzerlist)
                {
                    mezzer.Mezz();
                }
                List<GameNPC> SpawnList = BuildList(WaveNumber);
                //pick a random ramp
                int rand = Util.Random(1, 2);
                switch (rand){
                    case 1:
                        int i = 0;
                        foreach (GameNPC npc in SpawnList)
                        {
                            npc.X = Ramp1Position[i, 0];
                            npc.Y = Ramp1Position[i, 1];
                            npc.Z = Ramp1Position[i, 2];
                            npc.Heading = (ushort)Ramp1Position[i, 3];
                            npc.PathID = "BruiserRamp1";
                            npc.Realm = 0;
                            npc.CurrentRegionID = interacter.CurrentRegionID;
                            npc.RespawnInterval = -1;
                            npc.AutoSetStats();
                            npc.AddToWorld();
                            i++;
                        }
                        break;
                    case 2:
                        int n = 0;
                        foreach (GameNPC npc in SpawnList)
                        {
                            npc.X = Ramp2Position[n, 0];
                            npc.Y = Ramp2Position[n, 1];
                            npc.Z = Ramp2Position[n, 2];
                            npc.Heading = (ushort)Ramp2Position[n, 3];
                            npc.PathID = "BruiserRamp2";
                            npc.Realm = 0;
                            npc.CurrentRegionID = interacter.CurrentRegionID;
                            npc.RespawnInterval = -1;
                            npc.AutoSetStats();
                            npc.AddToWorld();
                            n++;
                        }
                        break;
		            default:
                    break;
	            }
        }

        //building a list of mobs to send as a wave of mobs.
        public List<GameNPC> BuildList(int wavenumber)
        {
            List<GameNPC> builtlist = new List<GameNPC>();
            switch (wavenumber)
            {
                case 1:
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56701)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56701)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56701)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56701)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56701)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56701)));
                    break;
                case 2:
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56702)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56702)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56702)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56702)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56702)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56702)));
                    break;
                case 3:
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56703)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56703)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56703)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56703)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56703)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56703)));
                    break;
                case 4:
                case 5:
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56703)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56703)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56704)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56703)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56703)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56703)));
                    break;
                case 6:
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56703)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56703)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56704)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56703)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56705)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56703)));
                    break;
                case 7:
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56705)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56704)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56703)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56703)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56703)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56703)));
                    break;
                case 8:
                case 9:
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56706)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56706)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56706)));
                    break;
                case 10:
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56707)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56707)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56707)));
                    break;
                case 11:
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56708)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56706)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56707)));
                    break;
                case 12:
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56706)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56706)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56706)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56706)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56706)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56706)));
                    break;
                case 13:
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56707)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56707)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56707)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56707)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56707)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56707)));
                    break;
                case 14:
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56707)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56706)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56707)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56708)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56706)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56708)));
                    break;
                case 15:
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56707)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56707)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56706)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56707)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56706)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56706)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56707)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56706)));
                    break;
                case 16:
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56708)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56706)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56706)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56708)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56706)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56708)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56706)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56708)));
                    break;
                case 17:
                case 18:
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56709)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56709)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56709)));
                    break;
                case 19:
                case 20:
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56710)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56710)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56710)));
                    break;
                case 21:
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56709)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56709)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56709)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56709)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56709)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56709)));
                    break;
                case 22:
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56710)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56710)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56710)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56710)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56710)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56710)));
                    break;
                case 23:
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56710)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56710)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56710)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56711)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56710)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56710)));
                    break;
                case 24:
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56710)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56710)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56710)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56710)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56710)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56710)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56710)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56710)));
                    break;
                case 25:
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56712)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56712)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56712)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56712)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56712)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56712)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56712)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56712)));
                    break;
                default:
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56712)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56712)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56712)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56712)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56712)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56712)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56712)));
                    builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56712)));
                    break;
            }
            return builtlist;
        }

    }
    public class InstantMezzer : BasicEncounterMob
    {
        public BruiserEncounter Encounter;
        public void Mezz()
        {
            //define the mezz spell here (duration is dynamic)
            DbSpell mezz = new DbSpell();
            mezz.Type = "Mesmerize";
            mezz.Icon = 2619;
            mezz.Target = "Enemy";
            mezz.CastTime = 0;
            mezz.ClientEffect = 2619;
            mezz.Name = "Umbral Wave";
            mezz.Radius = 1000;
            mezz.Duration = 20 + Encounter.MezSeconds;
            //CastSpellNoLOSChecks the mezz spell
            Spell mezzspell = new Spell(mezz, 1);
            SkillBase.AddScriptedSpell(GlobalSpellsLines.Mob_Spells, mezzspell);
            CastSpell(mezzspell, SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells), false);
        }
    }

    class SturdyHammer : GameStaticItem
    {
        public override void SaveIntoDatabase()
        {
        }
        public BruiserEncounter Encounter;
        public override bool Interact(GamePlayer interacter)
        {
            if (!base.Interact(interacter))
                return false;
            if (!interacter.IsWithinRadius(this, WorldMgr.INTERACT_DISTANCE))
                return false;
            
            long timesincetry = interacter.CurrentRegion.Time - Encounter.lastTriedTime;
            long trytimer = (30 * 1000);
            if (Encounter.lastTriedTime > 0 && timesincetry < trytimer)
            {
                    interacter.Out.SendMessage("You must wait a few moments before trying another hammer.", eChatType.CT_Chat, eChatLoc.CL_ChatWindow);
                    return false;
            }

            if (Encounter.encounterStarted)
            {
                //they don't have a chance at winning until they have survived at least 5 waves.
                if ((Encounter.WaveNumber > 5) && ((Util.Random(1, BruiserEncounter.hammerchance) == BruiserEncounter.hammerchance) || Encounter.WaveNumber >= BruiserEncounter.maxwaves))
                {
                    Encounter.encounterStarted = false;
                    Encounter.lastCompletedTime = interacter.CurrentRegion.Time;
                    Encounter.WaveNumber = 1;
                    Encounter.MezSeconds = 0;
                    EncounterMgr.GrantEncounterCredit(interacter, true, true, "Bruiser");
                    return true;
                }
                //Mez and send a wave of mobs.
                Encounter.WaveNumber++;
                Encounter.MezSeconds = Encounter.MezSeconds + 2;
                Encounter.MezAndSendWave(this.Encounter, interacter);
                return true;
            }
            long timesince = interacter.CurrentRegion.Time - Encounter.lastCompletedTime;
            long timer = ((60 * 1000) * BruiserEncounter.respawnmins);
            if (Encounter.lastCompletedTime > 0 && timesince < timer)
            {
                long timeleft = timer - timesince;
                if (timeleft < 1000 * 60)
                {

                    interacter.Out.SendMessage("The Bruiser encounter will be available again in " + (timeleft / 1000) + " seconds.", eChatType.CT_Chat, eChatLoc.CL_ChatWindow);
                    return false;
                }
                else
                {
                    interacter.Out.SendMessage("The Bruiser encounter will be available again in " + ((timeleft / (1000 * 60)) + 1) + " minutes.", eChatType.CT_Chat, eChatLoc.CL_ChatWindow);
                    return false;
                }
            }
            if (!Encounter.encounterStarted)
            {
                Encounter.encounterStarted = true;
                //Mez and send a wave of mobs.
                Encounter.MezAndSendWave(this.Encounter, interacter);
                return true;
            }
            return true;
        }

    }
}
