using System;
using System.Collections.Generic;
using System.Linq;


using HaCreator.MapSimulator.Contracts;
using HaCreator.MapSimulator.Physics;

namespace HaCreator.MapSimulator.Physics;

/// <summary>
/// Detached foothold and ladder data used by a simulator session. The snapshot is
/// created once for each loaded board so physics callbacks return stable runtime
/// object identities instead of editor shapes or newly allocated wrappers.
/// </summary>
public sealed class RuntimePhysicsSnapshot
{
    private RuntimePhysicsSnapshot(RuntimeFootholdGraph footholdGraph,
        IReadOnlyList<RuntimeLadderOrRope> laddersOrRopes)
    {
        FootholdGraph = footholdGraph;
        LaddersOrRopes = laddersOrRopes;
    }

    public RuntimeFootholdGraph FootholdGraph { get; }
    public IReadOnlyList<RuntimeFoothold> Footholds => FootholdGraph.Footholds;
    public IReadOnlyList<RuntimeLadderOrRope> LaddersOrRopes { get; }

    public static RuntimePhysicsSnapshot Create(RuntimeMapDefinition map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var ropes = map.Ropes.Select(rope => new RuntimeLadderOrRope(
            rope.X, Math.Min(rope.Y1, rope.Y2), Math.Max(rope.Y1, rope.Y2), rope.IsLadder)).ToArray();
        return new RuntimePhysicsSnapshot(new RuntimeFootholdGraph(map.Footholds), ropes);
    }
    public RuntimeFoothold FindFoothold(float x, float y, float searchRange,
        float upwardTolerance = 10f)
    {
        RuntimeFoothold best = null;
        float bestDistance = float.MaxValue;
        foreach (RuntimeFoothold foothold in Footholds)
        {
            float minX = Math.Min(foothold.FirstDot.X, foothold.SecondDot.X);
            float maxX = Math.Max(foothold.FirstDot.X, foothold.SecondDot.X);
            if (x < minX || x > maxX)
                continue;

            float deltaX = foothold.SecondDot.X - foothold.FirstDot.X;
            float deltaY = foothold.SecondDot.Y - foothold.FirstDot.Y;
            float t = deltaX == 0 ? 0 : (x - foothold.FirstDot.X) / deltaX;
            float footholdY = foothold.FirstDot.Y + (t * deltaY);
            float distance = footholdY - y;
            if (!((distance >= 0 && distance < searchRange)
                || (distance < 0 && -distance <= upwardTolerance)))
                continue;

            float absoluteDistance = Math.Abs(distance);
            if (absoluteDistance < bestDistance)
            {
                bestDistance = absoluteDistance;
                best = foothold;
            }
        }
        return best;
    }

    public static float CalculateYOnFoothold(RuntimeFoothold foothold, float x)
    {
        if (foothold == null)
            return 0;
        float x1 = foothold.FirstDot.X;
        float y1 = foothold.FirstDot.Y;
        float x2 = foothold.SecondDot.X;
        float y2 = foothold.SecondDot.Y;
        if (x2 == x1)
            return (y1 + y2) * 0.5f;
        float t = Math.Clamp((x - x1) / (x2 - x1), 0f, 1f);
        return y1 + ((y2 - y1) * t);
    }

    public RuntimeLadderOrRope? FindLadderOrRope(float x, float y, float range)
    {
        foreach (RuntimeLadderOrRope ladderOrRope in LaddersOrRopes)
        {
            if (Math.Abs(x - ladderOrRope.X) <= range
                && y >= ladderOrRope.Top
                && y <= ladderOrRope.Bottom)
            {
                return ladderOrRope;
            }
        }
        return null;
    }

}
