using CoreLib.Tools.Logging;
using Newtonsoft.Json.Linq;
using VSRO_CONTROL_API.Settings;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network;
using VSRO_CONTROL_API.VSRO.DTO;

namespace VSRO_CONTROL_API.VSRO.AsynchronousProxy
{
    public class AgentTools
    {
        #region - Obsoletes for archiving -

        [Obsolete("Legacy spawn tracker. Replaced by RegisterSpawnTrackerImproved. Do not register.")]
        public static void RegisterSpawnTracker(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_SPAWN, (sender, e) =>
            {
                var packet = e.Packet;
                if (packet.RemainingRead() < 8) return;
                uint refObjID = packet.ReadUInt();
                uint spawnUID = packet.ReadUInt();
                //if (Overseer.ShopNPCIds.Contains((int)refObjID))
                //    e.Proxy.SpawnedObjects[spawnUID] = refObjID;
            });

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_GROUPSPAWN_BODY, (sender, e) =>
            {
                var packet = e.Packet;
                if (packet.RemainingRead() < 8) return;
                uint refObjID = packet.ReadUInt();
                uint spawnUID = packet.ReadUInt();
                //if (Overseer.ShopNPCIds.Contains((int)refObjID))
                //   e.Proxy.SpawnedObjects[spawnUID] = refObjID;
            });

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_DESPAWN, (sender, e) =>
            {
                var packet = e.Packet;
                uint spawnUID = packet.ReadUInt();
                e.Proxy.SpawnedObjects.TryRemove(spawnUID, out _);
            });
        }

        #endregion

        public static HashSet<int> _regionIds = new HashSet<int>();
        private static Dictionary<byte, List<short>> _regionsByMarker = new Dictionary<byte, List<short>>();

        public static void BuildRegionIndex(IEnumerable<short> _regionIds)
        {
            _regionsByMarker.Clear();

            foreach (var regionId in _regionIds)
            {
                byte marker = (byte)(regionId & 0xFF);

                if (!_regionsByMarker.TryGetValue(marker, out var list))
                {
                    list = new List<short>();
                    _regionsByMarker[marker] = list;
                }

                list.Add(regionId);
            }
        }
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

                e.Proxy.Session = new PlayerSession
                {
                    CharacterName = charName,
                    JID = acc.jid,
                    IP = userIp,
                    LoginTime = now,
                    LastActivity = now,
                    AccumulatedPlayTime = TimeSpan.Zero,
                    IsAfk = false,
                };

                e.Proxy.SessionTokenSource = new CancellationTokenSource();

                _ = Task.Run(() =>
                    PlayerTools.RunSessionTracker(e.Proxy, e.Proxy.SessionTokenSource.Token)
                );

                e.Proxy.OnPlaytimeHourReached += async (p, session) =>
                {
                    if (session == null || string.IsNullOrWhiteSpace(session.CharacterName))
                        return;

                    Logger.Debug(p, $"{session.CharacterName} reached {session.RewardedHours} hours");

                    if (SettingsLoader.Settings?.Proxy?.SilkAmountPerHours is int amount)
                    {
                        var result = await DBConnect.AddSilkToUserByJID(session.JID, amount);

                        if (result.success)
                        {
                            Logger.Debug(p, $"Added silk to user ID {session.JID}. Silk given: {amount}. Session Time: {session.AccumulatedPlayTime}");
                            PlayerTools.SendToProxyChat(p, PlayerTools.ChatType.General, "FoxProxy", $"You have been rewarded {amount} silk! Session time: {session.AccumulatedPlayTime}");
                        }
                    }
                };

                Logger.Info(typeof(Overseer), $"Player logged in: {e.Proxy.Session.CharacterName}");
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
        /// Currently tracks: NPC's, Monsters
        /// </summary>
        /// <param name="_agentProxy">Proxy object to act upon</param>
        public static void RegisterSpawnTrackerImproved(Server _agentProxy)
        {
            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_GROUPSPAWN_HEAD, (s, e) =>
            {
                var packet = e.Packet.Clone();
                var proxy = e.Proxy;
                proxy.CurrentGroupSpawnType = packet.ReadByte(); // 1=spawn, 2=despawn
                byte count = packet.ReadByte();
            });

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_GROUPSPAWN_BODY, (s, e) =>
            {
                var packet = e.Packet;
                var proxy = e.Proxy;

                if (proxy.CurrentGroupSpawnType == 2)
                {
                    // Despawn list (unchanged)
                    while (packet.RemainingRead() >= 4)
                    {
                        uint despawnUID = packet.ReadUInt();
                        proxy.SpawnedObjects.TryRemove(despawnUID, out _);
                    }
                    return;
                }

                // Spawn group — reliable byte scan (works in EVERY region)
                byte[] raw = packet.GetBytes();

                for (int i = 0; i <= raw.Length - 10; i++)   // need at least ref + uid + region
                {
                    uint refObjID = BitConverter.ToUInt32(raw, i);
                    uint spawnUID = BitConverter.ToUInt32(raw, i + 4);
                    ushort regionID = BitConverter.ToUInt16(raw, i + 8);

                    // Same validation you already had + region now
                    if (refObjID < 1 || refObjID > 100000 ||
                        spawnUID == 0 ||
                        !_regionIds.Contains((int)regionID))
                    {
                        continue;
                    }


                    // Store the tuple your code now expects
                    proxy.SpawnedObjects[spawnUID] = (refObjID, (short)regionID);

                    // Skip past this entity (entities are ~55 bytes in 1.188; 50 is safe and prevents false matches inside data)
                    i += 50;
                }
            });

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_GROUPSPAWN_TAIL, (s, e) =>
            {
                var proxy = e.Proxy;
                proxy.CurrentGroupSpawnType = 0; // reset
            });

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_SPAWN, (s, e) =>
            {
                var packet = e.Packet.Clone();
                if (packet.RemainingRead() < 10) return;
                uint refObjID = packet.ReadUInt();
                uint spawnUID = packet.ReadUInt();
                ushort regionID = packet.ReadUShort();
                e.Proxy.SpawnedObjects[spawnUID] = (refObjID, (short)regionID);
            });

            _agentProxy.RegisterServerPacketHandler(Constant.SERVER_DESPAWN, (s, e) =>
            {
                var packet = e.Packet.Clone();
                uint spawnUID = packet.ReadUInt();
                e.Proxy.SpawnedObjects.TryRemove(spawnUID, out _);
            });
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
                e.Proxy.LastTargetUID = targetUID;
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
