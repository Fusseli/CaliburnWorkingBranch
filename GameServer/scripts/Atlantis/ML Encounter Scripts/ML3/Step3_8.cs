/* 
 * 
 * 
 * 
 * Glowing Sting Ray : ML3.8 
                                   *
                                   *
               SecT                *
                                   *
                                   *
 Hibernos Reviewed this            */


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
using DOL;
using DOL.Database;

namespace DOL.GS.Atlantis
{

    //GlowingStingRay
    public class GlowingStingRay : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static bool debug = true;

        //Niveau Minimal
        public static int MinimumLevel = 40;

        //GlowingStingRay Respawn Time ( in minutes )
        public static int GlowingStingRay_MinRespawn = 45;
        public static int GlowingStingRay_MaxRespawn = 60;

        //StingRay Respawn Time ( in minutes )
        public static int StingRay_MinRespawn = 7;
        public static int StingRay_MaxRespawn = 15;

        //Regions
        public static int albregion = 80;
        public static int midregion = 37;
        public static int hibregion = 137;

        //Realm Available for this Step
        public static bool Albion = false;
        public static bool Midgard = false;
        public static bool Hibernia = true;

        //Class Initalized
        public bool Initalized = false;

        //NpcList
        public List<StingRay> StingRayList = new List<StingRay>();


        #region StingRayArray
        //StingRay's Array
        public int[,] StingRayArray = 
        {
			{35073,43970,14779}, //1___StingRay
			{34874,43880,14779}, //2___StingRay
			{34624,44104,14933}, //3___StingRay
			{34899,44614,14933}, //4___StingRay
			{34750,44730,14749}, //5___StingRay
            {34280,44442,14837}, //6___StingRay
            {34084,44086,14837}, //7___StingRay
            {34290,43913,15058}, //8___StingRay
            {33502,44060,15248}, //9___StingRay
            {33722,44578,15025}, //10__StingRay
            {34421,44518,14737}, //11__StingRay
            {34852,44428,15026}, //12__StingRay
            {34701,45136,14924}, //13__StingRay
            {34468,45250,14856}, //14__StingRay
            {33861,45427,14868}, //15__StingRay
            {33733,45398,15205}, //16__StingRay
            {33353,45377,14953}, //17__StingRay
            {33306,44383,14800}, //18__StingRay
            {33368,43975,15082}, //19__StingRay
            {33808,43543,14981}, //20__StingRay
            {34060,43668,15008}, //21__StingRay
		};
        #endregion


        //Override
        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            //Change Respawn Interval
            this.RespawnInterval = (Util.Random(GlowingStingRay_MinRespawn, GlowingStingRay_MaxRespawn) * 60 * 1000);
            
            //Change SpawnPoint
            int RandLottery;
            RandLottery = Util.Random(1, 6);
            if (RandLottery == 1)
            {
                this.SpawnPoint.X = 34525;
                this.SpawnPoint.Y = 44812;
                this.SpawnPoint.Z = 15117;
            }
            if (RandLottery == 2)
            {
                this.SpawnPoint.X = 35561;
                this.SpawnPoint.Y = 45548;
                this.SpawnPoint.Z = 14886;
            }
            if (RandLottery == 3)
            {
                this.SpawnPoint.X = (int)34949;
                this.SpawnPoint.Y = (int)45014;
                this.SpawnPoint.Z = (int)15022;
            }
            if (RandLottery == 4)
            {
                this.SpawnPoint.X = 34254;
                this.SpawnPoint.Y = 44892;
                this.SpawnPoint.Z = 14794;
            }
            if (RandLottery == 5)
            {
                this.SpawnPoint.X = 34165;
                this.SpawnPoint.Y = 44329;
                this.SpawnPoint.Z = 14833;
            }
            if (RandLottery == 6)
            {
                this.SpawnPoint.X = 33204;
                this.SpawnPoint.Y = 45230;
                this.SpawnPoint.Z = 14796;
            }
            base.StartRespawn();
        }
        public override void Die(GameObject killer)
        {
            MLCreditHelper.CreditML((byte)3, (byte)8, killer, true, false, (byte)GlowingStingRay.MinimumLevel);
            base.Die(killer);
        }
        public override bool AddToWorld()
        {
            if (Initalized == false)
            {
                for (int i = 0; i < 21; i++)
                {
                    SpawnStingRay(StingRayArray[i, 0], StingRayArray[i, 1], StingRayArray[i, 2]);
                }
                if (this.CurrentRegionID == albregion)
                {
                    log.Warn("Master Level - 3.8 - Sting Rays Added !");
                }
                if (this.CurrentRegionID == midregion)
                {
                    log.Warn("Master Level - 3.8 - Sting Rays Added !");
                }
                if (this.CurrentRegionID == hibregion)
                {
                    log.Warn("Master Level - 3.8 - Sting Rays Added !");
                }
                Initalized = true;
            }
            return base.AddToWorld();
        }

        //Spawn StingsRays
        public void SpawnStingRay(int X, int Y, int Z)
        {
            StingRay StingR = new StingRay();
            StingR.Name = "Sting ray";
            StingR.GuildName = "";
            StingR.Model = 33746;
            StingR.Realm = 0;
            StingR.CurrentRegionID = this.CurrentRegionID;
            StingR.Size = 45;
            StingR.Model = 975;
            StingR.Level = 57;
            StingR.Strength = 500;
            StingR.Constitution = 400;
            StingR.X = X;
            StingR.Y = Y;
            StingR.Z = Z;
            StingR.Heading = (ushort)Util.Random(200, 3000);
            StingR.RoamingRange = 200;
            StingR.CurrentSpeed = 0;
            StingR.MaxSpeedBase = 191;
            StingR.RespawnInterval = 10 * 60 * 1000;
            StingR.BodyType = 0;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 600;
            StingR.SetOwnBrain(brain);
            if (GlowingStingRay.debug == true) StingR.debug = true;
            StingR.parent = this;
            StingRayList.Add(StingR);
            StingR.AddToWorld();
        }
        public static void SpawnGlowingStingRay(int R)
        {
            GlowingStingRay GStingR = new GlowingStingRay();
            GStingR.Name = "Sting ray";
            GStingR.GuildName = "";
            GStingR.Level = 72;
            GStingR.Realm = 0;
            GStingR.Model = 975;
            GStingR.CurrentRegionID = (ushort)R;
            GStingR.RespawnInterval = 10 * 60 * 1000;
            GStingR.BodyType = 0;
            GStingR.Size = 45;
            GStingR.Strength = 560;
            GStingR.Constitution = 450;
            GStingR.RoamingRange = -1;
            GStingR.CurrentSpeed = 0;
            GStingR.MaxSpeedBase = 191;
            int RandLottery;
            RandLottery = Util.Random(1, 6);
            if (RandLottery == 1)
            {
                GStingR.X = 34525;
                GStingR.Y = 44812;
                GStingR.Z = 15117;
            }
            if (RandLottery == 2)
            {
                GStingR.X = 35561;
                GStingR.Y = 45548;
                GStingR.Z = 14886;
            }
            if (RandLottery == 3)
            {
                GStingR.X = (int)34949;
                GStingR.Y = (int)45014;
                GStingR.Z = (int)15022;
            }
            if (RandLottery == 4)
            {
                GStingR.X = 34254;
                GStingR.Y = 44892;
                GStingR.Z = 14794;
            }
            if (RandLottery == 5)
            {
                GStingR.X = 34165;
                GStingR.Y = 44329;
                GStingR.Z = 14833;
            }
            if (RandLottery == 6)
            {
                GStingR.X = 33204;
                GStingR.Y = 45230;
                GStingR.Z = 14796;
            }
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 1000;
            GStingR.SetOwnBrain(brain);
            GStingR.AddToWorld();
        }

        //Load Event
        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Warn("Master Level - 3.8 - Initializing Event...");
            if (Albion == true)
            {
                SpawnGlowingStingRay(albregion);
                log.Warn("Master Level - 3.8 - GlowingStingRay ALB added.");
            }
            if (Midgard == true)
            {
                SpawnGlowingStingRay(midregion);
                log.Warn("Master Level - 3.8 - GlowingStingRay MID added.");
            }
            if (Hibernia == true)
            {
                SpawnGlowingStingRay(hibregion);
                log.Warn("Master Level - 3.8 - GlowingStingRay HIB added.");
            }
            log.Warn("Master Level - 3.8 - Event Initialized !");
        }

    }

    //StingRay
    public class StingRay : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;

        //Parent Creator
        public GlowingStingRay parent;

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            this.RespawnInterval = (Util.Random(GlowingStingRay.StingRay_MinRespawn, GlowingStingRay.StingRay_MaxRespawn) * 60 * 1000);
            base.StartRespawn();
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

}
