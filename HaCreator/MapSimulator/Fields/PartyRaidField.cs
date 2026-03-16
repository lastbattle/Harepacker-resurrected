using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Globalization;

namespace HaCreator.MapSimulator.Fields
{
    public enum PartyRaidFieldMode
    {
        None,
        Field,
        Boss,
        Result
    }

    public enum PartyRaidResultOutcome
    {
        Unknown,
        Win,
        Lose
    }

    public sealed class PartyRaidField
    {
        private const int PointBoardWidth = 238;
        private const int PointBoardHeight = 58;
        private const int PointBoardOffsetX = -119;
        private const int PointBoardY = 70;
        private const int BossGaugeWidth = 322;
        private const int BossGaugeHeight = 16;
        private const int BossGaugeBoardWidth = 436;
        private const int BossGaugeBoardHeight = 48;
        private const int BossGaugeBoardOffsetX = -218;
        private const int BossGaugeBoardY = 18;
        private const int BossGaugeFillInsetX = 54;
        private const int BossGaugeFillInsetY = 16;
        private const int ResultPanelWidth = 360;
        private const int ResultPanelHeight = 228;
        private const int ResultPanelOffsetY = 112;
        private const int DefaultGaugeCapacity = 100000;
        private const int MinePulsePeriodMs = 1200;
        private const int OtherPulsePeriodMs = 1700;

        private bool _isActive;
        private int _mapId;
        private PartyRaidFieldMode _mode;
        private int _stage;
        private int _point;
        private int _redDamage;
        private int _blueDamage;
        private int _gaugeCapacity;
        private int _resultPoint;
        private int _resultBonus;
        private int _resultTotal;
        private PartyRaidResultOutcome _resultOutcome;
        private int _lastUpdateTick;
        private float _minePulse;
        private float _otherPulse;

        public bool IsActive => _isActive;
        public int MapId => _mapId;
        public PartyRaidFieldMode Mode => _mode;
        public int Stage => _stage;
        public int Point => _point;
        public int RedDamage => _redDamage;
        public int BlueDamage => _blueDamage;
        public int GaugeCapacity => _gaugeCapacity;
        public int ResultPoint => _resultPoint;
        public int ResultBonus => _resultBonus;
        public int ResultTotal => _resultTotal;
        public PartyRaidResultOutcome ResultOutcome => _resultOutcome;

        public void Initialize(GraphicsDevice device)
        {
        }

        public void BindMap(MapInfo mapInfo)
        {
            Reset();
            if (mapInfo == null || mapInfo.fieldType == null)
            {
                return;
            }

            PartyRaidFieldMode mode = GetMode((FieldType)mapInfo.fieldType);
            if (mode == PartyRaidFieldMode.None)
            {
                return;
            }

            _isActive = true;
            _mapId = mapInfo.id;
            _mode = mode;
            _stage = 1;
            _gaugeCapacity = DefaultGaugeCapacity;
            _resultPoint = -1;
            _resultBonus = -1;
            _resultTotal = -1;
            _resultOutcome = InferOutcomeFromMap(mapInfo.id);
        }

        public void Update(int currentTimeMs)
        {
            if (!_isActive)
            {
                return;
            }

            if (_lastUpdateTick == 0)
            {
                _lastUpdateTick = currentTimeMs;
            }

            int elapsedMs = Math.Max(0, currentTimeMs - _lastUpdateTick);
            _lastUpdateTick = currentTimeMs;
            _minePulse = AdvancePulse(_minePulse, elapsedMs, MinePulsePeriodMs);
            _otherPulse = AdvancePulse(_otherPulse, elapsedMs, OtherPulsePeriodMs);
        }

        public void Draw(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int tickCount,
            Texture2D pixelTexture,
            SpriteFont font = null)
        {
            if (!_isActive || pixelTexture == null)
            {
                return;
            }

            int screenWidth = spriteBatch.GraphicsDevice.Viewport.Width;
            int screenHeight = spriteBatch.GraphicsDevice.Viewport.Height;

            switch (_mode)
            {
                case PartyRaidFieldMode.Field:
                    DrawFieldBoard(spriteBatch, pixelTexture, font, screenWidth, drawBossGauge: false);
                    break;
                case PartyRaidFieldMode.Boss:
                    DrawFieldBoard(spriteBatch, pixelTexture, font, screenWidth, drawBossGauge: true);
                    break;
                case PartyRaidFieldMode.Result:
                    DrawResultPanel(spriteBatch, pixelTexture, font, screenWidth, screenHeight);
                    break;
            }
        }

        public void SetStage(int stage)
        {
            _stage = Math.Max(0, stage);
        }

        public void SetPoint(int point)
        {
            _point = Math.Max(0, point);
        }

        public void SetGaugeCapacity(int gaugeCapacity)
        {
            _gaugeCapacity = Math.Max(1, gaugeCapacity);
        }

        public void SetBossDamage(int redDamage, int blueDamage)
        {
            _redDamage = Math.Max(0, redDamage);
            _blueDamage = Math.Max(0, blueDamage);
        }

        public void SetResultValues(int point, int bonus, int total)
        {
            _resultPoint = Math.Max(0, point);
            _resultBonus = Math.Max(0, bonus);
            _resultTotal = Math.Max(0, total);
        }

        public void SetResultOutcome(PartyRaidResultOutcome outcome)
        {
            _resultOutcome = outcome;
        }

        public bool OnFieldSetVariable(string key, string value)
        {
            if (!TryParseNonNegative(value, out int parsedValue))
            {
                return false;
            }

            if (MatchesAlias(key, "reddamage", "red", "damage_r", "partyraid_red"))
            {
                _redDamage = parsedValue;
                return true;
            }

            if (MatchesAlias(key, "bluedamage", "blue", "damage_b", "partyraid_blue"))
            {
                _blueDamage = parsedValue;
                return true;
            }

            if (MatchesAlias(key, "stage"))
            {
                _stage = parsedValue;
                return true;
            }

            if (MatchesAlias(key, "gaugecap", "maxhp", "gauge_capacity"))
            {
                _gaugeCapacity = Math.Max(1, parsedValue);
                return true;
            }

            return false;
        }

        public bool OnPartyValue(string key, string value)
        {
            if (!TryParseNonNegative(value, out int parsedValue))
            {
                return false;
            }

            if (MatchesAlias(key, "point", "partypoint", "pt"))
            {
                _point = parsedValue;
                return true;
            }

            return false;
        }

        public bool OnSessionValue(string key, string value)
        {
            if (!TryParseNonNegative(value, out int parsedValue))
            {
                return false;
            }

            if (MatchesAlias(key, "point", "partypoint", "pt"))
            {
                _resultPoint = parsedValue;
                return true;
            }

            if (MatchesAlias(key, "bonus", "rewardbonus"))
            {
                _resultBonus = parsedValue;
                return true;
            }

            if (MatchesAlias(key, "total", "sum"))
            {
                _resultTotal = parsedValue;
                return true;
            }

            return false;
        }

        public string DescribeStatus()
        {
            if (!_isActive)
            {
                return "Party Raid runtime inactive.";
            }

            return _mode switch
            {
                PartyRaidFieldMode.Field => $"Party Raid field map {_mapId}: stage {_stage}, point {_point}.",
                PartyRaidFieldMode.Boss => $"Party Raid boss map {_mapId}: point {_point}, red damage {_redDamage}, blue damage {_blueDamage}, gauge cap {_gaugeCapacity}.",
                PartyRaidFieldMode.Result => $"Party Raid result map {_mapId}: point {_resultPoint}, bonus {_resultBonus}, total {_resultTotal}, outcome {GetOutcomeLabel(_resultOutcome)}.",
                _ => "Party Raid runtime inactive."
            };
        }

        public void Reset()
        {
            _isActive = false;
            _mapId = 0;
            _mode = PartyRaidFieldMode.None;
            _stage = 0;
            _point = 0;
            _redDamage = 0;
            _blueDamage = 0;
            _gaugeCapacity = DefaultGaugeCapacity;
            _resultPoint = -1;
            _resultBonus = -1;
            _resultTotal = -1;
            _resultOutcome = PartyRaidResultOutcome.Unknown;
            _lastUpdateTick = 0;
            _minePulse = 0f;
            _otherPulse = 0f;
        }

        private void DrawFieldBoard(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int screenWidth, bool drawBossGauge)
        {
            int boardX = screenWidth / 2 + PointBoardOffsetX;
            Rectangle boardRect = new Rectangle(boardX, PointBoardY, PointBoardWidth, PointBoardHeight);
            spriteBatch.Draw(pixelTexture, boardRect, new Color(26, 27, 35, 220));
            spriteBatch.Draw(pixelTexture, new Rectangle(boardRect.X + 2, boardRect.Y + 2, boardRect.Width - 4, boardRect.Height - 4), new Color(48, 58, 73, 235));

            float mineGlow = 0.35f + (_minePulse * 0.25f);
            float otherGlow = 0.25f + (_otherPulse * 0.2f);
            spriteBatch.Draw(pixelTexture, new Rectangle(boardRect.X + 10, boardRect.Y + 10, 48, boardRect.Height - 20), new Color(255, 192, 64) * mineGlow);
            spriteBatch.Draw(pixelTexture, new Rectangle(boardRect.Right - 58, boardRect.Y + 10, 48, boardRect.Height - 20), new Color(112, 180, 255) * otherGlow);

            if (font != null)
            {
                DrawOutlinedString(spriteBatch, font, "STAGE", new Vector2(boardRect.X + 65, boardRect.Y + 8), Color.Gold);
                DrawOutlinedString(spriteBatch, font, _stage.ToString(CultureInfo.InvariantCulture), new Vector2(boardRect.X + 133, boardRect.Y + 8), Color.White);
                DrawOutlinedString(spriteBatch, font, "POINT", new Vector2(boardRect.X + 65, boardRect.Y + 29), Color.LightSkyBlue);
                DrawOutlinedString(spriteBatch, font, _point.ToString(CultureInfo.InvariantCulture), new Vector2(boardRect.X + 133, boardRect.Y + 29), Color.White);
            }

            if (drawBossGauge)
            {
                DrawBossGauge(spriteBatch, pixelTexture, font, screenWidth);
            }
        }

        private void DrawBossGauge(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int screenWidth)
        {
            int boardX = screenWidth / 2 + BossGaugeBoardOffsetX;
            Rectangle boardRect = new Rectangle(boardX, BossGaugeBoardY, BossGaugeBoardWidth, BossGaugeBoardHeight);
            spriteBatch.Draw(pixelTexture, boardRect, new Color(14, 16, 20, 220));
            spriteBatch.Draw(pixelTexture, new Rectangle(boardRect.X + 2, boardRect.Y + 2, boardRect.Width - 4, boardRect.Height - 4), new Color(46, 50, 58, 240));

            Rectangle gaugeRect = new Rectangle(boardRect.X + BossGaugeFillInsetX, boardRect.Y + BossGaugeFillInsetY, BossGaugeWidth, BossGaugeHeight);
            spriteBatch.Draw(pixelTexture, gaugeRect, new Color(28, 30, 36, 255));

            int redRemainingWidth = GetRemainingGaugeWidth(_redDamage);
            int blueRemainingWidth = GetRemainingGaugeWidth(_blueDamage);

            if (redRemainingWidth > 0)
            {
                spriteBatch.Draw(pixelTexture, new Rectangle(gaugeRect.X, gaugeRect.Y, redRemainingWidth, gaugeRect.Height), new Color(214, 72, 72, 255));
            }

            if (blueRemainingWidth > 0)
            {
                spriteBatch.Draw(pixelTexture, new Rectangle(gaugeRect.Right - blueRemainingWidth, gaugeRect.Y, blueRemainingWidth, gaugeRect.Height), new Color(78, 150, 255, 255));
            }

            spriteBatch.Draw(pixelTexture, new Rectangle(gaugeRect.X + BossGaugeWidth / 2 - 1, gaugeRect.Y - 3, 2, gaugeRect.Height + 6), new Color(255, 255, 255, 160));

            if (font != null)
            {
                DrawOutlinedString(spriteBatch, font, $"RED {_redDamage}", new Vector2(boardRect.X + 8, boardRect.Y + 14), Color.IndianRed);
                DrawOutlinedString(spriteBatch, font, $"BLUE {_blueDamage}", new Vector2(boardRect.Right - 116, boardRect.Y + 14), Color.LightSkyBlue);
            }
        }

        private void DrawResultPanel(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int screenWidth, int screenHeight)
        {
            int panelX = (screenWidth - ResultPanelWidth) / 2;
            int panelY = Math.Max(20, (screenHeight - ResultPanelHeight) / 2 - ResultPanelOffsetY);
            Rectangle panelRect = new Rectangle(panelX, panelY, ResultPanelWidth, ResultPanelHeight);
            spriteBatch.Draw(pixelTexture, panelRect, new Color(10, 12, 18, 230));
            spriteBatch.Draw(pixelTexture, new Rectangle(panelRect.X + 3, panelRect.Y + 3, panelRect.Width - 6, panelRect.Height - 6), new Color(42, 47, 58, 245));

            Color badgeColor = _resultOutcome switch
            {
                PartyRaidResultOutcome.Win => new Color(86, 184, 96, 255),
                PartyRaidResultOutcome.Lose => new Color(212, 90, 90, 255),
                _ => new Color(180, 160, 96, 255)
            };

            Rectangle badgeRect = new Rectangle(panelRect.X + 112, panelRect.Y + 24, 136, 34);
            spriteBatch.Draw(pixelTexture, badgeRect, badgeColor);

            if (font == null)
            {
                return;
            }

            string title = "PARTY RAID RESULT";
            Vector2 titleSize = font.MeasureString(title);
            DrawOutlinedString(spriteBatch, font, title, new Vector2(panelRect.Center.X - titleSize.X / 2f, panelRect.Y + 6), Color.White);

            string outcome = GetOutcomeLabel(_resultOutcome);
            Vector2 outcomeSize = font.MeasureString(outcome);
            DrawOutlinedString(spriteBatch, font, outcome, new Vector2(badgeRect.Center.X - outcomeSize.X / 2f, badgeRect.Y + 7), Color.White);

            DrawResultLine(spriteBatch, font, panelRect.X + 54, panelRect.Y + 92, "POINT", _resultPoint);
            DrawResultLine(spriteBatch, font, panelRect.X + 54, panelRect.Y + 124, "BONUS", _resultBonus);
            DrawResultLine(spriteBatch, font, panelRect.X + 54, panelRect.Y + 161, "TOTAL", _resultTotal);
        }

        private void DrawResultLine(SpriteBatch spriteBatch, SpriteFont font, int x, int y, string label, int value)
        {
            DrawOutlinedString(spriteBatch, font, label, new Vector2(x, y), Color.LightGray);
            string valueText = value >= 0 ? value.ToString(CultureInfo.InvariantCulture) : "--";
            DrawOutlinedString(spriteBatch, font, valueText, new Vector2(x + 140, y), Color.Gold);
        }

        private int GetRemainingGaugeWidth(int damage)
        {
            int clampedDamage = Math.Clamp(damage, 0, _gaugeCapacity);
            float remaining = 1f - ((float)clampedDamage / _gaugeCapacity);
            return Math.Clamp((int)Math.Round(BossGaugeWidth * remaining), 0, BossGaugeWidth);
        }

        private static float AdvancePulse(float current, int elapsedMs, int periodMs)
        {
            if (periodMs <= 0)
            {
                return current;
            }

            current += (float)elapsedMs / periodMs;
            current -= (float)Math.Floor(current);
            return 0.5f + 0.5f * (float)Math.Sin(current * MathHelper.TwoPi);
        }

        private static PartyRaidFieldMode GetMode(FieldType fieldType)
        {
            return fieldType switch
            {
                FieldType.FIELDTYPE_PARTYRAID => PartyRaidFieldMode.Field,
                FieldType.FIELDTYPE_PARTYRAID_BOSS => PartyRaidFieldMode.Boss,
                FieldType.FIELDTYPE_PARTYRAID_RESULT => PartyRaidFieldMode.Result,
                _ => PartyRaidFieldMode.None
            };
        }

        private static PartyRaidResultOutcome InferOutcomeFromMap(int mapId)
        {
            return mapId switch
            {
                923020010 => PartyRaidResultOutcome.Win,
                923020020 => PartyRaidResultOutcome.Lose,
                _ => PartyRaidResultOutcome.Unknown
            };
        }

        private static string GetOutcomeLabel(PartyRaidResultOutcome outcome)
        {
            return outcome switch
            {
                PartyRaidResultOutcome.Win => "WIN",
                PartyRaidResultOutcome.Lose => "LOSE",
                _ => "RESULT"
            };
        }

        private static bool MatchesAlias(string key, params string[] aliases)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            string normalized = NormalizeKey(key);
            for (int i = 0; i < aliases.Length; i++)
            {
                if (normalized == NormalizeKey(aliases[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeKey(string key)
        {
            return key.Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Trim()
                .ToLowerInvariant();
        }

        private static bool TryParseNonNegative(string value, out int parsedValue)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue))
            {
                parsedValue = 0;
                return false;
            }

            parsedValue = Math.Max(0, parsedValue);
            return true;
        }

        private static void DrawOutlinedString(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color color)
        {
            spriteBatch.DrawString(font, text, position + Vector2.One, Color.Black);
            spriteBatch.DrawString(font, text, position, color);
        }
    }
}
