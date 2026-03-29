using HaCreator.MapSimulator;
using Xunit;

namespace UnitTest_MapSimulator;

public class MapSimulatorChatTests
{
    [Fact]
    public void AddSystemMessage_UsesClientSystemType()
    {
        var chat = new MapSimulatorChat();

        chat.AddSystemMessage("Packet-owned local overlay applied.", 100);

        ChatMessage message = Assert.Single(chat.GetRenderState().Messages);
        Assert.Equal(12, message.ChatLogType);
        Assert.Equal(new Microsoft.Xna.Framework.Color(255, 228, 151), message.Color);
    }

    [Fact]
    public void AddNoticeMessage_UsesClientNoticeType()
    {
        var chat = new MapSimulatorChat();

        chat.AddNoticeMessage("Megassenger mirrored into the strip.", 100);

        ChatMessage message = Assert.Single(chat.GetRenderState().Messages);
        Assert.Equal(13, message.ChatLogType);
        Assert.Equal(new Microsoft.Xna.Framework.Color(151, 221, 255), message.Color);
    }

    [Fact]
    public void AddErrorMessage_UsesClientErrorType()
    {
        var chat = new MapSimulatorChat();

        chat.AddErrorMessage("Packet-owned utility dispatch failed.", 100);

        ChatMessage message = Assert.Single(chat.GetRenderState().Messages);
        Assert.Equal(15, message.ChatLogType);
        Assert.Equal(new Microsoft.Xna.Framework.Color(247, 75, 75), message.Color);
    }
}
