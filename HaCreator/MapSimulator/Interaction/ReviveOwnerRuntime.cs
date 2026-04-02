using System;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Interaction
{
    internal readonly struct ReviveOwnerResolution
    {
        public ReviveOwnerResolution(bool handled, bool premium, bool timedOut, string summary)
        {
            Handled = handled;
            Premium = premium;
            TimedOut = timedOut;
            Summary = summary ?? string.Empty;
        }

        public bool Handled { get; }
        public bool Premium { get; }
        public bool TimedOut { get; }
        public string Summary { get; }
    }

    internal sealed class ReviveOwnerSnapshot
    {
        public bool IsOpen { get; init; }
        public bool HasPremiumChoice { get; init; }
        public string Title { get; init; } = "Revive";
        public string Subtitle { get; init; } = string.Empty;
        public string PrimaryDetail { get; init; } = string.Empty;
        public string SecondaryDetail { get; init; } = string.Empty;
        public string CountdownText { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public string PremiumButtonLabel { get; init; } = "Yes";
        public string NormalButtonLabel { get; init; } = "No";
    }

    internal sealed class ReviveOwnerRuntime
    {
        private const int AutoResolveMs = 10 * 60 * 1000;

        private int _openedAtTick = int.MinValue;
        private string _mapName = string.Empty;
        private string _normalDetail = string.Empty;
        private string _premiumDetail = string.Empty;

        public bool IsOpen => _openedAtTick != int.MinValue;
        public bool HasPremiumChoice { get; private set; }

        public void Open(
            string mapName,
            bool hasPremiumChoice,
            string normalDetail,
            string premiumDetail,
            int currentTick)
        {
            _mapName = mapName ?? string.Empty;
            _normalDetail = normalDetail ?? string.Empty;
            _premiumDetail = premiumDetail ?? string.Empty;
            HasPremiumChoice = hasPremiumChoice;
            _openedAtTick = currentTick;
        }

        public void Close()
        {
            _openedAtTick = int.MinValue;
            HasPremiumChoice = false;
            _mapName = string.Empty;
            _normalDetail = string.Empty;
            _premiumDetail = string.Empty;
        }

        public ReviveOwnerResolution Update(int currentTick)
        {
            if (!IsOpen)
            {
                return default;
            }

            if (unchecked(currentTick - _openedAtTick) < AutoResolveMs)
            {
                return default;
            }

            return Resolve(premium: false, timedOut: true);
        }

        public ReviveOwnerResolution Resolve(bool premium, bool timedOut = false)
        {
            if (!IsOpen)
            {
                return default;
            }

            bool resolvedPremium = premium && HasPremiumChoice;
            string summary = resolvedPremium
                ? "CUIRevive premium recovery branch confirmed."
                : timedOut
                    ? "CUIRevive timed out and resolved through the default revive branch."
                    : "CUIRevive default recovery branch confirmed.";

            Close();
            return new ReviveOwnerResolution(true, resolvedPremium, timedOut, summary);
        }

        public ReviveOwnerSnapshot BuildSnapshot(int currentTick)
        {
            if (!IsOpen)
            {
                return new ReviveOwnerSnapshot();
            }

            int remainingMs = Math.Max(0, AutoResolveMs - unchecked(currentTick - _openedAtTick));
            TimeSpan remaining = TimeSpan.FromMilliseconds(remainingMs);
            string subtitle = string.IsNullOrWhiteSpace(_mapName)
                ? "Dedicated death-recovery owner"
                : $"Dedicated death-recovery owner for {_mapName}";

            return new ReviveOwnerSnapshot
            {
                IsOpen = true,
                HasPremiumChoice = HasPremiumChoice,
                Title = "Revive",
                Subtitle = subtitle,
                PrimaryDetail = HasPremiumChoice ? _premiumDetail : _normalDetail,
                SecondaryDetail = HasPremiumChoice ? _normalDetail : "This simulator-local owner still uses one recovery branch when no alternate local revive path is available.",
                CountdownText = $"Auto revive in {remaining.Minutes:00}:{remaining.Seconds:00}",
                StatusText = HasPremiumChoice
                    ? "Enter or Shift+R takes the premium branch. Escape or R takes the default branch."
                    : "Enter or R confirms the default revive branch.",
                PremiumButtonLabel = "Yes",
                NormalButtonLabel = HasPremiumChoice ? "No" : "OK"
            };
        }

        public static bool ShouldOfferPremiumChoice(Vector2 deathPosition, Vector2 respawnPosition)
        {
            return Vector2.DistanceSquared(deathPosition, respawnPosition) >= 32f * 32f;
        }
    }
}
