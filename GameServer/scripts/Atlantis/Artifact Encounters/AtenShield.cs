using System;
using System.Collections;
using System.Collections.Generic;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.AI.Brain;
using DOL.Events;
using DOL.Database;
using log4net;
using System.Reflection;
using DOL.GS.Atlantis;

namespace DOL.GS.Atlantis
{
    public class AtenShieldController : GameNPC
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static bool debug = false;

        public static int AlbRegionID = 73;
        public static int MidRegionID = 30;
        public static int HibRegionID = 130;

        public static bool Albion = false;
        public static bool Midgard = false;
        public static bool Hibernia = true;

        public static AtenShieldController AtenShieldControllerAlbion;
        public static AtenShieldController AtenShieldControllerMidgard;
        public static AtenShieldController AtenShieldControllerHibernia;

        public static int MinimumLevel = 30;
        public static int Chance = 20;
        public static int MinRepop = 20;
        public static int MaxRepop = 30;

        public int[,] ChestLocsArray = {
			{272445,579737,8255},
			{280924,582491,8117},
			{273918,580721,8226},
			{273243,580943,8171},
			{287569,577704,8250},
            {277051,577935,8194},
			{279847,576514,8120},
			{282796,577355,8170},
			{285269,578002,8389},
			{276221,579559,8321},
            {281372,583044,8071},
			{272573,579697,8278},
			{285881,577819,8352},
			{284370,575928,8164},
			{279085,580494,8315},
            {277653,578628,8272},
			{287555,578029,8308},
			{278553,580331,8312},
			{285275,577829,8389},
            {283503,576604,8166},
			{285575,578426,8372},
			{282792,577175,8164},
			{279920,576689,8131},
			{285224,578382,8381},
            {275895,579077,8374},
			{279432,577227,8172},
			{286965,577718,8282},
			{271999,579633,8194},
			{286140,580689,8185},
            {272210,579150,8225},
			{272376,579241,8259},
			{282598,576843,8155},
			{285342,577676,8379},
			{281082,582167,8134},
            {274102,580729,8231},
			{277380,578432,8244},
			{272761,578395,8254},
			{286385,580643,8205},
			{284161,575959,8164},
			{275524,579887,8404},
            {272787,579089,8332},
			{279244,580753,8294},
			{273356,580923,8176},
			{279102,580206,8322},
			{281447,582510,8116},
			{285781,578523,8380},
        };

        public List<AtenShieldChest> ChestList = new List<AtenShieldChest>();

		[ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Warn("Artifact - Aten's Shield - Initializing Event...");
            if (Albion == true)
            {
                SpawnCallia(AlbRegionID);
                log.Warn("Artifact - Aten's Shield - Callia ALB added.");
                SpawnAtenShieldController(AlbRegionID);
                log.Warn("Artifact - Aten's Shield - AtenShieldController ALB added.");
            }
            if (Midgard == true)
            {
                SpawnCallia(MidRegionID);
                log.Warn("Artifact - Aten's Shield - Callia MID added.");
                SpawnAtenShieldController(MidRegionID);
                log.Warn("Artifact - Aten's Shield - AtenShieldController MID added.");
            }
            if (Hibernia == true)
            {
                SpawnCallia(HibRegionID);
                log.Warn("Artifact - Aten's Shield - Callia HIB added.");
                SpawnAtenShieldController(HibRegionID);
                log.Warn("Artifact - Aten's Shield - AtenShieldController HIB added.");
            }
            log.Warn("Artifact - Aten's Shield - Event Initialized !");
        }

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            base.StartRespawn();
        }
        public override bool AddToWorld()
        {
            for (int i = 0; i < 46; i++)
            {
                SpawnChest(ChestLocsArray[i, 0], ChestLocsArray[i, 1], ChestLocsArray[i, 2]);
            }
            if (this.CurrentRegionID == AlbRegionID)
                log.Warn("Artifact - Aten's Shield - Chests ALB added.");
            else if (this.CurrentRegionID == MidRegionID)
                log.Warn("Artifact - Aten's Shield - Chests MID added.");
            else if (this.CurrentRegionID == HibRegionID)
                log.Warn("Artifact - Aten's Shield - Chests HIB added.");

            return base.AddToWorld();
        }
        public override bool Interact(GamePlayer player)
        {
            if (base.Interact(player))
            {
                TurnTo(player, 1500);
                return true;
            }
            return false;
        }

        public void SpawnChest(int X, int Y, int Z)
        {
            AtenShieldChest Chest = new AtenShieldChest();
            Chest.Name = "Treasure chest";
            Chest.GuildName = "";
            Chest.Model = 1493;
            Chest.Realm = 0;
            Chest.CurrentRegionID = this.CurrentRegionID;
            Chest.Level = 1;
            Chest.X = X;
            Chest.Y = Y;
            Chest.Z = Z;
            Chest.Heading = (ushort)Util.Random(200, 3000);
            Chest.RespawnInterval = 15 * 60 * 1000;
            Chest.BodyType = 0;
            Chest.Flags |= eFlags.PEACE;
            Chest.Flags |= eFlags.CANTTARGET;
            ChestList.Add(Chest);
            if (debug == true) Chest.debug = true;
            Chest.AddToWorld();
        }

        public static void SpawnAtenShieldController(int region)
        {
            AtenShieldController AtenShieldControllerNPC = new AtenShieldController();
            AtenShieldControllerNPC.Name = "AtenShield - Controller";
            AtenShieldControllerNPC.GuildName = "ToaManager";
            AtenShieldControllerNPC.Realm = eRealm.None;
            AtenShieldControllerNPC.Model = 2273;
            AtenShieldControllerNPC.CurrentRegionID = (ushort)region;
            AtenShieldControllerNPC.Size = 50;
            AtenShieldControllerNPC.Level = 100;
            AtenShieldControllerNPC.X = 282125;
            AtenShieldControllerNPC.Y = 579076;
            AtenShieldControllerNPC.Z = 8466;
            AtenShieldControllerNPC.Heading = 952;
            AtenShieldControllerNPC.RoamingRange = 0;
            AtenShieldControllerNPC.CurrentSpeed = 0;
            AtenShieldControllerNPC.MaxSpeedBase = 191;
            AtenShieldControllerNPC.RespawnInterval = 10 * 60 * 1000;
            AtenShieldControllerNPC.Flags |= eFlags.CANTTARGET;
            AtenShieldControllerNPC.Flags |= eFlags.PEACE;
            AtenShieldControllerNPC.AutoSetStats();
            AtenShieldControllerNPC.AddToWorld();
        }
        public static void SpawnCallia(int region)
        {
            Callia CalliaNPC = new Callia();
            CalliaNPC.Name = "Callia";
            CalliaNPC.GuildName = "";
            CalliaNPC.Realm = eRealm.None;
            CalliaNPC.Model = 1179;
            CalliaNPC.CurrentRegionID = (ushort)region;
            CalliaNPC.Size = 50;
            CalliaNPC.Level = 70;
            CalliaNPC.X = 267005;
            CalliaNPC.Y = 564759;
            CalliaNPC.Z = 8120;
            CalliaNPC.Heading = 193;
            CalliaNPC.RoamingRange = 0;
            CalliaNPC.CurrentSpeed = 0;
            CalliaNPC.MaxSpeedBase = 191;
            CalliaNPC.RespawnInterval = 10 * 60 * 1000;
            CalliaNPC.Flags |= eFlags.PEACE;
            CalliaNPC.AutoSetStats();
            CalliaNPC.AddToWorld();
        }
    }

    public class AtenShieldChest : GameMovingObject
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = true;

        public List<TreasureChestPopNPC> PopTrapsList = new List<TreasureChestPopNPC>();

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;
            if ((player.Level >= AtenShieldController.MinimumLevel) && (player.IsWithinRadius(this,100) == true))
            {
                OpenChest(player);
            }
            return true;
        }
        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            this.RespawnInterval = Util.Random(AtenShieldController.MinRepop, AtenShieldController.MaxRepop) * 60 * 1000;
            base.StartRespawn();
        }
        public override bool AddToWorld()
        {
            return base.AddToWorld();
        }

        public void OpenChest(GamePlayer ThePlayer)
        {
            ThePlayer.Out.SendMessage("You open the Chest !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
            foreach (GamePlayer p in ThePlayer.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (p != ThePlayer)
               {
                   p.Out.SendMessage(ThePlayer.Name + " open the Chest !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
               }
           }

            if (Util.Random(1, AtenShieldController.Chance) == 1)
            {
                ThePlayer.Out.SendMessage("You found the Aten's Shield !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                foreach (GamePlayer p in ThePlayer.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    if (p != ThePlayer)
                    {
                        p.Out.SendMessage(ThePlayer.Name + " found the Aten's Shield !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                    }
                }
            }
            else
            {
                foreach (GamePlayer p in ThePlayer.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    p.Out.SendMessage("Several monsters pop out of the box!", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                }
                Pop(ThePlayer);
            }

            this.Health = 0;
            this.RemoveFromWorld();
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(TimerRespawn), Util.Random(AtenShieldController.MinRepop, AtenShieldController.MaxRepop) * 60 * 1000);
        }
        public int TimerRespawn(ECSGameTimer timer)
        {
            this.AddToWorld();
            return 0;
        }

        public void SpawnPopNPC(int number,int Choose,int Level)
        {
            TreasureChestPopNPC TrapPopNPC = new TreasureChestPopNPC();
            if (number == 1)
            {
                TrapPopNPC.X = this.X + 50;
                TrapPopNPC.Y = this.Y;
                TrapPopNPC.Z = this.Z;
            }
            else if (number == 2)
            {
                TrapPopNPC.X = this.X - 50;
                TrapPopNPC.Y = this.Y;
                TrapPopNPC.Z = this.Z;
            }
            else if (number == 3)
            {
                TrapPopNPC.X = this.X;
                TrapPopNPC.Y = this.Y + 50;
                TrapPopNPC.Z = this.Z;
            }
            else if (number == 4)
            {
                TrapPopNPC.X = this.X;
                TrapPopNPC.Y = this.Y - 50;
                TrapPopNPC.Z = this.Z;
            }
            if (Choose == 1)
            {
                TrapPopNPC.Name = "Djinni Dust";
                TrapPopNPC.Model = 1196;
                TrapPopNPC.Size = 35;
            }
            else if (Choose == 2)
            {
                TrapPopNPC.Name = "Uraeus";
                TrapPopNPC.Model = 1234;
                TrapPopNPC.Size = 50;
            }
            else if (Choose == 3)
            {
                TrapPopNPC.Name = "Greater Iaculus";
                TrapPopNPC.Model = 1233;
                TrapPopNPC.Size = 50;
            }
            else if (Choose == 4)
            {
                TrapPopNPC.Name = "Scorpion";
                TrapPopNPC.Model = 1263;
                TrapPopNPC.Size = 50;
            }
            else if (Choose == 5)
            {
                TrapPopNPC.Name = "Olemo statue";
                TrapPopNPC.Model = 994;
                TrapPopNPC.Size = 50;
            }
            else if (Choose == 6)
            {
                TrapPopNPC.Name = "Mikoos statue";
                TrapPopNPC.Model = 997;
                TrapPopNPC.Size = 50;
            }
            else if (Choose == 7)
            {
                TrapPopNPC.Name = "Baby Crocodile";
                TrapPopNPC.Model = 1258;
                TrapPopNPC.Size = 40;
            }
            else if (Choose == 8)
            {
                TrapPopNPC.Name = "Dust Elemental";
                TrapPopNPC.Model = 1269;
                TrapPopNPC.Size = 33;
            }
            TrapPopNPC.GuildName = "";
            TrapPopNPC.Realm = eRealm.None;
            TrapPopNPC.CurrentRegionID = this.CurrentRegionID;
            TrapPopNPC.Level = (byte)Level;
            TrapPopNPC.Heading = (ushort)Util.Random(200, 3000);
            TrapPopNPC.RoamingRange = 0;
            TrapPopNPC.CurrentSpeed = 0;
            TrapPopNPC.MaxSpeedBase = 191;
            TrapPopNPC.AutoSetStats();
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 400;
            TrapPopNPC.SetOwnBrain(brain);
            if (debug == true) TrapPopNPC.debug = true;
            PopTrapsList.Add(TrapPopNPC);
            TrapPopNPC.Parent = this;
            TrapPopNPC.AddToWorld();
        }
        public void Pop(GamePlayer TargetedPlayer)
        {
            PopTrapsList.Clear();
            int Number = Util.Random(2, 4);
            int Choose = Util.Random(1, 8);
            int Level = 0;
            if (Number < 3)
            {
                Level = Util.Random(55, 80);
                if (Number > 0) SpawnPopNPC(1, Choose, Level);
                if (Number > 1) SpawnPopNPC(2, Choose, Level);
            }
            else
            {
                Level = Util.Random(40, 55);
                if (Number > 0) SpawnPopNPC(1, Choose, Level);
                if (Number > 1) SpawnPopNPC(2, Choose, Level);
                if (Number > 2) SpawnPopNPC(3, Choose, Level);
                if (Number > 3) SpawnPopNPC(4, Choose, Level);
            }
            if (TargetedPlayer != null)
            {
                foreach (TreasureChestPopNPC mob in PopTrapsList)
                {
                    mob.StartAttack(TargetedPlayer);
                }
            }
        }
    }

    public class TreasureChestPopNPC : GameNPC
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;
        public AtenShieldChest Parent;

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
        }
        public override bool AddToWorld()
        {
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(Depop), 10 * 60 * 500);
            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {
            base.Die(killer);
        }

        public int Depop(ECSGameTimer timer)
        {
            Parent.PopTrapsList.Remove(this);
            this.Health = 0;
            this.Delete();
            return 0;
        }
    }

    public class Callia : GameNPC
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static bool debug = true;

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            base.StartRespawn();
        }
        public override bool Interact(GamePlayer player)
        {
            TurnTo(player, 5000);
            this.TargetObject = player;

            if (!base.Interact(player)) return false;

            if (player.Level > AtenShieldController.MinimumLevel)
            {
                player.Out.SendMessage("I laugh aloud for the thing you seek, find it on an island meek."
                    +" Thought quiet and alone it seems. Danger will pervade your dreams."
                    +" Seek amongst the treasure holds, I hope you find your weight in gold."
                    +" If not your bleached bones will sing of days when you were a living thing.", eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            }
            return true;
        }
    };
}
