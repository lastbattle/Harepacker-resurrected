using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using MapleLib.Configuration;
using MapleLib.Helpers;
using MapleLib.MapleCryptoLib;

namespace HaSharedLibrary.Configuration;

/// <summary>
/// Loads and atomically saves HaRepacker's user preferences, application state,
/// and custom WZ keys in the shared Harepacker user-data hierarchy.
/// </summary>
public sealed class HaRepackerSettingsStore
{
    private const string UserSettingsFileName = "Settings.txt";
    private const string ApplicationSettingsFileName = "ApplicationSettings.txt";
    private const string CustomKeysFileName = "CustomKeys.txt";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true
    };

    private readonly object ioLock = new();
    private readonly string folderPath;
    private bool loaded;

    public HaRepackerUserSettings UserSettings { get; private set; } = new();
    public HaRepackerApplicationSettings ApplicationSettings { get; private set; } = new();
    public List<EncryptionKey> CustomKeys { get; private set; } = new();

    public HaRepackerSettingsStore()
        : this(UserDataPaths.HaRepackerDirectory)
    {
    }

    /// <summary>
    /// Creates a store at an explicit directory for tests and portable hosts.
    /// Desktop applications should use the parameterless canonical-path constructor.
    /// </summary>
    public HaRepackerSettingsStore(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            throw new ArgumentException("A settings directory is required.", nameof(folderPath));

        this.folderPath = Path.GetFullPath(folderPath);
        Directory.CreateDirectory(this.folderPath);
    }

    public bool Load()
    {
        lock (ioLock)
        {
            string userFilePath = Path.Combine(folderPath, UserSettingsFileName);
            string applicationFilePath = Path.Combine(folderPath, ApplicationSettingsFileName);
            string customKeysFilePath = Path.Combine(folderPath, CustomKeysFileName);

            try
            {
                if (!File.Exists(userFilePath) ||
                    !File.Exists(applicationFilePath) ||
                    !File.Exists(customKeysFilePath))
                {
                    ResetToDefaults();
                    return false;
                }

                UserSettings = JsonSerializer.Deserialize<HaRepackerUserSettings>(File.ReadAllText(userFilePath), JsonOptions)
                    ?? throw new JsonException("User settings cannot contain JSON null.");
                ApplicationSettings = JsonSerializer.Deserialize<HaRepackerApplicationSettings>(
                        File.ReadAllText(applicationFilePath), JsonOptions)
                    ?? throw new JsonException("Application settings cannot contain JSON null.");
                CustomKeys = JsonSerializer.Deserialize<List<EncryptionKey>>(
                        File.ReadAllText(customKeysFilePath), JsonOptions)
                    ?? throw new JsonException("Custom keys cannot contain JSON null.");
                loaded = true;
                return true;
            }
            catch (Exception)
            {
                // Keep malformed data as a recovery artifact; saving valid defaults will
                // atomically replace it once the user changes or closes the application.
                ResetToDefaults();
                return false;
            }
        }
    }

    public bool Save()
    {
        lock (ioLock)
        {
            string[] targetPaths =
            [
                Path.Combine(folderPath, UserSettingsFileName),
                Path.Combine(folderPath, ApplicationSettingsFileName),
                Path.Combine(folderPath, CustomKeysFileName)
            ];
            string[] contents =
            [
                JsonSerializer.Serialize(UserSettings, JsonOptions),
                JsonSerializer.Serialize(ApplicationSettings, JsonOptions),
                JsonSerializer.Serialize(CustomKeys, JsonOptions)
            ];
            string[] temporaryPaths = new string[targetPaths.Length];

            try
            {
                for (int index = 0; index < targetPaths.Length; index++)
                {
                    if (Directory.Exists(targetPaths[index]))
                        throw new IOException($"Configuration target is a directory: {targetPaths[index]}");

                    temporaryPaths[index] = $"{targetPaths[index]}.{Guid.NewGuid():N}.tmp";
                    WriteTemporaryFile(temporaryPaths[index], contents[index]);
                }

                for (int index = 0; index < targetPaths.Length; index++)
                    ReplaceFile(temporaryPaths[index], targetPaths[index]);

                loaded = true;
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                foreach (string temporaryPath in temporaryPaths)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(temporaryPath))
                            File.Delete(temporaryPath);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }

    public byte[] GetCusomWzIVEncryption()
    {
        if (!loaded)
            loaded = Load();

        if (loaded)
        {
            try
            {
                byte[] bytes = ByteUtils.HexToBytes(
                    ApplicationSettings.MapleVersion_CustomEncryptionBytes ?? string.Empty);
                if (bytes.Length == 4)
                    return bytes;
            }
            catch (FormatException)
            {
            }
        }

        return new byte[4];
    }

    public void SetCustomWzUserKeyFromConfig()
    {
        byte[] bytes;
        try
        {
            bytes = ByteUtils.HexToBytes(
                ApplicationSettings.MapleVersion_CustomAESUserKey ?? string.Empty);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("The configured WZ user key is not valid hexadecimal.", ex);
        }

        if (bytes.Length == 0)
        {
            MapleCryptoConstants.UserKey_WzLib =
                (byte[])MapleCryptoConstants.MAPLESTORY_USERKEY_DEFAULT.Clone();
            return;
        }

        if (bytes.Length != 32)
            throw new InvalidDataException("The configured WZ user key must contain exactly 32 bytes.");

        byte[] expanded = new byte[MapleCryptoConstants.MAPLESTORY_USERKEY_DEFAULT.Length];
        for (int index = 0; index < expanded.Length; index += 4)
            expanded[index] = bytes[index / 4];

        MapleCryptoConstants.UserKey_WzLib = expanded;
    }

    private static void WriteTemporaryFile(string path, string content)
    {
        using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            options: FileOptions.WriteThrough);
        using (StreamWriter writer = new(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
        {
            writer.Write(content);
            writer.Flush();
        }

        stream.Flush(flushToDisk: true);
    }

    private static void ReplaceFile(string temporaryPath, string targetPath)
    {
        if (File.Exists(targetPath))
            File.Replace(temporaryPath, targetPath, destinationBackupFileName: null);
        else
            File.Move(temporaryPath, targetPath);
    }

    private void ResetToDefaults()
    {
        loaded = false;
        UserSettings = new HaRepackerUserSettings();
        ApplicationSettings = new HaRepackerApplicationSettings();
        CustomKeys = new List<EncryptionKey>();
    }
}
