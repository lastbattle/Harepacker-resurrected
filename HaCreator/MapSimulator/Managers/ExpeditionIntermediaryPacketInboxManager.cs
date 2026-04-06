using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class ExpeditionIntermediaryPacketInboxMessage
    {
        public ExpeditionIntermediaryPacketInboxMessage(byte[] payload, string source, string rawText, int opcode = -1)
        {
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "expedition-inbox" : source;
            RawText = rawText ?? string.Empty;
            Opcode = opcode;
        }

        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
        public int Opcode { get; }
        public bool HasOpcodeFrame => Opcode >= 0;
    }

    /// <summary>
    /// Loopback inbox for decoded ExpeditionIntermediary payloads. Each line is
    /// either a direct retCode-prefixed payload, or an opcode-framed client
    /// packet line starting with packetclientraw.
    /// </summary>
    public sealed class ExpeditionIntermediaryPacketInboxManager : IDisposable
    {
        public const int DefaultPort = 18502;

        private readonly ConcurrentQueue<ExpeditionIntermediaryPacketInboxMessage> _pendingMessages = new();
        private readonly object _listenerLock = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;

        public int Port { get; private set; } = DefaultPort;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Expedition intermediary packet inbox inactive.";

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Expedition intermediary packet inbox already listening on 127.0.0.1:{Port}.";
                    return;
                }

                StopInternal();
                Port = port <= 0 ? DefaultPort : port;
                _listenerCancellation = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
                _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                LastStatus = $"Expedition intermediary packet inbox listening on 127.0.0.1:{Port}.";
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal();
                LastStatus = "Expedition intermediary packet inbox stopped.";
            }
        }

        public bool TryDequeue(out ExpeditionIntermediaryPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(ExpeditionIntermediaryPacketInboxMessage message, bool success, string detail)
        {
            string summary = DescribeMessage(message);
            LastStatus = success
                ? $"Applied {summary} from {message?.Source ?? "expedition-inbox"}."
                : $"Ignored {summary} from {message?.Source ?? "expedition-inbox"}: {detail}";
        }

        public static bool TryParseLine(string text, out ExpeditionIntermediaryPacketInboxMessage message, out string error)
        {
            message = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Expedition intermediary inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith("/expeditionpacket", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["/expeditionpacket".Length..].TrimStart();
            }

            if (trimmed.Length == 0)
            {
                error = "Expedition intermediary inbox line is empty.";
                return false;
            }

            if (trimmed.StartsWith("packetclientraw", StringComparison.OrdinalIgnoreCase))
            {
                string rawHex = trimmed["packetclientraw".Length..].Trim();
                if (!TryParsePayload(rawHex, out byte[] rawPacket, out error))
                {
                    return false;
                }

                if (!TryDecodeOpcodeFramedPacket(rawPacket, out int opcode, out byte[] payload, out error))
                {
                    return false;
                }

                message = new ExpeditionIntermediaryPacketInboxMessage(payload, "expedition-inbox", text, opcode);
                return true;
            }

            if (!TryParsePayload(trimmed, out byte[] directPayload, out error))
            {
                return false;
            }

            message = new ExpeditionIntermediaryPacketInboxMessage(directPayload, "expedition-inbox", text);
            return true;
        }

        public static bool TryDecodeOpcodeFramedPacket(byte[] rawPacket, out int opcode, out byte[] payload, out string error)
        {
            opcode = -1;
            payload = Array.Empty<byte>();
            error = null;

            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                error = "Expedition intermediary client packet must include a 2-byte opcode.";
                return false;
            }

            opcode = BitConverter.ToUInt16(rawPacket, 0);
            payload = rawPacket.Length == sizeof(ushort)
                ? Array.Empty<byte>()
                : rawPacket[sizeof(ushort)..];
            return true;
        }

        public static string DescribeMessage(ExpeditionIntermediaryPacketInboxMessage message)
        {
            if (message == null)
            {
                return "expedition payload";
            }

            return message.HasOpcodeFrame
                ? $"ExpeditionResult opcode {message.Opcode} ({message.Payload.Length} byte(s))"
                : $"expedition payload ({message.Payload.Length} byte(s))";
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
                LastStatus = $"Expedition intermediary packet inbox error: {ex.Message}";
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

                        if (!TryParseLine(line, out ExpeditionIntermediaryPacketInboxMessage message, out string error))
                        {
                            LastStatus = $"Ignored expedition intermediary inbox line from {remoteEndpoint}: {error}";
                            continue;
                        }

                        _pendingMessages.Enqueue(new ExpeditionIntermediaryPacketInboxMessage(message.Payload, remoteEndpoint, line, message.Opcode));
                        ReceivedCount++;
                        LastStatus = $"Queued {DescribeMessage(message)} from {remoteEndpoint}.";
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
                LastStatus = $"Expedition intermediary packet inbox client error: {ex.Message}";
            }
        }

        private static bool TryParsePayload(string text, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Expedition intermediary payload is missing.";
                return false;
            }

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

            string normalized = text.Replace(" ", string.Empty, StringComparison.Ordinal);
            if (normalized.Length == 0 || (normalized.Length % 2) != 0)
            {
                error = "Packet payload must use payloadhex=.., payloadb64=.., or a compact raw hex byte string.";
                return false;
            }

            try
            {
                payload = Convert.FromHexString(normalized);
                return true;
            }
            catch (FormatException)
            {
                error = "Packet payload must use payloadhex=.., payloadb64=.., or a compact raw hex byte string.";
                return false;
            }
        }

        private void StopInternal()
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

            _listenerTask = null;
            _listener = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;
        }
    }
}
