using System;

namespace HaCreator.MapSimulator
{
    internal static class MonsterCarnivalSessionCommandParsing
    {
        internal const string SessionUsage = "Usage: /mcarnival session [status|discover <remotePort> [processName|pid] [localPort]|attach <remotePort> [processName|pid] [localPort]|attachproxy <listenPort|0> <remotePort> [processName|pid] [localPort]|start <listenPort|0> <serverHost> <serverPort>|startauto <listenPort|0> <remotePort> [processName|pid] [localPort>|map <opcode> <enter|personalcp|teamcp|requestresult|requestfailure|processfordeath|memberout|gameresult>|unmap <opcode>|clearmap|recent|stop]";
        internal const string DiscoverUsage = "Usage: /mcarnival session discover <remotePort> [processName|pid] [localPort]";
        internal const string AttachUsage = "Usage: /mcarnival session attach <remotePort> [processName|pid] [localPort]";
        internal const string AttachProxyUsage = "Usage: /mcarnival session attachproxy <listenPort|0> <remotePort> [processName|pid] [localPort]";
        internal const string StartUsage = "Usage: /mcarnival session start <listenPort|0> <serverHost> <serverPort>";
        internal const string StartAutoUsage = "Usage: /mcarnival session startauto <listenPort|0> <remotePort> [processName|pid] [localPort]";
        internal const string MapUsage = "Usage: /mcarnival session map <opcode> <enter|personalcp|teamcp|requestresult|requestfailure|processfordeath|memberout|gameresult>";
        internal const string UnmapUsage = "Usage: /mcarnival session unmap <opcode>";

        internal static bool TryParseProxyListenPort(string value, out int listenPort)
        {
            return int.TryParse(value, out listenPort) && listenPort >= 0 && listenPort <= ushort.MaxValue;
        }
    }
}
