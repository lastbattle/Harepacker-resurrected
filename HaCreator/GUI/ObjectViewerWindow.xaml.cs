using HaCreator.MapEditor;
using System;
using System.Windows;

namespace HaCreator.GUI
{
    /// <summary>
    /// Floating window for the Object Viewer panel.
    /// </summary>
    public partial class ObjectViewerWindow : Window
    {
        private static ObjectViewerWindow _instance;
        private HaCreatorStateManager _hcsm;
        private bool _isInitialized;

        public ObjectViewerWindow()
        {
            InitializeComponent();
            this.Closing += ObjectViewerWindow_Closing;
        }

        /// <summary>
        /// Gets or creates the singleton instance of the Object Viewer window.
        /// </summary>
        public static ObjectViewerWindow Instance
        {
            get
            {
                if (_instance == null || !_instance.IsLoaded)
                {
                    _instance = new ObjectViewerWindow();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Shows the Object Viewer window, creating it if necessary.
        /// </summary>
        /// <param name="hcsm">The state manager</param>
        /// <param name="owner">The owner window</param>
        /// <param name="isManualOpen">True if user manually opened via button (updates preference)</param>
        public static void ShowWindow(HaCreatorStateManager hcsm, Window owner, bool isManualOpen = true)
        {
            var window = Instance;

            // If manually opened, remember to show on next load
            if (isManualOpen)
            {
                ApplicationSettings.ShowObjectViewerOnLoad = true;
            }

            if (!window.IsVisible)
            {
                window.Owner = owner;
                window._hcsm = hcsm;

                if (!window._isInitialized)
                {
                    window.objectViewerPanel.Initialize(hcsm);
                    window._isInitialized = true;
                }
                else
                {
                    // Refresh content if already initialized
                    window.objectViewerPanel.OnBoardChanged(hcsm.MultiBoard.SelectedBoard);
                }

                // Position near the right side of the owner window
                if (owner != null)
                {
                    window.Left = owner.Left + owner.Width - window.Width - 20;
                    window.Top = owner.Top + 100;
                }

                window.Show();
            }
            else
            {
                window.Activate();
                // Refresh content when activated
                window.objectViewerPanel.OnBoardChanged(hcsm.MultiBoard.SelectedBoard);
            }
        }

        /// <summary>
        /// Hides the window instead of closing it to preserve state.
        /// User closing the window means they don't want it to auto-show next time.
        /// </summary>
        private void ObjectViewerWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Remember that user closed it - don't auto-show next time
            ApplicationSettings.ShowObjectViewerOnLoad = false;

            // Hide instead of close to preserve state
            e.Cancel = true;
            this.Hide();
        }

        /// <summary>
        /// Forces the window to close (for application shutdown).
        /// </summary>
        public static void ForceClose()
        {
            if (_instance != null)
            {
                _instance.Closing -= _instance.ObjectViewerWindow_Closing;
                _instance.Close();
                _instance = null;
            }
        }

        /// <summary>
        /// Refreshes the Object Viewer content.
        /// </summary>
        public void RefreshContent()
        {
            if (_hcsm?.MultiBoard?.SelectedBoard != null)
            {
                objectViewerPanel.OnBoardChanged(_hcsm.MultiBoard.SelectedBoard);
            }
        }
    }
}
