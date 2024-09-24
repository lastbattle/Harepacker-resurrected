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
using WpfAnimatedGif;

namespace HaRepacker.GUI.Panels.SubPanels
{
    /// <summary>
    /// Interaction logic for LoadingPanel.xaml
    /// </summary>
    public partial class LoadingPanel : UserControl, INotifyPropertyChanged
    {
        private ImageAnimationController imageController = null;

        public LoadingPanel()
        {
            InitializeComponent();

            this.DataContext = this; // set data binding to self.
        }


        /// <summary>
        /// Loaded
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ImageLoadingGif_AnimationLoaded(object sender, RoutedEventArgs e)
        {
            imageController = ImageBehavior.GetAnimationController(imageLoadingGif);
        }

        /// <summary>
        /// 
        /// </summary>
        public void OnStartAnimate()
        {
            //imageController.Play(); // doesnt animate when Visibility is collapsed anyway.
        }

        /// <summary>
        /// 
        /// </summary>
        public void OnPauseAnimate()
        {
            //imageController.Pause();
        }


        #region PropertyChanged
        /// <summary>
        /// Property changed event handler to trigger update UI
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) 
                handler(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
