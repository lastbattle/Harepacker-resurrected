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

        public HaEditor()
        {
            InitializeComponent();

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

            int newHeight = (int)(e.NewSize.Height * 0.75);

            tilePanelHost.Height = newHeight;
            objPanelHost.Height = newHeight;
            lifePanelHost.Height = newHeight;
            portalPanelHost.Height = newHeight;
            bgPanelHost.Height = newHeight;
            commonPanelHost.Height = newHeight;
        }

        private void HaEditor2_Loaded(object sender, RoutedEventArgs e)
        {
            // helper classes
            handler = new InputHandler(multiBoard);
            hcsm = new HaCreatorStateManager(multiBoard, ribbon, tabControl1, handler, textblock_CursorX, textblock_CursorY, textblock_RCursorX, textblock_RCursorY, textblock_selectedItem);
            hcsm.CloseRequested += hcsm_CloseRequested;
            hcsm.FirstMapLoaded += hcsm_FirstMapLoaded;

            tilePanel.Initialize(hcsm);
            objPanel.Initialize(hcsm);
            lifePanel.Initialize(hcsm);
            portalPanel.Initialize(hcsm);
            bgPanel.Initialize(hcsm);
            commonPanel.Initialize(hcsm);

            if (!hcsm.backupMan.AttemptRestore())
            {
                hcsm.LoadMap(new Load(multiBoard, tabControl1, hcsm.MakeRightClickHandler()));
            }
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

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            Expander expanderSrc = sender as Expander;
            UIElement childContent = expanderSrc.Content as UIElement;

            childContent.Visibility = Visibility.Visible;
        }

        private void Expander_Collapsed(object sender, RoutedEventArgs e)
        {
            Expander expanderSrc = sender as Expander;
            UIElement childContent = expanderSrc.Content as UIElement;
  
            childContent.Visibility = Visibility.Collapsed; // collapse when its not needed, speed up the performance here
        }
    }
}
