using HaCreator.MapSimulator.Character;
using System;
using System.Collections.Generic;
using System.IO;

namespace HaCreator.MapSimulator.Managers
{
    public enum MapTransferRuntimePacketResultCode : byte
    {
        None = 0,
        RegisterApplied = 2,
        DeleteApplied = 3,
        NoEmptySlot = 5,
        AlreadyRegistered = 9,
        CannotSaveDestination = 10
    }

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
        public MapTransferRuntimePacketResultCode PacketResultCode { get; init; }
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
            public byte[] Payload { get; init; } = Array.Empty<byte>();
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
                _ => new MapTransferRuntimeResponse
                {
                    FailureMessage = "Unknown map transfer request."
                }
            };

            MapTransferRuntimePacketResultCode packetResultCode = response.PacketResultCode;
            if (packetResultCode == MapTransferRuntimePacketResultCode.None)
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
                Payload = BuildResponsePayload(request, response, slots)
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

            if (TryDequeueMapTransferResultPayload(build, out byte[] payload) &&
                ApplyMapTransferResultPayload(build, payload, out MapTransferRuntimeResponse response))
            {
                return new MapTransferRuntimeResponse
                {
                    Applied = response.Applied,
                    FailureMessage = response.FailureMessage,
                    FocusMapId = dispatchResult.FocusMapId,
                    FocusSlotIndex = dispatchResult.FocusSlotIndex,
                    ResultType = response.ResultType,
                    PacketResultCode = response.PacketResultCode,
                    CanTransferContinent = response.CanTransferContinent,
                    FieldList = response.FieldList
                };
            }

            return new MapTransferRuntimeResponse
            {
                Applied = false,
                FailureMessage = "Map transfer request did not produce a runtime response.",
                FocusMapId = dispatchResult.FocusMapId,
                FocusSlotIndex = dispatchResult.FocusSlotIndex
            };
        }

        public MapTransferRuntimeResponse PreviewRequest(CharacterBuild build, MapTransferRuntimeRequest request)
        {
            if (request == null)
            {
                return new MapTransferRuntimeResponse();
            }

            CharacterRuntimeBooks runtimeBooks = GetOrCreateBooks(build);
            int[] slots = (int[])GetSlots(runtimeBooks, request.Book).Clone();

            return request.Type switch
            {
                MapTransferRuntimeRequestType.Register => RegisterDestination(build, request, slots),
                MapTransferRuntimeRequestType.Delete => DeleteDestination(build, request, slots),
                _ => new MapTransferRuntimeResponse
                {
                    FailureMessage = "Unknown map transfer request."
                }
            };
        }

        public bool TryDequeueMapTransferResultPayload(CharacterBuild build, out byte[] payload)
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
                payload = pendingResponse.Payload;
                return true;
            }

            payload = null;
            return false;
        }

        public bool ApplyMapTransferResultPayload(CharacterBuild build, byte[] payload, out MapTransferRuntimeResponse response)
        {
            response = DecodeResponsePayload(payload);
            if (response == null)
            {
                return false;
            }

            if (!response.Applied ||
                response.ResultType == MapTransferRuntimeResultType.None ||
                response.FieldList == null ||
                response.FieldList.Count == 0)
            {
                return true;
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

        public void ApplyAuthoritativeBootstrap(
            CharacterBuild build,
            IReadOnlyList<int> regularFields,
            IReadOnlyList<int> continentFields)
        {
            CharacterRuntimeBooks runtimeBooks = GetOrCreateBooks(build);
            ApplyBootstrapBook(build, MapTransferDestinationBook.Regular, runtimeBooks.RegularSlots, regularFields, RegularCapacity);
            ApplyBootstrapBook(build, MapTransferDestinationBook.Continent, runtimeBooks.ContinentSlots, continentFields, ContinentCapacity);
        }

        private MapTransferRuntimeResponse RegisterDestination(CharacterBuild build, MapTransferRuntimeRequest request, int[] slots)
        {
            if (request.MapId <= 0 || request.MapId == EmptyDestinationMapId)
            {
                return new MapTransferRuntimeResponse
                {
                    PacketResultCode = MapTransferRuntimePacketResultCode.CannotSaveDestination,
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
                        PacketResultCode = MapTransferRuntimePacketResultCode.NoEmptySlot,
                        FailureMessage = "All saved teleport slots are already filled."
                    };
                }
            }

            int existingSlotIndex = FindSlot(slots, request.MapId);
            if (existingSlotIndex >= 0 && existingSlotIndex != targetSlotIndex)
            {
                return new MapTransferRuntimeResponse
                {
                    PacketResultCode = MapTransferRuntimePacketResultCode.AlreadyRegistered,
                    FailureMessage = "That map is already registered in this destination book.",
                    FocusMapId = request.MapId,
                    FocusSlotIndex = existingSlotIndex
                };
            }

            if (targetSlotIndex < 0 || targetSlotIndex >= slots.Length)
            {
                return new MapTransferRuntimeResponse
                {
                    PacketResultCode = MapTransferRuntimePacketResultCode.NoEmptySlot,
                    FailureMessage = "All saved teleport slots are already filled."
                };
            }

            slots[targetSlotIndex] = request.MapId;
            return new MapTransferRuntimeResponse
            {
                Applied = true,
                PacketResultCode = MapTransferRuntimePacketResultCode.RegisterApplied,
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
                PacketResultCode = MapTransferRuntimePacketResultCode.DeleteApplied,
                FocusMapId = removedMapId,
                FocusSlotIndex = targetSlotIndex
            };
        }

        private static byte[] BuildResponsePayload(MapTransferRuntimeRequest request, MapTransferRuntimeResponse response, int[] slots)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            writer.Write((byte)response.PacketResultCode);
            writer.Write(request.Book == MapTransferDestinationBook.Continent);
            if (response.Applied)
            {
                int fieldCount = request.Book == MapTransferDestinationBook.Continent
                    ? ContinentCapacity
                    : RegularCapacity;
                for (int slotIndex = 0; slotIndex < fieldCount; slotIndex++)
                {
                    int mapId = slotIndex < slots.Length ? slots[slotIndex] : EmptyDestinationMapId;
                    writer.Write(mapId);
                }
            }

            return stream.ToArray();
        }

        private static MapTransferRuntimeResponse DecodeResponsePayload(byte[] payload)
        {
            if (payload == null || payload.Length < 2)
            {
                return null;
            }

            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream);
            MapTransferRuntimePacketResultCode packetResultCode = (MapTransferRuntimePacketResultCode)reader.ReadByte();
            bool canTransferContinent = reader.ReadBoolean();
            int fieldCount = canTransferContinent ? ContinentCapacity : RegularCapacity;
            List<int> fieldList = new(fieldCount);

            bool applied = packetResultCode == MapTransferRuntimePacketResultCode.RegisterApplied ||
                           packetResultCode == MapTransferRuntimePacketResultCode.DeleteApplied;
            if (applied)
            {
                for (int slotIndex = 0; slotIndex < fieldCount; slotIndex++)
                {
                    if (stream.Position + sizeof(int) > stream.Length)
                    {
                        return null;
                    }

                    fieldList.Add(reader.ReadInt32());
                }
            }

            return new MapTransferRuntimeResponse
            {
                Applied = applied,
                FailureMessage = ResolveFailureMessage(packetResultCode),
                ResultType = packetResultCode switch
                {
                    MapTransferRuntimePacketResultCode.RegisterApplied => MapTransferRuntimeResultType.RegisterApplied,
                    MapTransferRuntimePacketResultCode.DeleteApplied => MapTransferRuntimeResultType.DeleteApplied,
                    _ => MapTransferRuntimeResultType.None
                },
                PacketResultCode = packetResultCode,
                CanTransferContinent = canTransferContinent,
                FieldList = fieldList
            };
        }

        private static string ResolveFailureMessage(MapTransferRuntimePacketResultCode packetResultCode)
        {
            return packetResultCode switch
            {
                MapTransferRuntimePacketResultCode.NoEmptySlot => "All saved teleport slots are already filled.",
                MapTransferRuntimePacketResultCode.AlreadyRegistered => "That map is already registered in this destination book.",
                MapTransferRuntimePacketResultCode.CannotSaveDestination => "This destination cannot be saved in a teleport slot.",
                _ => null
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

        private void ApplyBootstrapBook(
            CharacterBuild build,
            MapTransferDestinationBook book,
            int[] slots,
            IReadOnlyList<int> fields,
            int expectedCapacity)
        {
            if (slots == null ||
                fields == null ||
                fields.Count < expectedCapacity)
            {
                return;
            }

            Array.Fill(slots, EmptyDestinationMapId);
            for (int slotIndex = 0; slotIndex < expectedCapacity; slotIndex++)
            {
                int mapId = fields[slotIndex];
                slots[slotIndex] = IsBootstrapMapId(mapId)
                    ? mapId
                    : EmptyDestinationMapId;
            }

            PersistSlots(build, book, slots);
        }

        private static bool IsBootstrapMapId(int mapId)
        {
            return mapId == EmptyDestinationMapId || mapId > 0;
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
