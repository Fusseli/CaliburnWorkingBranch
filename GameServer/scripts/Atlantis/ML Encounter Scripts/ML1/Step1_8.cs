//---------------------------------------------------------
//------------------ML1.8 - Requin Azure ------------------
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

//Using Mgr

namespace DOL.GS.Atlantis
{

    //HammerheadController
    public class HammerheadController : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static bool debug = true;

        //Minimum Level
        public static int MinimumLevel = 40;

        //Realm Regions
        public static int albregion = 73;
        public static int midregion = 30;
        public static int hibregion = 130;

        //Realm Available for this Step
        public static bool Albion = false;
        public static bool Midgard = false;
        public static bool Hibernia = true;

        //Minimum Respawn Time - Maximum Respawn Time ( in minutes )
        public static int MinRespawn = 12;
        public static int MaxRespawn = 15;

        //HammerheadSharks Array
        public int[,] HammerheadSharksArray = {
			{343004,643214,4817},
			{342667,642207,6196},
			{342613,641331,5443},
			{340284,640354,5357},
			{341387,639670,4881},
            {340773,641428,5437},
			{343172,639619,4853},
			{340224,641359,4794},
			{342249,639273,6212},
			{342103,640967,5008},
            {340733,640452,6644},
			{341673,641579,5931},
			{340859,639059,6212},
			{341588,639820,5655},
			{343684,640732,5116},
		};

        //NpcList
        public List<HammerheadShark> HammerheadSharksList = new List<HammerheadShark>();
        public AzureShark AzureShark;

        //Override
        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            base.StartRespawn();
        }
        public override bool AddToWorld()
        {
            //HammerheadShark
            for (int i = 0; i < 15; i++)
            {
                SpawnHammerheadShark(HammerheadSharksArray[i, 0], HammerheadSharksArray[i, 1], HammerheadSharksArray[i, 2]);
            }
            if (this.CurrentRegionID == albregion) log.Warn("Master Level - 1.8 -¨HammerheadShark ALB added.");
            if (this.CurrentRegionID == midregion) log.Warn("Master Level - 1.8 -¨HammerheadShark MID added.");
            if (this.CurrentRegionID == hibregion) log.Warn("Master Level - 1.8 -¨HammerheadShark HIB added.");

            //StartEncounter Timer
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(StartEncounter), (10 * 1000));

            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {
            base.Die(killer);
        }

        //Spawn HammerheadController
        public static void SpawnHammerheadController(int region)
        {
            HammerheadController HammerheadControllerNPC = new HammerheadController();
            HammerheadControllerNPC.Name = "1.8 - Controller";
            HammerheadControllerNPC.GuildName = "ToaManager";
            HammerheadControllerNPC.Realm = eRealm.None;
            HammerheadControllerNPC.Model = 665;
            HammerheadControllerNPC.CurrentRegionID = (ushort)region;
            HammerheadControllerNPC.Size = 50;
            HammerheadControllerNPC.Level = 1;
            HammerheadControllerNPC.X = 341611;
            HammerheadControllerNPC.Y = 641338;
            HammerheadControllerNPC.Z = 4545;
            HammerheadControllerNPC.Heading = 3422;
            HammerheadControllerNPC.RoamingRange = 0;
            HammerheadControllerNPC.CurrentSpeed = 0;
            HammerheadControllerNPC.MaxSpeedBase = 191;
            HammerheadControllerNPC.RespawnInterval = 10 * 60 * 1000;
            HammerheadControllerNPC.Flags |= eFlags.CANTTARGET;
            HammerheadControllerNPC.Flags |= eFlags.PEACE;
            HammerheadControllerNPC.AutoSetStats();
            HammerheadControllerNPC.AddToWorld();
        }
        public void SpawnHammerheadShark(int X, int Y, int Z)
        {
            HammerheadShark Shark = new HammerheadShark();
            Shark.Name = "Hammerhead shark";
            Shark.GuildName = "";
            Shark.Model = 33740;
            Shark.Realm = 0;
            Shark.CurrentRegionID = this.CurrentRegionID;
            Shark.Size = 50;
            Shark.Level = 70;
            Shark.X = X;
            Shark.Y = Y;
            Shark.Z = Z;
            Shark.Heading = (ushort)Util.Random(200, 3000);
            Shark.RoamingRange = 600;
            Shark.CurrentSpeed = 0;
            Shark.MaxSpeedBase = 191;
            Shark.AutoSetStats();
            Shark.RespawnInterval = 0;
            Shark.BodyType = 0;
            Shark.Flags = eFlags.SWIMMING;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 600;
            Shark.SetOwnBrain(brain);
            HammerheadSharksList.Add(Shark);
            Shark.Parent = this;
            if (debug == true) Shark.debug = true;
            Shark.AddToWorld();
        }
        public void SpawnAzureShark(int X, int Y, int Z)
        {
            AzureShark Shark = new AzureShark();
            Shark.Name = "Azure shark";
            Shark.GuildName = "";
            Shark.Model = 33739;
            Shark.Realm = eRealm.Door;
            Shark.CurrentRegionID = this.CurrentRegionID;
            Shark.Size = 50;
            Shark.Level = 60;
            Shark.X = X;
            Shark.Y = Y;
            Shark.Z = Z;
            Shark.Heading = (ushort)Util.Random(200, 3000);
            Shark.RoamingRange = 300;
            Shark.CurrentSpeed = 0;
            Shark.MaxSpeedBase = 191;
            Shark.AutoSetStats();
            Shark.BodyType = 0;
            Shark.Flags = eFlags.SWIMMING;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 0;
            brain.AggroRange = 0;
            Shark.SetOwnBrain(brain);
            Shark.Parent = this;
            if (debug == true) Shark.debug = true;
            AzureShark = Shark;
            Shark.AddToWorld();
        }

        //Loot Rubis -- End Encounter
        public void AzureDie()
        {
            //
            foreach (HammerheadShark mob in HammerheadSharksList)
            {
                if (mob.IsAlive == true)
                {
                    mob.Rubis = true;
                    break;
                }
            }

            //Set Rubis on a HammerHead Shark
            foreach (HammerheadShark mob in HammerheadSharksList)
            {
                if (mob.IsAlive == true)
                {
                    mob.Rubis = true;
                    break;
                }
            }

            //Send Information
            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                player.Out.SendMessage("Azure shark loot a Rubis !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
            }

            //Log
            if(debug==true) log.Warn("Master Level - 1.8 - AzureDie.");

            //Start EndEncounter Timer
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(EndEncounterTimer), (10 * 60 * 1000));
        }
        public void EndEncounter()
        {
            if (debug == true) log.Warn("Master Level - 1.8 - EndEncounter.");
            foreach (HammerheadShark mob in HammerheadSharksList)
            {
                mob.Rubis = false;
            }
        }

        //Timer
        public int StartEncounter(ECSGameTimer timer)
        {
            //Spawn Azure Shark
           SpawnAzureShark(343062, 640899, 5905);

            //Attack AzureShark
            foreach (HammerheadShark mob in HammerheadSharksList)
            {
                mob.StartAttack(AzureShark);
            }
            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                player.Out.SendMessage("A Azure Shark is attacked !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
            }

            //Log And Reload Timer
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(StartEncounter), (Util.Random(MinRespawn,MaxRespawn) * 60 * 1000));
            if (this.CurrentRegionID == albregion) log.Warn("Master Level - 1.8 - ALB Available !");
            if (this.CurrentRegionID == midregion) log.Warn("Master Level - 1.8 - MID Available !");
            if (this.CurrentRegionID == hibregion) log.Warn("Master Level - 1.8 - HIB Available !");
            return 0;
        }
        public int EndEncounterTimer(ECSGameTimer timer)
        {
            EndEncounter();
            return 0;
        }

        //Load Event
        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Warn("Master Level - 1.8 - Initializing Objects ...");
            #region Many Facetted Ruby
            #region Base
            DbItemTemplate rubistest = (DbItemTemplate)GameServer.Database.FindObjectByKey<DbItemTemplate>("ToaManager_Many_Facetted_Ruby");
            if (rubistest == null)
            {
                log.Warn("Master Level - 1.8 - Many Facetted Ruby not Found ...");
                DbItemTemplate Ruby = new DbItemTemplate();
                Ruby.PackageID = "ToaManager001";
                Ruby.Id_nb = "ToaManager_Many_Facetted_Ruby";
                Ruby.Name = "Many Facetted Ruby";
                Ruby.Level = 35;
                Ruby.Durability = 50000;
                Ruby.MaxDurability = 50000;
                Ruby.Condition = 50000;
                Ruby.MaxCondition = 50000;
                Ruby.Quality = 85;
                Ruby.DPS_AF = 0;
                Ruby.SPD_ABS = 0;
                Ruby.Hand = 0;
                Ruby.Type_Damage = 0;
                Ruby.Object_Type = 41;
                Ruby.Item_Type = 24;
                Ruby.Color = 0;
                Ruby.Emblem = 0;
                Ruby.Effect = 0;
                Ruby.Weight = 1;
                Ruby.Model = 110;
                Ruby.Extension = 0;
                Ruby.Bonus = 0;
                Ruby.Bonus1 = 0;
                Ruby.Bonus2 = 0;
                Ruby.Bonus3 = 0;
                Ruby.Bonus4 = 0;
                Ruby.Bonus5 = 0;
                Ruby.Bonus6 = 0;
                Ruby.Bonus7 = 0;
                Ruby.Bonus8 = 0;
                Ruby.Bonus9 = 0;
                Ruby.Bonus10 = 0;
                Ruby.ExtraBonus = 0;
                Ruby.Bonus1Type = 0;
                Ruby.Bonus2Type = 0;
                Ruby.Bonus3Type = 0;
                Ruby.Bonus4Type = 0;
                Ruby.Bonus5Type = 0;
                Ruby.Bonus6Type = 0;
                Ruby.Bonus7Type = 0;
                Ruby.Bonus8Type = 0;
                Ruby.Bonus9Type = 0;
                Ruby.Bonus10Type = 0;
                Ruby.ExtraBonusType = 0;
                Ruby.IsPickable = false;
                Ruby.IsDropable = true;
                Ruby.CanDropAsLoot = false;
                Ruby.IsTradable = false;
                Ruby.MaxCount = 1;
                Ruby.PackSize = 1;
                Ruby.Charges = 0;
                Ruby.MaxCharges = 0;
                Ruby.Charges1 = 0;
                Ruby.MaxCharges1 = 0;
                Ruby.SpellID = 0;
                Ruby.SpellID1 = 0;
                Ruby.ProcSpellID = 0;
                Ruby.ProcSpellID1 = 0;
                Ruby.PoisonSpellID = 0;
                Ruby.PoisonMaxCharges = 0;
                Ruby.PoisonCharges = 0;
                Ruby.Realm = 0;
                Ruby.AllowedClasses = "";
                Ruby.CanUseEvery = 0;
                //Ruby.Flags = 0;
                //Ruby.BonusLevel = 0;
                Ruby.Description = "";
                //Ruby.IsIndestructible = false;
                //Ruby.IsNotLosingDur = false;
                //Ruby.LevelRequirement = 0;
                Ruby.Price = 0;
                //Ruby.ProcChance = 0;
                Ruby.ClassType = "";
                //Ruby.SalvageYieldID = 0;
                GameServer.Database.AddObject(Ruby);
                log.Warn("Master Level - 1.8 - Many Facetted Ruby added !");
            }
            #endregion Base
            #region Update1
            //Update1
            #endregion
            #endregion Kirkleis' Ring
            log.Warn("Master Level - 1.8 - Objects Initialized !");
            log.Warn("Master Level - 1.8 - Initializing Event...");
            if (Albion == true)
            {
                SpawnHammerheadController(albregion);
                log.Warn("Master Level - 1.8 - HammerheadController ALB added.");
            }
            if (Midgard == true)
            {
                SpawnHammerheadController(midregion);
                log.Warn("Master Level - 1.8 - HammerheadController MID added.");
            }
            if (Hibernia == true)
            {
                SpawnHammerheadController(hibregion);
                log.Warn("Master Level - 1.8 - HammerheadController HIB added.");
            }
            log.Warn("Master Level - 1.8 - Event Initialized !");
        }

    }

    //HammerheadShark
    public class HammerheadShark : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;
        public HammerheadController Parent;
        public bool Rubis = false;

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            base.StartRespawn();
        }
        public override bool AddToWorld()
        {
            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {
            GamePlayer Player = killer as GamePlayer;
            if (Rubis == true)
            {
                Parent.EndEncounter();

                //Loot
                MLCreditHelper.GiveItem(killer, this, "ToaManager_Many_Facetted_Ruby", 0, 3);
                if (Player != null)
                    Player.Out.SendMessage("You Loot Rubis!", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);

                //Base Die
                base.Die(killer);

                //Credit
                MLCreditHelper.CreditML((byte)1, (byte)8, killer, true, false, (byte)HammerheadController.MinimumLevel);

                return;
            }
            base.Die(killer);
        }

    }

    //AzureShark
    public class AzureShark : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;
        public HammerheadController Parent;

        //Override
        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
        }
        public override bool AddToWorld()
        {

            //AutoDepopTimer
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(Depop), (2 * 60 * 1000));

            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {
            if (killer is HammerheadShark)
            {
                Parent.AzureDie();
            }
            base.Die(killer);
            this.Delete();
        }

        //Timer AutoDepop
        public int Depop(ECSGameTimer timer)
        {
            if (this.IsAlive == true)
            {
                this.Health = 0;
                this.Delete();
                if (this.CurrentRegionID == HammerheadController.albregion && debug == true) log.Warn("Master Level - 1.8 - AzureShark Depop !");
                if (this.CurrentRegionID == HammerheadController.midregion && debug == true) log.Warn("Master Level - 1.8 - AzureShark Depop !");
                if (this.CurrentRegionID == HammerheadController.hibregion && debug == true) log.Warn("Master Level - 1.8 - AzureShark Depop !");
                return 0;
            }
            return 0;
        }

    }

}