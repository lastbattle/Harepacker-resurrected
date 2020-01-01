using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace HaRepacker.GUI.Panels.SubPanels
{
    /// <summary>
    /// Interaction logic for ImageRenderViewer.xaml
    /// </summary>
    public partial class ImageRenderViewer : UserControl, INotifyPropertyChanged
    {
        public ImageRenderViewer()
        {
            InitializeComponent();

            this.DataContext = this; // set data binding to self.
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
    }
}
