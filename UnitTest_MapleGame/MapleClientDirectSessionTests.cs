using System.Net;
using System.Net.Sockets;
using MapleLib.PacketLib;

namespace UnitTest_MapleGame
{
    /// <summary>
    /// Behavior tests for the client-owned direct v95 session (P02). Each test
    /// guards one distinct transport regression against a loopback fake server;
    /// this is a transport contract test, not a parity certificate.
    /// </summary>
    public sealed class MapleClientDirectSessionTests
    {
        [Fact]
        public async Task ConnectAsync_CompletesV95Handshake_AndDeliversEncryptedPacket()
        {
            using MapleTestFakeServer server = MapleTestFakeServer.Start(95);
            using var client = new MapleClientDirectSession(MapleServerRole.Login);

            long? handshakeGeneration = null;
            client.HandshakeCompleted += (_, args) => handshakeGeneration = args.Generation;
            TaskCompletionSource<MapleDirectSessionPacketEventArgs> received = NewTcs<MapleDirectSessionPacketEventArgs>();
            client.PacketReceived += (_, args) => received.TrySetResult(args);

            await client.ConnectAsync(IPAddress.Loopback.ToString(), server.Port);

            Assert.True(client.IsConnected);
            Assert.Equal((short)95, client.SessionVersion);
            long? generation = client.Generation;
            Assert.NotNull(generation);
            Assert.Equal(generation, handshakeGeneration);

            await server.SendPacketAsync(new byte[] { 0x01, 0x00, 0xAA, 0xBB });
            MapleDirectSessionPacketEventArgs packet = await received.Task.WaitAsync(MapleTestFakeServer.TestTimeout);
            Assert.Equal(0x0001, packet.Opcode);
            Assert.Equal(new byte[] { 0x01, 0x00, 0xAA, 0xBB }, packet.RawPacket);
            Assert.Equal(generation, packet.Generation);
        }

        [Fact]
        public async Task ConnectAsync_VersionMismatch_FailsAndRetiresConnection()
        {
            using MapleTestFakeServer server = MapleTestFakeServer.Start(96);
            using var client = new MapleClientDirectSession(MapleServerRole.Login);

            MapleDirectSessionException failure = await Assert.ThrowsAsync<MapleDirectSessionException>(
                () => client.ConnectAsync(IPAddress.Loopback.ToString(), server.Port));

            Assert.Contains("version mismatch", failure.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(client.IsConnected);
            Assert.Null(client.Generation);
        }

        [Fact]
        public async Task ConnectAsync_ReassemblesFragmentedEncryptedPacket()
        {
            using MapleTestFakeServer server = MapleTestFakeServer.Start(95);
            using var client = new MapleClientDirectSession(MapleServerRole.Login);
            TaskCompletionSource<MapleDirectSessionPacketEventArgs> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
            client.PacketReceived += (_, args) => received.TrySetResult(args);

            await client.ConnectAsync(IPAddress.Loopback.ToString(), server.Port);
            byte[] payload = { 0x2B, 0x00, 0x11, 0x22, 0x33 };
            await server.SendPacketFragmentedAsync(payload, 2, TimeSpan.FromMilliseconds(50));
            MapleDirectSessionPacketEventArgs args = await received.Task.WaitAsync(MapleTestFakeServer.TestTimeout);

            Assert.Equal(payload, args.RawPacket);
            Assert.Equal(0x002B, args.Opcode);
        }

        [Fact]
        public async Task ConnectAsync_CancellationWhileAwaitingHandshake_RetiresAndAllowsReconnect()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();
            using var client = new MapleClientDirectSession(MapleServerRole.Login);
            using var cancellation = new CancellationTokenSource();

            Task connectTask = client.ConnectAsync(IPAddress.Loopback.ToString(), port, cancellation.Token);
            TcpClient remoteClient = await acceptTask.WaitAsync(MapleTestFakeServer.TestTimeout);
            using TcpClient acceptedClient = remoteClient;
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connectTask);
            Assert.False(client.IsConnected);
            Assert.Null(client.Generation);

            using MapleTestFakeServer recoveryServer = MapleTestFakeServer.Start(95);
            await client.ConnectAsync(IPAddress.Loopback.ToString(), recoveryServer.Port);
            Assert.True(client.IsConnected);
            Assert.NotNull(client.Generation);
        }

        [Fact]
        public async Task RemoteDisconnect_IsNotReportedAsExpectedClose()
        {
            using MapleTestFakeServer server = MapleTestFakeServer.Start(95);
            using var client = new MapleClientDirectSession(MapleServerRole.Login);
            await client.ConnectAsync(IPAddress.Loopback.ToString(), server.Port);
            TaskCompletionSource<MapleDirectSessionDisconnectedEventArgs> disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
            client.Disconnected += (_, args) => disconnected.TrySetResult(args);

            await server.CloseClientConnectionAsync();
            MapleDirectSessionDisconnectedEventArgs args = await disconnected.Task.WaitAsync(MapleTestFakeServer.TestTimeout);

            Assert.False(args.Expected);
            Assert.Equal("Server closed the connection.", args.Reason);
            Assert.False(client.IsConnected);
            Assert.Null(client.Generation);

            using MapleTestFakeServer recoveryServer = MapleTestFakeServer.Start(95);
            await client.ConnectAsync(IPAddress.Loopback.ToString(), recoveryServer.Port);
            Assert.True(client.IsConnected);
        }

        [Fact]
        public async Task MalformedFrame_DisconnectsActiveSession()
        {
            using MapleTestFakeServer server = MapleTestFakeServer.Start(95);
            using var client = new MapleClientDirectSession(MapleServerRole.Login);
            await client.ConnectAsync(IPAddress.Loopback.ToString(), server.Port);
            TaskCompletionSource<MapleDirectSessionDisconnectedEventArgs> disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
            client.Disconnected += (_, args) => disconnected.TrySetResult(args);

            await server.SendRawAsync(new byte[4]);
            MapleDirectSessionDisconnectedEventArgs args = await disconnected.Task.WaitAsync(MapleTestFakeServer.TestTimeout);

            Assert.False(args.Expected);
            Assert.False(client.IsConnected);
            Assert.Null(client.Generation);
        }

        [Fact]
        public void TrySendPacket_WithoutActiveHandshake_Fails()
        {
            using var client = new MapleClientDirectSession(MapleServerRole.Login);

            bool sent = client.TrySendPacket(new byte[] { 0x01, 0x00 }, out string error);

            Assert.False(sent);
            Assert.Contains("no active", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Close_RaisesExpectedDisconnect_ForRetiredGeneration()
        {
            using MapleTestFakeServer server = MapleTestFakeServer.Start(95);
            using var client = new MapleClientDirectSession(MapleServerRole.Login);
            await client.ConnectAsync(IPAddress.Loopback.ToString(), server.Port);
            long generation = client.Generation ?? 0;

            TaskCompletionSource<MapleDirectSessionDisconnectedEventArgs> disconnected = NewTcs<MapleDirectSessionDisconnectedEventArgs>();
            client.Disconnected += (_, args) => disconnected.TrySetResult(args);

            client.Close("test retirement");

            MapleDirectSessionDisconnectedEventArgs args = await disconnected.Task.WaitAsync(MapleTestFakeServer.TestTimeout);
            Assert.True(args.Expected);
            Assert.Equal(generation, args.Generation);
            Assert.False(client.IsConnected);
            Assert.Null(client.Generation);
        }

        [Fact]
        public async Task Reconnect_AssignsStrictlyNewGeneration()
        {
            using MapleTestFakeServer firstServer = MapleTestFakeServer.Start(95);
            using var client = new MapleClientDirectSession(MapleServerRole.Login);
            await client.ConnectAsync(IPAddress.Loopback.ToString(), firstServer.Port);
            long firstGeneration = client.Generation ?? 0;
            client.Close("reconnect");

            using MapleTestFakeServer secondServer = MapleTestFakeServer.Start(95);
            await client.ConnectAsync(IPAddress.Loopback.ToString(), secondServer.Port);

            long secondGeneration = client.Generation ?? 0;
            Assert.True(secondGeneration > firstGeneration);
        }

        private static TaskCompletionSource<T> NewTcs<T>() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
