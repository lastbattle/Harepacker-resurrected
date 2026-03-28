using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class LoginPacketInboxMessage
    {
        public LoginPacketInboxMessage(LoginPacketType packetType, string source, string rawText, string[] arguments)
        {
            PacketType = packetType;
            Source = string.IsNullOrWhiteSpace(source) ? "login-inbox" : source;
            RawText = rawText ?? string.Empty;
            Arguments = arguments ?? Array.Empty<string>();
        }

        public LoginPacketType PacketType { get; }
        public string Source { get; }
        public string RawText { get; }
        public string[] Arguments { get; }
    }

    /// <summary>
    /// Optional loopback packet inbox for driving the login runtime from an external source.
    /// The listener accepts newline-delimited packet names or numeric ids as either
    /// "<packet> <args>", "<packet>:<args>", or "<packet>=<args>" and queues them for the
    /// simulator to drain on the main thread.
    /// </summary>
    public sealed class LoginPacketInboxManager : IDisposable
    {
        public const int DefaultPort = 18484;

        private readonly ConcurrentQueue<LoginPacketInboxMessage> _pendingMessages = new();
        private readonly object _listenerLock = new();

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;

        public int Port { get; private set; } = DefaultPort;
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public int ReceivedCount { get; private set; }
        public string LastStatus { get; private set; } = "Login packet inbox inactive.";

        public void Start(int port = DefaultPort)
        {
            lock (_listenerLock)
            {
                if (IsRunning)
                {
                    LastStatus = $"Login packet inbox already listening on 127.0.0.1:{Port}.";
                    return;
                }

                StopInternal();

                Port = port <= 0 ? DefaultPort : port;
                _listenerCancellation = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
                _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                LastStatus = $"Login packet inbox listening on 127.0.0.1:{Port}.";
            }
        }

        public void Stop()
        {
            lock (_listenerLock)
            {
                StopInternal();
                LastStatus = "Login packet inbox stopped.";
            }
        }

        public void EnqueueLocal(LoginPacketType packetType, string source)
        {
            string packetSource = string.IsNullOrWhiteSpace(source) ? "login-ui" : source;
            _pendingMessages.Enqueue(new LoginPacketInboxMessage(packetType, packetSource, packetType.ToString(), Array.Empty<string>()));
            ReceivedCount++;
            LastStatus = $"Queued {packetType} from {packetSource}.";
        }

        public bool TryDequeue(out LoginPacketInboxMessage message)
        {
            return _pendingMessages.TryDequeue(out message);
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
                LastStatus = $"Login packet inbox error: {ex.Message}";
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

                        if (!TryParsePacketLine(line, out LoginPacketType packetType, out string[] arguments))
                        {
                            LastStatus = $"Ignored login inbox line from {remoteEndpoint}: {line}";
                            continue;
                        }

                        _pendingMessages.Enqueue(new LoginPacketInboxMessage(packetType, remoteEndpoint, line, arguments));
                        ReceivedCount++;
                        LastStatus = $"Queued {packetType} from {remoteEndpoint}.";
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
                LastStatus = $"Login packet inbox client error: {ex.Message}";
            }
        }

        internal static bool TryParsePacketLine(string text, out LoginPacketType packetType, out string[] arguments)
        {
            packetType = LoginPacketType.CheckPasswordResult;
            arguments = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (trimmed.StartsWith("/loginpacket", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["/loginpacket".Length..].TrimStart();
                if (trimmed.Length == 0)
                {
                    return false;
                }
            }

            string token;
            string argumentText = null;
            int separatorIndex = FindTokenSeparatorIndex(trimmed);
            if (separatorIndex >= 0)
            {
                token = trimmed[..separatorIndex].Trim();
                char separator = trimmed[separatorIndex];
                int argumentStart = separatorIndex + 1;
                if (separator != ':' && separator != '=')
                {
                    while (argumentStart < trimmed.Length && char.IsWhiteSpace(trimmed[argumentStart]))
                    {
                        argumentStart++;
                    }
                }

                argumentText = argumentStart < trimmed.Length
                    ? trimmed[argumentStart..].Trim()
                    : null;
            }
            else
            {
                token = trimmed;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(argumentText))
            {
                arguments = argumentText.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            }

            if (LoginRuntimeManager.TryParsePacketType(token, out packetType))
            {
                return true;
            }

            return int.TryParse(token, out int numericPacket) &&
                   Enum.IsDefined(typeof(LoginPacketType), numericPacket) &&
                   LoginRuntimeManager.TryParsePacketType(((LoginPacketType)numericPacket).ToString(), out packetType);
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

            _listenerCancellation?.Dispose();
            _listenerCancellation = null;
            _listener = null;
            _listenerTask = null;
        }
    }
}
