using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator;

public sealed class PacketOwnedLocalOverlayParityTests
{
    [Fact]
    public void ClassCompetitionLaunchRejectsTrailingBytes()
    {
        bool decoded = MapSimulator.TryDecodePacketOwnedClassCompetitionLaunchPayloadForTests(
            new byte[] { 0x01 },
            out string message);

        Assert.False(decoded);
        Assert.Contains("should be empty", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClassCompetitionAuthDecodeAcceptsMapleStringPayload()
    {
        byte[] payload = BuildMapleStringPayload("AuthKey_291");

        bool decoded = MapSimulator.TryDecodeClassCompetitionAuthKeyPayloadForTests(
            payload,
            out string authKey,
            out string detail);

        Assert.True(decoded);
        Assert.Equal("AuthKey_291", authKey);
        Assert.Contains("Recovered auth token", detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeliveryQuestDecodeSupportsInt32CountWithUInt16QuestIds()
    {
        byte[] payload = BuildDeliveryQuestPayload(
            questId: 1007,
            itemId: 2430071,
            disallowedQuestIds: new[] { 2001, 2002 },
            disallowedCountWidth: sizeof(int),
            disallowedEntryWidth: sizeof(short),
            deliveryTypeWidth: sizeof(byte),
            deliveryTypeRaw: 1);

        bool decoded = MapSimulator.TryDecodePacketOwnedDeliveryQuestPayloadForTests(
            payload,
            out int questId,
            out int itemId,
            out int[] disallowedQuestIds,
            out QuestDetailDeliveryType deliveryType,
            out string error);

        Assert.True(decoded, error);
        Assert.Equal(1007, questId);
        Assert.Equal(2430071, itemId);
        Assert.Equal(new[] { 2001, 2002 }, disallowedQuestIds);
        Assert.Equal(QuestDetailDeliveryType.Complete, deliveryType);
    }

    [Fact]
    public void DeliveryQuestDecodeRejectsUnsupportedDiscriminatorWidth()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true))
        {
            writer.Write(1007);
            writer.Write(2430071);
            writer.Write((short)0);
            writer.Write(new byte[] { 0x00, 0x01, 0x02 }); // Invalid trailing discriminator width (3 bytes).
        }

        bool decoded = MapSimulator.TryDecodePacketOwnedDeliveryQuestPayloadForTests(
            stream.ToArray(),
            out _,
            out _,
            out _,
            out _,
            out string error);

        Assert.False(decoded);
        Assert.Contains("expected 0, 1, 2, or 4 bytes", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeliveryQuestDecodeRejectsUnsupportedDiscriminatorValue()
    {
        byte[] payload = BuildDeliveryQuestPayload(
            questId: 1007,
            itemId: 2430071,
            disallowedQuestIds: Array.Empty<int>(),
            disallowedCountWidth: sizeof(short),
            disallowedEntryWidth: sizeof(short),
            deliveryTypeWidth: sizeof(short),
            deliveryTypeRaw: 7);

        bool decoded = MapSimulator.TryDecodePacketOwnedDeliveryQuestPayloadForTests(
            payload,
            out _,
            out _,
            out _,
            out _,
            out string error);

        Assert.False(decoded);
        Assert.Contains("expected 0, 1, or 2", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LocalUtilityInboxParsesOpcode291FromFramedClientPayload()
    {
        byte[] rawClientPacket = BuildOpcodeFramedPacket(
            LocalUtilityPacketInboxManager.ClassCompetitionAuthCachePacketType,
            BuildMapleStringPayload("FramedAuth_291"));
        string line = "packetclientraw " + Convert.ToHexString(rawClientPacket);

        bool parsed = LocalUtilityPacketInboxManager.TryParseLine(
            line,
            out LocalUtilityPacketInboxMessage message,
            out string error);

        Assert.True(parsed, error);
        Assert.NotNull(message);
        Assert.Equal(LocalUtilityPacketInboxManager.ClassCompetitionAuthCachePacketType, message.PacketType);
        Assert.Equal(BuildMapleStringPayload("FramedAuth_291"), message.Payload);
    }

    [Fact]
    public void LocalUtilityInboxParsesClassCompetitionAuthAlias()
    {
        bool parsed = LocalUtilityPacketInboxManager.TryParseLine(
            "classcompetitionauth payloadhex=4142434445465F323931",
            out LocalUtilityPacketInboxMessage message,
            out string error);

        Assert.True(parsed, error);
        Assert.NotNull(message);
        Assert.Equal(LocalUtilityPacketInboxManager.ClassCompetitionAuthCachePacketType, message.PacketType);
    }

    [Fact]
    public void QuestDemandItemMapResolutionUsesPacketMobTargets()
    {
        int resolverCalls = 0;
        QuestDemandItemQueryState queryState = MapSimulator.BuildPacketOwnedQuestDemandItemQueryState(
            questId: 3000,
            records: new[]
            {
                (PrimaryId: 4000010, ChildIds: (IReadOnlyList<int>)new[] { 9300012, 9300013 })
            },
            currentMapId: 100000000,
            runtimeFallbackQuery: new QuestDemandItemQueryState
            {
                QuestId = 3000,
                VisibleItemIds = new[] { 4000010 },
                VisibleItemMapResults = new Dictionary<int, QuestDemandItemMapResultSet>()
            },
            resolveMobMapIds: mobId =>
            {
                resolverCalls++;
                return mobId switch
                {
                    9300012 => new[] { 101000000, 100000000 },
                    9300013 => new[] { 102000000 },
                    _ => Array.Empty<int>()
                };
            });

        Assert.True(queryState.HasPacketOwnedMapResults);
        Assert.True(resolverCalls >= 2);
        Assert.True(queryState.VisibleItemMapResults.TryGetValue(4000010, out QuestDemandItemMapResultSet resultSet));
        Assert.Equal(QuestDemandItemMapResultSource.PacketOwnedMob, resultSet.Source);
        Assert.Equal(new[] { 100000000, 101000000, 102000000 }, resultSet.MapIds.OrderBy(static id => id).ToArray());
    }

    private static byte[] BuildMapleStringPayload(string text)
    {
        byte[] bytes = Encoding.Default.GetBytes(text ?? string.Empty);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true))
        {
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }

        return stream.ToArray();
    }

    private static byte[] BuildOpcodeFramedPacket(int opcode, byte[] payload)
    {
        byte[] safePayload = payload ?? Array.Empty<byte>();
        byte[] raw = new byte[sizeof(ushort) + safePayload.Length];
        BitConverter.GetBytes((ushort)opcode).CopyTo(raw, 0);
        if (safePayload.Length > 0)
        {
            Buffer.BlockCopy(safePayload, 0, raw, sizeof(ushort), safePayload.Length);
        }

        return raw;
    }

    private static byte[] BuildDeliveryQuestPayload(
        int questId,
        int itemId,
        IReadOnlyList<int> disallowedQuestIds,
        int disallowedCountWidth,
        int disallowedEntryWidth,
        int deliveryTypeWidth,
        int deliveryTypeRaw)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true))
        {
            writer.Write(questId);
            writer.Write(itemId);

            int count = Math.Max(0, disallowedQuestIds?.Count ?? 0);
            WriteCount(writer, disallowedCountWidth, count);
            for (int i = 0; i < count; i++)
            {
                WriteEntry(writer, disallowedEntryWidth, disallowedQuestIds[i]);
            }

            WriteCount(writer, deliveryTypeWidth, deliveryTypeRaw);
        }

        return stream.ToArray();
    }

    private static void WriteCount(BinaryWriter writer, int width, int value)
    {
        switch (width)
        {
            case 0:
                return;
            case 1:
                writer.Write((byte)value);
                return;
            case 2:
                writer.Write((short)value);
                return;
            case 4:
                writer.Write(value);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(width), width, "Unsupported encoded width.");
        }
    }

    private static void WriteEntry(BinaryWriter writer, int width, int value)
    {
        switch (width)
        {
            case 2:
                writer.Write((short)value);
                return;
            case 4:
                writer.Write(value);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(width), width, "Unsupported entry width.");
        }
    }
}
