using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.UI {

    /// <summary>
    /// Character stats data for display on the status bar.
    /// Positions based on analysis of MapleStory client CUIStatusBar::SetNumberValue and CUIStatusBar::SetStatusValue functions.
    /// </summary>
    public class CharacterStatsData {
        public int HP { get; set; } = 100;
        public int MaxHP { get; set; } = 100;
        public int MP { get; set; } = 100;
        public int MaxMP { get; set; } = 100;
        public long EXP { get; set; } = 0;
        public long MaxEXP { get; set; } = 100;
        public int Level { get; set; } = 1;
        public string Name { get; set; } = "Player";
        public string Job { get; set; } = "Beginner";
    }

    public class StatusBarUI : BaseDXDrawableItem, IUIObjectEvents {
        private readonly List<UIObject> uiButtons = new List<UIObject>();

        // Character stats display - positions based on IDA Pro analysis of client CUIStatusBar
        // HP text at (163, 4), MP text at (332, 4), EXP text at (332, 20)
        // Level at (45, 552), Job at (75, 549), Name at (75, 561) (absolute screen positions)
        private SpriteFont _font;
        private Func<CharacterStatsData> _getCharacterStats;
        private Texture2D _pixelTexture;

        // Gauge textures loaded from UI.wz/StatusBar2.img/mainBar/gauge/hp/0, mp/0, exp/0
        private Texture2D _hpGaugeTexture;
        private Texture2D _mpGaugeTexture;
        private Texture2D _expGaugeTexture;

        // Bitmap font textures for MapleStory-style numbers
        // Loaded from UI.wz/Basic.img/ItemNo/ (digits 0-9, slash, percent, etc.)
        private Texture2D[] _digitTextures;       // 0-9
        private Point[] _digitOrigins;            // Origins for digits 0-9
        private Texture2D _slashTexture;          // For HP/MP display like "100/100"
        private Point _slashOrigin;
        private Texture2D _percentTexture;        // For EXP percentage
        private Point _percentOrigin;
        private Texture2D _bracketLeftTexture;    // [
        private Point _bracketLeftOrigin;
        private Texture2D _bracketRightTexture;   // ]
        private Point _bracketRightOrigin;
        private Texture2D _dotTexture;            // For decimal point
        private Point _dotOrigin;
        private bool _useBitmapFont = false;      // Whether to use bitmap font or SpriteFont

        // Text positions relative to status bar (from IDA Pro analysis)
        // The status bar is composed of: lvBacktrnd (left ~64px) + gaugeBackgrd (center) + buttons
        // From SetNumberValue: HP(163,4), MP(332,4), EXP(332,20) - relative to gauge layer
        // From SetStatusValue: Level(45,552), Job(75,549), Name(75,561) - in 800x578 canvas
        // Converting to relative positions within status bar:
        //   Level: (45, 10), Job: (75, 7), Name: (75, 19)

        // lvBacktrnd area (left side - character info)
        private static readonly Vector2 LEVEL_TEXT_POS = new Vector2(45, 10);  // Level number (offset adjusted for digit count)
        private static readonly Vector2 JOB_TEXT_POS = new Vector2(75, 7);     // Job name
        private static readonly Vector2 NAME_TEXT_POS = new Vector2(75, 19);   // Character name

        // gaugeBackgrd area - HP/MP/EXP text and gauges
        // lvBacktrnd is ~64px wide, so gauge area starts at X~64
        // HP/MP/EXP positions from SetNumberValue are relative to gauge layer
        private static readonly Vector2 HP_TEXT_POS = new Vector2(163, 4);     // HP text [HP\MaxHP]
        private static readonly Vector2 MP_TEXT_POS = new Vector2(332, 4);     // MP text [MP\MaxMP]
        private static readonly Vector2 EXP_TEXT_POS = new Vector2(332, 20);   // EXP text

        // Gauge bar positions and sizes
        // From IDA Pro CUIStatusBar::CGauge::Create:
        // HP Gauge: X=28 relative to gaugeBackgrd, Y=2, Length=138
        // MP Gauge: X=197 relative to gaugeBackgrd, Y=2, Length=138
        // EXP Gauge: X=28 relative to gaugeBackgrd, Y=18, Length=308
        // gaugeBackgrd starts at ~64px, so absolute X positions:
        // HP: 64+28=92, MP: 64+197=261, EXP: 64+28=92
        private static readonly Rectangle HP_GAUGE_RECT = new Rectangle(92, 3, 138, 12);    // HP gauge (red)
        private static readonly Rectangle MP_GAUGE_RECT = new Rectangle(261, 3, 138, 12);   // MP gauge (blue)
        private static readonly Rectangle EXP_GAUGE_RECT = new Rectangle(92, 20, 308, 10);  // EXP gauge (yellow)

        // Gauge colors matching original MapleStory client
        private static readonly Color HP_GAUGE_COLOR = new Color(255, 50, 50);       // Red for HP
        private static readonly Color HP_GAUGE_BG_COLOR = new Color(80, 20, 20);     // Dark red background
        private static readonly Color MP_GAUGE_COLOR = new Color(50, 100, 255);      // Blue for MP
        private static readonly Color MP_GAUGE_BG_COLOR = new Color(20, 40, 80);     // Dark blue background
        private static readonly Color EXP_GAUGE_COLOR = new Color(255, 255, 50);     // Yellow for EXP
        private static readonly Color EXP_GAUGE_BG_COLOR = new Color(60, 60, 20);    // Dark yellow background

        /// <summary>
        /// Constructor for the status bar window
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="obj_Ui_BtCashShop">The BtCashShop button</param>
        /// <param name="obj_Ui_BtMTS">The MTS button</param>
        /// <param name="obj_Ui_BtMenu"></param>
        /// <param name="obj_Ui_BtSystem">The System button</param>
        /// <param name="obj_Ui_BtChannel">The Channel button</param>
        public StatusBarUI(IDXObject frame, UIObject obj_Ui_BtCashShop, UIObject obj_Ui_BtMTS, UIObject obj_Ui_BtMenu, UIObject obj_Ui_BtSystem, UIObject obj_Ui_BtChannel, Point setPosition,
            List<UIObject> otherUI)
            : base(frame, false) {

            uiButtons.Add(obj_Ui_BtCashShop);
            if (obj_Ui_BtMTS != null)
                uiButtons.Add(obj_Ui_BtMTS);
            uiButtons.Add(obj_Ui_BtMenu);
            uiButtons.Add(obj_Ui_BtSystem);
            uiButtons.Add(obj_Ui_BtChannel);

            uiButtons.AddRange(otherUI);

            this.Position = setPosition;
        }

        /// <summary>
        /// Set the font and character stats callback for rendering character information
        /// </summary>
        /// <param name="font">SpriteFont to use for text rendering</param>
        /// <param name="getCharacterStats">Callback to get current character stats</param>
        public void SetCharacterStatsProvider(SpriteFont font, Func<CharacterStatsData> getCharacterStats) {
            _font = font;
            _getCharacterStats = getCharacterStats;
        }

        /// <summary>
        /// Set the pixel texture for drawing gauge bars
        /// </summary>
        /// <param name="graphicsDevice">Graphics device for creating textures</param>
        public void SetPixelTexture(GraphicsDevice graphicsDevice) {
            if (_pixelTexture == null && graphicsDevice != null) {
                _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
                _pixelTexture.SetData(new[] { Color.White });
            }
        }

        /// <summary>
        /// Set the gauge textures loaded from UI.wz/StatusBar2.img/mainBar/gauge/hp/0, mp/0, exp/0
        /// These textures are used to render the HP, MP, and EXP bars with proper graphics
        /// </summary>
        public void SetGaugeTextures(Texture2D hpGauge, Texture2D mpGauge, Texture2D expGauge) {
            _hpGaugeTexture = hpGauge;
            _mpGaugeTexture = mpGauge;
            _expGaugeTexture = expGauge;
        }

        /// <summary>
        /// Set the digit textures for MapleStory-style bitmap font rendering.
        /// Loaded from UI.wz/Basic.img/ItemNo/ or UI.wz/StatusBar2.img/mainBar/number/
        /// </summary>
        /// <param name="digitTextures">Array of 10 textures for digits 0-9</param>
        /// <param name="digitOrigins">Array of 10 origin points for digits 0-9</param>
        /// <param name="slashTexture">Texture for slash character</param>
        /// <param name="slashOrigin">Origin for slash</param>
        /// <param name="percentTexture">Texture for percent symbol</param>
        /// <param name="percentOrigin">Origin for percent</param>
        /// <param name="bracketLeft">Texture for left bracket [</param>
        /// <param name="bracketLeftOrigin">Origin for left bracket</param>
        /// <param name="bracketRight">Texture for right bracket ]</param>
        /// <param name="bracketRightOrigin">Origin for right bracket</param>
        /// <param name="dotTexture">Texture for decimal point</param>
        /// <param name="dotOrigin">Origin for dot</param>
        public void SetDigitTextures(Texture2D[] digitTextures, Point[] digitOrigins,
            Texture2D slashTexture, Point slashOrigin,
            Texture2D percentTexture, Point percentOrigin,
            Texture2D bracketLeft, Point bracketLeftOrigin,
            Texture2D bracketRight, Point bracketRightOrigin,
            Texture2D dotTexture, Point dotOrigin) {
            if (digitTextures != null && digitTextures.Length >= 10) {
                _digitTextures = digitTextures;
                _digitOrigins = digitOrigins ?? new Point[10];
                _slashTexture = slashTexture;
                _slashOrigin = slashOrigin;
                _percentTexture = percentTexture;
                _percentOrigin = percentOrigin;
                _bracketLeftTexture = bracketLeft;
                _bracketLeftOrigin = bracketLeftOrigin;
                _bracketRightTexture = bracketRight;
                _bracketRightOrigin = bracketRightOrigin;
                _dotTexture = dotTexture;
                _dotOrigin = dotOrigin;
                _useBitmapFont = true;
            }
        }

        /// <summary>
        /// Add UI buttons to be rendered
        /// </summary>
        /// <param name="baseClickableUIObject"></param>
        public void InitializeButtons() {
            //objUIBtMax.SetButtonState(UIObjectState.Disabled); // start maximised
        }

        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="sprite"></param>
        /// <param name="skeletonMeshRenderer"></param>
        /// <param name="gameTime"></param>
        /// <param name="mapShiftX"></param>
        /// <param name="mapShiftY"></param>
        /// <param name="centerX"></param>
        /// <param name="centerY"></param>
        /// <param name="drawReflectionInfo"></param>
        /// <param name="renderParameters"></param>
        /// <param name="TickCount"></param>
        public override void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount) {
            // control minimap render UI position via
            //  Position.X, Position.Y

            // Draw the main frame
            // Pass centerX=0, centerY=0 so the frame draws at its DXObject position (0, Position.Y)
            // without being offset by the minimap position
            base.Draw(sprite, skeletonMeshRenderer, gameTime,
                   this.Position.X, this.Position.Y, 0, 0,
                   drawReflectionInfo,
                   renderParameters,
                   TickCount);

            // draw other buttons
            foreach (UIObject uiBtn in uiButtons) {
                BaseDXDrawableItem buttonToDraw = uiBtn.GetBaseDXDrawableItemByState();

                // Position drawn is relative to this UI
                int drawRelativeX = -(this.Position.X) - uiBtn.X; // Left to right
                int drawRelativeY = -(this.Position.Y) - uiBtn.Y; // Top to bottom

                buttonToDraw.Draw(sprite, skeletonMeshRenderer,
                    gameTime,
                    drawRelativeX,
                    drawRelativeY,
                    0, 0,
                    null,
                    renderParameters,
                    TickCount);
            }

            // Draw character stats (HP, MP, EXP, Level, Name)
            // Use renderParameters to get screen height and calculate position from bottom
            DrawCharacterStats(sprite, renderParameters);
        }

        /// <summary>
        /// Draw character stats (HP, MP, EXP, Level, Name) on the status bar.
        /// Positions derived from IDA Pro analysis of CUIStatusBar::SetNumberValue and CUIStatusBar::SetStatusValue.
        /// </summary>
        private void DrawCharacterStats(SpriteBatch sprite, RenderParameters renderParameters) {
            if (_getCharacterStats == null)
                return;

            CharacterStatsData stats = _getCharacterStats();
            if (stats == null)
                return;

            // Calculate base position from bottom of screen
            // Use logical height (RenderHeight / RenderObjectScaling) to match how the frame is positioned
            int barHeight = Frame0?.Height ?? 30;
            int logicalHeight = (int)(renderParameters.RenderHeight / renderParameters.RenderObjectScaling);
            int statusBarY = logicalHeight - barHeight + 50;  // Offset to align with status bar frame

            // Two base positions: left side (Name, Job, Level) and gauge area (HP, MP, EXP)
            Vector2 basePosLeft = new Vector2(0, statusBarY - 7);    // Y - 7 for left side
            Vector2 basePosGauge = new Vector2(155, statusBarY + 2);  // X + 155, Y + 2 for gauges

            // Draw gauge bars first (under the text)
            DrawGaugeBars(sprite, stats, basePosGauge);

            // Skip text rendering if no font
            if (_font == null)
                return;

            // Draw character info section (left side) - use basePosLeft
            // Level number - format from client: level number with image digits
            string levelText = stats.Level.ToString();
            // Adjust X based on number of digits (from IDA: offset 0 for 1-9, 6 for 10-99, 12 for 100+)
            int levelOffset = stats.Level <= 9 ? 0 : (stats.Level <= 99 ? 6 : 12);
            Vector2 levelPos = basePosLeft + new Vector2(LEVEL_TEXT_POS.X - levelOffset, LEVEL_TEXT_POS.Y);
            DrawTextWithShadow(sprite, levelText, levelPos, Color.White, Color.Black);

            // Job name
            Vector2 jobPos = basePosLeft + JOB_TEXT_POS;
            DrawTextWithShadow(sprite, stats.Job, jobPos, Color.Yellow, Color.Black, 0.8f);

            // Character name - drawn with shadow effect (from IDA: multiple positions for shadow)
            Vector2 namePos = basePosLeft + NAME_TEXT_POS;
            DrawTextWithShadow(sprite, stats.Name, namePos, Color.White, Color.Black, 0.8f);

            // Draw gauge text section (HP/MP/EXP area) - use basePosGauge
            // HP text - format from client: [HP/MaxHP]
            string hpText = $"[{stats.HP}/{stats.MaxHP}]";
            Vector2 hpPos = basePosGauge + HP_TEXT_POS;
            if (_useBitmapFont) {
                DrawBitmapString(sprite, hpText, hpPos, 1.0f);
            } else {
                DrawTextWithShadow(sprite, hpText, hpPos, Color.White, Color.Black, 0.7f);
            }

            // MP text - format from client: [MP/MaxMP]
            string mpText = $"[{stats.MP}/{stats.MaxMP}]";
            Vector2 mpPos = basePosGauge + MP_TEXT_POS;
            if (_useBitmapFont) {
                DrawBitmapString(sprite, mpText, mpPos, 1.0f);
            } else {
                DrawTextWithShadow(sprite, mpText, mpPos, Color.White, Color.Black, 0.7f);
            }

            // EXP text - format from client: [percentage%]
            double expPercent = stats.MaxEXP > 0 ? (double)stats.EXP / stats.MaxEXP * 100.0 : 0.0;
            // Cap at 99.99% like the client does
            if (expPercent > 99.99) expPercent = 99.99;
            string expText = $"[{expPercent:F2}%]";
            Vector2 expPos = basePosGauge + EXP_TEXT_POS;
            if (_useBitmapFont) {
                DrawBitmapString(sprite, expText, expPos, 1.0f);
            } else {
                DrawTextWithShadow(sprite, expText, expPos, Color.Yellow, Color.Black, 0.7f);
            }
        }

        /// <summary>
        /// Draw HP, MP, and EXP gauge bars.
        /// Gauge positions derived from IDA Pro analysis of CUIStatusBar::CGauge::Create.
        /// Uses actual gauge textures from UI.wz when available, falls back to colored rectangles.
        /// </summary>
        private void DrawGaugeBars(SpriteBatch sprite, CharacterStatsData stats, Vector2 basePos) {
            // Calculate fill ratios
            float hpRatio = stats.MaxHP > 0 ? (float)stats.HP / stats.MaxHP : 0f;
            float mpRatio = stats.MaxMP > 0 ? (float)stats.MP / stats.MaxMP : 0f;
            float expRatio = 0.99f; // Always draw EXP at 99%

            // Clamp ratios to 0-1 range
            hpRatio = Math.Clamp(hpRatio, 0f, 1f);
            mpRatio = Math.Clamp(mpRatio, 0f, 1f);

            // Draw HP gauge - use texture if available, use predefined gauge rect for positioning
            if (_hpGaugeTexture != null) {
                DrawTexturedGauge(sprite, basePos, HP_GAUGE_RECT, hpRatio, _hpGaugeTexture);
            } else if (_pixelTexture != null) {
                DrawGaugeBar(sprite, basePos, HP_GAUGE_RECT, hpRatio, HP_GAUGE_COLOR, HP_GAUGE_BG_COLOR);
            }

            // Draw MP gauge
            if (_mpGaugeTexture != null) {
                DrawTexturedGauge(sprite, basePos, MP_GAUGE_RECT, mpRatio, _mpGaugeTexture);
            } else if (_pixelTexture != null) {
                DrawGaugeBar(sprite, basePos, MP_GAUGE_RECT, mpRatio, MP_GAUGE_COLOR, MP_GAUGE_BG_COLOR);
            }

            // Draw EXP gauge
            if (_expGaugeTexture != null) {
                DrawTexturedGauge(sprite, basePos, EXP_GAUGE_RECT, expRatio, _expGaugeTexture);
            } else if (_pixelTexture != null) {
                DrawGaugeBar(sprite, basePos, EXP_GAUGE_RECT, expRatio, EXP_GAUGE_COLOR, EXP_GAUGE_BG_COLOR);
            }
        }

        /// <summary>
        /// Draw a gauge bar using the actual gauge texture from UI.wz.
        /// The gauge is rendered by drawing a portion of the texture based on fill ratio.
        /// The texture is clipped from the left side proportional to the fill amount.
        /// </summary>
        private void DrawTexturedGauge(SpriteBatch sprite, Vector2 basePos, Rectangle gaugeRect, float fillRatio, Texture2D gaugeTexture) {
            if (fillRatio <= 0 || gaugeTexture == null)
                return;

            // Calculate the destination rectangle position
            Vector2 gaugePos = basePos + new Vector2(gaugeRect.X, gaugeRect.Y);

            // Calculate the filled width - use the gauge rect's defined width
            int destFilledWidth = (int)(gaugeRect.Width * fillRatio);
            if (destFilledWidth <= 0)
                return;

            // Calculate corresponding source width from texture (proportional to fill ratio)
            int srcFilledWidth = (int)(gaugeTexture.Width * fillRatio);
            if (srcFilledWidth <= 0)
                srcFilledWidth = 1;

            // Source rectangle - clip from left side of texture based on fill ratio
            Rectangle sourceRect = new Rectangle(0, 0, srcFilledWidth, gaugeTexture.Height);

            // Destination rectangle - draw at gauge position with filled width
            // Scale the texture to fit the gauge rect height
            Rectangle destRect = new Rectangle(
                (int)gaugePos.X,
                (int)gaugePos.Y,
                destFilledWidth,
                gaugeRect.Height
            );

            sprite.Draw(gaugeTexture, destRect, sourceRect, Color.White);
        }

        /// <summary>
        /// Draw a single gauge bar with background and filled portion (fallback when no texture).
        /// </summary>
        private void DrawGaugeBar(SpriteBatch sprite, Vector2 basePos, Rectangle gaugeRect, float fillRatio, Color fillColor, Color bgColor) {
            // Calculate absolute position
            Rectangle absoluteRect = new Rectangle(
                (int)basePos.X + gaugeRect.X,
                (int)basePos.Y + gaugeRect.Y,
                gaugeRect.Width,
                gaugeRect.Height
            );

            // Draw background
            sprite.Draw(_pixelTexture, absoluteRect, bgColor);

            // Draw filled portion
            if (fillRatio > 0) {
                Rectangle fillRect = new Rectangle(
                    absoluteRect.X,
                    absoluteRect.Y,
                    (int)(absoluteRect.Width * fillRatio),
                    absoluteRect.Height
                );
                sprite.Draw(_pixelTexture, fillRect, fillColor);
            }

            // Draw border (1px)
            Color borderColor = new Color(40, 40, 40);
            // Top border
            sprite.Draw(_pixelTexture, new Rectangle(absoluteRect.X, absoluteRect.Y, absoluteRect.Width, 1), borderColor);
            // Bottom border
            sprite.Draw(_pixelTexture, new Rectangle(absoluteRect.X, absoluteRect.Y + absoluteRect.Height - 1, absoluteRect.Width, 1), borderColor);
            // Left border
            sprite.Draw(_pixelTexture, new Rectangle(absoluteRect.X, absoluteRect.Y, 1, absoluteRect.Height), borderColor);
            // Right border
            sprite.Draw(_pixelTexture, new Rectangle(absoluteRect.X + absoluteRect.Width - 1, absoluteRect.Y, 1, absoluteRect.Height), borderColor);
        }

        /// <summary>
        /// Draw text with a shadow effect (similar to client rendering)
        /// </summary>
        private void DrawTextWithShadow(SpriteBatch sprite, string text, Vector2 position, Color textColor, Color shadowColor, float scale = 1.0f) {
            // Draw shadow at offset positions (similar to client shadow rendering)
            sprite.DrawString(_font, text, position + new Vector2(-1, 0), shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position + new Vector2(1, 0), shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position + new Vector2(0, -1), shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position + new Vector2(0, 1), shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            // Draw main text
            sprite.DrawString(_font, text, position, textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// Draw a string using bitmap font textures (MapleStory style).
        /// Falls back to SpriteFont for characters without bitmap textures.
        /// Returns the width of the drawn string for positioning.
        /// </summary>
        private int DrawBitmapString(SpriteBatch sprite, string text, Vector2 position, float scale = 1.0f) {
            if (_digitTextures == null || !_useBitmapFont)
                return 0;

            int xOffset = 0;
            const int CHAR_SPACING = 0;  // Spacing between characters

            // Get baseline from digit '0' origin - all characters should align to this
            int baselineY = _digitOrigins?[0].Y ?? 0;

            foreach (char c in text) {
                Texture2D texture = GetTextureForChar(c);
                Point origin = GetOriginForChar(c);

                if (texture != null) {
                    // Apply origin offset - origin.Y determines vertical alignment
                    // Characters with different origins need to be adjusted so they align on baseline
                    int yAdjust = baselineY - origin.Y;

                    Rectangle destRect = new Rectangle(
                        (int)(position.X + xOffset - origin.X * scale),
                        (int)(position.Y + yAdjust * scale),
                        (int)(texture.Width * scale),
                        (int)(texture.Height * scale)
                    );
                    sprite.Draw(texture, destRect, Color.White);
                    xOffset += (int)(texture.Width * scale) + CHAR_SPACING;
                } else if (c == ' ') {
                    // Space character - use average digit width
                    xOffset += (int)((_digitTextures[0]?.Width ?? 8) * scale * 0.5f);
                } else if (_font != null) {
                    // Fall back to SpriteFont for characters without bitmap textures
                    string charStr = c.ToString();
                    Vector2 charPos = new Vector2(position.X + xOffset, position.Y);
                    // Draw with slight shadow for visibility
                    sprite.DrawString(_font, charStr, charPos + new Vector2(1, 1), Color.Black, 0f, Vector2.Zero, scale * 0.8f, SpriteEffects.None, 0f);
                    sprite.DrawString(_font, charStr, charPos, Color.White, 0f, Vector2.Zero, scale * 0.8f, SpriteEffects.None, 0f);
                    Vector2 charSize = _font.MeasureString(charStr) * scale * 0.8f;
                    xOffset += (int)charSize.X + CHAR_SPACING;
                }
            }

            return xOffset;
        }

        /// <summary>
        /// Get the texture for a specific character
        /// </summary>
        private Texture2D GetTextureForChar(char c) {
            if (c >= '0' && c <= '9') {
                return _digitTextures?[c - '0'];
            }
            return c switch {
                '/' or '\\' => _slashTexture,
                '%' => _percentTexture,
                '[' => _bracketLeftTexture,
                ']' => _bracketRightTexture,
                '.' => _dotTexture,
                _ => null
            };
        }

        /// <summary>
        /// Get the origin point for a specific character
        /// </summary>
        private Point GetOriginForChar(char c) {
            if (c >= '0' && c <= '9' && _digitOrigins != null && _digitOrigins.Length > c - '0') {
                return _digitOrigins[c - '0'];
            }
            return c switch {
                '/' or '\\' => _slashOrigin,
                '%' => _percentOrigin,
                '[' => _bracketLeftOrigin,
                ']' => _bracketRightOrigin,
                '.' => _dotOrigin,
                _ => Point.Zero
            };
        }

        /// <summary>
        /// Measure the width of a bitmap string
        /// </summary>
        private int MeasureBitmapString(string text, float scale = 1.0f) {
            if (_digitTextures == null || !_useBitmapFont)
                return 0;

            int width = 0;
            const int CHAR_SPACING = 0;

            foreach (char c in text) {
                Texture2D texture = GetTextureForChar(c);
                if (texture != null) {
                    width += (int)(texture.Width * scale) + CHAR_SPACING;
                } else if (c == ' ') {
                    width += (int)((_digitTextures[0]?.Width ?? 8) * scale * 0.5f);
                }
            }

            return width;
        }

        #region IClickableUIObject
        private Point? mouseOffsetOnDragStart = null;
        public bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight) {
            return UIMouseEventHandler.CheckMouseEvent(shiftCenteredX, shiftCenteredY, this.Position.X, this.Position.Y, mouseState, mouseCursor, uiButtons, false);

            // handle UI movement
            /*  if (mouseState.LeftButton == ButtonState.Pressed) {
                  // The rectangle of the MinimapItem UI object

                  // if drag has not started, initialize the offset
                  if (mouseOffsetOnDragStart == null) {
                      Rectangle rect = new Rectangle(
                          this.Position.X,
                          this.Position.Y,
                          this.LastFrameDrawn.Width, this.LastFrameDrawn.Height);
                      if (!rect.Contains(mouseState.X, mouseState.Y)) {
                          return;
                      }
                      mouseOffsetOnDragStart = new Point(mouseState.X - this.Position.X, mouseState.Y - this.Position.Y);
                  }

                  // Calculate the mouse position relative to the minimap
                  // and move the minimap Position
                  this.Position = new Point(mouseState.X - mouseOffsetOnDragStart.Value.X, mouseState.Y - mouseOffsetOnDragStart.Value.Y);
                  //System.Diagnostics.Debug.WriteLine("Button rect: " + rect.ToString());
                  //System.Diagnostics.Debug.WriteLine("Mouse X: " + mouseState.X + ", Y: " + mouseState.Y);
              }
              else {
                  // if the mouse button is not pressed, reset the initial drag offset
                  mouseOffsetOnDragStart = null;

                  // If the window is outside at the end of mouse click + move
                  // move it slightly back to the nearest X and Y coordinate
              }*/
        }
        #endregion
    }
}
