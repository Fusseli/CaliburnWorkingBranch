//---------------------------------------------------------
//----------------ML1.4 - [Barrière] Fadrin ---------------
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

//Using Mgr

namespace DOL.GS.Atlantis
{

    //Fadrin
    class Fadrin : GameNPC
    {

        //Log - Debug
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static bool debug = true;

        //Minimum Level
        public static int MinimumLevel = 40;

        //Minimum Respawn Time - Maximum Respawn Time ( in minutes )
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

        //BlockPdv
        public int BaseBlockPdv = 10000;
        public int BlockPdv = 10000;

        //Override
        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            this.RespawnInterval = Util.Random(MinRespawn, MaxRespawn) * 60 * 1000;
            BlockPdv = BaseBlockPdv;
            base.StartRespawn();
        }
        public override void TakeDamage(GameObject source, eDamageType damageType, int damageAmount, int criticalAmount)
        {
            if ((BlockPdv > 0) && (damageType == eDamageType.Body || damageType == eDamageType.Cold || damageType == eDamageType.Crush || damageType == eDamageType.Energy || damageType == eDamageType.Heat || damageType == eDamageType.Matter || damageType == eDamageType.Spirit))
            {
                if (source is GamePlayer)
                {
                    GamePlayer player = source as GamePlayer;
                    BlockPdv = BlockPdv - (damageAmount + criticalAmount);
                    foreach (GamePlayer onlookers in this.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        onlookers.Out.SendMessage("The magical barrier absorb " + (damageAmount + criticalAmount) + " of " + player.Name + "'s Damage !", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        onlookers.Out.SendSpellEffectAnimation(this, this, 11523, 0, false, 1);
                        if (BlockPdv < 1)
                        {
                            foreach (GamePlayer p in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                            {
                                p.Out.SendMessage("The magical barrier fallen !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                            }
                        }
                    }
                    return;
                }
                return;
            }
            base.TakeDamage(source, damageType, damageAmount, criticalAmount);
        }
        public override void Die(GameObject killer)
        {
            //Loot
            MLCreditHelper.GiveItem(killer, this, "ToaManager_Negative_Absolution_Belt", 1, 1);

            base.Die(killer);

            //Credit
            MLCreditHelper.CreditML((byte)1, (byte)4, killer, true, false, (byte)MinimumLevel);

        }

        //Spawn Fadrin
        public static void SpawnFadrin(int region)
        {
            Fadrin FadrinNpc = new Fadrin();
            FadrinNpc.Name = "Fadrin";
            FadrinNpc.GuildName = "";
            FadrinNpc.Model = 1033;
            FadrinNpc.Realm = 0;
            FadrinNpc.CurrentRegionID = (ushort)region;
            FadrinNpc.Size = 50;
            FadrinNpc.Level = 60;
            FadrinNpc.X = 289584;
            FadrinNpc.Y = 555086;
            FadrinNpc.Z = 2135;
            FadrinNpc.Heading = 610;
            FadrinNpc.RoamingRange = 0;
            FadrinNpc.CurrentSpeed = 0;
            FadrinNpc.MaxSpeedBase = 191;
            FadrinNpc.RespawnInterval = 10 * 60 * 1000;
            FadrinNpc.AutoSetStats();
            StandardMobBrain brain = new StandardMobBrain();
            brain.AggroLevel = 100;
            brain.AggroRange = 10;
            FadrinNpc.SetOwnBrain(brain);
            FadrinNpc.AddToWorld();
        }

        //Load Event
        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            log.Warn("Master Level - 1.4 - Initializing Objects ...");
            #region Negative Absolution Belt
            #region Base
            DbItemTemplate belt = (DbItemTemplate)GameServer.Database.FindObjectByKey<DbItemTemplate>("ToaManager_Negative_Absolution_Belt");
            if (belt == null)
            {
                log.Warn("Master Level - 1.4 - Negative Absolution Belt not Found ...");
                DbItemTemplate Negative_Absolution_Belt = new DbItemTemplate();
                Negative_Absolution_Belt.PackageID = "ToaManager001";
                Negative_Absolution_Belt.Id_nb = "ToaManager_Negative_Absolution_Belt";
                Negative_Absolution_Belt.Name = "Negative absolution belt";
                Negative_Absolution_Belt.Level = 30;
                Negative_Absolution_Belt.Durability = 50000;
                Negative_Absolution_Belt.MaxDurability = 50000;
                Negative_Absolution_Belt.Condition = 50000;
                Negative_Absolution_Belt.MaxCondition = 50000;
                Negative_Absolution_Belt.Quality = 85;
                Negative_Absolution_Belt.DPS_AF = 0;
                Negative_Absolution_Belt.SPD_ABS = 0;
                Negative_Absolution_Belt.Hand = 0;
                Negative_Absolution_Belt.Type_Damage = 0;
                Negative_Absolution_Belt.Object_Type = 41;
                Negative_Absolution_Belt.Item_Type = 32;
                Negative_Absolution_Belt.Color = 0;
                Negative_Absolution_Belt.Emblem = 0;
                Negative_Absolution_Belt.Effect = 0;
                Negative_Absolution_Belt.Weight = 2;
                Negative_Absolution_Belt.Model = 597;
                Negative_Absolution_Belt.Extension = 0;
                Negative_Absolution_Belt.Bonus = 0;
                Negative_Absolution_Belt.Bonus1 = 0;
                Negative_Absolution_Belt.Bonus2 = 0;
                Negative_Absolution_Belt.Bonus3 = 0;
                Negative_Absolution_Belt.Bonus4 = 0;
                Negative_Absolution_Belt.Bonus5 = 0;
                Negative_Absolution_Belt.Bonus6 = 0;
                Negative_Absolution_Belt.Bonus7 = 0;
                Negative_Absolution_Belt.Bonus8 = 0;
                Negative_Absolution_Belt.Bonus9 = 0;
                Negative_Absolution_Belt.Bonus10 = 0;
                Negative_Absolution_Belt.ExtraBonus = 0;
                Negative_Absolution_Belt.Bonus1Type = 0;
                Negative_Absolution_Belt.Bonus2Type = 0;
                Negative_Absolution_Belt.Bonus3Type = 0;
                Negative_Absolution_Belt.Bonus4Type = 0;
                Negative_Absolution_Belt.Bonus5Type = 0;
                Negative_Absolution_Belt.Bonus6Type = 0;
                Negative_Absolution_Belt.Bonus7Type = 0;
                Negative_Absolution_Belt.Bonus8Type = 0;
                Negative_Absolution_Belt.Bonus9Type = 0;
                Negative_Absolution_Belt.Bonus10Type = 0;
                Negative_Absolution_Belt.ExtraBonusType = 0;
                Negative_Absolution_Belt.IsPickable = false;
                Negative_Absolution_Belt.IsDropable = true;
                Negative_Absolution_Belt.CanDropAsLoot = false;
                Negative_Absolution_Belt.IsTradable = false;
                Negative_Absolution_Belt.MaxCount = 1;
                Negative_Absolution_Belt.PackSize = 1;
                Negative_Absolution_Belt.Charges = 0;
                Negative_Absolution_Belt.MaxCharges = 0;
                Negative_Absolution_Belt.Charges1 = 0;
                Negative_Absolution_Belt.MaxCharges1 = 0;
                Negative_Absolution_Belt.SpellID = 0;
                Negative_Absolution_Belt.SpellID1 = 0;
                Negative_Absolution_Belt.ProcSpellID = 0;
                Negative_Absolution_Belt.ProcSpellID1 = 0;
                Negative_Absolution_Belt.PoisonSpellID = 0;
                Negative_Absolution_Belt.PoisonMaxCharges = 0;
                Negative_Absolution_Belt.PoisonCharges = 0;
                Negative_Absolution_Belt.Realm = 0;
                Negative_Absolution_Belt.AllowedClasses = "";
                Negative_Absolution_Belt.CanUseEvery = 0;
                //Nedfall_Entrapment_Gem.Flags = 0;
                //Nedfall_Entrapment_Gem.BonusLevel = 0;
                Negative_Absolution_Belt.Description = "";
                //Nedfall_Entrapment_Gem.IsIndestructible = false;
                //Nedfall_Entrapment_Gem.IsNotLosingDur = false;
                //Nedfall_Entrapment_Gem.LevelRequirement = 0;
                Negative_Absolution_Belt.Price = 0;
                //Nedfall_Entrapment_Gem.ProcChance = 0;
                Negative_Absolution_Belt.ClassType = "";
                //Nedfall_Entrapment_Gem.SalvageYieldID = 0;
                GameServer.Database.AddObject(Negative_Absolution_Belt);
                log.Warn("Master Level - 1.4 - Negative Absolution Belt added !");
            }
            #endregion Base
            #region Update1
            //Update1
            #endregion
            #endregion Negative Absolution Belt
            log.Warn("Master Level - 1.4 - Objects Initialized !");
            log.Warn("Master Level - 1.4 - Initializing Event...");
            if (Albion == true)
            {
                SpawnFadrin(albregion);
                log.Warn("Master Level - 1.4 - Fadrin ALB added.");
            }
            if (Midgard == true)
            {
                SpawnFadrin(midregion);
                log.Warn("Master Level - 1.4 - Fadrin MID added.");
            }
            if (Hibernia == true)
            {
                SpawnFadrin(hibregion);
                log.Warn("Master Level - 1.4 - Fadrin HIB added.");
            }
            log.Warn("Master Level - 1.4 - Event Initialized !");
        }

    }

}