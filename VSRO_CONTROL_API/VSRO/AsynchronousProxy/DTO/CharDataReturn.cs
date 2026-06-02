using System.Collections.Concurrent;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Framework;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Tracking;
using VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.DTO.VSRO_CONTROL_API.VSRO.DTO;

namespace VSRO_CONTROL_API.VSRO.AsynchronousProxy.DTO
{
    public class CharDataReturn
    {
        public uint CharacterUID { get; set; }
        public string CharacterName { get; set; } = "";
        public uint LastLoginTime { get; set; }
        public uint RefObjId { get; set; }
        public byte Scale { get; set; }
        public byte CurrentLevel { get; set; }
        public byte MaxLevel { get; set; }
        public ulong ExpOffset { get; set; }
        public uint SkillExpOffset { get; set; }
        public ulong RemainingGold { get; set; }
        public uint RemainingSkillPoint { get; set; }
        public ushort RemainingStatPoint { get; set; }
        public byte RemainingZerkBubbles { get; set; }
        public uint GatherExp { get; set; }
        public uint HP { get; set; }
        public uint MP { get; set; }
        public byte AutoInvestExp { get; set; }
        public byte DailyPK { get; set; }
        public ushort TotalPK { get; set; }
        public uint PKPenaltyPoint { get; set; }
        public byte ZerkTitleLevel { get; set; }
        public byte FreePVPFlag { get; set; }
        public byte InventorySize { get; set; }
        public byte ItemCount { get; set; }
        public byte AvatarSize { get; set; }
        public byte AvatarCount { get; set; }
        public int MasteryCount { get; set; }
        public int SkillCount { get; set; }
        public byte SectorX { get; set; }
        public byte SectorY { get; set; }
        public float RawX { get; set; }
        public float RawY { get; set; }
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public float WorldZ { get; set; }
        public int RegionId { get; set; }
        public string? RegionName { get; set; }
        public float WalkSpeed { get; set; }
        public float RunSpeed { get; set; }
        public bool IsGM { get; set; }
        public uint AccountJID { get; set; }
        public byte JobType { get; set; }
        public byte JobLevel { get; set; }
        public uint JobExp { get; set; }
        public uint JobContribution { get; set; }
        public uint JobReward { get; set; }


        public List<SR_Mastery?> MasteryList { get; set; } = new();
        public List<SR_Buff?> Buffs { get; set; } = new();
        public List<SR_Skill> LearnedSkills { get; set; } = new();

        public ConcurrentDictionary<byte, SR_Item> Avatars { get; } = new();
        public ConcurrentDictionary<byte, SR_Item> Slots { get; } = new();
        public ConcurrentDictionary<byte, SR_Item> Equipment { get; } = new();
        

    }
}
