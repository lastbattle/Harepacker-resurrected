using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.UI.Controls;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// Inventory UI window for post-Big Bang MapleStory (v100+)
    /// Structure: UI.wz/UIWindow2.img/Item
    /// Window dimensions: 172x293 pixels
    /// </summary>
    public class InventoryUIBigBang : UIWindowBase
    {
        #region Constants
        private const int SLOT_SIZE = 32;
        private const int SLOT_PADDING = 4;
        private const int SLOTS_PER_ROW = 4;
        private const int VISIBLE_ROWS = 6;
        private const int TOTAL_SLOTS = 24;

        // Small window dimensions (from UIWindow2.img/Item/backgrnd)
        private const int WINDOW_WIDTH_SMALL = 172;
        private const int WINDOW_HEIGHT = 293;

        // Expanded window dimensions (from UIWindow2.img/Item/FullBackgrnd)
        private const int WINDOW_WIDTH_EXPANDED = 594;
        // Height stays the same

        // Expanded mode: 4 columns per tab section, 5 tabs visible
        private const int EXPANDED_SLOTS_PER_ROW = 4;
        private const int EXPANDED_TAB_SECTIONS = 5;
        private const int EXPANDED_SECTION_WIDTH = 118; // Approximate width per tab section

        // Button positions from WZ origin (-147, -267) = (147, 267)
        private const int BTN_FULL_SMALL_X = 147;
        private const int BTN_FULL_SMALL_Y = 267;

        // Inventory tab indices
        private const int TAB_EQUIP = 0;
        private const int TAB_USE = 1;
        private const int TAB_SETUP = 2;
        private const int TAB_ETC = 3;
        private const int TAB_CASH = 4;
        #endregion

        #region Fields
        private int _currentTab = TAB_EQUIP;
        private int _scrollOffset = 0;
        private bool _isExpanded = false;

        // Small view textures
        private IDXObject _frameSmall;
        private IDXObject _foregroundSmall;
        private Point _foregroundSmallOffset;

        // Expanded view textures
        private IDXObject _frameExpanded;
        private IDXObject _foregroundExpanded;
        private Point _foregroundExpandedOffset;

        // Legacy compatibility - points to current foreground
        private IDXObject _foreground;
        private Point _foregroundOffset;

        // Tab buttons
        private UIObject _tabEquip;
        private UIObject _tabUse;
        private UIObject _tabSetup;
        private UIObject _tabEtc;
        private UIObject _tabCash;

        // Additional Big Bang buttons
        private UIObject _btnGather;
        private UIObject _btnSort;
        private UIObject _btnFull;
        private UIObject _btnSmall;

        // Item data
        private Dictionary<InventoryType, List<InventorySlotData>> inventoryData;

        // Meso display
        private long mesoCount = 0;

        // Graphics device
        private GraphicsDevice _device;
        #endregion

        #region Properties
        public override string WindowName => "Inventory";

        /// <summary>
        /// Whether the inventory is in expanded (full) view mode
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    UpdateViewMode();
                }
            }
        }

        /// <summary>
        /// Current window width based on view mode
        /// </summary>
        public int CurrentWidth => _isExpanded ? WINDOW_WIDTH_EXPANDED : WINDOW_WIDTH_SMALL;

        public int CurrentTab
        {
            get => _currentTab;
            set
            {
                if (value >= TAB_EQUIP && value <= TAB_CASH)
                {
                    _currentTab = value;
                    _scrollOffset = 0;
                }
            }
        }

        public long MesoCount
        {
            get => mesoCount;
            set => mesoCount = Math.Max(0, value);
        }
        #endregion

        #region Constructor
        public InventoryUIBigBang(IDXObject frame, GraphicsDevice device)
            : base(frame)
        {
            _device = device;
            _frameSmall = frame; // Store the small frame

            // Initialize empty inventory data
            inventoryData = new Dictionary<InventoryType, List<InventorySlotData>>
            {
                { InventoryType.EQUIP, new List<InventorySlotData>() },
                { InventoryType.USE, new List<InventorySlotData>() },
                { InventoryType.SETUP, new List<InventorySlotData>() },
                { InventoryType.ETC, new List<InventorySlotData>() },
                { InventoryType.CASH, new List<InventorySlotData>() }
            };
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Set the foreground texture for small view (backgrnd2 - labels/overlay from UIWindow2.img/Item)
        /// </summary>
        public void SetForeground(IDXObject foreground, int offsetX, int offsetY)
        {
            _foregroundSmall = foreground;
            _foregroundSmallOffset = new Point(offsetX, offsetY);

            // Set as current if not expanded
            if (!_isExpanded)
            {
                _foreground = foreground;
                _foregroundOffset = new Point(offsetX, offsetY);
            }
        }

        /// <summary>
        /// Set the expanded view textures (FullBackgrnd, FullBackgrnd2 from UIWindow2.img/Item)
        /// </summary>
        public void SetExpandedView(IDXObject expandedFrame, IDXObject expandedForeground, int fgOffsetX, int fgOffsetY)
        {
            _frameExpanded = expandedFrame;
            _foregroundExpanded = expandedForeground;
            _foregroundExpandedOffset = new Point(fgOffsetX, fgOffsetY);
        }

        /// <summary>
        /// Update the view mode (switch between small and expanded)
        /// </summary>
        private void UpdateViewMode()
        {
            // Calculate button position offset for expanded view
            // Small: 172 wide, Expanded: 594 wide, difference = 422
            int expandedOffsetX = WINDOW_WIDTH_EXPANDED - WINDOW_WIDTH_SMALL;

            if (_isExpanded)
            {
                // Switch to expanded view
                if (_frameExpanded != null)
                {
                    this.Frame = _frameExpanded;
                }
                _foreground = _foregroundExpanded;
                _foregroundOffset = _foregroundExpandedOffset;

                // Reposition buttons for expanded view
                // Close button moves to right side of expanded window
                if (closeButton != null)
                {
                    closeButton.X = 150 + expandedOffsetX; // 150 + 422 = 572
                }

                // BtSmall button position for expanded view
                if (_btnSmall != null)
                {
                    _btnSmall.X = BTN_FULL_SMALL_X + expandedOffsetX;
                }

                // Hide BtFull, show BtSmall
                _btnFull?.SetVisible(false);
                _btnSmall?.SetVisible(true);
            }
            else
            {
                // Switch to small view
                if (_frameSmall != null)
                {
                    this.Frame = _frameSmall;
                }
                _foreground = _foregroundSmall;
                _foregroundOffset = _foregroundSmallOffset;

                // Restore button positions for small view
                if (closeButton != null)
                {
                    closeButton.X = 150;
                }
                if (_btnFull != null)
                {
                    _btnFull.X = BTN_FULL_SMALL_X;
                }

                // Show BtFull, hide BtSmall
                _btnFull?.SetVisible(true);
                _btnSmall?.SetVisible(false);
            }
        }

        /// <summary>
        /// Initialize tab buttons
        /// </summary>
        public void InitializeTabs(UIObject equipTab, UIObject useTab, UIObject setupTab, UIObject etcTab, UIObject cashTab)
        {
            this._tabEquip = equipTab;
            this._tabUse = useTab;
            this._tabSetup = setupTab;
            this._tabEtc = etcTab;
            this._tabCash = cashTab;

            if (equipTab != null)
            {
                AddButton(equipTab);
                equipTab.ButtonClickReleased += (sender) => CurrentTab = TAB_EQUIP;
            }
            if (useTab != null)
            {
                AddButton(useTab);
                useTab.ButtonClickReleased += (sender) => CurrentTab = TAB_USE;
            }
            if (setupTab != null)
            {
                AddButton(setupTab);
                setupTab.ButtonClickReleased += (sender) => CurrentTab = TAB_SETUP;
            }
            if (etcTab != null)
            {
                AddButton(etcTab);
                etcTab.ButtonClickReleased += (sender) => CurrentTab = TAB_ETC;
            }
            if (cashTab != null)
            {
                AddButton(cashTab);
                cashTab.ButtonClickReleased += (sender) => CurrentTab = TAB_CASH;
            }

            UpdateTabStates();
        }

        /// <summary>
        /// Initialize Big Bang specific buttons (Gather, Sort, Full/Small view)
        /// </summary>
        public void InitializeBigBangButtons(UIObject btnGather, UIObject btnSort, UIObject btnFull, UIObject btnSmall)
        {
            _btnGather = btnGather;
            _btnSort = btnSort;
            _btnFull = btnFull;
            _btnSmall = btnSmall;

            if (btnGather != null) AddButton(btnGather);
            if (btnSort != null) AddButton(btnSort);

            // Full button - switches to expanded view
            if (btnFull != null)
            {
                btnFull.X = BTN_FULL_SMALL_X;
                btnFull.Y = BTN_FULL_SMALL_Y;
                AddButton(btnFull);
                btnFull.ButtonClickReleased += (sender) => IsExpanded = true;
            }

            // Small button - switches to small view (initially hidden)
            if (btnSmall != null)
            {
                btnSmall.X = BTN_FULL_SMALL_X;
                btnSmall.Y = BTN_FULL_SMALL_Y;
                btnSmall.SetVisible(false); // Hidden by default (small view is default)
                AddButton(btnSmall);
                btnSmall.ButtonClickReleased += (sender) => IsExpanded = false;
            }
        }

        private void UpdateTabStates()
        {
            _tabEquip?.SetButtonState(_currentTab == TAB_EQUIP ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabUse?.SetButtonState(_currentTab == TAB_USE ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabSetup?.SetButtonState(_currentTab == TAB_SETUP ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabEtc?.SetButtonState(_currentTab == TAB_ETC ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabCash?.SetButtonState(_currentTab == TAB_CASH ? UIObjectState.Pressed : UIObjectState.Normal);
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

            // Draw foreground (backgrnd2 - labels/overlay)
            if (_foreground != null)
            {
                _foreground.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    windowX + _foregroundOffset.X, windowY + _foregroundOffset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            // Item icons would be drawn here when items are added
        }
        #endregion

        #region Inventory Management
        private InventoryType GetInventoryTypeFromTab(int tab)
        {
            return tab switch
            {
                TAB_EQUIP => InventoryType.EQUIP,
                TAB_USE => InventoryType.USE,
                TAB_SETUP => InventoryType.SETUP,
                TAB_ETC => InventoryType.ETC,
                TAB_CASH => InventoryType.CASH,
                _ => InventoryType.NONE
            };
        }

        public void AddItem(InventoryType type, int itemId, Texture2D texture, int quantity = 1)
        {
            if (inventoryData.TryGetValue(type, out var slots))
            {
                slots.Add(new InventorySlotData
                {
                    ItemId = itemId,
                    ItemTexture = texture,
                    Quantity = quantity
                });
            }
        }

        public void ClearInventory()
        {
            foreach (var kvp in inventoryData)
            {
                kvp.Value.Clear();
            }
        }

        public void ScrollUp()
        {
            if (_scrollOffset > 0)
                _scrollOffset--;
        }

        public void ScrollDown()
        {
            InventoryType invType = GetInventoryTypeFromTab(_currentTab);
            if (inventoryData.TryGetValue(invType, out var slots))
            {
                int maxScroll = Math.Max(0, (slots.Count / SLOTS_PER_ROW) - VISIBLE_ROWS + 1);
                if (_scrollOffset < maxScroll)
                    _scrollOffset++;
            }
        }
        #endregion

        #region Update
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            UpdateTabStates();
        }
        #endregion
    }
}
