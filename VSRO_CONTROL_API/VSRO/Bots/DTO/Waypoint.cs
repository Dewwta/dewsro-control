namespace VSRO_CONTROL_API.VSRO.Bots.DTO
{
    public class Waypoint
    {
        public short RegionID { get; set; }
        public short RawX { get; set; }
        public short RawY { get; set; }
        public short Z { get; set; }
        public int WorldX { get; set; }
        public int WorldY { get; set; }
    }
}
