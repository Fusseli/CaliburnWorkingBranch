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

        //Stelles Array
        public int[,] StelleArray = {
			{32498,34703,16343},
			{30203,35242,16390},
			{32878,33978,16163},
			{31297,34771,16114},
        };

        //Npc List
        public List<EffectsNPC> StelleList = new List<EffectsNPC>(); //Stelle List

        //Overrides
        public override void SaveIntoDatabase() //Not Save In Database
        {
        }
        public override void StartRespawn() //Before Start Respawn Timer
        {
            //Change Respawn Time
            this.RespawnInterval = Util.Random(MinRespawn, MaxRespawn) * 60 * 1000;

            base.StartRespawn();
        }
        protected override int RespawnTimerCallback(ECSGameTimer respawnTimer) //Respawn Timer CallBack
        {

            //Spawn Stelles
            for (int i = 0; i < 4; i++)
            {
                SpawnStelle(StelleArray[i, 0], StelleArray[i, 1], StelleArray[i, 2]);
            }

            //Log
            if (this.CurrentRegionID == albregion) log.Warn("Master Level - 1.10 - Now Available.");
            if (this.CurrentRegionID == midregion) log.Warn("Master Level - 1.10 - Now Available.");
            if (this.CurrentRegionID == hibregion) log.Warn("Master Level - 1.10 - Now Available.");

            return base.RespawnTimerCallback(respawnTimer);
        }
        public override bool AddToWorld() //AddToWorld
        {
            //Spawn Stelles
            for (int i = 0; i < 4; i++)
            {
                SpawnStelle(StelleArray[i, 0], StelleArray[i, 1], StelleArray[i, 2]);
            }

            //Log
            if (this.CurrentRegionID == albregion) log.Warn("Master Level - 1.10 - ALB Now Available.");
            if (this.CurrentRegionID == midregion) log.Warn("Master Level - 1.10 - MID Now Available.");
            if (this.CurrentRegionID == hibregion) log.Warn("Master Level - 1.10 - HIB Now Available.");

            return base.AddToWorld();
        }
        public override void Die(GameObject killer) //Die
        {

            //Unload Stelle
            foreach (EffectsNPC Npc in StelleList)
            {
                Npc.Health = 0;
                Npc.Delete();
            }
            StelleList.Clear();

            //Base Die
            base.Die(killer);

            //LOG
            if (this.CurrentRegionID == albregion) log.Warn("Master Level - 1.10 - ALB Cetus Die.");
            if (this.CurrentRegionID == midregion) log.Warn("Master Level - 1.10 - MID Cetus Die.");
            if (this.CurrentRegionID == hibregion) log.Warn("Master Level - 1.10 - HIB Cetus Die.");

            //Credit
            MLCreditHelper.CreditML((byte)1, (byte)10, killer, true, false, (byte)MinimumLevel);

        }

        //Spawns
        public void SpawnStelle(int X, int Y, int Z) //Spawn a Stelle
        {
            EffectsNPC StelleNPC = new EffectsNPC();
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
            StelleNPC.Effect_Enable = true;
            StelleNPC.Effect_CastTimeSec = 1;
            StelleNPC.Effect_DelayMs = 1000;
            StelleNPC.Effect_ID = 805;
            StelleList.Add(StelleNPC);
            StelleNPC.AddToWorld();
        }

        //STATIC - Load Event - STATIC
        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Warn("Master Level - 1.10 - Initializing Event...");
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
            CetusNpc.X = 31310;
            CetusNpc.Y = 33833;
            CetusNpc.Z = 16429;
            CetusNpc.Heading = 3473;
            CetusNpc.RoamingRange = 0;
            CetusNpc.CurrentSpeed = 0;
            CetusNpc.MaxSpeedBase = 100;
            CetusNpc.RespawnInterval = 10 * 60 * 1000;
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

}

namespace DOL.GS.Atlantis
{

    //CetusBrain
    public class CetusBrain : StandardMobBrain
    {	
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        //ConfigValues
        public bool debug = false;
        public int LifeToBeginTP = 50; //Cetus begin TP after his life is less than LifeToBeginTP
        public int MinimumRubisAsked = 1;
        public int MaximumRubisAsked = 3;

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
        public override void Notify(DOL.Events.DOLEvent e, object sender, EventArgs args)
        {
            base.Notify(e, sender, args);
        }

        //Spell Timers

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