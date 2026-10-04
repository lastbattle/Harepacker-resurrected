using HaCreator.MapSimulator.Contracts;
using MapleLib.WzLib;
using System;

namespace HaCreator.MapSimulator.Entities;

/// <summary>Detached, mutable runtime state for a map reactor.</summary>
public sealed class RuntimeReactor
{
    public RuntimeReactor(RuntimeReactorDefinition definition, WzImage templateImage)
    {
        Asset = definition.Asset;
        Id = definition.Id;
        DisplayName = definition.DisplayName;
        X = definition.X;
        Y = definition.Y;
        Z = definition.Z;
        Flip = definition.Flip;
        ReactorTime = definition.ReactorTime;
        Name = definition.Name;
        TemplateImage = templateImage ?? throw new ArgumentNullException(nameof(templateImage));
    }

    public RuntimeAssetKey Asset { get; }
    public string Id { get; }
    public string DisplayName { get; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; }
    public bool Flip { get; set; }
    public int ReactorTime { get; }
    public string Name { get; set; }
    public WzImage TemplateImage { get; }
}
