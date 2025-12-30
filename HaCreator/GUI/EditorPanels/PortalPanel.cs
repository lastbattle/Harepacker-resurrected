using HaCreator.CustomControls;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace HaCreator.GUI.EditorPanels
{
    public partial class PortalPanel : UserControl
    {
        private HaCreatorStateManager hcsm;

        public PortalPanel()
        {
            InitializeComponent();
        }

        public void Initialize(HaCreatorStateManager hcsm)
        {
            this.hcsm = hcsm;

            foreach (PortalType pt in Program.InfoManager.PortalEditor_TypeById)
            {
                try
                {
                    PortalInfo pInfo = PortalInfo.GetPortalInfoByType(pt);
                    if (pInfo == null || pInfo.Image == null)
                        continue;

                    ImageViewer item = portalImageContainer.Add(pInfo.Image, PortalTypeExtensions.GetFriendlyName(pt), true);
                    item.Tag = pInfo;
                    item.MouseDown += new MouseEventHandler(portal_MouseDown);
                    item.MouseUp += new MouseEventHandler(ImageViewer.item_MouseUp);
                }
                catch (KeyNotFoundException)
                {
                }
                catch (Exception)
                {
                    // Skip portals that fail to load
                }
            }
        }

        void portal_MouseDown(object sender, MouseEventArgs e)
        {
            lock (hcsm.MultiBoard)
            {
                hcsm.EnterEditMode(ItemTypes.Portals);
                hcsm.MultiBoard.SelectedBoard.Mouse.SetHeldInfo((PortalInfo)((ImageViewer)sender).Tag);
                hcsm.MultiBoard.Focus();
                ((ImageViewer)sender).IsActive = true;
            }
        }
    }
}
