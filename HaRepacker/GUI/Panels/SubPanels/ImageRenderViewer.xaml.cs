using HaRepacker.GUI.Controls;
using HaRepacker.GUI.Input;
using HaSharedLibrary.Util;
using MapleLib.Converters;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
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
using static MapleLib.Configuration.UserSettings;

namespace HaRepacker.GUI.Panels.SubPanels
{
    /// <summary>
    /// Interaction logic for ImageRenderViewer.xaml
    /// </summary>
    public partial class ImageRenderViewer : UserControl
    {
        private bool isLoading = false;

        private MainPanel mainPanel;

        private ImageRenderViewerItem _bindingPropertyItem = new ImageRenderViewerItem();
        public ImageRenderViewerItem BindingPropertyItem {
            get { return _bindingPropertyItem; }
            private set { }
        }

        public ImageRenderViewer()
        {
            isLoading = true; // set isloading 

            InitializeComponent();

            // Set theme color
            if (Program.ConfigurationManager.UserSettings.ThemeColor == (int)UserSettingsThemeColor.Dark)
            {
                VisualStateManager.GoToState(this, "BlackTheme", false);
            }

            this.DataContext = _bindingPropertyItem; // set data binding
            _bindingPropertyItem.PropertyChanged += ImgPropertyItem_PropertyChanged; // on propertygrid property changed


            Loaded += ImageRenderViewer_Loaded;
        }

        public void SetIsLoading(bool bIsLoading) {
            this.isLoading = bIsLoading;
        }
        public void SetParentMainPanel(MainPanel panel) {
            this.mainPanel = panel;
        }


        /// <summary>
        /// When the page loads
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ImageRenderViewer_Loaded(object sender, RoutedEventArgs e) {
            try {
                // Set via app settings
                _bindingPropertyItem.ShowCrosshair = Program.ConfigurationManager.UserSettings.EnableCrossHairDebugInformation;
                _bindingPropertyItem.ShowImageBorder = Program.ConfigurationManager.UserSettings.EnableBorderDebugInformation;

                ZoomSlider.Value = Program.ConfigurationManager.UserSettings.ImageZoomLevel;
            }
            finally {
                isLoading = false;
            }
        }

        #region UI Events

        /// <summary>
        /// Image zoom level on value changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isLoading)
                return;

            Slider zoomSlider = (Slider)sender;
            Program.ConfigurationManager.UserSettings.ImageZoomLevel = zoomSlider.Value;
        }

        private bool bBorderDragging = false;

        private void Rectangle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            bBorderDragging = true;
            Rectangle_MouseMove(sender, e);

            System.Diagnostics.Debug.WriteLine("Mouse left button down");
        }

        private void Rectangle_MouseMove(object sender, MouseEventArgs e)
        {
            if (bBorderDragging)
            {
                // dragMove
                System.Diagnostics.Debug.WriteLine("Mouse drag move");
            }
        }

        private void Rectangle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            bBorderDragging = false;

            System.Diagnostics.Debug.WriteLine("Mouse left button up");
        }

        /// <summary>
        /// On propertygrid property changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ImgPropertyItem_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (isLoading) {
                return;
            }

            switch (e.PropertyName) {
                case "ShowCrosshair": {
                        if (_bindingPropertyItem.ShowCrosshair == true) {
                            Program.ConfigurationManager.UserSettings.EnableCrossHairDebugInformation = true;
                        }
                        else {
                            Program.ConfigurationManager.UserSettings.EnableCrossHairDebugInformation = false;
                        }
                        break;
                    }
                case "ShowImageBorder": {
                        if (_bindingPropertyItem.ShowImageBorder == true) {
                            Program.ConfigurationManager.UserSettings.EnableBorderDebugInformation = true;
                        }
                        else {
                            Program.ConfigurationManager.UserSettings.EnableBorderDebugInformation = false;
                        }
                        break;
                    }
                case "Delay": {
                        int newdelay = _bindingPropertyItem.Delay;
                        WzIntProperty intProperty = this._bindingPropertyItem.ParentWzCanvasProperty[WzCanvasProperty.AnimationDelayPropertyName] as WzIntProperty;
                        if (intProperty != null) {
                            intProperty.Value = newdelay;
                        }
                        break;
                    }
                case "CanvasVectorOrigin": {
                        NotifyPointF CanvasVectorOrigin = this._bindingPropertyItem.CanvasVectorOrigin;
                        
                        WzVectorProperty vectorProp = this._bindingPropertyItem.ParentWzCanvasProperty[WzCanvasProperty.OriginPropertyName] as WzVectorProperty;
                        if (vectorProp == null) {
                            vectorProp = new WzVectorProperty(WzCanvasProperty.OriginPropertyName, 0, 0);

                            this._bindingPropertyItem.ParentWzCanvasProperty.AddProperty(vectorProp);
                            this._bindingPropertyItem.ParentWzCanvasProperty.ParentImage.Changed = true;
                        }
                        vectorProp.X.Value = (int)CanvasVectorOrigin.X;
                        vectorProp.Y.Value = (int)CanvasVectorOrigin.Y;
                        break;
                    }
                case "CanvasVectorHead": {
                        NotifyPointF vectorHead = this._bindingPropertyItem.CanvasVectorHead;

                        WzVectorProperty vectorProp = this._bindingPropertyItem.ParentWzCanvasProperty[WzCanvasProperty.HeadPropertyName] as WzVectorProperty;
                        if (vectorProp == null) {
                            vectorProp = new WzVectorProperty(WzCanvasProperty.HeadPropertyName, 0, 0);

                            this._bindingPropertyItem.ParentWzCanvasProperty.AddProperty(vectorProp);
                            this._bindingPropertyItem.ParentWzCanvasProperty.ParentImage.Changed = true;
                        }
                        vectorProp.X.Value = (int)vectorHead.X;
                        vectorProp.Y.Value = (int)vectorHead.Y;
                        break;
                    }
                case "CanvasVectorLt": {
                        NotifyPointF vectorLt = this._bindingPropertyItem.CanvasVectorLt;

                        WzVectorProperty vectorProp = this._bindingPropertyItem.ParentWzCanvasProperty[WzCanvasProperty.LtPropertyName] as WzVectorProperty;
                        if (vectorProp == null) {
                            vectorProp = new WzVectorProperty(WzCanvasProperty.LtPropertyName, 0, 0);

                            this._bindingPropertyItem.ParentWzCanvasProperty.AddProperty(vectorProp);
                            this._bindingPropertyItem.ParentWzCanvasProperty.ParentImage.Changed = true;
                        }
                        vectorProp.X.Value = (int)vectorLt.X;
                        vectorProp.Y.Value = (int)vectorLt.Y;
                        break;
                    }
            }
        }

        /// <summary>
        /// Color picker -- image ARGB editor
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MyColorCanvas_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e) {
            if (isLoading)
                return;

            if (e.NewValue.HasValue) {
                System.Windows.Media.Color selectedColor = e.NewValue.Value;

                if (selectedColor != null) {
                    //ColorDisplay.Fill = new SolidColorBrush(selectedColor);

                    // set only the temporary "Image" object that only displays to the user
                    // while keeping the original copy until the user is ready to "apply"
                    _bindingPropertyItem.Bitmap = BitmapHelper.ApplyColorFilter(_bindingPropertyItem.BitmapBackup, selectedColor);
                }
            }
        }

        private void button_filter_apply_Click(object sender, RoutedEventArgs e) {
            if (isLoading)
                return;

            if (_bindingPropertyItem.Image != null) {
                // re-calculate based on current ARGB and then apply
                System.Windows.Media.Color? selectedColor = MyColorCanvas.SelectedColor;
                if (selectedColor != null) {
                    _bindingPropertyItem.Bitmap = BitmapHelper.ApplyColorFilter(_bindingPropertyItem.BitmapBackup, selectedColor.Value);

                    mainPanel.ChangeCanvasPropBoxImage(_bindingPropertyItem.Bitmap);
                }
            }
        }

        private void button_filter_reset_Click(object sender, RoutedEventArgs e) {
            if (isLoading)
                return;

            if (_bindingPropertyItem.Bitmap != null) {
                _bindingPropertyItem.Bitmap = _bindingPropertyItem.BitmapBackup;
            }
        }
        #endregion
    }
}
