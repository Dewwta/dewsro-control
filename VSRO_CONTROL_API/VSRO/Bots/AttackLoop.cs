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
        #region - Var -

        private const float ATTACK_RANGE = 40f;
        private const float AGGRO_RADIUS = 50f;
        private float TRAINING_RADIUS = 100f;

        private readonly Action<BotPosition> _sendMove;
        private readonly Func<BotPosition> _getPosition;
        private readonly Func<Dictionary<uint, BotPosition>> _getNearbyMobs;
        private readonly Action<Packet> _sendPacket;
        private readonly VSRO.DTO.ISession _session;

        // Pickup
        private int _killsSinceLastPickup = 0;
        private const int KILLS_PER_PICKUP = 3;
        private const float MAX_PICKUP_RADIUS = 75f;
        private const float PICKUP_ARRIVAL_RADIUS = 3f;

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

        private BotPosition? _trainingCenter = null;

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
            Logger.Info("AttackLoop", "Starting");

            if (_trainingCenter == null)
            {
                _trainingCenter = _getPosition();
                Logger.Info("AttackLoop", $"Training center initialized at ({_trainingCenter.Value.X:F1},{_trainingCenter.Value.Y:F1})");
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
                    Logger.Info("AttackLoop", "StopAfterCurrentKill requested — exiting");
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
                        Logger.Debug("AttackLoop", $"Locked target UID={_currentTargetUID} left training radius — dropping");
                        _currentTargetUID = 0;
                    }
                }

                if (nextTargetUID == 0)
                {
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

                        Logger.Debug("AttackLoop", $"Engaging Target UID={_currentTargetUID} dist={Distance(pos, mobs[_currentTargetUID]):F1}");

                        if (!_session.SpawnedObjects.ContainsKey(_currentTargetUID))
                        {
                            Logger.Debug("AttackLoop", $"UID={_currentTargetUID} no longer spawned — dropping");
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
                        Logger.Debug("AttackLoop", $"Using skill {skill.ReadableName} (ID={skill.ID})");
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
                        {
                            _killsSinceLastPickup++;
                            if (_killsSinceLastPickup >= KILLS_PER_PICKUP)
                            {
                                _killsSinceLastPickup = 0;
                                await RunPickupRoutineAsync(ct);
                            }
                        }
                        

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
            var pos = _getPosition();

            var itemsToPickup = _session.DroppedItems
                .Where(kvp =>
                {
                    var (_, codeName, wx, wy) = kvp.Value;
                    float dx = wx - pos.X;
                    float dy = wy - pos.Y;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > MAX_PICKUP_RADIUS) return false;

                    // Check pickup setting if available
                    //var opts = _session._botSettings?.ItemOptions
                    //    ?.FirstOrDefault(o => o.CodeName == codeName);
                    //if (opts != null && !opts.Pickup) return false;

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

            if (itemsToPickup.Count == 0) return;

            Logger.Debug("AttackLoop", $"Pickup routine — {itemsToPickup.Count} items in range");

            foreach (var kvp in itemsToPickup)
            {
                if (ct.IsCancellationRequested) return;

                uint itemUID = kvp.Key;
                var (_, codeName, targetWorldX, targetWorldY) = kvp.Value;

                // Re-check item still exists
                if (!_session.DroppedItems.ContainsKey(itemUID)) continue;

                pos = _getPosition();
                float dx = targetWorldX - pos.X;
                float dy = targetWorldY - pos.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);

                Logger.Debug("AttackLoop", $"Pickup: {codeName} uid={itemUID} dist={dist:F1}");

                // Walk to item in steps if needed
                while (dist > PICKUP_ARRIVAL_RADIUS && !ct.IsCancellationRequested)
                {
                    float stepDist = Math.Min(dist, 15f);
                    float nx = dx / dist;
                    float ny = dy / dist;

                    float stepX = pos.X + nx * stepDist;
                    float stepY = pos.Y + ny * stepDist;
                    bool isDungeon = (pos.RegionId & 0x8000) != 0;
                    var stepPos = isDungeon
                        ? BotPosition.FromDisplayWorldDungeon(stepX, stepY, pos.ZOffset, pos.RegionId)
                        : BotPosition.FromDisplayWorld(stepX, stepY, pos.ZOffset);
                    _sendMove(stepPos);

                    float gameSpeed = _session.GetCurrentMoveSpeed();
                    float unitsPerSec = gameSpeed / 7.86f;
                    int travelMs = (int)(stepDist / unitsPerSec * 1000f);
                    int waitMs = Math.Clamp(travelMs + 150, 100, 8000);

                    Logger.Debug("AttackLoop", $"  Pickup move → ({stepX:F1},{stepY:F1}) waitMs={waitMs}");

                    await Task.Delay(waitMs, ct);

                    pos = _getPosition();
                    dx = targetWorldX - pos.X;
                    dy = targetWorldY - pos.Y;
                    dist = MathF.Sqrt(dx * dx + dy * dy);
                }

                if (ct.IsCancellationRequested) return;

                // Send pickup packet
                _sendPacket(BuildPickup(itemUID));
                _session.DroppedItems.TryRemove(itemUID, out _);
                Logger.Debug("AttackLoop", $"Pickup sent for {codeName} uid={itemUID}");

                await Task.Delay(350, ct);
            }

            Logger.Debug("AttackLoop", "Pickup routine complete");
            await Task.Delay(350, ct);
        }

        /// <summary>
        /// Wanders around the bot radius until interrupted, needs tweaking.
        /// </summary>
        /// <param name="center"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task WanderStep(BotPosition center, CancellationToken ct)
        {
            var pos = _getPosition();
            bool isDungeon = (pos.RegionId & 0x8000) != 0;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                float angle = (float)(Random.Shared.NextDouble() * Math.PI * 2);
                float dist = (float)(Random.Shared.NextDouble() * (TRAINING_RADIUS * 0.6f) + 5f);

                float wx = center.X + MathF.Cos(angle) * dist;
                float wy = center.Y + MathF.Sin(angle) * dist;

                var wanderTarget = isDungeon
                    ? BotPosition.FromDisplayWorldDungeon(wx, wy, pos.ZOffset, pos.RegionId)
                    : BotPosition.FromDisplayWorld(wx, wy, pos.ZOffset);

                if (IsPathBlocked(pos, wanderTarget))
                    continue;

                float mx = (pos.X + wx) * 0.5f;
                float my = (pos.Y + wy) * 0.5f;
                var midpoint = isDungeon
                    ? BotPosition.FromDisplayWorldDungeon(mx, my, pos.ZOffset, pos.RegionId)
                    : BotPosition.FromDisplayWorld(mx, my, pos.ZOffset);

                if (IsPathBlocked(pos, midpoint))
                    continue;

                Logger.Debug("AttackLoop", $"Wander → ({wx:F1},{wy:F1}) dist from center={dist:F1}");
                _sendMove(wanderTarget);

                int wanderWaitMs = Random.Shared.Next(500, 2000);
                var deadline = DateTime.UtcNow.AddMilliseconds(wanderWaitMs);
                while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
                {
                    await Task.Delay(200, ct);

                    var currentPos = _getPosition();
                    var mobs = _getNearbyMobs();
                    bool mobFound = mobs
                        .Any(m => _session.MobUIDs.ContainsKey(m.Key)
                                && !_skipList.ContainsKey(m.Key)
                                && Distance(currentPos, m.Value) <= AGGRO_RADIUS
                                && Distance(center, m.Value) <= TRAINING_RADIUS
                                && !IsPathBlocked(currentPos, m.Value));

                    if (mobFound)
                    {
                        Logger.Debug("AttackLoop", "Mob spotted during wander — breaking to attack");
                        return;
                    }
                }
                return;
            }

            int idleMs = Random.Shared.Next(500, 1500);
            await Task.Delay(idleMs, ct);
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

                bool alreadyActive = _session.Buffs?.Any(s => s.ID == buff.ID) == true;
                if (alreadyActive)
                    continue;

                if (_skillCooldowns.TryGetValue(buff.ID, out var cd) && DateTime.UtcNow < cd)
                    continue;

                Logger.Debug("AttackLoop", $"Blocking cast for Buff: {buff.ReadableName} (ID={buff.ID})");
                _sendPacket(BuildBuffUse(buff.ID));
                RecordSkillUsed(buff);

                _skillCooldowns[buff.ID] = DateTime.UtcNow.AddMilliseconds(buff.ReuseDelay > 0 ? buff.ReuseDelay : 3000);
                castAnyBuff = true;

                int wait = buff.PreparingTime + buff.CastingTime;
                if (wait > 0)
                {
                    await Task.Delay(wait + 150, ct); // Generous overhead padding for packet round-trips
                }
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
            Logger.Debug("AttackLoop", $"Mob UID={mobUID} killed — waiting for queue drain");
            _pendingKill = true;
        }
        public void OnDurabilityChanged(byte slot, uint durability)
        {
            // Only care about equipment slots, not inventory
            // Threshold: flag repair needed when any piece drops below 20%
            // MaxDurability isn't in the packet — use a fixed low threshold
            // 0x0F = 15 which is clearly low on a 100-max item
            if (durability <= _session._botSettings.Maintenance.RepairDurabilityThreshold)
            {
                Logger.Debug("AttackLoop", $"Slot {slot} durability critical ({durability}) — flagging repair needed");
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
            Logger.Trace("AttackLoop", $"0xB074 type={type:X2} status={status:X2}");

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
            Logger.Info("AttackLoop", $"Training center explicitly locked at ({_trainingCenter.Value.X:F1},{_trainingCenter.Value.Y:F1})");
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
            _needsRepair = false;
            _killsSinceLastPickup = 0;
        }

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
            Logger.Debug("AttackLoop", $"Attack unconfirmed timeout UID={_currentTargetUID}, skipping");
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