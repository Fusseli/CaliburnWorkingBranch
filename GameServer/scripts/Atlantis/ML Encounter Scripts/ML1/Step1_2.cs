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

        //Borjad-Borjan
        public BorjanBorjad ActualBorjadBorjan = new BorjanBorjad();

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
                    if (ActualBorjadBorjan.IsAttacking == true)
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
                            if (ActualBorjadBorjan.IsAlive == true && ActualBorjadBorjan.IsAttacking == false)
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

                        if (ActualBorjadBorjan.IsAlive == true)
                        {
                            string Msg = "Thank you for helping us. We only require that you rid us of this nuisance."
                            +"By disposing of even one of these men you would be doing us a great service."
                            +"Ant belongings you find on these thieves you may keep for yourself as payment.";
                            t.Out.SendMessage(Msg, eChatType.CT_Say, eChatLoc.CL_PopupWindow);

                            string Msg2 = "Rumors surrounding Borjad and Borjan are plentiful."
                            +"I am unsure of where they may be,but when they last left us, they headed to ";

                            if (ActualBorjadBorjan.X == 354659)
                            {
                                //Sud et Ouest de Mésothalassa sur un gros rocher
                                Msg2 = Msg2 + "Southwest of Mésothalassa .";
                                t.Out.SendMessage(Msg2, eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                            }
                            else if (ActualBorjadBorjan.X == 353554)
                            {
                                //Nord dans le santuaire de Kitara
                                Msg2 = Msg2 + "north , around Kitara .";
                                t.Out.SendMessage(Msg2, eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                            }
                            else if (ActualBorjadBorjan.X == 382238)
                            {
                                //Est dans le territoire Naxos sur un gros rocher
                                Msg2 = Msg2 + "East on Naxos territory .";
                                t.Out.SendMessage(Msg2, eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                            }
                            else if (ActualBorjadBorjan.X == 333822)
                            {
                                //Ouest dans le territoire Skyros, sur un rocher
                                Msg2 = Msg2 + "West , on Skyros territory .";
                                t.Out.SendMessage(Msg2, eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                            }
                        }

                        #endregion accept
                    }
                    break;
            }

            return true;
        }
        public override bool AddToWorld()
        {
            //Spawn Borjan-Borjad
            SpawnBorjanBorjad();

            return base.AddToWorld();
        }

        //Spawn Borjan-Borjad
        public void SpawnBorjanBorjad()
        {
            int RandLottery; 
            RandLottery = Util.Random(1, 2);
            if (RandLottery == 1)
            {
                ActualBorjadBorjan.Name = "Borjan";
            }
            else if (RandLottery == 2)
            {
                ActualBorjadBorjan.Name = "Borjad";
            }
            RandLottery = Util.Random(1, 4);
            if (RandLottery == 1)
            {
                //Sud et Ouest de Mésothalassa sur un gros rocher
                ActualBorjadBorjan.X = 354659;
                ActualBorjadBorjan.Y = 568220;
                ActualBorjadBorjan.Z = 6718;
                ActualBorjadBorjan.Heading = 2793;
            }
            if (RandLottery == 2)
            {
                //Nord dans le santuaire de Kitara
                ActualBorjadBorjan.X = 353554;
                ActualBorjadBorjan.Y = 530965;
                ActualBorjadBorjan.Z = 5008;
                ActualBorjadBorjan.Heading = 3142;
            }
            if (RandLottery == 3)
            {
                //Est dans le territoire Naxos sur un gros rocher
                ActualBorjadBorjan.X = 382238;
                ActualBorjadBorjan.Y = 547833;
                ActualBorjadBorjan.Z = 5410;
                ActualBorjadBorjan.Heading = 4011;
            }
            if (RandLottery == 4)
            {
                //Ouest dans le territoire Skyros, sur un rocher
                ActualBorjadBorjan.X = 333822;
                ActualBorjadBorjan.Y = 544224;
                ActualBorjadBorjan.Z = 5188;
                ActualBorjadBorjan.Heading = 3450;
            }
            ActualBorjadBorjan.Model = 33745;
            ActualBorjadBorjan.Size = 50;
            ActualBorjadBorjan.Level = 50;
            ActualBorjadBorjan.CurrentRegionID = this.CurrentRegionID;
            ActualBorjadBorjan.Realm = 0;
            ActualBorjadBorjan.CurrentSpeed = 0;
            ActualBorjadBorjan.MaxSpeedBase = 170;
            ActualBorjadBorjan.GuildName = "";
            ActualBorjadBorjan.RoamingRange = 0;
            ActualBorjadBorjan.RespawnInterval = 5 * 60 * 1000;
            ActualBorjadBorjan.BodyType = 0;

            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 400;
            ActualBorjadBorjan.SetOwnBrain(brain);
            ActualBorjadBorjan.AutoSetStats();
            if (debug == true) ActualBorjadBorjan.debug = true;
            ActualBorjadBorjan.AddToWorld();
            if (this.CurrentRegionID == albregion)
            {
                log.Warn("Master Level - 1.2 -¨BorjadBorjan ALB Added.");
            }
            else if (this.CurrentRegionID == hibregion)
            {
                log.Warn("Master Level - 1.2 -¨BorjadBorjan HIB Added.");
            }
            else if (this.CurrentRegionID == midregion)
            {
                log.Warn("Master Level - 1.2 -¨BorjadBorjan MID Added.");
            }
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
            int RandLottery;
            RandLottery = Util.Random(1, 2);
            if (RandLottery == 1)
            {
                this.Name = "Borjan";
            }
            else if (RandLottery == 2)
            {
                this.Name = "Borjad";
            }
            RandLottery = Util.Random(1, 4);
            if (RandLottery == 1)
            {
                //Sud et Ouest de Mésothalassa sur un gros rocher
                this.SpawnPoint.X = 354659;
                this.SpawnPoint.Y = 568220;
                this.SpawnPoint.Z = 6718;
                this.Heading = 2793;
            }
            if (RandLottery == 2)
            {
                //Nord dans le santuaire de Kitara
                this.SpawnPoint.X = 353554;
                this.SpawnPoint.Y = 530965;
                this.SpawnPoint.Z = 5008;
                this.Heading = 3142;
            }
            if (RandLottery == 3)
            {
                //Est dans le territoire Naxos sur un gros rocher
                this.SpawnPoint.X = 382238;
                this.SpawnPoint.Y = 547833;
                this.SpawnPoint.Z = 5410;
                this.Heading = 4011;
            }
            if (RandLottery == 4)
            {
                //Ouest dans le territoire Skyros, sur un rocher
                this.SpawnPoint.X = 333822;
                this.SpawnPoint.Y = 544224;
                this.SpawnPoint.Z = 5188;
                this.Heading = 3450;
            }
            this.RespawnInterval = Util.Random(Lornas.MinRespawn, Lornas.MaxRespawn) * 60 * 1000;
            if (debug == true) log.Warn("Master Level - 1.2 - BorjanBorjad Change Loc.");
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