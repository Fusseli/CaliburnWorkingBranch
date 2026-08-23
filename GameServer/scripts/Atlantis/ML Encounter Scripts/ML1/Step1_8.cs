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
        public static int MinRespawn = 3;
        public static int MaxRespawn = 5;

        //Minimum/Maximum time until a new ruby is revealed after the previous one was resolved ( in minutes )
        public static int MinRubyRespawn = 15;
        public static int MaxRubyRespawn = 25;

        //Bleed attack / feeding frenzy
        public const int BleedChance = 15;              //Chance per shark hit to start a bleed on the player ( percent )
        public const int BleedTickInterval = 3 * 1000;  //Time between bleed ticks
        public const int BleedTicks = 5;                //Number of bleed ticks
        public const int BleedDamagePerTick = 40;       //Damage per bleed tick
        public const int FrenzyRadius = 1500;           //Sharks within this range of the victim join the frenzy
        public const string BleedingProperty = "Ml18_Bleeding";
        public const string BleedTicksProperty = "Ml18_BleedTicks";
        public const string BleedSourceProperty = "Ml18_BleedSource";

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

        //Ruby state
        public bool RubyActive = false;
        public GameStaticItem RubyItem = null;
        public long NextRubyTime = 0;

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

            //Start Ruby and Azure spawn timers
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(RubyTick), 10 * 1000);
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(AzureSpawnTimerTick), 60 * 1000);

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
            Shark.RoamingRange = 1200;
            Shark.CurrentSpeed = 0;
            Shark.MaxSpeedBase = 191;
            Shark.AutoSetStats();
            Shark.RespawnInterval = 5 * 60 * 1000;
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
            Shark.Realm = eRealm.None;
            Shark.CurrentRegionID = this.CurrentRegionID;
            Shark.Size = 50;
            Shark.Level = 60;
            Shark.X = X;
            Shark.Y = Y;
            Shark.Z = Z;
            Shark.Heading = (ushort)Util.Random(200, 3000);
            Shark.RoamingRange = 500;
            Shark.CurrentSpeed = 0;
            Shark.MaxSpeedBase = 191;
            Shark.AutoSetStats();
            Shark.BodyType = 0;
            Shark.Flags = eFlags.SWIMMING;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 600;
            Shark.SetOwnBrain(brain);
            Shark.Parent = this;
            if (debug == true) Shark.debug = true;
            AzureShark = Shark;
            Shark.AddToWorld();
        }

        //Ruby shark killed - loot and credit
        public void HandleRubySharkDeath(GameObject killer, GameNPC victim)
        {
            EndEncounter();

            //Loot
            MLCreditHelper.GiveItem(killer, victim, "ToaManager_Many_Facetted_Ruby", 1, 3);
            GamePlayer Player = killer as GamePlayer;
            if (Player != null)
                Player.Out.SendMessage("You Loot Rubis!", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);

            //Credit
            MLCreditHelper.CreditML((byte)1, (byte)8, killer, true, false, (byte)MinimumLevel);
        }
        public void EndEncounter()
        {
            if (debug == true) log.Warn("Master Level - 1.8 - EndEncounter.");
            RubyActive = false;
            if (NextRubyTime == 0)
                NextRubyTime = GameLoop.GameLoopTime + Util.Random(MinRubyRespawn, MaxRubyRespawn) * 60 * 1000;
            foreach (HammerheadShark mob in HammerheadSharksList)
            {
                mob.Rubis = false;
            }
            if (AzureShark != null)
                AzureShark.Rubis = false;
        }

        //Timer: reveal the ruby on the seafloor and check pickup
        public int RubyTick(ECSGameTimer timer)
        {
            if (this.ObjectState != GameObject.eObjectState.Active)
                return 0;

            bool playersNear = false;
            foreach (GamePlayer player in GetPlayersInRadius(2000))
            {
                if (player != null && player.IsAlive)
                {
                    playersNear = true;
                    break;
                }
            }

            if (!playersNear)
            {
                if (RubyItem != null)
                {
                    RubyItem.Delete();
                    RubyItem = null;
                }
                RubyActive = false;
                new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(RubyTick), 10 * 1000);
                return 0;
            }

            //Reveal ruby on the seafloor
            if (!RubyActive && GameLoop.GameLoopTime >= NextRubyTime)
            {
                RubyItem = new GameStaticItem();
                RubyItem.CurrentRegion = this.CurrentRegion;
                RubyItem.Name = "Many facetted ruby";
                RubyItem.Model = 110;
                RubyItem.Realm = 0;
                RubyItem.X = this.X;
                RubyItem.Y = this.Y;
                RubyItem.Z = this.Z;
                RubyItem.AddToWorld();
                RubyActive = true;
                NextRubyTime = 0;
                foreach (GamePlayer player in GetPlayersInRadius(2000))
                {
                    player.Out.SendMessage("A ruby is revealed on the seafloor!", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                }
                if (debug == true) log.Warn("Master Level - 1.8 - Ruby revealed.");
            }

            //A shark grabs the ruby
            if (RubyItem != null)
            {
                HammerheadShark grabber = null;
                foreach (HammerheadShark mob in HammerheadSharksList)
                {
                    if (mob.IsAlive && mob.IsWithinRadius(RubyItem, 700))
                    {
                        grabber = mob;
                        break;
                    }
                }
                if (grabber != null)
                {
                    RubyItem.Delete();
                    RubyItem = null;
                    grabber.Rubis = true;
                    foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        player.Out.SendMessage("A hammerhead shark snatches the ruby!", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                    }
                    new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(EndEncounterTimer), (10 * 60 * 1000));
                    if (debug == true) log.Warn("Master Level - 1.8 - A hammerhead shark grabbed the ruby.");
                }
                else if (AzureShark != null && AzureShark.IsAlive && AzureShark.IsWithinRadius(RubyItem, 700))
                {
                    RubyItem.Delete();
                    RubyItem = null;
                    AzureShark.Rubis = true;
                    foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        player.Out.SendMessage("A hammerhead shark snatches the ruby!", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                    }
                    new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(EndEncounterTimer), (10 * 60 * 1000));
                    if (debug == true) log.Warn("Master Level - 1.8 - The azure shark grabbed the ruby.");
                }
                else
                {
                    NudgeSharkToRuby();
                }
            }

            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(RubyTick), 10 * 1000);
            return 0;
        }

        //Timer: spawn an azure shark from time to time
        public int AzureSpawnTimerTick(ECSGameTimer timer)
        {
            if (this.ObjectState == GameObject.eObjectState.Active && (AzureShark == null || !AzureShark.IsAlive))
            {
                SpawnAzureShark(this.X + Util.Random(-500, 500), this.Y + Util.Random(-500, 500), this.Z + 200);
            }
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(AzureSpawnTimerTick), (Util.Random(MinRespawn, MaxRespawn) * 60 * 1000));
            return 0;
        }
        public int EndEncounterTimer(ECSGameTimer timer)
        {
            EndEncounter();
            return 0;
        }

        //Command the nearest alive shark to swim to the ruby
        public void NudgeSharkToRuby()
        {
            if (RubyItem == null)
                return;
            GameNPC nearest = null;
            int nearestDist = int.MaxValue;
            foreach (HammerheadShark mob in HammerheadSharksList)
            {
                if (!mob.IsAlive)
                    continue;
                int dist = mob.GetDistanceTo(RubyItem);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = mob;
                }
            }
            if (AzureShark != null && AzureShark.IsAlive)
            {
                int dist = AzureShark.GetDistanceTo(RubyItem);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = AzureShark;
                }
            }
            if (nearest != null)
            {
                nearest.WalkTo(new Point3D(RubyItem.X, RubyItem.Y, RubyItem.Z), (short)nearest.MaxSpeedBase);
            }
        }

        //Feeding frenzy - all sharks nearby converge on the bleeding victim
        public void FrenzyOn(GamePlayer victim)
        {
            foreach (HammerheadShark mob in HammerheadSharksList)
            {
                if (!mob.IsAlive || !mob.IsWithinRadius(victim, FrenzyRadius))
                    continue;
                if (mob.Brain is StandardMobBrain brain)
                    brain.AddToAggroList(victim, 60);
            }
            if (AzureShark != null && AzureShark.IsAlive && AzureShark.IsWithinRadius(victim, FrenzyRadius))
            {
                if (AzureShark.Brain is StandardMobBrain azureBrain)
                    azureBrain.AddToAggroList(victim, 60);
            }
        }

        //Applies a bleed to a player hit by a shark and triggers the feeding frenzy
        public static void TryApplyBleed(GameNPC shark, AttackData ad)
        {
            if (ad == null || ad.Target is not GamePlayer player || !player.IsAlive)
                return;

            //Already bleeding - no stacking
            if (player.TempProperties.GetProperty<bool>(BleedingProperty, false))
                return;

            player.TempProperties.SetProperty(BleedingProperty, true);
            player.TempProperties.SetProperty(BleedTicksProperty, BleedTicks);
            player.TempProperties.SetProperty(BleedSourceProperty, shark);

            player.Out.SendMessage("The shark's bite leaves you bleeding !", eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);
            foreach (GamePlayer p in player.GetPlayersInRadius(WorldMgr.SAY_DISTANCE))
            {
                if (p != null && p != player)
                    p.Out.SendMessage(player.Name + " is bleeding from the shark's bite !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
            }

            HammerheadController parent = null;
            switch (shark)
            {
                case HammerheadShark hammerhead:
                    parent = hammerhead.Parent;
                    break;
                case AzureShark azure:
                    parent = azure.Parent;
                    break;
            }
            parent?.FrenzyOn(player);

            new ECSGameTimer(player, new ECSGameTimer.ECSTimerCallback(BleedTick), BleedTickInterval);
        }

        //Applies one bleed damage tick - the timer is owned by the victim so it survives the death of the biting shark
        public static int BleedTick(ECSGameTimer timer)
        {
            GamePlayer player = timer.Owner as GamePlayer;
            if (player == null || player.ObjectState != GameObject.eObjectState.Active || !player.IsAlive)
            {
                ClearBleed(player);
                return 0;
            }

            GameObject source = player.TempProperties.GetProperty<GameObject>(BleedSourceProperty, null);
            int ticksLeft = player.TempProperties.GetProperty<int>(BleedTicksProperty, 0);

            if (ticksLeft <= 0)
            {
                ClearBleed(player);
                return 0;
            }

            ticksLeft--;
            player.TempProperties.SetProperty(BleedTicksProperty, ticksLeft);

            player.Out.SendMessage("You are bleeding !", eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);
            player.TakeDamage(source ?? (GameObject)player, eDamageType.Body, BleedDamagePerTick, 0);

            if (ticksLeft <= 0)
            {
                ClearBleed(player);
                player.Out.SendMessage("The bleeding stops.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return 0;
            }

            return BleedTickInterval;
        }

        public static void ClearBleed(GamePlayer player)
        {
            if (player == null)
                return;
            player.TempProperties.SetProperty(BleedingProperty, false);
            player.TempProperties.SetProperty(BleedTicksProperty, 0);
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
            Rubis = false;
            base.StartRespawn();
        }
        public override bool AddToWorld()
        {
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(RubyGlowTick), 3 * 1000);
            return base.AddToWorld();
        }
        public int RubyGlowTick(ECSGameTimer timer)
        {
            if (!IsAlive)
                return 0;
            if (Rubis)
            {
                foreach (GamePlayer player in GetPlayersInRadius(1500))
                {
                    player.Out.SendSpellEffectAnimation(this, this, 10621, 0, false, 1);
                }
            }
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(RubyGlowTick), 3 * 1000);
            return 0;
        }

        //Bleed attack / feeding frenzy
        public override void OnAttackEnemy(AttackData ad)
        {
            base.OnAttackEnemy(ad);

            if (Util.Chance(HammerheadController.BleedChance))
                HammerheadController.TryApplyBleed(this, ad);
        }

        public override void Die(GameObject killer)
        {
            if (Rubis == true)
            {
                if (Parent != null)
                    Parent.HandleRubySharkDeath(killer, this);
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
        public bool Rubis = false;

        //Override
        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
        }
        public override bool AddToWorld()
        {
            //Glow timer when carrying the ruby
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(RubyGlowTick), 3 * 1000);

            //AutoDepopTimer
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(Depop), (2 * 60 * 1000));

            return base.AddToWorld();
        }
        public int RubyGlowTick(ECSGameTimer timer)
        {
            if (!IsAlive)
                return 0;
            if (Rubis)
            {
                foreach (GamePlayer player in GetPlayersInRadius(1500))
                {
                    player.Out.SendSpellEffectAnimation(this, this, 10621, 0, false, 1);
                }
            }
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(RubyGlowTick), 3 * 1000);
            return 0;
        }

        //Bleed attack / feeding frenzy
        public override void OnAttackEnemy(AttackData ad)
        {
            base.OnAttackEnemy(ad);

            if (Util.Chance(HammerheadController.BleedChance))
                HammerheadController.TryApplyBleed(this, ad);
        }
        public override void Die(GameObject killer)
        {
            if (Rubis == true)
            {
                if (Parent != null)
                    Parent.HandleRubySharkDeath(killer, this);
            }
            base.Die(killer);
            this.Delete();
        }

        //Timer AutoDepop
        public int Depop(ECSGameTimer timer)
        {
            if (this.IsAlive == true)
            {
                if (Rubis == true && Parent != null)
                    Parent.EndEncounter();
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