using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using MapleLib.PacketLib;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Loopback outbox for packet-owned local utility requests that need to
    /// leave the simulator without a live Maple socket bridge.
    /// </summary>
    public sealed class LocalUtilityPacketTransportManager : IDisposable
    {
        public const int DefaultPort = 18487;
        private const int SentOutboundHistoryCapacity = 64;

        private sealed record PendingOutboundPacket(int Opcode, byte[] RawPacket);

        private readonly RetiredMapleSocketState _socketState = new("Local utility packet outbox", DefaultPort, "Local utility packet outbox inactive.");
        private readonly ConcurrentQueue<PendingOutboundPacket> _pendingOutboundPackets = new();
        private readonly ConcurrentQueue<PendingOutboundPacket> _sentOutboundHistory = new();

        public int Port => _socketState.Port;
        public bool IsRunning => _socketState.IsRunning;
        public bool HasConnectedClients => _socketState.HasConnectedClients;
        public int ConnectedClientCount => _socketState.ConnectedClientCount;
        public int SentCount { get; private set; }
        public int LastSentOpcode { get; private set; } = -1;
        public byte[] LastSentRawPacket { get; private set; } = Array.Empty<byte>();
        public int QueuedCount { get; private set; }
        public int LastQueuedOpcode { get; private set; } = -1;
        public byte[] LastQueuedRawPacket { get; private set; } = Array.Empty<byte>();
        public int PendingPacketCount => _pendingOutboundPackets.Count;
        public string LastStatus => _socketState.LastStatus;

        public string DescribeStatus()
        {
            string lastPacket = LastSentOpcode >= 0
                ? $" lastOut={LastSentOpcode}[{Convert.ToHexString(LastSentRawPacket)}]."
                : string.Empty;
            string queuedPacket = LastQueuedOpcode >= 0
                ? $" lastQueued={LastQueuedOpcode}[{Convert.ToHexString(LastQueuedRawPacket)}]."
                : string.Empty;
            return _socketState.Describe(sentCount: SentCount, pendingCount: PendingPacketCount, queuedCount: QueuedCount, detail: $"{lastPacket}{queuedPacket}".Trim());
        }

        public void Start(int port = DefaultPort)
        {
            _socketState.Start(port);
        }

        public void Stop()
        {
            StopInternal(clearState: true);
            _socketState.SetStatus("LocalUtility packet transport listener already retired.");
        }

        public bool TrySendOutboundPacket(int opcode, IReadOnlyList<byte> payload, out string status)
        {
            FlushQueuedOutboundPackets();
            if (opcode < ushort.MinValue || opcode > ushort.MaxValue)
            {
                status = $"Local utility outbound opcode {opcode} is outside the 16-bit Maple packet range.";
                _socketState.SetStatus(status);
                return false;
            }

            status = "Local utility packet outbox requires the role-session bridge or local packet command path; loopback transport is retired.";
            _socketState.SetStatus(status);
            return false;
        }

        public bool TryQueueOutboundPacket(int opcode, IReadOnlyList<byte> payload, out string status)
        {
            if (opcode < ushort.MinValue || opcode > ushort.MaxValue)
            {
                status = $"Local utility outbound opcode {opcode} is outside the 16-bit Maple packet range.";
                _socketState.SetStatus(status);
                return false;
            }

            byte[] rawPacket = BuildRawPacket((ushort)opcode, payload);
            _pendingOutboundPackets.Enqueue(new PendingOutboundPacket(opcode, rawPacket));
            QueuedCount++;
            LastQueuedOpcode = opcode;
            LastQueuedRawPacket = rawPacket;
            status = $"Queued packetoutraw {Convert.ToHexString(rawPacket)} for deferred local utility outbox delivery.";
            _socketState.SetStatus(status);
            return true;
        }

        public bool HasQueuedOutboundPacket(int opcode, IReadOnlyList<byte> rawPacket)
        {
            if (opcode < ushort.MinValue || opcode > ushort.MaxValue || rawPacket == null)
            {
                return false;
            }

            byte[] target = rawPacket as byte[] ?? rawPacket.ToArray();
            PendingOutboundPacket[] pending = _pendingOutboundPackets.ToArray();
            for (int i = 0; i < pending.Length; i++)
            {
                if (pending[i].Opcode == opcode && pending[i].RawPacket.AsSpan().SequenceEqual(target))
                {
                    return true;
                }
            }

            return false;
        }

        public bool WasLastSentOutboundPacket(int opcode, IReadOnlyList<byte> rawPacket)
        {
            if (opcode < ushort.MinValue || opcode > ushort.MaxValue || rawPacket == null || LastSentOpcode != opcode)
            {
                return false;
            }

            byte[] target = rawPacket as byte[] ?? rawPacket.ToArray();
            return LastSentRawPacket.AsSpan().SequenceEqual(target);
        }

        public bool HasSentOutboundPacket(int opcode, IReadOnlyList<byte> rawPacket)
        {
            if (opcode < ushort.MinValue || opcode > ushort.MaxValue || rawPacket == null)
            {
                return false;
            }

            byte[] target = rawPacket as byte[] ?? rawPacket.ToArray();
            if (LastSentOpcode == opcode && LastSentRawPacket.AsSpan().SequenceEqual(target))
            {
                return true;
            }

            PendingOutboundPacket[] history = _sentOutboundHistory.ToArray();
            for (int i = history.Length - 1; i >= 0; i--)
            {
                PendingOutboundPacket sent = history[i];
                if (sent.Opcode == opcode && sent.RawPacket.AsSpan().SequenceEqual(target))
                {
                    return true;
                }
            }

            return false;
        }

        public void Dispose()
        {
            StopInternal(clearState: true);
        }

        private void StopInternal(bool clearState)
        {
            if (clearState)
            {
                SentCount = 0;
                LastSentOpcode = -1;
                LastSentRawPacket = Array.Empty<byte>();
                while (_sentOutboundHistory.TryDequeue(out _))
                {
                }
            }
        }

        private int FlushQueuedOutboundPackets()
        {
            return 0;
        }

        private static byte[] BuildRawPacket(ushort opcode, IReadOnlyList<byte> payload)
        {
            using PacketWriter writer = new(sizeof(ushort) + (payload?.Count ?? 0));
            writer.Write(opcode);
            if (payload is byte[] bytes)
            {
                writer.WriteBytes(bytes);
            }
            else if (payload != null)
            {
                for (int i = 0; i < payload.Count; i++)
                {
                    writer.WriteByte(payload[i]);
                }
            }

            return writer.ToArray();
        }
    }
}
