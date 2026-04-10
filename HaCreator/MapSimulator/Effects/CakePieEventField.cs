using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Effects
{
    public enum CakePieTimerType
    {
        TownUi = 0,
        Ready = 1,
        Start = 2,
        Cake = 3,
        Pie = 4
    }

    public sealed record CakePieEventItemInfo(int FieldId, int ItemId, int Percentage, int EventStatus, int WinnerTeam);

    public sealed class CakePieEventField
    {
        public const string EventOwnerName = "CCakePieEvent";
        public const string TimerboardOwnerName = "CTimerboard_CakePieEvent";
        public const string ItemInfoOwnerName = "CUICakePieEventItemInfo";
        public const int CakeItemId = 4032658;
        public const int PieItemId = 4032659;
        public const int ItemInfoCloseButtonId = 1000;
        public const int ItemInfoCloseButtonX = 212;
        public const int ItemInfoCloseButtonY = 14;
        public const int TownGaugeFillWidth = 142;
        public const int ItemInfoGaugeFillWidth = 116;
        public const int DefaultItemInfoWidth = 256;
        public const int DefaultItemInfoHeight = 114;
        public const int TimerReadyType = 1;
        public const int TimerStartType = 2;
        public const int TimerCakeType = 3;
        public const int TimerPieType = 4;
        public const int TimerMinuteTextX = 273;
        public const int TimerSecondTextX = 333;
        public const int TimerTextY = 34;
        public const int TimerBadgeCenterBaseX = 30;
        public const int TimerBadgeCenterBaseY = 26;
        public const int TimerBadgeCenterWidth = 240;
        public const int TimerBadgeCenterHeight = 40;
        public const int TownGaugeX = 78;
        public const int TownCakeGaugeY = 37;
        public const int TownPieGaugeY = 57;
        public const int TownFlashX = 69;
        public const int TownCakeFlashY = 29;
        public const int TownPieFlashY = 49;
        public const int TownPercentTextX = 227;
        public const int TownCakePercentTextY = 34;
        public const int TownPiePercentTextY = 54;
        public const int ItemInfoRowCount = 8;
        public const int TimerboardBackgroundStringPoolId = 0x162F;
        public const int TimerboardFontStringPoolId = 0x1630;
        public const string TownUiRootPath = "Map/Obj/etc.img/5th_TownUI";
        public const string TownUiBackgroundPath = "Map/Obj/etc.img/5th_TownUI/backgrd";
        public const string TownUiBarEffectPath = "Map/Obj/etc.img/5th_TownUI/bareffect";
        public const string TownUiCakeGaugePath = "Map/Obj/etc.img/5th_TownUI/gage/0";
        public const string TownUiPieGaugePath = "Map/Obj/etc.img/5th_TownUI/gage/1";
        public const string TimerRootPath = "Map/Obj/etc.img/5th_Timer";
        public const string TimerBackgroundPath = "Map/Obj/etc.img/5th_Timer/backgrd";
        public const string TimerFontPath = "Map/Obj/etc.img/5th_Timer/fontTime";
        public const string TimerReadyPath = "Map/Obj/etc.img/5th_Timer/ready";
        public const string TimerStartPath = "Map/Obj/etc.img/5th_Timer/start";
        public const string TimerCakePath = "Map/Obj/etc.img/5th_Timer/cake";
        public const string TimerPiePath = "Map/Obj/etc.img/5th_Timer/pie";
        public const string ItemInfoRootPath = "UI/UIWindow.img/5thevent/WorldUI";
        public const string ItemInfoBackgroundPath = "UI/UIWindow.img/5thevent/WorldUI/back";
        public const string ItemInfoGaugeBarPath = "UI/UIWindow.img/5thevent/WorldUI/bar";
        public const string ItemInfoCakeGaugePath = "UI/UIWindow.img/5thevent/WorldUI/gage/0";
        public const string ItemInfoPieGaugePath = "UI/UIWindow.img/5thevent/WorldUI/gage/1";

        private static readonly int[] ItemInfoFieldIds =
        {
            100000000, 100000000, 200000000, 200000000, 240000000, 240000000, 261000000, 261000000
        };

        private static readonly int[] ItemInfoItemIds =
        {
            CakeItemId, PieItemId, CakeItemId, PieItemId, CakeItemId, PieItemId, CakeItemId, PieItemId
        };

        private static readonly int[] ItemInfoGaugeTopOffsets =
        {
            36, 41, 54, 59, 72, 77, 90, 95
        };

        private readonly Dictionary<(int FieldId, int ItemId), CakePieEventItemInfo> _eventItemInfo = new();
        private readonly Texture2D[] _digitTextures = new Texture2D[10];
        private GraphicsDevice _graphicsDevice;
        private bool _assetsLoaded;
        private bool _isActive;
        private int _currentFieldId;
        private Texture2D _townBackground;
        private Texture2D _townBarEffect;
        private Texture2D _townCakeGauge;
        private Texture2D _townPieGauge;
        private Texture2D _timerBackground;
        private Texture2D _timerReady;
        private Texture2D _timerStart;
        private Texture2D _timerCake;
        private Texture2D _timerPie;
        private Texture2D _itemInfoBackground;
        private Texture2D _itemInfoGaugeBar;
        private Texture2D _itemInfoCakeGauge;
        private Texture2D _itemInfoPieGauge;
        private CakePieTimerType _timerType = CakePieTimerType.TownUi;
        private int _timerDurationSec;
        private int _timeOverTick = int.MinValue;
        private int _lastBlinkTick = int.MinValue;
        private bool _blinkFlag;

        public bool IsActive => _isActive;
        public bool IsTimerboardVisible { get; private set; }
        public bool IsItemInfoVisible { get; private set; }
        public bool HasVisibleUi => IsTimerboardVisible || IsItemInfoVisible;
        public CakePieTimerType TimerType => _timerType;
        public int TimerDurationSeconds => _timerDurationSec;
        public int CurrentFieldId => _currentFieldId;
        public IReadOnlyList<int> ItemInfoRefreshFieldIds => ItemInfoFieldIds;
        public IReadOnlyList<int> ItemInfoRefreshItemIds => ItemInfoItemIds;
        public IReadOnlyCollection<CakePieEventItemInfo> EventItemInfos => _eventItemInfo.Values;

        public int RemainingSeconds
        {
            get
            {
                if (_timeOverTick == int.MinValue)
                {
                    return 0;
                }

                int remainingMs = _timeOverTick - Environment.TickCount;
                return remainingMs <= 0 ? 0 : (remainingMs + 999) / 1000;
            }
        }

        public void Initialize(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _assetsLoaded = false;
        }

        public void BindMap(int fieldId)
        {
            _currentFieldId = fieldId;
            _isActive = IsSupportedField(fieldId);

            if (!_isActive)
            {
                CloseTimerboard();
                CloseItemInfo();
            }
        }

        public void OpenTimerboard(CakePieTimerType timerType, int durationSeconds, int currentTimeMs)
        {
            IsTimerboardVisible = true;
            _timerType = timerType;
            _timerDurationSec = Math.Max(0, durationSeconds);
            _timeOverTick = currentTimeMs + (_timerDurationSec * 1000);
            _lastBlinkTick = int.MinValue;
            _blinkFlag = false;
        }

        public void CloseTimerboard()
        {
            IsTimerboardVisible = false;
            _timeOverTick = int.MinValue;
        }

        public void SetEventItemInfo(int fieldId, int itemId, int percentage, int eventStatus, int winnerTeam)
        {
            CakePieEventItemInfo info = new(
                fieldId,
                itemId,
                Math.Clamp(percentage, 0, 100),
                eventStatus,
                winnerTeam);
            _eventItemInfo[(fieldId, itemId)] = info;
        }

        public bool TryGetEventItemInfo(int fieldId, int itemId, out CakePieEventItemInfo info)
        {
            return _eventItemInfo.TryGetValue((fieldId, itemId), out info);
        }

        public void OpenItemInfo()
        {
            CloseItemInfo();
            IsItemInfoVisible = true;
        }

        public void CloseItemInfo()
        {
            IsItemInfoVisible = false;
        }

        public bool HandleItemInfoButton(int buttonId)
        {
            if (!IsItemInfoVisible || buttonId != ItemInfoCloseButtonId)
            {
                return false;
            }

            CloseItemInfo();
            return true;
        }

        public void Update(int currentTimeMs)
        {
            if (currentTimeMs > _lastBlinkTick)
            {
                _lastBlinkTick = currentTimeMs + 200;
                _blinkFlag = !_blinkFlag;
            }
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (spriteBatch == null || pixelTexture == null)
            {
                return;
            }

            EnsureAssetsLoaded();

            if (IsTimerboardVisible)
            {
                DrawTimerboard(spriteBatch, pixelTexture, font);
            }

            if (IsItemInfoVisible)
            {
                DrawItemInfo(spriteBatch, pixelTexture, font);
            }
        }

        public string DescribeStatus()
        {
            string activeText = _isActive
                ? $"active field={_currentFieldId}"
                : (_currentFieldId > 0 ? $"inactive field={_currentFieldId}" : "inactive");
            string timerText = IsTimerboardVisible
                ? $"{TimerboardOwnerName} type={(int)_timerType}:{_timerType} timer={FormatTimer(RemainingSeconds)} duration={_timerDurationSec}s"
                : $"{TimerboardOwnerName} hidden";
            string itemInfoText = IsItemInfoVisible
                ? $"{ItemInfoOwnerName} visible closeButton={ItemInfoCloseButtonId}@({ItemInfoCloseButtonX},{ItemInfoCloseButtonY}) rows={_eventItemInfo.Count}"
                : $"{ItemInfoOwnerName} hidden rows={_eventItemInfo.Count}";
            return $"{EventOwnerName}: {activeText}; {timerText}; {itemInfoText}; timerAssets=[{TownUiRootPath}; {TimerRootPath}; font={TimerFontPath} via StringPool 0x{TimerboardFontStringPoolId:X}]; itemInfoAssets=[{ItemInfoRootPath}]";
        }

        public void Reset()
        {
            _isActive = false;
            _currentFieldId = 0;
            IsTimerboardVisible = false;
            IsItemInfoVisible = false;
            _timerType = CakePieTimerType.TownUi;
            _timerDurationSec = 0;
            _timeOverTick = int.MinValue;
            _lastBlinkTick = int.MinValue;
            _blinkFlag = false;
            _eventItemInfo.Clear();
        }

        public static bool IsSupportedField(int fieldId)
        {
            return Array.IndexOf(ItemInfoFieldIds, fieldId) >= 0;
        }

        public static bool TryParseTimerType(string token, out CakePieTimerType timerType)
        {
            timerType = CakePieTimerType.TownUi;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            switch (token.Trim().ToLowerInvariant())
            {
                case "town":
                case "townui":
                case "ui":
                case "0":
                    timerType = CakePieTimerType.TownUi;
                    return true;
                case "ready":
                case "1":
                    timerType = CakePieTimerType.Ready;
                    return true;
                case "start":
                case "2":
                    timerType = CakePieTimerType.Start;
                    return true;
                case "cake":
                case "3":
                    timerType = CakePieTimerType.Cake;
                    return true;
                case "pie":
                case "4":
                    timerType = CakePieTimerType.Pie;
                    return true;
                default:
                    return false;
            }
        }

        private void DrawTimerboard(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            int width = _timerType == CakePieTimerType.TownUi
                ? (_townBackground?.Width ?? 258)
                : (_timerBackground?.Width ?? 300);
            int height = _timerType == CakePieTimerType.TownUi
                ? (_townBackground?.Height ?? 80)
                : (_timerBackground?.Height ?? 90);
            Rectangle bounds = new(Math.Max(0, (viewport.Width - width) / 2), 10, width, height);

            if (_timerType == CakePieTimerType.TownUi)
            {
                DrawTownTimerboard(spriteBatch, pixelTexture, font, bounds);
                return;
            }

            if (_timerBackground != null)
            {
                spriteBatch.Draw(_timerBackground, new Vector2(bounds.X, bounds.Y), Color.White);
            }
            else
            {
                spriteBatch.Draw(pixelTexture, bounds, new Color(24, 33, 52, 230));
                spriteBatch.Draw(pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), new Color(226, 197, 91));
            }

            Texture2D badge = ResolveTimerBadgeTexture();
            if (badge != null)
            {
                int badgeX = bounds.X + TimerBadgeCenterBaseX + ((TimerBadgeCenterWidth - badge.Width) / 2);
                int badgeY = bounds.Y + TimerBadgeCenterBaseY + ((TimerBadgeCenterHeight - badge.Height) / 2);
                spriteBatch.Draw(badge, new Vector2(badgeX, badgeY), Color.White);
            }
            else if (font != null)
            {
                DrawShadowedText(spriteBatch, font, _timerType.ToString().ToUpperInvariant(), new Vector2(bounds.X + 42, bounds.Y + 36), Color.Gold, 0.9f);
            }

            if (!TryDrawBitmapTimer(spriteBatch, bounds) && font != null)
            {
                int remaining = Math.Max(0, RemainingSeconds);
                int left = remaining >= 3600 ? Math.Min(99, remaining / 3600) : Math.Min(99, remaining / 60);
                int right = remaining >= 3600 ? Math.Min(59, remaining % 3600 / 60) : Math.Min(59, remaining % 60);
                DrawShadowedText(spriteBatch, font, $"{left:00}", new Vector2(bounds.X + TimerMinuteTextX, bounds.Y + TimerTextY), Color.White);
                DrawShadowedText(spriteBatch, font, $"{right:00}", new Vector2(bounds.X + TimerSecondTextX, bounds.Y + TimerTextY), Color.White);
            }
        }

        private void DrawTownTimerboard(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, Rectangle bounds)
        {
            if (_townBackground != null)
            {
                spriteBatch.Draw(_townBackground, new Vector2(bounds.X, bounds.Y), Color.White);
            }
            else
            {
                spriteBatch.Draw(pixelTexture, bounds, new Color(43, 32, 25, 232));
                spriteBatch.Draw(pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), new Color(247, 213, 128));
            }

            int cakePercent = ResolvePercentage(CakeItemId);
            int piePercent = ResolvePercentage(PieItemId);
            DrawTownGauge(spriteBatch, pixelTexture, font, bounds, cakePercent, TownCakeGaugeY, TownCakeFlashY, TownCakePercentTextY, _townCakeGauge, "Cake");
            DrawTownGauge(spriteBatch, pixelTexture, font, bounds, piePercent, TownPieGaugeY, TownPieFlashY, TownPiePercentTextY, _townPieGauge, "Pie");
        }

        private void DrawTownGauge(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, Rectangle bounds, int percent, int gaugeY, int flashY, int percentTextY, Texture2D gaugeTexture, string label)
        {
            int fillWidth = TownGaugeFillWidth * Math.Clamp(percent, 0, 100) / 100;
            if (fillWidth > 0)
            {
                Rectangle fillRect = new(bounds.X + TownGaugeX, bounds.Y + gaugeY, fillWidth, 8);
                if (gaugeTexture != null)
                {
                    for (int x = 0; x < fillWidth; x++)
                    {
                        spriteBatch.Draw(gaugeTexture, new Rectangle(fillRect.X + x, fillRect.Y, 1, Math.Min(8, gaugeTexture.Height)), Color.White);
                    }
                }
                else
                {
                    spriteBatch.Draw(pixelTexture, fillRect, string.Equals(label, "Cake", StringComparison.Ordinal) ? new Color(242, 118, 93) : new Color(118, 169, 242));
                }
            }

            if (_blinkFlag && percent >= 95 && percent < 100 && _townBarEffect != null)
            {
                spriteBatch.Draw(_townBarEffect, new Vector2(bounds.X + TownFlashX, bounds.Y + flashY), Color.White);
            }

            if (font != null)
            {
                DrawShadowedText(spriteBatch, font, $"{percent}%", new Vector2(bounds.X + TownPercentTextX, bounds.Y + percentTextY), Color.White, 0.82f);
            }
        }

        private void DrawItemInfo(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            int width = _itemInfoBackground?.Width ?? DefaultItemInfoWidth;
            int height = _itemInfoBackground?.Height ?? DefaultItemInfoHeight;
            Rectangle bounds = new(Math.Max(0, (viewport.Width - width) / 2), Math.Max(0, (viewport.Height - height) / 2), width, height);
            if (_itemInfoBackground != null)
            {
                spriteBatch.Draw(_itemInfoBackground, new Vector2(bounds.X, bounds.Y), Color.White);
            }
            else
            {
                spriteBatch.Draw(pixelTexture, bounds, new Color(33, 31, 30, 236));
                spriteBatch.Draw(pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), new Color(247, 213, 128));
            }

            Rectangle close = new(bounds.X + ItemInfoCloseButtonX, bounds.Y + ItemInfoCloseButtonY, 26, 18);
            if (_itemInfoBackground == null)
            {
                spriteBatch.Draw(pixelTexture, close, new Color(116, 47, 47, 235));
            }

            for (int i = 0; i < ItemInfoRowCount; i++)
            {
                int fieldId = ItemInfoFieldIds[i];
                int itemId = ItemInfoItemIds[i];
                TryGetEventItemInfo(fieldId, itemId, out CakePieEventItemInfo info);
                int percent = Math.Clamp(info?.Percentage ?? 0, 0, 100);
                int eventStatus = info?.EventStatus ?? 0;
                int winnerTeam = info?.WinnerTeam ?? 0;
                int y = bounds.Y + ItemInfoGaugeTopOffsets[i];

                if (eventStatus == 1 || eventStatus == 2)
                {
                    if (font != null)
                    {
                        string status = ResolveItemInfoStatusText(eventStatus, winnerTeam);
                        DrawShadowedText(spriteBatch, font, status, new Vector2(bounds.X + 88, y), Color.White, 0.72f);
                    }

                    continue;
                }

                if (_itemInfoGaugeBar != null)
                {
                    spriteBatch.Draw(_itemInfoGaugeBar, new Vector2(bounds.X + 88, y), Color.White);
                }
                else
                {
                    spriteBatch.Draw(pixelTexture, new Rectangle(bounds.X + 88, y, ItemInfoGaugeFillWidth, 8), new Color(16, 16, 16, 180));
                }

                Texture2D gaugeTexture = ResolveItemInfoGaugeTexture(itemId, percent);
                int fillWidth = ItemInfoGaugeFillWidth * percent / 100;
                if (gaugeTexture != null)
                {
                    for (int x = 0; x < fillWidth; x++)
                    {
                        spriteBatch.Draw(gaugeTexture, new Rectangle(bounds.X + 90 + x, y, 1, Math.Min(8, gaugeTexture.Height)), Color.White);
                    }
                }
                else if (fillWidth > 0)
                {
                    spriteBatch.Draw(
                        pixelTexture,
                        new Rectangle(bounds.X + 90, y, fillWidth, 8),
                        itemId == CakeItemId ? new Color(242, 118, 93) : new Color(118, 169, 242));
                }

                if (font != null)
                {
                    DrawShadowedText(spriteBatch, font, $"{percent}%", new Vector2(bounds.X + 210, y - 2), Color.White, 0.68f);
                }
            }
        }

        private static string ResolveItemInfoStatusText(int eventStatus, int winnerTeam)
        {
            return eventStatus switch
            {
                1 => "Battle in Progress!",
                2 => winnerTeam == 1 ? "Cake team has taken over." : "Pie team has taken over.",
                _ => string.Empty
            };
        }

        private Texture2D ResolveTimerBadgeTexture()
        {
            return _timerType switch
            {
                CakePieTimerType.Ready => _timerReady,
                CakePieTimerType.Start => _timerStart,
                CakePieTimerType.Cake => _timerCake,
                CakePieTimerType.Pie => _timerPie,
                _ => null
            };
        }

        private int ResolvePercentage(int itemId)
        {
            CakePieEventItemInfo info = _eventItemInfo.Values
                .FirstOrDefault(entry => (_currentFieldId <= 0 || entry.FieldId == _currentFieldId) && entry.ItemId == itemId)
                ?? _eventItemInfo.Values.FirstOrDefault(entry => entry.ItemId == itemId);
            return info?.Percentage ?? 0;
        }

        private Texture2D ResolveItemInfoGaugeTexture(int itemId, int percent)
        {
            bool blinkAlternate = _blinkFlag && percent >= 95 && percent < 100;
            if (itemId == CakeItemId)
            {
                return blinkAlternate ? (_itemInfoPieGauge ?? _itemInfoCakeGauge) : _itemInfoCakeGauge;
            }

            return blinkAlternate ? (_itemInfoCakeGauge ?? _itemInfoPieGauge) : _itemInfoPieGauge;
        }

        private bool TryDrawBitmapTimer(SpriteBatch spriteBatch, Rectangle bounds)
        {
            if (_digitTextures.Any(texture => texture == null))
            {
                return false;
            }

            int remaining = Math.Max(0, RemainingSeconds);
            int left = remaining >= 3600 ? Math.Min(99, remaining / 3600) : Math.Min(99, remaining / 60);
            int right = remaining >= 3600 ? Math.Min(59, remaining % 3600 / 60) : Math.Min(59, remaining % 60);
            DrawTwoDigits(spriteBatch, bounds, TimerMinuteTextX, TimerTextY, left);
            DrawTwoDigits(spriteBatch, bounds, TimerSecondTextX, TimerTextY, right);
            return true;
        }

        private void DrawTwoDigits(SpriteBatch spriteBatch, Rectangle bounds, int x, int y, int value)
        {
            int tens = (value / 10) % 10;
            int ones = value % 10;
            Texture2D tensTexture = _digitTextures[tens];
            Texture2D onesTexture = _digitTextures[ones];
            spriteBatch.Draw(tensTexture, new Vector2(bounds.X + x, bounds.Y + y), Color.White);
            spriteBatch.Draw(onesTexture, new Vector2(bounds.X + x + tensTexture.Width, bounds.Y + y), Color.White);
        }

        private void EnsureAssetsLoaded()
        {
            if (_assetsLoaded || _graphicsDevice == null)
            {
                return;
            }

            WzImage objImage = global::HaCreator.Program.FindImage("Map", "Obj/etc.img")
                ?? global::HaCreator.Program.FindImage("Map", "etc.img");
            WzImage uiWindowImage = global::HaCreator.Program.FindImage("UI", "UIWindow.img")
                ?? global::HaCreator.Program.FindImage("UI", "UIWindow2.img");
            _townBackground = LoadCanvasTexture(objImage?["5th_TownUI/backgrd"] as WzCanvasProperty);
            _townBarEffect = LoadCanvasTexture(objImage?["5th_TownUI/bareffect"] as WzCanvasProperty);
            _townCakeGauge = LoadCanvasTexture(objImage?["5th_TownUI/gage/0"] as WzCanvasProperty);
            _townPieGauge = LoadCanvasTexture(objImage?["5th_TownUI/gage/1"] as WzCanvasProperty);
            _timerBackground = LoadCanvasTexture(objImage?["5th_Timer/backgrd"] as WzCanvasProperty);
            _timerReady = LoadCanvasTexture(objImage?["5th_Timer/ready"] as WzCanvasProperty);
            _timerStart = LoadCanvasTexture(objImage?["5th_Timer/start"] as WzCanvasProperty);
            _timerCake = LoadCanvasTexture(objImage?["5th_Timer/cake"] as WzCanvasProperty);
            _timerPie = LoadCanvasTexture(objImage?["5th_Timer/pie"] as WzCanvasProperty);
            _itemInfoBackground = LoadCanvasTexture(ResolveCanvas(uiWindowImage?["5thevent/WorldUI/back"]));
            _itemInfoGaugeBar = LoadCanvasTexture(ResolveCanvas(uiWindowImage?["5thevent/WorldUI/bar"]));
            _itemInfoCakeGauge = LoadCanvasTexture(ResolveCanvas(uiWindowImage?["5thevent/WorldUI/gage/0"]));
            _itemInfoPieGauge = LoadCanvasTexture(ResolveCanvas(uiWindowImage?["5thevent/WorldUI/gage/1"]));
            LoadDigitTextures(objImage?["5th_Timer/fontTime"]);
            _assetsLoaded = true;
        }

        private void LoadDigitTextures(WzImageProperty digitContainer)
        {
            for (int i = 0; i < _digitTextures.Length; i++)
            {
                _digitTextures[i] = LoadCanvasTexture(ResolveCanvas(digitContainer?[i.ToString()]));
            }
        }

        private static WzCanvasProperty ResolveCanvas(WzImageProperty property)
        {
            if (property is WzCanvasProperty canvas)
            {
                return canvas;
            }

            return property?.WzProperties?.OfType<WzCanvasProperty>().FirstOrDefault();
        }

        private Texture2D LoadCanvasTexture(WzCanvasProperty canvas)
        {
            if (_graphicsDevice == null || canvas == null)
            {
                return null;
            }

            try
            {
                using var bitmap = canvas.GetLinkedWzCanvasBitmap();
                return bitmap?.ToTexture2DAndDispose(_graphicsDevice);
            }
            catch
            {
                return null;
            }
        }

        private static void DrawShadowedText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color color, float scale = 1f)
        {
            if (string.IsNullOrWhiteSpace(text) || font == null)
            {
                return;
            }

            spriteBatch.DrawString(font, text, position + Vector2.One, Color.Black, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private static string FormatTimer(int remainingSeconds)
        {
            int minutes = Math.Max(0, remainingSeconds) / 60;
            int seconds = Math.Max(0, remainingSeconds) % 60;
            return $"{minutes:00}:{seconds:00}";
        }
    }
}
