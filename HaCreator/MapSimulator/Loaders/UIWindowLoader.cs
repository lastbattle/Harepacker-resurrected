using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.UI.Controls;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
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
                loginButton,
                guestLoginButton,
                newButton,
                homePageButton,
                quitButton,
                saveIdButton,
                idLostButton,
                passwordLostButton)
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
                LoadCharacterSelectAnimationFrames(charSelectProperty?["scroll"]?["0"] as WzSubProperty, device),
                LoadCharacterSelectAnimationFrames(charSelectProperty?["effect"]?["0"] as WzSubProperty, device),
                LoadCharacterSelectAnimationFrames(charSelectProperty?["effect"]?["1"] as WzSubProperty, device),
                LoadCharacterSelectAnimationFrames(charSelectProperty?["character"]?["0"] as WzSubProperty, device),
                LoadCharacterSelectAnimationFrames(charSelectProperty?["BtSelect"]?["keyFocused"] as WzSubProperty, device),
                default,
                default)
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
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.LoginCreateCharacter) != null)
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

            Texture2D frameTexture = CreateFilledTexture(device, 520, 360, Color.Transparent, Color.Transparent);



            var stageTextures = new Dictionary<LoginCreateCharacterStage, Texture2D>
            {
                [LoginCreateCharacterStage.RaceSelect] = LoadCanvasTexture(newCharProperty, "charAlert", device),
                [LoginCreateCharacterStage.JobSelect] = LoadCanvasTexture(newCharProperty, "charJob", device),
                [LoginCreateCharacterStage.AvatarSelect] = LoadCanvasTexture(newCharProperty, "charSet", device),
                [LoginCreateCharacterStage.NameSelect] = LoadCanvasTexture(newCharProperty, "charName", device)
            };


            LoginCreateCharacterWindow window = new LoginCreateCharacterWindow(
                new DXObject(0, 0, frameTexture, 0),
                stageTextures,
                LoadIndexedCanvasTextureList(newCharProperty?["jobSelect"] as WzSubProperty, device),
                LoadIndexedCanvasTextureList(newCharProperty?["avatarSel"] as WzSubProperty, "normal", device),
                LoadIndexedCanvasTextureList(newCharProperty?["avatarSel"] as WzSubProperty, "disabled", device),
                LoadCharacterSelectAnimationFrames(newCharProperty?["dice"] as WzSubProperty, device),
                LoadButton(newCharProperty, "BtYes", btClickSound, btOverSound, device),
                LoadButton(newCharProperty, "BtNo", btClickSound, btOverSound, device),
                LoadButton(newCharProperty, "BtCheck", btClickSound, btOverSound, device))
            {
                Position = new Point(Math.Max(24, (screenWidth / 2) - 180), Math.Max(24, (screenHeight / 2) - 120))
            };


            manager.RegisterCustomWindow(window);

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


            Texture2D cardNormalTexture = LoadCanvasTexture(charSelectProperty, "charInfo", device)
                ?? CreateFilledTexture(device, 183, 115, new Color(245, 231, 206, 255), new Color(66, 44, 32, 255));
            Texture2D cardSelectedTexture = LoadCanvasTexture(charSelectProperty, "charInfo1", device)
                ?? cardNormalTexture;
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
            Texture2D emptySlotTexture = LoadCanvasTexture(charSelectProperty?["character"]?["1"] as WzSubProperty, "0", device);
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
            Texture2D hitTexture = CreateFilledTexture(device, 183, 151, Color.Transparent, Color.Transparent);
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
                cardNormalTexture,
                cardSelectedTexture,
                normalNameTagStyle,
                selectedNameTagStyle,
                jobDecorations,
                emptySlotTexture,
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
                cancelButton)
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



            UIObject okButton = LoadButton(utilDlgProperty, "BtOK", btClickSound, btOverSound, device);
            UIObject yesButton = LoadButton(utilDlgProperty, "BtYes", btClickSound, btOverSound, device)
                                 ?? LoadButton(loginNoticeProperty, "BtYes", btClickSound, btOverSound, device);
            UIObject noButton = LoadButton(utilDlgProperty, "BtNo", btClickSound, btOverSound, device)
                                ?? LoadButton(loginNoticeProperty, "BtNo", btClickSound, btOverSound, device);
            UIObject acceptButton = LoadButton(loginNoticeProperty, "BtAccept", btClickSound, btOverSound, device)
                                    ?? okButton;
            UIObject nowButton = LoadButton(loginNoticeProperty, "BtNow", btClickSound, btOverSound, device);
            UIObject laterButton = LoadButton(loginNoticeProperty, "BtLater", btClickSound, btOverSound, device);
            UIObject restartButton = LoadButton(loginNoticeProperty, "BtRestart", btClickSound, btOverSound, device);
            UIObject exitButton = LoadButton(loginNoticeProperty, "BtExit", btClickSound, btOverSound, device);
            UIObject nexonButton = LoadButton(loginNoticeProperty, "BtNexon", btClickSound, btOverSound, device);


            LoginUtilityDialogWindow window = new LoginUtilityDialogWindow(
                new DXObject(0, 0, frameTexture, 0),
                okButton,
                yesButton,
                noButton,
                acceptButton,
                nowButton,
                laterButton,
                restartButton,
                exitButton,
                nexonButton,
                noticeTextTextures)
            {
                Position = new Point(Math.Max(24, (screenWidth / 2) - (frameTexture.Width / 2)), Math.Max(24, (screenHeight / 2) - (frameTexture.Height / 2)))

            };



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
                        aranSkillGuide.Show();
                        manager.BringToFront(aranSkillGuide);
                    };
                }
            }


            SeedStarterCraftingInventory(manager.InventoryWindow as IInventoryRuntime);

            SeedStarterConsumableInventory(manager.InventoryWindow as IInventoryRuntime);

            SimulatorStorageRuntime storageRuntime = new SimulatorStorageRuntime(initialAccountLabel: storageAccountLabel, initialAccountKey: storageAccountKey);


            MapTransferUI mapTransfer = CreateMapTransferWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight);
            if (mapTransfer != null)
            {
                manager.RegisterCustomWindow(mapTransfer);
            }


            TrunkUI trunk = CreateTrunkWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, manager.InventoryWindow as InventoryUI, storageRuntime);
            if (trunk != null)
            {
                manager.RegisterCustomWindow(trunk);
                if (storageRuntime.GetUsedSlotCount() == 0 &&
                    storageRuntime.GetMesoCount() <= 0 &&
                    storageRuntime.GetSlotLimit() == 24)
                {
                    SeedStarterTrunkInventory(storageRuntime);
                }
            }


            WorldMapUI worldMap = CreateWorldMapWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight);
            if (worldMap != null)
            {
                manager.RegisterCustomWindow(worldMap);
            }
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

                characterInfo.BookCollectionRequested = () => manager.ShowWindow(MapSimulatorWindowNames.BookCollection);

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


            RegisterChannelSelectionWindows(manager, null, uiWindow1Image, uiWindow2Image, soundUIImage, device, screenWidth, screenHeight);



            int x = Math.Max(40, (screenWidth / 2) - 160);

            int y = Math.Max(40, (screenHeight / 2) - 120);

            const int cascade = 24;



            RegisterAdminShopWishListWindow(manager, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 3), y + cascade));
            RegisterAdminShopWindow(manager, uiWindow2Image, basicImage, soundUIImage, device,
                MapSimulatorWindowNames.CashShop, AdminShopServiceMode.CashShop,
                new Point(x + cascade, y + cascade),
                storageRuntime);
            RegisterAdminShopWindow(manager, uiWindow2Image, basicImage, soundUIImage, device,
                MapSimulatorWindowNames.Mts, AdminShopServiceMode.Mts,
                new Point(x + (cascade * 2), y + (cascade * 2)),
                storageRuntime);
            RegisterSocialListWindow(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 2), y + (cascade * 5)));
            RegisterFamilyChartWindow(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 3), y + (cascade * 5)));
            RegisterMessengerWindow(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 3), y + (cascade * 3)));
            RegisterEngagementProposalWindow(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 4), y + (cascade * 3)));
            RegisterMapleTvWindow(manager, uiWindow1Image, mapleTvImage, basicImage, soundUIImage, device,
                new Point(x + (cascade * 4), y + (cascade * 2)));
            RegisterItemMakerWindow(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 5), y + (cascade * 5)));
            RegisterBookCollectionWindow(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 6), y + (cascade * 5)));
            RegisterItemUpgradeWindow(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 6), y + (cascade * 6)));
            RegisterVegaSpellWindow(manager, uiWindow1Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 7), y + (cascade * 6)));
            RegisterMemoMailboxWindow(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 7), y + (cascade * 4)));
            RegisterQuestDeliveryWindow(manager, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 6), y + (cascade * 3)));
            RegisterRepairDurabilityWindow(manager, uiWindow2Image, basicImage, soundUIImage, device,

                new Point(x + (cascade * 6), y + (cascade * 4)));
            RegisterQuestAlarmWindow(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device,

                new Point(x + (cascade * 8), y + (cascade * 8)));

            RegisterClassCompetitionWindow(manager, uiWindow2Image, basicImage, soundUIImage, device,

                new Point(x + (cascade * 5), y + (cascade * 2)));
            RegisterKeyConfigWindow(manager, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 4), y + (cascade * 4)));
            RegisterOptionMenuWindow(manager, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 5), y + cascade));
            RegisterRankingWindow(manager, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 6), y + (cascade * 2)));
            RegisterEventWindow(manager, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 7), y + cascade));
            RegisterRadioWindow(manager, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 6), y + (cascade * 4)));
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



            RegisterSocialRoomWindow(manager, CreateMiniRoomWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x, y)));
            RegisterSocialRoomWindow(manager, CreatePersonalShopWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + cascade, y + cascade)));
            RegisterSocialRoomWindow(manager, CreateEntrustedShopWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 2), y + (cascade * 2))));
            RegisterSocialRoomWindow(manager, CreateTradingRoomWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 3), y + (cascade * 3))));
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


            Texture2D worldSelectFrameTexture = LoadCanvasTexture(loginWorldSelectProperty?["scroll"]?["0"] as WzSubProperty, "0_bak", device);
            int worldSelectFrameWidth = worldSelectFrameTexture?.Width ?? 564;
            int worldSelectFrameHeight = worldSelectFrameTexture?.Height ?? 177;
            WorldSelectWindow worldSelectWindow = CreateWorldSelectWindow(loginWorldSelectProperty, clickSound, overSound, device, worldBadges);

            worldSelectWindow.Position = new Point(
                Math.Max(24, (screenWidth / 2) - (worldSelectFrameWidth / 2)),
                Math.Max(24, (screenHeight / 2) - (worldSelectFrameHeight / 2)));

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
            List<(int worldId, UIObject button, Texture2D icon)> worldButtons = new List<(int, UIObject, Texture2D)>();
            foreach (KeyValuePair<int, Texture2D> badge in worldBadges.OrderBy(pair => pair.Key))
            {
                UIObject button = LoadButton(worldButtonProperty, badge.Key.ToString(CultureInfo.InvariantCulture), clickSound, overSound, device)
                    ?? CreateTextureButton(badge.Value, badge.Value);
                if (button == null)
                {
                    continue;
                }
                worldButtons.Add((badge.Key, button, badge.Value));

            }



            return new WorldSelectWindow(
                new DXObject(0, 0, frameTexture, 0),
                overlayTexture,
                overlayOffset,
                null,
                worldButtons,
                LoadButton(loginWorldSelectProperty, "BtViewChoice", clickSound, overSound, device),
                LoadButton(loginWorldSelectProperty, "BtViewAll", clickSound, overSound, device));

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
            for (int channelIndex = 0; channelIndex < 20; channelIndex++)
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
            Texture2D frameTexture = LoadCanvasTexture(loginWorldSelectProperty, "chBackgrn", device)
                ?? LoadCanvasTexture(channelProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return null;
            }


            Dictionary<int, Texture2D> channelIcons = new Dictionary<int, Texture2D>();
            WzSubProperty channelIconProperty = channelProperty?["ch"] as WzSubProperty;
            for (int channelIndex = 0; channelIndex < 20; channelIndex++)
            {
                Texture2D channelTexture = loginChannelProperty?[channelIndex.ToString(CultureInfo.InvariantCulture)] is WzSubProperty loginChannelEntry
                    ? LoadCanvasTexture(loginChannelEntry, "normal", device)
                    : LoadCanvasTexture(channelIconProperty, channelIndex.ToString(CultureInfo.InvariantCulture), device);
                if (channelTexture != null)
                {
                    channelIcons[channelIndex] = channelTexture;
                }
            }


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

                LoadCanvasTexture(loginWorldSelectProperty?["channel"]?["chSelect"] as WzSubProperty, "0", device)
                    ?? LoadCanvasTexture(channelProperty, "channel1", device),
                worldBadges,
                channelIcons);
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


                    wishListWindow.ShowFor(sourceDialog);
                    manager.BringToFront(wishListWindow);
                };
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


            UIWindowBase window = CreateAdminShopWishListWindow(uiWindow2Image, basicImage, soundUIImage, device, position);

            manager.RegisterCustomWindow(window);

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
                uiWindow1Image,
                uiWindow2Image,
                basicImage,
                soundUIImage,
                device,
                position,
                manager.InventoryWindow as IInventoryRuntime);
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


        private static void RegisterClassCompetitionWindow(
            UIWindowManager manager,
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

            UIWindowBase classCompetitionWindow = CreateClassCompetitionWindow(uiWindow2Image, basicImage, soundUIImage, device, position);
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


            UIWindowBase memoMailboxWindow = CreateMemoMailboxWindow(uiWindow1Image, basicImage, soundUIImage, device, position);

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
            Texture2D blackStone = LoadCanvasTexture(stoneRoot?["0"]?["black"] as WzSubProperty, "0", device);
            Texture2D whiteStone = LoadCanvasTexture(stoneRoot?["0"]?["white"] as WzSubProperty, "0", device);
            Texture2D lastBlackStone = LoadCanvasTexture(stoneRoot?["10"]?["black"] as WzSubProperty, "0", device) ?? blackStone;
            Texture2D lastWhiteStone = LoadCanvasTexture(stoneRoot?["10"]?["white"] as WzSubProperty, "0", device) ?? whiteStone;
            window.SetMiniRoomOmokStoneTextures(blackStone, whiteStone, lastBlackStone, lastWhiteStone);
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
            return window;
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


            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            window.BindButton(LoadButton(tradeProperty, "BtTrade", clickSound, overSound, device), () => runtime.ToggleTradeLock(out _));
            window.BindButton(LoadButton(tradeProperty, "BtReset", clickSound, overSound, device), runtime.ResetTrade);
            window.BindButton(LoadButton(tradeProperty, "BtCoin", clickSound, overSound, device), runtime.IncreaseTradeOffer);
            window.BindButton(LoadButton(tradeProperty, "BtClame", clickSound, overSound, device), () => runtime.ToggleTradeAcceptance(out _));
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
            WzSubProperty sourceProperty = uiWindow2Image?["MonsterBook"] as WzSubProperty
                ?? uiWindow1Image?["MonsterBook"] as WzSubProperty;
            if (sourceProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.BookCollection,
                    "Monster Book",
                    "Fallback owner for the dedicated Monster Book surface.",
                    position);
            }


            WzCanvasProperty background = sourceProperty["backgrnd"] as WzCanvasProperty;
            if (background == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.BookCollection,
                    "Monster Book",
                    "Fallback owner for the dedicated Monster Book surface.",
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


            bookCollection.SetMonsterBookArt(
                LoadCanvasTexture(sourceProperty, "cardSlot", device),
                LoadCanvasTexture(sourceProperty, "infoPage", device),
                LoadCanvasTexture(sourceProperty, "cover", device),
                LoadCanvasTexture(sourceProperty, "select", device),
                LoadCanvasTexture(sourceProperty, "fullMark", device));
            WzSubProperty utilDialogProperty = uiWindow2Image?["UtilDlgEx"] as WzSubProperty
                ?? uiWindow1Image?["UtilDlgEx"] as WzSubProperty;
            bookCollection.SetPageMarkerTextures(
                LoadCanvasTexture(utilDialogProperty, "dot0", device),
                LoadCanvasTexture(utilDialogProperty, "dot1", device));

            WzSubProperty leftTabInfoProperty = (sourceProperty["LeftTabInfo"] as WzSubProperty)?["0"] as WzSubProperty;
            WzSubProperty leftTabProperty = sourceProperty["LeftTab"] as WzSubProperty;
            WzSubProperty rightTabProperty = sourceProperty["RightTab"] as WzSubProperty;
            WzSubProperty contextMenuProperty = sourceProperty["ContextMenu"] as WzSubProperty;

            List<Texture2D> leftNormals = new();
            List<Texture2D> leftSelected = new();
            List<Texture2D> leftHover = new();
            List<Texture2D> leftIcons = new();
            if (leftTabProperty != null)
            {
                foreach (WzImageProperty tabEntry in leftTabProperty.WzProperties)
                {
                    if (tabEntry is not WzSubProperty tabProperty)
                    {
                        continue;
                    }

                    leftNormals.Add(LoadCanvasTexture(tabProperty, "normal/0", device));
                    leftSelected.Add(LoadCanvasTexture(tabProperty, "selected/0", device));
                    leftHover.Add(LoadCanvasTexture(tabProperty, "mouseOver/0", device));
                    leftIcons.Add(LoadCanvasTexture(sourceProperty, $"icon/{tabProperty.Name}", device));
                }
            }

            List<Texture2D> rightNormals = new();
            List<Texture2D> rightSelected = new();
            List<Texture2D> rightHover = new();
            List<Texture2D> rightDisabled = new();
            List<MonsterBookDetailTab> rightTabOrder = new();
            if (rightTabProperty != null)
            {
                foreach (WzImageProperty tabEntry in rightTabProperty.WzProperties)
                {
                    if (tabEntry is not WzSubProperty tabProperty)
                    {
                        continue;
                    }

                    rightNormals.Add(LoadCanvasTexture(tabProperty, "normal/0", device));
                    rightSelected.Add(LoadCanvasTexture(tabProperty, "selected/0", device));
                    rightHover.Add(LoadCanvasTexture(tabProperty, "mouseOver/0", device));
                    rightDisabled.Add(LoadCanvasTexture(tabProperty, "disabled/0", device));
                    rightTabOrder.Add(tabProperty.Name switch
                    {
                        "0" => MonsterBookDetailTab.BasicInfo,
                        "1" => MonsterBookDetailTab.Episode,
                        "2" => MonsterBookDetailTab.Rewards,
                        "3" => MonsterBookDetailTab.Habitat,
                        _ => MonsterBookDetailTab.BasicInfo
                    });
                }
            }

            bookCollection.SetMonsterBookTabArt(
                LoadCanvasTexture(leftTabInfoProperty, "normal/0", device),
                LoadCanvasTexture(leftTabInfoProperty, "selected/0", device),
                leftNormals,
                leftSelected,
                leftHover,
                leftIcons,
                rightNormals,
                rightSelected,
                rightHover,
                rightDisabled,
                rightTabOrder);

            bookCollection.SetMonsterBookContextMenuArt(
                LoadCanvasTexture(contextMenuProperty, "t", device),
                LoadCanvasTexture(contextMenuProperty, "c", device),
                LoadCanvasTexture(contextMenuProperty, "s", device));



            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;

            UIObject prevButton = LoadButton(sourceProperty, "arrowLeft", btClickSound, btOverSound, device)
                ?? LoadButton(sourceProperty, "BtPrev", btClickSound, btOverSound, device);
            UIObject nextButton = LoadButton(sourceProperty, "arrowRight", btClickSound, btOverSound, device)
                ?? LoadButton(sourceProperty, "BtNext", btClickSound, btOverSound, device);
            UIObject closeButton = LoadButton(sourceProperty, "BtClose", btClickSound, btOverSound, device);
            UIObject searchButton = LoadButton(sourceProperty, "BtSearch", btClickSound, btOverSound, device);
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
                searchButton.X = 437;
                searchButton.Y = 295;
            }

            bookCollection.InitializeButtons(prevButton, nextButton, closeButton, searchButton);
            bookCollection.InitializeContextMenuButtons(registerButton, releaseButton);
            return bookCollection;

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
                LoadButtonAnimationTextures(sourceProperty?["BtQ"] as WzSubProperty, device));


            window.InitializeControls(
                LoadButton(sourceProperty, "BtAuto", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtQ", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtMax", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtMin", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtDelete", btClickSound, btOverSound, device));


            return window;

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
            List<Texture2D> iconFrames = new List<Texture2D>(iconFrameCount);
            for (int i = 0; i < iconFrameCount; i++)
            {
                Texture2D frame = LoadCanvasTexture(iconProperty, i.ToString(), device);
                if (frame != null)
                {
                    iconFrames.Add(frame);
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

            window.AddLayer(LoadWindowCanvasLayerWithOffset(repairProperty, "backgrnd2", device, out Point overlayOffset), overlayOffset);
            window.AddLayer(LoadWindowCanvasLayerWithOffset(repairProperty, "backgrnd3", device, out Point contentOffset), contentOffset);

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject repairAllButton = LoadButton(repairProperty, "BtAllRepair", btClickSound, btOverSound, device);
            UIObject repairButton = LoadButton(repairProperty, "BtRepair", btClickSound, btOverSound, device);
            if (repairAllButton != null)
            {
                repairAllButton.X = 114;
                repairAllButton.Y = 28;
            }

            if (repairButton != null)
            {
                repairButton.X = 114;
                repairButton.Y = 46;
            }

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



        private static UIWindowBase CreateClassCompetitionWindow(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            Texture2D frameTexture = CreatePlaceholderWindowTexture(device, 312, 389, "Class Competition");
            UtilityPanelWindow window = new UtilityPanelWindow(
                new DXObject(0, 0, frameTexture, 0),
                MapSimulatorWindowNames.ClassCompetition,
                "Class Competition")
            {
                Position = position
            };


            window.SetStaticLines(

                "Packet-owned owner for CUserLocal::OnOpenClassCompetitionPage.",

                "The client only instantiates the singleton from this packet branch, so the simulator keeps the launch page separate from menu-button routing.");



            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;

            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject okButton = LoadButton(uiWindow2Image?["UtilDlgEx"] as WzSubProperty, "BtOK", btClickSound, btOverSound, device);
            if (okButton != null)
            {
                okButton.X = 124;
                okButton.Y = 355;
                window.RegisterButton(okButton, window.Hide);
            }
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



        private static UIWindowBase CreateMemoMailboxWindow(
            WzImage uiWindow1Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty memoProperty = uiWindow1Image?["Memo"] as WzSubProperty;
            WzCanvasProperty background = memoProperty?["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = background?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.MemoMailbox,
                    "Memo",
                    "Fallback owner for the simulator memo inbox and mailbox flow.",
                    position);
            }


            Texture2D unreadTexture = LoadCanvasTexture(memoProperty, "check0", device);
            Texture2D readTexture = LoadCanvasTexture(memoProperty, "check1", device);
            MemoMailboxWindow window = new MemoMailboxWindow(
                new DXObject(0, 0, frameTexture, 0),
                MapSimulatorWindowNames.MemoMailbox,
                device,
                unreadTexture,
                readTexture)
            {
                Position = position
            };


            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            window.InitializeButtons(
                LoadButton(memoProperty, "BtSave", btClickSound, btOverSound, device),
                LoadButton(memoProperty, "BtDelete", btClickSound, btOverSound, device),
                LoadButton(memoProperty, "BtOpen", btClickSound, btOverSound, device));


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
            WzSubProperty memoProperty = uiWindow2Image?["Memo"]?["Get"] as WzSubProperty;
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
            WzSubProperty sheetProperty = memoProperty["sheet"] as WzSubProperty;
            IDXObject sheetLayer = LoadWindowCanvasLayerWithOffset(sheetProperty, "innerCenter", device, out Point sheetOffset)
                ?? LoadWindowCanvasLayerWithOffset(sheetProperty, "innerTop", device, out sheetOffset);


            MemoGetWindow window = new MemoGetWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(memoProperty, "backgrnd2", device, out Point overlayOffset),
                overlayOffset,
                LoadWindowCanvasLayerWithOffset(memoProperty, "backgrnd3", device, out Point headerOffset),
                headerOffset,
                sheetLayer,
                sheetOffset,
                LoadWindowCanvasLayerWithOffset(memoProperty, "line", device, out Point lineOffset),
                lineOffset)
            {
                Position = position
            };


            window.InitializeControls(

                LoadButton(memoProperty, "BtOK", btClickSound, btOverSound, device),

                LoadButton(memoProperty, "BtClame", btClickSound, btOverSound, device));



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
                LoadWindowCanvasLayerWithOffset(sourceProperty, "selected", device, out Point selectedOffset),
                selectedOffset,
                LoadWindowCanvasLayerWithOffset(leaderPlateProperty, "0", device, out Point _),
                LoadWindowCanvasLayerWithOffset(leaderPlateProperty, "1", device, out Point _),
                LoadWindowCanvasLayerWithOffset(memberPlateProperty, "0", device, out Point _),
                LoadWindowCanvasLayerWithOffset(memberPlateProperty, "1", device, out Point _),
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


            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            GuildSkillWindow window = new GuildSkillWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(guildSkillProperty, "backgrnd2", device, out Point overlayOffset),
                overlayOffset,
                LoadWindowCanvasLayerWithOffset(guildSkillProperty, "base", device, out Point headerOffset),
                headerOffset,
                LoadCanvasTexture(guildSkillProperty, "skill0", device),
                LoadCanvasTexture(guildSkillProperty, "skill1", device),
                LoadCanvasTexture(guildSkillProperty?["recommend"] as WzSubProperty, "0", device),
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
            UIObject toButton = LoadButton(sourceProperty, "BtTo", btClickSound, btOverSound, device);
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
                    LoadEngagementProposalBand(sourceProperty["top"] as WzSubProperty, device, 35),
                    LoadEngagementProposalBand(sourceProperty["center"] as WzSubProperty, device, 5),
                    LoadEngagementProposalBand(sourceProperty["bottom"] as WzSubProperty, device, 35)),
                device);
            window.InitializeControls(acceptButton);
            return window;
        }

        private static EngagementProposalWindow CreateFallbackEngagementProposalWindow(GraphicsDevice device)
        {
            EngagementProposalWindow window = new(
                new EngagementProposalWindowAssets(
                    LoadEngagementProposalFallbackBand(device, 35),
                    LoadEngagementProposalFallbackBand(device, 5),
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

        private static EngagementProposalBand LoadEngagementProposalBand(WzSubProperty sourceProperty, GraphicsDevice device, int fallbackHeight)
        {
            return new EngagementProposalBand(
                LoadCanvasTexture(sourceProperty, "left", device) ?? CreateFilledTexture(device, 8, fallbackHeight, new Color(247, 233, 214), new Color(147, 112, 73)),
                LoadCanvasTexture(sourceProperty, "center", device) ?? CreateFilledTexture(device, 1, fallbackHeight, new Color(247, 233, 214), new Color(247, 233, 214)),
                LoadCanvasTexture(sourceProperty, "right", device) ?? CreateFilledTexture(device, 7, fallbackHeight, new Color(247, 233, 214), new Color(147, 112, 73)));
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


            WzSubProperty mediaRoot = mapleTvImage["TVmedia"] as WzSubProperty;
            Dictionary<int, IReadOnlyList<MapleTvAnimationFrame>> mediaFrames = new();
            Dictionary<int, IReadOnlyList<MapleTvAnimationFrame>> chatFrames = new();
            if (mediaRoot != null)
            {
                foreach (WzImageProperty child in mediaRoot.WzProperties)
                {
                    if (!int.TryParse(child.Name, out int mediaIndex) || child is not WzSubProperty mediaProperty)
                    {
                        continue;
                    }


                    IReadOnlyList<MapleTvAnimationFrame> frames = LoadMapleTvAnimationFrames(mediaProperty, device);
                    if (frames.Count > 0)
                    {
                        mediaFrames[mediaIndex] = frames;
                    }
                }
            }


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
                ResolveDefaultMapleTvMediaIndex(mediaFrames));
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
                    delayMs));
            }


            return frames;

        }



        private static int ResolveDefaultMapleTvMediaIndex(IReadOnlyDictionary<int, IReadOnlyList<MapleTvAnimationFrame>> mediaFrames)
        {
            if (mediaFrames == null || mediaFrames.Count == 0)
            {
                return 1;
            }


            // WZ ships branch 1 as the neutral, non-episode splash, so prefer it when present.
            if (mediaFrames.ContainsKey(1))
            {
                return 1;
            }


            return mediaFrames.Keys.OrderBy(index => index).First();

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
                LoadGuildBbsEmoticonSet(cashEmoticonProperty, GetPropertyChildCount(cashEmoticonProperty, 8), device),
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
            RegisterItemUpgradeTheme(
                itemUpgrade,
                ItemUpgradeUI.VisualThemeKind.Enhancement,
                sourceProperty,
                device);
            RegisterItemUpgradeTheme(
                itemUpgrade,
                ItemUpgradeUI.VisualThemeKind.MiracleCube,
                uiWindow2Image?["MiracleCube"] as WzSubProperty
                    ?? uiWindow1Image?["MiracleCube"] as WzSubProperty,
                device);
            RegisterItemUpgradeTheme(
                itemUpgrade,
                ItemUpgradeUI.VisualThemeKind.HyperMiracleCube,
                uiWindow2Image?["HyperMiracleCube"] as WzSubProperty,
                device);
            RegisterItemUpgradeTheme(
                itemUpgrade,
                ItemUpgradeUI.VisualThemeKind.MapleMiracleCube,
                uiWindow2Image?["MiracleCube_8th"] as WzSubProperty,
                device);



            WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];
            WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];
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
                    ResolveCanvasOffset(gaugeBarProperty?["bar"] as WzCanvasProperty)));
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
            KeyConfigWindow window = new KeyConfigWindow(
                new DXObject(0, 0, frameTexture, 0),
                MapSimulatorWindowNames.KeyConfig,
                CreateFilledTexture(device, 1, 1, Color.White, Color.White))
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
            return window;
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
            OptionMenuWindow window = new OptionMenuWindow(
                new DXObject(0, 0, frameTexture, 0),
                MapSimulatorWindowNames.OptionMenu,
                checkTexture,
                CreateFilledTexture(device, 1, 1, Color.White, Color.White))
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
            Texture2D[] statusIcons = LoadIndexedCanvasTextureList(eventListProperty?["icon"] as WzSubProperty, device).ToArray();
            WzSubProperty calendarProperty = eventRoot?["calendar"] as WzSubProperty;
            WzSubProperty calendarBackgroundProperty = calendarProperty?["bg"]?["1"] as WzSubProperty
                ?? calendarProperty?["bg"]?["0"] as WzSubProperty;
            EventWindow window = new EventWindow(
                new DXObject(0, 0, frameTexture, 0),
                MapSimulatorWindowNames.Event,
                normalRowTexture,
                selectedRowTexture,
                slotTexture,
                statusIcons,
                LoadCanvasTexture(calendarProperty, "today", device),
                LoadCanvasTexture(calendarBackgroundProperty, "backgrnd", device),
                LoadCanvasTexture(calendarBackgroundProperty, "backgrnd2", device),
                LoadCanvasTexture(calendarBackgroundProperty, "backgrnd3", device),
                LoadIndexedCanvasTextureList(calendarProperty?["number"] as WzSubProperty, "normal", device).ToArray(),
                LoadIndexedCanvasTextureList(calendarProperty?["number"] as WzSubProperty, "select", device).ToArray())
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
                        closeButton.X = Math.Max(8, frameTexture.Width - closeButton.CanvasSnapshotWidth - 8);
                        closeButton.Y = 8;
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
            WzSubProperty sourceProperty = uiWindow2Image?["Radio"] as WzSubProperty
                ?? uiWindow2Image?["MapleRadio"] as WzSubProperty
                ?? uiWindow2Image?["RadioSchedule"] as WzSubProperty;

            Texture2D frameTexture = LoadCanvasTexture(sourceProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                frameTexture = CreatePlaceholderWindowTexture(device, 292, 148, "Radio");
            }

            UtilityPanelWindow window = new UtilityPanelWindow(
                new DXObject(0, 0, frameTexture, 0),
                MapSimulatorWindowNames.Radio,
                "Radio")
            {
                Position = position
            };

            if (sourceProperty != null)
            {
                IDXObject overlay = LoadWindowCanvasLayerWithOffset(sourceProperty, "backgrnd2", device, out Point overlayOffset);
                IDXObject content = LoadWindowCanvasLayerWithOffset(sourceProperty, "backgrnd3", device, out Point contentOffset);
                IDXObject title = LoadWindowCanvasLayerWithOffset(sourceProperty, "title", device, out Point titleOffset);
                window.AddLayer(overlay, overlayOffset);
                window.AddLayer(content, contentOffset);
                window.AddLayer(title, titleOffset);
            }
            else
            {
                window.SetStaticLines("Packet-authored radio playback is idle.");
            }
            return window;
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
            Texture2D frameTexture = LoadCanvasTexture(sourceProperty, "backgrnd", device);
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
            UIObject searchButton = LoadButton(searchProperty, "BtSearch", btClickSound, btOverSound, device);
            UIObject closeButton = LoadButton(searchProperty, "BtCancel", btClickSound, btOverSound, device)
                                   ?? LoadButton(searchProperty["PopUp1"] as WzSubProperty, "BtCancel", btClickSound, btOverSound, device);


            AdminShopWishListUI window = new AdminShopWishListUI(
                new DXObject(0, 0, frameTexture, 0),
                categoryPopupTexture,
                searchFieldTexture,
                toggleAddOnButton,
                searchButton,
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


            if (inventory.GetItemCount(InventoryType.CASH, 5610000) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5610000, null, 1); // Vega's Spell(10%)
            }


            if (inventory.GetItemCount(InventoryType.CASH, 5610001) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5610001, null, 1); // Vega's Spell(60%)
            }


            if (inventory.GetItemCount(InventoryType.EQUIP, 1003243) <= 0)
            {
                inventory.AddItem(InventoryType.EQUIP, 1003243, null, 1); // Maple 8th Anniversary Crimson equip for Maple Miracle Cube req-gated flow
            }
        }


        private static void SeedStarterCompanionInventory(IInventoryRuntime inventory, GraphicsDevice device)
        {
            if (inventory == null)
            {
                return;
            }


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
                if (inventory.GetItemCount(InventoryType.EQUIP, itemId) > 0)
                {
                    continue;
                }


                inventory.AddItem(InventoryType.EQUIP, new InventorySlotData
                {
                    ItemId = itemId,
                    ItemTexture = LoadSeedInventoryItemIcon(itemId, device),
                    Quantity = 1,
                    MaxStackSize = 1,
                    GradeFrameIndex = 0
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
