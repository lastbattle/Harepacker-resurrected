using HaCreator.MapSimulator.Fields;
using MapleLib.MapleCryptoLib;
using MapleLib.PacketLib;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Proxies a live Maple session for CRPSGameDlg packet ownership.
    /// </summary>
    public sealed class RockPaperScissorsOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18493;

        private readonly ConcurrentQueue<RockPaperScissorsPacketInboxMessage> _pendingMessages = new();
        private readonly ConcurrentQueue<RockPaperScissorsClientPacket> _pendingClientPackets = new();
        private readonly object _sync = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;
        private BridgePair _activePair;

        private sealed class BridgePair
        {
            public BridgePair(TcpClient clientTcpClient, TcpClient serverTcpClient, Session clientSession, Session serverSession)
            {
                ClientTcpClient = clientTcpClient;
                ServerTcpClient = serverTcpClient;
                ClientSession = clientSession;
                ServerSession = serverSession;
                RemoteEndpoint = serverTcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown-remote";
                ClientEndpoint = clientTcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown-client";
            }

            public TcpClient ClientTcpClient { get; }
            public TcpClient ServerTcpClient { get; }
            public Session ClientSession { get; }
            public Session ServerSession { get; }
            public string RemoteEndpoint { get; }
            public string ClientEndpoint { get; }
            public short Version { get; set; }
            public bool InitCompleted { get; set; }

            public void Close()
            {
                try
                {
                    ClientTcpClient.Close();
                }
                catch
                {
                }

                try
                {
                    ServerTcpClient.Close();
                }
                catch
                {
                }
            }
        }

        public int ListenPort { get; private set; } = DefaultListenPort;
        public string RemoteHost { get; private set; } = IPAddress.Loopback.ToString();
        public int RemotePort { get; private set; }
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public bool HasConnectedSession => _activePair?.InitCompleted == true;
        public int ReceivedCount { get; private set; }
        public int SentCount { get; private set; }
        public int QueuedCount { get; private set; }
        public int PendingPacketCount => _pendingClientPackets.Count;
        public string LastStatus { get; private set; } = "Rock-Paper-Scissors official-session bridge inactive.";

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}"
                : "inactive";
            string session = HasConnectedSession
                ? $"connected session {_activePair?.ClientEndpoint ?? "unknown-client"} -> {_activePair?.RemoteEndpoint ?? "unknown-remote"}"
                : "no active Maple session";
            return $"Rock-Paper-Scissors official-session bridge {lifecycle}; {session}; received={ReceivedCount}; sent={SentCount}; pending={PendingPacketCount}; queued={QueuedCount}. {LastStatus}";
        }

        public bool Start(int listenPort, string remoteHost, int remotePort, out string status)
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);

                try
                {
                    ListenPort = listenPort <= 0 ? DefaultListenPort : listenPort;
                    RemoteHost = string.IsNullOrWhiteSpace(remoteHost) ? IPAddress.Loopback.ToString() : remoteHost.Trim();
                    RemotePort = remotePort;
                    _listenerCancellation = new CancellationTokenSource();
                    _listener = new TcpListener(IPAddress.Loopback, ListenPort);
                    _listener.Start();
                    _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                    LastStatus = $"Rock-Paper-Scissors official-session bridge listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                    status = LastStatus;
                    return true;
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Rock-Paper-Scissors official-session bridge failed to start: {ex.Message}";
                    status = LastStatus;
                    return false;
                }
            }
        }

        public void Stop()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
                LastStatus = "Rock-Paper-Scissors official-session bridge stopped.";
            }
        }

        public bool TryDequeue(out RockPaperScissorsPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public bool TrySendOrQueueClientPacket(RockPaperScissorsClientPacket packet, out bool queued, out string status)
        {
            queued = false;
            if (packet == null)
            {
                status = "Rock-Paper-Scissors official-session bridge requires a client packet.";
                LastStatus = status;
                return false;
            }

            if (HasConnectedSession)
            {
                return TrySendClientPacket(packet, out status);
            }

            if (!IsRunning)
            {
                status = "Rock-Paper-Scissors official-session bridge is not running.";
                LastStatus = status;
                return false;
            }

            _pendingClientPackets.Enqueue(packet);
            QueuedCount++;
            queued = true;
            status = $"Queued Rock-Paper-Scissors opcode {RockPaperScissorsField.ClientOpcode} subtype {(int)packet.RequestType} for deferred live-session injection.";
            LastStatus = status;
            return true;
        }

        public bool TrySendClientPacket(RockPaperScissorsClientPacket packet, out string status)
        {
            status = null;
            BridgePair pair = _activePair;
            if (pair == null || !pair.InitCompleted)
            {
                status = "Rock-Paper-Scissors official-session bridge has no initialized Maple session.";
                LastStatus = status;
                return false;
            }

            try
            {
                pair.ServerSession.SendPacket(RockPaperScissorsClientPacketTransportManager.BuildRawPacket(packet));
                SentCount++;
                status = $"Injected Rock-Paper-Scissors opcode {RockPaperScissorsField.ClientOpcode} subtype {(int)packet.RequestType} into live session {pair.RemoteEndpoint}.";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Rock-Paper-Scissors official-session bridge client-packet injection failed: {ex.Message}");
                status = LastStatus;
                return false;
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                StopInternal(clearPending: true);
            }
        }

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _listener != null)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                    _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                LastStatus = $"Rock-Paper-Scissors official-session bridge error: {ex.Message}";
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            BridgePair pair = null;

            try
            {
                lock (_sync)
                {
                    if (_activePair != null)
                    {
                        LastStatus = "Rejected Rock-Paper-Scissors official-session client because a live Maple session is already attached.";
                        client.Close();
                        return;
                    }
                }

                TcpClient server = new();
                await server.ConnectAsync(RemoteHost, RemotePort, cancellationToken).ConfigureAwait(false);

                Session clientSession = new(client.Client, SessionType.SERVER_TO_CLIENT);
                Session serverSession = new(server.Client, SessionType.CLIENT_TO_SERVER);
                pair = new BridgePair(client, server, clientSession, serverSession);

                clientSession.OnPacketReceived += (packet, isInit) => HandleClientPacket(pair, packet, isInit);
                clientSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Rock-Paper-Scissors official-session client disconnected: {pair.ClientEndpoint}.");
                serverSession.OnPacketReceived += (packet, isInit) => HandleServerPacket(pair, packet, isInit);
                serverSession.OnClientDisconnected += _ => ClearActivePair(pair, $"Rock-Paper-Scissors official-session server disconnected: {pair.RemoteEndpoint}.");

                lock (_sync)
                {
                    _activePair = pair;
                }

                LastStatus = $"Rock-Paper-Scissors official-session bridge connected {pair.ClientEndpoint} -> {pair.RemoteEndpoint}. Waiting for Maple init packet.";
                serverSession.WaitForDataNoEncryption();
            }
            catch (Exception ex)
            {
                client.Close();
                pair?.Close();
                LastStatus = $"Rock-Paper-Scissors official-session bridge connect failed: {ex.Message}";
            }
        }

        private void HandleServerPacket(BridgePair pair, PacketReader packet, bool isInit)
        {
            try
            {
                byte[] raw = packet.ToArray();
                if (isInit)
                {
                    PacketReader initReader = new(raw);
                    initReader.ReadShort();
                    pair.Version = initReader.ReadShort();
                    string patchLocation = initReader.ReadMapleString();
                    byte[] clientSendIv = initReader.ReadBytes(4);
                    byte[] clientReceiveIv = initReader.ReadBytes(4);
                    byte serverType = initReader.ReadByte();

                    pair.ClientSession.SIV = CreateCrypto(clientReceiveIv, pair.Version);
                    pair.ClientSession.RIV = CreateCrypto(clientSendIv, pair.Version);
                    pair.ClientSession.SendInitialPacket(pair.Version, patchLocation, clientSendIv, clientReceiveIv, serverType);
                    pair.InitCompleted = true;
                    int flushed = FlushQueuedClientPackets(pair);
                    LastStatus = flushed > 0
                        ? $"Rock-Paper-Scissors official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint} and flushed {flushed} queued client packet(s)."
                        : $"Rock-Paper-Scissors official-session bridge initialized Maple crypto for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint}.";
                    pair.ClientSession.WaitForData();
                    return;
                }

                pair.ClientSession.SendPacket((byte[])raw.Clone());

                if (!TryBuildInboundMessage(raw, $"official-session:{pair.RemoteEndpoint}", out RockPaperScissorsPacketInboxMessage message))
                {
                    return;
                }

                _pendingMessages.Enqueue(message);
                ReceivedCount++;
                LastStatus = $"Queued Rock-Paper-Scissors subtype {message.PacketType} from live session {pair.RemoteEndpoint}.";
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Rock-Paper-Scissors official-session server handling failed: {ex.Message}");
            }
        }

        private void HandleClientPacket(BridgePair pair, PacketReader packet, bool isInit)
        {
            try
            {
                if (isInit)
                {
                    return;
                }

                pair.ServerSession.SendPacket(packet.ToArray());
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"Rock-Paper-Scissors official-session client handling failed: {ex.Message}");
            }
        }

        private int FlushQueuedClientPackets(BridgePair pair)
        {
            if (pair == null || !pair.InitCompleted)
            {
                return 0;
            }

            int flushed = 0;
            while (_pendingClientPackets.TryDequeue(out RockPaperScissorsClientPacket packet))
            {
                pair.ServerSession.SendPacket(RockPaperScissorsClientPacketTransportManager.BuildRawPacket(packet));
                SentCount++;
                flushed++;
            }

            return flushed;
        }

        internal static bool TryBuildInboundMessage(byte[] rawPacket, string source, out RockPaperScissorsPacketInboxMessage message)
        {
            message = null;
            if (rawPacket == null || rawPacket.Length < sizeof(ushort) + sizeof(byte))
            {
                return false;
            }

            ushort opcode = BitConverter.ToUInt16(rawPacket, 0);
            if (opcode != RockPaperScissorsField.OwnerOpcode)
            {
                return false;
            }

            int packetType = rawPacket[sizeof(ushort)];
            if (!RockPaperScissorsField.TryParsePacketType(packetType.ToString(), out _))
            {
                return false;
            }

            byte[] payload = new byte[rawPacket.Length - sizeof(ushort) - sizeof(byte)];
            if (payload.Length > 0)
            {
                Buffer.BlockCopy(rawPacket, sizeof(ushort) + sizeof(byte), payload, 0, payload.Length);
            }

            message = new RockPaperScissorsPacketInboxMessage(
                packetType,
                payload,
                string.IsNullOrWhiteSpace(source) ? "official-session:unknown-remote" : source,
                $"packetraw {Convert.ToHexString(rawPacket)}");
            return true;
        }

        private void ClearActivePair(BridgePair pair, string status)
        {
            lock (_sync)
            {
                if (_activePair != pair)
                {
                    return;
                }

                _activePair = null;
                LastStatus = status;
            }

            pair?.Close();
        }

        private void StopInternal(bool clearPending)
        {
            try
            {
                _listenerCancellation?.Cancel();
            }
            catch
            {
            }

            try
            {
                _listener?.Stop();
            }
            catch
            {
            }

            try
            {
                _listenerTask?.Wait(100);
            }
            catch
            {
            }

            BridgePair pair;
            lock (_sync)
            {
                pair = _activePair;
                _activePair = null;
            }

            pair?.Close();
            _listenerTask = null;
            _listener = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;

            if (clearPending)
            {
                while (_pendingMessages.TryDequeue(out _))
                {
                }

                while (_pendingClientPackets.TryDequeue(out _))
                {
                }

                ReceivedCount = 0;
                SentCount = 0;
                QueuedCount = 0;
            }
        }

        private static MapleCrypto CreateCrypto(byte[] iv, short version)
        {
            return new MapleCrypto((byte[])iv.Clone(), version);
        }
    }
}
