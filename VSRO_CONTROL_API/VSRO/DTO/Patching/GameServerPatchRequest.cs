namespace VSRO_CONTROL_API.VSRO.DTO.Patching
{
    public class GameServerPatchRequest
    {
        public int? MaxLevel { get; set; } = null;
        public int? MasteryLimit { get; set; } = null;
        public bool FixRates { get; set; } = false;
        public bool DisableDumpFiles { get; set; } = false;
        public bool DisableGreenBook { get; set; } = false;
        public string? IpToSet { get; set; } = null;
        public int? ObjectLimitToSet { get; set; } = null;
    }
}
