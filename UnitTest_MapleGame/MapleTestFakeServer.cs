using System.IO;
using System.Net;
using System.Net.Sockets;
using MapleLib.MapleCryptoLib;
using MapleLib.PacketLib;

namespace UnitTest_MapleGame
{
    /// <summary>
    /// Minimal server-side peer that speaks the managed v95 handshake and
    /// framing, mirroring what a real server emits on the wire. Accepts in the
    /// background so callers can read <see cref="Port"/> before dialing.
    /// </summary>
    internal sealed class MapleTestFakeServer : IDisposable
    {
        internal static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

        private readonly TcpListener _listener;
        private readonly short _handshakeVersion;
        private readonly TaskCompletionSource<bool> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Session _session;

        private MapleTestFakeServer(TcpListener listener, short handshakeVersion)
        {
            _listener = listener;
            _handshakeVersion = handshakeVersion;
        }

        public int Port { get; private init; }

        public static MapleTestFakeServer Start(short handshakeVersion)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var server = new MapleTestFakeServer(listener, handshakeVersion) { Port = port };
            _ = Task.Run(server.AcceptAndHandshakeAsync);
            return server;
        }

        private async Task AcceptAndHandshakeAsync()
        {
            TcpClient client = await _listener.AcceptTcpClientAsync().WaitAsync(TestTimeout);
            _session = new Session(client.Client, SessionType.SERVER_TO_CLIENT);

            // First IV arms the client's send cipher, second IV arms the
            // client's receive cipher; the server side mirrors that split.
            byte[] clientSendIv = { 0x12, 0x34, 0x56, 0x78 };
            byte[] clientReceiveIv = { 0x9A, 0xBC, 0xDE, 0xF0 };
            _session.RIV = new MapleCrypto(clientSendIv, _handshakeVersion);
            _session.SIV = new MapleCrypto(clientReceiveIv, _handshakeVersion);
            _session.SendInitialPacket(_handshakeVersion, string.Empty, clientSendIv, clientReceiveIv, 0);
            _ready.TrySetResult(true);
        }

        public async Task SendPacketAsync(byte[] payload)
        {
            await _ready.Task.WaitAsync(TestTimeout);
            await Task.Run(() => _session.SendPacket(payload));
        }

        /// <summary>Reads one encrypted client frame and returns its decrypted body.</summary>
        public async Task<byte[]> ReceivePacketAsync()
        {
            await _ready.Task.WaitAsync(TestTimeout);
            return await Task.Run(async () =>
            {
                NetworkStream stream = new(_session.Socket, ownsSocket: false);
                byte[] header = await ReadExactlyAsync(stream, 4);
                int packetLength = MapleCrypto.GetPacketLength(BitConverter.ToInt32(header, 0));
                byte[] body = await ReadExactlyAsync(stream, packetLength);
                _session.RIV.Crypt(body);
                MapleCustomEncryption.Decrypt(body);
                return body;
            });
        }

        private static async Task<byte[]> ReadExactlyAsync(NetworkStream stream, int length)
        {
            byte[] buffer = new byte[length];
            int read = 0;
            while (read < length)
            {
                int chunk = await stream.ReadAsync(buffer.AsMemory(read, length - read));
                if (chunk <= 0)
                    throw new IOException("The peer closed the socket before the full frame arrived.");
                read += chunk;
            }

            return buffer;
        }

        public void Dispose()
        {
            try
            {
                _listener.Stop();
            }
            catch
            {
            }
        }
    }
}
