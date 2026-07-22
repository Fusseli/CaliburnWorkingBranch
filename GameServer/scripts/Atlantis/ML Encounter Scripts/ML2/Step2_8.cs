//---------------------------------------------------------
//-------------------ML2.8 - Amenemhat --------------------
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


    //AmenemhatController
    public class AmenemhatController : GameNPC
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

        //Config
        public static int MaxTimeDuelMn = 10; //Max time to finish the duel

        //NPC Pointer
        Amenemhat CurrentAmenemhat;

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

            //Spawn Amenemhat
            if (this.CurrentRegionID == albregion)
            {
                SpawnAmenemhat((ushort)albregion);
                log.Warn("Master Level - 2.8 - Amenemhat ALB added!");
            }
            if (this.CurrentRegionID == midregion)
            {
                SpawnAmenemhat((ushort)midregion);
                log.Warn("Master Level - 2.8 - Amenemhat MID added!");
            }
            if (this.CurrentRegionID == hibregion)
            {
                SpawnAmenemhat((ushort)hibregion);
                log.Warn("Master Level - 2.8 - Amenemhat HIB added!");
            }

            return base.AddToWorld();
        }
        public override bool Interact(GamePlayer player)
        {
            if (base.Interact(player))
            {
                TurnTo(player, 1500);

                return true;
            }

            return false;
        }

        //Spawn Amenemhat
        public void SpawnAmenemhat(ushort Region)
        {
            Amenemhat AmenemhatNPC = new Amenemhat();
            AmenemhatNPC.Name = "Amenemhat";
            AmenemhatNPC.GuildName = "";
            AmenemhatNPC.Realm = 0;
            AmenemhatNPC.CurrentRegionID = Region;
            AmenemhatNPC.X = 27402;
            AmenemhatNPC.Y = 32246;
            AmenemhatNPC.Z = 15989;
            AmenemhatNPC.Heading = 2013;
            AmenemhatNPC.RoamingRange = 0;
            AmenemhatNPC.CurrentSpeed = 0;
            AmenemhatNPC.MaxSpeedBase = 191;
            AmenemhatNPC.AutoSetStats();
            AmenemhatNPC.BodyType = 0;
            AmenemhatNPC.Model = 1034;
            AmenemhatNPC.Size = 50;
            AmenemhatNPC.Level = 50;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 40;
            brain.AggroRange = 350;
            AmenemhatNPC.SetOwnBrain(brain);
            AmenemhatNPC.Flags |= eFlags.PEACE;
            if (debug == true) AmenemhatNPC.debug = true;
            AmenemhatNPC.AddToWorld();
            CurrentAmenemhat = AmenemhatNPC;
        }

        //STATIC - Load Event
        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Warn("Master Level - 2.8 - Initializing Objects ...");
            log.Warn("Master Level - 2.8 - Objects Initialized !");
            log.Warn("Master Level - 2.8 - Initializing Event...");
            if (Albion == true)
            {
                SpawnAmenemhatController(albregion);
                if (debug == true) log.Warn("Master Level - 2.8 - AmenemhatController ALB added.");
            }
            if (Midgard == true)
            {
                SpawnAmenemhatController(midregion);
                if (debug == true) log.Warn("Master Level - 2.8 - AmenemhatController MID added.");
            }
            if (Hibernia == true)
            {
                SpawnAmenemhatController(hibregion);
                if (debug == true) log.Warn("Master Level - 2.8 - AmenemhatController HIB added.");
            }
            log.Warn("Master Level - 2.8 - Event Initialized !");
        }
        public static void SpawnAmenemhatController(int region)
        {
            AmenemhatController AmenemhatControllerNpc = new AmenemhatController();
            AmenemhatControllerNpc.Name = "2.8 - Controller";
            AmenemhatControllerNpc.GuildName = "ToaManager";
            AmenemhatControllerNpc.Realm = eRealm.None;
            AmenemhatControllerNpc.Model = 665;
            AmenemhatControllerNpc.CurrentRegionID = (ushort)region;
            AmenemhatControllerNpc.Size = 50;
            AmenemhatControllerNpc.Level = 100;
            AmenemhatControllerNpc.X = 28091;
            AmenemhatControllerNpc.Y = 31851;
            AmenemhatControllerNpc.Z = 15976;
            AmenemhatControllerNpc.Heading = 1086;
            AmenemhatControllerNpc.RoamingRange = 0;
            AmenemhatControllerNpc.CurrentSpeed = 0;
            AmenemhatControllerNpc.MaxSpeedBase = 191;
            AmenemhatControllerNpc.Flags |= eFlags.PEACE;
            AmenemhatControllerNpc.Flags |= eFlags.CANTTARGET;
            AmenemhatControllerNpc.RespawnInterval = 10 * 60 * 1000;
            AmenemhatControllerNpc.AddToWorld();
        }

    }

    //Amenemhat
    public class Amenemhat : GameNPC
    {

        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;

        //Other
        public bool EncounterAvailable = true;

        //NPC
        public List<AmenemhatRadiusController> AmenemhatRadiusControllerList = new List<AmenemhatRadiusController>();
        public AmenemhatFighter TheCurrentAmenemhatFighter;
        public GamePlayer Challenger;

        //CroCro Array
        public int[,] CroCroArray = {
			{26766,33013,15900,3217},
			{27172,33229,15485,2048},
			{26888,33418,15549,641},
			{27833,33040,15549,3584},
			{27409,33227,15571,2560},
            {27938,32726,15900,3851},
            {27969,33429,15900,4048},
            {27176,33744,15900,1802},
            {28267,33219,15900,3195},
		};

        //Override
        public override void SaveIntoDatabase() //Disable SaveInDB
        {
        }
        public override void StartRespawn() //Respawn
        {
            base.StartRespawn();
        }
        public override bool AddToWorld() //AddToWorld
        {

            //Spawn CroCro
            for (int i = 0; i < 9; i++)
            {
                SpawnCroCro(CroCroArray[i, 0], CroCroArray[i, 1], CroCroArray[i, 2], (ushort)CroCroArray[i, 3]);
            }
            if (this.CurrentRegionID == AmenemhatController.albregion)
            {
                log.Warn("Master Level - 2.8 - Crocodiles ALB added.");
            }
            else if (this.CurrentRegionID == AmenemhatController.midregion)
            {
                log.Warn("Master Level - 2.8 - Crocodiles MID added.");
            }
            else if (this.CurrentRegionID == AmenemhatController.hibregion)
            {
                log.Warn("Master Level - 2.8 - Crocodiles HIB added.");
            }
            //Spawn Radius
            SpawnAmenemhatRadiusController(27407, 33166, 18007);
            SpawnAmenemhatRadiusController(27407, 33166, 19007);
            SpawnAmenemhatRadiusController(27407, 33166, 20007);
            if (this.CurrentRegionID == AmenemhatController.albregion)
            {
                log.Warn("Master Level - 2.8 - RadiusController ALB added.");
            }
            else if (this.CurrentRegionID == AmenemhatController.midregion)
            {
                log.Warn("Master Level - 2.8 - RadiusController MID added.");
            }
            else if (this.CurrentRegionID == AmenemhatController.hibregion)
            {
                log.Warn("Master Level - 2.8 - RadiusController HIB added.");
            }

            return base.AddToWorld();
        }
        public override bool Interact(GamePlayer player) //Interact
        {
            TurnTo(player, 100);
            this.TargetObject = player;

            if (!base.Interact(player)) return false;
            if (((player.MLGranted == false) || (player.MLLevel != 1) || (player.Level < AmenemhatController.MinimumLevel) || (player.HasFinishedMLStep(2, 8) == true)) && (debug == false)) return true;

            player.Out.SendMessage("Well, "+ player.RaceName +",I hope you did not have too many problems finding your way to me."
                    +" I am Amenemhat and i am here to offer a [duel] to those who truly wish to proceed to battle the Imposter."
                    +" This is a one-on-one duel."
                    +" Either you will be powerful enough to defeat me and continue on or you will lose."
                    +" But i must warn you,if you cannot defeat me,you should not even attempt to adventure any further.", eChatType.CT_Say, eChatLoc.CL_PopupWindow);

            foreach (GamePlayer onlookers in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (onlookers != player)
                {
                    onlookers.Out.SendMessage(this.Name + " speaks to " + player.Name + " .", eChatType.CT_Chat, eChatLoc.CL_ChatWindow);
                }
            }

            return true;
        }
        public override bool WhisperReceive(GameLiving player, string str) //WhisperReceive
        {
            GamePlayer t = (GamePlayer)player;
            if (((t.MLGranted == false) || (t.MLLevel != 1) || (t.Level < AmenemhatController.MinimumLevel) || (t.HasFinishedMLStep(2, 8) == true)) && (debug == false)) return true;
            
                switch (str)
                {

                    case "duel":
                        {
                            t.Out.SendMessage("Behind me is a platform on wich you and you alone will [compete]."
                                +" Anyone else who attempts to follow you up, will find themselves in more danger than you will be in."
                                +" And they would be foolish to wait up there for you.", eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        }
                        break;

                    case "compete":
                        {
                            t.Out.SendMessage("The battle should not take long."
                                +" You will fight atop the platform and either die up there,or return here as the victor."
                                +" You need only to [accept] or [decline] my offer.", eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        }
                        break;

                    case "accept":
                        {
                            AcceptDuel(t);
                        }
                        break;

                    case "decline":
                        {
                            t.Out.SendMessage("", eChatType.CT_Say, eChatLoc.CL_PopupWindow);
                        }
                        break;
            }

            return true;
        }

        //Voids
        public void AcceptDuel(GamePlayer ThePlayer) //Player Accept Duel
        {

            if (EncounterAvailable == false) return;

            //Set Challenger
            Challenger = ThePlayer;

            //Msg
            ThePlayer.Out.SendMessage("Your opponent will be awaiting your arrival."
                +" All you need do is follow the path that leads to the top."
                +" Remember, if you fail, another must have an attempt before you will be able to try again."
                +" You have 5minutes to reach the top. Good luck to you." + ThePlayer.CharacterClass.Name + ".", eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            foreach (GamePlayer onlookers in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                onlookers.Out.SendMessage(ThePlayer.Name + " accepted my challenge ! Everyone must leave stairs !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
            }

            //Cast Teleport
            DbSpell spell = new DbSpell();
            spell.AllowAdd = false;
            spell.CastTime = 2;
            spell.ClientEffect = 321;
            spell.Icon = 321;
            spell.Duration = 0; //Duration = 65535
            spell.Value = 0;
            spell.Name = "Dexterity Buff";
            spell.Description = "test";
            spell.Range = WorldMgr.VISIBILITY_DISTANCE;
            spell.Target = "Self";
            spell.Type = "DexterityBuff";
            spell.Message1 = null;
            spell.Message2 = null;
            spell.Message3 = null;
            spell.Message4 = null;
            this.CastSpell(new Spell(spell, 0), new SpellLine("NPCSpell", "NPC Spell", "none", false), false);

            //Timer to Launch AmenemhatFighter Pop
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(DuelLaunch), (3 * 1000));

            //log
            if (debug == true) log.Warn("Master Level - 2.8 - " + ThePlayer.Name + " started encounter !");

        }
        public void Available(bool value,bool die) //Set Available
        {

            if (value == true && EncounterAvailable == false)
            {
                if (TheCurrentAmenemhatFighter != null)
                {
                    if (die == true) TheCurrentAmenemhatFighter.Health = 0;
                    TheCurrentAmenemhatFighter.Delete();
                }
                this.Model = 1034;
                this.Flags = eFlags.PEACE;
                EncounterAvailable = true;
            }
            else if (value == false && EncounterAvailable == true)
            {
                this.Model = 665;
                this.Flags = eFlags.PEACE;
                this.Flags |= eFlags.CANTTARGET;
                EncounterAvailable = false;
            }

        }

        //Timer
        public int DuelLaunch(ECSGameTimer timer)
        {
            SpawnAmenemhatFighter(Challenger);
            Available(false,false);
            return 0;
        }

        //Spawns
        public void SpawnCroCro(int X, int Y, int Z, ushort H)  //Spawn CroCro
        {
            AmenemhatCroCro AmenemhatCroCroNPC = new AmenemhatCroCro();
            AmenemhatCroCroNPC.Name = "water crocodile";
            AmenemhatCroCroNPC.GuildName = "";
            AmenemhatCroCroNPC.Realm = 0;
            AmenemhatCroCroNPC.CurrentRegionID = this.CurrentRegionID;
            AmenemhatCroCroNPC.X = X;
            AmenemhatCroCroNPC.Y = Y;
            AmenemhatCroCroNPC.Z = Z;
            AmenemhatCroCroNPC.Heading = H;
            AmenemhatCroCroNPC.RoamingRange = 300;
            AmenemhatCroCroNPC.CurrentSpeed = 0;
            AmenemhatCroCroNPC.MaxSpeedBase = 191;
            AmenemhatCroCroNPC.AutoSetStats();
            AmenemhatCroCroNPC.BodyType = 0;
            AmenemhatCroCroNPC.Model = 34026;
            AmenemhatCroCroNPC.Size = 125;
            AmenemhatCroCroNPC.Level = 73;
            AmenemhatCroCroNPC.RespawnInterval = 5 * 60 * 1000;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 350;
            AmenemhatCroCroNPC.SetOwnBrain(brain);
            AmenemhatCroCroNPC.AddToWorld();
        }
        public void SpawnAmenemhatFighter(GamePlayer TheChallenger)  //Spawn AmenemhatFighter
        {
            AmenemhatFighter AmenemhatNPC = new AmenemhatFighter();
            AmenemhatNPC.Name = "Amenemhat";
            AmenemhatNPC.GuildName = "";
            AmenemhatNPC.Realm = 0;
            AmenemhatNPC.CurrentRegionID = this.CurrentRegionID;
            AmenemhatNPC.X = 27400;
            AmenemhatNPC.Y = 33175;
            AmenemhatNPC.Z = 18333;
            AmenemhatNPC.Heading = 2064;
            AmenemhatNPC.RoamingRange = 0;
            AmenemhatNPC.CurrentSpeed = 0;
            AmenemhatNPC.MaxSpeedBase = 191;
            AmenemhatNPC.AutoSetStats();
            AmenemhatNPC.BodyType = 0;
            AmenemhatNPC.Model = 1034;
            AmenemhatNPC.Size = 50;
            AmenemhatNPC.Level = TheChallenger.Level;
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 40;
            brain.AggroRange = 350;
            AmenemhatNPC.SetOwnBrain(brain);
            AmenemhatNPC.Flags |= eFlags.PEACE;
            AmenemhatNPC.Parent = this;
            if (debug == true) AmenemhatNPC.debug = true;
            TheCurrentAmenemhatFighter = AmenemhatNPC;
            AmenemhatNPC.AddToWorld();
        }
        public void SpawnAmenemhatRadiusController(int X, int Y, int Z)
        {
            AmenemhatRadiusController Stone = new AmenemhatRadiusController();
            Stone.Name = "Radius Controller";
            Stone.GuildName = "";
            Stone.Model = 665;
            Stone.Realm = 0;
            Stone.CurrentRegionID = this.CurrentRegionID;
            Stone.Size = 62;
            Stone.Level = 70;
            Stone.X = X;
            Stone.Y = Y;
            Stone.Z = Z;
            Stone.Heading = (ushort)Util.Random(200, 3000);
            Stone.RoamingRange = 0;
            Stone.CurrentSpeed = 0;
            Stone.MaxSpeedBase = 191;
            Stone.AutoSetStats();
            Stone.RespawnInterval = 15 * 60 * 1000;
            Stone.BodyType = 0;
            Stone.Flags |= eFlags.PEACE;
            Stone.Flags |= eFlags.CANTTARGET;
            Stone.Flags |= eFlags.FLYING;
            Stone.Parent = this;
            AmenemhatRadiusControllerList.Add(Stone);
            Stone.AddToWorld();
        }
    }

    //AmenemhatFighter
    public class AmenemhatFighter : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;

        //Parent
        public Amenemhat Parent;
        public bool FightStarted = false;

        //Overrides
        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
        }
        public override bool AddToWorld()
        {

            //Launch TalkTimeElapsed Timer
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(TalkTimeElapsed), (5 * 60 * 1000));

            return base.AddToWorld();
        }
        public override bool Interact(GamePlayer player) //Interact
        {
            TurnTo(player, 100);
            this.TargetObject = player;

            if (!base.Interact(player)) return false;

            if (Parent.Challenger == player)
            {
                player.Out.SendMessage("Are you [ready] ?", eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            }
            foreach (GamePlayer onlookers in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (onlookers != player)
                {
                    onlookers.Out.SendMessage(this.Name + " speaks to " + player.Name + " .", eChatType.CT_Chat, eChatLoc.CL_ChatWindow);
                }
            }

            return true;
        }
        public override bool WhisperReceive(GameLiving player, string str) //WhisperReceive
        {
            GamePlayer t = (GamePlayer)player;
                switch (str)
                {

                    case "ready":
                        {

                            //Set Mob
                            this.Flags = 0;
                            this.StartAttack(t);
                            FightStarted = true;

                            //Launch TalkTimeElapsed Timer
                            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(FightTimeElapsed), (AmenemhatController.MaxTimeDuelMn * 60 * 1000));

                        }
                        break;
                }

            return true;
        }
        public override void Die(GameObject killer)
        {

            //Credit
            MLCreditHelper.CreditML((byte)2, (byte)8, killer, false, false, (byte)AmenemhatController.MinimumLevel);

            //Reload Encounter
            Parent.Available(true,true);

            base.Die(killer);
        }
        public override void TakeDamage(GameObject source, eDamageType damageType, int damageAmount, int criticalAmount)
        {

            if (source is GamePlayer)
            {
                if (source != Parent.Challenger) return;
            }
            else if (source is GameNPC npcSource && npcSource.Brain is IControlledBrain controlledBrain)
            {
                if (controlledBrain.GetPlayerOwner() != Parent.Challenger) return;
            }

            base.TakeDamage(source, damageType, damageAmount, criticalAmount);
        }

        //TalkTime Elapsed Timer
        public int TalkTimeElapsed(ECSGameTimer timer)
        {
            if (FightStarted == true) return 0;
            if (debug == true) log.Warn("Master Level - 2.8 - TalkTime Elapsed !");
            Parent.Available(true,false);
            return 0;
        }

        //FightTime Elapsed Timer
        public int FightTimeElapsed(ECSGameTimer timer)
        {
            if (debug == true) log.Warn("Master Level - 2.8 - FightTime Elapsed !");
            Parent.Available(true,false);
            return 0;
        }

    }

    //StoneController
    public class AmenemhatRadiusController : GameNPC
    {
        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool debug = false;
        public Amenemhat Parent;
        public bool PlayerOnMe = false;

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
            //Start Timer
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(CheckState), (1 * 1000));

            return base.AddToWorld();
        }
        public override void Die(GameObject killer)
        {
            base.Die(killer);
        }

        //Check State
        public int CheckState(ECSGameTimer timer)
        {
            foreach (GamePlayer player in GetPlayersInRadius(1750))
            {
                if ((player.IsAlive == true) && (Parent.EncounterAvailable == false) && (player != Parent.Challenger))
                {
                    if (Parent.TheCurrentAmenemhatFighter.FightStarted == true)
                    {
                        GameLocation TP = new GameLocation(null, this.CurrentRegionID, 27397, 33224, 15971, 3);
                        player.MoveTo(TP);
                        player.Out.SendMessage("Amenemhat teleport you !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                    }
                }
            }
            foreach (GamePlayer pl in GetPlayersInRadius(1750))
            {
                IControlledBrain brain = pl.ControlledBrain;
                if (brain != null)
                {
                    GameNPC pet = brain.Body;
                    if (pet != null && brain.GetPlayerOwner() != null && brain.GetPlayerOwner().IsAlive && Parent.EncounterAvailable == false && brain.GetPlayerOwner() != Parent.Challenger)
                    {
                        if (Parent.TheCurrentAmenemhatFighter.FightStarted)
                        {
                            GameLocation TP = new GameLocation(null, CurrentRegionID, 27397, 33224, 15971, 3);
                            pet.MoveTo(TP);
                            brain.GetPlayerOwner().Out.SendMessage("Amenemhat teleport " + pet.Name + " !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                        }
                    }
                }
            }

            //Reload Timer
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(CheckState), (1 * 500));
            return 0;

        }

    }

    //AmenemhatCroCro
    public class AmenemhatCroCro : GameNPC
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

}
