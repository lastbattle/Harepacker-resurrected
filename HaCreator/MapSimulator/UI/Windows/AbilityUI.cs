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
    /// Ability/Stat UI window displaying character stats (STR, DEX, INT, LUK)
    /// Opened with the "A" key in MapleStory
    /// Structure: UI.wz/UIWindow.img/Stat/
    ///
    /// Layout from WZ (175x347 pixels):
    /// - Title bar: "CHARACTER STAT" at top
    /// - Info section: NAME, JOB, LEVEL, GUILD, HP, MP, EXP, FAME
    /// - ABILITY POINT section header
    /// - Stats section (pink): STR, DEX, INT, LUK with +/- buttons
    /// </summary>
    public class AbilityUI : UIWindowBase
    {
        #region Constants
        // Text rendering scale (MapleStory uses small pixel fonts ~8-10px)
        private const float TEXT_SCALE = 0.5f;

        // Row heights in the stat window (from WZ layout)
        private const int ROW_HEIGHT = 18;

        // Y offset compensator to align text with background labels
        // Adjust this value if text doesn't align properly with the background image
        private const int Y_OFFSET = 18;  // Shift down by 1 tab (18 pixels) - for STR, DEX, INT, LUK

        // Info section offset (NAME, JOB)
        private const int INFO_Y_OFFSET = 0;  // No offset needed

        // Level offset
        private const int LEVEL_Y_OFFSET = 9;  // Shift down by half tab (9 pixels)

        // Vital stats offset (GUILD, HP, MP, EXP, FAME)
        private const int VITAL_Y_OFFSET = 9;  // Shift down by half tab (9 pixels)


        // X positions for labels and values (from IDA Pro analysis of CUIStat::Draw)
        private const int LABEL_X = 10;
        private const int VALUE_X = 54;  // IDA Pro: CUIStat::Draw DrawTextA(canvas, 54, Y, ...)

        // Left border width offset for X positions
        private const int LEFT_BORDER_X = 4;

        // Y positions from IDA Pro analysis of CUIStat::Draw DrawTextA calls (client values)
        private const int CLIENT_NAME_Y = 32;
        private const int CLIENT_JOB_Y = 50;
        private const int CLIENT_LEVEL_Y = 68;
        private const int CLIENT_GUILD_Y = 86;
        private const int CLIENT_HP_Y = 104;
        private const int CLIENT_MP_Y = 122;
        private const int CLIENT_EXP_Y = 140;
        private const int CLIENT_FAME_Y = 158;
        private const int CLIENT_AP_Y = 200;
        private const int CLIENT_STR_Y = 227;
        private const int CLIENT_DEX_Y = 245;
        private const int CLIENT_INT_Y = 263;
        private const int CLIENT_LUK_Y = 281;

        // Button position X (right side of stat values)
        private const int STAT_BUTTON_X = 155;
        #endregion

        #region Fields
        // Reference to character build for stat display
        private CharacterBuild _characterBuild;

        // Stat increase buttons
        private UIObject _btnIncSTR;
        private UIObject _btnIncDEX;
        private UIObject _btnIncINT;
        private UIObject _btnIncLUK;

        // Detail/expand button
        private UIObject _btnDetail;
        private bool _isDetailMode = false;

        // Auto-assign button
        private UIObject _btnAutoAssign;

        // Detail background texture (for expanded stats view)
        private IDXObject _detailBackground;

        // Font for rendering stats
        private SpriteFont _statsFont;

        // Graphics device for creating textures
        private GraphicsDevice _device;

        // Colors matching MapleStory UI
        private static readonly Color TextColorDark = new Color(0, 0, 0);           // Black text on light backgrounds
        private static readonly Color TextColorWhite = new Color(255, 255, 255);    // White text
        private static readonly Color APAvailableColor = new Color(50, 200, 50);    // Green for available AP
        private static readonly Color StatBonusColor = new Color(50, 150, 255);     // Blue for bonus stats
        #endregion

        #region Properties
        public override string WindowName => "Ability";

        /// <summary>
        /// The character build to display stats for
        /// </summary>
        public override CharacterBuild CharacterBuild
        {
            get => _characterBuild;
            set => _characterBuild = value;
        }

        /// <summary>
        /// Whether detail mode is enabled (shows extended stats)
        /// </summary>
        public bool IsDetailMode => _isDetailMode;
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="frame">Window background frame</param>
        /// <param name="device">Graphics device</param>
        public AbilityUI(IDXObject frame, GraphicsDevice device)
            : base(frame)
        {
            _device = device;
        }
        #endregion

        #region Initialization
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
        /// Initialize detail button
        /// </summary>
        public void InitializeDetailButton(UIObject btnDetail)
        {
            _btnDetail = btnDetail;
            if (btnDetail != null)
            {
                AddButton(btnDetail);
                btnDetail.ButtonClickReleased += OnToggleDetail;
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

        /// <summary>
        /// Set the font for stat text rendering
        /// </summary>
        public override void SetFont(SpriteFont font)
        {
            _statsFont = font;
        }

        /// <summary>
        /// Set the detail background texture (backgrnd3 from UIWindow.img/Stat)
        /// </summary>
        public void SetDetailBackground(IDXObject detailBg)
        {
            _detailBackground = detailBg;
        }
        #endregion

        #region Drawing
        /// <summary>
        /// Draw the ability window contents
        /// Positions are based on UIWindow.img/Stat layout (175x347 pixels)
        /// </summary>
        protected override void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            if (_characterBuild == null || _statsFont == null)
                return;

            // All positions are relative to window position
            int windowX = this.Position.X;
            int windowY = this.Position.Y;

            // Y positions using client constants + compensator offsets
            // Info section uses INFO_Y_OFFSET
            int nameY = CLIENT_NAME_Y + INFO_Y_OFFSET;
            int jobY = CLIENT_JOB_Y + INFO_Y_OFFSET;
            int levelY = CLIENT_LEVEL_Y + LEVEL_Y_OFFSET;
            int guildY = CLIENT_GUILD_Y + VITAL_Y_OFFSET;
            // Vital stats use VITAL_Y_OFFSET
            int hpY = CLIENT_HP_Y + VITAL_Y_OFFSET;
            int mpY = CLIENT_MP_Y + VITAL_Y_OFFSET;
            int expY = CLIENT_EXP_Y + VITAL_Y_OFFSET;
            int fameY = CLIENT_FAME_Y + VITAL_Y_OFFSET;
            // Stats section uses Y_OFFSET
            int apLabelY = CLIENT_AP_Y + Y_OFFSET;
            int strY = CLIENT_STR_Y + Y_OFFSET;
            int dexY = CLIENT_DEX_Y + Y_OFFSET;
            int intY = CLIENT_INT_Y + Y_OFFSET;
            int lukY = CLIENT_LUK_Y + Y_OFFSET;

            // Draw character info section
            DrawStatRow(sprite, windowX, windowY, nameY, _characterBuild.Name ?? "Unknown");
            DrawStatRow(sprite, windowX, windowY, jobY, _characterBuild.JobName ?? "Beginner");
            DrawStatRow(sprite, windowX, windowY, levelY, _characterBuild.Level.ToString());
            DrawStatRow(sprite, windowX, windowY, guildY, "-");  // Guild placeholder
            DrawStatRow(sprite, windowX, windowY, hpY, $"{_characterBuild.HP}/{_characterBuild.MaxHP}");
            DrawStatRow(sprite, windowX, windowY, mpY, $"{_characterBuild.MP}/{_characterBuild.MaxMP}");
            DrawStatRow(sprite, windowX, windowY, expY, "0%");  // EXP placeholder
            DrawStatRow(sprite, windowX, windowY, fameY, "0");  // Fame placeholder

            // Draw AP (ability points) with highlight if available, offset 10px right
            Color apColor = _characterBuild.AP > 0 ? APAvailableColor : TextColorDark;
            sprite.DrawString(_statsFont, _characterBuild.AP.ToString(),
                new Vector2(windowX + VALUE_X + LEFT_BORDER_X + 10, windowY + apLabelY),
                apColor, 0f, Vector2.Zero, TEXT_SCALE, SpriteEffects.None, 0f);

            // Draw primary stats (STR, DEX, INT, LUK)
            DrawStatRow(sprite, windowX, windowY, strY, _characterBuild.STR.ToString());
            DrawStatRow(sprite, windowX, windowY, dexY, _characterBuild.DEX.ToString());
            DrawStatRow(sprite, windowX, windowY, intY, _characterBuild.INT.ToString());
            DrawStatRow(sprite, windowX, windowY, lukY, _characterBuild.LUK.ToString());

            // If in detail mode, draw detail background and extended stats
            if (_isDetailMode && _detailBackground != null)
            {
                // Draw detail background to the right of main window
                // Main window: 175x347, Detail panel (backgrnd3): 177x221
                int detailX = windowX + 175;  // Main window width
                int detailY = windowY + 108;  // 126 - 18 = 108

                // Use DrawBackground method from IDXObject interface
                _detailBackground.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    detailX, detailY, Color.White, false, drawReflectionInfo);

                // Draw extended stats on detail panel
                DrawExtendedStats(sprite, detailX, detailY);
            }
        }

        /// <summary>
        /// Draw a stat row value at the specified Y position
        /// </summary>
        private void DrawStatRow(SpriteBatch sprite, int windowX, int windowY, int y, string value, Color? color = null)
        {
            // Values are drawn at right side of each row, offset by left border width
            sprite.DrawString(_statsFont, value,
                new Vector2(windowX + VALUE_X + LEFT_BORDER_X, windowY + y),
                color ?? TextColorDark,
                0f, Vector2.Zero, TEXT_SCALE, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// Draw extended stats on the detail panel
        /// Based on UIWindow.img/Stat/backgrnd3 layout
        /// </summary>
        private void DrawExtendedStats(SpriteBatch sprite, int panelX, int panelY)
        {
            // Detail panel row positions
            int startY = 7;
            int lineHeight = 18;

            DrawDetailStatRow(sprite, panelX, panelY, startY, _characterBuild.Attack.ToString());
            DrawDetailStatRow(sprite, panelX, panelY, startY + lineHeight, _characterBuild.Defense.ToString());
            DrawDetailStatRow(sprite, panelX, panelY, startY + lineHeight * 2, _characterBuild.MagicAttack.ToString());
            DrawDetailStatRow(sprite, panelX, panelY, startY + lineHeight * 3, _characterBuild.MagicDefense.ToString());
            DrawDetailStatRow(sprite, panelX, panelY, startY + lineHeight * 4, _characterBuild.Accuracy.ToString());
            DrawDetailStatRow(sprite, panelX, panelY, startY + lineHeight * 5, _characterBuild.Avoidability.ToString());
            DrawDetailStatRow(sprite, panelX, panelY, startY + lineHeight * 6, "0");  // Hands placeholder
            DrawDetailStatRow(sprite, panelX, panelY, startY + lineHeight * 7, "0%");  // Critical placeholder
            DrawDetailStatRow(sprite, panelX, panelY, startY + lineHeight * 8, $"{_characterBuild.Speed:F0}%");
            DrawDetailStatRow(sprite, panelX, panelY, startY + lineHeight * 9, $"{_characterBuild.JumpPower:F0}%");
        }

        /// <summary>
        /// Draw a detail stat row value
        /// </summary>
        private void DrawDetailStatRow(SpriteBatch sprite, int panelX, int panelY, int y, string value)
        {
            sprite.DrawString(_statsFont, value,
                new Vector2(panelX + 75, panelY + y),
                TextColorDark,
                0f, Vector2.Zero, TEXT_SCALE, SpriteEffects.None, 0f);
        }
        #endregion

        #region Stat Modification
        /// <summary>
        /// Increase STR by 1 (if AP available)
        /// </summary>
        public void IncreaseSTR()
        {
            if (_characterBuild != null && _characterBuild.AP > 0)
            {
                _characterBuild.STR++;
                _characterBuild.AP--;
            }
        }

        /// <summary>
        /// Increase DEX by 1 (if AP available)
        /// </summary>
        public void IncreaseDEX()
        {
            if (_characterBuild != null && _characterBuild.AP > 0)
            {
                _characterBuild.DEX++;
                _characterBuild.AP--;
            }
        }

        /// <summary>
        /// Increase INT by 1 (if AP available)
        /// </summary>
        public void IncreaseINT()
        {
            if (_characterBuild != null && _characterBuild.AP > 0)
            {
                _characterBuild.INT++;
                _characterBuild.AP--;
            }
        }

        /// <summary>
        /// Increase LUK by 1 (if AP available)
        /// </summary>
        public void IncreaseLUK()
        {
            if (_characterBuild != null && _characterBuild.AP > 0)
            {
                _characterBuild.LUK++;
                _characterBuild.AP--;
            }
        }

        /// <summary>
        /// Toggle detail mode
        /// </summary>
        public void ToggleDetailMode()
        {
            _isDetailMode = !_isDetailMode;
        }

        /// <summary>
        /// Add ability points (e.g., on level up)
        /// </summary>
        public void AddAbilityPoints(int amount)
        {
            if (_characterBuild != null)
            {
                _characterBuild.AP += amount;
            }
        }

        /// <summary>
        /// Set all stats at once (for initialization)
        /// </summary>
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
        private void OnIncreaseSTR(UIObject sender)
        {
            IncreaseSTR();
        }

        private void OnIncreaseDEX(UIObject sender)
        {
            IncreaseDEX();
        }

        private void OnIncreaseINT(UIObject sender)
        {
            IncreaseINT();
        }

        private void OnIncreaseLUK(UIObject sender)
        {
            IncreaseLUK();
        }

        private void OnToggleDetail(UIObject sender)
        {
            ToggleDetailMode();
        }

        private void OnAutoAssign(UIObject sender)
        {
            AutoAssignAP();
        }
        #endregion

        #region Auto-Assign
        /// <summary>
        /// Automatically assign all available AP based on job class
        /// Warriors: STR primary, DEX secondary
        /// Magicians: INT primary, LUK secondary
        /// Archers: DEX primary, STR secondary
        /// Thieves: LUK primary, DEX secondary
        /// Pirates: STR/DEX balanced
        /// Beginners: Balanced distribution
        /// </summary>
        public void AutoAssignAP()
        {
            if (_characterBuild == null || _characterBuild.AP <= 0)
                return;

            // Determine primary/secondary stats based on job
            int jobId = _characterBuild.Job;
            int jobClass = jobId / 100;  // First digit determines class

            while (_characterBuild.AP > 0)
            {
                switch (jobClass)
                {
                    case 1: // Warrior
                        // STR primary (4 out of 5), DEX secondary (1 out of 5)
                        if (_characterBuild.STR % 5 == 0 && _characterBuild.DEX < _characterBuild.STR / 2)
                            IncreaseDEX();
                        else
                            IncreaseSTR();
                        break;

                    case 2: // Magician
                        // INT primary (4 out of 5), LUK secondary (1 out of 5)
                        if (_characterBuild.INT % 5 == 0 && _characterBuild.LUK < _characterBuild.INT / 2)
                            IncreaseLUK();
                        else
                            IncreaseINT();
                        break;

                    case 3: // Archer
                        // DEX primary (4 out of 5), STR secondary (1 out of 5)
                        if (_characterBuild.DEX % 5 == 0 && _characterBuild.STR < _characterBuild.DEX / 2)
                            IncreaseSTR();
                        else
                            IncreaseDEX();
                        break;

                    case 4: // Thief
                        // LUK primary (4 out of 5), DEX secondary (1 out of 5)
                        if (_characterBuild.LUK % 5 == 0 && _characterBuild.DEX < _characterBuild.LUK / 2)
                            IncreaseDEX();
                        else
                            IncreaseLUK();
                        break;

                    case 5: // Pirate
                        // STR and DEX balanced
                        if (_characterBuild.STR <= _characterBuild.DEX)
                            IncreaseSTR();
                        else
                            IncreaseDEX();
                        break;

                    default: // Beginner or unknown - balanced distribution
                        // Distribute evenly across all stats
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

            // Enable/disable stat buttons based on AP availability
            bool hasAP = _characterBuild != null && _characterBuild.AP > 0;
            _btnIncSTR?.SetEnabled(hasAP);
            _btnIncDEX?.SetEnabled(hasAP);
            _btnIncINT?.SetEnabled(hasAP);
            _btnIncLUK?.SetEnabled(hasAP);
            _btnAutoAssign?.SetEnabled(hasAP);
        }
        #endregion
    }
}
