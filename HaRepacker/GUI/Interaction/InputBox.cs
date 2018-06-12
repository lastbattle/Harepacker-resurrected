using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaRepacker.GUI.Interaction
{
    public partial class InputBox : Form
    {
        private string title = "Title";
        private string text = "Text";
        private string input = "";
        public byte length;
        private TabControl tabControl;
        private TabPage tabPage;

        public InputBox(string title, string text, byte length = 25)
        {
            this.title = title;
            this.text = text;
            this.length = length;
            InitializeComponent();
            btn_done.Enabled = false;
            txt_input.MaxLength = length;
        }

        private void InputBox_Load(object sender, EventArgs e)
        {
            lb_title.Text = this.title;
            lb_text.Text = this.text;
        }     

        private void btn_cancel_Click(object sender, EventArgs e)
        {
            this.input = "";
            Close();
        }

        public void tab(TabControl tabControl, TabPage tabPage)
        {
            this.tabControl = tabControl;
            this.tabPage = tabPage;
        }

        private void addTab()
        {
            this.tabPage.Text = this.input;
            this.tabControl.TabPages.Add(this.tabPage);            
        }

        private void btn_done_Click(object sender, EventArgs e)
        {            
            bool status = true;
            if (txt_input.Text == "")
            {
                lb_error.Text = "Error, complete field...";
                status = false;
            }
            if (txt_input.Text.Length > length)
            {
                lb_error.Text = "Error, max character " + length;
                status = false;
            }
            if (status)
            {
                this.input = txt_input.Text;
                this.addTab();
                Close();
            }
            
        }

        private void txt_input_TextChanged(object sender, EventArgs e)
        {
            if(txt_input.Text.Length > 0)
            {
                btn_done.Enabled = true;
            }else
            {
                btn_done.Enabled = false;
            }
        }
    }
}
