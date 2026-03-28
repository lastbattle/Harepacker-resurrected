using HaCreator.MapSimulator.Managers;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public sealed class WeddingPacketInboxManagerTests
{
    [Fact]
    public void TryParseLine_ParsesCoupleAvatarLine()
    {
        bool parsed = WeddingPacketInboxManager.TryParseLine(
            "actor avatar groom 100 200 0A0B stand1 left",
            out WeddingInboxMessage message,
            out string error);

        Assert.True(parsed, error);
        Assert.NotNull(message);
        Assert.Equal(WeddingInboxMessageKind.CoupleAvatar, message.Kind);
        Assert.Equal("groom", message.ActorKey);
        Assert.Equal(new Vector2(100f, 200f), message.Position);
        Assert.False(message.FacingRight);
        Assert.Equal("stand1", message.ActionName);
        Assert.Equal(new byte[] { 0x0A, 0x0B }, message.Payload);
    }

    [Fact]
    public void TryParseLine_ParsesGuestMoveLine()
    {
        bool parsed = WeddingPacketInboxManager.TryParseLine(
            "guest move Alice 15.5 32.25 walk1 right",
            out WeddingInboxMessage message,
            out string error);

        Assert.True(parsed, error);
        Assert.NotNull(message);
        Assert.Equal(WeddingInboxMessageKind.GuestMove, message.Kind);
        Assert.Equal("Alice", message.ActorKey);
        Assert.Equal(new Vector2(15.5f, 32.25f), message.Position);
        Assert.True(message.FacingRight);
        Assert.Equal("walk1", message.ActionName);
    }

    [Fact]
    public void TryParseLine_RejectsUnsupportedCoupleActor()
    {
        bool parsed = WeddingPacketInboxManager.TryParseLine(
            "actor officiant 10 20",
            out WeddingInboxMessage _,
            out string error);

        Assert.False(parsed);
        Assert.Equal("Wedding actor must be groom or bride.", error);
    }
}
