//---------------------------------------------------------
//---------------------ML1.10 - Cetus ---------------------
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

    //Cetus Class
    public class Cetus : GameNPC
    {

        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static bool debug = true;

        //Minimum Level
        public static int MinimumLevel = 40;

        //Realm Regions
        public static int albregion = 78;
        public static int midregion = 35;
        public static int hibregion = 135;

        //Realm Available for this Step
        public static bool Albion = false;
        public static bool Midgard = false;
        public static bool Hibernia = true;

        //Minimum Respawn Time - Maximum Respawn Time ( in minutes )
        public static int MinRespawn = 12;
        public static int MaxRespawn = 15;

        //Cetus Max Speed ( restored after a retreat )
        public const int CetusMaxSpeed = 100;

        //Positions ( X , Y , Z , Heading )
        public static int SpawnX = 31916;            //Spawn / resume position
        public static int SpawnY = 33595;
        public static int SpawnZ = 16342;
        public static ushort SpawnHeading = 4048;

        public static int GateX = 31967;             //Retreat position behind his gate
        public static int GateY = 31079;
        public static int GateZ = 16149;
        public static ushort GateHeading = 4080;

        public static int EntranceX = 31667;         //Cave entrance - Desmona's Crown port destination
        public static int EntranceY = 38358;
        public static int EntranceZ = 16220;
        public static ushort EntranceHeading = 1944;

        //Item Templates used in this encounter
        public const string RubyItemId = "ToaManager_Many_Facetted_Ruby";
        public const string MirrorItemId = "ToaManager_Rassa's_Mirror";
        public const string RingItemId = "ToaManager_Kirkleis'_Ring";
        public const string CrownItemId = "ToaManager_Desmona_Crown";

        //Sphere visual on the Stelles ( spells.csv spell id - "sphere shield"; known-good fallback: 11523 )
        public const ushort SphereVisualSpellId = 5934;

        //Retreat system
        public const int RetreatMinInterval = 90 * 1000;   //Minimum time fighting before a retreat attempt
        public const int RetreatMaxInterval = 150 * 1000;  //Maximum time fighting before a retreat attempt
        public const int RetreatPrepTime = 10 * 1000;      //Warning phase before the retreat happens
        public const int RetreatHealDuration = 20 * 1000;  //Time spent healing behind the gate
        public const int RetreatHealPercentPerSecond = 5;  //Health healed per second behind the gate

        //Breath attack
        public const int BreathChance = 1;                 //Chance per hit taken that the breath fires ( percent )
        public const int BreathDamage = 600;               //Damage of an unblocked breath
        public const int BreathRadius = 500;               //Range of the breath and of the mirror check
        public const long BreathBlockWindow = 10 * 1000;   //Grace window after a mirror blocked the breath
        public const int BreathVisualSpellId = 251;        //Frost Blast visual for the breath

        //Mindslayer orbs ( Rassa's Mirror )
        public const int OrbCount = 3;
        public const int OrbDuration = 30 * 1000;
        public const int OrbTickInterval = 3 * 1000;
        public const int OrbDamagePerTick = 120;
        public const int OrbVisualSpellId = 759;           //Mind Flay visual for the orbs

        //Magical spheres ( Stelles )
        public const int SphereRadius = 250;               //Range in which a player standing inside uses his relics automatically

        //Treasure room gate behind his cave - sealed while Cetus is alive
        public const int TreasureDoorID = 135000101;

        //Ancient Key + Ancient Chests
        public const string KeyItemId = "ToaManager_Ancient_Key";
        public const int KeyModel = 498;                   //Placeholder - adjust if it looks wrong
        public const int ChestItemCount = 6;               //Items per opened chest
        public const byte ChestItemLevel = 51;             //Item level - L51 = 16.5 dps weapons / higher AF armors / better bonus caps
        public const int ChestTier150Chance = 6;           //Chance per item for a 150 utility trophy ( max once per chest )
        public const int ChestTier125Chance = 20;          //Chance per item for 125-138 utility
        public const int ChestTier100MinUtility = 85;
        public const int ChestTier100MaxUtility = 95;
        public const int ChestTier125MinUtility = 96;
        public const int ChestTier125MaxUtility = 110;
        public const int ChestTier150Utility = 115;           //Trophy - max once per chest

        //State
        public int RubyShields = 0;                        //Proactive retreat blocks set via a sphere
        public long BreathBlockedUntil = 0;                //GameLoop.GameLoopTime until which the breath can not fire
        public bool Retreating = false;                    //True while Cetus is retreating / healing behind his gate
        public long OrbsActiveUntil = 0;                   //GameLoop.GameLoopTime until which mindslayer orbs are active
        protected bool m_treasureDoorLocked = false;
        protected long m_nextRetreatTick = 0;
        protected bool m_retreatPending = false;
        protected int m_retreatHealSecondsLeft = 0;
        protected bool m_monitorStarted = false;

        //Stelles Array
        public int[,] StelleArray = {
			{32498,34703,16343},
			{30203,35242,16390},
			{32878,33978,16163},
			{31297,34771,16114},
        };

        //Npc List
        public List<CetusStelle> StelleList = new List<CetusStelle>();

        //Chests Array ( X , Y , Z , Heading )
        public int[,] ChestArray = {
			{31359,30640,15936,3584},
			{32592,30673,15936,682},
			{32244,30417,15936,352},
			{31788,30351,15936,4016},
        };

        //Chest List
        public List<CetusTreasureChest> ChestList = new List<CetusTreasureChest>();

        //Overrides
        public override void SaveIntoDatabase() //Not Saved In Database
        {
        }
        public override void StartRespawn() //Before Start Respawn Timer
        {
            this.RespawnInterval = Util.Random(MinRespawn, MaxRespawn) * 60 * 1000;
            base.StartRespawn();
        }
        protected override int RespawnTimerCallback(ECSGameTimer respawnTimer) //Respawn Timer CallBack
        {
            SpawnStelles();

            if (debug == true) log.Warn("Master Level - 1.10 - Now Available.");

            return base.RespawnTimerCallback(respawnTimer);
        }
        public override bool AddToWorld() //AddToWorld
        {
            SpawnStelles();
            SpawnChests();

            //Seal the treasure room gate
            LockTreasureDoor();

            //Start the combat monitor once
            if (!m_monitorStarted)
            {
                m_monitorStarted = true;
                new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(CombatMonitorTick), 5 * 1000);
            }

            if (debug == true) log.Warn("Master Level - 1.10 - HIB Now Available.");

            return base.AddToWorld();
        }
        public override void Die(GameObject killer) //Die
        {
            UnloadStelles();

            //Open the treasure room gate
            UnlockTreasureDoor();

            //Loot - Ancient Key ( only one )
            MLCreditHelper.GiveItem(killer, this, KeyItemId, 1, 1);
            Broadcast("Cetus dropped an Ancient Key !");

            //Reset the encounter state
            Retreating = false;
            m_retreatPending = false;
            m_nextRetreatTick = 0;
            BreathBlockedUntil = 0;
            RubyShields = 0;
            OrbsActiveUntil = 0;
            MaxSpeedBase = CetusMaxSpeed;
            Flags &= ~eFlags.CANTTARGET;

            base.Die(killer);

            if (debug == true) log.Warn("Master Level - 1.10 - HIB Cetus Die.");

            MLCreditHelper.CreditML((byte)1, (byte)10, killer, true, false, (byte)MinimumLevel);
        }

        //Stelles
        public void SpawnStelles()
        {
            UnloadStelles();
            for (int i = 0; i < 4; i++)
            {
                SpawnStelle(StelleArray[i, 0], StelleArray[i, 1], StelleArray[i, 2]);
            }
        }
        public void UnloadStelles()
        {
            foreach (CetusStelle Npc in StelleList)
            {
                Npc.Health = 0;
                Npc.Delete();
            }
            StelleList.Clear();
        }
        public void SpawnStelle(int X, int Y, int Z)
        {
            CetusStelle StelleNPC = new CetusStelle();
            StelleNPC.Name = "Stelle";
            StelleNPC.GuildName = "";
            StelleNPC.Model = 665;
            StelleNPC.Realm = 0;
            StelleNPC.CurrentRegionID = this.CurrentRegionID;
            StelleNPC.Size = 200;
            StelleNPC.Level = 70;
            StelleNPC.X = X;
            StelleNPC.Y = Y;
            StelleNPC.Z = Z;
            StelleNPC.Heading = (ushort)Util.Random(200, 3000);
            StelleNPC.RoamingRange = 0;
            StelleNPC.CurrentSpeed = 0;
            StelleNPC.MaxSpeedBase = 0;
            StelleNPC.AutoSetStats();
            StelleNPC.RespawnInterval = 15 * 60 * 1000;
            StelleNPC.BodyType = 0;
            StelleNPC.Flags |= eFlags.PEACE;
            StelleNPC.Flags |= eFlags.CANTTARGET;
            StelleNPC.ParentCetus = this;
            StelleList.Add(StelleNPC);
            StelleNPC.AddToWorld();
        }

        //Ancient Chests
        public void SpawnChests()
        {
            UnloadChests();
            for (int i = 0; i < 4; i++)
            {
                SpawnChest(ChestArray[i, 0], ChestArray[i, 1], ChestArray[i, 2], ChestArray[i, 3]);
            }
        }
        public void UnloadChests()
        {
            foreach (CetusTreasureChest Npc in ChestList)
            {
                if (Npc.ObjectState == eObjectState.Active)
                {
                    Npc.Health = 0;
                    Npc.Delete();
                }
            }
            ChestList.Clear();
        }
        public void SpawnChest(int X, int Y, int Z, int Heading)
        {
            CetusTreasureChest Chest = new CetusTreasureChest();
            Chest.Name = "Ancient Chest";
            Chest.GuildName = "";
            Chest.Model = 1596;
            Chest.Realm = eRealm.None;
            Chest.CurrentRegionID = this.CurrentRegionID;
            Chest.Level = 1;
            Chest.X = X;
            Chest.Y = Y;
            Chest.Z = Z;
            Chest.Heading = (ushort)Heading;
            Chest.RoamingRange = 0;
            Chest.CurrentSpeed = 0;
            Chest.MaxSpeedBase = 0;
            Chest.RespawnInterval = 0; //Opened chests come back when Cetus respawns
            Chest.BodyType = 0;
            Chest.Flags |= eFlags.PEACE;
            Chest.Flags |= eFlags.CANTTARGET;
            Chest.ParentController = this;
            ChestList.Add(Chest);
            Chest.AddToWorld();
        }

        //Combat Monitor - schedules the retreat attempts while fighting
        public int CombatMonitorTick(ECSGameTimer timer)
        {
            if (!IsAlive || ObjectState != GameObject.eObjectState.Active)
                return 5 * 1000;

            //Keep trying to seal the treasure door until the doors are loaded ( they load after scripts on boot )
            if (!m_treasureDoorLocked)
                LockTreasureDoor();

            if (!IsAttacking)
            {
                //Out of combat - reset the schedule
                m_nextRetreatTick = 0;
                return 5 * 1000;
            }

            if (m_retreatPending)
                return 5 * 1000;

            if (m_nextRetreatTick == 0)
            {
                //Schedule the next retreat attempt
                m_nextRetreatTick = GameLoop.GameLoopTime + Util.Random(RetreatMinInterval, RetreatMaxInterval);
                return 5 * 1000;
            }

            if (GameLoop.GameLoopTime >= m_nextRetreatTick)
            {
                m_nextRetreatTick = 0;
                BeginRetreatSequence();
            }

            return 5 * 1000;
        }

        //Retreat sequence
        public void BeginRetreatSequence()
        {
            m_retreatPending = true;
            Broadcast("Cetus bellows and prepares to retreat behind his gate to heal !");
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(RetreatPrepCallback), RetreatPrepTime);
        }

        public int RetreatPrepCallback(ECSGameTimer timer)
        {
            if (!IsAlive || ObjectState != GameObject.eObjectState.Active)
            {
                m_retreatPending = false;
                return 0;
            }

            //A proactive ruby shield blocks the retreat
            if (RubyShields > 0)
            {
                RubyShields--;
                m_retreatPending = false;
                Broadcast("A Many Facetted Ruby flares up - Cetus cannot retreat !");
                return 0;
            }

            //Otherwise a ruby is taken from any player nearby
            if (TryConsumeItemFromPlayers(RubyItemId))
            {
                m_retreatPending = false;
                Broadcast("A Many Facetted Ruby flares up - Cetus cannot retreat !");
                return 0;
            }

            TeleportToGate();
            return 0;
        }

        public void TeleportToGate()
        {
            StopAttack();
            if (Brain is StandardMobBrain brain)
                brain.ClearAggroList();

            //Freeze him behind the gate so his brain does not path back through the door
            //and make him untargetable while he is out of reach
            MaxSpeedBase = 0;
            Flags |= eFlags.CANTTARGET;
            MoveInRegion(CurrentRegionID, GateX, GateY, GateZ, GateHeading, true);
            Broadcast("Cetus retreats behind his gate to heal !");

            m_retreatHealSecondsLeft = RetreatHealDuration / 1000;
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(RetreatHealTick), 1000);
        }

        public int RetreatHealTick(ECSGameTimer timer)
        {
            if (!IsAlive || ObjectState != GameObject.eObjectState.Active)
            {
                Retreating = false;
                return 0;
            }

            m_retreatHealSecondsLeft--;

            int healAmount = MaxHealth * RetreatHealPercentPerSecond / 100;
            if (Health + healAmount > MaxHealth)
                Health = MaxHealth;
            else
                Health = Health + healAmount;

            if (m_retreatHealSecondsLeft <= 0)
            {
                ReturnFromGate();
                return 0;
            }

            return 1000;
        }

        public void ReturnFromGate()
        {
            MaxSpeedBase = CetusMaxSpeed;
            Flags &= ~eFlags.CANTTARGET;
            MoveInRegion(CurrentRegionID, SpawnX, SpawnY, SpawnZ, SpawnHeading, true);
            Retreating = false;
            Broadcast("Cetus returns to the fight !");
        }

        //Unattackable while retreating behind his gate
        public override void TakeDamage(GameObject source, eDamageType damageType, int damageAmount, int criticalAmount)
        {
            if (Retreating)
                return;

            base.TakeDamage(source, damageType, damageAmount, criticalAmount);
        }

        //Breath attack - small chance per hit taken, blocked by Rassa's Mirror holders nearby
        public override void OnAttackedByEnemy(AttackData ad)
        {
            base.OnAttackedByEnemy(ad);

            if (!IsAlive || ObjectState != GameObject.eObjectState.Active || ad == null || ad.Attacker == null)
                return;

            //Grace window after a mirror block
            if (GameLoop.GameLoopTime < BreathBlockedUntil)
                return;

            if (!Util.Chance(BreathChance))
                return;

            //A mirror holder among the attackers prevents the breath
            foreach (GamePlayer player in GetPlayersInRadius(BreathRadius))
            {
                if (player == null || !player.IsAlive)
                    continue;

                if (player.Inventory.GetFirstItemByID(MirrorItemId, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack) != null)
                {
                    BreathBlockedUntil = GameLoop.GameLoopTime + BreathBlockWindow;
                    Broadcast("Rassa's Mirror flares up and prevents Cetus from using his breath attack !");
                    return;
                }
            }

            //Unblocked breath hits everyone nearby
            Broadcast("Cetus unleashes a devastating breath attack !");
            foreach (GamePlayer victim in GetPlayersInRadius(BreathRadius))
            {
                if (victim == null || !victim.IsAlive)
                    continue;

                victim.Out.SendSpellEffectAnimation(this, victim, BreathVisualSpellId, 0, false, 1);
                victim.Out.SendMessage("Cetus' breath hits you for " + BreathDamage + " damage !", eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);
                foreach (GamePlayer onlooker in victim.GetPlayersInRadius(1500))
                {
                    if (onlooker != null && onlooker != victim)
                        onlooker.Out.SendMessage("Cetus' breath hits " + victim.Name + " for " + BreathDamage + " damage !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                }
                victim.TakeDamage(this, eDamageType.Cold, BreathDamage, 0);
            }
        }

        //Mindslayer orbs - spawned by using Rassa's Mirror at a sphere
        public void SpawnMindslayerOrbs(GamePlayer user)
        {
            OrbsActiveUntil = GameLoop.GameLoopTime + OrbDuration;
            Broadcast(user.Name + " releases mindslayer orbs from Rassa's Mirror - they assault Cetus !");

            for (int i = 0; i < OrbCount; i++)
            {
                MindslayerOrb orb = new MindslayerOrb();
                orb.Name = "Mindslayer Orb";
                orb.GuildName = "";
                orb.Model = 665;
                orb.Realm = eRealm.None;
                orb.CurrentRegionID = this.CurrentRegionID;
                orb.Size = 50;
                orb.Level = 50;
                orb.X = X + Util.Random(-200, 200);
                orb.Y = Y + Util.Random(-200, 200);
                orb.Z = Z;
                orb.Heading = (ushort)Util.Random(200, 3000);
                orb.RoamingRange = 0;
                orb.CurrentSpeed = 0;
                orb.MaxSpeedBase = 0;
                orb.AutoSetStats();
                orb.BodyType = 0;
                orb.Flags |= eFlags.PEACE;
                orb.Flags |= eFlags.CANTTARGET;
                orb.Victim = this;
                orb.AddToWorld();
                orb.StartAssault();
            }
        }

        //Kirkleis' Ring - clears Cetus' aggro
        public void DisengageFromFight()
        {
            StopAttack();
            if (Brain is StandardMobBrain brain)
                brain.ClearAggroList();

            Broadcast("Kirkleis' Ring shatters - Cetus loses interest in his attackers !");
        }

        //Consumes one item of the given template id from any living player nearby
        public bool TryConsumeItemFromPlayers(string itemId)
        {
            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (player == null || !player.IsAlive)
                    continue;

                DbInventoryItem item = player.Inventory.GetFirstItemByID(itemId, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                if (item != null)
                {
                    player.Inventory.RemoveItem(item);
                    player.Out.SendMessage("Your " + item.Name + " crumbles to dust !", eChatType.CT_Important, eChatLoc.CL_ChatWindow);
                    return true;
                }
            }
            return false;
        }

        //Treasure room gate - sealed while Cetus is alive
        protected GameDoor GetTreasureDoor()
        {
            foreach (GameDoorBase doorBase in DoorMgr.getDoorByID(TreasureDoorID))
            {
                if (doorBase is GameDoor gameDoor)
                    return gameDoor;
            }
            return null;
        }

        public void LockTreasureDoor()
        {
            if (m_treasureDoorLocked)
                return;

            GameDoor door = GetTreasureDoor();
            if (door == null)
                return; //Doors may not be loaded yet on boot - the combat monitor retries

            m_treasureDoorLocked = true;
            door.Locked = 1;
            door.Close();
            door.SaveIntoDatabase();
            Broadcast("The great gate seals shut !");

            if (debug == true) log.Warn("Master Level - 1.10 - Treasure door sealed.");
        }

        public void UnlockTreasureDoor()
        {
            m_treasureDoorLocked = false;

            GameDoor door = GetTreasureDoor();
            if (door == null)
            {
                if (debug == true) log.Warn("Master Level - 1.10 - Treasure door " + TreasureDoorID + " not found !");
                return;
            }

            door.Locked = 0;
            door.SaveIntoDatabase();
            door.Open();
            Broadcast("With Cetus defeated, the great gate unlocks !");
        }

        //Broadcasts a message to everyone nearby
        public void Broadcast(string message)
        {
            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (player != null)
                    player.Out.SendMessage(message, eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
            }
        }

        //STATIC - Load Event - STATIC
        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Warn("Master Level - 1.10 - Initializing Event...");

            EnsureAncientKeyTemplate();

            if (Albion == true)
            {
                SpawnCetus(albregion);
                log.Warn("Master Level - 1.10 - Cetus ALB added.");
            }
            if (Midgard == true)
            {
                SpawnCetus(midregion);
                log.Warn("Master Level - 1.10 - Cetus MID added.");
            }
            if (Hibernia == true)
            {
                SpawnCetus(hibregion);
                log.Warn("Master Level - 1.10 - Cetus HIB added.");
            }
            log.Warn("Master Level - 1.10 - Event Initialized !");
        }
        //Creates the Ancient Key template if it does not exist yet
        public static void EnsureAncientKeyTemplate()
        {
            DbItemTemplate keyTest = GameServer.Database.FindObjectByKey<DbItemTemplate>(KeyItemId);
            if (keyTest != null)
                return;

            DbItemTemplate key = new DbItemTemplate();
            key.Id_nb = KeyItemId;
            key.Name = "Ancient Key";
            key.Model = KeyModel;
            key.Level = 0;
            key.Object_Type = 41; //Magical
            key.Item_Type = 0;
            key.Weight = 1;
            key.IsPickable = false;
            key.IsDropable = true;
            key.CanDropAsLoot = false;
            key.IsTradable = true;
            key.MaxCount = 1;
            key.PackSize = 1;
            key.Realm = 0;
            key.Description = "An ancient key that opens one of Cetus' treasure chests.";
            key.PackageID = "Toa_Hib";
            GameServer.Database.AddObject(key);
            log.Warn("Master Level - 1.10 - Ancient Key added !");
        }
        public static void SpawnCetus(int region) //Spawn Cetus
        {
            Cetus CetusNpc = new Cetus();
            CetusNpc.Name = "Cetus";
            CetusNpc.GuildName = "";
            CetusNpc.Model = 973;
            CetusNpc.Size = 100;
            CetusNpc.Realm = 0;
            CetusNpc.CurrentRegionID = (ushort)region;
            CetusNpc.Level = 100;
            CetusNpc.X = SpawnX;
            CetusNpc.Y = SpawnY;
            CetusNpc.Z = SpawnZ;
            CetusNpc.Heading = SpawnHeading;
            CetusNpc.RoamingRange = 0;
            CetusNpc.CurrentSpeed = 0;
            CetusNpc.MaxSpeedBase = CetusMaxSpeed;
            CetusNpc.RespawnInterval = 12 * 60 * 1000;
            CetusNpc.Strength = 620;
            CetusNpc.Constitution = 620;
            CetusNpc.Dexterity = 300;
            CetusNpc.Quickness = 300;
            CetusNpc.Intelligence = 30;
            CetusNpc.Empathy = 30;
            CetusNpc.Piety = 30;
            CetusNpc.Charisma = 30;
            CetusBrain brain = new CetusBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 2500;
            CetusNpc.SetOwnBrain(brain);
            CetusNpc.AddToWorld();
        }

    }

    //Stelle Class - the magical spheres of the cave
    public class CetusStelle : GameNPC
    {

        public Cetus ParentCetus;

        //Starts the sphere proximity check and the pulsing visual
        public override bool AddToWorld()
        {
            bool result = base.AddToWorld();
            if (result)
            {
                new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(SphereTick), 1000);
                new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(SphereVisualTick), 2000);
            }
            return result;
        }

        //Pulsing sphere visual - same mechanism as the ruby glow and Fadrin's barrier
        public int SphereVisualTick(ECSGameTimer timer)
        {
            if (!IsAlive || ObjectState != GameObject.eObjectState.Active)
                return 0;

            foreach (GamePlayer player in GetPlayersInRadius(1500))
            {
                player.Out.SendSpellEffectAnimation(this, this, Cetus.SphereVisualSpellId, 0, false, 1);
            }

            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(SphereVisualTick), 2 * 1000);
            return 0;
        }

        //Players standing inside the sphere use their relics automatically
        public int SphereTick(ECSGameTimer timer)
        {
            Cetus cetus = ParentCetus;
            if (cetus == null || !cetus.IsAlive || cetus.ObjectState != GameObject.eObjectState.Active)
                return 1000;

            foreach (GamePlayer player in GetPlayersInRadius(Cetus.SphereRadius))
            {
                if (player == null || !player.IsAlive)
                    continue;

                TryAutoUseRelic(player, cetus);
            }

            return 1000;
        }

        //Uses the first applicable relic of the player - Desmona's Crown stays click only
        protected void TryAutoUseRelic(GamePlayer player, Cetus cetus)
        {
            //Many Facetted Ruby - only one shield at a time and only while fighting
            if (cetus.IsAttacking && cetus.RubyShields == 0)
            {
                DbInventoryItem ruby = FindItem(player, Cetus.RubyItemId);
                if (ruby != null)
                {
                    RemoveItem(player, ruby);
                    cetus.RubyShields++;
                    player.Out.SendMessage("You release the power of the Many Facetted Ruby - Cetus will not be able to retreat !", eChatType.CT_Important, eChatLoc.CL_ChatWindow);
                    return;
                }
            }

            //Rassa's Mirror - only while Cetus fights, is above half health and no orbs are active
            if (cetus.IsAttacking && cetus.HealthPercent > 50 && GameLoop.GameLoopTime >= cetus.OrbsActiveUntil)
            {
                DbInventoryItem mirror = FindItem(player, Cetus.MirrorItemId);
                if (mirror != null)
                {
                    RemoveItem(player, mirror);
                    cetus.SpawnMindslayerOrbs(player);
                    return;
                }
            }

            //Kirkleis' Ring - only while fighting
            if (cetus.IsAttacking)
            {
                DbInventoryItem ring = FindItem(player, Cetus.RingItemId);
                if (ring != null)
                {
                    RemoveItem(player, ring);
                    cetus.DisengageFromFight();
                    player.Out.SendMessage("The ring shatters in your hands.", eChatType.CT_Important, eChatLoc.CL_ChatWindow);
                    return;
                }
            }
        }

        //Using a relic manually happens by interacting with the sphere while carrying it
        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            TurnTo(player, 1500);

            Cetus cetus = ParentCetus;
            if (cetus == null || !cetus.IsAlive || cetus.ObjectState != GameObject.eObjectState.Active)
            {
                SayTo(player, "The sphere hums quietly... but Cetus is not here.");
                return true;
            }

            //Many Facetted Ruby - shields against the next retreat attempt
            DbInventoryItem ruby = FindItem(player, Cetus.RubyItemId);
            if (ruby != null)
            {
                RemoveItem(player, ruby);
                cetus.RubyShields++;
                SayTo(player, "You release the power of the Many Facetted Ruby - Cetus will not be able to retreat !");
                return true;
            }

            //Rassa's Mirror - mindslayer orbs, only while Cetus is above half health
            DbInventoryItem mirror = FindItem(player, Cetus.MirrorItemId);
            if (mirror != null)
            {
                if (cetus.HealthPercent > 50)
                {
                    RemoveItem(player, mirror);
                    cetus.SpawnMindslayerOrbs(player);
                }
                else
                {
                    SayTo(player, "The mirror stays silent - its power only works while Cetus is still strong.");
                }
                return true;
            }

            //Kirkleis' Ring - clears Cetus' aggro
            DbInventoryItem ring = FindItem(player, Cetus.RingItemId);
            if (ring != null)
            {
                RemoveItem(player, ring);
                cetus.DisengageFromFight();
                SayTo(player, "The ring shatters in your hands.");
                return true;
            }

            //Desmona's Crown - ports the user to the entrance of the cave
            DbInventoryItem crown = FindItem(player, Cetus.CrownItemId);
            if (crown != null)
            {
                RemoveItem(player, crown);
                SayTo(player, "Desmona's Crown teleports you back to the entrance of the cave !");
                player.MoveTo(cetus.CurrentRegionID, Cetus.EntranceX, Cetus.EntranceY, Cetus.EntranceZ, Cetus.EntranceHeading);
                return true;
            }

            SayTo(player, "The magical sphere shimmers around you. [Many Facetted Ruby | Rassa's Mirror | Kirkleis' Ring | Desmona's Crown] can be used here against Cetus.");
            return true;
        }

        //Helpers
        protected DbInventoryItem FindItem(GamePlayer player, string itemId)
        {
            return player.Inventory.GetFirstItemByID(itemId, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
        }
        protected void RemoveItem(GamePlayer player, DbInventoryItem item)
        {
            player.Inventory.RemoveItem(item);
        }

    }

    //Mindslayer Orb Class - attacks Cetus with direct damage ticks
    public class MindslayerOrb : GameNPC
    {

        public Cetus Victim;
        protected int m_ticksLeft = Cetus.OrbDuration / Cetus.OrbTickInterval;

        public void StartAssault()
        {
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(AssaultTick), Cetus.OrbTickInterval);
        }

        public int AssaultTick(ECSGameTimer timer)
        {
            if (!IsAlive || ObjectState != GameObject.eObjectState.Active || Victim == null || !Victim.IsAlive)
            {
                Health = 0;
                Delete();
                return 0;
            }

            foreach (GamePlayer player in Victim.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (player != null)
                    player.Out.SendSpellEffectAnimation(this, Victim, Cetus.OrbVisualSpellId, 0, false, 1);
            }

            Victim.TakeDamage(this, eDamageType.Spirit, Cetus.OrbDamagePerTick, 0);

            m_ticksLeft--;
            if (m_ticksLeft <= 0)
            {
                Health = 0;
                Delete();
                return 0;
            }

            return Cetus.OrbTickInterval;
        }

    }

    //Ancient Chest Class - opened with an Ancient Key, tied to Cetus' respawn cycle
    public class CetusTreasureChest : GameMovingObject
    {

        public Cetus ParentController;

        //Overrides
        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
        }
        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            TurnTo(player, 1500);

            Cetus cetus = ParentController;

            //Key check
            DbInventoryItem key = null;
            if (player != null)
                key = player.Inventory.GetFirstItemByID(Cetus.KeyItemId, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);

            if (key == null)
            {
                SayTo(player, "The ancient chest is locked tight. Perhaps Cetus holds its key...");
                return true;
            }

            player.Inventory.RemoveItem(key);

            if (cetus != null)
                cetus.Broadcast(player.Name + " unlocks an Ancient Chest !");
            player.Out.SendMessage("You unlock the Ancient Chest !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);

            GenerateChestLoot(player);

            this.RemoveFromWorld();
            return true;
        }

        //Generates the chest loot - one possible 150 utility trophy per chest
        protected void GenerateChestLoot(GamePlayer player)
        {
            eRealm realm = player.Realm;
            eCharacterClass charClass = (eCharacterClass)player.CharacterClass.ID;

            bool trophyUsed = false;

            for (int i = 0; i < Cetus.ChestItemCount; i++)
            {
                GeneratedUniqueItem item = null;

                if (!trophyUsed && Util.Chance(Cetus.ChestTier150Chance))
                {
                    item = new GeneratedUniqueItem(realm, charClass, Cetus.ChestItemLevel, Cetus.ChestTier150Utility, Cetus.ChestTier150Utility);
                    trophyUsed = true;
                    player.Out.SendMessage("You found an item of incredible power !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                    foreach (GamePlayer p in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        if (p != null && p != player)
                            p.Out.SendMessage(player.Name + " found an item of incredible power !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                    }
                }
                else if (Util.Chance(Cetus.ChestTier125Chance))
                {
                    item = new GeneratedUniqueItem(realm, charClass, Cetus.ChestItemLevel, Cetus.ChestTier125MinUtility, Cetus.ChestTier125MaxUtility);
                }
                else
                {
                    item = new GeneratedUniqueItem(realm, charClass, Cetus.ChestItemLevel, Cetus.ChestTier100MinUtility, Cetus.ChestTier100MaxUtility);
                }

                item.AllowAdd = true;
                item.IsTradable = true;
                item.Quality = Util.Random(99, 100); //chest rewards are top-quality

                DbInventoryItem invItem = GameInventoryItem.Create<DbItemUnique>(item);
                invItem.IsROG = true;

                if (player.Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, invItem))
                {
                    player.Out.SendMessage("You receive : " + invItem.Name, eChatType.CT_Loot, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    player.Out.SendMessage("Your backpack is full !", eChatType.CT_Important, eChatLoc.CL_ChatWindow);
                    break;
                }
            }
        }

    }

}

namespace DOL.GS.Atlantis
{

    //CetusBrain
    public class CetusBrain : StandardMobBrain
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //ConfigValues
        public bool debug = false;

        //DrainLife Spell
        public bool DrainLifeAvailable = true;
        public int DrainLifePlayerValue = 300; //Drain life value on player
        public int DrainLifeCetusValue = 300; //Drain life value on cetus
        public int DrainLifeMinCoolDown = 30; //Drain Life Minimum Cooldown
        public int DrainLifeMaxCoolDown = 320; //Drain Life Maximum Cooldown

        //Base
        public CetusBrain()
            : base()
        {
        }

        //Overrides
        public override int ThinkInterval //ThinkIntervalValue
        {
            get { return 300; }
        }
        public override void Think() //Think
        {

            //Define Cetus Body
            Cetus CetusBody = Body as Cetus;

            //DrainLife Spell
            #region DrainLife
            bool CastDrain = true;
            if (CetusBody.IsAttacking && CetusBody.Health != 0 && DrainLifeAvailable && CetusBody.TargetObject != null)
            {

                //Save TargetObject
                GameObject ObjectTargeted = CetusBody.TargetObject;

                //Check If Target Is GamePlayer or Pet And return if we can't Drain
                if (ObjectTargeted is GameNPC npcTarget && npcTarget.Brain is IControlledBrain)
                {
                    if (ObjectTargeted.Health == 0) CastDrain = false; //Check If pet is alive
                }
                else if (ObjectTargeted is GamePlayer)
                {
                    if (ObjectTargeted.Health == 0) CastDrain = false; //Check If player is alive
                    if (((GamePlayer)ObjectTargeted).Inventory.GetFirstItemByID("ToaManager_Rassa's_Mirror", eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack) != null)
                    {
                        CastDrain = false; //Check If player own Rassa mirror
                    }
                }

                //Drain
                if (CastDrain == true)
                {
                    //Damages - Logs - Broadcasts
                    if (ObjectTargeted is GamePlayer)
                    {
                        ((GamePlayer)ObjectTargeted).Out.SendMessage("Cetus drained " + DrainLifePlayerValue + " of your life !", eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);
                        ((GamePlayer)ObjectTargeted).TakeDamage(CetusBody, eDamageType.Body, DrainLifePlayerValue, 0);
                        foreach (GamePlayer p in CetusBody.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                        {
                            if (p != null && p != (GamePlayer)ObjectTargeted)
                            {
                                p.Out.SendMessage("Cetus drained " + DrainLifePlayerValue + " of " + ((GamePlayer)ObjectTargeted).Name + " life !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                            }
                        }
                    }
                    else if (ObjectTargeted is GameNPC npcTarget2 && npcTarget2.Brain is IControlledBrain controlledBrain)
                    {
                        npcTarget2.TakeDamage(CetusBody, eDamageType.Body, DrainLifePlayerValue, 0);
                        GamePlayer PetOwner = controlledBrain.GetPlayerOwner();
                        PetOwner.Out.SendMessage("Cetus drained " + DrainLifePlayerValue + " of " + controlledBrain.Body.Name + "'s life !", eChatType.CT_Damaged, eChatLoc.CL_SystemWindow);
                        foreach (GamePlayer p in CetusBody.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                        {
                            if (p != null && p != PetOwner)
                            {
                                p.Out.SendMessage("Cetus drained " + DrainLifePlayerValue + " of " + controlledBrain.Body.Name + " life !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                            }
                        }
                    }

                    //Heal Cetus
                    if ((CetusBody.Health + DrainLifeCetusValue) > CetusBody.MaxHealth)
                    {
                        CetusBody.Health = CetusBody.MaxHealth;
                    }
                    else
                    {
                        CetusBody.Health = CetusBody.Health + DrainLifeCetusValue;
                    }

                    //Set Cooldown oF DrainLife
                    DrainLifeAvailable = false;
                    new ECSGameTimer(CetusBody, new ECSGameTimer.ECSTimerCallback(ResetDrainLifeCD), Util.Random(DrainLifeMinCoolDown, DrainLifeMaxCoolDown) * 1000);
                }

            }
            #endregion DrainLife

            base.Think();
        }

        //ResetCoolDownTimers
        public int ResetDrainLifeCD(ECSGameTimer timer) //Set DrainLife Available
        {
            DrainLifeAvailable = true;
            return 0;
        }

        //Reset Brain
        public void ResetBrain()
        {
            DrainLifeAvailable = true;
        }

    }

}