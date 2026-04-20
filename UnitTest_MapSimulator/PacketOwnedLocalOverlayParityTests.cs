using System.Text;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Interaction;

namespace UnitTest_MapSimulator;

public sealed class PacketOwnedLocalOverlayParityTests
{
    [Fact]
    public void DeliveryQuestDecoder_AcceptsInt32CountWithUInt16QuestIds_AndDeliveryType()
    {
        byte[] payload = BuildDeliveryQuestPayload(
            questId: 3501,
            itemId: 2430071,
            disallowedQuestIds: new[] { 300, 301, 302 },
            deliveryTypeRaw: 2,
            useUInt16QuestIds: true);

        bool decoded = MapSimulator.TryDecodePacketOwnedDeliveryQuestPayloadForTests(
            payload,
            out int questId,
            out int itemId,
            out int[] disallowedQuestIds,
            out QuestDetailDeliveryType deliveryType,
            out string error);

        Assert.True(decoded, error);
        Assert.Equal(3501, questId);
        Assert.Equal(2430071, itemId);
        Assert.Equal(new[] { 300, 301, 302 }, disallowedQuestIds);
        Assert.Equal(QuestDetailDeliveryType.Complete, deliveryType);
    }

    [Fact]
    public void DeliveryQuestDecoder_RejectsInvalidOptionalDeliveryTypeWidth()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
        writer.Write(3501);
        writer.Write(2430071);
        writer.Write(1);
        writer.Write(300);
        writer.Write((byte)1);
        writer.Write((byte)2);
        writer.Write((byte)3);
        writer.Flush();

        bool decoded = MapSimulator.TryDecodePacketOwnedDeliveryQuestPayloadForTests(
            stream.ToArray(),
            out _,
            out _,
            out _,
            out _,
            out string error);

        Assert.False(decoded);
        Assert.Contains("expected 0, 1, 2, or 4 bytes", error);
    }

    [Fact]
    public void ClassCompetitionLaunchDecoder_RequiresEmptyPayload()
    {
        bool decodedEmpty = MapSimulator.TryDecodePacketOwnedClassCompetitionLaunchPayloadForTests(Array.Empty<byte>(), out string emptyMessage);
        bool decodedNonEmpty = MapSimulator.TryDecodePacketOwnedClassCompetitionLaunchPayloadForTests(new byte[] { 0x01 }, out string nonEmptyMessage);

        Assert.True(decodedEmpty, emptyMessage);
        Assert.False(decodedNonEmpty);
        Assert.Contains("should be empty", nonEmptyMessage);
    }

    [Fact]
    public void ClassCompetitionAuthDecoder_AcceptsMapleStringAuthToken()
    {
        const string token = "AUTH_TOKEN_1234";
        byte[] payload = BuildMapleStringPayload(token);

        bool decoded = MapSimulator.TryDecodeClassCompetitionAuthKeyPayloadForTests(payload, out string authKey, out string detail);

        Assert.True(decoded, detail);
        Assert.Equal(token, authKey);
    }

    [Fact]
    public void QuestDemandItemQueryState_UsesCurrentPacketMobTargetsForItemRows()
    {
        IReadOnlyList<(int PrimaryId, IReadOnlyList<int> ChildIds)> records = new[]
        {
            (PrimaryId: 4001126, ChildIds: (IReadOnlyList<int>)new[] { 9300012, 9300013 })
        };

        IReadOnlyList<int> ResolveMobMapIds(int mobId)
        {
            return mobId switch
            {
                9300012 => new[] { 100000000 },
                9300013 => new[] { 200000000 },
                _ => Array.Empty<int>()
            };
        }

        QuestDemandItemQueryState state = MapSimulator.BuildPacketOwnedQuestDemandItemQueryState(
            questId: 1020,
            records: records,
            currentMapId: 999999999,
            runtimeFallbackQuery: null,
            resolveMobMapIds: ResolveMobMapIds);

        Assert.NotNull(state);
        Assert.True(state.HasPacketOwnedMapResults);
        Assert.Equal(new[] { 4001126 }, state.VisibleItemIds);
        Assert.True(state.VisibleItemMapIds.TryGetValue(4001126, out IReadOnlyList<int> mapIds));
        Assert.Equal(new[] { 100000000, 200000000 }, mapIds);
        Assert.True(state.VisibleItemMapResults.TryGetValue(4001126, out QuestDemandItemMapResultSet resultSet));
        Assert.Equal(QuestDemandItemMapResultSource.PacketOwnedMob, resultSet.Source);
    }

    [Fact]
    public void ClassCompetitionAuthCacheDecoder_AcceptsMapleStringStructuredRemotePayload()
    {
        const string json = "{\"navigateUrl\":\"http://gamerank.maplestory.nexon.com/maplestory/page/Gnxgame.aspx?URL=Event/classbattle/gameview&key=TOKEN\",\"source\":\"packet 291\"}";
        byte[] payload = BuildMapleStringPayload(json);

        bool decoded = MapSimulator.TryDecodePacketOwnedClassCompetitionAuthCachePayloadForTests(
            payload,
            out string authKey,
            out MapSimulator.ClassCompetitionRemotePagePayload remotePayload,
            out string detail);

        Assert.True(decoded, detail);
        Assert.Equal(string.Empty, authKey);
        Assert.NotNull(remotePayload);
        Assert.Equal("packet 291", remotePayload.Source);
        Assert.Contains("gameview&key=TOKEN", remotePayload.NavigateUrl);
    }

    private static byte[] BuildMapleStringPayload(string value)
    {
        byte[] bytes = Encoding.Default.GetBytes(value ?? string.Empty);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
        writer.Write((ushort)bytes.Length);
        writer.Write(bytes);
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] BuildDeliveryQuestPayload(
        int questId,
        int itemId,
        IReadOnlyList<int> disallowedQuestIds,
        int? deliveryTypeRaw,
        bool useUInt16QuestIds)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
        writer.Write(questId);
        writer.Write(itemId);
        writer.Write(disallowedQuestIds?.Count ?? 0);
        if (disallowedQuestIds != null)
        {
            for (int i = 0; i < disallowedQuestIds.Count; i++)
            {
                if (useUInt16QuestIds)
                {
                    writer.Write((ushort)disallowedQuestIds[i]);
                }
                else
                {
                    writer.Write(disallowedQuestIds[i]);
                }
            }
        }

        if (deliveryTypeRaw.HasValue)
        {
            writer.Write((ushort)deliveryTypeRaw.Value);
        }

        writer.Flush();
        return stream.ToArray();
    }
}
