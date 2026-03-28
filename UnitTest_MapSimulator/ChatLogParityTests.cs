using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Interaction;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace UnitTest_MapSimulator;

public sealed class ChatLogParityTests
{
    [Fact]
    public void OutgoingWhisperRowsUseOutgoingWhisperLogType()
    {
        MapSimulatorChat chat = new MapSimulatorChat();

        chat.AddMessage("> Alice: Meet me in Henesys.", new XnaColor(255, 170, 255), 1000);

        ChatMessage message = Assert.Single(chat.GetRenderState().Messages);
        Assert.Equal(14, message.ChatLogType);
        Assert.Equal(new XnaColor(255, 170, 255), message.Color);
    }

    [Fact]
    public void IncomingWhisperRowsUseIncomingWhisperLogType()
    {
        MapSimulatorChat chat = new MapSimulatorChat();

        chat.AddMessage("[Whisper] Alice: Meet me in Henesys.", XnaColor.LightGreen, 1000);

        ChatMessage message = Assert.Single(chat.GetRenderState().Messages);
        Assert.Equal(16, message.ChatLogType);
        Assert.Equal(new XnaColor(255, 170, 255), message.Color);
    }

    [Fact]
    public void DirectedWhisperRowsUseOutgoingWhisperLogType()
    {
        MapSimulatorChat chat = new MapSimulatorChat();

        chat.AddMessage("[Whisper] Player -> Alice: Meet me in Henesys.", XnaColor.White, 1000);

        ChatMessage message = Assert.Single(chat.GetRenderState().Messages);
        Assert.Equal(14, message.ChatLogType);
        Assert.Equal(new XnaColor(255, 170, 255), message.Color);
    }

    [Fact]
    public void MessengerRemoteWhisperReturnsChatLogWhisperRow()
    {
        MessengerRuntime runtime = new MessengerRuntime();

        runtime.InviteContact("Alice");

        string chatLine = runtime.ReceiveRemoteWhisper("Alice", "Meet me in Henesys.");

        Assert.Equal("[Whisper] Alice: Meet me in Henesys.", chatLine);
    }

    [Theory]
    [InlineData("[System] Delivery completed.", 12, 255, 228, 151)]
    [InlineData("[Notice] Event map opens in five minutes.", 13, 151, 221, 255)]
    [InlineData("[Error] Unable to join channel.", 15, 247, 75, 75)]
    [InlineData("[Association] Player: Alliance regroup in six minutes.", 5, 124, 236, 255)]
    public void ClientStyledPrefixesInferExpectedChatLogTypeAndColor(string text, int expectedType, byte red, byte green, byte blue)
    {
        MapSimulatorChat chat = new MapSimulatorChat();

        chat.AddMessage(text, XnaColor.LightGreen, 1000);

        ChatMessage message = Assert.Single(chat.GetRenderState().Messages);
        Assert.Equal(expectedType, message.ChatLogType);
        Assert.Equal(new XnaColor(red, green, blue), message.Color);
    }
}
