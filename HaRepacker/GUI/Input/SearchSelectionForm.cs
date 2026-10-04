using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace HaRepacker.GUI
{
    public partial class SearchSelectionForm : Window
    {
        public delegate void SearchSelectionChanged(string str);
        public event SearchSelectionChanged OnSelectionChanged;

        public SearchSelectionForm()
        {
            InitializeComponent();
            Title = new ComponentResourceManager(typeof(SearchSelectionForm)).GetString("$this.Text") ?? "Results";
        }

        public static SearchSelectionForm Show(List<string> searchPaths)
        {
            SearchSelectionForm form = new();
            form.listBox_items.ItemsSource = searchPaths;
            form.Show();
            form.Activate();
            return form;
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listBox_items.SelectedItem is string selected)
                OnSelectionChanged?.Invoke(selected);
        }
    }
}
