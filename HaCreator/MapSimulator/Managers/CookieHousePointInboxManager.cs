using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    public enum CookieHousePointInboxPayloadKind
    {
        TextPoint,
        RawContextPoint
    }

    public sealed class CookieHousePointInboxMessage
    {
        public CookieHousePointInboxMessage(int point, string source, string rawText, CookieHousePointInboxPayloadKind payloadKind)
        {
            Point = Math.Max(0, point);
            Source = string.IsNullOrWhiteSpace(source) ? "cookiehouse-inbox" : source;
            RawText = rawText ?? string.Empty;
            PayloadKind = payloadKind;
        }

        public int Point { get; }
        public string Source { get; }
        public string RawText { get; }
        public CookieHousePointInboxPayloadKind PayloadKind { get; }
    }

    /// <summary>
    /// Optional loopback inbox for externally authored Cookie House point updates.
    /// Each line is encoded as either "<point>", "point <point>", or
    /// "raw <hex>" where <hex> is the client-shaped little-endian
    /// CWvsContext Cookie House point dword recovered from v95.
    /// </summary>
    public sealed class CookieHousePointInboxManager : IDisposable
    {
        public const int DefaultPort = 18486;
        public const int ClientContextPointByteLength = 4;
        public const int ClientContextPointOffset = 0x4148;

        private readonly ConcurrentQueue<CookieHousePointInboxMessage> _pendingMessages = new();
        private readonly object _listenerLock = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;

        public int Port { get; private set; } = DefaultPort;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Cookie House point inbox inactive.";

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Cookie House point inbox already listening on 127.0.0.1:{Port}.";
                    return;
                }

                StopInternal(clearPending: true);

                try
                {
                    Port = port <= 0 ? DefaultPort : port;
                    _listenerCancellation = new CancellationTokenSource();
                    _listener = new TcpListener(IPAddress.Loopback, Port);
                    _listener.Start();
                    _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                    LastStatus = $"Cookie House point inbox listening on 127.0.0.1:{Port}.";
                }
                catch (Exception ex)
                {
                    StopInternal(clearPending: true);
                    LastStatus = $"Cookie House point inbox failed to start: {ex.Message}";
                }
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal(clearPending: true);
                LastStatus = "Cookie House point inbox stopped.";
            }
        }

        public bool TryDequeue(out CookieHousePointInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
        }

        public void RecordDispatchResult(string source, bool success, string message)
        {
            string summary = string.IsNullOrWhiteSpace(message) ? "point update" : message;
            LastStatus = success
                ? $"Applied {summary} from {source}."
                : $"Ignored {summary} from {source}.";
        }

        public void Dispose()
        {
            lock (_listenerLock)
            {
                StopInternal(clearPending: true);
            }
        }

        public static bool TryParsePointLine(
            string text,
            out int point,
            out CookieHousePointInboxPayloadKind payloadKind,
            out string error)
        {
            point = 0;
            payloadKind = CookieHousePointInboxPayloadKind.TextPoint;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Cookie House inbox line is empty.";
                return false;
            }

            string trimmed = text.Trim();
            string[] parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && string.Equals(parts[0], "raw", StringComparison.OrdinalIgnoreCase))
            {
                return TryParseRawContextPoint(parts[1], out point, out payloadKind, out error);
            }

            string valueToken = parts.Length switch
            {
                1 => parts[0],
                >= 2 when string.Equals(parts[0], "point", StringComparison.OrdinalIgnoreCase) => parts[1],
                _ => null
            };

            if (string.IsNullOrWhiteSpace(valueToken) || !int.TryParse(valueToken, out point))
            {
                error = $"Invalid Cookie House point payload: {text}";
                return false;
            }

            point = Math.Max(0, point);
            return true;
        }

        private static bool TryParseRawContextPoint(
            string hexPayload,
            out int point,
            out CookieHousePointInboxPayloadKind payloadKind,
            out string error)
        {
            point = 0;
            payloadKind = CookieHousePointInboxPayloadKind.RawContextPoint;
            error = null;

            if (string.IsNullOrWhiteSpace(hexPayload))
            {
                error = "Cookie House raw payload is empty.";
                return false;
            }

            string normalized = hexPayload.Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase);
            normalized = normalized.Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase);
            normalized = normalized.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);
            if (normalized.Length != ClientContextPointByteLength * 2)
            {
                error = $"Cookie House raw payload must be exactly {ClientContextPointByteLength} bytes for CWvsContext+0x{ClientContextPointOffset:X}.";
                return false;
            }

            byte[] bytes = new byte[ClientContextPointByteLength];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (!byte.TryParse(normalized.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
                {
                    error = $"Invalid Cookie House raw payload: {hexPayload}";
                    return false;
                }
            }

            point = Math.Max(0, BitConverter.ToInt32(bytes, 0));
            return true;
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
                LastStatus = $"Cookie House point inbox error: {ex.Message}";
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

                        if (!TryParsePointLine(line, out int point, out CookieHousePointInboxPayloadKind payloadKind, out string error))
                        {
                            LastStatus = $"Ignored Cookie House inbox line from {remoteEndpoint}: {error}";
                            continue;
                        }

                        _pendingMessages.Enqueue(new CookieHousePointInboxMessage(point, remoteEndpoint, line, payloadKind));
                        ReceivedCount++;
                        string payloadLabel = payloadKind == CookieHousePointInboxPayloadKind.RawContextPoint
                            ? "raw context point"
                            : "point";
                        LastStatus = $"Queued Cookie House {payloadLabel} {point} from {remoteEndpoint}.";
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
                LastStatus = $"Cookie House point inbox client error: {ex.Message}";
            }
        }

        private void StopInternal(bool clearPending)
        {
            _listenerCancellation?.Cancel();

            try
            {
                _listener?.Stop();
            }
            catch (SocketException)
            {
            }

            _listener = null;
            _listenerTask = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;

            if (clearPending)
            {
                while (_pendingMessages.TryDequeue(out _))
                {
                }

                ReceivedCount = 0;
            }
        }
    }
}
