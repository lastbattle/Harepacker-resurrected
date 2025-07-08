using Footholds;
using MapleLib;
using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaCreator.GUI
{
    public partial class UnpackWzToImg : Form
    {
        /// <summary>
        /// Constructor for the UnpackWzToImg form
        /// </summary>
        public UnpackWzToImg()
        {
            InitializeComponent();
        }

        #region Initialization
        /// <summary>
        /// On init
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Initialization_Load(object sender, EventArgs e)
        {
            versionBox.SelectedIndex = ApplicationSettings.MapleVersionIndex;

            // Set the path text box to the MapleStory data base path from settings
            if (!Directory.Exists(ApplicationSettings.MapleStoryDataBasePath))
            {
                ApplicationSettings.MapleStoryDataBasePath = string.Empty; // reset if the path does not exist
            }
            textBox_path.Text = ApplicationSettings.MapleStoryDataBasePath;

            // Populate the MapleStory localisation box
            var values = Enum.GetValues(typeof(MapleLib.ClientLib.MapleStoryLocalisation))
                    .Cast<MapleLib.ClientLib.MapleStoryLocalisation>()
                    .Select(v => new
                    {
                        Text = v.ToString().Replace("MapleStory", "MapleStory "),
                        Value = (int)v
                    })
                    .ToList();
            // set ComboBox properties
            comboBox_localisation.DataSource = values;
            comboBox_localisation.DisplayMember = "Text";
            comboBox_localisation.ValueMember = "Value";

            var savedLocaliation = values.Where(x => x.Value == ApplicationSettings.MapleStoryClientLocalisation).FirstOrDefault(); // get the saved location from settings
            comboBox_localisation.SelectedItem = savedLocaliation ?? values[0]; // KMS if null
        }

        private void UpdateUI_CurrentLoadingWzFile(string fileName, bool isWzFile)
        {
            textBox_status.Text = string.Format("Extracting {0}{1}...", fileName, isWzFile ? ".wz" : "");
            Application.DoEvents();
        }
        #endregion

        #region Events
        /// <summary>
        /// On select path button click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_pathSelect_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                // Optional: Set dialog properties
                dialog.Description = "Select export folder";
                dialog.ShowNewFolderButton = true; // Allow creating new folders
                dialog.RootFolder = Environment.SpecialFolder.ProgramFiles; // Set root folder

                // Show the dialog and check if the user clicked OK
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedPath = dialog.SelectedPath;
                    textBox_path.Text = selectedPath;

                    // Save to settings
                    ApplicationSettings.MapleStoryDataBasePath = selectedPath;
                }
            }
        }

        /// <summary>
        /// On path text changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBox_path_TextChanged(object sender, EventArgs e)
        {
            if (textBox_path.Text == string.Empty)
            {
                button_unpack.Enabled = false;
                return;
            }
            button_unpack.Enabled = Directory.Exists(textBox_path.Text);
        }

        private bool _bIsInitialising = false;
        /// <summary>
        /// On unpack click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_unpack_Click(object sender, EventArgs e)
        {
            if (_bIsInitialising)
            {
                return;
            }
            _bIsInitialising = true;

            try
            {
                ApplicationSettings.MapleVersionIndex = versionBox.SelectedIndex;
                ApplicationSettings.MapleStoryClientLocalisation = (int)comboBox_localisation.SelectedValue;

                WzMapleVersion mapleVer = (WzMapleVersion)ApplicationSettings.MapleVersionIndex;

                // Wz export folder
                string wzExportFolder = textBox_path.Text;
                if (wzExportFolder == string.Empty || !Directory.Exists(wzExportFolder))
                {
                    return;
                }

                // Get the Base.wz file to determine the file lists to extract
                using (OpenFileDialog baseWzSelect = new()
                {
                    Filter = "MapleStory|Base.wz|All files (*.*)|*.*",
                    Title = "Select Base.wz file",
                    CheckFileExists = true,
                    CheckPathExists = true
                })
                {
                    if (baseWzSelect.ShowDialog() != DialogResult.OK)
                        return;

                    string wzfullPath = Path.GetFullPath(baseWzSelect.FileName);
                    string baseWzfileName = Path.GetFileName(wzfullPath);
                    if (baseWzfileName != "Base.wz")
                        return;

                    string exportWzFolder = Path.Combine(wzExportFolder, "Base");

                    using (WzFile wzFile = new WzFile(wzfullPath, mapleVer))
                    {
                        WzFileParseStatus openStatus = wzFile.ParseWzFile();

                        List<WzDirectory> dirs = new();
                        List<WzImage> imgs = new();
                        foreach (WzDirectory dir in wzFile.WzDirectory.WzDirectories)
                        {
                            //Debug.WriteLine("Directory: " + dir.FullPath);

                            // If its a directory, infer it as the Wz file to extract.
                            ExtractWzToImage(dir.Name, Path.GetDirectoryName(wzfullPath), mapleVer, wzExportFolder);
                        }
                        foreach (WzImage img in wzFile.WzDirectory.WzImages)
                        {
                            imgs.Add(img);
                        }

                        WzImgSerializer serializer = new WzImgSerializer();
                        WzFileExporter.RunWzImgDirsExtraction(dirs, imgs, exportWzFolder, serializer, (currentIndex) =>
                        {
                            UpdateUI_CurrentLoadingWzFile(baseWzfileName, true);
                            textBox_status.Text = string.Format("Extracting {0} ({1}/{2})", baseWzfileName, currentIndex + 1, dirs.Count + imgs.Count);
                        });
                    }
                }


                /*if (InitializeWzFiles(wzPath, fileVersion))
                {
                    Hide();
                    Application.DoEvents();
                    editor = new HaEditor();
                    editor.ShowDialog();

                    Application.Exit();
                }*/
            }
            finally
            {
                _bIsInitialising = false;
            }
        }
        #endregion

        #region Extract methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wzName"></param>
        /// <param name="basePath"></param>
        /// <param name="mapleVer"></param>
        /// <param name="wzExportFolder"></param>
        private void ExtractWzToImage(string wzName, string basePath, WzMapleVersion mapleVer,
            string wzExportFolderBase)
        {
            if (wzName == "Base")
                return;

            string exportWzFolder = Path.Combine(wzExportFolderBase, wzName);

            string wzfullPath = Path.Combine(basePath, wzName + ".wz");
            // Validate path exists
            if (!File.Exists(wzfullPath))
            {
                MessageBox.Show($"Could not find '{wzName}.wz' file at '{wzfullPath}'.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (WzFile wzFile = new WzFile(wzfullPath, mapleVer))
            {
                WzFileParseStatus openStatus = wzFile.ParseWzFile();

                List<WzDirectory> dirs = new();
                List<WzImage> imgs = new();
                foreach (WzDirectory dir in wzFile.WzDirectory.WzDirectories)
                {
                    dirs.Add(dir);
                }
                foreach (WzImage img in wzFile.WzDirectory.WzImages)
                {
                    imgs.Add(img);
                }

                WzImgSerializer serializer = new WzImgSerializer();
                WzFileExporter.RunWzImgDirsExtraction(dirs, imgs, exportWzFolder, serializer, (currentIndex) =>
                {
                    UpdateUI_CurrentLoadingWzFile(wzName, true);
                    textBox_status.Text = string.Format("Extracting {0} ({1}/{2})", wzName, currentIndex + 1, dirs.Count + imgs.Count);
                });
            }
        }

        #endregion
    }
}
