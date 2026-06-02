namespace VSRO_CONTROL_API.VSRO.Bots.DTO
{
    public class PlayerBotData
    {
        public int TrainX { get; set; }
        public int TrainY { get; set; }
        public int TrainZ { get; set; }
        public int TrainR { get; set; } = 25;
        public int RegionId { get; set; }
        public List<uint> AttackSkillIds { get; set; } = new();
        public List<uint> BuffSkillIds { get; set; } = new();
        public BotSettings Settings { get; set; } = new();
    }
}
