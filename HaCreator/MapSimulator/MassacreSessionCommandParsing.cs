using System;

namespace HaCreator.MapSimulator
{
    internal static class MassacreSessionCommandParsing
    {
        internal const string SessionUsage = "Usage: /massacre session [status|discover <remotePort> [processName|pid] [localPort]|attach <remotePort> [processName|pid] [localPort]|attachproxy <listenPort|0> <remotePort> [processName|pid] [localPort]|start <listenPort|0> <serverHost> <serverPort>|startauto <listenPort|0> <remotePort> [processName|pid] [localPort]|map <opcode> <clock|context|inc|result>|unmap <opcode|all>|recent [count]|clearrecent|stop]";
        internal const string DiscoverUsage = "Usage: /massacre session discover <remotePort> [processName|pid] [localPort]";
        internal const string AttachUsage = "Usage: /massacre session attach <remotePort> [processName|pid] [localPort]";
        internal const string AttachProxyUsage = "Usage: /massacre session attachproxy <listenPort|0> <remotePort> [processName|pid] [localPort]";
        internal const string StartUsage = "Usage: /massacre session start <listenPort|0> <serverHost> <serverPort>";
        internal const string StartAutoUsage = "Usage: /massacre session startauto <listenPort|0> <remotePort> [processName|pid] [localPort]";
        internal const string MapUsage = "Usage: /massacre session map <opcode> <clock|context|inc|result>";
        internal const string UnmapUsage = "Usage: /massacre session unmap <opcode|all>";
        internal const string RecentUsage = "Usage: /massacre session recent [count]";

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

        internal static bool TryParseOpcode(string value, out int opcode)
        {
            opcode = -1;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            bool parsed = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? int.TryParse(trimmed[2..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out opcode)
                : int.TryParse(trimmed, out opcode);

            return parsed && opcode >= 0 && opcode <= ushort.MaxValue;
        }

        internal static bool TryParseRecentCount(string value, out int count)
        {
            return int.TryParse(value, out count) && count > 0;
        }

        internal static bool TryParseMappedPacketKind(string value, out Managers.MassacrePacketInboxMessageKind kind)
        {
            kind = value?.Trim().ToLowerInvariant() switch
            {
                "clock" or "timer" => Managers.MassacrePacketInboxMessageKind.ClockPayload,
                "context" or "info" => Managers.MassacrePacketInboxMessageKind.InfoPayload,
                "inc" or "gauge" or "incgauge" or "inc-gauge" => Managers.MassacrePacketInboxMessageKind.IncGauge,
                "result" => Managers.MassacrePacketInboxMessageKind.Result,
                _ => default
            };

            return kind != default;
        }
    }
}
