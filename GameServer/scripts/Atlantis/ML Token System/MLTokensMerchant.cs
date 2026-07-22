using DOL.AI.Brain;
using DOL.Database;
using DOL.Events;
using DOL.GS.PacketHandler;
using log4net;
using System;
using System.Reflection;

namespace DOL.GS
{
    public class MLTokensMerchant : GameBountyMerchant
    {
        public MLTokensMerchant() : base()
        {
            SetOwnBrain(new BlankBrain());
        }

        [ScriptLoadedEvent]
        public static void OnScriptCompiled(DOLEvent e, object sender, EventArgs args)
        {
            SpawnMerchant("Albion ML Token Merchant", eRealm.Albion, 70, 577920, 533210, 7295, 3731, 6);
            SpawnMerchant("Midgard ML Token Merchant", eRealm.Midgard, 71, 565715, 569485, 7255, 708, 217);
            SpawnMerchant("Hibernia ML Token Merchant", eRealm.Hibernia, 72, 552275, 576350, 6767, 1074, 389);
        }

        private static void SpawnMerchant(string name, eRealm realm, ushort region, int x, int y, int z, ushort heading, ushort model)
        {
            GameNPC[] npcs = WorldMgr.GetNPCsByName(name, realm);
            if (npcs.Length > 0)
                return;

            MLTokensMerchant npc = new MLTokensMerchant();
            npc.Model = model;
            npc.Name = name;
            npc.GuildName = "ML Bounty Merchant";
            npc.Realm = realm;
            npc.CurrentRegionID = region;
            npc.Size = 50;
            npc.Level = 50;
            npc.MaxSpeedBase = 0;
            npc.X = x;
            npc.Y = y;
            npc.Z = z;
            npc.Heading = heading;
            npc.Inventory = new GameNpcInventoryTemplate();
            npc.TradeItems = new MerchantTradeItems("mltokens");
            npc.AddToWorld();
            npc.SaveIntoDatabase();
        }

        [ScriptUnloadedEvent]
        public static void OnScriptUnloaded(DOLEvent e, object sender, EventArgs args)
        {
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            SayTo(player, eChatLoc.CL_PopupWindow, "Welcome! I trade in Master Level tokens. Purchase a token and give it back to me to claim your credit.");
            return true;
        }

        public override bool ReceiveItem(GameLiving source, DbInventoryItem item)
        {
            GamePlayer player = source as GamePlayer;
            if (player == null || item == null)
                return base.ReceiveItem(source, item);

            if (item.Id_nb.StartsWith("ml") && item.Id_nb.EndsWith("token"))
            {
                int ml = 0;
                string numPart = item.Id_nb.Replace("ml", "").Replace("token", "");
                if (int.TryParse(numPart, out ml) && ml >= 1 && ml <= 10)
                {
                    if (!player.MLGranted)
                    {
                        player.Out.SendMessage("You must speak with the Arbiter first to begin your trials!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return false;
                    }

                    if (player.MLLevel != ml - 1)
                    {
                        player.Out.SendMessage("You must complete all previous Master Levels first!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return false;
                    }

                    player.Inventory.RemoveItem(item);
                    byte steps = player.GetStepCountForML((byte)ml);
                    for (byte s = 1; s <= steps; s++)
                        player.SetFinishedMLStep(ml, s);
                    player.Out.SendMessage("Congratulations, Master Level " + ml + " completed via token!", eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
                    player.Out.SendMasterLevelWindow((byte)ml);
                    player.Out.SendMessage("Return to the Arbiter for your promotion!", eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
                    player.SaveIntoDatabase();
                    return true;
                }
            }

            return base.ReceiveItem(source, item);
        }
    }
}