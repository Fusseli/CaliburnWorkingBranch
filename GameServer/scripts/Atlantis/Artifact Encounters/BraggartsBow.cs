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
    public class Karise : GameNPC
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static bool debug = false;

        public static int AlbRegionID = 73;
        public static int MidRegionID = 30;
        public static int HibRegionID = 130;

        public static bool Albion = false;
        public static bool Midgard = false;
        public static bool Hibernia = true;

        public static Karise KariseAlbion;
        public static Karise KariseMidgard;
        public static Karise KariseHibernia;

        public static int MinimumLevel = 46;
        public static int MinimumRespawn = 20;
        public static int MaximumRespawn = 30;

        public static int[,] GuardiansArray = {
			{404025,670899,8019,2924},
			{403504,671295,8040,386},
			{403373,671116,8036,705},
			{403499,670725,8022,1661},
			{403706,671356,8036,34},
			{404034,671121,8026,3254},
			{403694,670662,8014,2025},
			{403900,670731,8016,2446},
			{403359,670900,8029,1251},
			{403907,671288,8033,3686},
        };

        public List<KariseGuardian> GuardiansList = new List<KariseGuardian>();

		[ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Warn("Artifact - Braggart's Bow - Initializing Event ...");
            if (Albion == true)
            {
                SpawnKarise(AlbRegionID);
                log.Warn("Artifact - Braggart's Bow - Karise ALB added.");
            }
            if (Midgard == true)
            {
                SpawnKarise(MidRegionID);
                log.Warn("Artifact - Braggart's Bow - Karise MID added.");
            }
            if (Hibernia == true)
            {
                SpawnKarise(HibRegionID);
                log.Warn("Artifact - Braggart's Bow - Karise HIB added.");
            }
            log.Warn("Artifact - Braggart's Bow - Event Initialized !");
        }

        public override bool AddToWorld()
        {
            SpawnEncounter();
            return base.AddToWorld();
        }
        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            this.RespawnInterval = Util.Random(Karise.MinimumRespawn, Karise.MaximumRespawn) * 60 * 1000;
            base.StartRespawn();
        }

        public void SpawnEncounter()
        {
            for (int i = 0; i < 10; i++)
            {
                SpawnGuardian(GuardiansArray[i, 0], GuardiansArray[i, 1], GuardiansArray[i, 2], (ushort)GuardiansArray[i, 3]);
            }

            if (this.CurrentRegionID == AlbRegionID) log.Warn("Artifact - Braggart's Bow - ALB available !");
            if (this.CurrentRegionID == MidRegionID) log.Warn("Artifact - Braggart's Bow - MID available !");
            if (this.CurrentRegionID == HibRegionID) log.Warn("Artifact - Braggart's Bow - HIB available !");
        }

        public void SpawnGuardian(int X, int Y, int Z, ushort H)
        {
            KariseGuardian Attacker = new KariseGuardian();
            Attacker.Model = 985;
            Attacker.Size = 50;
            Attacker.Level = 66;
            Attacker.Name = "Karise's guardian";
            Attacker.CurrentRegionID = this.CurrentRegionID;
            Attacker.Heading = H;
            Attacker.Realm = 0;
            Attacker.CurrentSpeed = 0;
            Attacker.MaxSpeedBase = 0;
            Attacker.GuildName = "";
            Attacker.X = X;
            Attacker.Y = Y;
            Attacker.Z = Z;
            Attacker.RoamingRange = 0;
            Attacker.RespawnInterval = 10 * 60 * 1000;
            Attacker.BodyType = 0;
            ScoutMobBrain brain = new ScoutMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 1200;
            Attacker.SetOwnBrain(brain);
            GameNpcInventoryTemplate template = new GameNpcInventoryTemplate();
            template.AddNPCEquipment(eInventorySlot.DistanceWeapon, 848, 57);
            template.CloseTemplate();
            Attacker.Inventory = template;
            Attacker.SwitchWeapon(eActiveWeaponSlot.Distance);
            if (debug == true) Attacker.debug = true;
            Attacker.Parent = this;
            GuardiansList.Add(Attacker);
            Attacker.AddToWorld();
            return;
        }

        public static void SpawnKarise(int region)
        {
            Karise Karise = new Karise();
            Karise.Name = "Karise";
            Karise.GuildName = "";
            Karise.Model = 987;
            Karise.Size = 75;
            Karise.Realm = eRealm.None;
            Karise.CurrentRegionID = (ushort)region;
            Karise.Level = 75;
            Karise.X = 403703;
            Karise.Y = 671013;
            Karise.Z = 8025;
            Karise.Heading = 2912;
            Karise.RoamingRange = 0;
            Karise.CurrentSpeed = 0;
            Karise.MaxSpeedBase = 30;
            Karise.AutoSetStats();
            Karise.RoamingRange = 0;
            Karise.RespawnInterval = 10 * 60 * 1000;
            Karise.BodyType = 0;
            GameNpcInventoryTemplate template = new GameNpcInventoryTemplate();
            template.AddNPCEquipment(eInventorySlot.DistanceWeapon, 848, 57);
            template.CloseTemplate();
            Karise.Inventory = template;
            Karise.SwitchWeapon(eActiveWeaponSlot.Distance);
            ScoutMobBrain brain = new ScoutMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 1000;
            Karise.SetOwnBrain(brain);
            Karise.AddToWorld();
        }
    }

    public class KariseGuardian : GameNPC
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;

        public Karise Parent;
        public Bubble Children;

        public override bool AddToWorld()
        {
            if (this.X == 404025) SpawnBubble(403928, 670924, 8022);
            else if (this.X == 403504) SpawnBubble(403557,671211,8036);
            else if (this.X == 403373) SpawnBubble(403470,671090,8035);
            else if (this.X == 403499) SpawnBubble(403566,670823,3704);
            else if (this.X == 403706) SpawnBubble(403708,671259,8032);
            else if (this.X == 404034) SpawnBubble(403928,671093,8027);
            else if (this.X == 403694) SpawnBubble(403700,670772,8017);
            else if (this.X == 403900) SpawnBubble(403843,670814,8018);
            else if (this.X == 403359) SpawnBubble(403473,670939,8030);
            else if (this.X == 403907) SpawnBubble(403848,671212,8031);

            return base.AddToWorld();
        }
        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            base.StartRespawn();
        }
        public override void TakeDamage(AttackData ad)
        {
        }
        public override void Die(GameObject killer)
        {
            base.Die(killer);
        }
        public override void StartAttack(GameObject target)
        {
            if (Children != null && Children.IsAlive == false)
            {
                return;
            }
            base.StartAttack(target);
        }

        public void SpawnBubble(int X, int Y, int Z)
        {
            Bubble Bubble = new Bubble();
            Bubble.Model = 966;
            Bubble.Size = 100;
            Bubble.Level = 50;
            Bubble.Name = "Gardian's Bubble";
            Bubble.CurrentRegionID = this.CurrentRegionID;
            Bubble.Heading = 0;
            Bubble.Realm = 0;
            Bubble.CurrentSpeed = 0;
            Bubble.MaxSpeedBase = 0;
            Bubble.GuildName = "";
            Bubble.X = X;
            Bubble.Y = Y;
            Bubble.Z = Z;
            Bubble.RoamingRange = 0;
            Bubble.BodyType = 0;
            Bubble.Health = 800;
            Bubble.Flags |= eFlags.DONTSHOWNAME;
            Bubble.Flags |= eFlags.TORCH;
            BlankBrain brain = new BlankBrain();
            Bubble.SetOwnBrain(brain);
            if (debug == true) Bubble.debug = true;
            Bubble.Parent = this;
            Children = Bubble;
            Bubble.AddToWorld();
        }
    }

    public class Bubble : GameNPC
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;

        public KariseGuardian Parent;

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            this.RespawnInterval = Util.Random(30, 360) * 1000;
            base.StartRespawn();
        }
        public override void Die(GameObject killer)
        {
            base.Die(killer);
        }
    }
}
