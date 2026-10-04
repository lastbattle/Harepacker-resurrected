using System;

namespace HaCreator.MapSimulator.Contracts;

public enum RuntimeBackgroundKind
{
    Animation = 1,
    Background = 2,
    Spine = 3
}

public static class RuntimeBackgroundKindExtensions
{
    public static string ToPropertyString(this RuntimeBackgroundKind kind) => kind switch
    {
        RuntimeBackgroundKind.Animation => "ani",
        RuntimeBackgroundKind.Background => "back",
        RuntimeBackgroundKind.Spine => "spine",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
