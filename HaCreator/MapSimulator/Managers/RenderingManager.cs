using System;
using System.Runtime.CompilerServices;
using System.Text;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.Character;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Manages all rendering operations for the MapSimulator.
    /// Consolidates drawing logic for backgrounds, objects, entities, effects, and UI.
    /// </summary>
    public class RenderingManager
    {
        #region Dependencies

        // Reference to the main simulator for accessing shared state
        private readonly Func<Board> _getMapBoard;
        private readonly Func<int> _getWidth;
        private readonly Func<int> _getHeight;

        // Effect systems
        private EffectManager _effectManager;
        private DynamicFootholdSystem _dynamicFootholds;
        private TransportationField _transportField;
        private LimitedViewField _limitedViewField;

        // Game state
        private GameStateManager _gameState;

        // Player system
        private PlayerManager _playerManager;

        // Pools
        private DropPool _dropPool;

        // Fonts
        private SpriteFont _fontDebugValues;
        private SpriteFont _fontChat;
        private SpriteFont _fontNavigationKeysHelper;

        // Reusable string builder for debug text
        private readonly StringBuilder _debugStringBuilder = new StringBuilder(256);

        #endregion

        #region Render Arrays

        // Backgrounds
        private BackgroundItem[] _backgroundsBackArray;
        private BackgroundItem[] _backgroundsFrontArray;

        // Map objects (layered)
        private BaseDXDrawableItem[][] _mapObjectsArray;

        // Entities
        private MobItem[] _mobsArray;
        private NpcItem[] _npcsArray;
        private PortalItem[] _portalsArray;
        private ReactorItem[] _reactorsArray;
        private TooltipItem[] _tooltipsArray;

        #endregion

        #region Border Textures

        private Texture2D _vrBoundaryTextureLeft;
        private Texture2D _vrBoundaryTextureRight;
        private Texture2D _lbTextureLeft;
        private Texture2D _lbTextureRight;

        private Rectangle _vrFieldBoundary;
        private bool _drawVRBorderLeftRight;

        private const int VR_BORDER_WIDTHHEIGHT = 600;
        private const int LB_BORDER_WIDTHHEIGHT = 300;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new RenderingManager
        /// </summary>
        /// <param name="getMapBoard">Function to get current map board</param>
        /// <param name="getWidth">Function to get render width</param>
        /// <param name="getHeight">Function to get render height</param>
        public RenderingManager(Func<Board> getMapBoard, Func<int> getWidth, Func<int> getHeight)
        {
            _getMapBoard = getMapBoard ?? throw new ArgumentNullException(nameof(getMapBoard));
            _getWidth = getWidth ?? throw new ArgumentNullException(nameof(getWidth));
            _getHeight = getHeight ?? throw new ArgumentNullException(nameof(getHeight));
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize managers and systems
        /// </summary>
        public void Initialize(
            EffectManager effectManager,
            GameStateManager gameState,
            DynamicFootholdSystem dynamicFootholds,
            TransportationField transportField,
            LimitedViewField limitedViewField)
        {
            _effectManager = effectManager;
            _gameState = gameState;
            _dynamicFootholds = dynamicFootholds;
            _transportField = transportField;
            _limitedViewField = limitedViewField;
        }

        /// <summary>
        /// Set entity pools
        /// </summary>
        public void SetPools(DropPool dropPool, PlayerManager playerManager)
        {
            _dropPool = dropPool;
            _playerManager = playerManager;
        }

        /// <summary>
        /// Set fonts for text rendering
        /// </summary>
        public void SetFonts(SpriteFont fontDebugValues, SpriteFont fontChat, SpriteFont fontNavigationKeysHelper)
        {
            _fontDebugValues = fontDebugValues;
            _fontChat = fontChat;
            _fontNavigationKeysHelper = fontNavigationKeysHelper;
        }

        /// <summary>
        /// Set render arrays for drawable items
        /// </summary>
        public void SetRenderArrays(
            BackgroundItem[] backgroundsBack,
            BackgroundItem[] backgroundsFront,
            BaseDXDrawableItem[][] mapObjects,
            MobItem[] mobs,
            NpcItem[] npcs,
            PortalItem[] portals,
            ReactorItem[] reactors,
            TooltipItem[] tooltips)
        {
            _backgroundsBackArray = backgroundsBack ?? Array.Empty<BackgroundItem>();
            _backgroundsFrontArray = backgroundsFront ?? Array.Empty<BackgroundItem>();
            _mapObjectsArray = mapObjects;
            _mobsArray = mobs ?? Array.Empty<MobItem>();
            _npcsArray = npcs ?? Array.Empty<NpcItem>();
            _portalsArray = portals ?? Array.Empty<PortalItem>();
            _reactorsArray = reactors ?? Array.Empty<ReactorItem>();
            _tooltipsArray = tooltips ?? Array.Empty<TooltipItem>();
        }

        /// <summary>
        /// Set VR border rendering data
        /// </summary>
        public void SetVRBorderData(
            Rectangle vrFieldBoundary,
            bool drawVRBorderLeftRight,
            Texture2D vrBoundaryTextureLeft,
            Texture2D vrBoundaryTextureRight)
        {
            _vrFieldBoundary = vrFieldBoundary;
            _drawVRBorderLeftRight = drawVRBorderLeftRight;
            _vrBoundaryTextureLeft = vrBoundaryTextureLeft;
            _vrBoundaryTextureRight = vrBoundaryTextureRight;
        }

        /// <summary>
        /// Set LB border rendering data
        /// </summary>
        public void SetLBBorderData(Texture2D lbTextureLeft, Texture2D lbTextureRight)
        {
            _lbTextureLeft = lbTextureLeft;
            _lbTextureRight = lbTextureRight;
        }

        #endregion

        #region Main Draw Methods

        /// <summary>
        /// Draws background layer (back or front)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawBackgrounds(in RenderContext context, bool front)
        {
            var items = front ? _backgroundsFrontArray : _backgroundsBackArray;
            if (items == null) return;

            for (int i = 0; i < items.Length; i++)
            {
                items[i].Draw(context.SpriteBatch, context.SkeletonMeshRenderer, context.GameTime,
                    context.MapShiftX, context.MapShiftY, context.MapCenterX, context.MapCenterY,
                    null,
                    context.RenderParams,
                    context.TickCount);
            }
        }

        /// <summary>
        /// Draws map objects (tiles and objects) across all layers
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawMapObjects(in RenderContext context)
        {
            if (_mapObjectsArray == null) return;

            for (int layer = 0; layer < _mapObjectsArray.Length; layer++)
            {
                BaseDXDrawableItem[] layerItems = _mapObjectsArray[layer];
                if (layerItems == null) continue;

                for (int i = 0; i < layerItems.Length; i++)
                {
                    BaseDXDrawableItem item = layerItems[i];
                    if (!item.IsVisible)
                        continue;

                    item.Draw(context.SpriteBatch, context.SkeletonMeshRenderer, context.GameTime,
                        context.MapShiftX, context.MapShiftY, context.MapCenterX, context.MapCenterY,
                        null,
                        context.RenderParams,
                        context.TickCount);
                }
            }
        }

        /// <summary>
        /// Draws all mobs
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawMobs(in RenderContext context)
        {
            if (_mobsArray == null) return;

            for (int i = 0; i < _mobsArray.Length; i++)
            {
                MobItem mobItem = _mobsArray[i];
                if (mobItem == null)
                    continue;

                ReflectionDrawableBoundary mirrorFieldData = mobItem.CachedMirrorBoundary;

                mobItem.Draw(context.SpriteBatch, context.SkeletonMeshRenderer, context.GameTime,
                    context.MapShiftX, context.MapShiftY, context.MapCenterX, context.MapCenterY,
                    mirrorFieldData,
                    context.RenderParams,
                    context.TickCount);
            }
        }

        /// <summary>
        /// Draws all NPCs
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawNpcs(in RenderContext context)
        {
            if (_npcsArray == null) return;

            for (int i = 0; i < _npcsArray.Length; i++)
            {
                NpcItem npcItem = _npcsArray[i];
                ReflectionDrawableBoundary mirrorFieldData = npcItem.CachedMirrorBoundary;

                npcItem.Draw(context.SpriteBatch, context.SkeletonMeshRenderer, context.GameTime,
                    context.MapShiftX, context.MapShiftY, context.MapCenterX, context.MapCenterY,
                    mirrorFieldData,
                    context.RenderParams,
                    context.TickCount);
            }
        }

        /// <summary>
        /// Draws all portals
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawPortals(in RenderContext context)
        {
            if (_portalsArray == null) return;

            for (int i = 0; i < _portalsArray.Length; i++)
            {
                PortalItem portalItem = _portalsArray[i];
                if (!portalItem.IsVisible)
                    continue;

                portalItem.Draw(context.SpriteBatch, context.SkeletonMeshRenderer, context.GameTime,
                    context.MapShiftX, context.MapShiftY, context.MapCenterX, context.MapCenterY,
                    null,
                    context.RenderParams,
                    context.TickCount);
            }
        }

        /// <summary>
        /// Draws all reactors
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawReactors(in RenderContext context)
        {
            if (_reactorsArray == null) return;

            for (int i = 0; i < _reactorsArray.Length; i++)
            {
                ReactorItem reactorItem = _reactorsArray[i];
                if (!reactorItem.IsVisible)
                    continue;

                reactorItem.Draw(context.SpriteBatch, context.SkeletonMeshRenderer, context.GameTime,
                    context.MapShiftX, context.MapShiftY, context.MapCenterX, context.MapCenterY,
                    null,
                    context.RenderParams,
                    context.TickCount);
            }
        }

        /// <summary>
        /// Draws the player character
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawPlayer(in RenderContext context)
        {
            if (_playerManager == null || !_playerManager.IsPlayerActive)
                return;

            _playerManager.Draw(context.SpriteBatch, context.SkeletonMeshRenderer,
                context.MapShiftX, context.MapShiftY, context.MapCenterX, context.MapCenterY, context.TickCount);

            // Draw debug box around player (only in debug mode)
            var player = _playerManager.Player;
            if (_gameState.ShowDebugMode && player != null && context.DebugTexture != null)
            {
                int screenX = (int)player.X - context.MapShiftX + context.MapCenterX;
                int screenY = (int)player.Y - context.MapShiftY + context.MapCenterY;
                int boxSize = 60;

                Rectangle debugRect = new Rectangle(screenX - boxSize / 2, screenY - boxSize, boxSize, boxSize);
                context.SpriteBatch.Draw(context.DebugTexture, debugRect, Color.Lime * 0.5f);

                // Draw crosshair at exact position
                context.SpriteBatch.Draw(context.DebugTexture, new Rectangle(screenX - 2, screenY - 10, 4, 20), Color.Red);
                context.SpriteBatch.Draw(context.DebugTexture, new Rectangle(screenX - 10, screenY - 2, 20, 4), Color.Red);
            }
        }

        /// <summary>
        /// Draws all item/meso drops
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawDrops(in RenderContext context)
        {
            if (_dropPool == null || _dropPool.ActiveDropCount == 0)
                return;

            int screenWidth = context.RenderWidth;
            int screenHeight = context.RenderHeight;

            foreach (var drop in _dropPool.GetRenderableDrops(0, screenWidth, 0, screenHeight,
                context.MapShiftX, context.MapShiftY, context.MapCenterX, context.MapCenterY))
            {
                int screenX = (int)drop.X - context.MapShiftX + context.MapCenterX;
                int screenY = (int)drop.Y - context.MapShiftY + context.MapCenterY;

                IDXObject currentFrame = null;
                if (drop.AnimFrames != null && drop.AnimFrames.Count > 0)
                {
                    int frameIndex = drop.CurrentFrame % drop.AnimFrames.Count;
                    currentFrame = drop.AnimFrames[frameIndex];
                }
                else
                {
                    currentFrame = drop.Icon;
                }

                if (currentFrame != null)
                {
                    Color iconColor = Color.White * drop.Alpha;
                    currentFrame.DrawBackground(context.SpriteBatch, context.SkeletonMeshRenderer, null,
                        screenX, screenY, iconColor, false, null);
                }
                else if (context.DebugTexture != null)
                {
                    // Fallback: Draw a colored rectangle
                    int size = (int)(16 * drop.Scale);
                    Color dropColor = drop.Type == DropType.Meso
                        ? new Color((byte)255, (byte)215, (byte)0) * drop.Alpha
                        : new Color((byte)100, (byte)150, (byte)255) * drop.Alpha;

                    context.SpriteBatch.Draw(context.DebugTexture,
                        new Rectangle(screenX - size / 2, screenY - size, size, size),
                        dropColor);

                    // Draw amount text for mesos
                    if (drop.Type == DropType.Meso && _fontDebugValues != null && drop.MesoAmount > 0)
                    {
                        DrawMesoAmountText(context, screenX, screenY, size, drop.MesoAmount, drop.Alpha, dropColor);
                    }
                }
            }
        }

        private void DrawMesoAmountText(in RenderContext context, int screenX, int screenY, int size, int mesoAmount, float alpha, Color dropColor)
        {
            string mesoText = mesoAmount.ToString();
            Vector2 textSize = _fontDebugValues.MeasureString(mesoText);
            Vector2 textPos = new Vector2(screenX - textSize.X / 2, screenY - size - textSize.Y - 2);

            Color outlineColor = Color.Black * alpha;
            context.SpriteBatch.DrawString(_fontDebugValues, mesoText, textPos + new Vector2(-1, 0), outlineColor);
            context.SpriteBatch.DrawString(_fontDebugValues, mesoText, textPos + new Vector2(1, 0), outlineColor);
            context.SpriteBatch.DrawString(_fontDebugValues, mesoText, textPos + new Vector2(0, -1), outlineColor);
            context.SpriteBatch.DrawString(_fontDebugValues, mesoText, textPos + new Vector2(0, 1), outlineColor);
            context.SpriteBatch.DrawString(_fontDebugValues, mesoText, textPos, dropColor);
        }

        /// <summary>
        /// Draws tooltips
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawTooltips(in RenderContext context, MouseState mouseState)
        {
            if (_tooltipsArray == null || _tooltipsArray.Length == 0)
                return;

            var mapBoard = _getMapBoard();
            if (mapBoard == null) return;

            for (int i = 0; i < _tooltipsArray.Length; i++)
            {
                TooltipItem tooltip = _tooltipsArray[i];
                if (tooltip.TooltipInstance.CharacterToolTip != null)
                {
                    Rectangle tooltipRect = tooltip.TooltipInstance.CharacterToolTip.Rectangle;
                    if (tooltipRect != null)
                    {
                        Rectangle rect = new Rectangle(
                            tooltipRect.X - (int)context.ShiftCenter.X,
                            tooltipRect.Y - (int)context.ShiftCenter.Y,
                            tooltipRect.Width, tooltipRect.Height);

                        if (_gameState.ShowDebugMode && context.DebugTexture != null)
                        {
                            DrawBorder(context.SpriteBatch, rect, 1, Color.White, new Color(Color.Gray, 0.3f), context.DebugTexture);

                            if (tooltip.CanUpdateDebugText(context.TickCount, 1000))
                            {
                                tooltip.DebugText = $"X: {rect.X}, Y: {rect.Y}";
                            }
                            context.SpriteBatch.DrawString(_fontDebugValues, tooltip.DebugText, new Vector2(rect.X, rect.Y), Color.White);
                        }

                        if (!rect.Contains(mouseState.X, mouseState.Y))
                            continue;
                    }
                }

                tooltip.Draw(context.SpriteBatch, context.SkeletonMeshRenderer, context.GameTime,
                    context.MapShiftX, context.MapShiftY, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y,
                    null,
                    context.RenderParams,
                    context.TickCount);
            }
        }

        /// <summary>
        /// Draws transportation field (ship)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawTransportation(in RenderContext context)
        {
            _transportField?.Draw(context.SpriteBatch, context.SkeletonMeshRenderer, context.GameTime,
                context.MapShiftX, context.MapShiftY, context.MapCenterX, context.MapCenterY, context.TickCount,
                context.DebugTexture, _fontDebugValues);
        }

        /// <summary>
        /// Draws limited view field (fog of war)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawLimitedView(in RenderContext context)
        {
            _limitedViewField?.Draw(context.SpriteBatch, context.MapShiftX, context.MapShiftY,
                context.MapCenterX, context.MapCenterY);
        }

        #endregion

        #region Border Drawing

        /// <summary>
        /// Draws the VR field border
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawVRFieldBorder(in RenderContext context)
        {
            if (_gameState.IsBigBang2Update || !_drawVRBorderLeftRight ||
                (_vrFieldBoundary.X == 0 && _vrFieldBoundary.Y == 0))
                return;

            Color borderColor = Color.Black;

            // Draw left line
            if (_vrBoundaryTextureLeft != null)
            {
                context.SpriteBatch.Draw(_vrBoundaryTextureLeft,
                    new Rectangle(
                        _vrFieldBoundary.Left - (VR_BORDER_WIDTHHEIGHT + context.MapShiftX),
                        _vrFieldBoundary.Top - context.MapShiftY,
                        VR_BORDER_WIDTHHEIGHT,
                        _vrFieldBoundary.Height),
                    borderColor);
            }

            // Draw right line
            if (_vrBoundaryTextureRight != null)
            {
                context.SpriteBatch.Draw(_vrBoundaryTextureRight,
                    new Rectangle(
                        _vrFieldBoundary.Right - context.MapShiftX,
                        _vrFieldBoundary.Top - context.MapShiftY,
                        VR_BORDER_WIDTHHEIGHT,
                        _vrFieldBoundary.Height),
                    borderColor);
            }
        }

        /// <summary>
        /// Draws the LB field border (for maps before 1366x768 resolution update)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawLBFieldBorder(in RenderContext context)
        {
            if (!_gameState.IsBigBang2Update ||
                (_vrFieldBoundary.X == 0 && _vrFieldBoundary.Y == 0))
                return;

            Color borderColor = Color.Black;
            int width = _getWidth();

            // Draw left line
            if (_lbTextureLeft != null)
            {
                int distanceToVRLeft = _vrFieldBoundary.Left - context.MapShiftX;
                int adjustedWidth = Math.Min(_lbTextureLeft.Width, distanceToVRLeft + LB_BORDER_WIDTHHEIGHT);

                if (adjustedWidth > LB_BORDER_WIDTHHEIGHT)
                {
                    context.SpriteBatch.Draw(_lbTextureLeft,
                        new Rectangle(-LB_BORDER_WIDTHHEIGHT, 0, adjustedWidth, _lbTextureLeft.Height),
                        borderColor);
                }
            }

            // Draw right line
            if (_lbTextureRight != null)
            {
                int distanceToVRRight = width - _vrFieldBoundary.Right - context.MapShiftX;
                int adjustedWidth = Math.Min(_lbTextureRight.Width, distanceToVRRight + LB_BORDER_WIDTHHEIGHT);

                if (adjustedWidth > LB_BORDER_WIDTHHEIGHT)
                {
                    context.SpriteBatch.Draw(_lbTextureRight,
                        new Rectangle(width - adjustedWidth + LB_BORDER_WIDTHHEIGHT, 0, adjustedWidth, _lbTextureRight.Height),
                        borderColor);
                }
            }
        }

        #endregion

        #region Screen Effects

        /// <summary>
        /// Draws screen overlay effects (fade, flash, motion blur, explosion)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawScreenEffects(in RenderContext context)
        {
            if (_effectManager == null) return;

            var screenEffects = _effectManager.Screen;
            int width = _getWidth();
            int height = _getHeight();

            // Draw flash overlay
            if (screenEffects.IsFlashActive && screenEffects.FlashAlpha > 0 && context.DebugTexture != null)
            {
                Color flashColor = screenEffects.FlashColor * screenEffects.FlashAlpha;
                context.SpriteBatch.Draw(context.DebugTexture,
                    new Rectangle(0, 0, width, height),
                    flashColor);
            }

            // Draw explosion effect
            if (screenEffects.IsExplosionActive && screenEffects.ExplosionAlpha > 0)
            {
                DrawExplosionRing(context,
                    screenEffects.ExplosionOrigin,
                    screenEffects.ExplosionRadius,
                    screenEffects.ExplosionRingWidth,
                    screenEffects.ExplosionColor * screenEffects.ExplosionAlpha);
            }

            // Draw motion blur overlay
            if (screenEffects.IsMotionBlurActive && screenEffects.MotionBlurStrength > 0.01f)
            {
                DrawMotionBlurOverlay(context);
            }

            // Draw animation effects
            _effectManager.Animation?.Draw(context.SpriteBatch, context.SkeletonMeshRenderer,
                context.GameTime, context.DebugTexture, context.MapShiftX, context.MapShiftY, context.TickCount);

            // Draw combat effects
            var mapBoard = _getMapBoard();
            if (mapBoard != null)
            {
                _effectManager.Combat?.Draw(context.SpriteBatch, context.SkeletonMeshRenderer,
                    context.MapShiftX, context.MapShiftY, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y);
            }

            // Draw particles
            _effectManager.Particles?.Draw(context.SpriteBatch, context.DebugTexture,
                context.MapShiftX, context.MapShiftY);

            // Draw field effects
            _effectManager.Field?.Draw(context.SpriteBatch, context.DebugTexture,
                width, height, context.MapShiftX, context.MapShiftY, _fontChat);
        }

        /// <summary>
        /// Draws the portal fade overlay on top of everything
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawPortalFadeOverlay(in RenderContext context)
        {
            if (_effectManager == null) return;

            var screenEffects = _effectManager.Screen;
            if (screenEffects.FadeAlpha > 0 && context.DebugTexture != null)
            {
                Color fadeColor = screenEffects.FadeColor * screenEffects.FadeAlpha;
                context.SpriteBatch.Draw(context.DebugTexture,
                    new Rectangle(0, 0, _getWidth(), _getHeight()),
                    fadeColor);
            }
        }

        private void DrawExplosionRing(in RenderContext context, Vector2 origin, float radius, float ringWidth, Color color)
        {
            if (context.DebugTexture == null) return;

            float screenX = origin.X + context.MapShiftX;
            float screenY = origin.Y + context.MapShiftY;

            int segments = 32;
            float outerRadius = radius + ringWidth / 2;

            for (int i = 0; i < segments; i++)
            {
                float angle1 = (float)(i * 2 * Math.PI / segments);
                float angle2 = (float)((i + 1) * 2 * Math.PI / segments);

                float cos1 = (float)Math.Cos(angle1);
                float sin1 = (float)Math.Sin(angle1);
                float cos2 = (float)Math.Cos(angle2);
                float sin2 = (float)Math.Sin(angle2);

                Vector2 outer1 = new Vector2(screenX + outerRadius * cos1, screenY + outerRadius * sin1);
                Vector2 outer2 = new Vector2(screenX + outerRadius * cos2, screenY + outerRadius * sin2);

                DrawThickLine(context, outer1, outer2, ringWidth, color);
            }
        }

        private void DrawThickLine(in RenderContext context, Vector2 start, Vector2 end, float thickness, Color color)
        {
            if (context.DebugTexture == null) return;

            Vector2 delta = end - start;
            float length = delta.Length();
            if (length < 0.01f) return;

            float rotation = (float)Math.Atan2(delta.Y, delta.X);

            context.SpriteBatch.Draw(
                context.DebugTexture,
                start,
                null,
                color,
                rotation,
                new Vector2(0, 0.5f),
                new Vector2(length, thickness),
                SpriteEffects.None,
                0);
        }

        private void DrawMotionBlurOverlay(in RenderContext context)
        {
            if (_effectManager == null) return;

            var screenEffects = _effectManager.Screen;
            Vector2[] offsets = screenEffects.GetMotionBlurOffsets();
            if (offsets.Length == 0) return;

            float alpha = screenEffects.MotionBlurStrength * 0.3f;
            float angle = screenEffects.MotionBlurAngle;
            float streakLength = screenEffects.MotionBlurStrength * 50f;

            // Draw motion blur streaks (simplified visual effect)
            // Full implementation would require render target manipulation
        }

        #endregion

        #region Debug Overlays

        /// <summary>
        /// Draws debug overlays for all objects
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawDebugOverlays(in RenderContext context)
        {
            if (!_gameState.ShowDebugMode || context.DebugTexture == null || _fontDebugValues == null)
                return;

            var mapBoard = _getMapBoard();
            if (mapBoard == null) return;

            // Draw portal debug info
            DrawPortalDebugInfo(context);

            // Draw reactor debug info
            DrawReactorDebugInfo(context);

            // Draw mob debug info
            DrawMobDebugInfo(context);

            // Draw NPC debug info
            DrawNpcDebugInfo(context);

            // Draw dynamic platforms
            _dynamicFootholds?.DrawDebug(context.SpriteBatch, context.DebugTexture,
                context.MapShiftX, context.MapShiftY, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y);

            // Draw transportation field
            _transportField?.DrawDebug(context.SpriteBatch, context.DebugTexture,
                context.MapShiftX, context.MapShiftY, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y, _fontDebugValues);
        }

        private void DrawPortalDebugInfo(in RenderContext context)
        {
            if (_portalsArray == null) return;

            for (int i = 0; i < _portalsArray.Length; i++)
            {
                PortalItem portalItem = _portalsArray[i];
                if (!portalItem.IsVisible) continue;

                PortalInstance instance = portalItem.PortalInstance;
                Rectangle rect = new Rectangle(
                    instance.X - (int)context.ShiftCenter.X - (instance.Width - 20),
                    instance.Y - (int)context.ShiftCenter.Y - instance.Height,
                    instance.Width + 40,
                    instance.Height);

                DrawBorder(context.SpriteBatch, rect, 1, Color.White, new Color(Color.Gray, 0.3f), context.DebugTexture);

                if (portalItem.CanUpdateDebugText(context.TickCount, 1000))
                {
                    _debugStringBuilder.Clear();
                    _debugStringBuilder.Append(" x: ").Append(rect.X).Append('\n');
                    _debugStringBuilder.Append(" y: ").Append(rect.Y).Append('\n');
                    _debugStringBuilder.Append(" script: ").Append(instance.script).Append('\n');
                    _debugStringBuilder.Append(" tm: ").Append(instance.tm).Append('\n');
                    _debugStringBuilder.Append(" pt: ").Append(instance.pt).Append('\n');
                    _debugStringBuilder.Append(" pn: ").Append(instance.pn);
                    portalItem.DebugText = _debugStringBuilder.ToString();
                }
                context.SpriteBatch.DrawString(_fontDebugValues, portalItem.DebugText, new Vector2(rect.X, rect.Y), Color.White);
            }
        }

        private void DrawReactorDebugInfo(in RenderContext context)
        {
            if (_reactorsArray == null) return;

            for (int i = 0; i < _reactorsArray.Length; i++)
            {
                ReactorItem reactorItem = _reactorsArray[i];
                if (!reactorItem.IsVisible) continue;

                ReactorInstance instance = reactorItem.ReactorInstance;
                Rectangle rect = new Rectangle(
                    instance.X - (int)context.ShiftCenter.X - (instance.Width - 20),
                    instance.Y - (int)context.ShiftCenter.Y - instance.Height,
                    Math.Max(80, instance.Width + 40),
                    Math.Max(120, instance.Height));

                DrawBorder(context.SpriteBatch, rect, 1, Color.White, new Color(Color.Gray, 0.3f), context.DebugTexture);

                if (reactorItem.CanUpdateDebugText(context.TickCount, 1000))
                {
                    _debugStringBuilder.Clear();
                    _debugStringBuilder.Append(" x: ").Append(rect.X).Append('\n');
                    _debugStringBuilder.Append(" y: ").Append(rect.Y).Append('\n');
                    _debugStringBuilder.Append(" id: ").Append(instance.ReactorInfo.ID).Append('\n');
                    _debugStringBuilder.Append(" name: ").Append(instance.Name);
                    reactorItem.DebugText = _debugStringBuilder.ToString();
                }
                context.SpriteBatch.DrawString(_fontDebugValues, reactorItem.DebugText, new Vector2(rect.X, rect.Y), Color.White);
            }
        }

        private void DrawMobDebugInfo(in RenderContext context)
        {
            if (_mobsArray == null) return;

            for (int i = 0; i < _mobsArray.Length; i++)
            {
                MobItem mobItem = _mobsArray[i];
                if (mobItem == null) continue;

                MobInstance instance = mobItem.MobInstance;
                int mobX = mobItem.CurrentX;
                int mobY = mobItem.CurrentY;

                Rectangle rect = new Rectangle(
                    mobX - (int)context.ShiftCenter.X - (instance.Width - 20),
                    mobY - (int)context.ShiftCenter.Y - instance.Height,
                    Math.Max(100, instance.Width + 40),
                    Math.Max(140, instance.Height));

                DrawBorder(context.SpriteBatch, rect, 1, Color.White, new Color(Color.Gray, 0.3f), context.DebugTexture);

                if (mobItem.CanUpdateDebugText(context.TickCount, 500))
                {
                    _debugStringBuilder.Clear();
                    _debugStringBuilder.Append(" x: ").Append(mobX).Append(", y: ").Append(mobY).Append('\n');
                    _debugStringBuilder.Append(" id: ").Append(instance.MobInfo.ID).Append('\n');
                    if (mobItem.MovementInfo != null)
                    {
                        _debugStringBuilder.Append(" type: ").Append(mobItem.MovementInfo.MoveType).Append('\n');
                        _debugStringBuilder.Append(" action: ").Append(mobItem.CurrentAction).Append('\n');
                        _debugStringBuilder.Append(" dir: ").Append(mobItem.MovementInfo.MoveDirection).Append('\n');
                    }
                    mobItem.DebugText = _debugStringBuilder.ToString();
                }
                context.SpriteBatch.DrawString(_fontDebugValues, mobItem.DebugText, new Vector2(rect.X, rect.Y), Color.White);
            }
        }

        private void DrawNpcDebugInfo(in RenderContext context)
        {
            if (_npcsArray == null) return;

            for (int i = 0; i < _npcsArray.Length; i++)
            {
                NpcItem npcItem = _npcsArray[i];
                NpcInstance instance = npcItem.NpcInstance;

                Rectangle rect = new Rectangle(
                    instance.X - (int)context.ShiftCenter.X - (instance.Width - 20),
                    instance.Y - (int)context.ShiftCenter.Y - instance.Height,
                    Math.Max(100, instance.Width + 40),
                    Math.Max(120, instance.Height));

                DrawBorder(context.SpriteBatch, rect, 1, Color.White, new Color(Color.Gray, 0.3f), context.DebugTexture);

                if (npcItem.CanUpdateDebugText(context.TickCount, 1000))
                {
                    _debugStringBuilder.Clear();
                    _debugStringBuilder.Append(" x: ").Append(rect.X).Append('\n');
                    _debugStringBuilder.Append(" y: ").Append(rect.Y).Append('\n');
                    _debugStringBuilder.Append(" id: ").Append(instance.NpcInfo.ID);
                    npcItem.DebugText = _debugStringBuilder.ToString();
                }
                context.SpriteBatch.DrawString(_fontDebugValues, npcItem.DebugText, new Vector2(rect.X, rect.Y), Color.White);
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Draws a border rectangle with optional fill
        /// </summary>
        public static void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, int borderWidth,
            Color borderColor, Color? fillColor, Texture2D texture)
        {
            if (texture == null) return;

            // Fill
            if (fillColor.HasValue)
            {
                spriteBatch.Draw(texture, rect, fillColor.Value);
            }

            // Top
            spriteBatch.Draw(texture, new Rectangle(rect.X, rect.Y, rect.Width, borderWidth), borderColor);
            // Bottom
            spriteBatch.Draw(texture, new Rectangle(rect.X, rect.Y + rect.Height - borderWidth, rect.Width, borderWidth), borderColor);
            // Left
            spriteBatch.Draw(texture, new Rectangle(rect.X, rect.Y, borderWidth, rect.Height), borderColor);
            // Right
            spriteBatch.Draw(texture, new Rectangle(rect.X + rect.Width - borderWidth, rect.Y, borderWidth, rect.Height), borderColor);
        }

        #endregion
    }
}
