using HaCreator.MapSimulator.WorldMap;
using HaCreator.Wz;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HaCreator.GUI.WorldMap;

/// <summary>Copies editor map names for the runtime's metadata-only availability index.</summary>
internal sealed class EditorWorldMapAvailabilityLookup : IWorldMapAvailabilityLookup
{
    private readonly Dictionary<int, WorldMapAvailabilityNames> names = new();

    public EditorWorldMapAvailabilityLookup(WzInformationManager information)
    {
        if (information?.MapsNameCache == null)
            return;
        foreach (KeyValuePair<string, Tuple<string, string, string>> pair in information.MapsNameCache)
        {
            if (pair.Value != null && int.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                names[id] = new(pair.Value.Item1, pair.Value.Item2, pair.Value.Item3);
        }
    }

    public bool TryGetMapNames(int mapId, out WorldMapAvailabilityNames value) => names.TryGetValue(mapId, out value);
}
