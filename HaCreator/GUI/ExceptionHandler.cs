using System;
using System.IO;
using HaCreator.GUI.Localization;
using System.Windows;
using Forms = System.Windows.Forms;

namespace HaCreator.GUI
{
    public partial class ExceptionHandler : Window
    {
        public static bool InitializationFinished;

        public string GetExceptionInfo(Exception exception)
        {
            string result = exception.Message + "\r\n\r\n" + exception.Source + "\r\n\r\n" + exception.StackTrace;
            if (exception.InnerException != null)
                result += "\r\n\r\n" + GetExceptionInfo(exception.InnerException);
            return result;
        }

        public ExceptionHandler(Exception exception)
        {
            InitializeComponent();
            string logPath = Path.Combine(AppContext.BaseDirectory, "crashdump.log");
            File.WriteAllText(logPath, GetExceptionInfo(exception));

            if (!InitializationFinished)
            {
                crashMessageText.Text = DialogTextExtension.Format("Dialog_CrashBeforeEditing", logPath);
                restartButton.Content = DialogTextExtension.Get("Dialog_RestartHaCreator");
            }
            else
            {
                crashMessageText.Text = DialogTextExtension.Format("Dialog_CrashDuringEditing", logPath);
                restartButton.Content = DialogTextExtension.Get("Dialog_SaveRecoveryRestart");
            }
        }

        private void Restart_Click(object sender, RoutedEventArgs e)
        {
            Forms.Application.Restart();
            Application.Current?.Shutdown();
        }
    }
}
