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
            public LoginExtraCharInfoResultProfile ExtraCharInfoResult { get; set; }
            public List<CashShopStorageExpansionRecordState> StorageExpansionHistory { get; set; } = new();
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
            public LoginExtraCharInfoResultProfile ExtraCharInfoResult { get; init; }
            public IReadOnlyList<CashShopStorageExpansionRecordState> StorageExpansionHistory { get; init; } = Array.Empty<CashShopStorageExpansionRecordState>();
            public IReadOnlyList<LoginCharacterAccountEntryState> Entries { get; init; } = Array.Empty<LoginCharacterAccountEntryState>();
        }

        public sealed class CashShopStorageExpansionRecordState
        {
            public int CashItemResultSubtype { get; init; }
            public int CommoditySerialNumber { get; init; }
            public int ResultSubtype { get; init; }
            public int FailureReason { get; init; }
            public long NxPrice { get; init; }
            public int SlotLimitAfterResult { get; init; }
            public bool IsPacketOwned { get; init; }
            public int PacketType { get; init; }
            public bool CashAlreadySettled { get; init; }
            public string Message { get; init; } = string.Empty;
            public DateTime AppliedAtUtc { get; init; }
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
            public bool HasPacketOwnedRadioCreateLayerLeftContextValue { get; init; }
            public bool PacketOwnedRadioCreateLayerLeftContextValue { get; init; }
            public int PacketOwnedRadioCreateLayerMutationSequence { get; init; }
            public string PacketOwnedRadioCreateLayerLastMutationSource { get; init; } = string.Empty;
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

            LoginExtraCharInfoResultProfile normalizedExtraCharInfoResult =
                NormalizeExtraCharInfoResultProfile(persisted.ExtraCharInfoResult, persisted.AccountId);
            int normalizedBuyCharacterCount = NormalizeBuyCharacterCount(
                persisted.BuyCharacterCount,
                normalizedExtraCharInfoResult,
                persisted.AccountId);

            return new LoginCharacterAccountState
            {
                AccountName = string.IsNullOrWhiteSpace(persisted.AccountName) ? accountName : persisted.AccountName,
                AccountId = persisted.AccountId,
                WorldId = Math.Max(0, persisted.WorldId),
                SlotCount = Math.Max(0, persisted.SlotCount),
                BuyCharacterCount = normalizedBuyCharacterCount,
                NextCharacterId = nextCharacterId,
                CashShopNxCredit = NormalizeCashShopNxCredit(persisted.CashShopNxCredit),
                PicCode = NormalizeSecret(persisted.PicCode),
                BirthDate = NormalizeBirthDate(persisted.BirthDate),
                IsSecondaryPasswordEnabled = persisted.IsSecondaryPasswordEnabled,
                SecondaryPassword = NormalizeSecret(persisted.SecondaryPassword),
                ExtraCharInfoResult = normalizedExtraCharInfoResult,
                StorageExpansionHistory = CloneStorageExpansionHistory(persisted.StorageExpansionHistory),
                Entries = entries
            };
        }

        public IReadOnlyList<LoginCharacterAccountState> SnapshotStatesForAccount(string accountName, int? accountId = null)
        {
            Dictionary<int, LoginCharacterAccountState> statesByWorld = new();
            foreach (PersistedAccountState persisted in EnumeratePersistedStatesForAccount(accountName, accountId))
            {
                if (persisted == null)
                {
                    continue;
                }

                LoginCharacterAccountState state = CreateRuntimeState(
                    persisted,
                    string.IsNullOrWhiteSpace(accountName) ? persisted.AccountName : accountName);
                statesByWorld[Math.Max(0, state.WorldId)] = state;
            }

            return statesByWorld
                .OrderBy(entry => entry.Key)
                .Select(entry => entry.Value)
                .ToArray();
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
            int? accountId = null,
            LoginExtraCharInfoResultProfile extraCharInfoResult = null,
            IEnumerable<CashShopStorageExpansionRecordState> storageExpansionHistory = null)
        {
            string normalizedAccountName = string.IsNullOrWhiteSpace(accountName) ? "explorergm" : accountName.Trim();
            PersistedAccountState existingState = TryGetPersistedState(normalizedAccountName, worldId, accountId);
            List<LoginCharacterAccountEntryState> normalizedEntries = entries?
                .Where(entry => entry != null)
                .Select(CloneEntryState)
                .ToList()
                ?? new List<LoginCharacterAccountEntryState>();
            MergePersistedPacketOwnedRadioCreateLayerState(normalizedEntries, existingState);
            LoginExtraCharInfoResultProfile normalizedExtraCharInfoResult =
                NormalizeExtraCharInfoResultProfile(extraCharInfoResult, accountId);
            int normalizedBuyCharacterCount = NormalizeBuyCharacterCount(
                buyCharacterCount,
                normalizedExtraCharInfoResult,
                accountId);

            int maxCharacterId = normalizedEntries.Count == 0 ? 0 : normalizedEntries.Max(entry => entry.CharacterId);
            int normalizedNextCharacterId = Math.Max(Math.Max(1, nextCharacterId), maxCharacterId + 1);

            PersistedAccountState persistedState = new()
            {
                AccountName = normalizedAccountName,
                AccountId = accountId.HasValue && accountId.Value > 0 ? accountId.Value : null,
                WorldId = Math.Max(0, worldId),
                SlotCount = Math.Max(0, slotCount),
                BuyCharacterCount = normalizedBuyCharacterCount,
                NextCharacterId = normalizedNextCharacterId,
                CashShopNxCredit = NormalizeCashShopNxCredit(cashShopNxCredit),
                PicCode = NormalizeSecret(picCode),
                BirthDate = NormalizeBirthDate(birthDate),
                IsSecondaryPasswordEnabled = isSecondaryPasswordEnabled,
                SecondaryPassword = NormalizeSecret(secondaryPassword),
                ExtraCharInfoResult = normalizedExtraCharInfoResult,
                StorageExpansionHistory = CloneStorageExpansionHistory(storageExpansionHistory),
                Entries = normalizedEntries
            };
            _accountsByKey[ResolveAccountKey(normalizedAccountName, worldId, accountId)] = persistedState;

            if (accountId.HasValue && accountId.Value > 0)
            {
                _accountsByKey[ResolveAccountKey(normalizedAccountName, worldId)] = ClonePersistedState(persistedState);
            }

            SaveToDisk();
        }

        public bool UpdatePacketOwnedRadioCreateLayerState(
            string accountName,
            int worldId,
            int characterId,
            bool hasOverride,
            bool bLeft,
            int mutationSequence,
            string mutationSource,
            int? accountId = null)
        {
            if (characterId <= 0)
            {
                return false;
            }

            string normalizedAccountName = string.IsNullOrWhiteSpace(accountName)
                ? "explorergm"
                : accountName.Trim();
            int normalizedWorldId = Math.Max(0, worldId);
            bool changed = false;

            foreach (string key in EnumerateStateKeys(normalizedAccountName, normalizedWorldId, accountId))
            {
                if (!_accountsByKey.TryGetValue(key, out PersistedAccountState persistedState) || persistedState == null)
                {
                    continue;
                }

                List<LoginCharacterAccountEntryState> entries = persistedState.Entries?
                    .Where(entry => entry != null)
                    .Select(CloneEntryState)
                    .ToList()
                    ?? new List<LoginCharacterAccountEntryState>();
                int entryIndex = entries.FindIndex(entry => entry.CharacterId == characterId);
                if (entryIndex < 0)
                {
                    continue;
                }

                LoginCharacterAccountEntryState existingEntry = entries[entryIndex];
                LoginCharacterAccountEntryState updatedEntry = new()
                {
                    CharacterId = existingEntry.CharacterId,
                    Name = existingEntry.Name,
                    Gender = existingEntry.Gender,
                    Skin = existingEntry.Skin,
                    Level = existingEntry.Level,
                    Job = existingEntry.Job,
                    SubJob = existingEntry.SubJob,
                    JobName = existingEntry.JobName,
                    GuildName = existingEntry.GuildName,
                    AllianceName = existingEntry.AllianceName,
                    Fame = existingEntry.Fame,
                    WorldRank = existingEntry.WorldRank,
                    JobRank = existingEntry.JobRank,
                    Exp = existingEntry.Exp,
                    ExpToNextLevel = existingEntry.ExpToNextLevel,
                    HP = existingEntry.HP,
                    MaxHP = existingEntry.MaxHP,
                    MP = existingEntry.MP,
                    MaxMP = existingEntry.MaxMP,
                    Strength = existingEntry.Strength,
                    Dexterity = existingEntry.Dexterity,
                    Intelligence = existingEntry.Intelligence,
                    Luck = existingEntry.Luck,
                    AbilityPoints = existingEntry.AbilityPoints,
                    FieldMapId = existingEntry.FieldMapId,
                    FieldDisplayName = existingEntry.FieldDisplayName,
                    CanDelete = existingEntry.CanDelete,
                    PreviousWorldRank = existingEntry.PreviousWorldRank,
                    PreviousJobRank = existingEntry.PreviousJobRank,
                    AvatarLookPacket = existingEntry.AvatarLookPacket != null
                        ? (byte[])existingEntry.AvatarLookPacket.Clone()
                        : Array.Empty<byte>(),
                    Portal = existingEntry.Portal,
                    HasPacketOwnedRadioCreateLayerLeftContextValue = hasOverride,
                    PacketOwnedRadioCreateLayerLeftContextValue = hasOverride && bLeft,
                    PacketOwnedRadioCreateLayerMutationSequence = Math.Max(0, mutationSequence),
                    PacketOwnedRadioCreateLayerLastMutationSource = string.IsNullOrWhiteSpace(mutationSource)
                        ? string.Empty
                        : mutationSource.Trim()
                };

                if (AreEquivalentPacketOwnedRadioCreateLayerState(existingEntry, updatedEntry))
                {
                    continue;
                }

                entries[entryIndex] = updatedEntry;
                PersistedAccountState updatedState = ClonePersistedState(persistedState);
                updatedState.Entries = entries;
                _accountsByKey[key] = updatedState;
                changed = true;
            }

            if (changed)
            {
                SaveToDisk();
            }

            return changed;
        }

        public bool BindAccountId(string accountName, int worldId, int accountId)
        {
            if (accountId <= 0)
            {
                return false;
            }

            string accountNameKey = ResolveAccountKey(accountName, worldId);
            if (!_accountsByKey.TryGetValue(accountNameKey, out PersistedAccountState persistedState) ||
                persistedState == null)
            {
                return false;
            }

            PersistedAccountState reboundState = ClonePersistedState(persistedState);
            reboundState.AccountId = accountId;
            reboundState.ExtraCharInfoResult =
                NormalizeExtraCharInfoResultProfile(reboundState.ExtraCharInfoResult, accountId);
            reboundState.BuyCharacterCount = NormalizeBuyCharacterCount(
                reboundState.BuyCharacterCount,
                reboundState.ExtraCharInfoResult,
                accountId);
            _accountsByKey[ResolveAccountKey(accountName, worldId, accountId)] = reboundState;
            _accountsByKey[accountNameKey] = ClonePersistedState(reboundState);
            SaveToDisk();
            return true;
        }

        public bool BindAccountIdForAccount(string accountName, int accountId)
        {
            if (accountId <= 0)
            {
                return false;
            }

            string normalizedAccountName = string.IsNullOrWhiteSpace(accountName)
                ? "explorergm"
                : accountName.Trim();
            Dictionary<int, PersistedAccountState> statesByWorld = new();

            foreach (PersistedAccountState persistedState in _accountsByKey.Values
                         .Where(state =>
                             state != null &&
                             (state.AccountId == accountId ||
                              string.Equals(
                                  string.IsNullOrWhiteSpace(state.AccountName) ? "explorergm" : state.AccountName.Trim(),
                                  normalizedAccountName,
                                  StringComparison.OrdinalIgnoreCase)))
                         .OrderBy(state => Math.Max(0, state.WorldId))
                         .ThenByDescending(state => state.AccountId == accountId))
            {
                int worldId = Math.Max(0, persistedState.WorldId);
                if (!statesByWorld.ContainsKey(worldId))
                {
                    statesByWorld[worldId] = ClonePersistedState(persistedState);
                }
            }

            if (statesByWorld.Count == 0)
            {
                return false;
            }

            foreach ((int worldId, PersistedAccountState persistedState) in statesByWorld)
            {
                PersistedAccountState reboundState = ClonePersistedState(persistedState);
                reboundState.AccountId = accountId;
                reboundState.AccountName = normalizedAccountName;
                reboundState.ExtraCharInfoResult =
                    NormalizeExtraCharInfoResultProfile(reboundState.ExtraCharInfoResult, accountId);
                reboundState.BuyCharacterCount = NormalizeBuyCharacterCount(
                    reboundState.BuyCharacterCount,
                    reboundState.ExtraCharInfoResult,
                    accountId);
                _accountsByKey[ResolveAccountKey(normalizedAccountName, worldId, accountId)] = reboundState;
                _accountsByKey[ResolveAccountKey(normalizedAccountName, worldId)] = ClonePersistedState(reboundState);
            }

            SaveToDisk();
            return true;
        }

        public bool UpdateExtraCharacterEntitlementForAccount(
            string accountName,
            int accountId,
            int buyCharacterCount,
            LoginExtraCharInfoResultProfile extraCharInfoResult)
        {
            if (accountId <= 0)
            {
                return false;
            }

            string normalizedAccountName = string.IsNullOrWhiteSpace(accountName)
                ? "explorergm"
                : accountName.Trim();
            LoginExtraCharInfoResultProfile normalizedExtraCharInfoResult =
                NormalizeExtraCharInfoResultProfile(extraCharInfoResult, accountId);
            Dictionary<int, PersistedAccountState> statesByWorld = new();

            foreach (PersistedAccountState persistedState in EnumeratePersistedStatesForAccount(normalizedAccountName, accountId)
                         .Where(state => state != null)
                         .OrderBy(state => Math.Max(0, state.WorldId)))
            {
                int worldId = Math.Max(0, persistedState.WorldId);
                if (!statesByWorld.ContainsKey(worldId))
                {
                    statesByWorld[worldId] = ClonePersistedState(persistedState);
                }
            }

            if (statesByWorld.Count == 0)
            {
                return false;
            }

            int normalizedBuyCharacterCount = NormalizeBuyCharacterCount(
                buyCharacterCount,
                normalizedExtraCharInfoResult,
                accountId);
            bool changed = false;

            foreach ((int worldId, PersistedAccountState persistedState) in statesByWorld)
            {
                PersistedAccountState reboundState = ClonePersistedState(persistedState);
                reboundState.AccountId = accountId;
                reboundState.AccountName = normalizedAccountName;
                reboundState.ExtraCharInfoResult = CloneExtraCharInfoResultProfile(normalizedExtraCharInfoResult);
                reboundState.BuyCharacterCount = normalizedBuyCharacterCount;

                string accountIdKey = ResolveAccountKey(normalizedAccountName, worldId, accountId);
                string accountNameKey = ResolveAccountKey(normalizedAccountName, worldId);
                bool worldChanged =
                    !AreEquivalentPersistedStates(_accountsByKey.TryGetValue(accountIdKey, out PersistedAccountState persistedById)
                            ? persistedById
                            : null,
                        reboundState) ||
                    !AreEquivalentPersistedStates(_accountsByKey.TryGetValue(accountNameKey, out PersistedAccountState persistedByName)
                            ? persistedByName
                            : null,
                        reboundState);

                _accountsByKey[accountIdKey] = reboundState;
                _accountsByKey[accountNameKey] = ClonePersistedState(reboundState);
                changed |= worldChanged;
            }

            if (changed)
            {
                SaveToDisk();
            }

            return changed;
        }

        public int PruneStatesForAccount(string accountName, IEnumerable<int> keepWorldIds, int? accountId = null)
        {
            string normalizedAccountName = string.IsNullOrWhiteSpace(accountName)
                ? "explorergm"
                : accountName.Trim();
            HashSet<int> retainedWorldIds = keepWorldIds?
                .Select(worldId => Math.Max(0, worldId))
                .ToHashSet()
                ?? new HashSet<int>();
            List<int> removedWorldIds = EnumeratePersistedStatesForAccount(normalizedAccountName, accountId)
                .Where(state => state != null && !retainedWorldIds.Contains(Math.Max(0, state.WorldId)))
                .Select(state => Math.Max(0, state.WorldId))
                .Distinct()
                .ToList();
            if (removedWorldIds.Count == 0)
            {
                return 0;
            }

            foreach (int worldId in removedWorldIds)
            {
                _accountsByKey.Remove(ResolveAccountKey(normalizedAccountName, worldId));
                if (accountId.HasValue && accountId.Value > 0)
                {
                    _accountsByKey.Remove(ResolveAccountKey(normalizedAccountName, worldId, accountId.Value));
                }
            }

            SaveToDisk();
            return removedWorldIds.Count;
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

        private IEnumerable<PersistedAccountState> EnumeratePersistedStatesForAccount(string accountName, int? accountId)
        {
            string normalizedAccountName = string.IsNullOrWhiteSpace(accountName)
                ? "explorergm"
                : accountName.Trim();
            HashSet<int> yieldedWorldIds = new();

            if (accountId.HasValue && accountId.Value > 0)
            {
                foreach (PersistedAccountState persisted in _accountsByKey.Values
                             .Where(state => state != null && state.AccountId == accountId.Value)
                             .OrderBy(state => Math.Max(0, state.WorldId)))
                {
                    int worldId = Math.Max(0, persisted.WorldId);
                    if (yieldedWorldIds.Add(worldId))
                    {
                        yield return persisted;
                    }
                }
            }

            foreach (PersistedAccountState persisted in _accountsByKey.Values
                         .Where(state => state != null &&
                                         string.Equals(
                                             string.IsNullOrWhiteSpace(state.AccountName) ? "explorergm" : state.AccountName.Trim(),
                                             normalizedAccountName,
                                             StringComparison.OrdinalIgnoreCase))
                         .OrderBy(state => Math.Max(0, state.WorldId)))
            {
                int worldId = Math.Max(0, persisted.WorldId);
                if (yieldedWorldIds.Add(worldId))
                {
                    yield return persisted;
                }
            }
        }

        private static LoginCharacterAccountState CreateRuntimeState(PersistedAccountState persisted, string fallbackAccountName)
        {
            List<LoginCharacterAccountEntryState> entries = persisted.Entries?
                .Where(entry => entry != null)
                .Select(CloneEntryState)
                .ToList()
                ?? new List<LoginCharacterAccountEntryState>();

            int maxCharacterId = entries.Count == 0 ? 0 : entries.Max(entry => entry.CharacterId);
            int nextCharacterId = Math.Max(Math.Max(1, persisted.NextCharacterId), maxCharacterId + 1);

            LoginExtraCharInfoResultProfile normalizedExtraCharInfoResult =
                NormalizeExtraCharInfoResultProfile(persisted.ExtraCharInfoResult, persisted.AccountId);
            int normalizedBuyCharacterCount = NormalizeBuyCharacterCount(
                persisted.BuyCharacterCount,
                normalizedExtraCharInfoResult,
                persisted.AccountId);

            return new LoginCharacterAccountState
            {
                AccountName = string.IsNullOrWhiteSpace(persisted.AccountName) ? fallbackAccountName : persisted.AccountName,
                AccountId = persisted.AccountId,
                WorldId = Math.Max(0, persisted.WorldId),
                SlotCount = Math.Max(0, persisted.SlotCount),
                BuyCharacterCount = normalizedBuyCharacterCount,
                NextCharacterId = nextCharacterId,
                CashShopNxCredit = NormalizeCashShopNxCredit(persisted.CashShopNxCredit),
                PicCode = NormalizeSecret(persisted.PicCode),
                BirthDate = NormalizeBirthDate(persisted.BirthDate),
                IsSecondaryPasswordEnabled = persisted.IsSecondaryPasswordEnabled,
                SecondaryPassword = NormalizeSecret(persisted.SecondaryPassword),
                ExtraCharInfoResult = normalizedExtraCharInfoResult,
                StorageExpansionHistory = CloneStorageExpansionHistory(persisted.StorageExpansionHistory),
                Entries = entries
            };
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
                Portal = entry.Portal,
                HasPacketOwnedRadioCreateLayerLeftContextValue = entry.HasPacketOwnedRadioCreateLayerLeftContextValue,
                PacketOwnedRadioCreateLayerLeftContextValue = entry.HasPacketOwnedRadioCreateLayerLeftContextValue && entry.PacketOwnedRadioCreateLayerLeftContextValue,
                PacketOwnedRadioCreateLayerMutationSequence = Math.Max(0, entry.PacketOwnedRadioCreateLayerMutationSequence),
                PacketOwnedRadioCreateLayerLastMutationSource = entry.PacketOwnedRadioCreateLayerLastMutationSource ?? string.Empty
            };
        }

        private static IEnumerable<string> EnumerateStateKeys(string accountName, int worldId, int? accountId)
        {
            if (accountId.HasValue && accountId.Value > 0)
            {
                yield return ResolveAccountKey(accountName, worldId, accountId);
            }

            yield return ResolveAccountKey(accountName, worldId);
        }

        private static void MergePersistedPacketOwnedRadioCreateLayerState(
            IList<LoginCharacterAccountEntryState> entries,
            PersistedAccountState existingState)
        {
            if (entries == null || entries.Count == 0 || existingState?.Entries == null || existingState.Entries.Count == 0)
            {
                return;
            }

            foreach ((LoginCharacterAccountEntryState entry, int index) in entries.Select((entry, index) => (entry, index)))
            {
                if (entry == null || HasPersistedPacketOwnedRadioCreateLayerState(entry))
                {
                    continue;
                }

                LoginCharacterAccountEntryState persistedEntry = existingState.Entries.FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.CharacterId > 0 &&
                    candidate.CharacterId == entry.CharacterId);
                if (persistedEntry == null || !HasPersistedPacketOwnedRadioCreateLayerState(persistedEntry))
                {
                    continue;
                }

                entries[index] = CopyPacketOwnedRadioCreateLayerState(entry, persistedEntry);
            }
        }

        private static LoginCharacterAccountEntryState CopyPacketOwnedRadioCreateLayerState(
            LoginCharacterAccountEntryState target,
            LoginCharacterAccountEntryState source)
        {
            return new LoginCharacterAccountEntryState
            {
                CharacterId = target.CharacterId,
                Name = target.Name,
                Gender = target.Gender,
                Skin = target.Skin,
                Level = target.Level,
                Job = target.Job,
                SubJob = target.SubJob,
                JobName = target.JobName,
                GuildName = target.GuildName,
                AllianceName = target.AllianceName,
                Fame = target.Fame,
                WorldRank = target.WorldRank,
                JobRank = target.JobRank,
                Exp = target.Exp,
                ExpToNextLevel = target.ExpToNextLevel,
                HP = target.HP,
                MaxHP = target.MaxHP,
                MP = target.MP,
                MaxMP = target.MaxMP,
                Strength = target.Strength,
                Dexterity = target.Dexterity,
                Intelligence = target.Intelligence,
                Luck = target.Luck,
                AbilityPoints = target.AbilityPoints,
                FieldMapId = target.FieldMapId,
                FieldDisplayName = target.FieldDisplayName,
                CanDelete = target.CanDelete,
                PreviousWorldRank = target.PreviousWorldRank,
                PreviousJobRank = target.PreviousJobRank,
                AvatarLookPacket = target.AvatarLookPacket != null
                    ? (byte[])target.AvatarLookPacket.Clone()
                    : Array.Empty<byte>(),
                Portal = target.Portal,
                HasPacketOwnedRadioCreateLayerLeftContextValue = source.HasPacketOwnedRadioCreateLayerLeftContextValue,
                PacketOwnedRadioCreateLayerLeftContextValue = source.HasPacketOwnedRadioCreateLayerLeftContextValue && source.PacketOwnedRadioCreateLayerLeftContextValue,
                PacketOwnedRadioCreateLayerMutationSequence = Math.Max(0, source.PacketOwnedRadioCreateLayerMutationSequence),
                PacketOwnedRadioCreateLayerLastMutationSource = source.PacketOwnedRadioCreateLayerLastMutationSource ?? string.Empty
            };
        }

        private static bool HasPersistedPacketOwnedRadioCreateLayerState(LoginCharacterAccountEntryState entry)
        {
            return entry != null &&
                   (entry.HasPacketOwnedRadioCreateLayerLeftContextValue ||
                    entry.PacketOwnedRadioCreateLayerMutationSequence > 0 ||
                    !string.IsNullOrWhiteSpace(entry.PacketOwnedRadioCreateLayerLastMutationSource));
        }

        private static bool AreEquivalentPacketOwnedRadioCreateLayerState(
            LoginCharacterAccountEntryState left,
            LoginCharacterAccountEntryState right)
        {
            return left != null &&
                   right != null &&
                   left.HasPacketOwnedRadioCreateLayerLeftContextValue == right.HasPacketOwnedRadioCreateLayerLeftContextValue &&
                   (!left.HasPacketOwnedRadioCreateLayerLeftContextValue ||
                    left.PacketOwnedRadioCreateLayerLeftContextValue == right.PacketOwnedRadioCreateLayerLeftContextValue) &&
                   Math.Max(0, left.PacketOwnedRadioCreateLayerMutationSequence) ==
                   Math.Max(0, right.PacketOwnedRadioCreateLayerMutationSequence) &&
                   string.Equals(
                       left.PacketOwnedRadioCreateLayerLastMutationSource ?? string.Empty,
                       right.PacketOwnedRadioCreateLayerLastMutationSource ?? string.Empty,
                       StringComparison.Ordinal);
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
                ExtraCharInfoResult = CloneExtraCharInfoResultProfile(state?.ExtraCharInfoResult),
                StorageExpansionHistory = CloneStorageExpansionHistory(state?.StorageExpansionHistory),
                Entries = state?.Entries?
                    .Where(entry => entry != null)
                    .Select(CloneEntryState)
                    .ToList()
                    ?? new List<LoginCharacterAccountEntryState>()
            };
        }

        private static List<CashShopStorageExpansionRecordState> CloneStorageExpansionHistory(IEnumerable<CashShopStorageExpansionRecordState> history)
        {
            return history?
                .Where(record => record != null)
                .Select(record => new CashShopStorageExpansionRecordState
                {
                    CashItemResultSubtype = Math.Max(0, record.CashItemResultSubtype),
                    CommoditySerialNumber = Math.Max(0, record.CommoditySerialNumber),
                    ResultSubtype = record.ResultSubtype,
                    FailureReason = Math.Max(0, record.FailureReason),
                    NxPrice = Math.Max(0L, record.NxPrice),
                    SlotLimitAfterResult = Math.Max(0, record.SlotLimitAfterResult),
                    IsPacketOwned = record.IsPacketOwned,
                    PacketType = Math.Max(0, record.PacketType),
                    CashAlreadySettled = record.CashAlreadySettled,
                    Message = record.Message ?? string.Empty,
                    AppliedAtUtc = record.AppliedAtUtc == default ? DateTime.UtcNow : record.AppliedAtUtc
                })
                .ToList()
                ?? new List<CashShopStorageExpansionRecordState>();
        }

        private static bool AreEquivalentPersistedStates(PersistedAccountState left, PersistedAccountState right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            return string.Equals(left.AccountName ?? string.Empty, right.AccountName ?? string.Empty, StringComparison.Ordinal) &&
                   left.AccountId == right.AccountId &&
                   Math.Max(0, left.WorldId) == Math.Max(0, right.WorldId) &&
                   Math.Max(0, left.SlotCount) == Math.Max(0, right.SlotCount) &&
                   Math.Max(0, left.BuyCharacterCount) == Math.Max(0, right.BuyCharacterCount) &&
                   CloneExtraCharInfoResultProfile(left.ExtraCharInfoResult)?.AccountId == CloneExtraCharInfoResultProfile(right.ExtraCharInfoResult)?.AccountId &&
                   CloneExtraCharInfoResultProfile(left.ExtraCharInfoResult)?.ResultFlag == CloneExtraCharInfoResultProfile(right.ExtraCharInfoResult)?.ResultFlag &&
                   CloneExtraCharInfoResultProfile(left.ExtraCharInfoResult)?.CanHaveExtraCharacter == CloneExtraCharInfoResultProfile(right.ExtraCharInfoResult)?.CanHaveExtraCharacter;
        }

        private static LoginExtraCharInfoResultProfile CloneExtraCharInfoResultProfile(LoginExtraCharInfoResultProfile profile)
        {
            if (profile == null)
            {
                return null;
            }

            return new LoginExtraCharInfoResultProfile
            {
                AccountId = profile.AccountId,
                ResultFlag = profile.ResultFlag,
                CanHaveExtraCharacter = profile.CanHaveExtraCharacter
            };
        }

        private static LoginExtraCharInfoResultProfile NormalizeExtraCharInfoResultProfile(
            LoginExtraCharInfoResultProfile profile,
            int? accountId)
        {
            LoginExtraCharInfoResultProfile clonedProfile = CloneExtraCharInfoResultProfile(profile);
            if (clonedProfile == null)
            {
                return null;
            }

            if (accountId.HasValue && accountId.Value > 0)
            {
                return new LoginExtraCharInfoResultProfile
                {
                    AccountId = accountId.Value,
                    ResultFlag = clonedProfile.ResultFlag,
                    CanHaveExtraCharacter = clonedProfile.CanHaveExtraCharacter
                };
            }

            return clonedProfile;
        }

        private static int NormalizeBuyCharacterCount(
            int buyCharacterCount,
            LoginExtraCharInfoResultProfile extraCharInfoResult,
            int? accountId)
        {
            int normalizedBuyCharacterCount = Math.Max(0, buyCharacterCount);
            if (!accountId.HasValue || accountId.Value <= 0)
            {
                return normalizedBuyCharacterCount;
            }

            if (!CanHaveExtraCharacter(extraCharInfoResult, accountId.Value))
            {
                return 0;
            }

            return Math.Clamp(normalizedBuyCharacterCount, 0, 1);
        }

        private static bool CanHaveExtraCharacter(
            LoginExtraCharInfoResultProfile extraCharInfoResult,
            int accountId)
        {
            return extraCharInfoResult != null &&
                   accountId > 0 &&
                   extraCharInfoResult.AccountId == accountId &&
                   extraCharInfoResult.ResultFlag == 0 &&
                   extraCharInfoResult.CanHaveExtraCharacter;
        }
    }
}
