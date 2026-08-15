using DOL.AI;
using DOL.AI.Brain;
using DOL.GS.PacketHandler;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS.Scripts
{
    public class MimicSpawnerPersistent : GameNPC, IMimicSpawnerPersistent
    {
        public bool IsRunning => _timer != null && _timer.IsAlive;
        public List<MimicNPC> Mimics => _mimics;
        public int SpawnCount => _spawnCount;
        public bool SpawnAndStop { get; set; }

        public override eGameObjectType GameObjectType => eGameObjectType.NPC;

        private int _spawnCount;
        private ECSGameTimer _timer;
        private int _dormantInterval;
        private int _timerIntervalMin = 10000;
        private int _timerIntervalMax = 30000;

        private const int _batchIntervalMin = 300;
        private const int _batchIntervalMax = 700;
        private int _pendingBatchCount;
        private bool _batchIsGroup;
        private Group _pendingBatchGroup;
        private readonly List<MimicNPC> _pendingBatchMembers = new List<MimicNPC>();

        private List<MimicNPC> _mimics;

        public int LevelMin => base.Strength;
        public int LevelMax => base.Dexterity;
        public int SpawnMin => base.Intelligence;
        public int SpawnMax => base.Quickness;
        public int MinGroupSize => base.Constitution;
        public int MaxGroupSize => base.Charisma;

        bool deleteAllOnNextTick = false;

        // ✅ NEW: PreventCombat support
        public bool PreventCombat =>
            !string.IsNullOrEmpty(PackageID) && PackageID.Contains("PREVENT_COMBAT");

        private int TimerCallback(ECSGameTimer timer)
        {
            if (_mimics.Count >= SpawnMax)
            {
                WakeUpPendingBatch();
                return Util.Random(_timerIntervalMin, _timerIntervalMax);
            }

            var playersInRegion = ClientService.GetPlayersOfRegion(CurrentRegion)
                .Where(a => a.Client != null && a.Client.Account?.PrivLevel == (uint)ePrivLevel.Player)
                .ToList();

            if (!HasIgnorePlayerCheck && playersInRegion.Count == 0)
            {
                ResetBatch();

                if (deleteAllOnNextTick)
                {
                    foreach (var mimic in _mimics.ToList())
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
                }

                return 1000 * 60 * 5;
            }

            // Spawn one member per tick so the heavy mimic creation is spread
            // out instead of hitting the server with a whole group at once.
            if (_pendingBatchCount > 0)
                return SpawnNextBatchMember();

            // ✅ CLEAN group size logic
            int grpCount = Math.Max(1, Util.Random(MinGroupSize, MaxGroupSize));

            _pendingBatchCount = grpCount;
            _batchIsGroup = grpCount > 1;

            return SpawnNextBatchMember();
        }

        private int SpawnNextBatchMember()
        {
            _pendingBatchCount--;

            MimicNPC mimic = SpawnSingleMimic();

            if (mimic != null)
            {
                if (_batchIsGroup)
                {
                    if (_pendingBatchGroup == null)
                    {
                        _pendingBatchGroup = new Group(mimic);
                        _pendingBatchGroup.AddMember(mimic);
                    }
                    else
                    {
                        _pendingBatchGroup.AddMember(mimic);
                    }
                }

                _pendingBatchMembers.Add(mimic);

                // Hold the member idle until the whole group has spawned, then
                // everybody wakes up together so the group never runs off
                // before the buff/healer class has joined.
                if (_pendingBatchCount > 0 && mimic.Brain is MimicBrain waitingBrain)
                {
                    waitingBrain.FSM.SetCurrentState(eFSMStateType.IDLE);
                }
            }

            if (_pendingBatchCount > 0)
                return Util.Random(_batchIntervalMin, _batchIntervalMax);

            WakeUpPendingBatch();

            return Util.Random(_timerIntervalMin, _timerIntervalMax);
        }

        private void WakeUpPendingBatch()
        {
            foreach (MimicNPC mimic in _pendingBatchMembers)
            {
                if (mimic?.Brain is MimicBrain brain)
                    brain.FSM.SetCurrentState(eFSMStateType.WAKING_UP);
            }

            ResetBatch();
        }

        private void ResetBatch()
        {
            _pendingBatchCount = 0;
            _pendingBatchGroup = null;
            _pendingBatchMembers.Clear();
        }

        // ✅ NEW: Centralized spawn logic
        private MimicNPC SpawnSingleMimic()
        {
            int randomX = Util.Random(-100, 100);
            int randomY = Util.Random(-100, 100);

            Point3D spawnPoint = new Point3D(
                this.X + randomX,
                this.Y + randomY,
                this.Z
            );

            eMimicClass mimicClass = GetRandomMimicClassForSpawn();

            // Guard against inverted stats (min > max), which would make
            // Util.Random throw and stall the whole batch.
            int levelMin = Math.Min(LevelMin, LevelMax);
            int levelMax = Math.Max(LevelMin, LevelMax);

            MimicNPC mimicNPC = MimicManager.GetMimic(
                mimicClass,
                (byte)Util.Random(levelMin, levelMax),
                preventCombat: PreventCombat
            );

            if (MimicManager.AddMimicToWorld(mimicNPC, spawnPoint, this.CurrentRegionID))
            {
                _mimics.Add(mimicNPC);
                mimicNPC.MimicSpawnerPersistent = this;

                if (SpawnAndStop)
                    _spawnCount++;

                return mimicNPC;
            }

            return null;
        }

        protected virtual eMimicClass GetRandomMimicClassForSpawn()
        {
            return MimicManager.GetRandomMimicClass(this.Realm);
        }

        public void Remove(MimicNPC mimic)
        {
            if (mimic == null)
                return;

            lock (_mimics)
            {
                _mimics.Remove(mimic);
            }
        }

        public override short Strength { get { OnChangeInfo(); return base.Strength; } set => base.Strength = value; }
        public override short Dexterity { get { OnChangeInfo(); return base.Dexterity; } set => base.Dexterity = value; }
        public override short Intelligence { get { OnChangeInfo(); return base.Intelligence; } set => base.Intelligence = value; }
        public override short Quickness { get { OnChangeInfo(); return base.Quickness; } set => base.Quickness = value; }
        public override short Constitution { get { OnChangeInfo(); return base.Constitution; } set => base.Constitution = value; }
        public override short Charisma { get { OnChangeInfo(); return base.Charisma; } set => base.Charisma = value; }

        public void OnChangeInfo()
        {
            if (_mimics == null)
                return;

            foreach (var mimic in _mimics.ToList())
            {
                mimic.RemoveFromWorld();
            }

            _mimics.Clear();
            ResetBatch();
        }

        public override bool AddToWorld()
        {
            /*
            Name = "Mimic Spawner";
            Model = 408;
            Level = 75;
            Size = 50;
            X = _position.X;
            Y = _position.Y;
            Z = _position.Z;
            CurrentRegionID = _region;
            Heading = 0;*/

            /*
             * 
        public int LevelMin => base.Strength;
        public int LevelMax => base.Dexterity;
        public int SpawnMin => base.Intelligence;
        public int SpawnMax => base.Quickness;
        public int MinGroupSize => base.Constitution;
        public int MaxGroupSize => base.Charisma;
            */

            //Has just been created via /'mob create, lets set some sane defaults on our spawner
            if (this.LoadedFromScript)
            {
                this.Strength = 10;
                this.Dexterity = 15;
                this.Intelligence = 1;
                this.Quickness = 5;
                this.Constitution = 1;
                this.Charisma = 1;
            }

            _mimics = new List<MimicNPC>();
            _dormantInterval = 5000;
            SpawnAndStop = false;
            ResetBatch();

            _timer?.Stop();
            _timer = null;

            MimicSpawning.MimicSpawnersPersistent.Remove(this);

            _timer = new ECSGameTimer(this, new ECSGameTimer.ECSTimerCallback(TimerCallback),
                Util.Random(_timerIntervalMin, _timerIntervalMax));

            MimicSpawning.MimicSpawnersPersistent.Add(this);

            Flags |= eFlags.PEACE;

            return base.AddToWorld();
        }

        public override int ChangeHealth(GameObject changeSource, eHealthChangeType healthChangeType, int changeAmount)
        {
            return 0;
        }

        public override bool IsVisibleTo(GameObject checkObject)
        {
            if (checkObject is GamePlayer player && player.Client.Account.PrivLevel == 1)
                return false;

            return base.IsVisibleTo(checkObject);
        }

        public bool HasIgnorePlayerCheck =>
            !string.IsNullOrEmpty(PackageID) && PackageID.Contains("IGNORE_PLAYERCHECK");

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            /*
        
        public int LevelMin => base.Strength;
        public int LevelMax => base.Dexterity;
        public int SpawnMin => base.Intelligence;
        public int SpawnMax => base.Quickness;
        public int MinGroupSize => base.Constitution;
        public int MaxGroupSize => base.Charisma;*/

             
            player.Out.SendMessage(
                "---------------------------------------\n" +
                $"Realm: {this.Realm} (Realm)\n" +
                $"LevelMin: {base.Strength} (Strength)\n" +
                $"LevelMax: {base.Dexterity} (Dexterity)\n" +
                $"SpawnMin: {base.Intelligence} (Intelligence)\n" +
                $"SpawnMax: {base.Quickness} (Quickness)\n" +
                $"MinGroupSize: {base.Constitution} (Constitution)\n" +
                $"MaxGroupSize: {base.Charisma} (Charisma)\n" +
                $"IgnorePlayerCheck: {HasIgnorePlayerCheck} (add IGNORE_PLAYERCHECK to packageid)\n" +
                $"PreventCombat: {PreventCombat} (add PREVENT_COMBAT to packageid)\n" +
                "\n" +
                $"Running: {IsRunning}\n" +
                $"Spawns: {_mimics.Count}/{SpawnMax}\n\n" +
                "[Toggle]\n" +
                "[List]\n\n" +
                "[Delete]",
                eChatType.CT_Say,
                eChatLoc.CL_PopupWindow
            );

            return true;
        }

        public override bool WhisperReceive(GameLiving source, string str)
        {
            if (!base.WhisperReceive(source, str))
                return false;

            if (source is not GamePlayer player)
                return false;

            string message = string.Empty;

            switch (str)
            {
                case "Toggle":
                    {
                        if (IsRunning)
                        {
                            Stop();
                            message = "Spawner is no longer running.";
                        }
                        else
                        {
                            Start();
                            message = "Spawner is now running.";
                        }
                        break;
                    }

                case "Delete":
                    {
                        if (MimicSpawning.MimicSpawnersPersistent.Contains(this))
                        {
                            MimicSpawning.MimicSpawnersPersistent.Remove(this);
                        }

                        if (_timer != null && _timer.IsAlive)
                            _timer.Stop();

                        // ✅ Clean up mimics before delete (important!)
                        foreach (var mimic in _mimics.ToList())
                        {
                            mimic.RemoveFromWorld();
                            mimic.Delete();
                        }
                        _mimics.Clear();
                        ResetBatch();

                        _timer = null;

                        message = "Spawner has been deleted.";
                        Delete();

                        break;
                    }

                case "List":
                    {
                        foreach (MimicNPC mimic in _mimics)
                        {
                            message += $"{mimic.Name} {mimic.CharacterClass.Name} {mimic.Level} Region: {mimic.CurrentRegionID}\n";
                        }
                        break;
                    }

                default:
                    break;
            }

            if (message.Length > 0)
            {
                player.Out.SendMessage(message, eChatType.CT_System, eChatLoc.CL_PopupWindow);
            }

            return true;
        }

        public void Stop()
        {
            // A partial batch is still in the IDLE hold-state, waiting for the
            // rest of its group. Deleting it keeps the world free of half-formed
            // groups that would otherwise just stand around after a stop.
            foreach (MimicNPC mimic in _pendingBatchMembers)
            {
                if (mimic == null)
                    continue;

                mimic.RemoveFromWorld();
                mimic.Delete();
                Remove(mimic);
            }

            ResetBatch();

            if (_timer != null && _timer.IsAlive)
                _timer.Stop();
        }

        public void Start()
        {
            if (_timer != null && !_timer.IsAlive)
                _timer.Start();
        }
    }
}
