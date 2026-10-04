using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

namespace HaSharedLibrary.Configuration;

/// <summary>
/// Owns HaCreator's recently opened map database and imports historical databases.
/// </summary>
public sealed class MapHistoryStore
{
    private const string TableName = "LoadedMapsHistory";

    private readonly object migrationLock = new();
    private readonly string databasePath;
    private readonly string migrationMarkerPath;
    private readonly IReadOnlyList<string> legacyDatabasePaths;
    private bool migrationChecked;

    public MapHistoryStore(
        string databasePath,
        IEnumerable<string> legacyDatabasePaths = null,
        string migrationMarkerPath = null)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("A map-history database path is required.", nameof(databasePath));

        this.databasePath = Path.GetFullPath(databasePath);
        this.migrationMarkerPath = migrationMarkerPath ?? this.databasePath + ".legacy-import-v1.complete";
        this.legacyDatabasePaths = legacyDatabasePaths == null
            ? Array.Empty<string>()
            : new List<string>(legacyDatabasePaths);

        string directory = Path.GetDirectoryName(this.databasePath)
            ?? throw new InvalidOperationException("The map-history database has no parent directory.");
        Directory.CreateDirectory(directory);
    }

    public IReadOnlyList<string> Load()
    {
        EnsureMigrated();
        var result = new List<string>();
        using var connection = OpenConnection(databasePath);
        EnsureTable(connection);
        using var command = new SQLiteCommand(
            $"SELECT OpenedMapName FROM {TableName} " +
            "WHERE OpenedMapName IS NOT NULL AND TRIM(OpenedMapName) <> '' " +
            "GROUP BY OpenedMapName ORDER BY MIN(Id);",
            connection);
        using SQLiteDataReader reader = command.ExecuteReader();
        while (reader.Read())
            result.Add((string)reader["OpenedMapName"]);

        return result;
    }

    public void Add(string openedMapName)
    {
        if (string.IsNullOrWhiteSpace(openedMapName))
            return;

        EnsureMigrated();
        using var connection = OpenConnection(databasePath);
        EnsureTable(connection);
        InsertIfMissing(connection, openedMapName);
    }

    public void Clear()
    {
        EnsureMigrated();
        using var connection = OpenConnection(databasePath);
        EnsureTable(connection);
        using var command = new SQLiteCommand($"DELETE FROM {TableName};", connection);
        command.ExecuteNonQuery();
    }

    public void Remove(string openedMapName)
    {
        if (string.IsNullOrWhiteSpace(openedMapName))
            return;

        EnsureMigrated();
        using var connection = OpenConnection(databasePath);
        EnsureTable(connection);
        using var command = new SQLiteCommand(
            $"DELETE FROM {TableName} WHERE OpenedMapName = @OpenedMapName;",
            connection);
        command.Parameters.AddWithValue("@OpenedMapName", openedMapName);
        command.ExecuteNonQuery();
    }

    private void EnsureMigrated()
    {
        lock (migrationLock)
        {
            if (migrationChecked)
                return;

            if (File.Exists(migrationMarkerPath))
            {
                migrationChecked = true;
                return;
            }

            using (var destination = OpenConnection(databasePath))
            {
                EnsureTable(destination);
                var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string legacyPathValue in legacyDatabasePaths)
                {
                    if (string.IsNullOrWhiteSpace(legacyPathValue))
                        continue;

                    string legacyPath = Path.GetFullPath(legacyPathValue);
                    if (!uniquePaths.Add(legacyPath) ||
                        string.Equals(legacyPath, databasePath, StringComparison.OrdinalIgnoreCase) ||
                        !File.Exists(legacyPath))
                    {
                        continue;
                    }

                    ImportLegacyDatabase(destination, legacyPath);
                }
            }

            string markerDirectory = Path.GetDirectoryName(migrationMarkerPath);
            if (!string.IsNullOrEmpty(markerDirectory))
                Directory.CreateDirectory(markerDirectory);
            File.WriteAllText(migrationMarkerPath, DateTime.UtcNow.ToString("O"));
            migrationChecked = true;
        }
    }

    private static void ImportLegacyDatabase(SQLiteConnection destination, string legacyPath)
    {
        try
        {
            using var source = OpenConnection(legacyPath);
            using var select = new SQLiteCommand($"SELECT OpenedMapName FROM {TableName};", source);
            using SQLiteDataReader reader = select.ExecuteReader();
            while (reader.Read())
            {
                string openedMapName = reader["OpenedMapName"] as string;
                if (!string.IsNullOrWhiteSpace(openedMapName))
                    InsertIfMissing(destination, openedMapName);
            }
        }
        catch (SQLiteException)
        {
            // Obsolete or malformed databases do not make the canonical store unusable.
        }
    }

    private static SQLiteConnection OpenConnection(string path)
    {
        var builder = new SQLiteConnectionStringBuilder
        {
            DataSource = path,
            Version = 3
        };
        var connection = new SQLiteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    private static void EnsureTable(SQLiteConnection connection)
    {
        using var command = new SQLiteCommand(
            $"CREATE TABLE IF NOT EXISTS {TableName} (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT," +
            "OpenedMapName TEXT);",
            connection);
        command.ExecuteNonQuery();
    }

    private static void InsertIfMissing(SQLiteConnection connection, string openedMapName)
    {
        using var command = new SQLiteCommand(
            $"INSERT INTO {TableName} (OpenedMapName) " +
            $"SELECT @OpenedMapName WHERE NOT EXISTS " +
            $"(SELECT 1 FROM {TableName} WHERE OpenedMapName = @OpenedMapName);",
            connection);
        command.Parameters.AddWithValue("@OpenedMapName", openedMapName);
        command.ExecuteNonQuery();
    }
}
