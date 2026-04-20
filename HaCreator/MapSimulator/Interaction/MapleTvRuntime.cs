using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using MapleLib.PacketLib;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum MapleTvSendResultKind
    {
        Sent,
        WrongUserName,
        QueueTooLong,
        Failed
    }

    internal sealed class MapleTvRuntime
    {
        internal const int PacketTypeSetMessage = 405;
        internal const int PacketTypeClearMessage = 406;
        internal const int PacketTypeSendMessageResult = 407;
        internal const int ConsumeCashItemUseRequestOpcode = 0x55;

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
        private string _defaultMediaRootPath = "UI/MapleTV.img/TVmedia";
        private string _defaultMediaPathTemplate = "UI/MapleTV.img/TVmedia/%d";
        private string _defaultMediaResolutionSource = "fallback";
        private string _statusMessage = "Prepare a MapleTV draft, then publish it through the simulator window or /mapletv.";
        private int _itemId;
        private int _defaultItemId;
        private int _defaultMediaIndex = DefaultMediaIndex;
        private IReadOnlyList<int> _availableMediaIndices = Array.Empty<int>();
        private int _mediaBranchCount;
        private int _resolvedMediaIndex = DefaultMediaIndex;
        private int _messageType;
        private int _draftDurationMs = DefaultDurationMs;
        private int _activeDurationMs;
        private int _queueConfirmationWaitSeconds;
        private int _messageStartedAt = int.MinValue;
        private int _lastClientSendResultCode = -1;
        private int _lastClientSendResultStringPoolId = -1;
        private bool _useReceiver;
        private bool _showMessage;
        private bool _queueExists;
        private bool _isSelfMessage = true;
        private bool _awaitingQueueReuseConfirmation;
        private MapleTvItemProfile _itemProfile;
        private MapleTvSendResultFeedback _pendingSendResultFeedback;
        private MapleTvClientRequestState _lastClientRequestState;
        private MapleTvOfficialPacketState _lastOfficialPacketState;

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

        internal void ConfigureDefaultMedia(
            int itemId,
            string itemName,
            int defaultMediaIndex = DefaultMediaIndex,
            IReadOnlyList<int> availableMediaIndices = null,
            int explicitWzDefaultMediaIndex = -1)
        {
            _defaultItemId = Math.Max(0, itemId);
            _defaultItemName = string.IsNullOrWhiteSpace(itemName) ? "Maple TV" : itemName.Trim();
            _defaultMediaRootPath = MapleStoryStringPool.GetOrFallback(0x0F8D, "UI/MapleTV.img/TVmedia");
            _defaultMediaPathTemplate = MapleStoryStringPool.GetOrFallback(0x0F8E, "UI/MapleTV.img/TVmedia/%d");
            _availableMediaIndices = MapleTvMediaIndexResolver.NormalizeAvailableMediaIndices(availableMediaIndices, defaultMediaIndex);
            MapleTvClientInitMediaResolution resolution = MapleTvMediaIndexResolver.ResolveClientInitDefaultMedia(
                _defaultMediaRootPath,
                _defaultMediaPathTemplate,
                _availableMediaIndices,
                defaultMediaIndex,
                explicitWzDefaultMediaIndex);
            _defaultMediaIndex = resolution.MediaIndex;
            _defaultMediaResolutionSource = resolution.Source;
            _mediaBranchCount = _availableMediaIndices.Count;
            _itemProfile = MapleTvItemProfile.CreateDefault(_defaultItemId, _defaultItemName, _defaultMediaIndex, DefaultDurationMs, _availableMediaIndices);
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

            int elapsedMs = Math.Max(0, currentTick - _messageStartedAt);
            if (elapsedMs < _activeDurationMs)
            {
                int remainingMs = Math.Max(0, _activeDurationMs - elapsedMs);
                SetQueueConfirmationWaitFromDurationMs(remainingMs);
                return;
            }

            _showMessage = false;
            _queueExists = true;
            _messageStartedAt = int.MinValue;
            _activeDurationMs = 0;
            _queueConfirmationWaitSeconds = 0;
            _statusMessage = "MapleTV display interval elapsed. The queue remains visible until it is dismissed.";
        }

        internal MapleTvSnapshot BuildSnapshot(int currentTick)
        {
            int remainingMs = 0;
            int messageAnimationTick = 0;
            if (_showMessage && _messageStartedAt != int.MinValue)
            {
                messageAnimationTick = Math.Max(0, currentTick - _messageStartedAt);
                remainingMs = Math.Max(0, _activeDurationMs - Math.Max(0, currentTick - _messageStartedAt));
            }

            int totalWaitMs = ResolveSnapshotTotalWaitMs();

            return new MapleTvSnapshot
            {
                SenderName = _senderName,
                ReceiverName = _receiverName,
                ItemName = _itemName,
                ItemId = _itemId,
                DefaultItemId = _defaultItemId,
                DefaultItemName = _defaultItemName,
                DefaultMediaRootPath = _defaultMediaRootPath,
                DefaultMediaPathTemplate = _defaultMediaPathTemplate,
                DefaultMediaIndex = _defaultMediaIndex,
                DefaultMediaResolutionSource = _defaultMediaResolutionSource,
                MediaBranchCount = _mediaBranchCount,
                ResolvedMediaIndex = _resolvedMediaIndex,
                DraftLines = Array.AsReadOnly((string[])_draftLines.Clone()),
                DisplayLines = Array.AsReadOnly((string[])_displayLines.Clone()),
                StatusMessage = _statusMessage,
                UseReceiver = _useReceiver,
                IsShowingMessage = _showMessage,
                QueueExists = _queueExists,
                IsSelfMessage = _isSelfMessage,
                AwaitingQueueReuseConfirmation = _awaitingQueueReuseConfirmation,
                MessageType = _messageType,
                SenderBuild = _senderBuild,
                ReceiverBuild = _receiverBuild,
                RemainingMs = remainingMs,
                MessageAnimationTick = messageAnimationTick,
                TotalWaitMs = totalWaitMs,
                CanPublish = _draftLines.Any(line => !string.IsNullOrWhiteSpace(line)),
                CanClear = _showMessage || _queueExists,
                CanToggleReceiver = GetCurrentAudienceMode() == MapleTvAudienceMode.Flexible,
                MirrorsToChat = _itemProfile?.MirrorsToChat ?? false,
                LastClientRequest = _lastClientRequestState == null ? null : new MapleTvClientRequestSnapshot(
                    _lastClientRequestState.InventoryPosition,
                    _lastClientRequestState.ItemId,
                    _lastClientRequestState.ReceiverName,
                    Array.AsReadOnly((string[])_lastClientRequestState.Lines.Clone()),
                    _lastClientRequestState.Source,
                    _lastClientRequestState.Stage,
                    _lastClientRequestState.RequestTick,
                    _lastClientRequestState.LastUpdatedTick,
                    _lastClientRequestState.ResultCode,
                    _lastClientRequestState.ResultStringPoolId),
                LastOfficialPacket = _lastOfficialPacketState == null ? null : new MapleTvOfficialPacketSnapshot(
                    _lastOfficialPacketState.PacketType,
                    _lastOfficialPacketState.PacketLabel,
                    _lastOfficialPacketState.Source,
                    _lastOfficialPacketState.Tick,
                    _lastOfficialPacketState.ResultCode,
                    _lastOfficialPacketState.ResultStringPoolId)
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
            string initMedia = snapshot.MediaBranchCount > 0
                ? $" Init media root {snapshot.DefaultMediaRootPath} exposes {snapshot.MediaBranchCount} branch(es); default branch {snapshot.DefaultMediaIndex} via {snapshot.DefaultMediaResolutionSource}."
                : string.Empty;
            string queueConfirmationSuffix = snapshot.AwaitingQueueReuseConfirmation
                ? " Queue reuse confirmation is pending."
                : string.Empty;
            string requestSuffix = snapshot.LastClientRequest == null
                ? string.Empty
                : $" Client request {DescribeClientRequest(snapshot.LastClientRequest)}.";
            string packetSuffix = snapshot.LastOfficialPacket == null
                ? string.Empty
                : $" Last official packet {DescribeOfficialPacket(snapshot.LastOfficialPacket)}.";
            return $"MapleTV {mode}: {snapshot.SenderName} -> {receiver}, item {itemLabel}, {timer}.{initMedia} {snapshot.StatusMessage}{queueConfirmationSuffix}{requestSuffix}{packetSuffix}";
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
            MapleTvItemProfile profile = MapleTvItemProfile.Resolve(itemId, _itemName, itemDescription, _defaultItemId, _defaultItemName, _defaultMediaIndex, _availableMediaIndices);
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

            if (_queueExists && !_awaitingQueueReuseConfirmation)
            {
                _awaitingQueueReuseConfirmation = true;
                _statusMessage = BuildQueueReuseConfirmationText();
                return _statusMessage;
            }

            Array.Copy(_draftLines, _displayLines, DisplayLineCount);
            _showMessage = true;
            _queueExists = true;
            _awaitingQueueReuseConfirmation = false;
            _messageStartedAt = currentTick;
            _activeDurationMs = _draftDurationMs;
            SetQueueConfirmationWaitFromDurationMs(_activeDurationMs);
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
            _activeDurationMs = 0;
            _queueConfirmationWaitSeconds = 0;
            _awaitingQueueReuseConfirmation = false;
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
                MapleTvSendResultKind.Sent => ResolveSendResultDefinition(1),
                MapleTvSendResultKind.WrongUserName => ResolveSendResultDefinition(2),
                MapleTvSendResultKind.QueueTooLong => ResolveSendResultDefinition(3),
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

        internal void RecordClientRequest(
            MapleTvConsumeCashItemUseRequest request,
            int currentTick,
            string source,
            MapleTvClientRequestStage stage)
        {
            if (request == null)
            {
                return;
            }

            _lastClientRequestState = new MapleTvClientRequestState(
                request.InventoryPosition,
                request.ItemId,
                string.IsNullOrWhiteSpace(request.ReceiverName) ? string.Empty : request.ReceiverName.Trim(),
                request.Lines?.ToArray() ?? Array.Empty<string>(),
                string.IsNullOrWhiteSpace(source) ? "MapleTV client request" : source.Trim(),
                stage,
                currentTick,
                currentTick,
                -1,
                -1);
        }

        internal bool TryApplyPacket(
            int packetType,
            byte[] payload,
            int currentTick,
            Func<LoginAvatarLook, CharacterBuild> buildResolver,
            out string message,
            string source = null)
        {
            switch (packetType)
            {
                case PacketTypeSetMessage:
                    return TryApplySetMessagePacket(payload, currentTick, buildResolver, out message, source);

                case PacketTypeClearMessage:
                    message = ApplyClearMessagePacket(currentTick, source);
                    return true;

                case PacketTypeSendMessageResult:
                    return TryApplySendMessageResultPacket(payload, currentTick, out message, source);

                default:
                    message = $"Unsupported MapleTV packet type {packetType}.";
                    return false;
            }
        }

        internal bool TryBuildOutboundPacketPayload(int packetType, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = string.Empty;

            switch (packetType)
            {
                case PacketTypeSetMessage:
                    return TryBuildSetMessagePayload(out payload, out error);

                case PacketTypeClearMessage:
                    payload = Array.Empty<byte>();
                    return true;

                default:
                    error = $"MapleTV outbound packet payload generation is not supported for opcode {packetType}.";
                    return false;
            }
        }

        internal bool TryBuildConsumeCashItemUseRequestPayload(
            int currentTick,
            int inventoryPosition,
            int overrideItemId,
            out byte[] payload,
            out string error)
        {
            payload = Array.Empty<byte>();
            error = string.Empty;

            int itemId = overrideItemId > 0 ? overrideItemId : (_itemId > 0 ? _itemId : _defaultItemId);
            if (itemId <= 0)
            {
                error = "MapleTV client send request requires a MapleTV cash item id.";
                return false;
            }

            if (inventoryPosition < short.MinValue || inventoryPosition > ushort.MaxValue)
            {
                error = $"MapleTV client send request inventory position {inventoryPosition} is outside the 16-bit client packet range.";
                return false;
            }

            IReadOnlyList<string> lines = (_showMessage && _displayLines.Any(line => !string.IsNullOrWhiteSpace(line)))
                ? _displayLines
                : _draftLines;
            if (lines.All(line => string.IsNullOrWhiteSpace(line)))
            {
                error = "MapleTV client send request requires at least one message line.";
                return false;
            }

            PacketWriter writer = new();
            writer.WriteInt(Math.Max(0, currentTick));
            writer.WriteShort(inventoryPosition);
            writer.WriteInt(itemId);
            writer.WriteMapleString(_useReceiver ? _receiverName ?? string.Empty : string.Empty);
            for (int i = 0; i < DisplayLineCount; i++)
            {
                writer.WriteMapleString(i < lines.Count ? lines[i] ?? string.Empty : string.Empty);
            }

            payload = writer.ToArray();
            return true;
        }

        internal bool TryApplyConsumeCashItemUseRequestPayload(
            byte[] payload,
            int currentTick,
            Func<int, (string ItemName, string ItemDescription)> itemMetadataResolver,
            out string message)
        {
            message = string.Empty;
            if (!TryDecodeConsumeCashItemUseRequestPayload(payload, out MapleTvConsumeCashItemUseRequest request, out string error))
            {
                message = error;
                return false;
            }

            (string itemName, string itemDescription) = itemMetadataResolver?.Invoke(request.ItemId) ?? (null, null);
            RecordClientRequest(request, currentTick, "packet-owned MapleTV consume request", MapleTvClientRequestStage.AppliedLocally);
            SetItem(request.ItemId, itemName, itemDescription);
            SetReceiver(request.ReceiverName);
            for (int i = 0; i < DisplayLineCount; i++)
            {
                _draftLines[i] = i < request.Lines.Count ? request.Lines[i] ?? string.Empty : string.Empty;
            }

            string publishStatus = OnSetMessage(currentTick);
            if (publishStatus.StartsWith("MapleTV message set", StringComparison.Ordinal))
            {
                AdvanceClientRequestStage(MapleTvClientRequestStage.BroadcastStarted, currentTick);
                MapleTvSendResultDefinition successDefinition = ResolveSendResultDefinition(1);
                QueueSendResultFeedback(successDefinition);
                _statusMessage = $"{publishStatus} Queued CMapleTVMan::OnSendMessageResult success feedback (StringPool 0x{successDefinition.StringPoolId:X}).";
            }
            else
            {
                _statusMessage = publishStatus;
            }

            message = $"Applied CUserLocal::ConsumeCashItem MapleTV request for item {request.ItemId} from slot {request.InventoryPosition}. {_statusMessage}";
            return true;
        }

        internal bool TryObserveConsumeCashItemUseRequestPayload(
            byte[] payload,
            int currentTick,
            Func<int, (string ItemName, string ItemDescription)> itemMetadataResolver,
            out string message,
            string source = null)
        {
            message = string.Empty;
            if (!TryDecodeConsumeCashItemUseRequestPayload(payload, out MapleTvConsumeCashItemUseRequest request, out string error))
            {
                message = error;
                return false;
            }

            (string itemName, string itemDescription) = itemMetadataResolver?.Invoke(request.ItemId) ?? (null, null);
            MapleTvClientRequestStage stage =
                !string.IsNullOrWhiteSpace(source)
                && source.IndexOf("queue", StringComparison.OrdinalIgnoreCase) >= 0
                    ? MapleTvClientRequestStage.Queued
                    : MapleTvClientRequestStage.Dispatched;
            string requestSource = string.IsNullOrWhiteSpace(source)
                ? "MapleTV official-session outbound observe"
                : source.Trim();
            RecordClientRequest(request, currentTick, requestSource, stage);
            SetItem(request.ItemId, itemName, itemDescription);
            SetReceiver(request.ReceiverName);
            for (int i = 0; i < DisplayLineCount; i++)
            {
                _draftLines[i] = i < request.Lines.Count ? request.Lines[i] ?? string.Empty : string.Empty;
            }

            _statusMessage =
                $"Observed CUserLocal::ConsumeCashItem MapleTV request for item {request.ItemId} from slot {request.InventoryPosition}; awaiting CMapleTVMan::OnPacket set/result.";
            message = _statusMessage;
            return true;
        }

        internal static bool TryDecodeConsumeCashItemUseRequestPayload(
            byte[] payload,
            out MapleTvConsumeCashItemUseRequest request,
            out string error)
        {
            request = null;
            error = string.Empty;
            if (payload == null || payload.Length == 0)
            {
                error = "MapleTV consume-cash item request payload is empty.";
                return false;
            }

            try
            {
                PacketReader reader = new(payload);
                int clientTick = reader.ReadInt();
                short inventoryPosition = reader.ReadShort();
                int itemId = reader.ReadInt();
                if (!MapleTvItemProfile.IsKnownMapleTvCashItem(itemId))
                {
                    error = $"Consume-cash item request item {itemId} is not a MapleTV or Megassenger item.";
                    return false;
                }

                string receiverName = reader.ReadMapleString();
                string[] lines = new string[DisplayLineCount];
                for (int i = 0; i < lines.Length; i++)
                {
                    lines[i] = reader.ReadMapleString();
                }

                request = new MapleTvConsumeCashItemUseRequest(clientTick, inventoryPosition, itemId, receiverName, Array.AsReadOnly(lines));
                return true;
            }
            catch (EndOfStreamException)
            {
                error = "MapleTV consume-cash item request ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "MapleTV consume-cash item request could not be read.";
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
            out string message,
            string source = null)
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
                _awaitingQueueReuseConfirmation = false;
                _messageStartedAt = currentTick;
                _activeDurationMs = ResolvePacketTotalWaitDurationMs(totalWaitTime);
                SetQueueConfirmationWaitFromPacketValue(totalWaitTime, _activeDurationMs);
                // CMapleTVMan::OnSetMessage stores m_nMessageType but determines
                // self-vs-receiver presentation from nFlag bit 0x02.
                _isSelfMessage = (flag & 2) == 0;
                _useReceiver = !_isSelfMessage;
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
                RecordOfficialPacket(PacketTypeSetMessage, currentTick, -1, -1, source);
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

        internal string ApplyClearMessagePacket(int currentTick, string source = null)
        {
            string cleared = OnClearMessage(preserveQueue: true);
            RecordOfficialPacket(PacketTypeClearMessage, currentTick, -1, -1, source);
            return cleared;
        }

        internal bool TryApplySendMessageResultPacket(byte[] payload, int currentTick, out string message, string source = null)
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
                    RecordOfficialPacket(PacketTypeSendMessageResult, currentTick, 0, -1, source);
                    message = "MapleTV send-result packet contained no client chat feedback.";
                    return true;
                }

                byte resultCode = reader.ReadByte();
                if (!TryTryResolveSendResultDefinition(resultCode, out MapleTvSendResultDefinition definition))
                {
                    // CMapleTVMan::OnSendMessageResult silently ignores unknown result codes:
                    // only 1/2/3 map to StringPool chat notices.
                    RecordOfficialPacket(PacketTypeSendMessageResult, currentTick, resultCode, -1, source);
                    _statusMessage = $"MapleTV send-result packet used unsupported code {resultCode}; client chat feedback was skipped.";
                    message = _statusMessage;
                    return true;
                }

                QueueSendResultFeedback(definition);
                RecordOfficialPacket(PacketTypeSendMessageResult, currentTick, definition.ResultCode, definition.StringPoolId, source);
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

        private bool TryBuildSetMessagePayload(out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = string.Empty;

            CharacterBuild senderBuild = _senderBuild?.Clone();
            if (senderBuild == null)
            {
                error = "MapleTV outbound set-message payload requires an active sender avatar build.";
                return false;
            }

            bool includeReceiverAvatar = _useReceiver && _receiverBuild != null;
            CharacterBuild receiverBuild = includeReceiverAvatar ? _receiverBuild.Clone() : null;
            byte flag = includeReceiverAvatar ? (byte)2 : (byte)0;
            byte messageType = (byte)Math.Clamp(_messageType > 0 ? _messageType : (_useReceiver ? 2 : 1), 0, byte.MaxValue);
            string senderName = string.IsNullOrWhiteSpace(_senderName) ? "Player" : _senderName.Trim();
            string receiverName = _useReceiver && !string.IsNullOrWhiteSpace(_receiverName) ? _receiverName.Trim() : string.Empty;
            string[] lines = (_showMessage && _displayLines.Any(line => !string.IsNullOrWhiteSpace(line)))
                ? _displayLines
                : _draftLines;
            int totalWaitTime = Math.Max(0, _showMessage ? _activeDurationMs : _draftDurationMs);

            PacketWriter writer = new();
            writer.WriteByte(flag);
            writer.WriteByte(messageType);
            writer.WriteBytes(LoginAvatarLookCodec.Encode(senderBuild));
            writer.WriteMapleString(senderName);
            writer.WriteMapleString(receiverName);
            for (int i = 0; i < DisplayLineCount; i++)
            {
                writer.WriteMapleString(lines[i] ?? string.Empty);
            }

            writer.WriteInt(totalWaitTime);
            if (includeReceiverAvatar)
            {
                writer.WriteBytes(LoginAvatarLookCodec.Encode(receiverBuild));
            }

            payload = writer.ToArray();
            return true;
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
            return (_itemProfile ?? MapleTvItemProfile.CreateDefault(_defaultItemId, _defaultItemName, _defaultMediaIndex, DefaultDurationMs, _availableMediaIndices)).AudienceMode;
        }

        private void ApplyItemProfile(MapleTvItemProfile profile, bool preserveReceiverSelection)
        {
            _itemProfile = profile ?? MapleTvItemProfile.CreateDefault(_defaultItemId, _defaultItemName, _defaultMediaIndex, DefaultDurationMs, _availableMediaIndices);
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

        private int ResolveSnapshotTotalWaitMs()
        {
            if (_showMessage || _queueExists)
            {
                return _activeDurationMs;
            }

            return _draftDurationMs;
        }

        private string BuildQueueReuseConfirmationText()
        {
            int seconds = Math.Max(0, _queueConfirmationWaitSeconds);
            string template = MapleStoryStringPool.GetOrFallback(
                0x0FA2,
                "This message will be sent to \r\nMaple TV in %d seconds. \r\n Do you want to proceed?");
            _queueConfirmationWaitSeconds = 0;
            return template.Replace("%d", seconds.ToString());
        }

        private void SetQueueConfirmationWaitFromDurationMs(int durationMs)
        {
            _queueConfirmationWaitSeconds = Math.Max(0, (int)Math.Ceiling(durationMs / 1000f));
        }

        private void SetQueueConfirmationWaitFromPacketValue(int packetWaitValue, int resolvedDurationMs)
        {
            if (packetWaitValue <= 0)
            {
                _queueConfirmationWaitSeconds = 0;
                return;
            }

            // CMapleTVMan::OnSetMessage stores the packet wait value directly for
            // ConfirmTimeRemaining; server captures can expose either second or ms-like values.
            _queueConfirmationWaitSeconds = packetWaitValue <= 600
                ? packetWaitValue
                : Math.Max(0, (int)Math.Ceiling(resolvedDurationMs / 1000f));
        }

        private static int ResolvePacketTotalWaitDurationMs(int packetWaitValue)
        {
            if (packetWaitValue <= 0)
            {
                return 0;
            }

            return packetWaitValue <= 600
                ? packetWaitValue * 1000
                : packetWaitValue;
        }

        private string QueueSendResultFeedback(MapleTvSendResultDefinition definition)
        {
            _lastClientSendResultCode = definition.ResultCode;
            _lastClientSendResultStringPoolId = definition.StringPoolId;

            string resolvedText = BuildClientSendResultFeedbackText(definition);

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
                1 => new MapleTvSendResultDefinition(1, 0xF9E, "sent"),
                2 => new MapleTvSendResultDefinition(2, 0xFA0, "wrong-user-name"),
                3 => new MapleTvSendResultDefinition(3, 0xF9F, "queue-too-long"),
                _ => new MapleTvSendResultDefinition(resultCode, -1, "failed")
            };
        }

        private static bool TryTryResolveSendResultDefinition(byte resultCode, out MapleTvSendResultDefinition definition)
        {
            definition = ResolveSendResultDefinition(resultCode);
            return definition.StringPoolId >= 0;
        }

        internal static string BuildClientSendResultFeedbackTextForTest(int resultCode)
        {
            return BuildClientSendResultFeedbackText(ResolveSendResultDefinition((byte)resultCode));
        }

        private static string BuildClientSendResultFeedbackText(MapleTvSendResultDefinition definition)
        {
            if (MapleTvSendResultText.TryResolve(definition.StringPoolId, out string resolvedText))
            {
                return resolvedText;
            }

            return definition.StringPoolId >= 0
                ? $"MapleTV send result ({definition.StatusLabel}, code {definition.ResultCode}, StringPool 0x{definition.StringPoolId:X}; localized client text unresolved)."
                : $"MapleTV send result failed (code {definition.ResultCode}).";
        }

        private void AdvanceClientRequestStage(
            MapleTvClientRequestStage stage,
            int currentTick,
            int resultCode = -1,
            int stringPoolId = -1)
        {
            if (_lastClientRequestState == null)
            {
                return;
            }

            _lastClientRequestState = _lastClientRequestState with
            {
                Stage = stage,
                LastUpdatedTick = currentTick,
                ResultCode = resultCode,
                ResultStringPoolId = stringPoolId
            };
        }

        private void RecordOfficialPacket(
            int packetType,
            int currentTick,
            int resultCode,
            int stringPoolId,
            string source)
        {
            _lastOfficialPacketState = new MapleTvOfficialPacketState(
                packetType,
                DescribePacketType(packetType),
                string.IsNullOrWhiteSpace(source) ? "packet-owned MapleTV dispatcher" : source.Trim(),
                currentTick,
                resultCode,
                stringPoolId);
            switch (packetType)
            {
                case PacketTypeSetMessage:
                    AdvanceClientRequestStage(MapleTvClientRequestStage.BroadcastStarted, currentTick, resultCode, stringPoolId);
                    break;

                case PacketTypeClearMessage:
                    AdvanceClientRequestStage(MapleTvClientRequestStage.Cleared, currentTick, resultCode, stringPoolId);
                    break;

                case PacketTypeSendMessageResult:
                    AdvanceClientRequestStage(MapleTvClientRequestStage.SendResultReceived, currentTick, resultCode, stringPoolId);
                    break;
            }
        }

        private static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                PacketTypeSetMessage => "405 (OnSetMessage)",
                PacketTypeClearMessage => "406 (OnClearMessage)",
                PacketTypeSendMessageResult => "407 (OnSendMessageResult)",
                _ => packetType.ToString()
            };
        }

        private static string DescribeClientRequest(MapleTvClientRequestSnapshot request)
        {
            string receiver = string.IsNullOrWhiteSpace(request.ReceiverName)
                ? "self"
                : request.ReceiverName;
            string result = request.ResultCode > 0
                ? $", result {request.ResultCode}"
                : string.Empty;
            return $"{request.Stage} via {request.Source} (slot {request.InventoryPosition}, item {request.ItemId}, receiver {receiver}{result})";
        }

        private static string DescribeOfficialPacket(MapleTvOfficialPacketSnapshot packet)
        {
            string result = packet.ResultCode > 0
                ? $", result {packet.ResultCode}"
                : string.Empty;
            return $"{packet.PacketLabel} via {packet.Source}{result}";
        }
    }

    internal static class MapleTvSendResultText
    {
        // CMapleTVMan::OnSendMessageResult routes result codes 1/2/3 straight to
        // StringPool 0xF9E/0xFA0/0xF9F and emits them as chat-log type 12 notices.
        public static bool TryResolve(int stringPoolId, out string text)
        {
            return MapleStoryStringPool.TryGet(stringPoolId, out text);
        }
    }

    internal sealed record MapleTvSendResultDefinition(int ResultCode, int StringPoolId, string StatusLabel);

    internal sealed record MapleTvConsumeCashItemUseRequest(
        int ClientTick,
        short InventoryPosition,
        int ItemId,
        string ReceiverName,
        IReadOnlyList<string> Lines);

    internal sealed class MapleTvSnapshot
    {
        public string SenderName { get; init; } = string.Empty;
        public string ReceiverName { get; init; } = string.Empty;
        public string ItemName { get; init; } = string.Empty;
        public int ItemId { get; init; }
        public string DefaultItemName { get; init; } = string.Empty;
        public int DefaultItemId { get; init; }
        public string DefaultMediaRootPath { get; init; } = string.Empty;
        public string DefaultMediaPathTemplate { get; init; } = string.Empty;
        public int DefaultMediaIndex { get; init; }
        public string DefaultMediaResolutionSource { get; init; } = string.Empty;
        public int MediaBranchCount { get; init; }
        public int ResolvedMediaIndex { get; init; }
        public IReadOnlyList<string> DraftLines { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> DisplayLines { get; init; } = Array.Empty<string>();
        public string StatusMessage { get; init; } = string.Empty;
        public bool UseReceiver { get; init; }
        public bool IsShowingMessage { get; init; }
        public bool QueueExists { get; init; }
        public bool IsSelfMessage { get; init; }
        public bool AwaitingQueueReuseConfirmation { get; init; }
        public int MessageType { get; init; }
        public CharacterBuild SenderBuild { get; init; }
        public CharacterBuild ReceiverBuild { get; init; }
        public int RemainingMs { get; init; }
        public int MessageAnimationTick { get; init; }
        public int TotalWaitMs { get; init; }
        public bool CanPublish { get; init; }
        public bool CanClear { get; init; }
        public bool CanToggleReceiver { get; init; }
        public bool MirrorsToChat { get; init; }
        public MapleTvClientRequestSnapshot LastClientRequest { get; init; }
        public MapleTvOfficialPacketSnapshot LastOfficialPacket { get; init; }
    }

    internal sealed record MapleTvSendResultFeedback(string ChatMessage, int ChatLogType, int ResultCode, int StringPoolId);

    internal enum MapleTvClientRequestStage
    {
        Queued,
        Dispatched,
        AppliedLocally,
        BroadcastStarted,
        SendResultReceived,
        Cleared
    }

    internal sealed record MapleTvClientRequestSnapshot(
        short InventoryPosition,
        int ItemId,
        string ReceiverName,
        IReadOnlyList<string> Lines,
        string Source,
        MapleTvClientRequestStage Stage,
        int RequestTick,
        int LastUpdatedTick,
        int ResultCode,
        int ResultStringPoolId);

    internal sealed record MapleTvOfficialPacketSnapshot(
        int PacketType,
        string PacketLabel,
        string Source,
        int Tick,
        int ResultCode,
        int ResultStringPoolId);

    internal sealed record MapleTvClientRequestState(
        short InventoryPosition,
        int ItemId,
        string ReceiverName,
        string[] Lines,
        string Source,
        MapleTvClientRequestStage Stage,
        int RequestTick,
        int LastUpdatedTick,
        int ResultCode,
        int ResultStringPoolId);

    internal sealed record MapleTvOfficialPacketState(
        int PacketType,
        string PacketLabel,
        string Source,
        int Tick,
        int ResultCode,
        int ResultStringPoolId);

    internal enum MapleTvAudienceMode
    {
        Flexible,
        SenderOnly,
        ReceiverRequired
    }

    internal sealed record MapleTvClientInitMediaResolution(int MediaIndex, string Source);

    internal static class MapleTvMediaIndexResolver
    {
        internal static int TryResolveConfiguredDefaultMediaIndex(string configuredPathOrToken, IEnumerable<int> availableMediaIndices)
        {
            if (string.IsNullOrWhiteSpace(configuredPathOrToken))
            {
                return -1;
            }

            HashSet<int> availableSet = availableMediaIndices?
                .Where(index => index >= 0)
                .ToHashSet()
                ?? new HashSet<int>();
            if (availableSet.Count == 0)
            {
                return -1;
            }

            string normalized = configuredPathOrToken.Trim().Replace('\\', '/');
            string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return -1;
            }

            for (int i = segments.Length - 1; i >= 0; i--)
            {
                if (int.TryParse(segments[i], out int directIndex) && availableSet.Contains(directIndex))
                {
                    return directIndex;
                }
            }

            int tvMediaSegment = Array.FindLastIndex(
                segments,
                segment => string.Equals(segment, "TVmedia", StringComparison.OrdinalIgnoreCase));
            if (tvMediaSegment >= 0 && tvMediaSegment + 1 < segments.Length)
            {
                string candidateSegment = segments[tvMediaSegment + 1];
                if (int.TryParse(candidateSegment, out int branchIndex) && availableSet.Contains(branchIndex))
                {
                    return branchIndex;
                }
            }

            return -1;
        }

        internal static MapleTvClientInitMediaResolution ResolveClientInitDefaultMedia(
            string configuredPathOrToken,
            string configuredPathTemplateOrToken,
            IReadOnlyList<int> availableMediaIndices,
            int fallbackDefaultMediaIndex,
            int explicitWzDefaultMediaIndex = -1)
        {
            IReadOnlyList<int> normalizedIndices = NormalizeAvailableMediaIndices(availableMediaIndices, fallbackDefaultMediaIndex);
            int configuredBranchIndex = TryResolveConfiguredDefaultMediaIndex(configuredPathOrToken, normalizedIndices);
            if (configuredBranchIndex >= 0)
            {
                return new MapleTvClientInitMediaResolution(configuredBranchIndex, "client path");
            }

            int configuredTemplateIndex = TryResolveConfiguredDefaultMediaIndex(configuredPathTemplateOrToken, normalizedIndices);
            if (configuredTemplateIndex >= 0)
            {
                return new MapleTvClientInitMediaResolution(configuredTemplateIndex, "client path template");
            }

            if (normalizedIndices.Contains(explicitWzDefaultMediaIndex))
            {
                return new MapleTvClientInitMediaResolution(explicitWzDefaultMediaIndex, "client root direct value");
            }

            if (IsConfiguredDefaultMediaRoot(configuredPathOrToken) && normalizedIndices.Contains(1))
            {
                return new MapleTvClientInitMediaResolution(1, "client root inference");
            }

            return new MapleTvClientInitMediaResolution(
                ResolveKnownMediaIndex(fallbackDefaultMediaIndex, normalizedIndices),
                "fallback");
        }

        private static bool IsConfiguredDefaultMediaRoot(string configuredPathOrToken)
        {
            if (string.IsNullOrWhiteSpace(configuredPathOrToken))
            {
                return false;
            }

            string normalized = configuredPathOrToken.Trim().Replace('\\', '/').TrimEnd('/');
            return string.Equals(normalized, "UI/MapleTV.img/TVmedia", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "MapleTV.img/TVmedia", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "TVmedia", StringComparison.OrdinalIgnoreCase);
        }

        internal static IReadOnlyList<int> NormalizeAvailableMediaIndices(IReadOnlyList<int> availableMediaIndices, int fallbackDefaultMediaIndex)
        {
            List<int> normalizedIndices = availableMediaIndices?
                .Where(index => index >= 0)
                .Distinct()
                .OrderBy(index => index)
                .ToList()
                ?? new List<int>();
            if (normalizedIndices.Count == 0)
            {
                normalizedIndices.Add(Math.Max(0, fallbackDefaultMediaIndex));
            }

            return normalizedIndices;
        }

        internal static int ResolveKnownMediaIndex(int mediaIndex, IReadOnlyList<int> availableMediaIndices)
        {
            int normalizedMediaIndex = Math.Max(0, mediaIndex);
            if (availableMediaIndices == null || availableMediaIndices.Count == 0)
            {
                return normalizedMediaIndex;
            }

            return availableMediaIndices.Contains(normalizedMediaIndex)
                ? normalizedMediaIndex
                : availableMediaIndices[0];
        }

        internal static int ResolveAlternateMediaIndex(int defaultMediaIndex, IReadOnlyList<int> availableMediaIndices, int oneBasedAlternateOrdinal)
        {
            int resolvedDefaultMediaIndex = ResolveKnownMediaIndex(defaultMediaIndex, availableMediaIndices);
            if (availableMediaIndices == null || availableMediaIndices.Count == 0)
            {
                return resolvedDefaultMediaIndex;
            }

            if (availableMediaIndices.Count == 1)
            {
                return availableMediaIndices[0];
            }

            int defaultIndex = availableMediaIndices
                .Select((value, index) => value == resolvedDefaultMediaIndex ? index : -1)
                .FirstOrDefault(index => index >= 0);
            if (defaultIndex < 0)
            {
                defaultIndex = 0;
            }

            int steps = Math.Max(1, oneBasedAlternateOrdinal);
            return availableMediaIndices[(defaultIndex + steps) % availableMediaIndices.Count];
        }

        internal static int ResolveChatVariantKey(int mediaIndex, int defaultMediaIndex, IReadOnlyList<int> availableMediaIndices)
        {
            int resolvedDefaultMediaIndex = ResolveKnownMediaIndex(defaultMediaIndex, availableMediaIndices);
            int resolvedMediaIndex = ResolveKnownMediaIndex(mediaIndex, availableMediaIndices);
            int alternateMediaA = ResolveAlternateMediaIndex(resolvedDefaultMediaIndex, availableMediaIndices, 1);
            int alternateMediaB = ResolveAlternateMediaIndex(resolvedDefaultMediaIndex, availableMediaIndices, 2);
            if (resolvedMediaIndex == alternateMediaA && alternateMediaA != resolvedDefaultMediaIndex)
            {
                return 0;
            }

            if (resolvedMediaIndex == alternateMediaB
                && alternateMediaB != resolvedDefaultMediaIndex
                && alternateMediaB != alternateMediaA)
            {
                return 2;
            }

            return 1;
        }

        internal static Rectangle ResolveChatBounds(int mediaIndex, int defaultMediaIndex, IReadOnlyList<int> availableMediaIndices)
        {
            return ResolveChatVariantKey(mediaIndex, defaultMediaIndex, availableMediaIndices) switch
            {
                0 => MapleTvWindow.StarChatTextBounds,
                2 => MapleTvWindow.HeartChatTextBounds,
                _ => MapleTvWindow.DefaultChatTextBounds
            };
        }
    }

    internal sealed record MapleTvItemProfile(int ItemId, string ItemName, int MediaIndex, int DurationMs, MapleTvAudienceMode AudienceMode, bool MirrorsToChat)
    {
        private const int DefaultItemDurationMs = 15000;

        internal static MapleTvItemProfile CreateDefault(int itemId, string itemName, int defaultMediaIndex, int defaultDurationMs, IReadOnlyList<int> availableMediaIndices = null)
        {
            string name = string.IsNullOrWhiteSpace(itemName) ? "Maple TV" : itemName.Trim();
            return new MapleTvItemProfile(
                Math.Max(0, itemId),
                name,
                MapleTvMediaIndexResolver.ResolveKnownMediaIndex(defaultMediaIndex, availableMediaIndices),
                defaultDurationMs,
                MapleTvAudienceMode.Flexible,
                false);
        }

        internal static MapleTvItemProfile Resolve(int itemId, string itemName, string itemDescription, int defaultItemId, string defaultItemName, int defaultMediaIndex, IReadOnlyList<int> availableMediaIndices = null)
        {
            if (itemId <= 0)
            {
                return CreateDefault(defaultItemId, defaultItemName, defaultMediaIndex, DefaultItemDurationMs, availableMediaIndices);
            }

            int normalizedDefaultMediaIndex = MapleTvMediaIndexResolver.ResolveKnownMediaIndex(defaultMediaIndex, availableMediaIndices);
            int alternateMediaA = MapleTvMediaIndexResolver.ResolveAlternateMediaIndex(normalizedDefaultMediaIndex, availableMediaIndices, 1);
            int alternateMediaB = MapleTvMediaIndexResolver.ResolveAlternateMediaIndex(normalizedDefaultMediaIndex, availableMediaIndices, 2);
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
                : normalizedDefaultMediaIndex;

            MapleTvItemProfile inferredProfile = new(itemId, resolvedName, mediaIndex, durationMs, audienceMode, isMegassenger);
            return itemId switch
            {
                5075000 => inferredProfile with { MediaIndex = normalizedDefaultMediaIndex, DurationMs = 15000, AudienceMode = MapleTvAudienceMode.Flexible, MirrorsToChat = false },
                5075001 => inferredProfile with { MediaIndex = alternateMediaA, DurationMs = 30000, AudienceMode = MapleTvAudienceMode.SenderOnly, MirrorsToChat = false },
                5075002 => inferredProfile with { MediaIndex = alternateMediaB, DurationMs = 60000, AudienceMode = MapleTvAudienceMode.ReceiverRequired, MirrorsToChat = false },
                5075003 => inferredProfile with { MediaIndex = normalizedDefaultMediaIndex, DurationMs = 15000, AudienceMode = MapleTvAudienceMode.Flexible, MirrorsToChat = true },
                5075004 => inferredProfile with { MediaIndex = alternateMediaA, DurationMs = 15000, AudienceMode = MapleTvAudienceMode.Flexible, MirrorsToChat = true },
                5075005 => inferredProfile with { MediaIndex = alternateMediaB, DurationMs = 15000, AudienceMode = MapleTvAudienceMode.Flexible, MirrorsToChat = true },
                _ => CreateDefault(itemId, resolvedName, normalizedDefaultMediaIndex, DefaultItemDurationMs, availableMediaIndices)
            };
        }

        internal static bool IsKnownMapleTvCashItem(int itemId)
        {
            return itemId >= 5075000 && itemId <= 5075005;
        }
    }
}
