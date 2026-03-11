using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator.Fields
{
    public sealed class FieldRuleRuntime
    {
        private const int DefaultDecIntervalMs = 10000;
        private static readonly int[] TimeWarningThresholdsSeconds = { 60, 30, 10, 5, 4, 3, 2, 1 };

        private readonly int _timeLimitSeconds;
        private readonly int _transferMapId;
        private readonly int _decHp;
        private readonly int _decIntervalMs;
        private readonly long _fieldLimit;
        private readonly List<int> _allowedItems;
        private readonly List<int> _protectItems;
        private readonly int? _levelLimit;
        private readonly int? _moveLimit;
        private readonly bool _partyOnly;
        private readonly bool _expeditionOnly;
        private readonly bool _consumeItemCoolTime;

        private readonly HashSet<int> _announcedThresholds = new HashSet<int>();
        private int _enteredAt;
        private int _nextDamageAt;
        private bool _initialized;
        private bool _timeExpired;

        public FieldRuleRuntime(MapInfo mapInfo)
        {
            _timeLimitSeconds = Math.Max(0, mapInfo?.timeLimit ?? 0);
            _transferMapId = ResolveTransferMapId(mapInfo);
            _decHp = Math.Max(0, mapInfo?.decHP ?? 0);
            _decIntervalMs = NormalizeDecIntervalMs(mapInfo?.decInterval);
            _fieldLimit = mapInfo?.fieldLimit ?? 0;
            _allowedItems = mapInfo?.allowedItem != null ? new List<int>(mapInfo.allowedItem) : new List<int>();
            _protectItems = mapInfo?.protectItem != null ? new List<int>(mapInfo.protectItem) : new List<int>();
            _levelLimit = mapInfo?.lvLimit;
            _moveLimit = mapInfo?.moveLimit;
            _partyOnly = mapInfo?.partyOnly == true;
            _expeditionOnly = mapInfo?.expeditionOnly == true;
            _consumeItemCoolTime = mapInfo?.consumeItemCoolTime == true;
        }

        public bool IsActive =>
            _timeLimitSeconds > 0 ||
            _decHp > 0 ||
            _allowedItems.Count > 0 ||
            _levelLimit.HasValue ||
            FieldInteractionRestrictionEvaluator.GetTransferRestrictionMessage(_fieldLimit) != null ||
            FieldSkillRestrictionEvaluator.HasFieldEntryNotice(_fieldLimit) ||
            _moveLimit.HasValue ||
            _partyOnly ||
            _expeditionOnly ||
            _consumeItemCoolTime;

        public IReadOnlyList<string> Reset(int currentTimeMs)
        {
            _enteredAt = currentTimeMs;
            _nextDamageAt = _decHp > 0 ? currentTimeMs + _decIntervalMs : int.MaxValue;
            _initialized = true;
            _timeExpired = false;
            _announcedThresholds.Clear();
            return BuildEntryMessages();
        }

        public FieldRuleUpdateResult Update(int currentTimeMs, bool playerAlive, bool pendingMapChange)
        {
            var result = new FieldRuleUpdateResult();
            if (!_initialized)
            {
                foreach (string message in Reset(currentTimeMs))
                {
                    result.Messages.Add(message);
                }
            }

            if (_timeLimitSeconds > 0 && !_timeExpired)
            {
                int remainingMs = (_timeLimitSeconds * 1000) - (currentTimeMs - _enteredAt);
                int remainingSeconds = Math.Max(0, (int)Math.Ceiling(remainingMs / 1000d));

                for (int i = 0; i < TimeWarningThresholdsSeconds.Length; i++)
                {
                    int threshold = TimeWarningThresholdsSeconds[i];
                    if (remainingSeconds > threshold || !_announcedThresholds.Add(threshold))
                    {
                        continue;
                    }

                    string warning = $"Field timer: {FormatDurationSeconds(remainingSeconds)} remaining.";
                    result.Messages.Add(warning);
                    result.OverlayMessages.Add(warning);
                }

                if (remainingMs <= 0)
                {
                    _timeExpired = true;

                    string expiryMessage = _transferMapId > 0
                        ? $"Field timer expired. Returning to map {_transferMapId}."
                        : "Field timer expired.";
                    result.Messages.Add(expiryMessage);
                    result.OverlayMessages.Add(expiryMessage);

                    if (!pendingMapChange && _transferMapId > 0)
                    {
                        result.TransferMapId = _transferMapId;
                    }
                }
            }

            if (_decHp > 0 && playerAlive && !pendingMapChange && currentTimeMs >= _nextDamageAt)
            {
                result.EnvironmentalDamage = _decHp;
                result.TriggerDamageMist = true;

                do
                {
                    _nextDamageAt += _decIntervalMs;
                }
                while (_nextDamageAt <= currentTimeMs);
            }

            return result;
        }

        private IReadOnlyList<string> BuildEntryMessages()
        {
            List<string> messages = new List<string>();

            if (_timeLimitSeconds > 0)
            {
                messages.Add($"Field timer started: {FormatDurationSeconds(_timeLimitSeconds)}.");
            }

            if (_decHp > 0)
            {
                string intervalText = FormatInterval(_decIntervalMs);
                if (_protectItems.Count > 0)
                {
                    messages.Add($"Environmental damage: {_decHp} HP every {intervalText}. Protect item IDs exist ({FormatItemPreview(_protectItems)}), but inventory protection is not modeled.");
                }
                else
                {
                    messages.Add($"Environmental damage: {_decHp} HP every {intervalText}.");
                }
            }

            if (_allowedItems.Count > 0)
            {
                messages.Add($"Allowed-item rule active ({_allowedItems.Count} item(s)): {FormatItemPreview(_allowedItems)}.");
            }

            string transferRestrictionNotice = FieldInteractionRestrictionEvaluator.GetTransferRestrictionMessage(_fieldLimit);
            if (!string.IsNullOrWhiteSpace(transferRestrictionNotice))
            {
                messages.Add(transferRestrictionNotice.Replace("field", "map"));
            }

            string skillRestrictionNotice = FieldSkillRestrictionEvaluator.GetFieldEntryNotice(_fieldLimit);
            if (!string.IsNullOrWhiteSpace(skillRestrictionNotice))
            {
                messages.Add(skillRestrictionNotice);
            }

            string partialRestrictionNotice = BuildPartialRestrictionNotice();
            if (!string.IsNullOrWhiteSpace(partialRestrictionNotice))
            {
                messages.Add(partialRestrictionNotice);
            }

            return messages;
        }

        private string BuildPartialRestrictionNotice()
        {
            List<string> notices = new List<string>();

            if (_levelLimit.HasValue)
            {
                notices.Add($"lvLimit={_levelLimit.Value}");
            }

            if (_moveLimit.HasValue)
            {
                notices.Add($"moveLimit={_moveLimit.Value}");
            }

            if (_partyOnly)
            {
                notices.Add("partyOnly");
            }

            if (_expeditionOnly)
            {
                notices.Add("expeditionOnly");
            }

            if (_consumeItemCoolTime)
            {
                notices.Add("consumeItemCoolTime");
            }

            if (notices.Count == 0)
            {
                return null;
            }

            return $"Field metadata present but only surfaced as notices in the simulator: {string.Join(", ", notices)}.";
        }

        private static int ResolveTransferMapId(MapInfo mapInfo)
        {
            int returnMap = mapInfo?.returnMap ?? 0;
            if (returnMap > 0 && returnMap != MapConstants.MaxMap)
            {
                return returnMap;
            }

            int forcedReturn = mapInfo?.forcedReturn ?? 0;
            return forcedReturn > 0 && forcedReturn != MapConstants.MaxMap
                ? forcedReturn
                : -1;
        }

        private static int NormalizeDecIntervalMs(int? decInterval)
        {
            if (!decInterval.HasValue || decInterval.Value <= 0)
            {
                return DefaultDecIntervalMs;
            }

            return decInterval.Value < 1000
                ? decInterval.Value * 1000
                : decInterval.Value;
        }

        private static string FormatDurationSeconds(int totalSeconds)
        {
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return minutes > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", minutes, seconds)
                : string.Format(CultureInfo.InvariantCulture, "{0}s", seconds);
        }

        private static string FormatInterval(int intervalMs)
        {
            double seconds = intervalMs / 1000d;
            return string.Format(CultureInfo.InvariantCulture, "{0:0.###}s", seconds);
        }

        private static string FormatItemPreview(IEnumerable<int> itemIds)
        {
            List<int> ids = itemIds.Take(4).ToList();
            string suffix = itemIds.Skip(4).Any() ? ", ..." : string.Empty;
            return string.Join(", ", ids) + suffix;
        }
    }

    public sealed class FieldRuleUpdateResult
    {
        public List<string> Messages { get; } = new List<string>();
        public List<string> OverlayMessages { get; } = new List<string>();
        public int EnvironmentalDamage { get; set; }
        public bool TriggerDamageMist { get; set; }
        public int TransferMapId { get; set; } = -1;
    }
}
