using DOL.AI;
using DOL.GS.PacketHandler;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace DOL.GS.Scripts
{
    public class MimicSpawnerPersistent : GameNPC
    {
        public bool IsRunning { get { return _timer.IsAlive; } }
        public List<MimicNPC> Mimics { get { return _mimics; } }
        public int SpawnCount { get { return _spawnCount; } }
        public bool SpawnAndStop { get; set; }

        public override eGameObjectType GameObjectType => eGameObjectType.NPC;

        private int _spawnCount;
        private ECSGameTimer _timer;
        private int _dormantInterval;
        private int _timerIntervalMin = 15000; // 15 seconds
        private int _timerIntervalMax = 30000; // 30 seconds

        private List<MimicNPC> _mimics;

        public int LevelMin => base.Strength;
        public int LevelMax => base.Dexterity;
        public int SpawnMin => base.Intelligence;
        public int SpawnMax => base.Quickness;
        public int MinGroupSize => base.Constitution;
        public int MaxGroupSize => base.Charisma;

        // NEW: toggle Battleground group logic via Piety
        public bool UseBattlegroundGroupLogic => base.Piety == 1;

        // NEW: Empathy controls Battleground group chance (0–100)
        public int BattlegroundGroupChance => Math.Min(Math.Max((int)base.Empathy, 0), 100);

        Group mimicGroup = null;
        bool deleteAllOnNextTick = false;

        private int TimerCallback(ECSGameTimer timer)
        {
            int interval = Util.Random(_timerIntervalMin, _timerIntervalMax);

            if (_mimics.Count >= SpawnMax)
                return interval;

            var playersInRegion = ClientService.GetPlayersOfRegion(CurrentRegion)
                .Where(a => a.Client != null && a.Client.Account?.PrivLevel == (uint)ePrivLevel.Player).ToList();
            if (!HasIgnorePlayerCheck && playersInRegion.Count == 0)
            {
                if (deleteAllOnNextTick)
                {
                    if (Mimics.Count > 0)
                    {
                        log.Info($"MimicSpawner {this.Name} deleting {Mimics.Count} mimics! There's been nothing in this region for 1 minutes");
                    }
                    foreach (var mimic in Mimics.ToList())
                    {
                        mimic.RemoveFromWorld();
                        mimic.Delete();
                        Remove(mimic);
                    }
                    deleteAllOnNextTick = false;
                }
                else
                {
                    deleteAllOnNextTick = true;
                    log.Info($"MimicSpawner {this.Name} has nothing in region, going to sleep for 1mins");
                }
                return 1000 * 60 * 5;
            }

            // -------------------------
            // GROUP HANDLING SECTION
            // -------------------------
            if (UseBattlegroundGroupLogic)
            {
                // Spawn a small batch (like Battleground spawner)
                int batchSize = Util.Random(2, 16); // configurable batch size
                List<MimicNPC> spawnedBatch = new List<MimicNPC>();

                for (int i = 0; i < batchSize; i++)
                {
                    if (_mimics.Count >= SpawnMax)
                        break;

                    int randomX = Util.Random(-100, 100);
                    int randomY = Util.Random(-100, 100);
                    Point3D spawnPoint = new Point3D(this.X + randomX, this.Y + randomY, this.Z);

                    eMimicClass mimicClass = MimicManager.GetRandomMimicClass(this.Realm);
                    MimicNPC mimicNPC = MimicManager.GetMimic(mimicClass, (byte)Util.Random(LevelMin, LevelMax));

                    if (MimicManager.AddMimicToWorld(mimicNPC, spawnPoint, this.CurrentRegionID))
                    {
                        _mimics.Add(mimicNPC);
                        mimicNPC.MimicSpawnerPersistent = this;
                        spawnedBatch.Add(mimicNPC);

                        if (SpawnAndStop)
                            _spawnCount++;
                    }
                }

                // Apply Battleground-style group logic to the batch
                if (spawnedBatch.Count > 0)
                {
                    SetGroupMembersBattleground(spawnedBatch);
                    log.Info($"BG Mode: spawned {spawnedBatch.Count} mimics, applied group logic with {BattlegroundGroupChance}% base chance");
                }
            }
            else if (MinGroupSize >= 1 && MaxGroupSize > 1)
            {
                // Original deterministic group spawn
                int grpCount = Util.Random(MinGroupSize, MaxGroupSize > MinGroupSize ? MaxGroupSize : MinGroupSize);
                for (int i = 1; i <= grpCount; i++)
                {
                    int randomX = Util.Random(-100, 100);
                    int randomY = Util.Random(-100, 100);

                    Point3D spawnPoint = new Point3D(this.X + randomX, this.Y + randomY, this.Z);

                    eMimicClass mimicClass = MimicManager.GetRandomMimicClass(this.Realm);
                    MimicNPC mimicNPC = MimicManager.GetMimic(mimicClass, (byte)Util.Random(LevelMin, LevelMax));

                    if (MimicManager.AddMimicToWorld(mimicNPC, spawnPoint, this.CurrentRegionID))
                    {
                        _mimics.Add(mimicNPC);
                        mimicNPC.MimicSpawnerPersistent = this;

                        if (mimicGroup == null)
                        {
                            mimicGroup = new Group(mimicNPC);
                            mimicGroup.AddMember(mimicNPC);
                        }
                        else
                        {
                            mimicGroup.AddMember(mimicNPC);
                        }

                        if (SpawnAndStop)
                            _spawnCount++;
                    }
                }
                log.Info($"mimicSpawnerPersistent spawned {grpCount} mimics min:{MinGroupSize}/max:{MaxGroupSize}!");
            }
            else
            {
                // Original single mimic spawn
                int randomX = Util.Random(-100, 100);
                int randomY = Util.Random(-100, 100);

                Point3D spawnPoint = new Point3D(this.X + randomX, this.Y + randomY, this.Z);

                eMimicClass mimicClass = MimicManager.GetRandomMimicClass(this.Realm);
                MimicNPC mimicNPC = MimicManager.GetMimic(mimicClass, (byte)Util.Random(LevelMin, LevelMax));

                if (MimicManager.AddMimicToWorld(mimicNPC, spawnPoint, this.CurrentRegionID))
                {
                    _mimics.Add(mimicNPC);
                    mimicNPC.MimicSpawnerPersistent = this;

                    if (SpawnAndStop)
                        _spawnCount++;
                }
            }
            return interval;
        }

        // Battleground-style group assignment (batch-only)
        private void SetGroupMembersBattleground(List<MimicNPC> list)
        {
            if (list.Count > 1)
            {
                int groupChance = BattlegroundGroupChance;
                int groupLeaderIndex = -1;

                for (int i = 0; i < list.Count; i++)
                {
                    if (i + 1 < list.Count)
                    {
                        if (Util.Chance(groupChance) && !(list[i].Group?.GetMembersInTheGroup().Count > 7))
                        {
                            if (groupLeaderIndex == -1)
                            {
                                list[i].Group = new Group(list[i]);
                                list[i].Group.AddMember(list[i]);
                                groupLeaderIndex = i;
                            }

                            list[groupLeaderIndex].Group.AddMember(list[i + 1]);
                            groupChance -= 5;
                        }
                        else
                        {
                            groupLeaderIndex = -1;
                            groupChance = BattlegroundGroupChance;
                        }
                    }
                }
            }
        }

        // Old global grouping logic (kept for compatibility)
        private void SetGroupMembers(List<MimicNPC> list)
        {
            if (list.Count > 1)
            {
                int groupChance = BattlegroundGroupChance;
                int groupLeaderIndex = -1;

                for (int i = 0; i < list.Count; i++)
                {
                    if (i + 1 < list.Count)
                    {
                        if (Util.Chance(groupChance) && !(list[i].Group?.GetMembersInTheGroup().Count > 7))
                        {
                            if (groupLeaderIndex == -1)
                            {
                                list[i].Group = new Group(list[i]);
                                list[i].Group.AddMember(list[i]);
                                groupLeaderIndex = i;
                            }

                            list[groupLeaderIndex].Group.AddMember(list[i + 1]);
                            groupChance -= 5;
                        }
                        else
                        {
                            groupLeaderIndex = -1;
                            groupChance = BattlegroundGroupChance;
                        }
                    }
                }
            }
        }

        public void Remove(MimicNPC mimic)
        {
            if (mimic == null) return;
            lock (_mimics)
            {
                if (_mimics.Contains(mimic))
                    _mimics.Remove(mimic);
            }
        }

        public override short Strength { get { OnChangeInfo(); return base.Strength; } set => base.Strength = value; }
        public override short Dexterity { get { OnChangeInfo(); return base.Dexterity; } set => base.Dexterity = value; }
        public override short Intelligence { get { OnChangeInfo(); return base.Intelligence; } set => base.Intelligence = value; }
        public override short Quickness { get { OnChangeInfo(); return base.Quickness; } set => base.Quickness = value; }
        public override short Constitution { get { OnChangeInfo(); return base.Constitution; } set => base.Constitution = value; }
        public override short Charisma { get { OnChangeInfo(); return base.Charisma; } set => base.Charisma = value; }
        public override short Piety { get { OnChangeInfo(); return base.Piety; } set => base.Piety = value; }
        public override short Empathy { get { OnChangeInfo(); return base.Empathy; } set => base.Empathy = value; }

        public void OnChangeInfo()
        {
            if (_mimics == null) return;

            Say($"My spawner stats have changed, talk to me to see my new stats! Killed {_mimics.Count} mimics");

            foreach (var mimic in _mimics.ToList())
                mimic.RemoveFromWorld();

            _mimics.Clear();
            mimicGroup = null;
        }

        public override bool AddToWorld()
        {
            if (this.LoadedFromScript)
            {
                this.Strength = 10;
                this.Dexterity = 15;
                this.Intelligence = 1;
                this.Quickness = 5;
                this.Constitution = 1;
                this.Charisma = 1;
                this.Piety = 0;    // Battleground mode OFF by default
                this.Empathy = 50; // Default Battleground group chance
            }

            _mimics = new List<MimicNPC>();
            _dormantInterval = 5000;
            SpawnAndStop = false;

            if (_timer != null) { _timer.Stop(); _timer = null; }
            if (MimicSpawning.MimicSpawnersPersistent.Contains(this))
                MimicSpawning.MimicSpawnersPersistent.Remove(this);

            _timer = new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(TimerCallback),
                Util.Random(_timerIntervalMin, _timerIntervalMax));
            MimicSpawning.MimicSpawnersPersistent.Add(this);

            Flags |= eFlags.PEACE;

            return base.AddToWorld();
        }

        public override int ChangeHealth(GameObject changeSource, eHealthChangeType healthChangeType, int changeAmount) => 0;

        public override bool IsVisibleTo(GameObject checkObject)
        {
            if (checkObject is GamePlayer player && player.Client.Account.PrivLevel == 1)
                return false;
            return base.IsVisibleTo(checkObject);
        }

        public bool HasIgnorePlayerCheck => !string.IsNullOrEmpty(PackageID) && PackageID.Contains("IGNORE_PLAYERCHECK");

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player)) return false;

            player.Out.SendMessage(
                "---------------------------------------\n" +
                $"Realm: {this.Realm} (Realm)\n" +
                $"LevelMin: {base.Strength} (Strength)\n" +
                $"LevelMax: {base.Dexterity} (Dexterity)\n" +
                $"SpawnMin: {base.Intelligence} (Intelligence)\n" +
                $"SpawnMax: {base.Quickness} (Quickness)\n" +
                $"MinGroupSize: {base.Constitution} (Constitution)\n" +
                $"MaxGroupSize: {base.Charisma} (Charisma)\n" +
                $"UseBattlegroundGroupLogic: {UseBattlegroundGroupLogic} (Piety = 1 to enable)\n" +
                $"BattlegroundGroupChance: {BattlegroundGroupChance}% (Empathy stat)\n" +
                $"IgnorePlayerCheck: {HasIgnorePlayerCheck} (add IGNORE_PLAYERCHECK to packageid)\n" +
                "\n" +
                "Running: " + _timer.IsAlive + "\n" +
                "Spawns: " + _mimics.Count + "/" + SpawnMax + "\n\n" +
                "[Toggle]\n" +
                "[List]\n\n" +
                "[Delete]",
                eChatType.CT_Say, eChatLoc.CL_PopupWindow);
            return true;
        }

        public override bool WhisperReceive(GameLiving source, string str)
        {
            if (!base.WhisperReceive(source, str)) return false;
            GamePlayer player = source as GamePlayer;
            if (player == null) return false;

            string message = string.Empty;

            switch (str)
            {
                case "Toggle":
                    if (_timer.IsAlive) { _timer.Stop(); message = "Spawner is no longer running."; }
                    else { _timer.Start(); message = "Spawner is now running."; }
                    break;

                case "Delete":
                    if (MimicSpawning.MimicSpawnersPersistent.Contains(this))
                    {
                        MimicSpawning.MimicSpawnersPersistent.Remove(this);
                        if (_timer.IsAlive) _timer.Stop();
                        _timer = null;
                        message = "Spawner has been deleted.";
                    }
                    Delete();
                    break;

                case "List":
                    foreach (MimicNPC mimic in _mimics)
                        message += mimic.Name + " " + mimic.CharacterClass.Name + " " + mimic.Level + " Region: " + mimic.CurrentRegionID + "\n";
                    break;

                default: break;
            }

            if (message.Length > 0)
                player.Out.SendMessage(message, eChatType.CT_System, eChatLoc.CL_PopupWindow);

            return true;
        }

        public void Stop() { if (_timer.IsAlive) _timer.Stop(); }
        public void Start() { if (!_timer.IsAlive) _timer.Start(); }
    }
}
