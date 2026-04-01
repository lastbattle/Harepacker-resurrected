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

    public enum MapTransferRuntimeResultType
    {
        None = 0,
        RegisterApplied = 2,
        DeleteApplied = 3
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
        public MapTransferRuntimeResultType ResultType { get; init; }
        public bool CanTransferContinent { get; init; }
        public IReadOnlyList<int> FieldList { get; init; } = Array.Empty<int>();
    }

    public sealed class MapTransferRuntimeDispatchResult
    {
        public bool Accepted { get; init; }
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

        private sealed class PendingRuntimeResponse
        {
            public string CharacterKey { get; init; }
            public MapTransferRuntimeResponse Response { get; init; }
        }

        private readonly MapTransferDestinationStore _store;
        private readonly Dictionary<string, CharacterRuntimeBooks> _runtimeBooksByCharacter = new(StringComparer.Ordinal);
        private readonly List<PendingRuntimeResponse> _pendingResponses = new();

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

        public MapTransferRuntimeDispatchResult SendMapTransferRequest(CharacterBuild build, MapTransferRuntimeRequest request)
        {
            if (request == null)
            {
                return new MapTransferRuntimeDispatchResult();
            }

            CharacterRuntimeBooks runtimeBooks = GetOrCreateBooks(build);
            int[] slots = (int[])GetSlots(runtimeBooks, request.Book).Clone();

            MapTransferRuntimeResponse response = request.Type switch
            {
                MapTransferRuntimeRequestType.Register => RegisterDestination(build, request, slots),
                MapTransferRuntimeRequestType.Delete => DeleteDestination(build, request, slots),
                _ => new MapTransferRuntimeResponse()
            };

            if (!response.Applied)
            {
                return new MapTransferRuntimeDispatchResult
                {
                    FailureMessage = response.FailureMessage,
                    FocusMapId = response.FocusMapId,
                    FocusSlotIndex = response.FocusSlotIndex
                };
            }

            string characterKey = ResolveCharacterKey(build);
            _pendingResponses.Add(new PendingRuntimeResponse
            {
                CharacterKey = characterKey,
                Response = new MapTransferRuntimeResponse
                {
                    Applied = true,
                    FailureMessage = response.FailureMessage,
                    FocusMapId = response.FocusMapId,
                    FocusSlotIndex = response.FocusSlotIndex,
                    ResultType = request.Type == MapTransferRuntimeRequestType.Register
                        ? MapTransferRuntimeResultType.RegisterApplied
                        : MapTransferRuntimeResultType.DeleteApplied,
                    CanTransferContinent = request.Book == MapTransferDestinationBook.Continent,
                    FieldList = (int[])slots.Clone()
                }
            });

            return new MapTransferRuntimeDispatchResult
            {
                Accepted = true,
                FocusMapId = response.FocusMapId,
                FocusSlotIndex = response.FocusSlotIndex
            };
        }

        public MapTransferRuntimeResponse SubmitRequest(CharacterBuild build, MapTransferRuntimeRequest request)
        {
            MapTransferRuntimeDispatchResult dispatchResult = SendMapTransferRequest(build, request);
            if (!dispatchResult.Accepted)
            {
                return new MapTransferRuntimeResponse
                {
                    Applied = false,
                    FailureMessage = dispatchResult.FailureMessage,
                    FocusMapId = dispatchResult.FocusMapId,
                    FocusSlotIndex = dispatchResult.FocusSlotIndex
                };
            }

            if (TryDequeueMapTransferResult(build, out MapTransferRuntimeResponse response))
            {
                ApplyMapTransferResult(build, response);
                return response;
            }

            return new MapTransferRuntimeResponse
            {
                Applied = false,
                FailureMessage = "Map transfer request did not produce a runtime response.",
                FocusMapId = dispatchResult.FocusMapId,
                FocusSlotIndex = dispatchResult.FocusSlotIndex
            };
        }

        public bool TryDequeueMapTransferResult(CharacterBuild build, out MapTransferRuntimeResponse response)
        {
            string characterKey = ResolveCharacterKey(build);
            for (int i = 0; i < _pendingResponses.Count; i++)
            {
                PendingRuntimeResponse pendingResponse = _pendingResponses[i];
                if (!string.Equals(pendingResponse.CharacterKey, characterKey, StringComparison.Ordinal))
                {
                    continue;
                }

                _pendingResponses.RemoveAt(i);
                response = pendingResponse.Response;
                return true;
            }

            response = null;
            return false;
        }

        public bool ApplyMapTransferResult(CharacterBuild build, MapTransferRuntimeResponse response)
        {
            if (response == null ||
                !response.Applied ||
                response.ResultType == MapTransferRuntimeResultType.None ||
                response.FieldList == null ||
                response.FieldList.Count == 0)
            {
                return false;
            }

            CharacterRuntimeBooks runtimeBooks = GetOrCreateBooks(build);
            MapTransferDestinationBook book = response.CanTransferContinent
                ? MapTransferDestinationBook.Continent
                : MapTransferDestinationBook.Regular;
            int[] slots = GetSlots(runtimeBooks, book);

            Array.Fill(slots, EmptyDestinationMapId);
            int maxCount = Math.Min(slots.Length, response.FieldList.Count);
            for (int slotIndex = 0; slotIndex < maxCount; slotIndex++)
            {
                int mapId = response.FieldList[slotIndex];
                slots[slotIndex] = mapId > 0 ? mapId : EmptyDestinationMapId;
            }

            PersistSlots(build, book, slots);
            return true;
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

            int targetSlotIndex = request.SlotIndex;
            if (targetSlotIndex < 0)
            {
                targetSlotIndex = FindFirstEmptySlot(slots);
                if (targetSlotIndex < 0 || targetSlotIndex >= slots.Length)
                {
                    return new MapTransferRuntimeResponse
                    {
                        FailureMessage = "All saved teleport slots are already filled."
                    };
                }
            }

            int existingSlotIndex = FindSlot(slots, request.MapId);
            if (existingSlotIndex >= 0 && existingSlotIndex != targetSlotIndex)
            {
                return new MapTransferRuntimeResponse
                {
                    FailureMessage = "That map is already registered in this destination book.",
                    FocusMapId = request.MapId,
                    FocusSlotIndex = existingSlotIndex
                };
            }

            if (targetSlotIndex < 0 || targetSlotIndex >= slots.Length)
            {
                return new MapTransferRuntimeResponse
                {
                    FailureMessage = "All saved teleport slots are already filled."
                };
            }

            slots[targetSlotIndex] = request.MapId;
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
