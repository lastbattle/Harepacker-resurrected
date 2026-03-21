using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.UI.Controls;
using HaCreator.MapSimulator.Companions;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
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
using WeaponPart = HaCreator.MapSimulator.Character.WeaponPart;
using EquipSlotStateResolver = HaCreator.MapSimulator.Character.EquipSlotStateResolver;
using EquipSlotVisualState = HaCreator.MapSimulator.Character.EquipSlotVisualState;

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// Equipment UI window for post-Big Bang MapleStory (v100+)
    /// Structure: UI.wz/UIWindow2.img/Equip/character
    /// Window dimensions: 184x290 pixels
    /// </summary>
    public class EquipUIBigBang : UIWindowBase
    {
        #region Constants
        private const int SLOT_SIZE = 32;

        // Window dimensions (from UIWindow2.img/Equip/character/backgrnd)
        private const int WINDOW_WIDTH = 184;
        private const int WINDOW_HEIGHT = 290;
        private const int TOOLTIP_FALLBACK_WIDTH = 300;
        private const int TOOLTIP_PADDING = 10;
        private const int TOOLTIP_ICON_GAP = 8;
        private const int TOOLTIP_TITLE_GAP = 8;
        private const int TOOLTIP_SECTION_GAP = 4;
        private const int TOOLTIP_OFFSET_X = 12;
        private const int TOOLTIP_OFFSET_Y = -4;
        private const int COMPANION_PANE_ATTACH_X = 10;
        private const int COMPANION_PANE_ATTACH_Y = 18;
        private const int COMPANION_EMPTY_TEXT_PADDING = 14;

        // Tab indices
        private const int TAB_CHARACTER = 0;
        private const int TAB_PET = 1;
        private const int TAB_DRAGON = 2;
        private const int TAB_MECHANIC = 3;
        private const int TAB_ANDROID = 4;
        #endregion

        #region Equipment Slot Types
        public enum EquipSlot
        {
            Ring1 = 0,
            Ring2 = 1,
            Ring3 = 2,
            Ring4 = 3,
            Pocket = 4,
            Pendant1 = 5,
            Pendant2 = 6,
            Weapon = 7,
            Belt = 8,
            Cap = 9,
            FaceAccessory = 10,
            EyeAccessory = 11,
            Top = 12,
            Bottom = 13,
            Shoes = 14,
            Earring = 15,
            Shoulder = 16,
            Glove = 17,
            Shield = 18,
            Cape = 19,
            Heart = 20,
            Badge = 21,
            Medal = 22,
            Android = 23,
            AndroidHeart = 24,
            Totem1 = 25,
            Totem2 = 26,
            Totem3 = 27
        }
        #endregion

        private sealed class CompanionPaneLayout
        {
            public IDXObject Frame { get; init; }
            public IDXObject Foreground { get; init; }
            public Point ForegroundOffset { get; init; }
            public IDXObject SlotLabels { get; init; }
            public Point SlotLabelsOffset { get; init; }
        }

        #region Fields
        private int _currentTab = TAB_CHARACTER;

        // Foreground texture (backgrnd2 - grid overlay)
        private IDXObject _foreground;
        private Point _foregroundOffset;

        // Slot labels and character silhouette (backgrnd3)
        private IDXObject _slotLabels;
        private Point _slotLabelsOffset;

        // Tab buttons
        private UIObject _btnPet;
        private UIObject _btnDragon;
        private UIObject _btnMechanic;
        private UIObject _btnAndroid;
        private UIObject _btnSlot;

        // Equipment slot positions (relative to window)
        private readonly Dictionary<EquipSlot, Point> slotPositions;

        // Equipped items data
        private readonly Dictionary<EquipSlot, EquipSlotData> equippedItems;
        private readonly Dictionary<int, CompanionPaneLayout> _companionLayouts;
        private readonly Point[] _petSlotPositions;
        private readonly Point[] _petSkillSlotPositions;
        private readonly Dictionary<DragonEquipSlot, Point> _dragonSlotPositions;
        private readonly Dictionary<MechanicEquipSlot, Point> _mechanicSlotPositions;
        private readonly Dictionary<AndroidEquipSlot, Point> _androidSlotPositions;

        // Graphics device
        private GraphicsDevice _device;
        private readonly Texture2D[] _tooltipFrames = new Texture2D[3];
        private Texture2D _debugPlaceholder;
        private Texture2D _slotOverlayTexture;
        private SpriteFont _font;
        private CharacterBuild _characterBuild;
        private PetController _petController;
        private DragonEquipmentController _dragonEquipmentController;
        private MechanicEquipmentController _mechanicEquipmentController;
        private AndroidEquipmentController _androidEquipmentController;
        private EquipSlot? _hoveredSlot;
        private bool _isDraggingItem;
        private EquipSlot? _draggedSlot;
        private CharacterPart _draggedPart;
        private Point _draggedItemPosition;
        private int? _hoveredPetIndex;
        private DragonEquipSlot? _hoveredDragonSlot;
        private MechanicEquipSlot? _hoveredMechanicSlot;
        private AndroidEquipSlot? _hoveredAndroidSlot;
        private Point _lastMousePosition;
        private MouseState _previousMouseState;
        #endregion

        #region Properties
        public override string WindowName => "Equipment";
        public Action<CharacterEquipSlot> ItemUpgradeRequested { get; set; }
        public override CharacterBuild CharacterBuild
        {
            get => _characterBuild;
            set => _characterBuild = value;
        }
        public bool IsDraggingItem => _isDraggingItem;

        public int CurrentTab
        {
            get => _currentTab;
            set
            {
                if (value >= TAB_CHARACTER && value <= TAB_ANDROID)
                {
                    _currentTab = value;
                    UpdateTabButtonStates();
                }
            }
        }
        #endregion

        #region Constructor
        public EquipUIBigBang(IDXObject frame, GraphicsDevice device)
            : base(frame)
        {
            _device = device;

            // Initialize slot positions for Big Bang layout
            // Grid formula from IDA: X = 33*col + 10, Y = 33*row + 27 (for nType=0)
            // Col 0=10, Col 1=43, Col 2=76, Col 3=109, Col 4=142
            // Row 0=27, Row 1=60, Row 2=93, Row 3=126, Row 4=159, Row 5=192, Row 6=225
            slotPositions = new Dictionary<EquipSlot, Point>
            {
                // Row 0
                { EquipSlot.Badge, new Point(10, 27) },      // Col 0, Row 0
                { EquipSlot.Cap, new Point(43, 27) },        // Col 1, Row 0
                { EquipSlot.Android, new Point(109, 27) },   // Col 3, Row 0
                { EquipSlot.Heart, new Point(142, 27) },     // Col 4, Row 0

                // Row 1
                { EquipSlot.Medal, new Point(10, 60) },      // Col 0, Row 1
                { EquipSlot.FaceAccessory, new Point(43, 60) }, // Col 1, Row 1 (Forehead)
                { EquipSlot.Ring1, new Point(109, 60) },     // Col 3, Row 1
                { EquipSlot.Ring2, new Point(142, 60) },     // Col 4, Row 1

                // Row 2
                { EquipSlot.EyeAccessory, new Point(43, 93) }, // Col 1, Row 2
                { EquipSlot.Shoulder, new Point(142, 93) },  // Col 4, Row 2

                // Row 3
                { EquipSlot.Cape, new Point(10, 126) },      // Col 0, Row 3 (Mantle/Cape)
                { EquipSlot.Top, new Point(43, 126) },       // Col 1, Row 3 (Clothes)
                { EquipSlot.Pendant1, new Point(76, 126) },  // Col 2, Row 3
                { EquipSlot.Weapon, new Point(142, 126) },   // Col 4, Row 3

                // Row 4
                { EquipSlot.Glove, new Point(10, 159) },     // Col 0, Row 4
                { EquipSlot.Belt, new Point(76, 159) },      // Col 2, Row 4
                { EquipSlot.Ring3, new Point(142, 159) },    // Col 4, Row 4

                // Row 5
                { EquipSlot.Bottom, new Point(43, 192) },    // Col 1, Row 5 (Pants)
                { EquipSlot.Shoes, new Point(76, 192) },     // Col 2, Row 5
                { EquipSlot.Pocket, new Point(142, 192) },   // Col 4, Row 5 (Pet slot)

                // Row 6
                { EquipSlot.Totem1, new Point(10, 225) },    // Col 0, Row 6 (Taming Mob)
                { EquipSlot.Totem2, new Point(43, 225) },    // Col 1, Row 6 (Saddle)
                { EquipSlot.Totem3, new Point(76, 225) },    // Col 2, Row 6 (Mob Equip)

                // Additional slots (off main grid or secondary positions)
                { EquipSlot.Pendant2, new Point(109, 126) }, // Col 3, Row 3 (secondary pendant)
                { EquipSlot.Ring4, new Point(109, 159) },    // Col 3, Row 4
                { EquipSlot.Shield, new Point(109, 93) },    // Col 3, Row 2 (Sub-weapon)
                { EquipSlot.Earring, new Point(10, 93) },    // Col 0, Row 2
            };

            equippedItems = new Dictionary<EquipSlot, EquipSlotData>();
            _companionLayouts = new Dictionary<int, CompanionPaneLayout>();
            _petSlotPositions = new[]
            {
                new Point(11, 27),
                new Point(44, 27),
                new Point(77, 27)
            };
            _petSkillSlotPositions = new[]
            {
                new Point(11, 93),
                new Point(44, 93),
                new Point(77, 93)
            };
            _dragonSlotPositions = new Dictionary<DragonEquipSlot, Point>
            {
                { DragonEquipSlot.Mask, new Point(10, 55) },
                { DragonEquipSlot.Wings, new Point(76, 55) },
                { DragonEquipSlot.Pendant, new Point(43, 88) },
                { DragonEquipSlot.Tail, new Point(109, 88) }
            };
            _mechanicSlotPositions = new Dictionary<MechanicEquipSlot, Point>
            {
                { MechanicEquipSlot.Engine, new Point(11, 27) },
                { MechanicEquipSlot.Frame, new Point(44, 27) },
                { MechanicEquipSlot.Transistor, new Point(77, 27) },
                { MechanicEquipSlot.Arm, new Point(11, 93) },
                { MechanicEquipSlot.Leg, new Point(44, 93) }
            };
            _androidSlotPositions = new Dictionary<AndroidEquipSlot, Point>
            {
                { AndroidEquipSlot.Cap, new Point(12, 28) },
                { AndroidEquipSlot.FaceAccessory, new Point(44, 60) },
                { AndroidEquipSlot.Clothes, new Point(12, 92) },
                { AndroidEquipSlot.Glove, new Point(44, 92) },
                { AndroidEquipSlot.Cape, new Point(76, 92) },
                { AndroidEquipSlot.Pants, new Point(12, 124) },
                { AndroidEquipSlot.Shoes, new Point(44, 124) }
            };
            _debugPlaceholder = new Texture2D(device, 1, 1);
            _debugPlaceholder.SetData(new[] { Color.White });
            _slotOverlayTexture = _debugPlaceholder;
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Set the foreground texture (backgrnd2 - grid overlay)
        /// </summary>
        public void SetForeground(IDXObject foreground, int offsetX, int offsetY)
        {
            _foreground = foreground;
            _foregroundOffset = new Point(offsetX, offsetY);
        }

        /// <summary>
        /// Set the slot labels and character silhouette (backgrnd3)
        /// </summary>
        public void SetSlotLabels(IDXObject slotLabels, int offsetX, int offsetY)
        {
            _slotLabels = slotLabels;
            _slotLabelsOffset = new Point(offsetX, offsetY);
        }

        public void SetCompanionTabLayout(int tabIndex, IDXObject frame, IDXObject foreground, int foregroundOffsetX, int foregroundOffsetY, IDXObject slotLabels, int slotLabelOffsetX, int slotLabelOffsetY)
        {
            if (tabIndex < TAB_PET || tabIndex > TAB_ANDROID || frame == null)
            {
                return;
            }

            _companionLayouts[tabIndex] = new CompanionPaneLayout
            {
                Frame = frame,
                Foreground = foreground,
                ForegroundOffset = new Point(foregroundOffsetX, foregroundOffsetY),
                SlotLabels = slotLabels,
                SlotLabelsOffset = new Point(slotLabelOffsetX, slotLabelOffsetY)
            };
        }

        /// <summary>
        /// Initialize equipment tab buttons
        /// </summary>
        public void InitializeTabButtons(UIObject btnPet, UIObject btnDragon, UIObject btnMechanic, UIObject btnAndroid, UIObject btnSlot)
        {
            _btnPet = btnPet;
            _btnDragon = btnDragon;
            _btnMechanic = btnMechanic;
            _btnAndroid = btnAndroid;
            _btnSlot = btnSlot;

            if (btnPet != null)
            {
                AddButton(btnPet);
                btnPet.ButtonClickReleased += (sender) => CurrentTab = TAB_PET;
            }
            if (btnDragon != null)
            {
                AddButton(btnDragon);
                btnDragon.ButtonClickReleased += (sender) => CurrentTab = TAB_DRAGON;
            }
            if (btnMechanic != null)
            {
                AddButton(btnMechanic);
                btnMechanic.ButtonClickReleased += (sender) => CurrentTab = TAB_MECHANIC;
            }
            if (btnAndroid != null)
            {
                AddButton(btnAndroid);
                btnAndroid.ButtonClickReleased += (sender) => CurrentTab = TAB_ANDROID;
            }
            if (btnSlot != null)
            {
                AddButton(btnSlot);
                btnSlot.ButtonClickReleased += (sender) => CurrentTab = TAB_CHARACTER;
            }

            UpdateTabButtonStates();
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SetTooltipTextures(Texture2D[] tooltipFrames)
        {
            if (tooltipFrames == null)
                return;

            for (int i = 0; i < Math.Min(_tooltipFrames.Length, tooltipFrames.Length); i++)
            {
                _tooltipFrames[i] = tooltipFrames[i];
            }
        }

        public void SetPetController(PetController petController)
        {
            _petController = petController;
        }

        public void SetDragonEquipmentController(DragonEquipmentController dragonEquipmentController)
        {
            _dragonEquipmentController = dragonEquipmentController;
        }

        public void SetMechanicEquipmentController(MechanicEquipmentController mechanicEquipmentController)
        {
            _mechanicEquipmentController = mechanicEquipmentController;
        }

        public void SetAndroidEquipmentController(AndroidEquipmentController androidEquipmentController)
        {
            _androidEquipmentController = androidEquipmentController;
        }
        #endregion

        #region Drawing
        protected override void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            int windowX = this.Position.X;
            int windowY = this.Position.Y;

            // Draw foreground (backgrnd2 - grid overlay) z=0
            if (_foreground != null)
            {
                _foreground.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    windowX + _foregroundOffset.X, windowY + _foregroundOffset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            // Draw slot labels and character silhouette (backgrnd3) z=1
            if (_slotLabels != null)
            {
                _slotLabels.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    windowX + _slotLabelsOffset.X, windowY + _slotLabelsOffset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            foreach ((EquipSlot uiSlot, Point slotPosition) in slotPositions)
            {
                int slotX = windowX + slotPosition.X;
                int slotY = windowY + slotPosition.Y;
                CharacterPart part = ResolveEquippedPart(uiSlot);
                EquipSlotData slotData = equippedItems.TryGetValue(uiSlot, out EquipSlotData itemData) ? itemData : null;
                if (part != null || slotData != null)
                {
                    DrawEquippedItemIcon(sprite, part, slotData, slotX, slotY);
                }

                CharacterEquipSlot? characterSlot = MapToCharacterEquipSlot(uiSlot);
                if (!characterSlot.HasValue)
                {
                    continue;
                }

                EquipSlotVisualState visualState = EquipSlotStateResolver.ResolveVisualState(_characterBuild, characterSlot.Value);
                if (visualState.IsDisabled)
                {
                    DrawDisabledOverlay(sprite, slotX, slotY, visualState);
                }
            }

            DrawCompanionPane(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo, windowX, windowY);
        }

        protected override void DrawOverlay(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            DrawHoveredEquipmentTooltip(sprite, renderParameters.RenderWidth, renderParameters.RenderHeight);
            DrawDraggedItemOverlay(sprite);
        }
        #endregion

        #region Equipment Management
        /// <summary>
        /// Equip an item to a slot
        /// </summary>
        public void EquipItem(EquipSlot slot, int itemId, Texture2D texture, string itemName = "")
        {
            equippedItems[slot] = new EquipSlotData
            {
                ItemId = itemId,
                ItemTexture = texture,
                ItemName = itemName
            };
        }

        /// <summary>
        /// Unequip an item from a slot
        /// </summary>
        public void UnequipItem(EquipSlot slot)
        {
            if (equippedItems.ContainsKey(slot))
            {
                equippedItems.Remove(slot);
            }
        }

        /// <summary>
        /// Get equipped item data
        /// </summary>
        public EquipSlotData GetEquippedItem(EquipSlot slot)
        {
            return equippedItems.TryGetValue(slot, out var data) ? data : null;
        }

        /// <summary>
        /// Clear all equipped items
        /// </summary>
        public void ClearAllEquipment()
        {
            equippedItems.Clear();
        }
        #endregion

        #region Update
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
            _hoveredMechanicSlot = GetHoveredMechanicSlot(mouseState.X, mouseState.Y);
            _hoveredAndroidSlot = GetHoveredAndroidSlot(mouseState.X, mouseState.Y);
        }

        public bool HandlesEquipmentInteractionPoint(int mouseX, int mouseY)
        {
            return _currentTab == TAB_CHARACTER && GetSlotAtPosition(mouseX, mouseY).HasValue;
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
            if (!rightJustPressed || _currentTab != TAB_CHARACTER || !_hoveredSlot.HasValue)
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
        #endregion

        private void DrawCompanionPane(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo, int windowX, int windowY)
        {
            if (_currentTab == TAB_CHARACTER || !_companionLayouts.TryGetValue(_currentTab, out CompanionPaneLayout layout))
            {
                return;
            }

            Point panePosition = GetCompanionPanePosition(layout, windowX, windowY);
            layout.Frame.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                panePosition.X, panePosition.Y,
                Color.White, false, drawReflectionInfo);

            layout.Foreground?.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                panePosition.X + layout.ForegroundOffset.X, panePosition.Y + layout.ForegroundOffset.Y,
                Color.White, false, drawReflectionInfo);

            layout.SlotLabels?.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                panePosition.X + layout.SlotLabelsOffset.X, panePosition.Y + layout.SlotLabelsOffset.Y,
                Color.White, false, drawReflectionInfo);

            switch (_currentTab)
            {
                case TAB_PET:
                    DrawPetPaneContents(sprite, panePosition);
                    break;
                case TAB_DRAGON:
                    DrawDragonPaneContents(sprite, panePosition);
                    break;
                case TAB_ANDROID:
                    DrawAndroidPaneContents(sprite, panePosition);
                    break;
                case TAB_MECHANIC:
                    DrawMechanicPaneContents(sprite, panePosition);
                    break;
            }
        }

        private void DrawPetPaneContents(SpriteBatch sprite, Point panePosition)
        {
            IReadOnlyList<PetRuntime> pets = _petController?.ActivePets;
            bool hasActivePets = pets != null && pets.Count > 0;

            for (int i = 0; i < _petSlotPositions.Length; i++)
            {
                if (!hasActivePets || i >= pets.Count)
                {
                    continue;
                }

                PetRuntime pet = pets[i];
                IDXObject icon = pet.Definition?.IconRaw ?? pet.Definition?.Icon;
                Point petSlot = _petSlotPositions[i];
                if (icon != null)
                {
                    icon.DrawBackground(sprite, null, null,
                        panePosition.X + petSlot.X,
                        panePosition.Y + petSlot.Y,
                        Color.White, false, null);
                }

                if (pet.AutoLootEnabled && _font != null)
                {
                    Point skillSlot = _petSkillSlotPositions[i];
                    Rectangle markerRect = new Rectangle(
                        panePosition.X + skillSlot.X + 1,
                        panePosition.Y + skillSlot.Y + 1,
                        SLOT_SIZE - 2,
                        SLOT_SIZE - 2);
                    sprite.Draw(_debugPlaceholder, markerRect, new Color(27, 64, 41, 160));
                    Vector2 textSize = _font.MeasureString("AUTO");
                    Vector2 textPosition = new Vector2(
                        markerRect.Center.X - (textSize.X / 2f),
                        markerRect.Center.Y - (textSize.Y / 2f));
                    DrawTooltipText(sprite, "AUTO", textPosition, new Color(180, 244, 194));
                }
            }

            if (!hasActivePets)
            {
                _companionLayouts.TryGetValue(TAB_PET, out CompanionPaneLayout layout);
                DrawCompanionEmptyState(sprite, layout?.Frame, panePosition,
                    "Summon pets in the simulator to populate the Multi Pet pane.");
            }
        }

        private void DrawDragonPaneContents(SpriteBatch sprite, Point panePosition)
        {
            bool hasAnyItems = false;
            foreach ((DragonEquipSlot slot, Point slotPosition) in _dragonSlotPositions)
            {
                if (_dragonEquipmentController == null || !_dragonEquipmentController.TryGetItem(slot, out CompanionEquipItem item))
                {
                    continue;
                }

                hasAnyItems = true;
                DrawCompanionItemIcon(sprite, item, panePosition.X + slotPosition.X, panePosition.Y + slotPosition.Y);
            }

            if (!hasAnyItems)
            {
                _companionLayouts.TryGetValue(TAB_DRAGON, out CompanionPaneLayout layout);
                DrawCompanionEmptyState(sprite, layout?.Frame, panePosition,
                    "Dragon equipment runtime is available, but no dragon items are equipped for this session.");
            }
        }

        private void DrawAndroidPaneContents(SpriteBatch sprite, Point panePosition)
        {
            bool hasAnyItems = false;
            foreach ((AndroidEquipSlot slot, Point slotPosition) in _androidSlotPositions)
            {
                if (_androidEquipmentController == null || !_androidEquipmentController.TryGetItem(slot, out CompanionEquipItem item))
                {
                    continue;
                }

                hasAnyItems = true;
                DrawCompanionItemIcon(sprite, item, panePosition.X + slotPosition.X, panePosition.Y + slotPosition.Y);
            }

            if (!hasAnyItems)
            {
                _companionLayouts.TryGetValue(TAB_ANDROID, out CompanionPaneLayout layout);
                DrawCompanionEmptyState(sprite, layout?.Frame, panePosition,
                    "Android equipment runtime is available, but no android gear is equipped for this session.");
            }
        }

        private void DrawMechanicPaneContents(SpriteBatch sprite, Point panePosition)
        {
            bool hasAnyItems = false;
            foreach ((MechanicEquipSlot slot, Point slotPosition) in _mechanicSlotPositions)
            {
                if (_mechanicEquipmentController == null || !_mechanicEquipmentController.TryGetItem(slot, out CompanionEquipItem item))
                {
                    continue;
                }

                hasAnyItems = true;
                DrawCompanionItemIcon(sprite, item, panePosition.X + slotPosition.X, panePosition.Y + slotPosition.Y);
            }

            if (!hasAnyItems)
            {
                _companionLayouts.TryGetValue(TAB_MECHANIC, out CompanionPaneLayout layout);
                DrawCompanionEmptyState(sprite, layout?.Frame, panePosition,
                    "Mechanic equipment runtime is available, but no machine parts are equipped for this session.");
            }
        }

        private void DrawCompanionEmptyState(SpriteBatch sprite, IDXObject paneFrame, Point panePosition, string message)
        {
            if (_font == null || paneFrame == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            float maxWidth = Math.Max(80, paneFrame.Width - (COMPANION_EMPTY_TEXT_PADDING * 2));
            string[] lines = WrapTooltipText(message, maxWidth);
            if (lines.Length == 0)
            {
                return;
            }

            float textHeight = MeasureLinesHeight(lines);
            int startX = panePosition.X + COMPANION_EMPTY_TEXT_PADDING;
            float startY = panePosition.Y + Math.Max(34, (paneFrame.Height - textHeight) / 2f);
            DrawTooltipLines(sprite, lines, startX, startY, new Color(216, 216, 216));
        }

        private Point GetCompanionPanePosition(CompanionPaneLayout layout, int windowX, int windowY)
        {
            return new Point(
                windowX - layout.Frame.Width + COMPANION_PANE_ATTACH_X,
                windowY + COMPANION_PANE_ATTACH_Y);
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
            {
                sprite.Draw(slotData.ItemTexture, new Rectangle(slotX, slotY, SLOT_SIZE, SLOT_SIZE), Color.White);
            }
        }

        private void DrawCompanionItemIcon(SpriteBatch sprite, CompanionEquipItem item, int slotX, int slotY)
        {
            IDXObject icon = item?.IconRaw ?? item?.Icon ?? item?.CharacterPart?.IconRaw ?? item?.CharacterPart?.Icon;
            if (icon != null)
            {
                icon.DrawBackground(sprite, null, null, slotX, slotY, Color.White, false, null);
            }
        }

        private void DrawDisabledOverlay(SpriteBatch sprite, int slotX, int slotY, EquipSlotVisualState visualState)
        {
            Color overlay = visualState.IsExpired
                ? new Color(110, 30, 30, 180)
                : visualState.IsBroken
                    ? new Color(90, 60, 20, 180)
                    : new Color(20, 20, 20, 145);
            sprite.Draw(_slotOverlayTexture, new Rectangle(slotX, slotY, SLOT_SIZE, SLOT_SIZE), overlay);
            sprite.Draw(_slotOverlayTexture, new Rectangle(slotX, slotY, SLOT_SIZE, 2), Color.Black);
            sprite.Draw(_slotOverlayTexture, new Rectangle(slotX, slotY + SLOT_SIZE - 2, SLOT_SIZE, 2), Color.Black);
            sprite.Draw(_slotOverlayTexture, new Rectangle(slotX, slotY, 2, SLOT_SIZE), Color.Black);
            sprite.Draw(_slotOverlayTexture, new Rectangle(slotX + SLOT_SIZE - 2, slotY, 2, SLOT_SIZE), Color.Black);
        }

        private void DrawHoveredEquipmentTooltip(SpriteBatch sprite, int renderWidth, int renderHeight)
        {
            if (_font == null)
                return;

            if (_currentTab == TAB_PET && TryBuildPetTooltip(out string petTitle, out string petLine1, out string petLine2, out string petDescription, out IDXObject petIcon))
            {
                DrawItemTooltip(sprite, petTitle, petLine1, petLine2, petDescription, null, petIcon, renderWidth, renderHeight);
                return;
            }

            if (_currentTab == TAB_DRAGON && TryBuildDragonTooltip(out string dragonTitle, out string dragonLine1, out string dragonLine2, out string dragonDescription, out IDXObject dragonIcon))
            {
                DrawItemTooltip(sprite, dragonTitle, dragonLine1, dragonLine2, dragonDescription, null, dragonIcon, renderWidth, renderHeight);
                return;
            }

            if (_currentTab == TAB_MECHANIC && TryBuildMechanicTooltip(out string mechanicTitle, out string mechanicLine1, out string mechanicLine2, out string mechanicDescription, out IDXObject mechanicIcon))
            {
                DrawItemTooltip(sprite, mechanicTitle, mechanicLine1, mechanicLine2, mechanicDescription, null, mechanicIcon, renderWidth, renderHeight);
                return;
            }

            if (_currentTab == TAB_ANDROID && TryBuildAndroidTooltip(out string androidTitle, out string androidLine1, out string androidLine2, out string androidDescription, out IDXObject androidIcon))
            {
                DrawItemTooltip(sprite, androidTitle, androidLine1, androidLine2, androidDescription, null, androidIcon, renderWidth, renderHeight);
                return;
            }

            if (_currentTab != TAB_CHARACTER || _hoveredSlot == null)
                return;

            EquipSlot hoveredSlot = _hoveredSlot.Value;
            if (_isDraggingItem && _draggedPart != null)
            {
                DrawDraggedComparisonTooltip(sprite, hoveredSlot, renderWidth, renderHeight);
                return;
            }

            CharacterPart part = ResolveEquippedPart(hoveredSlot);
            if (part == null)
            {
                if (equippedItems.TryGetValue(hoveredSlot, out EquipSlotData fallbackData))
                {
                    DrawItemTooltip(sprite,
                        fallbackData.ItemName,
                        $"Item ID: {fallbackData.ItemId}",
                        $"Slot: {hoveredSlot}",
                        null,
                        fallbackData.ItemTexture,
                        fallbackData.ItemIcon,
                        renderWidth,
                        renderHeight);
                    return;
                }

                CharacterEquipSlot? emptySlot = MapToCharacterEquipSlot(hoveredSlot);
                if (!emptySlot.HasValue)
                    return;

                EquipSlotVisualState emptyState = EquipSlotStateResolver.ResolveVisualState(_characterBuild, emptySlot.Value);
                if (!emptyState.IsDisabled)
                    return;

                DrawItemTooltip(sprite,
                    ResolveSlotLabel(hoveredSlot),
                    emptyState.Message,
                    $"Slot: {ResolveSlotLabel(hoveredSlot)}",
                    null,
                    null,
                    null,
                    renderWidth,
                    renderHeight);
                return;
            }

            CharacterEquipSlot? characterSlot = MapToCharacterEquipSlot(hoveredSlot);
            string itemName = string.IsNullOrWhiteSpace(part.Name) ? $"Equip {part.ItemId}" : part.Name;
            string line1 = BuildEquipmentSummaryLine(part);
            string line2 = BuildEquipmentRequirementLine(part, hoveredSlot);
            string description = BuildEquipmentDescription(part);
            if (characterSlot.HasValue)
            {
                EquipSlotVisualState visualState = EquipSlotStateResolver.ResolveVisualState(_characterBuild, characterSlot.Value);
                if (!string.IsNullOrWhiteSpace(visualState.Message))
                {
                    line2 = string.IsNullOrWhiteSpace(line2)
                        ? visualState.Message
                        : $"{line2}  {visualState.Message}";
                }
            }

            DrawItemTooltip(sprite,
                itemName,
                line1,
                line2,
                description,
                null,
                part.IconRaw ?? part.Icon,
                renderWidth,
                renderHeight);
        }

        private void DrawDraggedComparisonTooltip(SpriteBatch sprite, EquipSlot hoveredSlot, int renderWidth, int renderHeight)
        {
            string itemName = string.IsNullOrWhiteSpace(_draggedPart.Name) ? $"Equip {_draggedPart.ItemId}" : _draggedPart.Name;
            DrawItemTooltip(
                sprite,
                itemName,
                BuildEquipmentSummaryLine(_draggedPart),
                $"Drop on {ResolveSlotLabel(hoveredSlot)}",
                BuildDragCompareDescription(hoveredSlot),
                null,
                _draggedPart.IconRaw ?? _draggedPart.Icon,
                renderWidth,
                renderHeight);
        }

        private bool TryBuildPetTooltip(out string title, out string line1, out string line2, out string description, out IDXObject icon)
        {
            title = null;
            line1 = null;
            line2 = null;
            description = null;
            icon = null;

            if (_hoveredPetIndex == null)
            {
                return false;
            }

            IReadOnlyList<PetRuntime> pets = _petController?.ActivePets;
            int petIndex = _hoveredPetIndex.Value;
            if (pets == null || petIndex < 0 || petIndex >= pets.Count)
            {
                return false;
            }

            PetRuntime pet = pets[petIndex];
            title = string.IsNullOrWhiteSpace(pet.Name) ? $"Pet {pet.ItemId}" : pet.Name;
            line1 = $"Item ID: {pet.ItemId}";
            line2 = $"Slot: Pet {petIndex + 1}  Cmd Lv: {pet.CommandLevel}  Auto Loot: {(pet.AutoLootEnabled ? "On" : "Off")}";
            description = ResolveItemDescription(pet.ItemId, "Pet");
            icon = pet.Definition?.IconRaw ?? pet.Definition?.Icon;
            return true;
        }

        private bool TryBuildDragonTooltip(out string title, out string line1, out string line2, out string description, out IDXObject icon)
        {
            title = null;
            line1 = null;
            line2 = null;
            description = null;
            icon = null;

            if (_hoveredDragonSlot == null || _dragonEquipmentController == null
                || !_dragonEquipmentController.TryGetItem(_hoveredDragonSlot.Value, out CompanionEquipItem item))
            {
                return false;
            }

            title = string.IsNullOrWhiteSpace(item.Name) ? $"Dragon Equip {item.ItemId}" : item.Name;
            line1 = $"Item ID: {item.ItemId}";
            line2 = $"Slot: {ResolveDragonSlotLabel(_hoveredDragonSlot.Value)}";
            description = item.Description;
            icon = item.IconRaw ?? item.Icon ?? item.CharacterPart?.IconRaw ?? item.CharacterPart?.Icon;
            return true;
        }

        private bool TryBuildAndroidTooltip(out string title, out string line1, out string line2, out string description, out IDXObject icon)
        {
            title = null;
            line1 = null;
            line2 = null;
            description = null;
            icon = null;

            if (_hoveredAndroidSlot == null || _androidEquipmentController == null
                || !_androidEquipmentController.TryGetItem(_hoveredAndroidSlot.Value, out CompanionEquipItem item))
            {
                return false;
            }

            title = string.IsNullOrWhiteSpace(item.Name) ? $"Android Equip {item.ItemId}" : item.Name;
            line1 = BuildCompanionEquipmentSummaryLine(item);
            line2 = $"Slot: {ResolveAndroidSlotLabel(_hoveredAndroidSlot.Value)}";
            description = BuildCompanionEquipmentDescription(item);
            icon = item.IconRaw ?? item.Icon ?? item.CharacterPart?.IconRaw ?? item.CharacterPart?.Icon;
            return true;
        }

        private bool TryBuildMechanicTooltip(out string title, out string line1, out string line2, out string description, out IDXObject icon)
        {
            title = null;
            line1 = null;
            line2 = null;
            description = null;
            icon = null;

            if (_hoveredMechanicSlot == null || _mechanicEquipmentController == null
                || !_mechanicEquipmentController.TryGetItem(_hoveredMechanicSlot.Value, out CompanionEquipItem item))
            {
                return false;
            }

            title = string.IsNullOrWhiteSpace(item.Name) ? $"Mechanic Equip {item.ItemId}" : item.Name;
            line1 = $"Item ID: {item.ItemId}";
            line2 = $"Slot: {ResolveMechanicSlotLabel(_hoveredMechanicSlot.Value)}";
            description = item.Description;
            icon = item.IconRaw ?? item.Icon ?? item.CharacterPart?.IconRaw ?? item.CharacterPart?.Icon;
            return true;
        }

        private static string ResolveItemDescription(int itemId, string fallbackCategory = null)
        {
            if (HaCreator.Program.InfoManager?.ItemNameCache != null
                && HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo))
            {
                return !string.IsNullOrWhiteSpace(itemInfo.Item3)
                    ? itemInfo.Item3
                    : itemInfo.Item1;
            }

            return fallbackCategory ?? string.Empty;
        }

        private static string BuildEquipmentSummaryLine(CharacterPart part)
        {
            var segments = new List<string>();
            if (part.ItemId > 0)
            {
                segments.Add($"Item ID: {part.ItemId}");
            }

            if (!string.IsNullOrWhiteSpace(part.ItemCategory))
            {
                segments.Add(part.ItemCategory);
            }

            AppendStatSegment(segments, "STR", part.BonusSTR);
            AppendStatSegment(segments, "DEX", part.BonusDEX);
            AppendStatSegment(segments, "INT", part.BonusINT);
            AppendStatSegment(segments, "LUK", part.BonusLUK);
            AppendStatSegment(segments, "HP", part.BonusHP);
            AppendStatSegment(segments, "MP", part.BonusMP);
            AppendStatSegment(segments, "ATT", part.BonusWeaponAttack);
            AppendStatSegment(segments, "M.ATT", part.BonusMagicAttack);
            AppendStatSegment(segments, "DEF", part.BonusWeaponDefense);
            AppendStatSegment(segments, "M.DEF", part.BonusMagicDefense);
            AppendStatSegment(segments, "ACC", part.BonusAccuracy);
            AppendStatSegment(segments, "AVOID", part.BonusAvoidability);
            AppendStatSegment(segments, "Speed", part.BonusSpeed);
            AppendStatSegment(segments, "Jump", part.BonusJump);

            if (part.UpgradeSlots > 0)
            {
                segments.Add($"Slots +{part.UpgradeSlots}");
            }

            if (part is WeaponPart weapon && weapon.AttackSpeed > 0)
            {
                segments.Add($"Speed {weapon.AttackSpeed}");
            }

            return string.Join("  ", segments);
        }

        private static string BuildEquipmentRequirementLine(CharacterPart part, EquipSlot hoveredSlot)
        {
            var segments = new List<string>
            {
                $"Slot: {ResolveSlotLabel(hoveredSlot)}"
            };

            if (part.RequiredLevel > 0)
            {
                segments.Add($"Req Lv {part.RequiredLevel}");
            }

            AppendRequirementSegment(segments, "STR", part.RequiredSTR);
            AppendRequirementSegment(segments, "DEX", part.RequiredDEX);
            AppendRequirementSegment(segments, "INT", part.RequiredINT);
            AppendRequirementSegment(segments, "LUK", part.RequiredLUK);

            if (part.IsCash)
            {
                segments.Add("Cash");
            }

            if (part.Durability.HasValue)
            {
                if (part.MaxDurability.HasValue && part.MaxDurability.Value > 0)
                {
                    segments.Add($"Durability {Math.Max(0, part.Durability.Value)}/{part.MaxDurability.Value}");
                }
                else
                {
                    segments.Add($"Durability {Math.Max(0, part.Durability.Value)}");
                }
            }

            if (part.ExpirationDateUtc.HasValue)
            {
                segments.Add($"Expires {part.ExpirationDateUtc.Value.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
            }

            return string.Join("  ", segments);
        }

        private static string BuildEquipmentDescription(CharacterPart part)
        {
            if (!string.IsNullOrWhiteSpace(part.Description))
            {
                return part.Description;
            }

            if (part is WeaponPart weapon && !string.IsNullOrWhiteSpace(weapon.WeaponType))
            {
                return weapon.WeaponType;
            }

            return string.Empty;
        }

        private static string BuildCompanionEquipmentSummaryLine(CompanionEquipItem item)
        {
            if (item?.CharacterPart != null)
            {
                return BuildEquipmentSummaryLine(item.CharacterPart);
            }

            return item != null ? $"Item ID: {item.ItemId}" : string.Empty;
        }

        private static string BuildCompanionEquipmentDescription(CompanionEquipItem item)
        {
            if (!string.IsNullOrWhiteSpace(item?.Description))
            {
                return item.Description;
            }

            if (item?.CharacterPart != null)
            {
                return BuildEquipmentDescription(item.CharacterPart);
            }

            return string.Empty;
        }

        private static void AppendStatSegment(List<string> segments, string label, int value)
        {
            if (value > 0)
            {
                segments.Add($"{label} +{value}");
            }
        }

        private static void AppendRequirementSegment(List<string> segments, string label, int value)
        {
            if (value > 0)
            {
                segments.Add($"{label} {value}");
            }
        }

        private void DrawItemTooltip(
            SpriteBatch sprite,
            string title,
            string line1,
            string line2,
            string description,
            Texture2D itemTexture,
            IDXObject itemIcon,
            int renderWidth,
            int renderHeight)
        {
            int tooltipWidth = ResolveTooltipWidth();
            int textLeftOffset = TOOLTIP_PADDING + SLOT_SIZE + TOOLTIP_ICON_GAP;
            float titleWidth = tooltipWidth - (TOOLTIP_PADDING * 2);
            float sectionWidth = tooltipWidth - textLeftOffset - TOOLTIP_PADDING;
            string[] wrappedTitle = WrapTooltipText(title, titleWidth);
            string[] wrappedLine1 = WrapTooltipText(line1, sectionWidth);
            string[] wrappedLine2 = WrapTooltipText(line2, sectionWidth);
            string[] wrappedDescription = WrapTooltipText(description, sectionWidth);
            float titleHeight = MeasureLinesHeight(wrappedTitle);
            float line1Height = MeasureLinesHeight(wrappedLine1);
            float line2Height = MeasureLinesHeight(wrappedLine2);
            float descriptionHeight = MeasureLinesHeight(wrappedDescription);
            float contentHeight = line1Height;
            if (line2Height > 0f)
                contentHeight += (contentHeight > 0f ? TOOLTIP_SECTION_GAP : 0f) + line2Height;
            if (descriptionHeight > 0f)
                contentHeight += (contentHeight > 0f ? TOOLTIP_SECTION_GAP : 0f) + descriptionHeight;

            float iconBlockHeight = Math.Max(SLOT_SIZE, contentHeight);
            int tooltipHeight = (int)Math.Ceiling((TOOLTIP_PADDING * 2) + titleHeight + TOOLTIP_TITLE_GAP + iconBlockHeight);
            int tooltipX = _lastMousePosition.X + TOOLTIP_OFFSET_X;
            int tooltipY = _lastMousePosition.Y + 20;
            int tooltipFrameIndex = 1;

            if (tooltipX + tooltipWidth > renderWidth - TOOLTIP_PADDING)
            {
                tooltipX = _lastMousePosition.X - tooltipWidth - TOOLTIP_OFFSET_X;
                tooltipFrameIndex = 0;
            }

            if (tooltipX < TOOLTIP_PADDING)
                tooltipX = TOOLTIP_PADDING;

            if (tooltipY + tooltipHeight > renderHeight - TOOLTIP_PADDING)
            {
                tooltipY = Math.Max(TOOLTIP_PADDING, _lastMousePosition.Y - tooltipHeight + TOOLTIP_OFFSET_Y);
                tooltipFrameIndex = 2;
            }

            Rectangle backgroundRect = new Rectangle(tooltipX, tooltipY, tooltipWidth, tooltipHeight);
            DrawTooltipBackground(sprite, backgroundRect, tooltipFrameIndex);

            int titleX = tooltipX + TOOLTIP_PADDING;
            int titleY = tooltipY + TOOLTIP_PADDING;
            DrawTooltipLines(sprite, wrappedTitle, titleX, titleY, new Color(255, 220, 120));

            int contentY = tooltipY + TOOLTIP_PADDING + (int)Math.Ceiling(titleHeight) + TOOLTIP_TITLE_GAP;
            int iconX = tooltipX + TOOLTIP_PADDING;
            if (itemIcon != null)
            {
                itemIcon.DrawBackground(sprite, null, null, iconX, contentY, Color.White, false, null);
            }
            else if (itemTexture != null)
            {
                sprite.Draw(itemTexture, new Rectangle(iconX, contentY, SLOT_SIZE, SLOT_SIZE), Color.White);
            }

            int textX = tooltipX + textLeftOffset;
            float sectionY = contentY;
            DrawTooltipLines(sprite, wrappedLine1, textX, sectionY, new Color(181, 224, 255));
            if (line1Height > 0f)
                sectionY += line1Height + (line2Height > 0f ? TOOLTIP_SECTION_GAP : 0f);
            DrawTooltipLines(sprite, wrappedLine2, textX, sectionY, Color.White);
            if (line2Height > 0f)
                sectionY += line2Height;
            if (descriptionHeight > 0f)
            {
                sectionY += ((line1Height > 0f || line2Height > 0f) ? TOOLTIP_SECTION_GAP : 0f);
                DrawTooltipLines(sprite, wrappedDescription, textX, sectionY, new Color(216, 216, 216));
            }
        }

        private void DrawDraggedItemOverlay(SpriteBatch sprite)
        {
            if (!_isDraggingItem || _draggedPart == null)
                return;

            IDXObject icon = _draggedPart.IconRaw ?? _draggedPart.Icon;
            if (icon != null)
            {
                icon.DrawBackground(
                    sprite,
                    null,
                    null,
                    _draggedItemPosition.X - (SLOT_SIZE / 2),
                    _draggedItemPosition.Y - (SLOT_SIZE / 2),
                    Color.White * 0.85f,
                    false,
                    null);
            }
        }

        private EquipSlot? GetSlotAtPosition(int mouseX, int mouseY)
        {
            if (_currentTab != TAB_CHARACTER)
                return null;

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
            if (_currentTab != TAB_PET || !_companionLayouts.TryGetValue(TAB_PET, out CompanionPaneLayout layout))
            {
                return null;
            }

            Point panePosition = GetCompanionPanePosition(layout, Position.X, Position.Y);
            IReadOnlyList<PetRuntime> pets = _petController?.ActivePets;
            int petCount = pets?.Count ?? 0;
            for (int i = 0; i < Math.Min(_petSlotPositions.Length, petCount); i++)
            {
                Rectangle slotRect = new Rectangle(
                    panePosition.X + _petSlotPositions[i].X,
                    panePosition.Y + _petSlotPositions[i].Y,
                    SLOT_SIZE,
                    SLOT_SIZE);
                if (slotRect.Contains(mouseX, mouseY))
                {
                    return i;
                }
            }

            return null;
        }

        private DragonEquipSlot? GetHoveredDragonSlot(int mouseX, int mouseY)
        {
            if (_currentTab != TAB_DRAGON || !_companionLayouts.TryGetValue(TAB_DRAGON, out CompanionPaneLayout layout))
            {
                return null;
            }

            Point panePosition = GetCompanionPanePosition(layout, Position.X, Position.Y);
            foreach ((DragonEquipSlot slot, Point slotPosition) in _dragonSlotPositions)
            {
                if (_dragonEquipmentController == null || !_dragonEquipmentController.TryGetItem(slot, out _))
                {
                    continue;
                }

                Rectangle slotRect = new Rectangle(
                    panePosition.X + slotPosition.X,
                    panePosition.Y + slotPosition.Y,
                    SLOT_SIZE,
                    SLOT_SIZE);
                if (slotRect.Contains(mouseX, mouseY))
                {
                    return slot;
                }
            }

            return null;
        }

        private AndroidEquipSlot? GetHoveredAndroidSlot(int mouseX, int mouseY)
        {
            if (_currentTab != TAB_ANDROID || !_companionLayouts.TryGetValue(TAB_ANDROID, out CompanionPaneLayout layout))
            {
                return null;
            }

            Point panePosition = GetCompanionPanePosition(layout, Position.X, Position.Y);
            foreach ((AndroidEquipSlot slot, Point slotPosition) in _androidSlotPositions)
            {
                if (_androidEquipmentController == null || !_androidEquipmentController.TryGetItem(slot, out _))
                {
                    continue;
                }

                Rectangle slotRect = new Rectangle(
                    panePosition.X + slotPosition.X,
                    panePosition.Y + slotPosition.Y,
                    SLOT_SIZE,
                    SLOT_SIZE);
                if (slotRect.Contains(mouseX, mouseY))
                {
                    return slot;
                }
            }

            return null;
        }

        private MechanicEquipSlot? GetHoveredMechanicSlot(int mouseX, int mouseY)
        {
            if (_currentTab != TAB_MECHANIC || !_companionLayouts.TryGetValue(TAB_MECHANIC, out CompanionPaneLayout layout))
            {
                return null;
            }

            Point panePosition = GetCompanionPanePosition(layout, Position.X, Position.Y);
            foreach ((MechanicEquipSlot slot, Point slotPosition) in _mechanicSlotPositions)
            {
                if (_mechanicEquipmentController == null || !_mechanicEquipmentController.TryGetItem(slot, out _))
                {
                    continue;
                }

                Rectangle slotRect = new Rectangle(
                    panePosition.X + slotPosition.X,
                    panePosition.Y + slotPosition.Y,
                    SLOT_SIZE,
                    SLOT_SIZE);
                if (slotRect.Contains(mouseX, mouseY))
                {
                    return slot;
                }
            }

            return null;
        }

        private void UpdateTabButtonStates()
        {
            _btnSlot?.SetButtonState(_currentTab == TAB_CHARACTER ? UIObjectState.Pressed : UIObjectState.Normal);
            _btnPet?.SetButtonState(_currentTab == TAB_PET ? UIObjectState.Pressed : UIObjectState.Normal);
            _btnDragon?.SetButtonState(_currentTab == TAB_DRAGON ? UIObjectState.Pressed : UIObjectState.Normal);
            _btnMechanic?.SetButtonState(_currentTab == TAB_MECHANIC ? UIObjectState.Pressed : UIObjectState.Normal);
            _btnAndroid?.SetButtonState(_currentTab == TAB_ANDROID ? UIObjectState.Pressed : UIObjectState.Normal);
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
            if (targetState.IsDisabled || !CanDisplayPartInSlot(_draggedPart, targetUiSlot.Value))
                return false;

            CharacterPart targetPart = ResolveEquippedPart(targetUiSlot.Value);
            if (targetPart != null && !CanDisplayPartInSlot(targetPart, _draggedSlot ?? targetUiSlot.Value))
                return false;

            if (_draggedPart.Slot == targetSlot.Value || (targetPart != null && ReferenceEquals(targetPart, _draggedPart)))
                return true;

            _characterBuild?.Unequip(_draggedPart.Slot);
            if (targetPart != null)
            {
                _characterBuild?.Unequip(targetPart.Slot);
                targetPart.Slot = _draggedPart.Slot;
                _characterBuild?.Equip(targetPart);
            }

            _draggedPart.Slot = ResolveTargetSlot(targetUiSlot.Value, _draggedPart);
            _characterBuild?.Equip(_draggedPart);
            return true;
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
                return "Release to move this equipment item.";

            return $"Swap with {(string.IsNullOrWhiteSpace(targetPart.Name) ? $"Equip {targetPart.ItemId}" : targetPart.Name)}.";
        }

        private static CharacterEquipSlot ResolveTargetSlot(EquipSlot uiSlot, CharacterPart part)
        {
            CharacterEquipSlot? mappedSlot = MapToCharacterEquipSlot(uiSlot);
            if (!mappedSlot.HasValue)
                return part.Slot;

            if (mappedSlot.Value == CharacterEquipSlot.Coat && part.Slot == CharacterEquipSlot.Longcoat)
                return CharacterEquipSlot.Longcoat;

            return mappedSlot.Value;
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
                CharacterEquipSlot.Coat => part.Slot == CharacterEquipSlot.Coat || part.Slot == CharacterEquipSlot.Longcoat,
                CharacterEquipSlot.Pendant => part.Slot == CharacterEquipSlot.Pendant,
                _ => part.Slot == mappedSlot.Value
            };
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
                EquipSlot.Pendant2 => CharacterEquipSlot.Pendant,
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
                EquipSlot.Totem1 => CharacterEquipSlot.TamingMob,
                EquipSlot.Totem2 => CharacterEquipSlot.Saddle,
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
                EquipSlot.Pendant2 => "Pendant",
                _ => slot.ToString()
            };
        }

        private static string ResolveDragonSlotLabel(DragonEquipSlot slot)
        {
            return slot switch
            {
                DragonEquipSlot.Mask => "Cap",
                DragonEquipSlot.Wings => "Wing Accessory",
                DragonEquipSlot.Pendant => "Pendant",
                DragonEquipSlot.Tail => "Tail Accessory",
                _ => slot.ToString()
            };
        }

        private static string ResolveAndroidSlotLabel(AndroidEquipSlot slot)
        {
            return slot switch
            {
                AndroidEquipSlot.FaceAccessory => "Face Accessory",
                AndroidEquipSlot.Clothes => "Clothes",
                AndroidEquipSlot.Glove => "Gloves",
                AndroidEquipSlot.Cape => "Mantle",
                _ => slot.ToString()
            };
        }

        private static string ResolveMechanicSlotLabel(MechanicEquipSlot slot)
        {
            return slot switch
            {
                MechanicEquipSlot.Engine => "Engine",
                MechanicEquipSlot.Frame => "Frame",
                MechanicEquipSlot.Transistor => "Trans",
                MechanicEquipSlot.Arm => "Arm",
                MechanicEquipSlot.Leg => "Leg",
                _ => slot.ToString()
            };
        }

        private int ResolveTooltipWidth()
        {
            int textureWidth = _tooltipFrames[1]?.Width ?? 0;
            return textureWidth > 0 ? textureWidth : TOOLTIP_FALLBACK_WIDTH;
        }

        private void DrawTooltipBackground(SpriteBatch sprite, Rectangle rect, int tooltipFrameIndex)
        {
            Texture2D tooltipFrame = tooltipFrameIndex >= 0 && tooltipFrameIndex < _tooltipFrames.Length
                ? _tooltipFrames[tooltipFrameIndex]
                : null;

            if (tooltipFrame != null)
            {
                sprite.Draw(tooltipFrame, rect, Color.White);
                return;
            }

            sprite.Draw(_debugPlaceholder, rect, new Color(18, 18, 26, 235));
            DrawTooltipBorder(sprite, rect);
        }

        private void DrawTooltipBorder(SpriteBatch sprite, Rectangle rect)
        {
            Color borderColor = new Color(214, 174, 82);
            sprite.Draw(_debugPlaceholder, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, 1), borderColor);
            sprite.Draw(_debugPlaceholder, new Rectangle(rect.X - 1, rect.Bottom, rect.Width + 2, 1), borderColor);
            sprite.Draw(_debugPlaceholder, new Rectangle(rect.X - 1, rect.Y, 1, rect.Height), borderColor);
            sprite.Draw(_debugPlaceholder, new Rectangle(rect.Right, rect.Y, 1, rect.Height), borderColor);
        }

        private void DrawTooltipLines(SpriteBatch sprite, string[] lines, int x, float y, Color color)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                DrawTooltipText(sprite, lines[i], new Vector2(x, y + (i * _font.LineSpacing)), color);
            }
        }

        private void DrawTooltipText(SpriteBatch sprite, string text, Vector2 position, Color color)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            sprite.DrawString(_font, text, position + Vector2.One, Color.Black);
            sprite.DrawString(_font, text, position, color);
        }

        private float MeasureLinesHeight(string[] lines)
        {
            if (lines == null || lines.Length == 0)
                return 0f;

            int nonEmptyLines = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                    nonEmptyLines++;
            }

            return nonEmptyLines > 0 ? nonEmptyLines * _font.LineSpacing : 0f;
        }

        private string[] WrapTooltipText(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
                return Array.Empty<string>();

            var lines = new List<string>();
            string[] paragraphs = text.Replace("\r\n", "\n").Split('\n');
            foreach (string paragraph in paragraphs)
            {
                string trimmed = paragraph.Trim();
                if (trimmed.Length == 0)
                    continue;

                string currentLine = string.Empty;
                string[] words = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
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
            }

            return lines.ToArray();
        }
    }
}
