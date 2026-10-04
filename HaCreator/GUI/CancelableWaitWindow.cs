using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using HaCreator.GUI.Localization;

namespace HaCreator.GUI
{
    public partial class CancelableWaitWindow : Window
    {
        private bool finished;
        private volatile bool canceled;
        private readonly Thread actionThread;
        public object result;

        public CancelableWaitWindow(string message, Func<object> action)
        {
            InitializeComponent();
            messageText.Text = message;
            if (Program.HaEditorWindow?.IsVisible == true)
            {
                Owner = Program.HaEditorWindow;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            actionThread = new Thread(() =>
            {
                try
                {
                    object actionResult = action();
                    if (!canceled)
                        result = actionResult;
                }
                catch (ThreadInterruptedException)
                {
                    result = null;
                }
                finally
                {
                    Dispatcher.BeginInvoke(EndWait);
                }
            })
            {
                IsBackground = true,
                Name = "HaCreator cancelable wait action"
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            actionThread.Start();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            cancelButton.IsEnabled = false;
            canceled = true;
            result = null;
            if (actionThread.IsAlive)
                actionThread.Interrupt();

            if (actionThread.IsAlive && !actionThread.Join(5000))
            {
                MessageBox.Show(this,
                    DialogTextExtension.Get("Dialog_CancelTimeoutMessage"),
                    DialogTextExtension.Get("Dialog_CancelTimeoutTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }

            EndWait();
        }

        public void EndWait()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(EndWait);
                return;
            }

            if (finished)
                return;
            finished = true;
            Close();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = !finished;
        }
    }
}
