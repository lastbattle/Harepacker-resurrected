using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Managers
{
    public enum WeddingInboxMessageKind
    {
        CoupleMove,
        CoupleAvatar,
        GuestAddClone,
        GuestAddAvatar,
        GuestMove,
        GuestRemove,
        GuestClear
    }

    public sealed class WeddingInboxMessage
    {
        public WeddingInboxMessage(
            WeddingInboxMessageKind kind,
            string actorKey,
            Vector2? position,
            bool? facingRight,
            string actionName,
            byte[] avatarLookPayload,
            string source,
            string rawText)
        {
            Kind = kind;
            ActorKey = actorKey?.Trim() ?? string.Empty;
            Position = position;
            FacingRight = facingRight;
            ActionName = actionName?.Trim();
            Payload = avatarLookPayload != null ? (byte[])avatarLookPayload.Clone() : Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "wedding-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public WeddingInboxMessageKind Kind { get; }
        public string ActorKey { get; }
        public Vector2? Position { get; }
        public bool? FacingRight { get; }
        public string ActionName { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    public sealed class WeddingPacketInboxManager : IDisposable
    {
        public const int DefaultPort = 18486;

        private readonly ConcurrentQueue<WeddingInboxMessage> _pendingMessages = new();
        private readonly object _listenerLock = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;

        public int Port { get; private set; } = DefaultPort;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Wedding inbox inactive.";

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Wedding inbox already listening on 127.0.0.1:{Port}.";
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
                    LastStatus = $"Wedding inbox listening on 127.0.0.1:{Port}.";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Wedding inbox failed to start: {ex.Message}";
                }
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal(clearPending: true);
                LastStatus = "Wedding inbox stopped.";
            }
        }

        public bool TryDequeue(out WeddingInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(WeddingInboxMessage message, bool success, string detail)
        {
            string label = DescribeMessage(message);
            string summary = string.IsNullOrWhiteSpace(detail) ? label : $"{label}: {detail}";
            LastStatus = success
                ? $"Applied {summary} from {message?.Source ?? "wedding-inbox"}."
                : $"Ignored {summary} from {message?.Source ?? "wedding-inbox"}.";
        }

        public void Dispose()
        {
            lock (_listenerLock)
            {
                StopInternal(clearPending: true);
            }
        }

        public static bool TryParseLine(string text, out WeddingInboxMessage message, out string error)
        {
            message = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Wedding inbox line is empty.";
                return false;
            }

            string[] tokens = text.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2)
            {
                error = "Wedding inbox line is missing a subject and action.";
                return false;
            }

            string subject = tokens[0].Trim().ToLowerInvariant();
            return subject switch
            {
                "actor" => TryParseCoupleLine(tokens, text, out message, out error),
                "guest" => TryParseGuestLine(tokens, text, out message, out error),
                _ => Fail($"Unsupported wedding inbox subject: {subject}", out message, out error)
            };
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
                LastStatus = $"Wedding inbox listener stopped: {ex.Message}";
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            using (var reader = new StreamReader(stream))
            {
                while (!cancellationToken.IsCancellationRequested && !reader.EndOfStream)
                {
                    string line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (!TryParseLine(line, out WeddingInboxMessage message, out string error))
                    {
                        LastStatus = error;
                        continue;
                    }

                    _pendingMessages.Enqueue(message);
                    ReceivedCount++;
                    LastStatus = $"Queued {DescribeMessage(message)} from wedding inbox.";
                }
            }
        }

        private void StopInternal(bool clearPending)
        {
            _listenerCancellation?.Cancel();
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;

            try
            {
                _listener?.Stop();
            }
            catch
            {
            }

            _listener = null;
            _listenerTask = null;

            if (clearPending)
            {
                while (_pendingMessages.TryDequeue(out _))
                {
                }

                ReceivedCount = 0;
            }
        }

        private static bool TryParseCoupleLine(string[] tokens, string rawText, out WeddingInboxMessage message, out string error)
        {
            message = null;
            error = null;
            if (tokens.Length < 4)
            {
                error = "Wedding actor inbox line requires '<groom|bride> <x> <y>' or 'avatar <groom|bride> <x> <y> <avatarLookHex>'.";
                return false;
            }

            string action = tokens[1].Trim().ToLowerInvariant();
            if (action == "avatar")
            {
                if (tokens.Length < 6)
                {
                    error = "Wedding actor avatar inbox line requires '<groom|bride> <x> <y> <avatarLookHex>'.";
                    return false;
                }

                if (!TryParseCoupleKey(tokens[2], out string actorKey, out error)
                    || !TryParsePosition(tokens[3], tokens[4], out Vector2 position, out error)
                    || !TryParseAvatarPayload(tokens[5], out byte[] payload, out error)
                    || !TryParseOptionalActionAndFacing(tokens, 6, out string actionName, out bool? facingRight, out error))
                {
                    return false;
                }

                message = new WeddingInboxMessage(WeddingInboxMessageKind.CoupleAvatar, actorKey, position, facingRight, actionName, payload, "wedding-inbox", rawText);
                return true;
            }

            if (!TryParseCoupleKey(tokens[1], out string moveActorKey, out error)
                || !TryParsePosition(tokens[2], tokens[3], out Vector2 movePosition, out error)
                || !TryParseOptionalActionAndFacing(tokens, 4, out string moveActionName, out bool? moveFacingRight, out error))
            {
                return false;
            }

            message = new WeddingInboxMessage(WeddingInboxMessageKind.CoupleMove, moveActorKey, movePosition, moveFacingRight, moveActionName, null, "wedding-inbox", rawText);
            return true;
        }

        private static bool TryParseGuestLine(string[] tokens, string rawText, out WeddingInboxMessage message, out string error)
        {
            message = null;
            error = null;
            string action = tokens[1].Trim().ToLowerInvariant();
            switch (action)
            {
                case "add":
                    return TryParseGuestPlacement(WeddingInboxMessageKind.GuestAddClone, tokens, rawText, 2, expectAvatarLookPayload: false, out message, out error);
                case "avatar":
                    return TryParseGuestPlacement(WeddingInboxMessageKind.GuestAddAvatar, tokens, rawText, 2, expectAvatarLookPayload: true, out message, out error);
                case "move":
                    return TryParseGuestPlacement(WeddingInboxMessageKind.GuestMove, tokens, rawText, 2, expectAvatarLookPayload: false, out message, out error);
                case "remove":
                    if (tokens.Length < 3 || string.IsNullOrWhiteSpace(tokens[2]))
                    {
                        error = "Wedding guest remove inbox line requires a name.";
                        return false;
                    }

                    message = new WeddingInboxMessage(WeddingInboxMessageKind.GuestRemove, tokens[2], null, null, null, null, "wedding-inbox", rawText);
                    return true;
                case "clear":
                    message = new WeddingInboxMessage(WeddingInboxMessageKind.GuestClear, null, null, null, null, null, "wedding-inbox", rawText);
                    return true;
                default:
                    error = $"Unsupported wedding guest inbox action: {action}";
                    return false;
            }
        }

        private static bool TryParseGuestPlacement(
            WeddingInboxMessageKind kind,
            string[] tokens,
            string rawText,
            int argumentIndex,
            bool expectAvatarLookPayload,
            out WeddingInboxMessage message,
            out string error)
        {
            message = null;
            error = null;

            int minimumTokenCount = expectAvatarLookPayload ? argumentIndex + 4 : argumentIndex + 3;
            if (tokens.Length < minimumTokenCount)
            {
                error = expectAvatarLookPayload
                    ? "Wedding guest avatar inbox line requires '<name> <x> <y> <avatarLookHex>'."
                    : "Wedding guest inbox line requires '<name> <x> <y>'.";
                return false;
            }

            string actorKey = tokens[argumentIndex];
            if (string.IsNullOrWhiteSpace(actorKey))
            {
                error = "Wedding guest inbox line requires a non-empty name.";
                return false;
            }

            if (!TryParsePosition(tokens[argumentIndex + 1], tokens[argumentIndex + 2], out Vector2 position, out error))
            {
                return false;
            }

            byte[] payload = null;
            int optionalStartIndex = argumentIndex + 3;
            if (expectAvatarLookPayload)
            {
                if (!TryParseAvatarPayload(tokens[argumentIndex + 3], out payload, out error))
                {
                    return false;
                }

                optionalStartIndex++;
            }

            if (!TryParseOptionalActionAndFacing(tokens, optionalStartIndex, out string actionName, out bool? facingRight, out error))
            {
                return false;
            }

            message = new WeddingInboxMessage(kind, actorKey, position, facingRight, actionName, payload, "wedding-inbox", rawText);
            return true;
        }

        private static bool TryParseCoupleKey(string token, out string actorKey, out string error)
        {
            actorKey = token?.Trim().ToLowerInvariant();
            error = null;
            if (actorKey == "groom" || actorKey == "bride")
            {
                return true;
            }

            error = "Wedding actor must be groom or bride.";
            return false;
        }

        private static bool TryParsePosition(string xToken, string yToken, out Vector2 position, out string error)
        {
            position = Vector2.Zero;
            error = null;
            if (!TryParseCoordinate(xToken, out float x) || !TryParseCoordinate(yToken, out float y))
            {
                error = "Wedding actor position requires numeric <x> <y> world coordinates.";
                return false;
            }

            position = new Vector2(x, y);
            return true;
        }

        private static bool TryParseAvatarPayload(string token, out byte[] payload, out string error)
        {
            payload = null;
            error = null;
            string compactHex = RemoveWhitespace(token);
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
                error = $"Invalid wedding AvatarLook hex payload: {token}";
                return false;
            }
        }

        private static bool TryParseOptionalActionAndFacing(string[] tokens, int startIndex, out string actionName, out bool? facingRight, out string error)
        {
            actionName = null;
            facingRight = null;
            error = null;

            for (int i = startIndex; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                if (string.Equals(token, "left", StringComparison.OrdinalIgnoreCase))
                {
                    facingRight = false;
                    continue;
                }

                if (string.Equals(token, "right", StringComparison.OrdinalIgnoreCase))
                {
                    facingRight = true;
                    continue;
                }

                if (actionName == null)
                {
                    actionName = token.Trim();
                    continue;
                }

                error = $"Unexpected wedding actor token '{token}'.";
                return false;
            }

            return true;
        }

        private static bool TryParseCoordinate(string token, out float value)
        {
            return float.TryParse(token, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value)
                || float.TryParse(token, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out value);
        }

        private static string RemoveWhitespace(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            Span<char> buffer = stackalloc char[text.Length];
            int count = 0;
            foreach (char character in text)
            {
                if (!char.IsWhiteSpace(character))
                {
                    buffer[count++] = character;
                }
            }

            return new string(buffer[..count]);
        }

        private static string DescribeMessage(WeddingInboxMessage message)
        {
            if (message == null)
            {
                return "Wedding inbox message";
            }

            return message.Kind switch
            {
                WeddingInboxMessageKind.CoupleMove => $"actor {message.ActorKey}",
                WeddingInboxMessageKind.CoupleAvatar => $"actor avatar {message.ActorKey}",
                WeddingInboxMessageKind.GuestAddClone => $"guest add '{message.ActorKey}'",
                WeddingInboxMessageKind.GuestAddAvatar => $"guest avatar '{message.ActorKey}'",
                WeddingInboxMessageKind.GuestMove => $"guest move '{message.ActorKey}'",
                WeddingInboxMessageKind.GuestRemove => $"guest remove '{message.ActorKey}'",
                WeddingInboxMessageKind.GuestClear => "guest clear",
                _ => "Wedding inbox message"
            };
        }

        private static bool Fail(string message, out WeddingInboxMessage parsedMessage, out string error)
        {
            parsedMessage = null;
            error = message;
            return false;
        }
    }
}
