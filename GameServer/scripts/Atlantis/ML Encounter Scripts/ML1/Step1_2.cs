//---------------------------------------------------------
//------------------ML 1.2 - Retrieval --------------------
//-------------------Author : Hibernos---------------------
//---------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.AI.Brain;
using DOL.Events;
using log4net;
using System.Reflection;

//Using Mgr

namespace DOL.GS.Atlantis
{

    //Lornas Class
    public class Lornas : GameNPC
    {

        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static bool debug = false;

        //Minimum Level
        public static int MinimumLevel = 40;

        //Minimum Respawn Time - Maximum Respawn Time ( in minutes )
        public static int MinRespawn = 30;
        public static int MaxRespawn = 45;

        //Realm Regions
        public static int albregion = 73;
        public static int midregion = 30;
        public static int hibregion = 130;

        //Realm Available for this Step
        public static bool Albion = false;
        public static bool Midgard = false;
        public static bool Hibernia = true;

        //Borjad and Borjan brothers
        public BorjanBorjad BorjadBrother;
        public BorjanBorjad BorjanBrother;

        //Overrides
        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
        }
        public override bool Interact(GamePlayer player)
        {
            if (base.Interact(player))
            {
                TurnTo(player, 1500);
                
                if (player.MLLevel == 0 && player.Level >= MinimumLevel)
                {
                    if ((BorjadBrother != null && BorjadBrother.IsAttacking) || (BorjanBrother != null && BorjanBrother.IsAttacking))
                    {
                        SayTo(player, "I am busy, go out !");
                    }
                    else
                    {
                        SayTo(player, "Welcome, can you [help] me ?");
                    }
                }
                else if (player.MLLevel > 0 && player.Level >= MinimumLevel)
                {
                    SayTo(player, "Thanks for your help !");
                }
                else if (player.Level < MinimumLevel)
                {
                    SayTo(player, "How you reach me alive ?");
                }

                return true;
            }

            return false;
        }
        public override bool WhisperReceive(GameLiving player, string str)
        {
            GamePlayer t = (GamePlayer)player;
            switch (str)
            {

                case "help":
                    {
                        #region help

                            string Msg = "Two brothers of our kind named Borjad and Borjan, have been robbing from our merchants."
                            +"Sometimes even killing them for the wares they may possess on their person."
                            +"As they will know us on sight and what we seek, we would like to hire someone to deal with this problem.";
                            bool anyAlive = (BorjadBrother != null && BorjadBrother.IsAlive && !BorjadBrother.IsAttacking)
                                || (BorjanBrother != null && BorjanBrother.IsAlive && !BorjanBrother.IsAttacking);
                            if (anyAlive)
                            {
                                Msg = Msg + "Would you be welling to [accept] this task?";
                            }
                            t.Out.SendMessage(Msg, eChatType.CT_Say, eChatLoc.CL_PopupWindow);

                        #endregion help
                    }
                    break;

                case "accept":
                    {
                        #region accept

                        string Msg = "Thank you for helping us. We only require that you rid us of this nuisance."
                        +"By disposing of even one of these men you would be doing us a great service."
                        +"Ant belongings you find on these thieves you may keep for yourself as payment.";
                        t.Out.SendMessage(Msg, eChatType.CT_Say, eChatLoc.CL_PopupWindow);

                        string Msg2 = "Rumors surrounding Borjad and Borjan are plentiful."
                        +"I am unsure of where they may be,but when they last left us, they headed to ";

                        if (BorjadBrother != null && BorjadBrother.IsAlive)
                        {
                            string locName = "";
                            if (BorjadBrother.X == 354659)
                                locName = "Southwest of Mésothalassa";
                            else if (BorjadBrother.X == 353554)
                                locName = "north, around Kitara";
                            else if (BorjadBrother.X == 382238)
                                locName = "East on Naxos territory";
                            else if (BorjadBrother.X == 333822)
                                locName = "West, on Skyros territory";
                            if (locName != "")
                                Msg2 = "Borjad was last seen " + locName + ". ";
                        }
                        if (BorjanBrother != null && BorjanBrother.IsAlive)
                        {
                            string locName = "";
                            if (BorjanBrother.X == 355159)
                                locName = "Southwest of Mésothalassa";
                            else if (BorjanBrother.X == 354054)
                                locName = "north, around Kitara";
                            else if (BorjanBrother.X == 382738)
                                locName = "East on Naxos territory";
                            else if (BorjanBrother.X == 334322)
                                locName = "West, on Skyros territory";
                            if (locName != "")
                                Msg2 = Msg2 + "Borjan was last seen " + locName + ".";
                        }
                        t.Out.SendMessage(Msg2, eChatType.CT_Say, eChatLoc.CL_PopupWindow);

                        #endregion accept
                    }
                    break;
            }

            return true;
        }
        public override bool AddToWorld()
        {
            //Spawn both brothers
            SpawnBorjanBorjad();

            return base.AddToWorld();
        }

        //Spawn both brothers with separate locations
        public void SpawnBorjanBorjad()
        {
            // Spawn Borjad
            BorjadBrother = SpawnBrother("Borjad", 0);
            // Spawn Borjan with offset to avoid stacking
            BorjanBrother = SpawnBrother("Borjan", 500);
        }

        private BorjanBorjad SpawnBrother(string name, int offset)
        {
            BorjanBorjad brother = new BorjanBorjad();
            int loc = Util.Random(0, 3);
            switch (loc)
            {
                case 0:
                    brother.X = 354659 + offset;
                    brother.Y = 568220 + offset;
                    brother.Z = 6718;
                    brother.Heading = 2793;
                    break;
                case 1:
                    brother.X = 353554 + offset;
                    brother.Y = 530965 + offset;
                    brother.Z = 5008;
                    brother.Heading = 3142;
                    break;
                case 2:
                    brother.X = 382238 + offset;
                    brother.Y = 547833 + offset;
                    brother.Z = 5410;
                    brother.Heading = 4011;
                    break;
                case 3:
                    brother.X = 333822 + offset;
                    brother.Y = 544224 + offset;
                    brother.Z = 5188;
                    brother.Heading = 3450;
                    break;
            }
            brother.Name = name;
            brother.Model = 33745;
            brother.Size = 50;
            brother.Level = 50;
            brother.CurrentRegionID = this.CurrentRegionID;
            brother.Realm = 0;
            brother.CurrentSpeed = 0;
            brother.MaxSpeedBase = 170;
            brother.GuildName = "";
            brother.RoamingRange = 800;
            brother.RespawnInterval = 5 * 60 * 1000;
            brother.BodyType = 0;

            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 400;
            brother.SetOwnBrain(brain);
            brother.AutoSetStats();
            brother.Flags |= eFlags.SWIMMING;
            if (debug) brother.debug = true;
            brother.AddToWorld();

            log.Warn("Master Level - 1.2 - " + name + " " + (this.CurrentRegionID == albregion ? "ALB" : this.CurrentRegionID == hibregion ? "HIB" : "MID") + " Added.");
            return brother;
        }

        //------------STATIC-------------
        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Warn("Master Level - 1.2 -¨Initializing Event...");
            if (Lornas.Albion == true)
            {
                SpawnLornas(albregion);
                log.Warn("Master Level - 1.2 -¨Lornas ALB added.");
            }
            if (Lornas.Midgard == true)
            {
                SpawnLornas(midregion);
                log.Warn("Master Level - 1.2 -¨Lornas MID added.");
            }
            if (Lornas.Hibernia == true)
            {
                SpawnLornas(hibregion);
                log.Warn("Master Level - 1.2 -¨Lornas HIB added.");
            }
            log.Warn("Master Level - 1.2 -¨Event Initialized !");
        }

        public static void SpawnLornas(int region) //Spawn Lornas
        {
            Lornas Lornas = new Lornas();
            Lornas.Name = "Lornas";
            Lornas.GuildName = "";
            Lornas.Model = 33746;
            Lornas.Realm = 0;
            Lornas.CurrentRegionID = (ushort)region;
            Lornas.Size = 50;
            Lornas.Level = 71;
            Lornas.X = 354560;
            Lornas.Y = 549120;
            Lornas.Z = 6488;
            Lornas.Heading = 3720;
            Lornas.RoamingRange = 0;
            Lornas.Flags |= GameNPC.eFlags.PEACE;
            Lornas.CurrentSpeed = 0;
            Lornas.MaxSpeedBase = 170;
            Lornas.AutoSetStats();
            Lornas.Flags |= eFlags.SWIMMING;
            Lornas.AddToWorld();
        }

    }

    //Borjan Borjad Class
    public class BorjanBorjad : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;
        public bool Initialized = false;

        //Overrides
        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            this.RespawnInterval = Util.Random(Lornas.MinRespawn, Lornas.MaxRespawn) * 60 * 1000;
            if (debug) log.Warn("Master Level - 1.2 - " + Name + " will respawn.");
            base.StartRespawn();
        }
        public override bool AddToWorld()
        {

            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {
            MLCreditHelper.CreditML((byte)1, (byte)2, killer, true, false, (byte)Lornas.MinimumLevel);
            base.Die(killer);
        }

    }

}