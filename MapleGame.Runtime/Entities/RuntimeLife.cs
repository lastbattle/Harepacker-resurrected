using HaCreator.MapSimulator.Contracts;
using MapleLib.WzLib.WzStructure.Data.MobStructure;
using System;

namespace HaCreator.MapSimulator.Entities;

/// <summary>Detached runtime state for a map life entry.</summary>
public sealed class RuntimeLife
{
    public RuntimeLife(
        RuntimeLifeDefinition definition,
        MobData mobData = null,
        string functionDescription = null,
        bool hideName = false)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Asset = definition.Asset;
        Id = definition.Id;
        DisplayName = definition.DisplayName;
        X = definition.X;
        Y = definition.Y;
        Z = definition.Z;
        Flip = definition.Flip;
        LimitedName = definition.LimitedName;
        Hide = definition.Hide;
        Rx0Shift = definition.Rx0Shift;
        Rx1Shift = definition.Rx1Shift;
        YShift = definition.YShift;
        MobTime = definition.MobTime;
        Info = definition.Info;
        Team = definition.Team;
        MobData = mobData;
        FunctionDescription = functionDescription;
        HideName = hideName;
    }

    public RuntimeAssetKey Asset { get; }
    public string Id { get; }
    public string DisplayName { get; }
    public int X { get; }
    public int Y { get; }
    public int Z { get; }
    public bool Flip { get; set; }
    public string LimitedName { get; }
    public MapleLib.WzLib.WzStructure.MapleBool Hide { get; }
    public int Rx0Shift { get; }
    public int Rx1Shift { get; }
    public int YShift { get; }
    public int? MobTime { get; }
    public int? Info { get; }
    public int? Team { get; }
    public MobData MobData { get; private set; }
    public string FunctionDescription { get; }
    public bool HideName { get; }

    public void SetMobDataIfMissing(MobData mobData)
    {
        MobData ??= mobData;
    }
}
