using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator
{
    internal enum MobSkillTargetMobType
    {
        None = 0,
        NearbyMobs = 1,
        Self = 2
    }

    internal sealed class MobSkillRuntimeData
    {
        public int X { get; init; }
        public int Y { get; init; }
        public int Hp { get; init; }
        public int DurationMs { get; init; }
        public int IntervalMs { get; init; }
        public int BombDelayMs { get; init; }
        public int ElementAttribute { get; init; }
        public int PropPercent { get; init; }
        public int Count { get; init; }
        public MobSkillTargetMobType TargetMobType { get; init; }
        public Point? Lt { get; init; }
        public Point? Rb { get; init; }
        public Point? BombLt { get; init; }
        public Point? BombRb { get; init; }
    }
}
