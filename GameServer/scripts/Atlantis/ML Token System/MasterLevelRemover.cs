using DOL.Database;
using DOL.Events;
using DOL.GS.PacketHandler;
using System;
using System.Reflection;

namespace DOL.GS.Scripts
{
    public class MLRespecNPC : GameNPC
    {
        [ScriptLoadedEvent]
        public static void OnScriptCompiled(DOLEvent e, object sender, EventArgs args)
        {
            SpawnRespec("Albion ML Respec", eRealm.Albion, 70, 577900, 533190, 7295, 3731);
            SpawnRespec("Midgard ML Respec", eRealm.Midgard, 71, 565695, 569465, 7255, 708);
            SpawnRespec("Hibernia ML Respec", eRealm.Hibernia, 72, 552255, 576330, 6767, 1074);
        }

        private static void SpawnRespec(string name, eRealm realm, ushort region, int x, int y, int z, ushort heading)
        {
            GameNPC[] npcs = WorldMgr.GetNPCsByName(name, realm);
            if (npcs.Length > 0)
                return;

            MLRespecNPC npc = new MLRespecNPC();
            npc.Model = 50;
            npc.Name = name;
            npc.GuildName = "Master Level Respec";
            npc.Realm = realm;
            npc.CurrentRegionID = region;
            npc.Size = 50;
            npc.Level = 50;
            npc.MaxSpeedBase = 0;
            npc.X = x;
            npc.Y = y;
            npc.Z = z;
            npc.Heading = heading;
            npc.Flags = eFlags.PEACE;
            npc.AddToWorld();
            npc.SaveIntoDatabase();
        }

        public override bool AddToWorld()
        {
            GameNpcInventoryTemplate template = new GameNpcInventoryTemplate();
            switch (Realm)
            {
                case eRealm.Albion:
                    template.AddNPCEquipment(eInventorySlot.TorsoArmor, 2230); break;
                case eRealm.Midgard:
                    template.AddNPCEquipment(eInventorySlot.TorsoArmor, 2232);
                    template.AddNPCEquipment(eInventorySlot.ArmsArmor, 2233);
                    template.AddNPCEquipment(eInventorySlot.LegsArmor, 2234);
                    template.AddNPCEquipment(eInventorySlot.HandsArmor, 2235);
                    template.AddNPCEquipment(eInventorySlot.FeetArmor, 2236);
                    break;
                case eRealm.Hibernia:
                    template.AddNPCEquipment(eInventorySlot.TorsoArmor, 2231); break;
            }
            Inventory = template.CloseTemplate();
            Flags = eFlags.PEACE;
            return base.AddToWorld();
        }

        public override bool ReceiveItem(GameLiving source, DbInventoryItem item)
        {
            GamePlayer player = source as GamePlayer;
            if (player == null)
                return base.ReceiveItem(source, item);

            if (!source.IsWithinRadius(this, WorldMgr.INTERACT_DISTANCE))
            {
                player.Out.SendMessage("You are too far away to give anything to " + GetName(0, false) + ".", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (player.MLLevel < 10)
            {
                player.Out.SendMessage("You must first have completed all MLs in order to be able to use my service.", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return false;
            }

            if (!player.Inventory.IsSlotsFree(10, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack))
            {
                player.Out.SendMessage("You need first ten free inventory slots so that the Respec can continue.", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return false;
            }

            if (item != null && item.Id_nb == "Star_of_Destiny")
            {
                player.Out.SendMessage("Excellent, you have a rare Star of Destiny! Here, please take your Tokens back!", eChatType.CT_Say, eChatLoc.CL_PopupWindow);

                string[] paths = { "Banelord", "Battlemaster", "Convoker", "Perfecter", "Sojourner", "Spymaster", "Stormlord", "Warlord" };
                foreach (string path in paths)
                {
                    for (int ml = 1; ml <= 10; ml++)
                        player.RemoveSpellLine("ML" + ml + " " + path);
                }

                DbItemTemplate token1 = GameServer.Database.FindObjectByKey<DbItemTemplate>("ml1token");
                if (token1 != null) player.Inventory.AddTemplate(GameInventoryItem.Create<DbItemTemplate>(token1), 1, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                DbItemTemplate token10 = GameServer.Database.FindObjectByKey<DbItemTemplate>("ml10token");
                if (token10 != null) player.Inventory.AddTemplate(GameInventoryItem.Create<DbItemTemplate>(token10), 1, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);

                player.Inventory.RemoveItem(item);
                player.Out.SendUpdatePlayerSkills();
                player.Out.SendUpdatePlayer();
                player.SaveIntoDatabase();
                player.Client.Out.SendPlayerQuit(false);
                player.Quit(true);
                return true;
            }

            return base.ReceiveItem(source, item);
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            SayTo(player, "Hello " + player.Name + ", for this service you need ML10. Hand me a Star of Destiny and I shall remove your current ML choice. I will also return your ML Tokens, you will need 10 empty inventory slots.");
            return true;
        }
    }
}