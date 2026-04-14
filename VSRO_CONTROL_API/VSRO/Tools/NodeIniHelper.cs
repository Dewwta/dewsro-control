using CoreLib.Processes;
using CoreLib.Tools.Logging;
using System.Diagnostics;
using VSRO_CONTROL_API.Settings;
using VSRO_CONTROL_API.VSRO.DTO;

namespace VSRO_CONTROL_API.VSRO.Tools
{
    public class NodeIniHelper
    {
        private string? root = null;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        public NodeIniHelper(string _rootOfInis)
        {
            root = _rootOfInis;
        }

        #region - Patching -

        /// <summary>
        /// Patches the cert ini's for the VSRO 1.188 Certification server and compiles them. You must create the NodeIniHelper object, and
        /// use this function, then you run iniPatcher.CompileCert(pathToCertCompileBat). You can set each paramter to null to avoid it being
        /// edited at all.
        /// </summary>
        /// <param name="_ipToSet">The ip to be set</param>
        /// <param name="_shardNameToSet">The new shard name (this is the name of the server in the login screen)</param>
        /// <param name="_accountQueryToSet">The SRO_VT_ACCOUNT query for the srGlobalService.ini</param>
        /// <param name="_shardQueryToSet">The SRO_VT_SHARD query for the srShard.ini</param>
        /// <param name="_logQueryToSet">The SRO_VT_LOG query for the srShard.ini</param>
        /// <returns>A boolean of true == succesful</returns>
        public async Task<bool> PatchAllInis(string? _ipToSet, string? _shardNameToSet, string? _accountQueryToSet, string? _shardQueryToSet, string? _logQueryToSet)
        {
            if (root == null)
            {
                Logger.Warn(this, $"The root was null");
                return false;
            }
            if (_ipToSet == null && _shardNameToSet == null && _accountQueryToSet == null && _shardQueryToSet == null && _logQueryToSet == null)
            {
                Logger.Warn(this, $"No parameters were set, doing nothing.");
                return false;
            }
            await _lock.WaitAsync();
            try
            { 
                bool isPatchNodeTypeOk = await PatchNodeTypeIni(root, _ipToSet!, _shardNameToSet!);
                bool isPatchGlobalServiceOk = await PatchGlobalServiceIni(root, _shardNameToSet!, _accountQueryToSet!);
                bool isPatchShardIniOk = await PatchShardIni(root, _shardNameToSet!, _shardQueryToSet!, _logQueryToSet!);

                return isPatchNodeTypeOk && isPatchGlobalServiceOk && isPatchShardIniOk;
            }
            catch (Exception ex)
            {
                Logger.Error(this, $"Error ocurred when patching all inis!: {ex.Message}");
                return false;
            }
            finally
            {
                _lock.Release();
            }
        }
        
        private async Task<bool> PatchNodeTypeIni(string path, string? ip, string? _shardName)
        {
            string filename = "srNodeType.ini";
            string iniPath = Path.Combine(path, filename);

            if (_shardName == null && ip == null)
                return true;

            bool changed = false;
            if (!File.Exists(iniPath))
            {
                Logger.Error(typeof(Overseer), $"srNodeType.ini not found at: {path}");
                return false;
            }

            var lines = await File.ReadAllLinesAsync(iniPath);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("wip="))
                {
                    if (ip == null) continue;
                    string old = lines[i];
                    lines[i] = $"wip={ip}";
                    Logger.Info(typeof(Overseer), $"srNodeType.ini - 1/3 patched: '{old}' → '{lines[i]}'");
                    changed = true;
                }
                else if (lines[i].TrimStart().StartsWith("nip="))
                {
                    if (ip == null) continue;
                    string old = lines[i];
                    lines[i] = $"nip={ip}";
                    Logger.Info(typeof(Overseer), $"srNodeType.ini - 2/3 patched: '{old}' → '{lines[i]}'");
                    changed = true;
                }
                else if (lines[i].TrimStart().StartsWith("name="))
                {
                    if (_shardName == null) continue;
                    string old = lines[i];
                    lines[i] = $"name={_shardName}";
                    Logger.Info(typeof(Overseer), $"srNodeType.ini - 3/3 patched: '{old}' → '{lines[i]}'");
                    changed = true;
                }
            }

            if (changed == true)
                await File.WriteAllLinesAsync(iniPath, lines);
            else
                Logger.Warn(this, $"[{filename}] No changes were inputted. No work to do.");

            return true;
        }
        private async Task<bool> PatchGlobalServiceIni(string path, string? _shardName, string? _accountQuery)
        {
            string filename = "srGlobalService.ini";
            string iniPath = Path.Combine(path, filename);
            
            if (_shardName == null && _accountQuery == null)
                return true;

            bool changed = false;
            if (!File.Exists(iniPath))
            {
                Logger.Error(typeof(Overseer), $"{filename} not found at: {path}");
                return false;
            }

            var lines = await File.ReadAllLinesAsync(iniPath);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("name="))
                {
                    if (_shardName == null) continue;
                    string old = lines[i];
                    lines[i] = $"name={_shardName}";
                    Logger.Info(typeof(Overseer), $"srGlobalService.ini - 1/2 patched: '{old}' → '{lines[i]}'");
                    changed = true;
                }
                else if (lines[i].TrimStart().StartsWith("query="))
                {
                    if (_accountQuery == null) continue;
                    string old = lines[i];
                    lines[i] = $"query={_accountQuery}";
                    Logger.Info(typeof(Overseer), $"srGlobalService.ini - 2/2 patched: '{old}' → '{lines[i]}'");
                    changed = true;
                }
            }

            if (changed == true)
                await File.WriteAllLinesAsync(iniPath, lines);
            else
                Logger.Warn(this, $"[{filename}] No changes were inputted. No work to do.");
            

            return true;
        }
        private async Task<bool> PatchShardIni(string path, string? _shardName, string? _shardQuery, string? _logQuery)
        {
            string filename = "srShard.ini";
            string iniPath = Path.Combine(path, filename);

            if (_shardName == null && _shardQuery == null && _logQuery == null)
                return true;

            bool changed = false;
            if (!File.Exists(iniPath))
            {
                Logger.Error(typeof(Overseer), $"{filename} not found at: {path}");
                return false;
            }

            var lines = await File.ReadAllLinesAsync(iniPath);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("name="))
                {
                    if (_shardName == null) continue;
                    string old = lines[i];
                    lines[i] = $"name={_shardName}";
                    Logger.Info(typeof(Overseer), $"srShard.ini - 1/3 patched: '{old}' → '{lines[i]}'");
                    changed = true;
                }
                else if (lines[i].TrimStart().StartsWith("query="))
                {
                    if (_shardQuery == null) continue;
                    string old = lines[i];
                    lines[i] = $"query={_shardQuery}";
                    Logger.Info(typeof(Overseer), $"srShard.ini - 2/3 patched: '{old}' → '{lines[i]}'");
                    changed = true;
                }
                else if (lines[i].TrimStart().StartsWith("query_log="))
                {
                    if (_logQuery == null) continue;
                    string old = lines[i];
                    lines[i] = $"query_log={_logQuery}";
                    Logger.Info(typeof(Overseer), $"srShard.ini - 3/3 patched: '{old}' → '{lines[i]}'");
                    changed = true;
                }
            }
            if (changed == true)
                await File.WriteAllLinesAsync(iniPath, lines);
            else
                Logger.Warn(this, $"[{filename}] No changes were inputted. No work to do.");

            return true;
        }

        /// <summary>
        /// Compiles the certification server. Make sure the cert compiler .bat file is input in settings.xml
        /// A 2 second delay is given for the compilation to avoid File Not Found errors.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> CompileCert(string path)
        {
            try
            {
                Logger.Info(typeof(Overseer), "Compiling cert");
                ProcessTools.RunBat(path);
                await Task.Delay(2000);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(this, $"Error ocurred when compiling certification server!: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region - Reading -

        /// <summary>
        /// Reads the current values from all three cert INI files without modifying them.
        /// Returns null if the root path is not set.
        /// </summary>
        public async Task<NodeIniValues?> ReadAllInis()
        {
            if (root == null)
            {
                Logger.Warn(this, "The root was null, cannot read INIs.");
                return null;
            }

            await _lock.WaitAsync();
            try
            {
                var values = new NodeIniValues();

                string nodeTypePath = Path.Combine(root, "srNodeType.ini");
                if (File.Exists(nodeTypePath))
                {
                    foreach (var line in await File.ReadAllLinesAsync(nodeTypePath))
                    {
                        if (line.TrimStart().StartsWith("wip="))
                            values.Wip = line.Split('=', 2).ElementAtOrDefault(1)?.Trim();
                        else if (line.TrimStart().StartsWith("nip="))
                            values.Nip = line.Split('=', 2).ElementAtOrDefault(1)?.Trim();
                        else if (line.TrimStart().StartsWith("name="))
                            values.ShardName = line.Split('=', 2).ElementAtOrDefault(1)?.Trim();
                    }
                }

                string globalServicePath = Path.Combine(root, "srGlobalService.ini");
                if (File.Exists(globalServicePath))
                {
                    foreach (var line in await File.ReadAllLinesAsync(globalServicePath))
                    {
                        if (line.TrimStart().StartsWith("query="))
                            values.AccountQuery = line.Split('=', 2).ElementAtOrDefault(1)?.Trim();
                    }
                }

                string shardPath = Path.Combine(root, "srShard.ini");
                if (File.Exists(shardPath))
                {
                    foreach (var line in await File.ReadAllLinesAsync(shardPath))
                    {
                        if (line.TrimStart().StartsWith("query_log="))
                            values.LogQuery = line.Split('=', 2).ElementAtOrDefault(1)?.Trim();
                        else if (line.TrimStart().StartsWith("query="))
                            values.ShardQuery = line.Split('=', 2).ElementAtOrDefault(1)?.Trim();
                    }
                }

                return values;
            }
            catch (Exception ex)
            {
                Logger.Error(this, $"Error reading INIs: {ex.Message}");
                return null;
            }
            finally
            {
                _lock.Release();
            }
        }

        #endregion


    }

    
}
