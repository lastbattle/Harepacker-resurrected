using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class PacketOwnedShopDialogRuntime
    {
        private readonly List<string> _recentNotes = new();
        private string _activePane = "Buy";
        private string _lastTemplateNote = "No shop result packet has been applied yet.";
        private int _openCount;
        private int _resultCount;
        private int _lastPacketType = -1;
        private int _lastSubtype = -1;
        private int _lastTemplateStringPoolId = -1;
        private int _lastTemplateArgument;

        internal bool IsOpen { get; private set; }
        internal string StatusMessage { get; private set; } = "CShopDlg::OnPacket idle.";

        internal void Close()
        {
            IsOpen = false;
            StatusMessage = "CShopDlg owner closed locally.";
        }

        internal bool TryApplyPacket(int packetType, byte[] payload, out string message)
        {
            payload ??= Array.Empty<byte>();
            _lastPacketType = packetType;

            switch (packetType)
            {
                case 364:
                    _openCount++;
                    IsOpen = true;
                    StatusMessage = $"CShopDlg::SetShopDlg opened the packet-owned NPC shop owner from packet 364 with {payload.Length.ToString(CultureInfo.InvariantCulture)} byte(s).";
                    AppendNote(StatusMessage);
                    message = StatusMessage;
                    return true;

                case 365:
                    return TryApplyResultPacket(payload, out message);

                default:
                    message = $"Unsupported NPC shop packet type {packetType}.";
                    return false;
            }
        }

        internal IReadOnlyList<string> BuildLines()
        {
            List<string> lines = new()
            {
                "Packet-owned owner: CShopDlg::OnPacket (364/365).",
                $"Dialog: {(IsOpen ? "open" : "closed")} | Active pane: {_activePane}",
                $"Packets: open={_openCount.ToString(CultureInfo.InvariantCulture)}, result={_resultCount.ToString(CultureInfo.InvariantCulture)}",
                $"Last packet: {DescribePacket()}",
                _lastTemplateNote
            };

            lines.AddRange(_recentNotes);
            return lines;
        }

        internal string BuildFooter()
        {
            return StatusMessage;
        }

        private bool TryApplyResultPacket(byte[] payload, out string message)
        {
            if (payload.Length == 0)
            {
                message = "NPC shop result packet 365 requires at least a subtype byte.";
                return false;
            }

            _resultCount++;
            IsOpen = true;
            _lastSubtype = payload[0];
            _lastTemplateStringPoolId = -1;
            _lastTemplateArgument = 0;

            switch (_lastSubtype)
            {
                case 0:
                    _activePane = "Sell";
                    StatusMessage = "CShopDlg result 0 refreshed the sell snapshot and moved the selection into the matching sell tab.";
                    _lastTemplateNote = "Result subtype 0 follows the client path that repositions the sell scrollbar around the snapshot TI.";
                    break;
                case 1:
                case 5:
                case 9:
                    ApplyTemplateNotice(0x365, "CShopDlg result reported the StringPool 0x365 notice path.");
                    break;
                case 2:
                case 10:
                    ApplyTemplateNotice(0x1A8B, "CShopDlg result reported the StringPool 0x1A8B notice path.");
                    break;
                case 3:
                    ApplyTemplateNotice(0x366, "CShopDlg result reported the StringPool 0x366 notice path.");
                    break;
                case 4:
                case 8:
                    StatusMessage = $"CShopDlg result {_lastSubtype.ToString(CultureInfo.InvariantCulture)} completed without a visible dialog change.";
                    _lastTemplateNote = "The client returns immediately for this subtype.";
                    break;
                case 13:
                    ApplyTemplateNotice(0x153F, "CShopDlg result reported the StringPool 0x153F notice path.");
                    break;
                case 14:
                    if (!TryReadInt32(payload, 1, out _lastTemplateArgument))
                    {
                        message = "NPC shop result subtype 14 requires a 4-byte integer argument.";
                        return false;
                    }

                    _lastTemplateStringPoolId = 0x154F;
                    StatusMessage = $"CShopDlg result 14 formatted StringPool 0x154F with argument {_lastTemplateArgument.ToString(CultureInfo.InvariantCulture)}.";
                    _lastTemplateNote = "Subtype 14 is the client branch that formats StringPool 0x154F with a decoded 4-byte value.";
                    break;
                case 15:
                    if (!TryReadInt32(payload, 1, out _lastTemplateArgument))
                    {
                        message = "NPC shop result subtype 15 requires a 4-byte integer argument.";
                        return false;
                    }

                    _lastTemplateStringPoolId = 0x154E;
                    StatusMessage = $"CShopDlg result 15 formatted StringPool 0x154E with argument {_lastTemplateArgument.ToString(CultureInfo.InvariantCulture)}.";
                    _lastTemplateNote = "Subtype 15 is the client branch that formats StringPool 0x154E with a decoded 4-byte value.";
                    break;
                case 16:
                    ApplyTemplateNotice(0x368, "CShopDlg result reported the StringPool 0x368 notice path.");
                    break;
                case 17:
                    ApplyTemplateNotice(0x16ED, "CShopDlg result reported the StringPool 0x16ED notice path.");
                    break;
                case 18:
                    ApplyTemplateNotice(0xFB2, "CShopDlg result reported the StringPool 0xFB2 notice path.");
                    break;
                case 19:
                    if (payload.Length > 1 && payload[1] != 0 && TryReadMapleString(payload, 2, out string shopNotice))
                    {
                        StatusMessage = $"CShopDlg result 19 delivered a custom notice: {shopNotice}";
                        _lastTemplateNote = "Subtype 19 used the branch that decodes a packet-authored ZXString<char> notice.";
                    }
                    else
                    {
                        ApplyTemplateNotice(0x369, "CShopDlg result 19 fell back to the default StringPool 0x369 notice path.");
                    }
                    break;
                default:
                    ApplyTemplateNotice(0x369, $"CShopDlg result {_lastSubtype.ToString(CultureInfo.InvariantCulture)} fell back to the default StringPool 0x369 notice path.");
                    break;
            }

            AppendNote(StatusMessage);
            message = StatusMessage;
            return true;
        }

        private void ApplyTemplateNotice(int stringPoolId, string statusMessage)
        {
            _lastTemplateStringPoolId = stringPoolId;
            StatusMessage = statusMessage;
            _lastTemplateNote = $"The simulator tracked the client notice branch for StringPool 0x{stringPoolId.ToString("X", CultureInfo.InvariantCulture)}.";
        }

        private string DescribePacket()
        {
            if (_lastPacketType < 0)
            {
                return "none";
            }

            return _lastPacketType == 365
                ? $"365 / subtype {_lastSubtype.ToString(CultureInfo.InvariantCulture)}"
                : _lastPacketType.ToString(CultureInfo.InvariantCulture);
        }

        private void AppendNote(string note)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                return;
            }

            _recentNotes.Insert(0, note);
            if (_recentNotes.Count > 3)
            {
                _recentNotes.RemoveAt(_recentNotes.Count - 1);
            }
        }

        private static bool TryReadInt32(byte[] payload, int offset, out int value)
        {
            value = 0;
            if (payload == null || offset < 0 || payload.Length < offset + sizeof(int))
            {
                return false;
            }

            value = BitConverter.ToInt32(payload, offset);
            return true;
        }

        private static bool TryReadMapleString(byte[] payload, int offset, out string value)
        {
            value = string.Empty;
            if (payload == null || offset < 0 || payload.Length < offset + sizeof(short))
            {
                return false;
            }

            short length = BitConverter.ToInt16(payload, offset);
            if (length < 0 || payload.Length < offset + sizeof(short) + length)
            {
                return false;
            }

            value = Encoding.ASCII.GetString(payload, offset + sizeof(short), length).Trim();
            return true;
        }
    }

    internal sealed class PacketOwnedStoreBankDialogRuntime
    {
        private readonly List<string> _recentNotes = new();
        private int _openCount;
        private int _packet369Count;
        private int _packet370Count;
        private int _lastPacketType = -1;
        private int _lastSubtype = -1;
        private int _pendingGetAllCharacterId;
        private int _pendingGetAllStoreId;

        internal bool IsOpen { get; private set; }
        internal bool HasPendingGetAllRequest { get; private set; }
        internal string StatusMessage { get; private set; } = "CStoreBankDlg::OnPacket idle.";

        internal void Close()
        {
            IsOpen = false;
            StatusMessage = "CStoreBankDlg owner closed locally.";
        }

        internal string ConsumePendingGetAllRequest()
        {
            if (!HasPendingGetAllRequest)
            {
                return "No packet-authored store-bank get-all request is waiting.";
            }

            HasPendingGetAllRequest = false;
            StatusMessage = $"Accepted packet-authored store-bank get-all request ({_pendingGetAllCharacterId.ToString(CultureInfo.InvariantCulture)}, {_pendingGetAllStoreId.ToString(CultureInfo.InvariantCulture)}).";
            AppendNote(StatusMessage);
            return StatusMessage;
        }

        internal bool TryApplyPacket(int packetType, byte[] payload, out string message)
        {
            payload ??= Array.Empty<byte>();
            _lastPacketType = packetType;

            switch (packetType)
            {
                case 369:
                    _packet369Count++;
                    return TryApply369Packet(payload, out message);
                case 370:
                    _packet370Count++;
                    return TryApply370Packet(payload, out message);
                default:
                    message = $"Unsupported store-bank packet type {packetType}.";
                    return false;
            }
        }

        internal IReadOnlyList<string> BuildLines()
        {
            List<string> lines = new()
            {
                "Packet-owned owner: CStoreBankDlg::OnPacket (369/370).",
                $"Dialog: {(IsOpen ? "open" : "closed")} | Pending get-all: {(HasPendingGetAllRequest ? "yes" : "no")}",
                $"Packets: 369={_packet369Count.ToString(CultureInfo.InvariantCulture)}, 370={_packet370Count.ToString(CultureInfo.InvariantCulture)}",
                $"Last packet: {DescribePacket()}",
                HasPendingGetAllRequest
                    ? $"Pending request ids: character={_pendingGetAllCharacterId.ToString(CultureInfo.InvariantCulture)}, store={_pendingGetAllStoreId.ToString(CultureInfo.InvariantCulture)}"
                    : "No pending get-all request is staged."
            };

            lines.AddRange(_recentNotes);
            return lines;
        }

        internal string BuildFooter()
        {
            return StatusMessage;
        }

        private bool TryApply369Packet(byte[] payload, out string message)
        {
            if (payload.Length == 0)
            {
                message = "Store-bank packet 369 requires a subtype byte.";
                return false;
            }

            IsOpen = true;
            _lastSubtype = payload[0];
            StatusMessage = _lastSubtype switch
            {
                30 => "CStoreBankDlg result 30 cleared the list and disabled the Get button (StringPool 0xDC6 branch).",
                31 => "CStoreBankDlg result 31 reported the StringPool 0xDC7 notice branch.",
                32 => "CStoreBankDlg result 32 reported the StringPool 0xDC8 notice branch.",
                33 => "CStoreBankDlg result 33 reported the StringPool 0xDC9 notice branch.",
                34 => "CStoreBankDlg result 34 reported the StringPool 0xDCA notice branch.",
                _ => $"CStoreBankDlg packet 369 subtype {_lastSubtype.ToString(CultureInfo.InvariantCulture)} is not modeled beyond owner tracking."
            };

            if (_lastSubtype == 30)
            {
                HasPendingGetAllRequest = false;
            }

            AppendNote(StatusMessage);
            message = StatusMessage;
            return true;
        }

        private bool TryApply370Packet(byte[] payload, out string message)
        {
            if (payload.Length == 0)
            {
                message = "Store-bank packet 370 requires a subtype byte.";
                return false;
            }

            _lastSubtype = payload[0];
            switch (_lastSubtype)
            {
                case 35:
                    _openCount++;
                    IsOpen = true;
                    StatusMessage = $"CStoreBankDlg::SetStoreBankDlg opened the packet-owned store-bank owner from packet 370 subtype 35 with {payload.Length.ToString(CultureInfo.InvariantCulture)} byte(s).";
                    break;

                case 36:
                    if (payload.Length < 1 + (sizeof(int) * 2))
                    {
                        message = "Store-bank packet 370 subtype 36 requires two 4-byte request ids.";
                        return false;
                    }

                    _pendingGetAllCharacterId = BitConverter.ToInt32(payload, 1);
                    _pendingGetAllStoreId = BitConverter.ToInt32(payload, 5);
                    HasPendingGetAllRequest = true;
                    IsOpen = true;
                    StatusMessage = $"CStoreBankDlg staged a SendGetAllRequest pair ({_pendingGetAllCharacterId.ToString(CultureInfo.InvariantCulture)}, {_pendingGetAllStoreId.ToString(CultureInfo.InvariantCulture)}).";
                    break;

                case 37:
                    if (payload.Length < 1 + (sizeof(int) * 2) + sizeof(byte))
                    {
                        message = "Store-bank packet 370 subtype 37 requires two 4-byte values and a channel byte.";
                        return false;
                    }

                    int senderId = BitConverter.ToInt32(payload, 1);
                    int storageToken = BitConverter.ToInt32(payload, 5);
                    byte channelId = payload[9];
                    IsOpen = true;
                    StatusMessage = channelId >= 0xFE || storageToken == 999999999
                        ? $"CStoreBankDlg showed the fallback shipment prompt branch for sender {senderId.ToString(CultureInfo.InvariantCulture)}."
                        : $"CStoreBankDlg showed the channel-routed shipment prompt for sender {senderId.ToString(CultureInfo.InvariantCulture)}, token {storageToken.ToString(CultureInfo.InvariantCulture)}, channel {channelId.ToString(CultureInfo.InvariantCulture)}.";
                    break;

                case 38:
                    IsOpen = true;
                    StatusMessage = "CStoreBankDlg showed the StringPool 0xDC3 notice branch.";
                    break;

                default:
                    IsOpen = true;
                    StatusMessage = $"CStoreBankDlg packet 370 subtype {_lastSubtype.ToString(CultureInfo.InvariantCulture)} is not modeled beyond owner tracking.";
                    break;
            }

            AppendNote(StatusMessage);
            message = StatusMessage;
            return true;
        }

        private string DescribePacket()
        {
            if (_lastPacketType < 0)
            {
                return "none";
            }

            return $"{_lastPacketType.ToString(CultureInfo.InvariantCulture)} / subtype {_lastSubtype.ToString(CultureInfo.InvariantCulture)}";
        }

        private void AppendNote(string note)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                return;
            }

            _recentNotes.Insert(0, note);
            if (_recentNotes.Count > 3)
            {
                _recentNotes.RemoveAt(_recentNotes.Count - 1);
            }
        }
    }

    internal sealed class PacketOwnedBattleRecordRuntime
    {
        private readonly List<string> _recentNotes = new();
        private int _packetCount420;
        private int _packetCount421;
        private int _packetCount422;
        private int _packetCount423;
        private int _lastPacketType = -1;
        private int _pageIndex;

        internal bool IsOpen { get; private set; }
        internal bool OnCalc { get; private set; }
        internal bool ServerOnCalc { get; private set; }
        internal bool DotTrackingEnabled { get; private set; }
        internal int TotalDamage { get; private set; }
        internal int TotalHits { get; private set; }
        internal int MaxDamage { get; private set; }
        internal int MinDamage { get; private set; }
        internal int LastDotDamage { get; private set; }
        internal int LastDotHitCount { get; private set; }
        internal int? LastAttrRate { get; private set; }
        internal string StatusMessage { get; private set; } = "CBattleRecordMan::OnPacket idle.";

        internal void Close()
        {
            IsOpen = false;
            OnCalc = false;
            ServerOnCalc = false;
            StatusMessage = "CBattleRecordMan owner closed locally.";
        }

        internal void SelectPage(int pageIndex)
        {
            _pageIndex = Math.Clamp(pageIndex, 0, 2);
            StatusMessage = _pageIndex switch
            {
                1 => "Battle Record page switched to DOT totals.",
                2 => "Battle Record page switched to packet log.",
                _ => "Battle Record page switched to summary."
            };
        }

        internal bool TryApplyPacket(int packetType, byte[] payload, out string message)
        {
            payload ??= Array.Empty<byte>();
            _lastPacketType = packetType;

            switch (packetType)
            {
                case 420:
                    _packetCount420++;
                    IsOpen = true;
                    OnCalc = true;
                    DotTrackingEnabled = true;
                    StatusMessage = "CBattleRecordMan received packet 420 and armed simulator-side battle-record tracking. The exact client-side setup branch still needs a deeper decompile.";
                    break;

                case 421:
                    _packetCount421++;
                    if (!TryApplyDotDamageInfo(payload, out message))
                    {
                        return false;
                    }

                    AppendNote(StatusMessage);
                    return true;

                case 422:
                    _packetCount422++;
                    if (payload.Length < 1)
                    {
                        message = "Battle-record packet 422 requires a 1-byte server-on-calc flag.";
                        return false;
                    }

                    ServerOnCalc = payload[0] != 0;
                    IsOpen = ServerOnCalc || IsOpen;
                    OnCalc = ServerOnCalc && (OnCalc || _packetCount420 > 0 || _packetCount421 > 0);
                    StatusMessage = ServerOnCalc
                        ? "CBattleRecordMan accepted the server-on-calc request result and kept battle-record collection active."
                        : "CBattleRecordMan rejected the server-on-calc request result and closed UI type 35 in the client path.";
                    if (!ServerOnCalc)
                    {
                        IsOpen = false;
                        OnCalc = false;
                    }
                    break;

                case 423:
                    _packetCount423++;
                    IsOpen = false;
                    OnCalc = false;
                    ServerOnCalc = false;
                    StatusMessage = "CBattleRecordMan received packet 423. The simulator treats it as the remaining close/reset family hook until the follow-up client branch is fully decompiled.";
                    break;

                default:
                    message = $"Unsupported battle-record packet type {packetType}.";
                    return false;
            }

            AppendNote(StatusMessage);
            message = StatusMessage;
            return true;
        }

        internal IReadOnlyList<string> BuildLines()
        {
            List<string> lines = new()
            {
                "Packet-owned owner: CBattleRecordMan::OnPacket (420-423).",
                $"Page: {ResolvePageName()} | Window: {(IsOpen ? "open" : "closed")}",
                $"Calc flags: onCalc={OnCalc}, serverOnCalc={ServerOnCalc}, dot={DotTrackingEnabled}",
                $"Packets: 420={_packetCount420.ToString(CultureInfo.InvariantCulture)}, 421={_packetCount421.ToString(CultureInfo.InvariantCulture)}, 422={_packetCount422.ToString(CultureInfo.InvariantCulture)}, 423={_packetCount423.ToString(CultureInfo.InvariantCulture)}"
            };

            switch (_pageIndex)
            {
                case 1:
                    lines.Add($"DOT totals: hits={TotalHits.ToString(CultureInfo.InvariantCulture)}, totalDamage={TotalDamage.ToString(CultureInfo.InvariantCulture)}, lastHit={LastDotDamage.ToString(CultureInfo.InvariantCulture)} x{LastDotHitCount.ToString(CultureInfo.InvariantCulture)}");
                    lines.Add($"Damage bounds: min={FormatDamage(MinDamage)}, max={FormatDamage(MaxDamage)}, avg={FormatAverageDamage()}");
                    lines.Add(LastAttrRate.HasValue
                        ? $"Last attr rate: {LastAttrRate.Value.ToString(CultureInfo.InvariantCulture)}"
                        : "Last attr rate: none");
                    break;

                case 2:
                    lines.Add($"Last packet: {(_lastPacketType < 0 ? "none" : _lastPacketType.ToString(CultureInfo.InvariantCulture))}");
                    lines.Add(StatusMessage);
                    lines.AddRange(_recentNotes);
                    break;

                default:
                    lines.Add($"Total damage: {TotalDamage.ToString(CultureInfo.InvariantCulture)} across {TotalHits.ToString(CultureInfo.InvariantCulture)} DOT hit(s)");
                    lines.Add($"Damage bounds: min={FormatDamage(MinDamage)}, max={FormatDamage(MaxDamage)}, avg={FormatAverageDamage()}");
                    lines.Add(LastAttrRate.HasValue
                        ? $"Last attr rate: {LastAttrRate.Value.ToString(CultureInfo.InvariantCulture)}"
                        : "Last attr rate: none");
                    break;
            }

            return lines;
        }

        internal string BuildFooter()
        {
            return StatusMessage;
        }

        private bool TryApplyDotDamageInfo(byte[] payload, out string message)
        {
            if (payload.Length < (sizeof(int) * 2) + sizeof(byte))
            {
                message = "Battle-record packet 421 requires damage, hit count, and an attr-rate flag.";
                return false;
            }

            int dotDamage = BitConverter.ToInt32(payload, 0);
            int hitCount = BitConverter.ToInt32(payload, 4);
            byte hasAttrRate = payload[8];
            int? attrRate = null;
            if (hasAttrRate != 0)
            {
                if (payload.Length < 13)
                {
                    message = "Battle-record packet 421 reported an attr-rate flag without the trailing 4-byte attr-rate value.";
                    return false;
                }

                attrRate = BitConverter.ToInt32(payload, 9);
            }

            IsOpen = true;
            DotTrackingEnabled = true;
            if (!OnCalc)
            {
                OnCalc = true;
            }

            if (!ServerOnCalc)
            {
                StatusMessage = "CBattleRecordMan received DOT damage info before a successful server-on-calc result. The simulator recorded the payload but the client would ignore it until both calc flags are enabled.";
            }
            else
            {
                if (hitCount > 0)
                {
                    LastDotDamage = dotDamage;
                    LastDotHitCount = hitCount;
                    TotalHits += hitCount;
                    TotalDamage += dotDamage * hitCount;
                    MaxDamage = Math.Max(MaxDamage, dotDamage);
                    MinDamage = MinDamage == 0 ? dotDamage : Math.Min(MinDamage, dotDamage);
                }

                LastAttrRate = attrRate;
                StatusMessage = $"CBattleRecordMan applied DOT damage info: {dotDamage.ToString(CultureInfo.InvariantCulture)} x {hitCount.ToString(CultureInfo.InvariantCulture)}.";
            }

            message = StatusMessage;
            return true;
        }

        private string ResolvePageName()
        {
            return _pageIndex switch
            {
                1 => "DOT",
                2 => "Packets",
                _ => "Summary"
            };
        }

        private string FormatAverageDamage()
        {
            if (TotalHits <= 0)
            {
                return "n/a";
            }

            double averageDamage = TotalDamage / (double)TotalHits;
            return averageDamage.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string FormatDamage(int value)
        {
            return value > 0 ? value.ToString(CultureInfo.InvariantCulture) : "n/a";
        }

        private void AppendNote(string note)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                return;
            }

            _recentNotes.Insert(0, note);
            if (_recentNotes.Count > 3)
            {
                _recentNotes.RemoveAt(_recentNotes.Count - 1);
            }
        }
    }
}
