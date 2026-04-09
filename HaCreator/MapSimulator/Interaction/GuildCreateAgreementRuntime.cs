using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class GuildCreateAgreementRuntime
    {
        internal const int IntroRevealDurationMs = 4460;
        internal const int ChoiceTimeoutMs = 30000;

        private bool _isOpen;
        private bool _timedOut;
        private int _elapsedMs;
        private int _choiceRemainingMs;
        private string _masterName = "Guild Master";
        private string _guildName = "New Guild";
        private GuildDialogContext _dialogContext = new(
            "No Guild",
            "Member",
            Array.Empty<string>(),
            string.Empty,
            true,
            Array.Empty<GuildRankingSeedEntry>());
        private string _statusMessage = "Guild creation agreement is idle.";

        internal string Open(string masterName, string guildName, GuildDialogContext dialogContext)
        {
            _isOpen = true;
            _timedOut = false;
            _elapsedMs = 0;
            _choiceRemainingMs = ChoiceTimeoutMs;
            _dialogContext = dialogContext;
            _masterName = string.IsNullOrWhiteSpace(masterName) ? "Guild Master" : masterName.Trim();
            _guildName = string.IsNullOrWhiteSpace(guildName) ? "New Guild" : guildName.Trim();
            _statusMessage = $"Opened guild creation agreement for {_masterName} creating {_guildName}. The guild-management seam now supplies the active role and admission state, while packet or script entry still remains outside the simulator.";
            return _statusMessage;
        }

        internal string Advance(int elapsedMs)
        {
            if (!_isOpen || elapsedMs <= 0)
            {
                return null;
            }

            if (_elapsedMs < IntroRevealDurationMs)
            {
                _elapsedMs = Math.Min(IntroRevealDurationMs, _elapsedMs + elapsedMs);
                return null;
            }

            _choiceRemainingMs = Math.Max(0, _choiceRemainingMs - elapsedMs);
            if (_choiceRemainingMs > 0)
            {
                return null;
            }

            _isOpen = false;
            _timedOut = true;
            string timeoutText = MapleStoryStringPool.GetOrFallback(0x015A, "The guild creation agreement timed out.");
            _statusMessage = $"{timeoutText} ({_masterName}, {_guildName})";
            return _statusMessage;
        }

        internal string Accept(out GuildCreateAgreementAcceptance acceptance)
        {
            acceptance = default;
            if (!_isOpen)
            {
                return _timedOut
                    ? "The guild creation agreement already timed out."
                    : "No guild creation agreement is active.";
            }

            if (!IsInteractive)
            {
                return "Guild creation agreement buttons unlock after the intro and message reveal complete.";
            }

            _isOpen = false;
            acceptance = new GuildCreateAgreementAcceptance(_masterName, _guildName, DateTimeOffset.UtcNow);
            _statusMessage = $"Accepted guild creation agreement for {_masterName} and {_guildName}. The simulator now hands the acceptance back to the shared guild seam, but a real server-backed guild record still remains outside this runtime.";
            return _statusMessage;
        }

        internal string Decline()
        {
            if (!_isOpen)
            {
                return _timedOut
                    ? "The guild creation agreement already timed out."
                    : "No guild creation agreement is active.";
            }

            _isOpen = false;
            _statusMessage = $"Declined guild creation agreement for {_masterName} and {_guildName}.";
            return _statusMessage;
        }

        internal string DescribeStatus()
        {
            string state = !_isOpen
                ? (_timedOut ? "timed out" : "idle")
                : (IsInteractive ? "awaiting choice" : "revealing");
            return $"Guild creation agreement {state}: master={_masterName}, guild={_guildName}, wait={Math.Max(0, _choiceRemainingMs)}ms. {_statusMessage}";
        }

        internal GuildCreateAgreementSnapshot BuildSnapshot()
        {
            return new GuildCreateAgreementSnapshot
            {
                IsOpen = _isOpen,
                ShowMessage = _isOpen && _elapsedMs >= 2460,
                IsInteractive = IsInteractive,
                TimedOut = _timedOut,
                MasterName = _masterName,
                GuildName = _guildName,
                ChoiceRemainingMs = _choiceRemainingMs,
                IntroRemainingMs = Math.Max(0, IntroRevealDurationMs - _elapsedMs),
                StatusMessage = _statusMessage
            };
        }

        private bool IsInteractive => _isOpen && _elapsedMs >= IntroRevealDurationMs;
    }

    internal sealed class GuildCreateAgreementSnapshot
    {
        public bool IsOpen { get; init; }
        public bool ShowMessage { get; init; }
        public bool IsInteractive { get; init; }
        public bool TimedOut { get; init; }
        public int IntroRemainingMs { get; init; }
        public int ChoiceRemainingMs { get; init; }
        public string MasterName { get; init; } = string.Empty;
        public string GuildName { get; init; } = string.Empty;
        public string StatusMessage { get; init; } = string.Empty;
    }
}
