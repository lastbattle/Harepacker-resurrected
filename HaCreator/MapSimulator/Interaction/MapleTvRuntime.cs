using System;
using System.Collections.Generic;
using System.Linq;

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
        private const int DefaultDurationMs = 12000;
        private const int MinDurationMs = 1000;
        private const int MaxDurationMs = 60000;
        private const int DisplayLineCount = 5;

        private readonly string[] _draftLines = new string[DisplayLineCount];
        private readonly string[] _displayLines = new string[DisplayLineCount];
        private string _senderName = "Player";
        private string _receiverName = string.Empty;
        private string _itemName = "Maple TV";
        private string _statusMessage = "Prepare a MapleTV draft, then publish it through the simulator window or /mapletv.";
        private int _itemId;
        private int _messageType;
        private int _draftDurationMs = DefaultDurationMs;
        private int _messageStartedAt = int.MinValue;
        private bool _useReceiver;
        private bool _showMessage;
        private bool _queueExists;
        private bool _isSelfMessage = true;

        internal MapleTvRuntime()
        {
            _draftLines[0] = "MapSimulator now exposes MapleTV parity.";
            _draftLines[1] = "Use /mapletv line <1-5> <text> to edit lines.";
            _draftLines[2] = "Use /mapletv receiver <name> to target someone.";
            _draftLines[3] = "Use /mapletv item <itemId> to swap the media item.";
            _draftLines[4] = "Use /mapletv set to start the timed display.";
        }

        internal void UpdateLocalContext(string playerName)
        {
            if (!string.IsNullOrWhiteSpace(playerName))
            {
                _senderName = playerName.Trim();
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
            _queueExists = false;
            _messageStartedAt = int.MinValue;
            _statusMessage = "MapleTV display interval elapsed.";
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
                DraftLines = Array.AsReadOnly((string[])_draftLines.Clone()),
                DisplayLines = Array.AsReadOnly((string[])_displayLines.Clone()),
                StatusMessage = _statusMessage,
                UseReceiver = _useReceiver,
                IsShowingMessage = _showMessage,
                QueueExists = _queueExists,
                IsSelfMessage = _isSelfMessage,
                MessageType = _messageType,
                RemainingMs = remainingMs,
                TotalWaitMs = _draftDurationMs,
                CanPublish = _draftLines.Any(line => !string.IsNullOrWhiteSpace(line)),
                CanClear = _showMessage || _queueExists
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
            return $"MapleTV {mode}: {snapshot.SenderName} -> {receiver}, item {snapshot.ItemId} ({snapshot.ItemName}), {timer}. {snapshot.StatusMessage}";
        }

        internal string ToggleReceiverMode()
        {
            _useReceiver = !_useReceiver;
            _isSelfMessage = !_useReceiver;
            _messageType = _useReceiver ? 2 : 0;
            if (!_useReceiver)
            {
                _receiverName = string.Empty;
                _statusMessage = "MapleTV receiver field disabled. Broadcast will target the sender only.";
            }
            else
            {
                _statusMessage = "MapleTV receiver field enabled. Set a recipient with /mapletv receiver <name>.";
            }

            return _statusMessage;
        }

        internal string SetReceiver(string receiverName)
        {
            if (string.IsNullOrWhiteSpace(receiverName) ||
                string.Equals(receiverName, "self", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(receiverName, "clear", StringComparison.OrdinalIgnoreCase))
            {
                _useReceiver = false;
                _receiverName = string.Empty;
                _isSelfMessage = true;
                _messageType = 0;
                _statusMessage = "MapleTV receiver cleared. Broadcast will target the sender only.";
                return _statusMessage;
            }

            _useReceiver = true;
            _receiverName = receiverName.Trim();
            _isSelfMessage = false;
            _messageType = 2;
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
            _statusMessage = $"MapleTV sender set to {_senderName}.";
            return _statusMessage;
        }

        internal string SetItem(int itemId, string itemName)
        {
            if (itemId < 0)
            {
                return $"Invalid MapleTV item ID: {itemId}";
            }

            _itemId = itemId;
            _itemName = string.IsNullOrWhiteSpace(itemName)
                ? (itemId > 0 ? $"Item #{itemId}" : "Maple TV")
                : itemName.Trim();
            _statusMessage = itemId > 0
                ? $"MapleTV media item set to {_itemName} ({itemId})."
                : "MapleTV media item reset to the default simulator label.";
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

        internal string LoadSample(string senderName, string locationSummary)
        {
            UpdateLocalContext(senderName);
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

            if (_draftLines.All(line => string.IsNullOrWhiteSpace(line)))
            {
                return "At least one MapleTV draft line is required before sending.";
            }

            Array.Copy(_draftLines, _displayLines, DisplayLineCount);
            _showMessage = true;
            _queueExists = true;
            _messageStartedAt = currentTick;
            _isSelfMessage = !_useReceiver;
            _messageType = _useReceiver ? 2 : 0;
            _statusMessage = $"MapleTV message set for {_draftDurationMs / 1000f:0.0}s.";
            return _statusMessage;
        }

        internal string OnClearMessage()
        {
            _showMessage = false;
            _queueExists = false;
            _messageStartedAt = int.MinValue;
            Array.Clear(_displayLines, 0, _displayLines.Length);
            _statusMessage = "MapleTV display cleared.";
            return _statusMessage;
        }

        internal string OnSendMessageResult(MapleTvSendResultKind result)
        {
            _statusMessage = result switch
            {
                MapleTvSendResultKind.Success => "MapleTV send request accepted.",
                MapleTvSendResultKind.Busy => "MapleTV send request rejected because another broadcast is already active.",
                MapleTvSendResultKind.RecipientOffline => "MapleTV send request rejected because the target recipient is unavailable.",
                _ => "MapleTV send request failed."
            };

            return _statusMessage;
        }
    }

    internal sealed class MapleTvSnapshot
    {
        public string SenderName { get; init; } = string.Empty;
        public string ReceiverName { get; init; } = string.Empty;
        public string ItemName { get; init; } = string.Empty;
        public int ItemId { get; init; }
        public IReadOnlyList<string> DraftLines { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> DisplayLines { get; init; } = Array.Empty<string>();
        public string StatusMessage { get; init; } = string.Empty;
        public bool UseReceiver { get; init; }
        public bool IsShowingMessage { get; init; }
        public bool QueueExists { get; init; }
        public bool IsSelfMessage { get; init; }
        public int MessageType { get; init; }
        public int RemainingMs { get; init; }
        public int TotalWaitMs { get; init; }
        public bool CanPublish { get; init; }
        public bool CanClear { get; init; }
    }
}
