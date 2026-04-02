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
    public sealed class MobAttackPacketInboxMessage
    {
        public MobAttackPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            PacketType = packetType;
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "mobattack-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    public sealed class MobAttackPacketInboxManager : IDisposable
    {
        public const int MovePacketType = 287;
        public const int DefaultPort = 18488;

        private readonly ConcurrentQueue<MobAttackPacketInboxMessage> _pendingMessages = new();
        private readonly object _listenerLock = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;

        public int Port { get; private set; } = DefaultPort;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Mob attack packet inbox inactive.";

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Mob attack packet inbox already listening on 127.0.0.1:{Port}.";
                    return;
                }

                StopInternal();

                Port = port <= 0 ? DefaultPort : port;
                _listenerCancellation = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
                _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                LastStatus = $"Mob attack packet inbox listening on 127.0.0.1:{Port}.";
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal();
                LastStatus = "Mob attack packet inbox stopped.";
            }
        }

        public bool TryDequeue(out MobAttackPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(MobAttackPacketInboxMessage message, bool success, string detail)
        {
            string summary = DescribePacketType(message?.PacketType ?? 0);
            LastStatus = success
                ? $"Applied {summary} from {message?.Source ?? "mobattack-inbox"}."
                : $"Ignored {summary} from {message?.Source ?? "mobattack-inbox"}: {detail}";
        }

        public static bool TryParseLine(string text, out MobAttackPacketInboxMessage message, out string error)
        {
            message = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Mob attack inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith("/mobattackpacket", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["/mobattackpacket".Length..].TrimStart();
            }

            if (trimmed.Length == 0)
            {
                error = "Mob attack inbox line is empty.";
                return false;
            }

            int splitIndex = FindTokenSeparatorIndex(trimmed);
            string packetToken = splitIndex >= 0 ? trimmed[..splitIndex].Trim() : trimmed;
            string payloadToken = splitIndex >= 0 ? trimmed[(splitIndex + 1)..].Trim() : string.Empty;

            if (!TryParsePacketType(packetToken, out int packetType))
            {
                error = $"Unsupported mob attack packet '{packetToken}'.";
                return false;
            }

            byte[] payload = Array.Empty<byte>();
            if (!string.IsNullOrWhiteSpace(payloadToken) && !TryParsePayload(payloadToken, out payload, out error))
            {
                return false;
            }

            message = new MobAttackPacketInboxMessage(packetType, payload, "mobattack-inbox", text);
            return true;
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
                    && packetType == MovePacketType;
            }

            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out packetType))
            {
                return packetType == MovePacketType;
            }

            if (trimmed.Equals("move", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("mobmove", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("onmove", StringComparison.OrdinalIgnoreCase))
            {
                packetType = MovePacketType;
                return true;
            }

            return false;
        }

        public static string DescribePacketType(int packetType)
        {
            return packetType == MovePacketType
                ? $"MobMove (0x{MovePacketType:X})"
                : $"packet {packetType}";
        }

        public void Dispose()
        {
            lock (_listenerLock)
            {
                StopInternal();
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
                LastStatus = $"Mob attack packet inbox error: {ex.Message}";
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

                        if (!TryParseLine(line, out MobAttackPacketInboxMessage message, out string error))
                        {
                            LastStatus = $"Ignored mob attack inbox line from {remoteEndpoint}: {error}";
                            continue;
                        }

                        _pendingMessages.Enqueue(new MobAttackPacketInboxMessage(message.PacketType, message.Payload, remoteEndpoint, line));
                        ReceivedCount++;
                        LastStatus = $"Queued {DescribePacketType(message.PacketType)} from {remoteEndpoint}.";
                    }
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
                LastStatus = $"Mob attack packet inbox client error from {remoteEndpoint}: {ex.Message}";
            }
        }

        private void StopInternal()
        {
            _listenerCancellation?.Cancel();
            _listener?.Stop();

            try
            {
                _listenerTask?.Wait(100);
            }
            catch
            {
            }

            _listenerTask = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;
            _listener = null;
        }

        private static int FindTokenSeparatorIndex(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]) || text[i] == ':')
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TryParsePayload(string token, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = null;

            string trimmed = token.Trim();
            const string payloadHexPrefix = "payloadhex=";
            const string payloadBase64Prefix = "payloadb64=";
            if (trimmed.StartsWith(payloadHexPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return TryDecodeHexBytes(trimmed[payloadHexPrefix.Length..], out payload, out error);
            }

            if (trimmed.StartsWith(payloadBase64Prefix, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    payload = Convert.FromBase64String(trimmed[payloadBase64Prefix.Length..]);
                    return true;
                }
                catch (FormatException ex)
                {
                    error = $"Mob attack inbox base64 payload was invalid: {ex.Message}";
                    return false;
                }
            }

            return TryDecodeHexBytes(trimmed, out payload, out error);
        }

        private static bool TryDecodeHexBytes(string text, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            string normalized = text.Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase);
            if ((normalized.Length & 1) != 0)
            {
                error = "Mob attack inbox hex payload must contain an even number of characters.";
                return false;
            }

            try
            {
                payload = Convert.FromHexString(normalized);
                return true;
            }
            catch (FormatException ex)
            {
                error = $"Mob attack inbox hex payload was invalid: {ex.Message}";
                return false;
            }
        }
    }
}
