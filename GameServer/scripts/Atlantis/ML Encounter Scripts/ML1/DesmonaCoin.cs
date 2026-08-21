//---------------------------------------------------------
//---------------------ML1.9 - DesmonaCoin -----------------
//---------------------------------------------------------

using System;
using System.Collections.Generic;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.Database;
using log4net;
using System.Reflection;

namespace DOL.GS.Atlantis
{
    //Desmona Revelation Token - reveals Desmona and her harpies to nearby players
    public class DesmonaCoin : GameInventoryItem
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public static bool debug = true;

        //Desmona island center
        public static readonly int IslandX = 353247;
        public static readonly int IslandY = 637086;
        public static readonly int IslandZ = 8286;

        //Tokens fade away if the player leaves the island
        public static readonly int ExpiryRange = 10000;

        //Interval between expiry checks ( in milliseconds )
        public const int ExpiryCheckInterval = 5000;

        //Mobs within this range are revealed/hidden instantly on coin gain/loss,
        //the rest is handled by the staggered mob visibility timers
        public const int InstantRevealRange = 2000;

        public DesmonaCoin() : base() { }
        public DesmonaCoin(DbItemTemplate template) : base(template) { }
        public DesmonaCoin(DbItemUnique template) : base(template) { }
        public DesmonaCoin(DbInventoryItem item) : base(item) { }

        public override void OnReceive(GamePlayer player)
        {
            base.OnReceive(player);

            //Update the visibility of nearby Desmona mobs
            RefreshMobVisibility(player);

            //Start the expiry check
            new ECSGameTimer(player, new ECSGameTimer.ECSTimerCallback(ExpiryTimerTick), ExpiryCheckInterval);

            if (debug == true) log.Warn("Master Level - 1.9 - " + player.Name + " received a Desmona Revelation Token.");
        }

        public override void OnLose(GamePlayer player)
        {
            base.OnLose(player);

            //Update the visibility of nearby Desmona mobs
            RefreshMobVisibility(player);
        }

        //Tokens automatically disappear if the player leaves the island, releases or logs out
        public int ExpiryTimerTick(ECSGameTimer timer)
        {
            GamePlayer player = timer.Owner as GamePlayer;
            if (player == null || player.ObjectState != GameObject.eObjectState.Active)
                return 0;

            //Nothing to check if the player holds no token anymore
            if (!HasToken(player))
                return 0;

            if (!player.IsAlive || player.CurrentRegionID != 130 || !player.IsWithinRadius(new Point3D(IslandX, IslandY, IslandZ), ExpiryRange))
            {
                List<DbInventoryItem> tokens = new List<DbInventoryItem>();
                foreach (DbInventoryItem item in player.Inventory.AllItems)
                {
                    if (item is DesmonaCoin)
                        tokens.Add(item);
                }
                foreach (DbInventoryItem token in tokens)
                {
                    player.Inventory.RemoveItem(token);
                }
                if (tokens.Count > 0)
                    player.Out.SendMessage("Your Desmona Revelation Tokens fade away...", eChatType.CT_Important, eChatLoc.CL_ChatWindow);
                return 0;
            }

            return ExpiryCheckInterval;
        }

        //Does the player hold a DesmonaCoin ?
        public static bool HasToken(GamePlayer player)
        {
            foreach (DbInventoryItem item in player.Inventory.AllItems)
            {
                if (item is DesmonaCoin)
                    return true;
            }
            return false;
        }

        //Update the visibility of all Desmona mobs near the player
        public static void RefreshMobVisibility(GamePlayer player)
        {
            foreach (GameNPC npc in player.GetNPCsInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                if (npc is DesmonaMob desmonaMob && desmonaMob.IsWithinRadius(player, InstantRevealRange))
                    desmonaMob.ReEvaluateVisibility();
            }
        }
    }
}