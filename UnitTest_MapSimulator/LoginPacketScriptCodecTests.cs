using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator;

public sealed class LoginPacketScriptCodecTests
{
    [Fact]
    public void TryParsePacketLine_DecodesRawOpcodeFramedHexPacket()
    {
        bool parsed = LoginPacketInboxManager.TryParsePacketLine(
            "0D 00 4D 61 70 6C 65 53 69 6D 00 00",
            out LoginPacketType packetType,
            out string[] arguments);

        Assert.True(parsed);
        Assert.Equal(LoginPacketType.CheckDuplicatedIdResult, packetType);
        Assert.Single(arguments);
        Assert.Equal("payloadhex=4D61706C6553696D0000", arguments[0]);
    }

    [Fact]
    public void TryParsePacketLine_DecodesExplicitOpcodeAndPayload()
    {
        bool parsed = LoginPacketInboxManager.TryParsePacketLine(
            "opcode=0x0E payloadhex=01020304",
            out LoginPacketType packetType,
            out string[] arguments);

        Assert.True(parsed);
        Assert.Equal(LoginPacketType.CreateNewCharacterResult, packetType);
        Assert.Single(arguments);
        Assert.Equal("payloadhex=01020304", arguments[0]);
    }

    [Fact]
    public void TryDecodeArguments_FallsBackToOpcodeFramedBinaryPayload()
    {
        byte[] packetBytes =
        {
            0x0B, 0x00,
            0x01, 0x02, 0x03, 0x04
        };

        bool decoded = LoginPacketScriptCodec.TryDecodeArguments(
            new[] { $"payloadb64={Convert.ToBase64String(packetBytes)}" },
            "login-ui",
            out IReadOnlyList<LoginPacketInboxMessage> messages,
            out string error);

        Assert.True(decoded, error);
        LoginPacketInboxMessage message = Assert.Single(messages);
        Assert.Equal(LoginPacketType.SelectWorldResult, message.PacketType);
        Assert.Single(message.Arguments);
        Assert.Equal("payloadhex=01020304", message.Arguments[0]);
    }
}
