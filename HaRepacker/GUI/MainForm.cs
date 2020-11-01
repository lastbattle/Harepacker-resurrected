/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.Serialization;
using System.Threading;
using HaRepacker.GUI.Interaction;
using MapleLib.WzLib.Util;
using System.Net;
using System.Text;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using HaRepacker.GUI.Panels;
using HaRepacker.GUI.Input;
using System.Reflection;
using HaRepacker.GUI.Panels.SubPanels;
using MapleLib.PacketLib;
using System.Timers;
using static MapleLib.Configuration.UserSettings;
using HaSharedLibrary;

namespace HaRepacker.GUI
{
    public partial class MainForm : Form
    {
        private bool mainFormLoaded = false;

        private MainPanel MainPanel = null;

        public MainForm(string wzToLoad, bool usingPipes, bool firstrun)
        {
            InitializeComponent();

            AddTabsInternal("Default");

            // Events
#if DEBUG
            debugToolStripMenuItem.Visible = true;
#endif

            // Sets theme color
            SetThemeColor();

            // encryptions
            AddWzEncryptionTypesToComboBox(encryptionBox);
            // Set encryption box
            SetWzEncryptionBoxSelectionByWzMapleVersion(Program.ConfigurationManager.ApplicationSettings.MapleVersion);


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
                            using (NamedPipeClientStream clientPipe = new NamedPipeClientStream(".", Program.pipeName, PipeDirection.Out))
                            {
                                clientPipe.Connect(0);
                                using (StreamWriter sw = new StreamWriter(clientPipe))
                                {
                                    sw.WriteLine(wzToLoad);
                                }
                                clientPipe.WaitForPipeDrain();
                            }
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
                SetWzEncryptionBoxSelectionByWzMapleVersion(encVersion);

                LoadWzFileThreadSafe(wzToLoad, MainPanel, false);
            }
            ContextMenuManager manager = new ContextMenuManager(MainPanel, MainPanel.UndoRedoMan);
            WzNode.ContextMenuBuilder = new WzNode.ContextMenuBuilderDelegate(manager.CreateMenu);

            // Focus on the tab control
            tabControl_MainPanels.Focus();

            // flag. loaded
            mainFormLoaded = true;
        }

        public void Interop_AddLoadedWzFileToManager(WzFile f)
        {
            Program.WzFileManager.InsertWzFileUnsafe(f, MainPanel);
        }

        #region Theme colors
        public void SetThemeColor()
        {
            if (Program.ConfigurationManager.UserSettings.ThemeColor == (int) UserSettingsThemeColor.Dark)//black
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

        #region Wz Encryption selection combobox
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
                HaRepacker.Properties.Resources.EncTypeNone,
                HaRepacker.Properties.Resources.EncTypeCustom,
                 HaRepacker.Properties.Resources.EncTypeGenerate,
            };
            bool isToolStripComboBox = encryptionBox is ToolStripComboBox;

            int i = 0;
            foreach (string res in resources)
            {
                if (isToolStripComboBox)
                    ((ToolStripComboBox)encryptionBox).Items.Add(res); // in mainform
                else
                {
                    if (i != 4) // dont show bruteforce option in SaveForm
                    {
                        ((ComboBox)encryptionBox).Items.Add(res); // in saveForm
                    }
                }
                i++;
            }
        }

        /// <summary>
        /// On encryption box selection changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void encryptionBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!mainFormLoaded) // first run during app startup
            {
                return;
            }

            int selectedIndex = encryptionBox.SelectedIndex;
            WzMapleVersion wzMapleVer = GetWzMapleVersionByWzEncryptionBoxSelection(selectedIndex);
            Program.ConfigurationManager.ApplicationSettings.MapleVersion = wzMapleVer;

            if (wzMapleVer == WzMapleVersion.CUSTOM)
            {
                CustomWZEncryptionInputBox customWzInputBox = new CustomWZEncryptionInputBox();
                customWzInputBox.ShowDialog();
            }
        }

        /// <summary>
        /// Gets the WzMapleVersion enum by encryptionBox selection index
        /// </summary>
        /// <param name="selectedIndex"></param>
        /// <returns></returns>
        public static WzMapleVersion GetWzMapleVersionByWzEncryptionBoxSelection(int selectedIndex)
        {
            WzMapleVersion wzMapleVer = WzMapleVersion.CUSTOM;
            switch (selectedIndex)
            {
                case 0:
                    wzMapleVer = WzMapleVersion.GMS;
                    break;
                case 1:
                    wzMapleVer = WzMapleVersion.EMS;
                    break;
                case 2:
                    wzMapleVer = WzMapleVersion.BMS;
                    break;
                case 3:
                    wzMapleVer = WzMapleVersion.CUSTOM;
                    break;
                case 4:
                    wzMapleVer = WzMapleVersion.GENERATE;
                    break;
                default: // hmm?
                    wzMapleVer = WzMapleVersion.BMS; // just default anyway to modern maplestory
                    break;
            }
            return wzMapleVer;
        }

        /// <summary>
        /// Gets the Combobox selection index by WzMapleVersion
        /// </summary>
        /// <param name="versionSelected"></param>
        /// <param name="fromNewForm">Called from NewForm.cs</param>
        /// <returns></returns>
        public static int GetIndexByWzMapleVersion(WzMapleVersion versionSelected, bool fromNewForm = false)
        {
            int setIndex = 0;
            switch (versionSelected)
            {
                case WzMapleVersion.GMS:
                    setIndex = 0;
                    break;
                case WzMapleVersion.EMS:
                    setIndex = 1;
                    break;
                case WzMapleVersion.BMS:
                    setIndex = 2;
                    break;
                case WzMapleVersion.CUSTOM:
                    setIndex = 3;
                    break;
                case WzMapleVersion.GENERATE:
                    if (fromNewForm) // dont return GENERATE, as that option is unavailable when creating a new WZ via NewForm.
                        setIndex = 2; // BMS
                    else 
                        setIndex = 4;
                    break;
            }
            return setIndex;
        }

        /// <summary>
        /// Sets the ComboBox selection index by WzMapleVersion enum
        /// </summary>
        /// <param name="versionSelected"></param>
        private void SetWzEncryptionBoxSelectionByWzMapleVersion(WzMapleVersion versionSelected)
        {
            encryptionBox.SelectedIndex = GetIndexByWzMapleVersion(versionSelected);
        }
        #endregion

        private delegate void LoadWzFileDelegate(string path, MainPanel panel, bool detectMapleVersion);
        private void LoadWzFileCallback(string path, MainPanel panel, bool detectMapleVersion)
        {
            try
            {
                WzFile loadedWzFile;
                if (detectMapleVersion)
                    loadedWzFile = Program.WzFileManager.LoadWzFile(path);
                else
                    loadedWzFile = Program.WzFileManager.LoadWzFile(path, (WzMapleVersion) GetWzMapleVersionByWzEncryptionBoxSelection(encryptionBox.SelectedIndex));

                if (loadedWzFile != null)
                    Program.WzFileManager.AddLoadedWzFileToMainPanel(loadedWzFile, panel);
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

        #region Handlers
        private void MainForm_Load(object sender, EventArgs e)
        {
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
        private void Button_addTab_Click(object sender, EventArgs e)
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
            System.Windows.Forms.Integration.ElementHost elemHost = new System.Windows.Forms.Integration.ElementHost
            {
                Dock = DockStyle.Fill,
                Child = new MainPanel()
            };
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
        #endregion

        #region WZ IV Key bruteforcing
        private ulong wzKeyBruteforceTries = 0;
        private DateTime wzKeyBruteforceStartTime = DateTime.Now;
        private bool wzKeyBruteforceCompleted = false;

        private System.Timers.Timer aTimer_wzKeyBruteforce = null;

        /// <summary>
        /// Find needles in a haystack o_O
        /// </summary>
        /// <param name="currentDispatcher"></param>
        private void StartWzKeyBruteforcing(Dispatcher currentDispatcher)
        {
            // Generate WZ keys via a test WZ file
            using (OpenFileDialog dialog = new OpenFileDialog()
            {
                Title = HaRepacker.Properties.Resources.SelectWz,
                Filter = string.Format("{0}|TamingMob.wz", HaRepacker.Properties.Resources.WzFilter), // Use the smallest possible file
                Multiselect = false
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                // Show splash screen
                MainPanel.OnSetPanelLoading(currentDispatcher);
                MainPanel.loadingPanel.SetWzIvBruteforceStackpanelVisiblity(System.Windows.Visibility.Visible);


                // Reset variables
                wzKeyBruteforceTries = 0;
                wzKeyBruteforceStartTime = DateTime.Now;
                wzKeyBruteforceCompleted = false;


                int processorCount = Environment.ProcessorCount * 3; // 8 core = 16 (with ht, smt) , multiply by 3 seems to be the magic number. it falls off after 4
                List<int> cpuIds = new List<int>();
                for (int cpuId_ = 0; cpuId_ < processorCount; cpuId_++)
                {
                    cpuIds.Add(cpuId_);
                }

                // UI update thread
                if (aTimer_wzKeyBruteforce != null)
                {
                    aTimer_wzKeyBruteforce.Stop();
                    aTimer_wzKeyBruteforce = null;
                }
                aTimer_wzKeyBruteforce = new System.Timers.Timer();
                aTimer_wzKeyBruteforce.Elapsed += new ElapsedEventHandler(OnWzIVKeyUIUpdateEvent);
                aTimer_wzKeyBruteforce.Interval = 5000;
                aTimer_wzKeyBruteforce.Enabled = true;


                // Key finder thread
                Task.Run(() =>
                {
                    Thread.Sleep(3000); // delay 3 seconds before starting

                    var parallelOption = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = processorCount,
                    };
                    ParallelLoopResult loop = Parallel.ForEach(cpuIds, parallelOption, cpuId =>
                    {
                        WzKeyBruteforceComputeTask(cpuId, processorCount, dialog, currentDispatcher);
                    });
                });
            }
        }

        /// <summary>
        /// UI Updating thread
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        private void OnWzIVKeyUIUpdateEvent(object source, ElapsedEventArgs e)
        {
            if (aTimer_wzKeyBruteforce == null)
                return;
            if (wzKeyBruteforceCompleted)
            {
                aTimer_wzKeyBruteforce.Stop();
                aTimer_wzKeyBruteforce = null;

                MainPanel.loadingPanel.SetWzIvBruteforceStackpanelVisiblity(System.Windows.Visibility.Collapsed);
            }

            MainPanel.loadingPanel.WzIvKeyDuration = DateTime.Now.Ticks - wzKeyBruteforceStartTime.Ticks;
            MainPanel.loadingPanel.WzIvKeyTries = wzKeyBruteforceTries;
        }

        /// <summary>
        /// Internal compute task for figuring out the WzKey automaticagically 
        /// </summary>
        /// <param name="cpuId_"></param>
        /// <param name="processorCount"></param>
        /// <param name="dialog"></param>
        /// <param name="currentDispatcher"></param>
        private void WzKeyBruteforceComputeTask(int cpuId_, int processorCount, OpenFileDialog dialog, Dispatcher currentDispatcher)
        {
            int cpuId = cpuId_;

            // try bruteforce keys
            const long startValue = int.MinValue;
            const long endValue = int.MaxValue;

            long lookupRangePerCPU = (endValue - startValue) / processorCount;

            Debug.WriteLine("CPUID {0}. Looking up from {1} to {2}. [Range = {3}]  TEST: {4} {5}",
                cpuId,
                (startValue + (lookupRangePerCPU * cpuId)),
                (startValue + (lookupRangePerCPU * (cpuId + 1))),
                lookupRangePerCPU,
                (lookupRangePerCPU * cpuId), (lookupRangePerCPU * (cpuId + 1)));

            for (long i = (startValue + (lookupRangePerCPU * cpuId)); i < (startValue + (lookupRangePerCPU * (cpuId + 1))); i++)  // 2 bill key pairs? o_O
            {
                if (wzKeyBruteforceCompleted)
                    break;

                byte[] bytes = new byte[4];
                unsafe
                {
                    fixed (byte* pbytes = &bytes[0])
                    {
                        *(int*)pbytes = (int) i;
                    }
                }
                bool tryDecrypt = WzTool.TryBruteforcingWzIVKey(dialog.FileName, bytes);
                //Debug.WriteLine("{0} = {1}", cpuId, HexTool.ToString(new PacketWriter(bytes).ToArray()));
                if (tryDecrypt)
                {
                    wzKeyBruteforceCompleted = true;

                    // Hide panel splash sdcreen
                    Action action = () =>
                    {
                        MainPanel.OnSetPanelLoadingCompleted(currentDispatcher);
                        MainPanel.loadingPanel.SetWzIvBruteforceStackpanelVisiblity(System.Windows.Visibility.Collapsed);
                    };
                    currentDispatcher.BeginInvoke(action);


                    PacketWriter writer = new PacketWriter(4);
                    writer.WriteBytes(bytes);
                    MessageBox.Show("Found the encryption key to the WZ file:\r\n" + HexTool.ToString(writer.ToArray()), "Success");
                    Debug.WriteLine("Found key. Key = " + HexTool.ToString(writer.ToArray()));

                    break;
                }
                wzKeyBruteforceTries++;
            }
        }
        #endregion

        #region Toolstrip Menu items
        /// <summary>
        /// Open file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Dispatcher currentDispatcher = Dispatcher.CurrentDispatcher;

            WzMapleVersion MapleVersionEncryptionSelected = GetWzMapleVersionByWzEncryptionBoxSelection(encryptionBox.SelectedIndex);
            if (MapleVersionEncryptionSelected == WzMapleVersion.GENERATE)
            {
                StartWzKeyBruteforcing(currentDispatcher); // find needles in a haystack
                return;
            }

            // Load WZ file
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

                List<string> wzfilePathsToLoad = new List<string>();
                foreach (string filePath in dialog.FileNames)
                {
                    string filePathLowerCase = filePath.ToLower();

                    if (filePathLowerCase.EndsWith("data.wz") && WzTool.IsDataWzHotfixFile(filePath))
                    {
                        WzImage img = Program.WzFileManager.LoadDataWzHotfixFile(filePath, MapleVersionEncryptionSelected, MainPanel);
                        if (img == null)
                        {
                            MessageBox.Show(HaRepacker.Properties.Resources.MainFileOpenFail, HaRepacker.Properties.Resources.Error);
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

                        // Check if there are any related files
                        string[] wzsWithRelatedFiles = { "Map", "Mob", "Skill", "Sound" };
                        bool bWithRelated = false;
                        string relatedFileName = null;

                        foreach (string wz in wzsWithRelatedFiles) 
                            if (filePathLowerCase.EndsWith(wz.ToLower() + ".wz"))
                            {
                                bWithRelated = true;
                                relatedFileName = wz;
                                break;
                            }
                        if (bWithRelated)
                        {
                            if (Program.ConfigurationManager.UserSettings.AutoloadRelatedWzFiles)
                            {
                                string[] otherMapWzFiles = Directory.GetFiles(filePath.Substring(0, filePath.LastIndexOf("\\")), relatedFileName + "*.wz");
                                foreach (string filePath_Others in otherMapWzFiles)
                                {
                                    if (filePath_Others != filePath)
                                        wzfilePathsToLoad.Add(filePath_Others);
                                }
                            }
                        }
                    }
                }

                // Show splash screen
                MainPanel.OnSetPanelLoading();

                // Try opening one, to see if the user is having the right priviledge

                // Load all original WZ files 
                await Task.Run(() =>
                {
                    List<WzFile> loadedWzFiles = new List<WzFile>();
                    ParallelLoopResult loop = Parallel.ForEach(wzfilePathsToLoad, filePath =>
                    {
                        WzFile f = Program.WzFileManager.LoadWzFile(filePath, MapleVersionEncryptionSelected);
                        if (f == null)
                        {
                            // error should be thrown 
                        }
                        else
                        {
                            lock (loadedWzFiles)
                            {
                                loadedWzFiles.Add(f);
                            }
                        }
                    });
                    while (!loop.IsCompleted)
                    {
                        Thread.Sleep(100); //?
                    }

                    foreach (WzFile wzFile in loadedWzFiles) // add later, once everything is loaded to memory
                    {
                        Program.WzFileManager.AddLoadedWzFileToMainPanel(wzFile, MainPanel, currentDispatcher);
                    }
                }); // load complete

                // Hide panel splash sdcreen
                MainPanel.OnSetPanelLoadingCompleted();
            }
        }

        private void unloadAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Warning.Warn(HaRepacker.Properties.Resources.MainUnloadAll))
                Program.WzFileManager.UnloadAll();
        }

        private void reloadAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Warning.Warn(HaRepacker.Properties.Resources.MainReloadAll))
                Program.WzFileManager.ReloadAll(MainPanel);
        }

        /// <summary>
        /// Field/ map rendering
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// On closing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Program.ConfigurationManager.ApplicationSettings.WindowMaximized = WindowState == FormWindowState.Maximized;
            e.Cancel = !Warning.Warn(HaRepacker.Properties.Resources.MainConfirmExit);

            // Save app settings quickly
            if (!e.Cancel)
            {
                Program.ConfigurationManager.Save();
            }
        }

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
            WzMapleVersion version = GetWzMapleVersionByWzEncryptionBoxSelection( ((int[])param)[2]);
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

                WzFileParseStatus parseStatus = f.ParseWzFile();

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
                string escapedPath = Path.Combine(baseDir, ProgressingWzSerializer.EscapeInvalidFilePathNames(img.Name));

                serializer.SerializeImage(img, escapedPath);
                UpdateProgressBar(MainPanel.mainProgressBar, 1, false, false);
            }
            foreach (WzDirectory dir in dirsToDump)
            {
                string escapedPath = Path.Combine(baseDir, ProgressingWzSerializer.EscapeInvalidFilePathNames(dir.Name));

                serializer.SerializeDirectory(dir, escapedPath);
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

        private void expandAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.DataTree.BeginUpdate();
            MainPanel.DataTree.ExpandAll();
            MainPanel.DataTree.EndUpdate();
        }

        private void collapseAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.DataTree.BeginUpdate();
            MainPanel.DataTree.CollapseAll();
            MainPanel.DataTree.EndUpdate();
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
        private void ViewHelpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string helpPath = Path.Combine(Application.StartupPath, HelpFile);
            if (File.Exists(helpPath))
                Help.ShowHelp(this, HelpFile);
            else
                Warning.Error(string.Format(HaRepacker.Properties.Resources.MainHelpOpenFail, HelpFile));
        }

        private void CopyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.DoCopy();
        }

        private void PasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.DoPaste();
        }
        /// <summary>
        /// Wz string searcher tool
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToolStripMenuItem_searchWzStrings_Click(object sender, EventArgs e)
        {
            // Map name load
            string loadedWzVersion;
            WzStringSearchFormDataCache dataCache = new WzStringSearchFormDataCache(GetWzMapleVersionByWzEncryptionBoxSelection(encryptionBox.SelectedIndex));
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
            AssemblyName executingAssemblyName = Assembly.GetExecutingAssembly().GetName();
            //similarly to find process architecture  
            var assemblyArchitecture = executingAssemblyName.ProcessorArchitecture;

            if (assemblyArchitecture == ProcessorArchitecture.X86)
            {
                ZLZPacketEncryptionKeyForm form = new ZLZPacketEncryptionKeyForm();
                bool opened = form.OpenZLZDllFile();

                if (opened)
                    form.Show();
            }
            else
            {
                MessageBox.Show(HaRepacker.Properties.Resources.ExecutingAssemblyError, HaRepacker.Properties.Resources.Warning, MessageBoxButtons.OK);
            }
        }
        #endregion 

        private void AbortButton_Click(object sender, EventArgs e)
        {
            if (Warning.Warn(HaRepacker.Properties.Resources.MainConfirmAbort))
            {
                threadDone = true;
                runningThread.Abort();
            }
        }

        #region Image directory add
        /// <summary>
        /// Add WzDirectory
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WzDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzDirectoryToSelectedNode(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzImage
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WzImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzImageToSelectedNode(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzByte
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WzByteFloatPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzByteFloatToSelectedNode(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add new canvas toolstrip
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WzCanvasPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzCanvasToSelectedNode(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzIntProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WzCompressedIntPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzCompressedIntToSelectedNode(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzLongProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WzLongPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzLongToSelectedNode(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzConvexProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WzConvexPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzConvexPropertyToSelectedNode(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzDoubleProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WzDoublePropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzDoublePropertyToSelectedNode(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzNullProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WzNullPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzNullPropertyToSelectedNode(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzSoundProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WzSoundPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzSoundPropertyToSelectedNode(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzStringProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WzStringPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzStringPropertyToSelectedIndex(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzSubProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WzSubPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzSubPropertyToSelectedIndex(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzShortProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WzUnsignedShortPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzUnsignedShortPropertyToSelectedIndex(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzUOLProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WzUolPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzUOLPropertyToSelectedIndex(MainPanel.DataTree.SelectedNode);
        }

        /// <summary>
        /// Add WzVectorProperty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WzVectorPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzVectorPropertyToSelectedIndex(MainPanel.DataTree.SelectedNode);
        }
        #endregion



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
            parent.AddNode(node, true);
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
    }
}
