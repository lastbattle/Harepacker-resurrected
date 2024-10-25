/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using HaCreator.MapEditor;
using System.Runtime.InteropServices;
using MapleLib.WzLib;
using HaCreator.GUI;
using System.IO;
using System.Globalization;
using System.Threading;
using System.Resources;
using System.Reflection;
using HaCreator.Wz;
using HaSharedLibrary;
using MapleLib;

namespace HaCreator
{
    static class Program
    {
        public static WzFileManager WzManager;
        public static WzInformationManager InfoManager;
        public static bool AbortThreads = false;
        public static bool Restarting;

        public const string APP_NAME = "HaCreator";

        public static HaEditor HaEditorWindow = null;

        #region Settings
        public static WzSettingsManager SettingsManager;
        public static string GetLocalSettingsFolder()
        {
            string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string our_folder = Path.Combine(appdata, APP_NAME);
            if (!Directory.Exists(our_folder))
                Directory.CreateDirectory(our_folder);
            return our_folder;
        }

        public static string GetLocalSettingsPath()
        {
            return Path.Combine(GetLocalSettingsFolder(), "Settings.json");
        }
        #endregion

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Startup
#if !DEBUG
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
#endif

            // Localisation
            CultureInfo ci = GetMainCulture(CultureInfo.CurrentCulture);
            Properties.Resources.Culture = ci;

            Thread.CurrentThread.CurrentCulture = ci;
            Thread.CurrentThread.CurrentUICulture = ci;

            CultureInfo.CurrentCulture = ci;
            CultureInfo.CurrentUICulture = ci;
            CultureInfo.DefaultThreadCurrentCulture = ci;
            CultureInfo.DefaultThreadCurrentUICulture = ci;


            Properties.Resources.Culture = CultureInfo.CurrentCulture;
            InfoManager = new WzInformationManager();
            SettingsManager = new WzSettingsManager(GetLocalSettingsPath(), typeof(UserSettings), typeof(ApplicationSettings));
            SettingsManager.LoadSettings();
           
            MultiBoard.RecalculateSettings();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            // Program run here
            GUI.Initialization initForm = new GUI.Initialization();
            Application.Run(initForm);

            // Shutdown
            if (initForm.editor != null)
                initForm.editor.hcsm.backupMan.ClearBackups();
            SettingsManager.SaveSettings();
            if (Restarting)
            {
                Application.Restart();
            }
            if (WzManager != null)  // doesnt initialise on load until WZ files are loaded via Initialization.xaml.cs
            {
                WzManager.Dispose();
            }
        }

        /// <summary>
        /// Allows customisation of display text during runtime..
        /// </summary>
        /// <param name="ci"></param>
        /// <returns></returns>
        private static CultureInfo GetMainCulture(CultureInfo ci)
        {
            var hyphen = ci.Name.IndexOf('-');
            if (hyphen < 0)
                return ci;
            return ci.Name.AsSpan(0, hyphen) switch
            {
                "ko" => new CultureInfo("ko"),
                "ja" => new CultureInfo("ja"),
                "en" => new CultureInfo("en"),
                "zh" => ci.ThreeLetterWindowsLanguageName == "CHS"
                    ? new CultureInfo("zh-CHS")
                    : new CultureInfo("zh-CHT"),
                _ => ci
            };
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            new ThreadExceptionDialog((Exception)e.ExceptionObject).ShowDialog();
            Environment.Exit(-1);
        }
    }
}

