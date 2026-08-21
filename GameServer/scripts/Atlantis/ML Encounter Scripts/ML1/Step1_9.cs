//---------------------------------------------------------
//---------------------ML1.9 - Desmona's Harpies ----------
//-------------------Based on : Hibernos scripts ----------
//---------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.Database;
using log4net;
using System.Reflection;

namespace DOL.GS.Atlantis
{

    //Base class for Desmona mobs - invisible until a player holding a DesmonaCoin is near
    public abstract class DesmonaMob : GameNPC
    {

        //Log - Debug
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public static bool debug = true;

        //Invisible model used by this server for hidden npcs
        public const ushort InvisibleModel = 665;

        //Interval between visibility refreshes ( in milliseconds )
        public const int VisibilityCheckInterval = 2500;

        //Real model shown when a player with a token is near
        public abstract ushort RealModel { get; }

        //Overrides
        public override bool AddToWorld() //AddToWorld
        {
            if (!base.AddToWorld())
                return false;

            //Start the visibility refresh timer with a random phase
            //so model changes are staggered and don't hit the clients all at once
            new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(VisibilityCheckTimerTick), Util.Random(0, VisibilityCheckInterval));
            return true;
        }

        //Visibility Refresh Timer
        public int VisibilityCheckTimerTick(ECSGameTimer timer)
        {
            ReEvaluateVisibility();
            return VisibilityCheckInterval;
        }

        //Show the real model while any nearby player (or group/battlegroup member) holds a DesmonaCoin
        public void ReEvaluateVisibility()
        {
            if (ObjectState != eObjectState.Active)
                return;

            ushort newModel = PlayerWithTokenNearby() ? RealModel : InvisibleModel;
            if (Model != newModel)
                Model = newModel;
        }

        //Is any player near enough to reveal this mob ?
        public bool PlayerWithTokenNearby()
        {
            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (player == null)
                    continue;

                if (DesmonaCoin.HasToken(player))
                    return true;

                //Group Support
                if (player.Group != null)
                {
                    foreach (GamePlayer groupMember in player.Group.GetPlayersInTheGroup())
                    {
                        if (groupMember != null && DesmonaCoin.HasToken(groupMember))
                            return true;
                    }
                }

                //Battlegroup Support
                BattleGroup battleGroup = player.TempProperties.GetProperty<BattleGroup>(BattleGroup.BATTLEGROUP_PROPERTY, null);
                if (battleGroup != null)
                {
                    foreach (GamePlayer bgMember in battleGroup.Members.Keys)
                    {
                        if (bgMember != null && DesmonaCoin.HasToken(bgMember))
                            return true;
                    }
                }
            }
            return false;
        }

    }

    //Desmona Class - Boss of the Encounter
    public class Desmona : DesmonaMob
    {

        //Minimum Level
        public static int MinimumLevel = 40;

        //Real model
        public override ushort RealModel { get { return 992; } }

        //Overrides
        public override void Die(GameObject killer) //Die
        {
            //Loot
            MLCreditHelper.GiveItem(killer, this, "ToaManager_Desmona_Crown", 1, 1);

            //Credit
            MLCreditHelper.CreditML((byte)1, (byte)9, killer, true, false, (byte)MinimumLevel);

            //Broadcast
            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (player != null)
                    player.Out.SendMessage("Desmona has been defeated !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
            }

            //LOG
            if (debug == true) log.Warn("Master Level - 1.9 - Desmona Die.");

            base.Die(killer);
        }

    }

    //Desmona Harpy Class - Steals DesmonaCoins from players
    public class DesmonaHarpy : DesmonaMob
    {

        private ushort m_realModel;

        //Chance to steal a token on hit ( in percent )
        public const int StealChance = 10;

        //Real model
        public override ushort RealModel { get { return m_realModel; } }

        //Overrides
        public override bool AddToWorld() //AddToWorld
        {
            //Livelike models for this mob are 990-992
            m_realModel = (ushort)Util.Random(990, 992);
            return base.AddToWorld();
        }

        public override void OnAttackEnemy(AttackData ad) //OnAttackEnemy
        {
            if (ad.Target is GamePlayer player)
            {
                if (Util.Chance(StealChance))
                {
                    StealToken(player);
                }
            }

            base.OnAttackEnemy(ad);
        }

        //Steal a DesmonaCoin from the player
        public void StealToken(GamePlayer player)
        {
            foreach (DbInventoryItem item in player.Inventory.AllItems)
            {
                if (item is DesmonaCoin)
                {
                    player.Inventory.RemoveItem(item);
                    player.Out.SendMessage(Name + " stole your " + item.Name + "!", eChatType.CT_Emote, eChatLoc.CL_ChatWindow);
                    return;
                }
            }
        }

    }

}