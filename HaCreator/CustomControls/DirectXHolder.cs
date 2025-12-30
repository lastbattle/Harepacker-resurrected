using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace HaCreator.CustomControls
{
    public partial class DirectXHolder : UserControl
    {
        public DirectXHolder()
        {
            InitializeComponent();
            SetStyle(ControlStyles.Opaque | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            base.OnKeyDown(new KeyEventArgs(keyData));
            return true;
        }
    }
}
