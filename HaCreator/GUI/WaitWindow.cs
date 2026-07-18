using System.ComponentModel;
using System.Windows;

namespace HaCreator.GUI
{
    public partial class WaitWindow : Window
    {
        private bool finished;

        public WaitWindow(string message)
        {
            InitializeComponent();
            messageText.Text = message;
            if (Program.HaEditorWindow?.IsVisible == true)
            {
                Owner = Program.HaEditorWindow;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
        }

        private void WaitWindow_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = !finished;
        }

        public void EndWait()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(EndWait);
                return;
            }

            finished = true;
            Close();
        }
    }
}
