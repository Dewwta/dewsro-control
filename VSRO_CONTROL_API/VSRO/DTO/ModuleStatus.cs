namespace VSRO_CONTROL_API.VSRO.DTO
{
    public class ModuleStatus
    {
        public string? Name { get; set; }
        public bool IsRunning { get; set; }
        public int? ProcessId { get; set; }
        public double CpuUsage { get; set; }
        public long MemoryBytes { get; set; }
        public DateTime? StartTime { get; set; }
        public bool IsResponsive { get; set; }

    }
}
