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
using VSRO_CONTROL_API.VSRO.Enums;

namespace VSRO_CONTROL_API.VSRO.AsynchronousProxy
{
    public static class PlayerTools
    {
        #region - Logging -

        public static bool Debug { get; private set; } = false;
        public static void SetDebug(bool debug) => Debug = debug;
        private static void Log(string handler, string message) { if (Debug == true) Logger.Trace($"PlayerTools:{handler}", message); }

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
                        Logger.Error("Chardata", "Chardata object was null!");
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

                        await BackfillMissedLevelRewardsAsync(e.Proxy, data.CurrentLevel);

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
                        e.Proxy.Session!.CharacterName = data.CharacterName;

                        e.Proxy.Session!.Inventory.Slots = data.Slots;
                        e.Proxy.Session!.Inventory.Equipment = data.Equipment;
                        e.Proxy.Session!.Inventory.Avatars = data.Avatars;

                        e.Proxy.Session.JID = (int)data.AccountJID;
                        e.Proxy.Session.IsGM = data.IsGM;

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

                        lock (e.Proxy.Session!.Buffs)
                            e.Proxy.Session!.Buffs.Clear();

                        e.Proxy.Session!._attackLoop?.ClearCooldowns();

                        e.Proxy.Session!.LearnedSkills.Clear();
                        e.Proxy.Session!.LearnedSkills.AddRange(data.LearnedSkills);

                        DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "skill_pool_sync", new
                        {
                            skills = e.Proxy.Session.LearnedSkills
                                .Where(s => s.Enabled)
                                .Select(s => new { id = s.ID, name = s.ReadableName, passive = s.BasicActivity == 0, icon = s.IconFile })
                        });

                        var botData = PlayerBotDataStore.Load(e.Proxy.Session.CharacterName!);
                        if (botData != null)
                        {
                            e.Proxy.Session._lastBotX = botData.TrainX;
                            e.Proxy.Session._lastBotY = botData.TrainY;
                            e.Proxy.Session._lastBotZ = botData.TrainZ;
                            e.Proxy.Session._lastBotR = botData.TrainR;

                            if (e.Proxy.Session._attackLoop != null)
                            {
                                foreach (var id in botData.AttackSkillIds)
                                {
                                    if (!e.Proxy.Session.LearnedSkills.Any(s => s.ID == id && s.Enabled)) continue;
                                    var skill = await DBConnect.GetSkillById(id);
                                    if (skill != null) e.Proxy.Session._attackLoop.AddSkillToUse(skill, false);
                                }
                                foreach (var id in botData.BuffSkillIds)
                                {
                                    if (!e.Proxy.Session.LearnedSkills.Any(s => s.ID == id && s.Enabled)) continue;
                                    var skill = await DBConnect.GetSkillById(id);
                                    if (skill != null) e.Proxy.Session._attackLoop.AddSkillToUse(skill, true);
                                }
                            }

                            e.Proxy.Session._botSettings = botData.Settings ?? new BotSettings();

                            DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "bot_config_sync", new
                            {
                                x = botData.TrainX,
                                y = botData.TrainY,
                                z = botData.TrainZ,
                                r = botData.TrainR
                            });

                            e.Proxy.Session.PushSkillListsToDll(e.Proxy.Session.AccountName!);

                            DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "bot_settings_sync", e.Proxy.Session._botSettings);

                            DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "bot_config_sync", new
                            {
                                x = botData.TrainX,
                                y = botData.TrainY,
                                z = botData.TrainZ,
                                r = botData.TrainR,
                                regionId = botData.RegionId,
                            });

                            e.Proxy.Session.PushSkillListsToDll(e.Proxy.Session.AccountName!);
                        }
                    }

                    if (data != null)
                    {
                        var achCharName = e.Proxy.Session?.CharacterName;
                        var achLevel    = data.CurrentLevel;
                        var achMinutes  = (long)(e.Proxy.Session?.TotalPlayTime.TotalMinutes ?? 0);
                        if (achCharName != null)
                            _ = Task.Run(async () =>
                            {
                                await Task.Delay(5000);
                                await AchievementService.OnLevelCheck(achCharName, achLevel, e.Proxy);
                                // can be 0 if gold arrives in a separate follow-up packet.
                                var sessionGold = (long)(e.Proxy.Session?.PlayerStats?.RemainingGold ?? 0);
                                await AchievementService.OnGoldChanged(achCharName, sessionGold, e.Proxy);
                                await AchievementService.OnPlaytimeCheck(achCharName, achMinutes, e.Proxy);
                            });
                    }

                    e.Proxy.Session!.Inventory.IsReady = true;
                }
                catch (Exception ex)
                {
                    Logger.Error("Chardata",
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

                    if (proxy != null && proxy.Session != null && proxy.Session.PlayerStats != null)
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
                        Logger.Error("ServerStats", $"Character session or PlayerStats was null!");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("ServerStats", $"Error occurred parsing character stats!: {ex.Message}");
                }
            });
        }
        public static void RegisterItemMoveHandler(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_ITEM_MOVEMENT, async (sender, e) =>
            {
                try { await PacketParser.ParseItemMove(e.Packet.Clone(), e.Proxy); }
                catch (Exception ex) { Logger.Error("ItemMoveHandler", ex.Message); }
            });

            // Second B034 handler - intercept ShopToInventory (0x08) while BotShopOpen=true,
            // suppress the original, and send a fake GroundToInventory (0x06) instead.
            //
            // the bot suppresses B046 from the game client (to
            // prevent an uninitialized-shop-object crash [From DMP]), so the game client never opens
            // its shop UI. A ShopToInventory B034 would crash it trying to update that
            // uninitialised shop object.
            //
            // Why send a fake GroundToInventory: type 0x06 ("picked up from ground") needs
            // no shop context at all — the client just places the item in the slot.  The
            // inventory display updates immediately and no relog/teleport is required.
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_ITEM_MOVEMENT, async (sender, e) =>
            {
                if (e.Proxy.Session?.BotShopOpen != true) return;

                var pkt = e.Packet.Clone();
                byte result      = pkt.ReadByte();
                var movementType = (ItemMovement)pkt.ReadByte();

                Logger.Trace("ItemMoveHandler",
                    $"B034 BotShopOpen=true: result=0x{result:X2} type={movementType}(0x{(byte)movementType:X2})");

                if (movementType != ItemMovement.ShopToInventory)
                    return; // inventory moves, pet moves etc. pass through normally

                // Suppress the original — must happen synchronously before first await
                e.CancelTransfer = true;

                if (result != 0x01)
                {
                    Logger.Warn("ItemMoveHandler",
                        $"B034 ShopToInventory result=0x{result:X2} — buy failed server-side, suppressed with no fake");
                    return;
                }

                // Parse ShopToInventory payload
                byte shopTab   = pkt.ReadByte();
                byte shopSlot  = pkt.ReadByte();
                byte slotCount = pkt.ReadByte();
                var toSlots    = new List<byte>(slotCount);
                for (int i = 0; i < slotCount; i++) toSlots.Add(pkt.ReadByte());
                ushort quantity = pkt.ReadUShort();

                Logger.Info("ItemMoveHandler",
                    $"B034 ShopToInventory suppressed — building fake GroundToInventory " +
                    $"(shopTab={shopTab} shopSlot={shopSlot} qty={quantity} → inv slots [{string.Join(",", toSlots)}])");

                // Resolve refItemId via ShopLookup (npcObjId → tab → slot → item)
                uint npcObjId = 0;
                if (e.Proxy.Session.SpawnedObjects.TryGetValue(e.Proxy.Session.LastTargetUID, out var spawnInfo))
                    npcObjId = spawnInfo.RefObjID;

                if (!Overseer.ShopLookup.TryGetValue(((int)npcObjId, shopTab, shopSlot), out var shopItem))
                {
                    Logger.Warn("ItemMoveHandler",
                        $"B034 fake pickup: ShopLookup miss — npcObjId={npcObjId} tab={shopTab} slot={shopSlot} — fake not sent, client inventory display will be stale until relog");
                    return;
                }

                // DB lookup for T-values
                var itemInfo = await DBConnect.GetItemRecord((uint)shopItem.RefItemID);
                if (!itemInfo.success)
                {
                    Logger.Warn("ItemMoveHandler",
                        $"B034 fake pickup: DB lookup failed for refItemId={shopItem.RefItemID} ({shopItem.CodeName}) — fake not sent");
                    return;
                }

                // Build one fake GroundToInventory (0x06) per destination slot
                foreach (var invSlot in toSlots)
                {
                    try
                    {
                        var fake = new Packet(Constant.SERVER_ITEM_MOVEMENT);
                        fake.WriteByte(0x01);                          // success
                        fake.WriteByte(0x06);                          // GroundToInventory (no shop UI needed)
                        fake.WriteByte(invSlot);                       // destination inventory slot
                        fake.WriteUInt(0);                             // rentType = 0
                        fake.WriteUInt((uint)shopItem.RefItemID);      // item ref ID

                        if (itemInfo.item.T2 == 3) // ETC / consumable
                        {
                            fake.WriteUShort(quantity);
                            if (itemInfo.item.T3 == 11 && itemInfo.item.T4 == 1)
                                fake.WriteByte(0); // extra byte for certain special items
                            else if (itemInfo.item.T3 == 14)
                                fake.WriteByte(0); // cm = 0 (no magic-option list)
                        }
                        else
                        {
                            Logger.Warn("ItemMoveHandler",
                                $"B034 fake pickup: unsupported T2={itemInfo.item.T2} for {shopItem.CodeName} — skipping slot {invSlot}");
                            continue;
                        }

                        e.Proxy.Client.Send(fake);
                        Logger.Info("ItemMoveHandler",
                            $"B034 fake GroundToInventory sent: {shopItem.CodeName} x{quantity} → client slot {invSlot} " +
                            $"(T2={itemInfo.item.T2} T3={itemInfo.item.T3} T4={itemInfo.item.T4})");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("ItemMoveHandler",
                            $"B034 fake pickup: send failed for slot {invSlot}: {ex.Message}");
                    }
                }
            });
        }
        public static void RegisterItemUseHandler(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_ITEM_USE, async (sender, e) =>
            {
                try
                {
                    var data = PacketParser.ParseItemUse(e.Packet.Clone());
                    if (data == null) return;

                    if (data.Result != 1)
                    {
                        if (data.Result == 2)
                            Log("ItemUseHandler", "Use of the item was denied by the server.");
                        else
                            Log("ItemUseHandler", $"Non-standard ITEM_USE result={data.Result} | slot={data.Slot}");
                        return;
                    }

                    Log("ItemUseHandler", $"result={data.Result} | slot={data.Slot} | remainingStack={data.RemainingStack}");

                    var inv = e.Proxy.Session!.Inventory;
                    if (!inv.Slots.TryGetValue(data.Slot, out var item))
                        return;

                    await AchievementService.OnItemUsed(e.Proxy.Session?.CharacterName!, item.CodeName128 ?? string.Empty, e.Proxy);

                    if (data.RemainingStack == 0)
                    {
                        inv.Slots.TryRemove(data.Slot, out _);
                        Log("ItemUseHandler", $"Removed item from slot {data.Slot} (stack reached 0)");
                    }
                    else
                    {
                        inv.Slots[data.Slot] = new SR_Item()
                        {
                            RefItemID = item.RefItemID,
                            CodeName128 = item.CodeName128,
                            Stack = data.RemainingStack,
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
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_ANIMATION_COS_SPAWN, async (sender, e) =>
            {
                try
                {
                    var inv = e.Proxy.Session!.Inventory;
                    var result = await PacketParser.ParseCOSSpawn(e.Packet.Clone());
                    if (result?.Pet == null) return;

                    inv.Pets[result.PetUID] = result.Pet;
                    e.Proxy.Session!.ActivePetUID = result.PetUID;

                    if (!result.IsAttackPet && inv.PendingCosPages.TryRemove(result.PetUID, out var pending))
                    {
                        Log("CosSpawn", $"Draining pending 0x30C9 for pet 0x{result.PetUID:X}");
                        var pageItems = await PacketParser.ParseCosPage(pending.Packet, pending.ItemCount);
                        if (pageItems != null)
                            foreach (var kvp in pageItems)
                                inv.Pets[result.PetUID].Inventory[kvp.Key] = kvp.Value;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("CosSpawn", $"Error parsing pet spawn: {ex.Message}\n{ex.StackTrace}");
                }
            });

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_ANIMATION_COS_REMOVE_MENU, async (sender, e) =>
            {
                try
                {
                    var packet = e.Packet.Clone();
                    var inv = e.Proxy.Session!.Inventory;

                    var result = PacketParser.ParseCOSDespawn(packet);
                    if (result == null) return;

                    inv.Pets.TryGetValue(result.PetUID, out var pet);
                    string petName = pet?.Info.Name ?? $"0x{result.PetUID:X}";

                    if (result.Type == 1)
                    {
                        inv.Pets.TryRemove(result.PetUID, out _);
                        if (e.Proxy.Session?.ActivePetUID == result.PetUID)
                            e.Proxy.Session.ActivePetUID = 0;
                        Log("CosDespawnHandler", $"Pet {petName} despawned and removed");
                        return;
                    }

                    if (result.Type == 2 && !result.HasInventoryPage)
                    {
                        Log("CosDespawnHandler", $"Pet {petName} state change (recalled)");
                        return;
                    }

                    if (result.Type == 2 && result.HasInventoryPage)
                    {
                        if (pet == null)
                        {
                            Log("CosSpawn", $"0x30C9 arrived early for 0x{result.PetUID:X}, queuing");
                            inv.PendingCosPages[result.PetUID] = (packet, result.PageItemCount);
                            return;
                        }

                        var pageItems = await PacketParser.ParseCosPage(packet, result.PageItemCount);
                        if (pageItems != null)
                            foreach (var kvp in pageItems)
                                pet.Inventory[kvp.Key] = kvp.Value;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("CosDespawnHandler", $"Error parsing 0x30C9: {ex.Message}");
                }
            });
        }
        public static void RegisterGoldUpdateHandler(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_NEW_GOLD_AMOUNT, async (sender, e) =>
            {
                try
                {
                    var data = PacketParser.ParseNewGoldAmount(e.Packet.Clone());
                    if (data == null) return;

                    var session = e.Proxy.Session;

                    if (data.Flag == 0x01)
                    {
                        e.Proxy.Session!.PlayerStats!.RemainingGold = data.Gold;
                        DllBridge.Instance.SendToDll(session!.AccountName!, "gold_update", new { remainGold = data.Gold });
                        await AchievementService.OnGoldChanged(e.Proxy.Session!.CharacterName, (long)data.Gold, e.Proxy);
                        Log("GoldUpdateHandler", $"Gold → {data.Gold}");
                    }
                    else if (data.Flag == 0x02)
                    {
                        e.Proxy.Session!.PlayerStats!.RemainingSkillPoints = data.SkillPoints;
                        Log("SkillPointHandler", $"Skill points → {data.SkillPoints}");
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
                var data = PacketParser.ParseHPMPUpdate(e.Packet.Clone());
                if (data == null) return;

                if (data.TargetUID != e.Proxy.Session?.CharacterUID)
                    return;

                if (data.StatType == 0x01)
                    e.Proxy.Session.PlayerStats!.CurrentHP = data.Value;
                else if (data.StatType == 0x02)
                    e.Proxy.Session.PlayerStats!.CurrentMP = data.Value;
            });
        }
        public static void RegisterStorageHandler(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_STORAGE_ITEMS, async (sender, e) =>
            {
                try
                {
                    var data = await PacketParser.ParseStorageItems(e.Packet.Clone());
                    if (data == null) return;

                    var inv = e.Proxy.Session!.Inventory;
                    inv.Storage.Clear();
                    foreach (var kvp in data.Storage)
                        inv.Storage[kvp.Key] = kvp.Value;
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
                var data = PacketParser.ParseEXP(e.Packet.Clone());
                if (data == null) return;

                var proxy = e.Proxy;
                uint mobUID = data.MobUID;
                ulong exp = data.Exp;
                ulong sExp = data.SExp;

                if (!e.Proxy.Session!.SpawnedObjects.TryGetValue(mobUID, out var spawnInfo))
                {
                    Log("KillTracker", $"Missing refObjID for UID=0x{mobUID:X} — GM exp or untracked source, still checking level-up.");
                    _ = Task.Run(() =>
                    {
                        lock (proxy.Session!)
                        {
                            proxy.Session!.CumulativeExp += exp;
                        }
                        _ = Task.Run(() => PlayerTools.CheckLevelUp(proxy));
                    });
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
                        Log("KillTracker",
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
                    var data = PacketParser.ParseSTRUpdate(e.Packet.Clone());
                    if (data == null) return;

                    if (data.Result != 0)
                    {
                        if (e.Proxy.Session != null)
                            e.Proxy.Session.PlayerStats!.UnusedStatPoints--;
                        else
                            Logger.Error("STRHandler", "Could not update STR on unknown user!");
                    }
                    else
                    {
                        Log("STRHandler", "Could not update STR with a success flag of 0");
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
                    var data = PacketParser.ParseINTUpdate(e.Packet.Clone());
                    if (data == null) return;

                    if (data.Result != 0)
                    {
                        if (e.Proxy.Session != null)
                            e.Proxy.Session.PlayerStats!.UnusedStatPoints--;
                        else
                            Logger.Error("INTHandler", "Could not update INT on unknown user!");
                    }
                    else
                    {
                        Log("INTHandler", "Could not update INT with a success flag of 0");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("INTHandler", $"Error occurred updating character INT!: {ex.Message}");
                }
            });
        }
        public static void RegisterSkillLearnHandler(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_SKILLUPDATE, async (sender, e) =>
            {
                try
                {
                    var data = PacketParser.ParseSkillUpdate(e.Packet.Clone());
                    if (data == null || !data.Success) return;

                    uint newSkillId = data.SkillId;
                    var session = e.Proxy.Session;
                    if (session == null) return;

                    var newSkillData = await DBConnect.GetSkillById(newSkillId);
                    if (newSkillData == null)
                    {
                        Log("SkillLearnHandler", $"Unknown skill ID {newSkillId} in 0xB0A1");
                        return;
                    }

                    // Find the old version in the active bot loops before mutating the list
                    var oldAttack = session._attackLoop?.SkillsToUse.FirstOrDefault(s => s.Group == newSkillData.Group);
                    var oldBuff   = session._attackLoop?.BuffsToUse.FirstOrDefault(s => s.Group == newSkillData.Group);

                    // Replace in-place to preserve list order/priority
                    if (oldAttack != null)
                        session._attackLoop!.ReplaceSkillInUse(oldAttack.ID, newSkillData, false);
                    if (oldBuff != null)
                        session._attackLoop!.ReplaceSkillInUse(oldBuff.ID, newSkillData, true);
                    if (oldAttack != null || oldBuff != null)
                        session.SaveBotConfig();

                    // Replace in learned skills list
                    session.LearnedSkills.RemoveAll(s => s.Group == newSkillData.Group);
                    session.LearnedSkills.Add(new SR_Skill
                    {
                        ID = newSkillData.ID,
                        CodeName = newSkillData.CodeName,
                        Group = newSkillData.Group,
                        Level = newSkillData.Level,
                        BasicActivity = newSkillData.BasicActivity,
                        PreparingTime = newSkillData.PreparingTime,
                        CastingTime = newSkillData.CastingTime,
                        ActionDuration = newSkillData.ActionDuration,
                        ReuseDelay = newSkillData.ReuseDelay,
                        CoolTime = newSkillData.CoolTime,
                        Range = newSkillData.Range,
                        AutoAttackType = newSkillData.AutoAttackType,
                        CanUseInTown = newSkillData.CanUseInTown,
                        RequiresTarget = newSkillData.RequiresTarget,
                        ConsumeHP = newSkillData.ConsumeHP,
                        ConsumeMP = newSkillData.ConsumeMP,
                        AIAttackChance = newSkillData.AIAttackChance,
                        AISkillType = newSkillData.AISkillType,
                        MoveSpeedPercent = newSkillData.MoveSpeedPercent,
                        Params = newSkillData.Params,
                        Enabled = true,
                        ReadableName = newSkillData.ReadableName,
                        IconFile = newSkillData.IconFile
                    });

                    // Re-sync the skill pool to the DLL
                    DllBridge.Instance.SendToDll(session.AccountName!, "skill_pool_sync", new
                    {
                        skills = session.LearnedSkills
                            .Where(s => s.Enabled)
                            .Select(s => new { id = s.ID, name = s.ReadableName, passive = s.BasicActivity == 0, icon = s.IconFile })
                    });
                    session.PushSkillListsToDll(session.AccountName!);

                    Log("SkillLearnHandler", $"Skill upgraded to ID {newSkillId} ({newSkillData.ReadableName})");
                }
                catch (Exception ex)
                {
                    Logger.Error("SkillLearnHandler", $"Error: {ex.Message}");
                }
            });

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_MASTERYUPDATE, (sender, e) =>
            {
                try
                {
                    var packet = e.Packet;
                    byte success = packet.ReadByte();
                    if (success != 0x01) return;

                    uint masteryId = packet.ReadUInt();
                    byte newLevel = packet.ReadByte();

                    Log("MasteryHandler", $"Mastery {masteryId} → level {newLevel}");
                }
                catch (Exception ex)
                {
                    Logger.Error("MasteryHandler", $"Error: {ex.Message}");
                }
            });
        }
        public static void RegisterServerMovementHandler(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_MOVEMENT, (sender, e) =>
            {
                try
                {
                    var data = PacketParser.ParseServerMovement(e.Packet.Clone());
                    if (data == null || !data.HasMovement) return;

                    if (e.Proxy.Session != null && data.UniqueId == e.Proxy.Session.CharacterUID)
                    {
                        bool isZeroPos = (data.RegionId & 0x8000) == 0 && data.RawX == 0 && data.RawY == 0;
                        if (isZeroPos)
                        {
                            e.CancelTransfer = true;
                            Logger.Warn("B021", $"[{DateTime.UtcNow:HH:mm:ss.fff}] GUARDED (0,0) uid={data.UniqueId} region=0x{data.RegionId:X4} — session kept rawX={e.Proxy.Session.RawX} rawY={e.Proxy.Session.RawY} regionId=0x{e.Proxy.Session.RegionId:X4}");
                        }
                        else
                        {
                            e.Proxy.Session.RegionId = data.RegionId;
                            e.Proxy.Session.Z = (short)data.WorldZ;
                            e.Proxy.Session.SectorX = data.SectorX;
                            e.Proxy.Session.SectorY = data.SectorY;
                            e.Proxy.Session.WorldX = data.WorldX;
                            e.Proxy.Session.WorldY = data.WorldY;
                            if ((data.RegionId & 0x8000) == 0)
                            {
                                e.Proxy.Session.RawX = (short)data.RawX;
                                e.Proxy.Session.RawY = (short)data.RawY;
                                Logger.Debug("B021", $"[{DateTime.UtcNow:HH:mm:ss.fff}] pos uid={data.UniqueId} region=0x{data.RegionId:X4} rawX={data.RawX} rawY={data.RawY}");
                            }
                            e.Proxy.Session.LastMovementUpdate = DateTime.UtcNow;
                            e.Proxy.Session.RegionReadableName = RegionResolver.ResolveReadable(data.SectorX, data.SectorY, (short)data.RegionId);
                            e.Proxy.Session.RegionName = RegionResolver.Resolve((short)data.RegionId, data.SectorX, data.SectorY);

                            DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "movement_sync", new
                            {
                                regionReadableName = e.Proxy.Session.RegionReadableName,
                                regionName = e.Proxy.Session.RegionName,
                                regionId = e.Proxy.Session.RegionId,
                                wX = data.WorldX,
                                wY = data.WorldY,
                                wZ = (short)data.WorldZ,
                                xSec = data.SectorX,
                                ySec = data.SectorY,
                            });
                        }
                    }
                    else if (e.Proxy.Session != null && e.Proxy.Session.SpawnedObjects.ContainsKey(data.UniqueId))
                    {
                        e.Proxy.Session.SpawnedPositions[data.UniqueId] = (data.WorldX, data.WorldY);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("B021", $"Parse error: {ex.Message}");
                }
            });
        }
        /// <summary>
        /// Blocks the game client's own 0x7021 move packets from reaching the server while
        /// the pickup routine is running. The client independently heartbeats its last stored
        /// destination, which fights the pickup loop's proxy-injected _sendMove commands and
        /// causes the character to oscillate between the item and the prior position.
        /// </summary>
        public static void RegisterClientMovementSuppressor(Server _agentProxy)
        {
            _agentProxy.RegisterClientPacketHandler(Constant.CLIENT_MOVEMENT, (sender, e) =>
            {
                if (e.Proxy.Session?._attackLoop?.IsPickingUp == true)
                {
                    e.CancelTransfer = true;
                    Logger.Debug("PickupMove", $"[{DateTime.UtcNow:HH:mm:ss.fff}] Blocked client 0x7021 during pickup");
                }
            });
        }
        /// <summary>
        /// Handles the DLL client hello handshake. Fires before any session exists
        /// (character select screen). Reads account name + client version from the packet,
        /// cancels the transfer so the game server never sees it, and notifies the DLL
        /// via DllBridge if the version is incompatible.
        /// </summary>
        public static void RegisterVersionCheckHandler(Server _agentProxy)
        {
            _agentProxy.RegisterClientPacketHandler(Constant.DEW_CLIENT_HELLO, (sender, e) =>
            {
                e.CancelTransfer = true;

                try
                {
                    var packet = e.Packet.Clone();
                    byte nameLen = packet.ReadByte();
                    var nameBytes = new byte[nameLen];
                    for (int i = 0; i < nameLen; i++) nameBytes[i] = packet.ReadByte();
                    string accountName = System.Text.Encoding.ASCII.GetString(nameBytes);
                    ushort clientVersion = packet.ReadUShort();

                    string fmtExpected = $"{Constant.COMPAT_CLIENT / 100}.{Constant.COMPAT_CLIENT % 100:D2}";
                    string fmtGot      = $"{clientVersion / 100}.{clientVersion % 100:D2}";

                    if (clientVersion != Constant.COMPAT_CLIENT)
                    {
                        Logger.Warn("VersionCheck", $"Version mismatch for '{accountName}': client={fmtGot} required={fmtExpected}");
                        DllBridge.Instance.SendToDll(accountName, "version_mismatch", new
                        {
                            expected = fmtExpected,
                            got = fmtGot
                        });
                    }
                    else
                    {
                        Logger.Info("VersionCheck", $"Client version OK for '{accountName}': v{fmtGot}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("VersionCheck", $"Failed to parse DEW_CLIENT_HELLO: {ex.Message}");
                }
            });
        }

        public static void RegisterBuffHandler(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_BUFF_START, (sender, e) =>
            {
                var data = PacketParser.ParseBuffStart(e.Packet.Clone());
                if (data == null) return;

                if (data.TargetUID != e.Proxy.Session?.CharacterUID)
                    return;

                _ = Task.Run(async () =>
                {
                    var cached = await DBConnect.GetSkillById(data.RefSkillId);
                    if (cached == null) return;

                    var entry = new SR_Skill
                    {
                        ID = cached.ID,
                        CodeName = cached.CodeName,
                        Group = cached.Group,
                        Level = cached.Level,
                        ReadableName = cached.ReadableName,
                        BasicActivity = cached.BasicActivity,
                        PreparingTime = cached.PreparingTime,
                        CastingTime = cached.CastingTime,
                        ActionDuration = cached.ActionDuration,
                        ReuseDelay = cached.ReuseDelay,
                        CoolTime = cached.CoolTime,
                        Range = cached.Range,
                        AutoAttackType = cached.AutoAttackType,
                        CanUseInTown = cached.CanUseInTown,
                        RequiresTarget = cached.RequiresTarget,
                        ConsumeHP = cached.ConsumeHP,
                        ConsumeMP = cached.ConsumeMP,
                        AIAttackChance = cached.AIAttackChance,
                        AISkillType = cached.AISkillType,
                        MoveSpeedPercent = cached.MoveSpeedPercent,
                        Params = cached.Params,
                        Enabled = true,
                        TimedJobId = data.TimedJobId
                    };

                    lock (e.Proxy.Session!.Buffs)
                    {
                        e.Proxy.Session!.Buffs.RemoveAll(b => b.ID == data.RefSkillId);
                        e.Proxy.Session!.Buffs.Add(entry);
                    }

                    e.Proxy.Session!.ActiveBuffTimedJobToSkillId[data.TimedJobId] = data.RefSkillId;
                    Log("BuffHandler", $"Buff added: {entry.CodeName} (ID={data.RefSkillId}) TimedJobId={data.TimedJobId}");
                });
            });

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_BUFF_END, (sender, e) =>
            {
                var data = PacketParser.ParseBuffEnd(e.Packet.Clone());
                if (data == null) return;

                lock (e.Proxy.Session!.Buffs)
                {
                    var removed = e.Proxy.Session?.Buffs.FirstOrDefault(b => b.TimedJobId == data.TimedJobId);
                    if (removed != null)
                    {
                        e.Proxy.Session!.Buffs.Remove(removed);
                        Logger.Info("BuffHandler", $"Buff removed: {removed.CodeName} (ID={removed.ID}) TimedJobId={data.TimedJobId}");
                    }
                }

                if (e.Proxy.Session!.ActiveBuffTimedJobToSkillId.TryRemove(data.TimedJobId, out uint expiredSkillId))
                {
                    e.Proxy.Session!._attackLoop?.ClearSkillCooldown(expiredSkillId);
                    Logger.Info("BuffHandler", $"Buff expired: SkillId={expiredSkillId} TimedJobId={data.TimedJobId} — cooldown cleared");
                }
            });
        }
        public static void RegisterAttackHandlers(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_SKILL_ATTACK, (sender, e) =>
            {
                try
                {
                    var data = PacketParser.ParseSkillAttack(e.Packet.Clone());
                    if (data == null) return;
                    Log("AttackHandler", $"0xB074 type={data.Type:X2} status={data.Status:X2}");
                    e.Proxy.Session!._attackLoop?.OnSkillAttackResponse(data.Type, data.Status);
                }
                catch (Exception ex)
                {
                    Logger.Error("ServerAttack:0xb074", $"Error in SERVER_SKILL_ATTACK: {ex.Message}");
                }
            });

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_ATTACK, (sender, e) =>
            {
                try
                {
                    var data = PacketParser.ParseAttack(e.Packet.Clone());
                    if (data == null) return;
                    if (data.IsShortVariant)
                    {
                        Log("AttackHandler", "0xB070 short variant — skipping");
                        return;
                    }
                    Log("AttackHandler", $"0xB070 attacker={data.AttackerUID} target={data.TargetUID}");
                    e.Proxy.Session!._attackLoop?.OnAttackResult(data.AttackerUID, data.TargetUID);

                    // A mob that attacks the player is provably within melee/skill range.
                    var sess = e.Proxy.Session;
                    if (sess != null
                        && data.AttackerUID != sess.CharacterUID
                        && data.TargetUID == sess.CharacterUID
                        && sess.SpawnedObjects.ContainsKey(data.AttackerUID))
                    {
                        sess.SpawnedPositions[data.AttackerUID] = (sess.WorldX, sess.WorldY);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("ServerAttack:0xb070", $"Error in SERVER_ATTACK: {ex.Message}");
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
                Log("OnPlayerLevelUp", $"Icon paths fetched: {iconPaths.Count} for codes: {string.Join(", ", codeNames)}");
                foreach (var kvp in iconPaths)
                    Log("OnPlayerLevelUp", $"  {kvp.Key} -> {kvp.Value}");

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
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_DEAD, async (sender, e) =>
            {
                try
                {
                    var session = e.Proxy.Session;
                    if (session == null) return;

                    byte deathType = e.Packet.ReadByte();
                    Log("DeathHandler", $"Character died, type={deathType}");

                    await AchievementService.OnDeath(session.CharacterName!, e.Proxy!);
                    Interlocked.Increment(ref session.PlayerDeaths);
                }
                catch (Exception ex)
                {
                    Logger.Error(typeof(PlayerTools), $"Error occurred in death handler!: {ex.Message}");
                }
                
            });

        }
        public static void RegisterBotHandler(Server _agentProxy)
        {
            _agentProxy.RegisterClientPacketHandler(Constant.DEW_START_BOT, async (sender, e) =>
            {
                e.CancelTransfer = true;


                var session = e.Proxy.Session;
                
                if (session == null) return;

                if (session.PlayerStats!.CurrentLevel < SettingsLoader.Settings!.MinimumBotLevel)
                {
                    PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, $"Your level is too low to use botting features, unlocks at level {SettingsLoader.Settings.MinimumBotLevel}");
                    return;
                }

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

                string teleportDest = RegionResolver.Resolve((short)destination.RegionId, destination.SectorX, destination.SectorY);
                if (teleportDest.Contains("Qin-Shi") && session.PlayerStats!.CurrentLevel < 70)
                {
                    PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, $"Your level is too low to bot in Qin-Shi Tomb.");
                    return;
                }
                else if (teleportDest.Contains("Stone Cave") && session.PlayerStats!.CurrentLevel < 50)
                {
                    PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, $"Your level is too low to bot in the Stone Cave.");
                    return;
                }
                else if ((teleportDest.Contains("Kings Valley") ||
                        teleportDest.Contains("Holy Water Temple") ||
                        teleportDest.Contains("Alexandria") ||
                        teleportDest.Contains("Alexandria Job Cave (Black/Red Eggre)")) &&
                        session.PlayerStats!.CurrentLevel < 95)
                {
                    PlayerTools.SendToProxyChat(e.Proxy, ChatType.Notice, null, $"Your level is too low to bot in {teleportDest}.");
                    return;
                }
                
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
                                Log("SendMove", $"DUNGEON SX={pos.SectorX} SY={pos.SectorY} rawX={rx} rawZ={rz} rawY={ry} | worldX={pos.X:F1} worldY={pos.Y:F1}");
                                p.WriteUInt(rx);
                                p.WriteInt(rz);
                                p.WriteUInt(ry);
                            }
                            else
                            {
                                Log("SendMove", $"OVERWORLD SX={pos.SectorX} SY={pos.SectorY} XOff={pos.XOffset:F1} YOff={pos.YOffset:F1}");
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
                    proxy: proxy,
                    sendPacket: p => proxy.Server.Send(p),
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
                Log("DurabilityHandler", $"Slot {slot} durability -> {durability}");
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

                Log("0xB023", $"Stuck at region=0x{region:X4} world=({authPos.X:F1},{authPos.Y:F1}) xOff={xOffset} yOff={yOffset}");

                if ((region & 0x8000) == 0 && xOffset == 0f && yOffset == 0f)
                {
                    Logger.Warn("0xB023", $"GUARDED (0,0) stuck uid={uid} region=0x{region:X4} — session kept at ({e.Proxy.Session?.WorldX},{e.Proxy.Session?.WorldY})");
                    return;
                }

                if (e.Proxy.Session != null)
                {
                    // Capture prior world position before overwriting — needed for the
                    // HandleStuckPacket threshold check below.
                    float prevX = e.Proxy.Session.WorldX;
                    float prevY = e.Proxy.Session.WorldY;

                    e.Proxy.Session.RegionId = region;
                    e.Proxy.Session.RawX = (short)xOffset;
                    e.Proxy.Session.RawY = (short)yOffset;
                    e.Proxy.Session.Z = (short)z;
                    e.Proxy.Session.SectorX = authPos.SectorX;
                    e.Proxy.Session.SectorY = authPos.SectorY;
                    e.Proxy.Session.WorldX = (int)authPos.X;
                    e.Proxy.Session.WorldY = (int)authPos.Y;

                    bool pickingUp = e.Proxy.Session._attackLoop?.IsPickingUp == true;
                    float stuckDx = authPos.X - prevX;
                    float stuckDy = authPos.Y - prevY;
                    bool meaningfulCorrection = MathF.Sqrt(stuckDx * stuckDx + stuckDy * stuckDy) > 5f;
                    if (!pickingUp && meaningfulCorrection)
                    {
                        e.Proxy.Session._walker?.HandleStuckPacket(authPos);
                        e.Proxy.Session._attackLoop?.NotifyStuck();
                    }
                }
            });

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_OPEN_SHOP, (sender, e) =>
            {
                if (e.Proxy.Session != null)
                {
                    // If the bot owns this open, suppress B046 from the game client.
                    bool botOwned = e.Proxy.Session!.ShopOpenGate != null;
                    e.Proxy.Session!.ShopOpenGate?.TrySetResult(true);
                    if (botOwned)
                        e.CancelTransfer = true;
                }
            });

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_CLOSE_SHOP, (sender, e) =>
            {
                if (e.Proxy.Session != null)
                {
                    bool botOwned = e.Proxy.Session!.ShopCloseGate != null;
                    e.Proxy.Session!.ShopCloseGate?.TrySetResult(true);
                    if (botOwned)
                        e.CancelTransfer = true;
                }
            });

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_TARGET, (sender, e) =>
            {

                if (e.Proxy.Session != null)
                    e.Proxy.Session!.TargetGate?.TrySetResult(true);
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

            // Build ordered list
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

                            DllBridge.Instance.SendToDll(proxy.Session!.AccountName!, "session_sync", new
                            {
                                sessionSeconds = (int)proxy.Session.AccumulatedPlayTime.TotalSeconds,
                                sessionKills = proxy.Session.SessionKills,
                                totalSeconds = (int)proxy.Session.TotalPlayTime.TotalSeconds,
                                isAfk = proxy.Session.IsAfk ? 1 : 0,
                                accountJID = proxy.Session.JID,
                                isGM = proxy.Session.IsGM ? 1 : 0,
                            });
                    }
                    else
                    {
                        string back = SettingsLoader.Settings?.Proxy?.BackFromAfkMessage
                                      ?? "Welcome back!";

                        SendToProxyChat(proxy,
                            ChatType.Notice,
                            null,
                            SettingsLoader.FormatPlayerMessage(back, proxy));

                        DllBridge.Instance.SendToDll(proxy.Session!.AccountName!, "session_sync", new
                        {
                            sessionSeconds = (int)proxy.Session.AccumulatedPlayTime.TotalSeconds,
                            sessionKills = proxy.Session.SessionKills,
                            totalSeconds = (int)proxy.Session.TotalPlayTime.TotalSeconds,
                            isAfk = proxy.Session.IsAfk ? 1 : 0,
                            accountJID = proxy.Session.JID,
                            isGM = proxy.Session.IsGM ? 1 : 0,
                        });
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
        
        private static async Task BackfillMissedLevelRewardsAsync(Proxy proxy, byte currentLevel)
        {
            var session = proxy.Session!;
            var charName = session.CharacterName!;

            foreach (var rewardLevel in Overseer.LevelRewardOptions.Keys)
            {
                if (rewardLevel > currentLevel) continue;
                if (session.UnclaimedRewards.Contains(rewardLevel)) continue;

                bool alreadyClaimed = await DBConnect.HasClaimedLevelRewardAsync(charName, rewardLevel);
                if (alreadyClaimed) continue;

                Logger.Info("BackfillRewards", $"{charName} missed reward for level {rewardLevel}, backfilling");

                bool claimed = await DBConnect.ClaimLevelRewardAsync(charName, rewardLevel);
                if (!claimed) continue;

                await DBConnect.AddUnclaimedRewardAsync(charName, rewardLevel);
                session.UnclaimedRewards.Add(rewardLevel);
            }
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
                    Log("LevelTracker", $"CheckLevelUp: level={currentLevel} cumExp={currentExp} threshold={threshold}");

                    if (session.CumulativeExp < threshold)
                    {
                        Log("LevelTracker", $"CheckLevelUp: not enough exp ({currentExp} < {threshold}), done");
                        return;
                    }

                    session.PlayerStats.CurrentLevel = nextLevel;
                }

                bool alreadyClaimed = await DBConnect.HasClaimedLevelRewardAsync(session.CharacterName!, nextLevel);
                if (alreadyClaimed)
                {
                    Log("LevelTracker", $"CheckLevelUp: level {nextLevel} already claimed, continuing");
                    continue;
                }

                bool claimed = await DBConnect.ClaimLevelRewardAsync(session.CharacterName!, nextLevel);
                if (!claimed)
                {
                    Log("LevelTracker", $"CheckLevelUp: claim insert failed for level {nextLevel} (race), continuing");
                    continue;
                }

                Logger.Info("LevelTracker", $"{session.CharacterName} reached level {nextLevel}");
                await OnPlayerLevelUp(proxy, nextLevel);
            }
        }
        private static async Task OnPlayerLevelUp(Proxy proxy, byte newLevel)
        {
            DllBridge.Instance.SendToDll(proxy.Session!.AccountName!, "lvl_update", new { lvl = (int)newLevel });
            await AchievementService.OnLevelUp(proxy.Session!.CharacterName!, newLevel, proxy);

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
