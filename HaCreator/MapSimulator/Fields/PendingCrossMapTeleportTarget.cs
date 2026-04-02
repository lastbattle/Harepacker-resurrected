namespace HaCreator.MapSimulator
{
    internal sealed class PendingCrossMapTeleportTarget
    {
        public PendingCrossMapTeleportTarget(int mapId, string targetPortalName = null, float? fallbackX = null, float? fallbackY = null)
        {
            MapId = mapId;
            TargetPortalName = targetPortalName;
            FallbackX = fallbackX;
            FallbackY = fallbackY;
        }

        public int MapId { get; }

        public string TargetPortalName { get; }

        public float? FallbackX { get; }

        public float? FallbackY { get; }

        public bool HasFallbackCoordinates => FallbackX.HasValue && FallbackY.HasValue;
    }
}
