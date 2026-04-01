using HaCreator.MapSimulator.Character;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Managers
{
    public enum MapTransferRuntimeRequestType
    {
        Delete = 0,
        Register = 1
    }

    public sealed class MapTransferRuntimeRequest
    {
        public MapTransferRuntimeRequestType Type { get; init; }
        public MapTransferDestinationBook Book { get; init; }
        public int MapId { get; init; }
        public int SlotIndex { get; init; } = -1;
    }

    public sealed class MapTransferRuntimeResponse
    {
        public bool Applied { get; init; }
        public string FailureMessage { get; init; }
        public int FocusMapId { get; init; }
        public int FocusSlotIndex { get; init; } = -1;
    }

    /// <summary>
    /// Mirrors the client's fixed teleport destination arrays per character while
    /// preserving the existing AppData store as the persistence layer.
    /// </summary>
    public sealed class MapTransferRuntimeManager
    {
        public const int EmptyDestinationMapId = 999_999_999;
        public const int RegularCapacity = 5;
        public const int ContinentCapacity = 10;

        private sealed class CharacterRuntimeBooks
        {
            public int[] RegularSlots { get; } = CreateEmptySlots(RegularCapacity);
            public int[] ContinentSlots { get; } = CreateEmptySlots(ContinentCapacity);
        }

        private readonly MapTransferDestinationStore _store;
        private readonly Dictionary<string, CharacterRuntimeBooks> _runtimeBooksByCharacter = new(StringComparer.Ordinal);

        public MapTransferRuntimeManager(MapTransferDestinationStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public IReadOnlyList<MapTransferDestinationRecord> GetDestinations(CharacterBuild build, MapTransferDestinationBook book)
        {
            CharacterRuntimeBooks runtimeBooks = GetOrCreateBooks(build);
            int[] slots = GetSlots(runtimeBooks, book);
            List<MapTransferDestinationRecord> destinations = new();
            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                int mapId = slots[slotIndex];
                if (mapId <= 0 || mapId == EmptyDestinationMapId)
                {
                    continue;
                }

                destinations.Add(new MapTransferDestinationRecord
                {
                    SlotIndex = slotIndex,
                    MapId = mapId,
                    DisplayName = null
                });
            }

            return destinations;
        }

        public MapTransferRuntimeResponse SubmitRequest(CharacterBuild build, MapTransferRuntimeRequest request)
        {
            if (request == null)
            {
                return new MapTransferRuntimeResponse();
            }

            CharacterRuntimeBooks runtimeBooks = GetOrCreateBooks(build);
            int[] slots = GetSlots(runtimeBooks, request.Book);

            return request.Type switch
            {
                MapTransferRuntimeRequestType.Register => RegisterDestination(build, request, slots),
                MapTransferRuntimeRequestType.Delete => DeleteDestination(build, request, slots),
                _ => new MapTransferRuntimeResponse()
            };
        }

        private MapTransferRuntimeResponse RegisterDestination(CharacterBuild build, MapTransferRuntimeRequest request, int[] slots)
        {
            if (request.MapId <= 0 || request.MapId == EmptyDestinationMapId)
            {
                return new MapTransferRuntimeResponse
                {
                    FailureMessage = "This destination cannot be saved in a teleport slot."
                };
            }

            int existingSlotIndex = FindSlot(slots, request.MapId);
            if (existingSlotIndex >= 0 && existingSlotIndex != request.SlotIndex)
            {
                return new MapTransferRuntimeResponse
                {
                    FailureMessage = "That map is already registered in this destination book.",
                    FocusMapId = request.MapId,
                    FocusSlotIndex = existingSlotIndex
                };
            }

            int targetSlotIndex = request.SlotIndex >= 0
                ? request.SlotIndex
                : FindFirstEmptySlot(slots);
            if (targetSlotIndex < 0 || targetSlotIndex >= slots.Length)
            {
                return new MapTransferRuntimeResponse
                {
                    FailureMessage = "All saved teleport slots are already filled."
                };
            }

            slots[targetSlotIndex] = request.MapId;
            PersistSlots(build, request.Book, slots);
            return new MapTransferRuntimeResponse
            {
                Applied = true,
                FocusMapId = request.MapId,
                FocusSlotIndex = targetSlotIndex
            };
        }

        private MapTransferRuntimeResponse DeleteDestination(CharacterBuild build, MapTransferRuntimeRequest request, int[] slots)
        {
            int targetSlotIndex = request.SlotIndex >= 0
                ? request.SlotIndex
                : FindSlot(slots, request.MapId);
            if (targetSlotIndex < 0 || targetSlotIndex >= slots.Length || slots[targetSlotIndex] == EmptyDestinationMapId)
            {
                return new MapTransferRuntimeResponse();
            }

            int removedMapId = slots[targetSlotIndex];
            slots[targetSlotIndex] = EmptyDestinationMapId;
            PersistSlots(build, request.Book, slots);
            return new MapTransferRuntimeResponse
            {
                Applied = true,
                FocusMapId = removedMapId,
                FocusSlotIndex = targetSlotIndex
            };
        }

        private CharacterRuntimeBooks GetOrCreateBooks(CharacterBuild build)
        {
            string key = ResolveCharacterKey(build);
            if (_runtimeBooksByCharacter.TryGetValue(key, out CharacterRuntimeBooks runtimeBooks))
            {
                return runtimeBooks;
            }

            runtimeBooks = new CharacterRuntimeBooks();
            HydrateSlots(build, MapTransferDestinationBook.Regular, runtimeBooks.RegularSlots);
            HydrateSlots(build, MapTransferDestinationBook.Continent, runtimeBooks.ContinentSlots);
            _runtimeBooksByCharacter[key] = runtimeBooks;
            return runtimeBooks;
        }

        private void HydrateSlots(CharacterBuild build, MapTransferDestinationBook book, int[] slots)
        {
            Array.Fill(slots, EmptyDestinationMapId);
            foreach (MapTransferDestinationRecord destination in _store.GetDestinations(build, book))
            {
                if (destination == null || destination.MapId <= 0)
                {
                    continue;
                }

                if (destination.SlotIndex < 0 || destination.SlotIndex >= slots.Length)
                {
                    continue;
                }

                slots[destination.SlotIndex] = destination.MapId;
            }
        }

        private void PersistSlots(CharacterBuild build, MapTransferDestinationBook book, int[] slots)
        {
            List<MapTransferDestinationRecord> destinations = new();
            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                int mapId = slots[slotIndex];
                if (mapId <= 0 || mapId == EmptyDestinationMapId)
                {
                    continue;
                }

                destinations.Add(new MapTransferDestinationRecord
                {
                    SlotIndex = slotIndex,
                    MapId = mapId,
                    DisplayName = null
                });
            }

            _store.ReplaceBook(build, destinations, slots.Length, book);
        }

        private static int[] GetSlots(CharacterRuntimeBooks runtimeBooks, MapTransferDestinationBook book)
        {
            return book == MapTransferDestinationBook.Continent
                ? runtimeBooks.ContinentSlots
                : runtimeBooks.RegularSlots;
        }

        private static int FindSlot(int[] slots, int mapId)
        {
            if (slots == null || mapId <= 0)
            {
                return -1;
            }

            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                if (slots[slotIndex] == mapId)
                {
                    return slotIndex;
                }
            }

            return -1;
        }

        private static int FindFirstEmptySlot(int[] slots)
        {
            if (slots == null)
            {
                return -1;
            }

            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                if (slots[slotIndex] == EmptyDestinationMapId)
                {
                    return slotIndex;
                }
            }

            return -1;
        }

        private static int[] CreateEmptySlots(int capacity)
        {
            int[] slots = new int[Math.Max(0, capacity)];
            Array.Fill(slots, EmptyDestinationMapId);
            return slots;
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
}
