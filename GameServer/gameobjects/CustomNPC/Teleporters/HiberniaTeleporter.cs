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

using DOL.Database;
using DOL.GS.PacketHandler;
using log4net.Core;
using System;

namespace DOL.GS
{
	/// <summary>
	/// Hibernia teleporter.
	/// </summary>
	/// <author>Aredhel</author>
	public class HiberniaTeleporter : GameTeleporter
	{
        /// <summary>
        /// Add equipment to the teleporter.
        /// </summary>
        /// <returns></returns>
        public override bool AddToWorld()
        {
            GameNpcInventoryTemplate template = new GameNpcInventoryTemplate();
            template.AddNPCEquipment(eInventorySlot.TorsoArmor, 1008, 0);
            template.AddNPCEquipment(eInventorySlot.HandsArmor, 396, 0);
            template.AddNPCEquipment(eInventorySlot.FeetArmor, 402, 0);
            template.AddNPCEquipment(eInventorySlot.TwoHandWeapon, 468);
            Inventory = template.CloseTemplate();

            SwitchWeapon(eActiveWeaponSlot.TwoHanded);
            VisibleActiveWeaponSlots = 34;
            return base.AddToWorld();
        }

        /// <summary>
        /// Display the teleport indicator around this teleporters feet
        /// </summary>
        public override bool ShowTeleporterIndicator
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Player right-clicked the teleporter.
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public override bool Interact(GamePlayer player)
		{
            if (!base.Interact(player) || GameRelic.IsPlayerCarryingRelic(player)) return false;

            TurnTo(player, 10000);

            SayTo(player, "Greetings, " + player.Name +
			              ", I am able to channel energy to transport you to distant lands. I can send you to the following locations:\n\n" +
						  "[Cruachan Gorge] in the Frontiers\n" +
                          "[Druim Ligen] in Connacht or\n" +
						  "[Druim Cain] in Bri Leith\n" +
			              "[Shannon Estuary] watchtower\n" +
			              "[Domnann] Grove in the [Shrouded Isles]\n" +
						  "[Oceanus] haven in the lost lands of Atlantis\n" +
						  "[Shar Labyrinth] or [Veil Rift] in the Catacombs\n" +
			              "[Tir na Nog] our glorious capital\n" +
			              "[Entrance] to the areas of [Housing]\n" +
                          "Appropriate [Battlegrounds] for your season\n\n" +
                          "Or one of the many [towns] throughout Hibernia");
            return true;
		}

		/// <summary>
		/// Player has picked a subselection.
		/// </summary>
		/// <param name="player"></param>
		/// <param name="subSelection"></param>
		protected override void OnSubSelectionPicked(GamePlayer player, DbTeleport subSelection)
		{
			switch (subSelection.TeleportID.ToLower())
			{
                case "cruachan gorge":
                    {
                        String reply = String.Format("Very well, you would fight for the glory of Hibernia. {0} {1}",
                                                     "Shall I send you to the relic camps [Crair Treflan] or [Magh Tuireadh],",
                                                     "or would you rather depart from the border keeps [Ligen] or [Cain]?");
                        SayTo(player, reply);
                        return;
                    }
                case "shrouded isles":
					{
						String reply = String.Format("The isles of Hy Brasil are an excellent choice. {0} {1}",
						                             "Would you prefer the grove of [Domnann] or perhaps one of the outlying towns",
						                             "like [Droighaid], [Aalid Feie], or [Necht]?");
						SayTo(player, reply);
						return;
					}
				case "housing":
					{
						SayTo(player,
                        "I can send you to your [personal] or [guild] house. If you do not have a personal house, I can teleport you to the housing [entrance] or your housing [hearth] bindstone.");
						return;
					}

				case "towns":
				{
					SayTo(player,
						"I can send you to:\n" +
                        "[Fintain] (Levels 1-9)\n" +
                        "[Mag Mell] (Levels 10-14)\n" +
                        "[Tir na mBeo] (Levels 15-19)\n" +
                        "[Ardagh] (Levels 20-24)\n" +
                        "[Howth] (Levels 25-29)\n" +
                        "[Connla] (Levels 30-34)\n" +
                        "[Innis Carthaig] (Levels 35+)\n" +
						"[Druim Cain] (Levels 40+)\n" +
                        "[Cursed Forest] (Levels 45+)\n" +
                        "[Sheeroe Hills] (Levels 45+)");
                    return;
				}
			}
			base.OnSubSelectionPicked(player, subSelection);
		}

		/// <summary>
		/// Player has picked a destination.
		/// </summary>
		/// <param name="player"></param>
		/// <param name="destination"></param>
		protected override void OnDestinationPicked(GamePlayer player, DbTeleport destination)
		{
			
			Region region = WorldMgr.GetRegion((ushort) destination.RegionID);

			if (region == null || region.IsDisabled)
			{
				player.Out.SendMessage("This destination is not available.", eChatType.CT_System,
					eChatLoc.CL_SystemWindow);
				return;
			}

            // Check for tutorial zone restrictions (Fintain)
            if (destination.TeleportID.Equals("Fintain", StringComparison.OrdinalIgnoreCase))
            {
                if (ServerProperties.Properties.DISABLE_TUTORIAL)
                {
                    SayTo(player, "Sorry, this place is not available for now!");
                    return;
                }

                if (player.Level > 15)
                {
                    SayTo(player, "Sorry, you are far too experienced to enjoy this place!");
                    return;
                }
            }

            Say("I'm now teleporting you to " + destination.TeleportID + ".");
			OnTeleportSpell(player, destination);
		}
	}
}
