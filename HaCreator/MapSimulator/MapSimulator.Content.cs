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
            LogStartupCheckpoint("LoadContent begin");

            // Load physics constants from Map.wz/Physics.img
            LoadPhysicsConstants();


            WzImage mapHelperImage = Program.FindImage("Map", "MapHelper.img");
            WzImage soundUIImage = Program.FindImage("Sound", "UI.img");
            WzImage uiToolTipImage = Program.FindImage("UI", "UIToolTip.img"); // UI_003.wz
            WzImage uiBasicImage = Program.FindImage("UI", "Basic.img");
            WzImage uiLoginImage = Program.FindImage("UI", "Login.img");
            WzImage uiWindow1Image = Program.FindImage("UI", "UIWindow.img"); //
            WzImage uiWindow2Image = Program.FindImage("UI", "UIWindow2.img"); // doesnt exist before big-bang
            WzImage uiMapleTvImage = Program.FindImage("UI", "MapleTV.img");
            WzImage uiGuildBbsImage = Program.FindImage("UI", "GuildBBS.img");
            WzImage uiBuffIconImage = Program.FindImage("UI", "BuffIcon.img");


            WzImage uiStatusBarImage = Program.FindImage("UI", "StatusBar.img");

            WzImage uiStatus2BarImage = Program.FindImage("UI", "StatusBar2.img");



            // Skill.wz and String.wz for skill window content
            WzFile skillWzFile = null;
            WzFile stringWzFile = null;
            try
            {
                var fileManager = WzFileManager.fileManager;
                if (fileManager != null)
                {
                    var skillDir = fileManager["skill"];
                    skillWzFile = skillDir?.WzFileParent;
                    var stringDir = fileManager["string"];
                    stringWzFile = stringDir?.WzFileParent;
                }
            }
            catch { }


            _gameState.IsBigBangUpdate = WzFileManager.IsBigBangUpdate(uiWindow2Image); // different rendering for pre and post-bb, to support multiple vers

            _gameState.IsBigBang2Update = WzFileManager.IsBigBang2Update(uiWindow2Image); // chaos update
            LogStartupCheckpoint("LoadContent resolved shared WZ/UI references");



            // BGM

            _mapBgmName = _mapBoard.MapInfo.bgm;

            ApplyRequestedBgm(_specialFieldBgmOverrideName ?? _mapBgmName);



            // Sound effects from Sound.wz/Game.img - using SoundManager for concurrent playback

            _soundManager = new SoundManager();
            ApplyUtilityAudioSettings();
            WzImage soundGameImage = Program.FindImage("Sound", "Game.img");
            if (soundGameImage != null)
            {
                // Portal teleport sound
                WzBinaryProperty portalSound = (WzBinaryProperty)soundGameImage["Portal"];
                if (portalSound != null)
                {
                    _soundManager.RegisterSound("Portal", portalSound);
                }


                // Jump sound
                WzBinaryProperty jumpSound = (WzBinaryProperty)soundGameImage["Jump"];
                if (jumpSound != null)
                {
                    _soundManager.RegisterSound("Jump", jumpSound);
                }


                // Drop item sound (played on mob death)
                WzBinaryProperty dropItemSound = (WzBinaryProperty)soundGameImage["DropItem"];
                if (dropItemSound != null)
                {
                    _soundManager.RegisterSound("DropItem", dropItemSound);
                }


                // Pick up item sound
                WzBinaryProperty pickUpItemSound = (WzBinaryProperty)soundGameImage["PickUpItem"];
                if (pickUpItemSound != null)
                {
                    _soundManager.RegisterSound("PickUpItem", pickUpItemSound);
                }

                WzBinaryProperty loginEntryGameInSound = (WzBinaryProperty)soundGameImage["GameIn"];
                if (loginEntryGameInSound != null)
                {
                    _soundManager.RegisterSound(LoginEntryGameInSoundKey, loginEntryGameInSound);
                }
            }


            WzImage soundUiImage = Program.FindImage("Sound", "UI.img");
            WzBinaryProperty cooldownNoticeSound = soundUiImage?["DlgNotice"] as WzBinaryProperty;
            if (cooldownNoticeSound != null)
            {
                _soundManager.RegisterSound(SkillCooldownNoticeSoundKey, cooldownNoticeSound);
            }

            // `CWvsContext::OpenBook` and `CloseBook` both request StringPool id `0x924`.
            // The exact localized descriptor is still unresolved, so prefer the short WZ-backed UI cue here.
            WzBinaryProperty bookLifecycleSound = soundUiImage?["WorldmapOpen"] as WzBinaryProperty
                ?? soundUiImage?["WorldmapClose"] as WzBinaryProperty
                ?? soundUiImage?["BtMouseClick"] as WzBinaryProperty;
            if (bookLifecycleSound != null)
            {
                _soundManager.RegisterSound(BookDialogLifecycleSoundKey, bookLifecycleSound);
            }


            // Load meso icons from Item.wz/Special/0900.img

            LoadMesoIcons();



            // Load tombstone animation from Effect.wz/Tomb.img

            LoadTombstoneAnimation();
            LogStartupCheckpoint("LoadContent initialized audio/icons/tombstone assets");



            if (_mapBoard.VRRectangle == null)
            {
                _vrFieldBoundary = new Rectangle(0, 0, _mapBoard.MapSize.X, _mapBoard.MapSize.Y);
                _vrRectangle = new Rectangle(0, 0, _mapBoard.MapSize.X, _mapBoard.MapSize.Y);
            }
            else
            {
                _vrFieldBoundary = new Rectangle(
                    _mapBoard.VRRectangle.X + _mapBoard.CenterPoint.X, 
                    _mapBoard.VRRectangle.Y + _mapBoard.CenterPoint.Y, 
                    _mapBoard.VRRectangle.Width, 
                    _mapBoard.VRRectangle.Height);
                _vrRectangle = new Rectangle(_mapBoard.VRRectangle.X, _mapBoard.VRRectangle.Y, _mapBoard.VRRectangle.Width, _mapBoard.VRRectangle.Height);
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
            Task t_tiles = Task.Run(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                foreach (LayeredItem tileObj in _mapBoard.BoardItems.TileObjs)
                {
                    WzImageProperty tileParent = (WzImageProperty)tileObj.BaseInfo.ParentObject;
                    BaseDXDrawableItem mapItem = MapSimulatorLoader.CreateMapItemFromProperty(
                        _texturePool,
                        tileParent,
                        tileObj.X,
                        tileObj.Y,
                        _mapBoard.CenterPoint,
                        _DxDeviceManager.GraphicsDevice,
                        usedProps,
                        tileObj is IFlippable flippable && flippable.Flip);
                    if (mapItem == null)
                    {
                        continue;
                    }


                    RegisterQuestGatedMapObject(mapItem, tileObj, questGatedMapObjects);
                    mapObjects[tileObj.LayerNumber].Add(mapItem);
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Tile task loaded {_mapBoard.BoardItems.TileObjs.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            // Background
            Task t_Background = Task.Run(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                foreach (BackgroundInstance background in _mapBoard.BoardItems.BackBackgrounds)
                {
                    WzImageProperty bgParent = (WzImageProperty)background.BaseInfo.ParentObject;
                    BackgroundItem bgItem = MapSimulatorLoader.CreateBackgroundFromProperty(_texturePool, bgParent, background, _DxDeviceManager.GraphicsDevice, usedProps, background.Flip);


                    if (bgItem != null)
                        backgrounds_back.Add(bgItem);
                }
                foreach (BackgroundInstance background in _mapBoard.BoardItems.FrontBackgrounds)
                {
                    WzImageProperty bgParent = (WzImageProperty)background.BaseInfo.ParentObject;
                    BackgroundItem bgItem = MapSimulatorLoader.CreateBackgroundFromProperty(_texturePool, bgParent, background, _DxDeviceManager.GraphicsDevice, usedProps, background.Flip);


                    if (bgItem != null)
                        backgrounds_front.Add(bgItem);
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Background task loaded {_mapBoard.BoardItems.BackBackgrounds.Count + _mapBoard.BoardItems.FrontBackgrounds.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            // Reactors
            Task t_reactor = Task.Run(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                foreach (ReactorInstance reactor in _mapBoard.BoardItems.Reactors)
                {
                    //WzImage imageProperty = (WzImage)NPCWZFile[reactorInfo.ID + ".img"];


                    ReactorItem reactorItem = MapSimulatorLoader.CreateReactorFromProperty(_texturePool, reactor, _DxDeviceManager.GraphicsDevice, usedProps);
                    if (reactorItem != null)
                        mapObjects_Reactors.Add(reactorItem);
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Reactor task loaded {_mapBoard.BoardItems.Reactors.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            // NPCs
            Task t_npc = Task.Run(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                int loadedCount = 0;
                foreach (NpcInstance npc in _mapBoard.BoardItems.NPCs)
                {
                    //WzImage imageProperty = (WzImage) NPCWZFile[npcInfo.ID + ".img"];
                    if (npc.Hide)
                        continue;


                    NpcItem npcItem = MapSimulatorLoader.CreateNpcFromProperty(
                        _texturePool,
                        npc,
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
                Debug.WriteLine($"[MapLoad] NPC task loaded {loadedCount}/{_mapBoard.BoardItems.NPCs.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            // Mobs
            Task t_mobs = Task.Run(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                int loadedCount = 0;
                foreach (MobInstance mob in _mapBoard.BoardItems.Mobs)
                {
                    //WzImage imageProperty = Program.WzManager.FindMobImage(mobInfo.ID); // Mob.wz Mob2.img Mob001.wz
                    if (mob.Hide)
                        continue;


                    MobItem npcItem = MapSimulatorLoader.CreateMobFromProperty(_texturePool, mob, UserScreenScaleFactor, _DxDeviceManager.GraphicsDevice, _soundManager, usedProps);
                    npcItem?.SetAnimationEffects(_animationEffects);
                    ConfigureMobAutoSkillSelection(npcItem);



                    mapObjects_Mobs.Add(npcItem);
                    loadedCount++;
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Mob task loaded {loadedCount}/{_mapBoard.BoardItems.Mobs.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });



            // Portals
            Task t_portal = Task.Run(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                int loadedCount = 0;
                WzSubProperty portalParent = (WzSubProperty) mapHelperImage["portal"];


                WzSubProperty gameParent = (WzSubProperty)portalParent["game"];

                //WzSubProperty editorParent = (WzSubProperty) portalParent["editor"];



                foreach (PortalInstance portal in _mapBoard.BoardItems.Portals)
                {
                    PortalItem portalItem = MapSimulatorLoader.CreatePortalFromProperty(_texturePool, gameParent, portal, _DxDeviceManager.GraphicsDevice, usedProps);
                    if (portalItem != null)
                    {
                        mapObjects_Portal.Add(portalItem);
                        loadedCount++;
                    }
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Portal task loaded {loadedCount}/{_mapBoard.BoardItems.Portals.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            // Tooltips
            Task t_tooltips = Task.Run(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                WzSubProperty farmFrameParent = (WzSubProperty) uiToolTipImage?["Item"]?["FarmFrame"]; // not exist before V update.
                foreach (ToolTipInstance tooltip in _mapBoard.BoardItems.ToolTips)
                {
                    TooltipItem item = MapSimulatorLoader.CreateTooltipFromProperty(_texturePool, UserScreenScaleFactor, farmFrameParent, tooltip, _DxDeviceManager.GraphicsDevice);


                    mapObjects_tooltips.Add(item);

                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Tooltip task loaded {_mapBoard.BoardItems.ToolTips.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });



            // Cursor
            Task t_cursor = Task.Run(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                WzImageProperty cursorImageProperty = (WzImageProperty)uiBasicImage["Cursor"];
                this.mouseCursor = MapSimulatorLoader.CreateMouseCursorFromProperty(_texturePool, cursorImageProperty, 0, 0, _DxDeviceManager.GraphicsDevice, usedProps, false);
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Cursor task finished in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            // Minimap
            Task t_minimap = Task.Run(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                if (!_gameState.IsLoginMap && !_mapBoard.MapInfo.hideMinimap && !_gameState.IsCashShopMap)
                {
                    miniMapUi = MapSimulatorLoader.CreateMinimapFromProperty(uiWindow1Image, uiWindow2Image, uiBasicImage, _mapBoard, GraphicsDevice, UserScreenScaleFactor, _mapBoard.MapInfo.strMapName, _mapBoard.MapInfo.strStreetName, soundUIImage, _gameState.IsBigBangUpdate);
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
            Task t_statusBar = Task.Run(() => {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                if (!_gameState.IsLoginMap && !_gameState.IsCashShopMap) {
                    Tuple<StatusBarUI, StatusBarChatUI> statusBar = MapSimulatorLoader.CreateStatusBarFromProperty(uiStatusBarImage, uiStatus2BarImage, uiBasicImage, uiBuffIconImage, _mapBoard, GraphicsDevice, UserScreenScaleFactor, _renderParams, soundUIImage, _gameState.IsBigBangUpdate);
                    if (statusBar != null) {
                        statusBarUi = statusBar.Item1;
                        statusBarChatUI = statusBar.Item2;
                    }
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Status bar task finished in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            while (!t_tiles.IsCompleted || !t_Background.IsCompleted || !t_reactor.IsCompleted || !t_npc.IsCompleted || !t_mobs.IsCompleted || !t_portal.IsCompleted ||
                !t_tooltips.IsCompleted || !t_cursor.IsCompleted || !t_minimap.IsCompleted || !t_statusBar.IsCompleted)
            {
                Thread.Sleep(100);
            }


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
                        _renderParams.RenderHeight);


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
                        _renderParams.RenderHeight);
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
                        _renderParams.RenderHeight);
                }
                else
                {
                    Stopwatch uiWindowCreationStopwatch = Stopwatch.StartNew();
                    uiWindowManager = UIWindowLoader.CreateUIWindowManager(
                        uiWindow1Image, uiWindow2Image, uiBasicImage, soundUIImage,
                        skillWzFile, stringWzFile, uiMapleTvImage,
                        GraphicsDevice, _renderParams.RenderWidth, _renderParams.RenderHeight, _gameState.IsBigBangUpdate, storageAccountLabel: BuildStorageAccountLabel(), storageAccountKey: BuildStorageAccountKey());
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
                _renderParams.RenderHeight);
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
                cashAvatarPreviewReload.PersonalShopRequested = () => ShowSocialRoomWindowForCallback(
                    SocialRoomKind.PersonalShop,
                    "CCSWnd_Char::ShowPersonalShop opened the dedicated personal-shop owner.");
                cashAvatarPreviewReload.EntrustedShopRequested = () => ShowSocialRoomWindowForCallback(
                    SocialRoomKind.EntrustedShop,
                    "CCSWnd_Char::ShowEntrustedShop opened the dedicated entrusted-shop owner.");
                cashAvatarPreviewReload.TradingRoomRequested = () =>
                {
                    ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.CashTradingRoom);
                    return "CCSWnd_Char handed the selected listing to CCashTradingRoomDlg.";
                };
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
                _playerManager?.Loader);

            ///////////////////////////////////////////////
            ////// Default positioning for character //////
            ///////////////////////////////////////////////
            ResolveSpawnPosition(out float spawnX, out float spawnY);

            ClampLegacyCameraToBoundaries();



            ///////////////////////////////////////////////
            ///////// Initialize Player Manager ///////////
            ///////////////////////////////////////////////
            // Store map center point for camera calculations
            _mapCenterX = _mapBoard.CenterPoint.X;
            _mapCenterY = _mapBoard.CenterPoint.Y;
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
            _specialFieldRuntime.BindMap(_mapBoard);
            ApplyClientOwnedFieldWrappers();
            _packetFieldStateRuntime.Initialize(GraphicsDevice, _mapBoard?.MapInfo);
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
            if (_mapBoard.MapInfo.LBSide != null)
            {
                _lbSide = (int)_mapBoard.MapInfo.LBSide;
                this._lbTextureLeft = CreateLBBorder(LB_BORDER_WIDTHHEIGHT + _lbSide, this.Height, _DxDeviceManager.GraphicsDevice);
                this._lbTextureRight = CreateLBBorder(LB_BORDER_WIDTHHEIGHT + _lbSide, this.Height, _DxDeviceManager.GraphicsDevice);
            }
            if (_mapBoard.MapInfo.LBTop != null)
            {
                _lbTop = (int)_mapBoard.MapInfo.LBTop;
                this._lbTextureTop = CreateLBBorder((int) (_vrFieldBoundary.Width * 1.45), LB_BORDER_WIDTHHEIGHT + _lbTop, _DxDeviceManager.GraphicsDevice); // add a little more width to the top LB border for very small maps
            }
            if (_mapBoard.MapInfo.LBBottom != null)
            {
                _lbBottom = (int)_mapBoard.MapInfo.LBBottom;
                this._lbTextureBottom = CreateLBBorder((int) (_vrFieldBoundary.Width * 1.45), LB_BORDER_WIDTHHEIGHT + _lbBottom, _DxDeviceManager.GraphicsDevice);
            }


            // Set border data on RenderingManager

            _renderingManager.SetVRBorderData(_vrFieldBoundary, _drawVRBorderLeftRight, _vrBoundaryTextureLeft, _vrBoundaryTextureRight);

            _renderingManager.SetLBBorderData(_lbTextureLeft, _lbTextureRight);



            ///////////////////////////////////////////////



            // mirror bottom boundaries
            //_mirrorBottomRect
            if (_mapBoard.MapInfo.mirror_Bottom)
            {
                if (_mapBoard.MapInfo.VRLeft != null && _mapBoard.MapInfo.VRRight != null)
                {
                    int vr_width = (int)_mapBoard.MapInfo.VRRight - (int)_mapBoard.MapInfo.VRLeft;
                    const int obj_mirrorBottom_height = 200;


                    _mirrorBottomRect = new Rectangle((int)_mapBoard.MapInfo.VRLeft, (int)_mapBoard.MapInfo.VRBottom - obj_mirrorBottom_height, vr_width, obj_mirrorBottom_height);



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
            _npcInteractionOverlay = new NpcInteractionOverlay(GraphicsDevice);
            _npcInteractionOverlay.SetFont(_fontChat);
            RegisterChatCommands();
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


            _temporaryPortalField = new TemporaryPortalField(_texturePool, _DxDeviceManager.GraphicsDevice);



            // Initialize combat effects (damage numbers, hit effects)

            _combatEffects.Initialize(_DxDeviceManager.GraphicsDevice, _fontDebugValues);
            _combatEffects.SetAnimationEffects(_animationEffects);
            _combatEffects.SetAnimationDisplayerSpecialTextSink(HandleAnimationDisplayerCombatFeedbackRequested);



            // Load damage number sprites from Effect.wz/BasicEff.img
            // This enables authentic MapleStory digit sprites for damage numbers
            var basicEffImage = Program.FindImage("Effect", "BasicEff.img");
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
                _playerManager?.Skills?.ConfigureBuffIconCatalog(UILoader.LoadBuffIconCatalogEntries(uiBuffIconImage));
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
                    var skills = _playerManager?.Skills;
                    if (skills == null)
                    {
                        return;
                    }

                    using var _ = skills.BeginClientCancelBatchScope();
                    skills.RequestClientSkillCancel(skillId, currTickCount, enforceFieldCancelRestrictions: true);
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
                bookCollectionWindow.CharacterBuild = _playerManager?.Player?.Build ?? _loginCharacterRoster.SelectedEntry?.Build;
                bookCollectionWindow.SetFont(_fontDebugValues);
                bookCollectionWindow.SetCollectionSnapshotProvider(null);
                bookCollectionWindow.SetMonsterBookSnapshotProvider(BuildActiveMonsterBookSnapshot);
                bookCollectionWindow.SetMonsterBookRegistrationHandler(ApplyActiveMonsterBookRegistration);
                bookCollectionWindow.ClientCloseRequested = HandleBookCollectionCloseRequested;
                bookCollectionWindow.OpenRequested = HandleBookCollectionOpened;
                bookCollectionWindow.ClosingRequested = HandleBookCollectionClosing;
                bookCollectionWindow.CloseRequested = HandleBookCollectionClosed;
                bookCollectionWindow.OnImeCandidateSelected = TrySelectBookCollectionImeCandidate;
                bookCollectionWindow.ResolveImeWindowHandle = () => Window?.Handle ?? IntPtr.Zero;

            }

            if (uiWindowManager?.EquipWindow != null && _playerManager?.Player?.Build != null)
            {
                uiWindowManager.EquipWindow.CharacterBuild = _playerManager.Player.Build;
                uiWindowManager.EquipWindow.SetFont(_fontChat);
                if (uiWindowManager.EquipWindow is EquipUI equipWindow)
                {
                    equipWindow.SetPetController(_playerManager.Pets);
                    equipWindow.SetDragonEquipmentController(_playerManager.CompanionEquipment?.Dragon);
                    equipWindow.EquipmentChangeSubmitted = SubmitEquipmentChangeRequest;
                    equipWindow.EquipmentChangeResultRequested = TryResolveEquipmentChangeRequest;
                    equipWindow.EquipmentDragStartBlocked = ShouldBlockEquipmentDragStart;
                }
                if (uiWindowManager.EquipWindow is EquipUIBigBang equipBigBang)
                {
                    equipBigBang.SetCharacterLoader(_playerManager.Loader);
                    equipBigBang.EquipmentChangeSubmitted = SubmitEquipmentChangeRequest;
                    equipBigBang.EquipmentChangeResultRequested = TryResolveEquipmentChangeRequest;
                    equipBigBang.EquipmentDragStartBlocked = ShouldBlockEquipmentDragStart;
                    equipBigBang.SetPetController(_playerManager.Pets);

                    equipBigBang.SetPetEquipmentController(_playerManager.CompanionEquipment?.Pet);

                    equipBigBang.SetDragonEquipmentController(_playerManager.CompanionEquipment?.Dragon);

                    equipBigBang.SetMechanicEquipmentController(_playerManager.CompanionEquipment?.Mechanic);
                    equipBigBang.SetMechanicPaneAvailable(
                        CompanionEquipmentController.HasMechanicOwnerState(_playerManager?.Player?.Build)
                        && FieldInteractionRestrictionEvaluator.CanUseTamingMob(_mapBoard?.MapInfo?.fieldLimit ?? 0));
                    equipBigBang.SetAndroidEquipmentController(_playerManager.CompanionEquipment?.Android);
                    equipBigBang.SetAndroidPaneAvailable(FieldInteractionRestrictionEvaluator.CanUseAndroid(_mapBoard?.MapInfo?.fieldLimit ?? 0));
                }
            }
            if (uiWindowManager?.InventoryWindow is InventoryUI inventoryWindow && _playerManager?.Player?.Build != null)
            {
                inventoryWindow.CharacterBuild = _playerManager.Player.Build;
                inventoryWindow.SetFont(_fontChat);
                inventoryWindow.SetCharacterLoader(_playerManager.Loader);
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
                cashAvatarPreviewRebuild.PersonalShopRequested = () => ShowSocialRoomWindowForCallback(
                    SocialRoomKind.PersonalShop,
                    "CCSWnd_Char::ShowPersonalShop opened the dedicated personal-shop owner.");
                cashAvatarPreviewRebuild.EntrustedShopRequested = () => ShowSocialRoomWindowForCallback(
                    SocialRoomKind.EntrustedShop,
                    "CCSWnd_Char::ShowEntrustedShop opened the dedicated entrusted-shop owner.");
                cashAvatarPreviewRebuild.TradingRoomRequested = () =>
                {
                    ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.CashTradingRoom);
                    return "CCSWnd_Char handed the selected listing to CCashTradingRoomDlg.";
                };
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
            foreach (WzObject obj in usedProps)
            {
                if (obj == null)
                    continue; // obj copied twice in usedProps?


                // Spine events
                WzSpineObject spineObj = (WzSpineObject) obj.MSTagSpine;
                if (spineObj != null)
                {
                    EnsureSpineRenderer();
                    spineObj.state.Start += Start;
                    spineObj.state.End += End;
                    spineObj.state.Complete += Complete;
                    spineObj.state.Event += Event;
                }


                obj.MSTag = null;
                obj.MSTagSpine = null; // cleanup
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
            miniMapUi = null;
            statusBarUi = null;
            statusBarChatUI = null;
            mouseCursor = null;


            // Clear mirror boundaries

            _mirrorBottomRect = new Rectangle();

            _mirrorBottomReflection = null;



            // Note: Don't call _texturePool.DisposeAll() here - it would dispose textures

            // still in use by preserved components (mouse cursor). The TexturePool has

            // TTL-based cleanup that will automatically dispose unused textures after 5 minutes.



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
                _lastPacketOwnedTeleportOutboundOpcode = -1;
                _lastPacketOwnedTeleportOutboundPayload = Array.Empty<byte>();
                _lastPacketOwnedTeleportOutboundSummary = null;
                _lastCollisionVerticalJumpMovePathAttribute = -1;
                _lastCollisionVerticalJumpMovePathPayload = Array.Empty<byte>();
                _lastCollisionCustomImpactMovePathAttribute = -1;
                _lastCollisionCustomImpactMovePathPayload = Array.Empty<byte>();
                _pendingMapSpawnTarget = null;
            }
            ClearPendingPortalSessionValueImpacts();
            ClearPassiveTransferRequest();


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
        private void LoadMapContent(Board newBoard, string newTitle, string spawnPortalName, int spawnPortalIndex = -1, string[] spawnPortalNameCandidates = null)
        {
            Stopwatch loadMapContentStopwatch = Stopwatch.StartNew();
            this._mapBoard = newBoard;
            this._spawnPortalName = spawnPortalName;
            this._spawnPortalNameCandidates = spawnPortalNameCandidates ?? Array.Empty<string>();
            this._spawnPortalIndex = spawnPortalIndex;


            // Update window title

            Window.Title = newTitle;



            // Update map type flags
            string[] titleNameParts = newTitle.Split(':');
            _gameState.IsLoginMap = titleNameParts.All(part => part.Contains("MapLogin"));
            _gameState.IsCashShopMap = titleNameParts.All(part => part.Contains("CashShopPreview"));


            // Regenerate minimap if needed

            if (_mapBoard.MiniMap == null)

                _mapBoard.RegenerateMinimap();



            // Load WZ images needed for this map
            WzImage mapHelperImage = Program.FindImage("Map", "MapHelper.img");
            WzImage soundUIImage = Program.FindImage("Sound", "UI.img");
            WzImage uiToolTipImage = Program.FindImage("UI", "UIToolTip.img");
            WzImage uiBasicImage = Program.FindImage("UI", "Basic.img");
            WzImage uiLoginImage = Program.FindImage("UI", "Login.img");
            WzImage uiWindow1Image = Program.FindImage("UI", "UIWindow.img");
            WzImage uiWindow2Image = Program.FindImage("UI", "UIWindow2.img");
            WzImage uiMapleTvImage = Program.FindImage("UI", "MapleTV.img");
            WzImage uiGuildBbsImage = Program.FindImage("UI", "GuildBBS.img");
            WzImage uiBuffIconImage = Program.FindImage("UI", "BuffIcon.img");
            WzImage uiStatusBarImage = Program.FindImage("UI", "StatusBar.img");
            WzImage uiStatus2BarImage = Program.FindImage("UI", "StatusBar2.img");


            // Skill.wz and String.wz for skill window content
            WzFile skillWzFile = null;
            WzFile stringWzFile = null;
            try
            {
                var fileManager = WzFileManager.fileManager;
                if (fileManager != null)
                {
                    var skillDir = fileManager["skill"];
                    skillWzFile = skillDir?.WzFileParent;
                    var stringDir = fileManager["string"];
                    stringWzFile = stringDir?.WzFileParent;
                }
            }
            catch { }


            // BGM - only reload if different from current BGM

            _mapBgmName = _mapBoard.MapInfo.bgm;

            ApplyRequestedBgm(_specialFieldBgmOverrideName ?? _mapBgmName);



            // VR boundaries
            if (_mapBoard.VRRectangle == null)
            {
                _vrFieldBoundary = new Rectangle(0, 0, _mapBoard.MapSize.X, _mapBoard.MapSize.Y);
                _vrRectangle = new Rectangle(0, 0, _mapBoard.MapSize.X, _mapBoard.MapSize.Y);
            }
            else
            {
                _vrFieldBoundary = new Rectangle(
                    _mapBoard.VRRectangle.X + _mapBoard.CenterPoint.X,
                    _mapBoard.VRRectangle.Y + _mapBoard.CenterPoint.Y,
                    _mapBoard.VRRectangle.Width,
                    _mapBoard.VRRectangle.Height);
                _vrRectangle = new Rectangle(_mapBoard.VRRectangle.X, _mapBoard.VRRectangle.Y, _mapBoard.VRRectangle.Width, _mapBoard.VRRectangle.Height);
            }


            // Initialize layer lists
            for (int i = 0; i < mapObjects.Length; i++)
            {
                mapObjects[i] = new List<BaseDXDrawableItem>();
            }


            ConcurrentBag<WzObject> usedProps = new ConcurrentBag<WzObject>();

            ConcurrentDictionary<BaseDXDrawableItem, QuestGatedMapObjectState> questGatedMapObjects = new();



            // Load map objects in parallel
            Task t_tiles = Task.Run(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                foreach (LayeredItem tileObj in _mapBoard.BoardItems.TileObjs)
                {
                    WzImageProperty tileParent = (WzImageProperty)tileObj.BaseInfo.ParentObject;
                    BaseDXDrawableItem mapItem = MapSimulatorLoader.CreateMapItemFromProperty(
                        _texturePool,
                        tileParent,
                        tileObj.X,
                        tileObj.Y,
                        _mapBoard.CenterPoint,
                        _DxDeviceManager.GraphicsDevice,
                        usedProps,
                        tileObj is IFlippable flippable && flippable.Flip);
                    if (mapItem == null)
                    {
                        continue;
                    }


                    RegisterQuestGatedMapObject(mapItem, tileObj, questGatedMapObjects);
                    mapObjects[tileObj.LayerNumber].Add(mapItem);
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Tiles task loaded {_mapBoard.BoardItems.TileObjs.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            Task t_Background = Task.Run(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                foreach (BackgroundInstance background in _mapBoard.BoardItems.BackBackgrounds)
                {
                    WzImageProperty bgParent = (WzImageProperty)background.BaseInfo.ParentObject;
                    BackgroundItem bgItem = MapSimulatorLoader.CreateBackgroundFromProperty(_texturePool, bgParent, background, _DxDeviceManager.GraphicsDevice, usedProps, background.Flip);
                    if (bgItem != null)
                        backgrounds_back.Add(bgItem);
                }
                foreach (BackgroundInstance background in _mapBoard.BoardItems.FrontBackgrounds)
                {
                    WzImageProperty bgParent = (WzImageProperty)background.BaseInfo.ParentObject;
                    BackgroundItem bgItem = MapSimulatorLoader.CreateBackgroundFromProperty(_texturePool, bgParent, background, _DxDeviceManager.GraphicsDevice, usedProps, background.Flip);
                    if (bgItem != null)
                        backgrounds_front.Add(bgItem);
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Background task loaded {_mapBoard.BoardItems.BackBackgrounds.Count + _mapBoard.BoardItems.FrontBackgrounds.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            Task t_reactor = Task.Run(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                foreach (ReactorInstance reactor in _mapBoard.BoardItems.Reactors)
                {
                    ReactorItem reactorItem = MapSimulatorLoader.CreateReactorFromProperty(_texturePool, reactor, _DxDeviceManager.GraphicsDevice, usedProps);
                    if (reactorItem != null)
                        mapObjects_Reactors.Add(reactorItem);
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Reactor task loaded {_mapBoard.BoardItems.Reactors.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            Task t_npc = Task.Run(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                int loadedCount = 0;
                foreach (NpcInstance npc in _mapBoard.BoardItems.NPCs)
                {
                    if (npc.Hide)
                        continue;
                    NpcItem npcItem = MapSimulatorLoader.CreateNpcFromProperty(
                        _texturePool,
                        npc,
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
                Debug.WriteLine($"[MapLoad] NPC task loaded {loadedCount}/{_mapBoard.BoardItems.NPCs.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            Task t_mobs = Task.Run(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                int loadedCount = 0;
                foreach (MobInstance mob in _mapBoard.BoardItems.Mobs)
                {
                    if (mob.Hide)
                        continue;
                    MobItem mobItem = MapSimulatorLoader.CreateMobFromProperty(_texturePool, mob, UserScreenScaleFactor, _DxDeviceManager.GraphicsDevice, _soundManager, usedProps);
                    mobItem?.SetAnimationEffects(_animationEffects);
                    ConfigureMobAutoSkillSelection(mobItem);
                    mapObjects_Mobs.Add(mobItem);
                    loadedCount++;
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Mob task loaded {loadedCount}/{_mapBoard.BoardItems.Mobs.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            Task t_portal = Task.Run(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                int loadedCount = 0;
                WzSubProperty portalParent = (WzSubProperty)mapHelperImage["portal"];
                WzSubProperty gameParent = (WzSubProperty)portalParent["game"];
                foreach (PortalInstance portal in _mapBoard.BoardItems.Portals)
                {
                    PortalItem portalItem = MapSimulatorLoader.CreatePortalFromProperty(_texturePool, gameParent, portal, _DxDeviceManager.GraphicsDevice, usedProps);
                    if (portalItem != null)
                    {
                        mapObjects_Portal.Add(portalItem);
                        loadedCount++;
                    }
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Portal task loaded {loadedCount}/{_mapBoard.BoardItems.Portals.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            Task t_tooltips = Task.Run(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                WzSubProperty farmFrameParent = (WzSubProperty)uiToolTipImage?["Item"]?["FarmFrame"];
                foreach (ToolTipInstance tooltip in _mapBoard.BoardItems.ToolTips)
                {
                    TooltipItem item = MapSimulatorLoader.CreateTooltipFromProperty(_texturePool, UserScreenScaleFactor, farmFrameParent, tooltip, _DxDeviceManager.GraphicsDevice);
                    mapObjects_tooltips.Add(item);
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Tooltip task loaded {_mapBoard.BoardItems.ToolTips.Count} items in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            Task t_minimap = Task.Run(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                if (!_gameState.IsLoginMap && !_mapBoard.MapInfo.hideMinimap && !_gameState.IsCashShopMap)
                {
                    miniMapUi = MapSimulatorLoader.CreateMinimapFromProperty(uiWindow1Image, uiWindow2Image, uiBasicImage, _mapBoard, GraphicsDevice, UserScreenScaleFactor, _mapBoard.MapInfo.strMapName, _mapBoard.MapInfo.strStreetName, soundUIImage, _gameState.IsBigBangUpdate);
                    miniMapUi?.ReloadMiniMap(_packetOwnedMiniMapOnOffVisible);
                    if (_packetFieldUtilityMinimapHiddenByAdminResult)
                    {
                        miniMapUi?.EnsureCollapsed();
                    }
                }
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Minimap task finished in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            Task t_statusBar = Task.Run(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                if (!_gameState.IsLoginMap && !_gameState.IsCashShopMap)
                {
                    Tuple<StatusBarUI, StatusBarChatUI> statusBar = MapSimulatorLoader.CreateStatusBarFromProperty(uiStatusBarImage, uiStatus2BarImage, uiBasicImage, uiBuffIconImage, _mapBoard, GraphicsDevice, UserScreenScaleFactor, _renderParams, soundUIImage, _gameState.IsBigBangUpdate);
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
            Task t_cursor = Task.Run(() =>
            {
                Stopwatch taskStopwatch = Stopwatch.StartNew();
                WzImageProperty cursorImageProperty = (WzImageProperty)uiBasicImage["Cursor"];
                this.mouseCursor = MapSimulatorLoader.CreateMouseCursorFromProperty(_texturePool, cursorImageProperty, 0, 0, _DxDeviceManager.GraphicsDevice, usedProps, false);
                taskStopwatch.Stop();
                Debug.WriteLine($"[MapLoad] Cursor task finished in {taskStopwatch.ElapsedMilliseconds} ms");
            });


            // Wait for all loading tasks

            ThreadPool.GetAvailableThreads(out int workerThreadsBeforeWait, out int completionPortThreadsBeforeWait);
            Stopwatch waitAllStopwatch = Stopwatch.StartNew();
            Task.WaitAll(t_tiles, t_Background, t_reactor, t_npc, t_mobs, t_portal, t_tooltips, t_minimap, t_statusBar, t_cursor);
            waitAllStopwatch.Stop();
            LogStartupCheckpoint("LoadContent completed parallel asset tasks");
            ThreadPool.GetAvailableThreads(out int workerThreadsAfterWait, out int completionPortThreadsAfterWait);
            Debug.WriteLine($"[MapLoad] Task.WaitAll blocked for {waitAllStopwatch.ElapsedMilliseconds} ms (worker threads before/after: {workerThreadsBeforeWait}/{workerThreadsAfterWait}, IO before/after: {completionPortThreadsBeforeWait}/{completionPortThreadsAfterWait})");
            Debug.WriteLine($"[MapLoad] Parallel asset tasks finished in {loadMapContentStopwatch.ElapsedMilliseconds} ms");



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
                        _renderParams.RenderHeight);
                    UIWindowLoader.RegisterLoginCharacterDetailWindow(
                        uiWindowManager,
                        uiBasicImage,
                        soundUIImage,
                        GraphicsDevice,
                        _renderParams.RenderWidth,
                        _renderParams.RenderHeight);
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
                        _renderParams.RenderHeight);
                }
                else if (uiWindowManager == null || uiWindowManager.InventoryWindow == null)
                {
                    uiWindowManager = UIWindowLoader.CreateUIWindowManager(
                        uiWindow1Image, uiWindow2Image, uiBasicImage, soundUIImage,
                        skillWzFile, stringWzFile, uiMapleTvImage,
                        GraphicsDevice, _renderParams.RenderWidth, _renderParams.RenderHeight, _gameState.IsBigBangUpdate, storageAccountLabel: BuildStorageAccountLabel(), storageAccountKey: BuildStorageAccountKey());
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
            _engagementProposalController.SocialMessagesObserved = TryTriggerSpecialistPetSocialFeedback;
            _engagementProposalController.WireWindow(uiWindowManager, _playerManager?.Player?.Build, _fontChat, ShowUtilityFeedbackMessage);
            _weddingWishListController.SocialChatObserved = TryTriggerSpecialistPetSocialFeedback;
            _weddingWishListController.ClientPacketDispatcher = DispatchWeddingWishListClientRequest;
            _weddingInvitationController.SocialMessagesObserved = TryTriggerSpecialistPetSocialFeedback;
            _weddingInvitationController.WireWindow(uiWindowManager, _playerManager?.Player?.Build, _fontChat, ShowUtilityFeedbackMessage);
            _weddingWishListController.WireWindow(uiWindowManager, _playerManager?.Player?.Build, uiWindowManager?.InventoryWindow as IInventoryRuntime, _fontChat, ShowUtilityFeedbackMessage);
            WireProgressionUtilityWindowLaunchers();
            LogStartupCheckpoint("LoadContent created UI windows");

            RefreshMapTransferWindow();

            RefreshWorldMapWindow();



            // Initialize status bar character stats display after map change

            if (statusBarUi != null)

            {

                _playerManager?.Skills?.ConfigureBuffIconCatalog(UILoader.LoadBuffIconCatalogEntries(uiBuffIconImage));
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
                    var skills = _playerManager?.Skills;
                    if (skills == null)
                    {
                        return;
                    }

                    using var _ = skills.BeginClientCancelBatchScope();
                    skills.RequestClientSkillCancel(skillId, currTickCount, enforceFieldCancelRestrictions: true);
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
                    equipWindow.EquipmentChangeSubmitted = SubmitEquipmentChangeRequest;
                    equipWindow.EquipmentChangeResultRequested = TryResolveEquipmentChangeRequest;
                    equipWindow.EquipmentDragStartBlocked = ShouldBlockEquipmentDragStart;
                }
                if (uiWindowManager.EquipWindow is EquipUIBigBang equipBigBang)
                {
                    equipBigBang.SetCharacterLoader(_playerManager.Loader);
                    equipBigBang.EquipmentChangeSubmitted = SubmitEquipmentChangeRequest;
                    equipBigBang.EquipmentChangeResultRequested = TryResolveEquipmentChangeRequest;
                    equipBigBang.EquipmentDragStartBlocked = ShouldBlockEquipmentDragStart;
                    equipBigBang.SetPetController(_playerManager.Pets);

                    equipBigBang.SetPetEquipmentController(_playerManager.CompanionEquipment?.Pet);

                    equipBigBang.SetDragonEquipmentController(_playerManager.CompanionEquipment?.Dragon);

                    equipBigBang.SetMechanicEquipmentController(_playerManager.CompanionEquipment?.Mechanic);
                    equipBigBang.SetMechanicPaneAvailable(
                        CompanionEquipmentController.HasMechanicOwnerState(_playerManager?.Player?.Build)
                        && FieldInteractionRestrictionEvaluator.CanUseTamingMob(_mapBoard?.MapInfo?.fieldLimit ?? 0));
                    equipBigBang.SetAndroidEquipmentController(_playerManager.CompanionEquipment?.Android);
                    equipBigBang.SetAndroidPaneAvailable(FieldInteractionRestrictionEvaluator.CanUseAndroid(_mapBoard?.MapInfo?.fieldLimit ?? 0));
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
                cashAvatarPreviewWindow.PersonalShopRequested = () => ShowSocialRoomWindowForCallback(
                    SocialRoomKind.PersonalShop,
                    "CCSWnd_Char::ShowPersonalShop opened the dedicated personal-shop owner.");
                cashAvatarPreviewWindow.EntrustedShopRequested = () => ShowSocialRoomWindowForCallback(
                    SocialRoomKind.EntrustedShop,
                    "CCSWnd_Char::ShowEntrustedShop opened the dedicated entrusted-shop owner.");
                cashAvatarPreviewWindow.TradingRoomRequested = () =>
                {
                    ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.CashTradingRoom);
                    return "CCSWnd_Char handed the selected listing to CCashTradingRoomDlg.";
                };
                cashAvatarPreviewWindow.WeatherRequested = PreviewCashAvatarWeatherAction;
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Mts) is AdminShopDialogUI mtsWindow)
            {
                mtsWindow.SetInventory(uiWindowManager.InventoryWindow as IInventoryRuntime);
            }


            // Initialize mob foothold references

            InitializeMobFootholds();



            // Convert lists to arrays

            ConvertListsToArrays();



            // Set camera position and spawn point

            ResolveSpawnPosition(out float spawnX, out float spawnY);


            ClampLegacyCameraToBoundaries();


            // Store map center point for camera calculations

            _mapCenterX = _mapBoard.CenterPoint.X;

            _mapCenterY = _mapBoard.CenterPoint.Y;



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
            _specialFieldRuntime.BindMap(_mapBoard);
            ApplyClientOwnedFieldWrappers();
            _packetFieldStateRuntime.Initialize(GraphicsDevice, _mapBoard?.MapInfo);
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


            // Auto-detect transport maps (CField_ContiMove) from ShipObject in map

            DetectAndInitializeTransportField();
            ApplyTransitAndVoyageFieldWrapper(_mapBoard?.MapInfo);
            LogStartupCheckpoint("LoadContent initialized transport/camera runtime");


            // Create border textures
            int leftRightVRDifference = (int)((_vrFieldBoundary.Right - _vrFieldBoundary.Left) * _renderParams.RenderObjectScaling);
            if (leftRightVRDifference < _renderParams.RenderWidth)
            {
                this._drawVRBorderLeftRight = true;
                this._vrBoundaryTextureLeft = CreateVRBorder(VR_BORDER_WIDTHHEIGHT, _vrFieldBoundary.Height, _DxDeviceManager.GraphicsDevice);
                this._vrBoundaryTextureRight = CreateVRBorder(VR_BORDER_WIDTHHEIGHT, _vrFieldBoundary.Height, _DxDeviceManager.GraphicsDevice);
                this._vrBoundaryTextureTop = CreateVRBorder(_vrFieldBoundary.Width * 2, VR_BORDER_WIDTHHEIGHT, _DxDeviceManager.GraphicsDevice);
                this._vrBoundaryTextureBottom = CreateVRBorder(_vrFieldBoundary.Width * 2, VR_BORDER_WIDTHHEIGHT, _DxDeviceManager.GraphicsDevice);
            }


            // LB borders
            if (_mapBoard.MapInfo.LBSide != null)
            {
                _lbSide = (int)_mapBoard.MapInfo.LBSide;
                this._lbTextureLeft = CreateLBBorder(LB_BORDER_WIDTHHEIGHT + _lbSide, this.Height, _DxDeviceManager.GraphicsDevice);
                this._lbTextureRight = CreateLBBorder(LB_BORDER_WIDTHHEIGHT + _lbSide, this.Height, _DxDeviceManager.GraphicsDevice);
            }
            if (_mapBoard.MapInfo.LBTop != null)
            {
                _lbTop = (int)_mapBoard.MapInfo.LBTop;
                this._lbTextureTop = CreateLBBorder((int)(_vrFieldBoundary.Width * 1.45), LB_BORDER_WIDTHHEIGHT + _lbTop, _DxDeviceManager.GraphicsDevice);
            }
            if (_mapBoard.MapInfo.LBBottom != null)
            {
                _lbBottom = (int)_mapBoard.MapInfo.LBBottom;
                this._lbTextureBottom = CreateLBBorder((int)(_vrFieldBoundary.Width * 1.45), LB_BORDER_WIDTHHEIGHT + _lbBottom, _DxDeviceManager.GraphicsDevice);
            }


            // Set border data on RenderingManager

            _renderingManager.SetVRBorderData(_vrFieldBoundary, _drawVRBorderLeftRight, _vrBoundaryTextureLeft, _vrBoundaryTextureRight);

            _renderingManager.SetLBBorderData(_lbTextureLeft, _lbTextureRight);



            // Mirror bottom boundaries
            if (_mapBoard.MapInfo.mirror_Bottom)
            {
                if (_mapBoard.MapInfo.VRLeft != null && _mapBoard.MapInfo.VRRight != null)
                {
                    int vr_width = (int)_mapBoard.MapInfo.VRRight - (int)_mapBoard.MapInfo.VRLeft;
                    const int obj_mirrorBottom_height = 200;
                    _mirrorBottomRect = new Rectangle((int)_mapBoard.MapInfo.VRLeft, (int)_mapBoard.MapInfo.VRBottom - obj_mirrorBottom_height, vr_width, obj_mirrorBottom_height);
                    _mirrorBottomReflection = new ReflectionDrawableBoundary(128, 255, "mirror", true, false);
                }
            }


            // Cleanup spine event handlers
            foreach (WzObject obj in usedProps)
            {
                if (obj == null)
                    continue;
                WzSpineObject spineObj = (WzSpineObject)obj.MSTagSpine;
                if (spineObj != null)
                {
                    EnsureSpineRenderer();
                    spineObj.state.Start += Start;
                    spineObj.state.End += End;
                    spineObj.state.Complete += Complete;
                    spineObj.state.Event += Event;
                }
                obj.MSTag = null;
                obj.MSTagSpine = null;
            }

            LogStartupCheckpoint($"LoadContent completed for map {_mapBoard?.MapInfo?.id}");
            loadMapContentStopwatch.Stop();
            Debug.WriteLine($"[MapLoad] Total LoadMapContent for map {_mapBoard?.MapInfo?.id} took {loadMapContentStopwatch.ElapsedMilliseconds} ms");
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
        }
    }
}
