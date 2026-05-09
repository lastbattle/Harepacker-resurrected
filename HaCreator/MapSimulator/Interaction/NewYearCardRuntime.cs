using MapleLib.PacketLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using BinaryWriter = MapleLib.PacketLib.PacketWriter;
namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class NewYearCardRuntime
    {
        internal const int SendCardOpcode = 183;
        internal const int DefaultItemId = 4300000;
        internal const int DefaultInventoryPosition = 1;
        internal const int MemoWrapWidth = 150;
        internal const int SenderWindowX = 221;
        internal const int SenderWindowY = 206;
        internal const int SenderWindowWidth = 518;
        internal const int SenderWindowHeight = 188;
        internal const int ReadWindowX = 286;
        internal const int ReadWindowY = 168;
        internal const int ReadWindowWidth = 227;
        internal const int ReadWindowHeight = 263;
        internal const int SenderSearchButtonX = 295;
        internal const int SenderSearchButtonY = 65;
        internal const int SenderOkButtonX = 124;
        internal const int SenderOkButtonY = 164;
        internal const int SenderCancelButtonX = 181;
        internal const int SenderCancelButtonY = 164;
        internal const int TargetEditX = 46;
        internal const int TargetEditY = 66;
        internal const int TargetEditWidth = 243;
        internal const int TargetEditHeight = 15;
        internal const int MemoEditX = 13;
        internal const int MemoEditY = 105;
        internal const int MemoEditWidth = 333;
        internal const int MemoEditHeight = 45;
        internal const int MemoEditMaxLineWidth = 323;
        internal const int MemoEditFontHeight = 14;
        internal const int NameListScrollBarX = 498;
        internal const int NameListScrollBarY = 7;
        internal const int NameListScrollBarHeight = 93;
        internal const int NameListScrollBarWheelRange = 100;
        internal const int NameListScrollBarRange = 100;
        internal const int SearchResultX = 353;
        internal const int SearchResultY = 0;
        internal const int SearchResultWidth = 165;
        internal const int SearchResultHeight = 106;
        internal const int SearchResultFirstTextX = 368;
        internal const int SearchResultFirstTextY = 24;
        internal const int SearchResultRowHeight = 15;
        internal const int SearchResultBottomY = 99;
        internal const int SearchResultVisibleRows = 5;
        internal const int ReadCloseButtonX = 91;
        internal const int ReadCloseButtonY = 231;
        internal const int SenderTargetMaxChars = 12;
        internal const int SenderMemoMaxChars = 120;
        internal const int SenderMemoMaxRows = 3;
        internal const string SenderBackgroundPath = "UI/UIWindow.img/NewYearsCard/backgrnd";
        internal const string SenderSearchResultBackgroundPath = "UI/UIWindow.img/NewYearsCard/backgrnd2";
        internal const string ReadBackgroundPath = "UI/UIWindow.img/NewYearsCard/backgrnd3";

        private readonly List<string> _searchResults = new();
        private string _senderName = "Player";
        private string _targetName = string.Empty;
        private string _memo = "Happy New Year!";
        private string _lastStatus = "New Year Card sender/read dialog runtime idle.";
        private int _inventoryPosition = DefaultInventoryPosition;
        private int _itemId = DefaultItemId;
        private int _selectedSearchResultIndex;
        private int _firstVisibleSearchResultIndex;

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
                _selectedSearchResultIndex,
                _firstVisibleSearchResultIndex,
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

        internal string ConfigureReadView(string senderName, string targetName, string memo)
        {
            UpdateLocalSender(senderName);
            _targetName = NormalizeName(targetName, "Recipient");
            _memo = NormalizeMemo(memo);
            _lastStatus = $"CUINewYearCardDlg configured the read owner for sender '{_senderName}' and receiver '{DisplayTargetName}'.";
            return _lastStatus;
        }

        internal string Search(string query)
        {
            string normalizedQuery = NormalizeName(query, string.Empty);
            _searchResults.Clear();
            _selectedSearchResultIndex = 0;
            _firstVisibleSearchResultIndex = 0;

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
            _selectedSearchResultIndex = index;
            EnsureSelectedSearchResultVisible();
            message = $"Selected New Year Card receiver '{_targetName}' from CNewYearCardReceiverSearchResult.";
            _lastStatus = message;
            return true;
        }

        internal string ScrollSearchResults(int delta)
        {
            int maxFirstVisibleIndex = GetMaxFirstVisibleSearchResultIndex();
            _firstVisibleSearchResultIndex = Math.Clamp(
                _firstVisibleSearchResultIndex + delta,
                0,
                maxFirstVisibleIndex);
            _lastStatus = $"CNewYearCardReceiverSearchResult name-list scrollbar moved to first row {_firstVisibleSearchResultIndex + 1} of {_searchResults.Count}.";
            return _lastStatus;
        }

        internal string ApplyCompletion(NewYearCardCompletionKind kind, string targetName = null, string senderName = null, string memo = null)
        {
            switch (kind)
            {
                case NewYearCardCompletionKind.SendSuccess:
                    string target = NormalizeName(targetName, _targetName);
                    if (!string.IsNullOrEmpty(target))
                    {
                        _targetName = target;
                    }

                    _lastStatus = NewYearCardClientText.ResolveSendSuccessNotice(DisplayTargetName);
                    return _lastStatus;

                case NewYearCardCompletionKind.ReceiveSuccess:
                    if (!string.IsNullOrWhiteSpace(senderName) || !string.IsNullOrWhiteSpace(targetName) || memo != null)
                    {
                        ConfigureReadView(senderName ?? _senderName, targetName ?? _targetName, memo ?? _memo);
                    }

                    _lastStatus = NewYearCardClientText.ResolveReceiveSuccessNotice();
                    return _lastStatus;

                case NewYearCardCompletionKind.DeleteSuccess:
                    _lastStatus = NewYearCardClientText.ResolveDeleteSuccessNotice();
                    return _lastStatus;

                case NewYearCardCompletionKind.NoFreeSlot:
                case NewYearCardCompletionKind.NoCardToSend:
                case NewYearCardCompletionKind.WrongInventory:
                case NewYearCardCompletionKind.TargetNotFound:
                case NewYearCardCompletionKind.IncoherentData:
                case NewYearCardCompletionKind.DatabaseError:
                case NewYearCardCompletionKind.UnknownError:
                    _lastStatus = NewYearCardClientText.ResolveFailureNotice(kind);
                    return _lastStatus;

                default:
                    _lastStatus = $"Unhandled New Year Card completion kind {kind}.";
                    return _lastStatus;
            }
        }

        internal NewYearCardSendRequest BuildSendRequest()
        {
            byte[] payload = EncodeSendPayload(_inventoryPosition, _itemId, _targetName, _memo);
            return new NewYearCardSendRequest(SendCardOpcode, payload, _inventoryPosition, _itemId, _targetName, _memo);
        }

        internal bool TryBuildSendRequest(out NewYearCardSendRequest request, out string message, bool confirmedEmptyMemo = false)
        {
            request = null;
            if (string.IsNullOrWhiteSpace(_targetName))
            {
                message = NewYearCardClientText.ResolveFailureNotice(NewYearCardCompletionKind.TargetNotFound);
                _lastStatus = message;
                return false;
            }

            if (string.Equals(_senderName, _targetName, StringComparison.OrdinalIgnoreCase))
            {
                message = NewYearCardClientText.ResolveCannotSendToSelfNotice();
                _lastStatus = message;
                return false;
            }

            if (string.IsNullOrEmpty(_memo) && !confirmedEmptyMemo)
            {
                message = NewYearCardClientText.ResolveEmptyMemoPrompt();
                _lastStatus = message;
                return false;
            }

            request = BuildSendRequest();
            message = $"CUINewYearCardSenderDlg::_SendNewYearCard prepared opcode {SendCardOpcode} for '{DisplayTargetName}'.";
            _lastStatus = message;
            return true;
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

        private int GetMaxFirstVisibleSearchResultIndex()
        {
            return Math.Max(0, _searchResults.Count - SearchResultVisibleRows);
        }

        private void EnsureSelectedSearchResultVisible()
        {
            int maxFirstVisibleIndex = GetMaxFirstVisibleSearchResultIndex();
            if (_selectedSearchResultIndex < _firstVisibleSearchResultIndex)
            {
                _firstVisibleSearchResultIndex = _selectedSearchResultIndex;
            }
            else if (_selectedSearchResultIndex >= _firstVisibleSearchResultIndex + SearchResultVisibleRows)
            {
                _firstVisibleSearchResultIndex = _selectedSearchResultIndex - SearchResultVisibleRows + 1;
            }

            _firstVisibleSearchResultIndex = Math.Clamp(_firstVisibleSearchResultIndex, 0, maxFirstVisibleIndex);
        }

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
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            return normalized.Length <= SenderTargetMaxChars
                ? normalized
                : normalized.Substring(0, SenderTargetMaxChars);
        }

        private static string NormalizeMemo(string value)
        {
            string normalized = (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            return normalized.Length <= SenderMemoMaxChars
                ? normalized
                : normalized.Substring(0, SenderMemoMaxChars);
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
                    if (line.Length > 0 && EstimateBasicFontPixelWidth(candidate) > MemoWrapWidth)
                    {
                        lines.Add(line);
                        AddOversizedWordSegments(lines, word, MemoWrapWidth, ref line);
                    }
                    else
                    {
                        AddOversizedWordSegments(lines, candidate, MemoWrapWidth, ref line);
                    }
                }

                if (!string.IsNullOrEmpty(line))
                {
                    lines.Add(line);
                }
            }

            return lines.Take(6).ToArray();
        }

        private static void AddOversizedWordSegments(List<string> lines, string candidate, int maxWidth, ref string line)
        {
            if (EstimateBasicFontPixelWidth(candidate) <= maxWidth)
            {
                line = candidate;
                return;
            }

            string segment = string.Empty;
            foreach (char c in candidate)
            {
                string next = segment + c;
                if (segment.Length > 0 && EstimateBasicFontPixelWidth(next) > maxWidth)
                {
                    lines.Add(segment);
                    segment = c.ToString();
                }
                else
                {
                    segment = next;
                }
            }

            line = segment;
        }

        internal static int EstimateBasicFontPixelWidth(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int width = 0;
            foreach (char c in text)
            {
                width += c switch
                {
                    ' ' => 4,
                    '\t' => 12,
                    'i' or 'l' or 'I' or '!' or '.' or ',' or ':' or ';' or '\'' => 3,
                    'm' or 'w' or 'M' or 'W' or '@' => 9,
                    >= '\u2E80' => 12,
                    _ => 6
                };
            }

            return width;
        }
    }

    internal sealed record NewYearCardSenderSnapshot(
        int InventoryPosition,
        int ItemId,
        string TargetName,
        string Memo,
        IReadOnlyList<string> SearchResults,
        int SelectedSearchResultIndex,
        int FirstVisibleSearchResultIndex,
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

    internal enum NewYearCardCompletionKind
    {
        SendSuccess,
        ReceiveSuccess,
        DeleteSuccess,
        NoFreeSlot,
        NoCardToSend,
        WrongInventory,
        TargetNotFound,
        IncoherentData,
        DatabaseError,
        UnknownError
    }
}
