using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;

namespace HaCreator.GUI.Input
{
    public partial class NameValueInput : Form
    {
        public int SelectedValue { get; set; }
        public string SelectedName { get; set; }

        /// <summary>
        /// Delegate for validation
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns>string.empty if all is fine, otherwise return the reason</returns>
        public delegate string ValidationCallback(string name, int value);
        // Event handler for validation
        private ValidationCallback _validationCallback;


        /// <summary>
        /// Constructor
        /// </summary>
        public NameValueInput(ValidationCallback validationCallback)
        {
            this._validationCallback = validationCallback;

            this.SelectedValue = 0;
            this.SelectedName = null;

            InitializeComponent();
        }

        #region Window
        public void SetWindowInfo(string overrideNameLabel, string overrideValueLabel, string overrideTitle)
        {
            if (overrideNameLabel != null)
            {
                label_name.Text = overrideNameLabel;
            }
            if (overrideValueLabel != null)
            {
                label_value.Text = overrideValueLabel;
            }
            if (overrideTitle != null)
            {
                Text = overrideTitle;
            }
        }

        /// <summary>
        /// Form window keydown
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Load_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close(); // close window
            }
            else if (e.KeyCode == Keys.Enter)
            {
                //loadButton_Click(null, null);
            }
        }
        #endregion

        #region UI events
        /// <summary>
        /// On OK button clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_ok_Click(object sender, EventArgs e)
        {
            // validate and close

            string nameInput = textBox_input.Text;
            int valueInput =  (int) numericUpDown_input.Value;
            if (valueInput < 0 && valueInput > int.MaxValue)
            {
                MessageBox.Show("Please enter a valid integer value.", "Invalid Input",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Call the validation callback
            string validationCallbackError = _validationCallback(nameInput, valueInput);
            if (_validationCallback != null && validationCallbackError == string.Empty)
            {
                SelectedName = nameInput;
                SelectedValue = valueInput;

                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show(validationCallbackError, "Validation Failed",MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// On cancel button clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_cancel_Click(object sender, EventArgs e)
        {
            this.SelectedValue = 0;
            this.SelectedName = null;

            DialogResult = DialogResult.Cancel;
            Close();
        }
        #endregion
    }
}
