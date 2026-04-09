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
        private int _lastRankingOpenTick = int.MinValue;
        private int _lastRankingNavigateTick = int.MinValue;
        private string _lastRankingLaunchSource = string.Empty;
        private int _lastEventOpenTick = int.MinValue;
        private string _lastEventLaunchSource = string.Empty;

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
            TryShowRecordedProgressionUtilityWindow(
                MapSimulatorWindowNames.Event,
                source,
                allowVisibleReset: false);
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

        private void ShowUtilityQuitDialog()
        {
            string body = MapleStoryStringPool.GetOrFallback(3304, "Are you sure you want to quit?");
            ShowLoginUtilityDialog(
                "Game Menu",
                body,
                LoginUtilityDialogButtonLayout.YesNo,
                LoginUtilityDialogAction.ConfirmUtilityQuit);
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
            int worldId = Math.Max(0, _simulatorWorldId);
            int worldRequestId = worldId + 1;
            int characterId = build?.Id ?? 0;
            string landingUrl = BuildRankingLandingUrl(build, worldId, out bool usedResolvedTemplate);
            string webSeedText = BuildRankingLandingSeed(build, worldId, out _);
            string requestShapeText = ProgressionUtilityParityRules.FormatRankingRequestParameters(worldRequestId, characterId);
            string hostText = $"get_server_string_0 => {RankingServerHost}";
            string launchSource = string.IsNullOrWhiteSpace(_lastRankingLaunchSource) ? "status-bar owner" : _lastRankingLaunchSource;
            bool hasActiveRequest = _lastRankingOpenTick != int.MinValue;
            bool requestPending = hasActiveRequest && _lastRankingNavigateTick == int.MinValue;
            bool isLoading = build != null && requestPending;
            string requestValue = build == null
                ? "No active character"
                : requestPending
                    ? "Loading"
                    : "Navigated";

            List<RankingEntrySnapshot> entries = new();
            if (build == null)
            {
                entries.Add(new RankingEntrySnapshot
                {
                    Label = "Landing Request",
                    Value = requestValue,
                    Detail = $"CUIRanking stays a CWebWnd owner with a loading layer plus close-only dismissal. No active character build is bound, so NavigateUrl still resolves against {landingUrl}."
                });
            }
            else if (requestPending)
            {
                entries.Add(new RankingEntrySnapshot
                {
                    Label = "Landing Request",
                    Value = requestValue,
                    Detail = BuildRankingOwnerLifecycleDetail(build, launchSource, webSeedText, usedResolvedTemplate)
                });
                entries.Add(new RankingEntrySnapshot
                {
                    Label = "Client Phase",
                    Value = "Loading layer armed",
                    Detail = "Local ladder cards stay hidden until the recovered CWebWnd landing request clears the loading layer."
                });
                entries.Add(new RankingEntrySnapshot
                {
                    Label = "Pending Summary",
                    Value = $"World {FormatRank(build.WorldRank)} / Job {FormatRank(build.JobRank)}",
                    Detail = $"Local ranking data is ready for {build.Name}, but it does not surface until the navigated owner phase."
                });
            }
            else
            {
                entries.Add(new RankingEntrySnapshot
                {
                    Label = "World Rank",
                    Value = FormatRank(build.WorldRank),
                    Detail = $"Previous {FormatRank(rankDelta.PreviousWorldRank)} ({FormatSignedDelta(rankDelta.PreviousWorldRank - build.WorldRank)})."
                });
                entries.Add(new RankingEntrySnapshot
                {
                    Label = "Job Rank",
                    Value = FormatRank(build.JobRank),
                    Detail = $"Previous {FormatRank(rankDelta.PreviousJobRank)} ({FormatSignedDelta(rankDelta.PreviousJobRank - build.JobRank)})."
                });
                entries.Add(new RankingEntrySnapshot
                {
                    Label = "Popularity",
                    Value = build.Fame.ToString(CultureInfo.InvariantCulture),
                    Detail = $"Lv. {build.Level} {build.JobName} in {mapName} (map {currentMapId})."
                });
                entries.Add(new RankingEntrySnapshot
                {
                    Label = "Combat Context",
                    Value = $"PAD {build.TotalAttack} / MAD {build.TotalMagicAttack}",
                    Detail = readyQuestCount > 0
                        ? $"EXP {build.ExpPercent}%, AP {build.AP.ToString(CultureInfo.InvariantCulture)}, ACC {build.TotalAccuracy.ToString(CultureInfo.InvariantCulture)}, EVA {build.TotalAvoidability.ToString(CultureInfo.InvariantCulture)}, and {readyQuestCount} tracked turn-in candidate{(readyQuestCount == 1 ? string.Empty : "s")}."
                        : $"EXP {build.ExpPercent}%, AP {build.AP.ToString(CultureInfo.InvariantCulture)}, ACC {build.TotalAccuracy.ToString(CultureInfo.InvariantCulture)}, EVA {build.TotalAvoidability.ToString(CultureInfo.InvariantCulture)}."
                });
            }

            string subtitle = build == null
                ? "UIWindow2.img/Ranking stays the owner seam while the recovered CWebWnd request shape remains unresolved."
                : $"UIWindow2.img/Ranking stays the owner seam while the recovered CWebWnd request queues NavigateUrl for {build.Name}, world {worldRequestId}, then swaps from loading into simulator-local ladder context.";

            return new RankingWindowSnapshot
            {
                Title = "Ranking",
                Subtitle = subtitle,
                StatusText = "BtRank now mirrors the client owner lifecycle more closely: loading-layer request first, navigated local world/job/popularity/combat cards second. StringPool[0xAA2] now resolves the full gamerank.maplestory NavigateUrl target, but remote ladders, returned page payloads, and packet-fed ranking pages are still outside this board.",
                NavigationCaption = usedResolvedTemplate ? "StringPool[0xAA2] NavigateUrl" : "NavigateUrl fallback",
                NavigationSeedText = landingUrl,
                NavigationHostText = hostText,
                NavigationRequestText = requestShapeText,
                NavigationStateText = BuildRankingOwnerLifecycleDetail(build, launchSource, webSeedText, usedResolvedTemplate),
                IsLoading = isLoading,
                Entries = entries
            };
        }

        private EventWindowSnapshot BuildUtilityEventSnapshot()
        {
            int currentTick = Environment.TickCount;
            CharacterBuild build = _playerManager?.Player?.Build;
            QuestAlarmSnapshot questSnapshot = _questRuntime.BuildQuestAlarmSnapshot(build);
            List<EventEntrySnapshot> entries = new();
            AppendPacketOwnedEventAlarmEntries(entries, currentTick);
            int nextSortOrder = entries.Count;

            if (_lastEventOpenTick != int.MinValue)
            {
                string lifecycleDetail = string.IsNullOrWhiteSpace(_lastEventLaunchSource)
                    ? $"Dedicated CUIEventAlarm owner was last launched at tick {_lastEventOpenTick.ToString(CultureInfo.InvariantCulture)}."
                    : $"Dedicated CUIEventAlarm owner was last launched from {_lastEventLaunchSource} at tick {_lastEventOpenTick.ToString(CultureInfo.InvariantCulture)}.";
                entries.Add(new EventEntrySnapshot
                {
                    Title = "Owner Lifecycle",
                    Detail = $"{lifecycleDetail} The client-observed 8 second auto-dismiss remains armed until the user touches the filter or calendar controls.",
                    StatusText = "Running",
                    Status = EventEntryStatus.InProgress,
                    ScheduledAt = DateTime.Today,
                    SourceTick = _lastEventOpenTick,
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
                    SortOrder = nextSortOrder++
                });
            }

            if (!entries.Any(entry => entry.Status == EventEntryStatus.Upcoming))
            {
                entries.Add(new EventEntrySnapshot
                {
                    Title = "Live Network Event Lists",
                    Detail = "UIWindow2.img/EventList art is active, but attendance packets and the official event-feed model are still pending deeper client dispatch work.",
                    StatusText = "Will",
                    Status = EventEntryStatus.Upcoming,
                    ScheduledAt = DateTime.Today.AddDays(1),
                    SortOrder = nextSortOrder++
                });
            }

            return new EventWindowSnapshot
            {
                Title = "Event",
                Subtitle = "EventList row, slot, icon, and calendar art now surface simulator runtime entries plus the latest packet-owned alarm text, tutor, radio, logout-gift, and sound state through an event owner that auto-dismisses like CUIEventAlarm until the user interacts with its WZ-backed controls.",
                StatusText = "BtEvent now exposes packet-owned utility, quest, overlay, tutor, radio, logout-gift, and sound activity through the client event owner, using the WZ-backed filter and calendar surfaces instead of text-only fallbacks. Official attendance, calendar packets, and live network event feeds still remain outside this window.",
                AutoDismissDelayMs = 8000,
                AlarmLines = BuildEventAlarmOwnerLines(currentTick),
                Entries = entries
            };
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
                ScheduledAt = DateTime.Today
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
                worldId + 1,
                characterId,
                out usedResolvedTemplate);
        }

        private string BuildRankingLandingSeed(CharacterBuild build, int worldId, out bool usedResolvedTemplate)
        {
            int characterId = build?.Id ?? 0;
            return ProgressionUtilityParityRules.FormatRankingLandingSeed(
                RankingServerHost,
                RankingStringPoolUrlTemplateId,
                worldId + 1,
                characterId,
                out usedResolvedTemplate);
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
                SourceTick = sourceTick
            };
        }

        private IReadOnlyList<EventAlarmLineSnapshot> BuildEventAlarmOwnerLines(int currentTick)
        {
            List<(string Text, int Tick, bool Highlight)> candidates = new();

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedNoticeMessage))
            {
                candidates.Add(($"Notice: {TruncatePacketOwnedUtilityText(_lastPacketOwnedNoticeMessage, 64)}", _lastPacketOwnedNoticeTick, true));
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedChatMessage))
            {
                candidates.Add(($"Chat: {TruncatePacketOwnedUtilityText(_lastPacketOwnedChatMessage, 64)}", _lastPacketOwnedChatTick, false));
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedBuffzoneMessage))
            {
                candidates.Add(($"Buff zone: {TruncatePacketOwnedUtilityText(_lastPacketOwnedBuffzoneMessage, 60)}", _lastPacketOwnedBuffzoneTick, false));
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedAskApspMessage))
            {
                string apspPrefix = _packetOwnedApspPromptActive ? "AP/SP prompt" : "AP/SP event";
                candidates.Add(($"{apspPrefix}: {TruncatePacketOwnedUtilityText(_lastPacketOwnedAskApspMessage, 58)}", _lastPacketOwnedAskApspTick, false));
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedSkillGuideMessage))
            {
                candidates.Add((
                    _lastPacketOwnedSkillGuideGrade > 0
                        ? $"Skill guide G{_lastPacketOwnedSkillGuideGrade}: {TruncatePacketOwnedUtilityText(_lastPacketOwnedSkillGuideMessage, 53)}"
                        : $"Skill guide: {TruncatePacketOwnedUtilityText(_lastPacketOwnedSkillGuideMessage, 60)}",
                    _lastPacketOwnedSkillGuideTick,
                    false));
            }

            if (_packetOwnedTutorRuntime.IsActive || _packetOwnedTutorRuntime.HasRegisteredTutorVariants)
            {
                candidates.Add((TruncatePacketOwnedUtilityText(DescribePacketOwnedTutorStatus(currentTick), 70), _packetOwnedTutorRuntime.ActiveMessageStartedAt, false));
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedRadioStatusMessage)
                && (!string.Equals(_lastPacketOwnedRadioStatusMessage, "Packet-owned radio idle.", StringComparison.OrdinalIgnoreCase)
                    || IsPacketOwnedRadioPlaying()))
            {
                int radioTick = _lastPacketOwnedRadioLastPollTick != int.MinValue
                    ? _lastPacketOwnedRadioLastPollTick
                    : _lastPacketOwnedRadioStartTick;
                candidates.Add((
                    IsPacketOwnedRadioPlaying()
                        ? $"Radio: {TruncatePacketOwnedUtilityText(_lastPacketOwnedRadioDisplayName ?? _lastPacketOwnedRadioTrackDescriptor, 60)}"
                        : $"Radio: {TruncatePacketOwnedUtilityText(_lastPacketOwnedRadioStatusMessage, 60)}",
                    radioTick,
                    false));
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedFollowFailureMessage))
            {
                candidates.Add(($"Follow: {TruncatePacketOwnedUtilityText(_lastPacketOwnedFollowFailureMessage, 62)}", _lastPacketOwnedFollowFailureTick, false));
            }

            if (!string.IsNullOrWhiteSpace(_lastPacketOwnedLogoutGiftSummary)
                && !string.Equals(_lastPacketOwnedLogoutGiftSummary, "Packet-owned logout gift idle.", StringComparison.OrdinalIgnoreCase))
            {
                int logoutGiftTick = _lastPacketOwnedLogoutGiftSelectionTick != int.MinValue
                    ? _lastPacketOwnedLogoutGiftSelectionTick
                    : _lastPacketOwnedLogoutGiftRefreshTick;
                candidates.Add(($"Logout gift: {TruncatePacketOwnedUtilityText(_lastPacketOwnedLogoutGiftSummary, 56)}", logoutGiftTick, false));
            }

            if (candidates.Count == 0)
            {
                return new[]
                {
                    CreateEventAlarmLine("No packet-authored event alarm text is active.", 0)
                };
            }

            List<EventAlarmLineSnapshot> lines = candidates
                .OrderBy(candidate => ResolveEventAlarmLineAge(candidate.Tick, currentTick))
                .ThenByDescending(candidate => candidate.Highlight)
                .Take(EventAlarmOwnerMaxVisibleLines)
                .Select((candidate, index) => CreateEventAlarmLine(candidate.Text, index, candidate.Highlight))
                .ToList();

            return lines;
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
