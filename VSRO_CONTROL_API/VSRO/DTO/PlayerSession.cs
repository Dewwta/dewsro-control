using System.Collections.Concurrent;
using VSRO_CONTROL_API.VSRO.Bots;
using VSRO_CONTROL_API.VSRO.Bots.DTO;
using VSRO_CONTROL_API.VSRO.DTO.VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.Tools;

namespace VSRO_CONTROL_API.VSRO.DTO
{
    public class PlayerSession : ISession
    {
        public string? CharacterName { get; set; }
        public string? AccountName { get; set; }
        public int JID { get; set; }
        public string? IP { get; set; }
        public DateTime LoginTime { get; set; }
        public DateTime LastActivity { get; set; }
        public TimeSpan AccumulatedPlayTime { get; set; }
        public bool IsAfk { get; set; }
        public int RewardedHours { get; set; }
        public Party? PlayerParty { get; set; } = null;
        public PlayerStats? PlayerStats { get; set; } = null;
        public uint ActivePetUID { get; set; }
        public uint CharacterUID { get; set; }  // spawn UID, changes per session
        public uint CharacterID { get; set; }   // persistent DB ID
        public uint SessionKills { get; set; } = 0; // kills in session
        public uint SessionUniqueKills { get; set; } = 0;
        public ulong CumulativeExp { get; set; } = 0;
        public byte? PendingLevelReward { get; set; } = null;
        public List<byte> UnclaimedRewards { get; set; } = new();
        public SemaphoreSlim AchievementLock { get; } = new SemaphoreSlim(1, 1);
        public TimeSpan TotalPlayTime { get; set; } = TimeSpan.Zero;
        public byte SectorX { get; set; }
        public byte SectorY { get; set; }
        public short Z { get; set; }
        public short RawX { get; set; }
        public short RawY { get; set; }
        public int WorldX { get; set; }
        public int WorldY { get; set; }
        public int RegionId { get; set; }
        public string? RegionReadableName { get; set; }
        public string? RegionName { get; set; }
        public InventoryTracker Inventory { get; } = new InventoryTracker();
        public uint LastTargetUID { get; set; }
        public CancellationTokenSource? SessionTokenSource { get; set; }
        public CancellationTokenSource? ActiveSortCts { get; set; }
        private readonly object _uniqueKillLock = new object();
        private readonly object _killLock = new object();
        public bool IsSorting { get; set; } = false;
        public bool IsGM { get; set; } = false;
        // Data
        public byte CurrentGroupSpawnType { get; set; } = 0;  // 1=spawn, 2=despawn
        public AutoWalker? _walker { get; set; } = null;
        public AttackLoop? _attackLoop { get; set; } = null;
        public CancellationTokenSource? _walkerCts { get; set; }
        public Task? _botTask { get; set; }
        public BotMode _botState { get; set; }
        public BotSettings _botSettings { get; set; } = new BotSettings();

        public ConcurrentDictionary<uint, (uint RefObjID, short RegionID, uint ObjId)> SpawnedObjects { get; set; } = new();
        public ConcurrentDictionary<uint, (int WorldX, int WorldY)> SpawnedPositions { get; set; } = new();
        public ConcurrentDictionary<uint, (uint RefObjID, string CodeName, int WorldX, int WorldY)> DroppedItems { get; set; } = new();

        public List<SR_Skill> Buffs { get; set; } = new(); // Active Buffs
        public List<SR_Skill> LearnedSkills { get; set; } = new(); // Populated with chardata login packet
        public PlayerBotData? _savedBotConfig { get; set; }
        public List<SR_Skill> _savedAttackSkills { get; set; } = new();
        public List<SR_Skill> _savedBuffSkills { get; set; } = new();

        public void PushSkillListsToDll(string accountName)
        {
            DllBridge.Instance.SendToDll(accountName, "skill_list_sync", new
            {
                attack = _attackLoop?.SkillsToUse.Select(s => new { id = s.ID, name = s.ReadableName }),
                buffs = _attackLoop?.BuffsToUse.Select(s => new { id = s.ID, name = s.ReadableName })
            });
        }

        public void SaveBotConfig()
        {
            if (string.IsNullOrEmpty(CharacterName)) return;

            PlayerBotDataStore.Save(CharacterName, new PlayerBotData
            {
                TrainX = _lastBotX,
                TrainY = _lastBotY,
                TrainZ = _lastBotZ,
                TrainR = _lastBotR,
                RegionId = _lastBotRegionID,
                AttackSkillIds = _attackLoop?.SkillsToUse.Select(s => s.ID).ToList() ?? new(),
                BuffSkillIds = _attackLoop?.BuffsToUse.Select(s => s.ID).ToList() ?? new(),
                Settings = _botSettings // Save directly into the database model
            });
        }
        public int _lastBotX { get; set; }
        public int _lastBotY { get; set; }
        public int _lastBotZ { get; set; }
        public int _lastBotR { get; set; }
        public int _lastBotRegionID { get; set; }

        public CancellationTokenSource? CombatCts { get; set; }
        public Task? CombatTask { get; set; }
        public BotBrain? _brain { get; set; }
        public CancellationTokenSource? ActionCts { get; set; }
        public Task? ActionTask { get; set; }
        public BotPosition? TrainingDestination { get; set; }

        public bool IsMoving { get; set; }
        public float RunSpeed { get; set; }
        
        public uint AgentSessionId { get; set; }
        public string? ServerName { get; set; }
        public uint SelectedEntityUID { get; set; }

        public DateTime LastMovementUpdate { get; set; } = DateTime.MinValue;
        public BotPosition? LastConfirmedBotPosition { get; set; }
        public float LastMoveDirectionX { get; set; }
        public float LastMoveDirectionY { get; set; }
        public DateTime LastMovePacketSentAt { get; set; } = DateTime.MinValue;
        public ConcurrentDictionary<uint, byte> MobUIDs { get; set; } = new();

        public void IncrementSessionUniqueKills()
        {
            lock (_uniqueKillLock)
            {
                SessionUniqueKills++;
            }
        }

        public void IncrementSessionKills()
        {
            lock (_killLock)
            {
                SessionKills++;
            }
        }

        public float GetCurrentMoveSpeed()
        {
            float _base = this.RunSpeed; // 50.0f from chardata
            float multiplier = 1.0f;
            foreach (var buff in this.Buffs)
                if (buff.MoveSpeedPercent != 0)
                    multiplier += buff.MoveSpeedPercent / 100f;
            return _base * multiplier;
        }

        public BotPosition GetEstimatedPosition()
        {
            if (LastConfirmedBotPosition == null)
                return BotPosition.FromSession(this);

            double elapsed = (DateTime.UtcNow - LastMovementUpdate).TotalSeconds;
            // Cap at reasonable maximum so we don't project past the target
            elapsed = Math.Min(elapsed, 3.0);

            float unitsPerSec = GetCurrentMoveSpeed() / 7.86f;
            float traveled = (float)(elapsed * unitsPerSec);

            float wx = LastConfirmedBotPosition.Value.X + LastMoveDirectionX * traveled;
            float wy = LastConfirmedBotPosition.Value.Y + LastMoveDirectionY * traveled;
            return BotPosition.FromDisplayWorld(wx, wy, LastConfirmedBotPosition.Value.ZOffset);
        }


    }
}
