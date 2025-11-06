using DOL.AI;
using DOL.GS.PacketHandler;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DOL.GS.Scripts
{
    public class MimicSpawnerPersistent : GameNPC
    {
        public bool IsRunning => _timer?.IsAlive == true;
        public List<MimicNPC> Mimics => _mimics;
        public int SpawnCount => _spawnCount;
        public bool SpawnAndStop { get; set; }

        public override eGameObjectType GameObjectType => eGameObjectType.NPC;

        private int _spawnCount;
        private ECSGameTimer _timer;
        private int _timerIntervalMin = 15000; // 15 seconds
        private int _timerIntervalMax = 30000; // 30 seconds
        private readonly List<MimicNPC> _mimics = new();
        private bool deleteAllOnNextTick = false;

        // Region player cache (shared across instances)
        private static readonly ConcurrentDictionary<ushort, (DateTime, int)> _regionPlayerCache = new();

        // Stat bindings
        public int LevelMin => base.Strength;
        public int LevelMax => base.Dexterity;
        public int SpawnMin => base.Intelligence;
        public int SpawnMax => base.Quickness;
        public int MinGroupSize => base.Constitution;
        public int MaxGroupSize => base.Charisma;

        public bool UseBattlegroundGroupLogic => base.Piety == 1;
        public int BattlegroundGroupChance => Math.Min(Math.Max((int)base.Empathy, 0), 100);

        // =======================================
        // Timer Callback (lightweight)
        // =======================================
        private int TimerCallback(ECSGameTimer timer)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { DoSpawnTick(); }
                catch (Exception e) { log.Error($"[MimicSpawnerPersistent] Error in DoSpawnTick ({Name})", e); }
            });

            return Util.Random(_timerIntervalMin, _timerIntervalMax);
        }

        // =======================================
        // Main spawn logic (off-thread)
        // =======================================
        private void DoSpawnTick()
        {
            if (_mimics.Count >= SpawnMax)
                return;

            int playersInRegion = GetCachedPlayersOfRegion(CurrentRegion);

            if (!HasIgnorePlayerCheck && playersInRegion == 0)
            {
                HandleEmptyRegion();
                return;
            }

            if (UseBattlegroundGroupLogic)
                SpawnBatchGroup(true);
            else if (MinGroupSize >= 1 && MaxGroupSize > 1)
                SpawnBatchGroup(false);
            else
                QueueSpawnSingle();
        }

        private void HandleEmptyRegion()
        {
            if (deleteAllOnNextTick)
            {
                int count = _mimics.Count;
                if (count > 0 && log.IsDebugEnabled)
                    log.Debug($"MimicSpawner {Name}: deleting {count} mimics (region empty).");

                lock (_mimics)
                {
                    foreach (var mimic in _mimics.ToList())
                    {
                        mimic.RemoveFromWorld();
                        mimic.Delete();
                        _mimics.Remove(mimic);
                    }
                }
                deleteAllOnNextTick = false;
            }
            else
            {
                deleteAllOnNextTick = true;
                if (log.IsDebugEnabled)
                    log.Debug($"MimicSpawner {Name}: going dormant (no players).");
            }
        }

        // =======================================
        // Group / batch spawning
        // =======================================
        private void SpawnBatchGroup(bool useBattlegroundLogic)
        {
            int batchSize = Util.Random(3, 8);
            int delayIncrement = 400;
            List<MimicNPC> batch = new();

            for (int i = 0; i < batchSize; i++)
            {
                int delay = i * delayIncrement;

                new ECSGameTimer(this, t =>
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try
                        {
                            var mimic = SpawnSingleMimic();
                            if (mimic == null)
                                return;

                            lock (_mimics) _mimics.Add(mimic);
                            batch.Add(mimic);

                            // Kickstart brain immediately on spawn
                            mimic.Brain?.Think();

                            // Wait until all are spawned before grouping
                            if (batch.Count == batchSize)
                            {
                                // Give them 100ms to finish brain setup
                                Thread.Sleep(100);

                                if (useBattlegroundLogic)
                                    SetGroupMembersBattleground(batch);
                                else
                                    SetGroupMembers(batch);

                                // Wake up leader explicitly
                                var leader = batch.FirstOrDefault(m => m.Group != null && m.Group.Leader == m);
                                if (leader != null)
                                {
                                    // Ensure leader’s brain has run at least once post-grouping
                                    leader.Brain?.Think();
                                }

                                // Sync followers one more time
                                foreach (var m in batch)
                                {
                                    if (m != leader)
                                        m.Brain?.Think();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Error($"[MimicSpawnerPersistent] Error in group spawn ({Name})", ex);
                        }
                    });
                    return 0;
                }, delay);
            }
        }

        private void QueueSpawnSingle()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var mimic = SpawnSingleMimic();
                    if (mimic != null)
                    {
                        lock (_mimics) _mimics.Add(mimic);
                        mimic.Brain?.Think(); // kickstart solo AI
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"[MimicSpawnerPersistent] Error in single spawn ({Name})", ex);
                }
            });
        }

        // =======================================
        // Cached Player Count
        // =======================================
        private int GetCachedPlayersOfRegion(Region region)
        {
            if (region == null) return 0;

            var regionId = region.ID;
            var now = DateTime.Now;

            if (_regionPlayerCache.TryGetValue(regionId, out var entry))
            {
                if ((now - entry.Item1).TotalSeconds < 5)
                    return entry.Item2;
            }

            int count = ClientService.GetPlayersOfRegion(region)
                .Count(a => a?.Client?.Account?.PrivLevel == (uint)ePrivLevel.Player);

            _regionPlayerCache[regionId] = (now, count);
            return count;
        }

        // =======================================
        // Spawn helper
        // =======================================
        private MimicNPC SpawnSingleMimic()
        {
            if (_mimics.Count >= SpawnMax)
                return null;

            int randomX = Util.Random(-100, 100);
            int randomY = Util.Random(-100, 100);
            Point3D spawnPoint = new Point3D(X + randomX, Y + randomY, Z);

            eMimicClass mimicClass = MimicManager.GetRandomMimicClass(Realm);
            MimicNPC mimicNPC = MimicManager.GetMimic(mimicClass, (byte)Util.Random(LevelMin, LevelMax));

            if (MimicManager.AddMimicToWorld(mimicNPC, spawnPoint, CurrentRegionID))
            {
                mimicNPC.MimicSpawnerPersistent = this;
                if (SpawnAndStop)
                    _spawnCount++;
                return mimicNPC;
            }

            return null;
        }

        // =======================================
        // Grouping logic
        // =======================================
        private void SetGroupMembersBattleground(List<MimicNPC> list)
        {
            if (list.Count <= 1) return;

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

        private void SetGroupMembers(List<MimicNPC> list)
        {
            if (list.Count <= 1) return;

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

        // =======================================
        // Overrides
        // =======================================
        public void Remove(MimicNPC mimic)
        {
            if (mimic == null) return;
            lock (_mimics)
                _mimics.Remove(mimic);
        }

        public override bool AddToWorld()
        {
            if (LoadedFromScript)
            {
                Strength = 10;
                Dexterity = 15;
                Intelligence = 1;
                Quickness = 5;
                Constitution = 1;
                Charisma = 1;
                Piety = 0;
                Empathy = 50;
            }

            _mimics.Clear();
            SpawnAndStop = false;

            _timer?.Stop();
            _timer = null;

            MimicSpawning.MimicSpawnersPersistent.Remove(this);

            int initialDelay = Util.Random(0, 10000);
            _timer = new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(TimerCallback),
                Util.Random(_timerIntervalMin, _timerIntervalMax) + initialDelay);

            MimicSpawning.MimicSpawnersPersistent.Add(this);
            Flags |= eFlags.PEACE;

            return base.AddToWorld();
        }

        public bool HasIgnorePlayerCheck => !string.IsNullOrEmpty(PackageID) && PackageID.Contains("IGNORE_PLAYERCHECK");

        public override int ChangeHealth(GameObject changeSource, eHealthChangeType healthChangeType, int changeAmount) => 0;

        public override bool IsVisibleTo(GameObject checkObject)
        {
            if (checkObject is GamePlayer player && player.Client.Account.PrivLevel == 1)
                return false;
            return base.IsVisibleTo(checkObject);
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player)) return false;

            player.Out.SendMessage(
                "---------------------------------------\n" +
                $"Realm: {Realm} (Realm)\n" +
                $"LevelMin: {Strength} (Strength)\n" +
                $"LevelMax: {Dexterity} (Dexterity)\n" +
                $"SpawnMin: {Intelligence} (Intelligence)\n" +
                $"SpawnMax: {Quickness} (Quickness)\n" +
                $"MinGroupSize: {Constitution} (Constitution)\n" +
                $"MaxGroupSize: {Charisma} (Charisma)\n" +
                $"UseBattlegroundGroupLogic: {UseBattlegroundGroupLogic} (Piety = 1 to enable)\n" +
                $"BattlegroundGroupChance: {BattlegroundGroupChance}% (Empathy stat)\n" +
                $"IgnorePlayerCheck: {HasIgnorePlayerCheck}\n" +
                "\nRunning: " + _timer.IsAlive + "\n" +
                "Spawns: " + _mimics.Count + "/" + SpawnMax + "\n\n" +
                "[Toggle]\n[List]\n\n[Delete]",
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
                    if (_timer.IsAlive)
                    {
                        _timer.Stop();
                        message = "Spawner is no longer running.";
                    }
                    else
                    {
                        _timer.Start();
                        message = "Spawner is now running.";
                    }
                    break;

                case "Delete":
                    MimicSpawning.MimicSpawnersPersistent.Remove(this);
                    if (_timer.IsAlive) _timer.Stop();
                    _timer = null;
                    Delete();
                    message = "Spawner has been deleted.";
                    break;

                case "List":
                    lock (_mimics)
                    {
                        foreach (MimicNPC mimic in _mimics)
                            message += $"{mimic.Name} {mimic.CharacterClass.Name} {mimic.Level} Region: {mimic.CurrentRegionID}\n";
                    }
                    break;
            }

            if (message.Length > 0)
                player.Out.SendMessage(message, eChatType.CT_System, eChatLoc.CL_PopupWindow);

            return true;
        }

        public void Stop() { if (_timer?.IsAlive == true) _timer.Stop(); }
        public void Start() { if (_timer?.IsAlive == false) _timer.Start(); }
    }
}
