using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Forms = System.Windows.Forms;

namespace HaCreator.GUI.InstanceEditor
{
    public partial class LoadMapSelector : Window
    {
        private readonly Forms.NumericUpDown numericUpDown;
        private readonly Forms.TextBox textBox;
        private bool accepted;
        private string selectedMap = string.Empty;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string SelectedMap
        {
            get => selectedMap;
            set => selectedMap = value;
        }

        public LoadMapSelector() => InitializeSelector();
        public LoadMapSelector(Forms.NumericUpDown numericUpDown)
        {
            this.numericUpDown = numericUpDown;
            InitializeSelector();
        }
        public LoadMapSelector(Forms.TextBox textbox)
        {
            textBox = textbox;
            InitializeSelector();
        }

        private void InitializeSelector()
        {
            InitializeComponent();
            Closing += (_, _) => { if (!accepted) selectedMap = string.Empty; };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) => mapBrowser.InitializeMapsListboxItem(false);
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => mapBrowser.ApplySearch(searchBox.Text);
        private void SelectButton_Click(object sender, RoutedEventArgs e) => AcceptSelection();
        private void AcceptSelection()
        {
            if (mapBrowser.SelectedItem == null || mapBrowser.SelectedItem.Length < 9)
                return;
            string mapId = mapBrowser.SelectedItem[..9];
            if (numericUpDown != null) numericUpDown.Value = long.Parse(mapId);
            else if (textBox != null) textBox.Text = mapId;
            selectedMap = mapId;
            accepted = true;
            DialogResult = true;
        }
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { selectedMap = string.Empty; Close(); }
            else if (e.Key == Key.Enter) AcceptSelection();
        }
    }
}
