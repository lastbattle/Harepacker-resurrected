namespace HaCreator.MapSimulator
{
    internal sealed class SameMapTeleportTarget
    {
        public SameMapTeleportTarget(
            float x,
            float y,
            int portalIndex = -1,
            string sourcePortalName = null,
            string targetPortalName = null,
            string[] targetPortalNameCandidates = null,
            bool usesPacketOwnedApply = false)
        {
            X = x;
            Y = y;
            PortalIndex = portalIndex;
            SourcePortalName = sourcePortalName;
            TargetPortalName = targetPortalName;
            TargetPortalNameCandidates = targetPortalNameCandidates ?? System.Array.Empty<string>();
            UsesPacketOwnedApply = usesPacketOwnedApply;
        }

        public float X { get; }

        public float Y { get; }

        public int PortalIndex { get; }

        public string SourcePortalName { get; }

        public string TargetPortalName { get; }

        public string[] TargetPortalNameCandidates { get; }

        public bool UsesPacketOwnedApply { get; }

        public bool HasPacketOwnedPortalIndex => PortalIndex >= 0;
    }
}
