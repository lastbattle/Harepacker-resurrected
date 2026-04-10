using System;

namespace HaCreator.MapSimulator
{
    internal static class GuildBossSessionCommandParsing
    {
        internal const string SessionUsage = "Usage: /guildboss session [status|discover <remotePort> [processName|pid] [localPort]|attach <remotePort> [processName|pid] [localPort]|attachproxy <listenPort|0> <remotePort> [processName|pid] [localPort]|start <listenPort|0> <serverHost> <serverPort>|startauto <listenPort|0> <remotePort> [processName|pid] [localPort]|stop]";
        internal const string DiscoverUsage = "Usage: /guildboss session discover <remotePort> [processName|pid] [localPort]";
        internal const string AttachUsage = "Usage: /guildboss session attach <remotePort> [processName|pid] [localPort]";
        internal const string AttachProxyUsage = "Usage: /guildboss session attachproxy <listenPort|0> <remotePort> [processName|pid] [localPort]";
        internal const string StartUsage = "Usage: /guildboss session start <listenPort|0> <serverHost> <serverPort>";
        internal const string StartAutoUsage = "Usage: /guildboss session startauto <listenPort|0> <remotePort> [processName|pid] [localPort]";

        internal static bool TryParseProxyListenPort(string value, out int listenPort)
        {
            return int.TryParse(value, out listenPort) && listenPort >= 0;
        }
    }
}
