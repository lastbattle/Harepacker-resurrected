using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace HaCreator.MapSimulator
{
    internal sealed class MobSummonSkillInfo
    {
        public List<string> MobIds { get; } = new List<string>();

        public int Limit { get; set; }

        public int HpThresholdPercent { get; set; }

        public int SummonCount { get; set; }

        public Point? Lt { get; set; }

        public Point? Rb { get; set; }
    }
}
