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
            string resolvedUserName = userName ?? string.Empty;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}\\{1}_{2:yyyyMMdd_HHmmss}.jpg",
                baseFolder ?? string.Empty,
                resolvedUserName,
                localTime);
        }

        internal static string BuildFallbackSafeFilePath(string baseFolder, string userName, DateTime localTime)
        {
            string safeUserName = SanitizeUserName(userName);
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}\\{1}_{2:yyyyMMdd_HHmmss}.jpg",
                baseFolder ?? string.Empty,
                safeUserName,
                localTime);
        }

        internal static string SanitizeUserName(string userName)
        {
            if (userName == null)
            {
                return string.Empty;
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

            return new string(sanitized);
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
