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

        //Morphed guard list
        public List<MorphedGuard> MorphedGuardList = new List<MorphedGuard>();

        //Barrier re-raise timer
        private ECSGameTimer m_barrierTimer;

        //Barrier visual timer
        private ECSGameTimer m_visualTimer;

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
            if (BlockPdv > 0)
            {
                bool isMelee = damageType == eDamageType.Slash || damageType == eDamageType.Thrust || damageType == eDamageType.Crush || damageType == eDamageType.Natural;
                string attackerName = source != null ? source.Name : "Someone";

                if (isMelee)
                {
                    int totalDamage = damageAmount + criticalAmount;
                    BlockPdv -= totalDamage;
                    if (BlockPdv < 0) BlockPdv = 0;

                    GamePlayer attackerPlayer = source as GamePlayer;
                    if (attackerPlayer != null)
                    {
                        attackerPlayer.Out.SendMessage("Your blow damages the magical barrier for " + totalDamage + " damage! (" + BlockPdv + " barrier HP remaining)", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                }
                foreach (GamePlayer player in GetPlayersInRadius(1500))
                {
                    if (isMelee)
                    {
                        if (player != source as GamePlayer)
                            player.Out.SendMessage(attackerName + " strikes the barrier for " + (damageAmount + criticalAmount) + " damage!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else
                    {
                        player.Out.SendMessage("The magical barrier absorbs " + attackerName + "'s attack!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    player.Out.SendSpellEffectAnimation(this, this, 11523, 0, false, 1);
                }
                if (BlockPdv < 1)
                {
                    foreach (GamePlayer p in GetPlayersInRadius(1500))
                    {
                        p.Out.SendMessage("The magical barrier falls!", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                    }
                    this.MaxSpeedBase = 191;
                    if (m_visualTimer != null)
                    {
                        m_visualTimer.Stop();
                        m_visualTimer = null;
                    }
                    if (m_barrierTimer == null)
                        m_barrierTimer = new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(RaiseBarrier), 45 * 1000);
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

            //Clean up timers and guards
            if (m_barrierTimer != null)
            {
                m_barrierTimer.Stop();
                m_barrierTimer = null;
            }
            if (m_visualTimer != null)
            {
                m_visualTimer.Stop();
                m_visualTimer = null;
            }
            RemoveGuards();

        }
        public override bool AddToWorld()
        {
            MaxSpeedBase = 0;
            SpawnGuards();
            StartBarrierVisual();
            return base.AddToWorld();
        }
        public void StartBarrierVisual()
        {
            if (m_visualTimer != null)
            {
                m_visualTimer.Stop();
                m_visualTimer = null;
            }
            m_visualTimer = new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(BarrierVisualTick), 1 * 1000);
        }
        public int BarrierVisualTick(ECSGameTimer timer)
        {
            if (!IsAlive || BlockPdv < 1)
            {
                m_visualTimer = null;
                return 0;
            }
            foreach (GamePlayer player in GetPlayersInRadius(1500))
            {
                player.Out.SendSpellEffectAnimation(this, this, 11523, 0, false, 1);
            }
            m_visualTimer = new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(BarrierVisualTick), 3 * 1000);
            return 0;
        }
        public void SpawnGuards()
        {
            int[,] guardPositions = {
                { -250, -250 },
                { 250, -250 },
                { -250, 250 },
                { 250, 250 },
                { -350, 0 },
                { 350, 0 }
            };
            for (int i = 0; i < guardPositions.GetLength(0); i++)
            {
                SpawnGuard(guardPositions[i, 0], guardPositions[i, 1]);
            }
        }
        public void SpawnGuard(int offsetX, int offsetY)
        {
            MorphedGuard guard = new MorphedGuard();
            guard.Name = "morphed creature";
            guard.Model = 408;
            guard.Realm = 0;
            guard.CurrentRegionID = this.CurrentRegionID;
            guard.Size = 50;
            guard.Level = (byte)Util.Random(45, 50);
            guard.X = this.X + offsetX;
            guard.Y = this.Y + offsetY;
            guard.Z = this.Z;
            guard.Heading = this.Heading;
            guard.RoamingRange = 300;
            guard.CurrentSpeed = 0;
            guard.MaxSpeedBase = 170;
            guard.AutoSetStats();
            guard.RespawnInterval = 5 * 60 * 1000;
            guard.BodyType = 0;
            MorphedGuardBrain brain = new MorphedGuardBrain();
            brain.AggroLevel = 50;
            brain.AggroRange = 400;
            guard.SetOwnBrain(brain);
            guard.Flags |= eFlags.SWIMMING;
            guard.AddToWorld();
            MorphedGuardList.Add(guard);
        }
        public void RemoveGuards()
        {
            foreach (MorphedGuard guard in MorphedGuardList)
            {
                guard.RemoveFromWorld();
            }
            MorphedGuardList.Clear();
        }

        public int RaiseBarrier(ECSGameTimer timer)
        {
            m_barrierTimer = null;
            BlockPdv = BaseBlockPdv;
            MaxSpeedBase = 0;
            StartBarrierVisual();
            foreach (GamePlayer p in GetPlayersInRadius(1500))
            {
                p.Out.SendMessage("Fadrin raises his barrier again!", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
            }
            return 0;
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
            FadrinNpc.Flags |= eFlags.SWIMMING;
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

    //Morphed creature guard
    class MorphedGuard : GameNPC
    {
        public override void SaveIntoDatabase()
        {
        }
        public override void StartRespawn()
        {
            base.StartRespawn();
        }
    }

    class MorphedGuardBrain : StandardMobBrain
    {
        public MorphedGuardBrain()
            : base()
        {
            AggroLevel = 50;
            AggroRange = 400;
            ThinkInterval = 3000;
        }
    }

}