using HaCreator.MapSimulator.Interaction;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class EngagementProposalInboxMessage
    {
        public EngagementProposalInboxMessage(
            string proposerName,
            string partnerName,
            int sealItemId,
            byte[] requestPayload,
            string customMessage,
            string source,
            string rawText)
        {
            ProposerName = proposerName?.Trim() ?? string.Empty;
            PartnerName = partnerName?.Trim() ?? string.Empty;
            SealItemId = sealItemId > 0 ? sealItemId : EngagementProposalRuntime.DefaultSealItemId;
            RequestPayload = requestPayload != null ? (byte[])requestPayload.Clone() : Array.Empty<byte>();
            CustomMessage = customMessage?.Trim() ?? string.Empty;
            Source = string.IsNullOrWhiteSpace(source) ? "engagement-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public string ProposerName { get; }
        public string PartnerName { get; }
        public int SealItemId { get; }
        public byte[] RequestPayload { get; }
        public string CustomMessage { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    public sealed class EngagementProposalInboxManager : IDisposable
    {
        public const int DefaultPort = 18487;
        public const string DefaultHost = "127.0.0.1";
        private const string RequestCommand = "request";

        private readonly ConcurrentQueue<EngagementProposalInboxMessage> _pendingMessages = new();
        private readonly object _listenerLock = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;

        public int Port { get; private set; } = DefaultPort;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Engagement proposal inbox inactive.";

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Engagement proposal inbox already listening on 127.0.0.1:{Port}.";
                    return;
                }

                StopInternal();
                Port = port <= 0 ? DefaultPort : port;
                _listenerCancellation = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
                _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                LastStatus = $"Engagement proposal inbox listening on 127.0.0.1:{Port}.";
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal();
                LastStatus = "Engagement proposal inbox stopped.";
            }
        }

        public bool TryDequeue(out EngagementProposalInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(EngagementProposalInboxMessage message, bool success, string detail)
        {
            string source = message?.Source ?? "engagement-inbox";
            string summary = string.IsNullOrWhiteSpace(detail)
                ? $"{message?.ProposerName ?? "unknown"} -> {message?.PartnerName ?? "local recipient"}"
                : detail;
            LastStatus = success
                ? $"Applied engagement request from {source}: {summary}"
                : $"Ignored engagement request from {source}: {summary}";
        }

        public void Dispose()
        {
            lock (_listenerLock)
            {
                StopInternal();
            }
        }

        internal static string BuildRequestLine(EngagementProposalInboxDispatch dispatch)
        {
            ArgumentNullException.ThrowIfNull(dispatch.RequestPayload);

            StringBuilder builder = new();
            builder.Append(RequestCommand);
            builder.Append(' ');
            builder.Append(string.IsNullOrWhiteSpace(dispatch.ProposerName) ? "Player" : dispatch.ProposerName.Trim());
            builder.Append(' ');
            builder.Append(string.IsNullOrWhiteSpace(dispatch.PartnerName) ? "Partner" : dispatch.PartnerName.Trim());
            builder.Append(' ');
            builder.Append("payloadhex=");
            builder.Append(Convert.ToHexString(dispatch.RequestPayload));

            if (dispatch.SealItemId > 0)
            {
                builder.Append(' ');
                builder.Append(dispatch.SealItemId);
            }

            if (!string.IsNullOrWhiteSpace(dispatch.CustomMessage))
            {
                builder.Append(' ');
                builder.Append(dispatch.CustomMessage.Trim());
            }

            return builder.ToString();
        }

        internal static void SendRequest(
            string host,
            int port,
            EngagementProposalInboxDispatch dispatch)
        {
            string resolvedHost = string.IsNullOrWhiteSpace(host) ? DefaultHost : host.Trim();
            int resolvedPort = port > 0 ? port : DefaultPort;
            string line = BuildRequestLine(dispatch);

            using TcpClient client = new();
            client.Connect(resolvedHost, resolvedPort);
            using NetworkStream stream = client.GetStream();
            using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: false);
            writer.WriteLine(line);
            writer.Flush();
        }

        public static bool TryParseLine(string text, out EngagementProposalInboxMessage message, out string error)
        {
            message = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Engagement inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith("/engageinbox", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["/engageinbox".Length..].TrimStart();
            }

            string[] tokens = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 4 || !string.Equals(tokens[0], RequestCommand, StringComparison.OrdinalIgnoreCase))
            {
                error = "Engagement inbox line must be: request <proposerName> <partnerName> <payloadhex=..|payloadb64=..> [sealItemId] [message...]";
                return false;
            }

            string proposerName = tokens[1];
            string partnerName = tokens[2];
            if (string.IsNullOrWhiteSpace(proposerName) || string.IsNullOrWhiteSpace(partnerName))
            {
                error = "Engagement inbox request requires non-empty proposer and partner names.";
                return false;
            }

            if (!TryParsePayload(tokens[3], out byte[] requestPayload, out error))
            {
                return false;
            }

            int sealItemId = EngagementProposalRuntime.DefaultSealItemId;
            int messageIndex = 4;
            if (tokens.Length > 4 && int.TryParse(tokens[4], out int parsedSealItemId) && parsedSealItemId > 0)
            {
                sealItemId = parsedSealItemId;
                messageIndex = 5;
            }

            string customMessage = tokens.Length > messageIndex
                ? string.Join(" ", tokens, messageIndex, tokens.Length - messageIndex)
                : string.Empty;

            message = new EngagementProposalInboxMessage(
                proposerName,
                partnerName,
                sealItemId,
                requestPayload,
                customMessage,
                "engagement-inbox",
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
                LastStatus = $"Engagement proposal inbox error: {ex.Message}";
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

                        if (!TryParseLine(line, out EngagementProposalInboxMessage message, out string error))
                        {
                            LastStatus = $"Ignored engagement inbox line from {remoteEndpoint}: {error}";
                            continue;
                        }

                        _pendingMessages.Enqueue(new EngagementProposalInboxMessage(
                            message.ProposerName,
                            message.PartnerName,
                            message.SealItemId,
                            message.RequestPayload,
                            message.CustomMessage,
                            remoteEndpoint,
                            line));
                        ReceivedCount++;
                        LastStatus = $"Queued engagement request from {message.ProposerName} for {message.PartnerName} from {remoteEndpoint}.";
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
                LastStatus = $"Engagement proposal inbox client error: {ex.Message}";
            }
        }

        private static bool TryParsePayload(string token, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = null;
            if (string.IsNullOrWhiteSpace(token))
            {
                error = "Engagement inbox request payload is missing.";
                return false;
            }

            const string payloadHexPrefix = "payloadhex=";
            const string payloadBase64Prefix = "payloadb64=";
            if (token.StartsWith(payloadHexPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string hex = token[payloadHexPrefix.Length..].Trim();
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

            if (token.StartsWith(payloadBase64Prefix, StringComparison.OrdinalIgnoreCase))
            {
                string base64 = token[payloadBase64Prefix.Length..].Trim();
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

            error = "Engagement inbox payload must use payloadhex=.. or payloadb64=..";
            return false;
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
