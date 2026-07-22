/* 
 * 
 * 
 * 
 * 
 *        Sinovia : ML3.9         *
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
using DOL.AI;
using DOL.AI.Brain;
using DOL.Database;
using DOL.Events;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using log4net;

namespace DOL.GS.Atlantis
{

    //Sinovia
    public class Sinovia : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static bool debug = true;

        //Niveau Minimal
        public static int MinimumLevel = 40;

        //Sinovia Respawn Time ( in minutes )
        public static int Sinovia_MinRespawn = 45;
        public static int Sinovia_MaxRespawn = 60;

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


        //override
        public override void EnemyHealed(GameLiving enemy, GameObject healSource, eHealthChangeType changeType, int healAmount)
        {
            base.EnemyHealed(enemy, healSource, changeType, healAmount);
            Brain.Notify(GameLivingEvent.EnemyHealed, this,
                new EnemyHealedEventArgs(enemy, healSource, changeType, healAmount));
        }
        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            this.RespawnInterval = (Util.Random(Sinovia_MinRespawn, Sinovia_MaxRespawn) * 60 * 1000);
            base.StartRespawn();
        }
        public override void Die(GameObject killer)
        {
            base.Die(killer);
        }
        public override bool AddToWorld()
        {
            return base.AddToWorld();
        }

        //Spawn Pieuvre
        public void Pieuvre()
        {
            Pieuvre Padd = new Pieuvre();
            Padd.X = X - 25;
            Padd.Y = Y - 10;
            Padd.Z = Z;
            Padd.CurrentRegionID = this.CurrentRegionID;
            Padd.Heading = Heading;
            Padd.Level = 60;
            Padd.Realm = 0;
            Padd.Strength = 400;
            Padd.Constitution = 450;
            Padd.Name = "Pieuvre";
            Padd.Model = 33738;
            Padd.CurrentSpeed = 0;
            Padd.MaxSpeedBase = 191;
            Padd.GuildName = "";
            Padd.Size = 15;
            Padd.parent = this;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 1000;
            Padd.SetOwnBrain(brain);
            if (debug == true) Padd.debug = true;
            Padd.AddToWorld();
        }

        //Spawn Sinovia
        public static void SpawnSinovia(int reg)
        {
            Sinovia Sin = new Sinovia();
            Sin.Name = "Sinovia";
            Sin.GuildName = "";
            Sin.Level = 75;
            Sin.Realm = 0;
            Sin.Model = 33738;
            Sin.CurrentRegionID = (ushort)reg;
            Sin.RespawnInterval = 10 * 60 * 1000;
            Sin.BodyType = 0;
            Sin.Size = 40;
            Sin.Strength = 750;
            Sin.Constitution = 750;
            Sin.RoamingRange = 0;
            Sin.CurrentSpeed = 0;
            Sin.MaxSpeedBase = 191;
            Sin.X = 58108;
            Sin.Y = 35039;
            Sin.Z = 15003;
            Sin.Heading = 1984;
            SinoviaBrain brain = new SinoviaBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 700;
            Sin.SetOwnBrain(brain);
            Sin.AddToWorld();
        }


        //Load Event
        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Warn("Master Level - 3.9 - Initializing Event...");
            if (Albion == true)
            {
                SpawnSinovia(albregion);
                log.Warn("Master Level - 3.9 - Sinovia ALB added.");
            }
            if (Midgard == true)
            {
                SpawnSinovia(midregion);
                log.Warn("Master Level - 3.9 - Sinovia MID added.");
            }
            if (Hibernia == true)
            {
                SpawnSinovia(hibregion);
                log.Warn("Master Level - 3.9 - Sinovia HIB added.");
            }
            log.Warn("Master Level - 3.9 - Event Initialized !");
        }
    }

    //Pieuvre
    public class Pieuvre : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;

        //Parent Creator
        public Sinovia parent;

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

}

namespace DOL.GS.Atlantis
{

    //Sinovia Brain
    public class SinoviaBrain : StandardMobBrain
    {	/// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static bool debug = false;

        /// <summary>
        /// Constructs a new SinoviaBrain
        /// </summary>
        public SinoviaBrain()
            : base()
        {
        }

        //override
        public override int ThinkInterval
        {
            get { return 300; }
        }
        public override void Think()
        {
            Sinovia Sin = Body as Sinovia;
            base.Think();
        }

        //Notify
        public override void Notify(DOL.Events.DOLEvent e, object sender, EventArgs args)
        {
            base.Notify(e, sender, args);
            if (sender == Body)
            {
                Sinovia Sin = sender as Sinovia;

                if (e == GameLivingEvent.EnemyHealed)
                {
                    GameObject source = (args as EnemyHealedEventArgs).HealSource;

                    if (source != null)
                    {
                        if (Sin.IsWithinRadius(source, Sin.MeleeAttackRange))
                        {
                            Sin.Pieuvre();
                        }

                    }

                }
            }
        }



    }

}
