using CoreLib.Tools.Logging;
using VSRO_CONTROL_API.Settings;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Framework;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network;
using VSRO_CONTROL_API.VSRO.Bots;
using VSRO_CONTROL_API.VSRO.DTO;
namespace VSRO_CONTROL_API.VSRO.AsynchronousProxy
{
    public class AgentTools
    {
        public static HashSet<int> _regionIds = new HashSet<int>();
        
        public static async Task Init()
        {
            try
            {
                _regionIds = await DBConnect.GetRegionIds();
            }
            catch (Exception ex)
            {
                Logger.Error("AgentTools.Init", $"Error ocurred during initialization: {ex.Message}");
            }
        }
        /// <summary>
        /// Registers player login handler.
        /// This is what creates the session object initially.
        /// </summary>
        /// <param name="_agentProxy">Proxy object to act upon</param>
        public static void RegisterPlayerLoginHandler(Server _agentProxy)
        {
            _agentProxy.RegisterClientPacketHandler(Constant.CLIENT_INGAME_REQUEST, async (sender, e) =>
            {
                var packet = e.Packet;
                var charName = packet.ReadAscii();

                string userIp = e.Proxy.Client.Socket.RemoteEndPoint?.ToString() ?? string.Empty;

                var acc = await DBConnect.GetJIDByCharName(charName);
                if (!acc.success)
                    return;

                var now = DateTime.UtcNow;
                var userName = await DBConnect.GetUserNameByJID(acc.jid);
                if (userName.success == false)
                {
                    Logger.Warn("PlayerLoginHandler", $"Couldnt get username by jid: {userName.reason} | JID={acc.jid}");
                }
                e.Proxy.Session = new PlayerSession
                {
                    CharacterName = charName,
                    AccountName = userName.userName,
                    JID = acc.jid,
                    IP = userIp,
                    LoginTime = now,
                    LastActivity = now,
                    AccumulatedPlayTime = TimeSpan.Zero,
                    IsAfk = false,
                };

                var unclaimed = await DBConnect.GetUnclaimedRewardsAsync(charName);
                e.Proxy.Session.UnclaimedRewards = unclaimed;

                DllBridge.Instance.SendToDll(userName.userName!, "session_init", new
                {
                    charName = charName,
                    jid = acc.jid,
                    accName = userName.userName
                });

                e.Proxy.Session!.SessionTokenSource = new CancellationTokenSource();

                _ = Task.Run(() =>
                    PlayerTools.RunSessionTracker(e.Proxy, e.Proxy.Session!.SessionTokenSource.Token)
                );

                e.Proxy.OnPlaytimeHourReached += async (p, session) =>
                {
                    try
                    {

                        Logger.Debug(p, $"{session.CharacterName} reached {session.RewardedHours} hours");

                        if (SettingsLoader.Settings?.Proxy?.SilkAmountPerHours is int amount)
                        {
                            var result = await DBConnect.AddSilkToUserByJID(session.JID, amount);

                            if (result.success)
                            {
                                Logger.Debug(p, $"Added silk to user ID {session.JID}. Silk given: {amount}. Session Time: {session.AccumulatedPlayTime}");
                                PlayerTools.SendToProxyChat(p, PlayerTools.ChatType.Notice, null, $"You have been rewarded {amount} silk! Session time: {session.AccumulatedPlayTime}");
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Nothing for this piece of shit handler
                    }

                };
                var session = e.Proxy.Session;
                if (session._attackLoop == null)
                {
                    session._attackLoop = new AttackLoop(
                        getPosition: () => session.GetEstimatedPosition(),
                        getNearbyMobs: () => session.SpawnedPositions.ToDictionary(
                            kvp => kvp.Key,
                            kvp => {
                                bool isDungeon = (session.RegionId & 0x8000) != 0;
                                return isDungeon
                                    ? BotPosition.FromDisplayWorldDungeon(kvp.Value.WorldX, kvp.Value.WorldY, 0, (ushort)session.RegionId)
                                    : BotPosition.FromDisplayWorld(kvp.Value.WorldX, kvp.Value.WorldY);
                            }
                        ),
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
                        session: session,
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
                                    p.WriteUInt((uint)(int)pos.XOffset);
                                    p.WriteInt((int)pos.ZOffset);
                                    p.WriteUInt((uint)(int)pos.YOffset);
                                }
                                else
                                {
                                    p.WriteShort((short)pos.XOffset);
                                    p.WriteShort((short)pos.ZOffset);
                                    p.WriteShort((short)pos.YOffset);
                                }

                                e.Proxy.Server.Send(p);
                            }
                            catch (Exception ex)
                            {
                                Logger.Warn("Bot", $"sendMove failed: {ex.Message}");
                            }
                        }
                    );
                }

                Logger.Info(typeof(Overseer), $"Player logged in: {e.Proxy.Session.CharacterName}");
            });
        }
        
        /// <summary>
        /// Registers player logout handler.
        /// Sends disconnect info to dll client.
        /// </summary>
        /// <param name="_agentProxy">Proxy object to act upon</param>
        public static void RegisterPlayerLogoutHandler(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_COUNTDOWN, async (sender, e) =>
            {
                var proxy = e.Proxy;
                var session = proxy.Session;
                if (proxy.Session != null)
                {
                    DllBridge.Instance.SendToDll(session.AccountName!, "session_clear", new { });
                }
            });

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_COUNTDOWN_INTERRUPT, async (sender, e) =>
            {
                var proxy = e.Proxy;
                var session = proxy.Session;
                if (proxy.Session != null)
                {
                    DllBridge.Instance.SendToDll(session.AccountName!, "session_init", new
                    {
                        charName = session.CharacterName,
                        jid = session.JID,
                        accName = session.AccountName
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

                    DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "unclaimed_rewards", new
                    {
                        levels = e.Proxy.Session.UnclaimedRewards.Select(b => (int)b).ToArray()
                    });

                    var payload = new
                    {
                        hp = e.Proxy.Session.PlayerStats!.CurrentHP,
                        mp = e.Proxy.Session.PlayerStats!.CurrentMP,
                        sessionKills = e.Proxy.Session.SessionKills,
                        unusedStatPoints = e.Proxy.Session.PlayerStats.UnusedStatPoints,
                        currentLevel = e.Proxy.Session.PlayerStats.CurrentLevel,
                        gold = e.Proxy.Session.PlayerStats.RemainingGold,
                    };
                    DllBridge.Instance.SendToDll(e.Proxy.Session.AccountName!, "char_init", payload);

                }
            });
        }
        
        /// <summary>
        /// Registers all party tracking handlers
        /// Currently handles: Creation, Changes, Deletion
        /// </summary>
        /// <param name="_agentProxy">Proxy object to act upon</param>
        public static void RegisterPartyHandlers(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_PARTY_CHANGES, (sender, e) =>
            {
                try
                {
                    var packet = e.Packet;
                    byte changeType = packet.ReadByte();

                    switch (changeType)
                    {
                        case 1: // Member left
                            {
                                byte flag = packet.ReadByte();
                                uint memberUID = packet.ReadUInt();
                                Logger.Debug("PartyChangesHandler", $"Member left (UID={memberUID})");
                                break;
                            }

                        case 2: // Member joined
                            {
                                byte flag = packet.ReadByte();
                                uint memberUID = packet.ReadUInt();
                                string name = packet.ReadAscii();
                                uint refObjId = packet.ReadUInt();

                                Logger.Debug("PartyChangesHandler", $"Member joined: {name} (UID={memberUID})");
                                break;
                            }

                        default:
                            {
                                Logger.Debug("PartyChangesHandler", $"Unknown changeType={changeType}, remaining bytes logged");
                                break;
                            }
                    }
                
                }
                catch (Exception ex)
                {
                    Logger.Error($"PartyChangesHandler", $"There was an error handling party changes!: {ex.Message}");
                }
                
            });
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_PARTY_DELETE, (sender, e) =>
            {
                try
                {
                    var packet = e.Packet;
                    byte result = packet.ReadByte();
                    if (result != 1) return;

                    uint partyId = packet.ReadUInt();

                    e.Proxy.Session!.PlayerParty = null;
                    Logger.Debug("PartyDeleteHandler", $"Party deleted: ID={partyId}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"PartyChangesHandler", $"There was an error handling party delete!: {ex.Message}");
                }
            });
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_PARTY_FORM, (sender, e) =>
            {
                try
                {
                    var packet = e.Packet;
                    byte result = packet.ReadByte();
                    if (result != 1) return;

                    uint partyId = packet.ReadUInt();
                    uint unk1 = packet.ReadUInt();
                    byte partyType = packet.ReadByte();
                    byte unk2 = packet.ReadByte();
                    byte allowInvite = packet.ReadByte();
                    byte minLevel = packet.ReadByte();
                    string message = packet.ReadAscii();

                    e.Proxy.Session!.PlayerParty = new Party
                    {
                        PartyID = partyId,
                        Leader = e.Proxy,
                        Message = message,
                        Members = { e.Proxy }
                    };

                    Logger.Debug("PartyHandler",
                        $"Party formed: ID={partyId} msg='{message}' minLvl={minLevel}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"PartyChangesHandler", $"There was an error handling party form!: {ex.Message}");
                }

            });

        }

        /// <summary>
        /// Registers the spawn trackers for group spawns.
        /// Currently tracks: NPC's, Monsters, and Teleporters
        /// </summary>
        /// <param name="_agentProxy">Proxy object to act upon</param>
        public static void RegisterSpawnTrackerImproved(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_GROUPSPAWN_HEAD, (s, e) =>
            {
                var packet = e.Packet.Clone();
                var proxy = e.Proxy;
                proxy.Session!.CurrentGroupSpawnType = packet.ReadByte(); // 1=spawn, 2=despawn
                byte count = packet.ReadByte();
            });

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_GROUPSPAWN_BODY, async (s, e) =>
            {
                var packet = e.Packet.Clone();
                var proxy = e.Proxy;

                try
                {
                    if (proxy.Session!.CurrentGroupSpawnType == 2)
                    {
                        while (packet.RemainingRead() >= 4)
                        {
                            uint despawnUID = packet.ReadUInt();
                            proxy.Session!.SpawnedObjects.TryRemove(despawnUID, out _);
                            proxy.Session!.SpawnedPositions.TryRemove(despawnUID, out _);
                            proxy.Session!.MobUIDs.TryRemove(despawnUID, out _);
                        }
                        return;
                    }

                    while (packet.RemainingRead() >= 10)
                    {
                        uint refObjID = packet.ReadUInt();
                        if (refObjID == 0 || refObjID > 100000) break;

                        var record = await DBConnect.GetItemRecord(refObjID);
                        if (record.item == null)
                        {
                            Logger.Warn("SpawnBody::Record", $"Unknown refObjID={refObjID} — cannot skip, aborting packet parse");
                            break;
                        }
                        string code = record.item.CodeName;

                        if (code.StartsWith("ITEM_") || code.StartsWith("GOLD"))
                        {
                            if (code.Contains("QNO") || code.Contains("SNOWFLAKE"))
                            {
                                ushort nameLen = packet.ReadUShort();
                                for (int i = 0; i < nameLen; i++) packet.ReadByte();
                            }

                            bool isGold = code.StartsWith("ITEM_ETC_GOLD") || code.StartsWith("GOLD");

                            uint goldAmount = 0;
                            if (isGold)
                                goldAmount = packet.ReadUInt(); // gold amount prepended

                            uint itemUID = packet.ReadUInt();

                            int itemWorldX, itemWorldY;
                            if (isGold)
                            {
                                byte gsx = packet.ReadByte();
                                byte gsy = packet.ReadByte();
                                float gx = packet.ReadFloat();
                                float gz = packet.ReadFloat();
                                float gy = packet.ReadFloat();
                                var (goldPos, _, _) = BotPosition.FromRawOffsets(gsx, gsy, gx, gy);
                                itemWorldX = (int)goldPos.X;
                                itemWorldY = (int)goldPos.Y;
                            }
                            else
                            {
                                ushort itemRegionID = packet.ReadUShort();
                                float _x = packet.ReadFloat();
                                float _z = packet.ReadFloat();
                                float _y = packet.ReadFloat();
                                byte itemSX = (byte)(itemRegionID & 0xFF);
                                byte itemSY = (byte)((itemRegionID >> 8) & 0xFF);
                                var (itemPos, _, _) = BotPosition.FromRawOffsets(itemSX, itemSY, _x, _y);
                                itemWorldX = (int)itemPos.X;
                                itemWorldY = (int)itemPos.Y;
                            }

                            packet.ReadUShort(); // angle
                            byte hasOwner = packet.ReadByte();
                            uint ownerJID = 0;
                            if (hasOwner != 0)
                                ownerJID = packet.ReadUInt();
                            packet.ReadByte(); // isBlue

                            proxy.Session!.DroppedItems[itemUID] = (refObjID, code, itemWorldX, itemWorldY);
                            Logger.Debug("SpawnBody::Item", $"Tracked drop {code} uid={itemUID} gold={goldAmount} owner={ownerJID} world=({itemWorldX},{itemWorldY}) remaining={packet.RemainingRead()}");
                            continue;
                        }

                        // --- read common position header ---
                        uint spawnUID = packet.ReadUInt();
                        byte xsec = packet.ReadByte();
                        byte ysec = packet.ReadByte();
                        float rawX = packet.ReadFloat();
                        float rawZ = packet.ReadFloat();
                        float rawY = packet.ReadFloat();
                        packet.ReadUShort(); // angle

                        var (entityPos, sx, sy) = BotPosition.FromRawOffsets(xsec, ysec, rawX, rawY);
                        int worldX = (int)entityPos.X;
                        int worldY = (int)entityPos.Y;
                        ushort regionID = (ushort)((sy << 8) | sx);

                        // --- STORE (portals/gates) ---
                        if (code.StartsWith("STORE"))
                        {
                            proxy.Session!.SpawnedObjects[spawnUID] = (refObjID, (short)regionID, refObjID);
                            proxy.Session!.SpawnedPositions[spawnUID] = (worldX, worldY);
                            packet.ReadUInt();
                            packet.ReadULong();
                            Logger.Debug("SpawnBody", $"Parsed {code} uid={spawnUID} ObjID={refObjID} world=({worldX},{worldY}) remaining={packet.RemainingRead()}");
                            continue;
                        }

                        // --- Movement block ---
                        byte hasDestination = packet.ReadByte();

                        if (!code.StartsWith("NPC"))
                            packet.ReadByte(); // isRunning — MOB/COS only

                        if (hasDestination == 1)
                        {
                            byte dxsec = packet.ReadByte();
                            byte dysec = packet.ReadByte();
                            bool isDungeonDest = PlayerTools.IsDungeon((ushort)((dysec << 8) | dxsec));

                            if (isDungeonDest)
                            {
                                // Dungeon destination - 4 ints
                                packet.ReadInt();  // destX
                                packet.ReadInt();  // destZ  
                                packet.ReadInt();  // destY
                            }
                            else
                            {
                                // Overworld destination — 3 ushorts
                                packet.ReadUShort();
                                packet.ReadUShort();
                                packet.ReadUShort();
                            }
                        }
                        else if (code.StartsWith("MOB"))
                        {
                            packet.ReadByte();
                            packet.ReadUShort();
                        }

                        // Register
                        if (code.StartsWith("MOB"))
                            proxy.Session!.MobUIDs[spawnUID] = 1;

                        proxy.Session!.SpawnedObjects[spawnUID] = (refObjID, (short)regionID, refObjID);
                        proxy.Session!.SpawnedPositions[spawnUID] = (worldX, worldY);

                        Logger.Debug("SpawnBody", $"Parsed {code} uid={spawnUID} world=({worldX},{worldY}) remaining={packet.RemainingRead()} hasDest={hasDestination}");
                        SkipEntityTail(packet, record.item);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("SpawnBody", $"Parse failed: {ex.Message} — remaining={packet.RemainingRead()}");
                }
            });
            
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_GROUPSPAWN_TAIL, (s, e) =>
            {
                var proxy = e.Proxy;
                proxy.Session!.CurrentGroupSpawnType = 0; // reset
            });

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_SPAWN, async (s, e) =>
            {
                var packet = e.Packet.Clone();
                if (packet.RemainingRead() < 10) return;

                uint refObjID = packet.ReadUInt();
                var record = await DBConnect.GetItemRecord(refObjID);

                if (record.item == null)
                {
                    Logger.Warn("SERVER_SPAWN", $"Unknown refObjID={refObjID} — aborting");
                    return;
                }

                string code = record.item.CodeName;

                if (code.StartsWith("ITEM_") || code.StartsWith("GOLD"))
                {
                    if (code.Contains("QNO") || code.Contains("SNOWFLAKE"))
                    {
                        ushort nameLen = packet.ReadUShort();
                        for (int i = 0; i < nameLen; i++) packet.ReadByte();
                    }

                    bool isGold = code.StartsWith("ITEM_ETC_GOLD") || code.StartsWith("GOLD");

                    uint goldAmount = 0;
                    if (isGold)
                        goldAmount = packet.ReadUInt(); // gold amount prepended

                    uint itemUID = packet.ReadUInt();

                    int itemWorldX, itemWorldY;
                    if (isGold)
                    {
                        // Gold uses separate sector bytes like entities
                        byte gsx = packet.ReadByte();
                        byte gsy = packet.ReadByte();
                        float gx = packet.ReadFloat();
                        float gz = packet.ReadFloat();
                        float gy = packet.ReadFloat();
                        var (goldPos2, _, _) = BotPosition.FromRawOffsets(gsx, gsy, gx, gy);
                        itemWorldX = (int)goldPos2.X;
                        itemWorldY = (int)goldPos2.Y;
                    }
                    else
                    {
                        ushort itemRegionID = packet.ReadUShort();
                        float _x = packet.ReadFloat();
                        float _z = packet.ReadFloat();
                        float _y = packet.ReadFloat();
                        byte itemSX = (byte)(itemRegionID & 0xFF);
                        byte itemSY = (byte)((itemRegionID >> 8) & 0xFF);
                        var (itemPos2, _, _) = BotPosition.FromRawOffsets(itemSX, itemSY, _x, _y);
                        itemWorldX = (int)itemPos2.X;
                        itemWorldY = (int)itemPos2.Y;
                    }

                    packet.ReadUShort(); // angle
                    byte hasOwner = packet.ReadByte();
                    uint ownerJID = 0;
                    if (hasOwner != 0)
                        ownerJID = packet.ReadUInt();
                    packet.ReadByte(); // isBlue

                    e.Proxy.Session!.DroppedItems[itemUID] = (refObjID, code, itemWorldX, itemWorldY);
                    Logger.Debug("SpawnBody::Item", $"Tracked drop {code} uid={itemUID} gold={goldAmount} owner={ownerJID} world=({itemWorldX},{itemWorldY}) remaining={packet.RemainingRead()}");
                    return;
                }

                uint spawnUID = packet.ReadUInt();
                byte xsec = packet.ReadByte();
                byte ysec = packet.ReadByte();
                float rawX = packet.ReadFloat();
                float rawZ = packet.ReadFloat();
                float rawY = packet.ReadFloat();
                packet.ReadUShort(); // angle

                var (spawnPos, sx, sy) = BotPosition.FromRawOffsets(xsec, ysec, rawX, rawY);
                int worldX = (int)spawnPos.X;
                int worldY = (int)spawnPos.Y;
                ushort regionID = (ushort)((sy << 8) | sx);

                if (code.StartsWith("MOB"))
                    e.Proxy.Session!.MobUIDs[spawnUID] = 1;

                e.Proxy.Session!.SpawnedObjects[spawnUID] = (refObjID, (short)regionID, refObjID);
                e.Proxy.Session!.SpawnedPositions[spawnUID] = (worldX, worldY);

                Logger.Debug("SERVER_SPAWN", $"Parsed {code} uid={spawnUID} world=({worldX:F1},{worldY:F1})");
            });

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_DESPAWN, (s, e) =>
            {
                var packet = e.Packet.Clone();
                uint spawnUID = packet.ReadUInt();
                e.Proxy.Session!.MobUIDs.TryRemove(spawnUID, out _);
                e.Proxy.Session!.SpawnedObjects.TryRemove(spawnUID, out _);
                e.Proxy.Session!.SpawnedPositions.TryRemove(spawnUID, out _);
                e.Proxy.Session!.DroppedItems.TryRemove(spawnUID, out _);
            });
        }

        private static void SkipEntityTail(Packet packet, ItemRecord record)
        {
            try
            {
                string code = record.CodeName;

                if (code.StartsWith("MOB"))
                {
                    packet.ReadByte();  // alive
                    packet.ReadByte();  // unknown
                    packet.ReadByte();  // unknown
                    packet.ReadByte();  // zerk active
                    packet.ReadFloat(); // walk speed
                    packet.ReadFloat(); // run speed
                    packet.ReadFloat(); // zerk speed
                    packet.ReadUInt();  // unknown
                    packet.ReadByte();  // type (passive/predator/etc)
                }
                else if (code.StartsWith("NPC") || code.StartsWith("STORE"))
                {
                    packet.ReadULong();  // unknown (8 bytes)
                    packet.ReadULong();  // unknown (8 bytes)
                    packet.ReadFloat();  // speed 100.0 (4 bytes)
                    ushort check = packet.ReadUShort();  // (2 bytes) = 22 minimum
                    if (check != 0)
                    {
                        byte count = packet.ReadByte();
                        for (int i = 0; i < count; i++)
                            packet.ReadByte();
                    }
                }
                else if (code.StartsWith("COS"))
                {
                    // nothing needed yet
                }
                else if (code.StartsWith("CHAR"))
                {
                    // nothing needed yet
                }
                
            }
            catch (Exception ex)
            {
                Logger.Error("PlayerTools::GroupSpawnBody", ex.Message);
            }

        }

        /// <summary>
        /// Registers tracking for the players last known target.
        /// Used for detecting NPC runtime ID's
        /// </summary>
        /// <param name="_agentProxy">Proxy object to act upon</param>
        public static void RegisterTargetTracker(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_TARGET, (sender, e) =>
            {
                var packet = e.Packet;
                byte result = packet.ReadByte();
                if (result != 1) return;
                uint targetUID = packet.ReadUInt();
                e.Proxy.Session!.LastTargetUID = targetUID;
            });
        }

        /// <summary>
        /// Registers tracking for the players UID.
        /// Required for HPMP update handler, basically your runtime ID.
        /// </summary>
        /// <param name="_agentProxy">Proxy object to act upon</param>
        public static void RegisterCharacterUIDTracker(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_CHAR_ID, (sender, e) =>
            {
                var packet = e.Packet;
                uint charUID = packet.ReadUInt();
                uint charID = packet.ReadUInt();

                if (e.Proxy.Session != null)
                {
                    e.Proxy.Session.CharacterUID = charUID;
                    e.Proxy.Session.CharacterID = charID;
                    Logger.Debug("CharID", $"CharacterUID=0x{charUID:X} CharacterID=0x{charID:X}");
                }
            });
        }
    }
}
