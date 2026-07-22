//---------------------------------------------------------
//----------------------ML1.7 - Rassa ---------------------
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

    //RassaController
    public class RassaController : GameNPC
    {

        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

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

        //Config Values
        public bool Available = true; //Event Available
        public static int MinRepop = 20; //Minimum Repop Time for Rassa (minutes)
        public static int MaxRepop = 30; //Maximum Repop Time for Rassa (minutes)
        public static int DepopTime = 10; //Depop Time

        //Launched - Step
        public bool Launched = true;
        public bool NamedAvailable = true;
        public int Step = 0;

        //Npc List
        public List<StoneController> StonesControllerList = new List<StoneController>();
        public List<StatuePop> Wave1List = new List<StatuePop>();
        public List<StatuePop> Wave2List = new List<StatuePop>();
        public List<StatuePop> Wave3List = new List<StatuePop>();
        public List<StatuePop> Wave4List = new List<StatuePop>();
        public List<StatuePop> Wave5List = new List<StatuePop>();
        public List<StatuePop> Wave6List = new List<StatuePop>();
        public List<StatuePop> Wave7List = new List<StatuePop>();
        public List<StatuePop> Wave8List = new List<StatuePop>();
        public List<StatuePop> Wave9List = new List<StatuePop>();
        public List<StatuePop> Wave10List = new List<StatuePop>();
        public List<StatuePop> Wave11List = new List<StatuePop>();
        public List<StatuePop> Wave12List = new List<StatuePop>();
        public Rassa RassaNPC;

        //Stone's controller Array
        public int[,] StonesControllerArray = {
			{429124,536166,8354}, //0---StonesController
			{429047,536597,8354}, //1---StonesController
			{428539,536573,8354}, //2---StonesController
			{428483,536148,8354}, //3---StonesController
			{428792,535967,8354}, //4---StonesController
		};

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

            //Enable Step
            EnableStep();

            //Check Encounter State
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(CheckEncounterState), (1 * 1000));

            return base.AddToWorld();
        }
        public override bool Interact(GamePlayer player)
        {
            if (base.Interact(player))
            {
                TurnTo(player, 1500);

                if (player.Level >= MinimumLevel)
                {
                    if (CheckStones() == true) SayTo(player, "Good");
                    if (CheckStones() == false) SayTo(player, "Bad");
                }

                return true;
            }

            return false;
        }

        //Enable - Disable mlstep - Check Stone States
        public void EnableStep()
        {

            //Spawn Stones
            for (int i = 0; i < 5; i++)
            {
                SpawnStoneController(StonesControllerArray[i, 0], StonesControllerArray[i, 1], StonesControllerArray[i, 2]);
            }
            if (this.CurrentRegionID == albregion)
            {
                log.Warn("Master Level - 1.7 - Now Available.");
            }
            if (this.CurrentRegionID == midregion)
            {
                log.Warn("Master Level - 1.7 - Now Available.");
            }
            if (this.CurrentRegionID == hibregion)
            {
                log.Warn("Master Level - 1.7 - Now Available.");
            }
            Available = true;

        }

        //Timers
        public int CheckEncounterState(ECSGameTimer timer)
        {
            if (Available == true)
            {
                if (RassaNPC != null)
                {
                    if (RassaNPC.IsAlive == true)
                    {
                        new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(CheckEncounterState), (1 * 1000));
                        return 0;
                    }
                }
                if (Step == 0)
                {
                    if (CheckStones() == true)
                    {
                        SpawnAWave(1);
                        Step = 1;
                    }
                }
                else if (Step == 1)
                {
                    if (CheckStones() == true)
                    {
                        bool Next = true;
                        foreach (StatuePop Statue in Wave1List)
                        {
                            if (Statue.IsAlive == true)
                            {
                                Next = false;
                            }
                        }
                        if (Next == true)
                        {
                            SpawnAWave(2);
                            Step = 2;
                        }
                    }
                    else
                    {
                        DespawnAllStatues();
                        Step = 0;
                    }
                }
                else if (Step == 2)
                {
                    if (CheckStones() == true)
                    {
                        bool Next = true;
                        foreach (StatuePop Statue in Wave2List)
                        {
                            if (Statue.IsAlive == true)
                            {
                                Next = false;
                            }
                        }
                        if (Next == true)
                        {
                            SpawnAWave(3);
                            Step = 3;
                        }
                    }
                    else
                    {
                        DespawnAllStatues();
                        Step = 0;
                    }
                }
                else if (Step == 3)
                {
                    if (CheckStones() == true)
                    {
                        bool Next = true;
                        foreach (StatuePop Statue in Wave3List)
                        {
                            if (Statue.IsAlive == true)
                            {
                                Next = false;
                            }
                        }
                        if (Next == true)
                        {
                            SpawnAWave(4);
                            Step = 4;
                        }
                    }
                    else
                    {
                        DespawnAllStatues();
                        Step = 0;
                    }
                }
                else if (Step == 4)
                {
                    if (CheckStones() == true)
                    {
                        bool Next = true;
                        foreach (StatuePop Statue in Wave4List)
                        {
                            if (Statue.IsAlive == true)
                            {
                                Next = false;
                            }
                        }
                        if (Next == true)
                        {
                            SpawnAWave(5);
                            Step = 5;
                        }
                    }
                    else
                    {
                        DespawnAllStatues();
                        Step = 0;
                    }
                }
                else if (Step == 5)
                {
                    if (CheckStones() == true)
                    {
                        bool Next = true;
                        foreach (StatuePop Statue in Wave5List)
                        {
                            if (Statue.IsAlive == true)
                            {
                                Next = false;
                            }
                        }
                        if (Next == true)
                        {
                            SpawnAWave(6);
                            Step = 6;
                        }
                    }
                    else
                    {
                        DespawnAllStatues();
                        Step = 0;
                    }
                }
                else if (Step == 6)
                {
                    if (CheckStones() == true)
                    {
                        bool Next = true;
                        foreach (StatuePop Statue in Wave6List)
                        {
                            if (Statue.IsAlive == true)
                            {
                                Next = false;
                            }
                        }
                        if (Next == true)
                        {
                            SpawnAWave(7);
                            Step = 7;
                        }
                    }
                    else
                    {
                        DespawnAllStatues();
                        Step = 0;
                    }
                }
                else if (Step == 7)
                {
                    if (CheckStones() == true)
                    {
                        bool Next = true;
                        foreach (StatuePop Statue in Wave7List)
                        {
                            if (Statue.IsAlive == true)
                            {
                                Next = false;
                            }
                        }
                        if (Next == true)
                        {
                            SpawnAWave(8);
                            Step = 8;
                        }
                    }
                    else
                    {
                        DespawnAllStatues();
                        Step = 0;
                    }
                }
                else if (Step == 8)
                {
                    if (CheckStones() == true)
                    {
                        bool Next = true;
                        foreach (StatuePop Statue in Wave8List)
                        {
                            if (Statue.IsAlive == true)
                            {
                                Next = false;
                            }
                        }
                        if (Next == true)
                        {
                            SpawnAWave(9);
                            Step = 9;
                        }
                    }
                    else
                    {
                        DespawnAllStatues();
                        Step = 0;
                    }
                }
                else if (Step == 9)
                {
                    if (CheckStones() == true)
                    {
                        bool Next = true;
                        foreach (StatuePop Statue in Wave9List)
                        {
                            if (Statue.IsAlive == true)
                            {
                                Next = false;
                            }
                        }
                        if (Next == true)
                        {
                            SpawnAWave(10);
                            Step = 10;
                        }
                    }
                    else
                    {
                        DespawnAllStatues();
                        Step = 0;
                    }
                }
                else if (Step == 10)
                {
                    if (CheckStones() == true)
                    {
                        bool Next = true;
                        foreach (StatuePop Statue in Wave10List)
                        {
                            if (Statue.IsAlive == true)
                            {
                                Next = false;
                            }
                        }
                        if (Next == true)
                        {
                            SpawnAWave(11);
                            Step = 11;
                        }
                    }
                    else
                    {
                        DespawnAllStatues();
                        Step = 0;
                    }
                }
                else if (Step == 11)
                {
                    if (CheckStones() == true)
                    {
                        bool Next = true;
                        foreach (StatuePop Statue in Wave11List)
                        {
                            if (Statue.IsAlive == true)
                            {
                                Next = false;
                            }
                        }
                        if (Next == true)
                        {
                            SpawnAWave(12);
                            Step = 12;
                        }
                    }
                    else
                    {
                        DespawnAllStatues();
                        Step = 0;
                    }
                }
                else if (Step == 12)
                {
                    if (CheckStones() == true)
                    {
                        bool Next = true;
                        foreach (StatuePop Statue in Wave12List)
                        {
                            if (Statue.IsAlive == true)
                            {
                                Next = false;
                            }
                        }
                        if (Next == true)
                        {
                            if (NamedAvailable == true)
                            {
                                SpawnRassa();
                                NamedAvailable = false;
                                new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(TimerNamedAvailable), (Util.Random(MinRepop, MaxRepop) * 60 * 1000));
                                Step = 0;
                            }
                            else
                            {
                                Step = 0;
                            }
                        }
                    }
                    else
                    {
                        DespawnAllStatues();
                        Step = 0;
                    }
                }

                //Reload Timer
                new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(CheckEncounterState), (1 * 1000));
            }
            else
            {
                new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(CheckEncounterState), (10 * 1000));
            }
            return 0;
        }

        //Check Stones
        public bool CheckStones()
        {
            int ActivatedStones = 0;
            foreach (StoneController stone in StonesControllerList)
            {
                if (stone.PlayerOnMe == true) ActivatedStones = ActivatedStones + 1;
            }
            if (ActivatedStones > 4)
            {
                return true;
            }
            else
            {
                if (Step != 0)
                {
                    foreach (GamePlayer p in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        p.Out.SendMessage("The portal before you slows its pulsing indicating one of the pads has become vacant!", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                    }
                }
                return false;
            }
        }

        //Spawn Stone's controller - Spawn Statue
        public void SpawnStoneController(int X, int Y, int Z)
        {
            StoneController Stone = new StoneController();
            Stone.Name = "Stone controller";
            Stone.GuildName = "";
            Stone.Model = 665;
            Stone.Realm = 0;
            Stone.CurrentRegionID = this.CurrentRegionID;
            Stone.Size = 62;
            Stone.Level = 70;
            Stone.X = X;
            Stone.Y = Y;
            Stone.Z = Z;
            Stone.Heading = (ushort)Util.Random(200, 3000);
            Stone.RoamingRange = 0;
            Stone.CurrentSpeed = 0;
            Stone.MaxSpeedBase = 191;
            Stone.AutoSetStats();
            Stone.RespawnInterval = 15 * 60 * 1000;
            Stone.BodyType = 0;
            Stone.Flags |= eFlags.PEACE;
            Stone.Flags |= eFlags.CANTTARGET;
            Stone.Effect_Enable = true;
            StonesControllerList.Add(Stone);
            Stone.AddToWorld();
        }
        public void SpawnStatue(int wave, int X, int Y, int Z, ushort H)
        {
            StatuePop Statue = new StatuePop();
            Statue.Name = "Propylais";
            Statue.GuildName = "";
            Statue.Realm = 0;
            Statue.CurrentRegionID = this.CurrentRegionID;
            Statue.X = X;
            Statue.Y = Y;
            Statue.Z = Z;
            Statue.Heading = H;
            Statue.RoamingRange = 0;
            Statue.CurrentSpeed = 0;
            Statue.MaxSpeedBase = 191;
            Statue.AutoSetStats();
            Statue.BodyType = 0;
            if (Util.Random(1, 2) == 1)
            {
                Statue.Model = 993;
            }
            else
            {
                Statue.Model = 984;
                GameNpcInventoryTemplate template = new GameNpcInventoryTemplate();
                template.AddNPCEquipment(eInventorySlot.DistanceWeapon, 570);
                template.CloseTemplate();
                Statue.Inventory = template;
                Statue.SwitchWeapon(eActiveWeaponSlot.Distance);
            }
            if (wave == 1)
            {
                Wave1List.Add(Statue);
                Statue.Size = 20;
                Statue.Level = 46;
            }
            if (wave == 2)
            {
                Wave2List.Add(Statue);
                Statue.Size = 22;
                Statue.Level = 47;
            }
            if (wave == 3)
            {
                Wave3List.Add(Statue);
                Statue.Size = 25;
                Statue.Level = 50;
            }
            if (wave == 4)
            {
                Wave4List.Add(Statue);
                Statue.Size = 35;
                Statue.Level = 52;
            }
            if (wave == 5)
            {
                Wave5List.Add(Statue);
                Statue.Size = 30;
                Statue.Level = 54;
            }
            if (wave == 6)
            {
                Wave6List.Add(Statue);
                Statue.Size = 43;
                Statue.Level = 56;
            }
            if (wave == 7)
            {
                Wave7List.Add(Statue);
                Statue.Size = 45;
                Statue.Level = 58;
            }
            if (wave == 8)
            {
                Wave8List.Add(Statue);
                Statue.Size = 50;
                Statue.Level = 60;
            }
            if (wave == 9)
            {
                Wave9List.Add(Statue);
                Statue.Size = 52;
                Statue.Level = 62;
            }
            if (wave == 10)
            {
                Wave10List.Add(Statue);
                Statue.Size = 55;
                Statue.Level = 64;
            }
            if (wave == 11)
            {
                Wave11List.Add(Statue);
                Statue.Size = 60;
                Statue.Level = 66;
            }
            if (wave == 12)
            {
                Wave12List.Add(Statue);
                Statue.Size = 65;
                Statue.Level = 68;
            }
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 200;
            Statue.SetOwnBrain(brain);
            Statue.AddToWorld();
        }
        public void SpawnAWave(int wave)
        {
            int numberofmobs = Util.Random(3, 4);
            SpawnStatue(wave, 428800, 536380, 8345, 92);
            SpawnStatue(wave, 428859, 536318, 8345, 3109);
            SpawnStatue(wave, 428799, 536258, 8345, 2044);
            if (numberofmobs == 4) SpawnStatue(wave, 428740, 536319, 8345, 1076);
        }
        public void DespawnAllStatues()
        {
            foreach (StatuePop statue in Wave1List)
            {
                statue.Health = 0;
                statue.Delete();
            }
            Wave1List.Clear();
            foreach (StatuePop statue in Wave2List)
            {
                statue.Health = 0;
                statue.Delete();
            }
            Wave2List.Clear();
            foreach (StatuePop statue in Wave3List)
            {
                statue.Health = 0;
                statue.Delete();
            }
            Wave3List.Clear();
            foreach (StatuePop statue in Wave4List)
            {
                statue.Health = 0;
                statue.Delete();
            }
            Wave4List.Clear();
            foreach (StatuePop statue in Wave5List)
            {
                statue.Health = 0;
                statue.Delete();
            }
            Wave5List.Clear();
            foreach (StatuePop statue in Wave6List)
            {
                statue.Health = 0;
                statue.Delete();
            }
            Wave6List.Clear();
            foreach (StatuePop statue in Wave7List)
            {
                statue.Health = 0;
                statue.Delete();
            }
            Wave7List.Clear();
            foreach (StatuePop statue in Wave8List)
            {
                statue.Health = 0;
                statue.Delete();
            }
            Wave8List.Clear();
            foreach (StatuePop statue in Wave9List)
            {
                statue.Health = 0;
                statue.Delete();
            }
            Wave9List.Clear();
            foreach (StatuePop statue in Wave10List)
            {
                statue.Health = 0;
                statue.Delete();
            }
            Wave10List.Clear();
            foreach (StatuePop statue in Wave11List)
            {
                statue.Health = 0;
                statue.Delete();
            }
            Wave11List.Clear();
            foreach (StatuePop statue in Wave12List)
            {
                statue.Health = 0;
                statue.Delete();
            }
            Wave12List.Clear();

        }
        public void SpawnRassa()
        {
            Rassa Statue = new Rassa();
            Statue.Name = "Rassa";
            Statue.GuildName = "";
            Statue.Realm = 0;
            Statue.CurrentRegionID = this.CurrentRegionID;
            Statue.X = 428799;
            Statue.Y = 536314;
            Statue.Z = 8345;
            Statue.Heading = (ushort)Util.Random(200, 3000);
            Statue.RoamingRange = 0;
            Statue.CurrentSpeed = 0;
            Statue.MaxSpeedBase = 191;
            Statue.AutoSetStats();
            Statue.BodyType = 0;
            Statue.Model = 996;
            Statue.Size = 112;
            Statue.Level = 72;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 200;
            Statue.SetOwnBrain(brain);
            Statue.parent = this;
            Statue.AddToWorld();
            RassaNPC = Statue;
        }

        //Event Completed
        public void EventCompleted(GameObject killer)
        {
            MLCreditHelper.CreditML((byte)1, (byte)7, killer, true, false, (byte)MinimumLevel);
            Step = 0;
        }

        //Timer Repop
        public int TimerNamedAvailable(ECSGameTimer timer)
        {
            NamedAvailable = true;
            return 0;
        }

        //Spawn RassaController
        public static void SpawnRassaController(int region)
        {
            RassaController RassaControllerNpc = new RassaController();
            RassaControllerNpc.Name = "1.7 - Controller";
            RassaControllerNpc.GuildName = "ToaManager";
            RassaControllerNpc.Realm = eRealm.None;
            RassaControllerNpc.Model = 665;
            RassaControllerNpc.CurrentRegionID = (ushort)region;
            RassaControllerNpc.Size = 50;
            RassaControllerNpc.Level = 100;
            RassaControllerNpc.X = 429278;
            RassaControllerNpc.Y = 535751;
            RassaControllerNpc.Z = 8313;
            RassaControllerNpc.Heading = 447;
            RassaControllerNpc.RoamingRange = 0;
            RassaControllerNpc.CurrentSpeed = 0;
            RassaControllerNpc.MaxSpeedBase = 191;
            RassaControllerNpc.Flags |= eFlags.PEACE;
            RassaControllerNpc.Flags |= eFlags.CANTTARGET;
            RassaControllerNpc.RespawnInterval = 10 * 60 * 1000;
            RassaControllerNpc.AddToWorld();
        }

        //Load Event
        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Warn("Master Level - 1.7 - Initializing Objects ...");
            #region Rassa's Mirror
            #region Base
            DbItemTemplate Mirrortest = (DbItemTemplate)GameServer.Database.FindObjectByKey<DbItemTemplate>("ToaManager_Rassa's_Mirror");
            if (Mirrortest == null)
            {
                log.Warn("Master Level - 1.7 - Rassa's Mirror not Found ...");
                DbItemTemplate Kirkleis_Ring = new DbItemTemplate();
                Kirkleis_Ring.PackageID = "ToaManager001";
                Kirkleis_Ring.Id_nb = "ToaManager_Rassa's_Mirror";
                Kirkleis_Ring.Name = "Rassa's Mirror";
                Kirkleis_Ring.Level = 35;
                Kirkleis_Ring.Durability = 50000;
                Kirkleis_Ring.MaxDurability = 50000;
                Kirkleis_Ring.Condition = 50000;
                Kirkleis_Ring.MaxCondition = 50000;
                Kirkleis_Ring.Quality = 85;
                Kirkleis_Ring.DPS_AF = 0;
                Kirkleis_Ring.SPD_ABS = 0;
                Kirkleis_Ring.Hand = 0;
                Kirkleis_Ring.Type_Damage = 0;
                Kirkleis_Ring.Object_Type = 41;
                Kirkleis_Ring.Item_Type = 24;
                Kirkleis_Ring.Color = 0;
                Kirkleis_Ring.Emblem = 0;
                Kirkleis_Ring.Effect = 0;
                Kirkleis_Ring.Weight = 5;
                Kirkleis_Ring.Model = 592;
                Kirkleis_Ring.Extension = 0;
                Kirkleis_Ring.Bonus = 0;
                Kirkleis_Ring.Bonus1 = 0;
                Kirkleis_Ring.Bonus2 = 0;
                Kirkleis_Ring.Bonus3 = 0;
                Kirkleis_Ring.Bonus4 = 0;
                Kirkleis_Ring.Bonus5 = 0;
                Kirkleis_Ring.Bonus6 = 0;
                Kirkleis_Ring.Bonus7 = 0;
                Kirkleis_Ring.Bonus8 = 0;
                Kirkleis_Ring.Bonus9 = 0;
                Kirkleis_Ring.Bonus10 = 0;
                Kirkleis_Ring.ExtraBonus = 0;
                Kirkleis_Ring.Bonus1Type = 0;
                Kirkleis_Ring.Bonus2Type = 0;
                Kirkleis_Ring.Bonus3Type = 0;
                Kirkleis_Ring.Bonus4Type = 0;
                Kirkleis_Ring.Bonus5Type = 0;
                Kirkleis_Ring.Bonus6Type = 0;
                Kirkleis_Ring.Bonus7Type = 0;
                Kirkleis_Ring.Bonus8Type = 0;
                Kirkleis_Ring.Bonus9Type = 0;
                Kirkleis_Ring.Bonus10Type = 0;
                Kirkleis_Ring.ExtraBonusType = 0;
                Kirkleis_Ring.IsPickable = false;
                Kirkleis_Ring.IsDropable = true;
                Kirkleis_Ring.CanDropAsLoot = false;
                Kirkleis_Ring.IsTradable = false;
                Kirkleis_Ring.MaxCount = 1;
                Kirkleis_Ring.PackSize = 1;
                Kirkleis_Ring.Charges = 0;
                Kirkleis_Ring.MaxCharges = 0;
                Kirkleis_Ring.Charges1 = 0;
                Kirkleis_Ring.MaxCharges1 = 0;
                Kirkleis_Ring.SpellID = 0;
                Kirkleis_Ring.SpellID1 = 0;
                Kirkleis_Ring.ProcSpellID = 0;
                Kirkleis_Ring.ProcSpellID1 = 0;
                Kirkleis_Ring.PoisonSpellID = 0;
                Kirkleis_Ring.PoisonMaxCharges = 0;
                Kirkleis_Ring.PoisonCharges = 0;
                Kirkleis_Ring.Realm = 0;
                Kirkleis_Ring.AllowedClasses = "";
                Kirkleis_Ring.CanUseEvery = 0;
                //Kirkleis_Ring.Flags = 0;
                //Kirkleis_Ring.BonusLevel = 0;
                Kirkleis_Ring.Description = "";
                //Kirkleis_Ring.IsIndestructible = false;
                //Kirkleis_Ring.IsNotLosingDur = false;
                //Kirkleis_Ring.LevelRequirement = 0;
                Kirkleis_Ring.Price = 0;
                //Kirkleis_Ring.ProcChance = 0;
                Kirkleis_Ring.ClassType = "";
                //NKirkleis_Ring.SalvageYieldID = 0;
                GameServer.Database.AddObject(Kirkleis_Ring);
                log.Warn("Master Level - 1.7 - Rassa's Mirror added !");
            }
            #endregion Base
            #region Update1
            //Update1
            #endregion
            #endregion Rassa's Mirror
            log.Warn("Master Level - 1.7 - Objects Initialized !");
            log.Warn("Master Level - 1.7 - Initializing Event...");
            if (Albion == true)
            {
                SpawnRassaController(albregion);
                log.Warn("Master Level - 1.7 - RassaController ALB added.");
            }
            if (Midgard == true)
            {
                SpawnRassaController(midregion);
                log.Warn("Master Level - 1.7 - RassaController MID added.");
            }
            if (Hibernia == true)
            {
                SpawnRassaController(hibregion);
                log.Warn("Master Level - 1.7 - RassaController HIB added.");
            }
            log.Warn("Master Level - 1.7 - Event Initialized !");
        }

    }

    //StoneController
    public class StoneController : EffectsNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;
        public bool PlayerOnMe = false;

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
            //Start Timer
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(CheckState), (1 * 1000));

            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {
            base.Die(killer);
        }

        //Check State
        public int CheckState(ECSGameTimer timer)
        {
            bool PlayerOnMeNow = false;
            foreach (GamePlayer player in GetPlayersInRadius(60))
            {
                if (player.IsAlive == true)
                {
                    PlayerOnMeNow = true;
                    break;
                }
            }
            if ((PlayerOnMeNow == true) && (PlayerOnMe == false)) PlayerOnMe = true;
            if ((PlayerOnMeNow == false) && (PlayerOnMe == true)) PlayerOnMe = false;

            //Effect
            if (PlayerOnMe == true)
            {
                this.Effect_CastTimeSec = 1;
                this.Effect_DelayMs = 1000;
                this.Effect_ID = 805;
            }
            else
            {
                this.Effect_CastTimeSec = 1;
                this.Effect_DelayMs = 1000;
                this.Effect_ID = 801;
            }

            //Reload Timer
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(CheckState), (1 * 1000));
            return 0;

        }

    }

    //Statue
    public class StatuePop : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
        }
        public override bool AddToWorld()
        {
            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {
            base.Die(killer);
        }

    }

    //Rassa
    public class Rassa : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;
        public RassaController parent;

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
        }
        public override bool AddToWorld()
        {

            //Launch Delete Timer
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(FightTimeElapsed), (RassaController.DepopTime * 60 * 1000));

            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {

            //Loot
            MLCreditHelper.GiveItem(killer, this, "ToaManager_Rassa's_Mirror", 0, 1);

            //base Die
            base.Die(killer);

            //Credit
            parent.EventCompleted(killer);

            //Launch Delete Timer
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(DeleteMe), (10 * 1000));

        }

        //Timer Delete
        public int DeleteMe(ECSGameTimer timer)
        {
            this.Health = 0;
            this.Delete();
            return 0;
        }

        //Timer Fight Elapsed
        public int FightTimeElapsed(ECSGameTimer timer)
        {
            this.Health = 0;
            this.Delete();
            return 0;
        }

    }

}