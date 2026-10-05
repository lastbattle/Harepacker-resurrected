using System.Net;
using HaCreator.MapSimulator.Managers;
using MapleLib.PacketLib;

namespace UnitTest_MapleGame
{
    public sealed class ReactorPoolDirectSessionTests
    {
        [Fact]
        public async Task TouchRequestUsesDirectChannelOwner()
        {
            using MapleTestFakeServer server = MapleTestFakeServer.Start(95);
            using var owner = new MapleOnlineDirectSessionOwner();
            Func<MapleRoleSessionProxy>? proxyFactory = null;
            using var bridge = new ReactorPoolOfficialSessionBridgeManager(proxyFactory, owner);

            await owner.ConnectAsync(MapleServerRole.Channel, IPAddress.Loopback.ToString(), server.Port);
            byte[] hwid = await server.ReceivePacketAsync();
            Assert.Equal(new byte[] { 0x1A, 0x00, 0x00, 0x00 }, hwid);

            Assert.True(bridge.TrySendTouchRequest(123, true, out string status));
            Assert.Contains("Injected reactor touch opcode 250", status, StringComparison.Ordinal);

            byte[] packet = await server.ReceivePacketAsync();
            Assert.Equal(new byte[] { 0xFA, 0x00, 0x7B, 0x00, 0x00, 0x00, 0x01 }, packet);
        }
    }
}
