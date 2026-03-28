using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator
{
    public class LoginPacketInboxManagerTests
    {
        [Theory]
        [InlineData("WorldInformation payloadhex=0102", LoginPacketType.WorldInformation, new[] { "payloadhex=0102" })]
        [InlineData("WorldInformation:payloadhex=0102", LoginPacketType.WorldInformation, new[] { "payloadhex=0102" })]
        [InlineData("/loginpacket WorldInformation payloadhex=0102 end", LoginPacketType.WorldInformation, new[] { "payloadhex=0102", "end" })]
        [InlineData("/loginpacket WorldInformation:payloadhex=0102", LoginPacketType.WorldInformation, new[] { "payloadhex=0102" })]
        [InlineData("10=payloadhex=0102", LoginPacketType.WorldInformation, new[] { "payloadhex=0102" })]
        [InlineData("RecommendWorldMessage 0300 0000 0200 4869", LoginPacketType.RecommendWorldMessage, new[] { "0300", "0000", "0200", "4869" })]
        public void TryParsePacketLine_AcceptsSupportedTransportFormats(string line, LoginPacketType expectedPacketType, string[] expectedArguments)
        {
            bool parsed = LoginPacketInboxManager.TryParsePacketLine(line, out LoginPacketType packetType, out string[] arguments);

            Assert.True(parsed);
            Assert.Equal(expectedPacketType, packetType);
            Assert.Equal(expectedArguments, arguments);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("/loginpacket")]
        [InlineData(":payloadhex=0102")]
        public void TryParsePacketLine_RejectsInvalidTransportLines(string line)
        {
            bool parsed = LoginPacketInboxManager.TryParsePacketLine(line, out _, out _);

            Assert.False(parsed);
        }

        [Theory]
        [InlineData(LoginPacketType.WorldInformation, new[] { "01", "00", "02", "00" }, new[] { "payloadhex=01000200" })]
        [InlineData(LoginPacketType.SelectWorldResult, new[] { "00", "01", "02", "03" }, new[] { "payloadhex=00010203" })]
        [InlineData(LoginPacketType.RecommendWorldMessage, new[] { "01", "00", "00", "00" }, new[] { "payloadhex=01000000" })]
        [InlineData(LoginPacketType.CheckUserLimitResult, new[] { "1", "2" }, new[] { "1", "2" })]
        public void Normalize_CollapsesOnlySupportedSelectorRawHexPayloads(LoginPacketType packetType, string[] arguments, string[] expectedArguments)
        {
            string[] normalized = LoginPacketPayloadArgumentNormalizer.Normalize(packetType, arguments);

            Assert.Equal(expectedArguments, normalized);
        }
    }
}
