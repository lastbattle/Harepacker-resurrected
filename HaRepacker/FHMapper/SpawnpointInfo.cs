using System;
using System.Windows.Forms;
using MapleLib.WzLib.WzProperties;

namespace Footholds
{
    public partial class SpawnpointInfo : Form
    {
        public SpawnPoint.Spawnpoint spawnpoint;
        public SpawnpointInfo()
        {
            InitializeComponent();
        }

        private void OKBtn_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void SpawnpointInfo_Load(object sender, EventArgs e)
        {
            XLbl.Text = ((WzIntProperty)spawnpoint.Data["x"]).Value.ToString();
            YLbl.Text = ((WzIntProperty)spawnpoint.Data["y"]).Value.ToString();
            MobIDLbl.Text = ((WzStringProperty)spawnpoint.Data["id"]).Value;
            FHIDLbl.Text = ((WzIntProperty)spawnpoint.Data["fh"]).Value.ToString();
        }
    }
}