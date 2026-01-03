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
    public partial class LoadNpcSelector : Form
    {
        private bool _isLoading = false;
        private bool _bItemsLoaded = false;

        // dictionary
        private readonly List<string> itemNames = new List<string>(); // cache

        private bool _bNotUserClosing = false;
        private string _selectedNpcId = string.Empty;
        /// <summary>
        /// The selected itemId in the listbox
        /// </summary>
        public string SelectedNpcId
        {
            get { return _selectedNpcId; }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public LoadNpcSelector()
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
                foreach (KeyValuePair<string, Tuple<string, string>> item in Program.InfoManager.NpcNameCache) // itemid, <item category, item name, item desc>
                {
                    string npcId = item.Key;
                    string npcName = item.Value.Item1;
                    string npcDesc = item.Value.Item2;

                    string combinedId_ItemName = string.Format("[{0}] - {1} {2}", 
                        npcId, 
                        npcName, 
                        npcDesc == string.Empty ? string.Empty : string.Format("({0})", npcDesc));
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
                _selectedNpcId = string.Empty; // set none
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
                _selectedNpcId = string.Empty; // set none
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
                string npcId = match.Groups[1].Value;

                string npcName = "NO NAME";
                string npcFuncs = string.Empty;
                if (Program.InfoManager.NpcNameCache.ContainsKey(npcId))
                {
                    Tuple<string, string> val = Program.InfoManager.NpcNameCache[npcId];

                    npcName = val.Item1; // itemid, <item category, item name, item desc>
                    npcFuncs = val.Item2;
                }

                // npc image - load on demand to reduce memory usage
                pictureBox_IconPreview.Image = null;
                if (Program.InfoManager.NpcNameCache.ContainsKey(npcId))
                {
                    // Try to get from cache first, or load on demand
                    WzImage npcImage = null;
                    if (Program.InfoManager.NpcPropertyCache.TryGetValue(npcId, out npcImage) && npcImage != null)
                    {
                        // Use cached image
                    }
                    else if (Program.DataSource != null)
                    {
                        // Load on demand from data source
                        npcImage = Program.DataSource.GetImage("Npc", $"{npcId}.img");
                        if (npcImage != null)
                        {
                            npcImage.ParseImage();
                            // Cache for future use
                            lock (Program.InfoManager.NpcPropertyCache)
                            {
                                if (!Program.InfoManager.NpcPropertyCache.ContainsKey(npcId))
                                    Program.InfoManager.NpcPropertyCache[npcId] = npcImage;
                            }
                        }
                    }

                    if (npcImage != null)
                    {
                        WzCanvasProperty standCanvas = npcImage["stand"]?["0"] as WzCanvasProperty;
                        if (standCanvas != null)
                            pictureBox_IconPreview.Image = standCanvas.GetLinkedWzCanvasBitmap();
                    }
                }

                // label desc
                label_npcName.Text = npcName;
                label_npcDesc.Text = npcFuncs;

                // set selected itemid
                this._selectedNpcId = npcId;
                this.button_select.Enabled = true;
                return;
            }
            this._selectedNpcId = string.Empty;
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
