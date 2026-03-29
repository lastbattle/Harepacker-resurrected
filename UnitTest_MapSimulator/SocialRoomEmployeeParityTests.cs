using HaCreator.MapSimulator.Interaction;
using System.IO;
using System.Text;

namespace UnitTest_MapSimulator;

public sealed class SocialRoomEmployeeParityTests
{
    [Fact]
    public void EmployeeEnterFieldPacket_UpdatesPacketBackedPersonalShopActorStateAndPersistsInSnapshot()
    {
        SocialRoomRuntime runtime = SocialRoomRuntime.CreatePersonalShopSample();
        byte[] payload = BuildEmployeeEnterFieldPacket(
            employerId: 9012,
            templateId: 5600001,
            worldX: 123,
            worldY: 456,
            footholdId: 77,
            ownerName: "PacketOwner",
            miniRoomType: 5,
            miniRoomSerial: 700001,
            balloonTitle: "Packet Shop",
            balloonByte0: 1,
            balloonByte1: 2,
            balloonByte2: 3);

        bool applied = runtime.TryApplyEmployeeEnterFieldPacket(payload, out string message);

        Assert.True(applied, message);
        Assert.Contains("template=5600001", message);

        SocialRoomFieldActorSnapshot snapshot = runtime.GetFieldActorSnapshot(DateTime.UtcNow);
        Assert.NotNull(snapshot);
        Assert.Equal(SocialRoomFieldActorTemplate.CashEmployee, snapshot.Template);
        Assert.Equal(5600001, snapshot.TemplateId);
        Assert.True(snapshot.HasWorldPosition);
        Assert.False(snapshot.UseOwnerAnchor);
        Assert.Equal(123, snapshot.WorldX);
        Assert.Equal(456, snapshot.WorldY);
        Assert.Equal("Packet Shop", snapshot.Headline);
        Assert.Contains("PacketOwner", snapshot.Detail);

        string status = runtime.DescribeStatus();
        Assert.Contains("pkt(owner=PacketOwner", status);
        Assert.Contains("balloon=Packet Shop", status);

        SocialRoomRuntime restored = SocialRoomRuntime.CreatePersonalShopSample();
        restored.RestoreSnapshot(runtime.BuildSnapshot());

        SocialRoomFieldActorSnapshot restoredSnapshot = restored.GetFieldActorSnapshot(DateTime.UtcNow);
        Assert.NotNull(restoredSnapshot);
        Assert.Equal(snapshot.Template, restoredSnapshot.Template);
        Assert.Equal(snapshot.TemplateId, restoredSnapshot.TemplateId);
        Assert.Equal(snapshot.WorldX, restoredSnapshot.WorldX);
        Assert.Equal(snapshot.WorldY, restoredSnapshot.WorldY);
        Assert.Equal(snapshot.Headline, restoredSnapshot.Headline);
        Assert.Equal(snapshot.Detail, restoredSnapshot.Detail);
    }

    [Fact]
    public void EntrustedShop_ExpiredPermitStillFallsBackToFredrickAfterPacketBackedEmployeeSpawn()
    {
        SocialRoomRuntime runtime = SocialRoomRuntime.CreateEntrustedShopSample();
        byte[] payload = BuildEmployeeEnterFieldPacket(
            employerId: 6001,
            templateId: 5030000,
            worldX: 300,
            worldY: 220,
            footholdId: 12,
            ownerName: "EntrustedOwner",
            miniRoomType: 4,
            miniRoomSerial: 111,
            balloonTitle: "Entrusted Packet Shop",
            balloonByte0: 0,
            balloonByte1: 1,
            balloonByte2: 0);

        Assert.True(runtime.TryApplyEmployeeEnterFieldPacket(payload, out string packetMessage), packetMessage);
        Assert.True(runtime.ExpireEntrustedPermit(out string expireMessage), expireMessage);

        SocialRoomFieldActorSnapshot snapshot = runtime.GetFieldActorSnapshot(DateTime.UtcNow.AddMinutes(1));

        Assert.NotNull(snapshot);
        Assert.Equal(SocialRoomFieldActorTemplate.StoreBanker, snapshot.Template);
        Assert.Equal("Contract expired", snapshot.Headline);
        Assert.Equal("Claim items and mesos at Fredrick.", snapshot.Detail);
        Assert.Contains("balloon=Entrusted Packet Shop", runtime.DescribeStatus());
    }

    [Fact]
    public void EmployeeEnterFieldPacket_RejectsTruncatedPayload()
    {
        SocialRoomRuntime runtime = SocialRoomRuntime.CreatePersonalShopSample();

        bool applied = runtime.TryApplyEmployeeEnterFieldPacket([0x34, 0x12, 0x00], out string message);

        Assert.False(applied);
        Assert.Contains("ended unexpectedly", message);
    }

    private static byte[] BuildEmployeeEnterFieldPacket(
        int employerId,
        int templateId,
        short worldX,
        short worldY,
        short footholdId,
        string ownerName,
        byte miniRoomType,
        int miniRoomSerial,
        string balloonTitle,
        byte balloonByte0,
        byte balloonByte1,
        byte balloonByte2)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.ASCII, leaveOpen: true);

        writer.Write(employerId);
        writer.Write(templateId);
        writer.Write(worldX);
        writer.Write(worldY);
        writer.Write(footholdId);
        WriteMapleString(writer, ownerName);
        writer.Write(miniRoomType);

        if (miniRoomType != 0)
        {
            writer.Write(miniRoomSerial);
            WriteMapleString(writer, balloonTitle);
            writer.Write(balloonByte0);
            writer.Write(balloonByte1);
            writer.Write(balloonByte2);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteMapleString(BinaryWriter writer, string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text ?? string.Empty);
        writer.Write((short)bytes.Length);
        writer.Write(bytes);
    }
}
