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
        internal static bool ShouldRefreshMobFearFieldEffect(
            bool isFearActive,
            int desiredExpireTick,
            int currentExpireTick,
            float desiredIntensity,
            float currentIntensity)
        {
            if (!isFearActive)
            {
                return true;
            }

            if (desiredExpireTick > currentExpireTick)
            {
                return true;
            }

            return Math.Abs(desiredIntensity - currentIntensity) > 0.001f;
        }

        private void SyncMobFearFieldEffect(int currentTime)
        {
            if (_playerManager?.TryGetFearMobStatusVisualState(currentTime, out float intensity, out int remainingFearDurationMs) != true
                || remainingFearDurationMs <= 0)
            {
                _mobFearEffectExpireTick = int.MinValue;
                _mobFearEffectIntensity = 0f;
                if (_fieldEffects.IsFearActive)
                {
                    _fieldEffects.StopFearEffect();
                }

                return;
            }

            int desiredExpireTick = currentTime + remainingFearDurationMs;
            if (!ShouldRefreshMobFearFieldEffect(
                    _fieldEffects.IsFearActive,
                    desiredExpireTick,
                    _mobFearEffectExpireTick,
                    intensity,
                    _mobFearEffectIntensity))
            {
                return;
            }

            _fieldEffects.InitFearEffect(intensity, remainingFearDurationMs, MobFearEffectPulseCount, currentTime);
            _mobFearEffectExpireTick = desiredExpireTick;
            _mobFearEffectIntensity = intensity;
        }


        /// <summary>
        /// Key, and frame update handling
        /// </summary>
        /// <param name="gameTime"></param>
        protected override void Update(GameTime gameTime)
        {
            if (!_startupFirstUpdateLogged)
            {
                _startupFirstUpdateLogged = true;
                LogStartupCheckpoint($"First Update entered (loginMap={_gameState.IsLoginMap}, playerActive={_playerManager?.IsPlayerActive ?? false}, controlEnabled={_gameState.PlayerControlEnabled})");
            }

            if (!_startupPlayableLogged && (_playerManager?.IsPlayerActive ?? false) && (_gameState.IsLoginMap || _gameState.PlayerControlEnabled))
            {
                _startupPlayableLogged = true;
                LogStartupCheckpoint($"Startup reached playable update state at tick {Environment.TickCount}");
            }

            SyncBgmPlaybackToWindowFocus();
            _soundManager?.Update();


            float frameRate = 1 / (float)gameTime.ElapsedGameTime.TotalSeconds;
            currTickCount = Environment.TickCount;
            float delta = gameTime.ElapsedGameTime.Milliseconds / 1000f;
            bool isWindowActive = IsActive;
            KeyboardState newKeyboardState = Keyboard.GetState();  // get the newest state
            MouseState newMouseState = GetEffectiveMouseState(Mouse.GetState(), isWindowActive);


            // Update UI Windows - handles ESC to close windows and I/E/S/Q toggles
            // Pass chat state to prevent hotkeys from working while typing
            bool uiWindowsHandledEsc = false;
            if (uiWindowManager != null)
            {
                RefreshQuestUiState();
                uiWindowsHandledEsc = uiWindowManager.Update(gameTime, currTickCount, _chat.IsActive, isWindowActive);
                ProcessPendingRepairDurabilityRequest();
            }



            // Allows the game to exit via gamepad Back button only
            // ESC key is used to close UI windows (Inventory, Skills, Quest, Equipment)
            // To exit simulator: use Alt+F4, window X button, or gamepad Back button
#if !WINDOWS_STOREAPP
            bool backPressed = GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed;


            if (!_chat.IsActive && backPressed)
            {
                this.Exit();
                return;
            }
#endif
            // Handle full screen
            bool bIsAltEnterPressed = isWindowActive &&
                                      newKeyboardState.IsKeyDown(Keys.LeftAlt) &&
                                      newKeyboardState.IsKeyDown(Keys.Enter);
            if (bIsAltEnterPressed)
            {
                _DxDeviceManager.IsFullScreen = !_DxDeviceManager.IsFullScreen;
                _DxDeviceManager.ApplyChanges();
                return;
            }


            // Handle print screen
            if (isWindowActive && newKeyboardState.IsKeyDown(Keys.PrintScreen))
            {
                if (!_screenshotManager.TakeScreenshot && _screenshotManager.IsComplete)
                {
                    _screenshotManager.RequestScreenshot();
                }
            }




            // Handle mouse

            mouseCursor.UpdateCursorState();



            if (IsLoginRuntimeSceneActive)

            {
                EnsureMapTransferOfficialSessionBridgeState(shouldRun: false);
                EnsureComboCounterPacketInboxState(shouldRun: false);
                EnsureLocalUtilityPacketInboxState(shouldRun: false);
                EnsureLocalUtilityOfficialSessionBridgeState(shouldRun: false);
                EnsureExpeditionIntermediaryPacketInboxState(shouldRun: false);
                EnsureExpeditionIntermediaryOfficialSessionBridgeState(shouldRun: false);
                EnsureSocialListOfficialSessionBridgeState(shouldRun: false);
                EnsureEngagementProposalInboxState(shouldRun: false);
                EnsureStageTransitionPacketInboxState(shouldRun: false);
                EnsureReactorPoolPacketInboxState(shouldRun: false);
                EnsurePacketFieldOfficialSessionBridgeState(shouldRun: false);
                EnsureTradingRoomPacketInboxState(shouldRun: false);
                EnsureSummonedPacketInboxState(shouldRun: false);
                EnsureSummonedOfficialSessionBridgeState(shouldRun: false);
                EnsureMobAttackPacketInboxState(shouldRun: false);
                UpdateLoginRuntimeFrame(gameTime, newKeyboardState, newMouseState, isWindowActive);
                return;
            }



            // Advance scripted field state before mouse/world interaction handlers so
            // newly opened timed dialogs can claim direction mode on the same frame.
            DrainMemoryGamePacketInbox(currTickCount);
            RefreshFrameMobSnapshot();
            _specialFieldRuntime.SetWeddingPlayerState(_playerManager?.Player?.Build?.Id, _playerManager?.Player?.Position, _playerManager?.Player?.Build);
            _specialFieldRuntime.SetBattlefieldPlayerState(_playerManager?.Player?.Build?.Id);
            _specialFieldRuntime.SetSnowBallPlayerState(_playerManager?.Player?.Position);
            _specialFieldRuntime.SetDojoRuntimeState(
                _playerManager?.Player?.HP,
                _playerManager?.Player?.MaxHP,
                _framePrimaryBossMob?.AI?.HpPercent);
            _specialFieldRuntime.SetGuildBossPlayerState(_playerManager?.GetPlayerHitbox());
            _specialFieldRuntime.SetAriantArenaPlayerState(
                _playerManager?.Player?.Build?.Name,
                _playerManager?.Player?.Build?.Job,
                _remoteUserPool);
            _specialFieldRuntime.SetMonsterCarnivalPlayerState(_playerManager?.Player?.Build?.Name);
            DrainWeddingPacketInbox(currTickCount);
            DrainCoconutPacketInbox(currTickCount);
            FlushPendingCoconutAttackRequests();
            DrainAriantArenaPacketInbox(currTickCount);
            DrainMonsterCarnivalPacketInbox(currTickCount);
            DrainMassacrePacketInbox(currTickCount);
            DrainDojoPacketInbox(currTickCount);
            DrainTransportPacketInbox();
            DrainGuildBossTransport(currTickCount);

            DrainPartyRaidPacketInbox(currTickCount);
            DrainTournamentPacketInbox(currTickCount);

            DrainCookieHousePointInbox();
            EnsureEngagementProposalInboxState(shouldRun: true);
            DrainEngagementProposalInbox();

            _specialFieldRuntime.Update(gameTime, currTickCount);
            EnsureTradingRoomPacketInboxState(shouldRun: true);
            DrainTradingRoomPacketInbox(currTickCount);
            SyncWeddingRemoteActorsToSharedPool(_specialFieldRuntime.SpecialEffects.Wedding);
            MessengerRemoteUserSynchronizer.Sync(
                _remoteUserPool,
                _messengerRuntime,
                _playerManager?.Player?.Build?.Clone(),
                _playerManager?.Player?.Position ?? Vector2.Zero,
                ResolveSyntheticRemoteUserId);
            DrainRemoteUserPacketInbox(currTickCount);
            _remoteUserPool.Update(currTickCount, _playerManager?.Player);
            _remoteUserPool.SyncPortableChairPairState(_playerManager?.Player);
            EnsureSummonedPacketInboxState(shouldRun: true);
            EnsureSummonedOfficialSessionBridgeState(shouldRun: true);
            RefreshSummonedOfficialSessionBridgeDiscovery(currTickCount);
            DrainSummonedPacketInbox();
            DrainSummonedOfficialSessionBridge();
            EnsureMobAttackPacketInboxState(shouldRun: true);
            DrainMobAttackPacketInbox();
            _summonedPool.Update(currTickCount);
            while (_specialFieldRuntime.Minigames.SnowBall.TryConsumeChatMessage(out string snowBallChatMessage))
            {
                _chat?.AddMessage(snowBallChatMessage, new Color(255, 228, 151), currTickCount);
            }
            if (!_gameState.PendingMapChange)
            {
                if (_specialFieldRuntime.TryConsumePendingTransfer(out int specialFieldTransferMapId, out string specialFieldTransferPortalName)
                    && specialFieldTransferMapId > 0)
                {
                    QueueFieldTransfer(specialFieldTransferMapId, specialFieldTransferPortalName);
                }
            }
            SyncBattlefieldLocalAppearance();
            _remoteUserPool.SyncBattlefieldAppearance(_specialFieldRuntime.SpecialEffects.Battlefield);
            UpdateDirectionModeState(currTickCount);

            UpdateWorldChannelSelectorRequestState();
            EnsureStageTransitionPacketInboxState(shouldRun: _mapBoard?.MapInfo != null);
            DrainStageTransitionPacketInbox();
            EnsureReactorPoolPacketInboxState(shouldRun: _mapBoard?.MapInfo != null);
            EnsurePacketFieldOfficialSessionBridgeState(shouldRun: _mapBoard?.MapInfo != null);
            RefreshPacketFieldOfficialSessionBridgeDiscovery(currTickCount);
            DrainReactorPoolPacketInbox();
            DrainPacketFieldOfficialSessionBridge();
            EnsureComboCounterPacketInboxState(shouldRun: true);
            DrainComboCounterPacketInbox();
            UpdatePacketOwnedComboState(currTickCount);
            SyncPacketOwnedApspContextLifecycle();
            EnsureLocalUtilityPacketInboxState(shouldRun: true);
            EnsurePacketScriptOfficialSessionBridgeState(shouldRun: true);
            RefreshPacketScriptOfficialSessionBridgeDiscovery(currTickCount);
            EnsureLocalUtilityOfficialSessionBridgeState(shouldRun: true);
            RefreshLocalUtilityOfficialSessionBridgeDiscovery(currTickCount);
            EnsureExpeditionIntermediaryPacketInboxState(shouldRun: true);
            EnsureExpeditionIntermediaryOfficialSessionBridgeState(shouldRun: true);
            RefreshExpeditionIntermediaryOfficialSessionBridgeDiscovery(currTickCount);
            EnsureSocialListOfficialSessionBridgeState(shouldRun: true);
            RefreshSocialListOfficialSessionBridgeDiscovery(currTickCount);
            DrainLocalUtilityPacketInbox();
            DrainLocalUtilityOfficialSessionBridge();
            DrainExpeditionIntermediaryPacketInbox();
            DrainExpeditionIntermediaryOfficialSessionBridge();
            DrainSocialListOfficialSessionBridge();
            EnsureMapTransferOfficialSessionBridgeState(shouldRun: _mapBoard?.MapInfo != null);
            RefreshMapTransferOfficialSessionBridgeDiscovery(currTickCount);
            DrainMapTransferOfficialSessionBridge();
            SyncUtilityChannelSelectorAvailability();
            UpdatePacketOwnedTutorRuntime(currTickCount);
            UpdatePacketOwnedRadioSchedule(currTickCount);
            UpdateUtilityAudioMix(currTickCount);


            if (isWindowActive)
            {
                NpcInteractionOverlayResult npcOverlayResult = _npcInteractionOverlay != null
                    ? _npcInteractionOverlay.HandleMouse(newMouseState, _oldMouseState, _renderParams.RenderWidth, _renderParams.RenderHeight)
                    : default;
                HandlePacketOwnedQuestResultOverlayClose(npcOverlayResult.CloseKind);
                HandleAnimationDisplayerOverlayClose(npcOverlayResult.CloseKind);

                bool memoryGameMouseConsumed = false;
                bool tournamentMatchTableMouseConsumed = false;
                if (npcOverlayResult.PrimaryActionEntry != null)
                {
                    HandleNpcOverlayPrimaryAction(npcOverlayResult.PrimaryActionEntry);
                }

                if (!npcOverlayResult.Consumed &&
                    uiWindowManager?.ContainsPoint(newMouseState.X, newMouseState.Y) != true &&
                    _specialFieldRuntime.Minigames.Tournament.HandleMatchTableDialogMouse(
                        new Point(newMouseState.X, newMouseState.Y),
                        _renderParams.RenderWidth,
                        _renderParams.RenderHeight,
                        newMouseState.ScrollWheelValue - _oldMouseState.ScrollWheelValue,
                        newMouseState.LeftButton == ButtonState.Released && _oldMouseState.LeftButton == ButtonState.Pressed,
                        out string tournamentMatchTableMessage))
                {
                    tournamentMatchTableMouseConsumed = true;
                    if (!string.IsNullOrWhiteSpace(tournamentMatchTableMessage))
                    {
                        _chat.AddMessage(tournamentMatchTableMessage, new Color(255, 228, 151), currTickCount);
                    }
                }


                if (!npcOverlayResult.Consumed &&
                    !tournamentMatchTableMouseConsumed &&
                    newMouseState.LeftButton == ButtonState.Released &&
                    _oldMouseState.LeftButton == ButtonState.Pressed &&
                    uiWindowManager?.ContainsPoint(newMouseState.X, newMouseState.Y) != true &&
                    _specialFieldRuntime.Minigames.MemoryGame.HandleMouseClick(
                        new Point(newMouseState.X, newMouseState.Y),
                        _renderParams.RenderWidth,
                        _renderParams.RenderHeight,
                        currTickCount,
                        out string memoryGameMouseMessage))
                {
                    memoryGameMouseConsumed = true;
                    if (!string.IsNullOrWhiteSpace(memoryGameMouseMessage))
                    {
                        _chat.AddMessage(memoryGameMouseMessage, new Color(255, 228, 151), currTickCount);
                    }
                }


                if (!npcOverlayResult.Consumed && !memoryGameMouseConsumed && !tournamentMatchTableMouseConsumed)
                {
                    // Avoid leaking the overlay-dismissal click into world interactions while
                    // direction mode is transitioning through its delayed release window.
                    CheckNpcHover(newMouseState);
                    HandleNpcTalkClick(newMouseState);
                    HandlePortalDoubleClick(newMouseState);
                }
            }


            UpdateNpcQuestFeedbackState(currTickCount);
            UpdateNpcIdleSpeechState(currTickCount);
            UpdatePetEventSpeechState(currTickCount);
            UpdatePetIdleSpeechState(currTickCount);
            _fieldMessageBoxRuntime.Initialize(GraphicsDevice);
            _fieldMessageBoxRuntime.Update(currTickCount);
            _packetFieldStateRuntime.Initialize(GraphicsDevice, _mapBoard?.MapInfo);
            _packetFieldStateRuntime.Update(currTickCount);
            SyncPacketOwnedQuestTimerOwnerWindows(currTickCount);
            UpdatePacketOwnedStageTransitionState(currTickCount);
            UpdatePendingPacketOwnedQuestResultFollowUp();
            UpdatePacketOwnedFieldFeedbackState(currTickCount);
            UpdatePacketOwnedLocalOverlayState(currTickCount);
            _localOverlayRuntime.Update(currTickCount);

            _engagementProposalController.UpdateLocalContext(_playerManager?.Player?.Build);
            _weddingInvitationController.UpdateLocalContext(_playerManager?.Player?.Build);
            _weddingWishListController.UpdateLocalContext(_playerManager?.Player?.Build);
            _mapleTvRuntime.UpdateLocalContext(_playerManager?.Player?.Build);
            _mapleTvRuntime.Update(currTickCount);
            FlushPendingMapleTvSendResultFeedback(currTickCount);


            // Handle portal UP key interaction (player presses UP near portal)
            if (isWindowActive)
            {
                HandlePortalUpInteract(currTickCount);
            }


            _temporaryPortalField?.Update(currTickCount);



            // Handle same-map portal teleport with delay (no fade, just wait for delay)
            if (_sameMapTeleportPending)
            {
                int elapsed = currTickCount - _sameMapTeleportStartTime;
                if (elapsed >= _sameMapTeleportDelay)
                {
                    CompleteSameMapTeleport();
                }
            }


            if (HandlePendingMapChange())
            {
                return;
            }


            // Handle chat input (returns true if chat consumed the input)

            NpcInteractionOverlayResult npcKeyboardResult = isWindowActive && _npcInteractionOverlay != null
                ? _npcInteractionOverlay.HandleKeyboard(newKeyboardState, _oldKeyboardState)
                : default;
            HandlePacketOwnedQuestResultOverlayClose(npcKeyboardResult.CloseKind);
            HandleAnimationDisplayerOverlayClose(npcKeyboardResult.CloseKind);

            if (npcKeyboardResult.InputSubmission != null)
            {
                HandleNpcOverlayInputSubmission(npcKeyboardResult.InputSubmission);
            }

            bool uiCapturesKeyboard = uiWindowManager?.CapturesKeyboardInput == true
                || _npcInteractionOverlay?.CapturesKeyboardInput == true;
            bool chatConsumedInput = isWindowActive &&

                                     !uiCapturesKeyboard &&

                                     _chat.HandleInput(newKeyboardState, _oldKeyboardState, currTickCount);



            // Skip navigation and other key handlers if chat is active
            if (isWindowActive && !chatConsumedInput && !_chat.IsActive && !uiCapturesKeyboard)
            {
                // Navigate around the rendered object
                bool bIsShiftPressed = newKeyboardState.IsKeyDown(Keys.LeftShift) || newKeyboardState.IsKeyDown(Keys.RightShift);


                bool bIsUpKeyPressed = newKeyboardState.IsKeyDown(Keys.Up);
                bool bIsDownKeyPressed = newKeyboardState.IsKeyDown(Keys.Down);
                bool bIsLeftKeyPressed = newKeyboardState.IsKeyDown(Keys.Left);
                bool bIsRightKeyPressed = newKeyboardState.IsKeyDown(Keys.Right);


                // Arrow keys control player movement via physics system (not direct position)
                // Input is passed to PlayerManager which forwards to PlayerCharacter.SetInput()
                // The actual movement happens in PlayerCharacter.ProcessInput() using proper physics
                if (_gameState.PlayerControlEnabled && _playerManager?.Player != null)
                {
                    // Camera follows player - center player on screen
                    // Formula: screenX = worldX - mapShiftX + mapCenterX
                    // To center player: RenderWidth/2 = player.X - mapShiftX + mapCenterX
                    // So: mapShiftX = player.X + mapCenterX - RenderWidth/2
                    var player = _playerManager.Player;
                    CenterCameraOnWorldPosition(player.X, player.Y);
                }
                else
                {
                    // Free camera mode - original behavior
                    int moveOffset = bIsShiftPressed ? (int)(3000f / frameRate) : (int)(1500f / frameRate);
                    if (bIsLeftKeyPressed || bIsRightKeyPressed)
                    {
                        SetCameraMoveX(bIsLeftKeyPressed, bIsRightKeyPressed, moveOffset);
                    }
                    if (bIsUpKeyPressed || bIsDownKeyPressed)
                    {
                        SetCameraMoveY(bIsUpKeyPressed, bIsDownKeyPressed, moveOffset);
                    }
                }


                // Minimap uses the current player binding instead of a hardcoded M key.
                if (_playerManager?.Input?.IsPressed(InputAction.ToggleMinimap) == true)
                {
                    if (miniMapUi != null)
                        miniMapUi.MinimiseOrMaximiseMinimap(currTickCount);
                }


                // Hide UI
                if (newKeyboardState.IsKeyUp(Keys.H) && _oldKeyboardState.IsKeyDown(Keys.H)) {
                    this._gameState.HideUIMode = !this._gameState.HideUIMode;
                }
            }


            // Debug keys
            if (isWindowActive)
            {
                // Debug keys
                if (newKeyboardState.IsKeyUp(Keys.F5) && _oldKeyboardState.IsKeyDown(Keys.F5))
                {
                    this._gameState.ShowDebugMode = !this._gameState.ShowDebugMode;
                }


                // Toggle mob movement with F6
                if (newKeyboardState.IsKeyUp(Keys.F6) && _oldKeyboardState.IsKeyDown(Keys.F6))
                {
                    this._gameState.MobMovementEnabled = !this._gameState.MobMovementEnabled;
                }


                // Toggle player control mode with Tab (switch between player control and free camera)
                /*if (newKeyboardState.IsKeyUp(Keys.Tab) && _oldKeyboardState.IsKeyDown(Keys.Tab))
                {
                    _gameState.PlayerControlEnabled = !_gameState.PlayerControlEnabled;
                    Debug.WriteLine($"Player control: {(_gameState.PlayerControlEnabled ? "ENABLED" : "DISABLED (free camera)")}");
                }*/


                // Respawn player with R key at original spawn point (portal position)
                if (newKeyboardState.IsKeyUp(Keys.R) && _oldKeyboardState.IsKeyDown(Keys.R))
                {
                    if (TryHandleReviveShortcut(newKeyboardState))
                    {
                        Debug.WriteLine("Player revive request routed through revive owner.");
                    }
                    else
                    {
                        _playerManager?.Respawn();
                        var pos = _playerManager?.GetPlayerPosition();
                        Debug.WriteLine($"Player respawned at spawn point ({pos?.X}, {pos?.Y})");
                    }
                }


                // Test screen tremble with F7 (for debugging effects)
                if (newKeyboardState.IsKeyUp(Keys.F7) && _oldKeyboardState.IsKeyDown(Keys.F7))
                {
                    _screenEffects.TriggerTremble(15, false, 0, 0, true, currTickCount);
                }


                // Test knockback on random mob with F8 (for debugging)
                if (newKeyboardState.IsKeyUp(Keys.F8) && _oldKeyboardState.IsKeyDown(Keys.F8))
                {
                    TestKnockbackRandomMob();
                }


                // Test motion blur with F9 (for debugging)
                if (newKeyboardState.IsKeyUp(Keys.F9) && _oldKeyboardState.IsKeyDown(Keys.F9))
                {
                    _screenEffects.HorizontalBlur(0.7f, true, 500, currTickCount);
                }


                // Test explosion effect with F10 (for debugging)
                if (newKeyboardState.IsKeyUp(Keys.F10) && _oldKeyboardState.IsKeyDown(Keys.F10))
                {
                    // Trigger explosion at center of screen (converted to map coordinates)
                    float explosionX = -mapShiftX + Width / 2;
                    float explosionY = -mapShiftY + Height / 2;
                    _screenEffects.FireExplosion(explosionX, explosionY, 200, 800, currTickCount);
                }


                // Test chain lightning with F11 (for debugging)
                if (newKeyboardState.IsKeyUp(Keys.F11) && _oldKeyboardState.IsKeyDown(Keys.F11))
                {
                    // Create chain lightning from left to right of screen
                    float startX = -mapShiftX + 100;
                    float endX = -mapShiftX + Width - 100;
                    float y = -mapShiftY + Height / 2;


                    var points = new System.Collections.Generic.List<Vector2>
                    {
                        new Vector2(startX, y),
                        new Vector2(startX + (endX - startX) * 0.33f, y - 50),
                        new Vector2(startX + (endX - startX) * 0.66f, y + 50),
                        new Vector2(endX, y)
                    };
                    _animationEffects.AddChainLightning(points, new Color(100, 150, 255), 800, currTickCount, 4f, 10);
                }


                // Test falling animation with F12 (for debugging)
                if (newKeyboardState.IsKeyUp(Keys.F12) && _oldKeyboardState.IsKeyDown(Keys.F12))
                {
                    // Create burst of falling particles at screen center
                    TestFallingBurst(currTickCount);
                }


                // Weather controls: 1=Rain, 2=Snow, 3=Leaves, 0=Off
                if (newKeyboardState.IsKeyUp(Keys.D1) && _oldKeyboardState.IsKeyDown(Keys.D1))
                {
                    ToggleWeather(WeatherType.Rain);
                }
                if (newKeyboardState.IsKeyUp(Keys.D2) && _oldKeyboardState.IsKeyDown(Keys.D2))
                {
                    ToggleWeather(WeatherType.Snow);
                }
                if (newKeyboardState.IsKeyUp(Keys.D3) && _oldKeyboardState.IsKeyDown(Keys.D3))
                {
                    ToggleWeather(WeatherType.Leaves);
                }
                if (newKeyboardState.IsKeyUp(Keys.D0) && _oldKeyboardState.IsKeyDown(Keys.D0))
                {
                    ToggleWeather(WeatherType.None);
                }


                // Toggle fear effect with 4
                if (newKeyboardState.IsKeyUp(Keys.D4) && _oldKeyboardState.IsKeyDown(Keys.D4))
                {
                    if (_fieldEffects.IsFearActive)
                    {
                        _fieldEffects.StopFearEffect();
                    }
                    else
                    {
                        _fieldEffects.InitFearEffect(0.7f, 10000, 5, currTickCount);
                    }
                }


                // Test weather message with 5
                if (newKeyboardState.IsKeyUp(Keys.D5) && _oldKeyboardState.IsKeyDown(Keys.D5))
                {
                    _fieldEffects.OnBlowWeather(WeatherEffectType.Rain, null, "A gentle rain begins to fall...", 1f, 15000, currTickCount);
                    ToggleWeather(WeatherType.Rain);
                }


                // Test horizontal moving platform with 6
                if (newKeyboardState.IsKeyUp(Keys.D6) && _oldKeyboardState.IsKeyDown(Keys.D6))
                {
                    // Spawn platform at mouse position in map coordinates (same formula as portal detection)
                    float platX = _oldMouseState.X + mapShiftX - _mapBoard.CenterPoint.X;
                    float platY = _oldMouseState.Y + mapShiftY - _mapBoard.CenterPoint.Y;
                    _dynamicFootholds.CreateHorizontalPlatform(platX, platY, 100, 15, platX - 150, platX + 150, 80f, 500);
                }


                // Test vertical moving platform with 7
                if (newKeyboardState.IsKeyUp(Keys.D7) && _oldKeyboardState.IsKeyDown(Keys.D7))
                {
                    float platX = _oldMouseState.X + mapShiftX - _mapBoard.CenterPoint.X;
                    float platY = _oldMouseState.Y + mapShiftY - _mapBoard.CenterPoint.Y;
                    _dynamicFootholds.CreateVerticalPlatform(platX, platY, 80, 15, platY - 100, platY + 100, 60f, 300);
                }


                // Test timed spawn/despawn platform with 8
                if (newKeyboardState.IsKeyUp(Keys.D8) && _oldKeyboardState.IsKeyDown(Keys.D8))
                {
                    float platX = _oldMouseState.X + mapShiftX - _mapBoard.CenterPoint.X;
                    float platY = _oldMouseState.Y + mapShiftY - _mapBoard.CenterPoint.Y;
                    _dynamicFootholds.CreateTimedPlatform(platX, platY, 100, 15, 2000, 1500, 0);
                }


                // Test waypoint platform with 9
                if (newKeyboardState.IsKeyUp(Keys.D9) && _oldKeyboardState.IsKeyDown(Keys.D9))
                {
                    float platX = _oldMouseState.X + mapShiftX - _mapBoard.CenterPoint.X;
                    float platY = _oldMouseState.Y + mapShiftY - _mapBoard.CenterPoint.Y;
                    var waypoints = new System.Collections.Generic.List<Microsoft.Xna.Framework.Vector2>
                    {
                        new Microsoft.Xna.Framework.Vector2(platX, platY),
                        new Microsoft.Xna.Framework.Vector2(platX + 100, platY - 50),
                        new Microsoft.Xna.Framework.Vector2(platX + 200, platY),
                        new Microsoft.Xna.Framework.Vector2(platX + 100, platY + 50)
                    };
                    _dynamicFootholds.CreateWaypointPlatform(80, 15, waypoints, 70f, true, 200);
                }


                // Test limited view / fog of war with 0 (cycle through modes)
                if (newKeyboardState.IsKeyUp(Keys.D0) && _oldKeyboardState.IsKeyDown(Keys.D0))
                {
                    EnsureLimitedViewFieldInitialized();

                    if (_limitedViewField.UsesClientOwnedUpdateParity)
                    {
                        System.Diagnostics.Debug.WriteLine("[LimitedView] Client-owned limited-view wrapper active; keeping parity mode.");
                    }
                    else
                    {

                        if (!_limitedViewField.Enabled)
                        {
                            // Start with circle mode
                            _limitedViewField.EnableCircle(250f);
                            System.Diagnostics.Debug.WriteLine("[LimitedView] Enabled: Circle mode, radius 250");
                        }
                        else
                        {
                            // Cycle through modes: Circle -> Rectangle -> Spotlight -> Disable
                            switch (_limitedViewField.Mode)
                            {
                                case LimitedViewField.ViewMode.Circle:
                                    _limitedViewField.EnableRectangle(400f, 300f);
                                    System.Diagnostics.Debug.WriteLine("[LimitedView] Switched to: Rectangle mode 400x300");
                                    break;
                                case LimitedViewField.ViewMode.Rectangle:
                                    _limitedViewField.EnableSpotlight(300f, true);
                                    System.Diagnostics.Debug.WriteLine("[LimitedView] Switched to: Spotlight mode with pulse");
                                    break;
                                case LimitedViewField.ViewMode.Spotlight:
                                    _limitedViewField.Disable();
                                    System.Diagnostics.Debug.WriteLine("[LimitedView] Disabled");
                                    break;
                                default:
                                    _limitedViewField.DisableImmediate();
                                    break;
                            }
                        }
                    }
                }


                // Ship controls: [-] Start voyage, [=] Balrog/Skip/Reset
                // Based on CField_ContiMove::OnContiMove packet handling:
                // - Case 8: OnStartShipMoveField (LeaveShipMove when value==2)
                // - Case 10: OnMoveField (AppearShip=4, DisappearShip=5)
                // - Case 12: OnEndShipMoveField (EnterShipMove when value==6)
                if (newKeyboardState.IsKeyUp(Keys.OemMinus) && _oldKeyboardState.IsKeyDown(Keys.OemMinus))
                {
                    // Load ship and Balrog textures if not already loaded
                    if (!_transportField.HasShipTextures)
                    {
                        LoadTransportFieldTextures();
                    }


                    // Initialize and start a demo ship voyage at mouse position

                    float shipX = _oldMouseState.X + mapShiftX - _mapBoard.CenterPoint.X;

                    float shipY = _oldMouseState.Y + mapShiftY - _mapBoard.CenterPoint.Y;



                    // Initialize using client-accurate parameters:
                    // shipKind: 0 = regular ship (moves x0->x), 1 = Balrog type (appears/disappears)
                    // x: docked position, y: ship height, x0: away position
                    // f: flip (0=right, 1=left), tMove: movement duration in seconds
                    _transportField.Initialize(
                        shipKind: 0,           // Regular ship
                        x: (int)shipX + 400,   // Dock position (right)
                        y: (int)shipY,         // Y position
                        x0: (int)shipX - 400,  // Away position (left)
                        f: 0,                  // Face right
                        tMove: 10              // 10 second movement
                    );
                    _transportField.SetBackgroundScroll(true, 30f);


                    // Start with ship arriving (EnterShipMove - Case 12 value 6)
                    _transportField.EnterShipMove();
                }
                if (newKeyboardState.IsKeyUp(Keys.OemPlus) && _oldKeyboardState.IsKeyDown(Keys.OemPlus))
                {
                    // Cycle through ship actions based on current state
                    switch (_transportField.State)
                    {
                        case ShipState.Moving:
                        case ShipState.InTransit:
                            // Trigger Balrog attack during voyage
                            _transportField.TriggerBalrogAttack(5000);
                            break;
                        case ShipState.Docked:
                            // Ship is docked, start departure (LeaveShipMove - Case 8 value 2)
                            _transportField.LeaveShipMove();
                            break;
                        case ShipState.WaitingDeparture:
                            // Skip waiting, force immediate departure
                            _transportField.ForceDeparture();
                            break;
                        default:
                            // Reset to idle
                            _transportField.Reset();
                            break;
                    }
                }


                // Sparkle burst at mouse position with Space
                if (newKeyboardState.IsKeyUp(Keys.Space) && _oldKeyboardState.IsKeyDown(Keys.Space))
                {
                    float sparkleX = -mapShiftX + _oldMouseState.X;
                    float sparkleY = -mapShiftY + _oldMouseState.Y;
                    _particleSystem.CreateSparkleBurst(sparkleX, sparkleY, 30, Color.Gold, 1500);
                }
            }


            // Camera zoom controls (scroll wheel or keyboard)
            if (newMouseState.ScrollWheelValue != _oldMouseState.ScrollWheelValue && _gameState.UseSmoothCamera)
            {
                int scrollDelta = newMouseState.ScrollWheelValue - _oldMouseState.ScrollWheelValue;
                if (scrollDelta > 0)
                    _cameraController.ZoomIn();
                else if (scrollDelta < 0)
                    _cameraController.ZoomOut();
            }
            // Home key = reset zoom to 1.0
            if (newKeyboardState.IsKeyUp(Keys.Home) && _oldKeyboardState.IsKeyDown(Keys.Home) && _gameState.UseSmoothCamera)
            {
                _cameraController.ResetZoom();
                Debug.WriteLine("Camera zoom reset to 1.0");
            }
            // C key = random attack (TryDoingMeleeAttack/TryDoingShoot/TryDoingMagicAttack)
            if (newKeyboardState.IsKeyUp(Keys.C) && _oldKeyboardState.IsKeyDown(Keys.C)
                && !newKeyboardState.IsKeyDown(Keys.LeftShift) && !newKeyboardState.IsKeyDown(Keys.RightShift))
            {
                if (_playerManager != null && _playerManager.IsPlayerActive && _gameState.IsPlayerInputEnabled)
                {
                    bool attacked = _playerManager.TryDoingRandomAttack(currTickCount);
                    if (attacked)
                    {
                        Debug.WriteLine($"[C Key] Random attack executed");
                    }
                }
            }


            // Shift+C key = toggle smooth camera (moved from C key)
            if (newKeyboardState.IsKeyUp(Keys.C) && _oldKeyboardState.IsKeyDown(Keys.C)
                && (newKeyboardState.IsKeyDown(Keys.LeftShift) || newKeyboardState.IsKeyDown(Keys.RightShift)))
            {
                _gameState.UseSmoothCamera = !_gameState.UseSmoothCamera;
                Debug.WriteLine($"Smooth camera: {(_gameState.UseSmoothCamera ? "ENABLED" : "DISABLED")}");
                // When enabling, snap camera to current position
                if (_gameState.UseSmoothCamera && _playerManager != null && _playerManager.IsPlayerActive)
                {
                    var playerPos = _playerManager.GetPlayerPosition();
                    _cameraController.TeleportTo(playerPos.X, playerPos.Y);
                }
            }


            // Update screen effects (tremble, fade, flash, motion blur, explosion)

            _screenEffects.Update(currTickCount);



            // Calculate delta time once for all frame-rate independent updates

            float deltaSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;



            // Update animation effects (one-time, repeat, chain lightning, falling, follow)

            _animationEffects.Update(currTickCount, deltaSeconds);



            // Update combat effects (damage numbers, hit effects, HP bars)
            _combatEffects.Update(currTickCount, deltaSeconds);
            _combatEffects.SyncFromMobPool(_mobPool, currTickCount);
            _combatEffects.SyncHPBarsFromMobPool(_mobPool, currTickCount);


            // Update particle system

            _particleSystem.Update(currTickCount, deltaSeconds);



            // Update moving transport/platform state before entity movement so grounded
            // passengers can stay on a foothold-backed seam for the current frame.
            _dynamicFootholds.Update(currTickCount, deltaSeconds);
            _transportField.Update(currTickCount, deltaSeconds);
            _passengerSync.SyncPlayer(_playerManager?.Player, _dynamicFootholds, _transportField);


            // Update player character
            // Pass chat state to block movement/jump input while typing
            if (_playerManager != null)
            {
                _playerManager.IsPlayerControlEnabled = _gameState.IsPlayerInputEnabled;
                TryHandlePacketOwnedLocalFollowReleaseInput(
                    newKeyboardState,
                    _oldKeyboardState,
                    isWindowActive,
                    chatConsumedInput || _chat.IsActive || uiCapturesKeyboard,
                    currTickCount);
                _playerManager.Update(currTickCount, deltaSeconds, _chat.IsActive || uiCapturesKeyboard, isWindowActive);
                UpdatePacketOwnedFuncKeyRuntime(
                    currTickCount,
                    newKeyboardState,
                    _oldKeyboardState,
                    isWindowActive,
                    chatConsumedInput || _chat.IsActive || uiCapturesKeyboard);
                UpdateReviveOwnerState(currTickCount);
                UpdatePacketOwnedPetConsumeMpRuntime(currTickCount);
                SyncPacketOwnedLocalFollowCharacter();


                if (_gameState.IsPlayerInputEnabled && _playerManager.IsPlayerActive)
                {
                    Vector2 updatedPlayerPosition = _playerManager.GetPlayerPosition();
                    CheckReactorTouch(updatedPlayerPosition.X, updatedPlayerPosition.Y, currentTick: currTickCount);
                }


                // Update camera controller based on player/camera mode

                var player = _playerManager.Player;

                bool isPlayerDead = player != null && !player.IsAlive;



                if (_gameState.UseSmoothCamera)
                {
                    if (_gameState.PlayerControlEnabled && _playerManager.IsPlayerActive)
                    {
                        // Use camera controller for smooth player following
                        var playerPos = _playerManager.GetPlayerPosition();
                        bool isOnGround = _playerManager.IsPlayerOnGround();
                        bool facingRight = _playerManager.IsPlayerFacingRight();


                        _cameraController.Update(playerPos.X, playerPos.Y, facingRight, isOnGround, deltaSeconds);

                        _cameraController.UpdateShake(deltaSeconds);



                        // Get camera position from controller

                        mapShiftX = _cameraController.MapShiftX + (int)_cameraController.ShakeOffsetX;

                        mapShiftY = _cameraController.MapShiftY + (int)_cameraController.ShakeOffsetY;



                        // Apply boundary clamping (simple clamp, preserves smooth movement)
                        ClampCameraToBoundaries();
                    }
                    else if (isPlayerDead)
                    {
                        // Player is dead - keep camera focused on death position (no movement)
                        _cameraController.Update(player.DeathX, player.DeathY, true, true, deltaSeconds);
                        _cameraController.UpdateShake(deltaSeconds);


                        mapShiftX = _cameraController.MapShiftX + (int)_cameraController.ShakeOffsetX;

                        mapShiftY = _cameraController.MapShiftY + (int)_cameraController.ShakeOffsetY;



                        ClampCameraToBoundaries();
                    }
                    else
                    {
                        // Free camera mode with smooth scrolling
                        bool left = isWindowActive && newKeyboardState.IsKeyDown(Keys.Left);
                        bool right = isWindowActive && newKeyboardState.IsKeyDown(Keys.Right);
                        bool up = isWindowActive && newKeyboardState.IsKeyDown(Keys.Up);
                        bool down = isWindowActive && newKeyboardState.IsKeyDown(Keys.Down);
                        bool shift = isWindowActive &&
                                     (newKeyboardState.IsKeyDown(Keys.LeftShift) || newKeyboardState.IsKeyDown(Keys.RightShift));
                        int freeCamSpeed = shift ? 3000 : 1500; // pixels per second


                        _cameraController.UpdateFreeCamera(left, right, up, down, freeCamSpeed, deltaSeconds);

                        _cameraController.UpdateShake(deltaSeconds);



                        mapShiftX = _cameraController.MapShiftX + (int)_cameraController.ShakeOffsetX;

                        mapShiftY = _cameraController.MapShiftY + (int)_cameraController.ShakeOffsetY;



                        // Apply boundary clamping (simple clamp, preserves smooth movement)
                        ClampCameraToBoundaries();
                    }
                }
                else
                {
                    // Legacy instant camera (no smoothing)
                    if (_gameState.PlayerControlEnabled && _playerManager.IsPlayerActive)
                    {
                        var playerPos = _playerManager.GetPlayerPosition();
                        CenterCameraOnWorldPosition(playerPos.X, playerPos.Y);


                        // Apply boundary clamping
                        ClampLegacyCameraToBoundaries();
                    }
                    else if (isPlayerDead)
                    {
                        // Player is dead - keep camera focused on death position
                        CenterCameraOnWorldPosition(player.DeathX, player.DeathY);


                        // Apply boundary clamping
                        ClampLegacyCameraToBoundaries();
                    }
                }
            }


            UpdateFieldRuleRuntime(currTickCount);

            UpdateReactorRuntime(currTickCount, deltaSeconds);

            SyncMobFearFieldEffect(currTickCount);



            // Update field effects (weather messages, fear effect, obstacles)

            // Pass deltaSeconds * 1000 to convert to milliseconds for frame-rate independence

            _fieldEffects.Update(currTickCount, Width, Height, _oldMouseState.X, _oldMouseState.Y, deltaSeconds * 1000f);



            // Update limited view field (fog of war) - use player position if available
            float playerX = _playerManager?.IsPlayerActive == true
                ? _playerManager.GetPlayerPosition().X
                : mapShiftX + _renderParams.RenderWidth / 2f;
            float playerY = _playerManager?.IsPlayerActive == true
                ? _playerManager.GetPlayerPosition().Y
                : mapShiftY + _renderParams.RenderHeight / 2f;
            SyncClientOwnedLimitedViewFocus(playerX, playerY);
            _limitedViewField.Update(gameTime, playerX, playerY);


            _passengerSync.SyncGroundMobPassengers(_frameMovableMobs, _dynamicFootholds, _transportField);



            // Update mob movement

            UpdateMobMovement(gameTime);



            // Update NPC movement and action cycling

            UpdateNpcActions(gameTime);
            DrainSocialRoomEmployeeOfficialSessionBridge(currTickCount);
            UpdateSocialRoomEmployeeActor(gameTime);


            // Pre-calculate visibility for all objects (culling optimization)

            _frameNumber++;

            UpdateObjectVisibility();



            this._oldKeyboardState = newKeyboardState;  // set the new state as the old state for next time

            this._oldMouseState = newMouseState;  // set the new state as the old state for next time



            base.Update(gameTime);

        }





        private void UpdateLoginWorldPopulationDrift()
        {
            if (!ShouldUseLoginWorldMetadata ||
                _selectorRequestKind != SelectorRequestKind.None ||
                unchecked(currTickCount - _nextLoginWorldPopulationUpdateAt) < 0)
            {
                return;
            }


            bool changed = false;
            foreach ((int worldId, LoginWorldSelectorMetadata metadata) in _loginWorldMetadataByWorld.ToArray())
            {
                if (metadata.HasAuthoritativePopulationData)
                {
                    continue;
                }


                List<ChannelSelectionState> updatedChannels = new(metadata.Channels.Count);

                bool worldChanged = false;



                foreach (ChannelSelectionState channel in metadata.Channels)
                {
                    if (channel.Capacity <= 0)
                    {
                        updatedChannels.Add(channel);
                        continue;
                    }


                    int driftSeed = ((currTickCount / LoginWorldPopulationUpdateIntervalMs) + 1 + (worldId * 7) + (channel.ChannelIndex * 11)) % 9;
                    int occupancyDelta = driftSeed - 4;
                    int nextUserCount = Math.Clamp(channel.UserCount + (occupancyDelta * Math.Max(4, channel.Capacity / 70)), 0, channel.Capacity);
                    bool isCurrentSelection = worldId == _simulatorWorldId && channel.ChannelIndex == _simulatorChannelIndex;
                    bool nextSelectable = nextUserCount < channel.Capacity || isCurrentSelection;


                    if (nextUserCount != channel.UserCount || nextSelectable != channel.IsSelectable)
                    {
                        worldChanged = true;
                    }


                    updatedChannels.Add(new ChannelSelectionState(
                        channel.ChannelIndex,
                        nextUserCount,
                        channel.Capacity,
                        nextSelectable,
                        channel.RequiresAdultAccount));
                }


                if (!worldChanged)
                {
                    continue;
                }


                _loginWorldMetadataByWorld[worldId] = new LoginWorldSelectorMetadata(
                    worldId,
                    updatedChannels,
                    metadata.RequiresAdultAccount,
                    metadata.HasAuthoritativePopulationData,
                    metadata.RecommendMessage,
                    metadata.RecommendOrder,
                    metadata.WorldState,
                    metadata.BlocksCharacterCreation,
                    metadata.WorldName);
                changed = true;
            }


            _nextLoginWorldPopulationUpdateAt = currTickCount + LoginWorldPopulationUpdateIntervalMs;



            if (!changed)
            {
                return;
            }


            UpdateRecommendedLoginWorlds();
            RefreshWorldChannelSelectorWindows();
            SyncRecommendWorldWindow();
        }

        private void CenterCameraOnWorldPosition(float worldX, float worldY)
        {
            mapShiftX = (int)(worldX + _mapCenterX - _renderParams.RenderWidth / 2);
            mapShiftY = (int)(worldY + _mapCenterY - _renderParams.RenderHeight / 2);
        }

        private void ClampLegacyCameraToBoundaries()
        {
            SetCameraMoveX(true, false, 0);
            SetCameraMoveX(false, true, 0);
            SetCameraMoveY(true, false, 0);
            SetCameraMoveY(false, true, 0);
        }




        private bool HandlePendingMapChange()
        {
            if (_gameState.PendingMapChange && _loadMapCallback != null)
            {
                if (_gameState.PendingMapId == _mapBoard.MapInfo.id
                    && (!string.IsNullOrEmpty(_gameState.PendingPortalName)
                        || (_gameState.PendingPortalNameCandidates?.Length ?? 0) > 0
                        || _gameState.PendingPortalIndex >= 0))
                {
                    PortalInstance targetPortal = ResolvePortalByNameCandidatesOrIndex(
                        _mapBoard.BoardItems.Portals,
                        _gameState.PendingPortalName,
                        _gameState.PendingPortalNameCandidates,
                        _gameState.PendingPortalIndex);
                    if (targetPortal != null && !_sameMapTeleportPending)
                    {
                        int targetPortalIndex = _portalPool?.GetPortalIndexByName(targetPortal.pn) ?? -1;
                        StartSameMapTeleport(
                            targetPortal.X,
                            targetPortal.Y,
                            targetPortal.delay ?? SAME_MAP_PORTAL_DEFAULT_DELAY_MS,
                            currTickCount,
                            targetPortalIndex,
                            targetPortal.pn,
                            targetPortal.tn,
                            usePacketOwnedApply: true);
                    }


                    _gameState.PendingMapChange = false;
                    _gameState.PendingMapId = -1;
                    _gameState.PendingPortalName = null;
                    _gameState.PendingPortalNameCandidates = Array.Empty<string>();
                    _gameState.PendingPortalIndex = -1;
                }
                else
                {
                    if (_portalFadeState == PortalFadeState.None)
                    {
                        _portalFadeState = PortalFadeState.FadingOut;
                        _screenEffects.FadeOut(PORTAL_FADE_DURATION_MS, currTickCount);
                    }


                    if (_portalFadeState == PortalFadeState.FadingOut)
                    {
                        _screenEffects.UpdateFade(currTickCount);
                        if (_screenEffects.IsFadeOutComplete || !_screenEffects.IsFadeActive)
                        {
                            Stopwatch mapChangeStopwatch = Stopwatch.StartNew();
                            PendingCrossMapTeleportTarget pendingCrossMapTeleport = _pendingCrossMapTeleportTarget;
                            _gameState.PendingMapChange = false;


                            Stopwatch loadCallbackStopwatch = Stopwatch.StartNew();
                            Tuple<Board, string> result = _loadMapCallback(_gameState.PendingMapId);
                            loadCallbackStopwatch.Stop();
                            Debug.WriteLine($"[MapChange] _loadMapCallback({_gameState.PendingMapId}) took {loadCallbackStopwatch.ElapsedMilliseconds} ms");
                            if (result != null && result.Item1 != null)
                            {
                                string entryRestrictionMessage = GetPendingMapEntryRestrictionMessage(result.Item1);
                                if (!string.IsNullOrWhiteSpace(entryRestrictionMessage))
                                {
                                    ShowFieldRestrictionMessage(entryRestrictionMessage);
                                    _gameState.PendingMapId = -1;
                                    _gameState.PendingPortalName = null;
                                    _gameState.PendingPortalNameCandidates = Array.Empty<string>();
                                    _gameState.PendingPortalIndex = -1;
                                    _portalFadeState = PortalFadeState.FadingIn;
                                    _screenEffects.FadeIn(PORTAL_FADE_DURATION_MS, currTickCount);
                                    return true;
                                }


                                int currentMapId = _mapBoard?.MapInfo?.id ?? -1;
                                if (currentMapId >= 0 && _mobsArray != null)
                                {
                                    _mapStateCache.SaveMapState(currentMapId, _mobsArray, currTickCount);
                                }


                                Stopwatch unloadStopwatch = Stopwatch.StartNew();
                                UnloadMapContent();
                                unloadStopwatch.Stop();
                                Debug.WriteLine($"[MapChange] UnloadMapContent took {unloadStopwatch.ElapsedMilliseconds} ms");

                                Stopwatch loadContentStopwatch = Stopwatch.StartNew();
                                LoadMapContent(
                                    result.Item1,
                                    result.Item2,
                                    _gameState.PendingPortalName,
                                    _gameState.PendingPortalIndex,
                                    _gameState.PendingPortalNameCandidates);
                                loadContentStopwatch.Stop();
                                Debug.WriteLine($"[MapChange] LoadMapContent took {loadContentStopwatch.ElapsedMilliseconds} ms");



                                int newMapId = _mapBoard?.MapInfo?.id ?? -1;
                                if (newMapId >= 0 && _mobsArray != null)
                                {
                                    _mapStateCache.RestoreMapState(newMapId, _mobsArray, currTickCount);
                                }

                                if (pendingCrossMapTeleport != null && pendingCrossMapTeleport.MapId == newMapId)
                                {
                                    TryFinalizePendingCrossMapTeleport(pendingCrossMapTeleport, out _);
                                    _pendingCrossMapTeleportTarget = null;
                                }
                                else if (pendingCrossMapTeleport != null)
                                {
                                    _pendingCrossMapTeleportTarget = null;
                                    _packetOwnedTeleportRequestActive = false;
                                }


                                _playerManager?.Input?.SyncState();
                                mapChangeStopwatch.Stop();
                                Debug.WriteLine($"[MapChange] Total transition to map {newMapId} took {mapChangeStopwatch.ElapsedMilliseconds} ms");

                            }



                            _gameState.PendingMapId = -1;

                            _gameState.PendingPortalName = null;
                            _gameState.PendingPortalNameCandidates = Array.Empty<string>();
                            _gameState.PendingPortalIndex = -1;



                            _portalFadeState = PortalFadeState.FadingIn;

                            _screenEffects.FadeIn(PORTAL_FADE_DURATION_MS, currTickCount);

                        }



                        return true;
                    }
                }
            }


            if (_portalFadeState == PortalFadeState.FadingIn)
            {
                if (_screenEffects.IsFadeInComplete || !_screenEffects.IsFadeActive)
                {
                    _portalFadeState = PortalFadeState.None;
                    _playerManager?.Input?.SyncState();
                }
            }


            return false;
        }
    }
}
