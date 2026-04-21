using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class LocalOverlayPacketInboxMessage
    {
        public LocalOverlayPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            PacketType = packetType;
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "local-overlay-packet-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    public sealed class LocalOverlayPacketInboxManager : IDisposable
    {
        public const int DefaultPort = 18497;
        public const int FieldFadeInOutClientPacketType = 240;
        public const int FieldFadeOutForceClientPacketType = 241;
        public const int BalloonMsgClientPacketType = 245;
        public const int DamageMeterClientPacketType = LocalUtilityPacketInboxManager.DamageMeterPacketType;
        public const int NotifyHpDecByFieldClientPacketType = LocalUtilityPacketInboxManager.NotifyHpDecByFieldPacketType;
        public const int PetConsumeResultPacketType = LocalUtilityPacketInboxManager.PetConsumeResultPacketType;

        private readonly ConcurrentQueue<LocalOverlayPacketInboxMessage> _pendingMessages = new();
        private readonly object _listenerLock = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;

        public int Port { get; private set; } = DefaultPort;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Local overlay packet inbox inactive.";

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Local overlay packet inbox already listening on 127.0.0.1:{Port}.";
                    return;
                }

                StopInternal();

                Port = port <= 0 ? DefaultPort : port;
                _listenerCancellation = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
                _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                LastStatus = $"Local overlay packet inbox listening on 127.0.0.1:{Port}.";
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal();
                LastStatus = "Local overlay packet inbox stopped.";
            }
        }

        public void EnqueueLocal(int packetType, byte[] payload, string source)
        {
            string packetSource = string.IsNullOrWhiteSpace(source) ? "local-overlay-ui" : source;
            _pendingMessages.Enqueue(new LocalOverlayPacketInboxMessage(packetType, payload, packetSource, packetType.ToString(CultureInfo.InvariantCulture)));
            ReceivedCount++;
            LastStatus = $"Queued {DescribePacketType(packetType)} from {packetSource}.";
        }

        public bool TryDequeue(out LocalOverlayPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(LocalOverlayPacketInboxMessage message, bool success, string detail)
        {
            string summary = DescribePacketType(message?.PacketType ?? 0);
            LastStatus = success
                ? $"Applied {summary} from {message?.Source ?? "local-overlay-packet-inbox"}."
                : $"Ignored {summary} from {message?.Source ?? "local-overlay-packet-inbox"}: {detail}";
        }

        public void Dispose()
        {
            lock (_listenerLock)
            {
                StopInternal();
            }
        }

        public static bool TryParseLine(string text, out LocalOverlayPacketInboxMessage message, out string error)
        {
            message = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Local overlay inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith("/localoverlaypacket", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["/localoverlaypacket".Length..].TrimStart();
            }

            if (trimmed.Length == 0)
            {
                error = "Local overlay inbox line is empty.";
                return false;
            }

            if (trimmed.StartsWith("packetclientraw", StringComparison.OrdinalIgnoreCase))
            {
                string rawHex = trimmed["packetclientraw".Length..].Trim();
                if (!TryParsePayload(rawHex, out byte[] rawPacket, out error))
                {
                    return false;
                }

                if (!LocalUtilityPacketInboxManager.TryDecodeOpcodeFramedPacket(rawPacket, out int packetType, out byte[] payload, out error)
                    || !IsSupportedPacketType(packetType))
                {
                    error ??= $"Unsupported local overlay client opcode {packetType}.";
                    return false;
                }

                message = new LocalOverlayPacketInboxMessage(packetType, payload, "local-overlay-packet-inbox", text);
                return true;
            }

            int splitIndex = FindTokenSeparatorIndex(trimmed);
            string packetToken = splitIndex >= 0 ? trimmed[..splitIndex].Trim() : trimmed;
            string payloadToken = splitIndex >= 0 ? trimmed[(splitIndex + 1)..].Trim() : string.Empty;

            if (!TryParsePacketType(packetToken, out int parsedPacketType))
            {
                error = $"Unsupported local overlay packet '{packetToken}'.";
                return false;
            }

            byte[] parsedPayload = Array.Empty<byte>();
            if (!string.IsNullOrWhiteSpace(payloadToken) && !TryParsePayload(payloadToken, out parsedPayload, out error))
            {
                return false;
            }

            message = new LocalOverlayPacketInboxMessage(parsedPacketType, parsedPayload, "local-overlay-packet-inbox", text);
            return true;
        }

        public static bool TryParsePacketType(string token, out int packetType)
        {
            packetType = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string normalized = token.Trim();
            if (string.Equals(normalized, "fade", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "fieldfade", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "fieldfadeinout", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "onfieldfadeinout", StringComparison.OrdinalIgnoreCase))
            {
                packetType = FieldFadeInOutClientPacketType;
                return true;
            }

            if (string.Equals(normalized, "fadeoutforce", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "fieldfadeoutforce", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "onfieldfadeoutforce", StringComparison.OrdinalIgnoreCase))
            {
                packetType = FieldFadeOutForceClientPacketType;
                return true;
            }

            if (string.Equals(normalized, "balloon", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "balloonmsg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "onballoonmsg", StringComparison.OrdinalIgnoreCase))
            {
                packetType = BalloonMsgClientPacketType;
                return true;
            }

            if (string.Equals(normalized, "damagemeter", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "damage", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "ondamagemeter", StringComparison.OrdinalIgnoreCase))
            {
                packetType = DamageMeterClientPacketType;
                return true;
            }

            if (string.Equals(normalized, "hpdec", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "hazard", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "notifyhpdecbyfield", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "onnotifyhpdecbyfield", StringComparison.OrdinalIgnoreCase))
            {
                packetType = NotifyHpDecByFieldClientPacketType;
                return true;
            }

            if (string.Equals(normalized, "petconsumeresult", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "onpetconsumeresult", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "petitemuseresult", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "onpetitemuseresult", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "petuseresult", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "onpetuseresult", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "hazardresult", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "hpresult", StringComparison.OrdinalIgnoreCase))
            {
                packetType = PetConsumeResultPacketType;
                return true;
            }

            if ((normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(normalized[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out packetType))
                || int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out packetType))
            {
                return IsSupportedPacketType(packetType);
            }

            return false;
        }

        public static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                FieldFadeInOutClientPacketType => $"OnFieldFadeInOut (0x{packetType:X})",
                FieldFadeOutForceClientPacketType => $"OnFieldFadeOutForce (0x{packetType:X})",
                BalloonMsgClientPacketType => $"OnBalloonMsg (0x{packetType:X})",
                NotifyHpDecByFieldClientPacketType => $"OnNotifyHPDecByField (0x{packetType:X})",
                DamageMeterClientPacketType => $"OnDamageMeter (0x{packetType:X})",
                PetConsumeResultPacketType => $"OnPetConsumeResult (0x{packetType:X})",
                _ => $"0x{packetType:X}"
            };
        }

        public static bool IsSupportedPacketType(int packetType)
        {
            return packetType == FieldFadeInOutClientPacketType
                || packetType == FieldFadeOutForceClientPacketType
                || packetType == BalloonMsgClientPacketType
                || packetType == NotifyHpDecByFieldClientPacketType
                || packetType == DamageMeterClientPacketType
                || packetType == PetConsumeResultPacketType;
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
                LastStatus = $"Local overlay packet inbox error: {ex.Message}";
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            string remoteEndpoint = client.Client?.RemoteEndPoint?.ToString() ?? "loopback-client";
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        string line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                        if (line == null)
                        {
                            break;
                        }

                        if (!TryParseLine(line, out LocalOverlayPacketInboxMessage message, out string lineError))
                        {
                            LastStatus = $"Ignored local overlay inbox line from {remoteEndpoint}: {lineError}";
                            continue;
                        }

                        _pendingMessages.Enqueue(new LocalOverlayPacketInboxMessage(message.PacketType, message.Payload, remoteEndpoint, line));
                        ReceivedCount++;
                        LastStatus = $"Queued {DescribePacketType(message.PacketType)} from {remoteEndpoint}.";
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                LastStatus = $"Local overlay packet inbox client error: {ex.Message}";
            }
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

        private static int FindTokenSeparatorIndex(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]) || text[i] == ':' || text[i] == '=')
                {
                    return i;
                }
            }

            return -1;
        }

        private void StopInternal()
        {
            _listenerCancellation?.Cancel();

            try
            {
                _listener?.Stop();
            }
            catch
            {
            }

            try
            {
                _listenerTask?.Wait(200);
            }
            catch
            {
            }

            _listener = null;
            _listenerTask = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;
        }
    }
}
