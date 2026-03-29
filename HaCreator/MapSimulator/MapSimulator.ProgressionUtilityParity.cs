using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private RankingWindowSnapshot BuildUtilityRankingSnapshot()
        {
            CharacterBuild build = _playerManager?.Player?.Build;
            QuestAlarmSnapshot questSnapshot = _questRuntime.BuildQuestAlarmSnapshot(build);
            string mapName = GetCurrentMapTransferDisplayName();
            int currentMapId = _mapBoard?.MapInfo?.id ?? 0;
            int readyQuestCount = questSnapshot.Entries.Count(entry => entry.IsReadyToComplete);
            int worldId = Math.Max(0, _simulatorWorldId);
            string webSeedText = build == null
                ? $"ranking://world/{worldId + 1}"
                : $"ranking://world/{worldId + 1}/character/{build.Id}";

            List<RankingEntrySnapshot> entries = new();
            if (build == null)
            {
                entries.Add(new RankingEntrySnapshot
                {
                    Label = "Landing Page",
                    Value = "Unavailable",
                    Detail = $"CUIRanking is a web owner, but there is no active character build to seed its landing request. Current local seed: {webSeedText}."
                });
            }
            else
            {
                entries.Add(new RankingEntrySnapshot
                {
                    Label = "Landing Page",
                    Value = $"World {worldId + 1}",
                    Detail = $"CUIRanking::OnCreate navigates through a web-style owner, so the simulator now exposes the local landing seed as {webSeedText} for {build.Name}."
                });
                entries.Add(new RankingEntrySnapshot
                {
                    Label = "World Rank",
                    Value = build.WorldRank > 0 ? $"#{build.WorldRank}" : "Local",
                    Detail = build.WorldRank > 0
                        ? $"World-ranking seed is attached to {build.Name} and would populate the remote page after the landing request resolves."
                        : "No packet-authored world-ranking feed is present, so this landing page remains simulator-local after the initial web-style seed."
                });
                entries.Add(new RankingEntrySnapshot
                {
                    Label = "Job Rank",
                    Value = build.JobRank > 0 ? $"#{build.JobRank}" : "Local",
                    Detail = build.JobRank > 0
                        ? $"{build.JobName} ladder seed is loaded for the active build after the landing request."
                        : $"No live {build.JobName} job-ranking packet feed is present behind the web owner."
                });
                entries.Add(new RankingEntrySnapshot
                {
                    Label = "Popularity",
                    Value = build.Fame.ToString(CultureInfo.InvariantCulture),
                    Detail = $"Lv. {build.Level} {build.JobName}, EXP {build.ExpPercent}% in {mapName}, AP {build.AP.ToString(CultureInfo.InvariantCulture)}."
                });
                entries.Add(new RankingEntrySnapshot
                {
                    Label = "Combat Seed",
                    Value = $"{build.TotalAttack}/{build.TotalMagicAttack}",
                    Detail = readyQuestCount > 0
                        ? $"PAD/MAD with {readyQuestCount} tracked quest turn-in candidate{(readyQuestCount == 1 ? string.Empty : "s")} waiting."
                        : $"PAD/MAD with ACC {build.TotalAccuracy.ToString(CultureInfo.InvariantCulture)} and EVA {build.TotalAvoidability.ToString(CultureInfo.InvariantCulture)}."
                });
            }

            string subtitle = build == null
                ? "Ranking owner art is loaded from UIWindow2.img/Ranking."
                : $"Dedicated ranking owner anchored to {build.Name}, Lv. {build.Level} {build.JobName}, map {currentMapId}, plus the client-observed web landing seed for world {worldId + 1}.";

            return new RankingWindowSnapshot
            {
                Title = "Ranking",
                Subtitle = subtitle,
                StatusText = "BtRank now follows the client owner split more closely: CUIRanking is treated as a close-only web landing owner with simulator-local page seeds. The live URL template, remote ladders, and packet-fed ranking pages are still outside this board.",
                Entries = entries
            };
        }

        private EventWindowSnapshot BuildUtilityEventSnapshot()
        {
            int currentTick = Environment.TickCount;
            CharacterBuild build = _playerManager?.Player?.Build;
            QuestAlarmSnapshot questSnapshot = _questRuntime.BuildQuestAlarmSnapshot(build);
            List<EventEntrySnapshot> entries = new();

            string loginStatus = _loginRuntime.LastEventSummary;
            entries.Add(new EventEntrySnapshot
            {
                Title = "Login Bootstrap Feed",
                Detail = $"{_loginRuntime.CurrentStep} step. {loginStatus}",
                StatusText = _loginRuntime.HasWorldInformation ? "Clear" : (_loginRuntime.LastPacketType.HasValue ? "Running" : "Start"),
                Status = _loginRuntime.HasWorldInformation ? EventEntryStatus.Clear : (_loginRuntime.LastPacketType.HasValue ? EventEntryStatus.InProgress : EventEntryStatus.Start),
                ScheduledAt = DateTime.Today
            });

            string packetFieldState = _packetFieldStateRuntime.DescribeStatus(currentTick);
            entries.Add(new EventEntrySnapshot
            {
                Title = "Packet Field State",
                Detail = packetFieldState,
                StatusText = packetFieldState.Contains("idle", StringComparison.OrdinalIgnoreCase) ? "Start" : "Running",
                Status = packetFieldState.Contains("idle", StringComparison.OrdinalIgnoreCase) ? EventEntryStatus.Start : EventEntryStatus.InProgress,
                ScheduledAt = DateTime.Today
            });

            string overlayStatus = DescribePacketOwnedFieldFadeAndBalloonStatus(currentTick);
            entries.Add(new EventEntrySnapshot
            {
                Title = "Local Overlay Feed",
                Detail = overlayStatus,
                StatusText = overlayStatus.Contains("idle", StringComparison.OrdinalIgnoreCase) ? "Start" : "Running",
                Status = overlayStatus.Contains("idle", StringComparison.OrdinalIgnoreCase) ? EventEntryStatus.Start : EventEntryStatus.InProgress,
                ScheduledAt = DateTime.Today
            });

            string guideDetail = _packetQuestGuideQuestId > 0
                ? $"Quest #{_packetQuestGuideQuestId} is keeping packet-authored world-map targets alive."
                : "No packet-authored quest-guide target is currently active.";
            entries.Add(new EventEntrySnapshot
            {
                Title = "Quest Guide Routing",
                Detail = guideDetail,
                StatusText = _packetQuestGuideQuestId > 0 ? "Running" : "Start",
                Status = _packetQuestGuideQuestId > 0 ? EventEntryStatus.InProgress : EventEntryStatus.Start,
                ScheduledAt = DateTime.Today
            });

            if (_lastDeliveryQuestId > 0)
            {
                entries.Add(new EventEntrySnapshot
                {
                    Title = "Quest Delivery Launch",
                    Detail = $"Quest #{_lastDeliveryQuestId} delivery owner is primed for item #{_lastDeliveryItemId}.",
                    StatusText = "Running",
                    Status = EventEntryStatus.InProgress,
                    ScheduledAt = DateTime.Today
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
                    ScheduledAt = DateTime.Today
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
                    ScheduledAt = DateTime.Today
                });
            }

            foreach (EventEntrySnapshot fieldEntry in BuildSpecialFieldEventEntries())
            {
                entries.Add(fieldEntry);
            }

            if (!entries.Any(entry => entry.Status == EventEntryStatus.Upcoming))
            {
                entries.Add(new EventEntrySnapshot
                {
                    Title = "Live Network Event Lists",
                    Detail = "UIWindow2.img/EventList art is active, but attendance packets and the official event-feed model are still pending deeper client dispatch work.",
                    StatusText = "Will",
                    Status = EventEntryStatus.Upcoming,
                    ScheduledAt = DateTime.Today.AddDays(1)
                });
            }

            return new EventWindowSnapshot
            {
                Title = "Event",
                Subtitle = "EventList row, slot, icon, and calendar art now surface simulator runtime entries through an event owner that auto-dismisses like CUIEventAlarm until the user interacts with its WZ-backed controls.",
                StatusText = "BtEvent now exposes packet-owned utility, quest, overlay, and special-field activity through the client event owner, using the WZ-backed filter and calendar surfaces instead of text-only fallbacks. Official attendance, calendar packets, and live network event feeds still remain outside this window.",
                AutoDismissDelayMs = 8000,
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
    }
}
