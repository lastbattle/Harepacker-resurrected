using System.Reflection;
using HaCreator.MapSimulator;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator
{
    public class MapSimulatorChatTests
    {
        [Fact]
        public void WhisperCommand_WithoutMessage_ArmsWhisperTargetForSubsequentMessages()
        {
            var chat = new MapSimulatorChat();

            bool handled = InvokeTryHandleWhisperCommand(chat, "/w Athena", 100);
            InvokeSendTargetedChatMessage(chat, "hello there", 120);

            Assert.True(handled);

            MapSimulatorChatRenderState renderState = chat.GetRenderState();
            Assert.Equal("Athena", renderState.WhisperTarget);

            ChatMessage sentMessage = Assert.Single(renderState.Messages);
            Assert.Equal("> Athena: hello there", sentMessage.Text);
            Assert.Equal(new Color(255, 170, 255), sentMessage.Color);
        }

        [Fact]
        public void WhisperReply_UsesCurrentWhisperTarget()
        {
            var chat = new MapSimulatorChat();

            Assert.True(InvokeTryHandleWhisperCommand(chat, "/w Athena first", 100));
            Assert.True(InvokeTryHandleWhisperCommand(chat, "/r second", 120));

            MapSimulatorChatRenderState renderState = chat.GetRenderState();
            Assert.Equal("Athena", renderState.WhisperTarget);
            Assert.Collection(
                renderState.Messages,
                message => Assert.Equal("> Athena: first", message.Text),
                message => Assert.Equal("> Athena: second", message.Text));
        }

        private static bool InvokeTryHandleWhisperCommand(MapSimulatorChat chat, string message, int tickCount)
        {
            MethodInfo? method = typeof(MapSimulatorChat).GetMethod(
                "TryHandleWhisperCommand",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);
            return (bool)method!.Invoke(chat, new object[] { message, tickCount })!;
        }

        private static void InvokeSendTargetedChatMessage(MapSimulatorChat chat, string message, int tickCount)
        {
            MethodInfo? method = typeof(MapSimulatorChat).GetMethod(
                "SendTargetedChatMessage",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);
            method!.Invoke(chat, new object[] { message, tickCount });
        }
    }
}
