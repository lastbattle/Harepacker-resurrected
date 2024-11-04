/*Copyright(c) 2024, LastBattle https://github.com/lastbattle/Harepacker-resurrected

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

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
    public partial class LoadSkillSelector : Form
    {
        private bool _isLoading = false;
        private bool _bSkillsLoaded = false;

        private int filterSkillId = 0; // 0 = no filter, 243 = _itemId / 10000

        // dictionary
        private readonly List<string> skillNames = new List<string>(); // cache


        private int _selectedSkillId = 0;
        private bool _bNotUserClosing = false;

        /// <summary>
        /// The selected itemId in the listbox
        /// </summary>
        public int SelectedSkillId
        {
            get { return _selectedSkillId; }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="filterSkillId"></param>
        public LoadSkillSelector(int filterSkillId)
        {
            InitializeComponent();

            this.FormClosing += LoadQuestSelector_FormClosing;

            this.filterSkillId = filterSkillId;

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
                foreach (KeyValuePair<string, Tuple<string, string>> item in Program.InfoManager.SkillNameCache) // skillId, <name, desc>
                {
                    string skillId = item.Key;
                    string skillName = item.Value.Item1;
                    string skillDesc = item.Value.Item2;

                    int intName = 0;
                    int.TryParse(skillId, out intName);

                    if (filterSkillId != 0 && (intName / 10000 != filterSkillId)) // filters for item category
                        continue;

                    string combinedId_ItemName = string.Format("[{0}] - ({1}) {2}", skillId, skillDesc, skillName);

                    // add it even if its there's no icon.
                    skillNames.Add(combinedId_ItemName);
                }
                skillNames.Sort();

                object[] itemObjs = skillNames.Cast<object>().ToArray();
                listBox_itemList.Items.AddRange(itemObjs);
            }
            finally
            {
                _isLoading = false;
                _bSkillsLoaded = true;
            }
        }

        private void Load_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                _selectedSkillId = 0;
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
                _selectedSkillId = 0;
            }
        }
        #endregion

        #region Search box
        private string _previousSeachText = string.Empty;
        private CancellationTokenSource _existingSearchTaskToken = null;

        /// <summary>
        /// Searchbox text changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void searchBox_TextChanged(object sender, EventArgs e)
        {
            TextBox searchBox = (TextBox)sender;
            string searchText = searchBox.Text.ToLower();

            if (_previousSeachText == searchText)
                return;
            _previousSeachText = searchText; // set

            // start searching
            searchItemInternal(searchText);
        }

        /// <summary>
        /// Search and filters map according to the user's query
        /// </summary>
        /// <param name="searchText"></param>
        public void searchItemInternal(string searchText)
        {
            if (!_bSkillsLoaded)
                return;

            // Cancel existing task if any
            if (_existingSearchTaskToken != null && !_existingSearchTaskToken.IsCancellationRequested)
            {
                _existingSearchTaskToken.Cancel();
            }

            // Clear 
            listBox_itemList.Items.Clear();
            if (searchText == string.Empty)
            {
                var filteredItems = skillNames.Where(kvp =>
                {
                    return true;
                }).Select(kvp => kvp) // or kvp.Value or any transformation you need
                  .Cast<object>()
                  .ToArray();

                listBox_itemList.Items.AddRange(filteredItems);

                listBox_itemList_SelectedIndexChanged(null, null);
            }
            else
            {

                Dispatcher currentDispatcher = Dispatcher.CurrentDispatcher;

                // new task
                _existingSearchTaskToken = new CancellationTokenSource();
                var cancellationToken = _existingSearchTaskToken.Token;

                Task t = Task.Run(() =>
                {
                    Thread.Sleep(500); // average key typing speed

                    List<string> itemsFiltered = new List<string>();
                    foreach (string map in skillNames)
                    {
                        if (_existingSearchTaskToken.IsCancellationRequested)
                            return; // stop immediately

                        // Filter by string first
                        if (map.ToLower().Contains(searchText))
                        {
                            itemsFiltered.Add(map);
                        }
                    }

                    currentDispatcher.BeginInvoke(new Action(() =>
                    {
                        foreach (string map in itemsFiltered)
                        {
                            if (_existingSearchTaskToken.IsCancellationRequested)
                                return; // stop immediately

                            listBox_itemList.Items.Add(map);
                        }

                        if (listBox_itemList.Items.Count > 0)
                        {
                            listBox_itemList.SelectedIndex = 0; // set default selection to reduce clicks
                        }
                    }));
                }, cancellationToken);

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
            if (_isLoading || listBox_itemList.SelectedItem == null)
                return;

            string selectedItem = listBox_itemList.SelectedItem as string;

            const string pattern = @"\[(\d+)\]"; //  "[123] - SampleItem"
            Match match = Regex.Match(selectedItem, pattern);

            if (match.Success)
            {
                string skillId = match.Groups[1].Value;

                int intName = 0;
                int.TryParse(skillId, out intName);

                if (intName != 0)
                {
                    Tuple<string, string> skillInfo = Program.InfoManager.SkillNameCache[skillId]; // // itemid, <item category, item name, item desc>

                    if (Program.InfoManager.SkillWzImageCache.ContainsKey(skillId))
                        pictureBox_IconPreview.Image = ((WzCanvasProperty) Program.InfoManager.SkillWzImageCache[skillId]?["icon"])?.GetLinkedWzCanvasBitmap();
                    else
                        pictureBox_IconPreview.Image = null;

                    label_itemDesc.Text = skillInfo.Item2;
                    label_itemName.Text = skillInfo.Item1;

                    // set selected itemid
                    this._selectedSkillId = intName;
                    this.button_select.Enabled = true;
                    return;
                }
            }
            this._selectedSkillId = 0;
            button_select.Enabled = false;

            label_itemDesc.Text = string.Empty;
            label_itemName.Text = string.Empty;
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
