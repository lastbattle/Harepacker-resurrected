/* Copyright (C) 2018 LastBattle
https://github.com/eaxvac/Harepacker-resurrected
*/

using System;
using System.IO;
using System.Windows.Forms;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;

namespace HaRepacker.GUI.Panels.SubPanels
{
    public partial class FieldLimitPanel : UserControl
    {
        // UI
        private TextBox setTextboxOnFieldLimitChange = null;
        private ChangeableTextBox setTextboxOnFieldLimitChange_wpf = null;

        // misc
        private bool initializingListViewForFieldLimit = false;


        public FieldLimitPanel()
        {
            InitializeComponent();

            Load += FieldLimitPanel_Load;
        }

        /// <summary>
        /// On loaded
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FieldLimitPanel_Load(object sender, EventArgs e)
        {
            PopulateDefaultListView();
        }

        public void SetTextboxOnFieldLimitChange(TextBox setTextboxOnFieldLimitChange)
        {
            this.setTextboxOnFieldLimitChange = setTextboxOnFieldLimitChange;
        }
        public void SetTextboxOnFieldLimitChange(ChangeableTextBox setTextboxOnFieldLimitChange_wpf)
        {
            this.setTextboxOnFieldLimitChange_wpf = setTextboxOnFieldLimitChange_wpf;
        }

        /// <summary>
        /// Update the checkboxes upon selection of a 'fieldLimit' WzIntProperty
        /// </summary>
        public void UpdateFieldLimitCheckboxes(WzIntProperty propertyValue)
        {
            initializingListViewForFieldLimit = true;

            // Fill checkboxes
            //int maxFieldLimitType = FieldLimitTypeExtension.GetMaxFieldLimitType();
            foreach (ListViewItem item in listView_fieldLimitType.Items)
            {
                item.Checked = FieldLimitTypeExtension.Check((int)item.Tag, propertyValue.GetLong());
            }
            initializingListViewForFieldLimit = false;
            ListView_fieldLimitType_ItemChecked(listView_fieldLimitType, null);
        }

        /// <summary>
        /// Populates the default values based upon hard coded WzFieldLimitType list
        /// </summary>
        private void PopulateDefaultListView()
        {
            initializingListViewForFieldLimit = true;

            // Populate FieldLimitType
            if (listView_fieldLimitType.Items.Count == 0)
            {
                // dummy column
                listView_fieldLimitType.Columns.Add(new ColumnHeader()
                {
                    Text = "",
                    Name = "col1",
                    Width = 450,
                });

                int i_index = 0;
                foreach (WzFieldLimitType limitType in Enum.GetValues(typeof(WzFieldLimitType)))
                {
                    ListViewItem item1 = new ListViewItem(
                        string.Format("{0} - {1}", (i_index).ToString(), limitType.ToString().Replace("_", " ")));
                    item1.Tag = limitType; // starts from 0
                    listView_fieldLimitType.Items.Add(item1);

                    i_index++;
                }
                for (int i = i_index; i < i_index + 30; i++) // add 30 dummy values, we really dont have the field properties of future MS versions :( 
                {
                    ListViewItem item1 = new ListViewItem(string.Format("{0} - UNKNOWN", (i).ToString()));
                    item1.Tag = i;
                    listView_fieldLimitType.Items.Add(item1);
                }
            }

            initializingListViewForFieldLimit = false;
        }

        /// <summary>
        /// On WzFieldLimitType listview item checked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListView_fieldLimitType_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (initializingListViewForFieldLimit)
                return;

            //System.Diagnostics.Debug.WriteLine("Set index at  " + e.Index + " to " + listView_fieldLimitType.Items[e.Index].Checked);
        }

        /// <summary>
        /// On WzFieldLimitType listview item checked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListView_fieldLimitType_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (initializingListViewForFieldLimit)
                return;

            ulong fieldLimit = 0;
            foreach (ListViewItem item in listView_fieldLimitType.Items)
            {
                if (item.Checked)
                {
                    int numShift = ((int)item.Tag);

                    //System.Diagnostics.Debug.WriteLine("Selected " + numShift + ", " + (long)(1L << numShift));
                    fieldLimit |= (ulong)(1L << numShift);
                }
            }
            //System.Diagnostics.Debug.WriteLine("Result " + fieldLimit);
            if (setTextboxOnFieldLimitChange != null)
                setTextboxOnFieldLimitChange.Text = fieldLimit.ToString();
            if (setTextboxOnFieldLimitChange_wpf != null)
                setTextboxOnFieldLimitChange_wpf.Text = fieldLimit.ToString();
        }
    }
}
