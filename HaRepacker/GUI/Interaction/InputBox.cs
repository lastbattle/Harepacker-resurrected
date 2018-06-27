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
        public byte length;
        private string value = null;

        public InputBox(string text, string title, byte length = 25)
        {
            this.title = title;
            this.text = text;
            this.length = length;
            InitializeComponent();
            btn_done.Enabled = false;
            txt_input.MaxLength = length;
            ShowDialog();
        }

        private void InputBox_Load(object sender, EventArgs e)
        {
            lb_title.Text = this.title;
            lb_text.Text = this.text;
        }     

        private void btn_cancel_Click(object sender, EventArgs e)
        {            
            Close();
        }
        public string getValue()
        {
            return value;
        }
        private void done()
        {
            bool status = true;
            if (txt_input.Text.Trim() == "")
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
                value = txt_input.Text;
                Close();                
            }
        }

        private void btn_done_Click(object sender, EventArgs e)
        {
            done();            
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
        private int posX = 0, posY = 0;

        private void txt_input_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                Close();
            }          
        }

        private void panel1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                posX = e.X;
                posY = e.Y;
            }else
            {
                Left = Left + (e.X - posX);
                Top = Top + (e.Y - posY);
            }
            
        }
    }
}
