using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int PacketOwnedFuncKeyEntryCount = 89;
        private const int PacketOwnedFuncKeyPayloadSize = 1 + (PacketOwnedFuncKeyEntryCount * 5);

        private readonly PacketOwnedFuncKeyConfigStore _packetOwnedFuncKeyConfigStore = new();
        private PacketOwnedFuncKeyMappedEntry[] _packetOwnedFuncKeyMapped = CreateEmptyPacketOwnedFuncKeyMap();
        private PacketOwnedFuncKeyMappedEntry[] _packetOwnedFuncKeyMappedOld = CreateEmptyPacketOwnedFuncKeyMap();
        private int _packetOwnedPetConsumeItemId;
        private int _packetOwnedPetConsumeMpItemId;
        private bool _packetOwnedFuncKeyConfigLoaded;
        private string _lastPacketOwnedFuncKeyInitMessage = "Packet-owned function-key init not applied yet.";

        private readonly struct PacketOwnedFuncKeyMappedEntry
        {
            public PacketOwnedFuncKeyMappedEntry(byte type, int id)
            {
                Type = type;
                Id = id;
            }

            public byte Type { get; }
            public int Id { get; }
        }

        private static PacketOwnedFuncKeyMappedEntry[] CreateEmptyPacketOwnedFuncKeyMap()
        {
            return new PacketOwnedFuncKeyMappedEntry[PacketOwnedFuncKeyEntryCount];
        }

        private void ApplyPersistedPacketOwnedFuncKeyConfig()
        {
            if (_packetOwnedFuncKeyConfigLoaded)
            {
                ApplyPacketOwnedPetConsumeItemPreferenceToFieldHazard();
                return;
            }

            _packetOwnedFuncKeyConfigLoaded = true;
            PacketOwnedFuncKeyConfigStore.Snapshot snapshot = _packetOwnedFuncKeyConfigStore.Load();
            if (snapshot == null)
            {
                _lastPacketOwnedFuncKeyInitMessage = "Packet-owned function-key init is using simulator defaults until packet or config ownership arrives.";
                ApplyPacketOwnedPetConsumeItemPreferenceToFieldHazard();
                return;
            }

            if (snapshot.FuncKeyMapped != null && snapshot.FuncKeyMapped.Count > 0)
            {
                LoadPacketOwnedFuncKeyMappedEntries(snapshot.FuncKeyMapped);
                CopyPacketOwnedFuncKeyMap(_packetOwnedFuncKeyMapped, _packetOwnedFuncKeyMappedOld);
            }

            _packetOwnedPetConsumeItemId = Math.Max(0, snapshot.PetConsumeItemId);
            _packetOwnedPetConsumeMpItemId = Math.Max(0, snapshot.PetConsumeMpItemId);

            if (_playerManager?.Input != null && snapshot.SimulatorBindings?.Count > 0)
            {
                foreach (KeyValuePair<string, PacketOwnedFuncKeyConfigStore.BindingRecord> entry in snapshot.SimulatorBindings)
                {
                    if (!Enum.TryParse(entry.Key, out InputAction action))
                    {
                        continue;
                    }

                    PacketOwnedFuncKeyConfigStore.BindingRecord bindingRecord = entry.Value;
                    if (bindingRecord == null
                        || !Enum.TryParse(bindingRecord.PrimaryKey, out Keys primary))
                    {
                        continue;
                    }

                    Enum.TryParse(bindingRecord.SecondaryKey, out Keys secondary);
                    Enum.TryParse(bindingRecord.GamepadButton, out Buttons gamepadButton);
                    _playerManager.Input.SetBinding(action, primary, secondary, gamepadButton);
                }
            }

            _lastPacketOwnedFuncKeyInitMessage = string.Format(
                CultureInfo.InvariantCulture,
                "Loaded packet-owned function-key fallback config ({0} entries, pet HP {1}, pet MP {2}).",
                CountConfiguredPacketOwnedFuncKeyEntries(_packetOwnedFuncKeyMapped),
                _packetOwnedPetConsumeItemId,
                _packetOwnedPetConsumeMpItemId);
            ApplyPacketOwnedPetConsumeItemPreferenceToFieldHazard();
        }

        private void SavePacketOwnedFuncKeyConfigFromLiveInput(PlayerInput input)
        {
            ApplyPersistedPacketOwnedFuncKeyConfig();
            PersistPacketOwnedFuncKeyConfig(input);
        }

        private void PersistPacketOwnedFuncKeyConfig(PlayerInput input = null)
        {
            var snapshot = new PacketOwnedFuncKeyConfigStore.Snapshot
            {
                PetConsumeItemId = Math.Max(0, _packetOwnedPetConsumeItemId),
                PetConsumeMpItemId = Math.Max(0, _packetOwnedPetConsumeMpItemId),
                SimulatorBindings = PacketOwnedFuncKeyConfigStore.CreateBindingRecords(input ?? _playerManager?.Input)
            };

            for (int i = 0; i < _packetOwnedFuncKeyMapped.Length; i++)
            {
                snapshot.FuncKeyMapped.Add(new PacketOwnedFuncKeyConfigStore.FuncKeyMappedRecord
                {
                    Type = _packetOwnedFuncKeyMapped[i].Type,
                    Id = _packetOwnedFuncKeyMapped[i].Id
                });
            }

            _packetOwnedFuncKeyConfigStore.Save(snapshot);
        }

        private bool TryApplyPacketOwnedFuncKeyInitPayload(byte[] payload, out string message)
        {
            ApplyPersistedPacketOwnedFuncKeyConfig();
            bool fallbackToConfig = payload == null
                || payload.Length < PacketOwnedFuncKeyPayloadSize
                || payload[0] != 0;

            int clearedAction22Count = 0;
            if (fallbackToConfig)
            {
                PacketOwnedFuncKeyConfigStore.Snapshot snapshot = _packetOwnedFuncKeyConfigStore.Load();
                if (snapshot?.FuncKeyMapped?.Count > 0)
                {
                    LoadPacketOwnedFuncKeyMappedEntries(snapshot.FuncKeyMapped);
                }
                else
                {
                    Array.Clear(_packetOwnedFuncKeyMapped, 0, _packetOwnedFuncKeyMapped.Length);
                }
            }
            else
            {
                int offset = 1;
                for (int i = 0; i < PacketOwnedFuncKeyEntryCount; i++)
                {
                    byte type = payload[offset];
                    int id = BitConverter.ToInt32(payload, offset + 1);
                    if (id == 22)
                    {
                        type = 0;
                        id = 0;
                        clearedAction22Count++;
                    }

                    _packetOwnedFuncKeyMapped[i] = new PacketOwnedFuncKeyMappedEntry(type, id);
                    offset += 5;
                }
            }

            CopyPacketOwnedFuncKeyMap(_packetOwnedFuncKeyMapped, _packetOwnedFuncKeyMappedOld);
            PersistPacketOwnedFuncKeyConfig();

            int configuredCount = CountConfiguredPacketOwnedFuncKeyEntries(_packetOwnedFuncKeyMapped);
            string source = fallbackToConfig
                ? "persisted simulator config/default fallback"
                : "packet payload";
            message = string.Format(
                CultureInfo.InvariantCulture,
                "Hydrated packet-owned function-key init from {0}: {1} configured entries, cleared {2} action-22 entries, persisted simulator key-config fallback.",
                source,
                configuredCount,
                clearedAction22Count);
            _lastPacketOwnedFuncKeyInitMessage = message;
            return true;
        }

        private bool TryApplyPacketOwnedPetConsumeItemInitPayload(byte[] payload, bool mpItem, out string message)
        {
            ApplyPersistedPacketOwnedFuncKeyConfig();

            int decodedItemId = payload != null && payload.Length >= sizeof(int)
                ? BitConverter.ToInt32(payload, 0)
                : 0;
            int resolvedItemId;
            string source;
            if (decodedItemId > 0)
            {
                resolvedItemId = decodedItemId;
                source = "packet payload";
            }
            else
            {
                resolvedItemId = mpItem ? _packetOwnedPetConsumeMpItemId : _packetOwnedPetConsumeItemId;
                source = resolvedItemId > 0 ? "persisted simulator config fallback" : "empty config fallback";
            }

            if (mpItem)
            {
                _packetOwnedPetConsumeMpItemId = Math.Max(0, resolvedItemId);
            }
            else
            {
                _packetOwnedPetConsumeItemId = Math.Max(0, resolvedItemId);
                ApplyPacketOwnedPetConsumeItemPreferenceToFieldHazard();
            }

            PersistPacketOwnedFuncKeyConfig();

            string itemLabel = ResolvePacketOwnedPetConsumeItemLabel(resolvedItemId);
            message = string.Format(
                CultureInfo.InvariantCulture,
                "Hydrated packet-owned pet auto-{0} consume item from {1}: {2}.",
                mpItem ? "MP" : "HP",
                source,
                itemLabel);
            return true;
        }

        private void ApplyPacketOwnedPetConsumeItemPreferenceToFieldHazard()
        {
            if (_packetOwnedPetConsumeItemId <= 0)
            {
                return;
            }

            InventoryType inventoryType = ResolvePacketOwnedPetConsumeInventoryType(_packetOwnedPetConsumeItemId);
            if (inventoryType != InventoryType.NONE)
            {
                SetFieldHazardSharedPetConsumeItem(_packetOwnedPetConsumeItemId, inventoryType);
            }
        }

        private InventoryType ResolvePacketOwnedPetConsumeInventoryType(int itemId)
        {
            if (itemId <= 0)
            {
                return InventoryType.NONE;
            }

            IInventoryRuntime inventoryWindow = uiWindowManager?.InventoryWindow as IInventoryRuntime;
            InventoryType resolvedType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);
            if (resolvedType == InventoryType.USE || resolvedType == InventoryType.CASH)
            {
                if (inventoryWindow == null || inventoryWindow.GetItemCount(resolvedType, itemId) > 0)
                {
                    return resolvedType;
                }
            }

            if (inventoryWindow != null)
            {
                if (inventoryWindow.GetItemCount(InventoryType.USE, itemId) > 0)
                {
                    return InventoryType.USE;
                }

                if (inventoryWindow.GetItemCount(InventoryType.CASH, itemId) > 0)
                {
                    return InventoryType.CASH;
                }
            }

            return resolvedType == InventoryType.USE || resolvedType == InventoryType.CASH
                ? resolvedType
                : InventoryType.NONE;
        }

        private static void CopyPacketOwnedFuncKeyMap(PacketOwnedFuncKeyMappedEntry[] source, PacketOwnedFuncKeyMappedEntry[] destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            int count = Math.Min(source.Length, destination.Length);
            for (int i = 0; i < count; i++)
            {
                destination[i] = source[i];
            }
        }

        private void LoadPacketOwnedFuncKeyMappedEntries(IReadOnlyList<PacketOwnedFuncKeyConfigStore.FuncKeyMappedRecord> records)
        {
            Array.Clear(_packetOwnedFuncKeyMapped, 0, _packetOwnedFuncKeyMapped.Length);
            if (records == null)
            {
                return;
            }

            int count = Math.Min(records.Count, PacketOwnedFuncKeyEntryCount);
            for (int i = 0; i < count; i++)
            {
                PacketOwnedFuncKeyConfigStore.FuncKeyMappedRecord record = records[i];
                if (record == null)
                {
                    continue;
                }

                int id = record.Id == 22 ? 0 : record.Id;
                byte type = record.Id == 22 ? (byte)0 : record.Type;
                _packetOwnedFuncKeyMapped[i] = new PacketOwnedFuncKeyMappedEntry(type, id);
            }
        }

        private static int CountConfiguredPacketOwnedFuncKeyEntries(IReadOnlyList<PacketOwnedFuncKeyMappedEntry> entries)
        {
            if (entries == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Type != 0 || entries[i].Id != 0)
                {
                    count++;
                }
            }

            return count;
        }

        private static string ResolvePacketOwnedPetConsumeItemLabel(int itemId)
        {
            if (itemId <= 0)
            {
                return "none";
            }

            return InventoryItemMetadataResolver.TryResolveItemName(itemId, out string itemName)
                && !string.IsNullOrWhiteSpace(itemName)
                ? string.Format(CultureInfo.InvariantCulture, "{0} ({1})", itemName.Trim(), itemId)
                : string.Format(CultureInfo.InvariantCulture, "item {0}", itemId);
        }
    }
}
