namespace VSRO_CONTROL_API.VSRO.DTO
{
    public class SnapshotItem
    {
        public int ItemID { get; set; }
        public string CodeName { get; set; } = "";
        public int Stack { get; set; }
        public int MaxStack { get; set; }
    }

    public class CharacterSnapshot
    {
        public string CharacterName { get; set; } = "";
        public uint CharacterID { get; set; }
        public int JID { get; set; }
        public DateTime SavedAt { get; set; }

        // Stats
        public uint Level { get; set; }
        public uint CurrentHP { get; set; }
        public uint CurrentMP { get; set; }
        public uint ZerkLevel { get; set; }
        public uint UnusedStatPoints { get; set; }
        public ulong Gold { get; set; }
        public uint SkillPoints { get; set; }

        // Inventory (slot index -> item)
        public Dictionary<byte, SnapshotItem> Equipment { get; set; } = new();
        public Dictionary<byte, SnapshotItem> Slots { get; set; } = new();
        // Pet UID (hex string) -> slot index -> item
        public Dictionary<string, Dictionary<byte, SnapshotItem>> Pets { get; set; } = new();
    }

    public class CharacterRecord
    {
        public string Login { get; set; } = "";
        public string CharName { get; set; } = "";
        public int SilkOwn { get; set; }
        public int SilkGift { get; set; }
        public int JID { get; set; }
        public uint CharID { get; set; }
    }

    public class CharacterWithStateDTO
    {
        public string Login { get; set; } = "";
        public string CharName { get; set; } = "";
        public int SilkOwn { get; set; }
        public int SilkGift { get; set; }
        public int JID { get; set; }
        public uint CharID { get; set; }
        public bool IsOnline { get; set; }
        public CharacterSnapshot? LastKnownState { get; set; }
    }
}
