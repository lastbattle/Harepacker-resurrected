using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator
{
    internal sealed class MobSkillRuntimeData
    {
        public int X { get; init; }

        public int Y { get; init; }

        public int Hp { get; init; }

        public int DurationMs { get; init; }

        public Point? Lt { get; init; }

        public Point? Rb { get; init; }
    }
}
