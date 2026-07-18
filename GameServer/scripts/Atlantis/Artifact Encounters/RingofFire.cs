//Author: BluRaven Date 5/31/2010
//Notes: The following things from the database are required: NPCTemplates and NPCEquipments.
//How it's supposed to work:
//You talk to the genie woman to start it,
//she ports you to an island but you never leave the island,
//all the waves of mobs you do on that island with her.  Then
//she gives you credit and ports you back to where you started.
//you talk to her each time your ready for another wave of mobs.

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

#endregion Using Statements

namespace DOL.GS.Atlantis
{
    public class RingofFireEncounter
    {
        public ushort Region;
        public static int Albion = 73;
        public static int Midgard = 30;
        public static int Hibernia = 130;
        public static RingofFireEncounter RFEAlbion;
        public static RingofFireEncounter RFEMidgard;
        public static RingofFireEncounter RFEHibernia;
        public static int despawnmins = 25;
        //********************************************
        //* Do not change variables below this line. *
        //********************************************
        public int EncounterStep = 1;
        public bool encounterStarted = false;
        public static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static int[,] SpawnPosition = {
            // 0-X     1-Y    2-Z    3-H
            {499248, 539749, 9537, 3936},
            {499000, 539752, 9523, 3936},
            {499121, 539723, 9533, 3936},
            {499061, 539737, 9528, 3936},
            {499274, 539686, 9534, 3936},
        };

        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Info("Spawning Artifact Encounter: Ring of Fire.");
            RingofFireEncounter RFEAlbion = new RingofFireEncounter((ushort)Albion);
            RingofFireEncounter.RFEAlbion = RFEAlbion;
            RingofFireEncounter RFEMidgard = new RingofFireEncounter((ushort)Midgard);
            RingofFireEncounter.RFEMidgard = RFEMidgard;
            RingofFireEncounter RFEHibernia = new RingofFireEncounter((ushort)Hibernia);
            RingofFireEncounter.RFEHibernia = RFEHibernia;
        }

        public RingofFireEncounter(ushort region)
        {
            Region = region;
            SpawnGenieWoman();
        }

        public void SpawnGenieWoman()
        {
            GenieWoman genie = new GenieWoman();
            genie.Name = "Soleh";
            genie.Model = 1199;
            genie.Size = 61;
            genie.Level = 60;
            genie.X = 497935;
            genie.Y = 533364;
            genie.Z = 10616;
            genie.Heading = (ushort)3515;
            genie.CurrentRegionID = Region;
            genie.Flags ^= GameNPC.eFlags.PEACE;
            genie.Encounter = this;
            genie.AddToWorld();

            //the same mob is also on the encounter island too.
            GenieWoman islandgenie = new GenieWoman();
            islandgenie.Name = "Soleh";
            islandgenie.Model = 1199;
            islandgenie.Size = 67;
            islandgenie.Level = 63;
            islandgenie.X = 499595;
            islandgenie.Y = 541086;
            islandgenie.Z = 9501;
            islandgenie.Heading = (ushort)1968;
            islandgenie.CurrentRegionID = Region;
            islandgenie.Flags ^= GameNPC.eFlags.PEACE;
            islandgenie.Encounter = this;
            islandgenie.AddToWorld();

        }
    }

    class GenieWoman : BasicEncounterMob
    {
        public RingofFireEncounter Encounter;
        
        public override bool Interact(GamePlayer interacter)
        {
            if (!base.Interact(interacter))
                return false;
            if (!interacter.IsWithinRadius(this, WorldMgr.INTERACT_DISTANCE))
                return false;
            if (Encounter.encounterStarted == false)
            {
                SayTo(interacter, "Hello " + interacter.Name + ".  What an excellent day for a competition!  You may fight along side up to [seven] of your peers.");
                return true;
            }
            if ((Encounter.encounterStarted == true) && (interacter.TempProperties.GetProperty<bool>("IsDoingROFEncounter") == false))
            {
                SayTo(interacter, "Don't bother me right now " + interacter.Name + ", I'm trying to concentrate.");
                return true;
            }
            if ((Encounter.encounterStarted == true) && (interacter.TempProperties.GetProperty<bool>("IsDoingROFEncounter") == true) && Encounter.EncounterStep == 2 && CheckForGladiators() == false)
            {
                EncounterMgr.BroadcastMsg(this, "The battle will now begin.  This is a fight to the death!  Good luck warriors.", true);
                SpawnWave(Encounter.EncounterStep);
                Encounter.EncounterStep = 3;
                return true;
            }
            if ((Encounter.encounterStarted == true) && (interacter.TempProperties.GetProperty<bool>("IsDoingROFEncounter") == true) && Encounter.EncounterStep == 3 && CheckForGladiators() == false)
            {
               SayTo(interacter, "Very impressive!  I see you are not as weak as my minotaur associates have assumed you to be.  Congratulations on surviving round one.  Two more rounds remain.  Are you [ready] to continue?");
               return true;
            }
            if ((Encounter.encounterStarted == true) && (interacter.TempProperties.GetProperty<bool>("IsDoingROFEncounter") == true) && Encounter.EncounterStep == 4 && CheckForGladiators() == false)
            {
                SayTo(interacter, "Again you surprise me with both your skill and determination " + interacter.Name + ".  You should all be very pleased to still be alive.  You are just one step away from your chance to destroy The Ring of Fire.  Are you [ready] for the final round?");

            }
            if ((Encounter.encounterStarted == true) && (interacter.TempProperties.GetProperty<bool>("IsDoingROFEncounter") == true) && Encounter.EncounterStep == 5 && CheckForGladiators() == false)
            {
                SayTo(interacter, "Congratulations warriors!  You have defeated our best gladiators three rounds in a row.  You have earned the honor of destroying The Ring of Fire.   Are you [prepared] to return to land?");
                EncounterMgr.GrantEncounterCredit(interacter, true, false, "Ring of Fire");
                Encounter.EncounterStep = 6;
                return true;
            }
            if ((Encounter.encounterStarted == true) && (interacter.TempProperties.GetProperty<bool>("IsDoingROFEncounter") == true) && Encounter.EncounterStep == 6 && CheckForGladiators() == false)
            {
                SayTo(interacter, "Congratulations warriors!  You have defeated our best gladiators three rounds in a row.  You have earned the honor of destroying The Ring of Fire.   Are you [prepared] to return to land?");
                return true;
            }
            return true;
        }

        public override bool WhisperReceive(GameLiving source, string str)
        {
            if (source != null && source is GamePlayer)
            {
                GamePlayer player = source as GamePlayer;
                switch (str)
                {
                    case "seven":
                        SayTo(player, "One full group is acceptable however be sure that you are prepared for battle before you accept this challenge and bear in mind that you'll have limited time to complete it.  Are you ready to [begin]?  I will lead you to the ring upon your command.");
                        break;
                    case "begin":
                        if (Encounter.encounterStarted == false)
                        {
                            BeginEncounter(player);
                        }
                        break;
                    case "ready":
                        if ((Encounter.encounterStarted == true) && (source.TempProperties.GetProperty<bool>("IsDoingROFEncounter") == true) && Encounter.EncounterStep == 3 && CheckForGladiators() == false)
                        {
                            EncounterMgr.BroadcastMsg(this, "Prepare for battle!", true);
                            SpawnWave(Encounter.EncounterStep);
                            Encounter.EncounterStep = 4;
                        }
                        if ((Encounter.encounterStarted == true) && (source.TempProperties.GetProperty<bool>("IsDoingROFEncounter") == true) && Encounter.EncounterStep == 4 && CheckForGladiators() == false)
                        {
                            EncounterMgr.BroadcastMsg(this, "Prepare for battle!", true);
                            SpawnWave(Encounter.EncounterStep);
                            Encounter.EncounterStep = 5;
                        }
                        break;
                    case "prepared":
                        if ((Encounter.encounterStarted == true) && (source.TempProperties.GetProperty<bool>("IsDoingROFEncounter") == true) && Encounter.EncounterStep == 6 && CheckForGladiators() == false)
                        {
                            foreach (GamePlayer p in this.GetPlayersInRadius((ushort)WorldMgr.VISIBILITY_DISTANCE))
                            {
                                if ((p.TempProperties.GetProperty<bool>("IsDoingROFEncounter") == true) && p.IsAlive)
                                {
                                    //move the players back to the starting location.
                                    p.MoveTo(p.CurrentRegionID, 498169, 533530, 10616, 1513);
                                    p.TempProperties.SetProperty("IsDoingROFEncounter", false);
                                }
                            }
                            Encounter.encounterStarted = false;
                            Encounter.EncounterStep = 1;
                            break;
                        }
                        break;
                    default:
                        break;
                }
                return true;
            }
            return base.WhisperReceive(source, str);
        }

        public void BeginEncounter(GamePlayer player)
        {
            if (Encounter.encounterStarted == true)
            {
                SayTo(player, "The encounter is already in progress.");
                return;
            }
            else
            {
                if (player.Group == null)
                {
                    SayTo(player, "You must be grouped with at least one other player to attempt this encounter.");
                    return;
                }
                if (player.Group != null)
                {
                    List<GamePlayer> groupPlayers = (List<GamePlayer>)player.Group.GetPlayersInTheGroup();
                    foreach (GamePlayer p in groupPlayers)
                    {
                        if (p.CurrentRegionID == player.CurrentRegionID && p.IsAlive)
                        {
                            p.MoveTo(p.CurrentRegionID, 499864, 540965, 9464, 1900);
                            p.TempProperties.SetProperty("IsDoingROFEncounter", true);
                        }
                    }
                    Encounter.encounterStarted = true;
                    Encounter.EncounterStep = 2;
                    new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(DespawnEncounter), RingofFireEncounter.despawnmins * 60000);
                }
            }
            return;
        }

        public int DespawnEncounter(ECSGameTimer timer)
        {
            if (Encounter.encounterStarted == false) { return 0; }

            foreach (GameNPC npc in this.GetNPCsInRadius((ushort)WorldMgr.VISIBILITY_DISTANCE))
            {
                if (npc.Name == "Taur Gladiator")
                {
                    npc.Brain.Stop();
                    npc.Health = 0;
                    npc.Delete();
                }
            }
            foreach (GamePlayer p in this.GetPlayersInRadius((ushort)WorldMgr.VISIBILITY_DISTANCE))
            {
                if ((p.TempProperties.GetProperty<bool>("IsDoingROFEncounter") == true))
                {
                    //move the players back to the starting location.
                    p.MoveTo(p.CurrentRegionID, 498169, 533530, 10616, 1513);
                    p.TempProperties.SetProperty("IsDoingROFEncounter", false);
                    SayTo(p, "You have run out of time to complete my Challenge.");
                }
            }  
            Encounter.encounterStarted = false;
            Encounter.EncounterStep = 1;
            return 0;
        }
        public bool CheckForGladiators()
        {
            bool gladiatorsfound = false;
            foreach (GameNPC npc in this.GetNPCsInRadius((ushort)WorldMgr.VISIBILITY_DISTANCE))
            {
                if (npc.Name == "Taur Gladiator")
                {
                    gladiatorsfound = true;
                }
            }
            return gladiatorsfound;
        }
        public void SpawnWave(int encounterStep)
        {

            List<GameNPC> spawnlist = BuildList();

            foreach (GameNPC npc in spawnlist)
            {
                //adjust their stats depending on the step number.
                //encounterStep will be 2, 3, 4.
                npc.Strength = (short)((474 * encounterStep) / 2);
                npc.Constitution = (short)((350 * encounterStep) / 2);
                npc.Dexterity = 30;
                npc.Quickness = 30;
            }

            //spawn the list now.
            int i = 0;
            foreach (GameNPC npc in spawnlist)
            {
                npc.X = RingofFireEncounter.SpawnPosition[i, 0];
                npc.Y = RingofFireEncounter.SpawnPosition[i, 1];
                npc.Z = RingofFireEncounter.SpawnPosition[i, 2];
                npc.Heading = (ushort)RingofFireEncounter.SpawnPosition[i, 3];
                npc.Realm = 0;
                npc.CurrentRegionID = this.CurrentRegionID;
                npc.RespawnInterval = -1;
                npc.AddToWorld();
                i++;
            }
            return;
        }

        //building a list of mobs to send as a wave of mobs.
        public List<GameNPC> BuildList()
        {
            List<GameNPC> builtlist = new List<GameNPC>();

            for (int i = 0; i < 5; i++)
            {
                int r = Util.Random(1, 3);

                switch (r)
                {
                    case 1:
                        builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56713)));
                        break;
                    case 2:
                        builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56714)));
                        break;
                    case 3:
                        builtlist.Add(new GameNPC(NpcTemplateMgr.GetTemplate(56715)));
                        break;
                    default:
                        break;
                }
            }
            return builtlist;
        }
    }
}
