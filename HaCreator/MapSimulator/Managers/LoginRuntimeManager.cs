using System;
using System.Collections.Generic;
using System.Text;

namespace HaCreator.MapSimulator.Managers
{
    public enum LoginStep
    {
        Title = 0,
        WorldSelect = 1,
        CharacterSelect = 2,
        NewCharacter = 3,
        NewCharacterAvatar = 4,
        ViewAllCharacters = 5,
        EnteringField = 6,
    }

    public enum LoginPacketType
    {
        CheckPasswordResult = 0,
        GuestIdLoginResult = 1,
        AccountInfoResult = 2,
        CheckUserLimitResult = 3,
        SetAccountResult = 4,
        ConfirmEulaResult = 5,
        CheckPinCodeResult = 6,
        UpdatePinCodeResult = 7,
        ViewAllCharResult = 8,
        SelectCharacterByVacResult = 9,
        WorldInformation = 10,
        SelectWorldResult = 11,
        SelectCharacterResult = 12,
        CheckDuplicatedIdResult = 13,
        CreateNewCharacterResult = 14,
        DeleteCharacterResult = 15,
        EnableSpwResult = 21,
        LatestConnectedWorld = 24,
        RecommendWorldMessage = 25,
        ExtraCharInfoResult = 26,
        CheckSpwResult = 27,
    }

    /// <summary>
    /// Minimal login bootstrap runtime mirroring the client-owned login step machine.
    /// This is the pre-field owner that advances delayed step transitions and routes
    /// login-specific packet notifications into step changes.
    /// </summary>
    public class LoginRuntimeManager
    {
        public const int DefaultStepChangeDelayMs = 800;

        private readonly Dictionary<LoginPacketType, Action<int>> _packetHandlers;
        private readonly Dictionary<LoginPacketType, int> _packetCounts = new();

        public LoginRuntimeManager()
        {
            _packetHandlers = new Dictionary<LoginPacketType, Action<int>>
            {
                [LoginPacketType.CheckPasswordResult] = HandleCheckPasswordResult,
                [LoginPacketType.GuestIdLoginResult] = HandleGuestIdLoginResult,
                [LoginPacketType.AccountInfoResult] = HandleAccountInfoResult,
                [LoginPacketType.CheckUserLimitResult] = HandleCheckUserLimitResult,
                [LoginPacketType.SetAccountResult] = HandleSetAccountResult,
                [LoginPacketType.ConfirmEulaResult] = HandleConfirmEulaResult,
                [LoginPacketType.CheckPinCodeResult] = HandleCheckPinCodeResult,
                [LoginPacketType.UpdatePinCodeResult] = HandleUpdatePinCodeResult,
                [LoginPacketType.WorldInformation] = HandleWorldInformation,
                [LoginPacketType.SelectWorldResult] = HandleSelectWorldResult,
                [LoginPacketType.SelectCharacterResult] = HandleSelectCharacterResult,
                [LoginPacketType.ViewAllCharResult] = HandleViewAllCharResult,
                [LoginPacketType.SelectCharacterByVacResult] = HandleViewAllCharResult,
                [LoginPacketType.CheckDuplicatedIdResult] = HandleCheckDuplicatedIdResult,
                [LoginPacketType.CreateNewCharacterResult] = HandleCreateNewCharacterResult,
                [LoginPacketType.DeleteCharacterResult] = HandleDeleteCharacterResult,
                [LoginPacketType.EnableSpwResult] = HandleEnableSpwResult,
                [LoginPacketType.RecommendWorldMessage] = HandleRecommendWorldMessage,
                [LoginPacketType.LatestConnectedWorld] = HandleLatestConnectedWorld,
                [LoginPacketType.ExtraCharInfoResult] = HandleExtraCharInfoResult,
                [LoginPacketType.CheckSpwResult] = HandleCheckSpwResult,
            };
        }

        public LoginStep CurrentStep { get; private set; } = LoginStep.Title;
        public LoginStep BaseStep { get; private set; } = LoginStep.Title;
        public LoginStep? PendingStep { get; private set; }
        public int StepChangeRequestedAt { get; private set; } = int.MinValue;
        public int PendingStepDelayMs { get; private set; }
        public int StepChangeAt { get; private set; } = int.MinValue;
        public string PendingTransitionReason { get; private set; }
        public LoginPacketType? LastPacketType { get; private set; }
        public string LastEventSummary { get; private set; } = "Login runtime not initialized.";
        public bool HasWorldInformation { get; private set; }
        public bool CharacterSelectReady { get; private set; }
        public bool FieldEntryRequested { get; private set; }

        public bool BlocksFieldSimulation => !FieldEntryRequested;

        public void Initialize(int currentTickCount)
        {
            Reset();
            CurrentStep = LoginStep.Title;
            BaseStep = LoginStep.Title;
            LastEventSummary = "Initialized login runtime at title step.";
            ScheduleStepChange(LoginStep.Title, currentTickCount, 0, "Bootstrap");
            Update(currentTickCount);
        }

        public void Reset()
        {
            CurrentStep = LoginStep.Title;
            BaseStep = LoginStep.Title;
            PendingStep = null;
            StepChangeRequestedAt = int.MinValue;
            PendingStepDelayMs = 0;
            StepChangeAt = int.MinValue;
            PendingTransitionReason = null;
            LastPacketType = null;
            LastEventSummary = "Login runtime reset.";
            HasWorldInformation = false;
            CharacterSelectReady = false;
            FieldEntryRequested = false;
            _packetCounts.Clear();
        }

        public bool Update(int currentTickCount)
        {
            if (!PendingStep.HasValue || StepChangeAt == int.MinValue)
            {
                return false;
            }

            if (unchecked(currentTickCount - StepChangeAt) < 0)
            {
                return false;
            }

            CurrentStep = PendingStep.Value;
            if (CurrentStep != LoginStep.EnteringField)
            {
                BaseStep = CurrentStep;
            }

            string reason = PendingTransitionReason;
            PendingStep = null;
            StepChangeRequestedAt = int.MinValue;
            PendingStepDelayMs = 0;
            StepChangeAt = int.MinValue;
            PendingTransitionReason = null;
            LastEventSummary = string.IsNullOrWhiteSpace(reason)
                ? $"Advanced to {CurrentStep}."
                : $"{reason} -> {CurrentStep}.";
            return true;
        }

        public void ForceStep(LoginStep step, string reason = null)
        {
            CurrentStep = step;
            if (step != LoginStep.EnteringField)
            {
                BaseStep = step;
            }

            PendingStep = null;
            StepChangeRequestedAt = int.MinValue;
            PendingStepDelayMs = 0;
            StepChangeAt = int.MinValue;
            PendingTransitionReason = null;
            FieldEntryRequested = step == LoginStep.EnteringField;
            LastEventSummary = string.IsNullOrWhiteSpace(reason)
                ? $"Forced step to {step}."
                : $"{reason} -> {step}.";
        }

        public void ScheduleStepChange(LoginStep step, int currentTickCount, int delayMs, string reason = null)
        {
            int normalizedDelay = Math.Max(0, delayMs);
            PendingStep = step;
            StepChangeRequestedAt = currentTickCount;
            PendingStepDelayMs = normalizedDelay;
            StepChangeAt = currentTickCount + normalizedDelay;
            PendingTransitionReason = reason;
        }

        public bool CancelPendingStep(string reason = null)
        {
            if (!PendingStep.HasValue)
            {
                return false;
            }

            LoginStep cancelledStep = PendingStep.Value;
            PendingStep = null;
            StepChangeRequestedAt = int.MinValue;
            PendingStepDelayMs = 0;
            StepChangeAt = int.MinValue;
            PendingTransitionReason = null;
            LastEventSummary = string.IsNullOrWhiteSpace(reason)
                ? $"Cancelled pending transition to {cancelledStep}."
                : $"{reason} -> stayed on {CurrentStep}.";
            return true;
        }

        public bool TryDispatchPacket(LoginPacketType packetType, int currentTickCount, out string message)
        {
            LastPacketType = packetType;
            _packetCounts.TryGetValue(packetType, out int count);
            _packetCounts[packetType] = count + 1;

            if (_packetHandlers.TryGetValue(packetType, out Action<int> handler))
            {
                handler(currentTickCount);
                message = LastEventSummary;
                return true;
            }

            LastEventSummary = $"Routed {packetType} without a simulator-side state change.";
            message = LastEventSummary;
            return false;
        }

        public int GetPacketCount(LoginPacketType packetType)
        {
            return _packetCounts.TryGetValue(packetType, out int count) ? count : 0;
        }

        public void OverrideLastEventSummary(string summary)
        {
            if (!string.IsNullOrWhiteSpace(summary))
            {
                LastEventSummary = summary;
            }
        }

        public string DescribeStatus()
        {
            var builder = new StringBuilder();
            builder.Append("Step: ").Append(CurrentStep);
            builder.Append(" | Base: ").Append(BaseStep);
            builder.Append(" | Field entry: ").Append(FieldEntryRequested ? "requested" : "blocked");

            if (PendingStep.HasValue)
            {
                builder.AppendLine();
                builder.Append("Pending: ").Append(PendingStep.Value);
                builder.Append(" @ ").Append(StepChangeAt);
                if (!string.IsNullOrWhiteSpace(PendingTransitionReason))
                {
                    builder.Append(" | ").Append(PendingTransitionReason);
                }
            }

            builder.AppendLine();
            builder.Append("Last packet: ").Append(LastPacketType?.ToString() ?? "None");
            builder.Append(" | Worlds loaded: ").Append(HasWorldInformation ? "yes" : "no");
            builder.Append(" | Character select ready: ").Append(CharacterSelectReady ? "yes" : "no");
            builder.AppendLine();
            builder.Append("Last event: ").Append(LastEventSummary);
            return builder.ToString();
        }

        public static bool TryParseStep(string text, out LoginStep step)
        {
            step = LoginStep.Title;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string normalized = text.Trim().Replace("-", string.Empty).Replace("_", string.Empty);
            return normalized.ToLowerInvariant() switch
            {
                "title" or "login" => Assign(LoginStep.Title, out step),
                "world" or "worldselect" => Assign(LoginStep.WorldSelect, out step),
                "char" or "character" or "characterselect" => Assign(LoginStep.CharacterSelect, out step),
                "newchar" or "newcharacter" => Assign(LoginStep.NewCharacter, out step),
                "avatar" or "newcharacteravatar" or "newcharavatar" => Assign(LoginStep.NewCharacterAvatar, out step),
                "vac" or "viewallcharacters" or "viewall" => Assign(LoginStep.ViewAllCharacters, out step),
                "enter" or "enterfield" or "enteringfield" => Assign(LoginStep.EnteringField, out step),
                _ => Enum.TryParse(text, true, out step),
            };
        }

        public static bool TryParsePacketType(string text, out LoginPacketType packetType)
        {
            packetType = LoginPacketType.CheckPasswordResult;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string trimmed = text.Trim();
            int namespaceSeparatorIndex = Math.Max(trimmed.LastIndexOf("::", StringComparison.Ordinal), trimmed.LastIndexOf('.'));
            if (namespaceSeparatorIndex >= 0 && namespaceSeparatorIndex + 1 < trimmed.Length)
            {
                trimmed = trimmed[(namespaceSeparatorIndex + 1)..];
            }

            string normalized = trimmed.Replace("-", string.Empty).Replace("_", string.Empty);
            return normalized.ToLowerInvariant() switch
            {
                "checkpassword" => Assign(LoginPacketType.CheckPasswordResult, out packetType),
                "oncheckpasswordresult" => Assign(LoginPacketType.CheckPasswordResult, out packetType),
                "guestlogin" or "guestidlogin" => Assign(LoginPacketType.GuestIdLoginResult, out packetType),
                "onguestidloginresult" => Assign(LoginPacketType.GuestIdLoginResult, out packetType),
                "accountinfo" => Assign(LoginPacketType.AccountInfoResult, out packetType),
                "onaccountinforesult" => Assign(LoginPacketType.AccountInfoResult, out packetType),
                "checkuserlimit" or "userlimit" => Assign(LoginPacketType.CheckUserLimitResult, out packetType),
                "oncheckuserlimitresult" => Assign(LoginPacketType.CheckUserLimitResult, out packetType),
                "setaccount" => Assign(LoginPacketType.SetAccountResult, out packetType),
                "onsetaccountresult" => Assign(LoginPacketType.SetAccountResult, out packetType),
                "confirmeula" or "eula" => Assign(LoginPacketType.ConfirmEulaResult, out packetType),
                "onconfirmeularesult" => Assign(LoginPacketType.ConfirmEulaResult, out packetType),
                "checkpincode" or "checkpin" or "pic" => Assign(LoginPacketType.CheckPinCodeResult, out packetType),
                "oncheckpincoderesult" => Assign(LoginPacketType.CheckPinCodeResult, out packetType),
                "updatepincode" or "updatepin" or "updatepic" => Assign(LoginPacketType.UpdatePinCodeResult, out packetType),
                "onupdatepincoderesult" => Assign(LoginPacketType.UpdatePinCodeResult, out packetType),
                "worldinfo" or "worldinformation" => Assign(LoginPacketType.WorldInformation, out packetType),
                "onworldinformation" => Assign(LoginPacketType.WorldInformation, out packetType),
                "selectworld" => Assign(LoginPacketType.SelectWorldResult, out packetType),
                "onselectworldresult" => Assign(LoginPacketType.SelectWorldResult, out packetType),
                "selectchar" or "selectcharacter" => Assign(LoginPacketType.SelectCharacterResult, out packetType),
                "onselectcharacterresult" => Assign(LoginPacketType.SelectCharacterResult, out packetType),
                "checkduplicatedid" or "checkduplicateid" or "checkduplicate" or "checkdup" => Assign(LoginPacketType.CheckDuplicatedIdResult, out packetType),
                "oncheckduplicatedidresult" => Assign(LoginPacketType.CheckDuplicatedIdResult, out packetType),
                "newcharresult" or "createnewcharacter" or "createnewcharacterresult" => Assign(LoginPacketType.CreateNewCharacterResult, out packetType),
                "oncreatenewcharacterresult" => Assign(LoginPacketType.CreateNewCharacterResult, out packetType),
                "deletechar" or "deletecharacter" or "deletecharacterresult" => Assign(LoginPacketType.DeleteCharacterResult, out packetType),
                "ondeletecharacterresult" => Assign(LoginPacketType.DeleteCharacterResult, out packetType),
                "enablespw" => Assign(LoginPacketType.EnableSpwResult, out packetType),
                "onenablespwresult" => Assign(LoginPacketType.EnableSpwResult, out packetType),
                "viewallchar" or "viewallcharacters" or "vac" => Assign(LoginPacketType.ViewAllCharResult, out packetType),
                "onviewallcharresult" => Assign(LoginPacketType.ViewAllCharResult, out packetType),
                "recommendworld" => Assign(LoginPacketType.RecommendWorldMessage, out packetType),
                "onrecommendworldmessage" => Assign(LoginPacketType.RecommendWorldMessage, out packetType),
                "latestworld" or "latestconnectedworld" => Assign(LoginPacketType.LatestConnectedWorld, out packetType),
                "onlatestconnectedworld" => Assign(LoginPacketType.LatestConnectedWorld, out packetType),
                "extracharinfo" => Assign(LoginPacketType.ExtraCharInfoResult, out packetType),
                "onextracharinforesult" => Assign(LoginPacketType.ExtraCharInfoResult, out packetType),
                "checkspw" => Assign(LoginPacketType.CheckSpwResult, out packetType),
                "oncheckspwresult" => Assign(LoginPacketType.CheckSpwResult, out packetType),
                _ => Enum.TryParse(text, true, out packetType),
            };
        }

        private void HandleCheckPasswordResult(int currentTickCount)
        {
            ScheduleStepChange(LoginStep.WorldSelect, currentTickCount, DefaultStepChangeDelayMs, "CheckPasswordResult");
            LastEventSummary = "Received CheckPasswordResult and scheduled world-select transition.";
        }

        private void HandleGuestIdLoginResult(int currentTickCount)
        {
            LastEventSummary = "Received GuestIdLoginResult for the login bootstrap flow.";
        }

        private void HandleAccountInfoResult(int currentTickCount)
        {
            LastEventSummary = "Received AccountInfoResult for the login bootstrap flow.";
        }

        private void HandleWorldInformation(int currentTickCount)
        {
            HasWorldInformation = true;
            LastEventSummary = "Received WorldInformation and populated the world-selection state.";
        }

        private void HandleCheckUserLimitResult(int currentTickCount)
        {
            LastEventSummary = "Received CheckUserLimitResult and unlocked channel selection for the chosen world.";
        }

        private void HandleSetAccountResult(int currentTickCount)
        {
            LastEventSummary = "Received SetAccountResult and opened the account-migration or account-choice flow.";
        }

        private void HandleConfirmEulaResult(int currentTickCount)
        {
            LastEventSummary = "Received ConfirmEulaResult and opened the EULA confirmation flow.";
        }

        private void HandleCheckPinCodeResult(int currentTickCount)
        {
            LastEventSummary = "Received CheckPinCodeResult and opened the PIC verification flow.";
        }

        private void HandleUpdatePinCodeResult(int currentTickCount)
        {
            LastEventSummary = "Received UpdatePinCodeResult and opened the PIC setup flow.";
        }

        private void HandleSelectWorldResult(int currentTickCount)
        {
            CharacterSelectReady = true;
            ScheduleStepChange(LoginStep.CharacterSelect, currentTickCount, DefaultStepChangeDelayMs, "SelectWorldResult");
            LastEventSummary = "Received SelectWorldResult and scheduled character-select transition.";
        }

        private void HandleSelectCharacterResult(int currentTickCount)
        {
            FieldEntryRequested = true;
            ScheduleStepChange(LoginStep.EnteringField, currentTickCount, 0, "SelectCharacterResult");
            Update(currentTickCount);
        }

        private void HandleViewAllCharResult(int currentTickCount)
        {
            CharacterSelectReady = true;
            ScheduleStepChange(LoginStep.ViewAllCharacters, currentTickCount, DefaultStepChangeDelayMs, "ViewAllCharResult");
            LastEventSummary = "Received view-all-character data and scheduled the expanded roster step.";
        }

        private void HandleCheckDuplicatedIdResult(int currentTickCount)
        {
            LastEventSummary = "Received CheckDuplicatedIdResult for the login new-character flow.";
        }

        private void HandleCreateNewCharacterResult(int currentTickCount)
        {
            LastEventSummary = "Received CreateNewCharacterResult for the login bootstrap flow.";
        }

        private void HandleDeleteCharacterResult(int currentTickCount)
        {
            LastEventSummary = "Received DeleteCharacterResult for the login bootstrap flow.";
        }

        private void HandleEnableSpwResult(int currentTickCount)
        {
            LastEventSummary = "Received EnableSpwResult and opened the secondary-password setup choice.";
        }

        private void HandleRecommendWorldMessage(int currentTickCount)
        {
            LastEventSummary = "Received RecommendWorldMessage for the login bootstrap flow.";
        }

        private void HandleLatestConnectedWorld(int currentTickCount)
        {
            LastEventSummary = "Received LatestConnectedWorld for the login bootstrap flow.";
        }

        private void HandleExtraCharInfoResult(int currentTickCount)
        {
            LastEventSummary = "Received ExtraCharInfoResult for the login bootstrap flow.";
        }

        private void HandleCheckSpwResult(int currentTickCount)
        {
            LastEventSummary = "Received CheckSpwResult and opened the secondary-password verification flow.";
        }

        private static bool Assign(LoginStep value, out LoginStep step)
        {
            step = value;
            return true;
        }

        private static bool Assign(LoginPacketType value, out LoginPacketType packetType)
        {
            packetType = value;
            return true;
        }
    }
}
