namespace VSRO_CONTROL_API.VSRO.AsynchronousProxy.DTO
{
    public class CosDespawnReturn
    {
        public uint PetUID { get; set; }
        public byte Type { get; set; }
        public bool HasInventoryPage { get; set; }
        public byte PageItemCount { get; set; }
    }
}
