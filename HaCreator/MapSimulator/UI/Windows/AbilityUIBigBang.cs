using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.UI.Controls;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// Ability/Stat UI window for post-Big Bang MapleStory (v100+)
    /// Opened with the "A" key in MapleStory
    /// Structure: UI.wz/UIWindow2.img/Stat/
    ///
    /// Layout from WZ (172x355 pixels):
    /// - Title bar at top
    /// - Info section: NAME, JOB, LEVEL, GUILD
    /// - HP/MP section with increase buttons
    /// - ABILITY POINT section with Auto button
    /// - Stats section: STR, DEX, INT, LUK with individual +buttons
    /// - Detail button at bottom
    /// </summary>
    public class AbilityUIBigBang : UIWindowBase
    {
        #region Constants
        // Text rendering scale
        private const float TEXT_SCALE = 0.5f;

        // Row heights
        private const int ROW_HEIGHT = 18;

        // Window dimensions (from UIWindow2.img/Stat/main/backgrnd)
        private const int WINDOW_WIDTH = 172;
        private const int WINDOW_HEIGHT = 355;

        // X positions for values
        private const int VALUE_X = 54;
        private const int LEFT_BORDER_X = 4;

        // Y positions from WZ origins (negated)
        // Stat buttons at X=147
        private const int BTN_STR_Y = 244;
        private const int BTN_DEX_Y = 262;
        private const int BTN_INT_Y = 280;
        private const int BTN_LUK_Y = 298;
        private const int BTN_AUTO_X = 94;
        private const int BTN_AUTO_Y = 198;
        private const int BTN_DETAIL_X = 92;
        private const int BTN_DETAIL_Y = 325;
        private const int STAT_BTN_X = 147;

        // Text Y positions (from CUIStat::Draw decompilation)
        private const int NAME_Y = 32;
        private const int JOB_Y = 50;
        private const int LEVEL_Y = 68;
        private const int GUILD_Y = 86;
        private const int HP_Y = 104;
        private const int MP_Y = 122;
        private const int AP_Y = 200;
        private const int STR_Y = 245;
        private const int DEX_Y = 263;
        private const int INT_Y = 281;
        private const int LUK_Y = 299;
        #endregion

        #region Fields
        private CharacterBuild _characterBuild;

        // Stat increase buttons (individual for Big Bang)
        private UIObject _btnIncHP;
        private UIObject _btnIncMP;
        private UIObject _btnIncSTR;
        private UIObject _btnIncDEX;
        private UIObject _btnIncINT;
        private UIObject _btnIncLUK;

        // Detail buttons (Open/Close for Big Bang)
        private UIObject _btnDetailOpen;
        private UIObject _btnDetailClose;
        private bool _isDetailMode = false;

        // Auto-assign button
        private UIObject _btnAutoAssign;

        // Detail background texture
        private IDXObject _detailBackground;

        // Detail foreground texture (backgrnd2 from Stat/detail)
        private IDXObject _detailForeground;
        private Point _detailForegroundOffset;

        // Foreground texture (backgrnd2 - labels/overlay)
        private IDXObject _foreground;
        private Point _foregroundOffset;

        // Font for rendering stats
        private SpriteFont _statsFont;

        // Graphics device
        private GraphicsDevice _device;

        // Colors
        private static readonly Color TextColorDark = new Color(0, 0, 0);
        private static readonly Color TextColorWhite = new Color(255, 255, 255);
        private static readonly Color APAvailableColor = new Color(50, 200, 50);
        private static readonly Color StatBonusColor = new Color(50, 150, 255);
        #endregion

        #region Properties
        public override string WindowName => "Ability";

        public override CharacterBuild CharacterBuild
        {
            get => _characterBuild;
            set => _characterBuild = value;
        }

        public bool IsDetailMode => _isDetailMode;
        #endregion

        #region Constructor
        public AbilityUIBigBang(IDXObject frame, GraphicsDevice device)
            : base(frame)
        {
            _device = device;
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Initialize HP/MP increase buttons (Big Bang feature)
        /// </summary>
        public void InitializeHpMpButtons(UIObject btnIncHP, UIObject btnIncMP)
        {
            _btnIncHP = btnIncHP;
            _btnIncMP = btnIncMP;

            if (btnIncHP != null)
            {
                AddButton(btnIncHP);
                btnIncHP.ButtonClickReleased += OnIncreaseHP;
            }
            if (btnIncMP != null)
            {
                AddButton(btnIncMP);
                btnIncMP.ButtonClickReleased += OnIncreaseMP;
            }
        }

        /// <summary>
        /// Initialize stat increase buttons
        /// </summary>
        public void InitializeStatButtons(UIObject btnIncSTR, UIObject btnIncDEX, UIObject btnIncINT, UIObject btnIncLUK)
        {
            _btnIncSTR = btnIncSTR;
            _btnIncDEX = btnIncDEX;
            _btnIncINT = btnIncINT;
            _btnIncLUK = btnIncLUK;

            if (btnIncSTR != null)
            {
                AddButton(btnIncSTR);
                btnIncSTR.ButtonClickReleased += OnIncreaseSTR;
            }
            if (btnIncDEX != null)
            {
                AddButton(btnIncDEX);
                btnIncDEX.ButtonClickReleased += OnIncreaseDEX;
            }
            if (btnIncINT != null)
            {
                AddButton(btnIncINT);
                btnIncINT.ButtonClickReleased += OnIncreaseINT;
            }
            if (btnIncLUK != null)
            {
                AddButton(btnIncLUK);
                btnIncLUK.ButtonClickReleased += OnIncreaseLUK;
            }
        }

        /// <summary>
        /// Initialize detail buttons (Open/Close for Big Bang)
        /// </summary>
        public void InitializeDetailButtons(UIObject btnDetailOpen, UIObject btnDetailClose)
        {
            _btnDetailOpen = btnDetailOpen;
            _btnDetailClose = btnDetailClose;

            if (btnDetailOpen != null)
            {
                AddButton(btnDetailOpen);
                btnDetailOpen.ButtonClickReleased += OnOpenDetail;
            }
            if (btnDetailClose != null)
            {
                AddButton(btnDetailClose);
                btnDetailClose.ButtonClickReleased += OnCloseDetail;
            }
        }

        /// <summary>
        /// Initialize auto-assign button
        /// </summary>
        public void InitializeAutoAssignButton(UIObject btnAutoAssign)
        {
            _btnAutoAssign = btnAutoAssign;
            if (btnAutoAssign != null)
            {
                AddButton(btnAutoAssign);
                btnAutoAssign.ButtonClickReleased += OnAutoAssign;
            }
        }

        public override void SetFont(SpriteFont font)
        {
            _statsFont = font;
        }

        public void SetDetailBackground(IDXObject detailBg)
        {
            _detailBackground = detailBg;
        }

        /// <summary>
        /// Set the detail foreground texture (backgrnd2 from UIWindow2.img/Stat/detail)
        /// </summary>
        public void SetDetailForeground(IDXObject detailFg, int offsetX, int offsetY)
        {
            _detailForeground = detailFg;
            _detailForegroundOffset = new Point(offsetX, offsetY);
        }

        /// <summary>
        /// Set the foreground texture (backgrnd2 - labels/overlay from UIWindow2.img/Stat/main)
        /// </summary>
        /// <param name="foreground">The foreground IDXObject</param>
        /// <param name="offsetX">X offset from canvas origin (negated)</param>
        /// <param name="offsetY">Y offset from canvas origin (negated)</param>
        public void SetForeground(IDXObject foreground, int offsetX, int offsetY)
        {
            _foreground = foreground;
            _foregroundOffset = new Point(offsetX, offsetY);
        }
        #endregion

        #region Drawing
        protected override void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            int windowX = this.Position.X;
            int windowY = this.Position.Y;

            // Draw foreground (backgrnd2 - labels/overlay) first
            if (_foreground != null)
            {
                _foreground.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    windowX + _foregroundOffset.X, windowY + _foregroundOffset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            if (_characterBuild == null || _statsFont == null)
                return;

            // Draw character info section
            DrawStatRow(sprite, windowX, windowY, NAME_Y, _characterBuild.Name ?? "Unknown");
            DrawStatRow(sprite, windowX, windowY, JOB_Y, _characterBuild.JobName ?? "Beginner");
            DrawStatRow(sprite, windowX, windowY, LEVEL_Y, _characterBuild.Level.ToString());
            DrawStatRow(sprite, windowX, windowY, GUILD_Y, "-");

            // Draw HP/MP
            DrawStatRow(sprite, windowX, windowY, HP_Y, $"{_characterBuild.HP}/{_characterBuild.MaxHP}");
            DrawStatRow(sprite, windowX, windowY, MP_Y, $"{_characterBuild.MP}/{_characterBuild.MaxMP}");

            // Draw AP
            Color apColor = _characterBuild.AP > 0 ? APAvailableColor : TextColorDark;
            sprite.DrawString(_statsFont, _characterBuild.AP.ToString(),
                new Vector2(windowX + VALUE_X + LEFT_BORDER_X + 10, windowY + AP_Y),
                apColor, 0f, Vector2.Zero, TEXT_SCALE, SpriteEffects.None, 0f);

            // Draw primary stats
            DrawStatRow(sprite, windowX, windowY, STR_Y, _characterBuild.STR.ToString());
            DrawStatRow(sprite, windowX, windowY, DEX_Y, _characterBuild.DEX.ToString());
            DrawStatRow(sprite, windowX, windowY, INT_Y, _characterBuild.INT.ToString());
            DrawStatRow(sprite, windowX, windowY, LUK_Y, _characterBuild.LUK.ToString());

            // Draw detail panel if open
            if (_isDetailMode && _detailBackground != null)
            {
                int detailX = windowX + WINDOW_WIDTH;
                int detailY = windowY + 80;

                _detailBackground.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    detailX, detailY, Color.White, false, drawReflectionInfo);

                // Draw detail foreground (backgrnd2 - labels)
                if (_detailForeground != null)
                {
                    _detailForeground.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                        detailX + _detailForegroundOffset.X, detailY + _detailForegroundOffset.Y,
                        Color.White, false, drawReflectionInfo);
                }

                DrawExtendedStats(sprite, detailX, detailY);
            }
        }

        private void DrawStatRow(SpriteBatch sprite, int windowX, int windowY, int y, string value, Color? color = null)
        {
            sprite.DrawString(_statsFont, value,
                new Vector2(windowX + VALUE_X + LEFT_BORDER_X, windowY + y),
                color ?? TextColorDark,
                0f, Vector2.Zero, TEXT_SCALE, SpriteEffects.None, 0f);
        }

        private void DrawExtendedStats(SpriteBatch sprite, int panelX, int panelY)
        {
            int startY = 56;  // Down by 2 rows (2 * 18 = 36) from original 20
            int lineHeight = 18;

            DrawDetailStatRow(sprite, panelX, panelY, startY, _characterBuild.Attack.ToString());
            DrawDetailStatRow(sprite, panelX, panelY, startY + lineHeight, _characterBuild.Defense.ToString());
            DrawDetailStatRow(sprite, panelX, panelY, startY + lineHeight * 2, _characterBuild.MagicAttack.ToString());
            DrawDetailStatRow(sprite, panelX, panelY, startY + lineHeight * 3, _characterBuild.MagicDefense.ToString());
            DrawDetailStatRow(sprite, panelX, panelY, startY + lineHeight * 4, _characterBuild.Accuracy.ToString());
            DrawDetailStatRow(sprite, panelX, panelY, startY + lineHeight * 5, _characterBuild.Avoidability.ToString());
            DrawDetailStatRow(sprite, panelX, panelY, startY + lineHeight * 6, "0");
            DrawDetailStatRow(sprite, panelX, panelY, startY + lineHeight * 7, "0%");
            DrawDetailStatRow(sprite, panelX, panelY, startY + lineHeight * 8, $"{_characterBuild.Speed:F0}%");
            DrawDetailStatRow(sprite, panelX, panelY, startY + lineHeight * 9, $"{_characterBuild.JumpPower:F0}%");
        }

        private void DrawDetailStatRow(SpriteBatch sprite, int panelX, int panelY, int y, string value)
        {
            sprite.DrawString(_statsFont, value,
                new Vector2(panelX + 75, panelY + y),
                TextColorDark,
                0f, Vector2.Zero, TEXT_SCALE, SpriteEffects.None, 0f);
        }
        #endregion

        #region Stat Modification
        public void IncreaseHP()
        {
            if (_characterBuild != null && _characterBuild.AP > 0)
            {
                _characterBuild.MaxHP += 20;  // Big Bang: HP increase per AP
                _characterBuild.HP += 20;
                _characterBuild.AP--;
            }
        }

        public void IncreaseMP()
        {
            if (_characterBuild != null && _characterBuild.AP > 0)
            {
                _characterBuild.MaxMP += 20;  // Big Bang: MP increase per AP
                _characterBuild.MP += 20;
                _characterBuild.AP--;
            }
        }

        public void IncreaseSTR()
        {
            if (_characterBuild != null && _characterBuild.AP > 0)
            {
                _characterBuild.STR++;
                _characterBuild.AP--;
            }
        }

        public void IncreaseDEX()
        {
            if (_characterBuild != null && _characterBuild.AP > 0)
            {
                _characterBuild.DEX++;
                _characterBuild.AP--;
            }
        }

        public void IncreaseINT()
        {
            if (_characterBuild != null && _characterBuild.AP > 0)
            {
                _characterBuild.INT++;
                _characterBuild.AP--;
            }
        }

        public void IncreaseLUK()
        {
            if (_characterBuild != null && _characterBuild.AP > 0)
            {
                _characterBuild.LUK++;
                _characterBuild.AP--;
            }
        }

        public void OpenDetailMode()
        {
            _isDetailMode = true;
            // TODO: Toggle button visibility when UIObject supports it
        }

        public void CloseDetailMode()
        {
            _isDetailMode = false;
            // TODO: Toggle button visibility when UIObject supports it
        }

        public void AddAbilityPoints(int amount)
        {
            if (_characterBuild != null)
            {
                _characterBuild.AP += amount;
            }
        }

        public void SetStats(int str, int dex, int intStat, int luk, int ap = 0)
        {
            if (_characterBuild != null)
            {
                _characterBuild.STR = str;
                _characterBuild.DEX = dex;
                _characterBuild.INT = intStat;
                _characterBuild.LUK = luk;
                _characterBuild.AP = ap;
            }
        }
        #endregion

        #region Event Handlers
        private void OnIncreaseHP(UIObject sender) => IncreaseHP();
        private void OnIncreaseMP(UIObject sender) => IncreaseMP();
        private void OnIncreaseSTR(UIObject sender) => IncreaseSTR();
        private void OnIncreaseDEX(UIObject sender) => IncreaseDEX();
        private void OnIncreaseINT(UIObject sender) => IncreaseINT();
        private void OnIncreaseLUK(UIObject sender) => IncreaseLUK();
        private void OnOpenDetail(UIObject sender) => OpenDetailMode();
        private void OnCloseDetail(UIObject sender) => CloseDetailMode();
        private void OnAutoAssign(UIObject sender) => AutoAssignAP();
        #endregion

        #region Auto-Assign
        public void AutoAssignAP()
        {
            if (_characterBuild == null || _characterBuild.AP <= 0)
                return;

            int jobId = _characterBuild.Job;
            int jobClass = jobId / 100;

            while (_characterBuild.AP > 0)
            {
                switch (jobClass)
                {
                    case 1: // Warrior
                        if (_characterBuild.STR % 5 == 0 && _characterBuild.DEX < _characterBuild.STR / 2)
                            IncreaseDEX();
                        else
                            IncreaseSTR();
                        break;

                    case 2: // Magician
                        if (_characterBuild.INT % 5 == 0 && _characterBuild.LUK < _characterBuild.INT / 2)
                            IncreaseLUK();
                        else
                            IncreaseINT();
                        break;

                    case 3: // Archer
                        if (_characterBuild.DEX % 5 == 0 && _characterBuild.STR < _characterBuild.DEX / 2)
                            IncreaseSTR();
                        else
                            IncreaseDEX();
                        break;

                    case 4: // Thief
                        if (_characterBuild.LUK % 5 == 0 && _characterBuild.DEX < _characterBuild.LUK / 2)
                            IncreaseDEX();
                        else
                            IncreaseLUK();
                        break;

                    case 5: // Pirate
                        if (_characterBuild.STR <= _characterBuild.DEX)
                            IncreaseSTR();
                        else
                            IncreaseDEX();
                        break;

                    default:
                        int minStat = Math.Min(Math.Min(_characterBuild.STR, _characterBuild.DEX),
                                               Math.Min(_characterBuild.INT, _characterBuild.LUK));
                        if (_characterBuild.STR == minStat)
                            IncreaseSTR();
                        else if (_characterBuild.DEX == minStat)
                            IncreaseDEX();
                        else if (_characterBuild.INT == minStat)
                            IncreaseINT();
                        else
                            IncreaseLUK();
                        break;
                }
            }
        }
        #endregion

        #region Update
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            bool hasAP = _characterBuild != null && _characterBuild.AP > 0;
            _btnIncHP?.SetEnabled(hasAP);
            _btnIncMP?.SetEnabled(hasAP);
            _btnIncSTR?.SetEnabled(hasAP);
            _btnIncDEX?.SetEnabled(hasAP);
            _btnIncINT?.SetEnabled(hasAP);
            _btnIncLUK?.SetEnabled(hasAP);
            _btnAutoAssign?.SetEnabled(hasAP);
        }
        #endregion
    }
}
