using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib;
using SharpDX.Direct3D9;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HaCreator.MapEditor.Instance.Shapes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Threading;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;

namespace HaCreator.GUI.InstanceEditor
{
    public partial class LoadMobSelector : Form
    {
        private bool _isLoading = false;
        private bool _bItemsLoaded = false;

        // dictionary
        private readonly List<string> itemNames = new(); // cache

        private bool _bNotUserClosing = false;
        private int _selectedMonsterId = 0;
        /// <summary>
        /// The selected itemId in the listbox
        /// </summary>
        public int SelectedMonsterId
        {
            get { return _selectedMonsterId; }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public LoadMobSelector()
        {
            InitializeComponent();

            LoadSearchHelper searchBox = new LoadSearchHelper(listBox_npcList, itemNames);
            this.searchBox.TextChanged += searchBox.TextChanged;

            this.FormClosing += LoadQuestSelector_FormClosing;

            // load items
            load();
        }

        #region Window

        /// <summary>
        /// Loads the item on start of the window
        /// </summary>
        private void load()
        {
            _isLoading = true;
            try
            {
                // Maps
                foreach (KeyValuePair<string, string> mob in Program.InfoManager.MobNameCache) // mobId, mob name
                {
                    string npcId = mob.Key;
                    string npcName = mob.Value;
                    
                    string combinedId_ItemName = string.Format("[{0}] - {1}", npcId, npcName);
                    itemNames.Add(combinedId_ItemName);
                }
                itemNames.Sort();

                object[] itemObjs = itemNames.Cast<object>().ToArray();
                listBox_npcList.Items.AddRange(itemObjs);
            }
            finally
            {
                _isLoading = false;
                _bItemsLoaded = true;
            }
        }

        private void Load_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                _selectedMonsterId = 0; // set none
                Close(); // close window
            }
            else if (e.KeyCode == Keys.Enter)
            {
                //loadButton_Click(null, null);
            }
        }

        /// <summary>
        /// The form is being closed by the user (e.g., clicking the X button)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoadQuestSelector_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !_bNotUserClosing)
            {
                _selectedMonsterId = 0; // set none
            }
        }
        #endregion

        /// <summary>
        /// On list box selection changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listBox_itemList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isLoading || listBox_npcList.SelectedItem == null)
                return;

            string selectedItem = listBox_npcList.SelectedItem as string;

            const string pattern = @"\[(\d+)\]"; //  "[123] - SampleItem"
            Match match = Regex.Match(selectedItem, pattern);

            if (match.Success)
            {
                string mobIdStr = match.Groups[1].Value;
                int mobId = int.Parse(mobIdStr);

                string mobName = "NO NAME";
                if (Program.InfoManager.MobNameCache.ContainsKey(mobIdStr))
                {
                    mobName = Program.InfoManager.MobNameCache[mobIdStr]; 
                }

                // mob image
                if (Program.InfoManager.MobNameCache.ContainsKey(mobIdStr) && Program.InfoManager.MobIconCache.ContainsKey(mobId))
                {
                    WzImageProperty standCanvas = Program.InfoManager.MobIconCache[mobId];
                    if (standCanvas != null)
                        pictureBox_IconPreview.Image = ((WzCanvasProperty) standCanvas).GetLinkedWzCanvasBitmap();
                    else
                        pictureBox_IconPreview.Image = null;
                }
                else
                    pictureBox_IconPreview.Image = null;

                // label desc
                label_itemDesc.Text = mobName;

                // set selected itemid
                this._selectedMonsterId = mobId;
                this.button_select.Enabled = true;
                return;
            }
            this._selectedMonsterId = 0;
            button_select.Enabled = false;
        }

        private void listBox_itemList_measureItem(object sender, MeasureItemEventArgs e)
        {
            //e.ItemHeight = (int)e.Graphics.MeasureString(listBox_itemList.Items[e.Index].ToString(), listBox_itemList.Font, listBox_itemList.Width).Height;
        }

        private void listBox_itemList_drawItem(object sender, DrawItemEventArgs e)
        {
            //e.DrawBackground();
            //e.DrawFocusRectangle();

            //e.Graphics.DrawString(listBox_itemList.Items[e.Index].ToString(), e.Font, new SolidBrush(e.ForeColor), e.Bounds);
        }

        /// <summary>
        /// On select button click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_select_Click(object sender, EventArgs e)
        {
            _bNotUserClosing = true;
            Close();
        }
    }
}
