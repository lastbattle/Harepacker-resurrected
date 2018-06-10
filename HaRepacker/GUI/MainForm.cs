/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Text.RegularExpressions;
using Footholds;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using HaRepackerLib;
using MapleLib.WzLib.Serialization;
using System.Threading;
using HaRepacker.GUI.Interaction;
using MapleLib.WzLib.Util;
using System.Runtime.InteropServices;
using MapleLib.WzLib.WzStructure;
using System.Net;
using System.Text;
using System.Diagnostics;
using HaRepackerLib.Controls;
using System.IO.Pipes;
using HaRepacker.WindowsAPIImports;
using HaRepackerLib.Controls.HaRepackerMainPanels;
using System.Linq;

namespace HaRepacker.GUI
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// Adds the WZ encryption types to ToolstripComboBox.
        /// Shared code between WzMapleVersionInputBox.cs
        /// </summary>
        /// <param name="encryptionBox"></param>
        public static void AddWzEncryptionTypesToComboBox(object encryptionBox)
        {
            string[] resources = {
                HaRepacker.Properties.Resources.EncTypeGMS,
                HaRepacker.Properties.Resources.EncTypeMSEA,
                HaRepacker.Properties.Resources.EncTypeNone
            };
            if (encryptionBox is ToolStripComboBox)
            {
                foreach (string res in resources)
                {
                    ((ToolStripComboBox)encryptionBox).Items.Add(res);
                }
            }
            else
            {
                foreach (string res in resources)
                {
                    ((ComboBox)encryptionBox).Items.Add(res);
                }
            }
        }

        private HaRepackerMainPanel MainPanel = null;

        public MainForm(string wzToLoad, bool usingPipes, bool firstrun)
        {
            InitializeComponent();

            // Set default selected main panel
            UpdateSelectedMainPanelTab();

            // encryptions
            AddWzEncryptionTypesToComboBox(encryptionBox);
#if DEBUG
            debugToolStripMenuItem.Visible = true;
#endif
            WindowState = ApplicationSettings.Maximized ? FormWindowState.Maximized : FormWindowState.Normal;
            Size = ApplicationSettings.WindowSize;

            if (usingPipes)
            {
                try
                {
                    Program.pipe = new NamedPipeServerStream(Program.pipeName, PipeDirection.In);
                    Program.pipeThread = new Thread(new ThreadStart(PipeServer));
                    Program.pipeThread.IsBackground = true;
                    Program.pipeThread.Start();
                }
                catch (IOException)
                {
                    if (wzToLoad != null)
                    {
                        try
                        {
                            NamedPipeClientStream clientPipe = new NamedPipeClientStream(".", Program.pipeName, PipeDirection.Out);
                            clientPipe.Connect(0);
                            StreamWriter sw = new StreamWriter(clientPipe);
                            sw.WriteLine(wzToLoad);
                            clientPipe.WaitForPipeDrain();
                            sw.Close();
                            Environment.Exit(0);
                        }
                        catch (TimeoutException)
                        {
                        }
                    }
                }
            }
            if (wzToLoad != null && File.Exists(wzToLoad))
            {
                short version;
                WzMapleVersion encVersion = WzTool.DetectMapleVersion(wzToLoad, out version);
                encryptionBox.SelectedIndex = (int)encVersion;
                LoadWzFileThreadSafe(wzToLoad, MainPanel, false);
            }
            WzNode.ContextMenuBuilder = new WzNode.ContextMenuBuilderDelegate(new ContextMenuManager(MainPanel, MainPanel.UndoRedoMan).CreateMenu);
        }

        public void Interop_AddLoadedWzFileToManager(WzFile f)
        {
            Program.WzMan.InsertWzFileUnsafe(f, MainPanel);
        }

        private delegate void LoadWzFileDelegate(string path, HaRepackerMainPanel panel, bool detectMapleVersion);
        private void LoadWzFileCallback(string path, HaRepackerMainPanel panel, bool detectMapleVersion)
        {
            try
            {
                if (detectMapleVersion)
                    Program.WzMan.LoadWzFile(path, panel);
                else
                    Program.WzMan.LoadWzFile(path, (WzMapleVersion)encryptionBox.SelectedIndex, MainPanel);
            }
            catch
            {
                Warning.Error(string.Format(HaRepacker.Properties.Resources.MainCouldntOpenWZ, path));
            }
        }

        private void LoadWzFileThreadSafe(string path, HaRepackerMainPanel panel, bool detectMapleVersion)
        {
            if (panel.InvokeRequired)
                panel.Invoke(new LoadWzFileDelegate(LoadWzFileCallback), path, panel, detectMapleVersion);
            else
                LoadWzFileCallback(path, panel, detectMapleVersion);
        }

        private delegate void SetWindowStateDelegate(FormWindowState state);
        private void SetWindowStateCallback(FormWindowState state)
        {
            WindowState = state;
            user32.SetWindowPos(Handle, user32.HWND_TOPMOST, 0, 0, 0, 0, user32.SWP_NOMOVE | user32.SWP_NOSIZE);
            user32.SetWindowPos(Handle, user32.HWND_NOTOPMOST, 0, 0, 0, 0, user32.SWP_NOMOVE | user32.SWP_NOSIZE);
        }

        private void SetWindowStateThreadSafe(FormWindowState state)
        {
            if (InvokeRequired)
                Invoke(new SetWindowStateDelegate(SetWindowStateCallback), state);
            else
                SetWindowStateCallback(state);
        }

        private string OnPipeRequest(string request)
        {
            if (File.Exists(request)) LoadWzFileThreadSafe(request, MainPanel, true);
            SetWindowStateThreadSafe(FormWindowState.Normal);
            return "OK";
        }

        private void PipeServer()
        {
            try
            {
                while (true)
                {
                    Program.pipe.WaitForConnection();
                    StreamReader sr = new StreamReader(Program.pipe);
                    OnPipeRequest(sr.ReadLine());
                    Program.pipe.Disconnect();
                }
            }
            catch { }
        }


        public static Thread updater = null;

        //a thread used by the updating feature
        private void UpdaterThread()
        {
            Thread.Sleep(60000);
            WebClient client = new WebClient();
            try
            {
                int version = int.Parse(
                    Encoding.ASCII.GetString(
                    client.DownloadData(
                    ApplicationSettings.UpdateServer + "version.txt"
                    )));
                string notice = Encoding.ASCII.GetString(
                    client.DownloadData(
                    ApplicationSettings.UpdateServer + "notice.txt"
                    ));
                string url = Encoding.ASCII.GetString(
                    client.DownloadData(
                    ApplicationSettings.UpdateServer + "url.txt"
                    ));
                if (version <= Constants.Version)
                    return;
                if (MessageBox.Show(string.Format(HaRepacker.Properties.Resources.MainUpdateAvailable, notice.Replace("%URL%", url)), HaRepacker.Properties.Resources.MainUpdateTitle, MessageBoxButtons.YesNo) == DialogResult.Yes)
                    Process.Start(url);
            }
            catch { }
        }

        #region Handlers
        private void MainForm_Load(object sender, EventArgs e)
        {
            encryptionBox.SelectedIndex = (int)ApplicationSettings.MapleVersion;
            if (UserSettings.AutoUpdate && ApplicationSettings.UpdateServer != "")
            {
                updater = new Thread(new ThreadStart(UpdaterThread));
                updater.IsBackground = true;
                updater.Start();
            }
        }

        /// <summary>
        /// Redocks the list of controls on the panel
        /// </summary>
        private void RedockControls()
        {
            /*   int mainControlHeight = this.Size.Height;
               int mainControlWidth = this.Size.Width;

               foreach (TabPage page in tabControl_MainPanels.TabPages)
               {
                   page.Size = new Size(mainControlWidth, mainControlHeight);
               }*/
        }

        private void MainForm_SizeChanged(object sender, EventArgs e)
        {
            if (this.Size.Width * this.Size.Height != 0)
            {
                RedockControls();

                ApplicationSettings.WindowSize = this.Size;
                ApplicationSettings.Maximized = WindowState == FormWindowState.Maximized;
            }
        }

        /// <summary>
        /// When the selected tab in the MainForm change
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tabControl_MainPanels_TabIndexChanged(object sender, EventArgs e)
        {
            UpdateSelectedMainPanelTab();
        }
        private void UpdateSelectedMainPanelTab()
        {
            MainPanel = tabControl_MainPanels.SelectedTab.Controls.OfType<HaRepackerMainPanel>().First();
        }

        /// <summary>
        /// Add a new tab to the TabControl
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_addTab_Click(object sender, EventArgs e)
        {
            if (tabControl_MainPanels.TabCount > 10)
            {
                return;
            }

            TabPage tabPage = new TabPage()
            {
                Padding = new Padding(3, 3, 3, 3),
                Margin = new Padding(3, 3, 3, 3),
                Size = new Size(1488, 880),
                Text = string.Format("Tab {0}", tabControl_MainPanels.TabCount + 1)
            };
            tabPage.Controls.Add(new HaRepackerMainPanel()
            {
                Padding = new Padding(0, 0, 0, 0),
                Margin = new Padding(0, 0, 0, 0),
                Size = new Size(1492, 884),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            });

            tabControl_MainPanels.TabPages.Add(tabPage);
        }

        private void encryptionBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplicationSettings.MapleVersion = (WzMapleVersion)encryptionBox.SelectedIndex;
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog()
            {
                Title = HaRepacker.Properties.Resources.SelectWz,
                Filter = string.Format("{0}|*.wz",
                HaRepacker.Properties.Resources.WzFilter),
                Multiselect = true,
            })
            {

                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                WzMapleVersion MapleVersionEncryptionSelected = (WzMapleVersion)encryptionBox.SelectedIndex;
                foreach (string filePath in dialog.FileNames)
                {
                    if (WzTool.IsDataWzFile(filePath))
                    {
                        WzImage img = Program.WzMan.LoadDataWzHotfixFile(filePath, MapleVersionEncryptionSelected, MainPanel);
                    }
                    else if (WzTool.IsListFile(filePath))
                    {
                        new ListEditor(filePath, MapleVersionEncryptionSelected).Show();
                    }
                    else
                    {
                        WzFile f = Program.WzMan.LoadWzFile(filePath, MapleVersionEncryptionSelected, MainPanel);

                        // Now pre-load the other part of Map.wz
                        if (filePath.ToLower().EndsWith("map.wz"))
                        {
                            string[] otherMapWzFiles = Directory.GetFiles(filePath.Substring(0, filePath.LastIndexOf("\\")), "Map*.wz");
                            foreach (string filePath_Others in otherMapWzFiles)
                            {
                                if (filePath_Others != filePath &&
                                    (filePath_Others.EndsWith("Map001.wz") || filePath_Others.EndsWith("Map2.wz"))) // damn, ugly hack to only whitelist those that Nexon uses. but someone could be saving as say Map_bak.wz in their folder.
                                {
                                    Program.WzMan.LoadWzFile(filePath_Others, MapleVersionEncryptionSelected, MainPanel);
                                }
                            }
                        }
                        else if (filePath.ToLower().EndsWith("mob.wz"))  // Now pre-load the other part of Mob.wz
                        {
                            string[] otherMobWzFiles = Directory.GetFiles(filePath.Substring(0, filePath.LastIndexOf("\\")), "Mob*.wz");
                            foreach (string filePath_Others in otherMobWzFiles)
                            {
                                if (filePath_Others != filePath &&
                                    filePath_Others.EndsWith("Mob2.wz"))
                                {
                                    Program.WzMan.LoadWzFile(filePath_Others, MapleVersionEncryptionSelected, MainPanel);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void unloadAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Warning.Warn(HaRepacker.Properties.Resources.MainUnloadAll))
                Program.WzMan.UnloadAll();
        }

        private void reloadAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Warning.Warn(HaRepacker.Properties.Resources.MainReloadAll))
                Program.WzMan.ReloadAll(MainPanel);
        }

        private void renderMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FHMapper.FHMapper mapper = new FHMapper.FHMapper(MainPanel);
            mapper.ParseSettings();
            if (MainPanel.DataTree.SelectedNode == null)
                return;
            if (MainPanel.DataTree.SelectedNode.Tag is WzImage)
            {
                WzImage img = (WzImage)MainPanel.DataTree.SelectedNode.Tag;
                string mapName = img.Name.Substring(0, img.Name.Length - 4);

                if (!Directory.Exists("Renders\\" + mapName))
                {
                    Directory.CreateDirectory("Renders\\" + mapName);
                }
                mapper.SaveMap(img, double.Parse(zoomTextBox.TextBox.Text));
            }
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FHMapper.FHMapper mapper = new FHMapper.FHMapper(MainPanel);
            mapper.ParseSettings();
            Settings settingsDialog = new Settings();
            settingsDialog.settings = mapper.settings;
            settingsDialog.main = mapper;
            settingsDialog.ShowDialog();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new AboutForm().ShowDialog();
        }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new OptionsForm(MainPanel).ShowDialog();
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new NewForm(MainPanel).ShowDialog();
        }

        /// <summary>
        /// Save tool strip menu button clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            WzNode node;
            if (MainPanel.DataTree.SelectedNode == null)
            {
                if (MainPanel.DataTree.Nodes.Count == 1)
                    node = (WzNode)MainPanel.DataTree.Nodes[0];
                else
                {
                    MessageBox.Show(HaRepacker.Properties.Resources.MainSelectWzFolder, HaRepacker.Properties.Resources.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            else
            {
                if (MainPanel.DataTree.SelectedNode.Tag is WzFile)
                    node = (WzNode)MainPanel.DataTree.SelectedNode;
                else
                    node = ((WzNode)MainPanel.DataTree.SelectedNode).TopLevelNode;
            }

            // Save to file.
            if (node.Tag is WzFile || node.Tag is WzImage)
            {
                new SaveForm(MainPanel, node).ShowDialog();
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            ApplicationSettings.Maximized = WindowState == FormWindowState.Maximized;
            e.Cancel = !Warning.Warn(HaRepacker.Properties.Resources.MainConfirmExit);
        }
        #endregion

        private void removeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RemoveSelectedNodes();
        }

        private void RemoveSelectedNodes()
        {
            if (!Warning.Warn(HaRepacker.Properties.Resources.MainConfirmRemoveNode))
            {
                return;
            }
            MainPanel.PromptRemoveSelectedTreeNodes();
        }

        private void RunWzFilesExtraction(object param)
        {
            ChangeApplicationState(false);

            string[] wzFilesToDump = (string[])((object[])param)[0];
            string baseDir = (string)((object[])param)[1];
            WzMapleVersion version = (WzMapleVersion)((object[])param)[2];
            IWzFileSerializer serializer = (IWzFileSerializer)((object[])param)[3];
            UpdateProgressBar(MainPanel.mainProgressBar, 0, false, true);
            UpdateProgressBar(MainPanel.mainProgressBar, wzFilesToDump.Length, true, true);

            if (!Directory.Exists(baseDir))
            {
                Directory.CreateDirectory(baseDir);
            }

            foreach (string wzpath in wzFilesToDump)
            {
                if (WzTool.IsListFile(wzpath))
                {
                    Warning.Error(string.Format(HaRepacker.Properties.Resources.MainListWzDetected, wzpath));
                    continue;
                }
                WzFile f = new WzFile(wzpath, version);
                f.ParseWzFile();
                serializer.SerializeFile(f, Path.Combine(baseDir, f.Name));
                f.Dispose();
                UpdateProgressBar(MainPanel.mainProgressBar, 1, false, false);
            }
            threadDone = true;
        }

        private void RunWzImgDirsExtraction(object param)
        {
            ChangeApplicationState(false);

            List<WzDirectory> dirsToDump = (List<WzDirectory>)((object[])param)[0];
            List<WzImage> imgsToDump = (List<WzImage>)((object[])param)[1];
            string baseDir = (string)((object[])param)[2];
            IWzImageSerializer serializer = (IWzImageSerializer)((object[])param)[3];

            UpdateProgressBar(MainPanel.mainProgressBar, 0, false, true);
            UpdateProgressBar(MainPanel.mainProgressBar, dirsToDump.Count + imgsToDump.Count, true, true);

            foreach (WzImage img in imgsToDump)
            {
                serializer.SerializeImage(img, Path.Combine(baseDir, img.Name));
                UpdateProgressBar(MainPanel.mainProgressBar, 1, false, false);
            }
            foreach (WzDirectory dir in dirsToDump)
            {
                serializer.SerializeDirectory(dir, Path.Combine(baseDir, dir.Name));
                UpdateProgressBar(MainPanel.mainProgressBar, 1, false, false);
            }
            threadDone = true;
        }

        private void RunWzObjExtraction(object param)
        {
            ChangeApplicationState(false);

            List<WzObject> objsToDump = (List<WzObject>)((object[])param)[0];
            string path = (string)((object[])param)[1];
            ProgressingWzSerializer serializer = (ProgressingWzSerializer)((object[])param)[2];

            UpdateProgressBar(MainPanel.mainProgressBar, 0, false, true);
            if (serializer is IWzObjectSerializer)
            {
                UpdateProgressBar(MainPanel.mainProgressBar, objsToDump.Count, true, true);
                foreach (WzObject obj in objsToDump)
                {
                    ((IWzObjectSerializer)serializer).SerializeObject(obj, path);
                    UpdateProgressBar(MainPanel.mainProgressBar, 1, false, false);
                }
            }
            else if (serializer is WzNewXmlSerializer)
            {
                UpdateProgressBar(MainPanel.mainProgressBar, 1, true, true);
                ((WzNewXmlSerializer)serializer).ExportCombinedXml(objsToDump, path);
                UpdateProgressBar(MainPanel.mainProgressBar, 1, false, false);

            }
            threadDone = true;
        }

        //yes I know this is a stupid way to synchronize threads, I'm just too lazy to use events or locks
        private bool threadDone = false;
        private Thread runningThread = null;


        private delegate void ChangeAppStateDelegate(bool enabled);
        private void ChangeApplicationStateCallback(bool enabled)
        {
            mainMenu.Enabled = enabled;
            MainPanel.Enabled = enabled;
            AbortButton.Visible = !enabled;
        }
        private void ChangeApplicationState(bool enabled)
        {
            Invoke(new ChangeAppStateDelegate(ChangeApplicationStateCallback), new object[] { enabled });
        }

        private void xMLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog()
            {
                Title = HaRepacker.Properties.Resources.SelectWz,
                Filter = string.Format("{0}|*.wz", HaRepacker.Properties.Resources.WzFilter),
                Multiselect = true
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;
            FolderBrowserDialog folderDialog = new FolderBrowserDialog()
            {
                Description = HaRepacker.Properties.Resources.SelectOutDir
            };
            if (folderDialog.ShowDialog() != DialogResult.OK)
                return;

            WzClassicXmlSerializer serializer = new WzClassicXmlSerializer(UserSettings.Indentation, UserSettings.LineBreakType, false);
            threadDone = false;
            new Thread(new ParameterizedThreadStart(RunWzFilesExtraction)).Start((object)new object[] { dialog.FileNames, folderDialog.SelectedPath, encryptionBox.SelectedIndex, serializer });
            new Thread(new ParameterizedThreadStart(ProgressBarThread)).Start(serializer);
        }

        private delegate void UpdateProgressBarDelegate(ToolStripProgressBar pbar, int value, bool max, bool absolute); //max for .Maximum, !max for .Value
        private void UpdateProgressBarCallback(ToolStripProgressBar pbar, int value, bool max, bool absolute)
        {
            if (max)
            {
                if (absolute)
                    pbar.Maximum = value;
                else pbar.Maximum += value;
            }
            else
            {
                if (absolute)
                    pbar.Value = value;
                else pbar.Value += value;
            }
        }
        private void UpdateProgressBar(ToolStripProgressBar pbar, int value, bool max, bool absolute)
        {
            if (pbar.ProgressBar.InvokeRequired) pbar.ProgressBar.Invoke(new UpdateProgressBarDelegate(UpdateProgressBarCallback), new object[] { pbar, value, max, absolute });
            else UpdateProgressBarCallback(pbar, value, max, absolute);
        }


        private void ProgressBarThread(object param)
        {
            ProgressingWzSerializer serializer = (ProgressingWzSerializer)param;
            while (!threadDone)
            {
                int total = serializer.Total;
                UpdateProgressBar(MainPanel.secondaryProgressBar, total, true, true);
                UpdateProgressBar(MainPanel.secondaryProgressBar, Math.Min(total, serializer.Current), false, true);
                Thread.Sleep(500);
            }
            UpdateProgressBar(MainPanel.mainProgressBar, 0, true, true);
            UpdateProgressBar(MainPanel.secondaryProgressBar, 0, false, true);
            ChangeApplicationState(true);
            threadDone = false;
        }

        private string GetOutputDirectory()
        {
            return UserSettings.DefaultXmlFolder == "" ?
                SavedFolderBrowser.Show(HaRepacker.Properties.Resources.SelectOutDir)
                : UserSettings.DefaultXmlFolder;
        }

        private void rawDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog() { Title = HaRepacker.Properties.Resources.SelectWz, Filter = string.Format("{0}|*.wz", HaRepacker.Properties.Resources.WzFilter), Multiselect = true };
            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            string outPath = GetOutputDirectory();
            if (outPath == string.Empty)
            {
                MessageBox.Show(Properties.Resources.MainWzExportError, Properties.Resources.Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            WzPngMp3Serializer serializer = new WzPngMp3Serializer();
            threadDone = false;
            runningThread = new Thread(new ParameterizedThreadStart(RunWzFilesExtraction));
            runningThread.Start((object)new object[] { dialog.FileNames, outPath, encryptionBox.SelectedIndex, serializer });
            new Thread(new ParameterizedThreadStart(ProgressBarThread)).Start(serializer);
        }

        private void imgToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog()
            {
                Title = HaRepacker.Properties.Resources.SelectWz,
                Filter = string.Format("{0}|*.wz", HaRepacker.Properties.Resources.WzFilter),
                Multiselect = true
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            string outPath = GetOutputDirectory();
            if (outPath == string.Empty)
            {
                MessageBox.Show(Properties.Resources.MainWzExportError, Properties.Resources.Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            WzImgSerializer serializer = new WzImgSerializer();
            threadDone = false;
            runningThread = new Thread(new ParameterizedThreadStart(RunWzFilesExtraction));
            runningThread.Start((object)new object[] { dialog.FileNames, outPath, encryptionBox.SelectedIndex, serializer });
            new Thread(new ParameterizedThreadStart(ProgressBarThread)).Start(serializer);
        }

        private void imgToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            string outPath = GetOutputDirectory();
            if (outPath == string.Empty)
            {
                MessageBox.Show(Properties.Resources.MainWzExportError, Properties.Resources.Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<WzDirectory> dirs = new List<WzDirectory>();
            List<WzImage> imgs = new List<WzImage>();
            foreach (WzNode node in MainPanel.DataTree.SelectedNodes)
            {
                if (node.Tag is WzDirectory) dirs.Add((WzDirectory)node.Tag);
                else if (node.Tag is WzImage) imgs.Add((WzImage)node.Tag);
            }
            WzImgSerializer serializer = new WzImgSerializer();
            threadDone = false;
            runningThread = new Thread(new ParameterizedThreadStart(RunWzImgDirsExtraction));
            runningThread.Start((object)new object[] { dirs, imgs, outPath, serializer });
            new Thread(new ParameterizedThreadStart(ProgressBarThread)).Start(serializer);
        }

        private void pNGsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string outPath = GetOutputDirectory();
            if (outPath == string.Empty)
            {
                MessageBox.Show(Properties.Resources.MainWzExportError, Properties.Resources.Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<WzObject> objs = new List<WzObject>();
            foreach (WzNode node in MainPanel.DataTree.SelectedNodes)
            {
                if (node.Tag is WzObject)
                {
                    objs.Add((WzObject)node.Tag);
                }
            }

            WzPngMp3Serializer serializer = new WzPngMp3Serializer();
            threadDone = false;
            runningThread = new Thread(new ParameterizedThreadStart(RunWzObjExtraction));
            runningThread.Start((object)new object[] { objs, outPath, serializer });
            new Thread(new ParameterizedThreadStart(ProgressBarThread)).Start(serializer);
        }

        /// <summary>
        /// Export to private server toolstrip
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void privateServerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string outPath = GetOutputDirectory();
            if (outPath == string.Empty)
            {
                MessageBox.Show(Properties.Resources.MainWzExportError, Properties.Resources.Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<WzDirectory> dirs = new List<WzDirectory>();
            List<WzImage> imgs = new List<WzImage>();
            foreach (WzNode node in MainPanel.DataTree.SelectedNodes)
            {
                if (node.Tag is WzDirectory)
                    dirs.Add((WzDirectory)node.Tag);
                else if (node.Tag is WzImage)
                    imgs.Add((WzImage)node.Tag);
                else if (node.Tag is WzFile)
                {
                    dirs.Add(((WzFile)node.Tag).WzDirectory);
                }
            }
            WzClassicXmlSerializer serializer = new WzClassicXmlSerializer(UserSettings.Indentation, UserSettings.LineBreakType, false);
            threadDone = false;
            runningThread = new Thread(new ParameterizedThreadStart(RunWzImgDirsExtraction));
            runningThread.Start((object)new object[] { dirs, imgs, outPath, serializer });

            new Thread(new ParameterizedThreadStart(ProgressBarThread)).Start(serializer);
        }

        private void classicToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string outPath = GetOutputDirectory();
            if (outPath == string.Empty)
            {
                MessageBox.Show(Properties.Resources.MainWzExportError, Properties.Resources.Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<WzDirectory> dirs = new List<WzDirectory>();
            List<WzImage> imgs = new List<WzImage>();
            foreach (WzNode node in MainPanel.DataTree.SelectedNodes)
            {
                if (node.Tag is WzDirectory)
                    dirs.Add((WzDirectory)node.Tag);
                else if (node.Tag is WzImage)
                    imgs.Add((WzImage)node.Tag);
                else if (node.Tag is WzFile)
                {
                    dirs.Add(((WzFile)node.Tag).WzDirectory);
                }
            }
            WzClassicXmlSerializer serializer = new WzClassicXmlSerializer(UserSettings.Indentation, UserSettings.LineBreakType, true);
            threadDone = false;
            runningThread = new Thread(new ParameterizedThreadStart(RunWzImgDirsExtraction));
            runningThread.Start((object)new object[] { dirs, imgs, outPath, serializer });
            new Thread(new ParameterizedThreadStart(ProgressBarThread)).Start(serializer);
        }

        private void newToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog()
            {
                Title = HaRepacker.Properties.Resources.SelectOutXml,
                Filter = string.Format("{0}|*.xml", HaRepacker.Properties.Resources.XmlFilter)
            };
            if (dialog.ShowDialog() != DialogResult.OK)
                return;
            List<WzObject> objs = new List<WzObject>();

            foreach (WzNode node in MainPanel.DataTree.SelectedNodes)
            {
                if (node.Tag is WzObject)
                    objs.Add((WzObject)node.Tag);
            }
            WzNewXmlSerializer serializer = new WzNewXmlSerializer(UserSettings.Indentation, UserSettings.LineBreakType);
            threadDone = false;
            runningThread = new Thread(new ParameterizedThreadStart(RunWzObjExtraction));
            runningThread.Start((object)new object[] { objs, dialog.FileName, serializer });
            new Thread(new ParameterizedThreadStart(ProgressBarThread)).Start(serializer);
        }

        private void AbortButton_Click(object sender, EventArgs e)
        {
            if (Warning.Warn(HaRepacker.Properties.Resources.MainConfirmAbort))
            {
                threadDone = true;
                runningThread.Abort();
            }
        }

        private bool ValidAnimation(WzObject prop)
        {
            if (!(prop is WzSubProperty))
                return false;

            WzSubProperty castedProp = (WzSubProperty)prop;
            List<WzCanvasProperty> props = new List<WzCanvasProperty>(castedProp.WzProperties.Count);
            int foo;

            foreach (WzImageProperty subprop in castedProp.WzProperties)
            {
                if (!(subprop is WzCanvasProperty))
                    continue;
                if (!int.TryParse(subprop.Name, out foo))
                    return false;
                props.Add((WzCanvasProperty)subprop);
            }
            if (props.Count < 2)
                return false;

            props.Sort(new Comparison<WzCanvasProperty>(AnimationBuilder.PropertySorter));
            for (int i = 0; i < props.Count; i++)
                if (i.ToString() != props[i].Name)
                    return false;
            return true;
        }

        private void aPNGToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MainPanel.DataTree.SelectedNode == null) return;
            if (!ValidAnimation((WzObject)MainPanel.DataTree.SelectedNode.Tag))
                Warning.Error(HaRepacker.Properties.Resources.MainAnimationFail);
            else
            {
                SaveFileDialog dialog = new SaveFileDialog() { Title = HaRepacker.Properties.Resources.SelectOutApng, Filter = string.Format("{0}|*.png", HaRepacker.Properties.Resources.ApngFilter) };
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }
                AnimationBuilder.ExtractAnimation((WzSubProperty)MainPanel.DataTree.SelectedNode.Tag, dialog.FileName, UserSettings.UseApngIncompatibilityFrame);
            }
        }

        #region Image directory add
        /// <summary>
        /// Add WzDirectory
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wzDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzDirectoryToSelectedNode(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzImage
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wzImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzImageToSelectedNode(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzByte
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wzByteFloatPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzByteFloatToSelectedNode(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add new canvas toolstrip
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wzCanvasPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzCanvasToSelectedNode(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzIntProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wzCompressedIntPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzCompressedIntToSelectedNode(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzConvexProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wzConvexPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzConvexPropertyToSelectedNode(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzDoubleProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wzDoublePropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzDoublePropertyToSelectedNode(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzNullProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wzNullPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzNullPropertyToSelectedNode(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzSoundProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wzSoundPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzSoundPropertyToSelectedNode(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzStringProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wzStringPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzStringPropertyToSelectedIndex(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzSubProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wzSubPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzSubPropertyToSelectedIndex(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzShortProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wzUnsignedShortPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzUnsignedShortPropertyToSelectedIndex(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzUOLProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wzUolPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzUOLPropertyToSelectedIndex(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzVectorProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wzVectorPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzVectorPropertyToSelectedIndex(MainPanel.DataTree.SelectedNode);
        }
        #endregion

        private void expandAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.DataTree.ExpandAll();
        }

        private void collapseAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.DataTree.CollapseAll();
        }

        private void xMLToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            if (MainPanel.DataTree.SelectedNode == null || (!(MainPanel.DataTree.SelectedNode.Tag is WzDirectory) && !(MainPanel.DataTree.SelectedNode.Tag is WzFile) && !(MainPanel.DataTree.SelectedNode.Tag is IPropertyContainer)))
                return;
            WzFile wzFile = ((WzObject)MainPanel.DataTree.SelectedNode.Tag).WzFileParent;
            if (!(wzFile is WzFile))
                return;
            OpenFileDialog dialog = new OpenFileDialog()
            {
                Title = HaRepacker.Properties.Resources.SelectXml,
                Filter = string.Format("{0}|*.xml", HaRepacker.Properties.Resources.XmlFilter),
                Multiselect = true
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;
            WzXmlDeserializer deserializer = new WzXmlDeserializer(true, WzTool.GetIvByMapleVersion(wzFile.MapleVersion));
            yesToAll = false;
            noToAll = false;
            threadDone = false;

            runningThread = new Thread(new ParameterizedThreadStart(WzImporterThread));
            runningThread.Start(new object[]
            {
                deserializer, dialog.FileNames, MainPanel.DataTree.SelectedNode, null
            });
            new Thread(new ParameterizedThreadStart(ProgressBarThread)).Start(deserializer);
        }


        private delegate void InsertWzNode(WzNode node, WzNode parent);
        private void InsertWzNodeCallback(WzNode node, WzNode parent)
        {
            WzNode child = WzNode.GetChildNode(parent, node.Text);
            if (child != null)
            {
                if (ShowReplaceDialog(node.Text))
                    child.Delete();
                else return;
            }
            parent.AddNode(node);
        }

        private void InsertWzNodeThreadSafe(WzNode node, WzNode parent)
        {
            if (MainPanel.InvokeRequired) MainPanel.Invoke(new InsertWzNode(InsertWzNodeCallback), node, parent);
            else InsertWzNodeCallback(node, parent);
        }

        private bool yesToAll = false;
        private bool noToAll = false;

        private bool ShowReplaceDialog(string name)
        {
            if (yesToAll) return true;
            else if (noToAll) return false;
            else
            {
                ReplaceBox dialog = new ReplaceBox(name);
                dialog.ShowDialog();
                switch (dialog.result)
                {
                    case ReplaceResult.NoToAll:
                        noToAll = true;
                        return false;
                    case ReplaceResult.No:
                        return false;
                    case ReplaceResult.YesToAll:
                        yesToAll = true;
                        return true;
                    case ReplaceResult.Yes:
                        return true;
                }
            }
            throw new Exception("cant get here anyway");
        }

        private void WzImporterThread(object param)
        {
            ChangeApplicationState(false);

            object[] arr = (object[])param;
            ProgressingWzSerializer deserializer = (ProgressingWzSerializer)arr[0];
            string[] files = (string[])arr[1];
            WzNode parent = (WzNode)arr[2];
            byte[] iv = (byte[])arr[3];

            WzObject parentObj = (WzObject)parent.Tag;
            if (parentObj is WzFile)
                parentObj = ((WzFile)parentObj).WzDirectory;
            UpdateProgressBar(MainPanel.mainProgressBar, files.Length, true, true);

            foreach (string file in files)
            {
                List<WzObject> objs;
                try
                {
                    if (deserializer is WzXmlDeserializer)
                        objs = ((WzXmlDeserializer)deserializer).ParseXML(file);
                    else
                    {
                        bool successfullyParsedImage;
                        objs = new List<WzObject>
                        {
                            ((WzImgDeserializer)deserializer).WzImageFromIMGFile(file, iv, Path.GetFileName(file), out successfullyParsedImage)
                        };

                        if (!successfullyParsedImage)
                        {
                            MessageBox.Show(
                                string.Format(HaRepacker.Properties.Resources.MainErrorImportingWzImageFile, file),
                                HaRepacker.Properties.Resources.Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            continue;
                        }
                    }
                }
                catch (ThreadAbortException)
                {
                    return;
                }
                catch (Exception e)
                {
                    Warning.Error(string.Format(HaRepacker.Properties.Resources.MainInvalidFileError, file, e.Message));
                    UpdateProgressBar(MainPanel.mainProgressBar, 1, false, false);
                    continue;
                }
                foreach (WzObject obj in objs)
                {
                    if (((obj is WzDirectory || obj is WzImage) && parentObj is WzDirectory) || (obj is WzImageProperty && parentObj is IPropertyContainer))
                    {
                        WzNode node = new WzNode(obj, true);
                        InsertWzNodeThreadSafe(node, parent);
                    }
                }
                UpdateProgressBar(MainPanel.mainProgressBar, 1, false, false);
            }
            threadDone = true;
        }

        private void iMGToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            if (MainPanel.DataTree.SelectedNode == null || (!(MainPanel.DataTree.SelectedNode.Tag is WzDirectory) && !(MainPanel.DataTree.SelectedNode.Tag is WzFile) && !(MainPanel.DataTree.SelectedNode.Tag is IPropertyContainer)))
                return;

            WzFile wzFile = ((WzObject)MainPanel.DataTree.SelectedNode.Tag).WzFileParent;
            if (!(wzFile is WzFile))
                return;

            OpenFileDialog dialog = new OpenFileDialog()
            {
                Title = HaRepacker.Properties.Resources.SelectWzImg,
                Filter = string.Format("{0}|*.img", HaRepacker.Properties.Resources.WzImgFilter),
                Multiselect = true
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            WzMapleVersion wzImageImportVersion = WzMapleVersion.BMS;
            bool input = WzMapleVersionInputBox.Show(HaRepacker.Properties.Resources.InteractionWzMapleVersionTitle, out wzImageImportVersion);
            if (!input)
                return;

            byte[] iv = WzTool.GetIvByMapleVersion(wzImageImportVersion);
            WzImgDeserializer deserializer = new WzImgDeserializer(true);
            yesToAll = false;
            noToAll = false;
            threadDone = false;

            runningThread = new Thread(new ParameterizedThreadStart(WzImporterThread));
            runningThread.Start(
                new object[]
                {
                    deserializer, dialog.FileNames, MainPanel.DataTree.SelectedNode, iv
                });
            new Thread(new ParameterizedThreadStart(ProgressBarThread)).Start(deserializer);
        }

        private void searchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.findStrip.Visible = true;
        }

        private static readonly string HelpFile = "Help.htm";
        private void viewHelpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string helpPath = Path.Combine(Application.StartupPath, HelpFile);
            if (File.Exists(helpPath))
                Help.ShowHelp(this, HelpFile);
            else
                Warning.Error(string.Format(HaRepacker.Properties.Resources.MainHelpOpenFail, HelpFile));
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.DoCopy();
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.DoPaste();
        }

        #region Remove WZ Image resource
        /// <summary>
        /// Remove all WZ image resource to optimize for botting purposes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripMenuItem_RemoveImageResource_Click(object sender, EventArgs e)
        {
            MainPanel.DoRemoveImageResource();
        }
        #endregion

        #region GetWZKey
        private void wzKeyMenuItem_Click(object sender, EventArgs e)
        {
            // Create an instance of the open file dialog box.
            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            // Set filter options and filter index.
            openFileDialog1.Filter = "ZLZ file (.dll)|*.dll";
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.Multiselect = false;

            // Call the ShowDialog method to show the dialog box.
            DialogResult userClickedOK = openFileDialog1.ShowDialog();

            // Process input if the user clicked OK.
            if (userClickedOK == DialogResult.OK)
            {
                FileInfo fileinfo = new FileInfo(openFileDialog1.FileName);

                // Since this library is x86, HaRepacker needs to be compiled under x86! Not any CPU or x64
                bool setDLLDirectory = kernel32.SetDllDirectory(fileinfo.Directory.FullName);

                IntPtr module;
                if (((int)(module = kernel32.LoadLibrary(fileinfo.FullName))) == 0)
                {
                    uint lastError = kernel32.GetLastError();

                    MessageBox.Show("ZLZ not found. Last Error: " + lastError);
                }
                else
                {
                    try
                    {
                        var Method = Marshal.GetDelegateForFunctionPointer((IntPtr)(module.ToInt32() + 0x1340), typeof(GenerateKey)) as GenerateKey;
                        Method();
                        ShowKey(module);
                    }
                    catch
                    {
                        MessageBox.Show("Invalid KeyGen position");
                    }
                    finally
                    {
                        kernel32.FreeLibrary(module);
                    }
                }
            }
        }

        // see http://forum.ragezone.com/f921/release-gms-key-retriever-895646/index2.html
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GenerateKey();


        /// <summary>
        /// Display the Aes key used for the maplestory encryption.
        /// </summary>
        private static void ShowKey(IntPtr module)
        {
            const int KeyPos = 0x14020;
            const int KeyGen = 0x1340;

            StringBuilder sb = new StringBuilder();

            sb.Append("AesKey ");
            sb.AppendLine();

            for (int i = 0; i < 16 * 8; i += 4 * 4)
            {
                short value = (short)Marshal.ReadInt32((IntPtr)(module.ToInt32() + KeyPos + i));
                //    Console.Write("0x" + value.ToString("X") + "-0x00-0x00-0x00-");
                sb.AppendLine("0x" + value.ToString("X") + "-0x00-0x00-0x00");
            }
            sb.AppendLine();

            Clipboard.SetText(Environment.NewLine + sb.ToString());
            MessageBox.Show("Copied to your clipboard! " + Environment.NewLine + sb.ToString());
        }
        #endregion
    }
}