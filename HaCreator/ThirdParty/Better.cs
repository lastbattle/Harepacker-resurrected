/* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows.Forms;
using System.Drawing;
using System.Data;


namespace HaCreator.ThirdParty
{
    //Show the CheckedListBox icon in the Toolbox for our component
    [ToolboxBitmap(typeof(CheckedListBox))]
    public class BetterCheckedListBox : System.Windows.Forms.CheckedListBox
    {
        //----------------------------------------------------------
        //  Class level variables 
        //----------------------------------------------------------
        bool AllowChecks;
        //Controls whether checkstate can be changed
        string ChkMember;
        //The DataColumn to use for checkstate
        DataTable dt;
        //Data to display

        //----------------------------------------------------------
        //  Bug Fix
        //----------------------------------------------------------
        //  When the CheckedListBox control is in a Tabcontrol 
        //  and that the Datasource property is used to fill
        //  up the item list, setting the clb's visible property
        //  to false, then to true, or flipping the tabs would
        //  cause the clb to "forget" the checks.
        //
        //  Implement Carl Mercier's workaround
        //  http://www.codeproject.com/cs/combobox/FixedCheckedListBox.asp
        //----------------------------------------------------------
        public new object DataSource
        {
            //Return our datatable variable
            get { return (object)dt; }
            set
            {
                //Set our datatable variable
                dt = (DataTable)value;
                LoadData();
            }
        }
        private void LoadData()
        {
            var bufAllowChecks = AllowChecks;
            if (AllowChecks == false)
            {
                //This is needed so we can change checkstates
                AllowChecks = true;
            }
            //Clear items
            base.Items.Clear();
            //Fill it again
            int i = 0;
            for (i = 0; i <= dt.DefaultView.Count - 1; i++)
            {
                //Determine whether to check each item or not
                if ((string)dt.Rows[i][ChkMember] == "1" | (string)dt.Rows[i][ChkMember] == "True")
                {
                    base.Items.Add(dt.DefaultView[i], true);
                }
                else
                {
                    base.Items.Add(dt.DefaultView[i], false);
                }
            }
            AllowChecks = bufAllowChecks;
        }
        //Added or deleted records won't show without a refresh
        public override void Refresh()
        {
            LoadData();
        }

        //----------------------------------------------------------
        //  Allow / Lock Checkstate Functionality
        //----------------------------------------------------------
        //  Only let users check or uncheck items when you let them. 
        //  Note that you can't programmatically check or uncheck
        //  items either unless AllowChecks is True.
        //
        //  This is an emulation of the Checkbox control's AutoCheck
        //  property.
        //----------------------------------------------------------
        //Show our property under the Behavior section of the Properties window
        //So that it can be set at design time
        [System.ComponentModel.Description("Allow checkstate to be changed"), System.ComponentModel.Category("Behavior")]
        public bool AutoCheck
        {
            //Return our checkstate variable
            get { return AllowChecks; }
            //Set our checkstate variable
            set { AllowChecks = value; }
        }
        //Override the ItemCheck event with our own
        protected override void OnItemCheck(System.Windows.Forms.ItemCheckEventArgs ice)
        {
            //Allow checks to be changed only if AutoCheck = True
            if (AllowChecks == false)
            {
                ice.NewValue = ice.CurrentValue;
            }
        }

        //----------------------------------------------------------
        //  Reference Item By It's Index Functionality
        //----------------------------------------------------------
        //  Wouldn't it be nice if you could reference items in a
        //  CheckedListBox control just like you can with a 
        //  ComboBox? Well now you can!
        //
        //  Obtain Checkstatus, Text, and Values via an items index
        //----------------------------------------------------------
        [System.ComponentModel.Description("Gets or sets the CheckMember")]
        public string CheckMember
        {
            get { return ChkMember; }
            set { ChkMember = value; }
        }

        public bool Checked(int index)
        {
            //Search for the item in CheckedIndices to see if its checked or not
            if (base.CheckedIndices.IndexOf(index) != -1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void SetChecked(int index, bool value)
        {
            var bufAllowChecks = AllowChecks;
            if (AllowChecks == false)
            {
                AllowChecks = true;
            }
            base.SetItemChecked(index, value);
            AllowChecks = bufAllowChecks;
        }

        public new string Text(int index)
        {
            return (string)dt.Rows[index][base.DisplayMember];
        }

        public void SetText(int index, string value)
        {
            dt.Rows[index][base.DisplayMember] = value;
        }

        public string Value(int index)
        {
            return (string)dt.Rows[index][base.ValueMember];
        }

        public void SetValue(int index, string value)
        {
            dt.Rows[index][base.ValueMember] = value;
        }
    }
}