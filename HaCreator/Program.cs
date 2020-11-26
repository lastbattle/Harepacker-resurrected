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

namespace HaCreator
{
    static class Program
    {
        public static WzFileManager WzManager;
        public static WzInformationManager InfoManager;
        public static WzSettingsManager SettingsManager;
        public static bool AbortThreads = false;
        public static bool Restarting;

        public const string APP_NAME = "HaCreator";

        public static HaEditor HaEditorWindow = null;

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
            return Path.Combine(GetLocalSettingsFolder(), "Settings.wz");
        }

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
            SettingsManager = new WzSettingsManager(GetLocalSettingsPath(), typeof(UserSettings), typeof(ApplicationSettings), typeof(Microsoft.Xna.Framework.Color));
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
            SettingsManager.Save();
            if (Restarting)
            {
                Application.Restart();
            }
        }

        /// <summary>
        /// Allows customisation of display text during runtime..
        /// </summary>
        /// <param name="ci"></param>
        /// <returns></returns>
        private static CultureInfo GetMainCulture(CultureInfo ci)
        {
            if (!ci.Name.Contains("-"))
                return ci;
            switch (ci.Name.Split("-".ToCharArray())[0])
            {
                case "ko":
                    return new CultureInfo("ko");
                case "ja":
                    return new CultureInfo("ja");
                case "en":
                    return new CultureInfo("en");
                case "zh":
                    if (ci.EnglishName.Contains("Simplified"))
                        return new CultureInfo("zh-CHS");
                    else
                        return new CultureInfo("zh-CHT");
                default:
                    return ci;
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            new ThreadExceptionDialog((Exception)e.ExceptionObject).ShowDialog();
            Environment.Exit(-1);
        }
    }
}

