using HaCreator.MapSimulator.Interaction;
using System.IO;
using System.Linq;
using System.Text;

namespace UnitTest_MapSimulator;

public sealed class PacketScriptMessageFlipSpeakerParityTests
{
    [Fact]
    public void AskYesNo_ParamWithoutRightSpeakerFlag_LeavesPageOnDefaultSide()
    {
        byte[] payload = BuildAskYesNoPacket(param: 0x00, prompt: "Test prompt", appendTrailingByte: false);

        var runtime = new PacketScriptMessageRuntime();
        bool decoded = runtime.TryDecode(
            payload,
            static _ => null,
            activeNpc: null,
            static (_, _) => null,
            static _ => { },
            out PacketScriptMessageRuntime.PacketScriptMessageOpenRequest request,
            out _);

        Assert.True(decoded);
        Assert.NotNull(request);
        Assert.NotNull(request.State);
        Assert.Single(request.State.Entries);
        Assert.Single(request.State.Entries[0].Pages);
        Assert.False(request.State.Entries[0].Pages[0].FlipSpeaker);
    }

    [Fact]
    public void AskYesNo_ParamWithRightSpeakerFlag_PublishesFlipSpeaker()
    {
        byte[] payload = BuildAskYesNoPacket(param: 0x06, prompt: "Test prompt", appendTrailingByte: false);

        var runtime = new PacketScriptMessageRuntime();
        bool decoded = runtime.TryDecode(
            payload,
            static _ => null,
            activeNpc: null,
            static (_, _) => null,
            static _ => { },
            out PacketScriptMessageRuntime.PacketScriptMessageOpenRequest request,
            out _);

        Assert.True(decoded);
        Assert.NotNull(request);
        Assert.NotNull(request.State);
        Assert.Single(request.State.Entries);
        Assert.Single(request.State.Entries[0].Pages);
        Assert.True(request.State.Entries[0].Pages[0].FlipSpeaker);
    }

    [Fact]
    public void AskYesNo_TrailingByteNotice_PreservesFlipSpeakerState()
    {
        byte[] payload = BuildAskYesNoPacket(param: 0x06, prompt: "Trailing payload", appendTrailingByte: true);

        var runtime = new PacketScriptMessageRuntime();
        bool decoded = runtime.TryDecode(
            payload,
            static _ => null,
            activeNpc: null,
            static (_, _) => null,
            static _ => { },
            out PacketScriptMessageRuntime.PacketScriptMessageOpenRequest request,
            out _);

        Assert.True(decoded);
        Assert.NotNull(request);
        Assert.NotNull(request.State);
        Assert.Single(request.State.Entries);

        var page = request.State.Entries[0].Pages.Single();
        Assert.True(page.FlipSpeaker);
        Assert.Contains("Trailing bytes left unread", page.Text);
    }

    private static byte[] BuildAskYesNoPacket(byte param, string prompt, bool appendTrailingByte)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
        writer.Write((byte)4); // speakerType
        writer.Write(9200000); // speakerTemplate
        writer.Write((byte)2); // msgType AskYesNo
        writer.Write(param);   // bParam
        WriteMapleString(writer, prompt);
        if (appendTrailingByte)
        {
            writer.Write((byte)0xAA);
        }

        writer.Flush();
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
