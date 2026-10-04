using System.Windows;

namespace HaRepacker.GUI.Input
{
    public enum ReplaceResult { Yes, No, YesToAll, NoToAll, NoneSelectedYet }

    public partial class ReplaceBox : Window
    {
        public ReplaceResult result = ReplaceResult.No;

        private ReplaceBox()
        {
            InitializeComponent();
            Title = InputDialogSupport.Text(GetType(), "$this.Text", "Replace");
            yesButton.Content = InputDialogSupport.Text(GetType(), "btnYes.Text", "Yes");
            noButton.Content = InputDialogSupport.Text(GetType(), "btnNo.Text", "No");
            yesAllButton.Content = InputDialogSupport.Text(GetType(), "btnYestoall.Text", "Yes to all");
            noAllButton.Content = InputDialogSupport.Text(GetType(), "btnNotoall.Text", "No to all");
        }

        public static bool Show(string name, out ReplaceResult result)
        {
            ReplaceBox box = new();
            box.messageText.Text = string.Format(Properties.Resources.ReplaceConfirm, name);
            box.ShowDialog();
            result = box.result;
            return true;
        }

        private void Choose(ReplaceResult choice) { result = choice; Close(); }
        private void Yes_Click(object sender, RoutedEventArgs e) => Choose(ReplaceResult.Yes);
        private void No_Click(object sender, RoutedEventArgs e) => Choose(ReplaceResult.No);
        private void YesAll_Click(object sender, RoutedEventArgs e) => Choose(ReplaceResult.YesToAll);
        private void NoAll_Click(object sender, RoutedEventArgs e) => Choose(ReplaceResult.NoToAll);
    }
}
