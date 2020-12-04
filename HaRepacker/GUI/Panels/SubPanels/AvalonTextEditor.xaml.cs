using ICSharpCode.AvalonEdit.Highlighting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace HaRepacker.GUI.Panels.SubPanels
{
    /// <summary>
    /// Interaction logic for AvalonTextEditor.xaml
    /// Providing a very very basic text editor for the purpose of Etc.wz/Script only. 
    /// 
    /// For more information: https://github.com/Dirkster99/AvalonEdit-Samples
    /// </summary>
    public partial class AvalonTextEditor : UserControl,  INotifyPropertyChanged
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public AvalonTextEditor()
        {
            InitializeComponent();

            // highlighting manager
            _highlightingManager = HighlightingManager.Instance;
            this._HighlightingDefinition = _highlightingManager.HighlightingDefinitions[2]; // default to javascript

            // data binding context
            this.DataContext = this;
        }

        #region Data Context
        private readonly HighlightingManager _highlightingManager;
        private IHighlightingDefinition _HighlightingDefinition;
        /// <summary>
        /// AvalonEdit exposes a Highlighting property that controls whether keywords,
        /// comments and other interesting text parts are colored or highlighted in any
        /// other visual way. This property exposes the highlighting information for the
        /// text file managed in this viewmodel class.
        /// </summary>
        public IHighlightingDefinition HighlightingDefinition
        {
            get
            {
                return _HighlightingDefinition;
            }

            set
            {
                if (_HighlightingDefinition != value)
                {
                    _HighlightingDefinition = value;
                    OnPropertyChanged("HighlightingDefinition");
                }
            }
        }

        /// <summary>
        /// Gets a copy of all highlightings.
        /// </summary>
        public ReadOnlyCollection<IHighlightingDefinition> HighlightingDefinitions
        {
            get
            {
                if (_highlightingManager != null)
                    return _highlightingManager.HighlightingDefinitions;

                return null;
            }
        }
        #endregion

        #region Property Changed
        /// <summary>
        /// Property changed event handler to trigger update UI
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Events
        /// <summary>
        /// Syntax type selection combobox changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBox_SyntaxHighlightingType_SelectionChanged_1(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
                return;

            IHighlightingDefinition definition = e.AddedItems[0] as IHighlightingDefinition;
            if (definition != null)
            {
                HighlightingDefinition = definition;
            }
        }

        public event EventHandler SaveButtonClicked;

        /// <summary>
        /// Save button clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_saveApply_Click(object sender, RoutedEventArgs e)
        {
            if (SaveButtonClicked != null)
            {
                SaveButtonClicked.Invoke(sender, e);
            }
            button_saveApply.IsEnabled = false; // set to disabled whenever save is clicked
        }

        /// <summary>
        /// Texteditor, text changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textEditor_TextChanged(object sender, EventArgs e)
        {
            // set to enabled whenever any text is changed
            if (!button_saveApply.IsEnabled)
            {
                button_saveApply.IsEnabled = true;
            }
        }
        #endregion
    }
}
