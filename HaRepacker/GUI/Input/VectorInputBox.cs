using System.Globalization;
using System.Windows;
using System.Windows.Input;
using DrawingPoint = System.Drawing.Point;

namespace HaRepacker.GUI.Input
{
    public partial class VectorInputBox : Window
    {
        private string nameResult;
        private DrawingPoint? pointResult;

        public static bool Show(string title, out string name, out DrawingPoint? pt)
        {
            VectorInputBox form = new(title);
            bool accepted = form.ShowDialog() == true;
            name = form.nameResult;
            pt = form.pointResult;
            return accepted;
        }

        public VectorInputBox(string title)
        {
            InitializeComponent();
            Title = title;
            labelName.Text = InputDialogSupport.Text(GetType(), "label1.Text", "Name:");
            labelValue.Text = InputDialogSupport.Text(GetType(), "label2.Text", "Value:");
            separatorText.Text = InputDialogSupport.Text(GetType(), "label3.Text", ",");
            okButton.Content = InputDialogSupport.Text(GetType(), "okButton.Text", "OK");
            cancelButton.Content = InputDialogSupport.Text(GetType(), "cancelButton.Text", "Cancel");
        }

        private void Input_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) Accept(); }
        private void OkButton_Click(object sender, RoutedEventArgs e) => Accept();
        private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void Accept()
        {
            if (!int.TryParse(xBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int x) ||
                !int.TryParse(yBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int y))
            { InputDialogSupport.WarnInvalidInput(); return; }
            nameResult = resultBox.Text;
            pointResult = new DrawingPoint(x, y);
            DialogResult = true;
        }
    }
}
