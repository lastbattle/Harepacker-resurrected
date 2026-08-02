namespace HaCreator.MapSimulator.Managers
{
    internal sealed class RetiredMapleSocketState
    {
        private readonly string _label;

        public RetiredMapleSocketState(string label, int defaultPort, string initialStatus)
        {
            _label = label;
            DefaultPort = defaultPort;
            Port = defaultPort;
            LastStatus = initialStatus;
        }

        public int DefaultPort { get; }
        public int Port { get; private set; }
        public bool IsRunning => false;
        public bool HasConnectedClients => false;
        public int ConnectedClientCount => 0;
        public string LastStatus { get; private set; }

        public void Start(int port)
        {
            Port = port <= 0 ? DefaultPort : port;
            LastStatus = $"{_label} uses role-session/local ingress; loopback listener is retired.";
        }

        public void Stop(string readyStatus)
        {
            LastStatus = readyStatus;
        }

        public void SetStatus(string status)
        {
            LastStatus = status;
        }

        public string Describe(int receivedCount = 0, int sentCount = 0, int pendingCount = 0, int queuedCount = 0, string detail = null)
        {
            string suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" {detail}";
            return $"{_label} inactive; no connected clients; received={receivedCount}; sent={sentCount}; pending={pendingCount}; queued={queuedCount}.{suffix} {LastStatus}";
        }
    }
}
