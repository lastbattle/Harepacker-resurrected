using MapleLib.PacketLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>A decrypted server packet queued for game-thread application.</summary>
    public sealed record MapleOnlineInboundPacket(
        MapleServerRole Role,
        long Generation,
        int Opcode,
        byte[] RawPacket,
        string RemoteEndpoint,
        DateTime ReceivedUtc);

    /// <summary>
    /// A stage-owned packet route. The owner fans every inbound packet of the
    /// handler's role out to registered handlers; each handler keeps its own
    /// opcode filtering and state ownership.
    /// </summary>
    public interface IMapleOnlineStageHandler
    {
        MapleServerRole Role { get; }

        /// <summary>Called on the game thread while draining the owner queue.</summary>
        void HandleInboundPacket(MapleOnlineInboundPacket packet);

        bool TrySendOutboundPacket(byte[] payload, out string status);
    }

    /// <summary>
    /// Client-owned online session authority. Owns one active direct session
    /// per role, implements the native single-socket migration contract
    /// (N04: close the active socket, then dial the next endpoint), and is the
    /// single ingress path for direct-session packets. Inbound packets are
    /// queued here and must be drained on the game thread via
    /// <see cref="DrainPendingInbound"/> so state application keeps its
    /// game-thread affinity.
    /// </summary>
    public sealed class MapleOnlineDirectSessionOwner : IDisposable
    {
        private const int TraceCapacity = 128;
        private static readonly TimeSpan DefaultHandshakeTimeout = TimeSpan.FromSeconds(10);
        public const ushort HwidPacketOpcode = 0x001A;

        private readonly object _sync = new();
        private readonly Dictionary<MapleServerRole, MapleClientDirectSession> _sessions = new();
        private readonly List<IMapleOnlineStageHandler> _stageHandlers = new();
        private readonly ConcurrentQueue<MapleOnlineInboundPacket> _pendingInbound = new();
        private readonly Queue<string> _trace = new();
        private bool _disposed;

        /// <summary>
        /// Hardware-ID blob sent as the native post-connect packet (N05:
        /// opcode 0x1A + u16 length + blob). The native client reads this from
        /// a local file and also emits an empty blob when the file is absent;
        /// a controlled server defines the accepted content.
        /// </summary>
        public byte[] HwidBlob { get; set; } = Array.Empty<byte>();

        public event EventHandler<string> TraceRecorded;

        public MapleOnlineDirectSessionOwner()
        {
            RecordTrace("Online session owner initialized (offline authority not active).");
        }

        public bool IsRoleConnected(MapleServerRole role)
        {
            lock (_sync)
                return !_disposed && _sessions.TryGetValue(role, out MapleClientDirectSession session) && session.IsConnected;
        }

        public long? CurrentGeneration(MapleServerRole role)
        {
            lock (_sync)
                return !_disposed && _sessions.TryGetValue(role, out MapleClientDirectSession session)
                    ? session.Generation
                    : null;
        }

        public string DescribeStatus()
        {
            lock (_sync)
            {
                if (_disposed)
                    return "Online session owner disposed.";
                if (_sessions.Count == 0)
                    return "Online session owner idle (no active role sessions).";
                string roles = string.Join(
                    ", ",
                    _sessions.OrderBy(entry => entry.Key).Select(entry =>
                        $"{entry.Key}: {(entry.Value.IsConnected ? "connected" : "connecting/retired")} gen={entry.Value.Generation} -> {entry.Value.RemoteEndpoint}"));
                return $"Online session owner roles [{roles}]; pending inbound={_pendingInbound.Count}.";
            }
        }

        public string DescribeTrace()
        {
            lock (_sync)
                return _trace.Count == 0 ? "none" : string.Join(Environment.NewLine, _trace);
        }

        public IReadOnlyList<string> SnapshotTrace()
        {
            lock (_sync)
                return _trace.ToList();
        }

        public void RegisterStageHandler(IMapleOnlineStageHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            lock (_sync)
            {
                if (_stageHandlers.Any(existing => ReferenceEquals(existing, handler)))
                    return;
                _stageHandlers.Add(handler);
            }

            RecordTrace($"Registered {handler.Role} stage handler.");
        }

        /// <summary>
        /// Connects the role's direct session. Any existing session for the
        /// role is retired first by the session itself (native IssueConnect
        /// close/connect contract).
        /// </summary>
        public async Task ConnectAsync(
            MapleServerRole role,
            string host,
            int port,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            MapleClientDirectSession session = GetOrCreateSession(role);
            await session.ConnectAsync(host, port, cancellationToken, DefaultHandshakeTimeout).ConfigureAwait(false);
            RecordTrace($"{role} connected to {host}:{port} generation {session.Generation}.");
            SendPostConnectHandshake(role, session);
        }

        /// <summary>
        /// Native migration contract (N04): the client owns a single socket, so
        /// migration retires every active role session before dialing the
        /// target endpoint. Returns the new generation for the target role.
        /// </summary>
        public async Task<long> MigrateAsync(
            MapleServerRole targetRole,
            IPAddress address,
            ushort port,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(address);

            List<MapleClientDirectSession> retired;
            lock (_sync)
            {
                ThrowIfDisposed();
                retired = _sessions.Values.ToList();
                _sessions.Clear();
            }

            foreach (MapleClientDirectSession session in retired)
            {
                session.PacketReceived -= OnSessionPacketReceived;
                session.Disconnected -= OnSessionDisconnected;
                session.Close($"Migrated to {targetRole} at {address}:{port}.");
                session.Dispose();
            }

            if (retired.Count > 0)
                RecordTrace($"Migration: retired {retired.Count} session(s) before dialing {targetRole} {address}:{port}.");

            MapleClientDirectSession target = GetOrCreateSession(targetRole);
            await target.ConnectAsync(address.ToString(), port, cancellationToken, DefaultHandshakeTimeout).ConfigureAwait(false);
            RecordTrace($"Migration complete: {targetRole} generation {target.Generation} at {address}:{port}.");
            SendPostConnectHandshake(targetRole, target);
            return target.Generation ?? 0;
        }

        public void CloseRole(MapleServerRole role, string reason = null)
        {
            MapleClientDirectSession session;
            lock (_sync)
            {
                if (!_sessions.TryGetValue(role, out session))
                    return;
                _sessions.Remove(role);
            }

            session.PacketReceived -= OnSessionPacketReceived;
            session.Disconnected -= OnSessionDisconnected;
            session.Close(reason ?? $"Closed {role} role session.");
            session.Dispose();
            RecordTrace($"{role} role session closed: {reason ?? "by owner"}.");
        }

        public bool TrySendPacket(MapleServerRole role, byte[] payload, out string status)
        {
            ArgumentNullException.ThrowIfNull(payload);
            MapleClientDirectSession session;
            lock (_sync)
            {
                if (_disposed)
                {
                    status = "Online session owner is disposed.";
                    return false;
                }
                if (!_sessions.TryGetValue(role, out session) || !session.IsConnected)
                {
                    status = $"No active {role} direct session for outbound delivery.";
                    return false;
                }
            }

            // The captured session validates its live handshake state again and
            // closes itself on send failure.
            return session.TrySendPacket(payload, out status);
        }

        /// <summary>
        /// Game-thread drain. Applies each queued packet to the registered
        /// stage handlers of its role, in arrival order.
        /// </summary>
        public int DrainPendingInbound()
        {
            IMapleOnlineStageHandler[] handlers;
            lock (_sync)
                handlers = _stageHandlers.ToArray();

            int applied = 0;
            while (_pendingInbound.TryDequeue(out MapleOnlineInboundPacket packet))
            {
                bool delivered = false;
                foreach (IMapleOnlineStageHandler handler in handlers)
                {
                    if (handler.Role != packet.Role)
                        continue;
                    handler.HandleInboundPacket(packet);
                    delivered = true;
                }

                if (delivered)
                    applied++;
                else
                    RecordTrace($"Dropped {packet.Role} opcode {packet.Opcode} generation {packet.Generation}: no stage handler registered.");
            }

            return applied;
        }

        public void Dispose()
        {
            List<MapleClientDirectSession> sessions;
            lock (_sync)
            {
                if (_disposed)
                    return;
                _disposed = true;
                sessions = _sessions.Values.ToList();
                _sessions.Clear();
            }

            foreach (MapleClientDirectSession session in sessions)
            {
                session.PacketReceived -= OnSessionPacketReceived;
                session.Disconnected -= OnSessionDisconnected;
                session.Close("Owner disposed.");
                session.Dispose();
            }

            RecordTrace("Online session owner disposed.");
        }

        private MapleClientDirectSession GetOrCreateSession(MapleServerRole role)
        {
            lock (_sync)
            {
                ThrowIfDisposed();
                if (_sessions.TryGetValue(role, out MapleClientDirectSession existing))
                    return existing;

                var session = new MapleClientDirectSession(role, MapleHandshakePolicy.GlobalV95);
                session.PacketReceived += OnSessionPacketReceived;
                session.Disconnected += OnSessionDisconnected;
                _sessions[role] = session;
                return session;
            }
        }

        /// <summary>
        /// Native OnConnect contract (N05): every successful connection emits
        /// opcode 0x1A with [u16 length][hwid blob]; an empty blob is a valid
        /// native case when the local hwid file is absent.
        /// </summary>
        private void SendPostConnectHandshake(MapleServerRole role, MapleClientDirectSession session)
        {
            byte[] blob = HwidBlob ?? Array.Empty<byte>();
            var packet = new byte[4 + blob.Length];
            packet[0] = (byte)(HwidPacketOpcode & 0xFF);
            packet[1] = (byte)(HwidPacketOpcode >> 8);
            packet[2] = (byte)(blob.Length & 0xFF);
            packet[3] = (byte)(blob.Length >> 8);
            if (blob.Length > 0)
                Buffer.BlockCopy(blob, 0, packet, 4, blob.Length);

            if (session.TrySendPacket(packet, out string status))
                RecordTrace($"{role} generation {session.Generation} sent post-connect hwid packet (opcode {HwidPacketOpcode}, {blob.Length}-byte blob).");
            else
                RecordTrace($"{role} generation {session.Generation} post-connect hwid send failed: {status}");
        }

        private void OnSessionPacketReceived(object sender, MapleDirectSessionPacketEventArgs e)
        {
            // The session already rejects stale generations; this guard keeps a
            // replaced session instance from enqueueing after the swap.
            lock (_sync)
            {
                if (_disposed || !_sessions.TryGetValue(e.Role, out MapleClientDirectSession current) || !ReferenceEquals(current, sender))
                    return;
            }

            _pendingInbound.Enqueue(
                new MapleOnlineInboundPacket(
                    e.Role,
                    e.Generation,
                    e.Opcode,
                    e.RawPacket,
                    e.RemoteEndpoint,
                    DateTime.UtcNow));
        }

        private void OnSessionDisconnected(object sender, MapleDirectSessionDisconnectedEventArgs e)
        {
            bool removed = false;
            lock (_sync)
            {
                if (_sessions.TryGetValue(e.Role, out MapleClientDirectSession current) && ReferenceEquals(current, sender))
                {
                    _sessions.Remove(e.Role);
                    removed = true;
                }
            }

            if (removed)
                RecordTrace($"{e.Role} generation {e.Generation} disconnected ({(e.Expected ? "expected" : "unexpected")}): {e.Reason}");
        }

        private void RecordTrace(string message)
        {
            string entry = $"[{DateTime.UtcNow:O}] {message}";
            lock (_sync)
            {
                _trace.Enqueue(entry);
                while (_trace.Count > TraceCapacity)
                    _trace.Dequeue();
            }

            TraceRecorded?.Invoke(this, entry);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MapleOnlineDirectSessionOwner));
        }
    }
}
