using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using MapleLib.PacketLib;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum MapleTvSendResultKind
    {
        Success,
        Busy,
        RecipientOffline,
        Failed
    }

    internal sealed class MapleTvRuntime
    {
        internal const int PacketTypeSetMessage = 405;
        internal const int PacketTypeClearMessage = 406;
        internal const int PacketTypeSendMessageResult = 407;

        private const int DefaultMediaIndex = 1;
        private const int DefaultDurationMs = 15000;
        private const int MinDurationMs = 1000;
        private const int MaxDurationMs = 60000;
        private const int DisplayLineCount = 5;

        private readonly string[] _draftLines = new string[DisplayLineCount];
        private readonly string[] _displayLines = new string[DisplayLineCount];
        private CharacterBuild _senderBuild;
        private CharacterBuild _receiverBuild;
        private string _senderName = "Player";
        private string _receiverName = string.Empty;
        private string _itemName = "Maple TV";
        private string _defaultItemName = "Maple TV";
        private string _statusMessage = "Prepare a MapleTV draft, then publish it through the simulator window or /mapletv.";
        private int _itemId;
        private int _defaultItemId;
        private int _defaultMediaIndex = DefaultMediaIndex;
        private int _resolvedMediaIndex = DefaultMediaIndex;
        private int _messageType;
        private int _draftDurationMs = DefaultDurationMs;
        private int _messageStartedAt = int.MinValue;
        private int _lastClientSendResultCode = -1;
        private int _lastClientSendResultStringPoolId = -1;
        private bool _useReceiver;
        private bool _showMessage;
        private bool _queueExists;
        private bool _isSelfMessage = true;
        private MapleTvItemProfile _itemProfile;
        private MapleTvSendResultFeedback _pendingSendResultFeedback;

        internal MapleTvRuntime()
        {
            _draftLines[0] = "MapSimulator now exposes MapleTV parity.";
            _draftLines[1] = "Use /mapletv line <1-5> <text> to edit lines.";
            _draftLines[2] = "Use /mapletv receiver <name> to target someone.";
            _draftLines[3] = "Use /mapletv item <itemId> to swap the media item.";
            _draftLines[4] = "Use /mapletv set to start the timed display.";
        }

        internal void UpdateLocalContext(CharacterBuild build)
        {
            if (build == null)
            {
                return;
            }

            _senderBuild = build.Clone();
            _senderName = string.IsNullOrWhiteSpace(build.Name) ? _senderName : build.Name.Trim();

            if (_useReceiver)
            {
                _receiverBuild = CreateReceiverBuild();
            }
            else
            {
                _receiverBuild = null;
            }
        }

        internal void ConfigureDefaultMedia(int itemId, string itemName, int defaultMediaIndex = DefaultMediaIndex)
        {
            _defaultItemId = Math.Max(0, itemId);
            _defaultItemName = string.IsNullOrWhiteSpace(itemName) ? "Maple TV" : itemName.Trim();
            _defaultMediaIndex = Math.Max(0, defaultMediaIndex);
            _itemProfile = MapleTvItemProfile.CreateDefault(_defaultItemId, _defaultItemName, _defaultMediaIndex, DefaultDurationMs);
            if (_itemId == 0)
            {
                ApplyItemProfile(_itemProfile, preserveReceiverSelection: true);
            }

            if (_itemId == 0)
            {
                _itemName = _defaultItemName;
            }
        }

        internal void Update(int currentTick)
        {
            if (!_showMessage || _messageStartedAt == int.MinValue)
            {
                return;
            }

            if (currentTick - _messageStartedAt < _draftDurationMs)
            {
                return;
            }

            _showMessage = false;
            _queueExists = true;
            _messageStartedAt = int.MinValue;
            _statusMessage = "MapleTV display interval elapsed. The queue remains visible until it is dismissed.";
        }

        internal MapleTvSnapshot BuildSnapshot(int currentTick)
        {
            int remainingMs = 0;
            if (_showMessage && _messageStartedAt != int.MinValue)
            {
                remainingMs = Math.Max(0, _draftDurationMs - Math.Max(0, currentTick - _messageStartedAt));
            }

            return new MapleTvSnapshot
            {
                SenderName = _senderName,
                ReceiverName = _receiverName,
                ItemName = _itemName,
                ItemId = _itemId,
                DefaultItemId = _defaultItemId,
                DefaultItemName = _defaultItemName,
                DefaultMediaIndex = _defaultMediaIndex,
                ResolvedMediaIndex = _resolvedMediaIndex,
                DraftLines = Array.AsReadOnly((string[])_draftLines.Clone()),
                DisplayLines = Array.AsReadOnly((string[])_displayLines.Clone()),
                StatusMessage = _statusMessage,
                UseReceiver = _useReceiver,
                IsShowingMessage = _showMessage,
                QueueExists = _queueExists,
                IsSelfMessage = _isSelfMessage,
                MessageType = _messageType,
                SenderBuild = _senderBuild,
                ReceiverBuild = _receiverBuild,
                RemainingMs = remainingMs,
                TotalWaitMs = _draftDurationMs,
                CanPublish = _draftLines.Any(line => !string.IsNullOrWhiteSpace(line)),
                CanClear = _showMessage || _queueExists,
                MirrorsToChat = _itemProfile?.MirrorsToChat ?? false
            };
        }

        internal string DescribeStatus(int currentTick)
        {
            MapleTvSnapshot snapshot = BuildSnapshot(currentTick);
            string mode = snapshot.IsShowingMessage ? "showing" : "idle";
            string receiver = snapshot.UseReceiver
                ? (!string.IsNullOrWhiteSpace(snapshot.ReceiverName) ? snapshot.ReceiverName : "(receiver pending)")
                : "self broadcast";
            string timer = snapshot.IsShowingMessage
                ? $"{snapshot.RemainingMs / 1000f:0.0}s remaining"
                : $"{snapshot.TotalWaitMs / 1000f:0.0}s duration";
            string itemLabel = snapshot.ItemId > 0
                ? $"{snapshot.ItemId} ({snapshot.ItemName})"
                : $"{snapshot.DefaultItemId} ({snapshot.DefaultItemName})";
            return $"MapleTV {mode}: {snapshot.SenderName} -> {receiver}, item {itemLabel}, {timer}. {snapshot.StatusMessage}";
        }

        internal string ToggleReceiverMode()
        {
            MapleTvAudienceMode audienceMode = GetCurrentAudienceMode();
            if (audienceMode == MapleTvAudienceMode.SenderOnly)
            {
                _useReceiver = false;
                _isSelfMessage = true;
                _messageType = 1;
                _receiverName = string.Empty;
                _receiverBuild = null;
                _statusMessage = "This MapleTV item only supports sender-only broadcasts.";
                return _statusMessage;
            }

            _useReceiver = !_useReceiver;
            _isSelfMessage = !_useReceiver;
            _messageType = _useReceiver ? 2 : 1;
            if (!_useReceiver)
            {
                if (audienceMode == MapleTvAudienceMode.ReceiverRequired)
                {
                    _useReceiver = true;
                    _isSelfMessage = false;
                    _messageType = 2;
                    _receiverBuild = CreateReceiverBuild();
                    _statusMessage = "This MapleTV item requires a receiver.";
                    return _statusMessage;
                }

                _receiverName = string.Empty;
                _receiverBuild = null;
                _statusMessage = "MapleTV receiver field disabled. Broadcast will target the sender only.";
            }
            else
            {
                _receiverBuild = CreateReceiverBuild();
                _statusMessage = "MapleTV receiver field enabled. Set a recipient with /mapletv receiver <name>.";
            }

            return _statusMessage;
        }

        internal string SetReceiver(string receiverName)
        {
            MapleTvAudienceMode audienceMode = GetCurrentAudienceMode();
            if (string.IsNullOrWhiteSpace(receiverName) ||
                string.Equals(receiverName, "self", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(receiverName, "clear", StringComparison.OrdinalIgnoreCase))
            {
                if (audienceMode == MapleTvAudienceMode.ReceiverRequired)
                {
                    _useReceiver = true;
                    _isSelfMessage = false;
                    _messageType = 2;
                    _receiverBuild = CreateReceiverBuild();
                    _statusMessage = "This MapleTV item requires a receiver.";
                    return _statusMessage;
                }

                _useReceiver = false;
                _receiverName = string.Empty;
                _isSelfMessage = true;
                _messageType = 1;
                _receiverBuild = null;
                _statusMessage = "MapleTV receiver cleared. Broadcast will target the sender only.";
                return _statusMessage;
            }

            if (audienceMode == MapleTvAudienceMode.SenderOnly)
            {
                _useReceiver = false;
                _receiverName = string.Empty;
                _isSelfMessage = true;
                _messageType = 1;
                _receiverBuild = null;
                _statusMessage = "This MapleTV item only supports sender-only broadcasts.";
                return _statusMessage;
            }

            _useReceiver = true;
            _receiverName = receiverName.Trim();
            _isSelfMessage = false;
            _messageType = 2;
            _receiverBuild = CreateReceiverBuild();
            _statusMessage = $"MapleTV receiver set to {_receiverName}.";
            return _statusMessage;
        }

        internal string SetSender(string senderName)
        {
            if (string.IsNullOrWhiteSpace(senderName))
            {
                return "Sender name must not be empty.";
            }

            _senderName = senderName.Trim();
            if (_senderBuild != null)
            {
                _senderBuild = _senderBuild.Clone();
                _senderBuild.Name = _senderName;
            }

            if (_useReceiver)
            {
                _receiverBuild = CreateReceiverBuild();
            }

            _statusMessage = $"MapleTV sender set to {_senderName}.";
            return _statusMessage;
        }

        internal string SetItem(int itemId, string itemName, string itemDescription = null)
        {
            if (itemId < 0)
            {
                return $"Invalid MapleTV item ID: {itemId}";
            }

            _itemId = itemId;
            _itemName = string.IsNullOrWhiteSpace(itemName)
                ? (itemId > 0 ? $"Item #{itemId}" : _defaultItemName)
                : itemName.Trim();
            MapleTvItemProfile profile = MapleTvItemProfile.Resolve(itemId, _itemName, itemDescription, _defaultItemId, _defaultItemName, _defaultMediaIndex);
            ApplyItemProfile(profile, preserveReceiverSelection: false);
            _statusMessage = itemId > 0
                ? $"MapleTV item {_itemName} ({itemId}) applied: media {_resolvedMediaIndex}, duration {_draftDurationMs} ms."
                : $"MapleTV media item reset to default media {_defaultItemName} ({_defaultItemId}).";
            return _statusMessage;
        }

        internal string SetDraftLine(int oneBasedLine, string text)
        {
            if (oneBasedLine < 1 || oneBasedLine > DisplayLineCount)
            {
                return $"Line must be between 1 and {DisplayLineCount}.";
            }

            _draftLines[oneBasedLine - 1] = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
            _statusMessage = $"MapleTV draft line {oneBasedLine} updated.";
            return _statusMessage;
        }

        internal string SetDuration(int durationMs)
        {
            if (durationMs < MinDurationMs || durationMs > MaxDurationMs)
            {
                return $"Duration must be between {MinDurationMs} and {MaxDurationMs} ms.";
            }

            _draftDurationMs = durationMs;
            _statusMessage = $"MapleTV display duration set to {_draftDurationMs} ms.";
            return _statusMessage;
        }

        internal string LoadSample(CharacterBuild build, string locationSummary)
        {
            UpdateLocalContext(build);
            _draftLines[0] = $"{_senderName} is broadcasting from MapSimulator.";
            _draftLines[1] = string.IsNullOrWhiteSpace(locationSummary)
                ? "Current field data is available in the simulator HUD."
                : $"Current field: {locationSummary}.";
            _draftLines[2] = "The MapleTV lifecycle is simulator-owned.";
            _draftLines[3] = "Use /mapletv result <success|busy|offline|fail> for send feedback.";
            _draftLines[4] = "Use /mapletv clear to dismiss the active display.";
            _statusMessage = "Loaded a MapleTV sample draft.";
            return _statusMessage;
        }

        internal string OnSetMessage(int currentTick)
        {
            if (_useReceiver && string.IsNullOrWhiteSpace(_receiverName))
            {
                return "Set a MapleTV receiver or disable the receiver field before sending.";
            }

            if (GetCurrentAudienceMode() == MapleTvAudienceMode.ReceiverRequired && string.IsNullOrWhiteSpace(_receiverName))
            {
                return "This MapleTV item requires a receiver before sending.";
            }

            if (_draftLines.All(line => string.IsNullOrWhiteSpace(line)))
            {
                return "At least one MapleTV draft line is required before sending.";
            }

            Array.Copy(_draftLines, _displayLines, DisplayLineCount);
            _showMessage = true;
            _queueExists = true;
            _messageStartedAt = currentTick;
            _isSelfMessage = !_useReceiver;
            _messageType = _useReceiver ? 2 : 1;
            _lastClientSendResultCode = -1;
            _lastClientSendResultStringPoolId = -1;
            _pendingSendResultFeedback = null;
            _statusMessage = (_itemProfile?.MirrorsToChat ?? false)
                ? $"MapleTV message set for {_draftDurationMs / 1000f:0.0}s. Megassenger chat mirroring is active."
                : $"MapleTV message set for {_draftDurationMs / 1000f:0.0}s.";
            return _statusMessage;
        }

        internal string OnClearMessage(bool preserveQueue = true)
        {
            _showMessage = false;
            _queueExists = preserveQueue;
            _messageStartedAt = int.MinValue;
            Array.Clear(_displayLines, 0, _displayLines.Length);
            _pendingSendResultFeedback = null;
            _statusMessage = preserveQueue
                ? "MapleTV display cleared. The queue remains active."
                : "MapleTV display cleared.";
            return _statusMessage;
        }

        internal string OnSendMessageResult(MapleTvSendResultKind result)
        {
            MapleTvSendResultDefinition definition = result switch
            {
                MapleTvSendResultKind.Success => ResolveSendResultDefinition(1),
                MapleTvSendResultKind.Busy => ResolveSendResultDefinition(2),
                MapleTvSendResultKind.RecipientOffline => ResolveSendResultDefinition(3),
                _ => new MapleTvSendResultDefinition(-1, -1, "failed")
            };

            _statusMessage = QueueSendResultFeedback(definition);
            return _statusMessage;
        }

        internal MapleTvSendResultFeedback ConsumePendingSendResultFeedback()
        {
            MapleTvSendResultFeedback feedback = _pendingSendResultFeedback;
            _pendingSendResultFeedback = null;
            return feedback;
        }

        internal bool TryApplyPacket(
            int packetType,
            byte[] payload,
            int currentTick,
            Func<LoginAvatarLook, CharacterBuild> buildResolver,
            out string message)
        {
            switch (packetType)
            {
                case PacketTypeSetMessage:
                    return TryApplySetMessagePacket(payload, currentTick, buildResolver, out message);

                case PacketTypeClearMessage:
                    message = ApplyClearMessagePacket();
                    return true;

                case PacketTypeSendMessageResult:
                    return TryApplySendMessageResultPacket(payload, out message);

                default:
                    message = $"Unsupported MapleTV packet type {packetType}.";
                    return false;
            }
        }

        internal string BuildMegassengerChatMirrorMessage()
        {
            if (!(_itemProfile?.MirrorsToChat ?? false))
            {
                return null;
            }

            string body = string.Join(" ", _displayLines.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim()));
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            return _useReceiver && !string.IsNullOrWhiteSpace(_receiverName)
                ? $"[Megassenger] {_senderName} -> {_receiverName}: {body}"
                : $"[Megassenger] {_senderName}: {body}";
        }

        internal bool TryApplySetMessagePacket(
            byte[] payload,
            int currentTick,
            Func<LoginAvatarLook, CharacterBuild> buildResolver,
            out string message)
        {
            message = string.Empty;
            if (payload == null || payload.Length == 0)
            {
                message = "MapleTV set-message packet payload is empty.";
                return false;
            }

            try
            {
                PacketReader reader = new(payload);
                byte flag = reader.ReadByte();
                _messageType = reader.ReadByte();

                if (!LoginAvatarLookCodec.TryDecode(reader, out LoginAvatarLook senderLook, out string senderError))
                {
                    message = senderError;
                    return false;
                }

                string senderName = reader.ReadMapleString();
                string receiverName = reader.ReadMapleString();
                string[] lines = new string[DisplayLineCount];
                for (int i = 0; i < lines.Length; i++)
                {
                    lines[i] = reader.ReadMapleString();
                }

                int totalWaitTime = reader.ReadInt();
                bool hasReceiverAvatar = (flag & 2) != 0;
                LoginAvatarLook receiverLook = null;
                if (hasReceiverAvatar)
                {
                    if (!LoginAvatarLookCodec.TryDecode(reader, out receiverLook, out string receiverError))
                    {
                        message = receiverError;
                        return false;
                    }
                }

                _senderName = string.IsNullOrWhiteSpace(senderName) ? _senderName : senderName.Trim();
                _receiverName = string.IsNullOrWhiteSpace(receiverName) ? string.Empty : receiverName.Trim();
                Array.Copy(lines, _displayLines, DisplayLineCount);
                _showMessage = true;
                _queueExists = true;
                _messageStartedAt = currentTick;
                _draftDurationMs = Math.Clamp(totalWaitTime, 0, MaxDurationMs);
                _isSelfMessage = !hasReceiverAvatar;
                _useReceiver = hasReceiverAvatar || !string.IsNullOrWhiteSpace(_receiverName);
                _senderBuild = buildResolver?.Invoke(senderLook);
                if (_senderBuild != null)
                {
                    _senderBuild.Name = _senderName;
                }

                _receiverBuild = receiverLook != null ? buildResolver?.Invoke(receiverLook) : null;
                if (_receiverBuild != null)
                {
                    _receiverBuild.Name = string.IsNullOrWhiteSpace(_receiverName) ? "Receiver" : _receiverName;
                }

                _statusMessage = $"Applied MapleTV set-message packet ({(_useReceiver ? "dedication" : "self")} type {_messageType}).";
                message = _statusMessage;
                return true;
            }
            catch (EndOfStreamException)
            {
                message = "MapleTV set-message packet ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                message = "MapleTV set-message packet could not be read.";
                return false;
            }
        }

        internal string ApplyClearMessagePacket()
        {
            return OnClearMessage(preserveQueue: true);
        }

        internal bool TryApplySendMessageResultPacket(byte[] payload, out string message)
        {
            message = string.Empty;
            if (payload == null || payload.Length == 0)
            {
                message = "MapleTV send-result packet payload is empty.";
                return false;
            }

            try
            {
                PacketReader reader = new(payload);
                bool shouldShowFeedback = reader.ReadByte() != 0;
                if (!shouldShowFeedback)
                {
                    message = "MapleTV send-result packet contained no client chat feedback.";
                    return true;
                }

                byte resultCode = reader.ReadByte();
                if (!TryTryResolveSendResultDefinition(resultCode, out MapleTvSendResultDefinition definition))
                {
                    _statusMessage = $"MapleTV send-result packet used unsupported code {resultCode}.";
                    message = _statusMessage;
                    return false;
                }

                QueueSendResultFeedback(definition);
                _statusMessage = $"MapleTV send-result packet queued client chat feedback for code {resultCode} (StringPool 0x{definition.StringPoolId:X}).";
                message = _statusMessage;
                return true;
            }
            catch (EndOfStreamException)
            {
                message = "MapleTV send-result packet ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                message = "MapleTV send-result packet could not be read.";
                return false;
            }
        }

        private CharacterBuild CreateReceiverBuild()
        {
            if (_senderBuild == null)
            {
                return null;
            }

            CharacterBuild receiverBuild = _senderBuild.Clone();
            receiverBuild.Name = string.IsNullOrWhiteSpace(_receiverName) ? "Receiver" : _receiverName;
            return receiverBuild;
        }

        private MapleTvAudienceMode GetCurrentAudienceMode()
        {
            return (_itemProfile ?? MapleTvItemProfile.CreateDefault(_defaultItemId, _defaultItemName, _defaultMediaIndex, DefaultDurationMs)).AudienceMode;
        }

        private void ApplyItemProfile(MapleTvItemProfile profile, bool preserveReceiverSelection)
        {
            _itemProfile = profile ?? MapleTvItemProfile.CreateDefault(_defaultItemId, _defaultItemName, _defaultMediaIndex, DefaultDurationMs);
            _resolvedMediaIndex = _itemProfile.MediaIndex;
            _draftDurationMs = _itemProfile.DurationMs;

            switch (_itemProfile.AudienceMode)
            {
                case MapleTvAudienceMode.SenderOnly:
                    _useReceiver = false;
                    _isSelfMessage = true;
                    _messageType = 1;
                    _receiverName = string.Empty;
                    _receiverBuild = null;
                    break;

                case MapleTvAudienceMode.ReceiverRequired:
                    _useReceiver = true;
                    _isSelfMessage = false;
                    _messageType = 2;
                    if (!preserveReceiverSelection && string.IsNullOrWhiteSpace(_receiverName))
                    {
                        _receiverName = string.Empty;
                    }

                    _receiverBuild = CreateReceiverBuild();
                    break;

                default:
                    if (!preserveReceiverSelection)
                    {
                        _useReceiver = false;
                        _isSelfMessage = true;
                        _messageType = 1;
                        _receiverName = string.Empty;
                        _receiverBuild = null;
                    }
                    else if (_useReceiver)
                    {
                        _receiverBuild = CreateReceiverBuild();
                    }

                    break;
            }
        }

        private string QueueSendResultFeedback(MapleTvSendResultDefinition definition)
        {
            _lastClientSendResultCode = definition.ResultCode;
            _lastClientSendResultStringPoolId = definition.StringPoolId;

            string resolvedText = definition.StringPoolId >= 0
                ? $"MapleTV send result {definition.StatusLabel}. [StringPool 0x{definition.StringPoolId:X} unresolved]"
                : "MapleTV send result failed.";

            _pendingSendResultFeedback = new MapleTvSendResultFeedback(
                resolvedText,
                12,
                definition.ResultCode,
                definition.StringPoolId);

            return resolvedText;
        }

        private static MapleTvSendResultDefinition ResolveSendResultDefinition(byte resultCode)
        {
            return resultCode switch
            {
                1 => new MapleTvSendResultDefinition(1, 0xF9E, "accepted"),
                2 => new MapleTvSendResultDefinition(2, 0xFA0, "rejected"),
                3 => new MapleTvSendResultDefinition(3, 0xF9F, "recipient unavailable"),
                _ => new MapleTvSendResultDefinition(resultCode, -1, "failed")
            };
        }

        private static bool TryTryResolveSendResultDefinition(byte resultCode, out MapleTvSendResultDefinition definition)
        {
            definition = ResolveSendResultDefinition(resultCode);
            return definition.StringPoolId >= 0;
        }
    }

    internal sealed record MapleTvSendResultDefinition(int ResultCode, int StringPoolId, string StatusLabel);

    internal sealed class MapleTvSnapshot
    {
        public string SenderName { get; init; } = string.Empty;
        public string ReceiverName { get; init; } = string.Empty;
        public string ItemName { get; init; } = string.Empty;
        public int ItemId { get; init; }
        public string DefaultItemName { get; init; } = string.Empty;
        public int DefaultItemId { get; init; }
        public int DefaultMediaIndex { get; init; }
        public int ResolvedMediaIndex { get; init; }
        public IReadOnlyList<string> DraftLines { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> DisplayLines { get; init; } = Array.Empty<string>();
        public string StatusMessage { get; init; } = string.Empty;
        public bool UseReceiver { get; init; }
        public bool IsShowingMessage { get; init; }
        public bool QueueExists { get; init; }
        public bool IsSelfMessage { get; init; }
        public int MessageType { get; init; }
        public CharacterBuild SenderBuild { get; init; }
        public CharacterBuild ReceiverBuild { get; init; }
        public int RemainingMs { get; init; }
        public int TotalWaitMs { get; init; }
        public bool CanPublish { get; init; }
        public bool CanClear { get; init; }
        public bool MirrorsToChat { get; init; }
    }

    internal sealed record MapleTvSendResultFeedback(string ChatMessage, int ChatLogType, int ResultCode, int StringPoolId);

    internal enum MapleTvAudienceMode
    {
        Flexible,
        SenderOnly,
        ReceiverRequired
    }

    internal sealed record MapleTvItemProfile(int ItemId, string ItemName, int MediaIndex, int DurationMs, MapleTvAudienceMode AudienceMode, bool MirrorsToChat)
    {
        private const int DefaultItemDurationMs = 15000;

        internal static MapleTvItemProfile CreateDefault(int itemId, string itemName, int defaultMediaIndex, int defaultDurationMs)
        {
            string name = string.IsNullOrWhiteSpace(itemName) ? "Maple TV" : itemName.Trim();
            return new MapleTvItemProfile(Math.Max(0, itemId), name, Math.Max(0, defaultMediaIndex), defaultDurationMs, MapleTvAudienceMode.Flexible, false);
        }

        internal static MapleTvItemProfile Resolve(int itemId, string itemName, string itemDescription, int defaultItemId, string defaultItemName, int defaultMediaIndex)
        {
            if (itemId <= 0)
            {
                return CreateDefault(defaultItemId, defaultItemName, defaultMediaIndex, DefaultItemDurationMs);
            }

            int alternateMediaA = defaultMediaIndex == 0 ? 1 : 0;
            int alternateMediaB = defaultMediaIndex <= 1 ? 2 : 1;
            string resolvedName = string.IsNullOrWhiteSpace(itemName) ? $"Item #{itemId}" : itemName.Trim();
            string normalizedDescription = itemDescription ?? string.Empty;
            bool isMegassenger =
                resolvedName.IndexOf("Megassenger", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedDescription.IndexOf("Megassenger", StringComparison.OrdinalIgnoreCase) >= 0;

            MapleTvAudienceMode audienceMode =
                normalizedDescription.IndexOf("only announcement type of message", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedDescription.IndexOf("only supports sender-only", StringComparison.OrdinalIgnoreCase) >= 0
                    ? MapleTvAudienceMode.SenderOnly
                    : normalizedDescription.IndexOf("only allows dedication type", StringComparison.OrdinalIgnoreCase) >= 0
                      || normalizedDescription.IndexOf("designated user also appears", StringComparison.OrdinalIgnoreCase) >= 0
                        ? MapleTvAudienceMode.ReceiverRequired
                        : MapleTvAudienceMode.Flexible;

            int durationMs =
                normalizedDescription.IndexOf("1 minute", StringComparison.OrdinalIgnoreCase) >= 0 ? 60000
                : normalizedDescription.IndexOf("30 seconds", StringComparison.OrdinalIgnoreCase) >= 0 ? 30000
                : 15000;

            int mediaIndex =
                normalizedDescription.IndexOf("star effect", StringComparison.OrdinalIgnoreCase) >= 0 ? alternateMediaA
                : normalizedDescription.IndexOf("heart effect", StringComparison.OrdinalIgnoreCase) >= 0 ? alternateMediaB
                : defaultMediaIndex;

            MapleTvItemProfile inferredProfile = new(itemId, resolvedName, mediaIndex, durationMs, audienceMode, isMegassenger);
            return itemId switch
            {
                5075000 => inferredProfile with { MediaIndex = defaultMediaIndex, DurationMs = 15000, AudienceMode = MapleTvAudienceMode.Flexible, MirrorsToChat = false },
                5075001 => inferredProfile with { MediaIndex = alternateMediaA, DurationMs = 30000, AudienceMode = MapleTvAudienceMode.SenderOnly, MirrorsToChat = false },
                5075002 => inferredProfile with { MediaIndex = alternateMediaB, DurationMs = 60000, AudienceMode = MapleTvAudienceMode.ReceiverRequired, MirrorsToChat = false },
                5075003 => inferredProfile with { MediaIndex = defaultMediaIndex, DurationMs = 15000, AudienceMode = MapleTvAudienceMode.Flexible, MirrorsToChat = true },
                5075004 => inferredProfile with { MediaIndex = alternateMediaA, DurationMs = 15000, AudienceMode = MapleTvAudienceMode.Flexible, MirrorsToChat = true },
                5075005 => inferredProfile with { MediaIndex = alternateMediaB, DurationMs = 15000, AudienceMode = MapleTvAudienceMode.Flexible, MirrorsToChat = true },
                _ => CreateDefault(itemId, resolvedName, defaultMediaIndex, DefaultItemDurationMs)
            };
        }
    }
}
