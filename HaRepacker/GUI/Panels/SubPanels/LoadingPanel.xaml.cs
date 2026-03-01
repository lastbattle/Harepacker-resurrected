using System.ComponentModel;
using System.Windows.Controls;

namespace HaRepacker.GUI.Panels.SubPanels
{
    /// <summary>
    /// Interaction logic for LoadingPanel.xaml
    /// </summary>
    public partial class LoadingPanel : UserControl, INotifyPropertyChanged
    {
        public LoadingPanel()
        {
            InitializeComponent();

            this.DataContext = this; // set data binding to self.
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
