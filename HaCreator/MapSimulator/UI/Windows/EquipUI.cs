using HaCreator.MapSimulator.Companions;
using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.UI.Controls;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;
using CharacterBuild = HaCreator.MapSimulator.Character.CharacterBuild;
using CharacterEquipSlot = HaCreator.MapSimulator.Character.EquipSlot;
using CharacterPart = HaCreator.MapSimulator.Character.CharacterPart;
using EquipSlotStateResolver = HaCreator.MapSimulator.Character.EquipSlotStateResolver;
using EquipSlotVisualState = HaCreator.MapSimulator.Character.EquipSlotVisualState;

namespace HaCreator.MapSimulator.UI
{
    public class EquipUI : UIWindowBase, IEquipmentPendingChangeWindow
    {
        private const int SLOT_SIZE = 32;
        private const int TOOLTIP_PADDING = 8;
        private const int TOOLTIP_OFFSET_X = 14;
        private const int TOOLTIP_OFFSET_Y = 8;
        private const int COMPANION_PANE_ATTACH_X = 12;
        private const int COMPANION_PANE_ATTACH_Y = 34;
        private const int COMPANION_MESSAGE_PADDING = 10;
        private const int COMPANION_TEXT_LINE_GAP = 4;
        private const int EQUIPMENT_EXCLUSIVE_REQUEST_COOLDOWN_MS = 500;

        private enum CompanionPaneMode
        {
            Hidden,
            Pet,
            Dragon
        }

        private sealed class PendingEquipmentChange
        {
            public EquipmentChangeRequestKind Kind { get; init; }
            public int RequestId { get; init; }
            public int RequestedAtTick { get; init; }
            public EquipmentChangeOwnerKind OwnerKind { get; init; }
            public int OwnerSessionId { get; init; }
            public InventoryType SourceInventoryType { get; init; }
            public int SourceInventoryIndex { get; init; } = -1;
            public bool SourceInventoryLocked { get; set; }
        }

        public enum EquipSlot
        {
            Ring1 = 0, Ring2 = 1, Ring3 = 2, Ring4 = 3, Pocket = 4, Pendant1 = 5, Pendant2 = 6,
            Weapon = 7, Belt = 8, Cap = 9, FaceAccessory = 10, EyeAccessory = 11, Top = 12,
            Bottom = 13, Shoes = 14, Earring = 15, Shoulder = 16, Glove = 17, Shield = 18,
            Cape = 19, Heart = 20, Badge = 21, Medal = 22, Android = 23, AndroidHeart = 24,
            Totem1 = 25, Totem2 = 26, Totem3 = 27
        }

        private readonly Dictionary<EquipSlot, Point> slotPositions;
        private readonly Dictionary<EquipSlot, EquipSlotData> equippedItems;
        private readonly Dictionary<DragonEquipSlot, Point> _dragonSlotPositions;
        private readonly Texture2D _overlayPixel;
        private readonly Point[] _petButtonIconPositions;
        private SpriteFont _font;
        private CharacterBuild _characterBuild;
        private PetController _petController;
        private DragonEquipmentController _dragonEquipmentController;
        private EquipSlot? _hoveredSlot;
        private bool _isDraggingItem;
        private EquipSlot? _draggedSlot;
        private CharacterPart _draggedPart;
        private Point _draggedItemPosition;
        private int? _hoveredPetIndex;
        private DragonEquipSlot? _hoveredDragonSlot;
        private Point _lastMousePosition;
        private MouseState _previousMouseState;
        private IDXObject _petPaneFrame;
        private IDXObject _dragonPaneFrame;
        private UIObject _btnPetEquipShow;
        private UIObject _btnPetEquipHide;
        private UIObject _btnDragonEquip;
        private readonly UIObject[] _petButtons = new UIObject[3];
        private CompanionPaneMode _companionPaneMode;
        private int _selectedPetIndex;
        private PendingEquipmentChange _pendingEquipmentChange;
        private int _equipmentRequestSessionId = 1;
        private int _lastEquipmentExclusiveRequestTick = int.MinValue;

        public override string WindowName => "Equipment";
        public Action<CharacterEquipSlot> ItemUpgradeRequested { get; set; }
        public Action<string> EquipmentEquipBlocked { get; set; }
        public Func<int, string> EquipmentEquipGuard { get; set; }
        public Func<EquipmentChangeRequest, EquipmentChangeResult> EquipmentChangeRequested { get; set; }
        public Func<EquipmentChangeRequest, EquipmentChangeSubmission> EquipmentChangeSubmitted { get; set; }
        public Func<EquipmentChangeResolutionQuery, EquipmentChangeResult> EquipmentChangeResultRequested { get; set; }
        public override CharacterBuild CharacterBuild
        {
            get => _characterBuild;
            set
            {
                if (!ReferenceEquals(_characterBuild, value))
                {
                    _equipmentRequestSessionId = NextEquipmentRequestSessionId(_equipmentRequestSessionId);
                }

                _characterBuild = value;
            }
        }
        public bool IsDraggingItem => _isDraggingItem;
        public bool HasDraggedCharacterItem => _isDraggingItem && _draggedPart != null;
        public InventorySlotData DraggedCharacterSlotData => HasDraggedCharacterItem ? CreateInventorySlot(_draggedPart) : null;
        public bool HasPendingEquipmentChange => _pendingEquipmentChange != null;

        public EquipUI(IDXObject frame, GraphicsDevice device) : base(frame)
        {
            slotPositions = new Dictionary<EquipSlot, Point>
            {
                { EquipSlot.Ring1, new Point(12, 55) }, { EquipSlot.Ring2, new Point(12, 92) },
                { EquipSlot.Ring3, new Point(12, 129) }, { EquipSlot.Ring4, new Point(12, 166) },
                { EquipSlot.Pocket, new Point(12, 203) }, { EquipSlot.Pendant1, new Point(49, 55) },
                { EquipSlot.Pendant2, new Point(49, 92) }, { EquipSlot.Weapon, new Point(49, 129) },
                { EquipSlot.Belt, new Point(49, 166) }, { EquipSlot.Cap, new Point(135, 55) },
                { EquipSlot.FaceAccessory, new Point(135, 92) }, { EquipSlot.EyeAccessory, new Point(135, 129) },
                { EquipSlot.Top, new Point(135, 166) }, { EquipSlot.Bottom, new Point(135, 203) },
                { EquipSlot.Shoes, new Point(135, 240) }, { EquipSlot.Earring, new Point(172, 55) },
                { EquipSlot.Shoulder, new Point(172, 92) }, { EquipSlot.Glove, new Point(172, 129) },
                { EquipSlot.Shield, new Point(172, 166) }, { EquipSlot.Cape, new Point(172, 203) }
            };
            _dragonSlotPositions = new Dictionary<DragonEquipSlot, Point>
            {
                { DragonEquipSlot.Mask, new Point(88, 45) }, { DragonEquipSlot.Wings, new Point(88, 74) },
                { DragonEquipSlot.Pendant, new Point(50, 92) }, { DragonEquipSlot.Tail, new Point(95, 104) }
            };
            _petButtonIconPositions = new[] { new Point(12, 11), new Point(49, 11), new Point(86, 11) };
            equippedItems = new Dictionary<EquipSlot, EquipSlotData>();
            _overlayPixel = new Texture2D(device, 1, 1);
            _overlayPixel.SetData(new[] { Color.White });
        }

        public override void SetFont(SpriteFont font) => _font = font;

        public void SetCompanionPanes(IDXObject petPaneFrame, IDXObject dragonPaneFrame)
        {
            _petPaneFrame = petPaneFrame;
            _dragonPaneFrame = dragonPaneFrame;
        }

        public void InitializeCompanionButtons(UIObject petEquipShowButton, UIObject petEquipHideButton, UIObject dragonEquipButton, UIObject pet1Button, UIObject pet2Button, UIObject pet3Button)
        {
            _btnPetEquipShow = petEquipShowButton;
            _btnPetEquipHide = petEquipHideButton;
            _btnDragonEquip = dragonEquipButton;
            _petButtons[0] = pet1Button;
            _petButtons[1] = pet2Button;
            _petButtons[2] = pet3Button;

            if (_btnPetEquipShow != null)
            {
                AddButton(_btnPetEquipShow);
                _btnPetEquipShow.ButtonClickReleased += _ => { _companionPaneMode = CompanionPaneMode.Pet; EnsureSelectedPet(); UpdateCompanionButtonStates(); };
            }
            if (_btnPetEquipHide != null)
            {
                AddButton(_btnPetEquipHide);
                _btnPetEquipHide.ButtonClickReleased += _ => { _companionPaneMode = CompanionPaneMode.Hidden; UpdateCompanionButtonStates(); };
            }
            if (_btnDragonEquip != null)
            {
                AddButton(_btnDragonEquip);
                _btnDragonEquip.ButtonClickReleased += _ =>
                {
                    _companionPaneMode = _companionPaneMode == CompanionPaneMode.Dragon ? CompanionPaneMode.Hidden : CompanionPaneMode.Dragon;
                    UpdateCompanionButtonStates();
                };
            }

            for (int i = 0; i < _petButtons.Length; i++)
            {
                UIObject petButton = _petButtons[i];
                if (petButton == null)
                    continue;
                int petIndex = i;
                AddButton(petButton);
                petButton.ButtonClickReleased += _ =>
                {
                    _companionPaneMode = CompanionPaneMode.Pet;
                    _selectedPetIndex = petIndex;
                    EnsureSelectedPet();
                    UpdateCompanionButtonStates();
                };
            }

            UpdateCompanionButtonStates();
        }

        public void SetPetController(PetController petController)
        {
            _petController = petController;
            EnsureSelectedPet();
            UpdateCompanionButtonStates();
        }

        public void SetDragonEquipmentController(DragonEquipmentController dragonEquipmentController)
        {
            _dragonEquipmentController = dragonEquipmentController;
        }

        public void SetDragonPaneAvailable(bool available)
        {
            if (!available && _companionPaneMode == CompanionPaneMode.Dragon)
            {
                _companionPaneMode = CompanionPaneMode.Hidden;
            }

            if (_btnDragonEquip != null)
            {
                _btnDragonEquip.ButtonVisible = available;
            }
        }

        protected override void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime, int mapShiftX, int mapShiftY, int centerX, int centerY, ReflectionDrawableBoundary drawReflectionInfo, RenderParameters renderParameters, int TickCount)
        {
            foreach ((EquipSlot uiSlot, Point slotPosition) in slotPositions)
            {
                int slotX = Position.X + slotPosition.X;
                int slotY = Position.Y + slotPosition.Y;
                CharacterPart part = ResolveEquippedPart(uiSlot);
                EquipSlotData slotData = equippedItems.TryGetValue(uiSlot, out EquipSlotData data) ? data : null;
                if (part != null || slotData != null)
                    DrawEquippedItemIcon(sprite, part, slotData, slotX, slotY);

                CharacterEquipSlot? characterSlot = MapToCharacterEquipSlot(uiSlot);
                if (!characterSlot.HasValue)
                    continue;

                EquipSlotVisualState visualState = EquipSlotStateResolver.ResolveVisualState(_characterBuild, characterSlot.Value);
                if (visualState.IsDisabled)
                    DrawDisabledOverlay(sprite, slotX, slotY, visualState);
            }

            DrawCompanionPane(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);
        }

        protected override void DrawOverlay(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime, int mapShiftX, int mapShiftY, int centerX, int centerY, ReflectionDrawableBoundary drawReflectionInfo, RenderParameters renderParameters, int TickCount)
        {
            DrawHoverTooltip(sprite, renderParameters.RenderWidth, renderParameters.RenderHeight);
            DrawDraggedItemOverlay(sprite);
        }

        public void EquipItem(EquipSlot slot, int itemId, Texture2D texture, string itemName = "")
        {
            equippedItems[slot] = new EquipSlotData { ItemId = itemId, ItemTexture = texture, ItemName = itemName };
        }

        public void UnequipItem(EquipSlot slot)
        {
            if (equippedItems.ContainsKey(slot))
                equippedItems.Remove(slot);
        }

        public EquipSlotData GetEquippedItem(EquipSlot slot) => equippedItems.TryGetValue(slot, out EquipSlotData data) ? data : null;
        public void ClearAllEquipment() => equippedItems.Clear();

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            MouseState mouseState = Mouse.GetState();
            _lastMousePosition = new Point(mouseState.X, mouseState.Y);
            if (_isDraggingItem)
                _draggedItemPosition = _lastMousePosition;
            _hoveredSlot = GetSlotAtPosition(mouseState.X, mouseState.Y);
            _hoveredPetIndex = GetHoveredPetIndex(mouseState.X, mouseState.Y);
            _hoveredDragonSlot = GetHoveredDragonSlot(mouseState.X, mouseState.Y);
        }

        public void ProcessPendingEquipmentChange(InventoryUI inventoryWindow)
        {
            if (_pendingEquipmentChange == null || EquipmentChangeResultRequested == null)
            {
                return;
            }

            EquipmentChangeResult result = EquipmentChangeResultRequested.Invoke(new EquipmentChangeResolutionQuery
            {
                RequestId = _pendingEquipmentChange.RequestId,
                OwnerKind = _pendingEquipmentChange.OwnerKind,
                OwnerSessionId = _pendingEquipmentChange.OwnerSessionId,
                RequestedAtTick = _pendingEquipmentChange.RequestedAtTick
            });
            if (result == null || result.IsPending)
            {
                return;
            }

            PendingEquipmentChange pendingChange = _pendingEquipmentChange;
            _pendingEquipmentChange = null;

            if (!result.Accepted)
            {
                if (pendingChange.SourceInventoryLocked && inventoryWindow != null)
                {
                    inventoryWindow.TryClearPendingRequestState(pendingChange.RequestId);
                }

                NotifyEquipmentEquipBlocked(result.RejectReason);
                return;
            }

            if (EquipmentChangeClientParity.IsResolvedResultStale(_characterBuild, result))
            {
                if (pendingChange.SourceInventoryLocked && inventoryWindow != null)
                {
                    inventoryWindow.TryClearPendingRequestState(pendingChange.RequestId);
                }

                NotifyEquipmentEquipBlocked(EquipmentChangeClientParity.StaleCompletionMessage);
                return;
            }

            if (inventoryWindow == null)
            {
                return;
            }

            switch (pendingChange.Kind)
            {
                case EquipmentChangeRequestKind.InventoryToCharacter:
                    inventoryWindow.TryRemovePendingRequestSlot(pendingChange.RequestId, out _);
                    IReadOnlyList<InventorySlotData> displacedSlots = CreateInventorySlots(result.DisplacedParts);
                    if (displacedSlots != null)
                    {
                        for (int i = 0; i < displacedSlots.Count; i++)
                        {
                            InventorySlotData displacedSlot = displacedSlots[i];
                            if (displacedSlot != null)
                            {
                                inventoryWindow.AddItem(ResolveInventoryTypeForSlot(displacedSlot), displacedSlot);
                            }
                        }
                    }
                    break;

                case EquipmentChangeRequestKind.CharacterToInventory:
                    if (pendingChange.SourceInventoryLocked)
                    {
                        inventoryWindow.TryClearPendingRequestState(pendingChange.RequestId);
                    }

                    if (result.ReturnedPart != null)
                    {
                        InventorySlotData returnedSlot = CreateInventorySlot(result.ReturnedPart);
                        if (returnedSlot != null)
                        {
                            inventoryWindow.AddItem(ResolveInventoryTypeForSlot(returnedSlot), returnedSlot);
                        }
                    }
                    break;
            }
        }

        public bool TryLockPendingInventorySource(InventoryUI inventoryWindow)
        {
            if (inventoryWindow == null
                || _pendingEquipmentChange == null
                || _pendingEquipmentChange.Kind != EquipmentChangeRequestKind.InventoryToCharacter
                || _pendingEquipmentChange.SourceInventoryLocked)
            {
                return false;
            }

            bool locked = inventoryWindow.TrySetPendingRequestState(
                _pendingEquipmentChange.SourceInventoryType,
                _pendingEquipmentChange.SourceInventoryIndex,
                _pendingEquipmentChange.RequestId,
                isPending: true);
            _pendingEquipmentChange.SourceInventoryLocked = locked;
            return locked;
        }

        public bool TryHandleInventoryDrop(
            int mouseX,
            int mouseY,
            InventoryType sourceInventoryType,
            int sourceInventoryIndex,
            InventorySlotData draggedSlotData,
            out IReadOnlyList<InventorySlotData> displacedSlots)
        {
            displacedSlots = Array.Empty<InventorySlotData>();
            if (_companionPaneMode != CompanionPaneMode.Hidden
                || draggedSlotData == null
                || draggedSlotData.IsDisabled
                || sourceInventoryType != InventoryType.EQUIP)
            {
                return false;
            }

            if (HasPendingEquipmentChange)
            {
                NotifyEquipmentEquipBlocked("An equipment change is already pending.");
                return false;
            }

            EquipSlot? targetUiSlot = GetSlotAtPosition(mouseX, mouseY);
            if (!targetUiSlot.HasValue)
            {
                return false;
            }

            CharacterEquipSlot? targetSlot = MapToCharacterEquipSlot(targetUiSlot.Value);
            if (!targetSlot.HasValue)
            {
                return false;
            }

            EquipSlotVisualState targetState = EquipSlotStateResolver.ResolveVisualState(_characterBuild, targetSlot.Value);
            if (targetState.IsDisabled)
            {
                NotifyEquipmentEquipBlocked(targetState.Message);
                return false;
            }

            CharacterPart incomingPart = draggedSlotData.TooltipPart?.Clone();
            if (incomingPart != null)
            {
                if (!CanDisplayPartInSlot(incomingPart, targetUiSlot.Value))
                {
                    NotifyEquipmentEquipBlocked(EquipUIBigBang.BuildSlotMismatchRejectReason(incomingPart));
                    return false;
                }

                if (!EquipUIBigBang.TryGetEquipRequirementRejectReason(incomingPart, _characterBuild, out string requirementRejectReason))
                {
                    NotifyEquipmentEquipBlocked(requirementRejectReason);
                    return false;
                }
            }

            string restrictionMessage = EquipmentEquipGuard?.Invoke(draggedSlotData.ItemId);
            if (!string.IsNullOrWhiteSpace(restrictionMessage))
            {
                NotifyEquipmentEquipBlocked(restrictionMessage);
                return false;
            }

            CharacterEquipSlot resolvedTargetSlot = incomingPart != null
                ? ResolveTargetSlot(targetUiSlot.Value, incomingPart)
                : targetSlot.Value;
            EquipmentChangeResult changeResult = TryRequestInventoryToCharacterChange(
                sourceInventoryType,
                sourceInventoryIndex,
                draggedSlotData,
                incomingPart,
                resolvedTargetSlot);
            if (changeResult != null)
            {
                if (changeResult.IsPending)
                {
                    return true;
                }

                if (!changeResult.Accepted)
                {
                    NotifyEquipmentEquipBlocked(changeResult.RejectReason);
                    return false;
                }

                displacedSlots = CreateInventorySlots(changeResult.DisplacedParts);
                return true;
            }

            CharacterPart resolvedIncomingPart = incomingPart?.Clone();
            if (resolvedIncomingPart == null)
            {
                string itemName = string.IsNullOrWhiteSpace(draggedSlotData.ItemName)
                    ? $"Item #{draggedSlotData.ItemId}"
                    : draggedSlotData.ItemName;
                NotifyEquipmentEquipBlocked($"Unable to load {itemName} as an equipment item.");
                return false;
            }

            IReadOnlyList<CharacterPart> displacedParts = _characterBuild?.PlaceEquipment(resolvedIncomingPart, resolvedTargetSlot)
                ?? Array.Empty<CharacterPart>();
            displacedSlots = CreateInventorySlots(displacedParts);
            return true;
        }

        public bool HandlesEquipmentInteractionPoint(int mouseX, int mouseY)
        {
            return _companionPaneMode == CompanionPaneMode.Hidden && GetSlotAtPosition(mouseX, mouseY).HasValue;
        }

        public void OnEquipmentMouseDown(int mouseX, int mouseY)
        {
            _lastMousePosition = new Point(mouseX, mouseY);
            EquipSlot? slot = GetSlotAtPosition(mouseX, mouseY);
            if (!slot.HasValue)
                return;

            CharacterPart part = ResolveEquippedPart(slot.Value);
            CharacterEquipSlot? characterSlot = MapToCharacterEquipSlot(slot.Value);
            if (part == null || !characterSlot.HasValue)
                return;

            EquipSlotVisualState visualState = EquipSlotStateResolver.ResolveVisualState(_characterBuild, characterSlot.Value);
            if (visualState.IsDisabled)
                return;

            _isDraggingItem = true;
            _draggedSlot = slot;
            _draggedPart = part;
            _draggedItemPosition = _lastMousePosition;
            _hoveredSlot = null;
        }

        public void OnEquipmentMouseMove(int mouseX, int mouseY)
        {
            _lastMousePosition = new Point(mouseX, mouseY);
            if (_isDraggingItem)
                _draggedItemPosition = _lastMousePosition;
        }

        public bool OnEquipmentMouseUp(int mouseX, int mouseY)
        {
            _lastMousePosition = new Point(mouseX, mouseY);
            if (!_isDraggingItem || _draggedPart == null)
            {
                CancelEquipmentDrag();
                return false;
            }

            bool moved = TryDropDraggedEquipment(mouseX, mouseY);
            CancelEquipmentDrag();
            return moved;
        }

        public bool TryCommitDraggedCharacterRemoval(out InventorySlotData slotData)
        {
            slotData = null;
            if (!HasDraggedCharacterItem)
            {
                return false;
            }

            if (HasPendingEquipmentChange)
            {
                NotifyEquipmentEquipBlocked("An equipment change is already pending.");
                return false;
            }

            EquipmentChangeResult changeResult = TryRequestCharacterToInventoryChange();
            if (changeResult != null)
            {
                if (changeResult.IsPending)
                {
                    return true;
                }

                if (!changeResult.Accepted)
                {
                    NotifyEquipmentEquipBlocked(changeResult.RejectReason);
                    return false;
                }

                slotData = CreateInventorySlot(changeResult.ReturnedPart);
                return slotData != null;
            }

            CharacterPart removedPart = _characterBuild?.Unequip(_draggedPart.Slot);
            if (removedPart == null)
            {
                return false;
            }

            slotData = CreateInventorySlot(removedPart);
            return slotData != null;
        }

        public void CancelEquipmentDrag()
        {
            _isDraggingItem = false;
            _draggedSlot = null;
            _draggedPart = null;
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                _previousMouseState = mouseState;
                return false;
            }

            if (TryRequestItemUpgrade(mouseState))
            {
                _previousMouseState = mouseState;
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            bool handled = base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
            _previousMouseState = mouseState;
            return handled;
        }

        private bool TryRequestItemUpgrade(MouseState mouseState)
        {
            bool rightJustPressed = mouseState.RightButton == ButtonState.Pressed &&
                                    _previousMouseState.RightButton == ButtonState.Released;
            if (!rightJustPressed || !_hoveredSlot.HasValue)
            {
                return false;
            }

            CharacterEquipSlot? characterSlot = MapToCharacterEquipSlot(_hoveredSlot.Value);
            CharacterPart part = ResolveEquippedPart(_hoveredSlot.Value);
            if (!characterSlot.HasValue || !ItemUpgradeUI.CanUpgrade(characterSlot.Value, part))
            {
                return false;
            }

            ItemUpgradeRequested?.Invoke(characterSlot.Value);
            return true;
        }

        private void DrawCompanionPane(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime, ReflectionDrawableBoundary drawReflectionInfo)
        {
            switch (_companionPaneMode)
            {
                case CompanionPaneMode.Pet:
                    if (_petPaneFrame != null)
                    {
                        Point panePosition = GetCompanionPanePosition(_petPaneFrame);
                        _petPaneFrame.DrawBackground(sprite, skeletonMeshRenderer, gameTime, panePosition.X, panePosition.Y, Color.White, false, drawReflectionInfo);
                        DrawPetPaneContents(sprite, panePosition);
                    }
                    break;
                case CompanionPaneMode.Dragon:
                    if (_dragonPaneFrame != null)
                    {
                        Point panePosition = GetCompanionPanePosition(_dragonPaneFrame);
                        _dragonPaneFrame.DrawBackground(sprite, skeletonMeshRenderer, gameTime, panePosition.X, panePosition.Y, Color.White, false, drawReflectionInfo);
                        DrawDragonPaneContents(sprite, panePosition);
                    }
                    break;
            }
        }

        private void DrawPetPaneContents(SpriteBatch sprite, Point panePosition)
        {
            IReadOnlyList<PetRuntime> pets = _petController?.ActivePets;
            if (pets == null || pets.Count == 0)
            {
                DrawCompanionMessage(sprite, panePosition, _petPaneFrame, "Summon pets in the simulator to populate the pet equip page.");
                return;
            }

            int selectedIndex = Math.Clamp(_selectedPetIndex, 0, pets.Count - 1);
            PetRuntime selectedPet = pets[selectedIndex];
            for (int i = 0; i < Math.Min(_petButtonIconPositions.Length, pets.Count); i++)
            {
                IDXObject icon = pets[i].Definition?.IconRaw ?? pets[i].Definition?.Icon;
                if (icon == null)
                    continue;

                Point iconPosition = _petButtonIconPositions[i];
                icon.DrawBackground(sprite, null, null, panePosition.X + iconPosition.X, panePosition.Y + iconPosition.Y, Color.White, false, null);
            }

            IDXObject selectedIcon = selectedPet.Definition?.IconRaw ?? selectedPet.Definition?.Icon;
            selectedIcon?.DrawBackground(sprite, null, null, panePosition.X + 108, panePosition.Y + 71, Color.White, false, null);

            if (_font == null)
                return;

            string autoLootText = selectedPet.AutoLootEnabled ? "AUTO" : "OFF";
            Color autoLootColor = selectedPet.AutoLootEnabled ? new Color(187, 244, 189) : new Color(255, 211, 140);
            Vector2 autoLootSize = _font.MeasureString(autoLootText);
            DrawTooltipText(sprite, autoLootText, new Vector2(panePosition.X + 77 - (autoLootSize.X / 2f), panePosition.Y + 86 - (autoLootSize.Y / 2f)), autoLootColor);

            float textX = panePosition.X + COMPANION_MESSAGE_PADDING;
            float textY = panePosition.Y + 142;
            DrawTooltipText(sprite, selectedPet.Name, new Vector2(textX, textY), new Color(255, 220, 120));
            textY += _font.LineSpacing + COMPANION_TEXT_LINE_GAP;
            DrawTooltipText(sprite, $"Item ID: {selectedPet.ItemId}", new Vector2(textX, textY), Color.White);
            textY += _font.LineSpacing + COMPANION_TEXT_LINE_GAP;
            DrawTooltipText(sprite, $"Pet {selectedIndex + 1}  Auto Loot: {(selectedPet.AutoLootEnabled ? "On" : "Off")}", new Vector2(textX, textY), new Color(181, 224, 255));
        }

        private void DrawDragonPaneContents(SpriteBatch sprite, Point panePosition)
        {
            bool hasAnyItems = false;
            foreach ((DragonEquipSlot slot, Point slotPosition) in _dragonSlotPositions)
            {
                if (_dragonEquipmentController == null || !_dragonEquipmentController.TryGetItem(slot, out CompanionEquipItem item))
                    continue;

                hasAnyItems = true;
                IDXObject icon = item.IconRaw ?? item.Icon ?? item.CharacterPart?.IconRaw ?? item.CharacterPart?.Icon;
                icon?.DrawBackground(sprite, null, null, panePosition.X + slotPosition.X, panePosition.Y + slotPosition.Y, Color.White, false, null);
            }

            if (!hasAnyItems)
                DrawCompanionMessage(sprite, panePosition, _dragonPaneFrame, "Dragon equipment defaults are loaded, but no dragon items are available to draw.");
        }

        private void DrawCompanionMessage(SpriteBatch sprite, Point panePosition, IDXObject paneFrame, string message)
        {
            if (_font == null || paneFrame == null || string.IsNullOrWhiteSpace(message))
                return;

            float maxWidth = Math.Max(80, paneFrame.Width - (COMPANION_MESSAGE_PADDING * 2));
            string[] lines = WrapTooltipText(message, maxWidth);
            float y = panePosition.Y + 132;
            for (int i = 0; i < lines.Length; i++)
            {
                DrawTooltipText(sprite, lines[i], new Vector2(panePosition.X + COMPANION_MESSAGE_PADDING, y), new Color(216, 216, 216));
                y += _font.LineSpacing;
            }
        }

        private void UpdateCompanionButtonStates()
        {
            bool petPaneVisible = _companionPaneMode == CompanionPaneMode.Pet && _petPaneFrame != null;
            bool dragonPaneVisible = _companionPaneMode == CompanionPaneMode.Dragon && _dragonPaneFrame != null;

            if (_btnPetEquipShow != null)
            {
                _btnPetEquipShow.ButtonVisible = !petPaneVisible;
                _btnPetEquipShow.SetButtonState(UIObjectState.Normal);
            }
            if (_btnPetEquipHide != null)
            {
                _btnPetEquipHide.ButtonVisible = petPaneVisible;
                _btnPetEquipHide.SetButtonState(petPaneVisible ? UIObjectState.Pressed : UIObjectState.Normal);
            }
            if (_btnDragonEquip != null)
            {
                _btnDragonEquip.ButtonVisible = true;
                _btnDragonEquip.SetButtonState(dragonPaneVisible ? UIObjectState.Pressed : UIObjectState.Normal);
            }

            IReadOnlyList<PetRuntime> pets = _petController?.ActivePets;
            for (int i = 0; i < _petButtons.Length; i++)
            {
                UIObject petButton = _petButtons[i];
                if (petButton == null)
                    continue;

                bool hasPet = pets != null && i < pets.Count;
                petButton.ButtonVisible = petPaneVisible;
                petButton.SetButtonState(petPaneVisible && hasPet && i == _selectedPetIndex ? UIObjectState.Pressed : UIObjectState.Normal);
            }
        }

        private void EnsureSelectedPet()
        {
            IReadOnlyList<PetRuntime> pets = _petController?.ActivePets;
            _selectedPetIndex = pets == null || pets.Count == 0 ? 0 : Math.Clamp(_selectedPetIndex, 0, pets.Count - 1);
        }

        private Point GetCompanionPanePosition(IDXObject paneFrame)
        {
            return new Point(Position.X - paneFrame.Width + COMPANION_PANE_ATTACH_X, Position.Y + COMPANION_PANE_ATTACH_Y);
        }

        private void DrawEquippedItemIcon(SpriteBatch sprite, CharacterPart part, EquipSlotData slotData, int slotX, int slotY)
        {
            IDXObject icon = part?.IconRaw ?? part?.Icon ?? slotData?.ItemIcon;
            if (icon != null)
            {
                icon.DrawBackground(sprite, null, null, slotX, slotY, Color.White, false, null);
                return;
            }
            if (slotData?.ItemTexture != null)
                sprite.Draw(slotData.ItemTexture, new Rectangle(slotX, slotY, SLOT_SIZE, SLOT_SIZE), Color.White);
        }

        private void DrawDisabledOverlay(SpriteBatch sprite, int slotX, int slotY, EquipSlotVisualState visualState)
        {
            Color overlay = visualState.IsExpired ? new Color(110, 30, 30, 180) : visualState.IsBroken ? new Color(90, 60, 20, 180) : new Color(20, 20, 20, 145);
            sprite.Draw(_overlayPixel, new Rectangle(slotX, slotY, SLOT_SIZE, SLOT_SIZE), overlay);
            sprite.Draw(_overlayPixel, new Rectangle(slotX, slotY, SLOT_SIZE, 2), Color.Black);
            sprite.Draw(_overlayPixel, new Rectangle(slotX, slotY + SLOT_SIZE - 2, SLOT_SIZE, 2), Color.Black);
            sprite.Draw(_overlayPixel, new Rectangle(slotX, slotY, 2, SLOT_SIZE), Color.Black);
            sprite.Draw(_overlayPixel, new Rectangle(slotX + SLOT_SIZE - 2, slotY, 2, SLOT_SIZE), Color.Black);
        }

        private void DrawHoverTooltip(SpriteBatch sprite, int renderWidth, int renderHeight)
        {
            if (_font == null)
                return;

            if (TryBuildPetTooltip(out string petTitle, out string petLine, out IDXObject petIcon))
            {
                DrawTooltip(sprite, petTitle, petLine, null, petIcon, renderWidth, renderHeight);
                return;
            }
            if (TryBuildDragonTooltip(out string dragonTitle, out string dragonLine, out string dragonDescription, out IDXObject dragonIcon))
            {
                DrawTooltip(sprite, dragonTitle, dragonLine, dragonDescription, dragonIcon, renderWidth, renderHeight);
                return;
            }
            if (!_hoveredSlot.HasValue)
                return;

            if (_isDraggingItem && _draggedPart != null)
            {
                DrawDraggedComparisonTooltip(sprite, _hoveredSlot.Value, renderWidth, renderHeight);
                return;
            }

            string title;
            string line;
            CharacterPart part = ResolveEquippedPart(_hoveredSlot.Value);
            if (part != null)
            {
                title = string.IsNullOrWhiteSpace(part.Name) ? $"Equip {part.ItemId}" : part.Name;
                line = $"Item ID: {part.ItemId}";
                CharacterEquipSlot? characterSlot = MapToCharacterEquipSlot(_hoveredSlot.Value);
                string description = null;
                if (characterSlot.HasValue)
                {
                    EquipSlotVisualState state = EquipSlotStateResolver.ResolveVisualState(_characterBuild, characterSlot.Value);
                    if (!string.IsNullOrWhiteSpace(state.Message))
                        line = $"{line}  {state.Message}";
                    CharacterPart hiddenPart = EquipSlotStateResolver.ResolveUnderlyingPart(_characterBuild, characterSlot.Value);
                    if (part.IsCash && hiddenPart != null)
                    {
                        string hiddenName = string.IsNullOrWhiteSpace(hiddenPart.Name)
                            ? $"Equip {hiddenPart.ItemId}"
                            : hiddenPart.Name;
                        description = $"Cash appearance active. Base equip: {hiddenName}.";
                    }
                }
                DrawTooltip(sprite, title, line, description, part.IconRaw ?? part.Icon, renderWidth, renderHeight);
                return;
            }
            if (equippedItems.TryGetValue(_hoveredSlot.Value, out EquipSlotData slotData))
            {
                DrawTooltip(sprite, slotData.ItemName, $"Item ID: {slotData.ItemId}", null, slotData.ItemIcon, renderWidth, renderHeight);
                return;
            }

            CharacterEquipSlot? emptySlot = MapToCharacterEquipSlot(_hoveredSlot.Value);
            if (!emptySlot.HasValue)
                return;

            EquipSlotVisualState emptyState = EquipSlotStateResolver.ResolveVisualState(_characterBuild, emptySlot.Value);
            if (!emptyState.IsDisabled)
                return;

            DrawTooltip(sprite, ResolveSlotLabel(_hoveredSlot.Value), emptyState.Message, null, null, renderWidth, renderHeight);
        }

        private void DrawDraggedComparisonTooltip(SpriteBatch sprite, EquipSlot hoveredSlot, int renderWidth, int renderHeight)
        {
            string title = string.IsNullOrWhiteSpace(_draggedPart.Name) ? $"Equip {_draggedPart.ItemId}" : _draggedPart.Name;
            string line = $"Drop on {ResolveSlotLabel(hoveredSlot)}";
            string description = BuildDragCompareDescription(hoveredSlot);
            DrawTooltip(sprite, title, line, description, _draggedPart.IconRaw ?? _draggedPart.Icon, renderWidth, renderHeight);
        }

        private bool TryBuildPetTooltip(out string title, out string line, out IDXObject icon)
        {
            title = null;
            line = null;
            icon = null;
            if (_hoveredPetIndex == null)
                return false;

            IReadOnlyList<PetRuntime> pets = _petController?.ActivePets;
            int petIndex = _hoveredPetIndex.Value;
            if (pets == null || petIndex < 0 || petIndex >= pets.Count)
                return false;

            PetRuntime pet = pets[petIndex];
            title = string.IsNullOrWhiteSpace(pet.Name) ? $"Pet {pet.ItemId}" : pet.Name;
            line = $"Item ID: {pet.ItemId}  Pet {petIndex + 1}  Auto Loot: {(pet.AutoLootEnabled ? "On" : "Off")}";
            icon = pet.Definition?.IconRaw ?? pet.Definition?.Icon;
            return true;
        }

        private bool TryBuildDragonTooltip(out string title, out string line, out string description, out IDXObject icon)
        {
            title = null;
            line = null;
            description = null;
            icon = null;
            if (_hoveredDragonSlot == null || _dragonEquipmentController == null || !_dragonEquipmentController.TryGetItem(_hoveredDragonSlot.Value, out CompanionEquipItem item))
                return false;

            title = string.IsNullOrWhiteSpace(item.Name) ? $"Dragon Equip {item.ItemId}" : item.Name;
            line = $"Item ID: {item.ItemId}  Slot: {ResolveDragonSlotLabel(_hoveredDragonSlot.Value)}";
            description = BuildDragonDescription(item);
            icon = item.IconRaw ?? item.Icon ?? item.CharacterPart?.IconRaw ?? item.CharacterPart?.Icon;
            return true;
        }

        private void DrawTooltip(SpriteBatch sprite, string title, string line, string description, IDXObject icon, int renderWidth, int renderHeight)
        {
            Vector2 titleSize = _font.MeasureString(title);
            Vector2 lineSize = string.IsNullOrWhiteSpace(line) ? Vector2.Zero : _font.MeasureString(line);
            Vector2 descriptionSize = string.IsNullOrWhiteSpace(description) ? Vector2.Zero : _font.MeasureString(description);
            int width = (int)Math.Ceiling(Math.Max(titleSize.X, Math.Max(lineSize.X, descriptionSize.X))) + (TOOLTIP_PADDING * 2) + SLOT_SIZE + 8;
            int height = (int)Math.Ceiling(titleSize.Y + (lineSize.Y > 0 ? lineSize.Y + 4 : 0) + (descriptionSize.Y > 0 ? descriptionSize.Y + 4 : 0)) + (TOOLTIP_PADDING * 2);
            int x = Math.Min(_lastMousePosition.X + TOOLTIP_OFFSET_X, Math.Max(TOOLTIP_PADDING, renderWidth - width - TOOLTIP_PADDING));
            int y = _lastMousePosition.Y - height - TOOLTIP_OFFSET_Y;
            if (y < TOOLTIP_PADDING)
                y = Math.Min(renderHeight - height - TOOLTIP_PADDING, _lastMousePosition.Y + TOOLTIP_OFFSET_Y);

            Rectangle rect = new Rectangle(x, y, width, height);
            sprite.Draw(_overlayPixel, rect, new Color(18, 18, 26, 235));
            sprite.Draw(_overlayPixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Color(214, 174, 82));
            sprite.Draw(_overlayPixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), new Color(214, 174, 82));
            sprite.Draw(_overlayPixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), new Color(214, 174, 82));
            sprite.Draw(_overlayPixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), new Color(214, 174, 82));

            int iconX = rect.X + TOOLTIP_PADDING;
            int textX = iconX + SLOT_SIZE + 8;
            int textY = rect.Y + TOOLTIP_PADDING;
            icon?.DrawBackground(sprite, null, null, iconX, textY, Color.White, false, null);
            DrawTooltipText(sprite, title, new Vector2(textX, textY), new Color(255, 220, 120));
            float currentY = textY + titleSize.Y + 4;
            if (!string.IsNullOrWhiteSpace(line))
            {
                DrawTooltipText(sprite, line, new Vector2(textX, currentY), Color.White);
                currentY += lineSize.Y + 4;
            }
            if (!string.IsNullOrWhiteSpace(description))
                DrawTooltipText(sprite, description, new Vector2(textX, currentY), new Color(216, 216, 216));
        }

        private void DrawDraggedItemOverlay(SpriteBatch sprite)
        {
            if (!_isDraggingItem || _draggedPart == null)
                return;

            IDXObject icon = _draggedPart.IconRaw ?? _draggedPart.Icon;
            int slotX = _draggedItemPosition.X - (SLOT_SIZE / 2);
            int slotY = _draggedItemPosition.Y - (SLOT_SIZE / 2);
            if (icon != null)
            {
                icon.DrawBackground(sprite, null, null, slotX, slotY, Color.White * 0.85f, false, null);
            }
        }

        private void DrawTooltipText(SpriteBatch sprite, string text, Vector2 position, Color color)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            sprite.DrawString(_font, text, position + Vector2.One, Color.Black);
            sprite.DrawString(_font, text, position, color);
        }

        private string[] WrapTooltipText(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
                return Array.Empty<string>();

            var lines = new List<string>();
            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string currentLine = string.Empty;
            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                if (!string.IsNullOrEmpty(currentLine) && _font.MeasureString(candidate).X > maxWidth)
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = candidate;
                }
            }
            if (!string.IsNullOrEmpty(currentLine))
                lines.Add(currentLine);
            return lines.ToArray();
        }

        private EquipSlot? GetSlotAtPosition(int mouseX, int mouseY)
        {
            foreach ((EquipSlot slot, Point slotPosition) in slotPositions)
            {
                Rectangle slotRect = new Rectangle(Position.X + slotPosition.X, Position.Y + slotPosition.Y, SLOT_SIZE, SLOT_SIZE);
                if (slotRect.Contains(mouseX, mouseY))
                    return slot;
            }
            return null;
        }

        private int? GetHoveredPetIndex(int mouseX, int mouseY)
        {
            if (_companionPaneMode != CompanionPaneMode.Pet)
                return null;

            IReadOnlyList<PetRuntime> pets = _petController?.ActivePets;
            if (pets == null || pets.Count == 0)
                return null;

            for (int i = 0; i < _petButtons.Length; i++)
            {
                UIObject button = _petButtons[i];
                if (button == null || !button.ButtonVisible)
                    continue;

                Rectangle buttonRect = new Rectangle(
                    Position.X + button.X,
                    Position.Y + button.Y,
                    Math.Max(button.CanvasSnapshotWidth, SLOT_SIZE),
                    Math.Max(button.CanvasSnapshotHeight, SLOT_SIZE));
                if (buttonRect.Contains(mouseX, mouseY) && i < pets.Count)
                    return i;
            }
            return null;
        }

        private DragonEquipSlot? GetHoveredDragonSlot(int mouseX, int mouseY)
        {
            if (_companionPaneMode != CompanionPaneMode.Dragon || _dragonPaneFrame == null)
                return null;

            Point panePosition = GetCompanionPanePosition(_dragonPaneFrame);
            foreach ((DragonEquipSlot slot, Point slotPosition) in _dragonSlotPositions)
            {
                if (_dragonEquipmentController == null || !_dragonEquipmentController.TryGetItem(slot, out _))
                    continue;

                Rectangle slotRect = new Rectangle(panePosition.X + slotPosition.X, panePosition.Y + slotPosition.Y, SLOT_SIZE, SLOT_SIZE);
                if (slotRect.Contains(mouseX, mouseY))
                    return slot;
            }
            return null;
        }

        private CharacterPart ResolveEquippedPart(EquipSlot uiSlot)
        {
            CharacterEquipSlot? characterSlot = MapToCharacterEquipSlot(uiSlot);
            return characterSlot.HasValue ? EquipSlotStateResolver.ResolveDisplayedPart(_characterBuild, characterSlot.Value) : null;
        }

        private bool TryDropDraggedEquipment(int mouseX, int mouseY)
        {
            EquipSlot? targetUiSlot = GetSlotAtPosition(mouseX, mouseY);
            if (!targetUiSlot.HasValue || _draggedPart == null)
                return false;

            CharacterEquipSlot? targetSlot = MapToCharacterEquipSlot(targetUiSlot.Value);
            if (!targetSlot.HasValue)
                return false;

            EquipSlotVisualState targetState = EquipSlotStateResolver.ResolveVisualState(_characterBuild, targetSlot.Value);
            if (targetState.IsDisabled)
            {
                NotifyEquipmentEquipBlocked(targetState.Message);
                return false;
            }

            if (!CanDisplayPartInSlot(_draggedPart, targetUiSlot.Value))
            {
                NotifyEquipmentEquipBlocked(EquipUIBigBang.BuildSlotMismatchRejectReason(_draggedPart));
                return false;
            }

            CharacterPart targetPart = ResolveEquippedPart(targetUiSlot.Value);
            if (targetPart != null && !CanDisplayPartInSlot(targetPart, _draggedSlot ?? targetUiSlot.Value))
            {
                NotifyEquipmentEquipBlocked(BuildSwapRejectReason(targetPart, _draggedSlot));
                return false;
            }

            CharacterEquipSlot resolvedTargetSlot = ResolveTargetSlot(targetUiSlot.Value, _draggedPart);
            if (_draggedPart.Slot == resolvedTargetSlot || (targetPart != null && ReferenceEquals(targetPart, _draggedPart)))
                return true;

            if (HasPendingEquipmentChange)
            {
                NotifyEquipmentEquipBlocked("An equipment change is already pending.");
                return false;
            }

            EquipmentChangeResult changeResult = TryRequestCharacterToCharacterChange(resolvedTargetSlot);
            if (changeResult != null)
            {
                if (changeResult.IsPending)
                {
                    return true;
                }

                if (!changeResult.Accepted)
                {
                    NotifyEquipmentEquipBlocked(changeResult.RejectReason);
                    return false;
                }

                return true;
            }

            CharacterEquipSlot sourceSlot = _draggedPart.Slot;
            CharacterPart movingPart = _characterBuild?.Unequip(sourceSlot);
            if (movingPart == null)
            {
                return false;
            }

            IReadOnlyList<CharacterPart> displacedParts = _characterBuild?.PlaceEquipment(movingPart, resolvedTargetSlot)
                ?? Array.Empty<CharacterPart>();
            CharacterPart swapCandidate = SelectSwapCandidateForSource(displacedParts, _draggedSlot);
            if (swapCandidate != null)
            {
                _characterBuild?.PlaceEquipment(swapCandidate, sourceSlot);
            }

            return true;
        }

        private EquipmentChangeResult TryRequestCharacterToCharacterChange(CharacterEquipSlot resolvedTargetSlot)
        {
            if (_draggedPart == null)
            {
                return null;
            }

            EquipmentChangeRequest request = new EquipmentChangeRequest
            {
                OwnerKind = EquipmentChangeOwnerKind.LegacyWindow,
                OwnerSessionId = _equipmentRequestSessionId,
                ExpectedCharacterId = _characterBuild?.Id ?? 0,
                ExpectedBuildStateToken = _characterBuild?.ComputeEquipmentStateToken() ?? 0,
                Kind = EquipmentChangeRequestKind.CharacterToCharacter,
                SourceEquipSlot = _draggedPart.Slot,
                TargetEquipSlot = resolvedTargetSlot,
                ItemId = _draggedPart.ItemId,
                ItemName = _draggedPart.Name ?? string.Empty,
                Summary = $"Move {_draggedPart.Name ?? $"Item {_draggedPart.ItemId}"} to {resolvedTargetSlot}.",
                RequestedPart = _draggedPart.Clone()
            };

            return TrySubmitEquipmentChangeRequest(request, EquipmentChangeRequestKind.CharacterToCharacter);
        }

        private EquipmentChangeResult TryRequestInventoryToCharacterChange(
            InventoryType sourceInventoryType,
            int sourceInventoryIndex,
            InventorySlotData draggedSlotData,
            CharacterPart incomingPart,
            CharacterEquipSlot resolvedTargetSlot)
        {
            if (draggedSlotData == null)
            {
                return null;
            }

            EquipmentChangeRequest request = new EquipmentChangeRequest
            {
                OwnerKind = EquipmentChangeOwnerKind.LegacyWindow,
                OwnerSessionId = _equipmentRequestSessionId,
                ExpectedCharacterId = _characterBuild?.Id ?? 0,
                ExpectedBuildStateToken = _characterBuild?.ComputeEquipmentStateToken() ?? 0,
                Kind = EquipmentChangeRequestKind.InventoryToCharacter,
                SourceInventoryType = sourceInventoryType,
                SourceInventoryIndex = sourceInventoryIndex,
                TargetEquipSlot = resolvedTargetSlot,
                ItemId = draggedSlotData.ItemId,
                ItemName = draggedSlotData.ItemName ?? string.Empty,
                Summary = $"Equip {draggedSlotData.ItemName ?? $"Item {draggedSlotData.ItemId}"} to {resolvedTargetSlot}.",
                SourceInventorySlot = draggedSlotData.Clone(),
                RequestedPart = incomingPart?.Clone()
            };

            return TrySubmitEquipmentChangeRequest(request, EquipmentChangeRequestKind.InventoryToCharacter);
        }

        private EquipmentChangeResult TryRequestCharacterToInventoryChange()
        {
            if (_draggedPart == null)
            {
                return null;
            }

            EquipmentChangeRequest request = new EquipmentChangeRequest
            {
                OwnerKind = EquipmentChangeOwnerKind.LegacyWindow,
                OwnerSessionId = _equipmentRequestSessionId,
                ExpectedCharacterId = _characterBuild?.Id ?? 0,
                ExpectedBuildStateToken = _characterBuild?.ComputeEquipmentStateToken() ?? 0,
                Kind = EquipmentChangeRequestKind.CharacterToInventory,
                SourceInventoryType = MapleLib.WzLib.WzStructure.Data.ItemStructure.InventoryType.EQUIP,
                SourceEquipSlot = _draggedPart.Slot,
                ItemId = _draggedPart.ItemId,
                ItemName = _draggedPart.Name ?? string.Empty,
                Summary = $"Unequip {_draggedPart.Name ?? $"Item {_draggedPart.ItemId}"} to inventory.",
                RequestedPart = _draggedPart.Clone()
            };

            return TrySubmitEquipmentChangeRequest(request, EquipmentChangeRequestKind.CharacterToInventory);
        }

        private EquipmentChangeResult TrySubmitEquipmentChangeRequest(
            EquipmentChangeRequest request,
            EquipmentChangeRequestKind kind)
        {
            if (request == null)
            {
                return null;
            }

            if (EquipmentChangeClientParity.IsExclusiveRequestThrottled(
                    Environment.TickCount,
                    _lastEquipmentExclusiveRequestTick,
                    EQUIPMENT_EXCLUSIVE_REQUEST_COOLDOWN_MS))
            {
                return EquipmentChangeResult.Reject("Please wait a moment before changing equipment again.");
            }

            if (EquipmentChangeSubmitted != null && EquipmentChangeResultRequested != null)
            {
                EquipmentChangeSubmission submission = EquipmentChangeSubmitted.Invoke(request);
                if (submission == null)
                {
                    return EquipmentChangeResult.Reject("The equipment request could not be submitted.");
                }

                if (!submission.Accepted)
                {
                    return EquipmentChangeResult.Reject(submission.RejectReason);
                }

                _lastEquipmentExclusiveRequestTick = submission.RequestedAtTick;
                _pendingEquipmentChange = new PendingEquipmentChange
                {
                    Kind = kind,
                    RequestId = submission.RequestId,
                    RequestedAtTick = submission.RequestedAtTick,
                    OwnerKind = request.OwnerKind,
                    OwnerSessionId = request.OwnerSessionId,
                    SourceInventoryType = request.SourceInventoryType,
                    SourceInventoryIndex = request.SourceInventoryIndex
                };

                return EquipmentChangeResult.Pending(submission.RequestId, submission.RequestedAtTick);
            }

            EquipmentChangeResult result = EquipmentChangeRequested?.Invoke(request);
            if (result?.Accepted == true)
            {
                _lastEquipmentExclusiveRequestTick = Environment.TickCount;
            }

            return result;
        }

        private static CharacterEquipSlot ResolveTargetSlot(EquipSlot uiSlot, CharacterPart part)
        {
            CharacterEquipSlot? mappedSlot = MapToCharacterEquipSlot(uiSlot);
            if (!mappedSlot.HasValue)
                return part.Slot;

            if (mappedSlot.Value == CharacterEquipSlot.Coat && part.Slot == CharacterEquipSlot.Longcoat)
                return CharacterEquipSlot.Longcoat;
            if (IsRingSlot(mappedSlot.Value) && IsRingSlot(part.Slot))
                return mappedSlot.Value;
            if ((mappedSlot.Value == CharacterEquipSlot.Pendant || mappedSlot.Value == CharacterEquipSlot.Pendant2)
                && (part.Slot == CharacterEquipSlot.Pendant || part.Slot == CharacterEquipSlot.Pendant2))
                return mappedSlot.Value;

            return mappedSlot.Value;
        }

        private string BuildDragCompareDescription(EquipSlot hoveredSlot)
        {
            CharacterEquipSlot? targetSlot = MapToCharacterEquipSlot(hoveredSlot);
            if (!targetSlot.HasValue)
                return "This slot is decorative in the current simulator build.";

            EquipSlotVisualState targetState = EquipSlotStateResolver.ResolveVisualState(_characterBuild, targetSlot.Value);
            if (targetState.IsDisabled)
                return targetState.Message;

            if (!CanDisplayPartInSlot(_draggedPart, hoveredSlot))
                return "This item cannot be placed in that slot.";

            CharacterPart targetPart = ResolveEquippedPart(hoveredSlot);
            if (targetPart == null || ReferenceEquals(targetPart, _draggedPart))
            {
                if (_draggedSlot.HasValue &&
                    MapToCharacterEquipSlot(_draggedSlot.Value) is CharacterEquipSlot sourceSlot &&
                    EquipSlotStateResolver.ResolveUnderlyingPart(_characterBuild, sourceSlot) is CharacterPart hiddenPart)
                {
                    string hiddenName = string.IsNullOrWhiteSpace(hiddenPart.Name)
                        ? $"Equip {hiddenPart.ItemId}"
                        : hiddenPart.Name;
                    return $"Release to move this equipment item. {hiddenName} will remain equipped underneath.";
                }

                return "Release to move this equipment item.";
            }

            if (targetPart.IsCash && !_draggedPart.IsCash)
            {
                CharacterPart hiddenTargetPart = targetSlot.HasValue
                    ? EquipSlotStateResolver.ResolveUnderlyingPart(_characterBuild, targetSlot.Value)
                    : null;
                string cashName = string.IsNullOrWhiteSpace(targetPart.Name)
                    ? $"Equip {targetPart.ItemId}"
                    : targetPart.Name;
                if (hiddenTargetPart != null && _draggedSlot.HasValue)
                {
                    string hiddenName = string.IsNullOrWhiteSpace(hiddenTargetPart.Name)
                        ? $"Equip {hiddenTargetPart.ItemId}"
                        : hiddenTargetPart.Name;
                    return $"Release to equip underneath {cashName}. {hiddenName} will move to {ResolveSlotLabel(_draggedSlot.Value)}.";
                }

                return $"Release to equip underneath {cashName}.";
            }

            if (_draggedPart.IsCash && !targetPart.IsCash)
            {
                string targetName = string.IsNullOrWhiteSpace(targetPart.Name)
                    ? $"Equip {targetPart.ItemId}"
                    : targetPart.Name;
                return $"Release to cover {targetName}. It will remain equipped underneath.";
            }

            return $"Swap with {(string.IsNullOrWhiteSpace(targetPart.Name) ? $"Equip {targetPart.ItemId}" : targetPart.Name)}.";
        }

        private CharacterPart SelectSwapCandidateForSource(IReadOnlyList<CharacterPart> displacedParts, EquipSlot? sourceUiSlot)
        {
            if (displacedParts == null || displacedParts.Count == 0 || !sourceUiSlot.HasValue)
            {
                return null;
            }

            for (int i = 0; i < displacedParts.Count; i++)
            {
                CharacterPart candidate = displacedParts[i];
                if (candidate != null && CanDisplayPartInSlot(candidate, sourceUiSlot.Value))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool CanDisplayPartInSlot(CharacterPart part, EquipSlot uiSlot)
        {
            if (part == null)
                return false;

            CharacterEquipSlot? mappedSlot = MapToCharacterEquipSlot(uiSlot);
            if (!mappedSlot.HasValue)
                return false;

            return mappedSlot.Value switch
            {
                CharacterEquipSlot.Ring1 or CharacterEquipSlot.Ring2 or CharacterEquipSlot.Ring3 or CharacterEquipSlot.Ring4
                    => IsRingSlot(part.Slot),
                CharacterEquipSlot.Coat => part.Slot == CharacterEquipSlot.Coat || part.Slot == CharacterEquipSlot.Longcoat,
                CharacterEquipSlot.Pendant => part.Slot == CharacterEquipSlot.Pendant || part.Slot == CharacterEquipSlot.Pendant2,
                CharacterEquipSlot.Pendant2 => part.Slot == CharacterEquipSlot.Pendant || part.Slot == CharacterEquipSlot.Pendant2,
                _ => part.Slot == mappedSlot.Value
            };
        }

        private static bool IsRingSlot(CharacterEquipSlot slot)
        {
            return slot is CharacterEquipSlot.Ring1 or CharacterEquipSlot.Ring2 or CharacterEquipSlot.Ring3 or CharacterEquipSlot.Ring4;
        }

        private static CharacterEquipSlot? MapToCharacterEquipSlot(EquipSlot uiSlot)
        {
            return uiSlot switch
            {
                EquipSlot.Ring1 => CharacterEquipSlot.Ring1,
                EquipSlot.Ring2 => CharacterEquipSlot.Ring2,
                EquipSlot.Ring3 => CharacterEquipSlot.Ring3,
                EquipSlot.Ring4 => CharacterEquipSlot.Ring4,
                EquipSlot.Pendant1 => CharacterEquipSlot.Pendant,
                EquipSlot.Pendant2 => CharacterEquipSlot.Pendant2,
                EquipSlot.Weapon => CharacterEquipSlot.Weapon,
                EquipSlot.Belt => CharacterEquipSlot.Belt,
                EquipSlot.Cap => CharacterEquipSlot.Cap,
                EquipSlot.FaceAccessory => CharacterEquipSlot.FaceAccessory,
                EquipSlot.EyeAccessory => CharacterEquipSlot.EyeAccessory,
                EquipSlot.Top => CharacterEquipSlot.Coat,
                EquipSlot.Bottom => CharacterEquipSlot.Pants,
                EquipSlot.Shoes => CharacterEquipSlot.Shoes,
                EquipSlot.Earring => CharacterEquipSlot.Earrings,
                EquipSlot.Glove => CharacterEquipSlot.Glove,
                EquipSlot.Shield => CharacterEquipSlot.Shield,
                EquipSlot.Cape => CharacterEquipSlot.Cape,
                EquipSlot.Medal => CharacterEquipSlot.Medal,
                _ => null
            };
        }

        private static string ResolveSlotLabel(EquipSlot slot)
        {
            return slot switch
            {
                EquipSlot.Earring => "Earring",
                EquipSlot.FaceAccessory => "Face Accessory",
                EquipSlot.EyeAccessory => "Eye Accessory",
                EquipSlot.Pendant1 => "Pendant",
                EquipSlot.Pendant2 => "Pendant 2",
                _ => slot.ToString()
            };
        }

        private static string ResolveDragonSlotLabel(DragonEquipSlot slot)
        {
            return slot switch
            {
                DragonEquipSlot.Mask => "Cap Accessory",
                DragonEquipSlot.Wings => "Wing Accessory",
                DragonEquipSlot.Pendant => "Pendant",
                DragonEquipSlot.Tail => "Tail Accessory",
                _ => slot.ToString()
            };
        }

        private static string BuildSwapRejectReason(CharacterPart part, EquipSlot? sourceUiSlot)
        {
            string targetName = string.IsNullOrWhiteSpace(part?.Name)
                ? $"Equip {part?.ItemId ?? 0}"
                : part.Name;

            if (!sourceUiSlot.HasValue)
            {
                return $"{targetName} cannot be moved to complete that swap.";
            }

            return $"{targetName} cannot be moved to the {ResolveSlotLabel(sourceUiSlot.Value)} slot.";
        }

        private static string BuildDragonDescription(CompanionEquipItem item)
        {
            if (!string.IsNullOrWhiteSpace(item?.Description))
                return item.Description;
            if (item?.CharacterPart != null && !string.IsNullOrWhiteSpace(item.CharacterPart.Description))
                return item.CharacterPart.Description;
            if (item?.CharacterPart != null && item.CharacterPart.ExpirationDateUtc.HasValue)
                return $"Expires {item.CharacterPart.ExpirationDateUtc.Value.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
            return null;
        }

        private void NotifyEquipmentEquipBlocked(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                EquipmentEquipBlocked?.Invoke(message);
            }
        }

        private static int NextEquipmentRequestSessionId(int currentSessionId)
        {
            if (currentSessionId >= int.MaxValue)
            {
                return 1;
            }

            return Math.Max(1, currentSessionId + 1);
        }

        private IReadOnlyList<InventorySlotData> CreateInventorySlots(IReadOnlyList<CharacterPart> parts)
        {
            if (parts == null || parts.Count == 0)
            {
                return Array.Empty<InventorySlotData>();
            }

            List<InventorySlotData> slots = new(parts.Count);
            for (int i = 0; i < parts.Count; i++)
            {
                InventorySlotData slot = CreateInventorySlot(parts[i]);
                if (slot != null)
                {
                    slots.Add(slot);
                }
            }

            return slots.Count == 0 ? Array.Empty<InventorySlotData>() : slots;
        }

        private InventorySlotData CreateInventorySlot(CharacterPart part)
        {
            if (part == null)
            {
                return null;
            }

            return new InventorySlotData
            {
                ItemId = part.ItemId,
                ItemTexture = null,
                Quantity = 1,
                PreferredInventoryType = ResolveInventoryTypeForPart(part),
                ItemName = part.Name,
                Description = part.Description,
                TooltipPart = part.Clone()
            };
        }

        private static InventoryType ResolveInventoryTypeForSlot(InventorySlotData slot)
        {
            if (slot?.PreferredInventoryType is InventoryType preferred && preferred != InventoryType.NONE)
            {
                return preferred;
            }

            return InventoryItemMetadataResolver.ResolveInventoryType(slot);
        }

        private static InventoryType ResolveInventoryTypeForPart(CharacterPart part)
        {
            if (part?.IsCash == true)
            {
                return InventoryType.CASH;
            }

            return InventoryType.EQUIP;
        }
    }

    public class EquipSlotData
    {
        public int ItemId { get; set; }
        public Texture2D ItemTexture { get; set; }
        public IDXObject ItemIcon { get; set; }
        public string ItemName { get; set; }
        public int STR { get; set; }
        public int DEX { get; set; }
        public int INT { get; set; }
        public int LUK { get; set; }
        public int HP { get; set; }
        public int MP { get; set; }
        public int WATK { get; set; }
        public int MATK { get; set; }
        public int WDEF { get; set; }
        public int MDEF { get; set; }
        public int Speed { get; set; }
        public int Jump { get; set; }
        public int Slots { get; set; }
        public int Stars { get; set; }
    }
}
