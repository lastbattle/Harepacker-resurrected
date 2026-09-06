using System;
using System.Collections.Generic;
using System.IO;

namespace HaSharedLibrary.Configuration;

/// <summary>
/// Defines the canonical per-user storage layout shared by HaCreator and HaRepacker.
/// </summary>
public static class UserDataPaths
{
    public const string SuiteName = "Harepacker";
    public const string HaCreator = "HaCreator";
    public const string HaRepacker = "HaRepacker";

    public static string HaCreatorDirectory => GetRoamingDirectory(HaCreator);
    public static string HaRepackerDirectory
    {
        get
        {
            foreach (string fileName in new[] { "Settings.txt", "ApplicationSettings.txt", "CustomKeys.txt" })
            {
                GetRoamingFile(
                    HaRepacker,
                    fileName,
                    GetLegacyRoamingPath(HaRepacker, fileName));
            }

            return GetRoamingDirectory(HaRepacker);
        }
    }

    public static string HaCreatorSettingsFile => GetRoamingFile(
        HaCreator,
        "Settings.json",
        GetLegacyRoamingPath(HaCreator, "Settings.json"));

    public static string HaCreatorConfigFile => GetRoamingFile(
        HaCreator,
        "config.json",
        GetLegacyRoamingPath(HaCreator, "config.json"));

    public static string HaCreatorAiSettingsFile => GetRoamingFile(
        HaCreator,
        Path.Combine("AI", "Settings.json"),
        GetLegacyRoamingPath(HaCreator, "Settings_AI.json"));

    public static string HaCreatorCharactersDirectory => GetMigratedRoamingDirectory(
        HaCreator,
        Path.Combine("MapSimulator", "Characters"),
        GetLegacyRoamingPath(HaCreator, "Characters"));

    public static string HaCreatorBackupsDirectory => GetMigratedRoamingDirectory(
        HaCreator,
        "Backups",
        GetLegacyRoamingPath(HaCreator, "Backups"));

    public static string HaCreatorMapHistoryDatabase => GetRoamingFile(
        HaCreator,
        Path.Combine("Databases", "map-history.db"));

    public static string HaRepackerFhMapperSettingsFile => GetRoamingFile(
        HaRepacker,
        Path.Combine("FHMapper", "Settings.ini"),
        GetLegacyRoamingPath(HaRepacker, "Settings.ini"));

    public static string AceStepInstallDirectory => GetLocalDirectory("AudioAI", "ACE-Step-1.5");

    public static string GetHaCreatorSimulatorFile(string fileName) => GetRoamingFile(
        HaCreator,
        Path.Combine("MapSimulator", fileName),
        GetLegacyRoamingPath(HaCreator, "MapSimulator", fileName));

    public static string GetRoamingMigrationMarker(string application, string migrationName)
    {
        ValidateApplication(application);
        if (string.IsNullOrWhiteSpace(migrationName))
            throw new ArgumentException("A migration name is required.", nameof(migrationName));

        string markerDirectory = Path.Combine(RoamingRoot, application, ".migrations");
        Directory.CreateDirectory(markerDirectory);
        return Path.Combine(markerDirectory, migrationName + ".complete");
    }

    public static string RoamingRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        SuiteName);

    public static string LocalRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        SuiteName);

    public static string GetRoamingDirectory(string application, params string[] relativeSegments) =>
        GetDirectory(RoamingRoot, application, relativeSegments);

    public static string GetLocalDirectory(string application, params string[] relativeSegments) =>
        GetDirectory(LocalRoot, application, relativeSegments);

    public static string GetRoamingFile(
        string application,
        string relativePath,
        params string[] legacyPaths) =>
        GetFile(RoamingRoot, application, relativePath, legacyPaths);

    public static string GetLocalFile(
        string application,
        string relativePath,
        params string[] legacyPaths) =>
        GetFile(LocalRoot, application, relativePath, legacyPaths);

    public static string GetLegacyRoamingPath(string legacyApplication, params string[] relativeSegments) =>
        Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), legacyApplication, relativeSegments);

    public static string GetLegacyLocalPath(string legacyApplication, params string[] relativeSegments) =>
        Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), legacyApplication, relativeSegments);

    /// <summary>
    /// Copies a legacy directory into the canonical layout when it has not been migrated yet.
    /// Existing canonical files always win.
    /// </summary>
    public static string GetMigratedRoamingDirectory(
        string application,
        string relativePath,
        params string[] legacyDirectories)
    {
        string destination = GetRoamingDirectory(application, relativePath);
        string markerPath = GetMigrationMarkerPath(
            RoamingRoot,
            application,
            "directory-" + NormalizeMigrationName(relativePath));
        if (File.Exists(markerPath))
            return destination;

        bool migrationSucceeded = true;
        foreach (string legacyDirectory in legacyDirectories)
        {
            if (string.IsNullOrWhiteSpace(legacyDirectory) || !Directory.Exists(legacyDirectory))
                continue;

            migrationSucceeded &= CopyDirectoryMissingFiles(legacyDirectory, destination);
        }

        if (migrationSucceeded)
            WriteMigrationMarker(markerPath);

        return destination;
    }

    private static string GetDirectory(string root, string application, params string[] relativeSegments)
    {
        ValidateApplication(application);
        string directory = GetContainedPath(root, application, relativeSegments);
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string GetFile(
        string root,
        string application,
        string relativePath,
        IReadOnlyList<string> legacyPaths)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("A relative file path is required.", nameof(relativePath));

        ValidateApplication(application);
        string destination = GetContainedPath(root, application, new[] { relativePath });
        string directory = Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException("The user-data file has no parent directory.");
        Directory.CreateDirectory(directory);

        string markerPath = GetMigrationMarkerPath(
            root,
            application,
            "file-" + NormalizeMigrationName(relativePath));
        if (!File.Exists(destination) && !File.Exists(markerPath))
        {
            bool migrationSucceeded = true;
            foreach (string legacyPath in legacyPaths)
            {
                if (string.IsNullOrWhiteSpace(legacyPath) || !File.Exists(legacyPath))
                    continue;

                try
                {
                    File.Copy(legacyPath, destination, overwrite: false);
                    break;
                }
                catch (IOException) when (File.Exists(destination))
                {
                    break;
                }
                catch (UnauthorizedAccessException)
                {
                    // A read-only legacy install directory must not prevent startup.
                    migrationSucceeded = false;
                }
                catch (IOException)
                {
                    // Preserve application defaults if a legacy file cannot be copied.
                    migrationSucceeded = false;
                }
            }

            if (migrationSucceeded)
                WriteMigrationMarker(markerPath);
        }

        return destination;
    }

    private static string Combine(string root, string application, IReadOnlyList<string> relativeSegments)
    {
        string result = Path.Combine(root, application);
        foreach (string segment in relativeSegments)
        {
            if (!string.IsNullOrWhiteSpace(segment))
                result = Path.Combine(result, segment);
        }

        return result;
    }

    private static string GetContainedPath(
        string root,
        string application,
        IReadOnlyList<string> relativeSegments)
    {
        string applicationRoot = Path.GetFullPath(Path.Combine(root, application));
        string candidate = Path.GetFullPath(Combine(root, application, relativeSegments));
        string containedPrefix = applicationRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!string.Equals(candidate, applicationRoot, StringComparison.OrdinalIgnoreCase) &&
            !candidate.StartsWith(containedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A user-data path cannot leave its application directory.");
        }

        return candidate;
    }

    private static bool CopyDirectoryMissingFiles(string source, string destination)
    {
        bool succeeded = true;
        Directory.CreateDirectory(destination);
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };
        foreach (string sourceFile in Directory.EnumerateFiles(source, "*", enumerationOptions))
        {
            string relativePath = Path.GetRelativePath(source, sourceFile);
            string destinationFile = Path.Combine(destination, relativePath);
            if (File.Exists(destinationFile))
                continue;

            string destinationDirectory = Path.GetDirectoryName(destinationFile)!;
            Directory.CreateDirectory(destinationDirectory);
            try
            {
                File.Copy(sourceFile, destinationFile, overwrite: false);
            }
            catch (IOException) when (File.Exists(destinationFile))
            {
            }
            catch (UnauthorizedAccessException)
            {
                succeeded = false;
            }
            catch (IOException)
            {
                succeeded = false;
            }
        }

        return succeeded;
    }

    private static string GetMigrationMarkerPath(string root, string application, string migrationName)
    {
        string markerDirectory = Path.Combine(root, application, ".migrations");
        Directory.CreateDirectory(markerDirectory);
        return Path.Combine(markerDirectory, migrationName + ".complete");
    }

    private static string NormalizeMigrationName(string value)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        var characters = value.ToCharArray();
        for (int index = 0; index < characters.Length; index++)
        {
            if (characters[index] == Path.DirectorySeparatorChar ||
                characters[index] == Path.AltDirectorySeparatorChar ||
                Array.IndexOf(invalidCharacters, characters[index]) >= 0)
            {
                characters[index] = '-';
            }
        }

        return new string(characters);
    }

    private static void WriteMigrationMarker(string markerPath)
    {
        if (!File.Exists(markerPath))
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
    }

    private static void ValidateApplication(string application)
    {
        if (string.IsNullOrWhiteSpace(application))
            throw new ArgumentException("An application name is required.", nameof(application));
        if (Path.IsPathRooted(application) ||
            application.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
            application.IndexOf(Path.AltDirectorySeparatorChar) >= 0 ||
            application.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("The application name must be a single valid path segment.", nameof(application));
        }
    }
}
