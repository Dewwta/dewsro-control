namespace VSRO_CONTROL_API.VSRO.DTO.Regions
{
    public class ReadableRegionEntry
    {
        public string ParentRegionName { get; set; } = "";
        public string Name { get; set; } = "";
        public int MinX { get; set; }
        public int MinY { get; set; }
        public int MaxX { get; set; }
        public int MaxY { get; set; }

    }
}
