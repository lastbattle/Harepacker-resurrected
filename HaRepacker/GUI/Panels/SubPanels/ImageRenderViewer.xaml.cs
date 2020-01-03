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
using static HaRepacker.Configuration.UserSettings;

namespace HaRepacker.GUI.Panels.SubPanels
{
    /// <summary>
    /// Interaction logic for ImageRenderViewer.xaml
    /// </summary>
    public partial class ImageRenderViewer : UserControl, INotifyPropertyChanged
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

                ZoomSlider.Value = Program.ConfigurationManager.UserSettings.ImageZoomLevel;
            } finally
            {
                isLoading = false;
            }
        }

        #region Exported Fields
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

        #region PropertyChanged
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
        #endregion
    }
}
