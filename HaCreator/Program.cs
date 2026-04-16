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
using MapleLib.Img;
using MapleLib.WzLib.WzProperties;

namespace HaCreator
{
    static class Program
    {
        public static WzFileManager WzManager;
        public static WzInformationManager InfoManager;
        public static IDataSource DataSource;
        public static StartupManager StartupManager;
        public static bool AbortThreads = false;
        public static bool Restarting;

        public const string APP_NAME = "HaCreator";

        public static HaEditor HaEditorWindow = null;

        #region Data Access Helpers
        /// <summary>
        /// Gets whether the data source is pre-Big Bang format
        /// </summary>
        public static bool IsPreBBDataWzFormat
        {
            get
            {
                // Check DataSource version info first
                if (DataSource != null)
                {
                    return DataSource.VersionInfo?.IsPreBB ?? false;
                }
                // Fall back to WzManager
                if (WzManager != null)
                {
                    return WzManager.IsPreBBDataWzFormat;
                }
                return false;
            }
        }

        /// <summary>
        /// Finds a WzImage from either IDataSource or WzManager
        /// </summary>
        /// <param name="category">Category name (e.g., "Mob", "Npc", "String")</param>
        /// <param name="imageName">Image name (e.g., "0100100.img")</param>
        /// <returns>The WzImage or null if not found</returns>
        public static WzImage FindImage(string category, string imageName)
        {
            WzImage image = null;

            // Try IDataSource first
            if (DataSource != null)
            {
                image = DataSource.GetImage(category, imageName);
            }
            // Fall back to WzManager
            if (image == null && WzManager != null)
            {
                image = (WzImage)WzManager.FindWzImageByName(category.ToLower(), imageName);
            }

            return image;
        }

        /// <summary>
        /// Finds a WzObject (image or directory) from either IDataSource or WzManager
        /// </summary>
        public static WzObject FindWzObject(string category, string name)
        {
            WzObject obj = null;

            // Try IDataSource first
            if (DataSource != null)
            {
                if (!string.IsNullOrEmpty(name))
                {
                    // Try as image first
                    obj = DataSource.GetImage(category, name);
                }

                if (obj == null)
                {
                    // Try as directory - get category root then navigate to subdirectory
                    var categoryDir = DataSource.GetDirectory(category);
                    if (categoryDir != null && !string.IsNullOrEmpty(name))
                    {
                        // Navigate to the subdirectory by name
                        obj = categoryDir[name];
                    }
                    else
                    {
                        obj = categoryDir;
                    }
                }
            }
            // Fall back to WzManager
            if (obj == null && WzManager != null)
            {
                obj = WzManager.FindWzImageByName(category.ToLower(), name);
            }

            return obj;
        }

        /// <summary>
        /// Marks an image as updated (triggers save for IMG filesystem, marks for later save for WZ files)
        /// </summary>
        /// <param name="category">Category name (e.g., "String", "Map")</param>
        /// <param name="image">The image that was modified</param>
        public static void MarkImageUpdated(string category, WzImage image)
        {
            if (image == null) return;

            // Try IDataSource first
            if (DataSource != null)
            {
                DataSource.MarkImageUpdated(category, image);
                return;
            }
            // Fall back to WzManager
            if (WzManager != null)
            {
                WzManager.SetWzFileUpdated(category.ToLower(), image);
            }
        }

        /// <summary>
        /// Gets directories from either IDataSource or WzManager
        /// </summary>
        /// <param name="baseCategory">Base category name (e.g., "string", "map")</param>
        /// <returns>List of WzDirectories</returns>
        public static List<WzDirectory> GetDirectories(string baseCategory)
        {
            var directories = new List<WzDirectory>();

            // Try IDataSource first
            if (DataSource != null)
            {
                directories.AddRange(DataSource.GetDirectories(baseCategory));
            }
            // Fall back to WzManager
            if (directories.Count == 0 && WzManager != null)
            {
                directories.AddRange(WzManager.GetWzDirectoriesFromBase(baseCategory.ToLower()));
            }

            return directories;
        }
        #endregion

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
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

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
            WzCanvasProperty.ExternalImageResolver = ResolveImageByFullPath;

            // Initialize StartupManager for IMG filesystem support
            StartupManager = new StartupManager();
            StartupManager.ScanVersions();

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
            StartupManager?.SaveConfig();
            if (Restarting)
            {
                Application.Restart();
            }
            DataSource?.Dispose();
            if (WzManager != null)  // doesnt initialise on load until WZ files are loaded via Initialization.xaml.cs
            {
                WzManager.Dispose();
            }
        }

        private static WzImage ResolveImageByFullPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            if (DataSource != null)
            {
                foreach (string candidatePath in GetCandidateCategoryRootedImagePaths(relativePath))
                {
                    WzImage candidateImage = DataSource.GetImageByPath(candidatePath);
                    if (candidateImage != null)
                    {
                        return candidateImage;
                    }
                }

                return null;
            }

            string normalizedPath = NormalizeCategoryRootedImagePath(relativePath.Replace('\\', '/').Trim('/'));
            int firstSeparator = normalizedPath.IndexOf('/');
            if (firstSeparator <= 0)
            {
                return null;
            }

            string category = normalizedPath.Substring(0, firstSeparator);
            string imagePath = normalizedPath.Substring(firstSeparator + 1);
            return FindWzObjectByPath(category, imagePath) as WzImage;
        }

        private static WzObject FindWzObjectByPath(string category, string relativePath)
        {
            if (WzManager == null || string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            string normalizedPath = NormalizeCategoryRelativePath(category, relativePath.Replace('\\', '/').Trim('/'));
            string[] segments = normalizedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return null;
            }

            IEnumerable<WzDirectory> roots = WzManager.GetWzDirectoriesFromBase(category.ToLower());
            foreach (WzDirectory root in roots)
            {
                WzObject current = root;
                bool resolved = true;
                foreach (string segment in segments)
                {
                    current = current?[segment];
                    if (current == null)
                    {
                        resolved = false;
                        break;
                    }
                }

                if (resolved)
                {
                    return current;
                }
            }

            return null;
        }

        private static IEnumerable<string> GetCandidateCategoryRootedImagePaths(string relativePath)
        {
            string originalPath = relativePath.Replace('\\', '/').Trim('/');
            if (string.IsNullOrWhiteSpace(originalPath))
            {
                yield break;
            }

            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (yielded.Add(originalPath))
            {
                yield return originalPath;
            }

            string normalizedPath = NormalizeCategoryRootedImagePath(originalPath);
            if (yielded.Add(normalizedPath))
            {
                yield return normalizedPath;
            }

            string withoutCanvasDirectory = originalPath.Replace("/_Canvas/", "/", StringComparison.OrdinalIgnoreCase);
            if (!string.Equals(withoutCanvasDirectory, originalPath, StringComparison.OrdinalIgnoreCase))
            {
                if (yielded.Add(withoutCanvasDirectory))
                {
                    yield return withoutCanvasDirectory;
                }

                string normalizedWithoutCanvas = NormalizeCategoryRootedImagePath(withoutCanvasDirectory);
                if (yielded.Add(normalizedWithoutCanvas))
                {
                    yield return normalizedWithoutCanvas;
                }
            }

            int separatorIndex = originalPath.IndexOf('/');
            if (separatorIndex > 0)
            {
                string category = originalPath.Substring(0, separatorIndex);
                string remainder = originalPath.Substring(separatorIndex + 1);
                if (!string.IsNullOrWhiteSpace(remainder))
                {
                    string categoryRelative = NormalizeCategoryRelativePath(category, remainder);
                    string categoryRelativeCandidate = $"{category}/{categoryRelative}";
                    if (yielded.Add(categoryRelativeCandidate))
                    {
                        yield return categoryRelativeCandidate;
                    }

                    string doubledCategoryCandidate = $"{category}/{category}/{categoryRelative}";
                    if (yielded.Add(doubledCategoryCandidate))
                    {
                        yield return doubledCategoryCandidate;
                    }

                    string categoryRelativeWithoutCanvas = categoryRelative.Replace("/_Canvas/", "/", StringComparison.OrdinalIgnoreCase);
                    if (!string.Equals(categoryRelativeWithoutCanvas, categoryRelative, StringComparison.OrdinalIgnoreCase))
                    {
                        string categoryRelativeWithoutCanvasCandidate = $"{category}/{categoryRelativeWithoutCanvas}";
                        if (yielded.Add(categoryRelativeWithoutCanvasCandidate))
                        {
                            yield return categoryRelativeWithoutCanvasCandidate;
                        }

                        string doubledCategoryWithoutCanvasCandidate = $"{category}/{category}/{categoryRelativeWithoutCanvas}";
                        if (yielded.Add(doubledCategoryWithoutCanvasCandidate))
                        {
                            yield return doubledCategoryWithoutCanvasCandidate;
                        }
                    }
                }
            }
        }

        private static string NormalizeCategoryRootedImagePath(string relativePath)
        {
            string normalizedPath = relativePath.Replace('\\', '/').Trim('/');
            int separatorIndex = normalizedPath.IndexOf('/');
            if (separatorIndex <= 0)
            {
                return normalizedPath;
            }

            string category = normalizedPath.Substring(0, separatorIndex);
            string remainder = normalizedPath.Substring(separatorIndex + 1);
            return string.IsNullOrEmpty(remainder)
                ? category
                : category + "/" + NormalizeCategoryRelativePath(category, remainder);
        }

        private static string NormalizeCategoryRelativePath(string category, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return string.Empty;
            }

            string normalizedPath = relativePath.Replace('\\', '/').Trim('/');
            if (normalizedPath.StartsWith(category + "/", StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath = normalizedPath.Substring(category.Length + 1);
            }

            return normalizedPath;
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
