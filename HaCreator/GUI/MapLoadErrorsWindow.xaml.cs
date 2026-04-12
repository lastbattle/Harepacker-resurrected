using MapleLib.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace HaCreator.GUI
{
    /// <summary>
    /// Floating window that displays map loading errors captured by ErrorLogger.
    /// </summary>
    public partial class MapLoadErrorsWindow : Window
    {
        private static MapLoadErrorsWindow _instance;
        private string _logFilePath;

        public MapLoadErrorsWindow()
        {
            InitializeComponent();
        }

        private static MapLoadErrorsWindow Instance
        {
            get
            {
                if (_instance == null || !_instance.IsLoaded)
                {
                    _instance = new MapLoadErrorsWindow();
                }

                return _instance;
            }
        }

        public static void ShowWindow(
            Window owner,
            string mapIdentifier,
            IReadOnlyDictionary<ErrorLevel, List<Error>> errorSnapshot,
            string logFilePath)
        {
            if (errorSnapshot == null || errorSnapshot.Count == 0)
            {
                return;
            }

            var window = Instance;
            window.Owner = owner;
            window.LoadErrors(mapIdentifier, errorSnapshot, logFilePath);
            bool shouldActivate = !IsMapSimulatorActive(owner);

            if (owner != null)
            {
                window.Left = owner.Left + Math.Max(20, owner.Width - window.Width - 20);
                window.Top = owner.Top + 120;
            }

            if (!window.IsVisible)
            {
                window.ShowActivated = shouldActivate;
                window.Show();
            }
            else if (shouldActivate)
            {
                window.Activate();
            }
        }

        public static void ForceClose()
        {
            if (_instance == null)
            {
                return;
            }

            _instance.Close();
            _instance = null;
        }

        private void LoadErrors(
            string mapIdentifier,
            IReadOnlyDictionary<ErrorLevel, List<Error>> errorSnapshot,
            string logFilePath)
        {
            _logFilePath = logFilePath;
            summaryTextBlock.Text = BuildSummary(mapIdentifier, errorSnapshot);
            errorsTextBox.Text = BuildErrorText(errorSnapshot);
            errorsTextBox.ScrollToHome();
            logPathTextBlock.Text = string.IsNullOrEmpty(logFilePath)
                ? string.Empty
                : $"Saved to: {Path.GetFileName(logFilePath)}";
            openLogButton.IsEnabled = !string.IsNullOrEmpty(logFilePath) && File.Exists(logFilePath);
        }

        private static string BuildSummary(
            string mapIdentifier,
            IReadOnlyDictionary<ErrorLevel, List<Error>> errorSnapshot)
        {
            int totalErrors = errorSnapshot.Values.Sum(errors => errors.Count);
            string mapLabel = string.IsNullOrWhiteSpace(mapIdentifier) ? "the current map" : mapIdentifier;
            return $"HaCreator loaded {mapLabel} with {totalErrors} issue(s). Missing objects, tiles, or other assets are listed below.";
        }

        private static string BuildErrorText(IReadOnlyDictionary<ErrorLevel, List<Error>> errorSnapshot)
        {
            var sb = new StringBuilder();

            foreach (var group in errorSnapshot.OrderBy(kvp => kvp.Key))
            {
                sb.AppendLine($"=== {group.Key} ===");

                foreach (var error in group.Value.OrderBy(err => err.Timestamp))
                {
                    sb.Append('[')
                      .Append(error.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff"))
                      .Append("] ")
                      .AppendLine(error.Message);
                }

                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(errorsTextBox.Text))
            {
                Clipboard.SetText(errorsTextBox.Text);
            }
        }

        private void OpenLogButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_logFilePath) || !File.Exists(_logFilePath))
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = _logFilePath,
                UseShellExecute = true
            });
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private static bool IsMapSimulatorActive(Window owner)
        {
            return owner is HaEditor editor &&
                   editor.hcsm?.MultiBoard != null &&
                   !editor.hcsm.MultiBoard.DeviceReady;
        }
    }
}
