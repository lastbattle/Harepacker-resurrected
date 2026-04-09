using System;
using System.Globalization;
using System.IO;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class PacketOwnedAntiMacroScreenshotPathResolver
    {
        internal const int FolderModeClientDirectory = 0;
        internal const int FolderModeDesktop = 1;
        internal const int FolderModeRootDrive = 2;
        internal const string EnvironmentOverrideName = "MAPSIM_CLIENT_SCREENSHOT_MODE";

        internal static int ResolveFolderMode()
        {
            string configuredValue = Environment.GetEnvironmentVariable(EnvironmentOverrideName);
            if (int.TryParse(configuredValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int environmentMode))
            {
                return environmentMode;
            }

            return UserSettings.AntiMacroScreenshotSaveLocation;
        }

        internal static string ResolveBaseFolder()
        {
            return ResolveBaseFolder(
                ResolveFolderMode(),
                Environment.ProcessPath,
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        }

        internal static string ResolveBaseFolder(int mode, string processPath, string desktopPath)
        {
            switch (mode)
            {
                case FolderModeClientDirectory:
                {
                    if (!string.IsNullOrWhiteSpace(processPath))
                    {
                        string executableDirectory = Path.GetDirectoryName(processPath);
                        if (!string.IsNullOrWhiteSpace(executableDirectory))
                        {
                            return TrimTrailingDirectorySeparator(Directory.GetParent(executableDirectory)?.FullName);
                        }
                    }

                    return string.Empty;
                }

                case FolderModeDesktop:
                    return TrimTrailingDirectorySeparator(desktopPath);

                case FolderModeRootDrive:
                    return @"C:\";

                default:
                    return string.Empty;
            }
        }

        internal static string BuildFilePath(string baseFolder, string userName, DateTime localTime)
        {
            string safeUserName = SanitizeUserName(string.IsNullOrWhiteSpace(userName) ? "AntiMacro" : userName.Trim());
            return Path.Combine(
                baseFolder ?? string.Empty,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}_{1:yyyyMMdd_HHmmss}.jpg",
                    safeUserName,
                    localTime));
        }

        internal static string SanitizeUserName(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return "AntiMacro";
            }

            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            char[] sanitized = userName.ToCharArray();
            for (int i = 0; i < sanitized.Length; i++)
            {
                if (Array.IndexOf(invalidCharacters, sanitized[i]) >= 0)
                {
                    sanitized[i] = '_';
                }
            }

            return sanitized.Length == 0 ? "AntiMacro" : new string(sanitized);
        }

        internal static string TrimTrailingDirectorySeparator(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string root = Path.GetPathRoot(path);
            string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.IsNullOrEmpty(trimmed) && !string.IsNullOrEmpty(root)
                ? root
                : trimmed;
        }
    }
}
