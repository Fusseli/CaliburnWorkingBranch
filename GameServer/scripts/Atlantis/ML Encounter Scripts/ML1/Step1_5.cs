//---------------------------------------------------------
//---------------------ML1.5 - Krojer ---------------------
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

//Using Mgr

namespace DOL.GS.Atlantis
{

    //Krojer
    public class Krojer : GameNPC
    {

        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

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

        //NpcList
        public List<KrojerSentinel> KrojerSentinelList = new List<KrojerSentinel>();
        public List<SobekiteHulk> SobekiteHulkList = new List<SobekiteHulk>();
        public List<KrojerBlueFire> KrojerBlueFireList = new List<KrojerBlueFire>();
        public List<KrojerBlueFireH> KrojerBlueFireHList = new List<KrojerBlueFireH>();

        //Challenger's PTR
        public Agnon AgnonNPC;
        public Sethrendar SethrendarNPC;
        public Xalarian XalarianNPC;
        public Jilena JilenaNPC;
        public Malison MalisonNPC;
        public Regent RegentNPC;

        //Encounter State
        public bool InProgress = false;
        public GamePlayer Challenger;

        //KrojerSentinel
        public int[,] KrojerSentinelArray = {
			//  X      Y     Z   H
			{439502,560589,3735,2814}, //0---KrojerSentinel 1
			{444024,561636,3976,2013}, //1---KrojerSentinel 2
			{441962,561793,3812,1763}, //2---KrojerSentinel 3
			{443773,561648,3974,2013}, //3---KrojerSentinel 4
			{442077,557928,3568,250}, //4---KrojerSentinel 5
			{439609,558562,3647,3458}, //5---KrojerSentinel 6
			{444132,560726,3899,1422}, //6---KrojerSentinel 7
			{443601,559764,3738,1058}, //7---KrojerSentinel 8
			{444227,560566,3906,1331},  //8---KrojerSentinel 9
			{439597,558554,4786,3400}, //9---KrojerSentinel 10
            {439493,560588,4786,2795},  //10---KrojerSentinel 11
			{441959,561791,4786,1755}, //11---KrojerSentinel 12
            {442077,557930,4786,230}, //11---KrojerSentinel 13
		};

        //SobekiteHulk
        public int[,] SobekiteHulkArray = {
			//  X      Y     Z
			{440082,556703,3941}, //0---SobekiteHulk 1
			{440055,556277,4469}, //1---SobekiteHulk 2
			{438358,559251,4118}, //2---SobekiteHulk 3
			{436064,559147,4554}, //3---SobekiteHulk 4
			{437669,558158,4687}, //4---SobekiteHulk 5
			{436662,558841,4422}, //5---SobekiteHulk 6
			{438914,557217,4020}, //6---SobekiteHulk 7
			{439600,555449,4181}, //7---SobekiteHulk 8
			{436598,559456,4656}, //8---SobekiteHulk 9
			{436646,559429,4132}, //9---SobekiteHulk 10
            {436554,560574,4508}, //10---SobekiteHulk 11
			{438139,558059,4132}, //11---SobekiteHulk 12
            {436266,558446,5317}, //12---SobekiteHulk 13
            {438977,556435,4133}, //13---SobekiteHulk 14
            {438479,558064,4478}, //14---SobekiteHulk 15
            {435199,559129,4349}, //15---SobekiteHulk 16
			{436202,558627,4313}, //16---SobekiteHulk 17
			{436530,559116,4132}, //17---SobekiteHulk 18
			{441334,556934,4196}, //18---SobekiteHulk 19
			{440055,556267,3916}, //19---SobekiteHulk 20
			{441080,556474,4090}, //20---SobekiteHulk 21
			{440700,556032,4589}, //21---SobekiteHulk 22
			{439808,555512,4613}, //22---SobekiteHulk 23
			{437063,558940,4233}, //23---SobekiteHulk 24
			{440306,555830,4071}, //24---SobekiteHulk 25
		};

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

            //Load Sentinel
                for (int i = 0; i < 13; i++)
                {
                    SpawnGuard(KrojerSentinelArray[i, 0], KrojerSentinelArray[i, 1], KrojerSentinelArray[i, 2], (ushort)KrojerSentinelArray[i, 3]);
                }
                if (this.CurrentRegionID == albregion)
                {
                    log.Warn("Master Level - 1.5 -¨KrojerSentinels ALB added.");
                }
                else if (this.CurrentRegionID == midregion)
                {
                    log.Warn("Master Level - 1.5 -¨KrojerSentinels MID added.");
                }
                else if (this.CurrentRegionID == hibregion)
                {
                    log.Warn("Master Level - 1.5 -¨KrojerSentinels HIB added.");
                }

                //Load Sobekite Hulk
                for (int i = 0; i < 25; i++)
                {
                    SpawnSobekiteHulk(SobekiteHulkArray[i, 0], SobekiteHulkArray[i, 1], SobekiteHulkArray[i, 2]);
                }
                if (this.CurrentRegionID == albregion)
                {
                    log.Warn("Master Level - 1.5 -¨SobekiteHulk ALB added.");
                }
                else if (this.CurrentRegionID == midregion)
                {
                    log.Warn("Master Level - 1.5 -¨SobekiteHulk MID added.");
                }
                else if (this.CurrentRegionID == hibregion)
                {
                    log.Warn("Master Level - 1.5 -¨SobekiteHulk HIB added.");
                }

            return base.AddToWorld();
        }
        public override bool Interact(GamePlayer player)
        {
            if (base.Interact(player))
            {
                TurnTo(player, 1500);

                if (player.Level >= MinimumLevel)
                {
                    SayTo(player, "Hello " + player.Name + " , do you want try a challenge ?"
                        +"\n Choose a Challenger :"
                        + "\n [Agnon]"
                        + "\n [Sethrendar]"
                        + "\n [Xalarian]"
                        + "\n [Jilena]"
                        + "\n [Malison]"
                        + "\n [Regent]");
                }
                else if (player.Level < MinimumLevel)
                {
                    SayTo(player, "Yes ?");
                }

                return true;
            }

            return false;
        }
        public override bool WhisperReceive(GameLiving source, string str)
        {
            GamePlayer player = source as GamePlayer;
            if (player == null || player.Level < MinimumLevel || InProgress)
                return false;

            TurnTo(player, 1500);

            switch (str.ToLower())
            {
                case "agnon": SpawnAgnon(); InProgress = true; Challenger = player; break;
                case "sethrendar": SpawnSethrendar(); InProgress = true; Challenger = player; break;
                case "xalarian": SpawnXalarian(); InProgress = true; Challenger = player; break;
                case "jilena": SpawnJilena(); InProgress = true; Challenger = player; break;
                case "malison": SpawnMalison(); InProgress = true; Challenger = player; break;
                case "regent": SpawnRegent(); InProgress = true; Challenger = player; break;
                default: return false;
            }
            return true;
        }

        //Spawns Krojer and Challengers
        public static void SpawnKrojer(int region)
        {
            Krojer KrojerNpc = new Krojer();
            KrojerNpc.Name = "Krojer";
            KrojerNpc.GuildName = "";
            KrojerNpc.Model = 33747;
            KrojerNpc.Realm = 0;
            KrojerNpc.CurrentRegionID = (ushort)region;
            KrojerNpc.Size = 50;
            KrojerNpc.Level = 55;
            KrojerNpc.X = 443808;
            KrojerNpc.Y = 560400;
            KrojerNpc.Z = 3810;
            KrojerNpc.Heading = 1331;
            KrojerNpc.RoamingRange = 0;
            KrojerNpc.CurrentSpeed = 0;
            KrojerNpc.MaxSpeedBase = 191;
            KrojerNpc.RespawnInterval = 10 * 60 * 1000;
            KrojerNpc.Flags |= eFlags.PEACE;
            KrojerNpc.AutoSetStats();
            KrojerNpc.AddToWorld();
        }
        public void SpawnAgnon()
        {
            Agnon AgnonNpc = new Agnon();
            AgnonNpc.Name = "Agnon";
            AgnonNpc.GuildName = "";
            AgnonNpc.Model = 33744;
            AgnonNpc.Realm = 0;
            AgnonNpc.CurrentRegionID = this.CurrentRegionID;
            AgnonNpc.Size = 50;
            AgnonNpc.Level = 45;
            AgnonNpc.X = 444019;
            AgnonNpc.Y = 560383;
            AgnonNpc.Z = 3845;
            AgnonNpc.Heading = 1331;
            AgnonNpc.RoamingRange = 0;
            AgnonNpc.CurrentSpeed = 0;
            AgnonNpc.MaxSpeedBase = 191;
            AgnonNpc.RespawnInterval = 10 * 60 * 1000;
            AgnonNpc.Flags |= eFlags.PEACE;
            AgnonNpc.AutoSetStats();
            AgnonNpc.Parent = this;
            AgnonNpc.AddToWorld();
        }
        public void SpawnSethrendar()
        {
            Sethrendar AgnonNpc = new Sethrendar();
            AgnonNpc.Name = "Sethrendar";
            AgnonNpc.GuildName = "";
            AgnonNpc.Model = 33745;
            AgnonNpc.Realm = 0;
            AgnonNpc.CurrentRegionID = this.CurrentRegionID;
            AgnonNpc.Size = 50;
            AgnonNpc.Level = 45;
            AgnonNpc.X = 443941;
            AgnonNpc.Y = 560406;
            AgnonNpc.Z = 3833;
            AgnonNpc.Heading = 1331;
            AgnonNpc.RoamingRange = 0;
            AgnonNpc.CurrentSpeed = 0;
            AgnonNpc.MaxSpeedBase = 191;
            AgnonNpc.RespawnInterval = 10 * 60 * 1000;
            AgnonNpc.Flags |= eFlags.PEACE;
            AgnonNpc.AutoSetStats();
            AgnonNpc.Parent = this;
            AgnonNpc.AddToWorld();
        }
        public void SpawnXalarian()
        {
            Xalarian XalarianNpc = new Xalarian();
            XalarianNpc.Name = "Xalarian";
            XalarianNpc.GuildName = "";
            XalarianNpc.Model = 33746;
            XalarianNpc.Realm = 0;
            XalarianNpc.CurrentRegionID = this.CurrentRegionID;
            XalarianNpc.Size = 50;
            XalarianNpc.Level = 45;
            XalarianNpc.X = 443982;
            XalarianNpc.Y = 560455;
            XalarianNpc.Z = 3845;
            XalarianNpc.Heading = 1331;
            XalarianNpc.RoamingRange = 0;
            XalarianNpc.CurrentSpeed = 0;
            XalarianNpc.MaxSpeedBase = 191;
            XalarianNpc.RespawnInterval = 10 * 60 * 1000;
            XalarianNpc.Flags |= eFlags.PEACE;
            XalarianNpc.AutoSetStats();
            XalarianNpc.Parent = this;
            XalarianNpc.AddToWorld();
        }
        public void SpawnJilena()
        {
            Jilena JilenaNpc = new Jilena();
            JilenaNpc.Name = "Jilena";
            JilenaNpc.GuildName = "";
            JilenaNpc.Model = 33748;
            JilenaNpc.Realm = 0;
            JilenaNpc.CurrentRegionID = this.CurrentRegionID;
            JilenaNpc.Size = 50;
            JilenaNpc.Level = 45;
            JilenaNpc.X = 443953;
            JilenaNpc.Y = 560514;
            JilenaNpc.Z = 3845;
            JilenaNpc.Heading = 1331;
            JilenaNpc.RoamingRange = 0;
            JilenaNpc.CurrentSpeed = 0;
            JilenaNpc.MaxSpeedBase = 191;
            JilenaNpc.RespawnInterval = 10 * 60 * 1000;
            JilenaNpc.Flags |= eFlags.PEACE;
            JilenaNpc.AutoSetStats();
            JilenaNpc.Parent = this;
            JilenaNpc.AddToWorld();
        }
        public void SpawnMalison()
        {
            Malison MalisonNpc = new Malison();
            MalisonNpc.Name = "Malison";
            MalisonNpc.GuildName = "";
            MalisonNpc.Model = 33744;
            MalisonNpc.Realm = 0;
            MalisonNpc.CurrentRegionID = this.CurrentRegionID;
            MalisonNpc.Size = 50;
            MalisonNpc.Level = 45;
            MalisonNpc.X = 443888;
            MalisonNpc.Y = 560579;
            MalisonNpc.Z = 3845;
            MalisonNpc.Heading = 1604;
            MalisonNpc.RoamingRange = 0;
            MalisonNpc.CurrentSpeed = 0;
            MalisonNpc.MaxSpeedBase = 191;
            MalisonNpc.RespawnInterval = 10 * 60 * 1000;
            MalisonNpc.Flags |= eFlags.PEACE;
            MalisonNpc.AutoSetStats();
            MalisonNpc.Parent = this;
            MalisonNpc.AddToWorld();
        }
        public void SpawnRegent()
        {
            Regent RegentNpc = new Regent();
            RegentNpc.Name = "Regent";
            RegentNpc.GuildName = "";
            RegentNpc.Model = 33745;
            RegentNpc.Realm = 0;
            RegentNpc.CurrentRegionID = this.CurrentRegionID;
            RegentNpc.Size = 50;
            RegentNpc.Level = 45;
            RegentNpc.X = 443882;
            RegentNpc.Y = 560628;
            RegentNpc.Z = 3845;
            RegentNpc.Heading = 1456;
            RegentNpc.RoamingRange = 0;
            RegentNpc.CurrentSpeed = 0;
            RegentNpc.MaxSpeedBase = 191;
            RegentNpc.RespawnInterval = 10 * 60 * 1000;
            RegentNpc.Flags |= eFlags.PEACE;
            RegentNpc.AutoSetStats();
            RegentNpc.Parent = this;
            RegentNpc.AddToWorld();
        }

        //Spawns Others
        public void SpawnGuard(int X, int Y, int Z, ushort H)
        {
            KrojerSentinel Guard = new KrojerSentinel();
            Guard.Name = "Krojer sentinel";
            Guard.GuildName = "";
            Guard.Model = 33745;
            Guard.Realm = 0;
            Guard.CurrentRegionID = this.CurrentRegionID;
            Guard.Size = 50;
            Guard.Level = 50;
            Guard.X = X;
            Guard.Y = Y;
            Guard.Z = Z;
            Guard.Heading = H;
            Guard.RoamingRange = 0;
            Guard.CurrentSpeed = 0;
            Guard.MaxSpeedBase = 170;
            Guard.AutoSetStats();
            Guard.RespawnInterval = 10 * 60 * 1000;
            Guard.BodyType = 0;
            Guard.Flags = eFlags.PEACE;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 50;
            brain.AggroRange = 1000;
            Guard.SetOwnBrain(brain);
            KrojerSentinelList.Add(Guard);
            Guard.AddToWorld();
        }
        public void SpawnSobekiteHulk(int X, int Y, int Z)
        {
            SobekiteHulk Sobekite = new SobekiteHulk();
            Sobekite.Name = "Sobekite Hulk";
            Sobekite.GuildName = "";
            Sobekite.Model = 33800;
            Sobekite.Realm = 0;
            Sobekite.CurrentRegionID = this.CurrentRegionID;
            Sobekite.Size = 62;
            Sobekite.Level = 70;
            Sobekite.X = X;
            Sobekite.Y = Y;
            Sobekite.Z = Z;
            Sobekite.Heading = (ushort)Util.Random(200, 3000);
            Sobekite.RoamingRange = 200;
            Sobekite.CurrentSpeed = 0;
            Sobekite.MaxSpeedBase = 191;
            Sobekite.AutoSetStats();
            Sobekite.RespawnInterval = 15 * 60 * 1000;
            Sobekite.BodyType = 0;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 400;
            Sobekite.SetOwnBrain(brain);
            SobekiteHulkList.Add(Sobekite);
            Sobekite.AddToWorld();
        }
        public void SpawnBlueFire(int X, int Y, int Z)
        {
            KrojerBlueFire BlueFire = new KrojerBlueFire();
            BlueFire.Name = "BlueFire";
            BlueFire.GuildName = "";
            BlueFire.Model = 907;
            BlueFire.Realm = 0;
            BlueFire.CurrentRegionID = this.CurrentRegionID;
            BlueFire.Size = 50;
            BlueFire.Level = 70;
            BlueFire.X = X;
            BlueFire.Y = Y;
            BlueFire.Z = Z;
            BlueFire.Heading = (ushort)Util.Random(200, 3000);
            BlueFire.RoamingRange = 0;
            BlueFire.CurrentSpeed = 0;
            BlueFire.MaxSpeedBase = 191;
            BlueFire.AutoSetStats();
            BlueFire.BodyType = 0;
            BlueFire.Flags |= eFlags.CANTTARGET;
            BlueFire.Flags |= eFlags.PEACE;
            KrojerBlueFireList.Add(BlueFire);
            BlueFire.AddToWorld();
        }
        public void SpawnBlueFireH(int X, int Y, int Z)
        {
            KrojerBlueFireH BlueFire = new KrojerBlueFireH();
            BlueFire.Name = "BlueFire";
            BlueFire.GuildName = "";
            BlueFire.Model = 907;
            BlueFire.Realm = 0;
            BlueFire.CurrentRegionID = this.CurrentRegionID;
            BlueFire.Size = 50;
            BlueFire.Level = 70;
            BlueFire.X = X;
            BlueFire.Y = Y;
            BlueFire.Z = Z;
            BlueFire.Heading = (ushort)Util.Random(200, 3000);
            BlueFire.RoamingRange = 0;
            BlueFire.CurrentSpeed = 0;
            BlueFire.MaxSpeedBase = 191;
            BlueFire.AutoSetStats();
            BlueFire.BodyType = 0;
            BlueFire.Flags |= eFlags.CANTTARGET;
            BlueFire.Flags |= eFlags.PEACE;
            KrojerBlueFireHList.Add(BlueFire);
            BlueFire.AddToWorld();
        }

        //Spawn BlueFireArray
        public void SpawnCage()
        {

        //BlueFireBaseArray
        int[,] FiresArray = {
		{439569,559090,3676}, //0---BlueFireBas 1
		{439541,559486,3676}, //1---BlueFireBas 2
		{442018,559887,3583}, //2---BlueFireBas 3
		{442070,558581,3577}, //3---BlueFireBas 4
		{439587,558761,3676}, //4---BlueFireBas 5
		{441637,561037,3729}, //5---BlueFireBas 6
		{441997,560587,3698}, //6---BlueFireBas 7
		{441303,558114,3608}, //7---BlueFireBas 8
		{440262,560763,3729}, //8---BlueFireBas 9
		{442077,558274,3577}, //9---BlueFireBas 10
        {442077,557928,3568}, //10---BlueFireBas 11
		{440765,560866,3729}, //11---BlueFireBas 12
        {441980,561103,3747}, //12---BlueFireBas 13
        {441208,560954,3729}, //13---BlueFireBas 14
        {439512,559896,3676}, //14---BlueFireBas 15
        {439486,560276,3706}, //15---BlueFireBas 16
		{442031,559602,3577}, //16---BlueFireBas 17
		{441711,558016,3578}, //17---BlueFireBas 18
		{442014,560211,3652}, //18---BlueFireBas 19
		{440185,558409,3651}, //19---BlueFireBas 20
		{439870,558492,3651}, //20---BlueFireBas 21
		{442064,558898,3577}, //21---BlueFireBas 22
		{439866,560681,3729}, //22---BlueFireBas 23
		{442047,559256,3577}, //23---BlueFireBas 24
		{440489,558332,3651}, //24---BlueFireBas 25
        {440895,558223,3651}, //25---BlueFireBas 26
        {441989,560835,3689}, //26---BlueFireBas 27
        {442004,560417,3639}, //27---BlueFireBas 28
        {442016,560063,3597}, //28---BlueFireBas 29
        {442025,559743,3550}, //29---BlueFireBas 30
        {442040,559416,3550}, //30---BlueFireBas 31
        {442058,559055,3550}, //31---BlueFireBas 32
        {442069,558712,3552}, //32---BlueFireBas 33
        {442076,558421,3552}, //33---BlueFireBas 34
        {442078,558116,3552}, //34---BlueFireBas 35
        {441897,557969,3557}, //35---BlueFireBas 36
        {441499,558065,3574}, //36---BlueFireBas 37
        {441089,558170,3569}, //37---BlueFireBas 38
        {440686,558277,3569}, //38---BlueFireBas 39
        {440350,558368,3587}, //39---BlueFireBas 40
        {440024,558450,3595}, //40---BlueFireBas 41
        {439578,558917,3640}, //41---BlueFireBas 42
        {439557,559274,3640}, //42---BlueFireBas 43
        {439527,559683,3656}, //43---BlueFireBas 44
        {439495,560095,3687}, //44---BlueFireBas 45
        {439664,560636,3722}, //45---BlueFireBas 46
        {440040,560719,3712}, //46---BlueFireBas 47
        {440517,560815,3666}, //47---BlueFireBas 48
        {440989,560911,3666}, //48---BlueFireBas 49
        {441426,560996,3666}, //49---BlueFireBas 50
        {441805,561068,3679}, //50---BlueFireBas 51
		};

        //TopFireArray
        int[,] FireH = {
		{441484,559518,3893}, //0---BlueFireHaut 1
		{441760,558358,3865}, //1---BlueFireHaut 2
		{441099,560023,3951}, //2---BlueFireHaut 3
		{440720,560083,3951}, //3---BlueFireHaut 4
		{439994,559800,3966}, //4---BlueFireHaut 5
		{440180,559098,3931}, //5---BlueFireHaut 6
		{440346,559734,3935}, //6---BlueFireHaut 7
		{441237,558817,3931}, //7---BlueFireHaut 8
		{440274,560153,3951}, //8---BlueFireHaut 9
		{440819,558929,3931}, //9---BlueFireHaut 10
        {441253,558488,3935}, //10---BlueFireHaut 11
		{439722,560239,4023}, //11---BlueFireHaut 12
        {440636,558647,3935}, //12---BlueFireHaut 13
        {441640,559941,3932}, //13---BlueFireHaut 14
        {440939,559622,3935}, //14---BlueFireHaut 15
        {441655,560544,3985}, //15---BlueFireHaut 16
		{440020,558808,3935}, //16---BlueFireHaut 17
		{441755,558673,3901}, //17---BlueFireHaut 18
		{441816,558956,3888}, //18---BlueFireHaut 19
		{441438,559054,3888}, //19---BlueFireHaut 20
		{441126,559140,3888}, //20---BlueFireHaut 21
		{440653,559250,3913}, //21---BlueFireHaut 22
		{439992,559394,3964}, //22---BlueFireHaut 23
		{441305,560503,3985}, //23---BlueFireHaut 24
		{440747,560440,3985}, //24---BlueFireHaut 25
        {440110,560415,3985}, //25---BlueFireHaut 26
        {439713,560406,3985}, //26---BlueFireHaut 27
		};

        for (int i = 0; i < 51; i++)
        {
            SpawnBlueFire(FiresArray[i, 0], FiresArray[i, 1], FiresArray[i, 2]);
            SpawnBlueFire(FiresArray[i, 0], FiresArray[i, 1], FiresArray[i, 2] + 100);
            SpawnBlueFire(FiresArray[i, 0], FiresArray[i, 1], FiresArray[i, 2] + 200);
        }
        for (int i = 0; i < 27; i++)
        {
            SpawnBlueFireH(FireH[i, 0], FireH[i, 1], FireH[i, 2]);
        }
        if (this.CurrentRegionID == albregion)
        {
            log.Warn("Master Level - 1.5 -¨KrojerArea ALB added.");
        }
        else if (this.CurrentRegionID == midregion)
        {
            log.Warn("Master Level - 1.5 -¨KrojerArea MID added.");
        }
        else if (this.CurrentRegionID == hibregion)
        {
            log.Warn("Master Level - 1.5 -¨KrojerArea HIB added.");
        }

        }
        public void DeSpawnCage()
        {
            foreach (KrojerBlueFire fire in KrojerBlueFireList)
            {
                fire.Health = 0;
                fire.Delete();
            }
            KrojerBlueFireList.Clear();
            foreach (KrojerBlueFireH fireH in KrojerBlueFireHList)
            {
                fireH.Health = 0;
                fireH.Delete();
            }
            KrojerBlueFireHList.Clear();
            if (this.CurrentRegionID == albregion)
            {
                log.Warn("Master Level - 1.5 - KrojerArea ALB deleted.");
            }
            else if (this.CurrentRegionID == midregion)
            {
                log.Warn("Master Level - 1.5 - KrojerArea MID deleted.");
            }
            else if (this.CurrentRegionID == hibregion)
            {
                log.Warn("Master Level - 1.5 - KrojerArea HIB deleted.");
            }

        }

        //Load Event
        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Warn("Master Level - 1.5 - Initializing Event...");
            if (Albion == true)
            {
                SpawnKrojer(albregion);
                log.Warn("Master Level - 1.5 - Krojer ALB added.");
            }
            if (Midgard == true)
            {
                SpawnKrojer(midregion);
                log.Warn("Master Level - 1.5 - Krojer MID added.");
            }
            if (Hibernia == true)
            {
                SpawnKrojer(hibregion);
                log.Warn("Master Level - 1.5 - Krojer HIB added.");
            }
            log.Warn("Master Level - 1.5 - Event Initialized !");
        }
    }

    //------------- Around NPC ---------------

    //SobekiteHulk
    public class SobekiteHulk : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
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

    //Sentinel
    public class KrojerSentinel : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
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

    //----------- Challenger's NPC -----------

    //Agnon
    public class Agnon : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;
        public Krojer Parent;

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            base.StartRespawn();
        }
        public override bool AddToWorld()
        {
            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {
            MLCreditHelper.CreditML(1, 5, killer, true, false, 40);
            if (Parent != null) Parent.InProgress = false;
            base.Die(killer);
        }

    }

    //Sethrendar
    public class Sethrendar : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;
        public Krojer Parent;

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            base.StartRespawn();
        }
        public override bool AddToWorld()
        {
            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {
            MLCreditHelper.CreditML(1, 5, killer, true, false, 40);
            if (Parent != null) Parent.InProgress = false;
            base.Die(killer);
        }

    }

    //Xalarian
    public class Xalarian : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;
        public Krojer Parent;

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            base.StartRespawn();
        }
        public override bool AddToWorld()
        {
            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {
            MLCreditHelper.CreditML(1, 5, killer, true, false, 40);
            if (Parent != null) Parent.InProgress = false;
            base.Die(killer);
        }

    }

    //Jilena
    public class Jilena : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;
        public Krojer Parent;

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            base.StartRespawn();
        }
        public override bool AddToWorld()
        {
            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {
            MLCreditHelper.CreditML(1, 5, killer, true, false, 40);
            if (Parent != null) Parent.InProgress = false;
            base.Die(killer);
        }

    }

    //Malison
    public class Malison : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;
        public Krojer Parent;

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            base.StartRespawn();
        }
        public override bool AddToWorld()
        {
            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {
            MLCreditHelper.CreditML(1, 5, killer, true, false, 40);
            if (Parent != null) Parent.InProgress = false;
            base.Die(killer);
        }

    }

    //Regent
    public class Regent : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;
        public Krojer Parent;

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            base.StartRespawn();
        }
        public override bool AddToWorld()
        {
            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {
            MLCreditHelper.CreditML(1, 5, killer, true, false, 40);
            if (Parent != null) Parent.InProgress = false;
            base.Die(killer);
        }

    }

    //----------------- Cage -----------------

    //Blue Fire
    public class KrojerBlueFire : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;

        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
        }
        public override bool AddToWorld()
        {
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(KillArea), (1 * 1000));
            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {
            base.Die(killer);
        }

        //Timers
        public int KillArea(ECSGameTimer timer)
        {
          
            //Kill
            foreach (GamePlayer player in GetPlayersInRadius(200))
            {
                if ((player.Z > this.Z) & (player.IsAlive == true))
                {
                    player.Out.SendMessage("The Wall of Fire burns you for 5,000 damage!", eChatType.CT_Damaged, eChatLoc.CL_ChatWindow);
                    player.Out.SendSpellEffectAnimation(this, player, 310, 0, false, 1);
                    player.TakeDamage(this, eDamageType.Heat, 5000, 0);
                    foreach (GamePlayer onlookers in player.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        if (onlookers != player)
                        {
                            onlookers.Out.SendSpellEffectAnimation(this, player, 310, 0, false, 1);
                            onlookers.Out.SendMessage("The Wall of Fire burns " + player.Name + " to death!", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                        }
                    }
                }
            }

            //Reload Timer
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(KillArea), (500));

            return 0;
        }

    }

    //Blue Fire H
    public class KrojerBlueFireH : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;

        //Override
        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
        }
        public override bool AddToWorld()
        {
            //Timer Kill Area
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(KillArea), (1 * 1000));

            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {
            base.Die(killer);
        }

        //Timers
        public int KillArea(ECSGameTimer timer)
        {

            //Kill
            foreach (GamePlayer player in GetPlayersInRadius(1000))
            {
                if ((player.Z > this.Z) & (player.IsAlive == true))
                {
                    player.Out.SendMessage("The Wall of Fire burns you for 5,000 damage!", eChatType.CT_Damaged, eChatLoc.CL_ChatWindow);
                    player.Out.SendSpellEffectAnimation(this, player, 310, 0, false, 1);
                    player.TakeDamage(this, eDamageType.Heat, 5000, 0);
                    foreach (GamePlayer onlookers in player.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        if (onlookers != player)
                        {
                            onlookers.Out.SendSpellEffectAnimation(this, player, 310, 0, false, 1);
                            onlookers.Out.SendMessage("The Wall of Fire burns " + player.Name + " to death!", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                        }
                    }
                }
            }

            //Reload Timer
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(KillArea), (500));

            return 0;
        }
    }

}