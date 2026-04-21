using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const string RankingServerHost = "gamerank.maplestory";
        private const int RankingStringPoolUrlTemplateId = 0xAA2;
        private const int RankingOwnerNavigateDelayMs = 250;
        private const int EventAlarmOwnerMaxVisibleLines = 3;
        private const int EventAlarmFeedLifetimeMs = 8000;
        private const int EventEntrySortPriorityPrimary = 0;
        private const int EventEntrySortPriorityRuntime = 1;
        private const int EventEntrySortPrioritySecondary = 2;
        private const int EventEntrySortPriorityBootstrap = 3;
        private const int EventEntrySortPriorityFallback = 4;
        private int _lastRankingOpenTick = int.MinValue;
        private int _lastRankingNavigateTick = int.MinValue;
        private string _lastRankingLaunchSource = string.Empty;
        private int _lastEventOpenTick = int.MinValue;
        private string _lastEventLaunchSource = string.Empty;
        private bool _eventAlarmOwnerAutoShowConsumed;

        private void RecordProgressionUtilityOwnerLaunch(string windowName, string source)
        {
            if (string.IsNullOrWhiteSpace(windowName))
            {
                return;
            }

            string normalizedSource = string.IsNullOrWhiteSpace(source) ? "simulator" : source.Trim();
            int now = Environment.TickCount;
            if (string.Equals(windowName, MapSimulatorWindowNames.Ranking, StringComparison.Ordinal))
            {
                _lastRankingOpenTick = now;
                _lastRankingNavigateTick = int.MinValue;
                _lastRankingLaunchSource = normalizedSource;
            }
            else if (string.Equals(windowName, MapSimulatorWindowNames.Event, StringComparison.Ordinal))
            {
                _lastEventOpenTick = now;
                _lastEventLaunchSource = normalizedSource;
            }
        }

        private bool TryShowRecordedProgressionUtilityWindow(string windowName, string source, bool allowVisibleReset = true)
        {
            if (uiWindowManager?.GetWindow(windowName) is not { } window)
            {
                return false;
            }

            if (!allowVisibleReset && window.IsVisible)
            {
                return false;
            }

            RecordProgressionUtilityOwnerLaunch(windowName, source);
            ShowWindow(windowName, window, trackDirectionModeOwner: ShouldTrackInheritedDirectionModeOwner());
            return true;
        }

        private void TryAutoShowEventAlarmOwner(string source)
        {
            if (_eventAlarmOwnerAutoShowConsumed)
            {
                return;
            }

            if (TryShowRecordedProgressionUtilityWindow(
                MapSimulatorWindowNames.Event,
                source,
                allowVisibleReset: false))
            {
                _eventAlarmOwnerAutoShowConsumed = true;
            }
        }

        private void NotifyEventAlarmOwnerActivity(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return;
            }

            TryAutoShowEventAlarmOwner(source.Trim());
        }

        private void ShowRecordedUtilityWindow(string windowName, string source)
        {
            if (string.IsNullOrWhiteSpace(windowName))
            {
                return;
            }

            if ((string.Equals(windowName, MapSimulatorWindowNames.Ranking, StringComparison.Ordinal)
                 || string.Equals(windowName, MapSimulatorWindowNames.Event, StringComparison.Ordinal))
                && TryShowRecordedProgressionUtilityWindow(windowName, source))
            {
                return;
            }

            ShowWindowWithInheritedDirectionModeOwner(windowName);
        }

        private void ShowUtilityQuitDialog(string source = null)
        {
            string body = MapleStoryStringPool.GetOrFallback(3304, "Are you sure you want to quit?");
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.InGameConfirmDialog) is InGameConfirmDialogWindow confirmDialogWindow)
            {
                string footer = string.IsNullOrWhiteSpace(source)
                    ? "Recovered in-field FadeYesNo owner path."
                    : $"Launch source: {source}";
                ConfigureInGameConfirmDialog(
                    "Game Menu",
                    body,
                    footer,
                    onConfirm: ContinueConfirmedUtilityQuitThroughLogoutGift,
                    onCancel: null);
                ShowWindow(
                    MapSimulatorWindowNames.InGameConfirmDialog,
                    confirmDialogWindow,
                    trackDirectionModeOwner: true);
                return;
            }

            ShowLoginUtilityDialog(
                "Game Menu",
                body,
                LoginUtilityDialogButtonLayout.YesNo,
                LoginUtilityDialogAction.ConfirmUtilityQuit,
                inputPlaceholder: string.IsNullOrWhiteSpace(source) ? null : $"Launch source: {source}.",
                frameVariant: LoginUtilityDialogFrameVariant.InGameFadeYesNo,
                trackDirectionModeOwner: true);
        }

        private void WireInGameConfirmDialogWindow()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.InGameConfirmDialog) is not InGameConfirmDialogWindow confirmDialogWindow)
            {
                return;
            }

            confirmDialogWindow.SetFont(_fontChat);
            confirmDialogWindow.ConfirmRequested -= HandleInGameConfirmDialogAccepted;
            confirmDialogWindow.CancelRequested -= HandleInGameConfirmDialogCancelled;
            confirmDialogWindow.ConfirmRequested += HandleInGameConfirmDialogAccepted;
            confirmDialogWindow.CancelRequested += HandleInGameConfirmDialogCancelled;
        }

        private void HandleInGameConfirmDialogAccepted()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.InGameConfirmDialog) is InGameConfirmDialogWindow confirmDialogWindow)
            {
                confirmDialogWindow.Hide();
            }

            Action acceptedAction = _inGameConfirmAcceptedAction;
            ClearInGameConfirmDialogActions();
            acceptedAction?.Invoke();
        }

        private void HandleInGameConfirmDialogCancelled()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.InGameConfirmDialog) is InGameConfirmDialogWindow confirmDialogWindow)
            {
                confirmDialogWindow.Hide();
            }

            Action cancelledAction = _inGameConfirmCancelledAction;
            ClearInGameConfirmDialogActions();
            cancelledAction?.Invoke();
        }

        private void HandleMessengerIncomingInvitePromptChanged(MessengerIncomingInvitePromptState state)
        {
            if (state?.IsVisible != true)
            {
                HideMessengerIncomingInvitePromptDialog();
                return;
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.InGameConfirmDialog) is not InGameConfirmDialogWindow confirmDialogWindow)
            {
                return;
            }

            ConfigureInGameConfirmDialog(
                string.IsNullOrWhiteSpace(state.TitleText) ? "Messenger" : state.TitleText,
                state.PromptText,
                string.Empty,
                onConfirm: AcceptMessengerIncomingInvitePrompt,
                onCancel: RejectMessengerIncomingInvitePrompt,
                presentation: confirmDialogWindow.CreateMessengerInvitePresentation(state.StackIndex));

            bool alarmCounterAdvanced = state.AlarmCounter > 0
                && state.AlarmCounter != _lastMessengerInvitePromptAlarmCounter;
            if (alarmCounterAdvanced)
            {
                TryPlayPacketOwnedWzSound(MapleStoryStringPool.GetOrFallback(1275, "Invite"), "UI.img", out _, out _);
            }

            _messengerInvitePromptOwnedDialogActive = true;
            _lastMessengerInvitePromptAlarmCounter = state.AlarmCounter;
            ShowWindow(
                MapSimulatorWindowNames.InGameConfirmDialog,
                confirmDialogWindow,
                trackDirectionModeOwner: true);
        }

        private void AcceptMessengerIncomingInvitePrompt()
        {
            _messengerInvitePromptOwnedDialogActive = false;
            string message = TryMirrorMessengerIncomingInviteAcceptClientRequest(
                string.Empty,
                out string mirroredMessage)
                ? mirroredMessage
                : _messengerRuntime.AcceptIncomingInvite();
            ShowUtilityFeedbackMessage(message);
            if (_messengerRuntime.BuildSnapshot(Environment.TickCount).Participants.Any(participant => participant is { IsLocalPlayer: false }))
            {
                ShowMessengerWindow();
            }
        }

        private void RejectMessengerIncomingInvitePrompt()
        {
            _messengerInvitePromptOwnedDialogActive = false;
            string message = TryMirrorMessengerIncomingInviteRejectClientSeam(
                string.Empty,
                out string mirroredMessage)
                ? mirroredMessage
                : _messengerRuntime.RejectIncomingInvite();
            ShowUtilityFeedbackMessage(message);
        }

        private void HideMessengerIncomingInvitePromptDialog()
        {
            if (!_messengerInvitePromptOwnedDialogActive)
            {
                _lastMessengerInvitePromptAlarmCounter = -1;
                return;
            }

            _messengerInvitePromptOwnedDialogActive = false;
            _lastMessengerInvitePromptAlarmCounter = -1;
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.InGameConfirmDialog) is InGameConfirmDialogWindow confirmDialogWindow)
            {
                confirmDialogWindow.Hide();
            }

            ClearInGameConfirmDialogActions();
        }

        private RankingWindowSnapshot BuildUtilityRankingSnapshot()
        {
            RefreshRankingOwnerRuntimeState();
            CharacterBuild build = _playerManager?.Player?.Build;
            QuestAlarmSnapshot questSnapshot = _questRuntime.BuildQuestAlarmSnapshot(build);
            UserInfoUI.RankDeltaSnapshot rankDelta = ResolveCharacterInfoRankDeltaSnapshot(build);
            string mapName = GetCurrentMapTransferDisplayName();
            int currentMapId = _mapBoard?.MapInfo?.id ?? 0;
            int readyQuestCount = questSnapshot.Entries.Count(entry => entry.IsReadyToComplete);
            int worldId = ResolveRankingRequestWorldId(_simulatorWorldId);
            int worldRequestId = worldId;
            int characterId = build?.Id ?? 0;
            string landingUrl = BuildRankingLandingUrl(build, worldId, out bool usedResolvedTemplate);
            string webSeedText = BuildRankingLandingSeed(build, worldId, out _);
            string templateSeedText = ProgressionUtilityParityRules.FormatRankingLandingTemplateSeed(
                RankingStringPoolUrlTemplateId,
                out bool usedResolvedTemplateSeed);
            string requestShapeText = ProgressionUtilityParityRules.FormatRankingRequestParameters(worldRequestId, characterId);
            string hostText = $"get_server_string_0 => {RankingServerHost}";
            string launchSource = string.IsNullOrWhiteSpace(_lastRankingLaunchSource) ? "status-bar owner" : _lastRankingLaunchSource;
            bool hasActiveRequest = _lastRankingOpenTick != int.MinValue;
            bool hasPacketOwnedRankingEntries = _packetOwnedRankingEntries.Count > 0;
            bool requestPending = ResolveRankingOwnerRequestPending(hasActiveRequest, _lastRankingNavigateTick, _packetOwnedRankingEntries.Count);
            bool isLoading = requestPending;
            string requestValue = hasPacketOwnedRankingEntries
                ? "Packet page"
                : build == null
                    ? "No active character"
                    : requestPending
                        ? "Loading"
                        : "Navigated";

            List<RankingEntrySnapshot> localEntries = new();
            if (build == null)
            {
                localEntries.Add(new RankingEntrySnapshot
                {
                    Label = "Landing Request",
                    Value = requestValue,
                    Detail = $"CUIRanking stays a CWebWnd owner with a loading layer plus close-only dismissal. No active character build is bound, so NavigateUrl still resolves against {landingUrl}."
                });
            }
            else if (requestPending)
            {
                localEntries.Add(new RankingEntrySnapshot
                {
                    Label = "Landing Request",
                    Value = requestValue,
                    Detail = BuildRankingOwnerLifecycleDetail(build, launchSource, webSeedText, usedResolvedTemplate)
                });
                localEntries.Add(new RankingEntrySnapshot
                {
                    Label = "Client Phase",
                    Value = "Loading layer armed",
                    Detail = "Local ladder cards stay hidden until the recovered CWebWnd landing request clears the loading layer."
                });
                localEntries.Add(new RankingEntrySnapshot
                {
                    Label = "Pending Summary",
                    Value = $"World {FormatRank(build.WorldRank)} / Job {FormatRank(build.JobRank)}",
                    Detail = $"Local ranking data is ready for {build.Name}, but it does not surface until the navigated owner phase."
                });
            }
            else
            {
                localEntries.Add(new RankingEntrySnapshot
                {
                    Label = "World Rank",
                    Value = FormatRank(build.WorldRank),
                    Detail = $"Previous {FormatRank(rankDelta.PreviousWorldRank)} ({FormatSignedDelta(rankDelta.PreviousWorldRank - build.WorldRank)})."
                });
                localEntries.Add(new RankingEntrySnapshot
                {
                    Label = "Job Rank",
                    Value = FormatRank(build.JobRank),
                    Detail = $"Previous {FormatRank(rankDelta.PreviousJobRank)} ({FormatSignedDelta(rankDelta.PreviousJobRank - build.JobRank)})."
                });
                localEntries.Add(new RankingEntrySnapshot
                {
                    Label = "Popularity",
                    Value = build.Fame.ToString(CultureInfo.InvariantCulture),
                    Detail = $"Lv. {build.Level} {build.JobName} in {mapName} (map {currentMapId})."
                });
                localEntries.Add(new RankingEntrySnapshot
                {
                    Label = "Combat Context",
                    Value = $"PAD {build.TotalAttack} / MAD {build.TotalMagicAttack}",
                    Detail = readyQuestCount > 0
                        ? $"EXP {build.ExpPercent}%, AP {build.AP.ToString(CultureInfo.InvariantCulture)}, ACC {build.TotalAccuracy.ToString(CultureInfo.InvariantCulture)}, EVA {build.TotalAvoidability.ToString(CultureInfo.InvariantCulture)}, and {readyQuestCount} tracked turn-in candidate{(readyQuestCount == 1 ? string.Empty : "s")}."
                        : $"EXP {build.ExpPercent}%, AP {build.AP.ToString(CultureInfo.InvariantCulture)}, ACC {build.TotalAccuracy.ToString(CultureInfo.InvariantCulture)}, EVA {build.TotalAvoidability.ToString(CultureInfo.InvariantCulture)}."
                });
            }

            IReadOnlyList<RankingEntrySnapshot> entries = ResolveRankingOwnerEntries(localEntries, _packetOwnedRankingEntries);
            string subtitle = hasPacketOwnedRankingEntries
                ? "UIWindow2.img/Ranking stays the owner seam while packet-authored CUIRanking rows now replace the local fallback cards as soon as a recovered ranking page payload lands."
                : build == null
                    ? "UIWindow2.img/Ranking stays the owner seam while the recovered CWebWnd request shape remains unresolved."
                    : $"UIWindow2.img/Ranking stays the owner seam while the recovered CWebWnd request queues NavigateUrl for {build.Name}, world {worldRequestId}, then swaps from loading into simulator-local ladder context.";
            string statusText = hasPacketOwnedRankingEntries
                ? $"BtRank now keeps the recovered loading/NavigateUrl owner lifecycle, but packet-authored ranking rows no longer die in the inbox decoder: {_lastPacketOwnedRankingSummary} The remaining gap is narrower and centered on official remote ladder ownership, returned page branching beyond injected row payloads, and final network dispatch fidelity."
                : "BtRank now mirrors the client owner lifecycle more closely: loading-layer request first, navigated local world/job/popularity/combat cards second. The owner now keeps the recovered StringPool[0xAA2] template, resolved host seed, and explicit worldid/characterid payload split visible, but remote ladders, returned page payloads, and packet-fed ranking pages are still outside this board.";
            string navigationStateText = BuildRankingOwnerLifecycleDetail(build, launchSource, webSeedText, usedResolvedTemplate);
            if (hasPacketOwnedRankingEntries && !string.IsNullOrWhiteSpace(_lastPacketOwnedRankingSummary))
            {
                navigationStateText = $"{navigationStateText} {_lastPacketOwnedRankingSummary}";
            }

            RankingWindowSnapshot snapshot = new RankingWindowSnapshot
            {
                Title = "Ranking",
                Subtitle = subtitle,
                StatusText = statusText,
                NavigationCaption = usedResolvedTemplateSeed ? templateSeedText : $"{templateSeedText} (fallback)",
                NavigationSeedText = $"NavigateUrl => {landingUrl}",
                NavigationHostText = hostText,
                NavigationRequestText = requestShapeText,
                NavigationStateText = navigationStateText,
                IsLoading = isLoading,
                LoadingStartTick = _lastRankingOpenTick,
                Entries = entries
            };

            return ProgressionUtilityParityRules.ApplyPacketOwnedRankingOwnerState(
                snapshot,
                _packetOwnedRankingOwnerState);
        }

        internal static bool ResolveRankingOwnerRequestPending(bool hasActiveRequest, int lastRankingNavigateTick, int packetOwnedEntryCount)
        {
            return hasActiveRequest
                && lastRankingNavigateTick == int.MinValue
                && packetOwnedEntryCount <= 0;
        }

        internal static IReadOnlyList<RankingEntrySnapshot> ResolveRankingOwnerEntries(
            IReadOnlyList<RankingEntrySnapshot> localEntries,
            IReadOnlyList<RankingEntrySnapshot> packetOwnedEntries)
        {
            if (packetOwnedEntries is { Count: > 0 })
            {
                return packetOwnedEntries;
            }

            return localEntries ?? Array.Empty<RankingEntrySnapshot>();
        }

        private EventWindowSnapshot BuildUtilityEventSnapshot()
        {
            int currentTick = Environment.TickCount;
            CharacterBuild build = _playerManager?.Player?.Build;
            QuestAlarmSnapshot questSnapshot = _questRuntime.BuildQuestAlarmSnapshot(build);
            List<EventEntrySnapshot> entries = new();
            AppendPacketOwnedEventAlarmEntries(entries, currentTick);
            int nextSortOrder = entries.Count;

            if (_packetOwnedEventAlarmLines.Count > 0)
            {
                entries.Add(new EventEntrySnapshot
                {
                    Title = "Event Alarm Text Feed",
                    Detail = string.IsNullOrWhiteSpace(_lastPacketOwnedEventAlarmSummary)
                        ? $"Packet-authored CUIEventAlarm::m_aCT feed is carrying {_packetOwnedEventAlarmLines.Count.ToString(CultureInfo.InvariantCulture)} explicit line(s)."
                        : _lastPacketOwnedEventAlarmSummary,
                    StatusText = "Running",
                    Status = EventEntryStatus.InProgress,
                    ScheduledAt = DateTime.Today,
                    SourceTick = _lastPacketOwnedEventAlarmTick,
                    IncludeInCalendar = false,
                    SortPriority = EventEntrySortPriorityPrimary,
                    SortOrder = nextSortOrder++
                });
            }

            if (_packetOwnedEventCalendarEntries.Count > 0)
            {
                for (int i = 0; i < _packetOwnedEventCalendarEntries.Count; i++)
                {
                    entries.Add(CloneEventEntryForOwnerSnapshot(_packetOwnedEventCalendarEntries[i], nextSortOrder++));
                }
            }

            if (_lastEventOpenTick != int.MinValue)
            {
                string lifecycleDetail = string.IsNullOrWhiteSpace(_lastEventLaunchSource)
                    ? $"Dedicated CUIEventAlarm owner was last launched at tick {_lastEventOpenTick.ToString(CultureInfo.InvariantCulture)}."
                    : $"Dedicated CUIEventAlarm owner was last launched from {_lastEventLaunchSource} at tick {_lastEventOpenTick.ToString(CultureInfo.InvariantCulture)}.";
                entries.Add(new EventEntrySnapshot
                {
                    Title = "Owner Lifecycle",
                    Detail = $"{lifecycleDetail} The client-observed 8 second auto-dismiss remains armed until the user touches the filter or calendar controls, the close control stays on the recovered `MakeUOLByUIType` seam from `CUIEventAlarm::OnCreate`, and the top alarm strip keeps the recovered 198 px clip region blank until packet/runtime text actually populates it.",
                    StatusText = "Running",
                    Status = EventEntryStatus.InProgress,
                    ScheduledAt = DateTime.Today,
                    SourceTick = _lastEventOpenTick,
                    IncludeInCalendar = false,
                    SortPriority = EventEntrySortPriorityPrimary,
                    SortOrder = nextSortOrder++
                });
            }

            string loginStatus = _loginRuntime.LastEventSummary;
            bool shouldSurfaceLoginBootstrap = _loginRuntime.LastPacketType.HasValue
                && (!_loginRuntime.HasWorldInformation || _loginRuntime.CurrentStep != LoginStep.EnteringField);
            if (shouldSurfaceLoginBootstrap)
            {
                entries.Add(new EventEntrySnapshot
                {
                    Title = "Login Bootstrap Feed",
                    Detail = $"{_loginRuntime.CurrentStep} step. {loginStatus}",
                    StatusText = _loginRuntime.HasWorldInformation ? "Clear" : "Running",
                    Status = _loginRuntime.HasWorldInformation ? EventEntryStatus.Clear : EventEntryStatus.InProgress,
                    ScheduledAt = DateTime.Today,
                    IncludeInCalendar = false,
                    SortPriority = EventEntrySortPriorityBootstrap,
                    SortOrder = nextSortOrder++
                });
            }

            string packetFieldState = _packetFieldStateRuntime.DescribeStatus(currentTick);
            if (!IsIdleEventOwnerStatus(packetFieldState))
            {
                entries.Add(new EventEntrySnapshot
                {
                    Title = "Packet Field State",
                    Detail = packetFieldState,
                    StatusText = "Running",
                    Status = EventEntryStatus.InProgress,
                    ScheduledAt = DateTime.Today,
                    IncludeInCalendar = false,
                    SortPriority = EventEntrySortPriorityPrimary,
                    SortOrder = nextSortOrder++
                });
            }

            string overlayStatus = DescribePacketOwnedFieldFadeAndBalloonStatus(currentTick);
            if (!IsIdleEventOwnerStatus(overlayStatus))
            {
                entries.Add(new EventEntrySnapshot
                {
                    Title = "Local Overlay Feed",
                    Detail = overlayStatus,
                    StatusText = "Running",
                    Status = EventEntryStatus.InProgress,
                    ScheduledAt = DateTime.Today,
                    IncludeInCalendar = false,
                    SortPriority = EventEntrySortPriorityPrimary,
                    SortOrder = nextSortOrder++
                });
            }

            if (_packetQuestGuideQuestId > 0)
            {
                entries.Add(new EventEntrySnapshot
                {
                    Title = "Quest Guide Routing",
                    Detail = $"Quest #{_packetQuestGuideQuestId} is keeping packet-authored world-map targets alive.",
                    StatusText = "Running",
                    Status = EventEntryStatus.InProgress,
                    ScheduledAt = DateTime.Today,
                    IncludeInCalendar = false,
                    SortPriority = EventEntrySortPriorityRuntime,
                    SortOrder = nextSortOrder++
                });
            }

            if (_lastDeliveryQuestId > 0)
            {
                entries.Add(new EventEntrySnapshot
                {
                    Title = "Quest Delivery Launch",
                    Detail = $"Quest #{_lastDeliveryQuestId} delivery owner is primed for item #{_lastDeliveryItemId}.",
                    StatusText = "Running",
                    Status = EventEntryStatus.InProgress,
                    ScheduledAt = DateTime.Today,
                    IncludeInCalendar = false,
                    SortPriority = EventEntrySortPriorityRuntime,
                    SortOrder = nextSortOrder++
                });
            }

            if (_lastClassCompetitionOpenTick != int.MinValue)
            {
                entries.Add(new EventEntrySnapshot
                {
                    Title = "Class Competition Page",
                    Detail = "Packet-owned class competition launch has been routed into the named utility owner.",
                    StatusText = "Clear",
                    Status = EventEntryStatus.Clear,
                    ScheduledAt = DateTime.Today,
                    SourceTick = _lastClassCompetitionOpenTick,
                    IncludeInCalendar = false,
                    SortPriority = EventEntrySortPrioritySecondary,
                    SortOrder = nextSortOrder++
                });
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedLogoutGiftSummary)
                && !string.Equals(_lastPacketOwnedLogoutGiftSummary, "Packet-owned logout gift idle.", StringComparison.OrdinalIgnoreCase))
            {
                bool logoutGiftVisible = uiWindowManager?.GetWindow(MapSimulatorWindowNames.LogoutGift)?.IsVisible == true;
                int logoutGiftTick = _lastPacketOwnedLogoutGiftSelectionTick != int.MinValue
                    ? _lastPacketOwnedLogoutGiftSelectionTick
                    : _lastPacketOwnedLogoutGiftRefreshTick;
                entries.Add(new EventEntrySnapshot
                {
                    Title = "Logout Gift Feed",
                    Detail = _lastPacketOwnedLogoutGiftSummary,
                    StatusText = logoutGiftVisible ? "Running" : "Clear",
                    Status = logoutGiftVisible ? EventEntryStatus.InProgress : EventEntryStatus.Clear,
                    ScheduledAt = DateTime.Today,
                    SourceTick = logoutGiftTick,
                    IncludeInCalendar = false,
                    SortPriority = logoutGiftVisible ? EventEntrySortPriorityPrimary : EventEntrySortPrioritySecondary,
                    SortOrder = nextSortOrder++
                });
            }

            if (questSnapshot.Entries.Count > 0)
            {
                int readyCount = questSnapshot.Entries.Count(entry => entry.IsReadyToComplete);
                entries.Add(new EventEntrySnapshot
                {
                    Title = "Tracked Quest Alerts",
                    Detail = readyCount > 0
                        ? $"{readyCount} tracked quest{(readyCount == 1 ? string.Empty : "s")} can complete now."
                        : $"{questSnapshot.Entries.Count} tracked quest entry{(questSnapshot.Entries.Count == 1 ? " is" : "ies are")} still in progress.",
                    StatusText = readyCount > 0 ? "Clear" : "Running",
                    Status = readyCount > 0 ? EventEntryStatus.Clear : EventEntryStatus.InProgress,
                    ScheduledAt = DateTime.Today,
                    IncludeInCalendar = false,
                    SortPriority = readyCount > 0 ? EventEntrySortPrioritySecondary : EventEntrySortPriorityRuntime,
                    SortOrder = nextSortOrder++
                });
            }

            foreach (EventEntrySnapshot fieldEntry in BuildSpecialFieldEventEntries())
            {
                entries.Add(new EventEntrySnapshot
                {
                    Title = fieldEntry.Title,
                    Detail = fieldEntry.Detail,
                    StatusText = fieldEntry.StatusText,
                    Status = fieldEntry.Status,
                    ScheduledAt = fieldEntry.ScheduledAt,
                    SourceTick = fieldEntry.SourceTick,
                    IncludeInCalendar = fieldEntry.IncludeInCalendar,
                    SortPriority = fieldEntry.SortPriority,
                    SortOrder = nextSortOrder++
                });
            }

            if (_packetOwnedEventCalendarEntries.Count == 0 && !entries.Any(entry => entry.Status == EventEntryStatus.Upcoming))
            {
                entries.Add(new EventEntrySnapshot
                {
                    Title = "Live Network Event Lists",
                    Detail = "UIWindow2.img/EventList art is active, the owner now leaves the recovered top text strip empty when no packet/runtime text is available instead of filling it with a simulator-only placeholder, but attendance packets and the official event-feed model are still pending deeper client dispatch work.",
                    StatusText = "Will",
                    Status = EventEntryStatus.Upcoming,
                    ScheduledAt = DateTime.Today.AddDays(1),
                    IncludeInCalendar = false,
                    SortPriority = EventEntrySortPriorityFallback,
                    SortOrder = nextSortOrder++
                });
            }

            return new EventWindowSnapshot
            {
                Title = "Event",
                Subtitle = "EventList row, slot, icon, and calendar art now surface simulator runtime entries plus packet-authored alarm text and calendar rows through an event owner that auto-dismisses like CUIEventAlarm until the user interacts with its WZ-backed controls. The recovered 198 px alarm strip now stays blank when no packet/runtime text is active instead of rendering a simulator-only placeholder sentence.",
                StatusText = "BtEvent now exposes packet-owned utility, quest, overlay, tutor, radio, logout-gift, sound, direct event-alarm CT text, and injected calendar rows through the client event owner while keeping the WZ-backed filter and calendar surfaces intact. Exact live attendance packet formats and the native server-fed attendance/calendar model still remain outside this window.",
                AutoDismissDelayMs = 8000,
                AlarmLines = BuildEventAlarmOwnerLines(currentTick),
                Entries = entries
            };
        }

        private static EventEntrySnapshot CloneEventEntryForOwnerSnapshot(
            EventEntrySnapshot entry,
            int sortOrder,
            int defaultSourceTick = int.MinValue)
        {
            if (entry == null)
            {
                return new EventEntrySnapshot
                {
                    ScheduledAt = DateTime.Today,
                    SourceTick = defaultSourceTick,
                    SortOrder = Math.Max(0, sortOrder)
                };
            }

            return new EventEntrySnapshot
            {
                Title = entry.Title,
                Detail = entry.Detail,
                StatusText = entry.StatusText,
                AlarmText = entry.AlarmText,
                Status = entry.Status,
                ScheduledAt = entry.ScheduledAt.Date,
                SourceTick = entry.SourceTick == int.MinValue ? defaultSourceTick : entry.SourceTick,
                IncludeInCalendar = entry.IncludeInCalendar,
                SortPriority = entry.SortPriority,
                SortOrder = Math.Max(0, sortOrder)
            };
        }

        internal static EventEntrySnapshot CloneEventEntryForOwnerSnapshotForTests(
            EventEntrySnapshot entry,
            int sortOrder,
            int defaultSourceTick = int.MinValue)
        {
            return CloneEventEntryForOwnerSnapshot(entry, sortOrder, defaultSourceTick);
        }

        private IEnumerable<EventEntrySnapshot> BuildSpecialFieldEventEntries()
        {
            if (_specialFieldRuntime.SpecialEffects.Wedding.IsActive)
            {
                yield return BuildSpecialFieldEntry("Wedding Ceremony", _specialFieldRuntime.SpecialEffects.Wedding.DescribeStatus());
            }

            if (_specialFieldRuntime.SpecialEffects.Witchtower.IsActive)
            {
                yield return BuildSpecialFieldEntry("Witchtower", _specialFieldRuntime.SpecialEffects.Witchtower.DescribeStatus());
            }

            if (_specialFieldRuntime.SpecialEffects.Battlefield.IsActive)
            {
                yield return BuildSpecialFieldEntry("Battlefield", _specialFieldRuntime.SpecialEffects.Battlefield.DescribeStatus());
            }

            if (_specialFieldRuntime.SpecialEffects.GuildBoss.IsActive)
            {
                yield return BuildSpecialFieldEntry("Guild Boss", _specialFieldRuntime.SpecialEffects.GuildBoss.DescribeStatus());
            }

            if (_specialFieldRuntime.SpecialEffects.Dojo.IsActive)
            {
                yield return BuildSpecialFieldEntry("Mu Lung Dojo", _specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus());
            }

            if (_specialFieldRuntime.SpecialEffects.SpaceGaga.IsActive)
            {
                yield return BuildSpecialFieldEntry("SpaceGAGA", _specialFieldRuntime.SpecialEffects.SpaceGaga.DescribeStatus());
            }

            if (_specialFieldRuntime.SpecialEffects.Massacre.IsActive)
            {
                yield return BuildSpecialFieldEntry("Massacre", _specialFieldRuntime.SpecialEffects.Massacre.DescribeStatus());
            }

            if (_specialFieldRuntime.Minigames.Tournament.IsActive)
            {
                yield return BuildSpecialFieldEntry("Tournament", _specialFieldRuntime.Minigames.Tournament.DescribeStatus());
            }
        }

        private static EventEntrySnapshot BuildSpecialFieldEntry(string title, string detail)
        {
            return new EventEntrySnapshot
            {
                Title = title,
                Detail = detail,
                StatusText = "Running",
                Status = EventEntryStatus.InProgress,
                ScheduledAt = DateTime.Today,
                IncludeInCalendar = false,
                SortPriority = EventEntrySortPriorityRuntime
            };
        }

        private void RefreshRankingOwnerRuntimeState()
        {
            if (_lastRankingOpenTick == int.MinValue || _lastRankingNavigateTick != int.MinValue)
            {
                return;
            }

            int now = Environment.TickCount;
            if (Math.Max(0, unchecked(now - _lastRankingOpenTick)) >= RankingOwnerNavigateDelayMs)
            {
                _lastRankingNavigateTick = now;
            }
        }

        private string BuildRankingLandingUrl(CharacterBuild build, int worldId, out bool usedResolvedTemplate)
        {
            int characterId = build?.Id ?? 0;
            return ProgressionUtilityParityRules.ResolveRankingLandingUrl(
                RankingServerHost,
                RankingStringPoolUrlTemplateId,
                worldId,
                characterId,
                out usedResolvedTemplate);
        }

        private string BuildRankingLandingSeed(CharacterBuild build, int worldId, out bool usedResolvedTemplate)
        {
            int characterId = build?.Id ?? 0;
            return ProgressionUtilityParityRules.FormatRankingLandingSeed(
                RankingServerHost,
                RankingStringPoolUrlTemplateId,
                worldId,
                characterId,
                out usedResolvedTemplate);
        }

        internal static int ResolveRankingRequestWorldId(int simulatorWorldId)
        {
            // Client evidence: CUIRanking::OnCreate formats StringPool[0xAA2] with the
            // live CWvsContext world value directly (no synthetic +1 remap).
            return Math.Max(0, simulatorWorldId);
        }

        private string BuildRankingOwnerLifecycleDetail(CharacterBuild build, string launchSource, string webSeedText, bool usedResolvedTemplate)
        {
            string resolutionText = usedResolvedTemplate
                ? "Recovered directly from StringPool[0xAA2]."
                : "StringPool[0xAA2] is unavailable here, so the simulator is using its fallback template.";
            if (_lastRankingOpenTick == int.MinValue)
            {
                return $"Recovered client shape: CWebWnd::OnCreate queues a loading layer, then formats {webSeedText}. {resolutionText} The owner has not been launched in this session yet.";
            }

            if (_lastRankingNavigateTick == int.MinValue)
            {
                string actorName = build?.Name ?? "the active character";
                return $"Launch source: {launchSource}. CWebWnd queued the landing request at tick {_lastRankingOpenTick.ToString(CultureInfo.InvariantCulture)} and the loading layer is still active for {actorName}. {resolutionText}";
            }

            return $"Launch source: {launchSource}. CWebWnd queued the landing request at tick {_lastRankingOpenTick.ToString(CultureInfo.InvariantCulture)} and switched into the navigated owner state at tick {_lastRankingNavigateTick.ToString(CultureInfo.InvariantCulture)}. {resolutionText}";
        }

        private void AppendPacketOwnedEventAlarmEntries(List<EventEntrySnapshot> entries, int currentTick)
        {
            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedNoticeMessage))
            {
                entries.Add(CreatePacketOwnedEventEntry("Notice Alarm", _lastPacketOwnedNoticeMessage, EventEntryStatus.Clear, "Clear", _lastPacketOwnedNoticeTick));
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedChatMessage))
            {
                entries.Add(CreatePacketOwnedEventEntry("Chat Alarm", _lastPacketOwnedChatMessage, EventEntryStatus.Clear, "Clear", _lastPacketOwnedChatTick));
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedBuffzoneMessage))
            {
                entries.Add(CreatePacketOwnedEventEntry("Buffzone Alarm", _lastPacketOwnedBuffzoneMessage, EventEntryStatus.InProgress, "Running", _lastPacketOwnedBuffzoneTick));
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedAskApspMessage) || _packetOwnedApspPromptActive)
            {
                string apspDetail = _packetOwnedApspPromptActive
                    ? $"{_lastPacketOwnedAskApspMessage} Prompt is still active for context {_packetOwnedApspPromptContextToken} / event {_packetOwnedApspPromptEventType}."
                    : _lastPacketOwnedAskApspMessage;
                entries.Add(CreatePacketOwnedEventEntry("AP/SP Event Prompt", apspDetail, _packetOwnedApspPromptActive ? EventEntryStatus.InProgress : EventEntryStatus.Clear, _packetOwnedApspPromptActive ? "Running" : "Clear", _lastPacketOwnedAskApspTick));
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedSkillGuideMessage))
            {
                entries.Add(CreatePacketOwnedEventEntry("Skill Guide Launch", _lastPacketOwnedSkillGuideMessage, EventEntryStatus.Clear, "Clear", _lastPacketOwnedSkillGuideTick));
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedFollowFailureMessage))
            {
                entries.Add(CreatePacketOwnedEventEntry("Follow Failure", _lastPacketOwnedFollowFailureMessage, EventEntryStatus.Start, "Start", _lastPacketOwnedFollowFailureTick));
            }

            string tutorStatus = DescribePacketOwnedTutorStatus(currentTick);
            if (_packetOwnedTutorRuntime.IsActive || _packetOwnedTutorRuntime.HasRegisteredTutorVariants)
            {
                entries.Add(CreatePacketOwnedEventEntry(
                    "Tutor Alarm",
                    tutorStatus,
                    _packetOwnedTutorRuntime.IsActive ? EventEntryStatus.InProgress : EventEntryStatus.Clear,
                    _packetOwnedTutorRuntime.IsActive ? "Running" : "Clear",
                    _packetOwnedTutorRuntime.ActiveMessageStartedAt));
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedRadioStatusMessage)
                && (!string.Equals(_lastPacketOwnedRadioStatusMessage, "Packet-owned radio idle.", StringComparison.OrdinalIgnoreCase)
                    || IsPacketOwnedRadioPlaying()))
            {
                entries.Add(CreatePacketOwnedEventEntry(
                    "Radio Schedule",
                    _lastPacketOwnedRadioStatusMessage,
                    IsPacketOwnedRadioPlaying() ? EventEntryStatus.InProgress : EventEntryStatus.Clear,
                    IsPacketOwnedRadioPlaying() ? "Running" : "Clear",
                    _lastPacketOwnedRadioLastPollTick != int.MinValue ? _lastPacketOwnedRadioLastPollTick : _lastPacketOwnedRadioStartTick));
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedEventSoundDescriptor))
            {
                entries.Add(CreatePacketOwnedEventEntry("Event Sound", $"Last event sound resolved through {_lastPacketOwnedEventSoundDescriptor}.", EventEntryStatus.Clear, "Clear", _lastPacketOwnedEventSoundTick));
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedMinigameSoundDescriptor))
            {
                entries.Add(CreatePacketOwnedEventEntry("Minigame Sound", $"Last minigame sound resolved through {_lastPacketOwnedMinigameSoundDescriptor}.", EventEntryStatus.Clear, "Clear", _lastPacketOwnedMinigameSoundTick));
            }
        }

        private static EventEntrySnapshot CreatePacketOwnedEventEntry(string title, string detail, EventEntryStatus status, string statusText)
        {
            return CreatePacketOwnedEventEntry(title, detail, status, statusText, int.MinValue);
        }

        private static EventEntrySnapshot CreatePacketOwnedEventEntry(string title, string detail, EventEntryStatus status, string statusText, int sourceTick)
        {
            return new EventEntrySnapshot
            {
                Title = title,
                Detail = detail ?? string.Empty,
                Status = status,
                StatusText = statusText,
                ScheduledAt = DateTime.Today,
                SourceTick = sourceTick,
                IncludeInCalendar = false,
                SortPriority = status == EventEntryStatus.InProgress || status == EventEntryStatus.Start
                    ? EventEntrySortPriorityPrimary
                    : EventEntrySortPrioritySecondary
            };
        }

        private IReadOnlyList<EventAlarmLineSnapshot> BuildEventAlarmOwnerLines(int currentTick)
        {
            if (_packetOwnedEventAlarmLines.Count > 0
                && ShouldRetainPacketOwnedEventAlarmLines(_lastPacketOwnedEventAlarmTick, currentTick))
            {
                return _packetOwnedEventAlarmLines
                    .Take(EventAlarmOwnerMaxVisibleLines)
                    .Select(line => new EventAlarmLineSnapshot
                    {
                        Text = line.Text,
                        Left = line.Left,
                        Top = line.Top,
                        IsHighlighted = line.IsHighlighted,
                        TextColorArgb = line.TextColorArgb
                    })
                    .ToArray();
            }
            if (_packetOwnedEventAlarmLines.Count > 0
                && _lastPacketOwnedEventAlarmTick != int.MinValue)
            {
                _packetOwnedEventAlarmLines.Clear();
            }

            List<(string Text, int Tick, bool Highlight, bool IsActive)> candidates = new();

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedNoticeMessage))
            {
                AddEventAlarmLineCandidate(
                    candidates,
                    $"Notice: {TruncatePacketOwnedUtilityText(_lastPacketOwnedNoticeMessage, 64)}",
                    _lastPacketOwnedNoticeTick,
                    currentTick,
                    highlight: true);
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedChatMessage))
            {
                AddEventAlarmLineCandidate(
                    candidates,
                    $"Chat: {TruncatePacketOwnedUtilityText(_lastPacketOwnedChatMessage, 64)}",
                    _lastPacketOwnedChatTick,
                    currentTick);
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedBuffzoneMessage))
            {
                AddEventAlarmLineCandidate(
                    candidates,
                    $"Buff zone: {TruncatePacketOwnedUtilityText(_lastPacketOwnedBuffzoneMessage, 60)}",
                    _lastPacketOwnedBuffzoneTick,
                    currentTick);
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedAskApspMessage))
            {
                string apspPrefix = _packetOwnedApspPromptActive ? "AP/SP prompt" : "AP/SP event";
                AddEventAlarmLineCandidate(
                    candidates,
                    $"{apspPrefix}: {TruncatePacketOwnedUtilityText(_lastPacketOwnedAskApspMessage, 58)}",
                    _lastPacketOwnedAskApspTick,
                    currentTick,
                    isActive: _packetOwnedApspPromptActive);
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedSkillGuideMessage))
            {
                AddEventAlarmLineCandidate(
                    candidates,
                    _lastPacketOwnedSkillGuideGrade > 0
                        ? $"Skill guide G{_lastPacketOwnedSkillGuideGrade}: {TruncatePacketOwnedUtilityText(_lastPacketOwnedSkillGuideMessage, 53)}"
                        : $"Skill guide: {TruncatePacketOwnedUtilityText(_lastPacketOwnedSkillGuideMessage, 60)}",
                    _lastPacketOwnedSkillGuideTick,
                    currentTick);
            }

            if (_packetOwnedTutorRuntime.IsActive || _packetOwnedTutorRuntime.HasRegisteredTutorVariants)
            {
                AddEventAlarmLineCandidate(
                    candidates,
                    TruncatePacketOwnedUtilityText(DescribePacketOwnedTutorStatus(currentTick), 70),
                    _packetOwnedTutorRuntime.ActiveMessageStartedAt,
                    currentTick,
                    isActive: _packetOwnedTutorRuntime.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedRadioStatusMessage)
                && (!string.Equals(_lastPacketOwnedRadioStatusMessage, "Packet-owned radio idle.", StringComparison.OrdinalIgnoreCase)
                    || IsPacketOwnedRadioPlaying()))
            {
                int radioTick = _lastPacketOwnedRadioLastPollTick != int.MinValue
                    ? _lastPacketOwnedRadioLastPollTick
                    : _lastPacketOwnedRadioStartTick;
                AddEventAlarmLineCandidate(
                    candidates,
                    IsPacketOwnedRadioPlaying()
                        ? $"Radio: {TruncatePacketOwnedUtilityText(_lastPacketOwnedRadioDisplayName ?? _lastPacketOwnedRadioTrackDescriptor, 60)}"
                        : $"Radio: {TruncatePacketOwnedUtilityText(_lastPacketOwnedRadioStatusMessage, 60)}",
                    radioTick,
                    currentTick,
                    isActive: IsPacketOwnedRadioPlaying());
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedFollowFailureMessage))
            {
                AddEventAlarmLineCandidate(
                    candidates,
                    $"Follow: {TruncatePacketOwnedUtilityText(_lastPacketOwnedFollowFailureMessage, 62)}",
                    _lastPacketOwnedFollowFailureTick,
                    currentTick);
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedLogoutGiftSummary)
                && !string.Equals(_lastPacketOwnedLogoutGiftSummary, "Packet-owned logout gift idle.", StringComparison.OrdinalIgnoreCase))
            {
                int logoutGiftTick = _lastPacketOwnedLogoutGiftSelectionTick != int.MinValue
                    ? _lastPacketOwnedLogoutGiftSelectionTick
                    : _lastPacketOwnedLogoutGiftRefreshTick;
                AddEventAlarmLineCandidate(
                    candidates,
                    $"Logout gift: {TruncatePacketOwnedUtilityText(_lastPacketOwnedLogoutGiftSummary, 56)}",
                    logoutGiftTick,
                    currentTick,
                    isActive: uiWindowManager?.GetWindow(MapSimulatorWindowNames.LogoutGift)?.IsVisible == true);
            }

            if (candidates.Count == 0)
            {
                return Array.Empty<EventAlarmLineSnapshot>();
            }

            List<EventAlarmLineSnapshot> lines = candidates
                .OrderByDescending(candidate => candidate.IsActive)
                .ThenBy(candidate => ResolveEventAlarmLineAge(candidate.Tick, currentTick))
                .ThenByDescending(candidate => candidate.Highlight)
                .Take(EventAlarmOwnerMaxVisibleLines)
                .Select((candidate, index) => CreateEventAlarmLine(candidate.Text, index, candidate.Highlight))
                .ToList();

            return lines;
        }

        private static void AddEventAlarmLineCandidate(
            ICollection<(string Text, int Tick, bool Highlight, bool IsActive)> candidates,
            string text,
            int sourceTick,
            int currentTick,
            bool highlight = false,
            bool isActive = false)
        {
            if (candidates == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (!isActive && !IsEventAlarmLineRecent(sourceTick, currentTick))
            {
                return;
            }

            candidates.Add((text, sourceTick, highlight, isActive));
        }

        private static bool IsEventAlarmLineRecent(int sourceTick, int currentTick)
        {
            return sourceTick != int.MinValue
                && ShouldRetainPacketOwnedEventAlarmLines(sourceTick, currentTick);
        }

        internal static bool ShouldRetainPacketOwnedEventAlarmLines(int sourceTick, int currentTick)
        {
            return ResolveEventAlarmLineAge(sourceTick, currentTick) <= EventAlarmFeedLifetimeMs;
        }

        private static int ResolveEventAlarmLineAge(int sourceTick, int currentTick)
        {
            if (sourceTick == int.MinValue)
            {
                return int.MaxValue;
            }

            int age = unchecked(currentTick - sourceTick);
            return age < 0 ? 0 : age;
        }

        private static bool IsIdleEventOwnerStatus(string statusText)
        {
            return string.IsNullOrWhiteSpace(statusText)
                || statusText.Contains("idle", StringComparison.OrdinalIgnoreCase);
        }

        private static EventAlarmLineSnapshot CreateEventAlarmLine(string text, int index, bool highlight = false)
        {
            int clampedIndex = Math.Max(0, index);
            return new EventAlarmLineSnapshot
            {
                Text = text ?? string.Empty,
                Left = 0,
                Top = clampedIndex * 13,
                IsHighlighted = highlight
            };
        }

        private static string FormatRank(int rank)
        {
            return rank > 0 ? $"#{rank.ToString(CultureInfo.InvariantCulture)}" : "local";
        }

        private static string FormatRank(int? rank)
        {
            return FormatRank(rank ?? 0);
        }

        private static string FormatSignedDelta(int delta)
        {
            return delta switch
            {
                > 0 => $"+{delta.ToString(CultureInfo.InvariantCulture)}",
                < 0 => delta.ToString(CultureInfo.InvariantCulture),
                _ => "0"
            };
        }

        private static string FormatSignedDelta(int? delta)
        {
            return FormatSignedDelta(delta ?? 0);
        }
    }
}
