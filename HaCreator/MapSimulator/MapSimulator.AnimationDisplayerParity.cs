using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private static readonly string AnimationDisplayerNewYearEffectUol =
            RelationshipOverlayClientStringPoolText.ResolveNewYearCardEffectPath();
        private const string AnimationDisplayerFireCrackerEffectUol = "Effect/OnUserEff.img/itemEffect/firework/5680024";
        private const string AnimationDisplayerGenericUserStateEffectUol = "Effect/OnUserEff.img/character";
        private const string AnimationDisplayerQuestDeliveryEffectBaseUol = "Effect/OnUserEff.img/itemEffect/quest";
        private const int AnimationDisplayerQuestDeliveryFallbackEffectItemId = 2430071;
        private const int AnimationDisplayerNewYearSoundStringPoolId = 0x125C;
        private const int AnimationDisplayerUserStateOffsetY = -70;
        private const int AnimationDisplayerTransientLayerWidth = 800;
        private const int AnimationDisplayerTransientLayerHeight = 600;
        private const int AnimationDisplayerTransientLayerDurationMs = 30000;
        private const int AnimationDisplayerNewYearUpdateIntervalMs = 2000;
        private const int AnimationDisplayerNewYearUpdateCount = 3;
        private const int AnimationDisplayerNewYearUpdateNextMs = 100;
        private const int AnimationDisplayerFireCrackerUpdateNextMs = 200;
        private static readonly string[] AnimationDisplayerNewYearSoundImageCandidates = { "Field", "Game", "MiniGame", "UI" };
        private static readonly (int UpdateIntervalMs, int UpdateCount)[] AnimationDisplayerFireCrackerBurstSchedule =
        {
            (200, 1),
            (1000, 5),
            (500, 3),
            (300, 2),
            (200, 1)
        };

        private readonly Dictionary<string, List<IDXObject>> _animationDisplayerEffectCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<int> _packetOwnedAnimationDisplayerAreaAnimationIds = new();
        private int _animationDisplayerLocalQuestDeliveryItemId;

        internal enum AnimationDisplayerTransientEffectKind
        {
            None = 0,
            NewYear = 1,
            FireCracker = 2
        }

        private void RegisterAnimationDisplayerChatCommand()
        {
            _chat.CommandHandler.RegisterCommand(
                "socialanim",
                "Exercise the shared social or event animation-displayer runtime",
                "/socialanim <status|clear|newyear|firecracker|userstate|questdelivery> [...]",
                HandleAnimationDisplayerChatCommand);
        }

        private ChatCommandHandler.CommandResult HandleAnimationDisplayerChatCommand(string[] args)
        {
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(DescribeAnimationDisplayerStatus());
            }

            if (string.Equals(args[0], "clear", StringComparison.OrdinalIgnoreCase))
            {
                ClearAnimationDisplayerState();
                return ChatCommandHandler.CommandResult.Ok(DescribeAnimationDisplayerStatus());
            }

            if (string.Equals(args[0], "newyear", StringComparison.OrdinalIgnoreCase))
            {
                Rectangle area = ResolveAnimationDisplayerArea(args, startIndex: 1);
                return TryRegisterAnimationDisplayerNewYearAnimation(area, out string message)
                    ? ChatCommandHandler.CommandResult.Ok(message)
                    : ChatCommandHandler.CommandResult.Error(message);
            }

            if (string.Equals(args[0], "firecracker", StringComparison.OrdinalIgnoreCase))
            {
                Rectangle area = ResolveAnimationDisplayerArea(args, startIndex: 1);
                return TryRegisterAnimationDisplayerFireCrackerAnimation(area, out string message)
                    ? ChatCommandHandler.CommandResult.Ok(message)
                    : ChatCommandHandler.CommandResult.Error(message);
            }

            if (string.Equals(args[0], "userstate", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /socialanim userstate <local|characterId> <on|off>");
                }

                if (!TryResolveAnimationDisplayerOwner(args[1], out int ownerCharacterId, out Func<Vector2> getPosition, out string ownerName))
                {
                    return ChatCommandHandler.CommandResult.Error($"Could not resolve animation owner {args[1]}.");
                }

                if (string.Equals(args[2], "on", StringComparison.OrdinalIgnoreCase))
                {
                    bool registered = TryRegisterAnimationDisplayerUserState(ownerCharacterId, getPosition);
                    return registered
                        ? ChatCommandHandler.CommandResult.Ok($"Registered animation-displayer user-state layer for {ownerName}.")
                        : ChatCommandHandler.CommandResult.Error("User-state animation frames could not be loaded from Effect/OnUserEff.img/character.");
                }

                if (string.Equals(args[2], "off", StringComparison.OrdinalIgnoreCase))
                {
                    _animationEffects.RemoveUserState(ownerCharacterId, currTickCount);
                    return ChatCommandHandler.CommandResult.Ok($"Cleared animation-displayer user-state layer for {ownerName}.");
                }

                return ChatCommandHandler.CommandResult.Error("Usage: /socialanim userstate <local|characterId> <on|off>");
            }

            if (string.Equals(args[0], "questdelivery", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /socialanim questdelivery <itemId|clear>");
                }

                if (string.Equals(args[1], "clear", StringComparison.OrdinalIgnoreCase))
                {
                    ClearAnimationDisplayerLocalQuestDeliveryOwner();
                    return ChatCommandHandler.CommandResult.Ok("Cleared animation-displayer quest-delivery user-state layer for the local user.");
                }

                if (!int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId) || itemId <= 0)
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /socialanim questdelivery <itemId|clear>");
                }

                bool registered = TryRegisterAnimationDisplayerQuestDeliveryLocalUserState(itemId, out int resolvedItemId);
                return registered
                    ? ChatCommandHandler.CommandResult.Ok($"Registered animation-displayer quest-delivery layer for local user using item {resolvedItemId}.")
                    : ChatCommandHandler.CommandResult.Error($"Quest-delivery animation frames could not be loaded for item {itemId}.");
            }

            return ChatCommandHandler.CommandResult.Error("Usage: /socialanim <status|clear|newyear|firecracker|userstate|questdelivery> [...]");
        }

        private string DescribeAnimationDisplayerStatus()
        {
            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            bool localUserStateActive = localCharacterId > 0 && _animationEffects.HasUserState(localCharacterId);
            string localQuestDelivery = _animationDisplayerLocalQuestDeliveryItemId > 0
                ? _animationDisplayerLocalQuestDeliveryItemId.ToString(CultureInfo.InvariantCulture)
                : "idle";
            return $"Animation displayer parity: userStates={_animationEffects.UserStateCount}, areaAnimations={_animationEffects.AreaAnimationCount}, localUserState={(localUserStateActive ? "active" : "idle")}, localQuestDelivery={localQuestDelivery}, localFade={(_packetOwnedFieldFadeOverlay.IsActive ? "active" : "idle")}.";
        }

        private void ClearAnimationDisplayerState()
        {
            _animationEffects.ClearUserStates();
            _animationEffects.ClearAreaAnimations();
            _packetOwnedAnimationDisplayerAreaAnimationIds.Clear();
            _animationDisplayerLocalQuestDeliveryItemId = 0;
            ResetAnimationDisplayerLocalFadeLayer();
        }

        private void ResetAnimationDisplayerLocalFadeLayer()
        {
            _packetOwnedFieldFadeOverlay.Clear();
        }

        private bool TryRegisterAnimationDisplayerNewYearAnimation(Rectangle area, out string message)
        {
            bool registered = TryRegisterAnimationDisplayerNewYearAnimation(area);
            message = registered
                ? $"Registered New Year animation-displayer area effect in ({area.X}, {area.Y}, {area.Width}, {area.Height})."
                : $"Could not resolve {AnimationDisplayerNewYearEffectUol}.";
            return registered;
        }

        private bool TryRegisterAnimationDisplayerFireCrackerAnimation(Rectangle area, out string message)
        {
            bool registered = TryRegisterAnimationDisplayerFireCrackerAnimation(area);
            message = registered
                ? $"Registered firecracker animation-displayer area effect in ({area.X}, {area.Y}, {area.Width}, {area.Height})."
                : $"Could not resolve {AnimationDisplayerFireCrackerEffectUol}.";
            return registered;
        }

        private bool TryRegisterAnimationDisplayerNewYearAnimation(Rectangle area)
        {
            return TryRegisterAnimationDisplayerAreaAnimation(
                cacheKey: "newyear",
                effectUol: AnimationDisplayerNewYearEffectUol,
                area,
                updateIntervalMs: AnimationDisplayerNewYearUpdateIntervalMs,
                updateCount: AnimationDisplayerNewYearUpdateCount,
                updateNextMs: AnimationDisplayerNewYearUpdateNextMs,
                durationMs: AnimationDisplayerTransientLayerDurationMs,
                onSpawn: () => TryPlayAnimationDisplayerNewYearTransientSound(weatherPath: null)) >= 0;
        }

        private bool TryRegisterAnimationDisplayerFireCrackerAnimation(Rectangle area)
        {
            bool anyRegistered = false;
            string[] effectUols = BuildAnimationDisplayerFireCrackerEffectUols(weatherPath: null);
            for (int i = 0; i < AnimationDisplayerFireCrackerBurstSchedule.Length; i++)
            {
                (int updateIntervalMs, int updateCount) = AnimationDisplayerFireCrackerBurstSchedule[i];
                string effectUol = i < effectUols.Length
                    ? effectUols[i]
                    : BuildAnimationDisplayerFireCrackerEffectUol(i, weatherPath: null);
                anyRegistered |= TryRegisterAnimationDisplayerAreaAnimation(
                    cacheKey: $"firecracker:{i}",
                    effectUol,
                    area,
                    updateIntervalMs,
                    updateCount,
                    AnimationDisplayerFireCrackerUpdateNextMs,
                    AnimationDisplayerTransientLayerDurationMs) >= 0;
            }

            return anyRegistered;
        }

        private int TryRegisterAnimationDisplayerAreaAnimation(
            string cacheKey,
            string effectUol,
            Rectangle area,
            int updateIntervalMs,
            int updateCount,
            int updateNextMs,
            int durationMs,
            Action onSpawn = null)
        {
            if (!TryGetAnimationDisplayerFrames(cacheKey, effectUol, out List<IDXObject> frames))
            {
                return -1;
            }

            return _animationEffects.RegisterAreaAnimation(
                frames,
                area,
                updateIntervalMs,
                updateCount,
                updateNextMs,
                durationMs,
                currTickCount,
                onSpawn);
        }

        private bool TryRegisterAnimationDisplayerUserState(int ownerCharacterId, Func<Vector2> getPosition)
        {
            if (ownerCharacterId <= 0 || getPosition == null)
            {
                return false;
            }

            if (!TryGetAnimationDisplayerFrames("userstate:generic", AnimationDisplayerGenericUserStateEffectUol, out List<IDXObject> repeatFrames))
            {
                return false;
            }

            return _animationEffects.RegisterUserState(
                       ownerCharacterId,
                       startFrames: null,
                       repeatFrames,
                       endFrames: null,
                       getPosition,
                       offsetX: 0f,
                       offsetY: AnimationDisplayerUserStateOffsetY,
                       currTickCount) >= 0;
        }

        private bool TryRegisterAnimationDisplayerQuestDeliveryLocalUserState(int itemId)
        {
            return TryRegisterAnimationDisplayerQuestDeliveryLocalUserState(itemId, out _);
        }

        private bool TryRegisterAnimationDisplayerQuestDeliveryLocalUserState(int itemId, out int resolvedItemId)
        {
            resolvedItemId = 0;
            PlayerCharacter player = _playerManager?.Player;
            if (player?.Build?.Id <= 0)
            {
                return false;
            }

            bool registered = TryRegisterAnimationDisplayerQuestDeliveryUserState(
                player.Build.Id,
                itemId,
                () => _playerManager?.Player?.Position ?? player.Position,
                out resolvedItemId);
            if (registered)
            {
                _animationDisplayerLocalQuestDeliveryItemId = resolvedItemId;
            }

            return registered;
        }

        private bool TryRegisterAnimationDisplayerQuestDeliveryUserState(
            int ownerCharacterId,
            int itemId,
            Func<Vector2> getPosition,
            out int resolvedItemId)
        {
            resolvedItemId = 0;
            if (ownerCharacterId <= 0 || getPosition == null || itemId <= 0)
            {
                return false;
            }

            if (!TryResolveAnimationDisplayerQuestDeliveryEffectUol(itemId, out string effectUol, out resolvedItemId))
            {
                return false;
            }

            WzImageProperty sourceProperty = ResolveAnimationDisplayerProperty(effectUol);
            if (!TryLoadAnimationDisplayerQuestDeliveryPhaseFrames(
                    sourceProperty,
                    out List<IDXObject> startFrames,
                    out List<IDXObject> repeatFrames,
                    out List<IDXObject> endFrames))
            {
                return false;
            }

            int registrationKey = BuildAnimationDisplayerQuestDeliveryRegistrationKey(ownerCharacterId);
            return _animationEffects.RegisterUserState(
                       registrationKey,
                       ownerCharacterId,
                       startFrames,
                       repeatFrames,
                       endFrames,
                       getPosition,
                       offsetX: 0f,
                       offsetY: AnimationDisplayerUserStateOffsetY,
                       currTickCount) >= 0;
        }

        private bool TryResolveAnimationDisplayerQuestDeliveryEffectUol(int itemId, out string effectUol, out int resolvedItemId)
        {
            effectUol = null;
            resolvedItemId = 0;

            string[] candidateUols = EnumerateAnimationDisplayerQuestDeliveryEffectUols(itemId);
            for (int i = 0; i < candidateUols.Length; i++)
            {
                string candidate = candidateUols[i];
                if (string.IsNullOrWhiteSpace(candidate) || ResolveAnimationDisplayerProperty(candidate) == null)
                {
                    continue;
                }

                effectUol = candidate;
                string[] segments = candidate.Split('/');
                if (segments.Length > 0)
                {
                    int.TryParse(segments[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out resolvedItemId);
                }

                return true;
            }

            return false;
        }

        private void SyncAnimationDisplayerRemoteUserState(int characterId)
        {
            if (characterId <= 0)
            {
                return;
            }

            if (_remoteUserPool?.TryGetActor(characterId, out RemoteUserActor actor) != true
                || actor == null
                || !actor.IsVisibleInWorld
                || !actor.TemporaryStats.HasPayload)
            {
                _animationEffects.RemoveUserState(characterId, currTickCount);
                return;
            }

            TryRegisterAnimationDisplayerUserState(characterId, () =>
            {
                if (_remoteUserPool?.TryGetActor(characterId, out RemoteUserActor liveActor) == true && liveActor != null)
                {
                    return liveActor.Position;
                }

                return actor.Position;
            });
        }

        private bool TryGetAnimationDisplayerFrames(string cacheKey, string effectUol, out List<IDXObject> frames)
        {
            if (_animationDisplayerEffectCache.TryGetValue(cacheKey, out frames) && Animation.AnimationEffects.HasFrames(frames))
            {
                return true;
            }

            frames = LoadAnimationDisplayerFrames(effectUol);
            if (!Animation.AnimationEffects.HasFrames(frames))
            {
                frames = null;
                return false;
            }

            _animationDisplayerEffectCache[cacheKey] = frames;
            return true;
        }

        private bool TryLoadAnimationDisplayerQuestDeliveryPhaseFrames(
            WzImageProperty sourceProperty,
            out List<IDXObject> startFrames,
            out List<IDXObject> repeatFrames,
            out List<IDXObject> endFrames)
        {
            startFrames = null;
            repeatFrames = null;
            endFrames = null;

            sourceProperty = sourceProperty?.GetLinkedWzImageProperty() ?? sourceProperty;
            if (sourceProperty == null)
            {
                return false;
            }

            List<Point> frameSizes = GetAnimationDisplayerFrameSizes(sourceProperty);
            ResolveAnimationDisplayerQuestDeliveryPhaseRanges(
                frameSizes,
                out int startIndex,
                out int startCount,
                out int repeatIndex,
                out int repeatCount,
                out int endIndex,
                out int endCount);

            startFrames = LoadAnimationDisplayerFrameRange(sourceProperty, startIndex, startCount);
            repeatFrames = LoadAnimationDisplayerFrameRange(sourceProperty, repeatIndex, repeatCount);
            endFrames = LoadAnimationDisplayerFrameRange(sourceProperty, endIndex, endCount);
            return Animation.AnimationEffects.HasFrames(startFrames)
                || Animation.AnimationEffects.HasFrames(repeatFrames)
                || Animation.AnimationEffects.HasFrames(endFrames);
        }

        private List<IDXObject> LoadAnimationDisplayerFrames(string effectUol)
        {
            WzImageProperty property = ResolveAnimationDisplayerProperty(effectUol);
            return LoadPacketOwnedAnimationFrames(property);
        }

        private WzImageProperty ResolveAnimationDisplayerProperty(string effectUol)
        {
            if (string.IsNullOrWhiteSpace(effectUol))
            {
                return null;
            }

            string normalized = effectUol.Replace('\\', '/').Trim().Trim('/');
            string[] segments = normalized.Split('/');
            if (segments.Length < 3)
            {
                return null;
            }

            string category = segments[0];
            string imageName = segments[1];
            string propertyPath = string.Join("/", segments, 2, segments.Length - 2);
            WzImage image = Program.FindImage(category, imageName);
            return ResolvePacketOwnedPropertyPath(image, propertyPath);
        }

        private List<IDXObject> LoadAnimationDisplayerFrameRange(WzImageProperty sourceProperty, int startIndex, int frameCount)
        {
            if (sourceProperty == null || frameCount <= 0)
            {
                return null;
            }

            sourceProperty = sourceProperty.GetLinkedWzImageProperty() ?? sourceProperty;
            List<IDXObject> frames = new();
            int sharedDelay = sourceProperty["delay"]?.GetInt() ?? 90;

            for (int i = 0; i < frameCount; i++)
            {
                int frameIndex = startIndex + i;
                if (sourceProperty[frameIndex.ToString(CultureInfo.InvariantCulture)] is not WzCanvasProperty frameCanvas)
                {
                    break;
                }

                List<IDXObject> frame = LoadPacketOwnedCanvasFrame(frameCanvas, sharedDelay);
                if (!Animation.AnimationEffects.HasFrames(frame))
                {
                    continue;
                }

                frames.AddRange(frame);
            }

            return frames.Count > 0 ? frames : null;
        }

        private static List<Point> GetAnimationDisplayerFrameSizes(WzImageProperty sourceProperty)
        {
            var frameSizes = new List<Point>();
            if (sourceProperty == null)
            {
                return frameSizes;
            }

            sourceProperty = sourceProperty.GetLinkedWzImageProperty() ?? sourceProperty;
            for (int i = 0; ; i++)
            {
                if (sourceProperty[i.ToString(CultureInfo.InvariantCulture)] is not WzCanvasProperty frameCanvas)
                {
                    break;
                }

                frameSizes.Add(new Point(frameCanvas.PngProperty.Width, frameCanvas.PngProperty.Height));
            }

            return frameSizes;
        }

        internal static string BuildAnimationDisplayerQuestDeliveryEffectUol(int itemId)
        {
            return itemId > 0
                ? $"{AnimationDisplayerQuestDeliveryEffectBaseUol}/{itemId.ToString(CultureInfo.InvariantCulture)}"
                : null;
        }

        internal static int BuildAnimationDisplayerQuestDeliveryRegistrationKey(int ownerCharacterId)
        {
            return ownerCharacterId > 0
                ? unchecked(int.MinValue | ownerCharacterId)
                : 0;
        }

        internal static string[] EnumerateAnimationDisplayerQuestDeliveryEffectUols(int itemId)
        {
            if (itemId <= 0)
            {
                return Array.Empty<string>();
            }

            string directUol = BuildAnimationDisplayerQuestDeliveryEffectUol(itemId);
            if (itemId == AnimationDisplayerQuestDeliveryFallbackEffectItemId)
            {
                return string.IsNullOrWhiteSpace(directUol)
                    ? Array.Empty<string>()
                    : new[] { directUol };
            }

            string fallbackUol = BuildAnimationDisplayerQuestDeliveryEffectUol(AnimationDisplayerQuestDeliveryFallbackEffectItemId);
            return string.IsNullOrWhiteSpace(directUol)
                ? new[] { fallbackUol }
                : new[] { directUol, fallbackUol };
        }

        internal static void ResolveAnimationDisplayerQuestDeliveryPhaseRanges(
            IReadOnlyList<Point> frameSizes,
            out int startIndex,
            out int startCount,
            out int repeatIndex,
            out int repeatCount,
            out int endIndex,
            out int endCount)
        {
            startIndex = 0;
            startCount = 0;
            repeatIndex = 0;
            repeatCount = 0;
            endIndex = 0;
            endCount = 0;

            if (frameSizes == null || frameSizes.Count == 0)
            {
                return;
            }

            int firstMaxIndex = 0;
            int lastMaxIndex = 0;
            long maxArea = long.MinValue;

            for (int i = 0; i < frameSizes.Count; i++)
            {
                Point size = frameSizes[i];
                long area = (long)Math.Max(0, size.X) * Math.Max(0, size.Y);
                if (area > maxArea)
                {
                    maxArea = area;
                    firstMaxIndex = i;
                    lastMaxIndex = i;
                }
                else if (area == maxArea)
                {
                    lastMaxIndex = i;
                }
            }

            startIndex = 0;
            startCount = firstMaxIndex;
            repeatIndex = firstMaxIndex;
            repeatCount = Math.Max(1, lastMaxIndex - firstMaxIndex + 1);
            endIndex = repeatIndex + repeatCount;
            endCount = Math.Max(0, frameSizes.Count - endIndex);
        }

        private Rectangle ResolveAnimationDisplayerArea(string[] args, int startIndex)
        {
            if (args.Length >= startIndex + 4
                && int.TryParse(args[startIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                && int.TryParse(args[startIndex + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y)
                && int.TryParse(args[startIndex + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int width)
                && int.TryParse(args[startIndex + 3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int height))
            {
                return new Rectangle(x, y, Math.Max(1, width), Math.Max(1, height));
            }

            return ResolveAnimationDisplayerTransientLayerArea(_playerManager?.Player?.Position ?? Vector2.Zero);
        }

        private void ApplyPacketOwnedAnimationDisplayerTransientLayer(int itemId, string weatherPath)
        {
            ResetPacketOwnedAnimationDisplayerTransientLayers();

            Rectangle area = ResolveAnimationDisplayerTransientLayerArea(_playerManager?.Player?.Position ?? Vector2.Zero);
            switch (ResolveAnimationDisplayerTransientEffectKind(itemId, weatherPath))
            {
                case AnimationDisplayerTransientEffectKind.NewYear:
                    int newYearId = TryRegisterAnimationDisplayerAreaAnimation(
                        cacheKey: "newyear",
                        effectUol: AnimationDisplayerNewYearEffectUol,
                        area,
                        AnimationDisplayerNewYearUpdateIntervalMs,
                        AnimationDisplayerNewYearUpdateCount,
                        AnimationDisplayerNewYearUpdateNextMs,
                        AnimationDisplayerTransientLayerDurationMs,
                        onSpawn: () => TryPlayAnimationDisplayerNewYearTransientSound(weatherPath));
                    if (newYearId >= 0)
                    {
                        _packetOwnedAnimationDisplayerAreaAnimationIds.Add(newYearId);
                    }
                    break;

                case AnimationDisplayerTransientEffectKind.FireCracker:
                    string[] effectUols = BuildAnimationDisplayerFireCrackerEffectUols(weatherPath);
                    for (int i = 0; i < AnimationDisplayerFireCrackerBurstSchedule.Length; i++)
                    {
                        (int updateIntervalMs, int updateCount) = AnimationDisplayerFireCrackerBurstSchedule[i];
                        string effectUol = i < effectUols.Length
                            ? effectUols[i]
                            : BuildAnimationDisplayerFireCrackerEffectUol(i, weatherPath);
                        int areaAnimationId = TryRegisterAnimationDisplayerAreaAnimation(
                            cacheKey: $"firecracker:{i}",
                            effectUol,
                            area,
                            updateIntervalMs,
                            updateCount,
                            AnimationDisplayerFireCrackerUpdateNextMs,
                            AnimationDisplayerTransientLayerDurationMs);
                        if (areaAnimationId >= 0)
                        {
                            _packetOwnedAnimationDisplayerAreaAnimationIds.Add(areaAnimationId);
                        }
                    }
                    break;
            }
        }

        private void ResetPacketOwnedAnimationDisplayerTransientLayers()
        {
            for (int i = 0; i < _packetOwnedAnimationDisplayerAreaAnimationIds.Count; i++)
            {
                _animationEffects.RemoveAreaAnimation(_packetOwnedAnimationDisplayerAreaAnimationIds[i]);
            }

            _packetOwnedAnimationDisplayerAreaAnimationIds.Clear();
        }

        internal static Rectangle ResolveAnimationDisplayerTransientLayerArea(Vector2 centerPosition)
        {
            int width = AnimationDisplayerTransientLayerWidth;
            int height = AnimationDisplayerTransientLayerHeight;
            return new Rectangle(
                (int)Math.Round(centerPosition.X) - width / 2,
                (int)Math.Round(centerPosition.Y) - height / 2,
                width,
                height);
        }

        internal static AnimationDisplayerTransientEffectKind ResolveAnimationDisplayerTransientEffectKind(int itemId, string weatherPath)
        {
            if (itemId == 4300000)
            {
                return AnimationDisplayerTransientEffectKind.NewYear;
            }

            if (itemId == 5680024)
            {
                return AnimationDisplayerTransientEffectKind.FireCracker;
            }

            string normalizedPath = weatherPath?.Replace('\\', '/').Trim();
            if (string.Equals(normalizedPath, AnimationDisplayerNewYearEffectUol, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedPath, "Map/MapHelper.img/weather/newyear", StringComparison.OrdinalIgnoreCase))
            {
                return AnimationDisplayerTransientEffectKind.NewYear;
            }

            if (string.Equals(normalizedPath, AnimationDisplayerFireCrackerEffectUol, StringComparison.OrdinalIgnoreCase)
                || normalizedPath?.Contains("/firecracker", StringComparison.OrdinalIgnoreCase) == true
                || normalizedPath?.Contains("/firework", StringComparison.OrdinalIgnoreCase) == true)
            {
                return AnimationDisplayerTransientEffectKind.FireCracker;
            }

            return AnimationDisplayerTransientEffectKind.None;
        }

        internal static IReadOnlyList<(int UpdateIntervalMs, int UpdateCount)> GetAnimationDisplayerFireCrackerBurstSchedule()
        {
            return AnimationDisplayerFireCrackerBurstSchedule;
        }

        internal static string BuildAnimationDisplayerFireCrackerEffectUol(int burstIndex, string weatherPath)
        {
            if (burstIndex < 0)
            {
                return null;
            }

            string baseUol = NormalizeAnimationDisplayerFireCrackerEffectBaseUol(weatherPath);
            return string.IsNullOrWhiteSpace(baseUol)
                ? null
                : $"{baseUol}/{burstIndex.ToString(CultureInfo.InvariantCulture)}";
        }

        internal static string[] BuildAnimationDisplayerFireCrackerEffectUols(string weatherPath)
        {
            string[] effectUols = new string[AnimationDisplayerFireCrackerBurstSchedule.Length];
            for (int i = 0; i < effectUols.Length; i++)
            {
                effectUols[i] = BuildAnimationDisplayerFireCrackerEffectUol(i, weatherPath);
            }

            return effectUols;
        }

        private static string NormalizeAnimationDisplayerFireCrackerEffectBaseUol(string weatherPath)
        {
            string normalizedPath = weatherPath?.Replace('\\', '/').Trim().Trim('/');
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return AnimationDisplayerFireCrackerEffectUol;
            }

            if (normalizedPath.StartsWith("Effect/", StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath = normalizedPath["Effect/".Length..];
            }

            if (normalizedPath.StartsWith("OnUserEff.img/", StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath = normalizedPath["OnUserEff.img/".Length..];
            }

            if (normalizedPath.EndsWith("/0", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.EndsWith("/1", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.EndsWith("/2", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.EndsWith("/3", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.EndsWith("/4", StringComparison.OrdinalIgnoreCase))
            {
                int separatorIndex = normalizedPath.LastIndexOf('/');
                normalizedPath = separatorIndex >= 0
                    ? normalizedPath[..separatorIndex]
                    : normalizedPath;
            }

            if (normalizedPath.Contains("itemEffect/firework/", StringComparison.OrdinalIgnoreCase))
            {
                return $"Effect/OnUserEff.img/{normalizedPath}";
            }

            return AnimationDisplayerFireCrackerEffectUol;
        }

        private void TryPlayAnimationDisplayerNewYearTransientSound(string weatherPath)
        {
            string[] candidates = BuildAnimationDisplayerNewYearSoundCandidates(weatherPath);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (TryPlayPacketOwnedWzSound(candidates[i], defaultImageName: "Field.img", out _, out _))
                {
                    return;
                }
            }
        }

        internal static string[] BuildAnimationDisplayerNewYearSoundCandidates(string weatherPath)
        {
            var candidates = new List<string>();
            foreach (string soundKey in EnumerateAnimationDisplayerNewYearSoundKeys(weatherPath))
            {
                AddAnimationDisplayerNewYearSoundCandidate(
                    candidates,
                    BuildAnimationDisplayerNewYearSoundDescriptor(soundKey));
            }

            AddAnimationDisplayerNewYearSoundCandidate(candidates, "Field/newyear");
            AddAnimationDisplayerNewYearSoundCandidate(candidates, "Field/weather/newyear");
            AddAnimationDisplayerNewYearSoundCandidate(candidates, "Game/newyear");

            string normalizedWeatherPath = NormalizeAnimationDisplayerPath(weatherPath);
            if (string.IsNullOrWhiteSpace(normalizedWeatherPath))
            {
                return candidates.ToArray();
            }

            string[] segments = normalizedWeatherPath.Split('/');
            string lastSegment = segments.Length > 0 ? segments[^1] : null;
            string secondLastSegment = segments.Length > 1 ? segments[^2] : null;
            string imageSegment = segments.Length > 1 ? NormalizeAnimationDisplayerImageNameSegment(segments[1]) : null;

            if (!string.IsNullOrWhiteSpace(lastSegment))
            {
                AddAnimationDisplayerNewYearSoundCandidate(candidates, $"Field/{lastSegment}");
                AddAnimationDisplayerNewYearSoundCandidate(candidates, $"Field/weather/{lastSegment}");
                AddAnimationDisplayerNewYearSoundCandidate(candidates, $"Game/{lastSegment}");
            }

            if (!string.IsNullOrWhiteSpace(secondLastSegment) && !string.IsNullOrWhiteSpace(lastSegment))
            {
                AddAnimationDisplayerNewYearSoundCandidate(candidates, $"Field/{secondLastSegment}/{lastSegment}");
                AddAnimationDisplayerNewYearSoundCandidate(candidates, $"Game/{secondLastSegment}/{lastSegment}");
            }

            if (!string.IsNullOrWhiteSpace(imageSegment) && !string.IsNullOrWhiteSpace(lastSegment))
            {
                AddAnimationDisplayerNewYearSoundCandidate(candidates, $"Field/{imageSegment}/{lastSegment}");
                AddAnimationDisplayerNewYearSoundCandidate(candidates, $"Field/{imageSegment}/weather/{lastSegment}");
                AddAnimationDisplayerNewYearSoundCandidate(candidates, $"Game/{imageSegment}/{lastSegment}");
            }

            for (int i = 0; i < AnimationDisplayerNewYearSoundImageCandidates.Length; i++)
            {
                string imageName = AnimationDisplayerNewYearSoundImageCandidates[i];
                if (!string.IsNullOrWhiteSpace(lastSegment))
                {
                    AddAnimationDisplayerNewYearSoundCandidate(candidates, $"{imageName}/{lastSegment}");
                    AddAnimationDisplayerNewYearSoundCandidate(candidates, $"{imageName}/weather/{lastSegment}");
                }
            }

            return candidates.ToArray();
        }

        internal static string BuildAnimationDisplayerNewYearSoundDescriptor(string soundKey)
        {
            if (string.IsNullOrWhiteSpace(soundKey))
            {
                return null;
            }

            string compositeFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                AnimationDisplayerNewYearSoundStringPoolId,
                "Sound/Field.img/{0}",
                maxPlaceholderCount: 1,
                out _);
            string formatted = string.Format(CultureInfo.InvariantCulture, compositeFormat, soundKey.Trim());
            return NormalizeAnimationDisplayerSoundDescriptor(formatted);
        }

        internal static string NormalizeAnimationDisplayerSoundDescriptor(string descriptor)
        {
            string normalized = NormalizeAnimationDisplayerPath(descriptor);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            if (normalized.StartsWith("Sound/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized["Sound/".Length..];
            }

            string[] segments = normalized.Split('/');
            if (segments.Length == 0)
            {
                return null;
            }

            if (segments[0].EndsWith(".img", StringComparison.OrdinalIgnoreCase))
            {
                segments[0] = NormalizeAnimationDisplayerImageNameSegment(segments[0]);
            }

            return NormalizeAnimationDisplayerPath(string.Join("/", segments));
        }

        private static string NormalizeAnimationDisplayerPath(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim().Replace('\\', '/').Trim('/');
        }

        private static IEnumerable<string> EnumerateAnimationDisplayerNewYearSoundKeys(string weatherPath)
        {
            string normalizedWeatherPath = NormalizeAnimationDisplayerPath(weatherPath);
            if (string.IsNullOrWhiteSpace(normalizedWeatherPath))
            {
                yield return "newyear";
                yield break;
            }

            string normalizedSoundDescriptor = NormalizeAnimationDisplayerSoundDescriptor(normalizedWeatherPath);
            if (!string.IsNullOrWhiteSpace(normalizedSoundDescriptor))
            {
                string[] directSegments = normalizedSoundDescriptor.Split('/');
                if (directSegments.Length >= 2)
                {
                    yield return string.Join("/", directSegments, 1, directSegments.Length - 1);
                }
            }

            string[] segments = normalizedWeatherPath.Split('/');
            int weatherSegmentIndex = Array.FindIndex(
                segments,
                segment => string.Equals(segment, "weather", StringComparison.OrdinalIgnoreCase));
            if (weatherSegmentIndex >= 0 && weatherSegmentIndex < segments.Length - 1)
            {
                yield return string.Join("/", segments, weatherSegmentIndex + 1, segments.Length - weatherSegmentIndex - 1);
            }

            if (segments.Length > 0)
            {
                yield return segments[^1];
            }
        }

        private static string NormalizeAnimationDisplayerImageNameSegment(string value)
        {
            string normalized = NormalizeAnimationDisplayerPath(value);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            int dotIndex = normalized.IndexOf(".img", StringComparison.OrdinalIgnoreCase);
            return dotIndex > 0 ? normalized[..dotIndex] : normalized;
        }

        private static void AddAnimationDisplayerNewYearSoundCandidate(List<string> candidates, string descriptor)
        {
            string normalized = NormalizeAnimationDisplayerPath(descriptor);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (string.Equals(candidates[i], normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            candidates.Add(normalized);
        }

        private void ClearAnimationDisplayerLocalQuestDeliveryOwner()
        {
            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            if (localCharacterId > 0 && _animationDisplayerLocalQuestDeliveryItemId > 0)
            {
                _animationEffects.RemoveUserStateByRegistrationKey(
                    BuildAnimationDisplayerQuestDeliveryRegistrationKey(localCharacterId),
                    currTickCount);
            }

            _animationDisplayerLocalQuestDeliveryItemId = 0;
        }

        private void HandleAnimationDisplayerOverlayClose(NpcInteractionOverlayCloseKind closeKind)
        {
            if (closeKind != NpcInteractionOverlayCloseKind.None)
            {
                ClearAnimationDisplayerLocalQuestDeliveryOwner();
            }
        }

        private bool TryResolveAnimationDisplayerOwner(
            string ownerToken,
            out int ownerCharacterId,
            out Func<Vector2> getPosition,
            out string ownerName)
        {
            ownerCharacterId = 0;
            getPosition = null;
            ownerName = "unknown";

            if (string.Equals(ownerToken, "local", StringComparison.OrdinalIgnoreCase))
            {
                PlayerCharacter player = _playerManager?.Player;
                if (player?.Build?.Id > 0)
                {
                    ownerCharacterId = player.Build.Id;
                    ownerName = player.Build.Name ?? $"Character {ownerCharacterId}";
                    getPosition = () => _playerManager?.Player?.Position ?? player.Position;
                    return true;
                }

                return false;
            }

            if (!int.TryParse(ownerToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out int characterId)
                || _remoteUserPool?.TryGetActor(characterId, out RemoteUserActor actor) != true
                || actor == null)
            {
                return false;
            }

            ownerCharacterId = characterId;
            ownerName = string.IsNullOrWhiteSpace(actor.Name) ? $"Character {characterId}" : actor.Name.Trim();
            getPosition = () =>
            {
                if (_remoteUserPool?.TryGetActor(characterId, out RemoteUserActor liveActor) == true && liveActor != null)
                {
                    return liveActor.Position;
                }

                return actor.Position;
            };
            return true;
        }
    }
}
