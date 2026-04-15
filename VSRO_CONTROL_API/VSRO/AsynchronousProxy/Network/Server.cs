using CoreLib.Tools.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Framework;

namespace VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network
{
    public class Server
    {
        #region Private Members

        /// <summary>
        /// Proxy was stopped
        /// </summary>
        private bool m_Stopped = false;
        /// <summary>
        /// The server socket handling all incoming connections
        /// </summary>
        private Socket m_Server;
        /// <summary>
        /// Remote IP to where all traffic is going to be proxy
        /// </summary>
        private string m_ServerIP;
        /// <summary>
        /// Remote Port to where all traffic is going to be proxy
        /// </summary>
        private int m_ServerPort;
        /// <summary>
        /// Group packet handlers by opcode
        /// </summary>
        private Dictionary<ushort, List<PacketTransferEventHandler>>
            m_ClientPacketEventHandlers = new Dictionary<ushort, List<PacketTransferEventHandler>>(),
            m_ServerPacketEventHandlers = new Dictionary<ushort, List<PacketTransferEventHandler>>();
        
        #endregion

        #region Public Properties
        /// <summary>
        /// Opcodes handled automatically by proxy
        /// </summary>
        public static class Opcode
        {
            public const ushort
                GLOBAL_HANDSHAKE = 0x5000,
                GLOBAL_HANDSHAKE_OK = 0x9000,
                GLOBAL_IDENTIFICATION = 0x2001;
        }
        public int Port { get; private set; }
        /// <summary>
        /// All connections currently handled by server
        /// </summary>
        public ConcurrentDictionary<int, Proxy> Connections { get; } = new ConcurrentDictionary<int, Proxy>();
        private static int _connectionSeed = 0;

        #endregion

        #region Constructor
        public Server()
        {
            // Proxy Stuffs
            var ignoreIt = new PacketTransferEventHandler((s, e) => 
            {
                e.CancelTransfer = true;
            });

            RegisterClientPacketHandler(Opcode.GLOBAL_HANDSHAKE, ignoreIt);
            RegisterClientPacketHandler(Opcode.GLOBAL_HANDSHAKE_OK, ignoreIt);
            RegisterClientPacketHandler(Opcode.GLOBAL_IDENTIFICATION, ignoreIt); // Handled by API

            RegisterServerPacketHandler(Opcode.GLOBAL_HANDSHAKE, ignoreIt);
            RegisterServerPacketHandler(Opcode.GLOBAL_HANDSHAKE_OK, ignoreIt);

            // When the player completes the handshake (0x9000), mark it done and flush any
            // game-server packets that arrived before the handshake finished (external/high-latency clients).
            RegisterClientPacketHandler(Opcode.GLOBAL_HANDSHAKE_OK, (s, e) => {
                if (!e.Proxy.ClientHandshakeDone)
                {
                    e.Proxy.ClientHandshakeDone = true;
                    while (e.Proxy.DeferredClientPackets.TryDequeue(out var deferred))
                        e.Proxy.Client.Send(deferred);
                }
            });
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Set remote address to proxy all incoming connections
        /// </summary>
        /// <param name="IP">IP where this connection will be redirected</param>
        /// <param name="Port">Port where this connection will be redirected</param>
        public void SetProxy(string IP, int Port)
        {
            // Proxy redirecting stuff
            m_ServerIP = IP;
            m_ServerPort = Port;
        }
        /// <summary>
        /// Start listening all client connections to redirect all traffic asynchronized
        /// </summary>
        public void Start(int Port)
        {
            m_Stopped = false;
            // Create the TCP/IP socket (IPV4)
            m_Server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections

            // Establish the endpoint for the server to listen
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, Port);

            // Bind the IP
            m_Server.Bind(localEndPoint);
            // Listening queue
            m_Server.Listen(50);

            int asyncConnectionQueueMax = 5;
            // Start an asynchronous socket to listen for connections
            for (int i = 0; i < asyncConnectionQueueMax; i++)
                BeginAcceptCallback();

            // Track port being use
            this.Port = ((IPEndPoint)m_Server.LocalEndPoint).Port;
        }
        /// <summary>
        /// Stop the server dropping all the connections.
        /// </summary>
        public void Stop()
        {
            m_Stopped = true;
            foreach (var connection in Connections)
                connection.Value.Client.Close();
            m_Server?.Close();
        }

        #endregion

        #region Private Helpers
        /// <summary>
        /// Accepting asynchronized...
        /// </summary>
        private void AcceptCallback(IAsyncResult AsyncResult)
        {
            if (m_Stopped) return;

            // Create the proxy relation...
            var proxy = new Proxy();

            // Try to accept the connection
            try
            {
                // Accept - Client to Proxy
                proxy.Client = new Client(m_Server.EndAccept(AsyncResult));
                Logger.Info(this, $"Server: Connection established with {proxy.Client.Socket.RemoteEndPoint}");
                // Create - Server to Proxy
                proxy.Server = new Client();
            }
            catch (Exception ex)
            {
                Logger.Info(this, $"Server: Connection error ({ex.Message})");

                // Handle next connection
                BeginAcceptCallback();
                return;
            }

            // Proxy setup
            proxy.Client.Security.GenerateSecurity(true, true, true);

            // Proxy-ing packets Client <-> Proxy <-> Server
            // From Client to Proxy
            proxy.Client.OnPacketReceived += (s, e) => {
                // call event handlers
                //Logger.Debug("OnClientReceived", $"Opcode[{e.Packet.Opcode.ToString("X2")}]");
                _OnPacketTransfer(e.Packet, m_ClientPacketEventHandlers, proxy.Server, proxy);
            };
            // From Server to Proxy
            proxy.Server.OnPacketReceived += (s, e) => {
                // call event handlers
                //Logger.Debug("OnServerReceived", $"Opcode[{e.Packet.Opcode.ToString("X2")}]");
                _OnPacketTransfer(e.Packet, m_ServerPacketEventHandlers, proxy.Client, proxy);
            };

            // Set network events
            proxy.Server.OnConnect += (s, e) => {
                // call event
                _OnProxyConnected(proxy);
            };
            proxy.Server.OnDisconnect += (s, e) => {
                // Try to shutdown client...
                if (proxy.Client.IsConnected)
                    proxy.Client.Close();
                else
                    _OnProxyDisconnected(proxy);
            };
            proxy.Client.OnDisconnect += (s, e) => {
                // Try to shutdown server...
                if (proxy.Server.IsConnected)
                    proxy.Server.Close();
                else
                    _OnProxyDisconnected(proxy);
            };

            // Create local to gameserver connection
            _OnProxyConnecting(proxy, m_ServerIP, m_ServerPort);

            // Handle next connection
            BeginAcceptCallback();
        }
        /// <summary>
        /// Continue accepting a new client connection
        /// </summary>
        private void BeginAcceptCallback()
        {
            if (m_Stopped) return;
            // Keep accepting a new connection, this has been handled
            try
            {
                m_Server.BeginAccept(AcceptCallback, null);
            }
            catch (ObjectDisposedException)
            {
                // Server was shut down, silently ignore
            }
            catch (Exception ex)
            {
                Logger.Error(this, ex.ToString());
            }
        }
        #endregion

        #region Events

        #region Packet Handlers
        /// <summary>
        /// Creates a handler method for the proxy packet
        /// </summary>
        public delegate void PacketTransferEventHandler(object sender, PacketTransferEventArgs e);
        public class PacketTransferEventArgs : EventArgs
        {
            public Packet Packet { get; }
            public bool CancelTransfer { get; set; }
            public Proxy Proxy { get; }
            internal PacketTransferEventArgs(Packet Packet, Proxy Proxy)
            {
                this.Packet = Packet;
                this.Proxy = Proxy;
            }
        }
        private void _OnPacketTransfer(Packet Packet, Dictionary<ushort, List<PacketTransferEventHandler>> Handlers, Client Destination, Proxy Proxy)
        {
            // Execute every packet handler registered
            if (Handlers.TryGetValue(Packet.Opcode, out List<PacketTransferEventHandler> handlers))
            {
                // Call event handlers
                PacketTransferEventArgs evt = new PacketTransferEventArgs(Packet, Proxy);

                for (int i = 0; i < handlers.Count; i++)
                    handlers[i].Invoke(this, evt);

                // Check if the packet is going to be proxied
                if (evt.CancelTransfer)
                    return;
            }

            // On high-latency (external) connections the game server can push application packets
            // (0x2001, 0x2005, 0x6005 …) before the player has finished the security handshake.
            // Queueing them in Security before m_accepted_handshake is set causes
            // HasPacketToSend() to block the handshake-response 0x5000 permanently, so the
            // player never receives it, times out, and disconnects.  Buffer those packets here
            // and flush them once the player sends 0x9000 (see GLOBAL_HANDSHAKE_OK handler).
            if (Destination == Proxy.Client && !Proxy.ClientHandshakeDone)
            {
                Proxy.DeferredClientPackets.Enqueue(Packet);
                return;
            }

            // Proxy packet
            Destination.Send(Packet);
        }
        /// <summary>
        /// Add handler by opcode using the object context
        /// </summary>
        private void RegisterHandler(ref Dictionary<ushort, List<PacketTransferEventHandler>> Context, ushort Opcode, PacketTransferEventHandler Handler)
        {
            // Check if the opcode has some subscriber
            if (!Context.TryGetValue(Opcode, out List<PacketTransferEventHandler> handlers))
            {
                // Create the subscribers list for this opcode
                handlers = new List<PacketTransferEventHandler>();
                Context[Opcode] = handlers;
            }
            // Add the new handler
            handlers.Add(Handler);
        }
        /// <summary>
        /// Add client (remote) handler by opcode
        /// </summary>
        public void RegisterClientPacketHandler(ushort Opcode, PacketTransferEventHandler Handler)
        {
            RegisterHandler(ref m_ClientPacketEventHandlers, Opcode, Handler);
        }
        /// <summary>
        /// Add server (local) handler by opcode
        /// </summary>
        public void RegisterServerPacketHandler(ushort Opcode, PacketTransferEventHandler Handler)
        {
            RegisterHandler(ref m_ServerPacketEventHandlers, Opcode, Handler);
        }
        /// <summary>
        /// Remove packet handler from opcode using context object. If is not specified all handlers from opcode will be removed
        /// </summary>
        private void UnregisterHandler(ref Dictionary<ushort, List<PacketTransferEventHandler>> Context, ushort Opcode, PacketTransferEventHandler Handler = null)
        {
            // Remove all
            if (Handler == null)
            {
                if (Context.ContainsKey(Opcode))
                    Context.Remove(Opcode);
            }
            // Remove specific handler
            else
            {
                // Check if the opcode has handlers
                if (Context.TryGetValue(Opcode, out List<PacketTransferEventHandler> handlers))
                {
                    for (int i = 0; i < handlers.Count; i++)
                    {
                        // Remove handler
                        if (Handler == handlers[i])
                            handlers.RemoveAt(i--);
                    }
                    // Clean opcode event
                    if (handlers.Count == 0)
                        Context.Remove(Opcode);
                }
            }
        }
        /// <summary>
        /// Remove client (remote) packet handler from opcode.
        /// If is not specified all handlers from opcode will be removed
        /// </summary>
        public void UnregisterClientPacketHandler(ushort Opcode, PacketTransferEventHandler Handler = null)
        {
            UnregisterHandler(ref m_ClientPacketEventHandlers, Opcode, Handler);
        }
        /// <summary>
        /// Remove server (local) packet handler from opcode.
        /// If is not specified all handlers from opcode will be removed
        /// </summary>
        public void UnregisterServerPacketHandler(ushort Opcode, PacketTransferEventHandler Handler = null)
        {
            UnregisterHandler(ref m_ServerPacketEventHandlers, Opcode, Handler);
        }
        #endregion

        /// <summary>
        /// Called when the client connection is established and the proxy tunnel is going to be created
        /// </summary>
        public event ProxyConnectingEventHandler OnProxyConnection;
        public delegate void ProxyConnectingEventHandler(object sender, ProxyConnectingEventArgs e);
        public class ProxyConnectingEventArgs : EventArgs
        {
            /// <summary>
            /// Proxy connection involved
            /// </summary>
            public Proxy Proxy { get; }
            /// <summary>
            /// IP to redirect this proxy connection
            /// </summary>
            public string IP { get; set; }
            /// <summary>
            /// Port to redirect this proxy connection
            /// </summary>
            public int Port { get; set; }
            public ProxyConnectingEventArgs(Proxy Proxy, string IP, int Port)
            {
                this.Proxy = Proxy;
                this.IP = IP;
                this.Port = Port;
            }
        }
        private void _OnProxyConnecting(Proxy Proxy, string IP, int Port)
        {
            // Check if the connection is going to be as default
            var e = new ProxyConnectingEventArgs(Proxy, IP, Port);
            OnProxyConnection?.Invoke(this, e);

            // Try to create connection to gameserver specified
            Proxy.Server.BeginConnect(e.IP, e.Port);
        }
        /// <summary>
        /// Called when a proxy connection is successfully established
        /// </summary>
        public event ProxyConnectedEventHandler OnProxyConnected;
        public delegate void ProxyConnectedEventHandler(object sender, ProxyConnectedEventArgs e);
        public class ProxyConnectedEventArgs : EventArgs
        {
            /// <summary>
            /// Proxy connection involved
            /// </summary>
            public Proxy Proxy { get; }
            public ProxyConnectedEventArgs(Proxy Proxy)
            {
                this.Proxy = Proxy;
            }
        }
        private void _OnProxyConnected(Proxy Proxy)
        {
            // Add connection as successfully handled
            Proxy.ConnectionId = Interlocked.Increment(ref _connectionSeed);

            Connections.TryAdd(Proxy.ConnectionId, Proxy);
            // Start client handshake after connecting to server
            Proxy.Client.BeginSend();
            Proxy.Client.BeginReceive();

            OnProxyConnected?.Invoke(this, new ProxyConnectedEventArgs(Proxy));
        }
        /// <summary>
        /// Called when a proxy connection is lost
        /// </summary>
        public event ProxyDisconnectedEventHandler OnProxyDisconnected;
        public delegate void ProxyDisconnectedEventHandler(object sender, ProxyDisconnectedEventArgs e);
        public class ProxyDisconnectedEventArgs : EventArgs
        {
            /// <summary>
            /// Proxy connection involved
            /// </summary>
            public Proxy Proxy { get; }
            public ProxyDisconnectedEventArgs(Proxy Proxy)
            {
                this.Proxy = Proxy;
            }
        }
        private void _OnProxyDisconnected(Proxy Proxy)
        {
            // Remove from connections handled
            Connections.TryRemove(Proxy.ConnectionId, out _);

            OnProxyDisconnected?.Invoke(this, new ProxyDisconnectedEventArgs(Proxy));
        }
        #endregion
    }
}
