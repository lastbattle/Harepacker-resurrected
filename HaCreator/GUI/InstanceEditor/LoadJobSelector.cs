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
using MapleLib.WzLib.WzStructure.Data.CharacterStructure;

namespace HaCreator.GUI.InstanceEditor
{
    public partial class LoadJobSelector : Form
    {
        private bool _isLoading = false;
        private bool _bJobsLoaded = false;

        // dictionary
        private readonly List<string> jobNames = new List<string>(); // cache

        private bool _bNotUserClosing = false;
        private CharacterJob _selectedJob = CharacterJob.None;

        /// <summary>
        /// The selected jobId in the listbox
        /// </summary>
        public CharacterJob SelectedJob
        {
            get { return _selectedJob; }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public LoadJobSelector()
        {
            InitializeComponent();

            LoadSearchHelper searchBox = new LoadSearchHelper(listBox_npcList, jobNames);
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
                // Jobs
                var jobsList = new List<(int jobId, string displayName)>();
                foreach (CharacterJob job in Enum.GetValues(typeof(CharacterJob)))
                {
                    int jobId = (int)job;
                    string jobName = job.GetFormattedJobName(false);

                    string combinedId_JobName = string.Format("[{0}] - {1}", jobId, jobName);

                    jobsList.Add((jobId, combinedId_JobName));
                }
                jobsList.Sort((a, b) => a.jobId.CompareTo(b.jobId));

                // Extract the display names in sorted order
                jobNames.AddRange(jobsList.Select(x => x.displayName));

                listBox_npcList.Items.AddRange(jobNames.ToArray());
            }
            finally
            {
                _isLoading = false;
                _bJobsLoaded = true;
            }
        }

        private void Load_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                _selectedJob = CharacterJob.None; // set none
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
                _selectedJob = CharacterJob.None; // set none
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
                string jobId = match.Groups[1].Value;
                CharacterJob job = (CharacterJob)int.Parse(jobId);
                string jobName = job.GetFormattedJobName();

                // Job image (if available)
                // You might need to implement a method to get the job image
                // pictureBox_IconPreview.Image = GetJobImage(job);

                // label desc
                label_itemDesc.Text = jobName;

                // set selected jobId
                _selectedJob = job;
                this.button_select.Enabled = true;
                return;
            }
            _selectedJob = CharacterJob.None;
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
