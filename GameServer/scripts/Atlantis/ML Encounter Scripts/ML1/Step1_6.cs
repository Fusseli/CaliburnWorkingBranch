//---------------------------------------------------------
//---------------------ML1.6 - Curses ---------------------
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

    //Kirkleis
    public class Kirkleis : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static bool debug = true;

        //Minimum Level
        public static int MinimumLevel = 40;

        //Minimum Respawn Time - Maximum Respawn Time ( in minutes )
        public static int MinRespawn = 30;
        public static int MaxRespawn = 45;

        //Realm Regions
        public static int albregion = 73;
        public static int midregion = 30;
        public static int hibregion = 130;

        //AOE
        public int AOECoolDown = 15;
        public int AOEDamage = 128;

        //Initialized
        public bool Initialized = false;

        //Realm Available for this Step
        public static bool Albion = false;
        public static bool Midgard = false;
        public static bool Hibernia = true;

        public List<KirkleisTrap> TrapsList = new List<KirkleisTrap>();

        #region Traps Array
        //Traps Array
        public int[,] TrapsArray = {
			{437680,548631,8152},
			{437192,548422,8187},
			{433637,552448,8190},
			{433154,552306,8137},
			{433878,549958,8258},
            {433449,553238,8058},
			{435103,547337,8237},
			{436971,547831,8189},
			{435799,547423,8243},
			{434280,553306,8055},
            {434570,552772,8169},
			{434922,553317,8139},
			{435790,547049,8152},
			{435346,552787,8204},
			{433261,549115,8221},
            {435105,546952,8126},
			{434422,550423,8287},
			{434734,547312,8227},
			{436873,547191,8035},
			{435731,553180,8092},
            {436031,552587,8208},
			{436476,552967,8151},
			{435024,550705,8310},
			{436364,552233,8215},
			{436809,552297,8187},
            {437412,547958,8132},
			{436293,547596,8218},
			{436357,547242,8177},
			{436502,551631,8211},
			{437028,551392,8158},
            {433343,551885,8193},
			{436743,550807,8214},
			{434021,552786,8146},
			{437328,550801,8130},
			{437130,550181,8204},
            {437562,550011,8165},
			{436832,548926,8231},
			{434350,549705,8271},
			{437698,549189,8102},
			{437317,548928,8183},
            {432844,551673,8133},
			{434454,548443,8262},
			{433084,551149,8214},
			{432759,550897,8177},
			{432207,550119,8053},
            {432974,550363,8215},
			{433356,550120,8245},
			{433041,549595,8226},
			{432668,548881,8124},
			{432938,548695,8174},
            {433422,548506,8229},
			{433024,547977,8214},
			{433514,547562,8135},
			{433676,548181,8224},
			{434759,547720,8238},
            {434099,547634,8223},
			{434618,548114,8253},
			{435300,547636,8251},
			{435726,547734,8259},
			{436167,547876,8239},
            {436606,547977,8216},
			{436438,548557,8236},
			{436131,548363,8240},
			{435677,548078,8255},
			{435057,548222,8256},
            {434823,548892,8285},
			{434453,548824,8272},
			{433778,548784,8244},
			{436405,550939,8233},
			{433534,549639,8247},
            {434093,549551,8251},
			{434763,549470,8288},
			{434670,549939,8287},
			{434201,550184,8283},
			{433745,550267,8264},
            {436329,550476,8250},
			{433577,550911,8250},
			{433648,551471,8230},
			{434107,551453,8236},
			{434513,551044,8278},
            {434767,550486,8307},
			{435363,550863,8292},
			{434992,551404,8278},
			{434198,548102,8238},
			{433903,551848,8226},
            {434538,552333,8214},
			{435211,552368,8241},
			{435301,551946,8267},
			{435407,551480,8275},
			{435744,551392,8281},
            {436159,551257,8252},
			{435959,550667,8253},
			{435479,550356,8290},
			{436014,550102,8266},
			{436640,549878,8253},
            {436769,549562,8252},
			{436422,549155,8250},
			{435915,548939,8250},
			{435488,548600,8261},
			{434421,549195,8271},
            {434736,548556,8270},
			{435819,552064,8265},
			{434065,550786,8272},
			{435947,549526,8259},
			{435156,548642,8274},
            {435272,549839,8292},
			{434408,546970,8147},
			{434544,551710,8246},
			{435037,550167,8314},
			{434021,548415,8245},
            {435690,549835,8271},
			{435268,548429,8266},
			{435902,548639,8243},
			{436288,549911,8260},
			{434419,547874,8239},
		};
        #endregion Traps Array

        //Override
        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            this.RespawnInterval = Util.Random(MinRespawn, MaxRespawn) * 60 * 1000;
            base.StartRespawn();
        }
        public override bool AddToWorld()
        {

            if (Initialized == false)
            {
                //Spawn Traps
                for (int i = 0; i < 115; i++)
                {
                    SpawnTrap(TrapsArray[i, 0], TrapsArray[i, 1], TrapsArray[i, 2]);
                }
                if (this.CurrentRegionID == albregion) log.Warn("Master Level - 1.6 - Traps Added !");
                if (this.CurrentRegionID == midregion) log.Warn("Master Level - 1.6 - Traps Added !");
                if (this.CurrentRegionID == hibregion) log.Warn("Master Level - 1.6 - Traps Added !");
                Initialized = true;
            }

            //Launch Timer AoE
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(AOE), (1000));

            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {

            //Loot
            MLCreditHelper.GiveItem(killer, this, "ToaManager_Kirkleis'_Ring", 0, 3);

            //Die
            base.Die(killer);

            //Credit
            MLCreditHelper.CreditML((byte)1, (byte)6, killer, true, false, (byte)MinimumLevel);

        }

        //Spawn KirKleis - Spawn a Trap
        public static void SpawnKirkleis(int region)
        {
            Kirkleis KirKleis = new Kirkleis();
            KirKleis.Name = "KirKleis";
            KirKleis.GuildName = "";
            KirKleis.Realm = eRealm.None;
            KirKleis.Model = 995;
            KirKleis.CurrentRegionID = (ushort)region;
            KirKleis.Size = 150;
            KirKleis.Level = 75;
            KirKleis.X = 435315;
            KirKleis.Y = 549214;
            KirKleis.Z = 8370;
            KirKleis.Heading = 1683;
            KirKleis.RoamingRange = 0;
            KirKleis.CurrentSpeed = 0;
            KirKleis.MaxSpeedBase = 191;
            KirKleis.RespawnInterval = 10 * 60 * 1000;
            KirKleis.AutoSetStats();
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 200;
            KirKleis.SetOwnBrain(brain);
            KirKleis.AddToWorld();
        }
        public void SpawnTrap(int X, int Y, int Z)
        {
            KirkleisTrap Trap = new KirkleisTrap();
            Trap.Name = "Trap";
            Trap.GuildName = "";
            Trap.Model = 665;
            Trap.Realm = 0;
            Trap.CurrentRegionID = this.CurrentRegionID;
            Trap.Size = 62;
            Trap.Level = 70;
            Trap.X = X;
            Trap.Y = Y;
            Trap.Z = Z;
            Trap.Heading = (ushort)Util.Random(200, 3000);
            Trap.RoamingRange = 0;
            Trap.CurrentSpeed = 0;
            Trap.MaxSpeedBase = 191;
            Trap.AutoSetStats();
            Trap.RespawnInterval = 15 * 60 * 1000;
            Trap.BodyType = 0;
            Trap.Flags |= eFlags.PEACE;
            Trap.Flags |= eFlags.CANTTARGET;
            TrapsList.Add(Trap);
            if (debug == true) Trap.debug = true;
            Trap.AddToWorld();
        }

        //AOE
        public int AOE(ECSGameTimer timer)
        {
            //AoE
            if (this.IsAttacking)
            {
                List<GamePlayer> PlayerAoEList = new List<GamePlayer>();
                foreach (GamePlayer player in GetPlayersInRadius(550))
                {

                    bool BeltInPlayerRadius = false;
                    DbInventoryItem FindLoot = player.Inventory.GetFirstItemByID("ToaManager_Negative_Absolution_Belt", eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                    if (FindLoot != null)
                    {
                        BeltInPlayerRadius = true;
                    }
                    foreach (GamePlayer p2 in player.GetPlayersInRadius(150))
                    {
                        DbInventoryItem FindLootRadius = p2.Inventory.GetFirstItemByID("ToaManager_Negative_Absolution_Belt", eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                        if (FindLoot != null)
                        {
                            BeltInPlayerRadius = true;
                        }
                    }
                    if (BeltInPlayerRadius == false)
                    {
                        if (PlayerAoEList.Contains(player) == false)
                        {
                            PlayerAoEList.Add(player);
                        }
                    }
                }
                foreach (GamePlayer playerAoe in PlayerAoEList)
                {
                    if (playerAoe.IsAlive == true)
                    {
                        playerAoe.Out.SendMessage("Kirkleis Fire's burns you for " + AOEDamage + " damages!", eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);
                        playerAoe.Out.SendSpellEffectAnimation(this, playerAoe, 310, 0, false, 1);
                        playerAoe.TakeDamage(this, eDamageType.Heat, AOEDamage, 0);
                        foreach (GamePlayer onlookers in playerAoe.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                        {
                            if (onlookers != playerAoe)
                            {
                                onlookers.Out.SendSpellEffectAnimation(this, playerAoe, 310, 0, false, 1);
                                onlookers.Out.SendMessage("Kirkleis Fire's burns " + playerAoe.Name + " for " + AOEDamage + " damages!", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                            }
                        }
                    }
                }

            }

            //Reload Timer
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(AOE), (AOECoolDown * 1000));
            return 0;

        }

        //Load Event
        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Warn("Master Level - 1.6 - Initializing Objects ...");
            #region Kirkleis' Ring
            #region Base
            DbItemTemplate ring = (DbItemTemplate)GameServer.Database.FindObjectByKey<DbItemTemplate>("ToaManager_Kirkleis'_Ring");
            if (ring == null)
            {
                log.Warn("Master Level - 1.6 - Kirkleis' Ring not Found ...");
                DbItemTemplate Kirkleis_Ring = new DbItemTemplate();
                Kirkleis_Ring.PackageID = "ToaManager001";
                Kirkleis_Ring.Id_nb = "ToaManager_Kirkleis'_Ring";
                Kirkleis_Ring.Name = "Kirkleis' Ring";
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
                Kirkleis_Ring.Item_Type = 35;
                Kirkleis_Ring.Color = 0;
                Kirkleis_Ring.Emblem = 0;
                Kirkleis_Ring.Effect = 0;
                Kirkleis_Ring.Weight = 1;
                Kirkleis_Ring.Model = 103;
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
                log.Warn("Master Level - 1.6 - Kirkleis' Ring added !");
            }
            #endregion Base
            #region Update1
            //Update1
            #endregion
            #endregion Kirkleis' Ring
            log.Warn("Master Level - 1.6 - Objects Initialized !");
            log.Warn("Master Level - 1.6 - Initializing Event...");
            if (Albion == true)
            {
                SpawnKirkleis(albregion);
                log.Warn("Master Level - 1.6 - Kirkleis ALB added.");
            }
            if (Midgard == true)
            {
                SpawnKirkleis(midregion);
                log.Warn("Master Level - 1.6 - Kirkleis MID added.");
            }
            if (Hibernia == true)
            {
                SpawnKirkleis(hibregion);
                log.Warn("Master Level - 1.6 - Kirkleis HIB added.");
            }
            log.Warn("Master Level - 1.6 - Event Initialized !");
        }

    }

    //Trap
    public class KirkleisTrap : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;

        //PopNpcList
        public List<KirkleisTrapPopNPC> PopTrapsList = new List<KirkleisTrapPopNPC>();

        //RespawnTrapTime (minutes)
        public int MinRespawn = 5;
        public int MaxRespawn = 10;

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
        }
        public override bool AddToWorld()
        {

            //Launch Timer
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(CheckRadius), 500);

            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {
            
        }

        //Check Radius
        public int CheckRadius(ECSGameTimer timer)
        {
            bool PlayerInRadius = false;
            bool GemInRadius = false;
            int NextTimer = 1;
            GamePlayer Targetplayer = null;
            foreach (GamePlayer player in GetPlayersInRadius(200))
            {
                if (player.IsAlive == true)
                {
                    if (PlayerInRadius == false) PlayerInRadius = true;
                    DbInventoryItem FindLoot = player.Inventory.GetFirstItemByID("ToaManager_Nedfall_Entrapment_Gem", eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                    if (FindLoot != null)
                    {
                        GemInRadius = true;
                    }
                    else
                    {
                        Targetplayer = player;
                    }
                }
            }
            if (PlayerInRadius == true)
            {
                if (GemInRadius == true)
                {
                    NextTimer = 6;
                }
                else
                {
                    Pop(Targetplayer);
                    foreach (GamePlayer player in GetPlayersInRadius(550))
                    {
                        //player.Out.SendSoundEffect()
                        player.Out.SendMessage("Trap !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                    }
                    NextTimer = Util.Random(MinRespawn, MaxRespawn) * 60 * 2;
                }
            }

            //Reload Timer
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(CheckRadius), (NextTimer * 500));
            return 0;

        }

        //Pop
        public void Pop(GamePlayer TargetedPlayer)
        {
            PopTrapsList.Clear();
            int Number = Util.Random(2, 4);
            if (Number > 0) SpawnPopNPC(1);
            if (Number > 1) SpawnPopNPC(2);
            if (Number > 2) SpawnPopNPC(3);
            if (Number > 3) SpawnPopNPC(4);
            if (TargetedPlayer != null)
            {
                foreach (KirkleisTrapPopNPC mob in PopTrapsList)
                {
                    mob.StartAttack(TargetedPlayer);
                }
            }
        }

        //Spawn A PopNPC
        public void SpawnPopNPC(int number)
        {
            KirkleisTrapPopNPC TrapPopNPC = new KirkleisTrapPopNPC();
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
            int Choose = Util.Random(1, 4);
            if (Choose == 1)
            {
                TrapPopNPC.Name = "Steam elemental";
                TrapPopNPC.Model = 913;
            }
            else if (Choose == 2)
            {
                TrapPopNPC.Name = "Dust devil";
                TrapPopNPC.Model = 1269;
            }
            else if (Choose == 3)
            {
                TrapPopNPC.Name = "Harpy";
                TrapPopNPC.Model = 992;
            }
            else if (Choose == 4)
            {
                TrapPopNPC.Name = "Nuker nedfall";
                TrapPopNPC.Model = 929;
            }
            TrapPopNPC.GuildName = "";
            TrapPopNPC.Realm = eRealm.None;
            TrapPopNPC.CurrentRegionID = this.CurrentRegionID;
            TrapPopNPC.Size = 50;
            TrapPopNPC.Level = (byte)Util.Random(51, 54);
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

    }

    //KirkleisTrapPopNPC
    public class KirkleisTrapPopNPC : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;
        public KirkleisTrap Parent;

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
        }
        public override bool AddToWorld()
        {
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(Depop), (10 * 60 * 500));
            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {
            base.Die(killer);
        }

        //Depop Timer
        public int Depop(ECSGameTimer timer)
        {
            Parent.PopTrapsList.Remove(this);
            this.Health = 0;
            this.Delete();
            return 0;

        }

    }

}