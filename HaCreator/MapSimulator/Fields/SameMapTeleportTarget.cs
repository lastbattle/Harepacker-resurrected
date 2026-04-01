namespace HaCreator.MapSimulator
{
    internal sealed class SameMapTeleportTarget
    {
        public SameMapTeleportTarget(float x, float y, int portalIndex = -1, string sourcePortalName = null, string targetPortalName = null)
        {
            X = x;
            Y = y;
            PortalIndex = portalIndex;
            SourcePortalName = sourcePortalName;
            TargetPortalName = targetPortalName;
        }

        public float X { get; }

        public float Y { get; }

        public int PortalIndex { get; }

        public string SourcePortalName { get; }

        public string TargetPortalName { get; }

        public bool HasPacketOwnedPortalIndex => PortalIndex >= 0;
    }
}
