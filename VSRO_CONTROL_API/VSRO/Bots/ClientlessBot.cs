using CoreLib.Tools.Logging;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.DTO;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Framework;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network;
using VSRO_CONTROL_API.VSRO.Bot.DTO;
using VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.Tools;
using static System.Collections.Specialized.BitVector32;

/// NOT USED YET
namespace VSRO_CONTROL_API.VSRO.Bots
{
    public class ClientlessBot
    {
        private Client? _gateway;
        private Client? _agent;
        private BotConfig _config;
        private ClientlessBotState _state = ClientlessBotState.Disconnected;
        private CancellationTokenSource _cts = new();

        public ClientlessCharState CharState { get; } = new();

        // Signals from packet responses
        private TaskCompletionSource<bool>? _skillCastTcs;
        private TaskCompletionSource<bool>? _entitySelectTcs;

        public ClientlessBot(BotConfig config)
        {
            _config = config;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _state = ClientlessBotState.ConnectingGateway;

            _gateway = new Client();
            _gateway.OnPacketReceived += OnGatewayPacket;
            _gateway.OnDisconnect += (s, e) => Stop();
            _gateway.BeginConnect(_config.GatewayHost, _config.GatewayPort);
        }

        public void Stop()
        {
            _cts.Cancel();
            _state = ClientlessBotState.Disconnected;
            _gateway?.Close();
            _agent?.Close();
        }

        #region - Gateway Connection -

        private void OnGatewayPacket(object sender, Client.PacketReceivedEventArgs e)
        {
            var p = e.Packet;
            switch (p.Opcode)
            {
                case 0x2001: // GLOBAL_IDENTIFICATION

                    SendLogin();
                    break;

                case 0xA102: // SERVER_LOGIN_RESPONSE
                    HandleLoginResponse(p);
                    break;

                case 0xA101: // SERVER_SHARD_LIST
                    HandleShardList(p);
                    break;
            }
        }

        private void SendLogin()
        {
            // 0x6102 = CLIENT_LOGIN_REQUEST
            var p = new Packet(0x6102);
            p.WriteByte(_config.Locale);        // locale (e.g. 0x22 = US/global)
            p.WriteAscii(_config.Username);
            p.WriteAscii(_config.Password);    // already MD5'd if needed
            p.WriteUShort(_config.ServerId);
            _gateway!.Send(p);
            _state = ClientlessBotState.LoggingIn;
        }

        private void HandleLoginResponse(Packet p)
        {
            byte result = p.ReadByte();
            if (result == 1)
            {
                // Success — read agent redirect
                uint sessionId = p.ReadUInt();
                string agentHost = p.ReadAscii();
                ushort agentPort = p.ReadUShort();

                // Done with gateway
                _gateway?.Close();
                _gateway = null;

                // Connect to agent
                ConnectAgent(sessionId, agentHost, agentPort);
            }
            else
            {
                byte error = p.ReadByte();
                // Log error, stop
                Stop();
            }
        }

        private void HandleShardList(Packet p)
        {
            // Read through farm list
            while (p.ReadByte() == 1)
            {
                p.ReadByte();   // farmID
                p.ReadAscii();  // farmName
            }
            // Read server list, find our target
            while (p.ReadByte() == 1)
            {
                ushort serverId = p.ReadUShort();
                string serverName = p.ReadAscii();
                p.ReadUShort(); // players
                p.ReadUShort(); // maxPlayers
                p.ReadByte();   // isAvailable
                p.ReadByte();   // farmID

                // Store if this is our target server
                if (serverId == _config.ServerId)
                    CharState.ServerName = serverName;
            }
        }



        #endregion

        #region - Agent Connection - 

        private void ConnectAgent(uint sessionId, string host, ushort port)
        {
            _state = ClientlessBotState.SelectingCharacter;
            CharState.AgentSessionId = sessionId;

            _agent = new Client();
            _agent.OnPacketReceived += OnAgentPacket;
            _agent.OnDisconnect += (s, e) => Stop();
            _agent.OnConnect += (s, e) => {
                // Send agent auth immediately on connect
                SendAgentAuth(sessionId);
            };
            _agent.BeginConnect(host, port);
        }

        private void SendAgentAuth(uint sessionId)
        {
            // 0x6103 = CLIENT_AGENT_AUTH
            var p = new Packet(0x6103);
            p.WriteUInt(sessionId);
            p.WriteAscii(_config.Username);
            p.WriteAscii(_config.Password);
            p.WriteByte(_config.Locale);
            _agent!.Send(p);
        }

        private void OnAgentPacket(object sender, Client.PacketReceivedEventArgs e)
        {
            var p = e.Packet;
            switch (p.Opcode)
            {
                case 0xB007: // CHARACTER_LIST_RESPONSE
                    HandleCharacterList(p);
                    break;

                case 0x34A5:
                    _state = ClientlessBotState.LoadingCharData;
                    break;

                case Constant.SERVER_CHARDATA: // CHARACTER_DATA — your existing handler logic
                    HandleCharData(p);
                    break;

                case 0x3015: // ENTITY_SPAWN
                    //HandleEntitySpawn(p);
                    break;

                case 0x3016: // ENTITY_DESPAWN
                    //HandleEntityDespawn(p);
                    break;

                case Constant.SERVER_CHAR_ID:
                    HandleCharID(p);
                    break;

                case 0x3057: // SKILL_CAST_RESPONSE
                    _skillCastTcs?.TrySetResult(true);
                    break;

                case 0x3021: // ENTITY_SELECT_RESPONSE
                    uint uid = p.ReadUInt();
                    CharState.SelectedEntityUID = uid;
                    _entitySelectTcs?.TrySetResult(true);
                    break;

                case 0x36F6: // PING — keep alive
                    var pong = new Packet(0x36F7);
                    _agent!.Send(pong);
                    break;
            }
        }

        private void HandleCharacterList(Packet p)
        {
            byte result = p.ReadByte();
            if (result != 1) { Stop(); return; }

            byte charCount = p.ReadByte();
            string? targetName = null;

            for (byte i = 0; i < charCount; i++)
            {
                uint modelID = p.ReadUInt();
                string name = p.ReadAscii();
                byte scale = p.ReadByte();
                byte level = p.ReadByte();
                ulong exp = p.ReadULong();
                ushort str = p.ReadUShort();
                ushort intel = p.ReadUShort();
                ushort statPoints = p.ReadUShort();
                uint hp = p.ReadUInt();
                uint mp = p.ReadUInt();
                bool isDeleting = p.ReadByte() == 0x01 ? true : false;
                if (isDeleting) p.ReadUInt(); // deletion timer

                byte guildMember = p.ReadByte();
                bool guildRename = p.ReadByte() == 0x01 ? true : false;
                if (guildRename) p.ReadAscii(); // guild name

                byte academyMember = p.ReadByte();

                // Skip equipment inventory
                byte invCount = p.ReadByte();
                for (byte j = 0; j < invCount; j++)
                {
                    p.ReadUInt(); // refItemID
                    p.ReadByte(); // plus
                }

                // Skip avatar inventory
                byte avatarCount = p.ReadByte();
                for (byte j = 0; j < avatarCount; j++)
                {
                    p.ReadUInt(); // refItemID
                    p.ReadByte(); // plus
                }

                if (name.Equals(_config.CharacterName, StringComparison.OrdinalIgnoreCase))
                    targetName = name;
            }

            if (targetName != null)
                SelectCharacter(targetName);
            else
                Stop(); // char not found
        }
        private void SelectCharacter(string name)
        {
            // 0x7007 = CLIENT_CHARACTER_SELECT
            var p = new Packet(0x7007);
            p.WriteAscii(name);
            _agent!.Send(p);
        }
        private async Task HandleCharData(Packet p)
        {
            try
            {
                CharDataReturn? data = await PacketParser.ParseCharData(p);

                CharState.PlayerStats = new PlayerStats
                {
                    CurrentHP = data!.HP,
                    CurrentMP = data.MP,
                    ZerkLevel = data.RemainingZerkBubbles,
                    CurrentLevel = data.CurrentLevel,
                    UnusedStatPoints = data.RemainingStatPoint,
                    RemainingGold = data.RemainingGold,
                    RemainingSkillPoints = data.RemainingSkillPoint,
                };

                if (Overseer.ExpTableCumulative.TryGetValue((byte)(data.CurrentLevel - 1), out ulong baseExp))
                    CharState.CumulativeExp = baseExp + data.ExpOffset;
                else
                    CharState.CumulativeExp = data.ExpOffset; // level 1, no base

                CharState.RegionId = data.RegionId;
                CharState.WorldX = (int)data.WorldX;
                CharState.WorldY = (int)data.WorldY;
                CharState.Z = (short)data.WorldZ;    // No conversion
                CharState.RawX = (short)data.RawX;
                CharState.RawY = (short)data.RawY;
                CharState.SectorX = data.SectorX;
                CharState.SectorY = data.SectorX;

                CharState.Inventory.Slots.Clear();
                CharState.Inventory.Equipment.Clear();
                CharState.Inventory.Avatars.Clear();

                CharState.Inventory.Slots = data.Slots;
                CharState.Inventory.Equipment = data.Equipment;
                CharState.Inventory.Avatars = data.Avatars;

                CharState.RegionName = RegionResolver.ResolveReadable(CharState.SectorX, CharState.SectorY, (short)CharState.RegionId);


                // Not finished yet
                _state = ClientlessBotState.Botting;
                //_ = Task.Run(() => BotLoop(_cts.Token));
            }
            catch (Exception ex)
            {
                Logger.Error($"Bot:HandleCharData:{CharState?.CharacterName}", ex.Message);
            }


        }
        private async Task HandleCharID(Packet p)
        {
            try
            {
                uint charUID = p.ReadUInt();
                uint charID = p.ReadUInt();

                if (CharState != null)
                {
                    CharState.CharacterUID = charUID;
                    CharState.CharacterID = charID;
                    Logger.Debug("CharID", $"CharacterUID=0x{charUID:X} CharacterID=0x{charID:X}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Bot:HandleCharData:{CharState?.CharacterName}", ex.Message);
            }
        }
        #endregion

        #region - Botting -

        public async Task BotLoop(CancellationToken token)
        {
            try
            {
                var walker = new AutoWalker(
                    getPosition: () => BotPosition.FromSession(CharState),
                    sendMove: pos => SendMove(pos),
                    sendPacket: p =>
                    {
                        try
                        {
                            _agent!.Send(p);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn("Bot", $"sendPacket failed: {ex.Message}");
                        }
                    },
                    CharState
                );

                var destination = BotPosition.FromSector(84, 104, 800, 600);
                await walker.WalkTo(destination, token, CharState);

                // attack loop next
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.Error("ClientlessBot::BotLoop", ex.Message);
            }
        }

        private void SendMove(BotPosition pos)
        {
            var p = new Packet(0x7021);
            p.WriteByte(1);                      // movement type = destination
            p.WriteUShort((ushort)pos.RegionId);
            bool isDungeon = (pos.RegionId & 0x8000) != 0;
            if (isDungeon)
            {
                p.WriteInt((int)pos.XOffset);
                p.WriteInt((int)pos.ZOffset);
                p.WriteInt((int)pos.YOffset);
            }
            else
            {
                p.WriteShort((short)pos.XOffset);
                p.WriteShort((short)pos.ZOffset);
                p.WriteShort((short)pos.YOffset);
            }
            _agent!.Send(p);
        }

        #endregion

    }

    public enum ClientlessBotState
    {
        Disconnected,
        ConnectingGateway,
        LoggingIn,
        SelectingServer,
        SelectingCharacter,
        LoadingCharData,
        Botting
    }
}