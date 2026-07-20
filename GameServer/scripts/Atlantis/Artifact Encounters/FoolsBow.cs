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
    public class FoolBowController : GameNPC
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static int AlbRegionID = 73;
        public static int MidRegionID = 30;
        public static int HibRegionID = 130;

        public static bool Albion = false;
        public static bool Midgard = false;
        public static bool Hibernia = true;

        public static FoolBowController FoolBowControllerAlbion;
        public static FoolBowController FoolBowControllerMidgard;
        public static FoolBowController FoolBowControllerHibernia;

        public static int MinimumLevel = 46;
        public static int MinRespawn = 20;
        public static int MaxRespawn = 30;
        public static int PopDuration = 20;

        public List<CasualArgos> ArgosList = new List<CasualArgos>();
        public List<CuiCui> CuicuiList = new List<CuiCui>();
        public SoundAreaNPC CuicuiSound;
        public bool InProgress = false;

        public static int[,] Argos = {
			{358519,626956,6157},
			{356301,623962,3669},
			{357057,622408,4328},
			{355762,625931,6755},
			{358553,622505,4491},
			{357353,623137,4272},
			{358963,623318,4161},
			{355812,624586,5877},
			{357920,621974,3886},
			{356530,625340,5696},
			{359334,624257,6048},
			{356702,627028,6626},
			{356274,623026,4003},
			{358304,625300,3888},
			{359418,621882,6025},
			{356846,625404,3446},
			{357354,625592,3067},
			{358180,625053,1793},
			{358847,623837,2230},
			{359092,622342,3489},
			{358721,621910,3886},
			{355561,622485,5796},
			{355807,623700,5281},
			{356743,620447,6375},
			{356541,623252,5436},
			{358101,622064,5755},
			{357626,622868,5653},
			{358435,625387,5229},
			{357591,625805,5229},
			{357902,625183,4965},
            {358405,624285,5135},
			{358164,623843,5611},
			{358544,622558,5611},
			{357591,622573,5145},
			{356870,622336,4987},
			{356107,623032,4868},
			{356172,624451,4743},
			{356496,626096,4600},
			{358009,626024,4115},
		};

        public static int[,] CuiCui = {
			{358652,622187,10189},
			{358950,625482,9115},
			{357347,624680,10914},
			{359510,623781,10686},
			{359363,625204,9115},
			{359585,623670,10686},
			{359487,622858,9115},
			{359213,625181,11652},
			{359156,622768,9115},
			{357502,622013,9115},
            {359511,625302,10397},
			{359383,622802,9115},
			{359667,623749,10686},
			{359671,623272,10189},
			{357562,622025,9115},
			{357700,624110,10695},
			{357641,622055,9115},
			{358857,626279,10390},
			{357652,622130,9115},
			{357709,622090,9115},
            {359280,625206,9089},
			{359308,625834,11515},
			{359406,625634,11594},
			{357853,622601,9391},
			{358859,626234,10391},
			{359345,622741,9115},
			{358884,626172,10392},
			{358432,625784,10189},
			{357732,622151,9163},
			{357530,622136,9115},
            {357012,623915,10189},
			{359283,625171,9088},
			{358352,624045,10686},
			{359506,625393,10397},
			{359258,622827,9115},
			{357896,625933,8747},
			{357209,625539,8694},
			{359340,625229,9115},
			{358782,624520,11437},
		};

		[ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Warn("Artifact - Fool's Bow - Initializing Event ...");
            if (Albion == true)
            {
                SpawnController(AlbRegionID);
                log.Warn("Artifact - Fool's Bow - Controller ALB added.");
            }
            if (Midgard == true)
            {
                SpawnController(MidRegionID);
                log.Warn("Artifact - Fool's Bow - Controller MID added.");
            }
            if (Hibernia == true)
            {
                SpawnController(HibRegionID);
                log.Warn("Artifact - Fool's Bow - Controller HIB added.");
            }
            log.Warn("Artifact - Fool's Bow - Event Initialized !");
        }

        public override void SaveIntoDatabase()
        {
        }
        public override bool AddToWorld()
        {
            for (int i = 0; i < 39; i++)
            {
                SpawnCasualArgos(Argos[i, 0], Argos[i, 1], Argos[i, 2]);
            }
            if (this.CurrentRegionID == AlbRegionID) log.Warn("Artifact - Fool's Bow - Argos ALB added.");
            if (this.CurrentRegionID == MidRegionID) log.Warn("Artifact - Fool's Bow - Argos MID added.");
            if (this.CurrentRegionID == HibRegionID) log.Warn("Artifact - Fool's Bow - Argos HIB added.");

            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(Pop), Util.Random(MinRespawn, MaxRespawn) * 60 * 1000);

            return base.AddToWorld();
        }

        public int Pop(ECSGameTimer timer)
        {
            if (InProgress == false)
            {
                SpawnArtifactEvent();
                new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(DePop), PopDuration * 60 * 1000);
                return 0;
            }
            return 0;
        }
        public int DePop(ECSGameTimer timer)
        {
            if (InProgress == true)
            {
                DeSpawnArtifactEvent();
                new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(Pop), Util.Random(MinRespawn, MaxRespawn) * 60 * 1000);
            }
            return 0;
        }

        public void SpawnArtifactEvent()
        {
            InProgress = true;

            for (int i = 0; i < 39; i++)
            {
                SpawnCuiCui(CuiCui[i, 0], CuiCui[i, 1], CuiCui[i, 2]);
            }
            if (this.CurrentRegionID == AlbRegionID) log.Warn("Artifact - Fool's Bow - ALB Available.");
            if (this.CurrentRegionID == MidRegionID) log.Warn("Artifact - Fool's Bow - MID Available.");
            if (this.CurrentRegionID == HibRegionID) log.Warn("Artifact - Fool's Bow - HIB Available.");

            SpawnCuiCuiSoundNPC(true);

            foreach (GamePlayer say in GetPlayersInRadius(3000))
            {
                say.Out.SendMessage("A strange object has just fallen from the sky!", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
            }
        }
        public void DeSpawnArtifactEvent()
        {
            InProgress = false;

            foreach (CuiCui TheCuiCui in CuicuiList)
            {
                TheCuiCui.Health = 0;
                TheCuiCui.Delete();
            }
            CuicuiList.Clear();
            if (this.CurrentRegionID == AlbRegionID)
                log.Warn("Artifact - Fool's Bow - ALB Not Available.");
            else if (this.CurrentRegionID == MidRegionID)
                log.Warn("Artifact - Fool's Bow - MID Not Available.");
            else if (this.CurrentRegionID == HibRegionID)
                log.Warn("Artifact - Fool's Bow - HIB Not Available.");

            SpawnCuiCuiSoundNPC(false);
        }

        public void SpawnCasualArgos(int X, int Y, int Z)
        {
            CasualArgos Attacker = new CasualArgos();
            Attacker.Model = 33738;
            Attacker.Size = 50;
            Attacker.Level = (byte)Util.Random(45, 55);
            Attacker.Name = "Argo of pit";
            Attacker.CurrentRegionID = this.CurrentRegionID;
            Attacker.Heading = (ushort)Util.Random(200, 3000);
            Attacker.Realm = 0;
            Attacker.CurrentSpeed = 0;
            Attacker.MaxSpeedBase = 170;
            Attacker.GuildName = "";
            Attacker.X = X;
            Attacker.Y = Y;
            Attacker.Z = Z;
            Attacker.RoamingRange = 250;
            Attacker.RespawnInterval = 10 * 60 * 1000;
            Attacker.BodyType = 0;

            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 300;
            Attacker.SetOwnBrain(brain);

            ArgosList.Add(Attacker);
            Attacker.AddToWorld();

            return;
        }
        public void SpawnCuiCui(int X, int Y, int Z)
        {
            CuiCui Attacker = new CuiCui();
            Attacker.Model = 992;
            Attacker.Size = (byte)Util.Random(25, 70); ;
            Attacker.Level = (byte)Util.Random(50, 55);
            Attacker.Name = "Harpy";
            Attacker.CurrentRegionID = this.CurrentRegionID;
            Attacker.Heading = 1690;
            Attacker.Realm = 0;
            Attacker.CurrentSpeed = 0;
            Attacker.MaxSpeedBase = 300;
            Attacker.GuildName = "";
            Attacker.X = X;
            Attacker.Y = Y;
            Attacker.Z = Z;
            Attacker.RoamingRange = 200;
            Attacker.Flags |= eFlags.FLYING;
            Attacker.RespawnInterval = 3 * 60 * 1000;
            Attacker.BodyType = 0;

            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 0;
            Attacker.SetOwnBrain(brain);

            CuicuiList.Add(Attacker);
            Attacker.AddToWorld();

            return;
        }
        public void SpawnCuiCuiSoundNPC(bool SpawnOrDespawn)
        {
            if(SpawnOrDespawn == true)
            {
                SoundAreaNPC Sound = new SoundAreaNPC();
                Sound.Name = "Harpys sound";
                Sound.Enable = true;
                Sound.AreaRadius = WorldMgr.VISIBILITY_DISTANCE;
                Sound.DirectSound = true;
                Sound.ChancePlayPercent = 100;
                Sound.ChancePlayDelaySecond = 10;
                Sound.Sound1ID = 1014;
                Sound.Sound2ID = 462;
                Sound.Flags |= eFlags.FLYING;
                Sound.X = 357680;
                Sound.Y = 624346;
                Sound.Z = 8534;
                Sound.Heading = 566;
                Sound.CurrentRegionID = this.CurrentRegionID;
                Sound.Realm = 0;
                CuicuiSound = Sound;
                Sound.AddToWorld();
            }
            else
            {
                if (CuicuiSound != null)
                {
                    CuicuiSound.Enable = false;
                    CuicuiSound.Health = 0;
                    CuicuiSound.Delete();
                    CuicuiSound = null;
                }
            }
        }

        public static void SpawnController(int region)
        {
            FoolBowController Statue = new FoolBowController();
            Statue.Name = "Fool's Bow - Controller";
            Statue.GuildName = "ToaManager";
            Statue.Model = 665;
            Statue.Realm = eRealm.None;
            Statue.CurrentRegionID = (ushort)region;
            Statue.Size = 30;
            Statue.Level = 71;
            Statue.X = 363722;
            Statue.Y = 629157;
            Statue.Z = 8554;
            Statue.Heading = 632;
            Statue.RoamingRange = 0;
            Statue.CurrentSpeed = 0;
            Statue.MaxSpeedBase = 191;
            Statue.Flags |= eFlags.PEACE;
            Statue.Flags |= eFlags.CANTTARGET;
            Statue.RespawnInterval = 10 * 60 * 1000;
            Statue.AddToWorld();
        }
    }

    public class CasualArgos : GameNPC
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            base.StartRespawn();
        }
        public override void Die(GameObject killer)
        {
            base.Die(killer);
        }
    }

    public class CuiCui : GameNPC
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
