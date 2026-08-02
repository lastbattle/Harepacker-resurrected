using HaCreator.MapSimulator.Character;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HaCreator.MapSimulator.Managers
{
    internal sealed class PacketOwnedFuncKeyConfigStore
    {
        internal sealed class BindingRecord
        {
            public string PrimaryKey { get; set; } = nameof(Keys.None);
            public string SecondaryKey { get; set; } = nameof(Keys.None);
            public string GamepadButton { get; set; } = "0";
        }

        internal sealed class FuncKeyMappedRecord
        {
            public byte Type { get; set; }
            public int Id { get; set; }
        }

        internal sealed class Snapshot
        {
            public List<FuncKeyMappedRecord> FuncKeyMapped { get; set; } = new();
            public Dictionary<string, BindingRecord> SimulatorBindings { get; set; } = new(StringComparer.Ordinal);
            public int PetConsumeItemId { get; set; }
            public string PetConsumeInventoryType { get; set; } = string.Empty;
            public int PetConsumeMpItemId { get; set; }
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly string _filePath;

        public PacketOwnedFuncKeyConfigStore(string filePath = null)
        {
            string configDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HaCreator",
                "MapSimulator");
            Directory.CreateDirectory(configDirectory);
            _filePath = filePath ?? Path.Combine(configDirectory, "packet-owned-funckey-config.json");
        }

        public Snapshot Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    return null;
                }

                string json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<Snapshot>(json, JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        public void Save(Snapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(snapshot, JsonOptions);
            File.WriteAllText(_filePath, json);
        }

        public static Dictionary<string, BindingRecord> CreateBindingRecords(PlayerInput input)
        {
            var bindings = new Dictionary<string, BindingRecord>(StringComparer.Ordinal);
            if (input == null)
            {
                return bindings;
            }

            foreach ((InputAction action, _, _, _) in PlayerInput.GetDefaultBindings())
            {
                KeyBinding binding = input.GetBinding(action);
                if (binding == null)
                {
                    continue;
                }

                bindings[action.ToString()] = new BindingRecord
                {
                    PrimaryKey = binding.PrimaryKey.ToString(),
                    SecondaryKey = binding.SecondaryKey.ToString(),
                    GamepadButton = binding.GamepadButton.ToString()
                };
            }

            return bindings;
        }
    }
}
