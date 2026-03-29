using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.Managers;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class SimulatorStorageRuntime : IStorageRuntime
    {
        private const int DefaultSlotLimit = 24;
        private const int MinSlotLimit = 24;
        private const int MaxSlotLimit = 96;
        private const int SlotExpansionStep = 4;

        private readonly Dictionary<InventoryType, List<InventorySlotData>> _storageItems = new()
        {
            { InventoryType.EQUIP, new List<InventorySlotData>() },
            { InventoryType.USE, new List<InventorySlotData>() },
            { InventoryType.SETUP, new List<InventorySlotData>() },
            { InventoryType.ETC, new List<InventorySlotData>() },
            { InventoryType.CASH, new List<InventorySlotData>() }
        };
        private readonly List<string> _sharedCharacterNames = new();
        private readonly HashSet<string> _sharedCharacterLookup = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _authorizedCharacterNames = new();
        private readonly HashSet<string> _authorizedCharacterLookup = new(StringComparer.OrdinalIgnoreCase);
        private readonly StorageAccountStore _accountStore;
        private static readonly Regex SecondaryPasswordPattern = new("^[0-9]{4,8}$", RegexOptions.Compiled);

        private int _slotLimit = DefaultSlotLimit;
        private long _meso;
        private string _currentAccountKey = StorageAccountStore.ResolveAccountKey("Simulator Account Storage");
        private string _loginAccountPicCode = string.Empty;
        private bool _loginAccountPicVerified;
        private bool _loginAccountSpwEnabled;
        private string _loginAccountSecondaryPassword = string.Empty;
        private bool _loginAccountSecondaryPasswordVerified;
        private string _secondaryPassword = string.Empty;
        private bool _secondaryPasswordVerified;
        private bool _isAccessSessionActive;
        private bool _suspendPersistence;

        public string AccountLabel { get; private set; } = "Simulator Account Storage";
        public string CurrentCharacterName { get; private set; } = string.Empty;
        public IReadOnlyList<string> SharedCharacterNames => _sharedCharacterNames;
        public IReadOnlyList<string> AuthorizedCharacterNames => _authorizedCharacterNames;

        public bool CanCurrentCharacterAccess =>
            _authorizedCharacterNames.Count == 0
                ? _sharedCharacterNames.Count == 0
                  || (!string.IsNullOrWhiteSpace(CurrentCharacterName) && _sharedCharacterLookup.Contains(CurrentCharacterName))
                : !string.IsNullOrWhiteSpace(CurrentCharacterName) && _authorizedCharacterLookup.Contains(CurrentCharacterName);
        public bool IsAccessSessionActive => _isAccessSessionActive;
        public bool HasAccountPic => !string.IsNullOrWhiteSpace(_loginAccountPicCode);
        public bool IsAccountPicVerified => !HasAccountPic || (_isAccessSessionActive && _loginAccountPicVerified);
        public bool HasAccountSecondaryPassword => _loginAccountSpwEnabled && !string.IsNullOrWhiteSpace(_loginAccountSecondaryPassword);
        public bool IsAccountSecondaryPasswordVerified => !HasAccountSecondaryPassword || (_isAccessSessionActive && _loginAccountSecondaryPasswordVerified);
        public bool RequiresClientAccountAuthority => HasAccountPic || HasAccountSecondaryPassword;
        public bool IsClientAccountAuthorityVerified => IsAccountPicVerified && IsAccountSecondaryPasswordVerified;
        public bool HasSecondaryPassword => !string.IsNullOrWhiteSpace(_secondaryPassword);
        public bool IsSecondaryPasswordVerified => !HasSecondaryPassword || (_isAccessSessionActive && _secondaryPasswordVerified);

        public SimulatorStorageRuntime(StorageAccountStore accountStore = null, string initialAccountLabel = null, string initialAccountKey = null)
        {
            _accountStore = accountStore ?? new StorageAccountStore();
            LoadAccountState(
                string.IsNullOrWhiteSpace(initialAccountLabel) ? AccountLabel : initialAccountLabel,
                initialAccountKey);
        }

        public IReadOnlyList<InventorySlotData> GetSlots(InventoryType type)
        {
            return _storageItems.TryGetValue(type, out List<InventorySlotData> rows)
                ? rows
                : Array.Empty<InventorySlotData>();
        }

        public int GetSlotLimit()
        {
            return _slotLimit;
        }

        public void SetSlotLimit(int slotLimit)
        {
            _slotLimit = Math.Clamp(slotLimit, MinSlotLimit, MaxSlotLimit);
            PersistCurrentState();
        }

        public int GetUsedSlotCount()
        {
            int total = 0;
            foreach (List<InventorySlotData> rows in _storageItems.Values)
            {
                total += rows.Count;
            }

            return total;
        }

        public bool CanExpandSlotLimit(int amount = SlotExpansionStep)
        {
            int normalizedAmount = NormalizeSlotExpansionAmount(amount);
            return _slotLimit + normalizedAmount <= MaxSlotLimit;
        }

        public bool TryExpandSlotLimit(int amount = SlotExpansionStep)
        {
            if (!CanExpandSlotLimit(amount))
            {
                return false;
            }

            _slotLimit += NormalizeSlotExpansionAmount(amount);
            PersistCurrentState();
            return true;
        }

        public long GetMesoCount()
        {
            return _meso;
        }

        public void SetMeso(long amount)
        {
            _meso = Math.Max(0, amount);
            PersistCurrentState();
        }

        public void AddItem(InventoryType type, InventorySlotData slotData)
        {
            if (type == InventoryType.NONE || slotData == null || !_storageItems.TryGetValue(type, out List<InventorySlotData> rows))
            {
                return;
            }

            InventorySlotData source = slotData.Clone();
            int maxStackSize = InventoryItemMetadataResolver.ResolveMaxStack(type, source.MaxStackSize);
            int remainingQuantity = Math.Max(1, source.Quantity);
            if (IsStackable(type, maxStackSize))
            {
                remainingQuantity = FillExistingStacks(rows, source, remainingQuantity, maxStackSize);
            }

            while (remainingQuantity > 0 && GetUsedSlotCount() < _slotLimit)
            {
                InventorySlotData stack = source.Clone();
                stack.Quantity = IsStackable(type, maxStackSize)
                    ? Math.Min(remainingQuantity, maxStackSize)
                    : 1;
                stack.MaxStackSize = maxStackSize;
                rows.Add(stack);
                remainingQuantity -= stack.Quantity;
            }

            PersistCurrentState();
        }

        public bool CanAcceptItem(InventoryType type, InventorySlotData slotData)
        {
            if (slotData == null || !_storageItems.TryGetValue(type, out List<InventorySlotData> rows))
            {
                return false;
            }

            int remainingQuantity = Math.Max(1, slotData.Quantity);
            int maxStackSize = InventoryItemMetadataResolver.ResolveMaxStack(type, slotData.MaxStackSize);
            if (IsStackable(type, maxStackSize))
            {
                foreach (InventorySlotData row in rows)
                {
                    if (row == null || row.ItemId != slotData.ItemId || row.IsDisabled)
                    {
                        continue;
                    }

                    int rowMax = InventoryItemMetadataResolver.ResolveMaxStack(type, row.MaxStackSize);
                    int capacity = rowMax - Math.Max(1, row.Quantity);
                    if (capacity <= 0)
                    {
                        continue;
                    }

                    remainingQuantity -= capacity;
                    if (remainingQuantity <= 0)
                    {
                        return true;
                    }
                }
            }

            int freeSlots = Math.Max(0, _slotLimit - GetUsedSlotCount());
            if (!IsStackable(type, maxStackSize))
            {
                return freeSlots >= remainingQuantity;
            }

            int neededStacks = (remainingQuantity + maxStackSize - 1) / maxStackSize;
            return freeSlots >= neededStacks;
        }

        public bool TryRemoveSlotAt(InventoryType type, int slotIndex, out InventorySlotData slotData)
        {
            slotData = null;
            if (!_storageItems.TryGetValue(type, out List<InventorySlotData> rows) ||
                slotIndex < 0 ||
                slotIndex >= rows.Count)
            {
                return false;
            }

            slotData = rows[slotIndex]?.Clone();
            rows.RemoveAt(slotIndex);
            PersistCurrentState();
            return slotData != null;
        }

        public void SortSlots(InventoryType type)
        {
            if (_storageItems.TryGetValue(type, out List<InventorySlotData> rows))
            {
                rows.Sort(CompareSlots);
                PersistCurrentState();
            }
        }

        public void BeginAccessSession()
        {
            _isAccessSessionActive = true;
            _loginAccountPicVerified = false;
            _loginAccountSecondaryPasswordVerified = false;
            _secondaryPasswordVerified = false;
        }

        public void EndAccessSession()
        {
            _isAccessSessionActive = false;
            _loginAccountPicVerified = false;
            _loginAccountSecondaryPasswordVerified = false;
            _secondaryPasswordVerified = false;
        }

        public void ConfigureAccess(string accountLabel, string accountKey, string currentCharacterName, IEnumerable<string> sharedCharacterNames)
        {
            string normalizedLabel = string.IsNullOrWhiteSpace(accountLabel) ? "Simulator Account Storage" : accountLabel.Trim();
            string nextAccountKey = ResolveConfiguredAccountKey(normalizedLabel, accountKey);
            if (!string.Equals(_currentAccountKey, nextAccountKey, StringComparison.Ordinal))
            {
                LoadAccountState(normalizedLabel, nextAccountKey);
            }
            else
            {
                AccountLabel = normalizedLabel;
            }

            CurrentCharacterName = currentCharacterName ?? string.Empty;
            EndAccessSession();

            _sharedCharacterNames.Clear();
            _sharedCharacterLookup.Clear();
            foreach (string characterName in sharedCharacterNames ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(characterName) || !_sharedCharacterLookup.Add(characterName))
                {
                    continue;
                }

                _sharedCharacterNames.Add(characterName);
            }

            ReconcileAuthorizedCharacters();
        }

        public void ConfigureLoginAccountSecurity(string picCode, bool secondaryPasswordEnabled, string secondaryPassword)
        {
            _loginAccountPicCode = NormalizeAccountSecret(picCode);
            _loginAccountSpwEnabled = secondaryPasswordEnabled;
            _loginAccountSecondaryPassword = NormalizeAccountSecret(secondaryPassword);
            _loginAccountPicVerified = false;
            _loginAccountSecondaryPasswordVerified = false;
        }

        public bool TryVerifyAccountPic(string password)
        {
            if (!CanCurrentCharacterAccess || !_isAccessSessionActive)
            {
                _loginAccountPicVerified = false;
                return false;
            }

            if (!HasAccountPic)
            {
                _loginAccountPicVerified = true;
                return true;
            }

            _loginAccountPicVerified = string.Equals(_loginAccountPicCode, NormalizeAccountSecret(password), StringComparison.Ordinal);
            return _loginAccountPicVerified;
        }

        public bool TryVerifyAccountSecondaryPassword(string password)
        {
            if (!CanCurrentCharacterAccess || !_isAccessSessionActive)
            {
                _loginAccountSecondaryPasswordVerified = false;
                return false;
            }

            if (!HasAccountSecondaryPassword)
            {
                _loginAccountSecondaryPasswordVerified = true;
                return true;
            }

            _loginAccountSecondaryPasswordVerified = string.Equals(
                _loginAccountSecondaryPassword,
                NormalizeAccountSecret(password),
                StringComparison.Ordinal);
            return _loginAccountSecondaryPasswordVerified;
        }

        public bool TrySetSecondaryPassword(string password)
        {
            if (!CanCurrentCharacterAccess || !_isAccessSessionActive)
            {
                _secondaryPasswordVerified = false;
                return false;
            }

            string normalizedPassword = NormalizeSecondaryPassword(password);
            if (!IsValidSecondaryPassword(normalizedPassword))
            {
                return false;
            }

            _secondaryPassword = normalizedPassword;
            _secondaryPasswordVerified = true;
            PersistCurrentState();
            return true;
        }

        public bool TryVerifySecondaryPassword(string password)
        {
            if (!CanCurrentCharacterAccess || !_isAccessSessionActive)
            {
                _secondaryPasswordVerified = false;
                return false;
            }

            if (!HasSecondaryPassword)
            {
                _secondaryPasswordVerified = true;
                return true;
            }

            _secondaryPasswordVerified = string.Equals(_secondaryPassword, NormalizeSecondaryPassword(password), StringComparison.Ordinal);
            return _secondaryPasswordVerified;
        }

        public void ClearSecondaryPasswordVerification()
        {
            _secondaryPasswordVerified = false;
        }

        private void LoadAccountState(string accountLabel, string accountKey = null)
        {
            string normalizedLabel = string.IsNullOrWhiteSpace(accountLabel) ? "Simulator Account Storage" : accountLabel.Trim();
            string nextAccountKey = ResolveConfiguredAccountKey(normalizedLabel, accountKey);
            string legacyAccountKey = StorageAccountStore.ResolveAccountKey(normalizedLabel);
            StorageAccountStore.StorageAccountState state = _accountStore?.GetStateByKey(nextAccountKey);
            bool migratedLegacyState = false;
            if (state == null && !string.Equals(nextAccountKey, legacyAccountKey, StringComparison.Ordinal))
            {
                state = _accountStore?.GetStateByKey(legacyAccountKey);
                migratedLegacyState = state != null;
            }

            _suspendPersistence = true;
            try
            {
                AccountLabel = normalizedLabel;
                _currentAccountKey = nextAccountKey;
                _slotLimit = DefaultSlotLimit;
                _meso = 0;
                _secondaryPassword = string.Empty;
                _secondaryPasswordVerified = false;
                _isAccessSessionActive = false;
                _authorizedCharacterNames.Clear();
                _authorizedCharacterLookup.Clear();

                foreach (List<InventorySlotData> rows in _storageItems.Values)
                {
                    rows.Clear();
                }

                if (state == null)
                {
                    return;
                }

                AccountLabel = string.IsNullOrWhiteSpace(state.AccountLabel) ? normalizedLabel : state.AccountLabel;
                _slotLimit = Math.Clamp(state.SlotLimit, MinSlotLimit, MaxSlotLimit);
                _meso = Math.Max(0, state.Meso);
                _secondaryPassword = NormalizeSecondaryPassword(state.SecondaryPassword);
                foreach (string authorizedCharacterName in state.AuthorizedCharacterNames ?? Array.Empty<string>())
                {
                    AddAuthorizedCharacter(authorizedCharacterName);
                }

                foreach (KeyValuePair<InventoryType, List<InventorySlotData>> entry in state.ItemsByType)
                {
                    foreach (InventorySlotData slot in entry.Value ?? new List<InventorySlotData>())
                    {
                        AddItem(entry.Key, slot);
                    }
                }
            }
            finally
            {
                _suspendPersistence = false;
            }

            if (migratedLegacyState)
            {
                PersistCurrentState();
            }
        }

        private void PersistCurrentState()
        {
            if (_suspendPersistence || _accountStore == null)
            {
                return;
            }

            Dictionary<InventoryType, List<InventorySlotData>> snapshot = new();
            foreach (KeyValuePair<InventoryType, List<InventorySlotData>> entry in _storageItems)
            {
                List<InventorySlotData> rows = new(entry.Value.Count);
                foreach (InventorySlotData slot in entry.Value)
                {
                    if (slot != null)
                    {
                        rows.Add(slot.Clone());
                    }
                }

                snapshot[entry.Key] = rows;
            }

            _accountStore.SaveState(_currentAccountKey, AccountLabel, _slotLimit, _meso, snapshot, _authorizedCharacterNames, _secondaryPassword);
        }

        private void ReconcileAuthorizedCharacters()
        {
            bool changed = false;
            if (_authorizedCharacterNames.Count == 0)
            {
                IEnumerable<string> bootstrapNames = _sharedCharacterNames.Count > 0
                    ? _sharedCharacterNames
                    : string.IsNullOrWhiteSpace(CurrentCharacterName)
                        ? Array.Empty<string>()
                        : new[] { CurrentCharacterName };

                foreach (string bootstrapName in bootstrapNames)
                {
                    changed |= AddAuthorizedCharacter(bootstrapName);
                }
            }
            else if (!string.IsNullOrWhiteSpace(CurrentCharacterName) && _authorizedCharacterLookup.Contains(CurrentCharacterName))
            {
                foreach (string sharedCharacterName in _sharedCharacterNames)
                {
                    changed |= AddAuthorizedCharacter(sharedCharacterName);
                }
            }

            if (changed)
            {
                PersistCurrentState();
            }
        }

        private bool AddAuthorizedCharacter(string characterName)
        {
            string normalizedName = characterName?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName) || !_authorizedCharacterLookup.Add(normalizedName))
            {
                return false;
            }

            _authorizedCharacterNames.Add(normalizedName);
            return true;
        }

        private static string ResolveConfiguredAccountKey(string accountLabel, string accountKey)
        {
            return string.IsNullOrWhiteSpace(accountKey)
                ? StorageAccountStore.ResolveAccountKey(accountLabel)
                : accountKey.Trim();
        }

        private static int NormalizeSlotExpansionAmount(int amount)
        {
            if (amount <= 0)
            {
                return SlotExpansionStep;
            }

            int remainder = amount % SlotExpansionStep;
            return remainder == 0 ? amount : amount + (SlotExpansionStep - remainder);
        }

        private static bool IsStackable(InventoryType type, int maxStackSize)
        {
            return type != InventoryType.EQUIP && maxStackSize > 1;
        }

        private static string NormalizeAccountSecret(string password)
        {
            return string.IsNullOrWhiteSpace(password) ? string.Empty : password.Trim();
        }

        private static string NormalizeSecondaryPassword(string password)
        {
            return string.IsNullOrWhiteSpace(password) ? string.Empty : password.Trim();
        }

        private static bool IsValidSecondaryPassword(string password)
        {
            return SecondaryPasswordPattern.IsMatch(password ?? string.Empty);
        }

        private static int FillExistingStacks(List<InventorySlotData> rows, InventorySlotData slotData, int remainingQuantity, int maxStackSize)
        {
            for (int i = 0; i < rows.Count && remainingQuantity > 0; i++)
            {
                InventorySlotData existing = rows[i];
                if (existing == null || existing.ItemId != slotData.ItemId || existing.IsDisabled)
                {
                    continue;
                }

                int rowMax = InventoryItemMetadataResolver.ResolveMaxStack(
                    InventoryItemMetadataResolver.ResolveInventoryType(existing.ItemId),
                    existing.MaxStackSize);
                int capacity = rowMax - Math.Max(1, existing.Quantity);
                if (capacity <= 0)
                {
                    continue;
                }

                int quantityToMerge = Math.Min(capacity, remainingQuantity);
                existing.Quantity += quantityToMerge;
                existing.MaxStackSize = maxStackSize;
                existing.ItemTexture ??= slotData.ItemTexture;
                existing.ItemName ??= slotData.ItemName;
                existing.ItemTypeName ??= slotData.ItemTypeName;
                existing.Description ??= slotData.Description;
                existing.TooltipPart ??= slotData.TooltipPart?.Clone();
                existing.GradeFrameIndex ??= slotData.GradeFrameIndex;
                existing.IsActiveBullet |= slotData.IsActiveBullet;
                remainingQuantity -= quantityToMerge;
            }

            return remainingQuantity;
        }

        private static int CompareSlots(InventorySlotData left, InventorySlotData right)
        {
            int leftId = left?.ItemId ?? int.MaxValue;
            int rightId = right?.ItemId ?? int.MaxValue;
            int idComparison = leftId.CompareTo(rightId);
            if (idComparison != 0)
            {
                return idComparison;
            }

            int rightQuantity = right?.Quantity ?? 0;
            int leftQuantity = left?.Quantity ?? 0;
            return rightQuantity.CompareTo(leftQuantity);
        }
    }
}
