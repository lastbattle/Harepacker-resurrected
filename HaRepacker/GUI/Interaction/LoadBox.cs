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
    public partial class LoadBox : Form
    {
        public Action Progress { get; set; }
        public LoadBox(Action progress)
        {
            InitializeComponent();
            if (progress == null) throw new ArgumentNullException();
            Progress = progress;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);            
            Task.Factory.StartNew(Progress).ContinueWith(t => { pgb_loading.Value++; }, TaskScheduler.FromCurrentSynchronizationContext());            
        }

        private void btn_accept_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
