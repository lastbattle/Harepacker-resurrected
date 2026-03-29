using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Effects;
using Microsoft.Xna.Framework;
using System.IO;

namespace UnitTest_MapSimulator;

public sealed class WeddingRemoteUserParityTests
{
    [Fact]
    public void WeddingMovePacket_PreservesMovementSnapshotAndInterpolatesAudiencePosition()
    {
        var field = new WeddingField();
        field.Enable(680000210);

        var guest = new CharacterBuild
        {
            Id = 2001,
            Name = "GuestOne"
        };
        field.UpsertAudienceParticipant(guest, new Vector2(10f, 20f), facingRight: true, actionName: "stand1", characterId: 2001);

        byte[] payload = BuildRemoteMovePacket(
            characterId: 2001,
            startX: 10,
            startY: 20,
            firstX: 40,
            firstY: 20,
            secondX: 70,
            secondY: 20,
            moveAction: 2,
            elapsedPerElement: 100);

        bool applied = field.TryApplyPacket(packetType: 210, payload, currentTimeMs: 1000, out string errorMessage);

        Assert.True(applied, errorMessage);
        Assert.True(field.TryGetAudienceParticipantById(2001, out WeddingRemoteParticipantSnapshot initial));
        Assert.NotNull(initial.MovementSnapshot);
        Assert.Equal(2, initial.MovementSnapshot.MovePath.Count);
        Assert.Equal(new Vector2(40f, 20f), initial.Position);

        field.Update(currentTimeMs: 1050, deltaSeconds: 0.05f);

        Assert.True(field.TryGetAudienceParticipantById(2001, out WeddingRemoteParticipantSnapshot sampled));
        Assert.Equal(55f, sampled.Position.X);
        Assert.Equal(20f, sampled.Position.Y);
        Assert.Equal("walk1", sampled.ActionName);

        WeddingRemoteParticipantSnapshot listed = Assert.Single(field.GetRemoteParticipantSnapshots());
        Assert.NotNull(listed.MovementSnapshot);
        Assert.Equal(2, listed.MovementSnapshot.MovePath.Count);
    }

    private static byte[] BuildRemoteMovePacket(
        int characterId,
        short startX,
        short startY,
        short firstX,
        short firstY,
        short secondX,
        short secondY,
        byte moveAction,
        short elapsedPerElement)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(characterId);
        writer.Write(startX);
        writer.Write(startY);
        writer.Write((short)0);
        writer.Write((short)0);
        writer.Write((byte)2);

        WriteAbsoluteElement(writer, firstX, firstY, moveAction, elapsedPerElement);
        WriteAbsoluteElement(writer, secondX, secondY, moveAction, elapsedPerElement);

        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteAbsoluteElement(BinaryWriter writer, short x, short y, byte moveAction, short elapsed)
    {
        writer.Write((byte)0);
        writer.Write(x);
        writer.Write(y);
        writer.Write((short)0);
        writer.Write((short)0);
        writer.Write((short)1);
        writer.Write((short)0);
        writer.Write((short)0);
        writer.Write(moveAction);
        writer.Write(elapsed);
    }
}
