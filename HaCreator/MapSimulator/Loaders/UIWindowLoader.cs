using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Companions;
using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.UI.Controls;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.Img;
using MapleLib;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using CashServiceWindowStageKind = HaCreator.MapSimulator.UI.CashServiceStageKind;



namespace HaCreator.MapSimulator.Loaders
{
    public static partial class UIWindowLoader
    {
        #region Helper Methods
        /// <summary>
        /// Load a standard button from WZ property
        /// </summary>
        private static UIObject LoadButton(WzSubProperty parent, string buttonName,
            WzBinaryProperty clickSound, WzBinaryProperty overSound, GraphicsDevice device)
        {
            WzSubProperty buttonProperty = (WzSubProperty)parent?[buttonName];
            if (buttonProperty == null)
                return null;


            try
            {
                return new UIObject(buttonProperty, clickSound, overSound, false, Point.Zero, device);
            }
            catch
            {
                return null;
            }
        }

        private static UIObject[] LoadButtonCopies(
            WzSubProperty parent,
            string buttonName,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device,
            int count)
        {
            if (count <= 0)
            {
                return Array.Empty<UIObject>();
            }

            WzSubProperty buttonProperty = parent?[buttonName] as WzSubProperty;
            if (buttonProperty == null)
            {
                return Array.Empty<UIObject>();
            }

            UIObject[] buttons = new UIObject[count];
            for (int index = 0; index < count; index++)
            {
                try
                {
                    buttons[index] = new UIObject(buttonProperty, clickSound, overSound, false, Point.Zero, device);
                }
                catch
                {
                    buttons[index] = null;
                }
            }

            return buttons;
        }

        private static UIObject LoadButton(WzImage parent, string buttonName,
            WzBinaryProperty clickSound, WzBinaryProperty overSound, GraphicsDevice device)
        {
            WzSubProperty buttonProperty = parent?[buttonName] as WzSubProperty;
            if (buttonProperty == null)
            {
                return null;
            }

            try
            {
                return new UIObject(buttonProperty, clickSound, overSound, false, Point.Zero, device);
            }
            catch
            {
                return null;
            }
        }

        private static WzSubProperty ResolveUiSubPropertyFromStringPoolPath(int stringPoolId, params string[] fallbackPathSegments)
        {
            string path = MapleStoryStringPool.GetOrNull(stringPoolId);
            WzSubProperty resolvedProperty = ResolveUiSubPropertyFromStringPath(path);
            return resolvedProperty ?? ResolveUiSubPropertyFromSegments(fallbackPathSegments);
        }

        private static WzSubProperty ResolveUiSubPropertyFromStringPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string[] segments = path
                .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(segment => !string.Equals(segment, "UI", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return ResolveUiSubPropertyFromSegments(segments);
        }

        private static WzSubProperty ResolveUiSubPropertyFromSegments(params string[] segments)
        {
            if (segments == null || segments.Length == 0)
            {
                return null;
            }

            WzImage image = global::HaCreator.Program.FindImage("ui", segments[0]);
            if (image == null)
            {
                return null;
            }

            object current = image;
            for (int i = 1; i < segments.Length; i++)
            {
                string segment = segments[i];
                if (current is WzImage wzImage)
                {
                    current = wzImage[segment];
                }
                else if (current is WzSubProperty wzSubProperty)
                {
                    current = wzSubProperty[segment];
                }
                else
                {
                    return null;
                }
            }

            return current as WzSubProperty;
        }

        private static UIObject LoadIndexedButton(
            WzSubProperty buttonProperty,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device,
            Point fallbackPosition)
        {
            if (buttonProperty == null || device == null)
            {
                return null;
            }

            static BaseDXDrawableItem CreateState(WzCanvasProperty canvas, GraphicsDevice graphicsDevice, Point fallback)
            {
                if (canvas == null)
                {
                    return null;
                }

                Texture2D texture = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(graphicsDevice);
                if (texture == null)
                {
                    return null;
                }

                Point offset = ResolveCanvasOffset(canvas, fallback);
                return new BaseDXDrawableItem(new DXObject(offset.X, offset.Y, texture), false);
            }

            try
            {
                BaseDXDrawableItem normalState = CreateState(buttonProperty["1"] as WzCanvasProperty ?? buttonProperty["0"] as WzCanvasProperty, device, fallbackPosition);
                BaseDXDrawableItem disabledState = CreateState(buttonProperty["0"] as WzCanvasProperty ?? buttonProperty["4"] as WzCanvasProperty, device, fallbackPosition) ?? normalState;
                BaseDXDrawableItem pressedState = CreateState(buttonProperty["2"] as WzCanvasProperty ?? buttonProperty["3"] as WzCanvasProperty, device, fallbackPosition) ?? normalState;
                BaseDXDrawableItem mouseOverState = CreateState(buttonProperty["3"] as WzCanvasProperty ?? buttonProperty["1"] as WzCanvasProperty, device, fallbackPosition) ?? normalState;
                if (normalState == null)
                {
                    return null;
                }

                UIObject button = new UIObject(normalState, disabledState, pressedState, mouseOverState);
                return button;
            }
            catch
            {
                return null;
            }
        }

        private static UIObject LoadMapleTvReceiverButton(
            WzSubProperty mapleTvProperty,
            GraphicsDevice device,
            Point fallbackPosition)
        {
            if (mapleTvProperty?["BtTo"] is not WzSubProperty receiverProperty || device == null)
            {
                return null;
            }

            static BaseDXDrawableItem CreateState(WzCanvasProperty canvas, GraphicsDevice graphicsDevice, Point fallback)
            {
                if (canvas == null)
                {
                    return null;
                }

                Texture2D texture = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(graphicsDevice);
                if (texture == null)
                {
                    return null;
                }

                Point offset = ResolveCanvasOffset(canvas, fallback);
                return new BaseDXDrawableItem(new DXObject(offset.X, offset.Y, texture), false);
            }

            try
            {
                BaseDXDrawableItem disabledState = CreateState(receiverProperty["disabled"]?["0"] as WzCanvasProperty, device, fallbackPosition);
                BaseDXDrawableItem enabledState = CreateState(receiverProperty["enabled"]?["0"] as WzCanvasProperty, device, fallbackPosition) ?? disabledState;
                if (disabledState == null && enabledState == null)
                {
                    return null;
                }

                return new UIObject(
                    disabledState ?? enabledState,
                    enabledState ?? disabledState,
                    enabledState ?? disabledState,
                    enabledState ?? disabledState);
            }
            catch
            {
                return null;
            }
        }


        /// <summary>
        /// Load a tab button from WZ property
        /// </summary>
        private static UIObject LoadTabButton(WzSubProperty parent, string tabName,
            WzBinaryProperty clickSound, WzBinaryProperty overSound, GraphicsDevice device)
        {
            WzSubProperty tabProperty = (WzSubProperty)parent?[tabName];
            if (tabProperty == null)
                return null;


            try
            {
                return new UIObject(tabProperty, clickSound, overSound, false, Point.Zero, device);
            }
            catch
            {
                return null;
            }
        }


        private static UIObject LoadInventoryCanvasTabButton(WzSubProperty itemProperty, string tabIndex,
            WzBinaryProperty clickSound, WzBinaryProperty overSound, GraphicsDevice device)
        {
            WzCanvasProperty enabledCanvas = itemProperty?["Tab"]?["enabled"]?[tabIndex] as WzCanvasProperty;
            WzCanvasProperty disabledCanvas = itemProperty?["Tab"]?["disabled"]?[tabIndex] as WzCanvasProperty;
            if (enabledCanvas == null || disabledCanvas == null)
            {
                return null;
            }


            try
            {
                Texture2D enabledTexture = enabledCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                Texture2D disabledTexture = disabledCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (enabledTexture == null || disabledTexture == null)
                {
                    return null;
                }


                Point enabledOffset = ResolveCanvasOffset(enabledCanvas, Point.Zero);
                Point disabledOffset = ResolveCanvasOffset(disabledCanvas, Point.Zero);
                BaseDXDrawableItem normalState = new BaseDXDrawableItem(new DXObject(disabledOffset.X, disabledOffset.Y, disabledTexture), false);
                BaseDXDrawableItem pressedState = new BaseDXDrawableItem(new DXObject(enabledOffset.X, disabledOffset.Y, enabledTexture), false);
                UIObject button = new UIObject(normalState, normalState, pressedState, pressedState);
                button.X = disabledOffset.X;
                button.Y = disabledOffset.Y;
                return button;
            }
            catch
            {
                return null;
            }
        }


        private static UIObject LoadQuestCanvasTabButton(WzSubProperty questProperty, string tabIndex,
            WzBinaryProperty clickSound, WzBinaryProperty overSound, GraphicsDevice device)
        {
            WzCanvasProperty enabledCanvas = questProperty?["Tab"]?["enabled"]?[tabIndex] as WzCanvasProperty;
            WzCanvasProperty disabledCanvas = questProperty?["Tab"]?["disabled"]?[tabIndex] as WzCanvasProperty;
            if (enabledCanvas == null || disabledCanvas == null)
            {
                return null;
            }


            try
            {
                Texture2D enabledTexture = enabledCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                Texture2D disabledTexture = disabledCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (enabledTexture == null || disabledTexture == null)
                {
                    return null;
                }


                Point enabledOffset = ResolveCanvasOffset(enabledCanvas, Point.Zero);
                Point disabledOffset = ResolveCanvasOffset(disabledCanvas, Point.Zero);
                BaseDXDrawableItem normalState = new BaseDXDrawableItem(new DXObject(disabledOffset.X, disabledOffset.Y, disabledTexture), false);
                BaseDXDrawableItem pressedState = new BaseDXDrawableItem(new DXObject(enabledOffset.X, enabledOffset.Y, enabledTexture), false);
                UIObject button = new UIObject(normalState, normalState, pressedState, pressedState);
                button.X = disabledOffset.X;
                button.Y = disabledOffset.Y;
                return button;
            }
            catch
            {
                return null;
            }
        }

        private static UIObject LoadCanvasStateTabButton(
            WzSubProperty tabProperty,
            string tabIndex,
            GraphicsDevice device)
        {
            WzCanvasProperty enabledCanvas = tabProperty?["enabled"]?[tabIndex] as WzCanvasProperty;
            WzCanvasProperty disabledCanvas = tabProperty?["disabled"]?[tabIndex] as WzCanvasProperty;
            if (enabledCanvas == null || disabledCanvas == null)
            {
                return null;
            }

            try
            {
                Texture2D enabledTexture = enabledCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                Texture2D disabledTexture = disabledCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (enabledTexture == null || disabledTexture == null)
                {
                    return null;
                }

                Point enabledOffset = ResolveCanvasOffset(enabledCanvas, Point.Zero);
                Point disabledOffset = ResolveCanvasOffset(disabledCanvas, Point.Zero);
                BaseDXDrawableItem normalState = new BaseDXDrawableItem(new DXObject(disabledOffset.X, disabledOffset.Y, disabledTexture), false);
                BaseDXDrawableItem pressedState = new BaseDXDrawableItem(new DXObject(enabledOffset.X, enabledOffset.Y, enabledTexture), false);
                UIObject button = new UIObject(normalState, normalState, pressedState, pressedState)
                {
                    X = disabledOffset.X,
                    Y = disabledOffset.Y
                };
                return button;
            }
            catch
            {
                return null;
            }
        }


        private static Texture2D[] LoadInventoryMarkerTextures(WzSubProperty itemProperty, string markerFamilyName, GraphicsDevice device)
        {
            Texture2D[] textures = new Texture2D[6];
            WzSubProperty markerFamily = itemProperty?[markerFamilyName] as WzSubProperty;
            if (markerFamily == null)
            {
                return textures;
            }


            for (int i = 0; i < textures.Length; i++)
            {
                textures[i] = LoadCanvasTexture(markerFamily, i.ToString(), device);
            }


            return textures;

        }



        private static Point ResolveCanvasOffset(WzSubProperty parent, string name, Point fallback)
        {
            WzCanvasProperty canvas = parent?[name] as WzCanvasProperty;
            System.Drawing.PointF? origin = canvas?.GetCanvasOriginPosition();
            if (!origin.HasValue)
            {
                return fallback;
            }


            return new Point(-(int)origin.Value.X, -(int)origin.Value.Y);

        }



        private static Point ResolveCanvasOffset(WzCanvasProperty canvas, Point fallback)
        {
            System.Drawing.PointF? origin = canvas?.GetCanvasOriginPosition();
            if (!origin.HasValue)
            {
                return fallback;
            }


            return new Point(-(int)origin.Value.X, -(int)origin.Value.Y);

        }

        private static SelectorOverlayFrame LoadSelectorOverlayFrame(WzSubProperty property, GraphicsDevice device)
        {
            if (property == null || device == null)
            {
                return default;
            }

            WzCanvasProperty canvas = property["0"] as WzCanvasProperty
                ?? property.WzProperties.OfType<WzCanvasProperty>().FirstOrDefault();
            if (canvas == null)
            {
                return default;
            }

            Texture2D texture = LoadCanvasTexture(canvas, device);
            if (texture == null)
            {
                return default;
            }

            return new SelectorOverlayFrame(texture, ResolveCanvasOffset(canvas, Point.Zero));
        }

        private static List<IDXObject> LoadWindowOverlayFrames(WzSubProperty animationProperty, GraphicsDevice device)
        {
            var frames = new List<IDXObject>();
            if (animationProperty == null || device == null)
            {
                return frames;
            }

            for (int i = 0; ; i++)
            {
                if (animationProperty[i.ToString(CultureInfo.InvariantCulture)] is not WzCanvasProperty canvas)
                {
                    break;
                }

                Texture2D texture = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (texture == null)
                {
                    continue;
                }

                Point offset = ResolveCanvasOffset(canvas, Point.Zero);
                int delay = Math.Max(1, (canvas["delay"] as WzIntProperty)?.Value ?? 100);
                frames.Add(new DXObject(offset.X, offset.Y, texture, delay));
            }

            return frames;
        }

        private static void ConfigureProductionEnhancementAnimationDisplayer(
            UIWindowManager manager,
            GraphicsDevice device,
            WzSubProperty hammerProperty)
        {
            ProductionEnhancementAnimationDisplayer animationDisplayer = manager?.ProductionEnhancementAnimationDisplayer;
            if (animationDisplayer == null || device == null)
            {
                return;
            }

            WzImage basicEffectImage = HaCreator.Program.FindImage("Effect", "BasicEff.img");
            WzSubProperty itemMakeProperty = basicEffectImage?["ItemMake"] as WzSubProperty;
            WzSubProperty enchantProperty = basicEffectImage?["Enchant"] as WzSubProperty;
            WzSubProperty skillBookProperty = basicEffectImage?["SkillBook"] as WzSubProperty;

            animationDisplayer.ConfigureItemMake(
                LoadWindowOverlayFrames(itemMakeProperty?["Success"] as WzSubProperty, device),
                LoadWindowOverlayFrames(itemMakeProperty?["Failure"] as WzSubProperty, device));
            animationDisplayer.ConfigureItemUpgrade(
                LoadWindowOverlayFrames(enchantProperty?["Success"] as WzSubProperty, device),
                LoadWindowOverlayFrames(enchantProperty?["Failure"] as WzSubProperty, device));
            animationDisplayer.ConfigureSkillBook(
                LoadWindowOverlayFrames(skillBookProperty?["Success"]?["0"] as WzSubProperty, device),
                LoadWindowOverlayFrames(skillBookProperty?["Success"]?["1"] as WzSubProperty, device),
                LoadWindowOverlayFrames(skillBookProperty?["Failure"]?["0"] as WzSubProperty, device),
                LoadWindowOverlayFrames(skillBookProperty?["Failure"]?["1"] as WzSubProperty, device));

            if (hammerProperty != null)
            {
                animationDisplayer.ConfigureViciousHammer(
                    LoadWindowOverlayFrames(hammerProperty["EffectP"] as WzSubProperty, device),
                    LoadWindowOverlayFrames(hammerProperty["EffectE"] as WzSubProperty, device));
            }
        }

        private static void ConfigureCashGachaponAnimationDisplayer(
            UIWindowManager manager,
            GraphicsDevice device)
        {
            ProductionEnhancementAnimationDisplayer animationDisplayer = manager?.ProductionEnhancementAnimationDisplayer;
            if (animationDisplayer == null || device == null)
            {
                return;
            }

            WzImage uiWindowImage = global::HaCreator.Program.FindImage("ui", "UIWindow.img");
            if (uiWindowImage == null)
            {
                return;
            }

            string openWindowPath = MapleStoryStringPool.ResolveCashGachaponWindowPropertyPath(isCopyResult: false);
            string copyWindowPath = MapleStoryStringPool.ResolveCashGachaponWindowPropertyPath(isCopyResult: true);
            WzSubProperty openWindowProperty = uiWindowImage[openWindowPath] as WzSubProperty;
            WzSubProperty copyWindowProperty = uiWindowImage[copyWindowPath] as WzSubProperty;

            animationDisplayer.ConfigureCashGachapon(
                LoadWindowOverlayFrames(openWindowProperty?["EffectNormal"] as WzSubProperty, device),
                LoadWindowOverlayFrames(openWindowProperty?["EffectJackpot"] as WzSubProperty, device),
                LoadWindowOverlayFrames(copyWindowProperty?["EffectNormal"] as WzSubProperty, device),
                LoadWindowOverlayFrames(copyWindowProperty?["EffectJackpot"] as WzSubProperty, device));
        }

        private static SelectorAnimatedOverlay LoadSelectorAnimatedOverlay(WzImageProperty property, GraphicsDevice device, int fallbackFrameDelayMs = 100)
        {
            if (property == null || device == null)
            {
                return null;
            }

            List<WzCanvasProperty> canvases = new List<WzCanvasProperty>();
            if (property is WzCanvasProperty singleCanvas)
            {
                canvases.Add(singleCanvas);
            }
            else if (property is WzSubProperty subProperty)
            {
                canvases.AddRange(
                    subProperty.WzProperties
                        .OfType<WzCanvasProperty>()
                        .OrderBy(canvas => int.TryParse(canvas.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int frameIndex) ? frameIndex : int.MaxValue)
                        .ThenBy(canvas => canvas.Name, StringComparer.Ordinal));
            }

            if (canvases.Count == 0)
            {
                return null;
            }

            List<SelectorAnimatedOverlayFrame> frames = new List<SelectorAnimatedOverlayFrame>(canvases.Count);
            int frameDelayMs = fallbackFrameDelayMs;
            foreach (WzCanvasProperty canvas in canvases)
            {
                Texture2D texture = LoadCanvasTexture(canvas, device);
                if (texture == null)
                {
                    continue;
                }

                WzIntProperty delayProperty = canvas["delay"] as WzIntProperty;
                if (delayProperty != null && delayProperty.Value > 0)
                {
                    frameDelayMs = delayProperty.Value;
                }

                frames.Add(new SelectorAnimatedOverlayFrame(texture, ResolveCanvasOffset(canvas, Point.Zero)));
            }

            return frames.Count == 0
                ? null
                : new SelectorAnimatedOverlay(frames, frameDelayMs);
        }



        private static IDXObject LoadWindowCanvasLayer(WzSubProperty parent, string name, GraphicsDevice device)
        {
            WzCanvasProperty canvas = parent?[name] as WzCanvasProperty;
            if (canvas == null)
            {
                return null;
            }


            Texture2D texture = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (texture == null)
            {
                return null;
            }


            System.Drawing.PointF origin = canvas.GetCanvasOriginPosition();

            return new DXObject(origin, texture, 0);

        }



        private static IDXObject LoadWindowCanvasLayerWithOffset(WzSubProperty parent, string name, GraphicsDevice device, out Point offset)

        {

            offset = Point.Zero;



            WzCanvasProperty canvas = parent?[name] as WzCanvasProperty;
            if (canvas == null)
            {
                return null;
            }


            Texture2D texture = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (texture == null)
            {
                return null;
            }


            System.Drawing.PointF origin = canvas.GetCanvasOriginPosition();
            offset = new Point(-(int)origin.X, -(int)origin.Y);
            return new DXObject(0, 0, texture, 0);
        }

        private static IDXObject LoadWindowCanvasLayerFromClientUiStringPoolPath(
            int stringPoolId,
            string fallbackFormatPath,
            int formatValue,
            WzImage primaryImage,
            WzImage secondaryImage,
            GraphicsDevice device,
            out Point offset)
        {
            string resolvedPath = ResolveClientUiStringPoolPath(stringPoolId, fallbackFormatPath, formatValue);
            return LoadWindowCanvasLayerFromClientUiPath(resolvedPath, primaryImage, secondaryImage, device, out offset);
        }

        private static IDXObject LoadWindowCanvasLayerFromClientUiStringPoolPath(
            int stringPoolId,
            string fallbackPath,
            WzImage primaryImage,
            WzImage secondaryImage,
            GraphicsDevice device,
            out Point offset)
        {
            string resolvedPath = MapleStoryStringPool.GetOrFallback(stringPoolId, fallbackPath);
            return LoadWindowCanvasLayerFromClientUiPath(resolvedPath, primaryImage, secondaryImage, device, out offset);
        }

        private static string ResolveClientUiStringPoolPath(int stringPoolId, string fallbackFormatPath, int formatValue)
        {
            string resolvedFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                stringPoolId,
                fallbackFormatPath,
                1,
                out _);

            try
            {
                return string.Format(CultureInfo.InvariantCulture, resolvedFormat, formatValue);
            }
            catch (FormatException)
            {
                return string.Format(CultureInfo.InvariantCulture, fallbackFormatPath, formatValue);
            }
        }

        private static IDXObject LoadWindowCanvasLayerFromClientUiPath(
            string clientUiPath,
            WzImage primaryImage,
            WzImage secondaryImage,
            GraphicsDevice device,
            out Point offset)
        {
            offset = Point.Zero;

            if (string.IsNullOrWhiteSpace(clientUiPath) || device == null)
            {
                return null;
            }

            WzCanvasProperty canvas = ResolveCanvasFromClientUiPath(clientUiPath, primaryImage, secondaryImage);
            if (canvas == null)
            {
                return null;
            }

            Texture2D texture = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (texture == null)
            {
                return null;
            }

            System.Drawing.PointF origin = canvas.GetCanvasOriginPosition();
            offset = new Point(-(int)origin.X, -(int)origin.Y);
            return new DXObject(0, 0, texture, 0);
        }

        private static WzCanvasProperty ResolveCanvasFromClientUiPath(
            string clientUiPath,
            WzImage primaryImage,
            WzImage secondaryImage)
        {
            if (string.IsNullOrWhiteSpace(clientUiPath))
            {
                return null;
            }

            string[] segments = clientUiPath
                .Replace('\\', '/')
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            int imageSegmentIndex = Array.FindIndex(
                segments,
                segment => segment.EndsWith(".img", StringComparison.OrdinalIgnoreCase));
            if (imageSegmentIndex < 0 || imageSegmentIndex >= segments.Length - 1)
            {
                return null;
            }

            string imageName = segments[imageSegmentIndex];
            string innerPath = string.Join("/", segments.Skip(imageSegmentIndex + 1));
            foreach (WzImage image in EnumerateClientUiImages(imageName, primaryImage, secondaryImage))
            {
                WzCanvasProperty canvas = ResolveCanvasProperty(image, innerPath);
                if (canvas != null)
                {
                    return canvas;
                }
            }

            return null;
        }

        private static IEnumerable<WzImage> EnumerateClientUiImages(string imageName, WzImage primaryImage, WzImage secondaryImage)
        {
            WzImage[] ordered = string.Equals(imageName, secondaryImage?.Name, StringComparison.OrdinalIgnoreCase)
                ? new[] { secondaryImage, primaryImage }
                : new[] { primaryImage, secondaryImage };

            HashSet<WzImage> yielded = new();
            foreach (WzImage image in ordered)
            {
                if (image != null && yielded.Add(image))
                {
                    yield return image;
                }
            }
        }


        /// <summary>
        /// Create a placeholder window texture when WZ assets aren't available
        /// </summary>
        private static Texture2D CreatePlaceholderWindowTexture(GraphicsDevice device, int width, int height, string title)
        {
            Texture2D texture = new Texture2D(device, width, height);
            Color[] data = new Color[width * height];


            // Window background color
            Color bgColor = new Color(40, 40, 60, 230);
            Color titleBarColor = new Color(60, 60, 90, 255);
            Color borderColor = new Color(80, 80, 120, 255);


            int titleBarHeight = 25;



            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;


                    // Border
                    if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                    {
                        data[index] = borderColor;
                    }
                    // Title bar
                    else if (y < titleBarHeight)
                    {
                        data[index] = titleBarColor;
                    }
                    // Title bar bottom border
                    else if (y == titleBarHeight)
                    {
                        data[index] = borderColor;
                    }
                    // Background
                    else
                    {
                        data[index] = bgColor;
                    }
                }
            }


            texture.SetData(data);

            return texture;

        }

        private static Texture2D CreateUtilDlgNoticeFrameTexture(
            WzSubProperty utilDialogProperty,
            WzImage uiWindowImage,
            WzImage uiWindow2Image,
            GraphicsDevice device,
            int width = 312,
            int height = 132)
        {
            Texture2D noticeTexture = LoadCanvasTexture(utilDialogProperty, "notice", device)
                ?? LoadTextureFromUiPath(PacketOwnedRewardResultRuntime.GetUtilDlgNoticeBackgroundResourcePath(), device, uiWindow2Image, uiWindowImage);
            Texture2D topTexture = LoadCanvasTexture(utilDialogProperty, "t", device)
                ?? LoadTextureFromUiPath(PacketOwnedRewardResultRuntime.GetUtilDlgNoticeTopResourcePath(), device, uiWindow2Image, uiWindowImage);
            Texture2D centerTexture = LoadCanvasTexture(utilDialogProperty, "c", device)
                ?? LoadTextureFromUiPath(PacketOwnedRewardResultRuntime.GetUtilDlgNoticeCenterResourcePath(), device, uiWindow2Image, uiWindowImage);
            Texture2D bottomTexture = LoadCanvasTexture(utilDialogProperty, "s", device)
                ?? LoadTextureFromUiPath(PacketOwnedRewardResultRuntime.GetUtilDlgNoticeBottomResourcePath(), device, uiWindow2Image, uiWindowImage);

            if (topTexture != null && centerTexture != null && bottomTexture != null)
            {
                Texture2D stitchedTexture = CreateStitchedUtilDlgFrameTexture(
                    topTexture,
                    centerTexture,
                    bottomTexture,
                    topTexture.Width,
                    height,
                    device);
                if (stitchedTexture != null)
                {
                    int cropX = ResolveUtilDlgFrameCropX(stitchedTexture, noticeTexture, width);
                    return CropTextureHorizontally(stitchedTexture, cropX, width, device);
                }
            }

            if (noticeTexture != null
                && width == noticeTexture.Width)
            {
                if (height == noticeTexture.Height)
                {
                    return noticeTexture;
                }

                if (height > noticeTexture.Height)
                {
                    return CreateExtendedUtilDlgNoticeFrameTexture(noticeTexture, height, device);
                }
            }

            return noticeTexture;
        }

        private static Texture2D CreateStitchedUtilDlgFrameTexture(
            Texture2D topTexture,
            Texture2D centerTexture,
            Texture2D bottomTexture,
            int width,
            int height,
            GraphicsDevice device)
        {
            // CUtilDlg::OnCreate draws the bottom slice at m_height - 0x0F, so the
            // frame canvas clips to the first 15 pixels of UtilDlgEx/s.
            const int clientVisibleBottomHeight = 15;
            const int clientCenterBandStride = 16;

            int frameWidth = Math.Max(1, width);
            int frameHeight = Math.Max(1, height);
            int topHeight = Math.Min(topTexture.Height, frameHeight);
            int bottomHeight = Math.Min(
                Math.Min(bottomTexture.Height, clientVisibleBottomHeight),
                Math.Max(0, frameHeight - topHeight));
            int centerHeight = Math.Max(0, frameHeight - topHeight - bottomHeight);

            Color[] topData = GetTextureData(topTexture);
            Color[] centerData = GetTextureData(centerTexture);
            Color[] bottomData = GetTextureData(bottomTexture);
            Color[] frameData = Enumerable.Repeat(Color.Transparent, frameWidth * frameHeight).ToArray();

            BlitTextureStrip(frameData, frameWidth, 0, topData, topTexture.Width, topHeight, frameWidth);

            foreach (int centerY in EnumerateUtilDlgCenterBandYPositions(topHeight, centerHeight, clientCenterBandStride))
            {
                int stripHeight = Math.Min(centerTexture.Height, (topHeight + centerHeight) - centerY);
                BlitTextureStrip(frameData, frameWidth, centerY, centerData, centerTexture.Width, stripHeight, frameWidth);
            }

            if (bottomHeight > 0)
            {
                BlitTextureStrip(frameData, frameWidth, frameHeight - bottomHeight, bottomData, bottomTexture.Width, bottomHeight, frameWidth);
            }

            Texture2D frameTexture = new Texture2D(device, frameWidth, frameHeight);
            frameTexture.SetData(frameData);
            return frameTexture;
        }

        internal static IReadOnlyList<int> EnumerateUtilDlgCenterBandYPositions(
            int topHeight,
            int centerHeight,
            int stride)
        {
            int normalizedTopHeight = Math.Max(0, topHeight);
            int normalizedCenterHeight = Math.Max(0, centerHeight);
            int normalizedStride = Math.Max(1, stride);
            int centerEndY = normalizedTopHeight + normalizedCenterHeight;

            List<int> positions = new();
            for (int centerY = normalizedTopHeight; centerY < centerEndY; centerY += normalizedStride)
            {
                positions.Add(centerY);
            }

            return positions;
        }

        private static Texture2D CreateExtendedUtilDlgNoticeFrameTexture(
            Texture2D noticeTexture,
            int height,
            GraphicsDevice device)
        {
            if (noticeTexture == null)
            {
                return null;
            }

            int frameWidth = noticeTexture.Width;
            int frameHeight = Math.Max(noticeTexture.Height, height);
            int topSectionHeight = noticeTexture.Height - 64;
            int bottomSectionHeight = 64;
            int centerSectionHeight = Math.Max(1, PacketOwnedRewardNoticeWindow.FrameHeightStep);
            int centerSourceY = 28;

            Color[] noticeData = GetTextureData(noticeTexture);
            Color[] frameData = Enumerable.Repeat(Color.Transparent, frameWidth * frameHeight).ToArray();

            BlitTextureRows(frameData, frameWidth, 0, noticeData, frameWidth, 0, topSectionHeight);

            int centerDestinationY = topSectionHeight;
            int centerDestinationEndY = Math.Max(topSectionHeight, frameHeight - bottomSectionHeight);
            while (centerDestinationY < centerDestinationEndY)
            {
                int stripHeight = Math.Min(centerSectionHeight, centerDestinationEndY - centerDestinationY);
                BlitTextureRows(frameData, frameWidth, centerDestinationY, noticeData, frameWidth, centerSourceY, stripHeight);
                centerDestinationY += stripHeight;
            }

            BlitTextureRows(
                frameData,
                frameWidth,
                frameHeight - bottomSectionHeight,
                noticeData,
                frameWidth,
                noticeTexture.Height - bottomSectionHeight,
                bottomSectionHeight);

            Texture2D frameTexture = new(device, frameWidth, frameHeight);
            frameTexture.SetData(frameData);
            return frameTexture;
        }

        internal static int ResolveUtilDlgFrameCropX(
            Color[] stitchedData,
            int stitchedWidth,
            int stitchedHeight,
            Color[] noticeData,
            int noticeWidth,
            int noticeHeight,
            int targetWidth)
        {
            if (stitchedData == null
                || stitchedWidth <= 0
                || stitchedHeight <= 0
                || targetWidth <= 0)
            {
                return 0;
            }

            int maxCropX = Math.Max(0, stitchedWidth - Math.Min(targetWidth, stitchedWidth));
            if (noticeData == null
                || noticeWidth <= 0
                || noticeHeight <= 0
                || noticeData.Length < noticeWidth * noticeHeight)
            {
                return maxCropX / 2;
            }

            int comparisonWidth = Math.Min(Math.Min(targetWidth, noticeWidth), stitchedWidth);
            int comparisonHeight = Math.Min(stitchedHeight, noticeHeight);
            if (comparisonWidth <= 0 || comparisonHeight <= 0)
            {
                return maxCropX / 2;
            }

            long bestScore = long.MaxValue;
            int bestCropX = maxCropX / 2;
            for (int cropX = 0; cropX <= maxCropX; cropX++)
            {
                long score = 0;
                for (int y = 0; y < comparisonHeight; y++)
                {
                    int stitchedRow = y * stitchedWidth;
                    int noticeRow = y * noticeWidth;
                    for (int x = 0; x < comparisonWidth; x++)
                    {
                        Color stitched = stitchedData[stitchedRow + cropX + x];
                        Color notice = noticeData[noticeRow + x];
                        score += Math.Abs(stitched.A - notice.A);
                        score += Math.Abs(stitched.R - notice.R);
                        score += Math.Abs(stitched.G - notice.G);
                        score += Math.Abs(stitched.B - notice.B);
                    }
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    bestCropX = cropX;
                }
            }

            return bestCropX;
        }

        private static int ResolveUtilDlgFrameCropX(
            Texture2D stitchedTexture,
            Texture2D noticeTexture,
            int targetWidth)
        {
            if (stitchedTexture == null)
            {
                return 0;
            }

            Color[] stitchedData = GetTextureData(stitchedTexture);
            Color[] noticeData = noticeTexture != null
                ? GetTextureData(noticeTexture)
                : null;
            return ResolveUtilDlgFrameCropX(
                stitchedData,
                stitchedTexture.Width,
                stitchedTexture.Height,
                noticeData,
                noticeTexture?.Width ?? 0,
                noticeTexture?.Height ?? 0,
                targetWidth);
        }

        private static Texture2D CropTextureHorizontally(
            Texture2D texture,
            int cropX,
            int targetWidth,
            GraphicsDevice device)
        {
            if (texture == null)
            {
                return null;
            }

            int width = Math.Max(1, Math.Min(targetWidth, texture.Width));
            int clampedCropX = Math.Clamp(cropX, 0, Math.Max(0, texture.Width - width));
            if (clampedCropX == 0 && width == texture.Width)
            {
                return texture;
            }

            int height = texture.Height;
            Color[] sourceData = GetTextureData(texture);
            Color[] croppedData = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                Array.Copy(
                    sourceData,
                    (y * texture.Width) + clampedCropX,
                    croppedData,
                    y * width,
                    width);
            }

            Texture2D croppedTexture = new(device, width, height);
            croppedTexture.SetData(croppedData);
            return croppedTexture;
        }

        private static Color[] GetTextureData(Texture2D texture)
        {
            Color[] data = new Color[texture.Width * texture.Height];
            texture.GetData(data);
            return data;
        }

        private static void BlitTextureStrip(
            Color[] destination,
            int destinationWidth,
            int destinationY,
            Color[] source,
            int sourceWidth,
            int sourceHeight,
            int blitWidth,
            int sourceY = 0)
        {
            if (destination == null
                || source == null
                || destinationWidth <= 0
                || sourceWidth <= 0
                || sourceHeight <= 0
                || blitWidth <= 0)
            {
                return;
            }

            int copyWidth = Math.Min(Math.Min(destinationWidth, sourceWidth), blitWidth);
            for (int row = 0; row < sourceHeight; row++)
            {
                int sourceRow = sourceY + row;
                Array.Copy(source, sourceRow * sourceWidth, destination, (destinationY + row) * destinationWidth, copyWidth);
            }
        }

        private static void BlitTextureRows(
            Color[] destination,
            int destinationWidth,
            int destinationY,
            Color[] source,
            int sourceWidth,
            int sourceY,
            int rowCount)
        {
            BlitTextureStrip(destination, destinationWidth, destinationY, source, sourceWidth, rowCount, destinationWidth, sourceY);
        }



        public static void RegisterLoginTitleWindow(
            UIWindowManager manager,
            WzImage loginImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.LoginTitle) != null)
            {
                return;
            }


            WzSubProperty titleProperty = loginImage?["Title"] as WzSubProperty;

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;

            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;



            IDXObject frame = LoadWindowCanvasLayer(titleProperty, "backFrame", device);
            IDXObject titleLayer = LoadWindowCanvasLayerWithOffset(titleProperty, "MSTitle", device, out Point titleOffset);
            IDXObject dollsLayer = LoadWindowCanvasLayerWithOffset(titleProperty, "MapleDolls", device, out Point dollsOffset);
            IDXObject signboardLayer = LoadWindowCanvasLayerWithOffset(titleProperty, "signboard", device, out Point signboardOffset);
            IDXObject idFieldLayer = LoadWindowCanvasLayerWithOffset(titleProperty, "ID", device, out Point idFieldOffset);
            IDXObject passwordFieldLayer = LoadWindowCanvasLayerWithOffset(titleProperty, "PW", device, out Point passwordFieldOffset);
            IReadOnlyList<IReadOnlyList<CharacterSelectWindow.AnimationFrame>> effectFrameSets =
                LoadCharacterSelectAnimationFrameSets(titleProperty?["effect"] as WzSubProperty, device);


            if (frame == null)
            {
                Texture2D frameTexture = CreatePlaceholderWindowTexture(device, 800, 412, "Login");
                frame = new DXObject(0, 0, frameTexture, 0);
            }


            UIObject loginButton = LoadButton(titleProperty, "BtLogin", btClickSound, btOverSound, device);

            UIObject guestLoginButton = LoadButton(titleProperty, "BtGuestLogin", btClickSound, btOverSound, device);
            UIObject newButton = LoadButton(titleProperty, "BtNew", btClickSound, btOverSound, device);
            UIObject homePageButton = LoadButton(titleProperty, "BtHomePage", btClickSound, btOverSound, device);
            UIObject quitButton = LoadButton(titleProperty, "BtQuit", btClickSound, btOverSound, device);
            UIObject saveIdButton = LoadButton(titleProperty, "BtLoginIDSave", btClickSound, btOverSound, device);

            UIObject idLostButton = LoadButton(titleProperty, "BtLoginIDLost", btClickSound, btOverSound, device);

            UIObject passwordLostButton = LoadButton(titleProperty, "BtPasswdLost", btClickSound, btOverSound, device);
            UIObject softKeyboardButton = LoadButton(titleProperty, "BtSoftkeyboard", btClickSound, btOverSound, device);
            Texture2D saveIdUncheckedTexture = LoadCanvasTexture(titleProperty?["check"] as WzSubProperty, "0", device);
            Texture2D saveIdCheckedTexture = LoadCanvasTexture(titleProperty?["check"] as WzSubProperty, "1", device);



            const int ownerOffsetX = 334;

            const int ownerOffsetY = 225;



            if (loginButton != null)
            {
                loginButton.X = ownerOffsetX + 178;
                loginButton.Y = ownerOffsetY + 15;
            }


            if (guestLoginButton != null)
            {
                guestLoginButton.X = ownerOffsetX + 178;
                guestLoginButton.Y = ownerOffsetY + 49;
            }


            if (saveIdButton != null)
            {
                saveIdButton.X = ownerOffsetX + 27;
                saveIdButton.Y = ownerOffsetY + 68;
            }


            if (idLostButton != null)
            {
                idLostButton.X = ownerOffsetX + 99;
                idLostButton.Y = ownerOffsetY + 68;
            }


            if (passwordLostButton != null)
            {
                passwordLostButton.X = ownerOffsetX + 171;
                passwordLostButton.Y = ownerOffsetY + 68;
            }

            if (softKeyboardButton != null)
            {
                softKeyboardButton.X = ownerOffsetX + 176;
                softKeyboardButton.Y = ownerOffsetY + 40;
            }


            if (newButton != null)
            {
                newButton.X = ownerOffsetX + 15;
                newButton.Y = ownerOffsetY + 88;
            }

            if (homePageButton != null)
            {
                homePageButton.X = ownerOffsetX + 87;
                homePageButton.Y = ownerOffsetY + 88;
            }

            if (quitButton != null)
            {
                quitButton.X = ownerOffsetX + 159;
                quitButton.Y = ownerOffsetY + 88;

            }



            LoginTitleWindow window = new LoginTitleWindow(
                frame,
                titleLayer,
                titleOffset,
                dollsLayer,
                dollsOffset,
                signboardLayer,
                signboardOffset,
                idFieldLayer,
                idFieldOffset,
                passwordFieldLayer,
                passwordFieldOffset,
                effectFrameSets,
                loginButton,
                guestLoginButton,
                newButton,
                homePageButton,
                quitButton,
                saveIdButton,
                idLostButton,
                passwordLostButton,
                softKeyboardButton,
                saveIdUncheckedTexture,
                saveIdCheckedTexture)
            {
                Position = new Point(
                    Math.Max(0, (screenWidth / 2) - 400),
                    Math.Max(0, (screenHeight / 2) - 300))
            };


            manager.RegisterCustomWindow(window);

        }



        public static void RegisterLoginCharacterSelectWindow(
            UIWindowManager manager,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.CharacterSelect) != null)
            {
                return;
            }


            WzImage loginImage = global::HaCreator.Program.FindImage("UI", "Login.img");
            WzSubProperty charSelectProperty = loginImage?["CharSelect"] as WzSubProperty;
            Texture2D frameTexture = CreateFilledTexture(device, 618, 320, Color.Transparent, Color.Transparent);
            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;


            UIObject enterButton = LoadButton(charSelectProperty, "BtSelect", btClickSound, btOverSound, device);

            UIObject newButton = LoadButton(charSelectProperty, "BtNew", btClickSound, btOverSound, device);

            UIObject deleteButton = LoadButton(charSelectProperty, "BtDelete", btClickSound, btOverSound, device);



            if (enterButton != null)
            {
                enterButton.X = 148;
                enterButton.Y = 246;
            }


            if (newButton != null)
            {
                newButton.X = 259;
                newButton.Y = 244;
            }


            if (deleteButton != null)
            {
                deleteButton.X = 370;
                deleteButton.Y = 238;
            }


            CharacterSelectWindow window = new CharacterSelectWindow(
                new DXObject(0, 0, frameTexture, 0),
                enterButton,
                newButton,
                deleteButton,
                LoadCharacterSelectAnimationFrameSets(charSelectProperty?["scroll"] as WzSubProperty, device),
                LoadCharacterSelectAnimationFrames(charSelectProperty?["effect"]?["0"] as WzSubProperty, device),
                LoadCharacterSelectAnimationFrames(charSelectProperty?["effect"]?["1"] as WzSubProperty, device),
                LoadCharacterSelectAnimationFrames(charSelectProperty?["character"]?["0"] as WzSubProperty, device),
                LoadCharacterSelectAnimationFrames(charSelectProperty?["BtSelect"]?["keyFocused"] as WzSubProperty, device),
                LoadCharacterSelectAnimationFrames(charSelectProperty?["BtNew"]?["keyFocused"] as WzSubProperty, device),
                LoadCharacterSelectAnimationFrames(charSelectProperty?["BtDelete"]?["keyFocused"] as WzSubProperty, device),
                LoadLoginProcessBalloonStyle(loginImage?["WorldNotice"]?["BalloonForLoginProcess"] as WzSubProperty, device),
                LoadOwnerCanvasFrame(ResolveNewestIndexedCanvas(charSelectProperty?["event"] as WzSubProperty), device),
                LoadCharacterSelectAnimationFrames(charSelectProperty?["character"]?["0"] as WzSubProperty, device))
            {
                Position = new Point(Math.Max(24, (screenWidth / 2) - 309), Math.Max(24, (screenHeight / 2) - 160))
            };


            WzSubProperty closeButtonProperty = basicImage?["BtClose"] as WzSubProperty;
            if (closeButtonProperty != null)
            {
                try
                {
                    UIObject closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = 590;
                    closeBtn.Y = 8;
                    window.InitializeCloseButton(closeBtn);
                }
                catch
                {
                }
            }


            manager.RegisterCustomWindow(window);



            RegisterAvatarPreviewCarouselWindow(manager, charSelectProperty, device, screenWidth, screenHeight);

        }



        public static void RegisterLoginCreateCharacterWindow(
            UIWindowManager manager,
            WzImage loginImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            if (manager == null
                || manager.GetWindow(MapSimulatorWindowNames.LoginCreateCharacterRaceSelect) != null
                || manager.GetWindow(MapSimulatorWindowNames.LoginCreateCharacterJobSelect) != null
                || manager.GetWindow(MapSimulatorWindowNames.LoginCreateCharacterAvatarSelect) != null
                || manager.GetWindow(MapSimulatorWindowNames.LoginCreateCharacterNameSelect) != null)
            {
                return;
            }


            WzSubProperty newCharProperty = loginImage?["NewChar"] as WzSubProperty;
            if (newCharProperty == null)
            {
                return;
            }


            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;

            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;

            Texture2D frameTexture = CreateFilledTexture(device, 800, 600, Color.Transparent, Color.Transparent);
            var stageTexturesByRace = new Dictionary<LoginCreateCharacterRaceKind, IReadOnlyDictionary<LoginCreateCharacterStage, Texture2D>>();
            var avatarEnabledTexturesByRace = new Dictionary<LoginCreateCharacterRaceKind, IReadOnlyList<Texture2D>>();
            var avatarDisabledTexturesByRace = new Dictionary<LoginCreateCharacterRaceKind, IReadOnlyList<Texture2D>>();
            var racePreviewTexturesByRace = new Dictionary<LoginCreateCharacterRaceKind, Texture2D>();
            var raceConfirmLabelFramesByRace = new Dictionary<LoginCreateCharacterRaceKind, CharacterSelectWindow.AnimationFrame>();
            WzSubProperty raceSelectConfirmProperty = loginImage?["RaceSelect"]?["confirm"] as WzSubProperty;

            foreach (LoginCreateCharacterRaceKind race in LoginCreateCharacterFlowState.SupportedRaces)
            {
                WzSubProperty raceProperty = ResolveCreateCharacterRaceProperty(loginImage, race, newCharProperty);
                stageTexturesByRace[race] = new Dictionary<LoginCreateCharacterStage, Texture2D>
                {
                    [LoginCreateCharacterStage.RaceSelect] = LoadCanvasTexture(raceProperty, "charAlert", device)
                        ?? LoadCanvasTexture(newCharProperty, "charAlert", device),
                    [LoginCreateCharacterStage.JobSelect] = LoadCanvasTexture(raceProperty, "charJob", device)
                        ?? LoadCanvasTexture(newCharProperty, "charJob", device),
                    [LoginCreateCharacterStage.AvatarSelect] = LoadCanvasTexture(raceProperty, "charSet", device)
                        ?? LoadCanvasTexture(newCharProperty, "charSet", device),
                    [LoginCreateCharacterStage.NameSelect] = LoadCanvasTexture(raceProperty, "charName", device)
                        ?? LoadCanvasTexture(newCharProperty, "charName", device)
                };
                avatarEnabledTexturesByRace[race] = LoadIndexedCanvasTextureList(raceProperty?["avatarSel"] as WzSubProperty, "normal", device);
                avatarDisabledTexturesByRace[race] = LoadIndexedCanvasTextureList(raceProperty?["avatarSel"] as WzSubProperty, "disabled", device);
                racePreviewTexturesByRace[race] = LoadCanvasTexture(raceProperty, "charAlert", device);
                int raceConfirmLabelIndex = Array.IndexOf(LoginCreateCharacterFlowState.SupportedRaces, race);
                raceConfirmLabelFramesByRace[race] = LoadSingleAnimationFrame(
                    raceSelectConfirmProperty?["race"] as WzSubProperty,
                    raceConfirmLabelIndex switch
                    {
                        0 => "0",
                        1 => "1",
                        2 => "2",
                        3 => "3",
                        4 => "4",
                        _ => "0"
                    },
                    device);
            }


            IReadOnlyList<Texture2D> jobTextures = LoadIndexedCanvasTextureList(newCharProperty?["jobSelect"] as WzSubProperty, device);
            IReadOnlyList<CharacterSelectWindow.AnimationFrame> diceFrames = LoadCharacterSelectAnimationFrames(newCharProperty?["dice"] as WzSubProperty, device);
            Texture2D leftArrowTexture = LoadCanvasTexture(newCharProperty?["BtLeft"]?["normal"] as WzSubProperty, "0", device);
            Texture2D rightArrowTexture = LoadCanvasTexture(newCharProperty?["BtRight"]?["normal"] as WzSubProperty, "0", device);
            Texture2D raceConfirmBackgroundTexture = LoadCanvasTexture(raceSelectConfirmProperty, "backgrnd", device);
            Texture2D raceConfirmOkTexture = LoadCanvasTexture(raceSelectConfirmProperty?["BtOK"]?["normal"] as WzSubProperty, "0", device);
            Texture2D raceConfirmCancelTexture = LoadCanvasTexture(raceSelectConfirmProperty?["BtCancel"]?["normal"] as WzSubProperty, "0", device);
            Point windowPosition = new(Math.Max(0, (screenWidth / 2) - 400), Math.Max(0, (screenHeight / 2) - 300));

            LoginCreateCharacterWindowBase[] windows =
            {
                new LoginCreateCharacterRaceSelectWindow(
                    new DXObject(0, 0, frameTexture, 0),
                    stageTexturesByRace,
                    jobTextures,
                    avatarEnabledTexturesByRace,
                    avatarDisabledTexturesByRace,
                    racePreviewTexturesByRace,
                    diceFrames,
                    leftArrowTexture,
                    rightArrowTexture,
                    raceConfirmBackgroundTexture,
                    raceConfirmOkTexture,
                    raceConfirmCancelTexture,
                    raceConfirmLabelFramesByRace,
                    LoadButton(newCharProperty, "BtYes", btClickSound, btOverSound, device),
                    LoadButton(newCharProperty, "BtNo", btClickSound, btOverSound, device),
                    LoadButton(newCharProperty, "BtCheck", btClickSound, btOverSound, device)),
                new LoginCreateCharacterJobSelectWindow(
                    new DXObject(0, 0, frameTexture, 0),
                    stageTexturesByRace,
                    jobTextures,
                    avatarEnabledTexturesByRace,
                    avatarDisabledTexturesByRace,
                    racePreviewTexturesByRace,
                    diceFrames,
                    leftArrowTexture,
                    rightArrowTexture,
                    raceConfirmBackgroundTexture,
                    raceConfirmOkTexture,
                    raceConfirmCancelTexture,
                    raceConfirmLabelFramesByRace,
                    LoadButton(newCharProperty, "BtYes", btClickSound, btOverSound, device),
                    LoadButton(newCharProperty, "BtNo", btClickSound, btOverSound, device),
                    LoadButton(newCharProperty, "BtCheck", btClickSound, btOverSound, device)),
                new LoginCreateCharacterAvatarSelectWindow(
                    new DXObject(0, 0, frameTexture, 0),
                    stageTexturesByRace,
                    jobTextures,
                    avatarEnabledTexturesByRace,
                    avatarDisabledTexturesByRace,
                    racePreviewTexturesByRace,
                    diceFrames,
                    leftArrowTexture,
                    rightArrowTexture,
                    raceConfirmBackgroundTexture,
                    raceConfirmOkTexture,
                    raceConfirmCancelTexture,
                    raceConfirmLabelFramesByRace,
                    LoadButton(newCharProperty, "BtYes", btClickSound, btOverSound, device),
                    LoadButton(newCharProperty, "BtNo", btClickSound, btOverSound, device),
                    LoadButton(newCharProperty, "BtCheck", btClickSound, btOverSound, device)),
                new LoginCreateCharacterNameSelectWindow(
                    new DXObject(0, 0, frameTexture, 0),
                    stageTexturesByRace,
                    jobTextures,
                    avatarEnabledTexturesByRace,
                    avatarDisabledTexturesByRace,
                    racePreviewTexturesByRace,
                    diceFrames,
                    leftArrowTexture,
                    rightArrowTexture,
                    raceConfirmBackgroundTexture,
                    raceConfirmOkTexture,
                    raceConfirmCancelTexture,
                    raceConfirmLabelFramesByRace,
                    LoadButton(newCharProperty, "BtYes", btClickSound, btOverSound, device),
                    LoadButton(newCharProperty, "BtNo", btClickSound, btOverSound, device),
                    LoadButton(newCharProperty, "BtCheck", btClickSound, btOverSound, device))
            };

            foreach (LoginCreateCharacterWindowBase window in windows)
            {
                window.Position = windowPosition;
                manager.RegisterCustomWindow(window);
            }

        }



        private static void RegisterAvatarPreviewCarouselWindow(
            UIWindowManager manager,
            WzSubProperty charSelectProperty,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.AvatarPreviewCarousel) != null)
            {
                return;
            }


            AvatarPreviewCarouselWindow.PreviewCanvasFrame cardNormalFrame = LoadPreviewCanvasFrame(charSelectProperty?["charInfo"] as WzCanvasProperty, device);
            if (cardNormalFrame.Texture == null)
            {
                cardNormalFrame = new AvatarPreviewCarouselWindow.PreviewCanvasFrame(
                    CreateFilledTexture(device, 183, 115, new Color(245, 231, 206, 255), new Color(66, 44, 32, 255)),
                    Point.Zero);
            }

            AvatarPreviewCarouselWindow.PreviewCanvasFrame cardSelectedFrame = LoadPreviewCanvasFrame(charSelectProperty?["charInfo1"] as WzCanvasProperty, device);
            if (cardSelectedFrame.Texture == null)
            {
                cardSelectedFrame = cardNormalFrame;
            }
            Texture2D frameTexture = CreateFilledTexture(device, 618, 238, Color.Transparent, Color.Transparent);
            WzSubProperty nameTagProperty = charSelectProperty?["nameTag"] as WzSubProperty;
            var normalNameTagStyle = LoadPreviewNameTagStyle(
                nameTagProperty?["0"] as WzSubProperty,
                device,
                new Color(153, 153, 153));
            var selectedNameTagStyle = LoadPreviewNameTagStyle(
                nameTagProperty?["1"] as WzSubProperty,
                device,
                Color.White);
            Dictionary<AvatarPreviewCarouselWindow.LoginJobDecorationStyle, AvatarPreviewCarouselWindow.PreviewCanvasFrame> jobDecorations = new()
            {
                [AvatarPreviewCarouselWindow.LoginJobDecorationStyle.Adventure] = LoadPreviewCanvasFrame(charSelectProperty?["adventure"]?["0"] as WzCanvasProperty, device),
                [AvatarPreviewCarouselWindow.LoginJobDecorationStyle.Knight] = LoadPreviewCanvasFrame(charSelectProperty?["knight"]?["0"] as WzCanvasProperty, device),
                [AvatarPreviewCarouselWindow.LoginJobDecorationStyle.Aran] = LoadPreviewCanvasFrame(charSelectProperty?["aran"]?["0"] as WzCanvasProperty, device),
                [AvatarPreviewCarouselWindow.LoginJobDecorationStyle.Evan] = LoadPreviewCanvasFrame(charSelectProperty?["evan"]?["0"] as WzCanvasProperty, device),
                [AvatarPreviewCarouselWindow.LoginJobDecorationStyle.Resistance] = LoadPreviewCanvasFrame(charSelectProperty?["resistance"]?["0"] as WzCanvasProperty, device)
            };
            AvatarPreviewCarouselWindow.PreviewCanvasFrame emptySlotFrame = LoadPreviewCanvasFrame(
                charSelectProperty?["character"]?["1"]?["0"] as WzCanvasProperty,
                device);
            List<AvatarPreviewCarouselWindow.PreviewCanvasFrame> buyCharacterFrames = new();
            WzSubProperty buyCharacterProperty = charSelectProperty?["buyCharacter"] as WzSubProperty;
            if (buyCharacterProperty != null)
            {
                for (int frameIndex = 0; frameIndex < 12; frameIndex++)
                {
                    if (buyCharacterProperty[frameIndex.ToString()] is WzCanvasProperty buyCanvas)
                    {
                        AvatarPreviewCarouselWindow.PreviewCanvasFrame frame = LoadPreviewCanvasFrame(buyCanvas, device);
                        if (frame.Texture != null)
                        {
                            buyCharacterFrames.Add(frame);
                        }
                    }
                }
            }


            List<UIObject> cardButtons = new List<UIObject>();
            int cardHitWidth = Math.Max(
                183,
                Math.Max(cardNormalFrame.Texture?.Width ?? 0, cardSelectedFrame.Texture?.Width ?? 0));
            int cardHitHeight = Math.Max(
                151,
                Math.Max(cardNormalFrame.Texture?.Height ?? 0, cardSelectedFrame.Texture?.Height ?? 0));
            Texture2D hitTexture = CreateFilledTexture(device, cardHitWidth, cardHitHeight, Color.Transparent, Color.Transparent);
            for (int slot = 0; slot < 3; slot++)
            {
                UIObject cardButton = CreateTextureButton(hitTexture, hitTexture);
                if (cardButton == null)
                {
                    continue;
                }


                cardButton.X = 18 + (slot * 197);
                cardButton.Y = 46;
                cardButtons.Add(cardButton);
            }


            UIObject prevPageButton = CreateCanvasButton(
                charSelectProperty?["pageL"]?["0"]?["0"] as WzCanvasProperty,
                charSelectProperty?["pageL"]?["1"]?["0"] as WzCanvasProperty,
                device);
            UIObject nextPageButton = CreateCanvasButton(
                charSelectProperty?["pageR"]?["0"]?["0"] as WzCanvasProperty,
                charSelectProperty?["pageR"]?["1"]?["0"] as WzCanvasProperty,
                device);


            if (prevPageButton != null)
            {
                prevPageButton.X = -2;
                prevPageButton.Y = 82;
            }


            if (nextPageButton != null)
            {
                nextPageButton.X = 531;
                nextPageButton.Y = 82;
            }


            AvatarPreviewCarouselWindow previewWindow = new AvatarPreviewCarouselWindow(
                new DXObject(0, 0, frameTexture, 0),
                cardNormalFrame,
                cardSelectedFrame,
                normalNameTagStyle,
                selectedNameTagStyle,
                jobDecorations,
                emptySlotFrame,
                buyCharacterFrames,
                cardButtons,
                prevPageButton,
                nextPageButton)
            {
                Position = new Point(Math.Max(24, (screenWidth / 2) - 309), Math.Max(24, (screenHeight / 2) - 160))
            };


            manager.RegisterCustomWindow(previewWindow);

        }



        private static AvatarPreviewCarouselWindow.PreviewNameTagStyle LoadPreviewNameTagStyle(
            WzSubProperty sourceProperty,
            GraphicsDevice device,
            Color textColor)
        {
            return new AvatarPreviewCarouselWindow.PreviewNameTagStyle(
                LoadCanvasTexture(sourceProperty, "0", device),
                LoadCanvasTexture(sourceProperty, "1", device),
                LoadCanvasTexture(sourceProperty, "2", device),
                textColor);
        }

        private static CharacterSelectWindow.BalloonStyle LoadLoginProcessBalloonStyle(
            WzSubProperty balloonProperty,
            GraphicsDevice device)
        {
            if (balloonProperty == null)
            {
                return default;
            }

            int textColorArgb = InfoTool.GetInt(balloonProperty["clr"], unchecked((int)0xFF000000));
            return new CharacterSelectWindow.BalloonStyle(
                LoadCanvasTexture(balloonProperty, "nw", device),
                LoadCanvasTexture(balloonProperty, "n", device),
                LoadCanvasTexture(balloonProperty, "ne", device),
                LoadCanvasTexture(balloonProperty, "w", device),
                LoadCanvasTexture(balloonProperty, "c", device),
                LoadCanvasTexture(balloonProperty, "e", device),
                LoadCanvasTexture(balloonProperty, "sw", device),
                LoadCanvasTexture(balloonProperty, "s", device),
                LoadCanvasTexture(balloonProperty, "se", device),
                LoadOwnerCanvasFrame(
                    (balloonProperty?["selArrow"] as WzCanvasProperty)
                    ?? (balloonProperty?["arrow"] as WzCanvasProperty),
                    device),
                new Color(unchecked((uint)textColorArgb)));
        }

        private static CharacterSelectWindow.OwnerCanvasFrame LoadOwnerCanvasFrame(
            WzCanvasProperty canvas,
            GraphicsDevice device)
        {
            if (canvas == null || device == null)
            {
                return default;
            }

            try
            {
                Texture2D texture = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (texture == null)
                {
                    return default;
                }

                Point origin = canvas.GetCanvasOriginPosition() is System.Drawing.PointF canvasOrigin
                    ? new Point((int)canvasOrigin.X, (int)canvasOrigin.Y)
                    : Point.Zero;
                return new CharacterSelectWindow.OwnerCanvasFrame(texture, origin);
            }
            catch
            {
                return default;
            }
        }

        private static WzCanvasProperty ResolveNewestIndexedCanvas(WzSubProperty property)
        {
            if (property == null)
            {
                return null;
            }

            WzSubProperty newestEntry = property.WzProperties
                .OfType<WzSubProperty>()
                .Where(static child => int.TryParse(child.Name, out _))
                .OrderBy(child => int.Parse(child.Name, CultureInfo.InvariantCulture))
                .LastOrDefault();
            return newestEntry?["0"] as WzCanvasProperty;
        }

        private static IReadOnlyList<IReadOnlyList<CharacterSelectWindow.AnimationFrame>> LoadCharacterSelectAnimationFrameSets(
            WzSubProperty sourceProperty,
            GraphicsDevice device)
        {
            List<IReadOnlyList<CharacterSelectWindow.AnimationFrame>> frameSets = new();
            if (sourceProperty == null || device == null)
            {
                return frameSets;
            }

            foreach (WzSubProperty indexedChild in sourceProperty.WzProperties
                         .OfType<WzSubProperty>()
                         .Where(static child => int.TryParse(child.Name, out _))
                         .OrderBy(child => int.Parse(child.Name, CultureInfo.InvariantCulture)))
            {
                frameSets.Add(LoadCharacterSelectAnimationFrames(indexedChild, device));
            }

            if (frameSets.Count == 0)
            {
                frameSets.Add(LoadCharacterSelectAnimationFrames(sourceProperty, device));
            }

            return frameSets;
        }


        private static List<CharacterSelectWindow.AnimationFrame> LoadCharacterSelectAnimationFrames(
            WzSubProperty sourceProperty,
            GraphicsDevice device)
        {
            List<CharacterSelectWindow.AnimationFrame> frames = new();
            if (sourceProperty == null || device == null)
            {
                return frames;
            }


            foreach (WzImageProperty child in sourceProperty.WzProperties)
            {
                if (child is not WzCanvasProperty canvas)
                {
                    continue;
                }


                try
                {
                    Texture2D texture = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                    if (texture == null)
                    {
                        continue;
                    }


                    Point offset = ResolveCanvasOffset(canvas, Point.Zero);
                    int delay = InfoTool.GetInt(canvas["delay"], 100);
                    frames.Add(new CharacterSelectWindow.AnimationFrame(texture, offset, Math.Max(1, delay)));
                }
                catch
                {
                }
            }


            return frames;

        }



        private static AvatarPreviewCarouselWindow.PreviewCanvasFrame LoadPreviewCanvasFrame(WzCanvasProperty canvas, GraphicsDevice device)
        {
            if (canvas == null || device == null)
            {
                return default;
            }


            try
            {
                Texture2D texture = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                Point origin = canvas.GetCanvasOriginPosition() is System.Drawing.PointF canvasOrigin
                    ? new Point((int)canvasOrigin.X, (int)canvasOrigin.Y)
                    : Point.Zero;
                int delay = 0;
                if (canvas["delay"] is WzIntProperty delayProperty)
                {
                    delay = delayProperty.Value;
                }


                return new AvatarPreviewCarouselWindow.PreviewCanvasFrame(texture, origin, delay);
            }
            catch
            {
                return default;
            }
        }


        public static void RegisterConnectionNoticeWindow(
            UIWindowManager manager,
            WzImage loginImage,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.ConnectionNotice) != null)
            {
                return;
            }


            WzSubProperty noticeProperty = loginImage?["Notice"] as WzSubProperty;
            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            IDXObject noticeFrame = LoadWindowCanvasLayer(noticeProperty?["backgrnd"] as WzSubProperty, "0", device);
            IDXObject noticeCogFrame = LoadWindowCanvasLayer(noticeProperty?["backgrnd"] as WzSubProperty, "1", device);
            IDXObject noticeBarFrame = LoadWindowCanvasLayer(noticeProperty?["backgrnd"] as WzSubProperty, "2", device);
            IDXObject loadingFrame = LoadWindowCanvasLayer(noticeProperty?["Loading"] as WzSubProperty, "backgrnd", device);
            IDXObject loadingSingleGaugeFrame = LoadWindowCanvasLayer(noticeProperty?["LoadingSG"] as WzSubProperty, "backgrnd", device);


            if (noticeFrame == null)
            {
                Texture2D noticeTexture = CreatePlaceholderWindowTexture(device, 249, 142, "Connection Notice");
                noticeFrame = new DXObject(0, 0, noticeTexture, 0);
            }


            noticeCogFrame ??= noticeFrame;
            noticeBarFrame ??= noticeFrame;
            if (loadingFrame == null)
            {
                loadingFrame = noticeFrame;
            }


            loadingSingleGaugeFrame ??= noticeBarFrame ?? noticeFrame;



            List<Texture2D> progressFrames = new List<Texture2D>();
            WzSubProperty progressBarProperty = noticeProperty?["Loading"]?["bar"] as WzSubProperty;
            for (int i = 0; i <= 10; i++)
            {
                Texture2D frame = LoadCanvasTexture(progressBarProperty, i.ToString(), device);
                if (frame != null)
                {
                    progressFrames.Add(frame);
                }
            }


            if (progressFrames.Count == 0)
            {
                progressFrames = CreateFallbackProgressFrames(device, 109, 8, 11);
            }


            List<Texture2D> singleGaugeProgressFrames = new List<Texture2D>();
            WzSubProperty singleGaugeBarProperty = noticeProperty?["LoadingSG"]?["bar"] as WzSubProperty;
            for (int i = 0; i <= 9; i++)
            {
                Texture2D frame = LoadCanvasTexture(singleGaugeBarProperty, i.ToString(), device);
                if (frame != null)
                {
                    singleGaugeProgressFrames.Add(frame);
                }
            }


            if (singleGaugeProgressFrames.Count == 0)
            {
                singleGaugeProgressFrames = CreateFallbackProgressFrames(device, 137, 11, 10);
            }
            List<Texture2D> loadingCircleFrames = new List<Texture2D>();
            WzSubProperty loadingCircleProperty = noticeProperty?["Loading"]?["circle"] as WzSubProperty;
            for (int i = 0; i <= 15; i++)
            {
                Texture2D frame = LoadCanvasTexture(loadingCircleProperty, i.ToString(), device);
                if (frame != null)
                {
                    loadingCircleFrames.Add(frame);
                }
            }


            Dictionary<int, Texture2D> noticeTextTextures = LoadIndexedCanvasTextures(

                noticeProperty?["text"] as WzSubProperty,

                device);
            UIObject cancelButton = LoadButton(noticeProperty?["Loading"] as WzSubProperty, "BtCancel", btClickSound, btOverSound, device)
                                    ?? LoadButton(noticeProperty?["LoadingSG"] as WzSubProperty, "BtCancel", btClickSound, btOverSound, device);
            Dictionary<ConnectionNoticeWindowVariant, IDXObject> framesByVariant = new()
            {
                [ConnectionNoticeWindowVariant.Notice] = noticeFrame,
                [ConnectionNoticeWindowVariant.NoticeCog] = noticeCogFrame,
                [ConnectionNoticeWindowVariant.Loading] = loadingFrame,
                [ConnectionNoticeWindowVariant.LoadingSingleGauge] = loadingSingleGaugeFrame,
            };
            Dictionary<ConnectionNoticeWindowVariant, IReadOnlyList<Texture2D>> progressFramesByVariant = new()
            {
                [ConnectionNoticeWindowVariant.Loading] = progressFrames,
                [ConnectionNoticeWindowVariant.LoadingSingleGauge] = singleGaugeProgressFrames,
            };
            Dictionary<ConnectionNoticeWindowVariant, IReadOnlyList<Texture2D>> animationFramesByVariant = new()
            {
                [ConnectionNoticeWindowVariant.Loading] = loadingCircleFrames,
            };


            ConnectionNoticeWindow window = new ConnectionNoticeWindow(
                framesByVariant,
                progressFramesByVariant,
                animationFramesByVariant,
                noticeTextTextures,
                cancelButton,
                null,
                null,
                null,
                screenWidth,
                screenHeight)
            {
                Position = new Point(
                    Math.Max(24, (screenWidth / 2) - ((noticeFrame.Width > 0 ? noticeFrame.Width : 249) / 2)),
                    Math.Max(24, (screenHeight / 2) - ((noticeFrame.Height > 0 ? noticeFrame.Height : 142) / 2)))
            };


            manager.RegisterCustomWindow(window);

        }



        public static void RegisterLoginUtilityDialogWindow(
            UIWindowManager manager,
            WzImage uiWindow2Image,
            WzImage loginImage,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.LoginUtilityDialog) != null)
            {
                return;
            }


            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            WzSubProperty utilDlgProperty = uiWindow2Image?["UtilDlgEx"] as WzSubProperty;
            WzSubProperty loginNoticeProperty = loginImage?["Notice"] as WzSubProperty;
            Dictionary<int, Texture2D> noticeTextTextures = LoadIndexedCanvasTextures(
                loginNoticeProperty?["text"] as WzSubProperty,
                device);

            Texture2D frameTexture = LoadCanvasTexture(utilDlgProperty, "notice", device)
                                     ?? LoadCanvasTexture(loginNoticeProperty?["backgrnd"] as WzSubProperty, "0", device)
                                     ?? CreatePlaceholderWindowTexture(device, 312, 132, "Login Utility");
            Texture2D loginNoticeTexture = LoadCanvasTexture(loginNoticeProperty?["backgrnd"] as WzSubProperty, "0", device)
                                           ?? frameTexture;
            Texture2D loginNoticeCogTexture = LoadCanvasTexture(loginNoticeProperty?["backgrnd"] as WzSubProperty, "1", device)
                                              ?? loginNoticeTexture;
            Texture2D loginNoticeBarTexture = LoadCanvasTexture(loginNoticeProperty?["backgrnd"] as WzSubProperty, "2", device)
                                              ?? loginNoticeTexture;
            WzImage uiWindowImage = Program.FindImage("UI", "UIWindow.img");
            WzSubProperty fadeYesNoProperty = uiWindow2Image?["FadeYesNo"] as WzSubProperty
                                              ?? uiWindowImage?["FadeYesNo"] as WzSubProperty;
            Texture2D fadeYesNoTexture = LoadCanvasTexture(fadeYesNoProperty, "backgrnd7", device)
                                         ?? LoadCanvasTexture(fadeYesNoProperty, "backgrnd", device)
                                         ?? frameTexture;



            UIObject okButton = LoadButton(utilDlgProperty, "BtOK", btClickSound, btOverSound, device);
            UIObject yesButton = LoadButton(utilDlgProperty, "BtYes", btClickSound, btOverSound, device)
                                 ?? LoadButton(loginNoticeProperty, "BtYes", btClickSound, btOverSound, device);
            UIObject noButton = LoadButton(utilDlgProperty, "BtNo", btClickSound, btOverSound, device)
                                ?? LoadButton(loginNoticeProperty, "BtNo", btClickSound, btOverSound, device);
            UIObject questionYesButton = LoadButton(loginNoticeProperty, "BtYes1", btClickSound, btOverSound, device)
                                         ?? yesButton;
            UIObject questionNoButton = LoadButton(loginNoticeProperty, "BtNo1", btClickSound, btOverSound, device)
                                        ?? noButton;
            UIObject acceptButton = LoadButton(loginNoticeProperty, "BtAccept", btClickSound, btOverSound, device)
                                    ?? okButton;
            UIObject nowButton = LoadButton(loginNoticeProperty, "BtNow", btClickSound, btOverSound, device);
            UIObject laterButton = LoadButton(loginNoticeProperty, "BtLater", btClickSound, btOverSound, device);
            UIObject restartButton = LoadButton(loginNoticeProperty, "BtRestart", btClickSound, btOverSound, device);
            UIObject exitButton = LoadButton(loginNoticeProperty, "BtExit", btClickSound, btOverSound, device);
            UIObject nexonButton = LoadButton(loginNoticeProperty, "BtNexon", btClickSound, btOverSound, device);


            Dictionary<LoginUtilityDialogFrameVariant, IDXObject> framesByVariant = new()
            {
                [LoginUtilityDialogFrameVariant.Default] = new DXObject(0, 0, frameTexture, 0),
                [LoginUtilityDialogFrameVariant.LoginNotice] = new DXObject(0, 0, loginNoticeTexture, 0),
                [LoginUtilityDialogFrameVariant.LoginNoticeCog] = new DXObject(0, 0, loginNoticeCogTexture, 0),
                [LoginUtilityDialogFrameVariant.LoginNoticeBar] = new DXObject(0, 0, loginNoticeBarTexture, 0),
                [LoginUtilityDialogFrameVariant.InGameFadeYesNo] = new DXObject(0, 0, fadeYesNoTexture, 0),
                [LoginUtilityDialogFrameVariant.UtilDlgNotice] = new DXObject(0, 0, frameTexture, 0),
            };

            LoginUtilityDialogWindow window = new LoginUtilityDialogWindow(
                framesByVariant,
                okButton,
                yesButton,
                noButton,
                questionYesButton,
                questionNoButton,
                acceptButton,
                nowButton,
                laterButton,
                restartButton,
                exitButton,
                nexonButton,
                noticeTextTextures,
                screenWidth,
                screenHeight)
            {
                Position = new Point(Math.Max(24, (screenWidth / 2) - (frameTexture.Width / 2)), Math.Max(24, (screenHeight / 2) - (frameTexture.Height / 2)))

            };



            manager.RegisterCustomWindow(window);

        }

        public static void RegisterInGameConfirmDialogWindow(
            UIWindowManager manager,
            WzImage uiWindow2Image,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.InGameConfirmDialog) != null)
            {
                return;
            }

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            WzImage uiWindowImage = Program.FindImage("UI", "UIWindow.img");
            WzSubProperty fadeYesNoProperty = uiWindow2Image?["FadeYesNo"] as WzSubProperty
                                              ?? uiWindowImage?["FadeYesNo"] as WzSubProperty;
            WzSubProperty uiWindowFadeYesNoProperty = uiWindowImage?["FadeYesNo"] as WzSubProperty
                                                     ?? fadeYesNoProperty;
            Texture2D frameTexture = LoadCanvasTexture(fadeYesNoProperty, "backgrnd7", device)
                                     ?? LoadCanvasTexture(fadeYesNoProperty, "backgrnd", device)
                                     ?? CreatePlaceholderWindowTexture(device, 206, 70, "Confirm");
            Texture2D defaultIcon = LoadCanvasTexture(fadeYesNoProperty, "icon0", device);
            Texture2D messengerInviteFrameTexture = LoadCanvasTexture(uiWindowFadeYesNoProperty, "backgrnd7", device)
                                                   ?? frameTexture;
            Texture2D messengerInviteIcon = LoadCanvasTexture(uiWindowFadeYesNoProperty, "icon0", device)
                                            ?? defaultIcon;
            UIObject confirmButton = LoadButton(fadeYesNoProperty, "BtOK", btClickSound, btOverSound, device);
            UIObject cancelButton = LoadButton(fadeYesNoProperty, "BtCancel", btClickSound, btOverSound, device);

            InGameConfirmDialogWindow window = new InGameConfirmDialogWindow(
                new DXObject(0, 0, frameTexture, 0),
                MapSimulatorWindowNames.InGameConfirmDialog,
                confirmButton,
                cancelButton,
                defaultIcon,
                new DXObject(0, 0, messengerInviteFrameTexture, 0),
                messengerInviteIcon,
                defaultIcon,
                screenWidth,
                screenHeight);
            manager.RegisterCustomWindow(window);
        }



        private static Dictionary<int, Texture2D> LoadIndexedCanvasTextures(WzSubProperty property, GraphicsDevice device)
        {
            Dictionary<int, Texture2D> textures = new();
            if (property == null)
            {
                return textures;
            }


            foreach (WzImageProperty child in property.WzProperties)
            {
                if (!int.TryParse(child.Name, out int index))
                {
                    continue;
                }


                Texture2D texture = LoadCanvasTexture(property, child.Name, device);
                if (texture != null)
                {
                    textures[index] = texture;
                }
            }


            return textures;
        }
        private static IReadOnlyList<Texture2D> LoadIndexedCanvasTextureList(WzSubProperty property, GraphicsDevice device)
        {
            if (property == null)
            {
                return Array.Empty<Texture2D>();
            }


            return property.WzProperties
                .Where(child => int.TryParse(child.Name, out _))
                .OrderBy(child => int.Parse(child.Name, CultureInfo.InvariantCulture))
                .Select(child => LoadCanvasTexture(property, child.Name, device))
                .ToArray();
        }
        private static IReadOnlyList<Texture2D> LoadIndexedCanvasTextureList(WzSubProperty property, string childName, GraphicsDevice device)
        {
            if (property == null)
            {
                return Array.Empty<Texture2D>();
            }


            return property.WzProperties
                .Where(child => int.TryParse(child.Name, out _))
                .OrderBy(child => int.Parse(child.Name, CultureInfo.InvariantCulture))
                .Select(child => child[childName] as WzCanvasProperty)
                .Select(canvas => canvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device))
                .ToArray();
        }


        public static void RegisterLoginEntryWindows(
            UIWindowManager manager,
            WzImage loginImage,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            if (manager == null)
            {
                return;
            }


            RegisterLoginTitleWindow(manager, loginImage, soundUIImage, device, screenWidth, screenHeight);
            RegisterChannelSelectionWindows(manager, loginImage, uiWindow1Image, uiWindow2Image, soundUIImage, device, screenWidth, screenHeight);
            RegisterLoginRecommendWorldWindow(manager, loginImage, soundUIImage, device, screenWidth, screenHeight);
            RegisterLoginCharacterSelectWindow(manager, basicImage, soundUIImage, device, screenWidth, screenHeight);
            RegisterConnectionNoticeWindow(manager, loginImage, basicImage, soundUIImage, device, screenWidth, screenHeight);
            RegisterLoginUtilityDialogWindow(manager, uiWindow2Image, loginImage, basicImage, soundUIImage, device, screenWidth, screenHeight);
        }


        public static void RegisterLoginCharacterDetailWindow(
            UIWindowManager manager,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.CharacterDetail) != null)
            {
                return;
            }


            WzImage loginImage = global::HaCreator.Program.FindImage("UI", "Login.img");

            WzSubProperty charSelectProperty = loginImage?["CharSelect"] as WzSubProperty;



            Texture2D panelTexture = LoadCanvasTexture(charSelectProperty, "charInfo", device)
                ?? CreatePlaceholderWindowTexture(device, 183, 115, "Character Detail");
            Texture2D panelTextureWithRank = LoadCanvasTexture(charSelectProperty, "charInfo1", device)
                ?? panelTexture;
            Texture2D frameTexture = CreateFilledTexture(device, panelTextureWithRank.Width, panelTextureWithRank.Height, Color.Transparent, Color.Transparent);
            WzSubProperty iconProperty = charSelectProperty?["icon"] as WzSubProperty;
            Dictionary<int, Texture2D> jobBadgeTextures = new Dictionary<int, Texture2D>();
            WzSubProperty jobBadgeProperty = iconProperty?["job"] as WzSubProperty;
            for (int index = 0; index <= 4; index++)
            {
                Texture2D badgeTexture = LoadCanvasTexture(jobBadgeProperty, index.ToString(), device);
                if (badgeTexture != null)
                {
                    jobBadgeTextures[index] = badgeTexture;
                }
            }


            CharacterDetailWindow window = new CharacterDetailWindow(
                new DXObject(0, 0, frameTexture, 0),
                panelTexture,
                panelTextureWithRank,
                LoadCanvasTexture(iconProperty, "up", device),
                LoadCanvasTexture(iconProperty, "down", device),
                LoadCanvasTexture(iconProperty, "same", device),
                jobBadgeTextures)
            {
                Position = new Point(Math.Max(24, (screenWidth / 2) + 194), Math.Max(24, (screenHeight / 2) - 118))
            };


            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            WzSubProperty closeButtonProperty = basicImage?["BtClose"] as WzSubProperty;
            if (closeButtonProperty != null)
            {
                try
                {
                    UIObject closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = Math.Max(0, frameTexture.Width - closeBtn.CanvasSnapshotWidth - 6);
                    closeBtn.Y = 4;
                    window.InitializeCloseButton(closeBtn);
                }
                catch
                {
                }
            }


            manager.RegisterCustomWindow(window);

        }

        #endregion



        #region UIWindowManager Factory
        /// <summary>
        /// Create and initialize a UIWindowManager with all windows
        /// </summary>
        public static UIWindowManager CreateUIWindowManager(
            WzImage uiWindow1Image, WzImage uiWindow2Image, WzImage basicImage, WzImage soundUIImage,
            GraphicsDevice device, int screenWidth, int screenHeight, bool isBigBang, string storageAccountLabel = null, string storageAccountKey = null)
        {

            return CreateUIWindowManager(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage,

                null, null, null, device, screenWidth, screenHeight, isBigBang, 900, storageAccountLabel, storageAccountKey); // Default to GM book (900 in data)
        }



        /// <summary>
        /// Create and initialize a UIWindowManager with all windows and skill loading support
        /// </summary>
        public static UIWindowManager CreateUIWindowManager(
            WzImage uiWindow1Image, WzImage uiWindow2Image, WzImage basicImage, WzImage soundUIImage,
            WzFile skillWzFile, WzFile stringWzFile, WzImage mapleTvImage,
            GraphicsDevice device, int screenWidth, int screenHeight, bool isBigBang, int jobId = 900, string storageAccountLabel = null, string storageAccountKey = null)
        {

            UIWindowManager manager = new UIWindowManager();



            // Create windows - use unified methods that select pre-BB or post-BB based on flag
            UIWindowBase inventory = CreateInventoryWindowUnified(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, isBigBang);
            UIWindowBase equip = CreateEquipWindowUnified(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, isBigBang);
            UIWindowBase skill = CreateSkillWindowUnified(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, isBigBang);
            UIWindowBase quest = CreateQuestWindowUnified(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, isBigBang);
            QuestDetailWindow questDetail = CreateQuestDetailWindowUnified(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, isBigBang);
            UIWindowBase ability = CreateAbilityWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, isBigBang);
            UserInfoUI characterInfo = CreateCharacterInfoWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, isBigBang);
            QuickSlotUI quickSlot = CreateQuickSlotWindow(uiWindow2Image, device, screenWidth, screenHeight);


            // Seed the skill window with the requested job path only.
            if (skill is SkillUIBigBang skillBigBang)
            {
                System.Diagnostics.Debug.WriteLine($"[UIWindowLoader] Loading job skills into SkillUIBigBang for job {jobId}");
                LoadSkillsForJob(skillBigBang, jobId, device);
            }
            else if (skill is SkillUI legacySkill)
            {
                System.Diagnostics.Debug.WriteLine($"[UIWindowLoader] Loading job skills into SkillUI for job {jobId}");
                LoadSkillsForJob(legacySkill, jobId, device);
            }


            // Create skill macro window (post-BB only)
            SkillMacroUI skillMacro = null;
            AranSkillGuideUI aranSkillGuide = null;
            if (isBigBang)
            {
                skillMacro = CreateSkillMacroWindowBigBang(uiWindow1Image, uiWindow2Image, soundUIImage, device, screenWidth, screenHeight);
                aranSkillGuide = CreateAranSkillGuideWindowBigBang(uiWindow2Image, soundUIImage, device, screenWidth, screenHeight);
            }


            // Register with manager
            manager.RegisterInventoryWindow(inventory);
            manager.RegisterEquipWindow(equip);
            manager.RegisterSkillWindow(skill);
            manager.RegisterQuestWindow(quest);
            manager.RegisterQuestDetailWindow(questDetail);
            manager.RegisterAbilityWindow(ability);
            manager.RegisterQuickSlotWindow(quickSlot);
            if (characterInfo != null)
            {
                manager.RegisterCustomWindow(characterInfo);
            }


            if (skillMacro != null)

            {

                manager.RegisterSkillMacroWindow(skillMacro);



                // Wire up the MACRO button in skill window to open the macro window
                if (skill is SkillUIBigBang skillBB && skillBB.MacroButton != null)
                {
                    var macroWindow = skillMacro;
                    skillBB.MacroButton.ButtonClickReleased += (sender) =>
                    {
                        if (macroWindow != null)
                        {
                            macroWindow.Show();
                            manager.BringToFront(macroWindow);
                        }
                    };
                }
            }


            if (aranSkillGuide != null)

            {

                manager.RegisterCustomWindow(aranSkillGuide);



                if (skill is SkillUIBigBang skillBB)
                {
                    skillBB.OnSkillGuideRequested = grade =>
                    {
                        aranSkillGuide.SetPage(grade);
                        manager.ShowWindow(aranSkillGuide);
                    };
                }
            }


            SeedStarterCraftingInventory(manager.InventoryWindow as IInventoryRuntime);

            SeedStarterConsumableInventory(manager.InventoryWindow as IInventoryRuntime);

            SimulatorStorageRuntime storageRuntime = new SimulatorStorageRuntime(initialAccountLabel: storageAccountLabel, initialAccountKey: storageAccountKey);


            manager.RegisterLazyWindow(
                MapSimulatorWindowNames.MapTransfer,
                m =>
                {
                    MapTransferUI mapTransfer = CreateMapTransferWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight);
                    if (mapTransfer != null)
                    {
                        m.RegisterCustomWindow(mapTransfer);
                    }
                });

            manager.RegisterLazyWindow(
                MapSimulatorWindowNames.Trunk,
                m =>
                {
                    TrunkUI trunk = CreateTrunkWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, m.InventoryWindow as InventoryUI, storageRuntime);
                    if (trunk != null)
                    {
                        m.RegisterCustomWindow(trunk);
                        if (storageRuntime.GetUsedSlotCount() == 0 &&
                            storageRuntime.GetMesoCount() <= 0 &&
                            storageRuntime.GetSlotLimit() == 24)
                        {
                            SeedStarterTrunkInventory(storageRuntime);
                        }
                    }
                });

            manager.RegisterLazyWindow(
                MapSimulatorWindowNames.WorldMap,
                m =>
                {
                    WorldMapUI worldMap = CreateWorldMapWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight);
                    if (worldMap != null)
                    {
                        m.RegisterCustomWindow(worldMap);
                    }
                });
            SoftKeyboardUI softKeyboard = CreateSoftKeyboardWindow(uiWindow1Image, device, screenWidth, screenHeight);
            if (softKeyboard != null)
            {
                manager.RegisterSoftKeyboardWindow(softKeyboard);
            }



            RegisterProgressionUtilityPlaceholderWindows(manager, uiWindow1Image, uiWindow2Image, mapleTvImage, basicImage, soundUIImage, device, screenWidth, screenHeight, storageRuntime);

            RegisterSocialRoomWindows(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight);



            if (characterInfo != null)

            {

                characterInfo.PartyRequested = _ =>
                {
                    manager.ShowWindow(MapSimulatorWindowNames.SocialList);
                    return "Party list opened from the profile window.";
                };
                characterInfo.MiniRoomRequested = () => manager.ShowWindow(MapSimulatorWindowNames.MiniRoom);
                characterInfo.PersonalShopRequested = () => manager.ShowWindow(MapSimulatorWindowNames.PersonalShop);
                characterInfo.EntrustedShopRequested = () => manager.ShowWindow(MapSimulatorWindowNames.EntrustedShop);
                characterInfo.TradingRoomRequested = _ =>
                {
                    manager.ShowWindow(MapSimulatorWindowNames.TradingRoom);
                    return "Trading-room shell opened.";
                };

                characterInfo.FamilyRequested = _ =>
                {
                    manager.ShowWindow(MapSimulatorWindowNames.FamilyChart);
                    return "Family chart opened from the profile window.";
                };

                characterInfo.BookCollectionRequested = _ =>
                {
                    manager.ShowWindow(MapSimulatorWindowNames.BookCollection);
                    return "Collection book opened.";
                };

            }



            return manager;

        }



        private static void RegisterProgressionUtilityPlaceholderWindows(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage mapleTvImage,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight,
            IStorageRuntime storageRuntime)
        {
            if (manager == null)
            {
                return;
            }


            manager.RegisterLazyWindow(
                MapSimulatorWindowNames.WorldSelect,
                m => RegisterChannelSelectionWindows(m, null, uiWindow1Image, uiWindow2Image, soundUIImage, device, screenWidth, screenHeight));
            manager.RegisterLazyWindow(
                MapSimulatorWindowNames.RecommendWorld,
                m => RegisterChannelSelectionWindows(m, null, uiWindow1Image, uiWindow2Image, soundUIImage, device, screenWidth, screenHeight));
            manager.RegisterLazyWindow(
                MapSimulatorWindowNames.ChannelSelect,
                m => RegisterChannelSelectionWindows(m, null, uiWindow1Image, uiWindow2Image, soundUIImage, device, screenWidth, screenHeight));
            manager.RegisterLazyWindow(
                MapSimulatorWindowNames.ChannelShift,
                m => RegisterChannelSelectionWindows(m, null, uiWindow1Image, uiWindow2Image, soundUIImage, device, screenWidth, screenHeight));



            int x = Math.Max(40, (screenWidth / 2) - 160);

            int y = Math.Max(40, (screenHeight / 2) - 120);

            const int cascade = 24;



            manager.RegisterLazyWindow(MapSimulatorWindowNames.AdminShopWishList, m => RegisterAdminShopWishListWindow(m, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 3), y + cascade)));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.CashAvatarPreview, m => RegisterCashAvatarPreviewWindow(m, basicImage, soundUIImage, device, new Point(x + (cascade * 8), y + cascade)));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.CashShop, m => RegisterAdminShopWindow(m, uiWindow2Image, basicImage, soundUIImage, device, MapSimulatorWindowNames.CashShop, AdminShopServiceMode.CashShop, new Point(x + cascade, y + cascade), storageRuntime));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.CashShopStage, m => RegisterCashServiceStageWindow(m, device, MapSimulatorWindowNames.CashShopStage, CashServiceWindowStageKind.CashShop, new Point(x + cascade, y + cascade)));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.Mts, m => RegisterAdminShopWindow(m, uiWindow2Image, basicImage, soundUIImage, device, MapSimulatorWindowNames.Mts, AdminShopServiceMode.Mts, new Point(x + (cascade * 2), y + (cascade * 2)), storageRuntime));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.MtsStatus, m => RegisterCashServiceStageWindow(m, device, MapSimulatorWindowNames.MtsStatus, CashServiceWindowStageKind.ItemTradingCenter, new Point(x + (cascade * 2), y + (cascade * 2))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.SocialList, m => RegisterSocialListWindow(m, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 2), y + (cascade * 5))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.FamilyChart, m => RegisterFamilyChartWindow(m, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 3), y + (cascade * 5))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.Messenger, m => RegisterMessengerWindow(m, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 3), y + (cascade * 3))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.EngagementProposal, m => RegisterEngagementProposalWindow(m, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 4), y + (cascade * 3))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.WeddingInvitation, m => RegisterWeddingInvitationWindow(m, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 5), y + (cascade * 3))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.WeddingWishList, m => RegisterWeddingWishListWindow(m, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 6), y + (cascade * 3))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.MapleTv, m => RegisterMapleTvWindow(m, uiWindow1Image, mapleTvImage, basicImage, soundUIImage, device, new Point(x + (cascade * 4), y + (cascade * 2))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.ItemMaker, m => RegisterItemMakerWindow(m, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 5), y + (cascade * 5))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.BookCollection, m => RegisterBookCollectionWindow(m, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 6), y + (cascade * 5))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.ItemUpgrade, m => RegisterItemUpgradeWindow(m, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 6), y + (cascade * 6))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.VegaSpell, m => RegisterVegaSpellWindow(m, uiWindow1Image, basicImage, soundUIImage, device, new Point(x + (cascade * 7), y + (cascade * 6))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.MemoMailbox, m => RegisterMemoMailboxWindow(m, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 7), y + (cascade * 4))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.PacketOwnedRewardResultNotice, m => RegisterPacketOwnedRewardResultNoticeWindow(m, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 6), y + (cascade * 2))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.RandomMesoBag, m => RegisterRandomMesoBagWindow(m, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 7), y + (cascade * 2))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.RandomMorph, m => RegisterRandomMorphWindow(m, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 7), y + (cascade * 3))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.QuestDelivery, m => RegisterQuestDeliveryWindow(m, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 6), y + (cascade * 3))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.QuestRewardRaise, m => RegisterQuestRewardRaiseWindow(m, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 6), y + (cascade * 3))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.Revive, m => RegisterReviveConfirmationWindow(m, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 7), y + (cascade * 3))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.RepairDurability, m => RegisterRepairDurabilityWindow(m, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 6), y + (cascade * 4))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.QuestAlarm, m => RegisterQuestAlarmWindow(m, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 8), y + (cascade * 8))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.ClassCompetition, m => RegisterClassCompetitionWindow(m, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 5), y + (cascade * 2))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.NpcShop, m => RegisterPacketOwnedNpcShopWindow(m, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 2), y + (cascade * 6))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.StoreBank, m => RegisterPacketOwnedStoreBankWindow(m, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 3), y + (cascade * 6))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.BattleRecord, m => RegisterPacketOwnedBattleRecordWindow(m, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 4), y + (cascade * 6))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.KeyConfig, m => RegisterKeyConfigWindow(m, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 4), y + (cascade * 4))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.OptionMenu, m => RegisterOptionMenuWindow(m, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 5), y + cascade)));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.Ranking, m => RegisterRankingWindow(m, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 6), y + (cascade * 2))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.Event, m => RegisterEventWindow(m, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 7), y + cascade)));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.MedalQuestInfo, m => RegisterPacketOwnedRawFunctionOwnerWindow(m, uiWindow2Image, basicImage, soundUIImage, device, MapSimulator.PacketOwnedRawFunctionOwner.Medal, "Medal", "Packet-owned medal key owner routed from the raw KeyConfig palette id 26.", new Point(x + (cascade * 8), y + (cascade * 2))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.ItemPot, m => RegisterPacketOwnedRawFunctionOwnerWindow(m, uiWindow2Image, basicImage, soundUIImage, device, MapSimulator.PacketOwnedRawFunctionOwner.ItemPot, "Item Pot", "Packet-owned item-pot key owner routed from the raw KeyConfig palette id 30.", new Point(x + (cascade * 8), y + (cascade * 3))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.MagicWheel, m => RegisterPacketOwnedRawFunctionOwnerWindow(m, uiWindow2Image, basicImage, soundUIImage, device, MapSimulator.PacketOwnedRawFunctionOwner.MagicWheel, "Magic Wheel", "Packet-owned magic-wheel key owner routed from the raw KeyConfig palette id 32.", new Point(x + (cascade * 8), y + (cascade * 4))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.Radio, m => RegisterRadioWindow(m, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 6), y + (cascade * 4))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.DragonBox, m => RegisterDragonBoxWindow(m, basicImage, soundUIImage, device, new Point(x + (cascade * 8), y + (cascade * 3))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.AccountMoreInfo, m => RegisterAccountMoreInfoWindow(m, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 7), y + (cascade * 4))));
        }

        private static void RegisterPacketOwnedRawFunctionOwnerWindow(
            UIWindowManager manager,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            MapSimulator.PacketOwnedRawFunctionOwner owner,
            string title,
            string body,
            Point position)
        {
            if (!MapSimulator.TryResolvePacketOwnedRawFunctionOwnerWindowRoute(owner, out MapSimulator.PacketOwnedRawFunctionOwnerWindowRoute route)
                || string.IsNullOrWhiteSpace(route.WindowName))
            {
                return;
            }

            RegisterPacketOwnedRawFunctionOwnerPlaceholderWindow(
                manager,
                uiWindow2Image,
                basicImage,
                soundUIImage,
                device,
                route.WindowName,
                title,
                body,
                route.HasUIWindow2Source ? route.UIWindow2SourcePropertyName : null,
                position);
        }

        private static void RegisterPacketOwnedRawFunctionOwnerPlaceholderWindow(
            UIWindowManager manager,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            string windowName,
            string title,
            string body,
            string sourcePropertyName,
            Point position)
        {
            if (manager == null
                || string.IsNullOrWhiteSpace(windowName)
                || manager.GetWindow(windowName) != null)
            {
                return;
            }

            WzSubProperty sourceProperty = !string.IsNullOrWhiteSpace(sourcePropertyName)
                ? uiWindow2Image?[sourcePropertyName] as WzSubProperty
                : null;
            UIWindowBase window = sourceProperty != null
                ? CreateWzPlaceholderUtilityWindow(sourceProperty, basicImage, soundUIImage, device, windowName, title, body, position)
                : CreatePlaceholderUtilityWindow(basicImage, soundUIImage, device, windowName, title, body, position);
            if (window != null)
            {
                manager.RegisterCustomWindow(window);
            }
        }



        private static void RegisterSocialRoomWindows(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            if (manager == null)
            {
                return;
            }


            int x = Math.Max(24, (screenWidth / 2) - 260);

            int y = Math.Max(24, (screenHeight / 2) - 196);

            const int cascade = 24;



            manager.RegisterLazyWindow(MapSimulatorWindowNames.MiniRoom, m => RegisterSocialRoomWindow(m, CreateMiniRoomWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x, y))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.PersonalShop, m => RegisterSocialRoomWindow(m, CreatePersonalShopWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + cascade, y + cascade))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.EntrustedShop, m => RegisterSocialRoomWindow(m, CreateEntrustedShopWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 2), y + (cascade * 2)))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.TradingRoom, m => RegisterSocialRoomWindow(m, CreateTradingRoomWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 3), y + (cascade * 3)))));
            manager.RegisterLazyWindow(MapSimulatorWindowNames.CashTradingRoom, m => RegisterSocialRoomWindow(m, CreateCashTradingRoomWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 4), y + (cascade * 4)))));
        }


        private static void RegisterSocialRoomWindow(UIWindowManager manager, UIWindowBase window)
        {
            if (manager == null || window == null || manager.GetWindow(window.WindowName) != null)
            {
                return;
            }


            manager.RegisterCustomWindow(window);
        }
        private static void RegisterKeyConfigWindow(
            UIWindowManager manager,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.KeyConfig) != null)
            {
                return;
            }


            UIWindowBase window = CreateKeyConfigWindow(uiWindow2Image, basicImage, soundUIImage, device, position);
            if (window != null)
            {
                manager.RegisterCustomWindow(window);
            }
        }
        private static void RegisterOptionMenuWindow(
            UIWindowManager manager,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.OptionMenu) != null)
            {
                return;
            }


            UIWindowBase window = CreateOptionMenuWindow(uiWindow2Image, basicImage, soundUIImage, device, position);
            if (window != null)
            {
                manager.RegisterCustomWindow(window);
            }
        }
        private static void RegisterRankingWindow(
            UIWindowManager manager,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.Ranking) != null)
            {
                return;
            }


            UIWindowBase window = CreateRankingWindow(uiWindow2Image, basicImage, soundUIImage, device, position);
            if (window != null)
            {
                manager.RegisterCustomWindow(window);
            }
        }
        private static void RegisterEventWindow(
            UIWindowManager manager,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.Event) != null)
            {
                return;
            }


            UIWindowBase window = CreateEventWindow(uiWindow2Image, basicImage, soundUIImage, device, position);
            if (window != null)
            {
                manager.RegisterCustomWindow(window);
            }
        }
        private static void RegisterRadioWindow(
            UIWindowManager manager,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.Radio) != null)
            {
                return;
            }


            UIWindowBase window = CreateRadioWindow(uiWindow2Image, basicImage, soundUIImage, device, position);
            if (window != null)
            {
                manager.RegisterCustomWindow(window);
            }
        }


        private static void RegisterChannelSelectionWindows(
            UIWindowManager manager,
            WzImage loginImage,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.WorldSelect) != null)
            {
                return;
            }

            const int ClientWorldSelectOwnerWidth = 0x234;
            const int ClientWorldSelectOwnerHeight = 0xB1;
            const int ClientWorldSelectOwnerOffsetX = -10;
            const int ClientWorldSelectOwnerOffsetY = -10;

            WzSubProperty loginWorldSelectProperty = loginImage?["WorldSelect"] as WzSubProperty;
            WzSubProperty channelProperty = uiWindow2Image?["Channel"] as WzSubProperty
                ?? uiWindow1Image?["Channel"] as WzSubProperty;
            if (loginWorldSelectProperty == null && channelProperty == null)
            {
                return;
            }


            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;

            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;



            Dictionary<int, Texture2D> worldBadges = new Dictionary<int, Texture2D>();
            WzSubProperty worldBadgeProperty = loginWorldSelectProperty?["world"] as WzSubProperty
                ?? channelProperty?["world"] as WzSubProperty;
            foreach (WzImageProperty property in worldBadgeProperty?.WzProperties ?? Enumerable.Empty<WzImageProperty>())
            {
                if (!int.TryParse(property.Name, out int worldId))
                {
                    continue;
                }


                Texture2D badgeTexture = LoadCanvasTexture(worldBadgeProperty, property.Name, device);
                if (badgeTexture != null && !worldBadges.ContainsKey(worldId))
                {
                    worldBadges.Add(worldId, badgeTexture);
                }
            }


            if (worldBadges.Count == 0)
            {
                return;
            }


            WorldSelectWindow worldSelectWindow = CreateWorldSelectWindow(
                loginWorldSelectProperty,
                loginImage?["WorldNotice"] as WzSubProperty,
                clickSound,
                overSound,
                device,
                worldBadges);

            worldSelectWindow.Position = new Point(
                Math.Max(24, ((screenWidth / 2) - (ClientWorldSelectOwnerWidth / 2)) + ClientWorldSelectOwnerOffsetX),
                Math.Max(24, ((screenHeight / 2) - (ClientWorldSelectOwnerHeight / 2)) + ClientWorldSelectOwnerOffsetY));

            manager.RegisterCustomWindow(worldSelectWindow);



            ChannelSelectWindow channelSelectWindow = CreateChannelSelectWindow(loginWorldSelectProperty, channelProperty, clickSound, overSound, device, worldBadges);
            if (channelSelectWindow != null)
            {
                channelSelectWindow.Position = new Point(Math.Max(24, (screenWidth / 2) - 185), Math.Max(24, (screenHeight / 2) - 84));
                manager.RegisterCustomWindow(channelSelectWindow);
            }


            ChannelShiftWindow channelShiftWindow = CreateChannelShiftWindow(loginWorldSelectProperty, channelProperty, device, worldBadges);
            if (channelShiftWindow != null)
            {
                channelShiftWindow.Position = new Point(Math.Max(24, (screenWidth / 2) - 185), Math.Max(24, (screenHeight / 2) - 84));
                manager.RegisterCustomWindow(channelShiftWindow);
            }
        }


        private static void RegisterLoginRecommendWorldWindow(
            UIWindowManager manager,
            WzImage loginImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            const int ClientLoginLayoutWidth = 800;
            const int ClientLoginLayoutHeight = 600;
            const int RecommendWorldLeft = 302;
            const int RecommendWorldTop = 152;


            if (manager.GetWindow(MapSimulatorWindowNames.RecommendWorld) != null)
            {
                return;
            }


            WzSubProperty alertProperty = loginImage?["WorldSelect"]?["alert"] as WzSubProperty;
            if (alertProperty == null)
            {
                return;
            }


            Texture2D frameTexture = LoadCanvasTexture(alertProperty, "backgrd", device);
            if (frameTexture == null)
            {
                return;
            }


            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;

            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;



            UIObject prevButton = LoadButton(alertProperty, "BtArrowL", clickSound, overSound, device);
            UIObject nextButton = LoadButton(alertProperty, "BtArrowR", clickSound, overSound, device);
            UIObject selectButton = LoadButton(alertProperty, "BtChoice", clickSound, overSound, device);
            UIObject closeButton = LoadButton(alertProperty, "BtClose", clickSound, overSound, device);


            Dictionary<int, Texture2D> worldNameTextures = new();
            WzSubProperty worldProperty = loginImage?["WorldSelect"]?["world"] as WzSubProperty;
            foreach (WzImageProperty property in worldProperty?.WzProperties ?? Enumerable.Empty<WzImageProperty>())
            {
                if (!int.TryParse(property.Name, out int worldId))
                {
                    continue;
                }


                Texture2D worldTexture = LoadCanvasTexture(worldProperty, property.Name, device);
                if (worldTexture != null)
                {
                    worldNameTextures[worldId] = worldTexture;
                }
            }


            if (prevButton != null)
            {
                prevButton.X = 34;
                prevButton.Y = 90;
            }


            if (nextButton != null)
            {
                nextButton.X = 135;
                nextButton.Y = 90;
            }


            if (selectButton != null)
            {
                selectButton.X = 47;
                selectButton.Y = 185;
            }


            if (closeButton != null)
            {
                closeButton.X = 104;
                closeButton.Y = 185;
            }


            RecommendWorldWindow window = new RecommendWorldWindow(
                new DXObject(0, 0, frameTexture, 0),
                worldNameTextures,
                prevButton,
                nextButton,
                selectButton,
                closeButton)
            {
                // Match CUIRecommendWorld::CUIRecommendWorld CreateDlg(302, 152, 200, 228)
                // against the client's 800x600 login layout, then center that layout in the viewport.
                Position = new Point(
                    Math.Max(24, ((screenWidth - ClientLoginLayoutWidth) / 2) + RecommendWorldLeft),
                    Math.Max(24, ((screenHeight - ClientLoginLayoutHeight) / 2) + RecommendWorldTop))
            };


            manager.RegisterCustomWindow(window);

        }



        private static WorldSelectWindow CreateWorldSelectWindow(
            WzSubProperty loginWorldSelectProperty,
            WzSubProperty worldNoticeProperty,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device,
            Dictionary<int, Texture2D> worldBadges)

        {

            WzSubProperty worldScrollProperty = loginWorldSelectProperty?["scroll"]?["0"] as WzSubProperty;
            Texture2D frameTexture = LoadCanvasTexture(worldScrollProperty, "0_bak", device)
                ?? CreatePlaceholderWindowTexture(device, 564, 177, "World Select");
            Texture2D overlayTexture = LoadCanvasTexture(worldScrollProperty, "0", device);
            Point overlayOffset = ResolveCanvasOffset(worldScrollProperty, "0", Point.Zero);
            WzSubProperty worldButtonProperty = loginWorldSelectProperty?["BtWorld"] as WzSubProperty;
            List<(int worldId, UIObject button, Texture2D icon, SelectorOverlayFrame keyFocusedFrame, SelectorAnimatedOverlay mouseOverOverlay)> worldButtons = new List<(int, UIObject, Texture2D, SelectorOverlayFrame, SelectorAnimatedOverlay)>();
            foreach (KeyValuePair<int, Texture2D> badge in worldBadges.OrderBy(pair => pair.Key))
            {
                string worldButtonName = badge.Key.ToString(CultureInfo.InvariantCulture);
                UIObject button = LoadButton(worldButtonProperty, badge.Key.ToString(CultureInfo.InvariantCulture), clickSound, overSound, device)
                    ?? CreateTextureButton(badge.Value, badge.Value);
                if (button == null)
                {
                    continue;
                }

                SelectorOverlayFrame keyFocusedFrame = LoadSelectorOverlayFrame(
                    worldButtonProperty?[worldButtonName]?["keyFocused"] as WzSubProperty,
                    device);
                SelectorAnimatedOverlay mouseOverOverlay = LoadSelectorAnimatedOverlay(
                    worldButtonProperty?[worldButtonName]?["mouseOver"],
                    device);
                worldButtons.Add((badge.Key, button, badge.Value, keyFocusedFrame, mouseOverOverlay));

            }

            List<UIObject> emptyWorldButtons = new List<UIObject>();
            UIObject emptySlotTemplate = LoadButton(worldButtonProperty, "e", clickSound, overSound, device);
            for (int i = 0; i < 36 - worldButtons.Count; i++)
            {
                UIObject emptyButton = LoadButton(worldButtonProperty, "e", clickSound, overSound, device)
                    ?? (i == 0 ? emptySlotTemplate : null);
                if (emptyButton == null)
                {
                    break;
                }

                emptyButton.SetEnabled(false);
                emptyWorldButtons.Add(emptyButton);
            }


            Dictionary<byte, SelectorAnimatedOverlay> worldStateAnimations = LoadWorldStateAnimations(worldNoticeProperty, device);

            return new WorldSelectWindow(
                new DXObject(0, 0, frameTexture, 0),
                overlayTexture,
                overlayOffset,
                CreateSolidTexture(device, Color.White),
                worldStateAnimations,
                worldButtons,
                emptyWorldButtons,
                LoadButton(loginWorldSelectProperty, "BtViewChoice", clickSound, overSound, device),
                LoadButton(loginWorldSelectProperty, "BtViewAll", clickSound, overSound, device),
                LoadSelectorOverlayFrame(loginWorldSelectProperty?["BtViewAll"]?["keyFocused"] as WzSubProperty, device),
                LoadSelectorAnimatedOverlay(loginWorldSelectProperty?["BtViewAll"]?["mouseOver"], device));

        }

        private static Dictionary<byte, SelectorAnimatedOverlay> LoadWorldStateAnimations(
            WzSubProperty worldNoticeProperty,
            GraphicsDevice device)
        {
            Dictionary<byte, SelectorAnimatedOverlay> animations = new();
            if (worldNoticeProperty == null || device == null)
            {
                return animations;
            }

            foreach (WzImageProperty property in worldNoticeProperty.WzProperties)
            {
                if (!byte.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte worldState))
                {
                    continue;
                }

                SelectorAnimatedOverlay overlay = LoadSelectorAnimatedOverlay(property, device);
                if (overlay == null || overlay.IsEmpty)
                {
                    continue;
                }

                animations[worldState] = overlay;
            }

            return animations;
        }



        private static ChannelSelectWindow CreateChannelSelectWindow(
            WzSubProperty loginWorldSelectProperty,
            WzSubProperty channelProperty,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device,
            Dictionary<int, Texture2D> worldBadges)
        {
            WzSubProperty loginChannelProperty = loginWorldSelectProperty?["channel"] as WzSubProperty;
            int channelSlotCount = ResolveChannelSlotCount(loginChannelProperty, channelProperty?["ch"] as WzSubProperty);
            Texture2D frameTexture = LoadCanvasTexture(loginWorldSelectProperty, "chBackgrn", device)
                ?? LoadCanvasTexture(channelProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return null;
            }


            Texture2D overlayTexture2 = LoadCanvasTexture(loginWorldSelectProperty, "chBackgrn_Bak", device)
                ?? LoadCanvasTexture(channelProperty, "backgrnd2", device);

            Texture2D overlayTexture3 = LoadCanvasTexture(loginWorldSelectProperty?["scroll"]?["1"] as WzSubProperty, "0", device)
                ?? LoadCanvasTexture(channelProperty, "backgrnd3", device);

            Point overlayOffset2 = ResolveCanvasOffset(loginWorldSelectProperty?["chBackgrn_Bak"] as WzCanvasProperty, Point.Zero);

            Point overlayOffset3 = ResolveCanvasOffset(loginWorldSelectProperty?["scroll"]?["1"]?["0"] as WzCanvasProperty, Point.Zero);
            Texture2D gaugeTexture = LoadCanvasTexture(loginChannelProperty, "chgauge", device);
            IReadOnlyList<Texture2D> selectionFrames = LoadIndexedCanvasTextureList(loginChannelProperty?["chSelect"] as WzSubProperty, device);

            if (overlayOffset2 == Point.Zero)
            {
                overlayOffset2 = GetCanvasOffset(channelProperty?["backgrnd2"] as WzCanvasProperty);
            }

            if (overlayOffset3 == Point.Zero)
            {
                overlayOffset3 = GetCanvasOffset(channelProperty?["backgrnd3"] as WzCanvasProperty);
            }



            UIObject changeButton = LoadButton(loginWorldSelectProperty, "BtGoworld", clickSound, overSound, device)
                ?? LoadButton(channelProperty, "BtChange", clickSound, overSound, device);

            UIObject cancelButton = loginWorldSelectProperty != null
                ? null
                : LoadButton(channelProperty, "BtCancel", clickSound, overSound, device);
            if (changeButton != null)
            {
                changeButton.X = loginWorldSelectProperty != null ? 230 : 278;
                changeButton.Y = loginWorldSelectProperty != null ? 43 : 20;
            }


            if (cancelButton != null)
            {
                cancelButton.X = 228;
                cancelButton.Y = 20;
            }


            List<(int channelIndex, UIObject button, Texture2D icon)> channelButtons = new List<(int, UIObject, Texture2D)>();
            for (int channelIndex = 0; channelIndex < channelSlotCount; channelIndex++)
            {
                UIObject button;
                Texture2D icon;
                if (loginChannelProperty?[channelIndex.ToString(CultureInfo.InvariantCulture)] is WzSubProperty loginChannelEntry)
                {
                    Texture2D normalTexture = LoadCanvasTexture(loginChannelEntry, "normal", device);
                    Texture2D disabledTexture = LoadCanvasTexture(loginChannelEntry, "disabled", device) ?? normalTexture;
                    button = CreateTextureButton(normalTexture, disabledTexture, normalTexture, normalTexture);
                    icon = null;
                }
                else
                {
                    Texture2D channelNormalTexture = LoadCanvasTexture(channelProperty, "channel0", device);
                    Texture2D channelSelectedTexture = LoadCanvasTexture(channelProperty, "channel1", device) ?? channelNormalTexture;
                    WzSubProperty channelIconProperty = channelProperty?["ch"] as WzSubProperty;
                    button = CreateTextureButton(channelNormalTexture, channelSelectedTexture);
                    icon = LoadCanvasTexture(channelIconProperty, channelIndex.ToString(CultureInfo.InvariantCulture), device);
                }
                if (button == null)
                {
                    continue;
                }


                int column = channelIndex % 5;
                int row = channelIndex / 5;
                button.X = 23 + (column * 66);
                button.Y = 93 + (row * 29);
                channelButtons.Add((channelIndex, button, icon));
            }


            return new ChannelSelectWindow(
                new DXObject(0, 0, frameTexture, 0),
                overlayTexture2,
                overlayOffset2,
                overlayTexture3,
                overlayOffset3,
                CreateSolidTexture(device, Color.White),
                gaugeTexture,
                selectionFrames,
                150,
                changeButton,
                cancelButton,
                channelButtons,
                worldBadges);
        }


        private static ChannelShiftWindow CreateChannelShiftWindow(
            WzSubProperty loginWorldSelectProperty,
            WzSubProperty channelProperty,
            GraphicsDevice device,
            Dictionary<int, Texture2D> worldBadges)
        {
            WzSubProperty loginChannelProperty = loginWorldSelectProperty?["channel"] as WzSubProperty;
            int channelSlotCount = ResolveChannelSlotCount(loginChannelProperty, channelProperty?["ch"] as WzSubProperty);
            Texture2D frameTexture = LoadCanvasTexture(loginWorldSelectProperty, "chBackgrn", device)
                ?? LoadCanvasTexture(channelProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return null;
            }


            Dictionary<int, Texture2D> channelIcons = new Dictionary<int, Texture2D>();
            WzSubProperty channelIconProperty = channelProperty?["ch"] as WzSubProperty;
            for (int channelIndex = 0; channelIndex < channelSlotCount; channelIndex++)
            {
                Texture2D channelTexture = loginChannelProperty?[channelIndex.ToString(CultureInfo.InvariantCulture)] is WzSubProperty loginChannelEntry
                    ? LoadCanvasTexture(loginChannelEntry, "normal", device)
                    : LoadCanvasTexture(channelIconProperty, channelIndex.ToString(CultureInfo.InvariantCulture), device);
                if (channelTexture != null)
                {
                    channelIcons[channelIndex] = channelTexture;
                }
            }

            IReadOnlyList<Texture2D> selectionFrames = LoadIndexedCanvasTextureList(loginChannelProperty?["chSelect"] as WzSubProperty, device);


            return new ChannelShiftWindow(

                new DXObject(0, 0, frameTexture, 0),

                LoadCanvasTexture(loginWorldSelectProperty, "chBackgrn_Bak", device)
                    ?? LoadCanvasTexture(channelProperty, "backgrnd2", device),

                ResolveCanvasOffset(loginWorldSelectProperty?["chBackgrn_Bak"] as WzCanvasProperty, Point.Zero) != Point.Zero
                    ? ResolveCanvasOffset(loginWorldSelectProperty?["chBackgrn_Bak"] as WzCanvasProperty, Point.Zero)
                    : GetCanvasOffset(channelProperty?["backgrnd2"] as WzCanvasProperty),

                LoadCanvasTexture(loginWorldSelectProperty?["scroll"]?["1"] as WzSubProperty, "0", device)
                    ?? LoadCanvasTexture(channelProperty, "backgrnd3", device),

                ResolveCanvasOffset(loginWorldSelectProperty?["scroll"]?["1"]?["0"] as WzCanvasProperty, Point.Zero) != Point.Zero
                    ? ResolveCanvasOffset(loginWorldSelectProperty?["scroll"]?["1"]?["0"] as WzCanvasProperty, Point.Zero)
                    : GetCanvasOffset(channelProperty?["backgrnd3"] as WzCanvasProperty),

                worldBadges,
                channelIcons,
                selectionFrames,
                150);
        }

        private static int ResolveChannelSlotCount(WzSubProperty loginChannelProperty, WzSubProperty fallbackChannelIconProperty)
        {
            int maxChannelIndex = -1;
            foreach (WzImageProperty property in loginChannelProperty?.WzProperties ?? Enumerable.Empty<WzImageProperty>())
            {
                if (int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int channelIndex) && channelIndex >= 0)
                {
                    maxChannelIndex = Math.Max(maxChannelIndex, channelIndex);
                }
            }

            foreach (WzImageProperty property in fallbackChannelIconProperty?.WzProperties ?? Enumerable.Empty<WzImageProperty>())
            {
                if (int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int channelIndex) && channelIndex >= 0)
                {
                    maxChannelIndex = Math.Max(maxChannelIndex, channelIndex);
                }
            }

            return maxChannelIndex >= 0
                ? Math.Clamp(maxChannelIndex + 1, 1, 60)
                : 20;
        }


        private static void RegisterAdminShopWindow(
            UIWindowManager manager,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            string windowName,
            AdminShopServiceMode defaultMode,
            Point position,
            IStorageRuntime storageRuntime)
        {
            if (manager.GetWindow(windowName) != null)
            {
                return;
            }


            UIWindowBase window = CreateAdminShopDialogWindow(
                uiWindow2Image,
                basicImage,
                soundUIImage,
                device,
                windowName,
                defaultMode,
                position);
            if (window is AdminShopDialogUI adminShop)
            {
                adminShop.SetStorageRuntime(storageRuntime);
                adminShop.WishlistWindowRequested = sourceDialog =>
                {
                    if (manager.GetWindow(MapSimulatorWindowNames.AdminShopWishList) is not AdminShopWishListUI wishListWindow)
                    {
                        return;
                    }

                    manager.ShowWindow(wishListWindow, () => wishListWindow.PrepareForShow(sourceDialog));
                };

                if (defaultMode == AdminShopServiceMode.CashShop
                    && manager.GetWindow(MapSimulatorWindowNames.CashAvatarPreview) is CashAvatarPreviewWindow previewWindow)
                {
                    previewWindow.SetSelectionProvider(adminShop.GetAvatarPreviewSelection);
                    previewWindow.SetShopRequestHandler(adminShop.SubmitSelectedEntryPreviewRequest);
                }
            }


            manager.RegisterCustomWindow(window);

        }



        private static void RegisterAdminShopWishListWindow(
            UIWindowManager manager,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.AdminShopWishList) != null)
            {
                return;
            }

            AdminShopWishListCategoryUI categoryWindow = manager.GetWindow(MapSimulatorWindowNames.AdminShopWishListCategory) as AdminShopWishListCategoryUI;
            if (categoryWindow == null)
            {
                UIWindowBase categoryBaseWindow = CreateAdminShopWishListCategoryWindow(uiWindow2Image, basicImage, soundUIImage, device, position);
                manager.RegisterCustomWindow(categoryBaseWindow);
                categoryWindow = categoryBaseWindow as AdminShopWishListCategoryUI;
            }

            AdminShopWishListSearchResultUI searchResultWindow = manager.GetWindow(MapSimulatorWindowNames.AdminShopWishListSearchResult) as AdminShopWishListSearchResultUI;
            if (searchResultWindow == null)
            {
                UIWindowBase searchResultBaseWindow = CreateAdminShopWishListSearchResultWindow(uiWindow2Image, basicImage, soundUIImage, device, position);
                manager.RegisterCustomWindow(searchResultBaseWindow);
                searchResultWindow = searchResultBaseWindow as AdminShopWishListSearchResultUI;
            }

            UIWindowBase window = CreateAdminShopWishListWindow(uiWindow2Image, basicImage, soundUIImage, device, position);
            if (window is AdminShopWishListUI wishListWindow)
            {
                if (categoryWindow != null)
                {
                    wishListWindow.ShowCategoryAddOnRequested = owner =>
                    {
                        manager.ShowWindow(categoryWindow, () => categoryWindow.PrepareForShow(owner));
                    };
                    wishListWindow.HideCategoryAddOnRequested = () =>
                    {
                        if (categoryWindow.IsVisible)
                        {
                            categoryWindow.Hide();
                        }
                    };
                    wishListWindow.IsCategoryAddOnVisible = () => categoryWindow.IsVisible;
                }

                if (searchResultWindow != null)
                {
                    wishListWindow.ShowSearchResultAddOnRequested = (owner, results) =>
                    {
                        manager.ShowWindow(searchResultWindow, () => searchResultWindow.PrepareForShow(owner, results));
                    };
                    wishListWindow.HideSearchResultAddOnRequested = () =>
                    {
                        if (searchResultWindow.IsVisible)
                        {
                            searchResultWindow.Hide();
                        }
                    };
                    wishListWindow.IsSearchResultAddOnVisible = () => searchResultWindow.IsVisible;
                }
            }

            manager.RegisterCustomWindow(window);

        }

        private static void RegisterCashServiceStageWindow(
            UIWindowManager manager,
            GraphicsDevice device,
            string windowName,
            CashServiceWindowStageKind stageKind,
            Point position)
        {
            if (manager == null || device == null || manager.GetWindow(windowName) != null)
            {
                return;
            }

            UIWindowBase window = CreateCashServiceStageWindow(device, windowName, stageKind, position);
            if (window != null)
            {
                manager.RegisterCustomWindow(window);
            }
        }

        private static void RegisterDragonBoxWindow(
            UIWindowManager manager,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.DragonBox) != null)
            {
                return;
            }

            UIWindowBase window = CreateDragonBoxWindow(basicImage, soundUIImage, device, position);
            if (window != null)
            {
                manager.RegisterCustomWindow(window);
            }
        }

        private static void RegisterAccountMoreInfoWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.AccountMoreInfo) != null)
            {
                return;
            }

            UIWindowBase window = CreateAccountMoreInfoWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, position);
            if (window != null)
            {
                manager.RegisterCustomWindow(window);
            }
        }

        private static UIWindowBase CreateCashServiceStageWindow(
            GraphicsDevice device,
            string windowName,
            CashServiceWindowStageKind stageKind,
            Point position)
        {
            string serviceImageName = stageKind == CashServiceWindowStageKind.CashShop
                ? "CashShop.img"
                : "ITC.img";
            WzImage serviceImage = global::HaCreator.Program.FindImage("ui", serviceImageName);
            WzSubProperty serviceBaseProperty = serviceImage?["Base"] as WzSubProperty;

            Texture2D frameTexture = LoadCanvasTexture(serviceBaseProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                Color fallback = stageKind == CashServiceWindowStageKind.CashShop
                    ? new Color(41, 31, 18, 255)
                    : new Color(17, 31, 49, 255);
                frameTexture = CreateFilledTexture(device, 800, 600, fallback, fallback);
            }

            CashServiceStageWindow window = new(
                new DXObject(0, 0, frameTexture, 0),
                windowName,
                stageKind,
                device)
            {
                Position = position
            };

            if (stageKind == CashServiceWindowStageKind.CashShop && serviceBaseProperty != null)
            {
                for (int i = 0; i <= 5; i++)
                {
                    string backdropName = i == 0 ? "backgrnd" : $"backgrnd{i}";
                    Texture2D backdropTexture = LoadCanvasTexture(serviceBaseProperty, backdropName, device);
                    if (backdropTexture != null)
                    {
                        window.AddBackdropVariant(i, backdropTexture);
                    }
                }
            }

            return window;
        }

        private static WzSubProperty ResolveCreateCharacterRaceProperty(
            WzImage loginImage,
            LoginCreateCharacterRaceKind race,
            WzSubProperty fallbackProperty)
        {
            WzSubProperty customizeCharProperty = loginImage?["CustomizeChar"] as WzSubProperty;
            string raceNode = race switch
            {
                LoginCreateCharacterRaceKind.Cygnus => "1000",
                LoginCreateCharacterRaceKind.Aran => "2000",
                LoginCreateCharacterRaceKind.Evan => "2001",
                LoginCreateCharacterRaceKind.Resistance => "3000",
                _ => "000"
            };
            string familyNode = race switch
            {
                LoginCreateCharacterRaceKind.Cygnus => "NewCharKnight",
                LoginCreateCharacterRaceKind.Aran => "NewCharAran",
                LoginCreateCharacterRaceKind.Evan => "NewCharEvan",
                LoginCreateCharacterRaceKind.Resistance => "NewCharResistance",
                _ => "NewChar"
            };

            return customizeCharProperty?[raceNode] as WzSubProperty
                ?? loginImage?[familyNode] as WzSubProperty
                ?? fallbackProperty;
        }

        private static void RegisterCashAvatarPreviewWindow(
            UIWindowManager manager,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.CashAvatarPreview) != null)
            {
                return;
            }

            UIWindowBase window = CreateCashAvatarPreviewWindow(basicImage, soundUIImage, device, position);
            if (window != null)
            {
                manager.RegisterCustomWindow(window);
            }
        }

        private static void RegisterCashShopStageChildWindows(
            UIWindowManager manager,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            RegisterCashShopStageChildWindow(manager, basicImage, soundUIImage, device, position, MapSimulatorWindowNames.CashShopLocker);
            RegisterCashShopStageChildWindow(manager, basicImage, soundUIImage, device, position, MapSimulatorWindowNames.CashShopInventory);
            RegisterCashShopStageChildWindow(manager, basicImage, soundUIImage, device, position, MapSimulatorWindowNames.CashShopList);
            RegisterCashShopStageChildWindow(manager, basicImage, soundUIImage, device, position, MapSimulatorWindowNames.CashShopStatus);
            RegisterCashShopStageChildWindow(manager, basicImage, soundUIImage, device, position, MapSimulatorWindowNames.CashShopOneADay);
        }

        private static void RegisterCashServiceModalOwnerWindows(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            RegisterCashServiceModalOwnerWindow(manager, uiWindow1Image, uiWindow2Image, device, screenWidth, screenHeight, MapSimulatorWindowNames.CashCouponDialog);
            RegisterCashServiceModalOwnerWindow(manager, uiWindow1Image, uiWindow2Image, device, screenWidth, screenHeight, MapSimulatorWindowNames.CashPurchaseConfirmDialog);
            RegisterCashServiceModalOwnerWindow(manager, uiWindow1Image, uiWindow2Image, device, screenWidth, screenHeight, MapSimulatorWindowNames.CashReceiveGiftDialog);
            RegisterCashServiceModalOwnerWindow(manager, uiWindow1Image, uiWindow2Image, device, screenWidth, screenHeight, MapSimulatorWindowNames.CashNameChangeLicenseDialog);
            RegisterCashServiceModalOwnerWindow(manager, uiWindow1Image, uiWindow2Image, device, screenWidth, screenHeight, MapSimulatorWindowNames.CashTransferWorldLicenseDialog);
        }

        private static void RegisterCashServiceModalOwnerWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight,
            string windowName)
        {
            if (manager == null || device == null || manager.GetWindow(windowName) != null)
            {
                return;
            }

            Texture2D frameTexture = ResolveCashServiceModalFrameTexture(uiWindow1Image, uiWindow2Image, device, windowName)
                ?? CreateFilledTexture(device, 266, 158, new Color(34, 34, 34, 240), new Color(118, 118, 118, 255));
            CashServiceModalOwnerWindow window = new(windowName, frameTexture, device, screenWidth, screenHeight, null);
            WzImage basicImage = global::HaCreator.Program.FindImage("ui", "Basic.img");
            window.SetOwnerChrome(
                LoadTextureFromClientUiPath("UI/Basic.img/ComboBox/normal/0", basicImage, uiWindow1Image, device),
                LoadTextureFromClientUiPath("UI/Basic.img/ComboBox/normal/1", basicImage, uiWindow1Image, device),
                LoadTextureFromClientUiPath("UI/Basic.img/ComboBox/normal/2", basicImage, uiWindow1Image, device),
                LoadTextureFromClientUiPath("UI/Basic.img/CheckBox/0", basicImage, uiWindow1Image, device),
                LoadTextureFromClientUiPath("UI/Basic.img/CheckBox/1", basicImage, uiWindow1Image, device));
            manager.RegisterCustomWindow(window);
        }

        private static Texture2D ResolveCashServiceModalFrameTexture(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            GraphicsDevice device,
            string windowName)
        {
            WzImage cashShopImage = global::HaCreator.Program.FindImage("ui", "CashShop.img");
            return windowName switch
            {
                MapSimulatorWindowNames.CashCouponDialog => LoadCanvasTexture(uiWindow2Image?["Coupon"] as WzSubProperty, "backgrnd", device)
                    ?? LoadCanvasTexture(uiWindow1Image?["Coupon"] as WzSubProperty, "backgrnd", device),
                MapSimulatorWindowNames.CashNameChangeLicenseDialog => LoadCanvasTexture(cashShopImage?["CSChangeName"]?["Base"] as WzSubProperty, "backgrndnotice", device)
                    ?? LoadCanvasTexture(cashShopImage?["CSChangeName"]?["Base"] as WzSubProperty, "backgrnd", device),
                MapSimulatorWindowNames.CashTransferWorldLicenseDialog => LoadCanvasTexture(cashShopImage?["CSTransferWorld"]?["Base"] as WzSubProperty, "backgrndnotice", device)
                    ?? LoadCanvasTexture(cashShopImage?["CSTransferWorld"]?["Base"] as WzSubProperty, "backgrnd", device),
                _ => null
            };
        }

        private static void RegisterItcStageChildWindows(
            UIWindowManager manager,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            RegisterCashShopStageChildWindow(manager, basicImage, soundUIImage, device, position, MapSimulatorWindowNames.ItcCharacter);
            RegisterCashShopStageChildWindow(manager, basicImage, soundUIImage, device, position, MapSimulatorWindowNames.ItcSale);
            RegisterCashShopStageChildWindow(manager, basicImage, soundUIImage, device, position, MapSimulatorWindowNames.ItcPurchase);
            RegisterCashShopStageChildWindow(manager, basicImage, soundUIImage, device, position, MapSimulatorWindowNames.ItcInventory);
            RegisterCashShopStageChildWindow(manager, basicImage, soundUIImage, device, position, MapSimulatorWindowNames.ItcTab);
            RegisterCashShopStageChildWindow(manager, basicImage, soundUIImage, device, position, MapSimulatorWindowNames.ItcSubTab);
            RegisterCashShopStageChildWindow(manager, basicImage, soundUIImage, device, position, MapSimulatorWindowNames.ItcList);
            RegisterCashShopStageChildWindow(manager, basicImage, soundUIImage, device, position, MapSimulatorWindowNames.ItcStatus);
        }

        private static void RegisterCashShopStageChildWindow(
            UIWindowManager manager,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position,
            string windowName)
        {
            if (manager == null || manager.GetWindow(windowName) != null)
            {
                return;
            }

            UIWindowBase window = CreateCashShopStageChildWindow(basicImage, soundUIImage, device, position, windowName);
            if (window != null)
            {
                manager.RegisterCustomWindow(window);
            }
        }



        private static void RegisterPlaceholderWindow(
            UIWindowManager manager,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            string windowName,
            string title,
            string body,
            Point position)
        {
            if (manager.GetWindow(windowName) != null)
            {
                return;
            }


            PlaceholderUtilityWindow window = CreatePlaceholderUtilityWindow(
                basicImage,
                soundUIImage,
                device,
                windowName,
                title,
                body,
                position);
            manager.RegisterCustomWindow(window);
        }


        private static void RegisterItemMakerWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.ItemMaker) != null)
            {
                return;
            }


            UIWindowBase itemMaker = CreateItemMakerWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, position, manager.InventoryWindow as IInventoryRuntime);
            if (itemMaker is ItemMakerUI itemMakerWindow)
            {
                itemMakerWindow.SetProductionEnhancementAnimationDisplayer(manager.ProductionEnhancementAnimationDisplayer);
            }

            ConfigureProductionEnhancementAnimationDisplayer(manager, device, hammerProperty: null);

            manager.RegisterCustomWindow(itemMaker);

        }



        private static void RegisterBookCollectionWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.BookCollection) != null)
            {
                return;
            }


            UIWindowBase bookCollectionWindow = CreateBookCollectionWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, position);
            if (bookCollectionWindow != null)
            {
                manager.RegisterCustomWindow(bookCollectionWindow);
            }
        }


        private static void RegisterItemUpgradeWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.ItemUpgrade) != null)
            {
                return;
            }


            SeedStarterEnhancementInventory(manager.InventoryWindow as IInventoryRuntime);
            UIWindowBase itemUpgrade = CreateItemUpgradeWindow(
                manager,
                uiWindow1Image,
                uiWindow2Image,
                basicImage,
                soundUIImage,
                device,
                position,
                manager.InventoryWindow as IInventoryRuntime);
            if (itemUpgrade is ItemUpgradeUI itemUpgradeWindow)
            {
                itemUpgradeWindow.SetProductionEnhancementAnimationDisplayer(manager.ProductionEnhancementAnimationDisplayer);
            }

            ConfigureProductionEnhancementAnimationDisplayer(
                manager,
                device,
                uiWindow1Image?["ViciousHammer"] as WzSubProperty
                    ?? uiWindow2Image?["GoldHammer"] as WzSubProperty);
            manager.RegisterCustomWindow(itemUpgrade);
        }


        private static void RegisterVegaSpellWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.VegaSpell) != null)
            {
                return;
            }


            UIWindowBase vegaSpellWindow = CreateVegaSpellWindow(uiWindow1Image, basicImage, soundUIImage, device, position);
            if (vegaSpellWindow is VegaSpellUI vegaSpellUi)
            {
                vegaSpellUi.SetProductionEnhancementAnimationDisplayer(manager.ProductionEnhancementAnimationDisplayer);
            }

            ConfigureVegaAnimationDisplayer(
                manager,
                uiWindow1Image?["VegaSpell"] as WzSubProperty,
                device);

            manager.RegisterCustomWindow(vegaSpellWindow);

        }



        private static void RegisterQuestAlarmWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.QuestAlarm) != null)
            {
                return;
            }


            UIWindowBase questAlarm = CreateQuestAlarmWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, position);

            manager.RegisterCustomWindow(questAlarm);

        }



        private static void RegisterQuestDeliveryWindow(
            UIWindowManager manager,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.QuestDelivery) != null)
            {
                return;
            }


            UIWindowBase questDeliveryWindow = CreateQuestDeliveryWindow(uiWindow2Image, basicImage, soundUIImage, device, position);
            if (questDeliveryWindow != null)
            {
                manager.RegisterCustomWindow(questDeliveryWindow);
            }
        }

        private static void RegisterPacketOwnedRewardResultNoticeWindow(
            UIWindowManager manager,
            WzImage uiWindowImage,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.PacketOwnedRewardResultNotice) != null)
            {
                return;
            }

            UIWindowBase window = CreatePacketOwnedRewardResultNoticeWindow(uiWindowImage, uiWindow2Image, basicImage, soundUIImage, device, position);
            if (window != null)
            {
                manager.RegisterCustomWindow(window);
            }
        }

        private static void RegisterRandomMesoBagWindow(
            UIWindowManager manager,
            WzImage uiWindowImage,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.RandomMesoBag) != null)
            {
                return;
            }

            UIWindowBase window = CreateRandomMesoBagWindow(uiWindowImage, uiWindow2Image, basicImage, soundUIImage, device, position);
            if (window != null)
            {
                manager.RegisterCustomWindow(window);
            }
        }

        private static void RegisterRandomMorphWindow(
            UIWindowManager manager,
            WzImage uiWindowImage,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.RandomMorph) != null)
            {
                return;
            }

            UIWindowBase window = CreateRandomMorphWindow(uiWindowImage, uiWindow2Image, basicImage, soundUIImage, device, position);
            if (window != null)
            {
                manager.RegisterCustomWindow(window);
            }
        }


        private static void RegisterQuestRewardRaiseWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.QuestRewardRaise) != null)
            {
                return;
            }


            UIWindowBase questRewardRaiseWindow = CreateQuestRewardRaiseWindow(
                uiWindow1Image,
                uiWindow2Image,
                basicImage,
                soundUIImage,
                device,
                position);
            if (questRewardRaiseWindow != null)
            {
                manager.RegisterCustomWindow(questRewardRaiseWindow);
            }
        }


        private static void RegisterClassCompetitionWindow(
            UIWindowManager manager,
            WzImage uiWindowImage,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.ClassCompetition) != null)
            {
                return;
            }

            UIWindowBase classCompetitionWindow = CreateClassCompetitionWindow(uiWindowImage, uiWindow2Image, basicImage, soundUIImage, device, position);
            if (classCompetitionWindow != null)
            {
                manager.RegisterCustomWindow(classCompetitionWindow);
            }

        }

        private static void RegisterRepairDurabilityWindow(
            UIWindowManager manager,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.RepairDurability) != null)
            {
                return;
            }

            UIWindowBase repairWindow = CreateRepairDurabilityWindow(uiWindow2Image, basicImage, soundUIImage, device, position);
            if (repairWindow != null)
            {
                manager.RegisterCustomWindow(repairWindow);
            }
        }



        private static void RegisterMemoMailboxWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.MemoMailbox) != null)
            {
                return;
            }


            UIWindowBase memoMailboxWindow = CreateMemoMailboxWindow(uiWindow2Image, basicImage, soundUIImage, device, position);

            manager.RegisterCustomWindow(memoMailboxWindow);



            UIWindowBase memoSendWindow = CreateMemoSendWindow(uiWindow2Image, basicImage, soundUIImage, device, new Point(position.X + 24, position.Y + 18));
            if (memoSendWindow != null)
            {
                manager.RegisterCustomWindow(memoSendWindow);
            }


            UIWindowBase memoGetWindow = CreateMemoGetWindow(uiWindow2Image, basicImage, soundUIImage, device, new Point(position.X + 18, position.Y + 10));
            if (memoGetWindow != null)
            {
                manager.RegisterCustomWindow(memoGetWindow);
            }
        }


        private static void RegisterSocialListWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.SocialList) != null)
            {
                return;
            }


            UIWindowBase socialListWindow = CreateSocialListWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, position);
            if (socialListWindow != null)
            {
                manager.RegisterCustomWindow(socialListWindow);
            }


            UIWindowBase socialSearchWindow = CreateSocialSearchWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(position.X + 18, position.Y + 14));
            if (socialSearchWindow != null)
            {
                manager.RegisterCustomWindow(socialSearchWindow);
            }

            UIWindowBase friendGroupWindow = CreateFriendGroupWindow(uiWindow1Image, uiWindow2Image, soundUIImage, device, new Point(position.X + 12, position.Y + 6));
            if (friendGroupWindow != null)
            {
                manager.RegisterCustomWindow(friendGroupWindow);
            }


            UIWindowBase guildSearchWindow = CreateGuildSearchWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(position.X + 26, position.Y + 8));
            if (guildSearchWindow != null)
            {
                manager.RegisterCustomWindow(guildSearchWindow);
            }


            UIWindowBase guildSkillWindow = CreateGuildSkillWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(position.X + 36, Math.Max(24, position.Y - 12)));
            if (guildSkillWindow != null)
            {
                manager.RegisterCustomWindow(guildSkillWindow);
            }



            UIWindowBase guildManageWindow = CreateGuildManageWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(position.X + 42, position.Y + 10));
            if (guildManageWindow != null)
            {
                manager.RegisterCustomWindow(guildManageWindow);
            }


            UIWindowBase guildRankWindow = CreateGuildRankWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(position.X + 54, Math.Max(24, position.Y - 6)));
            if (guildRankWindow != null)
            {
                manager.RegisterCustomWindow(guildRankWindow);
            }


            UIWindowBase guildMarkWindow = CreateGuildMarkWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(position.X + 64, Math.Max(24, position.Y - 2)));
            if (guildMarkWindow != null)
            {
                manager.RegisterCustomWindow(guildMarkWindow);
            }


            UIWindowBase guildCreateAgreementWindow = CreateGuildCreateAgreementWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(position.X + 72, Math.Max(24, position.Y + 6)));
            if (guildCreateAgreementWindow != null)
            {
                manager.RegisterCustomWindow(guildCreateAgreementWindow);
            }


            UIWindowBase allianceEditorWindow = CreateAllianceEditorWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(position.X + 50, position.Y + 2));
            if (allianceEditorWindow != null)
            {
                manager.RegisterCustomWindow(allianceEditorWindow);
            }
        }


        private static void RegisterFamilyChartWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.FamilyChart) != null)
            {
                return;
            }


            UIWindowBase familyChartWindow = CreateFamilyChartWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, position);
            if (familyChartWindow != null)
            {
                manager.RegisterCustomWindow(familyChartWindow);
            }


            UIWindowBase familyTreeWindow = CreateFamilyTreeWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(position.X + 120, Math.Max(24, position.Y - 36)));
            if (familyTreeWindow != null)
            {
                manager.RegisterCustomWindow(familyTreeWindow);
            }
        }


        private static void RegisterMessengerWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.Messenger) != null)
            {
                return;
            }


            UIWindowBase messengerWindow = CreateMessengerWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, position);

            manager.RegisterCustomWindow(messengerWindow);

        }



        public static void RegisterGuildBbsWindow(
            UIWindowManager manager,
            WzImage guildBbsImage,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.GuildBbs) != null)
            {
                return;
            }


            manager.RegisterCustomWindow(CreateGuildBbsWindow(guildBbsImage, basicImage, soundUIImage, device, position));

        }



        private static void RegisterMapleTvWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage mapleTvImage,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.MapleTv) != null)
            {
                return;
            }


            UIWindowBase mapleTvWindow = CreateMapleTvWindow(uiWindow1Image, mapleTvImage, basicImage, soundUIImage, device, position);
            if (mapleTvWindow != null)
            {
                manager.RegisterCustomWindow(mapleTvWindow);
            }
        }
        private static void RegisterEngagementProposalWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.EngagementProposal) != null)
            {
                return;
            }

            EngagementProposalWindow window = CreateEngagementProposalWindow(uiWindow1Image, uiWindow2Image, soundUIImage, device)
                ?? CreateFallbackEngagementProposalWindow(device);
            window.Position = position;
            manager.RegisterCustomWindow(window);
        }

        private static void RegisterWeddingInvitationWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.WeddingInvitation) != null)
            {
                return;
            }

            WeddingInvitationWindow window = CreateWeddingInvitationWindow(uiWindow1Image, uiWindow2Image, soundUIImage, device)
                ?? CreateFallbackWeddingInvitationWindow(device);
            window.Position = position;
            manager.RegisterCustomWindow(window);
        }

        private static void RegisterWeddingWishListWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.WeddingWishList) != null)
            {
                return;
            }

            WeddingWishListWindow window = CreateWeddingWishListWindow(uiWindow1Image, uiWindow2Image, soundUIImage, device)
                ?? CreateFallbackWeddingWishListWindow(device);
            window.Position = position;
            manager.RegisterCustomWindow(window);
        }
        private static UIWindowBase CreateMiniRoomWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty minigameRoot = uiWindow2Image?["Minigame"] as WzSubProperty
                ?? uiWindow1Image?["Minigame"] as WzSubProperty;
            WzSubProperty omokProperty = minigameRoot?["Omok"] as WzSubProperty;
            if (omokProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.MiniRoom,
                    "Mini Room",
                    "Fallback owner for Omok and Match Cards social-room parity.",
                    position);
            }


            SocialRoomRuntime runtime = SocialRoomRuntime.CreateMiniRoomSample();

            SocialRoomWindow window = CreateSocialRoomWindow(omokProperty, basicImage, soundUIImage, device, position, MapSimulatorWindowNames.MiniRoom, runtime);
            ConfigureMiniRoomOmokAssets(window, omokProperty, device);
            if (window == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.MiniRoom,
                    "Mini Room",
                    "Fallback owner for Omok and Match Cards social-room parity.",
                    position);
            }


            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            WzSubProperty commonProperty = minigameRoot?["Common"] as WzSubProperty;
            window.BindButton(LoadButton(commonProperty, "btReady", clickSound, overSound, device), runtime.ToggleMiniRoomGuestReady);
            window.BindButton(LoadButton(commonProperty, "btStart", clickSound, overSound, device), runtime.StartMiniRoomSession);
            window.BindButton(LoadButton(commonProperty, "btDraw", clickSound, overSound, device), runtime.CycleMiniRoomMode);
            window.BindButton(LoadButton(commonProperty, "btExit", clickSound, overSound, device), window.Hide);
            return window;
        }


        private static void ConfigureMiniRoomOmokAssets(SocialRoomWindow window, WzSubProperty omokProperty, GraphicsDevice device)
        {
            if (window == null || omokProperty == null)
            {
                return;
            }


            WzSubProperty stoneRoot = omokProperty["stone"] as WzSubProperty;
            Texture2D[] blackStoneFrames = Enumerable.Range(0, 12)
                .Select(index => LoadCanvasTexture(stoneRoot?[index.ToString()]?["black"] as WzSubProperty, "0", device))
                .Where(texture => texture != null)
                .ToArray();
            Texture2D[] whiteStoneFrames = Enumerable.Range(0, 12)
                .Select(index => LoadCanvasTexture(stoneRoot?[index.ToString()]?["white"] as WzSubProperty, "0", device))
                .Where(texture => texture != null)
                .ToArray();
            Texture2D blackStone = blackStoneFrames.FirstOrDefault();
            Texture2D whiteStone = whiteStoneFrames.FirstOrDefault();
            Texture2D lastBlackStone = LoadCanvasTexture(stoneRoot?["10"]?["black"] as WzSubProperty, "0", device) ?? blackStone;
            Texture2D lastWhiteStone = LoadCanvasTexture(stoneRoot?["10"]?["white"] as WzSubProperty, "0", device) ?? whiteStone;
            window.SetMiniRoomOmokStoneTextures(blackStoneFrames, whiteStoneFrames, lastBlackStone, lastWhiteStone);
        }


        private static UIWindowBase CreatePersonalShopWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty personalShopRoot = uiWindow2Image?["PersonalShop"] as WzSubProperty
                ?? uiWindow1Image?["PersonalShop"] as WzSubProperty;
            WzSubProperty shopProperty = personalShopRoot?["main"] as WzSubProperty ?? personalShopRoot;
            if (shopProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.PersonalShop,
                    "Personal Shop",
                    "Fallback owner for personal-shop social-room parity.",
                    position);
            }


            SocialRoomRuntime runtime = SocialRoomRuntime.CreatePersonalShopSample();
            SocialRoomWindow window = CreateSocialRoomWindow(shopProperty, basicImage, soundUIImage, device, position, MapSimulatorWindowNames.PersonalShop, runtime);
            if (window == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.PersonalShop,
                    "Personal Shop",
                    "Fallback owner for personal-shop social-room parity.",
                    position);
            }


            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            window.BindButton(LoadButton(shopProperty, "BtStart", clickSound, overSound, device), runtime.TogglePersonalShopOpen);
            window.BindButton(LoadButton(shopProperty, "BtArrange", clickSound, overSound, device), runtime.ArrangePersonalShopInventory);
            window.BindButton(LoadButton(shopProperty, "BtClame", clickSound, overSound, device), runtime.ClaimPersonalShopEarnings);
            window.BindButton(LoadButton(shopProperty, "BtVisit", clickSound, overSound, device), () => runtime.AddPersonalShopVisitor(null, out _));
            window.BindButton(LoadButton(shopProperty, "BtBlackList", clickSound, overSound, device), () => runtime.TogglePersonalShopBlacklist(null, out _));
            window.BindButton(LoadButton(shopProperty, "BtItem", clickSound, overSound, device), () => runtime.TryAutoListPersonalShopItem(out _));
            window.BindButton(LoadButton(shopProperty, "BtBuy", clickSound, overSound, device), () => runtime.TryBuyPersonalShopItem(-1, null, out _));
            window.BindButton(LoadButton(shopProperty, "BtClose", clickSound, overSound, device), () => runtime.ClosePersonalShop(out _));
            window.BindButton(LoadButton(shopProperty, "BtExit", clickSound, overSound, device), window.Hide);
            return window;
        }


        private static UIWindowBase CreateEntrustedShopWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty entrustedShopProperty = uiWindow2Image?["EntrustedShop"] as WzSubProperty
                ?? uiWindow1Image?["EntrustedShop"] as WzSubProperty;
            WzSubProperty memberShopProperty = uiWindow2Image?["MemberShop"] as WzSubProperty
                ?? uiWindow1Image?["MemberShop"] as WzSubProperty;
            WzSubProperty personalShopRoot = uiWindow2Image?["PersonalShop"] as WzSubProperty
                ?? uiWindow1Image?["PersonalShop"] as WzSubProperty;
            if (memberShopProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.EntrustedShop,
                    "Entrusted Shop",
                    "Fallback owner for entrusted-shop social-room parity.",
                    position);
            }


            SocialRoomRuntime runtime = SocialRoomRuntime.CreateEntrustedShopSample();
            SocialRoomWindow window = CreateSocialRoomWindow(memberShopProperty, basicImage, soundUIImage, device, position, MapSimulatorWindowNames.EntrustedShop, runtime);
            if (window == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.EntrustedShop,
                    "Entrusted Shop",
                    "Fallback owner for entrusted-shop social-room parity.",
                    position);
            }


            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            window.BindButton(LoadButton(entrustedShopProperty, "BtArrange", clickSound, overSound, device), runtime.ArrangeEntrustedShop);
            window.BindButton(LoadButton(entrustedShopProperty, "BtCoin", clickSound, overSound, device), runtime.ClaimEntrustedShopEarnings);
            window.BindButton(LoadButton(entrustedShopProperty, "BtItem", clickSound, overSound, device), () => runtime.TryAutoListEntrustedShopItem(out _));
            window.BindButton(LoadButton(memberShopProperty, "BtOK", clickSound, overSound, device), runtime.ToggleEntrustedLedgerMode);
            window.BindButton(LoadButton(memberShopProperty, "BtCancel", clickSound, overSound, device), window.Hide);
            ConfigureEntrustedChildDialog(
                window,
                runtime,
                EntrustedShopChildDialogKind.VisitList,
                personalShopRoot?["visit"] as WzSubProperty,
                basicImage,
                clickSound,
                overSound,
                device,
                parentWidth: 262,
                parentHeight: 247);
            ConfigureEntrustedChildDialog(
                window,
                runtime,
                EntrustedShopChildDialogKind.Blacklist,
                personalShopRoot?["blackList"] as WzSubProperty,
                basicImage,
                clickSound,
                overSound,
                device,
                parentWidth: 262,
                parentHeight: 247);
            WzImage uiWindowImage = global::HaCreator.Program.FindImage("ui", "UIWindow.img");
            WzSubProperty utilDlgExProperty = uiWindow2Image?["UtilDlgEx"] as WzSubProperty
                ?? uiWindowImage?["UtilDlgEx"] as WzSubProperty;
            window.RegisterEntrustedBlacklistModalAssets(
                CreateUtilDlgNoticeFrameTexture(utilDlgExProperty, uiWindowImage, uiWindow2Image, device),
                LoadButton(utilDlgExProperty, "BtOK", clickSound, overSound, device),
                LoadButton(utilDlgExProperty, "BtClose", clickSound, overSound, device),
                LoadButton(utilDlgExProperty, "BtOK", clickSound, overSound, device));
            return window;
        }

        private static void ConfigureEntrustedChildDialog(
            SocialRoomWindow window,
            SocialRoomRuntime runtime,
            EntrustedShopChildDialogKind kind,
            WzSubProperty dialogProperty,
            WzImage basicImage,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device,
            int parentWidth,
            int parentHeight)
        {
            if (window == null || runtime == null || dialogProperty == null)
            {
                return;
            }

            WzCanvasProperty backgroundProperty = dialogProperty["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = backgroundProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return;
            }

            Point offset = new Point(
                (parentWidth - frameTexture.Width) / 2,
                (parentHeight - frameTexture.Height) / 2);
            window.RegisterEntrustedChildDialog(kind, frameTexture, offset);
            AttachEntrustedChildDialogLayer(window, kind, dialogProperty, "backgrnd2", device);
            AttachEntrustedChildDialogLayer(window, kind, dialogProperty, "backgrnd3", device);

            UIObject closeButton = CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width);
            window.BindEntrustedChildDialogButton(kind, closeButton, () => runtime.CloseEntrustedChildDialog(out _));
            switch (kind)
            {
                case EntrustedShopChildDialogKind.VisitList:
                    window.BindEntrustedChildDialogButton(
                        kind,
                        LoadButton(dialogProperty, "BtSaveName", clickSound, overSound, device),
                        () => runtime.TryCopySelectedEntrustedVisitName(out _),
                        snapshot => snapshot?.CanPrimaryAction == true);
                    window.BindEntrustedChildDialogButton(
                        kind,
                        LoadButton(dialogProperty, "BtOK", clickSound, overSound, device),
                        () => runtime.CloseEntrustedChildDialog(out _));
                    break;
                case EntrustedShopChildDialogKind.Blacklist:
                    window.BindEntrustedChildDialogButton(
                        kind,
                        LoadButton(dialogProperty, "BtAdd", clickSound, overSound, device),
                        () => runtime.TryAddEntrustedBlacklistEntry(null, out _),
                        snapshot => snapshot?.CanPrimaryAction == true);
                    window.BindEntrustedChildDialogButton(
                        kind,
                        LoadButton(dialogProperty, "BtDelete", clickSound, overSound, device),
                        () => runtime.TryDeleteSelectedEntrustedBlacklistEntry(out _),
                        snapshot => snapshot?.CanSecondaryAction == true);
                    window.BindEntrustedChildDialogButton(
                        kind,
                        LoadButton(dialogProperty, "BtOK", clickSound, overSound, device),
                        () => runtime.CloseEntrustedChildDialog(out _));
                    break;
            }
        }

        private static void AttachEntrustedChildDialogLayer(
            SocialRoomWindow window,
            EntrustedShopChildDialogKind kind,
            WzSubProperty sourceProperty,
            string canvasName,
            GraphicsDevice device)
        {
            if (window == null || sourceProperty == null)
            {
                return;
            }

            IDXObject layer = LoadWindowCanvasLayerWithOffset(sourceProperty, canvasName, device, out Point offset);
            if (layer != null)
            {
                window.AddEntrustedChildDialogLayer(kind, layer, offset);
            }
        }


        private static UIWindowBase CreateTradingRoomWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty tradeProperty = uiWindow2Image?["TradingRoom"] as WzSubProperty
                ?? uiWindow1Image?["TradingRoom"] as WzSubProperty;
            if (tradeProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.TradingRoom,
                    "Trading Room",
                    "Fallback owner for trading-room social parity.",
                    position);
            }


            SocialRoomRuntime runtime = SocialRoomRuntime.CreateTradingRoomSample();
            SocialRoomWindow window = CreateSocialRoomWindow(tradeProperty, basicImage, soundUIImage, device, position, MapSimulatorWindowNames.TradingRoom, runtime);
            if (window == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.TradingRoom,
                    "Trading Room",
                    "Fallback owner for trading-room social parity.",
                    position);
            }

            AttachCanvasLayer(window, tradeProperty, "backgrnd2", device);
            AttachCanvasLayer(window, tradeProperty, "backgrnd3", device);

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject tradeButton = LoadButton(tradeProperty, "BtTrade", clickSound, overSound, device);
            UIObject resetButton = LoadButton(tradeProperty, "BtReset", clickSound, overSound, device);
            UIObject coinButton = LoadButton(tradeProperty, "BtCoin", clickSound, overSound, device);
            UIObject acceptButton = LoadButton(tradeProperty, "BtClame", clickSound, overSound, device);
            window.BindButton(tradeButton, () => runtime.TryApplyTradingRoomLocalTradeRequest(out _, out _));
            window.BindButton(resetButton, runtime.ResetTrade);
            window.BindButton(coinButton, runtime.IncreaseTradeOffer);
            window.BindButton(acceptButton, () => runtime.ToggleTradeAcceptance(out _));
            window.RegisterTradingRoomButtons(tradeButton, resetButton, coinButton, acceptButton);
            return window;
        }


        private static UIWindowBase CreateCashTradingRoomWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty tradeProperty = uiWindow2Image?["CashTradingRoom"] as WzSubProperty
                ?? uiWindow1Image?["CashTradingRoom"] as WzSubProperty;
            if (tradeProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.CashTradingRoom,
                    "Cash Trading Room",
                    "Fallback owner for cash-trading-room parity.",
                    position);
            }

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;

            Texture2D frameTexture = LoadCanvasTexture(tradeProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.CashTradingRoom,
                    "Cash Trading Room",
                    "Fallback owner for cash-trading-room parity.",
                    position);
            }

            CashTradingRoomWindow window = new(new DXObject(0, 0, frameTexture, 0))
            {
                Position = position
            };
            AttachCanvasLayer(window, tradeProperty, "backgrnd2", device);
            AttachCanvasLayer(window, tradeProperty, "backgrnd3", device);

            window.BindButton(LoadButton(tradeProperty, "BtTrade", clickSound, overSound, device), window.ToggleTradeLock);
            window.BindButton(LoadButton(tradeProperty, "BtReset", clickSound, overSound, device), window.ResetTrade);
            window.BindButton(LoadButton(tradeProperty, "BtCoin", clickSound, overSound, device), window.IncreaseTradeOffer);
            window.BindButton(LoadButton(tradeProperty, "BtClame", clickSound, overSound, device), window.ToggleTradeAcceptance);
            window.BindButton(LoadButton(tradeProperty, "BtEnter", clickSound, overSound, device), window.SubmitChatEntry);

            return window;
        }

        private static UIWindowBase CreateCashShopStageChildWindow(
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position,
            string windowName)
        {
            Texture2D frameTexture = CreateFilledTexture(device, 800, 600, Color.Transparent, Color.Transparent);
            CashShopStageChildWindow window = new(
                new DXObject(0, 0, frameTexture, 0),
                windowName,
                ResolveCashShopStageChildTitle(windowName))
            {
                Position = position
            };

            WzImage cashShopImage = global::HaCreator.Program.FindImage("ui", "CashShop.img");
            WzImage itcImage = global::HaCreator.Program.FindImage("ui", "ITC.img");
            WzImage picturePlateImage = cashShopImage;
            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;

            switch (windowName)
            {
                case MapSimulatorWindowNames.CashShopLocker:
                    window.SetContentBounds(new Rectangle(0, 318, 256, 104));
                    RegisterCashShopStageChildButton(window, (cashShopImage?["CSLocker"] as WzSubProperty), "BtRebate", clickSound, overSound, device, 170, 24, "CCSWnd_Locker previewed the rebate button owner.");
                    RegisterCashShopStageChildButton(window, (cashShopImage?["CSLocker"] as WzSubProperty), "BtRebate2", clickSound, overSound, device, 170, 51, "CCSWnd_Locker previewed the secondary rebate button owner.");
                    RegisterCashShopStageChildButton(window, (cashShopImage?["CSLocker"] as WzSubProperty), "BtRefund", clickSound, overSound, device, 170, 78, "CCSWnd_Locker previewed the refund button owner.");
                    window.SetFallbackLines(
                        "CCSWnd_Locker owns the shared cash locker selector and its vertical scrollbar.",
                        "The simulator keeps this owner separate from the catalog surface so locker actions no longer live only inside the parent stage summary.");
                    break;
                case MapSimulatorWindowNames.CashShopInventory:
                    window.SetContentBounds(new Rectangle(0, 426, 246, 163));
                    RegisterCashShopStageChildButton(window, (cashShopImage?["CSInventory"] as WzSubProperty), "BtExEquip", clickSound, overSound, device, 176, 27, "CCSWnd_Inventory switched to the Equip tab.");
                    RegisterCashShopStageChildButton(window, (cashShopImage?["CSInventory"] as WzSubProperty), "BtExConsume", clickSound, overSound, device, 176, 54, "CCSWnd_Inventory switched to the Use tab.");
                    RegisterCashShopStageChildButton(window, (cashShopImage?["CSInventory"] as WzSubProperty), "BtExInstall", clickSound, overSound, device, 176, 81, "CCSWnd_Inventory switched to the Setup tab.");
                    RegisterCashShopStageChildButton(window, (cashShopImage?["CSInventory"] as WzSubProperty), "BtExEtc", clickSound, overSound, device, 176, 108, "CCSWnd_Inventory switched to the Etc tab.");
                    RegisterCashShopStageChildButton(window, (cashShopImage?["CSInventory"] as WzSubProperty), "BtExTrunk", clickSound, overSound, device, 176, 135, "CCSWnd_Inventory switched to the locker-access button.");
                    window.SetFallbackLines(
                        "CCSWnd_Inventory now exists as its own owner with the recovered tab-button family.",
                        "Its scrollbar and number-font setup stay separate from the parent cash dialog.");
                    break;
                case MapSimulatorWindowNames.CashShopList:
                    window.SetContentBounds(new Rectangle(275, 95, 412, 430));
                    AttachCanvasLayer(window, cashShopImage?["CSList"] as WzSubProperty, "Base", device, new Point(279, 99));
                    RegisterCashShopStageChildButton(window, (cashShopImage?["CSList"] as WzSubProperty), "BtBuy", clickSound, overSound, device, 442, 364, "CCSWnd_List previewed the direct-buy button owner.");
                    RegisterCashShopStageChildButton(window, (cashShopImage?["CSList"] as WzSubProperty), "BtGift", clickSound, overSound, device, 505, 364, "CCSWnd_List previewed the gift button owner.");
                    RegisterCashShopStageChildButton(window, (cashShopImage?["CSList"] as WzSubProperty), "BtReserve", clickSound, overSound, device, 568, 364, "CCSWnd_List previewed the reserve button owner.");
                    RegisterCashShopStageChildButton(window, (cashShopImage?["CSList"] as WzSubProperty), "BtRemove", clickSound, overSound, device, 631, 364, "CCSWnd_List previewed the remove button owner.");
                    window.SetFallbackLines(
                        "CCSWnd_List owns category/page state, selector focus, and the list plate canvases.",
                        "The dedicated owner now exists apart from the coarse parent shop surface.");
                    break;
                case MapSimulatorWindowNames.CashShopStatus:
                    window.SetContentBounds(new Rectangle(254, 530, 545, 56));
                    RegisterCashShopStageChildButton(window, (cashShopImage?["CSStatus"] as WzSubProperty), "BtCharge", clickSound, overSound, device, 248, 13, "CCSWnd_Status previewed the charge button owner.");
                    RegisterCashShopStageChildButton(window, (cashShopImage?["CSStatus"] as WzSubProperty), "BtCheck", clickSound, overSound, device, 289, 13, "CCSWnd_Status previewed the check-balance button owner.");
                    RegisterCashShopStageChildButton(window, (cashShopImage?["CSStatus"] as WzSubProperty), "BtCoupon", clickSound, overSound, device, 330, 13, "CCSWnd_Status previewed the coupon button owner.");
                    RegisterCashShopStageChildButton(window, (cashShopImage?["CSStatus"] as WzSubProperty), "BtExit", clickSound, overSound, device, 378, 15, "CCSWnd_Status previewed the exit button owner.");
                    window.SetFallbackLines(
                        "CCSWnd_Status now exists as a dedicated owner with charge, check, coupon, and exit buttons.",
                        "The cash-balance strip is no longer represented only by the parent stage summary.");
                    break;
                case MapSimulatorWindowNames.CashShopOneADay:
                    window.SetContentBounds(new Rectangle(275, 95, 412, 430));
                    WzSubProperty oneADayProperty = ResolveUiSubPropertyFromSegments("OneADay.img", "CSOneADay");
                    AttachCanvasLayer(window, oneADayProperty, "Base01", device, new Point(275, 95), useOneADayVisibility: true);
                    AttachCanvasLayer(window, oneADayProperty, "ItemBox", device, new Point(275, 95), useOneADayVisibility: true);
                    AttachCanvasLayer(window, oneADayProperty, "ItemBoxBig", device, new Point(275, 95), useOneADayVisibility: true);
                    AttachCanvasLayer(window, picturePlateImage?["PicturePlate"] as WzSubProperty, "NoItem", device, new Point(279, 152), useOneADayVisibility: true);
                    AttachCanvasLayer(window, picturePlateImage?["PicturePlate"] as WzSubProperty, "NoItem0", device, new Point(279, 152), useOneADayVisibility: true);
                    AttachCanvasLayer(window, picturePlateImage?["PicturePlate"] as WzSubProperty, "NoItem1", device, new Point(279, 152), useOneADayVisibility: true);
                    AttachCanvasLayer(window, picturePlateImage?["PicturePlate"] as WzSubProperty, "ShortcutHelp", device, new Point(279, 152), useOneADayVisibility: true);
                    RegisterCashShopStageChildButton(window, oneADayProperty, "BtBuy", clickSound, overSound, device, 560, 346, "CCSWnd_OneADay previewed the dedicated buy button owner.");
                    RegisterCashShopStageChildButton(window, oneADayProperty, "BtItemBox", clickSound, overSound, device, 516, 346, "CCSWnd_OneADay previewed the dedicated item-box button owner.");
                    RegisterCashShopStageChildButton(window, (picturePlateImage?["PicturePlate"] as WzSubProperty), "BtJoin", clickSound, overSound, device, 560, 346, "CCSWnd_OneADay previewed the join button owner.");
                    RegisterCashShopStageChildButton(window, (picturePlateImage?["PicturePlate"] as WzSubProperty), "BtShortcut", clickSound, overSound, device, 516, 346, "CCSWnd_OneADay previewed the shortcut button owner.");
                    RegisterCashShopStageChildButton(window, (picturePlateImage?["PicturePlate"] as WzSubProperty), "BtClose", clickSound, overSound, device, 670, 104, "CCSWnd_OneADay previewed the close button owner.");
                    window.SetFallbackLines(
                        "CCSWnd_OneADay now exists as a dedicated list/plate owner instead of staying folded into the parent shop.",
                        "It tracks the packet-owned one-a-day state separately even when no daily reward is pending.");
                    break;
                case MapSimulatorWindowNames.ItcCharacter:
                    window.SetContentBounds(new Rectangle(0, 0, 256, 200));
                    AttachCanvasLayer(window, (itcImage?["Base"]?["Preview"] as WzSubProperty), "0", device, new Point(20, 56));
                    window.SetFallbackLines(
                        "CITCWnd_Char is separated from the trade list just like the client-owned ITC stage.",
                        "The owner now uses ui/ITC.img/Base/Preview instead of staying text-only.");
                    break;
                case MapSimulatorWindowNames.ItcSale:
                    window.SetContentBounds(new Rectangle(0, 200, 256, 110));
                    AttachCanvasLayer(window, itcImage?["Sell"] as WzSubProperty, "backgrnd1", device, new Point(0, 200));
                    RegisterCashShopStageChildButton(window, (itcImage?["Sell"] as WzSubProperty), "BtShoppingBasket", clickSound, overSound, device, 150, 287, "CITCWnd_Sale opened the shopping-basket owner path.");
                    RegisterCashShopStageChildButton(window, (itcImage?["Sell"] as WzSubProperty), "BtBuy", clickSound, overSound, device, 190, 287, "CITCWnd_Sale staged the selected sale row for direct purchase.");
                    window.SetFallbackLines(
                        "CITCWnd_Sale remains a dedicated owner instead of being merged into the broader MTS shell.",
                        "The owner now uses ui/ITC.img/Sell chrome instead of the generic fallback pane.");
                    break;
                case MapSimulatorWindowNames.ItcPurchase:
                    window.SetContentBounds(new Rectangle(0, 310, 256, 108));
                    AttachCanvasLayer(window, itcImage?["Buy"] as WzSubProperty, "backgrnd2", device, new Point(0, 310));
                    RegisterCashShopStageChildButton(window, (itcImage?["Buy"] as WzSubProperty), "BtRegistration", clickSound, overSound, device, 120, 394, "CITCWnd_Purchase armed the registration-owner flow.");
                    RegisterCashShopStageChildButton(window, (itcImage?["Buy"] as WzSubProperty), "BtSell", clickSound, overSound, device, 206, 394, "CITCWnd_Purchase switched focus back to the sell-owner path.");
                    window.SetFallbackLines(
                        "CITCWnd_Purchase stays distinct from the sale owner.",
                        "Purchase-side result flow and selection state now sit on ui/ITC.img/Buy chrome.");
                    break;
                case MapSimulatorWindowNames.ItcInventory:
                    window.SetContentBounds(new Rectangle(0, 418, 256, 180));
                    AttachCanvasLayer(window, (itcImage?["Buy"]?["backgrnd4"] as WzSubProperty), "0", device, new Point(0, 418));
                    window.SetFallbackLines(
                        "CITCWnd_Inventory now exists as its own owner under CITC.",
                        "The inventory seam now uses a dedicated ITC-authored chrome panel instead of only text fallback.");
                    break;
                case MapSimulatorWindowNames.ItcTab:
                    window.SetContentBounds(new Rectangle(272, 17, 509, 78));
                    AttachCanvasLayer(window, itcImage?["Tab"] as WzSubProperty, "1", device, new Point(272, 17));
                    window.SetFallbackLines(
                        "CITCWnd_Tab owns the category strip independently from the list and subtab panes.",
                        "This keeps ITC category resets inside the stage-owned family using ui/ITC.img/Tab.");
                    break;
                case MapSimulatorWindowNames.ItcSubTab:
                    window.SetContentBounds(new Rectangle(273, 98, 509, 48));
                    AttachCanvasLayer(window, (itcImage?["Sell"]?["Tab"] as WzSubProperty), "0", device, new Point(273, 98));
                    RegisterCashShopStageChildButton(window, itcImage, "BtSearch", clickSound, overSound, device, 742, 110, "CITCWnd_SubTab opened the dedicated ITC search path.");
                    window.SetFallbackLines(
                        "CITCWnd_SubTab keeps search and sort state separate from the main list.",
                        "The simulator now tracks this owner as part of the staged ITC layout with WZ-backed tab chrome.");
                    break;
                case MapSimulatorWindowNames.ItcList:
                    window.SetContentBounds(new Rectangle(273, 145, 509, 365));
                    AttachCanvasLayer(window, itcImage?["Sell"] as WzSubProperty, "backgrnd", device, new Point(273, 145));
                    RegisterCashShopStageChildButton(window, (itcImage?["MyPage"] as WzSubProperty), "BtBuy", clickSound, overSound, device, 574, 487, "CITCWnd_List staged the selected row for immediate buyout.");
                    RegisterCashShopStageChildButton(window, (itcImage?["MyPage"] as WzSubProperty), "BtDelete", clickSound, overSound, device, 612, 487, "CITCWnd_List staged the selected row for list removal.");
                    RegisterCashShopStageChildButton(window, (itcImage?["MyPage"] as WzSubProperty), "BtCancel", clickSound, overSound, device, 650, 487, "CITCWnd_List cancelled the current staged list action.");
                    RegisterCashShopStageChildButton(window, (itcImage?["MyPage"] as WzSubProperty), "BtBuy1", clickSound, overSound, device, 688, 487, "CITCWnd_List switched to the alternate buy-confirm owner.");
                    window.SetFallbackLines(
                        "CITCWnd_List owns the current page of normal-item results.",
                        "Packet 412 now lands on the dedicated ITC list owner path with ui/ITC.img list chrome.");
                    break;
                case MapSimulatorWindowNames.ItcStatus:
                    window.SetContentBounds(new Rectangle(255, 531, 545, 56));
                    RegisterCashShopStageChildButton(window, itcImage, "BtCharge", clickSound, overSound, device, 254, 535, "CITCWnd_Status previewed the charge button owner.");
                    RegisterCashShopStageChildButton(window, itcImage, "BtCheck", clickSound, overSound, device, 295, 535, "CITCWnd_Status previewed the check-balance button owner.");
                    RegisterCashShopStageChildButton(window, itcImage, "BtExit", clickSound, overSound, device, 379, 536, "CITCWnd_Status previewed the exit button owner.");
                    window.SetFallbackLines(
                        "CITCWnd_Status now exists as a dedicated balance and status owner.",
                        "Charge and query-cash results remain attached to CITC rather than generic field UI.");
                    break;
            }

            return window;
        }

        private static void RegisterCashShopStageChildButton(
            CashShopStageChildWindow window,
            WzSubProperty parent,
            string buttonName,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device,
            int x,
            int y,
            string actionMessage)
        {
            UIObject button = LoadButton(parent, buttonName, clickSound, overSound, device);
            if (button == null)
            {
                return;
            }

            button.X = x;
            button.Y = y;
            window.RegisterButton(button, buttonName, () => actionMessage);
        }

        private static void RegisterCashShopStageChildButton(
            CashShopStageChildWindow window,
            WzImage parent,
            string buttonName,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device,
            int x,
            int y,
            string actionMessage)
        {
            UIObject button = LoadButton(parent, buttonName, clickSound, overSound, device);
            if (button == null)
            {
                return;
            }

            button.X = x;
            button.Y = y;
            window.RegisterButton(button, buttonName, () => actionMessage);
        }

        private static string ResolveCashShopStageChildTitle(string windowName)
        {
            return windowName switch
            {
                MapSimulatorWindowNames.CashShopLocker => "CCSWnd_Locker",
                MapSimulatorWindowNames.CashShopInventory => "CCSWnd_Inventory",
                MapSimulatorWindowNames.CashShopList => "CCSWnd_List",
                MapSimulatorWindowNames.CashShopStatus => "CCSWnd_Status",
                MapSimulatorWindowNames.CashShopOneADay => "CCSWnd_OneADay",
                MapSimulatorWindowNames.ItcCharacter => "CITCWnd_Char",
                MapSimulatorWindowNames.ItcSale => "CITCWnd_Sale",
                MapSimulatorWindowNames.ItcPurchase => "CITCWnd_Purchase",
                MapSimulatorWindowNames.ItcInventory => "CITCWnd_Inventory",
                MapSimulatorWindowNames.ItcTab => "CITCWnd_Tab",
                MapSimulatorWindowNames.ItcSubTab => "CITCWnd_SubTab",
                MapSimulatorWindowNames.ItcList => "CITCWnd_List",
                MapSimulatorWindowNames.ItcStatus => "CITCWnd_Status",
                _ => "Cash Shop Child"
            };
        }


        private static UIWindowBase CreateCashAvatarPreviewWindow(
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzImage cashShopImage = global::HaCreator.Program.FindImage("ui", "CashShop.img");
            WzSubProperty charProperty = cashShopImage?["CSChar"] as WzSubProperty;
            WzSubProperty previewProperty = cashShopImage?["Base"]?["Preview"] as WzSubProperty;
            if (charProperty == null || previewProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.CashAvatarPreview,
                    "Cash Avatar Preview",
                    "Fallback owner because Cash Shop preview assets were unavailable.",
                    position);
            }

            Texture2D[] previewBackgrounds =
            {
                LoadCanvasTexture(previewProperty, "0", device),
                LoadCanvasTexture(previewProperty, "1", device),
                LoadCanvasTexture(previewProperty, "2", device)
            };

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            CashAvatarPreviewWindow window = new(
                device,
                windowBackgroundLayer: null,
                windowBackgroundOffset: Point.Zero,
                windowOverlayLayer: null,
                windowOverlayOffset: Point.Zero,
                windowContentLayer: null,
                windowContentOffset: Point.Zero,
                previewBackgrounds,
                LoadButton(charProperty, "BtBuyAvatar", clickSound, overSound, device),
                LoadButton(charProperty, "BtDefaultAvatar", clickSound, overSound, device),
                LoadButton(charProperty, "BtTakeoffAvatar", clickSound, overSound, device))
            {
                Position = position
            };
            return window;
        }


        private static SocialRoomWindow CreateSocialRoomWindow(
            WzSubProperty sourceProperty,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position,
            string windowName,
            SocialRoomRuntime runtime)
        {
            WzCanvasProperty backgroundProperty = sourceProperty?["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = backgroundProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return null;
            }


            SocialRoomWindow window = new SocialRoomWindow(
                new DXObject(0, 0, frameTexture, 0),
                windowName,
                device,
                CreateSolidTexture(device, Color.White),
                runtime)
            {
                Position = position
            };


            AttachCanvasLayer(window, sourceProperty, "backgrnd2", device);
            AttachCanvasLayer(window, sourceProperty, "backgrnd3", device);
            AttachCanvasLayer(window, sourceProperty, "backgrnd4", device);
            AttachCanvasLayer(window, sourceProperty, "backgrnd5", device);
            AttachCanvasLayer(window, sourceProperty, "backgrnd6", device);


            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject closeButton = CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width);
            window.InitializeCloseButton(closeButton);
            return window;
        }


        private static void AttachCanvasLayer(SocialRoomWindow window, WzSubProperty sourceProperty, string canvasName, GraphicsDevice device)
        {
            if (window == null || sourceProperty == null)
            {
                return;
            }


            IDXObject layer = LoadWindowCanvasLayerWithOffset(sourceProperty, canvasName, device, out Point offset);
            if (layer != null)
            {
                window.AddLayer(layer, offset);
            }
        }

        private static void AttachCanvasLayer(CashTradingRoomWindow window, WzSubProperty sourceProperty, string canvasName, GraphicsDevice device)
        {
            if (window == null || sourceProperty == null)
            {
                return;
            }

            IDXObject layer = LoadWindowCanvasLayerWithOffset(sourceProperty, canvasName, device, out Point offset);
            if (layer != null)
            {
                window.AddLayer(layer, offset);
            }
        }

        private static void AttachCanvasLayer(
            CashShopStageChildWindow window,
            WzSubProperty sourceProperty,
            string canvasName,
            GraphicsDevice device,
            Point baseOffset,
            bool useOneADayVisibility = false)
        {
            if (window == null || sourceProperty == null)
            {
                return;
            }

            IDXObject layer = LoadWindowCanvasLayerWithOffset(sourceProperty, canvasName, device, out Point offset);
            if (layer != null)
            {
                Func<bool> visibilityEvaluator = null;
                if (useOneADayVisibility)
                {
                    visibilityEvaluator = () => window.IsOneADayLayerVisible(canvasName);
                }

                window.AddLayer(layer, new Point(baseOffset.X + offset.X, baseOffset.Y + offset.Y), canvasName, visibilityEvaluator);
            }
        }


        private static UIWindowBase CreateItemMakerWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position,
            IInventoryRuntime inventory)
        {
            WzSubProperty sourceProperty = uiWindow2Image?["Maker"] as WzSubProperty
                ?? uiWindow1Image?["Maker"] as WzSubProperty;
            if (sourceProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.ItemMaker,
                    "Item Maker",
                    "Fallback owner for the dedicated crafting and recipe interaction window.",
                    position);
            }


            WzCanvasProperty background = sourceProperty["backgrnd"] as WzCanvasProperty;
            if (background == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.ItemMaker,
                    "Item Maker",
                    "Fallback owner for the dedicated crafting and recipe interaction window.",
                    position);
            }


            Texture2D frameTexture = background.GetLinkedWzCanvasBitmap().ToTexture2DAndDispose(device);

            Texture2D pixel = new Texture2D(device, 1, 1);

            pixel.SetData(new[] { Color.White });



            ItemMakerUI itemMaker = new ItemMakerUI(new DXObject(0, 0, frameTexture, 0), pixel)
            {
                Position = position
            };
            itemMaker.SetInventory(inventory);


            Texture2D overlay = LoadCanvasTexture(sourceProperty, "backgrnd2", device);
            Texture2D header = LoadCanvasTexture(sourceProperty, "backgrnd3", device);
            Texture2D innerOverlay = LoadCanvasTexture(sourceProperty, "backgrnd4", device);
            if (overlay != null)
            {
                itemMaker.AddBackgroundLayer(new DXObject(0, 0, overlay, 0), ResolveCanvasOffset(sourceProperty["backgrnd2"] as WzCanvasProperty));
            }
            if (header != null)
            {
                itemMaker.AddBackgroundLayer(new DXObject(0, 0, header, 0), ResolveCanvasOffset(sourceProperty["backgrnd3"] as WzCanvasProperty));
            }
            if (innerOverlay != null)
            {
                itemMaker.AddBackgroundLayer(new DXObject(0, 0, innerOverlay, 0), ResolveCanvasOffset(sourceProperty["backgrnd4"] as WzCanvasProperty));
            }


            WzSubProperty gaugeBarProperty = sourceProperty["GaugeBar"] as WzSubProperty;
            if (gaugeBarProperty != null)
            {
                Texture2D gaugeBar = LoadCanvasTexture(gaugeBarProperty, "bar", device);
                Texture2D gaugeFill = LoadCanvasTexture(gaugeBarProperty, "gauge", device);
                Point gaugeOffset = ResolveCanvasOffset(gaugeBarProperty["bar"] as WzCanvasProperty);
                itemMaker.SetGaugeTextures(gaugeBar, gaugeFill, gaugeOffset);
            }


            WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];
            WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];
            UIObject startButton = LoadButton(sourceProperty, "BtStart", btClickSound, btOverSound, device);
            UIObject cancelButton = LoadButton(sourceProperty, "BtCancel", btClickSound, btOverSound, device);
            UIObject pageCycleButton = LoadButton(sourceProperty, "BtDown1", btClickSound, btOverSound, device);
            itemMaker.InitializeControls(startButton, cancelButton, pageCycleButton);


            WzSubProperty closeButtonProperty = (WzSubProperty)basicImage?["BtClose"];
            if (closeButtonProperty != null)
            {
                try
                {
                    UIObject closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = frameTexture.Width - closeBtn.CanvasSnapshotWidth - 8;
                    closeBtn.Y = 8;
                    itemMaker.InitializeCloseButton(closeBtn);
                }
                catch
                {
                }
            }


            return itemMaker;

        }



        private static UIWindowBase CreateBookCollectionWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty bookProperty = uiWindow2Image?["Book"] as WzSubProperty
                ?? uiWindow1Image?["Book"] as WzSubProperty;
            WzSubProperty monsterBookProperty = uiWindow2Image?["MonsterBook"] as WzSubProperty
                ?? uiWindow1Image?["MonsterBook"] as WzSubProperty;
            if (monsterBookProperty == null && bookProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.BookCollection,
                    "Collection Book",
                    "Fallback owner for the dedicated Book collection surface.",
                    position);
            }

            WzCanvasProperty background = monsterBookProperty?["backgrnd"] as WzCanvasProperty
                ?? bookProperty?["backgrnd"] as WzCanvasProperty;
            if (background == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.BookCollection,
                    "Collection Book",
                    "Fallback owner for the dedicated Book collection surface.",
                    position);
            }


            Texture2D frameTexture = background.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return null;
            }


            Texture2D pixel = new Texture2D(device, 1, 1);
            pixel.SetData(new[] { Color.White });
            BookCollectionWindow bookCollection = new BookCollectionWindow(
                new DXObject(0, 0, frameTexture, 0),
                pixel,
                device)
            {
                Position = position
            };

            WzSubProperty utilDialogProperty = uiWindow2Image?["UtilDlgEx"] as WzSubProperty
                ?? uiWindow1Image?["UtilDlgEx"] as WzSubProperty;
            bookCollection.SetPageMarkerTextures(
                LoadCanvasTexture(utilDialogProperty, "dot0", device),
                LoadCanvasTexture(utilDialogProperty, "dot1", device));
            bookCollection.SetPageRuleTexture(LoadCanvasTexture(utilDialogProperty, "line", device));

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;

            bookCollection.SetMonsterBookArt(
                LoadCanvasTexture(monsterBookProperty, "cardSlot", device),
                LoadCanvasTexture(monsterBookProperty, "infoPage", device),
                LoadCanvasTexture(monsterBookProperty, "cover", device),
                LoadCanvasTexture(monsterBookProperty, "select", device),
                LoadCanvasTexture(monsterBookProperty, "fullMark", device));

            WzSubProperty leftTabInfoProperty = monsterBookProperty?["LeftTabInfo"]?["0"] as WzSubProperty;
            WzSubProperty leftTabProperty = monsterBookProperty?["LeftTab"] as WzSubProperty;
            WzSubProperty rightTabProperty = monsterBookProperty?["RightTab"] as WzSubProperty;
            WzSubProperty iconProperty = monsterBookProperty?["icon"] as WzSubProperty;
            bookCollection.SetMonsterBookTabArt(
                LoadButtonStateTexture(leftTabInfoProperty, "normal", device),
                LoadButtonStateTexture(leftTabInfoProperty, "selected", device),
                LoadIndexedMonsterBookTabTextures(leftTabProperty, "normal", device),
                LoadIndexedMonsterBookTabTextures(leftTabProperty, "selected", device),
                LoadIndexedMonsterBookTabTextures(leftTabProperty, "mouseOver", device),
                LoadIndexedCanvasTextureList(iconProperty, device),
                LoadIndexedMonsterBookTabTextures(rightTabProperty, "normal", device),
                LoadIndexedMonsterBookTabTextures(rightTabProperty, "selected", device),
                LoadIndexedMonsterBookTabTextures(rightTabProperty, "mouseOver", device),
                LoadIndexedMonsterBookTabTextures(rightTabProperty, "disabled", device),
                LoadMonsterBookRightTabOrder(rightTabProperty));

            WzSubProperty contextMenuProperty = monsterBookProperty?["ContextMenu"] as WzSubProperty;
            bookCollection.SetMonsterBookContextMenuArt(
                LoadCanvasTexture(contextMenuProperty, "t", device),
                LoadCanvasTexture(contextMenuProperty, "c", device),
                LoadCanvasTexture(contextMenuProperty, "s", device));

            UIObject prevButton = LoadButton(monsterBookProperty, "arrowLeft", btClickSound, btOverSound, device)
                ?? LoadButton(bookProperty, "BtPrev", btClickSound, btOverSound, device);
            UIObject nextButton = LoadButton(monsterBookProperty, "arrowRight", btClickSound, btOverSound, device)
                ?? LoadButton(bookProperty, "BtNext", btClickSound, btOverSound, device);
            UIObject closeButton = LoadButton(monsterBookProperty, "BtClose", btClickSound, btOverSound, device)
                ?? LoadButton(bookProperty, "BtClose", btClickSound, btOverSound, device);
            UIObject searchButton = LoadButton(monsterBookProperty, "BtSearch", btClickSound, btOverSound, device);
            UIObject registerButton = LoadButton(contextMenuProperty, "BtRegister", btClickSound, btOverSound, device);
            UIObject releaseButton = LoadButton(contextMenuProperty, "BtRelease", btClickSound, btOverSound, device);

            if (prevButton != null)

            {

                prevButton.X = 34;
                prevButton.Y = 287;
            }
            if (nextButton != null)
            {
                nextButton.X = 403;
                nextButton.Y = 287;
            }
            if (closeButton != null)
            {
                closeButton.X = 430;
                closeButton.Y = 10;

            }
            if (searchButton != null)
            {
                searchButton.X = 425;
                searchButton.Y = 299;
            }

            bookCollection.InitializeButtons(prevButton, nextButton, closeButton, searchButton);
            bookCollection.InitializeContextMenuButtons(registerButton, releaseButton);
            return bookCollection;

        }

        private static IReadOnlyList<Texture2D> LoadIndexedMonsterBookTabTextures(
            WzSubProperty tabContainer,
            string stateName,
            GraphicsDevice device)
        {
            if (tabContainer == null)
            {
                return Array.Empty<Texture2D>();
            }

            List<Texture2D> textures = new();
            foreach (WzImageProperty property in tabContainer.WzProperties)
            {
                if (property is not WzSubProperty tabProperty)
                {
                    continue;
                }

                textures.Add(LoadButtonStateTexture(tabProperty, stateName, device));
            }

            return textures;
        }

        private static IReadOnlyList<MonsterBookDetailTab> LoadMonsterBookRightTabOrder(WzSubProperty rightTabProperty)
        {
            if (rightTabProperty == null)
            {
                return new[]
                {
                    MonsterBookDetailTab.BasicInfo,
                    MonsterBookDetailTab.Episode,
                    MonsterBookDetailTab.Rewards,
                    MonsterBookDetailTab.Habitat
                };
            }

            List<MonsterBookDetailTab> tabs = new();
            foreach (WzImageProperty property in rightTabProperty.WzProperties)
            {
                tabs.Add(property.Name switch
                {
                    "0" => MonsterBookDetailTab.BasicInfo,
                    "3" => MonsterBookDetailTab.Episode,
                    "2" => MonsterBookDetailTab.Rewards,
                    "1" => MonsterBookDetailTab.Habitat,
                    _ => MonsterBookDetailTab.BasicInfo
                });
            }

            return tabs.Count > 0
                ? tabs
                : new[]
                {
                    MonsterBookDetailTab.BasicInfo,
                    MonsterBookDetailTab.Episode,
                    MonsterBookDetailTab.Rewards,
                    MonsterBookDetailTab.Habitat
                };
        }



        private static UIWindowBase CreateQuestAlarmWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty sourceProperty = uiWindow2Image?["QuestAlarm"] as WzSubProperty
                ?? uiWindow1Image?["QuestAlarm"] as WzSubProperty;
            if (sourceProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.QuestAlarm,
                    "Quest Alarm",
                    "Fallback owner for the standalone quest progress tracker surface.",
                    position);
            }


            Texture2D maxTexture = LoadCanvasTexture(sourceProperty, "backgrndmax", device);
            Texture2D centerTexture = LoadCanvasTexture(sourceProperty, "backgrndcenter", device);
            Texture2D bottomTexture = LoadCanvasTexture(sourceProperty, "backgrndbottom", device);
            Texture2D minTexture = LoadCanvasTexture(sourceProperty, "backgrndmin", device);
            if (maxTexture == null || centerTexture == null || bottomTexture == null || minTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.QuestAlarm,
                    "Quest Alarm",
                    "Fallback owner for the standalone quest progress tracker surface.",
                    position);
            }


            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            WzSubProperty questInfoProperty = uiWindow2Image?["Quest"]?["quest_info"] as WzSubProperty
                ?? uiWindow1Image?["Quest"] as WzSubProperty;


            QuestAlarmWindow window = new QuestAlarmWindow(
                MapSimulatorWindowNames.QuestAlarm,
                device,
                maxTexture,
                centerTexture,
                bottomTexture,
                minTexture)
            {
                Position = position
            };


            window.SetQuestChromeTextures(
                LoadCanvasTexture(questInfoProperty?["summary_icon"] as WzSubProperty, "select", device),
                LoadCanvasTexture(questInfoProperty?["summary_icon"] as WzSubProperty, "prob", device),
                LoadCanvasTexture(questInfoProperty?["Gauge"] as WzSubProperty, "frame", device),
                LoadCanvasTexture(questInfoProperty?["Gauge"] as WzSubProperty, "gauge", device),
                LoadCanvasTexture(questInfoProperty?["Gauge"] as WzSubProperty, "spot", device),
                LoadButtonStateTexture(sourceProperty?["BtDelete"] as WzSubProperty, "normal", device),
                LoadButtonStateTexture(sourceProperty?["BtDelete"] as WzSubProperty, "pressed", device),
                LoadButtonStateTexture(sourceProperty?["BtDelete"] as WzSubProperty, "disabled", device),
                LoadButtonStateTexture(sourceProperty?["BtDelete"] as WzSubProperty, "mouseOver", device),
                LoadQuestAlarmAnimationFrames(sourceProperty?["BtQ"] as WzSubProperty, device));


            window.InitializeControls(
                LoadButton(sourceProperty, "BtAuto", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtQ", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtMax", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtMin", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtDelete", btClickSound, btOverSound, device));
            window.SetTooltipDescription(ResolveQuestAlarmTooltipDescription());


            return window;

        }

        private static string ResolveQuestAlarmTooltipDescription()
        {
            try
            {
                WzImage tooltipHelpImage = global::HaCreator.Program.FindImage("String", "ToolTipHelp.img");
                return tooltipHelpImage?["Game"]?["Button"]?["QuestAlarm"]?["Desc"]?.GetString();
            }
            catch
            {
                return null;
            }
        }

        private static IReadOnlyList<QuestAlarmAnimationFrame> LoadQuestAlarmAnimationFrames(
            WzSubProperty buttonProperty,
            GraphicsDevice device)
        {
            LoadIndexedCanvasSequence(buttonProperty?["ani"] as WzSubProperty, device, out Texture2D[] textures, out Point[] offsets, out int[] delays);
            if (textures.Length == 0)
            {
                return Array.Empty<QuestAlarmAnimationFrame>();
            }

            List<QuestAlarmAnimationFrame> frames = new(textures.Length);
            for (int i = 0; i < textures.Length; i++)
            {
                frames.Add(new QuestAlarmAnimationFrame(
                    textures[i],
                    i < offsets.Length ? offsets[i] : Point.Zero,
                    i < delays.Length ? delays[i] : 0));
            }

            return frames;
        }

        private static CharacterSelectWindow.AnimationFrame LoadSingleAnimationFrame(
            WzSubProperty parentProperty,
            string frameName,
            GraphicsDevice device)
        {
            if (parentProperty?[frameName] is not WzSubProperty frameProperty)
            {
                return default;
            }

            List<CharacterSelectWindow.AnimationFrame> frames = LoadCharacterSelectAnimationFrames(frameProperty, device);
            return frames.Count > 0 ? frames[0] : default;
        }



        private static UIWindowBase CreateQuestDeliveryWindow(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty utilDialogProperty = uiWindow2Image?["UtilDlgEx"] as WzSubProperty;
            Texture2D frameTexture = LoadCanvasTexture(utilDialogProperty, "notice", device);
            if (frameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.QuestDelivery,
                    "Quest Delivery",
                    "Fallback owner for packet-authored quest delivery launches.",
                    position);
            }


            WzSubProperty iconProperty = utilDialogProperty?["QDeliveryIcon"] as WzSubProperty;
            int iconFrameCount = GetPropertyChildCount(iconProperty, 0);
            List<QuestDeliveryWindow.IconFrame> iconFrames = new List<QuestDeliveryWindow.IconFrame>(iconFrameCount);
            for (int i = 0; i < iconFrameCount; i++)
            {
                if (iconProperty?[i.ToString()]?.GetLinkedWzImageProperty() is not WzCanvasProperty canvas)
                {
                    continue;
                }

                Texture2D frame = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (frame != null)
                {
                    System.Drawing.PointF origin = canvas.GetCanvasOriginPosition();
                    int delay = Math.Max(1, canvas["delay"]?.GetInt() ?? 120);
                    iconFrames.Add(new QuestDeliveryWindow.IconFrame(
                        frame,
                        new Point((int)origin.X, (int)origin.Y),
                        delay));
                }
            }


            QuestDeliveryWindow window = new QuestDeliveryWindow(

                new DXObject(0, 0, frameTexture, 0),

                iconFrames.ToArray(),
                LoadCanvasTexture(utilDialogProperty, "list5", device),
                LoadCanvasTexture(utilDialogProperty, "list4", device),
                LoadCanvasTexture(utilDialogProperty, "bar", device),
                LoadCanvasTexture(utilDialogProperty, "line", device),
                device)
            {
                Position = position
            };


            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject okButton = LoadButton(utilDialogProperty, "BtOK", btClickSound, btOverSound, device);
            UIObject cancelButton = LoadButton(utilDialogProperty, "BtQNo", btClickSound, btOverSound, device)
                ?? LoadButton(utilDialogProperty, "BtClose", btClickSound, btOverSound, device);
            window.InitializeButtons(okButton, cancelButton);


            WzSubProperty closeButtonProperty = basicImage?["BtClose"] as WzSubProperty;
            if (closeButtonProperty != null)
            {
                try
                {
                    UIObject closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = frameTexture.Width - closeBtn.CanvasSnapshotWidth - 8;
                    closeBtn.Y = 8;
                    window.InitializeCloseButton(closeBtn);
                }
                catch
                {
                }
            }


            return window;

        }

        private static UIWindowBase CreateRepairDurabilityWindow(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty repairProperty = uiWindow2Image?["Repair"] as WzSubProperty;
            Texture2D frameTexture = LoadCanvasTexture(repairProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.RepairDurability,
                    "Repair",
                    "Fallback owner for packet-authored durability repair launches.",
                    position);
            }

            RepairDurabilityWindow window = new RepairDurabilityWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadCanvasTexture(repairProperty, "normal", device),
                LoadCanvasTexture(repairProperty, "select", device),
                "Repair Fee",
                device)
            {
                Position = position
            };

            WzSubProperty skillMainProperty = uiWindow2Image?["Skill"]?["main"] as WzSubProperty;
            if (skillMainProperty != null)
            {
                WzCanvasProperty tip0 = skillMainProperty["tip0"] as WzCanvasProperty;
                WzCanvasProperty tip1 = skillMainProperty["tip1"] as WzCanvasProperty;
                WzCanvasProperty tip2 = skillMainProperty["tip2"] as WzCanvasProperty;
                window.SetTooltipTextures(new[]
                {
                    LoadCanvasTexture(tip0, device),
                    LoadCanvasTexture(tip1, device),
                    LoadCanvasTexture(tip2, device)
                });
                window.SetTooltipOrigins(new[]
                {
                    tip0?.GetCanvasOriginPosition() is System.Drawing.PointF tip0Origin
                        ? new Point((int)tip0Origin.X, (int)tip0Origin.Y)
                        : Point.Zero,
                    tip1?.GetCanvasOriginPosition() is System.Drawing.PointF tip1Origin
                        ? new Point((int)tip1Origin.X, (int)tip1Origin.Y)
                        : Point.Zero,
                    tip2?.GetCanvasOriginPosition() is System.Drawing.PointF tip2Origin
                        ? new Point((int)tip2Origin.X, (int)tip2Origin.Y)
                        : Point.Zero
                });
            }

            WzSubProperty equipTooltipProperty = uiWindow2Image?["ToolTip"]?["Equip"] as WzSubProperty;
            if (equipTooltipProperty != null)
            {
                window.SetEquipTooltipAssets(new EquipUIBigBang.EquipTooltipAssets
                {
                    CanLabels = LoadCanvasTextureMap(equipTooltipProperty["Can"] as WzSubProperty, device),
                    CannotLabels = LoadCanvasTextureMap(equipTooltipProperty["Cannot"] as WzSubProperty, device),
                    PropertyLabels = LoadCanvasTextureMap(equipTooltipProperty["Property"] as WzSubProperty, device),
                    ItemCategoryLabels = LoadCanvasTextureMap(equipTooltipProperty["ItemCategory"] as WzSubProperty, device),
                    WeaponCategoryLabels = LoadCanvasTextureMap(equipTooltipProperty["WeaponCategory"] as WzSubProperty, device),
                    SpeedLabels = LoadCanvasTextureMap(equipTooltipProperty["Speed"] as WzSubProperty, device),
                    GrowthEnabledLabels = LoadCanvasTextureMap(equipTooltipProperty["GrowthEnabled"] as WzSubProperty, device),
                    GrowthDisabledLabels = LoadCanvasTextureMap(equipTooltipProperty["GrowthDisabled"] as WzSubProperty, device),
                    CashLabel = LoadCanvasTexture(equipTooltipProperty, "cash", device),
                    MesosLabel = LoadCanvasTexture(equipTooltipProperty, "mesos", device),
                    StarLabel = LoadCanvasTexture(equipTooltipProperty["Star"] as WzSubProperty, "Star", device)
                });
            }

            window.AddLayer(LoadWindowCanvasLayerWithOffset(repairProperty, "backgrnd2", device, out Point overlayOffset), overlayOffset);
            window.AddLayer(LoadWindowCanvasLayerWithOffset(repairProperty, "backgrnd3", device, out Point contentOffset), contentOffset);

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject repairAllButton = LoadButton(repairProperty, "BtAllRepair", btClickSound, btOverSound, device);
            UIObject repairButton = LoadButton(repairProperty, "BtRepair", btClickSound, btOverSound, device);
            ApplyRepairButtonOriginPosition(repairAllButton, repairProperty, "BtAllRepair");
            ApplyRepairButtonOriginPosition(repairButton, repairProperty, "BtRepair");

            UIObject closeButton = null;
            WzSubProperty basicCloseButton = basicImage?["BtClose"] as WzSubProperty;
            if (basicCloseButton != null)
            {
                try
                {
                    closeButton = new UIObject(basicCloseButton, btClickSound, btOverSound, false, Point.Zero, device)
                    {
                        X = 211,
                        Y = 6
                    };
                }
                catch
                {
                    closeButton = null;
                }
            }

            window.InitializeButtons(repairAllButton, repairButton, closeButton);
            return window;
        }

        private static void ApplyRepairButtonOriginPosition(UIObject button, WzSubProperty repairProperty, string buttonName)
        {
            if (button == null || repairProperty?[buttonName] is not WzSubProperty buttonProperty)
            {
                return;
            }

            if (buttonProperty["normal"]?["0"] is not WzCanvasProperty normalCanvas)
            {
                return;
            }

            System.Drawing.PointF origin = normalCanvas.GetCanvasOriginPosition();
            button.X = -(int)origin.X;
            button.Y = -(int)origin.Y;
        }



        private static UIWindowBase CreateClassCompetitionWindow(
            WzImage uiWindowImage,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            const int classCompetitionBackgroundStringPoolId = 0x11DA;
            const int classCompetitionLoadingStringPoolId = 0x11DB;
            const int classCompetitionOkButtonStringPoolId = 0x11DD;

            WzSubProperty classMatchProperty = uiWindowImage?["classMatch"] as WzSubProperty
                ?? uiWindow2Image?["classMatch"] as WzSubProperty;
            IDXObject frameLayer = LoadWindowCanvasLayerFromClientUiStringPoolPath(
                                   classCompetitionBackgroundStringPoolId,
                                   "UI/UIWindow.img/classMatch/backgrnd",
                                   uiWindowImage,
                                   uiWindow2Image,
                                   device,
                                   out _)
                               ?? LoadWindowCanvasLayerWithOffset(classMatchProperty, "backgrnd", device, out _);
            Texture2D frameTexture = frameLayer?.Texture as Texture2D
                ?? CreatePlaceholderWindowTexture(device, 312, 389, "Class Competition");
            var loadingFrames = new List<UtilityPanelWindow.IndicatorFrame>();
            for (int i = 0; i < 5; i++)
            {
                WzCanvasProperty loadingCanvas = classMatchProperty?["Loading"]?[i.ToString()]?.GetLinkedWzImageProperty() as WzCanvasProperty;
                Texture2D loadingFrame = (LoadWindowCanvasLayerFromClientUiStringPoolPath(
                        classCompetitionLoadingStringPoolId,
                        "UI/UIWindow.img/classMatch/Loading/{0}",
                        i,
                        uiWindowImage,
                        uiWindow2Image,
                        device,
                        out _)?.Texture as Texture2D)
                    ?? LoadCanvasTexture(classMatchProperty?["Loading"] as WzSubProperty, i.ToString(), device);
                if (loadingFrame != null)
                {
                    loadingFrames.Add(new UtilityPanelWindow.IndicatorFrame(
                        loadingFrame,
                        Math.Max(0, loadingCanvas?["delay"]?.GetInt() ?? 0)));
                }
            }

            ClassCompetitionWindow window = new ClassCompetitionWindow(
                frameLayer ?? new DXObject(0, 0, frameTexture, 0),
                loadingFrames,
                device)
            {
                Position = position
            };

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            string okButtonPath = MapleStoryStringPool.GetOrFallback(
                classCompetitionOkButtonStringPoolId,
                "UI/UIWindow.img/classMatch/BtOK");
            UIObject okButton = LoadButtonFromUiPath(okButtonPath, btClickSound, btOverSound, device, uiWindowImage, uiWindow2Image)
                ?? LoadButton(classMatchProperty, "BtOK", btClickSound, btOverSound, device)
                ?? LoadButton(uiWindow2Image?["UtilDlgEx"] as WzSubProperty, "BtOK", btClickSound, btOverSound, device);
            if (okButton != null)
            {
            }
            WzSubProperty closeButtonProperty = basicImage?["BtClose"] as WzSubProperty;
            UIObject closeBtn = null;
            if (closeButtonProperty != null)
            {
                try
                {
                    closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = frameTexture.Width - closeBtn.CanvasSnapshotWidth - 8;
                    closeBtn.Y = 8;
                }
                catch
                {
                    closeBtn = null;
                }
            }

            window.InitializeButtons(okButton);
            if (closeBtn != null)
            {
                window.InitializeCloseButton(closeBtn);
            }

            return window;

        }



        private static UIWindowBase CreateMemoMailboxWindow(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty deliveryProperty = uiWindow2Image?["Delivery"] as WzSubProperty;
            Texture2D frameTexture = LoadCanvasTexture(deliveryProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.MemoMailbox,
                    "Parcel Delivery",
                    "Fallback owner for the simulator parcel receive/send/quick-send flow.",
                    position);
            }


            WzSubProperty memoProperty = uiWindow2Image?["Memo"] as WzSubProperty;
            Texture2D unreadTexture = LoadCanvasTexture(memoProperty, "check0", device);
            Texture2D readTexture = LoadCanvasTexture(memoProperty, "check1", device);
            WzSubProperty tabEnabledProperty = deliveryProperty?["Tab"]?["enabled"] as WzSubProperty;
            WzSubProperty tabDisabledProperty = deliveryProperty?["Tab"]?["disabled"] as WzSubProperty;
            MemoMailboxWindow window = new MemoMailboxWindow(
                new DXObject(0, 0, frameTexture, 0),
                MapSimulatorWindowNames.MemoMailbox,
                device,
                unreadTexture,
                readTexture,
                new[]
                {
                    LoadCanvasTexture(tabEnabledProperty, "0", device),
                    LoadCanvasTexture(tabEnabledProperty, "1", device),
                    LoadCanvasTexture(tabEnabledProperty, "2", device)
                },
                new[]
                {
                    LoadCanvasTexture(tabDisabledProperty, "0", device),
                    LoadCanvasTexture(tabDisabledProperty, "1", device),
                    LoadCanvasTexture(tabDisabledProperty, "2", device)
                },
                LoadWindowCanvasLayerWithOffset(deliveryProperty, "backgrnd2", device, out Point windowOverlayOffset),
                windowOverlayOffset,
                LoadWindowCanvasLayerWithOffset(deliveryProperty?["Keep"] as WzSubProperty, "base", device, out Point receiveBaseOffset),
                receiveBaseOffset,
                LoadWindowCanvasLayerWithOffset(deliveryProperty?["Keep"] as WzSubProperty, "base2", device, out Point receiveDetailOffset),
                receiveDetailOffset,
                LoadWindowCanvasLayerWithOffset(deliveryProperty?["Keep"] as WzSubProperty, "text", device, out Point receiveTextOffset),
                receiveTextOffset,
                LoadWindowCanvasLayerWithOffset(deliveryProperty?["Send"] as WzSubProperty, "base", device, out Point sendBaseOffset),
                sendBaseOffset,
                LoadWindowCanvasLayerWithOffset(deliveryProperty?["Send_Info"] as WzSubProperty, "backgrnd2", device, out Point sendInfoOverlayOffset),
                sendInfoOverlayOffset,
                LoadWindowCanvasLayerWithOffset(deliveryProperty?["Send_Info"] as WzSubProperty, "backgrnd3", device, out Point sendInfoHeaderOffset),
                sendInfoHeaderOffset,
                LoadWindowCanvasLayerWithOffset(deliveryProperty?["Quick"] as WzSubProperty, "base", device, out Point quickBaseOffset),
                quickBaseOffset,
                LoadWindowCanvasLayerWithOffset(deliveryProperty?["Quick"] as WzSubProperty, "text", device, out Point quickTextOffset),
                quickTextOffset,
                LoadWindowCanvasLayerWithOffset(deliveryProperty?["Quick_Info"] as WzSubProperty, "backgrnd2", device, out Point quickInfoOverlayOffset),
                quickInfoOverlayOffset,
                LoadWindowCanvasLayerWithOffset(deliveryProperty?["Quick_Info"] as WzSubProperty, "backgrnd3", device, out Point quickInfoHeaderOffset),
                quickInfoHeaderOffset,
                LoadVerticalScrollbarSkin(basicImage?["VScr9"] as WzSubProperty, device))
            {
                Position = position
            };


            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            WzSubProperty receiveProperty = deliveryProperty?["Keep"] as WzSubProperty;
            WzSubProperty sendProperty = deliveryProperty?["Send"] as WzSubProperty;
            WzSubProperty quickProperty = deliveryProperty?["Quick"] as WzSubProperty;
            window.InitializeReceiveButtons(
                LoadButton(receiveProperty, "BtGet", btClickSound, btOverSound, device),
                LoadButton(receiveProperty, "BtDelete", btClickSound, btOverSound, device));
            window.InitializeSendButtons(
                LoadButton(sendProperty, "BtSend", btClickSound, btOverSound, device),
                LoadButton(sendProperty, "BtMeso", btClickSound, btOverSound, device),
                LoadButton(sendProperty, "BtOpen", btClickSound, btOverSound, device),
                LoadButton(sendProperty, "BtClose", btClickSound, btOverSound, device));
            window.InitializeQuickButtons(
                LoadButton(quickProperty, "BtSend", btClickSound, btOverSound, device),
                LoadButton(quickProperty, "BtMeso", btClickSound, btOverSound, device),
                LoadButton(quickProperty, "BtOpen", btClickSound, btOverSound, device),
                LoadButton(quickProperty, "BtClose", btClickSound, btOverSound, device));


            WzSubProperty closeButtonProperty = basicImage?["BtClose"] as WzSubProperty;
            if (closeButtonProperty != null)
            {
                try
                {
                    UIObject closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = frameTexture.Width - closeBtn.CanvasSnapshotWidth - 8;
                    closeBtn.Y = 8;
                    window.InitializeCloseButton(closeBtn);
                }
                catch
                {
                }
            }


            return window;

        }



        private static UIWindowBase CreateMemoSendWindow(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty memoProperty = uiWindow2Image?["Memo"]?["Send"] as WzSubProperty;
            if (memoProperty == null)
            {
                return null;
            }


            Texture2D frameTexture = LoadCanvasTexture(memoProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return null;
            }


            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;

            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;



            MemoSendWindow window = new MemoSendWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(memoProperty, "backgrnd2", device, out Point overlayOffset),
                overlayOffset,
                LoadWindowCanvasLayerWithOffset(memoProperty, "backgrnd3", device, out Point headerOffset),
                headerOffset)
            {
                Position = position
            };


            window.InitializeControls(

                LoadButton(memoProperty, "BtOK", btClickSound, btOverSound, device),

                LoadButton(memoProperty, "BtCancle", btClickSound, btOverSound, device));



            WzSubProperty closeButtonProperty = basicImage?["BtClose"] as WzSubProperty;
            if (closeButtonProperty != null)
            {
                try
                {
                    UIObject closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = frameTexture.Width - closeBtn.CanvasSnapshotWidth - 8;
                    closeBtn.Y = 8;
                    window.InitializeCloseButton(closeBtn);
                }
                catch
                {
                }
            }


            return window;

        }



        private static UIWindowBase CreateMemoGetWindow(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty deliveryInfoProperty = uiWindow2Image?["Delivery"]?["Keep_Info"] as WzSubProperty;
            WzSubProperty deliveryKeepProperty = uiWindow2Image?["Delivery"]?["Keep"] as WzSubProperty;
            if (deliveryInfoProperty == null || deliveryKeepProperty == null)
            {
                return null;
            }


            Texture2D frameTexture = LoadCanvasTexture(deliveryInfoProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return null;
            }


            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            MemoGetWindow window = new MemoGetWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(deliveryInfoProperty, "backgrnd2", device, out Point overlayOffset),
                overlayOffset,
                LoadWindowCanvasLayerWithOffset(deliveryInfoProperty, "backgrnd3", device, out Point headerOffset),
                headerOffset,
                null,
                Point.Zero,
                null,
                Point.Zero)
            {
                Position = position
            };


            window.InitializeControls(
                LoadButton(deliveryKeepProperty, "BtDelete", btClickSound, btOverSound, device),
                LoadButton(deliveryKeepProperty, "BtGet", btClickSound, btOverSound, device));



            WzSubProperty closeButtonProperty = basicImage?["BtClose"] as WzSubProperty;
            if (closeButtonProperty != null)
            {
                try
                {
                    UIObject closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = frameTexture.Width - closeBtn.CanvasSnapshotWidth - 8;
                    closeBtn.Y = 8;
                    window.InitializeCloseButton(closeBtn);
                }
                catch
                {
                }
            }


            return window;

        }



        private static UIWindowBase CreateFamilyChartWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty sourceProperty = uiWindow2Image?["Family"] as WzSubProperty
                ?? uiWindow1Image?["Family"] as WzSubProperty;
            WzCanvasProperty backgroundProperty = sourceProperty?["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = backgroundProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.FamilyChart,
                    "Family",
                    "Fallback owner for the family statistics and branch-management window.",
                    position);
            }


            Texture2D[] rightIcons = new Texture2D[5];
            WzSubProperty rightIconProperty = sourceProperty?["RightIcon"] as WzSubProperty;
            rightIcons[0] = LoadCanvasTexture(rightIconProperty, "2", device);
            rightIcons[1] = LoadCanvasTexture(rightIconProperty, "3", device);
            rightIcons[2] = LoadCanvasTexture(rightIconProperty, "2", device);
            rightIcons[3] = LoadCanvasTexture(rightIconProperty, "3", device);
            rightIcons[4] = LoadCanvasTexture(rightIconProperty, "4", device);


            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            FamilyChartWindow window = new FamilyChartWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(sourceProperty, "backgrnd2", device, out Point overlayOffset),
                overlayOffset,
                LoadWindowCanvasLayerWithOffset(sourceProperty, "backgrnd3", device, out Point headerOffset),
                headerOffset,
                rightIcons,
                LoadIndexedStringValues(rightIconProperty?["type"] as WzSubProperty, 5),
                device)
            {
                Position = position
            };


            window.InitializeButtons(
                LoadButton(sourceProperty, "BtTree", clickSound, overSound, device),
                LoadButton(sourceProperty, "BtFamilyPrecept", clickSound, overSound, device),
                LoadButton(sourceProperty, "BtJuniorEntry", clickSound, overSound, device),
                LoadButton(sourceProperty, "BtLeft", clickSound, overSound, device),
                LoadButton(sourceProperty, "BtRight", clickSound, overSound, device),
                LoadButton(sourceProperty, "BtSpecial", clickSound, overSound, device),
                LoadButton(sourceProperty, "BtOK", clickSound, overSound, device));


            return window;

        }



        private static string[] LoadIndexedStringValues(WzSubProperty sourceProperty, int count)
        {
            string[] values = new string[Math.Max(0, count)];
            if (sourceProperty == null)
            {
                return values;
            }


            for (int i = 0; i < values.Length; i++)
            {
                values[i] = (sourceProperty[i.ToString()] as WzStringProperty)?.Value ?? string.Empty;
            }


            return values;

        }



        private static UIWindowBase CreateFamilyTreeWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            const int ClientFamilyTreeLeaderPlateStringPoolId = 0x11F4;
            const int ClientFamilyTreeMemberPlateStringPoolId = 0x11F5;
            const int ClientFamilyTreeSelectedOverlayStringPoolId = 0x11F8;

            WzSubProperty sourceProperty = uiWindow2Image?["FamilyTree"] as WzSubProperty
                ?? uiWindow1Image?["FamilyTree"] as WzSubProperty;
            WzCanvasProperty backgroundProperty = sourceProperty?["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = backgroundProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.FamilyTree,
                    "Family Tree",
                    "Fallback owner for the dedicated family-tree layout window.",
                    position);
            }


            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            WzSubProperty leaderPlateProperty = sourceProperty?["PlateLeader"] as WzSubProperty;
            WzSubProperty memberPlateProperty = sourceProperty?["PlateOthers"] as WzSubProperty;
            FamilyTreeWindow window = new FamilyTreeWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerFromClientUiStringPoolPath(
                    ClientFamilyTreeSelectedOverlayStringPoolId,
                    "UI/UIWindow.img/FamilyTree/selected",
                    uiWindow2Image,
                    uiWindow1Image,
                    device,
                    out Point selectedOffset)
                    ?? LoadWindowCanvasLayerWithOffset(sourceProperty, "selected", device, out selectedOffset),
                selectedOffset,
                LoadWindowCanvasLayerFromClientUiStringPoolPath(
                    ClientFamilyTreeLeaderPlateStringPoolId,
                    "UI/UIWindow.img/FamilyTree/PlateLeader/{0}",
                    0,
                    uiWindow2Image,
                    uiWindow1Image,
                    device,
                    out Point _)
                    ?? LoadWindowCanvasLayerWithOffset(leaderPlateProperty, "0", device, out Point _),
                LoadWindowCanvasLayerFromClientUiStringPoolPath(
                    ClientFamilyTreeLeaderPlateStringPoolId,
                    "UI/UIWindow.img/FamilyTree/PlateLeader/{0}",
                    1,
                    uiWindow2Image,
                    uiWindow1Image,
                    device,
                    out Point _)
                    ?? LoadWindowCanvasLayerWithOffset(leaderPlateProperty, "1", device, out Point _),
                LoadWindowCanvasLayerFromClientUiStringPoolPath(
                    ClientFamilyTreeMemberPlateStringPoolId,
                    "UI/UIWindow.img/FamilyTree/PlateOthers/{0}",
                    0,
                    uiWindow2Image,
                    uiWindow1Image,
                    device,
                    out Point _)
                    ?? LoadWindowCanvasLayerWithOffset(memberPlateProperty, "0", device, out Point _),
                LoadWindowCanvasLayerFromClientUiStringPoolPath(
                    ClientFamilyTreeMemberPlateStringPoolId,
                    "UI/UIWindow.img/FamilyTree/PlateOthers/{0}",
                    1,
                    uiWindow2Image,
                    uiWindow1Image,
                    device,
                    out Point _)
                    ?? LoadWindowCanvasLayerWithOffset(memberPlateProperty, "1", device, out Point _),
                device)
            {
                Position = position
            };


            window.InitializeButtons(
                LoadButton(sourceProperty, "BtClose", clickSound, overSound, device),
                LoadButton(sourceProperty, "BtJuniorEntry", clickSound, overSound, device),
                LoadButton(sourceProperty, "BtBye", clickSound, overSound, device),
                LoadButton(sourceProperty, "BtLeft", clickSound, overSound, device),
                LoadButton(sourceProperty, "BtRight", clickSound, overSound, device));
            return window;
        }


        private static UIWindowBase CreateSocialListWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty userListProperty = uiWindow2Image?["UserList"] as WzSubProperty
                ?? uiWindow1Image?["UserList"] as WzSubProperty;
            WzSubProperty mainProperty = userListProperty?["Main"] as WzSubProperty;
            WzCanvasProperty backgroundProperty = mainProperty?["backgrnd"] as WzCanvasProperty
                ?? userListProperty?["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = backgroundProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.SocialList,
                    "Social",
                    "Fallback owner for friend, party, guild, alliance, and blacklist windows.",
                    position);
            }


            Texture2D[] enabledTabs = new Texture2D[5];
            Texture2D[] disabledTabs = new Texture2D[5];
            WzSubProperty tabProperty = mainProperty?["Tab"] as WzSubProperty ?? userListProperty?["Tab"] as WzSubProperty;
            WzSubProperty enabledTabProperty = tabProperty?["enabled"] as WzSubProperty;
            WzSubProperty disabledTabProperty = tabProperty?["disabled"] as WzSubProperty;
            for (int i = 0; i < enabledTabs.Length; i++)
            {
                enabledTabs[i] = LoadCanvasTexture(enabledTabProperty, i.ToString(), device);
                disabledTabs[i] = LoadCanvasTexture(disabledTabProperty, i.ToString(), device);
            }


            SocialListWindow window = new SocialListWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(mainProperty ?? userListProperty, "backgrnd2", device, out Point overlayOffset),
                overlayOffset,
                enabledTabs,
                disabledTabs,
                LoadVerticalScrollbarSkin(basicImage?["VScr9"] as WzSubProperty, device),
                device)
            {
                Position = position
            };


            RegisterSocialListHeader(window, SocialListTab.Friend, mainProperty?["Friend"] as WzSubProperty, "title", device);
            RegisterSocialListHeader(window, SocialListTab.Party, mainProperty?["Party"] as WzSubProperty, "partyOn", device);
            RegisterSocialListHeader(window, SocialListTab.Guild, mainProperty?["Guild"] as WzSubProperty, "guildOn", device);
            RegisterSocialListHeader(window, SocialListTab.Alliance, mainProperty?["Union"] as WzSubProperty, "guildName", device);
            RegisterSocialListHeader(window, SocialListTab.Blacklist, mainProperty?["BlackList"] as WzSubProperty, "base", device);


            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            window.SetPageButtons(
                LoadButton(userListProperty, "BtPagePre", clickSound, overSound, device),
                LoadButton(userListProperty, "BtPageNext", clickSound, overSound, device));
            window.SetFriendFilterButtons(
                LoadButton(mainProperty?["Friend"] as WzSubProperty, "TapShowAll", clickSound, overSound, device),
                LoadButton(mainProperty?["Friend"] as WzSubProperty, "TapShowOnline", clickSound, overSound, device));


            RegisterSocialListActionButtons(window, SocialListTab.Friend, mainProperty?["Friend"] as WzSubProperty, clickSound, overSound, device,
                ("Friend.AddFriend", "BtAddFriend"),
                ("Friend.AddGroup", "BtAddGroup"),
                ("Friend.Party", "BtParty"),
                ("Friend.Chat", "BtChat"),
                ("Friend.Whisper", "BtWhisper"),
                ("Friend.GroupWhisper", "BtGroupWhisper"),
                ("Friend.Mate", "BtMate"),
                ("Friend.Message", "BtMessage"),
                ("Friend.Mod", "BtMod"),
                ("Friend.Delete", "BtDelete"),
                ("Friend.Block", "BtBlock"),
                ("Friend.UnBlock", "BtUnBlock"));
            RegisterSocialListActionButtons(window, SocialListTab.Party, mainProperty?["Party"] as WzSubProperty, clickSound, overSound, device,
                ("Party.Create", "BtCreate"),
                ("Party.Invite", "BtInvite"),
                ("Party.Kick", "BtKick"),
                ("Party.Withdraw", "BtWithdraw"),
                ("Party.Whisper", "BtWhisper"),
                ("Party.Chat", "BtChat"),
                ("Party.ChangeBoss", "BtChangeBoss"),
                ("Party.Search", "BtSearch"));
            RegisterSocialListActionButtons(window, SocialListTab.Guild, mainProperty?["Guild"] as WzSubProperty, clickSound, overSound, device,
                ("Guild.Board", "BtBoard"),
                ("Guild.Invite", "BtInvite"),
                ("Guild.Withdraw", "BtWithdraw"),
                ("Guild.PartyInvite", "BtPartyInvite"),
                ("Guild.GradeUp", "BtGradeUp"),
                ("Guild.GradeDown", "BtGradeDown"),
                ("Guild.Kick", "BtKick"),
                ("Guild.Where", "BtWhere"),
                ("Guild.Whisper", "BtWhisper"),
                ("Guild.Info", "BtInfo"),
                ("Guild.Skill", "BtSkill"),
                ("Guild.Search", "BtSearch"),
                ("Guild.Manage", "BtManage"),
                ("Guild.Change", "BtChange"));
            RegisterSocialListActionButtons(window, SocialListTab.Alliance, mainProperty?["Union"] as WzSubProperty, clickSound, overSound, device,
                ("Alliance.Invite", "BtInvite"),
                ("Alliance.Withdraw", "BtWithdraw"),
                ("Alliance.PartyInvite", "BtPartyInvite"),
                ("Alliance.GradeUp", "BtGradeUp"),
                ("Alliance.GradeDown", "BtGradeDown"),
                ("Alliance.Kick", "BtKick"),
                ("Alliance.Change", "BtChange"),
                ("Alliance.Chat", "BtChat"),
                ("Alliance.Whisper", "BtWhisper"),
                ("Alliance.Info", "BtInfo"),
                ("Alliance.Notice", "Btnotice"));
            RegisterSocialListActionButtons(window, SocialListTab.Blacklist, mainProperty?["BlackList"] as WzSubProperty, clickSound, overSound, device,
                ("Blacklist.Add", "BtAdd"),
                ("Blacklist.Delete", "BtDelete"));


            window.InitializeCloseButton(CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width));
            return window;
        }

        private static UIWindowBase CreateFriendGroupWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty userListProperty = uiWindow2Image?["UserList"] as WzSubProperty
                ?? uiWindow1Image?["UserList"] as WzSubProperty;
            WzSubProperty groupProperty = userListProperty?["Group"] as WzSubProperty;
            Texture2D frameTexture = LoadCanvasTexture(groupProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return null;
            }

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            WzSubProperty popupProperty = groupProperty?["Popup"] as WzSubProperty;

            FriendGroupWindow window = new FriendGroupWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(groupProperty, "backgrnd2", device, out Point overlayOffset),
                overlayOffset,
                LoadWindowCanvasLayerWithOffset(groupProperty, "base", device, out Point baseOffset),
                baseOffset,
                LoadWindowCanvasLayerWithOffset(popupProperty, "AddFriend", device, out Point addFriendPopupOffset),
                addFriendPopupOffset,
                LoadWindowCanvasLayerWithOffset(popupProperty, "GroupWhisper", device, out Point groupWhisperPopupOffset),
                groupWhisperPopupOffset,
                LoadWindowCanvasLayerWithOffset(popupProperty, "GroupDel", device, out Point groupDeletePopupOffset),
                groupDeletePopupOffset,
                LoadWindowCanvasLayerWithOffset(popupProperty, "GroupDelDeny", device, out Point groupDeleteDenyPopupOffset),
                groupDeleteDenyPopupOffset,
                LoadWindowCanvasLayerWithOffset(popupProperty, "NeedMessage", device, out Point needMessagePopupOffset),
                needMessagePopupOffset,
                LoadButton(groupProperty, "BtOK", clickSound, overSound, device),
                LoadButton(groupProperty, "BtCancle", clickSound, overSound, device),
                device)
            {
                Position = position
            };

            return window;
        }

        private static VerticalScrollbarSkin LoadVerticalScrollbarSkin(WzSubProperty scrollbarProperty, GraphicsDevice device)
        {
            if (scrollbarProperty == null)
            {
                return null;
            }


            WzSubProperty enabledProperty = scrollbarProperty["enabled"] as WzSubProperty;

            WzSubProperty disabledProperty = scrollbarProperty["disabled"] as WzSubProperty;



            return new VerticalScrollbarSkin
            {
                PrevStates = new[]
                {
                    LoadCanvasTexture(enabledProperty, "prev0", device),
                    LoadCanvasTexture(enabledProperty, "prev1", device),
                    LoadCanvasTexture(enabledProperty, "prev2", device)
                },
                NextStates = new[]
                {
                    LoadCanvasTexture(enabledProperty, "next0", device),
                    LoadCanvasTexture(enabledProperty, "next1", device),
                    LoadCanvasTexture(enabledProperty, "next2", device)
                },
                ThumbStates = new[]
                {
                    LoadCanvasTexture(enabledProperty, "thumb0", device),
                    LoadCanvasTexture(enabledProperty, "thumb1", device),
                    LoadCanvasTexture(enabledProperty, "thumb2", device)
                },
                PrevDisabled = LoadCanvasTexture(disabledProperty, "prev", device),
                NextDisabled = LoadCanvasTexture(disabledProperty, "next", device),
                Base = LoadCanvasTexture(enabledProperty, "base", device) ?? LoadCanvasTexture(disabledProperty, "base", device)
            };
        }


        private static UIWindowBase CreateSocialSearchWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty userListProperty = uiWindow2Image?["UserList"] as WzSubProperty
                ?? uiWindow1Image?["UserList"] as WzSubProperty;
            WzSubProperty searchProperty = userListProperty?["Search"] as WzSubProperty;
            Texture2D frameTexture = LoadCanvasTexture(searchProperty, "backgrd", device);
            if (frameTexture == null)
            {
                return null;
            }


            Texture2D[] enabledTabs = new Texture2D[3];
            Texture2D[] disabledTabs = new Texture2D[3];
            WzSubProperty enabledTabProperty = searchProperty?["Tab"]?["enabled"] as WzSubProperty;
            WzSubProperty disabledTabProperty = searchProperty?["Tab"]?["disabled"] as WzSubProperty;
            for (int i = 0; i < enabledTabs.Length; i++)
            {
                enabledTabs[i] = LoadCanvasTexture(enabledTabProperty, i.ToString(), device);
                disabledTabs[i] = LoadCanvasTexture(disabledTabProperty, i.ToString(), device);
            }


            SocialSearchWindow window = new SocialSearchWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(searchProperty, "backgrd2", device, out Point overlayOffset),
                overlayOffset,
                LoadWindowCanvasLayerWithOffset(searchProperty, "backgrd3", device, out Point contentOffset),
                contentOffset,
                enabledTabs,
                disabledTabs,
                device)
            {
                Position = position
            };


            RegisterSocialSearchContent(window, SocialSearchTab.Party, searchProperty?["Party"] as WzSubProperty, "base", device);

            RegisterSocialSearchContent(window, SocialSearchTab.PartyMember, searchProperty?["PartyMember"] as WzSubProperty, "table", device);

            RegisterSocialSearchContent(window, SocialSearchTab.Expedition, searchProperty?["Expedition"] as WzSubProperty, "base", device);



            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            window.SetFilterButtons(
                LoadButton(searchProperty, "BtAllLevel", clickSound, overSound, device),
                LoadButton(searchProperty, "BtSimilarLevel", clickSound, overSound, device));
            RegisterSocialSearchButtons(window, SocialSearchTab.Party, searchProperty?["Party"] as WzSubProperty, clickSound, overSound, device,
                ("Search.Party.Request", "BtRequest"),
                ("Search.Party.PartyLeader", "BtPartyLeader"),
                ("Search.Party.PartyLevel", "BtPartyLevel"),
                ("Search.Party.Member", "BtMember"),
                ("Search.Party.Info", "PartyInfo"));
            RegisterSocialSearchButtons(window, SocialSearchTab.PartyMember, searchProperty?["PartyMember"] as WzSubProperty, clickSound, overSound, device,
                ("Search.PartyMember.Invite", "BtInvite"),
                ("Search.PartyMember.Name", "BtName"),
                ("Search.PartyMember.Job", "BtJob"),
                ("Search.PartyMember.Level", "BtLevel"));
            RegisterSocialSearchButtons(window, SocialSearchTab.Expedition, searchProperty?["Expedition"] as WzSubProperty, clickSound, overSound, device,
                ("Search.Expedition.Start", "BtStart"),
                ("Search.Expedition.Regist", "BtRegist"),
                ("Search.Expedition.Delete", "BtDelete"),
                ("Search.Expedition.QuickJoin", "BtQuickJoin"),
                ("Search.Expedition.Request", "BtRequest"),
                ("Search.Expedition.Whisper", "BtWhisper"),
                ("Search.Expedition.Front", "BtFront"),
                ("Search.Expedition.Regist2", "BtRegist2"),
                ("Search.Expedition.Cancel", "BtCancle"));


            UIObject closeButton = LoadButton(searchProperty, "BtClose", clickSound, overSound, device)
                ?? CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width);
            if (closeButton != null)
            {
                window.InitializeCloseButton(closeButton);
            }


            return window;

        }



        private static UIWindowBase CreateGuildSearchWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty userListProperty = uiWindow2Image?["UserList"] as WzSubProperty
                ?? uiWindow1Image?["UserList"] as WzSubProperty;
            WzSubProperty guildSearchProperty = userListProperty?["GuildSearch"] as WzSubProperty;
            Texture2D frameTexture = LoadCanvasTexture(guildSearchProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return null;
            }


            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            GuildSearchWindow window = new GuildSearchWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(guildSearchProperty, "backgrnd2", device, out Point overlayOffset),
                overlayOffset,
                LoadWindowCanvasLayerWithOffset(guildSearchProperty, "base2", device, out Point contentOffset),
                contentOffset,
                device)
            {
                Position = position
            };


            foreach ((string actionKey, string buttonName) in new[]
            {
                ("GuildSearch.Add", "BtAdd"),
                ("GuildSearch.Delete", "BtDelete"),
                ("GuildSearch.Join", "BtJoin"),
                ("GuildSearch.Whisper", "BtWhisper"),
                ("GuildSearch.Renew", "BtRenew"),
                ("GuildSearch.PagePrev", "BtPagePre"),
                ("GuildSearch.PageNext", "BtPageNext")
            })
            {
                UIObject button = LoadButton(guildSearchProperty, buttonName, clickSound, overSound, device);
                if (button != null)
                {
                    window.RegisterActionButton(actionKey, button);
                }
            }


            UIObject closeButton = CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width);
            if (closeButton != null)
            {
                window.InitializeCloseButton(closeButton);
            }


            return window;

        }



        private static UIWindowBase CreateGuildSkillWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty userListProperty = uiWindow2Image?["UserList"] as WzSubProperty
                ?? uiWindow1Image?["UserList"] as WzSubProperty;
            WzSubProperty guildSkillProperty = userListProperty?["GuildSkill"] as WzSubProperty;
            Texture2D frameTexture = LoadCanvasTexture(guildSkillProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return null;
            }

            Texture2D guildSkillBaseTexture = LoadCanvasTexture(guildSkillProperty, "base", device);
            IDXObject guildSkillBaseLayer = LoadWindowCanvasLayerWithOffset(guildSkillProperty, "base", device, out Point headerOffset);

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            GuildSkillWindow window = new GuildSkillWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(guildSkillProperty, "backgrnd2", device, out Point overlayOffset),
                overlayOffset,
                guildSkillBaseLayer,
                headerOffset,
                guildSkillBaseTexture ?? frameTexture,
                LoadCanvasTexture(guildSkillProperty, "skill0", device),
                LoadCanvasTexture(guildSkillProperty, "skill1", device),
                LoadCanvasTexture(guildSkillProperty?["recommend"] as WzSubProperty, "0", device),
                new[]
                {
                    LoadCanvasTexture(guildSkillProperty?["sheet1"] as WzSubProperty, "0", device),
                    LoadCanvasTexture(guildSkillProperty?["sheet1"] as WzSubProperty, "1", device),
                    LoadCanvasTexture(guildSkillProperty?["sheet1"] as WzSubProperty, "2", device)
                },
                LoadButton(guildSkillProperty, "BtRenewal", clickSound, overSound, device),
                LoadButton(guildSkillProperty, "BtUp", clickSound, overSound, device),
                device)
            {
                Position = position
            };


            UIObject closeButton = CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width);
            if (closeButton != null)
            {
                window.InitializeCloseButton(closeButton);
            }


            return window;

        }



        private static UIWindowBase CreateGuildManageWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty userListProperty = uiWindow2Image?["UserList"] as WzSubProperty
                ?? uiWindow1Image?["UserList"] as WzSubProperty;
            WzSubProperty guildManageProperty = userListProperty?["GuildManage"] as WzSubProperty;
            Texture2D frameTexture = LoadCanvasTexture(guildManageProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return null;
            }


            Texture2D[] enabledTabs = new Texture2D[3];
            Texture2D[] disabledTabs = new Texture2D[3];
            WzSubProperty tabProperty = guildManageProperty?["Tab"] as WzSubProperty;
            WzSubProperty enabledTabProperty = tabProperty?["enabled"] as WzSubProperty;
            WzSubProperty disabledTabProperty = tabProperty?["disabled"] as WzSubProperty;
            for (int i = 0; i < enabledTabs.Length; i++)
            {
                enabledTabs[i] = LoadCanvasTexture(enabledTabProperty, i.ToString(), device);
                disabledTabs[i] = LoadCanvasTexture(disabledTabProperty, i.ToString(), device);
            }


            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            GuildManageWindow window = new GuildManageWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(guildManageProperty, "backgrnd2", device, out Point overlayOffset),
                overlayOffset,
                enabledTabs,
                disabledTabs,
                LoadButton(guildManageProperty, "BtPagePre", clickSound, overSound, device),
                LoadButton(guildManageProperty, "BtPageNext", clickSound, overSound, device),
                LoadButton(guildManageProperty?["Position"] as WzSubProperty, "BtEdit", clickSound, overSound, device),
                LoadButton(guildManageProperty?["Position"] as WzSubProperty, "BtSave", clickSound, overSound, device),
                LoadButton(guildManageProperty?["Admission"] as WzSubProperty, "BtOK", clickSound, overSound, device),
                LoadButton(guildManageProperty?["Admission"] as WzSubProperty, "BtNO", clickSound, overSound, device),
                LoadButton(guildManageProperty?["Change"] as WzSubProperty, "BtChange", clickSound, overSound, device),
                device)
            {
                Position = position
            };


            RegisterGuildManageTabLayer(window, GuildManageTab.Position, guildManageProperty?["Position"] as WzSubProperty, device);

            RegisterGuildManageTabLayer(window, GuildManageTab.Admission, guildManageProperty?["Admission"] as WzSubProperty, device);

            RegisterGuildManageTabLayer(window, GuildManageTab.Change, guildManageProperty?["Change"] as WzSubProperty, device);



            UIObject closeButton = CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width);
            if (closeButton != null)
            {
                window.InitializeCloseButton(closeButton);
            }


            return window;

        }



        private static UIWindowBase CreateAllianceEditorWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty userListProperty = uiWindow2Image?["UserList"] as WzSubProperty
                ?? uiWindow1Image?["UserList"] as WzSubProperty;
            WzSubProperty unionInfoProperty = userListProperty?["UnionInfo"] as WzSubProperty;
            Texture2D frameTexture = LoadCanvasTexture(unionInfoProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return null;
            }


            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            AllianceEditorWindow window = new AllianceEditorWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(unionInfoProperty, "backgrnd2", device, out Point overlayOffset),
                overlayOffset,
                LoadWindowCanvasLayerWithOffset(unionInfoProperty, "base", device, out Point headerOffset),
                headerOffset,
                LoadWindowCanvasLayerWithOffset(unionInfoProperty, "base2", device, out Point contentOffset),
                contentOffset,
                LoadButton(unionInfoProperty, "BtEdit", clickSound, overSound, device),
                LoadButton(unionInfoProperty, "BtSave", clickSound, overSound, device),
                device)
            {
                Position = position
            };


            UIObject closeButton = CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width);
            if (closeButton != null)
            {
                window.InitializeCloseButton(closeButton);
            }


            return window;

        }



        private static void RegisterGuildManageTabLayer(
            GuildManageWindow window,
            GuildManageTab tab,
            WzSubProperty sourceProperty,
            GraphicsDevice device)
        {
            if (window == null || sourceProperty == null)
            {
                return;
            }


            IDXObject baseLayer = LoadWindowCanvasLayerWithOffset(sourceProperty, "base", device, out Point baseOffset);
            IDXObject contentLayer = LoadWindowCanvasLayerWithOffset(sourceProperty, "base2", device, out Point contentOffset);
            window.RegisterTabLayer(tab, baseLayer, baseOffset, contentLayer, contentOffset);
        }


        private static UIWindowBase CreateGuildRankWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty userListProperty = uiWindow2Image?["UserList"] as WzSubProperty
                ?? uiWindow1Image?["UserList"] as WzSubProperty;
            WzSubProperty guildRankProperty = userListProperty?["GuildRank"] as WzSubProperty;
            Texture2D frameTexture = LoadCanvasTexture(guildRankProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return null;
            }


            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            LoadIndexedCanvasSequence(guildRankProperty?["IconEff"] as WzSubProperty, device, out Texture2D[] iconFrames, out Point[] iconOffsets, out _);

            GuildRankWindow window = new(
                new DXObject(0, 0, frameTexture, 0),
                iconFrames,
                iconOffsets,
                LoadButton(guildRankProperty, "BtOK", clickSound, overSound, device),
                LoadButton(guildRankProperty, "BtLeft", clickSound, overSound, device),
                LoadButton(guildRankProperty, "BtRight", clickSound, overSound, device),
                device)
            {
                Position = position
            };

            WzSubProperty vScrollProperty = basicImage?["VScr"] as WzSubProperty;
            if (vScrollProperty != null)
            {
                WzSubProperty enabledProperty = vScrollProperty["enabled"] as WzSubProperty;
                WzSubProperty disabledProperty = vScrollProperty["disabled"] as WzSubProperty;
                window.SetScrollBarTextures(
                    LoadCanvasTexture(enabledProperty, "prev0", device),
                    LoadCanvasTexture(enabledProperty, "prev1", device),
                    LoadCanvasTexture(enabledProperty, "next0", device),
                    LoadCanvasTexture(enabledProperty, "next1", device),
                    LoadCanvasTexture(enabledProperty, "base", device),
                    LoadCanvasTexture(enabledProperty, "thumb0", device),
                    LoadCanvasTexture(enabledProperty, "thumb1", device),
                    LoadCanvasTexture(disabledProperty, "prev", device),
                    LoadCanvasTexture(disabledProperty, "next", device),
                    LoadCanvasTexture(disabledProperty, "base", device));
            }


            UIObject closeButton = CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width);
            if (closeButton != null)
            {
                window.InitializeCloseButton(closeButton);
            }


            return window;
        }


        private static UIWindowBase CreateGuildMarkWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty userListProperty = uiWindow2Image?["UserList"] as WzSubProperty
                ?? uiWindow1Image?["UserList"] as WzSubProperty;
            WzSubProperty guildMarkProperty = userListProperty?["Guild_MakeMark"] as WzSubProperty
                ?? ((uiWindow1Image?["UserList"] as WzSubProperty)?["Guild"] as WzSubProperty)?["MakeMark"] as WzSubProperty;
            if (guildMarkProperty == null)
            {
                return null;
            }


            LoadIndexedCanvasSequence(guildMarkProperty?["backgrnd"] as WzSubProperty, device, out Texture2D[] backgroundFrames, out _, out int[] backgroundDelays);
            if (backgroundFrames.Length == 0)
            {
                return null;
            }


            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject agreeButton = LoadButton(guildMarkProperty, "BtAgree", clickSound, overSound, device);
            UIObject disagreeButton = LoadButton(guildMarkProperty, "BtDisagree", clickSound, overSound, device);
            UIObject backgroundLeftButton = LoadButton(guildMarkProperty, "BtLeft", clickSound, overSound, device);
            UIObject backgroundRightButton = LoadButton(guildMarkProperty, "BtRight", clickSound, overSound, device);
            UIObject markLeftButton = LoadButton(guildMarkProperty, "BtLeft", clickSound, overSound, device);
            UIObject markRightButton = LoadButton(guildMarkProperty, "BtRight", clickSound, overSound, device);
            UIObject backgroundColorLeftButton = LoadButton(guildMarkProperty, "BtLeft", clickSound, overSound, device);
            UIObject backgroundColorRightButton = LoadButton(guildMarkProperty, "BtRight", clickSound, overSound, device);
            UIObject markColorLeftButton = LoadButton(guildMarkProperty, "BtLeft", clickSound, overSound, device);
            UIObject markColorRightButton = LoadButton(guildMarkProperty, "BtRight", clickSound, overSound, device);
            UIObject comboButton = LoadButton(guildMarkProperty, "BtDown", clickSound, overSound, device);

            SetButtonPosition(backgroundLeftButton, 37, 172);
            SetButtonPosition(markLeftButton, 37, 228);
            SetButtonPosition(backgroundColorLeftButton, 130, 172);
            SetButtonPosition(markColorLeftButton, 130, 228);
            SetButtonPosition(backgroundRightButton, 80, 172);
            SetButtonPosition(markRightButton, 80, 228);
            SetButtonPosition(backgroundColorRightButton, 173, 172);
            SetButtonPosition(markColorRightButton, 173, 228);
            SetButtonPosition(comboButton, 214, 252);

            GuildMarkWindow window = new(
                backgroundFrames,
                backgroundDelays,
                LoadWindowCanvasLayerWithOffset(guildMarkProperty, "backgrnd2", device, out Point overlayOffset),
                overlayOffset,
                agreeButton,
                disagreeButton,
                backgroundLeftButton,
                backgroundRightButton,
                markLeftButton,
                markRightButton,
                backgroundColorLeftButton,
                backgroundColorRightButton,
                markColorLeftButton,
                markColorRightButton,
                comboButton,
                device)
            {
                Position = position
            };


            UIObject closeButton = CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, backgroundFrames[0].Width);
            if (closeButton != null)
            {
                window.InitializeCloseButton(closeButton);
            }


            return window;
        }


        private static UIWindowBase CreateGuildCreateAgreementWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty userListProperty = uiWindow2Image?["UserList"] as WzSubProperty
                ?? uiWindow1Image?["UserList"] as WzSubProperty;
            WzSubProperty sourceProperty = userListProperty?["Guild_Make"] as WzSubProperty;
            if (sourceProperty == null)
            {
                return null;
            }


            LoadIndexedCanvasSequence(sourceProperty?["backgrnd"] as WzSubProperty, device, out Texture2D[] backgroundFrames, out _, out int[] backgroundDelays);
            if (backgroundFrames.Length == 0)
            {
                return null;
            }


            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            GuildCreateAgreementWindow window = new(
                backgroundFrames,
                backgroundDelays,
                LoadWindowCanvasLayerWithOffset(sourceProperty, "message", device, out Point messageOffset),
                messageOffset,
                LoadButton(sourceProperty, "BtAgree", clickSound, overSound, device),
                LoadButton(sourceProperty, "BtDisagree", clickSound, overSound, device),
                device)
            {
                Position = position
            };


            UIObject closeButton = CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, backgroundFrames[0].Width);
            if (closeButton != null)
            {
                window.InitializeCloseButton(closeButton);
            }


            return window;
        }


        private static void LoadIndexedCanvasSequence(
            WzSubProperty sourceProperty,
            GraphicsDevice device,
            out Texture2D[] textures,
            out Point[] offsets,
            out int[] delays)
        {
            List<Texture2D> textureList = new();
            List<Point> offsetList = new();
            List<int> delayList = new();

            if (sourceProperty != null)
            {
                foreach (WzImageProperty property in sourceProperty.WzProperties.OrderBy(current => int.TryParse(current.Name, out int index) ? index : int.MaxValue))
                {
                    if (property is not WzCanvasProperty canvas)
                    {
                        continue;
                    }

                    Texture2D texture = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                    if (texture == null)
                    {
                        continue;
                    }

                    System.Drawing.PointF origin = canvas.GetCanvasOriginPosition();
                    textureList.Add(texture);
                    offsetList.Add(new Point(-(int)origin.X, -(int)origin.Y));
                    delayList.Add(canvas["delay"]?.GetInt() ?? 120);
                }
            }

            textures = textureList.ToArray();
            offsets = offsetList.ToArray();
            delays = delayList.ToArray();
        }


        private static void SetButtonPosition(UIObject button, int x, int y)
        {
            if (button == null)
            {
                return;
            }

            button.X = x;
            button.Y = y;
        }


        private static void RegisterSocialListHeader(
            SocialListWindow window,
            SocialListTab tab,
            WzSubProperty sourceProperty,
            string canvasName,
            GraphicsDevice device)
        {
            if (window == null || sourceProperty == null)
            {
                return;
            }


            IDXObject headerLayer = LoadWindowCanvasLayerWithOffset(sourceProperty, canvasName, device, out Point offset);
            if (headerLayer != null)
            {
                window.RegisterHeaderLayer(tab, headerLayer, offset);
            }
        }


        private static void RegisterSocialListActionButtons(
            SocialListWindow window,
            SocialListTab tab,
            WzSubProperty sourceProperty,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device,
            params (string ActionKey, string ButtonName)[] buttonMappings)
        {
            if (window == null || sourceProperty == null || buttonMappings == null)
            {
                return;
            }


            foreach ((string actionKey, string buttonName) in buttonMappings)
            {
                UIObject button = LoadButton(sourceProperty, buttonName, clickSound, overSound, device);
                if (button != null)
                {
                    window.RegisterActionButton(tab, actionKey, button);
                }
            }
        }


        private static void RegisterSocialSearchContent(
            SocialSearchWindow window,
            SocialSearchTab tab,
            WzSubProperty sourceProperty,
            string canvasName,
            GraphicsDevice device)
        {
            if (window == null || sourceProperty == null)
            {
                return;
            }


            IDXObject layer = LoadWindowCanvasLayerWithOffset(sourceProperty, canvasName, device, out Point offset);
            if (layer != null)
            {
                window.RegisterContentLayer(tab, layer, offset);
            }
        }


        private static void RegisterSocialSearchButtons(
            SocialSearchWindow window,
            SocialSearchTab tab,
            WzSubProperty sourceProperty,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device,
            params (string ActionKey, string ButtonName)[] mappings)
        {
            if (window == null || sourceProperty == null || mappings == null)
            {
                return;
            }


            foreach ((string actionKey, string buttonName) in mappings)
            {
                UIObject button = LoadButton(sourceProperty, buttonName, clickSound, overSound, device);
                if (button != null)
                {
                    window.RegisterActionButton(tab, actionKey, button);
                }
            }
        }


        private static UIWindowBase CreateMessengerWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty sourceProperty = uiWindow2Image?["Messenger"] as WzSubProperty
                ?? uiWindow1Image?["Messenger"] as WzSubProperty;
            if (sourceProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.Messenger,
                    "Messenger",
                    "Fallback owner for the dedicated Messenger presence and invite surface.",
                    position);
            }


            WzSubProperty maximizedProperty = sourceProperty["Max"] as WzSubProperty ?? sourceProperty;
            WzSubProperty minimizedProperty = sourceProperty["Min"] as WzSubProperty
                ?? sourceProperty["Min2"] as WzSubProperty
                ?? maximizedProperty;
            WzSubProperty collapsedProperty = sourceProperty["Min2"] as WzSubProperty
                ?? minimizedProperty;
            WzSubProperty nameBarProperty = sourceProperty["Name"] as WzSubProperty
                ?? sourceProperty["NameBar"] as WzSubProperty;


            Texture2D maxFrameTexture = LoadCanvasTexture(maximizedProperty, "backgrnd", device);
            Texture2D minFrameTexture = LoadCanvasTexture(minimizedProperty, "backgrnd", device) ?? maxFrameTexture;
            Texture2D collapsedFrameTexture = LoadCanvasTexture(collapsedProperty, "backgrnd", device) ?? minFrameTexture;
            if (maxFrameTexture == null || minFrameTexture == null || collapsedFrameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.Messenger,
                    "Messenger",
                    "Fallback owner for the dedicated Messenger presence and invite surface.",
                    position);
            }


            IDXObject maxFrame = new DXObject(0, 0, maxFrameTexture, 0);
            IDXObject minFrame = new DXObject(0, 0, minFrameTexture, 0);
            IDXObject collapsedFrame = new DXObject(0, 0, collapsedFrameTexture, 0);
            IDXObject maxOverlay = LoadWindowCanvasLayerWithOffset(maximizedProperty, "backgrnd2", device, out Point maxOverlayOffset);
            IDXObject maxContent = LoadWindowCanvasLayerWithOffset(maximizedProperty, "backgrnd3", device, out Point maxContentOffset);
            IDXObject minOverlay = LoadWindowCanvasLayerWithOffset(minimizedProperty, "backgrnd2", device, out Point minOverlayOffset);
            IDXObject minContent = LoadWindowCanvasLayerWithOffset(minimizedProperty, "backgrnd3", device, out Point minContentOffset);
            IDXObject collapsedOverlay = LoadWindowCanvasLayerWithOffset(collapsedProperty, "backgrnd2", device, out Point collapsedOverlayOffset);
            IDXObject collapsedContent = LoadWindowCanvasLayerWithOffset(collapsedProperty, "backgrnd3", device, out Point collapsedContentOffset);


            Texture2D[] nameBars = new Texture2D[3];
            for (int i = 0; i < nameBars.Length; i++)
            {
                nameBars[i] = LoadCanvasTexture(nameBarProperty, i.ToString(), device);
            }


            Texture2D maxStatusIcon = LoadCanvasTexture(maximizedProperty, "icon", device);
            Texture2D minStatusIcon = LoadCanvasTexture(minimizedProperty, "icon", device) ?? maxStatusIcon;
            Texture2D collapsedStatusIcon = LoadCanvasTexture(collapsedProperty, "icon", device) ?? minStatusIcon;
            Point maxStatusIconPosition = GetCanvasOffset(maximizedProperty?["icon"] as WzCanvasProperty);
            Point minStatusIconPosition = GetCanvasOffset(minimizedProperty?["icon"] as WzCanvasProperty);
            Point collapsedStatusIconPosition = GetCanvasOffset(collapsedProperty?["icon"] as WzCanvasProperty);


            Texture2D[] chatBalloonFrames = Array.Empty<Texture2D>();
            if (sourceProperty["chatBalloon"] is WzSubProperty chatBalloonProperty)
            {
                var frames = new List<Texture2D>();
                for (int i = 0; i < 8; i++)
                {
                    Texture2D frame = LoadCanvasTexture(chatBalloonProperty, i.ToString(), device);
                    if (frame == null)
                    {
                        break;
                    }


                    frames.Add(frame);

                }



                chatBalloonFrames = frames.ToArray();

            }



            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject enterButton = LoadButton(maximizedProperty, "BtEnter", btClickSound, btOverSound, device)
                ?? LoadButton(sourceProperty, "BtEnter", btClickSound, btOverSound, device);
            UIObject claimButton = LoadButton(maximizedProperty, "BtClame", btClickSound, btOverSound, device)
                ?? LoadButton(sourceProperty, "BtClame", btClickSound, btOverSound, device);
            UIObject maximizeButton = LoadButton(minimizedProperty, "BtMax", btClickSound, btOverSound, device)
                ?? LoadButton(sourceProperty, "BtMax", btClickSound, btOverSound, device);
            UIObject minimizeButton = LoadButton(maximizedProperty, "BtMin", btClickSound, btOverSound, device)
                ?? LoadButton(sourceProperty, "BtMin", btClickSound, btOverSound, device);
            UIObject minEnterLayout = LoadButton(minimizedProperty, "BtEnter", btClickSound, btOverSound, device) ?? enterButton;
            UIObject minClaimLayout = LoadButton(minimizedProperty, "BtClame", btClickSound, btOverSound, device) ?? claimButton;
            UIObject collapsedMaxLayout = LoadButton(collapsedProperty, "BtMax", btClickSound, btOverSound, device) ?? maximizeButton;
            UIObject minMinLayout = LoadButton(minimizedProperty, "BtMin", btClickSound, btOverSound, device) ?? minimizeButton;
            UIObject collapsedMinLayout = LoadButton(collapsedProperty, "BtMin", btClickSound, btOverSound, device) ?? minimizeButton;


            MessengerWindow window = new MessengerWindow(
                maxFrame,
                minFrame,
                maxOverlay,
                maxOverlayOffset,
                maxContent,
                maxContentOffset,
                minOverlay,
                minOverlayOffset,
                minContent,
                minContentOffset,
                collapsedFrame,
                collapsedOverlay,
                collapsedOverlayOffset,
                collapsedContent,
                collapsedContentOffset,
                nameBars,
                chatBalloonFrames,
                maxStatusIcon,
                maxStatusIconPosition,
                minStatusIcon,
                minStatusIconPosition,
                collapsedStatusIcon,
                collapsedStatusIconPosition,
                new Point(enterButton?.X ?? 0, enterButton?.Y ?? 0),
                new Point(minEnterLayout?.X ?? enterButton?.X ?? 0, minEnterLayout?.Y ?? enterButton?.Y ?? 0),
                new Point(claimButton?.X ?? 0, claimButton?.Y ?? 0),
                new Point(minClaimLayout?.X ?? claimButton?.X ?? 0, minClaimLayout?.Y ?? claimButton?.Y ?? 0),
                new Point(maximizeButton?.X ?? 0, maximizeButton?.Y ?? 0),
                new Point(collapsedMaxLayout?.X ?? maximizeButton?.X ?? 0, collapsedMaxLayout?.Y ?? maximizeButton?.Y ?? 0),
                new Point(minimizeButton?.X ?? 0, minimizeButton?.Y ?? 0),
                new Point(minMinLayout?.X ?? minimizeButton?.X ?? 0, minMinLayout?.Y ?? minimizeButton?.Y ?? 0),
                new Point(collapsedMinLayout?.X ?? minimizeButton?.X ?? 0, collapsedMinLayout?.Y ?? minimizeButton?.Y ?? 0),
                device)
            {
                Position = position
            };
            window.InitializeControls(enterButton, claimButton, maximizeButton, minimizeButton);


            WzSubProperty closeButtonProperty = basicImage?["BtClose"] as WzSubProperty;
            if (closeButtonProperty != null)
            {
                try
                {
                    UIObject closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = maxFrameTexture.Width - closeBtn.CanvasSnapshotWidth - 8;
                    closeBtn.Y = 8;
                    window.InitializeCloseButton(closeBtn);
                }
                catch
                {
                }
            }


            return window;

        }



        private static UIWindowBase CreateMapleTvWindow(
            WzImage uiWindow1Image,
            WzImage mapleTvImage,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty sourceProperty = uiWindow1Image?["MapleTV"] as WzSubProperty;
            if (sourceProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.MapleTv,
                    "MapleTV",
                    "Fallback owner for the MapleTV send board and timed broadcast surface.",
                    position);
            }


            Texture2D selfFrameTexture = LoadCanvasTexture(sourceProperty, "backgrnd", device);
            Texture2D receiverFrameTexture = LoadCanvasTexture(sourceProperty, "backgrnd3", device) ?? selfFrameTexture;
            if (selfFrameTexture == null || receiverFrameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.MapleTv,
                    "MapleTV",
                    "Fallback owner for the MapleTV send board and timed broadcast surface.",
                    position);
            }


            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject okButton = LoadButton(sourceProperty, "BtOk", btClickSound, btOverSound, device);
            UIObject cancelButton = LoadButton(sourceProperty, "BtCancel", btClickSound, btOverSound, device);
            UIObject toButton = LoadMapleTvReceiverButton(sourceProperty, device, new Point(20, 65));
            if (okButton == null || cancelButton == null || toButton == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.MapleTv,
                    "MapleTV",
                    "MapleTV controls were unavailable in this UI dataset, so the simulator is using a placeholder window instead.",
                    position);
            }


            MapleTvWindow window = new MapleTvWindow(
                new DXObject(0, 0, selfFrameTexture, 0),
                new DXObject(0, 0, receiverFrameTexture, 0),
                LoadWindowCanvasLayerWithOffset(sourceProperty, "backgrnd2", device, out Point selfOverlayOffset),
                selfOverlayOffset,
                LoadWindowCanvasLayerWithOffset(sourceProperty, "backgrnd4", device, out Point receiverOverlayOffset),
                receiverOverlayOffset,
                LoadMapleTvVisualAssets(mapleTvImage, device))
            {
                Position = position
            };
            // CUIMapleTV::OnCreate places the primary controls at fixed client coordinates.
            okButton.X = 60;
            okButton.Y = 208;
            cancelButton.X = 110;
            cancelButton.Y = 208;
            toButton.X = 20;
            toButton.Y = 65;



            window.InitializeControls(okButton, cancelButton, toButton);



            WzSubProperty closeButtonProperty = basicImage?["BtClose"] as WzSubProperty;
            if (closeButtonProperty != null)
            {
                try
                {
                    UIObject closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = selfFrameTexture.Width - closeBtn.CanvasSnapshotWidth - 8;
                    closeBtn.Y = 8;
                    window.InitializeCloseButton(closeBtn);
                }
                catch
                {
                }
            }


            return window;

        }

        private static EngagementProposalWindow CreateEngagementProposalWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage soundUIImage,
            GraphicsDevice device)
        {
            WzSubProperty sourceProperty = uiWindow2Image?["MateMessage"] as WzSubProperty
                ?? uiWindow1Image?["MateMessage"] as WzSubProperty;
            if (sourceProperty == null)
            {
                return null;
            }

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject acceptButton = LoadButton(sourceProperty, "BtSend", clickSound, overSound, device)
                ?? UiButtonFactory.CreateSolidButton(
                    device,
                    48,
                    18,
                    new Color(240, 220, 168),
                    new Color(225, 196, 120),
                    new Color(255, 236, 183),
                    new Color(170, 170, 170));

            EngagementProposalWindow window = new(
                new EngagementProposalWindowAssets(
                    LoadCanvasTexture(sourceProperty, "text", device)
                    ?? LoadCanvasTexture(sourceProperty, "title", device),
                    ResolveEngagementProposalHeadingOffset(sourceProperty, "text"),
                    LoadEngagementProposalBand(sourceProperty["top"] as WzSubProperty, device, 35),
                    LoadEngagementProposalBand(sourceProperty["center"] as WzSubProperty, device, 5),
                    LoadEngagementProposalBand(sourceProperty["textBox"] as WzSubProperty, device, 35),
                    LoadEngagementProposalBand(sourceProperty["bottom"] as WzSubProperty, device, 35)),
                device);
            window.InitializeControls(acceptButton);
            return window;
        }

        private static EngagementProposalWindow CreateFallbackEngagementProposalWindow(GraphicsDevice device)
        {
            EngagementProposalWindow window = new(
                new EngagementProposalWindowAssets(
                    null,
                    new Point(92, 2),
                    LoadEngagementProposalFallbackBand(device, 35),
                    LoadEngagementProposalFallbackBand(device, 5),
                    LoadEngagementProposalFallbackBand(device, 35),
                    LoadEngagementProposalFallbackBand(device, 35)),
                device);
            window.InitializeControls(
                UiButtonFactory.CreateSolidButton(
                    device,
                    48,
                    18,
                    new Color(240, 220, 168),
                    new Color(225, 196, 120),
                    new Color(255, 236, 183),
                    new Color(170, 170, 170)));
            return window;
        }

        private static WeddingInvitationWindow CreateWeddingInvitationWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage soundUIImage,
            GraphicsDevice device)
        {
            WzSubProperty sourceProperty = uiWindow2Image?["Wedding/Invitation"] as WzSubProperty
                ?? uiWindow1Image?["Wedding/Invitation"] as WzSubProperty;
            if (sourceProperty == null)
            {
                return null;
            }

            Dictionary<WeddingInvitationStyle, Texture2D> backgrounds = new()
            {
                [WeddingInvitationStyle.Neat] = LoadCanvasTexture(sourceProperty, "neat", device),
                [WeddingInvitationStyle.Sweet] = LoadCanvasTexture(sourceProperty, "sweet", device),
                [WeddingInvitationStyle.Premium] = LoadCanvasTexture(sourceProperty, "premium", device)
            };

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject acceptButton = LoadButton(sourceProperty, "BtOK", clickSound, overSound, device)
                ?? UiButtonFactory.CreateSolidButton(
                    device,
                    57,
                    23,
                    new Color(240, 220, 168),
                    new Color(225, 196, 120),
                    new Color(255, 236, 183),
                    new Color(170, 170, 170));

            WeddingInvitationWindow window = new(backgrounds, device);
            window.InitializeControls(acceptButton);
            return window;
        }

        private static WeddingInvitationWindow CreateFallbackWeddingInvitationWindow(GraphicsDevice device)
        {
            Dictionary<WeddingInvitationStyle, Texture2D> backgrounds = new()
            {
                [WeddingInvitationStyle.Neat] = CreateFilledTexture(device, 234, 250, new Color(245, 233, 220), new Color(214, 195, 168)),
                [WeddingInvitationStyle.Sweet] = CreateFilledTexture(device, 234, 250, new Color(250, 235, 239), new Color(214, 176, 190)),
                [WeddingInvitationStyle.Premium] = CreateFilledTexture(device, 234, 250, new Color(242, 238, 228), new Color(174, 164, 134))
            };

            WeddingInvitationWindow window = new(backgrounds, device);
            window.InitializeControls(
                UiButtonFactory.CreateSolidButton(
                    device,
                    57,
                    23,
                    new Color(240, 220, 168),
                    new Color(225, 196, 120),
                    new Color(255, 236, 183),
                    new Color(170, 170, 170)));
            return window;
        }

        private static WeddingWishListWindow CreateWeddingWishListWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage soundUIImage,
            GraphicsDevice device)
        {
            WzSubProperty sourceProperty = uiWindow2Image?["Wedding/wishList"] as WzSubProperty
                ?? uiWindow1Image?["Wedding/wishList"] as WzSubProperty;
            if (sourceProperty == null)
            {
                return null;
            }

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;

            WeddingWishListWindow window = new(
                new WeddingWishListWindowAssets(
                    new Dictionary<WeddingWishListDialogMode, WeddingWishListModeAssets>
                    {
                        [WeddingWishListDialogMode.Receive] = CreateWeddingWishListModeAssets(sourceProperty["receive"] as WzSubProperty, device),
                        [WeddingWishListDialogMode.Give] = CreateWeddingWishListModeAssets(sourceProperty["give"] as WzSubProperty, device),
                        [WeddingWishListDialogMode.Input] = CreateWeddingWishListModeAssets(sourceProperty["input"] as WzSubProperty, device)
                    },
                    LoadIndexedCanvasTextureList((sourceProperty["receive"]?["Tab"]?["En"]) as WzSubProperty, device).ToArray(),
                    LoadIndexedCanvasTextureList((sourceProperty["receive"]?["Tab"]?["Ds"]) as WzSubProperty, device).ToArray(),
                    new[]
                    {
                        ResolveCanvasOffset(sourceProperty["receive"]?["Tab"]?["En"]?["0"] as WzCanvasProperty, new Point(216, 69)),
                        ResolveCanvasOffset(sourceProperty["receive"]?["Tab"]?["En"]?["1"] as WzCanvasProperty, new Point(256, 69)),
                        ResolveCanvasOffset(sourceProperty["receive"]?["Tab"]?["En"]?["2"] as WzCanvasProperty, new Point(296, 69)),
                        ResolveCanvasOffset(sourceProperty["receive"]?["Tab"]?["En"]?["3"] as WzCanvasProperty, new Point(336, 69)),
                        ResolveCanvasOffset(sourceProperty["receive"]?["Tab"]?["En"]?["4"] as WzCanvasProperty, new Point(376, 69))
                    },
                    ResolveButtonPosition(sourceProperty["receive"]?["BtGet"] as WzSubProperty, new Point(162, 265)),
                    ResolveButtonPosition(sourceProperty["give"]?["BtPut"] as WzSubProperty, new Point(323, 247)),
                    ResolveButtonPosition(sourceProperty["input"]?["BtEnter"] as WzSubProperty, new Point(145, 57)),
                    ResolveButtonPosition(sourceProperty["input"]?["BtDelete"] as WzSubProperty, new Point(53, 260)),
                    ResolveButtonPosition(sourceProperty["input"]?["BtOK"] as WzSubProperty, new Point(100, 260)),
                    ResolveButtonPosition(sourceProperty["receive"]?["BtExit"] as WzSubProperty, new Point(370, 265))),
                device);

            window.InitializeControls(
                LoadButton(sourceProperty["receive"] as WzSubProperty, "BtGet", clickSound, overSound, device),
                LoadButton(sourceProperty["give"] as WzSubProperty, "BtPut", clickSound, overSound, device),
                LoadButton(sourceProperty["input"] as WzSubProperty, "BtEnter", clickSound, overSound, device),
                LoadButton(sourceProperty["input"] as WzSubProperty, "BtDelete", clickSound, overSound, device),
                LoadButton(sourceProperty["input"] as WzSubProperty, "BtOK", clickSound, overSound, device),
                LoadButton(sourceProperty["receive"] as WzSubProperty, "BtExit", clickSound, overSound, device)
                ?? LoadButton(sourceProperty["give"] as WzSubProperty, "BtExit", clickSound, overSound, device));
            return window;
        }

        private static WeddingWishListWindow CreateFallbackWeddingWishListWindow(GraphicsDevice device)
        {
            WeddingWishListModeAssets CreateMode(int width, int height, Color borderColor, Color headerColor, int selectionWidth, int selectionHeight)
            {
                return new WeddingWishListModeAssets(
                    new Dictionary<WeddingWishListRole, WeddingWishListRoleAssets>
                    {
                        [WeddingWishListRole.Groom] = new(
                            CreateFilledTexture(device, width, height, Color.Transparent, borderColor),
                            CreateFilledTexture(device, Math.Max(1, width - 12), Math.Max(1, height - 28), new Color(242, 242, 242), new Color(210, 210, 210)),
                            new Point(6, 23),
                            CreateFilledTexture(device, Math.Max(1, width - 20), Math.Max(1, height - 60), Color.Transparent, headerColor),
                            new Point(10, 27)),
                        [WeddingWishListRole.Bride] = new(
                            CreateFilledTexture(device, width, height, Color.Transparent, borderColor),
                            CreateFilledTexture(device, Math.Max(1, width - 12), Math.Max(1, height - 28), new Color(242, 242, 242), new Color(210, 210, 210)),
                            new Point(6, 23),
                            CreateFilledTexture(device, Math.Max(1, width - 20), Math.Max(1, height - 60), Color.Transparent, headerColor),
                            new Point(10, 27))
                    },
                    CreateFilledTexture(device, selectionWidth, selectionHeight, new Color(255, 244, 195), new Color(228, 181, 62)));
            }

            WeddingWishListWindow window = new(
                new WeddingWishListWindowAssets(
                    new Dictionary<WeddingWishListDialogMode, WeddingWishListModeAssets>
                    {
                        [WeddingWishListDialogMode.Receive] = CreateMode(423, 290, new Color(72, 72, 72), new Color(80, 148, 214), 148, 35),
                        [WeddingWishListDialogMode.Give] = CreateMode(423, 273, new Color(72, 72, 72), new Color(214, 106, 144), 148, 35),
                        [WeddingWishListDialogMode.Input] = CreateMode(196, 286, new Color(72, 72, 72), new Color(80, 148, 214), 160, 14)
                    },
                    new[]
                    {
                        CreateFilledTexture(device, 39, 19, new Color(115, 179, 235), new Color(49, 98, 152)),
                        CreateFilledTexture(device, 39, 19, new Color(115, 179, 235), new Color(49, 98, 152)),
                        CreateFilledTexture(device, 39, 19, new Color(115, 179, 235), new Color(49, 98, 152)),
                        CreateFilledTexture(device, 39, 19, new Color(115, 179, 235), new Color(49, 98, 152)),
                        CreateFilledTexture(device, 39, 19, new Color(115, 179, 235), new Color(49, 98, 152))
                    },
                    new[]
                    {
                        CreateFilledTexture(device, 39, 19, new Color(210, 210, 210), new Color(124, 124, 124)),
                        CreateFilledTexture(device, 39, 19, new Color(210, 210, 210), new Color(124, 124, 124)),
                        CreateFilledTexture(device, 39, 19, new Color(210, 210, 210), new Color(124, 124, 124)),
                        CreateFilledTexture(device, 39, 19, new Color(210, 210, 210), new Color(124, 124, 124)),
                        CreateFilledTexture(device, 39, 19, new Color(210, 210, 210), new Color(124, 124, 124))
                    },
                    new[] { new Point(216, 69), new Point(256, 69), new Point(296, 69), new Point(336, 69), new Point(376, 69) },
                    new Point(162, 265),
                    new Point(323, 247),
                    new Point(145, 57),
                    new Point(53, 260),
                    new Point(100, 260),
                    new Point(370, 265)),
                device);

            window.InitializeControls(
                UiButtonFactory.CreateSolidButton(device, 44, 16, new Color(240, 220, 168), new Color(225, 196, 120), new Color(255, 236, 183), new Color(170, 170, 170)),
                UiButtonFactory.CreateSolidButton(device, 44, 16, new Color(240, 220, 168), new Color(225, 196, 120), new Color(255, 236, 183), new Color(170, 170, 170)),
                UiButtonFactory.CreateSolidButton(device, 44, 16, new Color(240, 220, 168), new Color(225, 196, 120), new Color(255, 236, 183), new Color(170, 170, 170)),
                UiButtonFactory.CreateSolidButton(device, 44, 16, new Color(240, 220, 168), new Color(225, 196, 120), new Color(255, 236, 183), new Color(170, 170, 170)),
                UiButtonFactory.CreateSolidButton(device, 44, 16, new Color(240, 220, 168), new Color(225, 196, 120), new Color(255, 236, 183), new Color(170, 170, 170)),
                UiButtonFactory.CreateSolidButton(device, 44, 16, new Color(240, 220, 168), new Color(225, 196, 120), new Color(255, 236, 183), new Color(170, 170, 170)));
            return window;
        }

        private static WeddingWishListModeAssets CreateWeddingWishListModeAssets(WzSubProperty modeProperty, GraphicsDevice device)
        {
            if (modeProperty == null)
            {
                return null;
            }

            return new WeddingWishListModeAssets(
                new Dictionary<WeddingWishListRole, WeddingWishListRoleAssets>
                {
                    [WeddingWishListRole.Groom] = CreateWeddingWishListRoleAssets(modeProperty["groom"] as WzSubProperty, device),
                    [WeddingWishListRole.Bride] = CreateWeddingWishListRoleAssets(modeProperty["bride"] as WzSubProperty, device)
                },
                LoadCanvasTexture(modeProperty, "select", device));
        }

        private static WeddingWishListRoleAssets CreateWeddingWishListRoleAssets(WzSubProperty roleProperty, GraphicsDevice device)
        {
            if (roleProperty == null)
            {
                return null;
            }

            return new WeddingWishListRoleAssets(
                LoadCanvasTexture(roleProperty, "backgrnd", device),
                LoadCanvasTexture(roleProperty, "backgrnd2", device),
                ResolveCanvasOffset(roleProperty["backgrnd2"] as WzCanvasProperty, new Point(6, 23)),
                LoadCanvasTexture(roleProperty, "backgrnd3", device),
                ResolveCanvasOffset(roleProperty["backgrnd3"] as WzCanvasProperty, new Point(10, 27)));
        }

        private static Point ResolveButtonPosition(WzSubProperty buttonProperty, Point fallback)
        {
            return ResolveCanvasOffset(buttonProperty?["normal"]?["0"] as WzCanvasProperty, fallback);
        }

        private static EngagementProposalBand LoadEngagementProposalBand(WzSubProperty sourceProperty, GraphicsDevice device, int fallbackHeight)
        {
            return new EngagementProposalBand(
                LoadCanvasTexture(sourceProperty, "left", device) ?? CreateFilledTexture(device, 8, fallbackHeight, new Color(247, 233, 214), new Color(147, 112, 73)),
                LoadCanvasTexture(sourceProperty, "center", device) ?? CreateFilledTexture(device, 1, fallbackHeight, new Color(247, 233, 214), new Color(247, 233, 214)),
                LoadCanvasTexture(sourceProperty, "right", device) ?? CreateFilledTexture(device, 7, fallbackHeight, new Color(247, 233, 214), new Color(147, 112, 73)));
        }

        private static Point ResolveEngagementProposalHeadingOffset(WzSubProperty sourceProperty, string name)
        {
            if (sourceProperty?[name] is not WzCanvasProperty canvas)
            {
                return new Point(92, 2);
            }

            System.Drawing.PointF? origin = canvas.GetCanvasOriginPosition();
            if (!origin.HasValue)
            {
                return new Point(92, 2);
            }

            return new Point(130 - (int)origin.Value.X, (int)origin.Value.Y);
        }

        private static EngagementProposalBand LoadEngagementProposalFallbackBand(GraphicsDevice device, int height)
        {
            return new EngagementProposalBand(
                CreateFilledTexture(device, 8, height, new Color(247, 233, 214), new Color(147, 112, 73)),
                CreateFilledTexture(device, 1, height, new Color(247, 233, 214), new Color(247, 233, 214)),
                CreateFilledTexture(device, 7, height, new Color(247, 233, 214), new Color(147, 112, 73)));
        }



        private static MapleTvVisualAssets LoadMapleTvVisualAssets(WzImage mapleTvImage, GraphicsDevice device)
        {
            if (mapleTvImage == null || device == null)
            {
                return null;
            }


            WzImageProperty mediaProperty = mapleTvImage["TVmedia"];
            WzSubProperty mediaRoot = mediaProperty as WzSubProperty;
            Dictionary<int, IReadOnlyList<MapleTvAnimationFrame>> mediaFrames = new();
            Dictionary<int, IReadOnlyList<MapleTvAnimationFrame>> chatFrames = new();
            if (mediaRoot != null)
            {
                foreach (WzImageProperty child in mediaRoot.WzProperties)
                {
                    if (!int.TryParse(child.Name, out int mediaIndex) || child is not WzSubProperty mediaSubProperty)
                    {
                        continue;
                    }


                    IReadOnlyList<MapleTvAnimationFrame> frames = LoadMapleTvAnimationFrames(mediaSubProperty, device);
                    if (frames.Count > 0)
                    {
                        mediaFrames[mediaIndex] = frames;
                    }
                }
            }

            int explicitWzDefaultMediaIndex = ResolveExplicitMapleTvMediaIndex(mediaProperty, mediaFrames.Keys);


            IReadOnlyList<MapleTvAnimationFrame> defaultChatFrames = LoadMapleTvAnimationFrames(mapleTvImage["TVchat"] as WzSubProperty, device);
            if (defaultChatFrames.Count > 0)
            {
                chatFrames[1] = defaultChatFrames;
            }


            IReadOnlyList<MapleTvAnimationFrame> starChatFrames = LoadMapleTvAnimationFrames(mapleTvImage["TVchat1"] as WzSubProperty, device);
            if (starChatFrames.Count > 0)
            {
                chatFrames[0] = starChatFrames;
            }


            IReadOnlyList<MapleTvAnimationFrame> heartChatFrames = LoadMapleTvAnimationFrames(mapleTvImage["TVchat2"] as WzSubProperty, device);
            if (heartChatFrames.Count > 0)
            {
                chatFrames[2] = heartChatFrames;
            }


            return new MapleTvVisualAssets(
                LoadMapleTvAnimationFrames(mapleTvImage["TVon"] as WzSubProperty, device),
                LoadMapleTvAnimationFrames(mapleTvImage["TVbasic"] as WzSubProperty, device),
                LoadMapleTvAnimationFrames(mapleTvImage["TVoff"] as WzSubProperty, device),
                chatFrames,
                mediaFrames,
                ResolveDefaultMapleTvMediaIndex(mediaFrames, explicitWzDefaultMediaIndex),
                mediaFrames.Keys.ToArray(),
                explicitWzDefaultMediaIndex);
        }


        private static IReadOnlyList<MapleTvAnimationFrame> LoadMapleTvAnimationFrames(WzSubProperty animationProperty, GraphicsDevice device)
        {
            if (animationProperty == null)
            {
                return Array.Empty<MapleTvAnimationFrame>();
            }


            List<MapleTvAnimationFrame> frames = new();
            foreach (WzCanvasProperty canvas in animationProperty.WzProperties
                         .OfType<WzCanvasProperty>()
                         .OrderBy(p => int.TryParse(p.Name, out int frameIndex) ? frameIndex : int.MaxValue)
                         .ThenBy(p => p.Name, StringComparer.Ordinal))
            {
                Texture2D texture = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (texture == null)
                {
                    continue;
                }


                System.Drawing.PointF origin = canvas.GetCanvasOriginPosition();
                int delayMs = canvas[WzCanvasProperty.AnimationDelayPropertyName]?.GetInt() ?? 120;
                frames.Add(new MapleTvAnimationFrame(
                    new DXObject(0, 0, texture, 0),
                    new Point(-(int)origin.X, -(int)origin.Y),
                    delayMs,
                    texture.Width,
                    texture.Height));
            }


            return frames;

        }



        private static int ResolveDefaultMapleTvMediaIndex(
            IReadOnlyDictionary<int, IReadOnlyList<MapleTvAnimationFrame>> mediaFrames,
            int explicitWzDefaultMediaIndex)
        {
            if (mediaFrames == null || mediaFrames.Count == 0)
            {
                return 1;
            }

            string clientDefaultPath = MapleStoryStringPool.GetOrFallback(0x0F8D, "UI/MapleTV.img/TVmedia");
            string clientDefaultPathTemplate = MapleStoryStringPool.GetOrFallback(0x0F8E, "UI/MapleTV.img/TVmedia/%d");
            MapleTvClientInitMediaResolution resolution = MapleTvMediaIndexResolver.ResolveClientInitDefaultMedia(
                clientDefaultPath,
                clientDefaultPathTemplate,
                mediaFrames.Keys.ToArray(),
                1,
                explicitWzDefaultMediaIndex);
            return resolution.MediaIndex;
        }

        private static int ResolveExplicitMapleTvMediaIndex(WzImageProperty mediaProperty, IEnumerable<int> availableMediaIndices)
        {
            if (mediaProperty == null)
            {
                return -1;
            }

            HashSet<int> availableIndexSet = availableMediaIndices?
                .Where(index => index >= 0)
                .ToHashSet()
                ?? new HashSet<int>();
            if (availableIndexSet.Count == 0)
            {
                return -1;
            }

            if (TryResolveMapleTvMediaIndexFromProperty(mediaProperty, availableIndexSet, out int directValue))
            {
                return directValue;
            }

            if (mediaProperty is not WzSubProperty mediaRoot)
            {
                return -1;
            }

            // Some exports surface the default branch as a scalar child under TVmedia.
            // Keep this in the same init seam before falling back to branch-path inference.
            foreach (string candidateName in new[] { "value", "default", "index", "media" })
            {
                if (TryResolveMapleTvMediaIndexFromProperty(mediaRoot[candidateName], availableIndexSet, out int candidateValue))
                {
                    return candidateValue;
                }
            }

            foreach (WzImageProperty child in mediaRoot.WzProperties)
            {
                if (!TryResolveMapleTvMediaIndexFromProperty(child, availableIndexSet, out int candidateValue))
                {
                    continue;
                }

                return candidateValue;
            }

            return -1;
        }

        private static bool TryResolveMapleTvMediaIndexFromProperty(
            WzImageProperty property,
            ISet<int> availableIndices,
            out int mediaIndex)
        {
            mediaIndex = -1;
            if (property == null || availableIndices == null || availableIndices.Count == 0)
            {
                return false;
            }

            int candidateValue;
            switch (property)
            {
                case WzIntProperty intProperty:
                    candidateValue = intProperty.Value;
                    break;

                case WzShortProperty shortProperty:
                    candidateValue = shortProperty.Value;
                    break;

                case WzLongProperty longProperty when longProperty.Value >= int.MinValue && longProperty.Value <= int.MaxValue:
                    candidateValue = (int)longProperty.Value;
                    break;

                case WzStringProperty stringProperty when int.TryParse(stringProperty.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedValue):
                    candidateValue = parsedValue;
                    break;

                default:
                    return false;
            }

            if (!availableIndices.Contains(candidateValue))
            {
                return false;
            }

            mediaIndex = candidateValue;
            return true;
        }



        private static UIWindowBase CreateGuildBbsWindow(
            WzImage guildBbsImage,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty sourceProperty = guildBbsImage?["GuildBBS"] as WzSubProperty;
            Texture2D frameTexture = LoadCanvasTexture(sourceProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.GuildBbs,
                    "Guild BBS",
                    "Fallback owner for the dedicated guild board thread and reply surface.",
                    position);
            }


            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;

            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;



            WzSubProperty emoticonProperty = sourceProperty["Emoticon"] as WzSubProperty;

            WzSubProperty basicEmoticonProperty = emoticonProperty?["Basic"] as WzSubProperty;

            WzSubProperty cashEmoticonProperty = emoticonProperty?["Cash"] as WzSubProperty;



            GuildBbsWindow window = new GuildBbsWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(sourceProperty, "backgrnd2", device, out Point overlayOffset),
                overlayOffset,
                LoadWindowCanvasLayerWithOffset(sourceProperty, "backgrnd3", device, out Point contentOffset),
                contentOffset,
                LoadCanvasTexture(emoticonProperty, "Select", device),
                LoadGuildBbsEmoticonSet(basicEmoticonProperty, GetPropertyChildCount(basicEmoticonProperty, 3), device),
                LoadGuildBbsEmoticonSet(
                    cashEmoticonProperty,
                    GetPropertyChildCount(cashEmoticonProperty, 8),
                    device),
                LoadVerticalScrollbarSkin(basicImage?["VScr9"] as WzSubProperty, device),
                device)
            {
                Position = position
            };


            window.InitializeButtons(
                LoadButton(sourceProperty, "BtRegister", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtCancel", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtNotice", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtWrite", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtRetouch", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtDelete", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtQuit", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtReply", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtReplyDelete", btClickSound, btOverSound, device),
                LoadButtonCopies(sourceProperty, "BtReplyDelete", btClickSound, btOverSound, device, 4),
                LoadButton(sourceProperty["MoveEmoticon"] as WzSubProperty, "BtLeft", btClickSound, btOverSound, device),
                LoadButton(sourceProperty["MoveEmoticon"] as WzSubProperty, "BtRight", btClickSound, btOverSound, device));


            UIObject closeButton = CreateUserInfoCloseButton(basicImage, btClickSound, btOverSound, device, frameTexture.Width);
            if (closeButton != null)
            {
                window.InitializeCloseButton(closeButton);
            }


            return window;

        }



        private static UIWindowBase CreateItemUpgradeWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position,
            IInventoryRuntime inventory)
        {
            WzSubProperty goldHammerProperty = uiWindow2Image?["GoldHammer"] as WzSubProperty;
            WzSubProperty viciousHammerProperty = uiWindow1Image?["ViciousHammer"] as WzSubProperty;
            WzSubProperty enchantSkillProperty = uiWindow2Image?["EnchantSkill"] as WzSubProperty
                ?? uiWindow1Image?["EnchantSkill"] as WzSubProperty;
            WzSubProperty sourceProperty = goldHammerProperty ?? viciousHammerProperty ?? enchantSkillProperty;
            if (sourceProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.ItemUpgrade,
                    "Item Upgrade",
                    "Fallback owner for the dedicated item enhancement flow.",
                    position);
            }


            WzCanvasProperty background = sourceProperty["backgrnd"] as WzCanvasProperty;
            if (background == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.ItemUpgrade,
                    "Item Upgrade",
                    "Fallback owner for the dedicated item enhancement flow.",
                    position);
            }


            Texture2D frameTexture = background.GetLinkedWzCanvasBitmap().ToTexture2DAndDispose(device);
            IDXObject frame = new DXObject(0, 0, frameTexture, 0);
            ItemUpgradeUI itemUpgrade = new ItemUpgradeUI(frame)
            {
                Position = position
            };
            itemUpgrade.SetInventory(inventory);


            Texture2D overlay = LoadCanvasTexture(sourceProperty, "backgrnd2", device);
            Texture2D header = LoadCanvasTexture(sourceProperty, "backgrnd3", device);
            Point overlayOffset = ResolveCanvasOffset(sourceProperty["backgrnd2"] as WzCanvasProperty);
            Point headerOffset = ResolveCanvasOffset(sourceProperty["backgrnd3"] as WzCanvasProperty);
            itemUpgrade.SetDecorations(overlay, overlayOffset, header, headerOffset);


            WzSubProperty gaugeBarProperty = sourceProperty["GaugeBar"] as WzSubProperty;
            if (gaugeBarProperty != null)
            {
                Texture2D gaugeBar = LoadCanvasTexture(gaugeBarProperty, "bar", device);
                Texture2D gaugeFill = LoadCanvasTexture(gaugeBarProperty, "gauge", device);
                Point gaugeOffset = ResolveCanvasOffset(gaugeBarProperty["bar"] as WzCanvasProperty);
                itemUpgrade.SetGaugeTextures(gaugeBar, gaugeFill, gaugeOffset);
            }

            WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];
            WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];
            RegisterItemUpgradeTheme(
                itemUpgrade,
                ItemUpgradeUI.VisualThemeKind.Enhancement,
                sourceProperty,
                btClickSound,
                btOverSound,
                device);
            RegisterItemUpgradeTheme(
                itemUpgrade,
                ItemUpgradeUI.VisualThemeKind.MiracleCube,
                uiWindow2Image?["MiracleCube"] as WzSubProperty
                    ?? uiWindow1Image?["MiracleCube"] as WzSubProperty,
                btClickSound,
                btOverSound,
                device);
            RegisterItemUpgradeTheme(
                itemUpgrade,
                ItemUpgradeUI.VisualThemeKind.HyperMiracleCube,
                uiWindow2Image?["HyperMiracleCube"] as WzSubProperty,
                btClickSound,
                btOverSound,
                device);
            RegisterItemUpgradeTheme(
                itemUpgrade,
                ItemUpgradeUI.VisualThemeKind.MapleMiracleCube,
                uiWindow2Image?["MiracleCube_8th"] as WzSubProperty,
                btClickSound,
                btOverSound,
                device);
            ConfigureCubeAnimationTheme(
                manager,
                ItemUpgradeUI.VisualThemeKind.MiracleCube,
                uiWindow2Image?["MiracleCube"] as WzSubProperty
                    ?? uiWindow1Image?["MiracleCube"] as WzSubProperty,
                device);
            ConfigureCubeAnimationTheme(
                manager,
                ItemUpgradeUI.VisualThemeKind.HyperMiracleCube,
                uiWindow2Image?["HyperMiracleCube"] as WzSubProperty,
                device);
            ConfigureCubeAnimationTheme(
                manager,
                ItemUpgradeUI.VisualThemeKind.MapleMiracleCube,
                uiWindow2Image?["MiracleCube_8th"] as WzSubProperty,
                device);
            UIObject startButton = LoadButton(sourceProperty, "BtStart", btClickSound, btOverSound, device);
            if (startButton == null)
            {
                WzSubProperty basicOk = basicImage?["BtOK"] as WzSubProperty;
                if (basicOk != null)
                {
                    startButton = new UIObject(basicOk, btClickSound, btOverSound, false, Point.Zero, device);
                }
            }


            UIObject cancelButton = LoadButton(sourceProperty, "BtCancel", btClickSound, btOverSound, device);
            if (cancelButton == null)
            {
                WzSubProperty basicCancel = basicImage?["BtCancel"] as WzSubProperty;
                if (basicCancel != null)
                {
                    cancelButton = new UIObject(basicCancel, btClickSound, btOverSound, false, Point.Zero, device);
                }
            }


            UIObject prevButton = null;
            WzSubProperty basicUp = basicImage?["BtUP"] as WzSubProperty;
            if (basicUp != null)
            {
                prevButton = new UIObject(basicUp, btClickSound, btOverSound, false, Point.Zero, device);
            }


            UIObject nextButton = null;
            WzSubProperty basicDown = basicImage?["BtDown"] as WzSubProperty;
            if (basicDown != null)
            {
                nextButton = new UIObject(basicDown, btClickSound, btOverSound, false, Point.Zero, device);
            }
            itemUpgrade.InitializeUpgradeButtons(startButton, cancelButton, prevButton, nextButton);


            WzSubProperty closeButtonProperty = (WzSubProperty)basicImage?["BtClose"];
            if (closeButtonProperty != null)
            {
                try
                {
                    UIObject closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = frameTexture.Width - closeBtn.CanvasSnapshotWidth - 8;
                    closeBtn.Y = 7;
                    itemUpgrade.InitializeCloseButton(closeBtn);
                }
                catch
                {
                }
            }


            return itemUpgrade;
        }
        private static void RegisterItemUpgradeTheme(
            ItemUpgradeUI window,
            ItemUpgradeUI.VisualThemeKind themeKind,
            WzSubProperty sourceProperty,
            WzBinaryProperty btClickSound,
            WzBinaryProperty btOverSound,
            GraphicsDevice device)
        {
            if (window == null || sourceProperty == null)
            {
                return;
            }


            WzCanvasProperty backgroundProperty = sourceProperty["backgrnd"] as WzCanvasProperty;
            if (backgroundProperty == null)
            {
                return;
            }


            Texture2D frameTexture = backgroundProperty.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return;
            }


            WzSubProperty gaugeBarProperty = sourceProperty["GaugeBar"] as WzSubProperty;
            bool useCubePresentationChrome = themeKind == ItemUpgradeUI.VisualThemeKind.MiracleCube ||
                                             themeKind == ItemUpgradeUI.VisualThemeKind.HyperMiracleCube ||
                                             themeKind == ItemUpgradeUI.VisualThemeKind.MapleMiracleCube;
            UIObject actionButton = useCubePresentationChrome
                ? LoadButton(sourceProperty, "BtOk", btClickSound, btOverSound, device)
                : null;
            UIObject cancelButton = useCubePresentationChrome
                ? LoadButton(sourceProperty, "BtCancel", btClickSound, btOverSound, device)
                : null;
            window.RegisterVisualTheme(
                themeKind,
                new ItemUpgradeUI.WindowVisualTheme(
                    new DXObject(0, 0, frameTexture, 0),
                    LoadCanvasTexture(sourceProperty, "backgrnd2", device),
                    ResolveCanvasOffset(sourceProperty["backgrnd2"] as WzCanvasProperty),
                    LoadCanvasTexture(sourceProperty, "backgrnd3", device),
                    ResolveCanvasOffset(sourceProperty["backgrnd3"] as WzCanvasProperty),
                    LoadCanvasTexture(gaugeBarProperty, "bar", device),
                    LoadCanvasTexture(gaugeBarProperty, "gauge", device),
                    ResolveCanvasOffset(gaugeBarProperty?["bar"] as WzCanvasProperty),
                    actionButton,
                    cancelButton,
                    useCubePresentationChrome
                        ? LoadAnimationFrames(sourceProperty["Effect"] as WzSubProperty, device)
                        : null));
        }

        private static void ConfigureCubeAnimationTheme(
            UIWindowManager manager,
            ItemUpgradeUI.VisualThemeKind themeKind,
            WzSubProperty sourceProperty,
            GraphicsDevice device)
        {
            ProductionEnhancementAnimationDisplayer animationDisplayer = manager?.ProductionEnhancementAnimationDisplayer;
            if (animationDisplayer == null || sourceProperty == null || device == null)
            {
                return;
            }

            animationDisplayer.ConfigureCube(
                themeKind,
                LoadWindowOverlayFrames(sourceProperty["Effect"] as WzSubProperty, device),
                Point.Zero);
        }


        private static UIWindowBase CreateVegaSpellWindow(
            WzImage uiWindow1Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty vegaSpellProperty = uiWindow1Image?["VegaSpell"] as WzSubProperty;
            WzCanvasProperty background10 = vegaSpellProperty?["backgrnd10"] as WzCanvasProperty;
            if (vegaSpellProperty == null || background10 == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.VegaSpell,
                    "Vega Spell",
                    "Fallback owner for the dedicated Vega enhancement flow.",
                    position);
            }


            Texture2D frame10Texture = background10.GetLinkedWzCanvasBitmap().ToTexture2DAndDispose(device);
            IDXObject frame10 = new DXObject(0, 0, frame10Texture, 0);
            Texture2D frame60Texture = LoadCanvasTexture(vegaSpellProperty, "backgrnd60", device) ?? frame10Texture;
            IDXObject frame60 = new DXObject(0, 0, frame60Texture, 0);
            VegaSpellUI window = new VegaSpellUI(frame10, device)
            {
                Position = position
            };
            window.SetFrames(frame10, frame60);
            window.SetResultTextures(
                LoadCanvasTexture(vegaSpellProperty, "SuccessWnd", device),
                LoadCanvasTexture(vegaSpellProperty, "FailWnd", device));
            window.SetDigitTextures(LoadDigitTextures(vegaSpellProperty["Count"] as WzSubProperty, device));
            window.SetEffectFrames(
                LoadAnimationFrames(vegaSpellProperty["EffectTwinkling"] as WzSubProperty, device),
                LoadAnimationFrames(vegaSpellProperty["EffectSpelling"] as WzSubProperty, device),
                LoadAnimationFrames(vegaSpellProperty["EffectArrow"] as WzSubProperty, device),
                LoadAnimationFrames(vegaSpellProperty["EffectSuccess"] as WzSubProperty, device),
                LoadAnimationFrames(vegaSpellProperty["EffectFail"] as WzSubProperty, device));


            WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];
            WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];
            WzSubProperty basicUp = basicImage?["BtUP"] as WzSubProperty;
            WzSubProperty basicDown = basicImage?["BtDown"] as WzSubProperty;
            UIObject prevButton = basicUp != null ? new UIObject(basicUp, btClickSound, btOverSound, false, Point.Zero, device) : null;
            UIObject nextButton = basicDown != null ? new UIObject(basicDown, btClickSound, btOverSound, false, Point.Zero, device) : null;
            window.InitializeButtons(
                LoadButton(vegaSpellProperty, "BtStart", btClickSound, btOverSound, device),
                LoadButton(vegaSpellProperty, "BtOK", btClickSound, btOverSound, device),
                LoadButton(vegaSpellProperty, "BtCancel", btClickSound, btOverSound, device),
                prevButton,
                nextButton);


            return window;

        }

        private static void ConfigureVegaAnimationDisplayer(
            UIWindowManager manager,
            WzSubProperty vegaSpellProperty,
            GraphicsDevice device)
        {
            ProductionEnhancementAnimationDisplayer animationDisplayer = manager?.ProductionEnhancementAnimationDisplayer;
            if (animationDisplayer == null || vegaSpellProperty == null || device == null)
            {
                return;
            }

            animationDisplayer.ConfigureVega(
                LoadWindowOverlayFrames(vegaSpellProperty["EffectTwinkling"] as WzSubProperty, device),
                LoadWindowOverlayFrames(vegaSpellProperty["EffectSpelling"] as WzSubProperty, device),
                LoadWindowOverlayFrames(vegaSpellProperty["EffectArrow"] as WzSubProperty, device),
                LoadWindowOverlayFrames(vegaSpellProperty["EffectSuccess"] as WzSubProperty, device),
                LoadWindowOverlayFrames(vegaSpellProperty["EffectFail"] as WzSubProperty, device));
        }



        private static PlaceholderUtilityWindow CreatePlaceholderUtilityWindow(
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            string windowName,
            string title,
            string body,
            Point position)
        {
            const int width = 292;
            const int height = 148;


            Texture2D bgTexture = CreatePlaceholderWindowTexture(device, width, height, title);
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);
            PlaceholderUtilityWindow window = new PlaceholderUtilityWindow(frame, windowName, title, body)
            {
                Position = position
            };


            WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];
            WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];
            WzSubProperty closeButtonProperty = (WzSubProperty)basicImage?["BtClose"];
            if (closeButtonProperty != null)
            {
                try
                {
                    UIObject closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = width - closeBtn.CanvasSnapshotWidth - 8;
                    closeBtn.Y = 8;
                    window.InitializeCloseButton(closeBtn);
                }
                catch
                {
                }
            }


            return window;
        }
        private static UIWindowBase CreateKeyConfigWindow(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty sourceProperty = uiWindow2Image?["KeyConfig"] as WzSubProperty;
            if (sourceProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.KeyConfig,
                    "Key Config",
                    "Fallback owner because UIWindow2.img/KeyConfig assets were unavailable.",
                    position);
            }


            Texture2D frameTexture = LoadCanvasTexture(sourceProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.KeyConfig,
                    "Key Config",
                    "Fallback owner because the client key-config background could not be loaded.",
                    position);
            }


            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            Dictionary<int, Texture2D> mainKeyTextures = LoadIndexedCanvasTextures(sourceProperty["key"] as WzSubProperty, device);
            Dictionary<int, Texture2D> paletteTextures = LoadIndexedCanvasTextures(sourceProperty["icon"] as WzSubProperty, device);
            Texture2D[] noticeTextures = LoadIndexedCanvasArray(sourceProperty["notice"] as WzSubProperty, 3, device);
            Texture2D[] itemNumberTextures = LoadIndexedCanvasArray(basicImage?["ItemNo"] as WzSubProperty, 10, device);
            KeyConfigWindow window = new KeyConfigWindow(
                new DXObject(0, 0, frameTexture, 0),
                MapSimulatorWindowNames.KeyConfig,
                CreateFilledTexture(device, 1, 1, Color.White, Color.White),
                mainKeyTextures,
                mainKeyTextures,
                noticeTextures,
                paletteTextures,
                itemNumberTextures)
            {
                Position = position
            };


            IDXObject overlay = LoadWindowCanvasLayerWithOffset(sourceProperty, "backgrnd2", device, out Point overlayOffset);
            IDXObject content = LoadWindowCanvasLayerWithOffset(sourceProperty, "backgrnd3", device, out Point contentOffset);
            window.AddLayer(overlay, overlayOffset);
            window.AddLayer(content, contentOffset);
            window.InitializeButtons(
                LoadButton(sourceProperty, "BtOK", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtCancel", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtDefault", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtDelete", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtQuickSlot", btClickSound, btOverSound, device));

            WzSubProperty quickSlotProperty = sourceProperty["quickslotConfig"] as WzSubProperty;
            Texture2D quickSlotFrameTexture = LoadCanvasTexture(quickSlotProperty, "backgrnd", device);
            if (quickSlotProperty != null && quickSlotFrameTexture != null)
            {
                Dictionary<int, Texture2D> quickSlotKeyTextures = LoadIndexedCanvasTextures(quickSlotProperty["key"] as WzSubProperty, device);
                if (quickSlotKeyTextures.Count > 0)
                {
                    window = new KeyConfigWindow(
                        new DXObject(0, 0, frameTexture, 0),
                        MapSimulatorWindowNames.KeyConfig,
                        CreateFilledTexture(device, 1, 1, Color.White, Color.White),
                        mainKeyTextures,
                        quickSlotKeyTextures,
                        noticeTextures,
                        paletteTextures,
                        itemNumberTextures)
                    {
                        Position = position
                    };

                    window.AddLayer(overlay, overlayOffset);
                    window.AddLayer(content, contentOffset);
                    window.InitializeButtons(
                        LoadButton(sourceProperty, "BtOK", btClickSound, btOverSound, device),
                        LoadButton(sourceProperty, "BtCancel", btClickSound, btOverSound, device),
                        LoadButton(sourceProperty, "BtDefault", btClickSound, btOverSound, device),
                        LoadButton(sourceProperty, "BtDelete", btClickSound, btOverSound, device),
                        LoadButton(sourceProperty, "BtQuickSlot", btClickSound, btOverSound, device));
                }

                window.ConfigureQuickSlotPage(
                    new DXObject(0, 0, quickSlotFrameTexture, 0),
                    LoadButton(quickSlotProperty, "BtQuickSetting", btClickSound, btOverSound, device),
                    LoadButton(quickSlotProperty, "BtOK", btClickSound, btOverSound, device),
                    LoadButton(quickSlotProperty, "BtCancel", btClickSound, btOverSound, device));
            }
            return window;
        }

        private static Texture2D[] LoadIndexedCanvasArray(WzSubProperty parent, int count, GraphicsDevice device)
        {
            Texture2D[] textures = new Texture2D[Math.Max(0, count)];
            if (parent == null)
            {
                return textures;
            }

            for (int i = 0; i < textures.Length; i++)
            {
                textures[i] = LoadCanvasTexture(parent, i.ToString(CultureInfo.InvariantCulture), device);
            }

            return textures;
        }
        private static UIWindowBase CreateOptionMenuWindow(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty sourceProperty = uiWindow2Image?["OptionMenu"] as WzSubProperty;
            if (sourceProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.OptionMenu,
                    "Option Menu",
                    "Fallback owner because UIWindow2.img/OptionMenu assets were unavailable.",
                    position);
            }


            Texture2D frameTexture = LoadCanvasTexture(sourceProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.OptionMenu,
                    "Option Menu",
                    "Fallback owner because the client option-menu background could not be loaded.",
                    position);
            }


            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            Texture2D checkTexture = LoadCanvasTexture(sourceProperty, "check", device) ?? CreateFilledTexture(device, 1, 1, Color.White, Color.White);
            Texture2D[] scrollTextures = LoadIndexedCanvasArray(sourceProperty["scroll"] as WzSubProperty, 4, device);
            OptionMenuWindow window = new OptionMenuWindow(
                new DXObject(0, 0, frameTexture, 0),
                MapSimulatorWindowNames.OptionMenu,
                checkTexture,
                CreateFilledTexture(device, 1, 1, Color.White, Color.White),
                scrollTextures)
            {
                Position = position
            };


            IDXObject overlay = LoadWindowCanvasLayerWithOffset(sourceProperty, "backgrnd2", device, out Point overlayOffset);
            IDXObject content = LoadWindowCanvasLayerWithOffset(sourceProperty, "backgrnd3", device, out Point contentOffset);
            window.AddLayer(overlay, overlayOffset);
            window.AddLayer(content, contentOffset);
            window.InitializeButtons(
                LoadButton(sourceProperty, "BtOK", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtCancle", btClickSound, btOverSound, device));
            return window;
        }
        private static UIWindowBase CreateRankingWindow(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty sourceProperty = uiWindow2Image?["Ranking"] as WzSubProperty;
            if (sourceProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.Ranking,
                    "Ranking",
                    "Fallback owner because UIWindow2.img/Ranking assets were unavailable.",
                    position);
            }

            Texture2D frameTexture = LoadCanvasTexture(sourceProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.Ranking,
                    "Ranking",
                    "Fallback owner because the client ranking background could not be loaded.",
                    position);
            }

            RankingWindow window = new RankingWindow(
                new DXObject(0, 0, frameTexture, 0),
                MapSimulatorWindowNames.Ranking,
                CreateFilledTexture(device, 1, 1, Color.White, Color.White))
            {
                Position = position
            };

            // Client evidence: CUIRanking::OnCreate loads UIWindow.img/Ranking/Loading
            // and positions the animated layer at x=128, y=161 before NavigateUrl.
            WzImage uiWindowImage = Program.FindImage("UI", "UIWindow.img");
            WzSubProperty rankingLegacyProperty = uiWindowImage?["Ranking"] as WzSubProperty;
            List<RankingWindow.LoadingFrame> loadingFrames = new();
            if (rankingLegacyProperty?["Loading"] is WzSubProperty loadingProperty)
            {
                foreach (WzImageProperty child in loadingProperty.WzProperties
                    .Where(candidate => int.TryParse(candidate.Name, out _))
                    .OrderBy(candidate => int.Parse(candidate.Name, CultureInfo.InvariantCulture)))
                {
                    if (child is not WzCanvasProperty frameCanvas)
                    {
                        continue;
                    }

                    Texture2D texture = frameCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                    if (texture == null)
                    {
                        continue;
                    }

                    int delayMs = InfoTool.GetInt(frameCanvas["delay"], 120);
                    loadingFrames.Add(new RankingWindow.LoadingFrame(texture, delayMs));
                }
            }

            if (loadingFrames.Count > 0)
            {
                window.SetLoadingFrames(loadingFrames, new Point(128, 161));
            }

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject closeButton = LoadButton(sourceProperty, "BtClose", btClickSound, btOverSound, device);
            if (closeButton == null)
            {
                WzSubProperty basicCloseButton = basicImage?["BtClose"] as WzSubProperty;
                if (basicCloseButton != null)
                {
                    try
                    {
                        closeButton = new UIObject(basicCloseButton, btClickSound, btOverSound, false, Point.Zero, device);
                        // Client evidence: CUIEventAlarm::OnCreate positions the close button
                        // at (m_width - 22, 10) after MakeUOLByUIType resolves its art seam.
                        closeButton.X = Math.Max(8, frameTexture.Width - 22);
                        closeButton.Y = 10;
                    }
                    catch
                    {
                        closeButton = null;
                    }
                }
            }

            if (closeButton != null)
            {
                window.InitializeCloseButton(closeButton);
            }
            return window;
        }
        private static UIWindowBase CreateEventWindow(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty eventRoot = uiWindow2Image?["EventList"] as WzSubProperty;
            WzSubProperty sourceProperty = eventRoot?["main"] as WzSubProperty
                ?? uiWindow2Image?["MapleEvent"] as WzSubProperty;
            if (sourceProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.Event,
                    "Event",
                    "Fallback owner because UIWindow2.img/EventList assets were unavailable.",
                    position);
            }

            Texture2D frameTexture = LoadCanvasTexture(sourceProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.Event,
                    "Event",
                    "Fallback owner because the client event background could not be loaded.",
                    position);
            }

            WzSubProperty eventListProperty = sourceProperty["event"] as WzSubProperty;
            Texture2D normalRowTexture = LoadCanvasTexture(eventListProperty, "normal", device);
            Texture2D selectedRowTexture = LoadCanvasTexture(eventListProperty, "select", device) ?? normalRowTexture;
            Texture2D slotTexture = LoadCanvasTexture(eventListProperty, "slot", device);
            LoadIndexedCanvasSequence(
                eventListProperty?["icon"] as WzSubProperty,
                device,
                out Texture2D[] statusIcons,
                out Point[] statusIconOffsets,
                out _);
            WzSubProperty calendarProperty = eventRoot?["calendar"] as WzSubProperty;
            WzSubProperty calendarBackgroundRoot = calendarProperty?["bg"] as WzSubProperty;
            WzSubProperty calendarBackgroundProperty0 = calendarBackgroundRoot?["0"] as WzSubProperty;
            WzSubProperty calendarBackgroundProperty1 = calendarBackgroundRoot?["1"] as WzSubProperty;
            EventWindow window = new EventWindow(
                new DXObject(0, 0, frameTexture, 0),
                MapSimulatorWindowNames.Event,
                normalRowTexture,
                selectedRowTexture,
                slotTexture,
                statusIcons,
                statusIconOffsets,
                LoadCanvasTexture(calendarProperty, "today", device),
                new[]
                {
                    LoadCanvasTexture(calendarBackgroundProperty0, "backgrnd", device),
                    LoadCanvasTexture(calendarBackgroundProperty1, "backgrnd", device)
                },
                new[]
                {
                    LoadCanvasTexture(calendarBackgroundProperty0, "backgrnd2", device),
                    LoadCanvasTexture(calendarBackgroundProperty1, "backgrnd2", device)
                },
                new[]
                {
                    LoadCanvasTexture(calendarBackgroundProperty0, "backgrnd3", device),
                    LoadCanvasTexture(calendarBackgroundProperty1, "backgrnd3", device)
                },
                LoadIndexedCanvasTextureList(calendarProperty?["number"] as WzSubProperty, "normal", device).ToArray(),
                LoadIndexedCanvasTextureList(calendarProperty?["number"] as WzSubProperty, "select", device).ToArray(),
                new Point(226, 5),
                57)
            {
                Position = position
            };

            IDXObject overlay = LoadWindowCanvasLayerWithOffset(sourceProperty, "backgrnd2", device, out Point overlayOffset);
            IDXObject content = LoadWindowCanvasLayerWithOffset(sourceProperty, "backgrnd3", device, out Point contentOffset);
            IDXObject title = LoadWindowCanvasLayerWithOffset(sourceProperty, "title", device, out Point titleOffset);
            window.AddLayer(overlay, overlayOffset);
            window.AddLayer(content, contentOffset);
            window.AddLayer(title, titleOffset);

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject closeButton = LoadButton(sourceProperty, "BtClose", btClickSound, btOverSound, device);
            if (closeButton == null)
            {
                WzSubProperty basicCloseButton = basicImage?["BtClose"] as WzSubProperty;
                if (basicCloseButton != null)
                {
                    try
                    {
                        closeButton = new UIObject(basicCloseButton, btClickSound, btOverSound, false, Point.Zero, device);
                        Point closeButtonPosition = ProgressionUtilityParityRules.ResolveEventAlarmFallbackCloseButtonPosition(frameTexture.Width);
                        closeButton.X = closeButtonPosition.X;
                        closeButton.Y = closeButtonPosition.Y;
                    }
                    catch
                    {
                        closeButton = null;
                    }
                }
            }

            window.InitializeButtons(
                LoadButton(eventListProperty, "BtNone", btClickSound, btOverSound, device),
                LoadButton(eventListProperty, "BtStart", btClickSound, btOverSound, device),
                LoadButton(eventListProperty, "BtIng", btClickSound, btOverSound, device),
                LoadButton(eventListProperty, "BtClear", btClickSound, btOverSound, device),
                LoadButton(eventListProperty, "BtWill", btClickSound, btOverSound, device),
                LoadButton(eventListProperty, "BtCalendar", btClickSound, btOverSound, device),
                LoadButton(calendarProperty, "BtPre", btClickSound, btOverSound, device),
                LoadButton(calendarProperty, "BtNext", btClickSound, btOverSound, device),
                closeButton);
            return window;
        }
        private static UIWindowBase CreateRadioWindow(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzImage uiWindowImage = Program.FindImage("UI", "UIWindow.img");
            WzSubProperty sourceProperty = uiWindowImage?["Radio"] as WzSubProperty
                ?? uiWindow2Image?["Radio"] as WzSubProperty
                ?? uiWindow2Image?["MapleRadio"] as WzSubProperty
                ?? uiWindow2Image?["RadioSchedule"] as WzSubProperty;

            Texture2D radioOffTexture = LoadCanvasTexture(sourceProperty?["Off"] as WzSubProperty, "0", device);
            List<UtilityPanelWindow.IndicatorFrame> radioOnFrames = new();
            if (sourceProperty?["On"] is WzSubProperty radioOnProperty)
            {
                for (int i = 0; ; i++)
                {
                    if (radioOnProperty[i.ToString()] is not WzCanvasProperty frameProperty)
                    {
                        break;
                    }

                    Texture2D frameTexture2D = LoadCanvasTexture(radioOnProperty, i.ToString(), device);
                    if (frameTexture2D == null)
                    {
                        continue;
                    }

                    int delayMs = (frameProperty["delay"] as WzIntProperty)?.Value ?? 150;
                    radioOnFrames.Add(new UtilityPanelWindow.IndicatorFrame(frameTexture2D, delayMs));
                }
            }

            if (radioOffTexture == null && radioOnFrames.Count > 0)
            {
                radioOffTexture = radioOnFrames[0].Texture;
            }

            radioOffTexture ??= CreatePlaceholderWindowTexture(device, 54, 32, "Radio");
            RadioStatusWindow window = new RadioStatusWindow(
                device,
                radioOffTexture,
                radioOnFrames,
                MapSimulatorWindowNames.Radio)
            {
                Position = position
            };
            return window;
        }

        private static UIWindowBase CreateDragonBoxWindow(
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzImage uiWindowImage = Program.FindImage("UI", "UIWindow.img");
            WzSubProperty unsummonableProperty = uiWindowImage?["DragonBall_A"] as WzSubProperty;
            WzSubProperty summonableProperty = uiWindowImage?["DragonBall_B"] as WzSubProperty;
            if (unsummonableProperty == null || summonableProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.DragonBox,
                    "Dragon Box",
                    "Fallback owner because UIWindow.img/DragonBall_A or DragonBall_B assets were unavailable.",
                    position);
            }

            Texture2D unsummonableBackground = LoadCanvasTexture(unsummonableProperty, "backgrnd", device);
            Texture2D summonableBackground = LoadCanvasTexture(summonableProperty, "backgrnd", device);
            if (unsummonableBackground == null || summonableBackground == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.DragonBox,
                    "Dragon Box",
                    "Fallback owner because the Dragon Box background textures could not be loaded.",
                    position);
            }

            int frameWidth = Math.Max(unsummonableBackground.Width, summonableBackground.Width);
            int frameHeight = Math.Max(unsummonableBackground.Height, summonableBackground.Height);
            Texture2D frameTexture = CreateFilledTexture(device, frameWidth, frameHeight, Color.Transparent, Color.Transparent);
            DragonBoxWindow window = new DragonBoxWindow(
                new DXObject(0, 0, frameTexture, 0),
                MapSimulatorWindowNames.DragonBox,
                unsummonableBackground,
                ResolveCanvasOffset(unsummonableProperty, "backgrnd", Point.Zero),
                summonableBackground,
                ResolveCanvasOffset(summonableProperty, "backgrnd", Point.Zero))
            {
                Position = position
            };

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject summonButton = LoadButton(summonableProperty, "BtSummon", btClickSound, btOverSound, device);
            if (summonButton != null)
            {
                window.InitializeSummonButton(summonButton);
            }

            UIObject closeButton = LoadButton(summonableProperty, "BtClose", btClickSound, btOverSound, device)
                ?? LoadButton(unsummonableProperty, "BtClose", btClickSound, btOverSound, device);
            if (closeButton != null)
            {
                window.InitializeCloseButton(closeButton);
            }

            return window;
        }

        private static UIWindowBase CreateAccountMoreInfoWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            const int clientOwnerWidth = 398;
            const int clientOwnerHeight = 355;
            IDXObject frame = CreateAccountMoreInfoClientSizedFrame(
                uiWindow1Image,
                uiWindow2Image,
                device,
                clientOwnerWidth,
                clientOwnerHeight);

            if (frame == null)
            {
                Texture2D frameTexture = CreateFilledTexture(
                    device,
                    clientOwnerWidth,
                    clientOwnerHeight,
                    new Color(28, 34, 45, 230),
                    new Color(86, 100, 130, 255));
                if (frameTexture != null)
                {
                    frame = new DXObject(0, 0, frameTexture, 0);
                }
            }
            if (frame == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.AccountMoreInfo,
                    "Account More Info",
                    "Fallback owner because the account-more-info frame could not be created.",
                    position);
            }

            AccountMoreInfoWindow window = new AccountMoreInfoWindow(
                frame,
                MapSimulatorWindowNames.AccountMoreInfo)
            {
                Position = position
            };
            window.SetOwnerChrome(
                LoadTextureFromClientUiPath("UI/Basic.img/ComboBox/normal/0", basicImage, uiWindow1Image, device),
                LoadTextureFromClientUiPath("UI/Basic.img/ComboBox/normal/1", basicImage, uiWindow1Image, device),
                LoadTextureFromClientUiPath("UI/Basic.img/ComboBox/normal/2", basicImage, uiWindow1Image, device),
                LoadTextureFromClientUiPath("UI/Basic.img/CheckBox/0", basicImage, uiWindow1Image, device),
                LoadTextureFromClientUiPath("UI/Basic.img/CheckBox/1", basicImage, uiWindow1Image, device));

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            WzSubProperty utilDlgProperty = uiWindow2Image?["UtilDlgEx"] as WzSubProperty
                ?? uiWindow1Image?["UtilDlgEx"] as WzSubProperty;
            UIObject okButton = LoadButtonFromUiPath(
                    AccountMoreInfoOwnerStringPoolText.ResolveOkButtonResourcePath(),
                    btClickSound,
                    btOverSound,
                    device,
                    basicImage,
                    uiWindow2Image,
                    uiWindow1Image)
                ?? LoadButton(utilDlgProperty, "BtOK", btClickSound, btOverSound, device);
            UIObject cancelButton = LoadButtonFromUiPath(
                    AccountMoreInfoOwnerStringPoolText.ResolveCancelButtonResourcePath(),
                    btClickSound,
                    btOverSound,
                    device,
                    basicImage,
                    uiWindow2Image,
                    uiWindow1Image)
                ?? LoadButton(utilDlgProperty, "BtCancel", btClickSound, btOverSound, device);
            window.InitializeActionButtons(okButton, cancelButton);

            return window;
        }

        private static IDXObject CreateAccountMoreInfoClientSizedFrame(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            GraphicsDevice device,
            int clientOwnerWidth,
            int clientOwnerHeight)
        {
            string currentDirectoryPath = Program.DataSource?.VersionInfo?.DirectoryPath;
            IReadOnlyList<string> fallbackUiDirectories = AccountMoreInfoOwnerStringPoolText
                .EnumerateFallbackUiDataSourceDirectories(currentDirectoryPath);

            foreach (AccountMoreInfoBackgroundResourceCandidate backgroundCandidate in AccountMoreInfoOwnerStringPoolText.EnumerateBackgroundProbeCandidates())
            {
                IDXObject candidateFrame = LoadWindowCanvasLayerFromClientUiPath(
                    backgroundCandidate.Path,
                    uiWindow1Image,
                    uiWindow2Image,
                    device,
                    out Point candidateOffset);
                string frameSourceDirectory = null;
                if (candidateFrame?.Texture != null)
                {
                    frameSourceDirectory = currentDirectoryPath;
                }

                if (candidateFrame == null)
                {
                    candidateFrame = TryLoadAccountMoreInfoBackgroundFromFallbackUiDataSources(
                        backgroundCandidate.Path,
                        fallbackUiDirectories,
                        device,
                        out candidateOffset,
                        out frameSourceDirectory);
                }

                Texture2D candidateTexture = candidateFrame?.Texture;
                if (candidateTexture == null)
                {
                    continue;
                }

                AccountMoreInfoOwnerStringPoolText.RememberPreferredAccountMoreInfoDataSourceDirectory(frameSourceDirectory);
                if (backgroundCandidate.MirrorsClientSetBackgrnd)
                {
                    return candidateFrame;
                }

                Texture2D clientSizedTexture = CreateAccountMoreInfoClientSizedBackgroundTexture(
                    device,
                    candidateTexture,
                    candidateOffset,
                    clientOwnerWidth,
                    clientOwnerHeight);
                if (clientSizedTexture != null)
                {
                    return new DXObject(0, 0, clientSizedTexture, 0);
                }
            }

            return null;
        }

        private static IDXObject TryLoadAccountMoreInfoBackgroundFromFallbackUiDataSources(
            string clientUiPath,
            IReadOnlyList<string> fallbackUiDirectories,
            GraphicsDevice device,
            out Point offset,
            out string sourceDirectoryPath)
        {
            offset = Point.Zero;
            sourceDirectoryPath = null;
            if (string.IsNullOrWhiteSpace(clientUiPath)
                || device == null
                || fallbackUiDirectories == null
                || fallbackUiDirectories.Count == 0)
            {
                return null;
            }

            foreach (string directory in fallbackUiDirectories)
            {
                try
                {
                    using ImgFileSystemDataSource fallbackDataSource = new(directory);
                    WzImage fallbackUiWindow1Image = fallbackDataSource.GetImage("UI", "UIWindow.img");
                    WzImage fallbackUiWindow2Image = fallbackDataSource.GetImage("UI", "UIWindow2.img");
                    IDXObject frame = LoadWindowCanvasLayerFromClientUiPath(
                        clientUiPath,
                        fallbackUiWindow1Image,
                        fallbackUiWindow2Image,
                        device,
                        out offset);
                    if (frame != null)
                    {
                        sourceDirectoryPath = directory;
                        return frame;
                    }
                }
                catch
                {
                    // Ignore malformed or incompatible sibling data sources and keep searching.
                }
            }

            return null;
        }

        private static Texture2D CreateAccountMoreInfoClientSizedBackgroundTexture(
            GraphicsDevice device,
            Texture2D sourceTexture,
            Point sourceOffset,
            int clientOwnerWidth,
            int clientOwnerHeight)
        {
            if (device == null || sourceTexture == null || clientOwnerWidth <= 0 || clientOwnerHeight <= 0)
            {
                return null;
            }

            if (sourceTexture.Width == clientOwnerWidth
                && sourceTexture.Height == clientOwnerHeight
                && sourceOffset == Point.Zero)
            {
                return sourceTexture;
            }

            // CWnd::SetBackgrnd creates a destination canvas and copies the source
            // canvas into it; it does not seed a synthetic frame color first.
            Color[] clientFrameData = new Color[clientOwnerWidth * clientOwnerHeight];
            Color[] sourceData = new Color[sourceTexture.Width * sourceTexture.Height];
            sourceTexture.GetData(sourceData);

            int destinationStartX = Math.Max(0, sourceOffset.X);
            int destinationStartY = Math.Max(0, sourceOffset.Y);
            int sourceStartX = Math.Max(0, -sourceOffset.X);
            int sourceStartY = Math.Max(0, -sourceOffset.Y);
            int copyWidth = Math.Min(clientOwnerWidth - destinationStartX, sourceTexture.Width - sourceStartX);
            int copyHeight = Math.Min(clientOwnerHeight - destinationStartY, sourceTexture.Height - sourceStartY);
            Texture2D clientSizedTexture = new Texture2D(device, clientOwnerWidth, clientOwnerHeight);
            if (copyWidth <= 0 || copyHeight <= 0)
            {
                clientSizedTexture.SetData(clientFrameData);
                return clientSizedTexture;
            }

            for (int y = 0; y < copyHeight; y++)
            {
                for (int x = 0; x < copyWidth; x++)
                {
                    Color sourceColor = sourceData[((sourceStartY + y) * sourceTexture.Width) + (sourceStartX + x)];
                    if (sourceColor.A != 0)
                    {
                        clientFrameData[((destinationStartY + y) * clientOwnerWidth) + (destinationStartX + x)] = sourceColor;
                    }
                }
            }

            clientSizedTexture.SetData(clientFrameData);
            return clientSizedTexture;
        }

        private static Texture2D LoadTextureFromClientUiPath(
            string clientUiPath,
            WzImage primaryImage,
            WzImage secondaryImage,
            GraphicsDevice device)
        {
            if (string.IsNullOrWhiteSpace(clientUiPath) || device == null)
            {
                return null;
            }

            WzCanvasProperty canvas = ResolveCanvasFromClientUiPath(clientUiPath, primaryImage, secondaryImage);
            return canvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
        }

        private static PlaceholderUtilityWindow CreateWzPlaceholderUtilityWindow(
            WzSubProperty sourceProperty,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            string windowName,
            string title,
            string body,
            Point position)
        {
            Texture2D frameTexture = LoadCanvasTexture(sourceProperty, "backgrnd", device)
                ?? LoadCanvasTexture(sourceProperty, "back", device);
            if (frameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(basicImage, soundUIImage, device, windowName, title, body, position);
            }


            PlaceholderUtilityWindow window = new PlaceholderUtilityWindow(new DXObject(0, 0, frameTexture, 0), windowName, title, body)
            {
                Position = position
            };


            IDXObject overlay = LoadWindowCanvasLayerWithOffset(sourceProperty, "backgrnd2", device, out Point overlayOffset);
            IDXObject content = LoadWindowCanvasLayerWithOffset(sourceProperty, "backgrnd3", device, out Point contentOffset);
            window.AddLayer(overlay, overlayOffset);
            window.AddLayer(content, contentOffset);


            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;

            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;

            UIObject closeButton = LoadButton(sourceProperty, "BtClose", btClickSound, btOverSound, device);
            if (closeButton == null)
            {
                WzSubProperty basicCloseButton = basicImage?["BtClose"] as WzSubProperty;
                if (basicCloseButton != null)
                {
                    try
                    {
                        closeButton = new UIObject(basicCloseButton, btClickSound, btOverSound, false, Point.Zero, device);
                        closeButton.X = Math.Max(8, frameTexture.Width - closeButton.CanvasSnapshotWidth - 8);
                        closeButton.Y = 8;
                    }
                    catch
                    {
                        closeButton = null;
                    }
                }
            }


            if (closeButton != null)
            {
                window.InitializeCloseButton(closeButton);
            }


            return window;

        }



        private static UIWindowBase CreateAdminShopDialogWindow(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            string windowName,
            AdminShopServiceMode defaultMode,
            Point position)
        {
            WzSubProperty shopProperty = uiWindow2Image?["Shop"] as WzSubProperty;
            if (shopProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    windowName,
                    defaultMode == AdminShopServiceMode.CashShop ? "Cash Shop" : "MTS",
                    "Fallback utility owner because UIWindow2.img/Shop assets were unavailable.",
                    position);
            }


            WzCanvasProperty backgroundProperty = shopProperty["backgrnd"] as WzCanvasProperty;
            if (backgroundProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    windowName,
                    defaultMode == AdminShopServiceMode.CashShop ? "Cash Shop" : "MTS",
                    "Fallback utility owner because the shop dialog background could not be loaded.",
                    position);
            }


            Texture2D frameTexture = backgroundProperty.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    windowName,
                    defaultMode == AdminShopServiceMode.CashShop ? "Cash Shop" : "MTS",
                    "Fallback utility owner because the shop dialog texture conversion failed.",
                    position);
            }


            IDXObject frame = new DXObject(0, 0, frameTexture, 0);
            IDXObject frameOverlay = LoadWindowCanvasLayerWithOffset(shopProperty, "backgrnd2", device, out Point frameOverlayOffset);
            IDXObject contentOverlay = LoadWindowCanvasLayerWithOffset(shopProperty, "backgrnd3", device, out Point contentOverlayOffset);
            Texture2D selectTexture = LoadCanvasTexture(shopProperty, "select", device);
            Texture2D mesoTexture = LoadCanvasTexture(shopProperty, "meso", device);
            WzSubProperty tabBuyProperty = shopProperty["TabBuy"] as WzSubProperty;
            WzSubProperty tabBuyEnabledProperty = tabBuyProperty?["enabled"] as WzSubProperty;
            WzSubProperty tabBuyDisabledProperty = tabBuyProperty?["disabled"] as WzSubProperty;
            WzSubProperty tabSellProperty = shopProperty["TabSell"] as WzSubProperty;
            WzSubProperty tabSellEnabledProperty = tabSellProperty?["enabled"] as WzSubProperty;
            WzSubProperty tabSellDisabledProperty = tabSellProperty?["disabled"] as WzSubProperty;
            WzSubProperty tabShopProperty = shopProperty["TabShop"] as WzSubProperty;
            WzSubProperty tabShopEnabledProperty = tabShopProperty?["enabled"] as WzSubProperty;
            WzSubProperty tabShopDisabledProperty = tabShopProperty?["disabled"] as WzSubProperty;
            WzSubProperty fadeYesNoProperty = uiWindow2Image?["FadeYesNo"] as WzSubProperty;
            WzSubProperty utilDlgExProperty = uiWindow2Image?["UtilDlgEx"] as WzSubProperty;

            WzSubProperty basicYesProperty = basicImage?["BtYes"] as WzSubProperty;

            WzSubProperty basicNoProperty = basicImage?["BtNo"] as WzSubProperty;



            Texture2D[] categoryEnabledTextures = new Texture2D[10];
            Texture2D[] categoryDisabledTextures = new Texture2D[10];
            for (int i = 0; i < categoryEnabledTextures.Length; i++)
            {
                string tabKey = i.ToString();
                categoryEnabledTextures[i] = LoadCanvasTexture(tabShopEnabledProperty, tabKey, device);
                categoryDisabledTextures[i] = LoadCanvasTexture(tabShopDisabledProperty, tabKey, device);
            }


            Texture2D modalTexture = LoadCanvasTexture(fadeYesNoProperty, "backgrnd7", device);

            Texture2D[] browseEnabledTextures = new Texture2D[5];
            Texture2D[] browseDisabledTextures = new Texture2D[5];
            Point[] browseOffsets = new Point[5];
            for (int i = 0; i < browseEnabledTextures.Length; i++)
            {
                string tabKey = i.ToString();

                browseEnabledTextures[i] = LoadCanvasTexture(tabBuyEnabledProperty, tabKey, device);

                browseDisabledTextures[i] = LoadCanvasTexture(tabBuyDisabledProperty, tabKey, device);

                browseOffsets[i] = ResolveCanvasOffset(tabBuyEnabledProperty?[tabKey] as WzCanvasProperty);
            }

            Texture2D[] mtsBrowseEnabledTextures = new Texture2D[4];
            Texture2D[] mtsBrowseDisabledTextures = new Texture2D[4];
            Point[] mtsBrowseOffsets = new Point[4];
            for (int i = 0; i < mtsBrowseEnabledTextures.Length; i++)
            {
                string tabKey = (i + 5).ToString(CultureInfo.InvariantCulture);
                mtsBrowseEnabledTextures[i] = LoadCanvasTexture(tabSellEnabledProperty, tabKey, device);
                mtsBrowseDisabledTextures[i] = LoadCanvasTexture(tabSellDisabledProperty, tabKey, device);
                mtsBrowseOffsets[i] = ResolveCanvasOffset(tabSellEnabledProperty?[tabKey] as WzCanvasProperty);
            }


            Texture2D[] quickCategoryEnabledTextures = new Texture2D[5];
            Texture2D[] quickCategoryDisabledTextures = new Texture2D[5];
            Point[] quickCategoryOffsets = new Point[5];
            for (int i = 0; i < quickCategoryEnabledTextures.Length; i++)
            {
                string tabKey = i.ToString();
                quickCategoryEnabledTextures[i] = LoadCanvasTexture(tabSellEnabledProperty, tabKey, device);
                quickCategoryDisabledTextures[i] = LoadCanvasTexture(tabSellDisabledProperty, tabKey, device);
                quickCategoryOffsets[i] = ResolveCanvasOffset(tabSellEnabledProperty?[tabKey] as WzCanvasProperty);
            }


            Point[] categoryOffsets = new Point[10];
            for (int i = 0; i < categoryOffsets.Length; i++)
            {
                categoryOffsets[i] = ResolveCanvasOffset(tabShopEnabledProperty?[i.ToString()] as WzCanvasProperty);
            }


            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject buyButton = LoadButton(shopProperty, "BtBuy", btClickSound, btOverSound, device);
            UIObject sellButton = LoadButton(shopProperty, "BtSell", btClickSound, btOverSound, device);
            UIObject exitButton = LoadButton(shopProperty, "BtExit", btClickSound, btOverSound, device);
            UIObject rechargeButton = LoadButton(shopProperty, "BtRecharge", btClickSound, btOverSound, device);
            UIObject modalConfirmButton = LoadButton(fadeYesNoProperty, "BtOK", btClickSound, btOverSound, device);
            if (modalConfirmButton == null && basicYesProperty != null)
            {
                modalConfirmButton = new UIObject(basicYesProperty, btClickSound, btOverSound, false, Point.Zero, device);
            }


            UIObject modalCancelButton = LoadButton(fadeYesNoProperty, "BtCancel", btClickSound, btOverSound, device);
            if (modalCancelButton == null && basicNoProperty != null)
            {
                modalCancelButton = new UIObject(basicNoProperty, btClickSound, btOverSound, false, Point.Zero, device);
            }
            UIObject modalPreviousButton = LoadButton(utilDlgExProperty, "BtPrev", btClickSound, btOverSound, device);
            UIObject modalNextButton = LoadButton(utilDlgExProperty, "BtNext", btClickSound, btOverSound, device);



            AdminShopDialogUI window = new AdminShopDialogUI(
                frame,
                windowName,
                defaultMode,
                frameOverlay,
                frameOverlayOffset,
                contentOverlay,
                contentOverlayOffset,
                selectTexture,
                mesoTexture,
                buyButton,
                sellButton,
                exitButton,
                rechargeButton,
                modalTexture,
                modalConfirmButton,
                modalCancelButton,
                modalPreviousButton,
                modalNextButton,
                device)
            {
                Position = position,
                Money = 0
            };


            window.SetBrowseTabTextures(browseEnabledTextures, browseDisabledTextures, browseOffsets);
            window.SetMtsBrowseTabTextures(mtsBrowseEnabledTextures, mtsBrowseDisabledTextures, mtsBrowseOffsets);
            window.SetQuickCategoryTabTextures(quickCategoryEnabledTextures, quickCategoryDisabledTextures, quickCategoryOffsets);
            window.SetCategoryTabTextures(categoryEnabledTextures, categoryDisabledTextures, categoryOffsets);


            return window;

        }



        private static UIWindowBase CreateAdminShopWishListWindow(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzImage cashShopImage = global::HaCreator.Program.FindImage("ui", "CashShop.img");
            WzSubProperty searchProperty = cashShopImage?["CSItemSearch"] as WzSubProperty;
            if (searchProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.AdminShopWishList,
                    "Wish List",
                    "Fallback utility owner because CashShop.img/CSItemSearch assets were unavailable.",
                    position);
            }


            Texture2D frameTexture = LoadCanvasTexture(searchProperty["PopUp"] as WzSubProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.AdminShopWishList,
                    "Wish List",
                    "Fallback utility owner because the Cash Shop wish-list frame could not be loaded.",
                    position);
            }


            Texture2D categoryPopupTexture = LoadCanvasTexture(searchProperty["PopUp1"] as WzSubProperty, "backgrnd", device);
            Texture2D searchFieldTexture = LoadCanvasTexture((searchProperty["PopUp"] as WzSubProperty)?["Box"] as WzSubProperty, "0", device);
            WzSubProperty scrollNormalProperty = ((searchProperty["PopUp1"] as WzSubProperty)?["Scroll"] as WzSubProperty)?["normal"] as WzSubProperty;
            Texture2D scrollBaseTexture = LoadCanvasTexture(scrollNormalProperty, "base", device);
            Texture2D scrollThumbTexture = LoadCanvasTexture(scrollNormalProperty, "thumb", device);
            Texture2D scrollPrevTexture = LoadCanvasTexture(scrollNormalProperty, "prev", device);
            Texture2D scrollNextTexture = LoadCanvasTexture(scrollNormalProperty, "next", device);


            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject toggleAddOnButton = LoadButton(searchProperty, "BtAllItem", btClickSound, btOverSound, device);
            UIObject priceRangeButton = LoadButton(searchProperty["PopUp"] as WzSubProperty, "BtComboBox", btClickSound, btOverSound, device);
            UIObject searchButton = LoadButton(searchProperty, "BtSearch", btClickSound, btOverSound, device);
            UIObject resultConfirmButton = LoadButton(searchProperty, "BtBuy", btClickSound, btOverSound, device);
            UIObject resultCancelButton = LoadButton(searchProperty["PopUp1"] as WzSubProperty, "BtCancel", btClickSound, btOverSound, device);
            UIObject closeButton = LoadButton(searchProperty, "BtCancel", btClickSound, btOverSound, device) ?? resultCancelButton;
            if (ReferenceEquals(closeButton, resultCancelButton))
            {
                resultCancelButton = null;
            }


            AdminShopWishListUI window = new AdminShopWishListUI(
                new DXObject(0, 0, frameTexture, 0),
                categoryPopupTexture,
                searchFieldTexture,
                toggleAddOnButton,
                priceRangeButton,
                searchButton,
                resultConfirmButton,
                resultCancelButton,
                closeButton,
                scrollBaseTexture,
                scrollThumbTexture,
                scrollPrevTexture,
                scrollNextTexture,
                device)
            {
                Position = position
            };


            return window;

        }

        private static UIWindowBase CreatePacketOwnedRewardResultNoticeWindow(
            WzImage uiWindowImage,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty utilDialogProperty = uiWindow2Image?["UtilDlgEx"] as WzSubProperty
                ?? uiWindowImage?["UtilDlgEx"] as WzSubProperty;
            Dictionary<int, IDXObject> framesByLineCount = new();
            for (int lineCount = 1; lineCount <= PacketOwnedRewardNoticeWindow.DefaultPrebuiltFrameLineCountLimit; lineCount++)
            {
                int frameHeight = PacketOwnedRewardNoticeWindow.ResolveFrameHeightForBodyLineCount(lineCount);
                Texture2D frameTexture = CreateUtilDlgNoticeFrameTexture(
                    utilDialogProperty,
                    uiWindowImage,
                    uiWindow2Image,
                    device,
                    PacketOwnedRewardNoticeWindow.DefaultFrameWidth,
                    frameHeight);
                if (frameTexture != null)
                {
                    framesByLineCount[lineCount] = new DXObject(0, 0, frameTexture, 0);
                }
            }

            if (framesByLineCount.Count == 0)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.PacketOwnedRewardResultNotice,
                    "Reward Result",
                    "Fallback owner for packet-owned mesos and meso-sack result notices.",
                    position);
            }

            Texture2D separatorLineTexture = LoadCanvasTexture(utilDialogProperty, "line", device)
                ?? LoadTextureFromUiPath(PacketOwnedRewardResultRuntime.GetUtilDlgNoticeSeparatorResourcePath(), device, uiWindow2Image, uiWindowImage);
            if (separatorLineTexture != null
                && separatorLineTexture.Width > PacketOwnedRewardNoticeWindow.DefaultFrameWidth)
            {
                int cropX = Math.Max(0, (separatorLineTexture.Width - PacketOwnedRewardNoticeWindow.DefaultFrameWidth) / 2);
                separatorLineTexture = CropTextureHorizontally(
                    separatorLineTexture,
                    cropX,
                    PacketOwnedRewardNoticeWindow.DefaultFrameWidth,
                    device);
            }
            IDXObject separatorLine = separatorLineTexture != null
                ? new DXObject(0, 0, separatorLineTexture, 0)
                : null;

            PacketOwnedRewardNoticeWindow window = new(framesByLineCount, separatorLine)
            {
                Position = position
            };

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject okButton = LoadButton(utilDialogProperty, "BtOK", btClickSound, btOverSound, device)
                ?? LoadButtonFromUiPath(PacketOwnedRewardResultRuntime.GetUtilDlgNoticeOkButtonResourcePath(), btClickSound, btOverSound, device, uiWindow2Image, uiWindowImage);
            UIObject closeButton = LoadButton(utilDialogProperty, "BtClose", btClickSound, btOverSound, device)
                ?? LoadButtonFromUiPath(PacketOwnedRewardResultRuntime.GetUtilDlgNoticeCloseButtonResourcePath(), btClickSound, btOverSound, device, uiWindow2Image, uiWindowImage);
            if (closeButton == null)
            {
                WzSubProperty basicCloseButton = basicImage?["BtClose"] as WzSubProperty;
                if (basicCloseButton != null)
                {
                    try
                    {
                        closeButton = new UIObject(basicCloseButton, btClickSound, btOverSound, false, Point.Zero, device);
                    }
                    catch
                    {
                        closeButton = null;
                    }
                }
            }
            window.InitializeButtons(okButton, closeButton);
            return window;
        }

        private static UIWindowBase CreateRandomMesoBagWindow(
            WzImage uiWindowImage,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty sourceProperty = uiWindowImage?["RandomMesoBag"] as WzSubProperty
                ?? uiWindow2Image?["RandomMesoBag"] as WzSubProperty;
            Dictionary<int, IDXObject> backgrounds = new();
            Dictionary<int, bool> authoredLayoutByRank = new();
            for (int rank = 1; rank <= 4; rank++)
            {
                string dialogResourcePath = PacketOwnedRewardResultRuntime.GetRandomMesoBagDialogResourcePath(rank);
                bool authoredLayout = false;

                Texture2D backgroundTexture = LoadCanvasTexture(sourceProperty, $"Back{rank}", device);
                if (backgroundTexture != null)
                {
                    authoredLayout = true;
                }
                else
                {
                    backgroundTexture = LoadTextureFromUiPath(dialogResourcePath, device, uiWindowImage, uiWindow2Image);
                    authoredLayout = backgroundTexture != null
                        && dialogResourcePath.IndexOf("RandomMesoBag", StringComparison.OrdinalIgnoreCase) >= 0;
                }

                if (backgroundTexture != null)
                {
                    backgrounds[rank] = new DXObject(0, 0, backgroundTexture, 0);
                    authoredLayoutByRank[rank] = authoredLayout;
                }
            }

            if (backgrounds.Count < 4)
            {
                Texture2D fallbackTexture = CreateUtilDlgNoticeFrameTexture(
                    uiWindow2Image?["UtilDlgEx"] as WzSubProperty,
                    uiWindowImage,
                    uiWindow2Image,
                    device);
                if (fallbackTexture != null)
                {
                    IDXObject fallbackFrame = new DXObject(0, 0, fallbackTexture, 0);
                    for (int rank = 1; rank <= 4; rank++)
                    {
                        if (!backgrounds.ContainsKey(rank))
                        {
                            backgrounds[rank] = fallbackFrame;
                        }

                        // The recovered CUIRandomMesoBag control/text coordinates still apply even when the
                        // authored rank art is missing and the simulator has to fall back to the notice frame.
                        authoredLayoutByRank[rank] = RandomMesoBagWindow.ShouldUseClientCoordinateLayout(
                            authoredLayoutByRank.TryGetValue(rank, out bool hasAuthoredRankArt) && hasAuthoredRankArt,
                            usesFallbackNoticeShell: true);
                    }
                }
            }

            if (backgrounds.Count == 0)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.RandomMesoBag,
                    "Random Meso Sack",
                    "Fallback owner for packet-authored random meso sack results.",
                    position);
            }

            RandomMesoBagWindow window = new(backgrounds, authoredLayoutByRank)
            {
                Position = position
            };

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject okButton = LoadButton(sourceProperty, "BtOk", btClickSound, btOverSound, device)
                ?? LoadButtonFromUiPath(PacketOwnedRewardResultRuntime.GetRandomMesoBagOkButtonResourcePath(), btClickSound, btOverSound, device, uiWindowImage, uiWindow2Image)
                ?? LoadButton(uiWindow2Image?["UtilDlgEx"] as WzSubProperty, "BtOK", btClickSound, btOverSound, device)
                ?? LoadButtonFromUiPath(PacketOwnedRewardResultRuntime.GetUtilDlgNoticeOkButtonResourcePath(), btClickSound, btOverSound, device, uiWindow2Image, uiWindowImage)
                ?? LoadButton(sourceProperty, "BtOK", btClickSound, btOverSound, device);
            window.InitializeButtons(okButton);
            return window;
        }

        private static UIWindowBase CreateRandomMorphWindow(
            WzImage uiWindowImage,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty transformProperty = uiWindowImage?["Transform"] as WzSubProperty
                ?? uiWindow2Image?["Transform"] as WzSubProperty;
            Texture2D backgroundTexture = LoadCanvasTexture(transformProperty, "backgrnd", device)
                ?? CreateUtilDlgNoticeFrameTexture(
                    uiWindow2Image?["UtilDlgEx"] as WzSubProperty,
                    uiWindowImage,
                    uiWindow2Image,
                    device);
            if (backgroundTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.RandomMorph,
                    "Random Morph",
                    "Fallback owner for target-name random morph requests.",
                    position);
            }

            Texture2D pixelTexture = new Texture2D(device, 1, 1);
            pixelTexture.SetData(new[] { Color.White });

            RandomMorphWindow window = new(new DXObject(0, 0, backgroundTexture, 0), pixelTexture);
            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject okButton = LoadButton(transformProperty, "BtOk", btClickSound, btOverSound, device)
                ?? LoadButton(uiWindow2Image?["UtilDlgEx"] as WzSubProperty, "BtOK", btClickSound, btOverSound, device);
            UIObject cancelButton = LoadButton(transformProperty, "BtCancel", btClickSound, btOverSound, device)
                ?? LoadButton(uiWindow2Image?["UtilDlgEx"] as WzSubProperty, "BtCancel", btClickSound, btOverSound, device);
            window.InitializeControls(okButton, cancelButton);
            return window;
        }

        private static Texture2D LoadTextureFromUiPath(string fullPath, GraphicsDevice device, params WzImage[] uiImages)
        {
            if (uiImages == null)
            {
                return null;
            }

            foreach (WzImage uiImage in uiImages)
            {
                WzCanvasProperty canvas = ResolveUiCanvasProperty(uiImage, fullPath);
                Texture2D texture = canvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (texture != null)
                {
                    return texture;
                }
            }

            return null;
        }

        private static UIObject LoadButtonFromUiPath(
            string fullPath,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device,
            params WzImage[] uiImages)
        {
            if (uiImages == null)
            {
                return null;
            }

            foreach (WzImage uiImage in uiImages)
            {
                WzSubProperty buttonProperty = ResolveUiProperty(uiImage, fullPath) as WzSubProperty;
                if (buttonProperty == null)
                {
                    continue;
                }

                try
                {
                    return new UIObject(buttonProperty, clickSound, overSound, false, Point.Zero, device);
                }
                catch
                {
                    // Try the next UI image before falling back to the simulator notice shell.
                }
            }

            return null;
        }

        private static WzCanvasProperty ResolveUiCanvasProperty(WzImage uiWindowImage, string fullPath)
        {
            return ResolveUiProperty(uiWindowImage, fullPath) as WzCanvasProperty;
        }

        private static WzImageProperty ResolveUiProperty(WzImage uiWindowImage, string fullPath)
        {
            if (uiWindowImage == null || string.IsNullOrWhiteSpace(fullPath))
            {
                return null;
            }

            string normalizedPath = fullPath.Replace('\\', '/');
            if (normalizedPath.StartsWith("UI/", StringComparison.OrdinalIgnoreCase))
            {
                int imagePrefixEnd = normalizedPath.IndexOf('/', 3);
                if (imagePrefixEnd >= 0 && imagePrefixEnd + 1 < normalizedPath.Length)
                {
                    normalizedPath = normalizedPath[(imagePrefixEnd + 1)..];
                }
            }

            return ResolveProperty(uiWindowImage, normalizedPath);
        }

        private static UIWindowBase CreateQuestRewardRaiseWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty raiseProperty = uiWindow2Image?["raise"] as WzSubProperty
                ?? uiWindow1Image?["raise"] as WzSubProperty;
            WzSubProperty backgroundProperty = raiseProperty?["backgrnd"] as WzSubProperty;
            if (backgroundProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.QuestRewardRaise,
                    "Quest Reward",
                    "Fallback owner for quest reward raise selection.",
                    position);
            }


            Texture2D frameTexture = new Texture2D(device, 332, 272);
            Color[] transparentPixels = Enumerable.Repeat(Color.Transparent, frameTexture.Width * frameTexture.Height).ToArray();
            frameTexture.SetData(transparentPixels);

            QuestRewardRaiseWindow window = new QuestRewardRaiseWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadCanvasTexture(backgroundProperty?["top"] as WzSubProperty, "left", device),
                LoadCanvasTexture(backgroundProperty?["top"] as WzSubProperty, "center", device),
                LoadCanvasTexture(backgroundProperty?["top"] as WzSubProperty, "right", device),
                LoadCanvasTexture(backgroundProperty?["center"] as WzSubProperty, "left", device),
                LoadCanvasTexture(backgroundProperty?["center"] as WzSubProperty, "center", device),
                LoadCanvasTexture(backgroundProperty?["center"] as WzSubProperty, "right", device),
                LoadCanvasTexture(backgroundProperty?["bottom"] as WzSubProperty, "left", device),
                LoadCanvasTexture(backgroundProperty?["bottom"] as WzSubProperty, "center", device),
                LoadCanvasTexture(backgroundProperty?["bottom"] as WzSubProperty, "right", device),
                LoadCanvasTexture(raiseProperty?["29"] as WzSubProperty, "0", device),
                device)
            {
                Position = position
            };


            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject okButton = LoadIndexedButton(raiseProperty?["30"] as WzSubProperty, btClickSound, btOverSound, device, new Point(178, 229))
                ?? LoadButton(uiWindow2Image?["UtilDlgEx"] as WzSubProperty, "BtOK", btClickSound, btOverSound, device);
            UIObject cancelButton = LoadIndexedButton(raiseProperty?["31"] as WzSubProperty, btClickSound, btOverSound, device, new Point(255, 229))
                ?? LoadButton(uiWindow2Image?["UtilDlgEx"] as WzSubProperty, "BtQNo", btClickSound, btOverSound, device)
                ?? LoadButton(uiWindow2Image?["UtilDlgEx"] as WzSubProperty, "BtNo", btClickSound, btOverSound, device);
            window.InitializeButtons(okButton, cancelButton);


            UIObject closeButton = LoadButton(backgroundProperty, "BtClose", btClickSound, btOverSound, device);
            if (closeButton != null)
            {
                closeButton.X = frameTexture.Width - closeButton.CanvasSnapshotWidth - 8;
                closeButton.Y = 6;
                window.InitializeCloseButton(closeButton);
            }


            return window;
        }



        private static UIWindowBase CreateAdminShopWishListCategoryWindow(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzImage cashShopImage = global::HaCreator.Program.FindImage("ui", "CashShop.img");
            WzSubProperty searchProperty = cashShopImage?["CSItemSearch"] as WzSubProperty;
            WzSubProperty popupProperty = searchProperty?["PopUp1"] as WzSubProperty;
            if (popupProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.AdminShopWishListCategory,
                    "Wish List Category",
                    "Fallback category add-on because CashShop.img/CSItemSearch/PopUp1 assets were unavailable.",
                    position);
            }


            Texture2D backgroundTexture = LoadCanvasTexture(popupProperty, "backgrnd", device);
            if (backgroundTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.AdminShopWishListCategory,
                    "Wish List Category",
                    "Fallback category add-on because the Cash Shop wish-list category frame could not be loaded.",
                    position);
            }


            WzSubProperty scrollNormalProperty = (popupProperty["Scroll"] as WzSubProperty)?["normal"] as WzSubProperty;
            Texture2D scrollBaseTexture = LoadCanvasTexture(scrollNormalProperty, "base", device);
            Texture2D scrollThumbTexture = LoadCanvasTexture(scrollNormalProperty, "thumb", device);
            Texture2D scrollPrevTexture = LoadCanvasTexture(scrollNormalProperty, "prev", device);
            Texture2D scrollNextTexture = LoadCanvasTexture(scrollNormalProperty, "next", device);


            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject closeButton = LoadButton(popupProperty, "BtCancel", btClickSound, btOverSound, device);


            AdminShopWishListCategoryUI window = new AdminShopWishListCategoryUI(
                new DXObject(0, 0, backgroundTexture, 0),
                backgroundTexture,
                closeButton,
                scrollBaseTexture,
                scrollThumbTexture,
                scrollPrevTexture,
                scrollNextTexture,
                device)
            {
                Position = position
            };


            return window;
        }

        private static UIWindowBase CreateAdminShopWishListSearchResultWindow(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzImage uiWindowImage = global::HaCreator.Program.FindImage("ui", "UIWindow.img");
            WzSubProperty itemSearchProperty = uiWindowImage?["itemSearch"] as WzSubProperty;
            if (itemSearchProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.AdminShopWishListSearchResult,
                    "Wish List Search Result",
                    "Fallback search-result add-on because UIWindow.img/itemSearch assets were unavailable.",
                    position);
            }

            Texture2D backgroundTexture = LoadCanvasTexture(itemSearchProperty, "resultback", device);
            if (backgroundTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.AdminShopWishListSearchResult,
                    "Wish List Search Result",
                    "Fallback search-result add-on because the itemSearch result surface could not be loaded.",
                    position);
            }

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject closeButton = LoadButton(basicImage, "BtHide", btClickSound, btOverSound, device);
            UIObject previousButton = LoadButton(itemSearchProperty, "BtLeft", btClickSound, btOverSound, device);
            UIObject nextButton = LoadButton(itemSearchProperty, "BtRight", btClickSound, btOverSound, device);
            UIObject registerButton = LoadButton(itemSearchProperty, "BtRegist", btClickSound, btOverSound, device);
            Texture2D iconPlaceholderTexture = LoadCanvasTexture(itemSearchProperty, "icon0", device);

            AdminShopWishListSearchResultUI window = new AdminShopWishListSearchResultUI(
                new DXObject(0, 0, backgroundTexture, 0),
                backgroundTexture,
                iconPlaceholderTexture,
                closeButton,
                previousButton,
                nextButton,
                registerButton,
                device)
            {
                Position = position
            };

            return window;
        }



        private static Point ResolveCanvasOffset(WzCanvasProperty canvas)
        {
            System.Drawing.PointF? origin = canvas?.GetCanvasOriginPosition();
            if (!origin.HasValue)
            {
                return Point.Zero;
            }


            return new Point(-(int)origin.Value.X, -(int)origin.Value.Y);

        }



        private static AranSkillGuideUI CreateAranSkillGuideWindowBigBang(
            WzImage uiWindow2Image,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            WzSubProperty aranSkillGuideProperty = uiWindow2Image?["AranSkillGuide"] as WzSubProperty;
            if (aranSkillGuideProperty == null)
            {
                return null;
            }


            IDXObject[] pages = new IDXObject[4];
            int pageWidth = 0;
            int pageHeight = 0;
            for (int i = 0; i < pages.Length; i++)
            {
                WzCanvasProperty pageProperty = aranSkillGuideProperty[i.ToString()] as WzCanvasProperty;
                Texture2D pageTexture = pageProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (pageTexture == null)
                {
                    continue;
                }


                pageWidth = Math.Max(pageWidth, pageTexture.Width);
                pageHeight = Math.Max(pageHeight, pageTexture.Height);
                pages[i] = new DXObject(0, 0, pageTexture, 0);
            }


            if (Array.TrueForAll(pages, page => page == null))
            {
                return null;
            }


            AranSkillGuideUI window = new AranSkillGuideUI(pages)
            {
                Position = new Point(
                    Math.Max(0, (screenWidth - pageWidth) / 2),
                    Math.Max(0, (screenHeight - pageHeight) / 2))
            };


            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject closeBtn = LoadButton(aranSkillGuideProperty, "BtClose", btClickSound, btOverSound, device);
            if (closeBtn != null)
            {
                int closeWidth = Math.Max(0, closeBtn.CanvasSnapshotWidth);
                closeBtn.X = Math.Max(0, pageWidth - closeWidth - 7);
                closeBtn.Y = 7;
                window.InitializeCloseButton(closeBtn);
            }


            return window;

        }



        private static void SeedStarterCraftingInventory(IInventoryRuntime inventory)
        {
            if (inventory == null || inventory.GetItemCount(InventoryType.ETC, 4010001) > 0)
            {
                return;
            }


            inventory.AddItem(InventoryType.ETC, 4010001, null, 30); // Steel Ore
            inventory.AddItem(InventoryType.ETC, 4010002, null, 20); // Mithril Ore
            inventory.AddItem(InventoryType.ETC, 4020008, null, 10); // Black Crystal Ore
            inventory.AddItem(InventoryType.ETC, 4130018, null, 3); // Basic catalyst
            inventory.AddMeso(250000);
        }


        private static void SeedStarterConsumableInventory(IInventoryRuntime inventory)
        {
            if (inventory == null)
            {
                return;
            }


            if (inventory.GetItemCount(InventoryType.USE, 2050004) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2050004, null, 3); // All Cure Potion
            }


            if (inventory.GetItemCount(InventoryType.USE, 2022179) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2022179, null, 2); // Onyx Apple
            }
        }


        private static void SeedStarterTrunkInventory(IStorageRuntime storageRuntime)
        {
            if (storageRuntime == null)
            {
                return;
            }


            storageRuntime.SetMeso(1250000);
            storageRuntime.AddItem(InventoryType.EQUIP, new InventorySlotData { ItemId = 1302000, GradeFrameIndex = 0 });
            storageRuntime.AddItem(InventoryType.USE, new InventorySlotData { ItemId = 2000005, Quantity = 30 });
            storageRuntime.AddItem(InventoryType.SETUP, new InventorySlotData { ItemId = 3010002, Quantity = 1 });
            storageRuntime.AddItem(InventoryType.ETC, new InventorySlotData { ItemId = 4000019, Quantity = 120 });
            storageRuntime.AddItem(InventoryType.CASH, new InventorySlotData { ItemId = 5150040, Quantity = 1 });
        }


        private static void SeedStarterEnhancementInventory(IInventoryRuntime inventory)
        {
            if (inventory == null)
            {
                return;
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049301) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049301, null, 12); // Equipment Enhancement Scroll
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049300) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049300, null, 4); // Advanced Equipment Enhancement Scroll
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049307) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049307, null, 3); // Equipment Enhancement Scroll
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049306) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049306, null, 2); // Advanced Equipment Enhancement Scroll
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049303) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049303, null, 2); // Advanced Equipment Enhancement Scroll
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049309) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049309, null, 2); // 2-Star Enhancement Scroll
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049304) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049304, null, 1); // 3 Star Enhancement Scroll
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049305) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049305, null, 1); // 4 Star Enhancement Scroll
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049308) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049308, null, 1); // 5 Star Enhancement Scroll
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049401) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049401, null, 2); // Potential Scroll
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049400) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049400, null, 1); // Advanced Potential Scroll
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049406) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049406, null, 1); // Special Potential Scroll
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049402) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049402, null, 1); // Special Potential Scroll (legacy)
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049407) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049407, null, 1); // Advanced Potential Scroll
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049408) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049408, null, 1); // Potential Scroll
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049500) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049500, null, 1); // Carved Golden Seal
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049501) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049501, null, 1); // Carved Silver Seal
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049700) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049700, null, 1); // Epic Potential Scroll 100%
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049701) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049701, null, 1); // Epic Potential Scroll 80%
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049702) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049702, null, 1); // Epic Potential Scroll 100%
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049703) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049703, null, 1); // Epic Potential Scroll 100%
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049000) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049000, null, 1); // Reverse Scroll 1%
            }


            if (inventory.GetItemCount(InventoryType.USE, 2049600) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049600, null, 1); // Innocence Scroll 70%
            }


            if (inventory.GetItemCount(InventoryType.USE, 2470000) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2470000, null, 1); // Golden Hammer
            }


            if (inventory.GetItemCount(InventoryType.USE, 2470001) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2470001, null, 1); // Golden Hammer 50%
            }


            if (inventory.GetItemCount(InventoryType.USE, 2470002) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2470002, null, 1); // Golden Hammer 50% (trade block)
            }


            if (inventory.GetItemCount(InventoryType.CASH, 5570000) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5570000, null, 1); // Vicious' Hammer
            }


            if (inventory.GetItemCount(InventoryType.CASH, 5062000) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5062000, null, 2); // Miracle Cube
            }


            if (inventory.GetItemCount(InventoryType.CASH, 5062001) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5062001, null, 1); // Premium Miracle Cube
            }


            if (inventory.GetItemCount(InventoryType.CASH, 5062002) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5062002, null, 1); // Super Miracle Cube
            }


            if (inventory.GetItemCount(InventoryType.CASH, 5062003) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5062003, null, 1); // Revolutionary Miracle Cube
            }


            if (inventory.GetItemCount(InventoryType.CASH, 5062004) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5062004, null, 1); // Golden Miracle Cube
            }


            if (inventory.GetItemCount(InventoryType.CASH, 5062005) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5062005, null, 1); // Enlightening Miracle Cube
            }


            if (inventory.GetItemCount(InventoryType.CASH, 5062100) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5062100, null, 1); // Maple Miracle Cube
            }


            if (inventory.GetItemCount(InventoryType.CASH, 5534000) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5534000, null, 1); // Urete's Time Lab
            }


            if (inventory.GetItemCount(InventoryType.USE, 2040759) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2040759, null, 1); // Vega-enabled 60% scroll family
            }


            if (inventory.GetItemCount(InventoryType.USE, 2040760) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2040760, null, 1); // Vega-enabled 10% scroll family
            }


            if (inventory.GetItemCount(InventoryType.USE, 2047204) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2047204, null, 1); // Tablet for Armor for Diligence
            }


            if (inventory.GetItemCount(InventoryType.USE, 2047304) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2047304, null, 1); // Tablet for Accessory for Diligence
            }


            if (inventory.GetItemCount(InventoryType.CASH, 5610000) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5610000, null, 1); // Vega's Spell(10%)
            }


            if (inventory.GetItemCount(InventoryType.CASH, 5610001) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5610001, null, 1); // Vega's Spell(60%)
            }

            foreach (int itemId in ItemUpgradeUI.GetStarterEnhancementEquipItemIds())
            {
                if (inventory.GetItemCount(InventoryType.EQUIP, itemId) <= 0)
                {
                    inventory.AddItem(InventoryType.EQUIP, itemId, null, 1);
                }
            }
        }


        private static void SeedStarterCompanionInventory(IInventoryRuntime inventory, GraphicsDevice device)
        {
            if (inventory == null)
            {
                return;
            }

            CompanionEquipmentLoader companionLoader = device != null ? new CompanionEquipmentLoader(device) : null;

            int[] starterEquipIds =
            {
                1802000, 1802001, 1802002,
                1942001, 1952001, 1962001, 1972001,
                1612001, 1622001, 1632001, 1642001, 1652001,
                1002140, 1010000, 1040000, 1050000, 1062007, 1072005, 1080000, 1100000
            };


            for (int i = 0; i < starterEquipIds.Length; i++)
            {
                int itemId = starterEquipIds[i];
                bool isCashItem = InventoryItemMetadataResolver.TryResolveTradeRestrictionFlags(
                    itemId,
                    out bool resolvedCashItem,
                    out _,
                    out _)
                    && resolvedCashItem;
                InventoryType inventoryType = isCashItem ? InventoryType.CASH : InventoryType.EQUIP;
                if (inventory.GetItemCount(inventoryType, itemId) > 0)
                {
                    continue;
                }

                CompanionEquipItem companionItem = companionLoader?.LoadCompanionEquipment(itemId);
                HaCreator.MapSimulator.Character.CharacterPart tooltipPart = CompanionEquipmentTooltipPartFactory.CreateTooltipPart(companionItem);

                inventory.AddItem(inventoryType, new InventorySlotData
                {
                    ItemId = itemId,
                    ItemTexture = companionItem?.ItemTexture ?? LoadSeedInventoryItemIcon(itemId, device),
                    Quantity = 1,
                    MaxStackSize = 1,
                    PreferredInventoryType = inventoryType,
                    GradeFrameIndex = 0,
                    ItemName = companionItem?.Name,
                    ItemTypeName = companionItem?.ItemCategory,
                    Description = companionItem?.Description,
                    TooltipPart = tooltipPart
                });
            }
        }


        private static Texture2D LoadSeedInventoryItemIcon(int itemId, GraphicsDevice device)
        {
            if (device == null || !InventoryItemMetadataResolver.TryResolveImageSource(itemId, out string category, out string imagePath))
            {
                return null;
            }


            WzImage itemImage = Program.FindImage(category, imagePath);
            if (itemImage == null)
            {
                return null;
            }


            itemImage.ParseImage();
            string itemText = string.Equals(category, "Character", StringComparison.OrdinalIgnoreCase)
                ? itemId.ToString("D8")
                : itemId.ToString("D7");
            WzSubProperty infoProperty = (itemImage[itemText] as WzSubProperty)?["info"] as WzSubProperty;
            WzCanvasProperty iconCanvas = infoProperty?["iconRaw"] as WzCanvasProperty
                                          ?? infoProperty?["icon"] as WzCanvasProperty;
            return iconCanvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
        }
        #endregion
    }
}
