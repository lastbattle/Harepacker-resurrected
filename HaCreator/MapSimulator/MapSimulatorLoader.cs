using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.MapObjects.UIObject;
using HaCreator.MapSimulator.Objects;
using HaCreator.MapSimulator.Objects.FieldObject;
using HaCreator.MapSimulator.Objects.UIObject;
using HaCreator.Wz;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.Spine;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.Converters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HaSharedLibrary.Wz;
using MapleLib.Helpers;
using SharpDX.Direct2D1.Effects;
using System.Drawing.Printing;
using HaCreator.MapSimulator.MapObjects.UIObject.Controls;
using System.Windows.Forms;
using HaCreator.MapSimulator.MapObjects.FieldObject;
using HaSharedLibrary.Render;

namespace HaCreator.MapSimulator {
    public class MapSimulatorLoader {

        // Constants
        private const string GLOBAL_FONT = "Arial";
        private const float TOOLTIP_FONTSIZE = 9.25f; // thankie willified, ya'll be remembered forever here <3

        private const float MINIMAP_STREETNAME_TOOLTIP_FONTSIZE = 10f;

        /// <summary>
        /// Create map simulator board with seamless map transitions.
        /// </summary>
        /// <param name="mapBoard"></param>
        /// <param name="titleName"></param>
        /// <param name="loadMapCallback">Optional callback to load a map by ID. Returns (Board, titleName) tuple or null if map not found. Enables seamless portal teleportation.</param>
        /// <param name="onComplete">Optional callback invoked on the UI thread when the simulator exits.</param>
        /// <returns></returns>
        public static void CreateAndShowMapSimulator(Board mapBoard, string titleName, Func<int, Tuple<Board, string>> loadMapCallback = null, Action onComplete = null) {
            if (mapBoard.MiniMap == null)
                mapBoard.RegenerateMinimap();

            Thread thread = new Thread(() => {
                var mapSimulator = new MapSimulator(mapBoard, titleName);

                // Set the callback for seamless map transitions
                if (loadMapCallback != null)
                {
                    mapSimulator.SetLoadMapCallback(loadMapCallback);
                }

                mapSimulator.Run();

                // Signal completion on the UI thread
                onComplete?.Invoke();
            }) {
                Priority = ThreadPriority.Highest
            };
            thread.Start();
            // Don't Join() - let the game run independently to avoid deadlock with Dispatcher.Invoke
        }

        #region Common
        /// <summary>
        /// Load frames from WzSubProperty or WzCanvasProperty
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="source"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <param name="spineAni">Spine animation path</param>
        /// <returns></returns>
        private static List<IDXObject> LoadFrames(TexturePool texturePool, WzImageProperty source, int x, int y, GraphicsDevice device, ref List<WzObject> usedProps, string spineAni = null) {
            List<IDXObject> frames = new List<IDXObject>();

            source = WzInfoTools.GetRealProperty(source);

            if (source is WzSubProperty property1 && property1.WzProperties.Count == 1) {
                source = property1.WzProperties[0];
            }

            if (source is WzCanvasProperty property) //one-frame
            {
                bool bLoadedSpine = LoadSpineMapObjectItem(source, source, device, spineAni);
                if (!bLoadedSpine) {
                    string canvasBitmapPath = property.FullPath;
                    Texture2D textureFromCache = texturePool.GetTexture(canvasBitmapPath);
                    if (textureFromCache != null) {
                        source.MSTag = textureFromCache;
                    }
                    else {
                        var bitmap = property.GetLinkedWzCanvasBitmap();
                        if (bitmap != null)
                        {
                            source.MSTag = bitmap.ToTexture2D(device);
                            // add to cache
                            texturePool.AddTextureToPool(canvasBitmapPath, (Texture2D)source.MSTag);
                        }
                    }
                }
                usedProps.Add(source);

                if (source.MSTagSpine != null) {
                    WzSpineObject spineObject = (WzSpineObject)source.MSTagSpine;
                    System.Drawing.PointF origin = property.GetCanvasOriginPosition();

                    frames.Add(new DXSpineObject(spineObject, x, y, origin));
                }
                else if (source.MSTag != null) {
                    Texture2D texture = (Texture2D)source.MSTag;
                    System.Drawing.PointF origin = property.GetCanvasOriginPosition();

                    frames.Add(new DXObject(x - (int)origin.X, y - (int)origin.Y, texture));
                }
                else // fallback
                {
                    Texture2D texture = Properties.Resources.placeholder.ToTexture2D(device);
                    System.Drawing.PointF origin = property.GetCanvasOriginPosition();

                    frames.Add(new DXObject(x - (int)origin.X, y - (int)origin.Y, texture));
                }
            }
            else if (source is WzSubProperty) // animated
            {
                WzImageProperty _frameProp;
                int i = 0;

                while ((_frameProp = WzInfoTools.GetRealProperty(source[(i++).ToString()])) != null) {
                    if (_frameProp is WzSubProperty) // issue with 867119250
                    {
                        frames.AddRange(LoadFrames(texturePool, _frameProp, x, y, device, ref usedProps, null));
                    }
                    else {
                        WzCanvasProperty frameProp;

                        if (_frameProp is WzUOLProperty) // some could be UOL. Ex: 321100000 Mirror world: [Mirror World] Leafre
                        {
                            WzObject linkVal = ((WzUOLProperty)_frameProp).LinkValue;
                            if (linkVal is WzCanvasProperty linkCanvas) {
                                frameProp = linkCanvas;
                            }
                            else
                                continue;
                        }
                        else {
                            frameProp = (WzCanvasProperty)_frameProp;
                        }

                        int delay = (int)InfoTool.GetOptionalInt(frameProp["delay"], 100);

                        bool bLoadedSpine = LoadSpineMapObjectItem((WzImageProperty)frameProp.Parent, frameProp, device, spineAni);
                        if (!bLoadedSpine) {
                            if (frameProp.MSTag == null) {
                                string canvasBitmapPath = frameProp.FullPath;
                                Texture2D textureFromCache = texturePool.GetTexture(canvasBitmapPath);
                                if (textureFromCache != null) {
                                    frameProp.MSTag = textureFromCache;
                                }
                                else {
                                    frameProp.MSTag = frameProp.GetLinkedWzCanvasBitmap().ToTexture2D(device);

                                    // add to cache
                                    texturePool.AddTextureToPool(canvasBitmapPath, (Texture2D)frameProp.MSTag);
                                }
                            }
                        }
                        usedProps.Add(frameProp);

                        if (frameProp.MSTagSpine != null) {
                            WzSpineObject spineObject = (WzSpineObject)frameProp.MSTagSpine;
                            System.Drawing.PointF origin = frameProp.GetCanvasOriginPosition();

                            frames.Add(new DXSpineObject(spineObject, x, y, origin, delay));
                        }
                        else if (frameProp.MSTag != null) {
                            Texture2D texture = (Texture2D)frameProp.MSTag;
                            System.Drawing.PointF origin = frameProp.GetCanvasOriginPosition();

                            frames.Add(new DXObject(x - (int)origin.X, y - (int)origin.Y, texture, delay));
                        }
                        else {
                            Texture2D texture = Properties.Resources.placeholder.ToTexture2D(device);
                            System.Drawing.PointF origin = frameProp.GetCanvasOriginPosition();

                            frames.Add(new DXObject(x - (int)origin.X, y - (int)origin.Y, texture, delay));
                        }
                    }
                }
            }
            return frames;
        }
        #endregion

        /// <summary>
        /// Map item
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="source"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="mapCenterX"></param>
        /// <param name="mapCenterY"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <param name="flip"></param>
        /// <returns></returns>
        public static BaseDXDrawableItem CreateMapItemFromProperty(TexturePool texturePool, 
            WzImageProperty source, 
            int x, int y, 
            Point mapCenter, GraphicsDevice device, ref List<WzObject> usedProps, bool flip) {

            BaseDXDrawableItem mapItem = new BaseDXDrawableItem(LoadFrames(texturePool, source, x, y, device, ref usedProps), flip);
            return mapItem;
        }

        /// <summary>
        /// Background
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="source"></param>
        /// <param name="bgInstance"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <param name="flip"></param>
        /// <returns></returns>
        public static BackgroundItem CreateBackgroundFromProperty(TexturePool texturePool, WzImageProperty source, BackgroundInstance bgInstance, GraphicsDevice device, ref List<WzObject> usedProps, bool flip) {
            List<IDXObject> frames = LoadFrames(texturePool, source, bgInstance.BaseX, bgInstance.BaseY, device, ref usedProps, bgInstance.SpineAni);
            if (frames.Count == 0) {
                string error = string.Format("[MapSimulatorLoader] 0 frames loaded for bg texture from src: '{0}'", source.FullPath); // Back_003.wz\\BM3_3.img\\spine\\0

                ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                return null;
            }

            if (frames.Count == 1) {
                return new BackgroundItem(bgInstance.cx, bgInstance.cy, bgInstance.rx, bgInstance.ry, bgInstance.type, bgInstance.a, bgInstance.front, frames[0], flip, bgInstance.screenMode);
            }
            return new BackgroundItem(bgInstance.cx, bgInstance.cy, bgInstance.rx, bgInstance.ry, bgInstance.type, bgInstance.a, bgInstance.front, frames, flip, bgInstance.screenMode);
        }

        #region Spine
        /// <summary>
        /// Load spine object from WzImageProperty (bg, map item)
        /// </summary>
        /// <param name="source"></param>
        /// <param name="prop"></param>
        /// <param name="device"></param>
        /// <returns></returns>
        private static bool LoadSpineMapObjectItem(WzImageProperty source, WzImageProperty prop, GraphicsDevice device, string spineAniPath = null) {
            WzImageProperty spineAtlas = null;

            bool bIsObjectLayer = source.Parent.Name == "spine";
            if (bIsObjectLayer) // load spine if the source is already the directory we need
            {
                string spineAtlasPath = ((WzStringProperty)source["spine"])?.GetString();
                if (spineAtlasPath != null) {
                    spineAtlas = source[spineAtlasPath + ".atlas"];
                }
            }
            else if (spineAniPath != null) {
                WzImageProperty spineSource = (WzImageProperty)source.Parent?.Parent["spine"]?[source.Name];

                string spineAtlasPath = ((WzStringProperty)spineSource["spine"])?.GetString();
                if (spineAtlasPath != null) {
                    spineAtlas = spineSource[spineAtlasPath + ".atlas"];
                }
            }
            else // simply check if 'spine' WzStringProperty exist, fix for Adele town
            {
                string spineAtlasPath = ((WzStringProperty)source["spine"])?.GetString();
                if (spineAtlasPath != null) {
                    spineAtlas = source[spineAtlasPath + ".atlas"];
                    bIsObjectLayer = true;
                }
            }

            if (spineAtlas != null) {
                if (spineAtlas is WzStringProperty stringObj) {
                    if (!stringObj.IsSpineAtlasResources)
                        return false;

                    WzSpineObject spineObject = new WzSpineObject(new WzSpineAnimationItem(stringObj));

                    spineObject.spineAnimationItem.LoadResources(device); //  load spine resources (this must happen after window is loaded)
                    spineObject.skeleton = new Skeleton(spineObject.spineAnimationItem.SkeletonData);
                    //spineObject.skeleton.R =153;
                    //spineObject.skeleton.G = 255;
                    //spineObject.skeleton.B = 0;
                    //spineObject.skeleton.A = 1f;

                    // Skin
                    foreach (Skin skin in spineObject.spineAnimationItem.SkeletonData.Skins) {
                        spineObject.skeleton.SetSkin(skin); // just set the first skin
                        break;
                    }

                    // Define mixing between animations.
                    spineObject.stateData = new AnimationStateData(spineObject.skeleton.Data);
                    spineObject.state = new AnimationState(spineObject.stateData);
                    if (!bIsObjectLayer)
                        spineObject.state.TimeScale = 0.1f;

                    if (spineAniPath != null) {
                        spineObject.state.SetAnimation(0, spineAniPath, true);
                    }
                    else {
                        int i = 0;
                        foreach (Animation animation in spineObject.spineAnimationItem.SkeletonData.Animations) {
                            spineObject.state.SetAnimation(i++, animation.Name, true);
                        }
                    }
                    prop.MSTagSpine = spineObject;
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region Reactor
        /// <summary>
        /// Create reactor item
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="reactorInstance"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static ReactorItem CreateReactorFromProperty(TexturePool texturePool, ReactorInstance reactorInstance, GraphicsDevice device, ref List<WzObject> usedProps) {
            ReactorInfo reactorInfo = (ReactorInfo)reactorInstance.BaseInfo;

            List<IDXObject> frames = new List<IDXObject>();

            WzImage linkedReactorImage = reactorInfo.LinkedWzImage;
            if (linkedReactorImage != null) {
                WzImageProperty framesImage = (WzImageProperty)linkedReactorImage?["0"]?["0"];
                if (framesImage != null) {
                    frames = LoadFrames(texturePool, framesImage, reactorInstance.X, reactorInstance.Y, device, ref usedProps);
                }
            }
            if (frames.Count == 0) {
                //string error = string.Format("[MapSimulatorLoader] 0 frames loaded for reactor from src: '{0}'",  reactorInfo.ID);

                //ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                return null;
            }
            return new ReactorItem(reactorInstance, frames);
        }
        #endregion

        #region Portal       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="gameParent"></param>
        /// <param name="portalInstance"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static PortalItem CreatePortalFromProperty(TexturePool texturePool, WzSubProperty gameParent, PortalInstance portalInstance, GraphicsDevice device, ref List<WzObject> usedProps) {
            PortalInfo portalInfo = (PortalInfo)portalInstance.BaseInfo;

            if (portalInstance.pt == PortalType.StartPoint ||
                portalInstance.pt == PortalType.Invisible ||
                //portalInstance.pt == PortalType.PORTALTYPE_CHANGABLE_INVISIBLE ||
                portalInstance.pt == PortalType.ScriptInvisible ||
                portalInstance.pt == PortalType.Script ||
                portalInstance.pt == PortalType.Collision ||
                portalInstance.pt == PortalType.CollisionScript ||
                portalInstance.pt == PortalType.CollisionCustomImpact || // springs in Mechanical grave 350040240
                portalInstance.pt == PortalType.CollisionVerticalJump) // vertical spring actually
                return null;

            List<IDXObject> frames = new List<IDXObject>(); // All frames "stand", "speak" "blink" "hair", "angry", "wink" etc

            //string portalType = portalInstance.pt;
            //int portalId = Program.InfoManager.PortalIdByType[portalInstance.pt];

            WzSubProperty portalTypeProperty = (WzSubProperty)gameParent[portalInstance.pt.ToCode()];
            if (portalTypeProperty == null) {
                portalTypeProperty = (WzSubProperty)gameParent["pv"];
            }
            else {
                // Support for older versions of MapleStory where 'pv' is a subproperty for the image frame than a collection of subproperty of frames
                if (portalTypeProperty["0"] is WzCanvasProperty) {
                    frames.AddRange(LoadFrames(texturePool, portalTypeProperty, portalInstance.X, portalInstance.Y, device, ref usedProps));
                    portalTypeProperty = null;
                }
            }

            if (portalTypeProperty != null) {
                WzSubProperty portalImageProperty = (WzSubProperty)portalTypeProperty[portalInstance.image == null ? "default" : portalInstance.image];

                if (portalImageProperty != null) {
                    WzSubProperty framesPropertyParent;
                    if (portalImageProperty["portalContinue"] != null)
                        framesPropertyParent = (WzSubProperty)portalImageProperty["portalContinue"];
                    else
                        framesPropertyParent = (WzSubProperty)portalImageProperty;

                    if (framesPropertyParent != null) {
                        frames.AddRange(LoadFrames(texturePool, framesPropertyParent, portalInstance.X, portalInstance.Y, device, ref usedProps));
                    }
                }
            }
            if (frames.Count == 0)
                return null;
            return new PortalItem(portalInstance, frames);
        }
        #endregion

        #region Life
        /// <summary>
        /// Creates a MobItem with separate animations for each action (stand, move, fly, etc.)
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="mobInstance"></param>
        /// <param name="UserScreenScaleFactor"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static MobItem CreateMobFromProperty(TexturePool texturePool, MobInstance mobInstance, float UserScreenScaleFactor, GraphicsDevice device, ref List<WzObject> usedProps) {
            MobInfo mobInfo = (MobInfo)mobInstance.BaseInfo;
            WzImage source = mobInfo.LinkedWzImage;

            // Create animation set to store frames per action
            MobAnimationSet animationSet = new MobAnimationSet();

            foreach (WzImageProperty childProperty in source.WzProperties) {
                if (childProperty is WzSubProperty mobStateProperty) // issue with 867119250, Eluna map mobs
                {
                    string actionName = mobStateProperty.Name.ToLower();

                    switch (actionName) {
                        case "info": // info/speak/0 WzStringProperty - skip info node
                            break;

                        case "stand":
                        case "move":
                        case "walk":
                        case "fly":
                        case "jump":
                        case "hit1":
                        case "die1":
                        case "die2":
                        case "attack1":
                        case "attack2":
                        case "skill1":
                        case "skill2":
                        case "chase":
                        case "regen":
                            {
                                // Load frames for this specific action
                                List<IDXObject> actionFrames = LoadFrames(texturePool, mobStateProperty, mobInstance.X, mobInstance.Y, device, ref usedProps);
                                if (actionFrames.Count > 0)
                                {
                                    animationSet.AddAnimation(actionName, actionFrames);
                                }
                                break;
                            }

                        default:
                            {
                                // For unknown actions, still load them in case they're needed
                                List<IDXObject> actionFrames = LoadFrames(texturePool, mobStateProperty, mobInstance.X, mobInstance.Y, device, ref usedProps);
                                if (actionFrames.Count > 0)
                                {
                                    animationSet.AddAnimation(actionName, actionFrames);
                                }
                                break;
                            }
                    }
                }
            }

            System.Drawing.Color color_foreGround = System.Drawing.Color.White; // mob foreground color
            NameTooltipItem nameTooltip = MapSimulatorLoader.CreateNPCMobNameTooltip(
                mobInstance.MobInfo.Name, mobInstance.X, mobInstance.Y, color_foreGround,
                texturePool, UserScreenScaleFactor, device);

            return new MobItem(mobInstance, animationSet, nameTooltip);
        }

        /// <summary>
        /// NPC
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="npcInstance"></param>
        /// <param name="UserScreenScaleFactor"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static NpcItem CreateNpcFromProperty(TexturePool texturePool, NpcInstance npcInstance, float UserScreenScaleFactor, GraphicsDevice device, ref List<WzObject> usedProps) {
            NpcInfo npcInfo = (NpcInfo)npcInstance.BaseInfo;
            WzImage source = npcInfo.LinkedWzImage;

            // Create animation set to store frames by action (stand, speak, blink, etc.)
            NpcAnimationSet animationSet = new NpcAnimationSet();

            foreach (WzImageProperty childProperty in source.WzProperties) {
                WzSubProperty npcStateProperty = (WzSubProperty)childProperty;
                switch (npcStateProperty.Name) {
                    case "info": // info/speak/0 WzStringProperty
                        {
                            break;
                        }
                    default: {
                            // Load frames for this action and store by action name
                            List<IDXObject> actionFrames = LoadFrames(texturePool, npcStateProperty, npcInstance.X, npcInstance.Y, device, ref usedProps);
                            if (actionFrames.Count > 0)
                            {
                                animationSet.AddAnimation(npcStateProperty.Name, actionFrames);
                            }
                            break;
                        }
                }
            }
            if (animationSet.ActionCount == 0) // fix japan ms v186, (9000021.img「ガガ」) なぜだ？;(
                return null;

            System.Drawing.Color color_foreGround = System.Drawing.Color.FromArgb(255, 255, 255, 0); // gold npc foreground color

            NameTooltipItem nameTooltip = MapSimulatorLoader.CreateNPCMobNameTooltip(
                npcInstance.NpcInfo.StringName, npcInstance.X, npcInstance.Y, color_foreGround,
                texturePool, UserScreenScaleFactor, device);

            const int NPC_FUNC_Y_POS = 17;

            NameTooltipItem npcDescTooltip = MapSimulatorLoader.CreateNPCMobNameTooltip(
                npcInstance.NpcInfo.StringFunc, npcInstance.X, npcInstance.Y + NPC_FUNC_Y_POS, color_foreGround,
                texturePool, UserScreenScaleFactor, device);

            return new NpcItem(npcInstance, animationSet, nameTooltip, npcDescTooltip);
        }
        #endregion

        #region UI
        /// <summary>
        /// Draws the status bar UI (Character health, level, name)
        /// </summary>
        /// <param name="uiStatusBar">UI.wz/StatusBar.img</param
        /// <param name="uiStatusBar2">UI.wz/StatusBar2.img</param>
        /// <param name="mapBoard"></param>
        /// <param name="device"></param>
        /// <param name="UserScreenScaleFactor"></param>
        /// <param name="RenderWidth"></param>
        /// <param name="RenderHeight"></param>
        /// <param name="soundUIImage"></param>
        /// <param name="bBigBang"></param>
        /// <returns></returns>
        public static Tuple<StatusBarUI, StatusBarChatUI> CreateStatusBarFromProperty(WzImage uiStatusBar, WzImage uiStatusBar2, Board mapBoard, GraphicsDevice device, float UserScreenScaleFactor, RenderParameters renderParams, WzImage soundUIImage, bool bBigBang) {
            // Pre-big bang maplestory status bar
            if (bBigBang) {
                WzSubProperty mainBarProperties = (uiStatusBar2?["mainBar"] as WzSubProperty);
                if (mainBarProperties != null) {
                    HaUIGrid grid = new HaUIGrid(1, 1);

                    System.Drawing.Bitmap backgrnd = ((WzCanvasProperty)mainBarProperties?["backgrnd"])?.GetLinkedWzCanvasBitmap();

                    grid.AddRenderable(0, 0, new HaUIImage(new HaUIInfo() {
                        Bitmap = backgrnd,
                        VerticalAlignment = HaUIAlignment.Start,
                        HorizontalAlignment = HaUIAlignment.Start
                    }));

                    const int UI_PADDING_PX = 2;

                    // Draw level, name, job area
                    HaUIStackPanel stackPanel_charStats = new HaUIStackPanel(HaUIStackOrientation.Horizontal, new HaUIInfo() {
                        VerticalAlignment = HaUIAlignment.End
                    });

                    System.Drawing.Bitmap bitmap_lvBacktrnd = ((WzCanvasProperty)mainBarProperties?["lvBacktrnd"])?.GetLinkedWzCanvasBitmap();

                    stackPanel_charStats.AddRenderable(new HaUIImage(new HaUIInfo() { Bitmap = bitmap_lvBacktrnd }));

                    // Draw HP, MP, EXP area
                    System.Drawing.Bitmap bitmap_gaugeBackgrd = ((WzCanvasProperty)mainBarProperties?["gaugeBackgrd"])?.GetLinkedWzCanvasBitmap();
                    System.Drawing.Bitmap bitmap_gaugeCover = ((WzCanvasProperty)mainBarProperties?["gaugeCover"])?.GetLinkedWzCanvasBitmap();

                    HaUIGrid grid_hpMpExp = new HaUIGrid(1, 1);
                    grid_hpMpExp.AddRenderable(0, 0, new HaUIImage(new HaUIInfo() { Bitmap = bitmap_gaugeCover }));
                    grid_hpMpExp.AddRenderable(0, 0, new HaUIImage(new HaUIInfo() { Bitmap = bitmap_gaugeBackgrd }));

                    // add HP, MP, EXP area to the [level, name, job area stackpanel]
                    stackPanel_charStats.AddRenderable(grid_hpMpExp);

                    // Cash shop, MTS, menu, system, channel UI
                    WzBinaryProperty binaryProp_BtMouseClickSoundProperty = (WzBinaryProperty)soundUIImage["BtMouseClick"];
                    WzBinaryProperty binaryProp_BtMouseOverSoundProperty = (WzBinaryProperty)soundUIImage["BtMouseOver"];

                    WzSubProperty subProperty_BtCashShop = (WzSubProperty)mainBarProperties?["BtCashShop"]; // cash shop
                    UIObject obj_Ui_BtCashShop = new UIObject(subProperty_BtCashShop, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device) {
                        X = 9 + bitmap_lvBacktrnd.Width + bitmap_gaugeBackgrd.Width + UI_PADDING_PX,
                    };
                    obj_Ui_BtCashShop.Y += backgrnd.Height;

                    WzSubProperty subProperty_BtMTS = (WzSubProperty)mainBarProperties?["BtMTS"]; // MTS
                    if (subProperty_BtMTS == null)
                        subProperty_BtMTS = (WzSubProperty)mainBarProperties?["BtNPT"]; // MapleStory Japan uses a different name
                    UIObject obj_Ui_BtMTS = null;
                    if (subProperty_BtMTS != null)
                    {
                        obj_Ui_BtMTS = new UIObject(subProperty_BtMTS, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                            false,
                            new Point(0, 0), device)
                        {
                        };
                        obj_Ui_BtMTS.X += obj_Ui_BtCashShop.X - obj_Ui_BtCashShop.CanvasSnapshotWidth;
                        obj_Ui_BtMTS.Y += backgrnd.Height;
                    }
                    WzSubProperty subProperty_BtMenu = (WzSubProperty)mainBarProperties?["BtMenu"]; // Menu
                    UIObject obj_Ui_BtMenu = new UIObject(subProperty_BtMenu, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device) {
                    };
                    obj_Ui_BtMenu.X += obj_Ui_BtCashShop.X - obj_Ui_BtCashShop.CanvasSnapshotWidth;
                    obj_Ui_BtMenu.Y += backgrnd.Height;

                    WzSubProperty subProperty_BtSystem = (WzSubProperty)mainBarProperties?["BtSystem"]; // System
                    UIObject obj_Ui_BtSystem = new UIObject(subProperty_BtSystem, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device) {
                    };
                    obj_Ui_BtSystem.X += obj_Ui_BtCashShop.X - obj_Ui_BtCashShop.CanvasSnapshotWidth;
                    obj_Ui_BtSystem.Y += backgrnd.Height;

                    WzSubProperty subProperty_BtChannel = (WzSubProperty)mainBarProperties?["BtChannel"]; // System
                    UIObject obj_Ui_BtChannel = new UIObject(subProperty_BtChannel, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device) {
                    };
                    obj_Ui_BtChannel.X += obj_Ui_BtCashShop.X - obj_Ui_BtCashShop.CanvasSnapshotWidth;
                    obj_Ui_BtChannel.Y += backgrnd.Height;


                    // Draw Chat UI
                    System.Drawing.Bitmap bitmap_chatSpace = ((WzCanvasProperty)mainBarProperties?["chatSpace"])?.GetLinkedWzCanvasBitmap(); // chat foreground
                    System.Drawing.Bitmap bitmap_chatSpace2 = ((WzCanvasProperty)mainBarProperties?["chatSpace2"])?.GetLinkedWzCanvasBitmap(); // chat background

                    HaUIGrid grid_chat = new HaUIGrid(1, 1, new HaUIInfo() {
                        Margins = new HaUIMargin() {
                            //Bottom = 50, // Add this line to move it lower
                        }
                    });
                    grid_chat.AddRenderable(0, 0, new HaUIImage(new HaUIInfo() {
                        Bitmap = bitmap_chatSpace2,
                        VerticalAlignment = HaUIAlignment.Center,
                        HorizontalAlignment = HaUIAlignment.Start,
                        Margins = new HaUIMargin() {
                            Left = 4,
                        }
                    }));
                    grid_chat.AddRenderable(0, 0, new HaUIImage(new HaUIInfo() {
                        Bitmap = bitmap_chatSpace,
                        VerticalAlignment = HaUIAlignment.Center,
                        HorizontalAlignment = HaUIAlignment.Center,
                        Padding = new HaUIMargin() {
                        }
                    }));

                    // notice
                    System.Drawing.Bitmap bitmap_notice = ((WzCanvasProperty)mainBarProperties?["notice"])?.GetLinkedWzCanvasBitmap();
                    HaUIImage uiImage_notice = new HaUIImage(new HaUIInfo() {
                        Bitmap = bitmap_notice,
                        VerticalAlignment = HaUIAlignment.Start,
                        HorizontalAlignment = HaUIAlignment.End,
                        Margins = new HaUIMargin() {
                            //Left= grid_chat.GetSize().Width,
                        }
                    });
                    grid_chat.AddRenderable(0, 0, uiImage_notice);

                    Texture2D texture_chatUI = grid_chat.Render().ToTexture2D(device);
                    IDXObject dxObj_chatUI = new DXObject(UI_PADDING_PX, (int) (renderParams.RenderHeight / renderParams.RenderObjectScaling) - grid_chat.GetSize().Height - 36, texture_chatUI, 0);

                    // Scroll up+down, Chat, report/ claim, notice, stat, quest, inventory, equip, skill, key set
                    System.Drawing.Bitmap bitmap_lvNumber1 = ((WzCanvasProperty)mainBarProperties?["lvNumber/1"])?.GetLinkedWzCanvasBitmap();

                    // chat
                    WzSubProperty subProperty_chatOpen = (WzSubProperty)mainBarProperties?["chatOpen"];
                    WzSubProperty subProperty_chatClose = (WzSubProperty)mainBarProperties?["chatClose"];
                    UIObject obj_Ui_chatOpen = new UIObject(subProperty_chatOpen, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device) {
                    };
                    obj_Ui_chatOpen.X = dxObj_chatUI.Width - obj_Ui_chatOpen.CanvasSnapshotWidth - 5;
                    obj_Ui_chatOpen.Y -= obj_Ui_chatOpen.Y - 4;

                    // chat scroll up/ down
                    WzSubProperty subProperty_scrollUp = (WzSubProperty)mainBarProperties?["scrollUp"];
                    WzSubProperty subProperty_scrollDown = (WzSubProperty)mainBarProperties?["scrollDown"];
                    UIObject obj_Ui_scrollUp = new UIObject(subProperty_scrollUp, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device) {
                    };
                    obj_Ui_scrollUp.X = obj_Ui_chatOpen.X + obj_Ui_scrollUp.CanvasSnapshotWidth + 8;
                    obj_Ui_scrollUp.Y = obj_Ui_chatOpen.Y - 2;
                    UIObject obj_Ui_scrollDown = new UIObject(subProperty_scrollDown, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device) {
                    };
                    obj_Ui_scrollDown.X = obj_Ui_scrollUp.X;
                    obj_Ui_scrollDown.Y = obj_Ui_scrollUp.Y + obj_Ui_scrollDown.CanvasSnapshotHeight + UI_PADDING_PX;

                    // chat
                    WzSubProperty subProperty_BtChat = (WzSubProperty)mainBarProperties?["BtChat"];
                    UIObject obj_Ui_BtChat = new UIObject(subProperty_BtChat, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device) {
                    };
                    obj_Ui_BtChat.X = obj_Ui_chatOpen.X + obj_Ui_BtChat.CanvasSnapshotWidth + 4;
                    obj_Ui_BtChat.Y = obj_Ui_chatOpen.Y - 2;

                    // report
                    WzSubProperty subProperty_BtClaim = (WzSubProperty)mainBarProperties?["BtClaim"]; // report
                    UIObject obj_Ui_BtClaim = new UIObject(subProperty_BtClaim, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device) {
                    };
                    obj_Ui_BtClaim.X = obj_Ui_BtChat.X + obj_Ui_BtClaim.CanvasSnapshotWidth;
                    obj_Ui_BtClaim.Y = obj_Ui_BtChat.Y;

                    // notice
                    // this is rendered above


                    // character
                    WzSubProperty subProperty_BtCharacter = (WzSubProperty)mainBarProperties?["BtCharacter"];
                    UIObject obj_Ui_BtCharacter = new UIObject(subProperty_BtCharacter, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device) {
                    };
                    obj_Ui_BtCharacter.X = obj_Ui_BtClaim.X + obj_Ui_BtCharacter.CanvasSnapshotWidth + bitmap_notice.Width - 7;
                    obj_Ui_BtCharacter.Y = obj_Ui_BtClaim.Y;

                    // stat
                    WzSubProperty subProperty_BtStat = (WzSubProperty)mainBarProperties?["BtStat"];
                    UIObject obj_Ui_BtStat = new UIObject(subProperty_BtStat, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device) {
                    };
                    obj_Ui_BtStat.X = obj_Ui_BtCharacter.X + obj_Ui_BtStat.CanvasSnapshotWidth;
                    obj_Ui_BtStat.Y = obj_Ui_BtCharacter.Y;

                    // quest
                    WzSubProperty subProperty_BtQuest = (WzSubProperty)mainBarProperties?["BtQuest"];
                    UIObject obj_Ui_BtQuest = new UIObject(subProperty_BtQuest, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device) {
                    };
                    obj_Ui_BtQuest.X = obj_Ui_BtStat.X + obj_Ui_BtQuest.CanvasSnapshotWidth;
                    obj_Ui_BtQuest.Y = obj_Ui_BtStat.Y;

                    // inventory
                    WzSubProperty subProperty_BtInven = (WzSubProperty)mainBarProperties?["BtInven"];
                    UIObject obj_Ui_BtInven = new UIObject(subProperty_BtInven, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device) {
                    };
                    obj_Ui_BtInven.X = obj_Ui_BtQuest.X + obj_Ui_BtInven.CanvasSnapshotWidth;
                    obj_Ui_BtInven.Y = obj_Ui_BtQuest.Y;

                    // equipment
                    WzSubProperty subProperty_BtEquip = (WzSubProperty)mainBarProperties?["BtEquip"];
                    UIObject obj_Ui_BtEquip = new UIObject(subProperty_BtEquip, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device) {
                    };
                    obj_Ui_BtEquip.X = obj_Ui_BtInven.X + obj_Ui_BtEquip.CanvasSnapshotWidth;
                    obj_Ui_BtEquip.Y = obj_Ui_BtInven.Y;

                    // skill
                    WzSubProperty subProperty_BtSkill = (WzSubProperty)mainBarProperties?["BtSkill"];
                    UIObject obj_Ui_BtSkill = new UIObject(subProperty_BtSkill, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device) {
                    };
                    obj_Ui_BtSkill.X = obj_Ui_BtEquip.X + obj_Ui_BtSkill.CanvasSnapshotWidth;
                    obj_Ui_BtSkill.Y = obj_Ui_BtEquip.Y;

                    // key setting
                    WzSubProperty subProperty_BtKeysetting = (WzSubProperty)mainBarProperties?["BtKeysetting"];
                    UIObject obj_Ui_BtKeysetting = new UIObject(subProperty_BtKeysetting, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device) {
                    };
                    obj_Ui_BtKeysetting.X = obj_Ui_BtSkill.X + obj_Ui_BtSkill.CanvasSnapshotWidth + 4;/* + obj_Ui_BtKeysetting.CanvasSnapshotWidth*/;
                    obj_Ui_BtKeysetting.Y = obj_Ui_BtSkill.Y;

                    // Add all items to the main grid
                    grid.AddRenderable(0, 0, stackPanel_charStats);

                    Texture2D texture_backgrnd = grid.Render().ToTexture2D(device);

                    IDXObject dxObj_backgrnd = new DXObject(0, (int) (renderParams.RenderHeight / renderParams.RenderObjectScaling) - grid.GetSize().Height, texture_backgrnd, 0);
                    StatusBarUI statusBar = new StatusBarUI(dxObj_backgrnd, obj_Ui_BtCashShop, obj_Ui_BtMTS, obj_Ui_BtMenu, obj_Ui_BtSystem, obj_Ui_BtChannel,
                        new Point(dxObj_backgrnd.X, dxObj_backgrnd.Y),
                        new List<UIObject> {  });
                    statusBar.InitializeButtons();

                    StatusBarChatUI chatUI = new StatusBarChatUI(dxObj_chatUI, new Point(dxObj_chatUI.X, dxObj_chatUI.Y),
                         new List<UIObject> { 
                             obj_Ui_chatOpen, 
                             obj_Ui_scrollUp, obj_Ui_scrollDown,
                             obj_Ui_BtChat, obj_Ui_BtClaim,
                             obj_Ui_BtCharacter, obj_Ui_BtStat, obj_Ui_BtQuest, obj_Ui_BtInven, obj_Ui_BtEquip, obj_Ui_BtSkill, obj_Ui_BtKeysetting
                         }
                        );
                    chatUI.InitializeButtons();

                    return new Tuple<StatusBarUI, StatusBarChatUI>(statusBar, chatUI);
                }
            }
            return null;
        }

        /// <summary>
        /// Draws the frame and the UI of the minimap.
        /// TODO: This whole thing needs to be dramatically simplified via further abstraction to keep it noob-proof :(
        /// </summary>
        /// <param name="uiWindow1Image">UI.wz/UIWindow1.img pre-bb</param>
        /// <param name="uiWindow2Image">UI.wz/UIWindow2.img post-bb</param>
        /// <param name="uiBasicImage">UI.wz/Basic.img</param>
        /// <param name="mapBoard"></param>
        /// <param name="device"></param>
        /// <param name="UserScreenScaleFactor">The scale factor of the window (DPI)</param>
        /// <param name="MapName">The map name. i.e The Hill North</param>
        /// <param name="StreetName">The street name. i.e Hidden street</param>
        /// <param name="soundUIImage">Sound.wz/UI.img</param>
        /// <param name="bBigBang">Big bang update</param>
        /// <returns></returns>
        public static MinimapUI CreateMinimapFromProperty(WzImage uiWindow1Image, WzImage uiWindow2Image, WzImage uiBasicImage, Board mapBoard, GraphicsDevice device, float UserScreenScaleFactor, string MapName, string StreetName, WzImage soundUIImage, bool bBigBang) {
            if (mapBoard.MiniMap == null)
                return null;

            WzSubProperty minimapFrameProperty = (WzSubProperty)uiWindow2Image?["MiniMap"];
            if (minimapFrameProperty == null) // UIWindow2 not available pre-BB.
            {
                minimapFrameProperty = (WzSubProperty)uiWindow1Image["MiniMap"];
            }

            WzSubProperty maxMapProperty = (WzSubProperty)minimapFrameProperty["MaxMap"];
            WzSubProperty minMapProperty = (WzSubProperty)minimapFrameProperty["MinMap"];
            WzSubProperty maxMapMirrorProperty = (WzSubProperty)minimapFrameProperty["MaxMapMirror"]; // for Zero maps
            WzSubProperty minMapMirrorProperty = (WzSubProperty)minimapFrameProperty["MinMapMirror"]; // for Zero maps


            WzSubProperty useFrameMaxMap;
            WzSubProperty useFrameMinMap;
            if (mapBoard.MapInfo.zeroSideOnly || MapConstants.IsZerosTemple(mapBoard.MapInfo.id)) // zero's temple
            {
                useFrameMaxMap = maxMapMirrorProperty;
                useFrameMinMap = minMapMirrorProperty;
            }
            else
            {
                useFrameMaxMap = maxMapProperty;
                useFrameMinMap = minMapProperty;
            }

            // Wz frames
            System.Drawing.Bitmap c = ((WzCanvasProperty)useFrameMaxMap?["c"])?.GetLinkedWzCanvasBitmap(); // the bg color
            System.Drawing.Bitmap e = ((WzCanvasProperty)useFrameMaxMap?["e"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap n = ((WzCanvasProperty)useFrameMaxMap?["n"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap s = ((WzCanvasProperty)useFrameMaxMap?["s"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap w = ((WzCanvasProperty)useFrameMaxMap?["w"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap ne = ((WzCanvasProperty)useFrameMaxMap?["ne"])?.GetLinkedWzCanvasBitmap(); // top right
            System.Drawing.Bitmap nw = ((WzCanvasProperty)useFrameMaxMap?["nw"])?.GetLinkedWzCanvasBitmap(); // top left
            System.Drawing.Bitmap se = ((WzCanvasProperty)useFrameMaxMap?["se"])?.GetLinkedWzCanvasBitmap(); // bottom right
            System.Drawing.Bitmap sw = ((WzCanvasProperty)useFrameMaxMap?["sw"])?.GetLinkedWzCanvasBitmap(); // bottom left

            // Constants
            const int MAPMARK_MAPNAME_LEFT_MARGIN = 4;
            const int MAPMARK_MAPNAME_TOP_MARGIN = 17;
            const int MAP_IMAGE_TEXT_PADDING = 2; // the number of pixels from the left to draw the minimap image
            System.Drawing.Color color_bgFill = System.Drawing.Color.Transparent;
            System.Drawing.Color color_foreGround = System.Drawing.Color.White;


            // Map background image
            // Using HaUIGrid and HaUIStackPanel
            System.Drawing.Bitmap miniMapImage = mapBoard.MiniMap; // the original minimap image without UI frame overlay


            // Create Map mark
            System.Drawing.Bitmap mapMark = null;
            if (Program.InfoManager.MapMarks.ContainsKey(mapBoard.MapInfo.mapMark)) {
                mapMark = Program.InfoManager.MapMarks[mapBoard.MapInfo.mapMark];
            }

            // Create map minimap image
            HaUIImage minimapUiImage = new HaUIImage(new HaUIInfo() {
                Bitmap = miniMapImage,
                HorizontalAlignment = HaUIAlignment.Center,
                Margins = new HaUIMargin() { Left = MAP_IMAGE_TEXT_PADDING + 10, Right = MAP_IMAGE_TEXT_PADDING + 10, Top = 10, Bottom = 0 },
                //Padding = new HaUIPadding() { Bottom = 10, Left = 10, Right = 10 }
            });

            // Create BitmapStackPanel for text and minimap
            HaUIStackPanel fullMiniMapStackPanel = new HaUIStackPanel(HaUIStackOrientation.Vertical, new HaUIInfo() {
                MinWidth = 150 // set a min width, so the MapName and StreetName is not cut off if the map image is too thin
            });
            HaUIStackPanel mapNameMarkStackPanel = new HaUIStackPanel(HaUIStackOrientation.Horizontal, new HaUIInfo() {
                Margins = new HaUIMargin() { Top = MAPMARK_MAPNAME_TOP_MARGIN, Left = MAPMARK_MAPNAME_LEFT_MARGIN, Bottom = 0, Right = 0 },
            });

            if (mapMark != null) {
                // minimap map-mark image
                HaUIImage mapNameMarkImage = new HaUIImage(new HaUIInfo() {
                    Bitmap = mapMark,
                });
                mapNameMarkStackPanel.AddRenderable(mapNameMarkImage);
            }
            // Minimap name, and street name
            string renderText = string.Format("{0}{1}{2}", StreetName, Environment.NewLine, MapName);
            HaUIText haUITextMapNameStreetName = new HaUIText(renderText, color_foreGround, GLOBAL_FONT, MINIMAP_STREETNAME_TOOLTIP_FONTSIZE, UserScreenScaleFactor);
            haUITextMapNameStreetName.GetInfo().Margins.Top = 3;
            haUITextMapNameStreetName.GetInfo().Margins.Left = MAP_IMAGE_TEXT_PADDING;
            haUITextMapNameStreetName.GetInfo().Margins.Right = MAP_IMAGE_TEXT_PADDING;

            mapNameMarkStackPanel.AddRenderable(haUITextMapNameStreetName);
            fullMiniMapStackPanel.AddRenderable(mapNameMarkStackPanel);

            System.Drawing.Bitmap finalMininisedMinimapBitmap = HaUIHelper.RenderAndMergeMinimapUIFrame(fullMiniMapStackPanel, color_bgFill, ne, nw, se, sw, e, w, n, s, 
                c, mapMark != null ? mapMark.Height : 0);

            HaUIGrid minimapUiGrid = new HaUIGrid(1, 1);
            minimapUiGrid.GetInfo().Margins.Top = 10;
            minimapUiGrid.GetInfo().HorizontalAlignment = HaUIAlignment.Center;
            minimapUiGrid.GetInfo().VerticalAlignment = HaUIAlignment.Center;
            minimapUiGrid.AddRenderable(minimapUiImage);
            fullMiniMapStackPanel.AddRenderable(minimapUiGrid);

            // Render final minimap Bitmap with UI frames
            System.Drawing.Bitmap finalFullMinimapBitmap = HaUIHelper.RenderAndMergeMinimapUIFrame(fullMiniMapStackPanel, color_bgFill, ne, nw, se, sw, e, w, n, s,
                c, mapMark != null ? mapMark.Height : 0);

            Texture2D texturer_miniMapMinimised = finalMininisedMinimapBitmap.ToTexture2D(device);
            Texture2D texturer_miniMap = finalFullMinimapBitmap.ToTexture2D(device);

            // Dots pixel 
            System.Drawing.Bitmap bmp_DotPixel = new System.Drawing.Bitmap(2, 4);
            using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bmp_DotPixel)) {
                graphics.FillRectangle(new System.Drawing.SolidBrush(System.Drawing.Color.Yellow), new System.Drawing.RectangleF(0, 0, bmp_DotPixel.Width, bmp_DotPixel.Height));
                graphics.Flush();
            }
            IDXObject dxObj_miniMapPixel = new DXObject(0, n.Height, bmp_DotPixel.ToTexture2D(device), 0);

            // Map
            IDXObject dxObj_miniMap_Minimised = new DXObject(0, 0, texturer_miniMapMinimised, 0);
            IDXObject dxObj_miniMap = new DXObject(0, 0, texturer_miniMap, 0); // starting position of the minimap in the map

            // need to calculate how much x position, where the map is shifted to the center by HorizontalAlignment
            // to compensate for in the character dot position indicator
            HaUISize fullMiniMapStackPanelSize = fullMiniMapStackPanel.GetSize();
            int alignmentXOffset = HaUIHelper.CalculateAlignmentOffset(fullMiniMapStackPanelSize.Width, minimapUiImage.GetInfo().Bitmap.Width, minimapUiGrid.GetInfo().HorizontalAlignment);

            MinimapUI minimapItem = new MinimapUI(dxObj_miniMap,
                new BaseDXDrawableItem(dxObj_miniMapPixel, false) {
                    Position = new Point(MAP_IMAGE_TEXT_PADDING + alignmentXOffset, 0) // map is on the center
                },
                new BaseDXDrawableItem(dxObj_miniMap_Minimised, false) {
                    Position = new Point(MAP_IMAGE_TEXT_PADDING, 0)
                },
                texturer_miniMap.Width, texturer_miniMap.Height);

            minimapItem.Position = new Point(10, 10); // default position

            ////////////// Minimap buttons////////////////////
            // This must be in order. 
            // >>> If aligning from the left to the right. Items at the left must be at the top of the code
            // >>> If aligning from the right to the left. Items at the right must be at the top of the code with its (x position - parent width).
            // TODO: probably a wrapper class in the future, such as HorizontalAlignment and VerticalAlignment, or Grid/ StackPanel 
            WzBinaryProperty BtMouseClickSoundProperty = (WzBinaryProperty)soundUIImage["BtMouseClick"];
            WzBinaryProperty BtMouseOverSoundProperty = (WzBinaryProperty)soundUIImage["BtMouseOver"];

            if (bBigBang) {
                WzSubProperty BtNpc = (WzSubProperty)minimapFrameProperty["BtNpc"]; // npc button
                WzSubProperty BtMin = (WzSubProperty)minimapFrameProperty["BtMin"]; // mininise button
                WzSubProperty BtMax = (WzSubProperty)minimapFrameProperty["BtMax"]; // maximise button
                WzSubProperty BtBig = (WzSubProperty)minimapFrameProperty["BtBig"]; // big button
                WzSubProperty BtMap = (WzSubProperty)minimapFrameProperty["BtMap"]; // world button

                UIObject objUIBtMap = new UIObject(BtMap, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                    false,
                    new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                objUIBtMap.X = texturer_miniMap.Width - objUIBtMap.CanvasSnapshotWidth - 8; // render at the (width of minimap - obj width)

                UIObject objUIBtBig = new UIObject(BtBig, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                    false,
                    new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                objUIBtBig.X = objUIBtMap.X - objUIBtBig.CanvasSnapshotWidth; // render at the (width of minimap - obj width)

                UIObject objUIBtMax = new UIObject(BtMax, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                    false,
                    new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                objUIBtMax.X = objUIBtBig.X - objUIBtMax.CanvasSnapshotWidth; // render at the (width of minimap - obj width)

                UIObject objUIBtMin = new UIObject(BtMin, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                    false,
                    new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                objUIBtMin.X = objUIBtMax.X - objUIBtMin.CanvasSnapshotWidth; // render at the (width of minimap - obj width)

                // BaseClickableUIObject objUINpc = new BaseClickableUIObject(BtNpc, false, new Point(objUIBtMap.CanvasSnapshotWidth + objUIBtBig.CanvasSnapshotWidth + objUIBtMax.CanvasSnapshotWidth + objUIBtMin.CanvasSnapshotWidth, MAP_IMAGE_PADDING), device);

                minimapItem.InitializeMinimapButtons(objUIBtMin, objUIBtMax, objUIBtBig, objUIBtMap);
            }
            else {
                WzSubProperty BtMin = (WzSubProperty)uiBasicImage["BtMin"]; // mininise button
                WzSubProperty BtMax = (WzSubProperty)uiBasicImage["BtMax"]; // maximise button
                WzSubProperty BtMap = (WzSubProperty)minimapFrameProperty["BtMap"]; // world button

                UIObject objUIBtMap = new UIObject(BtMap, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                    false,
                    new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                objUIBtMap.X = texturer_miniMap.Width - objUIBtMap.CanvasSnapshotWidth - 8; // render at the (width of minimap - obj width)

                UIObject objUIBtMax = new UIObject(BtMax, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                    false,
                    new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                objUIBtMax.X = objUIBtMap.X - objUIBtMax.CanvasSnapshotWidth; // render at the (width of minimap - obj width)

                UIObject objUIBtMin = new UIObject(BtMin, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                    false,
                    new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                objUIBtMin.X = objUIBtMax.X - objUIBtMin.CanvasSnapshotWidth; // render at the (width of minimap - obj width)

                // BaseClickableUIObject objUINpc = new BaseClickableUIObject(BtNpc, false, new Point(objUIBtMap.CanvasSnapshotWidth + objUIBtBig.CanvasSnapshotWidth + objUIBtMax.CanvasSnapshotWidth + objUIBtMin.CanvasSnapshotWidth, MAP_IMAGE_PADDING), device);

                minimapItem.InitializeMinimapButtons(objUIBtMin, objUIBtMax, null, objUIBtMap);
            }
            return minimapItem;
        }

        /// <summary>
        /// Map item
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="source"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="mapCenterX"></param>
        /// <param name="mapCenterY"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <param name="flip"></param>
        /// <returns></returns>
        public static MouseCursorItem CreateMouseCursorFromProperty(TexturePool texturePool, WzImageProperty source, int x, int y, GraphicsDevice device, ref List<WzObject> usedProps, bool flip)
        {
            WzSubProperty cursorCanvas = (WzSubProperty)source?["0"]; // normal
            WzSubProperty cursorClickable = (WzSubProperty)source?["1"]; // click-able item
            WzSubProperty cursorClickableOmok = (WzSubProperty)source?["2"]; // click-able item
            WzSubProperty cursorClickableHouse = (WzSubProperty)source?["3"]; // click-able item
            WzSubProperty cursorClickable2 = (WzSubProperty)source?["4"]; // click-able item
            WzSubProperty cursorPickable = (WzSubProperty)source?["5"]; // pickable inventory
            WzSubProperty cursorGift = (WzSubProperty)source?["6"]; // 
            WzSubProperty cursorVerticalScrollable = (WzSubProperty)source?["7"]; // 
            WzSubProperty cursorHorizontalScrollable = (WzSubProperty)source?["8"]; // 
            WzSubProperty cursorVerticalScrollable2 = (WzSubProperty)source?["9"]; // 
            WzSubProperty cursorHorizontalScrollable2 = (WzSubProperty)source?["10"]; // 
            WzSubProperty cursorPickable2 = (WzSubProperty)source?["11"]; // pickable inventory
            WzSubProperty cursorHold = (WzSubProperty)source?["12"]; // pickable inventory

            List<IDXObject> frames = LoadFrames(texturePool, cursorCanvas, x, y, device, ref usedProps);

            // Mouse hold state
            BaseDXDrawableItem holdState = CreateMapItemFromProperty(texturePool, cursorHold, 0, 0, new Point(0, 0), device, ref usedProps, false);

            // Mouse clicked item state
            BaseDXDrawableItem clickableButtonState = CreateMapItemFromProperty(texturePool, cursorClickable, 0, 0, new Point(0, 0), device, ref usedProps, false);

            return new MouseCursorItem(frames, holdState, clickableButtonState);
        }
        #endregion

        #region Tooltip
        /// <summary>
        /// Tooltip
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="UserScreenScaleFactor">The scale factor of the window (DPI)</param>
        /// <param name="farmFrameParent"></param>
        /// <param name="tooltip"></param>
        /// <param name="device"></param>
        /// <returns></returns>
        public static TooltipItem CreateTooltipFromProperty(TexturePool texturePool, float UserScreenScaleFactor, WzSubProperty farmFrameParent, ToolTipInstance tooltip, GraphicsDevice device) {
            // Wz frames
            System.Drawing.Bitmap c = ((WzCanvasProperty)farmFrameParent?["c"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap cover = ((WzCanvasProperty)farmFrameParent?["cover"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap e = ((WzCanvasProperty)farmFrameParent?["e"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap n = ((WzCanvasProperty)farmFrameParent?["n"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap s = ((WzCanvasProperty)farmFrameParent?["s"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap w = ((WzCanvasProperty)farmFrameParent?["w"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap ne = ((WzCanvasProperty)farmFrameParent?["ne"])?.GetLinkedWzCanvasBitmap(); // top right
            System.Drawing.Bitmap nw = ((WzCanvasProperty)farmFrameParent?["nw"])?.GetLinkedWzCanvasBitmap(); // top left
            System.Drawing.Bitmap se = ((WzCanvasProperty)farmFrameParent?["se"])?.GetLinkedWzCanvasBitmap(); // bottom right
            System.Drawing.Bitmap sw = ((WzCanvasProperty)farmFrameParent?["sw"])?.GetLinkedWzCanvasBitmap(); // bottom left


            // tooltip property
            string title = tooltip.Title;
            string desc = tooltip.Desc;
            if (desc != null)
            {
                desc = desc.Replace("\\n\\n", "\n \n"); // Add a space between consecutive newlines, due to how the DrawString method handles newline characters in the Graphics class
            }

            string renderText = string.Format("{0}{1}{2}", title, Environment.NewLine, desc);

            //System.Drawing.Color color_bgFill = System.Drawing.Color.FromArgb(230, 17, 54, 82); // pre V patch (dark blue theme used post-bb), leave this here in case someone needs it
            System.Drawing.Color color_bgFill = System.Drawing.Color.FromArgb(255, 17, 17, 17); // post V patch (dark black theme used), use color picker on paint via image extracted from WZ if you need to get it
            System.Drawing.Color color_foreGround = System.Drawing.Color.White;
            const int WIDTH_PADDING = 10;
            const int HEIGHT_PADDING = 6;

            // Create
            using (System.Drawing.Font font = new System.Drawing.Font(GLOBAL_FONT, TOOLTIP_FONTSIZE / UserScreenScaleFactor)) {
                System.Drawing.Graphics graphics_dummy = System.Drawing.Graphics.FromImage(new System.Drawing.Bitmap(1, 1)); // dummy image just to get the Graphics object for measuring string
                System.Drawing.SizeF tooltipSize = graphics_dummy.MeasureString(renderText, font);

                int effective_width = (int)tooltipSize.Width + WIDTH_PADDING;
                int effective_height = (int)tooltipSize.Height + HEIGHT_PADDING;

                System.Drawing.Bitmap bmp_tooltip = new System.Drawing.Bitmap(effective_width, effective_height);
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bmp_tooltip)) {
                    // Frames and background
                    UIFrameHelper.DrawUIFrame(graphics, color_bgFill, ne, nw, se, sw, e, w, n, s, c, 0, effective_width, effective_height);

                    // Text
                    graphics.DrawString(renderText, font, new System.Drawing.SolidBrush(color_foreGround), WIDTH_PADDING / 2, HEIGHT_PADDING / 2);
                    graphics.Flush();
                }
                IDXObject dxObj = new DXObject(tooltip.X, tooltip.Y, bmp_tooltip.ToTexture2D(device), 0);
                TooltipItem item = new TooltipItem(tooltip, dxObj);

                return item;
            }
        }

        /// <summary>
        /// Draws the name tooltip for NPC and mobs
        /// </summary>
        /// <param name="renderText"></param>
        /// <param name="x">The life object's X position.</param>
        /// <param name="y">The life object's Y position.</param>
        /// <param name="color_foreGround"></param>
        /// <param name="texturePool"></param>
        /// <param name="UserScreenScaleFactor"></param>
        /// <param name="device"></param>
        /// <returns></returns>
        public static NameTooltipItem CreateNPCMobNameTooltip(string renderText, int x, int y, System.Drawing.Color color_foreGround,
            TexturePool texturePool, float UserScreenScaleFactor, GraphicsDevice device)
        {
            //System.Drawing.Color color_bgFill = System.Drawing.Color.FromArgb(230, 17, 54, 82); // pre V patch (dark blue theme used post-bb), leave this here in case someone needs it
            System.Drawing.Color color_bgFill = System.Drawing.Color.FromArgb(200, 17, 17, 17); // post V patch (dark black theme used), use color picker on paint via image extracted from WZ if you need to get it

            const int WIDTH_PADDING = 6; // use even numbers or it gets odd
            const int HEIGHT_PADDING = 2;

            // Create
            using (System.Drawing.Font font = new System.Drawing.Font(GLOBAL_FONT, TOOLTIP_FONTSIZE / UserScreenScaleFactor))
            {
                System.Drawing.Graphics graphics_dummy = System.Drawing.Graphics.FromImage(new System.Drawing.Bitmap(1, 1)); // dummy image just to get the Graphics object for measuring string
                System.Drawing.SizeF tooltipSize = graphics_dummy.MeasureString(renderText, font);

                int effective_width = (int)tooltipSize.Width + WIDTH_PADDING;
                int effective_height = (int)tooltipSize.Height + HEIGHT_PADDING;

                System.Drawing.Bitmap bmp_tooltip = new System.Drawing.Bitmap(effective_width, effective_height);
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bmp_tooltip))
                {
                    // Frames and background
                    UIFrameHelper.DrawUIFrame(graphics, color_bgFill, effective_width, effective_height);

                    // Text
                    graphics.DrawString(renderText, font, new System.Drawing.SolidBrush(color_foreGround), (WIDTH_PADDING / 2), HEIGHT_PADDING / 2);
                    graphics.Flush();
                }

                int tooltipShiftX = (x - (effective_width / 2));

                IDXObject dxObj = new DXObject(tooltipShiftX, y, bmp_tooltip.ToTexture2D(device), 0);
                NameTooltipItem item = new NameTooltipItem(dxObj);

                return item;
            }
        }
        #endregion

        private static string DumpFhList(List<FootholdLine> fhs) {
            string res = "";
            foreach (FootholdLine fh in fhs)
                res += fh.FirstDot.X + "," + fh.FirstDot.Y + " : " + fh.SecondDot.X + "," + fh.SecondDot.Y + "\r\n";
            return res;
        }
    }
}
