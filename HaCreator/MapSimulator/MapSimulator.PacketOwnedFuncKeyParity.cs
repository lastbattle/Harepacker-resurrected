using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Pools;
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
        private const byte PacketOwnedFuncKeyClientControlType = 5;
        private const byte PacketOwnedFuncKeyEmotionType = 6;
        private const byte PacketOwnedFuncKeyItemTypeCash = 7;
        private const byte PacketOwnedFuncKeyMacroType = 8;
        private const int PacketOwnedPetConsumeMpAttemptThrottleMs = 200;
        private static readonly int[] PacketOwnedFuncKeyLegacyLookupScanCodes = { 112, 115, 121, 123, 125 };
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
        // CUIKeyConfig::ResetPaletteItems seeds palette slots 35-41 as type-6 ids 100-106,
        // and the authored KeyConfig icon art matches the simulator's first seven face states.
        private static readonly (int ClientEmotionId, int EmotionId)[] PacketOwnedKnownEmotionBindings =
        {
            (100, 0),
            (101, 1),
            (102, 2),
            (103, 3),
            (104, 4),
            (105, 5),
            (106, 6),
        };
        private static readonly (string StatusBarEntryName, string MenuEntryName, int ClientFunctionId, int StringPoolId, string FallbackFormat)[] PacketOwnedStatusBarShortcutTooltipBindings =
        {
            ("BtEquip", "BtEquip", 0, 0x989, "Equip ({0})"),
            ("BtInven", "BtItem", 1, 0x98A, "Inventory ({0})"),
            ("BtStat", "BtStat", 2, 0x98B, "Stats ({0})"),
            ("BtSkill", "BtSkill", 3, 0x98C, "Skills ({0})"),
            ("BtQuest", "BtQuest", 8, 0x18ED, "Quest ({0})"),
        };
        private static readonly InputAction[] PacketOwnedProtectedBindings = BuildPacketOwnedProtectedBindings();
        private static readonly PacketOwnedKeyActionSlot[] PacketOwnedBindableHotkeySlots = BuildPacketOwnedBindableHotkeySlots();
        private static readonly IReadOnlyDictionary<Keys, int> PacketOwnedPreferredBindableHotkeySlotIndicesByKey = BuildPacketOwnedPreferredBindableHotkeySlotIndicesByKey();

        private readonly PacketOwnedFuncKeyConfigStore _packetOwnedFuncKeyConfigStore = new();
        private PacketOwnedFuncKeyMappedEntry[] _packetOwnedFuncKeyMapped = CreateEmptyPacketOwnedFuncKeyMap();
        private PacketOwnedFuncKeyMappedEntry[] _packetOwnedFuncKeyMappedOld = CreateEmptyPacketOwnedFuncKeyMap();
        private int[] _packetOwnedBindableHotkeyAssignedScanCodes = CreatePacketOwnedBindableHotkeyAssignmentMap();
        private readonly Dictionary<Keys, List<PacketOwnedCastInputOwner>> _packetOwnedCastInputOwnersByKey = new();
        private readonly Dictionary<Keys, PacketOwnedCastInputOwner> _packetOwnedHeldRawCastInputOwnersByKey = new();
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

        private readonly struct PacketOwnedCastInputOwner
        {
            public PacketOwnedCastInputOwner(
                int scanCode,
                PacketOwnedFuncKeyMappedEntry entry,
                int bindableSlotIndex)
            {
                ScanCode = scanCode;
                Entry = entry;
                BindableSlotIndex = bindableSlotIndex;
            }

            public int ScanCode { get; }
            public PacketOwnedFuncKeyMappedEntry Entry { get; }
            public int BindableSlotIndex { get; }
            public bool IsHandledByLiveHotkeyBinding => BindableSlotIndex >= 0;
        }

        internal enum PacketOwnedRawFunctionOwner
        {
            None = 0,
            SocialListFriend = 1,
            WorldMap = 2,
            Messenger = 3,
            SocialListGuild = 4,
            SocialListParty = 5,
            ShortcutMenu = 6,
            PartySearch = 7,
            Family = 8,
            Profession = 9,
            Event = 10,
            Expedition = 11,
            SpouseWhisper = 12,
            QuestAlarm = 13,
            Sit = 14,
            CashShop = 15,
            Medal = 16,
            ItemPot = 17,
            MagicWheel = 18,
        }

        internal enum PacketOwnedRawChatOwner
        {
            None = 0,
            All = 1,
            WhisperTargetPicker = 2,
            Party = 3,
            Friend = 4,
            Guild = 5,
            Alliance = 6,
            Expedition = 7,
        }

        internal readonly struct PacketOwnedRawFunctionOwnerWindowRoute
        {
            public PacketOwnedRawFunctionOwnerWindowRoute(
                PacketOwnedRawFunctionOwner owner,
                string windowName,
                string uiWindow2SourcePropertyName)
            {
                Owner = owner;
                WindowName = windowName ?? string.Empty;
                UIWindow2SourcePropertyName = uiWindow2SourcePropertyName ?? string.Empty;
            }

            public PacketOwnedRawFunctionOwner Owner { get; }
            public string WindowName { get; }
            public string UIWindow2SourcePropertyName { get; }
            public bool HasUIWindow2Source => !string.IsNullOrWhiteSpace(UIWindow2SourcePropertyName);
        }

        private static readonly PacketOwnedRawFunctionOwnerWindowRoute[] PacketOwnedRawFunctionOwnerWindowRoutes =
        {
            // IDA owner is CUIMedalQuestInfo; in v95 exports the closest authored chrome is
            // UIWindow2.img/Title/main, so keep the medal owner routed there.
            new(
                PacketOwnedRawFunctionOwner.Medal,
                MapSimulatorWindowNames.MedalQuestInfo,
                "Title/main"),
            // Active WZ data exposes these packet-owned raw palette owners under UIWindow2.img.
            new(
                PacketOwnedRawFunctionOwner.ItemPot,
                MapSimulatorWindowNames.ItemPot,
                "itemPot"),
            new(
                PacketOwnedRawFunctionOwner.MagicWheel,
                MapSimulatorWindowNames.MagicWheel,
                "RollingGachaphone"),
        };

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

        private static InputAction[] BuildPacketOwnedProtectedBindings()
        {
            var actions = new List<InputAction>
            {
                InputAction.MoveLeft,
                InputAction.MoveRight,
                InputAction.MoveUp,
                InputAction.MoveDown,
                InputAction.Escape
            };

            for (int i = 0; i < PacketOwnedKnownFunctionBindings.Length; i++)
            {
                InputAction action = PacketOwnedKnownFunctionBindings[i].Action;
                if (!actions.Contains(action))
                {
                    actions.Add(action);
                }
            }

            return actions.ToArray();
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

            if (CountConfiguredPacketOwnedFuncKeyEntries(_packetOwnedFuncKeyMapped) == 0)
            {
                LoadPacketOwnedFuncKeyMappedEntriesFromSimulatorBindings(_playerManager?.Input);
                CopyPacketOwnedFuncKeyMap(_packetOwnedFuncKeyMapped, _packetOwnedFuncKeyMappedOld);
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
            PersistPacketOwnedFuncKeyConfig(
                input,
                persistSimulatorBindings: true,
                persistFuncKeyFallback: true);
        }

        private void PersistPacketOwnedFuncKeyConfig(
            PlayerInput input = null,
            bool persistSimulatorBindings = false,
            bool persistFuncKeyFallback = false)
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

            snapshot.FuncKeyMapped.AddRange(ResolvePersistedPacketOwnedFallbackFuncKeyMappedRecords(
                input ?? _playerManager?.Input,
                existingSnapshot?.FuncKeyMapped,
                persistFuncKeyFallback));

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
                    LoadPacketOwnedFuncKeyMappedEntriesFromSimulatorBindings(_playerManager?.Input);
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

            int translatedBindings = ApplyPacketOwnedFuncKeyMappingsToLiveInput(_playerManager?.Input);
            // Match CFuncKeyMappedMan::OnInit: once hydration/adaptation is complete, mirror the
            // current table into the Old buffer so legacy lookup paths stay aligned to the live map.
            CopyPacketOwnedFuncKeyMap(_packetOwnedFuncKeyMapped, _packetOwnedFuncKeyMappedOld);
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

            InventoryType inventoryType = ResolveFieldHazardClientPetConsumeInventoryType(
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

        private InventoryType ResolveFieldHazardClientPetConsumeInventoryType(int itemId, InventoryType persistedInventoryType = InventoryType.NONE)
        {
            if (itemId <= 0)
            {
                return InventoryType.NONE;
            }

            IInventoryRuntime inventoryWindow = uiWindowManager?.InventoryWindow as IInventoryRuntime;
            if (inventoryWindow?.GetItemCount(InventoryType.USE, itemId) > 0)
            {
                return InventoryType.USE;
            }

            InventoryType resolvedType = ResolvePacketOwnedPetConsumeInventoryType(itemId, persistedInventoryType);
            return IsClientFieldHazardPetConsumeInventoryType(resolvedType)
                ? resolvedType
                : InventoryType.NONE;
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
                if (!IsPacketOwnedLiveInputBindingEntry(entry.Type, entry.Id, clientFunctionId))
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
                if (!IsPacketOwnedLiveInputBindingEntry(entry.Type, entry.Id, clientFunctionId))
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

        private void LoadPacketOwnedFuncKeyMappedEntriesFromSimulatorBindings(PlayerInput input)
        {
            PopulatePacketOwnedFuncKeyMappedEntriesFromSimulatorBindings(input, _packetOwnedFuncKeyMapped);
        }

        private bool TrySetPacketOwnedFuncKeyMappedEntryFromSimulatorBinding(Keys key, int clientFunctionId)
        {
            return TrySetPacketOwnedFuncKeyMappedEntryFromSimulatorBinding(_packetOwnedFuncKeyMapped, key, clientFunctionId);
        }

        private static bool TrySetPacketOwnedFuncKeyMappedEntryFromSimulatorBinding(
            PacketOwnedFuncKeyMappedEntry[] destination,
            Keys key,
            int clientFunctionId)
        {
            if (!TryResolvePacketOwnedKeyScanCode(key, out int scanCode)
                || destination == null
                || scanCode < 0
                || scanCode >= destination.Length)
            {
                return false;
            }

            destination[scanCode] = new PacketOwnedFuncKeyMappedEntry(
                PacketOwnedFuncKeyFunctionType,
                clientFunctionId);
            return true;
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
                    ? new KeyConfigWindow.ClientOwnerState(
                        hasClientOwner: true,
                        clientFunctionId: clientFunctionId,
                        clientKey: functionKey,
                        packetPaletteSlotId: ResolvePacketOwnedKeyConfigPaletteSlotId(PacketOwnedFuncKeyFunctionType, clientFunctionId))
                    : new KeyConfigWindow.ClientOwnerState(
                        hasClientOwner: false,
                        clientFunctionId: clientFunctionId,
                        clientKey: Keys.None,
                        packetPaletteSlotId: ResolvePacketOwnedKeyConfigPaletteSlotId(PacketOwnedFuncKeyFunctionType, clientFunctionId));
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
            if (entry.Type == 0 || entry.Id <= 0)
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
                packetScanCode: scanCode,
                packetBindableSlotIndex: slotIndex,
                packetPaletteSlotId: ResolvePacketOwnedKeyConfigPaletteSlotId(entry.Type, entry.Id));
        }

        private KeyConfigWindow.ShortcutVisualState ResolvePacketOwnedKeyConfigShortcutVisualState(InputAction action)
        {
            if (!TryResolvePacketOwnedBindableHotkeySlotIndex(action, out int slotIndex)
                || slotIndex < 0)
            {
                return default;
            }

            SkillManager skillManager = _playerManager?.Skills;
            if (skillManager != null)
            {
                int skillId = skillManager.GetHotkeySkill(slotIndex);
                if (skillId > 0)
                {
                    return BuildPacketOwnedKeyConfigSkillShortcutVisualState(skillId, fromLiveHotkeySlot: true);
                }

                int itemId = skillManager.GetHotkeyItem(slotIndex);
                if (itemId > 0)
                {
                    InventoryType inventoryType = skillManager.GetHotkeyItemInventoryType(slotIndex);
                    int itemCount = skillManager.GetHotkeyItemCount(slotIndex);
                    byte packetEntryType = TryResolvePacketOwnedCastEntryForBindableHotkeyAction(action, out PacketOwnedFuncKeyMappedEntry liveEntry, out _)
                        && liveEntry.Id == itemId
                            ? liveEntry.Type
                            : PacketOwnedFuncKeyItemType;
                    return BuildPacketOwnedKeyConfigItemShortcutVisualState(itemId, inventoryType, itemCount, packetEntryType, fromLiveHotkeySlot: true);
                }

                int macroIndex = skillManager.GetHotkeyMacroIndex(slotIndex);
                if (macroIndex >= 0)
                {
                    return BuildPacketOwnedKeyConfigMacroShortcutVisualState(macroIndex, packetMacroId: macroIndex + 1, fromLiveHotkeySlot: true);
                }
            }

            return TryResolvePacketOwnedCastEntryForBindableHotkeyAction(action, out PacketOwnedFuncKeyMappedEntry entry, out _)
                ? BuildPacketOwnedKeyConfigShortcutVisualStateFromPacketEntry(entry)
                : default;
        }

        private IReadOnlyList<KeyConfigWindow.PacketSlotVisualState> ResolvePacketOwnedKeyConfigPacketSlotVisualStates()
        {
            if (_packetOwnedFuncKeyMapped == null || _packetOwnedFuncKeyMapped.Length == 0)
            {
                return Array.Empty<KeyConfigWindow.PacketSlotVisualState>();
            }

            KeyConfigWindow.PacketSlotVisualState[] states = new KeyConfigWindow.PacketSlotVisualState[_packetOwnedFuncKeyMapped.Length];
            for (int scanCode = 0; scanCode < _packetOwnedFuncKeyMapped.Length; scanCode++)
            {
                PacketOwnedFuncKeyMappedEntry entry = ResolvePacketOwnedFuncKeyMappedEntry(scanCode);
                int packetPaletteSlotId = entry.Type != 0
                    ? ResolvePacketOwnedKeyConfigPaletteSlotId(entry.Type, entry.Id)
                    : -1;
                KeyConfigWindow.ShortcutVisualState shortcutVisualState = BuildPacketOwnedKeyConfigShortcutVisualStateFromPacketEntry(entry);
                states[scanCode] = new KeyConfigWindow.PacketSlotVisualState(
                    scanCode,
                    ResolvePacketOwnedScanCodeKey(scanCode),
                    entry.Type,
                    entry.Id,
                    packetPaletteSlotId,
                    shortcutVisualState);
            }

            return states;
        }

        private KeyConfigWindow.ShortcutVisualState BuildPacketOwnedKeyConfigShortcutVisualStateFromPacketEntry(PacketOwnedFuncKeyMappedEntry entry)
        {
            if (entry.Id <= 0)
            {
                return default;
            }

            return entry.Type switch
            {
                PacketOwnedFuncKeySkillType => BuildPacketOwnedKeyConfigSkillShortcutVisualState(entry.Id, fromLiveHotkeySlot: false),
                PacketOwnedFuncKeyItemType or PacketOwnedFuncKeyItemTypeAlt or PacketOwnedFuncKeyItemTypeCash
                    => BuildPacketOwnedKeyConfigItemShortcutVisualState(
                        entry.Id,
                        ResolvePacketOwnedHotkeyInventoryType(entry.Id),
                        ResolvePacketOwnedKeyConfigItemCount(entry.Id),
                        entry.Type,
                        fromLiveHotkeySlot: false),
                PacketOwnedFuncKeyMacroType => BuildPacketOwnedKeyConfigMacroShortcutVisualState(
                    ResolvePacketOwnedMacroIndex(entry.Id),
                    entry.Id,
                    fromLiveHotkeySlot: false),
                _ => default,
            };
        }

        private KeyConfigWindow.ShortcutVisualState BuildPacketOwnedKeyConfigSkillShortcutVisualState(int skillId, bool fromLiveHotkeySlot)
        {
            SkillData skill = _playerManager?.SkillLoader?.LoadSkill(skillId);
            string title = !string.IsNullOrWhiteSpace(skill?.Name)
                ? skill.Name
                : $"Skill {skillId}";
            int skillLevel = Math.Max(0, _playerManager?.Skills?.GetSkillLevel(skillId) ?? 0);
            string detail = skillLevel > 0
                ? fromLiveHotkeySlot
                    ? $"Packet-owned skill entry {skillId} is staged through the live hotkey map at level {skillLevel}."
                    : $"Packet-owned skill entry {skillId} currently falls through to a direct footer visual at level {skillLevel} while the full live palette owner stays unresolved."
                : fromLiveHotkeySlot
                    ? $"Packet-owned skill entry {skillId} is staged through the live hotkey map."
                    : $"Packet-owned skill entry {skillId} currently falls through to a direct footer visual while the full live palette owner stays unresolved.";
            string badgeText = skillLevel > 0
                ? $"Lv{skillLevel}"
                : "SKILL";
            return new KeyConfigWindow.ShortcutVisualState(
                skill?.IconDisabledTexture ?? skill?.IconTexture,
                title,
                detail,
                badgeText: badgeText,
                drawLayer: KeyConfigWindow.ShortcutVisualState.ClientDrawLayer.Skill);
        }

        private KeyConfigWindow.ShortcutVisualState BuildPacketOwnedKeyConfigItemShortcutVisualState(
            int itemId,
            InventoryType inventoryType,
            int itemCount,
            byte packetEntryType,
            bool fromLiveHotkeySlot)
        {
            string title = InventoryItemMetadataResolver.TryResolveItemName(itemId, out string itemName)
                && !string.IsNullOrWhiteSpace(itemName)
                    ? itemName
                    : $"Item {itemId}";
            string inventoryLabel = inventoryType != InventoryType.NONE
                ? inventoryType.ToString()
                : "Unknown";
            bool drawsStackNumber = packetEntryType == PacketOwnedFuncKeyItemType;
            bool drawsUnavailableOverlay = packetEntryType == PacketOwnedFuncKeyItemTypeAlt && itemCount <= 0;
            bool isCashItemEntry = packetEntryType == PacketOwnedFuncKeyItemTypeCash;
            string clientLayer = packetEntryType switch
            {
                PacketOwnedFuncKeyItemType => "client item stack-number layer",
                PacketOwnedFuncKeyItemTypeAlt => "client item availability overlay layer",
                PacketOwnedFuncKeyItemTypeCash => "client cash-item icon layer",
                _ => "client item icon layer",
            };
            string detail = drawsUnavailableOverlay
                ? fromLiveHotkeySlot
                    ? $"Packet-owned {inventoryLabel.ToLowerInvariant()} item entry {itemId} is mapped through the {clientLayer}, but the live inventory count is empty."
                    : $"Packet-owned {inventoryLabel.ToLowerInvariant()} item entry {itemId} falls through to a direct footer visual through the {clientLayer}, but the live inventory count is empty."
                : fromLiveHotkeySlot
                    ? $"Packet-owned {inventoryLabel.ToLowerInvariant()} item entry {itemId} is staged through the {clientLayer}."
                    : $"Packet-owned {inventoryLabel.ToLowerInvariant()} item entry {itemId} currently falls through to a direct footer visual through the {clientLayer}.";
            string badgeText = isCashItemEntry || inventoryType == InventoryType.CASH
                ? "CASH"
                : "ITEM";
            string quantityText = drawsStackNumber && itemCount > 0
                ? itemCount.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
            KeyConfigWindow.ShortcutVisualState.ClientDrawLayer drawLayer = packetEntryType switch
            {
                PacketOwnedFuncKeyItemType => KeyConfigWindow.ShortcutVisualState.ClientDrawLayer.ItemStack,
                PacketOwnedFuncKeyItemTypeAlt when drawsUnavailableOverlay => KeyConfigWindow.ShortcutVisualState.ClientDrawLayer.ItemUnavailable,
                PacketOwnedFuncKeyItemTypeAlt => KeyConfigWindow.ShortcutVisualState.ClientDrawLayer.ItemStack,
                PacketOwnedFuncKeyItemTypeCash => KeyConfigWindow.ShortcutVisualState.ClientDrawLayer.CashItem,
                _ => KeyConfigWindow.ShortcutVisualState.ClientDrawLayer.ItemStack,
            };

            return new KeyConfigWindow.ShortcutVisualState(
                ResolvePacketOwnedKeyConfigItemIcon(itemId, inventoryType),
                title,
                detail,
                badgeText: badgeText,
                quantityText: quantityText,
                unavailable: drawsUnavailableOverlay,
                drawLayer: drawLayer);
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

        private KeyConfigWindow.ShortcutVisualState BuildPacketOwnedKeyConfigMacroShortcutVisualState(int macroIndex, int packetMacroId, bool fromLiveHotkeySlot)
        {
            SkillMacro macro = macroIndex >= 0
                ? uiWindowManager?.SkillMacroWindow?.GetMacro(macroIndex)
                : null;
            int displaySkillId = ResolvePacketOwnedMacroDisplaySkillId(macro);
            SkillData displaySkill = displaySkillId > 0
                ? _playerManager?.SkillLoader?.LoadSkill(displaySkillId)
                : null;
            int configuredSkillCount = CountPacketOwnedConfiguredMacroSkills(macro);
            string title = !string.IsNullOrWhiteSpace(macro?.Name)
                ? macro.Name
                : $"Macro {packetMacroId}";
            string detail = configuredSkillCount > 0
                ? fromLiveHotkeySlot
                    ? $"Packet-owned macro entry {packetMacroId} is staged through the live hotkey map with {configuredSkillCount} configured skill step{(configuredSkillCount == 1 ? string.Empty : "s")}."
                    : $"Packet-owned macro entry {packetMacroId} currently falls through to a direct footer visual with {configuredSkillCount} configured skill step{(configuredSkillCount == 1 ? string.Empty : "s")}."
                : fromLiveHotkeySlot
                    ? $"Packet-owned macro entry {packetMacroId} is staged through the live hotkey map, but the macro is still empty."
                    : $"Packet-owned macro entry {packetMacroId} currently falls through to a direct footer visual, but the macro is still empty.";
            bool unavailable = macro == null || !macro.IsEnabled || configuredSkillCount <= 0;
            return new KeyConfigWindow.ShortcutVisualState(
                (macroIndex >= 0
                    ? uiWindowManager?.SkillMacroWindow?.GetMacroIconTexture(macroIndex, enabled: macro?.IsEnabled == true)
                    : null)
                    ?? displaySkill?.IconTexture,
                title,
                detail,
                badgeText: $"M{packetMacroId}",
                unavailable: unavailable,
                drawLayer: KeyConfigWindow.ShortcutVisualState.ClientDrawLayer.Macro);
        }

        private int ResolvePacketOwnedKeyConfigItemCount(int itemId)
        {
            IInventoryRuntime inventory = uiWindowManager?.InventoryWindow as IInventoryRuntime;
            if (inventory == null || itemId <= 0)
            {
                return 0;
            }

            int totalCount = 0;
            InventoryType preferredType = ResolvePacketOwnedHotkeyInventoryType(itemId);
            if (preferredType != InventoryType.NONE)
            {
                totalCount += Math.Max(0, inventory.GetItemCount(preferredType, itemId));
            }

            if (preferredType != InventoryType.USE)
            {
                totalCount += Math.Max(0, inventory.GetItemCount(InventoryType.USE, itemId));
            }

            if (preferredType != InventoryType.CASH)
            {
                totalCount += Math.Max(0, inventory.GetItemCount(InventoryType.CASH, itemId));
            }

            return totalCount;
        }

        internal static int ResolvePacketOwnedKeyConfigPaletteSlotId(byte packetEntryType, int packetEntryId)
        {
            // `CUIKeyConfig::{ResetPaletteItems,GetPaletteSlotFromIdx}` keep the authored KeyConfig icon families
            // on explicit function (0-32), control (50-54), and emotion (100-106) ids.
            return packetEntryType switch
            {
                PacketOwnedFuncKeyFunctionType when packetEntryId >= 0 && packetEntryId <= 32 => packetEntryId,
                PacketOwnedFuncKeyClientControlType when packetEntryId >= 50 && packetEntryId <= 54 => packetEntryId,
                PacketOwnedFuncKeyEmotionType when packetEntryId >= 100 && packetEntryId <= 106 => packetEntryId,
                _ => -1,
            };
        }

        private static int ResolvePacketOwnedMacroIndex(int packetMacroId)
        {
            return packetMacroId > 0
                ? packetMacroId - 1
                : -1;
        }

        private bool TryResolvePacketOwnedCastEntryForBindableHotkeyAction(
            InputAction action,
            out PacketOwnedFuncKeyMappedEntry entry,
            out int scanCode)
        {
            entry = default;
            scanCode = -1;
            if (!TryResolvePacketOwnedBindableHotkeySlotIndex(action, out int slotIndex)
                || slotIndex < 0
                || slotIndex >= _packetOwnedBindableHotkeyAssignedScanCodes.Length)
            {
                return false;
            }

            scanCode = _packetOwnedBindableHotkeyAssignedScanCodes[slotIndex];
            if (scanCode < 0)
            {
                return false;
            }

            entry = ResolvePacketOwnedFuncKeyMappedEntry(scanCode);
            return IsPacketOwnedCastEntryType(entry.Type) && entry.Id > 0;
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

            IReadOnlyDictionary<Keys, int> existingBindableHotkeySlotIndicesByKey =
                BuildPacketOwnedExistingBindableHotkeySlotIndicesByKey(input);
            ClearPacketOwnedBindableHotkeyMappings(input);
            _packetOwnedCastInputOwnersByKey.Clear();
            _packetOwnedHeldRawCastInputOwnersByKey.Clear();

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

                int bindableSlotIndex = -1;
                if (TryResolvePacketOwnedBindableHotkeySlot(
                        input,
                        key,
                        existingBindableHotkeySlotIndicesByKey,
                        ref nextUnclaimedBindableSlotIndex,
                        out PacketOwnedKeyActionSlot slot)
                    && TryApplyPacketOwnedCastMappingToSkillSlot(slot.SlotIndex, entry))
                {
                    BindPacketOwnedHotkeyAction(input, slot.Action, key);
                    RecordPacketOwnedBindableHotkeyAssignment(slot, scanCode);
                    translated++;
                    bindableSlotIndex = slot.SlotIndex;
                }

                RegisterPacketOwnedCastInputOwner(scanCode, key, entry, bindableSlotIndex, input);
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
            SkillManager skills = _playerManager?.Skills;
            if (!isWindowActive
                || suppressInput
                || !_packetOwnedFuncKeyConfigLoaded
                || _gameState?.PendingMapChange == true
                || _gameState?.IsPlayerInputEnabled != true
                || _playerManager?.IsPlayerActive != true
                || _playerManager?.Player is not PlayerCharacter player
                || !player.IsAlive
                || skills == null)
            {
                _packetOwnedHeldRawCastInputOwnersByKey.Clear();
                return;
            }

            using var cancelBatchScope = skills.BeginClientCancelBatchScope();
            ReleasePacketOwnedHeldRawCastInputOwnersOnKeyUp(
                skills,
                currentTime,
                keyboardState,
                previousKeyboardState);
            foreach ((int scanCode, PacketOwnedFuncKeyMappedEntry entry) in EnumeratePacketOwnedCurrentMappedEntries())
            {
                if (entry.Type == PacketOwnedFuncKeyFunctionType
                    && entry.Id >= 0
                    && !IsPacketOwnedKnownFunctionId(entry.Id))
                {
                    Keys functionKey = ResolvePacketOwnedScanCodeKey(scanCode);
                    if (functionKey != Keys.None
                        && WasPacketOwnedFuncKeyPressed(keyboardState, previousKeyboardState, functionKey))
                    {
                        TryDispatchPacketOwnedRawFunctionEntry(entry.Id, currentTime);
                    }

                    continue;
                }

                if (entry.Type == PacketOwnedFuncKeyEmotionType
                    && TryResolvePacketOwnedEmotionBinding(entry.Id, out int emotionId))
                {
                    Keys emotionKey = ResolvePacketOwnedScanCodeKey(scanCode);
                    if (ShouldHandlePacketOwnedCastEntryViaRawRuntime(_playerManager?.Input, emotionKey, handledByLiveHotkeyBinding: false)
                        && WasPacketOwnedFuncKeyPressed(keyboardState, previousKeyboardState, emotionKey))
                    {
                        _playerManager?.Player?.TryApplyPacketOwnedEmotion(
                            emotionId,
                            durationMs: 0,
                            byItemOption: false,
                            currentTime,
                            out _);
                    }

                    continue;
                }

                if (!IsPacketOwnedCastEntryType(entry.Type)
                    || entry.Id <= 0)
                {
                    continue;
                }

                Keys key = ResolvePacketOwnedScanCodeKey(scanCode);
                if (key == Keys.None)
                {
                    continue;
                }

                if (TryResolvePacketOwnedHeldRawCastInputOwner(key, out _))
                {
                    // Keep the key bound to the owner that was active at key-down until key-up.
                    continue;
                }

                if (!TryResolvePacketOwnedActiveCastInputOwner(key, out PacketOwnedCastInputOwner owner)
                    || owner.ScanCode != scanCode
                    || owner.IsHandledByLiveHotkeyBinding)
                {
                    continue;
                }

                int ownerInputToken = ComposePacketOwnedFuncKeyInputToken(owner.ScanCode);
                if (WasPacketOwnedFuncKeyPressed(keyboardState, previousKeyboardState, key))
                {
                    if (TryDispatchPacketOwnedRawFuncKeyEntry(owner.Entry, currentTime, ownerInputToken)
                        && owner.Entry.Type == PacketOwnedFuncKeySkillType)
                    {
                        _packetOwnedHeldRawCastInputOwnersByKey[key] = owner;
                    }
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

        private bool TryDispatchPacketOwnedRawFunctionEntry(int clientFunctionId, int currentTime)
        {
            PacketOwnedRawFunctionOwner owner = ResolvePacketOwnedRawFunctionOwner(clientFunctionId);
            switch (owner)
            {
                case PacketOwnedRawFunctionOwner.SocialListFriend:
                    _socialListRuntime.SelectTab(SocialListTab.Friend);
                    ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.SocialList);
                    return true;
                case PacketOwnedRawFunctionOwner.WorldMap:
                    HandleMinimapWorldMapRequested();
                    return true;
                case PacketOwnedRawFunctionOwner.Messenger:
                    ShowMessengerWindow();
                    return true;
                case PacketOwnedRawFunctionOwner.SocialListGuild:
                    _socialListRuntime.SelectTab(SocialListTab.Guild);
                    ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.SocialList);
                    return true;
                case PacketOwnedRawFunctionOwner.SocialListParty:
                    _socialListRuntime.SelectTab(SocialListTab.Party);
                    ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.SocialList);
                    return true;
                case PacketOwnedRawFunctionOwner.ShortcutMenu:
                    ToggleStatusBarPopupWindow(MapSimulatorWindowNames.Menu, MapSimulatorWindowNames.System);
                    return true;
                case PacketOwnedRawFunctionOwner.PartySearch:
                    ApplyPacketOwnedPartySearchLaunch(option: -1);
                    return true;
                case PacketOwnedRawFunctionOwner.Family:
                    ShowPacketOwnedFamilyWindow();
                    return true;
                case PacketOwnedRawFunctionOwner.Profession:
                    ShowPacketOwnedProfessionWindow();
                    return true;
                case PacketOwnedRawFunctionOwner.Event:
                    TogglePacketOwnedRawUtilityWindow(MapSimulatorWindowNames.Event, () =>
                        ShowUtilityWindow(MapSimulatorWindowNames.Event, "packet-owned-funckey:31"));
                    return true;
                case PacketOwnedRawFunctionOwner.Expedition:
                    ShowPacketOwnedExpeditionSearchWindow();
                    return true;
                case PacketOwnedRawFunctionOwner.SpouseWhisper:
                    OpenPacketOwnedSpouseWhisperOwner(currentTime);
                    return true;
                case PacketOwnedRawFunctionOwner.QuestAlarm:
                    TogglePacketOwnedRawUtilityWindow(MapSimulatorWindowNames.QuestAlarm, () =>
                        ShowUtilityWindow(MapSimulatorWindowNames.QuestAlarm, "packet-owned-funckey:20"));
                    return true;
                case PacketOwnedRawFunctionOwner.Sit:
                    TogglePacketOwnedSitOwner();
                    return true;
                case PacketOwnedRawFunctionOwner.CashShop:
                    ShowPacketOwnedCashShopWindow();
                    return true;
                case PacketOwnedRawFunctionOwner.Medal:
                case PacketOwnedRawFunctionOwner.ItemPot:
                case PacketOwnedRawFunctionOwner.MagicWheel:
                    TogglePacketOwnedRawFunctionOwnerWindow(owner, clientFunctionId);
                    return true;
                default:
                    break;
            }

            switch (ResolvePacketOwnedRawChatOwner(clientFunctionId))
            {
                case PacketOwnedRawChatOwner.All:
                    _chat.ActivateTarget(MapSimulatorChatTargetType.All, currentTime);
                    return true;
                case PacketOwnedRawChatOwner.WhisperTargetPicker:
                    _chat.OpenWhisperTargetPicker(
                        currentTime,
                        presentation: MapSimulatorChat.WhisperTargetPickerPresentation.Modal);
                    return true;
                case PacketOwnedRawChatOwner.Party:
                    _chat.ActivateTarget(MapSimulatorChatTargetType.Party, currentTime);
                    return true;
                case PacketOwnedRawChatOwner.Friend:
                    _chat.ActivateTarget(MapSimulatorChatTargetType.Friend, currentTime);
                    return true;
                case PacketOwnedRawChatOwner.Guild:
                    _chat.ActivateTarget(MapSimulatorChatTargetType.Guild, currentTime);
                    return true;
                case PacketOwnedRawChatOwner.Alliance:
                    _chat.ActivateTarget(MapSimulatorChatTargetType.Association, currentTime);
                    return true;
                case PacketOwnedRawChatOwner.Expedition:
                    _chat.ActivateTarget(MapSimulatorChatTargetType.Expedition, currentTime);
                    return true;
                default:
                    return false;
            }
        }

        private void TogglePacketOwnedRawUtilityWindow(string windowName, Action showAction)
        {
            UIWindowBase window = uiWindowManager?.GetWindow(windowName);
            if (window?.IsVisible == true)
            {
                uiWindowManager.HideWindow(windowName);
                return;
            }

            showAction?.Invoke();
        }

        private void TogglePacketOwnedRawFunctionOwnerWindow(PacketOwnedRawFunctionOwner owner, int clientFunctionId)
        {
            if (!TryResolvePacketOwnedRawFunctionOwnerWindowName(owner, out string windowName))
            {
                return;
            }

            TogglePacketOwnedRawUtilityWindow(windowName, () =>
                ShowUtilityWindow(windowName, $"packet-owned-funckey:{clientFunctionId.ToString(CultureInfo.InvariantCulture)}"));
        }

        private bool TryUsePacketOwnedFuncKeyItem(int itemId, int currentTime)
        {
            InventoryType inventoryType = ResolvePacketOwnedHotkeyInventoryType(itemId);
            if (inventoryType == InventoryType.NONE)
            {
                return false;
            }

            if (TryRejectOnlyPickupInventoryUse(itemId, inventoryType, currentTime))
            {
                return false;
            }

            return TryUseConsumableInventoryItem(itemId, inventoryType, currentTime);
        }

        private void ShowPacketOwnedProfessionWindow()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemMaker) is ItemMakerUI itemMakerWindow)
            {
                ConfigureItemMakerWindow(itemMakerWindow);
                itemMakerWindow.ApplyLaunchContext("Packet-owned profession key");
            }

            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.ItemMaker);
        }

        private void ShowPacketOwnedFamilyWindow()
        {
            _familyChartRuntime.ClearRemotePreviewRequest();
            ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.FamilyChart);
        }

        private void ShowPacketOwnedExpeditionSearchWindow()
        {
            _socialListRuntime.OpenSearchWindow(SocialSearchTab.Expedition);
            WireSocialSearchWindowData();
            ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.SocialSearch);
        }

        private void ShowPacketOwnedCashShopWindow()
        {
            OpenCashServiceOwnerFamily(UI.CashServiceStageKind.CashShop, resetStageSession: false);
        }

        private void OpenPacketOwnedSpouseWhisperOwner(int currentTime)
        {
            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            if (localCharacterId > 0
                && _remoteUserPool.TryResolveRelationshipWhisperTarget(
                    localCharacterId,
                    RemoteRelationshipOverlayType.Marriage,
                    out string whisperTarget))
            {
                _chat.BeginWhisperTo(whisperTarget, currentTime);
                return;
            }

            if (localCharacterId > 0
                && _remoteUserPool.TryResolveRelationshipWhisperTarget(
                    localCharacterId,
                    RemoteRelationshipOverlayType.Couple,
                    out whisperTarget))
            {
                _chat.BeginWhisperTo(whisperTarget, currentTime);
                return;
            }

            _chat.OpenWhisperTargetPicker(
                currentTime,
                presentation: MapSimulatorChat.WhisperTargetPickerPresentation.Modal);
        }

        private void TogglePacketOwnedSitOwner()
        {
            if (_playerManager?.Player is not PlayerCharacter player
                || !player.IsAlive)
            {
                return;
            }

            if (player.Build?.ActivePortableChair != null
                || player.State == PlayerState.Sitting)
            {
                player.ApplyPacketOwnedChairStandCorrection();
                return;
            }

            if (player.Physics?.IsOnFoothold() != true)
            {
                return;
            }

            player.ApplyPacketOwnedSitPlacement(player.X, player.Y);
        }

        private static bool WasPacketOwnedFuncKeyPressed(KeyboardState keyboardState, KeyboardState previousKeyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !previousKeyboardState.IsKeyDown(key);
        }

        private static bool WasPacketOwnedFuncKeyReleased(KeyboardState keyboardState, KeyboardState previousKeyboardState, Keys key)
        {
            return !keyboardState.IsKeyDown(key) && previousKeyboardState.IsKeyDown(key);
        }

        internal static bool ShouldHandlePacketOwnedCastEntryViaRawRuntime(
            PlayerInput input,
            Keys key,
            bool handledByLiveHotkeyBinding)
        {
            return !handledByLiveHotkeyBinding
                && key != Keys.None
                && !IsPacketOwnedHotkeyKeyProtected(input, key);
        }

        internal static bool TryResolvePacketOwnedKnownFunctionBinding(InputAction action, out int clientFunctionId)
        {
            for (int i = 0; i < PacketOwnedKnownFunctionBindings.Length; i++)
            {
                (int candidateClientFunctionId, InputAction candidateAction) = PacketOwnedKnownFunctionBindings[i];
                if (candidateAction == action)
                {
                    clientFunctionId = candidateClientFunctionId;
                    return true;
                }
            }

            clientFunctionId = -1;
            return false;
        }

        internal static bool IsPacketOwnedKnownFunctionId(int clientFunctionId)
        {
            for (int i = 0; i < PacketOwnedKnownFunctionBindings.Length; i++)
            {
                if (PacketOwnedKnownFunctionBindings[i].ClientFunctionId == clientFunctionId)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsPacketOwnedLiveInputBindingEntry(byte entryType, int entryId, int clientFunctionId)
        {
            if (entryId != clientFunctionId)
            {
                return false;
            }

            return entryType == PacketOwnedFuncKeyFunctionType
                || entryType == PacketOwnedFuncKeyClientControlType && IsPacketOwnedClientControlFunctionId(clientFunctionId);
        }

        internal static bool IsPacketOwnedClientControlFunctionId(int clientFunctionId)
        {
            // CUIKeyConfig::ResetPaletteItems seeds palette slots 30-34 as type 5 ids 50-54.
            return clientFunctionId >= 50 && clientFunctionId <= 54;
        }

        internal static bool TryResolvePacketOwnedEmotionBinding(int clientEmotionId, out int emotionId)
        {
            for (int i = 0; i < PacketOwnedKnownEmotionBindings.Length; i++)
            {
                if (PacketOwnedKnownEmotionBindings[i].ClientEmotionId == clientEmotionId)
                {
                    emotionId = PacketOwnedKnownEmotionBindings[i].EmotionId;
                    return true;
                }
            }

            emotionId = -1;
            return false;
        }

        internal static PacketOwnedRawFunctionOwner ResolvePacketOwnedRawFunctionOwner(int clientFunctionId)
        {
            // IDA v95:
            // - CUIKeyConfig::ResetPaletteItems(0x7d8bc0) keeps palette slots 0-29 as type-4 ids 0-29.
            // - CUIKeyConfig::GetIdxFromPaletteSlot(0x7d83b0) only remaps slots 30-34 to 50-54
            //   and slots 35-41 to 100-106.
            // Keep raw function ownership aligned to those client ids, but only route ids that
            // already have a simulator-owned surface in this owner family.
            return clientFunctionId switch
            {
                4 => PacketOwnedRawFunctionOwner.SocialListFriend,
                5 => PacketOwnedRawFunctionOwner.WorldMap,
                6 => PacketOwnedRawFunctionOwner.Messenger,
                17 => PacketOwnedRawFunctionOwner.SocialListGuild,
                19 => PacketOwnedRawFunctionOwner.SocialListParty,
                20 => PacketOwnedRawFunctionOwner.QuestAlarm,
                51 => PacketOwnedRawFunctionOwner.Sit,
                14 => PacketOwnedRawFunctionOwner.ShortcutMenu,
                24 => PacketOwnedRawFunctionOwner.PartySearch,
                25 => PacketOwnedRawFunctionOwner.Family,
                26 => PacketOwnedRawFunctionOwner.Medal,
                21 => PacketOwnedRawFunctionOwner.SpouseWhisper,
                22 => PacketOwnedRawFunctionOwner.CashShop,
                27 => PacketOwnedRawFunctionOwner.Expedition,
                29 => PacketOwnedRawFunctionOwner.Profession,
                30 => PacketOwnedRawFunctionOwner.ItemPot,
                31 => PacketOwnedRawFunctionOwner.Event,
                32 => PacketOwnedRawFunctionOwner.MagicWheel,
                _ => PacketOwnedRawFunctionOwner.None,
            };
        }

        internal static bool TryResolvePacketOwnedRawFunctionOwnerWindowName(PacketOwnedRawFunctionOwner owner, out string windowName)
        {
            if (TryResolvePacketOwnedRawFunctionOwnerWindowRoute(owner, out PacketOwnedRawFunctionOwnerWindowRoute route))
            {
                windowName = route.WindowName;
                return !string.IsNullOrEmpty(windowName);
            }

            windowName = null;
            return false;
        }

        internal static bool TryResolvePacketOwnedRawFunctionOwnerWindowRoute(
            PacketOwnedRawFunctionOwner owner,
            out PacketOwnedRawFunctionOwnerWindowRoute route)
        {
            for (int i = 0; i < PacketOwnedRawFunctionOwnerWindowRoutes.Length; i++)
            {
                if (PacketOwnedRawFunctionOwnerWindowRoutes[i].Owner == owner)
                {
                    route = PacketOwnedRawFunctionOwnerWindowRoutes[i];
                    return true;
                }
            }

            route = default;
            return false;
        }

        internal static PacketOwnedRawChatOwner ResolvePacketOwnedRawChatOwner(int clientFunctionId)
        {
            return clientFunctionId switch
            {
                10 => PacketOwnedRawChatOwner.All,
                11 => PacketOwnedRawChatOwner.WhisperTargetPicker,
                12 => PacketOwnedRawChatOwner.Party,
                13 => PacketOwnedRawChatOwner.Friend,
                18 => PacketOwnedRawChatOwner.Guild,
                23 => PacketOwnedRawChatOwner.Alliance,
                28 => PacketOwnedRawChatOwner.Expedition,
                _ => PacketOwnedRawChatOwner.None,
            };
        }

        private static int ComposePacketOwnedFuncKeyInputToken(int scanCode)
        {
            return 0x50000000 | (scanCode & 0xFFFF);
        }

        private bool TryResolvePacketOwnedBindableHotkeySlot(
            PlayerInput input,
            Keys key,
            IReadOnlyDictionary<Keys, int> existingBindableHotkeySlotIndicesByKey,
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

            if (existingBindableHotkeySlotIndicesByKey != null
                && existingBindableHotkeySlotIndicesByKey.TryGetValue(key, out int existingSlotIndex)
                && existingSlotIndex >= 0
                && existingSlotIndex < _packetOwnedBindableHotkeyAssignedScanCodes.Length
                && _packetOwnedBindableHotkeyAssignedScanCodes[existingSlotIndex] < 0)
            {
                slot = PacketOwnedBindableHotkeySlots[existingSlotIndex];
                return true;
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

        private bool TryResolvePacketOwnedActiveCastInputOwner(Keys key, out PacketOwnedCastInputOwner owner)
        {
            owner = default;
            if (key == Keys.None
                || !_packetOwnedCastInputOwnersByKey.TryGetValue(key, out List<PacketOwnedCastInputOwner> owners)
                || owners == null
                || owners.Count == 0)
            {
                return false;
            }

            int ownerIndex = ResolvePacketOwnedActiveCastOwnerIndex(owners);
            if (ownerIndex < 0 || ownerIndex >= owners.Count)
            {
                return false;
            }

            owner = owners[ownerIndex];
            return true;
        }

        private bool TryResolvePacketOwnedHeldRawCastInputOwner(Keys key, out PacketOwnedCastInputOwner owner)
        {
            if (key != Keys.None
                && _packetOwnedHeldRawCastInputOwnersByKey.TryGetValue(key, out owner))
            {
                return true;
            }

            owner = default;
            return false;
        }

        private void ReleasePacketOwnedHeldRawCastInputOwnersOnKeyUp(
            SkillManager skills,
            int currentTime,
            KeyboardState keyboardState,
            KeyboardState previousKeyboardState)
        {
            if (skills == null || _packetOwnedHeldRawCastInputOwnersByKey.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<Keys, PacketOwnedCastInputOwner> heldOwnerEntry in _packetOwnedHeldRawCastInputOwnersByKey.ToArray())
            {
                Keys key = heldOwnerEntry.Key;
                if (key == Keys.None
                    || !WasPacketOwnedFuncKeyReleased(keyboardState, previousKeyboardState, key))
                {
                    continue;
                }

                PacketOwnedCastInputOwner owner = heldOwnerEntry.Value;
                if (owner.Entry.Type == PacketOwnedFuncKeySkillType && owner.Entry.Id > 0)
                {
                    int ownerInputToken = ComposePacketOwnedFuncKeyInputToken(owner.ScanCode);
                    skills.ReleasePacketOwnedFuncKeySkillIfActive(owner.Entry.Id, currentTime, ownerInputToken);
                }

                _packetOwnedHeldRawCastInputOwnersByKey.Remove(key);
            }
        }

        private void RegisterPacketOwnedCastInputOwner(
            int scanCode,
            Keys key,
            PacketOwnedFuncKeyMappedEntry entry,
            int bindableSlotIndex,
            PlayerInput input)
        {
            bool handledByLiveHotkeyBinding = bindableSlotIndex >= 0;
            if (scanCode < 0
                || key == Keys.None
                || (!handledByLiveHotkeyBinding
                    && !ShouldHandlePacketOwnedCastEntryViaRawRuntime(input, key, handledByLiveHotkeyBinding: false)))
            {
                return;
            }

            if (!_packetOwnedCastInputOwnersByKey.TryGetValue(key, out List<PacketOwnedCastInputOwner> owners))
            {
                owners = new List<PacketOwnedCastInputOwner>();
                _packetOwnedCastInputOwnersByKey[key] = owners;
            }

            for (int i = 0; i < owners.Count; i++)
            {
                if (owners[i].ScanCode == scanCode)
                {
                    return;
                }
            }

            owners.Add(new PacketOwnedCastInputOwner(scanCode, entry, bindableSlotIndex));
        }

        internal static bool TryRegisterPacketOwnedCastInputOwner(
            IDictionary<Keys, List<int>> ownersByKey,
            Keys key,
            int scanCode)
        {
            if (ownersByKey == null
                || key == Keys.None
                || scanCode < 0)
            {
                return false;
            }

            if (!ownersByKey.TryGetValue(key, out List<int> owners))
            {
                owners = new List<int>();
                ownersByKey[key] = owners;
            }

            if (owners.Contains(scanCode))
            {
                return false;
            }

            owners.Add(scanCode);
            return true;
        }

        internal static int ResolvePacketOwnedActiveCastOwnerIndex(IReadOnlyList<bool> handledByLiveHotkeyBindings)
        {
            if (handledByLiveHotkeyBindings == null || handledByLiveHotkeyBindings.Count == 0)
            {
                return -1;
            }

            for (int i = handledByLiveHotkeyBindings.Count - 1; i >= 0; i--)
            {
                if (handledByLiveHotkeyBindings[i])
                {
                    return i;
                }
            }

            return handledByLiveHotkeyBindings.Count - 1;
        }

        private static int ResolvePacketOwnedActiveCastOwnerIndex(IReadOnlyList<PacketOwnedCastInputOwner> owners)
        {
            if (owners == null || owners.Count == 0)
            {
                return -1;
            }

            for (int i = owners.Count - 1; i >= 0; i--)
            {
                if (owners[i].IsHandledByLiveHotkeyBinding)
                {
                    return i;
                }
            }

            return owners.Count - 1;
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

        private static IReadOnlyDictionary<Keys, int> BuildPacketOwnedExistingBindableHotkeySlotIndicesByKey(PlayerInput input)
        {
            var slots = new Dictionary<Keys, int>();
            if (input == null)
            {
                return slots;
            }

            for (int i = 0; i < PacketOwnedBindableHotkeySlots.Length; i++)
            {
                KeyBinding binding = input.GetBinding(PacketOwnedBindableHotkeySlots[i].Action);
                if (binding == null)
                {
                    continue;
                }

                RegisterPacketOwnedBindableHotkeySlotIndex(slots, binding.PrimaryKey, i);
                RegisterPacketOwnedBindableHotkeySlotIndex(slots, binding.SecondaryKey, i);
            }

            return slots;
        }

        private static void RegisterPacketOwnedBindableHotkeySlotIndex(Dictionary<Keys, int> slots, Keys key, int slotIndex)
        {
            if (slots == null
                || key == Keys.None
                || slots.ContainsKey(key))
            {
                return;
            }

            slots[key] = slotIndex;
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

        internal static bool IsPacketOwnedHotkeyKeyProtected(PlayerInput input, Keys key)
        {
            if (IsPacketOwnedReservedPhysicalKey(key))
            {
                return true;
            }

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

        internal static bool IsPacketOwnedReservedPhysicalKey(Keys key)
        {
            // The client keeps Escape ownership outside the packet-owned cast remap, even if local bindings move.
            return key == Keys.Escape;
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

        private static bool TryResolvePacketOwnedKeyScanCode(Keys key, out int scanCode)
        {
            scanCode = -1;
            if (key == Keys.None)
            {
                return false;
            }

            uint mappedScanCode = MapVirtualKey((uint)key, 0);
            if (mappedScanCode == 0 || mappedScanCode >= PacketOwnedFuncKeyEntryCount)
            {
                return false;
            }

            scanCode = (int)mappedScanCode;
            return true;
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

        internal static List<PacketOwnedFuncKeyConfigStore.FuncKeyMappedRecord> ResolvePersistedPacketOwnedFallbackFuncKeyMappedRecords(
            PlayerInput input,
            IReadOnlyList<PacketOwnedFuncKeyConfigStore.FuncKeyMappedRecord> existingRecords,
            bool persistSimulatorFallback)
        {
            if (persistSimulatorFallback)
            {
                return CreatePersistedPacketOwnedFallbackFuncKeyMappedRecords(input);
            }

            if (existingRecords?.Count > 0)
            {
                return ClonePacketOwnedFuncKeyMappedRecords(existingRecords);
            }

            return CreatePersistedPacketOwnedFallbackFuncKeyMappedRecords(input);
        }

        internal static List<PacketOwnedFuncKeyConfigStore.FuncKeyMappedRecord> CreatePersistedPacketOwnedFallbackFuncKeyMappedRecords(PlayerInput input)
        {
            PacketOwnedFuncKeyMappedEntry[] persistedMap = CreateEmptyPacketOwnedFuncKeyMap();
            PopulatePacketOwnedFuncKeyMappedEntriesFromSimulatorBindings(input, persistedMap);
            return CreatePacketOwnedFuncKeyMappedRecords(persistedMap);
        }

        private static void PopulatePacketOwnedFuncKeyMappedEntriesFromSimulatorBindings(
            PlayerInput input,
            PacketOwnedFuncKeyMappedEntry[] destination)
        {
            if (destination == null)
            {
                return;
            }

            Array.Clear(destination, 0, destination.Length);
            if (input == null)
            {
                return;
            }

            for (int i = 0; i < PacketOwnedKnownFunctionBindings.Length; i++)
            {
                (int clientFunctionId, InputAction action) = PacketOwnedKnownFunctionBindings[i];
                KeyBinding binding = input.GetBinding(action);
                if (binding == null)
                {
                    continue;
                }

                if (TrySetPacketOwnedFuncKeyMappedEntryFromSimulatorBinding(destination, binding.PrimaryKey, clientFunctionId))
                {
                    continue;
                }

                TrySetPacketOwnedFuncKeyMappedEntryFromSimulatorBinding(destination, binding.SecondaryKey, clientFunctionId);
            }
        }

        private static List<PacketOwnedFuncKeyConfigStore.FuncKeyMappedRecord> CreatePacketOwnedFuncKeyMappedRecords(
            IReadOnlyList<PacketOwnedFuncKeyMappedEntry> entries)
        {
            var records = new List<PacketOwnedFuncKeyConfigStore.FuncKeyMappedRecord>(PacketOwnedFuncKeyEntryCount);
            for (int i = 0; i < PacketOwnedFuncKeyEntryCount; i++)
            {
                PacketOwnedFuncKeyMappedEntry entry = entries != null && i < entries.Count
                    ? entries[i]
                    : default;
                records.Add(new PacketOwnedFuncKeyConfigStore.FuncKeyMappedRecord
                {
                    Type = entry.Type,
                    Id = entry.Id
                });
            }

            return records;
        }

        private static List<PacketOwnedFuncKeyConfigStore.FuncKeyMappedRecord> ClonePacketOwnedFuncKeyMappedRecords(
            IReadOnlyList<PacketOwnedFuncKeyConfigStore.FuncKeyMappedRecord> records)
        {
            var clones = new List<PacketOwnedFuncKeyConfigStore.FuncKeyMappedRecord>(PacketOwnedFuncKeyEntryCount);
            if (records == null)
            {
                return clones;
            }

            int count = Math.Min(records.Count, PacketOwnedFuncKeyEntryCount);
            for (int i = 0; i < count; i++)
            {
                PacketOwnedFuncKeyConfigStore.FuncKeyMappedRecord record = records[i];
                clones.Add(new PacketOwnedFuncKeyConfigStore.FuncKeyMappedRecord
                {
                    Type = record?.Type ?? 0,
                    Id = record?.Id ?? 0
                });
            }

            while (clones.Count < PacketOwnedFuncKeyEntryCount)
            {
                clones.Add(new PacketOwnedFuncKeyConfigStore.FuncKeyMappedRecord());
            }

            return clones;
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
