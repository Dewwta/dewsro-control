using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace CoreLib.Tools.Logging
{
    public static class Logger
    {
        #region - Var -

        // Path to logs
        private static string _logSavePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"logs");

        // Timers for saving and cleanup
        private static Timer? _saveTimer;
        private static Timer? _cleanupTimer;

        // Locks
        private static readonly object _consoleLock = new object();
        private static readonly object _logLock = new object();
        
        // Set once
        private static bool _hasInit = false;

        // History
        private static List<string> logHistory = new List<string>();

        // Mutating bools for threads
        private static bool _saveInProgress;
        private static bool _cleanupInProgress;

        public static uint _oldestLogAllowedInDays = 30;
        public static uint _saveTimerInHours = 12;
        public static uint _cleanupTimerInHours = 24;

        #endregion

        #region - Debug -

        private static bool _debug = false;
        public static void SetDebug(bool debug)
        {
            if (_debug == true) return;
            _debug = debug;
        }

        #endregion

        #region - Default Logging -

        #region - Special -

        public static void Okay()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("OK");
            Console.ForegroundColor = ConsoleColor.White;

        }
        public static void Fail()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FAIL");
            Console.ForegroundColor = ConsoleColor.White;
        }

        #endregion

        #region - Normal -
        
        public static void Debug(object obj, string msg)
        {
            if (_debug == true)
                Log_Out(obj, "DEBUG", msg);
        }

        public static void Info(object obj, string msg)
        {
            Log_Out(obj, "INFO", msg);
        }

        public static void Warn(object obj, string msg)
        {
            Log_Out(obj, "WARN", msg);
        }

        public static void Error(object obj, string msg)
        {
            Log_Out(obj, "ERROR", msg);
        }

        #endregion

        #region - No New Line -

        public static void InfoRaw(object obj, string msg)
        {
            Log_Raw(obj, "INFO", msg);
        }

        public static void WarnRaw(object obj, string msg)
        {
            Log_Raw(obj, "WARN", msg);
        }

        public static void ErrorRaw(object obj, string msg)
        {
            Log_Raw(obj, "ERROR", msg);
        }


        #endregion

        #region - Private -

        private static void Log_Out(object obj, string level, string msg)
        {
            string name = obj switch
            {
                null => "Unknown",
                Type t => t.Name,
                string s => s,
                _ => obj.GetType().Name
            };

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string one = $"[{timestamp}] [{name}] ";
            string two = $"{level}:";
            string three = $" {msg}";

            lock (_logLock)
            {
                string finalMsg = one + two + three;
                logHistory.Add(finalMsg);
            }
            lock (_consoleLock)
            {
                switch (level)
                {
                    case "ERROR":
                        // stderr
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Error.Write(one);
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.Write(two); // stderr
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine(three); // stderr
                        break;
                    case "WARN":
                        // stderr
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Error.Write(one);
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Error.Write(two); // stderr
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Error.WriteLine(three); // stderr
                        break;
                    case "INFO":
                        // stdout
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write(one);
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.Write(two); // stderr
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine(three); // stderr
                        break;
                    case "DEBUG":
                        break;
                    default:
                        Warn(typeof(Logger), $"Unknown log level?: {level}");
                        break;

                }

                Console.ForegroundColor = ConsoleColor.White;
            }
            
        }
        private static void Log_Raw(object obj, string level, string msg)
        {
            string name;

            // If it's a Type object, use its Name directly
            if (obj is Type t)
                name = t.Name;
            else
                name = obj.GetType().Name;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string one = $"[{timestamp}] [{name}] ";
            string two = $"{level}:";
            string three = $" {msg}";

            lock (_logLock)
            {
                string finalMsg = one + two + three;
                logHistory.Add(finalMsg);
            }

            lock (_consoleLock)
            {
                switch (level)
                {
                    case "ERROR":
                        // stderr
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Error.Write(one);
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.Write(two);
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Error.Write(three);
                        break;
                    case "WARN":
                        // stderr
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Error.Write(one);
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Error.Write(two);
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Error.Write(three);
                        break;
                    case "INFO":
                        // stdout
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write(one);
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.Write(two);
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write(three);
                        break;
                    default:
                        Warn(typeof(Logger), $"Unknown log level?: {level}");
                        break;

                }

                Console.ForegroundColor = ConsoleColor.White;
            }

            
        }

        #endregion

        #endregion

        #region - Initializing -

        public static void Init(LoggerSettings settings)
        {
            bool isSavingEnabled = true;
            InfoRaw(typeof(Logger), "Setting up logger...");
            CheckLogDir();
            if (settings != null)
            {
                if (settings.OldestLogAllowedInDays is int days)
                {
                    _oldestLogAllowedInDays = (uint)days;
                }

                if (settings.SaveTimerInHours is int saveHours)
                {
                    _saveTimerInHours = (uint)settings.SaveTimerInHours;
                }

                if (settings.CleanupTimerInHours is int cleanupHours)
                {
                    _cleanupTimerInHours = (uint)settings.CleanupTimerInHours;
                }

                if (settings.OldestLogAllowedInDays == null || settings.SaveTimerInHours == null || settings.CleanupTimerInHours == null)
                {
                    isSavingEnabled = false;
                }
            }
            

            if (isSavingEnabled == true)
            {
                _saveTimer = new Timer(SaveLogs, null, TimeSpan.FromHours(_saveTimerInHours), TimeSpan.FromHours(_saveTimerInHours));
                _cleanupTimer = new Timer(CleanupOldLogs, null, TimeSpan.Zero, TimeSpan.FromHours(_cleanupTimerInHours));
                AppDomain.CurrentDomain.ProcessExit += (s, e) => ForceSave();

            }


            Okay();
            _hasInit = true;
        }

        #endregion

        #region - Saving + Cleanup -

        private static void SaveLogs(object? state)
        {
            if (_saveInProgress)
                return;

            _saveInProgress = true;

            try
            {
                Thread saveThread = new Thread(() =>
                {
                    try
                    {
                        List<string> logsToSave;

                        lock (_logLock)
                        {
                            if (logHistory.Count == 0)
                            {
                                _saveInProgress = false;
                                return;
                            }

                            logsToSave = new List<string>(logHistory);
                            logHistory.Clear();
                        }


                        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                        string filename;

                        filename = Path.Combine(_logSavePath, $"log_{timestamp}.txt");

                        File.WriteAllLines(filename, logsToSave);
                        Info(typeof(Logger), $"Saved {logsToSave.Count} log entries to {filename}");
                    }
                    catch (Exception ex)
                    {
                        Error(typeof(Logger), $"Failed to save logs: {ex.Message}");
                    }
                    finally
                    {
                        _saveInProgress = false;
                    }
                });
                saveThread.IsBackground = true;
                saveThread.Start();
            }
            catch (Exception ex)
            {
                Error(typeof(Logger), $"Failed to start log save thread: {ex.Message}");
                _saveInProgress = false;
                return;
            }
        }
        private static void CleanupOldLogs(object? state)
        {
            if (_cleanupInProgress)
                return;

            _cleanupInProgress = true;

            try
            {
                Thread cleanupThread = new Thread(() =>
                {
                    try
                    {
                        Info(typeof(Logger), "Starting log cleanup routine...");

                        // Get all log files
                        string[] logFiles = Directory.GetFiles(_logSavePath, "log_*.txt");
                        int deletedCount = 0;

                        // Get current date for comparison
                        DateTime now = DateTime.Now;

                        foreach (string logFile in logFiles)
                        {
                            try
                            {
                                FileInfo fi = new FileInfo(logFile);

                                // Check if file is older than 7 days
                                if ((now - fi.CreationTime).TotalDays > _oldestLogAllowedInDays)
                                {
                                    File.Delete(logFile);
                                    deletedCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Error(typeof(Logger), $"Failed to process log file {logFile}: {ex.Message}");
                            }
                        }

                        if (deletedCount > 0)
                        {
                            Info(typeof(Logger), $"Cleanup complete. Removed {deletedCount} log files older than 7 days.");
                        }
                        else
                        {
                            Info(typeof(Logger), "Cleanup complete. No old log files to remove.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Error(typeof(Logger), $"Failed to cleanup logs: {ex.Message}");
                    }
                    finally
                    {
                        _cleanupInProgress = false;
                    }

                });

                cleanupThread.IsBackground = true;
                cleanupThread.Start();
            }
            catch (Exception ex)
            {
                Error(typeof(Logger), $"Failed to start log cleanup thread: {ex.Message}");
                _cleanupInProgress = false;
            }
        }

        public static void ForceSave()
        {
            SaveLogs(null);
        }
        public static void ForceCleanup()
        {
            CleanupOldLogs(null);
        }

        #endregion

        #region - Helpers + API -

        // Helpers
        private static void CheckLogDir()
        {
            if (!Directory.Exists(_logSavePath))
            {
                Info(typeof(Logger), "Logs save directory doesnt exist, creating...");
                Directory.CreateDirectory(_logSavePath);
            }
        }

        // Public
        public static List<string> GetAllLogs()
        {
            lock (_logLock)
                return new List<string>(logHistory);
        }
        public static List<string> GetLatest(int count)
        {
            return logHistory.TakeLast(count).ToList();
        }

        /// <summary>
        /// Clears the in-memory log history. Does not affect saved log files on disk.
        /// </summary>
        public static void ClearHistory()
        {
            lock (_logLock)
                logHistory.Clear();
            Info(typeof(Logger), "In-memory log history cleared.");
        }

        #endregion


    }
}
