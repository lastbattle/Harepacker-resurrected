using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;
using System.IO;

namespace UnitTest_MapSimulator;

public sealed class RemoteUserFollowCharacterTests
{
    [Fact]
    public void AttachFollowCharacter_LinksPassengerAndDriver()
    {
        RemoteUserActorPool pool = CreatePoolWithTwoActors();

        bool applied = pool.TryApplyFollowCharacter(1, 2, false, null, 999, new Vector2(500f, 600f), out string message);

        Assert.True(applied, message);
        Assert.True(pool.TryGetActor(1, out RemoteUserActor passenger));
        Assert.True(pool.TryGetActor(2, out RemoteUserActor driver));
        Assert.Equal(2, passenger.FollowDriverId);
        Assert.Equal(1, driver.FollowPassengerId);
    }

    [Fact]
    public void DetachWithoutTransfer_SnapsPassengerToPreviousDriverPosition()
    {
        RemoteUserActorPool pool = CreatePoolWithTwoActors();
        Assert.True(pool.TryApplyFollowCharacter(1, 2, false, null, 999, new Vector2(500f, 600f), out _));

        bool detached = pool.TryApplyFollowCharacter(1, 0, false, null, 999, new Vector2(500f, 600f), out string message);

        Assert.True(detached, message);
        Assert.True(pool.TryGetActor(1, out RemoteUserActor passenger));
        Assert.True(pool.TryGetActor(2, out RemoteUserActor driver));
        Assert.Equal(0, passenger.FollowDriverId);
        Assert.Equal(0, driver.FollowPassengerId);
        Assert.Equal(driver.Position, passenger.Position);
    }

    [Fact]
    public void DetachWithTransferField_UsesExplicitTransferPosition()
    {
        RemoteUserActorPool pool = CreatePoolWithTwoActors();
        Assert.True(pool.TryApplyFollowCharacter(1, 999, false, null, 999, new Vector2(500f, 600f), out _));

        Vector2 transferPosition = new(321f, 654f);
        bool detached = pool.TryApplyFollowCharacter(1, 0, true, transferPosition, 999, new Vector2(500f, 600f), out string message);

        Assert.True(detached, message);
        Assert.True(pool.TryGetActor(1, out RemoteUserActor passenger));
        Assert.Equal(0, passenger.FollowDriverId);
        Assert.Equal(transferPosition, passenger.Position);
    }

    [Fact]
    public void FollowCharacterCodec_DecodesAttachAndTransferDetachPayloads()
    {
        byte[] attachPayload = BuildPayload(writer =>
        {
            writer.Write(1);
            writer.Write(2);
        });
        byte[] detachPayload = BuildPayload(writer =>
        {
            writer.Write(1);
            writer.Write(0);
            writer.Write((byte)1);
            writer.Write(123);
            writer.Write(456);
        });

        Assert.True(RemoteUserPacketCodec.TryParseFollowCharacter(attachPayload, out RemoteUserFollowCharacterPacket attachPacket, out string attachError), attachError);
        Assert.Equal(1, attachPacket.CharacterId);
        Assert.Equal(2, attachPacket.DriverId);
        Assert.False(attachPacket.TransferField);

        Assert.True(RemoteUserPacketCodec.TryParseFollowCharacter(detachPayload, out RemoteUserFollowCharacterPacket detachPacket, out string detachError), detachError);
        Assert.Equal(1, detachPacket.CharacterId);
        Assert.Equal(0, detachPacket.DriverId);
        Assert.True(detachPacket.TransferField);
        Assert.Equal(123, detachPacket.TransferX);
        Assert.Equal(456, detachPacket.TransferY);
    }

    private static RemoteUserActorPool CreatePoolWithTwoActors()
    {
        var pool = new RemoteUserActorPool();
        Assert.True(pool.TryAddOrUpdate(1, CreateBuild(1, "Passenger"), new Vector2(10f, 20f), out string passengerMessage), passengerMessage);
        Assert.True(pool.TryAddOrUpdate(2, CreateBuild(2, "Driver"), new Vector2(70f, 80f), out string driverMessage), driverMessage);
        return pool;
    }

    private static CharacterBuild CreateBuild(int id, string name)
    {
        return new CharacterBuild
        {
            Id = id,
            Name = name
        };
    }

    private static byte[] BuildPayload(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        write(writer);
        writer.Flush();
        return stream.ToArray();
    }
}
