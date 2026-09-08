using System;

namespace HaCreator.MapSimulator
{
    internal static class TransportationSessionCommandParsing
    {
        internal const string SessionUsage = "Usage: /transport session [status|discover <remotePort> [processName|pid] [localPort]|attach <remotePort> [processName|pid] [localPort]|attachproxy <listenPort|0> <remotePort> [processName|pid] [localPort]|history [count]|clearhistory|replay <historyIndex>|queue <historyIndex>|sendraw <hex>|queueraw <hex>|sendinit [fieldId] [shipKind]|queueinit [fieldId] [shipKind]|start <listenPort|0> <serverHost> <serverPort>|startauto <listenPort|0> <remotePort> [processName|pid] [localPort]|stop]";
        internal const string DiscoverUsage = "Usage: /transport session discover <remotePort> [processName|pid] [localPort]";
        internal const string AttachUsage = "Usage: /transport session attach <remotePort> [processName|pid] [localPort]";
        internal const string AttachProxyUsage = "Usage: /transport session attachproxy <listenPort|0> <remotePort> [processName|pid] [localPort]";
        internal const string StartUsage = "Usage: /transport session start <listenPort|0> <serverHost> <serverPort>";
        internal const string StartAutoUsage = "Usage: /transport session startauto <listenPort|0> <remotePort> [processName|pid] [localPort]";

        internal static bool TryParseProxyListenPort(string value, out int listenPort)
        {
            return int.TryParse(value, out listenPort) && listenPort >= 0 && listenPort <= ushort.MaxValue;
        }

        internal static bool TryParseRemotePort(string value, out int remotePort)
        {
            return int.TryParse(value, out remotePort) && remotePort > 0 && remotePort <= ushort.MaxValue;
        }

        internal static bool TryParseLocalPortFilter(string value, out int localPort)
        {
            return int.TryParse(value, out localPort) && localPort > 0 && localPort <= ushort.MaxValue;
        }

        internal static bool TryParseFieldIdOverride(string value, out int fieldId)
        {
            return int.TryParse(value, out fieldId) && fieldId > 0;
        }

        internal static bool TryParseShipKindOverride(string value, out int shipKind)
        {
            shipKind = -1;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim();
            if (string.Equals(normalized, "transit", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "regular", StringComparison.OrdinalIgnoreCase))
            {
                shipKind = 0;
                return true;
            }

            if (string.Equals(normalized, "balrog", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "voyagebalrog", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "voyage-balrog", StringComparison.OrdinalIgnoreCase))
            {
                shipKind = 1;
                return true;
            }

            return int.TryParse(normalized, out shipKind);
        }
    }
}
