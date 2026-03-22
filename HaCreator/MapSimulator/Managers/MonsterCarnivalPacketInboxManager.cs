using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class MonsterCarnivalPacketInboxMessage
    {
        public MonsterCarnivalPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            PacketType = packetType;
            Payload = payload != null ? (byte[])payload.Clone() : Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "mcarnival-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    /// <summary>
    /// Optional loopback inbox for live Monster Carnival field packets.
    /// Each line is encoded as "<type> <hex-payload>", where type can be the numeric packet id
    /// or aliases such as "enter", "personalcp", "teamcp", "requestresult", "requestfail",
    /// "death", "memberout", and "result".
    /// </summary>
    public sealed class MonsterCarnivalPacketInboxManager : IDisposable
    {
        public const int DefaultPort = 18487;
        private const int PacketTypeEnter = 346;
        private const int PacketTypePersonalCp = 347;
        private const int PacketTypeTeamCp = 348;
        private const int PacketTypeRequestResult = 349;
        private const int PacketTypeRequestFailure = 350;
        private const int PacketTypeProcessForDeath = 351;
        private const int PacketTypeShowMemberOutMessage = 352;
        private const int PacketTypeGameResult = 353;

        private readonly ConcurrentQueue<MonsterCarnivalPacketInboxMessage> _pendingMessages = new();
        private readonly object _listenerLock = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;

        public int Port { get; private set; } = DefaultPort;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Monster Carnival packet inbox inactive.";

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Monster Carnival packet inbox already listening on 127.0.0.1:{Port}.";
                    return;
                }

                StopInternal(clearPending: true);

                try
                {
                    Port = port <= 0 ? DefaultPort : port;
                    _listenerCancellation = new CancellationTokenSource();
                    _listener = new TcpListener(IPAddress.Loopback, Port);
                    _listener.Start();
                    _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                    LastStatus = $"Monster Carnival packet inbox listening on 127.0.0.1:{Port}.";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Monster Carnival packet inbox failed to start: {ex.Message}";
                }
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal(clearPending: true);
                LastStatus = "Monster Carnival packet inbox stopped.";
            }
        }

        public void EnqueueLocal(int packetType, byte[] payload, string source)
        {
            _pendingMessages.Enqueue(new MonsterCarnivalPacketInboxMessage(packetType, payload, source, $"{packetType}"));
        }

        public bool TryDequeue(out MonsterCarnivalPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, int packetType, bool success, string message)
        {
            string packetLabel = DescribePacketType(packetType);
            string summary = string.IsNullOrWhiteSpace(message) ? packetLabel : $"{packetLabel}: {message}";
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
        }

        public void Dispose()
        {
            lock (_listenerLock)
            {
                StopInternal(clearPending: true);
            }
        }

        public static bool TryParsePacketLine(string text, out int packetType, out byte[] payload, out string error)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Monster Carnival inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            int separatorIndex = trimmed.IndexOfAny(new[] { ' ', '\t' });
            string typeToken = separatorIndex >= 0 ? trimmed[..separatorIndex] : trimmed;
            string payloadToken = separatorIndex >= 0 ? trimmed[(separatorIndex + 1)..].Trim() : string.Empty;

            if (!TryParsePacketType(typeToken, out packetType))
            {
                error = $"Unsupported Monster Carnival packet type: {typeToken}";
                return false;
            }

            string compactHex = RemoveWhitespace(payloadToken);
            if (string.IsNullOrWhiteSpace(compactHex))
            {
                error = "Monster Carnival packet requires a hex payload.";
                return false;
            }

            if (compactHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                compactHex = compactHex[2..];
            }

            try
            {
                payload = Convert.FromHexString(compactHex);
                return true;
            }
            catch (FormatException)
            {
                error = $"Invalid Monster Carnival packet hex payload: {payloadToken}";
                return false;
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
                LastStatus = $"Monster Carnival packet inbox error: {ex.Message}";
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

                        if (!TryParsePacketLine(line, out int packetType, out byte[] payload, out string error))
                        {
                            LastStatus = $"Ignored Monster Carnival inbox line from {remoteEndpoint}: {error}";
                            continue;
                        }

                        _pendingMessages.Enqueue(new MonsterCarnivalPacketInboxMessage(packetType, payload, remoteEndpoint, line));
                        ReceivedCount++;
                        LastStatus = $"Queued {DescribePacketType(packetType)} from {remoteEndpoint}.";
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
                LastStatus = $"Monster Carnival packet inbox client error: {ex.Message}";
            }
        }

        private static bool TryParsePacketType(string token, out int packetType)
        {
            packetType = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string normalized = token.Trim().ToLowerInvariant();
            return normalized switch
            {
                "346" or "enter" => AssignPacketType(PacketTypeEnter, out packetType),
                "347" or "personalcp" or "personal" => AssignPacketType(PacketTypePersonalCp, out packetType),
                "348" or "teamcp" or "team" => AssignPacketType(PacketTypeTeamCp, out packetType),
                "349" or "requestresult" or "requestok" => AssignPacketType(PacketTypeRequestResult, out packetType),
                "350" or "requestfailure" or "requestfail" or "fail" => AssignPacketType(PacketTypeRequestFailure, out packetType),
                "351" or "processfordeath" or "death" => AssignPacketType(PacketTypeProcessForDeath, out packetType),
                "352" or "showmemberoutmessage" or "memberout" => AssignPacketType(PacketTypeShowMemberOutMessage, out packetType),
                "353" or "gameresult" or "result" => AssignPacketType(PacketTypeGameResult, out packetType),
                _ => int.TryParse(normalized, out packetType)
            };
        }

        private static bool AssignPacketType(int value, out int packetType)
        {
            packetType = value;
            return true;
        }

        private static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                PacketTypeEnter => "enter (346)",
                PacketTypePersonalCp => "personalcp (347)",
                PacketTypeTeamCp => "teamcp (348)",
                PacketTypeRequestResult => "requestresult (349)",
                PacketTypeRequestFailure => "requestfailure (350)",
                PacketTypeProcessForDeath => "processfordeath (351)",
                PacketTypeShowMemberOutMessage => "memberout (352)",
                PacketTypeGameResult => "gameresult (353)",
                _ => packetType.ToString()
            };
        }

        private static string RemoveWhitespace(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            char[] buffer = new char[text.Length];
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (!char.IsWhiteSpace(ch))
                {
                    buffer[count++] = ch;
                }
            }

            return count == 0 ? string.Empty : new string(buffer, 0, count);
        }

        private void StopInternal(bool clearPending)
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

            _listenerCancellation?.Dispose();
            _listenerCancellation = null;
            _listener = null;
            _listenerTask = null;

            if (clearPending)
            {
                while (_pendingMessages.TryDequeue(out _))
                {
                }
            }
        }
    }
}
