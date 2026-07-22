//---------------------------------------------------------
//---------------ML2.1 - La Mort Venue des Ombres ---------
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

namespace DOL.GS.Atlantis
{

    //SnakeMen21Controller
    public class SnakeMen21Controller : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static bool debug = true;

        //Minimum Level
        public static int MinimumLevel = 40;

        //Realm Regions ( Sobekite )
        public static int albregion = 79;
        public static int midregion = 36;
        public static int hibregion = 136;

        //Realm Available for this Step
        public static bool Albion = false;
        public static bool Midgard = false;
        public static bool Hibernia = true;

        //Snake Men
        public SnakeMan21 Ka_a;
        public SnakeMan21 Ata;
        public SnakeMan21 Sinuhe;
        public SnakeMan21 Bassem;

        //LittleStatue Array
        public int[,] SpawnLocs = {
			{33103,24635,16218,148},
			{33304,25043,16073,575},
			{33655,25382,16202,929},
			{33479,25768,16073,1031},
			{33336,25411,15977,910},
            {32968,24968,15977,164},
            {32561,24976,15977,3732},
            {32223,25071,16073,3443},
            {31923,25407,16176,3116},
            {32233,25440,15976,3073},
            {32068,25751,16073,3080},
            {32432,24647,16214,3881},
		};

        //Overrides
        public override void SaveIntoDatabase() //Disable SaveInDB
        {
        }
        public override void StartRespawn() //Respawn
        {
            base.StartRespawn();
        }
        public override bool AddToWorld() //AddToWorld
        {
            Ka_a = SpawnNamedSnake("Ka'a", SpawnLocs[0, 0], SpawnLocs[0, 1], SpawnLocs[0, 2], (ushort)SpawnLocs[0, 3]);
            Ata = SpawnNamedSnake("Ata", SpawnLocs[1, 0], SpawnLocs[1, 1], SpawnLocs[1, 2], (ushort)SpawnLocs[1, 3]);
            Sinuhe = SpawnNamedSnake("Sinuhe", SpawnLocs[2, 0], SpawnLocs[2, 1], SpawnLocs[2, 2], (ushort)SpawnLocs[2, 3]);
            Bassem = SpawnNamedSnake("Bassem", SpawnLocs[3, 0], SpawnLocs[3, 1], SpawnLocs[3, 2], (ushort)SpawnLocs[3, 3]);
            return base.AddToWorld();
        }
        public override bool Interact(GamePlayer player) //Interact
        {
            TurnTo(player, 100);
            this.TargetObject = player;

            if (!base.Interact(player)) return false;
            return true;
        }
        public override bool WhisperReceive(GameLiving source, string str) //WhisperReceive
        {
            GamePlayer player = source as GamePlayer;
            if (player == null) return false;
            return true;
        }

        //Public
        public SnakeMan21 SpawnNamedSnake(string name, int X, int Y, int Z, ushort heading)
        {
            SnakeMan21 NPC = new SnakeMan21();
            NPC.Name = name;
            NPC.GuildName = "";
            NPC.Model = 33735;
            NPC.Realm = 0;
            NPC.CurrentRegionID = this.CurrentRegionID;
            NPC.Size = 45;
            NPC.Level = 65;
            NPC.Strength = 500;
            NPC.Constitution = 400;
            NPC.X = X;
            NPC.Y = Y;
            NPC.Z = Z;
            NPC.Heading = heading;
            NPC.RoamingRange = 200;
            NPC.CurrentSpeed = 0;
            NPC.MaxSpeedBase = 191;
            NPC.RespawnInterval = 10 * 60 * 1000;
            NPC.BodyType = 0;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 400;
            NPC.SetOwnBrain(brain);
            if (SnakeMen21Controller.debug == true) NPC.debug = true;
            NPC.Parent = this;
            NPC.AddToWorld();
            return NPC;
        }

        //Public STATIC
        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Warn("Master Level - 2.1 - Initializing Event ...");
            if (Albion == true)
            {
                SpawnSnakeMenController(albregion);
                log.Warn("Master Level - 2.1 - Controller ALB added.");
            }
            if (Midgard == true)
            {
                SpawnSnakeMenController(midregion);
                log.Warn("Master Level - 2.1 - Controller MID added.");
            }
            if (Hibernia == true)
            {
                SpawnSnakeMenController(hibregion);
                log.Warn("Master Level - 2.1 - Controller HIB added.");
            }
            log.Warn("Master Level - 2.1 - Event Initialized !");
        }
        public static void SpawnSnakeMenController(int region)
        {
            SnakeMen21Controller ControllerNPC = new SnakeMen21Controller();
            ControllerNPC.Name = "2.1 - Controller";
            ControllerNPC.GuildName = "ToaManager";
            ControllerNPC.Realm = eRealm.None;
            ControllerNPC.Model = 665;
            ControllerNPC.CurrentRegionID = (ushort)region;
            ControllerNPC.Size = 50;
            ControllerNPC.Level = 100;
            ControllerNPC.X = 21269;
            ControllerNPC.Y = 23198;
            ControllerNPC.Z = 3088;
            ControllerNPC.Heading = 2369;
            ControllerNPC.RoamingRange = 0;
            ControllerNPC.CurrentSpeed = 0;
            ControllerNPC.MaxSpeedBase = 191;
            ControllerNPC.Flags |= eFlags.PEACE;
            ControllerNPC.Flags |= eFlags.CANTTARGET;
            ControllerNPC.RespawnInterval = 10 * 60 * 1000;
            ControllerNPC.AddToWorld();
        }

    }

    //SnakeMan21
    public class SnakeMan21 : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;

        //Variables
        public SnakeMen21Controller Parent;

        //Overrides
        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {

            //Set Invisible
            this.Model = 665;

            //Random SpawnLoc


            //Random RespawnTime

            base.StartRespawn();
        }
        public override bool AddToWorld()
        {
            this.Model = 665;
            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {
            base.Die(killer);
        }

        //Public
        public void SetVisible()
        {
            this.Model = 665;
            this.Flags = 0;
        }
        public void SetInVisible()
        {
            this.Model = 665;
            this.Flags = eFlags.CANTTARGET;
        }
    }

}
