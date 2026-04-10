using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using HaSharedLibrary.Wz;
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
        private readonly Dictionary<string, List<PacketOwnedUiFrame>> _packetFieldFeedbackAnimationCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IReadOnlyList<PacketOwnedCachedUiLayer>> _packetFieldFeedbackUiLayerCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<PacketOwnedUiAnimation> _packetFieldFeedbackUiAnimations = new();
        private IReadOnlyList<PacketFieldSwindleWarningEntry> _packetFieldSwindleWarnings;
        private readonly Texture2D[] _packetFieldBossTimerDigits = new Texture2D[10];
        private PacketFieldBossTimerVisualState _packetFieldBossTimerClockState;
        private PacketFieldClockVisualState _packetFieldClockState;
        private Texture2D _packetFieldClockWindowPixelTexture;
        private Texture2D _packetFieldBossTimerBackgroundTexture;
        private Texture2D _packetFieldBossTimerHourBackgroundTexture;
        private Texture2D _packetFieldBossTimerSeparatorTexture;
        private Texture2D _packetFieldClockAmTexture;
        private Texture2D _packetFieldClockPmTexture;
        private bool _packetFieldBossTimerAssetsLoaded;
        private const int PacketOwnedSummonEffectStringPoolId = 0x663;
        private const int PacketOwnedScreenEffectStringPoolId = 0x9ED;
        private const int PacketOwnedRewardRoulettePathJoinStringPoolId = 0x3DA;
        private const int PacketOwnedUiReferenceWidth = 800;
        private const int PacketOwnedUiReferenceHeight = 600;
        private const int PacketOwnedScreenEffectYOffset = -40;
        private const int PacketOwnedRewardRouletteOffsetX = 400;
        private const int PacketOwnedRewardRouletteOffsetY = 260;
        private const int PacketOwnedBossTimerOffsetY = 25;
        private const int PacketOwnedBossTimerDigitSpacing = 2;
        private const int PacketOwnedClockTimerDigitY = 12;
        private const int PacketOwnedClockTimerDigitX1 = 86;
        private const int PacketOwnedClockTimerDigitX2 = 112;
        private const int PacketOwnedClockTimerDigitX3 = 179;
        private const int PacketOwnedClockTimerDigitX4 = 205;
        private const int PacketOwnedClockRealtimeColonBlinkAlpha = 64;
        private const int PacketOwnedClockRealtimeDigitSpacing = 2;
        private const int PacketOwnedClockRealtimeMeridiemPadding = 8;
        private const int PacketOwnedRewardRouletteMaxNumericSuffix = 31;
        private const byte PacketOwnedUiClientAlpha = 255;
        private const int PacketOwnedFieldClockReferenceWidth = 800;
        private const int PacketOwnedFieldClockReferenceHeight = 600;
        private const int PacketOwnedFieldClockDefaultWidth = 258;
        private const int PacketOwnedFieldClockDefaultHeight = 58;
        private const int PacketOwnedFieldClockEventOffsetX = 303;
        private const int PacketOwnedFieldClockEventOffsetY = 30;
        private const int PacketOwnedFieldClockEventWidth = 194;
        private const int PacketOwnedFieldClockEventHeight = 83;
        private const int PacketOwnedFieldClockCakePieSmallOffsetX = 260;
        private const int PacketOwnedFieldClockCakePieSmallOffsetY = 25;
        private const int PacketOwnedFieldClockCakePieSmallWidth = 279;
        private const int PacketOwnedFieldClockCakePieSmallHeight = 88;
        private const int PacketOwnedFieldClockCakePieLargeOffsetX = 204;
        private const int PacketOwnedFieldClockCakePieLargeOffsetY = 25;
        private const int PacketOwnedFieldClockCakePieLargeWidth = 391;
        private const int PacketOwnedFieldClockCakePieLargeHeight = 83;
        private static readonly Color PacketOwnedFieldClockEventBackColor = new(unchecked((uint)-16777152));
        private static readonly Color PacketOwnedFieldClockEventTextColor = new(unchecked((uint)-224));
        private static readonly Color PacketOwnedFieldClockDefaultTextColor = Color.White;
        private static readonly int[] PacketOwnedRewardRouletteLayerStringPoolIds =
        {
            0x11E0,
            0x11E1,
            0x11E2
        };
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
        private static readonly string[] PacketOwnedRewardRouletteFallbackFormats =
        {
            "Effect/BasicEff.img/MainNotice/userReward/Default/{0}",
            "Effect/BasicEff.img/MainNotice/userReward/Notify/{0}",
            "Effect/BasicEff.img/MainNotice/userReward/Appear/{0}"
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
            UpdatePacketOwnedBossTimerClockState(currentTickCount);
            UpdatePacketOwnedFieldClockState(currentTickCount);
            UpdatePacketOwnedFieldFeedbackUiAnimations(currentTickCount);
        }

        private void DrawPacketOwnedFieldFeedbackState(int currentTickCount)
        {
            _packetFieldFeedbackRuntime.Draw(_spriteBatch, _fontChat, _renderParams.RenderWidth, currentTickCount);
            DrawPacketOwnedBossTimerClock(currentTickCount);
            DrawPacketOwnedFieldClock(currentTickCount);
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
                HasMapTransferTarget = HasPacketOwnedWhisperTransferTarget,
                ResolveItemName = ResolvePacketFieldFeedbackItemName,
                ResolveChannelName = ResolvePacketFieldFeedbackChannelName,
                IsBlacklistedName = name => _socialListRuntime.IsBlacklisted(name),
                IsBlockedFriendName = name => _socialListRuntime.IsBlockedFriend(name),
                QueueMapTransfer = TryQueuePacketOwnedWhisperFindTransfer,
                ResolveSwindleWarnings = GetPacketOwnedSwindleWarningEntries,
                ShowBossTimerClock = ShowPacketOwnedBossTimerClock,
                ClearBossTimerClock = ClearPacketOwnedBossTimerClock,
                ShowFieldClock = ShowPacketOwnedFieldClock,
                ClearFieldClock = ClearPacketOwnedFieldClock
            };
        }

        private void ShowPacketOwnedFieldWarning(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            ShowConnectionNoticePrompt(new LoginPacketDialogPromptConfiguration
            {
                Owner = LoginPacketDialogOwner.ConnectionNotice,
                Title = "Warning",
                Body = message.Trim(),
                NoticeVariant = ConnectionNoticeWindowVariant.Notice,
                TrackDirectionModeOwner = true,
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

        private void HandleRemoteFieldSoundEffect(RemoteUserActorPool.RemoteFieldSoundPresentation presentation)
        {
            if (string.IsNullOrWhiteSpace(presentation.SoundPath))
            {
                return;
            }

            TryPlayPacketOwnedFieldFeedbackSound(presentation.SoundPath);
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
            if (!TryGetOrCreatePacketOwnedAnimationFrames(cacheKey, () => ResolvePacketOwnedSummonEffectFrames(effectId), out List<PacketOwnedUiFrame> frames))
            {
                return false;
            }

            _animationEffects?.AddOneTime(frames.Select(static frame => frame.Sprite).ToList(), x, y, flip: false, currTickCount, zOrder: 1);
            return true;
        }

        private bool TryShowPacketOwnedScreenEffect(string descriptor)
        {
            if (string.IsNullOrWhiteSpace(descriptor))
            {
                return false;
            }

            string animationKey = GetPacketOwnedScreenEffectAnimationKey(descriptor);
            string cacheKey = $"screen:{descriptor.Trim()}";
            if (!TryGetOrCreatePacketOwnedUiLayers(cacheKey, () => ResolvePacketOwnedScreenEffectLayers(descriptor), out IReadOnlyList<PacketOwnedCachedUiLayer> layers))
            {
                return false;
            }

            ClearPacketOwnedUiAnimations(animationKey);
            foreach (PacketOwnedCachedUiLayer layer in layers)
            {
                EnqueuePacketOwnedUiAnimation(
                    layer.Frames,
                    ResolvePacketOwnedScreenEffectRegistration(
                        _renderParams.RenderWidth,
                        Height,
                        animationKey,
                        layer.LayerOrder,
                        layer.Repeat),
                    currTickCount);
            }

            return true;
        }

        private bool TryShowPacketOwnedRewardRouletteEffect(int rewardJobIndex, int rewardPartIndex, int rewardLevelIndex)
        {
            const string animationKey = "reward-roulette";
            ClearPacketOwnedUiAnimations(animationKey);

            bool shown = false;
            PacketOwnedRewardRouletteLayerCandidate[] directFamily = BuildPacketOwnedRewardRouletteDirectAnimationFamily(
                rewardJobIndex,
                rewardPartIndex,
                rewardLevelIndex);
            if (TryEnqueuePacketOwnedRewardRouletteAnimationFamily(directFamily, animationKey))
            {
                shown = true;
            }

            foreach (string suffix in EnumeratePacketOwnedRewardRouletteSuffixes())
            {
                bool resolvedSuffix = false;
                foreach (PacketOwnedRewardRouletteLayerCandidate[] family in EnumeratePacketOwnedRewardRouletteAnimationCandidateFamilies(
                    rewardJobIndex,
                    rewardPartIndex,
                    rewardLevelIndex,
                    suffix))
                {
                    if (!TryEnqueuePacketOwnedRewardRouletteAnimationFamily(family, animationKey))
                    {
                        break;
                    }

                    shown = true;
                    resolvedSuffix = true;
                    break;
                }

                if (!resolvedSuffix)
                {
                    break;
                }
            }

            ShowUtilityFeedbackMessage(
                $"Packet-owned reward roulette: job={rewardJobIndex} part={rewardPartIndex} level={rewardLevelIndex}.");
            return shown;
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

            if (!CanQueueKnownCrossMapTransfer(mapId, showFailureMessage: true))
            {
                return false;
            }

            bool queued = QueueMapTransfer(mapId, null);
            if (queued)
            {
                SetPendingMapSpawnTarget(x, y);
                ShowUtilityFeedbackMessage(
                    $"Queued packet-owned whisper follow transfer to {ResolveMapTransferDisplayName(mapId, null)} ({x}, {y}).");
            }

            return queued;
        }

        private bool HasPacketOwnedWhisperTransferTarget(int mapId)
        {
            if (mapId <= 0 || mapId == MapConstants.MaxMap)
            {
                return false;
            }

            if (_mapBoard?.MapInfo?.id == mapId)
            {
                return true;
            }

            return TryResolveMapDisplayNameFromCache(mapId, out _);
        }

        private void ShowPacketOwnedBossTimerClock(PacketFieldBossTimerVisualState state)
        {
            _packetFieldBossTimerClockState = state;
            EnsurePacketOwnedBossTimerAssetsLoaded();
        }

        private void ShowPacketOwnedFieldClock(PacketFieldClockVisualState state)
        {
            _packetFieldClockState = state;
            EnsurePacketOwnedBossTimerAssetsLoaded();
        }

        private void ClearPacketOwnedBossTimerClock()
        {
            _packetFieldBossTimerClockState = null;
        }

        private void ClearPacketOwnedFieldClock()
        {
            _packetFieldClockState = null;
        }

        private void UpdatePacketOwnedBossTimerClockState(int currentTickCount)
        {
            if (_packetFieldBossTimerClockState == null)
            {
                return;
            }

            if (PacketFieldFeedbackRuntime.GetBossTimerRemainingSecondsForTest(_packetFieldBossTimerClockState, currentTickCount) <= 0)
            {
                _packetFieldBossTimerClockState = null;
            }
        }

        private void UpdatePacketOwnedFieldClockState(int currentTickCount)
        {
            if (_packetFieldClockState == null
                || _packetFieldClockState.Kind != PacketFieldClockVisualKind.Countdown)
            {
                return;
            }

            if (PacketFieldFeedbackRuntime.GetFieldClockRemainingSecondsForTest(_packetFieldClockState, currentTickCount) <= 0)
            {
                _packetFieldClockState = null;
            }
        }

        private void EnsurePacketOwnedBossTimerAssetsLoaded()
        {
            if (_packetFieldBossTimerAssetsLoaded || GraphicsDevice == null)
            {
                return;
            }

            _packetFieldClockWindowPixelTexture ??= new Texture2D(GraphicsDevice, 1, 1);
            _packetFieldClockWindowPixelTexture.SetData(new[] { Color.White });

            WzImage mapEtcImage = Program.FindImage("Map", "Obj/etc.img")
                ?? Program.FindImage("Map", "etc.img");
            mapEtcImage?.ParseImage();

            WzImageProperty timerProperty = mapEtcImage?["timer"];
            WzImageProperty clockFontProperty = mapEtcImage?["clock"]?["fontTime"];
            _packetFieldBossTimerBackgroundTexture = LoadPacketOwnedBossTimerTexture(timerProperty?["backgrnd"]);
            _packetFieldBossTimerHourBackgroundTexture = LoadPacketOwnedBossTimerTexture(timerProperty?["backgrndhour"]);
            _packetFieldBossTimerSeparatorTexture = LoadPacketOwnedBossTimerTexture(clockFontProperty?["comma"]);
            _packetFieldClockAmTexture = LoadPacketOwnedBossTimerTexture(clockFontProperty?["am"]);
            _packetFieldClockPmTexture = LoadPacketOwnedBossTimerTexture(clockFontProperty?["pm"]);
            for (int digit = 0; digit < _packetFieldBossTimerDigits.Length; digit++)
            {
                _packetFieldBossTimerDigits[digit] = LoadPacketOwnedBossTimerTexture(clockFontProperty?[digit.ToString(CultureInfo.InvariantCulture)]);
            }

            _packetFieldBossTimerAssetsLoaded = true;
        }

        private Texture2D LoadPacketOwnedBossTimerTexture(WzImageProperty property)
        {
            if (GraphicsDevice == null || property == null)
            {
                return null;
            }

            if (WzInfoTools.GetRealProperty(property) is not WzCanvasProperty canvas)
            {
                return null;
            }

            using System.Drawing.Bitmap bitmap = canvas.GetLinkedWzCanvasBitmap();
            return bitmap?.ToTexture2D(GraphicsDevice);
        }

        private void DrawPacketOwnedBossTimerClock(int currentTickCount)
        {
            if (_spriteBatch == null || _packetFieldBossTimerClockState == null)
            {
                return;
            }

            EnsurePacketOwnedBossTimerAssetsLoaded();
            int remainingSeconds = PacketFieldFeedbackRuntime.GetBossTimerRemainingSecondsForTest(_packetFieldBossTimerClockState, currentTickCount);
            if (remainingSeconds <= 0)
            {
                _packetFieldBossTimerClockState = null;
                return;
            }

            bool showHours = remainingSeconds >= 3600;
            Texture2D background = showHours
                ? _packetFieldBossTimerHourBackgroundTexture ?? _packetFieldBossTimerBackgroundTexture
                : _packetFieldBossTimerBackgroundTexture ?? _packetFieldBossTimerHourBackgroundTexture;
            if (background == null)
            {
                return;
            }

            Vector2 boardPosition = new(
                Math.Max(0f, (_renderParams.RenderWidth - background.Width) / 2f),
                PacketOwnedBossTimerOffsetY);
            _spriteBatch.Draw(background, boardPosition, Color.White);

            string timerText = FormatPacketOwnedBossTimerText(remainingSeconds, showHours);
            if (!TryDrawPacketOwnedBossTimerDigits(timerText, boardPosition, background))
            {
                if (_fontChat == null)
                {
                    return;
                }

                Vector2 fallbackPosition = boardPosition + new Vector2(32f, 15f);
                _spriteBatch.DrawString(_fontChat, timerText, fallbackPosition, Color.White, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);
            }
        }

        private bool TryDrawPacketOwnedBossTimerDigits(string timerText, Vector2 boardPosition, Texture2D background)
        {
            if (string.IsNullOrWhiteSpace(timerText)
                || _packetFieldBossTimerSeparatorTexture == null
                || _packetFieldBossTimerDigits.Any(static texture => texture == null))
            {
                return false;
            }

            List<Texture2D> glyphs = new(timerText.Length);
            int totalWidth = 0;
            foreach (char character in timerText)
            {
                Texture2D glyph = character switch
                {
                    ':' => _packetFieldBossTimerSeparatorTexture,
                    >= '0' and <= '9' => _packetFieldBossTimerDigits[character - '0'],
                    _ => null
                };
                if (glyph == null)
                {
                    return false;
                }

                glyphs.Add(glyph);
                totalWidth += glyph.Width;
            }

            totalWidth += PacketOwnedBossTimerDigitSpacing * Math.Max(0, glyphs.Count - 1);
            float drawX = boardPosition.X + Math.Max(0f, (background.Width - totalWidth) / 2f);
            float drawY = boardPosition.Y + Math.Max(0f, (background.Height - glyphs.Max(static texture => texture.Height)) / 2f);
            foreach (Texture2D glyph in glyphs)
            {
                _spriteBatch.Draw(glyph, new Vector2(drawX, drawY), Color.White);
                drawX += glyph.Width + PacketOwnedBossTimerDigitSpacing;
            }

            return true;
        }

        private static string FormatPacketOwnedBossTimerText(int remainingSeconds, bool showHours)
        {
            remainingSeconds = Math.Max(0, remainingSeconds);
            int hours = remainingSeconds / 3600;
            int minutes = (remainingSeconds / 60) % 60;
            int seconds = remainingSeconds % 60;
            return showHours
                ? string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", hours, minutes)
                : string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", minutes, seconds);
        }

        internal static string FormatPacketOwnedBossTimerTextForTest(int remainingSeconds, bool showHours)
        {
            return FormatPacketOwnedBossTimerText(remainingSeconds, showHours);
        }

        internal static IReadOnlyList<(int Digit, int X, int Y)> GetPacketOwnedClockTimerDigitPlanForTest(int remainingSeconds, bool showHours)
        {
            return BuildPacketOwnedClockTimerDigitPlan(remainingSeconds, showHours);
        }

        internal static (float MeridiemX, float Y, float HourTensX, float HourOnesX, float ColonX, float MinuteTensX, float MinuteOnesX) GetPacketOwnedRealtimeClockDigitPositionsForTest(
            int backgroundWidth,
            int backgroundHeight,
            int digitWidth,
            int digitHeight,
            int separatorWidth,
            int meridiemWidth)
        {
            return ResolvePacketOwnedRealtimeClockDigitPositions(
                backgroundWidth,
                backgroundHeight,
                digitWidth,
                digitHeight,
                separatorWidth,
                meridiemWidth);
        }

        private void DrawPacketOwnedFieldClock(int currentTickCount)
        {
            if (_spriteBatch == null || _packetFieldClockState == null)
            {
                return;
            }

            EnsurePacketOwnedBossTimerAssetsLoaded();
            Texture2D background = _packetFieldBossTimerHourBackgroundTexture ?? _packetFieldBossTimerBackgroundTexture;
            PacketOwnedFieldClockLayout layout = ResolvePacketOwnedFieldClockLayout(
                _packetFieldClockState.Variant,
                _renderParams.RenderWidth,
                Height);
            if (background == null && !layout.DrawSolidWindow)
            {
                return;
            }

            Rectangle layoutBounds = ResolvePacketOwnedFieldClockBounds(
                layout,
                background?.Width ?? 0,
                background?.Height ?? 0,
                _renderParams.RenderWidth,
                Height);
            if (layout.DrawSolidWindow && _packetFieldClockWindowPixelTexture != null)
            {
                _spriteBatch.Draw(_packetFieldClockWindowPixelTexture, layoutBounds, layout.BackColor);
            }

            Vector2 boardPosition = ResolvePacketOwnedFieldClockBoardPosition(layoutBounds, background);
            if (background != null)
            {
                _spriteBatch.Draw(background, boardPosition, Color.White);
            }

            if (_packetFieldClockState.Kind == PacketFieldClockVisualKind.Realtime)
            {
                (bool isPm, int hour, int minute, int second) = PacketFieldFeedbackRuntime.ResolveFieldClockDisplayTimeForTest(_packetFieldClockState, currentTickCount);
                string timeText = string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", hour, minute);
                if (!TryDrawPacketOwnedRealtimeClockDigits(isPm, hour, minute, second, boardPosition, background))
                {
                    DrawPacketOwnedClockFallbackText(layoutBounds, background, $"{(isPm ? "PM" : "AM")} {timeText}", layout.FallbackTextColor);
                }

                return;
            }

            int remainingSeconds = PacketFieldFeedbackRuntime.GetFieldClockRemainingSecondsForTest(_packetFieldClockState, currentTickCount);
            if (remainingSeconds <= 0)
            {
                _packetFieldClockState = null;
                return;
            }

            bool showHours = remainingSeconds >= 3600;
            string timerText = FormatPacketOwnedBossTimerText(remainingSeconds, showHours);
            if (!TryDrawPacketOwnedClockTimerDigits(remainingSeconds, showHours, boardPosition, background))
            {
                DrawPacketOwnedClockFallbackText(layoutBounds, background, timerText, layout.FallbackTextColor);
            }
        }

        private bool TryDrawPacketOwnedClockTimerDigits(int remainingSeconds, bool showHours, Vector2 boardPosition, Texture2D background)
        {
            if (background == null || _packetFieldBossTimerDigits.Any(static texture => texture == null))
            {
                return false;
            }

            foreach ((int digit, int x, int y) in BuildPacketOwnedClockTimerDigitPlan(remainingSeconds, showHours))
            {
                _spriteBatch.Draw(
                    _packetFieldBossTimerDigits[digit],
                    new Vector2(boardPosition.X + x, boardPosition.Y + y),
                    Color.White);
            }

            return true;
        }

        private bool TryDrawPacketOwnedRealtimeClockDigits(
            bool isPm,
            int hour,
            int minute,
            int second,
            Vector2 boardPosition,
            Texture2D background)
        {
            Texture2D meridiemTexture = isPm ? _packetFieldClockPmTexture : _packetFieldClockAmTexture;
            if (background == null
                || meridiemTexture == null
                || _packetFieldBossTimerSeparatorTexture == null
                || _packetFieldBossTimerDigits.Any(static texture => texture == null))
            {
                return false;
            }

            (float meridiemX, float y, float hourTensX, float hourOnesX, float colonX, float minuteTensX, float minuteOnesX) =
                ResolvePacketOwnedRealtimeClockDigitPositions(
                    background.Width,
                    background.Height,
                    _packetFieldBossTimerDigits[0].Width,
                    _packetFieldBossTimerDigits[0].Height,
                    _packetFieldBossTimerSeparatorTexture.Width,
                    meridiemTexture.Width);

            _spriteBatch.Draw(meridiemTexture, boardPosition + new Vector2(meridiemX, y), Color.White);
            _spriteBatch.Draw(_packetFieldBossTimerDigits[(Math.Max(0, hour) / 10) % 10], boardPosition + new Vector2(hourTensX, y), Color.White);
            _spriteBatch.Draw(_packetFieldBossTimerDigits[Math.Max(0, hour) % 10], boardPosition + new Vector2(hourOnesX, y), Color.White);
            byte colonAlpha = second % 2 == 0
                ? (byte)PacketOwnedUiClientAlpha
                : (byte)PacketOwnedClockRealtimeColonBlinkAlpha;
            _spriteBatch.Draw(
                _packetFieldBossTimerSeparatorTexture,
                boardPosition + new Vector2(colonX, y),
                new Color(byte.MaxValue, byte.MaxValue, byte.MaxValue, colonAlpha));
            _spriteBatch.Draw(_packetFieldBossTimerDigits[(Math.Max(0, minute) / 10) % 10], boardPosition + new Vector2(minuteTensX, y), Color.White);
            _spriteBatch.Draw(_packetFieldBossTimerDigits[Math.Max(0, minute) % 10], boardPosition + new Vector2(minuteOnesX, y), Color.White);
            return true;
        }

        private static IReadOnlyList<(int Digit, int X, int Y)> BuildPacketOwnedClockTimerDigitPlan(int remainingSeconds, bool showHours)
        {
            remainingSeconds = Math.Max(0, remainingSeconds);
            int leftValue = showHours
                ? (remainingSeconds / 3600) % 100
                : (remainingSeconds / 60) % 100;
            int rightValue = showHours
                ? (remainingSeconds / 60) % 60
                : remainingSeconds % 60;

            List<(int Digit, int X, int Y)> digits = new(4);
            int leftTens = (leftValue / 10) % 10;
            if (leftTens != 0)
            {
                digits.Add((leftTens, PacketOwnedClockTimerDigitX1, PacketOwnedClockTimerDigitY));
            }

            digits.Add((leftValue % 10, PacketOwnedClockTimerDigitX2, PacketOwnedClockTimerDigitY));
            digits.Add(((rightValue / 10) % 10, PacketOwnedClockTimerDigitX3, PacketOwnedClockTimerDigitY));
            digits.Add((rightValue % 10, PacketOwnedClockTimerDigitX4, PacketOwnedClockTimerDigitY));
            return digits;
        }

        private static (float MeridiemX, float Y, float HourTensX, float HourOnesX, float ColonX, float MinuteTensX, float MinuteOnesX) ResolvePacketOwnedRealtimeClockDigitPositions(
            int backgroundWidth,
            int backgroundHeight,
            int digitWidth,
            int digitHeight,
            int separatorWidth,
            int meridiemWidth)
        {
            float meridiemX = Math.Max(
                0f,
                (backgroundWidth - ((2 * ((2 * digitWidth) + separatorWidth)) + meridiemWidth + PacketOwnedClockRealtimeMeridiemPadding)) / 2f);
            float y = Math.Max(0f, (backgroundHeight - digitHeight) / 2f);
            float hourTensX = meridiemX + meridiemWidth + separatorWidth;
            float hourOnesX = hourTensX + digitWidth + PacketOwnedClockRealtimeDigitSpacing;
            float colonX = hourOnesX + digitWidth + PacketOwnedClockRealtimeDigitSpacing;
            float minuteTensX = colonX + separatorWidth + PacketOwnedClockRealtimeDigitSpacing;
            float minuteOnesX = minuteTensX + digitWidth + PacketOwnedClockRealtimeDigitSpacing;
            return (meridiemX, y, hourTensX, hourOnesX, colonX, minuteTensX, minuteOnesX);
        }

        private void DrawPacketOwnedClockFallbackText(Rectangle layoutBounds, Texture2D background, string text, Color color)
        {
            if (_fontChat == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            int backgroundHeight = background?.Height ?? layoutBounds.Height;
            Vector2 fallbackPosition = new(
                layoutBounds.X + 24f,
                layoutBounds.Y + Math.Max(8f, (backgroundHeight / 2f) - 10f));
            _spriteBatch.DrawString(_fontChat, text, fallbackPosition, color, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);
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

            foreach (PacketOwnedUiAnimation animation in _packetFieldFeedbackUiAnimations
                .OrderBy(static animation => animation.DrawOrder)
                .ThenBy(static animation => animation.LayerOrder)
                .ThenBy(static animation => animation.StartedAtTick))
            {
                PacketOwnedUiFrameState frameState = animation.ResolveFrameState(currentTickCount);
                IDXObject frame = frameState.Frame.Sprite;
                if (frame?.Texture == null)
                {
                    continue;
                }

                Vector2 position = animation.ResolveDrawPosition(frame, _renderParams.RenderWidth, Height);
                _spriteBatch.Draw(frame.Texture, position, animation.ResolveTint(frameState.FrameAlpha));
            }
        }

        private void EnqueuePacketOwnedUiAnimation(IReadOnlyList<PacketOwnedUiFrame> frames, PacketOwnedUiRegistration registration, int currentTickCount)
        {
            if (frames == null || frames.Count == 0)
            {
                return;
            }

            _packetFieldFeedbackUiAnimations.Add(new PacketOwnedUiAnimation(frames, registration, currentTickCount));
        }

        private void ClearPacketOwnedUiAnimations(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            _packetFieldFeedbackUiAnimations.RemoveAll(animation => string.Equals(animation.Key, key, StringComparison.Ordinal));
        }

        private bool TryGetOrCreatePacketOwnedAnimationFrames(string cacheKey, Func<List<PacketOwnedUiFrame>> loader, out List<PacketOwnedUiFrame> frames)
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

        private static List<IDXObject> ExtractPacketOwnedFrameSprites(IReadOnlyList<PacketOwnedUiFrame> frames)
        {
            if (frames == null || frames.Count == 0)
            {
                return null;
            }

            List<IDXObject> sprites = new(frames.Count);
            for (int i = 0; i < frames.Count; i++)
            {
                IDXObject sprite = frames[i].Sprite;
                if (sprite != null)
                {
                    sprites.Add(sprite);
                }
            }

            return sprites.Count > 0 ? sprites : null;
        }

        private bool TryGetOrCreatePacketOwnedUiLayers(string cacheKey, Func<IReadOnlyList<PacketOwnedCachedUiLayer>> loader, out IReadOnlyList<PacketOwnedCachedUiLayer> layers)
        {
            if (_packetFieldFeedbackUiLayerCache.TryGetValue(cacheKey, out layers) && layers?.Count > 0)
            {
                return true;
            }

            layers = loader?.Invoke();
            if (layers == null || layers.Count == 0)
            {
                layers = null;
                return false;
            }

            _packetFieldFeedbackUiLayerCache[cacheKey] = layers;
            return true;
        }

        private List<PacketOwnedUiFrame> ResolvePacketOwnedSummonEffectFrames(byte effectId)
        {
            WzImage summonImage = Program.FindImage("Effect", "Summon.img");
            List<PacketOwnedUiFrame> frames = LoadPacketOwnedAnimationFrames(
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

        private IReadOnlyList<PacketOwnedCachedUiLayer> ResolvePacketOwnedScreenEffectLayers(string descriptor)
        {
            foreach ((string categoryName, string imageName, string propertyPath) in EnumeratePacketOwnedScreenEffectCandidates(descriptor))
            {
                WzImage image = Program.FindImage(categoryName, imageName);
                IReadOnlyList<PacketOwnedCachedUiLayer> layers = LoadPacketOwnedAnimationLayers(
                    ResolvePacketOwnedPropertyPath(image, propertyPath));
                if (layers?.Count > 0)
                {
                    return layers;
                }
            }

            return null;
        }

        private static IEnumerable<(string CategoryName, string ImageName, string PropertyPath)> EnumeratePacketOwnedScreenEffectCandidates(string descriptor)
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
                out (string CategoryName, string ImageName, string PropertyPath) clientCandidate))
            {
                yield return clientCandidate;
            }

            int imageSeparator = normalized.IndexOf(".img/", StringComparison.OrdinalIgnoreCase);
            if (imageSeparator >= 0)
            {
                string imageName = normalized[..(imageSeparator + 4)];
                string propertyPath = normalized[(imageSeparator + 5)..];
                yield return ("Effect", imageName, propertyPath);
            }

            string[] variants =
            {
                normalized,
                normalized.Contains('/') ? normalized[(normalized.IndexOf('/') + 1)..] : normalized
            };

            foreach (string variant in variants.Where(static entry => !string.IsNullOrWhiteSpace(entry)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                yield return ("Effect", "MapEff.img", variant);
            }

            foreach (string imageName in PacketOwnedScreenEffectImageNames)
            {
                foreach (string variant in variants.Where(static entry => !string.IsNullOrWhiteSpace(entry)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    yield return ("Effect", imageName, variant);
                }
            }
        }

        private List<PacketOwnedUiFrame> LoadPacketOwnedAnimationFrames(WzImageProperty sourceProperty, int fallbackDelay = 90)
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

            List<PacketOwnedUiFrame> frames = new();
            int sharedDelay = sourceProperty["delay"]?.GetInt() ?? fallbackDelay;
            PacketOwnedUiAlphaRange sharedAlphaRange = ResolvePacketOwnedUiAlphaRange(sourceProperty);

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
                PacketOwnedUiAlphaRange alphaRange = ResolvePacketOwnedUiAlphaRange(frameCanvas, sharedAlphaRange);
                using (frameBitmap)
                {
                    Texture2D texture = frameBitmap.ToTexture2D(GraphicsDevice);
                    frames.Add(new PacketOwnedUiFrame(
                        new DXObject(frameCanvas.GetCanvasOriginPosition(), texture, delay),
                        alphaRange.StartAlpha,
                        alphaRange.EndAlpha));
                }
            }

            return frames.Count > 0 ? frames : null;
        }

        private IReadOnlyList<PacketOwnedCachedUiLayer> LoadPacketOwnedAnimationLayers(WzImageProperty sourceProperty, int fallbackDelay = 90)
        {
            sourceProperty = sourceProperty?.GetLinkedWzImageProperty() ?? sourceProperty;
            if (sourceProperty == null)
            {
                return null;
            }

            List<PacketOwnedCachedUiLayer> layers = new();
            CollectPacketOwnedAnimationLayers(sourceProperty, fallbackDelay, depth: 0, discoveryOrder: 0, layers);
            return layers.Count > 0
                ? layers
                    .OrderBy(static layer => layer.LayerOrder)
                    .ToArray()
                : null;
        }

        private void CollectPacketOwnedAnimationLayers(
            WzImageProperty sourceProperty,
            int fallbackDelay,
            int depth,
            int discoveryOrder,
            ICollection<PacketOwnedCachedUiLayer> layers)
        {
            sourceProperty = sourceProperty?.GetLinkedWzImageProperty() ?? sourceProperty;
            if (sourceProperty == null || depth > 8)
            {
                return;
            }

            List<PacketOwnedUiFrame> directFrames = LoadPacketOwnedAnimationFrames(sourceProperty, fallbackDelay);
            if (directFrames?.Count > 0)
            {
                layers.Add(new PacketOwnedCachedUiLayer(
                    directFrames,
                    ComposePacketOwnedUiLayerOrder(ResolvePacketOwnedAnimationLayerZ(sourceProperty), discoveryOrder),
                    ResolvePacketOwnedAnimationRepeat(sourceProperty)));
                return;
            }

            int childIndex = 0;
            foreach (WzImageProperty child in sourceProperty.WzProperties)
            {
                if (IsPacketOwnedAnimationMetadataProperty(child))
                {
                    continue;
                }

                int nextDiscoveryOrder = checked((discoveryOrder * 32) + childIndex + 1);
                CollectPacketOwnedAnimationLayers(child, fallbackDelay, depth + 1, nextDiscoveryOrder, layers);
                childIndex++;
            }
        }

        private List<PacketOwnedUiFrame> LoadPacketOwnedCanvasFrame(WzCanvasProperty canvasProperty, int fallbackDelay)
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
            PacketOwnedUiAlphaRange alphaRange = ResolvePacketOwnedUiAlphaRange(canvasProperty);
            using (frameBitmap)
            {
                Texture2D texture = frameBitmap.ToTexture2D(GraphicsDevice);
                return new List<PacketOwnedUiFrame>
                {
                    new(
                        new DXObject(canvasProperty.GetCanvasOriginPosition(), texture, delay),
                        alphaRange.StartAlpha,
                        alphaRange.EndAlpha)
                };
            }
        }

        private static PacketOwnedUiAlphaRange ResolvePacketOwnedUiAlphaRange(WzImageProperty sourceProperty, PacketOwnedUiAlphaRange? fallback = null)
        {
            sourceProperty = sourceProperty?.GetLinkedWzImageProperty() ?? sourceProperty;
            int defaultAlpha = fallback?.StartAlpha ?? PacketOwnedUiClientAlpha;
            int startAlpha = sourceProperty?["a0"]?.GetInt()
                ?? sourceProperty?["alpha"]?.GetInt()
                ?? defaultAlpha;
            int endAlpha = sourceProperty?["a1"]?.GetInt()
                ?? sourceProperty?["alpha"]?.GetInt()
                ?? fallback?.EndAlpha
                ?? startAlpha;
            return new PacketOwnedUiAlphaRange(
                (byte)Math.Clamp(startAlpha, byte.MinValue, byte.MaxValue),
                (byte)Math.Clamp(endAlpha, byte.MinValue, byte.MaxValue));
        }

        internal static byte ResolvePacketOwnedUiFrameAlphaForTest(int elapsedInFrameMs, int frameDelayMs, byte registrationAlpha, byte startAlpha, byte endAlpha)
        {
            return ResolvePacketOwnedUiFrameAlpha(elapsedInFrameMs, frameDelayMs, registrationAlpha, startAlpha, endAlpha);
        }

        private static byte ResolvePacketOwnedUiFrameAlpha(int elapsedInFrameMs, int frameDelayMs, byte registrationAlpha, byte startAlpha, byte endAlpha)
        {
            int clampedDelay = Math.Max(1, frameDelayMs);
            float progress = Math.Clamp(elapsedInFrameMs, 0, clampedDelay) / (float)clampedDelay;
            float interpolatedAlpha = startAlpha + ((endAlpha - startAlpha) * progress);
            float combinedAlpha = registrationAlpha * (interpolatedAlpha / byte.MaxValue);
            return (byte)Math.Clamp((int)MathF.Round(combinedAlpha), byte.MinValue, byte.MaxValue);
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
                out (string CategoryName, string ImageName, string PropertyPath) candidate)
                ? candidate.PropertyPath
                : effectId.ToString(CultureInfo.InvariantCulture);
        }

        private static bool TryResolvePacketOwnedEffectUol(
            int stringPoolId,
            string fallbackFormat,
            string argument,
            out (string CategoryName, string ImageName, string PropertyPath) candidate)
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
            if (string.IsNullOrWhiteSpace(format))
            {
                return string.Empty;
            }

            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, argument);
            }
            catch (FormatException)
            {
                return string.IsNullOrWhiteSpace(fallbackFormat)
                    ? string.Empty
                    : string.Format(CultureInfo.InvariantCulture, fallbackFormat, argument);
            }
        }

        private static bool TryParsePacketOwnedEffectUol(string uol, out (string CategoryName, string ImageName, string PropertyPath) candidate)
        {
            candidate = default;
            if (string.IsNullOrWhiteSpace(uol))
            {
                return false;
            }

            string normalized = uol.Trim().Replace('\\', '/').Trim('/');
            string categoryName = "Effect";
            int firstSeparator = normalized.IndexOf('/');
            int imageSeparator = normalized.IndexOf(".img/", StringComparison.OrdinalIgnoreCase);
            if (firstSeparator > 0
                && imageSeparator > firstSeparator
                && !normalized[..firstSeparator].EndsWith(".img", StringComparison.OrdinalIgnoreCase))
            {
                categoryName = normalized[..firstSeparator];
                normalized = normalized[(firstSeparator + 1)..];
            }

            if (normalized.StartsWith("Effect/", StringComparison.OrdinalIgnoreCase))
            {
                categoryName = "Effect";
                normalized = normalized["Effect/".Length..];
            }

            imageSeparator = normalized.IndexOf(".img/", StringComparison.OrdinalIgnoreCase);
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

            candidate = (categoryName, imageName, propertyPath);
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
                .Select(static candidate => $"{candidate.CategoryName}:{candidate.ImageName}:{candidate.PropertyPath}")
                .ToArray();
        }

        internal static IReadOnlyList<string> GetPacketOwnedRewardRouletteAnimationCandidatesForTest(
            int rewardJobIndex,
            int rewardPartIndex,
            int rewardLevelIndex)
        {
            return EnumeratePacketOwnedRewardRouletteAnimationCandidates(
                    rewardJobIndex,
                    rewardPartIndex,
                    rewardLevelIndex)
                .Select(static candidate => $"{candidate.CategoryName}:{candidate.ImageName}:{candidate.PropertyPath}")
                .ToArray();
        }

        internal static IReadOnlyList<IReadOnlyList<string>> GetPacketOwnedRewardRouletteAnimationCandidateFamiliesForTest(
            int rewardJobIndex,
            int rewardPartIndex,
            int rewardLevelIndex)
        {
            return EnumeratePacketOwnedRewardRouletteAnimationCandidateFamilies(
                    rewardJobIndex,
                    rewardPartIndex,
                    rewardLevelIndex)
                .Select(static family => (IReadOnlyList<string>)family
                    .Select(static candidate => $"{candidate.CategoryName}:{candidate.ImageName}:{candidate.PropertyPath}")
                    .ToArray())
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

        private static IEnumerable<(string CategoryName, string ImageName, string PropertyPath)> EnumeratePacketOwnedRewardRouletteAnimationCandidates(
            int rewardJobIndex,
            int rewardPartIndex,
            int rewardLevelIndex)
        {
            foreach (IReadOnlyList<PacketOwnedRewardRouletteLayerCandidate> family in EnumeratePacketOwnedRewardRouletteAnimationCandidateFamilies(
                rewardJobIndex,
                rewardPartIndex,
                rewardLevelIndex))
            {
                foreach (PacketOwnedRewardRouletteLayerCandidate candidate in family)
                {
                    yield return (candidate.CategoryName, candidate.ImageName, candidate.PropertyPath);
                }
            }
        }

        private static IEnumerable<PacketOwnedRewardRouletteLayerCandidate[]> EnumeratePacketOwnedRewardRouletteAnimationCandidateFamilies(
            int rewardJobIndex,
            int rewardPartIndex,
            int rewardLevelIndex)
        {
            foreach (string suffix in EnumeratePacketOwnedRewardRouletteSuffixes())
            {
                foreach (PacketOwnedRewardRouletteLayerCandidate[] family in EnumeratePacketOwnedRewardRouletteAnimationCandidateFamilies(
                    rewardJobIndex,
                    rewardPartIndex,
                    rewardLevelIndex,
                    suffix))
                {
                    yield return family;
                }
            }
        }

        private bool TryEnqueuePacketOwnedRewardRouletteAnimationFamily(
            IReadOnlyList<PacketOwnedRewardRouletteLayerCandidate> family,
            string animationKey)
        {
            if (family == null || family.Count == 0)
            {
                return false;
            }

            List<(List<PacketOwnedUiFrame> Frames, PacketOwnedRewardRouletteLayerCandidate Candidate)> resolvedLayers = new(family.Count);
            foreach (PacketOwnedRewardRouletteLayerCandidate candidate in family)
            {
                string cacheKey = $"reward-roulette:{candidate.CategoryName}/{candidate.ImageName}:{candidate.PropertyPath}";
                if (!TryGetOrCreatePacketOwnedAnimationFrames(
                    cacheKey,
                    () => LoadPacketOwnedAnimationFrames(
                        ResolvePacketOwnedPropertyPath(
                            Program.FindImage(candidate.CategoryName, candidate.ImageName),
                            candidate.PropertyPath)),
                    out List<PacketOwnedUiFrame> frames))
                {
                    return false;
                }

                resolvedLayers.Add((frames, candidate));
            }

            foreach ((List<PacketOwnedUiFrame> frames, PacketOwnedRewardRouletteLayerCandidate candidate) in resolvedLayers)
            {
                EnqueuePacketOwnedUiAnimation(
                    frames,
                    ResolvePacketOwnedRewardRouletteRegistration(
                        _renderParams.RenderWidth,
                        Height,
                        animationKey,
                        candidate.LayerRole),
                    currTickCount);
            }

            return resolvedLayers.Count > 0;
        }

        private static IEnumerable<PacketOwnedRewardRouletteLayerCandidate[]> EnumeratePacketOwnedRewardRouletteAnimationCandidateFamilies(
            int rewardJobIndex,
            int rewardPartIndex,
            int rewardLevelIndex,
            string suffix)
        {
            PacketOwnedRewardRouletteLayerCandidate[] mapFamily = BuildPacketOwnedRewardRouletteStringPoolAnimationFamily(
                rewardJobIndex,
                rewardPartIndex,
                rewardLevelIndex,
                suffix);
            if (mapFamily.Length > 0)
            {
                yield return mapFamily;
            }

            PacketOwnedRewardRouletteLayerCandidate[] fallbackFamily = BuildPacketOwnedRewardRouletteFallbackAnimationFamily(
                rewardJobIndex,
                rewardPartIndex,
                rewardLevelIndex,
                suffix);
            if (fallbackFamily.Length > 0)
            {
                yield return fallbackFamily;
            }
        }

        private static IEnumerable<string> EnumeratePacketOwnedRewardRouletteSuffixes()
        {
            yield return "Default";
            for (int suffix = 0; suffix <= PacketOwnedRewardRouletteMaxNumericSuffix; suffix++)
            {
                yield return suffix.ToString(CultureInfo.InvariantCulture);
            }
        }

        internal static IReadOnlyList<string> EnumeratePacketOwnedRewardRouletteSuffixesForTest(int maxNumericSuffix)
        {
            maxNumericSuffix = Math.Max(0, maxNumericSuffix);
            List<string> suffixes = new(maxNumericSuffix + 2)
            {
                "Default"
            };

            for (int suffix = 0; suffix <= maxNumericSuffix; suffix++)
            {
                suffixes.Add(suffix.ToString(CultureInfo.InvariantCulture));
            }

            return suffixes;
        }

        private static PacketOwnedRewardRouletteLayerCandidate[] BuildPacketOwnedRewardRouletteDirectAnimationFamily(
            int rewardJobIndex,
            int rewardPartIndex,
            int rewardLevelIndex)
        {
            PacketOwnedRewardRouletteLayerRequest[] layerRequests =
            {
                new(PacketOwnedRewardRouletteLayerRole.Job, PacketOwnedRewardRouletteLayerStringPoolIds[0], rewardJobIndex, PacketOwnedRewardRouletteFallbackFormats[0]),
                new(PacketOwnedRewardRouletteLayerRole.Part, PacketOwnedRewardRouletteLayerStringPoolIds[1], rewardPartIndex, PacketOwnedRewardRouletteFallbackFormats[1]),
                new(PacketOwnedRewardRouletteLayerRole.Level, PacketOwnedRewardRouletteLayerStringPoolIds[2], rewardLevelIndex, PacketOwnedRewardRouletteFallbackFormats[2])
            };
            List<PacketOwnedRewardRouletteLayerCandidate> resolved = new(layerRequests.Length);

            foreach (PacketOwnedRewardRouletteLayerRequest request in layerRequests)
            {
                if (request.RequestedIndex < 0)
                {
                    return Array.Empty<PacketOwnedRewardRouletteLayerCandidate>();
                }

                string directUol = FormatPacketOwnedEffectUol(
                    request.StringPoolId,
                    null,
                    request.RequestedIndex.ToString(CultureInfo.InvariantCulture));
                if (!TryParsePacketOwnedEffectUol(directUol, out (string CategoryName, string ImageName, string PropertyPath) candidate))
                {
                    return Array.Empty<PacketOwnedRewardRouletteLayerCandidate>();
                }

                resolved.Add(new PacketOwnedRewardRouletteLayerCandidate(request.LayerRole, candidate.CategoryName, candidate.ImageName, candidate.PropertyPath));
            }

            return resolved.ToArray();
        }

        private static PacketOwnedRewardRouletteLayerCandidate[] BuildPacketOwnedRewardRouletteStringPoolAnimationFamily(
            int rewardJobIndex,
            int rewardPartIndex,
            int rewardLevelIndex,
            string suffix)
        {
            PacketOwnedRewardRouletteLayerRequest[] layerRequests =
            {
                new(PacketOwnedRewardRouletteLayerRole.Job, PacketOwnedRewardRouletteLayerStringPoolIds[0], rewardJobIndex, PacketOwnedRewardRouletteFallbackFormats[0]),
                new(PacketOwnedRewardRouletteLayerRole.Part, PacketOwnedRewardRouletteLayerStringPoolIds[1], rewardPartIndex, PacketOwnedRewardRouletteFallbackFormats[1]),
                new(PacketOwnedRewardRouletteLayerRole.Level, PacketOwnedRewardRouletteLayerStringPoolIds[2], rewardLevelIndex, PacketOwnedRewardRouletteFallbackFormats[2])
            };
            List<PacketOwnedRewardRouletteLayerCandidate> resolved = new(layerRequests.Length);

            foreach (PacketOwnedRewardRouletteLayerRequest request in layerRequests)
            {
                if (request.RequestedIndex < 0)
                {
                    return Array.Empty<PacketOwnedRewardRouletteLayerCandidate>();
                }

                string joinedUol = FormatPacketOwnedRewardRouletteLayerPath(
                    request.StringPoolId,
                    request.RequestedIndex,
                    suffix);
                if (!TryParsePacketOwnedEffectUol(joinedUol, out (string CategoryName, string ImageName, string PropertyPath) candidate))
                {
                    return Array.Empty<PacketOwnedRewardRouletteLayerCandidate>();
                }

                resolved.Add(new PacketOwnedRewardRouletteLayerCandidate(request.LayerRole, candidate.CategoryName, candidate.ImageName, candidate.PropertyPath));
            }

            return resolved.ToArray();
        }

        private static PacketOwnedRewardRouletteLayerCandidate[] BuildPacketOwnedRewardRouletteFallbackAnimationFamily(
            int rewardJobIndex,
            int rewardPartIndex,
            int rewardLevelIndex,
            string suffix)
        {
            PacketOwnedRewardRouletteLayerRequest[] layerRequests =
            {
                new(PacketOwnedRewardRouletteLayerRole.Job, PacketOwnedRewardRouletteLayerStringPoolIds[0], rewardJobIndex, PacketOwnedRewardRouletteFallbackFormats[0]),
                new(PacketOwnedRewardRouletteLayerRole.Part, PacketOwnedRewardRouletteLayerStringPoolIds[1], rewardPartIndex, PacketOwnedRewardRouletteFallbackFormats[1]),
                new(PacketOwnedRewardRouletteLayerRole.Level, PacketOwnedRewardRouletteLayerStringPoolIds[2], rewardLevelIndex, PacketOwnedRewardRouletteFallbackFormats[2])
            };
            List<PacketOwnedRewardRouletteLayerCandidate> resolved = new(layerRequests.Length);

            foreach (PacketOwnedRewardRouletteLayerRequest request in layerRequests)
            {
                if (request.RequestedIndex < 0)
                {
                    return Array.Empty<PacketOwnedRewardRouletteLayerCandidate>();
                }

                string propertyPath = string.Equals(suffix, "0", StringComparison.Ordinal)
                    ? $"{PacketOwnedRewardRouletteLayerPaths[(int)request.LayerRole]}/0"
                    : string.Format(CultureInfo.InvariantCulture, request.FallbackFormat, request.RequestedIndex)
                        .Replace('\\', '/')
                        .Split(new[] { ".img/" }, StringSplitOptions.None)[^1];
                resolved.Add(new PacketOwnedRewardRouletteLayerCandidate(
                    request.LayerRole,
                    "Effect",
                    "BasicEff.img",
                    propertyPath));
            }

            return resolved.ToArray();
        }

        private static string FormatPacketOwnedRewardRouletteLayerPath(int layerStringPoolId, int requestedIndex, string suffix)
        {
            string basePath = FormatPacketOwnedEffectUol(
                layerStringPoolId,
                null,
                requestedIndex.ToString(CultureInfo.InvariantCulture));
            if (string.IsNullOrWhiteSpace(basePath))
            {
                return string.Empty;
            }

            bool usedResolvedText;
            string joinFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                PacketOwnedRewardRoulettePathJoinStringPoolId,
                "{0}{1}",
                maxPlaceholderCount: 2,
                out usedResolvedText);
            try
            {
                return string.Format(CultureInfo.InvariantCulture, joinFormat, basePath, suffix ?? string.Empty);
            }
            catch (FormatException)
            {
                return string.Concat(basePath, suffix ?? string.Empty);
            }
        }

        private static PacketOwnedUiRegistration ResolvePacketOwnedScreenEffectRegistration(
            int renderWidth,
            int renderHeight,
            string key,
            int layerOrder = 0,
            bool repeat = false)
        {
            return new PacketOwnedUiRegistration(
                PacketOwnedUiAnchorMode.WindowCenter,
                0,
                ScalePacketOwnedUiOffset(PacketOwnedScreenEffectYOffset, renderHeight, PacketOwnedUiReferenceHeight),
                key ?? string.Empty,
                PacketOwnedUiDrawOrder.ScreenEffect,
                PacketOwnedUiClientAlpha,
                layerOrder,
                repeat);
        }

        private static PacketOwnedUiRegistration ResolvePacketOwnedRewardRouletteRegistration(
            int renderWidth,
            int renderHeight,
            string key,
            PacketOwnedRewardRouletteLayerRole layerRole)
        {
            return new PacketOwnedUiRegistration(
                PacketOwnedUiAnchorMode.WindowTopLeft,
                layerRole == PacketOwnedRewardRouletteLayerRole.Job
                    ? 0
                    : ScalePacketOwnedUiOffset(PacketOwnedRewardRouletteOffsetX, renderWidth, PacketOwnedUiReferenceWidth),
                ScalePacketOwnedUiOffset(PacketOwnedRewardRouletteOffsetY, renderHeight, PacketOwnedUiReferenceHeight),
                key,
                layerRole switch
                {
                    PacketOwnedRewardRouletteLayerRole.Job => PacketOwnedUiDrawOrder.RewardRouletteJob,
                    PacketOwnedRewardRouletteLayerRole.Part => PacketOwnedUiDrawOrder.RewardRoulettePart,
                    _ => PacketOwnedUiDrawOrder.RewardRouletteLevel
                },
                PacketOwnedUiClientAlpha,
                LayerOrder: 0,
                Repeat: false);
        }

        private static int ScalePacketOwnedUiOffset(int referenceOffset, int actualSize, int referenceSize)
        {
            if (referenceSize <= 0)
            {
                return referenceOffset;
            }

            return (int)Math.Round(referenceOffset * (actualSize / (double)referenceSize), MidpointRounding.AwayFromZero);
        }

        private static PacketOwnedFieldClockLayout ResolvePacketOwnedFieldClockLayout(
            PacketFieldClockVisualVariant variant,
            int renderWidth,
            int renderHeight)
        {
            return variant switch
            {
                PacketFieldClockVisualVariant.Event => new PacketOwnedFieldClockLayout(
                    PacketOwnedUiAnchorMode.WindowTopLeft,
                    ScalePacketOwnedUiOffset(PacketOwnedFieldClockEventOffsetX, renderWidth, PacketOwnedFieldClockReferenceWidth),
                    ScalePacketOwnedUiOffset(PacketOwnedFieldClockEventOffsetY, renderHeight, PacketOwnedFieldClockReferenceHeight),
                    ScalePacketOwnedUiOffset(PacketOwnedFieldClockEventWidth, renderWidth, PacketOwnedFieldClockReferenceWidth),
                    ScalePacketOwnedUiOffset(PacketOwnedFieldClockEventHeight, renderHeight, PacketOwnedFieldClockReferenceHeight),
                    DrawSolidWindow: true,
                    PacketOwnedFieldClockEventBackColor,
                    PacketOwnedFieldClockEventTextColor),
                PacketFieldClockVisualVariant.CakePieSmall => new PacketOwnedFieldClockLayout(
                    PacketOwnedUiAnchorMode.WindowTopLeft,
                    ScalePacketOwnedUiOffset(PacketOwnedFieldClockCakePieSmallOffsetX, renderWidth, PacketOwnedFieldClockReferenceWidth),
                    ScalePacketOwnedUiOffset(PacketOwnedFieldClockCakePieSmallOffsetY, renderHeight, PacketOwnedFieldClockReferenceHeight),
                    ScalePacketOwnedUiOffset(PacketOwnedFieldClockCakePieSmallWidth, renderWidth, PacketOwnedFieldClockReferenceWidth),
                    ScalePacketOwnedUiOffset(PacketOwnedFieldClockCakePieSmallHeight, renderHeight, PacketOwnedFieldClockReferenceHeight),
                    DrawSolidWindow: false,
                    Color.Transparent,
                    PacketOwnedFieldClockDefaultTextColor),
                PacketFieldClockVisualVariant.CakePieLarge => new PacketOwnedFieldClockLayout(
                    PacketOwnedUiAnchorMode.WindowTopLeft,
                    ScalePacketOwnedUiOffset(PacketOwnedFieldClockCakePieLargeOffsetX, renderWidth, PacketOwnedFieldClockReferenceWidth),
                    ScalePacketOwnedUiOffset(PacketOwnedFieldClockCakePieLargeOffsetY, renderHeight, PacketOwnedFieldClockReferenceHeight),
                    ScalePacketOwnedUiOffset(PacketOwnedFieldClockCakePieLargeWidth, renderWidth, PacketOwnedFieldClockReferenceWidth),
                    ScalePacketOwnedUiOffset(PacketOwnedFieldClockCakePieLargeHeight, renderHeight, PacketOwnedFieldClockReferenceHeight),
                    DrawSolidWindow: false,
                    Color.Transparent,
                    PacketOwnedFieldClockDefaultTextColor),
                _ => new PacketOwnedFieldClockLayout(
                    PacketOwnedUiAnchorMode.WindowCenter,
                    0,
                    ScalePacketOwnedUiOffset(PacketOwnedBossTimerOffsetY, renderHeight, PacketOwnedFieldClockReferenceHeight),
                    ScalePacketOwnedUiOffset(PacketOwnedFieldClockDefaultWidth, renderWidth, PacketOwnedFieldClockReferenceWidth),
                    ScalePacketOwnedUiOffset(PacketOwnedFieldClockDefaultHeight, renderHeight, PacketOwnedFieldClockReferenceHeight),
                    DrawSolidWindow: false,
                    Color.Transparent,
                    PacketOwnedFieldClockDefaultTextColor)
            };
        }

        private static Rectangle ResolvePacketOwnedFieldClockBounds(
            PacketOwnedFieldClockLayout layout,
            int backgroundWidth,
            int backgroundHeight,
            int renderWidth,
            int renderHeight)
        {
            int width = Math.Max(1, layout.Width > 0 ? layout.Width : backgroundWidth);
            int height = Math.Max(1, layout.Height > 0 ? layout.Height : backgroundHeight);
            int x = layout.AnchorMode switch
            {
                PacketOwnedUiAnchorMode.WindowCenter => Math.Max(0, ((renderWidth - width) / 2) + layout.OffsetX),
                _ => Math.Max(0, layout.OffsetX)
            };
            int y = layout.AnchorMode switch
            {
                PacketOwnedUiAnchorMode.WindowCenter => Math.Max(0, layout.OffsetY),
                _ => Math.Max(0, layout.OffsetY)
            };
            return new Rectangle(x, y, width, height);
        }

        private static Vector2 ResolvePacketOwnedFieldClockBoardPosition(Rectangle layoutBounds, Texture2D background)
        {
            if (background == null)
            {
                return new Vector2(layoutBounds.X, layoutBounds.Y);
            }

            return new Vector2(
                layoutBounds.X + Math.Max(0f, (layoutBounds.Width - background.Width) / 2f),
                layoutBounds.Y + Math.Max(0f, (layoutBounds.Height - background.Height) / 2f));
        }

        internal static (PacketOwnedUiAnchorMode AnchorMode, int OffsetX, int OffsetY, int Width, int Height, uint BackColorArgb, uint TextColorArgb, bool DrawSolidWindow) GetPacketOwnedFieldClockLayoutForTest(
            PacketFieldClockVisualVariant variant,
            int renderWidth,
            int renderHeight)
        {
            PacketOwnedFieldClockLayout layout = ResolvePacketOwnedFieldClockLayout(variant, renderWidth, renderHeight);
            return (
                layout.AnchorMode,
                layout.OffsetX,
                layout.OffsetY,
                layout.Width,
                layout.Height,
                layout.BackColor.PackedValue,
                layout.FallbackTextColor.PackedValue,
                layout.DrawSolidWindow);
        }

        internal static (PacketOwnedUiAnchorMode AnchorMode, int OffsetX, int OffsetY, PacketOwnedUiDrawOrder DrawOrder, byte Alpha) GetPacketOwnedScreenEffectRegistrationForTest(
            int renderWidth,
            int renderHeight)
        {
            PacketOwnedUiRegistration registration = ResolvePacketOwnedScreenEffectRegistration(
                renderWidth,
                renderHeight,
                "screen:test");
            return (registration.AnchorMode, registration.OffsetX, registration.OffsetY, registration.DrawOrder, registration.Alpha);
        }

        internal static string GetPacketOwnedScreenEffectAnimationKey(string descriptor)
        {
            if (string.IsNullOrWhiteSpace(descriptor))
            {
                return "screen:";
            }

            string normalized = descriptor
                .Replace('\\', '/')
                .Trim()
                .Trim('/');
            return $"screen:{normalized}";
        }

        internal static bool GetPacketOwnedScreenEffectRegistrationRepeatForTest(bool repeat)
        {
            return ResolvePacketOwnedScreenEffectRegistration(
                PacketOwnedUiReferenceWidth,
                PacketOwnedUiReferenceHeight,
                "screen:test",
                repeat: repeat).Repeat;
        }

        internal static int GetPacketOwnedUiLayerOrderForTest(int? zHint, int discoveryOrder)
        {
            return ComposePacketOwnedUiLayerOrder(zHint, discoveryOrder);
        }

        internal static (PacketOwnedUiAnchorMode AnchorMode, int OffsetX, int OffsetY, PacketOwnedUiDrawOrder DrawOrder, byte Alpha) GetPacketOwnedRewardRouletteRegistrationForTest(
            int renderWidth,
            int renderHeight)
        {
            PacketOwnedUiRegistration registration = ResolvePacketOwnedRewardRouletteRegistration(
                renderWidth,
                renderHeight,
                "reward-roulette",
                PacketOwnedRewardRouletteLayerRole.Part);
            return (registration.AnchorMode, registration.OffsetX, registration.OffsetY, registration.DrawOrder, registration.Alpha);
        }

        internal static (PacketOwnedUiAnchorMode AnchorMode, int OffsetX, int OffsetY, PacketOwnedUiDrawOrder DrawOrder, byte Alpha) GetPacketOwnedRewardRouletteRegistrationForTest(
            int renderWidth,
            int renderHeight,
            int layerIndex)
        {
            PacketOwnedRewardRouletteLayerRole layerRole = layerIndex switch
            {
                0 => PacketOwnedRewardRouletteLayerRole.Job,
                1 => PacketOwnedRewardRouletteLayerRole.Part,
                2 => PacketOwnedRewardRouletteLayerRole.Level,
                _ => throw new ArgumentOutOfRangeException(nameof(layerIndex))
            };
            PacketOwnedUiRegistration registration = ResolvePacketOwnedRewardRouletteRegistration(
                renderWidth,
                renderHeight,
                "reward-roulette",
                layerRole);
            return (registration.AnchorMode, registration.OffsetX, registration.OffsetY, registration.DrawOrder, registration.Alpha);
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
                "clock" => HandlePacketOwnedFieldFeedbackClockCommand(args),
                "destroyclock" => ApplyPacketOwnedFieldFeedbackHelper(PacketFieldFeedbackPacketKind.DestroyClock, Array.Empty<byte>()),
                "zakumtimer" => HandlePacketOwnedFieldFeedbackBossTimerCommand(args, PacketFieldFeedbackPacketKind.ZakumTimer),
                "hontailtimer" => HandlePacketOwnedFieldFeedbackBossTimerCommand(args, PacketFieldFeedbackPacketKind.HontailTimer),
                "chaoszakumtimer" => HandlePacketOwnedFieldFeedbackBossTimerCommand(args, PacketFieldFeedbackPacketKind.ChaosZakumTimer),
                "hontaletimer" => HandlePacketOwnedFieldFeedbackBossTimerCommand(args, PacketFieldFeedbackPacketKind.HontaleTimer),
                "fadeoutforce" => HandlePacketOwnedFieldFeedbackFadeOutForceCommand(args),
                _ => ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback [status|clear|group <family> <sender> <text>|whisperin <sender> <channel> <text>|whisperresult <target> <ok|fail>|whisperavailability <target> <0|1>|whisperfind <find|findreply> <target> <result> <value> [x y]|couplechat <sender> <text>|couplenotice [text]|warn <text>|obstacle <tag> <state>|obstaclereset|bosshp <mobId> <currentHp> <maxHp> [color] [phase]|tremble <force> <durationMs>|fieldsound <descriptor>|fieldbgm <descriptor>|jukebox <itemId> <owner>|transferfieldignored <reason>|transferchannelignored <reason>|summonunavailable [0|1]|clock <realtime|countdown|event|cakepie> ...|destroyclock|zakumtimer <mode> <value>|hontailtimer <mode> <value>|chaoszakumtimer <mode> <value>|hontaletimer <mode> <value>|fadeoutforce [key]|packet <kind> [payloadhex=..|payloadb64=..]|packetraw <kind> <hex>]"),
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

        internal enum PacketOwnedUiAnchorMode
        {
            WindowTopLeft,
            WindowCenter
        }

        internal enum PacketOwnedUiDrawOrder
        {
            ScreenEffect = 0,
            RewardRouletteJob = 1,
            RewardRoulettePart = 2,
            RewardRouletteLevel = 3
        }

        private readonly record struct PacketOwnedUiRegistration(
            PacketOwnedUiAnchorMode AnchorMode,
            int OffsetX,
            int OffsetY,
            string Key,
            PacketOwnedUiDrawOrder DrawOrder,
            byte Alpha,
            int LayerOrder,
            bool Repeat);

        private readonly record struct PacketOwnedFieldClockLayout(
            PacketOwnedUiAnchorMode AnchorMode,
            int OffsetX,
            int OffsetY,
            int Width,
            int Height,
            bool DrawSolidWindow,
            Color BackColor,
            Color FallbackTextColor);

        private readonly record struct PacketOwnedUiAlphaRange(
            byte StartAlpha,
            byte EndAlpha);

        private readonly record struct PacketOwnedUiFrame(
            IDXObject Sprite,
            byte StartAlpha,
            byte EndAlpha);

        private readonly record struct PacketOwnedUiFrameState(
            PacketOwnedUiFrame Frame,
            byte FrameAlpha);

        private readonly record struct PacketOwnedCachedUiLayer(
            IReadOnlyList<PacketOwnedUiFrame> Frames,
            int LayerOrder,
            bool Repeat);

        private enum PacketOwnedRewardRouletteLayerRole
        {
            Job = 0,
            Part = 1,
            Level = 2
        }

        private readonly record struct PacketOwnedRewardRouletteLayerRequest(
            PacketOwnedRewardRouletteLayerRole LayerRole,
            int StringPoolId,
            int RequestedIndex,
            string FallbackFormat);

        private readonly record struct PacketOwnedRewardRouletteLayerCandidate(
            PacketOwnedRewardRouletteLayerRole LayerRole,
            string CategoryName,
            string ImageName,
            string PropertyPath);

        private sealed class PacketOwnedUiAnimation
        {
            private readonly IReadOnlyList<PacketOwnedUiFrame> _frames;
            private readonly int _durationMs;
            private readonly PacketOwnedUiRegistration _registration;

            public PacketOwnedUiAnimation(IReadOnlyList<PacketOwnedUiFrame> frames, PacketOwnedUiRegistration registration, int startedAtTick)
            {
                _frames = frames ?? Array.Empty<PacketOwnedUiFrame>();
                _registration = registration;
                StartedAtTick = startedAtTick;
                _durationMs = _frames.Sum(static frame => Math.Max(1, frame.Sprite?.Delay ?? 1));
            }

            public int StartedAtTick { get; }
            public string Key => _registration.Key ?? string.Empty;
            public PacketOwnedUiDrawOrder DrawOrder => _registration.DrawOrder;
            public int LayerOrder => _registration.LayerOrder;

            public bool IsComplete(int currentTickCount)
            {
                return _frames.Count == 0
                    || (!_registration.Repeat && currentTickCount - StartedAtTick >= _durationMs);
            }

            public PacketOwnedUiFrameState ResolveFrameState(int currentTickCount)
            {
                if (_frames.Count == 0)
                {
                    return default;
                }

                int elapsed = Math.Max(0, currentTickCount - StartedAtTick);
                if (_registration.Repeat && _durationMs > 0)
                {
                    elapsed %= _durationMs;
                }

                int cursor = 0;
                foreach (PacketOwnedUiFrame frame in _frames)
                {
                    int frameDelay = Math.Max(1, frame.Sprite?.Delay ?? 1);
                    int nextCursor = cursor + frameDelay;
                    if (elapsed < nextCursor)
                    {
                        return new PacketOwnedUiFrameState(
                            frame,
                            ResolvePacketOwnedUiFrameAlpha(
                                elapsed - cursor,
                                frameDelay,
                                _registration.Alpha,
                                frame.StartAlpha,
                                frame.EndAlpha));
                    }

                    cursor = nextCursor;
                }

                PacketOwnedUiFrame lastFrame = _frames[^1];
                int lastFrameDelay = Math.Max(1, lastFrame.Sprite?.Delay ?? 1);
                return new PacketOwnedUiFrameState(
                    lastFrame,
                    ResolvePacketOwnedUiFrameAlpha(
                        lastFrameDelay,
                        lastFrameDelay,
                        _registration.Alpha,
                        lastFrame.StartAlpha,
                        lastFrame.EndAlpha));
            }

            public Vector2 ResolveDrawPosition(IDXObject frame, int renderWidth, int renderHeight)
            {
                int anchorX = _registration.AnchorMode switch
                {
                    PacketOwnedUiAnchorMode.WindowCenter => renderWidth / 2,
                    _ => 0
                };
                int anchorY = _registration.AnchorMode switch
                {
                    PacketOwnedUiAnchorMode.WindowCenter => renderHeight / 2,
                    _ => 0
                };

                return new Vector2(
                    anchorX + _registration.OffsetX - (frame?.X ?? 0),
                    anchorY + _registration.OffsetY - (frame?.Y ?? 0));
            }

            public Color ResolveTint(byte frameAlpha)
            {
                return new Color(byte.MaxValue, byte.MaxValue, byte.MaxValue, frameAlpha);
            }
        }

        private static bool IsPacketOwnedAnimationMetadataProperty(WzImageProperty property)
        {
            if (property == null)
            {
                return true;
            }

            if (int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                return false;
            }

            return property.Name switch
            {
                "origin" => true,
                "delay" => true,
                "z" => true,
                "a0" => true,
                "a1" => true,
                "alpha" => true,
                "blend" => true,
                "repeat" => true,
                _ => false
            };
        }

        private static int? ResolvePacketOwnedAnimationLayerZ(WzImageProperty sourceProperty)
        {
            sourceProperty = sourceProperty?.GetLinkedWzImageProperty() ?? sourceProperty;
            if (sourceProperty == null)
            {
                return null;
            }

            int? directZ = sourceProperty["z"]?.GetInt();
            if (directZ.HasValue)
            {
                return directZ.Value;
            }

            if (sourceProperty is WzCanvasProperty)
            {
                return null;
            }

            for (int i = 0; ; i++)
            {
                if (sourceProperty[i.ToString(CultureInfo.InvariantCulture)] is not WzCanvasProperty frameCanvas)
                {
                    break;
                }

                int? frameZ = frameCanvas["z"]?.GetInt();
                if (frameZ.HasValue)
                {
                    return frameZ.Value;
                }
            }

            return null;
        }

        private static bool ResolvePacketOwnedAnimationRepeat(WzImageProperty sourceProperty)
        {
            sourceProperty = sourceProperty?.GetLinkedWzImageProperty() ?? sourceProperty;
            return sourceProperty?["repeat"]?.GetInt() > 0;
        }

        private static int ComposePacketOwnedUiLayerOrder(int? zHint, int discoveryOrder)
        {
            const int ZBias = 2048;
            const int DiscoveryBucketSize = 4096;
            int normalizedZ = Math.Clamp(zHint.GetValueOrDefault(), -ZBias, ZBias - 1) + ZBias;
            int normalizedDiscoveryOrder = Math.Clamp(discoveryOrder, 0, DiscoveryBucketSize - 1);
            return checked((normalizedZ * DiscoveryBucketSize) + normalizedDiscoveryOrder);
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

            if (kind == PacketFieldFeedbackPacketKind.HontaleTimer)
            {
                if (value < byte.MinValue || value > byte.MaxValue)
                {
                    return ChatCommandHandler.CommandResult.Error("Hontale timer value must be between 0 and 255.");
                }

                return ApplyPacketOwnedFieldFeedbackHelper(kind, PacketFieldFeedbackRuntime.BuildHontaleTimerPayload(mode, (byte)value));
            }

            return ApplyPacketOwnedFieldFeedbackHelper(kind, PacketFieldFeedbackRuntime.BuildBossTimerPayload(mode, value));
        }

        private ChatCommandHandler.CommandResult HandlePacketOwnedFieldFeedbackClockCommand(string[] args)
        {
            if (args.Length < 2)
            {
                return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback clock <realtime|countdown|event|cakepie> ...");
            }

            string mode = args[1].Trim().ToLowerInvariant();
            switch (mode)
            {
                case "realtime":
                    if (args.Length < 5
                        || !byte.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte hour)
                        || !byte.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte minute)
                        || !byte.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte second))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback clock realtime <hour> <minute> <second>");
                    }

                    return ApplyPacketOwnedFieldFeedbackHelper(
                        PacketFieldFeedbackPacketKind.Clock,
                        PacketFieldFeedbackRuntime.BuildClockRealtimePayload(hour, minute, second));
                case "countdown":
                    if (args.Length < 3 || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int durationSeconds))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback clock countdown <seconds>");
                    }

                    return ApplyPacketOwnedFieldFeedbackHelper(
                        PacketFieldFeedbackPacketKind.Clock,
                        PacketFieldFeedbackRuntime.BuildClockCountdownPayload(durationSeconds));
                case "event":
                    if (args.Length < 3)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback clock event <on|off> [seconds]");
                    }

                    bool showEvent = args[2].Equals("on", StringComparison.OrdinalIgnoreCase)
                        || args[2].Equals("1", StringComparison.OrdinalIgnoreCase)
                        || args[2].Equals("true", StringComparison.OrdinalIgnoreCase);
                    int eventDurationSeconds = 0;
                    if (showEvent
                        && (args.Length < 4 || !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out eventDurationSeconds)))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback clock event <on|off> [seconds]");
                    }

                    return ApplyPacketOwnedFieldFeedbackHelper(
                        PacketFieldFeedbackPacketKind.Clock,
                        PacketFieldFeedbackRuntime.BuildClockEventCountdownPayload(showEvent, eventDurationSeconds));
                case "cakepie":
                    if (args.Length < 4)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback clock cakepie <small|large> <on|off> [seconds]");
                    }

                    byte boardType = args[2].Equals("small", StringComparison.OrdinalIgnoreCase) ? (byte)0 : (byte)1;
                    bool showCakePie = args[3].Equals("on", StringComparison.OrdinalIgnoreCase)
                        || args[3].Equals("1", StringComparison.OrdinalIgnoreCase)
                        || args[3].Equals("true", StringComparison.OrdinalIgnoreCase);
                    int cakePieDurationSeconds = 0;
                    if (showCakePie
                        && (args.Length < 5 || !int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out cakePieDurationSeconds)))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback clock cakepie <small|large> <on|off> [seconds]");
                    }

                    return ApplyPacketOwnedFieldFeedbackHelper(
                        PacketFieldFeedbackPacketKind.Clock,
                        PacketFieldFeedbackRuntime.BuildClockCakePiePayload(showCakePie, boardType, cakePieDurationSeconds));
                default:
                    return ChatCommandHandler.CommandResult.Error("Usage: /fieldfeedback clock <realtime|countdown|event|cakepie> ...");
            }
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
                "clock" => Assign(PacketFieldFeedbackPacketKind.Clock, out kind),
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
