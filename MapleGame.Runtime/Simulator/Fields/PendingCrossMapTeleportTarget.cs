namespace HaCreator.MapSimulator
{
    internal sealed class PendingCrossMapTeleportTarget
    {
        public PendingCrossMapTeleportTarget(
            int mapId,
            int sourceMapId = -1,
            string sourcePortalName = null,
            string targetPortalName = null,
            int targetPortalIndex = -1,
            float? fallbackX = null,
            float? fallbackY = null,
            string[] targetPortalNameCandidates = null)
        {
            MapId = mapId;
            SourceMapId = sourceMapId;
            SourcePortalName = sourcePortalName;
            TargetPortalName = targetPortalName;
            TargetPortalIndex = targetPortalIndex;
            FallbackX = fallbackX;
            FallbackY = fallbackY;
            TargetPortalNameCandidates = targetPortalNameCandidates ?? System.Array.Empty<string>();
        }

        public int MapId { get; }

        public int SourceMapId { get; }

        public string SourcePortalName { get; }

        public string TargetPortalName { get; }

        public int TargetPortalIndex { get; }

        public float? FallbackX { get; }

        public float? FallbackY { get; }

        public string[] TargetPortalNameCandidates { get; }

        public bool HasFallbackCoordinates => FallbackX.HasValue && FallbackY.HasValue;
    }
}
