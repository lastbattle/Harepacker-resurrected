using HaCreator.MapSimulator.Contracts;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using System;

namespace HaCreator.MapSimulator.Entities;

public sealed class RuntimePortal
{
    public RuntimePortal(RuntimePortalDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Asset = definition.Asset; X = definition.X; Y = definition.Y; Z = definition.Z;
        Image = definition.Image; Name = definition.Name; Type = definition.Type;
        TargetName = definition.TargetName; TargetMapId = definition.TargetMapId; Script = definition.Script;
        Delay = definition.Delay; HideTooltip = definition.HideTooltip; OnlyOnce = definition.OnlyOnce;
        HorizontalImpact = definition.HorizontalImpact; VerticalImpact = definition.VerticalImpact;
        HorizontalRange = definition.HorizontalRange; VerticalRange = definition.VerticalRange;
        ReactorName = definition.ReactorName; SessionValueKey = definition.SessionValueKey;
        SessionValue = definition.SessionValue;
    }

    public RuntimeAssetKey Asset { get; }
    public int X { get; }
    public int Y { get; }
    public int Z { get; }
    public string Image { get; }
    public string Name { get; }
    public PortalType Type { get; }
    public string TargetName { get; }
    public int TargetMapId { get; }
    public string Script { get; }
    public int? Delay { get; }
    public MapleBool HideTooltip { get; }
    public MapleBool OnlyOnce { get; }
    public int? HorizontalImpact { get; }
    public int? VerticalImpact { get; }
    public int? HorizontalRange { get; }
    public int? VerticalRange { get; }
    public string ReactorName { get; }
    public string SessionValueKey { get; }
    public string SessionValue { get; }

    // Compatibility aliases keep the portal migration localized while the large
    // simulator surface moves to the runtime naming convention incrementally.
    public string image => Image;
    public string pn => Name;
    public PortalType pt => Type;
    public string tn => TargetName;
    public int tm => TargetMapId;
    public string script => Script;
    public int? delay => Delay;
    public MapleBool hideTooltip => HideTooltip;
    public MapleBool onlyOnce => OnlyOnce;
    public int? horizontalImpact => HorizontalImpact;
    public int? verticalImpact => VerticalImpact;
    public int? hRange => HorizontalRange;
    public int? vRange => VerticalRange;
    public string reactorName => ReactorName;
    public string sessionValueKey => SessionValueKey;
    public string sessionValue => SessionValue;
}
