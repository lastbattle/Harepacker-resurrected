/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Timers;
using System.Threading;
using System.Reflection;

using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using MapleLib.PacketLib;
using MapleLib.MapleCryptoLib;
using static MapleLib.Configuration.UserSettings;

using HaRepacker.GUI.Panels;
using HaRepacker.GUI.Interaction;
using HaRepacker.GUI.Input;
using HaRepacker.Comparer;

using HaSharedLibrary;
using MapleLib.WzLib.WzProperties;
using HaSharedLibrary.SystemInterop;
using MapleLib;
using System.Text.RegularExpressions;
using MapleLib.Configuration;
using System.Runtime.CompilerServices;
using HaSharedLibrary.Util;
using MapleLib.WzLib.Serializer;

namespace HaRepacker.GUI
{
    public partial class MainForm : Form
    {
        private readonly bool mainFormLoaded = false;

        private MainPanel MainPanel = null;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="wzPathToLoad"></param>
        /// <param name="usingPipes"></param>
        /// <param name="firstrun"></param>
        public MainForm(string wzPathToLoad, bool usingPipes, bool firstrun)
        {
            InitializeComponent();

            AddTabsInternal("Default");

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
            
            // Drag and drop file
            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;

            // Drag and drop at the data tree
            this.MainPanel.DataTree.DragEnter += MainForm_DragEnter;
            this.MainPanel.DataTree.DragDrop += MainForm_DragDrop;

            // Set default selected main panel
            UpdateSelectedMainPanelTab();

            if (usingPipes)
            {
                try
                {
                    Program.pipe = new NamedPipeServerStream(Program.pipeName, PipeDirection.In);
                    Program.pipeThread = new Thread(new ThreadStart(PipeServer))
                    {
                        IsBackground = true
                    };
                    Program.pipeThread.Start();
                }
                catch (IOException)
                {
                    if (wzPathToLoad != null)
                    {
                        try
                        {
                            using (NamedPipeClientStream clientPipe = new NamedPipeClientStream(".", Program.pipeName, PipeDirection.Out))
                            {
                                clientPipe.Connect(0);
                                using (StreamWriter sw = new StreamWriter(clientPipe))
                                {
                                    sw.WriteLine(wzPathToLoad);
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
            if (wzPathToLoad != null && File.Exists(wzPathToLoad))
            {
                short version;
                WzMapleVersion encVersion = WzTool.DetectMapleVersion(wzPathToLoad, out version);
                SetWzEncryptionBoxSelectionByWzMapleVersion(encVersion);

                LoadWzFileCallback(wzPathToLoad);
            }
            ContextMenuManager manager = new ContextMenuManager(MainPanel, MainPanel.UndoRedoMan);
            WzNode.ContextMenuBuilder = new WzNode.ContextMenuBuilderDelegate(manager.CreateMenu);

            // Focus on the tab control
            tabControl_MainPanels.Focus();

            // flag. loaded
            mainFormLoaded = true;
        }

        #region Load, unload WZ files + Panels & TreeView management
        /// <summary>
        /// MainForm -- Drag the file from Windows Explorer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void MainForm_DragEnter(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                e.Effect = DragDropEffects.Move; // Allow the file to be copied
            }
        }

        /// <summary>
        /// MainForm -- Drop the file from Windows Explorer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_DragDrop(object sender, DragEventArgs e) {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)) {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                // process the drag and dropped files
                OpenFileInternal(files);
            }
        }

        public void Interop_AddLoadedWzFileToManager(WzFile f)
        {
            InsertWzFileToPanel(f);
        }

        private delegate void LoadWzFileDelegate(string path);
        private void LoadWzFileCallback(string path)
        {
            try
            {
                WzFile loadedWzFile = Program.WzFileManager.LoadWzFile(path, (WzMapleVersion)GetWzMapleVersionByWzEncryptionBoxSelection(encryptionBox.SelectedIndex));
                if (loadedWzFile != null)
                {
                    WzNode node = new WzNode(loadedWzFile);

                    MainPanel.DataTree.BeginUpdate();

                    MainPanel.DataTree.Nodes.Add(node);
                    SortNodesRecursively(node);
                    MainPanel.DataTree.EndUpdate();
                }
            }
            catch
            {
                Warning.Error(string.Format(HaRepacker.Properties.Resources.MainCouldntOpenWZ, path));
            }
        }

        /// <summary>
        /// Sort all nodes that is a parent of 
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="sortFromTheParentNode">Sorts only items in the parent node</param>
        public void SortNodesRecursively(WzNode parent, bool sortFromTheParentNode = false)
        {
            if (Program.ConfigurationManager.UserSettings.Sort || sortFromTheParentNode)
            {
                parent.TreeView.BeginUpdate();
                parent.TreeView.TreeViewNodeSorter = new TreeViewNodeSorter(sortFromTheParentNode ? parent : null);
                parent.TreeView.EndUpdate();
            }
        }

        public void SortNodeProperties(WzNode node) {
            if (node.Tag is WzSubProperty) {
                WzNode nodeParent = (WzNode) node.Parent;

                nodeParent.TreeView.BeginUpdate();

                // sort the order in the WzSubProperty
                WzSubProperty subProperties = (node.Tag as WzSubProperty);
                subProperties.SortProperties();

                // Refresh the TreeView view to be in synchronized with the new WzSubProperty's order
                WzNode newNode = new WzNode(subProperties, true);
                nodeParent.Nodes[node.Index] = newNode;
                nodeParent.Nodes.Remove(node);

                nodeParent.TreeView.EndUpdate();
            }
        }

        /// <summary>
        /// Insert the WZ file to the main panel UI
        /// </summary>
        /// <param name="f"></param>
        /// <param name="panel"></param>
        public void InsertWzFileToPanel(WzFile f)
        {
            WzNode node = new WzNode(f);

            MainPanel.DataTree.BeginUpdate();
            MainPanel.DataTree.Nodes.Add(node);
            MainPanel.DataTree.EndUpdate();

            SortNodesRecursively(node);
        }

        /// <summary>
        /// Delayed loading of the loaded WzFile to the TreeNode panel
        /// This primarily fixes some performance issue when loading multiple WZ concurrently.
        /// </summary>
        /// <param name="wzObj"></param>
        /// <param name="panel"></param>
        /// <param name="currentDispatcher"></param>
        public async void AddLoadedWzObjectToMainPanel(WzObject wzObj, Dispatcher currentDispatcher = null)
        {
            WzNode node = new WzNode(wzObj);

            Debug.WriteLine("Adding wz object {0}, total size: {1}", wzObj.Name, MainPanel.DataTree.Nodes.Count);

            // execute in main thread
            if (currentDispatcher != null)
            {
                await currentDispatcher.BeginInvoke((Action)(() =>
                {
                    MainPanel.DataTree.BeginUpdate();

                    MainPanel.DataTree.Nodes.Add(node);
                    if (Program.ConfigurationManager.UserSettings.Sort)
                    {
                        SortNodesRecursively(node);
                    }

                    MainPanel.DataTree.EndUpdate();
                    //MainPanel.DataTree.Update();
                }));
            }
            else
            {
                MainPanel.DataTree.BeginUpdate();

                MainPanel.DataTree.Nodes.Add(node);
                if (Program.ConfigurationManager.UserSettings.Sort)
                {
                    SortNodesRecursively(node);
                }
                MainPanel.DataTree.EndUpdate();
                //MainPanel.DataTree.Update();
            }
            Debug.WriteLine("Done adding wz object {0}, total size: {1}", wzObj.Name, MainPanel.DataTree.Nodes.Count);
        }

        /// <summary>
        /// Reloaded the loaded wz file
        /// </summary>
        /// <param name="existingLoadedWzFile"></param>
        /// <param name="currentDispatcher"></param>
        public async void ReloadWzFile(WzFile existingLoadedWzFile, Dispatcher currentDispatcher = null)
        {
            // Get the current loaded wz file information
            WzMapleVersion encVersion = existingLoadedWzFile.MapleVersion;
            string path = existingLoadedWzFile.FilePath;
            
            // Unload it
            if (currentDispatcher != null)
            {
                await currentDispatcher.BeginInvoke((Action)(() =>
                {
                    UnloadWzFile(existingLoadedWzFile, currentDispatcher);
                }));
            }
            else
                UnloadWzFile(existingLoadedWzFile, currentDispatcher);

            // Load the new wz file from the same path
            WzFile newWzFile = Program.WzFileManager.LoadWzFile(path, encVersion);
            if (newWzFile != null)
            {
                AddLoadedWzObjectToMainPanel(newWzFile, currentDispatcher);  
            }
        }

        /// <summary>
        /// Unload the loaded WZ file
        /// </summary>
        /// <param name="file"></param>
        public async void UnloadWzFile(WzFile file, Dispatcher currentDispatcher = null)
        {
            WzNode node = (WzNode)file.HRTag; // get the ref first

            // unload the wz file
            Program.WzFileManager.UnloadWzFile(file, file.FilePath);

            // remove from treeview
            if (node != null) 
            {
                if (currentDispatcher != null)
                {
                    await currentDispatcher.BeginInvoke((Action)(() =>
                    {
                        node.DeleteWzNode();
                    }));
                } else
                    node.DeleteWzNode();
            }
        }
        #endregion

        #region Theme colors
        public void SetThemeColor()
        {
            if (Program.ConfigurationManager.UserSettings.ThemeColor == (int)UserSettingsThemeColor.Dark)//black
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
        public static void AddWzEncryptionTypesToComboBox(object encryptionBox) {
            string customKeyName = string.Format(Properties.Resources.EncTypeCustom, Program.ConfigurationManager.ApplicationSettings.MapleVersion_CustomEncryptionName);

            BindingList<EncryptionKey> keys = new BindingList<EncryptionKey> {
                new EncryptionKey { Name = Properties.Resources.EncTypeGMS, MapleVersion = WzMapleVersion.GMS },
                new EncryptionKey { Name = Properties.Resources.EncTypeMSEA, MapleVersion = WzMapleVersion.EMS },
                new EncryptionKey { Name = Properties.Resources.EncTypeNone, MapleVersion = WzMapleVersion.BMS },
                new EncryptionKey { Name = customKeyName, MapleVersion = WzMapleVersion.CUSTOM },
            };
        
            ComboBox comboBox; 
            if (encryptionBox is ToolStripComboBox tsBox) {
                // MainForm
                comboBox = tsBox.ComboBox;
                keys.Add(new EncryptionKey { Name = Properties.Resources.EncTypeGenerate, MapleVersion = WzMapleVersion.GENERATE }); // show bruteforce option
            }
            else {
                // SaveForm / NewForm / WZMapleVersionInputBox (import IMG)
                comboBox = encryptionBox as ComboBox;
            }

            comboBox.DisplayMember = "Name";
            comboBox.DataSource = keys;
        }

        private bool _handlingCustomEncryptionChange = false;

        /// <summary>
        /// On encryption box selection changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EncryptionBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!mainFormLoaded) // first run during app startup
            {
                return;
            }
        
            if (_handlingCustomEncryptionChange) // prevent CustomWZEncryptionInputBox from being shown multiple times
            {
                return;
            }
        
            EncryptionKey selectedEncryption = (EncryptionKey)encryptionBox.SelectedItem;
            Program.ConfigurationManager.ApplicationSettings.MapleVersion = selectedEncryption.MapleVersion;
        
            if (selectedEncryption.MapleVersion == WzMapleVersion.CUSTOM)
            {
                _handlingCustomEncryptionChange = true;
                CustomWZEncryptionInputBox customWzInputBox = new CustomWZEncryptionInputBox();
                customWzInputBox.ShowDialog();
                selectedEncryption.Name = string.Format(Properties.Resources.EncTypeCustom, Program.ConfigurationManager.ApplicationSettings.MapleVersion_CustomEncryptionName);
                _handlingCustomEncryptionChange = false;
            } 
            else if (selectedEncryption.MapleVersion == WzMapleVersion.GENERATE)
            {
                WzKeyBruteforceForm bfForm = new WzKeyBruteforceForm();
                bfForm.ShowDialog(); // find needles in a haystack
            }
            else
            {
                MapleCryptoConstants.UserKey_WzLib = MapleCryptoConstants.MAPLESTORY_USERKEY_DEFAULT.ToArray();
            }
        }

        /// <summary>
        /// Gets the WzMapleVersion enum by encryptionBox selection index
        /// </summary>
        /// <param name="selectedIndex"></param>
        /// <returns></returns>
        public static WzMapleVersion GetWzMapleVersionByWzEncryptionBoxSelection(int selectedIndex)
        {
            WzMapleVersion wzMapleVer;
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
        /// on program init.
        /// </summary>
        /// <param name="versionSelected"></param>
        private void SetWzEncryptionBoxSelectionByWzMapleVersion(WzMapleVersion versionSelected)
        {
            encryptionBox.SelectedIndex = GetIndexByWzMapleVersion(versionSelected);
            if (versionSelected == WzMapleVersion.CUSTOM)
            {
                Program.ConfigurationManager.SetCustomWzUserKeyFromConfig();
            }
        }
        #endregion

        #region Win32 API interop
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
        #endregion

        private string OnPipeRequest(string requestPath)
        {
            if (File.Exists(requestPath))
            {
                LoadWzFileCallback(requestPath);
            }
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

        #region UI Handlers
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
                    case Keys.I: // Open new Wz format
                        toolStripMenuItem_newWzFormat_Click(null, null);
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
                Child = new MainPanel(this)
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
            }
            else
            {
                MainPanel = (MainPanel)elemHost.Child;
            }

            tabPage.Text = defaultName;

            tabControl_MainPanels.TabPages.Add(tabPage);

            // Focus on that tab control
            tabControl_MainPanels.Focus();
        }
        #endregion


        #region Open WZ File
        /// <summary>
        /// Open WZ or ZLZ file internal
        /// </summary>
        /// <param name="fileNames"></param>
        private async void OpenFileInternal(string[] fileNames) {
            Dispatcher currentDispatcher = Dispatcher.CurrentDispatcher;

            WzMapleVersion MapleVersionEncryptionSelected = GetWzMapleVersionByWzEncryptionBoxSelection(encryptionBox.SelectedIndex);

            List<string> wzfilePathsToLoad = new List<string>();

            foreach (string filePath in fileNames) {
                string filePathLowerCase = filePath.ToLower();

                if (filePathLowerCase.EndsWith("zlz.dll") || filePathLowerCase.EndsWith("zlz64.dll"))
                {
                    var is64BitDll = filePathLowerCase.EndsWith("zlz64.dll");
                    var (bitness, architecture) = AssemblyBitnessDetector.GetAssemblyInfo();

                    bool isCompatible = (is64BitDll && (bitness == AssemblyBitnessDetector.Bitness.Bit64)) ||
                                        (!is64BitDll && (bitness == AssemblyBitnessDetector.Bitness.Bit32));

                    if (isCompatible)
                    {
                        var form = new ZLZPacketEncryptionKeyForm();
                        var opened = is64BitDll
                            ? form.OpenZLZDllFile_64Bit(filePath)
                            : form.OpenZLZDllFile_32Bit(filePath);

                        if (opened)
                        {
                            form.Show();
                        }
                    }
                    else
                    {
                        var errorMessage = is64BitDll
                            ? HaRepacker.Properties.Resources.ExecutingAssemblyError_64BitRequired
                            : HaRepacker.Properties.Resources.ExecutingAssemblyError;

                        MessageBox.Show(errorMessage, HaRepacker.Properties.Resources.Warning, MessageBoxButtons.OK);
                    }
                    return;

                }
                else 
                {
                    // Load WZFileManager here if its not loaded
                    if (Program.WzFileManager == null) {
                        // Pattern 1: Match paths containing "Data" directory, but capture up to "Data" (for post 64-bit wz files after V-Update)
                        string PATTERN_REGEX_DATADIR = @"^(.*?)\\Data\\.*$";
                        // Pattern 2: Match paths ending with .wz file without "Data" directory (for beta maplestory, pre-bb and post-bb MapleStory)
                        string PATTERN_REGEX_NORMAL_WZ = @"^(.*\\)[\w]+\.wz$";

                        string maplestoryBaseDirectory = string.Empty;

                        Match match = Regex.Match(filePath, PATTERN_REGEX_DATADIR);
                        if (match.Success) {
                            maplestoryBaseDirectory = match.Groups[1].Value;
                        }
                        else {
                            Match match2 = Regex.Match(filePath, PATTERN_REGEX_NORMAL_WZ);
                            if (match2.Success) {
                                maplestoryBaseDirectory = match2.Groups[1].Value.TrimEnd('\\');
                            }
                        }

                        Program.WzFileManager = new WzFileManager(maplestoryBaseDirectory);
                        Program.WzFileManager.BuildWzFileList();
                    }

                    // List.wz file (pre-bb maplestory enc)
                    if (WzTool.IsListFile(filePath)) {
                        new ListEditor(filePath, MapleVersionEncryptionSelected).Show();
                    }
                    // Other WZs
                    else if (filePathLowerCase.EndsWith("data.wz") && WzTool.IsDataWzHotfixFile(filePath)) {
                        WzImage img = Program.WzFileManager.LoadDataWzHotfixFile(filePath, MapleVersionEncryptionSelected);
                        if (img == null) {
                            MessageBox.Show(HaRepacker.Properties.Resources.MainFileOpenFail, HaRepacker.Properties.Resources.Error);
                            break;
                        }
                        AddLoadedWzObjectToMainPanel(img);

                    }
                    else {
                        if (MapleVersionEncryptionSelected == WzMapleVersion.GENERATE) {
                            WzKeyBruteforceForm bfForm = new WzKeyBruteforceForm();
                            bfForm.ShowDialog(); // find needles in a haystack
                            return;
                        }

                        wzfilePathsToLoad.Add(filePath); // add to list, so we can load it concurrently

                        // Check if there are any related files
                        string[] wzsWithRelatedFiles = { "Map", "Mob", "Skill", "Sound" };
                        bool bWithRelated = false;
                        string relatedFileName = null;

                        foreach (string wz in wzsWithRelatedFiles) {
                            if (filePathLowerCase.EndsWith(wz.ToLower() + ".wz")) {
                                bWithRelated = true;
                                relatedFileName = wz;
                                break;
                            }
                        }
                        if (bWithRelated) {
                            if (Program.ConfigurationManager.UserSettings.AutoloadRelatedWzFiles) {
                                string[] otherMapWzFiles = Directory.GetFiles(filePath.Substring(0, filePath.LastIndexOf("\\")), relatedFileName + "*.wz");
                                foreach (string filePath_Others in otherMapWzFiles) {
                                    if (filePath_Others != filePath)
                                        wzfilePathsToLoad.Add(filePath_Others);
                                }
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
                    if (f == null) {
                        // error should be thrown 
                    }
                    else {
                        lock (loadedWzFiles) {
                            loadedWzFiles.Add(f);
                        }
                    }
                });
                while (!loop.IsCompleted) {
                    Thread.Sleep(100); //?
                }

                foreach (WzFile wzFile in loadedWzFiles) // add later, once everything is loaded to memory
                {
                    AddLoadedWzObjectToMainPanel(wzFile, currentDispatcher);
                }
            }); // load complete

            // Hide panel splash sdcreen
            MainPanel.OnSetPanelLoadingCompleted();
        }
        #endregion

        #region Toolstrip Menu items
        /// <summary>
        /// Open WZ file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Load WZ file
            using (OpenFileDialog dialog = new OpenFileDialog()
            {
                Title = HaRepacker.Properties.Resources.SelectWz,
                Filter = string.Format("{0}|*.wz;ZLZ.dll;ZLZ64.dll",
                HaRepacker.Properties.Resources.WzFilter),
                Multiselect = true,
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                // Opens the selected file
                OpenFileInternal(dialog.FileNames);
            }
        }

        /// <summary>
        /// Open new WZ file (KMST) 
        /// with the split format
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void toolStripMenuItem_newWzFormat_Click(object sender, EventArgs e)
        {
            Dispatcher currentDispatcher = Dispatcher.CurrentDispatcher;

            WzMapleVersion MapleVersionEncryptionSelected = GetWzMapleVersionByWzEncryptionBoxSelection(encryptionBox.SelectedIndex);

            // Load WZ file
            using (FolderBrowserDialog fbd = new FolderBrowserDialog()
            {
                Description = "Select the WZ folder (Base, Mob, Character, etc)",
                ShowNewFolderButton = true,
            })
            {
                DialogResult result = fbd.ShowDialog();
                if (result != DialogResult.OK || string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    return;

                string[] iniFilesPath = Directory.GetFiles(fbd.SelectedPath, "*.ini", SearchOption.AllDirectories);

                // Search for all '.ini' file, and for each .ini found, proceed to parse all items in the sub directory
                // merge all parsed directory as a single WZ

                List<string> wzfilePathsToLoad = new List<string>();
                foreach (string iniFilePath in iniFilesPath)
                {
                    string directoryName = Path.GetDirectoryName(iniFilePath);
                    string[] wzFilesPath = Directory.GetFiles(directoryName, "*.wz", SearchOption.TopDirectoryOnly);

                    foreach (string wzFilePath in wzFilesPath)
                    {
                        wzfilePathsToLoad.Add(wzFilePath);
                        Debug.WriteLine(wzFilePath);
                    }
                }

                // Show splash screen
                MainPanel.OnSetPanelLoading();


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
                        AddLoadedWzObjectToMainPanel(wzFile, currentDispatcher);
                    }
                }); // load complete

                // Hide panel splash sdcreen
                MainPanel.OnSetPanelLoadingCompleted();
            }
        }

        /// <summary>
        /// Unload all wz file -- toolstrip button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void unloadAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Warning.Warn(HaRepacker.Properties.Resources.MainUnloadAll))
            {
                Dispatcher currentThread = Dispatcher.CurrentDispatcher;
                
                var wzFiles = Program.WzFileManager.WzFileList;
                /*foreach (WzFile wzFile in wzFiles)
                {
                    UnloadWzFile(wzFile);
                };*/
                Parallel.ForEach(wzFiles, wzFile =>
                {
                    UnloadWzFile(wzFile, currentThread);
                });
            }
        }

        /// <summary>
        /// Reload all wz file -- toolstrip button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void reloadAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Warning.Warn(HaRepacker.Properties.Resources.MainReloadAll))
            {
                Dispatcher currentThread = Dispatcher.CurrentDispatcher;

                var wzFiles = Program.WzFileManager.WzFileList;
                /*foreach (WzFile wzFile in wzFiles)
                {
                    ReloadLoadedWzFile(wzFile);
                };*/
                Parallel.ForEach(wzFiles, wzFile =>
                {
                    ReloadWzFile(wzFile, currentThread);
                });
            }
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

        /// <summary>
        /// Settings  -- toolstripmenu item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FHMapper.FHMapper mapper = new FHMapper.FHMapper(MainPanel);
            mapper.ParseSettings();
            Settings settingsDialog = new Settings();
            settingsDialog.settings = mapper.settings;
            settingsDialog.main = mapper;
            settingsDialog.ShowDialog();
        }

        /// <summary>
        /// About -- toolstripmenu item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new AboutForm().ShowDialog();
        }

        /// <summary>
        /// Options - toolstripmenuitem
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new OptionsForm().ShowDialog();
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
        private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
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

        private void RemoveToolStripMenuItem_Click(object sender, EventArgs e)
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

        private const string WZ_EXTRACT_ERROR_FILE = "WzExtract_Errors.txt";

        private void RunWzFilesExtraction(object param)
        {
            ChangeApplicationState(false);

            string[] wzFilesToDump = (string[])((object[])param)[0];
            string baseDir = (string)((object[])param)[1];
            WzMapleVersion version = GetWzMapleVersionByWzEncryptionBoxSelection((int)(((object[])param)[2]));
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
            MapleLib.Helpers.ErrorLogger.SaveToFile(WZ_EXTRACT_ERROR_FILE);

            // Reset progress bar to 0
            UpdateProgressBar(MainPanel.mainProgressBar, 0, false, true);
            UpdateProgressBar(MainPanel.mainProgressBar, 0, true, true);

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
            MapleLib.Helpers.ErrorLogger.SaveToFile(WZ_EXTRACT_ERROR_FILE);

            // Reset progress bar to 0
            UpdateProgressBar(MainPanel.mainProgressBar, 0, false, true);
            UpdateProgressBar(MainPanel.mainProgressBar, 0, true, true);

            threadDone = true;
        }

        private void RunWzObjExtraction(object param)
        {
            ChangeApplicationState(false);

#if DEBUG
            var watch = new Stopwatch();
            watch.Start();
#endif
            List<WzObject> objsToDump = (List<WzObject>)((object[])param)[0];
            string path = (string)((object[])param)[1];
            ProgressingWzSerializer serializers = (ProgressingWzSerializer)((object[])param)[2];

            UpdateProgressBar(MainPanel.mainProgressBar, 0, false, true);

            if (serializers is IWzObjectSerializer serializer)
            {
                UpdateProgressBar(MainPanel.mainProgressBar, objsToDump.Count, true, true);
                foreach (WzObject obj in objsToDump)
                {
                    serializer.SerializeObject(obj, path);
                    UpdateProgressBar(MainPanel.mainProgressBar, 1, false, false);
                }
            }
            else if (serializers is WzNewXmlSerializer serializer_)
            {
                UpdateProgressBar(MainPanel.mainProgressBar, 1, true, true);
                serializer_.ExportCombinedXml(objsToDump, path);
                UpdateProgressBar(MainPanel.mainProgressBar, 1, false, false);

            }
            MapleLib.Helpers.ErrorLogger.SaveToFile(WZ_EXTRACT_ERROR_FILE);
#if DEBUG
            // test benchmark
            watch.Stop();
            Debug.WriteLine($"WZ files Extracted. Execution Time: {watch.ElapsedMilliseconds} ms");
#endif

            // Reset progress bar to 0
            UpdateProgressBar(MainPanel.mainProgressBar, 0, false, true);
            UpdateProgressBar(MainPanel.mainProgressBar, 0, true, true);

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
            button_addTab.Enabled = enabled;
            tabControl_MainPanels.Enabled = enabled;
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

        /// <summary>
        /// Updates the progress bar
        /// </summary>
        /// <param name="pbar"></param>
        /// <param name="value"></param>
        /// <param name="setMaxValue"></param>
        /// <param name="absolute"></param>
        private void UpdateProgressBar(System.Windows.Controls.ProgressBar pbar, int value, bool setMaxValue, bool absolute)
        {
            pbar.Dispatcher.Invoke(() =>
            {
                if (setMaxValue)
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
            });
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
            UpdateProgressBar(MainPanel.mainProgressBar, 1, true, true);
            UpdateProgressBar(MainPanel.mainProgressBar, 0, false, true);

            UpdateProgressBar(MainPanel.secondaryProgressBar, 1, true, true);
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

        /// <summary>
        /// Export IMG
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// Export PNG / MP3
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
        /// Export as Json
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void jSONToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportBsonJsonInternal(true);
        }

        /// <summary>
        /// Export as BSON
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bSONToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportBsonJsonInternal(false);
        }

        /// <summary>
        /// Export as Json or Bson
        /// </summary>
        /// <param name="isJson"></param>
        private void ExportBsonJsonInternal(bool isJson)
        {
            string outPath = GetOutputDirectory();
            if (outPath == string.Empty)
            {
                MessageBox.Show(Properties.Resources.MainWzExportError, Properties.Resources.Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult dlgResult = MessageBox.Show(Properties.Resources.MainWzExportJson_IncludeBase64, Properties.Resources.MainWzExportJson_IncludeBase64_Title, MessageBoxButtons.YesNoCancel);
            if (dlgResult == DialogResult.Cancel)
                return;
            bool bIncludeBase64BinData = dlgResult == DialogResult.Yes;

            List<WzDirectory> dirs = new List<WzDirectory>();
            List<WzImage> imgs = new List<WzImage>();
            foreach (WzNode node in MainPanel.DataTree.SelectedNodes)
            {
                if (node.Tag is WzDirectory directory)
                    dirs.Add(directory);
                else if (node.Tag is WzImage image)
                    imgs.Add(image);
                else if (node.Tag is WzFile file)
                {
                    dirs.Add(file.WzDirectory);
                }
            }
            WzJsonBsonSerializer serializer = new WzJsonBsonSerializer(Program.ConfigurationManager.UserSettings.Indentation, Program.ConfigurationManager.UserSettings.LineBreakType, bIncludeBase64BinData, isJson);
            threadDone = false;

            runningThread = new Thread(new ParameterizedThreadStart(RunWzImgDirsExtraction));
            runningThread.Start((object)new object[] { dirs, imgs, outPath, serializer });

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
                if (node.Tag is WzDirectory directory)
                    dirs.Add(directory);
                else if (node.Tag is WzImage image)
                    imgs.Add(image);
                else if (node.Tag is WzFile file)
                {
                    dirs.Add(file.WzDirectory);
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

        /// <summary>
        /// Export as XML,  classic
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// Export as XML, new
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// Add Lua script property
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wzLuaPropertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainPanel.AddWzLuaPropertyToSelectedIndex(MainPanel.DataTree.SelectedNode);
        }
        #endregion


        private delegate void InsertWzNode(WzNode node, WzNode parent);
        private void InsertWzNodeCallback(WzNode node, WzNode parent)
        {
            WzNode child = WzNode.GetChildNode(parent, node.Text);
            if (child != null)
            {
                if (ShowReplaceDialog(node.Text))
                    child.DeleteWzNode();
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
            MapleLib.Helpers.ErrorLogger.SaveToFile("WzImport_Errors.txt");

            threadDone = true;
        }

        private void nXForamtToolStripMenuItem_Click(object sender, EventArgs e)
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

            WzToNxSerializer serializer = new WzToNxSerializer();
            threadDone = false;

            runningThread = new Thread(new ParameterizedThreadStart(RunWzFilesExtraction));
            runningThread.Start((object)new object[] { dialog.FileNames, outPath, encryptionBox.SelectedIndex, serializer });
            new Thread(new ParameterizedThreadStart(ProgressBarThread)).Start(serializer);
        }
    }
}
