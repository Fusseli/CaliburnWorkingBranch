using DOL.AI;
using DOL.AI.Brain;
using DOL.Database;
using DOL.GS.Commands;
using DOL.GS.PacketHandler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace DOL.GS.Scripts
{
    #region Admin/GM/Debug/Cheats

    [CmdAttribute(
    "&mcreate",
    ePrivLevel.GM,
    "/mcreate class [level] [class] [spec] [inv] - Create a mimic of a certain level, class, and weapon handedness at your position or ground target, and invite them if desired.")]
    public class MimicCreateCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length > 0)
            {
                GamePlayer player = client.Player;
                eMimicClass mclass;
                byte level = player.Level;
                eSpecType mimicSpec = eSpecType.None;
                bool invite = false;

                if (args.Length > 1)
                {
                    if (!Enum.TryParse<eMimicClass>(args[1], true, out mclass))
                    {
                        player.Out.SendMessage(args[1] + " could not be parsed into a class type", eChatType.CT_Say, eChatLoc.CL_ChatWindow);
                        return;
                    }
                }
                else
                {
                    player.Out.SendMessage("Class must be specified", eChatType.CT_Say, eChatLoc.CL_ChatWindow);
                    return;
                }

                for (int i = 2; i < args.Length; i++)
                {
                    if (args[i].StartsWith("inv", StringComparison.OrdinalIgnoreCase))
                        invite = true;
                    else if (byte.TryParse(args[i], out byte newLevel))
                    {
                        if (newLevel < 1 || newLevel > player.MaxLevel)
                        {
                            player.Out.SendMessage("Level must be between 1 and " + player.MaxLevel, eChatType.CT_Say, eChatLoc.CL_ChatWindow);
                            return;
                        }
                        level = newLevel; // TryParse clobbers it's out value, so we need an intermediate
                    }
                    else if (!Enum.TryParse<eSpecType>(args[i], true, out mimicSpec) || mimicSpec == eSpecType.None)
                    {
                        player.Out.SendMessage("Could not parse " + args[i], eChatType.CT_Say, eChatLoc.CL_ChatWindow);
                        return;
                    }
                }

                Point3D position = new Point3D(player.X, player.Y, player.Z);

                if (player.GroundTarget != null)
                {
                    Point2D playerPos = new Point2D(player.X, player.Y);

                    if (client.Player.GroundTarget.GetDistance(playerPos) < 5000)
                        position = new Point3D(player.GroundTarget);
                }

                MimicNPC mimic = MimicManager.GetMimic(mclass, level, spec: mimicSpec);
                MimicManager.AddMimicToWorld(mimic, position, player.CurrentRegionID);

                if (invite && GameServer.ServerRules.IsSameRealm(player, mimic, true))
                {
                    if (player.Group == null)
                    {
                        player.Group = new Group(player);
                        player.Group.AddMember(player);
                    }

                    if (!player.Group.AddMember(mimic))
                        player.Out.SendMessage("Could not add mimic to group", eChatType.CT_Say, eChatLoc.CL_ChatWindow);
                }
            }
        }
    }

    [CmdAttribute(
    "&mspawner",
    ePrivLevel.GM,
    "/mspawner - Spawns mimics at regular intervals at the groundset position. Args: realm, levelMin, levelMax, max amount")]

    public class MimicSpawnerCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            Point3D position = new Point3D(client.Player.X, client.Player.Y, client.Player.Z);

            if (client.Player.GroundTarget != null && client.Player.GroundTarget.IsWithinRadius(position, WorldMgr.VISIBILITY_DISTANCE))
                position = new Point3D(client.Player.GroundTarget);

            if (args.Length > 1)
                args[1] = args[1].ToLower();

            int levelMin = int.Parse(args[2]);
            int levelMax = int.Parse(args[3]);
            int maxAmount = int.Parse(args[4]);

            levelMin = Math.Max(1, levelMin);
            levelMax = Math.Min(levelMax, 50);

            if (levelMin > levelMax)
            {
                int tempMin = levelMin;
                levelMin = levelMax;
                levelMax = tempMin;
            }

            if (maxAmount > 500 || maxAmount < 0)
                maxAmount = 1;

            MimicSpawner mimicSpawner = null;

            switch (args[1])
            {
                case "alb":
                case "albion":
                mimicSpawner = new MimicSpawner(eRealm.Albion, levelMin, levelMax, maxAmount, position, client.Player.CurrentRegionID);
                break;

                case "mid":
                case "midgard":
                mimicSpawner = new MimicSpawner(eRealm.Midgard, levelMin, levelMax, maxAmount, position, client.Player.CurrentRegionID);
                break;

                case "hib":
                case "hibernia:":
                mimicSpawner = new MimicSpawner(eRealm.Hibernia, levelMin, levelMax, maxAmount, position, client.Player.CurrentRegionID);
                break;
            }

            if (mimicSpawner != null)
            {
                if (mimicSpawner.AddToWorld())
                    MimicSpawning.MimicSpawners.Add(mimicSpawner);
            }
        }
    }

    [CmdAttribute(
    "&mgroup",
    ePrivLevel.GM,
    "/mgroup - To summon a group of mimics from a realm. Args: realm, amount, level")]

    public class MimicSummonMimicGroupCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length >= 2)
            {
                args[1] = args[1].ToLower();

                byte groupSize = 8;
                if (args.Length >= 3)
                {
                    groupSize = byte.Parse(args[2]);

                    if (groupSize < 1 || groupSize > 8)
                        groupSize = 8;
                }

                byte level;
                if (args.Length >= 4)
                {
                    level = byte.Parse(args[3]);

                    if (level < 1 || level > 50)
                        level = 1;
                }
                else
                    level = client.Player.Level;

                bool preventCombat = false;
                if (args.Length >= 5)
                {
                    preventCombat = bool.Parse(args[4]);
                    Console.WriteLine(preventCombat);
                }

                Point3D position = new Point3D(client.Player.X, client.Player.Y, client.Player.Z);

                if (client.Player.GroundTarget != null)
                {
                    Point2D playerPos = new Point2D(client.Player.X, client.Player.Y);

                    if (client.Player.GroundTarget.GetDistance(playerPos) < WorldMgr.VISIBILITY_DISTANCE)
                        position = new Point3D(client.Player.GroundTarget);
                }

                if (position != null)
                {
                    List<GameLiving> groupMembers = new List<GameLiving>();
                    MimicNPC mimic;

                    switch (args[1])
                    {
                        case "alb":
                        case "albion":
                        {
                            for (int i = 0; i < groupSize; i++)
                            {
                                int randomX = Util.Random(-100, 100);
                                int randomY = Util.Random(-100, 100);

                                position.X += randomX;
                                position.Y += randomY;

                                mimic = MimicManager.GetMimic(MimicManager.GetRandomMimicClass(eRealm.Albion), level, preventCombat: preventCombat);
                                MimicManager.AddMimicToWorld(mimic, position, client.Player.CurrentRegionID);

                                if (mimic != null)
                                    groupMembers.Add(mimic);
                            }

                            break;
                        }

                        case "hib":
                        case "hibernia":
                        {
                            for (int i = 0; i < groupSize; i++)
                            {
                                int randomX = Util.Random(-100, 100);
                                int randomY = Util.Random(-100, 100);

                                position.X += randomX;
                                position.Y += randomY;

                                mimic = MimicManager.GetMimic(MimicManager.GetRandomMimicClass(eRealm.Hibernia), level, preventCombat: preventCombat);
                                MimicManager.AddMimicToWorld(mimic, position, client.Player.CurrentRegionID);

                                if (mimic != null)
                                    groupMembers.Add(mimic);
                            }

                            break;
                        }

                        case "mid":
                        case "midgard":
                        {
                            for (int i = 0; i < groupSize; i++)
                            {
                                int randomX = Util.Random(-100, 100);
                                int randomY = Util.Random(-100, 100);

                                position.X += randomX;
                                position.Y += randomY;

                                mimic = MimicManager.GetMimic(MimicManager.GetRandomMimicClass(eRealm.Midgard), level, preventCombat: preventCombat);
                                MimicManager.AddMimicToWorld(mimic, position, client.Player.CurrentRegionID);

                                if (mimic != null)
                                    groupMembers.Add(mimic);
                            }

                            break;
                        }

                        default: break;
                    }

                    if (groupMembers.Count > 0)
                    {
                        if (groupMembers[0].Group == null)
                        {
                            groupMembers[0].Group = new Group(groupMembers[0]);
                            groupMembers[0].Group.AddMember(groupMembers[0]);
                        }

                        foreach (GameLiving living in groupMembers)
                        {
                            if (living.Group == null)
                            {
                                groupMembers[0].Group.AddMember(living);

                                MimicBrain brain = ((MimicNPC)living).Brain as MimicBrain;
                                brain.FSM.SetCurrentState(eFSMStateType.WAKING_UP);
                            }
                        }
                    }
                }
            }
        }
    }

    [CmdAttribute(
       "&mpvp",
       ePrivLevel.Player,
       "/mpvp (true/false) - Set PvP mode on targeted mimic or your group with no target.")]
    public class MimicPvPModeCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player == null)
                return;

            string message = string.Empty;
            MimicNPC mimic = client.Player.TargetObject as MimicNPC;

            if (args.Length > 1)
            {
                args[1] = args[1].ToLower();

                bool toggle = false;

                switch (args[1])
                {
                    case "true":
                    toggle = true;
                    break;

                    case "false":
                    toggle = false;
                    break;
                }

                if (mimic != null)
                {
                    mimic.MimicBrain.PvPMode = toggle;
                    message = "PvP mode for " + mimic.Name + " is " + toggle;
                }
                else if (client.Player.Group != null)
                {
                    foreach (GameLiving groupMember in client.Player.Group.GetMembersInTheGroup())
                    {
                        if (groupMember is MimicNPC mimicNPC)
                            mimicNPC.MimicBrain.PvPMode = toggle;
                    }

                    message = "PvP mode for your grouped mimics is " + toggle;
                }

                client.Player.Out.SendMessage(message, eChatType.CT_Say, eChatLoc.CL_ChatWindow);
            }
        }
    }

    [CmdAttribute(
   "&mpc",
   ePrivLevel.Player,
   "/mpc (true/false) [group] - Set PreventCombat on targeted mimic or their group, or your group with no target.")]
    public class MimicCombatPreventCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player == null)
                return;

            string message = string.Empty;
            MimicNPC mimic = client.Player.TargetObject as MimicNPC;

            if (args.Length > 1)
            {
                args[1] = args[1].ToLower();

                bool toggle = false;

                switch (args[1])
                {
                    case "true":
                    toggle = true;
                    break;

                    case "false":
                    toggle = false;
                    break;
                }

                if (mimic != null)
                {
                    if (client.Account.PrivLevel < (uint)ePrivLevel.GM && mimic.Group != client.Player.Group)
                    {
                        client.Player.Out.SendMessage("You can only direct mimics in your group.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }
                    if (args.Length > 2 && args[2].Equals("group", StringComparison.OrdinalIgnoreCase)
                        && mimic.Group != null)
                    {
                        foreach (GameLiving groupMember in mimic.Group.GetMembersInTheGroup())
                        {
                            if (groupMember is MimicNPC mimicNPC)
                            {
                                mimicNPC.MimicBrain.PreventCombat = toggle;
                                message = "PreventCombat for " + mimicNPC.Name + "'s group is " + toggle;
                            }
                        }
                    }
                    else
                    {
                        mimic.MimicBrain.PreventCombat = toggle;
                        message = "PreventCombat for " + mimic.Name + " is " + toggle;
                    }
                }
                else if (client.Player.Group != null)
                {
                    foreach (GameLiving groupMember in client.Player.Group.GetMembersInTheGroup())
                    {
                        if (groupMember is MimicNPC mimicNPC)
                            mimicNPC.MimicBrain.PreventCombat = toggle;
                    }

                    message = "PreventCombat for your grouped mimics is " + toggle;
                }

                client.Player.Out.SendMessage(message, eChatType.CT_Say, eChatLoc.CL_ChatWindow);
            }
        }
    }

    [CmdAttribute(
    "&mheal",
    ePrivLevel.Player,
    "/mheal - Toggle whether a mimic will engage in combat or stay back and focus on healing spells")]
    public class MimicHealCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player.TargetObject is MimicNPC mimic)
            {
                if (client.Account.PrivLevel < (uint)ePrivLevel.GM && (mimic.Group == null || mimic.Group != client.Player.Group))
                {
                    mimic.Whisper(client.Player, "I only take orders from my group.");
                    return;
                }
                if (mimic.Group == null)
                    mimic.Whisper(client.Player, "I need to be a in a group");
                else if (!mimic.CanCastHealSpells && !mimic.CanCastInstantHealSpells)
                    mimic.Whisper(client.Player, "I cannot cast healing spells");
                else
                {
                    mimic.MimicBrain.IsHealer = !mimic.MimicBrain.IsHealer;
                    mimic.MimicBrain.HealerManual = true;
                    if (mimic.MimicBrain.IsHealer)
                        mimic.Group.SendMessageToGroupMembers(mimic, "I will stay out of combat and focus on healing", eChatType.CT_Group, eChatLoc.CL_ChatWindow);
                    else
                        mimic.Group.SendMessageToGroupMembers(mimic, "I will engage in combat", eChatType.CT_Group, eChatLoc.CL_ChatWindow);
                }
            }
        }
    }

    [CmdAttribute(
    "&mrez",
    ePrivLevel.Player,
    "/mrez - Toggle whether a mimic will resurrect dead group members")]
    public class MimicRezCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player.TargetObject is MimicNPC mimic)
            {
                if (client.Account.PrivLevel < (uint)ePrivLevel.GM && (mimic.Group == null || mimic.Group != client.Player.Group))
                {
                    mimic.Whisper(client.Player, "I only take orders from my group.");
                    return;
                }
                if (mimic.Group == null)
                    mimic.Whisper(client.Player, "I need to be a in a group");
                else if (mimic.MimicBrain == null || !mimic.MimicBrain.HasRezSpell())
                    mimic.Whisper(client.Player, "I cannot cast resurrection spells");
                else if (args.Length > 1 && args[1].ToLower() == "outside")
                {
                    mimic.MimicBrain.RezOutside = !mimic.MimicBrain.RezOutside;
                    if (mimic.MimicBrain.RezOutside)
                        mimic.Group.SendMessageToGroupMembers(mimic, "I will resurrect fallen strangers when all is calm", eChatType.CT_Group, eChatLoc.CL_ChatWindow);
                    else
                        mimic.Group.SendMessageToGroupMembers(mimic, "I will only resurrect our own group", eChatType.CT_Group, eChatLoc.CL_ChatWindow);
                }
                else
                {
                    mimic.MimicBrain.RezDisabled = !mimic.MimicBrain.RezDisabled;
                    if (mimic.MimicBrain.RezDisabled)
                        mimic.Group.SendMessageToGroupMembers(mimic, "I will not resurrect the dead", eChatType.CT_Group, eChatLoc.CL_ChatWindow);
                    else
                        mimic.Group.SendMessageToGroupMembers(mimic, "I will resurrect fallen group members", eChatType.CT_Group, eChatLoc.CL_ChatWindow);
                }
            }
        }
    }

    [CmdAttribute(
    "&mra",
    ePrivLevel.Player,
    "/mra - Show a mimic's realm rank and realm abilities")]
    public class MimicRealmAbilityCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player.TargetObject is MimicNPC mimic)
            {
                var owned = mimic.GetRealmAbilities();
                int budget = MimicRABuyer.RealmPointBudget(mimic.Level, mimic.RealmLevel);
                int spent = 0;
                var ownedLevels = new Dictionary<string, int>();
                foreach (var ab in owned)
                {
                    if (ab == null || string.IsNullOrEmpty(ab.KeyName))
                        continue;
                    ownedLevels[ab.KeyName] = ab.Level;
                }
                var classRAs = SkillBase.GetClassRealmAbilities(mimic.CharacterClass.ID);
                if (classRAs != null)
                {
                    var offers = new List<MimicRABuyer.Offer>();
                    foreach (var ab in classRAs)
                    {
                        if (ab == null)
                            continue;
                        var inst = ab;
                        offers.Add(new MimicRABuyer.Offer
                        {
                            KeyName = inst.KeyName,
                            MaxLevel = inst.MaxLevel,
                            CostForUpgrade = lvl => inst.CostForUpgrade(lvl),
                        });
                    }
                    spent = MimicRABuyer.SpentPoints(ownedLevels, offers);
                }
                mimic.Whisper(client.Player, $"Realm rank {mimic.RealmRankTitle} (RL {mimic.RealmLevel}), {spent}/{budget} points spent, {owned.Count} abilities");
                int shown = 0;
                foreach (var ab in owned.OrderBy(a => a.KeyName))
                {
                    if (shown >= 12)
                        break;
                    mimic.Whisper(client.Player, $"{ab.Name} {ab.Level}");
                    shown++;
                }
                if (owned.Count > shown)
                    mimic.Whisper(client.Player, $"...and {owned.Count - shown} more");
            }
        }
    }

    [CmdAttribute(
    "&msave",
    ePrivLevel.Player,
    "/msave [newname] - Save a targeted mimic bot into your realm stable (10 per realm)")]
    public class MimicSaveCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player.TargetObject is not MimicNPC mimic)
            {
                client.Player.Out.SendMessage("Target a mimic bot first.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if (!mimic.IsAlive)
            {
                mimic.Whisper(client.Player, "You cannot save a corpse. Resurrect me first.");
                return;
            }
            string account = client.Account.Name;
            // Your own active bot: refresh its snapshot (new gear, new levels).
            if (!string.IsNullOrEmpty(mimic.SavedKey))
            {
                if (!MimicSaveManager.OwnsKey(account, mimic.SavedKey))
                {
                    mimic.Whisper(client.Player, "I belong to someone else.");
                    return;
                }
                if (!mimic.IsAlive)
                {
                    mimic.Whisper(client.Player, "You cannot save a corpse. Resurrect me first.");
                    return;
                }
                if (MimicSaveManager.RefreshActive(mimic))
                    mimic.Whisper(client.Player, "My progress is written down.");
                else
                    mimic.Whisper(client.Player, "My stable row is gone. I am free again.");
                return;
            }
            bool regionIsRvR = client.Player.CurrentRegion != null && client.Player.CurrentRegion.IsRvR
                || client.Player.CurrentZone != null && client.Player.CurrentZone.IsRvR;
            if (!MimicSaveManager.MayKeepBotInRegion(mimic.Realm, client.Player.Realm, regionIsRvR))
            {
                mimic.Whisper(client.Player, "You cannot keep a bot of another realm in RvR lands.");
                return;
            }
            string name = args.Length > 1 ? args[1] : mimic.Name;
            if (!MimicSaveManager.IsValidBotName(name))
            {
                mimic.Whisper(client.Player, "That name is not allowed. Use 3-20 letters, apostrophe or hyphen.");
                return;
            }
            var rows = MimicSaveManager.SelectAccountRows(account);
            var realmRows = new List<DbMimicSave>();
            foreach (var row in rows)
            {
                if (row.Realm == (int)mimic.Realm)
                    realmRows.Add(row);
            }
            var names = new List<string>();
            var used = new List<int>();
            foreach (var row in realmRows)
            {
                names.Add(row.Name);
                used.Add(row.Slot);
            }
            if (MimicSaveManager.NameTaken(names, name))
            {
                mimic.Whisper(client.Player, $"You already keep a bot named {name} in this realm.");
                return;
            }
            int slot = MimicSaveManager.FindFreeSlot(used);
            if (slot < 0)
            {
                mimic.Whisper(client.Player, "Your realm stable is full (10 bots). Delete one first.");
                return;
            }
            string key = MimicSaveManager.StorageKey(account, mimic.Realm, slot);
            var save = MimicSaveManager.Snapshot(mimic, account, mimic.Realm, slot, name);
            if (!GameServer.Database.AddObject(save))
            {
                mimic.Whisper(client.Player, "I could not be saved. Please try again.");
                return;
            }
            // Tame: gear moves into storage, the wild bot leaves the world.
            try
            {
                MimicSaveManager.StoreGear(mimic, key);
            }
            catch (Exception)
            {
                GameServer.Database.DeleteObject(save);
                mimic.Whisper(client.Player, "I could not store my gear. Please try again.");
                return;
            }
            string savedName = name;
            mimic.Delete();
            client.Player.Out.SendMessage($"Saved {savedName} into your {mimic.Realm} stable (slot {slot + 1}/10).",
                eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }
    }

    [CmdAttribute(
    "&mbots",
    ePrivLevel.Player,
    "/mbots - List your saved mimic bots")]
    public class MimicBotsCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            string account = client.Account.Name;
            var rows = MimicSaveManager.SelectAccountRows(account);
            var byRealm = new Dictionary<int, List<DbMimicSave>>();
            foreach (var row in rows)
            {
                if (!byRealm.TryGetValue(row.Realm, out var list))
                {
                    list = new List<DbMimicSave>();
                    byRealm[row.Realm] = list;
                }
                list.Add(row);
            }
            foreach (eRealm realm in new[] { eRealm.Albion, eRealm.Midgard, eRealm.Hibernia })
            {
                if (!byRealm.TryGetValue((int)realm, out var list) || list.Count == 0)
                {
                    client.Player.Out.SendMessage($"{realm}: empty (0/10).", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    continue;
                }
                list.Sort((a, b) => a.Slot.CompareTo(b.Slot));
                foreach (var row in list)
                    client.Player.Out.SendMessage($"{realm} slot {row.Slot + 1}: {row.Name} ({(eCharacterClass)row.CharacterClass} {row.Level}, RL {row.RealmLevel}).",
                        eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }
    }

    [CmdAttribute(
    "&mdelete",
    ePrivLevel.Player,
    "/mdelete <slot|name> - Destroy a saved mimic bot and its stored gear")]
    public class MimicDeleteCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length < 2)
            {
                client.Player.Out.SendMessage("Use: /mdelete <slot|name> (see /mbots).", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            string account = client.Account.Name;
            var rows = MimicSaveManager.SelectAccountRows(account);
            DbMimicSave hit = null;
            if (int.TryParse(args[1], out int slot))
            {
                foreach (var row in rows)
                {
                    if (row.Realm == (int)client.Player.Realm && row.Slot == slot - 1)
                    {
                        hit = row;
                        break;
                    }
                }
            }
            else
            {
                foreach (var row in rows)
                {
                    if (string.Equals(row.Name, args[1], StringComparison.OrdinalIgnoreCase))
                    {
                        hit = row;
                        break;
                    }
                }
            }
            if (hit == null)
            {
                client.Player.Out.SendMessage("No saved bot found. See /mbots.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            string key = MimicSaveManager.StorageKey(hit);
            if (MimicSaveManager.TryGetActive(key, out _))
            {
                client.Player.Out.SendMessage($"{hit.Name} is out with you. Dismiss first, then delete.",
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            MimicSaveManager.DeleteStoredItems(key);
            GameServer.Database.DeleteObject(hit);
            client.Player.Out.SendMessage($"Destroyed saved bot {hit.Name} and its stored gear.",
                eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }
    }

    [CmdAttribute(
    "&mcall",
    ePrivLevel.Player,
    "/mcall <slot|name> - Summon a saved mimic bot to your side")]
    public class MimicCallCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length < 2)
            {
                client.Player.Out.SendMessage("Use: /mcall <slot|name> (see /mbots).", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            string account = client.Account.Name;
            var rows = MimicSaveManager.SelectAccountRows(account);
            DbMimicSave hit = null;
            if (int.TryParse(args[1], out int slot))
            {
                foreach (var row in rows)
                {
                    if (row.Realm == (int)client.Player.Realm && row.Slot == slot - 1)
                    {
                        hit = row;
                        break;
                    }
                }
            }
            else
            {
                foreach (var row in rows)
                {
                    if (string.Equals(row.Name, args[1], StringComparison.OrdinalIgnoreCase))
                    {
                        hit = row;
                        break;
                    }
                }
            }
            if (hit == null)
            {
                client.Player.Out.SendMessage("No saved bot found. See /mbots.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            string key = MimicSaveManager.StorageKey(hit);
            bool regionIsRvR = client.Player.CurrentRegion != null && client.Player.CurrentRegion.IsRvR
                || client.Player.CurrentZone != null && client.Player.CurrentZone.IsRvR;
            if (!MimicSaveManager.MayKeepBotInRegion((eRealm)hit.Realm, client.Player.Realm, regionIsRvR))
            {
                client.Player.Out.SendMessage($"Bots of {(eRealm)hit.Realm} cannot follow you into RvR lands.",
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if (MimicSaveManager.TryGetActive(key, out MimicNPC active))
            {
                if (!active.IsAlive)
                {
                    client.Player.Out.SendMessage($"{hit.Name} must be resurrected before returning to you.",
                        eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }
                active.MoveTo(client.Player.CurrentRegionID, client.Player.X, client.Player.Y, client.Player.Z, client.Player.Heading);
                client.Player.Out.SendMessage($"{hit.Name} is already with you and hurries over.",
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if (MimicSaveManager.ActiveCountForAccount(account) >= MimicSaveManager.MaxActivePerAccount)
            {
                client.Player.Out.SendMessage($"You already lead {MimicSaveManager.MaxActivePerAccount} saved bots.",
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            var mimic = MimicSaveManager.Instantiate(hit, client.Player, out string error);
            if (mimic == null)
            {
                client.Player.Out.SendMessage($"Summoning failed ({error}).", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            client.Player.Out.SendMessage($"{hit.Name} answers your call.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }
    }

    [CmdAttribute(
    "&mdismiss",
    ePrivLevel.Player,
    "/mdismiss - Send a targeted saved bot back into storage")]
    public class MimicDismissCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player.TargetObject is not MimicNPC mimic)
            {
                client.Player.Out.SendMessage("Target one of your saved bots first.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if (string.IsNullOrEmpty(mimic.SavedKey) || !MimicSaveManager.OwnsKey(client.Account.Name, mimic.SavedKey))
            {
                mimic.Whisper(client.Player, "I am not one of your saved bots.");
                return;
            }
            string name = mimic.Name;
            bool dismissed;
            try { dismissed = MimicSaveManager.DismissBot(mimic); }
            catch (Exception) { dismissed = false; }
            if (!dismissed)
            {
                client.Player.Out.SendMessage("Dismissing failed.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            client.Player.Out.SendMessage($"{name} returns to your stable.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }
    }

    [CmdAttribute(
    "&mimic",
    ePrivLevel.Player,
    "/mimic - List all mimic bot commands available to you")]
    public class MimicHelpCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            bool isGM = client.Account.PrivLevel >= (uint)ePrivLevel.GM;
            client.Out.SendCustomTextWindow("Mimic commands", MimicHelpBuilder.BuildText(isGM));
        }
    }

    [CmdAttribute(
    "&mbattle",
    ePrivLevel.GM,
    "/mbattle [Region] (Start/Stop/Clear>)",
    "Regions: Thid. Start - Start spawning. Stop - Stop spawning. Clear - Stop and remove mimics.")]
    public class MimicBattleCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length > 2)
            {
                args[1] = args[1].ToLower();
                args[2] = args[2].ToLower();

                switch (args[1])
                {
                    case "thid":
                    switch (args[2])
                    {
                        case "start": MimicBattlegrounds.ThidBattleground.Start(); break;
                        case "stop": MimicBattlegrounds.ThidBattleground.Stop(); break;
                        case "clear": MimicBattlegrounds.ThidBattleground.Clear(); break;
                    }
                    break;
                }
            }
        }
    }

    [CmdAttribute(
      "&msummon",
      ePrivLevel.Player,
      "/msummon - Summons all mimics in your group.")]
    public class MimicSummonCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player.Group == null)
                return;

            client.Player.Group.MimicGroup.SetCampPoint(null);
            client.Player.Group.MimicGroup.SetPullPoint(null);

            foreach (GameLiving groupMember in client.Player.Group.GetMembersInTheGroup())
            {
                if (groupMember is MimicNPC mimicNPC)
                {
                    mimicNPC.MoveTo(client.Player.CurrentRegionID, client.Player.X, client.Player.Y, client.Player.Z, client.Player.Heading);
                    mimicNPC.MimicBrain.FSM.SetCurrentState(eFSMStateType.WAKING_UP);
                }
            }

            // We need to update the group members and window, or it breaks selecting group members from the group menus
            foreach (GameLiving groupMember in client.Player.Group.GetMembersInTheGroup())
                client.Player.Group.UpdateAllToMember(client.Player, true, false);

            client.Player.Group.UpdateGroupWindow();
        }
    }

    #endregion Admin/GM/Debug/Cheats

    #region MimicGroup

    [CmdAttribute(
       "&mlfg",
       ePrivLevel.Player,
       "/mlfg - Get a list of Mimics that are looking for a group.")]
    public class MimicLfgCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client.Player;

            if (player == null)
                return;

            var entries = MimicLFGManager.GetLFG(player.Level);
            string message;

            if (args.Length < 2)
            {
                message = BuildMessage(entries);
            }
            else
            {
                int index = int.Parse(args[1]) - 1;

                if (index < 0 || index > entries.Count - 1)
                    message = BuildMessage(entries, true);
                else
                {
                    MimicLFGManager.MimicLFGEntry entry = entries[index];

                    int baseChance = 90;

                    if (MimicConfig.LFG_LEVEL_BIAS)
                    {
                        int biasAmount = 5;
                        int levelDifference = player.Level - entry.Level;

                        if (Math.Abs(levelDifference) > 1)
                            baseChance += levelDifference * biasAmount;

                        baseChance = Math.Clamp(baseChance, 5, 95);
                    }

                    if (Util.Chance(baseChance) && !entry.RefusedGroup)
                    {
                        MimicNPC mimic = MimicManager.GetMimic(entry.MimicClass, entry.Level, entry.Name, entry.Gender);

                        if (!GameServer.ServerRules.IsAllowedToGroup(player, mimic, false))
                            return;

                        if (player.Group == null)
                        {
                            player.Group = new Group(player);
                            player.Group.AddMember(player);
                        }

                        if (player.Group.MemberCount < ServerProperties.Properties.GROUP_MAX_MEMBER)
                        {
                            MimicManager.AddMimicToWorld(mimic, new Point3D(player.X, player.Y, player.Z), player.CurrentRegionID);
                            player.Group.AddMember(mimic);
                            MimicLFGManager.Remove(entry);

                            // Send a refreshed list with new indexes to avoid using wrong indexes while leaving the dialogue open
                            entries = MimicLFGManager.GetLFG(player.Level);
                            message = BuildMessage(entries);
                        }
                        else
                            message = BuildMessage(entries, true);
                    }
                    else
                    {
                        if (entry.RefusedGroup)
                            player.Out.SendMessage(entry.Name + " sends, \"Sorry, I've already said no.\"", eChatType.CT_Send, eChatLoc.CL_SystemWindow);
                        else
                            player.Out.SendMessage(entry.Name + " sends, \"No thanks, looking for a different group!\"", eChatType.CT_Send, eChatLoc.CL_SystemWindow);

                        entry.RefusedGroup = true;
                        return;
                    }
                }
            }

            player.Out.SendMessage(message, eChatType.CT_System, eChatLoc.CL_PopupWindow);
        }

        private string BuildMessage(List<MimicLFGManager.MimicLFGEntry> entries, bool invalid = false)
        {
            string message = "--------------------------------\n";

            if (invalid)
                message += "Invalid number selection or group is full\n";
            else if (entries.Any())
            {
                int index = 1;
                foreach (var entry in entries)
                    message += index++.ToString() + ". " + entry.Name + " " + Enum.GetName(typeof(eMimicClass), entry.MimicClass) + " " + entry.Level + "\n";
            }
            else
                message += "No Mimics available.\n";

            return message;
        }
    }

    [CmdAttribute(
        "&mrole",
        ePrivLevel.Player,
        "/mrole (leader/tank/assist/cc/puller) - Set the role of a group member.")]
    public class MimicRoleCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client.Player;
            GameLiving target = player.TargetObject as GameLiving;

            if (player.Group == null || target == null)
                return;

            if (args.Length > 1)
            {
                args[1] = args[1].ToLower();

                bool success = false;

                switch (args[1])
                {
                    case "leader": success = player.Group.MimicGroup.SetLeader(target); break;
                    case "tank": success = player.Group.MimicGroup.SetMainTank(target); break;
                    case "assist": success = player.Group.MimicGroup.SetMainAssist(target); break;
                    case "cc": success = player.Group.MimicGroup.SetMainCC(target); break;
                    case "puller": success = player.Group.MimicGroup.SetMainPuller(target); break;
                }

                if (success && args[1] != "leader")
                    player.Group.MimicGroup.LockRole(args[1]);

                if (!success)
                    player.Out.SendMessage("Failed to set " + args[1], eChatType.CT_Say, eChatLoc.CL_SystemWindow);
            }
        }
    }

    [CmdAttribute(
        "&mcamp",
        ePrivLevel.Player,
        "/mcamp (here/set/remove/aggrorange/filter)- Set where the group camp point is, remove the camp point, the range the group will aggro, and the con level the puller will pull.")]
    public class MimicCampCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client.Player;
            Point3D target = client.Player.GroundTarget;

            if (player.Group == null)
                return;

            if (args.Length > 1)
            {
                args[1] = args[1].ToLower();

                switch (args[1])
                {
                    case "here":
                        player.Group.MimicGroup.SetCampPoint(new Point3D(player.X, player.Y, player.Z));
                        player.Out.SendMessage("Camp point set to your location.", eChatType.CT_Say, eChatLoc.CL_SystemWindow);

                        foreach (GameLiving groupMember in player.Group.GetMembersInTheGroup())
                            if (groupMember is MimicNPC mimic)
                                mimic.Brain.FSM.SetCurrentState(eFSMStateType.CAMP);
                        break;
                    case "set":
                        {
                            if (target == null || player.GetDistance(player.GroundTarget) > 2000)
                        {
                            player.Out.SendMessage("Ground target is too far away.", eChatType.CT_Say, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        player.Group.MimicGroup.SetCampPoint(target);

                        player.Out.SendMessage("Set camp spot.", eChatType.CT_Say, eChatLoc.CL_SystemWindow);

                        foreach (GameLiving groupMember in player.Group.GetMembersInTheGroup())
                            if (groupMember is MimicNPC mimic)
                                mimic.Brain.FSM.SetCurrentState(eFSMStateType.CAMP);
                    }
                    break;

                    case "remove":
                    {
                        if (player.Group.MimicGroup.CampPoint != null)
                        {
                            player.Group.MimicGroup.SetCampPoint(null);
                            player.Out.SendMessage("Removed camp spot.", eChatType.CT_Say, eChatLoc.CL_SystemWindow);
                        }
                        else
                            player.Out.SendMessage("No camp spot to remove.", eChatType.CT_Say, eChatLoc.CL_SystemWindow);

                        foreach (GameLiving groupMember in player.Group.GetMembersInTheGroup())
                        {
                            if (groupMember is MimicNPC mimic)
                            {
                                mimic.Brain.FSM.SetCurrentState(eFSMStateType.FOLLOW_THE_LEADER);
                                mimic.MimicBrain.AggroRange = 3600;
                            }
                        }
                    }
                    break;

                    case "aggrorange":
                    {
                        if (args.Length > 2)
                        {
                            int range = int.Parse(args[2]);

                            if (range < 0 || range > int.MaxValue)
                                range = 550;

                            foreach (GameLiving groupMember in player.Group.GetMembersInTheGroup())
                            {
                                if (groupMember is MimicNPC mimic)
                                {
                                    FSMState mimicState = mimic.Brain.FSM.GetState(eFSMStateType.CAMP);

                                    ((MimicState_Camp)mimicState).AggroRange = range;
                                }
                            }

                            player.Out.SendMessage("Camp aggro range is " + range, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                    }
                    break;

                    case "filter":
                    {
                        if (args.Length > 2)
                        {
                            args[2] = args[2].ToLower();

                            switch (args[2])
                            {
                                case "purple": player.Group.MimicGroup.ConLevelFilter = 3; break;
                                case "red": player.Group.MimicGroup.ConLevelFilter = 2; break;
                                case "orange": player.Group.MimicGroup.ConLevelFilter = 1; break;
                                case "yellow": player.Group.MimicGroup.ConLevelFilter = 0; break;
                                case "blue": player.Group.MimicGroup.ConLevelFilter = -1; break;
                                case "green": player.Group.MimicGroup.ConLevelFilter = -2; break;
                            }
                        }
                    }
                    break;
                }
            }
        }
    }

    [CmdAttribute(
     "&mpull",
     ePrivLevel.Player,
     "/mpull - Set camp and pull points to your location, and have puller pull your target")]
    public class MimicPullCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public void OnCommand(GameClient client, string[] args)
        {
            var player = client.Player;

            if (player.TargetObject is not GameNPC target || !GameServer.ServerRules.IsAllowedToAttack(player, target, true))
                player.Out.SendMessage("Your target cannot be pulled", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            else if (player.Group?.MimicGroup is not MimicGroup mGroup)
                player.Out.SendMessage("You must be grouped with a mimic to use /mpull", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            else if (mGroup.MainPuller is not MimicNPC puller || puller.Brain is not MimicBrain brainPuller)
                player.Out.SendMessage("You must assign a puller to use /mpull", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            else if (puller.Inventory.GetItem(eInventorySlot.DistanceWeapon) == null)
                puller.Whisper(player, "I do not have a ranged weapon equipped");
            else
            {
                mGroup.SetCampPoint(new Point3D(player.X, player.Y, player.Z));
                mGroup.SetPullPoint(new Point2D(player.X, player.Y));

                foreach (GameLiving groupMember in player.Group.GetMembersInTheGroup())
                    if (groupMember is MimicNPC mimic)
                        mimic.Brain.FSM.SetCurrentState(eFSMStateType.CAMP);

                mGroup.ConLevelFilter = puller.GetConLevel(target);
                puller.TargetObject = target;
                brainPuller.LastTargetObject = null;
                brainPuller.PerformPull(target);
            }
        }
    }

    [CmdAttribute(
        "&mpullfrom",
        ePrivLevel.Player,
        "/mpullfrom (here/set/remove) - Set where the group puller should try to pull from.")]
    public class MimicPullFromCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client.Player;
            Point3D target = client.Player.GroundTarget;

            if (player.Group == null)
                return;

            if (args.Length > 1)
            {
                args[1] = args[1].ToLower();

                switch (args[1])
                {
                    case "here":
                        player.Group.MimicGroup.SetPullPoint(new Point2D(player.X, player.Y));
                        player.Out.SendMessage("Pull point set to your location.", eChatType.CT_Say, eChatLoc.CL_SystemWindow);
                        break;
                    case "set":
                        {
                            if (target == null || !player.GroundTargetInView)
                            return;

                        player.Group.MimicGroup.SetPullPoint(target);

                        player.Out.SendMessage("Set position to pull from.", eChatType.CT_Say, eChatLoc.CL_SystemWindow);
                    }
                    break;

                    case "remove":
                    {
                        player.Group.MimicGroup.SetPullPoint(null);

                        player.Out.SendMessage("Removed position to pull from.", eChatType.CT_Say, eChatLoc.CL_SystemWindow);
                    }
                    break;
                }
            }
        }
    }

    [CmdAttribute(
    "&mfollow",
    ePrivLevel.Player,
    "/mfollow - Clear camp and pull points, and have all grouped mimics follow you")]
    public class MimicFollowCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player.Group != null)
            {
                client.Player.Group.MimicGroup.SetCampPoint(null);
                client.Player.Group.MimicGroup.SetPullPoint(null);

                foreach (GameLiving groupMember in client.Player.Group.GetMembersInTheGroup())
                    if (groupMember is MimicNPC mimic)
                        mimic.Brain.FSM.SetCurrentState(eFSMStateType.FOLLOW_THE_LEADER);
            }
        }
    }

    [CmdAttribute(
    "&mattack",
    ePrivLevel.Player,
    "/mattack - Have all grouped mimics attack your target")]
    public class MimicAttackCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public void OnCommand(GameClient client, string[] args)
        {
            if (client.Player.Group != null && client.Player.TargetObject is GameLiving target)
                foreach (GameLiving groupMember in client.Player.Group.GetMembersInTheGroup())
                    if (groupMember is MimicNPC mimic && mimic.Brain is MimicBrain brain
                        && !brain.PreventCombat && !brain.IsHealer)
                    {
                        brain.AddToAggroList(target, brain.GetMaxAggro() + 1);
                        brain.AttackMostWanted();
                    }
        }
    }

    [CmdAttribute(
   "&mintercept",
   ePrivLevel.Player,
   "/mintercept [name/class] - Set a target to intercept.")]
    public class MimicInterceptCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client.Player;
            MimicNPC target = player.TargetObject as MimicNPC;

            if (player.Group == null || target == null || (player.Group != null && !player.Group.IsInTheGroup(target)))
                return;

            if (!target.HasAbility(Abilities.Intercept))
            {
                target.Whisper(player, "I do not have that ability.");
                return;
            }

            GameLiving targetGroupMember = null;

            if (args.Length > 1)
            {
                args[1] = args[1].ToLower();

                eCharacterClass charClass = eCharacterClass.Unknown;
                Enum.TryParse<eCharacterClass>(args[1], true, out charClass);

                foreach (GameLiving groupMember in player.Group.GetMembersInTheGroup())
                {
                    if (groupMember != target &&
                        ((groupMember.Name.Equals(args[1], StringComparison.OrdinalIgnoreCase))
                        || (groupMember is MimicNPC mimic && mimic.CharacterClass.ID == (int)charClass)
                        || (groupMember is GamePlayer play && play.CharacterClass.ID == (int)charClass)))
                    {
                        targetGroupMember = groupMember;
                        break;
                    }
                }

                if (targetGroupMember != null)
                {
                    if (target.MimicBrain.SetIntercept(targetGroupMember, out bool ourEffect))
                        target.Group.SendMessageToGroupMembers(target, "I will intercept for " + targetGroupMember.Name, eChatType.CT_Group, eChatLoc.CL_ChatWindow);
                    else
                    {
                        if (ourEffect)
                            target.Group.SendMessageToGroupMembers(target, "I will stop intercepting for " + targetGroupMember.Name, eChatType.CT_Group, eChatLoc.CL_ChatWindow);
                        else
                            target.Group.SendMessageToGroupMembers(targetGroupMember.Name + " is already being intercepting for.", eChatType.CT_Group, eChatLoc.CL_ChatWindow);
                    }
                }
                else
                    target.Whisper(player, "I could not find " + args[1]);
            }
        }
    }

    [CmdAttribute(
    "&mguard",
    ePrivLevel.Player,
    "/mguard [name/class] - Set a target to guard.")]
    public class MimicGuardCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client.Player;
            MimicNPC target = player.TargetObject as MimicNPC;

            if (target == null || player.Group == null || (player.Group != null && !player.Group.IsInTheGroup(target)))
                return;

            if (!target.HasAbility(Abilities.Guard))
            {
                target.Whisper(player, "I do not have the guard ability.");
                return;
            }

            GameLiving targetGroupMember = null;

            if (args.Length > 1)
            {
                args[1] = args[1].ToLower();

                eCharacterClass charClass = eCharacterClass.Unknown;
                Enum.TryParse<eCharacterClass>(args[1], true, out charClass);

                foreach (GameLiving groupMember in player.Group.GetMembersInTheGroup())
                {
                    if (groupMember != target &&
                        ((groupMember.Name.Equals(args[1], StringComparison.OrdinalIgnoreCase))
                        || (groupMember is MimicNPC mimic && mimic.CharacterClass.ID == (int)charClass)
                        || (groupMember is GamePlayer play && play.CharacterClass.ID == (int)charClass)))
                    {
                        targetGroupMember = groupMember;
                        break;
                    }
                }

                if (targetGroupMember != null)
                {
                    if (target.MimicBrain.SetGuard(targetGroupMember, out bool ourEffect))
                    {
                        target.MimicBrain.SetManualGuard(targetGroupMember);
                        target.Group.SendMessageToGroupMembers(target, "I will guard " + targetGroupMember.Name, eChatType.CT_Group, eChatLoc.CL_ChatWindow);
                    }
                    else
                    {
                        if (ourEffect)
                        {
                            target.MimicBrain.ClearManualGuard();
                            target.Group.SendMessageToGroupMembers(target, "I will stop guarding " + targetGroupMember.Name, eChatType.CT_Group, eChatLoc.CL_ChatWindow);
                        }
                        else
                            target.Group.SendMessageToGroupMembers(targetGroupMember.Name + " is already being guarded.", eChatType.CT_Group, eChatLoc.CL_ChatWindow);
                    }
                }
                else
                    target.Whisper(player, "I could not find " + args[1]);
            }
        }
    }

    [CmdAttribute(
    "&mprotect",
    ePrivLevel.Player,
    "/mprotect [name/class] - Set a target to protect.")]
    public class MimicProtectCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client.Player;
            MimicNPC target = player.TargetObject as MimicNPC;

            if (target == null || player.Group == null || (player.Group != null && !player.Group.IsInTheGroup(target)))
                return;

            if (!target.HasAbility(Abilities.Protect))
            {
                target.Whisper(player, "I do not have the protect ability.");
                return;
            }

            GameLiving targetGroupMember = null;

            if (args.Length > 1)
            {
                eCharacterClass charClass = eCharacterClass.Unknown;
                Enum.TryParse<eCharacterClass>(args[1], true, out charClass);

                foreach (GameLiving groupMember in player.Group.GetMembersInTheGroup())
                {
                    if (groupMember != target &&
                        ((groupMember.Name.Equals(args[1], StringComparison.OrdinalIgnoreCase))
                        || (groupMember is MimicNPC mimic && mimic.CharacterClass.ID == (int)charClass)
                        || (groupMember is GamePlayer play && play.CharacterClass.ID == (int)charClass)))
                    {
                        targetGroupMember = groupMember;
                        break;
                    }
                }

                if (targetGroupMember != null)
                {
                    if (target.MimicBrain.SetProtect(targetGroupMember, out bool ourEffect))
                        target.Group.SendMessageToGroupMembers(target, "I will protect " + targetGroupMember.Name, eChatType.CT_Group, eChatLoc.CL_ChatWindow);
                    else
                    {
                        if (ourEffect)
                            target.Group.SendMessageToGroupMembers("I will stop protecting " + targetGroupMember.Name, eChatType.CT_Group, eChatLoc.CL_ChatWindow);
                        else
                            target.Group.SendMessageToGroupMembers(target, targetGroupMember.Name + " is already being protected.", eChatType.CT_Group, eChatLoc.CL_ChatWindow);
                    }
                }
                else
                    target.Whisper(player, "I could not find " + args[1]);
            }
        }
    }

    #endregion MimicGroup

    [CmdAttribute(
      "&mbstats",
      ePrivLevel.Player,
      "/mbstats [Battleground] - Get stats on a battleground.",
      "[Battleground] - Thid")]    public class MimicBattleStatsCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            if (args.Length > 1)
            {
                args[1] = args[1].ToLower();

                switch (args[1])
                {
                    case "thid": MimicBattlegrounds.ThidBattleground.BattlegroundStats(client.Player); break;
                }
            }
        }
    }
}
