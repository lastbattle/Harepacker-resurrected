using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

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
        internal const int EngagementPacketOpcode = 161;
        internal const int WishListTransferPacketOpcode = 162;
        internal const byte SendWishListInputSubtype = 9;
        internal const byte SendPutItemRequestSubtype = 6;
        internal const byte SendGetItemRequestSubtype = 7;

        private const int MaxWishListEntryCount = 10;
        private const int ConfirmWishListInputStringPoolId = 0x1097;
        private const int WishListGiftAlreadySentStringPoolId = 0x1098;
        private const int ConfirmWishListGiftClaimStringPoolId = 0x036C;
        private const int PutQuantityPromptStringPoolId = 0x0370;
        private const int ConfirmWishListGiftSendStringPoolId = 0x10C0;
        private const int WishListFullStringPoolId = 0x10BE;

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
        private readonly Dictionary<int, InventorySlotData> _giftByWishItemId = new();
        private readonly Dictionary<(WeddingWishListSelectionPane Pane, int TabIndex), int> _paneStartIndices = new();
        private readonly List<InventorySlotData> _wishListEntries = new();
        private readonly List<InventorySlotData> _candidateEntries = new();

        private IInventoryRuntime _inventory;
        private string _localCharacterName = "Player";
        private string _candidateQuery = string.Empty;
        private WeddingWishListDialogMode _mode;
        private WeddingWishListRole _role = WeddingWishListRole.Groom;
        private WeddingWishListSelectionPane _activePane = WeddingWishListSelectionPane.GiftList;
        private int _selectedTabIndex;
        private int _selectedGiftIndex;
        private int _selectedInventoryIndex;
        private int _selectedWishIndex;
        private int _selectedCandidateIndex;
        private bool _hasPendingTransferRequest;
        private bool _isGetConfirmationArmed;
        private bool _isPutQuantityPromptOpen;
        private bool _isPutConfirmationArmed;
        private int _putQuantityPromptWishItemId;
        private InventorySlotData _putQuantityPromptSourceItem;
        private int _putQuantityPromptQuantity = 1;
        private int _putQuantityPromptSourceSlotIndex;
        private int _pendingPutWishItemId;
        private InventorySlotData _pendingPutSourceItem;
        private int _pendingPutQuantity = 1;
        private int _pendingPutSourceSlotIndex;
        private int _lastOutboundPacketOpcode = -1;
        private byte[] _lastOutboundPacketPayload = Array.Empty<byte>();
        private string _lastOutboundPacketSummary = string.Empty;
        private bool _inputConfirmationArmed;
        private bool _isOpen;
        private string _statusMessage = "Wedding wish-list dialog is idle.";
        private bool _seeded;

        internal Action<string, int> SocialChatObserved { get; set; }

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
            ClearTransientActionState();

            _mode = mode;
            _isOpen = true;
            _hasPendingTransferRequest = false;
            _activePane = mode switch
            {
                WeddingWishListDialogMode.Receive => WeddingWishListSelectionPane.GiftList,
                WeddingWishListDialogMode.Give => WeddingWishListSelectionPane.Inventory,
                WeddingWishListDialogMode.Input => WeddingWishListSelectionPane.Candidate,
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
            _hasPendingTransferRequest = false;
            ClearTransientActionState();
            _activePane = _mode switch
            {
                WeddingWishListDialogMode.Receive => WeddingWishListSelectionPane.GiftList,
                WeddingWishListDialogMode.Give => WeddingWishListSelectionPane.Inventory,
                WeddingWishListDialogMode.Input => WeddingWishListSelectionPane.Candidate,
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
            _selectedGiftIndex = 0;
            _selectedInventoryIndex = 0;
            _paneStartIndices[(WeddingWishListSelectionPane.GiftList, _selectedTabIndex)] = 0;
            _paneStartIndices[(WeddingWishListSelectionPane.Inventory, _selectedTabIndex)] = 0;
            _hasPendingTransferRequest = false;
            ClearTransientActionState();
            ClampSelections();
            NormalizeViewportState();
            _statusMessage = $"Selected {ResolveTabLabel(_selectedTabIndex)} tab for the wedding wish-list dialog.";
            return _statusMessage;
        }

        internal string SetActivePane(WeddingWishListSelectionPane pane)
        {
            _activePane = pane;
            ApplyInputPaneFocusBehavior();
            ClearTransientActionState();
            ClampSelections();
            EnsureSelectionVisible(pane);
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
            ApplyInputPaneFocusBehavior();
            ClearTransientActionState();
            ClampSelections();
            EnsureSelectionVisible(pane);
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

            ClearTransientActionState();
            ClampSelections();
            EnsureSelectionVisible(_activePane);
            _statusMessage = $"Moved selection in {ResolvePaneLabel(_activePane)}.";
            return _statusMessage;
        }

        internal string ScrollPane(WeddingWishListSelectionPane pane, int delta)
        {
            int count = GetEntryCount(pane);
            int visibleRows = GetVisibleRowCount(pane);
            if (count <= visibleRows || visibleRows <= 0)
            {
                return string.Empty;
            }

            int startIndex = GetFirstVisibleIndex(pane);
            int maxStart = Math.Max(0, count - visibleRows);
            int nextStart = Math.Clamp(startIndex + delta, 0, maxStart);
            if (nextStart == startIndex)
            {
                return string.Empty;
            }

            _paneStartIndices[CreateViewportKey(pane)] = nextStart;
            if (pane == _activePane)
            {
                ClampSelectionIntoVisibleRange(pane);
            }

            _statusMessage = $"Scrolled {ResolvePaneLabel(pane)} to row {nextStart + 1}.";
            return _statusMessage;
        }

        internal string TryPutSelectedItem()
        {
            if (_mode != WeddingWishListDialogMode.Give)
            {
                return "Wedding wish-list Put is only available in give mode.";
            }

            if (_hasPendingTransferRequest)
            {
                return GetTransferPendingText();
            }

            if (_isPutConfirmationArmed)
            {
                return CommitPendingPutItem();
            }

            if (_isPutQuantityPromptOpen)
            {
                return BeginPendingPutConfirmation();
            }

            InventorySlotData selectedWish = GetSelectedWishEntry();
            if (selectedWish == null)
            {
                return "Select a wedding wish-list row before pressing Put.";
            }

            if (selectedWish.ItemId <= 0)
            {
                return $"\"{ResolveItemLabel(selectedWish)}\" is still only a typed wish-list entry and cannot accept gifts until the client resolves it to an item.";
            }

            if (_giftByWishItemId.ContainsKey(selectedWish.ItemId))
            {
                return GetWishListGiftAlreadySentText();
            }

            InventorySlotData source = GetSelectedInventoryEntry();
            if (source == null)
            {
                return "Select a My Items row before pressing Put.";
            }

            if (source.Quantity > 1)
            {
                _putQuantityPromptSourceItem = CloneForDialog(source, ResolveInventoryTypeForSlot(source, ResolveSelectedInventoryType()), source.Quantity);
                _putQuantityPromptWishItemId = selectedWish.ItemId;
                _putQuantityPromptQuantity = 1;
                _putQuantityPromptSourceSlotIndex = ResolveSelectedInventoryPacketSlotIndex();
                _isPutQuantityPromptOpen = true;
                _isPutConfirmationArmed = false;
                _isGetConfirmationArmed = false;
                _inputConfirmationArmed = false;
                UpdatePutQuantityPromptStatus();
                return _statusMessage;
            }

            return ArmPendingPutConfirmation(selectedWish, source, 1, ResolveSelectedInventoryPacketSlotIndex());
        }

        internal string TryGetSelectedItem()
        {
            if (_mode != WeddingWishListDialogMode.Receive)
            {
                return "Wedding wish-list Get is only available in receive mode.";
            }

            if (_hasPendingTransferRequest)
            {
                return GetTransferPendingText();
            }

            List<InventorySlotData> giftList = GetGiftListForSelectedTab();
            InventorySlotData selected = _selectedGiftIndex >= 0 && _selectedGiftIndex < giftList.Count
                ? giftList[_selectedGiftIndex]
                : null;
            if (selected == null)
            {
                return "Select a wedding gift list row before pressing Get.";
            }

            if (!_isGetConfirmationArmed)
            {
                _isGetConfirmationArmed = true;
                _isPutQuantityPromptOpen = false;
                _inputConfirmationArmed = false;
                _statusMessage = GetWishListGiftClaimConfirmText();
                return _statusMessage;
            }

            InventoryType type = ResolveInventoryTypeForSlot(selected, ResolveSelectedInventoryType());
            if (_inventory == null || !_inventory.CanAcceptItem(type, selected.ItemId, 1, selected.MaxStackSize))
            {
                return $"Unable to return {ResolveItemLabel(selected)} to the local inventory.";
            }

            _inventory.AddItem(type, CloneForDialog(selected, type, 1));
            StageGetItemRequestPacket(selected, type, ResolvePacketSlotIndex(_selectedGiftIndex));
            giftList.RemoveAt(_selectedGiftIndex);
            _isGetConfirmationArmed = false;
            _hasPendingTransferRequest = true;
            RefreshCandidateEntries();
            ClampSelections();
            _statusMessage = $"Claimed {ResolveItemLabel(selected)} from the wedding gift list and marked the request pending until the dialog refreshes.";
            NotifySocialChatObserved(ResolveItemLabel(selected));
            return _statusMessage;
        }

        internal string TryAddCandidateWish()
        {
            if (_mode != WeddingWishListDialogMode.Input)
            {
                return "Wedding wish-list Enter is only available in input mode.";
            }

            string query = (_candidateQuery ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                return "Type an item name before pressing Enter.";
            }

            if (_wishListEntries.Count >= MaxWishListEntryCount)
            {
                return GetWishListFullText();
            }

            if (_wishListEntries.Any(entry => string.Equals(ResolveItemLabel(entry), query, StringComparison.OrdinalIgnoreCase)))
            {
                return $"\"{query}\" is already present in the wedding wish list.";
            }

            InventorySlotData enteredWish = CreateWishListInputEntry(query);
            _wishListEntries.Add(enteredWish);
            _candidateQuery = string.Empty;
            RefreshCandidateEntries();
            _selectedCandidateIndex = 0;
            _selectedWishIndex = -1;
            _activePane = WeddingWishListSelectionPane.Candidate;
            ClearTransientActionState();
            ClampSelections();
            EnsureSelectionVisible(WeddingWishListSelectionPane.Candidate);
            _statusMessage = $"Inserted \"{ResolveItemLabel(enteredWish)}\" into the wedding wish list.";
            NotifySocialChatObserved(ResolveItemLabel(enteredWish));
            return _statusMessage;
        }

        internal string AppendCandidateQuery(char value)
        {
            if (_mode != WeddingWishListDialogMode.Input || char.IsControl(value))
            {
                return string.Empty;
            }

            const int maxQueryLength = 24;
            if (_candidateQuery.Length >= maxQueryLength)
            {
                return string.Empty;
            }

            _candidateQuery += value;
            RefreshCandidateEntries();
            _selectedCandidateIndex = 0;
            _selectedWishIndex = -1;
            ClearTransientActionState();
            ClampSelections();
            EnsureSelectionVisible(WeddingWishListSelectionPane.Candidate);
            _statusMessage = $"Filtered candidate items by \"{_candidateQuery}\".";
            return _statusMessage;
        }

        internal string BackspaceCandidateQuery()
        {
            if (_mode != WeddingWishListDialogMode.Input || string.IsNullOrEmpty(_candidateQuery))
            {
                return string.Empty;
            }

            _candidateQuery = _candidateQuery[..^1];
            RefreshCandidateEntries();
            _selectedCandidateIndex = 0;
            _selectedWishIndex = -1;
            ClearTransientActionState();
            ClampSelections();
            EnsureSelectionVisible(WeddingWishListSelectionPane.Candidate);
            _statusMessage = string.IsNullOrEmpty(_candidateQuery)
                ? "Cleared candidate item-name filter."
                : $"Filtered candidate items by \"{_candidateQuery}\".";
            return _statusMessage;
        }

        internal string AppendPutQuantityDigit(char value)
        {
            if (!_isPutQuantityPromptOpen || !char.IsDigit(value))
            {
                return string.Empty;
            }

            int maxQuantity = GetPendingPutQuantityMax();
            if (maxQuantity <= 0)
            {
                return string.Empty;
            }

            string existingDigits = _putQuantityPromptQuantity.ToString();
            string candidateDigits = existingDigits == "1"
                ? value.ToString()
                : existingDigits + value;
            if (!int.TryParse(candidateDigits, out int parsedQuantity))
            {
                return string.Empty;
            }

            _putQuantityPromptQuantity = Math.Clamp(parsedQuantity, 1, maxQuantity);
            UpdatePutQuantityPromptStatus();
            return _statusMessage;
        }

        internal string BackspacePutQuantityDigit()
        {
            if (!_isPutQuantityPromptOpen)
            {
                return string.Empty;
            }

            string digits = _putQuantityPromptQuantity.ToString();
            _putQuantityPromptQuantity = digits.Length <= 1
                ? 1
                : Math.Max(1, int.Parse(digits[..^1]));
            UpdatePutQuantityPromptStatus();
            return _statusMessage;
        }

        internal string CancelTransientPrompt()
        {
            if (_isPutQuantityPromptOpen)
            {
                _isPutQuantityPromptOpen = false;
                _putQuantityPromptSourceItem = null;
                _putQuantityPromptWishItemId = 0;
                _putQuantityPromptQuantity = 1;
                _statusMessage = "Cancelled the wedding gift quantity prompt.";
                return _statusMessage;
            }

            if (_isGetConfirmationArmed)
            {
                _isGetConfirmationArmed = false;
                _statusMessage = "Cancelled the pending wedding gift claim confirmation.";
                return _statusMessage;
            }

            if (_isPutConfirmationArmed)
            {
                _isPutConfirmationArmed = false;
                _pendingPutWishItemId = 0;
                _pendingPutSourceItem = null;
                _pendingPutQuantity = 1;
                _statusMessage = "Cancelled the pending wedding gift send confirmation.";
                return _statusMessage;
            }

            if (_inputConfirmationArmed)
            {
                _inputConfirmationArmed = false;
                _statusMessage = "Cancelled the pending wedding wish-list registration confirmation.";
                return _statusMessage;
            }

            return string.Empty;
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
            if (removed.ItemId > 0)
            {
                _giftByWishItemId.Remove(removed.ItemId);
            }

            _selectedWishIndex = -1;
            ClearTransientActionState();
            ClampSelections();
            _statusMessage = $"Removed {ResolveItemLabel(removed)} from the wedding wish list.";
            NotifySocialChatObserved(ResolveItemLabel(removed));
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

            if (_mode != WeddingWishListDialogMode.Input)
            {
                return "Wedding wish-list OK is only available in input mode.";
            }

            if (!_inputConfirmationArmed)
            {
                _inputConfirmationArmed = true;
                _statusMessage = GetInputConfirmPromptText();
                return _statusMessage;
            }

            _isOpen = false;
            _inputConfirmationArmed = false;
            StageWishListInputPacket();
            _statusMessage = $"Confirmed {_wishListEntries.Count} wedding wish-list item(s) through the dedicated OK/SetRet owner path and staged the client-owned SendWishListInput request.";
            NotifySocialChatObserved(_wishListEntries.Select(ResolveItemLabel));
            return _statusMessage;
        }

        internal string CompletePendingTransferRequest()
        {
            if (!_isOpen)
            {
                return "Wedding wish-list dialog is not open.";
            }

            if (!_hasPendingTransferRequest)
            {
                return "Wedding wish-list dialog has no pending transfer request.";
            }

            _hasPendingTransferRequest = false;
            ClearTransientActionState();
            RefreshCandidateEntries();
            ClampSelections();
            NormalizeViewportState();
            _statusMessage = "Applied wedding wish-list transfer completion and reopened Get/Put request actions.";
            return _statusMessage;
        }

        internal string Clear()
        {
            _giftListByTab.Clear();
            _giftByWishItemId.Clear();
            _wishListEntries.Clear();
            _candidateEntries.Clear();
            _seeded = false;
            _isOpen = false;
            _mode = WeddingWishListDialogMode.None;
            _selectedGiftIndex = 0;
            _selectedInventoryIndex = 0;
            _selectedWishIndex = 0;
            _selectedCandidateIndex = 0;
            _candidateQuery = string.Empty;
            _hasPendingTransferRequest = false;
            ClearTransientActionState();
            _paneStartIndices.Clear();
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
                GiftEntries = new ReadOnlyCollection<InventorySlotData>(GetGiftEntriesForCurrentMode().Select(CloneForDialog).ToList()),
                InventoryEntries = new ReadOnlyCollection<InventorySlotData>(GetInventoryEntriesForSelectedTab().Select(CloneForDialog).ToList()),
                WishEntries = new ReadOnlyCollection<InventorySlotData>(_wishListEntries.Select(CloneForDialog).ToList()),
                CandidateEntries = new ReadOnlyCollection<InventorySlotData>(_candidateEntries.Select(CloneForDialog).ToList()),
                SelectedGiftIndex = _selectedGiftIndex,
                SelectedInventoryIndex = _selectedInventoryIndex,
                SelectedWishIndex = _selectedWishIndex,
                SelectedCandidateIndex = _selectedCandidateIndex,
                FirstVisibleGiftIndex = GetFirstVisibleIndex(WeddingWishListSelectionPane.GiftList),
                FirstVisibleInventoryIndex = GetFirstVisibleIndex(WeddingWishListSelectionPane.Inventory),
                FirstVisibleWishIndex = GetFirstVisibleIndex(WeddingWishListSelectionPane.WishList),
                FirstVisibleCandidateIndex = GetFirstVisibleIndex(WeddingWishListSelectionPane.Candidate),
                CandidateQuery = _candidateQuery,
                IsGetConfirmationArmed = _isGetConfirmationArmed,
                IsPutQuantityPromptOpen = _isPutQuantityPromptOpen,
                IsPutConfirmationArmed = _isPutConfirmationArmed,
                IsInputConfirmationArmed = _inputConfirmationArmed,
                CanGetSelectedItem = CanGetSelectedItem(),
                CanPutSelectedItem = CanPutSelectedItem(),
                CanEnterSelectedWish = CanEnterSelectedWish(),
                CanDeleteSelectedWish = CanDeleteSelectedWish(),
                CanConfirmInput = CanConfirmInput(),
                CanCloseWindow = CanCloseWindow(),
                PutQuantityPromptText = _isPutQuantityPromptOpen ? GetPutQuantityPromptText() : string.Empty,
                PutQuantityPromptItemLabel = _isPutQuantityPromptOpen ? ResolveItemLabel(_putQuantityPromptSourceItem) : string.Empty,
                PutQuantityPromptQuantity = _isPutQuantityPromptOpen ? _putQuantityPromptQuantity : 0,
                PutQuantityPromptMaxQuantity = _isPutQuantityPromptOpen ? GetPendingPutQuantityMax() : 0,
                LastOutboundPacketOpcode = _lastOutboundPacketOpcode,
                LastOutboundPacketPayload = Array.AsReadOnly(_lastOutboundPacketPayload),
                LastOutboundPacketSummary = _lastOutboundPacketSummary,
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
            IEnumerable<InventorySlotData> candidates = EnumerateInventorySlots();
            if (!string.IsNullOrWhiteSpace(_candidateQuery))
            {
                string query = _candidateQuery.Trim();
                candidates = candidates.Where(slot =>
                {
                    string label = ResolveItemLabel(slot);
                    return label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                        || slot.ItemId.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                });
            }

            foreach (InventorySlotData slot in candidates)
            {
                _candidateEntries.Add(CloneForDialog(slot, ResolveInventoryTypeForSlot(slot), 1));
            }

            if (_candidateEntries.Count == 0 && string.IsNullOrWhiteSpace(_candidateQuery))
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

        private List<InventorySlotData> GetGiftEntriesForCurrentMode()
        {
            if (_mode == WeddingWishListDialogMode.Give)
            {
                List<InventorySlotData> entries = new();
                foreach (InventorySlotData wishEntry in _wishListEntries)
                {
                    if (wishEntry != null && _giftByWishItemId.TryGetValue(wishEntry.ItemId, out InventorySlotData giftedEntry))
                    {
                        entries.Add(giftedEntry);
                    }
                }

                return entries;
            }

            return GetGiftListForSelectedTab();
        }

        private List<InventorySlotData> GetInventoryEntriesForSelectedTab()
        {
            InventoryType type = ResolveSelectedInventoryType();
            return EnumerateInventorySlots(type);
        }

        private InventorySlotData CreateWishListInputEntry(string query)
        {
            string trimmedQuery = (query ?? string.Empty).Trim();
            InventorySlotData matchedEntry = EnumerateInventorySlots()
                .FirstOrDefault(slot => string.Equals(ResolveItemLabel(slot), trimmedQuery, StringComparison.OrdinalIgnoreCase));
            if (matchedEntry != null)
            {
                InventorySlotData clone = CloneForDialog(matchedEntry, ResolveInventoryTypeForSlot(matchedEntry), 1);
                clone.ItemName = trimmedQuery;
                return clone;
            }

            return new InventorySlotData
            {
                ItemId = 0,
                Quantity = 1,
                PreferredInventoryType = InventoryType.NONE,
                ItemName = trimmedQuery,
                Description = string.Empty
            };
        }

        private InventorySlotData GetSelectedWishEntry()
        {
            return _selectedWishIndex >= 0 && _selectedWishIndex < _wishListEntries.Count
                ? _wishListEntries[_selectedWishIndex]
                : null;
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
            _selectedGiftIndex = ClampIndex(_selectedGiftIndex, GetGiftEntriesForCurrentMode().Count);
            _selectedInventoryIndex = ClampIndex(_selectedInventoryIndex, GetInventoryEntriesForSelectedTab().Count);
            _selectedWishIndex = ClampOptionalIndex(_selectedWishIndex, _wishListEntries.Count);
            _selectedCandidateIndex = ClampIndex(_selectedCandidateIndex, _candidateEntries.Count);
        }

        private void NormalizeViewportState()
        {
            NormalizePaneViewport(WeddingWishListSelectionPane.GiftList);
            NormalizePaneViewport(WeddingWishListSelectionPane.Inventory);
            NormalizePaneViewport(WeddingWishListSelectionPane.WishList);
            NormalizePaneViewport(WeddingWishListSelectionPane.Candidate);
            EnsureSelectionVisible(_activePane);
        }

        private void NormalizePaneViewport(WeddingWishListSelectionPane pane)
        {
            int count = GetEntryCount(pane);
            int visibleRows = GetVisibleRowCount(pane);
            if (count <= 0 || visibleRows <= 0)
            {
                _paneStartIndices[CreateViewportKey(pane)] = 0;
                return;
            }

            int maxStart = Math.Max(0, count - visibleRows);
            _paneStartIndices[CreateViewportKey(pane)] = Math.Clamp(GetFirstVisibleIndex(pane), 0, maxStart);
        }

        private void EnsureSelectionVisible(WeddingWishListSelectionPane pane)
        {
            int count = GetEntryCount(pane);
            int visibleRows = GetVisibleRowCount(pane);
            if (count <= 0 || visibleRows <= 0)
            {
                _paneStartIndices[CreateViewportKey(pane)] = 0;
                return;
            }

            int selectedIndex = GetSelectedIndex(pane);
            if (selectedIndex < 0)
            {
                return;
            }

            int startIndex = GetFirstVisibleIndex(pane);
            int maxStart = Math.Max(0, count - visibleRows);
            if (selectedIndex < startIndex)
            {
                _paneStartIndices[CreateViewportKey(pane)] = selectedIndex;
                return;
            }

            int endIndex = startIndex + visibleRows - 1;
            if (selectedIndex > endIndex)
            {
                _paneStartIndices[CreateViewportKey(pane)] = Math.Clamp(selectedIndex - visibleRows + 1, 0, maxStart);
            }
        }

        private void ClampSelectionIntoVisibleRange(WeddingWishListSelectionPane pane)
        {
            int count = GetEntryCount(pane);
            int visibleRows = GetVisibleRowCount(pane);
            if (count <= 0 || visibleRows <= 0)
            {
                SetSelectedIndex(pane, 0);
                return;
            }

            if (GetSelectedIndex(pane) < 0)
            {
                return;
            }

            int startIndex = GetFirstVisibleIndex(pane);
            int endIndex = Math.Min(count - 1, startIndex + visibleRows - 1);
            int selectedIndex = Math.Clamp(GetSelectedIndex(pane), startIndex, endIndex);
            SetSelectedIndex(pane, selectedIndex);
        }

        private int GetEntryCount(WeddingWishListSelectionPane pane)
        {
            return pane switch
            {
                WeddingWishListSelectionPane.GiftList => GetGiftEntriesForCurrentMode().Count,
                WeddingWishListSelectionPane.Inventory => GetInventoryEntriesForSelectedTab().Count,
                WeddingWishListSelectionPane.WishList => _wishListEntries.Count,
                WeddingWishListSelectionPane.Candidate => _candidateEntries.Count,
                _ => 0
            };
        }

        private int GetVisibleRowCount(WeddingWishListSelectionPane pane)
        {
            return (_mode, pane) switch
            {
                (WeddingWishListDialogMode.Receive, WeddingWishListSelectionPane.GiftList) => 5,
                (WeddingWishListDialogMode.Receive, WeddingWishListSelectionPane.Inventory) => 5,
                (WeddingWishListDialogMode.Give, WeddingWishListSelectionPane.WishList) => 3,
                (WeddingWishListDialogMode.Give, WeddingWishListSelectionPane.GiftList) => 2,
                (WeddingWishListDialogMode.Give, WeddingWishListSelectionPane.Inventory) => 3,
                (WeddingWishListDialogMode.Input, WeddingWishListSelectionPane.WishList) => 8,
                (WeddingWishListDialogMode.Input, WeddingWishListSelectionPane.Candidate) => 1,
                _ => 0
            };
        }

        private int GetFirstVisibleIndex(WeddingWishListSelectionPane pane)
        {
            return _paneStartIndices.TryGetValue(CreateViewportKey(pane), out int index)
                ? index
                : 0;
        }

        private int GetSelectedIndex(WeddingWishListSelectionPane pane)
        {
            return pane switch
            {
                WeddingWishListSelectionPane.GiftList => _selectedGiftIndex,
                WeddingWishListSelectionPane.Inventory => _selectedInventoryIndex,
                WeddingWishListSelectionPane.WishList => _selectedWishIndex,
                WeddingWishListSelectionPane.Candidate => _selectedCandidateIndex,
                _ => 0
            };
        }

        private void SetSelectedIndex(WeddingWishListSelectionPane pane, int index)
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
        }

        private (WeddingWishListSelectionPane Pane, int TabIndex) CreateViewportKey(WeddingWishListSelectionPane pane)
        {
            return pane switch
            {
                WeddingWishListSelectionPane.GiftList or WeddingWishListSelectionPane.Inventory => (pane, _selectedTabIndex),
                _ => (pane, -1)
            };
        }

        private static int ClampIndex(int index, int count)
        {
            return count <= 0 ? 0 : Math.Clamp(index, 0, count - 1);
        }

        private static int ClampOptionalIndex(int index, int count)
        {
            if (count <= 0)
            {
                return -1;
            }

            return index < 0
                ? -1
                : Math.Clamp(index, 0, count - 1);
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

        private static string GetInputConfirmPromptText()
        {
            return MapleStoryStringPool.GetOrFallback(
                ConfirmWishListInputStringPoolId,
                "Do you want to register this wish list now?",
                appendFallbackSuffix: true);
        }

        private static string GetTransferPendingText()
        {
            return "A wedding wish-list transfer request is already pending until the owner refreshes.";
        }

        private static string GetWishListGiftAlreadySentText()
        {
            return MapleStoryStringPool.GetOrFallback(
                WishListGiftAlreadySentStringPoolId,
                "You cannot give more than one present for each wishlist.",
                appendFallbackSuffix: true);
        }

        private static string GetWishListGiftClaimConfirmText()
        {
            return MapleStoryStringPool.GetOrFallback(
                ConfirmWishListGiftClaimStringPoolId,
                "Are you sure you want to take it out?",
                appendFallbackSuffix: true);
        }

        private static string GetPutQuantityPromptText()
        {
            return MapleStoryStringPool.GetOrFallback(
                PutQuantityPromptStringPoolId,
                "How many do you wish to send?",
                appendFallbackSuffix: true);
        }

        private static string GetPutConfirmationText()
        {
            return MapleStoryStringPool.GetOrFallback(
                ConfirmWishListGiftSendStringPoolId,
                "Once the gift is sent, it cannot be canceled. Do you still want to send it?",
                appendFallbackSuffix: true);
        }

        private static string GetWishListFullText()
        {
            return MapleStoryStringPool.GetOrFallback(
                WishListFullStringPoolId,
                "Your wish list is full.",
                appendFallbackSuffix: true);
        }

        private bool CanGetSelectedItem()
        {
            if (_mode != WeddingWishListDialogMode.Receive)
            {
                return false;
            }

            if (_isGetConfirmationArmed)
            {
                return GetGiftListForSelectedTab().Count > 0;
            }

            return !_hasPendingTransferRequest && GetGiftListForSelectedTab().Count > 0 && _selectedGiftIndex >= 0 && _selectedGiftIndex < GetGiftListForSelectedTab().Count;
        }

        private bool CanPutSelectedItem()
        {
            if (_mode != WeddingWishListDialogMode.Give)
            {
                return false;
            }

            if (_isPutQuantityPromptOpen || _isPutConfirmationArmed)
            {
                return true;
            }

            if (_hasPendingTransferRequest)
            {
                return false;
            }

            InventorySlotData selectedWish = GetSelectedWishEntry();
            if (selectedWish == null || selectedWish.ItemId <= 0 || _giftByWishItemId.ContainsKey(selectedWish.ItemId))
            {
                return false;
            }

            return GetSelectedInventoryEntry() != null;
        }

        private bool CanEnterSelectedWish()
        {
            if (_mode != WeddingWishListDialogMode.Input)
            {
                return false;
            }

            string query = (_candidateQuery ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query) || _wishListEntries.Count >= MaxWishListEntryCount)
            {
                return false;
            }

            return !_wishListEntries.Any(entry => string.Equals(ResolveItemLabel(entry), query, StringComparison.OrdinalIgnoreCase));
        }

        private bool CanDeleteSelectedWish()
        {
            return _mode == WeddingWishListDialogMode.Input
                && _selectedWishIndex >= 0
                && _selectedWishIndex < _wishListEntries.Count;
        }

        private bool CanConfirmInput()
        {
            return _isOpen && _mode == WeddingWishListDialogMode.Input;
        }

        private bool CanCloseWindow()
        {
            return _isOpen && (_mode == WeddingWishListDialogMode.Receive || _mode == WeddingWishListDialogMode.Give);
        }

        private string TryCommitGiftPut(InventorySlotData selectedWish, InventorySlotData source, int quantity)
        {
            InventoryType type = ResolveInventoryTypeForSlot(source, ResolveSelectedInventoryType());
            if (_inventory == null || !_inventory.TryConsumeItem(type, source.ItemId, quantity))
            {
                return $"Unable to move {ResolveItemLabel(source)} out of the local inventory.";
            }

            InventorySlotData gifted = CloneForDialog(source, type, quantity);
            StagePutItemRequestPacket(selectedWish, gifted, type, _pendingPutSourceSlotIndex);
            _giftByWishItemId[selectedWish.ItemId] = gifted;
            _hasPendingTransferRequest = true;
            ClearTransientActionState();
            RefreshCandidateEntries();
            ClampSelections();
            _statusMessage = $"Queued {ResolveItemLabel(gifted)} x{gifted.Quantity} for {ResolveItemLabel(selectedWish)} and marked the request pending until the dialog refreshes.";
            NotifySocialChatObserved(ResolveItemLabel(gifted), ResolveItemLabel(selectedWish));
            return _statusMessage;
        }

        private string BeginPendingPutConfirmation()
        {
            if (!_isPutQuantityPromptOpen || _putQuantityPromptSourceItem == null)
            {
                return string.Empty;
            }

            InventorySlotData selectedWish = _wishListEntries.FirstOrDefault(entry => entry.ItemId == _putQuantityPromptWishItemId);
            if (selectedWish == null)
            {
                ClearTransientActionState();
                _statusMessage = "The selected wedding wish-list row is no longer available.";
                return _statusMessage;
            }

            InventoryType type = ResolveInventoryTypeForSlot(_putQuantityPromptSourceItem, ResolveSelectedInventoryType());
            if (_inventory == null || _inventory.GetItemCount(type, _putQuantityPromptSourceItem.ItemId) < _putQuantityPromptQuantity)
            {
                ClearTransientActionState();
                _statusMessage = $"Unable to move {_putQuantityPromptQuantity} of {ResolveItemLabel(_putQuantityPromptSourceItem)} out of the local inventory.";
                return _statusMessage;
            }

            return ArmPendingPutConfirmation(selectedWish, _putQuantityPromptSourceItem, _putQuantityPromptQuantity, _putQuantityPromptSourceSlotIndex);
        }

        private string ArmPendingPutConfirmation(InventorySlotData selectedWish, InventorySlotData source, int quantity, int sourceSlotIndex)
        {
            if (selectedWish == null || source == null)
            {
                return string.Empty;
            }

            _pendingPutWishItemId = selectedWish.ItemId;
            _pendingPutSourceItem = CloneForDialog(source, ResolveInventoryTypeForSlot(source, ResolveSelectedInventoryType()), quantity);
            _pendingPutQuantity = Math.Max(1, quantity);
            _pendingPutSourceSlotIndex = sourceSlotIndex;
            _isPutConfirmationArmed = true;
            _isPutQuantityPromptOpen = false;
            _isGetConfirmationArmed = false;
            _inputConfirmationArmed = false;
            _statusMessage = GetPutConfirmationText();
            return _statusMessage;
        }

        private string CommitPendingPutItem()
        {
            if (!_isPutConfirmationArmed || _pendingPutSourceItem == null)
            {
                return string.Empty;
            }

            InventorySlotData selectedWish = _wishListEntries.FirstOrDefault(entry => entry.ItemId == _pendingPutWishItemId);
            if (selectedWish == null)
            {
                ClearTransientActionState();
                _statusMessage = "The selected wedding wish-list row is no longer available.";
                return _statusMessage;
            }

            InventoryType type = ResolveInventoryTypeForSlot(_pendingPutSourceItem, ResolveSelectedInventoryType());
            if (_inventory == null || _inventory.GetItemCount(type, _pendingPutSourceItem.ItemId) < _pendingPutQuantity)
            {
                ClearTransientActionState();
                _statusMessage = $"Unable to move {_pendingPutQuantity} of {ResolveItemLabel(_pendingPutSourceItem)} out of the local inventory.";
                return _statusMessage;
            }

            return TryCommitGiftPut(selectedWish, _pendingPutSourceItem, _pendingPutQuantity);
        }

        private int GetPendingPutQuantityMax()
        {
            return Math.Max(1, _putQuantityPromptSourceItem?.Quantity ?? 1);
        }

        private void UpdatePutQuantityPromptStatus()
        {
            int maxQuantity = GetPendingPutQuantityMax();
            _statusMessage = $"{GetPutQuantityPromptText()} {ResolveItemLabel(_putQuantityPromptSourceItem)} [{_putQuantityPromptQuantity}/{maxQuantity}]";
        }

        private int ResolveSelectedInventoryPacketSlotIndex()
        {
            return ResolvePacketSlotIndex(_selectedInventoryIndex);
        }

        private static int ResolvePacketSlotIndex(int zeroBasedIndex)
        {
            return Math.Max(1, zeroBasedIndex + 1);
        }

        private void StageWishListInputPacket()
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(SendWishListInputSubtype);
            writer.Write((byte)_wishListEntries.Count);
            foreach (InventorySlotData entry in _wishListEntries)
            {
                WritePacketString(writer, ResolveItemLabel(entry));
            }

            StageOutboundPacket(EngagementPacketOpcode, stream.ToArray(), $"SendWishListInput staged {_wishListEntries.Count} item(s).");
        }

        private void StageGetItemRequestPacket(InventorySlotData item, InventoryType type, int sourceSlotIndex)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(SendGetItemRequestSubtype);
            writer.Write((byte)ResolveClientItemCategory(item, type));
            writer.Write((byte)Math.Clamp(sourceSlotIndex, 1, byte.MaxValue));
            StageOutboundPacket(WishListTransferPacketOpcode, stream.ToArray(), $"SendGetItemRequest staged {ResolveItemLabel(item)} from slot {sourceSlotIndex}.");
        }

        private void StagePutItemRequestPacket(InventorySlotData wish, InventorySlotData item, InventoryType type, int sourceSlotIndex)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(SendPutItemRequestSubtype);
            writer.Write((short)Math.Max(1, sourceSlotIndex));
            writer.Write(item?.ItemId ?? 0);
            writer.Write((short)Math.Max(1, item?.Quantity ?? 1));
            StageOutboundPacket(WishListTransferPacketOpcode, stream.ToArray(), $"SendPutItemRequest staged {ResolveItemLabel(item)} for {ResolveItemLabel(wish)} from slot {sourceSlotIndex}.");
        }

        private static int ResolveClientItemCategory(InventorySlotData item, InventoryType fallbackType)
        {
            if (item?.ItemId > 0)
            {
                return Math.Clamp(item.ItemId / 1000000, 0, byte.MaxValue);
            }

            return fallbackType switch
            {
                InventoryType.EQUIP => 1,
                InventoryType.USE => 2,
                InventoryType.SETUP => 3,
                InventoryType.ETC => 4,
                InventoryType.CASH => 5,
                _ => 0
            };
        }

        private void StageOutboundPacket(int opcode, byte[] payload, string summary)
        {
            _lastOutboundPacketOpcode = opcode;
            _lastOutboundPacketPayload = payload ?? Array.Empty<byte>();
            _lastOutboundPacketSummary = summary ?? string.Empty;
        }

        private static void WritePacketString(BinaryWriter writer, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            int byteCount = Math.Min(short.MaxValue, bytes.Length);
            writer.Write((short)byteCount);
            writer.Write(bytes, 0, byteCount);
        }

        private void ClearTransientActionState()
        {
            _isGetConfirmationArmed = false;
            _isPutQuantityPromptOpen = false;
            _isPutConfirmationArmed = false;
            _putQuantityPromptWishItemId = 0;
            _putQuantityPromptSourceItem = null;
            _putQuantityPromptQuantity = 1;
            _putQuantityPromptSourceSlotIndex = 0;
            _pendingPutWishItemId = 0;
            _pendingPutSourceItem = null;
            _pendingPutQuantity = 1;
            _pendingPutSourceSlotIndex = 0;
            _inputConfirmationArmed = false;
        }

        private void NotifySocialChatObserved(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            SocialChatObserved?.Invoke(message.Trim(), Environment.TickCount);
        }

        private void NotifySocialChatObserved(IEnumerable<string> messages)
        {
            if (messages == null)
            {
                return;
            }

            int tickCount = Environment.TickCount;
            HashSet<string> seen = null;
            foreach (string message in messages)
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                string normalized = message.Trim();
                if (normalized.Length == 0)
                {
                    continue;
                }

                seen ??= new HashSet<string>(StringComparer.Ordinal);
                if (!seen.Add(normalized))
                {
                    continue;
                }

                SocialChatObserved?.Invoke(normalized, tickCount);
            }
        }

        private void NotifySocialChatObserved(params string[] messages)
        {
            NotifySocialChatObserved((IEnumerable<string>)messages);
        }

        private void ApplyInputPaneFocusBehavior()
        {
            if (_mode != WeddingWishListDialogMode.Input)
            {
                return;
            }

            if (_activePane == WeddingWishListSelectionPane.Candidate)
            {
                _selectedWishIndex = -1;
            }
            else if (_activePane == WeddingWishListSelectionPane.WishList && _selectedWishIndex < 0 && _wishListEntries.Count > 0)
            {
                _selectedWishIndex = 0;
            }
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
        public int FirstVisibleGiftIndex { get; init; }
        public int FirstVisibleInventoryIndex { get; init; }
        public int FirstVisibleWishIndex { get; init; }
        public int FirstVisibleCandidateIndex { get; init; }
        public string CandidateQuery { get; init; } = string.Empty;
        public bool IsGetConfirmationArmed { get; init; }
        public bool IsPutQuantityPromptOpen { get; init; }
        public bool IsPutConfirmationArmed { get; init; }
        public bool IsInputConfirmationArmed { get; init; }
        public bool CanGetSelectedItem { get; init; }
        public bool CanPutSelectedItem { get; init; }
        public bool CanEnterSelectedWish { get; init; }
        public bool CanDeleteSelectedWish { get; init; }
        public bool CanConfirmInput { get; init; }
        public bool CanCloseWindow { get; init; }
        public string PutQuantityPromptText { get; init; } = string.Empty;
        public string PutQuantityPromptItemLabel { get; init; } = string.Empty;
        public int PutQuantityPromptQuantity { get; init; }
        public int PutQuantityPromptMaxQuantity { get; init; }
        public int LastOutboundPacketOpcode { get; init; } = -1;
        public IReadOnlyList<byte> LastOutboundPacketPayload { get; init; } = Array.Empty<byte>();
        public string LastOutboundPacketSummary { get; init; } = string.Empty;
        public string StatusMessage { get; init; } = string.Empty;
        public string LocalCharacterName { get; init; } = string.Empty;
    }
}
