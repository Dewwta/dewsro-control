namespace VSRO_CONTROL_API.VSRO.DTO
{
    public class SR_Buff
    {
        public uint ID { get; set; }
        public uint TimedJobId { get; set; }

        // Populated after DB lookup
        public string? CodeName { get; set; }
        public float? MoveSpeedPercent { get; set; }   // e.g. 470f = 470%
        public bool IsAutoTransfer { get; set; }
        public byte Creator { get; set; }
        public int[] Params { get; set; } = new int[50];
    }
}
