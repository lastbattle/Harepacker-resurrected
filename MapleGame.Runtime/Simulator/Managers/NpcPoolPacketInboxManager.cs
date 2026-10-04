using HaCreator.MapSimulator.Interaction;
using System;
using System.Collections.Concurrent;
using System.Globalization;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class NpcPoolPacketInboxMessage
    {
        public NpcPoolPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            PacketType = packetType;
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "npcpool-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    public sealed class NpcPoolPacketInboxManager : IDisposable
    {
        private readonly ConcurrentQueue<NpcPoolPacketInboxMessage> _pendingMessages = new();
        public int ReceivedCount { get; private set; }
        public int LocalIngressReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "NPC pool packet inbox ready for local ingress.";

        public bool TryDequeue(out NpcPoolPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void EnqueueLocal(int packetType, byte[] payload, string source)
        {
            string packetSource = string.IsNullOrWhiteSpace(source) ? "npcpool-local" : source;
            var message = new NpcPoolPacketInboxMessage(packetType, payload, packetSource, packetType.ToString(CultureInfo.InvariantCulture));
            _pendingMessages.Enqueue(message);
            ReceivedCount++;
            LocalIngressReceivedCount++;
            LastStatus = $"Queued {DescribePacketType(packetType)} from {packetSource}.";
        }

        public void RecordDispatchResult(NpcPoolPacketInboxMessage message, bool success, string detail)
        {
            string summary = DescribePacketType(message?.PacketType ?? 0);
            LastStatus = success
                ? $"Applied {summary} from {message?.Source ?? "npcpool-inbox"}."
                : $"Ignored {summary} from {message?.Source ?? "npcpool-inbox"}: {detail}";
        }

        public static bool TryParsePacketType(string token, out int packetType)
        {
            packetType = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string trimmed = token.Trim();
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out packetType)
                    && Enum.IsDefined(typeof(PacketNpcPoolPacketKind), (PacketNpcPoolPacketKind)packetType);
            }

            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out packetType))
            {
                return Enum.IsDefined(typeof(PacketNpcPoolPacketKind), (PacketNpcPoolPacketKind)packetType);
            }

            packetType = trimmed.ToLowerInvariant() switch
            {
                "imitate" or "imitatedata" => (int)PacketNpcPoolPacketKind.ImitateData,
                "limiteddisable" or "limiteddisableinfo" => (int)PacketNpcPoolPacketKind.UpdateLimitedDisableInfo,
                "enter" or "enterfield" => (int)PacketNpcPoolPacketKind.EnterField,
                "leave" or "leavefield" => (int)PacketNpcPoolPacketKind.LeaveField,
                "controller" or "changecontroller" => (int)PacketNpcPoolPacketKind.ChangeController,
                "move" => (int)PacketNpcPoolPacketKind.Move,
                "limited" or "limitedinfo" => (int)PacketNpcPoolPacketKind.UpdateLimitedInfo,
                "special" or "specialaction" => (int)PacketNpcPoolPacketKind.SetSpecialAction,
                "template" or "templatepacket" => (int)PacketNpcPoolPacketKind.TemplatePacket,
                _ => 0
            };
            return packetType != 0;
        }

        public static string DescribePacketType(int packetType)
        {
            return Enum.IsDefined(typeof(PacketNpcPoolPacketKind), (PacketNpcPoolPacketKind)packetType)
                ? $"{(PacketNpcPoolPacketKind)packetType} (0x{packetType:X})"
                : $"packet {packetType}";
        }

        public void Dispose()
        {
        }
    }
}
