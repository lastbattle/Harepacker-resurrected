using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Instance;

namespace HaCreator.GUI.InstanceEditor
{
    public partial class TnSelector : EditorBase
    {
        public static string Show(Board board)
        {
            TnSelector ps = new TnSelector(board);
            ps.ShowDialog();
            return ps.result;
        }

        private string result = null;

        public TnSelector(Board board)
        {
            InitializeComponent();

            foreach (PortalInstance pi in board.BoardItems.Portals)
            {
                if (pi.pn != null && pi.pn != "" && pi.pn != "sp" && pi.pn != "pt")
                    pnList.Items.Add(pi.pn);
            }
        }

        protected override void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        protected override void okButton_Click(object sender, EventArgs e)
        {
            result = (string)pnList.SelectedItem;
            Close();
        }
    }
}
