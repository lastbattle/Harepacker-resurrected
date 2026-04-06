using HaCreator.MapSimulator.Interaction;
using System.IO;
using System.Text;

namespace UnitTest_MapSimulator
{
    public sealed class PacketScriptMessageRuntimeTests
    {
        [Fact]
        public void TryDecode_QuizClientOpenPacket_DecodesClientOwnedFields()
        {
            PacketScriptMessageRuntime runtime = new();
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(BuildScriptMessageHeader(6, 9010000));
            writer.Write((byte)0);
            WriteMapleString(writer, "Math Quiz");
            WriteMapleString(writer, "What is 2 + 2?");
            WriteMapleString(writer, "Add the numbers together.");
            writer.Write(4);
            writer.Write(2);
            writer.Write(30);

            bool decoded = runtime.TryDecode(
                stream.ToArray(),
                _ => null,
                null,
                _ => true,
                out PacketScriptMessageRuntime.PacketScriptMessageOpenRequest request,
                out string message);

            Assert.True(decoded);
            Assert.NotNull(request);
            Assert.NotNull(request.State);
            Assert.Equal("NPC #9010000", request.State.NpcName);
            Assert.Contains("Opened packet-authored script dialog", message);

            NpcInteractionEntry entry = Assert.Single(request.State.Entries);
            Assert.Equal("Quiz", entry.Title);
            Assert.Contains("What is 2 + 2?", entry.Pages[0].Text);
            Assert.Contains("Title: \"Math Quiz\"", entry.Pages[0].Text);
            Assert.Contains("Hint text: \"Add the numbers together.\"", entry.Pages[0].Text);
            Assert.Contains("answer=4, questionNo=2, remaining=30 sec.", entry.Pages[0].Text);
            Assert.Equal(new[] { "OK", "Next", "Give Up" }, entry.Pages[0].Choices.Select(static choice => choice.Label).ToArray());
        }

        [Fact]
        public void TryDecode_QuizClientClosePacket_WithTrailingBytesStillClosesDialog()
        {
            PacketScriptMessageRuntime runtime = new();
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(BuildScriptMessageHeader(6, 9010000));
            writer.Write((byte)1);
            writer.Write(1234);

            bool decoded = runtime.TryDecode(
                stream.ToArray(),
                _ => null,
                null,
                _ => true,
                out PacketScriptMessageRuntime.PacketScriptMessageOpenRequest request,
                out string message);

            Assert.True(decoded);
            Assert.NotNull(request);
            Assert.True(request.CloseExistingDialog);
            Assert.Contains("Closed packet-authored quiz owner", message);
        }

        [Theory]
        [InlineData(6, 2, "quiz")]
        [InlineData(7, 3, "speed-quiz")]
        public void TryDecode_ClientQuizUpdateModes_DoNotMutateDialog(int messageType, byte mode, string expectedLabel)
        {
            PacketScriptMessageRuntime runtime = new();
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(BuildScriptMessageHeader(messageType, 9010000));
            writer.Write(mode);

            bool decoded = runtime.TryDecode(
                stream.ToArray(),
                _ => null,
                null,
                _ => true,
                out PacketScriptMessageRuntime.PacketScriptMessageOpenRequest request,
                out string message);

            Assert.True(decoded);
            Assert.Null(request);
            Assert.Contains($"Ignored packet-authored {expectedLabel} payload", message);
        }

        [Fact]
        public void TryDecode_SpeedQuizClientOpenPacket_DecodesClientOwnedCounters()
        {
            PacketScriptMessageRuntime runtime = new();
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(BuildScriptMessageHeader(7, 9010000));
            writer.Write((byte)0);
            writer.Write(3);
            writer.Write(5);
            writer.Write(2);
            writer.Write(2);
            writer.Write(15);

            bool decoded = runtime.TryDecode(
                stream.ToArray(),
                _ => null,
                null,
                _ => true,
                out PacketScriptMessageRuntime.PacketScriptMessageOpenRequest request,
                out _);

            Assert.True(decoded);
            Assert.NotNull(request);
            NpcInteractionEntry entry = Assert.Single(request.State.Entries);
            Assert.Equal("Speed Quiz", entry.Title);
            Assert.Contains("Question 3 / 5", entry.Pages[0].Text);
            Assert.Contains("Correct answers: 2", entry.Pages[0].Text);
            Assert.Contains("Questions remaining: 2", entry.Pages[0].Text);
            Assert.Contains("Time remaining: 15 sec.", entry.Pages[0].Text);
        }

        private static byte[] BuildScriptMessageHeader(int messageType, int npcId)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)4);
            writer.Write(npcId);
            writer.Write((byte)messageType);
            writer.Write((byte)0);
            return stream.ToArray();
        }

        private static void WriteMapleString(BinaryWriter writer, string value)
        {
            string text = value ?? string.Empty;
            byte[] bytes = Encoding.Default.GetBytes(text);
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }
    }
}
