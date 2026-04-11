using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal readonly struct AccountMoreInfoBackgroundResourceCandidate
    {
        internal AccountMoreInfoBackgroundResourceCandidate(
            string path,
            bool mirrorsClientSetBackgrnd,
            int composedWidth = 0,
            int composedHeight = 0)
        {
            Path = path ?? string.Empty;
            MirrorsClientSetBackgrnd = mirrorsClientSetBackgrnd;
            ComposedWidth = Math.Max(0, composedWidth);
            ComposedHeight = Math.Max(0, composedHeight);
        }

        internal string Path { get; }

        internal bool MirrorsClientSetBackgrnd { get; }

        internal int ComposedWidth { get; }

        internal int ComposedHeight { get; }

        internal bool RequiresSimulatorComposition => !MirrorsClientSetBackgrnd
            && ComposedWidth > 0
            && ComposedHeight > 0;
    }

    internal static class AccountMoreInfoOwnerStringPoolText
    {
        // CUIAccountMoreInfo's recovered control coordinates extend to the OK /
        // Cancel buttons at x=345, so mounted fallback shells need this owner
        // size even when the exact StringPool 0x16AE canvas is unavailable.
        internal const int ClientOwnerWidth = 398;
        internal const int ClientOwnerHeight = 355;
        internal const int OkButtonUolStringPoolId = 0x512;
        internal const int CancelButtonUolStringPoolId = 0x513;
        internal const int BackgroundStringPoolId = 0x16AE;
        internal const int ExitWithoutInfoNoticeStringPoolId = 0x16B6;
        internal const int SaveFailedNoticeStringPoolId = 0x16B7;
        internal const int DefaultRegionItemStringPoolId = 0x16B8;
        internal const int FirstEntryPromptStringPoolId = 0x16B5;

        private const string OkButtonUolFallback = "UI/Basic.img/BtOK2";
        private const string CancelButtonUolFallback = "UI/Basic.img/BtCancel2";
        private const string BackgroundFallback = "UI/UIWindow.img/FriendRecommendations/UserInfo/back";
        private static readonly string[] MountedBackgroundRecoveryCandidates =
        {
            // Active WZ evidence: the mounted UI set exposes `UserInfo/backgrnd7`
            // while the older FriendRecommendations path is absent. Keep the
            // larger mounted shell first so the recovered control layout has a
            // usable owner frame when the exact string-pool skin is unavailable.
            "UI/UIWindow.img/UserInfo/backgrnd7",
            "UI/UIWindow.img/UserInfo/backgrnd8",
        };
        private const string ExitWithoutInfoNoticeFallback = "Are you sure you want to exit without filling in any information? (You can fill out your info later by clicking My Info in the Friends window.)";
        private const string SaveFailedNoticeFallback = "Fail. Please try again later.";
        private const string DefaultRegionItemFallback = "Select";
        private const string FirstEntryPromptFallback = "Filling in your information will help us recommend friends who share your interests! \r\nDo you want to fill in your information now?";

        internal static string ResolveOkButtonResourcePath()
        {
            return MapleStoryStringPool.GetOrFallback(
                OkButtonUolStringPoolId,
                OkButtonUolFallback,
                appendFallbackSuffix: true);
        }

        internal static string ResolveCancelButtonResourcePath()
        {
            return MapleStoryStringPool.GetOrFallback(
                CancelButtonUolStringPoolId,
                CancelButtonUolFallback,
                appendFallbackSuffix: true);
        }

        internal static string ResolveBackgroundResourcePath()
        {
            foreach (string candidate in EnumerateBackgroundResourcePaths())
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }

            return $"{BackgroundFallback} ({MapleStoryStringPool.FormatFallbackLabel(BackgroundStringPoolId)} fallback)";
        }

        internal static string ResolveExactBackgroundResourcePath()
        {
            return MapleStoryStringPool.GetOrNull(BackgroundStringPoolId) ?? BackgroundFallback;
        }

        internal static bool IsExactBackgroundResourcePath(string candidate)
        {
            string exactPath = ResolveExactBackgroundResourcePath();
            return !string.IsNullOrWhiteSpace(candidate)
                && string.Equals(candidate, exactPath, StringComparison.OrdinalIgnoreCase);
        }

        internal static IReadOnlyList<string> EnumerateBackgroundResourcePaths()
        {
            List<string> candidates = new();
            foreach (AccountMoreInfoBackgroundResourceCandidate candidate in EnumerateBackgroundResourceCandidates())
            {
                AddDistinctCandidate(candidates, candidate.Path);
            }

            return candidates;
        }

        internal static IReadOnlyList<AccountMoreInfoBackgroundResourceCandidate> EnumerateBackgroundResourceCandidates()
        {
            List<string> candidates = new();
            List<AccountMoreInfoBackgroundResourceCandidate> typedCandidates = new();

            // The generated table in this workspace currently resolves 0x16AE to
            // a FriendRecommendations path that is absent from the active mounted
            // UI set. Probe the mounted `UserInfo` shells first, then keep the
            // exact client string-pool path as the final non-fabricated fallback.
            // `CUIAccountMoreInfo::OnCreate` passes zero offset, no multi-part
            // mode, and zero expansion to CWnd::SetBackgrnd for 0x16AE, so only
            // the exact string-pool path can use raw SetBackgrnd canvas sizing.
            foreach (string candidate in MountedBackgroundRecoveryCandidates)
            {
                AddDistinctCandidate(
                    typedCandidates,
                    candidates,
                    candidate,
                    mirrorsClientSetBackgrnd: false,
                    composedWidth: ClientOwnerWidth,
                    composedHeight: ClientOwnerHeight);
            }

            AddDistinctCandidate(
                typedCandidates,
                candidates,
                ResolveExactBackgroundResourcePath(),
                mirrorsClientSetBackgrnd: true);
            return typedCandidates;
        }

        private static void AddDistinctCandidate(ICollection<string> candidates, string candidate)
        {
            if (candidates == null || string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            foreach (string existing in candidates)
            {
                if (string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            candidates.Add(candidate);
        }

        private static void AddDistinctCandidate(
            ICollection<AccountMoreInfoBackgroundResourceCandidate> typedCandidates,
            ICollection<string> pathCandidates,
            string candidate,
            bool mirrorsClientSetBackgrnd,
            int composedWidth = 0,
            int composedHeight = 0)
        {
            if (typedCandidates == null || pathCandidates == null || string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            foreach (string existing in pathCandidates)
            {
                if (string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            pathCandidates.Add(candidate);
            typedCandidates.Add(new AccountMoreInfoBackgroundResourceCandidate(
                candidate,
                mirrorsClientSetBackgrnd,
                composedWidth,
                composedHeight));
        }

        internal static string ResolveExitWithoutInfoNotice()
        {
            return MapleStoryStringPool.GetOrFallback(
                ExitWithoutInfoNoticeStringPoolId,
                ExitWithoutInfoNoticeFallback,
                appendFallbackSuffix: true);
        }

        internal static string ResolveSaveFailedNotice()
        {
            return MapleStoryStringPool.GetOrFallback(
                SaveFailedNoticeStringPoolId,
                SaveFailedNoticeFallback,
                appendFallbackSuffix: true);
        }

        internal static string ResolveDefaultRegionItem()
        {
            return MapleStoryStringPool.GetOrFallback(
                DefaultRegionItemStringPoolId,
                DefaultRegionItemFallback,
                appendFallbackSuffix: true);
        }

        internal static string ResolveFirstEntryPrompt()
        {
            return MapleStoryStringPool.GetOrFallback(
                FirstEntryPromptStringPoolId,
                FirstEntryPromptFallback,
                appendFallbackSuffix: true);
        }
    }
}
