using System.Collections.Concurrent;
using VSRO_CONTROL_API.Settings;
using VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.Tools;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Framework;

namespace VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network
{
    public class Proxy
    {
        #region Public Properties
        /// <summary>
        /// The local connection to outside world (Client to Proxy)
        /// </summary>
        public Client Server { get; set; }
        /// <summary>
        /// The remote connection to the game server (Proxy to Server)
        /// </summary>
        public Client Client { get; set; }
        /// <summary>
        /// Set to true once the player has completed the 0x9000 security handshake with the proxy.
        /// Game-server packets must not reach the player before this, or the Security
        /// outgoing queue gets an application packet in front of the handshake-response 0x5000,
        /// causing HasPacketToSend() to block it permanently on high-latency (external) connections.
        /// </summary>
        public bool ClientHandshakeDone { get; set; } = false;
        /// <summary>
        /// Game-server packets buffered while the player handshake is still in progress.
        /// Flushed to the player as soon as ClientHandshakeDone is set.
        /// </summary>
        public ConcurrentQueue<Packet> DeferredClientPackets { get; } = new ConcurrentQueue<Packet>();

        #endregion

        #region - Connection Info -

        public int ConnectionId { get; set; }
        public PlayerSession? Session;
        public InventoryTracker Inventory { get; } = new InventoryTracker();
        public ConcurrentDictionary<uint, uint> SpawnCache { get; } = new(); 
        public Dictionary<byte, TaskCompletionSource<bool>> PendingMoves = new();
        public event Action<Proxy, PlayerSession>? OnPlaytimeHourReached;
        public ConcurrentDictionary<uint, (uint RefObjID, short RegionID)> SpawnedObjects { get; set; } = new();
        public uint LastTargetUID { get; set; }
        public CancellationTokenSource? SessionTokenSource;
        public bool IsSorting { get; set; } = false;
        public CancellationTokenSource? ActiveSortCts { get; set; }
        public byte CurrentGroupSpawnType { get; set; } = 0;  // 1=spawn, 2=despawn
        internal void CheckPlaytimeReward(PlayerSession session)
        {
            if (SettingsLoader.Settings != null && SettingsLoader.Settings.Proxy?.SilkPerXHours > 0)
            {
                var minutes = (int)session.AccumulatedPlayTime.TotalMinutes;
                var rewardIntervals = minutes / SettingsLoader.Settings.Proxy.SilkPerXHours;

                if (rewardIntervals > session.RewardedHours)
                {
                    session.RewardedHours = rewardIntervals;
                    OnPlaytimeHourReached?.Invoke(this, session);
                }
            }
        }
        #endregion


        #region - Constructor -
        /// <summary>
        /// Default constructor
        /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public Proxy()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {

        }
        #endregion
    }
}
