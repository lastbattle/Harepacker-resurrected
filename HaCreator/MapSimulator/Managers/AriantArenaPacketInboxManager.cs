using System;
using System.Collections.Concurrent;
using System.Globalization;
using HaCreator.MapSimulator.Fields;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Managers
{
    public enum AriantArenaInboxMessageKind
    {
        Packet,
        ActorAddClone,
        ActorAddAvatar,
        ActorMove,
        ActorRemove,
        ActorClear
    }

    public sealed class AriantArenaPacketInboxMessage
    {
        public AriantArenaPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            Kind = AriantArenaInboxMessageKind.Packet;
            PacketType = packetType;
            Payload = payload != null ? (byte[])payload.Clone() : Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "ariant-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public AriantArenaPacketInboxMessage(
            AriantArenaInboxMessageKind kind,
            string actorName,
            Vector2? position,
            bool? facingRight,
            string actionName,
            byte[] avatarLookPayload,
            string source,
            string rawText)
        {
            Kind = kind;
            ActorName = actorName?.Trim() ?? string.Empty;
            Position = position;
            FacingRight = facingRight;
            ActionName = actionName?.Trim();
            Payload = avatarLookPayload != null ? (byte[])avatarLookPayload.Clone() : Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "ariant-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public AriantArenaInboxMessageKind Kind { get; }
        public int PacketType { get; }
        public byte[] Payload { get; }
        public string ActorName { get; }
        public Vector2? Position { get; }
        public bool? FacingRight { get; }
        public string ActionName { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    /// <summary>
    /// Adapter inbox for live Ariant Arena field packets.
    /// Each line is encoded as either "<type> <hex-payload>" for Ariant field packets
    /// or "actor <add|avatar|move|remove|clear> ..." for remote overlay updates.
    /// </summary>
    public sealed class AriantArenaPacketInboxManager : IDisposable
    {
        private const int PacketTypeUserEnterField = 179;
        private const int PacketTypeUserLeaveField = 180;
        private const int PacketTypeUserMove = 210;
        private const int PacketTypeMeleeAttack1 = 211;
        private const int PacketTypeMeleeAttack2 = 212;
        private const int PacketTypeMeleeAttack3 = 213;
        private const int PacketTypeMeleeAttack4 = 214;
        private const int PacketTypeSkillPrepare = 215;
        private const int PacketTypeMovingShootAttackPrepare = 216;
        private const int PacketTypeSkillCancel = 217;
        private const int PacketTypeHit = 218;
        private const int PacketTypeEmotion = 219;
        private const int PacketTypeSetActiveEffectItem = 220;
        private const int PacketTypeUpgradeTombEffect = 221;
        private const int PacketTypeSetActivePortableChair = 222;
        private const int PacketTypeAvatarModified = 223;
        private const int PacketTypeEffect = 224;
        private const int PacketTypeTemporaryStatSet = 225;
        private const int PacketTypeTemporaryStatReset = 226;
        private const int PacketTypeReceiveHp = 227;
        private const int PacketTypeGuildNameChanged = 228;
        private const int PacketTypeGuildMarkChanged = 229;
        private const int PacketTypeThrowGrenade = 230;
        private const int PacketTypeShowResult = 171;
        private const int PacketTypeUserScore = 354;

        private readonly ConcurrentQueue<AriantArenaPacketInboxMessage> _pendingMessages = new();
        public string LastStatus { get; private set; } = "Ariant Arena packet inbox ready for role-session/local ingress.";

        public void EnqueueLocal(int packetType, byte[] payload, string source)
        {
            EnqueueMessage(
                new AriantArenaPacketInboxMessage(packetType, payload, source, $"{packetType}"),
                source);
        }

        public void EnqueueProxy(int packetType, byte[] payload, string source)
        {
            EnqueueMessage(
                new AriantArenaPacketInboxMessage(packetType, payload, source, $"{packetType}"),
                source);
        }

        public bool TryDequeue(out AriantArenaPacketInboxMessage message)
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

        public void RecordDispatchResult(AriantArenaPacketInboxMessage message, bool success, string detail)
        {
            string label = DescribeMessage(message);
            string summary = string.IsNullOrWhiteSpace(detail) ? label : $"{label}: {detail}";
            LastStatus = success
                ? $"Applied {summary} from {message?.Source ?? "ariant-inbox"}."
                : $"Ignored {summary} from {message?.Source ?? "ariant-inbox"}.";
        }

        public void Dispose()
        {
        }

        public static bool TryParsePacketLine(string text, out int packetType, out byte[] payload, out string error)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Ariant inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            int separatorIndex = trimmed.IndexOfAny(new[] { ' ', '\t' });
            string typeToken = separatorIndex >= 0 ? trimmed[..separatorIndex] : trimmed;
            string payloadToken = separatorIndex >= 0 ? trimmed[(separatorIndex + 1)..].Trim() : string.Empty;

            if (typeToken.Equals("packetraw", StringComparison.OrdinalIgnoreCase)
                || typeToken.Equals("wrapped", StringComparison.OrdinalIgnoreCase)
                || typeToken.Equals("opcode", StringComparison.OrdinalIgnoreCase))
            {
                return TryParsePacketRawLine(payloadToken, out packetType, out payload, out error);
            }

            if (!TryParsePacketType(typeToken, out packetType))
            {
                error = $"Unsupported Ariant packet type: {typeToken}";
                return false;
            }

            if (packetType == SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode)
            {
                if (!TryParseRelayPayload(payloadToken, out payload, out error))
                {
                    packetType = 0;
                    return false;
                }

                return true;
            }

            if (packetType == PacketTypeShowResult)
            {
                payload = Array.Empty<byte>();
                return true;
            }

            string compactHex = RemoveWhitespace(payloadToken);
            if (string.IsNullOrWhiteSpace(compactHex))
            {
                error = "Ariant score packet requires a hex payload.";
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
                error = $"Invalid Ariant packet hex payload: {payloadToken}";
                return false;
            }
        }

        private static bool TryParsePacketRawLine(string payloadToken, out int packetType, out byte[] payload, out string error)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            error = null;

            string compactHex = RemoveWhitespace(payloadToken);
            if (string.IsNullOrWhiteSpace(compactHex))
            {
                error = "Ariant packetraw requires an opcode-wrapped hex payload.";
                return false;
            }

            if (compactHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                compactHex = compactHex[2..];
            }

            byte[] packetBytes;
            try
            {
                packetBytes = Convert.FromHexString(compactHex);
            }
            catch (FormatException)
            {
                error = $"Invalid Ariant packetraw hex payload: {payloadToken}";
                return false;
            }

            if (packetBytes.Length < sizeof(ushort))
            {
                error = "Ariant packetraw payload is too short.";
                return false;
            }

            packetType = BitConverter.ToUInt16(packetBytes, 0);
            payload = packetBytes.Length == sizeof(ushort)
                ? Array.Empty<byte>()
                : packetBytes.AsSpan(sizeof(ushort)).ToArray();

            if (packetType == SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode)
            {
                if (!TryValidateRelayPayload(payload, out error))
                {
                    packetType = 0;
                    payload = Array.Empty<byte>();
                    return false;
                }

                return true;
            }

            if (!IsSupportedRelayPacketType(packetType))
            {
                error = $"Ariant packetraw payload does not contain a supported opcode {packetType} packet.";
                packetType = 0;
                payload = Array.Empty<byte>();
                return false;
            }

            payload = SpecialFieldRuntimeCoordinator.BuildCurrentWrapperRelayPayload(packetType, payload);
            packetType = SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode;
            return true;
        }

        private static bool TryParseRelayPayload(string payloadToken, out byte[] relayPayload, out string error)
        {
            relayPayload = Array.Empty<byte>();
            error = null;

            string compactHex = RemoveWhitespace(payloadToken);
            if (string.IsNullOrWhiteSpace(compactHex))
            {
                error =
                    $"Ariant packet {SpecialFieldRuntimeCoordinator.CurrentWrapperRelayOpcode} requires a current-wrapper relay payload.";
                return false;
            }

            if (compactHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                compactHex = compactHex[2..];
            }

            try
            {
                relayPayload = Convert.FromHexString(compactHex);
            }
            catch (FormatException)
            {
                error = $"Invalid Ariant relay packet hex payload: {payloadToken}";
                return false;
            }

            return TryValidateRelayPayload(relayPayload, out error);
        }

        private static bool TryValidateRelayPayload(byte[] relayPayload, out string error)
        {
            if (!SpecialFieldRuntimeCoordinator.TryDecodeCurrentWrapperRelayPayload(
                    relayPayload,
                    out int wrapperPacketType,
                    out _,
                    out error))
            {
                return false;
            }

            if (!IsSupportedRelayPacketType(wrapperPacketType))
            {
                error = $"Unsupported Ariant current-wrapper relay packet opcode: {wrapperPacketType}";
                return false;
            }

            return true;
        }

        private static bool IsSupportedRelayPacketType(int packetType)
        {
            return packetType == PacketTypeShowResult
                || packetType == PacketTypeUserEnterField
                || packetType == PacketTypeUserLeaveField
                || packetType == PacketTypeUserMove
                || packetType == PacketTypeMeleeAttack1
                || packetType == PacketTypeMeleeAttack2
                || packetType == PacketTypeMeleeAttack3
                || packetType == PacketTypeMeleeAttack4
                || packetType == PacketTypeSkillPrepare
                || packetType == PacketTypeMovingShootAttackPrepare
                || packetType == PacketTypeSkillCancel
                || packetType == PacketTypeHit
                || packetType == PacketTypeEmotion
                || packetType == PacketTypeSetActiveEffectItem
                || packetType == PacketTypeUpgradeTombEffect
                || packetType == PacketTypeSetActivePortableChair
                || packetType == PacketTypeAvatarModified
                || packetType == PacketTypeEffect
                || packetType == PacketTypeTemporaryStatSet
                || packetType == PacketTypeTemporaryStatReset
                || packetType == PacketTypeReceiveHp
                || packetType == PacketTypeGuildNameChanged
                || packetType == PacketTypeGuildMarkChanged
                || packetType == PacketTypeThrowGrenade
                || packetType == PacketTypeUserScore;
        }

        public static bool TryParseLine(string text, out AriantArenaPacketInboxMessage message, out string error)
        {
            message = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Ariant inbox line is empty.";
                return false;
            }

            string[] tokens = text.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                error = "Ariant inbox line is empty.";
                return false;
            }

            string firstToken = tokens[0].Trim().ToLowerInvariant();
            if (firstToken == "actor" || firstToken.StartsWith("actor", StringComparison.Ordinal))
            {
                return TryParseActorLine(tokens, text, out message, out error);
            }

            if (!TryParsePacketLine(text, out int packetType, out byte[] payload, out error))
            {
                return false;
            }

            message = new AriantArenaPacketInboxMessage(packetType, payload, "ariant-inbox", text);
            return true;
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
                "171" or "showresult" or "result" => AssignPacketType(PacketTypeShowResult, out packetType),
                "179" or "userenter" or "spawn" => AssignPacketType(PacketTypeUserEnterField, out packetType),
                "180" or "userleave" or "despawn" => AssignPacketType(PacketTypeUserLeaveField, out packetType),
                "210" or "usermove" or "move" => AssignPacketType(PacketTypeUserMove, out packetType),
                "211" or "melee1" => AssignPacketType(PacketTypeMeleeAttack1, out packetType),
                "212" or "melee2" => AssignPacketType(PacketTypeMeleeAttack2, out packetType),
                "213" or "melee3" => AssignPacketType(PacketTypeMeleeAttack3, out packetType),
                "214" or "melee4" or "melee" or "attack" => AssignPacketType(PacketTypeMeleeAttack4, out packetType),
                "215" or "prepare" or "skillprepare" => AssignPacketType(PacketTypeSkillPrepare, out packetType),
                "216" or "movingshootprepare" or "movingshoot" or "shootprepare" => AssignPacketType(PacketTypeMovingShootAttackPrepare, out packetType),
                "217" or "prepareclear" or "preparedclear" or "skillcancel" => AssignPacketType(PacketTypeSkillCancel, out packetType),
                "218" or "hit" => AssignPacketType(PacketTypeHit, out packetType),
                "219" or "emotion" => AssignPacketType(PacketTypeEmotion, out packetType),
                "220" or "activeeffect" or "activeeffectitem" or "setactiveeffectitem" => AssignPacketType(PacketTypeSetActiveEffectItem, out packetType),
                "221" or "upgradetomb" or "tomb" => AssignPacketType(PacketTypeUpgradeTombEffect, out packetType),
                "222" or "chair" or "setchair" => AssignPacketType(PacketTypeSetActivePortableChair, out packetType),
                "223" or "avatarmod" or "avatarmodified" or "look" => AssignPacketType(PacketTypeAvatarModified, out packetType),
                "224" or "usereffect" or "officialeffect" => AssignPacketType(PacketTypeEffect, out packetType),
                "225" or "tempset" or "tempstatset" or "temporarystatset" => AssignPacketType(PacketTypeTemporaryStatSet, out packetType),
                "226" or "tempreset" or "tempstatreset" or "temporarystatreset" => AssignPacketType(PacketTypeTemporaryStatReset, out packetType),
                "227" or "receivehp" or "hp" => AssignPacketType(PacketTypeReceiveHp, out packetType),
                "228" or "guildname" or "guildnamechanged" => AssignPacketType(PacketTypeGuildNameChanged, out packetType),
                "229" or "guildmark" or "guildmarkchanged" => AssignPacketType(PacketTypeGuildMarkChanged, out packetType),
                "230" or "throwgrenade" or "grenade" => AssignPacketType(PacketTypeThrowGrenade, out packetType),
                "354" or "userscore" or "score" => AssignPacketType(PacketTypeUserScore, out packetType),
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
                PacketTypeShowResult => "showresult (171)",
                PacketTypeUserEnterField => "userenter (179)",
                PacketTypeUserLeaveField => "userleave (180)",
                PacketTypeUserMove => "usermove (210)",
                PacketTypeMeleeAttack1 => "melee1 (211)",
                PacketTypeMeleeAttack2 => "melee2 (212)",
                PacketTypeMeleeAttack3 => "melee3 (213)",
                PacketTypeMeleeAttack4 => "melee4 (214)",
                PacketTypeSkillPrepare => "skillprepare (215)",
                PacketTypeMovingShootAttackPrepare => "movingshootprepare (216)",
                PacketTypeSkillCancel => "skillcancel (217)",
                PacketTypeHit => "hit (218)",
                PacketTypeEmotion => "emotion (219)",
                PacketTypeSetActiveEffectItem => "setactiveeffectitem (220)",
                PacketTypeUpgradeTombEffect => "upgradetomb (221)",
                PacketTypeSetActivePortableChair => "chair (222)",
                PacketTypeAvatarModified => "avatarmodified (223)",
                PacketTypeEffect => "usereffect (224)",
                PacketTypeTemporaryStatSet => "tempstatset (225)",
                PacketTypeTemporaryStatReset => "tempstatreset (226)",
                PacketTypeReceiveHp => "receivehp (227)",
                PacketTypeGuildNameChanged => "guildname (228)",
                PacketTypeGuildMarkChanged => "guildmark (229)",
                PacketTypeThrowGrenade => "throwgrenade (230)",
                PacketTypeUserScore => "userscore (354)",
                _ => packetType.ToString()
            };
        }

        private static string DescribeMessage(AriantArenaPacketInboxMessage message)
        {
            if (message == null)
            {
                return "Ariant inbox message";
            }

            return message.Kind switch
            {
                AriantArenaInboxMessageKind.Packet => DescribePacketType(message.PacketType),
                AriantArenaInboxMessageKind.ActorAddClone => $"actor add '{message.ActorName}'",
                AriantArenaInboxMessageKind.ActorAddAvatar => $"actor avatar '{message.ActorName}'",
                AriantArenaInboxMessageKind.ActorMove => $"actor move '{message.ActorName}'",
                AriantArenaInboxMessageKind.ActorRemove => $"actor remove '{message.ActorName}'",
                AriantArenaInboxMessageKind.ActorClear => "actor clear",
                _ => "Ariant inbox message"
            };
        }

        private static bool TryParseActorLine(string[] tokens, string rawText, out AriantArenaPacketInboxMessage message, out string error)
        {
            message = null;
            error = null;

            int commandIndex = 0;
            string normalized = tokens[0].Trim().ToLowerInvariant();
            string command;
            if (normalized == "actor")
            {
                if (tokens.Length < 2)
                {
                    error = "Ariant actor inbox line is missing an action.";
                    return false;
                }

                commandIndex = 1;
                command = tokens[1].Trim().ToLowerInvariant();
            }
            else
            {
                command = normalized["actor".Length..];
            }

            int argumentIndex = commandIndex + 1;
            switch (command)
            {
                case "add":
                    return TryParseActorPlacementMessage(
                        AriantArenaInboxMessageKind.ActorAddClone,
                        tokens,
                        rawText,
                        argumentIndex,
                        expectAvatarLookPayload: false,
                        out message,
                        out error);

                case "avatar":
                    return TryParseActorPlacementMessage(
                        AriantArenaInboxMessageKind.ActorAddAvatar,
                        tokens,
                        rawText,
                        argumentIndex,
                        expectAvatarLookPayload: true,
                        out message,
                        out error);

                case "move":
                    return TryParseActorPlacementMessage(
                        AriantArenaInboxMessageKind.ActorMove,
                        tokens,
                        rawText,
                        argumentIndex,
                        expectAvatarLookPayload: false,
                        out message,
                        out error);

                case "remove":
                    if (tokens.Length <= argumentIndex || string.IsNullOrWhiteSpace(tokens[argumentIndex]))
                    {
                        error = "Ariant actor remove inbox line requires a name.";
                        return false;
                    }

                    message = new AriantArenaPacketInboxMessage(
                        AriantArenaInboxMessageKind.ActorRemove,
                        tokens[argumentIndex],
                        position: null,
                        facingRight: null,
                        actionName: null,
                        avatarLookPayload: null,
                        source: "ariant-inbox",
                        rawText);
                    return true;

                case "clear":
                    message = new AriantArenaPacketInboxMessage(
                        AriantArenaInboxMessageKind.ActorClear,
                        actorName: null,
                        position: null,
                        facingRight: null,
                        actionName: null,
                        avatarLookPayload: null,
                        source: "ariant-inbox",
                        rawText);
                    return true;

                default:
                    error = $"Unsupported Ariant actor inbox action: {command}";
                    return false;
            }
        }

        private static bool TryParseActorPlacementMessage(
            AriantArenaInboxMessageKind kind,
            string[] tokens,
            string rawText,
            int argumentIndex,
            bool expectAvatarLookPayload,
            out AriantArenaPacketInboxMessage message,
            out string error)
        {
            message = null;
            error = null;

            int minimumTokenCount = expectAvatarLookPayload ? argumentIndex + 4 : argumentIndex + 3;
            if (tokens.Length < minimumTokenCount)
            {
                error = expectAvatarLookPayload
                    ? "Ariant actor avatar inbox line requires '<name> <x> <y> <avatarLookHex>'."
                    : "Ariant actor inbox line requires '<name> <x> <y>'.";
                return false;
            }

            string actorName = tokens[argumentIndex];
            if (string.IsNullOrWhiteSpace(actorName))
            {
                error = "Ariant actor inbox line requires a non-empty name.";
                return false;
            }

            if (!TryParseCoordinate(tokens[argumentIndex + 1], out float x)
                || !TryParseCoordinate(tokens[argumentIndex + 2], out float y))
            {
                error = "Ariant actor inbox position requires numeric <x> <y> world coordinates.";
                return false;
            }

            byte[] avatarLookPayload = null;
            int optionalStartIndex = argumentIndex + 3;
            if (expectAvatarLookPayload)
            {
                string avatarLookHex = tokens[argumentIndex + 3];
                string compactHex = RemoveWhitespace(avatarLookHex);
                if (compactHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    compactHex = compactHex[2..];
                }

                try
                {
                    avatarLookPayload = Convert.FromHexString(compactHex);
                }
                catch (FormatException)
                {
                    error = $"Invalid Ariant actor AvatarLook hex payload: {avatarLookHex}";
                    return false;
                }

                optionalStartIndex++;
            }

            if (!TryParseOptionalActionAndFacing(tokens, optionalStartIndex, out string actionName, out bool? facingRight, out error))
            {
                return false;
            }

            message = new AriantArenaPacketInboxMessage(
                kind,
                actorName,
                new Vector2(x, y),
                facingRight,
                actionName,
                avatarLookPayload,
                source: "ariant-inbox",
                rawText);
            return true;
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

                error = $"Unexpected Ariant actor token '{token}'.";
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

        private void EnqueueMessage(AriantArenaPacketInboxMessage message, string sourceLabel)
        {
            if (message == null)
            {
                return;
            }

            _pendingMessages.Enqueue(message);
            string remoteEndpoint = string.IsNullOrWhiteSpace(sourceLabel) ? "ariant-inbox" : sourceLabel;
            LastStatus = $"Queued {DescribeMessage(message)} from {remoteEndpoint}.";
        }
    }
}

