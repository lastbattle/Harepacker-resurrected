using HaRepacker.Comparer;
using HaRepacker.Converter;
using HaRepacker.GUI.Input;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace HaRepacker.GUI.Panels
{
    /// <summary>
    /// Interaction logic for MainPanelXAML.xaml
    /// </summary>
    public partial class MainPanel : UserControl
    {
        private static List<WzObject> clipboard = new List<WzObject>();
        private UndoRedoManager undoRedoMan;

        private bool isSelectingWzMapFieldLimit = false;


        public MainPanel()
        {
            InitializeComponent();

            // undo redo
            undoRedoMan = new UndoRedoManager(this);

            nameBox.Header = "Key";
            textPropBox.Header = "Value";
            textPropBox.ButtonClicked += applyChangesButton_Click;

            vectorPanel.ButtonClicked += VectorPanel_ButtonClicked;

            textPropBox.Visibility = Visibility.Collapsed;
            //nameBox.Visibility = Visibility.Collapsed;

            // Storyboard
            System.Windows.Media.Animation.Storyboard sbb = (System.Windows.Media.Animation.Storyboard)(this.FindResource("Storyboard_Find_FadeIn"));
            sbb.Completed += Storyboard_Find_FadeIn_Completed;

            // buttons
            button_animateSelectedCanvas.Visibility = Visibility.Collapsed;
            changeImageButton.Visibility = Visibility.Collapsed;
            changeSoundButton.Visibility = Visibility.Collapsed;
            saveSoundButton.Visibility = Visibility.Collapsed;
            saveImageButton.Visibility = Visibility.Collapsed;

            Loaded += MainPanelXAML_Loaded;
        }


        private void MainPanelXAML_Loaded(object sender, RoutedEventArgs e)
        {
            this.fieldLimitPanel1.SetTextboxOnFieldLimitChange(textPropBox);

            // Set theme color
            if (Program.ConfigurationManager.UserSettings.ThemeColor == 0)
            {
                VisualStateManager.GoToState(this , "BlackTheme", false);
                DataTree.BackColor = System.Drawing.Color.Black;
                DataTree.ForeColor = System.Drawing.Color.White;
            }
        }

        #region Exported Fields
        public UndoRedoManager UndoRedoMan { get { return undoRedoMan; } }

        #endregion

        #region Data Tree
        private void DataTree_DoubleClick(object sender, EventArgs e)
        {
            if (DataTree.SelectedNode != null && DataTree.SelectedNode.Tag is WzImage && DataTree.SelectedNode.Nodes.Count == 0)
            {
                ParseOnDataTreeSelectedItem(((WzNode)DataTree.SelectedNode));
            }
        }

        private void DataTree_AfterSelect(object sender, System.Windows.Forms.TreeViewEventArgs e)
        {
            if (DataTree.SelectedNode == null)
            {
                return;
            }

            ShowObjectValue((WzObject)DataTree.SelectedNode.Tag);
            selectionLabel.Text = string.Format(Properties.Resources.SelectionType, ((WzNode)DataTree.SelectedNode).GetTypeName());
        }

        /// <summary>
        /// Parse the data tree selected item on double clicking, or copy pasting into it.
        /// </summary>
        /// <param name="selectedNode"></param>
        private static void ParseOnDataTreeSelectedItem(WzNode selectedNode)
        {
            if (!((WzImage)selectedNode.Tag).Parsed)
                ((WzImage)selectedNode.Tag).ParseImage();
            selectedNode.Reparse();
            selectedNode.Expand();
        }

        private void DataTree_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (!DataTree.Focused) return;
            bool ctrl = (System.Windows.Forms.Control.ModifierKeys & System.Windows.Forms.Keys.Control) == System.Windows.Forms.Keys.Control;
            bool alt = (System.Windows.Forms.Control.ModifierKeys & System.Windows.Forms.Keys.Alt) == System.Windows.Forms.Keys.Alt;
            bool shift = (System.Windows.Forms.Control.ModifierKeys & System.Windows.Forms.Keys.Shift) == System.Windows.Forms.Keys.Shift;
            System.Windows.Forms.Keys filteredKeys = e.KeyData;
            if (ctrl) filteredKeys = filteredKeys ^ System.Windows.Forms.Keys.Control;
            if (alt) filteredKeys = filteredKeys ^ System.Windows.Forms.Keys.Alt;
            if (shift) filteredKeys = filteredKeys ^ System.Windows.Forms.Keys.Shift;

            switch (filteredKeys)
            {
                case System.Windows.Forms.Keys.F5:
                    StartAnimateSelectedCanvas();
                    break;
                case System.Windows.Forms.Keys.Escape:
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    StopCanvasAnimation();
                    break;

                case System.Windows.Forms.Keys.Delete:
                    e.Handled = true;
                    e.SuppressKeyPress = true;

                    PromptRemoveSelectedTreeNodes();
                    break;
            }
            if (ctrl)
            {
                switch (filteredKeys)
                {
                    case System.Windows.Forms.Keys.R: // Render map        
                        //HaRepackerMainPanel.

                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        break;
                    case System.Windows.Forms.Keys.C:
                        DoCopy();
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        break;
                    case System.Windows.Forms.Keys.V:
                        DoPaste();
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        break;
                    case System.Windows.Forms.Keys.F: // open search box
                        if (grid_FindPanel.Visibility == Visibility.Collapsed)
                        {
                            System.Windows.Media.Animation.Storyboard sbb = (System.Windows.Media.Animation.Storyboard)(this.FindResource("Storyboard_Find_FadeIn"));
                            sbb.Begin();

                            e.Handled = true;
                            e.SuppressKeyPress = true;
                        }
                        break;
                    case System.Windows.Forms.Keys.T:
                    case System.Windows.Forms.Keys.O:
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        break;
                }
            }
        }
        #endregion

        #region Image directory add
        /// <summary>
        /// WzDirectory
        /// </summary>
        /// <param name="target"></param>
        public void AddWzDirectoryToSelectedNode(System.Windows.Forms.TreeNode target)
        {
            if (!(target.Tag is WzDirectory) && !(target.Tag is WzFile))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            string name;
            if (!NameInputBox.Show(Properties.Resources.MainAddDir, 0, out name))
                return;

            bool added = false;

            WzObject obj = (WzObject)target.Tag;
            while (obj is WzFile || ((obj = obj.Parent) is WzFile))
            {
                WzFile topMostWzFileParent = (WzFile)obj;

                ((WzNode)target).AddObject(new WzDirectory(name, topMostWzFileParent), UndoRedoMan);
                added = true;
                break;
            }
            if (!added)
            {
                MessageBox.Show(Properties.Resources.MainTreeAddDirError);
            }
        }

        /// <summary>
        /// WzDirectory
        /// </summary>
        /// <param name="target"></param>
        public void AddWzImageToSelectedNode(System.Windows.Forms.TreeNode target)
        {
            string name;
            if (!(target.Tag is WzDirectory) && !(target.Tag is WzFile))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!NameInputBox.Show(Properties.Resources.MainAddImg, 0, out name))
                return;
            ((WzNode)target).AddObject(new WzImage(name) { Changed = true }, UndoRedoMan);
        }

        /// <summary>
        /// WzByteProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzByteFloatToSelectedNode(System.Windows.Forms.TreeNode target)
        {
            string name;
            double? d;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!FloatingPointInputBox.Show(Properties.Resources.MainAddFloat, out name, out d))
                return;
            ((WzNode)target).AddObject(new WzFloatProperty(name, (float)d), UndoRedoMan);
        }

        /// <summary>
        /// WzCanvasProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzCanvasToSelectedNode(System.Windows.Forms.TreeNode target)
        {
            string name;
            List<System.Drawing.Bitmap> bitmaps = new List<System.Drawing.Bitmap>();
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!BitmapInputBox.Show(Properties.Resources.MainAddCanvas, out name, out bitmaps))
                return;

            WzNode wzNode = ((WzNode)target);

            int i = 0;
            foreach (System.Drawing.Bitmap bmp in bitmaps)
            {
                WzCanvasProperty canvas = new WzCanvasProperty(bitmaps.Count == 1 ? name : (name + i));
                WzPngProperty pngProperty = new WzPngProperty();
                pngProperty.SetPNG(bmp);
                canvas.PngProperty = pngProperty;

                WzNode newInsertedNode = wzNode.AddObject(canvas, UndoRedoMan);
                // Add an additional WzVectorProperty with X Y of 0,0
                newInsertedNode.AddObject(new WzVectorProperty(WzCanvasProperty.OriginPropertyName, new WzIntProperty("X", 0), new WzIntProperty("Y", 0)), UndoRedoMan);

                i++;
            }
        }

        /// <summary>
        /// WzCompressedInt
        /// </summary>
        /// <param name="target"></param>
        public void AddWzCompressedIntToSelectedNode(System.Windows.Forms.TreeNode target)
        {
            string name;
            int? value;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!IntInputBox.Show(Properties.Resources.MainAddInt, out name, out value))
                return;
            ((WzNode)target).AddObject(new WzIntProperty(name, (int)value), UndoRedoMan);
        }

        /// <summary>
        /// WzLongProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzLongToSelectedNode(System.Windows.Forms.TreeNode target)
        {
            string name;
            long? value;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!LongInputBox.Show(Properties.Resources.MainAddInt, out name, out value))
                return;
            ((WzNode)target).AddObject(new WzLongProperty(name, (long)value), UndoRedoMan);
        }

        /// <summary>
        /// WzConvexProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzConvexPropertyToSelectedNode(System.Windows.Forms.TreeNode target)
        {
            string name;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!NameInputBox.Show(Properties.Resources.MainAddConvex, 0, out name))
                return;
            ((WzNode)target).AddObject(new WzConvexProperty(name), UndoRedoMan);
        }

        /// <summary>
        /// WzNullProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzDoublePropertyToSelectedNode(System.Windows.Forms.TreeNode target)
        {
            string name;
            double? d;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!FloatingPointInputBox.Show(Properties.Resources.MainAddDouble, out name, out d))
                return;
            ((WzNode)target).AddObject(new WzDoubleProperty(name, (double)d), UndoRedoMan);
        }

        /// <summary>
        /// WzNullProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzNullPropertyToSelectedNode(System.Windows.Forms.TreeNode target)
        {
            string name;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!NameInputBox.Show(Properties.Resources.MainAddNull, 0, out name))
                return;
            ((WzNode)target).AddObject(new WzNullProperty(name), UndoRedoMan);
        }

        /// <summary>
        /// WzSoundProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzSoundPropertyToSelectedNode(System.Windows.Forms.TreeNode target)
        {
            string name;
            string path;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!SoundInputBox.Show(Properties.Resources.MainAddSound, out name, out path))
                return;
            ((WzNode)target).AddObject(new WzSoundProperty(name, path), UndoRedoMan);
        }

        /// <summary>
        /// WzStringProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzStringPropertyToSelectedIndex(System.Windows.Forms.TreeNode target)
        {
            string name;
            string value;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!NameValueInputBox.Show(Properties.Resources.MainAddString, out name, out value))
                return;
            ((WzNode)target).AddObject(new WzStringProperty(name, value), UndoRedoMan);
        }

        /// <summary>
        /// WzSubProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzSubPropertyToSelectedIndex(System.Windows.Forms.TreeNode target)
        {
            string name;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!NameInputBox.Show(Properties.Resources.MainAddSub, 0, out name))
                return;
            ((WzNode)target).AddObject(new WzSubProperty(name), UndoRedoMan);
        }

        /// <summary>
        /// WzUnsignedShortProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzUnsignedShortPropertyToSelectedIndex(System.Windows.Forms.TreeNode target)
        {
            string name;
            int? value;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!IntInputBox.Show(Properties.Resources.MainAddShort, out name, out value))
                return;
            ((WzNode)target).AddObject(new WzShortProperty(name, (short)value), UndoRedoMan);
        }

        /// <summary>
        /// WzUOLProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzUOLPropertyToSelectedIndex(System.Windows.Forms.TreeNode target)
        {
            string name;
            string value;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!NameValueInputBox.Show(Properties.Resources.MainAddLink, out name, out value))
                return;
            ((WzNode)target).AddObject(new WzUOLProperty(name, value), UndoRedoMan);
        }

        /// <summary>
        /// WzVectorProperty
        /// </summary>
        /// <param name="target"></param>
        public void AddWzVectorPropertyToSelectedIndex(System.Windows.Forms.TreeNode target)
        {
            string name;
            System.Drawing.Point? pt;
            if (!(target.Tag is IPropertyContainer))
            {
                Warning.Error(Properties.Resources.MainCannotInsertToNode);
                return;
            }
            else if (!VectorInputBox.Show(Properties.Resources.MainAddVec, out name, out pt))
                return;
            ((WzNode)target).AddObject(new WzVectorProperty(name, new WzIntProperty("X", ((System.Drawing.Point)pt).X), new WzIntProperty("Y", ((System.Drawing.Point)pt).Y)), UndoRedoMan);
        }

        /// <summary>
        /// Remove selected nodes
        /// </summary>
        public void PromptRemoveSelectedTreeNodes()
        {
            if (!Warning.Warn(Properties.Resources.MainConfirmRemove))
            {
                return;
            }

            List<UndoRedoAction> actions = new List<UndoRedoAction>();

            System.Windows.Forms.TreeNode[] nodeArr = new System.Windows.Forms.TreeNode[DataTree.SelectedNodes.Count];
            DataTree.SelectedNodes.CopyTo(nodeArr, 0);

            foreach (WzNode node in nodeArr)
                if (!(node.Tag is WzFile) && node.Parent != null)
                {
                    actions.Add(UndoRedoManager.ObjectRemoved((WzNode)node.Parent, node));
                    node.Delete();
                }
            UndoRedoMan.AddUndoBatch(actions);
        }

        /// <summary>
        /// Rename an individual node
        /// </summary>
        public void PromptRenameSelectedTreeNode()
        {
            if (DataTree.SelectedNodes.Count != 1)
            {
                return;
            }

            string newName = "";
            System.Windows.Forms.TreeNode currentSelectedNode = DataTree.SelectedNodes[0] as System.Windows.Forms.TreeNode;
            WzNode wzNode = (WzNode)currentSelectedNode;

            if (RenameInputBox.Show(Properties.Resources.MainConfirmRename, wzNode.Text, out newName))
            {
                wzNode.ChangeName(newName);
            }
        }
        #endregion

        #region Animate
        /// <summary>
        /// On button click for animating canvas
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_animateSelectedCanvas_Click_1(object sender, RoutedEventArgs e)
        {
            StartAnimateSelectedCanvas();
        }

        private DispatcherTimer timerImgSequence;
        private int i_animateCanvasNode = 0;
        private bool bCanvasAnimationActive = false;
        private List<Tuple<string, int, System.Drawing.PointF, ImageSource>> animate_PreLoadImages = new List<Tuple<string, int, System.Drawing.PointF, ImageSource>>(); // list of pre-loaded images for animation [Image name, delay, origin, image]
        /// <summary>
        /// Animate the list of selected canvases
        /// </summary>
        public void StartAnimateSelectedCanvas()
        {
            if (timerImgSequence != null)
            {
                timerImgSequence.Stop();
                timerImgSequence = null;
            }
            timerImgSequence = new DispatcherTimer();
            timerImgSequence.Interval = new TimeSpan(0, 0, 0, 0, Program.TimeStartAnimateDefault);
            timerImgSequence.Tick += TimerImgSequence_Tick;

            if (bCanvasAnimationActive) // currently animating
            {
                StopCanvasAnimation();
            }
            else if (DataTree.SelectedNodes.Count >= 1)
            {
                List<Tuple<string, int, System.Drawing.PointF, ImageSource>> load_animate_PreLoadImages = new List<Tuple<string, int, System.Drawing.PointF, ImageSource>>();

                // Check all selected nodes, make sure they're all images.
                // and add to a list
                bool loadSuccessfully = true;
                string loadErrorMsg = null;

                foreach (WzNode selNode in DataTree.SelectedNodes)
                {
                    WzObject obj = (WzObject)selNode.Tag;

                    WzCanvasProperty canvasProperty;

                    bool isUOLProperty = obj is WzUOLProperty;
                    if (obj is WzCanvasProperty || isUOLProperty)
                    {
                        // Get image property
                        System.Drawing.Bitmap image;
                        if (!isUOLProperty)
                        {
                            canvasProperty = ((WzCanvasProperty)obj);
                            image = canvasProperty.GetLinkedWzCanvasBitmap();
                        }
                        else
                        {
                            WzObject linkVal = ((WzUOLProperty)obj).LinkValue;
                            if (linkVal is WzCanvasProperty)
                            {
                                canvasProperty = ((WzCanvasProperty)linkVal);
                                image = canvasProperty.GetLinkedWzCanvasBitmap();
                            }
                            else
                            { // one of the WzUOLProperty link data isnt a canvas
                                loadSuccessfully = false;
                                loadErrorMsg = "Error loading WzUOLProperty ID: " + obj.Name;
                                break;
                            }
                        }

                        // Get delay property
                        int? delay = canvasProperty[WzCanvasProperty.AnimationDelayPropertyName]?.GetInt();
                        if (delay == null)
                            delay = 0;

                        System.Drawing.PointF origin = canvasProperty.GetCanvasVectorPosition();

                        // Add to the list of images to render
                        load_animate_PreLoadImages.Add(new Tuple<string, int, System.Drawing.PointF, ImageSource>(obj.Name, (int)delay, origin, BitmapToImageSource.ToWpfBitmap(image)));
                    }
                    else
                    {
                        loadSuccessfully = false;
                        loadErrorMsg = "One of the selected nodes is not a WzCanvasProperty type";
                        break;
                    }
                }

                if (!loadSuccessfully)
                {
                    MessageBox.Show(loadErrorMsg, "Animate", MessageBoxButton.OK);
                }
                else
                {
                    if (animate_PreLoadImages.Count > 0) // clear existing images
                        animate_PreLoadImages.Clear();

                    // Sort by image name
                    IOrderedEnumerable<Tuple<string, int, System.Drawing.PointF, ImageSource>> sorted = load_animate_PreLoadImages.OrderBy(x => x, new SemiNumericComparer());
                    animate_PreLoadImages.Clear(); // clear existing
                    animate_PreLoadImages.AddRange(sorted);

                    // Start animation
                    bCanvasAnimationActive = true; // flag

                    timerImgSequence.Start();
                    button_animateSelectedCanvas.Content = "Stop";
                }
            }
            else
            {
                MessageBox.Show("Select two or more nodes WzCanvasProperty", "Selection", MessageBoxButton.OK);
            }
        }

        /// <summary>
        /// Animate canvas timer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TimerImgSequence_Tick(object sender, EventArgs e)
        {
            timerImgSequence.Stop();

            if (i_animateCanvasNode >= animate_PreLoadImages.Count) // last animate node, reset to 0 next
            {
                i_animateCanvasNode = 0;
            }

            Tuple<string, int, System.Drawing.PointF, ImageSource> currentNode = animate_PreLoadImages[i_animateCanvasNode];
            i_animateCanvasNode++; // increment 1

            // Set vector origin
            vectorOriginSelected = currentNode.Item3;
            RefreshCanvasLocation();

            // Set current image
            canvasPropBox.Source = currentNode.Item4;

            // Set tooltip text
            if (i_animateCanvasNode == animate_PreLoadImages.Count)
                statusBarItemLabel_Others.Text = "# " + currentNode.Item1 + ", Delay: " + currentNode.Item2 + " ms. Repeating Animate.";
            else
                statusBarItemLabel_Others.Text = "# " + currentNode.Item1 + ", Delay: " + currentNode.Item2 + " ms.";

            timerImgSequence.Interval = new TimeSpan(0, 0, 0, 0, currentNode.Item2);
            timerImgSequence.Start();
        }

        /// <summary>
        /// Stop animating canvases
        /// </summary>
        public void StopCanvasAnimation()
        {
            i_animateCanvasNode = 0;

            if (timerImgSequence != null)
            {
                timerImgSequence.Stop();
                timerImgSequence = null;
            }
            button_animateSelectedCanvas.Content = "Animate (F5)";

            // clear memory
            animate_PreLoadImages.Clear();

            bCanvasAnimationActive = false; // flag
        }

        private void cartesianPlane_checkBox_CheckedChanged(object sender, EventArgs e)
        {
            /*    if (cartesianPlane_checkBox.Checked)
                    Program.ConfigurationManager.UserSettings.Plane = true;
                else
                    Program.ConfigurationManager.UserSettings.Plane = false;

                //cartesianPlaneX.Visible = UserSettings.Plane;
                //cartesianPlaneY.Visible = UserSettings.Plane;*/
        }

        private void nextLoopTime_comboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            /* if (nextLoopTime_comboBox == null)
                  return;

              switch (nextLoopTime_comboBox.SelectedIndex)
              {
                  case 1:
                      Program.ConfigurationManager.UserSettings.DelayNextLoop = 1000;
                      break;
                  case 2:
                      Program.ConfigurationManager.UserSettings.DelayNextLoop = 2000;
                      break;
                  case 3:
                      Program.ConfigurationManager.UserSettings.DelayNextLoop = 5000;
                      break;
                  case 4:
                      Program.ConfigurationManager.UserSettings.DelayNextLoop = 10000;
                      break;
                  default:
                      Program.ConfigurationManager.UserSettings.DelayNextLoop = Program.TimeStartAnimateDefault;
                      break;
              }*/
        }


        private System.Drawing.PointF vectorOriginSelected;
        private void RefreshCanvasLocation()
        {
            if (Program.ConfigurationManager.UserSettings.DevImgSequences && vectorOriginSelected != null)
            {
            }
        }
        #endregion

        #region Buttons
        private void nameBox_ButtonClicked(object sender, EventArgs e)
        {
            if (DataTree.SelectedNode == null) return;
            if (DataTree.SelectedNode.Tag is WzFile)
            {
                ((WzFile)DataTree.SelectedNode.Tag).Header.Copyright = nameBox.Text;
                ((WzFile)DataTree.SelectedNode.Tag).Header.RecalculateFileStart();
            }
            else if (WzNode.CanNodeBeInserted((WzNode)DataTree.SelectedNode.Parent, nameBox.Text))
            {
                string text = nameBox.Text;
                ((WzNode)DataTree.SelectedNode).ChangeName(text);
                nameBox.Text = text;
                nameBox.ButtonEnabled = false;
            }
            else
                Warning.Error(Properties.Resources.MainNodeExists);

        }

        /// <summary>
        /// On vector panel 'apply' button clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VectorPanel_ButtonClicked(object sender, EventArgs e)
        {
            applyChangesButton_Click(null, null);
        }

        private void applyChangesButton_Click(object sender, EventArgs e)
        {
            if (DataTree.SelectedNode == null)
                return;
            WzObject obj = (WzObject)DataTree.SelectedNode.Tag;
            if (obj is WzImageProperty)
                ((WzImageProperty)obj).ParentImage.Changed = true;
            if (obj is WzVectorProperty)
            {
                ((WzVectorProperty)obj).X.Value = vectorPanel.X;
                ((WzVectorProperty)obj).Y.Value = vectorPanel.Y;
            }
            else if (obj is WzStringProperty)
                ((WzStringProperty)obj).Value = textPropBox.Text;
            else if (obj is WzFloatProperty)
            {
                float val;
                if (!float.TryParse(textPropBox.Text, out val))
                {
                    Warning.Error(string.Format(Properties.Resources.MainConversionError, textPropBox.Text));
                    return;
                }
                ((WzFloatProperty)obj).Value = val;
            }
            else if (obj is WzIntProperty)
            {
                int val;
                if (!int.TryParse(textPropBox.Text, out val))
                {
                    Warning.Error(string.Format(Properties.Resources.MainConversionError, textPropBox.Text));
                    return;
                }
                ((WzIntProperty)obj).Value = val;
            }
            else if (obj is WzDoubleProperty)
            {
                double val;
                if (!double.TryParse(textPropBox.Text, out val))
                {
                    Warning.Error(string.Format(Properties.Resources.MainConversionError, textPropBox.Text));
                    return;
                }
                ((WzDoubleProperty)obj).Value = val;
            }
            else if (obj is WzShortProperty)
            {
                short val;
                if (!short.TryParse(textPropBox.Text, out val))
                {
                    Warning.Error(string.Format(Properties.Resources.MainConversionError, textPropBox.Text));
                    return;
                }
                ((WzShortProperty)obj).Value = val;
            }
            else if (obj is WzUOLProperty)
            {
                ((WzUOLProperty)obj).Value = textPropBox.Text;
            }
        }

        /// <summary>
        /// Save sound 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void saveSoundButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(DataTree.SelectedNode.Tag is WzSoundProperty))
                return;
            WzSoundProperty mp3 = (WzSoundProperty)DataTree.SelectedNode.Tag;

            System.Windows.Forms.SaveFileDialog dialog = new System.Windows.Forms.SaveFileDialog()
            {
                Title = "Select where to save the image...",
                Filter = "Moving Pictures Experts Group Format 1 Audio Layer 3 (*.mp3)|*.mp3"
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            mp3.SaveToFile(dialog.FileName);
        }

        /// <summary>
        /// Saving the image from WzCanvasProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void saveImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(DataTree.SelectedNode.Tag is WzCanvasProperty) && !(DataTree.SelectedNode.Tag is WzUOLProperty))
            {
                return;
            }

            System.Drawing.Bitmap wzCanvasPropertyObjLocation = null;

            if (DataTree.SelectedNode.Tag is WzCanvasProperty)
                wzCanvasPropertyObjLocation = ((WzCanvasProperty)DataTree.SelectedNode.Tag).GetLinkedWzCanvasBitmap();
            else
            {
                WzObject linkValue = ((WzUOLProperty)DataTree.SelectedNode.Tag).LinkValue;
                if (linkValue is WzCanvasProperty)
                {
                    wzCanvasPropertyObjLocation = ((WzCanvasProperty)linkValue).GetLinkedWzCanvasBitmap();
                }
                else
                    return;
            }
            if (wzCanvasPropertyObjLocation == null)
                return; // oops, we're fucked lulz

            System.Windows.Forms.SaveFileDialog dialog = new System.Windows.Forms.SaveFileDialog()
            {
                Title = "Select where to save the image...",
                Filter = "Portable Network Grpahics (*.png)|*.png|CompuServe Graphics Interchange Format (*.gif)|*.gif|Bitmap (*.bmp)|*.bmp|Joint Photographic Experts Group Format (*.jpg)|*.jpg|Tagged Image File Format (*.tif)|*.tif"
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            switch (dialog.FilterIndex)
            {
                case 1: //png
                    wzCanvasPropertyObjLocation.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Png);
                    break;
                case 2: //gif
                    wzCanvasPropertyObjLocation.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Gif);
                    break;
                case 3: //bmp
                    wzCanvasPropertyObjLocation.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Bmp);
                    break;
                case 4: //jpg
                    wzCanvasPropertyObjLocation.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Jpeg);
                    break;
                case 5: //tiff
                    wzCanvasPropertyObjLocation.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Tiff);
                    break;
            }
        }

        /// <summary>
        /// Changing the image of WzCanvasProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void changeImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataTree.SelectedNode.Tag is WzCanvasProperty)
            {
                System.Windows.Forms.OpenFileDialog dialog = new System.Windows.Forms.OpenFileDialog()
                {
                    Title = "Select the image",
                    Filter = "Supported Image Formats (*.png;*.bmp;*.jpg;*.gif;*.jpeg;*.tif;*.tiff)|*.png;*.bmp;*.jpg;*.gif;*.jpeg;*.tif;*.tiff"
                };
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                System.Windows.Media.Imaging.BitmapImage bmp;
                try
                {
                    bmp = new BitmapImage(new Uri(dialog.FileName));
                }
                catch
                {
                    Warning.Error(Properties.Resources.MainImageLoadError);
                    return;
                }
                //List<UndoRedoAction> actions = new List<UndoRedoAction>(); // Undo action

                WzCanvasProperty selectedWzCanvas = (WzCanvasProperty)DataTree.SelectedNode.Tag;
                if (selectedWzCanvas.HaveInlinkProperty()) // if its an inlink property, remove that before updating base image.
                {
                    selectedWzCanvas.RemoveProperty(selectedWzCanvas[WzCanvasProperty.InlinkPropertyName]);

                    WzNode parentCanvasNode = (WzNode)DataTree.SelectedNode;
                    WzNode childInlinkNode = WzNode.GetChildNode(parentCanvasNode, WzCanvasProperty.InlinkPropertyName);

                    // Add undo actions
                    //actions.Add(UndoRedoManager.ObjectRemoved((WzNode)parentCanvasNode, childInlinkNode));
                    childInlinkNode.Delete(); // Delete '_inlink' node
                }
                else if (selectedWzCanvas.HaveOutlinkProperty()) // if its an inlink property, remove that before updating base image.
                {
                    selectedWzCanvas.RemoveProperty(selectedWzCanvas[WzCanvasProperty.OutlinkPropertyName]);

                    WzNode parentCanvasNode = (WzNode)DataTree.SelectedNode;
                    WzNode childInlinkNode = WzNode.GetChildNode(parentCanvasNode, WzCanvasProperty.OutlinkPropertyName);

                    // Add undo actions
                    //actions.Add(UndoRedoManager.ObjectRemoved((WzNode)parentCanvasNode, childInlinkNode));
                }
                else
                {

                }
                System.Drawing.Bitmap image = BitmapToImageSource.ToWinFormsBitmap(bmp);

                selectedWzCanvas.PngProperty.SetPNG(image);

                // Updates
                selectedWzCanvas.ParentImage.Changed = true;
                canvasPropBox.Source = bmp;

                // Add undo actions
                //UndoRedoMan.AddUndoBatch(actions);
            }
        }

        private void changeSoundButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataTree.SelectedNode.Tag is WzSoundProperty)
            {
                System.Windows.Forms.OpenFileDialog dialog = new System.Windows.Forms.OpenFileDialog()
                {
                    Title = "Select the sound",
                    Filter = "Moving Pictures Experts Group Format 1 Audio Layer 3(*.mp3)|*.mp3"
                };
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                WzSoundProperty prop;
                try
                {
                    prop = new WzSoundProperty(((WzSoundProperty)DataTree.SelectedNode.Tag).Name, dialog.FileName);
                }
                catch
                {
                    Warning.Error(Properties.Resources.MainImageLoadError);
                    return;
                }
                IPropertyContainer parent = (IPropertyContainer)((WzSoundProperty)DataTree.SelectedNode.Tag).Parent;
                ((WzSoundProperty)DataTree.SelectedNode.Tag).ParentImage.Changed = true;
                ((WzSoundProperty)DataTree.SelectedNode.Tag).Remove();
                DataTree.SelectedNode.Tag = prop;
                parent.AddProperty(prop);
                mp3Player.SoundProperty = prop;
            }
        }
        #endregion

        #region Copy & Paste
        public WzObject CloneWzObject(WzObject obj)
        {
            if (obj is WzDirectory)
            {
                Warning.Error(Properties.Resources.MainCopyDirError);
                return null;
            }
            else if (obj is WzImage)
            {
                return ((WzImage)obj).DeepClone();
            }
            else if (obj is WzImageProperty)
            {
                return ((WzImageProperty)obj).DeepClone();
            }
            else
            {
                MapleLib.Helpers.ErrorLogger.Log(MapleLib.Helpers.ErrorLevel.MissingFeature, "The current WZ object type cannot be cloned " + obj.ToString() + " " + obj.FullPath);
                return null;
            }
        }

        /// <summary>
        /// Copies from the selected Wz object
        /// </summary>
        public void DoCopy()
        {
            if (!Warning.Warn(Properties.Resources.MainConfirmCopy))
                return;

            clipboard.Clear();
            foreach (WzNode node in DataTree.SelectedNodes)
            {
                WzObject clone = CloneWzObject((WzObject)node.Tag);
                if (clone != null)
                    clipboard.Add(clone);
            }
        }

        private ReplaceResult replaceBoxResult = ReplaceResult.NoneSelectedYet;

        /// <summary>
        /// Paste to the selected WzObject
        /// </summary>
        public void DoPaste()
        {
            if (!Warning.Warn(Properties.Resources.MainConfirmPaste))
                return;

            // Reset replace option
            replaceBoxResult = ReplaceResult.NoneSelectedYet;

            WzNode parent = (WzNode)DataTree.SelectedNode;
            WzObject parentObj = (WzObject)parent.Tag;

            if (parent != null && parent.Tag is WzImage && parent.Nodes.Count == 0)
            {
                ParseOnDataTreeSelectedItem(parent);
            }

            if (parentObj is WzFile)
                parentObj = ((WzFile)parentObj).WzDirectory;

            bool bNoToAllComplete = false;
            foreach (WzObject obj in clipboard)
            {
                if (((obj is WzDirectory || obj is WzImage) && parentObj is WzDirectory) || (obj is WzImageProperty && parentObj is IPropertyContainer))
                {
                    WzObject clone = CloneWzObject(obj);
                    if (clone == null)
                        continue;
                    WzNode node = new WzNode(clone, true);

                    WzNode child = WzNode.GetChildNode(parent, node.Text);
                    if (child != null) // A Child already exist
                    {
                        if (replaceBoxResult == ReplaceResult.NoneSelectedYet)
                        {
                            ReplaceBox.Show(node.Text, out replaceBoxResult);
                        }

                        switch (replaceBoxResult)
                        {
                            case ReplaceResult.No: // Skip just this
                                replaceBoxResult = ReplaceResult.NoneSelectedYet; // reset after use
                                break;

                            case ReplaceResult.Yes: // Replace just this
                                child.Delete();
                                parent.AddNode(node);
                                replaceBoxResult = ReplaceResult.NoneSelectedYet; // reset after use
                                break;

                            case ReplaceResult.NoToAll:
                                bNoToAllComplete = true;
                                break;

                            case ReplaceResult.YesToAll:
                                child.Delete();
                                parent.AddNode(node);
                                break;
                        }

                        if (bNoToAllComplete)
                            break;
                    }
                    else // not not in this 
                    {
                        parent.AddNode(node);
                    }
                }
            }
        }
        #endregion

        #region UI layout
        /// <summary>
        /// Shows the selected data treeview object to UI
        /// </summary>
        /// <param name="obj"></param>
        private void ShowObjectValue(WzObject obj)
        {
            mp3Player.SoundProperty = null;
            nameBox.Text = obj is WzFile ? ((WzFile)obj).Header.Copyright : obj.Name;
            nameBox.ButtonEnabled = false;

            toolStripStatusLabel_additionalInfo.Text = "-"; // Reset additional info to default
            if (isSelectingWzMapFieldLimit) // previously already selected. update again
            {
                isSelectingWzMapFieldLimit = false;
            }

            vectorOriginSelected = new System.Drawing.PointF(0, 0);

            // Canvas animation
            if (DataTree.SelectedNodes.Count <= 1)
                button_animateSelectedCanvas.Visibility = Visibility.Collapsed; // set invisible regardless if none of the nodes are selected.
            else
            {
                bool bIsAllCanvas = true;
                // check if everything selected is WzUOLProperty and WzCanvasProperty
                foreach (WzNode tree in DataTree.SelectedNodes)
                {
                    WzObject wzobj = (WzObject)tree.Tag;
                    if (!(wzobj is WzUOLProperty) && !(wzobj is WzCanvasProperty))
                    {
                        bIsAllCanvas = false;
                        break;
                    }
                }
                button_animateSelectedCanvas.Visibility = bIsAllCanvas ? Visibility.Visible : Visibility.Collapsed;
            }

            // Set default layout collapsed state
            mp3Player.Visibility = Visibility.Collapsed;
            // Button collapsed state
            changeImageButton.Visibility = Visibility.Collapsed;
            saveImageButton.Visibility = Visibility.Collapsed;
            changeSoundButton.Visibility = Visibility.Collapsed;
            saveSoundButton.Visibility = Visibility.Collapsed;
            // Canvas collapsed state
            canvasPropBox.Visibility = Visibility.Collapsed;
            // Value
            textPropBox.Visibility = Visibility.Collapsed;
            // Field limit panel
            fieldLimitPanelHost.Visibility = Visibility.Collapsed;
            // Vector panel
            vectorPanel.Visibility = Visibility.Collapsed;

            // Set layout visibility
            if (obj is WzFile || obj is WzDirectory || obj is WzImage || obj is WzNullProperty || obj is WzSubProperty || obj is WzConvexProperty)
            {
            }
            else if (obj is WzCanvasProperty)
            {
                changeImageButton.Visibility = Visibility.Visible;
                saveImageButton.Visibility = Visibility.Visible;

                // Image
                WzCanvasProperty canvas = (WzCanvasProperty)obj;
                if (canvas.HaveInlinkProperty() || canvas.HaveOutlinkProperty())
                {
                    System.Drawing.Image img = canvas.GetLinkedWzCanvasBitmap();
                    if (img != null)
                        canvasPropBox.Source = BitmapToImageSource.ToWpfBitmap((System.Drawing.Bitmap)img);
                }
                else
                    canvasPropBox.Source = BitmapToImageSource.ToWpfBitmap(canvas.GetBitmap());

                canvasPropBox.Visibility = Visibility.Visible;
            }
            else if (obj is WzUOLProperty)
            {
                // Image
                WzObject linkValue = ((WzUOLProperty)obj).LinkValue;
                if (linkValue is WzCanvasProperty)
                {
                    canvasPropBox.Visibility = Visibility.Visible;
                    canvasPropBox.Source = BitmapToImageSource.ToWpfBitmap(linkValue.GetBitmap());
                    saveImageButton.Visibility = Visibility.Visible;
                }

                // Value
                textPropBox.Visibility = Visibility.Visible;
                textPropBox.Text = obj.ToString();
            }
            else if (obj is WzSoundProperty)
            {
                mp3Player.Visibility = Visibility.Visible;
                mp3Player.SoundProperty = (WzSoundProperty)obj;

                changeSoundButton.Visibility = Visibility.Visible;
                saveSoundButton.Visibility = Visibility.Visible;
            }
            else if (obj is WzStringProperty || obj is WzIntProperty || obj is WzDoubleProperty || obj is WzFloatProperty || obj is WzShortProperty)
            {
                // Value
                textPropBox.Visibility = Visibility.Visible;
                textPropBox.Text = obj.ToString();

                // If text is a string property, expand the textbox
                if (obj is WzStringProperty)
                {
                    textPropBox.Height = 200;
                }
                else
                {
                    textPropBox.Height = 35;
                }


                if (obj is WzStringProperty)
                {
                    WzStringProperty stringObj = (WzStringProperty)obj;

                    // Portal type name display
                    if (stringObj.Name == "pn") // "pn" = portal name
                    {
                        if (MapleLib.WzLib.WzStructure.Data.Tables.PortalTypeNames.ContainsKey(obj.GetString()))
                        {
                            toolStripStatusLabel_additionalInfo.Text =
                                string.Format(Properties.Resources.MainAdditionalInfo_PortalType, MapleLib.WzLib.WzStructure.Data.Tables.PortalTypeNames[obj.GetString()]);
                        }
                        else
                        {
                            toolStripStatusLabel_additionalInfo.Text = string.Format(Properties.Resources.MainAdditionalInfo_PortalType, obj.GetString());
                        }
                    }
                }
                else if (obj is WzIntProperty)
                {
                    WzIntProperty intProperty = (WzIntProperty)obj;

                    if (intProperty.Name == "fieldLimit")
                    {
                        isSelectingWzMapFieldLimit = true;

                        fieldLimitPanel1.UpdateFieldLimitCheckboxes(intProperty);

                        // Set visibility
                        fieldLimitPanelHost.Visibility = Visibility.Visible;
                    }
                }
            }
            else if (obj is WzVectorProperty)
            {
                vectorPanel.Visibility = Visibility.Visible;

                vectorPanel.X = ((WzVectorProperty)obj).X.Value;
                vectorPanel.Y = ((WzVectorProperty)obj).Y.Value;
            }
            else
            {
            }
        }
        #endregion

        #region Search

        /// <summary>
        /// On search box fade in completed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Storyboard_Find_FadeIn_Completed(object sender, EventArgs e)
        {
            findBox.Focus();
        }

        private int searchidx = 0;
        private bool finished = false;
        private bool listSearchResults = false;
        private List<string> searchResultsList = new List<string>();
        private bool searchValues = true;
        private WzNode coloredNode = null;
        private int currentidx = 0;
        private string searchText = "";
        private bool extractImages = false;

        /// <summary>
        /// Close search box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_closeSearch_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Media.Animation.Storyboard sbb = (System.Windows.Media.Animation.Storyboard)(this.FindResource("Storyboard_Find_FadeOut"));
            sbb.Begin();
        }

        private void SearchWzProperties(IPropertyContainer parent)
        {
            foreach (WzImageProperty prop in parent.WzProperties)
            {
                if ((0 <= prop.Name.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase)) || (searchValues && prop is WzStringProperty && (0 <= ((WzStringProperty)prop).Value.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase))))
                {
                    if (listSearchResults)
                        searchResultsList.Add(prop.FullPath.Replace(";", @"\"));
                    else if (currentidx == searchidx)
                    {
                        if (prop.HRTag == null)
                            ((WzNode)prop.ParentImage.HRTag).Reparse();
                        WzNode node = (WzNode)prop.HRTag;
                        //if (node.Style == null) node.Style = new ElementStyle();
                        node.BackColor = System.Drawing.Color.Yellow;
                        coloredNode = node;
                        node.EnsureVisible();
                        //DataTree.Focus();
                        finished = true;
                        searchidx++;
                        return;
                    }
                    else
                        currentidx++;
                }
                if (prop is IPropertyContainer && prop.WzProperties.Count != 0)
                {
                    SearchWzProperties((IPropertyContainer)prop);
                    if (finished)
                        return;
                }
            }
        }

        private void SearchTV(WzNode node)
        {
            foreach (WzNode subnode in node.Nodes)
            {
                if (0 <= subnode.Text.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (listSearchResults)
                        searchResultsList.Add(subnode.FullPath.Replace(";", @"\"));
                    else if (currentidx == searchidx)
                    {
                        //if (subnode.Style == null) subnode.Style = new ElementStyle();
                        subnode.BackColor = System.Drawing.Color.Yellow;
                        coloredNode = subnode;
                        subnode.EnsureVisible();
                        //DataTree.Focus();
                        finished = true;
                        searchidx++;
                        return;
                    }
                    else
                        currentidx++;
                }
                if (subnode.Tag is WzImage)
                {
                    WzImage img = (WzImage)subnode.Tag;
                    if (img.Parsed)
                        SearchWzProperties(img);
                    else if (extractImages)
                    {
                        img.ParseImage();
                        SearchWzProperties(img);
                    }
                    if (finished) return;
                }
                else SearchTV(subnode);
            }
        }

        /// <summary>
        /// Find all
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_allSearch_Click(object sender, RoutedEventArgs e)
        {
            if (coloredNode != null)
            {
                coloredNode.BackColor = System.Drawing.Color.White;
                coloredNode = null;
            }
            if (findBox.Text == "" || DataTree.Nodes.Count == 0)
                return;
            if (DataTree.SelectedNode == null)
                DataTree.SelectedNode = DataTree.Nodes[0];

            finished = false;
            listSearchResults = true;
            searchResultsList.Clear();
            //searchResultsBox.Items.Clear();
            searchValues = Program.ConfigurationManager.UserSettings.SearchStringValues;
            currentidx = 0;
            searchText = findBox.Text;
            extractImages = Program.ConfigurationManager.UserSettings.ParseImagesInSearch;
            foreach (WzNode node in DataTree.SelectedNodes)
            {
                if (node.Tag is WzImageProperty)
                    continue;
                else if (node.Tag is IPropertyContainer)
                    SearchWzProperties((IPropertyContainer)node.Tag);
                else
                    SearchTV(node);
            }

            SearchSelectionForm form = SearchSelectionForm.Show(searchResultsList);
            form.OnSelectionChanged += Form_OnSelectionChanged;

            findBox.Focus();
        }

        /// <summary>
        /// On search selection from SearchSelectionForm list changed
        /// </summary>
        /// <param name="str"></param>
        private void Form_OnSelectionChanged(string str)
        {
            string[] splitPath = str.Split(@"\".ToCharArray());
            WzNode node = null;
            System.Windows.Forms.TreeNodeCollection collection = DataTree.Nodes;
            for (int i = 0; i < splitPath.Length; i++)
            {
                node = GetNodeByName(collection, splitPath[i]);
                if (node.Tag is WzImage && !((WzImage)node.Tag).Parsed && i != splitPath.Length - 1)
                {
                    ((WzImage)node.Tag).ParseImage();
                    node.Reparse();
                }
                collection = node.Nodes;
            }
            if (node != null)
            {
                DataTree.SelectedNode = node;
                node.EnsureVisible();
                DataTree.RefreshSelectedNodes();
            }
        }

        private WzNode GetNodeByName(System.Windows.Forms.TreeNodeCollection collection, string name)
        {
            foreach (WzNode node in collection)
                if (node.Text == name)
                    return node;
            return null;
        }

        /// <summary>
        /// Find next
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_nextSearch_Click(object sender, RoutedEventArgs e)
        {
            if (coloredNode != null)
            {
                coloredNode.BackColor = System.Drawing.Color.White;
                coloredNode = null;
            }
            if (findBox.Text == "" || DataTree.Nodes.Count == 0) return;
            if (DataTree.SelectedNode == null) DataTree.SelectedNode = DataTree.Nodes[0];
            finished = false;
            listSearchResults = false;
            searchResultsList.Clear();
            searchValues = Program.ConfigurationManager.UserSettings.SearchStringValues;
            currentidx = 0;
            searchText = findBox.Text;
            extractImages = Program.ConfigurationManager.UserSettings.ParseImagesInSearch;
            foreach (WzNode node in DataTree.SelectedNodes)
            {
                if (node.Tag is IPropertyContainer)
                    SearchWzProperties((IPropertyContainer)node.Tag);
                else if (node.Tag is WzImageProperty) continue;
                else SearchTV(node);
                if (finished) break;
            }
            if (!finished) { MessageBox.Show(Properties.Resources.MainTreeEnd); searchidx = 0; DataTree.SelectedNode.EnsureVisible(); }
            findBox.Focus();
        }

        private void findBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                button_nextSearch_Click(null, null);
                e.Handled = true;
            }
        }

        private void findBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            searchidx = 0;
        }
        #endregion
    }
}
