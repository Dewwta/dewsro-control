using CoreLib.Tools.Logging;
using System.Collections.Concurrent;
using VSRO_CONTROL_API.Settings;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Achivements;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.DTO;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Framework;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Tracking;
using VSRO_CONTROL_API.VSRO.Bots;
using VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.DTO.VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.Tools;


namespace VSRO_CONTROL_API.VSRO.AsynchronousProxy
{
    public static class PlayerTools
    {
        #region - Enums -

        public enum ChatType
        {
            General = 1,
            PrivateMessage = 2,
            PartyChat = 3,
            GuildChat = 4,
            Global = 6,
            Notice = 7,
            Academy = 8,
            
        }
        public enum ItemMovement : byte
        {
            InventoryToInventory = byte.MinValue,
            StorageToStorage = 0x01,
            InventoryToStorage = 0x02,
            StorageToInventory = 0x03,
            InventoryToExchange = 0x04,
            ExchangeToInventory = 0x05,
            GroundToInventory = 0x06,
            InventoryToGround = 0x07,
            ShopToInventory = 0x08,
            InventoryToShop = 0x09,
            InventoryGoldToGround = 0x0A,
            StorageGoldToInventory = 0x0B,
            InventoryGoldToStorage = 0x0C,
            InventoryGoldToExchange = 0x0D,
            GameServerToInventory = 0x0E,
            InventoryToGameServer = 0x0F,
            PetToPet = 0x10,
            GroundToPet = 0x11,
            ShopToTransport = 0x13,
            TransportToShop = 0x14,
            ItemMallToInventory = 0x18,
            PetToInventory = 0x1A,
            InventoryToPet = 0x1B,
            GroundToPetToInventory = 0x1C,
            GuildToGuild = 0x1D,
            InventoryToGuild = 0x1E,
            GuildToInventory = 0x1F,
            InventoryGoldToGuild = 0x20,
            GuildGoldToInventory = 0x21,
            ShopBuyBack = 0x22,
            AvatarToInventory = 0x23,
            InventoryToAvatar = 0x24,
            OpenMagicCube = 0x2A,
            ShopToInventoryCoin = 0x2B,
            InventoryToCube = 0x27,
            MagicCubeConsumed = 0x29
        }
        public enum SortResult
        {
            Continue,   // keep looping
            Completed,  // finished successfully
            Aborted     // stopped early (unsynced, error condition, etc.)
        }

        #endregion


        #region - Handler Registry (SERVER) -

        public static void RegisterChardataHandler(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_CHARDATA, async (sender, e) =>
            {
                try
                {
                    var packet = e.Packet.Clone();
                    
                    CharDataReturn? data = await PacketParser.ParseCharData(packet);
                    if (data == null)
                    {
                        Logger.Error("PlayerTools::Chardata", "Chardata object was null!");
                    } 
                    else
                    {
                        e.Proxy.Session!.PlayerStats = new PlayerStats
                        {
                            CurrentHP = data.HP,
                            CurrentMP = data.MP,
                            ZerkLevel = data.RemainingZerkBubbles,
                            CurrentLevel = data.CurrentLevel,
                            UnusedStatPoints = data.RemainingStatPoint,
                            RemainingGold = data.RemainingGold,
                            RemainingSkillPoints = data.RemainingSkillPoint,
                        };

                        if (Overseer.ExpTableCumulative.TryGetValue((byte)(data.CurrentLevel - 1), out ulong baseExp))
                            e.Proxy.Session.CumulativeExp = baseExp + data.ExpOffset;
                        else
                            e.Proxy.Session.CumulativeExp = data.ExpOffset; // level 1, no base

                        DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "unclaimed_rewards", new
                        {
                            levels = e.Proxy.Session.UnclaimedRewards.Select(b => (int)b).ToArray()
                        });

                        e.Proxy.Session!.Buffs.Clear(); // TODO: Clear buffs, check if buff is a scroll and dont clear then.

                        e.Proxy.Session!.RegionId = data.RegionId;
                        e.Proxy.Session!.WorldX = (int)data.WorldX;
                        e.Proxy.Session!.WorldY = (int)data.WorldY;
                        e.Proxy.Session!.Z = (short)data.WorldZ;  // No conversion
                        e.Proxy.Session!.RawX = (short)data.RawX;
                        e.Proxy.Session!.RawY = (short)data.RawY;
                        e.Proxy.Session!.SectorX = data.SectorX;
                        e.Proxy.Session!.SectorY = data.SectorY;
                        e.Proxy.Session!.RunSpeed = data.RunSpeed;
                        e.Proxy.Session!.Inventory.Slots.Clear();
                        e.Proxy.Session!.Inventory.Equipment.Clear();
                        e.Proxy.Session!.Inventory.Avatars.Clear();
                        
                        

                        e.Proxy.Session!.Inventory.Slots = data.Slots;
                        e.Proxy.Session!.Inventory.Equipment = data.Equipment;
                        e.Proxy.Session!.Inventory.Avatars = data.Avatars;

                        e.Proxy.Session.JID = (int)data.AccountJID;
                        e.Proxy.Session.IsGM = data.IsGM;

                        Logger.Info("Playertools:CharData", $"Account JID: {e.Proxy.Session.JID}, IsGM: {e.Proxy.Session.IsGM} RegionId: {e.Proxy.Session.RegionId}");
                        var savedPlaytime = await DBConnect.GetPlayTimeAsync(e.Proxy.Session!.CharacterName!);
                        e.Proxy.Session.TotalPlayTime = TimeSpan.FromSeconds(savedPlaytime.seconds);

                        DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "char_init", new
                        {
                            hp = e.Proxy.Session!.PlayerStats!.CurrentHP,
                            mp = e.Proxy.Session.PlayerStats.CurrentMP,
                            sessionKills = e.Proxy.Session.SessionKills,
                            unusedStatPoints = e.Proxy.Session.PlayerStats.UnusedStatPoints,
                            currentLevel = e.Proxy.Session.PlayerStats.CurrentLevel,
                            gold = e.Proxy.Session.PlayerStats.RemainingGold,
                            
                        });
                        DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "session_sync", new
                        {
                            sessionSeconds = (int)e.Proxy.Session.AccumulatedPlayTime.TotalSeconds,
                            sessionKills = e.Proxy.Session.SessionKills,
                            totalSeconds = (int)e.Proxy.Session.TotalPlayTime.TotalSeconds,
                            isAfk = e.Proxy.Session.IsAfk ? 1 : 0,
                            accountJID = e.Proxy.Session.JID,
                            isGM = e.Proxy.Session.IsGM ? 1 : 0,
                        });

                        if (e.Proxy.Session.UnclaimedRewards.Count > 0)
                            DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "unclaimed_rewards", new
                            {
                                levels = e.Proxy.Session.UnclaimedRewards.Select(b => (int)b).ToArray()
                            });

                        e.Proxy.Session.RegionReadableName = RegionResolver.ResolveReadable(e.Proxy.Session!.SectorX, e.Proxy.Session!.SectorY, (short)e.Proxy.Session!.RegionId);
                        e.Proxy.Session.RegionName = RegionResolver.Resolve((short)e.Proxy.Session!.RegionId, e.Proxy.Session!.SectorX, e.Proxy.Session!.SectorY); // The specific code name for dungeons, Qin-Shi with floors, or Stone Cave using Z based floor math. 

                        DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "movement_sync", new
                        {
                            regionReadableName = e.Proxy.Session!.RegionReadableName,
                            regionName = e.Proxy.Session!.RegionName,
                            regionId = e.Proxy.Session!.RegionId,
                            wX = e.Proxy.Session!.WorldX,
                            wY = e.Proxy.Session!.WorldY,
                            wZ = e.Proxy.Session!.Z,
                            xSec = e.Proxy.Session.SectorX,
                            ySec = e.Proxy.Session.SectorY,
                        });

                        e.Proxy.Session!.LearnedSkills.Clear();
                        e.Proxy.Session!.LearnedSkills.AddRange(data.LearnedSkills);

                        DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "skill_pool_sync", new
                        {
                            skills = e.Proxy.Session.LearnedSkills
                                .Where(s => s.Enabled)
                                .Select(s => new { id = s.ID, name = s.ReadableName, passive = s.AutoAttackType == 0 })
                        });

                        var botData = PlayerBotDataStore.Load(e.Proxy.Session.CharacterName!);
                        if (botData != null)
                        {
                            e.Proxy.Session._lastBotX = botData.TrainX;
                            e.Proxy.Session._lastBotY = botData.TrainY;
                            e.Proxy.Session._lastBotZ = botData.TrainZ;
                            e.Proxy.Session._lastBotR = botData.TrainR;

                            e.Proxy.Session._savedAttackSkills.Clear();
                            e.Proxy.Session._savedBuffSkills.Clear();

                            foreach (var id in botData.AttackSkillIds)
                            {
                                var skill = e.Proxy.Session.LearnedSkills.FirstOrDefault(s => s.ID == id);
                                if (skill != null) e.Proxy.Session._savedAttackSkills.Add(skill);
                            }
                            foreach (var id in botData.BuffSkillIds)
                            {
                                var skill = e.Proxy.Session.LearnedSkills.FirstOrDefault(s => s.ID == id);
                                if (skill != null) e.Proxy.Session._savedBuffSkills.Add(skill);
                            }

                            // Restore into attack loop if it already exists
                            if (e.Proxy.Session._attackLoop != null)
                            {
                                foreach (var sk in e.Proxy.Session._savedAttackSkills)
                                    e.Proxy.Session._attackLoop.AddSkillToUse(sk, false);
                                foreach (var sk in e.Proxy.Session._savedBuffSkills)
                                    e.Proxy.Session._attackLoop.AddSkillToUse(sk, true);
                            }

                            e.Proxy.Session._botSettings = botData.Settings ?? new BotSettings();

                            // Push coordinates to sync positions
                            DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "bot_config_sync", new
                            {
                                x = botData.TrainX,
                                y = botData.TrainY,
                                z = botData.TrainZ,
                                r = botData.TrainR
                            });

                            // Push skill states
                            DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "skill_list_sync", new
                            {
                                attack = e.Proxy.Session._savedAttackSkills.Select(s => new { id = s.ID, name = s.ReadableName }),
                                buffs = e.Proxy.Session._savedBuffSkills.Select(s => new { id = s.ID, name = s.ReadableName })
                            });

                            DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "bot_settings_sync", e.Proxy.Session._botSettings);

                            DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "bot_config_sync", new
                            {
                                x = botData.TrainX,
                                y = botData.TrainY,
                                z = botData.TrainZ,
                                r = botData.TrainR,
                                regionId = botData.RegionId,
                            });

                            DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "skill_list_sync", new
                            {
                                attack = e.Proxy.Session._savedAttackSkills.Select(s => new { id = s.ID, name = s.ReadableName }),
                                buffs = e.Proxy.Session._savedBuffSkills.Select(s => new { id = s.ID, name = s.ReadableName })
                            });
                        }
                    }

                    e.Proxy.Session!.Inventory.IsReady = true;
                }
                catch (Exception ex)
                {
                    Logger.Error("ChardataHandler",
                        $"CRASH parsing CHARDATA: {ex.Message}\n{ex.StackTrace}");
                }
            });
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_STATS, async (sender, e) =>
            {
                try
                {
                    var proxy = e.Proxy;
                    var packet = e.Packet.Clone();

                    CharStatsReturn? data = await PacketParser.ParseCharStats(packet);

                    if (proxy != null && proxy.Session != null)
                    {
                        e.Proxy.Session!.PlayerStats!.STR = data!.STR;
                        e.Proxy.Session!.PlayerStats!.INT = data!.INT;
                        e.Proxy.Session!.PlayerStats!.MaxHP = data!.MaxHP;
                        e.Proxy.Session!.PlayerStats.MaxMP = data!.MaxMP;

                        var payload = new
                        {
                            strength = e.Proxy.Session.PlayerStats.STR,
                            intelligence = e.Proxy.Session.PlayerStats.INT,
                            maxHp = e.Proxy.Session.PlayerStats.MaxHP,
                            maxMp = e.Proxy.Session.PlayerStats.MaxMP,
                        };
                        DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "stat_init", payload);
                    }
                    else
                    {
                        Logger.Error("ServerStatsHandler", $"Character session was null!");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("CharacterStatsHandler", $"Error occurred parsing character stats!: {ex.Message}");
                }
            });
        }
        public static void RegisterItemMoveHandler(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_ITEM_MOVEMENT, async (sender, e) =>
            {
                try
                {
                    var packet = e.Packet.Clone();

                    byte result = packet.ReadByte();
                    ItemMovement moveType = (ItemMovement)packet.ReadByte();

                    // Need success flag
                    if (result != 1)
                        return;

                    var inv = e.Proxy.Session!.Inventory;

                    switch (moveType)
                    {
                        case ItemMovement.InventoryToInventory:     // 0x00
                            {
                                byte sourceSlot = packet.ReadByte();
                                byte destSlot = packet.ReadByte();
                                // CORRECT --

                                ushort movedQty = packet.ReadUShort();
                                byte unk = packet.ReadByte();

                                bool IsEquipmentSlot(byte slot) => slot >= 1 && slot <= 12;

                                if (e.Proxy.PendingMoves.TryGetValue(sourceSlot, out var tcs))
                                {
                                    e.Proxy.PendingMoves.Remove(sourceSlot);
                                    tcs.TrySetResult(true);
                                }

                                bool sourceIsEquip = IsEquipmentSlot(sourceSlot);
                                bool destIsEquip = IsEquipmentSlot(destSlot);

                                inv.Equipment.TryGetValue(sourceSlot, out var srcEquip);
                                inv.Slots.TryGetValue(sourceSlot, out var srcInv);

                                var src = sourceIsEquip ? srcEquip : srcInv;

                                if (src == null || src.RefItemID == 0)
                                    return;


                                // Destination check
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

                        case ItemMovement.GroundToInventory:        // 0x06
                            {
                                byte slotOrFlag = packet.ReadByte();
                                if (slotOrFlag == 0xFE)
                                {
                                    uint gold = packet.ReadUInt();
                                    Logger.Debug("ItemMoveHandler", $"Picked up {gold} gold");
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
                                await AchievementService.OnItemPickup(e.Proxy.Session?.CharacterName!, itemInfo.item.CodeName, e.Proxy);
                                Logger.Debug("ItemMoveHandler", $"Picked up {itemInfo.item.CodeName} x{finalStack} → slot {slot}");
                                break;
                            }

                        case ItemMovement.ShopToInventory:          // 0x08
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
                                if (e.Proxy.Session!.SpawnedObjects.TryGetValue(e.Proxy.Session!.LastTargetUID, out var spawnInfo))
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

                                    Logger.Trace("ItemMoveHandler", $"Bought {shopItem.CodeName} x{quantity} → slots [{string.Join(",", toSlots)}]");
                                }
                                else
                                {
                                    Logger.Warn("ItemMoveHandler", $"BUY: unresolved NPC=0x{e.Proxy.Session!.LastTargetUID:X} objId={npcObjId} tab={shopTab} slot={shopSlot}");
                                }
                                break;
                            }

                        case ItemMovement.InventoryToShop:          // 0x09
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
                                        Logger.Debug("ItemMoveHandler", $"Sold {item.CodeName128} x{quantity} from slot {playerSlot} for {goldReceived}g (removed)");
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
                                        Logger.Debug("ItemMoveHandler", $"Sold {item.CodeName128} x{quantity} from slot {playerSlot} for {goldReceived}g ({remaining} remain)");
                                    }
                                }
                                break;
                            }

                        case ItemMovement.GroundToPet:              // 0x11
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
                                    Logger.Debug("ItemMoveHandler", $"Pet picked up quest item {itemInfo.item.CodeName} x{finalStack} → PLAYER slot {slot}");
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

                                    Logger.Debug("ItemMoveHandler", $"Pet picked up event item {itemInfo.item.CodeName} x{finalStack} → PLAYER slot {slot}");

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
                                    await AchievementService.OnItemPickup(e.Proxy.Session?.CharacterName!, itemInfo.item.CodeName, e.Proxy);
                                    Logger.Debug("ItemMoveHandler", $"Pet picked up {itemInfo.item.CodeName} x{finalStack} → pet 0x{petUID:X} slot {slot}");
                                }
                                break;
                            }

                        case ItemMovement.GroundToPetToInventory:   // 0x1C
                            {
                                uint petUID = packet.ReadUInt();
                                byte slotOrFlag = packet.ReadByte();

                                if (slotOrFlag == 0xFE)
                                {
                                    uint gold = packet.ReadUInt();
                                    Logger.Debug("ItemMoveHandler", $"Pet picked up {gold} gold");
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
                                    // Route to PLAYER inventory
                                    inv.Slots[slot] = new SR_Item()
                                    {
                                        RefItemID = (int)refItemId,
                                        CodeName128 = itemInfo.item.CodeName,
                                        Stack = finalStack,
                                        MaxStack = itemInfo.item.MaxStack
                                    };
                                    Logger.Debug("ItemMoveHandler", $"Pet picked up quest item {itemInfo.item.CodeName} x{finalStack} → PLAYER slot {slot}");
                                }
                                else
                                {
                                    // Route to PET inventory
                                    if (!inv.Pets.ContainsKey(petUID))
                                        inv.Pets[petUID] = new();

                                    inv.Pets[petUID].Inventory[slot] = new SR_Item()
                                    {
                                        RefItemID = (int)refItemId,
                                        CodeName128 = itemInfo.item.CodeName,
                                        Stack = finalStack,
                                        MaxStack = itemInfo.item.MaxStack
                                    };
                                    await AchievementService.OnItemPickup(e.Proxy.Session?.CharacterName!, itemInfo.item.CodeName, e.Proxy);
                                    Logger.Debug("ItemMoveHandler", $"Pet picked up {itemInfo.item.CodeName} x{finalStack} → pet 0x{petUID:X} slot {slot}");
                                }

                                break;
                            }

                        case ItemMovement.PetToInventory:           // 0x1A
                            {
                                uint petUID = packet.ReadUInt();
                                byte petSlot = packet.ReadByte();
                                byte playerSlot = packet.ReadByte();

                                // Try the specific pet first
                                bool found = false;
                                if (inv.Pets.TryGetValue(petUID, out var petInv) &&
                                    petInv.Inventory.TryGetValue(petSlot, out var petItem))
                                {
                                    inv.Slots[playerSlot] = petItem;
                                    petInv.Inventory.TryRemove(petSlot, out _);
                                    found = true;
                                    Logger.Debug("ItemMoveHandler", $"Pet→Player: {petItem.CodeName128} x{petItem.Stack} → slot {playerSlot}");
                                }

                                // If not found, search all pet inventories for that slot
                                if (!found)
                                {
                                    foreach (var pet in inv.Pets)
                                    {
                                        if (pet.Value.Inventory.TryGetValue(petSlot, out var item))
                                        {
                                            inv.Slots[playerSlot] = item;
                                            pet.Value.Inventory.TryRemove(petSlot, out _);
                                            found = true;
                                            Logger.Debug("ItemMoveHandler", $"Pet→Player: {item.CodeName128} x{item.Stack} → slot {playerSlot} (from pet 0x{pet.Key:X})");
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

                        case ItemMovement.InventoryToPet:           // 0x1B
                            {
                                uint petUID = packet.ReadUInt();
                                byte playerSlot = packet.ReadByte();
                                byte petSlot = packet.ReadByte();

                                if (inv.Slots.TryGetValue(playerSlot, out var item))
                                {
                                    // Store in the correct pet
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
                                    Logger.Debug("ItemMoveHandler", $"Player→Pet: {item.CodeName128} → pet 0x{targetPet:X} slot {petSlot}");
                                }
                                break;
                            }

                        case ItemMovement.PetToPet:                 // 0x10
                            {
                                uint petUID = packet.ReadUInt();
                                byte srcSlot = (byte)(packet.ReadByte() + 1);
                                byte destSlot = (byte)(packet.ReadByte() + 1);
                                ushort qty = packet.ReadUShort();

                                // Resolve pending move
                                if (e.Proxy.PendingMoves.TryGetValue(srcSlot, out var tcs))
                                {
                                    e.Proxy.PendingMoves.Remove(srcSlot);
                                    tcs.TrySetResult(true);
                                }


                                // Find the pet inventory
                                ConcurrentDictionary<byte, SR_Item>? petInv = null;
                                foreach (var pet in inv.Pets)
                                {
                                    if (pet.Value.Inventory.ContainsKey(srcSlot))
                                    {
                                        petInv = pet.Value.Inventory;
                                        break;
                                    }
                                }
                                if (petInv == null && inv.Pets.TryGetValue(petUID, out var directPet))
                                    petInv = directPet.Inventory;

                                if (petInv != null && petInv.TryGetValue(srcSlot, out var src))
                                {
                                    petInv.TryGetValue(destSlot, out var dst);

                                    if (dst.RefItemID != 0 && dst.RefItemID == src.RefItemID && dst.MaxStack > 1)
                                    {
                                        // Stack merge
                                        int total = dst.Stack + qty;
                                        if (total <= dst.MaxStack)
                                        {
                                            petInv[destSlot] = new SR_Item()
                                            {
                                                RefItemID = dst.RefItemID,
                                                CodeName128 = dst.CodeName128,
                                                Stack = total,
                                                MaxStack = dst.MaxStack
                                            };
                                            
                                            petInv.TryRemove(srcSlot, out _);
                                        }
                                        else
                                        {
                                            int remainder = total - dst.MaxStack;
                                            petInv[destSlot] = new SR_Item()
                                            {
                                                RefItemID = dst.RefItemID,
                                                CodeName128 = dst.CodeName128,
                                                Stack = dst.MaxStack,
                                                MaxStack = dst.MaxStack
                                            };
                                            petInv[srcSlot] = new SR_Item()
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
                                        // Swap
                                        petInv[destSlot] = src;
                                        petInv[srcSlot] = dst;
                                    }
                                    else
                                    {
                                        // Move to empty slot
                                        petInv[destSlot] = src;
                                        petInv.TryRemove(srcSlot, out _);
                                    }
                                }
                                break;
                            }

                        case ItemMovement.AvatarToInventory:        // 0x23
                            {
                                byte avatarSlot = packet.ReadByte();
                                byte playerSlot = packet.ReadByte();
                                ushort qty = packet.ReadUShort();
                                byte unk = packet.ReadByte();

                                if (inv.Avatars.TryGetValue(avatarSlot, out var avatarItem))
                                {
                                    inv.Slots[playerSlot] = avatarItem;
                                    inv.Avatars.TryRemove(avatarSlot, out _);
                                    Logger.Debug("ItemMoveHandler", $"Unequipped avatar {avatarItem.CodeName128} slot {avatarSlot} → inventory slot {playerSlot}");
                                }
                                break;
                            }

                        case ItemMovement.InventoryToAvatar:        // 0x24
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
                                    Logger.Debug("ItemMoveHandler", $"Equipped avatar {item.CodeName128} slot {playerSlot} → avatar slot {avatarSlot}");
                                }
                                break;
                            }

                        case ItemMovement.InventoryToStorage:       // 0x02
                            {
                                byte playerSlot = packet.ReadByte();
                                byte storageSlot = packet.ReadByte();

                                if (inv.Slots.TryGetValue(playerSlot, out var item))
                                {
                                    inv.Storage[storageSlot] = item;
                                    inv.Slots.TryRemove(playerSlot, out _);
                                    Logger.Debug("ItemMoveHandler", $"Deposited {item.CodeName128} slot {playerSlot} → storage slot {storageSlot}");
                                }
                                break;
                            }

                        case ItemMovement.StorageToInventory:       // 0x03
                            {
                                byte storageSlot = packet.ReadByte();
                                byte playerSlot = packet.ReadByte();

                                if (inv.Storage.TryGetValue(storageSlot, out var item))
                                {
                                    inv.Slots[playerSlot] = item;
                                    inv.Storage.TryRemove(storageSlot, out _);
                                    Logger.Debug("ItemMoveHandler", $"Withdrew {item.CodeName128} storage slot {storageSlot} → slot {playerSlot}");
                                }
                                else
                                {
                                    Logger.Warn("ItemMoveHandler", $"Withdrew unknown item from storage slot {storageSlot} → slot {playerSlot}");
                                }
                                break;
                            }

                        case ItemMovement.StorageToStorage:         // 0x01
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
                                    Logger.Debug("ItemMoveHandler", $"Storage move: {src.CodeName128} slot {srcSlot} → slot {destSlot}");
                                }
                                break;
                            }

                        case ItemMovement.InventoryToGround:        // 0x07
                            {
                                byte playerSlot = packet.ReadByte();

                                if (inv.Slots.TryGetValue(playerSlot, out var item))
                                {
                                    inv.Slots.TryRemove(playerSlot, out _);
                                    Logger.Debug("ItemMoveHandler", $"Dropped {item.CodeName128} from slot {playerSlot}");
                                }
                                break;
                            }

                        case ItemMovement.ItemMallToInventory:      // 0x18
                            {
                                ushort shopGroup = packet.ReadUShort();
                                byte mallTab = packet.ReadByte(); // tab
                                byte mallSlot = packet.ReadByte(); // slot
                                byte mallItemInfo = packet.ReadByte(); // useless
                                byte slotCount = packet.ReadByte(); // Sometimes a quantity of 5, will be given to you spread across 5 slots. So the 5 first open slots, and fills in any gaps, doesnt move anything around.

                                var toSlots = new List<byte>();
                                for (int i = 0; i < slotCount; i++)
                                    toSlots.Add(packet.ReadByte());

                                ushort quantity = packet.ReadUShort();

                                Logger.Debug("ItemMoveHandler",
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

                        case ItemMovement.GameServerToInventory:    // 0x0E
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
                              
                                    Logger.Debug("GameServerToInventory:ItemMoveHandler",
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

                        case ItemMovement.InventoryToGameServer:    // 0x0F
                            {

                                byte playerSlot = packet.ReadByte();

                                if (inv.Slots.TryRemove(playerSlot, out var item))
                                {
                                    Logger.Debug("ItemMoveHandler",
                                        $"I→G move: {item.CodeName128} slot={playerSlot}");
                                }

                                break;
                            }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("ItemMoveHandler", ex.Message);
                }
            });
        }
        public static void RegisterItemUseHandler(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_ITEM_USE, (sender, e) =>
            {
                try
                {
                    var packet = e.Packet.Clone();

                    byte result = packet.ReadByte();
                    byte slot = packet.ReadByte();

                    if (result != 1)
                    {
                        if (result == 2)
                        {
                            // Server Denied Flag
                            Logger.Debug("ItemUseHandler", "Use of the item was denied by the server.");
                            return;
                        }

                        Logger.Debug("ItemUseHandler",
                            $"Non-standard ITEM_USE, most likely denied. result={result} | slot={slot} | remaining bytes={packet.RemainingRead()}");
                        return;
                    }

                    ushort remainingStack = packet.ReadUShort();
                    ushort unk = packet.ReadUShort(); // required to be synchronized

                    Logger.Debug("ItemUseHandler",
                        $"result={result} | slot={slot} | remainingStack={remainingStack}");

                    var inv = e.Proxy.Session!.Inventory;

                    if (!inv.Slots.TryGetValue(slot, out var item))
                        return;

                    if (remainingStack == 0)
                    {
                        inv.Slots.TryRemove(slot, out _);

                        Logger.Debug("ItemUseHandler", $"Removed item from slot {slot} (stack reached 0)");
                    }
                    else
                    {
                        inv.Slots[slot] = new SR_Item()
                        {
                            RefItemID = item.RefItemID,
                            CodeName128 = item.CodeName128,
                            Stack = remainingStack,
                            MaxStack = item.MaxStack
                        };
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("ItemUseHandler", $"Error in ITEM_USE parse: {ex.Message}");
                }
            });
        }
        public static void RegisterChatCommandHandler(Server _agentProxy)
        {
            _agentProxy.RegisterClientPacketHandler(Constant.CLIENT_CHAT, async (sender, e) =>
            {
                var packet = e.Packet.Clone();
                byte chatType = packet.ReadByte();
                byte chatIndex = packet.ReadByte();
                string message = packet.ReadAscii();

                if (message.StartsWith("!sort", StringComparison.OrdinalIgnoreCase))
                {
                    e.CancelTransfer = true;
                    e.Proxy.Session!.IsSorting = true;
                    string sortMode = "type";
                    var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1)
                        sortMode = parts[1].ToLower();

                    if (sortMode != "name" && sortMode != "type")
                    {
                        PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, "Usage: !sort [name|type]");
                        return;
                    }

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, "Sorting started...");

                            int safety = 0;

                            while (safety <= 300)
                            {
                                var result = await SortInventoryStep(e.Proxy, sortMode);

                                if (result == SortResult.Completed)
                                {
                                    PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, "Sort complete.");
                                    return;
                                }

                                if (result == SortResult.Aborted)
                                {
                                    return; // silently stop (you already sent a reason inside)
                                }

                                await Task.Delay(150);
                                safety++;
                            }

                            PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, "Sort timed out.");

                        }
                        catch (Exception ex)
                        {
                           
                            Logger.Error(typeof(Overseer), $"Sort error: {ex}");
                            PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, "Sort failed! You must teleport before sorting.");
                        }
                        finally
                        {
                            e.Proxy.Session!.IsSorting = false;
                        }
                    });
                }
                else if (message.Equals("!bothelp", StringComparison.OrdinalIgnoreCase))
                {
                    e.CancelTransfer = true;
                    var charName = e.Proxy.Session?.CharacterName;
                    if (charName == null) return;

                    // Check cooldown
                    if (BotHelpManager.IsOnCooldown(charName, out var remaining))
                    {
                        int mins = (int)remaining.TotalMinutes + 1;
                        PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null,
                            $"You must wait {mins} minute(s) before requesting bot help again.");
                        return;
                    }

                    // Check if they already have a bot
                    if (BotHelpManager.GetBotAssignedTo(charName) != null)
                    {
                        PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null,
                            "You already have a bot assigned. Type !botcancel to release it.");
                        return;
                    }

                    // Find a free bot
                    var freeBot = await BotHelpManager.GetFreeBotAsync();
                    if (freeBot == null)
                    {
                        PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null,
                            "No bots are available right now. Try again later.");
                        return;
                    }

                    // Get player position
                    var session = e.Proxy.Session;
                    if (session == null) return;

                    _ = Task.Run(async () =>
                    {
                        PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null,
                            $"Dispatching bot to your location...");

   
                        bool success = await BotHelpManager.AssignBot(
                            freeBot, charName,
                            session.WorldX, session.Z, session.WorldY,
                            e.Proxy);

                        if (success)
                        {

                            PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null,
                                $"Bot dispatched! You have 1 hour. Type !botcancel to release early.");
                        }
                        else
                        {
                            PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null,
                                "Failed to dispatch bot. Try again.");
                        }
                            
                    });
                }
                else if (message.Equals("!botcancel", StringComparison.OrdinalIgnoreCase))
                {
                    e.CancelTransfer = true;
                    var charName = e.Proxy.Session?.CharacterName;
                    if (charName == null) return;

                    var assignedBot = BotHelpManager.GetBotAssignedTo(charName);
                    if (assignedBot == null)
                    {
                        PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null,
                            "You don't have a bot assigned.");
                        return;
                    }

                    _ = Task.Run(async () =>
                    {
                        await BotHelpManager.ReleaseBot(assignedBot, e.Proxy);
                        PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null,
                            "Bot released and returning to its training area.");
                    });
                }
                else if (message.Equals("!inv", StringComparison.OrdinalIgnoreCase))
                {
                    e.CancelTransfer = true;

                    var inv = e.Proxy.Session!.Inventory;
                    if (!inv.IsReady)
                    {
                        PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, "Inventory not loaded.");
                        return;
                    }

                    PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, $"Tracked inventory: {inv.Slots.Count()} items ; Equipment: {inv.Equipment.Count()} ; Pets: {inv.Pets.Count()}");
                }
            });
        }
        public static void RegisterCosSpawnHandler(Server _agentProxy)
        {
            async Task ParseCosPage(Packet packet, byte itemCount, uint petUID, InventoryTracker inv)
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

                    if (item!.T2 == 3)
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

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_ANIMATION_COS_SPAWN, async (sender, e) =>
            {
                try
                {
                    var packet = e.Packet;
                    var inv = e.Proxy.Session!.Inventory;

                    uint petUID = packet.ReadUInt();
                    uint refObjID = packet.ReadUInt();
                    packet.ReadUInt(); // stat1
                    packet.ReadUInt(); // stat2
                    packet.ReadUInt(); // stat3

                    var petObjInfo = await DBConnect.GetItemRecord(refObjID);
                    if (!petObjInfo.success)
                    {
                        Logger.Warn("CosSpawn", $"Unknown refObjID 0x{refObjID:X} for pet 0x{petUID:X}");
                        return;
                    }

                    bool isAttackPet = petObjInfo.item!.T4 == 3;
                    Logger.Debug("CosSpawn", $"Pet 0x{petUID:X} refObjID=0x{refObjID:X} T4={petObjInfo.item.T4} isAttack={isAttackPet} remaining={packet.RemainingRead()}");

                    if (isAttackPet)
                    {
                        packet.ReadUInt();
                        packet.ReadByte();
                        packet.ReadUInt();
                        packet.ReadUShort();
                        string attackPetName = packet.ReadAscii();
                        packet.ReadByte();

                        Logger.Debug("CosSpawn", $"Attack pet 0x{petUID:X} name='{attackPetName}'");

                        inv.Pets[petUID] = new Pet
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

                        e.Proxy.Session!.ActivePetUID = petUID;
                        return;
                    }

                    // Pickup / growth pet
                    string petName = packet.ReadAscii();
                    byte invSize = packet.ReadByte();
                    byte itemCount = packet.ReadByte();

                    Logger.Debug("CosSpawn", $"Pet 0x{petUID:X} name='{(string.IsNullOrEmpty(petName) ? "No name" : petName)}' invSize={invSize} itemCount={itemCount}");

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
                        Logger.Debug("CosSpawn", $"Pet 0x{petUID:X} Slot [{slot}] {item.CodeName} ({finalStack}/{item.MaxStack})");
                    }

                    // Consume trailing 5 bytes
                    packet.ReadUInt(); // linked UID
                    packet.ReadByte(); // unknown

                    inv.Pets[petUID] = new Pet
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

                    e.Proxy.Session!.ActivePetUID = petUID;
                    Logger.Debug("CosSpawn", $"Successfully parsed pet 0x{petUID:X} with {petSlots.Count} items");

                    if (inv.PendingCosPages.TryRemove(petUID, out var pending))
                    {
                        Logger.Debug("CosSpawn", $"Draining pending 0x30C9 for pet 0x{petUID:X}");
                        await ParseCosPage(pending.Packet, pending.ItemCount, petUID, inv);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("CosSpawn", $"Error parsing pet spawn: {ex.Message}\n{ex.StackTrace}");
                }
            });

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_ANIMATION_COS_REMOVE_MENU, async (sender, e) =>
            {
                var packet = e.Packet.Clone();
                uint petUID = packet.ReadUInt();
                byte type = packet.ReadByte();

                var inv = e.Proxy.Session!.Inventory;
                inv.Pets.TryGetValue(petUID, out var pet);
                string petName = pet?.Info.Name ?? $"0x{petUID:X}";

                if (type == 1)
                {
                    inv.Pets.TryRemove(petUID, out _);
                    if (e.Proxy.Session?.ActivePetUID == petUID)
                        e.Proxy.Session.ActivePetUID = 0;
                    Logger.Debug("CosDespawnHandler", $"Pet {petName} despawned and removed");
                    return;
                }

                if (type == 2 && packet.RemainingRead() == 0)
                {
                    Logger.Debug("CosDespawnHandler", $"Pet {petName} state change (recalled)");
                    return;
                }

                if (type == 2 && packet.RemainingRead() > 0)
                {
                    packet.ReadByte();              // 0x70 — unknown
                    byte pageItemCount = packet.ReadByte(); // 0x0F = item count for this page

                    Logger.Debug("CosSpawn", $"0x30C9 inventory page for pet 0x{petUID:X} | pageItemCount={pageItemCount} remaining={packet.RemainingRead()}");

                    if (pet == null)
                    {
                        Logger.Debug("CosSpawn", $"0x30C9 arrived early for 0x{petUID:X}, queuing");
                        inv.PendingCosPages[petUID] = (packet, pageItemCount);
                        return;
                    }

                    await ParseCosPage(packet, pageItemCount, petUID, inv);
                }
            });
        }
        public static void RegisterGoldUpdateHandler(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_NEW_GOLD_AMOUNT, async (sender, e) =>
            {
                try
                {
                    var packet = e.Packet;
                    int size = packet.RemainingRead();
                    var session = e.Proxy.Session;

                    if (size == 10)
                    {
                        byte flag = packet.ReadByte();
                        if (flag != 0x01) return;

                        ulong gold = packet.ReadULong();
                        packet.ReadByte();

                        e.Proxy.Session!.PlayerStats!.RemainingGold = gold;
                        DllBridge.Instance.SendToDll(session.AccountName!, "gold_update", new { remainGold = gold });
                        await AchievementService.OnGoldChanged(e.Proxy.Session!.CharacterName, (long)gold, e.Proxy);
                        Logger.Debug("GoldUpdateHandler", $"Gold → {gold}");

                    }
                    else if (size == 6)
                    {
                        byte flag = packet.ReadByte();
                        uint value = packet.ReadUInt();
                        packet.ReadByte();

                        if (flag == 0x02)
                        {
                            e.Proxy.Session!.PlayerStats!.RemainingSkillPoints = value;
                            Logger.Debug("SkillPointHandler", $"Skill points → {value}");
                        }
                        
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("GoldUpdateHandler", $"Error: {ex.Message}");
                }
            });
        }
        public static void RegisterPlayerHPMPHandler(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_HPMP_UPDATE, (sender, e) =>
            {
                
                var packet = e.Packet;
                uint targetUID = packet.ReadUInt();

                if (targetUID != e.Proxy.Session?.CharacterUID)
                    return;

                byte flags = packet.ReadByte();
                byte unk = packet.ReadByte();
                byte statType = packet.ReadByte();
                uint value = packet.ReadUInt();

                if (statType == 0x01)
                    e.Proxy.Session.PlayerStats!.CurrentHP = value;
                else if (statType == 0x02)
                    e.Proxy.Session.PlayerStats!.CurrentMP = value;
            });
        }
        public static void RegisterStorageHandler(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_STORAGE_ITEMS, async (sender, e) =>
            {
                try
                {
                    var packet = e.Packet;
                    var inv = e.Proxy.Session!.Inventory;
                    inv.Storage.Clear();

                    byte storageSize = packet.ReadByte();
                    byte itemCount = packet.ReadByte();

                    Logger.Debug("StorageHandler", $"Storage opened: size={storageSize} items={itemCount}");

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

                        inv.Storage[slot] = new SR_Item()
                        {
                            RefItemID = (int)refItemId,
                            CodeName128 = itemInfo.item.CodeName,
                            Stack = finalStack,
                            MaxStack = itemInfo.item.MaxStack
                        };
                        Logger.Debug("StorageHandler", $"Storage [{slot}] {itemInfo.item.CodeName} ({finalStack}/{itemInfo.item.MaxStack})");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("StorageHandler", $"Error: {ex.Message}\n{ex.StackTrace}");
                }
            });
        }
        public static void RegisterPlayerKillHandler(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_EXP, (sender, e) =>
            {
                var packet = e.Packet.Clone();
                var proxy = e.Proxy;

                uint mobUID = packet.ReadUInt();
                ulong exp = packet.ReadULong();
                ulong sExp = packet.ReadULong();

                if (!e.Proxy.Session!.SpawnedObjects.TryGetValue(mobUID, out var spawnInfo))
                {
                    Logger.Debug("KillTracker", $"Missing refObjID for UID=0x{mobUID:X}, most likely NPC.");
                    return;
                }

                e.Proxy.Session!._attackLoop?.OnMobKilled(mobUID);
                e.Proxy.Session!.SpawnedPositions.TryRemove(mobUID, out _);
                e.Proxy.Session!.SpawnedObjects.TryRemove(mobUID, out _);
                e.Proxy.Session!.MobUIDs.TryRemove(mobUID, out _);


                _ = Task.Run(async () =>
                {
                    var result = await DBConnect.GetMonsterCodeName(spawnInfo.RefObjID);

                    lock (proxy.Session!)
                    {
                        proxy.Session!.CumulativeExp += exp;
                    }

                    e.Proxy.Session.IncrementSessionKills();

                    if (!result.codeName.StartsWith("NPC"))
                    {
                        await AchievementService.OnMonsterKill(
                            proxy.Session!.CharacterName!,
                            result.codeName,
                            proxy);
                    }

                    if (!result.codeName.StartsWith("NPC"))
                    {
                        Logger.Debug("KillTracker",
                            $"{proxy.Session?.CharacterName} killed mob {GameObjectNameResolver.Resolve(result.codeName)}");
                    }

                    if (UniqueKillResolver.Resolve(result.codeName) && !result.codeName.StartsWith("NPC"))
                    {
                        proxy.Session!.IncrementSessionUniqueKills();
                        await UniqueKillResolver.OnUniqueKill(proxy, result.codeName);
                    }

                    _ = Task.Run(() => PlayerTools.CheckLevelUp(proxy));

                    DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "kill_update", new
                    {
                        sessionKills = e.Proxy.Session.SessionKills
                    });
                });
            });
        }
        public static void RegisterSTRINTUpdateHandler(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_STR_UPDATE, (sender, e) =>
            {
                try
                {
                    var packet = e.Packet.Clone();
                    var proxy = e.Proxy;

                    byte result = packet.ReadByte();

                    if (result != 0)
                    {
                        if (proxy.Session != null)
                        {
                            proxy.Session.PlayerStats!.UnusedStatPoints--;
                        }
                        else
                        {
                            Logger.Error("STRHandler", $"Could not update STR on unkown user!");
                        }
                    }
                    else
                    {
                        Logger.Debug("STRHandler", $"Could not update STR with a success flag of 0");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("STRHandler", $"Error occurred updating character STR!: {ex.Message}");
                }
            });
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_INT_UPDATE, (sender, e) =>
            {
                try
                {
                    var packet = e.Packet.Clone();
                    var proxy = e.Proxy;

                    byte result = packet.ReadByte();

                    if (result != 0)
                    {
                        if (proxy.Session != null)
                        {
                            proxy.Session.PlayerStats!.UnusedStatPoints--;
                        }
                        else
                        {
                            Logger.Error("INTHandler", $"Could not update STR on unkown user!");
                        }
                    }
                    else
                    {
                        Logger.Debug("INTHandler", $"Could not update INT with a success flag of 0");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("INTHandler", $"Error occurred updating character INT!: {ex.Message}");
                }
            });
        }
        public static void RegisterServerMovementHandler(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_MOVEMENT, (sender, e) =>
            {
                var packet = e.Packet.Clone();

                try
                {
                    uint uniqueId = packet.ReadUInt();
                    bool hasMovement = packet.ReadByte() == 1;

                    if (!hasMovement)
                    {
                        //Logger.Debug("B021", $"UID=0x{uniqueId:X8} hasMovement=false — skipping");
                        return;
                    }

                    ushort regionId = packet.ReadUShort();
                    bool isDungeon = IsDungeon(regionId);

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
                        packet.ReadUShort(); // dest region
                        packet.ReadUShort();
                        packet.ReadUShort();
                        packet.ReadUShort();
                    }

                    byte sx = (byte)(regionId & 0xFF);
                    byte sy = (byte)((regionId >> 8) & 0xFF);

                    var (botPos, outSX, outSY) = BotPosition.FromRawOffsets(sx, sy, (float)(int)rawX, (float)(int)rawY, rawZ);
                    int worldX = (int)botPos.X;
                    int worldY = (int)botPos.Y;
                    sx = outSX;
                    sy = outSY;


                    if (e.Proxy.Session != null &&
                        uniqueId == e.Proxy.Session.CharacterUID)
                    {
                        e.Proxy.Session.RegionId = regionId;
                        e.Proxy.Session.RawX = (short)rawX;
                        e.Proxy.Session.RawY = (short)rawY;
                        e.Proxy.Session.Z = (short)rawZ;
                        e.Proxy.Session.SectorX = sx;
                        e.Proxy.Session.SectorY = sy;
                        e.Proxy.Session.WorldX = worldX;
                        e.Proxy.Session.WorldY = worldY;
                        e.Proxy.Session.RegionReadableName = RegionResolver.ResolveReadable(sx, sy, (short)regionId);
                        e.Proxy.Session.RegionName = RegionResolver.Resolve((short)regionId, e.Proxy.Session!.SectorX, e.Proxy.Session!.SectorY); // The specific code name for dungeons, Qin-Shi with floors, or Stone Cave using Z based floor math. 
                        e.Proxy.Session.LastMovementUpdate = DateTime.UtcNow;

                        DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "movement_sync", new
                        {
                            regionReadableName = e.Proxy.Session!.RegionReadableName,
                            regionName = e.Proxy.Session!.RegionName,
                            regionId = e.Proxy.Session!.RegionId,
                            wX = worldX,
                            wY = worldY,
                            wZ = (short)rawZ,
                            xSec = sx,
                            ySec = sy,
                        });
                    }
                    else if (e.Proxy.Session != null &&
                        e.Proxy.Session.SpawnedObjects.ContainsKey(uniqueId))
                    {
                        e.Proxy.Session.SpawnedPositions[uniqueId] = (worldX, worldY);
                    }


                }
                catch (Exception ex)
                {
                    Logger.Error("B021", $"Parse error: {ex.Message}");
                }
            });
        }
        public static void RegisterBuffHandler(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_BUFF_START, (sender, e) =>
            {
                var packet = e.Packet;
                uint targetUID = packet.ReadUInt();

                if (targetUID != e.Proxy.Session?.CharacterUID)
                    return;

                uint refSkillId = packet.ReadUInt();
                uint timedJobId = packet.ReadUInt();

                _ = Task.Run(async () =>
                {
                    var skill = await DBConnect.GetSkillById(refSkillId);
                    if (skill == null) return;

                    skill.TimedJobId = timedJobId;

                    e.Proxy.Session!.Buffs.RemoveAll(b => b.ID == refSkillId);
                    e.Proxy.Session!.Buffs.Add(skill);

                    Logger.Debug("BuffHandler", $"Buff added: {skill.CodeName} TimedJobId={timedJobId} MoveSpeed={skill.MoveSpeedPercent}%");
                });
            });

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_BUFF_END, (sender, e) =>
            {
                var packet = e.Packet;
                byte unk = packet.ReadByte();
                uint timedJobId = packet.ReadUInt();

                var removed = e.Proxy.Session?.Buffs.FirstOrDefault(b => b.TimedJobId == timedJobId);
                if (removed != null)
                {
                    e.Proxy.Session!.Buffs.Remove(removed);
                    Logger.Debug("BuffHandler", $"Buff removed: {removed.CodeName} TimedJobId={timedJobId}");
                }
            });
        }

        #endregion

        #region - Handler Registry (CUSTOM PACKETS)

        public static void RegisterAttackHandlers(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_SKILL_ATTACK, (sender, e) =>
            {
                try
                {
                    var packet = e.Packet;
                    byte type = packet.ReadByte();
                    byte status = packet.ReadByte();
                    Logger.Trace("AttackHandler", $"0xB074 type={type:X2} status={status:X2}");
                    e.Proxy.Session!._attackLoop?.OnSkillAttackResponse(type, status);
                }
                catch (Exception ex)
                {
                    Logger.Error("ServerAttack:0xb070", $"Error in SERVER_ATTACK!!!: {ex.Message}");
                }
                
            });

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_ATTACK, (sender, e) =>
            {
                try
                {
                    var packet = e.Packet;
                    if (packet.RemainingRead() < 19)
                    {
                        Logger.Trace("AttackHandler", $"0xB070 short variant ({packet.RemainingRead()} bytes) — skipping");
                        return;
                    }
                    packet.ReadByte();
                    packet.ReadByte();
                    packet.ReadByte();
                    packet.ReadUInt();
                    uint attackerUID = packet.ReadUInt();
                    packet.ReadUInt();
                    uint targetUID = packet.ReadUInt();
                    Logger.Trace("AttackHandler", $"0xB070 attacker={attackerUID} target={targetUID}");
                    e.Proxy.Session!._attackLoop?.OnAttackResult(attackerUID, targetUID);
                }
                catch (Exception ex)
                {
                    Logger.Error("ServerAttack:0xb070", $"Error in SERVER_ATTACK!!!: {ex.Message}");
                }
            });
        }
        public static void RegisterClientSortHandler(Server _agentProxy)
        {
            _agentProxy.RegisterClientPacketHandler(Constant.DEW_SORT, (sender, e) =>
            {
                e.CancelTransfer = true;
                var packet = e.Packet.Clone();
                var type = packet.ReadByte();

                
                string sortMode = "type";

                switch (type)
                {
                    case 0x01:
                        sortMode = "type";
                        break;
                    case 0x02:
                        sortMode = "name";
                        break;
                    case 0x03:
                        sortMode = "logical";
                        break;
                    default:
                        PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, "Usage: !sort [name|type]");
                        return;
                }

                byte target = packet.ReadByte(); // target is 0x00 -> player | 0x01 -> Pickpet | 0x02 -> Storage
                
                void SortPlayer()
                {
                    if (e.Proxy.Session!.IsSorting)
                    {
                        PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, "Sort already in progress.");
                        return;
                    }
                    e.Proxy.Session!.IsSorting = true;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var cts = new CancellationTokenSource();
                            e.Proxy.Session!.ActiveSortCts = cts;
                            PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, "Sorting started...");

                            int safety = 0;

                            while (safety <= 300)
                            {

                                var result = sortMode == "logical"
                                    ? await SortInventoryLogical(e.Proxy, cts.Token)
                                    : await SortInventoryStep(e.Proxy, sortMode, cts.Token);

                                if (result == SortResult.Completed)
                                {
                                    PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, "Sort complete.");
                                    return;
                                }

                                if (result == SortResult.Aborted)
                                {
                                    return;
                                }

                                await Task.Delay(150, cts.Token);
                                safety++;
                            }

                            PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, "Sort timed out.");

                        }
                        catch (OperationCanceledException)
                        {
                            // normal shutdown
                        }
                        catch (Exception ex)
                        {

                            Logger.Error(typeof(Overseer), $"Sort error: {ex}");
                            PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, "Sort failed! You must teleport before sorting.");
                        }
                        finally
                        {
                            e.Proxy.Session!.IsSorting = false;
                            try { e.Proxy.Session!.ActiveSortCts?.Cancel(); } catch (ObjectDisposedException) { }
                            try { e.Proxy.Session!.ActiveSortCts?.Dispose(); } catch (ObjectDisposedException) { }
                            e.Proxy.Session!.ActiveSortCts = null;
                        }
                        return;
                    });
                }
                void SortPet()
                {
                    if (e.Proxy.Session!.IsSorting)
                    {
                        PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, "Sort already in progress.");
                        return;
                    }
                    e.Proxy.Session!.IsSorting = true;

                    var pickPet = e.Proxy.Session!.Inventory.Pets.FirstOrDefault(p => !p.Value.IsAttackPet);
                    if (pickPet.Value == null)
                    {
                        e.Proxy.Session!.IsSorting = false;
                        PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, "No pick pet active.");
                        return;
                    }

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var cts = new CancellationTokenSource();
                            e.Proxy.Session!.ActiveSortCts = cts;
                            PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, "Sorting started...");

                            int safety = 0;

                            while (safety <= 300)
                            {

                                var result = await SortPetInventoryStep(e.Proxy, pickPet.Key, sortMode, cts.Token);


                                if (result == SortResult.Completed)
                                {
                                    PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, "Sort complete.");
                                    return;
                                }

                                if (result == SortResult.Aborted)
                                {
                                    return;
                                }

                                await Task.Delay(150, cts.Token);
                                safety++;
                            }

                            PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, "Sort timed out.");

                        }
                        catch (OperationCanceledException)
                        {
                            // normal shutdown
                        }
                        catch (Exception ex)
                        {

                            Logger.Error(typeof(Overseer), $"Sort error: {ex}");
                            PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, "Sort failed! You must teleport before sorting.");
                        }
                        finally
                        {
                            e.Proxy.Session!.IsSorting = false;
                            try { e.Proxy.Session!.ActiveSortCts?.Cancel(); } catch (ObjectDisposedException) { }
                            try { e.Proxy.Session!.ActiveSortCts?.Dispose(); } catch (ObjectDisposedException) { }
                            e.Proxy.Session!.ActiveSortCts = null;
                        }
                        return;
                    });

                }
                void SortStorage()
                {
                    if (e.Proxy.Session!.IsSorting)
                    {
                        PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, "Sort already in progress.");
                        return;
                    }
                }

                switch (target)
                {
                    case 0x00:
                        SortPlayer();
                        break;
                    case 0x01:
                        SortPet();
                        break;
                    case 0x02:
                        SortStorage();
                        break;
                }
            });
        }
        public static void RegisterPlayerRewardHandler(Server _agentProxy)
        {
            _agentProxy.RegisterClientPacketHandler(Constant.DEW_CLAIM_REWARD, async (sender, e) =>
            {
                e.CancelTransfer = true;
                var packet = e.Packet.Clone();

                byte claimedLevel = packet.ReadByte();
                ushort qty = packet.ReadUShort();
                byte plus = packet.ReadByte();

                byte codeLen = packet.ReadByte();
                var codeChars = new byte[codeLen];
                for (int i = 0; i < codeLen; i++)
                    codeChars[i] = packet.ReadByte();
                string itemCode = System.Text.Encoding.ASCII.GetString(codeChars);

                var session = e.Proxy.Session;
                if (session == null) return;

                if (session.PendingLevelReward != claimedLevel)
                {
                    Logger.Warn("RewardClaim", $"Rejected claim from {session.CharacterName}: " +
                                $"pending={session.PendingLevelReward} claimed={claimedLevel}");
                    return;
                }

                if (!Overseer.LevelRewardOptions.TryGetValue(claimedLevel, out var options))
                {
                    Logger.Warn("RewardClaim", $"No reward options for level {claimedLevel}");
                    return;
                }

                var chosen = options.FirstOrDefault(o => o.CodeName == itemCode && o.Plus == plus && o.Qty == qty);
                if (chosen == null)
                {
                    Logger.Warn("RewardClaim", $"Invalid reward {itemCode} +{plus} x{qty} for level {claimedLevel}");
                    return;
                }

                bool isEquipment = itemCode.Contains("_WEAPON_") || itemCode.Contains("_SHIELD_")
                                || itemCode.Contains("_ARMOR_") || itemCode.Contains("_HELM_");

                var result = await DBConnect.GiveItemToPlayer(
                    session.CharacterName!, chosen.CodeName, chosen.Plus, chosen.Qty,
                    isEquipment);

                if (result.success)
                {
                    session.PendingLevelReward = null;
                    session.UnclaimedRewards.Remove(claimedLevel);
                    await DBConnect.RemoveUnclaimedRewardAsync(session.CharacterName!, claimedLevel);
                    DllBridge.Instance.SendToDll(session.AccountName!, "unclaimed_rewards", new
                    {
                        levels = session.UnclaimedRewards.Select(b => (int)b).ToArray()
                    });

                    PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null,
                        $"Reward claimed! {GameObjectNameResolver.Resolve(itemCode)} has been added to your inventory.");

                    Logger.Info("RewardClaim",
                        $"{session.CharacterName} claimed {itemCode} x{qty} +{plus} for level {claimedLevel}");
                }
                else
                {
                    Logger.Error("RewardClaim", $"GiveItem failed for {session.CharacterName}: {result.reason}");
                    PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null,
                        "Something went wrong delivering your reward. Please contact an admin.");
                }
            });

            DllBridge.Instance.RegisterHandler("reward_reopen", async (accountName, json) =>
            {
                var proxy = Overseer.GetProxyByAccountName(accountName);
                if (proxy?.Session == null)
                {
                    Logger.Error("reward_reopen", $"Session null {accountName}");
                    return;
                }

                byte level = (byte)json.GetProperty("level").GetInt32();

                if (!proxy.Session.UnclaimedRewards.Contains(level))
                {
                    Logger.Error("reward_reopen", $"UNCLAIMED REWARDS DOESNT CONTAIN ANYTHING");
                    return;
                }

                if (!Overseer.LevelRewardOptions.TryGetValue(level, out var options) || options.Count == 0)
                {
                    Logger.Error("reward_reopen", $"Options dont exist");
                    return;
                }

                proxy.Session.PendingLevelReward = level;

                var codeNames = options.Select(o => o.CodeName);
                var iconPaths = await DBConnect.GetItemIconPaths(codeNames);
                Logger.Debug("OnPlayerLevelUp", $"Icon paths fetched: {iconPaths.Count} for codes: {string.Join(", ", codeNames)}");
                foreach (var kvp in iconPaths)
                    Logger.Debug("OnPlayerLevelUp", $"  {kvp.Key} -> {kvp.Value}");

                DllBridge.Instance.SendToDll(accountName, "level_reward", new
                {
                    level,
                    options = options.Select(o => new {
                        code = o.CodeName,
                        plus = o.Plus,
                        qty = o.Qty,
                        name = o.DisplayName,
                        icon = iconPaths.TryGetValue(o.CodeName, out var path)
                           ? path.Replace(".ddj", ".png").Replace("\\", "/")
                           : ""
                    }).ToArray()
                });
            });
        }
        public static void RegisterAchievementHandler(Server _agentProxy)
        {
            _agentProxy.RegisterClientPacketHandler(Constant.DEW_ACHIEVEMENTS, async (sender, e) =>
            {
                e.CancelTransfer = true;
                var session = e.Proxy.Session;
                if (session == null) return;

                var allDefs = AchievementLoader.Definitions?.Achievements ?? new();
                var dbProgress = await DBConnect.GetAllAchievementsForChar(session.CharacterName!);
                var progressLookup = dbProgress.ToDictionary(p => p.name, p => p);

                var payload = allDefs.Select(def =>
                {
                    progressLookup.TryGetValue(def.Name, out var p);
                    return new
                    {
                        name = def.Name,
                        desc = def.Description,
                        type = def.Type,
                        goal = def.Count,
                        progress = p.progress,
                        completed = p.completed,
                        completedAt = p.completedAt?.ToString("yyyy-MM-dd") ?? ""
                    };
                }).ToArray();

                DllBridge.Instance.SendToDll(session.AccountName!, "achievements", new { items = payload });
            });
        }
        public static void RegisterBotHandler(Server _agentProxy)
        {
            _agentProxy.RegisterClientPacketHandler(Constant.DEW_START_BOT, async (sender, e) =>
            {
                e.CancelTransfer = true;
                var session = e.Proxy.Session;
                if (session == null) return;

                // Cancel old Brain if it exists
                if (session._walkerCts != null)
                {
                    session._walkerCts.Cancel();
                    session._walkerCts.Dispose();
                    session._walkerCts = null;
                }

                // Read bot coordinates
                int x = e.Packet.ReadInt();
                int y = e.Packet.ReadInt();
                int z = e.Packet.ReadInt();
                int r = e.Packet.ReadInt();
                int rId = e.Packet.ReadInt();

                session._lastBotX = x;
                session._lastBotY = y;
                session._lastBotZ = z;
                session._lastBotR = r;
                session._lastBotRegionID = rId;
                session.SaveBotConfig();

                session.TrainingDestination = null;

                bool isDungeon = (session._lastBotRegionID & 0x8000) != 0;
                if (isDungeon) { Logger.Warn("DUNGEON", $"DEST IN DUNGEON"); }
                var destination = isDungeon ?
                    BotPosition.FromDisplayWorldDungeon(x, y, z, (ushort)session._lastBotRegionID) :
                    BotPosition.FromDisplayWorld(x, y, z);

                session.TrainingDestination = destination;

                // Create a new cancellation token for brain lifetime (ignore name lol)
                session._walkerCts = new CancellationTokenSource();
                var ct = session._walkerCts.Token;

                var proxy = e.Proxy;
                session._walker = new AutoWalker(
                    getPosition: () => session.GetEstimatedPosition(),
                    sendMove: pos =>
                    {
                        try
                        {
                            var p = new Packet(0x7021);
                            p.WriteByte(1);
                            p.WriteByte(pos.SectorX);
                            p.WriteByte(pos.SectorY);
                            bool isDungeon = (pos.RegionId & 0x8000) != 0;
                            if (isDungeon)
                            {
                                uint rx = (uint)(int)pos.XOffset;
                                uint ry = (uint)(int)pos.YOffset;
                                int rz = (int)pos.ZOffset;
                                Logger.Debug("SendMove", $"DUNGEON SX={pos.SectorX} SY={pos.SectorY} rawX={rx} rawZ={rz} rawY={ry} | worldX={pos.X:F1} worldY={pos.Y:F1}");
                                p.WriteUInt(rx);
                                p.WriteInt(rz);
                                p.WriteUInt(ry);
                            }
                            else
                            {
                                Logger.Debug("SendMove", $"OVERWORLD SX={pos.SectorX} SY={pos.SectorY} XOff={pos.XOffset:F1} YOff={pos.YOffset:F1}");
                                p.WriteShort((short)pos.XOffset);
                                p.WriteShort((short)pos.ZOffset);
                                p.WriteShort((short)pos.YOffset);
                            }
                            MarkActivity(e.Proxy);
                            proxy.Server.Send(p);

                        }
                        catch (Exception ex)
                        {
                            Logger.Warn("Bot", $"sendMove failed: {ex.Message}");
                        }
                    },
                    sendPacket: p =>
                    {
                        try
                        {
                            PlayerTools.MarkActivity(e.Proxy);
                            e.Proxy.Server.Send(p);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn("Bot", $"sendPacket failed: {ex.Message}");
                        }
                    },
                    session
                );

                session._brain = new BotBrain(
                    session,
                    session._walker,
                    session._attackLoop!,
                    sendPacket: p => proxy.Server.Send(p), // Inject packet sending for teleports
                    rootCt: ct
                );

                // Launch the Brain in the background
                session._botTask = Task.Run(() => session._brain.RunAsync(), ct);


                
            });

            _agentProxy.RegisterClientPacketHandler(Constant.DEW_STOP_BOT, async (sender, e) =>
            {
                e.CancelTransfer = true;
                var session = e.Proxy.Session;
                if (session == null) return;

                if (session._walkerCts != null)
                {
                    session._walkerCts.Cancel();

                    if (session._botTask != null)
                    {
                        try { await session._botTask; }
                        catch {  }
                    }

                    session._walkerCts.Dispose();
                    session._walkerCts = null;
                    session._botTask = null;
                }

                // Reset runtime
                session._botState = BotMode.Idle;
                session.LastTargetUID = 0; // Clear the target UID tracker

                // 3. Immediately push a definitive final "Idle" state broadcast to ImGui
                if (!string.IsNullOrEmpty(session.AccountName))
                {
                    DllBridge.Instance.SendToDll(session.AccountName, "bot_state_update", new
                    {
                        botState = BotMode.Idle.ToString(), // Force "Idle"
                        distanceToTarget = 0.0f,
                        targetUid = 0
                    });
                }

                Logger.Info("Bot", "Bot stopped and UI state flushed to Idle.");
            });
            
            _agentProxy.RegisterClientPacketHandler(Constant.DEW_SKILL_ADD, async (sender, e) =>
            {
                e.CancelTransfer = true;
                var session = e.Proxy.Session;
                if (session == null) return;

                uint skillId = (uint)e.Packet.ReadInt();
                bool isBuff = e.Packet.ReadByte() == 1;

                var skill = await DBConnect.GetSkillById(skillId);
                if (skill == null) return;

                skill.ReadableName = GameObjectNameResolver.Resolve(skill.CodeName);
                session._attackLoop?.AddSkillToUse(skill, isBuff);
                session.PushSkillListsToDll(session.AccountName!);
                session.SaveBotConfig();
            });

            _agentProxy.RegisterClientPacketHandler(Constant.DEW_SKILL_REMOVE, async (sender, e) =>
            {
                e.CancelTransfer = true;
                var session = e.Proxy.Session;
                if (session == null) return;

                uint skillId = (uint)e.Packet.ReadInt();
                bool isBuff = e.Packet.ReadByte() == 1;

                session._attackLoop?.RemoveSkillToUse(skillId, isBuff);
                session.PushSkillListsToDll(session.AccountName!);
                session.SaveBotConfig();
            });

            _agentProxy.RegisterClientPacketHandler(Constant.DEW_SKILL_MOVE, async (sender, e) =>
            {
                e.CancelTransfer = true;
                var session = e.Proxy.Session;
                if (session == null) return;

                uint skillId = (uint)e.Packet.ReadInt();
                int direction = e.Packet.ReadInt();

                session._attackLoop?.MoveSkillPriority(skillId, direction);
                session.PushSkillListsToDll(session.AccountName!);
                session.SaveBotConfig();
            });

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_ITEM_DURABILITY_CHANGE, async (sender, e) =>
            {
                var packet = e.Packet;
                byte slot = packet.ReadByte();
                uint durability = packet.ReadUInt();
                Logger.Debug("DurabilityHandler", $"Slot {slot} durability → {durability}");
                e.Proxy.Session!._attackLoop?.OnDurabilityChanged(slot, durability);
            });

            _agentProxy.RegisterClientPacketHandler(Constant.DEW_BOT_SETTINGS, async (sender, e) =>
            {
                e.CancelTransfer = true;
                var packet = e.Packet.Clone();
                var session = e.Proxy.Session;
                if (session == null) return;

                var settings = session._botSettings ?? new BotSettings();

                // Consumables
                settings.Consumables.BuyHpPotions = packet.ReadByte() == 1;
                settings.Consumables.HpPotionRefillAmount = packet.ReadInt();
                settings.Consumables.HpPotionReturnThreshold = packet.ReadInt();
                settings.Consumables.HPType = (PotionType)packet.ReadInt();

                settings.Consumables.BuyMpPotions = packet.ReadByte() == 1;
                settings.Consumables.MpPotionRefillAmount = packet.ReadInt();
                settings.Consumables.MpPotionReturnThreshold = packet.ReadInt();
                settings.Consumables.MPType = (PotionType)packet.ReadInt();

                settings.Consumables.BuyReturnScrolls = packet.ReadByte() == 1;
                settings.Consumables.ReturnScrollRefillAmount = packet.ReadInt();
                settings.Consumables.ReturnScrollType = (ScrollType)packet.ReadInt();

                settings.Consumables.BuyVigorPotions = packet.ReadByte() == 1;
                settings.Consumables.VigorPotionRefillAmount = packet.ReadInt();
                settings.Consumables.VigorPotionReturnThreshold = packet.ReadInt();

                settings.Consumables.BuyUniversalPills = packet.ReadByte() == 1;
                settings.Consumables.UniversalPillsRefillAmount = packet.ReadInt();
                settings.Consumables.UniversalPillsReturnThreshold = packet.ReadInt();
                settings.Consumables.UniPillType = (UniversalPillType)packet.ReadInt();

                settings.Consumables.BuyPurifPills = packet.ReadByte() == 1;
                settings.Consumables.PurifPillsRefillAmount = packet.ReadInt();
                settings.Consumables.PurifPillsReturnThreshold = packet.ReadInt();
                settings.Consumables.PurificationPillType = (PurificationPillType)packet.ReadInt();

                settings.Consumables.BuySpeedDrugs = packet.ReadByte() == 1;
                settings.Consumables.SpeedDrugsRefillAmount = packet.ReadInt();
                settings.Consumables.SpeedDrugsReturnThreshold = packet.ReadInt();
                settings.Consumables.DrugType = (SpeedDrugsType)packet.ReadInt();

                settings.Consumables.BuyRecKits = packet.ReadByte() == 1;
                settings.Consumables.RecKitsRefillAmount = packet.ReadInt();
                settings.Consumables.RecKitsReturnThreshold = packet.ReadInt();
                settings.Consumables.RecKitsType = (RecoveryKitType)packet.ReadInt();

                settings.Consumables.BuyHGPPotions = packet.ReadByte() == 1;
                settings.Consumables.HGPPotionsRefillAmount = packet.ReadInt();
                settings.Consumables.HGPPotionsReturnThreshold = packet.ReadInt();

                settings.Consumables.BuyAbnPill = packet.ReadByte() == 1;
                settings.Consumables.AbnPillRefillAmount = packet.ReadInt();
                settings.Consumables.AbnPillReturnThreshold = packet.ReadInt();
                settings.Consumables.AbnPillType = (AbnormalPillType)packet.ReadInt();

                settings.Consumables.BuyHorses = packet.ReadByte() == 1;
                settings.Consumables.HorsesRefillAmount = packet.ReadInt();
                settings.Consumables.HorsesType = (AbnormalPillType)packet.ReadInt();

                settings.Consumables.BuyAmmo = packet.ReadByte() == 1;
                settings.Consumables.AmmoRefillAmount = packet.ReadInt();
                settings.Consumables.AmmoReturnThreshold = packet.ReadInt();
                settings.Consumables.AmmoType = (AmmunitionType)packet.ReadInt();

                // Autowalker
                settings.Autowalker.CastSpeedBuffWhileWalking = packet.ReadByte() == 1;
                settings.Autowalker.CastNoiseBuffWhileWalking = packet.ReadByte() == 1;

                // AutoPotion
                settings.AutoPotion.AutoUseHP = packet.ReadByte() == 1;
                settings.AutoPotion.AutoUseMP = packet.ReadByte() == 1;
                settings.AutoPotion.UseVigorPotions = packet.ReadByte() == 1;
                settings.AutoPotion.HPPotHealthThreshold = packet.ReadInt();
                settings.AutoPotion.MPPotManaThreshold = packet.ReadInt();
                settings.AutoPotion.VigorHPMPThreshold = packet.ReadInt();
                settings.AutoPotion.PreferVigorFirst = packet.ReadByte() == 1;
                settings.AutoPotion.HPDelay = packet.ReadInt();
                settings.AutoPotion.MPDelay = packet.ReadInt();
                settings.AutoPotion.HealPets = packet.ReadByte() == 1;
                settings.AutoPotion.HealPetHPThreshold = packet.ReadInt();
                settings.AutoPotion.HealPetsDelay = packet.ReadInt();
                settings.AutoPotion.UseHGPPotions = packet.ReadByte() == 1;
                settings.AutoPotion.HGPPotionsThreshold = packet.ReadInt();
                settings.AutoPotion.AutoUseUnivPills = packet.ReadByte() == 1;
                settings.AutoPotion.AutoUsePurifPills = packet.ReadByte() == 1;

                // Pickup
                settings.Pickup.PickAmmoIfAmountLowerThan = packet.ReadByte() == 1;
                settings.Pickup.AmmoAmount = packet.ReadInt();
                settings.Pickup.PickDelay = packet.ReadByte() == 1;
                settings.Pickup.PickDelayTime = packet.ReadInt();
                settings.Pickup.PickGold = packet.ReadByte() == 1;
                settings.Pickup.PickAll = packet.ReadByte() == 1;

                // BackTownMonitor
                settings.BackTownMonitor.ReturnIfDead = packet.ReadByte() == 1;
                settings.BackTownMonitor.ReturnIfInventoryFull = packet.ReadByte() == 1;

                // Maintenance
                settings.Maintenance.RepairWeapon = packet.ReadByte() == 1;
                settings.Maintenance.RepairDurabilityThreshold = packet.ReadInt();

                // Attack
                settings.Attack.IgnoreDimensionPillars = packet.ReadByte() == 1;
                settings.Attack.UseZerkRightAwayWhenFull = packet.ReadByte() == 1;
                settings.Attack.UseZerkOnNormalGiants = packet.ReadByte() == 1;
                settings.Attack.UseZerkOnPartyMobs = packet.ReadByte() == 1;
                settings.Attack.UseZerkOnPartyGiants = packet.ReadByte() == 1;
                settings.Attack.UseZerkOnUniques = packet.ReadByte() == 1;
                settings.Attack.UseZerkIfNMobsAttackingSimulataneously = packet.ReadByte() == 1;
                settings.Attack.ZerkMobCount = packet.ReadInt();

                session._botSettings = settings;
                session.SaveBotConfig();

                Logger.Info("BotSettings", $"Successfully synchronized and stored settings for {session.CharacterName}");
            });

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_CHARACTER_STUCK, (sender, e) =>
            {
                var p = e.Packet;
                uint uid = p.ReadUInt();
                ushort region = p.ReadUShort();
                float xOffset = p.ReadFloat();
                float z = p.ReadFloat();
                float yOffset = p.ReadFloat();

                if (uid != e.Proxy.Session!.CharacterUID)
                    return;

                var authPos = new BotPosition
                {
                    RegionId = region,
                    XOffset = xOffset,
                    YOffset = yOffset,
                    ZOffset = z
                };

                Logger.Debug("0xB023", $"Stuck at region=0x{region:X4} world=({authPos.X:F1},{authPos.Y:F1})");

                if (e.Proxy.Session != null)
                {
                    e.Proxy.Session.RegionId = region;
                    e.Proxy.Session.RawX = (short)xOffset;
                    e.Proxy.Session.RawY = (short)yOffset;
                    e.Proxy.Session.Z = (short)z;
                    e.Proxy.Session.SectorX = authPos.SectorX;
                    e.Proxy.Session.SectorY = authPos.SectorY;
                    e.Proxy.Session.WorldX = (int)authPos.X;
                    e.Proxy.Session.WorldY = (int)authPos.Y;

                    e.Proxy.Session._walker?.HandleStuckPacket(authPos);
                }
            });
        }

        #endregion

        #region - Sorting -

        // private
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

        /// <summary>
        /// Sorts player inventory by type and name.
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="sortMode"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<SortResult> SortInventoryStep(Proxy proxy, string sortMode = "type", CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (proxy.Session!.Inventory.Slots.IsEmpty)
                return SortResult.Aborted;

            if (proxy.Session!.Inventory.Slots.Values.Any(s => s.CodeName128 == "MALL_PENDING"))
            {
                PlayerTools.SendToProxyChat(proxy, ChatType.Notice, null, "You have pending mall items. Teleport to resync before sorting.");
                return SortResult.Aborted;
            }
            else if (proxy.Session!.Inventory.Slots.Values.Any(s => s.CodeName128 == "UNKNOWN_PET_TRANSFER"))
            {
                PlayerTools.SendToProxyChat(proxy, ChatType.Notice, null, "You have unsynced items. Teleport to resync before sorting.");
                return SortResult.Aborted;
            }

            // Snapshot current inventory slots (slot >= 13 = player inventory)
            var slots = proxy.Session!.Inventory.Slots
                .Where(kvp => kvp.Key >= 13)
                .OrderBy(kvp => kvp.Key)
                .ToList();

            // STACK
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
            if (didStack) return SortResult.Continue;

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
                    return SortResult.Continue;
                }
            }

            // Sort
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
                    return SortResult.Continue;
                }
            }

            return SortResult.Completed;
        }
        
        /// <summary>
        /// Sorts pet inventory by type and name
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="petUID"></param>
        /// <param name="sortMode"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<SortResult> SortPetInventoryStep(Proxy proxy, uint petUID, string sortMode = "type", CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!proxy.Session!.Inventory.Pets.TryGetValue(petUID, out var petInv))
                return SortResult.Aborted;

            if (petInv.Inventory.IsEmpty)
                return SortResult.Aborted;

            var slots = petInv.Inventory
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
            if (didStack) return SortResult.Continue;

            // Pack
            const byte start = 1;
            for (int i = 0; i < slots.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                byte expectedSlot = (byte)(start + i);
                var (slot, item) = slots[i];
                if (slot != expectedSlot)
                {
                    await SendPetMoveAndWait(proxy, petUID, slot, expectedSlot, (ushort)item.Stack, cancellationToken);

                    return SortResult.Continue;
                }
            }

            // Sort
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
                    return SortResult.Continue;
                }
            }

            return SortResult.Completed;
        }

        /// <summary>
        /// Logical sort should be as follows:
        /// Pet Scrolls -> slots 13 & 14 (first 2) starts with ITEM_COS AND ends with "SCROLL"
        /// Reverse Return, Instant Return scroll, and ITEM_MOVE_SPEED_UP next (Contains("REVERSE_RETURN_SCROLL") OR RETURN_SCROLL_HIGH_SPEED
        /// if any slot contains an HP (ITEM_ETC_HP_POTION_01 - 05)item with a stack higher than 20, put that single stack in next slot, the rest are deferred to the end of the slots, same with MP potions for the next slot
        /// Next should be all quest items contains("SNOWFLAKE") or "QNO" or "QSP"
        /// Next should be potion of growth contains PET_GROWTH_POTION, ITEM_PET_SKILL_FIRE or COLD or LIGHTNING
        /// Last should be all equipment, and deferred items. (this may change as im thinking of more ways to improve this)
        /// </summary>
        public static async Task<SortResult> SortInventoryLogical(Proxy proxy, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (proxy.Session!.Inventory.Slots.IsEmpty)
                return SortResult.Aborted;

            if (proxy.Session!.Inventory.Slots.Values.Any(s => s.CodeName128 == "MALL_PENDING"))
            {
                PlayerTools.SendToProxyChat(proxy, ChatType.Notice, null, "You have pending mall items. Teleport to resync before sorting.");
                return SortResult.Aborted;
            }
            else if (proxy.Session!.Inventory.Slots.Values.Any(s => s.CodeName128 == "UNKNOWN_PET_TRANSFER"))
            {
                PlayerTools.SendToProxyChat(proxy, ChatType.Notice, null, "You have unsynced items. Teleport to resync before sorting.");
                return SortResult.Aborted;
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
            if (didStack) return SortResult.Continue;

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
                    return SortResult.Continue;
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
                .Where(s => IsHpPotion(s.Value.CodeName128!) && s.Value.Stack > 20)
                .OrderByDescending(s => s.Value.Stack)
                .FirstOrDefault();

            var largeMpCandidate = slots
                .Where(s => IsMpPotion(s.Value.CodeName128!) && s.Value.Stack > 20)
                .OrderByDescending(s => s.Value.Stack)
                .FirstOrDefault();

            // Build ordered list exactly in the sequence you wanted
            var sorted = new List<KeyValuePair<byte, SR_Item>>();

            // Pet Scrolls
            sorted.AddRange(slots
                .Where(s => IsPetScroll(s.Value.CodeName128!))
                .OrderBy(s => s.Value.CodeName128)
                .ThenByDescending(s => s.Value.Stack));

            // Special scrolls
            sorted.AddRange(slots
                .Where(s => IsSpecialScroll(s.Value.CodeName128!))
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
                .Where(s => IsQuestItem(s.Value.CodeName128!) && !placed.Contains(s.Key))
                .OrderBy(s => s.Value.CodeName128!)
                .ThenByDescending(s => s.Value.Stack));

            placed.UnionWith(sorted.Skip(placed.Count).Select(s => s.Key));

            // Growth / pet skill potions
            sorted.AddRange(slots
                .Where(s => IsGrowthPotion(s.Value.CodeName128!) && !placed.Contains(s.Key))
                .OrderBy(s => s.Value.CodeName128)
                .ThenByDescending(s => s.Value.Stack));

            placed.UnionWith(sorted.Skip(placed.Count).Select(s => s.Key));

            // Everything else that is NOT a potion
            sorted.AddRange(slots
                .Where(s => !placed.Contains(s.Key) &&
                            !IsHpPotion(s.Value.CodeName128!) &&
                            !IsMpPotion(s.Value.CodeName128!))
                .OrderBy(s => s.Value.CodeName128)
                .ThenByDescending(s => s.Value.Stack));

            // Deferred HP and MP potions
            sorted.AddRange(slots
                .Where(s => !placed.Contains(s.Key) &&
                            (IsHpPotion(s.Value.CodeName128!) || IsMpPotion(s.Value.CodeName128!)))
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
                    return SortResult.Continue;
                }
            }

            return SortResult.Completed;
        }

        #endregion

        #region - Play Time -

        public static void MarkActivity(Proxy proxy)
        {
            var session = proxy.Session;
            if (session == null) return;

            var now = DateTime.UtcNow;

            session.LastActivity = now;
        }
        public static async Task RunSessionTracker(Proxy proxy, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(1000, token);

                var session = proxy.Session;
                if (session == null)
                    continue;

                var now = DateTime.UtcNow;

                int afkTime = SettingsLoader.Settings?.Proxy?.AFKTime ?? 60;
                double idle = (now - session.LastActivity).TotalSeconds;

                bool wasAfk = session.IsAfk;
                bool isAfkNow = idle >= afkTime;

                if (wasAfk != isAfkNow)
                {
                    session.IsAfk = isAfkNow;

                    if (isAfkNow)
                    {
                        string afk = SettingsLoader.Settings?.Proxy?.AFKMessage
                                     ?? "{NAME}, You are AFK";

                        SendToProxyChat(proxy,
                            ChatType.Notice,
                            null,
                            SettingsLoader.FormatPlayerMessage(afk, proxy));
                    }
                    else
                    {
                        string back = SettingsLoader.Settings?.Proxy?.BackFromAfkMessage
                                      ?? "Welcome back!";

                        SendToProxyChat(proxy,
                            ChatType.Notice,
                            null,
                            SettingsLoader.FormatPlayerMessage(back, proxy));
                    }
                }

                // normal playtime tracking
                if (!session.IsAfk)
                {
                    session.AccumulatedPlayTime += TimeSpan.FromSeconds(1);
                    proxy.CheckPlaytimeReward(session);

                    long totalMinutes = (long)(session.TotalPlayTime + session.AccumulatedPlayTime).TotalMinutes;
                    await AchievementService.OnPlaytimeTick(session.CharacterName!, totalMinutes, proxy);


                }
            }
        }    

        #endregion

        #region - Tools -

        public static void SendToProxyChat(Proxy proxy, ChatType type, string? senderName, string message)
        {
            var pkt = new Packet(Constant.SERVER_CHAT);
            pkt.WriteByte((byte)type);

            switch (type)
            {
                case ChatType.General: 
                    pkt.WriteUInt(0);
                    break;

                case ChatType.PrivateMessage:
                case ChatType.PartyChat:
                case ChatType.GuildChat:
                case ChatType.Global:
                case ChatType.Academy:
                    pkt.WriteAscii(senderName ?? "FoxProxy");
                    break;

                case ChatType.Notice:
                    break;
            }

            pkt.WriteAscii(message);
            proxy.Client.Send(pkt);
        }

        #endregion

        #region - Helpers -
        
        public static async Task CheckLevelUp(Proxy proxy)
        {
            var session = proxy.Session;
            if (session?.PlayerStats == null)
            {
                Logger.Warn("LevelTracker", "CheckLevelUp: session or PlayerStats is null");
                return;
            }

            while (true)
            {
                byte currentLevel;
                byte nextLevel;
                ulong threshold;
                ulong currentExp;

                lock (session)
                {
                    currentLevel = (byte)session.PlayerStats.CurrentLevel;
                    nextLevel = (byte)(currentLevel + 1);
                    currentExp = session.CumulativeExp;

                    if (!Overseer.ExpTableCumulative.TryGetValue(currentLevel, out threshold))
                    {
                        Logger.Warn("LevelTracker", $"CheckLevelUp: no cumulative threshold for level {currentLevel}");
                        return;
                    }
                    Logger.Debug("LevelTracker", $"CheckLevelUp: level={currentLevel} cumExp={currentExp} threshold={threshold}");

                    if (session.CumulativeExp < threshold)
                    {
                        Logger.Debug("LevelTracker", $"CheckLevelUp: not enough exp ({currentExp} < {threshold}), done");
                        return;
                    }

                    session.PlayerStats.CurrentLevel = nextLevel;
                }

                bool alreadyClaimed = await DBConnect.HasClaimedLevelRewardAsync(session.CharacterName!, nextLevel);
                if (alreadyClaimed)
                {
                    Logger.Debug("LevelTracker", $"CheckLevelUp: level {nextLevel} already claimed, continuing");
                    continue;
                }

                bool claimed = await DBConnect.ClaimLevelRewardAsync(session.CharacterName!, nextLevel);
                if (!claimed)
                {
                    Logger.Debug("LevelTracker", $"CheckLevelUp: claim insert failed for level {nextLevel} (race), continuing");
                    continue;
                }

                Logger.Info("LevelTracker", $"{session.CharacterName} reached level {nextLevel}");
                await OnPlayerLevelUp(proxy, nextLevel);
            }
        }
        private static async Task OnPlayerLevelUp(Proxy proxy, byte newLevel)
        {
            // Level up
            DllBridge.Instance.SendToDll(proxy.Session!.AccountName!, "lvl_update", new { lvl = (int)newLevel });

            // Reward logic and stuff
            if (!Overseer.LevelRewardOptions.TryGetValue(newLevel, out var options) || options.Count == 0)
                return;

            var codeNames = options.Select(o => o.CodeName);
            var iconPaths = await DBConnect.GetItemIconPaths(codeNames);

            proxy.Session!.UnclaimedRewards.Add(newLevel);
            await DBConnect.AddUnclaimedRewardAsync(proxy.Session.CharacterName!, newLevel);
            proxy.Session!.PendingLevelReward = newLevel;

            DllBridge.Instance.SendToDll(proxy.Session.AccountName!, "level_reward", new
            {
                level = newLevel,
                options = options.Select(o => new {
                    code = o.CodeName,
                    plus = o.Plus,
                    qty = o.Qty,
                    name = o.DisplayName,
                    icon = iconPaths.TryGetValue(o.CodeName, out var path)
                           ? path.Replace(".ddj", ".png").Replace("\\", "/")
                           : ""
                }).ToArray()
            });

            if (proxy.Session.UnclaimedRewards.Count > 0)
                DllBridge.Instance.SendToDll(proxy.Session.AccountName!, "unclaimed_rewards", new
                {
                    levels = proxy.Session.UnclaimedRewards.Select(b => (int)b).ToArray()
                });
        }
        public static bool IsDungeon(ushort regionId)
        {
            return (regionId & 0x8000) != 0;
        }
        
        
        #endregion
    }
}
