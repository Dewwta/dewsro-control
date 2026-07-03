using CoreLib.Tools.Logging;
using System.Collections.Concurrent;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Achivements;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.DTO;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Framework;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Tracking;
using VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.DTO.VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.Tools;
using VSRO_CONTROL_API.VSRO.Enums;
namespace VSRO_CONTROL_API.VSRO.AsynchronousProxy
{
    public static class PacketParser 
    {
        
        public static bool Debug { get; private set; } = false;
        public static void SetDebug(bool debug) => Debug = debug;
        private static void Log(string handler, string message) { if (Debug == true) Logger.Trace($"PacketParser:{handler}", message); }


        // Server
        public static async Task<CharDataReturn?> ParseCharData(Packet packet)
        {
            // This packet is the reason we can't have nice things.
            try
            {
                CharDataReturn _data = new CharDataReturn();

                _data.LastLoginTime = packet.ReadUInt();
                Log("CharData", $"serverTime={_data.LastLoginTime}");

                _data.RefObjId = packet.ReadUInt();
                Log("CharData", $"refObjId={_data.RefObjId}");

                _data.Scale = packet.ReadByte();
                _data.CurrentLevel = packet.ReadByte();
                _data.MaxLevel = packet.ReadByte();

                Log("CharData", $"scale={_data.Scale} curLevel={_data.CurrentLevel} maxLevel={_data.MaxLevel}");

                _data.ExpOffset = packet.ReadULong();
                _data.SkillExpOffset = packet.ReadUInt();

                Log("CharData", $"expOffset={_data.ExpOffset} sExpOffset={_data.SkillExpOffset}");

                _data.RemainingGold = packet.ReadULong();
                _data.RemainingSkillPoint = packet.ReadUInt();
                _data.RemainingStatPoint = packet.ReadUShort();

                Log("CharData", $"gold={_data.RemainingGold} skill={_data.RemainingSkillPoint} stat={_data.RemainingStatPoint}");

                _data.RemainingZerkBubbles = packet.ReadByte(); // Zerk gauge bubbles
                _data.GatherExp = packet.ReadUInt();       // Academy Exp
                _data.HP = packet.ReadUInt();              // Current HP
                _data.MP = packet.ReadUInt();              // Current MP
                _data.AutoInvestExp = packet.ReadByte();   // Beginner icon / Auto invest
                _data.DailyPK = packet.ReadByte();         // Daily PK count
                _data.TotalPK = packet.ReadUShort();     // Total PK count
                _data.PKPenaltyPoint = packet.ReadUInt();  // PK Penalty points
                _data.ZerkTitleLevel = packet.ReadByte();       // Zerk Title level
                _data.FreePVPFlag = packet.ReadByte();         // Cape / Free PVP flag00

                Log("CharData", $"HP={_data.HP} MP={_data.MP} Zerk={_data.RemainingZerkBubbles}");

                _data.InventorySize = packet.ReadByte();         // 0x61
                _data.ItemCount = packet.ReadByte();             // 0x1C

                Log("CharData", $"invSize={_data.InventorySize} itemCount={_data.ItemCount}");

                for (int i = 0; i < _data.ItemCount; i++)
                {
                    Log("CharData", $"--- ITEM {i} ---");

                    // SLOT & RENT
                    byte slot = packet.ReadByte();
                    uint rentType = packet.ReadUInt();
                    Log("CharData", $"ITEM {i}: slot={slot} rentType={rentType}");

                    switch (rentType)
                    {
                        case 1:
                            packet.ReadUShort(); packet.ReadUInt(); packet.ReadUInt();
                            break;
                        case 2:
                            packet.ReadUShort(); packet.ReadUShort(); packet.ReadUInt();
                            break;
                        case 3:
                            packet.ReadUShort();
                            packet.ReadUInt();
                            packet.ReadUInt();
                            packet.ReadUShort();
                            packet.ReadUInt();
                            break;
                        default:
                            break;
                    }

                    // ITEMS
                    uint refItemId = packet.ReadUInt();
                    var itemInfo = await DBConnect.GetItemRecord(refItemId);

                    if (!itemInfo.success)
                    {
                        Logger.Warn("CharData", $"DB MISS: refItemId={refItemId} at item index={i}, slot={slot}");
                        Logger.Warn("CharData",
                            $"Unknown refItemId={refItemId} at slot={slot} — reading ushort as fallback");
                        packet.ReadUShort(); // assume default stackable
                        continue;
                    }
                    // Initialize defaults
                    ushort finalStack = 1;
                    Log("CharData", $"{itemInfo.item!.CodeName}: T1={itemInfo.item.T1} | T2={itemInfo.item.T2} | T3={itemInfo.item.T3} | T4={itemInfo.item.T4}");

                    if (itemInfo.item.T1 == 1) // NPC/Character objects
                    {
                        // Need to determine structure
                        Logger.Warn("CharData", $"T1=1 object: {itemInfo.item.CodeName} T2={itemInfo.item.T2} T3={itemInfo.item.T3} T4={itemInfo.item.T4} remaining={packet.RemainingRead()}");
                    }
                    else if (itemInfo.item.T1 == 3) // ITEM_
                    {
                        if (itemInfo.item.T2 == 1) // Equipment & Avatars
                        {
                            packet.ReadByte();   // OptLevel
                            packet.ReadULong();  // Variance
                            packet.ReadUInt();   // Durability

                            byte magParamNum = packet.ReadByte();
                            for (int p = 0; p < magParamNum; p++)
                            {
                                packet.ReadUInt(); packet.ReadUInt();
                            }

                            // Sockets (Binding Type 1)
                            packet.ReadByte(); // Binding Type
                            byte socketCount = packet.ReadByte();
                            for (int j = 0; j < socketCount; j++)
                            {
                                packet.ReadByte(); packet.ReadUInt(); packet.ReadUInt();
                            }

                            // Adv Elixirs (Binding Type 2)
                            packet.ReadByte(); // Binding Type
                            byte advCount = packet.ReadByte();
                            for (int j = 0; j < advCount; j++)
                            {
                                packet.ReadByte(); packet.ReadUInt(); packet.ReadUInt();
                            }
                        }
                        else if (itemInfo.item.T2 == 2) // COS
                        {
                            if (itemInfo.item.T3 == 1)
                            {
                                byte state = packet.ReadByte();
                                Log("PetHandler:Chardata", $"STATE={state}");
                                if (state == 1) continue;

                                uint cosObjId = packet.ReadUInt();
                                ushort nameLen = packet.ReadUShort();
                                for (int s = 0; s < nameLen; s++)
                                    packet.ReadByte();

                                if (itemInfo.item.T4 == 2) // AbilityPet
                                    packet.ReadUInt(); // SecondsToRentEndTime

                                byte unk02 = packet.ReadByte();
                                Log("PetHandler:Chardata", $"unk02={unk02} remaining_after_unk02={packet.RemainingRead()}");
                                if (unk02 != 0)
                                {
                                    int entrySize = (itemInfo.item.T4 == 2) ? 14 : 9;
                                    int extraBytes = unk02 * entrySize;
                                    Log("PetHandler:Chardata", $"reading {extraBytes} extra bytes (unk02={unk02} x {entrySize})");
                                    for (int x = 0; x < extraBytes; x++)
                                        packet.ReadByte();
                                    Log("PetHandler:Chardata", $"remaining_after_extra={packet.RemainingRead()}");
                                }

                                finalStack = 1;
                            }
                            else if (itemInfo.item.T3 == 2) // Transport
                            {
                                uint transportObjId = packet.ReadUInt();
                            }
                            else if (itemInfo.item.T3 == 3)
                            {
                                uint mc_quantity = packet.ReadUInt();
                            }
                        }
                        else if (itemInfo.item.T2 == 3) // ETC
                        {
                            finalStack = packet.ReadUShort();

                            string code = itemInfo.item.CodeName;

                            if (code.Contains("ATTRSTONE"))
                            {
                                packet.ReadByte(); // AssimilationProbability
                            }
                            else if (code.Contains("MAGICSTONE"))
                            {
                                if (!code.Contains("_LUCK_") && !code.Contains("_APE_") && !code.Contains("_SOLID_"))
                                {
                                    packet.ReadByte();
                                }

                            }
                            else if (itemInfo.item.T3 == 14) // Cards
                            {
                                byte cardMagParam = packet.ReadByte();
                                for (int p = 0; p < cardMagParam; p++)
                                {
                                    packet.ReadUInt();
                                    packet.ReadUInt();
                                }
                            }
                        }
                    }

                    if (slot >= 13)
                    {
                        _data.Slots[slot] = new SR_Item()
                        {
                            RefItemID = (int)refItemId,
                            CodeName128 = itemInfo.item.CodeName,
                            Stack = finalStack,
                            MaxStack = itemInfo.item.MaxStack
                        };
                    }
                    else
                    {
                        _data.Equipment[slot] = new SR_Item()
                        {
                            RefItemID = (int)refItemId,
                            CodeName128 = itemInfo.item.CodeName,
                            Stack = finalStack,
                            MaxStack = itemInfo.item.MaxStack
                        };
                    }

                    Log("CharData", $"SLOT [{slot}] {itemInfo.item.CodeName} ({finalStack}/{itemInfo.item.MaxStack}) | remainingPacketRead={packet.RemainingRead()}");
                }

                _data.AvatarSize = packet.ReadByte();
                _data.AvatarCount = packet.ReadByte();

                for (int i = 0; i < _data.AvatarCount; i++)
                {
                    byte slot = packet.ReadByte();
                    uint rentType = packet.ReadUInt();

                    if (rentType == 1) { packet.ReadUShort(); packet.ReadUInt(); packet.ReadUInt(); }
                    else if (rentType == 2) { packet.ReadUShort(); packet.ReadUShort(); packet.ReadUInt(); }
                    else if (rentType == 3) { packet.ReadUShort(); packet.ReadUInt(); packet.ReadUInt(); packet.ReadUShort(); packet.ReadUInt(); }

                    uint refItemId = packet.ReadUInt();
                    var itemInfo = await DBConnect.GetItemRecord(refItemId);

                    // fuck this data in particular
                    packet.ReadByte(); packet.ReadULong(); packet.ReadUInt();
                    byte magParamNum = packet.ReadByte();
                    for (int j = 0; j < magParamNum; j++) { packet.ReadUInt(); packet.ReadUInt(); }
                    packet.ReadByte();
                    byte socketCount = packet.ReadByte();
                    for (int j = 0; j < socketCount; j++) { packet.ReadByte(); packet.ReadUInt(); packet.ReadUInt(); }
                    packet.ReadByte();
                    byte advCount = packet.ReadByte();
                    for (int j = 0; j < advCount; j++) { packet.ReadByte(); packet.ReadUInt(); packet.ReadUInt(); }

                    if (itemInfo.success)
                    {
                        _data.Avatars[slot] = new SR_Item()
                        {
                            RefItemID = (int)refItemId,
                            CodeName128 = itemInfo.item!.CodeName,
                            Stack = 1,
                            MaxStack = 1
                        };
                        Log("CharData", $"AVATAR [{slot}] {itemInfo.item.CodeName}");
                    }
                }

                packet.ReadByte();

                int masteryCount = 0;
                int mastery = packet.ReadByte();
                while (mastery == 1)
                {
                    SR_Mastery mst = new SR_Mastery();
                    mst.ID = packet.ReadUInt();
                    mst.CurLevel = packet.ReadByte();
                    Log("Chardata:Mastery", $"Mastery ID: {mst.ID}, Level: {mst.CurLevel}");

                    mastery = packet.ReadByte();
                    if (mst.CurLevel > 0)
                    {
                        _data.MasteryList.Add(mst);
                        masteryCount++;
                    }
                    
                }
                _data.MasteryCount = masteryCount;
                
                Log("Chardata:Mastery", $"Mastery count: {_data.MasteryCount} | remaining: {packet.RemainingRead()}");
                
                packet.ReadByte();

                int skillCount = 0;
                byte skillMarker = packet.ReadByte();
                while (skillMarker == 1)
                {
                    uint skillId = packet.ReadUInt();
                    bool enabled = packet.ReadByte() == 0x01;

                    var skillData = await DBConnect.GetSkillById(skillId);
                    if (skillData != null)
                    {
                        var learned = new SR_Skill
                        {
                            ID = skillData.ID,
                            CodeName = skillData.CodeName,
                            Group = skillData.Group,
                            Level = skillData.Level,
                            BasicActivity = skillData.BasicActivity,
                            PreparingTime = skillData.PreparingTime,
                            CastingTime = skillData.CastingTime,
                            ActionDuration = skillData.ActionDuration,
                            ReuseDelay = skillData.ReuseDelay,
                            CoolTime = skillData.CoolTime,
                            Range = skillData.Range,
                            AutoAttackType = skillData.AutoAttackType,
                            CanUseInTown = skillData.CanUseInTown,
                            RequiresTarget = skillData.RequiresTarget,
                            ConsumeHP = skillData.ConsumeHP,
                            ConsumeMP = skillData.ConsumeMP,
                            AIAttackChance = skillData.AIAttackChance,
                            AISkillType = skillData.AISkillType,
                            MoveSpeedPercent = skillData.MoveSpeedPercent,
                            Params = skillData.Params,
                            Enabled = enabled,
                            ReadableName = skillData.ReadableName,
                            IconFile = skillData.IconFile
                        };
                        _data.LearnedSkills.Add(learned);
                        skillCount++;
                    }

                    skillMarker = packet.ReadByte();
                }
                _data.SkillCount = skillCount;
                Log("Chardata:Skill", $"Skill count: {skillCount} | remaining: {packet.RemainingRead()}");

                // QUESTS
                ushort completedQuestCount = packet.ReadUShort();
                Log("Chardata:Quest", $"Completed quests: {completedQuestCount}");
                for (int i = 0; i < completedQuestCount; i++)
                {
                    packet.ReadUInt(); // completed quest ref ID
                }

                byte activeQuestCount = packet.ReadByte();
                Log("Chardata:Quest", $"Active quests: {activeQuestCount}");

                for (int q = 0; q < activeQuestCount; q++)
                {
                    uint questID = packet.ReadUInt();
                    ushort unkShort = packet.ReadUShort(); // horse shit
                    byte questType = packet.ReadByte();
                    byte questStatus = packet.ReadByte();

                    byte objectiveCount = 0;
                    string questName = "";

                    Log("Chardata:Quest", $"Quest {q} header: ID={questID} Type=0x{questType:X2} Status={questStatus} unkShort=0x{unkShort:X4}");

                    switch (questType)
                    {
                        
                        case 0x08:
                            break;
                        case 0x18:
                        case 0x58:
                            objectiveCount = packet.ReadByte();
                            Log("Chardata:Quest", $"Quest {q} objectiveCount={objectiveCount}");

                            for (int o = 0; o < objectiveCount; o++)
                            {
                                byte objID = packet.ReadByte();
                                byte objStatus = packet.ReadByte();
                                string objName = packet.ReadAscii();

                                // flag(1) + current_count(4)
                                byte[] tail = new byte[5];
                                for (int i = 0; i < 5; i++) tail[i] = packet.ReadByte();
                                Log("Chardata:Quest", $"Quest {q} Obj {o} tail: {BitConverter.ToString(tail).Replace("-", " ")}");

                                if (objectiveCount == 1)
                                    questName = objName;

                                Log("Chardata:Quest", $"Quest {q} Obj {o}: ID={objID} Status={objStatus} Name='{objName}'");
                            }

                            // 0x58 adds 5 quest-level bytes after all objectives (flag + target_count)
                            if (questType == 0x58)
                            {
                                byte[] questExtra = new byte[5];
                                for (int i = 0; i < 5; i++) questExtra[i] = packet.ReadByte();
                                Log("Chardata:Quest", $"Quest {q} 0x58 extra: {BitConverter.ToString(questExtra).Replace("-", " ")}");
                            }
                            break;
                        default:
                            Log("Chardata:Quest", $"Quest {q} UNHANDLED TYPE 0x{questType:X2}");
                            break;
                    }

                    Log("Chardata:Quest", $"Quest {q} done: ID={questID} Type=0x{questType:X2} Objectives={objectiveCount} Name='{(string.IsNullOrEmpty(questName) ? "Tatorial" : questName)}'");
                }
                for (int i = 0; i < 5; i++) packet.ReadByte();
                _data.CharacterUID = packet.ReadUInt();
                Log("Chardata:UID", $"CharacterUID: {_data.CharacterUID}");

                _data.SectorX = packet.ReadByte();
                _data.SectorY = packet.ReadByte();

                _data.RawX = packet.ReadFloat();
                _data.WorldZ = packet.ReadFloat(); // This is the RawZ offset
                _data.RawY = packet.ReadFloat();

                _data.RegionId = (_data.SectorY << 8) | _data.SectorX;
                _data.RegionName = RegionResolver.ResolveReadable(_data.SectorX, _data.SectorY, (short)_data.RegionId);

                // World coordinate conversion using consolidated logic
                var (botPos, outSX, outSY) = BotPosition.FromRawOffsets(_data.SectorX, _data.SectorY, _data.RawX, _data.RawY, _data.WorldZ);
                _data.WorldX = (int)botPos.X;
                _data.WorldY = (int)botPos.Y;
                _data.SectorX = outSX;
                _data.SectorY = outSY;

                Log("Chardata:Position",
                    $"Sectors: X={_data.SectorX} Y={_data.SectorY} UID={_data.CharacterUID}");
                Log("Chardata:Position",
                    $"Raw Pos: X={_data.RawX:F2} Z={_data.WorldZ:F2} Y={_data.RawY:F2}");
                Log("Chardata:Position",
                    $"World Pos: X={_data.WorldX} Y={_data.WorldY} Z={_data.WorldZ:F1}");

                packet.ReadUShort(); // unknown
                packet.ReadByte();   // move
                packet.ReadByte();   // run flag
                packet.ReadByte();   // unknown
                packet.ReadUShort(); // unknown
                packet.ReadByte();   // unknown
                packet.ReadByte();   // DeathFlag
                packet.ReadByte();   // MovementFlag
                packet.ReadByte();   // BerserkerFlag

                _data.WalkSpeed = packet.ReadFloat();
                _data.RunSpeed = packet.ReadFloat();
                packet.ReadUInt(); // Berserker/Hwan Speed
                Log("Chardata:Speeds", $"WalkSpeed={_data.WalkSpeed} RunSpeed={_data.RunSpeed}");
                Log("Chardata:Speeds", $"Remaining after speeds: {packet.RemainingRead()} bytes");

                byte activeBuffCount = packet.ReadByte();
                Log("Chardata:Buffs", $"Active buffs: {activeBuffCount}");

                // Useless fucking buff section that never contains anything, handled through BUFF_START
                for (int i = 0; i < activeBuffCount; i++)
                {
                    uint refSkillId = packet.ReadUInt();
                    ushort timedJobId = packet.ReadUShort();

                    var buff = new SR_Buff { ID = refSkillId, TimedJobId = timedJobId };
                    var buff_res = await DBConnect.GetBuffById(refSkillId);
                    if (buff_res != null)
                    {
                        Log("Chardata:Buffs", $"Buff ID: {buff.ID}, Codename: {buff_res.CodeName}");

                        buff.CodeName = buff_res.CodeName;

                        if ((uint)buff_res.Params[1] == 1701213281) // Param2 is atfe
                            buff.Creator = packet.ReadByte();

                        for (int p = 0; p < buff_res.Params.Length - 1; p += 2)
                            if ((uint)buff_res.Params[p] == 1752396901)
                                buff.MoveSpeedPercent = buff_res.Params[p + 1];
                    }
                    _data.Buffs.Add(buff);
                }

                _data.CharacterName = packet.ReadAscii();
                ushort jobNameLength = packet.ReadUShort();
                if (jobNameLength != 0)
                {
                    packet.SeekBack(2);
                    packet.ReadAscii();
                }

                _data.JobType = packet.ReadByte();
                _data.JobLevel = packet.ReadByte();

                _data.JobExp = packet.ReadUInt();
                _data.JobContribution = packet.ReadUInt();
                _data.JobReward = packet.ReadUInt();

                byte unk7 = packet.ReadByte();
                byte unk8 = packet.ReadByte();
                byte unk9 = packet.ReadByte();
                byte PKFlag = packet.ReadByte();
                ulong unk10 = packet.ReadULong();
                _data.AccountJID = packet.ReadUInt();
                _data.IsGM = packet.ReadByte() == 0x00 ? false : true;
                byte unk11 = packet.ReadByte();

                return _data;
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::CharData", ex.Message + ex.StackTrace);
                return null;
            }
        }
        public static async Task<CharStatsReturn?> ParseCharStats(Packet packet)
        {
            try
            {
                CharStatsReturn _data = new CharStatsReturn();

                _data.PhyAtkMin = packet.ReadUInt();
                _data.PhyAtkMax = packet.ReadUInt();
                _data.MagAtkMin = packet.ReadUInt();
                _data.MagAtkMax = packet.ReadUInt();

                _data.PhyDef = packet.ReadUShort();
                _data.MagDef = packet.ReadUShort();
                _data.HitRate = packet.ReadUShort();
                _data.ParryRate = packet.ReadUShort();

                _data.MaxHP = packet.ReadUInt();
                _data.MaxMP = packet.ReadUInt();
                _data.STR = packet.ReadUShort();
                _data.INT = packet.ReadUShort();

                return _data;
            }
            catch (Exception ex)
            {
                Logger.Error("CharStats", ex.Message + ex.StackTrace);
                return null;
            }
        }
        public static async Task ParseItemMove(Packet packet, Proxy proxy)
        {
            try
            {
                byte result = packet.ReadByte();
                ItemMovement moveType = (ItemMovement)packet.ReadByte();

                if (result != 1)
                    return;

                var inv = proxy.Session!.Inventory;

                switch (moveType)
                {
                    case ItemMovement.InventoryToInventory:
                        {
                            byte sourceSlot = packet.ReadByte();
                            byte destSlot = packet.ReadByte();

                            ushort movedQty = packet.ReadUShort();
                            byte unk = packet.ReadByte();

                            bool IsEquipmentSlot(byte slot) => slot >= 1 && slot <= 12;

                            if (proxy.PendingMoves.TryGetValue(sourceSlot, out var tcs))
                            {
                                proxy.PendingMoves.Remove(sourceSlot);
                                tcs.TrySetResult(true);
                            }

                            bool sourceIsEquip = IsEquipmentSlot(sourceSlot);
                            bool destIsEquip = IsEquipmentSlot(destSlot);

                            inv.Equipment.TryGetValue(sourceSlot, out var srcEquip);
                            inv.Slots.TryGetValue(sourceSlot, out var srcInv);

                            var src = sourceIsEquip ? srcEquip : srcInv;

                            if (src == null || src.RefItemID == 0)
                                return;

                            inv.Equipment.TryGetValue(destSlot, out var dstEquip);
                            inv.Slots.TryGetValue(destSlot, out var dstInv);

                            var dst = destIsEquip ? dstEquip : dstInv;

                            bool dstExists = dst != null && dst.RefItemID != 0;

                            // STACK
                            if (!sourceIsEquip && !destIsEquip &&
                                dstExists &&
                                dst.RefItemID == src.RefItemID &&
                                dst.MaxStack > 1)
                            {
                                int total = dst.Stack + src.Stack;

                                if (total <= dst.MaxStack)
                                {
                                    inv.Slots[destSlot] = new SR_Item()
                                    {
                                        RefItemID = dst.RefItemID,
                                        CodeName128 = dst.CodeName128,
                                        Stack = total,
                                        MaxStack = dst.MaxStack,
                                    };
                                    inv.Slots.TryRemove(sourceSlot, out _);
                                }
                                else
                                {
                                    int remainder = total - dst.MaxStack;
                                    inv.Slots[destSlot] = new SR_Item()
                                    {
                                        RefItemID = dst.RefItemID,
                                        CodeName128 = dst.CodeName128,
                                        Stack = dst.MaxStack,
                                        MaxStack = dst.MaxStack,
                                    };
                                    inv.Slots[sourceSlot] = new SR_Item()
                                    {
                                        RefItemID = src.RefItemID,
                                        CodeName128 = src.CodeName128,
                                        Stack = remainder,
                                        MaxStack = src.MaxStack,
                                    };
                                }

                                return;
                            }

                            // SWAP
                            if (dstExists)
                            {
                                if (sourceIsEquip)
                                    inv.Equipment[sourceSlot] = dst;
                                else
                                    inv.Slots[sourceSlot] = dst;

                                if (destIsEquip)
                                    inv.Equipment[destSlot] = src;
                                else
                                    inv.Slots[destSlot] = src;
                            }
                            else
                            {
                                // MOVE
                                if (destIsEquip)
                                    inv.Equipment[destSlot] = src;
                                else
                                    inv.Slots[destSlot] = src;

                                if (sourceIsEquip)
                                    inv.Equipment.TryRemove(sourceSlot, out _);
                                else
                                    inv.Slots.TryRemove(sourceSlot, out _);
                            }

                            break;
                        }

                    case ItemMovement.GroundToInventory:
                        {
                            byte slotOrFlag = packet.ReadByte();
                            if (slotOrFlag == 0xFE)
                            {
                                uint gold = packet.ReadUInt();
                                Log("ItemMoveHandler", $"Picked up {gold} gold");
                                break;
                            }

                            byte slot = slotOrFlag;
                            uint rentType = packet.ReadUInt();
                            if (rentType == 1) { packet.ReadUShort(); packet.ReadUInt(); packet.ReadUInt(); }
                            else if (rentType == 2) { packet.ReadUShort(); packet.ReadUShort(); packet.ReadUInt(); }
                            else if (rentType == 3) { packet.ReadUShort(); packet.ReadUInt(); packet.ReadUInt(); packet.ReadUShort(); packet.ReadUInt(); }

                            uint refItemId = packet.ReadUInt();
                            var itemInfo = await DBConnect.GetItemRecord(refItemId);
                            if (!itemInfo.success) break;

                            ushort finalStack = 1;
                            if (itemInfo.item.T2 == 1) // Equipment
                            {
                                packet.ReadByte(); packet.ReadULong(); packet.ReadUInt();
                                byte mag = packet.ReadByte();
                                for (int p = 0; p < mag; p++) { packet.ReadUInt(); packet.ReadUInt(); }
                                packet.ReadByte(); byte sc = packet.ReadByte();
                                for (int j = 0; j < sc; j++) { packet.ReadByte(); packet.ReadUInt(); packet.ReadUInt(); }
                                packet.ReadByte(); byte ac = packet.ReadByte();
                                for (int j = 0; j < ac; j++) { packet.ReadByte(); packet.ReadUInt(); packet.ReadUInt(); }
                            }
                            else if (itemInfo.item.T2 == 3) // ETC
                            {
                                finalStack = packet.ReadUShort();
                                if (itemInfo.item.T3 == 11 && itemInfo.item.T4 == 1)
                                    packet.ReadByte();
                                else if (itemInfo.item.T3 == 14)
                                {
                                    byte cm = packet.ReadByte();
                                    for (int p = 0; p < cm; p++) { packet.ReadUInt(); packet.ReadUInt(); }
                                }
                            }

                            inv.Slots[slot] = new SR_Item()
                            {
                                RefItemID = (int)refItemId,
                                CodeName128 = itemInfo.item.CodeName,
                                Stack = finalStack,
                                MaxStack = itemInfo.item.MaxStack
                            };
                            await AchievementService.OnItemPickup(proxy.Session?.CharacterName!, itemInfo.item.CodeName, proxy);
                            Log("ItemMoveHandler", $"Picked up {itemInfo.item.CodeName} x{finalStack} → slot {slot}");
                            break;
                        }

                    case ItemMovement.ShopToInventory:
                        {
                            byte shopTab = packet.ReadByte();
                            byte shopSlot = packet.ReadByte();
                            byte slotCount = packet.ReadByte();
                            var toSlots = new List<byte>();
                            for (int i = 0; i < slotCount; i++)
                                toSlots.Add(packet.ReadByte());
                            ushort quantity = packet.ReadUShort();
                            packet.ReadUInt(); // recipientNpcId

                            uint npcObjId = 0;
                            if (proxy.Session!.SpawnedObjects.TryGetValue(proxy.Session!.LastTargetUID, out var spawnInfo))
                                npcObjId = spawnInfo.RefObjID;

                            if (Overseer.ShopLookup.TryGetValue(((int)npcObjId, shopTab, shopSlot), out var shopItem))
                            {
                                var itemInfo = await DBConnect.GetItemRecord((uint)shopItem.RefItemID);
                                int maxStack = itemInfo.success ? itemInfo.item!.MaxStack : 1;

                                foreach (var slot in toSlots)
                                    inv.Slots[slot] = new SR_Item()
                                    {
                                        RefItemID = (int)shopItem.RefItemID,
                                        CodeName128 = itemInfo.item!.CodeName,
                                        Stack = quantity,
                                        MaxStack = itemInfo.item.MaxStack
                                    };

                                Log("ItemMoveHandler", $"Bought {shopItem.CodeName} x{quantity} → slots [{string.Join(",", toSlots)}]");
                            }
                            else
                            {
                                Logger.Warn("ItemMoveHandler", $"BUY: unresolved NPC=0x{proxy.Session!.LastTargetUID:X} objId={npcObjId} tab={shopTab} slot={shopSlot}");
                            }
                            break;
                        }

                    case ItemMovement.InventoryToShop:
                        {
                            byte playerSlot = packet.ReadByte();
                            ushort quantity = packet.ReadUShort();
                            uint goldReceived = packet.ReadUInt();

                            if (inv.Slots.TryGetValue(playerSlot, out var item))
                            {
                                int remaining = item.Stack - quantity;
                                if (remaining <= 0)
                                {
                                    inv.Slots.TryRemove(playerSlot, out _);
                                    Log("ItemMoveHandler", $"Sold {item.CodeName128} x{quantity} from slot {playerSlot} for {goldReceived}g (removed)");
                                }
                                else
                                {
                                    inv.Slots[playerSlot] = new SR_Item()
                                    {
                                        RefItemID = item.RefItemID,
                                        CodeName128 = item.CodeName128,
                                        Stack = remaining,
                                        MaxStack = item.MaxStack
                                    };
                                    Log("ItemMoveHandler", $"Sold {item.CodeName128} x{quantity} from slot {playerSlot} for {goldReceived}g ({remaining} remain)");
                                }
                            }
                            break;
                        }

                    case ItemMovement.GroundToPet:
                        {
                            uint petUID = packet.ReadUInt();
                            byte slot = packet.ReadByte();
                            uint rentType = packet.ReadUInt();
                            if (rentType == 1) { packet.ReadUShort(); packet.ReadUInt(); packet.ReadUInt(); }
                            else if (rentType == 2) { packet.ReadUShort(); packet.ReadUShort(); packet.ReadUInt(); }
                            else if (rentType == 3) { packet.ReadUShort(); packet.ReadUInt(); packet.ReadUInt(); packet.ReadUShort(); packet.ReadUInt(); }

                            uint refItemId = packet.ReadUInt();
                            var itemInfo = await DBConnect.GetItemRecord(refItemId);
                            if (!itemInfo.success) break;

                            ushort finalStack = 1;
                            if (itemInfo.item.T2 == 1)
                            {
                                packet.ReadByte(); packet.ReadULong(); packet.ReadUInt();
                                byte mag = packet.ReadByte();
                                for (int p = 0; p < mag; p++) { packet.ReadUInt(); packet.ReadUInt(); }
                                packet.ReadByte(); byte sc = packet.ReadByte();
                                for (int j = 0; j < sc; j++) { packet.ReadByte(); packet.ReadUInt(); packet.ReadUInt(); }
                                packet.ReadByte(); byte ac = packet.ReadByte();
                                for (int j = 0; j < ac; j++) { packet.ReadByte(); packet.ReadUInt(); packet.ReadUInt(); }
                            }
                            else if (itemInfo.item.T2 == 3)
                            {
                                finalStack = packet.ReadUShort();
                                if (itemInfo.item.T3 == 11 && itemInfo.item.T4 == 1)
                                    packet.ReadByte();
                                else if (itemInfo.item.T3 == 14)
                                {
                                    byte cm = packet.ReadByte();
                                    for (int p = 0; p < cm; p++) { packet.ReadUInt(); packet.ReadUInt(); }
                                }
                            }
                            if (itemInfo.item.CodeName.Contains("SNOWFLAKE")) break;

                            if (itemInfo.item.T2 == 3 && itemInfo.item.T3 == 12)
                            {
                                inv.Slots[slot] = new SR_Item()
                                {
                                    RefItemID = (int)refItemId,
                                    CodeName128 = itemInfo.item.CodeName,
                                    Stack = finalStack,
                                    MaxStack = itemInfo.item.MaxStack
                                };
                                Log("ItemMoveHandler", $"Pet picked up quest item {itemInfo.item.CodeName} x{finalStack} → PLAYER slot {slot}");
                            }
                            else if (itemInfo.item.T2 == 3 && itemInfo.item.T3 == 9)
                            {
                                inv.Slots[slot] = new SR_Item()
                                {
                                    RefItemID = (int)refItemId,
                                    CodeName128 = itemInfo.item.CodeName,
                                    Stack = finalStack,
                                    MaxStack = itemInfo.item.MaxStack
                                };

                                Log("ItemMoveHandler", $"Pet picked up event item {itemInfo.item.CodeName} x{finalStack} → PLAYER slot {slot}");
                                await AchievementService.OnItemPickup(proxy.Session?.CharacterName!, itemInfo.item.CodeName, proxy);
                            }
                            else
                            {
                                if (!inv.Pets.ContainsKey(petUID))
                                    inv.Pets[petUID] = new();

                                inv.Pets[petUID].Inventory[slot] = new SR_Item()
                                {
                                    RefItemID = (int)refItemId,
                                    CodeName128 = itemInfo.item.CodeName,
                                    Stack = finalStack,
                                    MaxStack = itemInfo.item.MaxStack
                                };
                                await AchievementService.OnItemPickup(proxy.Session?.CharacterName!, itemInfo.item.CodeName, proxy);
                                Log("ItemMoveHandler", $"Pet picked up {itemInfo.item.CodeName} x{finalStack} → pet 0x{petUID:X} slot {slot}");
                            }
                            break;
                        }

                    case ItemMovement.GroundToPetToInventory:
                        {
                            uint petUID = packet.ReadUInt();
                            byte slotOrFlag = packet.ReadByte();

                            if (slotOrFlag == 0xFE)
                            {
                                uint gold = packet.ReadUInt();
                                Log("ItemMoveHandler", $"Pet picked up {gold} gold");
                                break;
                            }

                            byte slot = slotOrFlag;
                            uint rentType = packet.ReadUInt();
                            if (rentType == 1) { packet.ReadUShort(); packet.ReadUInt(); packet.ReadUInt(); }
                            else if (rentType == 2) { packet.ReadUShort(); packet.ReadUShort(); packet.ReadUInt(); }
                            else if (rentType == 3) { packet.ReadUShort(); packet.ReadUInt(); packet.ReadUInt(); packet.ReadUShort(); packet.ReadUInt(); }

                            uint refItemId = packet.ReadUInt();
                            var itemInfo = await DBConnect.GetItemRecord(refItemId);
                            if (!itemInfo.success) break;

                            ushort finalStack = 1;
                            if (itemInfo.item.T2 == 3)
                            {
                                finalStack = packet.ReadUShort();
                                if (itemInfo.item.T3 == 11 && itemInfo.item.T4 == 1)
                                    packet.ReadByte();
                                else if (itemInfo.item.T3 == 14)
                                {
                                    byte cm = packet.ReadByte();
                                    for (int p = 0; p < cm; p++) { packet.ReadUInt(); packet.ReadUInt(); }
                                }
                            }

                            bool isQuestItem = itemInfo.item.CodeName.Contains("SNOWFLAKE") ||
                                               itemInfo.item.CodeName.Contains("QNO") ||
                                               itemInfo.item.CodeName.Contains("QSP");

                            if (isQuestItem)
                            {
                                inv.Slots[slot] = new SR_Item()
                                {
                                    RefItemID = (int)refItemId,
                                    CodeName128 = itemInfo.item.CodeName,
                                    Stack = finalStack,
                                    MaxStack = itemInfo.item.MaxStack
                                };
                                Log("ItemMoveHandler", $"Pet picked up quest item {itemInfo.item.CodeName} x{finalStack} → PLAYER slot {slot}");
                            }
                            else
                            {
                                if (!inv.Pets.ContainsKey(petUID))
                                    inv.Pets[petUID] = new();

                                inv.Pets[petUID].Inventory[slot] = new SR_Item()
                                {
                                    RefItemID = (int)refItemId,
                                    CodeName128 = itemInfo.item.CodeName,
                                    Stack = finalStack,
                                    MaxStack = itemInfo.item.MaxStack
                                };
                                await AchievementService.OnItemPickup(proxy.Session?.CharacterName!, itemInfo.item.CodeName, proxy);
                                Log("ItemMoveHandler", $"Pet picked up {itemInfo.item.CodeName} x{finalStack} → pet 0x{petUID:X} slot {slot}");
                            }

                            break;
                        }

                    case ItemMovement.PetToInventory:
                        {
                            uint petUID = packet.ReadUInt();
                            byte petSlot = packet.ReadByte();
                            byte playerSlot = packet.ReadByte();

                            bool found = false;
                            if (inv.Pets.TryGetValue(petUID, out var petInv) &&
                                petInv.Inventory.TryGetValue(petSlot, out var petItem))
                            {
                                inv.Slots[playerSlot] = petItem;
                                petInv.Inventory.TryRemove(petSlot, out _);
                                found = true;
                                Log("ItemMoveHandler", $"Pet→Player: {petItem.CodeName128} x{petItem.Stack} → slot {playerSlot}");
                            }

                            if (!found)
                            {
                                foreach (var pet in inv.Pets)
                                {
                                    if (pet.Value.Inventory.TryGetValue(petSlot, out var item))
                                    {
                                        inv.Slots[playerSlot] = item;
                                        pet.Value.Inventory.TryRemove(petSlot, out _);
                                        found = true;
                                        Log("ItemMoveHandler", $"Pet→Player: {item.CodeName128} x{item.Stack} → slot {playerSlot} (from pet 0x{pet.Key:X})");
                                        break;
                                    }
                                }
                            }

                            if (!found)
                            {
                                inv.Slots[playerSlot] = new SR_Item()
                                {
                                    RefItemID = 0,
                                    CodeName128 = "UNKNOWN_PET_TRANSFER",
                                    Stack = 1,
                                    MaxStack = 1
                                };
                                Logger.Warn("ItemMoveHandler", $"Pet→Player: unknown item from pet 0x{petUID:X} slot {petSlot}");
                            }
                            break;
                        }

                    case ItemMovement.InventoryToPet:
                        {
                            uint petUID = packet.ReadUInt();
                            byte playerSlot = packet.ReadByte();
                            byte petSlot = packet.ReadByte();

                            if (inv.Slots.TryGetValue(playerSlot, out var item))
                            {
                                uint targetPet = petUID;
                                if (!inv.Pets.ContainsKey(petUID))
                                {
                                    foreach (var pet in inv.Pets)
                                    {
                                        if (pet.Value.Inventory.Keys.Any(k => Math.Abs(k - petSlot) < 28))
                                        {
                                            targetPet = pet.Key;
                                            break;
                                        }
                                    }
                                }

                                if (!inv.Pets.ContainsKey(targetPet))
                                    inv.Pets[targetPet] = new();
                                inv.Pets[targetPet].Inventory[petSlot] = item;
                                inv.Slots.TryRemove(playerSlot, out _);
                                Log("ItemMoveHandler", $"Player→Pet: {item.CodeName128} → pet 0x{targetPet:X} slot {petSlot}");
                            }
                            break;
                        }

                    case ItemMovement.PetToPet:
                        {
                            uint petUID = packet.ReadUInt();
                            byte srcSlot = (byte)(packet.ReadByte() + 1);
                            byte destSlot = (byte)(packet.ReadByte() + 1);
                            ushort qty = packet.ReadUShort();

                            if (proxy.PendingMoves.TryGetValue(srcSlot, out var tcs))
                            {
                                proxy.PendingMoves.Remove(srcSlot);
                                tcs.TrySetResult(true);
                            }

                            ConcurrentDictionary<byte, SR_Item>? petInvSlots = null;
                            foreach (var pet in inv.Pets)
                            {
                                if (pet.Value.Inventory.ContainsKey(srcSlot))
                                {
                                    petInvSlots = pet.Value.Inventory;
                                    break;
                                }
                            }
                            if (petInvSlots == null && inv.Pets.TryGetValue(petUID, out var directPet))
                                petInvSlots = directPet.Inventory;

                            if (petInvSlots != null && petInvSlots.TryGetValue(srcSlot, out var src))
                            {
                                petInvSlots.TryGetValue(destSlot, out var dst);

                                if (dst.RefItemID != 0 && dst.RefItemID == src.RefItemID && dst.MaxStack > 1)
                                {
                                    int total = dst.Stack + qty;
                                    if (total <= dst.MaxStack)
                                    {
                                        petInvSlots[destSlot] = new SR_Item()
                                        {
                                            RefItemID = dst.RefItemID,
                                            CodeName128 = dst.CodeName128,
                                            Stack = total,
                                            MaxStack = dst.MaxStack
                                        };
                                        petInvSlots.TryRemove(srcSlot, out _);
                                    }
                                    else
                                    {
                                        int remainder = total - dst.MaxStack;
                                        petInvSlots[destSlot] = new SR_Item()
                                        {
                                            RefItemID = dst.RefItemID,
                                            CodeName128 = dst.CodeName128,
                                            Stack = dst.MaxStack,
                                            MaxStack = dst.MaxStack
                                        };
                                        petInvSlots[srcSlot] = new SR_Item()
                                        {
                                            RefItemID = src.RefItemID,
                                            CodeName128 = src.CodeName128,
                                            Stack = remainder,
                                            MaxStack = src.MaxStack
                                        };
                                    }
                                }
                                else if (dst.RefItemID != 0)
                                {
                                    petInvSlots[destSlot] = src;
                                    petInvSlots[srcSlot] = dst;
                                }
                                else
                                {
                                    petInvSlots[destSlot] = src;
                                    petInvSlots.TryRemove(srcSlot, out _);
                                }
                            }
                            break;
                        }

                    case ItemMovement.AvatarToInventory:
                        {
                            byte avatarSlot = packet.ReadByte();
                            byte playerSlot = packet.ReadByte();
                            ushort qty = packet.ReadUShort();
                            byte unk = packet.ReadByte();

                            if (inv.Avatars.TryGetValue(avatarSlot, out var avatarItem))
                            {
                                inv.Slots[playerSlot] = avatarItem;
                                inv.Avatars.TryRemove(avatarSlot, out _);
                                Log("ItemMoveHandler", $"Unequipped avatar {avatarItem.CodeName128} slot {avatarSlot} → inventory slot {playerSlot}");
                            }
                            break;
                        }

                    case ItemMovement.InventoryToAvatar:
                        {
                            byte playerSlot = packet.ReadByte();
                            byte avatarSlot = packet.ReadByte();
                            ushort qty = packet.ReadUShort();
                            byte unk = packet.ReadByte();

                            if (inv.Slots.TryGetValue(playerSlot, out var item))
                            {
                                if (inv.Avatars.TryGetValue(avatarSlot, out var oldAvatar))
                                    inv.Slots[playerSlot] = oldAvatar;
                                else
                                    inv.Slots.TryRemove(playerSlot, out _);

                                inv.Avatars[avatarSlot] = item;
                                Log("ItemMoveHandler", $"Equipped avatar {item.CodeName128} slot {playerSlot} → avatar slot {avatarSlot}");
                            }
                            break;
                        }

                    case ItemMovement.InventoryToStorage:
                        {
                            byte playerSlot = packet.ReadByte();
                            byte storageSlot = packet.ReadByte();

                            if (inv.Slots.TryGetValue(playerSlot, out var item))
                            {
                                inv.Storage[storageSlot] = item;
                                inv.Slots.TryRemove(playerSlot, out _);
                                Log("ItemMoveHandler", $"Deposited {item.CodeName128} slot {playerSlot} → storage slot {storageSlot}");
                            }
                            break;
                        }

                    case ItemMovement.StorageToInventory:
                        {
                            byte storageSlot = packet.ReadByte();
                            byte playerSlot = packet.ReadByte();

                            if (inv.Storage.TryGetValue(storageSlot, out var item))
                            {
                                inv.Slots[playerSlot] = item;
                                inv.Storage.TryRemove(storageSlot, out _);
                                Log("ItemMoveHandler", $"Withdrew {item.CodeName128} storage slot {storageSlot} → slot {playerSlot}");
                            }
                            else
                            {
                                Logger.Warn("ItemMoveHandler", $"Withdrew unknown item from storage slot {storageSlot} → slot {playerSlot}");
                            }
                            break;
                        }

                    case ItemMovement.StorageToStorage:
                        {
                            byte srcSlot = packet.ReadByte();
                            byte destSlot = packet.ReadByte();
                            ushort qty = packet.ReadUShort();

                            if (inv.Storage.TryGetValue(srcSlot, out var src))
                            {
                                inv.Storage.TryGetValue(destSlot, out var dst);

                                if (dst.RefItemID != 0)
                                {
                                    inv.Storage[destSlot] = src;
                                    inv.Storage[srcSlot] = dst;
                                }
                                else
                                {
                                    inv.Storage[destSlot] = src;
                                    inv.Storage.TryRemove(srcSlot, out _);
                                }
                                Log("ItemMoveHandler", $"Storage move: {src.CodeName128} slot {srcSlot} → slot {destSlot}");
                            }
                            break;
                        }

                    case ItemMovement.InventoryToGround:
                        {
                            byte playerSlot = packet.ReadByte();

                            if (inv.Slots.TryGetValue(playerSlot, out var item))
                            {
                                inv.Slots.TryRemove(playerSlot, out _);
                                Log("ItemMoveHandler", $"Dropped {item.CodeName128} from slot {playerSlot}");
                            }
                            break;
                        }

                    case ItemMovement.ItemMallToInventory:
                        {
                            ushort shopGroup = packet.ReadUShort();
                            byte mallTab = packet.ReadByte();
                            byte mallSlot = packet.ReadByte();
                            byte mallItemInfo = packet.ReadByte();
                            byte slotCount = packet.ReadByte();

                            var toSlots = new List<byte>();
                            for (int i = 0; i < slotCount; i++)
                                toSlots.Add(packet.ReadByte());

                            ushort quantity = packet.ReadUShort();

                            Log("ItemMoveHandler",
                                $"Mall buy: tab={mallTab} slot={mallSlot} qty={quantity} → slots [{string.Join(",", toSlots)}]");
                            if (Overseer.MallLookup.TryGetValue((mallTab, mallSlot), out var item))
                            {
                                foreach (var slot in toSlots)
                                {
                                    inv.Slots[slot] = new SR_Item()
                                    {
                                        RefItemID = item.ID,
                                        CodeName128 = item.CodeName,
                                        Stack = quantity,
                                        MaxStack = item.MaxStack
                                    };
                                }
                            }
                            else
                            {
                                Logger.Warn("ItemMoveHandler",
                                    $"MALL unresolved tabID={mallTab} slot={mallSlot}");
                            }
                            break;
                        }

                    case ItemMovement.GameServerToInventory:
                        {
                            ushort slot = packet.ReadUShort();
                            uint padding = packet.ReadUInt();
                            uint itemObjId = packet.ReadUInt();
                            byte quantity = packet.ReadByte();

                            var res = await DBConnect.GetItemRecord(itemObjId);
                            if (res.success)
                            {
                                inv.Slots[(byte)slot] = new SR_Item()
                                {
                                    RefItemID = (int)itemObjId,
                                    CodeName128 = res.item!.CodeName,
                                    Stack = quantity,
                                    MaxStack = res.item.MaxStack
                                };

                                Log("GameServerToInventory:ItemMoveHandler",
                                    $"GS->Inv move slot={slot}, uid={itemObjId}, qty={quantity}, name={res.item.CodeName}");
                            }
                            else
                            {
                                Logger.Warn("GameServerToInventory:ItemMoveHandler",
                                    $"Item record failed for uid {itemObjId} (slot {slot})");

                                inv.Slots[(byte)slot] = new SR_Item()
                                {
                                    RefItemID = (int)itemObjId,
                                    CodeName128 = $"UNKNOWN_{itemObjId}",
                                    Stack = quantity,
                                    MaxStack = 500
                                };
                            }
                            break;
                        }

                    case ItemMovement.InventoryToGameServer:
                        {
                            byte playerSlot = packet.ReadByte();

                            if (inv.Slots.TryRemove(playerSlot, out var item))
                            {
                                Log("ItemMoveHandler",
                                    $"I→G move: {item.CodeName128} slot={playerSlot}");
                            }

                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::ItemMove", ex.Message);
            }
        }
        public static ItemUseReturn? ParseItemUse(Packet packet)
        {
            try
            {
                byte result = packet.ReadByte();
                byte slot = packet.ReadByte();
                if (result != 1)
                    return new ItemUseReturn { Result = result, Slot = slot };
                ushort remainingStack = packet.ReadUShort();
                packet.ReadUShort(); // unk
                return new ItemUseReturn { Result = result, Slot = slot, RemainingStack = remainingStack };
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::ItemUse", ex.Message);
                return null;
            }
        }
        public static async Task<CosSpawnReturn?> ParseCOSSpawn(Packet packet)
        {
            try
            {
                uint petUID = packet.ReadUInt();
                uint refObjID = packet.ReadUInt();
                packet.ReadUInt(); // stat1
                packet.ReadUInt(); // stat2
                packet.ReadUInt(); // stat3

                var petObjInfo = await DBConnect.GetItemRecord(refObjID);
                if (!petObjInfo.success)
                {
                    Logger.Warn("CosSpawn", $"Unknown refObjID 0x{refObjID:X} for pet 0x{petUID:X}");
                    return null;
                }

                bool isAttackPet = petObjInfo.item!.T4 == 3;
                Log("CosSpawn", $"Pet 0x{petUID:X} refObjID=0x{refObjID:X} T4={petObjInfo.item.T4} isAttack={isAttackPet} remaining={packet.RemainingRead()}");

                Pet pet;

                if (isAttackPet)
                {
                    packet.ReadUInt();
                    packet.ReadByte();
                    packet.ReadUInt();
                    packet.ReadUShort();
                    string attackPetName = packet.ReadAscii();
                    packet.ReadByte();

                    Log("CosSpawn", $"Attack pet 0x{petUID:X} name='{attackPetName}'");

                    pet = new Pet
                    {
                        Uid = petUID,
                        Info = new PetInfo
                        {
                            Name = string.IsNullOrEmpty(attackPetName) ? "No name" : attackPetName,
                            IsAttackPet = true,
                            CodeName = petObjInfo.item.CodeName,
                            ReadableName = GameObjectNameResolver.Resolve(petObjInfo.item.CodeName)
                        },
                        Inventory = new ConcurrentDictionary<byte, SR_Item>()
                    };

                    return new CosSpawnReturn { PetUID = petUID, Pet = pet, IsAttackPet = true };
                }

                string petName = packet.ReadAscii();
                byte invSize = packet.ReadByte();
                byte itemCount = packet.ReadByte();

                Log("CosSpawn", $"Pet 0x{petUID:X} name='{(string.IsNullOrEmpty(petName) ? "No name" : petName)}' invSize={invSize} itemCount={itemCount}");

                var petSlots = new ConcurrentDictionary<byte, SR_Item>();

                for (int i = 0; i < itemCount; i++)
                {
                    byte indexByte = packet.ReadByte();
                    packet.ReadUInt();
                    uint refItemId = packet.ReadUInt();
                    byte slot = (byte)(indexByte + 1);

                    var itemInfoResult = await DBConnect.GetItemRecord(refItemId);
                    if (!itemInfoResult.success)
                    {
                        Logger.Warn("CosSpawn", $"Unknown item refObjID 0x{refItemId:X} at slot {slot}");
                        break;
                    }

                    var item = itemInfoResult.item;
                    ushort finalStack = 1;

                    if (item!.T2 == 3)
                    {
                        finalStack = packet.ReadUShort();
                        if (item.CodeName.Contains("ATTRSTONE") || item.CodeName.Contains("MAGICSTONE"))
                            packet.ReadByte();
                    }
                    else if (item.T2 == 1)
                    {
                        packet.ReadByte(); packet.ReadULong(); packet.ReadUInt();
                        byte magCount = packet.ReadByte();
                        for (int m = 0; m < magCount; m++) { packet.ReadUInt(); packet.ReadUInt(); }
                        packet.ReadByte();
                        byte scCount = packet.ReadByte();
                        for (int s = 0; s < scCount; s++) { packet.ReadByte(); packet.ReadUInt(); packet.ReadUInt(); }
                        packet.ReadByte();
                        byte acCount = packet.ReadByte();
                        for (int a = 0; a < acCount; a++) { packet.ReadByte(); packet.ReadUInt(); packet.ReadUInt(); }
                    }
                    else
                    {
                        Logger.Warn("CosSpawn", $"Unhandled T2={item.T2} for item 0x{refItemId:X} ({item.CodeName}) at slot {slot}");
                        break;
                    }

                    petSlots[slot] = new SR_Item()
                    {
                        RefItemID = (int)refItemId,
                        CodeName128 = item.CodeName,
                        Stack = finalStack,
                        MaxStack = item.MaxStack
                    };
                    Log("CosSpawn", $"Pet 0x{petUID:X} Slot [{slot}] {item.CodeName} ({finalStack}/{item.MaxStack})");
                }

                packet.ReadUInt(); // linked UID
                packet.ReadByte(); // unknown

                pet = new Pet
                {
                    Uid = petUID,
                    Info = new PetInfo
                    {
                        Name = string.IsNullOrEmpty(petName) ? "No name" : petName,
                        IsAttackPet = false,
                        CodeName = petObjInfo.item.CodeName,
                        ReadableName = GameObjectNameResolver.Resolve(petObjInfo.item.CodeName)
                    },
                    Inventory = petSlots
                };

                Log("CosSpawn", $"Successfully parsed pet 0x{petUID:X} with {petSlots.Count} items");
                return new CosSpawnReturn { PetUID = petUID, Pet = pet, IsAttackPet = false };
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::COSSpawn", ex.Message + ex.StackTrace);
                return null;
            }
        }
        public static CosDespawnReturn? ParseCOSDespawn(Packet packet)
        {
            try
            {
                uint petUID = packet.ReadUInt();
                byte type = packet.ReadByte();

                if (type == 1)
                    return new CosDespawnReturn { PetUID = petUID, Type = 1 };

                if (type == 2 && packet.RemainingRead() == 0)
                    return new CosDespawnReturn { PetUID = petUID, Type = 2, HasInventoryPage = false };

                if (type != 2)
                    return new CosDespawnReturn { PetUID = petUID, Type = type, HasInventoryPage = false };

                // type == 2 with remaining bytes = inventory page
                packet.ReadByte();              // 0x70 — unknown
                byte pageItemCount = packet.ReadByte();
                Log("CosSpawn", $"0x30C9 inventory page for pet 0x{petUID:X} | pageItemCount={pageItemCount} remaining={packet.RemainingRead()}");
                return new CosDespawnReturn { PetUID = petUID, Type = 2, HasInventoryPage = true, PageItemCount = pageItemCount };
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::COSDespawn", ex.Message);
                return null;
            }
        }
        public static GoldUpdateReturn? ParseNewGoldAmount(Packet packet)
        {
            try
            {
                int size = packet.RemainingRead();
                byte flag = packet.ReadByte();

                if (size == 10)
                {
                    if (flag != 0x01) return null;
                    ulong gold = packet.ReadULong();
                    packet.ReadByte();
                    return new GoldUpdateReturn { Flag = flag, Gold = gold };
                }
                else if (size == 6)
                {
                    uint value = packet.ReadUInt();
                    packet.ReadByte();
                    return new GoldUpdateReturn { Flag = flag, SkillPoints = value };
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::NewGoldAmount", ex.Message);
                return null;
            }
        }
        public static HPMPUpdateReturn? ParseHPMPUpdate(Packet packet)
        {
            try
            {
                uint targetUID = packet.ReadUInt();
                packet.ReadByte(); // flags
                packet.ReadByte(); // unk
                byte statType = packet.ReadByte();
                uint value = packet.ReadUInt();
                return new HPMPUpdateReturn { TargetUID = targetUID, StatType = statType, Value = value };
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::HPMPUpdate", ex.Message);
                return null;
            }
        }
        public static async Task<StorageReturn?> ParseStorageItems(Packet packet)
        {
            try
            {
                var result = new StorageReturn();

                byte storageSize = packet.ReadByte();
                byte itemCount = packet.ReadByte();

                Log("StorageHandler", $"Storage opened: size={storageSize} items={itemCount}");

                for (int i = 0; i < itemCount; i++)
                {
                    byte slot = packet.ReadByte();
                    uint rentType = packet.ReadUInt();
                    if (rentType == 1) { packet.ReadUShort(); packet.ReadUInt(); packet.ReadUInt(); }
                    else if (rentType == 2) { packet.ReadUShort(); packet.ReadUShort(); packet.ReadUInt(); }
                    else if (rentType == 3) { packet.ReadUShort(); packet.ReadUInt(); packet.ReadUInt(); packet.ReadUShort(); packet.ReadUInt(); }

                    uint refItemId = packet.ReadUInt();
                    var itemInfo = await DBConnect.GetItemRecord(refItemId);

                    if (!itemInfo.success)
                    {
                        Logger.Warn("StorageHandler", $"DB MISS: refItemId={refItemId} slot={slot}");
                        packet.ReadUShort();
                        continue;
                    }

                    ushort finalStack = 1;

                    if (itemInfo.item.T1 == 3)
                    {
                        if (itemInfo.item.T2 == 1) // Equipment
                        {
                            packet.ReadByte(); packet.ReadULong(); packet.ReadUInt();
                            byte mag = packet.ReadByte();
                            for (int p = 0; p < mag; p++) { packet.ReadUInt(); packet.ReadUInt(); }
                            packet.ReadByte(); byte sc = packet.ReadByte();
                            for (int j = 0; j < sc; j++) { packet.ReadByte(); packet.ReadUInt(); packet.ReadUInt(); }
                            packet.ReadByte(); byte ac = packet.ReadByte();
                            for (int j = 0; j < ac; j++) { packet.ReadByte(); packet.ReadUInt(); packet.ReadUInt(); }
                        }
                        else if (itemInfo.item.T2 == 3) // ETC
                        {
                            finalStack = packet.ReadUShort();
                            if (itemInfo.item.T3 == 11 && itemInfo.item.T4 == 1)
                                packet.ReadByte();
                            else if (itemInfo.item.T3 == 14)
                            {
                                byte cm = packet.ReadByte();
                                for (int p = 0; p < cm; p++) { packet.ReadUInt(); packet.ReadUInt(); }
                            }
                        }
                    }

                    result.Storage[slot] = new SR_Item()
                    {
                        RefItemID = (int)refItemId,
                        CodeName128 = itemInfo.item.CodeName,
                        Stack = finalStack,
                        MaxStack = itemInfo.item.MaxStack
                    };
                    Log("StorageHandler", $"Storage [{slot}] {itemInfo.item.CodeName} ({finalStack}/{itemInfo.item.MaxStack})");
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::StorageItems", ex.Message + ex.StackTrace);
                return null;
            }
        }
        public static ExpReturn? ParseEXP(Packet packet)
        {
            try
            {
                uint mobUID = packet.ReadUInt();
                ulong exp = packet.ReadULong();
                ulong sExp = packet.ReadULong();
                return new ExpReturn { MobUID = mobUID, Exp = exp, SExp = sExp };
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::EXP", ex.Message);
                return null;
            }
        }
        public static STRINTUpdateReturn? ParseSTRUpdate(Packet packet)
        {
            try
            {
                return new STRINTUpdateReturn { Result = packet.ReadByte() };
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::STRUpdate", ex.Message);
                return null;
            }
        }
        public static STRINTUpdateReturn? ParseINTUpdate(Packet packet)
        {
            try
            {
                return new STRINTUpdateReturn { Result = packet.ReadByte() };
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::INTUpdate", ex.Message);
                return null;
            }
        }
        public static MovementReturn? ParseServerMovement(Packet packet)
        {
            try
            {
                uint uniqueId = packet.ReadUInt();
                bool hasMovement = packet.ReadByte() == 1;

                if (!hasMovement)
                    return new MovementReturn { UniqueId = uniqueId, HasMovement = false };

                ushort regionId = packet.ReadUShort();
                bool isDungeon = (regionId & 0x8000) != 0;

                uint rawX, rawY;
                int rawZ;
                if (isDungeon)
                {
                    rawX = packet.ReadUInt();
                    rawZ = packet.ReadInt();
                    rawY = packet.ReadUInt();
                }
                else
                {
                    rawX = packet.ReadUShort();
                    rawZ = (int)packet.ReadShort();
                    rawY = packet.ReadUShort();
                }

                bool hasDestination = packet.ReadByte() == 1;
                if (hasDestination)
                {
                    packet.ReadUShort();
                    packet.ReadUShort();
                    packet.ReadUShort();
                    packet.ReadUShort();
                }

                byte sx = (byte)(regionId & 0xFF);
                byte sy = (byte)((regionId >> 8) & 0xFF);

                var (botPos, outSX, outSY) = BotPosition.FromRawOffsets(sx, sy, (float)(int)rawX, (float)(int)rawY, rawZ);

                return new MovementReturn
                {
                    UniqueId = uniqueId,
                    HasMovement = true,
                    RegionId = regionId,
                    WorldX = (int)botPos.X,
                    WorldY = (int)botPos.Y,
                    WorldZ = rawZ,
                    SectorX = outSX,
                    SectorY = outSY,
                    RawX = (int)rawX,
                    RawY = (int)rawY
                };
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::ServerMovement", ex.Message);
                return null;
            }
        }
        public static SkillUpdateReturn? ParseSkillUpdate(Packet packet)
        {
            try
            {
                byte success = packet.ReadByte();
                if (success != 0x01)
                    return new SkillUpdateReturn { Success = false };
                uint skillId = packet.ReadUInt();
                return new SkillUpdateReturn { Success = true, SkillId = skillId };
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::SkillUpdate", ex.Message);
                return null;
            }
        }
        public static BuffStartReturn? ParseBuffStart(Packet packet)
        {
            try
            {
                uint targetUID = packet.ReadUInt();
                uint refSkillId = packet.ReadUInt();
                uint timedJobId = packet.ReadUInt();
                return new BuffStartReturn { TargetUID = targetUID, RefSkillId = refSkillId, TimedJobId = timedJobId };
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::BuffStart", ex.Message);
                return null;
            }
        }
        public static BuffEndReturn? ParseBuffEnd(Packet packet)
        {
            try
            {
                packet.ReadByte(); // unk
                uint timedJobId = packet.ReadUInt();
                return new BuffEndReturn { TimedJobId = timedJobId };
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::BuffEnd", ex.Message);
                return null;
            }
        }
        public static SkillAttackReturn? ParseSkillAttack(Packet packet)
        {
            try
            {
                byte type = packet.ReadByte();
                byte status = packet.ReadByte();
                return new SkillAttackReturn { Type = type, Status = status };
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::SkillAttack", ex.Message);
                return null;
            }
        }
        public static AttackReturn? ParseAttack(Packet packet)
        {
            try
            {
                if (packet.RemainingRead() < 19)
                    return new AttackReturn { IsShortVariant = true };

                packet.ReadByte(); packet.ReadByte(); packet.ReadByte();
                packet.ReadUInt();
                uint attackerUID = packet.ReadUInt();
                packet.ReadUInt();
                uint targetUID = packet.ReadUInt();
                return new AttackReturn { IsShortVariant = false, AttackerUID = attackerUID, TargetUID = targetUID };
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::Attack", ex.Message);
                return null;
            }
        }

        

        #region - Helpers + Sorting -

        public static async Task<ConcurrentDictionary<byte, SR_Item>?> ParseCosPage(Packet packet, byte itemCount)
        {
            try
            {
                var result = new ConcurrentDictionary<byte, SR_Item>();
                for (int i = 0; i < itemCount; i++)
                {
                    // header - 1 (index) + 4 (padding) + 1 (unknown) + 4 (refItemId) = 10 bytes
                    if (packet.RemainingRead() < 10)
                    {
                        Logger.Warn("CosSpawn", $"0x30C9 packet truncated at item {i}/{itemCount} ({packet.RemainingRead()} bytes left)");
                        return result.Count > 0 ? result : null;
                    }

                    byte indexByte = packet.ReadByte();
                    packet.ReadUInt(); // padding / rent-type field
                    packet.ReadByte(); // extra unknown byte
                    uint refItemId = packet.ReadUInt();
                    byte slot = (byte)(indexByte + 1);

                    var itemInfoResult = await DBConnect.GetItemRecord(refItemId);
                    if (!itemInfoResult.success)
                    {
                        Logger.Warn("CosSpawn", $"0x30C9 unknown refItemId=0x{refItemId:X} at item {i}/{itemCount} slot={slot} — aborting page parse");
                        return result.Count > 0 ? result : null;
                    }

                    var item = itemInfoResult.item;
                    ushort finalStack = 1;

                    if (item.T2 == 3)
                    {
                        finalStack = packet.ReadUShort();
                        if (finalStack == 0) finalStack = 1;
                        if (item.CodeName.Contains("ATTRSTONE"))
                            packet.ReadByte();
                    }
                    else if (item.T2 == 1)
                    {
                        packet.ReadByte(); packet.ReadULong(); packet.ReadUInt();
                        byte magCount = packet.ReadByte();
                        for (int m = 0; m < magCount; m++) { packet.ReadUInt(); packet.ReadUInt(); }
                        packet.ReadByte();
                        byte scCount = packet.ReadByte();
                        for (int s = 0; s < scCount; s++) { packet.ReadByte(); packet.ReadUInt(); packet.ReadUInt(); }
                        packet.ReadByte();
                        byte acCount = packet.ReadByte();
                        for (int a = 0; a < acCount; a++) { packet.ReadByte(); packet.ReadUInt(); packet.ReadUInt(); }
                    }
                    else if (item.T2 == 2)
                    {
                        // COS/attack pet item - no additional bytes beyond the header
                        finalStack = 1;
                    }
                    else
                    {
                        Logger.Warn("CosSpawn", $"0x30C9 unhandled T2={item.T2} for {item.CodeName} at slot {slot} — aborting page parse");
                        return result.Count > 0 ? result : null;
                    }

                    Log("CosSpawn", $"0x30C9 Slot [{slot}] {item.CodeName} ({finalStack}/{item.MaxStack}) | remaining={packet.RemainingRead()}");
                    result[slot] = new SR_Item()
                    {
                        RefItemID = (int)refItemId,
                        CodeName128 = item.CodeName,
                        Stack = finalStack,
                        MaxStack = item.MaxStack
                    };
                }
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::CosPage", $"0x30C9 parse error: {ex.Message}");
                return null;
            }
        }
        
        #endregion

    }
}
