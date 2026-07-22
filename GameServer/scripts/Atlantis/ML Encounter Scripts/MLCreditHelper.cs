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

namespace DOL.GS.Atlantis
{
    public static class MLCreditHelper
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static void GiveItem(GameObject killer, GameNPC sender, string ItemTemplateId, int MinNumber, int MaxNumber)
        {
            int Number = Util.Random(MinNumber, MaxNumber);
            GamePlayer ThePlayer;
            if (killer is GameNPC)
            {
                IControlledBrain controlled = ((GameNPC)killer).Brain as IControlledBrain;
                if (controlled != null)
                    ThePlayer = controlled.GetPlayerOwner();
                else
                    return;
            }
            else
            {
                ThePlayer = (GamePlayer)killer;
            }
            if (ThePlayer.Group != null)
            {
                foreach (GamePlayer p in ThePlayer.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    if (p.Group == ThePlayer.Group)
                    {
                        DbInventoryItem FindLoot = p.Inventory.GetFirstItemByID(ItemTemplateId, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                        if (FindLoot == null)
                        {
                            if (Number > 0)
                            {
                                DbItemTemplate Loot = (DbItemTemplate)GameServer.Database.FindObjectByKey<DbItemTemplate>(ItemTemplateId);
                                if (Loot != null)
                                {
                                    p.Inventory.AddTemplate(GameInventoryItem.Create<DbItemTemplate>(Loot), 1, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                                    Number = Number - 1;
                                    p.Out.SendMessage("You received " + Loot.Name + " !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                                    foreach (GamePlayer p2 in ThePlayer.Group.GetPlayersInTheGroup())
                                    {
                                        if (p2.Name != p.Name)
                                            p2.Out.SendMessage(p2.Name + " received " + Loot.Name + " !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                DbInventoryItem FindLoot = ThePlayer.Inventory.GetFirstItemByID(ItemTemplateId, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                if (FindLoot == null)
                {
                    if (Number > 0)
                    {
                        DbItemTemplate Loot = GameServer.Database.FindObjectByKey<DbItemTemplate>(ItemTemplateId);
                        if (Loot != null)
                        {
                            ThePlayer.Inventory.AddTemplate(GameInventoryItem.Create<DbItemTemplate>(Loot), 1, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                            Number = Number - 1;
                            ThePlayer.Out.SendMessage("You received " + Loot.Name + " !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                        }
                    }
                }
            }
        }

        public static void CreditML(byte ml, byte step, GameObject Grantedobject, bool group, bool battlegroup, byte Minlevel)
        {
            GamePlayer player;
            if (Grantedobject is GameNPC)
            {
                IControlledBrain controlled = ((GameNPC)Grantedobject).Brain as IControlledBrain;
                if (controlled != null)
                    player = controlled.GetPlayerOwner();
                else
                    return;
            }
            else
            {
                player = (GamePlayer)Grantedobject;
            }
            if (player.IsAlive == true)
            {
                if (group == false && battlegroup == false)
                {
                    if (player.MLGranted == true && player.MLLevel == (ml - 1))
                    {
                        log.Warn("Master Level - " + ml + "." + step + " - " + player.Name + " - Granted");
                        player.SetFinishedMLStep(ml, step);
                        player.Out.SendMessage("Congratulations, Master Level " + ml + "." + step + " completed !", eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
                        player.Out.SendMessage("Congratulations, Master Level " + ml + "." + step + " completed !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                        player.Out.SendMasterLevelWindow(ml);
                        player.Out.SendPlaySound(eSoundType.Divers, 11);
                        player.SaveIntoDatabase();
                        if (player.GetCountMLStepsCompleted(ml) >= player.GetStepCountForML(ml))
                            player.Out.SendMessage("Return to the Arbiter for your promotion!", eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
                    }
                }
                else if (group == true && battlegroup == false)
                {
                    if (player.Group != null)
                    {
                        log.Warn("Master Level - " + ml + "." + step + " - " + player.Name + "Group Granting...");
                        foreach (GamePlayer p in player.Group.GetPlayersInTheGroup())
                        {
                            if (p != null)
                            {
                                if (p.MLGranted == true && p.MLLevel == (ml - 1) && player.IsAlive && p.IsWithinRadius(player, WorldMgr.MAX_EXPFORKILL_DISTANCE))
                                {
                                    log.Warn("Master Level - " + ml + "." + step + " - " + p.Name + " - Granted");
                                    p.SetFinishedMLStep(ml, step);
                                    p.Out.SendMessage("Congratulations, Master Level " + ml + "." + step + " completed !", eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
                                    p.Out.SendMessage("Congratulations, Master Level " + ml + "." + step + " completed !", eChatType.CT_Broadcast, eChatLoc.CL_ChatWindow);
                                    p.Out.SendMasterLevelWindow(ml);
                                    p.Out.SendPlaySound(eSoundType.Divers, 11);
                                    p.SaveIntoDatabase();
                                    if (p.GetCountMLStepsCompleted(ml) >= p.GetStepCountForML(ml))
                                        p.Out.SendMessage("Return to the Arbiter for your promotion!", eChatType.CT_ScreenCenter, eChatLoc.CL_SystemWindow);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
