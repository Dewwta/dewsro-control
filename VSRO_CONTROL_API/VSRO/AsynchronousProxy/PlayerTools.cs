using CoreLib.Models;
using CoreLib.Tools.Logging;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Collections.Concurrent;
using System.Text;
using VSRO_CONTROL_API.Settings;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Achivements;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Framework;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Tracking;
using VSRO_CONTROL_API.VSRO.DTO;
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

        #region - Static -

        private static readonly ConcurrentDictionary<uint, string> _codeCache = new();

        #endregion

        #region - Handler Registry (SERVER) -

        public static void RegisterChardataHandler(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_CHARDATA, async (sender, e) =>
            {
                // This packet is the reason we can't have nice things.
                try
                {
                    var packet = e.Packet.Clone();
                    var inv = e.Proxy.Inventory;
                    inv.Slots.Clear();
                    inv.Equipment.Clear();

                    Logger.Debug("ChardataHandler", $"START PACKET | LENGTH={packet.RemainingRead()}");

                    uint lastLoginTime = packet.ReadUInt();
                    Logger.Debug("ChardataHandler", $"serverTime={lastLoginTime}");

                    uint refObjId = packet.ReadUInt();
                    Logger.Debug("ChardataHandler", $"refObjId={refObjId}");

                    byte scale = packet.ReadByte();
                    byte curLevel = packet.ReadByte();
                    byte maxLevel = packet.ReadByte();

                    Logger.Debug("ChardataHandler", $"scale={scale} curLevel={curLevel} maxLevel={maxLevel}");

                    ulong expOffset = packet.ReadULong();
                    uint sExpOffset = packet.ReadUInt();

                    
                    Logger.Debug("ChardataHandler", $"expOffset={expOffset} sExpOffset={sExpOffset}");

                    ulong remainGold = packet.ReadULong();
                    uint remainSkillPoint = packet.ReadUInt();
                    ushort remainStatPoint = packet.ReadUShort();

                    Logger.Debug("ChardataHandler", $"gold={remainGold} skill={remainSkillPoint} stat={remainStatPoint}");

                    // --- THE MISSING STATS (Bridge between Stats and Inventory) ---
                    byte remainHwanCount = packet.ReadByte(); // Zerk gauge bubbles
                    uint gatherExp = packet.ReadUInt();       // Academy Exp
                    uint hp = packet.ReadUInt();              // Current HP
                    uint mp = packet.ReadUInt();              // Current MP
                    byte autoInvestExp = packet.ReadByte();   // Beginner icon / Auto invest
                    byte dailyPk = packet.ReadByte();         // Daily PK count
                    ushort totalPk = packet.ReadUShort();     // Total PK count
                    uint pkPenaltyPoint = packet.ReadUInt();  // PK Penalty points
                    byte hwanLevel = packet.ReadByte();       // Zerk Title level
                    byte freePvp = packet.ReadByte();         // Cape / Free PVP flag00

                    Logger.Debug("ChardataHandler", $"HP={hp} MP={mp} Zerk={remainHwanCount}");

                    byte invSize = packet.ReadByte();         // 0x61
                    byte itemCount = packet.ReadByte();       // 0x1C

                    Logger.Debug("ChardataHandler", $"invSize={invSize} itemCount={itemCount}");

                    e.Proxy.Session!.PlayerStats = new PlayerStats
                    {
                        CurrentHP = hp,
                        CurrentMP = mp,
                        ZerkLevel = remainHwanCount,
                        CurrentLevel = curLevel,
                        UnusedStatPoints = remainStatPoint,
                        RemainingGold = remainGold,
                        RemainingSkillPoints = remainSkillPoint,
                    };

                    if (Overseer.ExpTableCumulative.TryGetValue((byte)(curLevel - 1), out ulong baseExp))
                        e.Proxy.Session.CumulativeExp = baseExp + expOffset;
                    else
                        e.Proxy.Session.CumulativeExp = expOffset; // level 1, no base

                    DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "session_sync", new
                    {
                        sessionSeconds = (int)e.Proxy.Session.AccumulatedPlayTime.TotalSeconds,
                        sessionKills = e.Proxy.Session.SessionKills,
                        isAfk = e.Proxy.Session.IsAfk ? 1 : 0  // int not bool
                    });

                    DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "unclaimed_rewards", new
                    {
                        levels = e.Proxy.Session.UnclaimedRewards.Select(b => (int)b).ToArray()
                    });

                    for (int i = 0; i < itemCount; i++)
                    {
                        Logger.Debug("ChardataHandler", $"--- ITEM {i} ---");

                        // 1. SLOT & RENT
                        byte slot = packet.ReadByte();
                        uint rentType = packet.ReadUInt();
                        Logger.Debug("ChardataHandler", $"ITEM {i}: slot={slot} rentType={rentType}");
                        if (rentType == 1)
                        {
                            //    CanDelete      RentalPeriodBegin  RentalPeriodEnd
                            packet.ReadUShort(); packet.ReadUInt(); packet.ReadUInt();
                        }
                        else if (rentType == 2)
                        {
                            //    CanDelete          CanRecharge        MeterRateTime
                            packet.ReadUShort(); packet.ReadUShort(); packet.ReadUInt();
                        }
                        else if (rentType == 3)
                        {

                            packet.ReadUShort(); // CanDelete
                            packet.ReadUInt();   // PeriodBegin
                            packet.ReadUInt();   // PeriodEnd
                            packet.ReadUShort(); // CanRecharge
                            packet.ReadUInt();   // PackingTime
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
                        Logger.Debug("ChardataHandler", $"{itemInfo.item.CodeName}: T1={itemInfo.item.T1} | T2={itemInfo.item.T2} | T3={itemInfo.item.T3} | T4={itemInfo.item.T4}");

                        if (itemInfo.item.T1 == 1) // NPC/Character objects
                        {
                            // Need to determine structure
                            Logger.Warn("ChardataHandler", $"T1=1 object: {itemInfo.item.CodeName} T2={itemInfo.item.T2} T3={itemInfo.item.T3} T4={itemInfo.item.T4} remaining={packet.RemainingRead()}");
                        }
                        else if(itemInfo.item.T1 == 3) // ITEM_
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
                                if (itemInfo.item.T3 == 1) // Pet items
                                {
                                    // Dont ask me how this works, i got incredibly lucky and im still questioning it.
                                    // This does NOT match ANY of the documentation for this packet.
                                    byte state = packet.ReadByte(); // (1 = Never Summoned (Not Active), 2 = Summoned (Not Active), 3 = Alive (Actually Active, 4 = Dead (The only correct value from the docs.)
                                    Logger.Debug("PetHandler:Chardata", $"STATE={state}");
                                    if (state != 1)
                                    {
                                        uint cosObjId = packet.ReadUInt();   // RefObjID
                                        ushort nameLen = packet.ReadUShort();
                                        for (int s = 0; s < nameLen; s++)
                                            packet.ReadByte();

                                        if (itemInfo.item.T4 == 2) // AbilityPet
                                            packet.ReadUInt();     // SecondsToRentEndTime

                                        byte unk02 = packet.ReadByte();
                                        if (unk02 != 0)
                                        {
                                            int extraBytes = (itemInfo.item.T4 == 2) ? 14 : 16;
                                            for (int x = 0; x < extraBytes; x++)
                                                packet.ReadByte();
                                        }

                                        finalStack = 1;
                                    }
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
                               
                                if (code.Contains("ATTRSTONE") || code.Contains("MAGICSTONE"))
                                {
                                    packet.ReadByte(); // AssimilationProbability
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
                            inv.Slots[slot] = ((int)refItemId, itemInfo.item.CodeName, finalStack, itemInfo.item.MaxStack);
                        else
                            inv.Equipment[slot] = ((int)refItemId, itemInfo.item.CodeName, finalStack, itemInfo.item.MaxStack);


                        Logger.Debug("ChardataHandler", $"SLOT [{slot}] {itemInfo.item.CodeName} ({finalStack}/{itemInfo.item.MaxStack}) | remainingPacketRead={packet.RemainingRead()}");
                    }
                    
                    byte avatarSize = packet.ReadByte();
                    byte avatarCount = packet.ReadByte();

                    for (int i = 0; i < avatarCount; i++)
                    {
                        byte slot = packet.ReadByte();
                        uint rentType = packet.ReadUInt();

                        if (rentType == 1) { packet.ReadUShort(); packet.ReadUInt(); packet.ReadUInt(); }
                        else if (rentType == 2) { packet.ReadUShort(); packet.ReadUShort(); packet.ReadUInt(); }
                        else if (rentType == 3) { packet.ReadUShort(); packet.ReadUInt(); packet.ReadUInt(); packet.ReadUShort(); packet.ReadUInt(); }

                        uint refItemId = packet.ReadUInt();
                        var itemInfo = await DBConnect.GetItemRecord(refItemId);

                        // Still need to consume the equipment bytes regardless
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
                            inv.Avatars[slot] = ((int)refItemId, itemInfo.item.CodeName, 1, 1);
                            Logger.Debug("ChardataHandler", $"AVATAR [{slot}] {itemInfo.item.CodeName}");
                        }
                    }

                    e.Proxy.Inventory.IsReady = true;
                }
                catch (Exception ex)
                {
                    Logger.Error("ChardataHandler",
                        $"CRASH parsing CHARDATA: {ex.Message}\n{ex.StackTrace}");
                }

                var payload = new
                {
                    hp = e.Proxy.Session.PlayerStats.CurrentHP,
                    mp = e.Proxy.Session.PlayerStats.CurrentMP,
                    sessionKills = e.Proxy.Session.SessionKills,
                    unusedStatPoints = e.Proxy.Session.PlayerStats.UnusedStatPoints,
                    currentLevel = e.Proxy.Session.PlayerStats.CurrentLevel,
                    gold = e.Proxy.Session.PlayerStats.RemainingGold,
                };
                DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "char_init", payload);

                if (e.Proxy.Session.UnclaimedRewards.Count > 0)
                    DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "unclaimed_rewards", new
                    {
                        levels = e.Proxy.Session.UnclaimedRewards.Select(b => (int)b).ToArray()
                    });

            });
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_STATS, async (sender, e) =>
            {
                try
                {
                    var proxy = e.Proxy;
                    var packet = e.Packet.Clone();

                    // Unused
                    uint PhyAtkMin = packet.ReadUInt();
                    uint PhyAtkMax = packet.ReadUInt();
                    uint MagAtkMin = packet.ReadUInt();
                    uint MagAtkMax = packet.ReadUInt();

                    ushort PhyDef = packet.ReadUShort();
                    ushort MagDef = packet.ReadUShort();
                    ushort HitRate = packet.ReadUShort();
                    ushort ParryRate = packet.ReadUShort();

                    uint MaxHP = packet.ReadUInt();
                    uint MaxMP = packet.ReadUInt();

                    ushort STR = packet.ReadUShort();
                    ushort INT = packet.ReadUShort();

                    if (proxy != null && proxy.Session != null)
                    {
                        e.Proxy.Session!.PlayerStats!.STR = STR;
                        e.Proxy.Session!.PlayerStats!.INT = INT;
                        e.Proxy.Session!.PlayerStats!.MaxHP = MaxHP;
                        e.Proxy.Session!.PlayerStats.MaxMP = MaxMP;

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
                        Logger.Error("ServerStatsHandler", $"Character session was null!: STR={STR} INT={INT} MaxHP={MaxHP} MaxMP={MaxMP} HitRate={HitRate} ParryRate={ParryRate}");
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

                    var inv = e.Proxy.Inventory;

                    switch (moveType)
                    {
                        case ItemMovement.InventoryToInventory:     // 0x00
                            {
                                byte sourceSlot = packet.ReadByte();
                                byte destSlot = packet.ReadByte();
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

                                if (src.ItemID == 0)
                                    return;

                                inv.Equipment.TryGetValue(destSlot, out var dstEquip);
                                inv.Slots.TryGetValue(destSlot, out var dstInv);

                                var dst = destIsEquip ? dstEquip : dstInv;

                                bool dstExists = dst.ItemID != 0;

                                // ---- STACK
                                if (!sourceIsEquip && !destIsEquip &&
                                    dstExists &&
                                    dst.ItemID == src.ItemID &&
                                    dst.MaxStack > 1)
                                {
                                    int total = dst.Stack + src.Stack;

                                    if (total <= dst.MaxStack)
                                    {
                                        inv.Slots[destSlot] = (dst.ItemID, dst.CodeName, total, dst.MaxStack);
                                        inv.Slots.TryRemove(sourceSlot, out _);
                                    }
                                    else
                                    {
                                        int remainder = total - dst.MaxStack;
                                        inv.Slots[destSlot] = (dst.ItemID, dst.CodeName, dst.MaxStack, dst.MaxStack);
                                        inv.Slots[sourceSlot] = (src.ItemID, src.CodeName, remainder, src.MaxStack);
                                    }

                                    return;
                                }

                                // ---- SWAP
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
                                    // ---- MOVE ONLY
                                    if (destIsEquip)
                                        inv.Equipment[destSlot] = src;   // keep full item intact
                                    else
                                        inv.Slots[destSlot] = src;

                                    // FIX: DO NOT overwrite with empty struct
                                    if (sourceIsEquip)
                                        inv.Equipment.TryRemove(sourceSlot, out _);   // or leave as empty-slot behavior
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
                                if (itemInfo.item.T2 == 1) // Equipment — skip all equipment bytes
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

                                inv.Slots[slot] = ((int)refItemId, itemInfo.item.CodeName, finalStack, itemInfo.item.MaxStack);
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
                                if (e.Proxy.SpawnedObjects.TryGetValue(e.Proxy.LastTargetUID, out var spawnInfo))
                                    npcObjId = spawnInfo.RefObjID;

                                if (Overseer.ShopLookup.TryGetValue(((int)npcObjId, shopTab, shopSlot), out var shopItem))
                                {
                                    var itemInfo = await DBConnect.GetItemRecord((uint)shopItem.RefItemID);
                                    int maxStack = itemInfo.success ? itemInfo.item!.MaxStack : 1;

                                    foreach (var slot in toSlots)
                                        inv.Slots[slot] = (shopItem.RefItemID, shopItem.CodeName, quantity, maxStack);

                                    Logger.Debug("ItemMoveHandler", $"Bought {shopItem.CodeName} x{quantity} → slots [{string.Join(",", toSlots)}]");
                                }
                                else
                                {
                                    Logger.Warn("ItemMoveHandler", $"BUY: unresolved NPC=0x{e.Proxy.LastTargetUID:X} objId={npcObjId} tab={shopTab} slot={shopSlot}");
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
                                        Logger.Debug("ItemMoveHandler", $"Sold {item.CodeName} x{quantity} from slot {playerSlot} for {goldReceived}g (removed)");
                                    }
                                    else
                                    {
                                        inv.Slots[playerSlot] = (item.ItemID, item.CodeName, remaining, item.MaxStack);
                                        Logger.Debug("ItemMoveHandler", $"Sold {item.CodeName} x{quantity} from slot {playerSlot} for {goldReceived}g ({remaining} remain)");
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
                                    inv.Slots[slot] = ((int)refItemId, itemInfo.item.CodeName, finalStack, itemInfo.item.MaxStack);
                                    Logger.Debug("ItemMoveHandler", $"Pet picked up quest item {itemInfo.item.CodeName} x{finalStack} → PLAYER slot {slot}");
                                }
                                else if (itemInfo.item.T2 == 3 && itemInfo.item.T3 == 9)
                                {
                                    inv.Slots[slot] = ((int)refItemId, itemInfo.item.CodeName, finalStack, itemInfo.item.MaxStack);
                                    Logger.Debug("ItemMoveHandler", $"Pet picked up event item {itemInfo.item.CodeName} x{finalStack} → PLAYER slot {slot}");

                                }
                                else
                                {
                                    if (!inv.Pets.ContainsKey(petUID))
                                        inv.Pets[petUID] = new();
                                    inv.Pets[petUID].Inventory[slot] = ((int)refItemId, itemInfo.item.CodeName, finalStack, itemInfo.item.MaxStack);
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
                                    inv.Slots[slot] = ((int)refItemId, itemInfo.item.CodeName, finalStack, itemInfo.item.MaxStack);
                                    Logger.Debug("ItemMoveHandler", $"Pet picked up quest item {itemInfo.item.CodeName} x{finalStack} → PLAYER slot {slot}");
                                }
                                else
                                {
                                    // Route to PET inventory
                                    if (!inv.Pets.ContainsKey(petUID))
                                        inv.Pets[petUID] = new();

                                    inv.Pets[petUID].Inventory[slot] = ((int)refItemId, itemInfo.item.CodeName, finalStack, itemInfo.item.MaxStack);
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
                                    Logger.Debug("ItemMoveHandler", $"Pet→Player: {petItem.CodeName} x{petItem.Stack} → slot {playerSlot}");
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
                                            Logger.Debug("ItemMoveHandler", $"Pet→Player: {item.CodeName} x{item.Stack} → slot {playerSlot} (from pet 0x{pet.Key:X})");
                                            break;
                                        }
                                    }
                                }

                                if (!found)
                                {
                                    inv.Slots[playerSlot] = (0, "UNKNOWN_PET_TRANSFER", 1, 1);
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
                                    // Store in the correct pet based on slot range
                                    // Try specified pet first, fall back to any pet that has nearby slots
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
                                    Logger.Debug("ItemMoveHandler", $"Player→Pet: {item.CodeName} → pet 0x{targetPet:X} slot {petSlot}");
                                }
                                break;
                            }

                        case ItemMovement.PetToPet:                 // 0x10
                            {
                                uint petUID = packet.ReadUInt();
                                byte srcSlot = packet.ReadByte();
                                byte destSlot = packet.ReadByte();
                                ushort qty = packet.ReadUShort();

                                // Find the pet inventory (may be under a different UID)
                                ConcurrentDictionary<byte, (int ItemID, string CodeName, int Stack, int MaxStack)>? petInv = null;
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

                                    if (dst.ItemID != 0)
                                    {
                                        petInv[destSlot] = src;
                                        petInv[srcSlot] = dst;
                                    }
                                    else
                                    {
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
                                    Logger.Debug("ItemMoveHandler", $"Unequipped avatar {avatarItem.CodeName} slot {avatarSlot} → inventory slot {playerSlot}");
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
                                    // If there's already an avatar in that slot, swap it back to inventory
                                    if (inv.Avatars.TryGetValue(avatarSlot, out var oldAvatar))
                                        inv.Slots[playerSlot] = oldAvatar;
                                    else
                                        inv.Slots.TryRemove(playerSlot, out _);

                                    inv.Avatars[avatarSlot] = item;
                                    Logger.Debug("ItemMoveHandler", $"Equipped avatar {item.CodeName} slot {playerSlot} → avatar slot {avatarSlot}");
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
                                    Logger.Debug("ItemMoveHandler", $"Deposited {item.CodeName} slot {playerSlot} → storage slot {storageSlot}");
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
                                    Logger.Debug("ItemMoveHandler", $"Withdrew {item.CodeName} storage slot {storageSlot} → slot {playerSlot}");
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

                                    if (dst.ItemID != 0)
                                    {
                                        inv.Storage[destSlot] = src;
                                        inv.Storage[srcSlot] = dst;
                                    }
                                    else
                                    {
                                        inv.Storage[destSlot] = src;
                                        inv.Storage.TryRemove(srcSlot, out _);
                                    }
                                    Logger.Debug("ItemMoveHandler", $"Storage move: {src.CodeName} slot {srcSlot} → slot {destSlot}");
                                }
                                break;
                            }

                        case ItemMovement.InventoryToGround:        // 0x07
                            {
                                byte playerSlot = packet.ReadByte();

                                if (inv.Slots.TryGetValue(playerSlot, out var item))
                                {
                                    inv.Slots.TryRemove(playerSlot, out _);
                                    Logger.Debug("ItemMoveHandler", $"Dropped {item.CodeName} from slot {playerSlot}");
                                }
                                break;
                            }

                        case ItemMovement.ItemMallToInventory:      // 0x18
                            {
                                byte unk1 = packet.ReadByte();  // 0x1B
                                byte unk2 = packet.ReadByte();  // 0x04
                                byte mallTab = packet.ReadByte();
                                byte mallSlot = packet.ReadByte();
                                byte mallItemInfo = packet.ReadByte();
                                byte slotCount = packet.ReadByte();

                                var toSlots = new List<byte>();
                                for (int i = 0; i < slotCount; i++)
                                    toSlots.Add(packet.ReadByte());

                                ushort quantity = packet.ReadUShort();

                                Logger.Debug("ItemMoveHandler",
                                    $"Mall buy: tab={mallTab} slot={mallSlot} qty={quantity} → slots [{string.Join(",", toSlots)}]");

                                // Mall items can't be resolved from ShopLookup (different system)
                                // Mark slots as needing identification
                                foreach (var slot in toSlots)
                                {
                                    inv.Slots[slot] = (0, "MALL_PENDING", quantity, 1);
                                }
                                break;
                            }

                        case ItemMovement.GameServerToInventory:    // 0x0E
                            {
                                ushort slot = packet.ReadUShort();     // Why is slot a ushort? no idea

                                uint padding = packet.ReadUInt();      // 00 00 00 00

                                uint itemObjId = packet.ReadUInt();    // unique item ID

                                ushort quantity = packet.ReadUShort(); // stack

                                var res = await DBConnect.GetItemRecord(itemObjId);
                                if (res.success)
                                {
                                    inv.Slots[(byte)slot] = ((int)itemObjId, res.item!.CodeName, quantity, res.item.MaxStack);

                                    Logger.Debug("GameServerToInventory:ItemMoveHandler",
                                        $"GS->Inv move slot={slot}, uid={itemObjId}, qty={quantity}, name={res.item.CodeName}");
                                }
                                else
                                {
                                    Logger.Warn("GameServerToInventory:ItemMoveHandler",
                                        $"Item record failed for uid {itemObjId} (slot {slot})");

                                    // Fallback: still mark the slot so inventory doesn't desync
                                    inv.Slots[(byte)slot] = ((int)itemObjId, $"UNKNOWN_{itemObjId}", quantity, 500);
                                }
                                break;
                            }

                        case ItemMovement.InventoryToGameServer: // 0x0F
                            {

                                byte playerSlot = packet.ReadByte(); // 2F

                                if (inv.Slots.TryRemove(playerSlot, out var item))
                                {
                                    Logger.Debug("ItemMoveHandler",
                                        $"I→G move: {item.CodeName} slot={playerSlot}");
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

                    var inv = e.Proxy.Inventory;

                    if (!inv.Slots.TryGetValue(slot, out var item))
                        return;

                    if (remainingStack == 0)
                    {
                        inv.Slots.TryRemove(slot, out _);

                        Logger.Debug("ItemUseHandler", $"Removed item from slot {slot} (stack reached 0)");
                    }
                    else
                    {
                        inv.Slots[slot] = (
                            item.ItemID,
                            item.CodeName,
                            remainingStack,
                            item.MaxStack
                        );
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
                    e.Proxy.IsSorting = true;
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
                            e.Proxy.IsSorting = false;
                        }
                    });
                }
                else if (message.StartsWith("!ach", StringComparison.OrdinalIgnoreCase))
                {
                    e.CancelTransfer = true;

                    var parts = message.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    string charName = e.Proxy.Session?.CharacterName!;

                    // !ach list
                    if (parts.Length == 1 || parts[1].Equals("list", StringComparison.OrdinalIgnoreCase))
                    {
                        var achievements = await DBConnect.GetAchievementNamesAsync(charName);

                        if (achievements.Count == 0)
                        {
                            PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, "You have no achievements.");
                            return;
                        }

                        // Build pages of up to 255 chars
                        var pages = BuildPages(achievements, maxLen: 240, separator: ", ");
                        foreach (var page in pages)
                            PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, page);
                    }
                    // !ach <name>
                    else
                    {
                        string achName = parts[1].Trim();
                        var progress = await DBConnect.GetAchievementProgressAsync(charName, achName);

                        if (progress == null)
                        {
                            PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, $"Achievement '{achName}' not found.");
                            return;
                        }

                        string status = progress.Value.completed
                            ? $"Completed on {progress.Value.completedAt:yyyy-MM-dd}"
                            : $"In progress: {progress.Value.progress}";
                        
                        var ach = AchievementLoader.Definitions!.Achievements.FirstOrDefault(a => a.Name == achName);
                        if (ach != null)
                        {
                            PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, $"[{ach.Name}] {ach.Description} {status}");
                        }
                    }
                }
                else if (message.StartsWith("!totalplaytime", StringComparison.OrdinalIgnoreCase))
                {
                    e.CancelTransfer = true;

                    var result = await DBConnect.GetPlayTimeAsync(e.Proxy.Session?.CharacterName!);

                    var hours = result.seconds / 3600;
                    var minutes = (result.seconds % 3600) / 60;
                    var secs = result.seconds % 60;

                    TimeSpan? time = e.Proxy.Session?.AccumulatedPlayTime;
                    if (time.HasValue)
                    {
                        var t = time.Value;

                        hours += (int)t.TotalHours;
                        minutes += t.Minutes;
                        secs += t.Seconds;

                        if (secs > 60 && secs < 120)
                        {
                            minutes++;
                            secs %= 60;
                        }
                        else
                        {
                            minutes += 2;
                            secs %= 60;
                        }

                        if (minutes > 60 && minutes < 120)
                        {
                            hours++;
                            minutes %= 60;
                        }
                        else
                        {
                            hours += 2;
                            minutes %= 60;
                        }

                        PlayerTools.SendToProxyChat(
                            e.Proxy,
                            ChatType.Notice, 
                            null, 
                            $"[Total Playtime] {e.Proxy.Session?.CharacterName}: {hours}h {minutes}m {secs}s"
                        );
                    }
                }
                else if (message.StartsWith("!sessiontime", StringComparison.OrdinalIgnoreCase))
                {
                    e.CancelTransfer = true;

                    TimeSpan? time = e.Proxy.Session?.AccumulatedPlayTime;
                    if (time.HasValue)
                    {
                        var t = time.Value;

                        int hours = (int)t.TotalHours;
                        int minutes = t.Minutes;
                        int secs = t.Seconds;

                        PlayerTools.SendToProxyChat(
                            e.Proxy,
                            ChatType.Notice,
                            null,
                            $"[Playtime] {e.Proxy.Session?.CharacterName}: {hours}h {minutes}m {secs}s"
                        );
                    }

                }
                else if (message.Equals("!inv", StringComparison.OrdinalIgnoreCase))
                {
                    e.CancelTransfer = true;

                    var inv = e.Proxy.Inventory;
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
                    pet.Inventory[slot] = ((int)refItemId, item.CodeName, finalStack, item.MaxStack);
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
                    var inv = e.Proxy.Inventory;

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

                    bool isAttackPet = petObjInfo.item.T4 == 3;
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
                            Inventory = new ConcurrentDictionary<byte, (int ItemID, string CodeName, int Stack, int MaxStack)>()
                        };

                        e.Proxy.Session!.ActivePetUID = petUID;
                        return;
                    }

                    // Pickup / growth pet
                    string petName = packet.ReadAscii();
                    byte invSize = packet.ReadByte();
                    byte itemCount = packet.ReadByte();

                    Logger.Debug("CosSpawn", $"Pet 0x{petUID:X} name='{(string.IsNullOrEmpty(petName) ? "No name" : petName)}' invSize={invSize} itemCount={itemCount}");

                    var petSlots = new ConcurrentDictionary<byte, (int ItemID, string CodeName, int Stack, int MaxStack)>();

                    for (int i = 0; i < itemCount; i++)
                    {
                        byte indexByte = packet.ReadByte();
                        packet.ReadUInt();  // padding
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

                        if (item.T2 == 3)
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

                        petSlots[slot] = ((int)refItemId, item.CodeName, finalStack, item.MaxStack);
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

                var inv = e.Proxy.Inventory;
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
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_NEW_GOLD_AMOUNT, (sender, e) =>
            {
                try
                {
                    var packet = e.Packet;
                    int size = packet.RemainingRead();

                    if (size == 10)
                    {
                        byte flag = packet.ReadByte();
                        if (flag != 0x01) return;

                        ulong gold = packet.ReadULong();
                        packet.ReadByte();

                        e.Proxy.Session!.PlayerStats!.RemainingGold = gold;
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
                        // flag == 0x04 is likely EXP
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
                    var inv = e.Proxy.Inventory;
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

                        inv.Storage[slot] = ((int)refItemId, itemInfo.item.CodeName, finalStack, itemInfo.item.MaxStack);
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

                if (!e.Proxy.SpawnedObjects.TryGetValue(mobUID, out var spawnInfo))
                {
                    Logger.Warn("KillTracker", $"Missing refObjID for UID=0x{mobUID:X}");
                    return;
                }

                // remove
                e.Proxy.SpawnedObjects.TryRemove(mobUID, out _);

                _ = Task.Run(async () =>
                {
                    var result = await DBConnect.GetMonsterCodeName(spawnInfo.RefObjID);
                    if (result.codeName.StartsWith("NPC")) return;

                    if (e.Proxy.Session != null)
                    {
                        Interlocked.Increment(ref e.Proxy.Session.SessionKills); // Not a bug fix, but techincally more correct here.
                        await AchievementService.OnMonsterKill(proxy.Session!.CharacterName!, result.codeName, proxy);

                        Logger.Debug("KillTracker",
                            $"{proxy.Session?.CharacterName} killed mob {GameObjectNameResolver.Resolve(result.codeName)} " +
                            $"in {RegionResolver.Resolve((short)spawnInfo.RegionID)} " +
                            $"(refObjID={spawnInfo.RefObjID}, UID=0x{mobUID:X}) +{exp}exp +{sExp}sp");

                        lock (proxy.Session!)
                        {
                            proxy.Session!.CumulativeExp += exp;
                        }
                        
                        _ = Task.Run(() => PlayerTools.CheckLevelUp(proxy));

                        DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "kill_update", new
                        {
                            sessionKills = e.Proxy.Session.SessionKills
                        });
                    }
                    else
                    {
                        Logger.Warn("KillTracker", "Proxy session was null!");
                    }
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
        public static void RegisterClientMovementHandler(Server _agentProxy)
        {
            _agentProxy.RegisterClientPacketHandler(Constant.CLIENT_MOVEMENT, (sender, e) =>
            {
                var packet = e.Packet.Clone();

                try
                {
                    byte moveType = packet.ReadByte();

                    //Logger.Debug("Movement",
                    //    $"MOVE_TYPE = 0x{moveType:X2} | Remaining = {packet.RemainingRead()} bytes");

                    switch (moveType)
                    {
                        case 0x01:
                            {
                                byte sx = packet.ReadByte();
                                byte sy = packet.ReadByte();

                                short x = packet.ReadShort();
                                short z = packet.ReadShort();
                                short y = packet.ReadShort();

                                //Logger.Debug("Movement",
                                //    $"ABS MOVE | Sector=({sx},{sy}) Pos=({x},{y},{z})");
                                //Logger.Debug("Movement",
                                //    $"Hex Dump: {BitConverter.ToString(packet.GetBytes())}");

                                break;
                            }

                        case 0x00:
                            {
                                byte mode = packet.ReadByte();
                                ushort packed = packet.ReadUShort();

                                //Logger.Debug("Movement",
                                //    $"REL MOVE | Mode={mode} Packed=0x{packed:X4} ({packed})");
                                //Logger.Debug("Movement",
                                //    $"Hex Dump: {BitConverter.ToString(packet.GetBytes())}");

                                break;
                            }

                        default:
                            {
                                Logger.Debug("Movement",
                                    $"UNKNOWN MOVE TYPE | Hex Dump: {BitConverter.ToString(packet.GetBytes())}");
                                break;
                            }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Movement",
                        $"Parse error: {ex.Message}");
                }
            });
        }

        #endregion

        #region - Handler Registry (CUSTOM PACKETS)

        public static void RegisterClientSortHandler(Server _agentProxy)
        {
            _agentProxy.RegisterClientPacketHandler(Constant.DEW_SORT, (sender, e) =>
            {
                e.CancelTransfer = true;
                var packet = e.Packet.Clone();
                var type = packet.ReadByte();

                // This is for later. :3
                // var target = packet.ReadByte();

                e.Proxy.IsSorting = true;
                string sortMode = "type";
                if (type == 0x01) // type
                {
                    sortMode = "type";
                }
                else if (type == 0x02) // name
                {
                    sortMode = "name";
                }
                else if (type == 0x03) // logical
                {
                    sortMode = "logical";
                }
                else
                {
                    PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, "Usage: !sort [name|type]");
                    return;
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var cts = new CancellationTokenSource();
                        e.Proxy.ActiveSortCts = cts;
                        PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, "Sorting started...");

                        int safety = 0;
                        
                        while (safety <= 300)
                        {

                            var result = sortMode == "logical" ? await SortInventoryStep(e.Proxy, sortMode, cts.Token) : await SortInventoryLogical(e.Proxy, cts.Token);
                            

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
                        e.Proxy.IsSorting = false;
                        e.Proxy.ActiveSortCts?.Cancel();
                        e.Proxy.ActiveSortCts?.Dispose();
                        e.Proxy.ActiveSortCts = null;
                    }
                });
            });
        }
        public static void RegisterPlayerRewardHandler(Server _agentProxy)
        {
            _agentProxy.RegisterClientPacketHandler(Constant.DEW_CLAIM_REWARD, async (sender, e) =>
            {
                e.CancelTransfer = true;
                var packet = e.Packet.Clone();

                byte claimedLevel = packet.ReadByte();
                byte qty = packet.ReadByte();
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
                        $"Reward claimed! {itemCode} has been added to your inventory.");

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
                var proxy = Overseer.GetProxyByAccount(accountName);
                if (proxy?.Session == null) return;

                byte level = (byte)json.GetProperty("level").GetInt32();

                if (!proxy.Session.UnclaimedRewards.Contains(level)) return;

                if (!Overseer.LevelRewardOptions.TryGetValue(level, out var options) || options.Count == 0)
                    return;

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
        #endregion

        #region - Sorting -
        private static async Task<bool> SendPetMoveAndWait(Proxy proxy, byte source, byte dest, ushort qty, int timeoutMs = 3000)
        {
            var tcs = new TaskCompletionSource<bool>();

            proxy.PendingMoves[source] = tcs;

            var movePacket = new Packet(Constant.CLIENT_ITEM_MOVE);
            movePacket.WriteByte(16);
            movePacket.WriteByte(source);
            movePacket.WriteByte(dest);
            movePacket.WriteUShort(qty);
            proxy.Server.Send(movePacket);

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));

            proxy.PendingMoves.Remove(source);

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

            // Delay now respects the token
            var completed = await Task.WhenAny(
                tcs.Task,
                Task.Delay(timeoutMs, cancellationToken));

            proxy.PendingMoves.Remove(source);

            // If we were cancelled while waiting, just bail
            if (cancellationToken.IsCancellationRequested)
                return false;

            if (completed == tcs.Task)
                return tcs.Task.Result;

            return false;
        }

        /// <summary>
        /// Sorts inventory by type and name.
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="sortMode"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<SortResult> SortInventoryStep(Proxy proxy, string sortMode = "type", CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (proxy.Inventory.Slots.IsEmpty)
                return SortResult.Aborted;

            if (proxy.Inventory.Slots.Values.Any(s => s.CodeName == "MALL_PENDING"))
            {
                PlayerTools.SendToProxyChat(proxy, ChatType.Notice, null, "You have pending mall items. Teleport to resync before sorting.");
                return SortResult.Aborted;
            }
            else if (proxy.Inventory.Slots.Values.Any(s => s.CodeName == "UNKNOWN_PET_TRANSFER"))
            {
                PlayerTools.SendToProxyChat(proxy, ChatType.Notice, null, "You have unsynced items. Teleport to resync before sorting.");
                return SortResult.Aborted;
            }

            // Snapshot current inventory slots (slot >= 13 = player inventory)
            var slots = proxy.Inventory.Slots
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
                    if (itemB.ItemID != itemA.ItemID) continue;
                    if (itemB.Stack <= 0) continue;

                    int spaceInA = itemA.MaxStack - itemA.Stack;
                    if (spaceInA <= 0) break;

                    ushort qty = (ushort)Math.Min(itemB.Stack, spaceInA);

                    // FIXED: move FROM later stack TO earlier stack
                    bool moved = await SendMoveAndWait(proxy, slotB, slotA, qty, cancellationToken);
                    if (moved)
                    {
                        didStack = true;
                        itemA = (itemA.ItemID, itemA.CodeName, itemA.Stack + qty, itemA.MaxStack);
                        slots[i] = new KeyValuePair<byte, (int, string, int, int)>(slotA, itemA);

                        itemB = (itemB.ItemID, itemB.CodeName, itemB.Stack - qty, itemB.MaxStack);
                        slots[j] = new KeyValuePair<byte, (int, string, int, int)>(slotB, itemB);
                    }
                }
            }
            if (didStack) return SortResult.Continue;

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
                    return SortResult.Continue;
                }
            }

            // === SORT ===
            var sorted = sortMode == "name"
                ? slots
                    .OrderBy(s => GameObjectNameResolver.Resolve(s.Value.CodeName))
                    .ThenBy(s => s.Value.CodeName)
                    .ThenByDescending(s => s.Value.Stack)
                    .ToList()
                : slots
                    .OrderBy(s => s.Value.CodeName)
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

            if (proxy.Inventory.Slots.IsEmpty)
                return SortResult.Aborted;

            if (proxy.Inventory.Slots.Values.Any(s => s.CodeName == "MALL_PENDING"))
            {
                PlayerTools.SendToProxyChat(proxy, ChatType.Notice, null, "You have pending mall items. Teleport to resync before sorting.");
                return SortResult.Aborted;
            }
            else if (proxy.Inventory.Slots.Values.Any(s => s.CodeName == "UNKNOWN_PET_TRANSFER"))
            {
                PlayerTools.SendToProxyChat(proxy, ChatType.Notice, null, "You have unsynced items. Teleport to resync before sorting.");
                return SortResult.Aborted;
            }

            // Snapshot current
            var slots = proxy.Inventory.Slots
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
                    if (itemB.ItemID != itemA.ItemID) continue;
                    if (itemB.Stack <= 0) continue;

                    int spaceInA = itemA.MaxStack - itemA.Stack;
                    if (spaceInA <= 0) break;

                    ushort qty = (ushort)Math.Min(itemB.Stack, spaceInA);

                    bool moved = await SendMoveAndWait(proxy, slotB, slotA, qty, cancellationToken);
                    if (moved)
                    {
                        didStack = true;
                        itemA = (itemA.ItemID, itemA.CodeName, itemA.Stack + qty, itemA.MaxStack);
                        slots[i] = new KeyValuePair<byte, (int, string, int, int)>(slotA, itemA);

                        itemB = (itemB.ItemID, itemB.CodeName, itemB.Stack - qty, itemB.MaxStack);
                        slots[j] = new KeyValuePair<byte, (int, string, int, int)>(slotB, itemB);
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
                .Where(s => IsHpPotion(s.Value.CodeName) && s.Value.Stack > 20)
                .OrderByDescending(s => s.Value.Stack)
                .FirstOrDefault();

            var largeMpCandidate = slots
                .Where(s => IsMpPotion(s.Value.CodeName) && s.Value.Stack > 20)
                .OrderByDescending(s => s.Value.Stack)
                .FirstOrDefault();

            // Build ordered list exactly in the sequence you wanted
            var sorted = new List<KeyValuePair<byte, (int ItemID, string CodeName, int Stack, int MaxStack)>>();

            // Pet Scrolls
            sorted.AddRange(slots
                .Where(s => IsPetScroll(s.Value.CodeName))
                .OrderBy(s => s.Value.CodeName)
                .ThenByDescending(s => s.Value.Stack));

            // Special scrolls
            sorted.AddRange(slots
                .Where(s => IsSpecialScroll(s.Value.CodeName))
                .OrderBy(s => s.Value.CodeName)
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
                .Where(s => IsQuestItem(s.Value.CodeName) && !placed.Contains(s.Key))
                .OrderBy(s => s.Value.CodeName)
                .ThenByDescending(s => s.Value.Stack));

            placed.UnionWith(sorted.Skip(placed.Count).Select(s => s.Key));

            // Growth / pet skill potions
            sorted.AddRange(slots
                .Where(s => IsGrowthPotion(s.Value.CodeName) && !placed.Contains(s.Key))
                .OrderBy(s => s.Value.CodeName)
                .ThenByDescending(s => s.Value.Stack));

            placed.UnionWith(sorted.Skip(placed.Count).Select(s => s.Key));

            // Everything else that is NOT a potion
            sorted.AddRange(slots
                .Where(s => !placed.Contains(s.Key) &&
                            !IsHpPotion(s.Value.CodeName) &&
                            !IsMpPotion(s.Value.CodeName))
                .OrderBy(s => s.Value.CodeName)
                .ThenByDescending(s => s.Value.Stack));

            // Deferred HP and MP potions
            sorted.AddRange(slots
                .Where(s => !placed.Contains(s.Key) &&
                            (IsHpPotion(s.Value.CodeName) || IsMpPotion(s.Value.CodeName)))
                .OrderBy(s => s.Value.CodeName)
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
                case ChatType.General: // "All" — needs uint UID, no name
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

        private static List<string> BuildPages(IEnumerable<string> items, int maxLen, string separator)
        {
            var pages = new List<string>();
            var sb = new StringBuilder();

            foreach (var item in items)
            {
                string append = sb.Length == 0 ? item : separator + item;

                if (sb.Length + append.Length > maxLen)
                {
                    pages.Add(sb.ToString());
                    sb.Clear();
                    sb.Append(item);
                }
                else
                {
                    sb.Append(append);
                }
            }

            if (sb.Length > 0)
                pages.Add(sb.ToString());

            return pages;
        }
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
            if (!Overseer.LevelRewardOptions.TryGetValue(newLevel, out var options) || options.Count == 0)
                return;

            var codeNames = options.Select(o => o.CodeName);
            var iconPaths = await DBConnect.GetItemIconPaths(codeNames);

            // Debug
            Logger.Debug("OnPlayerLevelUp", $"Icon paths fetched: {iconPaths.Count} for codes: {string.Join(", ", codeNames)}");
            foreach (var kvp in iconPaths)
                Logger.Debug("OnPlayerLevelUp", $"  {kvp.Key} -> {kvp.Value}");

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
        #endregion
    }
}
