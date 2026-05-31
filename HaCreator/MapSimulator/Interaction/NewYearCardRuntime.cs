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
        internal const byte SendCardSubtype = 0;
        internal const byte ReadArrivedCardSubtype = 1;
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
        private readonly List<NewYearCardRecordSnapshot> _localRecords = new();
        private string _senderName = "Player";
        private string _targetName = string.Empty;
        private string _memo = "Happy New Year!";
        private string _lastStatus = "New Year Card sender/read dialog runtime idle.";
        private int _inventoryPosition = DefaultInventoryPosition;
        private int _itemId = DefaultItemId;
        private int _selectedSearchResultIndex;
        private int _firstVisibleSearchResultIndex;
        private NewYearCardResultSnapshot _lastResultSnapshot = NewYearCardResultSnapshot.Empty;

        internal NewYearCardRuntime()
        {
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

        internal NewYearCardResultSnapshot LastResultSnapshot => _lastResultSnapshot;

        internal IReadOnlyList<NewYearCardRecordSnapshot> LocalRecords => _localRecords.ToArray();

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
            return Search(query, null);
        }

        internal string Search(string query, IEnumerable<string> contactNames)
        {
            string normalizedQuery = NormalizeName(query, string.Empty);
            _searchResults.Clear();
            _selectedSearchResultIndex = 0;
            _firstVisibleSearchResultIndex = 0;

            foreach (string candidate in ResolveSearchCandidates(normalizedQuery, contactNames, GetLocalRecordParticipantNames()))
            {
                if (_searchResults.Count >= NameListScrollBarRange)
                {
                    break;
                }

                _searchResults.Add(candidate);
            }

            _lastStatus = $"CNewYearCardReceiverSearchResult refreshed {_searchResults.Count} friend/guild candidate name(s) for '{(string.IsNullOrWhiteSpace(normalizedQuery) ? "default" : normalizedQuery)}'.";
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

        internal bool TryConfigureReadViewFromLocalRecord(int serialNumber, out string message)
        {
            NewYearCardRecordSnapshot record = _localRecords.FirstOrDefault(existing => existing.SerialNumber == serialNumber);
            if (record.Equals(default(NewYearCardRecordSnapshot)))
            {
                message = $"No packet-owned New Year Card record with serial {serialNumber} is available for CUINewYearCardDlg.";
                _lastStatus = message;
                return false;
            }

            ConfigureReadView(record.SenderName, record.ReceiverName, record.Content);
            message = $"CUINewYearCardDlg opened packet-owned New Year Card record serial {serialNumber} from '{record.SenderName}'.";
            _lastStatus = message;
            return true;
        }

        internal string ApplyCompletion(NewYearCardCompletionKind kind, string targetName = null, string senderName = null, string memo = null)
        {
            NewYearCardRecordSnapshot record = default;
            switch (kind)
            {
                case NewYearCardCompletionKind.SendSuccess:
                    string target = NormalizeName(targetName, _targetName);
                    if (!string.IsNullOrEmpty(target))
                    {
                        _targetName = target;
                    }

                    _lastStatus = NewYearCardClientText.ResolveSendSuccessNotice(DisplayTargetName);
                    record = new NewYearCardRecordSnapshot(0, 0, senderName ?? _senderName, false, 0, 0, _targetName, false, false, 0, memo ?? _memo);
                    _lastResultSnapshot = NewYearCardResultSnapshot.FromCompletion(NewYearCardResultSubtype.None, kind, NewYearCardFailureReason.None, record, 0, _lastStatus);
                    return _lastStatus;

                case NewYearCardCompletionKind.ReceiveSuccess:
                    if (!string.IsNullOrWhiteSpace(senderName) || !string.IsNullOrWhiteSpace(targetName) || memo != null)
                    {
                        ConfigureReadView(senderName ?? _senderName, targetName ?? _targetName, memo ?? _memo);
                    }

                    _lastStatus = NewYearCardClientText.ResolveReceiveSuccessNotice();
                    record = new NewYearCardRecordSnapshot(0, 0, _senderName, false, 0, 0, _targetName, false, false, 0, _memo);
                    _lastResultSnapshot = NewYearCardResultSnapshot.FromCompletion(NewYearCardResultSubtype.None, kind, NewYearCardFailureReason.None, record, 0, _lastStatus);
                    return _lastStatus;

                case NewYearCardCompletionKind.DeleteSuccess:
                    _lastStatus = NewYearCardClientText.ResolveDeleteSuccessNotice();
                    _lastResultSnapshot = NewYearCardResultSnapshot.FromCompletion(NewYearCardResultSubtype.None, kind, NewYearCardFailureReason.None, default, 0, _lastStatus);
                    return _lastStatus;

                case NewYearCardCompletionKind.CannotSendToSelf:
                case NewYearCardCompletionKind.NoFreeSlot:
                case NewYearCardCompletionKind.NoCardToSend:
                case NewYearCardCompletionKind.WrongInventory:
                case NewYearCardCompletionKind.TargetNotFound:
                case NewYearCardCompletionKind.IncoherentData:
                case NewYearCardCompletionKind.DatabaseError:
                case NewYearCardCompletionKind.UnknownError:
                    _lastStatus = NewYearCardClientText.ResolveFailureNotice(kind);
                    _lastResultSnapshot = NewYearCardResultSnapshot.FromCompletion(NewYearCardResultSubtype.Failure, kind, NewYearCardResultPacketCodec.ToFailureReason(kind), default, 0, _lastStatus);
                    return _lastStatus;

                default:
                    _lastStatus = $"Unhandled New Year Card completion kind {kind}.";
                    _lastResultSnapshot = NewYearCardResultSnapshot.FromCompletion(NewYearCardResultSubtype.None, kind, NewYearCardFailureReason.None, default, 0, _lastStatus);
                    return _lastStatus;
            }
        }

        internal string ApplyResultPacket(NewYearCardResultPacket packet)
        {
            if (packet == null)
            {
                _lastStatus = "CWvsContext::OnNewYearCardRes rejected an empty packet.";
                _lastResultSnapshot = NewYearCardResultSnapshot.Empty with { Message = _lastStatus };
                return _lastStatus;
            }

            switch (packet.Subtype)
            {
                case NewYearCardResultSubtype.SendSuccess:
                    string target = NormalizeName(packet.Record.ReceiverName, _targetName);
                    if (!string.IsNullOrEmpty(target))
                    {
                        _targetName = target;
                    }

                    AddOrReplaceLocalRecord(packet.Record);
                    _lastStatus = NewYearCardClientText.ResolveSendSuccessNotice(DisplayTargetName);
                    break;

                case NewYearCardResultSubtype.ReceiveSuccess:
                    ConfigureReadView(packet.Record.SenderName, packet.Record.ReceiverName, packet.Record.Content);
                    AddOrReplaceLocalRecord(packet.Record);
                    _lastStatus = NewYearCardClientText.ResolveReceiveSuccessNotice();
                    break;

                case NewYearCardResultSubtype.DeleteSuccess:
                    RemoveLocalRecord(packet.SerialNumber);
                    _lastStatus = NewYearCardClientText.ResolveDeleteSuccessNotice();
                    break;

                case NewYearCardResultSubtype.SendFailure:
                case NewYearCardResultSubtype.ReceiveFailure:
                case NewYearCardResultSubtype.DeleteFailure:
                case NewYearCardResultSubtype.ReadFailure:
                    _lastStatus = NewYearCardClientText.ResolveFailureNotice(packet.CompletionKind);
                    break;

                case NewYearCardResultSubtype.ArrivalList:
                case NewYearCardResultSubtype.ArrivalSingle:
                    _lastStatus = packet.Arrivals.Count == 1
                        ? $"CWvsContext::OnNewYearCardRes created one New Year Card arrival prompt from '{packet.Arrivals[0].SenderName}'."
                        : $"CWvsContext::OnNewYearCardRes created {packet.Arrivals.Count} New Year Card arrival prompt(s).";
                    break;

                case NewYearCardResultSubtype.RemoteRecordAdd:
                    _lastStatus = $"CWvsContext::OnNewYearCardRes routed New Year Card record add serial {packet.SerialNumber} to CUserPool owner {packet.RemoteUserId}.";
                    break;

                case NewYearCardResultSubtype.RemoteRecordRemove:
                    _lastStatus = $"CWvsContext::OnNewYearCardRes routed New Year Card record remove serial {packet.SerialNumber} to CUserPool.";
                    break;

                default:
                    _lastStatus = $"CWvsContext::OnNewYearCardRes preserved unknown subtype {(byte)packet.Subtype}.";
                    break;
            }

            _lastResultSnapshot = new NewYearCardResultSnapshot(
                packet.Subtype,
                packet.CompletionKind,
                packet.FailureReason,
                packet.Record,
                packet.SerialNumber,
                packet.RemoteUserId,
                packet.Arrivals,
                _lastStatus);
            return _lastStatus;
        }

        internal bool TryApplyResultPayload(byte[] payload, out string message)
        {
            if (!NewYearCardResultPacketCodec.TryDecode(payload, out NewYearCardResultPacket packet, out string decodeError))
            {
                message = decodeError;
                _lastStatus = decodeError;
                _lastResultSnapshot = NewYearCardResultSnapshot.Empty with { Message = decodeError };
                return false;
            }

            message = ApplyResultPacket(packet);
            return true;
        }

        internal NewYearCardSendRequest BuildSendRequest()
        {
            byte[] payload = EncodeSendPayload(_inventoryPosition, _itemId, _targetName, _memo);
            return new NewYearCardSendRequest(SendCardOpcode, payload, _inventoryPosition, _itemId, _targetName, _memo);
        }

        internal NewYearCardReadRequest BuildReadRequest(int serialNumber)
        {
            byte[] payload = EncodeReadPayload(serialNumber);
            return new NewYearCardReadRequest(SendCardOpcode, payload, serialNumber);
        }

        internal bool TryBuildReadRequest(int serialNumber, out NewYearCardReadRequest request, out string message)
        {
            request = null;
            if (serialNumber <= 0)
            {
                message = "CUIFadeYesNo New Year Card arrival prompt requires a positive card serial number.";
                _lastStatus = message;
                return false;
            }

            request = BuildReadRequest(serialNumber);
            message = $"CUIFadeYesNo New Year Card arrival prompt prepared opcode {SendCardOpcode} read request for serial {serialNumber}.";
            _lastStatus = message;
            return true;
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

        internal void MarkReadRequestDispatched(int serialNumber, string dispatchStatus)
        {
            _lastStatus = $"CUIFadeYesNo New Year Card arrival prompt emitted opcode {SendCardOpcode} read request for serial {serialNumber}. {dispatchStatus}";
        }

        internal void MarkSendRejected(string reason)
        {
            _lastStatus = reason;
        }

        internal string DescribeStatus()
        {
            return $"New Year Card runtime: sender='{_senderName}', target='{DisplayTargetName}', item={_itemId}, invenPOS={_inventoryPosition}, memoChars={_memo.Length}, searchResults={_searchResults.Count}, localRecords={_localRecords.Count}. {_lastStatus}";
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

        private void AddOrReplaceLocalRecord(NewYearCardRecordSnapshot record)
        {
            if (record.Equals(default(NewYearCardRecordSnapshot)))
            {
                return;
            }

            int index = _localRecords.FindIndex(existing => existing.SerialNumber == record.SerialNumber);
            if (index >= 0)
            {
                _localRecords[index] = record;
            }
            else
            {
                _localRecords.Add(record);
            }
        }

        private void RemoveLocalRecord(int serialNumber)
        {
            _localRecords.RemoveAll(record => record.SerialNumber == serialNumber);
        }

        private IEnumerable<string> GetLocalRecordParticipantNames()
        {
            foreach (NewYearCardRecordSnapshot record in _localRecords)
            {
                if (!record.Equals(default(NewYearCardRecordSnapshot)))
                {
                    yield return record.SenderName;
                    yield return record.ReceiverName;
                }
            }
        }

        private static IEnumerable<string> ResolveSearchCandidates(string normalizedQuery, IEnumerable<string> contactNames)
        {
            return ResolveSearchCandidates(normalizedQuery, contactNames, null);
        }

        private static IEnumerable<string> ResolveSearchCandidates(
            string normalizedQuery,
            IEnumerable<string> contactNames,
            IEnumerable<string> packetOwnedNames)
        {
            HashSet<string> emitted = new(StringComparer.OrdinalIgnoreCase);
            bool hasQuery = !string.IsNullOrWhiteSpace(normalizedQuery);

            foreach (string candidate in ResolveCandidateGroup(contactNames, normalizedQuery, hasQuery, emitted))
            {
                yield return candidate;
            }

            foreach (string candidate in ResolveCandidateGroup(packetOwnedNames, normalizedQuery, hasQuery, emitted))
            {
                yield return candidate;
            }
        }

        private static IEnumerable<string> ResolveCandidateGroup(
            IEnumerable<string> candidates,
            string normalizedQuery,
            bool hasQuery,
            HashSet<string> emitted)
        {
            if (candidates == null)
            {
                yield break;
            }

            foreach (string candidateName in candidates)
            {
                string candidate = NormalizeName(candidateName, string.Empty);
                if (candidate.Length == 0)
                {
                    continue;
                }

                if (hasQuery
                    && candidate.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (emitted.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private static byte[] EncodeSendPayload(int inventoryPosition, int itemId, string targetName, string memo)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            writer.Write(SendCardSubtype);
            writer.Write((short)Math.Max(0, inventoryPosition));
            writer.Write(itemId);
            WriteMapleString(writer, NormalizeName(targetName, string.Empty));
            WriteMapleString(writer, NormalizeMemo(memo));
            writer.Flush();
            return stream.ToArray();
        }

        private static byte[] EncodeReadPayload(int serialNumber)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            writer.Write(ReadArrivedCardSubtype);
            writer.Write(serialNumber);
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

    internal sealed record NewYearCardReadRequest(
        int Opcode,
        byte[] Payload,
        int SerialNumber);

    internal enum NewYearCardCompletionKind
    {
        None,
        SendSuccess,
        ReceiveSuccess,
        DeleteSuccess,
        CannotSendToSelf,
        NoFreeSlot,
        NoCardToSend,
        WrongInventory,
        TargetNotFound,
        IncoherentData,
        DatabaseError,
        UnknownError
    }

    internal enum NewYearCardResultSubtype
    {
        None = 0,
        SendSuccess = 4,
        SendFailure = 5,
        ReceiveSuccess = 6,
        ReceiveFailure = 7,
        DeleteSuccess = 8,
        DeleteFailure = 9,
        ArrivalList = 10,
        ReadFailure = 11,
        ArrivalSingle = 12,
        RemoteRecordAdd = 13,
        RemoteRecordRemove = 14,
        Failure = 255
    }

    internal enum NewYearCardFailureReason
    {
        None = 0,
        CannotSendToSelf = 15,
        NoFreeSlot = 16,
        NoCardToSend = 17,
        WrongInventory = 18,
        TargetNotFound = 19,
        IncoherentData = 20,
        DatabaseError = 21,
        UnknownError = 22
    }

    internal sealed record NewYearCardRecordSnapshot(
        int SerialNumber,
        int SenderId,
        string SenderName,
        bool SenderDiscarded,
        long DateSentFileTime,
        int ReceiverId,
        string ReceiverName,
        bool ReceiverDiscarded,
        bool ReceiverReceivedCard,
        long DateReceivedFileTime,
        string Content);

    internal sealed record NewYearCardArrivalPrompt(
        int SerialNumber,
        int SenderId,
        string SenderName);

    internal sealed record NewYearCardResultPacket(
        NewYearCardResultSubtype Subtype,
        NewYearCardCompletionKind CompletionKind,
        NewYearCardFailureReason FailureReason,
        NewYearCardRecordSnapshot Record,
        int SerialNumber,
        int RemoteUserId,
        IReadOnlyList<NewYearCardArrivalPrompt> Arrivals);

    internal sealed record NewYearCardResultSnapshot(
        NewYearCardResultSubtype Subtype,
        NewYearCardCompletionKind CompletionKind,
        NewYearCardFailureReason FailureReason,
        NewYearCardRecordSnapshot Record,
        int SerialNumber,
        int RemoteUserId,
        IReadOnlyList<NewYearCardArrivalPrompt> Arrivals,
        string Message)
    {
        internal static NewYearCardResultSnapshot Empty { get; } = new(
            NewYearCardResultSubtype.None,
            NewYearCardCompletionKind.None,
            NewYearCardFailureReason.None,
            default,
            0,
            0,
            Array.Empty<NewYearCardArrivalPrompt>(),
            string.Empty);

        internal static NewYearCardResultSnapshot FromCompletion(
            NewYearCardResultSubtype subtype,
            NewYearCardCompletionKind completionKind,
            NewYearCardFailureReason failureReason,
            NewYearCardRecordSnapshot record,
            int serialNumber,
            string message)
        {
            return new NewYearCardResultSnapshot(
                subtype,
                completionKind,
                failureReason,
                record,
                serialNumber,
                0,
                Array.Empty<NewYearCardArrivalPrompt>(),
                message);
        }
    }

    internal static class NewYearCardResultPacketCodec
    {
        internal static bool TryDecode(byte[] payload, out NewYearCardResultPacket packet, out string error)
        {
            packet = null;
            error = null;
            if (payload == null || payload.Length == 0)
            {
                error = "CWvsContext::OnNewYearCardRes payload is empty.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload);
                using System.IO.BinaryReader reader = new(stream, Encoding.Default, leaveOpen: true);
                NewYearCardResultSubtype subtype = DecodeSubtype(reader.ReadByte());
                packet = subtype switch
                {
                    NewYearCardResultSubtype.SendSuccess => new NewYearCardResultPacket(
                        subtype,
                        NewYearCardCompletionKind.SendSuccess,
                        NewYearCardFailureReason.None,
                        DecodeRecord(reader),
                        0,
                        0,
                        Array.Empty<NewYearCardArrivalPrompt>()),
                    NewYearCardResultSubtype.ReceiveSuccess => new NewYearCardResultPacket(
                        subtype,
                        NewYearCardCompletionKind.ReceiveSuccess,
                        NewYearCardFailureReason.None,
                        DecodeRecord(reader),
                        0,
                        0,
                        Array.Empty<NewYearCardArrivalPrompt>()),
                    NewYearCardResultSubtype.DeleteSuccess => DecodeDeleteSuccess(reader, subtype),
                    NewYearCardResultSubtype.SendFailure
                        or NewYearCardResultSubtype.ReceiveFailure
                        or NewYearCardResultSubtype.DeleteFailure
                        or NewYearCardResultSubtype.ReadFailure => DecodeFailure(reader, subtype),
                    NewYearCardResultSubtype.ArrivalList => DecodeArrivalList(reader, subtype),
                    NewYearCardResultSubtype.ArrivalSingle => DecodeArrivalSingle(reader, subtype),
                    NewYearCardResultSubtype.RemoteRecordAdd => DecodeRemoteRecordAdd(reader, subtype),
                    NewYearCardResultSubtype.RemoteRecordRemove => DecodeRemoteRecordRemove(reader, subtype),
                    _ => new NewYearCardResultPacket(
                        subtype,
                        NewYearCardCompletionKind.None,
                        NewYearCardFailureReason.None,
                        default,
                        0,
                        0,
                        Array.Empty<NewYearCardArrivalPrompt>())
                };

                if (stream.Position != stream.Length)
                {
                    error = $"CWvsContext::OnNewYearCardRes decoded subtype {(byte)subtype} with {stream.Length - stream.Position} trailing byte(s).";
                    return false;
                }

                return true;
            }
            catch (EndOfStreamException)
            {
                error = "CWvsContext::OnNewYearCardRes payload ended before the client-shaped fields were complete.";
                return false;
            }
        }

        internal static NewYearCardFailureReason ToFailureReason(NewYearCardCompletionKind kind)
        {
            return kind switch
            {
                NewYearCardCompletionKind.CannotSendToSelf => NewYearCardFailureReason.CannotSendToSelf,
                NewYearCardCompletionKind.NoFreeSlot => NewYearCardFailureReason.NoFreeSlot,
                NewYearCardCompletionKind.NoCardToSend => NewYearCardFailureReason.NoCardToSend,
                NewYearCardCompletionKind.WrongInventory => NewYearCardFailureReason.WrongInventory,
                NewYearCardCompletionKind.TargetNotFound => NewYearCardFailureReason.TargetNotFound,
                NewYearCardCompletionKind.IncoherentData => NewYearCardFailureReason.IncoherentData,
                NewYearCardCompletionKind.DatabaseError => NewYearCardFailureReason.DatabaseError,
                NewYearCardCompletionKind.UnknownError => NewYearCardFailureReason.UnknownError,
                _ => NewYearCardFailureReason.None
            };
        }

        private static NewYearCardResultSubtype DecodeSubtype(byte value)
        {
            return value switch
            {
                4 => NewYearCardResultSubtype.SendSuccess,
                5 => NewYearCardResultSubtype.SendFailure,
                6 => NewYearCardResultSubtype.ReceiveSuccess,
                7 => NewYearCardResultSubtype.ReceiveFailure,
                8 => NewYearCardResultSubtype.DeleteSuccess,
                9 => NewYearCardResultSubtype.DeleteFailure,
                10 => NewYearCardResultSubtype.ArrivalList,
                11 => NewYearCardResultSubtype.ReadFailure,
                12 => NewYearCardResultSubtype.ArrivalSingle,
                13 => NewYearCardResultSubtype.RemoteRecordAdd,
                14 => NewYearCardResultSubtype.RemoteRecordRemove,
                _ => (NewYearCardResultSubtype)value
            };
        }

        private static NewYearCardResultPacket DecodeDeleteSuccess(System.IO.BinaryReader reader, NewYearCardResultSubtype subtype)
        {
            int serialNumber = reader.ReadInt32();
            return new NewYearCardResultPacket(
                subtype,
                NewYearCardCompletionKind.DeleteSuccess,
                NewYearCardFailureReason.None,
                default,
                serialNumber,
                0,
                Array.Empty<NewYearCardArrivalPrompt>());
        }

        private static NewYearCardResultPacket DecodeFailure(System.IO.BinaryReader reader, NewYearCardResultSubtype subtype)
        {
            NewYearCardFailureReason reason = DecodeFailureReason(reader.ReadByte());
            return new NewYearCardResultPacket(
                subtype,
                ToCompletionKind(reason),
                reason,
                default,
                0,
                0,
                Array.Empty<NewYearCardArrivalPrompt>());
        }

        private static NewYearCardResultPacket DecodeArrivalList(System.IO.BinaryReader reader, NewYearCardResultSubtype subtype)
        {
            int count = reader.ReadInt32();
            if (count < 1 || count > 99)
            {
                return new NewYearCardResultPacket(
                    subtype,
                    NewYearCardCompletionKind.None,
                    NewYearCardFailureReason.None,
                    default,
                    count,
                    0,
                    Array.Empty<NewYearCardArrivalPrompt>());
            }

            List<NewYearCardArrivalPrompt> arrivals = new(count);
            for (int i = 0; i < count; i++)
            {
                arrivals.Add(new NewYearCardArrivalPrompt(
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    ReadMapleString(reader)));
            }

            return new NewYearCardResultPacket(
                subtype,
                NewYearCardCompletionKind.None,
                NewYearCardFailureReason.None,
                default,
                0,
                0,
                arrivals);
        }

        private static NewYearCardResultPacket DecodeArrivalSingle(System.IO.BinaryReader reader, NewYearCardResultSubtype subtype)
        {
            int serialNumber = reader.ReadInt32();
            string senderName = ReadMapleString(reader);
            return new NewYearCardResultPacket(
                subtype,
                NewYearCardCompletionKind.None,
                NewYearCardFailureReason.None,
                default,
                serialNumber,
                0,
                new[] { new NewYearCardArrivalPrompt(serialNumber, 0, senderName) });
        }

        private static NewYearCardResultPacket DecodeRemoteRecordAdd(System.IO.BinaryReader reader, NewYearCardResultSubtype subtype)
        {
            int serialNumber = reader.ReadInt32();
            int remoteUserId = reader.ReadInt32();
            return new NewYearCardResultPacket(
                subtype,
                NewYearCardCompletionKind.None,
                NewYearCardFailureReason.None,
                default,
                serialNumber,
                remoteUserId,
                Array.Empty<NewYearCardArrivalPrompt>());
        }

        private static NewYearCardResultPacket DecodeRemoteRecordRemove(System.IO.BinaryReader reader, NewYearCardResultSubtype subtype)
        {
            int serialNumber = reader.ReadInt32();
            return new NewYearCardResultPacket(
                subtype,
                NewYearCardCompletionKind.None,
                NewYearCardFailureReason.None,
                default,
                serialNumber,
                0,
                Array.Empty<NewYearCardArrivalPrompt>());
        }

        private static NewYearCardRecordSnapshot DecodeRecord(System.IO.BinaryReader reader)
        {
            int serialNumber = reader.ReadInt32();
            int senderId = reader.ReadInt32();
            string senderName = LimitClientFixedString(ReadMapleString(reader), NewYearCardRuntime.SenderTargetMaxChars);
            bool senderDiscarded = reader.ReadByte() != 0;
            long sentFileTime = reader.ReadInt64();
            int receiverId = reader.ReadInt32();
            string receiverName = LimitClientFixedString(ReadMapleString(reader), NewYearCardRuntime.SenderTargetMaxChars);
            bool receiverDiscarded = reader.ReadByte() != 0;
            bool receiverReceived = reader.ReadByte() != 0;
            long receivedFileTime = reader.ReadInt64();
            string content = LimitClientFixedString(ReadMapleString(reader), NewYearCardRuntime.SenderMemoMaxChars);

            return new NewYearCardRecordSnapshot(
                serialNumber,
                senderId,
                senderName,
                senderDiscarded,
                sentFileTime,
                receiverId,
                receiverName,
                receiverDiscarded,
                receiverReceived,
                receivedFileTime,
                content);
        }

        private static NewYearCardFailureReason DecodeFailureReason(byte reason)
        {
            return reason switch
            {
                15 => NewYearCardFailureReason.CannotSendToSelf,
                16 => NewYearCardFailureReason.NoFreeSlot,
                17 => NewYearCardFailureReason.NoCardToSend,
                18 => NewYearCardFailureReason.WrongInventory,
                19 => NewYearCardFailureReason.TargetNotFound,
                20 => NewYearCardFailureReason.IncoherentData,
                21 => NewYearCardFailureReason.DatabaseError,
                22 => NewYearCardFailureReason.UnknownError,
                _ => NewYearCardFailureReason.UnknownError
            };
        }

        private static NewYearCardCompletionKind ToCompletionKind(NewYearCardFailureReason reason)
        {
            return reason switch
            {
                NewYearCardFailureReason.CannotSendToSelf => NewYearCardCompletionKind.CannotSendToSelf,
                NewYearCardFailureReason.NoFreeSlot => NewYearCardCompletionKind.NoFreeSlot,
                NewYearCardFailureReason.NoCardToSend => NewYearCardCompletionKind.NoCardToSend,
                NewYearCardFailureReason.WrongInventory => NewYearCardCompletionKind.WrongInventory,
                NewYearCardFailureReason.TargetNotFound => NewYearCardCompletionKind.TargetNotFound,
                NewYearCardFailureReason.IncoherentData => NewYearCardCompletionKind.IncoherentData,
                NewYearCardFailureReason.DatabaseError => NewYearCardCompletionKind.DatabaseError,
                _ => NewYearCardCompletionKind.UnknownError
            };
        }

        private static string ReadMapleString(System.IO.BinaryReader reader)
        {
            ushort length = reader.ReadUInt16();
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException();
            }

            return Encoding.Default.GetString(bytes);
        }

        private static string LimitClientFixedString(string value, int maxChars)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Length <= maxChars
                ? value
                : value.Substring(0, maxChars);
        }
    }
}
