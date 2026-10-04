using HaCreator.MapSimulator.Physics;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Companions;
using HaCreator.MapSimulator.Contracts;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Hosting;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Loaders;
using HaSharedLibrary.Wz;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Animation;
using MobItem = HaCreator.MapSimulator.Entities.MobItem;
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
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Core;
using HaCreator.MapSimulator.Combat;
using MapleLib.Helpers;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;


namespace HaCreator.MapSimulator
{
    public partial class MapSimulator : Microsoft.Xna.Framework.Game
    {
        internal readonly struct ClientOwnedMapBoundaryContract
        {
            public ClientOwnedMapBoundaryContract(
                Rectangle viewportRectangle,
                Rectangle fieldBoundary,
                int side,
                int top,
                int bottom,
                Rectangle? mirrorBottomRectangle)
            {
                ViewportRectangle = viewportRectangle;
                FieldBoundary = fieldBoundary;
                Side = Math.Max(0, side);
                Top = Math.Max(0, top);
                Bottom = Math.Max(0, bottom);
                MirrorBottomRectangle = mirrorBottomRectangle;
            }

            public Rectangle ViewportRectangle { get; }
            public Rectangle FieldBoundary { get; }
            public int Side { get; }
            public int Top { get; }
            public int Bottom { get; }
            public Rectangle? MirrorBottomRectangle { get; }
        }

        internal static ClientOwnedMapBoundaryContract ResolveClientOwnedMapBoundaryContract(
            MapInfo mapInfo,
            Rectangle? mapVrRectangle,
            Point centerPoint,
            Point mapSize)
        {
            return ResolveClientOwnedMapBoundaryContract(mapInfo, mapVrRectangle, centerPoint, mapSize, linkedMapResolver: null);
        }

        internal static ClientOwnedMapBoundaryContract ResolveClientOwnedMapBoundaryContract(
            MapInfo mapInfo,
            Rectangle? mapVrRectangle,
            Point centerPoint,
            Point mapSize,
            Func<int, WzImage> linkedMapResolver)
        {
            Rectangle viewportRectangle = ResolveClientOwnedViewportRectangle(mapInfo, mapVrRectangle, mapSize, linkedMapResolver);
            Rectangle fieldBoundary = new(
                viewportRectangle.X + centerPoint.X,
                viewportRectangle.Y + centerPoint.Y,
                viewportRectangle.Width,
                viewportRectangle.Height);

            (int side, int top, int bottom) = ResolveClientOwnedSafeArea(mapInfo, linkedMapResolver);
            Rectangle? mirrorBottomRectangle = ResolveClientOwnedMirrorBottomRectangle(mapInfo, viewportRectangle);
            return new ClientOwnedMapBoundaryContract(
                viewportRectangle,
                fieldBoundary,
                side,
                top,
                bottom,
                mirrorBottomRectangle);
        }

        private static Rectangle ResolveClientOwnedViewportRectangle(
            MapInfo mapInfo,
            Rectangle? mapVrRectangle,
            Point mapSize,
            Func<int, WzImage> linkedMapResolver)
        {
            if (TryBuildWeddingPhotoSceneContract(mapInfo, out WeddingPhotoSceneContract contract, linkedMapResolver)
                && contract.HasViewport)
            {
                return new Rectangle(
                    contract.ViewportLeft,
                    contract.ViewportTop,
                    Math.Max(0, contract.ViewportRight - contract.ViewportLeft),
                    Math.Max(0, contract.ViewportBottom - contract.ViewportTop));
            }

            if (mapVrRectangle.HasValue)
            {
                return mapVrRectangle.Value;
            }

            return new Rectangle(0, 0, mapSize.X, mapSize.Y);
        }

        private static (int Side, int Top, int Bottom) ResolveClientOwnedSafeArea(
            MapInfo mapInfo,
            Func<int, WzImage> linkedMapResolver)
        {
            if (TryBuildWeddingPhotoSceneContract(mapInfo, out WeddingPhotoSceneContract contract, linkedMapResolver)
                && contract.HasSafeArea)
            {
                return (contract.Side, contract.Top, contract.Bottom);
            }

            return (
                Math.Max(0, mapInfo?.LBSide ?? 0),
                Math.Max(0, mapInfo?.LBTop ?? 0),
                Math.Max(0, mapInfo?.LBBottom ?? 0));
        }

        private static Rectangle? ResolveClientOwnedMirrorBottomRectangle(MapInfo mapInfo, Rectangle viewportRectangle)
        {
            if (mapInfo?.mirror_Bottom != true || viewportRectangle.Width <= 0 || viewportRectangle.Height <= 0)
            {
                return null;
            }

            const int objectMirrorBottomHeight = 200;
            return new Rectangle(
                viewportRectangle.Left,
                viewportRectangle.Bottom - objectMirrorBottomHeight,
                viewportRectangle.Width,
                objectMirrorBottomHeight);
        }


        /// <summary>
        /// Load game assets
        /// </summary>
        protected override void LoadContent()
        {
            hostCancellation.ThrowIfCancellationRequested();
            LogStartupCheckpoint("LoadContent begin");

            // Load physics constants from Map.wz/Physics.img
            LoadPhysicsConstants();


            WzImage mapHelperImage = runtimeServices.Assets.FindImage("Map", "MapHelper.img");
            WzImage soundUIImage = runtimeServices.Assets.FindImage("Sound", "UI.img");
            WzImage uiToolTipImage = runtimeServices.Assets.FindImage("UI", "UIToolTip.img"); // UI_003.wz
            WzImage uiBasicImage = runtimeServices.Assets.FindImage("UI", "Basic.img");
            WzImage uiLoginImage = runtimeServices.Assets.FindImage("UI", "Login.img");
            WzImage uiWindow1Image = runtimeServices.Assets.FindImage("UI", "UIWindow.img"); //
            WzImage uiWindow2Image = runtimeServices.Assets.FindImage("UI", "UIWindow2.img"); // doesnt exist before big-bang
            WzImage uiMapImage = runtimeServices.Assets.FindImage("UI", "UIMap.img");
            WzImage uiMapleTvImage = runtimeServices.Assets.FindImage("UI", "MapleTV.img");
            WzImage uiGuildBbsImage = runtimeServices.Assets.FindImage("UI", "GuildBBS.img");
            WzImage uiBuffIconImage = runtimeServices.Assets.FindImage("UI", "BuffIcon.img");


            WzImage uiStatusBarImage = runtimeServices.Assets.FindImage("UI", "StatusBar.img");

            WzImage uiStatus2BarImage = runtimeServices.Assets.FindImage("UI", "StatusBar2.img");
            WzImage uiStatus3BarImage = runtimeServices.Assets.FindImage("UI", "StatusBar3.img");



            _gameState.UiFamily = MapSimulatorUiFamilyResolver.ResolveFromStatusBarImages(
                hasStatusBar: uiStatusBarImage != null,
                hasStatusBar2: uiStatus2BarImage != null,
                hasStatusBar3: uiStatus3BarImage != null);
            _gameState.IsBigBangUpdate = _gameState.UiFamily != MapSimulatorUiFamily.LegacyPreBigBang;

            _gameState.IsBigBang2Update = WzFileManager.IsBigBang2Update(uiWindow2Image); // chaos update
            LogStartupCheckpoint("LoadContent resolved shared WZ/UI references");



            // BGM

            _mapBgmName = _mapInfo.bgm;

            ApplyRequestedBgm(_specialFieldBgmOverrideName ?? _mapBgmName);



            // Sound effects from Sound.wz/Game.img - using SoundManager for concurrent playback

            _soundManager = new SoundManager();
            UIObject.ClientSoundEffectPlayer = (key, soundProperty, startVolumeScale, suppressWhileActive) =>
                _soundManager.TryPlayClientSoundEffect(
                    key,
                    soundProperty,
                    startVolumeScale,
                    loop: false,
                    suppressWhileActive: suppressWhileActive,
                    out _,
                    out _);
            ApplyUtilityAudioSettings();
            WzImage soundGameImage = runtimeServices.Assets.FindImage("Sound", "Game.img");
            if (soundGameImage != null)
            {
                // Portal teleport sound
                WzBinaryProperty portalSound = (WzBinaryProperty)soundGameImage["Portal"];
                if (portalSound != null)
                {
                    _portalSoundProperty = portalSound;
                    _soundManager.RegisterSound("Portal", portalSound);
                }


                // Jump sound
                WzBinaryProperty jumpSound = (WzBinaryProperty)soundGameImage["Jump"];
                if (jumpSound != null)
                {
                    _jumpSoundProperty = jumpSound;
                    _soundManager.RegisterSound("Jump", jumpSound);
                }


                // Drop item sound (played on mob death)
                WzBinaryProperty dropItemSound = (WzBinaryProperty)soundGameImage["DropItem"];
                if (dropItemSound != null)
                {
                    _dropItemSoundProperty = dropItemSound;
                    _soundManager.RegisterSound("DropItem", dropItemSound);
                }


                // Pick up item sound
                WzBinaryProperty pickUpItemSound = (WzBinaryProperty)soundGameImage["PickUpItem"];
                if (pickUpItemSound != null)
                {
                    _pickUpItemSoundProperty = pickUpItemSound;
                    _soundManager.RegisterSound("PickUpItem", pickUpItemSound);
                }

                WzBinaryProperty loginEntryGameInSound = (WzBinaryProperty)soundGameImage["GameIn"];
                if (loginEntryGameInSound != null)
                {
                    _loginEntryGameInSoundProperty = loginEntryGameInSound;
                    _soundManager.RegisterSound(LoginEntryGameInSoundKey, loginEntryGameInSound);
                }
            }


            WzImage soundUiImage = runtimeServices.Assets.FindImage("Sound", "UI.img");
            WzBinaryProperty cooldownNoticeSound = soundUiImage?["DlgNotice"] as WzBinaryProperty;
            if (cooldownNoticeSound != null)
            {
                _skillCooldownNoticeSoundProperty = cooldownNoticeSound;
                _soundManager.RegisterSound(SkillCooldownNoticeSoundKey, cooldownNoticeSound);
            }

            // `CWvsContext::OpenBook` and `CloseBook` both request StringPool id `0x924`.
            // The exact localized descriptor is still unresolved, so prefer the short WZ-backed UI cue here.
            WzBinaryProperty bookLifecycleSound = soundUiImage?["WorldmapOpen"] as WzBinaryProperty
                ?? soundUiImage?["WorldmapClose"] as WzBinaryProperty
                ?? soundUiImage?["BtMouseClick"] as WzBinaryProperty;
            if (bookLifecycleSound != null)
            {
                _bookDialogLifecycleSoundProperty = bookLifecycleSound;
                _soundManager.RegisterSound(BookDialogLifecycleSoundKey, bookLifecycleSound);
            }

            string miniGameSoundPrefix = MapleStoryStringPool.GetOrFallback(
                MiniGameSoundPrefixStringPoolId,
                "Sound/MiniGame.img/");
            string miniGameReadySuffix = MapleStoryStringPool.GetOrFallback(
                MemoryGameReadyClickSoundStringPoolId,
                "Ready");
            string miniGameReadyDescriptor = string.Concat(miniGameSoundPrefix, miniGameReadySuffix);
            if (TryResolvePacketOwnedWzSound(
                    miniGameReadyDescriptor,
                    "MiniGame.img",
                    out WzBinaryProperty miniGameReadySound,
                    out _,
                    strictClientSoundFamily: true))
            {
                _memoryGameReadyClickSoundProperty = miniGameReadySound;
                _soundManager.RegisterSound(MemoryGameReadyClickSoundKey, miniGameReadySound);
            }

            string miniGameTimerSuffix = MapleStoryStringPool.GetOrFallback(
                MemoryGameTimerWarningSoundStringPoolId,
                "Timer");
            string miniGameTimerDescriptor = string.Concat(miniGameSoundPrefix, miniGameTimerSuffix);
            if (TryResolvePacketOwnedWzSound(
                    miniGameTimerDescriptor,
                    "MiniGame.img",
                    out WzBinaryProperty miniGameTimerSound,
                    out _,
                    strictClientSoundFamily: true))
            {
                _memoryGameTimerWarningSoundProperty = miniGameTimerSound;
                _soundManager.RegisterSound(MemoryGameTimerWarningSoundKey, miniGameTimerSound);
            }


            // Load meso icons from Item.wz/Special/0900.img

            LoadMesoIcons();



            // Load tombstone animation from Effect.wz/Tomb.img

            LoadTombstoneAnimation();
            LogStartupCheckpoint("LoadContent initialized audio/icons/tombstone assets");



            if (_runtimeMapDefinition.VirtualBounds == null)
            {
                _vrFieldBoundary = new Rectangle(0, 0, _runtimeMapDefinition.MapSize.X, _runtimeMapDefinition.MapSize.Y);
                _vrRectangle = new Rectangle(0, 0, _runtimeMapDefinition.MapSize.X, _runtimeMapDefinition.MapSize.Y);
            }
            else
            {
                _vrFieldBoundary = new Rectangle(
                    _runtimeMapDefinition.VirtualBounds.Value.X + _runtimeMapDefinition.CenterPoint.X,
                    _runtimeMapDefinition.VirtualBounds.Value.Y + _runtimeMapDefinition.CenterPoint.Y,
                    _runtimeMapDefinition.VirtualBounds.Value.Width,
                    _runtimeMapDefinition.VirtualBounds.Value.Height);
                _vrRectangle = new Rectangle(_runtimeMapDefinition.VirtualBounds.Value.X, _runtimeMapDefinition.VirtualBounds.Value.Y, _runtimeMapDefinition.VirtualBounds.Value.Width, _runtimeMapDefinition.VirtualBounds.Value.Height);
            }
            //SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);


            // test benchmark
#if DEBUG
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
#endif


            /////// Background and objects

            ConcurrentBag<WzObject> usedProps = new ConcurrentBag<WzObject>();

            ConcurrentDictionary<BaseDXDrawableItem, QuestGatedMapObjectState> questGatedMapObjects = new();

            

            // Objects
            RunContentStage(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                foreach ((int DrawOrder, RuntimeTileDefinition? Tile, RuntimeObjectDefinition Object) entry in
                    _runtimeMapDefinition.Tiles
                        .Select(tile => (tile.DrawOrder, (RuntimeTileDefinition?)tile, (RuntimeObjectDefinition)null))
                        .Concat(_runtimeMapDefinition.Objects.Select(obj => (obj.DrawOrder, (RuntimeTileDefinition?)null, obj)))
                        .OrderBy(entry => entry.DrawOrder))
                {
                    RuntimeAssetKey asset = entry.Tile?.Asset ?? entry.Object.Asset;
                    WzImageProperty sourceProperty = ResolveRuntimeAssetProperty(asset);
                    int x = entry.Tile?.X ?? entry.Object.X;
                    int y = entry.Tile?.Y ?? entry.Object.Y;
                    int layer = entry.Tile?.Layer ?? entry.Object.Layer;
                    bool flip = entry.Object?.Flip ?? false;
                    BaseDXDrawableItem mapItem = MapSimulatorLoader.CreateMapItemFromProperty(
                        sceneryTexturePool,
                        sourceProperty,
                        x,
                        y,
                        _runtimeMapDefinition.CenterPoint,
                        _DxDeviceManager.GraphicsDevice,
                        usedProps,
                        flip);
                    if (mapItem == null)
                        continue;

                    if (entry.Object != null)
                    {
                        RegisterQuestGatedMapObject(mapItem, entry.Object, sourceProperty, questGatedMapObjects);
                    }
                    mapObjects[layer].Add(mapItem);
                    if (entry.Object != null)
                    {
                        foreach (BaseDXDrawableItem branchItem in CreatePacketOwnedStageTransitionAuthoredStateBranchItems(
                            mapItem, entry.Object, sourceProperty, usedProps, questGatedMapObjects))
                        {
                            mapObjects[layer].Add(branchItem);
                        }
                    }
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Tile task loaded {_runtimeMapDefinition.Tiles.Count + _runtimeMapDefinition.Objects.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            // Background
            RunContentStage(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                foreach (RuntimeBackgroundDefinition background in _runtimeMapDefinition.Backgrounds.OrderBy(item => item.DrawOrder))
                {
                    WzImageProperty bgParent = ResolveRuntimeAssetProperty(background.Asset);
                    BackgroundItem bgItem = MapSimulatorLoader.CreateBackgroundFromProperty(
                        sceneryTexturePool, bgParent, background, _DxDeviceManager.GraphicsDevice, usedProps);
                    if (bgItem != null)
                    {
                        (background.Front ? backgrounds_front : backgrounds_back).Add(bgItem);
                    }
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Background task loaded {_runtimeMapDefinition.Backgrounds.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            // Reactors
            RunContentStage(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                foreach (RuntimeReactorDefinition reactorDefinition in _runtimeMapDefinition.Reactors)
                {
                    RuntimeReactor reactor = CreateRuntimeReactor(reactorDefinition);
                    if (reactor == null)
                        continue;
                    //WzImage imageProperty = (WzImage)NPCWZFile[reactorInfo.ID + ".img"];


                    ReactorItem reactorItem = MapSimulatorLoader.CreateReactorFromProperty(ContentTarget.MapTextures, reactor, runtimeServices.Assets, _DxDeviceManager.GraphicsDevice, usedProps);
                    if (reactorItem != null)
                        mapObjects_Reactors.Add(reactorItem);
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Reactor task loaded {_runtimeMapDefinition.Reactors.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            // NPCs
            RunContentStage(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                int loadedCount = 0;
                foreach (RuntimeLifeDefinition npcDefinition in _runtimeMapDefinition.Npcs)
                {
                    RuntimeLife npc = new RuntimeLife(npcDefinition);
                    //WzImage imageProperty = (WzImage) NPCWZFile[npcInfo.ID + ".img"];
                    if (npc.Hide)
                        continue;


                    NpcItem npcItem = MapSimulatorLoader.CreateNpcFromProperty(
                        ContentTarget.MapTextures,
                        npc,
                        runtimeServices.Assets,
                        UserScreenScaleFactor,
                        _DxDeviceManager.GraphicsDevice,
                        usedProps,
                        _playerManager?.Player?.Build?.Gender,
                        _questRuntime.HasNpcClientActionSelectionContext(),
                        _questRuntime.GetCurrentState,
                        questId => _questRuntime.TryGetQuestRecordValue(questId, out string value) ? value : string.Empty);
                    if (npcItem != null)
                    {
                        mapObjects_NPCs.Add(npcItem);
                        loadedCount++;
                    }
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] NPC task loaded {loadedCount}/{_runtimeMapDefinition.Npcs.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            // Mobs
            RunContentStage(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                int loadedCount = 0;
                foreach (RuntimeLifeDefinition mobDefinition in _runtimeMapDefinition.Mobs)
                {
                    RuntimeLife mob = new RuntimeLife(mobDefinition);
                    if (mob.Hide)
                        continue;


                    MobItem npcItem = MapSimulatorLoader.CreateMobFromProperty(ContentTarget.MapTextures, mob, runtimeServices.Assets, UserScreenScaleFactor, _DxDeviceManager.GraphicsDevice, _soundManager, usedProps);
                    npcItem?.SetAnimationEffects(_animationEffects);
                    ConfigureMobActionSpeechConditionContext(npcItem);
                    ConfigureMobAutoSkillSelection(npcItem);



                    mapObjects_Mobs.Add(npcItem);
                    loadedCount++;
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Mob task loaded {loadedCount}/{_runtimeMapDefinition.Mobs.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });



            // Portals
            RunContentStage(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                int loadedCount = 0;
                WzSubProperty portalParent = (WzSubProperty) mapHelperImage["portal"];


                WzSubProperty gameParent = (WzSubProperty)portalParent["game"];

                //WzSubProperty editorParent = (WzSubProperty) portalParent["editor"];



                foreach (RuntimePortal portal in _runtimePortals)
                {
                    PortalItem portalItem = MapSimulatorLoader.CreatePortalFromProperty(ContentTarget.MapTextures, gameParent, portal, _DxDeviceManager.GraphicsDevice, usedProps);
                    if (portalItem != null)
                    {
                        mapObjects_Portal.Add(portalItem);
                        loadedCount++;
                    }
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Portal task loaded {loadedCount}/{_runtimePortals.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            // Tooltips
            RunContentStage(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                WzSubProperty farmFrameParent = (WzSubProperty) uiToolTipImage?["Item"]?["FarmFrame"]; // not exist before V update.
                foreach (RuntimeTooltipDefinition tooltip in _runtimeMapDefinition.Tooltips)
                {
                    TooltipItem item = MapSimulatorLoader.CreateTooltipFromProperty(ContentTarget.MapTextures, UserScreenScaleFactor, farmFrameParent, tooltip, _DxDeviceManager.GraphicsDevice);


                    mapObjects_tooltips.Add(item);

                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Tooltip task loaded {_runtimeMapDefinition.Tooltips.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });



            // Cursor
            RunContentStage(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                WzImageProperty cursorImageProperty = (WzImageProperty)uiBasicImage["Cursor"];
                this.mouseCursor = MapSimulatorLoader.CreateMouseCursorFromProperty(ContentTarget.MapTextures, cursorImageProperty, 0, 0, _DxDeviceManager.GraphicsDevice, usedProps, false);
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Cursor task finished in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            // Minimap
            RunContentStage(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                if (FieldInteractionRestrictionEvaluator.ShouldCreateStartupMinimap(
                    _gameState.IsLoginMap,
                    _gameState.IsCashShopMap,
                    _mapInfo))
                {
                    using var minimapData = HaCreator.MapSimulator.Assets.RuntimeMinimapFactory.Create(_runtimeMapDefinition, runtimeServices);
                    miniMapUi = MapSimulatorLoader.CreateMinimapFromProperty(uiWindow1Image, uiWindow2Image, uiMapImage, uiBasicImage, minimapData, GraphicsDevice, UserScreenScaleFactor, soundUIImage, _gameState.IsBigBangUpdate, resourceCache: uiResourceCache);
                    miniMapUi?.ReloadMiniMap(_packetOwnedMiniMapOnOffVisible);
                    if (_packetFieldUtilityMinimapHiddenByAdminResult)
                    {
                        miniMapUi?.EnsureCollapsed();
                    }
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Minimap task finished in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            // Statusbar
            RunContentStage(() => {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                if (!_gameState.IsLoginMap && !_gameState.IsCashShopMap) {
                    Tuple<StatusBarUI, StatusBarChatUI> statusBar = MapSimulatorLoader.CreateStatusBarFromProperty(uiStatusBarImage, uiStatus2BarImage, uiStatus3BarImage, uiBasicImage, uiBuffIconImage, GraphicsDevice, UserScreenScaleFactor, _renderParams, soundUIImage, _gameState.IsBigBangUpdate, runtimeServices.Assets, resourceCache: uiResourceCache);
                    if (statusBar != null) {
                        statusBarUi = statusBar.Item1;
                        statusBarChatUI = statusBar.Item2;
                    }
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Status bar task finished in {taskStopwatch.ElapsedMilliseconds} ms");
            });





            // UI windows touch GraphicsDevice-backed resources and must be created on the main thread.
            if (!_gameState.IsCashShopMap)
            {
                if (_gameState.IsLoginMap)
                {
                    uiWindowManager ??= new UIWindowManager();
                    UIWindowLoader.RegisterLoginEntryWindows(
                        uiWindowManager,
                        uiLoginImage,
                        uiWindow1Image,
                        uiWindow2Image,
                        uiBasicImage,
                        soundUIImage,
                        GraphicsDevice,
                        _renderParams.RenderWidth,
                        _renderParams.RenderHeight,
                        runtimeServices.Assets);


                    UIWindowLoader.RegisterLoginCreateCharacterWindow(
                        uiWindowManager,
                        uiLoginImage,
                        soundUIImage,
                        GraphicsDevice,
                        _renderParams.RenderWidth,
                        _renderParams.RenderHeight);
                    UIWindowLoader.RegisterLoginCharacterDetailWindow(
                        uiWindowManager,
                        uiBasicImage,
                        soundUIImage,
                        GraphicsDevice,
                        _renderParams.RenderWidth,
                        _renderParams.RenderHeight,
                        runtimeServices.Assets);
                    UIWindowLoader.RegisterConnectionNoticeWindow(
                        uiWindowManager,
                        uiLoginImage,
                        uiBasicImage,
                        soundUIImage,
                        GraphicsDevice,
                        _renderParams.RenderWidth,
                        _renderParams.RenderHeight);
                    UIWindowLoader.RegisterLoginUtilityDialogWindow(
                        uiWindowManager,
                        uiWindow2Image,
                        uiLoginImage,
                        uiBasicImage,
                        soundUIImage,
                        GraphicsDevice,
                        _renderParams.RenderWidth,
                        _renderParams.RenderHeight,
                        runtimeServices.Assets);
                }
                else
                {
                    Stopwatch uiWindowCreationStopwatch = Stopwatch.StartNew();
                    uiWindowManager = UIWindowLoader.CreateUIWindowManager(
                        uiWindow1Image, uiWindow2Image, uiBasicImage, soundUIImage,
                        null, null, uiMapleTvImage,
                        GraphicsDevice, _renderParams.RenderWidth, _renderParams.RenderHeight, _gameState.IsBigBangUpdate, runtimeServices: runtimeServices, resourceCache: uiResourceCache, storageAccountLabel: BuildStorageAccountLabel(), storageAccountKey: BuildStorageAccountKey(), profileStorage: sessionOptions.ProfileStorage);
                    uiWindowCreationStopwatch.Stop();
                    Debug.WriteLine($"[MapLoad] UIWindowLoader.CreateUIWindowManager finished in {uiWindowCreationStopwatch.ElapsedMilliseconds} ms");

                    uiWindowManager.RegisterLazyWindow(
                        MapSimulatorWindowNames.GuildBbs,
                        manager => UIWindowLoader.RegisterGuildBbsWindow(
                            manager,
                            uiGuildBbsImage,
                            uiBasicImage,
                            soundUIImage,
                            GraphicsDevice,
                            new Point(
                                Math.Max(24, (_renderParams.RenderWidth / 2) - 367),
                                Math.Max(24, (_renderParams.RenderHeight / 2) - 263))));
                }
            }


            ReplaceQuestGatedMapObjects(questGatedMapObjects);



            RegisterStatusBarPopupUtilityWindows(uiStatus2BarImage, uiBasicImage, soundUIImage);
            UIWindowLoader.RegisterInGameConfirmDialogWindow(
                uiWindowManager,
                uiWindow2Image,
                soundUIImage,
                GraphicsDevice,
                _renderParams.RenderWidth,
                _renderParams.RenderHeight,
                runtimeServices.Assets);
            RegisterPacketOwnedAntiMacroWindows();
            RegisterPacketOwnedLogoutGiftWindow();


            // Set fonts on UI windows after all tasks complete
            uiWindowManager?.SetFonts(_fontChat);
            WireLoginTitleWindow();
            WireWorldChannelSelectorWindows();
            WireRecommendWorldWindow();
            WireQuestLogWindowData();
            WireQuestRewardRaiseWindow();
            WireMemoMailboxWindowData();
            WireFamilyChartWindowData();
            WireSocialListWindowData();
            WireSocialSearchWindowData();
            WireGuildSearchWindowData();
            WireGuildSkillWindowData();
            WireGuildBbsWindowData();
            _engagementProposalController.SocialMessagesObserved = TryTriggerSpecialistPetSocialFeedback;
            _engagementProposalController.ClientPacketDispatcher = DispatchEngagementProposalClientRequest;
            _engagementProposalController.WireWindow(uiWindowManager, _playerManager?.Player?.Build, _fontChat, ShowUtilityFeedbackMessage);
            _weddingWishListController.SocialChatObserved = TryTriggerSpecialistPetSocialFeedback;
            _weddingWishListController.ClientPacketDispatcher = DispatchWeddingWishListClientRequest;
            _weddingInvitationController.SocialMessagesObserved = TryTriggerSpecialistPetSocialFeedback;
            _weddingInvitationController.WireWindow(uiWindowManager, _playerManager?.Player?.Build, _fontChat, ShowUtilityFeedbackMessage);
            _weddingWishListController.WireWindow(uiWindowManager, _playerManager?.Player?.Build, uiWindowManager?.InventoryWindow as IInventoryRuntime, _fontChat, ShowUtilityFeedbackMessage);
            WireProgressionUtilityWindowLaunchers();
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemMaker) is ItemMakerUI itemMakerWindow)
            {
                itemMakerWindow.SetItemIconProvider(LoadInventoryItemIcon);
                itemMakerWindow.CraftCompleted -= HandleItemMakerCraftCompleted;
                itemMakerWindow.RecipesDiscovered -= HandleItemMakerRecipesDiscovered;
                itemMakerWindow.HiddenRecipesUnlocked -= HandleItemMakerHiddenRecipesUnlocked;
                itemMakerWindow.CraftCompleted += HandleItemMakerCraftCompleted;

                itemMakerWindow.RecipesDiscovered += HandleItemMakerRecipesDiscovered;

                itemMakerWindow.HiddenRecipesUnlocked += HandleItemMakerHiddenRecipesUnlocked;
                ConfigureItemMakerWindow(itemMakerWindow);
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is AdminShopDialogUI cashShopWindowReload)
            {
                cashShopWindowReload.SetInventory(uiWindowManager.InventoryWindow as IInventoryRuntime);
                cashShopWindowReload.SetCashBalances(_loginAccountCashShopNxCredit);
                cashShopWindowReload.TryConsumeCashBalance = TryConsumeLoginAccountCashShopNxCredit;
                cashShopWindowReload.ResolveStorageExpansionCommoditySerialNumber = ResolveStorageExpansionCommoditySerialNumber;
                cashShopWindowReload.GetStorageExpansionStatusSummary = GetStorageExpansionStatusSummary;
                cashShopWindowReload.StorageExpansionResolved = HandleStorageExpansionResolved;
                cashShopWindowReload.WindowHidden = _ => HideCashShopOwnerFamilyWindows();
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashAvatarPreview) is CashAvatarPreviewWindow cashAvatarPreviewReload
                && _playerManager?.Player?.Build != null)
            {
                cashAvatarPreviewReload.CharacterBuild = _playerManager.Player.Build;
                cashAvatarPreviewReload.SetFont(_fontChat);
                cashAvatarPreviewReload.EquipmentLoader = _playerManager.Loader != null ? _playerManager.Loader.LoadEquipment : null;
                cashAvatarPreviewReload.ClientCancelIngressRequested =
                    () => ReleaseActiveKeydownSkillForClientCancelIngress(currTickCount);
                cashAvatarPreviewReload.PersonalShopRequested = ShowCashAvatarPersonalShopAction;
                cashAvatarPreviewReload.EntrustedShopRequested = ShowCashAvatarEntrustedShopAction;
                cashAvatarPreviewReload.TradingRoomRequested = ShowCashAvatarTradingRoomAction;
                cashAvatarPreviewReload.WeatherRequested = PreviewCashAvatarWeatherAction;
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Mts) is AdminShopDialogUI mtsWindowReload)
            {
                mtsWindowReload.SetInventory(uiWindowManager.InventoryWindow as IInventoryRuntime);
            }
            RefreshMapTransferWindow();
            RefreshWorldMapWindow();


            // Initialize mob foothold references after all mobs are loaded

            InitializeMobFootholds();



            // Convert lists to arrays for faster iteration

            ConvertListsToArrays();



#if DEBUG
            // test benchmark
            watch.Stop();
            Debug.WriteLine($"Map WZ files loaded. Execution Time: {watch.ElapsedMilliseconds} ms");
#endif
            //
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _specialFieldRuntime.Initialize(
                _DxDeviceManager.GraphicsDevice,
                _soundManager,
                RequestSpecialFieldBgmOverride,
                ClearSpecialFieldBgmOverride,
                BuildAriantArenaRemoteCharacter,
                BuildAriantArenaRemoteCharacter,
                _playerManager?.Loader,
                runtimeServices,
                new UILoaderResourceCache(),
                ownCreatedGraphicsResources: true);
            _specialFieldRuntime.Minigames.MemoryGame.SetReadyClickSoundCallback(PlayMemoryGameReadyClickSE);
            _specialFieldRuntime.Minigames.MemoryGame.SetTimerWarningSoundCallback(PlayMemoryGameTimerWarningSE);

            ///////////////////////////////////////////////
            ////// Default positioning for character //////
            ///////////////////////////////////////////////
            ResolveSpawnPosition(out float spawnX, out float spawnY);

            ClampLegacyCameraToBoundaries();



            ///////////////////////////////////////////////
            ///////// Initialize Player Manager ///////////
            ///////////////////////////////////////////////
            // Store map center point for camera calculations
            _mapCenterX = _runtimeMapDefinition.CenterPoint.X;
            _mapCenterY = _runtimeMapDefinition.CenterPoint.Y;
            // Spawn at portal spawn point (spawnX, spawnY set above from StartPoint portal)
            ResetLoginRuntimeForCurrentMap(currTickCount);
            InitializeAuthoredDynamicObjectTagStates();
            bool runOnFirstUserEnterScript = ShouldRunOnFirstUserEnterForCurrentMap();
            ApplyEntryScriptDynamicObjectTagStates(currTickCount, runOnFirstUserEnterScript);
            InitializeDynamicObjectDirectionEventTriggers();



            Stopwatch initializePlayerManagerStopwatch = Stopwatch.StartNew();
            InitializePlayerManager(spawnX, spawnY);
            initializePlayerManagerStopwatch.Stop();
            Debug.WriteLine($"[Startup] InitializePlayerManager finished in {initializePlayerManagerStopwatch.ElapsedMilliseconds} ms");

            if (!_gameState.IsLoginMap)

            {

                InitializeFieldRuleRuntime(currTickCount, runOnFirstUserEnterScript);
            }
            else
            {
                _gameState.PlayerControlEnabled = false;
                InitializeLoginCharacterRoster();
            }
            SetCookieHouseContextPoint(0);
            BindRemoteAffectedAreaPacketField();
            BindRemoteDropPacketField();
            _specialFieldRuntime.BindMap(_runtimeMapDefinition);
            ApplyClientOwnedFieldWrappers();
            _packetFieldStateRuntime.Initialize(GraphicsDevice, _mapInfo);
            RestorePacketOwnedFieldPropertyClockFromMap();
            BindPacketOwnedStageTransitionMapState();
            BindPacketOwnedReactorPoolMapState();
            SyncWeddingPacketInboxState();
            SyncSnowBallPacketInboxState();
            SyncCoconutPacketInboxState();
            SyncMemoryGamePacketInboxState();
            SyncAriantArenaPacketInboxState();
            SyncMonsterCarnivalPacketInboxState();
            SyncMassacrePacketInboxState();
            SyncDojoPacketInboxState();
            SyncTransportPacketInboxState();
            SyncGuildBossTransportState();
            SyncPartyRaidPacketInboxState();
            SyncTournamentPacketInboxState();
            SyncRockPaperScissorsPacketInboxState();

            SyncCookieHousePointInboxState();

            SyncBattlefieldLocalAppearance();
            _remoteUserPool.SyncBattlefieldAppearance(_specialFieldRuntime.SpecialEffects.Battlefield);


            // Initialize camera controller
            _cameraController.Initialize(
                _vrFieldBoundary,
                _renderParams.RenderWidth,
                _renderParams.RenderHeight,
                _mapCenterX,
                _mapCenterY,
                _renderParams.RenderObjectScaling);
            _cameraController.SetPosition(spawnX, spawnY);
            ///////////////////////////////////////////////


            ///////////////////////////////////////////////
            ///////////// Border //////////////////////////
            ///////////////////////////////////////////////
            int leftRightVRDifference = (int)((_vrFieldBoundary.Right - _vrFieldBoundary.Left) * _renderParams.RenderObjectScaling);
            if (leftRightVRDifference < _renderParams.RenderWidth) // viewing range is smaller than the render width.. 
            {
                this._drawVRBorderLeftRight = true; // flag


                this._vrBoundaryTextureLeft = CreateVRBorder(VR_BORDER_WIDTHHEIGHT, _vrFieldBoundary.Height, _DxDeviceManager.GraphicsDevice);
                this._vrBoundaryTextureRight = CreateVRBorder(VR_BORDER_WIDTHHEIGHT, _vrFieldBoundary.Height, _DxDeviceManager.GraphicsDevice);
                this._vrBoundaryTextureTop = CreateVRBorder(_vrFieldBoundary.Width * 2, VR_BORDER_WIDTHHEIGHT, _DxDeviceManager.GraphicsDevice);
                this._vrBoundaryTextureBottom = CreateVRBorder(_vrFieldBoundary.Width * 2, VR_BORDER_WIDTHHEIGHT, _DxDeviceManager.GraphicsDevice);
            }
            // LB Border
            if (_mapInfo.LBSide != null)
            {
                _lbSide = (int)_mapInfo.LBSide;
                this._lbTextureLeft = CreateLBBorder(LB_BORDER_WIDTHHEIGHT + _lbSide, this.Height, _DxDeviceManager.GraphicsDevice);
                this._lbTextureRight = CreateLBBorder(LB_BORDER_WIDTHHEIGHT + _lbSide, this.Height, _DxDeviceManager.GraphicsDevice);
            }
            if (_mapInfo.LBTop != null)
            {
                _lbTop = (int)_mapInfo.LBTop;
                this._lbTextureTop = CreateLBBorder((int) (_vrFieldBoundary.Width * 1.45), LB_BORDER_WIDTHHEIGHT + _lbTop, _DxDeviceManager.GraphicsDevice); // add a little more width to the top LB border for very small maps
            }
            if (_mapInfo.LBBottom != null)
            {
                _lbBottom = (int)_mapInfo.LBBottom;
                this._lbTextureBottom = CreateLBBorder((int) (_vrFieldBoundary.Width * 1.45), LB_BORDER_WIDTHHEIGHT + _lbBottom, _DxDeviceManager.GraphicsDevice);
            }


            // Set border data on RenderingManager

            _renderingManager.SetVRBorderData(_vrFieldBoundary, _drawVRBorderLeftRight, _vrBoundaryTextureLeft, _vrBoundaryTextureRight);

            _renderingManager.SetLBBorderData(_lbTextureLeft, _lbTextureRight);



            ///////////////////////////////////////////////



            // mirror bottom boundaries
            //_mirrorBottomRect
            if (_mapInfo.mirror_Bottom)
            {
                if (_mapInfo.VRLeft != null && _mapInfo.VRRight != null)
                {
                    int vr_width = (int)_mapInfo.VRRight - (int)_mapInfo.VRLeft;
                    const int obj_mirrorBottom_height = 200;


                    _mirrorBottomRect = new Rectangle((int)_mapInfo.VRLeft, (int)_mapInfo.VRBottom - obj_mirrorBottom_height, vr_width, obj_mirrorBottom_height);



                    _mirrorBottomReflection = new ReflectionDrawableBoundary(128, 255, "mirror", true, false);
                }
            }
            /*
            DXObject leftDXVRObject = new DXObject(
                _vrFieldBoundary.Left - VR_BORDER_WIDTHHEIGHT,
                _vrFieldBoundary.Top,
                _vrBoundaryTextureLeft);
            this.leftVRBorderDrawableItem = new BaseDXDrawableItem(leftDXVRObject, false);
            //new BackgroundItem(int cx, int cy, int rx, int ry, BackgroundType.Regular, 255, true, leftDXVRObject, false, (int) RenderResolution.Res_All);


            // Right VR
            DXObject rightDXVRObject = new DXObject(
                _vrFieldBoundary.Right,
                _vrFieldBoundary.Top,
                _vrBoundaryTextureRight);
            this.rightVRBorderDrawableItem = new BaseDXDrawableItem(rightDXVRObject, false);
            */
            ///////////// End Border


            // Debug items
            System.Drawing.Bitmap bitmap_debug = new System.Drawing.Bitmap(1, 1);
            bitmap_debug.SetPixel(0, 0, System.Drawing.Color.White);
            _debugBoundaryTexture = bitmap_debug.ToTexture2D(_DxDeviceManager.GraphicsDevice);


            // Initialize chat system
            _chat.Initialize(_fontChat, _debugBoundaryTexture, Height);
            _npcInteractionOverlay = new NpcInteractionOverlay(GraphicsDevice, runtimeServices.Assets);
            _npcInteractionOverlay.SetFont(_fontChat);
            RegisterChatCommands();
            RegisterChatBalloonPresentationCommand();
            RegisterRemoteUserChatCommand();
            RegisterSummonedPacketChatCommand();
            RegisterMobAttackPacketChatCommand();
            RegisterReactorPoolPacketChatCommand();
            RegisterAnimationDisplayerChatCommand();


            // Initialize pickup notice UI (bottom right corner messages)

            _pickupNoticeUI.Initialize(_fontChat, _debugBoundaryTexture, Width, Height);

            Stopwatch startupOverlayAssetStopwatch = Stopwatch.StartNew();
            _skillCooldownNoticeUI.Initialize(_fontChat, _debugBoundaryTexture, Width, Height);
            LoadSkillCooldownNoticeUiFrame();
            _packetOwnedHudNoticeUI.Initialize(_fontChat, _debugBoundaryTexture, Width, Height);
            LoadPacketOwnedHudNoticeUiFrame();
            LoadPacketOwnedLocalOverlayAssets();
            LoadPacketOwnedComboAssets();
            LoadPacketOwnedTutorAssets();
            startupOverlayAssetStopwatch.Stop();
            Debug.WriteLine($"[Startup] Overlay/HUD asset initialization finished in {startupOverlayAssetStopwatch.ElapsedMilliseconds} ms");


            _temporaryPortalField = new TemporaryPortalField(ContentTarget.MapTextures, _DxDeviceManager.GraphicsDevice, runtimeServices);



            // Initialize combat effects (damage numbers, hit effects)

            _combatEffects.Initialize(_DxDeviceManager.GraphicsDevice, _fontDebugValues);
            _combatEffects.SetAnimationEffects(_animationEffects);
            _combatEffects.SetAnimationDisplayerSpecialTextSink(HandleAnimationDisplayerCombatFeedbackRequested);



            // Load damage number sprites from Effect.wz/BasicEff.img
            // This enables authentic MapleStory digit sprites for damage numbers
            var basicEffImage = runtimeServices.Assets.FindImage("Effect", "BasicEff.img");
            Stopwatch combatEffectAssetStopwatch = Stopwatch.StartNew();
            if (basicEffImage != null)
            {
                _combatEffects.LoadDamageNumbersFromWz(basicEffImage);
            }
            combatEffectAssetStopwatch.Stop();
            Debug.WriteLine($"[Startup] Combat effect asset initialization finished in {combatEffectAssetStopwatch.ElapsedMilliseconds} ms");


            // Initialize status bar character stats display
            // Positions derived from IDA Pro analysis of CUIStatusBar::SetNumberValue and CUIStatusBar::SetStatusValue
            if (statusBarUi != null)
            {
                    _playerManager?.Skills?.ConfigureBuffIconCatalog(UILoader.LoadBuffIconCatalogEntries(uiBuffIconImage, uiResourceCache));
                statusBarUi.SetCharacterStatsProvider(_fontChat, GetCharacterStatsData);
                statusBarUi.SetBuffStatusProvider(GetStatusBarBuffData);
                statusBarUi.SetCooldownStatusProvider(GetStatusBarCooldownData);
                statusBarUi.SetOffBarCooldownStatusProvider(GetStatusBarOffBarCooldownData);
                statusBarUi.SetPreparedSkillProvider(currentTime => GetPreparedSkillBarData(currentTime, PreparedSkillHudSurface.StatusBar));
                statusBarUi.SetPreparedSkillOverlayProvider(currentTime => GetPreparedSkillBarData(currentTime, PreparedSkillHudSurface.World));
                statusBarUi.SetPixelTexture(_DxDeviceManager.GraphicsDevice);
                statusBarUi.SetLowResourceWarningThresholds(_statusBarHpWarningThresholdPercent, _statusBarMpWarningThresholdPercent);
                statusBarUi.BuffCancelRequested = skillId =>
                {
                    RequestStatusBarBuffCancelForClientCancelIngress(skillId, currTickCount);
                };
            }
            ConfigureStatusBarChatUi();


            // Initialize Ability/Stat window with player's CharacterBuild
            // This connects the stat window to the player's actual stats (STR, DEX, INT, LUK, etc.)
            if (uiWindowManager?.AbilityWindow != null && _playerManager?.Player?.Build != null)
            {
                uiWindowManager.AbilityWindow.CharacterBuild = _playerManager.Player.Build;
                uiWindowManager.AbilityWindow.SetFont(_fontDebugValues);
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CharacterInfo) != null && _playerManager?.Player?.Build != null)
            {
                UIWindowBase characterInfoWindow = uiWindowManager.GetWindow(MapSimulatorWindowNames.CharacterInfo);
                characterInfoWindow.CharacterBuild = _playerManager.Player.Build;
                characterInfoWindow.SetFont(_fontDebugValues);
                if (characterInfoWindow is UserInfoUI userInfoWindow)
                {
                    userInfoWindow.SetPetController(_playerManager.Pets);
                    userInfoWindow.SetCollectionSnapshotProvider(ResolveCharacterInfoItemMakerProgressionSnapshot);
                    userInfoWindow.SetMonsterBookSnapshotProvider(ResolveCharacterInfoMonsterBookSnapshot);
                    userInfoWindow.SetRankDeltaProvider(ResolveCharacterInfoRankDeltaSnapshot);
                    userInfoWindow.SetRemoteRideSnapshotProvider(ResolveCharacterInfoRemoteRideSnapshot);

                    WireCharacterInfoWindowActionRoutes(userInfoWindow);
                }

            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.BookCollection) is BookCollectionWindow bookCollectionWindow)
            {
                WireBookCollectionWindowData(bookCollectionWindow, useCollectionLayout: false);
            }

            if (uiWindowManager?.EquipWindow != null && _playerManager?.Player?.Build != null)
            {
                uiWindowManager.EquipWindow.CharacterBuild = _playerManager.Player.Build;
                uiWindowManager.EquipWindow.SetFont(_fontChat);
                if (uiWindowManager.EquipWindow is EquipUI equipWindow)
                {
                    equipWindow.SetPetController(_playerManager.Pets);
                    equipWindow.SetDragonEquipmentController(_playerManager.CompanionEquipment?.Dragon);
                    equipWindow.ItemUpgradeRequested = OpenItemUpgradeWindowForEquipmentWithClientCancelIngress;
                    equipWindow.EquipmentChangeSubmitted = SubmitEquipmentChangeRequest;
                    equipWindow.EquipmentChangeResultRequested = TryResolveEquipmentChangeRequest;
                    equipWindow.EquipmentDragStartBlocked = ShouldBlockEquipmentDragStart;
                }
                if (uiWindowManager.EquipWindow is EquipUIBigBang equipBigBang)
                {
                    equipBigBang.SetCharacterLoader(_playerManager.Loader);
                    equipBigBang.ItemUpgradeRequested = OpenItemUpgradeWindowForEquipmentWithClientCancelIngress;
                    equipBigBang.EquipmentChangeSubmitted = SubmitEquipmentChangeRequest;
                    equipBigBang.EquipmentChangeResultRequested = TryResolveEquipmentChangeRequest;
                    equipBigBang.EquipmentDragStartBlocked = ShouldBlockEquipmentDragStart;
                    equipBigBang.CompanionDragCommitIngressRequested =
                        () => ReleaseActiveKeydownSkillForClientCancelIngress(currTickCount);
                    equipBigBang.SetPetController(_playerManager.Pets);

                    equipBigBang.SetPetEquipmentController(_playerManager.CompanionEquipment?.Pet);

                    equipBigBang.SetDragonEquipmentController(_playerManager.CompanionEquipment?.Dragon);

                    equipBigBang.SetMechanicEquipmentController(_playerManager.CompanionEquipment?.Mechanic);
                    equipBigBang.SetMechanicPaneAvailable(
                        CompanionEquipmentController.HasMechanicOwnerState(_playerManager?.Player?.Build)
                        && FieldInteractionRestrictionEvaluator.CanUseTamingMob(_mapInfo?.fieldLimit ?? 0));
                    equipBigBang.SetAndroidEquipmentController(_playerManager.CompanionEquipment?.Android);
                    equipBigBang.SetAndroidPaneAvailable(FieldInteractionRestrictionEvaluator.CanUseAndroid(
                        _mapInfo?.fieldLimit ?? 0,
                        _mapInfo));
                }
            }
            if (uiWindowManager?.InventoryWindow is InventoryUI inventoryWindow && _playerManager?.Player?.Build != null)
            {
                inventoryWindow.CharacterBuild = _playerManager.Player.Build;
                inventoryWindow.SetFont(_fontChat);
                inventoryWindow.SetCharacterLoader(_playerManager.Loader);
                inventoryWindow.ItemUpgradeRequested = OpenItemUpgradeWindowForConsumableWithClientCancelIngress;
                inventoryWindow.ItemUseRequested = TryUseInventoryItem;
                inventoryWindow.ItemUseRequestedAtSlot = TryUseInventoryItemAtSlot;
                inventoryWindow.InventoryDropRequested = HandleLocalInventoryDropRequestWithClientCancelIngress;
                inventoryWindow.MesoDropRequested = HandleLocalMesoDropRequestWithClientCancelIngress;
                inventoryWindow.EquipmentDragStartBlocked = ShouldBlockEquipmentDragStart;
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Trunk) is TrunkUI trunkWindow)
            {
                WireTrunkSecurityWindow();
                if (_playerManager?.Player?.Build != null)
                {
                    trunkWindow.SetFont(_fontChat);
                    trunkWindow.CharacterBuild = _playerManager.Player.Build;
                    trunkWindow.SetCharacterLoader(_playerManager.Loader);
                }
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) is ItemUpgradeUI itemUpgradeWindow && _playerManager?.Player?.Build != null)
            {
                itemUpgradeWindow.CharacterBuild = _playerManager.Player.Build;
                itemUpgradeWindow.SetFont(_fontChat);
                itemUpgradeWindow.SetInventory(uiWindowManager.InventoryWindow as IInventoryRuntime);
                WireItemUpgradeOwnerCallbacks(itemUpgradeWindow);
                if (uiWindowManager.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI vegaSpellWindow)
                {
                    vegaSpellWindow.CharacterBuild = _playerManager.Player.Build;
                    vegaSpellWindow.SetFont(_fontChat);
                    vegaSpellWindow.SetItemUpgradeBackend(itemUpgradeWindow);
                    WireVegaSpellWindowOwnerCallbacks(vegaSpellWindow);
                }
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.RepairDurability) is RepairDurabilityWindow repairDurabilityWindow && _playerManager?.Player?.Build != null)
            {
                repairDurabilityWindow.CharacterBuild = _playerManager.Player.Build;
                repairDurabilityWindow.SetFont(_fontChat);
                repairDurabilityWindow.SetInventory(uiWindowManager.InventoryWindow as IInventoryRuntime);
                repairDurabilityWindow.RepairRequested -= HandleRepairDurabilityRequested;
                repairDurabilityWindow.RepairRequested += HandleRepairDurabilityRequested;
                repairDurabilityWindow.RepairAllRequested -= HandleRepairDurabilityAllRequested;
                repairDurabilityWindow.RepairAllRequested += HandleRepairDurabilityAllRequested;
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is AdminShopDialogUI cashShopWindowRebuild)
            {

                cashShopWindowRebuild.SetInventory(uiWindowManager.InventoryWindow as IInventoryRuntime);
                cashShopWindowRebuild.SetCashBalances(_loginAccountCashShopNxCredit);
                cashShopWindowRebuild.TryConsumeCashBalance = TryConsumeLoginAccountCashShopNxCredit;
                cashShopWindowRebuild.ResolveStorageExpansionCommoditySerialNumber = ResolveStorageExpansionCommoditySerialNumber;
                cashShopWindowRebuild.GetStorageExpansionStatusSummary = GetStorageExpansionStatusSummary;
                cashShopWindowRebuild.StorageExpansionResolved = HandleStorageExpansionResolved;
                cashShopWindowRebuild.WindowHidden = _ => HideCashShopOwnerFamilyWindows();
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashAvatarPreview) is CashAvatarPreviewWindow cashAvatarPreviewRebuild
                && _playerManager?.Player?.Build != null)
            {
                cashAvatarPreviewRebuild.CharacterBuild = _playerManager.Player.Build;
                cashAvatarPreviewRebuild.SetFont(_fontChat);
                cashAvatarPreviewRebuild.EquipmentLoader = _playerManager.Loader != null ? _playerManager.Loader.LoadEquipment : null;
                cashAvatarPreviewRebuild.ClientCancelIngressRequested =
                    () => ReleaseActiveKeydownSkillForClientCancelIngress(currTickCount);
                cashAvatarPreviewRebuild.PersonalShopRequested = ShowCashAvatarPersonalShopAction;
                cashAvatarPreviewRebuild.EntrustedShopRequested = ShowCashAvatarEntrustedShopAction;
                cashAvatarPreviewRebuild.TradingRoomRequested = ShowCashAvatarTradingRoomAction;
                cashAvatarPreviewRebuild.WeatherRequested = PreviewCashAvatarWeatherAction;
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Mts) is AdminShopDialogUI mtsWindowRebuild)
            {
                mtsWindowRebuild.SetInventory(uiWindowManager.InventoryWindow as IInventoryRuntime);
            }
            if (uiWindowManager?.SkillWindow != null)
            {
                uiWindowManager.SkillWindow.SetFont(_fontChat);
            }
            if (uiWindowManager?.SkillWindow != null)
            {
                uiWindowManager.SkillWindow.SetFont(_fontChat);
            }


            // Start fade-in effect on initial map load (matching CField::Init behavior)
            // This creates the classic MapleStory "fade in from black" effect when entering a map
            _portalFadeState = PortalFadeState.FadingIn;
            _screenEffects.FadeIn(PORTAL_FADE_DURATION_MS, Environment.TickCount);


            // cleanup
            // clear used items
            foreach (WzSpineObject spineObj in ContentTarget.MapTextures.TakeNewSpineObjects().Concat(sceneryTexturePool.TakeNewSpineObjects()))
            {
                EnsureSpineRenderer();
                spineObj.state.Start += Start;
                spineObj.state.End += End;
                spineObj.state.Complete += Complete;
                spineObj.state.Event += Event;
            }
        }




        /// <summary>
        /// Unloads current map content for seamless map transitions.
        /// Does not dispose shared resources (GraphicsDevice, SpriteBatch, fonts, cursor).
        /// Audio is handled separately in LoadMapContent to allow BGM continuity.
        /// </summary>
        private void UnloadMapContent()
        {
            // Note: Audio is NOT disposed here - handled in LoadMapContent to allow same BGM to continue playing


            // Clear object lists
            mapObjects_NPCs.Clear();
            mapObjects_Mobs.Clear();
            mapObjects_Reactors.Clear();
            mapObjects_Portal.Clear();
            mapObjects_tooltips.Clear();
            backgrounds_front.Clear();
            backgrounds_back.Clear();


            // Clear layer objects
            if (mapObjects != null)
            {
                for (int i = 0; i < mapObjects.Length; i++)
                {
                    mapObjects[i]?.Clear();
                }
            }


            // Clear mob pool

            _mobPool?.Clear();



            // Clear drop pool

            _dropPool?.Clear();



            // Clear portal pool

            _portalPool?.Clear();



            // Clear reactor pool

            _reactorPool?.Clear();

            _mobAttackSystem.Clear();



            // Clear combat effects (only map-specific effects like mob HP bars)

            _combatEffects?.ClearMapState();

            _fieldEffects?.ResetAllEffects();

            _specialFieldRuntime.Reset();
            _remoteUserPool.Clear();
            _summonedPool.Clear();


            // Prepare player manager for map change (preserves character, caches, skill levels)

            _playerManager?.PrepareForMapChange();

            _passengerSync.Clear();

            _escortFollow.Clear();
            _localFollowRuntime.Clear();
            HidePacketOwnedFollowCharacterPrompt();
            ClearPacketOwnedPassiveMoveState();
            _fieldRuleRuntime = null;
            _lastFieldRestrictionMessageTime = int.MinValue;
            _lastFieldRestrictionMessage = null;
            _lastSkillCooldownBlockedMessageTimes.Clear();
            _skillCooldownNoticeUI.Clear();


            // Clear arrays
            _mapObjectsArray = null;
            _questGatedMapObjects.Clear();
            _authoredDynamicObjectTagStates.Clear();
            _npcsArray = null;
            _lastNpcClientActionSelectionContextStamp = int.MinValue;
            _npcsById.Clear();
            _mobsArray = null;
            _reactorsArray = null;
            _portalsArray = null;
            _tooltipsArray = null;
            _dynamicObjectDirectionEventTriggers.Clear();
            _triggeredDynamicObjectDirectionEventIndices.Clear();
            _scheduledDynamicObjectScriptPublications.Clear();
            _frameActiveMobs.Clear();
            _frameMovableMobs.Clear();
            _framePrimaryBossMob = null;
            _backgroundsFrontArray = null;
            _backgroundsBackArray = null;


            // Clear spatial grids
            _mapObjectsGrid = null;
            _portalsGrid = null;
            _reactorsGrid = null;
            _visibleMapObjects = null;
            _visiblePortals = null;
            _visibleReactors = null;
            _visibleTooltips = null;
            _reactorVisibilityBuffer = null;
            _useSpatialPartitioning = false;


            // Dispose VR border textures
            _vrBoundaryTextureLeft?.Dispose();
            _vrBoundaryTextureRight?.Dispose();
            _vrBoundaryTextureTop?.Dispose();
            _vrBoundaryTextureBottom?.Dispose();
            _vrBoundaryTextureLeft = null;
            _vrBoundaryTextureRight = null;
            _vrBoundaryTextureTop = null;
            _vrBoundaryTextureBottom = null;
            _drawVRBorderLeftRight = false;


            // Dispose LB border textures
            _lbTextureLeft?.Dispose();
            _lbTextureRight?.Dispose();
            _lbTextureTop?.Dispose();
            _lbTextureBottom?.Dispose();
            _lbTextureLeft = null;
            _lbTextureRight = null;
            _lbTextureTop = null;
            _lbTextureBottom = null;
            _lbSide = 0;
            _lbTop = 0;
            _lbBottom = 0;


            // Clear map-scoped UI so the next field rebuild starts from a clean state.
            _minimapTooltipResourcesOwner = null;
            miniMapUi = null;
            statusBarUi = null;
            statusBarChatUI = null;
            mouseCursor = null;


            // Clear mirror boundaries

            _mirrorBottomRect = new Rectangle();

            _mirrorBottomReflection = null;



            // All scenery lists, grids and quest-gated references are now cleared.
            // Character, mob and shared UI resources belong to the session pool.
            sceneryTexturePool.DisposeAll();

            // Reset portal click tracking
            _lastClickedPortal = null;
            _lastClickedHiddenPortal = null;
            _lastClickTime = 0;


            // Reset same-map teleport state
            _sameMapTeleportPending = false;
            _sameMapTeleportTarget = null;
            bool preserveCrossMapTeleportRequest = _pendingCrossMapTeleportTarget != null;
            if (!preserveCrossMapTeleportRequest)
            {
                _packetOwnedTeleportRequestActive = false;
                ConsumeSharedExclusiveRequestStateFromTransferResponseLifecycle();
                _packetOwnedTeleportRequestCompletedAt = int.MinValue;
                _lastPacketOwnedTeleportPortalRequestTick = int.MinValue;
                _lastPacketOwnedTeleportPortalIndex = -1;
                _lastPacketOwnedTeleportSourcePortalName = null;
                _lastPacketOwnedTeleportTargetPortalName = null;
                _lastPacketOwnedTeleportRegistrationTick = int.MinValue;
                _lastPacketOwnedTeleportMovePathAttribute = -1;
                _lastPacketOwnedTeleportMovePathPayload = Array.Empty<byte>();
                _lastPacketOwnedTeleportSetItemBackgroundActive = false;
                _lastPacketOwnedTeleportEffectTick = int.MinValue;
                _lastPacketOwnedTeleportEffectPath = null;
                _animationDisplayerPacketOwnedTeleportOneTimeOwnerStates.Clear();
                _lastPacketOwnedTeleportOutboundOpcode = -1;
                _lastPacketOwnedTeleportOutboundPayload = Array.Empty<byte>();
                _lastPacketOwnedTeleportOutboundSummary = null;
                _lastCollisionVerticalJumpMovePathAttribute = -1;
                _lastCollisionVerticalJumpMovePathPayload = Array.Empty<byte>();
                _lastCollisionCustomImpactMovePathAttribute = -1;
                _lastCollisionCustomImpactMovePathPayload = Array.Empty<byte>();
                _lastPortalOwnedMovePathFlushAdmissionTick = int.MinValue;
                _hasPortalOwnedMovePathFlushAdmission = false;
                _portalOwnedMovePathPostFlushCarry.Clear();
                _lastSimulatorPortalOwnedMovePathFlushTail = null;
                _lastCapturedPortalOwnedMovePathFlushTail = null;
                _lastCapturedPortalOwnedMovePathFlushTailSource = null;
                _lastCapturedPortalOwnedMovePathFlushTailFromOfficialSession = false;
                _lastCapturedPortalOwnedMovePathKeyPadMemoryStates = null;
                _lastCapturedPortalOwnedMovePathKeyPadMemorySource = null;
                _lastCapturedPortalOwnedMovePathKeyPadMemoryFromOfficialSession = false;
                _lastCapturedPortalOwnedMovePathKeyPadMemoryIsPostFlushClearEvidence = false;
                _recentCapturedPortalOwnedMovePathFlushTails.Clear();
                _pendingMapSpawnTarget = null;
            }
            ClearPendingPortalSessionValueImpacts();
            ConsumePassiveTransferRequestFromFieldInterfaceTeardown();


            // Deactivate chat input (but preserve message history)
            _chat.Deactivate();
            _npcInteractionOverlay?.Close();
            ClearAnimationDisplayerLocalQuestDeliveryOwner();
            _activeNpcInteractionNpc = null;
            _activeNpcInteractionNpcId = 0;
            _npcQuestFeedback.Clear();
            ResetPetSpeechEventState();
            _fieldMessageBoxRuntime.Clear();
            _packetFieldStateRuntime.Clear();
            ClearPacketOwnedStageTransitionState();
            ClearPacketOwnedReactorPoolState();
            _gameState.ExitDirectionModeImmediate();
            _scriptedDirectionModeWindows.Reset();

            _scriptedDirectionModeOwnerActive = false;

        }





        /// <summary>
        /// Loads map content for a new map during seamless transitions.
        /// </summary>
        /// <param name="newBoard">The new map board to load</param>
        /// <param name="newTitle">The new window title</param>
        /// <param name="spawnPortalName">Optional portal name to spawn at</param>
        internal Action CandidatePreparationCheckpointForTesting { get; set; }
        internal Action CandidateActivationPreflightCheckpointForTesting { get; set; }
        internal Action CandidateActivationCheckpointForTesting { get; set; }

        private sealed class PreparedMapActivation : IDisposable
        {
            public bool ChangesBgm { get; init; }
            public string BgmName { get; init; }
            public MonoGameBgmPlayer BgmPlayer { get; set; }

            public MonoGameBgmPlayer TakeBgmPlayer()
            {
                MonoGameBgmPlayer player = BgmPlayer;
                BgmPlayer = null;
                return player;
            }

            public void Dispose()
            {
                BgmPlayer?.Dispose();
                BgmPlayer = null;
            }
        }

        private sealed class MapActivationFatalException : Exception
        {
            public MapActivationFatalException(string message, Exception innerException)
                : base(message, innerException)
            {
            }
        }

        private void LoadMapContent(Contracts.RuntimeMapDefinition newMap, string newTitle, string spawnPortalName, int spawnPortalIndex = -1, string[] spawnPortalNameCandidates = null)
        {
            var candidate = new MapContentGeneration();
            _stagingContent = candidate;

            try
            {
                LoadMapContentIntoTarget(newMap, newTitle, spawnPortalName, spawnPortalIndex, spawnPortalNameCandidates);
                hostCancellation.ThrowIfCancellationRequested();
            }
            catch (Exception targetLoadError)
            {
                _stagingContent = null;
                Exception candidateCleanupError = null;
                try
                {
                    ContentGenerationTransaction.Reject(candidate);
                }
                catch (Exception error)
                {
                    candidateCleanupError = error;
                }

                if (candidateCleanupError != null)
                {
                    throw new AggregateException(
                        "The destination map failed to stage and its resources could not be fully retired.",
                        targetLoadError,
                        candidateCleanupError);
                }

                throw;
            }

            _stagingContent = null;
            PreparedMapActivation preparedActivation = null;
            try
            {
                preparedActivation = PrepareMapActivation(candidate);
                hostCancellation.ThrowIfCancellationRequested();
            }
            catch (Exception preparationError)
            {
                var cleanupErrors = new List<Exception>();
                try
                {
                    preparedActivation?.Dispose();
                }
                catch (Exception cleanupError)
                {
                    cleanupErrors.Add(cleanupError);
                }

                try
                {
                    ContentGenerationTransaction.Reject(candidate);
                }
                catch (Exception cleanupError)
                {
                    cleanupErrors.Add(cleanupError);
                }

                if (cleanupErrors.Count > 0)
                {
                    cleanupErrors.Insert(0, preparationError);
                    throw new AggregateException(
                        "Destination activation preflight failed and its resources could not be fully retired.",
                        cleanupErrors);
                }

                throw;
            }

            MapContentGeneration previous = _activeContent;
            _activeContent = candidate;

            Exception activationError = null;
            try
            {
                SyncActiveContentUi();
                ResetSessionMapStateForCommit();
                CandidateActivationCheckpointForTesting?.Invoke();
                ActivatePreparedMapContent(preparedActivation);
            }
            catch (Exception error)
            {
                activationError = error;
            }
            Exception preparedCleanupError = null;
            try
            {
                preparedActivation.Dispose();
            }
            catch (Exception error)
            {
                preparedCleanupError = error;
            }

            Exception retirementError = null;
            try
            {
                previous?.Dispose();
            }
            catch (Exception error)
            {
                retirementError = error;
            }

            if (activationError != null)
            {
                const string message =
                    "The destination map committed but mandatory activation failed; the game session must terminate.";
                var failures = new List<Exception> { activationError };
                if (preparedCleanupError != null) failures.Add(preparedCleanupError);
                if (retirementError != null) failures.Add(retirementError);
                Exit();
                throw new MapActivationFatalException(
                    message,
                    failures.Count == 1 ? activationError : new AggregateException(failures));
            }

            if (preparedCleanupError != null)
            {
                runtimeServices.Diagnostics.Report(
                    "The map activated, but a prepared activation resource failed to dispose cleanly.",
                    preparedCleanupError);
            }

            if (retirementError != null)
            {
                runtimeServices.Diagnostics.Report(
                    "The previous map remained retired, but one or more of its resources failed to dispose cleanly.",
                    retirementError);
            }
        }

        private PreparedMapActivation PrepareMapActivation(MapContentGeneration candidate)
        {
            if (_playerManager?.Player == null)
            {
                throw new InvalidOperationException(
                    "A map transition requires an initialized session player before the destination can commit.");
            }

            string bgmName = candidate.SpecialFieldRuntime == null
                ? candidate._mapBgmName
                : _specialFieldBgmOverrideName ?? candidate._mapBgmName;
            SoundManager.BgmRequestAction action = SoundManager.ResolveBgmRequestAction(
                _currentBgmName,
                bgmName,
                forceRestart: false,
                hasActiveBgm: _audio != null);
            if (action == SoundManager.BgmRequestAction.None)
            {
                return new PreparedMapActivation();
            }

            if (string.IsNullOrWhiteSpace(bgmName))
            {
                return new PreparedMapActivation { ChangesBgm = true };
            }

            CandidateActivationPreflightCheckpointForTesting?.Invoke();
            WzBinaryProperty bgmProperty = runtimeServices.Catalog.TryGetBgm(bgmName, out var bgm)
                ? bgm.Data
                : null;
            if (bgmProperty == null
                || !MonoGameBgmPlayer.TryCreate(bgmProperty, true, 0, 0.5f, out MonoGameBgmPlayer audio))
            {
                return new PreparedMapActivation { ChangesBgm = true };
            }

            return new PreparedMapActivation
            {
                ChangesBgm = true,
                BgmName = bgmName,
                BgmPlayer = audio
            };
        }

        private void LoadMapContentIntoTarget(Contracts.RuntimeMapDefinition newMap, string newTitle, string spawnPortalName, int spawnPortalIndex = -1, string[] spawnPortalNameCandidates = null)
        {
            hostCancellation.ThrowIfCancellationRequested();
            Stopwatch loadMapContentStopwatch = Stopwatch.StartNew();
            ContentTarget.PacketFieldStateRuntime = new PacketFieldStateRuntime(runtimeServices.Assets);
            ContentTarget.SpecialFieldRuntime = new SpecialFieldRuntimeCoordinator();
            _specialFieldRuntime.Initialize(
                _DxDeviceManager.GraphicsDevice,
                _soundManager,
                null,
                null,
                BuildAriantArenaRemoteCharacter,
                BuildAriantArenaRemoteCharacter,
                _playerManager?.Loader,
                runtimeServices,
                new UILoaderResourceCache(),
                ownCreatedGraphicsResources: true);
            _specialFieldRuntime.Minigames.MemoryGame.SetReadyClickSoundCallback(PlayMemoryGameReadyClickSE);
            _specialFieldRuntime.Minigames.MemoryGame.SetTimerWarningSoundCallback(PlayMemoryGameTimerWarningSE);
            SetMapDefinition(newMap);
            this._spawnPortalName = spawnPortalName;
            this._spawnPortalNameCandidates = spawnPortalNameCandidates ?? Array.Empty<string>();
            this._spawnPortalIndex = spawnPortalIndex;


            ContentTarget.WindowTitle = newTitle;
            string[] titleNameParts = newTitle.Split(':');
            ContentTarget.IsLoginMap = titleNameParts.All(part => part.Contains("MapLogin"));
            ContentTarget.IsCashShopMap = titleNameParts.All(part => part.Contains("CashShopPreview"));






            // Load WZ images needed for this map
            WzImage mapHelperImage = runtimeServices.Assets.FindImage("Map", "MapHelper.img");
            WzImage soundUIImage = runtimeServices.Assets.FindImage("Sound", "UI.img");
            WzImage uiToolTipImage = runtimeServices.Assets.FindImage("UI", "UIToolTip.img");
            WzImage uiBasicImage = runtimeServices.Assets.FindImage("UI", "Basic.img");
            WzImage uiLoginImage = runtimeServices.Assets.FindImage("UI", "Login.img");
            WzImage uiWindow1Image = runtimeServices.Assets.FindImage("UI", "UIWindow.img");
            WzImage uiWindow2Image = runtimeServices.Assets.FindImage("UI", "UIWindow2.img");
            WzImage uiMapImage = runtimeServices.Assets.FindImage("UI", "UIMap.img");
            WzImage uiMapleTvImage = runtimeServices.Assets.FindImage("UI", "MapleTV.img");
            WzImage uiGuildBbsImage = runtimeServices.Assets.FindImage("UI", "GuildBBS.img");
            WzImage uiBuffIconImage = runtimeServices.Assets.FindImage("UI", "BuffIcon.img");
            WzImage uiStatusBarImage = runtimeServices.Assets.FindImage("UI", "StatusBar.img");
            WzImage uiStatus2BarImage = runtimeServices.Assets.FindImage("UI", "StatusBar2.img");
            WzImage uiStatus3BarImage = runtimeServices.Assets.FindImage("UI", "StatusBar3.img");


            // BGM - only reload if different from current BGM

            _mapBgmName = _mapInfo.bgm;

            // VR boundaries
            if (_runtimeMapDefinition.VirtualBounds == null)
            {
                _vrFieldBoundary = new Rectangle(0, 0, _runtimeMapDefinition.MapSize.X, _runtimeMapDefinition.MapSize.Y);
                _vrRectangle = new Rectangle(0, 0, _runtimeMapDefinition.MapSize.X, _runtimeMapDefinition.MapSize.Y);
            }
            else
            {
                _vrFieldBoundary = new Rectangle(
                    _runtimeMapDefinition.VirtualBounds.Value.X + _runtimeMapDefinition.CenterPoint.X,
                    _runtimeMapDefinition.VirtualBounds.Value.Y + _runtimeMapDefinition.CenterPoint.Y,
                    _runtimeMapDefinition.VirtualBounds.Value.Width,
                    _runtimeMapDefinition.VirtualBounds.Value.Height);
                _vrRectangle = new Rectangle(_runtimeMapDefinition.VirtualBounds.Value.X, _runtimeMapDefinition.VirtualBounds.Value.Y, _runtimeMapDefinition.VirtualBounds.Value.Width, _runtimeMapDefinition.VirtualBounds.Value.Height);
            }


            // Initialize layer lists
            for (int i = 0; i < mapObjects.Length; i++)
            {
                mapObjects[i] = new List<BaseDXDrawableItem>();
            }


            ConcurrentBag<WzObject> usedProps = new ConcurrentBag<WzObject>();

            ConcurrentDictionary<BaseDXDrawableItem, QuestGatedMapObjectState> questGatedMapObjects = new();



            // Load map objects in parallel
            RunContentStage(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                foreach ((int DrawOrder, RuntimeTileDefinition? Tile, RuntimeObjectDefinition Object) entry in
                    _runtimeMapDefinition.Tiles
                        .Select(tile => (tile.DrawOrder, (RuntimeTileDefinition?)tile, (RuntimeObjectDefinition)null))
                        .Concat(_runtimeMapDefinition.Objects.Select(obj => (obj.DrawOrder, (RuntimeTileDefinition?)null, obj)))
                        .OrderBy(entry => entry.DrawOrder))
                {
                    RuntimeAssetKey asset = entry.Tile?.Asset ?? entry.Object.Asset;
                    WzImageProperty sourceProperty = ResolveRuntimeAssetProperty(asset);
                    int x = entry.Tile?.X ?? entry.Object.X;
                    int y = entry.Tile?.Y ?? entry.Object.Y;
                    int layer = entry.Tile?.Layer ?? entry.Object.Layer;
                    bool flip = entry.Object?.Flip ?? false;
                    BaseDXDrawableItem mapItem = MapSimulatorLoader.CreateMapItemFromProperty(
                        sceneryTexturePool,
                        sourceProperty,
                        x,
                        y,
                        _runtimeMapDefinition.CenterPoint,
                        _DxDeviceManager.GraphicsDevice,
                        usedProps,
                        flip);
                    if (mapItem == null)
                        continue;

                    if (entry.Object != null)
                    {
                        RegisterQuestGatedMapObject(mapItem, entry.Object, sourceProperty, questGatedMapObjects);
                    }
                    mapObjects[layer].Add(mapItem);
                    if (entry.Object != null)
                    {
                        foreach (BaseDXDrawableItem branchItem in CreatePacketOwnedStageTransitionAuthoredStateBranchItems(
                            mapItem, entry.Object, sourceProperty, usedProps, questGatedMapObjects))
                        {
                            mapObjects[layer].Add(branchItem);
                        }
                    }
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Tiles task loaded {_runtimeMapDefinition.Tiles.Count + _runtimeMapDefinition.Objects.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            RunContentStage(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                foreach (RuntimeBackgroundDefinition background in _runtimeMapDefinition.Backgrounds.OrderBy(item => item.DrawOrder))
                {
                    WzImageProperty bgParent = ResolveRuntimeAssetProperty(background.Asset);
                    BackgroundItem bgItem = MapSimulatorLoader.CreateBackgroundFromProperty(
                        sceneryTexturePool, bgParent, background, _DxDeviceManager.GraphicsDevice, usedProps);
                    if (bgItem != null)
                    {
                        (background.Front ? backgrounds_front : backgrounds_back).Add(bgItem);
                    }
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Background task loaded {_runtimeMapDefinition.Backgrounds.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            RunContentStage(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                foreach (RuntimeReactorDefinition reactorDefinition in _runtimeMapDefinition.Reactors)
                {
                    RuntimeReactor reactor = CreateRuntimeReactor(reactorDefinition);
                    if (reactor == null)
                        continue;
                    ReactorItem reactorItem = MapSimulatorLoader.CreateReactorFromProperty(ContentTarget.MapTextures, reactor, runtimeServices.Assets, _DxDeviceManager.GraphicsDevice, usedProps);
                    if (reactorItem != null)
                        mapObjects_Reactors.Add(reactorItem);
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Reactor task loaded {_runtimeMapDefinition.Reactors.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            RunContentStage(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                int loadedCount = 0;
                foreach (RuntimeLifeDefinition npcDefinition in _runtimeMapDefinition.Npcs)
                {
                    RuntimeLife npc = new RuntimeLife(npcDefinition);
                    if (npc.Hide)
                        continue;
                    NpcItem npcItem = MapSimulatorLoader.CreateNpcFromProperty(
                        ContentTarget.MapTextures,
                        npc,
                        runtimeServices.Assets,
                        UserScreenScaleFactor,
                        _DxDeviceManager.GraphicsDevice,
                        usedProps,
                        _playerManager?.Player?.Build?.Gender,
                        _questRuntime.HasNpcClientActionSelectionContext(),
                        _questRuntime.GetCurrentState,
                        questId => _questRuntime.TryGetQuestRecordValue(questId, out string value) ? value : string.Empty);
                    if (npcItem != null)
                    {
                        mapObjects_NPCs.Add(npcItem);
                        loadedCount++;
                    }
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] NPC task loaded {loadedCount}/{_runtimeMapDefinition.Npcs.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            RunContentStage(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                int loadedCount = 0;
                foreach (RuntimeLifeDefinition mobDefinition in _runtimeMapDefinition.Mobs)
                {
                    RuntimeLife mob = new RuntimeLife(mobDefinition);
                    if (mob.Hide)
                        continue;
                    MobItem mobItem = MapSimulatorLoader.CreateMobFromProperty(ContentTarget.MapTextures, mob, runtimeServices.Assets, UserScreenScaleFactor, _DxDeviceManager.GraphicsDevice, _soundManager, usedProps);
                    mobItem?.SetAnimationEffects(_animationEffects);
                    ConfigureMobActionSpeechConditionContext(mobItem);
                    ConfigureMobAutoSkillSelection(mobItem);
                    mapObjects_Mobs.Add(mobItem);
                    loadedCount++;
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Mob task loaded {loadedCount}/{_runtimeMapDefinition.Mobs.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            RunContentStage(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                int loadedCount = 0;
                WzSubProperty portalParent = (WzSubProperty)mapHelperImage["portal"];
                WzSubProperty gameParent = (WzSubProperty)portalParent["game"];
                foreach (RuntimePortal portal in _runtimePortals)
                {
                    PortalItem portalItem = MapSimulatorLoader.CreatePortalFromProperty(ContentTarget.MapTextures, gameParent, portal, _DxDeviceManager.GraphicsDevice, usedProps);
                    if (portalItem != null)
                    {
                        mapObjects_Portal.Add(portalItem);
                        loadedCount++;
                    }
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Portal task loaded {loadedCount}/{_runtimePortals.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            RunContentStage(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                WzSubProperty farmFrameParent = (WzSubProperty)uiToolTipImage?["Item"]?["FarmFrame"];
                foreach (RuntimeTooltipDefinition tooltip in _runtimeMapDefinition.Tooltips)
                {
                    TooltipItem item = MapSimulatorLoader.CreateTooltipFromProperty(ContentTarget.MapTextures, UserScreenScaleFactor, farmFrameParent, tooltip, _DxDeviceManager.GraphicsDevice);
                    mapObjects_tooltips.Add(item);
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Tooltip task loaded {_runtimeMapDefinition.Tooltips.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            RunContentStage(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                if (FieldInteractionRestrictionEvaluator.ShouldCreateStartupMinimap(
                    ContentTarget.IsLoginMap,
                    ContentTarget.IsCashShopMap,
                    _mapInfo))
                {
                    using var minimapData = HaCreator.MapSimulator.Assets.RuntimeMinimapFactory.Create(_runtimeMapDefinition, runtimeServices);
                    miniMapUi = MapSimulatorLoader.CreateMinimapFromProperty(uiWindow1Image, uiWindow2Image, uiMapImage, uiBasicImage, minimapData, GraphicsDevice, UserScreenScaleFactor, soundUIImage, _gameState.IsBigBangUpdate, resourceCache: uiResourceCache);
                    miniMapUi?.ReloadMiniMap(_packetOwnedMiniMapOnOffVisible);
                    if (_packetFieldUtilityMinimapHiddenByAdminResult)
                    {
                        miniMapUi?.EnsureCollapsed();
                    }
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Minimap task finished in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            RunContentStage(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                if (!ContentTarget.IsLoginMap && !ContentTarget.IsCashShopMap)
                {
                    Tuple<StatusBarUI, StatusBarChatUI> statusBar = MapSimulatorLoader.CreateStatusBarFromProperty(uiStatusBarImage, uiStatus2BarImage, uiStatus3BarImage, uiBasicImage, uiBuffIconImage, GraphicsDevice, UserScreenScaleFactor, _renderParams, soundUIImage, _gameState.IsBigBangUpdate, runtimeServices.Assets, resourceCache: uiResourceCache);
                    if (statusBar != null)
                    {
                        statusBarUi = statusBar.Item1;
                        statusBarChatUI = statusBar.Item2;
                    }
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Status bar task finished in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            // Recreate the cursor with the rest of the UI so it never carries stale render state
            // or expired pooled textures across map transitions.
            RunContentStage(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                WzImageProperty cursorImageProperty = (WzImageProperty)uiBasicImage["Cursor"];
                this.mouseCursor = MapSimulatorLoader.CreateMouseCursorFromProperty(ContentTarget.MapTextures, cursorImageProperty, 0, 0, _DxDeviceManager.GraphicsDevice, usedProps, false);
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Cursor task finished in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            // Asset uploads have completed on the game thread.

            LogStartupCheckpoint("LoadContent completed asset upload stages");



            // UI windows touch GraphicsDevice-backed resources and must be created on the main thread.
            if (!ContentTarget.IsCashShopMap)
            {
                if (ContentTarget.IsLoginMap)
                {
                    uiWindowManager ??= new UIWindowManager();
                    UIWindowLoader.RegisterLoginEntryWindows(
                        uiWindowManager,
                        uiLoginImage,
                        uiWindow1Image,
                        uiWindow2Image,
                        uiBasicImage,
                        soundUIImage,
                        GraphicsDevice,
                        _renderParams.RenderWidth,
                        _renderParams.RenderHeight,
                        runtimeServices.Assets);
                    UIWindowLoader.RegisterLoginCharacterDetailWindow(
                        uiWindowManager,
                        uiBasicImage,
                        soundUIImage,
                        GraphicsDevice,
                        _renderParams.RenderWidth,
                        _renderParams.RenderHeight,
                        runtimeServices.Assets);
                    UIWindowLoader.RegisterConnectionNoticeWindow(
                        uiWindowManager,
                        uiLoginImage,
                        uiBasicImage,
                        soundUIImage,
                        GraphicsDevice,
                        _renderParams.RenderWidth,
                        _renderParams.RenderHeight);
                    UIWindowLoader.RegisterLoginUtilityDialogWindow(
                        uiWindowManager,
                        uiWindow2Image,
                        uiLoginImage,
                        uiBasicImage,
                        soundUIImage,
                        GraphicsDevice,
                        _renderParams.RenderWidth,
                        _renderParams.RenderHeight,
                        runtimeServices.Assets);
                }
                else if (uiWindowManager == null || uiWindowManager.InventoryWindow == null)
                {
                    uiWindowManager = UIWindowLoader.CreateUIWindowManager(
                        uiWindow1Image, uiWindow2Image, uiBasicImage, soundUIImage,
                        null, null, uiMapleTvImage,
                        GraphicsDevice, _renderParams.RenderWidth, _renderParams.RenderHeight, _gameState.IsBigBangUpdate, runtimeServices: runtimeServices, resourceCache: uiResourceCache, storageAccountLabel: BuildStorageAccountLabel(), storageAccountKey: BuildStorageAccountKey(), profileStorage: sessionOptions.ProfileStorage);
                    UIWindowLoader.RegisterGuildBbsWindow(
                        uiWindowManager,
                        uiGuildBbsImage,
                        uiBasicImage,
                        soundUIImage,
                        GraphicsDevice,
                        new Point(
                            Math.Max(24, (_renderParams.RenderWidth / 2) - 367),
                            Math.Max(24, (_renderParams.RenderHeight / 2) - 263)));
                }
            }
            Debug.WriteLine($"[MapLoad] UI/window setup finished in {loadMapContentStopwatch.ElapsedMilliseconds} ms");


            ReplaceQuestGatedMapObjects(questGatedMapObjects);


            RegisterPacketOwnedLogoutGiftWindow();


            // Set fonts on UI windows after all tasks complete
            uiWindowManager?.SetFonts(_fontChat);
            WireWorldChannelSelectorWindows();
            WireRecommendWorldWindow();
            WireQuestLogWindowData();
            WireQuestRewardRaiseWindow();
            WireMemoMailboxWindowData();
            WireFamilyChartWindowData();
            WireSocialListWindowData();
            WireSocialSearchWindowData();
            WireGuildSearchWindowData();
            WireGuildSkillWindowData();
            WireGuildBbsWindowData();
            WireProgressionUtilityWindowLaunchers();
            LogStartupCheckpoint("LoadContent created UI windows");

            RefreshMapTransferWindow();

            RefreshWorldMapWindow();



            // Initialize status bar character stats display after map change

            if (statusBarUi != null)

            {

                ContentTarget.BuffIconCatalog = UILoader.LoadBuffIconCatalogEntries(uiBuffIconImage, uiResourceCache);
                statusBarUi.SetCharacterStatsProvider(_fontChat, GetCharacterStatsData);
                statusBarUi.SetBuffStatusProvider(GetStatusBarBuffData);
                statusBarUi.SetCooldownStatusProvider(GetStatusBarCooldownData);
                statusBarUi.SetOffBarCooldownStatusProvider(GetStatusBarOffBarCooldownData);
                statusBarUi.SetPreparedSkillProvider(currentTime => GetPreparedSkillBarData(currentTime, PreparedSkillHudSurface.StatusBar));
                statusBarUi.SetPreparedSkillOverlayProvider(currentTime => GetPreparedSkillBarData(currentTime, PreparedSkillHudSurface.World));
                statusBarUi.SetPixelTexture(_DxDeviceManager.GraphicsDevice);
                statusBarUi.SetLowResourceWarningThresholds(_statusBarHpWarningThresholdPercent, _statusBarMpWarningThresholdPercent);
                statusBarUi.BuffCancelRequested = skillId =>
                {
                    RequestStatusBarBuffCancelForClientCancelIngress(skillId, currTickCount);
                };
            }
            ConfigureStatusBarChatUi();
            LogStartupCheckpoint("LoadContent finished status/UI provider hookup");
            Debug.WriteLine($"[MapLoad] Status/UI provider hookup finished in {loadMapContentStopwatch.ElapsedMilliseconds} ms");


            // Reconnect Ability/Stat window to player's CharacterBuild after map change
            if (uiWindowManager?.AbilityWindow != null && _playerManager?.Player?.Build != null)
            {
                uiWindowManager.AbilityWindow.CharacterBuild = _playerManager.Player.Build;
                uiWindowManager.AbilityWindow.SetFont(_fontDebugValues);
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CharacterInfo) != null && _playerManager?.Player?.Build != null)
            {
                UIWindowBase characterInfoWindow = uiWindowManager.GetWindow(MapSimulatorWindowNames.CharacterInfo);
                characterInfoWindow.CharacterBuild = _playerManager.Player.Build;
                characterInfoWindow.SetFont(_fontDebugValues);
                if (characterInfoWindow is UserInfoUI userInfoWindow)
                {
                    userInfoWindow.SetPetController(_playerManager.Pets);
                    userInfoWindow.SetCollectionSnapshotProvider(ResolveCharacterInfoItemMakerProgressionSnapshot);
                    userInfoWindow.SetMonsterBookSnapshotProvider(ResolveCharacterInfoMonsterBookSnapshot);
                    userInfoWindow.SetRankDeltaProvider(ResolveCharacterInfoRankDeltaSnapshot);
                    userInfoWindow.SetRemoteRideSnapshotProvider(ResolveCharacterInfoRemoteRideSnapshot);

                    WireCharacterInfoWindowActionRoutes(userInfoWindow);
                }
            }
            if (uiWindowManager?.EquipWindow != null && _playerManager?.Player?.Build != null)
            {
                uiWindowManager.EquipWindow.CharacterBuild = _playerManager.Player.Build;
                uiWindowManager.EquipWindow.SetFont(_fontChat);
                if (uiWindowManager.EquipWindow is EquipUI equipWindow)
                {
                    equipWindow.SetPetController(_playerManager.Pets);
                    equipWindow.SetDragonEquipmentController(_playerManager.CompanionEquipment?.Dragon);
                    equipWindow.ItemUpgradeRequested = OpenItemUpgradeWindowForEquipmentWithClientCancelIngress;
                    equipWindow.EquipmentChangeSubmitted = SubmitEquipmentChangeRequest;
                    equipWindow.EquipmentChangeResultRequested = TryResolveEquipmentChangeRequest;
                    equipWindow.EquipmentDragStartBlocked = ShouldBlockEquipmentDragStart;
                }
                if (uiWindowManager.EquipWindow is EquipUIBigBang equipBigBang)
                {
                    equipBigBang.SetCharacterLoader(_playerManager.Loader);
                    equipBigBang.ItemUpgradeRequested = OpenItemUpgradeWindowForEquipmentWithClientCancelIngress;
                    equipBigBang.EquipmentChangeSubmitted = SubmitEquipmentChangeRequest;
                    equipBigBang.EquipmentChangeResultRequested = TryResolveEquipmentChangeRequest;
                    equipBigBang.EquipmentDragStartBlocked = ShouldBlockEquipmentDragStart;
                    equipBigBang.CompanionDragCommitIngressRequested =
                        () => ReleaseActiveKeydownSkillForClientCancelIngress(currTickCount);
                    equipBigBang.SetPetController(_playerManager.Pets);

                    equipBigBang.SetPetEquipmentController(_playerManager.CompanionEquipment?.Pet);

                    equipBigBang.SetDragonEquipmentController(_playerManager.CompanionEquipment?.Dragon);

                    equipBigBang.SetMechanicEquipmentController(_playerManager.CompanionEquipment?.Mechanic);
                    equipBigBang.SetMechanicPaneAvailable(
                        CompanionEquipmentController.HasMechanicOwnerState(_playerManager?.Player?.Build)
                        && FieldInteractionRestrictionEvaluator.CanUseTamingMob(_mapInfo?.fieldLimit ?? 0));
                    equipBigBang.SetAndroidEquipmentController(_playerManager.CompanionEquipment?.Android);
                    equipBigBang.SetAndroidPaneAvailable(FieldInteractionRestrictionEvaluator.CanUseAndroid(
                        _mapInfo?.fieldLimit ?? 0,
                        _mapInfo));
                }
            }
            if (uiWindowManager?.InventoryWindow is InventoryUI inventoryWindow && _playerManager?.Player?.Build != null)
            {
                inventoryWindow.CharacterBuild = _playerManager.Player.Build;
                inventoryWindow.SetFont(_fontChat);
                inventoryWindow.SetCharacterLoader(_playerManager.Loader);
                inventoryWindow.ItemUpgradeRequested = OpenItemUpgradeWindowForConsumableWithClientCancelIngress;
                inventoryWindow.ItemUseRequested = TryUseInventoryItem;
                inventoryWindow.ItemUseRequestedAtSlot = TryUseInventoryItemAtSlot;
                inventoryWindow.InventoryDropRequested = HandleLocalInventoryDropRequestWithClientCancelIngress;
                inventoryWindow.MesoDropRequested = HandleLocalMesoDropRequestWithClientCancelIngress;
                inventoryWindow.EquipmentDragStartBlocked = ShouldBlockEquipmentDragStart;
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) is ItemUpgradeUI itemUpgradeWindow && _playerManager?.Player?.Build != null)
            {
                itemUpgradeWindow.CharacterBuild = _playerManager.Player.Build;
                itemUpgradeWindow.SetFont(_fontChat);
                itemUpgradeWindow.SetInventory(uiWindowManager.InventoryWindow as IInventoryRuntime);
                WireItemUpgradeOwnerCallbacks(itemUpgradeWindow);
                if (uiWindowManager.GetWindow(MapSimulatorWindowNames.VegaSpell) is VegaSpellUI vegaSpellWindow)
                {
                    vegaSpellWindow.CharacterBuild = _playerManager.Player.Build;
                    vegaSpellWindow.SetFont(_fontChat);
                    vegaSpellWindow.SetItemUpgradeBackend(itemUpgradeWindow);
                    WireVegaSpellWindowOwnerCallbacks(vegaSpellWindow);
                }
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.RepairDurability) is RepairDurabilityWindow repairDurabilityWindow && _playerManager?.Player?.Build != null)
            {
                repairDurabilityWindow.CharacterBuild = _playerManager.Player.Build;
                repairDurabilityWindow.SetFont(_fontChat);
                repairDurabilityWindow.SetInventory(uiWindowManager.InventoryWindow as IInventoryRuntime);
                repairDurabilityWindow.RepairRequested -= HandleRepairDurabilityRequested;
                repairDurabilityWindow.RepairRequested += HandleRepairDurabilityRequested;
                repairDurabilityWindow.RepairAllRequested -= HandleRepairDurabilityAllRequested;
                repairDurabilityWindow.RepairAllRequested += HandleRepairDurabilityAllRequested;
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is AdminShopDialogUI cashShopWindow)
            {

                cashShopWindow.SetInventory(uiWindowManager.InventoryWindow as IInventoryRuntime);
                cashShopWindow.SetCashBalances(_loginAccountCashShopNxCredit);
                cashShopWindow.TryConsumeCashBalance = TryConsumeLoginAccountCashShopNxCredit;
                cashShopWindow.ResolveStorageExpansionCommoditySerialNumber = ResolveStorageExpansionCommoditySerialNumber;
                cashShopWindow.GetStorageExpansionStatusSummary = GetStorageExpansionStatusSummary;
                cashShopWindow.StorageExpansionResolved = HandleStorageExpansionResolved;
                cashShopWindow.WindowHidden = _ => HideCashShopOwnerFamilyWindows();
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashAvatarPreview) is CashAvatarPreviewWindow cashAvatarPreviewWindow
                && _playerManager?.Player?.Build != null)
            {
                cashAvatarPreviewWindow.CharacterBuild = _playerManager.Player.Build;
                cashAvatarPreviewWindow.SetFont(_fontChat);
                cashAvatarPreviewWindow.EquipmentLoader = _playerManager.Loader != null ? _playerManager.Loader.LoadEquipment : null;
                cashAvatarPreviewWindow.ClientCancelIngressRequested =
                    () => ReleaseActiveKeydownSkillForClientCancelIngress(currTickCount);
                cashAvatarPreviewWindow.PersonalShopRequested = ShowCashAvatarPersonalShopAction;
                cashAvatarPreviewWindow.EntrustedShopRequested = ShowCashAvatarEntrustedShopAction;
                cashAvatarPreviewWindow.TradingRoomRequested = ShowCashAvatarTradingRoomAction;
                cashAvatarPreviewWindow.WeatherRequested = PreviewCashAvatarWeatherAction;
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Mts) is AdminShopDialogUI mtsWindow)
            {
                mtsWindow.SetInventory(uiWindowManager.InventoryWindow as IInventoryRuntime);
            }

            _temporaryPortalField = new TemporaryPortalField(
                ContentTarget.MapTextures,
                _DxDeviceManager.GraphicsDevice,
                runtimeServices);


            // Initialize mob foothold references

            InitializeMobFootholds();



            // Convert lists to arrays

            ConvertListsToArrays();



            // Set camera position and spawn point

            ResolveSpawnPosition(out float spawnX, out float spawnY);


            ClampLegacyCameraToBoundaries();


            // Store map center point for camera calculations

            _mapCenterX = _runtimeMapDefinition.CenterPoint.X;

            _mapCenterY = _runtimeMapDefinition.CenterPoint.Y;

            ContentTarget.SpawnX = spawnX;
            ContentTarget.SpawnY = spawnY;
            _packetFieldStateRuntime.Initialize(GraphicsDevice, _mapInfo);
            _specialFieldRuntime.BindMap(_runtimeMapDefinition);
            PrepareMapBoundaryResources();
            DetectAndInitializeTransportField();
            AttachPreparedMapSpineEvents();
            CandidatePreparationCheckpointForTesting?.Invoke();

            LogStartupCheckpoint($"Prepared map content for map {_mapInfo?.id}");
            loadMapContentStopwatch.Stop();
            Debug.WriteLine($"[MapLoad] Prepared map {_mapInfo?.id} in {loadMapContentStopwatch.ElapsedMilliseconds} ms");
        }

        private void ActivatePreparedMapContent(PreparedMapActivation preparedActivation)
        {
            float spawnX = ContentTarget.SpawnX;
            float spawnY = ContentTarget.SpawnY;
            Window.Title = ContentTarget.WindowTitle;
            _gameState.IsLoginMap = ContentTarget.IsLoginMap;
            _gameState.IsCashShopMap = ContentTarget.IsCashShopMap;
            ApplyPreparedBgm(preparedActivation);



            // Initialize player at portal spawn position (not viewfinder center)
            // spawnX/spawnY are set above from target portal or start point
            // For map changes, reconnect existing player instead of creating new one
            ResetLoginRuntimeForCurrentMap(currTickCount);
            InitializeAuthoredDynamicObjectTagStates();
            bool runOnFirstUserEnterScript = ShouldRunOnFirstUserEnterForCurrentMap();
            ApplyEntryScriptDynamicObjectTagStates(currTickCount, runOnFirstUserEnterScript);
            InitializeDynamicObjectDirectionEventTriggers();
            if (!_gameState.IsLoginMap)
            {
                if (_playerManager != null && _playerManager.Player != null)
                {
                    ReconnectPlayerToMap(spawnX, spawnY);
                }
                else
                {
                    InitializePlayerManager(spawnX, spawnY);
                }


                InitializeFieldRuleRuntime(currTickCount, runOnFirstUserEnterScript);
            }
            else
            {
                _gameState.PlayerControlEnabled = false;
                if (_playerManager == null || _playerManager.Player == null)
                {
                    InitializePlayerManager(spawnX, spawnY);
                }


                InitializeLoginCharacterRoster();

            }
            LogStartupCheckpoint($"LoadContent initialized player/map runtime (loginMap={_gameState.IsLoginMap}, playerActive={_playerManager?.IsPlayerActive ?? false})");

            SetCookieHouseContextPoint(0);

            BindRemoteAffectedAreaPacketField();
            BindRemoteDropPacketField();
            _specialFieldRuntime.SetBgmCallbacks(RequestSpecialFieldBgmOverride, ClearSpecialFieldBgmOverride);
            ApplyClientOwnedFieldWrappers();
            BindPacketOwnedStageTransitionMapState();
            BindPacketOwnedReactorPoolMapState();
            SyncSnowBallPacketInboxState();
            SyncCoconutPacketInboxState();
            SyncMemoryGamePacketInboxState();
            SyncAriantArenaPacketInboxState();
            SyncMonsterCarnivalPacketInboxState();
            SyncMassacrePacketInboxState();
            SyncDojoPacketInboxState();
            SyncTransportPacketInboxState();
            SyncGuildBossTransportState();
            SyncPartyRaidPacketInboxState();
            SyncTournamentPacketInboxState();

            SyncCookieHousePointInboxState();

            SyncBattlefieldLocalAppearance();
            _remoteUserPool.SyncBattlefieldAppearance(_specialFieldRuntime.SpecialEffects.Battlefield);


            // Initialize camera controller for smooth scrolling
            _cameraController.Initialize(
                _vrFieldBoundary,
                _renderParams.RenderWidth,
                _renderParams.RenderHeight,
                _mapCenterX,
                _mapCenterY,
                _renderParams.RenderObjectScaling);
            _cameraController.SetPosition(spawnX, spawnY);


            ApplyTransitAndVoyageFieldWrapper(_mapInfo);
            if (ContentTarget.HasTransportationField)
            {
                MirrorTransportFieldInitRequestForCurrentMap();
            }
            LogStartupCheckpoint("LoadContent initialized transport/camera runtime");

            _renderingManager.SetVRBorderData(_vrFieldBoundary, _drawVRBorderLeftRight, _vrBoundaryTextureLeft, _vrBoundaryTextureRight);

            _renderingManager.SetLBBorderData(_lbTextureLeft, _lbTextureRight);

            if (ContentTarget.BuffIconCatalog != null)
            {
                _playerManager?.Skills?.ConfigureBuffIconCatalog(ContentTarget.BuffIconCatalog);
            }
            WireSessionOwnedControllersToActiveWindows();
            LogStartupCheckpoint($"LoadContent completed for map {_mapInfo?.id}");
        }

        private void ApplyPreparedBgm(PreparedMapActivation preparedActivation)
        {
            if (!preparedActivation.ChangesBgm)
            {
                return;
            }

            MonoGameBgmPlayer previousAudio = _audio;
            _audio = preparedActivation.TakeBgmPlayer();
            _currentBgmName = _audio == null ? null : preparedActivation.BgmName;
            _isBgmPausedForFocusLoss = false;
            previousAudio?.Dispose();
            if (_audio != null)
            {
                StartBgmForCurrentFocusState();
            }
        }

        private void WireSessionOwnedControllersToActiveWindows()
        {
            _engagementProposalController.SocialMessagesObserved = TryTriggerSpecialistPetSocialFeedback;
            _engagementProposalController.ClientPacketDispatcher = DispatchEngagementProposalClientRequest;
            _engagementProposalController.WireWindow(uiWindowManager, _playerManager?.Player?.Build, _fontChat, ShowUtilityFeedbackMessage);
            _weddingWishListController.SocialChatObserved = TryTriggerSpecialistPetSocialFeedback;
            _weddingWishListController.ClientPacketDispatcher = DispatchWeddingWishListClientRequest;
            _weddingInvitationController.SocialMessagesObserved = TryTriggerSpecialistPetSocialFeedback;
            _weddingInvitationController.WireWindow(uiWindowManager, _playerManager?.Player?.Build, _fontChat, ShowUtilityFeedbackMessage);
            _weddingWishListController.WireWindow(uiWindowManager, _playerManager?.Player?.Build, uiWindowManager?.InventoryWindow as IInventoryRuntime, _fontChat, ShowUtilityFeedbackMessage);
        }

        private void ResetSessionMapStateForCommit()
        {
            _mobAttackSystem.Clear();
            _combatEffects?.ClearMapState();
            _fieldEffects?.ResetAllEffects();
            _remoteUserPool.Clear();
            _summonedPool.Clear();
            _playerManager?.PrepareForMapChange();
            _passengerSync.Clear();
            _escortFollow.Clear();
            _localFollowRuntime.Clear();
            HidePacketOwnedFollowCharacterPrompt();
            ClearPacketOwnedPassiveMoveState();
            _lastFieldRestrictionMessageTime = int.MinValue;
            _lastFieldRestrictionMessage = null;
            _lastSkillCooldownBlockedMessageTimes.Clear();
            _lastMobPickupTimes.Clear();
            _skillCooldownNoticeUI.Clear();
            _frameActiveMobs.Clear();
            _frameMovableMobs.Clear();
            _framePrimaryBossMob = null;
            _lastClickedPortal = null;
            _lastClickedHiddenPortal = null;
            _lastClickTime = 0;
            _sameMapTeleportPending = false;
            _sameMapTeleportTarget = null;
            ClearPendingPortalSessionValueImpacts();
            ConsumePassiveTransferRequestFromFieldInterfaceTeardown();
            _chat.Deactivate();
            _npcInteractionOverlay?.Close();
            ClearAnimationDisplayerLocalQuestDeliveryOwner();
            _activeNpcInteractionNpc = null;
            _activeNpcInteractionNpcId = 0;
            _npcQuestFeedback.Clear();
            ResetPetSpeechEventState();
            _fieldMessageBoxRuntime.Clear();
            ClearPacketOwnedStageTransitionState();
            ClearPacketOwnedReactorPoolState();
            _gameState.ExitDirectionModeImmediate();
            _scriptedDirectionModeWindows.Reset();
            _scriptedDirectionModeOwnerActive = false;
        }

        private void PrepareMapBoundaryResources()
        {
            int leftRightVRDifference = (int)((_vrFieldBoundary.Right - _vrFieldBoundary.Left) * _renderParams.RenderObjectScaling);
            if (leftRightVRDifference < _renderParams.RenderWidth)
            {
                _drawVRBorderLeftRight = true;
                _vrBoundaryTextureLeft = CreateVRBorder(VR_BORDER_WIDTHHEIGHT, _vrFieldBoundary.Height, _DxDeviceManager.GraphicsDevice);
                _vrBoundaryTextureRight = CreateVRBorder(VR_BORDER_WIDTHHEIGHT, _vrFieldBoundary.Height, _DxDeviceManager.GraphicsDevice);
                _vrBoundaryTextureTop = CreateVRBorder(_vrFieldBoundary.Width * 2, VR_BORDER_WIDTHHEIGHT, _DxDeviceManager.GraphicsDevice);
                _vrBoundaryTextureBottom = CreateVRBorder(_vrFieldBoundary.Width * 2, VR_BORDER_WIDTHHEIGHT, _DxDeviceManager.GraphicsDevice);
            }

            if (_mapInfo.LBSide != null)
            {
                _lbSide = (int)_mapInfo.LBSide;
                _lbTextureLeft = CreateLBBorder(LB_BORDER_WIDTHHEIGHT + _lbSide, Height, _DxDeviceManager.GraphicsDevice);
                _lbTextureRight = CreateLBBorder(LB_BORDER_WIDTHHEIGHT + _lbSide, Height, _DxDeviceManager.GraphicsDevice);
            }
            if (_mapInfo.LBTop != null)
            {
                _lbTop = (int)_mapInfo.LBTop;
                _lbTextureTop = CreateLBBorder((int)(_vrFieldBoundary.Width * 1.45), LB_BORDER_WIDTHHEIGHT + _lbTop, _DxDeviceManager.GraphicsDevice);
            }
            if (_mapInfo.LBBottom != null)
            {
                _lbBottom = (int)_mapInfo.LBBottom;
                _lbTextureBottom = CreateLBBorder((int)(_vrFieldBoundary.Width * 1.45), LB_BORDER_WIDTHHEIGHT + _lbBottom, _DxDeviceManager.GraphicsDevice);
            }

            if (_mapInfo.mirror_Bottom && _mapInfo.VRLeft != null && _mapInfo.VRRight != null)
            {
                int vrWidth = (int)_mapInfo.VRRight - (int)_mapInfo.VRLeft;
                const int mirrorBottomHeight = 200;
                _mirrorBottomRect = new Rectangle(
                    (int)_mapInfo.VRLeft,
                    (int)_mapInfo.VRBottom - mirrorBottomHeight,
                    vrWidth,
                    mirrorBottomHeight);
                _mirrorBottomReflection = new ReflectionDrawableBoundary(128, 255, "mirror", true, false);
            }
        }

        private void AttachPreparedMapSpineEvents()
        {
            foreach (WzSpineObject spineObject in ContentTarget.MapTextures
                .TakeNewSpineObjects()
                .Concat(sceneryTexturePool.TakeNewSpineObjects()))
            {
                EnsureSpineRenderer();
                spineObject.state.Start += Start;
                spineObject.state.End += End;
                spineObject.state.Complete += Complete;
                spineObject.state.Event += Event;
            }
        }

        private void SyncActiveContentUi()
        {
            _uiManager.Minimap = _activeContent?.Minimap;
            _uiManager.StatusBar = _activeContent?.StatusBar;
            _uiManager.StatusBarChat = _activeContent?.StatusBarChat;
            _uiManager.WindowManager = _activeContent?.WindowManager;
            _uiManager.MouseCursor = _activeContent?.MouseCursor;
        }

        private void RestoreActiveMapRuntimeAfterFailedStage(string previousTitle, Vector2? previousPlayerPosition)
        {
            SyncActiveContentUi();
            Window.Title = previousTitle;
            string[] titleNameParts = previousTitle.Split(':');
            _gameState.IsLoginMap = titleNameParts.All(part => part.Contains("MapLogin"));
            _gameState.IsCashShopMap = titleNameParts.All(part => part.Contains("CashShopPreview"));
            _mapBgmName = _mapInfo?.bgm;
            ApplyRequestedBgm(_specialFieldBgmOverrideName ?? _mapBgmName);

            InitializeMobFootholds();
            if (previousPlayerPosition.HasValue && _playerManager?.Player != null)
            {
                ReconnectPlayerToMap(previousPlayerPosition.Value.X, previousPlayerPosition.Value.Y);
            }

            BindRemoteAffectedAreaPacketField();
            BindRemoteDropPacketField();
            ApplyClientOwnedFieldWrappers();
            BindPacketOwnedStageTransitionMapState();
            BindPacketOwnedReactorPoolMapState();
            SyncSnowBallPacketInboxState();
            SyncCoconutPacketInboxState();
            SyncMemoryGamePacketInboxState();
            SyncAriantArenaPacketInboxState();
            SyncMonsterCarnivalPacketInboxState();
            SyncMassacrePacketInboxState();
            SyncDojoPacketInboxState();
            SyncTransportPacketInboxState();
            SyncGuildBossTransportState();
            SyncPartyRaidPacketInboxState();
            SyncTournamentPacketInboxState();
            SyncCookieHousePointInboxState();

            float cameraX = previousPlayerPosition?.X ?? _mapCenterX;
            float cameraY = previousPlayerPosition?.Y ?? _mapCenterY;
            _cameraController.Initialize(
                _vrFieldBoundary,
                _renderParams.RenderWidth,
                _renderParams.RenderHeight,
                _mapCenterX,
                _mapCenterY,
                _renderParams.RenderObjectScaling);
            _cameraController.SetPosition(cameraX, cameraY);
            DetectAndInitializeTransportField();
            ApplyTransitAndVoyageFieldWrapper(_mapInfo);
            _renderingManager.SetVRBorderData(
                _vrFieldBoundary,
                _drawVRBorderLeftRight,
                _vrBoundaryTextureLeft,
                _vrBoundaryTextureRight);
            _renderingManager.SetLBBorderData(_lbTextureLeft, _lbTextureRight);
        }

        private void ConfigureStatusBarChatUi()
        {
            if (statusBarChatUI == null)
            {
                return;
            }

            statusBarChatUI.SetFont(_fontChat);
            statusBarChatUI.SetPixelTexture(_DxDeviceManager.GraphicsDevice);
            statusBarChatUI.SetChatRenderProvider(() => _chat.GetRenderState(_playerManager?.Player?.Name));
            statusBarChatUI.SetPointNotificationRenderProvider(GetStatusBarPointNotificationState);
            statusBarChatUI.ToggleChatRequested = () => _chat.ToggleActive(Environment.TickCount);
            statusBarChatUI.CycleChatTargetRequested = delta => _chat.CycleTarget(delta);
            statusBarChatUI.WhisperTargetRequested = target => _chat.BeginWhisperTo(target, Environment.TickCount);
            statusBarChatUI.WhisperTargetPickerRequested = () => _chat.OpenWhisperTargetPicker(Environment.TickCount);
            statusBarChatUI.WhisperTargetPickerCandidateRequested = target => _chat.SelectWhisperTargetPickerCandidate(target, Environment.TickCount);
            statusBarChatUI.WhisperTargetPickerSelectionDeltaRequested = delta => _chat.OffsetWhisperTargetPickerSelection(delta);
            statusBarChatUI.WhisperTargetPickerConfirmRequested = () => _chat.ConfirmWhisperTargetPicker(Environment.TickCount);
            statusBarChatUI.WhisperTargetPickerCancelRequested = () => _chat.CancelActiveWhisperTargetPicker();
            statusBarChatUI.WhisperTargetPickerModalButtonFocusRequested = () => _chat.ActivateWhisperTargetPickerModalButtonFocus();
            statusBarChatUI.WhisperTargetPickerModalComboFocusRequested = () => _chat.ActivateWhisperTargetPickerModalComboFocus();
            statusBarChatUI.WhisperTargetPickerModalComboDropdownCloseRequested = () => _chat.CloseWhisperTargetPickerModalComboDropdown();
            statusBarChatUI.WhisperTargetPickerModalComboDropdownToggleRequested = () => _chat.ToggleWhisperTargetPickerModalComboDropdown();
            statusBarChatUI.WhisperTargetPickerModalComboDropdownHoverRequested = target => _chat.HighlightWhisperTargetPickerModalComboDropdownCandidate(target);
            statusBarChatUI.WhisperTargetPickerModalComboDropdownHoverIndexRequested = rowIndex => _chat.HighlightWhisperTargetPickerModalComboDropdownCandidateAtClientRowIndex(rowIndex);
            statusBarChatUI.WhisperTargetPickerModalComboDropdownSelectIndexRequested = rowIndex => _chat.SelectWhisperTargetPickerModalComboDropdownCandidateAtClientRowIndex(rowIndex);
            statusBarChatUI.WhisperTargetPickerModalComboDropdownDeleteRequested = target => _chat.DeleteWhisperTargetPickerModalComboDropdownCandidate(target);
            statusBarChatUI.WhisperTargetPickerModalComboDropdownDeleteIndexRequested = rowIndex => _chat.DeleteWhisperTargetPickerModalComboDropdownCandidateAtClientRowIndex(rowIndex);
            statusBarChatUI.WhisperTargetPickerModalComboDropdownScrollRequested = delta => _chat.ScrollWhisperTargetPickerModalComboDropdown(delta);
            statusBarChatUI.WhisperTargetPickerModalComboDropdownPageRequested = delta => _chat.PageWhisperTargetPickerModalComboDropdown(delta);
            statusBarChatUI.WhisperTargetPickerModalComboDropdownScrollPositionRequested = firstVisibleIndex => _chat.SetWhisperTargetPickerModalComboDropdownFirstVisibleIndex(firstVisibleIndex);
            statusBarChatUI.ResolveImeWindowHandle = () => Window?.Handle ?? IntPtr.Zero;
            statusBarChatUI.ImeCandidateListRefreshedRequested = state => _chat.HandleImeCandidateList(state);
            statusBarChatUI.ImeCandidateSelectedRequested = (listIndex, candidateIndex) =>
                WindowsImeCandidateSelectionBridge.TrySelectCandidate(Window?.Handle ?? IntPtr.Zero, listIndex, candidateIndex);
            statusBarChatUI.ImeEditStateReleasedRequested = () => _chat.ReleaseImeEditStateAfterNativeCandidateSelection();
        }

        private RuntimeReactor CreateRuntimeReactor(RuntimeReactorDefinition definition)
        {
            string category = string.IsNullOrWhiteSpace(definition.Asset.Category)
                ? "Reactor"
                : definition.Asset.Category;
            string path = string.IsNullOrWhiteSpace(definition.Asset.Path)
                ? WzInfoTools.AddLeadingZeros(definition.Id, 7) + ".img"
                : definition.Asset.Path;
            WzImage templateImage = runtimeServices.Assets.FindImage(category, path);
            return templateImage == null ? null : new RuntimeReactor(definition, templateImage);
        }

        private WzImageProperty ResolveRuntimeAssetProperty(RuntimeAssetKey asset)
        {
            string path = (asset.Path ?? string.Empty).Replace('\\', '/').Trim('/');
            int imageEnd = path.IndexOf(".img", StringComparison.OrdinalIgnoreCase);
            if (imageEnd < 0)
                return runtimeServices.Assets.FindObject(asset.Category, path) as WzImageProperty;

            imageEnd += 4;
            WzImage image = runtimeServices.Assets.FindImage(asset.Category, path[..imageEnd]);
            WzObject current = image;
            foreach (string segment in path[imageEnd..].Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                current = current?[segment];
                if (current == null)
                    break;
            }
            return current as WzImageProperty;
        }
    }
}
