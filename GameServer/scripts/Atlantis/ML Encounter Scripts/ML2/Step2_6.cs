//---------------------------------------------------------
//--------------------ML2.6 - Kanahkt ---------------------
//-------------------Author : Hibernos---------------------
//---------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.AI.Brain;
using DOL.GS.Effects;
using DOL.Events;
using log4net;
using System.Reflection;
using DOL.Database;

namespace DOL.GS.Atlantis
{

    //Kanahkt
    public class Kanahkt : GameNPC
    {

        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static bool debug = true;

        //Minimum Level
        public static int MinimumLevel = 40;

        //Realm Regions ( Sobekite )
        public static int albregion = 79;
        public static int midregion = 36;
        public static int hibregion = 136;

        //Realm Available for this Step
        public static bool Albion = false;
        public static bool Midgard = false;
        public static bool Hibernia = true;

        //Config
        public static int MaxTime = 40; //Max time

        //Npcs
        public List<GameNPC> SobekiteAidolonList = new List<GameNPC>();
        public KanahktStatue LeftStatue;
        public KanahktStatue RightStatue;
        public KanahktStatue MarbleStatue;
        public KanahktStatue SlateStatue;

        //Challenger
        public GamePlayer Challenger;

        //Timer
        public ECSGameTimer EndEncounterTimer;

        //LittleStatue Array
        public int[,] LittleStatueArray = {
			{33103,24635,16218,148},
			{33304,25043,16073,575},
			{33655,25382,16202,929},
			{33479,25768,16073,1031},
			{33336,25411,15977,910},
            {32968,24968,15977,164},
            {32561,24976,15977,3732},
            {32223,25071,16073,3443},
            {31923,25407,16176,3116},
            {32233,25440,15976,3073},
            {32068,25751,16073,3080},
            {32432,24647,16214,3881},
		};
        //Sobekite Aidolon Array
        public int[,] SobekiteAidolonArray = {
			{33042,26389,16043},
			{32477,25220,15974},
			{32669,26493,16182},
			{32415,24748,16225},
			{33667,25816,16389},
            {32306,25604,16178},
            {32918,24637,16509},
            {33299,25483,16134},
            {32210,24729,16706},
            {31985,25337,16319},
            {33755,24845,16560},
            {33318,26121,16312},
            {32455,26081,16125},
            {32761,25078,16170},
            {32813,25749,16180},
            {32340,25105,16504},
            {33319,25093,16297},
            {32880,25510,15977},
            {31990,25777,16230},
            {32496,24355,16453},
		};

        //Override
        public override void SaveIntoDatabase() //Disable SaveInDB
        {
        }
        public override void StartRespawn() //Respawn
        {
            base.StartRespawn();
        }
        public override bool AddToWorld() //AddToWorld
        {

            //Spawn Statue
            for (int i = 0; i < 12; i++)
            {
                SpawnLittleStatue(LittleStatueArray[i, 0], LittleStatueArray[i, 1], LittleStatueArray[i, 2], (ushort)LittleStatueArray[i, 3]);
            }
            SpawnLeftStatue();
            SpawnRightStatue();
            SpawnMarbleStatue();
            SpawnSlateStatue();
            if (this.CurrentRegionID == albregion)
            {
                log.Warn("Master Level - 2.8 - Statues ALB added.");
            }
            else if (this.CurrentRegionID == midregion)
            {
                log.Warn("Master Level - 2.8 - Statues MID added.");
            }
            else if (this.CurrentRegionID == hibregion)
            {
                log.Warn("Master Level - 2.8 - Statues HIB added.");
            }

            return base.AddToWorld();
        }
        public override bool Interact(GamePlayer player) //Interact
        {
            TurnTo(player, 100);
            this.TargetObject = player;

            if (!base.Interact(player)) return false;
            if (((player.MLGranted == false) || (player.MLLevel != 1) || (player.Level < Kanahkt.MinimumLevel) || (player.HasFinishedMLStep(2,6) == true)) && (debug == false)) return true;

            player.Out.SendMessage("It has been a long time since someone has attempted this trial."
                +" A very long time indeed."
                +" Are you sure your coming here was [intentional] or did you somehow get lost,"
                +" find the key to my chamber and decide you wanted to see what monster lay within to [slay]?", eChatType.CT_Say, eChatLoc.CL_PopupWindow);

            foreach (GamePlayer onlookers in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (onlookers != player)
                {
                    onlookers.Out.SendMessage(this.Name + " speaks to " + player.Name + " .", eChatType.CT_Chat, eChatLoc.CL_ChatWindow);
                }
            }
            return true;
        }
        public override bool WhisperReceive(GameLiving player, string str) //WhisperReceive
        {
            GamePlayer t = (GamePlayer)player;
            if (((t.MLGranted == false) || (t.MLLevel != 1) || (t.Level < Kanahkt.MinimumLevel) || (t.HasFinishedMLStep(2, 6) == true)) && (debug == false)) return true;
                switch (str)
                {

                    case "intentional":
                        {
                            t.Out.SendMessage("Ahhh! So you have come here to attempt the Trial. Either you are a very courageous XXX or"
                                +" you must have many friends to support you in the endeavor. In any event, it is not for me to judge."
                                +" I am here simply to test you."
                                +" To be sure that you are worthy of proceeding, for only those strong enough to defeat me may have the"
                                +" strength necessary to defeat the Imposter."
                                +" Shall we [proceed]?", eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        }
                        break;

                    case "slay":
                        {
                            string RealmName = "";
                            if (t.Realm == eRealm.Albion) RealmName = "Albion";
                            if (t.Realm == eRealm.Hibernia) RealmName = "Hibernia";
                            if (t.Realm == eRealm.Midgard) RealmName = "Midgard";
                            t.Out.SendMessage("Honestly now,if you've only come to slay the big bad monster,"
                                + " maybe a dragon or one of those " + RealmName + " whelps would be more to your liking.", eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        }
                        break;

                    case "proceed":
                        {
                            t.Out.SendMessage("Rather than wasting your time with minute details of the encounter,"
                                +" I'll just tell you that you will need to defeat all of the monsters in this room."
                                +" Just remember that your actions will impact the outcome - for better or worse."
                                +" Teamwork is a necessity."
                                +" There was something else I wanted to say about the souls, but I just can't seem to remember it at this moment."
                                +" It must not be that important."
                                +" Just let me know when you're ready to [accept] the challenge.", eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        }
                        break;

                    case "accept":
                        {
                            StartEncounter(t);
                        }
                        break;
                }

            return true;
        }

        //Start-Stop Encounter
        public void StartEncounter(GamePlayer TheChallenger)
        {

            //Reset
            ResetEncounter();

            //Set Statues Models
            LeftStatue.Model = 997;
            RightStatue.Model = 997;
            MarbleStatue.Model = 993;
            SlateStatue.Model = 994;

            //Set Challenger Ptr
            Challenger = TheChallenger;

            //Msg
            TheChallenger.Out.SendMessage("Oh I remember now! Watch out for the eidolons."
                + " They can get very nasty.", eChatType.CT_Say, eChatLoc.CL_ChatWindow);
            foreach (GamePlayer onlookers in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                onlookers.Out.SendMessage(TheChallenger.Name + " accepted my challenge ! You have " + MaxTime + "minuts to finish it !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
            }

            //Launch EndTimer
            EndEncounterTimer = new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(EndEncounter), (MaxTime * 60 * 1000));

            //Spawn Fake Kanahkt
            this.Model = 665;
            this.Flags = eFlags.PEACE;
            this.Flags |= eFlags.CANTTARGET;

            //NextStep
            NextStep();

            //Log
            if (debug == true) log.Warn("Master Level - 2.6 - " + TheChallenger.Name + " started encounter !");
        }
        public void ResetEncounter()
        {

            //Stop Timer
            if (EndEncounterTimer != null)
            {
                EndEncounterTimer.Stop();
                EndEncounterTimer = null;
            }

            //Despawn Aidolons
            DeSpawnAllSobekiteAidolon();

            //Set Statues
            if (LeftStatue.Model != 665)
            {
                LeftStatue.Model = 665;
                LeftStatue.Flags = eFlags.PEACE;
                LeftStatue.Flags |= eFlags.CANTTARGET;
                LeftStatue.Health = LeftStatue.MaxHealth;
            }
            if (RightStatue.Model != 665)
            {
                RightStatue.Model = 665;
                RightStatue.Flags = eFlags.PEACE;
                RightStatue.Flags |= eFlags.CANTTARGET;
                RightStatue.Health = RightStatue.MaxHealth;
            }
            if (MarbleStatue.Model != 665)
            {
                MarbleStatue.Model = 665;
                MarbleStatue.Flags = eFlags.PEACE;
                MarbleStatue.Flags |= eFlags.CANTTARGET;
                MarbleStatue.Health = MarbleStatue.MaxHealth;
            }
            if (SlateStatue.Model != 665)
            {
                SlateStatue.Model = 665;
                SlateStatue.Flags = eFlags.PEACE;
                SlateStatue.Flags |= eFlags.CANTTARGET;
                SlateStatue.Health = SlateStatue.MaxHealth;
            }

            //Despawn Fake kanahkt
            this.Model = 1033;
            this.Flags = eFlags.PEACE;

        }
        public void NextStep()
        {
            if (LeftStatue.CheckStatue() == true && RightStatue.CheckStatue() == true && MarbleStatue.CheckStatue() == true && SlateStatue.CheckStatue() == true)
            {
                LeftStatue.Flags = 0;
                SpawnAllSobekiteAidolon();
            }
            else if (LeftStatue.CheckStatue() == false && RightStatue.CheckStatue() == true && MarbleStatue.CheckStatue() == true && SlateStatue.CheckStatue() == true)
            {
                RightStatue.Flags = 0;
                SpawnAllSobekiteAidolon();
            }
            else if (LeftStatue.CheckStatue() == false && RightStatue.CheckStatue() == false && MarbleStatue.CheckStatue() == true && SlateStatue.CheckStatue() == true)
            {
                MarbleStatue.Flags = 0;
                SpawnAllSobekiteAidolon();
            }
            else if (LeftStatue.CheckStatue() == false && RightStatue.CheckStatue() == false && MarbleStatue.CheckStatue() == false && SlateStatue.CheckStatue() == true)
            {
                SlateStatue.Flags = 0;
                SpawnAllSobekiteAidolon();
            }
            else if (LeftStatue.CheckStatue() == false && RightStatue.CheckStatue() == false && MarbleStatue.CheckStatue() == false && SlateStatue.CheckStatue() == false)
            {
                ResetEncounter();
                MLCreditHelper.CreditML((byte)2, (byte)6, Challenger, true, false, (byte)MinimumLevel);
            }
        }

        //EndEncounterTimer
        public int EndEncounter(ECSGameTimer timer)
        {
            //Log
            if (debug) log.Warn("Master Level - 2.6 - MaxTime reached !");

            //Reset
            ResetEncounter();

            return 0;
        }

        //Sobekite Aidolon
        public void SpawnAllSobekiteAidolon()
        {
            //Spawn Sobekite Aidolon
            for (int i = 0; i < 20; i++)
            {
                SpawnSobekiteAidolon(SobekiteAidolonArray[i, 0], SobekiteAidolonArray[i, 1], SobekiteAidolonArray[i, 2]);
            }
        }
        public void DeSpawnAllSobekiteAidolon()
        {
            foreach (GameNPC SobekiteAidolon in SobekiteAidolonList)
            {
                SobekiteAidolon.Health = 0;
                SobekiteAidolon.Delete();
            }
            SobekiteAidolonList.Clear();
        }

        //Spawns
        public void SpawnLittleStatue(int X, int Y, int Z, ushort H)  //Spawn Little Statue
        {
            KanahktStatue LittleStatueNPC = new KanahktStatue();
            LittleStatueNPC.Name = "Kanahkt statue";
            LittleStatueNPC.GuildName = "";
            LittleStatueNPC.Realm = 0;
            LittleStatueNPC.CurrentRegionID = this.CurrentRegionID;
            LittleStatueNPC.X = X;
            LittleStatueNPC.Y = Y;
            LittleStatueNPC.Z = Z;
            LittleStatueNPC.Heading = H;
            LittleStatueNPC.RoamingRange = 0;
            LittleStatueNPC.CurrentSpeed = 0;
            LittleStatueNPC.MaxSpeedBase = 191;
            LittleStatueNPC.AutoSetStats();
            LittleStatueNPC.BodyType = 0;
            LittleStatueNPC.Model = 993;
            LittleStatueNPC.Size = 50;
            LittleStatueNPC.Level = 77;
            LittleStatueNPC.RespawnInterval = 5 * 60 * 1000;
            LittleStatueNPC.Flags |= eFlags.PEACE;
            LittleStatueNPC.Flags |= eFlags.CANTTARGET;
            LittleStatueNPC.Parent = this;
            LittleStatueNPC.AddToWorld();
        }
        public void SpawnLeftStatue()  //Spawn Left Statue
        {
            KanahktStatue StatueNPC = new KanahktStatue();
            StatueNPC.Name = "Stone statue";
            StatueNPC.GuildName = "";
            StatueNPC.Realm = 0;
            StatueNPC.CurrentRegionID = this.CurrentRegionID;
            StatueNPC.X = 31754;
            StatueNPC.Y = 25354;
            StatueNPC.Z = 16233;
            StatueNPC.Heading = 3192;
            StatueNPC.RoamingRange = 0;
            StatueNPC.CurrentSpeed = 0;
            StatueNPC.MaxSpeedBase = 191;
            StatueNPC.AutoSetStats();
            StatueNPC.BodyType = 0;
            StatueNPC.Model = 665;
            StatueNPC.Size = 100;
            StatueNPC.Level = 75;
            StatueNPC.RespawnInterval = 1000;
            StatueNPC.Flags |= eFlags.PEACE;
            StatueNPC.Flags |= eFlags.CANTTARGET;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 40;
            brain.AggroRange = 350;
            StatueNPC.SetOwnBrain(brain);
            LeftStatue = StatueNPC;
            StatueNPC.Parent = this;
            StatueNPC.AddToWorld();
        }
        public void SpawnRightStatue()  //Spawn Right Statue
        {
            KanahktStatue StatueNPC = new KanahktStatue();
            StatueNPC.Name = "Stone statue";
            StatueNPC.GuildName = "";
            StatueNPC.Realm = 0;
            StatueNPC.CurrentRegionID = this.CurrentRegionID;
            StatueNPC.X = 33814;
            StatueNPC.Y = 25357;
            StatueNPC.Z = 16233;
            StatueNPC.Heading = 940;
            StatueNPC.RoamingRange = 0;
            StatueNPC.CurrentSpeed = 0;
            StatueNPC.MaxSpeedBase = 191;
            StatueNPC.AutoSetStats();
            StatueNPC.BodyType = 0;
            StatueNPC.Model = 665;
            StatueNPC.Size = 100;
            StatueNPC.Level = 75;
            StatueNPC.RespawnInterval = 1000;
            StatueNPC.Flags |= eFlags.PEACE;
            StatueNPC.Flags |= eFlags.CANTTARGET;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 40;
            brain.AggroRange = 350;
            StatueNPC.SetOwnBrain(brain);
            RightStatue = StatueNPC;
            StatueNPC.Parent = this;
            StatueNPC.AddToWorld();
        }
        public void SpawnMarbleStatue()  //Spawn Marble Statue
        {
            KanahktStatue StatueNPC = new KanahktStatue();
            StatueNPC.Name = "Marble statue";
            StatueNPC.GuildName = "";
            StatueNPC.Realm = 0;
            StatueNPC.CurrentRegionID = this.CurrentRegionID;
            StatueNPC.X = 32251;
            StatueNPC.Y = 24242;
            StatueNPC.Z = 16489;
            StatueNPC.Heading = 3871;
            StatueNPC.RoamingRange = 0;
            StatueNPC.CurrentSpeed = 0;
            StatueNPC.MaxSpeedBase = 191;
            StatueNPC.AutoSetStats();
            StatueNPC.BodyType = 0;
            StatueNPC.Model = 665;
            StatueNPC.Size = 100;
            StatueNPC.Level = 75;
            StatueNPC.RespawnInterval = 1000;
            StatueNPC.Flags |= eFlags.PEACE;
            StatueNPC.Flags |= eFlags.CANTTARGET;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 40;
            brain.AggroRange = 350;
            StatueNPC.SetOwnBrain(brain);
            MarbleStatue = StatueNPC;
            StatueNPC.Parent = this;
            StatueNPC.AddToWorld();
        }
        public void SpawnSlateStatue()  //Spawn Slate Statue
        {
            KanahktStatue StatueNPC = new KanahktStatue();
            StatueNPC.Name = "Slate statue";
            StatueNPC.GuildName = "";
            StatueNPC.Realm = 0;
            StatueNPC.CurrentRegionID = this.CurrentRegionID;
            StatueNPC.X = 33304;
            StatueNPC.Y = 24219;
            StatueNPC.Z = 16490;
            StatueNPC.Heading = 202;
            StatueNPC.RoamingRange = 0;
            StatueNPC.CurrentSpeed = 0;
            StatueNPC.MaxSpeedBase = 191;
            StatueNPC.AutoSetStats();
            StatueNPC.BodyType = 0;
            StatueNPC.Model = 665;
            StatueNPC.Size = 100;
            StatueNPC.Level = 75;
            StatueNPC.RespawnInterval = 1000;
            StatueNPC.Flags |= eFlags.PEACE;
            StatueNPC.Flags |= eFlags.CANTTARGET;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 40;
            brain.AggroRange = 350;
            StatueNPC.SetOwnBrain(brain);
            SlateStatue = StatueNPC;
            StatueNPC.Parent = this;
            StatueNPC.AddToWorld();
        }
        public void SpawnSobekiteAidolon(int X, int Y, int Z)  //Spawn Sobekite Aidolon
        {
            KanahktAidolon AidolonNPC = new KanahktAidolon();
            AidolonNPC.Name = "sobekite aidolon";
            AidolonNPC.GuildName = "";
            AidolonNPC.Realm = 0;
            AidolonNPC.CurrentRegionID = this.CurrentRegionID;
            AidolonNPC.X = X;
            AidolonNPC.Y = Y;
            AidolonNPC.Z = Z;
            AidolonNPC.Heading = 200;
            AidolonNPC.RoamingRange = 200;
            AidolonNPC.CurrentSpeed = 0;
            AidolonNPC.MaxSpeedBase = 191;
            AidolonNPC.AutoSetStats();
            AidolonNPC.BodyType = 0;
            AidolonNPC.Model = 907;
            AidolonNPC.Size = 50;
            AidolonNPC.Level = 38;
            AidolonNPC.RespawnInterval = 99999999;
            AidolonNPC.Flags = eFlags.FLYING;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 450;
            AidolonNPC.SetOwnBrain(brain);
            AidolonNPC.Parent = this;
            SobekiteAidolonList.Add(AidolonNPC);
            AidolonNPC.AddToWorld();
        }


        //STATIC - Load Event
        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Warn("Master Level - 2.6 - Initializing Objects ...");
            log.Warn("Master Level - 2.6 - Objects Initialized !");
            log.Warn("Master Level - 2.6 - Initializing Event...");
            if (Albion == true)
            {
                SpawnKanahkt((ushort)albregion);
                if (debug == true) log.Warn("Master Level - 2.6 - Kanahkt ALB added.");
            }
            if (Midgard == true)
            {
                SpawnKanahkt((ushort)midregion);
                if (debug == true) log.Warn("Master Level - 2.6 - Kanahkt MID added.");
            }
            if (Hibernia == true)
            {
                SpawnKanahkt((ushort)hibregion);
                if (debug == true) log.Warn("Master Level - 2.6 - Kanahkt HIB added.");
            }
            log.Warn("Master Level - 2.6 - Event Initialized !");
        }
        public static void SpawnKanahkt(ushort Region)
        {
            Kanahkt KanahktNPC = new Kanahkt();
            KanahktNPC.Name = "Kanahkt";
            KanahktNPC.GuildName = "";
            KanahktNPC.Realm = 0;
            KanahktNPC.CurrentRegionID = Region;
            KanahktNPC.X = 32814;
            KanahktNPC.Y = 25854;
            KanahktNPC.Z = 15973;
            KanahktNPC.Heading = 3719;
            KanahktNPC.RoamingRange = 0;
            KanahktNPC.CurrentSpeed = 0;
            KanahktNPC.MaxSpeedBase = 191;
            KanahktNPC.AutoSetStats();
            KanahktNPC.BodyType = 0;
            KanahktNPC.Model = 1033;
            KanahktNPC.Size = 50;
            KanahktNPC.Level = 21;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 40;
            brain.AggroRange = 350;
            KanahktNPC.SetOwnBrain(brain);
            KanahktNPC.Flags |= eFlags.PEACE;
            KanahktNPC.AddToWorld();
        }
    }

    //KanahktStatue
    public class KanahktStatue : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;
        public Kanahkt Parent;

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            this.Flags |= eFlags.PEACE;
            this.Flags |= eFlags.CANTTARGET;
            this.Model = 665;
            base.StartRespawn();
        }
        public override bool AddToWorld()
        {
            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {
            base.Die(killer);
            Parent.DeSpawnAllSobekiteAidolon();
            Parent.NextStep();
        }

        public bool CheckStatue()
        {
            if (IsAlive == false || Model == 665)
            {
                return false;
            }
            return true;
        }

    }

    //KanahktAidolon
    public class KanahktAidolon : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;
        public Kanahkt Parent;

        //Overrides
        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            base.StartRespawn();
        }
        public override bool AddToWorld()
        {
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(TimeReached), (20 * 1000));
            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {
            base.Die(killer);
        }


        //Timer
        public int TimeReached(ECSGameTimer timer)
        {
            if (this.Level == 38)
            {
                this.Level = 41;
                new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(TimeReached), (20 * 1000));
            }
            else if (this.Level == 41)
            {
                this.Level = 46;
                new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(TimeReached), (20 * 1000));
            }
            else if (this.Level == 46)
            {
                this.Level = 51;
                new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(TimeReached), (20 * 1000));
            }
            else if (this.Level == 51)
            {
                this.Level = 56;
                new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(TimeReached), (20 * 1000));
            }
            else if (this.Level == 56)
            {
                this.Level = 61;
            }
            return 0;
        }

    }

}
