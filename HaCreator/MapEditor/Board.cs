/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

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

        public void RenderList(IMapleList list, SpriteBatch sprite, int xShift, int yShift, SelectionInfo sel)
        {
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
                            item.Draw(sprite, item.GetColor(sel, item.Selected), xShift, yShift);
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
                    new Point(MultiBoard.VirtualToPhysical(mouse.MultiSelectStart.X, centerPoint.X, hScroll, 0), MultiBoard.VirtualToPhysical(mouse.MultiSelectStart.Y, centerPoint.Y, vScroll, 0)),
                    new Point(MultiBoard.VirtualToPhysical(mouse.X, centerPoint.X, hScroll, 0), MultiBoard.VirtualToPhysical(mouse.Y, centerPoint.Y, vScroll, 0)));
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

            // Render the minimap itself
            if (miniMap != null && UserSettings.useMiniMap)
            {
                // Area for the image itself
                Rectangle minimapImageArea = new Rectangle((miniMapPos.X + centerPoint.X) / _mag, (miniMapPos.Y + centerPoint.Y) / _mag, miniMap.Width, miniMap.Height);

                // Render gray area
                parent.FillRectangle(sprite, minimapArea, Color.Gray);
                // Render minimap
                if (miniMapTexture == null) 
                    miniMapTexture = miniMap.ToTexture2D(parent.GraphicsDevice);

                sprite.Draw(miniMapTexture, minimapImageArea, null, Color.White, 0, new Vector2(0, 0), SpriteEffects.None, 0.99999f);
                // Render current location on minimap
                parent.DrawRectangle(sprite, new Rectangle(hScroll / _mag, vScroll / _mag, parent.CurrentDXWindowSize.Width / _mag, (int)parent.CurrentDXWindowSize.Height / _mag), Color.Blue);
                
                // Render minimap borders
                parent.DrawRectangle(sprite, minimapImageArea, Color.Black);
            }
            
            // Render center point if InfoMode on
            if (ApplicationSettings.InfoMode)
            {
                parent.FillRectangle(sprite, new Rectangle(MultiBoard.VirtualToPhysical(-5, centerPoint.X, hScroll, 0), MultiBoard.VirtualToPhysical(-5 , centerPoint.Y, vScroll, 0), 10, 10), Color.DarkRed);
            }
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
    }
}
