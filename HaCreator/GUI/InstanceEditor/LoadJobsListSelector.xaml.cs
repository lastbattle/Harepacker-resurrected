/*Copyright(c) 2024, LastBattle https://github.com/lastbattle/Harepacker-resurrected

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

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
    public partial class LoadJobsListSelector : Window
    {
        private bool _bIsLoading = false;
        private bool _bNotUserClosing = false;


        public long _selectedClassCategoryBitfield;
        /// <summary>
        /// The return bitfield value for the selected class categories
        /// </summary>
        public long SelectedClassCategoryBitfield
        {
            get { return _selectedClassCategoryBitfield; }
            private set { }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public LoadJobsListSelector()
        {
            InitializeComponent();

            this.Closing += Window_Closing;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_bNotUserClosing)
            {
                _selectedClassCategoryBitfield = 0;
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

            long jobTypesSelectedBitfield = 0;

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
                            CharacterClassType classType = (CharacterClassType)items[i];

                            int classTypeBitfieldNum = (int)classType;

                            if (classTypeBitfieldNum != -1 && classType != CharacterClassType.NULL && classType != CharacterClassType.UltimateAdventurer)
                            {

                                // Set the bit corresponding to this job type
                                jobTypesSelectedBitfield |= (1L << classTypeBitfieldNum);
                            }
                        }
                    }
                }
            }
            _selectedClassCategoryBitfield = jobTypesSelectedBitfield;

            // set ui
            textblock_bitfield.Text = _selectedClassCategoryBitfield.ToString();
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
