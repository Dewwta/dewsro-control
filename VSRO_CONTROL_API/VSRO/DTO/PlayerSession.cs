using System.Collections.Concurrent;
using VSRO_CONTROL_API.VSRO.Bots;
using VSRO_CONTROL_API.VSRO.Bots.DTO;
using VSRO_CONTROL_API.VSRO.DTO.VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.Enums;
using VSRO_CONTROL_API.VSRO.Tools;

namespace VSRO_CONTROL_API.VSRO.DTO
{
    public class PlayerSession : ISession
    {
        #region Character Information

        public string? CharacterName { get; set; }
        public uint CharacterUID { get; set; }
        public uint CharacterID { get; set; }
        public uint ActivePetUID { get; set; }
        public bool IsGM { get; set; } = false;
        public Race CharacterRace { get; set; } = Race.Unknown;
        public Party? PlayerParty { get; set; } = null;
        public PlayerStats? PlayerStats { get; set; } = null;
        public InventoryTracker Inventory { get; } = new InventoryTracker();
        public List<SR_Skill> Buffs { get; set; } = new();
        public List<SR_Skill> LearnedSkills { get; set; } = new();
        public ConcurrentDictionary<uint, uint> ActiveBuffTimedJobToSkillId { get; } = new();

        #endregion

        #region Bot State

        public BotMode _botState { get; set; }
        public BotBrain? _brain { get; set; }
        public BotSettings _botSettings { get; set; } = new BotSettings();
        public AutoWalker? _walker { get; set; } = null;
        public AttackLoop? _attackLoop { get; set; } = null;
        public PlayerBotData? _savedBotConfig { get; set; }
        public List<SR_Skill> _savedAttackSkills { get; set; } = new();
        public List<SR_Skill> _savedBuffSkills { get; set; } = new();
        public BotPosition? TrainingDestination { get; set; }
        public CancellationTokenSource? _walkerCts { get; set; }
        public Task? _botTask { get; set; }
        public CancellationTokenSource? CombatCts { get; set; }
        public Task? CombatTask { get; set; }
        public CancellationTokenSource? ActionCts { get; set; }
        public Task? ActionTask { get; set; }
        public int _lastBotX { get; set; }
        public int _lastBotY { get; set; }
        public int _lastBotZ { get; set; }
        public int _lastBotR { get; set; }
        public int _lastBotRegionID { get; set; }

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
                Settings = _botSettings
            });
        }

        #endregion

        #region Session Statistics

        public uint SessionKills { get; set; } = 0;
        public uint SessionUniqueKills { get; set; } = 0;
        public ulong CumulativeExp { get; set; } = 0;
        public DateTime LoginTime { get; set; }

        #endregion

        #region Targeting & Combat

        public uint LastTargetUID { get; set; }
        public uint SelectedEntityUID { get; set; }
        public TaskCompletionSource<bool>? TargetGate { get; set; }

        #endregion

        #region Movement & Positioning

        public bool IsMoving { get; set; }
        public float RunSpeed { get; set; }
        public byte SectorX { get; set; }
        public byte SectorY { get; set; }
        public short RawX { get; set; }
        public short RawY { get; set; }
        public short Z { get; set; }
        public int RegionId { get; set; }
        public string? RegionName { get; set; }
        public string? RegionReadableName { get; set; }
        public DateTime LastMovementUpdate { get; set; } = DateTime.MinValue;
        public bool IsSorting { get; set; } = false;
        public BotPosition? LastConfirmedBotPosition { get; set; }
        public float LastMoveDirectionX { get; set; }
        public float LastMoveDirectionY { get; set; }
        public DateTime LastMovePacketSentAt { get; set; } = DateTime.MinValue;

        public BotPosition GetEstimatedPosition()
        {
            if (LastConfirmedBotPosition == null)
                return BotPosition.FromSession(this);

            double elapsed = Math.Min((DateTime.UtcNow - LastMovementUpdate).TotalSeconds, 3.0);
            float unitsPerSec = GetCurrentMoveSpeed() / 7.86f;
            float traveled = (float)(elapsed * unitsPerSec);

            float wx = LastConfirmedBotPosition.Value.X + LastMoveDirectionX * traveled;
            float wy = LastConfirmedBotPosition.Value.Y + LastMoveDirectionY * traveled;
            return BotPosition.FromDisplayWorld(wx, wy, LastConfirmedBotPosition.Value.ZOffset);
        }

        #endregion

        #region Shop Interaction

        public TaskCompletionSource<bool>? ShopOpenGate { get; set; }
        public TaskCompletionSource<bool>? ShopCloseGate { get; set; }
        public bool ShopVectorInitialized { get; set; } = false;
        public bool BotShopOpen { get; set; } = false;

        #endregion

        #region Spawn & World Tracking

        public byte CurrentGroupSpawnType { get; set; } = 0;
        public ConcurrentDictionary<uint, (uint RefObjID, short RegionID, uint ObjId)> SpawnedObjects { get; set; } = new();
        public ConcurrentDictionary<uint, (int WorldX, int WorldY)> SpawnedPositions { get; set; } = new();
        public ConcurrentDictionary<uint, byte> MobUIDs { get; set; } = new();
        public ConcurrentDictionary<uint, (uint RefObjID, string CodeName, int WorldX, int WorldY)> DroppedItems { get; set; } = new();

        #endregion

        #region Runtime State

        public CancellationTokenSource? SessionTokenSource { get; set; }
        public CancellationTokenSource? ActiveSortCts { get; set; }

        #endregion

        #region Account & Server Information

        public int JID { get; set; }
        public string? AccountName { get; set; }
        public string? ServerName { get; set; }
        public uint AgentSessionId { get; set; }

        #endregion

        #region Extended Player State

        public string? IP { get; set; }
        public DateTime LastActivity { get; set; }
        public TimeSpan AccumulatedPlayTime { get; set; }
        public TimeSpan TotalPlayTime { get; set; } = TimeSpan.Zero;
        public bool IsAfk { get; set; }
        public int RewardedHours { get; set; }
        public int WorldX { get; set; }
        public int WorldY { get; set; }
        public int PlayerDeaths = 0;
        public byte? PendingLevelReward { get; set; } = null;
        public List<byte> UnclaimedRewards { get; set; } = new();
        public SemaphoreSlim AchievementLock { get; } = new SemaphoreSlim(1, 1);

        #endregion

        #region Session Methods

        private readonly object _uniqueKillLock = new object();
        private readonly object _killLock = new object();

        public void IncrementSessionUniqueKills()
        {
            lock (_uniqueKillLock) { SessionUniqueKills++; }
        }

        public void IncrementSessionKills()
        {
            lock (_killLock) { SessionKills++; }
        }

        public float GetCurrentMoveSpeed()
        {
            float multiplier = 1.0f;
            foreach (var buff in Buffs)
                if (buff.MoveSpeedPercent != 0)
                    multiplier += buff.MoveSpeedPercent / 100f;
            return RunSpeed * multiplier;
        }

        #endregion
    }
}
