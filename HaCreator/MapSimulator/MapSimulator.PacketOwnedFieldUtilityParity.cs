using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.Character;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private sealed class PacketFieldUtilityStalkMarkerState
        {
            public int CharacterId { get; init; }
            public string Name { get; init; }
            public Vector2 Position { get; set; }
        }

        private readonly PacketFieldUtilityRuntime _packetFieldUtilityRuntime = new();
        private readonly Dictionary<int, PacketFieldUtilityStalkMarkerState> _packetFieldUtilityStalkTargets = new();
        private readonly List<PacketFieldUtilityFootholdEntry> _packetFieldUtilityFootholdEntries = new();
        private readonly Dictionary<int, string> _packetFieldUtilityFootholdNamesBySerial = new();
        private const int PacketOwnedFootholdInfoResponseOpcode = 270;
        private int[] _packetFieldUtilityQuickslotKeyCodes;
        private bool _packetFieldUtilityWeatherOverrideActive;
        private int _packetFieldUtilityWeatherItemId;
        private WeatherType _packetFieldUtilityWeatherType = WeatherType.None;
        private string _packetFieldUtilityWeatherPath;
        private string _packetFieldUtilityWeatherMessage;
        private string _packetFieldUtilityQuizSummary;
        private string _packetFieldUtilityQuizDisplayText;
        private int _packetFieldUtilityQuizDisplayExpiresAt;
        private int _packetFieldUtilityQuizTimerExpiresAt;
        private Texture2D _packetFieldUtilityStatusNoticeIcon;
        private bool _packetFieldUtilityMinimapHiddenByAdminResult;
        private string _packetFieldUtilityFootholdRequestSummary = "No packet-owned foothold-info request has been handled.";
        private string _packetFieldUtilityFootholdOfficialResponseSummary = "No packet-owned foothold-info response payload has been prepared.";

        private bool TryApplyPacketOwnedFieldUtilityPacket(int packetType, byte[] payload, out string message)
        {
            if (!TryParsePacketFieldUtilityKind(packetType, out PacketFieldUtilityPacketKind kind))
            {
                message = $"Unsupported field utility packet type {packetType}.";
                return false;
            }

            return _packetFieldUtilityRuntime.TryApplyPacket(
                kind,
                payload,
                BuildPacketFieldUtilityCallbacks(),
                out message);
        }

        private PacketFieldUtilityCallbacks BuildPacketFieldUtilityCallbacks()
        {
            return new PacketFieldUtilityCallbacks
            {
                ResolveWeatherItemPath = ResolvePacketOwnedFieldUtilityWeatherPath,
                ApplyWeather = ApplyPacketOwnedFieldUtilityWeather,
                PresentAdminResult = PresentPacketOwnedAdminResult,
                PresentQuizState = PresentPacketOwnedQuizState,
                UpsertStalkTarget = UpsertPacketOwnedStalkTarget,
                RemoveStalkTarget = RemovePacketOwnedStalkTarget,
                ApplyQuickslotKeyMap = ApplyPacketOwnedQuickslotKeyMap,
                ApplyFootholdInfo = ApplyPacketOwnedFootholdInfo,
                RequestFootholdInfo = HandlePacketOwnedFootholdInfoRequest
            };
        }

        private string ResolvePacketOwnedFieldUtilityWeatherPath(int itemId)
        {
            return InventoryItemMetadataResolver.TryResolveItemInfoPath(itemId, out string path)
                ? path
                : null;
        }

        private void ApplyPacketOwnedFieldUtilityWeather(int itemId, byte blowType, string weatherPath, string message)
        {
            bool clearWeather = itemId <= 0 || string.Equals(weatherPath, "Map/MapHelper.img/weather/none", StringComparison.OrdinalIgnoreCase);
            if (clearWeather)
            {
                _packetFieldUtilityWeatherOverrideActive = false;
                _packetFieldUtilityWeatherItemId = 0;
                _packetFieldUtilityWeatherType = WeatherType.None;
                _packetFieldUtilityWeatherPath = null;
                _packetFieldUtilityWeatherMessage = null;
                ResetPacketOwnedAnimationDisplayerTransientLayers();
                _fieldEffects?.StopWeather();
                return;
            }

            _packetFieldUtilityWeatherOverrideActive = true;
            _packetFieldUtilityWeatherItemId = itemId;
            _packetFieldUtilityWeatherPath = weatherPath;
            _packetFieldUtilityWeatherType = ResolvePacketOwnedWeatherType(weatherPath);
            _packetFieldUtilityWeatherMessage = string.IsNullOrWhiteSpace(message) ? null : message.Trim();

            ApplyPacketOwnedAnimationDisplayerTransientLayer(itemId, weatherPath);
            ToggleWeather(_packetFieldUtilityWeatherType);
            _fieldEffects?.OnBlowWeather(
                ConvertToFieldWeatherEffect(_packetFieldUtilityWeatherType),
                itemId.ToString(CultureInfo.InvariantCulture),
                _packetFieldUtilityWeatherMessage,
                1f,
                -1,
                currTickCount);

            if (!string.IsNullOrWhiteSpace(_packetFieldUtilityWeatherMessage))
            {
                _chat?.AddClientChatMessage($"[Weather] {_packetFieldUtilityWeatherMessage}", currTickCount, 12);
            }
        }

        private bool TryApplyPacketOwnedFieldUtilityWeatherOverride(int currentTime)
        {
            if (!_packetFieldUtilityWeatherOverrideActive)
            {
                return false;
            }

            ToggleWeather(_packetFieldUtilityWeatherType);
            _fieldEffects?.OnBlowWeather(
                ConvertToFieldWeatherEffect(_packetFieldUtilityWeatherType),
                _packetFieldUtilityWeatherItemId > 0 ? _packetFieldUtilityWeatherItemId.ToString(CultureInfo.InvariantCulture) : null,
                null,
                1f,
                -1,
                currentTime);
            return true;
        }

        private static WeatherType ResolvePacketOwnedWeatherType(string weatherPath)
        {
            if (string.IsNullOrWhiteSpace(weatherPath))
            {
                return WeatherType.None;
            }

            string normalized = weatherPath.Replace('\\', '/');
            if (normalized.Contains("/snow", StringComparison.OrdinalIgnoreCase))
            {
                return WeatherType.Snow;
            }

            if (normalized.Contains("/maple", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("/leaf", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("/flower", StringComparison.OrdinalIgnoreCase))
            {
                return WeatherType.Leaves;
            }

            if (normalized.Contains("/rain", StringComparison.OrdinalIgnoreCase))
            {
                return WeatherType.Rain;
            }

            return WeatherType.None;
        }

        private void PresentPacketOwnedAdminResult(PacketFieldUtilityAdminResult result)
        {
            if (result == null)
            {
                return;
            }

            if (result.AppendChatLogEntry && !string.IsNullOrWhiteSpace(result.Body))
            {
                _chat?.AddClientChatMessage(result.Body, currTickCount, result.ChatLogType);
            }

            if (result.ReloadMinimap)
            {
                _mapBoard?.RegenerateMinimap();
                if (_packetFieldUtilityMinimapHiddenByAdminResult)
                {
                    miniMapUi?.EnsureCollapsed();
                }
            }

            if (result.ToggleMinimap)
            {
                _packetFieldUtilityMinimapHiddenByAdminResult = true;
                miniMapUi?.EnsureCollapsed();
            }
        }

        private void PresentPacketOwnedQuizState(bool isQuestion, byte category, ushort problemId)
        {
            if (problemId == 0)
            {
                _packetFieldUtilityQuizSummary = null;
                _packetFieldUtilityQuizDisplayText = null;
                _packetFieldUtilityQuizDisplayExpiresAt = 0;
                _packetFieldUtilityQuizTimerExpiresAt = 0;
                return;
            }

            PacketOwnedQuizPresentation presentation = ResolvePacketOwnedQuizPresentation(isQuestion, category, problemId);
            _packetFieldUtilityQuizSummary = presentation.Summary;
            _packetFieldUtilityQuizDisplayText = presentation.DisplayText;
            _packetFieldUtilityQuizDisplayExpiresAt = currTickCount + 5000;
            _packetFieldUtilityQuizTimerExpiresAt = presentation.StartEventTimer
                ? currTickCount + 30000
                : 0;
        }

        private void UpsertPacketOwnedStalkTarget(int characterId, string name, int x, int y)
        {
            _packetFieldUtilityStalkTargets[characterId] = new PacketFieldUtilityStalkMarkerState
            {
                CharacterId = characterId,
                Name = string.IsNullOrWhiteSpace(name) ? $"Player {characterId}" : name.Trim(),
                Position = new Vector2(x, y)
            };
        }

        private void RemovePacketOwnedStalkTarget(int characterId)
        {
            _packetFieldUtilityStalkTargets.Remove(characterId);
        }

        private void ApplyPacketOwnedQuickslotKeyMap(int[] keyCodes, bool useDefault)
        {
            _packetFieldUtilityQuickslotKeyCodes = useDefault ? null : keyCodes?.ToArray();
            ApplyPacketOwnedQuickslotBindings(_packetFieldUtilityQuickslotKeyCodes, useDefault);
            SyncPacketOwnedUtilityWindowBindings(_playerManager?.Input);
            PersistPacketOwnedFuncKeyConfig(_playerManager?.Input, persistSimulatorBindings: true);
            uiWindowManager?.QuickSlotWindow?.SetPrimaryBarKeyLabels(BuildPacketOwnedQuickslotLabels(_packetFieldUtilityQuickslotKeyCodes));
        }

        private void ApplyPacketOwnedQuickslotBindings(IReadOnlyList<int> keyCodes, bool useDefault)
        {
            PlayerInput input = _playerManager?.Input;
            if (input == null)
            {
                return;
            }

            for (int i = 0; i < 8; i++)
            {
                InputAction action = InputAction.QuickSlot1 + i;
                if (useDefault || keyCodes == null || keyCodes.Count <= i)
                {
                    RestorePacketOwnedQuickslotDefaultBinding(input, action);
                    continue;
                }

                Keys primaryKey = ResolvePacketOwnedScanCodeKey(keyCodes[i]);
                KeyBinding existingBinding = input.GetBinding(action);
                if (existingBinding != null && existingBinding.PrimaryKey == primaryKey)
                {
                    continue;
                }

                input.SetBinding(
                    action,
                    primaryKey,
                    existingBinding?.SecondaryKey ?? Keys.None,
                    existingBinding?.GamepadButton ?? (Buttons)0);
            }
        }

        private static void RestorePacketOwnedQuickslotDefaultBinding(PlayerInput input, InputAction action)
        {
            if (input == null)
            {
                return;
            }

            foreach ((InputAction defaultAction, Keys primary, Keys secondary, Buttons gamepad) in PlayerInput.GetDefaultBindings())
            {
                if (defaultAction != action)
                {
                    continue;
                }

                input.SetBinding(action, primary, secondary, gamepad);
                return;
            }

            input.SetBinding(action, Keys.None, Keys.None, (Buttons)0);
        }

        private static string[] BuildPacketOwnedQuickslotLabels(int[] keyCodes)
        {
            if (keyCodes == null || keyCodes.Length != 8)
            {
                return null;
            }

            string[] labels = new string[keyCodes.Length];
            for (int i = 0; i < keyCodes.Length; i++)
            {
                labels[i] = ResolveQuickslotKeyLabel(keyCodes[i]);
            }

            return labels;
        }

        private static string ResolveQuickslotKeyLabel(int keyCode)
        {
            Keys key = ResolvePacketOwnedScanCodeKey(keyCode);
            return key switch
            {
                Keys.D0 => "0",
                Keys.D1 => "1",
                Keys.D2 => "2",
                Keys.D3 => "3",
                Keys.D4 => "4",
                Keys.D5 => "5",
                Keys.D6 => "6",
                Keys.D7 => "7",
                Keys.D8 => "8",
                Keys.D9 => "9",
                Keys.LeftControl or Keys.RightControl => "Ctrl",
                Keys.LeftShift or Keys.RightShift => "Shift",
                Keys.LeftAlt or Keys.RightAlt => "Alt",
                Keys.PageUp => "PgUp",
                Keys.PageDown => "PgDn",
                Keys.Escape => "Esc",
                Keys.Enter => "Enter",
                Keys.Back => "Bksp",
                Keys.CapsLock => "Caps",
                Keys.Space => "Space",
                Keys.OemOpenBrackets => "[",
                Keys.OemCloseBrackets => "]",
                Keys.OemSemicolon => ";",
                Keys.OemQuotes => "'",
                Keys.OemComma => ",",
                Keys.OemPeriod => ".",
                Keys.OemQuestion => "/",
                Keys.OemPipe => "\\",
                Keys.OemMinus => "-",
                Keys.OemPlus => "=",
                Keys.OemTilde => "`",
                Keys.None => keyCode.ToString(CultureInfo.InvariantCulture),
                _ => key.ToString()
            };
        }

        private void ApplyPacketOwnedFootholdInfo(IReadOnlyList<PacketFieldUtilityFootholdEntry> entries)
        {
            _packetFieldUtilityFootholdEntries.Clear();
            _packetFieldUtilityFootholdNamesBySerial.Clear();
            if (entries != null)
            {
                _packetFieldUtilityFootholdEntries.AddRange(entries);
                CachePacketOwnedFootholdNames(entries);
            }

            for (int i = 0; i < _packetFieldUtilityFootholdEntries.Count; i++)
            {
                ApplyPacketOwnedFootholdEntryToRuntime(_packetFieldUtilityFootholdEntries[i]);
            }
        }

        private void ApplyPacketOwnedFootholdEntryToRuntime(PacketFieldUtilityFootholdEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Name))
            {
                return;
            }

            if (!TryResolvePacketOwnedDynamicPlatform(entry, out DynamicPlatform platform))
            {
                return;
            }

            platform.IsActive = entry.State != 0;
            platform.IsVisible = entry.State != 0;
            if (entry.MovingState != null)
            {
                platform.Speed = Math.Max(0f, entry.MovingState.Speed);
                platform.LeftBound = Math.Min(entry.MovingState.X1, entry.MovingState.X2);
                platform.RightBound = Math.Max(entry.MovingState.X1, entry.MovingState.X2);
                platform.TopBound = Math.Min(entry.MovingState.Y1, entry.MovingState.Y2);
                platform.BottomBound = Math.Max(entry.MovingState.Y1, entry.MovingState.Y2);
                platform.X = entry.MovingState.CurrentX;
                platform.Y = entry.MovingState.CurrentY;
                platform.MovingDown = !entry.MovingState.ReverseVertical;
                platform.MovingRight = !entry.MovingState.ReverseHorizontal;
            }
        }

        private void CachePacketOwnedFootholdNames(IReadOnlyList<PacketFieldUtilityFootholdEntry> entries)
        {
            if (entries == null)
            {
                return;
            }

            foreach (PacketFieldUtilityFootholdEntry entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Name) || entry.FootholdSerialNumbers == null)
                {
                    continue;
                }

                string normalizedName = entry.Name.Trim();
                foreach (int serialNumber in entry.FootholdSerialNumbers)
                {
                    if (serialNumber >= 0)
                    {
                        _packetFieldUtilityFootholdNamesBySerial[serialNumber] = normalizedName;
                    }
                }
            }
        }

        private bool TryResolvePacketOwnedDynamicPlatform(PacketFieldUtilityFootholdEntry entry, out DynamicPlatform platform)
        {
            platform = null;
            // The client resolves dynamic footholds by object name first and only then refreshes the foothold serial list.
            if (TryResolvePacketOwnedDynamicPlatform(entry?.Name, out platform))
            {
                return true;
            }

            if (entry?.FootholdSerialNumbers == null || _dynamicFootholds == null)
            {
                return false;
            }

            foreach (int serialNumber in entry.FootholdSerialNumbers)
            {
                if (serialNumber < 0)
                {
                    continue;
                }

                platform = _dynamicFootholds.GetPlatform(serialNumber);
                if (platform != null)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryResolvePacketOwnedDynamicPlatform(string name, out DynamicPlatform platform)
        {
            platform = null;
            if (string.IsNullOrWhiteSpace(name) || _dynamicFootholds == null)
            {
                return false;
            }

            const string prefix = "platform-";
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(name[prefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int platformId))
            {
                platform = _dynamicFootholds.GetPlatform(platformId);
                if (platform != null)
                {
                    return true;
                }
            }

            if (_dynamicFootholdField != null
                && _dynamicFootholdField.TryResolveAuthoredDynamicObjectPlatformId(name, out int authoredPlatformId))
            {
                platform = _dynamicFootholds.GetPlatform(authoredPlatformId);
                if (platform != null)
                {
                    return true;
                }
            }

            return platform != null;
        }

        private string HandlePacketOwnedFootholdInfoRequest()
        {
            IReadOnlyList<PacketFieldUtilityFootholdEntry> snapshot = BuildPacketOwnedFootholdSnapshot();
            byte[] officialResponsePayload = PacketFieldUtilityRuntime.BuildOfficialSessionFootHoldInfoResponsePayload(snapshot);
            _packetFieldUtilityFootholdRequestSummary = snapshot.Count == 0
                ? "Received packet-owned foothold-info request; no dynamic foothold entries were available to snapshot."
                : $"Received packet-owned foothold-info request; prepared {snapshot.Count} dynamic foothold snapshot entr{(snapshot.Count == 1 ? "y" : "ies")} for the current runtime.";
            _packetFieldUtilityFootholdOfficialResponseSummary = DescribePacketOwnedFootholdOfficialResponse(officialResponsePayload, snapshot.Count);
            return _packetFieldUtilityFootholdRequestSummary;
        }

        private string DescribePacketOwnedFootholdOfficialResponse(byte[] officialResponsePayload, int snapshotCount)
        {
            string payloadSummary = snapshotCount == 0
                ? "Prepared an empty client-shaped foothold-info response payload."
                : $"Prepared client-shaped foothold-info response payload ({officialResponsePayload.Length} byte{(officialResponsePayload.Length == 1 ? string.Empty : "s")}, hex={Convert.ToHexString(officialResponsePayload)}).";

            (bool Success, string Status) TrySendBridge(int opcode, IReadOnlyList<byte> payload)
            {
                if (_localUtilityOfficialSessionBridge == null)
                {
                    return (false, "Local utility official-session bridge is unavailable.");
                }

                bool success = _localUtilityOfficialSessionBridge.TrySendOutboundPacket(opcode, payload, out string status);
                return (success, status);
            }

            (bool Success, string Status) TrySendOutbox(int opcode, IReadOnlyList<byte> payload)
            {
                if (_localUtilityPacketOutbox == null)
                {
                    return (false, "Generic local-utility outbox is unavailable.");
                }

                bool success = _localUtilityPacketOutbox.TrySendOutboundPacket(opcode, payload, out string status);
                return (success, status);
            }

            (bool Success, string Status) TryQueueBridge(int opcode, IReadOnlyList<byte> payload)
            {
                if (_localUtilityOfficialSessionBridge == null)
                {
                    return (false, "Local utility official-session bridge is unavailable.");
                }

                bool success = _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(opcode, payload, out string status);
                return (success, status);
            }

            (bool Success, string Status) TryQueueOutbox(int opcode, IReadOnlyList<byte> payload)
            {
                if (_localUtilityPacketOutbox == null)
                {
                    return (false, "Generic local-utility outbox is unavailable.");
                }

                bool success = _localUtilityPacketOutbox.TryQueueOutboundPacket(opcode, payload, out string status);
                return (success, status);
            }

            return DescribePacketOwnedFootholdOfficialResponseDispatch(
                payloadSummary,
                PacketOwnedFootholdInfoResponseOpcode,
                officialResponsePayload,
                TrySendBridge,
                TrySendOutbox,
                TryQueueBridge,
                TryQueueOutbox,
                allowDeferredBridge: _localUtilityOfficialSessionBridge?.IsRunning == true);
        }

        internal static string DescribePacketOwnedFootholdOfficialResponseDispatch(
            string payloadSummary,
            int opcode,
            IReadOnlyList<byte> payload,
            Func<int, IReadOnlyList<byte>, (bool Success, string Status)> trySendBridge,
            Func<int, IReadOnlyList<byte>, (bool Success, string Status)> trySendOutbox,
            Func<int, IReadOnlyList<byte>, (bool Success, string Status)> tryQueueBridge,
            Func<int, IReadOnlyList<byte>, (bool Success, string Status)> tryQueueOutbox,
            bool allowDeferredBridge)
        {
            string bridgeStatus = "Live official-session bridge transport was not attempted.";
            string outboxStatus = "Generic local-utility outbox transport was not attempted.";

            if (trySendBridge != null)
            {
                (bool success, string status) = trySendBridge(opcode, payload);
                bridgeStatus = string.IsNullOrWhiteSpace(status)
                    ? "Live official-session bridge transport produced no status."
                    : status;
                if (success)
                {
                    return $"{payloadSummary} {bridgeStatus}";
                }
            }

            if (trySendOutbox != null)
            {
                (bool success, string status) = trySendOutbox(opcode, payload);
                outboxStatus = string.IsNullOrWhiteSpace(status)
                    ? "Generic local-utility outbox transport produced no status."
                    : status;
                if (success)
                {
                    return $"{payloadSummary} Dispatched opcode {opcode} through the generic local-utility outbox after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus}";
                }
            }

            if (allowDeferredBridge && tryQueueBridge != null)
            {
                (bool success, string status) = tryQueueBridge(opcode, payload);
                string queuedBridgeStatus = string.IsNullOrWhiteSpace(status)
                    ? "Deferred official-session bridge queueing produced no status."
                    : status;
                if (success)
                {
                    return $"{payloadSummary} Queued opcode {opcode} for deferred official-session injection after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred bridge: {queuedBridgeStatus}";
                }
            }

            if (tryQueueOutbox != null)
            {
                (bool success, string status) = tryQueueOutbox(opcode, payload);
                string queuedOutboxStatus = string.IsNullOrWhiteSpace(status)
                    ? "Deferred generic local-utility outbox queueing produced no status."
                    : status;
                if (success)
                {
                    return $"{payloadSummary} Queued opcode {opcode} for deferred generic local-utility outbox delivery after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred outbox: {queuedOutboxStatus}";
                }
            }

            return $"{payloadSummary} The simulator kept opcode {opcode} local because neither the live bridge nor the packet outbox accepted it. Bridge: {bridgeStatus} Outbox: {outboxStatus}";
        }

        private IReadOnlyList<PacketFieldUtilityFootholdEntry> BuildPacketOwnedFootholdSnapshot()
        {
            int runtimePlatformCount = _dynamicFootholds?.PlatformCount ?? 0;
            int authoredObjectCount = _dynamicFootholdField?.DynamicObjectCount ?? 0;
            if (runtimePlatformCount == 0 && authoredObjectCount == 0)
            {
                return _packetFieldUtilityFootholdEntries.Count == 0
                    ? Array.Empty<PacketFieldUtilityFootholdEntry>()
                    : _packetFieldUtilityFootholdEntries.ToArray();
            }

            int snapshotCount = Math.Max(runtimePlatformCount, authoredObjectCount);
            bool hasLiveRuntimePlatforms = runtimePlatformCount > 0;
            List<PacketFieldUtilityFootholdEntry> entries = new(snapshotCount);
            for (int i = 0; i < snapshotCount; i++)
            {
                if (TryBuildPacketOwnedLiveFootholdSnapshotEntry(i, out PacketFieldUtilityFootholdEntry liveEntry))
                {
                    entries.Add(liveEntry);
                    continue;
                }

                if (!hasLiveRuntimePlatforms
                    && TryBuildPacketOwnedCachedFootholdSnapshotEntry(i, out PacketFieldUtilityFootholdEntry cachedEntry))
                {
                    entries.Add(cachedEntry);
                    continue;
                }

                if (TryBuildPacketOwnedDefaultFootholdSnapshotEntry(i, out PacketFieldUtilityFootholdEntry defaultEntry))
                {
                    entries.Add(defaultEntry);
                }
            }

            if (entries.Count > 0)
            {
                return entries;
            }

            return _packetFieldUtilityFootholdEntries.Count == 0
                ? Array.Empty<PacketFieldUtilityFootholdEntry>()
                : _packetFieldUtilityFootholdEntries.ToArray();
        }

        private bool TryBuildPacketOwnedLiveFootholdSnapshotEntry(int platformId, out PacketFieldUtilityFootholdEntry entry)
        {
            entry = null;
            DynamicPlatform platform = _dynamicFootholds?.GetPlatform(platformId);
            if (platform == null)
            {
                return false;
            }

            entry = new PacketFieldUtilityFootholdEntry(
                ResolvePacketOwnedDynamicPlatformSnapshotName(platformId),
                platform.IsActive ? 2 : 0,
                ResolvePacketOwnedSnapshotSerialNumbers(platformId),
                new PacketFieldUtilityMovingFootholdState(
                    (int)platform.Speed,
                    (int)platform.LeftBound,
                    (int)platform.RightBound,
                    (int)platform.TopBound,
                    (int)platform.BottomBound,
                    (int)platform.X,
                    (int)platform.Y,
                    !platform.MovingDown,
                    !platform.MovingRight));
            return true;
        }

        private bool TryBuildPacketOwnedCachedFootholdSnapshotEntry(int platformId, out PacketFieldUtilityFootholdEntry entry)
        {
            entry = null;
            if (!TryFindPacketOwnedFootholdEntry(platformId, out PacketFieldUtilityFootholdEntry cachedEntry))
            {
                return false;
            }

            string snapshotName = ResolvePacketOwnedDynamicPlatformSnapshotName(platformId);
            entry = new PacketFieldUtilityFootholdEntry(
                string.IsNullOrWhiteSpace(snapshotName) ? cachedEntry.Name : snapshotName,
                cachedEntry.State,
                ResolvePacketOwnedSnapshotSerialNumbers(platformId, cachedEntry),
                cachedEntry.MovingState);
            return true;
        }

        private bool TryBuildPacketOwnedDefaultFootholdSnapshotEntry(int platformId, out PacketFieldUtilityFootholdEntry entry)
        {
            entry = null;
            string snapshotName = ResolvePacketOwnedDynamicPlatformSnapshotName(platformId);
            if (string.IsNullOrWhiteSpace(snapshotName))
            {
                return false;
            }

            entry = new PacketFieldUtilityFootholdEntry(
                snapshotName,
                0,
                ResolvePacketOwnedSnapshotSerialNumbers(platformId),
                null);
            return true;
        }

        private bool TryFindPacketOwnedFootholdEntry(int platformId, out PacketFieldUtilityFootholdEntry entry)
        {
            entry = null;
            for (int i = 0; i < _packetFieldUtilityFootholdEntries.Count; i++)
            {
                PacketFieldUtilityFootholdEntry candidate = _packetFieldUtilityFootholdEntries[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.FootholdSerialNumbers?.Contains(platformId) == true)
                {
                    entry = candidate;
                    return true;
                }
            }

            for (int i = 0; i < _packetFieldUtilityFootholdEntries.Count; i++)
            {
                PacketFieldUtilityFootholdEntry candidate = _packetFieldUtilityFootholdEntries[i];
                if (candidate == null
                    || string.IsNullOrWhiteSpace(candidate.Name)
                    || _dynamicFootholdField == null
                    || !_dynamicFootholdField.TryResolveAuthoredDynamicObjectPlatformId(candidate.Name, out int authoredPlatformId)
                    || authoredPlatformId != platformId)
                {
                    continue;
                }

                entry = candidate;
                return true;
            }

            return false;
        }

        private int[] ResolvePacketOwnedSnapshotSerialNumbers(int platformId, PacketFieldUtilityFootholdEntry cachedEntry = null)
        {
            if (cachedEntry?.FootholdSerialNumbers == null || cachedEntry.FootholdSerialNumbers.Count == 0)
            {
                return new[] { platformId };
            }

            List<int> serialNumbers = new(cachedEntry.FootholdSerialNumbers.Count);
            for (int i = 0; i < cachedEntry.FootholdSerialNumbers.Count; i++)
            {
                int serialNumber = cachedEntry.FootholdSerialNumbers[i];
                if (serialNumber >= 0)
                {
                    serialNumbers.Add(serialNumber);
                }
            }

            return serialNumbers.Count == 0
                ? new[] { platformId }
                : serialNumbers.ToArray();
        }

        private string ResolvePacketOwnedDynamicPlatformSnapshotName(int platformId)
        {
            if (_dynamicFootholdField.TryResolvePacketOwnedSnapshotDynamicObjectName(platformId, out string authoredName)
                && !string.IsNullOrWhiteSpace(authoredName))
            {
                return authoredName;
            }

            if (_packetFieldUtilityFootholdNamesBySerial.TryGetValue(platformId, out string packetName)
                && !string.IsNullOrWhiteSpace(packetName))
            {
                return packetName;
            }

            if (_dynamicFootholdField.TryResolveAuthoredDynamicObjectName(platformId, out authoredName)
                && !string.IsNullOrWhiteSpace(authoredName))
            {
                return authoredName;
            }

            return $"platform-{platformId}";
        }

        private void AppendPacketOwnedStalkTrackedUserMarkers(Dictionary<string, MinimapTrackedUserState> trackedUsers)
        {
            if (_packetFieldUtilityStalkTargets.Count == 0 || trackedUsers == null)
            {
                return;
            }

            foreach (PacketFieldUtilityStalkMarkerState target in _packetFieldUtilityStalkTargets.Values)
            {
                if (!trackedUsers.TryGetValue(target.Name, out MinimapTrackedUserState state))
                {
                    state = new MinimapTrackedUserState(target.Name);
                    trackedUsers[target.Name] = state;
                }

                state.IsStalkTarget = true;
                state.HasPosition = true;
                state.Position = target.Position;
            }
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldUtilityCommand(string[] args)
        {
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(
                    $"{_packetFieldUtilityRuntime.DescribeStatus()}{Environment.NewLine}{_packetFieldUtilityFootholdRequestSummary}{Environment.NewLine}{_packetFieldUtilityFootholdOfficialResponseSummary}");
            }

            if (string.Equals(args[0], "clear", StringComparison.OrdinalIgnoreCase))
            {
                _packetFieldUtilityRuntime.Clear();
                _packetFieldUtilityStalkTargets.Clear();
                _packetFieldUtilityFootholdEntries.Clear();
                _packetFieldUtilityFootholdNamesBySerial.Clear();
                _packetFieldUtilityQuickslotKeyCodes = null;
                _packetFieldUtilityWeatherOverrideActive = false;
                _packetFieldUtilityWeatherItemId = 0;
                _packetFieldUtilityWeatherPath = null;
                _packetFieldUtilityWeatherMessage = null;
                _packetFieldUtilityQuizSummary = null;
                _packetFieldUtilityQuizDisplayText = null;
                _packetFieldUtilityQuizDisplayExpiresAt = 0;
                _packetFieldUtilityQuizTimerExpiresAt = 0;
                _packetFieldUtilityMinimapHiddenByAdminResult = false;
                _packetFieldUtilityFootholdRequestSummary = "No packet-owned foothold-info request has been handled.";
                _packetFieldUtilityFootholdOfficialResponseSummary = "No packet-owned foothold-info response payload has been prepared.";
                ApplyPacketOwnedQuickslotBindings(null, useDefault: true);
                uiWindowManager?.QuickSlotWindow?.SetPrimaryBarKeyLabels(null);
                _fieldEffects?.StopWeather();
                return ChatCommandHandler.CommandResult.Ok(_packetFieldUtilityRuntime.DescribeStatus());
            }

            if (string.Equals(args[0], "packet", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase))
            {
                return HandlePacketOwnedFieldUtilityPacketCommand(
                    args,
                    rawHex: string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase));
            }

            return args[0].ToLowerInvariant() switch
            {
                "weather" => HandlePacketOwnedFieldUtilityWeatherCommand(args),
                "quiz" => HandlePacketOwnedFieldUtilityQuizCommand(args),
                "stalk" => HandlePacketOwnedFieldUtilityStalkCommand(args),
                "quickslot" => HandlePacketOwnedFieldUtilityQuickslotCommand(args),
                "footholdrequest" => ApplyPacketOwnedFieldUtilityHelper(PacketFieldUtilityPacketKind.RequestFootHoldInfo, Array.Empty<byte>()),
                _ => ChatCommandHandler.CommandResult.Error("Usage: /fieldutility [status|clear|weather <itemId|clear> [message...]|quiz <question|answer|clear> <category> <problemId>|stalk <add <characterId> <name> <x> <y>|remove <characterId>>|quickslot <default|k1 k2 k3 k4 k5 k6 k7 k8>|footholdrequest|packet <kind> [payloadhex=..|payloadb64=..]|packetraw <kind> <hex>]"),
            };
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldUtilityPacketCommand(string[] args, bool rawHex)
        {
            if (args.Length < 2 || !TryParsePacketFieldUtilityKind(args[1], out PacketFieldUtilityPacketKind kind))
            {
                return ChatCommandHandler.CommandResult.Error(
                    rawHex
                        ? "Usage: /fieldutility packetraw <kind> <hex>"
                        : "Usage: /fieldutility packet <kind> [payloadhex=..|payloadb64=..]");
            }

            byte[] payload = Array.Empty<byte>();
            if (rawHex)
            {
                if (args.Length < 3 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out payload))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /fieldutility packetraw <kind> <hex>");
                }
            }
            else if (args.Length >= 3 && !TryParseBinaryPayloadArgument(args[2], out payload, out string payloadError))
            {
                return ChatCommandHandler.CommandResult.Error(payloadError ?? "Usage: /fieldutility packet <kind> [payloadhex=..|payloadb64=..]");
            }

            return ApplyPacketOwnedFieldUtilityHelper(kind, payload);
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldUtilityWeatherCommand(string[] args)
        {
            if (args.Length < 2)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldutility weather <itemId|clear> [message...]");
            }

            if (string.Equals(args[1], "clear", StringComparison.OrdinalIgnoreCase))
            {
                return ApplyPacketOwnedFieldUtilityHelper(PacketFieldUtilityPacketKind.BlowWeather, PacketFieldUtilityRuntime.BuildBlowWeatherPayload(0, 0, null));
            }

            if (!int.TryParse(args[1], out int itemId) || itemId <= 0)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldutility weather <itemId|clear> [message...]");
            }

            string weatherMessage = args.Length > 2 ? string.Join(" ", args.Skip(2)) : string.Empty;
            return ApplyPacketOwnedFieldUtilityHelper(
                PacketFieldUtilityPacketKind.BlowWeather,
                PacketFieldUtilityRuntime.BuildBlowWeatherPayload(0, itemId, weatherMessage));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldUtilityQuizCommand(string[] args)
        {
            if (args.Length < 2)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldutility quiz <question|answer|clear> <category> <problemId>");
            }

            if (string.Equals(args[1], "clear", StringComparison.OrdinalIgnoreCase))
            {
                return ApplyPacketOwnedFieldUtilityHelper(PacketFieldUtilityPacketKind.Quiz, PacketFieldUtilityRuntime.BuildQuizPayload(false, 0, 0));
            }

            if (args.Length < 4
                || !byte.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte category)
                || !ushort.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort problemId))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldutility quiz <question|answer|clear> <category> <problemId>");
            }

            bool isQuestion = string.Equals(args[1], "question", StringComparison.OrdinalIgnoreCase);
            if (!isQuestion && !string.Equals(args[1], "answer", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldutility quiz <question|answer|clear> <category> <problemId>");
            }

            return ApplyPacketOwnedFieldUtilityHelper(
                PacketFieldUtilityPacketKind.Quiz,
                PacketFieldUtilityRuntime.BuildQuizPayload(isQuestion, category, problemId));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldUtilityStalkCommand(string[] args)
        {
            if (args.Length < 3)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldutility stalk <add <characterId> <name> <x> <y>|remove <characterId>>");
            }

            if (!int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int characterId))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldutility stalk <add <characterId> <name> <x> <y>|remove <characterId>>");
            }

            if (string.Equals(args[1], "remove", StringComparison.OrdinalIgnoreCase))
            {
                return ApplyPacketOwnedFieldUtilityHelper(
                    PacketFieldUtilityPacketKind.StalkResult,
                    PacketFieldUtilityRuntime.BuildStalkResultPayload((characterId, true, null, 0, 0)));
            }

            if (args.Length < 6
                || !int.TryParse(args[^2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                || !int.TryParse(args[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldutility stalk <add <characterId> <name> <x> <y>|remove <characterId>>");
            }

            string name = string.Join(" ", args.Skip(3).Take(args.Length - 5));
            return ApplyPacketOwnedFieldUtilityHelper(
                PacketFieldUtilityPacketKind.StalkResult,
                PacketFieldUtilityRuntime.BuildStalkResultPayload((characterId, false, name, x, y)));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldUtilityQuickslotCommand(string[] args)
        {
            if (args.Length == 2 && string.Equals(args[1], "default", StringComparison.OrdinalIgnoreCase))
            {
                return ApplyPacketOwnedFieldUtilityHelper(PacketFieldUtilityPacketKind.QuickslotInit, PacketFieldUtilityRuntime.BuildQuickslotInitPayload(null));
            }

            if (args.Length != 9)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldutility quickslot <default|k1 k2 k3 k4 k5 k6 k7 k8>");
            }

            int[] keyCodes = new int[8];
            for (int i = 0; i < keyCodes.Length; i++)
            {
                if (!int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out keyCodes[i]))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /fieldutility quickslot <default|k1 k2 k3 k4 k5 k6 k7 k8>");
                }
            }

            return ApplyPacketOwnedFieldUtilityHelper(PacketFieldUtilityPacketKind.QuickslotInit, PacketFieldUtilityRuntime.BuildQuickslotInitPayload(keyCodes));
        }

        private ChatCommandHandler.CommandResult ApplyPacketOwnedFieldUtilityHelper(PacketFieldUtilityPacketKind kind, byte[] payload)
        {
            return _packetFieldUtilityRuntime.TryApplyPacket(kind, payload, BuildPacketFieldUtilityCallbacks(), out string message)
                ? ChatCommandHandler.CommandResult.Ok(message)
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private static bool TryParsePacketFieldUtilityKind(string value, out PacketFieldUtilityPacketKind kind)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int packetType))
            {
                packetType = value?.Trim().ToLowerInvariant() switch
                {
                    "blowweather" or "weather" => 158,
                    "admin" or "adminresult" => 160,
                    "quiz" => 161,
                    "stalk" or "stalkresult" => 172,
                    "quickslot" or "quickslotinit" => 175,
                    "foothold" or "footholdinfo" => 176,
                    "requestfoothold" or "requestfootholdinfo" => 177,
                    _ => -1
                };
            }

            return TryParsePacketFieldUtilityKind(packetType, out kind);
        }

        private static bool TryParsePacketFieldUtilityKind(int packetType, out PacketFieldUtilityPacketKind kind)
        {
            kind = packetType switch
            {
                158 => PacketFieldUtilityPacketKind.BlowWeather,
                160 => PacketFieldUtilityPacketKind.AdminResult,
                161 => PacketFieldUtilityPacketKind.Quiz,
                172 => PacketFieldUtilityPacketKind.StalkResult,
                175 => PacketFieldUtilityPacketKind.QuickslotInit,
                176 => PacketFieldUtilityPacketKind.FootHoldInfo,
                177 => PacketFieldUtilityPacketKind.RequestFootHoldInfo,
                _ => default
            };

            return Enum.IsDefined(typeof(PacketFieldUtilityPacketKind), kind);
        }

        private sealed record PacketOwnedQuizPresentation(string Summary, string DisplayText, bool StartEventTimer);

        private const int QuizAnswerBracketPrefixStringPoolId = 930;
        private const int QuizAnswerTrueMarkerStringPoolId = 2260;
        private const int QuizAnswerFalseMarkerStringPoolId = 2261;

        private PacketOwnedQuizPresentation ResolvePacketOwnedQuizPresentation(bool isQuestion, byte category, ushort problemId)
        {
            if (!TryResolvePacketOwnedQuizProblem(category, problemId, out WzSubProperty problemProperty))
            {
                string fallback = $"{(isQuestion ? "Question" : "Answer")} {category}-{problemId}";
                return new PacketOwnedQuizPresentation(
                    $"Packet-authored quiz {(isQuestion ? "question" : "answer")} {category}-{problemId} is unavailable in Etc/OXQuiz.img.",
                    fallback,
                    isQuestion);
            }

            if (isQuestion)
            {
                string questionText = NormalizePacketOwnedQuizText(problemProperty["q"]?.GetString());
                string displayText = string.IsNullOrWhiteSpace(questionText)
                    ? $"Question {category}-{problemId}"
                    : questionText;
                return new PacketOwnedQuizPresentation(
                    $"Packet-authored quiz question {category}-{problemId}: {displayText}",
                    displayText,
                    true);
            }

            int answerValue = problemProperty["a"]?.GetInt() ?? -1;
            string detailText = NormalizePacketOwnedQuizText(problemProperty["d"]?.GetString());
            string answerPrefix = ResolvePacketOwnedQuizAnswerMarker(answerValue);
            string answerDisplayText = string.IsNullOrWhiteSpace(detailText)
                ? answerPrefix
                : $"{detailText} {answerPrefix}";
            return new PacketOwnedQuizPresentation(
                $"Packet-authored quiz answer {category}-{problemId}: {answerDisplayText}",
                answerDisplayText,
                false);
        }

        private static string ResolvePacketOwnedQuizAnswerMarker(int answerValue)
        {
            string answerBody = answerValue switch
            {
                0 => MapleStoryStringPool.GetOrFallback(QuizAnswerFalseMarkerStringPoolId, "X"),
                1 => MapleStoryStringPool.GetOrFallback(QuizAnswerTrueMarkerStringPoolId, "O"),
                _ => "?"
            };
            string bracketPrefix = MapleStoryStringPool.GetOrFallback(QuizAnswerBracketPrefixStringPoolId, "[");
            if (string.IsNullOrWhiteSpace(bracketPrefix))
            {
                bracketPrefix = "[";
            }

            return $"{bracketPrefix}{answerBody}]";
        }

        private static bool TryResolvePacketOwnedQuizProblem(byte category, ushort problemId, out WzSubProperty problemProperty)
        {
            problemProperty = null;
            if (category == 0 || problemId == 0)
            {
                return false;
            }

            WzImage oxQuizImage = Program.FindImage("Etc", "OXQuiz.img");
            if (oxQuizImage == null)
            {
                return false;
            }

            if (!oxQuizImage.Parsed)
            {
                oxQuizImage.ParseImage();
            }

            problemProperty = oxQuizImage[category.ToString(CultureInfo.InvariantCulture)]?[problemId.ToString(CultureInfo.InvariantCulture)] as WzSubProperty;
            return problemProperty != null;
        }

        private static string NormalizePacketOwnedQuizText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Trim();
        }

        private void DrawPacketOwnedQuizNotice(int currentTickCount)
        {
            if (string.IsNullOrWhiteSpace(_packetFieldUtilityQuizDisplayText)
                || _fontChat == null
                || currentTickCount >= _packetFieldUtilityQuizDisplayExpiresAt)
            {
                return;
            }

            string displayText = _packetFieldUtilityQuizDisplayText;
            if (_packetFieldUtilityQuizTimerExpiresAt > currentTickCount)
            {
                int remainingSeconds = Math.Max(0, (_packetFieldUtilityQuizTimerExpiresAt - currentTickCount + 999) / 1000);
                displayText = $"{displayText} ({remainingSeconds}s)";
            }

            Vector2 textSize = _fontChat.MeasureString(displayText);
            float iconWidth = _packetFieldUtilityStatusNoticeIcon?.Width ?? 0f;
            float iconHeight = _packetFieldUtilityStatusNoticeIcon?.Height ?? 0f;
            float totalWidth = textSize.X + (iconWidth > 0f ? iconWidth + 8f : 0f);
            float drawX = (_renderParams.RenderWidth - totalWidth) * 0.5f;
            float drawY = statusBarUi != null
                ? statusBarUi.Position.Y + 14f
                : MathF.Max(8f, _renderParams.RenderHeight - 72f);

            if (_packetFieldUtilityStatusNoticeIcon != null)
            {
                _spriteBatch.Draw(
                    _packetFieldUtilityStatusNoticeIcon,
                    new Vector2(drawX, drawY + MathF.Max(0f, (textSize.Y - iconHeight) * 0.5f)),
                    Color.White);
                drawX += iconWidth + 8f;
            }

            Vector2 textPosition = new(drawX, drawY);
            _spriteBatch.DrawString(_fontChat, displayText, textPosition + Vector2.One, Color.Black);
            _spriteBatch.DrawString(_fontChat, displayText, textPosition, new Color(255, 246, 181));
        }
    }
}
