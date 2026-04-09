using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        private static readonly InputAction[] PacketOwnedProtectedBindings =
        {
            InputAction.MoveLeft,
            InputAction.MoveRight,
            InputAction.MoveUp,
            InputAction.MoveDown,
            InputAction.Jump,
            InputAction.Attack,
            InputAction.Pickup,
            InputAction.Interact,
            InputAction.ToggleChat,
            InputAction.ToggleQuickSlot,
            InputAction.Escape
        };
        private static readonly PacketOwnedKeyActionSlot[] PacketOwnedBindableHotkeySlots = BuildPacketOwnedBindableHotkeySlots();
        private static readonly IReadOnlyDictionary<Keys, int> PacketOwnedPreferredBindableHotkeySlotIndicesByKey = BuildPacketOwnedPreferredBindableHotkeySlotIndicesByKey();

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
            (16, InputAction.ToggleChat),
            (50, InputAction.Pickup),
            (52, InputAction.Attack),
            (53, InputAction.Jump),
            (54, InputAction.Interact),
            (15, InputAction.ToggleQuickSlot),
        };
        private static readonly (string StatusBarEntryName, string MenuEntryName, int ClientFunctionId, int StringPoolId, string FallbackFormat)[] PacketOwnedStatusBarShortcutTooltipBindings =
        {
            ("BtEquip", "BtEquip", 0, 0x989, "Equip ({0})"),
            ("BtInven", "BtItem", 1, 0x98A, "Inventory ({0})"),
            ("BtStat", "BtStat", 2, 0x98B, "Stats ({0})"),
            ("BtSkill", "BtSkill", 3, 0x98C, "Skills ({0})"),
            ("BtQuest", "BtQuest", 8, 0x18ED, "Quest ({0})"),
        };

        private readonly PacketOwnedFuncKeyConfigStore _packetOwnedFuncKeyConfigStore = new();
        private PacketOwnedFuncKeyMappedEntry[] _packetOwnedFuncKeyMapped = CreateEmptyPacketOwnedFuncKeyMap();
        private PacketOwnedFuncKeyMappedEntry[] _packetOwnedFuncKeyMappedOld = CreateEmptyPacketOwnedFuncKeyMap();
        private int[] _packetOwnedBindableHotkeyAssignedScanCodes = CreatePacketOwnedBindableHotkeyAssignmentMap();
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

        [DllImport("user32.dll", EntryPoint = "MapVirtualKeyW", ExactSpelling = true)]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        private static PacketOwnedFuncKeyMappedEntry[] CreateEmptyPacketOwnedFuncKeyMap()
        {
            return new PacketOwnedFuncKeyMappedEntry[PacketOwnedFuncKeyEntryCount];
        }

        private static int[] CreatePacketOwnedBindableHotkeyAssignmentMap()
        {
            int[] assignments = new int[PacketOwnedBindableHotkeySlots.Length];
            Array.Fill(assignments, -1);
            return assignments;
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
            PersistPacketOwnedFuncKeyConfig(input, persistSimulatorBindings: true);
        }

        private void PersistPacketOwnedFuncKeyConfig(PlayerInput input = null, bool persistSimulatorBindings = false)
        {
            PacketOwnedFuncKeyConfigStore.Snapshot existingSnapshot = _packetOwnedFuncKeyConfigStore.Load();
            var snapshot = new PacketOwnedFuncKeyConfigStore.Snapshot
            {
                PetConsumeItemId = Math.Max(0, _packetOwnedPetConsumeItemId),
                PetConsumeInventoryType = _packetOwnedPetConsumeItemInventoryType == InventoryType.NONE
                    ? string.Empty
                    : _packetOwnedPetConsumeItemInventoryType.ToString(),
                PetConsumeMpItemId = Math.Max(0, _packetOwnedPetConsumeMpItemId),
                SimulatorBindings = persistSimulatorBindings
                    ? PacketOwnedFuncKeyConfigStore.CreateBindingRecords(input ?? _playerManager?.Input)
                    : existingSnapshot?.SimulatorBindings != null
                        ? new Dictionary<string, PacketOwnedFuncKeyConfigStore.BindingRecord>(existingSnapshot.SimulatorBindings, StringComparer.Ordinal)
                        : new Dictionary<string, PacketOwnedFuncKeyConfigStore.BindingRecord>(StringComparer.Ordinal)
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

            if (resolvedType != InventoryType.NONE)
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

            if (inventoryWindow?.GetItemCount(InventoryType.CASH, itemId) > 0)
            {
                return InventoryType.CASH;
            }

            return resolvedType;
        }

        private static InventoryType TryParsePersistedPacketOwnedInventoryType(string persistedInventoryType)
        {
            return Enum.TryParse(persistedInventoryType, ignoreCase: true, out InventoryType inventoryType)
                ? NormalizePacketOwnedPetConsumeInventoryType(inventoryType)
                : InventoryType.NONE;
        }

        private static InventoryType NormalizePacketOwnedPetConsumeInventoryType(InventoryType inventoryType)
        {
            return inventoryType is InventoryType.USE or InventoryType.CASH
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
            UpdatePacketOwnedStatusBarShortcutTooltips();
            return appliedFunctionBindings + appliedHotkeyBindings;
        }

        private void SyncPacketOwnedUtilityWindowBindings(PlayerInput input = null)
        {
            uiWindowManager?.SyncKeyBindingsFromPlayerInput(input ?? _playerManager?.Input);
        }

        private void UpdatePacketOwnedStatusBarShortcutTooltips()
        {
            StatusBarPopupMenuWindow menuWindow = uiWindowManager?.GetWindow(MapSimulatorWindowNames.Menu) as StatusBarPopupMenuWindow;
            StatusBarChatUI statusBarChat = statusBarChatUI;
            if (menuWindow == null && statusBarChat == null)
            {
                return;
            }

            for (int i = 0; i < PacketOwnedStatusBarShortcutTooltipBindings.Length; i++)
            {
                (string statusBarEntryName, string menuEntryName, int clientFunctionId, int stringPoolId, string fallbackFormat) = PacketOwnedStatusBarShortcutTooltipBindings[i];
                string tooltipText = BuildPacketOwnedStatusBarShortcutTooltip(clientFunctionId, stringPoolId, fallbackFormat);
                menuWindow?.SetEntryTooltip(menuEntryName, tooltipText);
                statusBarChat?.SetShortcutTooltip(statusBarEntryName, tooltipText);
            }
        }

        private string BuildPacketOwnedStatusBarShortcutTooltip(int clientFunctionId, int stringPoolId, string fallbackFormat)
        {
            if (!TryResolvePacketOwnedStatusBarTooltipKey(clientFunctionId, out Keys key) || key == Keys.None)
            {
                return string.Empty;
            }

            string keyText = FormatPacketOwnedStatusBarShortcutKey(key);
            if (string.IsNullOrWhiteSpace(keyText))
            {
                return string.Empty;
            }

            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                stringPoolId,
                fallbackFormat,
                maxPlaceholderCount: 1,
                out _);
            return string.Format(CultureInfo.InvariantCulture, format, keyText);
        }

        private bool TryResolvePacketOwnedStatusBarTooltipKey(int clientFunctionId, out Keys key)
        {
            key = Keys.None;
            if (clientFunctionId < 0)
            {
                return false;
            }

            for (int scanCode = 0; scanCode < PacketOwnedFuncKeyEntryCount; scanCode++)
            {
                PacketOwnedFuncKeyMappedEntry entry = _packetOwnedFuncKeyMapped[scanCode];
                if (entry.Type != PacketOwnedFuncKeyFunctionType || entry.Id != clientFunctionId)
                {
                    continue;
                }

                key = ResolvePacketOwnedScanCodeKey(scanCode);
                return key != Keys.None;
            }

            return false;
        }

        private static string FormatPacketOwnedStatusBarShortcutKey(Keys key)
        {
            return key switch
            {
                Keys.None => string.Empty,
                Keys.LeftControl => "Ctrl",
                Keys.RightControl => "Ctrl",
                Keys.LeftAlt => "Alt",
                Keys.RightAlt => "Alt",
                Keys.PageUp => "PageUp",
                Keys.PageDown => "PageDown",
                Keys.Insert => "Ins",
                Keys.Delete => "Del",
                >= Keys.D0 and <= Keys.D9 => ((int)(key - Keys.D0)).ToString(CultureInfo.InvariantCulture),
                >= Keys.NumPad0 and <= Keys.NumPad9 => ((int)(key - Keys.NumPad0)).ToString(CultureInfo.InvariantCulture),
                _ => key.ToString()
            };
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

        private KeyConfigWindow.ClientOwnerState ResolvePacketOwnedKeyConfigClientOwnerState(InputAction action)
        {
            for (int i = 0; i < PacketOwnedKnownFunctionBindings.Length; i++)
            {
                (int clientFunctionId, InputAction mappedAction) = PacketOwnedKnownFunctionBindings[i];
                if (mappedAction != action)
                {
                    continue;
                }

                return TryResolvePacketOwnedBindingKey(clientFunctionId, out Keys functionKey)
                    ? new KeyConfigWindow.ClientOwnerState(hasClientOwner: true, clientFunctionId, functionKey)
                    : new KeyConfigWindow.ClientOwnerState(hasClientOwner: false, clientFunctionId, Keys.None);
            }

            if (!TryResolvePacketOwnedBindableHotkeySlotIndex(action, out int slotIndex)
                || slotIndex < 0
                || slotIndex >= _packetOwnedBindableHotkeyAssignedScanCodes.Length)
            {
                return default;
            }

            int scanCode = _packetOwnedBindableHotkeyAssignedScanCodes[slotIndex];
            if (scanCode < 0)
            {
                return default;
            }

            PacketOwnedFuncKeyMappedEntry entry = ResolvePacketOwnedFuncKeyMappedEntry(scanCode);
            if (!IsPacketOwnedCastEntryType(entry.Type) || entry.Id <= 0)
            {
                return default;
            }

            Keys key = ResolvePacketOwnedScanCodeKey(scanCode);
            return new KeyConfigWindow.ClientOwnerState(
                hasClientOwner: false,
                clientFunctionId: -1,
                clientKey: key,
                packetEntryType: entry.Type,
                packetEntryId: entry.Id,
                packetScanCode: scanCode);
        }

        private KeyConfigWindow.ShortcutVisualState ResolvePacketOwnedKeyConfigShortcutVisualState(InputAction action)
        {
            if (_playerManager?.Skills == null
                || !TryResolvePacketOwnedBindableHotkeySlotIndex(action, out int slotIndex)
                || slotIndex < 0)
            {
                return default;
            }

            int skillId = _playerManager.Skills.GetHotkeySkill(slotIndex);
            if (skillId > 0)
            {
                return BuildPacketOwnedKeyConfigSkillShortcutVisualState(skillId);
            }

            int itemId = _playerManager.Skills.GetHotkeyItem(slotIndex);
            if (itemId > 0)
            {
                InventoryType inventoryType = _playerManager.Skills.GetHotkeyItemInventoryType(slotIndex);
                int itemCount = _playerManager.Skills.GetHotkeyItemCount(slotIndex);
                return BuildPacketOwnedKeyConfigItemShortcutVisualState(itemId, inventoryType, itemCount);
            }

            int macroIndex = _playerManager.Skills.GetHotkeyMacroIndex(slotIndex);
            if (macroIndex >= 0)
            {
                return BuildPacketOwnedKeyConfigMacroShortcutVisualState(macroIndex);
            }

            return default;
        }

        private KeyConfigWindow.ShortcutVisualState BuildPacketOwnedKeyConfigSkillShortcutVisualState(int skillId)
        {
            SkillData skill = _playerManager?.SkillLoader?.LoadSkill(skillId);
            string title = !string.IsNullOrWhiteSpace(skill?.Name)
                ? skill.Name
                : $"Skill {skillId}";
            int skillLevel = Math.Max(0, _playerManager?.Skills?.GetSkillLevel(skillId) ?? 0);
            string detail = skillLevel > 0
                ? $"Packet-owned skill entry {skillId} is staged through the live hotkey map at level {skillLevel}."
                : $"Packet-owned skill entry {skillId} is staged through the live hotkey map.";
            string badgeText = skillLevel > 0
                ? $"Lv{skillLevel}"
                : "SKILL";
            return new KeyConfigWindow.ShortcutVisualState(
                skill?.IconTexture,
                title,
                detail,
                badgeText: badgeText);
        }

        private KeyConfigWindow.ShortcutVisualState BuildPacketOwnedKeyConfigItemShortcutVisualState(
            int itemId,
            InventoryType inventoryType,
            int itemCount)
        {
            string title = InventoryItemMetadataResolver.TryResolveItemName(itemId, out string itemName)
                && !string.IsNullOrWhiteSpace(itemName)
                    ? itemName
                    : $"Item {itemId}";
            string inventoryLabel = inventoryType != InventoryType.NONE
                ? inventoryType.ToString()
                : "Unknown";
            bool unavailable = itemCount <= 0;
            string detail = unavailable
                ? $"Packet-owned {inventoryLabel.ToLowerInvariant()} item entry {itemId} is mapped, but the live inventory count is empty."
                : $"Packet-owned {inventoryLabel.ToLowerInvariant()} item entry {itemId} is staged through the live hotkey map.";
            string badgeText = inventoryType == InventoryType.CASH
                ? "CASH"
                : "ITEM";
            string quantityText = itemCount > 1
                ? itemCount.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
            return new KeyConfigWindow.ShortcutVisualState(
                ResolvePacketOwnedKeyConfigItemIcon(itemId, inventoryType),
                title,
                detail,
                badgeText: badgeText,
                quantityText: quantityText,
                unavailable: unavailable);
        }

        private Texture2D ResolvePacketOwnedKeyConfigItemIcon(int itemId, InventoryType inventoryType)
        {
            Texture2D itemTexture = (uiWindowManager?.InventoryWindow as IInventoryRuntime)?.GetItemTexture(inventoryType, itemId);
            if (itemTexture != null && !itemTexture.IsDisposed)
            {
                return itemTexture;
            }

            return LoadInventoryItemIcon(itemId);
        }

        private KeyConfigWindow.ShortcutVisualState BuildPacketOwnedKeyConfigMacroShortcutVisualState(int macroIndex)
        {
            SkillMacro macro = uiWindowManager?.SkillMacroWindow?.GetMacro(macroIndex);
            int displaySkillId = ResolvePacketOwnedMacroDisplaySkillId(macro);
            SkillData displaySkill = displaySkillId > 0
                ? _playerManager?.SkillLoader?.LoadSkill(displaySkillId)
                : null;
            int configuredSkillCount = CountPacketOwnedConfiguredMacroSkills(macro);
            string title = !string.IsNullOrWhiteSpace(macro?.Name)
                ? macro.Name
                : $"Macro {macroIndex + 1}";
            string detail = configuredSkillCount > 0
                ? $"Packet-owned macro entry {macroIndex + 1} is staged through the live hotkey map with {configuredSkillCount} configured skill step{(configuredSkillCount == 1 ? string.Empty : "s")}."
                : $"Packet-owned macro entry {macroIndex + 1} is staged through the live hotkey map, but the macro is still empty.";
            bool unavailable = macro == null || !macro.IsEnabled || configuredSkillCount <= 0;
            return new KeyConfigWindow.ShortcutVisualState(
                displaySkill?.IconTexture,
                title,
                detail,
                badgeText: $"M{macroIndex + 1}",
                unavailable: unavailable);
        }

        private static int ResolvePacketOwnedMacroDisplaySkillId(SkillMacro macro)
        {
            if (macro?.SkillIds == null)
            {
                return 0;
            }

            for (int i = 0; i < macro.SkillIds.Length; i++)
            {
                if (macro.SkillIds[i] > 0)
                {
                    return macro.SkillIds[i];
                }
            }

            return 0;
        }

        private static int CountPacketOwnedConfiguredMacroSkills(SkillMacro macro)
        {
            if (macro?.SkillIds == null)
            {
                return 0;
            }

            int configuredSkillCount = 0;
            for (int i = 0; i < macro.SkillIds.Length; i++)
            {
                if (macro.SkillIds[i] > 0)
                {
                    configuredSkillCount++;
                }
            }

            return configuredSkillCount;
        }

        private int ApplyPacketOwnedCastMappingsToLiveInput(PlayerInput input)
        {
            if (input == null || _playerManager?.Skills == null)
            {
                return 0;
            }

            ClearPacketOwnedBindableHotkeyMappings(input);

            int translated = 0;
            int nextUnclaimedBindableSlotIndex = 0;
            foreach ((int scanCode, PacketOwnedFuncKeyMappedEntry entry) in EnumeratePacketOwnedCurrentMappedEntries())
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

                if (!TryResolvePacketOwnedBindableHotkeySlot(input, key, ref nextUnclaimedBindableSlotIndex, out PacketOwnedKeyActionSlot slot))
                {
                    continue;
                }

                if (!TryApplyPacketOwnedCastMappingToSkillSlot(slot.SlotIndex, entry))
                {
                    continue;
                }

                BindPacketOwnedHotkeyAction(input, slot.Action, key);
                RecordPacketOwnedBindableHotkeyAssignment(slot, scanCode);
                translated++;
            }

            return translated;
        }

        private IEnumerable<(int ScanCode, PacketOwnedFuncKeyMappedEntry Entry)> EnumeratePacketOwnedCurrentMappedEntries()
        {
            for (int scanCode = 0; scanCode < PacketOwnedFuncKeyEntryCount; scanCode++)
            {
                yield return (scanCode, ResolvePacketOwnedFuncKeyMappedEntry(scanCode));
            }
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

        private bool TryExecutePacketOwnedMacro(int packetMacroId, int currentTime)
        {
            if (_playerManager?.Skills == null || packetMacroId <= 0)
            {
                return false;
            }

            if (_playerManager.Skills.TryExecuteMacro(packetMacroId, currentTime))
            {
                return true;
            }

            int zeroBasedMacroIndex = packetMacroId - 1;
            return zeroBasedMacroIndex >= 0
                && _playerManager.Skills.TryExecuteMacro(zeroBasedMacroIndex, currentTime);
        }

        private static InventoryType ResolvePacketOwnedHotkeyInventoryType(int itemId)
        {
            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);
            return inventoryType is InventoryType.USE or InventoryType.CASH
                ? inventoryType
                : InventoryType.NONE;
        }

        private void UpdatePacketOwnedFuncKeyRuntime(
            int currentTime,
            KeyboardState keyboardState,
            KeyboardState previousKeyboardState,
            bool isWindowActive,
            bool suppressInput)
        {
            if (!isWindowActive
                || suppressInput
                || !_packetOwnedFuncKeyConfigLoaded
                || _gameState?.PendingMapChange == true
                || _gameState?.IsPlayerInputEnabled != true
                || _playerManager?.IsPlayerActive != true
                || _playerManager?.Player is not PlayerCharacter player
                || !player.IsAlive
                || _playerManager.Skills == null)
            {
                return;
            }

            foreach ((int scanCode, PacketOwnedFuncKeyMappedEntry entry) in EnumeratePacketOwnedCurrentMappedEntries())
            {
                if (!IsPacketOwnedCastEntryType(entry.Type)
                    || entry.Id <= 0)
                {
                    continue;
                }

                Keys key = ResolvePacketOwnedScanCodeKey(scanCode);
                if (!ShouldHandlePacketOwnedCastEntryViaRawRuntime(
                    _playerManager?.Input,
                    key,
                    IsPacketOwnedCastEntryHandledByLiveHotkeyBinding(scanCode)))
                {
                    continue;
                }

                int ownerInputToken = ComposePacketOwnedFuncKeyInputToken(scanCode);
                if (WasPacketOwnedFuncKeyPressed(keyboardState, previousKeyboardState, key))
                {
                    TryDispatchPacketOwnedRawFuncKeyEntry(entry, currentTime, ownerInputToken);
                }

                if (entry.Type == PacketOwnedFuncKeySkillType
                    && WasPacketOwnedFuncKeyReleased(keyboardState, previousKeyboardState, key))
                {
                    _playerManager.Skills.ReleasePacketOwnedFuncKeySkillIfActive(entry.Id, currentTime, ownerInputToken);
                }
            }
        }

        private bool TryDispatchPacketOwnedRawFuncKeyEntry(PacketOwnedFuncKeyMappedEntry entry, int currentTime, int ownerInputToken)
        {
            return entry.Type switch
            {
                PacketOwnedFuncKeySkillType => _playerManager?.Skills?.TryCastPacketOwnedFuncKeySkill(entry.Id, currentTime, ownerInputToken) == true,
                PacketOwnedFuncKeyItemType or PacketOwnedFuncKeyItemTypeAlt or PacketOwnedFuncKeyItemTypeCash => TryUsePacketOwnedFuncKeyItem(entry.Id, currentTime),
                PacketOwnedFuncKeyMacroType => TryExecutePacketOwnedMacro(entry.Id, currentTime),
                _ => false,
            };
        }

        private bool TryUsePacketOwnedFuncKeyItem(int itemId, int currentTime)
        {
            InventoryType inventoryType = ResolvePacketOwnedHotkeyInventoryType(itemId);
            return inventoryType != InventoryType.NONE
                && TryUseConsumableInventoryItem(itemId, inventoryType, currentTime);
        }

        private static bool WasPacketOwnedFuncKeyPressed(KeyboardState keyboardState, KeyboardState previousKeyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !previousKeyboardState.IsKeyDown(key);
        }

        private static bool WasPacketOwnedFuncKeyReleased(KeyboardState keyboardState, KeyboardState previousKeyboardState, Keys key)
        {
            return !keyboardState.IsKeyDown(key) && previousKeyboardState.IsKeyDown(key);
        }

        private static bool ShouldHandlePacketOwnedCastEntryViaRawRuntime(
            PlayerInput input,
            Keys key,
            bool handledByLiveHotkeyBinding)
        {
            return !handledByLiveHotkeyBinding
                && key != Keys.None
                && !IsPacketOwnedHotkeyKeyProtected(input, key);
        }

        private static int ComposePacketOwnedFuncKeyInputToken(int scanCode)
        {
            return 0x50000000 | (scanCode & 0xFFFF);
        }

        private bool TryResolvePacketOwnedBindableHotkeySlot(
            PlayerInput input,
            Keys key,
            ref int nextUnclaimedBindableSlotIndex,
            out PacketOwnedKeyActionSlot slot)
        {
            slot = default;
            if (input == null
                || key == Keys.None
                || IsPacketOwnedHotkeyKeyProtected(input, key))
            {
                return false;
            }

            if (PacketOwnedPreferredBindableHotkeySlotIndicesByKey.TryGetValue(key, out int preferredSlotIndex)
                && preferredSlotIndex >= 0
                && preferredSlotIndex < _packetOwnedBindableHotkeyAssignedScanCodes.Length
                && _packetOwnedBindableHotkeyAssignedScanCodes[preferredSlotIndex] < 0)
            {
                slot = PacketOwnedBindableHotkeySlots[preferredSlotIndex];
                return true;
            }

            while (nextUnclaimedBindableSlotIndex < _packetOwnedBindableHotkeyAssignedScanCodes.Length
                && _packetOwnedBindableHotkeyAssignedScanCodes[nextUnclaimedBindableSlotIndex] >= 0)
            {
                nextUnclaimedBindableSlotIndex++;
            }

            if (nextUnclaimedBindableSlotIndex < 0
                || nextUnclaimedBindableSlotIndex >= _packetOwnedBindableHotkeyAssignedScanCodes.Length)
            {
                return false;
            }

            slot = PacketOwnedBindableHotkeySlots[nextUnclaimedBindableSlotIndex];
            return true;
        }

        private void ClearPacketOwnedBindableHotkeyMappings(PlayerInput input)
        {
            if (_playerManager?.Skills == null)
            {
                return;
            }

            Array.Fill(_packetOwnedBindableHotkeyAssignedScanCodes, -1);
            for (int i = 0; i < PacketOwnedBindableHotkeySlots.Length; i++)
            {
                PacketOwnedKeyActionSlot slot = PacketOwnedBindableHotkeySlots[i];
                _playerManager.Skills.ClearHotkey(slot.SlotIndex);

                if (input != null)
                {
                    KeyBinding existingBinding = input.GetBinding(slot.Action);
                    input.SetBinding(
                        slot.Action,
                        Keys.None,
                        Keys.None,
                        existingBinding?.GamepadButton ?? (Buttons)0);
                }
            }
        }

        private void BindPacketOwnedHotkeyAction(PlayerInput input, InputAction action, Keys key)
        {
            if (input == null)
            {
                return;
            }

            KeyBinding existingBinding = input.GetBinding(action);
            if (existingBinding != null
                && existingBinding.PrimaryKey == key
                && existingBinding.SecondaryKey == Keys.None)
            {
                return;
            }

            input.SetBinding(
                action,
                key,
                Keys.None,
                existingBinding?.GamepadButton ?? (Buttons)0);
        }

        private void RecordPacketOwnedBindableHotkeyAssignment(PacketOwnedKeyActionSlot slot, int scanCode)
        {
            for (int i = 0; i < PacketOwnedBindableHotkeySlots.Length; i++)
            {
                if (PacketOwnedBindableHotkeySlots[i].Action == slot.Action
                    && PacketOwnedBindableHotkeySlots[i].SlotIndex == slot.SlotIndex)
                {
                    _packetOwnedBindableHotkeyAssignedScanCodes[i] = scanCode;
                    return;
                }
            }
        }

        private bool IsPacketOwnedCastEntryHandledByLiveHotkeyBinding(int scanCode)
        {
            for (int i = 0; i < _packetOwnedBindableHotkeyAssignedScanCodes.Length; i++)
            {
                if (_packetOwnedBindableHotkeyAssignedScanCodes[i] == scanCode)
                {
                    return true;
                }
            }

            return false;
        }

        private static PacketOwnedKeyActionSlot[] BuildPacketOwnedBindableHotkeySlots()
        {
            var slots = new List<PacketOwnedKeyActionSlot>(SkillManager.TOTAL_SLOT_COUNT);
            for (int i = 0; i < SkillManager.PRIMARY_SLOT_COUNT; i++)
            {
                slots.Add(new PacketOwnedKeyActionSlot(InputAction.Skill1 + i, i));
            }

            for (int i = 0; i < SkillManager.FUNCTION_SLOT_COUNT; i++)
            {
                slots.Add(new PacketOwnedKeyActionSlot(
                    InputAction.FunctionSlot1 + i,
                    SkillManager.FUNCTION_SLOT_OFFSET + i));
            }

            for (int i = 0; i < SkillManager.CTRL_SLOT_COUNT; i++)
            {
                slots.Add(new PacketOwnedKeyActionSlot(
                    InputAction.CtrlSlot1 + i,
                    SkillManager.CTRL_SLOT_OFFSET + i));
            }

            return slots.ToArray();
        }

        private static IReadOnlyDictionary<Keys, int> BuildPacketOwnedPreferredBindableHotkeySlotIndicesByKey()
        {
            var slots = new Dictionary<Keys, int>();
            int nextSlotIndex = 0;
            foreach ((InputAction action, Keys primary, _, _) in PlayerInput.GetDefaultBindings())
            {
                if (primary == Keys.None
                    || !TryResolvePacketOwnedBindableHotkeySlotIndex(action, out _)
                    || nextSlotIndex >= PacketOwnedBindableHotkeySlots.Length)
                {
                    continue;
                }

                slots[primary] = nextSlotIndex++;
            }

            return slots;
        }

        private static bool TryResolvePacketOwnedBindableHotkeySlotIndex(InputAction action, out int slotIndex)
        {
            if (action >= InputAction.Skill1 && action <= InputAction.Skill8)
            {
                slotIndex = action - InputAction.Skill1;
                return true;
            }

            if (action >= InputAction.FunctionSlot1 && action <= InputAction.FunctionSlot12)
            {
                slotIndex = SkillManager.FUNCTION_SLOT_OFFSET + (action - InputAction.FunctionSlot1);
                return true;
            }

            if (action >= InputAction.CtrlSlot1 && action <= InputAction.CtrlSlot8)
            {
                slotIndex = SkillManager.CTRL_SLOT_OFFSET + (action - InputAction.CtrlSlot1);
                return true;
            }

            slotIndex = -1;
            return false;
        }

        private static bool IsPacketOwnedProtectedBindingAction(InputAction action)
        {
            for (int i = 0; i < PacketOwnedProtectedBindings.Length; i++)
            {
                if (PacketOwnedProtectedBindings[i] == action)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPacketOwnedHotkeyKeyProtected(PlayerInput input, Keys key)
        {
            if (input == null || key == Keys.None)
            {
                return false;
            }

            foreach ((InputAction action, _, _, _) in PlayerInput.GetDefaultBindings())
            {
                if (!IsPacketOwnedProtectedBindingAction(action))
                {
                    continue;
                }

                KeyBinding binding = input.GetBinding(action);
                if (binding != null
                    && (binding.PrimaryKey == key || binding.SecondaryKey == key))
                {
                    return true;
                }
            }

            return false;
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
