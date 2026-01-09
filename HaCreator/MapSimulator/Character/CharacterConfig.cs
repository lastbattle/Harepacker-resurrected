using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HaCreator.MapSimulator.Character
{
    /// <summary>
    /// Serializable character preset data
    /// </summary>
    public class CharacterPreset
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }

        // Character data
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CharacterGender Gender { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SkinColor Skin { get; set; }

        public int FaceId { get; set; }
        public int HairId { get; set; }

        // Equipment IDs
        public Dictionary<string, int> Equipment { get; set; } = new();

        // Stats
        public int Level { get; set; } = 1;
        public int MaxHP { get; set; } = 50;
        public int MaxMP { get; set; } = 50;
        public int Attack { get; set; } = 10;
        public int Defense { get; set; } = 5;
        public float Speed { get; set; } = 100;
        public float JumpPower { get; set; } = 100;

        /// <summary>
        /// Create preset from build
        /// </summary>
        public static CharacterPreset FromBuild(CharacterBuild build, string name = null)
        {
            var preset = new CharacterPreset
            {
                Id = build.Id,
                Name = name ?? build.Name ?? "Unnamed",
                Created = DateTime.Now,
                Modified = DateTime.Now,
                Gender = build.Gender,
                Skin = build.Skin,
                FaceId = build.Face?.ItemId ?? 20000,
                HairId = build.Hair?.ItemId ?? 30000,
                Level = build.Level,
                MaxHP = build.MaxHP,
                MaxMP = build.MaxMP,
                Attack = build.Attack,
                Defense = build.Defense,
                Speed = build.Speed,
                JumpPower = build.JumpPower
            };

            // Save equipment IDs
            foreach (var kv in build.Equipment)
            {
                if (kv.Value != null)
                {
                    preset.Equipment[kv.Key.ToString()] = kv.Value.ItemId;
                }
            }

            return preset;
        }

        /// <summary>
        /// Apply preset to build using loader
        /// </summary>
        public CharacterBuild ToBuild(CharacterLoader loader)
        {
            var build = new CharacterBuild
            {
                Id = Id,
                Name = Name,
                Gender = Gender,
                Skin = Skin,
                Body = loader.LoadBody(Skin),
                Head = loader.LoadHead(Skin),
                Face = loader.LoadFace(FaceId),
                Hair = loader.LoadHair(HairId),
                Level = Level,
                MaxHP = MaxHP,
                MaxMP = MaxMP,
                HP = MaxHP,
                MP = MaxMP,
                Attack = Attack,
                Defense = Defense,
                Speed = Speed,
                JumpPower = JumpPower
            };

            // Load equipment
            foreach (var kv in Equipment)
            {
                if (Enum.TryParse<EquipSlot>(kv.Key, out var slot))
                {
                    var equip = loader.LoadEquipment(kv.Value);
                    if (equip != null)
                    {
                        build.Equipment[slot] = equip;
                    }
                }
            }

            return build;
        }
    }

    /// <summary>
    /// Character configuration manager - saves/loads presets
    /// </summary>
    public class CharacterConfigManager
    {
        private readonly string _configDirectory;
        private readonly Dictionary<int, CharacterPreset> _presets = new();
        private int _nextId = 1;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public IReadOnlyDictionary<int, CharacterPreset> Presets => _presets;

        public CharacterConfigManager(string configDirectory = null)
        {
            _configDirectory = configDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HaCreator", "Characters");

            // Ensure directory exists
            Directory.CreateDirectory(_configDirectory);
        }

        #region Preset Management

        /// <summary>
        /// Create a new preset from a build
        /// </summary>
        public CharacterPreset CreatePreset(CharacterBuild build, string name = null)
        {
            var preset = CharacterPreset.FromBuild(build, name);
            preset.Id = _nextId++;
            preset.Created = DateTime.Now;
            preset.Modified = DateTime.Now;

            _presets[preset.Id] = preset;
            return preset;
        }

        /// <summary>
        /// Update an existing preset
        /// </summary>
        public void UpdatePreset(int id, CharacterBuild build)
        {
            if (!_presets.TryGetValue(id, out var existing))
                return;

            var updated = CharacterPreset.FromBuild(build, existing.Name);
            updated.Id = id;
            updated.Created = existing.Created;
            updated.Modified = DateTime.Now;

            _presets[id] = updated;
        }

        /// <summary>
        /// Delete a preset
        /// </summary>
        public bool DeletePreset(int id)
        {
            if (!_presets.Remove(id))
                return false;

            // Delete file
            string filePath = GetPresetFilePath(id);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            return true;
        }

        /// <summary>
        /// Rename a preset
        /// </summary>
        public void RenamePreset(int id, string newName)
        {
            if (_presets.TryGetValue(id, out var preset))
            {
                preset.Name = newName;
                preset.Modified = DateTime.Now;
            }
        }

        /// <summary>
        /// Get a preset by ID
        /// </summary>
        public CharacterPreset GetPreset(int id)
        {
            return _presets.TryGetValue(id, out var preset) ? preset : null;
        }

        /// <summary>
        /// Get all presets sorted by name
        /// </summary>
        public IEnumerable<CharacterPreset> GetAllPresets()
        {
            var sorted = new List<CharacterPreset>(_presets.Values);
            sorted.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return sorted;
        }

        #endregion

        #region Save/Load

        /// <summary>
        /// Save a preset to disk
        /// </summary>
        public void SavePreset(int id)
        {
            if (!_presets.TryGetValue(id, out var preset))
                return;

            string filePath = GetPresetFilePath(id);
            string json = JsonSerializer.Serialize(preset, JsonOptions);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Save all presets to disk
        /// </summary>
        public void SaveAllPresets()
        {
            foreach (var id in _presets.Keys)
            {
                SavePreset(id);
            }
        }

        /// <summary>
        /// Load a preset from disk
        /// </summary>
        public CharacterPreset LoadPreset(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            try
            {
                string json = File.ReadAllText(filePath);
                var preset = JsonSerializer.Deserialize<CharacterPreset>(json, JsonOptions);
                if (preset != null)
                {
                    _presets[preset.Id] = preset;
                    if (preset.Id >= _nextId)
                        _nextId = preset.Id + 1;
                }
                return preset;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Load all presets from disk
        /// </summary>
        public void LoadAllPresets()
        {
            _presets.Clear();
            _nextId = 1;

            if (!Directory.Exists(_configDirectory))
                return;

            foreach (var file in Directory.GetFiles(_configDirectory, "*.json"))
            {
                LoadPreset(file);
            }
        }

        /// <summary>
        /// Export preset to a specific file
        /// </summary>
        public void ExportPreset(int id, string filePath)
        {
            if (!_presets.TryGetValue(id, out var preset))
                return;

            string json = JsonSerializer.Serialize(preset, JsonOptions);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Import preset from a file
        /// </summary>
        public CharacterPreset ImportPreset(string filePath)
        {
            var preset = LoadPreset(filePath);
            if (preset != null)
            {
                // Assign new ID to avoid conflicts
                preset.Id = _nextId++;
                _presets[preset.Id] = preset;
            }
            return preset;
        }

        private string GetPresetFilePath(int id)
        {
            return Path.Combine(_configDirectory, $"character_{id}.json");
        }

        #endregion

        #region Quick Presets

        /// <summary>
        /// Create default male preset
        /// </summary>
        public CharacterPreset CreateDefaultMalePreset()
        {
            var preset = new CharacterPreset
            {
                Id = _nextId++,
                Name = "Default Male",
                Created = DateTime.Now,
                Modified = DateTime.Now,
                Gender = CharacterGender.Male,
                Skin = SkinColor.Light,
                FaceId = 20000,
                HairId = 30000,
                Level = 1,
                MaxHP = 50,
                MaxMP = 50,
                Attack = 10,
                Defense = 5,
                Speed = 100,
                JumpPower = 100
            };

            _presets[preset.Id] = preset;
            return preset;
        }

        /// <summary>
        /// Create default female preset
        /// </summary>
        public CharacterPreset CreateDefaultFemalePreset()
        {
            var preset = new CharacterPreset
            {
                Id = _nextId++,
                Name = "Default Female",
                Created = DateTime.Now,
                Modified = DateTime.Now,
                Gender = CharacterGender.Female,
                Skin = SkinColor.Light,
                FaceId = 21000,
                HairId = 31000,
                Level = 1,
                MaxHP = 50,
                MaxMP = 50,
                Attack = 10,
                Defense = 5,
                Speed = 100,
                JumpPower = 100
            };

            _presets[preset.Id] = preset;
            return preset;
        }

        /// <summary>
        /// Create random preset
        /// </summary>
        public CharacterPreset CreateRandomPreset()
        {
            var random = new Random();
            var gender = random.Next(2) == 0 ? CharacterGender.Male : CharacterGender.Female;

            var preset = new CharacterPreset
            {
                Id = _nextId++,
                Name = $"Random_{DateTime.Now:yyyyMMddHHmmss}",
                Created = DateTime.Now,
                Modified = DateTime.Now,
                Gender = gender,
                Skin = (SkinColor)random.Next(5),
                FaceId = gender == CharacterGender.Male ? 20000 + random.Next(30) : 21000 + random.Next(30),
                HairId = gender == CharacterGender.Male ? 30000 + random.Next(50) : 31000 + random.Next(50),
                Level = 1,
                MaxHP = 50,
                MaxMP = 50,
                Attack = 10,
                Defense = 5,
                Speed = 100,
                JumpPower = 100
            };

            _presets[preset.Id] = preset;
            return preset;
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validate a preset has valid IDs
        /// </summary>
        public bool ValidatePreset(CharacterPreset preset, CharacterLoader loader)
        {
            // Try to load core parts
            var body = loader.LoadBody(preset.Skin);
            var head = loader.LoadHead(preset.Skin);
            var face = loader.LoadFace(preset.FaceId);
            var hair = loader.LoadHair(preset.HairId);

            return body != null && head != null;
        }

        /// <summary>
        /// Get list of missing items in a preset
        /// </summary>
        public List<(string type, int id)> GetMissingItems(CharacterPreset preset, CharacterLoader loader)
        {
            var missing = new List<(string, int)>();

            if (loader.LoadBody(preset.Skin) == null)
                missing.Add(("Body", 2000 + (int)preset.Skin));
            if (loader.LoadHead(preset.Skin) == null)
                missing.Add(("Head", 12000 + (int)preset.Skin));
            if (loader.LoadFace(preset.FaceId) == null)
                missing.Add(("Face", preset.FaceId));
            if (loader.LoadHair(preset.HairId) == null)
                missing.Add(("Hair", preset.HairId));

            foreach (var kv in preset.Equipment)
            {
                if (loader.LoadEquipment(kv.Value) == null)
                    missing.Add((kv.Key, kv.Value));
            }

            return missing;
        }

        #endregion
    }

    /// <summary>
    /// Recently used characters list
    /// </summary>
    public class RecentCharacters
    {
        private readonly List<int> _recentIds = new();
        private const int MaxRecent = 10;

        public IReadOnlyList<int> RecentIds => _recentIds;

        public void AddRecent(int presetId)
        {
            _recentIds.Remove(presetId);
            _recentIds.Insert(0, presetId);

            while (_recentIds.Count > MaxRecent)
            {
                _recentIds.RemoveAt(_recentIds.Count - 1);
            }
        }

        public void Clear()
        {
            _recentIds.Clear();
        }

        public string Serialize()
        {
            return string.Join(",", _recentIds);
        }

        public void Deserialize(string data)
        {
            _recentIds.Clear();
            if (string.IsNullOrEmpty(data)) return;

            foreach (var part in data.Split(','))
            {
                if (int.TryParse(part, out int id))
                {
                    _recentIds.Add(id);
                }
            }
        }
    }
}
