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
    public class GemOfLostMemoriesController : GameNPC
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static int AlbRegionID = 73;
        public static int MidRegionID = 30;
        public static int HibRegionID = 130;

        public static bool Albion = false;
        public static bool Midgard = false;
        public static bool Hibernia = true;

        public static int MinimumLevel = 46;
        public static int MinimumRespawn = 30;
        public static int MaximumRespawn = 90;

        public static int[,] SmallCuicuiArray = {
			{266830,558268,9490},
			{265833,557973,8411},
			{267871,557968,8307},
			{265659,558019,8716},
			{266072,558659,8644},
			{268720,557754,8059},
			{265847,557540,8305},
			{267137,557656,8120},
			{267017,557998,9613},
			{266477,558292,8344},
			{267129,557106,8589},
			{266890,558745,9014},
			{267685,556910,8280},
			{267197,559843,8315},
			{266285,558917,9391},
			{266504,559398,8285},
			{267820,559284,9326},
			{267748,558969,9326},
		};

        public static List<SmallCuiCui> SmallCuicuiList = new List<SmallCuiCui>();
        NightTerror CurrentNightTerror;
        public string state = "none";

		[ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Warn("Artifact - GOLM - Initializing Event ...");
            if (Albion == true)
            {
                SpawnGemOfLostMemoriesController(AlbRegionID);
                log.Warn("Artifact - GOLM - Controller ALB added.");
            }
            if (Midgard == true)
            {
                SpawnGemOfLostMemoriesController(MidRegionID);
                log.Warn("Artifact - GOLM - Controller MID added.");
            }
            if (Hibernia == true)
            {
                SpawnGemOfLostMemoriesController(HibRegionID);
                log.Warn("Artifact - GOLM - Controller HIB added.");
            }
            log.Warn("Artifact - GOLM - Event Initialized !");
        }

        public override bool AddToWorld()
        {
            if (this.CurrentRegionID == AlbRegionID)
            {
                SpawnNightTerror(AlbRegionID);
                log.Warn("Artifact - GOLM - Night Terror ALB pop!");
                state = "pop";
            }
            if (this.CurrentRegionID == MidRegionID)
            {
                SpawnNightTerror(MidRegionID);
                log.Warn("Artifact - GOLM - Night Terror MID pop!");
                state = "pop";
            }
            if (this.CurrentRegionID == HibRegionID)
            {
                SpawnNightTerror(HibRegionID);
                EncounterMgr.BroadcastMsg(CurrentNightTerror, "Darkness has once again fallen upon the land!",
                    WorldMgr.VISIBILITY_DISTANCE, eChatType.CT_Important, true);

                log.Warn("Artifact - GOLM - Night Terror HIB pop!");
                state = "pop";
            }

            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(CheckTime), 10 * 1000);

            return base.AddToWorld();
        }

        public int CheckTime(ECSGameTimer timer)
        {
            uint cTime = WorldMgr.GetCurrentGameTime();
            uint hour = cTime / 1000 / 60 / 60;
            uint minute = cTime / 1000 / 60 % 60;
            uint seconds = cTime / 1000 % 60;

            if (((hour >= 18) || (hour < 4)) && ((state == "none") || (state == "depop")) && (CurrentNightTerror.Health > 0))
            {
                log.Warn("Artifact - GOLM - Night Terror HIB enabled!");
                CurrentNightTerror.Model = 1194;
                CurrentNightTerror.Flags = 0;
                state = "pop";
                foreach (SmallCuiCui TheCuiCui in SmallCuicuiList)
                {
                    TheCuiCui.Health = 0;
                    TheCuiCui.Delete();
                }
                SmallCuicuiList.Clear();
            }
            else if (((hour < 18) && (hour >= 4)) && (state == "pop") && (CurrentNightTerror.Health > 0))
            {
                CurrentNightTerror.Model = 665;
                CurrentNightTerror.Flags |= eFlags.PEACE;
                CurrentNightTerror.Flags |= eFlags.CANTTARGET;
                CurrentNightTerror.StopAttack();
                if (this.CurrentRegionID == AlbRegionID) log.Warn("Artifact - GOLM - Night Terror ALB disabled!");
                if (this.CurrentRegionID == MidRegionID) log.Warn("Artifact - GOLM - Night Terror MID disabled!");
                if (this.CurrentRegionID == HibRegionID) log.Warn("Artifact - GOLM - Night Terror HIB disabled!");
                for (int i = 0; i < 18; i++)
                {
                    SpawnSmallCuiCui(SmallCuicuiArray[i, 0], SmallCuicuiArray[i, 1], SmallCuicuiArray[i, 2]);
                }
                state = "depop";
            }

            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(CheckTime), 30 * 1000);

            return 0;
        }

        public void SpawnNightTerror(int region)
        {
            NightTerror NightTerrorNPC = new NightTerror();
            NightTerrorNPC.Name = "Night Terror";
            NightTerrorNPC.GuildName = "";
            NightTerrorNPC.Model = 1194;
            NightTerrorNPC.Realm = eRealm.None;
            NightTerrorNPC.CurrentRegionID = (ushort)region;
            NightTerrorNPC.Size = 50;
            NightTerrorNPC.Level = 45;
            NightTerrorNPC.X = 267263;
            NightTerrorNPC.Y = 558078;
            NightTerrorNPC.Z = 8120;
            NightTerrorNPC.Heading = 3031;
            NightTerrorNPC.RoamingRange = 200;
            NightTerrorNPC.CurrentSpeed = 0;
            NightTerrorNPC.MaxSpeedBase = 170;
            NightTerrorNPC.RespawnInterval = 60 * 1000;
            NightTerrorNPC.BodyType = 0;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 500;
            NightTerrorNPC.SetOwnBrain(brain);
            NightTerrorNPC.AutoSetStats();
            CurrentNightTerror = NightTerrorNPC;
            NightTerrorNPC.AddToWorld();
        }
        public void SpawnSmallCuiCui(int X, int Y, int Z)
        {
            SmallCuiCui Attacker = new SmallCuiCui();
            Attacker.Model = 991;
            Attacker.Size = (byte)Util.Random(25, 50); ;
            Attacker.Level = (byte)Util.Random(35, 45);
            Attacker.Name = "Odysse Harpy";
            Attacker.CurrentRegionID = this.CurrentRegionID;
            Attacker.Heading = 1690;
            Attacker.Realm = 0;
            Attacker.CurrentSpeed = 0;
            Attacker.MaxSpeedBase = 191;
            Attacker.GuildName = "";
            Attacker.X = X;
            Attacker.Y = Y;
            Attacker.Z = Z;
            Attacker.RoamingRange = 200;
            Attacker.Flags |= eFlags.FLYING;
            Attacker.RespawnInterval = 10 * 60 * 1000;
            Attacker.BodyType = 0;

            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 200;
            Attacker.SetOwnBrain(brain);

            SmallCuicuiList.Add(Attacker);
            Attacker.AddToWorld();

            return;
        }

        public static void SpawnGemOfLostMemoriesController(int region)
        {
            GemOfLostMemoriesController SpawnGemOfLostMemoriesControllerNpc = new GemOfLostMemoriesController();
            SpawnGemOfLostMemoriesControllerNpc.Name = "GemOfLostMemories - Controller";
            SpawnGemOfLostMemoriesControllerNpc.GuildName = "ToaManager";
            SpawnGemOfLostMemoriesControllerNpc.Realm = eRealm.None;
            SpawnGemOfLostMemoriesControllerNpc.Model = 665;
            SpawnGemOfLostMemoriesControllerNpc.CurrentRegionID = (ushort)region;
            SpawnGemOfLostMemoriesControllerNpc.Size = 50;
            SpawnGemOfLostMemoriesControllerNpc.Level = 100;
            SpawnGemOfLostMemoriesControllerNpc.X = 267015;
            SpawnGemOfLostMemoriesControllerNpc.Y = 558124;
            SpawnGemOfLostMemoriesControllerNpc.Z = 9345;
            SpawnGemOfLostMemoriesControllerNpc.Heading = 3076;
            SpawnGemOfLostMemoriesControllerNpc.RoamingRange = 0;
            SpawnGemOfLostMemoriesControllerNpc.CurrentSpeed = 0;
            SpawnGemOfLostMemoriesControllerNpc.MaxSpeedBase = 191;
            SpawnGemOfLostMemoriesControllerNpc.Flags |= eFlags.PEACE;
            SpawnGemOfLostMemoriesControllerNpc.Flags |= eFlags.CANTTARGET;
            SpawnGemOfLostMemoriesControllerNpc.RespawnInterval = 10 * 60 * 1000;
            SpawnGemOfLostMemoriesControllerNpc.AddToWorld();
        }
    };

    public class NightTerror : GameNPC
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public override void SaveIntoDatabase()
        {
        }
        public override bool AddToWorld()
        {
            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {
            SpawnNightTerrorx2();
            base.Die(killer);
        }
        public override void StartRespawn()
        {
            this.RespawnInterval = Util.Random(GemOfLostMemoriesController.MinimumRespawn, GemOfLostMemoriesController.MaximumRespawn) * 60 * 1000;
            base.StartRespawn();
        }

        public void SpawnNightTerrorx2()
        {
            NightTerrorX2 NT1 = new NightTerrorX2();
            NT1.Name = "Small Night Terror";
            NT1.GuildName = "";
            NT1.Model = 1194;
            NT1.Realm = eRealm.None;
            NT1.CurrentRegionID = this.CurrentRegionID;
            NT1.Size = 35;
            NT1.Level = 35;
            NT1.X = this.X;
            NT1.Y = this.Y;
            NT1.Z = this.Z;
            NT1.Heading = this.Heading;
            NT1.RoamingRange = 100;
            NT1.CurrentSpeed = 0;
            NT1.MaxSpeedBase = 170;
            NT1.BodyType = 0;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 200;
            NT1.SetOwnBrain(brain);
            NT1.AutoSetStats();
            NT1.attackComponent.AddAttacker(this.TargetObject as GameLiving);

            NightTerrorX2 NT2 = new NightTerrorX2();
            NT2.Name = "Small Night Terror";
            NT2.GuildName = "";
            NT2.Model = 1194;
            NT2.Realm = eRealm.None;
            NT2.CurrentRegionID = this.CurrentRegionID;
            NT2.Size = 35;
            NT2.Level = 35;
            NT2.X = this.X - 25;
            NT2.Y = this.Y - 25;
            NT2.Z = this.Z;
            NT2.Heading = this.Heading;
            NT2.RoamingRange = 100;
            NT2.CurrentSpeed = 0;
            NT2.MaxSpeedBase = 170;
            NT2.BodyType = 0;
            StandardMobBrain brain2 = new StandardMobBrain();
            brain2.AggroLevel = 100;
            brain2.AggroRange = 200;
            NT2.SetOwnBrain(brain2);
            NT2.AutoSetStats();
            NT2.attackComponent.AddAttacker(this.TargetObject as GameLiving);

            NT1.Brother = NT2;
            NT2.Brother = NT1;
            NT1.AddToWorld();
            NT2.AddToWorld();
        }
    }

    public class NightTerrorX2 : GameNPC
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public NightTerrorX2 Brother;
        public NightTerrorX4 Children1;
        public NightTerrorX4 Children2;

        public override bool AddToWorld()
        {
            return base.AddToWorld();
        }
        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
        }
        public override void Die(GameObject killer)
        {
            SpawnNightTerrorx4();
            base.Die(killer);
        }

        public void SpawnNightTerrorx4()
        {
            NightTerrorX4 NT1 = new NightTerrorX4();
            NT1.Name = "Little Night Terror";
            NT1.GuildName = "";
            NT1.Model = 1194;
            NT1.Realm = eRealm.None;
            NT1.CurrentRegionID = this.CurrentRegionID;
            NT1.Size = 20;
            NT1.Level = 35;
            NT1.X = this.X;
            NT1.Y = this.Y;
            NT1.Z = this.Z;
            NT1.Heading = this.Heading;
            NT1.RoamingRange = 100;
            NT1.CurrentSpeed = 0;
            NT1.MaxSpeedBase = 170;
            NT1.BodyType = 0;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 200;
            NT1.SetOwnBrain(brain);
            NT1.AutoSetStats();
            NT1.attackComponent.AddAttacker(this.TargetObject as GameLiving);
            Children1 = NT1;
            NT1.Parent = this;

            NightTerrorX4 NT2 = new NightTerrorX4();
            NT2.Name = "Little Night Terror";
            NT2.GuildName = "";
            NT2.Model = 1194;
            NT2.Realm = eRealm.None;
            NT2.CurrentRegionID = this.CurrentRegionID;
            NT2.Size = 20;
            NT2.Level = 35;
            NT2.X = this.X - 25;
            NT2.Y = this.Y - 25;
            NT2.Z = this.Z;
            NT2.Heading = this.Heading;
            NT2.RoamingRange = 100;
            NT2.CurrentSpeed = 0;
            NT2.MaxSpeedBase = 170;
            NT2.BodyType = 0;
            StandardMobBrain brain2 = new StandardMobBrain();
            brain2.AggroLevel = 100;
            brain2.AggroRange = 200;
            NT2.SetOwnBrain(brain2);
            NT2.AutoSetStats();
            NT2.attackComponent.AddAttacker(this.TargetObject as GameLiving);
            Children2 = NT2;
            NT2.Parent = this;

            NT1.Brother = NT2;
            NT2.Brother = NT1;
            NT1.AddToWorld();
            NT2.AddToWorld();
        }
    }

    public class NightTerrorX4 : GameNPC
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = true;
        public NightTerrorX2 Parent;
        public NightTerrorX4 Brother;

        public override bool AddToWorld()
        {
            return base.AddToWorld();
        }
        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
        }
        public override void Die(GameObject killer)
        {
            if (Brother.IsAlive == false)
            {
                if (Parent.Brother.IsAlive == false)
                {
                    if (Parent.Brother.Children1.IsAlive == false && Parent.Brother.Children2.IsAlive == false)
                    {
                        log.Warn("Artifact - GOLM - " + killer.Name + " - Group Grant");
                    }
                }
            }
            base.Die(killer);
        }
    }

    public class SmallCuiCui : GameNPC
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            base.StartRespawn();
        }
    }
}
