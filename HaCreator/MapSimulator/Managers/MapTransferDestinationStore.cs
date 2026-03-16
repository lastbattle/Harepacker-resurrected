using HaCreator.MapSimulator.Character;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Stores simulator-side map transfer destinations using the active character identity,
    /// matching teleport-rock behavior more closely than a single session-global list.
    /// </summary>
    public sealed class MapTransferDestinationStore
    {
        private readonly Dictionary<string, List<MapTransferDestinationRecord>> _destinationsByCharacter = new(StringComparer.Ordinal);

        public IReadOnlyList<MapTransferDestinationRecord> GetDestinations(CharacterBuild build)
        {
            string key = ResolveCharacterKey(build);
            if (_destinationsByCharacter.TryGetValue(key, out List<MapTransferDestinationRecord> destinations))
            {
                return destinations;
            }

            return Array.Empty<MapTransferDestinationRecord>();
        }

        public bool Contains(CharacterBuild build, int mapId)
        {
            if (mapId <= 0)
            {
                return false;
            }

            IReadOnlyList<MapTransferDestinationRecord> destinations = GetDestinations(build);
            for (int i = 0; i < destinations.Count; i++)
            {
                if (destinations[i].MapId == mapId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryAdd(CharacterBuild build, MapTransferDestinationRecord destination, int maxCapacity)
        {
            if (destination == null || destination.MapId <= 0 || maxCapacity <= 0)
            {
                return false;
            }

            string key = ResolveCharacterKey(build);
            List<MapTransferDestinationRecord> destinations = GetOrCreateBucket(key);
            if (destinations.Count >= maxCapacity)
            {
                return false;
            }

            for (int i = 0; i < destinations.Count; i++)
            {
                if (destinations[i].MapId == destination.MapId)
                {
                    return false;
                }
            }

            destinations.Add(destination);
            return true;
        }

        public bool Remove(CharacterBuild build, int mapId)
        {
            if (mapId <= 0)
            {
                return false;
            }

            string key = ResolveCharacterKey(build);
            if (!_destinationsByCharacter.TryGetValue(key, out List<MapTransferDestinationRecord> destinations))
            {
                return false;
            }

            int removedCount = destinations.RemoveAll(destination => destination.MapId == mapId);
            if (destinations.Count == 0)
            {
                _destinationsByCharacter.Remove(key);
            }

            return removedCount > 0;
        }

        private List<MapTransferDestinationRecord> GetOrCreateBucket(string key)
        {
            if (!_destinationsByCharacter.TryGetValue(key, out List<MapTransferDestinationRecord> destinations))
            {
                destinations = new List<MapTransferDestinationRecord>();
                _destinationsByCharacter[key] = destinations;
            }

            return destinations;
        }

        private static string ResolveCharacterKey(CharacterBuild build)
        {
            if (build == null)
            {
                return "session:default";
            }

            if (build.Id > 0)
            {
                return $"id:{build.Id}";
            }

            if (!string.IsNullOrWhiteSpace(build.Name))
            {
                return $"name:{build.Name.Trim().ToLowerInvariant()}";
            }

            return "session:default";
        }
    }

    public sealed class MapTransferDestinationRecord
    {
        public int MapId { get; init; }
        public string DisplayName { get; init; }
    }
}
