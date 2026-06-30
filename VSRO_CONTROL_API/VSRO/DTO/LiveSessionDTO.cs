using System.Text.Json.Serialization;

namespace VSRO_CONTROL_API.VSRO.DTO
{
    public class LiveSessionDTO
    {
        public int ConnectionId { get; set; }
        public string CharacterName { get; set; } = "";
        public int JID { get; set; }
        public string IP { get; set; } = "";
        public DateTime LoginTime { get; set; }
        public double SessionSeconds { get; set; }
        public bool IsAfk { get; set; }
        public bool IsGM { get; set; }
        public bool InventoryReady { get; set; }
        public LivePartyDTO? Party { get; set; }
        public LiveStatsDTO? Stats { get; set; }
    }

    public class LivePartyDTO
    {
        public uint PartyId { get; set; }
        public string? Message { get; set; }
        public List<string> MemberNames { get; set; } = new();
        public string? LeaderName { get; set; }
    }

    public class LiveStatsDTO
    {
        public uint Level { get; set; }
        public uint CurrentHP { get; set; }
        public uint MaxHP { get; set; }
        public uint CurrentMP { get; set; }
        public uint MaxMP { get; set; }
        public uint ZerkLevel { get; set; }
        public uint UnusedStatPoints { get; set; }
        public ulong Gold { get; set; }
        public uint SkillPoints { get; set; }

        // [JsonPropertyName] fixes the camelCase serialiser producing "sTR"/"iNT"
        [JsonPropertyName("str")]
        public uint STR { get; set; }
        [JsonPropertyName("int")]
        public uint INT { get; set; }

        // World position (kept for backward-compat)
        public string CurrentRegion { get; set; } = "N/A";
        public int WorldX { get; set; }
        public int WorldY { get; set; }
        public int WorldZ { get; set; }

        // Extended: identity & race
        public string? Race { get; set; }
        public string? ServerName { get; set; }
        public uint CharacterUID { get; set; }
        public uint AgentSessionId { get; set; }

        // Extended: movement
        public bool IsMoving { get; set; }
        public float RunSpeed { get; set; }
        public byte SectorX { get; set; }
        public byte SectorY { get; set; }
        public int RegionId { get; set; }
        public string? RegionName { get; set; }
        public string? RegionReadableName { get; set; }

        // Extended: session statistics
        public uint SessionKills { get; set; }
        public uint SessionUniqueKills { get; set; }
        public ulong CumulativeExp { get; set; }
        public uint LastTargetUID { get; set; }

        // Extended: bot state
        public string? BotState { get; set; }
        public float? TrainingDestX { get; set; }
        public float? TrainingDestY { get; set; }
        public float? TrainingDestZ { get; set; }
        public int? TrainingDestRegionId { get; set; }

        // Extended: world tracking counts
        public int SpawnedObjectCount { get; set; }
        public int MobCount { get; set; }
        public int DroppedItemCount { get; set; }
    }

    public class LiveInventoryItemDTO
    {
        public byte Slot { get; set; }
        public int ItemId { get; set; }
        public string CodeName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int Stack { get; set; }
        public int MaxStack { get; set; }
        public string? IconUrl { get; set; }
    }

    public class LiveInventoryDTO
    {
        public int ConnectionId { get; set; }
        public string CharacterName { get; set; } = "";
        public List<LiveInventoryItemDTO> Equipment { get; set; } = new();
        public List<LiveInventoryItemDTO> Inventory { get; set; } = new();
        public Dictionary<string, List<LiveInventoryItemDTO>> Pets { get; set; } = new();
        public Dictionary<string, PetInfo> PetInfos { get; set; } = new();
    }
}
