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
using DOL.Database;
using DOL.GS.PacketHandler;

namespace DOL.GS
{
	/// <summary>
	/// Midgard dungeon teleporter.
	/// </summary>
	/// <author>MrEyeblaze</author>
	public class MidgardDungeonTeleporter : GameTeleporter
	{
        /// <summary>
        /// Teleporter type, needed to pick the right TeleportID.
        /// </summary>
        protected override String Type
        {
            get { return "Dungeons"; }
        }

        /// <summary>
        /// Add equipment to the teleporter.
        /// </summary>
        /// <returns></returns>
        public override bool AddToWorld()
        {
            GameNpcInventoryTemplate template = new GameNpcInventoryTemplate();
            template.AddNPCEquipment(eInventorySlot.TorsoArmor, 3203, 26, 0, 2);
            template.AddNPCEquipment(eInventorySlot.ArmsArmor, 3205);
            template.AddNPCEquipment(eInventorySlot.HandsArmor, 3207, 26, 0, 2);
            template.AddNPCEquipment(eInventorySlot.LegsArmor, 3204);
            template.AddNPCEquipment(eInventorySlot.FeetArmor, 3206, 26, 0, 2);
            template.AddNPCEquipment(eInventorySlot.Cloak, 3801);
            template.AddNPCEquipment(eInventorySlot.HeadArmor, 1283);
            template.AddNPCEquipment(eInventorySlot.LeftHandWeapon, 3353);
            template.AddNPCEquipment(eInventorySlot.RightHandWeapon, 3712);
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

            SayTo(player, "Greetings, " + player.Name +"\n" +
                          "I am a specialist in Midgard's dungeons and can send you to our various\n" +
                          "expeditionary forces positioned savely either by, or near each entry.\n" +
                          "Please tell me your your destination out of the following dungeon categories\n\n" +
                          "[Classic Dungeons]\n" +
                          "[Shrouded Isles Dungeons]\n" +
                          "[Darkness Rising Dungeons]\n" +
                          "[Frontier Dungeons]\n\n" +
                          "Dungeons in [Atlantis] or [Catacombs]\n" +
                          "are in the hands of two different teleportation guilds.\n" +
                          "The mystic [Djinns of Atlantis], and the magic [Obilisks of Catacombs]\n" +
                          "Visit your realm channelers and reach out to each zone, to find them.");
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
				case "classic dungeons":
				{
					SayTo(player,
						"Alright, these are all the Classic Dungeons, where i can send you to:\n\n" +
                        "[Nisse's Lair] (Levels 10-18)\n" +
                        "[Cursed Tomb] (Levels 18-26)\n" +
                        "[Vendo Caverns] (Levels 26-34)\n" +
                        "[Varulvhamn] (Levels 34-42)\n" +
                        "[Spindelhalla] (Levels 42-50)");
					return;
				}
                case "shrouded isles dungeons":
                    {
                        SayTo(player,
                        "Very well, these are all the Shrouded Isles Dungeons where i can send you to:\n\n" +
                        "[Trollheim] (Levels 50+)\n" +
                        "[Iarnvidiur's Lair] (Levels 50+)\n" +
                        "[Tuscaran Glacier] (Levels 50+)");
                        return;
                    }
                case "darkness rising dungeons":
                    {
                        SayTo(player,
                        "As you wish, these are all the Darkness Rising Dungeons where i can send you to:\n\n" +
                        "[Foothills of Midgard] (Levels 30+)\n" +
                        "[Nephraal's Gaol] (Levels 35+)\n" +
                        "[The Demonic Prison] (Levels 40+)\n" +
                        "[Halls of Helgardh] (Levels 45+)\n" +
                        "[Demon Lord's Lair] (Levels 50+)");
                        return;
                    }
                case "frontier dungeons":
                    {
                        SayTo(player,
                        "Please beware, that all frontier dungeons are one way ports, it's just to dangerous for us to operate out there.\n" +
                        "These are all Frontier Dungeons where i can send you to:\n\n" +
                        "[Darkness Falls] (Levels 50+)\n" +
                        "[Passage of Conflict] (Levels 50+)\n" +
                        "[Summoner's Hall] (Levels 50+)\n" +
                        "[Labyrinth of the Minotaur] (Levels 50+)");
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

			Say("I'm now teleporting you to " + destination.TeleportID + ".");
			OnTeleportSpell(player, destination);
		}
	}
}
