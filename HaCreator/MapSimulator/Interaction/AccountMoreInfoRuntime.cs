using HaCreator.MapSimulator.Character;
using System;
using System.Globalization;

namespace HaCreator.MapSimulator.Interaction
{
    internal readonly record struct AccountMoreInfoSelection(
        byte AreaCode0,
        byte AreaCode1,
        int BirthYear,
        int BirthMonth,
        int BirthDay,
        uint PlayStyleMask,
        uint ActivityMask)
    {
        public ushort PackedAreaCode => (ushort)(AreaCode0 | (AreaCode1 << 8));

        public int BirthDateValue => (BirthYear * 10000) + (BirthMonth * 100) + BirthDay;

        public string Describe()
        {
            return $"Area {AreaCode0}/{AreaCode1}, birthday {BirthYear:D4}-{BirthMonth:D2}-{BirthDay:D2}, play 0x{PlayStyleMask:X2}, activity 0x{ActivityMask:X5}";
        }
    }

    internal sealed class AccountMoreInfoRuntime
    {
        internal const int PlayStyleCount = 5;
        internal const int ActivityCount = 19;
        private const int DefaultBirthYear = 1990;

        public bool IsOpen { get; private set; }
        public bool IsFirstEntry { get; private set; }
        public bool IsSavePending { get; private set; }
        public int LastOpenTick { get; private set; } = int.MinValue;
        public int LastLoadTick { get; private set; } = int.MinValue;
        public int LastSaveTick { get; private set; } = int.MinValue;
        public int LastSetGenderTick { get; private set; } = int.MinValue;
        public byte? LastAppliedGender { get; private set; }
        public bool? LastSaveSucceeded { get; private set; }
        public string LastStatus { get; private set; } = "Account-more-info owner idle.";

        private AccountMoreInfoSelection _selection = CreateDefaultSelection();

        public AccountMoreInfoSelection Selection => _selection;

        public static AccountMoreInfoSelection CreateDefaultSelection()
        {
            return new AccountMoreInfoSelection(
                AreaCode0: 0,
                AreaCode1: 0,
                BirthYear: DefaultBirthYear,
                BirthMonth: 1,
                BirthDay: 1,
                PlayStyleMask: 0,
                ActivityMask: 0);
        }

        public void OpenFromContext(int currentTick, bool firstEntry)
        {
            IsOpen = true;
            IsFirstEntry = firstEntry;
            IsSavePending = false;
            LastOpenTick = currentTick;
            LastSaveSucceeded = null;
            LastStatus = firstEntry
                ? "Opened the dedicated account-more-info owner through the first-entry context path."
                : "Opened the dedicated account-more-info owner.";
        }

        public void ApplyLoadResult(AccountMoreInfoSelection selection, int currentTick)
        {
            _selection = Normalize(selection);
            IsOpen = true;
            IsSavePending = false;
            LastLoadTick = currentTick;
            LastSaveSucceeded = null;
            LastStatus = $"Loaded account-more-info result into the dedicated owner: {_selection.Describe()}.";
        }

        public void BeginSave(AccountMoreInfoSelection selection, int currentTick)
        {
            _selection = Normalize(selection);
            IsOpen = true;
            IsFirstEntry = false;
            IsSavePending = true;
            LastSaveTick = currentTick;
            LastSaveSucceeded = null;
            LastStatus = $"Queued account-more-info save request from the dedicated owner: {_selection.Describe()}.";
        }

        public void ApplySaveResult(bool succeeded, int currentTick)
        {
            IsSavePending = false;
            LastSaveTick = currentTick;
            LastSaveSucceeded = succeeded;
            if (succeeded)
            {
                IsOpen = false;
                LastStatus = "Account-more-info save succeeded and the dedicated owner closed.";
                return;
            }

            IsOpen = true;
            LastStatus = "Account-more-info save failed and the dedicated owner remained open.";
        }

        public void ApplySetGender(byte gender, int currentTick)
        {
            LastAppliedGender = gender;
            LastSetGenderTick = currentTick;
            LastStatus = $"Applied adjacent OnSetGender context state mutation to {(gender == (byte)CharacterGender.Female ? "female" : "male")}.";
        }

        public void Close(string reason = null)
        {
            IsOpen = false;
            IsSavePending = false;
            LastStatus = string.IsNullOrWhiteSpace(reason)
                ? "Closed the dedicated account-more-info owner."
                : reason.Trim();
        }

        public void Reset()
        {
            IsOpen = false;
            IsFirstEntry = false;
            IsSavePending = false;
            LastOpenTick = int.MinValue;
            LastLoadTick = int.MinValue;
            LastSaveTick = int.MinValue;
            LastSetGenderTick = int.MinValue;
            LastAppliedGender = null;
            LastSaveSucceeded = null;
            _selection = CreateDefaultSelection();
            LastStatus = "Account-more-info owner idle.";
        }

        public string DescribeStatus()
        {
            string ownerState = IsOpen ? "open" : "closed";
            string firstEntryState = IsFirstEntry ? "first-entry" : "regular";
            string saveState = IsSavePending
                ? "save pending"
                : LastSaveSucceeded.HasValue
                    ? LastSaveSucceeded.Value ? "last save succeeded" : "last save failed"
                    : "no save result";
            string genderState = LastAppliedGender.HasValue
                ? $"last gender {(LastAppliedGender.Value == (byte)CharacterGender.Female ? "female" : "male")}@{LastSetGenderTick.ToString(CultureInfo.InvariantCulture)}"
                : "no set-gender mutation";
            return $"Account-more-info owner: {ownerState}, {firstEntryState}, {saveState}, {genderState}. {LastStatus}";
        }

        private static AccountMoreInfoSelection Normalize(AccountMoreInfoSelection selection)
        {
            int year = Math.Clamp(selection.BirthYear, 1900, DateTime.UtcNow.Year);
            int month = Math.Clamp(selection.BirthMonth, 1, 12);
            int maxDay = DateTime.DaysInMonth(Math.Max(1900, year), month);
            int day = Math.Clamp(selection.BirthDay, 1, maxDay);
            uint playMask = selection.PlayStyleMask & ((1u << PlayStyleCount) - 1u);
            uint activityMask = selection.ActivityMask & ((1u << ActivityCount) - 1u);
            return new AccountMoreInfoSelection(
                selection.AreaCode0,
                selection.AreaCode1,
                year,
                month,
                day,
                playMask,
                activityMask);
        }
    }
}
