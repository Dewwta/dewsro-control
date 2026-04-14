namespace VSRO_CONTROL_API.VSRO.DTO
{
    public class ItemRecord
    {
        public string CodeName { get; set; }
        public byte T1 { get; set; }
        public byte T2 { get; set; }
        public byte T3 { get; set; }
        public byte T4 { get; set; }

        public ushort MaxStack { get; set; }
    }
}
