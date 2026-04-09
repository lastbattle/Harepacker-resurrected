using System;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum ReviveOwnerVariant
    {
        DefaultOnly = 0,
        SafetyCharmChoice = 1,
        PremiumSafetyCharmChoice = 2,
        UpgradeTombChoice = 3,
        SoulStoneChoice = 4,
        PremiumChoice = PremiumSafetyCharmChoice,
    }

    internal readonly struct ReviveOwnerResolution
    {
        public ReviveOwnerResolution(bool handled, bool premium, bool timedOut, ReviveOwnerVariant variant, string summary)
        {
            Handled = handled;
            Premium = premium;
            TimedOut = timedOut;
            Variant = variant;
            Summary = summary ?? string.Empty;
        }

        public bool Handled { get; }
        public bool Premium { get; }
        public bool TimedOut { get; }
        public ReviveOwnerVariant Variant { get; }
        public string Summary { get; }
    }

    internal readonly struct ReviveOwnerTransferRequest
    {
        public ReviveOwnerTransferRequest(bool premium, bool timedOut, ReviveOwnerVariant variant, string summary)
        {
            Premium = premium;
            TimedOut = timedOut;
            Variant = variant;
            Summary = summary ?? string.Empty;
        }

        public bool Premium { get; }
        public bool TimedOut { get; }
        public ReviveOwnerVariant Variant { get; }
        public string Summary { get; }
    }

    internal sealed class ReviveOwnerSnapshot
    {
        public bool IsOpen { get; init; }
        public bool HasPremiumChoice { get; init; }
        public ReviveOwnerVariant Variant { get; init; }
        public string Title { get; init; } = "Revive";
        public string Subtitle { get; init; } = string.Empty;
        public string PrimaryTitle { get; init; } = string.Empty;
        public string PrimaryDetail { get; init; } = string.Empty;
        public string SecondaryTitle { get; init; } = string.Empty;
        public string SecondaryDetail { get; init; } = string.Empty;
        public string CountdownText { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public string PremiumButtonLabel { get; init; } = "Yes";
        public string NormalButtonLabel { get; init; } = "No";
        public float RemainingRatio { get; init; }
    }

    internal sealed class ReviveOwnerRuntime
    {
        private const int AutoResolveMs = 0x927C0;
        private const float PremiumChoiceMinDistanceSquared = 1f;

        private int _openedAtTick = int.MinValue;
        private string _mapName = string.Empty;
        private string _normalDetail = string.Empty;
        private string _premiumDetail = string.Empty;
        private string _ownerLabel = string.Empty;

        public bool IsOpen => _openedAtTick != int.MinValue;
        public ReviveOwnerVariant Variant { get; private set; }
        public bool HasPremiumChoice => Variant != ReviveOwnerVariant.DefaultOnly;

        public void Open(
            string mapName,
            string normalDetail,
            string premiumDetail,
            ReviveOwnerVariant variant,
            int currentTick)
        {
            _mapName = mapName ?? string.Empty;
            _normalDetail = normalDetail ?? string.Empty;
            _premiumDetail = premiumDetail ?? string.Empty;
            Variant = variant;
            _ownerLabel = GetOwnerLabel(variant);
            _openedAtTick = currentTick;
        }

        public void Open(
            string mapName,
            string normalDetail,
            string premiumDetail,
            ReviveOwnerVariant variant)
        {
            Open(mapName, normalDetail, premiumDetail, variant, Environment.TickCount);
        }

        public void Close()
        {
            _openedAtTick = int.MinValue;
            Variant = ReviveOwnerVariant.DefaultOnly;
            _mapName = string.Empty;
            _normalDetail = string.Empty;
            _premiumDetail = string.Empty;
            _ownerLabel = string.Empty;
        }

        public ReviveOwnerResolution Update(int currentTick)
        {
            if (!IsOpen)
            {
                return default;
            }

            if (unchecked(currentTick - _openedAtTick) <= AutoResolveMs)
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

            ReviveOwnerVariant variant = Variant;
            bool resolvedPremium = premium && HasPremiumChoice;
            string ownerLabel = string.IsNullOrWhiteSpace(_ownerLabel) ? "revive owner" : _ownerLabel;
            string summary = resolvedPremium
                ? $"CUIRevive premium recovery branch confirmed through {ownerLabel}."
                : timedOut
                    ? $"CUIRevive timed out and resolved through the default revive branch ({ownerLabel})."
                    : $"CUIRevive default recovery branch confirmed through {ownerLabel}.";

            Close();
            return new ReviveOwnerResolution(true, resolvedPremium, timedOut, variant, summary);
        }

        public static ReviveOwnerTransferRequest CreateTransferRequest(ReviveOwnerResolution resolution)
        {
            return new ReviveOwnerTransferRequest(
                resolution.Premium,
                resolution.TimedOut,
                resolution.Variant,
                resolution.Summary);
        }

        public static ReviveOwnerVariant ResolveClientVariant(
            bool hasSoulStone,
            bool hasUpgradeTombChoice,
            bool hasPremiumSafetyCharm,
            bool hasSafetyCharm)
        {
            if (hasSoulStone)
            {
                return ReviveOwnerVariant.SoulStoneChoice;
            }

            if (hasUpgradeTombChoice)
            {
                return ReviveOwnerVariant.UpgradeTombChoice;
            }

            if (hasPremiumSafetyCharm)
            {
                return ReviveOwnerVariant.PremiumSafetyCharmChoice;
            }

            if (hasSafetyCharm)
            {
                return ReviveOwnerVariant.SafetyCharmChoice;
            }

            return ReviveOwnerVariant.DefaultOnly;
        }

        public static bool ShouldOfferPremiumChoice(Vector2 deathPoint, Vector2 spawnPoint)
        {
            return Vector2.DistanceSquared(deathPoint, spawnPoint) > PremiumChoiceMinDistanceSquared;
        }

        public static bool HasPremiumChoiceForVariant(ReviveOwnerVariant variant)
        {
            return variant != ReviveOwnerVariant.DefaultOnly;
        }

        public static bool UsesCurrentFieldRespawn(ReviveOwnerVariant variant)
        {
            return variant != ReviveOwnerVariant.DefaultOnly;
        }

        public static int GetConsumableCashItemId(ReviveOwnerVariant variant)
        {
            return variant switch
            {
                // WZ evidence:
                // - String/Cash.img/5130000/name -> "Safety Charm"
                // - String/Cash.img/5131000/name -> "Premium Safety Charm"
                // - String/Cash.img/5510000/name -> "Wheel of Fortune"
                ReviveOwnerVariant.SafetyCharmChoice => 5130000,
                ReviveOwnerVariant.PremiumSafetyCharmChoice => 5131000,
                ReviveOwnerVariant.UpgradeTombChoice => 5510000,
                _ => 0,
            };
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
            string variantStatus = Variant switch
            {
                ReviveOwnerVariant.SafetyCharmChoice => "Safety Charm branch available.",
                ReviveOwnerVariant.PremiumSafetyCharmChoice => "Premium Safety Charm branch available.",
                ReviveOwnerVariant.UpgradeTombChoice => "Upgrade Tomb branch available.",
                ReviveOwnerVariant.SoulStoneChoice => "Soul Stone branch available.",
                _ => "Default revive branch only."
            };

            return new ReviveOwnerSnapshot
            {
                IsOpen = true,
                HasPremiumChoice = HasPremiumChoice,
                Variant = Variant,
                Title = "Revive",
                Subtitle = subtitle,
                PrimaryTitle = ResolvePrimaryTitle(Variant),
                PrimaryDetail = HasPremiumChoice ? _premiumDetail : _normalDetail,
                SecondaryTitle = ResolveSecondaryTitle(Variant),
                SecondaryDetail = HasPremiumChoice ? _normalDetail : "This simulator-local owner still uses one recovery branch when no alternate local revive path is available.",
                CountdownText = $"Auto revive in {remaining.Minutes:00}:{remaining.Seconds:00}",
                StatusText = HasPremiumChoice
                    ? $"{variantStatus} Enter or Shift+R takes the premium branch. Escape, the close button, or R takes the default branch."
                    : $"{variantStatus} Enter, the close button, or R confirms the default revive branch.",
                PremiumButtonLabel = "Yes",
                NormalButtonLabel = HasPremiumChoice ? "No" : "OK",
                RemainingRatio = AutoResolveMs <= 0 ? 0f : MathHelper.Clamp((float)remainingMs / AutoResolveMs, 0f, 1f)
            };
        }

        public static string GetOwnerLabel(ReviveOwnerVariant variant)
        {
            return variant switch
            {
                ReviveOwnerVariant.SafetyCharmChoice => "Safety Charm",
                ReviveOwnerVariant.PremiumSafetyCharmChoice => "Premium Safety Charm",
                ReviveOwnerVariant.UpgradeTombChoice => "Upgrade Tomb",
                ReviveOwnerVariant.SoulStoneChoice => "Soul Stone",
                _ => "Default revive owner"
            };
        }

        private static string ResolvePrimaryTitle(ReviveOwnerVariant variant)
        {
            return variant switch
            {
                ReviveOwnerVariant.SoulStoneChoice => "Current Field Recovery",
                ReviveOwnerVariant.UpgradeTombChoice => "Current Field Recovery",
                ReviveOwnerVariant.PremiumSafetyCharmChoice => "Current Field Recovery",
                ReviveOwnerVariant.SafetyCharmChoice => "Current Field Recovery",
                _ => "Return to Safety"
            };
        }

        private static string ResolveSecondaryTitle(ReviveOwnerVariant variant)
        {
            return variant == ReviveOwnerVariant.DefaultOnly
                ? string.Empty
                : "Return to Safety";
        }
    }
}
