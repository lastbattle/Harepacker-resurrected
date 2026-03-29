using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class CashServicePacketInboxMessage
    {
        public CashServicePacketInboxMessage(int packetType, byte[] payload, string source, string rawText)
        {
            PacketType = packetType;
            Payload = payload ?? Array.Empty<byte>();
            Source = string.IsNullOrWhiteSpace(source) ? "cash-service-inbox" : source;
            RawText = rawText ?? string.Empty;
        }

        public int PacketType { get; }
        public byte[] Payload { get; }
        public string Source { get; }
        public string RawText { get; }
    }

    public sealed class CashServicePacketInboxManager : IDisposable
    {
        public const int DefaultPort = 18486;

        private readonly ConcurrentQueue<CashServicePacketInboxMessage> _pendingMessages = new();
        private readonly object _listenerLock = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;

        public int Port { get; private set; } = DefaultPort;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Cash-service packet inbox inactive.";

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Cash-service packet inbox already listening on 127.0.0.1:{Port}.";
                    return;
                }

                StopInternal();
                Port = port <= 0 ? DefaultPort : port;
                _listenerCancellation = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
                _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                LastStatus = $"Cash-service packet inbox listening on 127.0.0.1:{Port}.";
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal();
                LastStatus = "Cash-service packet inbox stopped.";
            }
        }

        public void EnqueueLocal(int packetType, byte[] payload, string source)
        {
            string packetSource = string.IsNullOrWhiteSpace(source) ? "cash-service-ui" : source;
            _pendingMessages.Enqueue(new CashServicePacketInboxMessage(packetType, payload, packetSource, packetType.ToString()));
            ReceivedCount++;
            LastStatus = $"Queued {DescribePacketType(packetType)} from {packetSource}.";
        }

        public bool TryDequeue(out CashServicePacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(CashServicePacketInboxMessage message, bool success, string detail)
        {
            string summary = DescribePacketType(message?.PacketType ?? 0);
            LastStatus = success
                ? $"Applied {summary} from {message?.Source ?? "cash-service-inbox"}."
                : $"Ignored {summary} from {message?.Source ?? "cash-service-inbox"}: {detail}";
        }

        public void Dispose()
        {
            lock (_listenerLock)
            {
                StopInternal();
            }
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
                LastStatus = $"Cash-service packet inbox error: {ex.Message}";
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            string remoteEndpoint = client.Client?.RemoteEndPoint?.ToString() ?? "loopback-client";
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new(stream))
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        string line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                        if (line == null)
                        {
                            break;
                        }

                        if (!TryParseLine(line, out CashServicePacketInboxMessage message, out string error))
                        {
                            LastStatus = $"Ignored cash-service inbox line from {remoteEndpoint}: {error}";
                            continue;
                        }

                        _pendingMessages.Enqueue(new CashServicePacketInboxMessage(message.PacketType, message.Payload, remoteEndpoint, line));
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
                LastStatus = $"Cash-service packet inbox client error: {ex.Message}";
            }
        }

        public static bool TryParseLine(string text, out CashServicePacketInboxMessage message, out string error)
        {
            message = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Cash-service inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith("/cashservicepacket", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["/cashservicepacket".Length..].TrimStart();
            }

            if (trimmed.Length == 0)
            {
                error = "Cash-service inbox line is empty.";
                return false;
            }

            int splitIndex = FindTokenSeparatorIndex(trimmed);
            string packetToken = splitIndex >= 0 ? trimmed[..splitIndex].Trim() : trimmed;
            string payloadToken = splitIndex >= 0 ? trimmed[(splitIndex + 1)..].Trim() : string.Empty;

            if (!TryParsePacketType(packetToken, out int packetType))
            {
                error = $"Unsupported cash-service packet '{packetToken}'.";
                return false;
            }

            byte[] payload = Array.Empty<byte>();
            if (!string.IsNullOrWhiteSpace(payloadToken)
                && !TryParsePayload(payloadToken, out payload, out error))
            {
                return false;
            }

            message = new CashServicePacketInboxMessage(packetType, payload, "cash-service-inbox", text);
            return true;
        }

        public static bool TryParsePacketType(string token, out int packetType)
        {
            packetType = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (int.TryParse(token, out packetType))
            {
                return CashServiceStageRuntime.IsCashShopPacket(packetType)
                    || CashServiceStageRuntime.IsItcPacket(packetType);
            }

            string normalized = token.Trim().ToLowerInvariant();
            packetType = normalized switch
            {
                "chargeparam" or "cscharge" => 382,
                "querycash" or "csquery" => 383,
                "cashitem" or "cashitemresult" => 384,
                "purchaseexp" => 385,
                "giftmate" => 386,
                "duplicateid" => 387,
                "namechange" => 388,
                "transferworld" => 390,
                "gachaponstamp" => 391,
                "gachaponresult" => 392,
                "gachaponbonus" => 393,
                "oneaday" => 395,
                "freeitemnotice" => 396,
                "itccharge" => 410,
                "itcquery" => 411,
                "normalitem" or "itcnormalitem" => 412,
                _ => 0
            };
            return packetType != 0;
        }

        private static bool TryParsePayload(string text, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = null;

            const string payloadHexPrefix = "payloadhex=";
            const string payloadBase64Prefix = "payloadb64=";
            if (text.StartsWith(payloadHexPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string hex = text[payloadHexPrefix.Length..].Trim();
                if (hex.Length == 0 || (hex.Length % 2) != 0)
                {
                    error = "payloadhex= must contain an even-length hexadecimal byte string.";
                    return false;
                }

                try
                {
                    payload = Convert.FromHexString(hex);
                    return true;
                }
                catch (FormatException)
                {
                    error = "payloadhex= must contain only hexadecimal characters.";
                    return false;
                }
            }

            if (text.StartsWith(payloadBase64Prefix, StringComparison.OrdinalIgnoreCase))
            {
                string base64 = text[payloadBase64Prefix.Length..].Trim();
                if (base64.Length == 0)
                {
                    error = "payloadb64= must not be empty.";
                    return false;
                }

                try
                {
                    payload = Convert.FromBase64String(base64);
                    return true;
                }
                catch (FormatException)
                {
                    error = "payloadb64= must contain a valid base64 payload.";
                    return false;
                }
            }

            try
            {
                payload = Convert.FromHexString(text.Replace(" ", string.Empty, StringComparison.Ordinal));
                return true;
            }
            catch (FormatException)
            {
                error = "Packet payload must use payloadhex=.., payloadb64=.., or a compact raw hex byte string.";
                return false;
            }
        }

        private static int FindTokenSeparatorIndex(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]) || text[i] == ':' || text[i] == '=')
                {
                    return i;
                }
            }

            return -1;
        }

        private static string DescribePacketType(int packetType)
        {
            return packetType switch
            {
                382 => "ChargeParam(382)",
                383 => "QueryCash(383)",
                384 => "CashItem(384)",
                385 => "PurchaseExp(385)",
                386 => "GiftMate(386)",
                387 => "DuplicateID(387)",
                388 => "NameChange(388)",
                390 => "TransferWorld(390)",
                391 => "GachaponStamp(391)",
                392 => "GachaponResult(392)",
                393 => "GachaponResult(393)",
                395 => "OneADay(395)",
                396 => "FreeItemNotice(396)",
                410 => "ITCChargeParam(410)",
                411 => "ITCQueryCash(411)",
                412 => "ITCNormalItem(412)",
                _ => $"packet {packetType}"
            };
        }

        private void StopInternal()
        {
            try
            {
                _listenerCancellation?.Cancel();
            }
            catch
            {
            }

            try
            {
                _listener?.Stop();
            }
            catch
            {
            }

            try
            {
                _listenerTask?.Wait(100);
            }
            catch
            {
            }

            _listenerTask = null;
            _listener = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;
        }
    }
}
