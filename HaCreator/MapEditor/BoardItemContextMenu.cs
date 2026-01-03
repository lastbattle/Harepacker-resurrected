using HaCreator.Collections;
using HaCreator.GUI;
using HaCreator.GUI.InstanceEditor;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapEditor.UndoRedo;
using HaCreator.Wz;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor
{
    public class BoardItemContextMenu
    {
        private MultiBoard multiboard;
        private Board board;
        private BoardItem target;
        private ContextMenuStrip cms;

        public BoardItemContextMenu(MultiBoard multiboard, Board board, BoardItem target)
        {
            this.multiboard = multiboard;
            this.board = board;
            this.target = target;
            this.cms = null;
        }

        public ContextMenuStrip Menu
        {
            get 
            {
                if (cms == null)
                {
                    generateContextMenuStrip();
                }
                return cms;
            }
        }

        private void generateContextMenuStrip()
        {
            cms = new ContextMenuStrip();
            List<ToolStripMenuItem> generalCategory = [];
            List<ToolStripMenuItem> zCategory = new();
            List<ToolStripMenuItem> platformCategory = new();

            ToolStripMenuItem editInstance = new ToolStripMenuItem("Edit this instance...");
            editInstance.Click += editInstance_Click;
            editInstance.Font = new System.Drawing.Font(editInstance.Font, System.Drawing.FontStyle.Bold);
            generalCategory.Add(editInstance);

            // Portal
            if (target is PortalInstance && ((PortalInstance)target).tm != MapConstants.MaxMap)
            {
                ToolStripMenuItem loadTargetMap = new ToolStripMenuItem("Load target map in a new tab");
                loadTargetMap.Click += LoadPortalTargetMap_Click;
                generalCategory.Add(loadTargetMap);
            }

            /*ToolStripMenuItem baseInfo = new ToolStripMenuItem("Edit base info...");
            baseInfo.Click += new EventHandler(baseInfo_Click);
            cms.Items.Add(baseInfo);*/

            // ToolTip
            if (target is ToolTipInstance && ((ToolTipInstance)target).CharacterToolTip == null)
            {
                ToolStripMenuItem addChar = new ToolStripMenuItem("Add Character Tooltip");
                addChar.Click += addChar_Click;
                generalCategory.Add(addChar);
            }

            // Background
            if (target is BackgroundInstance || target is LayeredItem)
            {
                ToolStripMenuItem bringToFront = new ToolStripMenuItem("Bring to Front");
                bringToFront.Click += new EventHandler(bringToFront_Click);
                zCategory.Add(bringToFront);
                ToolStripMenuItem sendToBack = new ToolStripMenuItem("Send to Back");
                sendToBack.Click += new EventHandler(sendToBack_Click);
                zCategory.Add(sendToBack);
            }

            // Foothold
            if (target is FootholdAnchor)
            {
                ToolStripMenuItem selectPlat = new ToolStripMenuItem("Select Connected");
                selectPlat.Click += selectPlat_Click;
                platformCategory.Add(selectPlat);
            }

            // Layer
            if (target is IContainsLayerInfo)
            {
                ToolStripMenuItem moveLayer = new ToolStripMenuItem("Change Layer/Platform...");
                moveLayer.Click += moveLayer_Click;
                platformCategory.Add(moveLayer);
            }

            if (target is IContainsLayerInfo || (target is FootholdAnchor && getZmOfSelectedFoothold() != -1))
            {
                ToolStripMenuItem selectZm = new ToolStripMenuItem("Select Platform");
                selectZm.Click += selectZm_Click;
                platformCategory.Add(selectZm);
                /*ToolStripMenuItem editZm = new ToolStripMenuItem("Edit ZM...");
                editZm.Click += editZm_Click;
                platformCategory.Add(editZm);*/
            }


            bool hasItems = false;
            foreach (List<ToolStripMenuItem> currList in new List<ToolStripMenuItem>[] { generalCategory, zCategory, platformCategory })
            {
                if (currList.Count > 0)
                {
                    if (hasItems)
                    {
                        cms.Items.Add(new ToolStripSeparator());
                    }
                    cms.Items.AddRange(currList.ToArray());
                    hasItems = true;
                }
            }
        }

        /// <summary>
        /// Load portal target map
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void LoadPortalTargetMap_Click(object sender, EventArgs e)
        {
            PortalInstance portal = (PortalInstance)target;

            if (portal.tm != MapConstants.MaxMap)
            {
                multiboard.HaCreatorStateManager.LoadMap(portal.tm);
            }
        }

        /// <summary>
        /// Add character tooltip
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void addChar_Click(object sender, EventArgs e)
        {
            ToolTipInstance tt = (ToolTipInstance)target;
            tt.CreateCharacterTooltip(new XNA.Rectangle(tt.Left - 50, tt.Top - 50, tt.Width + 100, tt.Height + 100));
        }

        void moveLayer_Click(object sender, EventArgs e)
        {
            lock (multiboard)
            {
                for (int i = 0; i < board.SelectedItems.Count; i++)
                {
                    if (board.SelectedItems[i] is FootholdAnchor)
                    {
                        foreach (FootholdAnchor x in new AnchorEnumerator((FootholdAnchor)board.SelectedItems[i]))
                        {
                            x.Selected = true;
                        }
                    }
                }
                new LayerChange(board.SelectedItems, board).ShowDialog();
            }
        }

        private int getZmOfSelectedFoothold()
        {
            FootholdAnchor anchor = (FootholdAnchor)target;
            foreach (FootholdLine line in anchor.connectedLines)
            {
                if (line.Selected)
                {
                    return line.PlatformNumber;
                }
            }
            return -1;
        }

        void selectPlat_Click(object sender, EventArgs e)
        {
            lock (multiboard)
            {
                foreach (FootholdAnchor x in new AnchorEnumerator((FootholdAnchor)target))
                {
                    x.Selected = true;
                }
            }
        }

        void selectZm_Click(object sender, EventArgs e)
        {
            lock (multiboard)
            {
                int zm = target is IContainsLayerInfo ? ((IContainsLayerInfo)target).PlatformNumber : getZmOfSelectedFoothold();
                foreach (BoardItem item in target.Board.BoardItems.Items)
                {
                    if (item is IContainsLayerInfo && ((IContainsLayerInfo)item).PlatformNumber == zm)
                    {
                        ((BoardItem)item).Selected = true;
                    }
                }
                foreach (FootholdLine line in target.Board.BoardItems.FootholdLines)
                {
                    if (line.PlatformNumber == zm)
                    {
                        ((FootholdLine)line).FirstDot.Selected = true;
                        ((FootholdLine)line).SecondDot.Selected = true;
                    }
                }
            }
        }

        void editZm_Click(object sender, EventArgs e)
        {
            lock (multiboard)
            {
                List<IContainsLayerInfo> items = new List<IContainsLayerInfo>();
                foreach (BoardItem item in board.SelectedItems)
                {
                    if (item is IContainsLayerInfo)
                    {
                        items.Add((IContainsLayerInfo)item);
                    }
                    else if (item is FootholdAnchor)
                    {
                        foreach (FootholdLine line in ((FootholdAnchor)item).connectedLines)
                        {
                            if (line.Selected)
                            {
                                items.Add(line);
                            }
                        }
                    }
                }
                new MassZmEditor(items.ToArray(), board, ((IContainsLayerInfo)target).PlatformNumber).ShowDialog();
            }
        }

        void sendToBack_Click(object sender, EventArgs e)
        {
            multiboard.SendToBackClicked(target);
        }

        void bringToFront_Click(object sender, EventArgs e)
        {
            multiboard.BringToFrontClicked(target);
        }

        private void baseInfo_Click(object sender, EventArgs e)
        {
            multiboard.EditBaseClicked(target);
        }

        private void editInstance_Click(object sender, EventArgs e)
        {
            multiboard.EditInstanceClicked(target);
        }
    }
}
