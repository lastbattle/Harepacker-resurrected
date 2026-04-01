using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum WeddingWishListDialogMode
    {
        None,
        Receive,
        Give,
        Input
    }

    internal enum WeddingWishListRole
    {
        Groom,
        Bride
    }

    internal enum WeddingWishListSelectionPane
    {
        GiftList,
        Inventory,
        WishList,
        Candidate
    }

    internal sealed class WeddingWishListRuntime
    {
        private static readonly InventoryType[] TabInventoryTypes =
        {
            InventoryType.EQUIP,
            InventoryType.USE,
            InventoryType.SETUP,
            InventoryType.ETC,
            InventoryType.CASH
        };

        private static readonly int[] FallbackGiftItemIds =
        {
            1002140,
            2240000,
            3010002,
            4210000,
            5150040
        };

        private readonly Dictionary<int, List<InventorySlotData>> _giftListByTab = new();
        private readonly List<InventorySlotData> _wishListEntries = new();
        private readonly List<InventorySlotData> _candidateEntries = new();

        private IInventoryRuntime _inventory;
        private string _localCharacterName = "Player";
        private WeddingWishListDialogMode _mode;
        private WeddingWishListRole _role = WeddingWishListRole.Groom;
        private WeddingWishListSelectionPane _activePane = WeddingWishListSelectionPane.GiftList;
        private int _selectedTabIndex;
        private int _selectedGiftIndex;
        private int _selectedInventoryIndex;
        private int _selectedWishIndex;
        private int _selectedCandidateIndex;
        private bool _isOpen;
        private string _statusMessage = "Wedding wish-list dialog is idle.";
        private bool _seeded;

        internal void UpdateLocalContext(CharacterBuild build)
        {
            if (!string.IsNullOrWhiteSpace(build?.Name))
            {
                _localCharacterName = build.Name.Trim();
            }

            if (build != null)
            {
                _role = build.Gender == CharacterGender.Female
                    ? WeddingWishListRole.Bride
                    : WeddingWishListRole.Groom;
            }
        }

        internal void BindInventory(IInventoryRuntime inventory)
        {
            _inventory = inventory;
            RefreshCandidateEntries();
            EnsureSeedData();
            ClampSelections();
        }

        internal string Open(
            WeddingWishListDialogMode mode,
            WeddingWishListRole? roleOverride = null)
        {
            if (mode == WeddingWishListDialogMode.None)
            {
                mode = WeddingWishListDialogMode.Receive;
            }

            if (roleOverride.HasValue)
            {
                _role = roleOverride.Value;
            }

            EnsureSeedData();
            RefreshCandidateEntries();

            _mode = mode;
            _isOpen = true;
            _activePane = mode switch
            {
                WeddingWishListDialogMode.Receive => WeddingWishListSelectionPane.GiftList,
                WeddingWishListDialogMode.Give => WeddingWishListSelectionPane.Inventory,
                WeddingWishListDialogMode.Input => WeddingWishListSelectionPane.WishList,
                _ => WeddingWishListSelectionPane.GiftList
            };

            ClampSelections();
            _statusMessage = $"Opened wedding wish-list owner in {mode.ToString().ToLowerInvariant()} mode as {ResolveRoleLabel(_role)}.";
            return _statusMessage;
        }

        internal string SetMode(WeddingWishListDialogMode mode)
        {
            if (!_isOpen)
            {
                return Open(mode);
            }

            _mode = mode == WeddingWishListDialogMode.None ? WeddingWishListDialogMode.Receive : mode;
            _activePane = _mode switch
            {
                WeddingWishListDialogMode.Receive => WeddingWishListSelectionPane.GiftList,
                WeddingWishListDialogMode.Give => WeddingWishListSelectionPane.Inventory,
                WeddingWishListDialogMode.Input => WeddingWishListSelectionPane.WishList,
                _ => WeddingWishListSelectionPane.GiftList
            };

            ClampSelections();
            _statusMessage = $"Switched wedding wish-list owner to {_mode.ToString().ToLowerInvariant()} mode.";
            return _statusMessage;
        }

        internal string SetTab(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex >= TabInventoryTypes.Length)
            {
                return $"Wedding wish-list tab must be between 0 and {TabInventoryTypes.Length - 1}.";
            }

            _selectedTabIndex = tabIndex;
            ClampSelections();
            _statusMessage = $"Selected {ResolveTabLabel(_selectedTabIndex)} tab for the wedding wish-list dialog.";
            return _statusMessage;
        }

        internal string SetActivePane(WeddingWishListSelectionPane pane)
        {
            _activePane = pane;
            ClampSelections();
            _statusMessage = $"Focused {ResolvePaneLabel(pane)} in the wedding wish-list dialog.";
            return _statusMessage;
        }

        internal string SelectEntry(WeddingWishListSelectionPane pane, int index)
        {
            switch (pane)
            {
                case WeddingWishListSelectionPane.GiftList:
                    _selectedGiftIndex = index;
                    break;
                case WeddingWishListSelectionPane.Inventory:
                    _selectedInventoryIndex = index;
                    break;
                case WeddingWishListSelectionPane.WishList:
                    _selectedWishIndex = index;
                    break;
                case WeddingWishListSelectionPane.Candidate:
                    _selectedCandidateIndex = index;
                    break;
            }

            _activePane = pane;
            ClampSelections();
            _statusMessage = $"Selected {ResolvePaneLabel(pane)} entry {Math.Max(0, index) + 1}.";
            return _statusMessage;
        }

        internal string MoveSelection(int delta)
        {
            switch (_activePane)
            {
                case WeddingWishListSelectionPane.GiftList:
                    _selectedGiftIndex += delta;
                    break;
                case WeddingWishListSelectionPane.Inventory:
                    _selectedInventoryIndex += delta;
                    break;
                case WeddingWishListSelectionPane.WishList:
                    _selectedWishIndex += delta;
                    break;
                case WeddingWishListSelectionPane.Candidate:
                    _selectedCandidateIndex += delta;
                    break;
            }

            ClampSelections();
            _statusMessage = $"Moved selection in {ResolvePaneLabel(_activePane)}.";
            return _statusMessage;
        }

        internal string TryPutSelectedItem()
        {
            if (_mode != WeddingWishListDialogMode.Give)
            {
                return "Wedding wish-list Put is only available in give mode.";
            }

            InventorySlotData source = GetSelectedInventoryEntry();
            if (source == null)
            {
                return "Select a My Items row before pressing Put.";
            }

            InventoryType type = ResolveInventoryTypeForSlot(source, ResolveSelectedInventoryType());
            if (_inventory == null || !_inventory.TryConsumeItem(type, source.ItemId, 1))
            {
                return $"Unable to move {ResolveItemLabel(source)} out of the local inventory.";
            }

            InventorySlotData gifted = CloneForDialog(source, type, 1);
            _giftListByTab[_selectedTabIndex].Add(gifted);
            RefreshCandidateEntries();
            ClampSelections();
            _statusMessage = $"Moved {ResolveItemLabel(gifted)} into the wedding gift list.";
            return _statusMessage;
        }

        internal string TryGetSelectedItem()
        {
            if (_mode != WeddingWishListDialogMode.Receive)
            {
                return "Wedding wish-list Get is only available in receive mode.";
            }

            List<InventorySlotData> giftList = GetGiftListForSelectedTab();
            InventorySlotData selected = _selectedGiftIndex >= 0 && _selectedGiftIndex < giftList.Count
                ? giftList[_selectedGiftIndex]
                : null;
            if (selected == null)
            {
                return "Select a wedding gift list row before pressing Get.";
            }

            InventoryType type = ResolveInventoryTypeForSlot(selected, ResolveSelectedInventoryType());
            if (_inventory == null || !_inventory.CanAcceptItem(type, selected.ItemId, 1, selected.MaxStackSize))
            {
                return $"Unable to return {ResolveItemLabel(selected)} to the local inventory.";
            }

            _inventory.AddItem(type, CloneForDialog(selected, type, 1));
            giftList.RemoveAt(_selectedGiftIndex);
            RefreshCandidateEntries();
            ClampSelections();
            _statusMessage = $"Claimed {ResolveItemLabel(selected)} from the wedding gift list.";
            return _statusMessage;
        }

        internal string TryAddCandidateWish()
        {
            if (_mode != WeddingWishListDialogMode.Input)
            {
                return "Wedding wish-list Enter is only available in input mode.";
            }

            InventorySlotData selected = _selectedCandidateIndex >= 0 && _selectedCandidateIndex < _candidateEntries.Count
                ? _candidateEntries[_selectedCandidateIndex]
                : null;
            if (selected == null)
            {
                return "No candidate item is available to insert into the wedding wish list.";
            }

            if (_wishListEntries.Any(entry => entry.ItemId == selected.ItemId))
            {
                return $"{ResolveItemLabel(selected)} is already present in the wedding wish list.";
            }

            _wishListEntries.Add(CloneForDialog(selected, ResolveInventoryTypeForSlot(selected), 1));
            ClampSelections();
            _statusMessage = $"Inserted {ResolveItemLabel(selected)} into the wedding wish list.";
            return _statusMessage;
        }

        internal string TryDeleteWish()
        {
            if (_mode != WeddingWishListDialogMode.Input)
            {
                return "Wedding wish-list Delete is only available in input mode.";
            }

            if (_selectedWishIndex < 0 || _selectedWishIndex >= _wishListEntries.Count)
            {
                return "Select a wedding wish-list row before pressing Delete.";
            }

            InventorySlotData removed = _wishListEntries[_selectedWishIndex];
            _wishListEntries.RemoveAt(_selectedWishIndex);
            ClampSelections();
            _statusMessage = $"Removed {ResolveItemLabel(removed)} from the wedding wish list.";
            return _statusMessage;
        }

        internal string Close()
        {
            _isOpen = false;
            _statusMessage = "Closed the wedding wish-list dialog.";
            return _statusMessage;
        }

        internal string ConfirmInput()
        {
            if (!_isOpen)
            {
                return "Wedding wish-list dialog is not open.";
            }

            _isOpen = false;
            _statusMessage = $"Confirmed {_wishListEntries.Count} wedding wish-list item(s) through the dedicated OK/SetRet owner path. Downstream packet or script handoff is still not modeled.";
            return _statusMessage;
        }

        internal string Clear()
        {
            _giftListByTab.Clear();
            _wishListEntries.Clear();
            _candidateEntries.Clear();
            _seeded = false;
            _isOpen = false;
            _mode = WeddingWishListDialogMode.None;
            _selectedGiftIndex = 0;
            _selectedInventoryIndex = 0;
            _selectedWishIndex = 0;
            _selectedCandidateIndex = 0;
            _statusMessage = "Cleared wedding wish-list dialog state.";
            return _statusMessage;
        }

        internal WeddingWishListSnapshot BuildSnapshot()
        {
            EnsureSeedData();
            RefreshCandidateEntries();
            ClampSelections();

            return new WeddingWishListSnapshot
            {
                IsOpen = _isOpen,
                Mode = _mode,
                Role = _role,
                ActivePane = _activePane,
                SelectedTabIndex = _selectedTabIndex,
                TabLabels = Array.AsReadOnly(new[]
                {
                    "Equip",
                    "Use",
                    "Setup",
                    "Etc",
                    "Cash"
                }),
                GiftEntries = new ReadOnlyCollection<InventorySlotData>(GetGiftListForSelectedTab().Select(CloneForDialog).ToList()),
                InventoryEntries = new ReadOnlyCollection<InventorySlotData>(GetInventoryEntriesForSelectedTab().Select(CloneForDialog).ToList()),
                WishEntries = new ReadOnlyCollection<InventorySlotData>(_wishListEntries.Select(CloneForDialog).ToList()),
                CandidateEntries = new ReadOnlyCollection<InventorySlotData>(_candidateEntries.Select(CloneForDialog).ToList()),
                SelectedGiftIndex = _selectedGiftIndex,
                SelectedInventoryIndex = _selectedInventoryIndex,
                SelectedWishIndex = _selectedWishIndex,
                SelectedCandidateIndex = _selectedCandidateIndex,
                StatusMessage = _statusMessage,
                LocalCharacterName = _localCharacterName
            };
        }

        internal string DescribeStatus()
        {
            WeddingWishListSnapshot snapshot = BuildSnapshot();
            return $"Wedding wish-list owner: open={snapshot.IsOpen}, mode={snapshot.Mode}, role={snapshot.Role}, tab={ResolveTabLabel(snapshot.SelectedTabIndex)}, gifts={snapshot.GiftEntries.Count}, inventory={snapshot.InventoryEntries.Count}, wishes={snapshot.WishEntries.Count}. {snapshot.StatusMessage}";
        }

        private void EnsureSeedData()
        {
            if (_seeded)
            {
                return;
            }

            for (int i = 0; i < TabInventoryTypes.Length; i++)
            {
                _giftListByTab[i] = new List<InventorySlotData>();
            }

            List<InventorySlotData> inventorySeed = EnumerateInventorySlots();
            for (int i = 0; i < TabInventoryTypes.Length; i++)
            {
                InventoryType type = TabInventoryTypes[i];
                InventorySlotData inventoryItem = inventorySeed.FirstOrDefault(slot => ResolveInventoryTypeForSlot(slot) == type);
                if (inventoryItem != null)
                {
                    _giftListByTab[i].Add(CloneForDialog(inventoryItem, type, 1));
                    if (_wishListEntries.Count < 4)
                    {
                        _wishListEntries.Add(CloneForDialog(inventoryItem, type, 1));
                    }

                    continue;
                }

                int fallbackItemId = FallbackGiftItemIds[Math.Min(i, FallbackGiftItemIds.Length - 1)];
                _giftListByTab[i].Add(CreateFallbackSlot(fallbackItemId, type));
                if (_wishListEntries.Count < 4)
                {
                    _wishListEntries.Add(CreateFallbackSlot(fallbackItemId, type));
                }
            }

            _seeded = true;
        }

        private void RefreshCandidateEntries()
        {
            _candidateEntries.Clear();
            foreach (InventorySlotData slot in EnumerateInventorySlots())
            {
                _candidateEntries.Add(CloneForDialog(slot, ResolveInventoryTypeForSlot(slot), 1));
            }

            if (_candidateEntries.Count == 0)
            {
                for (int i = 0; i < FallbackGiftItemIds.Length; i++)
                {
                    InventoryType type = TabInventoryTypes[Math.Min(i, TabInventoryTypes.Length - 1)];
                    _candidateEntries.Add(CreateFallbackSlot(FallbackGiftItemIds[i], type));
                }
            }
        }

        private List<InventorySlotData> GetGiftListForSelectedTab()
        {
            if (!_giftListByTab.TryGetValue(_selectedTabIndex, out List<InventorySlotData> list))
            {
                list = new List<InventorySlotData>();
                _giftListByTab[_selectedTabIndex] = list;
            }

            return list;
        }

        private List<InventorySlotData> GetInventoryEntriesForSelectedTab()
        {
            InventoryType type = ResolveSelectedInventoryType();
            return EnumerateInventorySlots(type);
        }

        private InventorySlotData GetSelectedInventoryEntry()
        {
            List<InventorySlotData> inventoryEntries = GetInventoryEntriesForSelectedTab();
            return _selectedInventoryIndex >= 0 && _selectedInventoryIndex < inventoryEntries.Count
                ? inventoryEntries[_selectedInventoryIndex]
                : null;
        }

        private List<InventorySlotData> EnumerateInventorySlots(InventoryType? typeFilter = null)
        {
            var entries = new List<InventorySlotData>();
            if (_inventory == null)
            {
                return entries;
            }

            foreach (InventoryType type in TabInventoryTypes)
            {
                if (typeFilter.HasValue && typeFilter.Value != type)
                {
                    continue;
                }

                IReadOnlyList<InventorySlotData> slots = _inventory.GetSlots(type);
                for (int i = 0; i < slots.Count; i++)
                {
                    InventorySlotData slot = slots[i];
                    if (slot == null)
                    {
                        continue;
                    }

                    entries.Add(CloneForDialog(slot, type, slot.Quantity));
                }
            }

            return entries
                .OrderBy(slot => ResolveItemLabel(slot), StringComparer.OrdinalIgnoreCase)
                .ThenBy(slot => slot.ItemId)
                .ToList();
        }

        private void ClampSelections()
        {
            _selectedTabIndex = Math.Clamp(_selectedTabIndex, 0, TabInventoryTypes.Length - 1);
            _selectedGiftIndex = ClampIndex(_selectedGiftIndex, GetGiftListForSelectedTab().Count);
            _selectedInventoryIndex = ClampIndex(_selectedInventoryIndex, GetInventoryEntriesForSelectedTab().Count);
            _selectedWishIndex = ClampIndex(_selectedWishIndex, _wishListEntries.Count);
            _selectedCandidateIndex = ClampIndex(_selectedCandidateIndex, _candidateEntries.Count);
        }

        private static int ClampIndex(int index, int count)
        {
            return count <= 0 ? 0 : Math.Clamp(index, 0, count - 1);
        }

        private static InventorySlotData CloneForDialog(InventorySlotData slot)
        {
            return CloneForDialog(slot, ResolveInventoryTypeForSlot(slot), slot?.Quantity ?? 1);
        }

        private static InventorySlotData CloneForDialog(InventorySlotData slot, InventoryType type, int quantity)
        {
            if (slot == null)
            {
                return null;
            }

            InventorySlotData clone = slot.Clone();
            clone.Quantity = Math.Max(1, quantity);
            clone.PreferredInventoryType = type;
            if (string.IsNullOrWhiteSpace(clone.ItemName))
            {
                clone.ItemName = InventoryItemMetadataResolver.TryResolveItemName(clone.ItemId, out string itemName)
                    ? itemName
                    : $"Item {clone.ItemId}";
            }

            if (string.IsNullOrWhiteSpace(clone.Description))
            {
                InventoryItemMetadataResolver.TryResolveItemDescription(clone.ItemId, out string description);
                clone.Description = description ?? string.Empty;
            }

            return clone;
        }

        private static InventorySlotData CreateFallbackSlot(int itemId, InventoryType type)
        {
            InventoryItemMetadataResolver.TryResolveItemName(itemId, out string itemName);
            InventoryItemMetadataResolver.TryResolveItemDescription(itemId, out string description);

            return new InventorySlotData
            {
                ItemId = itemId,
                Quantity = 1,
                PreferredInventoryType = type,
                ItemName = string.IsNullOrWhiteSpace(itemName) ? $"Item {itemId}" : itemName,
                Description = description ?? string.Empty
            };
        }

        private static string ResolveItemLabel(InventorySlotData slot)
        {
            return slot == null
                ? "Unknown item"
                : !string.IsNullOrWhiteSpace(slot.ItemName)
                    ? slot.ItemName
                    : $"Item {slot.ItemId}";
        }

        private static InventoryType ResolveInventoryTypeForSlot(InventorySlotData slot, InventoryType fallback = InventoryType.NONE)
        {
            if (slot?.PreferredInventoryType.HasValue == true && slot.PreferredInventoryType.Value != InventoryType.NONE)
            {
                return slot.PreferredInventoryType.Value;
            }

            return slot != null && slot.ItemId > 0
                ? InventoryItemMetadataResolver.ResolveInventoryType(slot.ItemId)
                : fallback;
        }

        private InventoryType ResolveSelectedInventoryType()
        {
            return TabInventoryTypes[Math.Clamp(_selectedTabIndex, 0, TabInventoryTypes.Length - 1)];
        }

        private static string ResolvePaneLabel(WeddingWishListSelectionPane pane)
        {
            return pane switch
            {
                WeddingWishListSelectionPane.GiftList => "Wedding Gift List",
                WeddingWishListSelectionPane.Inventory => "My Items",
                WeddingWishListSelectionPane.WishList => "Wish List",
                WeddingWishListSelectionPane.Candidate => "Item Name",
                _ => "Selection"
            };
        }

        private static string ResolveRoleLabel(WeddingWishListRole role)
        {
            return role == WeddingWishListRole.Bride ? "bride" : "groom";
        }

        private static string ResolveTabLabel(int tabIndex)
        {
            return tabIndex switch
            {
                0 => "Equip",
                1 => "Use",
                2 => "Setup",
                3 => "Etc",
                4 => "Cash",
                _ => "Equip"
            };
        }
    }

    internal sealed class WeddingWishListSnapshot
    {
        public bool IsOpen { get; init; }
        public WeddingWishListDialogMode Mode { get; init; }
        public WeddingWishListRole Role { get; init; }
        public WeddingWishListSelectionPane ActivePane { get; init; }
        public int SelectedTabIndex { get; init; }
        public IReadOnlyList<string> TabLabels { get; init; } = Array.Empty<string>();
        public IReadOnlyList<InventorySlotData> GiftEntries { get; init; } = Array.Empty<InventorySlotData>();
        public IReadOnlyList<InventorySlotData> InventoryEntries { get; init; } = Array.Empty<InventorySlotData>();
        public IReadOnlyList<InventorySlotData> WishEntries { get; init; } = Array.Empty<InventorySlotData>();
        public IReadOnlyList<InventorySlotData> CandidateEntries { get; init; } = Array.Empty<InventorySlotData>();
        public int SelectedGiftIndex { get; init; }
        public int SelectedInventoryIndex { get; init; }
        public int SelectedWishIndex { get; init; }
        public int SelectedCandidateIndex { get; init; }
        public string StatusMessage { get; init; } = string.Empty;
        public string LocalCharacterName { get; init; } = string.Empty;
    }
}
