using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using MapleLib.WzLib.WzProperties;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Interaction;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator.Fields
{
    public sealed class FieldRuleRuntime
    {
        private const int DefaultDecIntervalMs = 10000;
        private const int DefaultRecoveryIntervalMs = 10000;
        private static readonly int[] TimeWarningThresholdsSeconds = { 60, 30, 10, 5, 4, 3, 2, 1 };

        private readonly int _timeLimitSeconds;
        private readonly int _transferMapId;
        private readonly int _decHp;
        private readonly int _decIntervalMs;
        private readonly float _recoveryRate;
        private readonly long _fieldLimit;
        private readonly WeatherType _ambientWeather;
        private readonly List<int> _allowedItems;
        private readonly List<int> _protectItems;
        private readonly int? _moveLimit;
        private readonly int _consumeItemCoolTimeSeconds;
        private readonly List<string> _entryScripts;
        private readonly Func<int, bool> _hasItem;
        private readonly Func<int, int> _resolveEnvironmentalDamageProtectionAmount;

        private readonly HashSet<int> _announcedThresholds = new HashSet<int>();
        private int _remainingMoveSkillUses;
        private int _enteredAt;
        private int _nextDamageAt;
        private int _nextRecoveryAt;
        private int _nextConsumableItemUseAt;
        private bool _initialized;
        private bool _timeExpired;

        public FieldRuleRuntime(
            MapInfo mapInfo,
            Func<int, bool> hasItem = null,
            Func<int, int> resolveEnvironmentalDamageProtectionAmount = null,
            bool includeFirstUserEnterScript = true)
        {
            _timeLimitSeconds = Math.Max(0, mapInfo?.timeLimit ?? 0);
            _transferMapId = ResolveTransferMapId(mapInfo);
            _decHp = Math.Max(0, mapInfo?.decHP ?? 0);
            _decIntervalMs = NormalizeDecIntervalMs(mapInfo?.decInterval);
            _recoveryRate = NormalizeRecoveryRate(mapInfo?.recovery);
            _fieldLimit = mapInfo?.fieldLimit ?? 0;
            _ambientWeather = FieldEnvironmentEffectEvaluator.ResolveAmbientWeather(mapInfo);
            _allowedItems = mapInfo?.allowedItem != null ? new List<int>(mapInfo.allowedItem) : new List<int>();
            _protectItems = mapInfo?.protectItem != null ? new List<int>(mapInfo.protectItem) : new List<int>();
            _moveLimit = NormalizeMoveLimit(mapInfo?.moveLimit);
            _consumeItemCoolTimeSeconds = Math.Max(0, mapInfo?.consumeItemCoolTime ?? 0);
            _entryScripts = CollectEntryScripts(mapInfo, includeFirstUserEnterScript);
            _hasItem = hasItem;
            _resolveEnvironmentalDamageProtectionAmount = resolveEnvironmentalDamageProtectionAmount;
        }

        public bool IsActive =>
            _timeLimitSeconds > 0 ||
            _decHp > 0 ||
            _recoveryRate > 0f ||
            _ambientWeather != WeatherType.None ||
            _allowedItems.Count > 0 ||
            FieldInteractionRestrictionEvaluator.GetFieldEntryItemRestrictionMessages(_fieldLimit).Count > 0 ||
            FieldInteractionRestrictionEvaluator.GetFieldEntryInteractionRestrictionMessages(_fieldLimit).Count > 0 ||
            FieldInteractionRestrictionEvaluator.GetJumpRestrictionMessage(_fieldLimit) != null ||
            FieldInteractionRestrictionEvaluator.GetTeleportItemRestrictionMessage(_fieldLimit) != null ||
            FieldInteractionRestrictionEvaluator.GetTransferRestrictionMessage(_fieldLimit) != null ||
            FieldInteractionRestrictionEvaluator.GetMiniGameRestrictionMessage(_fieldLimit) != null ||
            FieldInteractionRestrictionEvaluator.GetPetRuntimeRestrictionMessage(_fieldLimit) != null ||
            FieldInteractionRestrictionEvaluator.GetTamingMobRestrictionMessage(_fieldLimit) != null ||
            FieldInteractionRestrictionEvaluator.GetMonsterCapacityLimitMessage(_fieldLimit) != null ||
            FieldInteractionRestrictionEvaluator.GetExpDecreaseRestrictionMessage(_fieldLimit) != null ||
            FieldInteractionRestrictionEvaluator.GetItemOptionLimitMessage(_fieldLimit) != null ||
            FieldInteractionRestrictionEvaluator.GetAutoExpandMinimapMessage(_fieldLimit) != null ||
            FieldSkillRestrictionEvaluator.HasFieldEntryNotice(_fieldLimit) ||
            _moveLimit.HasValue ||
            _entryScripts.Count > 0 ||
            _consumeItemCoolTimeSeconds > 0;

        /// <summary>
        /// Passive transfer-field retry now reads through the live field-runtime seam
        /// rather than re-querying map info inline. Time-expired fields also stop
        /// advertising retry readiness once they have taken over transfer ownership.
        /// </summary>
        public bool CanTransferField =>
            !_timeExpired &&
            FieldInteractionRestrictionEvaluator.CanTransferField(_fieldLimit);

        public IReadOnlyList<string> Reset(int currentTimeMs)
        {
            _enteredAt = currentTimeMs;
            _nextDamageAt = _decHp > 0 ? currentTimeMs + _decIntervalMs : int.MaxValue;
            _nextRecoveryAt = _recoveryRate > 0f ? currentTimeMs + DefaultRecoveryIntervalMs : int.MaxValue;
            _nextConsumableItemUseAt = currentTimeMs;
            _remainingMoveSkillUses = _moveLimit ?? 0;
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
                if (!HasProtectItemEquipped())
                {
                    int protectedDamage = Math.Max(0, _decHp - GetEnvironmentalDamageProtectionAmount(currentTimeMs));
                    if (protectedDamage > 0)
                    {
                        result.EnvironmentalDamage = protectedDamage;
                        result.TriggerDamageMist = true;
                    }
                }

                do
                {
                    _nextDamageAt += _decIntervalMs;
                }
                while (_nextDamageAt <= currentTimeMs);
            }

            if (_recoveryRate > 0f && playerAlive && !pendingMapChange && currentTimeMs >= _nextRecoveryAt)
            {
                result.HpRecoveryPercent = _recoveryRate;
                result.MpRecoveryPercent = _recoveryRate;

                do
                {
                    _nextRecoveryAt += DefaultRecoveryIntervalMs;
                }
                while (_nextRecoveryAt <= currentTimeMs);
            }

            return result;
        }

        public bool CanUseItem(int itemId)
        {
            return GetItemUseRestrictionMessage(InventoryType.NONE, itemId, 0) == null;
        }

        public string GetItemUseRestrictionMessage(
            InventoryType inventoryType,
            int itemId,
            int currentTimeMs,
            bool usesSharedConsumeItemCooldown = false)
        {
            string consumeCooldownMessage = GetConsumeItemCooldownRestrictionMessage(
                inventoryType,
                currentTimeMs,
                usesSharedConsumeItemCooldown);
            if (!string.IsNullOrWhiteSpace(consumeCooldownMessage))
            {
                return consumeCooldownMessage;
            }

            if (itemId <= 0 || _allowedItems.Count == 0 || _allowedItems.Contains(itemId))
            {
                return null;
            }

            return $"Item {itemId} cannot be used in this field. Allowed item IDs: {FormatItemPreview(_allowedItems)}.";
        }

        public void RegisterSuccessfulItemUse(bool usesSharedConsumeItemCooldown, int currentTimeMs)
        {
            if (!usesSharedConsumeItemCooldown || _consumeItemCoolTimeSeconds <= 0)
            {
                return;
            }

            _nextConsumableItemUseAt = currentTimeMs + (_consumeItemCoolTimeSeconds * 1000);
        }

        public bool CanUseSkill(SkillData skill)
        {
            return GetSkillRestrictionMessage(skill) == null;
        }

        public string GetSkillRestrictionMessage(SkillData skill)
        {
            if (_moveLimit is not > 0 || skill?.IsMovement != true)
            {
                return null;
            }

            return _remainingMoveSkillUses > 0
                ? null
                : $"Movement skills can only be used {_moveLimit.Value} time(s) in this map.";
        }

        public void RegisterSuccessfulSkillUse(SkillData skill)
        {
            if (_moveLimit is not > 0 || skill?.IsMovement != true || _remainingMoveSkillUses <= 0)
            {
                return;
            }

            _remainingMoveSkillUses--;
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
                    messages.Add(
                        $"Environmental damage: {_decHp} HP every {intervalText}. Protect item IDs ({FormatItemPreview(_protectItems)}) suppress this damage while held.");
                }
                else
                {
                    messages.Add($"Environmental damage: {_decHp} HP every {intervalText}.");
                }
            }

            if (_recoveryRate > 0f)
            {
                messages.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Field recovery active: {0:0.###}% HP/MP every {1}.",
                        _recoveryRate,
                        FormatInterval(DefaultRecoveryIntervalMs)));
            }

            string ambientWeatherNotice = _ambientWeather switch
            {
                WeatherType.Rain => "Ambient field weather: rain.",
                WeatherType.Snow => "Ambient field weather: snow.",
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(ambientWeatherNotice))
            {
                messages.Add(ambientWeatherNotice);
            }

            if (_allowedItems.Count > 0)
            {
                messages.Add($"Allowed-item rule active ({_allowedItems.Count} item(s)): {FormatItemPreview(_allowedItems)}.");
            }

            if (_consumeItemCoolTimeSeconds > 0)
            {
                messages.Add($"Consumable item cooldown active: {_consumeItemCoolTimeSeconds}s between use-item activations.");
            }

            IReadOnlyList<string> itemRestrictionNotices = FieldInteractionRestrictionEvaluator.GetFieldEntryItemRestrictionMessages(_fieldLimit);
            for (int i = 0; i < itemRestrictionNotices.Count; i++)
            {
                messages.Add(itemRestrictionNotices[i]);
            }

            IReadOnlyList<string> interactionRestrictionNotices = FieldInteractionRestrictionEvaluator.GetFieldEntryInteractionRestrictionMessages(_fieldLimit);
            for (int i = 0; i < interactionRestrictionNotices.Count; i++)
            {
                messages.Add(interactionRestrictionNotices[i]);
            }

            if (_moveLimit is > 0)
            {
                messages.Add($"Movement skill limit active: {_moveLimit.Value} use(s) in this field.");
            }

            string teleportItemRestrictionNotice = FieldInteractionRestrictionEvaluator.GetTeleportItemRestrictionMessage(_fieldLimit);
            if (!string.IsNullOrWhiteSpace(teleportItemRestrictionNotice))
            {
                messages.Add(teleportItemRestrictionNotice);
            }

            string transferRestrictionNotice = FieldInteractionRestrictionEvaluator.GetTransferRestrictionMessage(_fieldLimit);
            if (!string.IsNullOrWhiteSpace(transferRestrictionNotice))
            {
                messages.Add(transferRestrictionNotice.Replace("field", "map"));
            }

            string jumpRestrictionNotice = FieldInteractionRestrictionEvaluator.GetJumpRestrictionMessage(_fieldLimit);
            if (!string.IsNullOrWhiteSpace(jumpRestrictionNotice))
            {
                messages.Add(jumpRestrictionNotice);
            }

            string skillRestrictionNotice = FieldSkillRestrictionEvaluator.GetFieldEntryNotice(_fieldLimit);
            if (!string.IsNullOrWhiteSpace(skillRestrictionNotice))
            {
                messages.Add(skillRestrictionNotice);
            }

            string autoExpandMinimapNotice = FieldInteractionRestrictionEvaluator.GetAutoExpandMinimapMessage(_fieldLimit);
            if (!string.IsNullOrWhiteSpace(autoExpandMinimapNotice))
            {
                messages.Add(autoExpandMinimapNotice);
            }

            string fieldScriptNotice = BuildFieldScriptNotice();
            if (!string.IsNullOrWhiteSpace(fieldScriptNotice))
            {
                messages.Add(fieldScriptNotice);
            }

            return messages;
        }

        private string BuildFieldScriptNotice()
        {
            if (_entryScripts.Count == 0)
            {
                return null;
            }

            return $"Field scripts detected but not executed by the simulator: {string.Join(", ", _entryScripts)}.";
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

        private static int? NormalizeMoveLimit(int? moveLimit)
        {
            return moveLimit is > 0 ? moveLimit : null;
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

        private static float NormalizeRecoveryRate(float? recoveryRate)
        {
            return recoveryRate is > 0f
                ? recoveryRate.Value
                : 0f;
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

        private static List<string> CollectEntryScripts(MapInfo mapInfo, bool includeFirstUserEnterScript)
        {
            var scripts = new List<string>();
            var seenScripts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddScriptIfPresent(scripts, seenScripts, "onUserEnter", mapInfo?.onUserEnter);
            if (includeFirstUserEnterScript)
            {
                AddScriptIfPresent(scripts, seenScripts, "onFirstUserEnter", mapInfo?.onFirstUserEnter);
            }

            foreach (string fieldScript in QuestRuntimeManager.ParseScriptNames(mapInfo?.Image?["info"]?["fieldScript"]))
            {
                AddScriptIfPresent(scripts, seenScripts, "fieldScript", fieldScript);
            }

            return scripts;
        }

        private static void AddScriptIfPresent(
            ICollection<string> scripts,
            ISet<string> seenScripts,
            string sourceName,
            string scriptName)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                return;
            }

            string normalizedScript = scriptName.Trim();
            string dedupeKey = string.Concat(sourceName, ":", normalizedScript);
            if (!seenScripts.Add(dedupeKey))
            {
                return;
            }

            scripts.Add($"{sourceName}={normalizedScript}");
        }

        private bool HasProtectItemEquipped()
        {
            if (_hasItem == null || _protectItems.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < _protectItems.Count; i++)
            {
                if (_hasItem(_protectItems[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private int GetEnvironmentalDamageProtectionAmount(int currentTimeMs)
        {
            return _resolveEnvironmentalDamageProtectionAmount == null
                ? 0
                : Math.Max(0, _resolveEnvironmentalDamageProtectionAmount(currentTimeMs));
        }

        private string GetConsumeItemCooldownRestrictionMessage(
            InventoryType inventoryType,
            int currentTimeMs,
            bool usesSharedConsumeItemCooldown)
        {
            if (!usesSharedConsumeItemCooldown
                || inventoryType == InventoryType.NONE
                || _consumeItemCoolTimeSeconds <= 0
                || currentTimeMs >= _nextConsumableItemUseAt)
            {
                return null;
            }

            int remainingSeconds = Math.Max(1, (int)Math.Ceiling((_nextConsumableItemUseAt - currentTimeMs) / 1000d));
            return $"Consumable items are on cooldown in this map. {FormatDurationSeconds(remainingSeconds)} remaining.";
        }
    }

    public sealed class FieldRuleUpdateResult
    {
        public List<string> Messages { get; } = new List<string>();
        public List<string> OverlayMessages { get; } = new List<string>();
        public int EnvironmentalDamage { get; set; }
        public float HpRecoveryPercent { get; set; }
        public float MpRecoveryPercent { get; set; }
        public bool TriggerDamageMist { get; set; }
        public int TransferMapId { get; set; } = -1;
    }
}
