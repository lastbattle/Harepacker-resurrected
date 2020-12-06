using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
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

namespace HaSharedLibrary.GUI
{
    /// <summary>
    /// Interaction logic for FieldTypePanel.xaml
    /// </summary>
    public partial class FieldTypePanel : UserControl
    {
        private readonly List<string> fieldTypes = new List<string>();

        private bool bIsLoading = false;

        // UI
        private ChangeableTextBox setTextboxOnFieldTypeChange = null;

        public FieldTypePanel()
        {
            InitializeComponent();

            Loaded += FieldTypePanel_Loaded;
        }

        /// <summary>
        /// On load
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FieldTypePanel_Loaded(object sender, RoutedEventArgs e)
        {
            bIsLoading = true;
            try
            {
                // Load from the list of enums in FieldType
                foreach (FieldType fieldType in Enum.GetValues(typeof(FieldType)))
                {
                    fieldTypes.Add(fieldType.ToReadableString());
                }
                // Set binding source
                comboBox_fieldType.ItemsSource = fieldTypes;
            } finally
            {
                bIsLoading = false;
            }
        }


        public void SetTextboxOnFieldTypeChange(ChangeableTextBox setTextboxOnFieldTypeChange)
        {
            this.setTextboxOnFieldTypeChange = setTextboxOnFieldTypeChange;
        }
        public void SetFieldTypeIndex(ulong enumValue)
        {
            bIsLoading = true;
            try
            {
                int i = 0;
                foreach (FieldType fieldType in Enum.GetValues(typeof(FieldType)))
                {
                    if ((ulong) fieldType == enumValue)
                    {
                        comboBox_fieldType.SelectedIndex = i;
                        break;
                    }
                    i++;
                }
            } finally
            {
                bIsLoading = false;
            }
        }


        /// <summary>
        /// On ComboBox selection changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBox_fieldType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0 || comboBox_fieldType.SelectedIndex < 0 || bIsLoading) 
                return;

            int selectedIndex = comboBox_fieldType.SelectedIndex;
            FieldType fieldType = (FieldType) Enum.GetValues(typeof(FieldType)).GetValue(selectedIndex);

            if (setTextboxOnFieldTypeChange != null)
            {
                setTextboxOnFieldTypeChange.Text = ((int)fieldType).ToString(); // the int value of the enum
            }
        }
    }
}
