using System.Collections.Concurrent;
using VSRO_CONTROL_API.VSRO.Bots;
using VSRO_CONTROL_API.VSRO.Bots.DTO;
using VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.DTO.VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.Tools;

namespace VSRO_CONTROL_API.VSRO.Bot.DTO
{
    public class ClientlessCharState : VSRO_CONTROL_API.VSRO.DTO.ISession
    {
        public string? ServerName { get; set; }
        public DateTime LoginTime { get; set; }
        public string? CharacterName { get; set; }
        public uint AgentSessionId { get; set; }
        public uint CharacterUID { get; set; }
        public uint CharacterID { get; set; }
        public uint SelectedEntityUID { get; set; }
        public float RunSpeed { get; set; }
        public byte SectorX { get; set; }
        public byte SectorY { get; set; }
        public short Z { get; set; }
        public short RawX { get; set; }
        public short RawY { get; set; }
        public int WorldX { get; set; }
        public int WorldY { get; set; }
        public int RegionId { get; set; }
        public string? RegionName { get; set; }
        public string? RegionReadableName { get; set; }
        public bool IsMoving { get; set; }
        public InventoryTracker Inventory { get; } = new InventoryTracker();
        public PlayerStats? PlayerStats { get; set; } = new();
        public uint LastTargetUID { get; set; }
        public bool IsSorting { get; set; } = false;
        public CancellationTokenSource? ActiveSortCts { get; set; }
        public BotPosition? LastConfirmedBotPosition { get; set; }
        public float LastMoveDirectionX { get; set; }
        public float LastMoveDirectionY { get; set; }
        public DateTime LastMovePacketSentAt { get; set; } = DateTime.MinValue;
        // Account 
        public int JID { get; set; }
        public string? AccountName { get; set; }
        public AutoWalker? _walker { get; set; }
        public AttackLoop? _attackLoop { get; set; }
        public CancellationTokenSource? _walkerCts { get; set; }
        public BotMode _botState { get; set; }
        public Task? _botTask { get; set; }
        public PlayerBotData? _savedBotConfig { get; set; }
        public List<SR_Skill> _savedAttackSkills { get; set; } = new();
        public List<SR_Skill> _savedBuffSkills { get; set; } = new();

        // Just in case.
        public CancellationTokenSource? SessionTokenSource { get; set; }

        // Data
        public byte CurrentGroupSpawnType { get; set; } = 0;
        public ConcurrentDictionary<uint, (uint RefObjID, short RegionID, uint ObjId)> SpawnedObjects { get; set; } = new();
        public ConcurrentDictionary<uint, (int WorldX, int WorldY)> SpawnedPositions { get; set; } = new();
        public ConcurrentDictionary<uint, (uint RefObjID, string CodeName, int WorldX, int WorldY)> DroppedItems { get; set; } = new();
        public ConcurrentDictionary<uint, uint> SpawnCache { get; } = new();
        public List<SR_Skill> Buffs { get; set; } = new();
        public List<SR_Skill> LearnedSkills { get; set; } = new();
        public int _lastBotX { get; set; }
        public int _lastBotY { get; set; }
        public int _lastBotZ { get; set; }
        public int _lastBotR { get; set; }
        public int _lastBotRegionID { get; set; }

        public BotBrain? _brain { get; set; }
        public CancellationTokenSource? CombatCts { get; set; }
        public Task? CombatTask { get; set; }
        public CancellationTokenSource? ActionCts { get; set; }
        public Task? ActionTask { get; set; }
        public BotPosition? TrainingDestination { get; set; }
        public BotSettings _botSettings { get; set; } = new BotSettings();

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
                AttackSkillIds = _attackLoop?.SkillsToUse.Select(s => s.ID).ToList() ?? new(),
                BuffSkillIds = _attackLoop?.BuffsToUse.Select(s => s.ID).ToList() ?? new(),
                Settings = _botSettings // Save directly into the database model
            });
        }

        public uint SessionKills { get; set; } = 0;
        public uint SessionUniqueKills { get; set; } = 0;
        public uint ActivePetUID { get; set; }
        public Party? PlayerParty { get; set; } = null;
        public ulong CumulativeExp { get; set; } = 0;
        public ConcurrentDictionary<uint, byte> MobUIDs { get; set; } = new();

        public DateTime LastMovementUpdate { get; set; } = DateTime.MinValue;

        private readonly object _uniqueKillLock = new object();
        private readonly object _killLock = new object();
        public bool IsGM { get; set; }

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

            float unitsPerSec = GetCurrentMoveSpeed() / 8f;
            float traveled = (float)(elapsed * unitsPerSec);

            float wx = LastConfirmedBotPosition.Value.X + LastMoveDirectionX * traveled;
            float wy = LastConfirmedBotPosition.Value.Y + LastMoveDirectionY * traveled;
            return BotPosition.FromDisplayWorld(wx, wy, LastConfirmedBotPosition.Value.ZOffset);
        }
    }
}
