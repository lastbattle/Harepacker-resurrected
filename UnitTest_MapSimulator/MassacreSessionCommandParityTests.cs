using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator;

public sealed class MassacreSessionCommandParityTests
{
    [Fact]
    public void TryParseRecentCount_AcceptsPositiveCounts()
    {
        Assert.True(MassacreSessionCommandParsing.TryParseRecentCount("1", out int oneCount));
        Assert.Equal(1, oneCount);

        Assert.True(MassacreSessionCommandParsing.TryParseRecentCount("25", out int manyCount));
        Assert.Equal(25, manyCount);
    }

    [Fact]
    public void TryParseRecentCount_RejectsInvalidCounts()
    {
        Assert.False(MassacreSessionCommandParsing.TryParseRecentCount("0", out _));
        Assert.False(MassacreSessionCommandParsing.TryParseRecentCount("-1", out _));
        Assert.False(MassacreSessionCommandParsing.TryParseRecentCount("abc", out _));
    }

    [Fact]
    public void TryParseMappedPacketKind_AcceptsIncGaugeAlias()
    {
        Assert.True(MassacreSessionCommandParsing.TryParseMappedPacketKind("inc-gauge", out MassacrePacketInboxMessageKind kind));
        Assert.Equal(MassacrePacketInboxMessageKind.IncGauge, kind);
    }

    [Fact]
    public void ClearRecentInboundPackets_ResetsRecentHistoryStatus()
    {
        using var bridge = new MassacreOfficialSessionBridgeManager();
        string cleared = bridge.ClearRecentInboundPackets();

        Assert.Equal("Massacre official-session bridge inbound history cleared.", cleared);
        Assert.Equal(cleared, bridge.LastStatus);
        Assert.Equal("Massacre official-session bridge inbound history is empty.", bridge.DescribeRecentInboundPackets());
    }
}
