using System;
using HaCreator.MapSimulator.Effects;

namespace HaCreator.MapSimulator
{
    internal static class GuildBossSessionCommandParsing
    {
        internal const string SessionUsage = "Usage: /guildboss session [status|verify|recent|send [sequence] [tick]|queue [sequence] [tick]|discover <remotePort> [processName|pid] [localPort]|attach <remotePort> [processName|pid] [localPort]|attachproxy <listenPort|0> <remotePort> [processName|pid] [localPort]|start <listenPort|0> <serverHost> <serverPort>|startauto <listenPort|0> <remotePort> [processName|pid] [localPort]|stop]";
        internal const string PulleyRequestUsage = "Usage: /guildboss session <send|queue> [sequence] [tick]";
        internal const string DiscoverUsage = "Usage: /guildboss session discover <remotePort> [processName|pid] [localPort]";
        internal const string AttachUsage = "Usage: /guildboss session attach <remotePort> [processName|pid] [localPort]";
        internal const string AttachProxyUsage = "Usage: /guildboss session attachproxy <listenPort|0> <remotePort> [processName|pid] [localPort]";
        internal const string StartUsage = "Usage: /guildboss session start <listenPort|0> <serverHost> <serverPort>";
        internal const string StartAutoUsage = "Usage: /guildboss session startauto <listenPort|0> <remotePort> [processName|pid] [localPort]";

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

        internal static bool TryParsePulleyRequestSequence(string value, out int sequence)
        {
            return int.TryParse(value, out sequence) && sequence >= 0;
        }

        internal static bool TryParsePulleyRequestTick(string value, out int tick)
        {
            return int.TryParse(value, out tick);
        }

        internal static bool TryParsePulleyRequest(
            string[] args,
            int valueIndex,
            int defaultTick,
            out GuildBossField.PulleyPacketRequest request)
        {
            request = default;
            if (args == null || valueIndex < 0 || args.Length > valueIndex + 2)
            {
                return false;
            }

            int sequence = 0;
            int tick = defaultTick;
            if (args.Length > valueIndex
                && !TryParsePulleyRequestSequence(args[valueIndex], out sequence))
            {
                return false;
            }

            if (args.Length > valueIndex + 1
                && !TryParsePulleyRequestTick(args[valueIndex + 1], out tick))
            {
                return false;
            }

            request = new GuildBossField.PulleyPacketRequest(tick, sequence);
            return true;
        }
    }
}
