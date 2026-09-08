using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Contracts;
using MapleLib.WzLib.WzStructure;

namespace HaCreator.MapSimulator.Physics;

/// <summary>Detached endpoint used by runtime foothold topology.</summary>
public sealed class RuntimeFootholdAnchor
{
    private readonly List<RuntimeFoothold> _connected = new();

    public RuntimeFootholdAnchor(int x, int y, int layerNumber = 0, int platformNumber = 0)
    {
        X = x;
        Y = y;
        LayerNumber = layerNumber;
        PlatformNumber = platformNumber;
    }

    public int X { get; private set; }
    public int Y { get; private set; }
    public int LayerNumber { get; }
    public int PlatformNumber { get; }
    public IReadOnlyList<RuntimeFoothold> ConnectedLines => _connected;
    public IReadOnlyList<RuntimeFoothold> ConnectedFootholds => _connected;

    public void SetPosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    internal void Connect(RuntimeFoothold foothold)
    {
        if (foothold != null && !_connected.Contains(foothold))
            _connected.Add(foothold);
    }
}

/// <summary>
/// Mutable runtime foothold. It carries the same topology and movement metadata
/// as a native map foothold without retaining Board, MapleDot, or editor shapes.
/// </summary>
public sealed class RuntimeFoothold
{
    public RuntimeFoothold(
        int number,
        RuntimeFootholdAnchor first,
        RuntimeFootholdAnchor second,
        int previous = 0,
        int next = 0,
        int? force = null,
        int? piece = null,
        MapleBool forbidFallDown = default,
        MapleBool cantThrough = default)
    {
        Number = number;
        FirstAnchor = first ?? throw new ArgumentNullException(nameof(first));
        SecondAnchor = second ?? throw new ArgumentNullException(nameof(second));
        Previous = previous;
        Next = next;
        Force = force;
        Piece = piece;
        ForbidFallDown = forbidFallDown;
        CantThrough = cantThrough;
        FirstAnchor.Connect(this);
        SecondAnchor.Connect(this);
    }

    // Runtime-authored synthetic footholds (moving platforms, transport decks)
    // need a stable negative id that can be refreshed while they are reused.
    public int Number { get; set; }
    public int num { get => Number; set => Number = value; }
    public RuntimeFootholdAnchor FirstAnchor { get; }
    public RuntimeFootholdAnchor SecondAnchor { get; }
    public RuntimeFootholdAnchor FirstDot => FirstAnchor;
    public RuntimeFootholdAnchor SecondDot => SecondAnchor;
    public int Previous { get; set; }
    public int Next { get; set; }
    public int prev { get => Previous; set => Previous = value; }
    public int next { get => Next; set => Next = value; }
    public RuntimeFoothold PrevOverride { get; set; }
    public RuntimeFoothold NextOverride { get; set; }
    public RuntimeFoothold prevOverride { get => PrevOverride; set => PrevOverride = value; }
    public RuntimeFoothold nextOverride { get => NextOverride; set => NextOverride = value; }
    public bool IsWall => FirstAnchor.X == SecondAnchor.X;
    public int LayerNumber => FirstAnchor.LayerNumber;
    public int PlatformNumber => FirstAnchor.PlatformNumber;
    public int X1 => FirstAnchor.X;
    public int Y1 => FirstAnchor.Y;
    public int X2 => SecondAnchor.X;
    public int Y2 => SecondAnchor.Y;
    public int? Force { get; set; }
    public int? Piece { get; set; }
    public MapleBool ForbidFallDown { get; set; }
    public MapleBool CantThrough { get; set; }

    public RuntimeFootholdAnchor GetOtherAnchor(RuntimeFootholdAnchor first)
    {
        if (ReferenceEquals(FirstAnchor, first)) return SecondAnchor;
        if (ReferenceEquals(SecondAnchor, first)) return FirstAnchor;
        throw new InvalidOperationException("The foothold does not contain the supplied anchor.");
    }

    public static RuntimeFoothold FromDefinition(RuntimeFootholdDefinition definition,
        RuntimeFootholdAnchor first, RuntimeFootholdAnchor second)
    {
        return new RuntimeFoothold(definition.Number, first, second, definition.Previous,
            definition.Next, definition.Force, definition.Piece, definition.ForbidFallDown,
            definition.CantThrough);
    }
}

/// <summary>Runtime ladder or rope geometry used by movement lookups.</summary>
public readonly record struct RuntimeLadderOrRope(int X, int Top, int Bottom, bool IsLadder);

/// <summary>Builds a connected, mutable runtime graph from detached map descriptors.</summary>
public sealed class RuntimeFootholdGraph
{
    public RuntimeFootholdGraph(IEnumerable<RuntimeFootholdDefinition> footholds)
    {
        var anchors = new Dictionary<(int EndpointId, int X, int Y, int Layer, int Platform), RuntimeFootholdAnchor>();
        var linesByNumber = new Dictionary<int, RuntimeFoothold>();
        var lines = new List<RuntimeFoothold>();
        var usedNumbers = new HashSet<int>();
        int nextSyntheticNumber = int.MinValue + 1;
        foreach (RuntimeFootholdDefinition definition in footholds ?? Enumerable.Empty<RuntimeFootholdDefinition>())
        {
            RuntimeFootholdAnchor first = GetAnchor(anchors, definition.FirstEndpointId,
                definition.X1, definition.Y1, definition.Layer, definition.Platform);
            RuntimeFootholdAnchor second = GetAnchor(anchors, definition.SecondEndpointId,
                definition.X2, definition.Y2, definition.Layer, definition.Platform);
            int number = definition.Number;
            if (!usedNumbers.Add(number))
            {
                do
                {
                    number = nextSyntheticNumber++;
                }
                while (!usedNumbers.Add(number));
            }

            RuntimeFoothold foothold = new(number, first, second, definition.Previous,
                definition.Next, definition.Force, definition.Piece, definition.ForbidFallDown,
                definition.CantThrough);
            lines.Add(foothold);
            if (!linesByNumber.ContainsKey(definition.Number))
                linesByNumber.Add(definition.Number, foothold);
        }

        foreach (RuntimeFoothold foothold in lines)
        {
            if (foothold.Previous != 0
                && linesByNumber.TryGetValue(foothold.Previous, out RuntimeFoothold previous))
                foothold.PrevOverride = previous;
            if (foothold.Next != 0
                && linesByNumber.TryGetValue(foothold.Next, out RuntimeFoothold next))
                foothold.NextOverride = next;
        }
        Anchors = anchors.Values.ToArray();
        Footholds = lines.ToArray();
    }

    public IReadOnlyList<RuntimeFoothold> Footholds { get; }
    public IReadOnlyList<RuntimeFootholdAnchor> Anchors { get; }

    private static RuntimeFootholdAnchor GetAnchor(
        IDictionary<(int EndpointId, int X, int Y, int Layer, int Platform), RuntimeFootholdAnchor> anchors,
        int endpointId, int x, int y, int layer, int platform)
    {
        var key = (endpointId, x, y, layer, platform);
        if (!anchors.TryGetValue(key, out RuntimeFootholdAnchor anchor))
            anchors[key] = anchor = new RuntimeFootholdAnchor(x, y, layer, platform);
        return anchor;
    }
}
