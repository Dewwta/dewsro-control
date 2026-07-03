using System.Collections.Concurrent;
using VSRO_CONTROL_API.VSRO.Bot.DTO;
using VSRO_CONTROL_API.VSRO.Bots;
using VSRO_CONTROL_API.VSRO.Bots.DTO;
using VSRO_CONTROL_API.VSRO.DTO.VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.Enums;
using VSRO_CONTROL_API.VSRO.Tools;

namespace VSRO_CONTROL_API.VSRO.DTO
{
    /// <summary>
    /// Represents the active runtime state for a connected game session.
    ///
    /// Implemented by <see cref="PlayerSession"/> (proxied client) and
    /// <see cref="ClientlessCharState"/> (headless/clientless bot session).
    /// </summary>
    public interface ISession
    {
        #region Character Information

        /// <summary>Gets or sets the character name.</summary>
        string? CharacterName { get; set; }

        /// <summary>Gets or sets the runtime spawn UID assigned to the character. Changes each login.</summary>
        uint CharacterUID { get; set; }

        /// <summary>Gets or sets the persistent database character identifier.</summary>
        uint CharacterID { get; set; }

        /// <summary>Gets or sets the character refObjID. For determining player stats.</summary>
        uint CharacterRefObjID { get; set; }

        /// <summary>Gets or sets the currently active pet UID.</summary>
        uint ActivePetUID { get; set; }

        /// <summary>Gets or sets whether the player has GM privileges.</summary>
        bool IsGM { get; set; }

        /// <summary>Gets or sets the character's race (Chinese or European).</summary>
        Race CharacterRace { get; set; }

        /// <summary>Gets or sets the character's gender (Male or Female).</summary>
        Gender CharacterGender { get; set; }

        /// <summary>Gets or sets the player's party information.</summary>
        Party? PlayerParty { get; set; }

        /// <summary>Gets or sets the player's stat block.</summary>
        PlayerStats? PlayerStats { get; set; }

        /// <summary>Gets the player's inventory tracker instance.</summary>
        InventoryTracker Inventory { get; }

        /// <summary>Gets or sets the player's currently active buffs.</summary>
        List<SR_Skill> Buffs { get; set; }

        /// <summary>Gets or sets all skills the character has learned. Populated from the chardata login packet.</summary>
        List<SR_Skill> LearnedSkills { get; set; }

        #endregion

        #region Bot State

        /// <summary>Gets or sets the current bot operating mode.</summary>
        BotMode _botState { get; set; }

        /// <summary>Gets or sets the active bot brain (high-level decision loop).</summary>
        BotBrain? _brain { get; set; }

        /// <summary>Gets or sets the bot's runtime settings.</summary>
        BotSettings _botSettings { get; set; }

        /// <summary>Gets or sets the active auto-walker instance.</summary>
        AutoWalker? _walker { get; set; }

        /// <summary>Gets or sets the active attack loop instance.</summary>
        AttackLoop? _attackLoop { get; set; }

        /// <summary>Gets or sets the last persisted bot configuration loaded from the database.</summary>
        PlayerBotData? _savedBotConfig { get; set; }

        /// <summary>Gets or sets the attack skill list restored from the saved bot config.</summary>
        List<SR_Skill> _savedAttackSkills { get; set; }

        /// <summary>Gets or sets the buff skill list restored from the saved bot config.</summary>
        List<SR_Skill> _savedBuffSkills { get; set; }

        /// <summary>Gets or sets the destination the bot is navigating toward during training.</summary>
        BotPosition? TrainingDestination { get; set; }

        /// <summary>Gets or sets the cancellation token source for the main bot walker task.</summary>
        CancellationTokenSource? _walkerCts { get; set; }

        /// <summary>Gets or sets the main bot background task.</summary>
        Task? _botTask { get; set; }

        /// <summary>Gets or sets the cancellation token source for the combat loop.</summary>
        CancellationTokenSource? CombatCts { get; set; }

        /// <summary>Gets or sets the combat loop background task.</summary>
        Task? CombatTask { get; set; }

        /// <summary>Gets or sets the cancellation token source for the current discrete bot action (e.g. loot, buff, shop).</summary>
        CancellationTokenSource? ActionCts { get; set; }

        /// <summary>Gets or sets the current discrete bot action task.</summary>
        Task? ActionTask { get; set; }

        /// <summary>Gets or sets the last saved training spot X coordinate.</summary>
        int _lastBotX { get; set; }

        /// <summary>Gets or sets the last saved training spot Y coordinate.</summary>
        int _lastBotY { get; set; }

        /// <summary>Gets or sets the last saved training spot Z coordinate.</summary>
        int _lastBotZ { get; set; }

        /// <summary>Gets or sets the last saved training spot radius.</summary>
        int _lastBotR { get; set; }

        /// <summary>Gets or sets the last saved training spot region ID.</summary>
        int _lastBotRegionID { get; set; }

        /// <summary>Pushes the current attack and buff skill lists to the injected DLL over the named pipe bridge.</summary>
        void PushSkillListsToDll(string accountName);

        /// <summary>Persists the current bot configuration (training position, skills, settings) to the database.</summary>
        void SaveBotConfig();

        #endregion

        #region Session Statistics

        /// <summary>Gets or sets the total monster kills recorded during this session.</summary>
        uint SessionKills { get; set; }

        /// <summary>Gets or sets the total unique (boss/named) kills recorded during this session.</summary>
        uint SessionUniqueKills { get; set; }

        /// <summary>Gets or sets the cumulative experience gained during this session.</summary>
        ulong CumulativeExp { get; set; }

        /// <summary>Gets or sets the UTC timestamp when this session was authenticated and started.</summary>
        DateTime LoginTime { get; set; }

        #endregion

        #region Targeting & Combat

        /// <summary>Gets or sets the UID of the most recently attacked or interacted-with entity.</summary>
        uint LastTargetUID { get; set; }

        /// <summary>Gets or sets the UID of the currently client-selected entity.</summary>
        uint SelectedEntityUID { get; set; }

        /// <summary>
        /// Completion gate signaled when the character successfully acquires a target.
        /// Awaited by bot routines that require confirmed target selection before casting.
        /// </summary>
        TaskCompletionSource<bool>? TargetGate { get; set; }

        #endregion

        #region Movement & Positioning

        /// <summary>Gets or sets whether the character is currently in motion.</summary>
        bool IsMoving { get; set; }

        /// <summary>Gets or sets the character's base run speed as reported by the server.</summary>
        float RunSpeed { get; set; }

        /// <summary>Gets or sets the sector X tile coordinate.</summary>
        byte SectorX { get; set; }

        /// <summary>Gets or sets the sector Y tile coordinate.</summary>
        byte SectorY { get; set; }

        /// <summary>Gets or sets the raw local X offset within the current sector.</summary>
        short RawX { get; set; }

        /// <summary>Gets or sets the raw local Y offset within the current sector.</summary>
        short RawY { get; set; }

        /// <summary>Gets or sets the vertical world Z coordinate.</summary>
        short Z { get; set; }

        /// <summary>Gets or sets the world region identifier.</summary>
        int RegionId { get; set; }

        /// <summary>Gets or sets the internal region code name (e.g. "Donwhang_A").</summary>
        string? RegionName { get; set; }

        /// <summary>Gets or sets the human-readable region display name (e.g. "Donwhang").</summary>
        string? RegionReadableName { get; set; }

        /// <summary>Gets or sets the UTC timestamp of the most recent server-side movement update.</summary>
        DateTime LastMovementUpdate { get; set; }

        /// <summary>Gets or sets whether an inventory sort operation is currently in progress.</summary>
        bool IsSorting { get; set; }

        /// <summary>
        /// Gets or sets the last server-confirmed world position used as the dead-reckoning origin.
        /// Reset each time a movement packet with a confirmed destination arrives.
        /// </summary>
        BotPosition? LastConfirmedBotPosition { get; set; }

        /// <summary>Gets or sets the normalized X component of the last issued movement direction vector.</summary>
        float LastMoveDirectionX { get; set; }

        /// <summary>Gets or sets the normalized Y component of the last issued movement direction vector.</summary>
        float LastMoveDirectionY { get; set; }

        /// <summary>Gets or sets the UTC timestamp when the most recent movement packet was sent.</summary>
        DateTime LastMovePacketSentAt { get; set; }

        /// <summary>
        /// Returns the estimated current world position using dead-reckoning from
        /// <see cref="LastConfirmedBotPosition"/>, elapsed time, and <see cref="GetCurrentMoveSpeed"/>.
        /// Falls back to the raw sector-derived position when no confirmed origin is available.
        /// </summary>
        BotPosition GetEstimatedPosition();

        #endregion

        #region Shop Interaction

        /// <summary>
        /// Completion gate signaled by the packet handler when the server confirms an NPC shop has opened.
        /// Awaited by <c>OpenShopAsync</c> before beginning item purchase operations.
        /// </summary>
        TaskCompletionSource<bool>? ShopOpenGate { get; set; }

        /// <summary>
        /// Completion gate signaled when the server confirms the NPC shop has closed.
        /// Awaited by <c>CloseShopAsync</c> to ensure the session is clean before resuming combat.
        /// </summary>
        TaskCompletionSource<bool>? ShopCloseGate { get; set; }

        /// <summary>
        /// Gets or sets whether the shop item vector has been populated for this session.
        /// Set true once the initial shop item list has been received and indexed.
        /// </summary>
        bool ShopVectorInitialized { get; set; }

        /// <summary>
        /// True while the bot has an NPC shop open server-side (between OpenShopAsync and CloseShopAsync).
        /// Suppresses B034 ShopToInventory responses from reaching the game client, which would crash
        /// because it never received the B046 shop-open packet.
        /// </summary>
        bool BotShopOpen { get; set; }

        #endregion

        #region Spawn & World Tracking

        /// <summary>
        /// Gets or sets the current group spawn operation type.
        /// 1 = Spawn, 2 = Despawn.
        /// </summary>
        byte CurrentGroupSpawnType { get; set; }

        /// <summary>
        /// Gets or sets all tracked spawned world objects.
        /// Key = Entity UID. Value = (RefObjID, RegionID, ObjId).
        /// </summary>
        ConcurrentDictionary<uint, (uint RefObjID, short RegionID, uint ObjId)> SpawnedObjects { get; set; }

        /// <summary>
        /// Gets or sets the world-space positions of all tracked spawned entities.
        /// Key = Entity UID. Value = (WorldX, WorldY).
        /// </summary>
        ConcurrentDictionary<uint, (int WorldX, int WorldY)> SpawnedPositions { get; set; }

        /// <summary>
        /// Gets or sets mob UIDs currently visible and eligible for the attack loop.
        /// Value byte is 1 (active) or 0 (suppressed/ignored).
        /// </summary>
        ConcurrentDictionary<uint, byte> MobUIDs { get; set; }

        /// <summary>
        /// Gets or sets items currently on the ground within spawn range, indexed by drop UID.
        /// Populated by group spawn body parsing. Key = Drop UID. Value = (RefObjID, CodeName, WorldX, WorldY).
        /// </summary>
        ConcurrentDictionary<uint, (uint RefObjID, string CodeName, int WorldX, int WorldY)> DroppedItems { get; set; }

        #endregion

        #region Runtime State

        /// <summary>Gets or sets the primary session-level cancellation token source. Cancelled on disconnect or logout.</summary>
        CancellationTokenSource? SessionTokenSource { get; set; }

        /// <summary>Gets or sets the cancellation token source for the active inventory sort operation.</summary>
        CancellationTokenSource? ActiveSortCts { get; set; }

        #endregion

        #region Account & Server Information

        /// <summary>Gets or sets the Joymax account identifier (JID).</summary>
        int JID { get; set; }

        /// <summary>Gets or sets the account username used to authenticate this session.</summary>
        string? AccountName { get; set; }

        /// <summary>Gets or sets the name of the game server this session is connected to.</summary>
        string? ServerName { get; set; }

        /// <summary>Gets or sets the agent server session identifier assigned at login.</summary>
        uint AgentSessionId { get; set; }

        #endregion

        #region Session Methods

        /// <summary>Increments <see cref="SessionUniqueKills"/> in a thread-safe manner.</summary>
        void IncrementSessionUniqueKills();

        /// <summary>Increments <see cref="SessionKills"/> in a thread-safe manner.</summary>
        void IncrementSessionKills();

        /// <summary>
        /// Calculates effective movement speed by applying all active percentage-based buff modifiers
        /// on top of the base <see cref="RunSpeed"/>.
        /// </summary>
        float GetCurrentMoveSpeed();

        #endregion
    }
}
