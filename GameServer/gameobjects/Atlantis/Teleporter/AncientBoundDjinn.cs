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
using log4net;
using System.Reflection;
using DOL.Events;
using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.GS.Housing;

namespace DOL.GS
{
    /// <summary>
    /// Ancient bound djinn (Atlantis teleporter).
    /// </summary>
    /// <author>Aredhel</author>
    public abstract class AncientBoundDjinn : GameTeleporter
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const int NpcTemplateId = 3000;      
        private const int ZOffset = 63;

        /// <summary>
        /// Creates a new djinn.
        /// </summary>
        public AncientBoundDjinn(DjinnStone djinnStone) : base()
        {
            NpcTemplate npcTemplate = NpcTemplateMgr.GetTemplate(NpcTemplateId);

            if (npcTemplate == null)
                throw new ArgumentNullException("Can't find NPC template for ancient bound djinn");

            LoadTemplate(npcTemplate);

            Level = 70;
            CurrentRegion = djinnStone.CurrentRegion;
            Heading = djinnStone.Heading;
            Realm = eRealm.None;
            Flags ^= GameNPC.eFlags.FLYING | GameNPC.eFlags.PEACE;
            X = djinnStone.X;
            Y = djinnStone.Y;
            Z = djinnStone.Z + HoverHeight;
            base.Size = Size;
        }

        /// <summary>
        /// Gets the actual size.
        /// </summary>
        virtual protected new byte Size
        {
            get { return base.Size; }
        }

        /// <summary>
        /// Gets the height at which the djinn is hovering.
        /// </summary>
        virtual protected int HoverHeight
        {
            get { return 63; }
        }

        /// <summary>
        /// Teleporter type, needed to pick the right TeleportID.
        /// </summary>
        protected override String Type
        {
            get { return "Djinn"; }
        }

        /// <summary>
        /// The destination realm.
        /// </summary>
        protected override eRealm DestinationRealm
        {
            get
            {
				return CurrentZone.Realm;
            }
        }

        /// <summary>
        /// Pick a model for this zone.
        /// </summary>
        protected ushort VisibleModel
        {
            get
            {
                // Null-Check for CurrentZone
                if (CurrentZone == null)
                {
                    log.Warn($"CurrentZone is null for Djinn at {X},{Y},{Z} in Region {CurrentRegionID}. Using default model.");
                    return 0x4aa; // Fallback: Standard Oceanus Hesperos Model
                }

                switch (CurrentZone.ID)
                {
                    // Oceanus Hesperos.
                    case 73:            // Albion.
                    case 30:            // Midgard.
                    case 130:           // Hibernia.
                        return 0x4aa;

                    // Stygian Delta.
                    case 81:
                    case 38:
                    case 138:
                        return 0x4ac;

                    // Land of Atum.
                    case 82:
                    case 39:
                    case 139:
                        return 0x4ac;

                    // Oceanus Notos.
                    case 76:
                    case 33:
                    case 133:
                        return 0x4ae;

                    // Arbor Glen.
                    case 87:
                    case 44:
                    case 144:
                        return 0x4ae;

                    // Oceanus Anatole:
                    case 77:
                    case 34:
                    case 134:
                        return 0x4aa;

                    // Ashen Isles:
                    case 85:
                    case 42:
                    case 142:
                        return 0x4af;

                    // Sobekite Eternal:
                    case 79:
                    case 36:
                    case 136:
                        return 0x4ad;

                    // Temple of Twilight:
                    case 80:
                    case 37:
                    case 137:
                        return 0x4ad;

                    // Great Pyramid:
                    case 88:
                    case 45:
                    case 145:
                        return 0x4ac;

                    // Halls of Ma'ati:
                    case 83:
                    case 40:
                    case 140:
                        return 0x4ac;

                    // Deep Volcanos:
                    case 89:
                    case 46:
                    case 146:
                        return 0x4af;

                    // Aerus City:
                    case 90:
                    case 47:
                    case 147:
                        return 0x4ab;

                    default:
                        return 0x4aa;
                }
            }
        }

        /// <summary>
        /// Summon the djinn.
        /// </summary>
        public virtual void Summon()
        {
        }

        /// <summary>
        /// Whether or not the djinn is summoned.
        /// </summary>
        public virtual bool IsSummoned
        {
            get { return false; }
        }

        /// <summary>
        /// Player right-clicked the djinn.
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            String intro = String.Format("According to the rules set down by the Atlantean [masters], {0} {1} ",
                "you are authorized for expeditious transport to your homeland or any of the Havens. Please state",
                "your destination:");

            String destinations;

            switch (player.Realm)
            {
                case eRealm.Albion:
                    destinations = String.Format("[Castle Sauvage], [Oceanus], [Stygia], [Volcanus], [Aerus], the [dungeons of Atlantis], {0} {1}",
                        "[Snowdonia Fortress], [Camelot], [Gothwaite Harbor], [Inconnu Crypt], your [Guild] house, your",
                        "[Personal] house, your [Hearth] bind, or to the [Caerwent] housing area?");
                    break;
                case eRealm.Midgard:
                    destinations = String.Format("[Svasud Faste], [Oceanus], [Stygia], [Volcanus], [Aerus], the [dungeons of Atlantis], {0} {1}",
                        "[Vindsaul Faste], [Jordheim], [Aegirhamn], [Kobold] Undercity, your [Guild] house, your",
                        "[Personal] house, your [Hearth] bind, or to the [Erikstaad] housing area?");
                    break;
                case eRealm.Hibernia:
                    destinations = String.Format("[Druim Ligen], [Oceanus], [Stygia], [Volcanus], [Aerus], the [dungeons of Atlantis], {0} {1}",
                        "[Druim Cain], the [Grove of Domnann], [Tir na Nog], [Shar Labyrinth], your [Guild] house, your",
                        "[Personal] house, your [Hearth] bind, or to the [Meath] housing area?");
                    break;
                default:
                    SayTo(player, "I don't know you, which realm are you from?");
                    return true;
            }

            SayTo(player, String.Format("{0}{1}", intro, destinations));
            return true;
        }

        /// <summary>
        /// Talk to the djinn.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        public override bool WhisperReceive(GameLiving source, String text)
        {
            if (!(source is GamePlayer))
                return false;

            GamePlayer player = source as GamePlayer;

            // Manage the chit-chat.

            switch (text.ToLower())
            {
                case "masters":
                    String reply = String.Format("The Atlantean masters are a great and powerful people to whom [we] are bound.");
                    SayTo(player, reply);
                    return true;
                case "we":
                    return true;    // No reply on live.
            }

            return base.WhisperReceive(source, text);
        }


        protected override bool GetTeleportLocation(GamePlayer player, string text)
        {
            string lowerText = text.ToLower();

            // SPECIAL: Personal House Fallback
            if (lowerText == "personal")
            {
                House house = HouseMgr.GetHouseByPlayer(player);

                if (house == null)
                {
                    // No Existing Personal House → Fallback to Housing-Entrance (per Realm)
                    switch (player.Realm)
                    {
                        case eRealm.Albion:
                            text = "Caerwent";
                            break;
                        case eRealm.Midgard:
                            text = "Erikstaad";
                            break;
                        case eRealm.Hibernia:
                            text = "Meath";
                            break;
                        default:
                            text = "entrance";
                            break;
                    }
                    // Fall-through for normal DB-Search
                }
                else
                {
                    // Existing Personal House -> Teleport to Personal House
                    IGameLocation location = house.OutdoorJumpPoint;
                    DbTeleport teleport = new DbTeleport
                    {
                        TeleportID = "personal",
                        Realm = (int)player.Realm,
                        RegionID = location.RegionID,
                        X = location.X,
                        Y = location.Y,
                        Z = location.Z,
                        Heading = location.Heading
                    };
                    OnDestinationPicked(player, teleport);
                    return true;
                }
            }

            // Specials for other Housing-Teleports -> sent to GameTeleporter Base
            if (lowerText == "guild" || lowerText == "hearth" || lowerText == "battlegrounds")
            {
                return base.GetTeleportLocation(player, text);
            }

            // Normal Djinn-Teleports (Realm-specific search)
            DbTeleport port = WorldMgr.GetTeleportLocation(player.Realm, String.Format("{0}:{1}", Type, text));

            if (port != null)
            {
                if (port.RegionID == 0 && port.X == 0 && port.Y == 0 && port.Z == 0)
                {
                    OnSubSelectionPicked(player, port);
                }
                else
                {
                    OnDestinationPicked(player, port);
                }
                return false;
            }

            return true;  // Needs further processing.
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
                case "oceanus":
                    {
                        String reply = String.Format("I can transport you to the Haven of Oceanus in {0} {1}",
                            "Oceanus [Hesperos], the mouth of [Cetus' Pit], or the heights of the great",
                            "[Temple] of Sobekite Eternal.");
                        SayTo(player, reply);
                        return;
                    }
                case "stygia":
                    {
                        String reply = String.Format("Do you seek the sandy Haven of Stygia in the Stygian {0}",
                            "[Delta] or the distant [Land of Atum]?");
                        SayTo(player, reply);
                        return;
                    }
                case "volcanus":
                    {
                        String reply = String.Format("Do you wish to approach [Typhon's Reach] from the Haven {0} {1}",
                            "of Volcanus or do you perhaps have more ambitious plans, such as attacking",
                            "[Thusia Nesos], the Temple of [Apollo], [Vazul's Fortress], or the [Chimera] herself?");
                        SayTo(player, reply);
                        return;
                    }
                case "aerus":
                    {
                        String reply = String.Format("Do you seek the Haven of Aerus outside [Green Glades] or {0}",
                            "perhaps the Temple of [Talos]?");
                        SayTo(player, reply);
                        return;
                    }
                case "dungeons of atlantis":
                    {
                        String reply = String.Format("I can provide access to [Sobekite Eternal], the {0} {1}",
                            "Temple of [Twilight], the [Great Pyramid], the [Halls of Ma'ati], [Deep] within",
                            "Volcanus, or even the [City] of Aerus.");
                        SayTo(player, reply);
                        return;
                    }
                case "twilight":
                    {
                        String reply = String.Format("Do you seek an audience with one of the great ladies {0} {1}",
                            "of that dark temple? I'm sure that [Moirai], [Kepa], [Casta], [Laodameia], [Antioos],",
                            "[Sinovia], or even [Medusa] would love to have you over for dinner.");
                        SayTo(player, reply);
                        return;
                    }
                case "halls of ma'ati":
                    {
                        String reply = String.Format("Which interests you, the [entrance], the [Anubite] side, {0} {1}",
                            "or the [An-Uat] side? Or are you already ready to face your final fate in the",
                            "[Chamber of Ammut]?");
                        SayTo(player, reply);
                        return;
                    }
                case "deep":
                    {
                        String reply = String.Format("Do you wish to meet with the Mediators of the [southwest] {0} {1}",
                            "or [northeast] hall, face [Katorii's] gaze, or are you foolish enough to battle the",
                            "likes of [Typhon himself]?");
                        SayTo(player, reply);
                        return;
                    }
                case "city":
                    {
                        String reply = String.Format("I can send you to the entrance near the [portal], the great {0} {1} {2}",
                            "Unifier, [Lethos], the [ancient kings] remembered now only for their reputations, the",
                            "famous teacher, [Nelos], the most well known and honored avriel, [Katri], or even",
                            "the [Phoenix] itself.");
                        SayTo(player, reply);
                        return;
                    }

            }

            base.OnSubSelectionPicked(player, subSelection);
        }

        /// <summary>
        /// Player has picked a teleport destination.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="destination"></param>
        protected override void OnDestinationPicked(GamePlayer player, DbTeleport destination)
        {
            if (player == null)
                return;

            if (Region.IsAtlantis(player.CurrentRegionID) &&
                Region.IsAtlantis(destination.RegionID))
                destination.RegionID = player.CurrentRegionID;

            String teleportInfo = "The magic of the {0} delivers you to the Haven of {1}.";

            switch (destination.TeleportID.ToLower())
            {
                case "hesperos":
                    {
                        player.Out.SendMessage(String.Format(teleportInfo, Name, "Oceanus"),
                            eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        base.OnTeleport(player, destination);
                        return;
                    }
                case "delta":
                    {
                        player.Out.SendMessage(String.Format(teleportInfo, Name, "Stygia"),
                            eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        base.OnTeleport(player, destination);
                        return;
                    }
                case "green glades":
                    {
                        player.Out.SendMessage(String.Format(teleportInfo, Name, "Aerus"),
                            eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        base.OnTeleport(player, destination);
                        return;
                    }
            }

            base.OnDestinationPicked(player, destination);
        }

        /// <summary>
        /// Teleport the player to the designated coordinates. 
        /// </summary>
        /// <param name="player"></param>
        /// <param name="destination"></param>
        protected override void OnTeleport(GamePlayer player, DbTeleport destination)
        {
            player.Out.SendMessage("There is an odd distortion in the air around you...", 
                eChatType.CT_System, eChatLoc.CL_SystemWindow);

            base.OnTeleport(player, destination);
        }

        /// <summary>
        /// "Say" content sent to the system window.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public override bool Say(String message)
        {
			foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.SAY_DISTANCE))
			{
				player.Out.SendMessage(String.Format("The {0} says, \"{1}\"", this.Name, message), eChatType.CT_System, eChatLoc.CL_SystemWindow);
			}

            return true;
        }
    }
}
