namespace VSRO_CONTROL_API.VSRO.DTO
{
    public class VSROServerStatus
    {
        public bool IsRunning { get; set; } = false;
        public int ModulesOpened { get; set; } = 0;
        public int PlayersOnline { get; set; } = 0;
        public List<ModuleStatus> ModuleStatuses { get; set; } = new List<ModuleStatus>();
        public string StartupStage { get; set; } = "";

    }
}
