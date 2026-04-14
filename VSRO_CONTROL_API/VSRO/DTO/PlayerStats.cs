namespace VSRO_CONTROL_API.VSRO.DTO
{
    public class PlayerStats
    {
        public uint STR { get; set; }
        public uint INT { get; set; }
        public uint CurrentHP { get; set; }
        public uint CurrentMP { get; set; }
        public uint ZerkLevel { get; set; }
        public uint CurrentLevel { get; set; }
        public uint UnusedStatPoints { get; set; }
        public ulong RemainingGold { get; set; }
        public uint RemainingSkillPoints { get; set; }
    }
}
