using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using BinaryReader = MapleLib.PacketLib.PacketReader;
using BinaryWriter = MapleLib.PacketLib.PacketWriter;
namespace HaCreator.MapSimulator.Interaction
{
    internal enum PacketFieldUtilityPacketKind
    {
        BlowWeather = 158,
        AdminResult = 160,
        Quiz = 161,
        StalkResult = 172,
        QuickslotInit = 175,
        FootHoldInfo = 176,
        RequestFootHoldInfo = 177
    }

    internal sealed record PacketFieldUtilityStalkTarget(
        int CharacterId,
        string Name,
        int X,
        int Y);

    internal sealed record PacketFieldUtilityMovingFootholdState(
        int Speed,
        int X1,
        int X2,
        int Y1,
        int Y2,
        int CurrentX,
        int CurrentY,
        bool ReverseVertical,
        bool ReverseHorizontal);

    internal sealed record PacketFieldUtilityFootholdEntry(
        string Name,
        int State,
        IReadOnlyList<int> FootholdSerialNumbers,
        PacketFieldUtilityMovingFootholdState MovingState);

    internal sealed record PacketFieldUtilityAdminResult(
        byte Subtype,
        string Title,
        string Summary,
        string Body,
        int StringPoolId,
        int ChatLogType,
        bool AppendChatLogEntry,
        bool ReloadMinimap,
        bool ToggleMinimap);

    internal sealed class PacketFieldUtilityCallbacks
    {
        internal Func<int, string> ResolveWeatherItemPath { get; init; }
        internal Action<int, byte, string, string> ApplyWeather { get; init; }
        internal Action<PacketFieldUtilityAdminResult> PresentAdminResult { get; init; }
        internal Action<bool, byte, ushort> PresentQuizState { get; init; }
        internal Action<int, string, int, int> UpsertStalkTarget { get; init; }
        internal Action<int> RemoveStalkTarget { get; init; }
        internal Action<int[], bool> ApplyQuickslotKeyMap { get; init; }
        internal Action<IReadOnlyList<PacketFieldUtilityFootholdEntry>> ApplyFootholdInfo { get; init; }
        internal Func<string> RequestFootholdInfo { get; init; }
    }

    internal sealed class PacketFieldUtilityRuntime
    {
        private string _statusMessage = "Packet-owned field utility idle.";
        private string _lastWeatherSummary = string.Empty;
        private string _lastAdminSummary = string.Empty;
        private string _lastQuizSummary = string.Empty;
        private string _lastQuickslotSummary = string.Empty;
        private string _lastFootholdSummary = string.Empty;
        private readonly Dictionary<int, PacketFieldUtilityStalkTarget> _stalkTargets = new();

        internal void Clear()
        {
            _statusMessage = "Packet-owned field utility cleared.";
            _lastWeatherSummary = string.Empty;
            _lastAdminSummary = string.Empty;
            _lastQuizSummary = string.Empty;
            _lastQuickslotSummary = string.Empty;
            _lastFootholdSummary = string.Empty;
            _stalkTargets.Clear();
        }

        internal string DescribeStatus()
        {
            string stalkStatus = _stalkTargets.Count == 0
                ? "stalk=none"
                : $"stalk={string.Join(", ", _stalkTargets.Values.OrderBy(static entry => entry.Name, StringComparer.OrdinalIgnoreCase).Select(entry => $"{entry.Name}@({entry.X},{entry.Y})"))}";

            return string.Join(
                "; ",
                _statusMessage,
                string.IsNullOrWhiteSpace(_lastWeatherSummary) ? "weather=none" : $"weather=\"{TrimForStatus(_lastWeatherSummary)}\"",
                string.IsNullOrWhiteSpace(_lastAdminSummary) ? "admin=none" : $"admin=\"{TrimForStatus(_lastAdminSummary)}\"",
                string.IsNullOrWhiteSpace(_lastQuizSummary) ? "quiz=none" : $"quiz=\"{TrimForStatus(_lastQuizSummary)}\"",
                string.IsNullOrWhiteSpace(_lastQuickslotSummary) ? "quickslot=default-ui" : $"quickslot=\"{TrimForStatus(_lastQuickslotSummary)}\"",
                string.IsNullOrWhiteSpace(_lastFootholdSummary) ? "foothold=none" : $"foothold=\"{TrimForStatus(_lastFootholdSummary)}\"",
                stalkStatus);
        }

        internal bool TryApplyPacket(
            PacketFieldUtilityPacketKind kind,
            byte[] payload,
            PacketFieldUtilityCallbacks callbacks,
            out string message)
        {
            payload ??= Array.Empty<byte>();
            try
            {
                return kind switch
                {
                    PacketFieldUtilityPacketKind.BlowWeather => TryApplyBlowWeather(payload, callbacks, out message),
                    PacketFieldUtilityPacketKind.AdminResult => TryApplyAdminResult(payload, callbacks, out message),
                    PacketFieldUtilityPacketKind.Quiz => TryApplyQuiz(payload, callbacks, out message),
                    PacketFieldUtilityPacketKind.StalkResult => TryApplyStalkResult(payload, callbacks, out message),
                    PacketFieldUtilityPacketKind.QuickslotInit => TryApplyQuickslotInit(payload, callbacks, out message),
                    PacketFieldUtilityPacketKind.FootHoldInfo => TryApplyFootholdInfo(payload, callbacks, out message),
                    PacketFieldUtilityPacketKind.RequestFootHoldInfo => TryApplyFootholdInfoRequest(callbacks, out message),
                    _ => Unsupported(kind, out message)
                };
            }
            catch (EndOfStreamException ex)
            {
                message = $"Packet-owned field utility payload ended early: {ex.Message}";
                return false;
            }
            catch (IOException ex)
            {
                message = $"Packet-owned field utility payload could not be read: {ex.Message}";
                return false;
            }
        }

        internal static byte[] BuildBlowWeatherPayload(byte blowType, int itemId, string message)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(blowType);
            writer.Write(itemId);
            if (itemId > 0 && blowType == 0)
            {
                WriteMapleString(writer, message);
            }

            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildQuizPayload(bool isQuestion, byte category, ushort problemId)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(isQuestion ? (byte)1 : (byte)0);
            writer.Write(category);
            writer.Write(problemId);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildStalkResultPayload(params (int CharacterId, bool Remove, string Name, int X, int Y)[] entries)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(entries?.Length ?? 0);
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    writer.Write(entries[i].CharacterId);
                    writer.Write(entries[i].Remove ? (byte)1 : (byte)0);
                    if (!entries[i].Remove)
                    {
                        WriteMapleString(writer, entries[i].Name);
                        writer.Write(entries[i].X);
                        writer.Write(entries[i].Y);
                    }
                }
            }

            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildQuickslotInitPayload(IReadOnlyList<int> keyCodes)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            bool hasCustom = keyCodes != null && keyCodes.Count == 8;
            writer.Write(hasCustom ? (byte)1 : (byte)0);
            if (hasCustom)
            {
                for (int i = 0; i < 8; i++)
                {
                    writer.Write(keyCodes[i]);
                }
            }

            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildFootHoldInfoPayload(IReadOnlyList<PacketFieldUtilityFootholdEntry> entries)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(entries?.Count ?? 0);
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    PacketFieldUtilityFootholdEntry entry = entries[i];
                    WriteMapleString(writer, entry?.Name);
                    writer.Write(entry?.State ?? 0);

                    IReadOnlyList<int> footholds = entry?.FootholdSerialNumbers ?? Array.Empty<int>();
                    writer.Write(footholds.Count);
                    for (int footholdIndex = 0; footholdIndex < footholds.Count; footholdIndex++)
                    {
                        writer.Write(footholds[footholdIndex]);
                    }

                    if ((entry?.State ?? 0) == 2 && entry?.MovingState != null)
                    {
                        writer.Write(entry.MovingState.Speed);
                        writer.Write(entry.MovingState.X1);
                        writer.Write(entry.MovingState.X2);
                        writer.Write(entry.MovingState.Y1);
                        writer.Write(entry.MovingState.Y2);
                        writer.Write(entry.MovingState.CurrentX);
                        writer.Write(entry.MovingState.CurrentY);
                        writer.Write(entry.MovingState.ReverseVertical ? (byte)1 : (byte)0);
                        writer.Write(entry.MovingState.ReverseHorizontal ? (byte)1 : (byte)0);
                    }
                }
            }

            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildOfficialSessionFootHoldInfoResponsePayload(IReadOnlyList<PacketFieldUtilityFootholdEntry> entries)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    PacketFieldUtilityFootholdEntry entry = entries[i];
                    PacketFieldUtilityMovingFootholdState movingState = entry?.MovingState;
                    writer.Write(entry?.State ?? 0);
                    writer.Write(movingState?.CurrentX ?? 0);
                    writer.Write(movingState?.CurrentY ?? 0);
                    writer.Write(movingState?.ReverseVertical == true ? (byte)1 : (byte)0);
                    writer.Write(movingState?.ReverseHorizontal == true ? (byte)1 : (byte)0);
                }
            }

            writer.Flush();
            return stream.ToArray();
        }

        private bool TryApplyBlowWeather(byte[] payload, PacketFieldUtilityCallbacks callbacks, out string message)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            byte blowType = reader.ReadByte();
            int itemId = reader.ReadInt32();
            string text = itemId > 0 && blowType == 0 ? ReadMapleString(reader) : string.Empty;
            string weatherPath = itemId > 0 ? callbacks?.ResolveWeatherItemPath?.Invoke(itemId) : null;

            callbacks?.ApplyWeather?.Invoke(itemId, blowType, weatherPath, text);

            _lastWeatherSummary = itemId <= 0
                ? "Cleared packet-authored blow-weather override."
                : string.IsNullOrWhiteSpace(text)
                    ? $"Applied packet-authored weather item {itemId} ({weatherPath ?? "path unavailable"}), blowType={blowType}."
                    : $"Applied packet-authored weather item {itemId} ({weatherPath ?? "path unavailable"}) with message {FormatQuotedValue(text)}.";
            _statusMessage = _lastWeatherSummary;
            message = _lastWeatherSummary;
            return true;
        }

        private bool TryApplyAdminResult(byte[] payload, PacketFieldUtilityCallbacks callbacks, out string message)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            byte subtype = reader.ReadByte();
            PacketFieldUtilityAdminResult result = DecodeAdminResult(subtype, reader);
            callbacks?.PresentAdminResult?.Invoke(result);
            _lastAdminSummary = string.IsNullOrWhiteSpace(result?.Summary)
                ? $"Admin result {subtype}."
                : $"Admin result {subtype}: {result.Summary}";
            _statusMessage = _lastAdminSummary;
            message = _lastAdminSummary;
            return true;
        }

        private bool TryApplyQuiz(byte[] payload, PacketFieldUtilityCallbacks callbacks, out string message)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            bool isQuestion = reader.ReadByte() != 0;
            byte category = reader.ReadByte();
            ushort problemId = reader.ReadUInt16();
            callbacks?.PresentQuizState?.Invoke(isQuestion, category, problemId);

            _lastQuizSummary = problemId == 0
                ? "Cleared packet-authored quiz status."
                : $"{(isQuestion ? "Question" : "Answer")} category {category}, problem {problemId}.";
            _statusMessage = _lastQuizSummary;
            message = _lastQuizSummary;
            return true;
        }

        private bool TryApplyStalkResult(byte[] payload, PacketFieldUtilityCallbacks callbacks, out string message)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            int count = reader.ReadInt32();
            if (count < 0)
            {
                message = "Packet-owned stalk-result count cannot be negative.";
                return false;
            }

            int addedCount = 0;
            int removedCount = 0;
            for (int i = 0; i < count; i++)
            {
                int characterId = reader.ReadInt32();
                bool remove = reader.ReadByte() != 0;
                if (remove)
                {
                    _stalkTargets.Remove(characterId);
                    callbacks?.RemoveStalkTarget?.Invoke(characterId);
                    removedCount++;
                    continue;
                }

                string name = ReadMapleString(reader);
                int x = reader.ReadInt32();
                int y = reader.ReadInt32();
                PacketFieldUtilityStalkTarget target = new(characterId, string.IsNullOrWhiteSpace(name) ? $"Player {characterId}" : name.Trim(), x, y);
                _stalkTargets[characterId] = target;
                callbacks?.UpsertStalkTarget?.Invoke(characterId, target.Name, x, y);
                addedCount++;
            }

            _statusMessage = $"Applied packet-authored stalk-result update ({addedCount} added, {removedCount} removed).";
            message = _statusMessage;
            return true;
        }

        private bool TryApplyQuickslotInit(byte[] payload, PacketFieldUtilityCallbacks callbacks, out string message)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            bool hasCustom = reader.ReadByte() != 0;
            if (!hasCustom)
            {
                callbacks?.ApplyQuickslotKeyMap?.Invoke(null, true);
                _lastQuickslotSummary = "Restored the client default quickslot keymap.";
                _statusMessage = _lastQuickslotSummary;
                message = _statusMessage;
                return true;
            }

            int[] keyCodes = new int[8];
            for (int i = 0; i < keyCodes.Length; i++)
            {
                keyCodes[i] = reader.ReadInt32();
            }

            callbacks?.ApplyQuickslotKeyMap?.Invoke(keyCodes, false);
            _lastQuickslotSummary = $"Hydrated packet-owned quickslot keymap [{string.Join(", ", keyCodes)}].";
            _statusMessage = _lastQuickslotSummary;
            message = _statusMessage;
            return true;
        }

        private bool TryApplyFootholdInfo(byte[] payload, PacketFieldUtilityCallbacks callbacks, out string message)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            int count = reader.ReadInt32();
            if (count <= 0)
            {
                _lastFootholdSummary = "Received packet-authored foothold info with no entries; preserved the existing dynamic foothold state.";
                _statusMessage = _lastFootholdSummary;
                message = _lastFootholdSummary;
                return true;
            }

            List<PacketFieldUtilityFootholdEntry> entries = new(count);
            for (int i = 0; i < count; i++)
            {
                string name = ReadMapleString(reader);
                int state = reader.ReadInt32();
                int footholdCount = reader.ReadInt32();

                int[] serialNumbers = footholdCount > 0
                    ? new int[footholdCount]
                    : Array.Empty<int>();
                for (int footholdIndex = 0; footholdIndex < serialNumbers.Length; footholdIndex++)
                {
                    serialNumbers[footholdIndex] = reader.ReadInt32();
                }

                PacketFieldUtilityMovingFootholdState movingState = null;
                if (state == 2)
                {
                    movingState = new PacketFieldUtilityMovingFootholdState(
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadByte() != 0,
                        reader.ReadByte() != 0);
                }

                entries.Add(new PacketFieldUtilityFootholdEntry(
                    string.IsNullOrWhiteSpace(name) ? $"foothold-{i}" : name.Trim(),
                    state,
                    serialNumbers,
                    movingState));
            }

            callbacks?.ApplyFootholdInfo?.Invoke(entries);
            _lastFootholdSummary = entries.Count == 0
                ? "Cleared packet-authored foothold info."
                : $"Applied packet-authored foothold info for {entries.Count} dynamic object entr{(entries.Count == 1 ? "y" : "ies")}.";
            _statusMessage = _lastFootholdSummary;
            message = _statusMessage;
            return true;
        }

        private bool TryApplyFootholdInfoRequest(PacketFieldUtilityCallbacks callbacks, out string message)
        {
            string responseSummary = callbacks?.RequestFootholdInfo?.Invoke();
            _lastFootholdSummary = string.IsNullOrWhiteSpace(responseSummary)
                ? "Received packet-owned foothold-info request."
                : responseSummary.Trim();
            _statusMessage = _lastFootholdSummary;
            message = _statusMessage;
            return true;
        }

        private static PacketFieldUtilityAdminResult DecodeAdminResult(byte subtype, BinaryReader reader)
        {
            return subtype switch
            {
                11 => DecodeAdminResultNotice(reader, subtype),
                18 => new PacketFieldUtilityAdminResult(subtype, "Admin", $"Updated the admin-hide flag to {reader.ReadByte()}.", string.Empty, -1, 12, false, false, false),
                21 => DecodeAdminResultMapOrChannel(reader, subtype),
                40 => new PacketFieldUtilityAdminResult(subtype, "Admin", "Reloaded the minimap.", string.Empty, -1, 12, false, true, false),
                41 => new PacketFieldUtilityAdminResult(subtype, "Admin", "Applied the client minimap-hidden state.", string.Empty, -1, 12, false, false, true),
                42 => DecodeAdminResultClaimState(reader, subtype),
                43 => DecodeAdminBlockState(reader, subtype),
                51 or 52 or 53 or 54 or 55 or 56 or 57 => DecodeChatOnlyAdminResult(reader, subtype, 11),
                58 => DecodeChatOnlyAdminResult(reader, subtype, 12),
                71 => DecodeChatOnlyAdminResult(reader, subtype, 12),
                72 => DecodeChatOnlyAdminResult(reader, subtype, 11),
                4 => DecodeAdminResultMode(reader, subtype),
                5 => DecodeAdminResultMode(reader, subtype),
                6 => new PacketFieldUtilityAdminResult(
                    subtype,
                    "Admin",
                    "Processed the subtype-6 direct notice branch.",
                    DecodeMapleAdminNoticeMode(reader, out int stringPoolId),
                    stringPoolId,
                    9,
                    true,
                    false,
                    false),
                _ => new PacketFieldUtilityAdminResult(subtype, "Admin", $"Unhandled admin-result subtype {subtype}.", string.Empty, -1, 12, false, false, false)
            };
        }

        private static PacketFieldUtilityAdminResult DecodeAdminResultNotice(BinaryReader reader, byte subtype)
        {
            string channel = ReadMapleString(reader);
            if (string.IsNullOrWhiteSpace(channel))
            {
                string noticeBody = PacketFieldUtilityAdminResultStringPoolText.GetSubtype11EmptyChannelText();
                return new PacketFieldUtilityAdminResult(
                    subtype,
                    "Admin",
                    "Processed the subtype-11 empty-channel branch.",
                    noticeBody,
                    PacketFieldUtilityAdminResultStringPoolText.WrongNpcNameStringPoolId,
                    12,
                    true,
                    false,
                    false);
            }

            string world = ReadMapleString(reader);
            string body = ReadMapleString(reader);
            string message = string.IsNullOrWhiteSpace(world)
                ? $"[{channel}] {body}"
                : $"[{channel}] [{world}] {body}";
            return new PacketFieldUtilityAdminResult(subtype, "Admin Notice", $"Posted admin notice on {channel}.", message, -1, 12, true, false, false);
        }

        private static PacketFieldUtilityAdminResult DecodeAdminResultMapOrChannel(BinaryReader reader, byte subtype)
        {
            bool hasChannel = reader.ReadByte() != 0;
            if (hasChannel)
            {
                byte channel = reader.ReadByte();
                bool success = channel <= 0xFD;
                string target = $"channel {channel}";
                string body = success
                    ? PacketFieldUtilityAdminResultStringPoolText.FormatHiredMerchantLocationText(target)
                    : PacketFieldUtilityAdminResultStringPoolText.GetHiredMerchantNotFoundText();
                return new PacketFieldUtilityAdminResult(
                    subtype,
                    "Admin",
                    success ? "Reported the hired merchant location." : "Unable to find the hired merchant.",
                    body,
                    success
                        ? PacketFieldUtilityAdminResultStringPoolText.HiredMerchantLocatedFormatStringPoolId
                        : PacketFieldUtilityAdminResultStringPoolText.HiredMerchantNotFoundStringPoolId,
                    12,
                    true,
                    false,
                    false);
            }

            int mapId = reader.ReadInt32();
            string mapName = PacketFieldUtilityAdminResultStringPoolText.GetMapNameFallback(mapId);
            string bodyText = PacketFieldUtilityAdminResultStringPoolText.FormatHiredMerchantLocationText(mapName);
            return new PacketFieldUtilityAdminResult(
                subtype,
                "Admin",
                $"Reported the hired merchant location for map {mapId}.",
                bodyText,
                PacketFieldUtilityAdminResultStringPoolText.HiredMerchantLocatedFormatStringPoolId,
                12,
                true,
                false,
                false);
        }

        private static PacketFieldUtilityAdminResult DecodeAdminResultClaimState(BinaryReader reader, byte subtype)
        {
            bool enabled = reader.ReadByte() != 0;
            return enabled
                ? new PacketFieldUtilityAdminResult(subtype, "Admin", "Updated the admin-claim state.", string.Empty, -1, 9, false, false, false)
                : new PacketFieldUtilityAdminResult(
                    subtype,
                    "Admin",
                    "Processed the subtype-42 failure branch.",
                    PacketFieldUtilityAdminResultStringPoolText.GetSubtype42FailureText(),
                    PacketFieldUtilityAdminResultStringPoolText.RequestFailedStringPoolId,
                    9,
                    true,
                    false,
                    false);
        }

        private static PacketFieldUtilityAdminResult DecodeAdminBlockState(BinaryReader reader, byte subtype)
        {
            bool enabled = reader.ReadByte() != 0;
            return new PacketFieldUtilityAdminResult(
                subtype,
                "Admin",
                enabled ? "Warning send succeeded." : "Warning send failed.",
                PacketFieldUtilityAdminResultStringPoolText.GetWarningText(enabled),
                enabled
                    ? PacketFieldUtilityAdminResultStringPoolText.WarningSentStringPoolId
                    : PacketFieldUtilityAdminResultStringPoolText.WarningMessageNotEnteredStringPoolId,
                9,
                true,
                false,
                false);
        }

        private static PacketFieldUtilityAdminResult DecodeChatOnlyAdminResult(BinaryReader reader, byte subtype, int chatLogType)
        {
            string body = ReadMapleString(reader);
            return new PacketFieldUtilityAdminResult(
                subtype,
                "Admin",
                string.IsNullOrWhiteSpace(body) ? $"Received admin subtype {subtype}." : $"Received admin text for subtype {subtype}.",
                body,
                -1,
                chatLogType,
                !string.IsNullOrWhiteSpace(body),
                false,
                false);
        }

        private static PacketFieldUtilityAdminResult DecodeAdminResultMode(BinaryReader reader, byte subtype)
        {
            reader.ReadByte();
            return new PacketFieldUtilityAdminResult(
                subtype,
                "Admin",
                subtype == 4 ? "Processed the block-access success branch." : "Processed the unblock-access success branch.",
                subtype == 4
                    ? PacketFieldUtilityAdminResultStringPoolText.GetSubtype4Notice()
                    : PacketFieldUtilityAdminResultStringPoolText.GetSubtype5Notice(),
                subtype == 4
                    ? PacketFieldUtilityAdminResultStringPoolText.BlockAccessSuccessStringPoolId
                    : PacketFieldUtilityAdminResultStringPoolText.UnblockAccessSuccessStringPoolId,
                9,
                true,
                false,
                false);
        }

        private static string DecodeMapleAdminNoticeMode(BinaryReader reader, out int stringPoolId)
        {
            bool successBranch = reader.ReadByte() != 0;
            stringPoolId = successBranch
                ? PacketFieldUtilityAdminResultStringPoolText.InvalidCharacterNameStringPoolId
                : PacketFieldUtilityAdminResultStringPoolText.RemoveNameFromRanksStringPoolId;
            return PacketFieldUtilityAdminResultStringPoolText.GetSubtype6Notice(successBranch);
        }

        private static bool Unsupported(PacketFieldUtilityPacketKind kind, out string message)
        {
            message = $"Unsupported field utility packet kind {(int)kind}.";
            return false;
        }

        private static string ReadMapleString(BinaryReader reader)
        {
            short length = reader.ReadInt16();
            if (length <= 0)
            {
                return string.Empty;
            }

            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException($"Expected {length.ToString(CultureInfo.InvariantCulture)} MapleString byte(s), read {bytes.Length.ToString(CultureInfo.InvariantCulture)}.");
            }

            return Encoding.Default.GetString(bytes);
        }

        private static void WriteMapleString(BinaryWriter writer, string value)
        {
            value ??= string.Empty;
            byte[] bytes = Encoding.Default.GetBytes(value);
            writer.Write((short)bytes.Length);
            writer.Write(bytes);
        }

        private static string FormatQuotedValue(string value)
        {
            return $"\"{value?.Replace("\"", "\\\"", StringComparison.Ordinal) ?? string.Empty}\"";
        }

        private static string TrimForStatus(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            value = value.Trim();
            return value.Length <= 80 ? value : value[..77] + "...";
        }
    }
}
