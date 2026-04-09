namespace HaCreator.MapSimulator
{
    internal sealed class PendingCrossMapTeleportTarget
    {
        public PendingCrossMapTeleportTarget(
            int mapId,
            string sourcePortalName = null,
            string targetPortalName = null,
            float? fallbackX = null,
            float? fallbackY = null,
            string[] targetPortalNameCandidates = null)
        {
            MapId = mapId;
            SourcePortalName = sourcePortalName;
            TargetPortalName = targetPortalName;
            FallbackX = fallbackX;
            FallbackY = fallbackY;
            TargetPortalNameCandidates = targetPortalNameCandidates ?? System.Array.Empty<string>();
        }

        public int MapId { get; }

        public string SourcePortalName { get; }

        public string TargetPortalName { get; }

        public float? FallbackX { get; }

        public float? FallbackY { get; }

        public string[] TargetPortalNameCandidates { get; }

        public bool HasFallbackCoordinates => FallbackX.HasValue && FallbackY.HasValue;
    }
}
