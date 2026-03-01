using MapleLib.WzLib.WzStructure.Data.CharacterStructure;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HaCreator.GUI.InstanceEditor
{
    /// <summary>
    /// Interaction logic for LoadJobsListSelector.xaml
    /// </summary>
    public partial class LoadJobListSelector : Window
    {
        private bool _bIsLoading = false;
        private bool _bNotUserClosing = false;


        public CharacterJobPreBBType _defaultJobListBitfield;
        /// <summary>
        /// The return bitfield value for the selected class categories
        /// </summary>
        public CharacterJobPreBBType SelectedJobListBitfield
        {
            get { return _defaultJobListBitfield; }
            private set { }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public LoadJobListSelector(CharacterJobPreBBType defaultJobListBitfield)
        {
            InitializeComponent();

            this._defaultJobListBitfield = defaultJobListBitfield;
            Loaded += LoadClassListSelector_Loaded;

            this.Closing += Window_Closing;
        }

        private void LoadClassListSelector_Loaded(object sender, RoutedEventArgs e)
        {
            // Init
            Load();
        }

        /// <summary>
        /// Load
        /// </summary>
        private void Load()
        {
            if (_bIsLoading)
                return;

            _bIsLoading = true;
            try
            {
                var items = comboBox_jobsCategoryList.Items;
                var itemsCount = items.Count;

                for (int i = 0; i < itemsCount; i++)
                {
                    var item = comboBox_jobsCategoryList.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                    if (item != null)
                    {
                        var contentPresenter = FindVisualChild<ContentPresenter>(item);
                        if (contentPresenter != null)
                        {
                            var dataTemplate = contentPresenter.ContentTemplate;
                            CheckBox checkbox = dataTemplate.FindName("checkbox_selectJobCategory", contentPresenter) as CheckBox;

                            CharacterJobPreBBType jobType = (CharacterJobPreBBType)items[i];

                            if (jobType != CharacterJobPreBBType.None)
                            {
                                bool bSet = _defaultJobListBitfield.HasFlag(jobType);
                                if (bSet) // check if the job bitfield is selected
                                {
                                    checkbox.IsChecked = true; // check the checkbox if so
                                }
                            }
                        }
                    }
                }
                textblock_bitfield.Text = _defaultJobListBitfield.ToString();
            }
            finally
            {
                _bIsLoading = false;
            }
        }

        /// <summary>
        /// On window closing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_bNotUserClosing)
            {
                _defaultJobListBitfield = 0;
            }
        }

        /// <summary>
        /// On confirm click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_cfm_Click(object sender, RoutedEventArgs e)
        {
            _bNotUserClosing = true;
            Close();
        }

        /// <summary>
        /// On checkbox checked or unchecked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkbox_selectJobCategory_CheckedUnchecked(object sender, RoutedEventArgs e)
        {
            if (_bIsLoading)
                return;

            var items = comboBox_jobsCategoryList.Items;
            var itemsCount = items.Count;

            CharacterJobPreBBType jobTypesSelectedBitfield = CharacterJobPreBBType.None;

            for (int i = 0; i < itemsCount; i++)
            {
                var item = comboBox_jobsCategoryList.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                if (item != null)
                {
                    var contentPresenter = FindVisualChild<ContentPresenter>(item);
                    if (contentPresenter != null)
                    {
                        var dataTemplate = contentPresenter.ContentTemplate;
                        var checkbox = dataTemplate.FindName("checkbox_selectJobCategory", contentPresenter) as CheckBox;

                        bool isChecked = checkbox.IsChecked ?? false;
                        if (isChecked)
                        {
                            CharacterJobPreBBType jobType = (CharacterJobPreBBType)items[i];

                            if (jobType != CharacterJobPreBBType.None)
                            {
                                // Set the bit corresponding to this job type
                                jobTypesSelectedBitfield |= jobType;
                            }
                        }
                    }
                }
            }
            _defaultJobListBitfield = jobTypesSelectedBitfield;

            // set ui
            textblock_bitfield.Text = _defaultJobListBitfield.ToString();
        }


        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                {
                    return result;
                }
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }
            return null;
        }
    }
}
