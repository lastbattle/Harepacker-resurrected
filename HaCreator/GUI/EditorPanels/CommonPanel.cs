using HaCreator.CustomControls;
using HaCreator.MapEditor;
using HaCreator.Wz;
using HaSharedLibrary.Wz;
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
    public partial class CommonPanel : UserControl
    {
        private HaCreatorStateManager hcsm;

        public CommonPanel()
        {
            InitializeComponent();
        }

        public void Initialize(HaCreatorStateManager hcsm)
        {
            this.hcsm = hcsm;

            ImageViewer[] commonItems = new ImageViewer[] {
                miscItemsContainer.Add(CreateColoredBitmap(WzInfoTools.XNAToDrawingColor(UserSettings.FootholdColor)), "Foothold", true),
                miscItemsContainer.Add(CreateColoredBitmap(WzInfoTools.XNAToDrawingColor(UserSettings.RopeColor)), "Rope", true),
                miscItemsContainer.Add(CreateColoredBitmap(WzInfoTools.XNAToDrawingColor(UserSettings.ChairColor)), "Chair", true),
                miscItemsContainer.Add(CreateColoredBitmap(WzInfoTools.XNAToDrawingColor(UserSettings.ToolTipColor)), "Tooltip", true),
                miscItemsContainer.Add(CreateColoredBitmap(WzInfoTools.XNAToDrawingColor(UserSettings.MiscColor)), "Clock", true)
            };
            foreach (ImageViewer item in commonItems)
            {
                item.MouseDown += new MouseEventHandler(commonItem_Click);
                item.MouseUp += new MouseEventHandler(ImageViewer.item_MouseUp);
            }
        }

        private Bitmap CreateColoredBitmap(Color color)
        {
            int containerSize = UserSettings.dotDescriptionBoxSize;
            int DotWidth = Math.Min(UserSettings.DotWidth, containerSize);
            Bitmap result = new Bitmap(containerSize, containerSize);
            using (Graphics g = Graphics.FromImage(result))
                g.FillRectangle(new SolidBrush(color), new Rectangle((containerSize / 2) - (DotWidth / 2), (containerSize / 2) - (DotWidth / 2), DotWidth, DotWidth));
            return result;
        }

        void commonItem_Click(object sender, MouseEventArgs e)
        {
            lock (hcsm.MultiBoard)
            {
                ImageViewer item = (ImageViewer)sender;
                switch (item.Name)
                {
                    case "Foothold":
                        if (!hcsm.MultiBoard.AssertLayerSelected())
                        {
                            return;
                        }
                        hcsm.EnterEditMode(ItemTypes.Footholds);
                        hcsm.MultiBoard.SelectedBoard.Mouse.SetFootholdMode();
                        hcsm.MultiBoard.Focus();
                        break;
                    case "Rope":
                        if (!hcsm.MultiBoard.AssertLayerSelected())
                        {
                            return;
                        }
                        hcsm.EnterEditMode(ItemTypes.Ropes);
                        hcsm.MultiBoard.SelectedBoard.Mouse.SetRopeMode();
                        hcsm.MultiBoard.Focus();
                        break;
                    case "Chair":
                        hcsm.EnterEditMode(ItemTypes.Chairs);
                        hcsm.MultiBoard.SelectedBoard.Mouse.SetChairMode();
                        hcsm.MultiBoard.Focus();
                        break;
                    case "Tooltip":
                        hcsm.EnterEditMode(ItemTypes.Footholds);
                        hcsm.MultiBoard.SelectedBoard.Mouse.SetTooltipMode();
                        hcsm.MultiBoard.Focus();
                        break;
                    case "Clock":
                        hcsm.EnterEditMode(ItemTypes.Misc);
                        hcsm.MultiBoard.SelectedBoard.Mouse.SetClockMode();
                        hcsm.MultiBoard.Focus();
                        break;
                }
                item.IsActive = true;
            }
        }
    }
}
