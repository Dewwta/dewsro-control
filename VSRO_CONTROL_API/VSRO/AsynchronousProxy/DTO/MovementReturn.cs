namespace VSRO_CONTROL_API.VSRO.AsynchronousProxy.DTO
{
    public class MovementReturn
    {
        public uint UniqueId { get; set; }
        public bool HasMovement { get; set; }
        public ushort RegionId { get; set; }
        public int WorldX { get; set; }
        public int WorldY { get; set; }
        public int WorldZ { get; set; }
        public byte SectorX { get; set; }
        public byte SectorY { get; set; }
        public int RawX { get; set; }
        public int RawY { get; set; }
    }
}
