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
        private string input = "0";
        private TabControl tabControl;
        private TabPage tabPage;

        public InputBox(string title, string text)
        {
            this.title = title;
            this.text = text;
            InitializeComponent();
        }

        private void InputBox_Load(object sender, EventArgs e)
        {
            lb_title.Text = this.title;
            lb_text.Text = this.text;
        }     

        private void btn_cancel_Click(object sender, EventArgs e)
        {
            this.input = "0";
            Close();
        }

        public void tab(TabControl tabControl, TabPage tabPage)
        {
            this.tabControl = tabControl;
            this.tabPage = tabPage;
        }

        private void addTab()
        {
            if (this.input == "0") return;
            this.tabPage.Text = this.input;
            this.tabControl.TabPages.Add(this.tabPage);
            return;
        }

        private void btn_done_Click(object sender, EventArgs e)
        {
            bool status = true;
            if (txt_input.Text == "")
            {
                lb_text.Text += ": error, complete field...";
                status = false;
            }
            if (txt_input.Text.Length >= 35)
            {
                lb_text.Text += ": error, max character 35";
                status = false;
            }
            if (status)
            {
                this.input = txt_input.Text;
                this.addTab();
                Close();
            }
            
        }
    }
}
