using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.Wz;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace HaCreator.GUI.EditorPanels
{
    public partial class LifePanel : UserControl
    {
        private readonly List<string> reactors = new();
        private readonly List<string> npcs = new();
        private readonly List<string> mobs = new();

        private HaCreatorStateManager hcsm;
        private HotSwapRefreshService _hotSwapService;

        public LifePanel()
        {
            InitializeComponent();
        }

        public void Initialize(HaCreatorStateManager hcsm)
        {
            this.hcsm = hcsm;
            hcsm.SetLifePanel(this);

            foreach (KeyValuePair<string, ReactorInfo> entry in Program.InfoManager.Reactors)
            {
                string reactorId = entry.Value.ID;
                string reactorName = entry.Value.Name;

                string combinedName = string.Format("{0} {1}", 
                    reactorId,
                    reactorName == string.Empty ? string.Empty : string.Format("({0})", reactorName));

                reactors.Add(combinedName);
            }
            foreach (KeyValuePair<string, Tuple<string, string>> entry in Program.InfoManager.NpcNameCache)
            {
                string npcName = entry.Value.Item1;
                string npcDesc = entry.Value.Item2;

                string combinedName = string.Format("{0} - {1} {2}", 
                    entry.Key, 
                    npcName,
                    npcDesc == string.Empty ? string.Empty : string.Format("({0})", npcDesc));

                npcs.Add(combinedName);
            }
            foreach (KeyValuePair<string, string> entry in Program.InfoManager.MobNameCache)
            {
                mobs.Add(entry.Key + " - " + entry.Value);
            }

            ReloadLifeList();
        }

        private void lifeModeChanged(object sender, EventArgs e)
        {
            ReloadLifeList();
        }

        public static bool ContainsIgnoreCase(string haystack, string needle)
        {
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) != -1;
        }

        private void ReloadLifeList()
        {
            string searchText = lifeSearchBox.Text;
            bool getAll = searchText == "";
            lifeListBox.Items.Clear();
            List<string> items = [];
            if (reactorRButton.Checked)
            {
                items.AddRange(getAll ? reactors : reactors.Where(x => ContainsIgnoreCase(x, searchText)));
            }
            else if (npcRButton.Checked)
            {
                items.AddRange(getAll ? npcs : npcs.Where(x => ContainsIgnoreCase(x, searchText)));
            }
            else if (mobRButton.Checked)
            {
                items.AddRange(getAll ? mobs : mobs.Where(x => ContainsIgnoreCase(x, searchText)));
            }
            items.Sort();
            lifeListBox.Items.AddRange(items.Cast<object>().ToArray());
        }

        private void lifeListBox_SelectedValueChanged(object sender, EventArgs e)
        {
            lock (hcsm.MultiBoard)
            {
                lifePictureBox.Image = new Bitmap(1, 1);
                if (lifeListBox.SelectedItem == null) 
                    return;

                if (reactorRButton.Checked) // is reactor
                {
                    string reactorIdName = (string)lifeListBox.SelectedItem;

                    const string regexPattern = @"^\d+"; // "1002009 (메이플아일랜드 범용리엑터)"
                    string number = Regex.Match(reactorIdName, regexPattern).Value;

                    ReactorInfo info = Program.InfoManager.Reactors[number];
                    lifePictureBox.Image = new Bitmap(info.Image);
                    hcsm.EnterEditMode(ItemTypes.Reactors);
                    hcsm.MultiBoard.SelectedBoard.Mouse.SetHeldInfo(info);
                }
                else if (npcRButton.Checked) // npc
                {
                    string id = ((string)lifeListBox.SelectedItem).Substring(0, ((string)lifeListBox.SelectedItem).IndexOf(" - "));
                    NpcInfo info = NpcInfo.Get(id);
                    if (info == null)
                    {
                        lifePictureBox.Image = null;
                        return;
                    }
                    if(info.Height==1 && info.Width == 1)
                    {
                        info.Image = global::HaCreator.Properties.Resources.placeholder;
                    }
                    lifePictureBox.Image = new Bitmap(info.Image);
                    hcsm.EnterEditMode(ItemTypes.NPCs);
                    hcsm.MultiBoard.SelectedBoard.Mouse.SetHeldInfo(info);
                }
                else if (mobRButton.Checked) // mobs
                {
                    string id = ((string)lifeListBox.SelectedItem).Substring(0, ((string)lifeListBox.SelectedItem).IndexOf(" - "));
                    MobInfo info = MobInfo.Get(id);
                    if (info == null)
                    {
                        lifePictureBox.Image = null;
                        return;
                    }
                    lifePictureBox.Image = new Bitmap(info.Image);
                    hcsm.EnterEditMode(ItemTypes.Mobs);
                    hcsm.MultiBoard.SelectedBoard.Mouse.SetHeldInfo(info);
                }
            }
        }

        #region Hot Swap
        /// <summary>
        /// Subscribes to hot swap events from the HotSwapRefreshService
        /// </summary>
        /// <param name="refreshService">The hot swap service to subscribe to</param>
        public void SubscribeToHotSwap(HotSwapRefreshService refreshService)
        {
            if (_hotSwapService != null)
            {
                _hotSwapService.LifeDataChanged -= OnLifeDataChanged;
            }

            _hotSwapService = refreshService;

            if (_hotSwapService != null)
            {
                _hotSwapService.LifeDataChanged += OnLifeDataChanged;
            }
        }

        /// <summary>
        /// Handles life data change events
        /// </summary>
        private void OnLifeDataChanged(object sender, LifeDataChangedEventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => HandleLifeDataChange(e)));
                return;
            }
            HandleLifeDataChange(e);
        }

        /// <summary>
        /// Handles the life data change on the UI thread
        /// </summary>
        private void HandleLifeDataChange(LifeDataChangedEventArgs e)
        {
            switch (e.LifeType)
            {
                case Wz.LifeType.Mob:
                    RefreshMobList();
                    break;
                case Wz.LifeType.Npc:
                    RefreshNpcList();
                    break;
                case Wz.LifeType.Reactor:
                    RefreshReactorList();
                    break;
            }
        }

        /// <summary>
        /// Refreshes the mob list from InfoManager
        /// </summary>
        public void RefreshMobList()
        {
            mobs.Clear();
            // Create snapshot to avoid collection modified exception during enumeration
            var mobSnapshot = Program.InfoManager.MobNameCache.ToList();
            foreach (KeyValuePair<string, string> entry in mobSnapshot)
            {
                mobs.Add(entry.Key + " - " + entry.Value);
            }

            // Refresh display if mobs are currently shown
            if (mobRButton.Checked)
            {
                ReloadLifeList();
            }
        }

        /// <summary>
        /// Refreshes the NPC list from InfoManager
        /// </summary>
        public void RefreshNpcList()
        {
            npcs.Clear();
            // Create snapshot to avoid collection modified exception during enumeration
            var npcSnapshot = Program.InfoManager.NpcNameCache.ToList();
            foreach (KeyValuePair<string, Tuple<string, string>> entry in npcSnapshot)
            {
                string npcName = entry.Value.Item1;
                string npcDesc = entry.Value.Item2;

                string combinedName = string.Format("{0} - {1} {2}",
                    entry.Key,
                    npcName,
                    npcDesc == string.Empty ? string.Empty : string.Format("({0})", npcDesc));

                npcs.Add(combinedName);
            }

            // Refresh display if NPCs are currently shown
            if (npcRButton.Checked)
            {
                ReloadLifeList();
            }
        }

        /// <summary>
        /// Refreshes the reactor list from InfoManager
        /// </summary>
        public void RefreshReactorList()
        {
            reactors.Clear();
            // Create snapshot to avoid collection modified exception during enumeration
            var reactorSnapshot = Program.InfoManager.Reactors.ToList();
            foreach (KeyValuePair<string, ReactorInfo> entry in reactorSnapshot)
            {
                string reactorId = entry.Value.ID;
                string reactorName = entry.Value.Name;

                string combinedName = string.Format("{0} {1}",
                    reactorId,
                    reactorName == string.Empty ? string.Empty : string.Format("({0})", reactorName));

                reactors.Add(combinedName);
            }

            // Refresh display if reactors are currently shown
            if (reactorRButton.Checked)
            {
                ReloadLifeList();
            }
        }
        #endregion
    }
}
