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
    public partial class LoadQuestSelector : Form
    {
        private bool _isLoading = false;
        private bool _bItemsLoaded = false;

        // dictionary
        private readonly List<string> questNames = new List<string>(); // cache


        private bool _bNotUserClosing = false;
        private string _selectedQuestId = string.Empty;
        /// <summary>
        /// The selected itemId in the listbox
        /// </summary>
        public string SelectedQuestId
        {
            get { return _selectedQuestId; }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public LoadQuestSelector()
        {
            InitializeComponent();

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
                foreach (KeyValuePair<string, WzSubProperty> kvp in Program.InfoManager.QuestInfos)
                {
                    string key = kvp.Key;
                    WzSubProperty questProp = kvp.Value;

                    // Quest name
                    string questName = (questProp["name"] as WzStringProperty)?.Value ?? "NO NAME";

                    string combinedId_ItemName = string.Format("[{0}] - {1}", key, questName);
                    questNames.Add(combinedId_ItemName);
                }
                questNames.Sort();

                object[] itemObjs = questNames.Cast<object>().ToArray();
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
                _selectedQuestId = string.Empty;
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
                _selectedQuestId = string.Empty;
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
            if (!_bItemsLoaded)
                return;

            // Cancel existing task if any
            if (_existingSearchTaskToken != null && !_existingSearchTaskToken.IsCancellationRequested)
            {
                _existingSearchTaskToken.Cancel();
            }

            // Clear 
            listBox_npcList.Items.Clear();
            if (searchText == string.Empty)
            {
                var filteredItems = questNames.Where(kvp =>
                {
                    return true;
                }).Select(kvp => kvp) // or kvp.Value or any transformation you need
                  .Cast<object>()
                  .ToArray();

                listBox_npcList.Items.AddRange(filteredItems);

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
                    foreach (string map in questNames)
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

                            listBox_npcList.Items.Add(map);
                        }

                        if (listBox_npcList.Items.Count > 0)
                        {
                            listBox_npcList.SelectedIndex = 0; // set default selection to reduce clicks
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
            if (_isLoading || listBox_npcList.SelectedItem == null)
                return;

            string selectedItem = listBox_npcList.SelectedItem as string;

            const string pattern = @"\[(\d+)\]"; //  "[123] - SampleItem"
            Match match = Regex.Match(selectedItem, pattern);

            if (match.Success)
            {
                string questId = match.Groups[1].Value;

                string questName = "NO NAME";
                int index = selectedItem.IndexOf("] - ") + 3;
                if (index > 0)
                {
                    questName = selectedItem.Substring(index);
                }

                if (Program.InfoManager.QuestInfos.ContainsKey(questId)) {
                    WzSubProperty questProp = Program.InfoManager.QuestInfos[questId];

                    string summary = (questProp["summary"] as WzStringProperty)?.Value;

                    label_questSummary.Text = summary;
                } else
                {
                    label_questSummary.Text = string.Empty;
                }

                // label desc
                label_questName.Text = questName;

                // set selected itemid
                this._selectedQuestId = questId;
                this.button_select.Enabled = true;
                return;
            }
            label_questName.Text = string.Empty;
            this._selectedQuestId = string.Empty;
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
