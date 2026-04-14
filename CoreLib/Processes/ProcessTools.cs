using CoreLib.Tools.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreLib.Processes
{
    public class ProcessTools
    {
        public static Process? LaunchProcessTracked(string exePath, int delayMs, string name, string args = "")
        {
            try
            {
                var p = new Process();
                p.StartInfo.FileName = exePath;
                p.StartInfo.WorkingDirectory = Path.GetDirectoryName(exePath);
                if (!string.IsNullOrEmpty(args))
                    p.StartInfo.Arguments = args;
                p.Start();
                Logger.Info(typeof(ProcessTools), $"Started: {name}");
                Thread.Sleep(delayMs);
                return p;
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(ProcessTools), $"Failed to start {name}: {ex.Message}");
                return null;
            }
        }

        public static Process? RunBat(string batPath)
        {
            try
            {
                var p = new Process();
                p.StartInfo.FileName = "cmd.exe";
                p.StartInfo.Arguments = $"/c \"{batPath}\"";
                p.StartInfo.WorkingDirectory = Path.GetDirectoryName(batPath);
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                return p;
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(ProcessTools), $"Failed to launch bat: {ex.Message}");
                return null;
            }
        }
    }
}
