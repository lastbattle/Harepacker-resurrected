using HaCreator.MapEditor;
using HaCreator.Wz;
using HaSharedLibrary.Wz;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Controls;

namespace HaCreator.GUI.EditorPanels
{
    public partial class CommonPanel : UserControl
    {
        private HaCreatorStateManager hcsm;
        private IReadOnlyList<AssetGalleryItem> commonItems = Array.Empty<AssetGalleryItem>();

        public CommonPanel()
        {
            InitializeComponent();
            EditorPanelLocalizer.Attach(this);
        }

        public void Initialize(HaCreatorStateManager stateManager)
        {
            hcsm = stateManager;
            using (miscItemsContainer.DeferUpdates())
            {
                miscItemsContainer.Clear();
                AddTool("Foothold", WzInfoTools.XNAToDrawingColor(UserSettings.FootholdColor));
                AddTool("Rope", WzInfoTools.XNAToDrawingColor(UserSettings.RopeColor));
                AddTool("Chair", WzInfoTools.XNAToDrawingColor(UserSettings.ChairColor));
                AddTool("Tooltip", WzInfoTools.XNAToDrawingColor(UserSettings.ToolTipColor));
                AddTool("Clock", WzInfoTools.XNAToDrawingColor(UserSettings.MiscColor));
            }
            commonItems = miscItemsContainer.Items;
        }

        private void AddTool(string name, Color color)
        {
            using Bitmap bitmap = CreateColoredBitmap(color);
            miscItemsContainer.Add(bitmap, name, name);
        }

        private static Bitmap CreateColoredBitmap(Color color)
        {
            int containerSize = UserSettings.dotDescriptionBoxSize;
            int dotWidth = Math.Min(UserSettings.DotWidth, containerSize);
            Bitmap result = new(containerSize, containerSize);
            using Graphics graphics = Graphics.FromImage(result);
            using SolidBrush brush = new(color);
            graphics.FillRectangle(brush, new Rectangle(
                (containerSize - dotWidth) / 2,
                (containerSize - dotWidth) / 2,
                dotWidth,
                dotWidth));
            return result;
        }

        private void MiscItemsContainer_ItemActivated(object sender, AssetGalleryItemEventArgs e)
        {
            ActivateTool(e.Item.Tag as string ?? e.Item.Name);
        }

        public void ActivateTool(string toolName)
        {
            if (hcsm?.MultiBoard.SelectedBoard == null || !commonItems.Any(item => item.Name == toolName))
                return;

            lock (hcsm.MultiBoard)
            {
                switch (toolName)
                {
                    case "Foothold":
                        if (!hcsm.MultiBoard.AssertLayerSelected()) return;
                        hcsm.EnterEditMode(ItemTypes.Footholds);
                        hcsm.MultiBoard.SelectedBoard.Mouse.SetFootholdMode();
                        break;
                    case "Rope":
                        if (!hcsm.MultiBoard.AssertLayerSelected()) return;
                        hcsm.EnterEditMode(ItemTypes.Ropes);
                        hcsm.MultiBoard.SelectedBoard.Mouse.SetRopeMode();
                        break;
                    case "Chair":
                        hcsm.EnterEditMode(ItemTypes.Chairs);
                        hcsm.MultiBoard.SelectedBoard.Mouse.SetChairMode();
                        break;
                    case "Tooltip":
                        hcsm.EnterEditMode(ItemTypes.Footholds);
                        hcsm.MultiBoard.SelectedBoard.Mouse.SetTooltipMode();
                        break;
                    case "Clock":
                        hcsm.EnterEditMode(ItemTypes.Misc);
                        hcsm.MultiBoard.SelectedBoard.Mouse.SetClockMode();
                        break;
                    default:
                        return;
                }
                hcsm.MultiBoard.Focus();
            }
        }
    }
}
