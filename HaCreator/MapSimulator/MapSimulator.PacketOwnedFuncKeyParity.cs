using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int PacketOwnedFuncKeyEntryCount = 89;
        private const int PacketOwnedFuncKeyPayloadSize = 1 + (PacketOwnedFuncKeyEntryCount * 5);
        private const byte PacketOwnedFuncKeySkillType = 1;
        private const byte PacketOwnedFuncKeyItemType = 2;
        private const byte PacketOwnedFuncKeyItemTypeAlt = 3;
        private const byte PacketOwnedFuncKeyFunctionType = 4;
        private const byte PacketOwnedFuncKeyItemTypeCash = 7;
        private const byte PacketOwnedFuncKeyMacroType = 8;
        private const int PacketOwnedPetConsumeMpAttemptThrottleMs = 200;
        private static readonly int[] PacketOwnedFuncKeyLegacyLookupScanCodes = { 112, 115, 121, 123, 125 };
        private static readonly InputAction[] PacketOwnedHotkeyInputActions = BuildPacketOwnedHotkeyInputActions();

        // Client menu ids follow the v95 MENU_* enum used by CDraggableMenu/CFuncKeyMappedMan.
        private static readonly (int ClientFunctionId, InputAction Action)[] PacketOwnedKnownFunctionBindings =
        {
            (0, InputAction.ToggleEquip),
            (1, InputAction.ToggleInventory),
            (2, InputAction.ToggleStats),
            (3, InputAction.ToggleSkills),
            (7, InputAction.ToggleMinimap),
            (8, InputAction.ToggleQuest),
            (9, InputAction.ToggleKeyConfig),
            (15, InputAction.ToggleQuickSlot),
        };

        private readonly PacketOwnedFuncKeyConfigStore _packetOwnedFuncKeyConfigStore = new();
        private PacketOwnedFuncKeyMappedEntry[] _packetOwnedFuncKeyMapped = CreateEmptyPacketOwnedFuncKeyMap();
        private PacketOwnedFuncKeyMappedEntry[] _packetOwnedFuncKeyMappedOld = CreateEmptyPacketOwnedFuncKeyMap();
        private int _packetOwnedPetConsumeItemId;
        private InventoryType _packetOwnedPetConsumeItemInventoryType = InventoryType.NONE;
        private int _packetOwnedPetConsumeMpItemId;
        private int _lastPacketOwnedPetConsumeMpAttemptTick = int.MinValue;
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

        private readonly struct PacketOwnedKeyActionSlot
        {
            public PacketOwnedKeyActionSlot(InputAction action, int slotIndex)
            {
                Action = action;
                SlotIndex = slotIndex;
            }

            public InputAction Action { get; }
            public int SlotIndex { get; }
        }

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

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
            _packetOwnedPetConsumeItemInventoryType = TryParsePersistedPacketOwnedInventoryType(snapshot.PetConsumeInventoryType);
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

            int translatedBindings = ApplyPacketOwnedFuncKeyMappingsToLiveInput(_playerManager?.Input);

            _lastPacketOwnedFuncKeyInitMessage = string.Format(
                CultureInfo.InvariantCulture,
                "Loaded packet-owned function-key fallback config ({0} entries, translated {1} supported bindings, pet HP {2} [{3}], pet MP {4}).",
                CountConfiguredPacketOwnedFuncKeyEntries(_packetOwnedFuncKeyMapped),
                translatedBindings,
                _packetOwnedPetConsumeItemId,
                _packetOwnedPetConsumeItemInventoryType,
                _packetOwnedPetConsumeMpItemId);
            ApplyPacketOwnedPetConsumeItemPreferenceToFieldHazard();
        }

        private void SavePacketOwnedFuncKeyConfigFromLiveInput(PlayerInput input)
        {
            ApplyPersistedPacketOwnedFuncKeyConfig();
            SyncPacketOwnedUtilityWindowBindings(input);
            PersistPacketOwnedFuncKeyConfig(input);
        }

        private void PersistPacketOwnedFuncKeyConfig(PlayerInput input = null)
        {
            var snapshot = new PacketOwnedFuncKeyConfigStore.Snapshot
            {
                PetConsumeItemId = Math.Max(0, _packetOwnedPetConsumeItemId),
                PetConsumeInventoryType = _packetOwnedPetConsumeItemInventoryType == InventoryType.NONE
                    ? string.Empty
                    : _packetOwnedPetConsumeItemInventoryType.ToString(),
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
            int translatedBindings = ApplyPacketOwnedFuncKeyMappingsToLiveInput(_playerManager?.Input);
            PersistPacketOwnedFuncKeyConfig();

            int configuredCount = CountConfiguredPacketOwnedFuncKeyEntries(_packetOwnedFuncKeyMapped);
            string source = fallbackToConfig
                ? "persisted simulator config/default fallback"
                : "packet payload";
            message = string.Format(
                CultureInfo.InvariantCulture,
                "Hydrated packet-owned function-key init from {0}: {1} configured entries, translated {2} supported bindings, cleared {3} action-22 entries, persisted simulator key-config fallback.",
                source,
                configuredCount,
                translatedBindings,
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
                _packetOwnedPetConsumeItemInventoryType = resolvedItemId > 0
                    ? ResolvePacketOwnedPetConsumeInventoryType(resolvedItemId, _packetOwnedPetConsumeItemInventoryType)
                    : InventoryType.NONE;
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

            InventoryType inventoryType = ResolvePacketOwnedPetConsumeInventoryType(
                _packetOwnedPetConsumeItemId,
                _packetOwnedPetConsumeItemInventoryType);
            if (inventoryType != InventoryType.NONE)
            {
                _packetOwnedPetConsumeItemInventoryType = inventoryType;
                SetFieldHazardSharedPetConsumeItem(
                    _packetOwnedPetConsumeItemId,
                    inventoryType,
                    FieldHazardSharedPetConsumeSource.PacketOwnedConfig);
            }
        }

        private InventoryType ResolvePacketOwnedPetConsumeInventoryType(int itemId, InventoryType persistedInventoryType = InventoryType.NONE)
        {
            if (itemId <= 0)
            {
                return InventoryType.NONE;
            }

            IInventoryRuntime inventoryWindow = uiWindowManager?.InventoryWindow as IInventoryRuntime;
            InventoryType resolvedType = NormalizePacketOwnedPetConsumeInventoryType(persistedInventoryType);
            if (resolvedType == InventoryType.NONE)
            {
                resolvedType = NormalizePacketOwnedPetConsumeInventoryType(InventoryItemMetadataResolver.ResolveInventoryType(itemId));
            }

            if (resolvedType == InventoryType.USE)
            {
                if (inventoryWindow == null || inventoryWindow.GetItemCount(resolvedType, itemId) > 0)
                {
                    return resolvedType;
                }
            }

            if (inventoryWindow?.GetItemCount(InventoryType.USE, itemId) > 0)
            {
                return InventoryType.USE;
            }

            return resolvedType == InventoryType.USE
                ? InventoryType.USE
                : InventoryType.NONE;
        }

        private static InventoryType TryParsePersistedPacketOwnedInventoryType(string persistedInventoryType)
        {
            return Enum.TryParse(persistedInventoryType, ignoreCase: true, out InventoryType inventoryType)
                ? NormalizePacketOwnedPetConsumeInventoryType(inventoryType)
                : InventoryType.NONE;
        }

        private static InventoryType NormalizePacketOwnedPetConsumeInventoryType(InventoryType inventoryType)
        {
            return inventoryType == InventoryType.USE
                ? inventoryType
                : InventoryType.NONE;
        }

        private int ApplyPacketOwnedFuncKeyMappingsToLiveInput(PlayerInput input)
        {
            if (input == null)
            {
                return 0;
            }

            int appliedFunctionBindings = 0;
            for (int i = 0; i < PacketOwnedKnownFunctionBindings.Length; i++)
            {
                (int clientFunctionId, InputAction action) = PacketOwnedKnownFunctionBindings[i];
                Keys mappedKey = TryResolvePacketOwnedBindingKey(clientFunctionId, out Keys resolvedKey)
                    ? resolvedKey
                    : Keys.None;

                KeyBinding existingBinding = input.GetBinding(action);
                if (existingBinding != null && existingBinding.PrimaryKey == mappedKey)
                {
                    continue;
                }

                input.SetBinding(
                    action,
                    mappedKey,
                    existingBinding?.SecondaryKey ?? Keys.None,
                    existingBinding?.GamepadButton ?? (Buttons)0);
                appliedFunctionBindings++;
            }

            int appliedHotkeyBindings = ApplyPacketOwnedCastMappingsToLiveInput(input);
            SyncPacketOwnedUtilityWindowBindings(input);
            return appliedFunctionBindings + appliedHotkeyBindings;
        }

        private void SyncPacketOwnedUtilityWindowBindings(PlayerInput input = null)
        {
            uiWindowManager?.SyncKeyBindingsFromPlayerInput(input ?? _playerManager?.Input);
        }

        private void UpdatePacketOwnedPetConsumeMpRuntime(int currentTime)
        {
            if (!_packetOwnedFuncKeyConfigLoaded
                || _packetOwnedPetConsumeMpItemId <= 0
                || _gameState?.PendingMapChange == true
                || _playerManager?.Player is not PlayerCharacter player
                || _playerManager?.Pets == null
                || _playerManager.Pets.IsFieldUsageBlocked
                || _playerManager.Pets.ActivePets.Count <= 0
                || player.Build == null
                || !player.IsAlive
                || player.MaxMP <= 0
                || player.MP >= player.MaxMP)
            {
                return;
            }

            int mpThresholdPercent = Math.Clamp(_statusBarMpWarningThresholdPercent, 1, 99);
            int mpThreshold = Math.Max(1, (int)Math.Ceiling(player.MaxMP * (mpThresholdPercent / 100f)));
            if (player.MP >= mpThreshold)
            {
                return;
            }

            if (_lastPacketOwnedPetConsumeMpAttemptTick != int.MinValue
                && unchecked(currentTime - _lastPacketOwnedPetConsumeMpAttemptTick) < PacketOwnedPetConsumeMpAttemptThrottleMs)
            {
                return;
            }

            InventoryType inventoryType = ResolvePacketOwnedPetConsumeInventoryType(_packetOwnedPetConsumeMpItemId);
            if (inventoryType == InventoryType.NONE)
            {
                return;
            }

            ConsumableItemEffect effect = ResolveConsumableItemEffect(_packetOwnedPetConsumeMpItemId);
            if (effect.FlatMp <= 0 && effect.PercentMp <= 0)
            {
                return;
            }

            _lastPacketOwnedPetConsumeMpAttemptTick = currentTime;
            TryUseConsumableInventoryItem(_packetOwnedPetConsumeMpItemId, inventoryType, currentTime);
        }

        private bool TryResolvePacketOwnedBindingKey(int clientFunctionId, out Keys key)
        {
            key = Keys.None;
            if (clientFunctionId < 0)
            {
                return false;
            }

            for (int scanCode = 0; scanCode < _packetOwnedFuncKeyMapped.Length; scanCode++)
            {
                PacketOwnedFuncKeyMappedEntry entry = ResolvePacketOwnedFuncKeyMappedEntry(scanCode);
                if (entry.Type != PacketOwnedFuncKeyFunctionType || entry.Id != clientFunctionId)
                {
                    continue;
                }

                key = ResolvePacketOwnedScanCodeKey(scanCode);
                return key != Keys.None;
            }

            for (int i = 0; i < PacketOwnedFuncKeyLegacyLookupScanCodes.Length; i++)
            {
                int scanCode = PacketOwnedFuncKeyLegacyLookupScanCodes[i];
                PacketOwnedFuncKeyMappedEntry entry = ResolvePacketOwnedFuncKeyMappedEntry(scanCode);
                if (entry.Type != PacketOwnedFuncKeyFunctionType || entry.Id != clientFunctionId)
                {
                    continue;
                }

                key = ResolvePacketOwnedScanCodeKey(scanCode);
                return key != Keys.None;
            }

            return false;
        }

        private int ApplyPacketOwnedCastMappingsToLiveInput(PlayerInput input)
        {
            if (input == null || _playerManager?.Skills == null)
            {
                return 0;
            }

            var slots = BuildPacketOwnedActionSlots();
            if (slots.Count == 0)
            {
                return 0;
            }

            var keyToSlot = new Dictionary<Keys, PacketOwnedKeyActionSlot>();
            var assignedActions = new HashSet<InputAction>();
            for (int i = 0; i < slots.Count; i++)
            {
                PacketOwnedKeyActionSlot slot = slots[i];
                KeyBinding binding = input.GetBinding(slot.Action);
                if (binding != null && binding.PrimaryKey != Keys.None)
                {
                    keyToSlot[binding.PrimaryKey] = slot;
                }
            }

            int translated = 0;
            foreach ((int scanCode, PacketOwnedFuncKeyMappedEntry entry) in EnumeratePacketOwnedMappedEntries())
            {
                if (!IsPacketOwnedCastEntryType(entry.Type) || entry.Id <= 0)
                {
                    continue;
                }

                Keys key = ResolvePacketOwnedScanCodeKey(scanCode);
                if (key == Keys.None)
                {
                    continue;
                }

                PacketOwnedKeyActionSlot slot = ResolvePacketOwnedActionSlotForKey(
                    key,
                    keyToSlot,
                    assignedActions,
                    slots);
                if (slot.Action == default)
                {
                    continue;
                }

                if (!TryApplyPacketOwnedCastMappingToSkillSlot(slot.SlotIndex, entry))
                {
                    continue;
                }

                KeyBinding existingBinding = input.GetBinding(slot.Action);
                if (existingBinding == null || existingBinding.PrimaryKey != key)
                {
                    input.SetBinding(
                        slot.Action,
                        key,
                        existingBinding?.SecondaryKey ?? Keys.None,
                        existingBinding?.GamepadButton ?? (Buttons)0);
                }

                assignedActions.Add(slot.Action);
                keyToSlot[key] = slot;
                translated++;
            }

            return translated;
        }

        private IEnumerable<(int ScanCode, PacketOwnedFuncKeyMappedEntry Entry)> EnumeratePacketOwnedMappedEntries()
        {
            for (int scanCode = 0; scanCode < PacketOwnedFuncKeyEntryCount; scanCode++)
            {
                yield return (scanCode, ResolvePacketOwnedFuncKeyMappedEntry(scanCode));
            }

            for (int i = 0; i < PacketOwnedFuncKeyLegacyLookupScanCodes.Length; i++)
            {
                int scanCode = PacketOwnedFuncKeyLegacyLookupScanCodes[i];
                yield return (scanCode, ResolvePacketOwnedFuncKeyMappedEntry(scanCode));
            }
        }

        private PacketOwnedFuncKeyMappedEntry ResolvePacketOwnedFuncKeyMappedEntry(int scanCode)
        {
            return scanCode switch
            {
                54 => _packetOwnedFuncKeyMapped[42],
                112 => _packetOwnedFuncKeyMappedOld[2],
                115 => _packetOwnedFuncKeyMappedOld[4],
                121 => _packetOwnedFuncKeyMappedOld[1],
                123 => _packetOwnedFuncKeyMappedOld[0],
                125 => _packetOwnedFuncKeyMappedOld[3],
                _ => scanCode >= 0 && scanCode < _packetOwnedFuncKeyMapped.Length
                    ? _packetOwnedFuncKeyMapped[scanCode]
                    : default,
            };
        }

        private static bool IsPacketOwnedCastEntryType(byte type)
        {
            return type == PacketOwnedFuncKeySkillType
                || type == PacketOwnedFuncKeyItemType
                || type == PacketOwnedFuncKeyItemTypeAlt
                || type == PacketOwnedFuncKeyItemTypeCash
                || type == PacketOwnedFuncKeyMacroType;
        }

        private PacketOwnedKeyActionSlot ResolvePacketOwnedActionSlotForKey(
            Keys key,
            IReadOnlyDictionary<Keys, PacketOwnedKeyActionSlot> keyToSlot,
            ISet<InputAction> assignedActions,
            IReadOnlyList<PacketOwnedKeyActionSlot> slots)
        {
            if (keyToSlot.TryGetValue(key, out PacketOwnedKeyActionSlot existingSlot)
                && !assignedActions.Contains(existingSlot.Action))
            {
                return existingSlot;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                PacketOwnedKeyActionSlot candidate = slots[i];
                if (assignedActions.Contains(candidate.Action))
                {
                    continue;
                }

                return candidate;
            }

            return default;
        }

        private bool TryApplyPacketOwnedCastMappingToSkillSlot(int slotIndex, PacketOwnedFuncKeyMappedEntry entry)
        {
            if (slotIndex < 0 || _playerManager?.Skills == null)
            {
                return false;
            }

            return entry.Type switch
            {
                PacketOwnedFuncKeySkillType => _playerManager.Skills.TrySetHotkey(slotIndex, entry.Id),
                PacketOwnedFuncKeyItemType or PacketOwnedFuncKeyItemTypeAlt or PacketOwnedFuncKeyItemTypeCash
                    => _playerManager.Skills.TrySetItemHotkey(slotIndex, entry.Id, ResolvePacketOwnedHotkeyInventoryType(entry.Id)),
                PacketOwnedFuncKeyMacroType => TryApplyPacketOwnedMacroHotkey(slotIndex, entry.Id),
                _ => false,
            };
        }

        private bool TryApplyPacketOwnedMacroHotkey(int slotIndex, int packetMacroId)
        {
            if (_playerManager?.Skills == null || packetMacroId <= 0)
            {
                return false;
            }

            if (_playerManager.Skills.TrySetMacroHotkey(slotIndex, packetMacroId))
            {
                return true;
            }

            int zeroBasedMacroIndex = packetMacroId - 1;
            return zeroBasedMacroIndex >= 0
                && _playerManager.Skills.TrySetMacroHotkey(slotIndex, zeroBasedMacroIndex);
        }

        private static InventoryType ResolvePacketOwnedHotkeyInventoryType(int itemId)
        {
            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);
            return inventoryType is InventoryType.USE or InventoryType.CASH
                ? inventoryType
                : InventoryType.NONE;
        }

        private static List<PacketOwnedKeyActionSlot> BuildPacketOwnedActionSlots()
        {
            var slots = new List<PacketOwnedKeyActionSlot>(PacketOwnedHotkeyInputActions.Length);
            for (int i = 0; i < PacketOwnedHotkeyInputActions.Length; i++)
            {
                slots.Add(new PacketOwnedKeyActionSlot(PacketOwnedHotkeyInputActions[i], i));
            }

            return slots;
        }

        private static InputAction[] BuildPacketOwnedHotkeyInputActions()
        {
            var actions = new InputAction[SkillManager.TOTAL_SLOT_COUNT];
            int index = 0;
            for (int i = 0; i < SkillManager.PRIMARY_SLOT_COUNT; i++)
            {
                actions[index++] = InputAction.Skill1 + i;
            }

            for (int i = 0; i < SkillManager.FUNCTION_SLOT_COUNT; i++)
            {
                actions[index++] = InputAction.FunctionSlot1 + i;
            }

            for (int i = 0; i < SkillManager.CTRL_SLOT_COUNT; i++)
            {
                actions[index++] = InputAction.CtrlSlot1 + i;
            }

            return actions;
        }

        private static Keys ResolvePacketOwnedScanCodeKey(int scanCode)
        {
            if (scanCode <= 0)
            {
                return Keys.None;
            }

            uint virtualKey = MapVirtualKey((uint)scanCode, 1);
            if (virtualKey == 0)
            {
                return Keys.None;
            }

            return virtualKey switch
            {
                0x11u => Keys.LeftControl,
                0x12u => Keys.LeftAlt,
                0x10u => Keys.LeftShift,
                0x5Bu or 0x5Cu => Keys.None,
                0u => Keys.None,
                _ => (Keys)virtualKey,
            };
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
