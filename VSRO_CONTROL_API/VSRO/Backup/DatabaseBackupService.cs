using CoreLib.Tools.Logging;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
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

            var settings   = SettingsLoader.Settings;
            var backupPath = settings?.Backup?.BackupPath;

            if (string.IsNullOrWhiteSpace(backupPath))
                return (false, "BackupPath is not configured in settings.xml.");

            var dbList   = (settings?.Backup?.BackupDatabases ?? "SRO_VT_ACCOUNT,SRO_VT_SHARD,SRO_VT_LOG")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var maxCount = settings?.Backup!.BackupMaxCount > 0 ? settings.Backup!.BackupMaxCount : 10;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            var localErrors = new List<string>();
            var secErrors   = new List<string>();

            _isBusy = true;
            try
            {
                foreach (var db in dbList)
                {
                    string? filePath = null;
                    try
                    {
                        var dbDir = Path.Combine(backupPath, db);
                        Directory.CreateDirectory(dbDir);
                        filePath = Path.Combine(dbDir, $"{db}_{timestamp}.bak");

                        using var conn = DBConnect.OpenConnection("master");
                        await conn.OpenAsync().ConfigureAwait(false);

                        using var cmd = new SqlCommand(
                            $"BACKUP DATABASE [{db}] TO DISK = N'{filePath}' WITH FORMAT, STATS = 10",
                            conn
                        );
                        cmd.CommandTimeout = 0;
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

                        PruneOldBackups(dbDir, db, maxCount);
                        Logger.Info(typeof(DatabaseBackupService), $"Backed up [{db}] -> {filePath}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(typeof(DatabaseBackupService), $"Backup failed for [{db}]: {ex.Message}");
                        localErrors.Add($"{db}: {ex.Message}");
                        continue;
                    }

                    // Secondary upload — failures are warnings only, never affect the primary result
                    if (filePath != null)
                    {
                        var secOk = await UploadToSecondaryAsync(filePath, db, maxCount).ConfigureAwait(false);
                        if (!secOk) secErrors.Add(db);
                    }
                }
            }
            finally
            {
                _isBusy         = false;
                _lastRunAt      = DateTime.Now;
                _lastRunSuccess = localErrors.Count == 0;

                var msg = _lastRunSuccess
                    ? $"All {dbList.Length} database(s) backed up successfully."
                    : $"Completed with errors — {string.Join("; ", localErrors)}";

                if (secErrors.Count > 0)
                    msg += $" | Secondary upload failed for: {string.Join(", ", secErrors)}";

                _lastRunMessage = msg;
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
                IsBusy:         _isBusy,
                LastRunAt:      _lastRunAt?.ToString("o"),
                LastRunMessage: _lastRunMessage,
                LastRunSuccess: _lastRunSuccess,
                Files:          files
            );
        }

        // Returns true if upload succeeded OR if secondary is not configured (not an error).
        private static async Task<bool> UploadToSecondaryAsync(string localFilePath, string db, int maxCount)
        {
            var s    = SettingsLoader.Settings?.Backup;
            var host = s?.SecondaryHost;
            if (string.IsNullOrWhiteSpace(host)) return true;  // not configured — skip silently

            var user      = !string.IsNullOrWhiteSpace(s?.SecondaryUser) ? s!.SecondaryUser! : "vsrobackup";
            var basePath  = (s?.SecondaryPath ?? "/srv/vsro-backups").TrimEnd('/');
            var remoteDir = $"{basePath}/{db}";
            var sshOpts   = BuildSshOptions(s?.SecondaryKeyFile);

            Logger.Info(typeof(DatabaseBackupService),
                $"[Secondary] Starting upload of [{db}] -> {user}@{host}:{remoteDir}");

            // 1. Ensure the remote directory exists
            var mkdirOk = await RunCommandAsync(
                "ssh",
                $"{sshOpts} {user}@{host} \"mkdir -p {remoteDir}\"",
                TimeSpan.FromSeconds(30),
                $"[Secondary:mkdir:{db}]"
            ).ConfigureAwait(false);

            if (!mkdirOk) return false;

            // 2. Upload the .bak file (allow 30 min — files can be several GB)
            var uploadOk = await RunCommandAsync(
                "scp",
                $"{sshOpts} \"{localFilePath}\" {user}@{host}:\"{remoteDir}/\"",
                TimeSpan.FromMinutes(30),
                $"[Secondary:scp:{db}]"
            ).ConfigureAwait(false);

            if (!uploadOk) return false;

            Logger.Info(typeof(DatabaseBackupService),
                $"[Secondary] Upload complete: [{db}] -> {host}:{remoteDir}/{Path.GetFileName(localFilePath)}");

            var pruneCmd = $"ls -t {remoteDir}/{db}_*.bak 2>/dev/null | tail -n +{maxCount + 1} | xargs -r rm -f";
            await RunCommandAsync(
                "ssh",
                $"{sshOpts} {user}@{host} \"{pruneCmd}\"",
                TimeSpan.FromSeconds(30),
                $"[Secondary:prune:{db}]"
            ).ConfigureAwait(false);

            return true;
        }

        private static string BuildSshOptions(string? keyFile)
        {
            var opts = "-o BatchMode=yes -o StrictHostKeyChecking=accept-new -o ConnectTimeout=10";
            if (!string.IsNullOrWhiteSpace(keyFile))
                opts += $" -i \"{keyFile}\"";
            return opts;
        }

        private static async Task<bool> RunCommandAsync(string exe, string arguments, TimeSpan timeout, string logTag)
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName               = exe,
                Arguments              = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };

            try { proc.Start(); }
            catch (Exception ex)
            {
                Logger.Warn(typeof(DatabaseBackupService),
                    $"{logTag} Cannot launch '{exe}': {ex.Message} — is OpenSSH Client installed on this machine?");
                return false;
            }

            // Read both streams concurrently to prevent buffer deadlocks
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(timeout);
            try
            {
                await proc.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* best effort */ }
                Logger.Warn(typeof(DatabaseBackupService),
                    $"{logTag} Timed out after {timeout.TotalSeconds:F0}s — process killed");
                return false;
            }

            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

            if (proc.ExitCode == 0) return true;

            var err = stderrTask.Result.Trim();
            Logger.Warn(typeof(DatabaseBackupService),
                $"{logTag} Failed (exit {proc.ExitCode}){(string.IsNullOrEmpty(err) ? "" : $": {err}")}");
            return false;
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
