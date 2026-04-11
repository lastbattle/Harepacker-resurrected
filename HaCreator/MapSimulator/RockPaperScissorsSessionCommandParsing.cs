using System;

namespace HaCreator.MapSimulator
{
    internal static class RockPaperScissorsSessionCommandParsing
    {
        internal const string SessionUsage = "Usage: /rps session [status|discover <remotePort> [processName|pid] [localPort]|attach <remotePort> [processName|pid] [localPort]|attachproxy <listenPort|0> <remotePort> [processName|pid] [localPort]|start <listenPort|0> <serverHost> <serverPort>|startauto <listenPort|0> <remotePort> [processName|pid] [localPort]|stop]";
        internal const string DiscoverUsage = "Usage: /rps session discover <remotePort> [processName|pid] [localPort]";
        internal const string AttachUsage = "Usage: /rps session attach <remotePort> [processName|pid] [localPort]";
        internal const string AttachProxyUsage = "Usage: /rps session attachproxy <listenPort|0> <remotePort> [processName|pid] [localPort]";
        internal const string StartUsage = "Usage: /rps session start <listenPort|0> <serverHost> <serverPort>";
        internal const string StartAutoUsage = "Usage: /rps session startauto <listenPort|0> <remotePort> [processName|pid] [localPort]";

        internal static bool TryParseProxyListenPort(string value, out int listenPort)
        {
            return int.TryParse(value, out listenPort) && listenPort >= 0 && listenPort <= ushort.MaxValue;
        }
    }
}
