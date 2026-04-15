using CoreLib.Network;
using CoreLib.Processes;
using CoreLib.Tools.Logging;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using VSRO_CONTROL_API.Settings;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Framework;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network;
using VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.ServerCfg;
using VSRO_CONTROL_API.VSRO.Settings;
using VSRO_CONTROL_API.VSRO.Tools;

namespace VSRO_CONTROL_API.VSRO
{
    public static class Overseer
    {
        #region Win32

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public uint Type; public MOUSEINPUT MouseInput; }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx, dy;
            public uint mouseData, dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        enum ShowWindowCommands { Restore = 9, Show = 5, Minimize = 2, Maximize = 3 }

        public enum VirtualKey : int
        {
            RETURN = 0x0D,
            TAB = 0x09,
            ESCAPE = 0x1B,
            CONTROL = 0x11,
            SHIFT = 0x10,
            ALT = 0x12
        }

        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inputs, int size);
        [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, ShowWindowCommands cmd);
        [DllImport("user32.dll")] static extern void mouse_event(uint flags, int dx, int dy, uint data, int extra);
        [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);
        [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string windowTitle);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int nStdHandle);
        [DllImport("kernel32.dll")] static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);
        [DllImport("kernel32.dll")] static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        const int    STD_INPUT_HANDLE      = -10;
        const uint   ENABLE_QUICK_EDIT     = 0x0040;
        const uint   ENABLE_EXTENDED_FLAGS = 0x0080;

        static void DisableQuickEdit()
        {
            IntPtr handle = GetStdHandle(STD_INPUT_HANDLE);
            if (GetConsoleMode(handle, out uint mode))
            {
                mode &= ~ENABLE_QUICK_EDIT;
                mode |= ENABLE_EXTENDED_FLAGS;
                SetConsoleMode(handle, mode);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X, Y; }

        const uint MOUSEEVENTF_LEFTDOWN  = 0x0002;
        const uint MOUSEEVENTF_LEFTUP    = 0x0004;
        const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        const uint MOUSEEVENTF_RIGHTUP   = 0x0010;
        const uint WM_CHAR    = 0x0102;
        const uint WM_KEYDOWN = 0x0100;
        const uint WM_KEYUP   = 0x0101;

        static void PostString(IntPtr hwnd, string text)
        {
            foreach (char c in text)
            {
                PostMessage(hwnd, WM_CHAR, (IntPtr)c, IntPtr.Zero);
                Thread.Sleep(30); // Small delay between chars for stability
            }
        }

        static void PostKey(IntPtr hwnd, VirtualKey key)
        {
            PostMessage(hwnd, WM_KEYDOWN, (IntPtr)key, IntPtr.Zero);
            Thread.Sleep(50);
            PostMessage(hwnd, WM_KEYUP, (IntPtr)key, IntPtr.Zero);
        }

        static void ForceWindowToFront(IntPtr hwnd)
        {
            uint targetThread  = GetWindowThreadProcessId(hwnd, out _);
            uint currentThread = GetCurrentThreadId();

            AttachThreadInput(currentThread, targetThread, true);

            ShowWindow(hwnd, ShowWindowCommands.Restore);
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);

            AttachThreadInput(currentThread, targetThread, false);

            Thread.Sleep(300);
        }

        #endregion

        #region - Fields -

        private static StartupSettings? ss;
        private static ApiSettings Settings => SettingsLoader.Settings;
        private static readonly object _startupShutdownLock = new object();
        private static string dir = AppDomain.CurrentDomain.BaseDirectory;

        private static Process? CertServer     = null;
        private static Process? GlobalManager  = null;
        private static Process? DownloadServer = null;
        private static Process? GatewayServer  = null;
        private static Process? FarmManager    = null;
        private static Process? MachineManager = null;
        private static Process? AgentServer    = null;
        private static Process? ShardManager   = null;
        private static Process? GameServer     = null;
        private static Process? SMC            = null;

        public static bool HasInitialized = false;
        private static bool _isStarting   = false;
        private static string _stage      = string.Empty;

        public static Server? GatewayProxy = null;
        public static Server? AgentProxy = null;
        public static Server? DownloadProxy = null;

        private static string? _publicIP = null;
        private static Dictionary<string, List<object[]>> mAgentServerQueue = new Dictionary<string, List<object[]>>();
        private static TimeSpan mDownloadServerQueueTimeLimit = new TimeSpan(0, 0, 5);
        private static Dictionary<string, List<object[]>> mDownloadServerQueue = new Dictionary<string, List<object[]>>();
        private static TimeSpan mAgentServerQueueTimeLimit = new TimeSpan(0, 0, 5);

        public static Dictionary<ushort, Server.PacketTransferEventHandler> opcodeLogHandlers
                = new Dictionary<ushort, Server.PacketTransferEventHandler>();

        private static CancellationTokenSource? _monitorCts;
        private static Task? _monitorTask;
        public static Dictionary<(int NpcObjID, int TabIndex, int SlotIndex), (int RefItemID, string CodeName)> ShopLookup = new Dictionary<(int NpcObjID, int TabIndex, int SlotIndex), (int RefItemID, string CodeName)>();
        public static HashSet<int> ShopNPCIds = new HashSet<int>();
        
        #endregion

        #region - Startup -

        public static async Task<bool> Initialize()
        {
            try
            {
                DisableQuickEdit();

                string configFilePath = Path.Combine(dir, "configs", "Config.json");

                ss = await StartupSettings.Load(configFilePath);
                await ss.Save(configFilePath);
                Logger.Info(typeof(Overseer), "Overseer initialized.");
                _publicIP = await IPTools.GetPublicIPAsync();
                if (_publicIP == null)
                {
                    Logger.Error(typeof(Overseer), $"Public ip resolution failed. This happens if the server loses connection to the internet. Please verify your servers internet connection.");
                    Logger.Warn(typeof(Overseer), $"Shutting down...");
                    return HasInitialized;
                }
                HasInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(Overseer), $"Error occurred while initializing: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Fires off the startup sequence in the background.
        /// Returns false if startup is already in progress.
        /// </summary>
        public static Task<bool> StartVsroServer()
        {
            lock (_startupShutdownLock)
            {
                if (_isStarting) return Task.FromResult(false);
                _isStarting = true;
                _stage = string.Empty;
            }
            _ = Task.Run(RunStartupSequence);
            return Task.FromResult(true);
        }

        private static async Task RunStartupSequence()
        {
            try
            { 
                // Get public ip to patch Node ini's and server.cfgs, automate srPatcher
                _publicIP = await IPTools.GetPublicIPAsync();

                // Reload server.cfg so any edits made before startup are picked up
                ServerCfgParser.Instance?.Reload();
                Logger.Info(typeof(Overseer), "server.cfg reloaded for startup.");

                // Build the shop db
                await BuildShopDB();

                // ── Step 2: Start Cert module ─────────────────────────────────────
                _stage = "Starting Cert Server";
                CertServer = ProcessTools.LaunchProcessTracked(ss.CertServerPath!, 4000, "Cert Server", ss.CertServerArgs ?? "");
                if (CertServer == null)
                {
                    Logger.Error(typeof(Overseer), "Cert server failed to start!");
                    return;
                }
                _stage = "Cert Server Started";

                // ── Step 3: Start all server modules in order ─────────────────────
                Logger.Info(typeof(Overseer), "PLEASE DO NOT USE ANY INPUTS DURING STARTUP.");

                // ── Deploy staged quest.sct if present ───────────────────────────
                if (!string.IsNullOrWhiteSpace(ss.QuestSctTempPath) && !string.IsNullOrWhiteSpace(ss.QuestSctDestinationPath))
                {
                    string stagedSct = Path.Combine(ss.QuestSctTempPath, "Quest.sct");
                    if (File.Exists(stagedSct))
                    {
                        _stage = "Deploying Quest SCT";
                        Logger.Info(typeof(Overseer), $"Staged Quest.sct found, deploying to: {ss.QuestSctDestinationPath}");
                        try
                        {
                            Directory.CreateDirectory(ss.QuestSctDestinationPath);
                            string destFile = Path.Combine(ss.QuestSctDestinationPath, "Quest.sct");
                            File.Copy(stagedSct, destFile, overwrite: true);
                            File.Delete(stagedSct);
                            Logger.Info(typeof(Overseer), "Quest SCT deployed successfully.");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(typeof(Overseer), $"Failed to deploy Quest SCT: {ex.Message}");
                        }
                    }
                }
                
                _stage = "Starting Global Manager";
                GlobalManager = ProcessTools.LaunchProcessTracked(ss.GlobalManagerPath!, 3000, "Global Manager");

                _stage = "Starting Download Server";
                DownloadServer = ProcessTools.LaunchProcessTracked(ss.DownloadServerPath!, 3000, "Download Server");

                _stage = "Starting Machine Manager";
                MachineManager = ProcessTools.LaunchProcessTracked(ss.MachineManagerPath!, 3000, "Machine Manager");

                _stage = "Starting Farm Manager";
                FarmManager = ProcessTools.LaunchProcessTracked(ss.FarmManagerPath!, 3000, "Farm Manager");

                _stage = "Starting Gateway Server";
                GatewayServer = ProcessTools.LaunchProcessTracked(ss.GatewayServerPath!, 3000, "Gateway Server");

                _stage = "Starting Agent Server";
                AgentServer = ProcessTools.LaunchProcessTracked(ss.AgentServerPath!, 7000, "Agent Server");

                _stage = "Starting Shard Manager";
                ShardManager = ProcessTools.LaunchProcessTracked(ss.ShardManagerPath!, 8000, "Shard Manager");

                _stage = "Starting Game Server";
                GameServer = ProcessTools.LaunchProcessTracked(ss.GameServerPath!, 5000, "Game Server");

                // ── Step 4: Start Proxy ───────────────────────────────────────────
                _stage = "Starting Proxy";
                SetupProxy();

                // ── Step 5: Start SMC ─────────────────────────────────────────────
                _stage = "Starting SMC";
                SMC = ProcessTools.LaunchProcessTracked(ss.SMCPath!, 6000, "SMC");

                // ── Step 6: Automate SMC Login ────────────────────────────────────
                _stage = "SMC Login";
                Logger.Info(typeof(Overseer), "Waiting for SMC login window...");
                IntPtr loginHwnd = WaitForWindow("Login", timeoutMs: 8000);
                if (loginHwnd == IntPtr.Zero)
                {
                    Logger.Error(typeof(Overseer), "SMC Login window not found.");
                    return;
                }

                // Find the child controls inside the login dialog
                IntPtr usernameField = FindWindowEx(loginHwnd, IntPtr.Zero, "Edit", null!);
                IntPtr passwordField = FindWindowEx(loginHwnd, usernameField, "Edit", null!);

                // Send directly to each control — no focus needed at all
                PostString(usernameField, ss.SmcUsername!);
                Thread.Sleep(100);
                PostString(passwordField, ss.SmcPassword!);
                Thread.Sleep(100);
                PostKey(loginHwnd, VirtualKey.RETURN);

                Logger.Info(typeof(Overseer), "Login submitted. Waiting for SMC main window...");

                // ── Step 7: Automate SMC "Launch All Nodes" ───────────────────────
                // Give SMC time to log in and load the node tree
                Thread.Sleep(6000);
                _stage = "Launching All Nodes";
                bool launched = TryLaunchAllNodes(ss.SmcMainWindowTitle!);
                int maxTries = 3;

                if (!launched)
                {
                    for (int i = 0; i < maxTries; i++)
                    {
                        if (TryLaunchAllNodes(ss.SmcMainWindowTitle!))
                        {
                            launched = true;
                            Logger.Info(typeof(Overseer), $"Launched nodes after {i + 1} attempt(s).");
                            break;
                        }
                        Thread.Sleep(10000);
                    }
                }

                if (launched)
                    Logger.Info(typeof(Overseer), "All nodes launched via SMC.");
                else
                    Logger.Warn(typeof(Overseer), "Failed to launch all nodes via SMC.");

                _stage = "Running";
                Logger.Info(typeof(Overseer), "Server startup complete.");
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(Overseer), $"Error occurred during startup: {ex.Message}");
                _stage = "Error";
            }
            finally
            {
                _isStarting = false;
            }
        }


        /// <summary>
        /// Kills the Gateway Server and relaunches it in-place.
        /// Returns false if startup/restart is already in progress.
        /// </summary>
        public static bool RestartGatewayServer()
        {
            lock (_startupShutdownLock)
            {
                if (_isStarting) return false;
            }

            try
            {
                Logger.Info(typeof(Overseer), "Restarting Gateway Server...");

                if (GatewayServer != null && !GatewayServer.HasExited)
                {
                    GatewayServer.Kill();
                    Logger.Info(typeof(Overseer), "Gateway Server killed.");
                }
                GatewayServer = null;

                Thread.Sleep(2000);

                GatewayServer = ProcessTools.LaunchProcessTracked(ss!.GatewayServerPath!, 3000, "Gateway Server");
                if (GatewayServer == null)
                {
                    Logger.Error(typeof(Overseer), "Gateway Server failed to restart!");
                    return false;
                }

                bool launched = TryLaunchAllNodesAfterRestart(ss!.SmcMainWindowTitle!);

                if (!launched)
                    Logger.Error(typeof(Overseer), "Could not automate SMC node launch after restart. Please launch manually.");
                else
                    Logger.Info(typeof(Overseer), "All nodes re-launched via SMC after restart.");

                Logger.Info(typeof(Overseer), "Gateway Server restarted successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(Overseer), $"Error restarting Gateway Server: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Kills Shard Manager and Game Server, relaunches them, waits 35s for full
        /// initialization, then re-runs the SMC node-launch automation.
        /// Returns false if startup/restart is already in progress.
        /// </summary>
        public static Task<bool> RestartShardAndGame()
        {
            lock (_startupShutdownLock)
            {
                if (_isStarting) return Task.FromResult(false);
                _isStarting = true;
                _stage = "Restarting Shard + Game";
            }
            _ = Task.Run(RunShardGameRestartSequence);
            return Task.FromResult(true);
        }

        private static async Task RunShardGameRestartSequence()
        {
            try
            {
                Logger.Info(typeof(Overseer), "Restarting Shard Manager and Game Server...");

                _stage = "Stopping Game Server";
                if (GameServer != null && !GameServer.HasExited)
                {
                    GameServer.Kill();
                    Logger.Info(typeof(Overseer), "Game Server killed.");
                }
                GameServer = null;

                _stage = "Stopping Shard Manager";
                if (ShardManager != null && !ShardManager.HasExited)
                {
                    ShardManager.Kill();
                    Logger.Info(typeof(Overseer), "Shard Manager killed.");
                }
                ShardManager = null;

                Thread.Sleep(3000);

                _stage = "Restarting Shard Manager";
                ShardManager = ProcessTools.LaunchProcessTracked(ss!.ShardManagerPath!, 8000, "Shard Manager");

                _stage = "Restarting Game Server";
                GameServer = ProcessTools.LaunchProcessTracked(ss!.GameServerPath!, 5000, "Game Server");

                // Game server can take 20-30s to fully load before SMC can activate modules
                _stage = "Waiting for Game Server Init";
                Logger.Info(typeof(Overseer), "Waiting 35 seconds for Game Server to fully initialize...");
                Thread.Sleep(35000);

                _stage = "Launching All Nodes";
                bool launched = TryLaunchAllNodesAfterRestart(ss!.SmcMainWindowTitle!);

                if (!launched)
                    Logger.Error(typeof(Overseer), "Could not automate SMC node launch after restart. Please launch manually.");
                else
                    Logger.Info(typeof(Overseer), "All nodes re-launched via SMC after restart.");

                _stage = "Running";
                Logger.Info(typeof(Overseer), "Shard + Game restart complete.");
            }
            catch (Exception ex)
            {
                Logger.Error(typeof(Overseer), $"Error during Shard+Game restart: {ex.Message}");
                _stage = "Error";
            }
            finally
            {
                _isStarting = false;
            }
        }

        /// <summary>
        /// Kills all tracked server processes in reverse startup order.
        /// Returns false if startup is currently in progress.
        /// </summary>
        public static bool ShutdownVsroServer()
        {
            lock (_startupShutdownLock)
            {
                if (_isStarting) return false;
            }

            var processes = new (Process? proc, string name)[]
            {
                (SMC,           "SMC"),
                (GameServer,    "Game Server"),
                (ShardManager,  "Shard Manager"),
                (AgentServer,   "Agent Server"),
                (GatewayServer, "Gateway Server"),
                (FarmManager,   "Farm Manager"),
                (MachineManager,"Machine Manager"),
                (DownloadServer,"Download Server"),
                (GlobalManager, "Global Manager"),
                (CertServer,    "Cert Server"),
            };

            foreach (var (proc, name) in processes)
            {
                try
                {
                    if (proc != null && !proc.HasExited)
                    {
                        proc.Kill();
                        Logger.Info(typeof(Overseer), $"Killed: {name}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(typeof(Overseer), $"Failed to kill {name}: {ex.Message}");
                }
            }

            GatewayProxy?.Stop(); GatewayProxy = null;
            DownloadProxy?.Stop(); DownloadProxy = null;
            AgentProxy?.Stop();   AgentProxy   = null;

            SMC = null; GameServer = null; ShardManager = null;
            AgentServer = null; GatewayServer = null; FarmManager = null;
            MachineManager = null; DownloadServer = null; GlobalManager = null;
            CertServer = null;
            _stage = string.Empty;

            return true;
        }

        #endregion

        #region - Status And Settings -

        public static VSROServerStatus GetServerStatus()
        {
            var moduleList = new (Process? proc, string name)[]
            {
                (CertServer,    "Cert Server"),
                (GlobalManager, "Global Manager"),
                (DownloadServer,"Download Server"),
                (MachineManager,"Machine Manager"),
                (FarmManager,   "Farm Manager"),
                (GatewayServer, "Gateway Server"),
                (AgentServer,   "Agent Server"),
                (ShardManager,  "Shard Manager"),
                (GameServer,    "Game Server"),
                (SMC,           "SMC"),
            };

            var statuses = new List<ModuleStatus>();
            int runningCount = 0;

            foreach (var (proc, name) in moduleList)
            {
                bool isRunning = proc != null && !proc.HasExited;
                if (isRunning) runningCount++;

                var ms = new ModuleStatus
                {
                    Name        = name,
                    IsRunning   = isRunning,
                    ProcessId   = isRunning ? proc!.Id : null,
                    StartTime   = isRunning ? proc!.StartTime : null,
                    IsResponsive = isRunning,
                };

                if (isRunning)
                {
                    try { ms.MemoryBytes = proc!.WorkingSet64; } catch { }
                }

                statuses.Add(ms);
            }

            // Insert integrated proxy status between Game Server and SMC
            bool proxyRunning = GatewayProxy != null;
            if (proxyRunning) runningCount++;
            statuses.Insert(9, new ModuleStatus
            {
                Name         = "Integrated Proxy",
                IsRunning    = proxyRunning,
                ProcessId    = proxyRunning ? Environment.ProcessId : null,
                IsResponsive = proxyRunning,
            });

            return new VSROServerStatus
            {
                IsRunning      = runningCount > 0,
                ModulesOpened  = runningCount,
                StartupStage   = _stage,
                ModuleStatuses = statuses,
            };
        }

        public static StartupSettings? GetSettings() => ss;

        public static async Task UpdateSettings(StartupSettings newSettings)
        {
            ss = newSettings;
            string configFilePath = Path.Combine(dir, "configs", "Config.json");
            await ss.Save(configFilePath);
        }

        public static bool IsProxyRunning => GatewayProxy != null;

        /// <summary>Starts the integrated proxy (Gateway, Download, Agent). No-op if already running.</summary>
        public static bool StartProxy()
        {
            if (GatewayProxy != null)
            {
                Logger.Warn(typeof(Overseer), "StartProxy called but proxy is already running.");
                return false;
            }
            SetupProxy();
            Logger.Info(typeof(Overseer), "Integrated proxy started.");
            return true;
        }

        /// <summary>Stops the integrated proxy. No-op if already stopped.</summary>
        public static bool StopProxy()
        {
            if (GatewayProxy == null)
            {
                Logger.Warn(typeof(Overseer), "StopProxy called but proxy is not running.");
                return false;
            }
            GatewayProxy?.Stop();  GatewayProxy  = null;
            DownloadProxy?.Stop(); DownloadProxy = null;
            AgentProxy?.Stop();    AgentProxy    = null;
            Logger.Info(typeof(Overseer), "Integrated proxy stopped.");
            return true;
        }

        /// <summary>Stops then restarts the integrated proxy.</summary>
        public static void RestartProxy()
        {
            StopProxy();
            SetupProxy();
            Logger.Info(typeof(Overseer), "Integrated proxy restarted.");
        }

        #endregion

        #region - Proxy -

        public static void SetupProxy()
        {
            GatewayProxy = new Server();
            GatewayProxy.SetProxy(Settings.Proxy!.IntegratedProxyIP!, Settings.Proxy!.GatewayServerPort);
            GatewayProxy.Start(15779);
            RegisterGWSHandlers();

            
            DownloadProxy = new Server();
            DownloadProxy.SetProxy(Settings.Proxy!.IntegratedProxyIP!, Settings.Proxy!.DownloadServerPort);
            DownloadProxy.Start(15881);
            RegisterDLSHandlers();

            AgentProxy = new Server();
            AgentProxy.SetProxy(Settings.Proxy!.IntegratedProxyIP!, Settings.Proxy!.AgentServerPort);
            AgentProxy.Start(15884);
            RegisterAGSHandlers();

            DllBridge.Instance.RegisterHandler("auth", async (_, element) => {
                try
                {
                    var user = element.GetProperty("user").GetString();
                    // store writer
                    Logger.Debug("DllAuth", $"Sending ack for user {user}");
                    DllBridge.Instance.SendToDll(user!, "loginAck", new { });
                }
                catch (Exception ex)
                {
                    Logger.Error("DllAuth", ex.Message);
                }
            });

            

            Logger.Info(typeof(Overseer), "FoxProxy started on ports 15779, 15881, 15884");
        }
        public static async Task BuildShopDB()
        {
            var query = @"
                SELECT 
                    oc.ID AS RefObjID,
                    st.ID AS TabID,
                    goods.SlotIndex,
                    scrap.RefItemCodeName,
                    item.ID AS RefItemID
                FROM _refShopGroup sg
                JOIN _refShopTabGroup stg ON 
                    stg.CodeName128 LIKE REPLACE(sg.CodeName128, 'GROUP_', '') + '_GROUP%'
                    OR stg.CodeName128 LIKE REPLACE(REPLACE(REPLACE(REPLACE(
                        sg.CodeName128, 'GROUP1_', ''), 'GROUP2_', ''), 'GROUP3_', ''), 'GROUP4_', '') 
                        + '_GROUP%'
                JOIN _refShopTab st ON st.RefTabGroupCodeName = stg.CodeName128
                JOIN _refShopGoods goods ON goods.RefTabCodeName = st.CodeName128
                JOIN _refScrapOfPackageItem scrap ON scrap.RefPackageItemCodeName = goods.RefPackageItemCodeName
                JOIN _refObjCommon oc ON oc.CodeName128 = sg.RefNPCCodeName
                JOIN _refObjCommon item ON item.CodeName128 = scrap.RefItemCodeName
                WHERE sg.RefNPCCodeName != 'xxx'
                ORDER BY oc.ID, st.ID, goods.SlotIndex";

            using var conn = DBConnect.OpenConnection("SRO_VT_SHARD");
            await conn.OpenAsync();
            using var cmd = new SqlCommand(query, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var rawRows = new List<(int RefObjID, int TabID, int SlotIndex, int RefItemID, string CodeName)>();
            while (await reader.ReadAsync())
            {
                rawRows.Add((
                    Convert.ToInt32(reader["RefObjID"]),
                    Convert.ToInt32(reader["TabID"]),
                    Convert.ToInt32(reader["SlotIndex"]),
                    Convert.ToInt32(reader["RefItemID"]),
                    reader["RefItemCodeName"].ToString()!
                ));
            }

            foreach (var npc in rawRows.GroupBy(r => r.RefObjID))
            {
                var tabIds = npc.Select(r => r.TabID).Distinct().OrderBy(x => x).ToList();
                foreach (var row in npc)
                {
                    int tabIndex = tabIds.IndexOf(row.TabID);
                    var key = (row.RefObjID, tabIndex, row.SlotIndex);
                    if (!ShopLookup.ContainsKey(key))
                        ShopLookup[key] = (row.RefItemID, row.CodeName);
                }
            }

            ShopNPCIds = ShopLookup.Keys.Select(k => k.NpcObjID).ToHashSet();
            Logger.Info("ShopDB", $"Loaded {ShopLookup.Count} shop entries for {ShopNPCIds.Count} NPCs");
        }
        private static void RegisterGWSHandlers()
        {
            if (GatewayProxy == null)
                return;

            GatewayProxy.OnProxyConnected += (_s, _e) => {
                Logger.Info(typeof(Overseer), "Gateway Server: Connection established (" + _e.Proxy.Server.Socket.LocalEndPoint + ")");
            };
            GatewayProxy.OnProxyDisconnected += (_s, _e) => {
                Logger.Info(typeof(Overseer), "Gateway Server: Connection finished (" + _e.Proxy.Server.Socket.LocalEndPoint + ")");
            };

            // Rewrite correct local or public ip to download server (this opcode is not documented, i couldnt find anything.)
            GatewayProxy.RegisterServerPacketHandler(0xA100, (_s, _e) =>
            {
                var localIPAddresses = GetMyLocalAddresses();
                var packet = _e.Packet;
                // Patch error
                if (packet.ReadByte() == 2)
                {
                    // Downloading patch required
                    if (packet.ReadByte() == 2)
                    {
                        var downloadServerIP = packet.ReadAscii();
                        var downloadServerPort = packet.ReadUShort();

                        var clientIP = ((IPEndPoint)_e.Proxy.Client.Socket.RemoteEndPoint).Address.ToString();
                        // Check if client connection is from an external address to redirect them properly
                        var isLocal = localIPAddresses.Find(x => x == clientIP);
                        if (isLocal != null)
                            clientIP = "localhost";
                        else
                            downloadServerIP = _publicIP;

                        // Add this connection to the agent server queue control
                        if (!mDownloadServerQueue.TryGetValue(clientIP, out List<object[]> connections))
                        {
                            connections = new List<object[]>();
                            mDownloadServerQueue[clientIP] = connections;
                        }
                        connections.Add(new object[]
                        {
                            Settings.Proxy!.IntegratedProxyIP!, // Server IP
                            (ushort)Settings.Proxy!.DownloadServerPort, // Server Port
                            Stopwatch.StartNew(), // Time register to discard connection after some time
                        });

                        Console.WriteLine("Gateway Server: Redirecting connection from (" + _e.Proxy.Client.Socket.LocalEndPoint + ") to local Download Server");
                        // Redirect client to the local download server
                        var p = new Packet(0xA100, packet.Encrypted, packet.Massive);
                        p.WriteByte(2);
                        p.WriteByte(2);
                        p.WriteAscii(downloadServerIP!);
                        p.WriteUShort(15881);
                        p.WriteByteArray(packet.ReadByteArray(packet.RemainingRead())); // Copy data left
                        _e.Proxy.Client.Send(p);

                        // Avoid send this packet already handled to client
                        _e.CancelTransfer = true;

                    }
                }

            });

            // Rewrite correct local or public ip to agent server (this fixes C9)
            GatewayProxy.RegisterServerPacketHandler(Constant.LOGIN_SERVER_AUTH_INFO, (_s, _e) =>
            {
                var localIPAddresses = GetMyLocalAddresses();
                var packet = _e.Packet;
                // Check success
                if (packet.ReadByte() == 1)
                {
                    var queueId = packet.ReadUInt();
                    var agentServerIP = packet.ReadAscii();
                    var agentServerPort = packet.ReadUShort();

                    var clientIP = ((IPEndPoint)_e.Proxy.Client.Socket.RemoteEndPoint!).Address.ToString();
                    // Check if client connection is from an external address to redirect them properly
                    var isLocal = localIPAddresses.Find(x => x == clientIP);
                    if (isLocal != null)
                        clientIP = "localhost";
                    else
                        agentServerIP = _publicIP;

                    // Add this connection to the agent server queue control
                    if (!mAgentServerQueue.TryGetValue(clientIP, out List<object[]> connections))
                    {
                        connections = new List<object[]>();
                        mAgentServerQueue[clientIP] = connections;
                    }

                    connections.Add(new object[]
                    {
                        Settings.Proxy!.IntegratedProxyIP!, // Server IP
                        (ushort)Settings.Proxy!.AgentServerPort, // Server Port
                        Stopwatch.StartNew(), // Time register to discard connection after some time
                    });

                    Logger.Info(typeof(Overseer), "Gateway Server: Redirecting connection from (" + _e.Proxy.Client.Socket.LocalEndPoint + $") to local Agent Server: {agentServerIP}:{15884}");
                    // Redirect client to the local agent server
                    var p = new Packet(0xA102, packet.Encrypted, packet.Massive);
                    p.WriteByte(1); // success
                    p.WriteUInt(queueId);
                    p.WriteAscii(agentServerIP!);
                    p.WriteUShort(15884);
                    _e.Proxy.Client.Send(p);

                    // Avoid send this packet already handled to client
                    _e.CancelTransfer = true;
                }
            });

            Logger.Info(typeof(Overseer), $"Registered gateway server handlers.");
        }
        private static void RegisterDLSHandlers()
        {
            if (DownloadProxy == null)
                return;

            DownloadProxy.OnProxyConnected += (_s, _e) => {
                Logger.Info(typeof(Overseer), "Download Server: Connection established (" + _e.Proxy.Server.Socket.LocalEndPoint + ")");
            };
            DownloadProxy.OnProxyDisconnected += (_s, _e) => {
                Logger.Info(typeof(Overseer), "Download Server: Connection finished (" + _e.Proxy.Server.Socket.LocalEndPoint + ")");
            };
            DownloadProxy.OnProxyConnection += (_s, _e) =>
            {
                var localIPAddresses = GetMyLocalAddresses();
                var clientIP = ((IPEndPoint)_e.Proxy.Client.Socket.RemoteEndPoint).Address.ToString();
                var isLocal = localIPAddresses.Find(x => x == clientIP);
                if (isLocal != null)
                    clientIP = "localhost";

                // Check connections controller
                if (mDownloadServerQueue.TryGetValue(clientIP, out List<object[]> connections))
                {
                    // Check all connections from this IP and remove the old ones
                    object[] clientQueue = null;
                    for (int i = 0; i < connections.Count; i++)
                    {
                        Stopwatch connectionTime = (Stopwatch)connections[i][2];
                        // Remove connections longer than one minute
                        if (connectionTime.Elapsed > mDownloadServerQueueTimeLimit)
                        {
                            connections.RemoveAt(i--);
                            continue;
                        }
                        else
                        {
                            clientQueue = connections[i];
                            connections.RemoveAt(i);
                            break;
                        }
                    }

                    // Set proxy connection
                    if (clientQueue == null)
                    {
                        // Shutdown connection if cannot be found
                        _e.Proxy.Server.Close();
                    }
                    else
                    {
                        // Redirect connection
                        _e.IP = (string)clientQueue[0];
                        _e.Port = (ushort)clientQueue[1];
                    }
                }
            };

            Logger.Info(typeof(Overseer), $"Registered download server handlers.");
        }
        private static void RegisterAGSHandlers()
        {
            if (AgentProxy == null)
                return;

            // Default
            AgentProxy.OnProxyConnected += (_s, _e) => {
                Logger.Info(typeof(Overseer), "Agent Server: Connection established (" + _e.Proxy.Server.Socket.LocalEndPoint + ")");
            };
            AgentProxy.OnProxyDisconnected += (_s, _e) =>
            {
                var proxy = _e.Proxy;
                var session = proxy.Session;
                var endpoint = proxy.Server?.Socket?.LocalEndPoint?.ToString(); // snapshot safely

                _ = Task.Run(async () =>
                {
                    try
                    {
                        Logger.Info(typeof(Overseer),
                            $"Agent Server: Connection finished ({endpoint})");

                        if (proxy.SessionTokenSource != null)
                        {
                            proxy.SessionTokenSource.Cancel();
                            proxy.SessionTokenSource.Dispose();
                            proxy.SessionTokenSource = null;
                        }

                        if (session != null && !string.IsNullOrEmpty(session.CharacterName))
                        {
                            await DBConnect.AddPlayTimeAsync(session.CharacterName, session.AccumulatedPlayTime);

                            Logger.Info(typeof(Overseer),
                                $"Saved playtime for {session.CharacterName}: {session.AccumulatedPlayTime}");

                            CharacterSnapshotStore.Save(session, proxy.Inventory);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(typeof(Overseer),
                            $"Disconnect handler failed: {ex}");
                    }
                });
            };
            AgentProxy.OnProxyConnection += (_s, _e) =>
            {
                var clientIP = ((IPEndPoint)_e.Proxy.Client.Socket.RemoteEndPoint).Address.ToString();
                var localIPAddresses = GetMyLocalAddresses();
                var isLocal = localIPAddresses.Find(x => x == clientIP);


                // Check connections controller
                if (mAgentServerQueue.TryGetValue(clientIP, out List<object[]> connections))
                {
                    // Check all connections from this IP and remove the old ones
                    object[] clientQueue = null;
                    for (int i = 0; i < connections.Count; i++)
                    {
                        Stopwatch connectionTime = (Stopwatch)connections[i][2];
                        // Remove connections longer than one minute
                        if (connectionTime.Elapsed > mAgentServerQueueTimeLimit)
                        {
                            connections.RemoveAt(i--);
                            continue;
                        }
                        else
                        {
                            clientQueue = connections[i];
                            connections.RemoveAt(i);
                            break;
                        }
                    }

                    // Set proxy connection
                    if (clientQueue == null)
                    {
                        // Shutdown connection if cannot be found
                        _e.Proxy.Server.Close();
                    }
                    else
                    {
                        // Redirect connection
                        _e.IP = (string)clientQueue[0];
                        _e.Port = (ushort)clientQueue[1];
                    }
                }
            };
            
            // Login
            AgentTools.RegisterPlayerLoginHandler(AgentProxy);
            AgentTools.RegisterPartyHandlers(AgentProxy);
            AgentTools.RegisterSpawnTrackerImproved(AgentProxy);
            AgentTools.RegisterTargetTracker(AgentProxy);
            AgentTools.RegisterCharacterUIDTracker(AgentProxy);

            // Character Inventories
            PlayerTools.RegisterChatCommandHandler(AgentProxy);
            PlayerTools.RegisterItemMoveHandler(AgentProxy);
            PlayerTools.RegisterCosDespawnHandler(AgentProxy);
            PlayerTools.RegisterChardataHandler(AgentProxy);
            PlayerTools.RegisterItemUseHandler(AgentProxy);
            PlayerTools.RegisterCosSpawnHandler(AgentProxy);
            PlayerTools.RegisterGoldUpdateHandler(AgentProxy);
            PlayerTools.RegisterPlayerHPMPHandler(AgentProxy);
            PlayerTools.RegisterStorageHandler(AgentProxy);
            PlayerTools.RegisterPlayerKillHandler(AgentProxy);
            PlayerTools.RegisterSTRINTUpdateHandler(AgentProxy);
            PlayerTools.RegisterClientMovementHandler(AgentProxy);
            PlayerTools.RegisterClientSortHandler(AgentProxy);
            // Activity Time
            ushort[] activityOpcodes =
            {
                Constant.CLIENT_MOVEMENT,
                Constant.CLIENT_CHAT,
                Constant.CLIENT_PLAYER_ACTION,
                Constant.CLIENT_TARGET,
                Constant.CLIENT_INT_UPDATE,
                Constant.CLIENT_STR_UPDATE,
                Constant.CLIENT_OPEN_SHOP,
                Constant.CLIENT_CLOSE_SHOP,
                Constant.CLIENT_ITEM_MOVE,
                Constant.CLIENT_ITEM_USE
            };

            foreach (var opcode in activityOpcodes)
            {
                AgentProxy.RegisterClientPacketHandler(opcode, (sender, e) =>
                {
                    PlayerTools.MarkActivity(e.Proxy);
                });
            }

            Logger.Info(typeof(Overseer), $"Registered agent server handlers.");
            StartAgentMonitor();
            Logger.Info(typeof(Overseer), $"Started auto services");
        }

        #endregion

        #region - Tasks -

        public static void StartAgentMonitor()
        {
            _monitorCts = new CancellationTokenSource();
            var token = _monitorCts.Token;

            _monitorTask = Task.Run(async () =>
            {
                var tasks = new[]
                {
                    LongestPlayerLoop(token),
                    GlobalNoticeLoop(token)
                };

                await Task.WhenAll(tasks);
            });
        }
        public static void StopAgentMonitorLoop()
        {
            try
            {
                _monitorCts?.Cancel();
                _monitorTask?.Wait(2000);
            }
            catch { }

            _monitorTask = null;
            _monitorCts = null;
        }
        private static async Task GlobalNoticeLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                int delay = SettingsLoader.Settings?.Proxy?.AutoNoticeInterval ?? 0;
                if (delay <= 0)
                    return;
                
                if (AgentProxy == null) return;
                await Task.Delay(TimeSpan.FromMinutes(delay), token);

                string msg = SettingsLoader.Settings?.Proxy?.AutoNoticeMessage ?? string.Empty;
                SendNotice(msg);
            }
        }
        private static async Task LongestPlayerLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                int delay = SettingsLoader.Settings?.Proxy?.AutoLongestPlayerOnlineInterval ?? 0;
                if (delay <= 0)
                    return;

                await Task.Delay(TimeSpan.FromMinutes(delay), token);


                if (AgentProxy == null) return;
                var snapshot = AgentProxy.Connections.Values
                    .Where(p => p.Session != null)
                    .ToList();

                if (snapshot.Count == 0)
                    continue;

                var longest = snapshot
                    .OrderByDescending(p => p.Session?.AccumulatedPlayTime)
                    .FirstOrDefault();

                if (longest?.Session != null)
                {
                    foreach (var conn in AgentProxy.Connections.Values)
                    {
                        string template = SettingsLoader.Settings?.Proxy?.LongestPlayerOnlineMessage ?? string.Empty;
                        
                        string msg = SettingsLoader.FormatPlayerMessage(template, longest);
                        PlayerTools.SendToProxyChat(conn, PlayerTools.ChatType.Notice, null, msg);
                    }
                }
            }
        }
        
        #endregion

        #region - Helpers -

        /// <summary>
        /// Assumes SMC is already running and logged in. Navigates to ServerControl
        /// tab, right-clicks the node area, and starts all services.
        /// </summary>
        static bool TryLaunchAllNodesAfterRestart(string smcWindowTitle)
        {
            Logger.Info(typeof(Overseer), "Locating SMC main window for node re-launch...");
            IntPtr hwnd = WaitForWindow(smcWindowTitle, timeoutMs: 15000);
            if (hwnd == IntPtr.Zero)
            {
                Logger.Error(typeof(Overseer), $"SMC main window '{smcWindowTitle}' not found.");
                return false;
            }

            ForceWindowToFront(hwnd);
            Thread.Sleep(800);
            GetWindowRect(hwnd, out RECT rect);

            // ── Click "ServerControl" tab ─────────────────────────────────────
            ClickAt(rect.Left + 375, rect.Top + 58);
            Thread.Sleep(2000);

            // ── Right-click in the node area ──────────────────────────────────
            RightClickAt(rect.Left + 640, rect.Top + 200);
            Thread.Sleep(500);

            // ── Click "Start All Service" (4th item) ──────────────────────────
            ClickAt(rect.Left + 900, rect.Top + 200 + 99);
            Thread.Sleep(600);

            // ── Handle "warnning" popup ───────────────────────────────────────
            Logger.Info(typeof(Overseer), "Waiting for 'warnning' confirmation popup...");
            IntPtr warnHwnd = WaitForWindow("warnning", timeoutMs: 8000);
            if (warnHwnd == IntPtr.Zero)
            {
                Logger.Error(typeof(Overseer), "'warnning' popup did not appear.");
                return false;
            }

            FocusWindow(warnHwnd);
            Thread.Sleep(400);
            PostKey(warnHwnd, VirtualKey.RETURN);
            Logger.Info(typeof(Overseer), "Confirmed. Nodes re-launching.");

            return true;
        }

        // ── SMC Node Launch Automation ────────────────────────────────────────────

        static bool TryLaunchAllNodes(string smcWindowTitle)
        {
            Logger.Info(typeof(Overseer), "Waiting for login dialog to close...");
            WaitForWindowToClose("Login", timeoutMs: 15000);
            Thread.Sleep(1000);

            Logger.Info(typeof(Overseer), "Waiting for SMC main window...");
            IntPtr hwnd = WaitForWindow(smcWindowTitle, timeoutMs: 20000);
            if (hwnd == IntPtr.Zero)
            {
                Logger.Error(typeof(Overseer), $"SMC main window '{smcWindowTitle}' not found.");
                return false;
            }

            ForceWindowToFront(hwnd);
            Thread.Sleep(800);
            GetWindowRect(hwnd, out RECT rect);

            // Window is full/near-full screen on 1024x768
            // All offsets are relative to window top-left

            // ── Step 1: Click "Application" menu ─────────────────────────────────
            // From screenshot: "Application" text starts at roughly x=48, menu bar y≈11
            ClickAt(rect.Left + 48, rect.Top + 11);
            Thread.Sleep(600); // Slightly longer — give dropdown time to fully render

            // ── Step 2: Click "LoadPlugins" dropdown item ─────────────────────────
            // From screenshot: dropdown appears directly below, "LoadPlugins" is first item ~y=39
            ClickAt(rect.Left + 48, rect.Top + 58);
            Logger.Info(typeof(Overseer), "Clicked LoadPlugins. Waiting 8 seconds for plugins to load...");
            Thread.Sleep(20000); // Give it extra headroom

            // Re-assert focus after the long wait — another window may have taken it
            ForceWindowToFront(hwnd);
            Thread.Sleep(500);

            // ── Step 3: Click "ServerControl" tab ────────────────────────────────
            // From screenshot: tab bar is at y≈36 after plugins load
            // "ServerControl" is the 6th tab. Tabs start ~x=5, each roughly 105px wide
            // CAS|ConcurrentUserLog|ModulePatch|Notice|Security|ServerControl
            // 0    1                2           3      4        5
            // Approximate x center of ServerControl tab ≈ 440
            ClickAt(rect.Left + 375, rect.Top + 58);
            Thread.Sleep(5000);

            // ── Step 4: Right-click in the node area ─────────────────────────────
            // From screenshot: node boxes (Common/Shard/ServerMachine) appear in upper area
            // Right click in open space below/beside them, not ON a box
            // Safe area: center of window, avoiding the boxes ~x=640, y=200
            RightClickAt(rect.Left + 640, rect.Top + 200);
            Thread.Sleep(500);

            // ── Step 5: Click "Start All Service" (4th item) ─────────────────────
            // Menu rendered at ~x=848 center, items ~37px tall, Start All Service = 4th item
            // Right-click was at (640, 200), menu appeared to the right
            // Click at fixed offset from where menu appeared
            ClickAt(rect.Left + 900, rect.Top + 200 + 99);
            Thread.Sleep(600);


            // ── Step 6: Handle "warnning" popup ──────────────────────────────────
            Logger.Info(typeof(Overseer), "Waiting for 'warnning' confirmation popup...");
            IntPtr warnHwnd = WaitForWindow("warnning", timeoutMs: 8000);
            if (warnHwnd == IntPtr.Zero)
            {
                Logger.Error(typeof(Overseer), "'warnning' popup did not appear.");
                return false;
            }

            FocusWindow(warnHwnd);
            Thread.Sleep(400);
            PostKey(warnHwnd, VirtualKey.RETURN);
            Logger.Info(typeof(Overseer), "Confirmed. Nodes launching.");

            // Right click again
            RightClickAt(rect.Left + 640, rect.Top + 200);
            Thread.Sleep(500);

            // ── Step 7: Click "Deactivate Alarm" (4th item) ─────────────────────
            // Menu rendered at ~x=848 center, items ~37px tall, Start All Service = 4th item
            // Right-click was at (640, 200), menu appeared to the right
            // Click at fixed offset from where menu appeared
            ClickAt(rect.Left + 900, rect.Top + 200 + 79);
            Thread.Sleep(600);

            return true;
        }

        
        // ── Window Helpers ────────────────────────────────────────────────────────

        static IntPtr WaitForWindow(string title, int timeoutMs = 10000, int pollMs = 500)
        {
            int elapsed = 0;
            while (elapsed < timeoutMs)
            {
                var hwnd = FindWindow(null!, title);
                if (hwnd != IntPtr.Zero) return hwnd;
                Thread.Sleep(pollMs);
                elapsed += pollMs;
            }
            return IntPtr.Zero;
        }

        static void FocusWindow(IntPtr hwnd)
        {
            ShowWindow(hwnd, ShowWindowCommands.Show);
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
        }

        static void WaitForWindowToClose(string title, int timeoutMs = 10000, int pollMs = 300)
        {
            int elapsed = 0;
            while (elapsed < timeoutMs)
            {
                var hwnd = FindWindow(null!, title);
                if (hwnd == IntPtr.Zero) return; // Gone
                Thread.Sleep(pollMs);
                elapsed += pollMs;
            }
            Logger.Warn(typeof(Overseer), $"Window '{title}' did not close within timeout.");
        }

        static void ClickAt(int x, int y)
        {
            SetCursorPos(x, y);
            Thread.Sleep(150);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            Thread.Sleep(80);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
            Thread.Sleep(150);
        }

        static void RightClickAt(int x, int y)
        {
            SetCursorPos(x, y);
            Thread.Sleep(150);
            mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
            Thread.Sleep(80);
            mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
            Thread.Sleep(150);
        }

        public static List<string> GetMyLocalAddresses()
        {
            List<string> myLocalIps = new List<string>();
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    myLocalIps.Add(ip.ToString());
            //return ip;
            return myLocalIps;
        }

        public static int SendNotice(string message)
        {
            if (AgentProxy == null)
            {
                Logger.Error(typeof(Overseer), $"AgentProxy was null!");
                return -1;
            }

            var notice = new Packet(Constant.SERVER_CHAT);
            notice.WriteByte(7);
            notice.WriteAscii(message);

            int sent = 0;
            foreach (var connection in AgentProxy.Connections)
            {
                try
                {
                    connection.Value.Client.Send(notice);
                    sent++;
                }
                catch (Exception ex)
                {
                    Logger.Warn(typeof(Overseer), $"Failed to send notice to client: {ex.Message}");
                }
            }

            Logger.Info(typeof(Overseer), $"Notice sent to {sent} client(s): {message}");
            return sent;
        }

        #endregion


    }
}
