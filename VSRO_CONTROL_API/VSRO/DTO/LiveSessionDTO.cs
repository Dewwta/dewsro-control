using System.Collections.Concurrent;

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
        public uint CurrentMP { get; set; }
        public uint ZerkLevel { get; set; }
        public uint UnusedStatPoints { get; set; }
        public ulong Gold { get; set; }
        public uint SkillPoints { get; set; }
        public uint STR { get; set; }
        public uint INT { get; set; }
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
