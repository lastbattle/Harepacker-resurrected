using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

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
        public int BirthYear { get; init; }
        public int BirthMonth { get; init; }
        public int BirthDay { get; init; }
        public IReadOnlyList<string> PlayStyleLabels { get; init; } = Array.Empty<string>();
        public IReadOnlyList<bool> PlayStyleSelections { get; init; } = Array.Empty<bool>();
        public IReadOnlyList<string> ActivityLabels { get; init; } = Array.Empty<string>();
        public IReadOnlyList<bool> ActivitySelections { get; init; } = Array.Empty<bool>();
        public string StatusText { get; init; } = string.Empty;
        public string LastDispatchText { get; init; } = string.Empty;
    }

    internal sealed class AccountMoreInfoRuntime
    {
        internal const int ClientOpcode = 193;
        private const int LoadResultPayloadLength = 17;
        private const int SaveResultPayloadLength = 2;
        private const int DefaultBirthYear = 1990;

        private static readonly string[] PlayStyleLabels =
        {
            "Play style 1",
            "Play style 2",
            "Play style 3",
            "Play style 4",
            "Play style 5",
        };

        private static readonly string[] ActivityLabels =
        {
            "Activity 1",
            "Activity 2",
            "Activity 3",
            "Activity 4",
            "Activity 5",
            "Activity 6",
            "Activity 7",
            "Activity 8",
            "Activity 9",
            "Activity 10",
            "Activity 11",
            "Activity 12",
            "Activity 13",
            "Activity 14",
            "Activity 15",
            "Activity 16",
            "Activity 17",
            "Activity 18",
            "Activity 19",
        };

        private bool _isOpen;
        private bool _isFirstEntry;
        private bool _hasLoadedProfile;
        private bool _loadPending;
        private bool _savePending;
        private int _areaGroup;
        private int _areaDetail;
        private int _birthYear = DefaultBirthYear;
        private int _birthMonth = 1;
        private int _birthDay = 1;
        private uint _playStyleMask;
        private uint _activityMask;
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

        internal void Close(string reason = null)
        {
            _isOpen = false;
            _isFirstEntry = false;
            _loadPending = false;
            _savePending = false;
            if (!string.IsNullOrWhiteSpace(reason))
            {
                _statusText = reason.Trim();
            }
        }

        internal void RecordDispatchStatus(string status)
        {
            _lastDispatchText = status?.Trim() ?? string.Empty;
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
            _statusText = "Queued an account-more-info save request and disabled further saves until subtype 3 returns.";
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
            _birthYear = Math.Max(1900, (int)(birthday / 10000));
            _birthMonth = Math.Clamp((int)((birthday / 100) % 100), 1, 12);
            _birthDay = Math.Clamp((int)(birthday % 100), 1, DateTime.DaysInMonth(_birthYear, _birthMonth));
            _hasLoadedProfile = true;
            _loadPending = false;
            _savePending = false;
            _statusText = "Applied subtype 1 load result onto the dedicated account-more-info owner.";
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

            _statusText = "Subtype 3 save result failed, so the owner stayed open and re-enabled saving.";
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
                    _birthYear = Wrap(_birthYear + delta, 1900, 2099);
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
    }
}
