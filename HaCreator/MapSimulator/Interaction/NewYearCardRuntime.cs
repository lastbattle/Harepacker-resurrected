using MapleLib.PacketLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class NewYearCardRuntime
    {
        internal const int SendCardOpcode = 183;
        internal const int DefaultItemId = 4300000;
        internal const int DefaultInventoryPosition = 1;
        internal const int MemoWrapWidth = 150;

        private readonly List<string> _searchResults = new();
        private string _senderName = "Player";
        private string _targetName = string.Empty;
        private string _memo = "Happy New Year!";
        private string _lastStatus = "New Year Card sender/read dialog runtime idle.";
        private int _inventoryPosition = DefaultInventoryPosition;
        private int _itemId = DefaultItemId;

        internal NewYearCardRuntime()
        {
            _searchResults.AddRange(new[] { "ExplorerGM", "MapleAdmin", "Cassandra" });
        }

        internal void UpdateLocalSender(string senderName)
        {
            if (!string.IsNullOrWhiteSpace(senderName))
            {
                _senderName = senderName.Trim();
            }
        }

        internal NewYearCardSenderSnapshot GetSenderSnapshot()
        {
            return new NewYearCardSenderSnapshot(
                _inventoryPosition,
                _itemId,
                _targetName,
                _memo,
                _searchResults.ToArray(),
                _lastStatus);
        }

        internal NewYearCardReadSnapshot GetReadSnapshot()
        {
            return new NewYearCardReadSnapshot(
                $"From: {_senderName}",
                $"To: {NormalizeName(_targetName, "Recipient")}",
                _memo,
                WrapMemoForReadDialog(_memo));
        }

        internal string ConfigureDraft(int inventoryPosition, int itemId, string targetName, string memo)
        {
            _inventoryPosition = Math.Max(0, inventoryPosition);
            _itemId = itemId > 0 ? itemId : DefaultItemId;
            _targetName = NormalizeName(targetName, string.Empty);
            _memo = NormalizeMemo(memo);
            _lastStatus = $"CUINewYearCardSenderDlg draft targets '{DisplayTargetName}' with item {_itemId} at inventory position {_inventoryPosition}.";
            return _lastStatus;
        }

        internal string SetTarget(string targetName)
        {
            _targetName = NormalizeName(targetName, string.Empty);
            _lastStatus = $"CUINewYearCardSenderDlg target edit now contains '{DisplayTargetName}'.";
            return _lastStatus;
        }

        internal string SetMemo(string memo)
        {
            _memo = NormalizeMemo(memo);
            _lastStatus = $"CUINewYearCardSenderDlg multiline memo edit now has {_memo.Length} character(s).";
            return _lastStatus;
        }

        internal string Search(string query)
        {
            string normalizedQuery = NormalizeName(query, string.Empty);
            _searchResults.Clear();

            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                _searchResults.AddRange(new[] { "ExplorerGM", "MapleAdmin", "Cassandra" });
            }
            else
            {
                _searchResults.Add(normalizedQuery);
                _searchResults.Add($"{normalizedQuery}Jr");
                _searchResults.Add($"{normalizedQuery}II");
            }

            _lastStatus = $"CNewYearCardReceiverSearchResult refreshed {_searchResults.Count} candidate name(s) for '{(string.IsNullOrWhiteSpace(normalizedQuery) ? "default" : normalizedQuery)}'.";
            return _lastStatus;
        }

        internal bool TrySelectSearchResult(int index, out string message)
        {
            if (index < 0 || index >= _searchResults.Count)
            {
                message = $"Search-result index {index + 1} is outside the current CNewYearCardReceiverSearchResult list.";
                _lastStatus = message;
                return false;
            }

            _targetName = _searchResults[index];
            message = $"Selected New Year Card receiver '{_targetName}' from CNewYearCardReceiverSearchResult.";
            _lastStatus = message;
            return true;
        }

        internal NewYearCardSendRequest BuildSendRequest()
        {
            byte[] payload = EncodeSendPayload(_inventoryPosition, _itemId, _targetName, _memo);
            return new NewYearCardSendRequest(SendCardOpcode, payload, _inventoryPosition, _itemId, _targetName, _memo);
        }

        internal void MarkSendDispatched(string dispatchStatus)
        {
            _lastStatus = $"CUINewYearCardSenderDlg::_SendNewYearCard emitted opcode {SendCardOpcode} for '{DisplayTargetName}'. {dispatchStatus}";
        }

        internal void MarkSendRejected(string reason)
        {
            _lastStatus = reason;
        }

        internal string DescribeStatus()
        {
            return $"New Year Card runtime: sender='{_senderName}', target='{DisplayTargetName}', item={_itemId}, invenPOS={_inventoryPosition}, memoChars={_memo.Length}, searchResults={_searchResults.Count}. {_lastStatus}";
        }

        private string DisplayTargetName => string.IsNullOrWhiteSpace(_targetName) ? "(empty)" : _targetName;

        private static byte[] EncodeSendPayload(int inventoryPosition, int itemId, string targetName, string memo)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            writer.Write((byte)0);
            writer.Write((short)Math.Max(0, inventoryPosition));
            writer.Write(itemId);
            WriteMapleString(writer, NormalizeName(targetName, string.Empty));
            WriteMapleString(writer, NormalizeMemo(memo));
            writer.Flush();
            return stream.ToArray();
        }

        private static void WriteMapleString(BinaryWriter writer, string value)
        {
            byte[] bytes = Encoding.Default.GetBytes(value ?? string.Empty);
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }

        private static string NormalizeName(string value, string fallback)
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            return normalized ?? string.Empty;
        }

        private static string NormalizeMemo(string value)
        {
            return (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        }

        private static IReadOnlyList<string> WrapMemoForReadDialog(string memo)
        {
            string normalized = NormalizeMemo(memo);
            if (string.IsNullOrEmpty(normalized))
            {
                return Array.Empty<string>();
            }

            List<string> lines = new();
            foreach (string paragraph in normalized.Split('\n'))
            {
                string[] words = paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string line = string.Empty;
                foreach (string word in words)
                {
                    string candidate = string.IsNullOrEmpty(line) ? word : $"{line} {word}";
                    if (line.Length > 0 && candidate.Length > 28)
                    {
                        lines.Add(line);
                        line = word;
                    }
                    else
                    {
                        line = candidate;
                    }
                }

                if (!string.IsNullOrEmpty(line))
                {
                    lines.Add(line);
                }
            }

            return lines.Take(6).ToArray();
        }
    }

    internal sealed record NewYearCardSenderSnapshot(
        int InventoryPosition,
        int ItemId,
        string TargetName,
        string Memo,
        IReadOnlyList<string> SearchResults,
        string LastStatus);

    internal sealed record NewYearCardReadSnapshot(
        string SenderLine,
        string TargetLine,
        string Memo,
        IReadOnlyList<string> WrappedMemoLines);

    internal sealed record NewYearCardSendRequest(
        int Opcode,
        byte[] Payload,
        int InventoryPosition,
        int ItemId,
        string TargetName,
        string Memo);
}
