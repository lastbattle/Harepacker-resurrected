using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace HaCreator.GUI.EditorPanels
{
    public partial class PortalPanel : UserControl
    {
        private HaCreatorStateManager hcsm;

        public PortalPanel()
        {
            InitializeComponent();
            EditorPanelLocalizer.Attach(this);
        }

        public void Initialize(HaCreatorStateManager stateManager)
        {
            hcsm = stateManager;
            using (portalImageContainer.DeferUpdates())
            {
                portalImageContainer.Clear();
                foreach (PortalType portalType in Program.InfoManager.PortalEditor_TypeById)
                {
                    try
                    {
                        PortalInfo info = PortalInfo.GetPortalInfoByType(portalType);
                        if (info?.Image != null)
                            portalImageContainer.Add(info.Image, PortalTypeExtensions.GetFriendlyName(portalType), info);
                    }
                    catch (KeyNotFoundException)
                    {
                    }
                    catch (Exception)
                    {
                        // A broken portal asset should not prevent the remaining types from loading.
                    }
                }
            }
        }

        private void PortalImageContainer_ItemActivated(object sender, AssetGalleryItemEventArgs e)
        {
            if (hcsm?.MultiBoard.SelectedBoard == null || e.Item.Tag is not PortalInfo info)
                return;

            lock (hcsm.MultiBoard)
            {
                hcsm.EnterEditMode(ItemTypes.Portals);
                hcsm.MultiBoard.SelectedBoard.Mouse.SetHeldInfo(info);
                hcsm.MultiBoard.Focus();
            }
        }
    }
}
