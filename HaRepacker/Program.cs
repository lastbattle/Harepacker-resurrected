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
using MapleLib.Configuration;
using HaSharedLibrary;
using System.Runtime.CompilerServices;
using MapleLib;

namespace HaRepacker
{
    public static class Program
    {
        private static WzFileManager _wzFileManager;
        public static WzFileManager WzFileManager
        {
            get { return _wzFileManager; }
            set { _wzFileManager = value; }
        }

        public static NamedPipeServerStream pipe;
        public static Thread pipeThread;

        private static ConfigurationManager _ConfigurationManager; // default for VS UI designer
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
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

            // App
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

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
            ThreadPool.SetMaxThreads(Environment.ProcessorCount * 3, Environment.ProcessorCount * 3); // This includes hyper-threading(Intel)/SMT (AMD) count.

            // Parameters
            bool firstRun = PrepareApplication(true);
            string wzToLoad = null;
            if (args.Length > 0)
                wzToLoad = args[0];
            Application.Run(new MainForm(wzToLoad, true, firstRun));
            EndApplication(true, true);
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
            _ConfigurationManager = new ConfigurationManager();

            bool loaded = _ConfigurationManager.Load();
            if (!loaded)
            {
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
                if (WzFileManager != null)
                    WzFileManager.Dispose();
            }
            _ConfigurationManager.Save();
        }
    }
}
