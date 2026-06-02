using CoreLib.Tools.Logging;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.DTO;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Framework;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network;
using VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.DTO.VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.Tools;

namespace VSRO_CONTROL_API.VSRO.AsynchronousProxy
{
    public static class PacketParser
    {
        // Server
        public static async Task<CharDataReturn?> ParseCharData(Packet packet)
        {
            // This packet is the reason we can't have nice things.
            try
            {
                CharDataReturn _data = new CharDataReturn();

                _data.LastLoginTime = packet.ReadUInt();
                Logger.Debug("ChardataHandler", $"serverTime={_data.LastLoginTime}");

                _data.RefObjId = packet.ReadUInt();
                Logger.Debug("ChardataHandler", $"refObjId={_data.RefObjId}");

                _data.Scale = packet.ReadByte();
                _data.CurrentLevel = packet.ReadByte();
                _data.MaxLevel = packet.ReadByte();

                Logger.Debug("ChardataHandler", $"scale={_data.Scale} curLevel={_data.CurrentLevel} maxLevel={_data.MaxLevel}");

                _data.ExpOffset = packet.ReadULong();
                _data.SkillExpOffset = packet.ReadUInt();

                Logger.Debug("ChardataHandler", $"expOffset={_data.ExpOffset} sExpOffset={_data.SkillExpOffset}");

                _data.RemainingGold = packet.ReadULong();
                _data.RemainingSkillPoint = packet.ReadUInt();
                _data.RemainingStatPoint = packet.ReadUShort();

                Logger.Debug("ChardataHandler", $"gold={_data.RemainingGold} skill={_data.RemainingSkillPoint} stat={_data.RemainingStatPoint}");

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

                Logger.Debug("ChardataHandler", $"HP={_data.HP} MP={_data.MP} Zerk={_data.RemainingZerkBubbles}");

                _data.InventorySize = packet.ReadByte();         // 0x61
                _data.ItemCount = packet.ReadByte();             // 0x1C

                Logger.Debug("ChardataHandler", $"invSize={_data.InventorySize} itemCount={_data.ItemCount}");

                for (int i = 0; i < _data.ItemCount; i++)
                {
                    Logger.Debug("ChardataHandler", $"--- ITEM {i} ---");

                    // 1. SLOT & RENT
                    byte slot = packet.ReadByte();
                    uint rentType = packet.ReadUInt();
                    Logger.Debug("ChardataHandler", $"ITEM {i}: slot={slot} rentType={rentType}");

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
                        Logger.Warn("ChardataHandler", $"DB MISS: refItemId={refItemId} at item index={i}, slot={slot}");
                        Logger.Warn("ChardataHandler",
                            $"Unknown refItemId={refItemId} at slot={slot} — reading ushort as fallback");
                        packet.ReadUShort(); // assume default stackable
                        continue;
                    }
                    // Initialize defaults
                    ushort finalStack = 1;
                    Logger.Debug("ChardataHandler", $"{itemInfo.item!.CodeName}: T1={itemInfo.item.T1} | T2={itemInfo.item.T2} | T3={itemInfo.item.T3} | T4={itemInfo.item.T4}");

                    if (itemInfo.item.T1 == 1) // NPC/Character objects
                    {
                        // Need to determine structure
                        Logger.Warn("ChardataHandler", $"T1=1 object: {itemInfo.item.CodeName} T2={itemInfo.item.T2} T3={itemInfo.item.T3} T4={itemInfo.item.T4} remaining={packet.RemainingRead()}");
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
                                Logger.Debug("PetHandler:Chardata", $"STATE={state}");
                                if (state == 1) continue;

                                uint cosObjId = packet.ReadUInt();
                                ushort nameLen = packet.ReadUShort();
                                for (int s = 0; s < nameLen; s++)
                                    packet.ReadByte();

                                if (itemInfo.item.T4 == 2) // AbilityPet
                                    packet.ReadUInt(); // SecondsToRentEndTime

                                byte unk02 = packet.ReadByte();
                                Logger.Debug("PetHandler:Chardata", $"unk02={unk02} remaining_after_unk02={packet.RemainingRead()}");
                                if (unk02 != 0)
                                {
                                    int extraBytes = (itemInfo.item.T4 == 2) ? 14 : 9;
                                    Logger.Debug("PetHandler:Chardata", $"reading {extraBytes} extra bytes");
                                    for (int x = 0; x < extraBytes; x++)
                                        packet.ReadByte();
                                    Logger.Debug("PetHandler:Chardata", $"remaining_after_extra={packet.RemainingRead()}");
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

                    Logger.Debug("ChardataHandler", $"SLOT [{slot}] {itemInfo.item.CodeName} ({finalStack}/{itemInfo.item.MaxStack}) | remainingPacketRead={packet.RemainingRead()}");
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
                        Logger.Debug("ChardataHandler", $"AVATAR [{slot}] {itemInfo.item.CodeName}");
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
                    Logger.Debug("Chardata:Mastery", $"Mastery ID: {mst.ID}, Level: {mst.CurLevel}");

                    mastery = packet.ReadByte();
                    if (mst.CurLevel > 0)
                    {
                        _data.MasteryList.Add(mst);
                        masteryCount++;
                    }
                    
                }
                _data.MasteryCount = masteryCount;
                
                Logger.Debug("Chardata:Mastery", $"Mastery count: {_data.MasteryCount} | remaining: {packet.RemainingRead()}");
                
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
                            ReadableName = skillData.ReadableName
                        };
                        _data.LearnedSkills.Add(learned);
                        skillCount++;
                    }

                    skillMarker = packet.ReadByte();
                }
                _data.SkillCount = skillCount;
                Logger.Debug("Chardata:Skill", $"Skill count: {skillCount} | remaining: {packet.RemainingRead()}");

                // QUESTS
                ushort completedQuestCount = packet.ReadUShort();
                Logger.Debug("Chardata:Quest", $"Completed quests: {completedQuestCount}");
                for (int i = 0; i < completedQuestCount; i++)
                {
                    packet.ReadUInt(); // completed quest ref ID
                }

                byte activeQuestCount = packet.ReadByte();
                Logger.Debug("Chardata:Quest", $"Active quests: {activeQuestCount}");

                for (int q = 0; q < activeQuestCount; q++)
                {
                    uint questID = packet.ReadUInt();
                    ushort unkShort = packet.ReadUShort(); // horse shit
                    byte questType = packet.ReadByte();
                    byte questStatus = packet.ReadByte();

                    byte objectiveCount = 0;
                    string questName = "";

                    Logger.Debug("Chardata:Quest", $"Quest {q} header: ID={questID} Type=0x{questType:X2} Status={questStatus} unkShort=0x{unkShort:X4}");

                    switch (questType)
                    {
                        
                        case 0x08:
                            break;
                        case 0x18:
                        case 0x58:
                            objectiveCount = packet.ReadByte();
                            Logger.Debug("Chardata:Quest", $"Quest {q} objectiveCount={objectiveCount}");

                            for (int o = 0; o < objectiveCount; o++)
                            {
                                byte objID = packet.ReadByte();
                                byte objStatus = packet.ReadByte();
                                string objName = packet.ReadAscii();

                                int tailBytes = (questType == 0x58) ? 10 : 5; // i declare useless
                                byte[] tail = new byte[tailBytes];
                                for (int i = 0; i < tailBytes; i++) tail[i] = packet.ReadByte();
                                Logger.Debug("Chardata:Quest", $"Quest {q} Obj {o} tail ({tailBytes}): {BitConverter.ToString(tail).Replace("-", " ")}");

                                if (objectiveCount == 1)
                                    questName = objName;

                                Logger.Info("Chardata:Quest", $"Quest {q} Obj {o}: ID={objID} Status={objStatus} Name='{objName}'");
                            }
                            break;
                        default:
                            Logger.Debug("Chardata:Quest", $"Quest {q} UNHANDLED TYPE 0x{questType:X2}");
                            break;
                    }

                    Logger.Debug("Chardata:Quest", $"Quest {q} done: ID={questID} Type=0x{questType:X2} Objectives={objectiveCount} Name='{(string.IsNullOrEmpty(questName) ? "Tatorial" : questName)}'");
                }
                for (int i = 0; i < 5; i++) packet.ReadByte();
                _data.CharacterUID = packet.ReadUInt();
                Logger.Debug("Chardata:UID", $"CharacterUID: {_data.CharacterUID}");

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

                Logger.Debug("Chardata:Position",
                    $"Sectors: X={_data.SectorX} Y={_data.SectorY} UID={_data.CharacterUID}");
                Logger.Debug("Chardata:Position",
                    $"Raw Pos: X={_data.RawX:F2} Z={_data.WorldZ:F2} Y={_data.RawY:F2}");
                Logger.Debug("Chardata:Position",
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
                Logger.Debug("Chardata:Speeds", $"WalkSpeed={_data.WalkSpeed} RunSpeed={_data.RunSpeed}");
                Logger.Debug("Chardata:Speeds", $"Remaining after speeds: {packet.RemainingRead()} bytes");

                byte activeBuffCount = packet.ReadByte();
                Logger.Debug("Chardata:Buffs", $"Active buffs: {activeBuffCount}");

                // Useless fucking buff section that never contains anything, handled through BUFF_START
                for (int i = 0; i < activeBuffCount; i++)
                {
                    uint refSkillId = packet.ReadUInt();
                    ushort timedJobId = packet.ReadUShort();

                    var buff = new SR_Buff { ID = refSkillId, TimedJobId = timedJobId };
                    var buff_res = await DBConnect.GetBuffById(refSkillId);
                    if (buff_res != null)
                    {
                        Logger.Debug("Chardata:Buffs", $"Buff ID: {buff.ID}, Codename: {buff_res.CodeName}");

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
                Logger.Error("PacketParser::CharStats", ex.Message + ex.StackTrace);
                return null;
            }
        }
        public static async Task<CharDataReturn?> ParseItemMove(Packet packet, VSRO.DTO.ISession session)
        {
            try
            {
                return new CharDataReturn();
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::ItemMove", ex.Message);
                return null;
            }
        }
        public static async Task<CharDataReturn?> ParseItemUse(Packet packet)
        {
            try
            {
                return new CharDataReturn();
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::ItemUse", ex.Message);
                return null;
            }
        }
        public static async Task<CharDataReturn?> ParseClientChat(Packet packet)
        {
            try
            {
                return new CharDataReturn();
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::ClientChat", ex.Message);
                return null;
            }
        }
        public static async Task<CharDataReturn?> ParseCOSSpawn(Packet packet)
        {
            try
            {
                return new CharDataReturn();
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::COSSpawn", ex.Message);
                return null;
            }
        }
        public static async Task<CharDataReturn?> ParseCOSDespawn(Packet packet)
        {
            try
            {
                return new CharDataReturn();
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::COSDespawn", ex.Message);
                return null;
            }
        }
        public static async Task<CharDataReturn?> ParseNewGoldAmount(Packet packet)
        {
            try
            {
                return new CharDataReturn();
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::NewGoldAmount", ex.Message);
                return null;
            }
        }
        public static async Task<CharDataReturn?> ParseHPMPUpdate(Packet packet)
        {
            try
            {
                return new CharDataReturn();
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::HPMPUpdate", ex.Message);
                return null;
            }
        }
        public static async Task<CharDataReturn?> ParseStorageItems(Packet packet)
        {
            try
            {
                return new CharDataReturn();
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::StorageItems", ex.Message);
                return null;
            }
        }
        public static async Task<CharDataReturn?> ParseEXP(Packet packet)
        {
            try
            {
                return new CharDataReturn();
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::EXP", ex.Message);
                return null;
            }
        }
        public static async Task<CharDataReturn?> ParseSTRUpdate(Packet packet)
        {
            try
            {
                return new CharDataReturn();
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::STRUpdate", ex.Message);
                return null;
            }
        }
        public static async Task<CharDataReturn?> ParseINTUpdate(Packet packet)
        {
            try
            {
                return new CharDataReturn();
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::INTUpdate", ex.Message);
                return null;
            }
        }
        public static async Task<CharDataReturn?> ParseServerMovement(Packet packet)
        {
            try
            {
                return new CharDataReturn();
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::ClientMovement", ex.Message);
                return null;
            }
        }

        // Client
        public static async Task<CharDataReturn?> ParseClientSort(Packet packet)
        {
            try
            {
                return new CharDataReturn();
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::ClientSort", ex.Message);
                return null;
            }
        }
        public static async Task<CharDataReturn?> ParseClientClaimReward(Packet packet)
        {
            try
            {
                return new CharDataReturn();
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::ClientClaimReward", ex.Message);
                return null;
            }
        }
        public static async Task<CharDataReturn?> ParseClientGetAchievements(Packet packet)
        {
            try
            {

                return new CharDataReturn();
            }
            catch (Exception ex)
            {
                Logger.Error("PacketParser::ClientGetAchievements", ex.Message);
                return null;
            }
        }

        #region - Helpers + Sorting -

        private static async Task ParseCosPage(Packet packet, byte itemCount, uint petUID, InventoryTracker inv)
        {
            var pet = inv.Pets[petUID];
            for (int i = 0; i < itemCount; i++)
            {
                byte indexByte = packet.ReadByte();
                packet.ReadUInt(); // padding
                uint refItemId = packet.ReadUInt();
                byte slot = (byte)(indexByte + 1);

                var itemInfoResult = await DBConnect.GetItemRecord(refItemId);
                if (!itemInfoResult.success)
                {
                    Logger.Warn("CosSpawn", $"0x30C9 unknown refObjID 0x{refItemId:X} at slot {slot}");
                    break;
                }

                var item = itemInfoResult.item;
                ushort finalStack = 1;

                if (item.T2 == 3)
                {
                    finalStack = packet.ReadUShort();
                    if (finalStack == 0) finalStack = 1;
                    if (item.CodeName.Contains("ATTRSTONE"))
                        packet.ReadByte(); // assimilation byte

                }
                else if (item.T2 == 1)
                {
                    packet.ReadByte();
                    packet.ReadULong();
                    packet.ReadUInt();
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
                    Logger.Warn("CosSpawn", $"0x30C9 unhandled T2={item.T2} for {item.CodeName} at slot {slot}");
                    break;
                }
                Logger.Debug("CosSpawn", $"0x30C9 Pet 0x{petUID:X} Slot [{slot}] {item.CodeName} ({finalStack}/{item.MaxStack}) | remaining={packet.RemainingRead()}");
                pet.Inventory[slot] = new SR_Item() 
                {
                    RefItemID = (int)refItemId,
                    CodeName128 = item.CodeName,
                    Stack = finalStack,
                    MaxStack = item.MaxStack
                };
                Logger.Debug("CosSpawn", $"0x30C9 Pet 0x{petUID:X} Slot [{slot}] {item.CodeName} ({finalStack}/{item.MaxStack})");
            }

            if (packet.RemainingRead() > 0)
            {
                var tail = new List<string>();
                while (packet.RemainingRead() > 0)
                    tail.Add(packet.ReadByte().ToString("X2"));
                Logger.Debug("CosSpawn", $"0x30C9 trailing bytes: {string.Join(" ", tail)}");
            }
        }
        private static async Task<bool> SendPetMoveAndWait(Proxy proxy, uint petUID, byte source, byte dest, ushort qty, CancellationToken cancellationToken = default, int timeoutMs = 3000)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tcs = new TaskCompletionSource<bool>();
            proxy.PendingMoves[source] = tcs;

            var movePacket = new Packet(Constant.CLIENT_ITEM_MOVE);
            movePacket.WriteByte(16);
            movePacket.WriteUInt(petUID);
            movePacket.WriteByte((byte)(source - 1));
            movePacket.WriteByte((byte)(dest - 1));
            movePacket.WriteUShort(qty);
            proxy.Server.Send(movePacket);

            var completed = await Task.WhenAny(
                tcs.Task,
                Task.Delay(timeoutMs, cancellationToken));

            proxy.PendingMoves.Remove(source);

            if (cancellationToken.IsCancellationRequested)
                return false;

            if (completed == tcs.Task)
                return tcs.Task.Result;

            return false;
        }
        private static async Task<bool> SendMoveAndWait(Proxy proxy, byte source, byte dest, ushort qty, CancellationToken cancellationToken = default, int timeoutMs = 3000)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tcs = new TaskCompletionSource<bool>();
            proxy.PendingMoves[source] = tcs;

            var movePacket = new Packet(Constant.CLIENT_ITEM_MOVE);
            movePacket.WriteByte(0);
            movePacket.WriteByte(source);
            movePacket.WriteByte(dest);
            movePacket.WriteUShort(qty);
            proxy.Server.Send(movePacket);

            var completed = await Task.WhenAny(
                tcs.Task,
                Task.Delay(timeoutMs, cancellationToken));

            proxy.PendingMoves.Remove(source);

            // just bail
            if (cancellationToken.IsCancellationRequested)
                return false;

            if (completed == tcs.Task)
                return tcs.Task.Result;

            return false;
        }
        private static async Task<PlayerTools.SortResult> SortInventoryStep(Proxy proxy, string sortMode = "type", CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (proxy.Session!.Inventory.Slots.IsEmpty)
                return PlayerTools.SortResult.Aborted;

            if (proxy.Session!.Inventory.Slots.Values.Any(s => s.CodeName128 == "MALL_PENDING"))
            {
                PlayerTools.SendToProxyChat(proxy, PlayerTools.ChatType.Notice, null, "You have pending mall items. Teleport to resync before sorting.");
                return PlayerTools.SortResult.Aborted;
            }
            else if (proxy.Session!.Inventory.Slots.Values.Any(s => s.CodeName128 == "UNKNOWN_PET_TRANSFER"))
            {
                PlayerTools.SendToProxyChat(proxy, PlayerTools.ChatType.Notice, null, "You have unsynced items. Teleport to resync before sorting.");
                return PlayerTools.SortResult.Aborted;
            }

            // Snapshot current inventory slots (slot >= 13 = player inventory)
            var slots = proxy.Session!.Inventory.Slots
                .Where(kvp => kvp.Key >= 13)
                .OrderBy(kvp => kvp.Key)
                .ToList();

            // === STACK (fixed direction + token checks) ===
            bool didStack = false;
            for (int i = 0; i < slots.Count; i++)
            {
                var (slotA, itemA) = slots[i];
                if (itemA.MaxStack <= 1) continue;
                if (itemA.Stack >= itemA.MaxStack) continue;

                for (int j = i + 1; j < slots.Count; j++)
                {
                    cancellationToken.ThrowIfCancellationRequested();   // ← important for large inventories

                    var (slotB, itemB) = slots[j];
                    if (itemB.RefItemID != itemA.RefItemID) continue;
                    if (itemB.Stack <= 0) continue;

                    int spaceInA = itemA.MaxStack - itemA.Stack;
                    if (spaceInA <= 0) break;

                    ushort qty = (ushort)Math.Min(itemB.Stack, spaceInA);

                    bool moved = await SendMoveAndWait(proxy, slotB, slotA, qty, cancellationToken);
                    if (moved)
                    {
                        didStack = true;
                        itemA = new SR_Item()
                        {
                            RefItemID = itemA.RefItemID,
                            CodeName128 = itemA.CodeName128,
                            Stack = itemA.Stack + qty,
                            MaxStack = itemA.MaxStack
                        };
                        slots[i] = new KeyValuePair<byte, SR_Item>(slotA, itemA);

                        itemB = new SR_Item()
                        {
                            RefItemID = itemB.RefItemID,
                            CodeName128 = itemB.CodeName128,
                            Stack = itemB.Stack - qty,
                            MaxStack = itemB.MaxStack
                        }; 
                        slots[j] = new KeyValuePair<byte, SR_Item>(slotB, itemB);
                    }
                }
            }
            if (didStack) return PlayerTools.SortResult.Continue;

            // === PACK ===
            int start = 13;
            for (int i = 0; i < slots.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                byte expectedSlot = (byte)(start + i);
                var (slot, item) = slots[i];
                if (slot != expectedSlot)
                {
                    await SendMoveAndWait(proxy, slot, expectedSlot, (ushort)item.Stack, cancellationToken);
                    return PlayerTools.SortResult.Continue;
                }
            }

            // === SORT ===
            var sorted = sortMode == "name"
                ? slots
                    .OrderBy(s => GameObjectNameResolver.Resolve(s.Value.CodeName128))
                    .ThenBy(s => s.Value.CodeName128)
                    .ThenByDescending(s => s.Value.Stack)
                    .ToList()
                : slots
                    .OrderBy(s => s.Value.CodeName128)
                    .ThenByDescending(s => s.Value.Stack)
                    .ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                byte targetSlot = (byte)(start + i);
                var (currentSlot, item) = sorted[i];

                if (currentSlot != targetSlot)
                {
                    await SendMoveAndWait(proxy, currentSlot, targetSlot, (ushort)item.Stack, cancellationToken);
                    return PlayerTools.SortResult.Continue;
                }
            }

            return PlayerTools.SortResult.Completed;
        }
        private static async Task<PlayerTools.SortResult> SortPetInventoryStep(Proxy proxy, uint petUID, string sortMode = "type", CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!proxy.Session!.Inventory.Pets.TryGetValue(petUID, out var petInv))
                return PlayerTools.SortResult.Aborted;

            if (petInv.Inventory.IsEmpty)
                return PlayerTools.SortResult.Aborted;

            var slots = petInv.Inventory
                .OrderBy(kvp => kvp.Key)
                .ToList();

            // === STACK ===
            bool didStack = false;
            for (int i = 0; i < slots.Count; i++)
            {
                var (slotA, itemA) = slots[i];
                if (itemA.MaxStack <= 1) continue;
                if (itemA.Stack >= itemA.MaxStack) continue;

                for (int j = i + 1; j < slots.Count; j++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var (slotB, itemB) = slots[j];
                    if (itemB.RefItemID != itemA.RefItemID) continue;
                    if (itemB.Stack <= 0) continue;

                    int spaceInA = itemA.MaxStack - itemA.Stack;
                    if (spaceInA <= 0) break;

                    ushort qty = (ushort)Math.Min(itemB.Stack, spaceInA);

                    bool moved = await SendPetMoveAndWait(proxy, petUID, slotB, slotA, qty, cancellationToken);
                    if (moved)
                    {
                        didStack = true;
                        itemA = new SR_Item()
                        {
                            RefItemID = itemA.RefItemID,
                            CodeName128 = itemA.CodeName128,
                            Stack = itemA.Stack + qty,
                            MaxStack = itemA.MaxStack
                        };
                        slots[i] = new KeyValuePair<byte, SR_Item>(slotA, itemA);

                        itemB = new SR_Item()
                        {
                            RefItemID = itemB.RefItemID,
                            CodeName128 = itemB.CodeName128,
                            Stack = itemB.Stack - qty,
                            MaxStack = itemB.MaxStack
                        };
                        slots[j] = new KeyValuePair<byte, SR_Item>(slotB, itemB);
                    }
                }
            }
            if (didStack) return PlayerTools.SortResult.Continue;

            // === PACK ===
            const byte start = 1;
            for (int i = 0; i < slots.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                byte expectedSlot = (byte)(start + i);
                var (slot, item) = slots[i];
                if (slot != expectedSlot)
                {
                    await SendPetMoveAndWait(proxy, petUID, slot, expectedSlot, (ushort)item.Stack, cancellationToken);

                    return PlayerTools.SortResult.Continue;
                }
            }

            // === SORT ===
            var sorted = sortMode == "name"
                ? slots
                    .OrderBy(s => GameObjectNameResolver.Resolve(s.Value.CodeName128))
                    .ThenBy(s => s.Value.CodeName128)
                    .ThenByDescending(s => s.Value.Stack)
                    .ToList()
                : slots
                    .OrderBy(s => s.Value.CodeName128)
                    .ThenByDescending(s => s.Value.Stack)
                    .ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                byte targetSlot = (byte)(start + i);
                var (currentSlot, item) = sorted[i];

                if (currentSlot != targetSlot)
                {
                    await SendPetMoveAndWait(proxy, petUID, currentSlot, targetSlot, (ushort)item.Stack, cancellationToken);
                    return PlayerTools.SortResult.Continue;
                }
            }

            return PlayerTools.SortResult.Completed;
        }
        private static async Task<PlayerTools.SortResult> SortInventoryLogical(Proxy proxy, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (proxy.Session!.Inventory.Slots.IsEmpty)
                return PlayerTools.SortResult.Aborted;

            if (proxy.Session!.Inventory.Slots.Values.Any(s => s.CodeName128 == "MALL_PENDING"))
            {
                PlayerTools.SendToProxyChat(proxy, PlayerTools.ChatType.Notice, null, "You have pending mall items. Teleport to resync before sorting.");
                return PlayerTools.SortResult.Aborted;
            }
            else if (proxy.Session!.Inventory.Slots.Values.Any(s => s.CodeName128 == "UNKNOWN_PET_TRANSFER"))
            {
                PlayerTools.SendToProxyChat(proxy, PlayerTools.ChatType.Notice, null, "You have unsynced items. Teleport to resync before sorting.");
                return PlayerTools.SortResult.Aborted;
            }

            // Snapshot current
            var slots = proxy.Session!.Inventory.Slots
                .Where(kvp => kvp.Key >= 13)
                .OrderBy(kvp => kvp.Key)
                .ToList();

            // Stack
            bool didStack = false;
            for (int i = 0; i < slots.Count; i++)
            {
                var (slotA, itemA) = slots[i];
                if (itemA.MaxStack <= 1) continue;
                if (itemA.Stack >= itemA.MaxStack) continue;

                for (int j = i + 1; j < slots.Count; j++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var (slotB, itemB) = slots[j];
                    if (itemB.RefItemID != itemA.RefItemID) continue;
                    if (itemB.Stack <= 0) continue;

                    int spaceInA = itemA.MaxStack - itemA.Stack;
                    if (spaceInA <= 0) break;

                    ushort qty = (ushort)Math.Min(itemB.Stack, spaceInA);

                    bool moved = await SendMoveAndWait(proxy, slotB, slotA, qty, cancellationToken);
                    if (moved)
                    {
                        didStack = true;
                        itemA = new SR_Item()
                        {
                            RefItemID = itemA.RefItemID,
                            CodeName128 = itemA.CodeName128,
                            Stack = itemA.Stack + qty,
                            MaxStack = itemA.MaxStack
                        };
                        slots[i] = new KeyValuePair<byte, SR_Item>(slotA, itemA);

                        itemB = new SR_Item()
                        {
                            RefItemID = itemB.RefItemID,
                            CodeName128 = itemB.CodeName128,
                            Stack = itemB.Stack - qty,
                            MaxStack = itemB.MaxStack
                        };
                        slots[j] = new KeyValuePair<byte, SR_Item>(slotB, itemB);
                    }
                }
            }
            if (didStack) return PlayerTools.SortResult.Continue;

            // Pack
            int start = 13;
            for (int i = 0; i < slots.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                byte expectedSlot = (byte)(start + i);
                var (slot, item) = slots[i];
                if (slot != expectedSlot)
                {
                    await SendMoveAndWait(proxy, slot, expectedSlot, (ushort)item.Stack, cancellationToken);
                    return PlayerTools.SortResult.Continue;
                }
            }

            // Logical Sort
            bool IsPetScroll(string cn) => cn.StartsWith("ITEM_COS") && cn.EndsWith("SCROLL");

            bool IsSpecialScroll(string cn) => cn.Contains("REVERSE_RETURN_SCROLL") ||
                                               cn.Contains("RETURN_SCROLL_HIGH_SPEED") ||
                                               cn.Contains("ITEM_MOVE_SPEED_UP");

            bool IsHpPotion(string cn) => cn.StartsWith("ITEM_ETC_HP_POTION_") &&
                                          int.TryParse(cn.Substring(19), out int lvl) && lvl is >= 1 and <= 5;

            bool IsMpPotion(string cn) => cn.StartsWith("ITEM_ETC_MP_POTION_") &&
                                          int.TryParse(cn.Substring(19), out int lvl) && lvl is >= 1 and <= 5;

            bool IsQuestItem(string cn) => cn.Contains("SNOWFLAKE") || cn.Contains("QNO") || cn.Contains("QSP");

            bool IsGrowthPotion(string cn) => cn.Contains("PET_GROWTH_POTION") ||
                                              cn.Contains("ITEM_PET_SKILL_FIRE") ||
                                              cn.Contains("ITEM_PET_SKILL_COLD") ||
                                              cn.Contains("ITEM_PET_SKILL_LIGHTNING");

            // Find the SINGLE largest HP / MP stack (>20) to promote
            var largeHpCandidate = slots
                .Where(s => IsHpPotion(s.Value.CodeName128) && s.Value.Stack > 20)
                .OrderByDescending(s => s.Value.Stack)
                .FirstOrDefault();

            var largeMpCandidate = slots
                .Where(s => IsMpPotion(s.Value.CodeName128) && s.Value.Stack > 20)
                .OrderByDescending(s => s.Value.Stack)
                .FirstOrDefault();

            // Build ordered list exactly in the sequence you wanted
            var sorted = new List<KeyValuePair<byte, SR_Item>>();

            // Pet Scrolls
            sorted.AddRange(slots
                .Where(s => IsPetScroll(s.Value.CodeName128))
                .OrderBy(s => s.Value.CodeName128)
                .ThenByDescending(s => s.Value.Stack));

            // Special scrolls
            sorted.AddRange(slots
                .Where(s => IsSpecialScroll(s.Value.CodeName128))
                .OrderBy(s => s.Value.CodeName128)
                .ThenByDescending(s => s.Value.Stack));

            // One large HP stack (if any)
            if (largeHpCandidate.Key != 0)
                sorted.Add(largeHpCandidate);

            // One large MP stack (if any)
            if (largeMpCandidate.Key != 0)
                sorted.Add(largeMpCandidate);

            var placed = new HashSet<byte>(sorted.Select(s => s.Key));

            // Quest items (everything not already placed)
            sorted.AddRange(slots
                .Where(s => IsQuestItem(s.Value.CodeName128) && !placed.Contains(s.Key))
                .OrderBy(s => s.Value.CodeName128)
                .ThenByDescending(s => s.Value.Stack));

            placed.UnionWith(sorted.Skip(placed.Count).Select(s => s.Key));

            // Growth / pet skill potions
            sorted.AddRange(slots
                .Where(s => IsGrowthPotion(s.Value.CodeName128) && !placed.Contains(s.Key))
                .OrderBy(s => s.Value.CodeName128)
                .ThenByDescending(s => s.Value.Stack));

            placed.UnionWith(sorted.Skip(placed.Count).Select(s => s.Key));

            // Everything else that is NOT a potion
            sorted.AddRange(slots
                .Where(s => !placed.Contains(s.Key) &&
                            !IsHpPotion(s.Value.CodeName128) &&
                            !IsMpPotion(s.Value.CodeName128))
                .OrderBy(s => s.Value.CodeName128)
                .ThenByDescending(s => s.Value.Stack));

            // Deferred HP and MP potions
            sorted.AddRange(slots
                .Where(s => !placed.Contains(s.Key) &&
                            (IsHpPotion(s.Value.CodeName128) || IsMpPotion(s.Value.CodeName128)))
                .OrderBy(s => s.Value.CodeName128)
                .ThenByDescending(s => s.Value.Stack));

            // Final
            for (int i = 0; i < sorted.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                byte targetSlot = (byte)(start + i);
                var (currentSlot, item) = sorted[i];

                if (currentSlot != targetSlot)
                {
                    await SendMoveAndWait(proxy, currentSlot, targetSlot, (ushort)item.Stack, cancellationToken);
                    return PlayerTools.SortResult.Continue;
                }
            }

            return PlayerTools.SortResult.Completed;
        }

        #endregion

    }
}
