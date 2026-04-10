using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class AdminShopPacketInboxMessage
    {
        public AdminShopPacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            PacketType = packetType;
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "admin-shop-packet-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    /// <summary>
    /// Loopback inbox for CField::OnPacket admin-shop packets before they reach
    /// the live CAdminShopDlg owner seam.
    /// </summary>
    public sealed class AdminShopPacketInboxManager : IDisposable
    {
        public const int DefaultPort = 18497;
        public const int ResultClientPacketType = LocalUtilityPacketInboxManager.AdminShopResultClientPacketType;
        public const int OpenClientPacketType = LocalUtilityPacketInboxManager.AdminShopOpenClientPacketType;

        private readonly ConcurrentQueue<AdminShopPacketInboxMessage> _pendingMessages = new();
        private readonly object _listenerLock = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;

        public int Port { get; private set; } = DefaultPort;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Admin-shop packet inbox inactive.";

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Admin-shop packet inbox already listening on 127.0.0.1:{Port}.";
                    return;
                }

                StopInternal();

                Port = port <= 0 ? DefaultPort : port;
                _listenerCancellation = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
                _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                LastStatus = $"Admin-shop packet inbox listening on 127.0.0.1:{Port}.";
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal();
                LastStatus = "Admin-shop packet inbox stopped.";
            }
        }

        public bool TryDequeue(out AdminShopPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void EnqueueLocal(int packetType, byte[] payload, string source)
        {
            string packetSource = string.IsNullOrWhiteSpace(source) ? "admin-shop-ui" : source;
            _pendingMessages.Enqueue(new AdminShopPacketInboxMessage(packetType, payload, packetSource, packetType.ToString(CultureInfo.InvariantCulture)));
            ReceivedCount++;
            LastStatus = $"Queued {DescribePacketType(packetType)} from {packetSource}.";
        }

        public void RecordDispatchResult(AdminShopPacketInboxMessage message, bool success, string detail)
        {
            string summary = DescribePacketType(message?.PacketType ?? 0);
            LastStatus = success
                ? $"Applied {summary} from {message?.Source ?? "admin-shop-packet-inbox"}."
                : $"Ignored {summary} from {message?.Source ?? "admin-shop-packet-inbox"}: {detail}";
        }

        public void Dispose()
        {
            lock (_listenerLock)
            {
                StopInternal();
            }
        }

        public static bool TryParseLine(string text, out AdminShopPacketInboxMessage message, out string error)
        {
            message = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Admin-shop inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith("/adminshoppacket", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["/adminshoppacket".Length..].TrimStart();
            }
            else if (trimmed.StartsWith("/adminshop", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["/adminshop".Length..].TrimStart();
            }

            if (trimmed.Length == 0)
            {
                error = "Admin-shop inbox line is empty.";
                return false;
            }

            if (trimmed.StartsWith("packetraw", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["packetraw".Length..].TrimStart();
            }
            else if (trimmed.StartsWith("packet", StringComparison.OrdinalIgnoreCase)
                && !trimmed.StartsWith("packetclientraw", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["packet".Length..].TrimStart();
            }

            if (trimmed.StartsWith("packetclientraw", StringComparison.OrdinalIgnoreCase))
            {
                string rawHex = trimmed["packetclientraw".Length..].Trim();
                if (!TryParsePayload(rawHex, out byte[] rawPacket, out error))
                {
                    return false;
                }

                if (!TryDecodeOpcodeFramedPacket(rawPacket, out int clientPacketType, out byte[] clientPayload, out error))
                {
                    return false;
                }

                if (!IsSupportedPacketType(clientPacketType))
                {
                    error = $"Opcode-framed packet {clientPacketType} is not a CAdminShopDlg::OnPacket packet.";
                    return false;
                }

                message = new AdminShopPacketInboxMessage(clientPacketType, clientPayload, "admin-shop-packet-inbox", text);
                return true;
            }

            int splitIndex = FindTokenSeparatorIndex(trimmed);
            string packetToken = splitIndex >= 0 ? trimmed[..splitIndex].Trim() : trimmed;
            string payloadToken = splitIndex >= 0 ? trimmed[(splitIndex + 1)..].Trim() : string.Empty;

            if (!TryParsePacketType(packetToken, out int packetType))
            {
                error = $"Unsupported admin-shop packet '{packetToken}'.";
                return false;
            }

            byte[] payload = Array.Empty<byte>();
            if (!string.IsNullOrWhiteSpace(payloadToken) && !TryParsePayload(payloadToken, out payload, out error))
            {
                return false;
            }

            message = new AdminShopPacketInboxMessage(packetType, payload, "admin-shop-packet-inbox", text);
            return true;
        }

        public static bool TryParsePacketType(string token, out int packetType)
        {
            packetType = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string normalized = token.Trim();
            if (normalized.Equals("adminshopresult", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("adminshopreply", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("adminshoponpacket366", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("cadminshopdlg366", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("result", StringComparison.OrdinalIgnoreCase))
            {
                packetType = ResultClientPacketType;
                return true;
            }

            if (normalized.Equals("adminshopopen", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("adminshop", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("adminshopset", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("adminshoponpacket367", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("cadminshopdlg367", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("open", StringComparison.OrdinalIgnoreCase))
            {
                packetType = OpenClientPacketType;
                return true;
            }

            if ((normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(normalized[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out packetType))
                || int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out packetType))
            {
                return IsSupportedPacketType(packetType);
            }

            return false;
        }

        public static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                ResultClientPacketType => "CAdminShopDlg::OnPacket Result(366)",
                OpenClientPacketType => "CAdminShopDlg::OnPacket Open(367)",
                _ => $"0x{packetType:X}"
            };
        }

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _listener != null)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                    _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                LastStatus = $"Admin-shop packet inbox error: {ex.Message}";
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            string remoteEndpoint = client.Client?.RemoteEndPoint?.ToString() ?? "loopback-client";
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        string line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                        if (line == null)
                        {
                            break;
                        }

                        if (!TryParseLine(line, out AdminShopPacketInboxMessage message, out string error))
                        {
                            LastStatus = $"Ignored admin-shop inbox line from {remoteEndpoint}: {error}";
                            continue;
                        }

                        _pendingMessages.Enqueue(new AdminShopPacketInboxMessage(message.PacketType, message.Payload, remoteEndpoint, line));
                        ReceivedCount++;
                        LastStatus = $"Queued {DescribePacketType(message.PacketType)} from {remoteEndpoint}.";
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                LastStatus = $"Admin-shop packet inbox client error: {ex.Message}";
            }
        }

        private static bool TryParsePayload(string text, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Packet payload is empty.";
                return false;
            }

            const string payloadHexPrefix = "payloadhex=";
            if (text.StartsWith(payloadHexPrefix, StringComparison.OrdinalIgnoreCase))
            {
                text = text[payloadHexPrefix.Length..].Trim();
            }
            else if (text.StartsWith("payloadb64=", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    payload = Convert.FromBase64String(text["payloadb64=".Length..].Trim());
                    return true;
                }
                catch (FormatException)
                {
                    error = "payloadb64= must contain a valid base64 payload.";
                    return false;
                }
            }

            string normalized = text.Replace(" ", string.Empty, StringComparison.Ordinal);
            if ((normalized.Length & 1) != 0)
            {
                error = "Hex payload must contain an even number of digits.";
                return false;
            }

            try
            {
                payload = Convert.FromHexString(normalized);
                return true;
            }
            catch (FormatException)
            {
                error = "Packet payload must use payloadhex=.., payloadb64=.., or a compact raw hex byte string.";
                return false;
            }
        }

        private static bool IsSupportedPacketType(int packetType)
        {
            return packetType == ResultClientPacketType || packetType == OpenClientPacketType;
        }

        public static bool TryDecodeOpcodeFramedPacket(byte[] rawPacket, out int packetType, out byte[] payload, out string error)
        {
            packetType = 0;
            payload = Array.Empty<byte>();
            error = null;

            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                error = "Admin-shop client packet must include a 2-byte opcode.";
                return false;
            }

            packetType = BitConverter.ToUInt16(rawPacket, 0);
            if (!IsSupportedPacketType(packetType))
            {
                error = $"Unsupported admin-shop client opcode {packetType}.";
                return false;
            }

            payload = rawPacket.Length == sizeof(ushort)
                ? Array.Empty<byte>()
                : rawPacket[sizeof(ushort)..];
            return true;
        }

        private static int FindTokenSeparatorIndex(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c) || c == ':' || c == '=')
                {
                    return i;
                }
            }

            return -1;
        }

        private void StopInternal()
        {
            _listenerCancellation?.Cancel();

            try
            {
                _listener?.Stop();
            }
            catch
            {
            }

            try
            {
                _listenerTask?.Wait(200);
            }
            catch
            {
            }

            _listener = null;
            _listenerTask = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;
        }
    }
}
