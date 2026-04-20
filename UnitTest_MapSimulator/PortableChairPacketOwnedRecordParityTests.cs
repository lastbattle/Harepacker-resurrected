using HaCreator.MapSimulator.Pools;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class PortableChairPacketOwnedRecordParityTests
{
    [Fact]
    public void IsCoupleChairRecordItemIdForClientParity_OnlyAccepts3012Series()
    {
        Assert.True(RemoteUserActorPool.IsCoupleChairRecordItemIdForClientParity(3012000));
        Assert.True(RemoteUserActorPool.IsCoupleChairRecordItemIdForClientParity(3012999));
        Assert.False(RemoteUserActorPool.IsCoupleChairRecordItemIdForClientParity(3011000));
        Assert.False(RemoteUserActorPool.IsCoupleChairRecordItemIdForClientParity(3013000));
        Assert.False(RemoteUserActorPool.IsCoupleChairRecordItemIdForClientParity(0));
    }

    [Fact]
    public void NormalizePortableChairRecordAddForClientParity_ClearsPairAndStatus()
    {
        var packet = new RemoteUserPortableChairRecordAddPacket(
            CharacterId: 1001,
            ChairItemId: 3012001,
            PairCharacterId: 2002,
            Status: 3);

        RemoteUserPortableChairRecordAddPacket normalized =
            RemoteUserActorPool.NormalizePortableChairRecordAddForClientParity(packet);

        Assert.Equal(1001, normalized.CharacterId);
        Assert.Equal(3012001, normalized.ChairItemId);
        Assert.Null(normalized.PairCharacterId);
        Assert.Equal(0, normalized.Status);
    }
}
