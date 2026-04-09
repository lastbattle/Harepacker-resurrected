using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HaCreator.MapSimulator.Interaction;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class ContextStagePeriodPacketInboxMessage
    {
        public ContextStagePeriodPacketInboxMessage(byte[] payload, string source, string rawText)
        {
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "stageperiod-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    public sealed class ContextStagePeriodPacketInboxManager : IDisposable
    {
        public const int DefaultPort = 18497;

        private readonly ConcurrentQueue<ContextStagePeriodPacketInboxMessage> _pendingMessages = new();
        private readonly object _listenerLock = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;

        public int Port { get; private set; } = DefaultPort;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Context-owned stage-period inbox inactive.";

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Context-owned stage-period inbox already listening on 127.0.0.1:{Port.ToString(CultureInfo.InvariantCulture)}.";
                    return;
                }

                StopInternal();
                Port = port <= 0 ? DefaultPort : port;
                _listenerCancellation = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
                _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                LastStatus = $"Context-owned stage-period inbox listening on 127.0.0.1:{Port.ToString(CultureInfo.InvariantCulture)}.";
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal();
                LastStatus = "Context-owned stage-period inbox stopped.";
            }
        }

        public bool TryDequeue(out ContextStagePeriodPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(ContextStagePeriodPacketInboxMessage message, bool success, string detail)
        {
            LastStatus = success
                ? $"Applied context-owned stage-period payload from {message?.Source ?? "stageperiod-inbox"}."
                : $"Ignored context-owned stage-period payload from {message?.Source ?? "stageperiod-inbox"}: {detail}";
        }

        public void Dispose()
        {
            lock (_listenerLock)
            {
                StopInternal();
            }
        }

        public static bool TryParseLine(string text, out ContextStagePeriodPacketInboxMessage message, out string error)
        {
            message = null;
            error = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Context-owned stage-period inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith("/stageperiod", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["/stageperiod".Length..].TrimStart();
            }

            if (trimmed.StartsWith("apply ", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["apply".Length..].TrimStart();
            }

            if (trimmed.StartsWith("packet ", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["packet".Length..].TrimStart();
            }

            if (trimmed.StartsWith("packetraw ", StringComparison.OrdinalIgnoreCase))
            {
                string hex = trimmed["packetraw".Length..].Trim();
                if (!TryParsePayload($"payloadhex={hex}", out byte[] rawPayload, out error))
                {
                    return false;
                }

                message = new ContextStagePeriodPacketInboxMessage(rawPayload, "stageperiod-inbox", text);
                return true;
            }

            if (trimmed.StartsWith("payloadhex=", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("payloadb64=", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParsePayload(trimmed, out byte[] payload, out error))
                {
                    return false;
                }

                message = new ContextStagePeriodPacketInboxMessage(payload, "stageperiod-inbox", text);
                return true;
            }

            int splitIndex = FindTokenSeparatorIndex(trimmed);
            string stagePeriod = splitIndex >= 0 ? trimmed[..splitIndex].Trim() : trimmed;
            string modeToken = splitIndex >= 0 ? trimmed[(splitIndex + 1)..].Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(stagePeriod))
            {
                error = "Context-owned stage-period inbox line must include a stage-period string.";
                return false;
            }

            byte mode = 0;
            if (!string.IsNullOrWhiteSpace(modeToken) && !byte.TryParse(modeToken, out mode))
            {
                error = "Context-owned stage-period mode must be a byte.";
                return false;
            }

            message = new ContextStagePeriodPacketInboxMessage(
                ContextOwnedStagePeriodRuntime.BuildPayload(stagePeriod, mode),
                "stageperiod-inbox",
                text);
            return true;
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
                LastStatus = $"Context-owned stage-period inbox error: {ex.Message}";
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            string remoteEndpoint = client.Client?.RemoteEndPoint?.ToString() ?? "loopback-client";
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new(stream))
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        string line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                        if (line == null)
                        {
                            break;
                        }

                        if (!TryParseLine(line, out ContextStagePeriodPacketInboxMessage message, out string error))
                        {
                            LastStatus = $"Ignored context-owned stage-period inbox line from {remoteEndpoint}: {error}";
                            continue;
                        }

                        _pendingMessages.Enqueue(new ContextStagePeriodPacketInboxMessage(message.Payload, remoteEndpoint, line));
                        ReceivedCount++;
                        LastStatus = $"Queued context-owned stage-period payload from {remoteEndpoint}.";
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
                LastStatus = $"Context-owned stage-period inbox client error: {ex.Message}";
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
                try
                {
                    payload = Convert.FromBase64String(base64);
                    return true;
                }
                catch (FormatException)
                {
                    error = "payloadb64= must contain valid Base64 text.";
                    return false;
                }
            }

            error = "Context-owned stage-period payloads must use payloadhex=.. or payloadb64=..";
            return false;
        }

        private static int FindTokenSeparatorIndex(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]))
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
            catch (SocketException)
            {
            }

            _listener = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;
            _listenerTask = null;
        }
    }
}
