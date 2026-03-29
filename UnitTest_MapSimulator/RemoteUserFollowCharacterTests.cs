using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;
using System.Reflection;

namespace UnitTest_MapSimulator;

public class RemoteUserFollowCharacterTests
{
    [Fact]
    public void TryParseFollowCharacter_CompactAttachPayload_DecodesCharacterAndDriver()
    {
        byte[] payload = BuildCompactAttachPayload(characterId: 1234, driverId: 5678);

        bool parsed = RemoteUserPacketCodec.TryParseFollowCharacter(payload, out RemoteUserFollowCharacterPacket packet, out string error);

        Assert.True(parsed, error);
        Assert.Equal(1234, packet.CharacterId);
        Assert.Equal(5678, packet.DriverId);
        Assert.False(packet.TransferField);
        Assert.Null(packet.TransferX);
        Assert.Null(packet.TransferY);
    }

    [Fact]
    public void TryParseFollowCharacter_OfficialDetachTransferPayload_UsesOverrideCharacterId()
    {
        byte[] payload = BuildOfficialDetachTransferPayload(transferX: 91, transferY: 207);

        bool parsed = RemoteUserPacketCodec.TryParseFollowCharacter(payload, out RemoteUserFollowCharacterPacket packet, out string error, characterIdOverride: 4321);

        Assert.True(parsed, error);
        Assert.Equal(4321, packet.CharacterId);
        Assert.Equal(0, packet.DriverId);
        Assert.True(packet.TransferField);
        Assert.Equal(91, packet.TransferX);
        Assert.Equal(207, packet.TransferY);
    }

    [Fact]
    public void TryApplyFollowCharacter_AttachAndDetachWithoutTransfer_SnapsBackToDriverPosition()
    {
        var pool = new RemoteUserActorPool();

        AddRemote(pool, 100, new Vector2(25f, 40f), "Driver");
        AddRemote(pool, 200, new Vector2(150f, 220f), "Passenger");

        bool attached = pool.TryApplyFollowCharacter(200, 100, transferField: false, transferPosition: null, localCharacterId: 0, localCharacterPosition: Vector2.Zero, out string attachMessage);
        Assert.True(attached, attachMessage);

        bool detached = pool.TryApplyFollowCharacter(200, 0, transferField: false, transferPosition: null, localCharacterId: 0, localCharacterPosition: Vector2.Zero, out string detachMessage);
        Assert.True(detached, detachMessage);

        Assert.True(pool.TryGetActor(100, out RemoteUserActor driver));
        Assert.True(pool.TryGetActor(200, out RemoteUserActor passenger));
        Assert.Equal(0, driver.FollowPassengerId);
        Assert.Equal(0, passenger.FollowDriverId);
        Assert.Equal(driver.Position, passenger.Position);
    }

    [Fact]
    public void TryApplyFollowCharacter_DetachWithTransfer_UsesExplicitTransferPosition()
    {
        var pool = new RemoteUserActorPool();

        AddRemote(pool, 100, new Vector2(25f, 40f), "Driver");
        AddRemote(pool, 200, new Vector2(150f, 220f), "Passenger");

        bool attached = pool.TryApplyFollowCharacter(200, 100, transferField: false, transferPosition: null, localCharacterId: 0, localCharacterPosition: Vector2.Zero, out string attachMessage);
        Assert.True(attached, attachMessage);

        Vector2 transferPosition = new(501f, 777f);
        bool detached = pool.TryApplyFollowCharacter(200, 0, transferField: true, transferPosition, localCharacterId: 0, localCharacterPosition: Vector2.Zero, out string detachMessage);
        Assert.True(detached, detachMessage);

        Assert.True(pool.TryGetActor(200, out RemoteUserActor passenger));
        Assert.Equal(transferPosition, passenger.Position);
    }

    [Fact]
    public void TryParsePortableChair_WithPairCharacterId_DecodesPair()
    {
        byte[] payload = new byte[sizeof(int) * 3];
        WriteInt32(payload, 0, 1200);
        WriteInt32(payload, sizeof(int), 3010000);
        WriteInt32(payload, sizeof(int) * 2, 3400);

        bool parsed = RemoteUserPacketCodec.TryParsePortableChair(payload, out RemoteUserPortableChairPacket packet, out string error);

        Assert.True(parsed, error);
        Assert.Equal(1200, packet.CharacterId);
        Assert.Equal(3010000, packet.ChairItemId);
        Assert.Equal(3400, packet.PairCharacterId);
    }

    [Fact]
    public void FindPortableChairPairActor_PrefersExplicitPacketPairOverCloserHeuristicMatch()
    {
        var pool = new RemoteUserActorPool();
        AddRemote(pool, 100, new Vector2(0f, 0f), "Owner");
        AddRemote(pool, 200, new Vector2(40f, 0f), "ConfiguredPair");
        AddRemote(pool, 300, new Vector2(40f, 0f), "HeuristicOnly");

        PortableChair chair = CreateCoupleChair(distanceX: 40, distanceY: 0, maxDiff: 0, direction: 0);
        AssignPortableChair(pool, 100, chair, pairCharacterId: 200);
        AssignPortableChair(pool, 200, chair, pairCharacterId: 100);
        AssignPortableChair(pool, 300, chair, pairCharacterId: null);

        RemoteUserActor partner = InvokeFindPortableChairPairActor(pool, chair, ownerCharacterId: 100, ownerFacingRight: true, ownerX: 0f, ownerY: 0f, skipCharacterId: 100, preferVisibleOnly: true);

        Assert.NotNull(partner);
        Assert.Equal(200, partner.CharacterId);
    }

    private static void AddRemote(RemoteUserActorPool pool, int characterId, Vector2 position, string name)
    {
        CharacterBuild build = new()
        {
            Id = characterId,
            Name = name
        };

        bool added = pool.TryAddOrUpdate(characterId, build, position, out string message);
        Assert.True(added, message);
    }

    private static void AssignPortableChair(RemoteUserActorPool pool, int characterId, PortableChair chair, int? pairCharacterId)
    {
        Assert.True(pool.TryGetActor(characterId, out RemoteUserActor actor));
        actor.Build.ActivePortableChair = chair;
        actor.PreferredPortableChairPairCharacterId = pairCharacterId;
    }

    private static PortableChair CreateCoupleChair(int distanceX, int distanceY, int maxDiff, int direction)
    {
        return new PortableChair
        {
            IsCoupleChair = true,
            CoupleDistanceX = distanceX,
            CoupleDistanceY = distanceY,
            CoupleMaxDiff = maxDiff,
            CoupleDirection = direction
        };
    }

    private static RemoteUserActor InvokeFindPortableChairPairActor(
        RemoteUserActorPool pool,
        PortableChair chair,
        int ownerCharacterId,
        bool ownerFacingRight,
        float ownerX,
        float ownerY,
        int skipCharacterId,
        bool preferVisibleOnly)
    {
        MethodInfo method = typeof(RemoteUserActorPool).GetMethod("FindPortableChairPairActor", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<RemoteUserActor>(method.Invoke(pool, new object[]
        {
            chair,
            ownerCharacterId,
            ownerFacingRight,
            ownerX,
            ownerY,
            skipCharacterId,
            preferVisibleOnly
        }));
    }

    private static byte[] BuildCompactAttachPayload(int characterId, int driverId)
    {
        byte[] payload = new byte[sizeof(int) * 2];
        WriteInt32(payload, 0, characterId);
        WriteInt32(payload, sizeof(int), driverId);
        return payload;
    }

    private static byte[] BuildOfficialDetachTransferPayload(int transferX, int transferY)
    {
        byte[] payload = new byte[sizeof(int) + sizeof(byte) + sizeof(int) * 2];
        WriteInt32(payload, 0, 0);
        payload[sizeof(int)] = 1;
        WriteInt32(payload, sizeof(int) + sizeof(byte), transferX);
        WriteInt32(payload, sizeof(int) + sizeof(byte) + sizeof(int), transferY);
        return payload;
    }

    private static void WriteInt32(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }
}
