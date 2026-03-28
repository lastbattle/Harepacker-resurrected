using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.UI.Controls;
using HaCreator.MapSimulator.Companions;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using CharacterBuild = HaCreator.MapSimulator.Character.CharacterBuild;
using CharacterLoader = HaCreator.MapSimulator.Character.CharacterLoader;
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
        private const int TOOLTIP_BITMAP_GAP = 1;
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

        private sealed class SpecialSlotChrome
        {
            public string Name { get; init; }
            public IDXObject Chrome { get; init; }
            public Point Offset { get; init; }
            public Func<CharacterBuild, bool> IsActive { get; init; }
        }

        public sealed class EquipTooltipAssets
        {
            public IReadOnlyDictionary<string, Texture2D> CanLabels { get; init; }
            public IReadOnlyDictionary<string, Texture2D> CannotLabels { get; init; }
            public IReadOnlyDictionary<string, Texture2D> PropertyLabels { get; init; }
            public IReadOnlyDictionary<string, Texture2D> ItemCategoryLabels { get; init; }
            public IReadOnlyDictionary<string, Texture2D> WeaponCategoryLabels { get; init; }
            public IReadOnlyDictionary<string, Texture2D> SpeedLabels { get; init; }
            public IReadOnlyDictionary<string, Texture2D> GrowthEnabledLabels { get; init; }
            public IReadOnlyDictionary<string, Texture2D> GrowthDisabledLabels { get; init; }
            public Texture2D CashLabel { get; init; }
        }

        private enum CompanionDragKind
        {
            None,
            Pet,
            Dragon,
            Mechanic,
            Android
        }

        private readonly struct TooltipSection
        {
            public TooltipSection(string text, Color color)
            {
                Text = text;
                Color = color;
            }

            public string Text { get; }
            public Color Color { get; }
        }

        private readonly struct TooltipLabeledValueRow
        {
            public TooltipLabeledValueRow(
                Texture2D labelTexture,
                string fallbackLabel,
                string valueText,
                Color valueColor,
                IReadOnlyList<Texture2D> valueTextures = null)
            {
                LabelTexture = labelTexture;
                FallbackLabel = fallbackLabel;
                ValueText = valueText;
                ValueColor = valueColor;
                ValueTextures = valueTextures;
            }

            public Texture2D LabelTexture { get; }
            public string FallbackLabel { get; }
            public string ValueText { get; }
            public Color ValueColor { get; }
            public IReadOnlyList<Texture2D> ValueTextures { get; }
        }

        #region Fields
        private int _currentTab = TAB_CHARACTER;

        // Foreground texture (backgrnd2 - grid overlay)
        private IDXObject _foreground;
        private Point _foregroundOffset;

        // Slot labels and character silhouette (backgrnd3/backgrnd3_dual)
        private IDXObject _slotLabels;
        private Point _slotLabelsOffset;
        private IDXObject _dualSlotLabels;
        private Point _dualSlotLabelsOffset;
        private readonly List<SpecialSlotChrome> _specialSlotChrome = new();

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
        private EquipTooltipAssets _equipTooltipAssets;
        private Texture2D _debugPlaceholder;
        private Texture2D _slotOverlayTexture;
        private SpriteFont _font;
        private CharacterBuild _characterBuild;
        private CharacterLoader _characterLoader;
        private PetController _petController;
        private PetEquipmentController _petEquipmentController;
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
        private CompanionDragKind _draggedCompanionKind;
        private InventorySlotData _draggedCompanionSlotData;
        private CompanionEquipItem _draggedCompanionItem;
        private int _draggedPetRuntimeId;
        private DragonEquipSlot? _draggedDragonSlot;
        private MechanicEquipSlot? _draggedMechanicSlot;
        private AndroidEquipSlot? _draggedAndroidSlot;
        #endregion

        #region Properties
        public override string WindowName => "Equipment";
        public Action<CharacterEquipSlot> ItemUpgradeRequested { get; set; }
        public Func<int, string> EquipmentEquipGuard { get; set; }
        public Action<string> EquipmentEquipBlocked { get; set; }
        public override CharacterBuild CharacterBuild
        {
            get => _characterBuild;
            set => _characterBuild = value;
        }
        public bool IsDraggingItem => _isDraggingItem;
        public bool HasDraggedCompanionItem => _draggedCompanionKind != CompanionDragKind.None && _draggedCompanionSlotData != null;
        public InventorySlotData DraggedCompanionSlotData => _draggedCompanionSlotData?.Clone();

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

        public void SetDualSlotLabels(IDXObject slotLabels, int offsetX, int offsetY)
        {
            _dualSlotLabels = slotLabels;
            _dualSlotLabelsOffset = new Point(offsetX, offsetY);
        }

        public void SetSpecialSlotChrome(string chromeName, IDXObject chrome, int offsetX, int offsetY)
        {
            if (string.IsNullOrWhiteSpace(chromeName) || chrome == null)
            {
                return;
            }

            Func<CharacterBuild, bool> isActive = ResolveSpecialSlotChromeActivation(chromeName);
            if (isActive == null)
            {
                return;
            }

            _specialSlotChrome.Add(new SpecialSlotChrome
            {
                Name = chromeName,
                Chrome = chrome,
                Offset = new Point(offsetX, offsetY),
                IsActive = isActive
            });
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

        public void SetEquipTooltipAssets(EquipTooltipAssets assets)
        {
            _equipTooltipAssets = assets;
        }

        public void SetPetController(PetController petController)
        {
            _petController = petController;
        }

        public void SetCharacterLoader(CharacterLoader characterLoader)
        {
            _characterLoader = characterLoader;
        }

        public void SetPetEquipmentController(PetEquipmentController petEquipmentController)
        {
            _petEquipmentController = petEquipmentController;
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
            IDXObject activeSlotLabels = ResolveCharacterSlotLabelsChrome();
            Point activeSlotLabelsOffset = ReferenceEquals(activeSlotLabels, _dualSlotLabels)
                ? _dualSlotLabelsOffset
                : _slotLabelsOffset;
            if (activeSlotLabels != null)
            {
                activeSlotLabels.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    windowX + activeSlotLabelsOffset.X, windowY + activeSlotLabelsOffset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            DrawSpecialSlotChrome(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo, windowX, windowY);

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
            return _currentTab switch
            {
                TAB_CHARACTER => GetSlotAtPosition(mouseX, mouseY).HasValue,
                TAB_PET => GetHoveredPetIndex(mouseX, mouseY).HasValue,
                TAB_DRAGON => GetDragonSlotAtPosition(mouseX, mouseY).HasValue,
                TAB_MECHANIC => GetMechanicSlotAtPosition(mouseX, mouseY).HasValue,
                TAB_ANDROID => GetAndroidSlotAtPosition(mouseX, mouseY).HasValue,
                _ => false
            };
        }

        public bool TryHandleInventoryDrop(int mouseX, int mouseY, InventorySlotData draggedSlotData, out IReadOnlyList<InventorySlotData> displacedSlots)
        {
            displacedSlots = Array.Empty<InventorySlotData>();
            if (draggedSlotData == null
                || draggedSlotData.IsDisabled
                || InventoryItemMetadataResolver.ResolveInventoryType(draggedSlotData.ItemId) != InventoryType.EQUIP)
            {
                return false;
            }

            switch (_currentTab)
            {
                case TAB_PET:
                {
                    int? targetIndex = GetHoveredPetIndex(mouseX, mouseY);
                    if (!targetIndex.HasValue || _petEquipmentController == null)
                    {
                        return false;
                    }

                    PetRuntime targetPet = ResolvePetByIndex(targetIndex.Value);
                    if (!_petEquipmentController.TryEquipItem(targetPet, draggedSlotData.ItemId, out IReadOnlyList<CompanionEquipItem> displacedItems, out string rejectReason))
                    {
                        if (!string.IsNullOrWhiteSpace(rejectReason))
                        {
                            EquipmentEquipBlocked?.Invoke(rejectReason);
                        }

                        return false;
                    }

                    displacedSlots = CreateInventorySlots(displacedItems);
                    return true;
                }
                case TAB_DRAGON:
                {
                    DragonEquipSlot? targetSlot = GetDragonSlotAtPosition(mouseX, mouseY);
                    IReadOnlyList<CompanionEquipItem> displacedItems = Array.Empty<CompanionEquipItem>();
                    string rejectReason = null;
                    if (!targetSlot.HasValue
                        || _dragonEquipmentController == null
                        || !_dragonEquipmentController.TryEquipItem(targetSlot.Value, draggedSlotData.ItemId, out displacedItems, out rejectReason))
                    {
                        if (!string.IsNullOrWhiteSpace(rejectReason))
                        {
                            EquipmentEquipBlocked?.Invoke(rejectReason);
                        }

                        return false;
                    }

                    displacedSlots = CreateInventorySlots(displacedItems);
                    return true;
                }
                case TAB_MECHANIC:
                {
                    MechanicEquipSlot? targetSlot = GetMechanicSlotAtPosition(mouseX, mouseY);
                    IReadOnlyList<CompanionEquipItem> displacedItems = Array.Empty<CompanionEquipItem>();
                    string rejectReason = null;
                    if (!targetSlot.HasValue
                        || _mechanicEquipmentController == null
                        || !_mechanicEquipmentController.TryEquipItem(targetSlot.Value, draggedSlotData.ItemId, out displacedItems, out rejectReason))
                    {
                        if (!string.IsNullOrWhiteSpace(rejectReason))
                        {
                            EquipmentEquipBlocked?.Invoke(rejectReason);
                        }

                        return false;
                    }

                    displacedSlots = CreateInventorySlots(displacedItems);
                    return true;
                }
                case TAB_ANDROID:
                {
                    AndroidEquipSlot? targetSlot = GetAndroidSlotAtPosition(mouseX, mouseY);
                    IReadOnlyList<CompanionEquipItem> displacedItems = Array.Empty<CompanionEquipItem>();
                    string rejectReason = null;
                    if (!targetSlot.HasValue
                        || _androidEquipmentController == null
                        || !_androidEquipmentController.TryEquipItem(targetSlot.Value, draggedSlotData.ItemId, out displacedItems, out rejectReason))
                    {
                        if (!string.IsNullOrWhiteSpace(rejectReason))
                        {
                            EquipmentEquipBlocked?.Invoke(rejectReason);
                        }

                        return false;
                    }

                    displacedSlots = CreateInventorySlots(displacedItems);
                    return true;
                }
                case TAB_CHARACTER:
                    return TryHandleCharacterInventoryDrop(mouseX, mouseY, draggedSlotData, out displacedSlots);
                default:
                    return false;
            }
        }

        private bool TryHandleCharacterInventoryDrop(int mouseX, int mouseY, InventorySlotData draggedSlotData, out IReadOnlyList<InventorySlotData> displacedSlots)
        {
            displacedSlots = Array.Empty<InventorySlotData>();

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

            CharacterPart incomingPart = _characterLoader?.LoadEquipment(draggedSlotData.ItemId) ?? CreateInventoryEquipmentPart(draggedSlotData);
            if (incomingPart == null)
            {
                string itemName = string.IsNullOrWhiteSpace(draggedSlotData.ItemName)
                    ? $"Item #{draggedSlotData.ItemId}"
                    : draggedSlotData.ItemName;
                NotifyEquipmentEquipBlocked($"Unable to load {itemName} as an equipment item.");
                return false;
            }

            if (!CanDisplayPartInSlot(incomingPart, targetUiSlot.Value))
            {
                NotifyEquipmentEquipBlocked(BuildSlotMismatchRejectReason(incomingPart));
                return false;
            }

            if (!TryGetEquipRequirementRejectReason(incomingPart, _characterBuild, out string requirementRejectReason))
            {
                NotifyEquipmentEquipBlocked(requirementRejectReason);
                return false;
            }

            string restrictionMessage = EquipmentEquipGuard?.Invoke(draggedSlotData.ItemId);
            if (!string.IsNullOrWhiteSpace(restrictionMessage))
            {
                EquipmentEquipBlocked?.Invoke(restrictionMessage);
                return false;
            }

            CharacterPart targetPart = ResolveEquippedPart(targetUiSlot.Value);
            if (targetPart != null && ReferenceEquals(targetPart, incomingPart))
            {
                return true;
            }

            CharacterEquipSlot resolvedTargetSlot = ResolveTargetSlot(targetUiSlot.Value, incomingPart);
            IReadOnlyList<CharacterPart> displacedParts = _characterBuild?.PlaceEquipment(incomingPart, resolvedTargetSlot)
                ?? Array.Empty<CharacterPart>();
            displacedSlots = CreateInventorySlots(displacedParts);
            return true;
        }

        public void OnEquipmentMouseDown(int mouseX, int mouseY)
        {
            _lastMousePosition = new Point(mouseX, mouseY);
            if (TryBeginDraggedCompanion(mouseX, mouseY))
            {
                return;
            }

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
            if (!_isDraggingItem)
            {
                CancelEquipmentDrag();
                return false;
            }

            bool moved = HasDraggedCompanionItem
                ? TryHandleDraggedCompanionDrop(mouseX, mouseY)
                : _draggedPart != null && TryDropDraggedEquipment(mouseX, mouseY);
            CancelEquipmentDrag();
            return moved;
        }

        public void CancelEquipmentDrag()
        {
            _isDraggingItem = false;
            _draggedSlot = null;
            _draggedPart = null;
            _draggedCompanionKind = CompanionDragKind.None;
            _draggedCompanionSlotData = null;
            _draggedCompanionItem = null;
            _draggedPetRuntimeId = 0;
            _draggedDragonSlot = null;
            _draggedMechanicSlot = null;
            _draggedAndroidSlot = null;
        }

        public bool TryCommitDraggedCompanionRemoval(out InventorySlotData slotData)
        {
            slotData = null;
            if (!HasDraggedCompanionItem)
            {
                return false;
            }

            bool removed = _draggedCompanionKind switch
            {
                CompanionDragKind.Pet => TryUnequipDraggedPetItem(),
                CompanionDragKind.Dragon => _draggedDragonSlot.HasValue
                                            && _dragonEquipmentController != null
                                            && _dragonEquipmentController.TryUnequipItem(_draggedDragonSlot.Value, out _),
                CompanionDragKind.Mechanic => _draggedMechanicSlot.HasValue
                                              && _mechanicEquipmentController != null
                                              && _mechanicEquipmentController.TryUnequipItem(_draggedMechanicSlot.Value, out _),
                CompanionDragKind.Android => _draggedAndroidSlot.HasValue
                                             && _androidEquipmentController != null
                                             && _androidEquipmentController.TryUnequipItem(_draggedAndroidSlot.Value, out _),
                _ => false
            };

            if (!removed)
            {
                return false;
            }

            slotData = _draggedCompanionSlotData.Clone();
            return true;
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
                IDXObject icon = ResolvePetSlotIcon(pet);
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

        private void DrawSpecialSlotChrome(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo,
            int windowX,
            int windowY)
        {
            foreach (SpecialSlotChrome chrome in _specialSlotChrome)
            {
                if (chrome.Chrome == null || chrome.IsActive?.Invoke(_characterBuild) != true)
                {
                    continue;
                }

                chrome.Chrome.DrawBackground(
                    sprite,
                    skeletonMeshRenderer,
                    gameTime,
                    windowX + chrome.Offset.X,
                    windowY + chrome.Offset.Y,
                    Color.White,
                    false,
                    drawReflectionInfo);
            }
        }

        internal static bool ShouldUseDualSlotLabels(CharacterBuild build)
        {
            return build?.IsPendantSlotExtensionActive == true;
        }

        private static Func<CharacterBuild, bool> ResolveSpecialSlotChromeActivation(string chromeName)
        {
            return chromeName switch
            {
                "cashPendant" => build => build?.IsPendantSlotExtensionActive == true,
                "charmPocket" => build => build?.IsPocketSlotAvailable == true,
                _ => null
            };
        }

        private IDXObject ResolveCharacterSlotLabelsChrome()
        {
            if (ShouldUseDualSlotLabels(_characterBuild) && _dualSlotLabels != null)
            {
                return _dualSlotLabels;
            }

            return _slotLabels ?? _dualSlotLabels;
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
                int petIndex = _hoveredPetIndex ?? -1;
                IReadOnlyList<PetRuntime> pets = _petController?.ActivePets;
                PetRuntime hoveredPet = petIndex >= 0 && pets != null && petIndex < pets.Count ? pets[petIndex] : null;
                if (hoveredPet != null
                    && _petEquipmentController != null
                    && _petEquipmentController.TryGetItem(hoveredPet, out CompanionEquipItem petEquipItem)
                    && TryCreateTooltipPart(petEquipItem, out CharacterPart petTooltipPart))
                {
                    DrawCharacterPartTooltip(
                        sprite,
                        petTooltipPart,
                        petTitle,
                        petDescription,
                        petIcon ?? petEquipItem.IconRaw ?? petEquipItem.Icon ?? petTooltipPart.IconRaw ?? petTooltipPart.Icon,
                        $"Pet {petIndex + 1}",
                        renderWidth,
                        renderHeight,
                        BuildPetTooltipSections(petEquipItem, petLine2));
                    return;
                }

                DrawItemTooltip(
                    sprite,
                    petTitle,
                    null,
                    null,
                    petDescription,
                    null,
                    petIcon,
                    renderWidth,
                    renderHeight,
                    CreateTooltipSections(
                        (petLine1, new Color(181, 224, 255)),
                        (petLine2, Color.White)));
                return;
            }

            if (_currentTab == TAB_DRAGON && TryBuildDragonTooltip(out string dragonTitle, out string dragonLine1, out string dragonLine2, out string dragonDescription, out IDXObject dragonIcon))
            {
                if (_hoveredDragonSlot.HasValue
                    && _dragonEquipmentController != null
                    && _dragonEquipmentController.TryGetItem(_hoveredDragonSlot.Value, out CompanionEquipItem dragonItem)
                    && TryCreateTooltipPart(dragonItem, out CharacterPart dragonTooltipPart))
                {
                    DrawCharacterPartTooltip(
                        sprite,
                        dragonTooltipPart,
                        dragonTitle,
                        dragonDescription,
                        dragonIcon ?? dragonItem.IconRaw ?? dragonItem.Icon ?? dragonTooltipPart.IconRaw ?? dragonTooltipPart.Icon,
                        ResolveDragonSlotLabel(_hoveredDragonSlot.Value),
                        renderWidth,
                        renderHeight,
                        Array.Empty<TooltipSection>());
                    return;
                }

                DrawItemTooltip(
                    sprite,
                    dragonTitle,
                    null,
                    null,
                    dragonDescription,
                    null,
                    dragonIcon,
                    renderWidth,
                    renderHeight,
                    CreateTooltipSections(
                        (dragonLine1, new Color(181, 224, 255)),
                        (dragonLine2, Color.White)));
                return;
            }

            if (_currentTab == TAB_MECHANIC && TryBuildMechanicTooltip(out string mechanicTitle, out string mechanicLine1, out string mechanicLine2, out string mechanicDescription, out IDXObject mechanicIcon))
            {
                if (_hoveredMechanicSlot.HasValue
                    && _mechanicEquipmentController != null
                    && _mechanicEquipmentController.TryGetItem(_hoveredMechanicSlot.Value, out CompanionEquipItem mechanicItem)
                    && TryCreateTooltipPart(mechanicItem, out CharacterPart mechanicTooltipPart))
                {
                    DrawCharacterPartTooltip(
                        sprite,
                        mechanicTooltipPart,
                        mechanicTitle,
                        mechanicDescription,
                        mechanicIcon ?? mechanicItem.IconRaw ?? mechanicItem.Icon ?? mechanicTooltipPart.IconRaw ?? mechanicTooltipPart.Icon,
                        ResolveMechanicSlotLabel(_hoveredMechanicSlot.Value),
                        renderWidth,
                        renderHeight,
                        Array.Empty<TooltipSection>());
                    return;
                }

                DrawItemTooltip(
                    sprite,
                    mechanicTitle,
                    null,
                    null,
                    mechanicDescription,
                    null,
                    mechanicIcon,
                    renderWidth,
                    renderHeight,
                    CreateTooltipSections(
                        (mechanicLine1, new Color(181, 224, 255)),
                        (mechanicLine2, Color.White)));
                return;
            }

            if (_currentTab == TAB_ANDROID && TryBuildAndroidTooltip(out string androidTitle, out string androidLine1, out string androidLine2, out string androidDescription, out IDXObject androidIcon))
            {
                if (_hoveredAndroidSlot.HasValue
                    && _androidEquipmentController != null
                    && _androidEquipmentController.TryGetItem(_hoveredAndroidSlot.Value, out CompanionEquipItem androidItem)
                    && TryCreateTooltipPart(androidItem, out CharacterPart androidTooltipPart))
                {
                    DrawCharacterPartTooltip(
                        sprite,
                        androidTooltipPart,
                        androidTitle,
                        androidDescription,
                        androidIcon ?? androidItem.IconRaw ?? androidItem.Icon ?? androidTooltipPart.IconRaw ?? androidTooltipPart.Icon,
                        ResolveAndroidSlotLabel(_hoveredAndroidSlot.Value),
                        renderWidth,
                        renderHeight,
                        Array.Empty<TooltipSection>());
                    return;
                }

                DrawItemTooltip(
                    sprite,
                    androidTitle,
                    null,
                    null,
                    androidDescription,
                    null,
                    androidIcon,
                    renderWidth,
                    renderHeight,
                    CreateTooltipSections(
                        (androidLine1, new Color(181, 224, 255)),
                        (androidLine2, Color.White)));
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
            string description = BuildEquipmentDescription(part);
            DrawCharacterPartTooltip(
                sprite,
                part,
                itemName,
                description,
                part.IconRaw ?? part.Icon,
                ResolveSlotLabel(hoveredSlot),
                renderWidth,
                renderHeight,
                BuildEquipmentVisualStateSections(part, characterSlot));
        }

        private void DrawDraggedComparisonTooltip(SpriteBatch sprite, EquipSlot hoveredSlot, int renderWidth, int renderHeight)
        {
            string itemName = string.IsNullOrWhiteSpace(_draggedPart.Name) ? $"Equip {_draggedPart.ItemId}" : _draggedPart.Name;
            CharacterEquipSlot? targetSlot = MapToCharacterEquipSlot(hoveredSlot);
            var sections = new List<TooltipSection>(BuildEquipmentVisualStateSections(_draggedPart, targetSlot))
            {
                new TooltipSection(BuildDragCompareDescription(hoveredSlot), new Color(255, 214, 156))
            };
            DrawCharacterPartTooltip(
                sprite,
                _draggedPart,
                itemName,
                BuildEquipmentDescription(_draggedPart),
                _draggedPart.IconRaw ?? _draggedPart.Icon,
                ResolveSlotLabel(hoveredSlot),
                renderWidth,
                renderHeight,
                sections);
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
            if (_petEquipmentController != null && _petEquipmentController.TryGetItem(pet, out CompanionEquipItem item))
            {
                title = string.IsNullOrWhiteSpace(item.Name) ? $"Pet Equip {item.ItemId}" : item.Name;
                line1 = BuildCompanionEquipmentSummaryLine(item);
                line2 = $"Pet {petIndex + 1}: {pet.Name}  Auto Loot: {(pet.AutoLootEnabled ? "On" : "Off")}";
                description = BuildCompanionEquipmentDescription(item);
                icon = item.IconRaw ?? item.Icon ?? pet.Definition?.IconRaw ?? pet.Definition?.Icon;
                return true;
            }

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
            line1 = BuildCompanionEquipmentSummaryLine(item);
            line2 = BuildCompanionRequirementLine(item, ResolveDragonSlotLabel(_hoveredDragonSlot.Value));
            description = BuildCompanionEquipmentDescription(item);
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
            line2 = BuildCompanionRequirementLine(item, ResolveAndroidSlotLabel(_hoveredAndroidSlot.Value));
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
            line1 = BuildCompanionEquipmentSummaryLine(item);
            line2 = BuildCompanionRequirementLine(item, ResolveMechanicSlotLabel(_hoveredMechanicSlot.Value));
            description = BuildCompanionEquipmentDescription(item);
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
            return BuildEquipmentRequirementLine(part, ResolveSlotLabel(hoveredSlot));
        }

        private static string BuildEquipmentRequirementLine(CharacterPart part, string slotLabel)
        {
            var segments = new List<string>
            {
                $"Slot: {slotLabel}"
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

        private static string BuildDetailedRequirementLine(CharacterPart part)
        {
            if (part == null)
            {
                return string.Empty;
            }

            var segments = new List<string>();
            string requiredJobs = ResolveRequiredJobNames(part.RequiredJobMask);
            if (!string.IsNullOrWhiteSpace(requiredJobs))
            {
                segments.Add($"Req Job {requiredJobs}");
            }

            if (part.RequiredFame > 0)
            {
                segments.Add($"Req Fame {part.RequiredFame}");
            }

            return string.Join("  ", segments);
        }

        private static string BuildCashLayerLine(CharacterPart part, CharacterPart hiddenPart)
        {
            if (part?.IsCash != true || hiddenPart == null)
            {
                return string.Empty;
            }

            string hiddenName = string.IsNullOrWhiteSpace(hiddenPart.Name)
                ? $"Equip {hiddenPart.ItemId}"
                : hiddenPart.Name;
            return $"Cash appearance active  Base equip: {hiddenName}";
        }

        private static string BuildEquipmentEligibilityLine(CharacterPart part, CharacterBuild build)
        {
            if (part == null || build == null)
            {
                return string.Empty;
            }

            if (MeetsEquipRequirements(part, build))
            {
                return "Can equip";
            }

            var failures = new List<string>();
            if (part.RequiredLevel > 0 && build.Level < part.RequiredLevel)
            {
                failures.Add($"Lv {part.RequiredLevel}");
            }
            if (part.RequiredSTR > 0 && build.TotalSTR < part.RequiredSTR)
            {
                failures.Add($"STR {part.RequiredSTR}");
            }
            if (part.RequiredDEX > 0 && build.TotalDEX < part.RequiredDEX)
            {
                failures.Add($"DEX {part.RequiredDEX}");
            }
            if (part.RequiredINT > 0 && build.TotalINT < part.RequiredINT)
            {
                failures.Add($"INT {part.RequiredINT}");
            }
            if (part.RequiredLUK > 0 && build.TotalLUK < part.RequiredLUK)
            {
                failures.Add($"LUK {part.RequiredLUK}");
            }
            if (part.RequiredFame > 0 && build.Fame < part.RequiredFame)
            {
                failures.Add($"Fame {part.RequiredFame}");
            }
            if (part.RequiredJobMask != 0 && !MatchesRequiredJobMask(part.RequiredJobMask, build.Job))
            {
                string requiredJobs = ResolveRequiredJobNames(part.RequiredJobMask);
                failures.Add(string.IsNullOrWhiteSpace(requiredJobs) ? "job" : requiredJobs);
            }

            return failures.Count == 0
                ? "Cannot equip"
                : $"Cannot equip: {string.Join(", ", failures)}";
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

            if (item == null)
            {
                return string.Empty;
            }

            var segments = new List<string> { $"Item ID: {item.ItemId}" };
            if (!string.IsNullOrWhiteSpace(item.ItemCategory))
            {
                segments.Add(item.ItemCategory);
            }

            AppendStatSegment(segments, "STR", item.BonusSTR);
            AppendStatSegment(segments, "DEX", item.BonusDEX);
            AppendStatSegment(segments, "INT", item.BonusINT);
            AppendStatSegment(segments, "LUK", item.BonusLUK);
            AppendStatSegment(segments, "HP", item.BonusHP);
            AppendStatSegment(segments, "MP", item.BonusMP);
            AppendStatSegment(segments, "ATT", item.BonusWeaponAttack);
            AppendStatSegment(segments, "M.ATT", item.BonusMagicAttack);
            AppendStatSegment(segments, "DEF", item.BonusWeaponDefense);
            AppendStatSegment(segments, "M.DEF", item.BonusMagicDefense);
            AppendStatSegment(segments, "ACC", item.BonusAccuracy);
            AppendStatSegment(segments, "AVOID", item.BonusAvoidability);
            AppendStatSegment(segments, "Hands", item.BonusHands);
            AppendStatSegment(segments, "Speed", item.BonusSpeed);
            AppendStatSegment(segments, "Jump", item.BonusJump);
            if (item.UpgradeSlots > 0)
            {
                segments.Add($"Slots +{item.UpgradeSlots}");
            }

            return string.Join("  ", segments);
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

            return item?.ItemCategory ?? string.Empty;
        }

        private static string BuildCompanionRequirementLine(CompanionEquipItem item, string slotLabel)
        {
            if (item == null)
            {
                return $"Slot: {slotLabel}";
            }

            if (item.CharacterPart == null)
            {
                var segments = new List<string> { $"Slot: {slotLabel}" };
                if (item.RequiredLevel > 0)
                {
                    segments.Add($"Req Lv {item.RequiredLevel}");
                }

                AppendRequirementSegment(segments, "STR", item.RequiredSTR);
                AppendRequirementSegment(segments, "DEX", item.RequiredDEX);
                AppendRequirementSegment(segments, "INT", item.RequiredINT);
                AppendRequirementSegment(segments, "LUK", item.RequiredLUK);
                if (item.RequiredFame > 0)
                {
                    segments.Add($"Req Fame {item.RequiredFame}");
                }

                string requiredJobs = ResolveRequiredJobNames(item.RequiredJobMask);
                if (!string.IsNullOrWhiteSpace(requiredJobs))
                {
                    segments.Add($"Req Job {requiredJobs}");
                }

                return string.Join("  ", segments);
            }

            string requirementLine = BuildDetailedRequirementLine(item.CharacterPart);
            return string.IsNullOrWhiteSpace(requirementLine)
                ? $"Slot: {slotLabel}"
                : $"Slot: {slotLabel}  {requirementLine}";
        }

        private static bool TryCreateTooltipPart(CompanionEquipItem item, out CharacterPart tooltipPart)
        {
            tooltipPart = item?.CharacterPart;
            if (tooltipPart != null)
            {
                return true;
            }

            if (item == null || item.ItemId <= 0)
            {
                return false;
            }

            tooltipPart = new CharacterPart
            {
                ItemId = item.ItemId,
                Name = item.Name,
                Description = item.Description,
                ItemCategory = item.ItemCategory,
                IsCash = item.IsCash,
                RequiredJobMask = item.RequiredJobMask,
                RequiredFame = item.RequiredFame,
                RequiredLevel = item.RequiredLevel,
                RequiredSTR = item.RequiredSTR,
                RequiredDEX = item.RequiredDEX,
                RequiredINT = item.RequiredINT,
                RequiredLUK = item.RequiredLUK,
                BonusSTR = item.BonusSTR,
                BonusDEX = item.BonusDEX,
                BonusINT = item.BonusINT,
                BonusLUK = item.BonusLUK,
                BonusHP = item.BonusHP,
                BonusMP = item.BonusMP,
                BonusWeaponAttack = item.BonusWeaponAttack,
                BonusMagicAttack = item.BonusMagicAttack,
                BonusWeaponDefense = item.BonusWeaponDefense,
                BonusMagicDefense = item.BonusMagicDefense,
                BonusAccuracy = item.BonusAccuracy,
                BonusAvoidability = item.BonusAvoidability,
                BonusHands = item.BonusHands,
                BonusSpeed = item.BonusSpeed,
                BonusJump = item.BonusJump,
                UpgradeSlots = item.UpgradeSlots,
                KnockbackRate = item.KnockbackRate,
                TradeAvailable = item.TradeAvailable,
                IsTimeLimited = item.IsTimeLimited,
                Durability = item.Durability,
                MaxDurability = item.MaxDurability,
                Icon = item.Icon,
                IconRaw = item.IconRaw
            };
            return true;
        }

        private static IReadOnlyList<TooltipSection> BuildPetTooltipSections(CompanionEquipItem item, string petContextLine)
        {
            var sections = new List<TooltipSection>();
            if (!string.IsNullOrWhiteSpace(petContextLine))
            {
                sections.Add(new TooltipSection(petContextLine, Color.White));
            }

            string supportedPetsLine = BuildSupportedPetsLine(item);
            if (!string.IsNullOrWhiteSpace(supportedPetsLine))
            {
                sections.Add(new TooltipSection(supportedPetsLine, new Color(181, 224, 255)));
            }

            string excludedPetsLine = BuildExcludedPetsLine(item);
            if (!string.IsNullOrWhiteSpace(excludedPetsLine))
            {
                sections.Add(new TooltipSection(excludedPetsLine, new Color(255, 214, 156)));
            }

            return sections;
        }

        private static string BuildSupportedPetsLine(CompanionEquipItem item)
        {
            if (item?.SupportedPetItemIds == null || item.SupportedPetItemIds.Count == 0)
            {
                return string.Empty;
            }

            var petNames = new List<string>();
            foreach (int petItemId in item.SupportedPetItemIds)
            {
                if (HaCreator.Program.InfoManager?.ItemNameCache != null
                    && HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(petItemId, out Tuple<string, string, string> petInfo)
                    && !string.IsNullOrWhiteSpace(petInfo.Item2))
                {
                    petNames.Add(petInfo.Item2);
                }
                else
                {
                    petNames.Add(petItemId.ToString(CultureInfo.InvariantCulture));
                }
            }

            return petNames.Count == 0
                ? string.Empty
                : $"Usable by: {string.Join(", ", petNames)}";
        }

        private static string BuildExcludedPetsLine(CompanionEquipItem item)
        {
            return item?.ExcludedPetNames == null || item.ExcludedPetNames.Count == 0
                ? string.Empty
                : $"Except: {string.Join(", ", item.ExcludedPetNames)}";
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

        private void DrawCharacterPartTooltip(
            SpriteBatch sprite,
            CharacterPart part,
            string title,
            string description,
            IDXObject itemIcon,
            string slotLabel,
            int renderWidth,
            int renderHeight,
            IReadOnlyList<TooltipSection> footerSections)
        {
            if (_font == null || part == null || _equipTooltipAssets == null)
            {
                DrawItemTooltip(
                    sprite,
                    title,
                    null,
                    null,
                    description,
                    null,
                    itemIcon,
                    renderWidth,
                    renderHeight,
                    footerSections);
                return;
            }

            int tooltipWidth = ResolveTooltipWidth();
            int textLeftOffset = TOOLTIP_PADDING + SLOT_SIZE + TOOLTIP_ICON_GAP;
            int contentWidth = tooltipWidth - (TOOLTIP_PADDING * 2);
            int sectionWidth = tooltipWidth - textLeftOffset - TOOLTIP_PADDING;
            string[] wrappedTitle = WrapTooltipText(title, contentWidth);
            float titleHeight = MeasureLinesHeight(wrappedTitle);

            Texture2D categoryTexture = ResolveCategoryTexture(part);
            string categoryFallback = categoryTexture == null ? ResolveCategoryFallbackText(part) : string.Empty;
            string[] wrappedCategory = WrapTooltipText(categoryFallback, sectionWidth);
            float categoryHeight = categoryTexture?.Height ?? MeasureLinesHeight(wrappedCategory);
            Texture2D cashLabelTexture = part.IsCash ? _equipTooltipAssets.CashLabel : null;
            float cashLabelHeight = cashLabelTexture?.Height ?? 0f;
            string[] wrappedDescription = WrapTooltipText(description, sectionWidth);
            float descriptionHeight = MeasureLinesHeight(wrappedDescription);
            float topTextHeight = categoryHeight;
            if (cashLabelHeight > 0f)
            {
                topTextHeight += (topTextHeight > 0f ? 2f : 0f) + cashLabelHeight;
            }

            if (descriptionHeight > 0f)
            {
                topTextHeight += (topTextHeight > 0f ? TOOLTIP_SECTION_GAP : 0f) + descriptionHeight;
            }

            float topBlockHeight = Math.Max(SLOT_SIZE, topTextHeight);
            List<TooltipLabeledValueRow> statRows = BuildTooltipStatRows(part);
            List<TooltipLabeledValueRow> requirementRows = BuildTooltipRequirementRows(part, _characterBuild);
            List<Texture2D> jobBadges = BuildTooltipJobBadges(part.RequiredJobMask);
            List<(string[] Lines, Color Color, float Height)> wrappedFooters = BuildWrappedTooltipSections(
                null,
                null,
                null,
                BuildTooltipFooterSections(part, slotLabel, footerSections),
                contentWidth);

            float contentHeight = topBlockHeight;
            float statHeight = MeasureLabeledValueRowsHeight(statRows);
            float requirementHeight = MeasureLabeledValueRowsHeight(requirementRows);
            float jobBadgeHeight = jobBadges.Count > 0 ? 13f : 0f;
            float footerHeight = MeasureWrappedSectionHeight(wrappedFooters);
            if (statHeight > 0f)
            {
                contentHeight += TOOLTIP_TITLE_GAP + statHeight;
            }

            if (requirementHeight > 0f)
            {
                contentHeight += TOOLTIP_TITLE_GAP + requirementHeight;
            }

            if (jobBadgeHeight > 0f)
            {
                contentHeight += TOOLTIP_TITLE_GAP + jobBadgeHeight;
            }

            if (footerHeight > 0f)
            {
                contentHeight += TOOLTIP_TITLE_GAP + footerHeight;
            }

            int tooltipHeight = (int)Math.Ceiling((TOOLTIP_PADDING * 2) + titleHeight + TOOLTIP_TITLE_GAP + contentHeight);
            int tooltipX = _lastMousePosition.X + TOOLTIP_OFFSET_X;
            int tooltipY = _lastMousePosition.Y + 20;
            int tooltipFrameIndex = 1;

            if (tooltipX + tooltipWidth > renderWidth - TOOLTIP_PADDING)
            {
                tooltipX = _lastMousePosition.X - tooltipWidth - TOOLTIP_OFFSET_X;
                tooltipFrameIndex = 0;
            }

            if (tooltipX < TOOLTIP_PADDING)
            {
                tooltipX = TOOLTIP_PADDING;
            }

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

            int textX = tooltipX + textLeftOffset;
            float topY = contentY;
            if (categoryTexture != null)
            {
                sprite.Draw(categoryTexture, new Vector2(textX, topY), Color.White);
                topY += categoryTexture.Height;
            }
            else if (categoryHeight > 0f)
            {
                DrawTooltipLines(sprite, wrappedCategory, textX, topY, new Color(181, 224, 255));
                topY += categoryHeight;
            }

            if (cashLabelHeight > 0f)
            {
                if (topY > contentY)
                {
                    topY += 2f;
                }

                sprite.Draw(cashLabelTexture, new Vector2(textX, topY), Color.White);
                topY += cashLabelHeight;
            }

            if (descriptionHeight > 0f)
            {
                if (topY > contentY)
                {
                    topY += TOOLTIP_SECTION_GAP;
                }

                DrawTooltipLines(sprite, wrappedDescription, textX, topY, new Color(216, 216, 216));
            }

            float sectionY = contentY + topBlockHeight;
            if (statHeight > 0f)
            {
                sectionY += TOOLTIP_TITLE_GAP;
                sectionY = DrawLabeledValueRows(sprite, tooltipX + TOOLTIP_PADDING, sectionY, statRows);
            }

            if (requirementHeight > 0f)
            {
                sectionY += TOOLTIP_TITLE_GAP;
                sectionY = DrawLabeledValueRows(sprite, tooltipX + TOOLTIP_PADDING, sectionY, requirementRows);
            }

            if (jobBadgeHeight > 0f)
            {
                sectionY += TOOLTIP_TITLE_GAP;
                sectionY = DrawJobBadgeRow(sprite, tooltipX + TOOLTIP_PADDING, sectionY, jobBadges);
            }

            if (footerHeight > 0f)
            {
                sectionY += TOOLTIP_TITLE_GAP;
                DrawWrappedSections(sprite, tooltipX + TOOLTIP_PADDING, sectionY, wrappedFooters);
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
            int renderHeight,
            IReadOnlyList<TooltipSection> sections = null)
        {
            int tooltipWidth = ResolveTooltipWidth();
            int textLeftOffset = TOOLTIP_PADDING + SLOT_SIZE + TOOLTIP_ICON_GAP;
            float titleWidth = tooltipWidth - (TOOLTIP_PADDING * 2);
            float sectionWidth = tooltipWidth - textLeftOffset - TOOLTIP_PADDING;
            string[] wrappedTitle = WrapTooltipText(title, titleWidth);
            float titleHeight = MeasureLinesHeight(wrappedTitle);
            List<(string[] Lines, Color Color, float Height)> wrappedSections =
                BuildWrappedTooltipSections(line1, line2, description, sections, sectionWidth);
            float contentHeight = 0f;
            for (int i = 0; i < wrappedSections.Count; i++)
            {
                float sectionHeight = wrappedSections[i].Height;
                if (sectionHeight <= 0f)
                {
                    continue;
                }

                if (contentHeight > 0f)
                {
                    contentHeight += TOOLTIP_SECTION_GAP;
                }

                contentHeight += sectionHeight;
            }

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
            for (int i = 0; i < wrappedSections.Count; i++)
            {
                (string[] lines, Color color, float height) = wrappedSections[i];
                if (height <= 0f)
                {
                    continue;
                }

                if (sectionY > contentY)
                {
                    sectionY += TOOLTIP_SECTION_GAP;
                }

                DrawTooltipLines(sprite, lines, textX, sectionY, color);
                sectionY += height;
            }
        }

        private List<(string[] Lines, Color Color, float Height)> BuildWrappedTooltipSections(
            string line1,
            string line2,
            string description,
            IReadOnlyList<TooltipSection> sections,
            float sectionWidth)
        {
            var wrappedSections = new List<(string[] Lines, Color Color, float Height)>();
            if (sections != null && sections.Count > 0)
            {
                for (int i = 0; i < sections.Count; i++)
                {
                    string[] lines = WrapTooltipText(sections[i].Text, sectionWidth);
                    wrappedSections.Add((lines, sections[i].Color, MeasureLinesHeight(lines)));
                }

                return wrappedSections;
            }

            string[] wrappedLine1 = WrapTooltipText(line1, sectionWidth);
            string[] wrappedLine2 = WrapTooltipText(line2, sectionWidth);
            string[] wrappedDescription = WrapTooltipText(description, sectionWidth);
            wrappedSections.Add((wrappedLine1, new Color(181, 224, 255), MeasureLinesHeight(wrappedLine1)));
            wrappedSections.Add((wrappedLine2, Color.White, MeasureLinesHeight(wrappedLine2)));
            wrappedSections.Add((wrappedDescription, new Color(216, 216, 216), MeasureLinesHeight(wrappedDescription)));
            return wrappedSections;
        }

        private IReadOnlyList<TooltipSection> BuildTooltipFooterSections(
            CharacterPart part,
            string slotLabel,
            IReadOnlyList<TooltipSection> existingSections)
        {
            var sections = new List<TooltipSection>();
            string summaryLine = BuildEquipmentSummaryLine(part);
            if (!string.IsNullOrWhiteSpace(summaryLine))
            {
                sections.Add(new TooltipSection(summaryLine, new Color(181, 224, 255)));
            }

            string requirementLine = BuildEquipmentRequirementLine(part, slotLabel);
            if (!string.IsNullOrWhiteSpace(requirementLine))
            {
                sections.Add(new TooltipSection(requirementLine, Color.White));
            }

            string detailedRequirementLine = BuildDetailedRequirementLine(part);
            if (!string.IsNullOrWhiteSpace(detailedRequirementLine))
            {
                sections.Add(new TooltipSection(detailedRequirementLine, new Color(255, 232, 176)));
            }

            string metadataLine = BuildAdditionalEquipmentMetadataLine(part);
            if (!string.IsNullOrWhiteSpace(metadataLine))
            {
                sections.Add(new TooltipSection(metadataLine, new Color(255, 214, 156)));
            }

            string slotLine = string.IsNullOrWhiteSpace(slotLabel)
                ? $"Item ID: {part.ItemId}"
                : $"Item ID: {part.ItemId}  Slot: {slotLabel}";
            if (part.IsCash)
            {
                slotLine += "  Cash";
            }

            if (part.ExpirationDateUtc.HasValue)
            {
                slotLine += $"  Expires {part.ExpirationDateUtc.Value.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
            }

            sections.Add(new TooltipSection(slotLine, Color.White));

            string eligibilityLine = BuildEquipmentEligibilityLine(part, _characterBuild);
            if (!string.IsNullOrWhiteSpace(eligibilityLine))
            {
                sections.Add(new TooltipSection(
                    eligibilityLine,
                    eligibilityLine.StartsWith("Can equip", StringComparison.Ordinal)
                        ? new Color(176, 255, 176)
                        : new Color(255, 186, 186)));
            }

            if (existingSections != null)
            {
                sections.AddRange(existingSections);
            }

            return sections;
        }

        private static string BuildAdditionalEquipmentMetadataLine(CharacterPart part)
        {
            if (part == null)
            {
                return string.Empty;
            }

            var segments = new List<string>();
            if (part.TradeAvailable > 0)
            {
                segments.Add($"Trade available {part.TradeAvailable} time{(part.TradeAvailable == 1 ? string.Empty : "s")}");
            }

            if (part.KnockbackRate > 0)
            {
                segments.Add($"Knockback resistance {part.KnockbackRate}%");
            }

            if (part.IsTimeLimited)
            {
                segments.Add("Time-limited item");
            }

            return string.Join("  ", segments);
        }

        private List<TooltipLabeledValueRow> BuildTooltipStatRows(CharacterPart part)
        {
            var rows = new List<TooltipLabeledValueRow>();
            AppendStatRow(rows, "STR:", null, part.BonusSTR, new Color(176, 255, 176), includePlusPrefix: true);
            AppendStatRow(rows, "DEX:", null, part.BonusDEX, new Color(176, 255, 176), includePlusPrefix: true);
            AppendStatRow(rows, "INT:", null, part.BonusINT, new Color(176, 255, 176), includePlusPrefix: true);
            AppendStatRow(rows, "LUK:", null, part.BonusLUK, new Color(176, 255, 176), includePlusPrefix: true);
            AppendStatRow(rows, "HP:", null, part.BonusHP, new Color(176, 255, 176), includePlusPrefix: true);
            AppendStatRow(rows, "MP:", null, part.BonusMP, new Color(176, 255, 176), includePlusPrefix: true);
            AppendStatRow(rows, null, ResolvePropertyLabel("6"), part.BonusWeaponAttack, new Color(176, 255, 176), includePlusPrefix: true);
            AppendStatRow(rows, null, ResolvePropertyLabel("7"), part.BonusMagicAttack, new Color(176, 255, 176), includePlusPrefix: true);
            AppendStatRow(rows, null, ResolvePropertyLabel("8"), part.BonusWeaponDefense, new Color(176, 255, 176), includePlusPrefix: true);
            AppendStatRow(rows, null, ResolvePropertyLabel("9"), part.BonusMagicDefense, new Color(176, 255, 176), includePlusPrefix: true);
            AppendStatRow(rows, null, ResolvePropertyLabel("10"), part.BonusAccuracy, new Color(176, 255, 176), includePlusPrefix: true);
            AppendStatRow(rows, null, ResolvePropertyLabel("11"), part.BonusAvoidability, new Color(176, 255, 176), includePlusPrefix: true);
            AppendStatRow(rows, null, ResolvePropertyLabel("12"), part.BonusHands, new Color(176, 255, 176), includePlusPrefix: true);
            AppendStatRow(rows, null, ResolvePropertyLabel("13"), part.BonusSpeed, new Color(176, 255, 176), includePlusPrefix: true);
            AppendStatRow(rows, null, ResolvePropertyLabel("14"), part.BonusJump, new Color(176, 255, 176), includePlusPrefix: true);
            AppendStatRow(rows, null, ResolvePropertyLabel("16"), part.UpgradeSlots, new Color(255, 232, 176), includePlusPrefix: false);
            if (part is WeaponPart weapon)
            {
                AppendAttackSpeedRow(rows, weapon.AttackSpeed);
            }

            return rows;
        }

        private List<TooltipLabeledValueRow> BuildTooltipRequirementRows(CharacterPart part, CharacterBuild build)
        {
            var rows = new List<TooltipLabeledValueRow>();
            AppendRequirementRow(rows, "reqLEV", part.RequiredLevel, build?.Level ?? int.MaxValue);
            AppendRequirementRow(rows, "reqSTR", part.RequiredSTR, build?.TotalSTR ?? int.MaxValue);
            AppendRequirementRow(rows, "reqDEX", part.RequiredDEX, build?.TotalDEX ?? int.MaxValue);
            AppendRequirementRow(rows, "reqINT", part.RequiredINT, build?.TotalINT ?? int.MaxValue);
            AppendRequirementRow(rows, "reqLUK", part.RequiredLUK, build?.TotalLUK ?? int.MaxValue);
            AppendRequirementRow(rows, "reqPOP", part.RequiredFame, build?.Fame ?? int.MaxValue);
            if (part.Durability.HasValue)
            {
                bool canUse = !part.MaxDurability.HasValue || part.Durability.Value > 0;
                string value = part.MaxDurability.HasValue && part.MaxDurability.Value > 0
                    ? $"{Math.Max(0, part.Durability.Value)}/{part.MaxDurability.Value}"
                    : Math.Max(0, part.Durability.Value).ToString(CultureInfo.InvariantCulture);
                rows.Add(new TooltipLabeledValueRow(
                    ResolveRequirementLabel(canUse, "durability"),
                    "Durability:",
                    value,
                    canUse ? new Color(181, 224, 255) : new Color(255, 186, 186),
                    BuildTooltipValueTextures(value, canUse, preferGrowthDigits: false)));
            }

            return rows;
        }

        private void AppendStatRow(
            List<TooltipLabeledValueRow> rows,
            string fallbackLabel,
            Texture2D labelTexture,
            int value,
            Color color,
            bool includePlusPrefix)
        {
            if (value <= 0)
            {
                return;
            }

            string valueText = includePlusPrefix ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);
            rows.Add(new TooltipLabeledValueRow(
                labelTexture,
                fallbackLabel,
                valueText,
                color,
                BuildTooltipValueTextures(valueText, enabled: true, preferGrowthDigits: true)));
        }

        private void AppendAttackSpeedRow(List<TooltipLabeledValueRow> rows, int attackSpeed)
        {
            if (attackSpeed < 0)
            {
                return;
            }

            Texture2D speedTexture = ResolveSpeedTexture(attackSpeed);
            rows.Add(new TooltipLabeledValueRow(
                ResolvePropertyLabel("4"),
                "Attack Speed:",
                ResolveAttackSpeedText(attackSpeed),
                new Color(181, 224, 255),
                speedTexture != null ? new[] { speedTexture } : null));
        }

        private void AppendRequirementRow(List<TooltipLabeledValueRow> rows, string labelKey, int requiredValue, int actualValue)
        {
            if (requiredValue <= 0)
            {
                return;
            }

            bool canUse = actualValue >= requiredValue;
            rows.Add(new TooltipLabeledValueRow(
                ResolveRequirementLabel(canUse, labelKey),
                labelKey + ":",
                requiredValue.ToString(CultureInfo.InvariantCulture),
                canUse ? new Color(181, 224, 255) : new Color(255, 186, 186),
                BuildTooltipValueTextures(requiredValue.ToString(CultureInfo.InvariantCulture), canUse, preferGrowthDigits: false)));
        }

        private IReadOnlyList<TooltipSection> BuildEquipmentTooltipSections(
            CharacterPart part,
            EquipSlot hoveredSlot,
            CharacterBuild build,
            CharacterEquipSlot? characterSlot)
        {
            var sections = new List<TooltipSection>();
            string summaryLine = BuildEquipmentSummaryLine(part);
            if (!string.IsNullOrWhiteSpace(summaryLine))
            {
                sections.Add(new TooltipSection(summaryLine, new Color(181, 224, 255)));
            }

            string requirementLine = BuildEquipmentRequirementLine(part, hoveredSlot);
            if (!string.IsNullOrWhiteSpace(requirementLine))
            {
                sections.Add(new TooltipSection(requirementLine, Color.White));
            }

            string detailedRequirementLine = BuildDetailedRequirementLine(part);
            if (!string.IsNullOrWhiteSpace(detailedRequirementLine))
            {
                sections.Add(new TooltipSection(detailedRequirementLine, new Color(255, 232, 176)));
            }

            string eligibilityLine = BuildEquipmentEligibilityLine(part, build);
            if (!string.IsNullOrWhiteSpace(eligibilityLine))
            {
                Color eligibilityColor = eligibilityLine.StartsWith("Can equip", StringComparison.Ordinal)
                    ? new Color(176, 255, 176)
                    : new Color(255, 186, 186);
                sections.Add(new TooltipSection(eligibilityLine, eligibilityColor));
            }

            CharacterPart hiddenPart = characterSlot.HasValue
                ? EquipSlotStateResolver.ResolveUnderlyingPart(build, characterSlot.Value)
                : null;
            string cashLayerLine = BuildCashLayerLine(part, hiddenPart);
            if (!string.IsNullOrWhiteSpace(cashLayerLine))
            {
                sections.Add(new TooltipSection(cashLayerLine, new Color(255, 214, 156)));
            }

            if (characterSlot.HasValue)
            {
                EquipSlotVisualState visualState = EquipSlotStateResolver.ResolveVisualState(_characterBuild, characterSlot.Value);
                if (!string.IsNullOrWhiteSpace(visualState.Message))
                {
                    sections.Add(new TooltipSection(visualState.Message, new Color(255, 208, 150)));
                }
            }

            return sections;
        }

        private IReadOnlyList<TooltipSection> BuildEquipmentVisualStateSections(CharacterPart part, CharacterEquipSlot? characterSlot)
        {
            var sections = new List<TooltipSection>();
            if (characterSlot.HasValue)
            {
                CharacterPart hiddenPart = EquipSlotStateResolver.ResolveUnderlyingPart(_characterBuild, characterSlot.Value);
                string cashLayerLine = BuildCashLayerLine(part, hiddenPart);
                if (!string.IsNullOrWhiteSpace(cashLayerLine))
                {
                    sections.Add(new TooltipSection(cashLayerLine, new Color(255, 214, 156)));
                }

                EquipSlotVisualState visualState = EquipSlotStateResolver.ResolveVisualState(_characterBuild, characterSlot.Value);
                if (!string.IsNullOrWhiteSpace(visualState.Message))
                {
                    sections.Add(new TooltipSection(visualState.Message, new Color(255, 208, 150)));
                }
            }

            return sections;
        }

        private float MeasureLabeledValueRowsHeight(IReadOnlyList<TooltipLabeledValueRow> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                return 0f;
            }

            float height = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                height += MeasureLabeledValueRowHeight(rows[i]);
                if (i < rows.Count - 1)
                {
                    height += 2f;
                }
            }

            return height;
        }

        private float MeasureLabeledValueRowHeight(TooltipLabeledValueRow row)
        {
            float labelHeight = row.LabelTexture?.Height ?? (_font?.LineSpacing ?? 0);
            float valueHeight = MeasureTooltipValueTexturesHeight(row.ValueTextures);
            return Math.Max(labelHeight, Math.Max(valueHeight, _font?.LineSpacing ?? 0));
        }

        private float DrawLabeledValueRows(SpriteBatch sprite, int x, float y, IReadOnlyList<TooltipLabeledValueRow> rows)
        {
            if (rows == null)
            {
                return y;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                y = DrawLabeledValueRow(sprite, x, y, rows[i]);
                if (i < rows.Count - 1)
                {
                    y += 2f;
                }
            }

            return y;
        }

        private float DrawLabeledValueRow(SpriteBatch sprite, int x, float y, TooltipLabeledValueRow row)
        {
            int valueX = x;
            if (row.LabelTexture != null)
            {
                sprite.Draw(row.LabelTexture, new Vector2(x, y), Color.White);
                valueX = x + row.LabelTexture.Width + 6;
            }
            else if (!string.IsNullOrWhiteSpace(row.FallbackLabel))
            {
                DrawTooltipText(sprite, row.FallbackLabel, new Vector2(x, y), new Color(181, 224, 255));
                valueX = x + (int)Math.Ceiling(_font.MeasureString(row.FallbackLabel).X) + 6;
            }

            if (row.ValueTextures != null && row.ValueTextures.Count > 0)
            {
                DrawTooltipValueTextures(sprite, row.ValueTextures, valueX, y);
            }
            else if (!string.IsNullOrWhiteSpace(row.ValueText))
            {
                DrawTooltipText(sprite, row.ValueText, new Vector2(valueX, y), row.ValueColor);
            }

            return y + MeasureLabeledValueRowHeight(row);
        }

        private List<Texture2D> BuildTooltipJobBadges(int requiredJobMask)
        {
            var textures = new List<Texture2D>(6);
            AppendJobBadgeTexture(textures, requiredJobMask, 1, "beginner");
            AppendJobBadgeTexture(textures, requiredJobMask, 2, "warrior");
            AppendJobBadgeTexture(textures, requiredJobMask, 4, "magician");
            AppendJobBadgeTexture(textures, requiredJobMask, 8, "bowman");
            AppendJobBadgeTexture(textures, requiredJobMask, 16, "thief");
            AppendJobBadgeTexture(textures, requiredJobMask, 32, "pirate");
            return textures;
        }

        private void AppendJobBadgeTexture(List<Texture2D> textures, int requiredJobMask, int maskBit, string key)
        {
            bool canUse = requiredJobMask == 0 || (requiredJobMask & maskBit) != 0;
            Texture2D texture = ResolveRequirementLabel(canUse, key);
            if (texture != null)
            {
                textures.Add(texture);
            }
        }

        private float DrawJobBadgeRow(SpriteBatch sprite, int x, float y, IReadOnlyList<Texture2D> textures)
        {
            int drawX = x;
            for (int i = 0; i < textures.Count; i++)
            {
                Texture2D texture = textures[i];
                if (texture == null)
                {
                    continue;
                }

                sprite.Draw(texture, new Vector2(drawX, y), Color.White);
                drawX += texture.Width + 3;
            }

            return y + 13f;
        }

        private void DrawWrappedSections(
            SpriteBatch sprite,
            int x,
            float y,
            IReadOnlyList<(string[] Lines, Color Color, float Height)> sections)
        {
            float currentY = y;
            for (int i = 0; i < sections.Count; i++)
            {
                (string[] lines, Color color, float height) = sections[i];
                if (height <= 0f)
                {
                    continue;
                }

                if (currentY > y)
                {
                    currentY += TOOLTIP_SECTION_GAP;
                }

                DrawTooltipLines(sprite, lines, x, currentY, color);
                currentY += height;
            }
        }

        private float MeasureWrappedSectionHeight(IReadOnlyList<(string[] Lines, Color Color, float Height)> sections)
        {
            float height = 0f;
            if (sections == null)
            {
                return height;
            }

            for (int i = 0; i < sections.Count; i++)
            {
                float sectionHeight = sections[i].Height;
                if (sectionHeight <= 0f)
                {
                    continue;
                }

                if (height > 0f)
                {
                    height += TOOLTIP_SECTION_GAP;
                }

                height += sectionHeight;
            }

            return height;
        }

        private static IReadOnlyList<TooltipSection> CreateTooltipSections(params (string Text, Color Color)[] sections)
        {
            var result = new List<TooltipSection>();
            if (sections == null)
            {
                return result;
            }

            for (int i = 0; i < sections.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(sections[i].Text))
                {
                    result.Add(new TooltipSection(sections[i].Text, sections[i].Color));
                }
            }

            return result;
        }

        private void DrawDraggedItemOverlay(SpriteBatch sprite)
        {
            if (!_isDraggingItem)
                return;

            IDXObject icon = _draggedPart?.IconRaw
                             ?? _draggedPart?.Icon
                             ?? _draggedCompanionItem?.IconRaw
                             ?? _draggedCompanionItem?.Icon
                             ?? _draggedCompanionItem?.CharacterPart?.IconRaw
                             ?? _draggedCompanionItem?.CharacterPart?.Icon;
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
                return;
            }

            if (_draggedCompanionSlotData?.ItemTexture != null)
            {
                sprite.Draw(
                    _draggedCompanionSlotData.ItemTexture,
                    new Rectangle(
                        _draggedItemPosition.X - (SLOT_SIZE / 2),
                        _draggedItemPosition.Y - (SLOT_SIZE / 2),
                        SLOT_SIZE,
                        SLOT_SIZE),
                    Color.White * 0.85f);
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
            return GetDragonSlotAtPosition(mouseX, mouseY);
        }

        private AndroidEquipSlot? GetHoveredAndroidSlot(int mouseX, int mouseY)
        {
            return GetAndroidSlotAtPosition(mouseX, mouseY);
        }

        private MechanicEquipSlot? GetHoveredMechanicSlot(int mouseX, int mouseY)
        {
            return GetMechanicSlotAtPosition(mouseX, mouseY);
        }

        private DragonEquipSlot? GetDragonSlotAtPosition(int mouseX, int mouseY)
        {
            if (_currentTab != TAB_DRAGON || !_companionLayouts.TryGetValue(TAB_DRAGON, out CompanionPaneLayout layout))
            {
                return null;
            }

            Point panePosition = GetCompanionPanePosition(layout, Position.X, Position.Y);
            foreach ((DragonEquipSlot slot, Point slotPosition) in _dragonSlotPositions)
            {
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

        private AndroidEquipSlot? GetAndroidSlotAtPosition(int mouseX, int mouseY)
        {
            if (_currentTab != TAB_ANDROID || !_companionLayouts.TryGetValue(TAB_ANDROID, out CompanionPaneLayout layout))
            {
                return null;
            }

            Point panePosition = GetCompanionPanePosition(layout, Position.X, Position.Y);
            foreach ((AndroidEquipSlot slot, Point slotPosition) in _androidSlotPositions)
            {
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

        private MechanicEquipSlot? GetMechanicSlotAtPosition(int mouseX, int mouseY)
        {
            if (_currentTab != TAB_MECHANIC || !_companionLayouts.TryGetValue(TAB_MECHANIC, out CompanionPaneLayout layout))
            {
                return null;
            }

            Point panePosition = GetCompanionPanePosition(layout, Position.X, Position.Y);
            foreach ((MechanicEquipSlot slot, Point slotPosition) in _mechanicSlotPositions)
            {
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

        private IDXObject ResolvePetSlotIcon(PetRuntime pet)
        {
            if (pet != null
                && _petEquipmentController != null
                && _petEquipmentController.TryGetItem(pet, out CompanionEquipItem item))
            {
                return item.IconRaw ?? item.Icon ?? item.CharacterPart?.IconRaw ?? item.CharacterPart?.Icon;
            }

            return pet?.Definition?.IconRaw ?? pet?.Definition?.Icon;
        }

        private PetRuntime ResolvePetByIndex(int petIndex)
        {
            IReadOnlyList<PetRuntime> pets = _petController?.ActivePets;
            return pets != null && petIndex >= 0 && petIndex < pets.Count ? pets[petIndex] : null;
        }

        private PetRuntime ResolveDraggedPet()
        {
            IReadOnlyList<PetRuntime> pets = _petController?.ActivePets;
            if (pets == null)
            {
                return null;
            }

            for (int i = 0; i < pets.Count; i++)
            {
                PetRuntime pet = pets[i];
                if (pet != null && pet.RuntimeId == _draggedPetRuntimeId)
                {
                    return pet;
                }
            }

            return null;
        }

        private bool TryBeginDraggedCompanion(int mouseX, int mouseY)
        {
            switch (_currentTab)
            {
                case TAB_PET:
                {
                    int? petIndex = GetHoveredPetIndex(mouseX, mouseY);
                    PetRuntime pet = petIndex.HasValue ? ResolvePetByIndex(petIndex.Value) : null;
                    if (pet == null
                        || _petEquipmentController == null
                        || !_petEquipmentController.TryGetItem(pet, out CompanionEquipItem item))
                    {
                        return false;
                    }

                    BeginCompanionDrag(CreateInventorySlot(item), item, CompanionDragKind.Pet);
                    _draggedPetRuntimeId = pet.RuntimeId;
                    return true;
                }
                case TAB_DRAGON:
                {
                    DragonEquipSlot? slot = GetDragonSlotAtPosition(mouseX, mouseY);
                    if (!slot.HasValue
                        || _dragonEquipmentController == null
                        || !_dragonEquipmentController.TryGetItem(slot.Value, out CompanionEquipItem item))
                    {
                        return false;
                    }

                    BeginCompanionDrag(CreateInventorySlot(item), item, CompanionDragKind.Dragon);
                    _draggedDragonSlot = slot;
                    return true;
                }
                case TAB_MECHANIC:
                {
                    MechanicEquipSlot? slot = GetMechanicSlotAtPosition(mouseX, mouseY);
                    if (!slot.HasValue
                        || _mechanicEquipmentController == null
                        || !_mechanicEquipmentController.TryGetItem(slot.Value, out CompanionEquipItem item))
                    {
                        return false;
                    }

                    BeginCompanionDrag(CreateInventorySlot(item), item, CompanionDragKind.Mechanic);
                    _draggedMechanicSlot = slot;
                    return true;
                }
                case TAB_ANDROID:
                {
                    AndroidEquipSlot? slot = GetAndroidSlotAtPosition(mouseX, mouseY);
                    if (!slot.HasValue
                        || _androidEquipmentController == null
                        || !_androidEquipmentController.TryGetItem(slot.Value, out CompanionEquipItem item))
                    {
                        return false;
                    }

                    BeginCompanionDrag(CreateInventorySlot(item), item, CompanionDragKind.Android);
                    _draggedAndroidSlot = slot;
                    return true;
                }
                default:
                    return false;
            }
        }

        private void BeginCompanionDrag(InventorySlotData slotData, CompanionEquipItem item, CompanionDragKind kind)
        {
            if (slotData == null || item == null)
            {
                return;
            }

            _isDraggingItem = true;
            _draggedSlot = null;
            _draggedPart = null;
            _draggedItemPosition = _lastMousePosition;
            _draggedCompanionKind = kind;
            _draggedCompanionSlotData = slotData;
            _draggedCompanionItem = item;
            _draggedPetRuntimeId = 0;
            _draggedDragonSlot = null;
            _draggedMechanicSlot = null;
            _draggedAndroidSlot = null;
        }

        private bool TryHandleDraggedCompanionDrop(int mouseX, int mouseY)
        {
            if (!HasDraggedCompanionItem)
            {
                return false;
            }

            if (_draggedCompanionKind != CompanionDragKind.Pet)
            {
                return false;
            }

            int? targetIndex = GetHoveredPetIndex(mouseX, mouseY);
            if (!targetIndex.HasValue || _petEquipmentController == null)
            {
                return false;
            }

            PetRuntime sourcePet = ResolveDraggedPet();
            PetRuntime targetPet = ResolvePetByIndex(targetIndex.Value);
            string rejectReason = null;
            bool moved = sourcePet != null
                         && targetPet != null
                         && _petEquipmentController.TryMoveItem(sourcePet, targetPet, out rejectReason);
            if (!moved && !string.IsNullOrWhiteSpace(rejectReason))
            {
                EquipmentEquipBlocked?.Invoke(rejectReason);
            }

            return moved;
        }

        private bool TryUnequipDraggedPetItem()
        {
            PetRuntime pet = ResolveDraggedPet();
            return pet != null && _petEquipmentController != null && _petEquipmentController.TryUnequipItem(pet, out _);
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
            if (targetState.IsDisabled)
            {
                NotifyEquipmentEquipBlocked(targetState.Message);
                return false;
            }

            if (!CanDisplayPartInSlot(_draggedPart, targetUiSlot.Value))
            {
                NotifyEquipmentEquipBlocked(BuildSlotMismatchRejectReason(_draggedPart));
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

        private void NotifyEquipmentEquipBlocked(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                EquipmentEquipBlocked?.Invoke(message);
            }
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

        private static bool TryGetEquipRequirementRejectReason(CharacterPart part, CharacterBuild build, out string rejectReason)
        {
            rejectReason = null;
            if (part == null || build == null)
            {
                return true;
            }

            if (part.RequiredLevel > 0 && build.Level < part.RequiredLevel)
            {
                rejectReason = $"Requires level {part.RequiredLevel}.";
                return false;
            }

            if (part.RequiredSTR > 0 && build.TotalSTR < part.RequiredSTR)
            {
                rejectReason = $"Requires {part.RequiredSTR} STR.";
                return false;
            }

            if (part.RequiredDEX > 0 && build.TotalDEX < part.RequiredDEX)
            {
                rejectReason = $"Requires {part.RequiredDEX} DEX.";
                return false;
            }

            if (part.RequiredINT > 0 && build.TotalINT < part.RequiredINT)
            {
                rejectReason = $"Requires {part.RequiredINT} INT.";
                return false;
            }

            if (part.RequiredLUK > 0 && build.TotalLUK < part.RequiredLUK)
            {
                rejectReason = $"Requires {part.RequiredLUK} LUK.";
                return false;
            }

            if (part.RequiredFame > 0 && build.Fame < part.RequiredFame)
            {
                rejectReason = $"Requires {part.RequiredFame} Fame.";
                return false;
            }

            if (part.RequiredJobMask != 0 && !MatchesRequiredJobMask(part.RequiredJobMask, build.Job))
            {
                rejectReason = $"Requires {ResolveRequiredJobNames(part.RequiredJobMask)}.";
                return false;
            }

            return true;
        }

        private static string BuildSlotMismatchRejectReason(CharacterPart part)
        {
            string slotLabel = part == null ? null : ResolvePreferredSlotLabel(part.Slot);
            return string.IsNullOrWhiteSpace(slotLabel)
                ? "Drop this item on the matching equipment slot."
                : $"Drop this item on the {slotLabel} slot.";
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

        private static string ResolvePreferredSlotLabel(CharacterEquipSlot slot)
        {
            return slot switch
            {
                CharacterEquipSlot.Ring1 or CharacterEquipSlot.Ring2 or CharacterEquipSlot.Ring3 or CharacterEquipSlot.Ring4 => "Ring",
                CharacterEquipSlot.Pendant or CharacterEquipSlot.Pendant2 => "Pendant",
                CharacterEquipSlot.Coat or CharacterEquipSlot.Longcoat => "Top",
                CharacterEquipSlot.Pants => "Bottom",
                CharacterEquipSlot.FaceAccessory => "Face Accessory",
                CharacterEquipSlot.EyeAccessory => "Eye Accessory",
                CharacterEquipSlot.Earrings => "Earring",
                CharacterEquipSlot.AndroidHeart => "Heart",
                CharacterEquipSlot.TamingMob => "Monster Riding",
                CharacterEquipSlot.Saddle => "Saddle",
                _ => Enum.GetName(typeof(CharacterEquipSlot), slot)
            };
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
                EquipSlot.Pocket => CharacterEquipSlot.Pocket,
                EquipSlot.Weapon => CharacterEquipSlot.Weapon,
                EquipSlot.Belt => CharacterEquipSlot.Belt,
                EquipSlot.Badge => CharacterEquipSlot.Badge,
                EquipSlot.Cap => CharacterEquipSlot.Cap,
                EquipSlot.FaceAccessory => CharacterEquipSlot.FaceAccessory,
                EquipSlot.EyeAccessory => CharacterEquipSlot.EyeAccessory,
                EquipSlot.Top => CharacterEquipSlot.Coat,
                EquipSlot.Bottom => CharacterEquipSlot.Pants,
                EquipSlot.Shoes => CharacterEquipSlot.Shoes,
                EquipSlot.Earring => CharacterEquipSlot.Earrings,
                EquipSlot.Shoulder => CharacterEquipSlot.Shoulder,
                EquipSlot.Glove => CharacterEquipSlot.Glove,
                EquipSlot.Shield => CharacterEquipSlot.Shield,
                EquipSlot.Cape => CharacterEquipSlot.Cape,
                EquipSlot.Medal => CharacterEquipSlot.Medal,
                EquipSlot.Android => CharacterEquipSlot.Android,
                EquipSlot.Heart => CharacterEquipSlot.AndroidHeart,
                EquipSlot.AndroidHeart => CharacterEquipSlot.AndroidHeart,
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
                EquipSlot.Pendant2 => "Pendant 2",
                EquipSlot.Pocket => "Pocket",
                EquipSlot.Badge => "Badge",
                EquipSlot.Shoulder => "Shoulder",
                EquipSlot.Heart => "Heart",
                EquipSlot.AndroidHeart => "Heart",
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

        private CharacterPart CreateInventoryEquipmentPart(InventorySlotData slotData)
        {
            if (slotData == null)
            {
                return null;
            }

            CharacterEquipSlot slot = ResolveFallbackEquipSlot(slotData.ItemId);
            if (slot == CharacterEquipSlot.None)
            {
                return null;
            }

            return new CharacterPart
            {
                ItemId = slotData.ItemId,
                Name = string.IsNullOrWhiteSpace(slotData.ItemName) ? $"Equip {slotData.ItemId}" : slotData.ItemName,
                Description = slotData.Description,
                ItemCategory = slotData.ItemTypeName,
                Slot = slot,
                Type = HaCreator.MapSimulator.Character.CharacterPartType.Accessory
            };
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
                ItemTexture = LoadInventoryItemIcon(part.ItemId),
                Quantity = 1,
                MaxStackSize = 1,
                GradeFrameIndex = 0,
                ItemName = string.IsNullOrWhiteSpace(part.Name) ? $"Equip {part.ItemId}" : part.Name,
                ItemTypeName = string.IsNullOrWhiteSpace(part.ItemCategory) ? "Equip" : part.ItemCategory,
                Description = BuildEquipmentDescription(part)
            };
        }

        private Texture2D LoadInventoryItemIcon(int itemId)
        {
            if (!InventoryItemMetadataResolver.TryResolveImageSource(itemId, out string category, out string imagePath))
            {
                return null;
            }

            WzImage itemImage = global::HaCreator.Program.FindImage(category, imagePath);
            if (itemImage == null)
            {
                return null;
            }

            itemImage.ParseImage();
            string itemText = category == "Character" ? itemId.ToString("D8") : itemId.ToString("D7");
            WzSubProperty itemProperty = itemImage[itemText] as WzSubProperty;
            WzSubProperty infoProperty = itemProperty?["info"] as WzSubProperty;
            WzCanvasProperty iconCanvas = infoProperty?["iconRaw"] as WzCanvasProperty
                                          ?? infoProperty?["icon"] as WzCanvasProperty;
            return iconCanvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(_device);
        }

        private static CharacterEquipSlot ResolveFallbackEquipSlot(int itemId)
        {
            int category = itemId / 10000;
            return category switch
            {
                100 => CharacterEquipSlot.Cap,
                101 => CharacterEquipSlot.FaceAccessory,
                102 => CharacterEquipSlot.EyeAccessory,
                103 => CharacterEquipSlot.Earrings,
                104 => CharacterEquipSlot.Coat,
                105 => CharacterEquipSlot.Longcoat,
                106 => CharacterEquipSlot.Pants,
                107 => CharacterEquipSlot.Shoes,
                108 => CharacterEquipSlot.Glove,
                109 => CharacterEquipSlot.Shield,
                110 => CharacterEquipSlot.Cape,
                111 => CharacterEquipSlot.Ring1,
                112 => CharacterEquipSlot.Pendant,
                113 => CharacterEquipSlot.Belt,
                114 => CharacterEquipSlot.Medal,
                115 => CharacterEquipSlot.Shoulder,
                116 => CharacterEquipSlot.Pocket,
                118 => CharacterEquipSlot.Badge,
                166 => CharacterEquipSlot.Android,
                167 => CharacterEquipSlot.AndroidHeart,
                >= 130 and < 170 => CharacterEquipSlot.Weapon,
                180 => CharacterEquipSlot.TamingMob,
                >= 190 and < 200 => CharacterEquipSlot.TamingMob,
                _ => CharacterEquipSlot.None
            };
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

            return slots.Count == 0
                ? Array.Empty<InventorySlotData>()
                : new ReadOnlyCollection<InventorySlotData>(slots);
        }

        private static IReadOnlyList<InventorySlotData> CreateInventorySlots(IReadOnlyList<CompanionEquipItem> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<InventorySlotData>();
            }

            List<InventorySlotData> slots = new(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                CompanionEquipItem item = items[i];
                if (item == null)
                {
                    continue;
                }

                slots.Add(new InventorySlotData
                {
                    ItemId = item.ItemId,
                    ItemTexture = item.ItemTexture,
                    Quantity = 1,
                    MaxStackSize = 1,
                    GradeFrameIndex = 0,
                    ItemName = item.Name,
                    ItemTypeName = string.IsNullOrWhiteSpace(item.ItemCategory) ? "Equip" : item.ItemCategory,
                    Description = BuildCompanionEquipmentDescription(item)
                });
            }

            return slots.Count == 0
                ? Array.Empty<InventorySlotData>()
                : new ReadOnlyCollection<InventorySlotData>(slots);
        }

        private static InventorySlotData CreateInventorySlot(CompanionEquipItem item)
        {
            if (item == null)
            {
                return null;
            }

            return new InventorySlotData
            {
                ItemId = item.ItemId,
                ItemTexture = item.ItemTexture,
                Quantity = 1,
                MaxStackSize = 1,
                GradeFrameIndex = 0,
                ItemName = item.Name,
                ItemTypeName = string.IsNullOrWhiteSpace(item.ItemCategory) ? "Equip" : item.ItemCategory,
                Description = BuildCompanionEquipmentDescription(item)
            };
        }

        private static bool MeetsEquipRequirements(CharacterPart part, CharacterBuild build)
        {
            if (part == null || build == null)
            {
                return true;
            }

            return (part.RequiredLevel <= 0 || build.Level >= part.RequiredLevel)
                   && (part.RequiredSTR <= 0 || build.TotalSTR >= part.RequiredSTR)
                   && (part.RequiredDEX <= 0 || build.TotalDEX >= part.RequiredDEX)
                   && (part.RequiredINT <= 0 || build.TotalINT >= part.RequiredINT)
                   && (part.RequiredLUK <= 0 || build.TotalLUK >= part.RequiredLUK)
                   && (part.RequiredFame <= 0 || build.Fame >= part.RequiredFame)
                   && (part.RequiredJobMask == 0 || MatchesRequiredJobMask(part.RequiredJobMask, build.Job));
        }

        private static bool MatchesRequiredJobMask(int requiredJobMask, int jobId)
        {
            if (requiredJobMask == 0)
            {
                return true;
            }

            int jobGroup = Math.Abs(jobId) / 100;
            return jobGroup switch
            {
                0 => (requiredJobMask & 1) != 0,
                1 => (requiredJobMask & 2) != 0,
                2 => (requiredJobMask & 4) != 0,
                3 => (requiredJobMask & 8) != 0,
                4 => (requiredJobMask & 16) != 0,
                5 => (requiredJobMask & 32) != 0,
                _ => false
            };
        }

        private static string ResolveRequiredJobNames(int requiredJobMask)
        {
            if (requiredJobMask == 0)
            {
                return string.Empty;
            }

            var jobNames = new List<string>();
            AppendRequiredJobName(jobNames, requiredJobMask, 1, "Beginner");
            AppendRequiredJobName(jobNames, requiredJobMask, 2, "Warrior");
            AppendRequiredJobName(jobNames, requiredJobMask, 4, "Magician");
            AppendRequiredJobName(jobNames, requiredJobMask, 8, "Bowman");
            AppendRequiredJobName(jobNames, requiredJobMask, 16, "Thief");
            AppendRequiredJobName(jobNames, requiredJobMask, 32, "Pirate");
            return string.Join("/", jobNames);
        }

        private static void AppendRequiredJobName(List<string> jobNames, int requiredJobMask, int maskBit, string jobName)
        {
            if ((requiredJobMask & maskBit) != 0)
            {
                jobNames.Add(jobName);
            }
        }

        private Texture2D ResolveRequirementLabel(bool canUse, string key)
        {
            if (_equipTooltipAssets == null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            IReadOnlyDictionary<string, Texture2D> source = canUse
                ? _equipTooltipAssets.CanLabels
                : _equipTooltipAssets.CannotLabels;
            return TryResolveTooltipAsset(source, key);
        }

        private Texture2D ResolvePropertyLabel(string key)
        {
            return TryResolveTooltipAsset(_equipTooltipAssets?.PropertyLabels, key);
        }

        private Texture2D ResolveSpeedTexture(int attackSpeed)
        {
            return TryResolveTooltipAsset(_equipTooltipAssets?.SpeedLabels, Math.Clamp(attackSpeed, 0, 6).ToString(CultureInfo.InvariantCulture));
        }

        private IReadOnlyList<Texture2D> BuildTooltipValueTextures(string valueText, bool enabled, bool preferGrowthDigits)
        {
            if (string.IsNullOrWhiteSpace(valueText) || _equipTooltipAssets == null)
            {
                return null;
            }

            IReadOnlyDictionary<string, Texture2D> source = preferGrowthDigits
                ? (enabled ? _equipTooltipAssets.GrowthEnabledLabels : _equipTooltipAssets.GrowthDisabledLabels)
                : (enabled ? _equipTooltipAssets.CanLabels : _equipTooltipAssets.CannotLabels);
            if (source == null)
            {
                return null;
            }

            var textures = new List<Texture2D>(valueText.Length);
            for (int i = 0; i < valueText.Length; i++)
            {
                char character = valueText[i];
                if (character == '+')
                {
                    continue;
                }

                string key = character switch
                {
                    '%' => "percent",
                    _ => char.IsDigit(character) ? character.ToString() : null
                };
                Texture2D texture = TryResolveTooltipAsset(source, key);
                if (texture == null)
                {
                    return null;
                }

                textures.Add(texture);
            }

            return textures.Count == 0 ? null : textures;
        }

        private float MeasureTooltipValueTexturesHeight(IReadOnlyList<Texture2D> textures)
        {
            if (textures == null || textures.Count == 0)
            {
                return 0f;
            }

            int height = 0;
            for (int i = 0; i < textures.Count; i++)
            {
                if (textures[i] != null)
                {
                    height = Math.Max(height, textures[i].Height);
                }
            }

            return height;
        }

        private void DrawTooltipValueTextures(SpriteBatch sprite, IReadOnlyList<Texture2D> textures, int x, float y)
        {
            if (textures == null || textures.Count == 0)
            {
                return;
            }

            int drawX = x;
            for (int i = 0; i < textures.Count; i++)
            {
                Texture2D texture = textures[i];
                if (texture == null)
                {
                    continue;
                }

                sprite.Draw(texture, new Vector2(drawX, y), Color.White);
                drawX += texture.Width + TOOLTIP_BITMAP_GAP;
            }
        }

        private Texture2D ResolveCategoryTexture(CharacterPart part)
        {
            if (_equipTooltipAssets == null || part == null || part.ItemId <= 0)
            {
                return null;
            }

            int itemCategory = part.ItemId / 10000;
            if (part is WeaponPart)
            {
                Texture2D weaponTexture = TryResolveTooltipAsset(
                    _equipTooltipAssets.WeaponCategoryLabels,
                    (itemCategory - 100).ToString(CultureInfo.InvariantCulture));
                if (weaponTexture != null)
                {
                    return weaponTexture;
                }
            }

            string categoryKey = itemCategory switch
            {
                100 => "1",
                101 => "2",
                102 => "3",
                103 => "4",
                104 => "5",
                105 => "21",
                106 => "6",
                107 => "7",
                108 => "8",
                109 => "10",
                110 => "9",
                111 => "12",
                _ => null
            };

            return categoryKey == null
                ? null
                : TryResolveTooltipAsset(_equipTooltipAssets.ItemCategoryLabels, categoryKey);
        }

        private static Texture2D TryResolveTooltipAsset(IReadOnlyDictionary<string, Texture2D> assets, string key)
        {
            if (assets == null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            return assets.TryGetValue(key, out Texture2D texture) ? texture : null;
        }

        private static string ResolveCategoryFallbackText(CharacterPart part)
        {
            if (part is WeaponPart weapon && !string.IsNullOrWhiteSpace(weapon.WeaponType))
            {
                return weapon.WeaponType;
            }

            return part?.ItemCategory ?? string.Empty;
        }

        private static string ResolveAttackSpeedText(int attackSpeed)
        {
            return Math.Clamp(attackSpeed, 0, 6) switch
            {
                0 => "Fastest",
                1 => "Faster",
                2 => "Fast",
                3 => "Normal",
                4 => "Slow",
                5 => "Slower",
                6 => "Slowest",
                _ => string.Empty
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
