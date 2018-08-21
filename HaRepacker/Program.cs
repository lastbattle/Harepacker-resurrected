/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows.Forms;
using HaRepacker.GUI;
using Microsoft.Win32;
using System.Threading;
using MapleLib.WzLib;
using System.IO.Pipes;
using System.IO;
using System.Security.Principal;
using System.Globalization;
using HaRepacker.Configuration;

namespace HaRepacker
{
    public static class Program
    {
        public const string Version = "4.2.4";
        public const int Version_ = 424;

        public const int TimeStartAnimateDefault = 60;

        public static WzFileManager WzMan = new WzFileManager();
        public static NamedPipeServerStream pipe;
        public static Thread pipeThread;

        private static ConfigurationManager _ConfigurationManager = new ConfigurationManager(string.Empty); // default for VS UI designer
        public static ConfigurationManager ConfigurationManager
        {
            get { return _ConfigurationManager; }
            private set { }
        }

        public const string pipeName = "HaRepacker";

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Localisation
            CultureInfo ci = GetMainCulture(CultureInfo.CurrentCulture);
            Properties.Resources.Culture = ci;

            Thread.CurrentThread.CurrentCulture = ci;
            Thread.CurrentThread.CurrentUICulture = ci;

            CultureInfo.CurrentCulture = ci;
            CultureInfo.CurrentUICulture = ci;
            CultureInfo.DefaultThreadCurrentCulture = ci;
            CultureInfo.DefaultThreadCurrentUICulture = ci;

            // Threads
            ThreadPool.SetMaxThreads(Environment.ProcessorCount, Environment.ProcessorCount); // This includes hyper-threading(Intel)/SMT (AMD) count.

            // App
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            // Parameters
            bool firstRun = PrepareApplication(true);
            string wzToLoad = null;
            if (args.Length > 0)
                wzToLoad = args[0];
            Application.Run(new MainForm(wzToLoad, true, firstRun));
            EndApplication(true, true);
        }

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

        /// <summary>
        /// Gets the local folder path
        /// </summary>
        /// <returns></returns>
        public static string GetLocalFolderPath()
        {
            string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string our_folder = Path.Combine(appdata, pipeName);
            if (!Directory.Exists(our_folder))
                Directory.CreateDirectory(our_folder);
            return our_folder;
        }

        public static bool IsUserAdministrator()
        {
            //bool value to hold our return value
            bool isAdmin;
            try
            {
                //get the currently logged in user
                WindowsIdentity user = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(user);
                isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception)
            {
                isAdmin = false;
            }
            return isAdmin;
        }

        public static bool PrepareApplication(bool from_internal)
        {
            _ConfigurationManager = new ConfigurationManager(GetLocalFolderPath());

            bool loaded = _ConfigurationManager.Load();
            if (!loaded)
            {
                Warning.Error(HaRepacker.Properties.Resources.ProgramLoadSettingsError);
                return true;
            }
            bool firstRun = Program.ConfigurationManager.ApplicationSettings.FirstRun;
            if (Program.ConfigurationManager.ApplicationSettings.FirstRun)
            {
                //new FirstRunForm().ShowDialog();
                Program.ConfigurationManager.ApplicationSettings.FirstRun = false;
                _ConfigurationManager.Save();
            }
            if (Program.ConfigurationManager.UserSettings.AutoAssociate && from_internal && IsUserAdministrator())
            {
                string path = Application.ExecutablePath;
                Registry.ClassesRoot.CreateSubKey(".wz").SetValue("", "WzFile");
                RegistryKey wzKey = Registry.ClassesRoot.CreateSubKey("WzFile");
                wzKey.SetValue("", "Wz File");
                wzKey.CreateSubKey("DefaultIcon").SetValue("", path + ",1");
                wzKey.CreateSubKey("shell\\open\\command").SetValue("", "\"" + path + "\" \"%1\"");
            }
            return firstRun;
        }

        public static void EndApplication(bool usingPipes, bool disposeFiles)
        {
            if (pipe != null && usingPipes)
            {
                pipe.Close();
            }
            if (disposeFiles)
            {
                WzMan.Terminate();
            }
            _ConfigurationManager.Save();
        }
    }
}
