using HaCreator.GUI.EditorPanels;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace HaCreator.GUI
{
    /// <summary>
    /// Interaction logic for HaEditor2.xaml
    /// </summary>
    public partial class HaEditor : Window
    {
        private InputHandler handler;
        public HaCreatorStateManager hcsm;

        private TilePanel tilePanel;
        private ObjPanel objPanel;
        private LifePanel lifePanel;
        private PortalPanel portalPanel;
        private BackgroundPanel bgPanel;
        private CommonPanel commonPanel;

        public HaEditor()
        {
            InitializeComponent();
            InitializeComponentCustom();

            Program.HaEditorWindow = this;

            this.Loaded += HaEditor2_Loaded;
            this.Closed += HaEditor2_Closed;
            this.StateChanged += HaEditor2_StateChanged;
        }

        /// <summary>
        /// On window state changed
        /// Normal, Minimized, Maximized
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HaEditor2_StateChanged(object sender, EventArgs e)
        {
            multiBoard.UpdateWindowState(this.WindowState);
        }

        /// <summary>
        /// Window size change
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            multiBoard.UpdateWindowSize(e.NewSize);
        }

        private void HaEditor2_Loaded(object sender, RoutedEventArgs e)
        {
            // This has to be here and not in .ctor for some reason, otherwise subwindows are not locating properly
            tilePanel = new TilePanel(hcsm) { Enabled = false };
            objPanel = new ObjPanel(hcsm) { Enabled = false };
            lifePanel = new LifePanel(hcsm) { Enabled = false };
            portalPanel = new PortalPanel(hcsm) { Enabled = false };
            bgPanel = new BackgroundPanel(hcsm) { Enabled = false };
            commonPanel = new CommonPanel(hcsm) { Enabled = false };

            // Obj panel

            List<System.Windows.Forms.UserControl> dockContents = new List<System.Windows.Forms.UserControl> { tilePanel, objPanel, lifePanel, portalPanel, bgPanel, commonPanel };
            foreach (System.Windows.Forms.UserControl dockContent in dockContents)
            {
                dockContent.Show();

                WindowsFormsHost formsHost = new WindowsFormsHost();
                formsHost.Child = dockContent;
                DockPanel.SetDock(formsHost, Dock.Right | Dock.Top);
                dockPanel.Children.Add(formsHost);
            }

           // commonPanel.Pane = bgPanel.Pane = portalPanel.Pane = lifePanel.Pane = objPanel.Pane = tilePanel.Pane;

            if (!hcsm.backupMan.AttemptRestore())
                hcsm.LoadMap(new Load(multiBoard, tabControl1, hcsm.MakeRightClickHandler()));
        }

        private void InitializeComponentCustom()
        {
            // helper classes
            handler = new InputHandler(multiBoard);
            hcsm = new HaCreatorStateManager(multiBoard, ribbon, tabControl1, handler);
            hcsm.CloseRequested += hcsm_CloseRequested;
            hcsm.FirstMapLoaded += hcsm_FirstMapLoaded;
        }

        /// <summary>
        /// Mouse wheel
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var el = (UIElement)sender;

            if (multiBoard.TriggerMouseWheel(e, el))
            {
                base.OnMouseWheel(e);
            }
        }

        void hcsm_CloseRequested()
        {
            Close();
        }

        void hcsm_FirstMapLoaded()
        {
            tilePanel.Enabled = true;
            objPanel.Enabled = true;
            lifePanel.Enabled = true;
            portalPanel.Enabled = true;
            bgPanel.Enabled = true;
            commonPanel.Enabled = true;

            WindowState = WindowState.Maximized;
        }

        /// <summary>
        /// On window closing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HaEditor2_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!Program.Restarting && System.Windows.MessageBox.Show("Are you sure you want to quit?", "Quit", MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                e.Cancel = true;
            }
            else
            {
                // Thread safe without locks since reference assignment is atomic
                Program.AbortThreads = true;
            }
        }

        /// <summary>
        /// On form close
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HaEditor2_Closed(object sender, EventArgs e)
        {
            multiBoard.Stop();
        }
    }
}
