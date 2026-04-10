namespace HaCreator.MapSimulator
{
    internal static class DojoSessionCommandParsing
    {
        internal const string SessionUsage = "Usage: /dojo session [status|discover <remotePort> [processName|pid] [localPort]|attach <remotePort> [processName|pid] [localPort]|attachproxy <listenPort|0> <remotePort> [processName|pid] [localPort]|start <listenPort|0> <serverHost> <serverPort>|startauto <listenPort|0> <remotePort> [processName|pid] [localPort]|map <opcode> <clock|stage|clear|timeover>|unmap <opcode>|clearmap|recent|stop]";
        internal const string DiscoverUsage = "Usage: /dojo session discover <remotePort> [processName|pid] [localPort]";
        internal const string AttachUsage = "Usage: /dojo session attach <remotePort> [processName|pid] [localPort]";
        internal const string AttachProxyUsage = "Usage: /dojo session attachproxy <listenPort|0> <remotePort> [processName|pid] [localPort]";
        internal const string StartUsage = "Usage: /dojo session start <listenPort|0> <serverHost> <serverPort>";
        internal const string StartAutoUsage = "Usage: /dojo session startauto <listenPort|0> <remotePort> [processName|pid] [localPort]";
        internal const string MapUsage = "Usage: /dojo session map <opcode> <clock|stage|clear|timeover>";
        internal const string UnmapUsage = "Usage: /dojo session unmap <opcode>";

        internal static bool TryParseProxyListenPort(string value, out int listenPort)
        {
            return int.TryParse(value, out listenPort) && listenPort >= 0;
        }
    }
}
