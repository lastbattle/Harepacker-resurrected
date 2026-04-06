using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private readonly PacketFieldFeedbackRuntime _packetFieldFeedbackRuntime = new();
        private readonly Dictionary<string, List<IDXObject>> _packetFieldFeedbackAnimationCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<PacketOwnedUiAnimation> _packetFieldFeedbackUiAnimations = new();
        private IReadOnlyList<PacketFieldSwindleWarningEntry> _packetFieldSwindleWarnings;
        private const int PacketOwnedSummonEffectStringPoolId = 0x663;
        private const int PacketOwnedScreenEffectStringPoolId = 0x9ED;
        private static readonly string[] PacketOwnedScreenEffectImageNames =
        {
            "BasicEff.img",
            "CharacterEff.img",
            "Direction.img",
            "Direction1.img",
            "Direction100.img",
            "Direction2.img",
            "Direction3.img",
            "Direction4.img",
            "Direction5.img",
            "Direction6.img",
            "Direction7.img",
            "Direction_Vampire.img",
            "ItemEff.img",
            "MobEff.img",
            "OnUserEff.img",
            "PetEff.img",
            "PvPEff.img",
            "SetEff.img",
            "SetItemInfoEff.img",
            "SkillName1.img",
            "SkillName2.img",
            "SkillName3.img",
            "SkillName4.img",
            "Summon.img"
            ,"Tomb.img"
        };
        private static readonly string[] PacketOwnedRewardRouletteLayerPaths =
        {
            "MainNotice/userReward/Default",
            "MainNotice/userReward/Notify",
            "MainNotice/userReward/Appear"
        };
        private static readonly byte[] PacketOwnedNpcSummonFallbackEffectIds =
        {
            0,
            2,
            5,
            13,
            14,
            24,
            25,
            26
        };

        private void UpdatePacketOwnedFieldFeedbackState(int currentTickCount)
        {
            _packetFieldFeedbackRuntime.Initialize(GraphicsDevice);
            _packetFieldFeedbackRuntime.Update(currentTickCount);
            UpdatePacketOwnedFieldFeedbackUiAnimations(currentTickCount);
        }

        private void DrawPacketOwnedFieldFeedbackState(int currentTickCount)
        {
            _packetFieldFeedbackRuntime.Draw(_spriteBatch, _fontChat, _renderParams.RenderWidth, currentTickCount);
            DrawPacketOwnedFieldFeedbackUiAnimations(currentTickCount);
        }

        private bool TryApplyPacketOwnedFieldFeedbackPacket(PacketFieldFeedbackPacketKind kind, byte[] payload, out string message)
        {
            _packetFieldFeedbackRuntime.Initialize(GraphicsDevice);
            return _packetFieldFeedbackRuntime.TryApplyPacket(
                kind,
                payload,
                currTickCount,
                BuildPacketFieldFeedbackCallbacks(),
                out message);
        }

        private PacketFieldFeedbackCallbacks BuildPacketFieldFeedbackCallbacks()
        {
            return new PacketFieldFeedbackCallbacks
            {
                AddClientChatMessage = (text, chatLogType, whisperTargetCandidate) =>
                {
                    _chat?.AddClientChatMessage(text, currTickCount, chatLogType, whisperTargetCandidate);
                    if (ShouldTriggerPetSpecialistFeedbackForClientChatLogType(chatLogType))
                    {
                        TryTriggerSpecialistPetSocialFeedback(text, currTickCount);
                    }
                },
                ShowUtilityFeedback = ShowUtilityFeedbackMessage,
                ShowModalWarning = ShowPacketOwnedFieldWarning,
                RememberWhisperTarget = target => _chat?.RememberWhisperTarget(target),
                TriggerTremble = (force, durationMs) => _screenEffects.TriggerTremble(Math.Max(1, force), false, 0, Math.Max(0, durationMs), true, currTickCount),
                ClearFieldFade = () => ClearPacketOwnedLocalOverlayState("fade"),
                RequestBgm = RequestSpecialFieldBgmOverride,
                PlayFieldSound = descriptor => TryPlayPacketOwnedFieldFeedbackSound(descriptor),
                PlaySummonEffectSound = TryPlayPacketOwnedSummonEffectSound,
                SetObjectTagState = (tag, state, transition, currentTime) => SetDynamicObjectTagState(tag, state, transition, currentTime),
                ShowSummonEffectVisual = TryShowPacketOwnedSummonEffect,
                ShowScreenEffectVisual = TryShowPacketOwnedScreenEffect,
                ShowRewardRouletteVisual = TryShowPacketOwnedRewardRouletteEffect,
                ResolveMobName = ResolvePacketFieldFeedbackMobName,
                ResolveMapName = mapId => ResolveMapTransferDisplayName(mapId, null),
                ResolveItemName = ResolvePacketFieldFeedbackItemName,
                ResolveChannelName = ResolvePacketFieldFeedbackChannelName,
                IsBlacklistedName = name => _socialListRuntime.IsBlacklisted(name),
                IsBlockedFriendName = name => _socialListRuntime.IsBlockedFriend(name),
                QueueMapTransfer = TryQueuePacketOwnedWhisperFindTransfer,
                ResolveSwindleWarnings = GetPacketOwnedSwindleWarningEntries
            };
        }

        private void ShowPacketOwnedFieldWarning(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            _chat?.AddClientChatMessage($"[System] {message}", currTickCount, 12);
            ShowConnectionNoticePrompt(new LoginPacketDialogPromptConfiguration
            {
                Owner = LoginPacketDialogOwner.ConnectionNotice,
                Title = "Warning",
                Body = message.Trim(),
                NoticeVariant = ConnectionNoticeWindowVariant.Notice,
                DurationMs = 5000
            });
        }

        private bool TryPlayPacketOwnedFieldFeedbackSound(string descriptor)
        {
            if (!TryPlayPacketOwnedWzSound(descriptor, "FieldSound", out string resolvedDescriptor, out string error))
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    ShowUtilityFeedbackMessage(error);
                }

                return false;
            }

            ShowUtilityFeedbackMessage($"Played packet-owned field sound {resolvedDescriptor}.");
            return true;
        }

        private bool TryPlayPacketOwnedSummonEffectSound(byte effectId)
        {
            return TryPlayPacketOwnedWzSound(
                effectId.ToString(CultureInfo.InvariantCulture),
                "Summon.img",
                out _,
                out _);
        }

        private static string ResolvePacketFieldFeedbackMobName(int mobId)
        {
            return ResolvePacketGuideMobName(mobId);
        }

        private static string ResolvePacketFieldFeedbackItemName(int itemId)
        {
            return InventoryItemMetadataResolver.TryResolveItemName(itemId, out string itemName)
                ? itemName
                : $"Item {itemId}";
        }

        private static string ResolvePacketFieldFeedbackChannelName(int channelId)
        {
            return channelId > 0
                ? $"Ch. {channelId}"
                : string.Empty;
        }

        private bool TryShowPacketOwnedSummonEffect(byte effectId, int x, int y)
        {
            string cacheKey = $"summon:{effectId}";
            if (!TryGetOrCreatePacketOwnedAnimationFrames(cacheKey, () => ResolvePacketOwnedSummonEffectFrames(effectId), out List<IDXObject> frames))
            {
                return false;
            }

            _animationEffects?.AddOneTime(frames, x, y, flip: false, currTickCount, zOrder: 1);
            return true;
        }

        private bool TryShowPacketOwnedScreenEffect(string descriptor)
        {
            if (string.IsNullOrWhiteSpace(descriptor))
            {
                return false;
            }

            string cacheKey = $"screen:{descriptor.Trim()}";
            if (!TryGetOrCreatePacketOwnedAnimationFrames(cacheKey, () => ResolvePacketOwnedScreenEffectFrames(descriptor), out List<IDXObject> frames))
            {
                return false;
            }

            EnqueuePacketOwnedUiAnimation(
                frames,
                _renderParams.RenderWidth / 2,
                Math.Max(0, Height / 2 - 40),
                currTickCount);
            return true;
        }

        private bool TryShowPacketOwnedRewardRouletteEffect(int rewardJobIndex, int rewardPartIndex, int rewardLevelIndex)
        {
            bool shownAnyLayer = false;
            foreach (string propertyPath in EnumeratePacketOwnedRewardRouletteLayerSourcePaths(
                rewardJobIndex,
                rewardPartIndex,
                rewardLevelIndex))
            {
                string cacheKey = $"reward-roulette:{propertyPath}";
                if (!TryGetOrCreatePacketOwnedAnimationFrames(
                    cacheKey,
                    () => LoadPacketOwnedAnimationFrames(ResolvePacketOwnedPropertyPath(Program.FindImage("Effect", "BasicEff.img"), propertyPath)),
                    out List<IDXObject> frames))
                {
                    continue;
                }

                EnqueuePacketOwnedUiAnimation(
                    frames,
                    _renderParams.RenderWidth / 2,
                    Math.Max(0, Height / 2 - 24),
                    currTickCount);
                shownAnyLayer = true;
            }

            ShowUtilityFeedbackMessage(
                $"Packet-owned reward roulette: job={rewardJobIndex} part={rewardPartIndex} level={rewardLevelIndex}.");
            return shownAnyLayer;
        }

        private bool TryQueuePacketOwnedWhisperFindTransfer(int mapId, int x, int y)
        {
            if (mapId <= 0 || mapId == MapConstants.MaxMap)
            {
                return false;
            }

            string restrictionMessage = FieldInteractionRestrictionEvaluator.GetMapTransferRestrictionMessage(_mapBoard?.MapInfo?.fieldLimit ?? 0);
            if (!string.IsNullOrWhiteSpace(restrictionMessage))
            {
                ShowUtilityFeedbackMessage(restrictionMessage);
                return false;
            }

            if (_loadMapCallback == null)
            {
                ShowUtilityFeedbackMessage("Whisper follow transfer is unavailable without a map loader.");
                return false;
            }

            bool queued = QueueMapTransfer(mapId, null);
            if (queued)
            {
                ShowUtilityFeedbackMessage(
                    $"Queued packet-owned whisper follow transfer to {ResolveMapTransferDisplayName(mapId, null)} ({x}, {y}).");
            }

            return queued;
        }

        private void UpdatePacketOwnedFieldFeedbackUiAnimations(int currentTickCount)
        {
            for (int i = _packetFieldFeedbackUiAnimations.Count - 1; i >= 0; i--)
            {
                if (_packetFieldFeedbackUiAnimations[i].IsComplete(currentTickCount))
                {
                    _packetFieldFeedbackUiAnimations.RemoveAt(i);
                }
            }
        }

        private void DrawPacketOwnedFieldFeedbackUiAnimations(int currentTickCount)
        {
            if (_spriteBatch == null)
            {
                return;
            }

            foreach (PacketOwnedUiAnimation animation in _packetFieldFeedbackUiAnimations)
            {
                IDXObject frame = animation.ResolveFrame(currentTickCount);
                if (frame?.Texture == null)
                {
                    continue;
                }

                Vector2 position = new(animation.AnchorX - frame.X, animation.AnchorY - frame.Y);
                _spriteBatch.Draw(frame.Texture, position, Color.White);
            }
        }

        private void EnqueuePacketOwnedUiAnimation(List<IDXObject> frames, int anchorX, int anchorY, int currentTickCount)
        {
            if (frames == null || frames.Count == 0)
            {
                return;
            }

            _packetFieldFeedbackUiAnimations.Add(new PacketOwnedUiAnimation(frames, anchorX, anchorY, currentTickCount));
        }

        private bool TryGetOrCreatePacketOwnedAnimationFrames(string cacheKey, Func<List<IDXObject>> loader, out List<IDXObject> frames)
        {
            if (_packetFieldFeedbackAnimationCache.TryGetValue(cacheKey, out frames) && frames?.Count > 0)
            {
                return true;
            }

            frames = loader?.Invoke();
            if (frames == null || frames.Count == 0)
            {
                frames = null;
                return false;
            }

            _packetFieldFeedbackAnimationCache[cacheKey] = frames;
            return true;
        }

        private List<IDXObject> ResolvePacketOwnedSummonEffectFrames(byte effectId)
        {
            WzImage summonImage = Program.FindImage("Effect", "Summon.img");
            List<IDXObject> frames = LoadPacketOwnedAnimationFrames(
                ResolvePacketOwnedPropertyPath(
                    summonImage,
                    ResolvePacketOwnedSummonEffectPropertyPath(effectId)));
            if (frames?.Count > 0)
            {
                return frames;
            }

            if (!ShouldUsePacketOwnedNpcSummonFallback(effectId))
            {
                return null;
            }

            WzImage mapEffectImage = Program.FindImage("Effect", "MapEff.img");
            return LoadPacketOwnedAnimationFrames(ResolvePacketOwnedPropertyPath(mapEffectImage, "NpcSummon"));
        }

        private bool HasPacketOwnedSummonSoundAsset(byte effectId)
        {
            WzImage soundImage = Program.FindImage("Sound", "Summon.img");
            return ResolvePacketOwnedPropertyPath(soundImage, effectId.ToString(CultureInfo.InvariantCulture)) != null;
        }

        private List<IDXObject> ResolvePacketOwnedScreenEffectFrames(string descriptor)
        {
            foreach ((string imageName, string propertyPath) in EnumeratePacketOwnedScreenEffectCandidates(descriptor))
            {
                WzImage image = Program.FindImage("Effect", imageName);
                List<IDXObject> frames = LoadPacketOwnedAnimationFrames(ResolvePacketOwnedPropertyPath(image, propertyPath));
                if (frames?.Count > 0)
                {
                    return frames;
                }
            }

            return null;
        }

        private static IEnumerable<(string ImageName, string PropertyPath)> EnumeratePacketOwnedScreenEffectCandidates(string descriptor)
        {
            if (string.IsNullOrWhiteSpace(descriptor))
            {
                yield break;
            }

            string normalized = descriptor.Replace('\\', '/').Trim().Trim('/');
            if (string.IsNullOrWhiteSpace(normalized))
            {
                yield break;
            }

            if (normalized.StartsWith("effect/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized["effect/".Length..];
            }

            if (TryResolvePacketOwnedEffectUol(
                PacketOwnedScreenEffectStringPoolId,
                "Effect/MapEff.img/{0}",
                normalized,
                out (string ImageName, string PropertyPath) clientCandidate))
            {
                yield return clientCandidate;
            }

            int imageSeparator = normalized.IndexOf(".img/", StringComparison.OrdinalIgnoreCase);
            if (imageSeparator >= 0)
            {
                string imageName = normalized[..(imageSeparator + 4)];
                string propertyPath = normalized[(imageSeparator + 5)..];
                yield return (imageName, propertyPath);
            }

            string[] variants =
            {
                normalized,
                normalized.Contains('/') ? normalized[(normalized.IndexOf('/') + 1)..] : normalized
            };

            foreach (string variant in variants.Where(static entry => !string.IsNullOrWhiteSpace(entry)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                yield return ("MapEff.img", variant);
            }

            foreach (string imageName in PacketOwnedScreenEffectImageNames)
            {
                foreach (string variant in variants.Where(static entry => !string.IsNullOrWhiteSpace(entry)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    yield return (imageName, variant);
                }
            }
        }

        private List<IDXObject> LoadPacketOwnedAnimationFrames(WzImageProperty sourceProperty, int fallbackDelay = 90)
        {
            sourceProperty = sourceProperty?.GetLinkedWzImageProperty() ?? sourceProperty;
            if (sourceProperty == null || GraphicsDevice == null)
            {
                return null;
            }

            if (sourceProperty is WzCanvasProperty canvasProperty)
            {
                return LoadPacketOwnedCanvasFrame(canvasProperty, fallbackDelay);
            }

            List<IDXObject> frames = new();
            int sharedDelay = sourceProperty["delay"]?.GetInt() ?? fallbackDelay;

            for (int i = 0; ; i++)
            {
                if (sourceProperty[i.ToString(CultureInfo.InvariantCulture)] is not WzCanvasProperty frameCanvas)
                {
                    break;
                }

                System.Drawing.Bitmap frameBitmap = frameCanvas.GetLinkedWzCanvasBitmap();
                if (frameBitmap == null)
                {
                    continue;
                }

                int delay = frameCanvas[WzCanvasProperty.AnimationDelayPropertyName]?.GetInt()
                    ?? frameCanvas["delay"]?.GetInt()
                    ?? sharedDelay;
                using (frameBitmap)
                {
                    Texture2D texture = frameBitmap.ToTexture2D(GraphicsDevice);
                    frames.Add(new DXObject(frameCanvas.GetCanvasOriginPosition(), texture, delay));
                }
            }

            return frames.Count > 0 ? frames : null;
        }

        private List<IDXObject> LoadPacketOwnedCanvasFrame(WzCanvasProperty canvasProperty, int fallbackDelay)
        {
            if (canvasProperty == null || GraphicsDevice == null)
            {
                return null;
            }

            System.Drawing.Bitmap frameBitmap = canvasProperty.GetLinkedWzCanvasBitmap();
            if (frameBitmap == null)
            {
                return null;
            }

            int delay = canvasProperty[WzCanvasProperty.AnimationDelayPropertyName]?.GetInt()
                ?? canvasProperty["delay"]?.GetInt()
                ?? fallbackDelay;
            using (frameBitmap)
            {
                Texture2D texture = frameBitmap.ToTexture2D(GraphicsDevice);
                return new List<IDXObject>
                {
                    new DXObject(canvasProperty.GetCanvasOriginPosition(), texture, delay)
                };
            }
        }

        private static WzImageProperty ResolvePacketOwnedPropertyPath(WzObject root, string propertyPath)
        {
            if (root == null || string.IsNullOrWhiteSpace(propertyPath))
            {
                return root as WzImageProperty;
            }

            WzObject current = root;
            foreach (string segment in propertyPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                current = current?[segment];
                if (current == null)
                {
                    break;
                }
            }

            return current as WzImageProperty;
        }

        private static string ResolvePacketOwnedSummonEffectPropertyPath(byte effectId)
        {
            return TryResolvePacketOwnedEffectUol(
                PacketOwnedSummonEffectStringPoolId,
                "Effect/Summon.img/{0}",
                effectId.ToString(CultureInfo.InvariantCulture),
                out (string ImageName, string PropertyPath) candidate)
                ? candidate.PropertyPath
                : effectId.ToString(CultureInfo.InvariantCulture);
        }

        private static bool TryResolvePacketOwnedEffectUol(
            int stringPoolId,
            string fallbackFormat,
            string argument,
            out (string ImageName, string PropertyPath) candidate)
        {
            candidate = default;
            if (string.IsNullOrWhiteSpace(argument))
            {
                return false;
            }

            string uol = FormatPacketOwnedEffectUol(stringPoolId, fallbackFormat, argument);
            if (string.IsNullOrWhiteSpace(uol))
            {
                return false;
            }

            return TryParsePacketOwnedEffectUol(uol, out candidate);
        }

        private static string FormatPacketOwnedEffectUol(int stringPoolId, string fallbackFormat, string argument)
        {
            bool usedResolvedText;
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                stringPoolId,
                fallbackFormat,
                maxPlaceholderCount: 1,
                out usedResolvedText);
            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, argument);
            }
            catch (FormatException)
            {
                return string.Format(CultureInfo.InvariantCulture, fallbackFormat, argument);
            }
        }

        private static bool TryParsePacketOwnedEffectUol(string uol, out (string ImageName, string PropertyPath) candidate)
        {
            candidate = default;
            if (string.IsNullOrWhiteSpace(uol))
            {
                return false;
            }

            string normalized = uol.Trim().Replace('\\', '/').Trim('/');
            if (normalized.StartsWith("Effect/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized["Effect/".Length..];
            }

            int imageSeparator = normalized.IndexOf(".img/", StringComparison.OrdinalIgnoreCase);
            if (imageSeparator < 0 || imageSeparator + 5 > normalized.Length)
            {
                return false;
            }

            string imageName = normalized[..(imageSeparator + 4)];
            string propertyPath = normalized[(imageSeparator + 5)..];
            if (string.IsNullOrWhiteSpace(imageName) || string.IsNullOrWhiteSpace(propertyPath))
            {
                return false;
            }

            candidate = (imageName, propertyPath);
            return true;
        }

        private IReadOnlyList<PacketFieldSwindleWarningEntry> GetPacketOwnedSwindleWarningEntries()
        {
            if (_packetFieldSwindleWarnings != null)
            {
                return _packetFieldSwindleWarnings;
            }

            WzImage swindleImage = Program.FindImage("Etc", "Swindle");
            if (swindleImage == null)
            {
                return _packetFieldSwindleWarnings = Array.Empty<PacketFieldSwindleWarningEntry>();
            }

            if (!swindleImage.Parsed)
            {
                swindleImage.ParseImage();
            }

            _packetFieldSwindleWarnings = BuildPacketOwnedSwindleWarningEntries(swindleImage);
            return _packetFieldSwindleWarnings;
        }

        internal static IReadOnlyList<PacketFieldSwindleWarningEntry> BuildPacketOwnedSwindleWarningEntries(WzImage swindleImage)
        {
            if (swindleImage == null)
            {
                return Array.Empty<PacketFieldSwindleWarningEntry>();
            }

            List<PacketFieldSwindleWarningEntry> entries = new();
            foreach (WzImageProperty groupProperty in swindleImage.WzProperties)
            {
                if (!int.TryParse(groupProperty.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int groupId))
                {
                    continue;
                }

                List<string> keywords = LoadPacketOwnedSwindleStringList(groupProperty["word"]);
                List<string> warnings = LoadPacketOwnedSwindleStringList(groupProperty["warn"]);
                if (keywords.Count == 0 || warnings.Count == 0)
                {
                    continue;
                }

                entries.Add(new PacketFieldSwindleWarningEntry(groupId, keywords, warnings));
            }

            return entries
                .OrderByDescending(static entry => entry.GroupId)
                .ToArray();
        }

        private static List<string> LoadPacketOwnedSwindleStringList(WzImageProperty parentProperty)
        {
            List<string> values = new();
            if (parentProperty == null)
            {
                return values;
            }

            foreach (WzImageProperty child in parentProperty.WzProperties)
            {
                string rawValue = child switch
                {
                    WzStringProperty stringProperty => stringProperty.Value,
                    WzNullProperty => null,
                    _ => child.GetString()
                };

                string normalized = NormalizePacketOwnedSwindleEntry(rawValue);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    values.Add(normalized);
                }
            }

            return values;
        }

        private static string NormalizePacketOwnedSwindleEntry(string value)
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

        internal static IReadOnlyList<string> GetPacketOwnedRewardRouletteLayerPathsForTest()
        {
            return PacketOwnedRewardRouletteLayerPaths;
        }

        internal static IReadOnlyList<string> GetPacketOwnedRewardRouletteLayerSourcePathsForTest()
        {
            return EnumeratePacketOwnedRewardRouletteLayerSourcePaths().ToArray();
        }

        internal static IReadOnlyList<string> GetPacketOwnedRewardRouletteLayerSourcePathsForTest(
            int rewardJobIndex,
            int rewardPartIndex,
            int rewardLevelIndex)
        {
            return EnumeratePacketOwnedRewardRouletteLayerSourcePaths(
                    rewardJobIndex,
                    rewardPartIndex,
                    rewardLevelIndex)
                .ToArray();
        }

        internal static IReadOnlyList<string> GetPacketOwnedScreenEffectCandidatesForTest(string descriptor)
        {
            return EnumeratePacketOwnedScreenEffectCandidates(descriptor)
                .Select(static candidate => $"{candidate.ImageName}:{candidate.PropertyPath}")
                .ToArray();
        }

        internal static string GetPacketOwnedSummonEffectUolForTest(byte effectId)
        {
            return FormatPacketOwnedEffectUol(
                PacketOwnedSummonEffectStringPoolId,
                "Effect/Summon.img/{0}",
                effectId.ToString(CultureInfo.InvariantCulture));
        }

        internal static string GetPacketOwnedScreenEffectUolForTest(string descriptor)
        {
            return FormatPacketOwnedEffectUol(
                PacketOwnedScreenEffectStringPoolId,
                "Effect/MapEff.img/{0}",
                descriptor?.Replace('\\', '/').Trim().Trim('/') ?? string.Empty);
        }

        internal static bool ShouldUsePacketOwnedNpcSummonFallback(byte effectId)
        {
            return PacketOwnedNpcSummonFallbackEffectIds.Contains(effectId);
        }

        private static IEnumerable<string> EnumeratePacketOwnedRewardRouletteLayerSourcePaths()
        {
            foreach (string layerPath in PacketOwnedRewardRouletteLayerPaths)
            {
                yield return layerPath;
            }

            foreach (string layerPath in PacketOwnedRewardRouletteLayerPaths)
            {
                yield return $"{layerPath}/0";
            }
        }

        private static IEnumerable<string> EnumeratePacketOwnedRewardRouletteLayerSourcePaths(
            int rewardJobIndex,
            int rewardPartIndex,
            int rewardLevelIndex)
        {
            (string LayerPath, int RequestedIndex)[] layerRequests =
            {
                (PacketOwnedRewardRouletteLayerPaths[0], rewardJobIndex),
                (PacketOwnedRewardRouletteLayerPaths[1], rewardPartIndex),
                (PacketOwnedRewardRouletteLayerPaths[2], rewardLevelIndex)
            };

            foreach ((string layerPath, int requestedIndex) in layerRequests)
            {
                if (string.IsNullOrWhiteSpace(layerPath) || requestedIndex < 0)
                {
                    continue;
                }

                yield return $"{layerPath}/{requestedIndex.ToString(CultureInfo.InvariantCulture)}";
            }

            foreach ((string layerPath, int requestedIndex) in layerRequests)
            {
                if (string.IsNullOrWhiteSpace(layerPath) || requestedIndex == 0)
                {
                    continue;
                }

                yield return $"{layerPath}/0";
            }
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackCommand(string[] args)
        {
            if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
            {
                return ChatCommandHandler.CommandResult.Info(_packetFieldFeedbackRuntime.DescribeStatus(currTickCount));
            }

            if (string.Equals(args[0], "clear", StringComparison.OrdinalIgnoreCase))
            {
                _packetFieldFeedbackRuntime.Clear();
                _packetFieldFeedbackUiAnimations.Clear();
                return ChatCommandHandler.CommandResult.Ok(_packetFieldFeedbackRuntime.DescribeStatus(currTickCount));
            }

            if (string.Equals(args[0], "packet", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase))
            {
                return HandlePacketOwnedFieldFeedbackPacketCommand(
                    args,
                    rawHex: string.Equals(args[0], "packetraw", StringComparison.OrdinalIgnoreCase));
            }

            return args[0].ToLowerInvariant() switch
            {
                "group" => HandlePacketOwnedFieldFeedbackGroupCommand(args),
                "whisperin" => HandlePacketOwnedFieldFeedbackWhisperIncomingCommand(args),
                "whisperresult" => HandlePacketOwnedFieldFeedbackWhisperResultCommand(args),
                "whisperavailability" => HandlePacketOwnedFieldFeedbackWhisperAvailabilityCommand(args),
                "whisperfind" => HandlePacketOwnedFieldFeedbackWhisperFindCommand(args),
                "couplechat" => HandlePacketOwnedFieldFeedbackCoupleChatCommand(args),
                "couplenotice" => HandlePacketOwnedFieldFeedbackCoupleNoticeCommand(args),
                "warn" => ApplyPacketOwnedFieldFeedbackHelper(PacketFieldFeedbackPacketKind.WarnMessage, PacketFieldFeedbackRuntime.BuildWarnMessagePayload(string.Join(" ", args.Skip(1)))),
                "obstacle" => HandlePacketOwnedFieldFeedbackObstacleCommand(args),
                "obstaclereset" => ApplyPacketOwnedFieldFeedbackHelper(PacketFieldFeedbackPacketKind.FieldObstacleAllReset, Array.Empty<byte>()),
                "bosshp" => HandlePacketOwnedFieldFeedbackBossHpCommand(args),
                "tremble" => HandlePacketOwnedFieldFeedbackTrembleCommand(args),
                "fieldsound" => ApplyPacketOwnedFieldFeedbackHelper(PacketFieldFeedbackPacketKind.FieldEffect, PacketFieldFeedbackRuntime.BuildSoundFieldEffectPayload(string.Join(" ", args.Skip(1)))),
                "fieldbgm" => ApplyPacketOwnedFieldFeedbackHelper(PacketFieldFeedbackPacketKind.FieldEffect, PacketFieldFeedbackRuntime.BuildBgmFieldEffectPayload(string.Join(" ", args.Skip(1)))),
                "jukebox" => HandlePacketOwnedFieldFeedbackJukeboxCommand(args),
                "transferfieldignored" => HandlePacketOwnedFieldFeedbackTransferReasonCommand(args, PacketFieldFeedbackPacketKind.TransferFieldReqIgnored),
                "transferchannelignored" => HandlePacketOwnedFieldFeedbackTransferReasonCommand(args, PacketFieldFeedbackPacketKind.TransferChannelReqIgnored),
                "summonunavailable" => HandlePacketOwnedFieldFeedbackSummonUnavailableCommand(args),
                "destroyclock" => ApplyPacketOwnedFieldFeedbackHelper(PacketFieldFeedbackPacketKind.DestroyClock, Array.Empty<byte>()),
                "zakumtimer" => HandlePacketOwnedFieldFeedbackBossTimerCommand(args, PacketFieldFeedbackPacketKind.ZakumTimer),
                "hontailtimer" => HandlePacketOwnedFieldFeedbackBossTimerCommand(args, PacketFieldFeedbackPacketKind.HontailTimer),
                "chaoszakumtimer" => HandlePacketOwnedFieldFeedbackBossTimerCommand(args, PacketFieldFeedbackPacketKind.ChaosZakumTimer),
                "hontaletimer" => HandlePacketOwnedFieldFeedbackBossTimerCommand(args, PacketFieldFeedbackPacketKind.HontaleTimer),
                "fadeoutforce" => HandlePacketOwnedFieldFeedbackFadeOutForceCommand(args),
                _ => ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback [status|clear|group <family> <sender> <text>|whisperin <sender> <channel> <text>|whisperresult <target> <ok|fail>|whisperavailability <target> <0|1>|whisperfind <find|findreply> <target> <result> <value> [x y]|couplechat <sender> <text>|couplenotice [text]|warn <text>|obstacle <tag> <state>|obstaclereset|bosshp <mobId> <currentHp> <maxHp> [color] [phase]|tremble <force> <durationMs>|fieldsound <descriptor>|fieldbgm <descriptor>|jukebox <itemId> <owner>|transferfieldignored <reason>|transferchannelignored <reason>|summonunavailable [0|1]|destroyclock|zakumtimer <mode> <value>|hontailtimer <mode> <value>|chaoszakumtimer <mode> <value>|hontaletimer <mode> <value>|fadeoutforce [key]|packet <kind> [payloadhex=..|payloadb64=..]|packetraw <kind> <hex>]"),
            };
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackPacketCommand(string[] args, bool rawHex)
        {
            if (args.Length < 2 || !TryParsePacketFieldFeedbackKind(args[1], out PacketFieldFeedbackPacketKind kind))
            {
                return ChatCommandHandler.CommandResult.Error(
                    rawHex
                        ? "Usage: /fieldfeedback packetraw <kind> <hex>"
                        : "Usage: /fieldfeedback packet <kind> [payloadhex=..|payloadb64=..]");
            }

            byte[] payload = Array.Empty<byte>();
            if (rawHex)
            {
                if (args.Length < 3 || !TryDecodeHexBytes(string.Join(string.Empty, args.Skip(2)), out payload))
                {
                    return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback packetraw <kind> <hex>");
                }
            }
            else if (args.Length >= 3 && !TryParseBinaryPayloadArgument(args[2], out payload, out string payloadError))
            {
                return ChatCommandHandler.CommandResult.Error(payloadError ?? "Usage: /fieldfeedback packet <kind> [payloadhex=..|payloadb64=..]");
            }

            return ApplyPacketOwnedFieldFeedbackHelper(kind, payload);
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackGroupCommand(string[] args)
        {
            if (args.Length < 4 || !byte.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte family))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback group <family> <sender> <text>");
            }

            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.GroupMessage,
                PacketFieldFeedbackRuntime.BuildGroupMessagePayload(family, args[2], string.Join(" ", args.Skip(3))));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackWhisperIncomingCommand(string[] args)
        {
            if (args.Length < 4 || !byte.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte channelId))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback whisperin <sender> <channel> <text>");
            }

            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.Whisper,
                PacketFieldFeedbackRuntime.BuildIncomingWhisperPayload(args[1], channelId, fromAdmin: false, string.Join(" ", args.Skip(3))));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackWhisperResultCommand(string[] args)
        {
            if (args.Length < 3)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback whisperresult <target> <ok|fail>");
            }

            bool success = args[2].Equals("ok", StringComparison.OrdinalIgnoreCase)
                || args[2].Equals("success", StringComparison.OrdinalIgnoreCase)
                || args[2] == "1";
            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.Whisper,
                PacketFieldFeedbackRuntime.BuildWhisperResultPayload(args[1], success));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackWhisperAvailabilityCommand(string[] args)
        {
            if (args.Length < 3)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback whisperavailability <target> <0|1>");
            }

            bool available = args[2].Equals("1", StringComparison.OrdinalIgnoreCase)
                || args[2].Equals("true", StringComparison.OrdinalIgnoreCase)
                || args[2].Equals("yes", StringComparison.OrdinalIgnoreCase);
            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.Whisper,
                PacketFieldFeedbackRuntime.BuildWhisperAvailabilityPayload(args[1], available));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackWhisperFindCommand(string[] args)
        {
            if (args.Length < 5
                || !byte.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte result)
                || !int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback whisperfind <find|findreply> <target> <result> <value> [x y]");
            }

            byte subtype = args[1].Equals("findreply", StringComparison.OrdinalIgnoreCase) ? (byte)72 : (byte)9;
            byte[] payload = PacketFieldFeedbackRuntime.BuildWhisperLocationPayload(subtype, args[2], result, value);
            if (subtype == 9
                && result == 1
                && args.Length >= 7
                && int.TryParse(args[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int transferX)
                && int.TryParse(args[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out int transferY))
            {
                payload = payload.Concat(BitConverter.GetBytes(transferX)).Concat(BitConverter.GetBytes(transferY)).ToArray();
            }

            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.Whisper,
                payload);
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackCoupleChatCommand(string[] args)
        {
            if (args.Length < 3)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback couplechat <sender> <text>");
            }

            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.CoupleMessage,
                PacketFieldFeedbackRuntime.BuildCoupleChatPayload(args[1], string.Join(" ", args.Skip(2))));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackCoupleNoticeCommand(string[] args)
        {
            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.CoupleMessage,
                PacketFieldFeedbackRuntime.BuildCoupleNoticePayload(args.Length >= 2 ? string.Join(" ", args.Skip(1)) : null));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackObstacleCommand(string[] args)
        {
            if (args.Length < 3 || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int state))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback obstacle <tag> <state>");
            }

            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.FieldObstacleOnOff,
                PacketFieldFeedbackRuntime.BuildObstaclePayload(args[1], state));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackBossHpCommand(string[] args)
        {
            if (args.Length < 4
                || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int mobId)
                || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int currentHp)
                || !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int maxHp))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback bosshp <mobId> <currentHp> <maxHp> [color] [phase]");
            }

            byte color = 1;
            byte phase = 0;
            if (args.Length >= 5)
            {
                byte.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out color);
            }

            if (args.Length >= 6)
            {
                byte.TryParse(args[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out phase);
            }

            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.FieldEffect,
                PacketFieldFeedbackRuntime.BuildBossHpFieldEffectPayload(mobId, currentHp, maxHp, color, phase));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackTrembleCommand(string[] args)
        {
            if (args.Length < 3
                || !byte.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte force)
                || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int durationMs))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback tremble <force> <durationMs>");
            }

            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.FieldEffect,
                PacketFieldFeedbackRuntime.BuildTrembleFieldEffectPayload(force, durationMs));
        }

        private sealed class PacketOwnedUiAnimation
        {
            private readonly IReadOnlyList<IDXObject> _frames;
            private readonly int _durationMs;

            public PacketOwnedUiAnimation(IReadOnlyList<IDXObject> frames, int anchorX, int anchorY, int startedAtTick)
            {
                _frames = frames ?? Array.Empty<IDXObject>();
                AnchorX = anchorX;
                AnchorY = anchorY;
                StartedAtTick = startedAtTick;
                _durationMs = _frames.Sum(static frame => Math.Max(1, frame?.Delay ?? 1));
            }

            public int AnchorX { get; }
            public int AnchorY { get; }
            public int StartedAtTick { get; }

            public bool IsComplete(int currentTickCount)
            {
                return _frames.Count == 0 || currentTickCount - StartedAtTick >= _durationMs;
            }

            public IDXObject ResolveFrame(int currentTickCount)
            {
                if (_frames.Count == 0)
                {
                    return null;
                }

                int elapsed = Math.Max(0, currentTickCount - StartedAtTick);
                int cursor = 0;
                foreach (IDXObject frame in _frames)
                {
                    cursor += Math.Max(1, frame?.Delay ?? 1);
                    if (elapsed < cursor)
                    {
                        return frame;
                    }
                }

                return _frames[^1];
            }
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackJukeboxCommand(string[] args)
        {
            if (args.Length < 3 || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback jukebox <itemId> <owner>");
            }

            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.PlayJukeBox,
                PacketFieldFeedbackRuntime.BuildJukeBoxPayload(itemId, string.Join(" ", args.Skip(2))));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackTransferReasonCommand(string[] args, PacketFieldFeedbackPacketKind kind)
        {
            if (args.Length < 2 || !byte.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte reason))
            {
                return ChatCommandHandler.CommandResult.Error($"Usage: /fieldfeedback {(kind == PacketFieldFeedbackPacketKind.TransferFieldReqIgnored ? "transferfieldignored" : "transferchannelignored")} <reason>");
            }

            return ApplyPacketOwnedFieldFeedbackHelper(kind, new[] { reason });
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackSummonUnavailableCommand(string[] args)
        {
            byte blocked = 0;
            if (args.Length >= 2)
            {
                byte.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out blocked);
            }

            return ApplyPacketOwnedFieldFeedbackHelper(PacketFieldFeedbackPacketKind.SummonItemUnavailable, new[] { blocked });
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackBossTimerCommand(string[] args, PacketFieldFeedbackPacketKind kind)
        {
            if (args.Length < 3
                || !byte.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte mode)
                || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return ChatCommandHandler.CommandResult.Error($"Usage: /fieldfeedback {args[0]} <mode> <value>");
            }

            return ApplyPacketOwnedFieldFeedbackHelper(kind, PacketFieldFeedbackRuntime.BuildBossTimerPayload(mode, value));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackFadeOutForceCommand(string[] args)
        {
            int fadeKey = 0;
            if (args.Length >= 2 && !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out fadeKey))
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback fadeoutforce [key]");
            }

            return ApplyPacketOwnedFieldFeedbackHelper(
                PacketFieldFeedbackPacketKind.FieldFadeOutForce,
                PacketFieldFeedbackRuntime.BuildFadeOutForcePayload(fadeKey));
        }

        private ChatCommandHandler.CommandResult ApplyPacketOwnedFieldFeedbackHelper(PacketFieldFeedbackPacketKind kind, byte[] payload)
        {
            return TryApplyPacketOwnedFieldFeedbackPacket(kind, payload, out string message)
                ? ChatCommandHandler.CommandResult.Ok(message)
                : ChatCommandHandler.CommandResult.Error(message);
        }

        private static bool TryParsePacketFieldFeedbackKind(string value, out PacketFieldFeedbackPacketKind kind)
        {
            kind = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim().Replace("-", string.Empty).Replace("_", string.Empty);
            return normalized.ToLowerInvariant() switch
            {
                "group" or "groupmessage" or "150" => Assign(PacketFieldFeedbackPacketKind.GroupMessage, out kind),
                "whisper" or "151" => Assign(PacketFieldFeedbackPacketKind.Whisper, out kind),
                "couple" or "couplemessage" or "152" => Assign(PacketFieldFeedbackPacketKind.CoupleMessage, out kind),
                "fieldeffect" or "154" => Assign(PacketFieldFeedbackPacketKind.FieldEffect, out kind),
                "obstacle" or "fieldobstacleonoff" or "155" => Assign(PacketFieldFeedbackPacketKind.FieldObstacleOnOff, out kind),
                "obstaclestatus" or "fieldobstacleonoffstatus" or "156" => Assign(PacketFieldFeedbackPacketKind.FieldObstacleOnOffStatus, out kind),
                "warn" or "warnmessage" or "157" => Assign(PacketFieldFeedbackPacketKind.WarnMessage, out kind),
                "jukebox" or "playjukebox" or "158" => Assign(PacketFieldFeedbackPacketKind.PlayJukeBox, out kind),
                "obstaclereset" or "fieldobstacleallreset" or "159" => Assign(PacketFieldFeedbackPacketKind.FieldObstacleAllReset, out kind),
                "transferfieldignored" or "160" => Assign(PacketFieldFeedbackPacketKind.TransferFieldReqIgnored, out kind),
                "transferchannelignored" or "161" => Assign(PacketFieldFeedbackPacketKind.TransferChannelReqIgnored, out kind),
                "destroyclock" or "163" => Assign(PacketFieldFeedbackPacketKind.DestroyClock, out kind),
                "summonunavailable" or "summonitemunavailable" or "164" => Assign(PacketFieldFeedbackPacketKind.SummonItemUnavailable, out kind),
                "zakumtimer" => Assign(PacketFieldFeedbackPacketKind.ZakumTimer, out kind),
                "hontailtimer" or "horntailtimer" => Assign(PacketFieldFeedbackPacketKind.HontailTimer, out kind),
                "chaoszakumtimer" => Assign(PacketFieldFeedbackPacketKind.ChaosZakumTimer, out kind),
                "hontaletimer" => Assign(PacketFieldFeedbackPacketKind.HontaleTimer, out kind),
                "fadeoutforce" or "fieldfadeoutforce" => Assign(PacketFieldFeedbackPacketKind.FieldFadeOutForce, out kind),
                _ => false
            };
        }

        private static bool Assign(PacketFieldFeedbackPacketKind value, out PacketFieldFeedbackPacketKind kind)
        {
            kind = value;
            return true;
        }
    }
}
