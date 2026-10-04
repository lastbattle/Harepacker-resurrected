namespace HaCreator.MapSimulator
{
    internal sealed class PendingMapSpawnTarget
    {
        public PendingMapSpawnTarget(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }

        public float Y { get; }
    }
}
