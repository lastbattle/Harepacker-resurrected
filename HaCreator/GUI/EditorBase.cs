using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaCreator.GUI
{
    public partial class EditorBase : Form
    {
        public EditorBase()
        {
            InitializeComponent();
        }

        protected virtual void InstanceEditorBase_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            if (e.KeyCode == Keys.Escape)
            {
                cancelButton_Click(null, null);
            }
            else if (e.KeyCode == Keys.Enter)
            {
                okButton_Click(null, null);
            }
            else
            {
                e.Handled = false;
            }
        }

        protected virtual void cancelButton_Click(object sender, EventArgs e)
        {

        }

        protected virtual void okButton_Click(object sender, EventArgs e)
        {

        }
    }
}
