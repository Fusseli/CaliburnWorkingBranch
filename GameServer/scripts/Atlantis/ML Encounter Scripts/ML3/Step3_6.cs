/* 
 * 
 * 
 * 
 * 
 *      Barracuda : ML3.6         *
                                   *
                                   *
               SecT                *
                                   *
                                   *
                                   */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DOL;
using DOL.AI.Brain;
using DOL.Database;
using DOL.Events;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using log4net;

namespace DOL.GS.Atlantis
{

    //Barracuda
    public class Barracuda : GameNPC
    {

        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static bool debug = true;

        //Niveau Minimal
        public static int MinimumLevel = 40;

        //Barracuda Respawn Time ( in minutes )
        public static int Barracuda_MinRespawn = 45;
        public static int Barracuda_MaxRespawn = 60;

        //YBarracuda Respawn Time ( in minutes )
        public static int YBarracuda_MinRespawn = 7;
        public static int YBarracuda_MaxRespawn = 15;

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
        public List<YBarracuda> YBarracudaList = new List<YBarracuda>();

        //YBarracuda's Array
        #region YBarracudaArray

        public int[,] YBarracudaArray = 
        {
			{28158,31709,14931}, //1__YBarracuda_
			{28119,31817,14792}, //2__YBarracuda_
			{28222,32146,14792}, //3__YBarracuda_
			{28100,31827,14982}, //4__YBarracuda_
			{28118,31551,15079}, //5__YBarracuda_
            {27728,31257,14933}, //6__YBarracuda_
            {27367,31628,14837}, //7__YBarracuda_
            {27237,32410,14963}, //8__YBarracuda_
            {27025,30544,14888}, //9__YBarracuda_
            {27482,30538,14833}, //10_YBarracuda_
            {27958,31051,14886}, //11_YBarracuda_
            {27464,31101,14757}, //12_YBarracuda_
            {27104,31103,15112}, //13_YBarracuda_
            {26572,30511,15005}, //14_YBarracuda_
            {26793,31544,14830}, //15_YBarracuda_
            {26246,32112,14992}, //16_YBarracuda_
            {26681,32491,14826}, //17_YBarracuda_
            {26932,32028,15118}, //18_YBarracuda_
            {27407,31447,14880}, //19_YBarracuda_
            {27378,43543,14981}, //20_YBarracuda_ 

		};
        #endregion

        //Override
        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
        }
        public override void Die(GameObject killer)
        {
            MLCreditHelper.CreditML((byte)3, (byte)6, killer, true, false, (byte)Barracuda.MinimumLevel);
            base.Die(killer);
        }
        public override void StartAttack(GameObject target)
        {
            foreach (YBarracuda barracuda in YBarracudaList)
            {
                foreach (GamePlayer player in target.GetPlayersInRadius(800))
                {
                    barracuda.attackComponent.AddAttacker(player);
                }
                barracuda.StartAttack(target);
            }
            base.StartAttack(target);
        }
        public override bool AddToWorld()
        {
            if (Initalized == false)
            {
                for (int i = 0; i < 20; i++)
                {
                    SpawnYBarracuda(YBarracudaArray[i, 0], YBarracudaArray[i, 1], YBarracudaArray[i, 2]);
                }
                if (this.CurrentRegionID == albregion)
                {
                    log.Warn("Master Level - 3.6 - Barracudas Added !");
                }
                if (this.CurrentRegionID == midregion)
                {
                    log.Warn("Master Level - 3.6 - Barracudas Added !");
                }
                if (this.CurrentRegionID == hibregion)
                {
                    log.Warn("Master Level - 3.6 - Barracudas Added !");
                }
                Initalized = true;
            }
            return base.AddToWorld();
        }

        //Spawn Barracudas
        public void SpawnYBarracuda(int X, int Y, int Z)
        {
            YBarracuda YBarr = new YBarracuda();
            YBarr.Name = "Barracuda";
            YBarr.GuildName = "";
            YBarr.Model = 33735;
            YBarr.Realm = 0;
            YBarr.CurrentRegionID = this.CurrentRegionID;
            YBarr.Size = 45;
            YBarr.Level = (byte)Util.Random(55, 60);
            YBarr.Strength = 500;
            YBarr.Constitution = 400;
            YBarr.X = X;
            YBarr.Y = Y;
            YBarr.Z = Z;
            YBarr.Heading = (ushort)Util.Random(200, 3000);
            YBarr.RoamingRange = 200;
            YBarr.CurrentSpeed = 0;
            YBarr.MaxSpeedBase = 191;
            YBarr.RespawnInterval = 10 * 60 * 1000;
            YBarr.BodyType = 0;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 400;
            YBarr.SetOwnBrain(brain);
            if (Barracuda.debug == true) YBarr.debug = true;
            YBarr.parent = this;
            YBarracudaList.Add(YBarr);
            YBarr.AddToWorld();
        }
        public static void SpawnBarracuda(int Reg)
        {
            Barracuda Barr = new Barracuda();
            Barr.Name = "Barracuda NAMED";
            Barr.GuildName = "";
            Barr.Model = 33735;
            Barr.Realm = 0;
            Barr.CurrentRegionID = (ushort)Reg;
            Barr.Size = 45;
            Barr.Level = 75;
            Barr.Strength = 700;
            Barr.Constitution = 600;
            Barr.X = 26818;
            Barr.Y = 31218;
            Barr.Z = 14935;
            Barr.Heading = (ushort)Util.Random(200, 3000);
            Barr.RoamingRange = -1;
            Barr.CurrentSpeed = 0;
            Barr.MaxSpeedBase = 191;
            Barr.RespawnInterval = 10 * 60 * 1000;
            Barr.BodyType = 0;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 400;
            Barr.SetOwnBrain(brain);
            Barr.AddToWorld();
        }

        //Load Event
        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Warn("Master Level - 3.6 - Initializing Event...");
            if (Albion == true)
            {
                SpawnBarracuda(albregion);
                log.Warn("Master Level - 3.6 - Barracuda ALB added.");
            }
            if (Midgard == true)
            {
                SpawnBarracuda(midregion);
                log.Warn("Master Level - 3.6 - Barracuda MID added.");
            }
            if (Hibernia == true)
            {
                SpawnBarracuda(hibregion);
                log.Warn("Master Level - 3.6 - Barracuda HIB added.");
            }
            log.Warn("Master Level - 3.6 - Event Initialized !");
        }

    }

    //Usual YBarracuda
    public class YBarracuda : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;

        //Parent Creator
        public Barracuda parent;

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            this.RespawnInterval = (Util.Random(Barracuda.YBarracuda_MinRespawn, Barracuda.YBarracuda_MaxRespawn) * 60 * 1000);
            base.StartRespawn();
        }
        public override bool AddToWorld()
        {
            return base.AddToWorld();
        }

    }

}
