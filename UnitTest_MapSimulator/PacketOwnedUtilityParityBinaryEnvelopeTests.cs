using HaCreator.MapSimulator;
using System;
using System.IO;
using System.Text;

namespace UnitTest_MapSimulator;

public sealed class PacketOwnedUtilityParityBinaryEnvelopeTests
{
    [Fact]
    public void TryDecodeRankingPagePayload_AcceptsLengthPrefixedCompactInt32RowCountPayload()
    {
        byte[] body = BuildRankingCompactBinaryPayloadWithInt32RowCount("World Rank", "1", "Hero");
        byte[] payload = PrefixWithLength(body, includeHeaderInLength: false);

        bool decoded = MapSimulator.TryDecodePacketOwnedRankingPagePayloadForTests(
            payload,
            out bool clearRequested,
            out var entries,
            out bool hasOwnerState,
            out _,
            out _,
            out _);

        Assert.True(decoded);
        Assert.False(clearRequested);
        Assert.False(hasOwnerState);
        Assert.Single(entries);
        Assert.Equal("World Rank", entries[0].Label);
        Assert.Equal("1", entries[0].Value);
        Assert.Equal("Hero", entries[0].Detail);
    }

    [Fact]
    public void TryDecodeEventCalendarEntriesPayload_AcceptsLengthPrefixedPackedDateInt32RowCountPayload()
    {
        byte[] body = BuildEventCompactBinaryPayloadWithInt32RowCountAndPackedDate(2026, 4, 20, "Hot Time", "Bonus active", "Ongoing");
        byte[] payload = PrefixWithLength(body, includeHeaderInLength: true);

        bool decoded = MapSimulator.TryDecodePacketOwnedEventCalendarEntriesPayloadForTests(
            payload,
            out bool clearRequested,
            out bool replaceExistingEntries,
            out var entries,
            out bool hasAlarmLines,
            out bool replaceAlarmLines,
            out var alarmLines,
            out _,
            out _);

        Assert.True(decoded);
        Assert.False(clearRequested);
        Assert.True(replaceExistingEntries);
        Assert.False(hasAlarmLines);
        Assert.True(replaceAlarmLines);
        Assert.Empty(alarmLines);
        Assert.Single(entries);
        Assert.Equal(new DateTime(2026, 4, 20), entries[0].ScheduledAt.Date);
        Assert.Equal("Hot Time", entries[0].Title);
        Assert.Equal("Bonus active", entries[0].Detail);
    }

    private static byte[] BuildRankingCompactBinaryPayloadWithInt32RowCount(string label, string value, string detail)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write((byte)0x00);
        writer.Write(1);
        WriteMapleString(writer, label);
        WriteMapleString(writer, value);
        WriteMapleString(writer, detail);
        return stream.ToArray();
    }

    private static byte[] BuildEventCompactBinaryPayloadWithInt32RowCountAndPackedDate(
        int year,
        int month,
        int day,
        string title,
        string detail,
        string statusText)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write((byte)0x00);
        writer.Write(1);
        writer.Write((year * 10000) + (month * 100) + day);
        writer.Write((byte)1);
        WriteMapleString(writer, title);
        WriteMapleString(writer, detail);
        WriteMapleString(writer, statusText);
        return stream.ToArray();
    }

    private static byte[] PrefixWithLength(byte[] body, bool includeHeaderInLength)
    {
        byte[] payloadBody = body ?? Array.Empty<byte>();
        int declaredLength = includeHeaderInLength
            ? payloadBody.Length + sizeof(int)
            : payloadBody.Length;

        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(declaredLength);
        writer.Write(payloadBody);
        return stream.ToArray();
    }

    private static void WriteMapleString(BinaryWriter writer, string value)
    {
        string text = value ?? string.Empty;
        byte[] bytes = Encoding.Default.GetBytes(text);
        writer.Write((ushort)bytes.Length);
        writer.Write(bytes);
    }
}
