using HaCreator.MapSimulator.Pools;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

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
    /// Adapter inbox for packet-shaped remote-user updates that feed the
    /// shared RemoteUserPacketCodec / MapSimulator.RemoteUsers seam.
    /// </summary>
    public sealed class RemoteUserPacketInboxManager : IDisposable
    {
        private readonly ConcurrentQueue<RemoteUserPacketInboxMessage> _pendingMessages = new();
        public string LastStatus { get; private set; } = "Remote user packet inbox ready for role-session/local ingress.";

        public bool TryDequeue(out RemoteUserPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void EnqueueLocal(int packetType, byte[] payload, string source)
        {
            string packetSource = string.IsNullOrWhiteSpace(source) ? "remote-user-local" : source;
            EnqueueMessage(
                new RemoteUserPacketInboxMessage(packetType, payload, packetSource, packetType.ToString(CultureInfo.InvariantCulture)),
                packetSource);
        }

        public void EnqueueProxy(int packetType, byte[] payload, string source, string rawText = null)
        {
            EnqueueMessage(
                new RemoteUserPacketInboxMessage(packetType, payload, source, rawText ?? packetType.ToString(CultureInfo.InvariantCulture)),
                source);
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
                "chat" or "onchat" => (int)RemoteUserPacketType.UserChat,
                "outsidechat" or "chatoutside" or "onchatoutside" => (int)RemoteUserPacketType.UserChatFromOutsideMap,
                "tutorhire" or "hiretutor" or "onhiretutor" => (int)RemoteUserPacketType.UserTutorHire,
                "tutormsg" or "tutormessage" or "ontutormsg" => (int)RemoteUserPacketType.UserTutorMessage,
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
                "movingshootprepare" or "movingshoot" or "movingprepare" => (int)RemoteUserPacketType.UserMovingShootAttackPrepareOfficial,
                "preparedclear" => (int)RemoteUserPacketType.UserPreparedSkillClear,
                "hit" => (int)RemoteUserPacketType.UserHitOfficial,
                "emotion" => (int)RemoteUserPacketType.UserEmotionOfficial,
                "activeeffect" or "activeeffectitem" or "setactiveeffectitem" => (int)RemoteUserPacketType.UserActiveEffectItemOfficial,
                "upgradetomb" or "showupgradetomb" => (int)RemoteUserPacketType.UserUpgradeTombOfficial,
                "officialchair" or "setactiveportablechair" => (int)RemoteUserPacketType.UserPortableChairOfficial,
                "usereffect" or "officialeffect" => (int)RemoteUserPacketType.UserEffectOfficial,
                "receivehp" or "partyhp" => (int)RemoteUserPacketType.UserReceiveHpOfficial,
                "guildname" or "guildnamechanged" => (int)RemoteUserPacketType.UserGuildNameChangedOfficial,
                "guildmark" or "guildmarkchanged" => (int)RemoteUserPacketType.UserGuildMarkChangedOfficial,
                "throwgrenade" or "grenade" => (int)RemoteUserPacketType.UserThrowGrenadeOfficial,
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
                (int)RemoteUserPacketType.UserHitOfficial => $"UserHitOfficial (0x{packetType:X})",
                (int)RemoteUserPacketType.UserEmotionOfficial => $"UserEmotionOfficial (0x{packetType:X})",
                (int)RemoteUserPacketType.UserActiveEffectItemOfficial => $"UserActiveEffectItemOfficial (0x{packetType:X})",
                (int)RemoteUserPacketType.UserUpgradeTombOfficial => $"UserUpgradeTombOfficial (0x{packetType:X})",
                (int)RemoteUserPacketType.UserPortableChairOfficial => $"UserPortableChairOfficial (0x{packetType:X})",
                (int)RemoteUserPacketType.UserEffectOfficial => $"UserEffectOfficial (0x{packetType:X})",
                (int)RemoteUserPacketType.UserReceiveHpOfficial => $"UserReceiveHpOfficial (0x{packetType:X})",
                (int)RemoteUserPacketType.UserGuildNameChangedOfficial => $"UserGuildNameChangedOfficial (0x{packetType:X})",
                (int)RemoteUserPacketType.UserGuildMarkChangedOfficial => $"UserGuildMarkChangedOfficial (0x{packetType:X})",
                (int)RemoteUserPacketType.UserThrowGrenadeOfficial => $"UserThrowGrenadeOfficial (0x{packetType:X})",
                (int)RemoteUserPacketType.UserChat => "UserChat (CUser::OnChat)",
                (int)RemoteUserPacketType.UserChatFromOutsideMap => "UserChatFromOutsideMap (CUser::OnChat)",
                (int)RemoteUserPacketType.UserTutorHire => "UserTutorHire (remote CTutor hire)",
                (int)RemoteUserPacketType.UserTutorMessage => "UserTutorMessage (remote CTutor message)",
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

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private void EnqueueMessage(RemoteUserPacketInboxMessage message, string sourceLabel)
        {
            if (message == null)
            {
                return;
            }

            _pendingMessages.Enqueue(message);
            string packetSource = string.IsNullOrWhiteSpace(sourceLabel) ? "remote-user-inbox" : sourceLabel;
            LastStatus = $"Queued {DescribePacketType(message.PacketType)} from {packetSource}.";
        }
    }
}

