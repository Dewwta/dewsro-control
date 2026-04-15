using CoreLib.Tools.Logging;
using System.Collections.Concurrent;
using System.Text;
using VSRO_CONTROL_API.Settings;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Framework;
using VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.Tools;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Achivements;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Tracking;

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
                        
                        // 2. ITEM ID & DB LOOKUP
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
                        // 3. BRANCHING LOGIC
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

                                // 1. Sockets (Binding Type 1)
                                packet.ReadByte(); // Binding Type
                                byte socketCount = packet.ReadByte();
                                for (int j = 0; j < socketCount; j++)
                                {
                                    packet.ReadByte(); packet.ReadUInt(); packet.ReadUInt();
                                }

                                // 2. Adv Elixirs (Binding Type 2)
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

                                if (!inv.Pets.ContainsKey(petUID))
                                    inv.Pets[petUID] = new();
                                inv.Pets[petUID].Inventory[slot] = ((int)refItemId, itemInfo.item.CodeName, finalStack, itemInfo.item.MaxStack);
                                Logger.Debug("ItemMoveHandler", $"Pet picked up {itemInfo.item.CodeName} x{finalStack} → pet 0x{petUID:X} slot {slot}");
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
                                ushort slot = packet.ReadUShort();     // Why is slot a ushort? no idea, but this is the only place it is.

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
        public static void RegisterCosDespawnHandler(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_ANIMATION_COS_REMOVE_MENU, (sender, e) =>
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
                    Logger.Info("CosDespawnHandler", $"Pet {petName} despawned and removed");
                }
                else if (type == 2)
                {
                    // State change only - inventory stays, pet is recalled/unsummoned
                    Logger.Info("CosDespawnHandler", $"Pet {petName} state change (recalled)");
                }
            });
        }
        public static void RegisterCosSpawnHandler(Server _agentProxy)
        {
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
                        
                        // Create attack pet object for tracking
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
                        // Every item: [index byte] [4 bytes padding/rent] [refObjID uint]
                        byte indexByte = packet.ReadByte();
                        packet.ReadUInt();  // 00 00 00 00 padding
                        uint refItemId = packet.ReadUInt();

                        byte slot = (byte)(indexByte + 1);

                        var itemInfoResult = await DBConnect.GetItemRecord(refItemId);
                        if (!itemInfoResult.success)
                        {
                            Logger.Warn("CosSpawn", $"Unknown item refObjID 0x{refItemId:X} at slot {slot}");
                            // Can't safely continue — we don't know how many bytes to skip
                            break;
                        }

                        var item = itemInfoResult.item;
                        ushort finalStack = 1;

                        if (item.T2 == 3)
                        {
                            // ETC: [qty ushort]  — done, 11 bytes total per item
                            finalStack = packet.ReadUShort();
                        }
                        else if (item.T2 == 1)
                        {
                            // Equipment blob
                            packet.ReadByte();   // opt1 (plus/state byte)
                            packet.ReadULong();  // serial
                            packet.ReadUInt();   // durability

                            byte magCount = packet.ReadByte();
                            for (int m = 0; m < magCount; m++)
                            {
                                packet.ReadUInt();
                                packet.ReadUInt();
                            }

                            packet.ReadByte();   // plus level

                            byte scCount = packet.ReadByte();
                            for (int s = 0; s < scCount; s++)
                            {
                                packet.ReadByte();
                                packet.ReadUInt();
                                packet.ReadUInt();
                            }

                            packet.ReadByte();   // separator

                            byte acCount = packet.ReadByte();
                            for (int a = 0; a < acCount; a++)
                            {
                                packet.ReadByte();
                                packet.ReadUInt();
                                packet.ReadUInt();
                            }

                            finalStack = 1;
                        }
                        else
                        {
                            Logger.Warn("CosSpawn", $"Unhandled T2={item.T2} for item 0x{refItemId:X} ({item.CodeName}) at slot {slot}");
                            break;
                        }

                        petSlots[slot] = ((int)refItemId, item.CodeName, finalStack, item.MaxStack);
                        Logger.Debug("CosSpawn", $"Pet 0x{petUID:X} Slot [{slot}] {item.CodeName} ({finalStack}/{item.MaxStack})");
                    }
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
                }
                catch (Exception ex)
                {
                    Logger.Error("CosSpawn", $"Error parsing pet spawn: {ex.Message}\n{ex.StackTrace}");
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

                    Logger.Debug("Movement",
                        $"MOVE_TYPE = 0x{moveType:X2} | Remaining = {packet.RemainingRead()} bytes");

                    switch (moveType)
                    {
                        case 0x01:
                            {
                                byte sx = packet.ReadByte();
                                byte sy = packet.ReadByte();

                                short x = packet.ReadShort();
                                short z = packet.ReadShort();
                                short y = packet.ReadShort();

                                Logger.Debug("Movement",
                                    $"ABS MOVE | Sector=({sx},{sy}) Pos=({x},{y},{z})");
                                Logger.Debug("Movement",
                                    $"Hex Dump: {BitConverter.ToString(packet.GetBytes())}");

                                break;
                            }

                        case 0x00:
                            {
                                byte mode = packet.ReadByte();
                                ushort packed = packet.ReadUShort();

                                Logger.Debug("Movement",
                                    $"REL MOVE | Mode={mode} Packed=0x{packed:X4} ({packed})");
                                Logger.Debug("Movement",
                                    $"Hex Dump: {BitConverter.ToString(packet.GetBytes())}");

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
                
                Logger.Info("SortHandler:Dew", $"Sorting called!!");

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
                else
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
            });
        }

        #endregion

        #region - Sorting -

        private static async Task<bool> SendMoveAndWait(Proxy proxy, byte source, byte dest, ushort qty, int timeoutMs = 3000)
        {
            var tcs = new TaskCompletionSource<bool>();

            proxy.PendingMoves[source] = tcs;

            var movePacket = new Packet(Constant.CLIENT_ITEM_MOVE);
            movePacket.WriteByte(0);
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

        public static async Task<SortResult> SortInventoryStep(Proxy proxy, string sortMode = "type")
        {
            var (success, items, _) =
                await DBConnect.GetInventoryWithNamesByCharName(proxy.Session!.CharacterName!);

            if (!success || items == null || items.Count == 0)
                return SortResult.Aborted;

            if (proxy.Inventory.Slots.Values.Any(s => s.CodeName == "MALL_PENDING"))
            {
                PlayerTools.SendToProxyChat(proxy, ChatType.Notice, null, "You have pending mall items detected. Teleport to resync inventory before sorting");
                return SortResult.Aborted;
            } 
            else if (proxy.Inventory.Slots.Values.Any(s => s.CodeName == "UNKNOWN_PET_TRANSFER"))
            {
                PlayerTools.SendToProxyChat(proxy, ChatType.Notice, null, "You have unsynced items. Teleport to resync inventory before sorting");
                return SortResult.Aborted;
            }
            int start = 13;

            var ordered = items.OrderBy(i => i.Slot).ToList();

            // =========================
            // PHASE 1: STACK
            // =========================
            for (int i = 0; i < ordered.Count; i++)
            {
                var a = ordered[i];

                for (int j = i + 1; j < ordered.Count; j++)
                {
                    var b = ordered[j];

                    if (a.ItemID == b.ItemID && a.MaxStack > 1)
                    {
                        int spaceInA = a.MaxStack - proxy.Inventory.Slots[a.Slot].Stack;
                        if (spaceInA <= 0) continue;

                        ushort qty = (ushort)Math.Min(proxy.Inventory.Slots[b.Slot].Stack, spaceInA);
                        await SendMoveAndWait(proxy, b.Slot, a.Slot, qty);
                        return SortResult.Continue;
                    }
                }
            }

            // =========================
            // PHASE 2: PACK (fill gaps)
            // =========================
            for (int i = 0; i < ordered.Count; i++)
            {
                byte expectedSlot = (byte)(start + i);

                if (ordered[i].Slot != expectedSlot)
                {
                    byte from = ordered[i].Slot;
                    byte to = expectedSlot;
                    ushort qty = (ushort)proxy.Inventory.Slots[from].Stack;

                    await SendMoveAndWait(proxy, from, to, qty);
                    return SortResult.Continue;
                }
            }

            // =========================
            // PHASE 3: SORT
            // =========================
            var sorted =
                sortMode == "name"
                    ? ordered
                        .OrderBy(i => GameObjectNameResolver.Resolve(i.CodeName))
                        .ThenBy(i => i.CodeName)
                        .ThenByDescending(i => proxy.Inventory.Slots[i.Slot].Stack)
                        .ToList()
                    : ordered
                        .OrderBy(i => i.CodeName)
                        .ThenByDescending(i => proxy.Inventory.Slots[i.Slot].Stack)
                        .ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                byte targetSlot = (byte)(start + i);

                var desired = sorted[i];

                var current = ordered.FirstOrDefault(x =>
                    x.ItemID == desired.ItemID &&
                    x.Slot != targetSlot
                );

                if (current.Slot == 0)
                    continue;

                if (current.Slot != targetSlot)
                {
                    ushort qty = (ushort)proxy.Inventory.Slots[current.Slot].Stack;
                    await SendMoveAndWait(proxy, current.Slot, targetSlot, qty);
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
        private static async Task SaveSession(Proxy proxy)
        {
            var session = proxy.Session;
            if (session == null) return;

            Logger.Debug(typeof(Overseer),
                $"Saving playtime for {session.CharacterName}: {session.AccumulatedPlayTime}");

            // Example DB call
            await DBConnect.AddPlayTimeAsync(session?.CharacterName, session.AccumulatedPlayTime);
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

        #endregion
    }
}
