using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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
        private int[] _packetFieldUtilityQuickslotKeyCodes;
        private bool _packetFieldUtilityWeatherOverrideActive;
        private int _packetFieldUtilityWeatherItemId;
        private WeatherType _packetFieldUtilityWeatherType = WeatherType.None;
        private string _packetFieldUtilityWeatherPath;
        private string _packetFieldUtilityWeatherMessage;
        private string _packetFieldUtilityQuizSummary;
        private string _packetFieldUtilityFootholdRequestSummary = "No packet-owned foothold-info request has been handled.";

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
                _fieldEffects?.StopWeather();
                return;
            }

            _packetFieldUtilityWeatherOverrideActive = true;
            _packetFieldUtilityWeatherItemId = itemId;
            _packetFieldUtilityWeatherPath = weatherPath;
            _packetFieldUtilityWeatherType = ResolvePacketOwnedWeatherType(weatherPath);
            _packetFieldUtilityWeatherMessage = string.IsNullOrWhiteSpace(message) ? null : message.Trim();

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
            if (result == null || string.IsNullOrWhiteSpace(result.Body))
            {
                return;
            }

            _chat?.AddClientChatMessage(result.Body, currTickCount, result.ChatLogType);
            if (result.ShowPrompt)
            {
                ShowConnectionNoticePrompt(new LoginPacketDialogPromptConfiguration
                {
                    Owner = LoginPacketDialogOwner.ConnectionNotice,
                    Title = result.Title ?? "Admin",
                    Body = result.Body,
                    NoticeVariant = ConnectionNoticeWindowVariant.Notice,
                    DurationMs = 5000
                });
            }

            if (result.ReloadMinimap)
            {
                _mapBoard?.RegenerateMinimap();
                ShowUtilityFeedbackMessage("Reloaded the minimap after packet-owned admin result.");
            }

            if (result.ToggleMinimap)
            {
                miniMapUi?.MinimiseOrMaximiseMinimap(currTickCount);
            }
        }

        private void PresentPacketOwnedQuizState(string quizText, bool isQuestion, byte category, ushort problemId)
        {
            _packetFieldUtilityQuizSummary = problemId == 0
                ? null
                : $"{(isQuestion ? "Question" : "Answer")} {category}-{problemId}: {quizText}";

            if (problemId == 0)
            {
                ShowUtilityFeedbackMessage("Cleared packet-authored quiz status.");
                return;
            }

            string message = quizText ?? $"{(isQuestion ? "Question" : "Answer")} {category}-{problemId}";
            _chat?.AddClientChatMessage($"[Quiz] {message}", currTickCount, 12);
            ShowUtilityFeedbackMessage($"Packet-authored quiz {(isQuestion ? "question" : "answer")}: {message}");
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
            uiWindowManager?.QuickSlotWindow?.SetPrimaryBarKeyLabels(BuildPacketOwnedQuickslotLabels(_packetFieldUtilityQuickslotKeyCodes));
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
            return keyCode switch
            {
                2 => "1",
                3 => "2",
                4 => "3",
                5 => "4",
                6 => "5",
                7 => "6",
                8 => "7",
                9 => "8",
                10 => "9",
                11 => "0",
                16 => "Q",
                17 => "W",
                18 => "E",
                19 => "R",
                20 => "T",
                21 => "Y",
                22 => "U",
                23 => "I",
                24 => "O",
                25 => "P",
                26 => "[",
                27 => "]",
                29 => "A",
                30 => "S",
                31 => "D",
                32 => "F",
                33 => "G",
                34 => "H",
                35 => "J",
                36 => "K",
                37 => "L",
                38 => ";",
                39 => "'",
                40 => "Z",
                41 => "X",
                42 => "C",
                43 => "V",
                44 => "B",
                _ => keyCode.ToString(CultureInfo.InvariantCulture)
            };
        }

        private void ApplyPacketOwnedFootholdInfo(IReadOnlyList<PacketFieldUtilityFootholdEntry> entries)
        {
            _packetFieldUtilityFootholdEntries.Clear();
            if (entries != null)
            {
                _packetFieldUtilityFootholdEntries.AddRange(entries);
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

            if (!TryResolvePacketOwnedDynamicPlatform(entry.Name, out DynamicPlatform platform))
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

        private bool TryResolvePacketOwnedDynamicPlatform(string name, out DynamicPlatform platform)
        {
            platform = null;
            if (string.IsNullOrWhiteSpace(name) || _dynamicFootholds == null)
            {
                return false;
            }

            const string prefix = "platform-";
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || !int.TryParse(name[prefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int platformId))
            {
                return false;
            }

            platform = _dynamicFootholds.GetPlatform(platformId);
            return platform != null;
        }

        private string HandlePacketOwnedFootholdInfoRequest()
        {
            IReadOnlyList<PacketFieldUtilityFootholdEntry> snapshot = BuildPacketOwnedFootholdSnapshot();
            _packetFieldUtilityFootholdRequestSummary = snapshot.Count == 0
                ? "Received packet-owned foothold-info request; no dynamic foothold entries were available to snapshot."
                : $"Received packet-owned foothold-info request; prepared {snapshot.Count} dynamic foothold snapshot entr{(snapshot.Count == 1 ? "y" : "ies")} for the current runtime.";
            return _packetFieldUtilityFootholdRequestSummary;
        }

        private IReadOnlyList<PacketFieldUtilityFootholdEntry> BuildPacketOwnedFootholdSnapshot()
        {
            if (_packetFieldUtilityFootholdEntries.Count > 0)
            {
                return _packetFieldUtilityFootholdEntries.ToArray();
            }

            List<PacketFieldUtilityFootholdEntry> entries = new();
            for (int i = 0; i < _dynamicFootholds.PlatformCount; i++)
            {
                DynamicPlatform platform = _dynamicFootholds.GetPlatform(i);
                if (platform == null)
                {
                    continue;
                }

                entries.Add(new PacketFieldUtilityFootholdEntry(
                    $"platform-{i}",
                    platform.IsActive ? 2 : 0,
                    new[] { i },
                    new PacketFieldUtilityMovingFootholdState(
                        (int)platform.Speed,
                        (int)platform.LeftBound,
                        (int)platform.RightBound,
                        (int)platform.TopBound,
                        (int)platform.BottomBound,
                        (int)platform.X,
                        (int)platform.Y,
                        !platform.MovingDown,
                        !platform.MovingRight)));
            }

            return entries;
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
                    $"{_packetFieldUtilityRuntime.DescribeStatus()}{Environment.NewLine}{_packetFieldUtilityFootholdRequestSummary}");
            }

            if (string.Equals(args[0], "clear", StringComparison.OrdinalIgnoreCase))
            {
                _packetFieldUtilityRuntime.Clear();
                _packetFieldUtilityStalkTargets.Clear();
                _packetFieldUtilityFootholdEntries.Clear();
                _packetFieldUtilityQuickslotKeyCodes = null;
                _packetFieldUtilityWeatherOverrideActive = false;
                _packetFieldUtilityWeatherItemId = 0;
                _packetFieldUtilityWeatherPath = null;
                _packetFieldUtilityWeatherMessage = null;
                _packetFieldUtilityQuizSummary = null;
                _packetFieldUtilityFootholdRequestSummary = "No packet-owned foothold-info request has been handled.";
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
    }
}
