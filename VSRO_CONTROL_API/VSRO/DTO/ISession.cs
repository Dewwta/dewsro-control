using System.Collections.Concurrent;
using VSRO_CONTROL_API.VSRO.Bot.DTO;
using VSRO_CONTROL_API.VSRO.Bots;
using VSRO_CONTROL_API.VSRO.Bots.DTO;
using VSRO_CONTROL_API.VSRO.DTO.VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.Tools;

namespace VSRO_CONTROL_API.VSRO.DTO
{
    /// <summary>
    /// Represents the active runtime state for a connected game session.
    /// 
    /// This interface contains:
    /// - Character identity and progression data
    /// - World positioning and movement state
    /// - Spawn tracking and entity caches
    /// - Combat/session statistics
    /// - Account and server metadata
    /// - Runtime cancellation and task state
    /// </summary>
    public interface ISession
    {
        #region Character Information

        /// <summary>
        /// Gets or sets the current character name.
        /// </summary>
        string? CharacterName { get; set; }

        /// <summary>
        /// Gets or sets the unique runtime UID assigned to the character entity.
        /// </summary>
        uint CharacterUID { get; set; }

        /// <summary>
        /// Gets or sets the database character identifier.
        /// </summary>
        uint CharacterID { get; set; }

        /// <summary>
        /// Gets or sets the currently active pet UID.
        /// </summary>
        uint ActivePetUID { get; set; }

        /// <summary>
        /// Gets or sets whether the player has GM privileges.
        /// </summary>
        bool IsGM { get; set; }

        /// <summary>
        /// Gets or sets the player's party information.
        /// </summary>
        Party? PlayerParty { get; set; }

        /// <summary>
        /// Gets or sets the player's stat information.
        /// </summary>
        PlayerStats? PlayerStats { get; set; }

        /// <summary>
        /// Gets the player's inventory tracker instance.
        /// </summary>
        InventoryTracker Inventory { get; }

        /// <summary>
        /// Gets or sets the player's currently active buffs.
        /// </summary>
        List<SR_Skill> Buffs { get; set; }

        /// <summary>
        /// Gets or sets the player's currently active skills.
        /// </summary>
        List<SR_Skill> LearnedSkills { get; set; }
        void PushSkillListsToDll(string accountName);
        public void SaveBotConfig();
        public AutoWalker? _walker { get; set; }
        public AttackLoop? _attackLoop { get; set; }
        public BotMode _botState { get; set; }
        public PlayerBotData? _savedBotConfig { get; set; }
        public List<SR_Skill> _savedAttackSkills { get; set; }
        public List<SR_Skill> _savedBuffSkills { get; set; }


        public CancellationTokenSource? _walkerCts { get; set; }
        public Task? _botTask { get; set; }
        public int _lastBotX { get; set; }
        public int _lastBotY { get; set; }
        public int _lastBotZ { get; set; }
        public int _lastBotR { get; set; }
        public int _lastBotRegionID { get; set; }

        // NEW
        public CancellationTokenSource? CombatCts { get; set; }
        public Task? CombatTask { get; set; }
        public BotBrain? _brain { get; set; }
        public CancellationTokenSource? ActionCts { get; set; }
        public Task? ActionTask { get; set; }
        public BotPosition? TrainingDestination { get; set; }
        public BotSettings _botSettings { get; set; }
        #endregion

        #region Session Statistics

        /// <summary>
        /// Gets or sets the total monster kills during this session.
        /// </summary>
        uint SessionKills { get; set; }

        /// <summary>
        /// Gets or sets the total unique kills during this session.
        /// </summary>
        uint SessionUniqueKills { get; set; }

        /// <summary>
        /// Gets or sets the cumulative experience gained during this session.
        /// </summary>
        ulong CumulativeExp { get; set; }

        /// <summary>
        /// Gets or sets the login timestamp for the session.
        /// </summary>
        DateTime LoginTime { get; set; }

        #endregion

        #region Targeting & Combat

        /// <summary>
        /// Gets or sets the last attacked or interacted target UID.
        /// </summary>
        uint LastTargetUID { get; set; }

        /// <summary>
        /// Gets or sets the currently selected entity UID.
        /// </summary>
        uint SelectedEntityUID { get; set; }

        #endregion

        #region Movement & Positioning

        /// <summary>
        /// Gets or sets whether the character is currently moving.
        /// </summary>
        bool IsMoving { get; set; }

        /// <summary>
        /// Gets or sets the current character run speed.
        /// </summary>
        float RunSpeed { get; set; }

        /// <summary>
        /// Gets or sets the current sector X coordinate.
        /// </summary>
        byte SectorX { get; set; }

        /// <summary>
        /// Gets or sets the current sector Y coordinate.
        /// </summary>
        byte SectorY { get; set; }

        /// <summary>
        /// Gets or sets the raw local X coordinate within the sector.
        /// </summary>
        short RawX { get; set; }

        /// <summary>
        /// Gets or sets the raw local Y coordinate within the sector.
        /// </summary>
        short RawY { get; set; }

        /// <summary>
        /// Gets or sets the vertical world coordinate.
        /// </summary>
        short Z { get; set; }

        /// <summary>
        /// Gets or sets the current world region identifier.
        /// </summary>
        int RegionId { get; set; }

        /// <summary>
        /// Gets or sets the resolved region name Code.
        /// </summary>
        string? RegionName { get; set; }

        /// <summary>
        /// Gets or sets the resolved region name Code.
        /// </summary>
        string? RegionReadableName { get; set; }


        /// <summary>
        /// Gets or sets the timestamp of the most recent movement update.
        /// </summary>
        DateTime LastMovementUpdate { get; set; }

        /// <summary>
        /// Gets or sets whether an inventory sort operation is currently active.
        /// </summary>
        bool IsSorting { get; set; }

        public BotPosition? LastConfirmedBotPosition { get; set; }
        public float LastMoveDirectionX { get; set; }
        public float LastMoveDirectionY { get; set; }
        public DateTime LastMovePacketSentAt { get; set; }
        public BotPosition GetEstimatedPosition();

        #endregion

        #region Spawn & World Tracking

        /// <summary>
        /// Gets or sets the current group spawn operation type.
        /// 
        /// Values:
        /// 1 = Spawn
        /// 2 = Despawn
        /// </summary>
        byte CurrentGroupSpawnType { get; set; }

        /// <summary>
        /// Gets or sets the tracked spawned world objects.
        /// 
        /// Key   = Entity UID
        /// Value = (Reference Object ID, Region ID)
        /// </summary>
        ConcurrentDictionary<uint, (uint RefObjID, short RegionID, uint ObjId)> SpawnedObjects { get; set; }

        /// <summary>
        /// Gets or sets tracked spawned world positions.
        /// 
        /// Key   = Entity UID
        /// Value = (World X, World Y)
        /// </summary>
        ConcurrentDictionary<uint, (int WorldX, int WorldY)> SpawnedPositions { get; set; }

        /// <summary>
        /// Gets or sets tracked mob UID's for attack loop.
        /// 
        /// Key   = UID
        /// Value = Enabled 1 or 0
        /// </summary>
        public ConcurrentDictionary<uint, byte> MobUIDs { get; set; }

        /// <summary>
        /// Dropped items in radius, parsed by group spawn body
        /// 
        /// </summary>
        public ConcurrentDictionary<uint, (uint RefObjID, string CodeName, int WorldX, int WorldY)> DroppedItems { get; set; }
        #endregion

        #region Runtime State

        /// <summary>
        /// Gets or sets the primary session cancellation token source.
        /// </summary>
        CancellationTokenSource? SessionTokenSource { get; set; }

        /// <summary>
        /// Gets or sets the active inventory sort cancellation token source.
        /// </summary>
        CancellationTokenSource? ActiveSortCts { get; set; }

        #endregion

        #region Account & Server Information

        /// <summary>
        /// Gets or sets the account JID.
        /// </summary>
        int JID { get; set; }

        /// <summary>
        /// Gets or sets the account username.
        /// </summary>
        string? AccountName { get; set; }

        /// <summary>
        /// Gets or sets the connected server name.
        /// </summary>
        string? ServerName { get; set; }

        /// <summary>
        /// Gets or sets the active agent server session identifier.
        /// </summary>
        uint AgentSessionId { get; set; }

        #endregion

        #region Session Methods

        /// <summary>
        /// Increments the session unique kill counter.
        /// </summary>
        void IncrementSessionUniqueKills();

        /// <summary>
        /// Increments the session kill counter.
        /// </summary>
        void IncrementSessionKills();

        /// <summary>
        /// Gets the current calculated movement speed.
        /// </summary>
        /// <returns>The current movement speed value.</returns>
        float GetCurrentMoveSpeed();

        #endregion
    }
}

