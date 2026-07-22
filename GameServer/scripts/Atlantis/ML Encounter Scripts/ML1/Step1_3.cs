//---------------------------------------------------------
//-----------------ML 1.3 - Greater Good ------------------
//-------------------Author : Hibernos---------------------
//---------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.AI.Brain;
using DOL.Events;
using log4net;
using System.Reflection;

using DOL.GS.Spells;
using DOL.Database;

//Using Mgr

namespace DOL.GS.Atlantis
{

    //Chief
    public class ArxemOxomis : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static bool debug = true;

        //Minimum Level
        public static int MinimumLevel = 40;

        //Minimum Respawn Time - Maximum Respawn Time ( in minutes ) of Chief
        public static int MinRespawn = 30;
        public static int MaxRespawn = 45;

        //Realm Regions
        public static int albregion = 73;
        public static int midregion = 30;
        public static int hibregion = 130;

        //Realm Available for this Step
        public static bool Albion = false;
        public static bool Midgard = false;
        public static bool Hibernia = true;

        //initialized - name
        public bool Initialized = false;
        public string MyBaseName = "";

        //Guards Lists
        public List<ArxemOxomisCasterGuard> ArxemCasterGuardList = new List<ArxemOxomisCasterGuard>();
        public List<ArxemOxomisHealerGuard> ArxemHealerGuardList = new List<ArxemOxomisHealerGuard>();
        public List<ArxemOxomisUsualGuard> ArxemUsualGuardList = new List<ArxemOxomisUsualGuard>();
        public List<ArxemOxomisCasterGuard> OxomisCasterGuardList = new List<ArxemOxomisCasterGuard>();
        public List<ArxemOxomisHealerGuard> OxomisHealerGuardList = new List<ArxemOxomisHealerGuard>();
        public List<ArxemOxomisUsualGuard> OxomisUsualGuardList = new List<ArxemOxomisUsualGuard>();

        //Overrides
        public override void SaveIntoDatabase() //NoSaveInDB
        {
        }
        public override void StartRespawn() //Start Respawn
        {
            this.RespawnInterval = Util.Random(MinRespawn, MaxRespawn) * 60 * 1000;
            base.StartRespawn();
        }
        public override bool AddToWorld() //Add To World
        {
            if(Initialized == false)
            {
                if (this.MyBaseName == "Zhton Chief Arxem")
                {
                    SpawnCasterGuard(369560, 526226, 6319, 1660);
                    SpawnUsualGuard(370489, 525807, 6280, 1762);
                    SpawnUsualGuard(369953, 525958, 6280, 1674);
                    SpawnHealerGuard(371045, 525796, 6406, 2361);
                    SpawnUsualGuard(371402, 526172, 6321, 3539);
                    SpawnCasterGuard(370856, 526701, 6321, 3483);
                    SpawnUsualGuard(370316, 527288, 6390, 3760);
                    SpawnHealerGuard(369951, 527629, 6390, 3540);
                    SpawnUsualGuard(369450, 527532, 6390, 775);
                    SpawnUsualGuard(369419, 526905, 6414, 1406);
                    if (this.CurrentRegionID == albregion) log.Warn("Master Level - 1.3 -¨Arxem Gards ALB added.");
                    if (this.CurrentRegionID == midregion) log.Warn("Master Level - 1.3 -¨Arxem Gards MID added.");
                    if (this.CurrentRegionID == hibregion) log.Warn("Master Level - 1.3 -¨Arxem Gards HIB added.");
                }
                else if (this.MyBaseName == "Kynhroe Chief Oxomis")
                {
                    SpawnCasterGuard(374775, 517932, 5178, 3830);
                    SpawnUsualGuard(374203, 516175, 5234, 2245);
                    SpawnUsualGuard(374314, 518250, 5203, 302);
                    SpawnHealerGuard(374112, 517875, 5312, 1063);
                    SpawnUsualGuard(373813, 517314, 5312, 858);
                    SpawnCasterGuard(373517, 516645, 5312, 1514);
                    SpawnUsualGuard(373731, 516221, 5345, 2105);
                    SpawnHealerGuard(374490, 516373, 5246, 2742);
                    SpawnUsualGuard(374939, 516808, 5185, 3100);
                    SpawnUsualGuard(374918, 517475, 5125, 3516);
                    if (this.CurrentRegionID == albregion) log.Warn("Master Level - 1.3 -¨Oxomis Gards ALB added.");
                    if (this.CurrentRegionID == midregion) log.Warn("Master Level - 1.3 -¨Oxomis Gards MID added.");
                    if (this.CurrentRegionID == hibregion) log.Warn("Master Level - 1.3 -¨Oxomis Gards HIB added.");
                }
                Initialized = true;
            }
            return base.AddToWorld();
        }
        public override void Die(GameObject killer) //Die
        {
            //Change Aggro Settings
            if (this.MyBaseName == "Zhton Chief Arxem")
            {
                foreach (ArxemOxomisCasterGuard TheGuard in ArxemCasterGuardList)
                {
                    //Change Aggro Setting to NULL
                }
                foreach (ArxemOxomisHealerGuard TheGuard in ArxemHealerGuardList)
                {
                    //Change Aggro Setting to NULL
                }
                foreach (ArxemOxomisUsualGuard TheGuard in ArxemUsualGuardList)
                {
                    //Change Aggro Setting to NULL
                }
            }
            else if (this.MyBaseName == "Kynhroe Chief Oxomis")
            {
                foreach (ArxemOxomisCasterGuard TheGuard in OxomisCasterGuardList)
                {
                    //Change Aggro Setting to NULL
                }
                foreach (ArxemOxomisHealerGuard TheGuard in OxomisHealerGuardList)
                {
                    //Change Aggro Setting to NULL
                }
                foreach (ArxemOxomisUsualGuard TheGuard in OxomisUsualGuardList)
                {
                    //Change Aggro Setting to NULL
                }
            }
            MLCreditHelper.CreditML((byte)1, (byte)3, killer, true, false, (byte)ArxemOxomis.MinimumLevel);
            base.Die(killer);
        }
        public override void StartAttack(GameObject target) //Start Attack
        {
            //Change Aggro Settings
            if (this.MyBaseName == "Zhton Chief Arxem")
            {
                foreach (ArxemOxomisCasterGuard TheGuard in ArxemCasterGuardList)
                {
                    //Change Aggro Setting to MAX
                }
                foreach (ArxemOxomisHealerGuard TheGuard in ArxemHealerGuardList)
                {
                    //Change Aggro Setting to MAX
                }
                foreach (ArxemOxomisUsualGuard TheGuard in ArxemUsualGuardList)
                {
                    //Change Aggro Setting to MAX
                }
            }
            else if (this.MyBaseName == "Kynhroe Chief Oxomis")
            {
                foreach (ArxemOxomisCasterGuard TheGuard in OxomisCasterGuardList)
                {
                    //Change Aggro Setting to MAX
                }
                foreach (ArxemOxomisHealerGuard TheGuard in OxomisHealerGuardList)
                {
                    //Change Aggro Setting to MAX
                }
                foreach (ArxemOxomisUsualGuard TheGuard in OxomisUsualGuardList)
                {
                    //Change Aggro Setting to MAX
                }
            }
            base.StartAttack(target);
        }
        public override bool Interact(GamePlayer player) //Interact
        {
            if (base.Interact(player))
            {
                TurnTo(player, 1500);

                if (player.Level >= MinimumLevel)
                {
                    string Camp = "";
                    if (this.MyBaseName == "Zhton Chief Arxem") Camp = "Zhton";
                    if (this.MyBaseName == "Kynhroe Chief Oxomis") Camp = "Kynhroe";
                    string OtherCamp = "";
                    if (this.MyBaseName == "Zhton Chief Arxem") OtherCamp = "Kynhroe";
                    if (this.MyBaseName == "Kynhroe Chief Oxomis") OtherCamp = "Zhton";
                    string TalkLines;
                    TalkLines = "Are you a " + OtherCamp + " assassin? ha well,let me tell you a little about the " + OtherCamp + " Clan before you draw your weapons."
                        +" They are a pack of murderous thieves."
                        +" They have stymied our every attempt to get them under control."
                        +"All we "+ Camp +" want is to compete for the right to be called a hero."
                        +" But we cannot compete when the " + OtherCamp + "s are constantly stealing our food,our weapons,our valuables and our lives!"
                        +" Do not believe a word they tell you my friend, they are vile killers.";
                    foreach (GamePlayer onlookers in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        if (onlookers != player)
                        {
                            onlookers.Out.SendMessage(this.MyBaseName + " speaks to " + player.Name + " .", eChatType.CT_Chat, eChatLoc.CL_ChatWindow);
                        }
                    }
                    SayTo(player, TalkLines);
                }
                else if (player.Level < MinimumLevel)
                {
                    SayTo(player, "Go out !");
                }

                return true;
            }

            return false;
        }

        //Spawn Guards
        public void SpawnCasterGuard(int X, int Y, int Z,ushort H) //Spawn Caster Guard
        {
            ArxemOxomisCasterGuard Guard = new ArxemOxomisCasterGuard();
            if (this.MyBaseName == "Zhton Chief Arxem")
            {
                Guard.Name = "Zhton dark mage";
            }
            else if (this.MyBaseName == "Kynhroe Chief Oxomis")
            {
                Guard.Name = "Kynhroe dark mage";
            }
            Guard.GuildName = "";
            Guard.Model = 33746;
            Guard.Realm = 0;
            Guard.CurrentRegionID = this.CurrentRegionID;
            Guard.Size = 53;
            Guard.Level = (byte)Util.Random(40, 50);
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
            ArxemOxomisCasterGuardBrain brain = new ArxemOxomisCasterGuardBrain();
            brain.AggroLevel = 0;
            brain.AggroRange = 500;
            Guard.SetOwnBrain(brain);
            if (this.MyBaseName == "Zhton Chief Arxem")
            {
                ArxemCasterGuardList.Add(Guard);
            }
            else if (this.MyBaseName == "Kynhroe Chief Oxomis")
            {
                OxomisCasterGuardList.Add(Guard);
            }
            //Def Template
            GameNpcInventoryTemplate template = new GameNpcInventoryTemplate();
            template.AddNPCEquipment(eInventorySlot.TwoHandWeapon, 19);
            template.CloseTemplate();
            Guard.Inventory = template;
            Guard.SwitchWeapon(eActiveWeaponSlot.TwoHanded);
            Guard.AddToWorld();
        }
        public void SpawnHealerGuard(int X, int Y, int Z, ushort H) //Spawn Healer Guard
        {
            ArxemOxomisHealerGuard Guard = new ArxemOxomisHealerGuard();
            if (this.MyBaseName == "Zhton Chief Arxem")
            {
                Guard.Name = "Zhton tide drifter";
            }
            else if (this.MyBaseName == "Kynhroe Chief Oxomis")
            {
                Guard.Name = "Kynhroe tide drifter";
            }
            Guard.GuildName = "";
            Guard.Model = 33746;
            Guard.Realm = 0;
            Guard.CurrentRegionID = this.CurrentRegionID;
            Guard.Size = 53;
            Guard.Level = (byte)Util.Random(40, 50);
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
            ArxemOxomisHealerGuardBrain brain = new ArxemOxomisHealerGuardBrain();
            brain.AggroLevel = 0;
            brain.AggroRange = 500;
            Guard.SetOwnBrain(brain);
            if (this.MyBaseName == "Zhton Chief Arxem")
            {
                ArxemHealerGuardList.Add(Guard);
            }
            else if (this.MyBaseName == "Kynhroe Chief Oxomis")
            {
                OxomisHealerGuardList.Add(Guard);
            }
            //Def Template
            GameNpcInventoryTemplate template = new GameNpcInventoryTemplate();
            int RandRightHand = Util.Random(1, 7);
            if (RandRightHand == 1) template.AddNPCEquipment(eInventorySlot.RightHandWeapon, 333);
            if (RandRightHand == 2) template.AddNPCEquipment(eInventorySlot.RightHandWeapon, 324);
            if (RandRightHand == 3) template.AddNPCEquipment(eInventorySlot.RightHandWeapon, 450);
            if (RandRightHand == 4) template.AddNPCEquipment(eInventorySlot.RightHandWeapon, 451);
            if (RandRightHand == 5) template.AddNPCEquipment(eInventorySlot.RightHandWeapon, 452);
            if (RandRightHand == 6) template.AddNPCEquipment(eInventorySlot.RightHandWeapon, 461);
            if (RandRightHand == 7) template.AddNPCEquipment(eInventorySlot.RightHandWeapon, 578);
            int RandLeftHand = Util.Random(1, 2);
            if (RandLeftHand == 1) template.AddNPCEquipment(eInventorySlot.LeftHandWeapon, 59);
            if (RandLeftHand == 2) template.AddNPCEquipment(eInventorySlot.LeftHandWeapon, 1040);
            template.CloseTemplate();
            Guard.Inventory = template;
            Guard.SwitchWeapon(eActiveWeaponSlot.Standard);
            Guard.AddToWorld();
        }
        public void SpawnUsualGuard(int X, int Y, int Z, ushort H) //Spawn Usual Guard
        {
            ArxemOxomisUsualGuard Guard = new ArxemOxomisUsualGuard();
            if (this.MyBaseName == "Zhton Chief Arxem")
            {
                Guard.Name = "Zhton ambusher";
            }
            else if (this.MyBaseName == "Kynhroe Chief Oxomis")
            {
                Guard.Name = "Kynhroe ambusher";
            }
            Guard.GuildName = "";
            Guard.Model = 33746;
            Guard.Realm = 0;
            Guard.CurrentRegionID = this.CurrentRegionID;
            Guard.Size = 53;
            Guard.Level = (byte)Util.Random(40, 50);
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
            ArxemOxomisUsualGuardBrain brain = new ArxemOxomisUsualGuardBrain();
            brain.AggroLevel = 0;
            brain.AggroRange = 500;
            Guard.SetOwnBrain(brain);
            if (this.MyBaseName == "Zhton Chief Arxem")
            {
                ArxemUsualGuardList.Add(Guard);
            }
            else if (this.MyBaseName == "Kynhroe Chief Oxomis")
            {
                OxomisUsualGuardList.Add(Guard);
            }
            //Def Template
            GameNpcInventoryTemplate template = new GameNpcInventoryTemplate();
            int RandRightHand = Util.Random(1, 5);
            if (RandRightHand == 1)
            {
                template.AddNPCEquipment(eInventorySlot.RightHandWeapon, 444);
                Guard.SwitchWeapon(eActiveWeaponSlot.Standard);
            }
            if (RandRightHand == 2)
            {
                template.AddNPCEquipment(eInventorySlot.RightHandWeapon, 445);
                Guard.SwitchWeapon(eActiveWeaponSlot.Standard);
            }
            if (RandRightHand == 3)
            {
                template.AddNPCEquipment(eInventorySlot.RightHandWeapon, 460);
                Guard.SwitchWeapon(eActiveWeaponSlot.Standard);
            }
            if (RandRightHand == 4)
            {
                template.AddNPCEquipment(eInventorySlot.DistanceWeapon, 471);
                Guard.SwitchWeapon(eActiveWeaponSlot.Distance);
            }
            if (RandRightHand == 5)
            {
                template.AddNPCEquipment(eInventorySlot.DistanceWeapon, 570);
                Guard.SwitchWeapon(eActiveWeaponSlot.Distance);
            }
            template.CloseTemplate();
            Guard.Inventory = template;
            Guard.AddToWorld();
        }

        //-----STATIC-----
        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Warn("Master Level - 1.3 -¨Initializing Event...");
            if (Albion == true)
            {
                SpawnArxem(albregion);
                log.Warn("Master Level - 1.3 -¨Zhton Chief Arxem ALB added.");
                SpawnOxomis(albregion);
                log.Warn("Master Level - 1.3 -¨Zhton Chief Oxomis ALB added.");
            }
            if (Midgard == true)
            {
                SpawnArxem(midregion);
                log.Warn("Master Level - 1.3 -¨Zhton Chief Arxem MID added.");
                SpawnOxomis(midregion);
                log.Warn("Master Level - 1.3 -¨Zhton Chief Oxomis MID added.");
            }
            if (Hibernia == true)
            {
                SpawnArxem(hibregion);
                log.Warn("Master Level - 1.3 -¨Zhton Chief Arxem HIB added.");
                SpawnOxomis(hibregion);
                log.Warn("Master Level - 1.3 -¨Zhton Chief Oxomis HIB added.");
            }
            log.Warn("Master Level - 1.3 -¨Event Initialized !");
        }
        public static void SpawnArxem(int region) //Spawn Arxem
        {
            ArxemOxomis Arxem = new ArxemOxomis();
            Arxem.Name = "Zhton Chief Arxem";
            Arxem.MyBaseName = "Zhton Chief Arxem";
            Arxem.GuildName = "";
            Arxem.Model = 33747;
            Arxem.Realm = 0;
            Arxem.CurrentRegionID = (ushort)region;
            Arxem.Size = 100;
            Arxem.Level = 60;
            Arxem.X = 370102;
            Arxem.Y = 526583;
            Arxem.Z = 6321;
            Arxem.Heading = 2076;
            Arxem.RoamingRange = 0;
            Arxem.CurrentSpeed = 0;
            Arxem.MaxSpeedBase = 170;
            Arxem.AutoSetStats();
            Arxem.RespawnInterval = 5 * 60 * 1000;
            Arxem.BodyType = 0;

            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 50;
            brain.AggroRange = 500;
            Arxem.SetOwnBrain(brain);
            Arxem.AddToWorld();

        }
        public static void SpawnOxomis(int region) //Spawn Oxomis
        {
            ArxemOxomis Oxomis = new ArxemOxomis();
            Oxomis.Name = "Kynhroe Chief Oxomis";
            Oxomis.MyBaseName = "Kynhroe Chief Oxomis";
            Oxomis.GuildName = "";
            Oxomis.Model = 33747;
            Oxomis.Realm = 0;
            Oxomis.CurrentRegionID = (ushort)region;
            Oxomis.Size = 100;
            Oxomis.Level = 60;
            Oxomis.X = 374295;
            Oxomis.Y = 517165;
            Oxomis.Z = 5288;
            Oxomis.Heading = 2000;
            Oxomis.RoamingRange = 0;
            Oxomis.CurrentSpeed = 0;
            Oxomis.MaxSpeedBase = 170;
            Oxomis.AutoSetStats();
            Oxomis.RespawnInterval = 5 * 60 * 1000;
            Oxomis.BodyType = 0;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 50;
            brain.AggroRange = 500;
            Oxomis.SetOwnBrain(brain);
            Oxomis.AddToWorld();
        }

    }
    
    //Basic Class Guard
    public class ArxemOxomisGuard : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;

        //Parent
        public ArxemOxomis parent;

        //Overrides
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

    //Caster Sub-Class Guard
    public class ArxemOxomisCasterGuard : ArxemOxomisGuard
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;

        //Spells
        public Spell BoltSpell;
        public SpellLine BoltSpellLine;

        //Overrides
        public override bool AddToWorld()
        {
            return base.AddToWorld();
        }

        //Spells
        public void InitializeSpells()
        {
            //BoltSpell
            DbSpell spell = new DbSpell();
            spell.AllowAdd = false;
            spell.CastTime = 2;
            spell.ClientEffect = 3296;
            spell.Icon = 3296;
            spell.Duration = 1;
            spell.Value = 1;
            spell.Name = "GuardBolt50";
            spell.Description = "Send a bolt on the target";
            spell.Range = WorldMgr.VISIBILITY_DISTANCE;
            spell.SpellID = 100015;
            spell.Target = "Ennemy";
            spell.Type = "EnduranceRegenBuff";
            BoltSpell = new Spell(spell, 0);
            BoltSpellLine = SkillBase.GetSpellLine(GlobalSpellsLines.Character_Abilities);
        }

    }
    public class ArxemOxomisCasterGuardBrain : StandardMobBrain
    {
        //base
        public ArxemOxomisCasterGuardBrain()
            : base()
        {
            ThinkInterval = 3000;
        }

        //Override
        public override void Think()
        {
            ArxemOxomisCasterGuard BrainParent = Body as ArxemOxomisCasterGuard;
            ushort CastAttackRange = 200;
            int Damage = 0;
            if(BrainParent.TargetObject != null)
            {
                if(BrainParent.TargetObject.Health != 0)
                {
                   //BrainParent.CastSpell(BrainParent.BoltSpell);
                }
            }
            base.Think();
        }

    }

    //Healer Sub-Class Guard
    public class ArxemOxomisHealerGuard : ArxemOxomisGuard
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;

        //Overrides
        public override bool AddToWorld()
        {
            return base.AddToWorld();
        }

    }
    public class ArxemOxomisHealerGuardBrain : StandardMobBrain
    {
        //base
        public ArxemOxomisHealerGuardBrain()
            : base()
        {
            ThinkInterval = 3000;
        }

        //Override
        public override void Think()
        {
            ArxemOxomisCasterGuard BrainParent = Body as ArxemOxomisCasterGuard;
            base.Think();
        }

    }

    //Usual Sub-Class Guard
    public class ArxemOxomisUsualGuard : ArxemOxomisGuard
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;

        //Overrides
        public override bool AddToWorld()
        {
            return base.AddToWorld();
        }

    }
    public class ArxemOxomisUsualGuardBrain : StandardMobBrain
    {
        //base
        public ArxemOxomisUsualGuardBrain()
            : base()
        {
            ThinkInterval = 3000;
        }

        //Override
        public override void Think()
        {
            ArxemOxomisCasterGuard BrainParent = Body as ArxemOxomisCasterGuard;
            base.Think();
        }

    }

}