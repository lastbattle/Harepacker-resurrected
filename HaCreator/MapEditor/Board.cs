/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure;
using System.Windows.Forms;
using HaCreator.Collections;
using HaCreator.MapEditor.UndoRedo;
using HaCreator.MapEditor.Input;
using HaCreator.MapEditor.Instance.Shapes;
using System.Threading;

namespace HaCreator.MapEditor
{
    public class Board
    {
        private Point mapSize;
        private Rectangle minimapArea;
        //private Point maxMapSize;
        private Point centerPoint;
        private BoardItemsManager boardItems;
        private List<Layer> layers = new List<Layer>();
        private List<BoardItem> selected = new List<BoardItem>();
        private MultiBoard parent;
        private Mouse mouse;
        private MapInfo mapInfo = new MapInfo();
        private System.Drawing.Bitmap miniMap;
        private System.Drawing.Point miniMapPos;
        private Texture2D miniMapTexture;
        private int selectedLayerIndex = ApplicationSettings.lastDefaultLayer;
        private int selectedPlatform = 0;
        private bool selectedAllLayers = ApplicationSettings.lastAllLayers;
        private bool selectedAllPlats = true;
        private int _hScroll = 0;
        private int _vScroll = 0;
        private int _mag = 16;
        private UndoRedoManager undoRedoMan;
        ItemTypes visibleTypes;
        ItemTypes editedTypes;
        private bool loading = false;
        private VRRectangle vrRect = null;
        private MinimapRectangle mmRect = null;
        private ContextMenuStrip menu = null;
        private SerializationManager serMan = null;
        private HaCreator.ThirdParty.TabPages.TabPage page = null;
        private bool dirty;
        private int uid;

        private static int uidCounter = 0;

        public ItemTypes VisibleTypes { get { return visibleTypes; } set { visibleTypes = value; } }
        public ItemTypes EditedTypes { get { return editedTypes; } set { editedTypes = value; } }

        public Board(Point mapSize, Point centerPoint, MultiBoard parent, ContextMenuStrip menu, ItemTypes visibleTypes, ItemTypes editedTypes)
        {
            this.uid = Interlocked.Increment(ref uidCounter);
            this.MapSize = mapSize;
            this.centerPoint = centerPoint;
            this.parent = parent;
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
                            processor.DrawImage(item.Image, new System.Drawing.Point(item.X + centerPoint.X - item.Origin.X, item.Y + centerPoint.Y - item.Origin.Y));
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
            for (int i = 0; i < boardItems.AllItemLists.Length; i++)
                RenderList(boardItems.AllItemLists[i], sprite, xShift, yShift, sel);

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
                    miniMapTexture = BoardItem.TextureFromBitmap(parent.Device, miniMap);
                sprite.Draw(miniMapTexture, minimapImageArea, null, Color.White, 0, new Vector2(0, 0), SpriteEffects.None, 0.99999f);
                // Render current location on minimap
                parent.DrawRectangle(sprite, new Rectangle(hScroll / _mag, vScroll / _mag, parent.Width / _mag, parent.Height / _mag), Color.Blue);
                
                // Render minimap borders
                parent.DrawRectangle(sprite, minimapImageArea, Color.Black);
            }
            
            // Render center point if InfoMode on
            if (ApplicationSettings.InfoMode)
            {
                parent.FillRectangle(sprite, new Rectangle(MultiBoard.VirtualToPhysical(-5, centerPoint.X, hScroll, 0), MultiBoard.VirtualToPhysical(-5 , centerPoint.Y, vScroll, 0), 10, 10), Color.DarkRed);
            }
        }

        public void CreateLayers(int num)
        {
            for (int i = 0; i < num; i++)
                new Layer(this);
        }

        public void Dispose()
        {
            lock (parent)
            {
                parent.Boards.Remove(this);
                boardItems.Clear();
                selected.Clear();
                layers.Clear();
            }
            // This must be called when MultiBoard is unlocked, to prevent BackupManager deadlocking
            parent.OnBoardRemoved(this);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public void CopyItemsTo(List<BoardItem> items, Board dstBoard, Point offset)
        {

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
                menu.Items[1].Enabled = value == null;
            }
        }

        public MinimapRectangle MinimapRectangle
        {
            get { return mmRect; }
            set 
            { 
                mmRect = value;
                menu.Items[2].Enabled = value == null;
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

        public List<Layer> Layers
        {
            get
            {
                return layers;
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

        public ContextMenuStrip Menu
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

        public HaCreator.ThirdParty.TabPages.TabPage TabPage
        {
            get { return page; }
            set { page = value; }
        }
        #endregion
    }
}
