namespace VSRO_CONTROL_API.VSRO.AsynchronousProxy.DTO
{
    public class ItemUseReturn
    {
        public byte Result { get; set; }
        public byte Slot { get; set; }
        public ushort RemainingStack { get; set; }
    }
}
