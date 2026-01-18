using System;
using System.Threading;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure;
using HaCreator.Collections;
using HaCreator.MapEditor.UndoRedo;
using HaCreator.MapEditor.Input;
using HaCreator.MapEditor.Instance.Shapes;
using HaSharedLibrary.Util;
using System.Runtime.CompilerServices;
using HaCreator.MapEditor.Instance;
using System.Linq;
using Footholds;

namespace HaCreator.MapEditor
{
    public class Board
    {
        private Point mapSize;
        private Rectangle minimapArea;
        //private Point maxMapSize;
        private Point centerPoint;
        private readonly BoardItemsManager boardItems;
        private readonly List<Layer> mapLayers = new List<Layer>();
        private readonly List<BoardItem> selected = new List<BoardItem>();
        private MultiBoard parent;
        private readonly Mouse mouse;
        private MapInfo mapInfo = new MapInfo();
        private bool bIsNewMapDesign = false; // determines if this board is a new map design or editing an existing map.
        private System.Drawing.Bitmap miniMap;
        private System.Drawing.Point miniMapPos;
        private Texture2D miniMapTexture;

        // App settings
        private int selectedLayerIndex = ApplicationSettings.lastDefaultLayer;
        private int selectedPlatform = 0;
        private bool selectedAllLayers = ApplicationSettings.lastAllLayers;
        private bool selectedAllPlats = true;
        private int _hScroll = 0;
        private int _vScroll = 0;
        private int _mag = 16;
        private float _zoom = 1.0f;

        // Zoom limits
        public const float MinZoom = 0.1f;
        public const float MaxZoom = 4.0f;
        public const float ZoomStep = 0.1f;
        private readonly UndoRedoManager undoRedoMan;
        private ItemTypes visibleTypes;
        private ItemTypes editedTypes;
        private bool loading = false;
        private VRRectangle vrRect = null;
        private MinimapRectangle mmRect = null;
        private System.Windows.Controls.ContextMenu menu = null;
        private readonly SerializationManager serMan = null;
        private System.Windows.Controls.TabItem page = null;
        private bool dirty;
        private readonly int uid;

        private static int uidCounter = 0;

        // Cached portal connection pairs for efficient rendering
        private List<(PortalInstance, PortalInstance)> _cachedPortalPairs = null;
        private int _cachedPortalCount = -1;

        public ItemTypes VisibleTypes { get { return visibleTypes; } set { visibleTypes = value; } }
        public ItemTypes EditedTypes { get { return editedTypes; } set { editedTypes = value; } }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mapSize"></param>
        /// <param name="centerPoint"></param>
        /// <param name="parent"></param>
        /// <param name="bIsNewMapDesign">Determines if this board is a new map design or editing an existing map.</param>
        /// <param name="menu"></param>
        /// <param name="visibleTypes"></param>
        /// <param name="editedTypes"></param>
        public Board(Point mapSize, Point centerPoint, MultiBoard parent, bool bIsNewMapDesign, System.Windows.Controls.ContextMenu menu, ItemTypes visibleTypes, ItemTypes editedTypes)
        {
            this.uid = Interlocked.Increment(ref uidCounter);
            this.MapSize = mapSize;
            this.centerPoint = centerPoint;
            this.parent = parent;
            this.bIsNewMapDesign = bIsNewMapDesign;
            this.visibleTypes = visibleTypes;
            this.editedTypes = editedTypes;
            this.menu = menu;

            boardItems = new BoardItemsManager(this);
            undoRedoMan = new UndoRedoManager(this);
            mouse = new Mouse(this);
            serMan = new SerializationManager(this);
        }

        public static System.Drawing.Bitmap ResizeImage(System.Drawing.Bitmap FullsizeImage, float coeff)
        {
            return (System.Drawing.Bitmap)FullsizeImage.GetThumbnailImage((int)Math.Round(FullsizeImage.Width / coeff), (int)Math.Round(FullsizeImage.Height / coeff), null, IntPtr.Zero);
        }

        public static System.Drawing.Bitmap CropImage(System.Drawing.Bitmap img, System.Drawing.Rectangle selection)
        {
            System.Drawing.Bitmap result = new System.Drawing.Bitmap(selection.Width, selection.Height);
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(result))
            {
                g.DrawImage(img, new System.Drawing.Rectangle(0, 0, selection.Width, selection.Height), selection, System.Drawing.GraphicsUnit.Pixel);
            }
            return result;
        }


        /// <summary>
        /// Re-generates the minimap image from the board
        /// </summary>
        /// <returns></returns>
        public bool RegenerateMinimap()
        {
            try
            {
                lock (parent)
                {
                    if (MinimapRectangle == null)
                    {
                        MiniMap = null;
                    }
                    else
                    {
                        System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(mapSize.X, mapSize.Y);
                        System.Drawing.Graphics processor = System.Drawing.Graphics.FromImage(bmp);

                        foreach (BoardItem item in BoardItems.TileObjs)
                        {
                            bool isFlippedBoardItem = item.IsFlipped();
                            System.Drawing.Rectangle destRect = new System.Drawing.Rectangle(
                                item.X + centerPoint.X - item.Origin.X,
                                item.Y + centerPoint.Y - item.Origin.Y,
                                item.Image.Width,
                                item.Image.Height
                                );

                            if (isFlippedBoardItem)
                            {
                                processor.DrawImage(item.Image,
                                    destRect,
                                    item.Image.Width, 0, -item.Image.Width, item.Image.Height,
                                    System.Drawing.GraphicsUnit.Pixel);
                            }
                            else
                            {
                                processor.DrawImage(item.Image, destRect);
                            }
                        }
                        bmp = CropImage(bmp, new System.Drawing.Rectangle(MinimapRectangle.X + centerPoint.X, MinimapRectangle.Y + centerPoint.Y, MinimapRectangle.Width, MinimapRectangle.Height));
                        MiniMap = ResizeImage(bmp, (float)_mag);
                        MinimapPosition = new System.Drawing.Point(MinimapRectangle.X, MinimapRectangle.Y);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void RenderBoard(SpriteBatch sprite)
        {
            if (mapInfo == null) 
                return;
            int xShift = centerPoint.X - hScroll;
            int yShift = centerPoint.Y - vScroll;
            SelectionInfo sel = GetUserSelectionInfo();

            // Render the object lists
            foreach (IMapleList list in boardItems.AllItemLists)
            {
                RenderList(list, sprite, xShift, yShift, sel);
            }

            // Render the user's selection square
            if (mouse.MultiSelectOngoing)
            {
                Rectangle selectionRect = InputHandler.CreateRectangle(
                    new Point(
                        MultiBoard.VirtualToPhysical(mouse.MultiSelectStart.X, centerPoint.X, hScroll, 0), 
                        MultiBoard.VirtualToPhysical(mouse.MultiSelectStart.Y, centerPoint.Y, vScroll, 0)),
                    new Point(
                        MultiBoard.VirtualToPhysical(mouse.X, centerPoint.X, hScroll, 0), 
                        MultiBoard.VirtualToPhysical(mouse.Y, centerPoint.Y, vScroll, 0)));
                parent.DrawRectangle(sprite, selectionRect, UserSettings.SelectSquare);
                selectionRect.X++;
                selectionRect.Y++;
                selectionRect.Width--;
                selectionRect.Height--;
                parent.FillRectangle(sprite, selectionRect, UserSettings.SelectSquareFill);
            }
            
            // Render VR if it exists
            if (VRRectangle != null && (sel.visibleTypes & VRRectangle.Type) != 0)
            {
                VRRectangle.Draw(sprite, xShift, yShift, sel);
            }
            // Render minimap rectangle
            if (MinimapRectangle != null && (sel.visibleTypes & MinimapRectangle.Type) != 0)
            {
                MinimapRectangle.Draw(sprite, xShift, yShift, sel);
            }

            // Render center point if InfoMode on
            if (ApplicationSettings.InfoMode)
            {
                parent.FillRectangle(sprite, new Rectangle(MultiBoard.VirtualToPhysical(-5, centerPoint.X, hScroll, 0), MultiBoard.VirtualToPhysical(-5 , centerPoint.Y, vScroll, 0), 10, 10), Color.DarkRed);
            }
        }

        /// <summary>
        /// Renders backgrounds separately. This should be called with a sprite batch
        /// that does NOT have the zoom transform applied, so backgrounds stay at a fixed
        /// screen position regardless of viewport zoom level.
        /// </summary>
        public void RenderBackgrounds(SpriteBatch sprite)
        {
            if (mapInfo == null)
                return;

            int xShift = centerPoint.X - hScroll;
            int yShift = centerPoint.Y - vScroll;
            SelectionInfo sel = GetUserSelectionInfo();

            // Render back backgrounds
            if ((sel.visibleTypes & ItemTypes.Backgrounds) != 0)
            {
                foreach (BackgroundInstance bg in boardItems.BackBackgrounds)
                {
                    bg.Draw(sprite, bg.GetColor(sel, bg.Selected), xShift, yShift);
                }
            }
        }

        /// <summary>
        /// Renders front backgrounds separately. This should be called after other items
        /// but without the zoom transform.
        /// </summary>
        public void RenderFrontBackgrounds(SpriteBatch sprite)
        {
            if (mapInfo == null)
                return;

            int xShift = centerPoint.X - hScroll;
            int yShift = centerPoint.Y - vScroll;
            SelectionInfo sel = GetUserSelectionInfo();

            // Render front backgrounds
            if ((sel.visibleTypes & ItemTypes.Backgrounds) != 0)
            {
                foreach (BackgroundInstance bg in boardItems.FrontBackgrounds)
                {
                    bg.Draw(sprite, bg.GetColor(sel, bg.Selected), xShift, yShift);
                }
            }
        }

        /// <summary>
        /// Renders the minimap overlay. This should be called with a separate sprite batch
        /// that does NOT have the zoom transform applied, so the minimap stays at a fixed
        /// screen size regardless of viewport zoom level.
        /// </summary>
        public void RenderMinimap(SpriteBatch sprite)
        {
            if (miniMap == null || !UserSettings.useMiniMap)
                return;

            // Area for the image itself
            Rectangle minimapImageArea = new Rectangle(
                (miniMapPos.X + centerPoint.X) / _mag,
                (miniMapPos.Y + centerPoint.Y) / _mag,
                miniMap.Width,
                miniMap.Height);

            // Render gray area
            parent.FillRectangle(sprite, minimapArea, Color.Gray);

            // Render minimap
            if (miniMapTexture == null)
                miniMapTexture = miniMap.ToTexture2D(parent.GraphicsDevice);

            sprite.Draw(miniMapTexture, minimapImageArea, null, Color.White, 0, new Vector2(0, 0), SpriteEffects.None, 0.99999f);

            // Render current location on minimap
            // Account for zoom: when zoomed in, viewport shows less virtual space
            int viewportWidth = (int)(parent.CurrentDXWindowSize.Width / _zoom);
            int viewportHeight = (int)(parent.CurrentDXWindowSize.Height / _zoom);
            parent.DrawRectangle(sprite, new Rectangle(
                hScroll / _mag,
                vScroll / _mag,
                viewportWidth / _mag,
                viewportHeight / _mag), Color.Blue);

            // Render minimap borders
            parent.DrawRectangle(sprite, minimapImageArea, Color.Black);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RenderList(IMapleList list, SpriteBatch sprite, int xShift, int yShift, SelectionInfo sel)
        {
            // Skip backgrounds - they are rendered separately without zoom transform
            if (list.ListType == ItemTypes.Backgrounds)
                return;

            if (list.ListType == ItemTypes.None)
            {
                foreach (BoardItem item in list)
                {
                    if (parent.IsItemInRange(item.X, item.Y, item.Width, item.Height, xShift - item.Origin.X, yShift - item.Origin.Y) && ((sel.visibleTypes & item.Type) != 0))
                        item.Draw(sprite, item.GetColor(sel, item.Selected), xShift, yShift);
                }
            }
            else if ((sel.visibleTypes & list.ListType) != 0)
            {
                if (list.IsItem)
                {
                    foreach (BoardItem item in list)
                    {
                        if (parent.IsItemInRange(item.X, item.Y, item.Width, item.Height, xShift - item.Origin.X, yShift - item.Origin.Y))
                        {
                            item.Draw(sprite, item.GetColor(sel, item.Selected), xShift, yShift);
                        }
                    }

                    // Render lines between local teleport portal (using cached pairs)
                    if (list.ListType == ItemTypes.Portals)
                    {
                        Color portalLineColor = (sel.editedTypes & ItemTypes.Portals) == ItemTypes.Portals ? Color.LightBlue : MultiBoard.InactiveColor;

                        foreach (var (portal1, portal2) in GetPortalConnectionPairs())
                        {
                            // Calculate screen positions
                            int x1 = MultiBoard.VirtualToPhysical(portal1.X, centerPoint.X, hScroll, 0);
                            int y1 = MultiBoard.VirtualToPhysical(portal1.Y, centerPoint.Y, vScroll, 0);
                            int x2 = MultiBoard.VirtualToPhysical(portal2.X, centerPoint.X, hScroll, 0);
                            int y2 = MultiBoard.VirtualToPhysical(portal2.Y, centerPoint.Y, vScroll, 0);

                            // Draw the line
                            parent.DrawLine(sprite,
                                new Vector2(x1, y1),
                                new Vector2(x2, y2),
                                portalLineColor);
                        }
                    }
                }
                else
                {
                    foreach (MapleLine line in list)
                    {
                        if (parent.IsItemInRange(Math.Min(line.FirstDot.X, line.SecondDot.X), Math.Min(line.FirstDot.Y, line.SecondDot.Y), Math.Abs(line.FirstDot.X - line.SecondDot.X), Math.Abs(line.FirstDot.Y - line.SecondDot.Y), xShift, yShift))
                            line.Draw(sprite, line.GetColor(sel), xShift, yShift);
                    }
                }
            }
        }

        /// <summary>
        /// Gets cached portal connection pairs for local teleport portals.
        /// Rebuilds cache if portals have changed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private List<(PortalInstance, PortalInstance)> GetPortalConnectionPairs()
        {
            int currentCount = BoardItems.Portals.Count;

            // Rebuild cache if portal count changed or cache doesn't exist
            if (_cachedPortalPairs == null || _cachedPortalCount != currentCount)
            {
                _cachedPortalPairs = new List<(PortalInstance, PortalInstance)>();
                _cachedPortalCount = currentCount;

                // Build lookup dictionary for O(1) portal name lookups
                var portalsByName = new Dictionary<string, PortalInstance>();
                var localTeleportPortals = new List<PortalInstance>();

                foreach (var portal in BoardItems.Portals)
                {
                    if (portal.pt == PortalType.Hidden || portal.pt == PortalType.Invisible)
                    {
                        localTeleportPortals.Add(portal);
                    }
                    // Store all portals by name for target lookup
                    if (!string.IsNullOrEmpty(portal.pn) && !portalsByName.ContainsKey(portal.pn))
                    {
                        portalsByName[portal.pn] = portal;
                    }
                }

                // Build pairs using HashSet to avoid duplicates
                var processedPairs = new HashSet<(string, string)>();
                foreach (var portal1 in localTeleportPortals)
                {
                    if (string.IsNullOrEmpty(portal1.tn) || !portalsByName.TryGetValue(portal1.tn, out var portal2))
                        continue;
                    if (portal1 == portal2)
                        continue;

                    // Create unique pair identifier
                    var pair = string.CompareOrdinal(portal1.pn, portal2.pn) < 0
                        ? (portal1.pn, portal2.pn)
                        : (portal2.pn, portal1.pn);

                    if (!processedPairs.Contains(pair))
                    {
                        processedPairs.Add(pair);
                        _cachedPortalPairs.Add((portal1, portal2));
                    }
                }
            }

            return _cachedPortalPairs;
        }

        /// <summary>
        /// Invalidates the cached portal pairs. Call when portal pn/tn properties change.
        /// </summary>
        public void InvalidatePortalPairCache()
        {
            _cachedPortalPairs = null;
            _cachedPortalCount = -1;
        }

        public void Dispose()
        {
            lock (parent)
            {
                parent.Boards.Remove(this);
                boardItems.Clear();
                selected.Clear();
                mapLayers.Clear();
            }
            // This must be called when MultiBoard is unlocked, to prevent BackupManager deadlocking
            parent.OnBoardRemoved(this);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        #region Properties
        public int UniqueID
        {
            get { return uid; }
        }

        public bool Dirty
        {
            get { return dirty; }
            set { dirty = value; }
        }

        public UndoRedoManager UndoRedoMan
        {
            get { return undoRedoMan; }
        }

        public int mag
        {
            get { return _mag; }
            set { lock (parent) { _mag = value; } }
        }

        /// <summary>
        /// Zoom level for the main viewport (1.0 = 100%, 0.5 = 50%, 2.0 = 200%)
        /// </summary>
        public float Zoom
        {
            get { return _zoom; }
            set
            {
                lock (parent)
                {
                    _zoom = Math.Max(MinZoom, Math.Min(MaxZoom, value));
                    parent.AdjustScrollBars();
                }
            }
        }

        /// <summary>
        /// Zoom in by one step
        /// </summary>
        public void ZoomIn()
        {
            Zoom = _zoom + ZoomStep;
        }

        /// <summary>
        /// Zoom out by one step
        /// </summary>
        public void ZoomOut()
        {
            Zoom = _zoom - ZoomStep;
        }

        /// <summary>
        /// Reset zoom to 100%
        /// </summary>
        public void ResetZoom()
        {
            Zoom = 1.0f;
        }

        public MapInfo MapInfo
        {
            get { return mapInfo; }
            set 
            { 
                lock (parent) 
                { 
                    mapInfo = value; 
                } 
            }
        }

        /// <summary>
        /// Determines if this board is a new map design or editing an existing map.
        /// </summary>
        public bool IsNewMapDesign { 
            get { return bIsNewMapDesign; }
            private set { }
        }


        public System.Drawing.Bitmap MiniMap
        {
            get { return miniMap; }
            set { lock (parent) { miniMap = value; miniMapTexture = null; } }
        }

        public System.Drawing.Point MinimapPosition
        {
            get { return miniMapPos; }
            set { miniMapPos = value; }
        }

        public int hScroll
        {
            get
            {
                return _hScroll;
            }
            set
            {
                lock (parent)
                {
                    _hScroll = value;
                    parent.SetHScrollbarValue(_hScroll);
                }
            }
        }

        public Point CenterPoint
        {
            get { return centerPoint; }
            internal set { centerPoint = value; }
        }

        public int vScroll
        {
            get
            {
                return _vScroll;
            }
            set
            {
                lock (parent)
                {
                    _vScroll = value;
                    parent.SetVScrollbarValue(_vScroll);
                }
            }
        }

        public MultiBoard ParentControl
        {
            get
            {
                return parent;
            }
            internal set
            {
                parent = value;
            }
        }

        public Mouse Mouse
        {
            get { return mouse; }
        }

        public Point MapSize
        {
            get
            {
                return mapSize;
            }
            set
            {
                mapSize = value;
                minimapArea = new Rectangle(0, 0, mapSize.X / _mag, mapSize.Y / _mag);
            }
        }

        public Rectangle MinimapArea
        {
            get { return minimapArea; }
        }

        public VRRectangle VRRectangle
        {
            get { return vrRect; }
            set 
            { 
                vrRect = value;
                ((System.Windows.Controls.MenuItem) menu.Items[1]).IsEnabled = value == null;
            }
        }

        public MinimapRectangle MinimapRectangle
        {
            get { return mmRect; }
            set 
            { 
                mmRect = value;
                ((System.Windows.Controls.MenuItem)menu.Items[2]).IsEnabled = value == null;
                parent.OnMinimapStateChanged(this, mmRect != null);
            }
        }

        public BoardItemsManager BoardItems
        {
            get
            {
                return boardItems;
            }
        }

        public List<BoardItem> SelectedItems
        {
            get
            {
                return selected;
            }
        }

        /// <summary>
        /// Map layers
        /// </summary>
        public void CreateMapLayers()
        {
            for (int i = 0; i <= MapConstants.MaxMapLayers; i++)
            {
                AddMapLayer(new Layer(this));
            }
        }
        
        public void AddMapLayer(Layer layer)
        {
            lock (parent)
                mapLayers.Add(layer);
        }

        /// <summary>
        /// Gets the map layers
        /// </summary>
        public ReadOnlyCollection<Layer> Layers
        {
            get
            {
                return mapLayers.AsReadOnly();
            }
        }

        public int SelectedLayerIndex
        {
            get
            {
                return selectedLayerIndex;
            }
            set
            {
                lock (parent)
                {
                    selectedLayerIndex = value;
                }
            }
        }

        public bool SelectedAllLayers
        {
            get { return selectedAllLayers; }
            set { selectedAllLayers = value; }
        }

        public System.Windows.Controls.ContextMenu Menu
        {
            get { return menu; }
        }

        public Layer SelectedLayer
        {
            get { return Layers[SelectedLayerIndex]; }
        }

        public int SelectedPlatform
        {
            get { return selectedPlatform; }
            set { selectedPlatform = value; }
        }

        public bool SelectedAllPlatforms
        {
            get { return selectedAllPlats; }
            set { selectedAllPlats = value; }
        }

        public SelectionInfo GetUserSelectionInfo()
        {
            return new SelectionInfo(selectedAllLayers ? -1 : selectedLayerIndex, selectedAllPlats ? -1 : selectedPlatform, visibleTypes, editedTypes);
        }

        public bool Loading { get { return loading; } set { loading = value; } }

        public SerializationManager SerializationManager
        {
            get { return serMan; }
        }

        public System.Windows.Controls.TabItem TabPage
        {
            get { return page; }
            set { page = value; }
        }
        #endregion

        #region Foothold Utilities
        /// <summary>
        /// Finds the foothold below the given position.
        /// Used by MapSimulator and AI MapEditor for placing entities on platforms.
        /// </summary>
        /// <param name="x">X coordinate to check</param>
        /// <param name="y">Y coordinate to check</param>
        /// <param name="searchRange">Maximum distance below to search (default 500)</param>
        /// <param name="upwardTolerance">Allow finding footholds slightly above (default 10)</param>
        /// <returns>The foothold below, or null if none found</returns>
        public FootholdLine FindFootholdBelow(float x, float y, float searchRange = 500f, float upwardTolerance = 10f)
        {
            var footholds = BoardItems.FootholdLines;
            if (footholds == null || footholds.Count == 0)
                return null;

            FootholdLine bestFh = null;
            float bestDist = float.MaxValue;

            foreach (var fh in footholds)
            {
                // Skip walls (vertical footholds)
                if (fh.IsWall)
                    continue;

                // Check if X is within foothold range
                float fhMinX = Math.Min(fh.FirstDot.X, fh.SecondDot.X);
                float fhMaxX = Math.Max(fh.FirstDot.X, fh.SecondDot.X);

                if (x < fhMinX || x > fhMaxX)
                    continue;

                // Calculate Y at X position on this foothold (linear interpolation for slopes)
                float dx = fh.SecondDot.X - fh.FirstDot.X;
                float dy = fh.SecondDot.Y - fh.FirstDot.Y;
                float t = (dx != 0) ? (x - fh.FirstDot.X) / dx : 0;
                float fhY = fh.FirstDot.Y + t * dy;

                // Check if foothold is within range (below or slightly above)
                // dist > 0 means foothold is below, dist < 0 means foothold is above
                float dist = fhY - y;
                float absDist = Math.Abs(dist);

                // Accept footholds below (within searchRange) or slightly above (within tolerance)
                if ((dist >= 0 && dist < searchRange) || (dist < 0 && -dist <= upwardTolerance))
                {
                    if (absDist < bestDist)
                    {
                        bestDist = absDist;
                        bestFh = fh;
                    }
                }
            }

            return bestFh;
        }

        /// <summary>
        /// Calculates the Y coordinate on a foothold at a given X position.
        /// </summary>
        public static float CalculateYOnFoothold(FootholdLine fh, float x)
        {
            float dx = fh.SecondDot.X - fh.FirstDot.X;
            float dy = fh.SecondDot.Y - fh.FirstDot.Y;
            float t = (dx != 0) ? (x - fh.FirstDot.X) / dx : 0;
            return fh.FirstDot.Y + t * dy;
        }
        #endregion
    }
}
