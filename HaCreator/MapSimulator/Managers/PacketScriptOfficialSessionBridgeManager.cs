using HaCreator.MapSimulator.Interaction;
using MapleLib.PacketLib;
using System;
using System.Linq;

namespace HaCreator.MapSimulator.Managers
{
    internal sealed class PacketScriptOfficialSessionBridgeManager : IDisposable
    {
        public const int DefaultListenPort = 18498;

        private readonly LocalUtilityOfficialSessionBridgeManager _bridge;

        public int ListenPort => _bridge.ListenPort;
        public string RemoteHost => _bridge.RemoteHost;
        public int RemotePort => _bridge.RemotePort;
        public bool IsRunning => _bridge.IsRunning;
        public bool HasAttachedClient => _bridge.HasAttachedClient;
        public bool HasConnectedSession => _bridge.HasConnectedSession;
        public int SentCount => _bridge.SentCount;
        public int QueuedCount => _bridge.QueuedCount;
        public int PendingPacketCount => _bridge.PendingPacketCount;
        public int LastSentOpcode => _bridge.LastSentOpcode;
        public byte[] LastSentRawPacket => _bridge.LastSentRawPacket;
        public int LastQueuedOpcode => _bridge.LastQueuedOpcode;
        public byte[] LastQueuedRawPacket => _bridge.LastQueuedRawPacket;
        public string LastStatus => NormalizeStatus(_bridge.LastStatus);
        public bool UsesUnifiedRoleSessionProxy => true;

        public PacketScriptOfficialSessionBridgeManager(Func<MapleRoleSessionProxy> roleSessionProxyFactory = null)
        {
            _bridge = new LocalUtilityOfficialSessionBridgeManager(roleSessionProxyFactory);
        }

        public string DescribeStatus()
        {
            string lifecycle = IsRunning
                ? $"listening on 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}"
                : "inactive";
            string session = HasConnectedSession
                ? "connected Maple session"
                : HasAttachedClient
                    ? "attached client awaiting Maple init"
                    : "no active Maple session";
            string lastOutbound = LastSentOpcode >= 0
                ? $" lastOut={LastSentOpcode}[{Convert.ToHexString(LastSentRawPacket ?? Array.Empty<byte>())}]."
                : string.Empty;
            string lastQueued = LastQueuedOpcode >= 0
                ? $" lastQueued={LastQueuedOpcode}[{Convert.ToHexString(LastQueuedRawPacket ?? Array.Empty<byte>())}]."
                : string.Empty;
            return $"Packet-script official-session bridge {lifecycle}; {session}; sent={SentCount}; pending={PendingPacketCount}; queued={QueuedCount}; outbound opcode=65.{lastOutbound}{lastQueued} {LastStatus}";
        }

        public void Start(int listenPort, string remoteHost, int remotePort)
        {
            _bridge.Start(listenPort, remoteHost, remotePort);
        }

        public bool TryStartFromDiscovery(int listenPort, int remotePort, string processSelector, int? localPort, out string status)
        {
            bool started = _bridge.TryStartFromDiscovery(listenPort, remotePort, processSelector, localPort, out status);
            status = NormalizeStatus(status);
            return started;
        }

        public bool TryRefreshFromDiscovery(int listenPort, int remotePort, string processSelector, int? localPort, out string status)
        {
            bool refreshed = _bridge.TryRefreshFromDiscovery(listenPort, remotePort, processSelector, localPort, out status);
            status = NormalizeStatus(status);
            return refreshed;
        }

        public string DescribeDiscoveredSessions(int remotePort, string processSelector = null, int? localPort = null)
        {
            return NormalizeStatus(_bridge.DescribeDiscoveredSessions(remotePort, processSelector, localPort));
        }

        public void Stop()
        {
            _bridge.Stop();
        }

        public bool TrySendResponse(PacketScriptMessageRuntime.PacketScriptResponsePacket responsePacket, out string status)
        {
            if (!TryDecodeResponseRawPacket(responsePacket?.RawPacket, out int opcode, out byte[] payload, out status))
            {
                status = NormalizeStatus(status);
                return false;
            }

            bool sent = _bridge.TrySendOutboundPacket(opcode, payload, out status);
            status = NormalizeStatus(status);
            return sent;
        }

        public bool TryQueueResponse(PacketScriptMessageRuntime.PacketScriptResponsePacket responsePacket, out string status)
        {
            if (!TryDecodeResponseRawPacket(responsePacket?.RawPacket, out int opcode, out byte[] payload, out status))
            {
                status = NormalizeStatus(status);
                return false;
            }

            bool queued = _bridge.TryQueueOutboundPacket(opcode, payload, out status);
            status = NormalizeStatus(status);
            return queued;
        }

        internal static bool TryDecodeResponseRawPacket(byte[] rawPacket, out int opcode, out byte[] payload, out string status)
        {
            opcode = -1;
            payload = Array.Empty<byte>();
            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                status = "Packet-script official-session bridge did not receive a valid opcode-framed reply.";
                return false;
            }

            opcode = BitConverter.ToUInt16(rawPacket, 0);
            payload = rawPacket.Skip(sizeof(ushort)).ToArray();
            status = null;
            return true;
        }

        public void Dispose()
        {
            _bridge.Dispose();
        }

        private static string NormalizeStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return "Packet-script official-session bridge idle.";
            }

            return status
                .Replace("Local utility official-session bridge", "Packet-script official-session bridge", StringComparison.Ordinal)
                .Replace("Local utility outbound opcode", "Packet-script outbound opcode", StringComparison.Ordinal)
                .Replace("local utility outbound opcode", "packet-script outbound opcode", StringComparison.Ordinal);
        }
    }
}
