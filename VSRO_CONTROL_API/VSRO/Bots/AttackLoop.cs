using CoreLib.Tools.Logging;
using System.Collections.Concurrent;
using VSRO_CONTROL.NavMeshApi;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Framework;
using VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.DTO.VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.Tools;

namespace VSRO_CONTROL_API.VSRO.Bots
{
    public class AttackLoop
    {
        #region - NOTES -

        // From documentation at https://www.elitepvpers.com/forum/sro-pserver-questions-answers/4810801-0xb074-packet-action-combinations.html
        // Skill Queue Structure:
        //[C -> S][7074]
        //01                                                ................
        //04                                                ................
        //7F 00 00 00                                  Cast Buff 7F
        //00                                                ................

        //[S -> C][B074]
        //01                                                OK / added to queue
        //01                                                Position in Queue 1

        //[C -> S][7074]
        //01                                                ................
        //04                                                ................
        //82 00 00 00                                  Cast Buff 82
        //00                                                ................

        //[C -> S][7074]
        //01                                                ................
        //04                                                ................
        //6E 00 00 00                                  Cast Buff 6E
        //00                                                ................

        //[S -> C][B074]
        //01                                                OK / added to queue
        //02                                                Position in Queue 2

        //[S -> C][B074]
        //01                                                OK / added to queue
        //02                                                Position in Queue 2

        //------------------------------------------------------------------------
        //my guess here is that the engine only allows a maximum queue of 2.
        //therefore the 82 00 00 00 buff cast is being overriden by the 6E 00 00 00
        //------------------------------------------------------------------------

        //[S -> C][B074]
        //02                                                casting / action done
        //01                                                queue-items remaining: 1

        //[S -> C][B074]
        //02                                                casting / action done
        //00                                                queue-items remaining: 0

        #endregion

        #region - Var -

        public static bool Debug { get; private set; } = false;
        public static void SetDebug(bool debug) => Debug = debug;
        private static void Log(string handler, string message) { if (Debug == true) Logger.Trace($"AttackLoop:{handler}", message); }

        private const float ATTACK_RANGE = 40f;
        private const float AGGRO_RADIUS = 50f;
        private float TRAINING_RADIUS = 100f;

        private readonly Action<BotPosition> _sendMove;
        private readonly Func<BotPosition> _getPosition;
        private readonly Func<Dictionary<uint, BotPosition>> _getNearbyMobs;
        private readonly Action<Packet> _sendPacket;
        private readonly VSRO.DTO.ISession _session;

        // Pickup
        private DateTime _lastTargetSearchLog = DateTime.MinValue;
        private const float MAX_PICKUP_RADIUS = 75f;
        private const float PICKUP_ARRIVAL_RADIUS = 5f;
        public bool IsPickingUp { get; private set; } = false;

        private uint _currentTargetUID = 0;
        private uint _lastHitTargetUID = 0;
        private bool _obstructed = false;
        private bool _targetDead = false;
        private bool _combatConfirmed = false;
        private readonly ConcurrentDictionary<uint, DateTime> _skipList = new();
        private List<SR_Skill> _skillsToUse = new();
        private List<SR_Skill> _buffsToUse = new();
        private readonly ConcurrentDictionary<uint, DateTime> _skillCooldowns = new();
        private bool _needsRepair = false;
        public bool NeedsRepair => _needsRepair;

        private int _skillQueueIndex = 0;
        private int _repingCount = 0;
        public IReadOnlyList<SR_Skill> SkillsToUse => _skillsToUse;
        public IReadOnlyList<SR_Skill> BuffsToUse => _buffsToUse;
        private bool _pendingKill = false;
        public bool StopAfterCurrentKill { get; set; } = false;
        private volatile bool _stuckNotified = false;

        private BotPosition? _trainingCenter = null;
        private int _wanderSpoke = 0;
        private const int WANDER_SPOKES = 8;

        public bool WaitingForResult { get; private set; } = false;
        private readonly SemaphoreSlim _resultSignal = new(0, 1);


        #endregion

        #region - Setup/Call -

        public AttackLoop(
            Func<BotPosition> getPosition,
            Func<Dictionary<uint, BotPosition>> getNearbyMobs,
            Action<Packet> sendPacket,
            Action<BotPosition> sendMove,
            VSRO.DTO.ISession session)
        {
            _getPosition = getPosition;
            _getNearbyMobs = getNearbyMobs;
            _sendPacket = sendPacket;
            _session = session;
            _sendMove = sendMove;
        }

        public async Task Run(CancellationToken ct, int radius)
        {
            TRAINING_RADIUS = radius;
            Log("AttackLoop", "Starting");

            // Discard drops from previous bot sessions so pickup doesn't chase stale clusters.
            int staleCleared = _session.DroppedItems.Count;
            _session.DroppedItems.Clear();
            if (staleCleared > 0)
                Log("AttackLoop", $"Cleared {staleCleared} stale DroppedItems from previous session");

            if (_trainingCenter == null)
            {
                _trainingCenter = _getPosition();
                Log("AttackLoop", $"Training center initialized at ({_trainingCenter.Value.X:F1},{_trainingCenter.Value.Y:F1})");
            }

            while (!ct.IsCancellationRequested)
            {
                await Task.Yield();

                if (await HandleBuffsAsync(ct))
                    continue;

                var expired = _skipList.Where(kvp => DateTime.UtcNow > kvp.Value)
                                       .Select(kvp => kvp.Key).ToList();
                foreach (var uid in expired) _skipList.TryRemove(uid, out _);

                if (StopAfterCurrentKill)
                {
                    Log("AttackLoop", "StopAfterCurrentKill requested — exiting");
                    return;
                }

                var pos = _getPosition();
                var mobs = _getNearbyMobs();

                uint nextTargetUID = 0;

                if (_currentTargetUID != 0 && mobs.ContainsKey(_currentTargetUID) && !_skipList.ContainsKey(_currentTargetUID))
                {
                    if (Distance(_trainingCenter.Value, mobs[_currentTargetUID]) <= TRAINING_RADIUS)
                    {
                        nextTargetUID = _currentTargetUID;
                    }
                    else
                    {
                        Log("AttackLoop", $"Locked target UID={_currentTargetUID} left training radius — dropping");
                        _currentTargetUID = 0;
                    }
                }

                if (nextTargetUID == 0)
                {
                    var knownMobs = mobs.Where(m => _session.MobUIDs.ContainsKey(m.Key)).ToList();
                    if (knownMobs.Count > 0 && DateTime.UtcNow - _lastTargetSearchLog > TimeSpan.FromSeconds(3))
                    {
                        _lastTargetSearchLog = DateTime.UtcNow;
                        float nearest = knownMobs.Min(m => Distance(pos, m.Value));
                        int inRadius = knownMobs.Count(m => Distance(pos, m.Value) <= AGGRO_RADIUS);
                        int inTraining = knownMobs.Count(m => Distance(_trainingCenter.Value, m.Value) <= TRAINING_RADIUS);
                        Log("AttackLoop", $"[TargetSearch] pos=({pos.X:F1},{pos.Y:F1}) mobs={knownMobs.Count} inAggroRadius={inRadius} inTraining={inTraining} nearestDist={nearest:F1}");
                    }

                    var target = mobs
                        .Where(m => _session.MobUIDs.ContainsKey(m.Key))
                        .Where(m => !_skipList.ContainsKey(m.Key))
                        .Where(m => Distance(pos, m.Value) <= AGGRO_RADIUS)
                        .Where(m => Distance(_trainingCenter.Value, m.Value) <= TRAINING_RADIUS)
                        .Where(m => !IsPathBlocked(pos, m.Value))
                        .OrderBy(m => Distance(pos, m.Value))
                        .FirstOrDefault();

                    nextTargetUID = target.Key;
                }

                if (nextTargetUID != 0)
                {
                    bool isNewTarget = _currentTargetUID != nextTargetUID;
                    _currentTargetUID = nextTargetUID;

                    if (isNewTarget)
                    {
                        _repingCount = 0;
                        _combatConfirmed = false;
                        _obstructed = false;
                        _targetDead = false;

                        Log("AttackLoop", $"Engaging Target UID={_currentTargetUID} dist={Distance(pos, mobs[_currentTargetUID]):F1}");

                        if (!_session.SpawnedObjects.ContainsKey(_currentTargetUID))
                        {
                            Log("AttackLoop", $"UID={_currentTargetUID} no longer spawned — dropping");
                            _currentTargetUID = 0;
                            continue;
                        }

                        // Clear signals, select target
                        while (_resultSignal.CurrentCount > 0) _resultSignal.Wait(0);
                        _sendPacket(BuildSelectTarget(_currentTargetUID));
                        await Task.Delay(50, ct);

                        // Start auto attack
                        _sendPacket(BuildAutoAttack(_currentTargetUID));
                    }

                    // Evaluate skill use safely on every iteration
                    var skill = PickNextSkill();
                    if (skill != null)
                    {
                        Log("AttackLoop", $"Using skill {skill.ReadableName} (ID={skill.ID})");
                        while (_resultSignal.CurrentCount > 0) _resultSignal.Wait(0); // Clean signal tracker for skill

                        _sendPacket(BuildSkillUse(skill.ID, _currentTargetUID));
                        RecordSkillUsed(skill);

                        WaitingForResult = true;
                        int timeoutMs = _combatConfirmed ? 1200 : 800;
                        bool responded = await _resultSignal.WaitAsync(timeoutMs, ct);
                        WaitingForResult = false;

                        if (responded && !_targetDead && !_obstructed)
                        {
                            int castWait = skill.PreparingTime + skill.CastingTime;
                            if (castWait > 0)
                                await Task.Delay(castWait + 50, ct); // Block loop exactly for the skill animation time
                        }
                        else if (!responded)
                        {
                            HandleTimeout();
                            continue;
                        }
                    }
                    else
                    {
                        // No skills available (on cooldown). 
                        await Task.Delay(100, ct);
                    }

                    // State cleanup checks
                    if (_targetDead)
                    {
                        _session.SpawnedPositions.TryRemove(_currentTargetUID, out _);
                        _session.SpawnedObjects.TryRemove(_currentTargetUID, out _);
                        _session.MobUIDs.TryRemove(_currentTargetUID, out _);
                        _currentTargetUID = 0;

                        if (_session._botSettings.Pickup.PickAll)
                            await RunPickupRoutineAsync(ct);

                        continue;
                    }

                    if (_obstructed)
                    {
                        _skipList[_currentTargetUID] = DateTime.UtcNow.AddSeconds(10);
                        _currentTargetUID = 0;
                        continue;
                    }

                    continue;
                }

                if (_pendingKill && !_targetDead)
                {
                    Log("AttackLoop", $"[RaceGuard] pendingKill uid={_currentTargetUID} — waiting for kill confirmation");
                    await _resultSignal.WaitAsync(400, ct);
                }

                if (_targetDead)
                {
                    _targetDead = false;
                    _pendingKill = false;
                    _session.SpawnedPositions.TryRemove(_currentTargetUID, out _);
                    _session.SpawnedObjects.TryRemove(_currentTargetUID, out _);
                    _session.MobUIDs.TryRemove(_currentTargetUID, out _);
                    _currentTargetUID = 0;

                    if (_session._botSettings.Pickup.PickAll)
                        await RunPickupRoutineAsync(ct);
                    continue;
                }

                await WanderStep(_trainingCenter.Value, ct);
            }

            Logger.Info("AttackLoop", "Stopped");
        }

        #endregion

        #region - Routines -


        /// <summary>
        /// Runs a pickup routine, will attempt picking up everything in the train radius. 
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task RunPickupRoutineAsync(CancellationToken ct)
        {
            IsPickingUp = true;
            try
            {
                // Cancel auto-attack follow
                var pos = _getPosition();
                _sendMove(pos);

                var dropDeadline = DateTime.UtcNow.AddMilliseconds(1000);
                while (DateTime.UtcNow < dropDeadline && !ct.IsCancellationRequested)
                {
                    await Task.Delay(100, ct);
                    pos = _getPosition();
                    if (_session.DroppedItems.Any(kvp =>
                    {
                        var (_, _, wx, wy) = kvp.Value;
                        float ddx = wx - pos.X, ddy = wy - pos.Y;
                        return MathF.Sqrt(ddx * ddx + ddy * ddy) <= MAX_PICKUP_RADIUS;
                    })) break;
                }
                pos = _getPosition();

                var itemsToPickup = _session.DroppedItems
                    .Where(kvp =>
                    {
                        var (_, codeName, wx, wy) = kvp.Value;
                        float dx = wx - pos.X;
                        float dy = wy - pos.Y;
                        float dist = MathF.Sqrt(dx * dx + dy * dy);
                        if (dist > MAX_PICKUP_RADIUS) return false;
                        return true;
                    })
                    .OrderBy(kvp =>
                    {
                        var (_, _, wx, wy) = kvp.Value;
                        float dx = wx - pos.X;
                        float dy = wy - pos.Y;
                        return MathF.Sqrt(dx * dx + dy * dy);
                    })
                    .ToList();

                if (itemsToPickup.Count == 0)
                {
                    Log("AttackLoop", "No items to pickup");
                    return;
                }

                Log("AttackLoop", $"Pickup routine — {itemsToPickup.Count} items in range");

                foreach (var kvp in itemsToPickup)
                {
                    if (ct.IsCancellationRequested) return;

                    uint itemUID = kvp.Key;
                    var (_, codeName, targetWorldX, targetWorldY) = kvp.Value;

                    if (!_session.DroppedItems.ContainsKey(itemUID)) continue;

                    pos = _getPosition();
                    float dx = targetWorldX - pos.X;
                    float dy = targetWorldY - pos.Y;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);

                    if (dist > MAX_PICKUP_RADIUS)
                    {
                        Log("AttackLoop", $"Pickup skip: {codeName} uid={itemUID} dist={dist:F1} — outside radius at pickup time");
                        continue;
                    }

                    Log("AttackLoop", $"Pickup: {codeName} uid={itemUID} dist={dist:F1}");

                    float prevDist = dist;
                    int stuckCount = 0;
                    bool walkStuck = false;

                    while (dist > PICKUP_ARRIVAL_RADIUS && !ct.IsCancellationRequested)
                    {
                        float stepDist = Math.Min(dist, 15f);
                        float nx = dx / dist;
                        float ny = dy / dist;
                        float stepX = pos.X + nx * stepDist;
                        float stepY = pos.Y + ny * stepDist;
                        bool isDungeon = (pos.RegionId & 0x8000) != 0;
                        var stepTarget = isDungeon
                            ? BotPosition.FromDisplayWorldDungeon(stepX, stepY, pos.ZOffset, pos.RegionId)
                            : BotPosition.FromDisplayWorld(stepX, stepY, pos.ZOffset);

                        float gameSpeed = _session.GetCurrentMoveSpeed();
                        float unitsPerSec = gameSpeed / 7.86f;
                        int travelMs = (int)(stepDist / unitsPerSec * 1000f);
                        int waitMs = Math.Clamp(travelMs + 200, 100, 8000);

                        Log("AttackLoop", $"  Pickup move → ({stepX:F1},{stepY:F1}) waitMs={waitMs}");
                        _sendMove(stepTarget);

                        await Task.Delay(waitMs, ct);

                        pos = _getPosition();
                        Log("Pickup", $"  Arrived at pos=({pos.X:F1},{pos.Y:F1}) RawX={_session.RawX} RawY={_session.RawY}");

                        dx = targetWorldX - pos.X;
                        dy = targetWorldY - pos.Y;
                        dist = MathF.Sqrt(dx * dx + dy * dy);

                        if (dist >= prevDist - 1f)
                        {
                            stuckCount++;
                            if (stuckCount >= 3)
                            {
                                Log("Pickup", $"  No progress after {stuckCount} steps at dist={dist:F1} — giving up on {codeName} uid={itemUID}");
                                walkStuck = true;
                                break;
                            }
                        }
                        else
                        {
                            stuckCount = 0;
                        }
                        prevDist = dist;
                    }

                    if (ct.IsCancellationRequested) return;

                    // Stuck detection broke us out before arriving
                    // Sending pickup from dist > PICKUP_ARRIVAL_RADIUS triggers server "invalid target".
                    if (walkStuck)
                    {
                        _session.DroppedItems.TryRemove(itemUID, out _);
                        continue;
                    }

                    Log("Pickup", $"  Sending pickup for {codeName} uid={itemUID} at pos=({pos.X:F1},{pos.Y:F1})");
                    _sendPacket(BuildPickup(itemUID));
                    _session.DroppedItems.TryRemove(itemUID, out _);
                    Log("AttackLoop", $"Pickup sent for {codeName} uid={itemUID}");

                    await Task.Delay(350, ct);
                }

                Log("AttackLoop", "Pickup routine complete");
            }
            finally
            {
                IsPickingUp = false;
            }
        }

        /// <summary>
        /// Advances through evenly-spaced spokes around the training radius, tracing a polygon
        /// around the perimeter instead of zigzagging back through center. Each call moves one
        /// spoke forward so consecutive steps travel along the edge, not inward.
        /// Includes stuck detection: if the character hasn't moved after the expected travel
        /// time, back-steps with alternating perpendicular offset then returns to center.
        /// </summary>
        private async Task WanderStep(BotPosition center, CancellationToken ct)
        {
            _stuckNotified = false; // clear any stale stuck signal from previous step
            var pos = _getPosition();
            bool isDungeon = (pos.RegionId & 0x8000) != 0;

            _wanderSpoke = (_wanderSpoke + 1) % WANDER_SPOKES;
            float spokeAngle = _wanderSpoke * (MathF.PI * 2f / WANDER_SPOKES);
            float jitter = (float)((Random.Shared.NextDouble() - 0.5) * (MathF.PI / WANDER_SPOKES));
            float angle = spokeAngle + jitter;

            float[] distFractions = [0.95f, 0.75f, 0.55f, 0.35f];
            foreach (float fraction in distFractions)
            {
                float dist = TRAINING_RADIUS * fraction;
                float wx = center.X + MathF.Cos(angle) * dist;
                float wy = center.Y + MathF.Sin(angle) * dist;

                var target = isDungeon
                    ? BotPosition.FromDisplayWorldDungeon(wx, wy, pos.ZOffset, pos.RegionId)
                    : BotPosition.FromDisplayWorld(wx, wy, pos.ZOffset);

                if (IsPathBlocked(pos, target)) continue;

                var mid = isDungeon
                    ? BotPosition.FromDisplayWorldDungeon((pos.X + wx) * 0.5f, (pos.Y + wy) * 0.5f, pos.ZOffset, pos.RegionId)
                    : BotPosition.FromDisplayWorld((pos.X + wx) * 0.5f, (pos.Y + wy) * 0.5f, pos.ZOffset);

                if (IsPathBlocked(pos, mid)) continue;

                Log("AttackLoop", $"Wander spoke {_wanderSpoke}/{WANDER_SPOKES} {angle * 180f / MathF.PI:F0}° → ({wx:F1},{wy:F1}) dist={dist:F1}");

                _sendMove(target);

                // time the expected journey, break early on mob
                float speed = Math.Max(_session.GetCurrentMoveSpeed() / 7.86f, 5f);
                int travelMs = Math.Clamp((int)(dist / speed * 1000f) + 400, 600, 5000);
                if (await WanderWait(travelMs, center, ct)) return;

                // sit at the spoke end so nearby mobs can walk into aggro range
                if (await WanderWait(Random.Shared.Next(250, 1000), center, ct)) return;
                return;
            }

            await Task.Delay(Random.Shared.Next(300, 600), ct);
        }

        /// <summary>
        /// Polls for nearby mobs every 200 ms for up to <paramref name="ms"/> milliseconds.
        /// Returns true immediately when a valid target appears so the caller can break out.
        /// </summary>
        private async Task<bool> WanderWait(int ms, BotPosition center, CancellationToken ct)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(ms);
            while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
            {
                await Task.Delay(200, ct);

                if (_stuckNotified)
                {
                    _stuckNotified = false;
                    Log("AttackLoop", "Stuck correction received during wander — aborting step");
                    return true;
                }

                var currentPos = _getPosition();
                bool mobFound = _getNearbyMobs().Any(m =>
                    _session.MobUIDs.ContainsKey(m.Key)
                    && !_skipList.ContainsKey(m.Key)
                    && Distance(currentPos, m.Value) <= AGGRO_RADIUS
                    && Distance(center, m.Value) <= TRAINING_RADIUS
                    && !IsPathBlocked(currentPos, m.Value));
                if (mobFound)
                {
                    Log("AttackLoop", "Mob spotted during wander — breaking to attack");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Handles casting buffs for the trainplace, loops through assigned buffs.
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task<bool> HandleBuffsAsync(CancellationToken ct)
        {
            if (_session._botSettings.Attack.UseZerkRightAwayWhenFull == true)
            {
                _sendPacket(BuildBerzerkUse());
            }
            //else if (_session._botSettings) -- For override


            if (_buffsToUse == null || _buffsToUse.Count == 0)
                return false;

            bool castAnyBuff = false;

            foreach (var buff in _buffsToUse)
            {
                if (ct.IsCancellationRequested) break;

                bool alreadyActive;
                lock (_session.Buffs)
                    alreadyActive = _session.Buffs.Any(s => s.ID == buff.ID);

                if (alreadyActive)
                    continue;

                if (_skillCooldowns.TryGetValue(buff.ID, out var cd) && DateTime.UtcNow < cd)
                    continue;

                Log("AttackLoop", $"Blocking cast for Buff: {buff.ReadableName} (ID={buff.ID})");
                _sendPacket(BuildBuffUse(buff.ID));
                castAnyBuff = true;

                // Minimum 250ms between consecutive buff sends. The server has a 2-slot cast queue.
                // if buffs are sent faster than they fire, the 3rd overrides slot 2 and the previous
                // occupant is silently lost. with 250ms spacing, each PrepTime=0 buff fires before
                // the next is sent, so every buff gets its own queue slot.
                int wait = buff.PreparingTime + buff.CastingTime;
                await Task.Delay(Math.Max(wait + 350, 250), ct);

                _skillCooldowns[buff.ID] = DateTime.UtcNow.AddMilliseconds(3000);
            }

            return castAnyBuff;
        }

        #endregion

        #region - Packet Building -

        private static Packet BuildSkillUse(uint skillId, uint targetUID)
        {
            var p = new Packet(0x7074);
            p.WriteByte(0x01);
            p.WriteByte(0x04);
            p.WriteUInt(skillId);
            p.WriteByte(0x01);
            p.WriteUInt(targetUID);
            return p;
        }
        private static Packet BuildBuffUse(uint skillId)
        {
            var p = new Packet(0x7074);
            p.WriteByte(0x01);
            p.WriteByte(0x04);
            p.WriteUInt(skillId);
            p.WriteByte(0x00);
            return p;
        }
        private static Packet BuildPickup(uint uniqueId)
        {
            var p = new Packet(0x7074);
            p.WriteByte(0x01); // Execute action
            p.WriteByte(0x02); // Pickup mode
            p.WriteByte(0x01); // Target is entity
            p.WriteUInt(uniqueId); // unique ID
            return p;
        }
        private static Packet BuildBerzerkUse()
        {
            var p = new Packet(0x70A7);
            p.WriteByte(0x01);
            return p;
        }
        private static Packet BuildSelectTarget(uint targetUID)
        {
            var p = new Packet(0x7045);
            p.WriteUInt(targetUID);
            return p;
        }

        private static Packet BuildAutoAttack(uint targetUID)
        {
            var p = new Packet(0x7074);
            p.WriteByte(0x01);
            p.WriteByte(0x01);
            p.WriteByte(0x01);
            p.WriteUInt(targetUID);
            return p;
        }

        #endregion

        #region - Public Handlers/Modifiers -

        public void OnMobKilled(uint mobUID)
        {
            if (mobUID != _currentTargetUID) return;
            Log("AttackLoop", $"Mob UID={mobUID} killed — waiting for queue drain");
            _pendingKill = true;
        }
        public void OnDurabilityChanged(byte slot, uint durability)
        {
            if (durability <= _session._botSettings.Maintenance.RepairDurabilityThreshold)
            {
                Log("AttackLoop", $"Slot {slot} durability critical ({durability}) — flagging repair needed");
                _needsRepair = true;

                if (StopAfterCurrentKill == false)
                {
                    StopAfterCurrentKill = true;
                    Logger.Info("AttackLoop", "Durability critical — requesting stop after current kill");
                }
            }
        }
        public void ClearRepairFlag()
        {
            _needsRepair = false;
        }
        public void OnSkillAttackResponse(byte type, byte status)
        {
            Log("AttackLoop", $"0xB074 type={type:X2} status={status:X2}");

            if (type == 0x02 && status == 0x00)
            {
                if (_pendingKill)
                {
                    _pendingKill = false;
                    _targetDead = true;
                }
            }
            TrySignal();
        }
        public void OnAttackResult(uint attackerUID, uint targetUID)
        {
            if (attackerUID != _session.CharacterUID) return;
            _lastHitTargetUID = targetUID;
            _combatConfirmed = true;
            TrySignal();
        }
        
        public void SetTrainingCenter(BotPosition centerPoint)
        {
            _trainingCenter = centerPoint;
            Log("AttackLoop", $"Training center explicitly locked at ({_trainingCenter.Value.X:F1},{_trainingCenter.Value.Y:F1})");
        }
        public void AddSkillToUse(SR_Skill skill, bool isBuff)
        {
            if (isBuff)
            {
                if (!_buffsToUse.Any(s => s.ID == skill.ID))
                    _buffsToUse.Add(skill);
            }
            else
            {
                if (!_skillsToUse.Any(s => s.ID == skill.ID))
                    _skillsToUse.Add(skill);
            }
        }
        public void RemoveSkillToUse(uint skillId, bool isBuff)
        {
            if (isBuff)
                _buffsToUse.RemoveAll(s => s.ID == skillId);
            else
                _skillsToUse.RemoveAll(s => s.ID == skillId);
        }
        public bool ReplaceSkillInUse(uint oldSkillId, SR_Skill newSkill, bool isBuff)
        {
            var list = isBuff ? _buffsToUse : _skillsToUse;
            int idx = list.FindIndex(s => s.ID == oldSkillId);
            if (idx < 0) return false;
            list[idx] = newSkill;
            return true;
        }
        public void MoveSkillPriority(uint skillId, int direction)
        {
            var idx = _skillsToUse.FindIndex(s => s.ID == skillId);
            if (idx < 0) return;
            int newIdx = Math.Clamp(idx + direction, 0, _skillsToUse.Count - 1);
            if (newIdx == idx) return;
            var skill = _skillsToUse[idx];
            _skillsToUse.RemoveAt(idx);
            _skillsToUse.Insert(newIdx, skill);
        }
        public void ResetRuntime()
        {
            _currentTargetUID = 0;
            _repingCount = 0;
            _skillQueueIndex = 0;
            _skillCooldowns.Clear();
            _skipList.Clear();
            _combatConfirmed = false;
            _obstructed = false;
            _targetDead = false;
            _pendingKill = false;
            StopAfterCurrentKill = false;
            _trainingCenter = null;
            _wanderSpoke = 0;
            _needsRepair = false;
        }

        public void NotifyStuck()
        {
            _stuckNotified = true;
        }

        public void ClearCooldowns() => _skillCooldowns.Clear();
        public void ClearSkillCooldown(uint skillId) => _skillCooldowns.TryRemove(skillId, out _);

        #endregion

        #region - Internal/Private -

        private SR_Skill? PickNextSkill()
        {
            var skills = this.SkillsToUse;
            if (skills.Count == 0) return null;

            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                if (_skillCooldowns.TryGetValue(skill.ID, out var readyAt) && DateTime.UtcNow < readyAt)
                    continue;
                return skill;
            }
            return null;
        }
        private void RecordSkillUsed(SR_Skill skill)
        {
            int cooldownMs = Math.Max(skill.CoolTime, skill.ReuseDelay);
            if (cooldownMs > 0)
                _skillCooldowns[skill.ID] = DateTime.UtcNow.AddMilliseconds(cooldownMs);
        }
        private void HandleTimeout()
        {
            Log("AttackLoop", $"Attack unconfirmed timeout UID={_currentTargetUID}, skipping");
            _skipList[_currentTargetUID] = DateTime.UtcNow.AddSeconds(5);
            _currentTargetUID = 0;
        }
        private static float Distance(BotPosition a, BotPosition b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }
        private bool IsPathBlocked(BotPosition src, BotPosition dst)
        {
            var srcT = NavMeshPathfinder.MakeTransform(src);
            var dstT = NavMeshPathfinder.MakeTransform(dst);

            if (!NavMeshManager.ResolveCellAndHeight(srcT)) return false;
            if (!NavMeshManager.ResolveCellAndHeight(dstT)) return false;

            bool reached = NavMeshManager.Raycast(srcT, dstT, NavMeshRaycastType.Move, out var hit);
            return !reached && hit != null && !hit.Edge!.IsRailing;
        }
        private void TrySignal()
        {
            if (_resultSignal.CurrentCount == 0)
                _resultSignal.Release();
        }

        #endregion
    }
}