using System;
using System.IO;
using System.Text.Json;
using HaSharedLibrary.Configuration;

namespace MapleGame.Client;

/// <summary>Best-effort persistence for values entered in the interactive launcher.</summary>
internal static class ClientLaunchPreferences
{
    public static string LoadLastImgDirectory()
    {
        try
        {
            string settingsPath = UserDataPaths.MapleGameClientSettingsFile;
            if (!File.Exists(settingsPath))
                return null;

            string json = File.ReadAllText(settingsPath);
            return JsonSerializer.Deserialize<Settings>(json)?.LastImgDirectory;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void SaveLastImgDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return;

        try
        {
            string settingsPath = UserDataPaths.MapleGameClientSettingsFile;
            string json = JsonSerializer.Serialize(new Settings
            {
                LastImgDirectory = Path.GetFullPath(directory)
            });
            File.WriteAllText(settingsPath, json);
        }
        catch (IOException)
        {
            // Launcher preferences must never prevent a valid game session from starting.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class Settings
    {
        public Settings()
        {
        }

        public string LastImgDirectory { get; set; }
    }
}
