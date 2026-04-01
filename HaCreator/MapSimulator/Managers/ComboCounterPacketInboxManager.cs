using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class ComboCounterPacketInboxMessage
    {
        public ComboCounterPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            PacketType = packetType;
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "combo-packet-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    /// <summary>
    /// Loopback inbox for packet-owned combo counter updates.
    /// </summary>
    public sealed class ComboCounterPacketInboxManager : IDisposable
    {
        public const int DefaultPort = 18486;
        public const int IncComboResponsePacketType = 1100;

        private readonly ConcurrentQueue<ComboCounterPacketInboxMessage> _pendingMessages = new();
        private readonly object _listenerLock = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;

        public int Port { get; private set; } = DefaultPort;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Combo packet inbox inactive.";

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Combo packet inbox already listening on 127.0.0.1:{Port}.";
                    return;
                }

                StopInternal();
                Port = port <= 0 ? DefaultPort : port;
                _listenerCancellation = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
                _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                LastStatus = $"Combo packet inbox listening on 127.0.0.1:{Port}.";
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal();
                LastStatus = "Combo packet inbox stopped.";
            }
        }

        public bool TryDequeue(out ComboCounterPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(ComboCounterPacketInboxMessage message, bool success, string detail)
        {
            string summary = DescribePacketType(message?.PacketType ?? 0);
            LastStatus = success
                ? $"Applied {summary} from {message?.Source ?? "combo-packet-inbox"}."
                : $"Ignored {summary} from {message?.Source ?? "combo-packet-inbox"}: {detail}";
        }

        public static byte[] BuildComboCountPayload(int comboCount)
        {
            return BitConverter.GetBytes(comboCount);
        }

        public void Dispose()
        {
            lock (_listenerLock)
            {
                StopInternal();
            }
        }

        public static bool TryParseLine(string text, out ComboCounterPacketInboxMessage message, out string error)
        {
            message = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Combo inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith("/combopacket", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["/combopacket".Length..].TrimStart();
            }

            if (trimmed.Length == 0)
            {
                error = "Combo inbox line is empty.";
                return false;
            }

            int splitIndex = FindTokenSeparatorIndex(trimmed);
            string packetToken = splitIndex >= 0 ? trimmed[..splitIndex].Trim() : trimmed;
            string payloadToken = splitIndex >= 0 ? trimmed[(splitIndex + 1)..].Trim() : string.Empty;

            if (!TryParsePacketType(packetToken, out int packetType))
            {
                error = $"Unsupported combo packet '{packetToken}'.";
                return false;
            }

            byte[] payload = Array.Empty<byte>();
            if (!string.IsNullOrWhiteSpace(payloadToken) && !TryParsePayload(payloadToken, out payload, out error))
            {
                return false;
            }

            message = new ComboCounterPacketInboxMessage(packetType, payload, "combo-packet-inbox", text);
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
            if (string.Equals(normalized, "inccombo", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "inccomboresponse", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "combo", StringComparison.OrdinalIgnoreCase))
            {
                packetType = IncComboResponsePacketType;
                return true;
            }

            if ((normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(normalized[2..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out packetType))
                || int.TryParse(normalized, out packetType))
            {
                return packetType == IncComboResponsePacketType;
            }

            return false;
        }

        public static string DescribePacketType(int packetType)
        {
            return packetType == IncComboResponsePacketType
                ? $"IncComboResponse (0x{packetType:X})"
                : $"0x{packetType:X}";
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
                LastStatus = $"Combo packet inbox error: {ex.Message}";
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

                        if (!TryParseLine(line, out ComboCounterPacketInboxMessage message, out string error))
                        {
                            LastStatus = $"Ignored combo inbox line from {remoteEndpoint}: {error}";
                            continue;
                        }

                        _pendingMessages.Enqueue(new ComboCounterPacketInboxMessage(message.PacketType, message.Payload, remoteEndpoint, line));
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
                LastStatus = $"Combo packet inbox client error: {ex.Message}";
            }
        }

        private static bool TryParsePayload(string payloadToken, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = null;

            if (payloadToken.StartsWith("payloadhex=", StringComparison.OrdinalIgnoreCase))
            {
                return TryDecodeHex(payloadToken["payloadhex=".Length..], out payload, out error);
            }

            if (payloadToken.StartsWith("payloadb64=", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    payload = Convert.FromBase64String(payloadToken["payloadb64=".Length..]);
                    return true;
                }
                catch (FormatException ex)
                {
                    error = $"Base64 payload is invalid: {ex.Message}";
                    return false;
                }
            }

            if (int.TryParse(payloadToken, out int comboCount))
            {
                payload = BuildComboCountPayload(comboCount);
                return true;
            }

            error = "Combo payload must use payloadhex=.., payloadb64=.., or a plain integer combo count.";
            return false;
        }

        private static bool TryDecodeHex(string hex, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = null;

            if (string.IsNullOrWhiteSpace(hex))
            {
                error = "Hex payload is empty.";
                return false;
            }

            string normalized = hex.Replace(" ", string.Empty, StringComparison.Ordinal);
            if ((normalized.Length & 1) != 0)
            {
                error = "Hex payload must contain an even number of digits.";
                return false;
            }

            byte[] buffer = new byte[normalized.Length / 2];
            for (int i = 0; i < buffer.Length; i++)
            {
                if (!byte.TryParse(normalized.AsSpan(i * 2, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out buffer[i]))
                {
                    error = $"Hex payload contains an invalid byte at offset {i}.";
                    return false;
                }
            }

            payload = buffer;
            return true;
        }

        private static int FindTokenSeparatorIndex(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c) || c == ':' || c == '=')
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
