using HaCreator.MapSimulator.Pools;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class RemoteUserPacketInboxMessage
    {
        public RemoteUserPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            PacketType = packetType;
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "remote-user-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    /// <summary>
    /// Loopback inbox for packet-shaped remote-user updates that feed the
    /// shared RemoteUserPacketCodec / MapSimulator.RemoteUsers seam.
    /// </summary>
    public sealed class RemoteUserPacketInboxManager : IDisposable
    {
        public const int DefaultPort = 18488;

        private readonly ConcurrentQueue<RemoteUserPacketInboxMessage> _pendingMessages = new();
        private readonly object _listenerLock = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;

        public int Port { get; private set; } = DefaultPort;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Remote user packet inbox inactive.";

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Remote user packet inbox already listening on 127.0.0.1:{Port}.";
                    return;
                }

                StopInternal();
                Port = port <= 0 ? DefaultPort : port;
                _listenerCancellation = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
                _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                LastStatus = $"Remote user packet inbox listening on 127.0.0.1:{Port}.";
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal();
                LastStatus = "Remote user packet inbox stopped.";
            }
        }

        public bool TryDequeue(out RemoteUserPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(RemoteUserPacketInboxMessage message, bool success, string detail)
        {
            string summary = DescribePacketType(message?.PacketType ?? 0);
            LastStatus = success
                ? $"Applied {summary} from {message?.Source ?? "remote-user-inbox"}."
                : $"Ignored {summary} from {message?.Source ?? "remote-user-inbox"}: {detail}";
        }

        public static bool TryParseLine(string text, out RemoteUserPacketInboxMessage message, out string error)
        {
            message = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Remote user inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith("/remoteuserpacket", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["/remoteuserpacket".Length..].TrimStart();
            }

            if (trimmed.Length == 0)
            {
                error = "Remote user inbox line is empty.";
                return false;
            }

            int splitIndex = FindTokenSeparatorIndex(trimmed);
            string packetToken = splitIndex >= 0 ? trimmed[..splitIndex].Trim() : trimmed;
            string payloadToken = splitIndex >= 0 ? trimmed[(splitIndex + 1)..].Trim() : string.Empty;

            if (!TryParsePacketType(packetToken, out int packetType))
            {
                error = $"Unsupported remote user packet '{packetToken}'.";
                return false;
            }

            byte[] payload = Array.Empty<byte>();
            if (!string.IsNullOrWhiteSpace(payloadToken) && !TryParsePayload(payloadToken, out payload, out error))
            {
                return false;
            }

            message = new RemoteUserPacketInboxMessage(packetType, payload, "remote-user-inbox", text);
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
                    && Enum.IsDefined(typeof(RemoteUserPacketType), packetType);
            }

            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out packetType))
            {
                return Enum.IsDefined(typeof(RemoteUserPacketType), packetType);
            }

            packetType = trimmed.ToLowerInvariant() switch
            {
                "coupleadd" => (int)RemoteUserPacketType.UserCoupleRecordAdd,
                "coupleremove" => (int)RemoteUserPacketType.UserCoupleRecordRemove,
                "friendadd" => (int)RemoteUserPacketType.UserFriendRecordAdd,
                "friendremove" => (int)RemoteUserPacketType.UserFriendRecordRemove,
                "marriageadd" => (int)RemoteUserPacketType.UserMarriageRecordAdd,
                "marriageremove" => (int)RemoteUserPacketType.UserMarriageRecordRemove,
                "newyearadd" => (int)RemoteUserPacketType.UserNewYearCardRecordAdd,
                "newyearremove" => (int)RemoteUserPacketType.UserNewYearCardRecordRemove,
                "couplechairadd" => (int)RemoteUserPacketType.UserCoupleChairRecordAdd,
                "couplechairremove" => (int)RemoteUserPacketType.UserCoupleChairRecordRemove,
                "enter" => (int)RemoteUserPacketType.UserEnterField,
                "leave" => (int)RemoteUserPacketType.UserLeaveField,
                "move" => (int)RemoteUserPacketType.UserMove,
                "state" => (int)RemoteUserPacketType.UserMoveAction,
                "helper" => (int)RemoteUserPacketType.UserHelper,
                "team" => (int)RemoteUserPacketType.UserBattlefieldTeam,
                "follow" => (int)RemoteUserPacketType.UserFollowCharacter,
                "couplerecordadd" or "coupleadd" => (int)RemoteUserPacketType.UserCoupleRecordAdd,
                "couplerecordremove" or "coupleremove" => (int)RemoteUserPacketType.UserCoupleRecordRemove,
                "friendrecordadd" or "friendadd" => (int)RemoteUserPacketType.UserFriendRecordAdd,
                "friendrecordremove" or "friendremove" => (int)RemoteUserPacketType.UserFriendRecordRemove,
                "marriagerecordadd" or "marriageadd" => (int)RemoteUserPacketType.UserMarriageRecordAdd,
                "marriagerecordremove" or "marriageremove" => (int)RemoteUserPacketType.UserMarriageRecordRemove,
                "newyearcardrecordadd" or "newyearadd" => (int)RemoteUserPacketType.UserNewYearCardRecordAdd,
                "newyearcardrecordremove" or "newyearremove" => (int)RemoteUserPacketType.UserNewYearCardRecordRemove,
                "couplechairrecordadd" or "couplechairadd" => (int)RemoteUserPacketType.UserCoupleChairRecordAdd,
                "couplechairrecordremove" or "couplechairremove" => (int)RemoteUserPacketType.UserCoupleChairRecordRemove,
                "chair" => (int)RemoteUserPacketType.UserPortableChair,
                "mount" => (int)RemoteUserPacketType.UserMount,
                "prepare" => (int)RemoteUserPacketType.UserPreparedSkill,
                "preparedclear" => (int)RemoteUserPacketType.UserPreparedSkillClear,
                "emotion" => (int)RemoteUserPacketType.UserEmotionOfficial,
                "activeeffect" or "activeeffectitem" or "setactiveeffectitem" => (int)RemoteUserPacketType.UserActiveEffectItemOfficial,
                "officialchair" or "setactiveportablechair" => (int)RemoteUserPacketType.UserPortableChairOfficial,
                "pickup" or "droppickup" => (int)RemoteUserPacketType.UserDropPickup,
                "melee" or "attack" or "meleeattack" => (int)RemoteUserPacketType.UserMeleeAttack,
                "effect" or "itemeffect" or "ringeffect" => (int)RemoteUserPacketType.UserItemEffect,
                "avatarmodified" or "avatarmod" or "look" => (int)RemoteUserPacketType.UserAvatarModified,
                "tempset" or "tempstatset" => (int)RemoteUserPacketType.UserTemporaryStatSet,
                "tempreset" or "tempstatreset" => (int)RemoteUserPacketType.UserTemporaryStatReset,
                _ => 0
            };

            return packetType != 0;
        }

        public static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                (int)RemoteUserPacketType.UserEmotionOfficial => $"UserEmotionOfficial (0x{packetType:X})",
                (int)RemoteUserPacketType.UserActiveEffectItemOfficial => $"UserActiveEffectItemOfficial (0x{packetType:X})",
                (int)RemoteUserPacketType.UserPortableChairOfficial => $"UserPortableChairOfficial (0x{packetType:X})",
                _ => Enum.IsDefined(typeof(RemoteUserPacketType), packetType)
                    ? $"{(RemoteUserPacketType)packetType} (0x{packetType:X})"
                    : $"packet {packetType}"
            };
        }

        public static byte[] BuildDropPickupPayload(int dropId, int actorId, DropPickupActorKind actorKind, string actorName)
        {
            byte[] actorNameBytes = string.IsNullOrWhiteSpace(actorName)
                ? Array.Empty<byte>()
                : Encoding.UTF8.GetBytes(actorName.Trim());
            if (actorNameBytes.Length > byte.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(actorName), "Remote user actor names must fit in an 8-bit packet string.");
            }

            byte[] payload = new byte[4 + 4 + 1 + 1 + actorNameBytes.Length];
            WriteInt32(payload, 0, dropId);
            WriteInt32(payload, 4, actorId);
            payload[8] = (byte)actorKind;
            payload[9] = (byte)actorNameBytes.Length;
            if (actorNameBytes.Length > 0)
            {
                Buffer.BlockCopy(actorNameBytes, 0, payload, 10, actorNameBytes.Length);
            }

            return payload;
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
                LastStatus = $"Remote user packet inbox error: {ex.Message}";
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

                        if (!TryParseLine(line, out RemoteUserPacketInboxMessage message, out string error))
                        {
                            LastStatus = $"Ignored remote user inbox line from {remoteEndpoint}: {error}";
                            continue;
                        }

                        _pendingMessages.Enqueue(new RemoteUserPacketInboxMessage(message.PacketType, message.Payload, remoteEndpoint, line));
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
                LastStatus = $"Remote user packet inbox client error: {ex.Message}";
            }
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

        private static bool TryParsePayload(string token, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = null;

            if (string.IsNullOrWhiteSpace(token))
            {
                return true;
            }

            string trimmed = token.Trim();
            if (trimmed.StartsWith("payloadhex=", StringComparison.OrdinalIgnoreCase))
            {
                return TryDecodeHex(trimmed["payloadhex=".Length..], out payload, out error);
            }

            if (trimmed.StartsWith("payloadb64=", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    payload = Convert.FromBase64String(trimmed["payloadb64=".Length..]);
                    return true;
                }
                catch (FormatException ex)
                {
                    error = $"Remote user packet payload base64 is invalid: {ex.Message}";
                    return false;
                }
            }

            error = "Remote user payload must use payloadhex=.. or payloadb64=..";
            return false;
        }

        private static bool TryDecodeHex(string text, out byte[] bytes, out string error)
        {
            bytes = Array.Empty<byte>();
            error = null;
            if (text == null)
            {
                error = "Remote user packet payload hex is missing.";
                return false;
            }

            string normalized = text.Replace(" ", string.Empty, StringComparison.Ordinal);
            if (normalized.Length == 0)
            {
                bytes = Array.Empty<byte>();
                return true;
            }

            if ((normalized.Length & 1) != 0)
            {
                error = "Remote user packet payload hex must contain an even number of characters.";
                return false;
            }

            bytes = new byte[normalized.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (!byte.TryParse(normalized.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[i]))
                {
                    error = $"Remote user packet payload hex contains an invalid byte at offset {i * 2}.";
                    bytes = Array.Empty<byte>();
                    return false;
                }
            }

            return true;
        }

        private void StopInternal()
        {
            _listenerCancellation?.Cancel();
            _listener?.Stop();
            _listener?.Server?.Dispose();
            _listenerTask = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;
            _listener = null;
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }
    }
}
