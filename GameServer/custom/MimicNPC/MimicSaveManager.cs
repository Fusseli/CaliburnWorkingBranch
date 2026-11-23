using System;
using System.Collections.Generic;
using System.Linq;
using DOL.Database;
using DOL.Events;
using DOL.GS.ServerProperties;

namespace DOL.GS.Scripts
{
    /// <summary>
    /// Player-owned persistent mimic bots (PR20a: storage + save/list/delete,
    /// PR20b: summon/dismiss). Tame model: saving removes the wild bot from the
    /// world and moves its gear into storage, so items can never duplicate.
    /// </summary>
    public static class MimicSaveManager
    {
        public const int MaxSlotsPerRealm = 10;
        public const int MaxActivePerAccount = 7;

        private static readonly Dictionary<string, MimicNPC> _active = new Dictionary<string, MimicNPC>();
        private static readonly HashSet<string> _pending = new HashSet<string>();
        private static readonly object _activeLock = new object();

        public static string StorageKey(string accountName, eRealm realm, int slot)
            => $"mimic:{accountName}:{(int)realm}:{slot}";

        public static string StorageKey(DbMimicSave row)
            => StorageKey(row.AccountName, (eRealm)row.Realm, row.Slot);

        /// <summary>First free slot in 0..9, or -1 when the realm stable is full.</summary>
        public static int FindFreeSlot(ICollection<int> usedSlots)
        {
            for (int slot = 0; slot < MaxSlotsPerRealm; slot++)
            {
                if (!usedSlots.Contains(slot))
                    return slot;
            }
            return -1;
        }

        public static bool NameTaken(IEnumerable<string> names, string name)
            => names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>Simple name rules (no DB-backed InvalidNames here): letters,
        /// apostrophe and hyphen, 3..20 chars.</summary>
        public static bool IsValidBotName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length < 3 || name.Length > 20)
                return false;
            return name.All(c => char.IsLetter(c) || c == '\'' || c == '-');
        }

        /// <summary>Region gate shared by save and summon: RvR regions only
        /// allow own-realm bots, everywhere else any realm goes.</summary>
        public static bool MayKeepBotInRegion(eRealm botRealm, eRealm playerRealm, bool regionIsRvR)
            => !regionIsRvR || botRealm == playerRealm;

        /// <summary>Builds "Key|Level;" CSV from pairs (specs, RAs).</summary>
        public static string BuildPairCsv(IEnumerable<(string Key, int Level)> pairs)
        {
            var parts = new List<string>();
            foreach (var (key, level) in pairs)
            {
                if (string.IsNullOrEmpty(key))
                    continue;
                parts.Add($"{key}|{level}");
            }
            return string.Join(";", parts);
        }

        /// <summary>Parses "Key|Level;" CSV back into pairs, skipping garbage.</summary>
        public static List<(string Key, int Level)> ParsePairCsv(string csv)
        {
            var result = new List<(string Key, int Level)>();
            if (string.IsNullOrEmpty(csv))
                return result;
            foreach (string entry in csv.Split(';'))
            {
                string[] values = entry.Split('|');
                if (values.Length >= 2 && !string.IsNullOrEmpty(values[0])
                    && int.TryParse(values[1], out int level))
                    result.Add((values[0], level));
            }
            return result;
        }

        /// <summary>Snapshots a live bot into a save row (not yet stored).</summary>
        public static DbMimicSave Snapshot(MimicNPC mimic, string accountName, eRealm realm, int slot, string name)
        {
            var row = new DbMimicSave
            {
                AccountName = accountName,
                Realm = (int)realm,
                Slot = slot,
                Name = name,
            };
            RefreshRow(row, mimic);
            return row;
        }

        /// <summary>Writes live state onto an existing row (dismiss/refresh).</summary>
        public static void RefreshRow(DbMimicSave row, MimicNPC mimic)
        {
            row.CharacterClass = mimic.CharacterClass.ID;
            row.Level = mimic.Level;
            row.Experience = mimic.Experience;
            row.RealmLevel = mimic.RealmLevel;
            row.RealmPoints = mimic.RealmPoints;
            row.MLLine = mimic.MLLine;
            row.MLLevel = mimic.MLLevel;
            row.MLExperience = 0; // wired in ML phase; column reserved
            row.MLGranted = mimic.MLGranted;
            row.Champion = mimic.Champion;
            row.ChampionLevel = mimic.ChampionLevel;
            row.ChampionExperience = 0; // wired in CL phase; column reserved
            row.Race = mimic.Race;
            row.Gender = (int)mimic.Gender;
            row.Model = mimic.Model;
            row.Size = mimic.Size;
            row.SpecType = mimic.MimicSpec != null ? (int)mimic.MimicSpec.SpecType : (int)eSpecType.None;

            var specs = new List<(string Key, int Level)>();
            foreach (Specialization spec in mimic.GetSpecList())
            {
                if (spec == null || string.IsNullOrEmpty(spec.KeyName) || !spec.AllowSave)
                    continue;
                specs.Add((spec.KeyName, spec.Level));
            }
            row.SerializedSpecs = BuildPairCsv(specs);

            var ras = new List<(string Key, int Level)>();
            foreach (var ab in mimic.GetRealmAbilities())
            {
                if (ab == null || string.IsNullOrEmpty(ab.KeyName))
                    continue;
                ras.Add((ab.KeyName, ab.Level));
            }
            row.SerializedRealmAbilities = BuildPairCsv(ras);
        }

        /// <summary>Restores trained spec levels onto a fresh bot (PR20b summon).</summary>
        public static void RestoreSpecs(MimicNPC mimic, string csv)
        {
            mimic.LoadClassSpecializations(false);
            foreach (var (key, level) in ParsePairCsv(csv))
            {
                string restoredKey = key;
                string classBow = mimic.CharacterClass.ID switch
                {
                    (int)eCharacterClass.Scout => Specs.Longbow,
                    (int)eCharacterClass.Hunter => Specs.CompositeBow,
                    (int)eCharacterClass.Ranger => Specs.RecurveBow,
                    _ => null,
                };
                if (classBow != null)
                {
                    if (!Properties.ALLOW_OLD_ARCHERY && key == classBow)
                        restoredKey = Specs.Archery;
                    else if (Properties.ALLOW_OLD_ARCHERY && key == Specs.Archery)
                        restoredKey = classBow;
                }
                Specialization spec = SkillBase.GetSpecialization(restoredKey, false);
                if (spec == null || !spec.AllowSave)
                    continue;
                if (mimic.HasSpecialization(restoredKey))
                    mimic.GetSpecializationByName(restoredKey).Level = level;
                else
                {
                    spec.Level = level;
                    mimic.AddSpecialization(spec);
                }
            }
            mimic.RefreshSpecDependantSkills(false);
        }

        /// <summary>Restores realm abilities onto a fresh bot (PR20b summon).</summary>
        public static void RestoreRealmAbilities(MimicNPC mimic, string csv)
        {
            List<RealmAbilities.RealmAbility> classRAs;
            try
            {
                classRAs = SkillBase.GetClassRealmAbilities(mimic.CharacterClass.ID);
            }
            catch
            {
                return;
            }
            if (classRAs == null)
                return;
            foreach (var (key, level) in ParsePairCsv(csv))
            {
                var inst = classRAs.FirstOrDefault(r => r != null && r.KeyName == key);
                if (inst == null)
                    continue;
                inst.Level = level;
                mimic.AddRealmAbility(inst, false);
            }
        }

        public static IList<DbMimicSave> SelectAccountRows(string accountName)
            => DOLDB<DbMimicSave>.SelectObjects(DB.Column("AccountName").IsEqualTo(accountName));

        public static IList<DbInventoryItem> SelectStoredItems(string storageKey)
            => DOLDB<DbInventoryItem>.SelectObjects(DB.Column("OwnerID").IsEqualTo(storageKey));

        public static void DeleteStoredItems(string storageKey)
        {
            foreach (var item in SelectStoredItems(storageKey))
                GameServer.Database.DeleteObject(item);
        }

        public static bool TryParseKey(string key, out string accountName, out eRealm realm, out int slot)
        {
            accountName = null;
            realm = eRealm.None;
            slot = -1;
            if (string.IsNullOrEmpty(key))
                return false;
            string[] parts = key.Split(':');
            if (parts.Length != 4 || parts[0] != "mimic")
                return false;
            accountName = parts[1];
            if (!int.TryParse(parts[2], out int realmNum) || !int.TryParse(parts[3], out slot))
                return false;
            realm = (eRealm)realmNum;
            return true;
        }

        public static bool IsActive(string storageKey)
        {
            lock (_activeLock)
                return _active.ContainsKey(storageKey);
        }

        public static bool TryGetActive(string storageKey, out MimicNPC mimic)
        {
            lock (_activeLock)
            {
                if (_active.TryGetValue(storageKey, out mimic))
                {
                    // Self-healing registry: bots deleted by other paths (GM,
                    // cleanup, duel cascade) must not block resummoning forever.
                    if (mimic == null || mimic.ObjectState != GameObject.eObjectState.Active)
                    {
                        _active.Remove(storageKey);
                        mimic = null;
                        return false;
                    }
                    return true;
                }
                mimic = null;
                return false;
            }
        }

        public static void Unregister(string storageKey)
        {
            lock (_activeLock)
                _active.Remove(storageKey);
        }

        public static int ActiveCountForAccount(string accountName)
        {
            lock (_activeLock)
            {
                int count = 0;
                foreach (string key in _active.Keys)
                {
                    if (key.StartsWith("mimic:" + accountName + ":", StringComparison.Ordinal))
                        count++;
                }
                foreach (string key in _pending)
                {
                    if (key.StartsWith("mimic:" + accountName + ":", StringComparison.Ordinal))
                        count++;
                }
                return count;
            }
        }

        public static bool OwnsKey(string accountName, string storageKey)
            => !string.IsNullOrEmpty(storageKey)
            && storageKey.StartsWith("mimic:" + accountName + ":", StringComparison.Ordinal);

        /// <summary>Moves the bot's current gear into storage (old stored rows
        /// are replaced). The world instance is left naked but alive.</summary>
        public static void StoreGear(MimicNPC mimic, string storageKey)
        {
            string[] oldItemIds = SelectStoredItems(storageKey).Select(item => item.ObjectId).ToArray();
            DbInventoryItem[] worn = mimic.Inventory.AllItems.ToArray();
            var added = new List<DbInventoryItem>();
            try
            {
                foreach (DbInventoryItem item in worn)
                {
                    var copy = new GameInventoryItem(item) { ObjectId = null, OwnerID = storageKey };
                    if (!GameServer.Database.AddObject(copy))
                        throw new InvalidOperationException($"Could not store {item.Name} for {mimic.Name}");
                    added.Add(copy);
                }
            }
            catch
            {
                foreach (DbInventoryItem item in added)
                    GameServer.Database.DeleteObject(item);
                throw;
            }
            foreach (string oldId in oldItemIds)
            {
                if (string.IsNullOrEmpty(oldId))
                    continue;
                var oldItem = GameServer.Database.FindObjectByKey<DbInventoryItem>(oldId);
                if (oldItem != null)
                    GameServer.Database.DeleteObject(oldItem);
            }
            foreach (DbInventoryItem item in worn)
                mimic.Inventory.RemoveItemWithoutDbDeletion(item);
            mimic.RefreshItemBonuses();
        }

        /// <summary>Refreshes the snapshot of a live saved bot without
        /// despawning it (/msave on your own active bot).</summary>
        public static bool RefreshActive(MimicNPC mimic)
        {
            if (mimic == null || string.IsNullOrEmpty(mimic.SavedKey))
                return false;
            if (!TryParseKey(mimic.SavedKey, out string account, out eRealm realm, out int slot))
                return false;
            DbMimicSave hit = null;
            foreach (var row in SelectAccountRows(account))
            {
                if (row.Realm == (int)realm && row.Slot == slot)
                {
                    hit = row;
                    break;
                }
            }
            if (hit == null)
            {
                Unregister(mimic.SavedKey);
                mimic.SavedKey = null;
                return false;
            }
            RefreshRow(hit, mimic);
            if (!GameServer.Database.SaveObject(hit))
                return false;
            // The active bot keeps wearing its gear. Gear is written when it is
            // dismissed, including during logout and orderly shutdown.
            return true;
        }

        /// <summary>Builds a live bot from a save row at the owner's side and
        /// joins the owner's group when realms allow. Registers the instance so
        /// a row can only be out once.</summary>
        public static MimicNPC Instantiate(DbMimicSave row, GamePlayer owner, out string error)
        {
            error = null;
            string key = StorageKey(row);
            lock (_activeLock)
            {
                if (_active.ContainsKey(key) || _pending.Contains(key))
                {
                    error = "already";
                    return null;
                }
                if (ActiveCountForAccount(row.AccountName) >= MaxActivePerAccount)
                {
                    error = "limit";
                    return null;
                }
                _pending.Add(key);
            }
            try
            {
            if (!Enum.IsDefined(typeof(eMimicClass), row.CharacterClass)
                || (eMimicClass)row.CharacterClass == eMimicClass.None)
            {
                error = "class";
                return null;
            }
            var mimic = MimicManager.GetMimic((eMimicClass)row.CharacterClass, (byte)Math.Min(50, Math.Max(1, row.Level)),
                row.Name, (eGender)row.Gender, (eSpecType)row.SpecType);
            if (mimic == null || mimic.CharacterClass == null)
            {
                error = "create";
                return null;
            }
            mimic.Race = (short)row.Race;
            mimic.Gender = (eGender)row.Gender;
            mimic.Model = (ushort)row.Model;
            mimic.Size = (byte)row.Size;
            mimic.Name = row.Name;
            mimic.Experience = row.Experience;
            mimic.RealmLevel = row.RealmLevel;
            mimic.RealmPoints = row.RealmPoints;
            mimic.MLLevel = row.MLLevel;
            mimic.MLLine = (byte)row.MLLine;
            mimic.MLGranted = row.MLGranted;
            mimic.Champion = row.Champion;
            mimic.ChampionLevel = row.ChampionLevel;
            RestoreSpecs(mimic, row.SerializedSpecs);
            RestoreRealmAbilities(mimic, row.SerializedRealmAbilities);

            var stored = SelectStoredItems(key);
            if (stored.Count > 0)
            {
                foreach (var old in mimic.Inventory.AllItems.ToArray())
                    mimic.Inventory.RemoveItemWithoutDbDeletion(old);
                foreach (var item in stored)
                {
                    var copy = new GameInventoryItem(item) { ObjectId = null, OwnerID = null };
                    if (!mimic.Inventory.AddItem((eInventorySlot)item.SlotPosition, copy) &&
                        !mimic.Inventory.AddItem(eInventorySlot.FirstEmptyBackpack, copy))
                    {
                        error = "inventory";
                        return null;
                    }
                }
                mimic.RefreshItemBonuses();
            }
            mimic.Health = mimic.MaxHealth;
            mimic.Mana = mimic.MaxMana;
            mimic.Endurance = mimic.MaxEndurance;
            mimic.SavedKey = key;

            if (!MimicManager.AddMimicToWorld(mimic,
                new Point3D(owner.X, owner.Y, owner.Z), owner.CurrentRegionID))
            {
                error = "world";
                return null;
            }

            if (GameServer.ServerRules.IsSameRealm(owner, mimic, true))
            {
                if (owner.Group == null)
                {
                    owner.Group = new Group(owner);
                    owner.Group.AddMember(owner);
                }
                owner.Group.AddMember(mimic);
            }

            lock (_activeLock)
                _active[key] = mimic;
            return mimic;
            }
            finally
            {
                lock (_activeLock)
                    _pending.Remove(key);
            }
        }

        /// <summary>Writes a live saved bot back to its row + storage and
        /// despawns it. Returns false without despawning if storage fails.</summary>
        public static bool DismissBot(MimicNPC mimic)
        {
            if (mimic == null)
                return false;
            string key = mimic.SavedKey;
            if (string.IsNullOrEmpty(key))
                return false;
            if (!TryParseKey(key, out string account, out eRealm realm, out int slot))
                return false;
                DbMimicSave hit = null;
                foreach (var row in SelectAccountRows(account))
                {
                    if (row.Realm == (int)realm && row.Slot == slot)
                    {
                        hit = row;
                        break;
                    }
                }
                if (hit == null)
                    return false;
                RefreshRow(hit, mimic);
                if (!GameServer.Database.SaveObject(hit))
                    return false;
                StoreGear(mimic, key);
            lock (_activeLock)
                _active.Remove(key);
            mimic.SavedKey = null;
            mimic.Delete();
            return true;
        }

        /// <summary>Dismisses all live saved bots of one account (logout).</summary>
        public static void DismissOwnerBots(GamePlayer owner)
        {
            if (owner == null || owner.Client?.Account == null)
                return;
            string prefix = "mimic:" + owner.Client.Account.Name + ":";
            var mine = new List<MimicNPC>();
            lock (_activeLock)
            {
                foreach (var kv in _active)
                {
                    if (kv.Key.StartsWith(prefix, StringComparison.Ordinal))
                        mine.Add(kv.Value);
                }
            }
            foreach (var mimic in mine)
            {
                try { DismissBot(mimic); }
                catch (Exception ex)
                {
                    log.Error($"DismissOwnerBots failed for {mimic?.Name}", ex);
                }
            }
        }

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>Orderly shutdown: every live saved bot is written back.</summary>
        [GameServerStoppedEvent]
        public static void OnServerStopped(DOLEvent e, object sender, EventArgs arguments)
        {
            var all = new List<MimicNPC>();
            lock (_activeLock)
            {
                foreach (var kv in _active)
                    all.Add(kv.Value);
            }
            foreach (var mimic in all)
            {
                try { DismissBot(mimic); }
                catch (Exception ex)
                {
                    log.Error($"OnServerStopped dismiss failed for {mimic?.Name}", ex);
                }
            }
        }
    }
}
