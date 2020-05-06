/* Copyright (C) 2015 haha01haha01
 * 2020 lastbattle

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

// uncomment line below to use XNA's Z-order functions
// #define UseXNAZorder

// uncomment line below to show FPS counter
// #define FPS_TEST


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;

using HaCreator.Collections;
using HaCreator.MapEditor.Input;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Text;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using SpriteBatch = Microsoft.Xna.Framework.Graphics.SpriteBatch;



namespace HaCreator.MapEditor
{
    public partial class MultiBoard : UserControl
    {
        private GraphicsDevice DxDevice;
        private Microsoft.Xna.Framework.Graphics.SpriteBatch sprite;
        private PresentationParameters pParams = new PresentationParameters();
        private Microsoft.Xna.Framework.Graphics.Texture2D pixel;
        private List<Board> boards = new List<Board>();
        private Board selectedBoard = null;
        private FontEngine fontEngine;
        private Thread renderer;
        private bool needsReset = false;
        private IntPtr dxHandle;
        private UserObjectsManager userObjs;
        private Scheduler scheduler;

        private System.Windows.WindowState CurrentHostWindowState = System.Windows.WindowState.Normal;
        private System.Drawing.Size _CurrentDXWindowSize = new System.Drawing.Size();
        public System.Drawing.Size CurrentDXWindowSize
        {
            get { return _CurrentDXWindowSize; }
            private set { }
        }

#if FPS_TEST
        private FPSCounter fpsCounter = new FPSCounter();
#endif


        #region Window
        /// <summary>
        /// Update the state of the host window
        /// </summary>
        /// <param name="CurrentHostWindowState"></param>
        public void UpdateWindowState(System.Windows.WindowState CurrentHostWindowState)
        {
            this.CurrentHostWindowState = CurrentHostWindowState;
        }

        public void UpdateWindowSize(System.Windows.Size CurrentWindowSize)
        {
            _CurrentDXWindowSize = DxContainer.ClientSize;

            needsReset = true;
        }

        private void MultiBoard2_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _CurrentDXWindowSize = DxContainer.ClientSize;
        }

        #endregion 

        private void RenderLoop()
        {
            PrepareDevice();
            pixel = CreatePixel();
            DeviceReady = true;

            while (!Program.AbortThreads)
            {
                if (DeviceReady && CurrentHostWindowState != System.Windows.WindowState.Minimized)
                {
                    RenderFrame();
#if FPS_TEST
                    fpsCounter.Tick();
#endif
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }

        #region Initialization
        public MultiBoard()
        {
            InitializeComponent();

            if (!DesignerProperties.GetIsInDesignMode(this)) // stupid errors popping up in design mode 
                winFormDXHolder.Visibility = Visibility.Visible;

            this.dxHandle = DxContainer.Handle;
            this.userObjs = new UserObjectsManager(this);
            this.SizeChanged += MultiBoard2_SizeChanged;
        }

        public void Start()
        {
            if (DeviceReady) 
                return;

            if (selectedBoard == null) 
                throw new Exception("Cannot start without a selected board");
            Visibility = Visibility.Visible;

            AdjustScrollBars();
            renderer = new Thread(new ThreadStart(RenderLoop));
            renderer.Start();

            Dictionary<Action, int> clientList = new Dictionary<Action, int>();
            clientList.Add(delegate
            {
                //if (BackupCheck != null)
                //    BackupCheck.Invoke();
            }, 1000);
            scheduler = new Scheduler(clientList);
        }

        public void Stop()
        {
            if (renderer != null)
            {
                renderer.Join();
                renderer = null;
            }
            if (scheduler != null)
            {
                scheduler.Dispose();
            }
        }

        public static Microsoft.Xna.Framework.Graphics.GraphicsDevice CreateGraphicsDevice(PresentationParameters pParams)
        {
            Microsoft.Xna.Framework.Graphics.GraphicsDevice result;
            try
            {
                result = new Microsoft.Xna.Framework.Graphics.GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef, pParams);
            }
            catch (Exception e)
            {
                MessageBox.Show(string.Format("Graphics adapter is not supported: {0}\r\n\r\n{1}", e.Message, e.StackTrace));
                Environment.Exit(0);
                // This code will never be reached, but VS still requires this path to end
                throw;
            }
            return result;
        }

        private void PrepareDevice()
        {
            pParams.BackBufferWidth = Math.Max(_CurrentDXWindowSize.Width, 1);
            pParams.BackBufferHeight = Math.Max(_CurrentDXWindowSize.Height, 1);
            pParams.BackBufferFormat = SurfaceFormat.Color;
            pParams.DepthStencilFormat = DepthFormat.Depth24Stencil8;
            pParams.DeviceWindowHandle = dxHandle;
            pParams.IsFullScreen = false;
            //pParams.PresentationInterval = PresentInterval.Immediate;
            DxDevice = CreateGraphicsDevice(pParams);
            fontEngine = new FontEngine(UserSettings.FontName, UserSettings.FontStyle, UserSettings.FontSize, DxDevice);
            sprite = new SpriteBatch(DxDevice);
        }

        #endregion

        #region Methods
        public void OnMinimapStateChanged(Board board, bool hasMm)
        {
            if (MinimapStateChanged != null)
                MinimapStateChanged.Invoke(board, hasMm);
        }

        public void OnBoardRemoved(Board board)
        {
            if (BoardRemoved != null)
                BoardRemoved.Invoke(board, null);
        }

        private Microsoft.Xna.Framework.Graphics.Texture2D CreatePixel()
        {
            System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(1, 1);
            bmp.SetPixel(0, 0, System.Drawing.Color.White);

            return BoardItem.TextureFromBitmap(DxDevice, bmp);
        }

        public Board CreateBoard(Microsoft.Xna.Framework.Point mapSize, Point centerPoint, int layers, System.Windows.Controls.ContextMenu menu)
        {
            lock (this)
            {
                Board newBoard = new Board(mapSize, centerPoint, this, menu, ApplicationSettings.theoreticalVisibleTypes, ApplicationSettings.theoreticalEditedTypes);
                boards.Add(newBoard);
                newBoard.CreateLayers(layers);
                return newBoard;
            }
        }

        public Board CreateHiddenBoard(Point mapSize, Point centerPoint, int layers)
        {
            lock (this)
            {
                Board newBoard = new Board(mapSize, centerPoint, this, null, ItemTypes.None, ItemTypes.None);
                newBoard.CreateLayers(layers);
                return newBoard;
            }
        }

        public void DrawLine(SpriteBatch sprite, Vector2 start, Vector2 end, Color color)
        {
            int width = (int)Vector2.Distance(start, end);
            float rotation = (float)Math.Atan2((double)(end.Y - start.Y), (double)(end.X - start.X));
            sprite.Draw(pixel, new Rectangle((int)start.X, (int)start.Y, width, UserSettings.LineWidth), null, color, rotation, new Vector2(0f, 0f), SpriteEffects.None, 1f);
        }

        public void DrawRectangle(SpriteBatch sprite, Rectangle rectangle, Color color)
        {
            //clockwise
            Vector2 pt1 = new Vector2(rectangle.Left, rectangle.Top);
            Vector2 pt2 = new Vector2(rectangle.Right, rectangle.Top);
            Vector2 pt3 = new Vector2(rectangle.Right, rectangle.Bottom);
            Vector2 pt4 = new Vector2(rectangle.Left, rectangle.Bottom);

            DrawLine(sprite, pt1, pt2, color);
            DrawLine(sprite, pt2, pt3, color);
            DrawLine(sprite, pt3, pt4, color);
            DrawLine(sprite, pt4, pt1, color);
        }

        public void FillRectangle(SpriteBatch sprite, Rectangle rectangle, Color color)
        {
            sprite.Draw(pixel, rectangle, color);
        }

        public void DrawDot(SpriteBatch sprite, int x, int y, Color color, int dotSize)
        {
            int dotW = UserSettings.DotWidth * dotSize;
            FillRectangle(sprite, new Rectangle(x - dotW, y - dotW, dotW * 2, dotW * 2), color);
        }

        public void RenderFrame()
        {
            if (needsReset)
            {
                needsReset = false;

                this.Dispatcher.Invoke((Action)delegate
                {
                    AdjustScrollBars();
                });
                ResetDevice();
            }
            DxDevice.Clear(ClearOptions.Target, Color.White, 1.0f, 0); // Clear the window to black
#if UseXNAZorder
            sprite.Begin(SpriteBlendMode.AlphaBlend, SpriteSortMode.FrontToBack, SaveStateMode.None);
#else
            sprite.Begin(SpriteSortMode.Immediate, Microsoft.Xna.Framework.Graphics.BlendState.NonPremultiplied);
#endif
            lock (this)
            {
                selectedBoard.RenderBoard(sprite);
                if (selectedBoard.MapSize.X < _CurrentDXWindowSize.Width)
                {
                    DrawLine(sprite, new Vector2(MapSize.X, 0), new Vector2(MapSize.X, _CurrentDXWindowSize.Height), Color.Black);
                }
                if (selectedBoard.MapSize.Y < _CurrentDXWindowSize.Height)
                {
                    DrawLine(sprite, new Vector2(0, MapSize.Y), new Vector2(_CurrentDXWindowSize.Width, MapSize.Y), Color.Black);
                }
            }
#if FPS_TEST
            fontEngine.DrawString(sprite, new System.Drawing.Point(), Color.Black, fpsCounter.Frames.ToString(), 1000);
#endif
            sprite.End();
            try
            {
                DxDevice.Present();
            }
            catch (DeviceLostException)
            {
            }
            catch (DeviceNotResetException)
            {
                needsReset = true;
            }

        }

        public bool IsItemInRange(int x, int y, int w, int h, int xshift, int yshift)
        {
            return x + xshift + w > 0 && y + yshift + h > 0 && x + xshift < _CurrentDXWindowSize.Width && y + yshift < _CurrentDXWindowSize.Height;
        }
        #endregion

        #region Properties
        public bool DeviceReady { get; set; } = false;

        public FontEngine FontEngine
        {
            get { return fontEngine; }
        }

        public double MaxHScroll
        {
            get { return hScrollBar.Maximum; }
        }

        public double MaxVScroll
        {
            get { return vScrollBar.Maximum; }
        }

        public GraphicsDevice Device
        {
            get { return DxDevice; }
        }

        public List<Board> Boards
        {
            get
            {
                return boards;
            }
        }

        public Board SelectedBoard
        {
            get
            {
                return selectedBoard;
            }
            set
            {
                lock (this)
                {
                    selectedBoard = value;
                    if (value != null)
                        AdjustScrollBars();
                }
            }
        }

        public Point MapSize
        {
            get
            {
                return selectedBoard.MapSize;
            }
        }

        public UserObjectsManager UserObjects
        {
            get
            {
                return userObjs;
            }
        }

        public bool AssertLayerSelected()
        {
            if (SelectedBoard.SelectedLayerIndex == -1)
            {
                MessageBox.Show("Select a real layer", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
            return true;
        }
        #endregion

        #region Human I\O Handling
        private BoardItem GetHighestItem(List<BoardItem> items)
        {
            if (items.Count < 1) return null;
            int highestZ = -1;
            BoardItem highestItem = null;
            int zSum;
            foreach (BoardItem item in items)
            {
                zSum = (item is LayeredItem) ? ((LayeredItem)item).Layer.LayerNumber * 100 + item.Z : 900 + item.Z;
                if (zSum > highestZ)
                {
                    highestZ = zSum;
                    highestItem = item;
                }
            }
            return highestItem;
        }

        public static int PhysicalToVirtual(int location, int center, int scroll, int origin)
        {
            return location - center + scroll + origin;
        }

        public static int VirtualToPhysical(int location, int center, int scroll, int origin)
        {
            return location + center - scroll - origin;
        }

        public static bool IsItemUnderRectangle(BoardItem item, Rectangle rect)
        {
            return (item.Right > rect.Left && item.Left < rect.Right && item.Bottom > rect.Top && item.Top < rect.Bottom);
        }

        public static bool IsItemInsideRectangle(BoardItem item, Rectangle rect)
        {
            return (item.Left > rect.Left && item.Right < rect.Right && item.Top > rect.Top && item.Bottom < rect.Bottom);
        }

        private void GetObjsUnderPointFromList(IMapleList list, Point locationVirtualPos, ref BoardItem itemUnderPoint, ref BoardItem selectedUnderPoint, ref bool selectedItemHigher)
        {
            if (!list.IsItem) 
                return;
            SelectionInfo sel = selectedBoard.GetUserSelectionInfo();
            if (list.ListType == ItemTypes.None)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    BoardItem item = (BoardItem)list[i];
                    if ((selectedBoard.EditedTypes & item.Type) != item.Type) continue;
                    if (IsPointInsideRectangle(locationVirtualPos, item.Left, item.Top, item.Right, item.Bottom) 
                        && !(item is HaCreator.MapEditor.Input.Mouse) 
                        && item.CheckIfLayerSelected(sel)
                        && !item.IsPixelTransparent(locationVirtualPos.X - item.Left, locationVirtualPos.Y - item.Top))
                    {
                        if (item.Selected)
                        {
                            selectedUnderPoint = item;
                            selectedItemHigher = true;
                        }
                        else
                        {
                            itemUnderPoint = item;
                            selectedItemHigher = false;
                        }
                    }
                }
            }
            else if ((selectedBoard.EditedTypes & list.ListType) == list.ListType)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    BoardItem item = (BoardItem)list[i];
                    if (IsPointInsideRectangle(locationVirtualPos, item.Left, item.Top, item.Right, item.Bottom) 
                        && !(item is HaCreator.MapEditor.Input.Mouse) 
                        && !(item is HaCreator.MapEditor.Input.Mouse) 
                        && item.CheckIfLayerSelected(sel) 
                        && !item.IsPixelTransparent(locationVirtualPos.X - item.Left, locationVirtualPos.Y - item.Top))
                    {
                        if (item.Selected)
                        {
                            selectedUnderPoint = item;
                            selectedItemHigher = true;
                        }
                        else
                        {
                            itemUnderPoint = item;
                            selectedItemHigher = false;
                        }
                    }
                }
            }
        }

        private BoardItemPair GetObjectsUnderPoint(Point location, out bool selectedItemHigher)
        {
            selectedItemHigher = false; //to stop VS from bitching
            BoardItem itemUnderPoint = null, selectedUnderPoint = null;
            Point locationVirtualPos = new Point(PhysicalToVirtual(location.X, selectedBoard.CenterPoint.X, selectedBoard.hScroll, /*item.Origin.X*/0), PhysicalToVirtual(location.Y, selectedBoard.CenterPoint.Y, selectedBoard.vScroll, /*item.Origin.Y*/0));
            for (int i = 0; i < selectedBoard.BoardItems.AllItemLists.Length; i++)
            {
                GetObjsUnderPointFromList(selectedBoard.BoardItems.AllItemLists[i], locationVirtualPos, ref itemUnderPoint, ref selectedUnderPoint, ref selectedItemHigher);
            }
            return new BoardItemPair(itemUnderPoint, selectedUnderPoint);
        }

        private BoardItemPair GetObjectsUnderPoint(Point location)
        {
            bool foo;
            return GetObjectsUnderPoint(location, out foo);
        }

        private BoardItem GetObjectUnderPoint(Point location)
        {
            bool selectedItemHigher;
            BoardItemPair objsUnderPoint = GetObjectsUnderPoint(location, out selectedItemHigher);
            if (objsUnderPoint.SelectedItem == null && objsUnderPoint.NonSelectedItem == null) return null;
            else if (objsUnderPoint.SelectedItem == null) return objsUnderPoint.NonSelectedItem;
            else if (objsUnderPoint.NonSelectedItem == null) return objsUnderPoint.SelectedItem;
            else return selectedItemHigher ? objsUnderPoint.SelectedItem : objsUnderPoint.NonSelectedItem;
        }

        public static bool IsPointInsideRectangle(Point point, int left, int top, int right, int bottom)
        {
            if (bottom > point.Y && top < point.Y && left < point.X && right > point.X)
                return true;
            return false;
        }

        public void OnExportRequested()
        {
            if (ExportRequested != null)
                ExportRequested.Invoke();
        }

        public void OnLoadRequested()
        {
            if (LoadRequested != null)
                LoadRequested.Invoke();
        }

        public void OnCloseTabRequested()
        {
            if (CloseTabRequested != null)
                CloseTabRequested.Invoke();
        }

        public void OnSwitchTabRequested(bool reverse)
        {
            if (SwitchTabRequested != null)
                SwitchTabRequested.Invoke(this, reverse);
        }

        public delegate void LeftMouseDownDelegate(Board selectedBoard, BoardItem item, BoardItem selectedItem, Point realPosition, Point virtualPosition, bool selectedItemHigher);
        public event LeftMouseDownDelegate LeftMouseDown;

        public delegate void LeftMouseUpDelegate(Board selectedBoard, BoardItem item, BoardItem selectedItem, Point realPosition, Point virtualPosition, bool selectedItemHigher);
        public event LeftMouseUpDelegate LeftMouseUp;

        public delegate void RightMouseClickDelegate(Board selectedBoard, BoardItem target, Point realPosition, Point virtualPosition, MouseState mouseState);
        public event RightMouseClickDelegate RightMouseClick;

        public delegate void MouseDoubleClickDelegate(Board selectedBoard, BoardItem target, Point realPosition, Point virtualPosition);
        public new event MouseDoubleClickDelegate MouseDoubleClick; //"new" is to make VS shut up with it's warnings

        public delegate void ShortcutKeyPressedDelegate(Board selectedBoard, bool ctrl, bool shift, bool alt, System.Windows.Forms.Keys key);
        public event ShortcutKeyPressedDelegate ShortcutKeyPressed;

        public delegate void MouseMovedDelegate(Board selectedBoard, Point oldPos, Point newPos, Point currPhysicalPos);
        public event MouseMovedDelegate MouseMoved;

        public delegate void ImageDroppedDelegate(Board selectedBoard, System.Drawing.Bitmap bmp, string name, Point pos);
        public event ImageDroppedDelegate ImageDropped;

        public event HaCreator.GUI.HaRibbon.EmptyEvent ExportRequested;
        public event HaCreator.GUI.HaRibbon.EmptyEvent LoadRequested;
        public event HaCreator.GUI.HaRibbon.EmptyEvent CloseTabRequested;
        public event EventHandler<bool> SwitchTabRequested;
        public event HaCreator.GUI.HaRibbon.EmptyEvent BackupCheck;

        /// <summary>
        /// Mouse click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DxContainer_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            // We only handle right click here because left click is handled more thoroughly by up-down handlers
            if (e.Button == System.Windows.Forms.MouseButtons.Right && RightMouseClick != null)
            {
                Point realPosition = new Point(e.X, e.Y);
                lock (this)
                {
                    RightMouseClick(selectedBoard, GetObjectUnderPoint(realPosition), realPosition, new Point(PhysicalToVirtual(e.X, selectedBoard.CenterPoint.X, selectedBoard.hScroll, 0), PhysicalToVirtual(e.Y, selectedBoard.CenterPoint.Y, selectedBoard.vScroll, 0)), selectedBoard.Mouse.State);
                }
            }
        }

        /// <summary>
        /// Mouse double click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DxContainer_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left && MouseDoubleClick != null)
            {
                Point realPosition = new Point(e.X, e.Y);
                lock (this)
                {
                    MouseDoubleClick(selectedBoard, GetObjectUnderPoint(realPosition), realPosition, new Point(PhysicalToVirtual(e.X, selectedBoard.CenterPoint.X, selectedBoard.hScroll, 0), PhysicalToVirtual(e.Y, selectedBoard.CenterPoint.Y, selectedBoard.vScroll, 0)));
                }
            }
        }

        /// <summary>
        /// Mouse down
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DxContainer_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            // If the mouse has not been moved while we were in focus (e.g. when clicking on the editor while another window focused), this event will be sent without a mousemove event preceding it.
            // We will move it to its correct position by invoking the move event handler manually.
            if (selectedBoard.Mouse.X != e.X || selectedBoard.Mouse.Y != e.Y)
            {
                // No need to lock because MouseMove locks anyway
                DxContainer_MouseMove(sender, e);
            }

            selectedBoard.Mouse.IsDown = true;
            if (e.Button == System.Windows.Forms.MouseButtons.Left && LeftMouseDown != null)
            {
                bool selectedItemHigher;
                Point realPosition = new Point(e.X, e.Y);
                lock (this)
                {
                    BoardItemPair objsUnderMouse = GetObjectsUnderPoint(realPosition, out selectedItemHigher);
                    LeftMouseDown(selectedBoard, objsUnderMouse.NonSelectedItem, objsUnderMouse.SelectedItem, realPosition, new Point(PhysicalToVirtual(e.X, selectedBoard.CenterPoint.X, selectedBoard.hScroll, 0), PhysicalToVirtual(e.Y, selectedBoard.CenterPoint.Y, selectedBoard.vScroll, 0)), selectedItemHigher);
                }
            }
        }

        /// <summary>
        /// Mouse up
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DxContainer_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            selectedBoard.Mouse.IsDown = false;
            if (e.Button == System.Windows.Forms.MouseButtons.Left && LeftMouseUp != null)
            {
                Point realPosition = new Point(e.X, e.Y);
                bool selectedItemHigher;
                lock (this)
                {
                    BoardItemPair objsUnderMouse = GetObjectsUnderPoint(realPosition, out selectedItemHigher);
                    LeftMouseUp(selectedBoard, objsUnderMouse.NonSelectedItem, objsUnderMouse.SelectedItem, realPosition, new Point(PhysicalToVirtual(e.X, selectedBoard.CenterPoint.X, selectedBoard.hScroll, 0), PhysicalToVirtual(e.Y, selectedBoard.CenterPoint.Y, selectedBoard.vScroll, 0)), selectedItemHigher);
                }
            }
        }

        /// <summary>
        /// Key down
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void DxContainer_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            lock (this)
            {
                if (ShortcutKeyPressed != null)
                {
                    bool ctrl = (System.Windows.Forms.Control.ModifierKeys & System.Windows.Forms.Keys.Control) == System.Windows.Forms.Keys.Control;
                    bool alt = (System.Windows.Forms.Control.ModifierKeys & System.Windows.Forms.Keys.Alt) == System.Windows.Forms.Keys.Alt;
                    bool shift = (System.Windows.Forms.Control.ModifierKeys & System.Windows.Forms.Keys.Shift) == System.Windows.Forms.Keys.Shift;
                    System.Windows.Forms.Keys filteredKeys = e.KeyData;

                    if (ctrl && (filteredKeys & System.Windows.Forms.Keys.Control) != 0)
                        filteredKeys = filteredKeys ^ System.Windows.Forms.Keys.Control;
                    if (alt && (filteredKeys & System.Windows.Forms.Keys.Alt) != 0)
                        filteredKeys = filteredKeys ^ System.Windows.Forms.Keys.Alt;
                    if (shift && (filteredKeys & System.Windows.Forms.Keys.Shift) != 0)
                        filteredKeys = filteredKeys ^ System.Windows.Forms.Keys.Shift;
                    lock (this)
                    {
                        ShortcutKeyPressed(selectedBoard, ctrl, shift, alt, filteredKeys);
                    }
                }
            }
        }

        /// <summary>
        /// Mouse move
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DxContainer_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            lock (this)
            {
                Point realPosition = new Point(e.X, e.Y);

                if (VirtualToPhysical(selectedBoard.Mouse.X, selectedBoard.CenterPoint.X, selectedBoard.hScroll, 0) != realPosition.X 
                    || VirtualToPhysical(selectedBoard.Mouse.Y, selectedBoard.CenterPoint.Y, selectedBoard.vScroll, 0) != realPosition.Y)
                {
                    Point oldPos = new Point(selectedBoard.Mouse.X, selectedBoard.Mouse.Y);
                    Point newPos = new Point(PhysicalToVirtual(realPosition.X, selectedBoard.CenterPoint.X, selectedBoard.hScroll, 0), PhysicalToVirtual(realPosition.Y, selectedBoard.CenterPoint.Y, selectedBoard.vScroll, 0));
                    selectedBoard.Mouse.Move(newPos.X, newPos.Y);

                    if (MouseMoved != null)
                        MouseMoved.Invoke(selectedBoard, oldPos, newPos, new Point(realPosition.X, realPosition.Y));
                }
            }
        }

        /// <summary>
        /// Drag enter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DxContainer_DragEnter(object sender, System.Windows.Forms.DragEventArgs e)
        {
            lock (this)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    e.Effect = System.Windows.Forms.DragDropEffects.Copy;
                else
                    e.Effect = System.Windows.Forms.DragDropEffects.None;
            }
        }

        /// <summary>
        /// Drag drop
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DxContainer_DragDrop(object sender, System.Windows.Forms.DragEventArgs e)
        {
            lock (this)
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    return;
                }
                if (!AssertLayerSelected())
                    return;

                string[] data = (string[])e.Data.GetData(DataFormats.FileDrop);
                System.Windows.Point p = PointToScreen(new System.Windows.Point(e.X, e.Y));
                foreach (string file in data)
                {
                    System.Drawing.Bitmap bmp;
                    try
                    {
                        bmp = (System.Drawing.Bitmap)System.Drawing.Image.FromFile(file);
                    }
                    catch
                    {
                        continue;
                    }
                    if (ImageDropped != null)
                        ImageDropped.Invoke(selectedBoard, bmp, Path.GetFileNameWithoutExtension(file), 
                            new Point(PhysicalToVirtual((int) p.X, selectedBoard.CenterPoint.X, selectedBoard.hScroll, 0), PhysicalToVirtual((int) p.Y, selectedBoard.CenterPoint.Y, selectedBoard.vScroll, 0)));
                }
            }
        }
        #endregion

        #region Event Handlers
        private System.Windows.Point _mousePoint; // Initial point of drag
        public bool TriggerMouseWheel(System.Windows.Input.MouseEventArgs e, System.Windows.UIElement sender) // Were not overriding OnMouseWheel anymore because it's better to override it in mainform
        {
            lock (this)
            {
                if (!this.DeviceReady)
                    return false;

                System.Windows.Point newMousePoint = e.MouseDevice.GetPosition(sender);

                double oldvalue = vScrollBar.Value;
                var delta = _mousePoint - newMousePoint;
                double scrollValue = (delta.Length / 10) * vScrollBar.LargeChange;

                this._mousePoint = newMousePoint;

                if (vScrollBar.Value - scrollValue < vScrollBar.Minimum)
                    vScrollBar.Value = vScrollBar.Minimum;
                else if (vScrollBar.Value - scrollValue > vScrollBar.Maximum)
                    vScrollBar.Value = vScrollBar.Maximum;
                else
                    vScrollBar.Value -= (int)scrollValue;
                vScrollBar_Scroll(null, null);

                return true;
            }
        }


        public void AdjustScrollBars()
        {
            lock (this)
            {
                if (MapSize.X > _CurrentDXWindowSize.Width)
                {
                    hScrollBar.IsEnabled = true;
                    hScrollBar.Maximum = MapSize.X - _CurrentDXWindowSize.Width;
                    hScrollBar.Minimum = 0;
                    if (hScrollBar.Maximum < selectedBoard.hScroll)
                    {
                        hScrollBar.Value = hScrollBar.Maximum - 1;
                        selectedBoard.hScroll = (int)hScrollBar.Value;
                    }
                    else
                    {
                        hScrollBar.Value = selectedBoard.hScroll;
                    }
                }
                else
                {
                    hScrollBar.IsEnabled = false;
                    hScrollBar.Value = 0;
                    hScrollBar.Maximum = 0;
                }

                if (MapSize.Y > _CurrentDXWindowSize.Height)
                {
                    vScrollBar.IsEnabled = true;
                    vScrollBar.Maximum = MapSize.Y - _CurrentDXWindowSize.Height;
                    vScrollBar.Minimum = 0;
                    if (vScrollBar.Maximum < selectedBoard.vScroll)
                    {
                        vScrollBar.Value = vScrollBar.Maximum - 1;
                        selectedBoard.vScroll = (int) vScrollBar.Value;
                    }
                    else
                    {
                        vScrollBar.Value = selectedBoard.vScroll;
                    }
                }
                else
                {
                    vScrollBar.IsEnabled = false;
                    vScrollBar.Value = 0;
                    vScrollBar.Maximum = 0;
                }
            }
        }


        private void ResetDevice()
        {
            // Note that this function has to be thread safe - it is called from the renderer thread
            /*if (form.WindowState == FormWindowState.Minimized) 
                return;*/
            pParams.BackBufferWidth = _CurrentDXWindowSize.Width;
            pParams.BackBufferHeight = _CurrentDXWindowSize.Height;
            DxDevice.Reset(pParams);
        }

        public void SetHScrollbarValue(int value)
        {
            lock (this)
            {
                hScrollBar.Value = value;
            }
        }

        public void SetVScrollbarValue(int value)
        {
            lock (this)
            {
                vScrollBar.Value = value;
            }
        }

        /// <summary>
        /// Vertical scroll bar
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void vScrollBar_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
        {
            lock (this)
            {
                selectedBoard.vScroll = (int) vScrollBar.Value;
            }
        }

        /// <summary>
        /// Horizontal scroll bar
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void hScrollBar_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
        {
            lock (this)
            {
                selectedBoard.hScroll = (int) hScrollBar.Value;
            }
        }
        #endregion

        #region Events
        public delegate void UndoRedoDelegate();
        public event UndoRedoDelegate OnUndoListChanged;
        public event UndoRedoDelegate OnRedoListChanged;

        public delegate void LayerTSChangedDelegate(Layer layer);
        public event LayerTSChangedDelegate OnLayerTSChanged;

        public delegate void MenuItemClickedDelegate(BoardItem item);
        public event MenuItemClickedDelegate OnEditInstanceClicked;
        public event MenuItemClickedDelegate OnEditBaseClicked;
        public event MenuItemClickedDelegate OnSendToBackClicked;
        public event MenuItemClickedDelegate OnBringToFrontClicked;

        public delegate void ReturnToSelectionStateDelegate();
        public event ReturnToSelectionStateDelegate ReturnToSelectionState;

        public delegate void SelectedItemChangedDelegate(BoardItem selectedItem);
        public event SelectedItemChangedDelegate SelectedItemChanged;

        public event EventHandler BoardRemoved;
        public event EventHandler<bool> MinimapStateChanged;

        public void OnSelectedItemChanged(BoardItem selectedItem)
        {
            if (SelectedItemChanged != null) SelectedItemChanged.Invoke(selectedItem);
        }

        public void InvokeReturnToSelectionState()
        {
            if (ReturnToSelectionState != null) ReturnToSelectionState.Invoke();
        }

        public void SendToBackClicked(BoardItem item)
        {
            if (OnSendToBackClicked != null) OnSendToBackClicked.Invoke(item);
        }

        public void BringToFrontClicked(BoardItem item)
        {
            if (OnBringToFrontClicked != null) OnBringToFrontClicked.Invoke(item);
        }

        public void EditInstanceClicked(BoardItem item)
        {
            if (OnEditInstanceClicked != null) OnEditInstanceClicked.Invoke(item);
        }

        public void EditBaseClicked(BoardItem item)
        {
            if (OnEditBaseClicked != null) OnEditBaseClicked.Invoke(item);
        }

        public void LayerTSChanged(Layer layer)
        {
            if (OnLayerTSChanged != null) OnLayerTSChanged.Invoke(layer);
        }

        public void UndoListChanged()
        {
            if (OnUndoListChanged != null) OnUndoListChanged.Invoke();
        }

        public void RedoListChanged()
        {
            if (OnRedoListChanged != null) OnRedoListChanged.Invoke();
        }
        #endregion

        #region Static Settings
        private const int ScrollbarWidth = 16;

        public static float FirstSnapVerification;
        public static Color InactiveColor;
        public static Color RopeInactiveColor;
        public static Color FootholdInactiveColor;
        public static Color ChairInactiveColor;
        public static Color ToolTipInactiveColor;
        public static Color MiscInactiveColor;
        public static Color VRInactiveColor;
        public static Color MinimapBoundInactiveColor;

        static MultiBoard()
        {
            RecalculateSettings();
        }

        public static Color CreateTransparency(Color orgColor, int alpha)
        {
            return new Color(orgColor.R, orgColor.B, orgColor.G, alpha);
        }

        public static void RecalculateSettings()
        {
            int alpha = UserSettings.NonActiveAlpha;
            FirstSnapVerification = UserSettings.SnapDistance * 20;
            InactiveColor = CreateTransparency(Color.White, alpha);
            RopeInactiveColor = CreateTransparency(UserSettings.RopeColor, alpha);
            FootholdInactiveColor = CreateTransparency(UserSettings.FootholdColor, alpha);
            ChairInactiveColor = CreateTransparency(UserSettings.ChairColor, alpha);
            ToolTipInactiveColor = CreateTransparency(UserSettings.ToolTipColor, alpha);
            MiscInactiveColor = CreateTransparency(UserSettings.MiscColor, alpha);
            VRInactiveColor = CreateTransparency(UserSettings.VRColor, alpha);
            MinimapBoundInactiveColor = CreateTransparency(UserSettings.MinimapBoundColor, alpha);
        }
        #endregion
    }
}
