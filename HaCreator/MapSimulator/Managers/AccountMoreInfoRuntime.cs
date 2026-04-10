using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using HaCreator.MapSimulator.Interaction;

namespace HaCreator.MapSimulator.Managers
{
    internal enum AccountMoreInfoEditableField
    {
        AreaGroup = 0,
        AreaDetail = 1,
        BirthYear = 2,
        BirthMonth = 3,
        BirthDay = 4,
    }

    internal sealed class AccountMoreInfoOwnerSnapshot
    {
        public bool IsOpen { get; init; }
        public bool IsFirstEntry { get; init; }
        public bool HasLoadedProfile { get; init; }
        public bool LoadPending { get; init; }
        public bool SavePending { get; init; }
        public int AreaGroup { get; init; }
        public int AreaDetail { get; init; }
        public string AreaGroupText { get; init; } = string.Empty;
        public string AreaDetailText { get; init; } = string.Empty;
        public int BirthYear { get; init; }
        public int BirthMonth { get; init; }
        public int BirthDay { get; init; }
        public IReadOnlyList<string> PlayStyleLabels { get; init; } = Array.Empty<string>();
        public IReadOnlyList<bool> PlayStyleSelections { get; init; } = Array.Empty<bool>();
        public IReadOnlyList<string> ActivityLabels { get; init; } = Array.Empty<string>();
        public IReadOnlyList<bool> ActivitySelections { get; init; } = Array.Empty<bool>();
        public string GenderStatusText { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public string LastDispatchText { get; init; } = string.Empty;
    }

        internal sealed class AccountMoreInfoRuntime
    {
        internal const int ClientOpcode = 193;
        internal const string CountryNameRootPath = "Etc/CountryName.img";
        private const int LoadResultPayloadLength = 17;
        private const int SaveResultPayloadLength = 2;
        private static readonly string[] PlayStyleLabels =
        {
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
        };

        private static readonly string[] ActivityLabels =
        {
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
        };

        private static readonly object CountryNameCatalogSync = new();
        private static bool _countryNameCatalogLoaded;
        private static IReadOnlyDictionary<int, string> _areaGroupDisplayNames = new Dictionary<int, string>();
        private static IReadOnlyDictionary<int, IReadOnlyDictionary<int, string>> _areaDetailDisplayNames = new Dictionary<int, IReadOnlyDictionary<int, string>>();

        private bool _isOpen;
        private bool _isFirstEntry;
        private bool _hasLoadedProfile;
        private bool _loadPending;
        private bool _savePending;
        private int _areaGroup;
        private int _areaDetail;
        private int _birthYear = GetDefaultBirthYear();
        private int _birthMonth = 1;
        private int _birthDay = 1;
        private uint _playStyleMask;
        private uint _activityMask;
        private byte? _lastGender;
        private int _lastGenderTick = int.MinValue;
        private string _statusText = "Account-more-info owner idle.";
        private string _lastDispatchText = string.Empty;

        internal bool IsOpen => _isOpen;

        internal void Open(bool firstEntry)
        {
            _isOpen = true;
            _isFirstEntry = firstEntry;
            _loadPending = true;
            _savePending = false;
            _statusText = firstEntry
                ? "CWvsContext::OnAccountMoreInfo subtype 0 opened UI owner 40 and queued a load request."
                : "Account-more-info owner reopened and queued a load request.";
            _lastDispatchText = string.Empty;
        }

        internal bool Close(string reason = null)
        {
            bool shouldShowFirstEntryCloseNotice = _isFirstEntry;
            _isOpen = false;
            _isFirstEntry = false;
            _loadPending = false;
            _savePending = false;
            if (!string.IsNullOrWhiteSpace(reason))
            {
                _statusText = reason.Trim();
            }

            return shouldShowFirstEntryCloseNotice;
        }

        internal void RecordDispatchStatus(string status)
        {
            _lastDispatchText = status?.Trim() ?? string.Empty;
        }

        internal void ApplySetGender(byte gender, int currentTick)
        {
            _lastGender = gender;
            _lastGenderTick = currentTick;
            _statusText = $"Applied adjacent CWvsContext::OnSetGender mutation with raw gender byte {gender}.";
        }

        internal byte[] BuildLoadRequestPayload()
        {
            _loadPending = true;
            return new byte[] { 1 };
        }

        internal byte[] BuildSaveRequestPayload()
        {
            byte[] payload = new byte[17];
            payload[0] = 3;
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(1, 4), (uint)(_areaGroup | (_areaDetail << 8)));
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(5, 4), (uint)(_birthYear * 10000 + (_birthMonth * 100) + _birthDay));
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(9, 4), _playStyleMask);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(13, 4), _activityMask);
            _savePending = true;
            _isFirstEntry = false;
            _statusText = "Queued an account-more-info save request and disabled further saves until server subtype 4 returns.";
            return payload;
        }

        internal bool TryApplyLoadResult(byte[] payload, out string message)
        {
            if (!_isOpen)
            {
                message = "Ignored account-more-info load result because the owner was not open.";
                return false;
            }

            if (payload == null || payload.Length < LoadResultPayloadLength)
            {
                message = "Account-more-info load result payload was shorter than the client-owned 16-byte body.";
                return false;
            }

            uint area = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(1, 4));
            uint birthday = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(5, 4));
            _playStyleMask = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(9, 4));
            _activityMask = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(13, 4));

            _areaGroup = (int)(area & 0xFF);
            _areaDetail = (int)((area >> 8) & 0xFF);
            _birthYear = ClampBirthYear((int)(birthday / 10000));
            _birthMonth = Math.Clamp((int)((birthday / 100) % 100), 1, 12);
            _birthDay = Math.Clamp((int)(birthday % 100), 1, DateTime.DaysInMonth(_birthYear, _birthMonth));
            _hasLoadedProfile = true;
            _loadPending = false;
            _savePending = false;
            _statusText = "Applied server subtype 2 load result onto the dedicated account-more-info owner.";
            message = $"Applied account-more-info load result: area=0x{area:X8}, birthday={birthday}, playMask=0x{_playStyleMask:X8}, activityMask=0x{_activityMask:X8}.";
            return true;
        }

        internal bool TryApplySaveResult(byte[] payload, out bool succeeded, out string message)
        {
            succeeded = false;
            if (!_isOpen)
            {
                message = "Ignored account-more-info save result because the owner was not open.";
                return false;
            }

            if (payload == null || payload.Length < SaveResultPayloadLength)
            {
                message = "Account-more-info save result payload was shorter than the client-owned success byte.";
                return false;
            }

            succeeded = payload[1] != 0;
            _savePending = false;
            if (succeeded)
            {
                Close("Account-more-info save completed and the client-owned owner closed.");
                message = "Applied account-more-info save result: success.";
                return true;
            }

            _statusText = "Server subtype 4 save result failed, so the owner stayed open and re-enabled saving.";
            message = "Applied account-more-info save result: failure.";
            return true;
        }

        internal void AdjustField(AccountMoreInfoEditableField field, int delta)
        {
            if (!_isOpen || delta == 0)
            {
                return;
            }

            switch (field)
            {
                case AccountMoreInfoEditableField.AreaGroup:
                    _areaGroup = Wrap(_areaGroup + delta, 0, 255);
                    break;

                case AccountMoreInfoEditableField.AreaDetail:
                    _areaDetail = Wrap(_areaDetail + delta, 0, 255);
                    break;

                case AccountMoreInfoEditableField.BirthYear:
                    _birthYear = Wrap(_birthYear + delta, GetMinimumBirthYear(), DateTime.Now.Year);
                    _birthDay = Math.Min(_birthDay, DateTime.DaysInMonth(_birthYear, _birthMonth));
                    break;

                case AccountMoreInfoEditableField.BirthMonth:
                    _birthMonth = Wrap(_birthMonth + delta, 1, 12);
                    _birthDay = Math.Min(_birthDay, DateTime.DaysInMonth(_birthYear, _birthMonth));
                    break;

                case AccountMoreInfoEditableField.BirthDay:
                    _birthDay = Wrap(_birthDay + delta, 1, DateTime.DaysInMonth(_birthYear, _birthMonth));
                    break;
            }

            _statusText = "Adjusted account-more-info draft state inside the dedicated owner.";
        }

        internal void TogglePlayStyle(int index)
        {
            if (!_isOpen || index < 0 || index >= PlayStyleLabels.Length)
            {
                return;
            }

            _playStyleMask ^= 1u << index;
            _statusText = $"Toggled play-style bit {index + 1}.";
        }

        internal void ToggleActivity(int index)
        {
            if (!_isOpen || index < 0 || index >= ActivityLabels.Length)
            {
                return;
            }

            _activityMask ^= 1u << index;
            _statusText = $"Toggled activity bit {index + 1}.";
        }

        internal AccountMoreInfoOwnerSnapshot BuildSnapshot()
        {
            return new AccountMoreInfoOwnerSnapshot
            {
                IsOpen = _isOpen,
                IsFirstEntry = _isFirstEntry,
                HasLoadedProfile = _hasLoadedProfile,
                LoadPending = _loadPending,
                SavePending = _savePending,
                AreaGroup = _areaGroup,
                AreaDetail = _areaDetail,
                AreaGroupText = ResolveAreaGroupComboText(_areaGroup),
                AreaDetailText = ResolveAreaDetailComboText(_areaGroup, _areaDetail),
                BirthYear = _birthYear,
                BirthMonth = _birthMonth,
                BirthDay = _birthDay,
                PlayStyleLabels = PlayStyleLabels,
                PlayStyleSelections = Enumerable.Range(0, PlayStyleLabels.Length)
                    .Select(index => ((_playStyleMask >> index) & 1u) != 0)
                    .ToArray(),
                ActivityLabels = ActivityLabels,
                ActivitySelections = Enumerable.Range(0, ActivityLabels.Length)
                    .Select(index => ((_activityMask >> index) & 1u) != 0)
                    .ToArray(),
                GenderStatusText = BuildGenderStatusText(),
                StatusText = _statusText,
                LastDispatchText = _lastDispatchText
            };
        }

        internal static bool TryResolveSubtype(byte[] payload, out byte subtype, out string error)
        {
            subtype = 0;
            error = null;
            if (payload == null || payload.Length == 0)
            {
                return true;
            }

            subtype = payload[0];
            return true;
        }

        internal static string ResolveAreaGroupComboText(int areaCode)
        {
            EnsureCountryNameCatalogLoaded();
            if (_areaGroupDisplayNames.TryGetValue(areaCode, out string displayName)
                && !string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            return ResolveRegionComboText(areaCode);
        }

        internal static string ResolveAreaDetailComboText(int areaGroup, int areaCode)
        {
            EnsureCountryNameCatalogLoaded();
            if (_areaDetailDisplayNames.TryGetValue(areaGroup, out IReadOnlyDictionary<int, string> detailDisplayNames)
                && detailDisplayNames != null
                && detailDisplayNames.TryGetValue(areaCode, out string displayName)
                && !string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            return ResolveRegionComboText(areaCode);
        }

        internal static string ResolveRegionComboText(int areaCode)
        {
            return areaCode == 0
                ? AccountMoreInfoOwnerStringPoolText.ResolveDefaultRegionItem()
                : FormatComboNumericValue(areaCode);
        }

        internal static string ResolveAreaDetailCountryNamePath(int areaGroup)
        {
            return $"{CountryNameRootPath}/{Math.Clamp(areaGroup, 0, 255)}";
        }

        internal static string FormatComboNumericValue(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        internal static IReadOnlyDictionary<int, string> BuildCountryNameLookup(IPropertyContainer propertyContainer)
        {
            Dictionary<int, string> lookup = new();
            if (propertyContainer?.WzProperties == null)
            {
                return lookup;
            }

            lookup[0] = AccountMoreInfoOwnerStringPoolText.ResolveDefaultRegionItem();
            foreach (WzImageProperty child in propertyContainer.WzProperties)
            {
                if (!TryGetCountryNameEntry(child, out int id, out string name))
                {
                    continue;
                }

                lookup[id] = name;
            }

            return lookup;
        }

        private static int Wrap(int value, int minInclusive, int maxInclusive)
        {
            if (minInclusive >= maxInclusive)
            {
                return minInclusive;
            }

            int span = (maxInclusive - minInclusive) + 1;
            int normalized = value - minInclusive;
            normalized %= span;
            if (normalized < 0)
            {
                normalized += span;
            }

            return minInclusive + normalized;
        }

        private static int GetMinimumBirthYear()
        {
            return DateTime.Now.Year - 100;
        }

        private static int GetDefaultBirthYear()
        {
            return DateTime.Now.Year - 15;
        }

        private static int ClampBirthYear(int year)
        {
            return Math.Clamp(year, GetMinimumBirthYear(), DateTime.Now.Year);
        }

        private static void EnsureCountryNameCatalogLoaded()
        {
            if (_countryNameCatalogLoaded)
            {
                return;
            }

            lock (CountryNameCatalogSync)
            {
                if (_countryNameCatalogLoaded)
                {
                    return;
                }

                Dictionary<int, string> areaGroups = new();
                Dictionary<int, IReadOnlyDictionary<int, string>> areaDetails = new();
                WzImage countryNameImage = HaCreator.Program.FindImage("Etc", "CountryName.img");
                if (countryNameImage != null)
                {
                    countryNameImage.ParseImage();
                    IReadOnlyDictionary<int, string> groupLookup = BuildCountryNameLookup(countryNameImage);
                    foreach (KeyValuePair<int, string> entry in groupLookup)
                    {
                        areaGroups[entry.Key] = entry.Value;
                    }

                    foreach (WzImageProperty child in countryNameImage.WzProperties)
                    {
                        if (child is not IPropertyContainer container
                            || !int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int areaGroup))
                        {
                            continue;
                        }

                        areaDetails[areaGroup] = BuildCountryNameLookup(container);
                    }
                }

                if (!areaGroups.ContainsKey(0))
                {
                    areaGroups[0] = AccountMoreInfoOwnerStringPoolText.ResolveDefaultRegionItem();
                }

                _areaGroupDisplayNames = areaGroups;
                _areaDetailDisplayNames = areaDetails;
                _countryNameCatalogLoaded = true;
            }
        }

        private static bool TryGetCountryNameEntry(WzImageProperty child, out int id, out string name)
        {
            id = 0;
            name = null;
            if (child is not IPropertyContainer container
                || !int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
            {
                return false;
            }

            if (container["name"] is not WzStringProperty nameProperty
                || string.IsNullOrWhiteSpace(nameProperty.Value))
            {
                return false;
            }

            name = nameProperty.Value;
            return true;
        }

        private string BuildGenderStatusText()
        {
            if (!_lastGender.HasValue)
            {
                return "No adjacent OnSetGender mutation has been observed yet.";
            }

            string genderText = _lastGender.Value switch
            {
                0 => "male",
                1 => "female",
                _ => $"raw={_lastGender.Value}"
            };

            return _lastGenderTick == int.MinValue
                ? $"Last adjacent OnSetGender mutation: {genderText}."
                : $"Last adjacent OnSetGender mutation: {genderText} at tick {_lastGenderTick}.";
        }
    }
}
