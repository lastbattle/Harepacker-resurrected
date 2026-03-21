using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Stores simulator-side skill macros per active character so macro definitions persist across sessions.
    /// </summary>
    public sealed class SkillMacroStore
    {
        private sealed class PersistedStore
        {
            public Dictionary<string, List<SkillMacroRecord>> MacrosByCharacter { get; set; } = new(StringComparer.Ordinal);
        }

        private sealed class SkillMacroRecord
        {
            public string Name { get; set; }
            public bool NotifyParty { get; set; }
            public int[] SkillIds { get; set; }
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly Dictionary<string, List<SkillMacroRecord>> _macrosByCharacter = new(StringComparer.Ordinal);
        private readonly string _storageFilePath;

        public SkillMacroStore(string storageFilePath = null)
        {
            _storageFilePath = storageFilePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HaCreator",
                "MapSimulator",
                "skill-macros.json");

            string directoryPath = Path.GetDirectoryName(_storageFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            LoadFromDisk();
        }

        public IReadOnlyList<SkillMacro> GetMacros(CharacterBuild build)
        {
            string key = ResolveCharacterKey(build);
            if (!_macrosByCharacter.TryGetValue(key, out List<SkillMacroRecord> records) || records == null)
            {
                return Array.Empty<SkillMacro>();
            }

            List<SkillMacro> macros = new(records.Count);
            foreach (SkillMacroRecord record in records)
            {
                if (record == null)
                {
                    continue;
                }

                macros.Add(new SkillMacro
                {
                    Name = record.Name ?? string.Empty,
                    NotifyParty = record.NotifyParty,
                    SkillIds = NormalizeSkillIds(record.SkillIds)
                });
            }

            return macros;
        }

        public void Save(CharacterBuild build, IReadOnlyList<SkillMacro> macros)
        {
            if (macros == null)
            {
                return;
            }

            string key = ResolveCharacterKey(build);
            List<SkillMacroRecord> records = new(macros.Count);
            foreach (SkillMacro macro in macros)
            {
                records.Add(new SkillMacroRecord
                {
                    Name = macro?.Name ?? string.Empty,
                    NotifyParty = macro?.NotifyParty == true,
                    SkillIds = NormalizeSkillIds(macro?.SkillIds)
                });
            }

            _macrosByCharacter[key] = records;
            SaveToDisk();
        }

        private static int[] NormalizeSkillIds(int[] skillIds)
        {
            int[] normalized = new int[SkillMacroUI.SKILLS_PER_MACRO];
            if (skillIds == null)
            {
                return normalized;
            }

            for (int i = 0; i < Math.Min(normalized.Length, skillIds.Length); i++)
            {
                normalized[i] = Math.Max(0, skillIds[i]);
            }

            return normalized;
        }

        private static string ResolveCharacterKey(CharacterBuild build)
        {
            if (build == null)
            {
                return "session:default";
            }

            if (build.Id > 0)
            {
                return $"id:{build.Id}";
            }

            if (!string.IsNullOrWhiteSpace(build.Name))
            {
                return $"name:{build.Name.Trim().ToLowerInvariant()}";
            }

            return "session:default";
        }

        private void LoadFromDisk()
        {
            if (string.IsNullOrWhiteSpace(_storageFilePath) || !File.Exists(_storageFilePath))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(_storageFilePath);
                PersistedStore persisted = JsonSerializer.Deserialize<PersistedStore>(json, JsonOptions);
                if (persisted?.MacrosByCharacter == null)
                {
                    return;
                }

                _macrosByCharacter.Clear();
                foreach (KeyValuePair<string, List<SkillMacroRecord>> entry in persisted.MacrosByCharacter)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null)
                    {
                        continue;
                    }

                    List<SkillMacroRecord> records = entry.Value
                        .Where(record => record != null)
                        .Select(record => new SkillMacroRecord
                        {
                            Name = record.Name ?? string.Empty,
                            NotifyParty = record.NotifyParty,
                            SkillIds = NormalizeSkillIds(record.SkillIds)
                        })
                        .ToList();

                    if (records.Count > 0)
                    {
                        _macrosByCharacter[entry.Key] = records;
                    }
                }
            }
            catch
            {
                _macrosByCharacter.Clear();
            }
        }

        private void SaveToDisk()
        {
            if (string.IsNullOrWhiteSpace(_storageFilePath))
            {
                return;
            }

            PersistedStore persisted = new()
            {
                MacrosByCharacter = new Dictionary<string, List<SkillMacroRecord>>(_macrosByCharacter, StringComparer.Ordinal)
            };

            try
            {
                string json = JsonSerializer.Serialize(persisted, JsonOptions);
                File.WriteAllText(_storageFilePath, json);
            }
            catch
            {
                // Ignore persistence failures so macro editing remains usable in restricted environments.
            }
        }
    }
}
