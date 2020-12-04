using HaRepacker.GUI.Input;
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
    public partial class ImageRenderViewer : UserControl,  INotifyPropertyChanged
    {
        private bool isLoading = false;

        public ImageRenderViewer()
        {
            isLoading = true; // set isloading 

            InitializeComponent();

            // Set theme color
            if (Program.ConfigurationManager.UserSettings.ThemeColor == (int)UserSettingsThemeColor.Dark)
            {
                VisualStateManager.GoToState(this, "BlackTheme", false);
            }

            this.DataContext = this; // set data binding to self.

            Loaded += ImageRenderViewer_Loaded;
        }

        /// <summary>
        /// When the page loads
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ImageRenderViewer_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Set via app settings
                checkbox_crosshair.IsChecked = Program.ConfigurationManager.UserSettings.EnableCrossHairDebugInformation;
                checkbox_border.IsChecked = Program.ConfigurationManager.UserSettings.EnableBorderDebugInformation;

                ZoomSlider.Value = Program.ConfigurationManager.UserSettings.ImageZoomLevel;
            } finally
            {
                isLoading = false;
            }
        }

        #region Exported Fields
        private WzCanvasProperty _ParentWzCanvasProperty = null;
        /// <summary>
        /// The parent WZCanvasProperty to display from
        /// </summary>
        public WzCanvasProperty ParentWzCanvasProperty
        {
            get { return _ParentWzCanvasProperty; }
            set
            {
                _ParentWzCanvasProperty = value;
            }
        }

        private ImageSource _Image = null;
        /// <summary>
        /// The image to display on the canvas
        /// </summary>
        public ImageSource Image
        {
            get { return _Image; }
            set
            {
                _Image = value;
                OnPropertyChanged("Image");

                // Update image width and height too.
                ImageWidth = _Image.Width;
                ImageHeight = _Image.Height;
            }
        }

        private int _Delay = 0;
        /// <summary>
        /// Delay of the image
        /// </summary>
        public int Delay
        {
            get { return _Delay; }
            set
            {
                _Delay = value;
                OnPropertyChanged("Delay");

                textbox_delay.Text = _Delay.ToString();
            }
        }

        private PointF _CanvasVectorOrigin = new PointF(0, 0);
        /// <summary>
        /// Origin to center the crosshair
        /// </summary>
        public PointF CanvasVectorOrigin
        {
            get { return _CanvasVectorOrigin; }
            set
            {
                _CanvasVectorOrigin = value;
                OnPropertyChanged("CanvasVectorOrigin");

                textbox_originX.Text = _CanvasVectorOrigin.X.ToString();
                textbox_originY.Text = _CanvasVectorOrigin.Y.ToString();
            }
        }

        private PointF _CanvasVectorHead = new PointF(0, 0);
        /// <summary>
        /// Head vector (Hit positioning for mobs?)
        /// </summary>
        public PointF CanvasVectorHead
        {
            get { return _CanvasVectorHead; }
            set
            {
                _CanvasVectorHead = value;
                OnPropertyChanged("CanvasVectorHead");

                textbox_headX.Text = _CanvasVectorHead.X.ToString();
                textbox_headY.Text = _CanvasVectorHead.Y.ToString();
            }
        }

        private PointF _CanvasVectorLt = new PointF(0, 0);
        /// <summary>
        /// lt vector
        /// </summary>
        public PointF CanvasVectorLt
        {
            get { return _CanvasVectorLt; }
            set
            {
                _CanvasVectorLt = value;
                OnPropertyChanged("CanvasVectorLt");

                textbox_ltX.Text = _CanvasVectorLt.X.ToString();
                textbox_ltY.Text = _CanvasVectorLt.Y.ToString();
            }
        }

        private double _ImageWidth = 0;
        /// <summary>
        /// The width of the image currently displayed on the canvas
        /// </summary>
        public double ImageWidth
        {
            get { return _ImageWidth; }
            set { 
                this._ImageWidth = value;
                OnPropertyChanged("ImageWidth");
            }
        }

        private double _ImageHeight = 0;
        /// <summary>
        /// The Height of the image currently displayed on the canvas
        /// </summary>
        public double ImageHeight
        {
            get { return _ImageHeight; }
            set {
                this._ImageHeight = value;
                OnPropertyChanged("ImageHeight");
            }
        }
        #endregion

        #region Property Changed
        /// <summary>
        /// Property changed event handler to trigger update UI
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region UI Events
        /// <summary>
        /// Checkbox for crosshair
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkbox_crosshair_Checked(object sender, RoutedEventArgs e)
        {
            if (isLoading)
                return;

            CheckBox checkbox = (CheckBox)sender;
            if (checkbox.IsChecked == true)
            {
                Program.ConfigurationManager.UserSettings.EnableCrossHairDebugInformation = true;
            } else
            {
                Program.ConfigurationManager.UserSettings.EnableCrossHairDebugInformation = false;
            }
        }

        /// <summary>
        /// Checkbox for Border
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkbox_border_Checked(object sender, RoutedEventArgs e)
        {
            if (isLoading)
                return;

            CheckBox checkbox = (CheckBox)sender;
            if (checkbox.IsChecked == true)
            {
                Program.ConfigurationManager.UserSettings.EnableBorderDebugInformation = true;
            }
            else
            {
                Program.ConfigurationManager.UserSettings.EnableBorderDebugInformation = false;
            }
        }

        /// <summary>
        /// 'lt' value changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textbox_lt_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isLoading)
                return;

            button_ltEdit.IsEnabled = true;
        }

        /// <summary>
        /// 'head' value changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textbox_head_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isLoading)
                return;

            button_headEdit.IsEnabled = true;
        }

        /// <summary>
        ///  'vector' value changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textbox_origin_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isLoading)
                return;

            button_originEdit.IsEnabled = true;
        }

        /// <summary>
        /// 'delay' valeu changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textbox_delay_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isLoading)
                return;

            button_delayEdit.IsEnabled = true;
        }

        /// <summary>
        /// Easy access to editing image 'lt' properties 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_ltEdit_Click(object sender, RoutedEventArgs e)
        {
            if (isLoading)
                return;

            if (int.TryParse(textbox_ltX.Text, out int newX) && int.TryParse(textbox_ltY.Text, out int newY))
            {
                WzVectorProperty vectorProp = _ParentWzCanvasProperty[WzCanvasProperty.LtPropertyName] as WzVectorProperty;
                if (vectorProp != null)
                {
                    vectorProp.X.Value = newX;
                    vectorProp.Y.Value = newY;

                    // Update local UI
                    CanvasVectorLt = new PointF(newX, newY);

                    button_ltEdit.IsEnabled = false;
                }
            }
        }

        /// <summary>
        /// Easy access to editing image 'head' properties 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_headEdit_Click(object sender, RoutedEventArgs e)
        {
            if (isLoading)
                return;

            if (int.TryParse(textbox_headX.Text, out int newX) && int.TryParse(textbox_headY.Text, out int newY))
            {
                WzVectorProperty vectorProp = _ParentWzCanvasProperty[WzCanvasProperty.HeadPropertyName] as WzVectorProperty;
                if (vectorProp != null)
                {
                    vectorProp.X.Value = newX;
                    vectorProp.Y.Value = newY;

                    // Update local UI
                    CanvasVectorHead = new PointF(newX, newY);

                    button_headEdit.IsEnabled = false;
                }
            }
        }

        /// <summary>
        /// Easy access to editing image 'delay' properties 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_delayEdit_Click(object sender, RoutedEventArgs e)
        {
            if (isLoading)
                return;

            if (int.TryParse(textbox_delay.Text, out int newdelay))
            {
                WzIntProperty intProperty = _ParentWzCanvasProperty[WzCanvasProperty.AnimationDelayPropertyName] as WzIntProperty;
                if (intProperty != null)
                {
                    intProperty.Value = newdelay;

                    // Update local UI
                    Delay = newdelay;

                    button_delayEdit.IsEnabled = false;
                }
            }
        }

        /// <summary>
        /// Easy access to editing image 'origin' properties 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_originEdit_Click(object sender, RoutedEventArgs e)
        {
            if (isLoading)
                return;

            if (int.TryParse(textbox_originX.Text, out int newX) && int.TryParse(textbox_originY.Text, out int newY))
            {
                WzVectorProperty vectorProp = _ParentWzCanvasProperty[WzCanvasProperty.OriginPropertyName] as WzVectorProperty;
                if (vectorProp != null)
                {
                    vectorProp.X.Value = newX;
                    vectorProp.Y.Value = newY;

                    // Update local UI
                    CanvasVectorOrigin = new PointF(newX, newY);

                    button_originEdit.IsEnabled = false;
                }
            }
        }

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
        #endregion
    }
}
