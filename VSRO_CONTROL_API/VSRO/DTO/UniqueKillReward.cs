namespace VSRO_CONTROL_API.VSRO.DTO
{
    public class UniqueKillReward
    {
        public List<Rewards> Reward { get; set; } = new();
        public int Gold { get; set; } = 0;
    }

    public class Rewards
    {
        public string ItemCodename { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public int Plus { get; set; } = 0;

    }


}
