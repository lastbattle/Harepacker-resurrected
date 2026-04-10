using System;

namespace HaCreator.MapSimulator
{
    internal static class CoconutSessionCommandParsing
    {
        internal const string SessionUsage = "Usage: /coconut session [status|discover <remotePort> [processName|pid] [localPort]|attach <remotePort> [processName|pid] [localPort]|start <listenPort|0> <serverHost> <serverPort>|startauto <listenPort|0> <remotePort> [processName|pid] [localPort]|stop]";
        internal const string StartUsage = "Usage: /coconut session start <listenPort|0> <serverHost> <serverPort>";
        internal const string StartAutoUsage = "Usage: /coconut session startauto <listenPort|0> <remotePort> [processName|pid] [localPort]";

        internal static bool TryParseProxyListenPort(string value, out int listenPort)
        {
            return int.TryParse(value, out listenPort) && listenPort >= 0;
        }
    }
}
