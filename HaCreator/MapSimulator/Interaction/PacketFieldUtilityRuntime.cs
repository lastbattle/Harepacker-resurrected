using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

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
        string Body,
        int ChatLogType,
        bool ShowPrompt,
        bool ReloadMinimap,
        bool ToggleMinimap);

    internal sealed class PacketFieldUtilityCallbacks
    {
        internal Func<int, string> ResolveWeatherItemPath { get; init; }
        internal Action<int, byte, string, string> ApplyWeather { get; init; }
        internal Action<PacketFieldUtilityAdminResult> PresentAdminResult { get; init; }
        internal Action<string, bool, byte, ushort> PresentQuizState { get; init; }
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
            _lastAdminSummary = $"Admin result {subtype}: {result.Body}";
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

            string quizText = problemId == 0
                ? null
                : $"{(isQuestion ? "Question" : "Answer")} {category.ToString(CultureInfo.InvariantCulture)}-{problemId.ToString(CultureInfo.InvariantCulture)}";
            callbacks?.PresentQuizState?.Invoke(quizText, isQuestion, category, problemId);

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
            if (count < 0)
            {
                message = "Packet-owned foothold-info count cannot be negative.";
                return false;
            }

            List<PacketFieldUtilityFootholdEntry> entries = new(count);
            for (int i = 0; i < count; i++)
            {
                string name = ReadMapleString(reader);
                int state = reader.ReadInt32();
                int footholdCount = reader.ReadInt32();
                if (footholdCount < 0)
                {
                    message = "Packet-owned foothold-info foothold count cannot be negative.";
                    return false;
                }

                int[] serialNumbers = new int[footholdCount];
                for (int footholdIndex = 0; footholdIndex < footholdCount; footholdIndex++)
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
                18 => new PacketFieldUtilityAdminResult(subtype, "Admin", $"Updated admin-hide flag to {reader.ReadByte()}.", 12, false, false, false),
                21 => DecodeAdminResultMapOrChannel(reader, subtype),
                40 => new PacketFieldUtilityAdminResult(subtype, "Admin", "Reloaded the minimap.", 12, false, true, false),
                41 => new PacketFieldUtilityAdminResult(subtype, "Admin", "Toggled the minimap state.", 12, false, false, true),
                42 => new PacketFieldUtilityAdminResult(subtype, "Admin", reader.ReadByte() != 0 ? string.Empty : "Admin claim is unavailable.", 9, false, false, false),
                43 => new PacketFieldUtilityAdminResult(subtype, "Admin", reader.ReadByte() != 0 ? "Admin block is enabled." : "Admin block is disabled.", 9, true, false, false),
                51 or 52 or 53 or 54 or 55 or 56 or 57 => new PacketFieldUtilityAdminResult(subtype, "Admin", ReadMapleString(reader), 11, true, false, false),
                58 => new PacketFieldUtilityAdminResult(subtype, "Admin", ReadMapleString(reader), 12, true, false, false),
                71 => new PacketFieldUtilityAdminResult(subtype, "Admin", ReadMapleString(reader), 12, true, false, false),
                72 => new PacketFieldUtilityAdminResult(subtype, "Admin", ReadMapleString(reader), 11, true, false, false),
                4 => new PacketFieldUtilityAdminResult(subtype, "Admin", $"Applied admin result subtype 4 (mode={reader.ReadByte()}).", 9, false, false, false),
                5 => new PacketFieldUtilityAdminResult(subtype, "Admin", $"Applied admin result subtype 5 (mode={reader.ReadByte()}).", 9, false, false, false),
                6 => new PacketFieldUtilityAdminResult(subtype, "Admin", reader.ReadByte() != 0 ? "Enabled Maple Admin notice mode." : "Disabled Maple Admin notice mode.", 9, false, false, false),
                _ => new PacketFieldUtilityAdminResult(subtype, "Admin", $"Unhandled admin-result subtype {subtype}.", 12, false, false, false)
            };
        }

        private static PacketFieldUtilityAdminResult DecodeAdminResultNotice(BinaryReader reader, byte subtype)
        {
            string channel = ReadMapleString(reader);
            if (string.IsNullOrWhiteSpace(channel))
            {
                return new PacketFieldUtilityAdminResult(subtype, "Admin", "Cleared the current admin notice.", 12, false, false, false);
            }

            string world = ReadMapleString(reader);
            string body = ReadMapleString(reader);
            string message = string.IsNullOrWhiteSpace(world)
                ? $"[{channel}] {body}"
                : $"[{channel}] [{world}] {body}";
            return new PacketFieldUtilityAdminResult(subtype, "Admin Notice", message, 12, true, false, false);
        }

        private static PacketFieldUtilityAdminResult DecodeAdminResultMapOrChannel(BinaryReader reader, byte subtype)
        {
            bool hasChannel = reader.ReadByte() != 0;
            if (hasChannel)
            {
                byte channel = reader.ReadByte();
                return new PacketFieldUtilityAdminResult(subtype, "Admin", $"Moved to channel {channel}.", 12, false, false, false);
            }

            int mapId = reader.ReadInt32();
            return new PacketFieldUtilityAdminResult(subtype, "Admin", $"Moved to map {mapId}.", 12, false, false, false);
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
