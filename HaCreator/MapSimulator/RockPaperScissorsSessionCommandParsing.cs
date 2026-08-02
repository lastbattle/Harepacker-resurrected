using System;

namespace HaCreator.MapSimulator
{
    internal static class RockPaperScissorsSessionCommandParsing
    {
        internal const string SessionUsage = "Usage: /rps session [status|discover <remotePort> [processName|pid] [localPort]|attach <remotePort> [processName|pid] [localPort]|attachproxy <listenPort|0> <remotePort> [processName|pid] [localPort]|verify [count]|clearverify|recent [count]|clearrecent|recentin [count]|clearrecentin|start <listenPort|0> <serverHost> <serverPort>|startauto <listenPort|0> <remotePort> [processName|pid] [localPort]|stop]";
        internal const string DiscoverUsage = "Usage: /rps session discover <remotePort> [processName|pid] [localPort]";
        internal const string AttachUsage = "Usage: /rps session attach <remotePort> [processName|pid] [localPort]";
        internal const string AttachProxyUsage = "Usage: /rps session attachproxy <listenPort|0> <remotePort> [processName|pid] [localPort]";
        internal const string VerifyUsage = "Usage: /rps session verify [count]";
        internal const string ClearVerifyUsage = "Usage: /rps session clearverify";
        internal const string RecentUsage = "Usage: /rps session recent [count]";
        internal const string ClearRecentUsage = "Usage: /rps session clearrecent";
        internal const string RecentInboundUsage = "Usage: /rps session recentin [count]";
        internal const string ClearRecentInboundUsage = "Usage: /rps session clearrecentin";
        internal const string StartUsage = "Usage: /rps session start <listenPort|0> <serverHost> <serverPort>";
        internal const string StartAutoUsage = "Usage: /rps session startauto <listenPort|0> <remotePort> [processName|pid] [localPort]";

        internal static bool HasExactArgCount(string[] args, int count)
        {
            return args != null && args.Length == count;
        }

        internal static bool HasArgCountInRange(string[] args, int minCount, int maxCount)
        {
            return args != null && args.Length >= minCount && args.Length <= maxCount;
        }

        internal static bool TryParseOptionalPositiveCount(string[] args, int defaultCount, out int count)
        {
            count = defaultCount;
            if (!HasArgCountInRange(args, 1, 2))
            {
                return false;
            }

            return args.Length != 2
                || (int.TryParse(args[1], out count) && count > 0);
        }

        internal static bool TryParseProxyListenPort(string value, out int listenPort)
        {
            return int.TryParse(value, out listenPort) && listenPort >= 0 && listenPort <= ushort.MaxValue;
        }
    }
}
