using HaCreator.MapSimulator.UI.Controls;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// Inventory UI window for post-Big Bang MapleStory (v100+).
    /// Structure: UI.wz/UIWindow2.img/Item
    /// </summary>
    public class InventoryUIBigBang : InventoryUI
    {
        #region Constants
        private const int WindowWidthSmall = 172;
        private const int WindowWidthExpanded = 594;
        private const int BtnFullSmallX = 147;
        private const int BtnFullSmallY = 267;

        private const int SmallSlotOriginX = 9;
        private const int SmallSlotOriginY = 45;
        private const int SmallMesoTextRightX = 143;
        private const int SmallMesoTextY = 267;

        private const int ExpandedSlotOriginX = 9;
        private const int ExpandedSlotOriginY = 45;
        private const int ExpandedSectionPitch = 116;
        private const int ExpandedVisibleSlotCountPerTab = 24;
        #endregion

        #region Fields
        private bool _isExpanded;

        private readonly IDXObject _frameSmall;
        private IDXObject _foreground;
        private Point _foregroundOffset;

        private IDXObject _foregroundSmall;
        private Point _foregroundSmallOffset;

        private IDXObject _frameExpanded;
        private IDXObject _foregroundExpanded;
        private Point _foregroundExpandedOffset;

        private UIObject _btnFull;
        private UIObject _btnSmall;
        #endregion

        #region Properties
        public override string WindowName => "Inventory";

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                {
                    return;
                }

                _isExpanded = value;
                UpdateViewMode();
            }
        }

        public int CurrentWidth => _isExpanded ? WindowWidthExpanded : WindowWidthSmall;
        #endregion

        #region Constructor
        public InventoryUIBigBang(IDXObject frame, GraphicsDevice device)
            : base(frame, null, device)
        {
            _frameSmall = frame;
        }
        #endregion

        #region Initialization
        public void SetForeground(IDXObject foreground, int offsetX, int offsetY)
        {
            _foregroundSmall = foreground;
            _foregroundSmallOffset = new Point(offsetX, offsetY);

            if (!_isExpanded)
            {
                _foreground = foreground;
                _foregroundOffset = _foregroundSmallOffset;
            }
        }

        public void SetExpandedView(IDXObject expandedFrame, IDXObject expandedForeground, int fgOffsetX, int fgOffsetY)
        {
            _frameExpanded = expandedFrame;
            _foregroundExpanded = expandedForeground;
            _foregroundExpandedOffset = new Point(fgOffsetX, fgOffsetY);
        }

        public void InitializeBigBangButtons(UIObject btnGather, UIObject btnSort, UIObject btnFull, UIObject btnSmall)
        {
            _btnFull = btnFull;
            _btnSmall = btnSmall;

            if (_btnFull != null)
            {
                _btnFull.X = BtnFullSmallX;
                _btnFull.Y = BtnFullSmallY;
                AddButton(_btnFull);
                _btnFull.ButtonClickReleased += sender => IsExpanded = true;
            }

            if (_btnSmall != null)
            {
                _btnSmall.X = BtnFullSmallX;
                _btnSmall.Y = BtnFullSmallY;
                _btnSmall.SetVisible(false);
                AddButton(_btnSmall);
                _btnSmall.ButtonClickReleased += sender => IsExpanded = false;
            }
        }

        private void UpdateViewMode()
        {
            int expandedOffsetX = WindowWidthExpanded - WindowWidthSmall;

            if (_isExpanded)
            {
                if (_frameExpanded != null)
                {
                    Frame = _frameExpanded;
                }

                _foreground = _foregroundExpanded;
                _foregroundOffset = _foregroundExpandedOffset;

                if (closeButton != null)
                {
                    closeButton.X = 150 + expandedOffsetX;
                }

                if (_btnSmall != null)
                {
                    _btnSmall.X = BtnFullSmallX + expandedOffsetX;
                }

                _btnFull?.SetVisible(false);
                _btnSmall?.SetVisible(true);
            }
            else
            {
                Frame = _frameSmall;
                _foreground = _foregroundSmall;
                _foregroundOffset = _foregroundSmallOffset;

                if (closeButton != null)
                {
                    closeButton.X = 150;
                }

                if (_btnFull != null)
                {
                    _btnFull.X = BtnFullSmallX;
                }

                _btnFull?.SetVisible(true);
                _btnSmall?.SetVisible(false);
            }
        }
        #endregion

        #region Drawing
        protected override void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            int windowX = Position.X;
            int windowY = Position.Y;

            if (_foreground != null)
            {
                _foreground.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    windowX + _foregroundOffset.X, windowY + _foregroundOffset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            if (_isExpanded)
            {
                DrawExpandedContents(sprite, windowX, windowY);
                return;
            }

            InventoryType inventoryType = GetInventoryTypeFromTab(CurrentTab);
            if (!TryGetSlotsForType(inventoryType, out var slots))
            {
                return;
            }

            DrawMesoText(sprite, windowX, windowY, SmallMesoTextRightX, SmallMesoTextY);
            DrawSlotGrid(sprite, windowX, windowY, inventoryType, slots, SmallSlotOriginX, SmallSlotOriginY, _scrollOffset, TOTAL_SLOTS);
        }

        private void DrawExpandedContents(SpriteBatch sprite, int windowX, int windowY)
        {
            for (int tabIndex = TAB_EQUIP; tabIndex <= TAB_CASH; tabIndex++)
            {
                InventoryType inventoryType = GetInventoryTypeFromTab(tabIndex);
                if (!TryGetSlotsForType(inventoryType, out var slots))
                {
                    continue;
                }

                int originX = ExpandedSlotOriginX + ((tabIndex - TAB_EQUIP) * ExpandedSectionPitch);
                DrawSlotGrid(sprite, windowX, windowY, inventoryType, slots, originX, ExpandedSlotOriginY, 0, ExpandedVisibleSlotCountPerTab);
            }
        }

        protected override bool TryGetSlotAtPosition(int mouseX, int mouseY, out InventoryType inventoryType, out int slotIndex)
        {
            if (_isExpanded)
            {
                for (int tabIndex = TAB_EQUIP; tabIndex <= TAB_CASH; tabIndex++)
                {
                    inventoryType = GetInventoryTypeFromTab(tabIndex);
                    int originX = Position.X + ExpandedSlotOriginX + ((tabIndex - TAB_EQUIP) * ExpandedSectionPitch);
                    int originY = Position.Y + ExpandedSlotOriginY;
                    if (TryResolveSlotAtPosition(mouseX, mouseY, originX, originY, SLOTS_PER_ROW, VISIBLE_ROWS, 0, out slotIndex))
                    {
                        return true;
                    }
                }
            }

            inventoryType = GetInventoryTypeFromTab(CurrentTab);
            return TryResolveSlotAtPosition(
                mouseX,
                mouseY,
                Position.X + SmallSlotOriginX,
                Position.Y + SmallSlotOriginY,
                SLOTS_PER_ROW,
                VISIBLE_ROWS,
                _scrollOffset,
                out slotIndex);
        }
        #endregion

        #region Update
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }
        #endregion
    }
}
