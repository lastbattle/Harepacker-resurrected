using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HaCreator.MapSimulator.Contracts
{
    public readonly record struct RuntimeAssetKey(string Category, string Path)
    {
        public bool IsEmpty => string.IsNullOrWhiteSpace(Path);
    }

    public readonly record struct RuntimeObjectQuestDefinition(int QuestId, int State);

    public readonly record struct RuntimeTileDefinition(
        RuntimeAssetKey Asset,
        int X,
        int Y,
        int Z,
        int Layer,
        int Platform,
        int DrawOrder,
        string TileSet,
        string Variant,
        string Number,
        int Magnification,
        int IntrinsicZ);

    public sealed record RuntimeObjectDefinition
    {
        public RuntimeAssetKey Asset { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
        public int Z { get; init; }
        public int Layer { get; init; }
        public int Platform { get; init; }
        public int DrawOrder { get; init; }
        public string ObjectSet { get; init; }
        public string L0 { get; init; }
        public string L1 { get; init; }
        public string L2 { get; init; }
        public bool Flip { get; init; }
        public MapleBool Rotation { get; init; }
        public MapleBool Hide { get; init; }
        public MapleBool Reactor { get; init; }
        public MapleBool Dynamic { get; init; }
        public MapleBool Flow { get; init; }
        public int? Rx { get; init; }
        public int? Ry { get; init; }
        public int? Cx { get; init; }
        public int? Cy { get; init; }
        public System.Drawing.Point Origin { get; init; }
        public string Name { get; init; }
        public string Tags { get; init; }
        public IReadOnlyList<RuntimeObjectQuestDefinition> Quests { get; init; } = Array.Empty<RuntimeObjectQuestDefinition>();
    }

    public readonly record struct RuntimeBackgroundDefinition(
        RuntimeAssetKey Asset,
        int X,
        int Y,
        int Z,
        int DrawOrder,
        string BackgroundSet,
        string Number,
        int Type,
        int Rx,
        int Ry,
        int Cx,
        int Cy,
        int Alpha,
        bool Front,
        bool Flip,
        int Page,
        int ScreenMode,
        string SpineAnimation,
        bool SpineRandomStart);

    public sealed record RuntimeLifeDefinition
    {
        public RuntimeAssetKey Asset { get; init; }
        public string Id { get; init; }
        public string DisplayName { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
        public int Z { get; init; }
        public bool Flip { get; init; }
        public string LimitedName { get; init; }
        public MapleBool Hide { get; init; }
        public int Rx0Shift { get; init; }
        public int Rx1Shift { get; init; }
        public int YShift { get; init; }
        public int? MobTime { get; init; }
        public int? Info { get; init; }
        public int? Team { get; init; }
    }

    public readonly record struct RuntimeReactorDefinition(
        RuntimeAssetKey Asset,
        string Id,
        string DisplayName,
        int X,
        int Y,
        int Z,
        bool Flip,
        int ReactorTime,
        string Name);

    public sealed record RuntimePortalDefinition
    {
        public RuntimeAssetKey Asset { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
        public int Z { get; init; }
        public string Image { get; init; }
        public string Name { get; init; }
        public PortalType Type { get; init; }
        public string TargetName { get; init; }
        public int TargetMapId { get; init; }
        public string Script { get; init; }
        public int? Delay { get; init; }
        public MapleBool HideTooltip { get; init; }
        public MapleBool OnlyOnce { get; init; }
        public int? HorizontalImpact { get; init; }
        public int? VerticalImpact { get; init; }
        public int? HorizontalRange { get; init; }
        public int? VerticalRange { get; init; }
        public string ReactorName { get; init; }
        public string SessionValueKey { get; init; }
        public string SessionValue { get; init; }
    }

    public readonly record struct RuntimeFootholdDefinition(
        int Number,
        int Previous,
        int Next,
        int X1,
        int Y1,
        int X2,
        int Y2,
        int Layer,
        int Platform,
        int? Force,
        int? Piece,
        MapleBool ForbidFallDown,
        MapleBool CantThrough,
        int FirstEndpointId = 0,
        int SecondEndpointId = 0);

    public readonly record struct RuntimeRopeDefinition(
        int X,
        int Y1,
        int Y2,
        int Layer,
        bool IsLadder,
        bool LadderSetByUser,
        bool UpperFoothold);

    public readonly record struct RuntimeChairDefinition(int X, int Y);

    public readonly record struct RuntimeTooltipDefinition(
        Rectangle Bounds,
        string Title,
        string Description,
        int OriginalNumber,
        Rectangle? CharacterBounds);

    public enum RuntimeMiscKind
    {
        Area,
        SwimArea,
        BuffZone,
        Clock,
        Healer,
        Pulley,
        Ship,
        Unknown
    }

    public sealed record RuntimeMiscDefinition
    {
        public RuntimeMiscKind Kind { get; init; }
        public Rectangle Bounds { get; init; }
        public RuntimeAssetKey Asset { get; init; }
        public string Identifier { get; init; }
        public int ItemId { get; init; }
        public int Interval { get; init; }
        public int Duration { get; init; }
        public int YMin { get; init; }
        public int YMax { get; init; }
        public int HealMin { get; init; }
        public int HealMax { get; init; }
        public int Fall { get; init; }
        public int Rise { get; init; }
        public int? X0 { get; init; }
        public int? ZValue { get; init; }
        public int TimeMove { get; init; }
        public int ShipKind { get; init; }
        public bool Flip { get; init; }
    }

    public readonly record struct RuntimeReflectionDefinition(
        int Gradient,
        int Alpha,
        string ObjectForOverlay,
        bool Reflection,
        bool AlphaTest);

    public enum RuntimeMirrorFieldType
    {
        Info = 0,
        Mob = 1,
        User = 2,
        Npc = 3
    }

    public readonly record struct RuntimeMirrorFieldDefinition(
        Rectangle Bounds,
        int Type,
        Vector2 Offset,
        RuntimeReflectionDefinition Reflection);

    internal static class RuntimeReadOnly
    {
        public static IReadOnlyList<T> Copy<T>(IEnumerable<T> source)
        {
            return new ReadOnlyCollection<T>(source == null ? Array.Empty<T>() : new List<T>(source));
        }
    }
}
