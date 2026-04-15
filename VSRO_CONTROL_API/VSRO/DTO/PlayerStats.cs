namespace VSRO_CONTROL_API.VSRO.DTO
{
    public class PlayerStats
    {
        public uint STR { get; set; } = 1;
        public uint INT { get; set; } = 1;
        public uint CurrentHP { get; set; } = 1;
        public uint CurrentMP { get; set; } = 1;
        public uint MaxHP { get; set; } = 1;
        public uint MaxMP { get; set; } = 1;
        public uint ZerkLevel { get; set; } = 0;
        public uint CurrentLevel { get; set; } = 1;
        public uint UnusedStatPoints { get; set; } = 0;
        public ulong RemainingGold { get; set; } = 0;
        public uint RemainingSkillPoints { get; set; } = 0;
    }
}
