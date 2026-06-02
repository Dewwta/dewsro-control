using VSRO_CONTROL_API.VSRO.Bots.DTO;

namespace VSRO_CONTROL_API.VSRO.Bot.DTO
{
    public class BotConfig
    {
        // Connection
        public string GatewayHost { get; set; } = "";
        public int GatewayPort { get; set; } = 15779;
        public byte Locale { get; set; } = 0x22;
        public ushort ServerId { get; set; }

        // Credentials
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string CharacterName { get; set; } = "";

        // Bot behaviour
        public BotTrainplace? TrainingCenter { get; set; }
        public int TrainingRadius { get; set; } = 50;
        //public List<SkillEntry> SkillRotation { get; set; } = new();
    }
}
