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
using MapleLib.WzLib.Serialization;
using System.Threading;
using HaRepacker.GUI.Interaction;
using MapleLib.WzLib.Util;
using System.Runtime.InteropServices;
using MapleLib.WzLib.WzStructure;
using System.Net;
using System.Text;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using Win32;
using HaRepacker.GUI.Panels;
using HaRepacker.GUI.Input;
using HaRepacker.Configuration;

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

        private bool mainFormLoaded = false;

        private MainPanel MainPanel = null;

        public MainForm(string wzToLoad, bool usingPipes, bool firstrun)
        {
            InitializeComponent();

            AddTabsInternal("Default");

            // Events
            Load += MainForm_Load1;
#if DEBUG
            debugToolStripMenuItem.Visible = true;
#endif

            // Sets theme color
            SetThemeColor();

            // encryptions
            AddWzEncryptionTypesToComboBox(encryptionBox);

            WindowState = Program.ConfigurationManager.ApplicationSettings.WindowMaximized ? FormWindowState.Maximized : FormWindowState.Normal;
            Size = new Size(
                Program.ConfigurationManager.ApplicationSettings.Width,
                Program.ConfigurationManager.ApplicationSettings.Height);


            // Set default selected main panel
            UpdateSelectedMainPanelTab();

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
            ContextMenuManager manager = new ContextMenuManager(MainPanel, MainPanel.UndoRedoMan);
            WzNode.ContextMenuBuilder = new WzNode.ContextMenuBuilderDelegate(manager.CreateMenu);

            // Focus on the tab control
            tabControl_MainPanels.Focus();

            // flag. loaded
            mainFormLoaded = true;
        }

        private void MainForm_Load1(object sender, EventArgs e)
        {
        }

        public void Interop_AddLoadedWzFileToManager(WzFile f)
        {
            Program.WzMan.InsertWzFileUnsafe(f, MainPanel);
        }

        #region Theme colors
        public void SetThemeColor()
        {
            if (Program.ConfigurationManager.UserSettings.ThemeColor == 0)//black
            {
                this.BackColor = Color.Black;
                mainMenu.BackColor = Color.Black;
                mainMenu.ForeColor = Color.White;

                /*for (int i = 0; i < mainMenu.Items.Count; i++)
                {
                    try
                    {
                        foreach (ToolStripMenuItem item in ((ToolStripMenuItem)mainMenu.Items[i]).DropDownItems)
                        {
                            item.BackColor = Color.Black;
                            item.ForeColor = Color.White;
                            MessageBox.Show(item.Name);
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                        //throw;
                    }
                }*/
                button_addTab.ForeColor = Color.White;
                button_addTab.BackColor = Color.Black;
            }
            else
            {
                this.BackColor = DefaultBackColor;
                mainMenu.BackColor = DefaultBackColor;
                mainMenu.ForeColor = Color.Black;

                button_addTab.ForeColor = Color.Black;
                button_addTab.BackColor = Color.White;
            }
        }
        #endregion

        private delegate void LoadWzFileDelegate(string path, MainPanel panel, bool detectMapleVersion);
        private void LoadWzFileCallback(string path, MainPanel panel, bool detectMapleVersion)
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

        private void LoadWzFileThreadSafe(string path, MainPanel panel, bool detectMapleVersion)
        {
            /*    if (panel.InvokeRequired)
                    panel.Invoke(new LoadWzFileDelegate(LoadWzFileCallback), path, panel, detectMapleVersion);
                else
                    LoadWzFileCallback(path, panel, detectMapleVersion);*/
            panel.Dispatcher.Invoke(() =>
            {
                LoadWzFileCallback(path, panel, detectMapleVersion);
            });
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
                    Program.ConfigurationManager.ApplicationSettings.UpdateServer + "version.txt"
                    )));
                string notice = Encoding.ASCII.GetString(
                    client.DownloadData(
                    Program.ConfigurationManager.ApplicationSettings.UpdateServer + "notice.txt"
                    ));
                string url = Encoding.ASCII.GetString(
                    client.DownloadData(
                    Program.ConfigurationManager.ApplicationSettings.UpdateServer + "url.txt"
                    ));
                if (version <= Program.Version_)
                    return;
                if (MessageBox.Show(string.Format(HaRepacker.Properties.Resources.MainUpdateAvailable, notice.Replace("%URL%", url)), HaRepacker.Properties.Resources.MainUpdateTitle, MessageBoxButtons.YesNo) == DialogResult.Yes)
                    Process.Start(url);
            }
            catch { }
        }

        #region Handlers
        private void MainForm_Load(object sender, EventArgs e)
        {
            encryptionBox.SelectedIndex = (int)Program.ConfigurationManager.ApplicationSettings.MapleVersion;
            if (Program.ConfigurationManager.UserSettings.AutoUpdate && Program.ConfigurationManager.ApplicationSettings.UpdateServer != "")
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
            if (!mainFormLoaded)
                return;

            if (this.Size.Width * this.Size.Height != 0)
            {
                RedockControls();

                Program.ConfigurationManager.ApplicationSettings.Height = this.Size.Height;
                Program.ConfigurationManager.ApplicationSettings.Width = this.Size.Width;
                Program.ConfigurationManager.ApplicationSettings.WindowMaximized = WindowState == FormWindowState.Maximized;
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

        /// <summary>
        ///  On key up event for hotkeys
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tabControl_MainPanels_KeyUp(object sender, KeyEventArgs e)
        {
            byte countTabs = Convert.ToByte(tabControl_MainPanels.TabCount);

            if (e.Control)
            {
                switch (e.KeyCode)
                {
                    case Keys.T: // Open new tab
                        AddTabsInternal();
                        break;
                    case Keys.O: // Open new WZ file
                        openToolStripMenuItem_Click(null, null);
                        break;
                    case Keys.N: // New
                        newToolStripMenuItem_Click(null, null);
                        break;
                    case Keys.A:
                        MainPanel.StartAnimateSelectedCanvas();
                        break;
                    case Keys.P:
                        MainPanel.StopCanvasAnimation();
                        break;

                    // Switch between tabs
                    case Keys.NumPad1:
                        tabControl_MainPanels.SelectTab(0);
                        break;
                    case Keys.NumPad2:
                        if (countTabs < 2) return;
                        tabControl_MainPanels.SelectTab(1);
                        break;
                    case Keys.NumPad3:
                        if (countTabs < 3) return;
                        tabControl_MainPanels.SelectTab(2);
                        break;
                    case Keys.NumPad4:
                        if (countTabs < 4) return;
                        tabControl_MainPanels.SelectTab(3);
                        break;
                    case Keys.NumPad5:
                        if (countTabs < 5) return;
                        tabControl_MainPanels.SelectTab(4);
                        break;
                    case Keys.NumPad6:
                        if (countTabs < 6) return;
                        tabControl_MainPanels.SelectTab(5);
                        break;
                    case Keys.NumPad7:
                        if (countTabs < 7) return;
                        tabControl_MainPanels.SelectTab(6);
                        break;
                    case Keys.NumPad8:
                        if (countTabs < 8) return;
                        tabControl_MainPanels.SelectTab(7);
                        break;
                    case Keys.NumPad9:
                        if (countTabs < 9) return;
                        tabControl_MainPanels.SelectTab(8);
                        break;
                    case Keys.NumPad0:
                        if (countTabs < 10) return;
                        tabControl_MainPanels.SelectTab(9);
                        break;
                }
            }
        }

        private void UpdateSelectedMainPanelTab()
        {
            TabPage selectedTab = tabControl_MainPanels.SelectedTab;
            if (selectedTab != null && selectedTab.Controls.Count > 0)
            {
                System.Windows.Forms.Integration.ElementHost elemntHost = (System.Windows.Forms.Integration.ElementHost)selectedTab.Controls[0];

                MainPanel = (MainPanel)elemntHost?.Child;
            }
        }

        /// <summary>
        /// Add a new tab to the TabControl
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_addTab_Click(object sender, EventArgs e)
        {
            AddTabsInternal();
        }

        /// <summary>
        /// Prompts a window to add a new tab
        /// </summary>
        private void AddTabsInternal(string defaultName = null)
        {
            if (tabControl_MainPanels.TabCount > 10)
            {
                return;
            }

            TabPage tabPage = new TabPage()
            {
                Margin = new Padding(1, 1, 1, 1)
            };
            System.Windows.Forms.Integration.ElementHost elemHost = new System.Windows.Forms.Integration.ElementHost();
            elemHost.Dock = DockStyle.Fill;
            elemHost.Child = new MainPanel();

            tabPage.Controls.Add(elemHost);


            string tabName = null;
            if (defaultName == null)
            {
                if (!NameInputBox.Show(Properties.Resources.MainAddTabTitle, 25, out tabName))
                {
                    return;
                }
                defaultName = tabName;
            } else
            {
                MainPanel = (MainPanel)elemHost.Child;
            }

            tabPage.Text = defaultName;

            tabControl_MainPanels.TabPages.Add(tabPage);

            // Focus on that tab control
            tabControl_MainPanels.Focus();
        }

        private void encryptionBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            Program.ConfigurationManager.ApplicationSettings.MapleVersion = (WzMapleVersion)encryptionBox.SelectedIndex;
        }

        /// <summary>
        /// Open file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

                bool errorOpeningFile_Admin = false;
                List<string> wzfilePathsToLoad = new List<string>();

                WzMapleVersion MapleVersionEncryptionSelected = (WzMapleVersion)encryptionBox.SelectedIndex;
                foreach (string filePath in dialog.FileNames)
                {
                    string filePathLowerCase = filePath.ToLower();

                    if (filePathLowerCase.EndsWith("data.wz") && WzTool.IsDataWzHotfixFile(filePath))
                    {
                        WzImage img = Program.WzMan.LoadDataWzHotfixFile(filePath, MapleVersionEncryptionSelected, MainPanel);
                        if (img == null)
                        {
                            errorOpeningFile_Admin = true;
                            break;
                        }
                    }
                    else if (WzTool.IsListFile(filePath))
                    {
                        new ListEditor(filePath, MapleVersionEncryptionSelected).Show();
                    }
                    else
                    {
                        wzfilePathsToLoad.Add(filePath); // add to list, so we can load it concurrently

                        if (filePathLowerCase.EndsWith("map.wz"))
                        {
                            string[] otherMapWzFiles = Directory.GetFiles(filePath.Substring(0, filePath.LastIndexOf("\\")), "Map*.wz");
                            foreach (string filePath_Others in otherMapWzFiles)
                            {
                                if (filePath_Others != filePath &&
                                    (filePath_Others.EndsWith("Map001.wz") || filePath_Others.EndsWith("Map2.wz"))) // damn, ugly hack to only whitelist those that Nexon uses. but someone could be saving as say Map_bak.wz in their folder.
                                {
                                    wzfilePathsToLoad.Add(filePath_Others);
                                }
                            }
                        }
                        else if (filePathLowerCase.EndsWith("mob.wz"))  // Now pre-load the other part of Mob.wz
                        {
                            string[] otherMobWzFiles = Directory.GetFiles(filePath.Substring(0, filePath.LastIndexOf("\\")), "Mob*.wz");
                            foreach (string filePath_Others in otherMobWzFiles)
                            {
                                if (filePath_Others != filePath &&
                                    filePath_Others.EndsWith("Mob2.wz"))
                                {
                                    wzfilePathsToLoad.Add(filePath_Others);
                                }
                            }
                        }
                    }
                }

                Dispatcher currentDispatcher = Dispatcher.CurrentDispatcher;

                // Load all original WZ files 
                Parallel.ForEach(wzfilePathsToLoad, filePath =>
                {
                    WzFile f = Program.WzMan.LoadWzFile(filePath, MapleVersionEncryptionSelected, MainPanel, currentDispatcher);
                    if (f == null)
                    {
                        errorOpeningFile_Admin = true;
                    }
                });

                // error opening one of the files
                if (errorOpeningFile_Admin)
                {
                    MessageBox.Show(HaRepacker.Properties.Resources.MainFileOpenFail, HaRepacker.Properties.Resources.Error);
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
            if (MainPanel.DataTree.SelectedNode == null)
                return;
            if (MainPanel.DataTree.SelectedNode.Tag is WzImage)
            {
                double zoomLevel = double.Parse(zoomTextBox.TextBox.Text);
                WzImage img = (WzImage)MainPanel.DataTree.SelectedNode.Tag;
                string mapName = img.Name.Substring(0, img.Name.Length - 4);

                if (!Directory.Exists("Renders\\" + mapName))
                {
                    Directory.CreateDirectory("Renders\\" + mapName);
                }
                try
                {
                    List<string> renderErrorList = new List<string>();

                    FHMapper.FHMapper mapper = new FHMapper.FHMapper(MainPanel);
                    mapper.ParseSettings();
                    bool rendered = mapper.TryRenderMapAndSave(img, zoomLevel, ref renderErrorList);

                    if (!rendered)
                    {
                        StringBuilder sb = new StringBuilder();
                        int i = 1;
                        foreach (string error in renderErrorList)
                        {
                            sb.Append("[").Append(i).Append("] ").Append(error);
                            sb.AppendLine();
                            i++;
                        }
                        MessageBox.Show(sb.ToString(), "Error rendering map");
                    }
                }
                catch (ArgumentException argExp)
                {
                    MessageBox.Show(argExp.Message, "Error rendering map");
                }
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

        /// <summary>
        /// New WZ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
            Program.ConfigurationManager.ApplicationSettings.WindowMaximized = WindowState == FormWindowState.Maximized;
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


            if (!Directory.Exists(baseDir))
            {
                Directory.CreateDirectory(baseDir);
            }

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
            MainPanel.IsEnabled = enabled;
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

            WzClassicXmlSerializer serializer = new WzClassicXmlSerializer(
                Program.ConfigurationManager.UserSettings.Indentation,
                Program.ConfigurationManager.UserSettings.LineBreakType, false);
            threadDone = false;
            new Thread(new ParameterizedThreadStart(RunWzFilesExtraction)).Start((object)new object[] { dialog.FileNames, folderDialog.SelectedPath, encryptionBox.SelectedIndex, serializer });
            new Thread(new ParameterizedThreadStart(ProgressBarThread)).Start(serializer);
        }

        private delegate void UpdateProgressBarDelegate(ToolStripProgressBar pbar, int value, bool max, bool absolute); //max for .Maximum, !max for .Value
        private void UpdateProgressBarCallback(System.Windows.Controls.ProgressBar pbar, int value, bool max, bool absolute)
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
        private void UpdateProgressBar(System.Windows.Controls.ProgressBar pbar, int value, bool max, bool absolute)
        {
            pbar.Dispatcher.Invoke(() =>
            {
                UpdateProgressBarCallback(pbar, value, max, absolute);
            });
            /*   if (pbar.ProgressBar.InvokeRequired)
                   pbar.ProgressBar.Invoke(new UpdateProgressBarDelegate(UpdateProgressBarCallback), new object[] { pbar, value, max, absolute });
               else UpdateProgressBarCallback(pbar, value, max, absolute);*/
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
            return Program.ConfigurationManager.UserSettings.DefaultXmlFolder == "" ?
                SavedFolderBrowser.Show(HaRepacker.Properties.Resources.SelectOutDir)
                : Program.ConfigurationManager.UserSettings.DefaultXmlFolder;
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
                if (node.Tag is WzDirectory)
                {
                    dirs.Add((WzDirectory)node.Tag);
                }
                else if (node.Tag is WzImage)
                {
                    imgs.Add((WzImage)node.Tag);
                }
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
            WzClassicXmlSerializer serializer = new WzClassicXmlSerializer(
                Program.ConfigurationManager.UserSettings.Indentation,
                Program.ConfigurationManager.UserSettings.LineBreakType, false);
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
            WzClassicXmlSerializer serializer = new WzClassicXmlSerializer(
                Program.ConfigurationManager.UserSettings.Indentation,
                Program.ConfigurationManager.UserSettings.LineBreakType, true);
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
            WzNewXmlSerializer serializer = new WzNewXmlSerializer(
                Program.ConfigurationManager.UserSettings.Indentation,
                Program.ConfigurationManager.UserSettings.LineBreakType);
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

        #region Extras
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
                AnimationBuilder.ExtractAnimation((WzSubProperty)MainPanel.DataTree.SelectedNode.Tag, dialog.FileName,
                    Program.ConfigurationManager.UserSettings.UseApngIncompatibilityFrame);
            }
        }

        /// <summary>
        /// Wz string searcher tool
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripMenuItem_searchWzStrings_Click(object sender, EventArgs e)
        {
            // Map name load
            string loadedWzVersion;
            WzStringSearchFormDataCache dataCache = new WzStringSearchFormDataCache((WzMapleVersion)encryptionBox.SelectedIndex);
            if (dataCache.OpenBaseWZFile(out loadedWzVersion))
            {
                WzStringSearchForm form = new WzStringSearchForm(dataCache, loadedWzVersion);
                form.Show();
            }
        }

        /// <summary>
        /// Get packet encryption keys from ZLZ.dll
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripMenuItem_WzEncryption_Click(object sender, EventArgs e)
        {
            ZLZPacketEncryptionKeyForm form = new ZLZPacketEncryptionKeyForm();
            bool opened = form.OpenZLZDllFile();

            if (opened)
                form.Show();
        }
        #endregion

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
        /// Add WzLongProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wzLongPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzLongToSelectedNode(MainPanel.DataTree.SelectedNode);
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
            MainPanel.Dispatcher.Invoke(() =>
            {
                InsertWzNodeCallback(node, parent);
            });
            /*  if (MainPanel.InvokeRequired)
                  MainPanel.Invoke(new InsertWzNode(InsertWzNodeCallback), node, parent);
              else
                  InsertWzNodeCallback(node, parent);*/
        }

        private bool yesToAll = false;
        private bool noToAll = false;
        private ReplaceResult result;

        private bool ShowReplaceDialog(string name)
        {
            if (yesToAll) return true;
            else if (noToAll) return false;
            else
            {
                ReplaceBox.Show(name, out result);
                switch (result)
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
            //MainPanel.findStrip.Visible = true;
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
    }
}