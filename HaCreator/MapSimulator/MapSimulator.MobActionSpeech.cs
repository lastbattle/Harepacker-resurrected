using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Companions;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Entities;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int MobActionSpeechHorizontalPadding = 18;
        private const int MobActionSpeechVerticalPadding = 12;
        private const int MobActionSpeechOwnerMaxTextWidth = 220;
        private const int MobActionSpeechScreenMaxTextWidth = 360;
        private const int MobActionSpeechNativeScreenCenterY = 100;
        private const int MobActionSpeechNativeScreenWidth = 800;
        private const int MobActionSpeechNativeScreenBalloonType = 1005;
        private const int MobActionSpeechNativeOwnerBalloonType = 1004;
        private const int MobActionSpeechNativeScreenLayerOption = unchecked((int)0xC00616FC);
        private const int MobActionSpeechNativeScreenLayerPriority = -1;
        private const int MobActionSpeechNativeScreenCanvasInsertAlpha = 255;
        private const int MobActionSpeechNativeScreenCanvasInsertX = 0;
        private const int MobActionSpeechNativeScreenCanvasInsertY = 0;

        private readonly Dictionary<int, LocalOverlayBalloonSkin> _mobActionSpeechBalloonSkins = new();
        private bool _mobActionSpeechBalloonSkinsLoaded;

        internal sealed class MobActionSpeechTextLayout
        {
            public IReadOnlyList<string> Lines { get; init; }
            public Vector2 TextSize { get; init; }
        }

        private void ConfigureMobActionSpeechConditionContext(MobItem mob)
        {
            mob?.SetActionSpeakConditionContextProvider(BuildLiveMobActionSpeechConditionContext);
        }

        private MobAnimationSet.ActionSpeakConditionContext BuildLiveMobActionSpeechConditionContext()
        {
            return new MobAnimationSet.ActionSpeakConditionContext
            {
                QuestStateProvider = questId => _questRuntime == null ? null : (int?)_questRuntime.GetCurrentState(questId),
                HasPetItem = HasMobActionSpeechConditionPetItem
            };
        }

        private bool HasMobActionSpeechConditionPetItem(int petItemId)
        {
            if (petItemId <= 0)
            {
                return false;
            }

            IReadOnlyList<PetRuntime> activePets = _playerManager?.Pets?.ActivePets;
            if (activePets == null || activePets.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < activePets.Count; i++)
            {
                PetRuntime pet = activePets[i];
                if (pet != null && (pet.ItemId == petItemId || pet.PetWearItemId == petItemId))
                {
                    return true;
                }
            }

            return false;
        }

        private void DrawMobActionSpeechFeedback(in Managers.RenderContext renderContext)
        {
            if (_fontChat == null ||
                _debugBoundaryTexture == null ||
                _visibleMobs == null ||
                _visibleMobsCount <= 0)
            {
                return;
            }

            for (int i = 0; i < _visibleMobsCount; i++)
            {
                MobItem mob = _visibleMobs[i];
                if (mob?.IsActionSpeechActive(renderContext.TickCount) == true)
                {
                    DrawMobActionSpeechBalloon(mob, renderContext);
                }
            }
        }

        private void DrawMobActionSpeechBalloon(MobItem mob, in Managers.RenderContext renderContext)
        {
            if (mob == null ||
                !mob.HasActiveActionSpeech ||
                _fontChat == null ||
                _debugBoundaryTexture == null)
            {
                return;
            }

            LocalOverlayBalloonSkin skin = IsMobActionSpeechFloatNotice(mob.ActiveActionSpeechFloatNotice)
                ? null
                : ResolveMobActionSpeechBalloonSkin(mob.ActiveActionSpeechChatBalloon);
            bool isScreenNotice = ResolveMobActionSpeechScreenNotice(
                mob.ActiveActionSpeechChatBalloon,
                mob.ActiveActionSpeechFloatNotice);
            MobActionSpeechTextLayout textLayout = BuildMobActionSpeechTextLayout(
                mob.ActiveActionSpeechText,
                mob.ActiveActionSpeechChatBalloon,
                mob.ActiveActionSpeechFloatNotice,
                isScreenNotice,
                renderContext.RenderParams.RenderWidth,
                MeasureChatTextWithFallback);
            Rectangle bounds = ResolveMobActionSpeechBounds(
                textLayout.TextSize,
                mob.CurrentX,
                mob.CurrentY - mob.GetVisualHeight(60),
                mob.ActiveActionSpeechChatBalloon,
                mob.ActiveActionSpeechFloatNotice,
                isScreenNotice,
                renderContext.MapShiftX,
                renderContext.MapShiftY,
                renderContext.MapCenterX,
                renderContext.MapCenterY,
                renderContext.RenderParams.RenderWidth,
                renderContext.RenderParams.RenderHeight,
                ResolveMobActionSpeechSkinMetrics(skin));

            if (bounds.Right < 0 ||
                bounds.Bottom < 0 ||
                bounds.Left > renderContext.RenderParams.RenderWidth ||
                bounds.Top > renderContext.RenderParams.RenderHeight)
            {
                return;
            }

            int fadeDurationMs = Math.Max(1, mob.ActiveActionSpeechFadeDurationMs);
            float remainingAlpha = MathHelper.Clamp((mob.ActiveActionSpeechExpiresAt - renderContext.TickCount) / (float)fadeDurationMs, 0f, 1f);
            ResolveMobActionSpeechColors(
                mob.ActiveActionSpeechChatBalloon,
                mob.ActiveActionSpeechFloatNotice,
                isScreenNotice,
                remainingAlpha,
                out Color backgroundColor,
                out Color borderColor,
                out Color textColor);

            if (skin?.IsLoaded == true && !IsMobActionSpeechFloatNotice(mob.ActiveActionSpeechFloatNotice))
            {
                textColor = skin.TextColor * MathHelper.Clamp(remainingAlpha, 0f, 1f);
            }

            bool drewAuthoredSkin = DrawMobActionSpeechBalloonSkin(skin, bounds, remainingAlpha, ShouldDrawMobActionSpeechArrow(isScreenNotice));
            if (!drewAuthoredSkin)
            {
                DrawMobActionSpeechFallbackFrame(
                    bounds,
                    backgroundColor,
                    borderColor,
                    !isScreenNotice);
            }

            DrawMobActionSpeechText(textLayout, bounds, textColor, ResolveMobActionSpeechSkinMetrics(skin));
        }

        private LocalOverlayBalloonSkin ResolveMobActionSpeechBalloonSkin(int chatBalloon)
        {
            EnsureMobActionSpeechBalloonSkinsLoaded();
            int normalizedChatBalloon = Math.Max(0, chatBalloon);
            if (_mobActionSpeechBalloonSkins.TryGetValue(normalizedChatBalloon, out LocalOverlayBalloonSkin skin) &&
                skin?.IsLoaded == true)
            {
                return skin;
            }

            return normalizedChatBalloon != 0 &&
                   _mobActionSpeechBalloonSkins.TryGetValue(0, out LocalOverlayBalloonSkin fallback) &&
                   fallback?.IsLoaded == true
                ? fallback
                : null;
        }

        private void EnsureMobActionSpeechBalloonSkinsLoaded()
        {
            if (_mobActionSpeechBalloonSkinsLoaded)
            {
                return;
            }

            _mobActionSpeechBalloonSkinsLoaded = true;
            WzImage chatBalloonImage = Program.FindImage("UI", "ChatBalloon.img");
            if (chatBalloonImage == null)
            {
                return;
            }

            chatBalloonImage.ParseImage();
            if (chatBalloonImage["mob"] is not WzSubProperty mobBalloonRoot)
            {
                return;
            }

            foreach (WzImageProperty child in mobBalloonRoot.WzProperties)
            {
                if (!int.TryParse(child?.Name, out int chatBalloonId) ||
                    child is not WzSubProperty source)
                {
                    continue;
                }

                LocalOverlayBalloonSkin skin = LoadMobActionSpeechBalloonSkin(source);
                if (skin?.IsLoaded == true)
                {
                    _mobActionSpeechBalloonSkins[chatBalloonId] = skin;
                }
            }
        }

        private LocalOverlayBalloonSkin LoadMobActionSpeechBalloonSkin(WzSubProperty source)
        {
            if (source == null)
            {
                return null;
            }

            return new LocalOverlayBalloonSkin
            {
                NorthWest = LoadUiCanvasTexture(source["nw"] as WzCanvasProperty),
                NorthEast = LoadUiCanvasTexture(source["ne"] as WzCanvasProperty),
                SouthWest = LoadUiCanvasTexture(source["sw"] as WzCanvasProperty),
                SouthEast = LoadUiCanvasTexture(source["se"] as WzCanvasProperty),
                North = LoadUiCanvasTexture(source["n"] as WzCanvasProperty),
                South = LoadUiCanvasTexture(source["s"] as WzCanvasProperty),
                West = LoadUiCanvasTexture(source["w"] as WzCanvasProperty),
                East = LoadUiCanvasTexture(source["e"] as WzCanvasProperty),
                Center = LoadUiCanvasTexture(source["c"] as WzCanvasProperty),
                Arrow = LoadUiArrowSprite(source["arrow"] as WzCanvasProperty),
                TextColor = ResolvePacketOwnedBalloonTextColor(source["clr"] as WzImageProperty),
                IsScreenChat = IsMobActionSpeechScreenChatSource(source)
            };
        }

        private bool DrawMobActionSpeechBalloonSkin(LocalOverlayBalloonSkin skin, Rectangle bounds, float alpha, bool includeArrow)
        {
            if (skin?.IsLoaded != true)
            {
                return false;
            }

            Color tint = Color.White * MathHelper.Clamp(alpha, 0f, 1f);
            DrawMobActionSpeechNineSlice(skin, bounds, tint);

            LocalOverlayBalloonArrowSprite arrow = skin.Arrow;
            if (includeArrow && arrow?.IsLoaded == true)
            {
                int arrowX = bounds.Left + (bounds.Width / 2) - arrow.Origin.X;
                int arrowY = bounds.Bottom - arrow.Origin.Y;
                _spriteBatch.Draw(arrow.Texture, new Vector2(arrowX, arrowY), tint);
            }

            return true;
        }

        private void DrawMobActionSpeechNineSlice(LocalOverlayBalloonSkin skin, Rectangle bounds, Color tint)
        {
            Texture2D northWest = skin.NorthWest;
            Texture2D northEast = skin.NorthEast;
            Texture2D southWest = skin.SouthWest;
            Texture2D southEast = skin.SouthEast;
            Texture2D north = skin.North;
            Texture2D south = skin.South;
            Texture2D west = skin.West;
            Texture2D east = skin.East;
            Texture2D center = skin.Center;

            int leftWidth = northWest.Width;
            int rightWidth = northEast.Width;
            int topHeight = northWest.Height;
            int bottomHeight = southWest.Height;
            int centerWidth = Math.Max(0, bounds.Width - leftWidth - rightWidth);
            int centerHeight = Math.Max(0, bounds.Height - topHeight - bottomHeight);

            _spriteBatch.Draw(center, new Rectangle(bounds.X + leftWidth, bounds.Y + topHeight, centerWidth, centerHeight), tint);
            _spriteBatch.Draw(northWest, new Vector2(bounds.X, bounds.Y), tint);
            _spriteBatch.Draw(northEast, new Vector2(bounds.Right - rightWidth, bounds.Y), tint);
            _spriteBatch.Draw(southWest, new Vector2(bounds.X, bounds.Bottom - bottomHeight), tint);
            _spriteBatch.Draw(southEast, new Vector2(bounds.Right - rightWidth, bounds.Bottom - bottomHeight), tint);

            if (centerWidth > 0)
            {
                _spriteBatch.Draw(north, new Rectangle(bounds.X + leftWidth, bounds.Y, centerWidth, north.Height), tint);
                _spriteBatch.Draw(south, new Rectangle(bounds.X + leftWidth, bounds.Bottom - south.Height, centerWidth, south.Height), tint);
            }

            if (centerHeight > 0)
            {
                _spriteBatch.Draw(west, new Rectangle(bounds.X, bounds.Y + topHeight, west.Width, centerHeight), tint);
                _spriteBatch.Draw(east, new Rectangle(bounds.Right - east.Width, bounds.Y + topHeight, east.Width, centerHeight), tint);
            }
        }

        private void DrawMobActionSpeechFallbackFrame(Rectangle bounds, Color backgroundColor, Color borderColor, bool includeArrow)
        {
            _spriteBatch.Draw(_debugBoundaryTexture, bounds, backgroundColor);
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(bounds.Left, bounds.Top, bounds.Width, 2), borderColor);
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(bounds.Left, bounds.Bottom - 2, bounds.Width, 2), borderColor);
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(bounds.Left, bounds.Top, 2, bounds.Height), borderColor);
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(bounds.Right - 2, bounds.Top, 2, bounds.Height), borderColor);

            if (!includeArrow)
            {
                return;
            }

            int arrowX = bounds.Left + (bounds.Width / 2) - 5;
            int arrowY = bounds.Bottom - 1;
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(arrowX, arrowY, 10, 4), borderColor);
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(arrowX + 2, arrowY + 4, 6, 3), borderColor);
            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(arrowX + 4, arrowY + 7, 2, 3), borderColor);
        }

        internal static Rectangle ResolveMobActionSpeechBounds(
            Vector2 textSize,
            int mobWorldX,
            int mobWorldTop,
            int chatBalloon,
            int floatNotice,
            int mapShiftX,
            int mapShiftY,
            int mapCenterX,
            int mapCenterY,
            int renderWidth,
            int renderHeight)
        {
            return ResolveMobActionSpeechBounds(
                textSize,
                mobWorldX,
                mobWorldTop,
                chatBalloon,
                floatNotice,
                IsMobActionSpeechScreenNotice(chatBalloon, floatNotice),
                mapShiftX,
                mapShiftY,
                mapCenterX,
                mapCenterY,
                renderWidth,
                renderHeight);
        }

        internal static Rectangle ResolveMobActionSpeechBounds(
            Vector2 textSize,
            int mobWorldX,
            int mobWorldTop,
            int chatBalloon,
            int floatNotice,
            bool isScreenNotice,
            int mapShiftX,
            int mapShiftY,
            int mapCenterX,
            int mapCenterY,
            int renderWidth,
            int renderHeight)
        {
            return ResolveMobActionSpeechBounds(
                textSize,
                mobWorldX,
                mobWorldTop,
                chatBalloon,
                floatNotice,
                isScreenNotice,
                mapShiftX,
                mapShiftY,
                mapCenterX,
                mapCenterY,
                renderWidth,
                renderHeight,
                null);
        }

        internal static Rectangle ResolveMobActionSpeechBounds(
            Vector2 textSize,
            int mobWorldX,
            int mobWorldTop,
            int chatBalloon,
            int floatNotice,
            bool isScreenNotice,
            int mapShiftX,
            int mapShiftY,
            int mapCenterX,
            int mapCenterY,
            int renderWidth,
            int renderHeight,
            MobActionSpeechSkinMetrics skinMetrics)
        {
            ResolveMobActionSpeechBoxSize(
                textSize,
                skinMetrics,
                out int boxWidth,
                out int boxHeight);

            if (isScreenNotice)
            {
                return ResolveMobActionSpeechScreenBounds(boxWidth, boxHeight, renderWidth, renderHeight);
            }

            int boxX = mobWorldX - mapShiftX + mapCenterX - (boxWidth / 2);
            int boxY = mobWorldTop - mapShiftY + mapCenterY - boxHeight - 24;
            return new Rectangle(boxX, boxY, boxWidth, boxHeight);
        }

        internal static Rectangle ResolveMobActionSpeechScreenBounds(
            int boxWidth,
            int boxHeight,
            int renderWidth,
            int renderHeight)
        {
            int safeWidth = Math.Max(0, renderWidth);
            int safeHeight = Math.Max(0, renderHeight);
            int x = Math.Max(0, (safeWidth - boxWidth) / 2);
            int nativeY = ResolveMobActionSpeechNativeScreenLayerY(boxHeight);
            int maxY = Math.Max(0, safeHeight - boxHeight);
            int y = Math.Max(0, Math.Min(maxY, nativeY));
            return new Rectangle(x, y, Math.Max(1, boxWidth), Math.Max(1, boxHeight));
        }

        internal sealed class MobActionSpeechNativeCompositionTrace
        {
            public string Entrypoint { get; init; }
            public int BalloonType { get; init; }
            public int ChatBalloon { get; init; }
            public string SkinPath { get; init; }
            public bool UsesScreenLayer { get; init; }
            public int ScreenLayerOption { get; init; }
            public int ScreenWidth { get; init; }
            public int ScreenLayerX { get; init; }
            public int ScreenLayerY { get; init; }
            public int ScreenCenterY { get; init; }
            public bool UsesOwnerOverlayLayer { get; init; }
            public bool IncludesOwnerArrow { get; init; }
            public bool UsesMobSkinFallback { get; init; }
            public int LayerPriority { get; init; }
            public int CanvasInsertX { get; init; }
            public int CanvasInsertY { get; init; }
            public int CanvasInsertAlpha { get; init; }
            public bool AssignsLayerChat { get; init; }
            public IReadOnlyList<string> NativeLifetimeOperations { get; init; }
        }

        internal static MobActionSpeechNativeCompositionTrace BuildMobActionSpeechNativeCompositionTraceForTests(
            int chatBalloon,
            int floatNotice,
            bool authoredSkinLoaded,
            bool isScreenNotice)
        {
            return BuildMobActionSpeechNativeCompositionTrace(
                chatBalloon,
                floatNotice,
                authoredSkinLoaded,
                isScreenNotice,
                1,
                1);
        }

        internal static MobActionSpeechNativeCompositionTrace BuildMobActionSpeechNativeCompositionTraceForTests(
            int chatBalloon,
            int floatNotice,
            bool authoredSkinLoaded,
            bool isScreenNotice,
            int nativeCanvasWidth,
            int nativeCanvasHeight)
        {
            return BuildMobActionSpeechNativeCompositionTrace(
                chatBalloon,
                floatNotice,
                authoredSkinLoaded,
                isScreenNotice,
                nativeCanvasWidth,
                nativeCanvasHeight);
        }

        private static MobActionSpeechNativeCompositionTrace BuildMobActionSpeechNativeCompositionTrace(
            int chatBalloon,
            int floatNotice,
            bool authoredSkinLoaded,
            bool isScreenNotice,
            int nativeCanvasWidth,
            int nativeCanvasHeight)
        {
            int normalizedChatBalloon = Math.Max(0, chatBalloon);
            bool usesFloatNotice = IsMobActionSpeechFloatNotice(floatNotice);
            bool useScreenLayer = isScreenNotice || usesFloatNotice;
            int safeNativeCanvasWidth = Math.Max(1, nativeCanvasWidth);
            int safeNativeCanvasHeight = Math.Max(1, nativeCanvasHeight);
            IReadOnlyList<string> lifetimeOperations = useScreenLayer
                ? new[]
                {
                    "AddRef(pProp)",
                    "AddRef(bsText)",
                    "CreateCanvas(type=1005)",
                    "CreateLayer(option=0xC00616FC)",
                    "Release(CreateLayer out-param layer)",
                    "SetLayerOrigin(Origin_LT)",
                    "Getcanvas(0)",
                    "InsertCanvas(0,0,alpha=255)",
                    "SetLayerPriority(-1)",
                    "AddRef(m_pLayerChat)",
                    "Release(previous m_pLayerChat)",
                    "Release(layer canvas)",
                    "Release(local layer)",
                    "Release(source canvas)",
                    "Release(pProp)",
                    "Release(bsText)"
                }
                : new[]
                {
                    "CreateCanvas(type=1004)",
                    "AttachOwnerOverlayLayer",
                    "StoreTimeout"
                };

            return new MobActionSpeechNativeCompositionTrace
            {
                Entrypoint = useScreenLayer
                    ? "CChatBalloon::MakeScreenBalloon"
                    : "CChatBalloon::MakeBalloon",
                BalloonType = useScreenLayer
                    ? MobActionSpeechNativeScreenBalloonType
                    : MobActionSpeechNativeOwnerBalloonType,
                ChatBalloon = normalizedChatBalloon,
                SkinPath = ResolveMobActionSpeechBalloonSkinPathForTests(normalizedChatBalloon),
                UsesScreenLayer = useScreenLayer,
                ScreenLayerOption = useScreenLayer ? MobActionSpeechNativeScreenLayerOption : 0,
                ScreenWidth = useScreenLayer ? MobActionSpeechNativeScreenWidth : 0,
                ScreenLayerX = useScreenLayer ? ResolveMobActionSpeechNativeScreenLayerX(safeNativeCanvasWidth) : 0,
                ScreenLayerY = useScreenLayer ? ResolveMobActionSpeechNativeScreenLayerY(safeNativeCanvasHeight) : 0,
                ScreenCenterY = useScreenLayer ? MobActionSpeechNativeScreenCenterY : 0,
                UsesOwnerOverlayLayer = !useScreenLayer,
                IncludesOwnerArrow = authoredSkinLoaded && !useScreenLayer,
                UsesMobSkinFallback = !authoredSkinLoaded && normalizedChatBalloon != 0,
                LayerPriority = useScreenLayer ? MobActionSpeechNativeScreenLayerPriority : 0,
                CanvasInsertX = useScreenLayer ? MobActionSpeechNativeScreenCanvasInsertX : 0,
                CanvasInsertY = useScreenLayer ? MobActionSpeechNativeScreenCanvasInsertY : 0,
                CanvasInsertAlpha = useScreenLayer ? MobActionSpeechNativeScreenCanvasInsertAlpha : 0,
                AssignsLayerChat = useScreenLayer,
                NativeLifetimeOperations = lifetimeOperations
            };
        }

        internal static int ResolveMobActionSpeechNativeScreenLayerX(int canvasWidth)
        {
            return (MobActionSpeechNativeScreenWidth - Math.Max(1, canvasWidth)) / 2;
        }

        internal static int ResolveMobActionSpeechNativeScreenLayerY(int canvasHeight)
        {
            return MobActionSpeechNativeScreenCenterY - (Math.Max(1, canvasHeight) / 2);
        }

        internal sealed class MobActionSpeechSkinMetrics
        {
            public int LeftTextInset { get; init; }
            public int RightTextInset { get; init; }
            public int TopTextInset { get; init; }
            public int BottomTextInset { get; init; }
            public int MinimumWidth { get; init; }
            public int MinimumHeight { get; init; }
        }

        internal static MobActionSpeechSkinMetrics BuildMobActionSpeechSkinMetricsForTests(
            int northWestWidth,
            int northEastWidth,
            int southWestHeight,
            int northWestHeight,
            int westWidth,
            int eastWidth,
            int centerWidth,
            int centerHeight)
        {
            return BuildMobActionSpeechSkinMetrics(
                northWestWidth,
                northEastWidth,
                southWestHeight,
                northWestHeight,
                westWidth,
                eastWidth,
                centerWidth,
                centerHeight);
        }

        private static MobActionSpeechSkinMetrics ResolveMobActionSpeechSkinMetrics(LocalOverlayBalloonSkin skin)
        {
            return skin?.IsLoaded == true
                ? BuildMobActionSpeechSkinMetrics(
                    skin.NorthWest.Width,
                    skin.NorthEast.Width,
                    skin.SouthWest.Height,
                    skin.NorthWest.Height,
                    skin.West.Width,
                    skin.East.Width,
                    skin.Center.Width,
                    skin.Center.Height)
                : null;
        }

        private static MobActionSpeechSkinMetrics BuildMobActionSpeechSkinMetrics(
            int northWestWidth,
            int northEastWidth,
            int southWestHeight,
            int northWestHeight,
            int westWidth,
            int eastWidth,
            int centerWidth,
            int centerHeight)
        {
            int leftInset = Math.Max(0, Math.Max(northWestWidth, westWidth)) + 3;
            int rightInset = Math.Max(0, Math.Max(northEastWidth, eastWidth)) + 3;
            int topInset = Math.Max(0, northWestHeight);
            int bottomInset = Math.Max(0, southWestHeight);

            return new MobActionSpeechSkinMetrics
            {
                LeftTextInset = leftInset,
                RightTextInset = rightInset,
                TopTextInset = topInset,
                BottomTextInset = bottomInset,
                MinimumWidth = Math.Max(0, northWestWidth) + Math.Max(0, centerWidth) + Math.Max(0, northEastWidth),
                MinimumHeight = Math.Max(0, northWestHeight) + Math.Max(0, centerHeight) + Math.Max(0, southWestHeight)
            };
        }

        private static void ResolveMobActionSpeechBoxSize(
            Vector2 textSize,
            MobActionSpeechSkinMetrics skinMetrics,
            out int boxWidth,
            out int boxHeight)
        {
            if (skinMetrics == null)
            {
                boxWidth = Math.Max(MobActionSpeechHorizontalPadding, (int)Math.Ceiling(textSize.X) + MobActionSpeechHorizontalPadding);
                boxHeight = Math.Max(20, (int)Math.Ceiling(textSize.Y) + MobActionSpeechVerticalPadding);
                return;
            }

            boxWidth = Math.Max(
                skinMetrics.MinimumWidth,
                (int)Math.Ceiling(textSize.X) + skinMetrics.LeftTextInset + skinMetrics.RightTextInset);
            boxHeight = Math.Max(
                skinMetrics.MinimumHeight,
                (int)Math.Ceiling(textSize.Y) + skinMetrics.TopTextInset + skinMetrics.BottomTextInset);
        }

        internal static MobActionSpeechTextLayout BuildMobActionSpeechTextLayout(
            string text,
            int chatBalloon,
            int floatNotice,
            int renderWidth,
            Func<string, Vector2> measureText)
        {
            return BuildMobActionSpeechTextLayout(
                text,
                chatBalloon,
                floatNotice,
                IsMobActionSpeechScreenNotice(chatBalloon, floatNotice),
                renderWidth,
                measureText);
        }

        internal static MobActionSpeechTextLayout BuildMobActionSpeechTextLayout(
            string text,
            int chatBalloon,
            int floatNotice,
            bool isScreenNotice,
            int renderWidth,
            Func<string, Vector2> measureText)
        {
            measureText ??= _ => Vector2.Zero;
            string normalizedText = NormalizeMobActionSpeechText(text);
            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                return new MobActionSpeechTextLayout
                {
                    Lines = Array.Empty<string>(),
                    TextSize = Vector2.Zero
                };
            }

            int maxTextWidth = ResolveMobActionSpeechMaxTextWidth(chatBalloon, floatNotice, isScreenNotice, renderWidth);
            List<string> lines = WrapMobActionSpeechText(normalizedText, maxTextWidth, measureText);
            if (lines.Count == 0)
            {
                lines.Add(normalizedText);
            }

            float maxLineWidth = 0f;
            float lineHeight = Math.Max(1f, measureText("Ay").Y);
            foreach (string line in lines)
            {
                Vector2 lineSize = measureText(line);
                maxLineWidth = Math.Max(maxLineWidth, lineSize.X);
                lineHeight = Math.Max(lineHeight, lineSize.Y);
            }

            return new MobActionSpeechTextLayout
            {
                Lines = lines,
                TextSize = new Vector2(maxLineWidth, lineHeight * lines.Count)
            };
        }

        private static int ResolveMobActionSpeechMaxTextWidth(int chatBalloon, int floatNotice, int renderWidth)
        {
            return ResolveMobActionSpeechMaxTextWidth(
                chatBalloon,
                floatNotice,
                IsMobActionSpeechScreenNotice(chatBalloon, floatNotice),
                renderWidth);
        }

        private static int ResolveMobActionSpeechMaxTextWidth(
            int chatBalloon,
            int floatNotice,
            bool isScreenNotice,
            int renderWidth)
        {
            int authoredMaxWidth = isScreenNotice
                ? MobActionSpeechScreenMaxTextWidth
                : MobActionSpeechOwnerMaxTextWidth;
            int viewportMaxWidth = renderWidth > 0
                ? Math.Max(48, renderWidth - (MobActionSpeechHorizontalPadding * 2))
                : authoredMaxWidth;
            return Math.Max(24, Math.Min(authoredMaxWidth, viewportMaxWidth));
        }

        private static List<string> WrapMobActionSpeechText(
            string text,
            int maxTextWidth,
            Func<string, Vector2> measureText)
        {
            var lines = new List<string>();
            foreach (string paragraph in text.Replace("\r\n", "\n").Split('\n'))
            {
                string normalizedParagraph = NormalizeMobActionSpeechParagraph(paragraph);
                if (string.IsNullOrWhiteSpace(normalizedParagraph))
                {
                    continue;
                }

                string currentLine = string.Empty;
                foreach (string word in normalizedParagraph.Split(' '))
                {
                    if (string.IsNullOrWhiteSpace(word))
                    {
                        continue;
                    }

                    string candidateLine = string.IsNullOrEmpty(currentLine)
                        ? word
                        : currentLine + " " + word;
                    if (measureText(candidateLine).X <= maxTextWidth)
                    {
                        currentLine = candidateLine;
                        continue;
                    }

                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        lines.Add(currentLine);
                        currentLine = string.Empty;
                    }

                    if (measureText(word).X <= maxTextWidth)
                    {
                        currentLine = word;
                        continue;
                    }

                    foreach (string segment in SplitMobActionSpeechLongWord(word, maxTextWidth, measureText))
                    {
                        if (measureText(segment).X <= maxTextWidth)
                        {
                            lines.Add(segment);
                        }
                        else
                        {
                            currentLine = segment;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    lines.Add(currentLine);
                }
            }

            return lines;
        }

        private static IEnumerable<string> SplitMobActionSpeechLongWord(
            string word,
            int maxTextWidth,
            Func<string, Vector2> measureText)
        {
            string segment = string.Empty;
            foreach (char character in word)
            {
                string candidate = segment + character;
                if (candidate.Length > 1 && measureText(candidate).X > maxTextWidth)
                {
                    yield return segment;
                    segment = character.ToString();
                    continue;
                }

                segment = candidate;
            }

            if (!string.IsNullOrEmpty(segment))
            {
                yield return segment;
            }
        }

        private static string NormalizeMobActionSpeechText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            string[] lines = text.Replace("\r\n", "\n")
                .Split('\n')
                .Select(NormalizeMobActionSpeechParagraph)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
            return lines.Length == 0 ? null : string.Join("\n", lines);
        }

        private static string NormalizeMobActionSpeechParagraph(string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? null
                : string.Join(" ", text.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        }

        private void DrawMobActionSpeechText(
            MobActionSpeechTextLayout layout,
            Rectangle bounds,
            Color textColor,
            MobActionSpeechSkinMetrics skinMetrics)
        {
            if (layout?.Lines == null || layout.Lines.Count == 0)
            {
                return;
            }

            float lineHeight = Math.Max(1f, MeasureChatTextWithFallback("Ay").Y);
            int textInsetX = skinMetrics?.LeftTextInset ?? 9;
            int textInsetY = skinMetrics?.TopTextInset ?? 6;
            int textAreaWidth = Math.Max(1, bounds.Width - textInsetX - (skinMetrics?.RightTextInset ?? 9));
            for (int i = 0; i < layout.Lines.Count; i++)
            {
                string line = layout.Lines[i];
                Vector2 lineSize = MeasureChatTextWithFallback(line);
                Vector2 position = ResolveMobActionSpeechLinePosition(
                    bounds,
                    lineSize,
                    textInsetX,
                    textInsetY,
                    textAreaWidth,
                    i,
                    lineHeight);
                DrawChatTextWithFallback(line, position, textColor);
            }
        }

        internal static Vector2 ResolveMobActionSpeechLinePosition(
            Rectangle bounds,
            Vector2 lineSize,
            int textInsetX,
            int textInsetY,
            int textAreaWidth,
            int lineIndex,
            float lineHeight)
        {
            int safeTextAreaWidth = Math.Max(1, textAreaWidth);
            float x = bounds.Left + textInsetX + Math.Max(0f, (safeTextAreaWidth - lineSize.X) / 2f);
            float y = bounds.Top + textInsetY + (Math.Max(0, lineIndex) * Math.Max(1f, lineHeight));
            return new Vector2(x, y);
        }

        internal static bool IsMobActionSpeechScreenChat(int chatBalloon)
        {
            // UI/ChatBalloon.img/mob/1 carries screenChat=1; other mob balloons stay anchored to the owner.
            return chatBalloon == 1;
        }

        private bool IsMobActionSpeechScreenChatBalloon(int chatBalloon)
        {
            EnsureMobActionSpeechBalloonSkinsLoaded();
            int normalizedChatBalloon = Math.Max(0, chatBalloon);
            return _mobActionSpeechBalloonSkins.TryGetValue(normalizedChatBalloon, out LocalOverlayBalloonSkin skin)
                ? skin?.IsScreenChat == true
                : IsMobActionSpeechScreenChat(normalizedChatBalloon);
        }

        internal static string ResolveMobActionSpeechBalloonSkinPathForTests(int chatBalloon)
        {
            return $"UI/ChatBalloon.img/mob/{Math.Max(0, chatBalloon)}";
        }

        internal static bool IsMobActionSpeechScreenChatSource(WzImageProperty source)
        {
            return (source?["screenChat"] as WzIntProperty)?.Value != 0;
        }

        internal static bool IsMobActionSpeechFloatNotice(int floatNotice)
        {
            return floatNotice > 0;
        }

        internal static bool IsMobActionSpeechScreenNotice(int chatBalloon, int floatNotice)
        {
            return IsMobActionSpeechScreenChat(chatBalloon) || IsMobActionSpeechFloatNotice(floatNotice);
        }

        internal static bool ShouldDrawMobActionSpeechArrowForTests(bool isScreenNotice)
        {
            return ShouldDrawMobActionSpeechArrow(isScreenNotice);
        }

        private static bool ShouldDrawMobActionSpeechArrow(bool isScreenNotice)
        {
            // CChatBalloon::MakeMobBalloon routes UI/ChatBalloon.img/mob/<id> skins
            // with screenChat=1 through MakeScreenBalloon instead of the owner-anchored
            // MakeBalloon(type 1004) path, so screen notices do not keep the owner arrow.
            return !isScreenNotice;
        }

        private bool ResolveMobActionSpeechScreenNotice(int chatBalloon, int floatNotice)
        {
            return IsMobActionSpeechScreenChatBalloon(chatBalloon) || IsMobActionSpeechFloatNotice(floatNotice);
        }

        private static void ResolveMobActionSpeechColors(
            int chatBalloon,
            int floatNotice,
            float alpha,
            out Color backgroundColor,
            out Color borderColor,
            out Color textColor)
        {
            ResolveMobActionSpeechColors(
                chatBalloon,
                floatNotice,
                IsMobActionSpeechScreenNotice(chatBalloon, floatNotice),
                alpha,
                out backgroundColor,
                out borderColor,
                out textColor);
        }

        private static void ResolveMobActionSpeechColors(
            int chatBalloon,
            int floatNotice,
            bool isScreenNotice,
            float alpha,
            out Color backgroundColor,
            out Color borderColor,
            out Color textColor)
        {
            float clampedAlpha = MathHelper.Clamp(alpha, 0f, 1f);
            if (IsMobActionSpeechFloatNotice(floatNotice))
            {
                backgroundColor = new Color(32, 28, 18) * (0.90f * clampedAlpha);
                borderColor = new Color(255, 214, 91) * clampedAlpha;
                textColor = new Color(255, 244, 198) * clampedAlpha;
                return;
            }

            if (isScreenNotice)
            {
                backgroundColor = new Color(31, 31, 31) * (0.88f * clampedAlpha);
                borderColor = new Color(246, 246, 246) * clampedAlpha;
                textColor = Color.White * clampedAlpha;
                return;
            }

            backgroundColor = new Color(255, 255, 245) * (0.92f * clampedAlpha);
            borderColor = new Color(72, 72, 72) * clampedAlpha;
            textColor = Color.Black * clampedAlpha;
        }
    }
}
