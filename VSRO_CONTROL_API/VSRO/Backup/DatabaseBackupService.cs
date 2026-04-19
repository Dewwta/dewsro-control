using CoreLib.Tools.Logging;
using Microsoft.Data.SqlClient;
using VSRO_CONTROL_API.Settings;

namespace VSRO_CONTROL_API.VSRO.Backup
{
    public record BackupFileInfo(string Database, string FileName, long SizeBytes, string CreatedAt);

    public record BackupStatus(
        bool IsBusy,
        string? LastRunAt,
        string LastRunMessage,
        bool LastRunSuccess,
        List<BackupFileInfo> Files
    );

    public class DatabaseBackupService : BackgroundService
    {
        public static DatabaseBackupService? Instance { get; private set; }

        private volatile bool _isBusy;
        private DateTime? _lastRunAt;
        private string _lastRunMessage = "";
        private bool _lastRunSuccess;

        public DatabaseBackupService() { Instance = this; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken).ConfigureAwait(false);

            while (!stoppingToken.IsCancellationRequested)
            {
                await RunBackupsAsync().ConfigureAwait(false);
                try
                {
                    await Task.Delay(TimeSpan.FromHours(2), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
            }
        }

        public async Task<(bool Success, string Message)> RunBackupsAsync()
        {
            if (_isBusy)
                return (false, "A backup is already in progress.");

            var settings  = SettingsLoader.Settings;
            var backupPath = settings?.Backup?.BackupPath;

            if (string.IsNullOrWhiteSpace(backupPath))
                return (false, "BackupPath is not configured in settings.xml.");

            var dbList = (settings?.Backup?.BackupDatabases ?? "SRO_VT_ACCOUNT,SRO_VT_SHARD,SRO_VT_LOG")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var maxCount  = settings?.Backup!.BackupMaxCount > 0 ? settings.Backup!.BackupMaxCount : 10;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var errors    = new List<string>();

            _isBusy = true;
            try
            {
                foreach (var db in dbList)
                {
                    try
                    {
                        var dbDir    = Path.Combine(backupPath, db);
                        Directory.CreateDirectory(dbDir);

                        var filePath = Path.Combine(dbDir, $"{db}_{timestamp}.bak");

                        using var conn = DBConnect.OpenConnection("master");
                        await conn.OpenAsync().ConfigureAwait(false);

                        using var cmd = new SqlCommand(
                            $"BACKUP DATABASE [{db}] TO DISK = N'{filePath}' WITH FORMAT, STATS = 10",
                            conn
                        );
                        cmd.CommandTimeout = 0; // backups can take a while
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

                        PruneOldBackups(dbDir, db, maxCount);
                        Logger.Info(typeof(DatabaseBackupService), $"Backed up [{db}] → {filePath}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(typeof(DatabaseBackupService), $"Backup failed for [{db}]: {ex.Message}");
                        errors.Add($"{db}: {ex.Message}");
                    }
                }
            }
            finally
            {
                _isBusy         = false;
                _lastRunAt      = DateTime.Now;
                _lastRunSuccess = errors.Count == 0;
                _lastRunMessage = _lastRunSuccess
                    ? $"All {dbList.Length} database(s) backed up successfully."
                    : $"Completed with errors — {string.Join("; ", errors)}";
            }

            return (_lastRunSuccess, _lastRunMessage);
        }

        public BackupStatus GetStatus()
        {
            var settings   = SettingsLoader.Settings;
            var backupPath = settings?.Backup?.BackupPath ?? "";
            var dbList     = (settings?.Backup?.BackupDatabases ?? "SRO_VT_ACCOUNT,SRO_VT_SHARD,SRO_VT_LOG,SRO_VT_PROXY")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var files = new List<BackupFileInfo>();

            if (!string.IsNullOrWhiteSpace(backupPath))
            {
                foreach (var db in dbList)
                {
                    var dbDir = Path.Combine(backupPath, db);
                    if (!Directory.Exists(dbDir)) continue;

                    var dbFiles = Directory.GetFiles(dbDir, $"{db}_*.bak")
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.CreationTime)
                        .Select(f => new BackupFileInfo(db, f.Name, f.Length, f.CreationTime.ToString("o")));

                    files.AddRange(dbFiles);
                }
            }

            return new BackupStatus(
                IsBusy:          _isBusy,
                LastRunAt:       _lastRunAt?.ToString("o"),
                LastRunMessage:  _lastRunMessage,
                LastRunSuccess:  _lastRunSuccess,
                Files:           files
            );
        }

        private static void PruneOldBackups(string dir, string db, int maxCount)
        {
            var old = Directory.GetFiles(dir, $"{db}_*.bak")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .Skip(maxCount);

            foreach (var f in old)
            {
                try { f.Delete(); Logger.Info(typeof(DatabaseBackupService), $"Pruned old backup: {f.Name}"); }
                catch (Exception ex) { Logger.Warn(typeof(DatabaseBackupService), $"Could not prune {f.Name}: {ex.Message}"); }
            }
        }
    }
}
