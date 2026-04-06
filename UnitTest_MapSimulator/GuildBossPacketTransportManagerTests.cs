using System.IO;
using System.Net;
using System.Net.Sockets;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator
{
    public sealed class GuildBossPacketTransportManagerTests
    {
        [Fact]
        public void TryParsePacketLine_DecodesRawGuildBossPacket()
        {
            bool parsed = GuildBossPacketTransportManager.TryParsePacketLine(
                "packetraw 58012A00",
                out int packetType,
                out byte[] payload,
                out string? error);

            Assert.True(parsed, error);
            Assert.Equal(344, packetType);
            Assert.Equal(new byte[] { 0x2A, 0x00 }, payload);
        }

        [Fact]
        public async Task TrySendPulleyRequest_EmitsLegacyAndRawOutboundLines()
        {
            using var manager = new GuildBossPacketTransportManager();
            int port = GetFreeTcpPort();
            manager.Start(port);

            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            Assert.True(SpinWait.SpinUntil(() => manager.ConnectedClientCount == 1, TimeSpan.FromSeconds(2)));

            using var reader = new StreamReader(client.GetStream());

            bool sent = manager.TrySendPulleyRequest(new GuildBossField.PulleyPacketRequest(1234, 7), out string status);

            Assert.True(sent, status);
            Assert.Equal("pulleyhit 7 1234", await ReadLineAsync(reader));
            Assert.Equal("packetoutraw 0301", await ReadLineAsync(reader));
            Assert.Contains("packetoutraw 0301", status);
        }

        private static async Task<string?> ReadLineAsync(StreamReader reader)
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return await reader.ReadLineAsync(cancellation.Token);
        }

        private static int GetFreeTcpPort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
