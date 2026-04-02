using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Misc;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Companions;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Loaders;
using HaSharedLibrary.Wz;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Animation;
using MobItem = HaCreator.MapSimulator.Entities.MobItem;
using HaRepacker.Utils;
using HaSharedLibrary;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib;
using MapleLib.WzLib;
using MapleLib.WzLib.Spine;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using MapleLib.WzLib.WzStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SD = System.Drawing;
using SDText = System.Drawing.Text;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Core;
using HaCreator.MapSimulator.Combat;
using MapleLib.Helpers;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;


namespace HaCreator.MapSimulator
{
    public partial class MapSimulator : Microsoft.Xna.Framework.Game
    {


        /// <summary>
        /// On frame draw
        /// </summary>
        /// <param name="gameTime"></param>
        protected override void Draw(GameTime gameTime)
        {
            float frameRate = 1 / (float)gameTime.ElapsedGameTime.TotalSeconds;
            int TickCount = currTickCount;
            //float delta = gameTime.ElapsedGameTime.Milliseconds / 1000f;


            MouseState mouseState = this._oldMouseState;
            int mouseXRelativeToMap = mouseState.X - mapShiftX;
            int mouseYRelativeToMap = mouseState.Y - mapShiftY;
            //System.Diagnostics.Debug.WriteLine("Mouse relative to map: X {0}, Y {1}", mouseXRelativeToMap, mouseYRelativeToMap);


            // The coordinates of the map's center point, obtained from _mapBoard.CenterPoint

            int mapCenterX = _mapBoard.CenterPoint.X;

            int mapCenterY = _mapBoard.CenterPoint.Y;



            // A Vector2 that calculates the offset between the map's current position (mapShiftX, mapShiftY) and its center point:

            // This shift vector is used in various Draw methods to properly position elements relative to the map's current view position.

            var shiftCenter = new Vector2(mapShiftX - mapCenterX, mapShiftY - mapCenterY);



            //GraphicsDevice.Clear(ClearOptions.Target, Color.Black, 1.0f, 0); // Clear the window to black

            GraphicsDevice.Clear(Color.Black);



            // Apply screen effects (tremble offset) to the transformation matrix
            Matrix effectMatrix = this._matrixScale;
            if (_screenEffects.IsTrembleActive)
            {
                effectMatrix = Matrix.CreateTranslation(_screenEffects.TrembleOffsetX, _screenEffects.TrembleOffsetY, 0) * this._matrixScale;
            }


            _spriteBatch.Begin(
                SpriteSortMode.Immediate, // spine :( needs to be drawn immediately to maintain the layer orders
                                          //SpriteSortMode.Deferred,
                BlendState.NonPremultiplied,
                Microsoft.Xna.Framework.Graphics.SamplerState.LinearClamp, // Add proper sampling
                DepthStencilState.None,
                RasterizerState.CullCounterClockwise,
                null,
                effectMatrix);
            //_skeletonMeshRenderer.Begin();


            // Create render context for RenderingManager
            var renderContext = new Managers.RenderContext(
                _spriteBatch, _skeletonMeshRenderer, gameTime,
                mapShiftX, mapShiftY, mapCenterX, mapCenterY,
                _renderParams, TickCount, _debugBoundaryTexture);


            // World rendering via RenderingManager
            _renderingManager.DrawBackgrounds(in renderContext, false); // back background
            _renderingManager.DrawMapObjects(in renderContext); // tiles and objects
            _renderingManager.DrawMobs(in renderContext); // mobs - rendered behind portals
            _remoteUserPool.Draw(_spriteBatch, _skeletonMeshRenderer, mapShiftX, mapShiftY, mapCenterX, mapCenterY, TickCount, _fontDebugValues, _playerManager?.Player, statusBarUi);
            _summonedPool.Draw(_spriteBatch, mapShiftX, mapShiftY, mapCenterX, mapCenterY, TickCount);
            DrawPlayer(gameTime, mapCenterX, mapCenterY, TickCount); // player character (has tombstone logic)
            _mobAttackSystem.Draw(_spriteBatch, _debugBoundaryTexture, mapShiftX, mapShiftY, mapCenterX, mapCenterY, TickCount);
            _renderingManager.DrawDrops(in renderContext); // item/meso drops
            _renderingManager.DrawDrops(in renderContext, elevatedOnly: true); // packet-authored elevated drop layer
            _renderingManager.DrawPortals(in renderContext); // portals
            _temporaryPortalField?.DrawCurrentMap(
                _mapBoard.MapInfo.id,
                _spriteBatch,
                _skeletonMeshRenderer,
                gameTime,
                mapShiftX,
                mapShiftY,
                mapCenterX,
                mapCenterY,
                _renderParams,
                TickCount);
            _renderingManager.DrawReactors(in renderContext); // reactors
            _renderingManager.DrawNpcs(in renderContext); // NPCs - rendered on top
            _socialRoomEmployeeActor.Draw(
                _spriteBatch,
                _skeletonMeshRenderer,
                gameTime,
                mapShiftX,
                mapShiftY,
                mapCenterX,
                mapCenterY,
                _renderParams,
                TickCount,
                _fontChat ?? _fontDebugValues,
                _debugBoundaryTexture);
            DrawNpcQuestAlerts(in renderContext);

            DrawNpcQuestFeedback(in renderContext);

            DrawPetIdleSpeechFeedback(in renderContext);

            _fieldMessageBoxRuntime.Draw(
                _spriteBatch,
                _fontChat,
                mapShiftX,
                mapShiftY,
                mapCenterX,
                mapCenterY,
                _renderParams.RenderWidth,
                _renderParams.RenderHeight,
                TickCount);
            _renderingManager.DrawTransportation(in renderContext); // ship/balrog
            _renderingManager.DrawBackgrounds(in renderContext, true); // front background
            _specialFieldRuntime.Draw(
                _spriteBatch,
                _skeletonMeshRenderer,
                gameTime,
                mapShiftX,
                mapShiftY,
                mapCenterX,
                mapCenterY,
                TickCount,
                _debugBoundaryTexture,
                _fontDebugValues);
            DrawClientOwnedResultFieldWrappers(TickCount);
            // Borders
            _renderingManager.DrawVRFieldBorder(in renderContext);
            _renderingManager.DrawLBFieldBorder(in renderContext);


            // Debug overlays (separate pass - only runs when debug mode is on)

            _renderingManager.DrawDebugOverlays(in renderContext);



            // Screen effects (fade, flash, explosion, motion blur) and animation effects

            _renderingManager.DrawScreenEffects(in renderContext);



            // Limited view field (fog of war) - draws after world, before UI

            _renderingManager.DrawLimitedView(in renderContext);

            DrawMapleTvOverlay(gameTime, TickCount);
            _packetFieldStateRuntime.DrawHelpOverlay(
                _spriteBatch,
                _fontChat,
                _renderParams.RenderWidth,
                _renderParams.RenderHeight,
                TickCount);
            DrawPacketOwnedFieldFeedbackState(TickCount);
            DrawPacketOwnedLocalOverlayState(TickCount, mapCenterX, mapCenterY);
            DrawPacketOwnedComboState(TickCount);
            DrawPacketOwnedTutorState(TickCount, mapCenterX, mapCenterY);


            //////////////////// UI related here ////////////////////

            _renderingManager.DrawTooltips(in renderContext, mouseState); 



            // Boss HP bar should stay behind the map UI layers.
            if (!_gameState.HideUIMode && _combatEffects.HasActiveBossBar)
            {
                EnsureBossHpBarAssetsLoaded();
                _combatEffects.DrawBossHPBar(_spriteBatch);
            }


            // Status bar [layer below minimap]
            if (!_gameState.HideUIMode) {
                DrawUI(gameTime, shiftCenter, _renderParams, mapCenterX, mapCenterY, mouseState, TickCount, IsActive); // status bar and minimap
            }


            if (!_gameState.HideUIMode)
            {
                _packetOwnedHudNoticeUI.SetScreenSize(_renderParams.RenderWidth, _renderParams.RenderHeight);
                _packetOwnedHudNoticeUI.Draw(_spriteBatch, _localOverlayRuntime, TickCount);
            }

            if (gameTime.TotalGameTime.TotalSeconds < 5)
            {
                if (!_gameState.IsLoginMap)
                {
                    _spriteBatch.DrawString(_fontNavigationKeysHelper,
                        _gameState.MobMovementEnabled ? _navHelpTextMobOn : _navHelpTextMobOff,
                        new Vector2(20, Height - 190), Color.White);
                }
            }


            if (IsLoginRuntimeSceneActive && !_gameState.HideUIMode)
            {
                DrawLoginRuntimeOverlay();
            }
            

            if (!_screenshotManager.TakeScreenshot && _gameState.ShowDebugMode)
            {
                _debugStringBuilder.Clear();
                _debugStringBuilder.Append("FPS: ").Append(frameRate).Append('\n');
                _debugStringBuilder.Append("Cursor: X ").Append(mouseState.X).Append(", Y ").Append(mouseState.Y).Append('\n');
                _debugStringBuilder.Append("Relative cursor: X ").Append(mouseXRelativeToMap).Append(", Y ").Append(mouseYRelativeToMap);


                _spriteBatch.DrawString(_fontDebugValues, _debugStringBuilder,

                    new Vector2(Width - 270, 10), Color.White); // use the original width to render text

            }



            // Draw chat messages and input box
            if (!_gameState.HideUIMode)
            {
                _skillCooldownNoticeUI.SetScreenSize(_renderParams.RenderWidth, _renderParams.RenderHeight);
                if (statusBarChatUI == null)
                {
                    _chat.Draw(_spriteBatch, TickCount);
                }


                // Draw pickup notices (meso/item gain messages at bottom right)
                _pickupNoticeUI?.Draw(_spriteBatch);
                _skillCooldownNoticeUI?.Draw(_spriteBatch);
            }


            // Draw portal fade overlay AFTER all UI elements (covers everything like official client)

            // This is separate from DrawScreenEffects which handles other effects drawn before UI

            _renderingManager.DrawPortalFadeOverlay(in renderContext);



            // Cursor [this is in front of everything else]
            mouseCursor.Draw(_spriteBatch, _skeletonMeshRenderer, gameTime,
                0, 0, 0, 0, // pos determined in the class
                null,
                _renderParams,
                TickCount);


            _spriteBatch.End();

            //_skeletonMeshRenderer.End();



            // Save screenshot if render is activated

            _screenshotManager.ProcessScreenshot(GraphicsDevice);





            base.Draw(gameTime);

        }

        private void DrawLocalPreparedSkillWorldOverlay(int mapCenterX, int mapCenterY, int currentTime)
        {
            if (statusBarUi == null)
            {
                return;
            }

            StatusBarPreparedSkillRenderData preparedSkill = GetPreparedSkillBarData(currentTime, PreparedSkillHudSurface.World);
            if (preparedSkill == null || !IsDragonPreparedSkill(preparedSkill.SkillId))
            {
                return;
            }

            statusBarUi.DrawPreparedSkillWorldOverlay(
                _spriteBatch,
                mapShiftX,
                mapShiftY,
                mapCenterX,
                mapCenterY,
                currentTime,
                preparedSkill);
        }




        /// <summary>
        /// Draws the player character
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawPlayer(GameTime gameTime, int mapCenterX, int mapCenterY, int TickCount)
        {
            if (IsLoginRuntimeSceneActive || _playerManager == null || _playerManager.Player == null)
                return;


            var player = _playerManager.Player;



            // If player is dead, draw tombstone at death position
            if (!player.IsAlive)
            {
                // Initialize tombstone falling physics on first frame of death
                if (!_tombHasLanded && _tombAnimationStartTime == 0)
                {
                    _tombAnimationStartTime = Environment.TickCount;


                    // Find the actual ground position below the death location
                    float groundY = player.DeathY;
                    var findFoothold = _playerManager.GetFootholdLookup();
                    if (findFoothold != null)
                    {
                        // Search downward from death position to find ground (large search range for mid-air deaths)
                        var fh = findFoothold(player.DeathX, player.DeathY, 2000);
                        if (fh != null)
                        {
                            // Calculate Y position on the foothold at the death X coordinate
                            float x1 = fh.FirstDot.X;
                            float y1 = fh.FirstDot.Y;
                            float x2 = fh.SecondDot.X;
                            float y2 = fh.SecondDot.Y;


                            if (Math.Abs(x2 - x1) < 0.001f)
                            {
                                groundY = y1;
                            }
                            else
                            {
                                float t = (player.DeathX - x1) / (x2 - x1);
                                t = Math.Max(0, Math.Min(1, t)); // Clamp to [0,1]
                                groundY = y1 + t * (y2 - y1);
                            }
                        }
                    }


                    _tombTargetY = groundY;
                    _tombCurrentY = player.DeathY - TOMB_START_HEIGHT; // Start above death position
                    _tombVelocityY = 0;
                    _tombHasLanded = false;
                }


                // Calculate delta time for physics

                float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;



                DrawTombstone(mapCenterX, mapCenterY, player.DeathX, deltaTime, Environment.TickCount);
                return;
            }
            else
            {
                // Reset tombstone state when player is alive (respawned)
                _tombAnimationStartTime = 0;
                _tombHasLanded = false;
                _tombVelocityY = 0;
            }


            // Draw living player

            _playerManager.Draw(
                _spriteBatch,
                _skeletonMeshRenderer,
                mapShiftX,
                mapShiftY,
                mapCenterX,
                mapCenterY,
                TickCount,
                () => DrawLocalPreparedSkillWorldOverlay(mapCenterX, mapCenterY, TickCount));


            // Draw debug box around player (only in debug mode F5)
            if (_gameState.ShowDebugMode && _debugBoundaryTexture != null)
            {
                int screenX = (int)player.X - mapShiftX + mapCenterX;
                int screenY = (int)player.Y - mapShiftY + mapCenterY;
                int boxSize = 60;


                // Draw a visible debug rectangle around player position

                Rectangle debugRect = new Rectangle(screenX - boxSize / 2, screenY - boxSize, boxSize, boxSize);

                _spriteBatch.Draw(_debugBoundaryTexture, debugRect, Color.Lime * 0.5f);



                // Draw crosshair at exact position
                _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(screenX - 2, screenY - 10, 4, 20), Color.Red);
                _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(screenX - 10, screenY - 2, 20, 4), Color.Red);
            }
        }




        /// <summary>
        /// Draws a tombstone that falls from above and lands at the death position.
        /// Uses animation frames from Effect.wz/Tomb.img
        /// </summary>
        private void DrawTombstone(int mapCenterX, int mapCenterY, float worldX, float deltaTime, int currentTime)
        {
            // Update falling physics if not landed
            if (!_tombHasLanded)
            {
                // Apply gravity
                _tombVelocityY += TOMB_GRAVITY * deltaTime;


                // Update position

                _tombCurrentY += _tombVelocityY * deltaTime;



                // Check if landed
                if (_tombCurrentY >= _tombTargetY)
                {
                    _tombCurrentY = _tombTargetY;
                    _tombHasLanded = true;
                }
            }


            int screenX = (int)worldX - mapShiftX + mapCenterX;

            int screenY = (int)_tombCurrentY - mapShiftY + mapCenterY;



            // Use loaded tombstone animation if available
            if (_tombFallFrames != null && _tombFallFrames.Count > 0)
            {
                IDXObject frameToDraw = null;


                if (!_tombHasLanded)
                {
                    // While falling, cycle through fall animation frames based on time
                    int elapsedTime = currentTime - _tombAnimationStartTime;
                    int totalDuration = 0;


                    for (int i = 0; i < _tombFallFrames.Count; i++)
                    {
                        int frameDelay = _tombFallFrames[i].Delay > 0 ? _tombFallFrames[i].Delay : 100;
                        if (elapsedTime < totalDuration + frameDelay)
                        {
                            frameToDraw = _tombFallFrames[i];
                            break;
                        }
                        totalDuration += frameDelay;
                    }


                    // If animation finished but still falling, loop to last frame
                    if (frameToDraw == null && _tombFallFrames.Count > 0)
                    {
                        frameToDraw = _tombFallFrames[_tombFallFrames.Count - 1];
                    }
                }
                else
                {
                    // Landed - show land frame (static)
                    frameToDraw = _tombLandFrame ?? (_tombFallFrames.Count > 0 ? _tombFallFrames[_tombFallFrames.Count - 1] : null);
                }


                // Draw the frame - apply origin offset stored in DXObject (X, Y are negative origin values)
                if (frameToDraw != null)
                {
                    frameToDraw.DrawBackground(_spriteBatch, _skeletonMeshRenderer, null,
                        screenX + frameToDraw.X, screenY + frameToDraw.Y, Color.White, false, null);
                    return;
                }
            }


            // Fallback: draw simple tombstone shape if animation not loaded

            if (_debugBoundaryTexture == null)

                return;



            int tombWidth = 30;

            int tombHeight = 40;

            int tombTop = screenY - tombHeight;



            // Main tombstone body (gray)
            _spriteBatch.Draw(_debugBoundaryTexture,
                new Rectangle(screenX - tombWidth / 2, tombTop, tombWidth, tombHeight),
                Color.DarkGray);


            // Tombstone top (rounded - approximated with smaller rectangle)
            _spriteBatch.Draw(_debugBoundaryTexture,
                new Rectangle(screenX - tombWidth / 2 + 3, tombTop - 8, tombWidth - 6, 10),
                Color.DarkGray);


            // Cross on tombstone
            int crossWidth = 4;
            int crossHeight = 16;
            int crossX = screenX - crossWidth / 2;
            int crossY = tombTop + 8;


            // Vertical part of cross
            _spriteBatch.Draw(_debugBoundaryTexture,
                new Rectangle(crossX, crossY, crossWidth, crossHeight),
                Color.Black);


            // Horizontal part of cross
            _spriteBatch.Draw(_debugBoundaryTexture,
                new Rectangle(crossX - 4, crossY + 4, crossWidth + 8, crossWidth),
                Color.Black);
        }




        /// <summary>
        /// Draw UI
        /// </summary>
        /// <param name="gameTime"></param>
        /// <param name="shiftCenter"></param>
        /// <param name="renderParams"></param>
        /// <param name="mapCenterX"></param>
        /// <param name="mapCenterY"></param>
        /// <param name="mouseState"></param>
        /// <param name="TickCount"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawUI(GameTime gameTime, Vector2 shiftCenter, RenderParameters renderParams,
            int mapCenterX, int mapCenterY, Microsoft.Xna.Framework.Input.MouseState mouseState, int TickCount, bool isWindowActive)
        {
            // Status bar [layer below minimap]
            if (statusBarUi != null)
            {
                statusBarUi.DrawPreparedSkillOverlay(
                    _spriteBatch,
                    _skeletonMeshRenderer,
                    gameTime,
                    mapShiftX,
                    mapShiftY,
                    mapCenterX,
                    mapCenterY,
                    _renderParams,
                    TickCount);


                IReadOnlyList<StatusBarPreparedSkillRenderData> remotePreparedSkills = _remoteUserPool.BuildPreparedSkillWorldOverlays(TickCount);
                for (int i = 0; i < _remoteUserPool.PreparedSkillWorldOverlayCount; i++)
                {
                    StatusBarPreparedSkillRenderData remotePreparedSkill = remotePreparedSkills[i];
                    statusBarUi.DrawPreparedSkillWorldOverlay(
                        _spriteBatch,
                        mapShiftX,
                        mapShiftY,
                        mapCenterX,
                        mapCenterY,
                        TickCount,
                        remotePreparedSkill);
                }

                statusBarUi.Draw(_spriteBatch, _skeletonMeshRenderer, gameTime,
                            mapShiftX, mapShiftY, minimapPos.X, minimapPos.Y,
                            null,
                            _renderParams,
                            TickCount);


                if (isWindowActive)
                {
                    statusBarUi.CheckMouseEvent((int)shiftCenter.X, (int)shiftCenter.Y, mouseState, mouseCursor, _renderParams.RenderWidth, _renderParams.RenderHeight);
                }


                // StatusBarChatUI may be null for pre-BigBang versions
                if (statusBarChatUI != null)
                {
                    statusBarChatUI.Draw(_spriteBatch, _skeletonMeshRenderer, gameTime,
                                mapShiftX, mapShiftY, minimapPos.X, minimapPos.Y,
                                null,
                                _renderParams,
                                TickCount);
                    if (isWindowActive)
                    {
                        statusBarChatUI.CheckMouseEvent((int)shiftCenter.X, (int)shiftCenter.Y, mouseState, mouseCursor, _renderParams.RenderWidth, _renderParams.RenderHeight);
                    }
                }
            }


            // Minimap
            if (miniMapUi != null)
            {
                // Update player position on minimap (uses actual character position, not viewport center)
                // MinimapPosition is the world coordinate that corresponds to minimap (0,0)
                if (_playerManager?.Player != null)
                {
                    miniMapUi.SetPlayerPosition(_playerManager.Player.X, _playerManager.Player.Y,
                        _mapBoard.MinimapPosition.X, _mapBoard.MinimapPosition.Y);
                }


                RefreshMinimapTrackedUserMarkers();



                miniMapUi.Draw(_spriteBatch, _skeletonMeshRenderer, gameTime,
                        mapShiftX, mapShiftY, minimapPos.X, minimapPos.Y,
                        null,
                        _renderParams,
                TickCount);
            }


            // UI Windows (Inventory, Equipment, Skills, Quest)
            // Toggle: I=Inventory, E=Equipment, S=Skills, Q=Quest
            // Handle mouse events for minimap and windows with proper priority
            // Windows are drawn ON TOP of minimap, so they get priority when starting a new drag
            // Once dragging starts, that element keeps exclusive control until mouse is released
            bool minimapIsDragging = miniMapUi != null && miniMapUi.IsDragging;
            bool npcOverlayIsVisible = _npcInteractionOverlay?.IsVisible == true;
            bool windowBlocksMinimap = npcOverlayIsVisible ||
                (uiWindowManager != null &&
                 (uiWindowManager.IsDraggingWindow || uiWindowManager.ContainsPoint(mouseState.X, mouseState.Y)));


            if (uiWindowManager != null)
            {
                uiWindowManager.Draw(_spriteBatch, _skeletonMeshRenderer, gameTime,
                    mapShiftX, mapShiftY, minimapPos.X, minimapPos.Y,
                    null,
                    _renderParams,
                    TickCount);


                // Check UI windows - but not if minimap is ALREADY being dragged
                if (ShouldRouteMouseToUiWindows(isWindowActive, minimapIsDragging, npcOverlayIsVisible))
                {
                    uiWindowManager.CheckMouseEvent((int)shiftCenter.X, (int)shiftCenter.Y, mouseState, mouseCursor, _renderParams.RenderWidth, _renderParams.RenderHeight);
                }
                else
                {
                    ResetUiWindowDragStates();
                }
            }


            _npcInteractionOverlay?.Draw(_spriteBatch, _renderParams.RenderWidth, _renderParams.RenderHeight);



            // Minimap mouse events
            if (miniMapUi != null && isWindowActive)
            {
                // If minimap is already being dragged, continue dragging regardless of window positions
                if (ShouldRouteMouseToMinimap(minimapIsDragging, windowBlocksMinimap))
                {
                    miniMapUi.CheckMouseEvent((int)shiftCenter.X, (int)shiftCenter.Y, mouseState, mouseCursor, _renderParams.RenderWidth, _renderParams.RenderHeight);
                }
            }
        }

        private void ResetUiWindowDragStates()
        {
            uiWindowManager?.ResetAllDragStates();
        }

        private static bool ShouldRouteMouseToUiWindows(bool isWindowActive, bool minimapIsDragging, bool npcOverlayIsVisible)
        {
            return isWindowActive && !minimapIsDragging && !npcOverlayIsVisible;
        }

        private static bool ShouldRouteMouseToMinimap(bool minimapIsDragging, bool windowBlocksMinimap)
        {
            return minimapIsDragging || !windowBlocksMinimap;
        }
    }
}
