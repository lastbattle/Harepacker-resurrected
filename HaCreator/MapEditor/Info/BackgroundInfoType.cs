using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapEditor.Info
{
    public enum BackgroundInfoType
    {
        Animation = 1,
        Background = 2,
        Spine = 3
    }

    public static class BackgroundInfoTypeExtensions
    {
        public static string ToPropertyString(this BackgroundInfoType type)
        {
            return type switch
            {
                BackgroundInfoType.Animation => "ani",
                BackgroundInfoType.Background => "back",
                BackgroundInfoType.Spine => "spine",
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Invalid BackgroundInfoType")
            };
        }
    }
}