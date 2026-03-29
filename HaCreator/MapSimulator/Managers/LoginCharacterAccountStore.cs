using HaCreator.MapSimulator.Character;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Persists login-character rosters per simulator account and world so the
    /// login shell can keep account-backed ownership even without packet input.
    /// </summary>
    public sealed class LoginCharacterAccountStore
    {
        private sealed class PersistedStore
        {
            public Dictionary<string, PersistedAccountState> AccountsByKey { get; set; } = new(StringComparer.Ordinal);
        }

        private sealed class PersistedAccountState
        {
            public const long DefaultCashShopNxCredit = 10000;

            public string AccountName { get; set; }
            public int? AccountId { get; set; }
            public int WorldId { get; set; }
            public int SlotCount { get; set; } = 3;
            public int BuyCharacterCount { get; set; }
            public int NextCharacterId { get; set; } = 1;
            public long? CashShopNxCredit { get; set; }
            public string PicCode { get; set; }
            public string BirthDate { get; set; }
            public bool IsSecondaryPasswordEnabled { get; set; }
            public string SecondaryPassword { get; set; }
            public List<LoginCharacterAccountEntryState> Entries { get; set; } = new();
        }

        public sealed class LoginCharacterAccountState
        {
            public string AccountName { get; init; }
            public int? AccountId { get; init; }
            public int WorldId { get; init; }
            public int SlotCount { get; init; } = 3;
            public int BuyCharacterCount { get; init; }
            public int NextCharacterId { get; init; } = 1;
            public long CashShopNxCredit { get; init; } = PersistedAccountState.DefaultCashShopNxCredit;
            public string PicCode { get; init; } = string.Empty;
            public string BirthDate { get; init; } = string.Empty;
            public bool IsSecondaryPasswordEnabled { get; init; }
            public string SecondaryPassword { get; init; } = string.Empty;
            public IReadOnlyList<LoginCharacterAccountEntryState> Entries { get; init; } = Array.Empty<LoginCharacterAccountEntryState>();
        }

        public sealed class LoginCharacterAccountEntryState
        {
            public int CharacterId { get; init; }
            public string Name { get; init; } = string.Empty;
            public CharacterGender Gender { get; init; }
            public SkinColor Skin { get; init; }
            public int Level { get; init; }
            public int Job { get; init; }
            public int SubJob { get; init; }
            public string JobName { get; init; } = string.Empty;
            public string GuildName { get; init; } = string.Empty;
            public string AllianceName { get; init; } = string.Empty;
            public int Fame { get; init; }
            public int WorldRank { get; init; }
            public int JobRank { get; init; }
            public long Exp { get; init; }
            public long ExpToNextLevel { get; init; }
            public int HP { get; init; }
            public int MaxHP { get; init; }
            public int MP { get; init; }
            public int MaxMP { get; init; }
            public int Strength { get; init; }
            public int Dexterity { get; init; }
            public int Intelligence { get; init; }
            public int Luck { get; init; }
            public int AbilityPoints { get; init; }
            public int FieldMapId { get; init; }
            public string FieldDisplayName { get; init; } = string.Empty;
            public bool CanDelete { get; init; } = true;
            public int? PreviousWorldRank { get; init; }
            public int? PreviousJobRank { get; init; }
            public byte[] AvatarLookPacket { get; init; } = Array.Empty<byte>();
            public int Portal { get; init; }
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly Dictionary<string, PersistedAccountState> _accountsByKey = new(StringComparer.Ordinal);
        private readonly string _storageFilePath;

        public LoginCharacterAccountStore(string storageFilePath = null)
        {
            _storageFilePath = storageFilePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HaCreator",
                "MapSimulator",
                "login-character-accounts.json");

            string directoryPath = Path.GetDirectoryName(_storageFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            LoadFromDisk();
        }

        public static string ResolveAccountKey(string accountName, int worldId, int? accountId = null)
        {
            if (accountId.HasValue && accountId.Value > 0)
            {
                return $"login-roster:accountid:{accountId.Value}:world:{Math.Max(0, worldId)}";
            }

            string normalizedAccount = string.IsNullOrWhiteSpace(accountName)
                ? "explorergm"
                : accountName.Trim().ToLowerInvariant();
            return $"login-roster:{normalizedAccount}:world:{Math.Max(0, worldId)}";
        }

        public LoginCharacterAccountState GetState(string accountName, int worldId, int? accountId = null)
        {
            PersistedAccountState persisted = TryGetPersistedState(accountName, worldId, accountId);
            if (persisted == null)
            {
                return null;
            }

            List<LoginCharacterAccountEntryState> entries = persisted.Entries?
                .Where(entry => entry != null)
                .Select(CloneEntryState)
                .ToList()
                ?? new List<LoginCharacterAccountEntryState>();

            int maxCharacterId = entries.Count == 0 ? 0 : entries.Max(entry => entry.CharacterId);
            int nextCharacterId = Math.Max(Math.Max(1, persisted.NextCharacterId), maxCharacterId + 1);

            return new LoginCharacterAccountState
            {
                AccountName = string.IsNullOrWhiteSpace(persisted.AccountName) ? accountName : persisted.AccountName,
                AccountId = persisted.AccountId,
                WorldId = Math.Max(0, persisted.WorldId),
                SlotCount = Math.Max(0, persisted.SlotCount),
                BuyCharacterCount = Math.Max(0, persisted.BuyCharacterCount),
                NextCharacterId = nextCharacterId,
                CashShopNxCredit = NormalizeCashShopNxCredit(persisted.CashShopNxCredit),
                PicCode = NormalizeSecret(persisted.PicCode),
                BirthDate = NormalizeBirthDate(persisted.BirthDate),
                IsSecondaryPasswordEnabled = persisted.IsSecondaryPasswordEnabled,
                SecondaryPassword = NormalizeSecret(persisted.SecondaryPassword),
                Entries = entries
            };
        }

        public void SaveState(
            string accountName,
            int worldId,
            int slotCount,
            int buyCharacterCount,
            int nextCharacterId,
            IEnumerable<LoginCharacterAccountEntryState> entries,
            long cashShopNxCredit = PersistedAccountState.DefaultCashShopNxCredit,
            string picCode = null,
            string birthDate = null,
            bool isSecondaryPasswordEnabled = false,
            string secondaryPassword = null,
            int? accountId = null)
        {
            string normalizedAccountName = string.IsNullOrWhiteSpace(accountName) ? "explorergm" : accountName.Trim();
            List<LoginCharacterAccountEntryState> normalizedEntries = entries?
                .Where(entry => entry != null)
                .Select(CloneEntryState)
                .ToList()
                ?? new List<LoginCharacterAccountEntryState>();

            int maxCharacterId = normalizedEntries.Count == 0 ? 0 : normalizedEntries.Max(entry => entry.CharacterId);
            int normalizedNextCharacterId = Math.Max(Math.Max(1, nextCharacterId), maxCharacterId + 1);

            PersistedAccountState persistedState = new()
            {
                AccountName = normalizedAccountName,
                AccountId = accountId.HasValue && accountId.Value > 0 ? accountId.Value : null,
                WorldId = Math.Max(0, worldId),
                SlotCount = Math.Max(0, slotCount),
                BuyCharacterCount = Math.Max(0, buyCharacterCount),
                NextCharacterId = normalizedNextCharacterId,
                CashShopNxCredit = NormalizeCashShopNxCredit(cashShopNxCredit),
                PicCode = NormalizeSecret(picCode),
                BirthDate = NormalizeBirthDate(birthDate),
                IsSecondaryPasswordEnabled = isSecondaryPasswordEnabled,
                SecondaryPassword = NormalizeSecret(secondaryPassword),
                Entries = normalizedEntries
            };
            _accountsByKey[ResolveAccountKey(normalizedAccountName, worldId, accountId)] = persistedState;

            if (accountId.HasValue && accountId.Value > 0)
            {
                _accountsByKey[ResolveAccountKey(normalizedAccountName, worldId)] = ClonePersistedState(persistedState);
            }

            SaveToDisk();
        }

        private PersistedAccountState TryGetPersistedState(string accountName, int worldId, int? accountId)
        {
            string accountIdKey = ResolveAccountKey(accountName, worldId, accountId);
            if (_accountsByKey.TryGetValue(accountIdKey, out PersistedAccountState persistedById) && persistedById != null)
            {
                return persistedById;
            }

            string accountNameKey = ResolveAccountKey(accountName, worldId);
            if (_accountsByKey.TryGetValue(accountNameKey, out PersistedAccountState persistedByName) && persistedByName != null)
            {
                return persistedByName;
            }

            return null;
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
                if (persisted?.AccountsByKey == null)
                {
                    return;
                }

                _accountsByKey.Clear();
                foreach (KeyValuePair<string, PersistedAccountState> entry in persisted.AccountsByKey)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null)
                    {
                        continue;
                    }

                    _accountsByKey[entry.Key] = entry.Value;
                }
            }
            catch
            {
                _accountsByKey.Clear();
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
                AccountsByKey = new Dictionary<string, PersistedAccountState>(_accountsByKey, StringComparer.Ordinal)
            };

            try
            {
                string json = JsonSerializer.Serialize(persisted, JsonOptions);
                File.WriteAllText(_storageFilePath, json);
            }
            catch
            {
                // Ignore persistence failures so login remains usable when writes are blocked.
            }
        }

        private static LoginCharacterAccountEntryState CloneEntryState(LoginCharacterAccountEntryState entry)
        {
            return new LoginCharacterAccountEntryState
            {
                CharacterId = entry.CharacterId,
                Name = entry.Name ?? string.Empty,
                Gender = entry.Gender,
                Skin = entry.Skin,
                Level = entry.Level,
                Job = entry.Job,
                SubJob = entry.SubJob,
                JobName = entry.JobName ?? string.Empty,
                GuildName = entry.GuildName ?? string.Empty,
                AllianceName = entry.AllianceName ?? string.Empty,
                Fame = entry.Fame,
                WorldRank = entry.WorldRank,
                JobRank = entry.JobRank,
                Exp = entry.Exp,
                ExpToNextLevel = entry.ExpToNextLevel,
                HP = entry.HP,
                MaxHP = entry.MaxHP,
                MP = entry.MP,
                MaxMP = entry.MaxMP,
                Strength = entry.Strength,
                Dexterity = entry.Dexterity,
                Intelligence = entry.Intelligence,
                Luck = entry.Luck,
                AbilityPoints = entry.AbilityPoints,
                FieldMapId = entry.FieldMapId,
                FieldDisplayName = entry.FieldDisplayName ?? string.Empty,
                CanDelete = entry.CanDelete,
                PreviousWorldRank = entry.PreviousWorldRank,
                PreviousJobRank = entry.PreviousJobRank,
                AvatarLookPacket = entry.AvatarLookPacket != null ? (byte[])entry.AvatarLookPacket.Clone() : Array.Empty<byte>(),
                Portal = entry.Portal
            };
        }

        private static string NormalizeSecret(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string NormalizeBirthDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string digits = new(value.Where(char.IsDigit).Take(8).ToArray());
            return digits.Length == 8 ? digits : string.Empty;
        }

        private static long NormalizeCashShopNxCredit(long? value)
        {
            long normalized = value ?? PersistedAccountState.DefaultCashShopNxCredit;
            return Math.Max(0, normalized);
        }

        private static PersistedAccountState ClonePersistedState(PersistedAccountState state)
        {
            return new PersistedAccountState
            {
                AccountName = state?.AccountName,
                AccountId = state?.AccountId,
                WorldId = state?.WorldId ?? 0,
                SlotCount = state?.SlotCount ?? 3,
                BuyCharacterCount = state?.BuyCharacterCount ?? 0,
                NextCharacterId = state?.NextCharacterId ?? 1,
                CashShopNxCredit = NormalizeCashShopNxCredit(state?.CashShopNxCredit),
                PicCode = NormalizeSecret(state?.PicCode),
                BirthDate = NormalizeBirthDate(state?.BirthDate),
                IsSecondaryPasswordEnabled = state?.IsSecondaryPasswordEnabled ?? false,
                SecondaryPassword = NormalizeSecret(state?.SecondaryPassword),
                Entries = state?.Entries?
                    .Where(entry => entry != null)
                    .Select(CloneEntryState)
                    .ToList()
                    ?? new List<LoginCharacterAccountEntryState>()
            };
        }
    }
}
