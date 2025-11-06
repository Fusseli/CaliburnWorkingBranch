/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */
using System;
using System.Collections.Generic;
using System.Text;
using DOL.GS.PacketHandler;
using DOL.Database;
using System.Collections;
using DOL.GS.Quests;
using log4net;
using System.Reflection;
using DOL.Events;
using DOL.GS.Quests.Atlantis;
using System.Linq;

namespace DOL.GS
{
    /// <summary>
    /// The scholars handing out the artifacts.
    /// </summary>
    /// <author>Aredhel</author>
    public class Scholar : Researcher
    {
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		/// <summary>
		/// Create a new scholar.
		/// </summary>
        public Scholar()
            : base() { }

        /// <summary>
        /// Interact with scholar.
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player)) 
                return false;

			IList quests = QuestListToGive;
			int count = 0;
			string artifacts = "";
			if (quests.Count > 0)
			{
				lock (quests.SyncRoot)
				{
					int numQuests = quests.Count;
					foreach (ArtifactQuest quest in quests)
					{
						// if continuing a quest and on a step that requires input then 
						// let the quest handle the interaction
						ArtifactQuest playerQuest = (ArtifactQuest)player.IsDoingQuest(quest.GetType());
						if (playerQuest != null)
						{
							if (playerQuest.Interact(this, player))
								return true;
						}

						if (player.CanReceiveArtifact(quest.ArtifactID))
						{
							if (count > 0 && numQuests < quests.Count)
								artifacts += (numQuests == 1) ? ", or " : ", ";

							artifacts += String.Format("[{0}]", quest.ArtifactID);

							++count;
						}

						--numQuests;
					}
				}
			}

			String intro = "";

			if (count == 0)
			{
				intro = "I have no artifacts available for your class";
			}
			else
			{
				intro = String.Format("Which artifact may I assist you with, {0}? {1} {2}",
					player.Name,
					"I study the lore and magic of the following artifacts:",
					artifacts);

			}

			intro += ".";

            SayTo(player, eChatLoc.CL_PopupWindow, intro);

            intro = String.Format("{0}, did you find any of the stories that chronicle the powers of the {1} {2} ",
                player.Name,
                "artifacts? We can unlock the powers of these artifacts by studying the stories.",
                "I can take the story and unlock the artifact's magic.");

            SayTo(player, eChatLoc.CL_PopupWindow, intro);
            return true;
        }

		/// <summary>
		/// Talk to the scholar.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="text"></param>
		/// <returns></returns>
		public override bool WhisperReceive(GameLiving source, string text)
		{
			if (!base.WhisperReceive(source, text)) 
				return false;

			GamePlayer player = source as GamePlayer;
			if (player == null)
				return false;

			lock (QuestListToGive.SyncRoot)
			{
				// Start new quest...

				foreach (ArtifactQuest quest in QuestListToGive)
				{
					if (text.ToLower() == quest.ArtifactID.ToLower())
					{
						if (quest.CheckQuestQualification(player))
						{
							if (player.CanReceiveArtifact(quest.ArtifactID))
							{
								GiveArtifactQuest(player, quest.GetType());
							}
							else
							{
								RefuseArtifact(player);
							}
						}
						else
						{
							DenyArtifactQuest(player, quest.ReasonFailQualification);
						}
						return false;
					}
				}

				// ...or continuing a quest?

				foreach (AbstractQuest quest in player.DataQuestList)
				{
					if (quest is ArtifactQuest && (HasQuest(quest.GetType()) != null))
						if ((quest as ArtifactQuest).WhisperReceive(player, this, text))
							return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Deny a quest to a player.
		/// </summary>
		/// <param name="player"></param>
		private void DenyArtifactQuest(GamePlayer player, string reason)
		{
			if (player != null)
			{
                String reply = String.Format("{0} I cannot activate that artifact for you. {1} {2} {3} {4} {5} \n\nHint: {6}",
                     player.Name,
                     "This could be because you have already activated it, or you are in the",
                     "process of activating it, or you may not have completed everything",
                     "you need to do. Remember that the activation process requires you to",
                     "have credit for the artifact's encounter, as well as the artifact's",
                     "complete book of scrolls.",
					 reason);
				TurnTo(player);
				SayTo(player, eChatLoc.CL_PopupWindow, reply);
			}
			return;
		}

        /// <summary>
        /// This is used when the player is ready to receive the artifact,
        /// but is not of a class who can accept this artifact.
        /// </summary>
        /// <param name="player"></param>
        private void RefuseArtifact(GamePlayer player)
        {
            if (player != null)
            {
				String reply = String.Format("I'm sorry, but I shouldn't recreate this artifact for you, {0} {1} {2}",
											"as it wouldn't make proper use of your abilities. There are other artifacts",
											"in Atlantis better suited to your needs.",
											"\n\nIf you feel like your class qualifies for this artifact please /report this error to my superiors.");
                TurnTo(player);
                SayTo(player, eChatLoc.CL_PopupWindow, reply);
            }
            return;
        }

		/// <summary>
		/// Give the artifact quest to the player.
		/// </summary>
		/// <param name="player"></param>
		/// <param name="questType"></param>
		private void GiveArtifactQuest(GamePlayer player, Type questType)
		{
			if (player == null || questType == null)
				return;

			ArtifactQuest quest = (ArtifactQuest)Activator.CreateInstance(questType,
				new object[] { player });

			if (quest == null)
				return;

			player.AddQuest(quest);
			quest.WhisperReceive(player, this, quest.ArtifactID);
		}

        /// <summary>
        /// Invoked when scholar receives an item.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        /// <summary>
        /// Invoked when scholar receives an item.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        public override bool ReceiveItem(GameLiving source, DbInventoryItem item)
        {
            Console.WriteLine($"[SCHOLAR DEBUG] ReceiveItem CALLED! Player: {(source as GamePlayer)?.Name}, Item: {item?.Name}");

            if (base.ReceiveItem(source, item))
            {
                Console.WriteLine("[SCHOLAR DEBUG] base.ReceiveItem returned TRUE");
                return true;
            }

            GamePlayer player = source as GamePlayer;
            if (player == null || item == null)
            {
                Console.WriteLine("[SCHOLAR DEBUG] Player or Item is NULL!");
                return false;
            }

            Console.WriteLine($"[SCHOLAR DEBUG] Processing item: {item.Name}");

            // 1. Prüfen auf Credit Items
            if (item.Name.EndsWith("Credit"))
            {
                Console.WriteLine($"[SCHOLAR DEBUG] Item is Credit item, processing...");
                if (ArtifactMgr.GrantArtifactBountyCredit(player, item.Name))
                {
                    player.Inventory.RemoveItem(item);
                    InventoryLogging.LogInventoryAction(player, this, eInventoryActionType.Merchant, item.Template, item.Count);
                    return true;
                }
                else
                {
                    player.Inventory.RemoveItem(item);
                    InventoryLogging.LogInventoryAction(player, this, eInventoryActionType.Merchant, item.Template, item.Count);
                    long totalValue = item.Price;
                    player.GainBountyPoints(totalValue);
                    player.Out.SendUpdatePoints();
                    player.Out.SendMessage(totalValue + " Bounty Points refunded", eChatType.CT_ScreenCenterSmaller, eChatLoc.CL_SystemWindow);
                    player.Out.SendMessage("You already have this credit or your class is not eligible to receive this artifact. " + totalValue + " Bounty Points refunded!", eChatType.CT_Merchant, eChatLoc.CL_SystemWindow);
                    return true;
                }
            }

            // 2. Prüfen ob das Item ein Artifact Book ist
            string bookArtifactID = null;
            var pageNumbers = ArtifactMgr.GetPageNumbers(item, ref bookArtifactID);

            Console.WriteLine($"[SCHOLAR DEBUG] BookArtifactID: {bookArtifactID}, PageNumbers: {pageNumbers}");

            // ? NEU: Prüfe BEIDE Quest-Listen!
            Console.WriteLine($"[SCHOLAR DEBUG] Scholar '{Name}' has {QuestListToGive.Count} quests to give");
            foreach (var quest in QuestListToGive)
            {
                Console.WriteLine($"[SCHOLAR DEBUG]   - Quest: {quest.GetType().Name}");
            }

            // 3. Quest Items an die entsprechende Quest weitergeben
            lock (QuestListToGive.SyncRoot)
            {
                try
                {
                    // ? WICHTIG: Durchsuche player.QuestList (geskriptete Quests) statt DataQuestList!
                    Console.WriteLine($"[SCHOLAR DEBUG] Player has {player.QuestList.Count} active quests (scripted)");

                    foreach (var questEntry in player.QuestList)
                    {
                        AbstractQuest quest = questEntry.Key;

                        if (quest is ArtifactQuest artifactQuest && HasQuest(quest.GetType()) != null)
                        {
                            Console.WriteLine($"[SCHOLAR DEBUG] Checking quest: {artifactQuest.Name}, ArtifactID: {artifactQuest.ArtifactID}, Step: {artifactQuest.Step}");

                            // Wenn es ein vollständiges Buch ist, prüfen wir ob es zur Quest passt
                            if (pageNumbers == ArtifactMgr.Book.AllPages &&
                                bookArtifactID != null &&
                                bookArtifactID == artifactQuest.ArtifactID)
                            {
                                Console.WriteLine($"[SCHOLAR DEBUG] Book matches quest {artifactQuest.Name}, calling ReceiveItem on quest");

                                // Übergebe das Item an die Quest
                                if (artifactQuest.ReceiveItem(player, this, item))
                                {
                                    Console.WriteLine($"[SCHOLAR DEBUG] Quest accepted the item!");
                                    return true;
                                }
                                else
                                {
                                    Console.WriteLine($"[SCHOLAR DEBUG] Quest REJECTED the item!");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"[SCHOLAR DEBUG] Book doesn't match quest, trying as normal item...");
                                // Versuche das Item als normales Quest-Item zu übergeben
                                if (artifactQuest.ReceiveItem(player, this, item))
                                {
                                    Console.WriteLine($"[SCHOLAR DEBUG] Quest accepted the item as normal item!");
                                    return true;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SCHOLAR DEBUG] Exception: {ex.Message}");
                    log.Error("Scholar ReceiveItem Error: ", ex);
                    SayTo(player, eChatLoc.CL_PopupWindow, "I'm very sorry but I'm having trouble locating an artifact for you. Please /report this problem to my superiors.");
                }
            }

            Console.WriteLine($"[SCHOLAR DEBUG] Item NOT accepted! Returning FALSE");
            return false;
        }
    }
}
