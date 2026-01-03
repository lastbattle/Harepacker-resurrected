using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MapleLib.Img;

namespace HaRepacker.GUI.HotSwap
{
    /// <summary>
    /// Represents a pending file modification notification
    /// </summary>
    public class FileModificationInfo
    {
        public string FilePath { get; set; }
        public ImgChangeType ChangeType { get; set; }
        public DateTime DetectedAt { get; set; }
        public bool HasLocalChanges { get; set; }
        public string OldPath { get; set; }

        public string FileName => Path.GetFileName(FilePath);

        public string DisplayMessage
        {
            get
            {
                switch (ChangeType)
                {
                    case ImgChangeType.ContentChanged:
                    case ImgChangeType.SizeChanged:
                        return $"{FileName} reloaded";
                    case ImgChangeType.Deleted:
                        return $"{FileName} removed";
                    case ImgChangeType.Added:
                        return $"{FileName} added";
                    case ImgChangeType.Renamed:
                        return $"{Path.GetFileName(OldPath)} renamed to {FileName}";
                    default:
                        return $"{FileName} updated";
                }
            }
        }
    }

    /// <summary>
    /// User's response to a modification notification
    /// </summary>
    public enum NotificationResponse
    {
        Reload,
        Ignore,
        IgnoreAll,
        KeepLocal,
        AddToTree
    }

    /// <summary>
    /// Event args for when user responds to a notification
    /// </summary>
    public class NotificationResponseEventArgs : EventArgs
    {
        public FileModificationInfo Modification { get; }
        public NotificationResponse Response { get; }

        public NotificationResponseEventArgs(FileModificationInfo modification, NotificationResponse response)
        {
            Modification = modification;
            Response = response;
        }
    }

    /// <summary>
    /// A subtle notification bar that briefly shows hot-swap status messages
    /// </summary>
    public class HotSwapNotificationBar : UserControl
    {
        private Label _messageLabel;
        private Timer _hideTimer;
        private const int DisplayDurationMs = 3000;

        public event EventHandler<NotificationResponseEventArgs> UserResponse;

        public HotSwapNotificationBar()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Height = 24;
            this.Dock = DockStyle.Top;
            this.BackColor = Color.FromArgb(230, 245, 230); // Light green for success
            this.Visible = false;
            this.Padding = new Padding(8, 0, 8, 0);

            _messageLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(40, 80, 40),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            this.Controls.Add(_messageLabel);

            _hideTimer = new Timer { Interval = DisplayDurationMs };
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer.Stop();
                this.Visible = false;
            };

            this.ResumeLayout(false);
        }

        /// <summary>
        /// Shows a brief notification message
        /// </summary>
        public void ShowMessage(string message, bool isError = false)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ShowMessage(message, isError)));
                return;
            }

            _messageLabel.Text = message;
            this.BackColor = isError
                ? Color.FromArgb(255, 230, 230)  // Light red for errors
                : Color.FromArgb(230, 245, 230); // Light green for success
            _messageLabel.ForeColor = isError
                ? Color.FromArgb(120, 40, 40)
                : Color.FromArgb(40, 80, 40);

            this.Visible = true;
            this.BringToFront();

            _hideTimer.Stop();
            _hideTimer.Start();
        }

        /// <summary>
        /// Queue a file modification notification (auto-handled, just shows message)
        /// </summary>
        public void QueueNotification(FileModificationInfo modification)
        {
            if (modification == null)
                return;

            ShowMessage(modification.DisplayMessage);
        }

        /// <summary>
        /// Clear all pending notifications
        /// </summary>
        public void ClearAll()
        {
            _hideTimer.Stop();
            this.Visible = false;
        }

        public void ResetIgnoreAllSession() { }

        public int PendingCount => 0;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hideTimer?.Stop();
                _hideTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
