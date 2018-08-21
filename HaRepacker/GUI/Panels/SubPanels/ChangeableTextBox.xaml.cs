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

namespace HaRepacker.GUI.Panels.SubPanels
{
    /// <summary>
    /// Interaction logic for ChangeableTextBoxXAML.xaml
    /// </summary>
    public partial class ChangeableTextBox : UserControl
    {
        public ChangeableTextBox()
        {
            InitializeComponent();

            this.DataContext = this; // set data binding to self.
        }

        private string _Header = "";
        public string Header
        {
            get { return _Header; }
            set { _Header = value; }
        }

        public string Text
        {
            get { return textBox.Text; }
            set { textBox.Text = value; }
        }

        public bool ButtonEnabled
        {
            get { return applyButton.IsEnabled; }
            set { applyButton.IsEnabled = value; }
        }

        public event EventHandler ButtonClicked;
        private void applyButton_Click(object sender, RoutedEventArgs e)
        {
            if (ButtonClicked != null)
                ButtonClicked.Invoke(sender, e);

            applyButton.IsEnabled = false;
        }

        /// <summary>
        /// On text changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            applyButton.IsEnabled = true;
        }
    }
}
