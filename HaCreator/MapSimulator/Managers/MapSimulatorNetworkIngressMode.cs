using System;

namespace HaCreator.MapSimulator.Managers
{
    public static class MapSimulatorNetworkIngressMode
    {
        public const string Proxy = "proxy-ingress";
        public const string Local = "local-ingress";

        public static bool IsKnown(string ingressMode)
        {
            return string.Equals(ingressMode, Proxy, StringComparison.Ordinal)
                || string.Equals(ingressMode, Local, StringComparison.Ordinal);
        }
    }
}
