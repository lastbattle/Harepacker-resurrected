using System.Windows;

namespace HaRepacker.GUI.Input
{
    public partial class SearchOptionsForm : Window
    {
        public SearchOptionsForm()
        {
            InitializeComponent();
            Title = InputDialogSupport.Text(GetType(), "$this.Text", "Search Options");
            parseImages.Content = InputDialogSupport.Text(GetType(), "parseImages.Text", "Parse images while searching");
            searchValues.Content = InputDialogSupport.Text(GetType(), "searchValues.Text", "Search string values");
            okButton.Content = InputDialogSupport.Text(GetType(), "button1.Text", "OK");
            cancelButton.Content = InputDialogSupport.Text(GetType(), "button2.Text", "Cancel");
            parseImages.IsChecked = Program.ConfigurationManager.UserSettings.ParseImagesInSearch;
            searchValues.IsChecked = Program.ConfigurationManager.UserSettings.SearchStringValues;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Program.ConfigurationManager.UserSettings.ParseImagesInSearch = parseImages.IsChecked == true;
            Program.ConfigurationManager.UserSettings.SearchStringValues = searchValues.IsChecked == true;
            Close();
        }
    }
}
