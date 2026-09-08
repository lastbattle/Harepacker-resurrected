using System;
using System.Collections.Concurrent;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class StageTransitionPacketInboxMessage
    {
        public StageTransitionPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            PacketType = packetType;
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "stagepacket-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    public sealed class StageTransitionPacketInboxManager : IDisposable
    {
        private readonly ConcurrentQueue<StageTransitionPacketInboxMessage> _pendingMessages = new();
        public string LastStatus { get; private set; } = "Stage-transition packet inbox ready for role-session/local ingress.";

        public bool TryDequeue(out StageTransitionPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void EnqueueProxy(int packetType, byte[] payload, string source)
        {
            string packetSource = string.IsNullOrWhiteSpace(source) ? "stagepacket-proxy" : source;
            EnqueueMessage(
                new StageTransitionPacketInboxMessage(packetType, payload, packetSource, packetType.ToString()),
                packetSource);
        }

        public void EnqueueLocal(int packetType, byte[] payload, string source)
        {
            string packetSource = string.IsNullOrWhiteSpace(source) ? "stagepacket-local" : source;
            EnqueueMessage(
                new StageTransitionPacketInboxMessage(packetType, payload, packetSource, packetType.ToString()),
                packetSource);
        }

        public void RecordDispatchResult(StageTransitionPacketInboxMessage message, bool success, string detail)
        {
            string summary = DescribePacketType(message?.PacketType ?? 0);
            LastStatus = success
                ? $"Applied {summary} from {message?.Source ?? "stagepacket-inbox"}."
                : $"Ignored {summary} from {message?.Source ?? "stagepacket-inbox"}: {detail}";
        }

        public void Dispose()
        {
        }

        public static bool TryParsePacketType(string token, out int packetType)
        {
            packetType = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (int.TryParse(token, out packetType))
            {
                return packetType is >= 141 and <= 146;
            }

            packetType = token.Trim().ToLowerInvariant() switch
            {
                "field" or "setfield" => 141,
                "itc" or "setitc" => 142,
                "cashshop" or "setcashshop" => 143,
                "backeffect" or "setbackeffect" => 144,
                "objectvisible" or "setmapobjectvisible" => 145,
                "clearbackeffect" => 146,
                _ => 0
            };
            return packetType != 0;
        }

        private static bool TryParsePayload(string text, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = null;

            const string payloadHexPrefix = "payloadhex=";
            const string payloadBase64Prefix = "payloadb64=";
            if (text.StartsWith(payloadHexPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string hex = text[payloadHexPrefix.Length..].Trim();
                if (hex.Length == 0 || (hex.Length % 2) != 0)
                {
                    error = "payloadhex= must contain an even-length hexadecimal byte string.";
                    return false;
                }

                try
                {
                    payload = Convert.FromHexString(hex);
                    return true;
                }
                catch (FormatException)
                {
                    error = "payloadhex= must contain only hexadecimal characters.";
                    return false;
                }
            }

            if (text.StartsWith(payloadBase64Prefix, StringComparison.OrdinalIgnoreCase))
            {
                string base64 = text[payloadBase64Prefix.Length..].Trim();
                if (base64.Length == 0)
                {
                    error = "payloadb64= must not be empty.";
                    return false;
                }

                try
                {
                    payload = Convert.FromBase64String(base64);
                    return true;
                }
                catch (FormatException)
                {
                    error = "payloadb64= must contain a valid base64 payload.";
                    return false;
                }
            }

            try
            {
                payload = Convert.FromHexString(text.Replace(" ", string.Empty, StringComparison.Ordinal));
                return true;
            }
            catch (FormatException)
            {
                error = "Packet payload must use payloadhex=.., payloadb64=.., or a compact raw hex byte string.";
                return false;
            }
        }

        private static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                141 => "SetField(141)",
                142 => "SetITC(142)",
                143 => "SetCashShop(143)",
                144 => "SetBackEffect(144)",
                145 => "SetMapObjectVisible(145)",
                146 => "ClearBackEffect(146)",
                _ => $"packet {packetType}"
            };
        }

        private void EnqueueMessage(StageTransitionPacketInboxMessage message, string sourceLabel)
        {
            if (message == null)
            {
                return;
            }

            _pendingMessages.Enqueue(message);
            string packetSource = string.IsNullOrWhiteSpace(sourceLabel) ? "stagepacket-inbox" : sourceLabel;
            LastStatus = $"Queued {DescribePacketType(message.PacketType)} from {packetSource}.";
        }
    }
}

