namespace VSRO_CONTROL_API.VSRO.Enums
{
    public enum SortResult
    {
        Continue,   // keep looping
        Completed,  // finished successfully
        Aborted     // stopped early (unsynced, error condition, etc.)
    }
}
