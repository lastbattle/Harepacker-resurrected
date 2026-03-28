using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Managers;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator
{
    public sealed class CoconutTransportTests
    {
        [Fact]
        public void TryParsePacketLine_DecodesOpcodeWrappedHitPacket()
        {
            bool parsed = CoconutPacketInboxManager.TryParsePacketLine(
                "packetraw 56010200780001",
                out int packetType,
                out byte[] payload,
                out string error);

            Assert.True(parsed, error);
            Assert.Equal(CoconutField.PacketTypeHit, packetType);
            Assert.Equal(Convert.FromHexString("0200780001"), payload);
        }

        [Fact]
        public void TryParsePacketLine_IgnoresOpcodeWrappedOutboundAttackPacket()
        {
            bool parsed = CoconutPacketInboxManager.TryParsePacketLine(
                "packetraw 010105007800",
                out int packetType,
                out byte[] payload,
                out bool ignored,
                out string message);

            Assert.False(parsed);
            Assert.True(ignored);
            Assert.Equal(0, packetType);
            Assert.Empty(payload);
            Assert.Contains("257", message);
        }

        [Fact]
        public async Task TrySendAttackRequest_EmitsLegacyAndRawPacketLines()
        {
            using var manager = new CoconutPacketInboxManager();
            int port = GetAvailablePort();
            manager.Start(port);

            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            Assert.True(SpinWait.SpinUntil(() => manager.HasConnectedClients, TimeSpan.FromSeconds(3)));

            using var reader = new StreamReader(client.GetStream());
            var request = new CoconutField.AttackPacketRequest(TargetId: 5, DelayMs: 120, RequestedAtTick: 3210);

            bool sent = manager.TrySendAttackRequest(request, out string status);

            Assert.True(sent, status);
            Assert.Equal("attack 5 120 3210", await ReadLineWithTimeoutAsync(reader));
            Assert.Equal("packetoutraw 010105007800", await ReadLineWithTimeoutAsync(reader));
        }

        [Fact]
        public async Task InboundTransport_IgnoresOpcodeWrappedOutboundAttackEcho()
        {
            using var manager = new CoconutPacketInboxManager();
            int port = GetAvailablePort();
            manager.Start(port);

            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            Assert.True(SpinWait.SpinUntil(() => manager.HasConnectedClients, TimeSpan.FromSeconds(3)));

            using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
            await writer.WriteLineAsync("packetraw 010105007800");

            Assert.True(SpinWait.SpinUntil(() => manager.LastStatus.Contains("Ignored outbound Coconut raw attack packet opcode: 257.", StringComparison.Ordinal), TimeSpan.FromSeconds(3)));
            Assert.Equal(0, manager.ReceivedCount);
            Assert.False(manager.TryDequeue(out _));
        }

        [Fact]
        public void TryHandleNormalAttack_WhenTransportOwnsHit_WaitsForPacketBeforeChangingState()
        {
            var field = new CoconutField();
            field.Initialize(1, new Rectangle(100, 100, 40, 40), 220);
            field.StartGame();

            CoconutField.Coconut coconut = field.Coconuts[0];
            Rectangle attackBounds = new Rectangle((int)coconut.Position.X - 16, (int)coconut.Position.Y - 16, 32, 32);
            int attackTick = 1000;

            bool handled = field.TryHandleNormalAttack(attackBounds, attackTick, skillId: 0, allowLocalPreview: false);

            Assert.True(handled);
            Assert.Equal(CoconutField.CoconutState.OnTree, field.Coconuts[0].State);
            Assert.Equal(1, field.PendingAttackPacketRequestCount);
            Assert.Equal(1, field.PendingUndispatchedAttackPacketRequestCount);

            field.Update(attackTick + 121);

            Assert.Equal(CoconutField.CoconutState.OnTree, field.Coconuts[0].State);

            field.OnCoconutHit(0, 120, (int)CoconutField.CoconutState.Falling, attackTick);
            field.Update(attackTick + 120);

            Assert.Equal(CoconutField.CoconutState.Falling, field.Coconuts[0].State);
            Assert.Equal(0, field.PendingAttackPacketRequestCount);
        }

        private static async Task<string> ReadLineWithTimeoutAsync(StreamReader reader)
        {
            Task<string?> readTask = reader.ReadLineAsync();
            Task completedTask = await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(3)));
            Assert.Same(readTask, completedTask);
            string? line = await readTask;
            Assert.NotNull(line);
            return line;
        }

        private static int GetAvailablePort()
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
