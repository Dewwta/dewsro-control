using CoreLib.Tools.Logging;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Framework;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network;
using VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.Tools;

namespace VSRO_CONTROL_API.VSRO.Bots
{
    public enum BotMode
    {
        Idle,
        Teleporting,
        WalkingToTrainplace,
        Training,
        ReturningToTown,
        InTown,
        Dead
    }

    public sealed class BotBrain
    {
        #region Fields

        public static bool Debug { get; private set; } = false;
        public static void SetDebug(bool debug) => Debug = debug;
        private static void Log(string handler, string message) { if (Debug == true) Logger.Trace($"BotBrain:{handler}", message); }


        private readonly VSRO_CONTROL_API.VSRO.DTO.ISession _session;
        private readonly CancellationToken _rootCt;
        private readonly AutoWalker _walker;
        private readonly AttackLoop _attackLoop;
        private readonly TeleportGraph _teleportGraph = new();
        private readonly TownLoop _townLoop;
        private readonly ResourceMonitor _resourceMonitor;
        private readonly Action<Packet> _sendPacket;
        private DateTime _townExitLockUntil = DateTime.MinValue;
        private readonly Proxy _proxy;
        private BotMode _mode = BotMode.Idle;
        public BotMode Mode => _mode;

        private Task? _currentTask;
        private CancellationTokenSource? _currentActionCts;

        private bool _taskCompletedThisTick = false;
        private bool _townSequenceCompleted = false;

        #endregion

        #region Constructor

        public BotBrain(
            VSRO_CONTROL_API.VSRO.DTO.ISession session,
            AutoWalker walker,
            AttackLoop attackLoop,
            Action<Packet> sendPacket,
            CancellationToken rootCt,
            Proxy proxy)
        {
            _session = session;
            _walker = walker;
            _attackLoop = attackLoop;
            _proxy = proxy;
            _sendPacket = sendPacket;
            _rootCt = rootCt;

            _resourceMonitor = new ResourceMonitor(session, () => _session._botSettings);

            _townLoop = new TownLoop(
                proxy,
                walker,
                sendPacket,
                () => _session._botSettings);
        }

        #endregion

        #region Main Loop

        public async Task RunAsync()
        {
            Log("BotBrain", "Authoritative loop started");

            _ = Task.Run(() => _resourceMonitor.RunAsync(_rootCt), _rootCt);

            while (!_rootCt.IsCancellationRequested)
            {
                try
                {
                    await TickAsync();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Logger.Error("BotBrain", $"Tick exception: {ex}");
                }

                await Task.Delay(250, _rootCt);
            }

            await CleanupCurrentActionAsync();
            Log("BotBrain", "Stopped");
        }

        private async Task TickAsync()
        {
            _taskCompletedThisTick = _currentTask != null && _currentTask.IsCompleted;

            BotMode desired = EvaluateDesiredState();

            if (_mode != desired)
                await TransitionToAsync(desired);

            EnsureStateEngineRunning();
            SendStateUpdateToUi();
        }

        #endregion

        #region State Evaluation

        private BotMode EvaluateDesiredState()
        {
            if (!_session.TrainingDestination.HasValue)
                return BotMode.Idle;

            if (_mode == BotMode.InTown && !_taskCompletedThisTick)
                return BotMode.InTown;

            if (_mode == BotMode.Teleporting && !_taskCompletedThisTick)
                return BotMode.Teleporting;

            var dest = _session.TrainingDestination.Value;
            var pos = (_mode == BotMode.WalkingToTrainplace && _taskCompletedThisTick)
                ? (_session.LastConfirmedBotPosition ?? _session.GetEstimatedPosition())
                : _session.GetEstimatedPosition();

            if (IsInTown(pos) && !_townSequenceCompleted && DateTime.UtcNow >= _townExitLockUntil)
            {
                if (_mode == BotMode.Idle || (_mode == BotMode.ReturningToTown && _taskCompletedThisTick))
                    return BotMode.InTown;
            }

            if (_resourceMonitor.NeedsReturn)
            {
                if (_mode == BotMode.Training)
                {
                    _attackLoop.StopAfterCurrentKill = true;

                    if (_taskCompletedThisTick)
                        return BotMode.ReturningToTown;

                    return BotMode.Training;
                }

                return BotMode.ReturningToTown;
            }

            if (NeedsTeleport(pos, dest))
                return BotMode.Teleporting;

            if (_mode == BotMode.Training)
            {
                float driftRadius = _session._lastBotR + 15f;
                if (Distance(pos, dest) > driftRadius)
                {
                    Logger.Warn("BotBrain", $"Drifted from center ({Distance(pos, dest):F1} > {driftRadius:F1}) -- returning");
                    return BotMode.WalkingToTrainplace;
                }
            }
            else
            {
                if (Distance(pos, dest) > 5f)
                    return BotMode.WalkingToTrainplace;
            }

            return BotMode.Training;
        }

        private async Task TransitionToAsync(BotMode newMode)
        {
            Log("BotBrain", $"Transition: {_mode} -> {newMode}");
            await CleanupCurrentActionAsync();
            _mode = newMode;
            _session._botState = newMode;
        }

        private void EnsureStateEngineRunning()
        {
            if (_currentTask != null && !_currentTask.IsCompleted)
                return;

            if (_taskCompletedThisTick)
                return;

            _currentActionCts = CancellationTokenSource.CreateLinkedTokenSource(_rootCt);
            var ct = _currentActionCts.Token;

            switch (_mode)
            {
                case BotMode.Teleporting:
                    _currentTask = Task.Run(() => ExecuteTeleportSequenceAsync(ct), ct);
                    break;

                case BotMode.WalkingToTrainplace:
                    if (_session.TrainingDestination.HasValue)
                        _currentTask = Task.Run(
                            () => _walker.WalkTo(_session.TrainingDestination.Value, ct, _session), ct);
                    break;

                case BotMode.Training:
                    if (_townSequenceCompleted)
                    {
                        Log("BotBrain", "Arrived at training center safely. Resetting town completion flag.");
                        _townSequenceCompleted = false;
                    }

                    _attackLoop.StopAfterCurrentKill = false;
                    _attackLoop.ResetRuntime();
                    _attackLoop.SetTrainingCenter(_session.TrainingDestination!.Value);
                    _currentTask = Task.Run(() => _attackLoop.Run(ct, _session._lastBotR), ct);
                    break;

                case BotMode.ReturningToTown:
                    _currentTask = Task.Run(() => ExecuteReturnToTownAsync(ct), ct);
                    break;

                case BotMode.InTown:
                    _currentTask = Task.Run(() => ExecuteTownLoopAsync(ct), ct);
                    break;

                case BotMode.Idle:
                default:
                    break;
            }
        }

        #endregion

        #region Teleport Sequences

        private async Task ExecuteTeleportSequenceAsync(CancellationToken ct)
        {

            var dest = _session.TrainingDestination!.Value;
            var currentPos = _session.GetEstimatedPosition();
            var currentRegion = ResolveDungeonAwareRegion(currentPos);
            var targetRegion = ResolveDungeonAwareRegion(dest);

            
            var route = _teleportGraph.FindShortestRoute(
                currentRegion, targetRegion, currentPos, dest,
                _session.CharacterRace,
                (int)(_session.PlayerStats?.CurrentLevel ?? 0));

            
            if (route == null)
            {
                Logger.Error("BotBrain", $"No teleport route from {currentRegion} to {targetRegion}");
                return;
            }

            Log("BotBrain", $"Teleport sequence: from={currentRegion} to={targetRegion}");
            Log("BotBrain", $"Route: {string.Join(" -> ", route.Select(h => h.ToRegion))}");

            foreach (var hop in route)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    await _walker.WalkTo(hop.GateApproachPosition, ct, _session, disableStuckOnFinalLeg: hop.IsPad);
                    _walker.ClearStuckState();
                    ct.ThrowIfCancellationRequested();

                    if (!hop.IsPad)
                    {
                        uint gateUID = 0;
                        var deadline = DateTime.UtcNow.AddSeconds(5);
                        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
                        {
                            gateUID = _session.SpawnedObjects
                                .FirstOrDefault(kvp => kvp.Value.RefObjID == hop.GateRefObjId).Key;
                            if (gateUID != 0) break;
                            await Task.Delay(200, ct);
                        }

                        if (gateUID == 0)
                        {
                            Logger.Error("BotBrain", $"Gate UID not found for refObjId={hop.GateRefObjId}");
                            return;
                        }

                        await Task.Delay(3000, ct);

                        var select = new Packet(0x7045);
                        select.WriteUInt(gateUID);
                        _sendPacket(select);

                        await Task.Delay(1000, ct);

                        var teleport = new Packet(0x705A);
                        teleport.WriteUInt(gateUID);
                        teleport.WriteByte(0x02);
                        teleport.WriteUInt(hop.TargetTeleportId);
                        _sendPacket(teleport);

                        Log("BotBrain", $"Teleport sent -- {hop.FromRegion} -> {hop.ToRegion}");
                    }

                    if (hop.IsPad)
                    {
                        var padDeadline = DateTime.UtcNow.AddSeconds(10);
                        while (DateTime.UtcNow < padDeadline && !ct.IsCancellationRequested)
                        {
                            await Task.Delay(1000, ct);
                            string current = ResolveDungeonAwareRegion(_session.GetEstimatedPosition());
                            if (current == hop.ToRegion)
                                break;
                        }
                        string padRegion = ResolveDungeonAwareRegion(_session.GetEstimatedPosition());
                        Log("BotBrain", $"Pad hop landed in: {padRegion}");
                    }
                    else
                    {
                        var regionDeadline = DateTime.UtcNow.AddSeconds(15);
                        string stableRegion = string.Empty;
                        int stableCount = 0;
                        while (DateTime.UtcNow < regionDeadline && !ct.IsCancellationRequested)
                        {
                            await Task.Delay(1000, ct);
                            string resolved = RegionResolver.Resolve(
                                (short)_session.RegionId, _session.SectorX, _session.SectorY);
                            if (resolved == stableRegion)
                            {
                                stableCount++;
                                if (stableCount >= 3) break;
                            }
                            else
                            {
                                stableRegion = resolved;
                                stableCount = 0;
                            }
                        }
                        Log("BotBrain", $"Stable region: {stableRegion}");
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Logger.Error("BotBrain", $"Hop failed: {ex.Message}");
                    return;
                }
                finally
                {
                    await Task.Delay(2000);
                }
            }
        }

        private async Task ExecuteTownTeleportAsync(string currentRegion, CancellationToken ct)
        {
            var pos = _session.GetEstimatedPosition();
            string? targetRegion = GetTownRegionFor(currentRegion);
            if (targetRegion == null)
            {
                Logger.Error("BotBrain", $"No town region mapping for {currentRegion}");
                return;
            }

            var townCenter = _townLoop.GetTownCenterForRegion(targetRegion);
            if (townCenter == null)
            {
                Logger.Error("BotBrain", $"No town center for region {targetRegion}");
                return;
            }
            var dest = townCenter.Value;
            var route = _teleportGraph.FindShortestRoute(currentRegion, targetRegion, pos, townCenter.Value, _session.CharacterRace, (int)(_session.PlayerStats?.CurrentLevel ?? 0));
            if (route == null)
            {
                Logger.Error("BotBrain", $"No route from {currentRegion} to {targetRegion}");
                return;
            }

            foreach (var hop in route)
            {
                ct.ThrowIfCancellationRequested();

                await _walker.WalkTo(hop.GateApproachPosition, ct, _session);
                _walker.ClearStuckState();
                if (ct.IsCancellationRequested) return;

                if (!hop.IsPad)
                {
                    uint gateUID = 0;
                    var deadline = DateTime.UtcNow.AddSeconds(5);
                    while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
                    {
                        gateUID = _session.SpawnedObjects
                            .FirstOrDefault(kvp => kvp.Value.RefObjID == hop.GateRefObjId).Key;
                        if (gateUID != 0) break;
                        await Task.Delay(500, ct);
                    }

                    if (gateUID == 0)
                    {
                        Logger.Error("BotBrain", $"Town return: gate not found refObjId={hop.GateRefObjId}");
                        return;
                    }

                    await Task.Delay(2000, ct);

                    var select = new Packet(0x7045);
                    select.WriteUInt(gateUID);
                    _sendPacket(select);

                    await Task.Delay(2000, ct);

                    var teleport = new Packet(0x705A);
                    teleport.WriteUInt(gateUID);
                    teleport.WriteByte(0x02);
                    teleport.WriteUInt(hop.TargetTeleportId);
                    _sendPacket(teleport);
                }
                await Task.Delay(1000);
                if (hop.IsPad)
                {
                    var padDeadline = DateTime.UtcNow.AddSeconds(10);
                    while (DateTime.UtcNow < padDeadline && !ct.IsCancellationRequested)
                    {
                        await Task.Delay(300, ct);
                        string current = RegionResolver.Resolve(
                            (short)_session.RegionId, _session.SectorX, _session.SectorY);
                        if (current == hop.ToRegion)
                            break;
                    }
                    string padRegion = RegionResolver.Resolve(
                        (short)_session.RegionId, _session.SectorX, _session.SectorY);
                    Log("BotBrain", $"Pad hop landed in: {padRegion}");
                }
                else
                {
                    var regionDeadline = DateTime.UtcNow.AddSeconds(15);
                    string stableRegion = string.Empty;
                    int stableCount = 0;
                    while (DateTime.UtcNow < regionDeadline && !ct.IsCancellationRequested)
                    {
                        await Task.Delay(500, ct);
                        string resolved = RegionResolver.Resolve(
                            (short)_session.RegionId, _session.SectorX, _session.SectorY);
                        if (resolved == stableRegion)
                        {
                            stableCount++;
                            if (stableCount >= 3) break;
                        }
                        else
                        {
                            stableRegion = resolved;
                            stableCount = 0;
                        }
                    }
                    Log("BotBrain", $"Stable region: {stableRegion}");
                }
            }
        }

        #endregion

        #region Town Sequences

        private async Task ExecuteReturnToTownAsync(CancellationToken ct)
        {
            var pos = _session.GetEstimatedPosition();
            var currentRegion = RegionResolver.Resolve((short)pos.RegionId, _session!.SectorX, _session!.SectorY);

            Log("BotBrain", $"Returning to town from {currentRegion}. Reason: {_resourceMonitor.ReturnReason}");

            bool needsTeleport = TownRequiresTeleport(currentRegion);

            if (needsTeleport)
            {
                Log("BotBrain", $"Region {currentRegion} requires teleport to reach town");
                await ExecuteTownTeleportAsync(currentRegion, ct);
                if (ct.IsCancellationRequested) return;
            }

            bool success = await _townLoop.RunAsync(ct);

            if (success)
            {
                Log("BotBrain", "Town loop complete -- setting sequence completion flag");
                _resourceMonitor.AcknowledgeReturn();
                _townSequenceCompleted = true;
                _townExitLockUntil = DateTime.UtcNow.AddSeconds(5);
            }
            else
            {
                Logger.Warn("BotBrain", "Town loop did not complete cleanly");
            }
        }

        private async Task ExecuteTownLoopAsync(CancellationToken ct)
        {
            Log("BotBrain", "Running town loop (entered town)");
            bool success = await _townLoop.RunAsync(ct);

            if (success)
            {
                Log("BotBrain", "Town loop finished successfully -- setting sequence completion flag");
                _resourceMonitor.AcknowledgeReturn();
                _townSequenceCompleted = true;
                _townExitLockUntil = DateTime.UtcNow.AddSeconds(5);
            }
            else
            {
                Logger.Warn("BotBrain", "Town loop aborted or failed.");
            }
        }

        #endregion

        #region Region / Town Data

        private static readonly HashSet<string> RegionsRequiringTownTeleport = new()
        {
            "Roc Mountain",
            "Storm Cloud Desert Entrance",
            "Storm Cloud Desert West Portal",
            "Kings Valley Desert Entrance Portal",
        };

        private static bool TownRequiresTeleport(string region) =>
            RegionsRequiringTownTeleport.Contains(region);

        // TODO: add the rest of regions to the old placeholders
        private static readonly Dictionary<string, string> RegionToTownRegion = new()
        {
            ["Roc Mountain"] = "Hotan Kingdom",
            ["Storm Cloud Desert Entrance"] = "Egypt (Alexandria)",
            ["Storm Cloud Desert West Portal"] = "Egypt (Alexandria)",
            ["Kings Valley Desert Entrance Portal"] = "Egypt (Alexandria)",
        };

        private static string? GetTownRegionFor(string region) =>
            RegionToTownRegion.TryGetValue(region, out var r) ? r : null;

        #endregion

        #region Helpers

        private bool IsInTown(BotPosition pos)
        {
            var readable = RegionResolver.ResolveReadable(pos.SectorX, pos.SectorY, (short)pos.RegionId);
            return TownLoop.KnownTownNames.Any(town => readable.StartsWith(town));
        }

        private bool NeedsTeleport(BotPosition current, BotPosition target)
        {
            string currentZone = ResolveDungeonAwareRegion(current);
            string targetZone = ResolveDungeonAwareRegion(target);
            return currentZone != targetZone;
        }

        private static string ResolveDungeonAwareRegion(BotPosition pos)
        {
            return (pos.RegionId & 0x8000) != 0
                ? RegionResolver.Resolve((short)pos.RegionId, (int)(pos.X / 192), (int)(pos.Y / 192))
                : RegionResolver.Resolve((short)pos.RegionId, pos.SectorX, pos.SectorY);
        }

        private async Task CleanupCurrentActionAsync()
        {
            if (_currentActionCts != null)
            {
                _currentActionCts.Cancel();

                if (_currentTask != null)
                {
                    try { await _currentTask; }
                    catch (OperationCanceledException) { }
                    catch (Exception ex) { Logger.Warn("BotBrain", $"Cleanup: {ex.Message}"); }
                }

                _currentActionCts.Dispose();
                _currentActionCts = null;
                _currentTask = null;
            }
        }

        private static float Distance(BotPosition a, BotPosition b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        private void SendStateUpdateToUi()
        {
            if (string.IsNullOrEmpty(_session.AccountName)) return;

            float dist = 0f;
            if (_session.TrainingDestination.HasValue)
            {
                var pos = _session.GetEstimatedPosition();
                dist = Distance(pos, _session.TrainingDestination.Value);
            }

            var mobs = _session.MobUIDs
                .Where(kvp => kvp.Value > 0)
                .Select(kvp =>
                {
                    if (!_session.SpawnedPositions.TryGetValue(kvp.Key, out var mpos))
                        return (valid: false, x: 0, y: 0, refObjId: 0u);
                    _session.SpawnedObjects.TryGetValue(kvp.Key, out var obj);
                    return (valid: true, x: mpos.WorldX, y: mpos.WorldY, refObjId: obj.RefObjID);
                })
                .Where(m => m.valid)
                .Take(128)
                .Select(m => new { m.x, m.y, m.refObjId })
                .ToArray();

            DllBridge.Instance.SendToDll(_session.AccountName, "bot_state_update", new
            {
                botState = _mode.ToString(),
                distanceToTarget = dist,
                returnReason = _resourceMonitor.ReturnReason,
                mobs
            });
        }

        #endregion
    }
}