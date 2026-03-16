using HaSharedLibrary.Render.DX;
using MapleLib.Helpers;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MapleLib.WzLib.WzStructure.Data;
using Spine;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance.Misc;

namespace HaCreator.MapSimulator.Effects
{
    /// <summary>
    /// Special Effect Field System - Manages specialized field types from MapleStory client.
    ///
    /// Handles:
    /// - CField_Wedding: Wedding ceremony effects (packets 379, 380)
    /// - CField_Witchtower: Witch tower score tracking (packet 358)
    /// - CField_GuildBoss: Guild boss healer/pulley mechanics (packets 344, 345)
    /// - CField_Dojang: Mu Lung Dojo timer and HUD gauges
    /// - CField_SpaceGAGA: Rescue Gaga timerboard clock
    /// - CField_Massacre: Kill counting and gauge system (packet 173)
    /// </summary>
    public class SpecialEffectFields
    {
        #region Sub-systems
        private readonly WeddingField _wedding = new();
        private readonly WitchtowerField _witchtower = new();
        private readonly BattlefieldField _battlefield = new();
        private readonly GuildBossField _guildBoss = new();
        private readonly DojoField _dojo = new();
        private readonly SpaceGagaField _spaceGaga = new();
        private readonly MassacreField _massacre = new();
        #endregion

        #region Public Access
        public WeddingField Wedding => _wedding;
        public WitchtowerField Witchtower => _witchtower;
        public BattlefieldField Battlefield => _battlefield;
        public GuildBossField GuildBoss => _guildBoss;
        public DojoField Dojo => _dojo;
        public SpaceGagaField SpaceGaga => _spaceGaga;
        public MassacreField Massacre => _massacre;
        public bool HasBlockingScriptedSequence => _wedding.HasActiveScriptedDialog;

        public void SetWeddingPlayerState(int? localCharacterId, Vector2? localWorldPosition)
        {
            _wedding.SetLocalPlayerState(localCharacterId, localWorldPosition);
        }

        public void SetDojoRuntimeState(int? playerHp, int? playerMaxHp, float? bossHpPercent)
        {
            _dojo.SetRuntimeState(playerHp, playerMaxHp, bossHpPercent);
        }
        #endregion

        #region Initialization
        public void Initialize(GraphicsDevice device)
        {
            _wedding.Initialize(device);
            _witchtower.Initialize(device);
            _battlefield.Initialize(device);
            _guildBoss.Initialize(device);
            _dojo.Initialize(device);
            _spaceGaga.Initialize(device);
            _massacre.Initialize(device);
        }

        /// <summary>
        /// Detect and enable appropriate field type based on map ID
        /// </summary>
        public void DetectFieldType(int mapId, FieldType? fieldType = null)
        {
            // Wedding maps: 680000110 (Cathedral), 680000210 (Chapel)
            if (fieldType == FieldType.FIELDTYPE_WEDDING || mapId == 680000110 || mapId == 680000210)
            {
                _wedding.Enable(mapId);
                System.Diagnostics.Debug.WriteLine($"[SpecialEffectFields] Wedding field detected: {mapId}");
            }
            // Witchtower maps (would be in 900000000 range typically)
            else if (IsWitchtowerMap(mapId, fieldType))
            {
                _witchtower.Enable();
                System.Diagnostics.Debug.WriteLine($"[SpecialEffectFields] Witchtower field detected: {mapId}");
            }
            else if (IsBattlefieldMap(mapId, fieldType))
            {
                _battlefield.Enable();
                System.Diagnostics.Debug.WriteLine($"[SpecialEffectFields] Battlefield field detected: {mapId}");
            }
            // Guild boss maps (e.g., Ergoth, Shao maps)
            else if (IsGuildBossMap(mapId, fieldType))
            {
                _guildBoss.Enable();
                System.Diagnostics.Debug.WriteLine($"[SpecialEffectFields] GuildBoss field detected: {mapId}");
            }
            else if (IsDojoMap(mapId, fieldType))
            {
                _dojo.Enable(mapId);
                System.Diagnostics.Debug.WriteLine($"[SpecialEffectFields] Dojo field detected: {mapId}");
            }
            else if (IsSpaceGagaMap(mapId, fieldType))
            {
                _spaceGaga.Enable(mapId);
                System.Diagnostics.Debug.WriteLine($"[SpecialEffectFields] SpaceGAGA field detected: {mapId}");
            }
            // Massacre maps (special event PQ maps)
            else if (IsMassacreMap(mapId, fieldType))
            {
                _massacre.Enable();
                System.Diagnostics.Debug.WriteLine($"[SpecialEffectFields] Massacre field detected: {mapId}");
            }
        }

        public void ConfigureMap(Board board)
        {
            if (_battlefield.IsActive)
            {
                _battlefield.Configure(board?.MapInfo);
            }

            if (_guildBoss.IsActive)
            {
                //_guildBoss.ConfigureFromBoard(board);
            }
        }

        private static bool IsWitchtowerMap(int mapId, FieldType? fieldType)
        {
            // Witchtower maps - typically special event maps
            return fieldType == FieldType.FIELDTYPE_WITCHTOWER
                || (mapId >= 922000000 && mapId <= 922000099);
        }

        private static bool IsGuildBossMap(int mapId, FieldType? fieldType)
        {
            // Guild boss maps (Ergoth, Shao, etc.)
            // Examples: 610030000 series, 673000000 series
            return fieldType == FieldType.FIELDTYPE_GUILDBOSS
                || (mapId >= 610030000 && mapId <= 610030099)
                || (mapId >= 673000000 && mapId <= 673000099);
        }

        private static bool IsBattlefieldMap(int mapId, FieldType? fieldType)
        {
            return fieldType == FieldType.FIELDTYPE_BATTLEFIELD
                || (mapId >= 910040000 && mapId <= 910041399);
        }

        private static bool IsMassacreMap(int mapId, FieldType? fieldType)
        {
            // Massacre/hunting event maps
            return fieldType == FieldType.FIELDTYPE_MASSACRE
                || fieldType == FieldType.FIELDTYPE_MASSACRE_RESULT
                || (mapId >= 910000000 && mapId <= 910000099);
        }

        private static bool IsDojoMap(int mapId, FieldType? fieldType)
        {
            return fieldType == FieldType.FIELDTYPE_DOJANG
                || (mapId >= 925020000 && mapId <= 925040999);
        }

        private static bool IsSpaceGagaMap(int mapId, FieldType? fieldType)
        {
            return fieldType == FieldType.FIELDTYPE_SPACEGAGA
                || (mapId >= 922240000 && mapId <= 922240200);
        }
        #endregion

        #region Update
        public void Update(GameTime gameTime, int currentTimeMs)
        {
            float deltaSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_wedding.IsActive)
                _wedding.Update(currentTimeMs, deltaSeconds);

            if (_witchtower.IsActive)
                _witchtower.Update(currentTimeMs, deltaSeconds);

            if (_battlefield.IsActive)
                _battlefield.Update(currentTimeMs, deltaSeconds);

            if (_guildBoss.IsActive)
                _guildBoss.Update(currentTimeMs, deltaSeconds);

            if (_dojo.IsActive)
                _dojo.Update(currentTimeMs, deltaSeconds);

            if (_spaceGaga.IsActive)
                _spaceGaga.Update(currentTimeMs, deltaSeconds);

            if (_massacre.IsActive)
                _massacre.Update(currentTimeMs, deltaSeconds);
        }
        #endregion

        #region Draw
        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int tickCount,
            Texture2D pixelTexture, SpriteFont font = null)
        {
            if (_wedding.IsActive)
                _wedding.Draw(spriteBatch, skeletonMeshRenderer, gameTime, mapShiftX, mapShiftY, centerX, centerY, tickCount, pixelTexture, font);

            if (_witchtower.IsActive)
                _witchtower.Draw(spriteBatch, pixelTexture, font);

            if (_battlefield.IsActive)
                _battlefield.Draw(spriteBatch, pixelTexture, font);

            if (_guildBoss.IsActive)
                _guildBoss.Draw(spriteBatch, skeletonMeshRenderer, gameTime, mapShiftX, mapShiftY, centerX, centerY, tickCount, pixelTexture, font);

            if (_dojo.IsActive)
                _dojo.Draw(spriteBatch, pixelTexture, font, tickCount);

            if (_spaceGaga.IsActive)
                _spaceGaga.Draw(spriteBatch, pixelTexture, font);

            if (_massacre.IsActive)
                _massacre.Draw(spriteBatch, pixelTexture, font);
        }
        #endregion

        #region Reset
        public void ResetAll()
        {
            _wedding.Reset();
            _witchtower.Reset();
            _battlefield.Reset();
            _guildBoss.Reset();
            _dojo.Reset();
            _spaceGaga.Reset();
            _massacre.Reset();
        }
        #endregion
    }

    #region Wedding Field (CField_Wedding)
    /// <summary>
    /// Wedding Field - Wedding ceremony effects and dialogs.
    ///
    /// - Map 680000110: Cathedral wedding (NPC 9201011)
    /// - Map 680000210: Chapel wedding (NPC 9201002)
    /// - OnWeddingProgress (packet 379): Wedding step progress with dialogs
    /// - OnWeddingCeremonyEnd (packet 380): Ceremony completion with bless effect
    /// - SetBlessEffect: Sparkle animation between groom and bride positions
    /// </summary>
    public class WeddingField
    {
        private const int CathedralWeddingMapId = 680000110;
        private const int ChapelWeddingMapId = 680000210;
        private const int CathedralNpcId = 9201011;
        private const int ChapelNpcId = 9201002;
        private const int DialogDurationMs = 5000;
        private const string ChapelGuestBlessPromptText = "Would you like to give your blessing to the couple?";

        private static readonly Dictionary<int, Dictionary<int, string>> WeddingDialogFallbacks = new()
        {
            [ChapelNpcId] = new Dictionary<int, string>
            {
                [0] = "Dearly beloved, we are gathered here today to celebrate the marriage of these two fine, upstanding people. One can clearly see the love between you two, and it's a sight I'll never tire of. You have proved your love and received your Parent's Blessing. Do you wish to seal your love in the eternal embrace of marriage?",
                [1] = "Very well. Guests may now Bless the couple if they choose...",
                [3] = "By the power vested in me through the mighty Maple tree, I now pronounce you Husband and Wife. You may kiss the bride!",
                [4] = "With the Blessing of the Maple tree, I wish both of you a long and safe marriage."
            },
            [CathedralNpcId] = new Dictionary<int, string>
            {
                [0] = "We are gathered here today to celebrate the union of these two love birds. I've never seen a better-looking couple in all my years of running this Chapel. So, do you want to travel the world and spend the rest of your life with your chosen spouse?",
                [1] = "Very well! I pronounce you Husband and Wife. You may kiss the bride!",
                [2] = "You two truly are a sight to behold...you may leave the Chapel now, and may the blessing of the Maple tree be with you."
            }
        };

        #region State
        private bool _isActive = false;
        private int _mapId;
        private int _npcId;
        private int? _localCharacterId;
        private Vector2? _localPlayerPosition;
        private Vector2? _groomPosition;
        private Vector2? _bridePosition;

        // Wedding step (from OnWeddingProgress)
        private int _currentStep = 0;
        private int _groomId = 0;
        private int _brideId = 0;

        // Bless effect (sparkles between couple)
        private bool _blessEffectActive = false;
        private float _blessEffectAlpha = 0f;
        private int _blessEffectStartTime;
        private const int BLESS_EFFECT_DURATION = 5000; // 5 seconds
        #endregion

        #region Visual Effects
        private List<WeddingSparkle> _sparkles = new();
        private List<IDXObject> _blessFrames;
        private Random _random = new();
        #endregion

        #region Dialog
        private WeddingDialog _currentDialog;
        private readonly Queue<WeddingDialog> _dialogQueue = new();
        #endregion

        #region Public Properties
        public bool IsActive => _isActive;
        public int CurrentStep => _currentStep;
        public bool IsBlessEffectActive => _blessEffectActive;
        public bool HasActiveScriptedDialog => _currentDialog != null || _dialogQueue.Count > 0;
        public WeddingParticipantRole LocalParticipantRole { get; private set; } = WeddingParticipantRole.Guest;
        public string CurrentDialogMessage => _currentDialog?.Message;
        public WeddingDialogMode CurrentDialogMode => _currentDialog?.Mode ?? WeddingDialogMode.Text;
        public bool IsGuestBlessPromptActive => _currentDialog?.Mode == WeddingDialogMode.YesNo;
        public Vector2? BlessEffectWorldCenter => TryGetBlessEffectWorldCenter();
        #endregion

        #region Initialization
        public void Initialize(GraphicsDevice device)
        {
            // Load bless effect frames from Effect.wz if available
        }

        public void Enable(int mapId)
        {
            _isActive = true;
            _mapId = mapId;
            _currentStep = 0;

            // Set NPC based on map (from client: 680000110 = 9201011, 680000210 = 9201002)
            _npcId = mapId == CathedralWeddingMapId ? CathedralNpcId : ChapelNpcId;
            _groomId = 0;
            _brideId = 0;
            _groomPosition = null;
            _bridePosition = null;
            LocalParticipantRole = WeddingParticipantRole.Guest;

            System.Diagnostics.Debug.WriteLine($"[WeddingField] Enabled for map {mapId}, NPC {_npcId}");
        }

        public void SetBlessFrames(List<IDXObject> frames)
        {
            _blessFrames = frames;
        }

        public void SetLocalPlayerState(int? localCharacterId, Vector2? localWorldPosition)
        {
            _localCharacterId = localCharacterId;
            _localPlayerPosition = localWorldPosition;
            UpdateParticipantState();
        }
        #endregion

        #region Packet Handling (matching CField_Wedding)

        /// <summary>
        /// OnWeddingProgress - Packet 379
        /// Updates wedding step and shows dialog
        /// </summary>
        public void OnWeddingProgress(int step, int groomId, int brideId, int currentTimeMs)
        {
            System.Diagnostics.Debug.WriteLine($"[WeddingField] OnWeddingProgress: step={step}, groom={groomId}, bride={brideId}");

            _currentStep = step;
            _groomId = groomId;
            _brideId = brideId;
            UpdateParticipantState();

            // Step 0: Start ceremony - play wedding BGM
            if (step == 0)
            {
                SetBlessEffect(false, currentTimeMs);
                // Would trigger: CSoundMan::PlayBGM for wedding music
                // Cathedral: 0x108E, Chapel: 0x108F from StringPool
            }

            // Show dialog for current step
            ShowWeddingDialog(step, currentTimeMs);
        }

        /// <summary>
        /// OnWeddingCeremonyEnd - Packet 380
        /// Triggers the bless effect (sparkles)
        /// </summary>
        public void OnWeddingCeremonyEnd(int currentTimeMs)
        {
            System.Diagnostics.Debug.WriteLine("[WeddingField] OnWeddingCeremonyEnd - Starting bless effect");
            SetBlessEffect(true, currentTimeMs);
        }

        /// <summary>
        /// SetBlessEffect - Start/stop sparkle effect between couple
        /// Based on client's CField_Wedding::SetBlessEffect
        /// </summary>
        public void SetBlessEffect(bool active, int currentTimeMs)
        {
            _blessEffectActive = active;
            if (active)
            {
                _blessEffectStartTime = currentTimeMs;
                _blessEffectAlpha = 0f;

                // Create sparkles
                _sparkles.Clear();
                for (int i = 0; i < 50; i++)
                {
                    _sparkles.Add(new WeddingSparkle
                    {
                        X = (float)(_random.NextDouble() - 0.5f) * 80f,
                        Y = (float)(_random.NextDouble() - 0.5f) * 40f,
                        Scale = 0.5f + (float)_random.NextDouble() * 0.5f,
                        Alpha = 0f,
                        Velocity = new Vector2((float)(_random.NextDouble() - 0.5f) * 12f, (float)(_random.NextDouble() - 0.25f) * 10f),
                        LifeTime = 2000 + _random.Next(3000),
                        SpawnDelay = i * 100
                    });
                }
            }
            else
            {
                _blessEffectAlpha = 0f;
                _sparkles.Clear();
            }
        }

        private void ShowWeddingDialog(int step, int currentTimeMs)
        {
            _currentDialog = CreateDialogForStep(step, currentTimeMs);
        }

        private WeddingDialog CreateDialogForStep(int step, int currentTimeMs)
        {
            if (_mapId == ChapelWeddingMapId && step == 2)
            {
                if (LocalParticipantRole == WeddingParticipantRole.Guest)
                {
                    return new WeddingDialog
                    {
                        Message = ChapelGuestBlessPromptText,
                        NpcId = _npcId,
                        StartTime = currentTimeMs,
                        Duration = DialogDurationMs,
                        Mode = WeddingDialogMode.YesNo
                    };
                }

                return null;
            }

            string dialogText = ResolveWeddingDialogText(_npcId, step);
            if (string.IsNullOrWhiteSpace(dialogText))
            {
                return null;
            }

            return new WeddingDialog
            {
                Message = dialogText,
                NpcId = _npcId,
                StartTime = currentTimeMs,
                Duration = DialogDurationMs,
                Mode = WeddingDialogMode.Text
            };
        }

        private string ResolveWeddingDialogText(int npcId, int step)
        {
            string propertyName = $"wedding{step}";
            try
            {
                WzImage npcImage = global::HaCreator.Program.FindImage("String", "Npc.img");
                string liveValue = npcImage?[npcId.ToString()]?[propertyName]?.GetString();
                if (!string.IsNullOrWhiteSpace(liveValue))
                {
                    return liveValue.Trim();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WeddingField] Failed to load {propertyName} for NPC {npcId}: {ex.Message}");
            }

            if (WeddingDialogFallbacks.TryGetValue(npcId, out Dictionary<int, string> dialogSet)
                && dialogSet.TryGetValue(step, out string fallback))
            {
                return fallback;
            }

            return null;
        }

        private void UpdateParticipantState()
        {
            LocalParticipantRole = WeddingParticipantRole.Guest;
            if (_localCharacterId.HasValue)
            {
                if (_localCharacterId.Value == _groomId)
                {
                    LocalParticipantRole = WeddingParticipantRole.Groom;
                }
                else if (_localCharacterId.Value == _brideId)
                {
                    LocalParticipantRole = WeddingParticipantRole.Bride;
                }
            }

            if (_localPlayerPosition.HasValue)
            {
                if (LocalParticipantRole == WeddingParticipantRole.Groom)
                {
                    _groomPosition = _localPlayerPosition;
                }
                else if (LocalParticipantRole == WeddingParticipantRole.Bride)
                {
                    _bridePosition = _localPlayerPosition;
                }
            }
        }

        private Vector2? TryGetBlessEffectWorldCenter()
        {
            Vector2? groom = _groomPosition;
            Vector2? bride = _bridePosition;

            if (!groom.HasValue && !bride.HasValue)
            {
                return null;
            }

            if (!groom.HasValue && bride.HasValue)
            {
                groom = new Vector2(bride.Value.X - 40f, bride.Value.Y);
            }

            if (!bride.HasValue && groom.HasValue)
            {
                bride = new Vector2(groom.Value.X + 40f, groom.Value.Y);
            }

            return new Vector2(
                (groom!.Value.X + bride!.Value.X) * 0.5f,
                ((groom.Value.Y + bride.Value.Y) * 0.5f) - 20f);
        }

        private Vector2 GetBlessEffectScreenCenter(int mapShiftX, int mapShiftY, int centerX, int centerY, int screenWidth, int screenHeight)
        {
            Vector2? worldCenter = TryGetBlessEffectWorldCenter();
            if (worldCenter.HasValue)
            {
                return new Vector2(
                    worldCenter.Value.X - mapShiftX + centerX,
                    worldCenter.Value.Y - mapShiftY + centerY);
            }

            return new Vector2(screenWidth * 0.5f, screenHeight * 0.5f - 20f);
        }
        #endregion

        #region Update
        public void Update(int currentTimeMs, float deltaSeconds)
        {
            if (!_isActive) return;

            // Update bless effect
            if (_blessEffectActive)
            {
                int elapsed = currentTimeMs - _blessEffectStartTime;

                // Fade in (first 500ms)
                if (elapsed < 500)
                {
                    _blessEffectAlpha = elapsed / 500f;
                }
                // Fade out (last 500ms)
                else if (elapsed > BLESS_EFFECT_DURATION - 500)
                {
                    _blessEffectAlpha = (BLESS_EFFECT_DURATION - elapsed) / 500f;
                }
                else
                {
                    _blessEffectAlpha = 1f;
                }

                // End effect
                if (elapsed >= BLESS_EFFECT_DURATION)
                {
                    _blessEffectActive = false;
                    _blessEffectAlpha = 0f;
                    _sparkles.Clear();
                }

                // Update sparkles
                foreach (var sparkle in _sparkles)
                {
                    if (elapsed < sparkle.SpawnDelay) continue;

                    int sparkleElapsed = elapsed - sparkle.SpawnDelay;
                    float lifeProgress = (float)sparkleElapsed / sparkle.LifeTime;

                    if (lifeProgress > 1f)
                    {
                        sparkle.Alpha = 0f;
                        continue;
                    }

                    // Fade in/out
                    if (lifeProgress < 0.2f)
                        sparkle.Alpha = lifeProgress / 0.2f;
                    else if (lifeProgress > 0.8f)
                        sparkle.Alpha = (1f - lifeProgress) / 0.2f;
                    else
                        sparkle.Alpha = 1f;

                    // Move
                    sparkle.X += sparkle.Velocity.X * deltaSeconds;
                    sparkle.Y += sparkle.Velocity.Y * deltaSeconds;
                }
            }

            // Update dialog
            if (_currentDialog != null)
            {
                if (currentTimeMs - _currentDialog.StartTime > _currentDialog.Duration)
                {
                    _currentDialog = null;
                    if (_dialogQueue.Count > 0)
                    {
                        _currentDialog = _dialogQueue.Dequeue();
                        _currentDialog.StartTime = currentTimeMs;
                    }
                }
            }
        }
        #endregion

        #region Draw
        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int tickCount,
            Texture2D pixelTexture, SpriteFont font)
        {
            if (!_isActive || pixelTexture == null) return;

            // Draw bless effect sparkles
            if (_blessEffectActive && _blessEffectAlpha > 0)
            {
                int screenWidth = spriteBatch.GraphicsDevice.Viewport.Width;
                int screenHeight = spriteBatch.GraphicsDevice.Viewport.Height;
                Vector2 blessCenter = GetBlessEffectScreenCenter(mapShiftX, mapShiftY, centerX, centerY, screenWidth, screenHeight);

                foreach (var sparkle in _sparkles)
                {
                    if (sparkle.Alpha <= 0) continue;

                    int x = (int)(blessCenter.X + sparkle.X);
                    int y = (int)(blessCenter.Y + sparkle.Y);
                    int size = (int)(8 * sparkle.Scale);
                    byte alpha = (byte)(sparkle.Alpha * _blessEffectAlpha * 255);

                    // Draw sparkle (cross pattern)
                    Color sparkleColor = new Color((byte)255, (byte)255, (byte)200, alpha);
                    spriteBatch.Draw(pixelTexture, new Rectangle(x - size / 2, y - 1, size, 2), sparkleColor);
                    spriteBatch.Draw(pixelTexture, new Rectangle(x - 1, y - size / 2, 2, size), sparkleColor);
                }
            }

            // Draw current dialog
            if (_currentDialog != null && font != null)
            {
                DrawDialog(spriteBatch, pixelTexture, font, tickCount);
            }

            // Debug info
            if (font != null)
            {
                string info = $"Wedding: Step {_currentStep} | Role: {LocalParticipantRole} | Bless: {_blessEffectActive}";
                spriteBatch.DrawString(font, info, new Vector2(10, 10), Color.Pink);
            }
        }

        private void DrawDialog(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int currentTimeMs)
        {
            if (_currentDialog == null) return;

            int screenWidth = spriteBatch.GraphicsDevice.Viewport.Width;
            Vector2 textSize = font.MeasureString(_currentDialog.Message);
            int boxWidth = (int)textSize.X + 40;
            int boxHeight = (int)textSize.Y + 20;
            int boxX = (screenWidth - boxWidth) / 2;
            int boxY = 100;

            // Calculate alpha based on time
            int elapsed = currentTimeMs - _currentDialog.StartTime;
            float alpha = 1f;
            if (elapsed < 300)
                alpha = elapsed / 300f;
            else if (elapsed > _currentDialog.Duration - 300)
                alpha = (_currentDialog.Duration - elapsed) / 300f;

            Color bgColor = new Color(50, 50, 50, (int)(200 * alpha));
            Color textColor = new Color(255, 255, 255, (int)(255 * alpha));

            // Draw box
            spriteBatch.Draw(pixelTexture, new Rectangle(boxX, boxY, boxWidth, boxHeight), bgColor);

            // Draw text
            spriteBatch.DrawString(font, _currentDialog.Message, new Vector2(boxX + 20, boxY + 10), textColor);

            if (_currentDialog.Mode == WeddingDialogMode.YesNo)
            {
                Vector2 promptPosition = new Vector2(boxX + 20, boxY + boxHeight - 28);
                spriteBatch.DrawString(font, "[Yes]     [No]", promptPosition, new Color(255, 235, 180, (int)(255 * alpha)));
            }
        }
        #endregion

        #region Reset
        public void Reset()
        {
            _isActive = false;
            _currentStep = 0;
            _blessEffectActive = false;
            _sparkles.Clear();
            _currentDialog = null;
            _dialogQueue.Clear();
            _groomId = 0;
            _brideId = 0;
            _groomPosition = null;
            _bridePosition = null;
            LocalParticipantRole = WeddingParticipantRole.Guest;
        }
        #endregion
    }

    public enum WeddingParticipantRole
    {
        Guest,
        Groom,
        Bride
    }

    public enum WeddingDialogMode
    {
        Text,
        YesNo
    }

    public class WeddingSparkle
    {
        public float X, Y;
        public float Scale;
        public float Alpha;
        public Vector2 Velocity;
        public int LifeTime;
        public int SpawnDelay;
    }

    public class WeddingDialog
    {
        public string Message;
        public int NpcId;
        public int StartTime;
        public int Duration;
        public WeddingDialogMode Mode;
    }
    #endregion

    #region Witchtower Field (CField_Witchtower)
    /// <summary>
    /// Witchtower Field - Score tracking for witchtower event.
    ///
    /// Client evidence:
    /// - CField_Witchtower::OnScoreUpdate (0x564ad0): lazy-creates a scoreboard window, stores a Decode1 score, invalidates it
    /// - CScoreboard_Witchtower::OnCreate (0x564e50): loads dedicated background, key, and score-font assets
    /// - CScoreboard_Witchtower::Draw (0x564bd0): draws a 115x36 center-top widget, overlays the key art at (7, 0),
    ///   then renders a zero-padded score at (67, 4)
    /// </summary>
    public class WitchtowerField
    {
        #region State
        private bool _isActive = false;
        private int _score = 0;
        #endregion

        #region Scoreboard Position (from client: CWnd::CreateWnd position)
        private const int SCOREBOARD_OFFSET_X = -57;
        private const int SCOREBOARD_Y = 92;
        private const int SCOREBOARD_WIDTH = 115;
        private const int SCOREBOARD_HEIGHT = 36;
        #endregion

        #region Focus Pulse
        private const int SCORE_PULSE_DURATION_MS = 650;
        private int _lastScoreUpdateTime = int.MinValue;
        #endregion

        #region Public Properties
        public bool IsActive => _isActive;
        public int Score => _score;
        #endregion

        #region Initialization
        public void Initialize(GraphicsDevice device)
        {
        }

        public void Enable()
        {
            _isActive = true;
            _score = 0;
            _lastScoreUpdateTime = int.MinValue;
        }
        #endregion

        #region Packet Handling

        /// <summary>
        /// OnScoreUpdate - Packet 358
        /// From client: this->m_pScoreboard.p->m_nScore = Decode1(iPacket);
        /// </summary>
        public void OnScoreUpdate(int newScore, int currentTimeMs)
        {
            int clampedScore = Math.Clamp(newScore, 0, byte.MaxValue);
            System.Diagnostics.Debug.WriteLine($"[WitchtowerField] OnScoreUpdate: {_score} -> {clampedScore}");
            _score = clampedScore;
            _lastScoreUpdateTime = currentTimeMs;
        }
        #endregion

        #region Update
        public void Update(int currentTimeMs, float deltaSeconds)
        {
            if (!_isActive) return;
        }
        #endregion

        #region Draw
        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (!_isActive || pixelTexture == null) return;

            Rectangle widgetBounds = GetScoreboardBounds(spriteBatch.GraphicsDevice.Viewport);
            Rectangle innerBounds = new Rectangle(widgetBounds.X + 1, widgetBounds.Y + 1, widgetBounds.Width - 2, widgetBounds.Height - 2);
            Rectangle panelBounds = new Rectangle(widgetBounds.X + 24, widgetBounds.Y + 4, 85, 28);
            Rectangle keyBounds = new Rectangle(widgetBounds.X + 7, widgetBounds.Y, 22, 22);

            float pulseStrength = GetPulseStrength();
            Color outerBorder = Color.Lerp(new Color(88, 71, 44), new Color(240, 218, 120), pulseStrength);
            Color innerFill = Color.Lerp(new Color(56, 41, 18, 228), new Color(104, 78, 30, 244), pulseStrength * 0.45f);
            Color panelFill = Color.Lerp(new Color(28, 24, 18, 220), new Color(77, 54, 18, 236), pulseStrength * 0.30f);
            Color panelHighlight = Color.Lerp(new Color(134, 109, 61), new Color(255, 234, 154), pulseStrength);
            Color keyColor = Color.Lerp(new Color(197, 168, 93), new Color(255, 234, 148), pulseStrength);
            Color pulseOverlay = new Color(255, 234, 154) * (pulseStrength * 0.28f);

            spriteBatch.Draw(pixelTexture, widgetBounds, outerBorder);
            spriteBatch.Draw(pixelTexture, innerBounds, innerFill);
            spriteBatch.Draw(pixelTexture, panelBounds, panelFill);
            spriteBatch.Draw(pixelTexture, new Rectangle(panelBounds.X, panelBounds.Y, panelBounds.Width, 1), panelHighlight);
            spriteBatch.Draw(pixelTexture, new Rectangle(panelBounds.X, panelBounds.Bottom - 1, panelBounds.Width, 1), new Color(38, 28, 16, 255));
            spriteBatch.Draw(pixelTexture, new Rectangle(panelBounds.X, panelBounds.Y, 1, panelBounds.Height), panelHighlight * 0.75f);
            spriteBatch.Draw(pixelTexture, new Rectangle(panelBounds.Right - 1, panelBounds.Y, 1, panelBounds.Height), new Color(25, 19, 12, 255));

            DrawKeyGlyph(spriteBatch, pixelTexture, keyBounds, keyColor);

            if (pulseStrength > 0f)
            {
                spriteBatch.Draw(pixelTexture, innerBounds, pulseOverlay);
            }

            if (font != null)
            {
                string scoreText = _score.ToString("00");
                Vector2 textPos = new Vector2(widgetBounds.X + 67, widgetBounds.Y + 4);
                spriteBatch.DrawString(font, scoreText, textPos + new Vector2(1, 1), Color.Black);
                spriteBatch.DrawString(font, scoreText, textPos, Color.White);
            }
        }
        #endregion

        #region Reset
        public void Reset()
        {
            _isActive = false;
            _score = 0;
            _lastScoreUpdateTime = int.MinValue;
        }
        #endregion

        #region Debug Helpers
        public string DescribeStatus()
        {
            if (!_isActive)
            {
                return "Witchtower scoreboard inactive";
            }

            return $"Witchtower scoreboard active, score={_score:00}";
        }
        #endregion

        #region Private Helpers
        private static Rectangle GetScoreboardBounds(Viewport viewport)
        {
            int x = viewport.Width / 2 + SCOREBOARD_OFFSET_X;
            return new Rectangle(x, SCOREBOARD_Y, SCOREBOARD_WIDTH, SCOREBOARD_HEIGHT);
        }

        private float GetPulseStrength()
        {
            if (_lastScoreUpdateTime == int.MinValue)
            {
                return 0f;
            }

            int elapsed = Environment.TickCount - _lastScoreUpdateTime;
            if (elapsed < 0 || elapsed >= SCORE_PULSE_DURATION_MS)
            {
                return 0f;
            }

            float normalized = 1f - (elapsed / (float)SCORE_PULSE_DURATION_MS);
            return normalized * normalized;
        }

        private static void DrawKeyGlyph(SpriteBatch spriteBatch, Texture2D pixelTexture, Rectangle keyBounds, Color keyColor)
        {
            spriteBatch.Draw(pixelTexture, new Rectangle(keyBounds.X + 4, keyBounds.Y + 3, 10, 10), keyColor);
            spriteBatch.Draw(pixelTexture, new Rectangle(keyBounds.X + 6, keyBounds.Y + 5, 6, 6), new Color(72, 52, 24, 255));
            spriteBatch.Draw(pixelTexture, new Rectangle(keyBounds.X + 12, keyBounds.Y + 7, 8, 4), keyColor);
            spriteBatch.Draw(pixelTexture, new Rectangle(keyBounds.X + 17, keyBounds.Y + 7, 2, 7), keyColor);
            spriteBatch.Draw(pixelTexture, new Rectangle(keyBounds.X + 14, keyBounds.Y + 11, 2, 5), keyColor);
            spriteBatch.Draw(pixelTexture, new Rectangle(keyBounds.X + 18, keyBounds.Y + 11, 2, 3), new Color(116, 88, 42, 255));
        }
        #endregion
    }
    #endregion

    #region Battlefield Field (CField_Battlefield)
    /// <summary>
    /// Battlefield Field - Sheep vs Wolf event scoreboard and timer flow.
    ///
    /// WZ evidence:
    /// - map/Map/Map9/910040100/battleField exposes timeDefault=300, timeFinish=3, rewardMap* and effectWin/effectLose.
    ///
    /// Client evidence:
    /// - CField_Battlefield::OnScoreUpdate (0x5499a0): decodes wolves/sheep bytes and redraws the scoreboard.
    /// - CField_Battlefield::OnClock (0x549ad0): for clock type 2, creates a 258x73 center-top scoreboard window and starts its timer.
    /// </summary>
    public class BattlefieldField
    {
        private const int ScoreboardOffsetX = -107;
        private const int ScoreboardY = 30;
        private const int ScoreboardWidth = 258;
        private const int ScoreboardHeight = 73;
        private const int ScorePulseDurationMs = 800;

        private bool _isActive;
        private int _wolvesScore;
        private int _sheepScore;
        private int _defaultDurationSeconds = 300;
        private int _finishDurationSeconds = 3;
        private int _clockDurationSeconds;
        private int _clockStartTimeMs;
        private int _currentObservedTimeMs;
        private int _lastScoreUpdateTimeMs = int.MinValue;
        private bool _clockVisible;

        public bool IsActive => _isActive;
        public int WolvesScore => _wolvesScore;
        public int SheepScore => _sheepScore;
        public int DefaultDurationSeconds => _defaultDurationSeconds;
        public int FinishDurationSeconds => _finishDurationSeconds;
        public int RemainingSeconds => !_clockVisible
            ? _defaultDurationSeconds
            : Math.Max(0, _clockDurationSeconds - Math.Max(0, _currentObservedTimeMs - _clockStartTimeMs) / 1000);

        public string EffectWinPath { get; private set; }
        public string EffectLosePath { get; private set; }
        public int RewardMapWinWolf { get; private set; }
        public int RewardMapWinSheep { get; private set; }
        public int RewardMapLoseWolf { get; private set; }
        public int RewardMapLoseSheep { get; private set; }

        public void Initialize(GraphicsDevice device)
        {
        }

        public void Enable()
        {
            _isActive = true;
            _wolvesScore = 0;
            _sheepScore = 0;
            _clockVisible = false;
            _clockDurationSeconds = 0;
            _clockStartTimeMs = 0;
            _currentObservedTimeMs = 0;
            _lastScoreUpdateTimeMs = int.MinValue;
            _defaultDurationSeconds = 300;
            _finishDurationSeconds = 3;
            EffectWinPath = null;
            EffectLosePath = null;
            RewardMapWinWolf = 0;
            RewardMapWinSheep = 0;
            RewardMapLoseWolf = 0;
            RewardMapLoseSheep = 0;
        }

        public void Configure(MapInfo mapInfo)
        {
            if (!_isActive || mapInfo == null)
            {
                return;
            }

            for (int i = 0; i < mapInfo.additionalNonInfoProps.Count; i++)
            {
                if (mapInfo.additionalNonInfoProps[i] is not WzSubProperty battleField
                    || !string.Equals(battleField.Name, "battleField", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _defaultDurationSeconds = Math.Max(1, InfoTool.GetOptionalInt(battleField["timeDefault"]) ?? _defaultDurationSeconds);
                _finishDurationSeconds = Math.Max(0, InfoTool.GetOptionalInt(battleField["timeFinish"]) ?? _finishDurationSeconds);
                EffectWinPath = InfoTool.GetOptionalString(battleField["effectWin"]);
                EffectLosePath = InfoTool.GetOptionalString(battleField["effectLose"]);
                RewardMapWinWolf = InfoTool.GetOptionalInt(battleField["rewardMapWinWolf"]) ?? RewardMapWinWolf;
                RewardMapWinSheep = InfoTool.GetOptionalInt(battleField["rewardMapWinSheep"]) ?? RewardMapWinSheep;
                RewardMapLoseWolf = InfoTool.GetOptionalInt(battleField["rewardMapLoseWolf"]) ?? RewardMapLoseWolf;
                RewardMapLoseSheep = InfoTool.GetOptionalInt(battleField["rewardMapLoseSheep"]) ?? RewardMapLoseSheep;
                return;
            }
        }

        public void OnScoreUpdate(int wolves, int sheep, int currentTimeMs)
        {
            if (!_isActive)
            {
                return;
            }

            int clampedWolves = Math.Clamp(wolves, 0, byte.MaxValue);
            int clampedSheep = Math.Clamp(sheep, 0, byte.MaxValue);
            System.Diagnostics.Debug.WriteLine($"[BattlefieldField] OnScoreUpdate: wolves {_wolvesScore} -> {clampedWolves}, sheep {_sheepScore} -> {clampedSheep}");

            _wolvesScore = clampedWolves;
            _sheepScore = clampedSheep;
            _lastScoreUpdateTimeMs = currentTimeMs;
        }

        public void OnClock(int clockType, int remainingSeconds, int currentTimeMs)
        {
            if (!_isActive || clockType != 2)
            {
                return;
            }

            _clockVisible = true;
            _clockDurationSeconds = Math.Max(0, remainingSeconds);
            _clockStartTimeMs = currentTimeMs;
            _currentObservedTimeMs = currentTimeMs;
            System.Diagnostics.Debug.WriteLine($"[BattlefieldField] OnClock: type={clockType}, seconds={remainingSeconds}");
        }

        public void StartDefaultClock(int currentTimeMs)
        {
            OnClock(2, _defaultDurationSeconds, currentTimeMs);
        }

        public void Update(int currentTimeMs, float deltaSeconds)
        {
            if (!_isActive)
            {
                return;
            }

            _currentObservedTimeMs = currentTimeMs;
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (!_isActive || !_clockVisible || pixelTexture == null)
            {
                return;
            }

            Rectangle bounds = GetScoreboardBounds(spriteBatch.GraphicsDevice.Viewport);
            Rectangle inner = new Rectangle(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4);
            Rectangle wolvesPanel = new Rectangle(bounds.X + 10, bounds.Y + 28, 105, 28);
            Rectangle sheepPanel = new Rectangle(bounds.Right - 115, bounds.Y + 28, 105, 28);
            Rectangle timerPanel = new Rectangle(bounds.X + 92, bounds.Y + 9, 74, 18);

            float pulse = GetScorePulseStrength();
            Color frame = Color.Lerp(new Color(66, 56, 34), new Color(242, 229, 168), pulse);
            Color fill = new Color(28, 25, 21, 226);
            Color wolvesColor = Color.Lerp(new Color(110, 74, 31), new Color(212, 147, 62), pulse * 0.5f);
            Color sheepColor = Color.Lerp(new Color(52, 76, 97), new Color(118, 181, 222), pulse * 0.5f);

            spriteBatch.Draw(pixelTexture, bounds, frame);
            spriteBatch.Draw(pixelTexture, inner, fill);
            spriteBatch.Draw(pixelTexture, timerPanel, new Color(14, 14, 14, 220));
            spriteBatch.Draw(pixelTexture, wolvesPanel, wolvesColor);
            spriteBatch.Draw(pixelTexture, sheepPanel, sheepColor);
            spriteBatch.Draw(pixelTexture, new Rectangle(bounds.X + 128, bounds.Y + 24, 2, 36), new Color(90, 85, 73, 220));

            if (font != null)
            {
                string timerText = FormatTimer(RemainingSeconds);
                string wolvesScore = _wolvesScore.ToString("D2");
                string sheepScore = _sheepScore.ToString("D2");
                string statusText = GetLeadingTeamLabel();

                spriteBatch.DrawString(font, timerText, new Vector2(timerPanel.X + 9, timerPanel.Y + 1), Color.White);
                spriteBatch.DrawString(font, "Wolves", new Vector2(wolvesPanel.X + 8, wolvesPanel.Y + 2), Color.White);
                spriteBatch.DrawString(font, wolvesScore, new Vector2(wolvesPanel.Right - 26, wolvesPanel.Y + 2), Color.White);
                spriteBatch.DrawString(font, "Sheep", new Vector2(sheepPanel.X + 10, sheepPanel.Y + 2), Color.White);
                spriteBatch.DrawString(font, sheepScore, new Vector2(sheepPanel.Right - 26, sheepPanel.Y + 2), Color.White);
                spriteBatch.DrawString(font, statusText, new Vector2(bounds.X + 84, bounds.Bottom - 17), new Color(225, 213, 161, 255));
            }
        }

        public string DescribeStatus()
        {
            if (!_isActive)
            {
                return "Battlefield runtime inactive";
            }

            string clockText = _clockVisible
                ? $"timer={FormatTimer(RemainingSeconds)}"
                : $"timer=idle(default {FormatTimer(_defaultDurationSeconds)})";
            return $"Battlefield active, wolves={_wolvesScore:D2}, sheep={_sheepScore:D2}, {clockText}";
        }

        public void Reset()
        {
            _isActive = false;
            _wolvesScore = 0;
            _sheepScore = 0;
            _clockVisible = false;
            _clockDurationSeconds = 0;
            _clockStartTimeMs = 0;
            _currentObservedTimeMs = 0;
            _lastScoreUpdateTimeMs = int.MinValue;
            _defaultDurationSeconds = 300;
            _finishDurationSeconds = 3;
            EffectWinPath = null;
            EffectLosePath = null;
            RewardMapWinWolf = 0;
            RewardMapWinSheep = 0;
            RewardMapLoseWolf = 0;
            RewardMapLoseSheep = 0;
        }

        private static Rectangle GetScoreboardBounds(Viewport viewport)
        {
            int x = viewport.Width / 2 + ScoreboardOffsetX;
            return new Rectangle(x, ScoreboardY, ScoreboardWidth, ScoreboardHeight);
        }

        private float GetScorePulseStrength()
        {
            if (_lastScoreUpdateTimeMs == int.MinValue)
            {
                return 0f;
            }

            int elapsed = _currentObservedTimeMs - _lastScoreUpdateTimeMs;
            if (elapsed < 0 || elapsed >= ScorePulseDurationMs)
            {
                return 0f;
            }

            float normalized = 1f - (elapsed / (float)ScorePulseDurationMs);
            return normalized * normalized;
        }

        private string GetLeadingTeamLabel()
        {
            if (_wolvesScore == _sheepScore)
            {
                return "Draw";
            }

            return _wolvesScore > _sheepScore ? "Wolves lead" : "Sheep lead";
        }

        private static string FormatTimer(int totalSeconds)
        {
            int safeSeconds = Math.Max(0, totalSeconds);
            return $"{safeSeconds / 60}:{safeSeconds % 60:D2}";
        }
    }
    #endregion

    #region GuildBoss Field (CField_GuildBoss)
    /// <summary>
    /// GuildBoss Field - Healer and pulley mechanics for guild boss fights.
    ///
    /// - OnHealerMove (packet 344): Move healer NPC vertically
    /// - OnPulleyStateChange (packet 345): Change pulley interaction state
    /// - CHealer: Animated healing NPC that moves on Y axis
    /// - CPulley: Interactive area for pulley mechanic (rope-based)
    /// </summary>
    public class GuildBossField
    {
        #region State
        private bool _isActive = false;
        private int _pulleyState = 0; // 0 = idle, 1 = activating, 2 = active
        #endregion

        #region Healer (from CHealer class)
        private bool _healerEnabled = false;
        private float _healerX;
        private float _healerY;
        private float _healerTargetY;
        private float _healerMoveSpeed = 100f;
        private List<IDXObject> _healerFrames;
        private int _healerFrameIndex = 0;
        private int _lastHealerFrameTime = 0;
        #endregion

        #region Pulley (from CPulley class)
        private bool _pulleyEnabled = false;
        private Rectangle _pulleyArea; // From CPulley::Init: (x-186, y+90, x-60, y+184)
        private float _pulleyX;
        private float _pulleyY;
        private List<IDXObject> _pulleyFrames;
        #endregion

        #region Heal Effect
        private bool _healEffectActive = false;
        private float _healEffectAlpha = 0f;
        private int _healEffectStartTime;
        private List<HealParticle> _healParticles = new();
        private Random _random = new();
        #endregion

        #region Public Properties
        public bool IsActive => _isActive;
        public int PulleyState => _pulleyState;
        public float HealerY => _healerY;
        public bool IsHealEffectActive => _healEffectActive;
        #endregion

        #region Initialization
        public void Initialize(GraphicsDevice device)
        {
        }

        public void Enable()
        {
            _isActive = true;
            _pulleyState = 0;
        }

        /// <summary>
        /// Initialize healer from map properties
        /// From CField_GuildBoss::Init / CHealer::Init
        /// </summary>
        public void InitHealer(int x, int yMin, string healerPath)
        {
            _healerEnabled = true;
            _healerX = x;
            _healerY = yMin;
            _healerTargetY = yMin;
            System.Diagnostics.Debug.WriteLine($"[GuildBossField] Healer initialized at ({x}, {yMin}), path: {healerPath}");
        }

        /// <summary>
        /// Initialize pulley from map properties
        /// From CField_GuildBoss::Init / CPulley::Init
        /// </summary>
        public void InitPulley(int x, int y, string pulleyPath)
        {
            _pulleyEnabled = true;
            _pulleyX = x;
            _pulleyY = y;
            // Pulley area from client: (x-186, y+90) to (x-60, y+184)
            _pulleyArea = new Rectangle(x - 186, y + 90, 126, 94);
            System.Diagnostics.Debug.WriteLine($"[GuildBossField] Pulley initialized at ({x}, {y}), area: {_pulleyArea}");
        }

        public void SetHealerFrames(List<IDXObject> frames)
        {
            _healerFrames = frames;
        }

        public void SetPulleyFrames(List<IDXObject> frames)
        {
            _pulleyFrames = frames;
        }
        #endregion

        #region Packet Handling

        /// <summary>
        /// OnHealerMove - Packet 344
        /// From client: CHealer::Move(&this->m_healer, v3 - this->m_nY)
        ///              this->m_nY = v3
        /// </summary>
        public void OnHealerMove(int newY, int currentTimeMs)
        {
            if (!_healerEnabled) return;

            System.Diagnostics.Debug.WriteLine($"[GuildBossField] OnHealerMove: {_healerY} -> {newY}");
            _healerTargetY = newY;

            // Trigger heal effect when healer moves up
            if (newY < _healerY)
            {
                TriggerHealEffect(currentTimeMs);
            }
        }

        /// <summary>
        /// OnPulleyStateChange - Packet 345
        /// From client: this->m_nState = Decode1(iPacket)
        /// </summary>
        public void OnPulleyStateChange(int newState, int currentTimeMs)
        {
            System.Diagnostics.Debug.WriteLine($"[GuildBossField] OnPulleyStateChange: {_pulleyState} -> {newState}");
            _pulleyState = newState;
        }

        private void TriggerHealEffect(int currentTimeMs)
        {
            _healEffectActive = true;
            _healEffectStartTime = currentTimeMs;
            _healEffectAlpha = 1f;

            // Create heal particles rising from healer
            _healParticles.Clear();
            for (int i = 0; i < 20; i++)
            {
                _healParticles.Add(new HealParticle
                {
                    X = _healerX + (float)(_random.NextDouble() - 0.5) * 100,
                    Y = _healerY,
                    VelocityY = -50f - (float)_random.NextDouble() * 50f,
                    VelocityX = (float)(_random.NextDouble() - 0.5) * 20f,
                    Alpha = 1f,
                    LifeTime = 1000 + _random.Next(1000),
                    SpawnDelay = i * 50
                });
            }
        }
        #endregion

        #region Update
        public void Update(int currentTimeMs, float deltaSeconds)
        {
            if (!_isActive) return;

            // Move healer towards target
            if (_healerEnabled)
            {
                float diff = _healerTargetY - _healerY;
                if (Math.Abs(diff) > 0.5f)
                {
                    float move = Math.Sign(diff) * Math.Min(Math.Abs(diff), _healerMoveSpeed * deltaSeconds);
                    _healerY += move;
                }
            }

            // Update heal effect
            if (_healEffectActive)
            {
                int elapsed = currentTimeMs - _healEffectStartTime;
                bool anyAlive = false;

                foreach (var particle in _healParticles)
                {
                    if (elapsed < particle.SpawnDelay) continue;

                    int particleElapsed = elapsed - particle.SpawnDelay;
                    float lifeProgress = (float)particleElapsed / particle.LifeTime;

                    if (lifeProgress > 1f)
                    {
                        particle.Alpha = 0f;
                        continue;
                    }

                    anyAlive = true;
                    particle.Alpha = 1f - lifeProgress;
                    particle.Y += particle.VelocityY * deltaSeconds;
                    particle.X += particle.VelocityX * deltaSeconds;
                }

                if (!anyAlive && elapsed > 100)
                {
                    _healEffectActive = false;
                    _healParticles.Clear();
                }
            }
        }
        #endregion

        #region Draw
        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int tickCount,
            Texture2D pixelTexture, SpriteFont font)
        {
            if (!_isActive || pixelTexture == null) return;

            int shiftCenterX = mapShiftX - centerX;
            int shiftCenterY = mapShiftY - centerY;

            // Draw healer
            if (_healerEnabled)
            {
                int healerScreenX = (int)_healerX - shiftCenterX;
                int healerScreenY = (int)_healerY - shiftCenterY;

                // Debug rectangle for healer
                spriteBatch.Draw(pixelTexture, new Rectangle(healerScreenX - 20, healerScreenY - 40, 40, 60),
                    new Color(100, 200, 100, 150));

                if (font != null)
                {
                    spriteBatch.DrawString(font, "Healer", new Vector2(healerScreenX - 20, healerScreenY - 55), Color.LightGreen);
                }
            }

            // Draw pulley
            if (_pulleyEnabled)
            {
                int pulleyScreenX = (int)_pulleyX - shiftCenterX;
                int pulleyScreenY = (int)_pulleyY - shiftCenterY;

                // Draw pulley area
                Rectangle screenArea = new Rectangle(
                    _pulleyArea.X - shiftCenterX,
                    _pulleyArea.Y - shiftCenterY,
                    _pulleyArea.Width,
                    _pulleyArea.Height);

                Color pulleyColor = _pulleyState switch
                {
                    0 => new Color(100, 100, 200, 100),
                    1 => new Color(200, 200, 100, 150),
                    2 => new Color(100, 200, 100, 150),
                    _ => new Color(100, 100, 100, 100)
                };

                spriteBatch.Draw(pixelTexture, screenArea, pulleyColor);

                if (font != null)
                {
                    string stateText = _pulleyState switch
                    {
                        0 => "Idle",
                        1 => "Activating",
                        2 => "Active",
                        _ => $"State {_pulleyState}"
                    };
                    spriteBatch.DrawString(font, $"Pulley: {stateText}",
                        new Vector2(screenArea.X, screenArea.Y - 15), Color.LightBlue);
                }
            }

            // Draw heal particles
            if (_healEffectActive)
            {
                foreach (var particle in _healParticles)
                {
                    if (particle.Alpha <= 0) continue;

                    int px = (int)particle.X - shiftCenterX;
                    int py = (int)particle.Y - shiftCenterY;
                    byte alpha = (byte)(particle.Alpha * 255);

                    spriteBatch.Draw(pixelTexture, new Rectangle(px - 3, py - 3, 6, 6),
                        new Color((byte)100, (byte)255, (byte)100, alpha));
                }
            }

            // Debug info
            if (font != null)
            {
                string info = $"GuildBoss: Pulley={_pulleyState} | Healer Y={_healerY:F0}";
                spriteBatch.DrawString(font, info, new Vector2(10, 60), Color.LightBlue);
            }
        }
        #endregion

        #region Reset
        public void Reset()
        {
            _isActive = false;
            _pulleyState = 0;
            _healerEnabled = false;
            _pulleyEnabled = false;
            _healEffectActive = false;
            _healParticles.Clear();
        }
        #endregion
    }

    public class HealParticle
    {
        public float X, Y;
        public float VelocityX, VelocityY;
        public float Alpha;
        public int LifeTime;
        public int SpawnDelay;
    }
    #endregion

    #region Dojo Field (CField_Dojang)
    /// <summary>
    /// Mu Lung Dojo field HUD.
    ///
    /// Client evidence:
    /// - CField_Dojang::OnClock (0x550940) only reacts to clock type 2, stores a duration in seconds,
    ///   creates a dedicated timer layer, and refreshes the timer immediately.
    /// - CField_Dojang::Update (0x54ef10) continuously mirrors boss HP, player HP, and energy into
    ///   dedicated HUD layers before calling UpdateTimer again.
    ///
    /// This simulator pass mirrors the Dojo-specific timer plus the three gauge surfaces behind a
    /// stable runtime seam. Stage effects and packet-driven result flow still need follow-up work.
    /// </summary>
    public class DojoField
    {
        private const int HudWidth = 332;
        private const int GaugeWidth = 305;
        private const int GaugeHeight = 10;
        private const int HudOffsetX = -166;
        private const int HudY = 34;
        private const int GaugeStartX = 14;
        private const int BossGaugeY = 46;
        private const int PlayerGaugeY = 82;
        private const int EnergyGaugeY = 118;
        private const int EnergyMax = 10000;
        private const int BannerDurationMs = 1800;
        private const int TimeOverOverlayDurationMs = 2200;

        private bool _isActive;
        private int _mapId;
        private int _stage;
        private int _timerDurationSec;
        private int _timeOverTick = int.MinValue;
        private int _lastClockUpdateTick = int.MinValue;
        private int _playerHp;
        private int _playerMaxHp = 100;
        private float? _bossHpPercent;
        private int _energy;
        private int _stageBannerStartTick = int.MinValue;
        private int _timeOverOverlayStartTick = int.MinValue;

        public bool IsActive => _isActive;
        public int Stage => _stage;
        public int Energy => _energy;
        public int RemainingSeconds
        {
            get
            {
                if (_timeOverTick == int.MinValue)
                {
                    return 0;
                }

                int remainingMs = _timeOverTick - Environment.TickCount;
                if (remainingMs <= 0)
                {
                    return 0;
                }

                return (remainingMs + 999) / 1000;
            }
        }

        public void Initialize(GraphicsDevice device)
        {
        }

        public void Enable(int mapId)
        {
            _isActive = true;
            _mapId = mapId;
            _stage = ResolveStage(mapId);
            _timerDurationSec = 0;
            _timeOverTick = int.MinValue;
            _lastClockUpdateTick = int.MinValue;
            _playerHp = 0;
            _playerMaxHp = 100;
            _bossHpPercent = null;
            _energy = 0;
            _stageBannerStartTick = Environment.TickCount;
            _timeOverOverlayStartTick = int.MinValue;
        }

        public void OnClock(int clockType, int durationSec, int currentTimeMs)
        {
            if (clockType != 2)
            {
                return;
            }

            _timerDurationSec = Math.Max(0, durationSec);
            _timeOverTick = currentTimeMs + (_timerDurationSec * 1000);
            _lastClockUpdateTick = currentTimeMs;
            _timeOverOverlayStartTick = int.MinValue;
        }

        public void SetRuntimeState(int? playerHp, int? playerMaxHp, float? bossHpPercent)
        {
            if (playerMaxHp.HasValue && playerMaxHp.Value > 0)
            {
                _playerMaxHp = playerMaxHp.Value;
            }

            if (playerHp.HasValue)
            {
                _playerHp = Math.Clamp(playerHp.Value, 0, _playerMaxHp);
            }

            if (bossHpPercent.HasValue)
            {
                _bossHpPercent = Math.Clamp(bossHpPercent.Value, 0f, 1f);
            }
            else
            {
                _bossHpPercent = null;
            }
        }

        public void SetEnergy(int energy)
        {
            _energy = Math.Clamp(energy, 0, EnergyMax);
        }

        public void SetStage(int stage, int currentTimeMs)
        {
            _stage = Math.Clamp(stage, 0, 32);
            _stageBannerStartTick = currentTimeMs;
        }

        public void Update(int currentTimeMs, float deltaSeconds)
        {
            if (!_isActive)
            {
                return;
            }

            if (_timeOverTick != int.MinValue && currentTimeMs >= _timeOverTick && _timeOverOverlayStartTick == int.MinValue)
            {
                _timeOverOverlayStartTick = currentTimeMs;
            }
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int currentTimeMs)
        {
            if (!_isActive || pixelTexture == null)
            {
                return;
            }

            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            Rectangle hudBounds = new(viewport.Width / 2 + HudOffsetX, HudY, HudWidth, 144);

            spriteBatch.Draw(pixelTexture, hudBounds, new Color(22, 18, 12, 214));
            spriteBatch.Draw(pixelTexture, new Rectangle(hudBounds.X, hudBounds.Y, hudBounds.Width, 2), new Color(126, 95, 45, 255));
            spriteBatch.Draw(pixelTexture, new Rectangle(hudBounds.X, hudBounds.Bottom - 2, hudBounds.Width, 2), new Color(48, 34, 16, 255));

            DrawGaugeRow(spriteBatch, pixelTexture, font, hudBounds, GaugeStartX, BossGaugeY, "Boss", _bossHpPercent ?? 0f, new Color(148, 44, 32, 255), _bossHpPercent.HasValue ? $"{(int)MathF.Round(_bossHpPercent.Value * 100f)}%" : "--");
            DrawGaugeRow(spriteBatch, pixelTexture, font, hudBounds, GaugeStartX, PlayerGaugeY, "HP", _playerMaxHp > 0 ? (float)_playerHp / _playerMaxHp : 0f, new Color(42, 137, 63, 255), $"{_playerHp}/{_playerMaxHp}");
            DrawGaugeRow(spriteBatch, pixelTexture, font, hudBounds, GaugeStartX, EnergyGaugeY, "Energy", (float)_energy / EnergyMax, new Color(232, 185, 49, 255), $"{_energy / 100f:0}%");

            if (font != null)
            {
                string timerText = FormatTimer(RemainingSeconds);
                Vector2 timerSize = font.MeasureString(timerText);
                Vector2 timerPos = new(hudBounds.Center.X - (timerSize.X / 2f), hudBounds.Y + 12);
                spriteBatch.DrawString(font, timerText, timerPos + Vector2.One, Color.Black);
                spriteBatch.DrawString(font, timerText, timerPos, Color.White);

                string stageText = _stage >= 0 ? $"Mu Lung Dojo Floor {_stage}" : "Mu Lung Dojo";
                spriteBatch.DrawString(font, stageText, new Vector2(hudBounds.X + 12, hudBounds.Y + 12), new Color(235, 212, 149));
            }

            DrawStageBanner(spriteBatch, pixelTexture, font, viewport, currentTimeMs);
            DrawTimeOverOverlay(spriteBatch, pixelTexture, font, viewport, currentTimeMs);
        }

        public string DescribeStatus()
        {
            if (!_isActive)
            {
                return "Mu Lung Dojo HUD inactive";
            }

            string bossText = _bossHpPercent.HasValue
                ? $"{(int)MathF.Round(_bossHpPercent.Value * 100f)}%"
                : "--";
            string timerText = _timeOverTick == int.MinValue ? "stopped" : FormatTimer(RemainingSeconds);
            return $"Mu Lung Dojo floor {_stage}, timer={timerText}, boss={bossText}, player={_playerHp}/{_playerMaxHp}, energy={_energy}/{EnergyMax}";
        }

        public void Reset()
        {
            _isActive = false;
            _mapId = 0;
            _stage = -1;
            _timerDurationSec = 0;
            _timeOverTick = int.MinValue;
            _lastClockUpdateTick = int.MinValue;
            _playerHp = 0;
            _playerMaxHp = 100;
            _bossHpPercent = null;
            _energy = 0;
            _stageBannerStartTick = int.MinValue;
            _timeOverOverlayStartTick = int.MinValue;
        }

        private static int ResolveStage(int mapId)
        {
            if (mapId < 925020000 || mapId > 925040999)
            {
                return -1;
            }

            int rawStage = (mapId / 100) % 100;
            return Math.Clamp(rawStage, 0, 32);
        }

        private static string FormatTimer(int remainingSeconds)
        {
            int minutes = Math.Max(0, remainingSeconds) / 60;
            int seconds = Math.Max(0, remainingSeconds) % 60;
            return $"{minutes}:{seconds:00}";
        }

        private static void DrawGaugeRow(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, Rectangle hudBounds, int gaugeXOffset, int gaugeYOffset, string label, float progress, Color fillColor, string valueText)
        {
            Rectangle gaugeBounds = new(hudBounds.X + gaugeXOffset, hudBounds.Y + gaugeYOffset, GaugeWidth, GaugeHeight);
            Rectangle innerBounds = new(gaugeBounds.X + 1, gaugeBounds.Y + 1, gaugeBounds.Width - 2, gaugeBounds.Height - 2);
            int fillWidth = Math.Clamp((int)MathF.Round(innerBounds.Width * Math.Clamp(progress, 0f, 1f)), 0, innerBounds.Width);

            spriteBatch.Draw(pixelTexture, gaugeBounds, new Color(83, 63, 32, 255));
            spriteBatch.Draw(pixelTexture, innerBounds, new Color(18, 18, 18, 255));
            if (fillWidth > 0)
            {
                spriteBatch.Draw(pixelTexture, new Rectangle(innerBounds.X, innerBounds.Y, fillWidth, innerBounds.Height), fillColor);
            }

            if (font != null)
            {
                Vector2 labelPos = new(hudBounds.X + gaugeXOffset, gaugeBounds.Y - 18);
                Vector2 valueSize = font.MeasureString(valueText);
                Vector2 valuePos = new(gaugeBounds.Right - valueSize.X, gaugeBounds.Y - 18);
                spriteBatch.DrawString(font, label, labelPos, new Color(230, 218, 190));
                spriteBatch.DrawString(font, valueText, valuePos, Color.White);
            }
        }

        private void DrawStageBanner(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, Viewport viewport, int currentTimeMs)
        {
            if (_stageBannerStartTick == int.MinValue)
            {
                return;
            }

            int elapsed = currentTimeMs - _stageBannerStartTick;
            if (elapsed < 0 || elapsed >= BannerDurationMs)
            {
                return;
            }

            float alpha = 1f - (elapsed / (float)BannerDurationMs);
            Rectangle bannerBounds = new(viewport.Width / 2 - 150, 200, 300, 42);
            spriteBatch.Draw(pixelTexture, bannerBounds, new Color(36, 25, 10) * (0.86f * alpha));
            spriteBatch.Draw(pixelTexture, new Rectangle(bannerBounds.X, bannerBounds.Y, bannerBounds.Width, 2), new Color(221, 183, 87) * alpha);

            if (font != null)
            {
                string bannerText = _stage >= 0 ? $"Mu Lung Dojo Floor {_stage}" : "Mu Lung Dojo";
                Vector2 size = font.MeasureString(bannerText);
                Vector2 pos = new(bannerBounds.Center.X - (size.X / 2f), bannerBounds.Center.Y - (size.Y / 2f));
                spriteBatch.DrawString(font, bannerText, pos, Color.White * alpha);
            }
        }

        private void DrawTimeOverOverlay(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, Viewport viewport, int currentTimeMs)
        {
            if (_timeOverOverlayStartTick == int.MinValue)
            {
                return;
            }

            int elapsed = currentTimeMs - _timeOverOverlayStartTick;
            if (elapsed < 0 || elapsed >= TimeOverOverlayDurationMs)
            {
                return;
            }

            float alpha = 1f - (elapsed / (float)TimeOverOverlayDurationMs);
            Rectangle panel = new(viewport.Width / 2 - 180, viewport.Height / 2 - 46, 360, 92);
            spriteBatch.Draw(pixelTexture, panel, new Color(15, 10, 8) * (0.82f * alpha));
            spriteBatch.Draw(pixelTexture, new Rectangle(panel.X, panel.Y, panel.Width, 2), new Color(171, 61, 39) * alpha);

            if (font != null)
            {
                const string text = "Time Over";
                Vector2 size = font.MeasureString(text);
                Vector2 pos = new(panel.Center.X - (size.X / 2f), panel.Center.Y - (size.Y / 2f));
                spriteBatch.DrawString(font, text, pos + Vector2.One, Color.Black * alpha);
                spriteBatch.DrawString(font, text, pos, new Color(255, 217, 180) * alpha);
            }
        }
    }
    #endregion

    #region SpaceGAGA Field (CField_SpaceGAGA)
    /// <summary>
    /// SpaceGAGA timerboard HUD.
    ///
    /// WZ evidence:
    /// - Space Gaga maps 922240000, 922240100, and 922240200 all declare fieldType 20
    ///   (FIELDTYPE_SPACEGAGA) in map/Map\Map9.
    ///
    /// Client evidence:
    /// - CField_SpaceGAGA::OnClock (0x5625d0) only reacts to clock type 2, destroys the
    ///   previous clock, creates a 258x69 timerboard at (-114, 30), then sets and starts it.
    /// - CTimerboard_SpaceGAGA::Draw (0x5626c0) renders a dedicated source canvas and draws
    ///   zero-padded minutes and seconds at fixed positions: (44, 23) and (131, 23).
    ///
    /// This simulator pass adds the dedicated timerboard flow and clock ownership seam.
    /// Exact WZ canvas and timer font loading still need follow-up work.
    /// </summary>
    public class SpaceGagaField
    {
        private const int TimerboardWidth = 258;
        private const int TimerboardHeight = 69;
        private const int TimerboardOffsetX = -114;
        private const int TimerboardY = 30;
        private const int MinuteTextX = 44;
        private const int SecondTextX = 131;
        private const int TextY = 23;
        private const int DividerX = 110;
        private const int DividerY = 16;
        private const int DividerWidth = 38;
        private const int DividerHeight = 36;
        private const int ResetPulseDurationMs = 600;

        private bool _isActive;
        private int _mapId;
        private int _durationSec;
        private int _timeOverTick = int.MinValue;
        private int _lastResetTick = int.MinValue;

        public bool IsActive => _isActive;
        public int MapId => _mapId;
        public int DurationSeconds => _durationSec;
        public int RemainingSeconds
        {
            get
            {
                if (_timeOverTick == int.MinValue)
                {
                    return 0;
                }

                int remainingMs = _timeOverTick - Environment.TickCount;
                if (remainingMs <= 0)
                {
                    return 0;
                }

                return (remainingMs + 999) / 1000;
            }
        }

        public void Initialize(GraphicsDevice device)
        {
        }

        public void Enable(int mapId)
        {
            _isActive = true;
            _mapId = mapId;
            _durationSec = 0;
            _timeOverTick = int.MinValue;
            _lastResetTick = int.MinValue;
        }

        public void OnClock(int clockType, int durationSec, int currentTimeMs)
        {
            if (clockType != 2)
            {
                return;
            }

            _durationSec = Math.Max(0, durationSec);
            _timeOverTick = currentTimeMs + (_durationSec * 1000);
            _lastResetTick = currentTimeMs;
        }

        public void Update(int currentTimeMs, float deltaSeconds)
        {
            if (!_isActive)
            {
                return;
            }
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (!_isActive || pixelTexture == null)
            {
                return;
            }

            Rectangle bounds = GetTimerboardBounds(spriteBatch.GraphicsDevice.Viewport);
            Rectangle innerBounds = new(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4);
            Rectangle faceBounds = new(bounds.X + 10, bounds.Y + 10, bounds.Width - 20, bounds.Height - 20);
            Rectangle dividerBounds = new(bounds.X + DividerX, bounds.Y + DividerY, DividerWidth, DividerHeight);

            float pulse = GetResetPulseStrength();
            float urgency = RemainingSeconds <= 10 ? 1f - (RemainingSeconds / 10f) : 0f;

            Color outerBorder = Color.Lerp(new Color(68, 120, 166, 255), new Color(255, 233, 158, 255), pulse * 0.65f);
            Color innerFill = Color.Lerp(new Color(10, 22, 41, 232), new Color(25, 51, 84, 240), pulse * 0.35f);
            Color faceFill = Color.Lerp(new Color(25, 53, 87, 230), new Color(123, 49, 33, 236), urgency * 0.75f);
            Color faceHighlight = Color.Lerp(new Color(122, 193, 227, 255), new Color(255, 209, 122, 255), Math.Max(pulse, urgency));
            Color dividerColor = Color.Lerp(new Color(188, 222, 244, 255), new Color(255, 212, 118, 255), Math.Max(pulse, urgency));

            spriteBatch.Draw(pixelTexture, bounds, outerBorder);
            spriteBatch.Draw(pixelTexture, innerBounds, innerFill);
            spriteBatch.Draw(pixelTexture, faceBounds, faceFill);
            spriteBatch.Draw(pixelTexture, new Rectangle(faceBounds.X, faceBounds.Y, faceBounds.Width, 2), faceHighlight);
            spriteBatch.Draw(pixelTexture, new Rectangle(faceBounds.X, faceBounds.Bottom - 2, faceBounds.Width, 2), new Color(8, 14, 24, 255));
            spriteBatch.Draw(pixelTexture, dividerBounds, dividerColor * 0.18f);
            DrawDividerDots(spriteBatch, pixelTexture, bounds, dividerColor);

            if (font != null)
            {
                string minutesText = (Math.Max(0, RemainingSeconds) / 60).ToString("00");
                string secondsText = (Math.Max(0, RemainingSeconds) % 60).ToString("00");
                Color timeColor = RemainingSeconds <= 10 ? new Color(255, 229, 177) : Color.White;

                DrawDigitString(spriteBatch, font, minutesText, new Vector2(bounds.X + MinuteTextX, bounds.Y + TextY), timeColor);
                DrawDigitString(spriteBatch, font, secondsText, new Vector2(bounds.X + SecondTextX, bounds.Y + TextY), timeColor);

                string title = GetTitleText();
                Vector2 titleSize = font.MeasureString(title);
                Vector2 titlePos = new(bounds.Center.X - (titleSize.X / 2f), bounds.Y + 5);
                spriteBatch.DrawString(font, title, titlePos + Vector2.One, Color.Black);
                spriteBatch.DrawString(font, title, titlePos, new Color(225, 243, 255));
            }
        }

        public string DescribeStatus()
        {
            if (!_isActive)
            {
                return "SpaceGAGA timerboard inactive";
            }

            string timerText = _timeOverTick == int.MinValue ? "stopped" : FormatTimer(RemainingSeconds);
            return $"SpaceGAGA timerboard active on map {_mapId}, timer={timerText}, duration={_durationSec}s";
        }

        public void Reset()
        {
            _isActive = false;
            _mapId = 0;
            _durationSec = 0;
            _timeOverTick = int.MinValue;
            _lastResetTick = int.MinValue;
        }

        private string GetTitleText()
        {
            return _mapId == 922240100 ? "GAGA RESCUE" : "SPACE GAGA";
        }

        private float GetResetPulseStrength()
        {
            if (_lastResetTick == int.MinValue)
            {
                return 0f;
            }

            int elapsed = Environment.TickCount - _lastResetTick;
            if (elapsed < 0 || elapsed >= ResetPulseDurationMs)
            {
                return 0f;
            }

            float normalized = 1f - (elapsed / (float)ResetPulseDurationMs);
            return normalized * normalized;
        }

        private static Rectangle GetTimerboardBounds(Viewport viewport)
        {
            int x = viewport.Width / 2 + TimerboardOffsetX;
            return new Rectangle(x, TimerboardY, TimerboardWidth, TimerboardHeight);
        }

        private static void DrawDividerDots(SpriteBatch spriteBatch, Texture2D pixelTexture, Rectangle bounds, Color dividerColor)
        {
            Rectangle topDot = new(bounds.X + DividerX + 15, bounds.Y + DividerY + 7, 8, 8);
            Rectangle bottomDot = new(bounds.X + DividerX + 15, bounds.Y + DividerY + 21, 8, 8);
            spriteBatch.Draw(pixelTexture, topDot, dividerColor);
            spriteBatch.Draw(pixelTexture, bottomDot, dividerColor);
        }

        private static void DrawDigitString(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color color)
        {
            spriteBatch.DrawString(font, text, position + Vector2.One, Color.Black);
            spriteBatch.DrawString(font, text, position, color);
        }

        private static string FormatTimer(int remainingSeconds)
        {
            int minutes = Math.Max(0, remainingSeconds) / 60;
            int seconds = Math.Max(0, remainingSeconds) % 60;
            return $"{minutes:00}:{seconds:00}";
        }
    }
    #endregion

    #region Massacre Field (CField_Massacre)
    /// <summary>
    /// Massacre Field - Kill counting and gauge system for hunting events.
    ///
    /// - OnMassacreIncGauge (packet 173): Update gauge value
    /// - m_nIncGauge: Current gauge increase amount
    /// - m_nTimer: Timer countdown
    /// - m_nGaugeDec: Gauge decrease rate over time
    /// - Clear effect on timer expiry
    /// </summary>
    public class MassacreField
    {
        #region State
        private bool _isActive = false;

        // Gauge system (from CField_Massacre)
        private int _incGauge = 0;          // m_nIncGauge
        private int _gaugeDec = 1;          // m_nGaugeDec (decrease rate per second)
        private int _currentGauge = 0;
        private int _maxGauge = 100;

        // Timer (from m_pTimerboard)
        private int _timer = 0;             // m_nTimer
        private int _timerRemain = 0;       // m_tRemain
        private int _startTime;
        private bool _showedClearEffect = false;
        #endregion

        #region Display
        private float _displayGauge = 0f;
        private int _killCount = 0;
        private int _comboCount = 0;
        private int _lastKillTime = 0;
        private const int COMBO_TIMEOUT = 3000;

        // UI positions
        private const int GAUGE_X = 10;
        private const int GAUGE_Y = 130;
        private const int GAUGE_WIDTH = 200;
        private const int GAUGE_HEIGHT = 20;
        #endregion

        #region Effects
        private bool _clearEffectActive = false;
        private float _clearEffectAlpha = 0f;
        private int _clearEffectStartTime;
        #endregion

        #region Public Properties
        public bool IsActive => _isActive;
        public int CurrentGauge => _currentGauge;
        public float GaugeProgress => Math.Clamp((float)_currentGauge / _maxGauge, 0f, 1f);
        public int KillCount => _killCount;
        public int ComboCount => _comboCount;
        public int TimerRemain => _timerRemain;
        #endregion

        #region Initialization
        public void Initialize(GraphicsDevice device)
        {
        }

        public void Enable()
        {
            _isActive = true;
            _currentGauge = 0;
            _incGauge = 0;
            _killCount = 0;
            _comboCount = 0;
            _showedClearEffect = false;
        }

        /// <summary>
        /// Set massacre parameters
        /// </summary>
        public void SetParameters(int maxGauge, int timer, int gaugeDec)
        {
            _maxGauge = Math.Max(1, maxGauge);
            _timer = timer;
            _timerRemain = timer;
            _gaugeDec = gaugeDec;
            _startTime = Environment.TickCount;
        }
        #endregion

        #region Packet Handling

        /// <summary>
        /// OnMassacreIncGauge - Packet 173
        /// From client: this->m_nIncGauge = Decode4(iPacket)
        /// </summary>
        public void OnMassacreIncGauge(int newIncGauge, int currentTimeMs)
        {
            System.Diagnostics.Debug.WriteLine($"[MassacreField] OnMassacreIncGauge: {_incGauge} -> {newIncGauge}");

            int increase = newIncGauge - _incGauge;
            _incGauge = newIncGauge;

            // Update kill tracking
            if (increase > 0)
            {
                _killCount++;

                // Check combo
                if (currentTimeMs - _lastKillTime < COMBO_TIMEOUT)
                {
                    _comboCount++;
                }
                else
                {
                    _comboCount = 1;
                }
                _lastKillTime = currentTimeMs;
            }
        }

        /// <summary>
        /// Add kills directly (for testing/simulation)
        /// </summary>
        public void AddKill(int gaugeAmount, int currentTimeMs)
        {
            _incGauge += gaugeAmount;

            _killCount++;
            if (currentTimeMs - _lastKillTime < COMBO_TIMEOUT)
            {
                _comboCount++;
            }
            else
            {
                _comboCount = 1;
            }
            _lastKillTime = currentTimeMs;
        }
        #endregion

        #region Update
        public void Update(int currentTimeMs, float deltaSeconds)
        {
            if (!_isActive) return;

            // Update timer (from client's Update: m_pTimerboard.p->m_tRemain)
            if (_timer > 0)
            {
                int elapsed = currentTimeMs - _startTime;
                _timerRemain = Math.Max(0, _timer - elapsed / 1000);
            }

            // Calculate gauge with decay (from client's _SetDecGauge)
            // Formula: m_nGaugeDec * (m_nTimer - m_tRemain) - m_nIncGauge
            int elapsedSeconds = _timer > 0 ? (_timer - _timerRemain) : 0;
            int decayAmount = _gaugeDec * elapsedSeconds;
            _currentGauge = Math.Max(0, _incGauge - decayAmount);
            _currentGauge = Math.Min(_currentGauge, _maxGauge);

            // Animate gauge display
            float targetGauge = _currentGauge;
            float diff = targetGauge - _displayGauge;
            _displayGauge += diff * 3f * deltaSeconds;

            // Check for clear effect (from client: if m_tRemain <= 1 && !m_bShowedClearEffect)
            if (!_showedClearEffect && _timerRemain <= 1 && _timer > 0)
            {
                TriggerClearEffect(currentTimeMs);
            }

            // Update clear effect
            if (_clearEffectActive)
            {
                int clearElapsed = currentTimeMs - _clearEffectStartTime;
                if (clearElapsed > 3000)
                {
                    _clearEffectActive = false;
                }
                else
                {
                    // Flash effect
                    _clearEffectAlpha = (float)(0.5f + 0.5f * Math.Sin(clearElapsed * 0.01));
                }
            }

            // Reset combo if timed out
            if (currentTimeMs - _lastKillTime > COMBO_TIMEOUT)
            {
                _comboCount = 0;
            }
        }

        private void TriggerClearEffect(int currentTimeMs)
        {
            System.Diagnostics.Debug.WriteLine("[MassacreField] Clear effect triggered!");
            _showedClearEffect = true;
            _clearEffectActive = true;
            _clearEffectStartTime = currentTimeMs;
            // Would call: CField::ShowScreenEffect(this, "clear effect path")
        }
        #endregion

        #region Draw
        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (!_isActive || pixelTexture == null) return;

            int screenWidth = spriteBatch.GraphicsDevice.Viewport.Width;

            // Draw gauge bar
            Color bgColor = new Color(40, 40, 40, 200);
            Color gaugeColor = GetGaugeColor();
            Color borderColor = new Color(100, 100, 100, 255);

            // Background
            spriteBatch.Draw(pixelTexture, new Rectangle(GAUGE_X, GAUGE_Y, GAUGE_WIDTH, GAUGE_HEIGHT), bgColor);

            // Gauge fill
            int fillWidth = (int)(GAUGE_WIDTH * GaugeProgress);
            spriteBatch.Draw(pixelTexture, new Rectangle(GAUGE_X, GAUGE_Y, fillWidth, GAUGE_HEIGHT), gaugeColor);

            // Border
            spriteBatch.Draw(pixelTexture, new Rectangle(GAUGE_X, GAUGE_Y, GAUGE_WIDTH, 2), borderColor);
            spriteBatch.Draw(pixelTexture, new Rectangle(GAUGE_X, GAUGE_Y + GAUGE_HEIGHT - 2, GAUGE_WIDTH, 2), borderColor);
            spriteBatch.Draw(pixelTexture, new Rectangle(GAUGE_X, GAUGE_Y, 2, GAUGE_HEIGHT), borderColor);
            spriteBatch.Draw(pixelTexture, new Rectangle(GAUGE_X + GAUGE_WIDTH - 2, GAUGE_Y, 2, GAUGE_HEIGHT), borderColor);

            // Text
            if (font != null)
            {
                // Gauge value
                string gaugeText = $"{_currentGauge}/{_maxGauge}";
                Vector2 textPos = new Vector2(GAUGE_X + GAUGE_WIDTH + 10, GAUGE_Y + 2);
                spriteBatch.DrawString(font, gaugeText, textPos, Color.White);

                // Kill count
                string killText = $"Kills: {_killCount}";
                spriteBatch.DrawString(font, killText, new Vector2(GAUGE_X, GAUGE_Y + GAUGE_HEIGHT + 5), Color.LightGray);

                // Combo
                if (_comboCount > 1)
                {
                    string comboText = $"{_comboCount}x COMBO!";
                    Color comboColor = _comboCount >= 10 ? Color.Gold :
                                       _comboCount >= 5 ? Color.Orange : Color.Yellow;
                    spriteBatch.DrawString(font, comboText, new Vector2(GAUGE_X + 100, GAUGE_Y + GAUGE_HEIGHT + 5), comboColor);
                }

                // Timer
                if (_timer > 0)
                {
                    int minutes = _timerRemain / 60;
                    int seconds = _timerRemain % 60;
                    string timerText = $"Time: {minutes}:{seconds:D2}";
                    Color timerColor = _timerRemain <= 10 ? Color.Red : Color.White;
                    Vector2 timerPos = new Vector2(screenWidth - 100, GAUGE_Y);
                    spriteBatch.DrawString(font, timerText, timerPos, timerColor);
                }
            }

            // Clear effect overlay
            if (_clearEffectActive)
            {
                Color clearColor = new Color(255, 255, 255, (int)(_clearEffectAlpha * 100));
                spriteBatch.Draw(pixelTexture,
                    new Rectangle(0, 0, screenWidth, spriteBatch.GraphicsDevice.Viewport.Height),
                    clearColor);

                if (font != null)
                {
                    string clearText = "CLEAR!";
                    Vector2 clearSize = font.MeasureString(clearText) * 2;
                    Vector2 clearPos = new Vector2((screenWidth - clearSize.X) / 2, 200);
                    spriteBatch.DrawString(font, clearText, clearPos + new Vector2(3, 3), Color.Black);
                    spriteBatch.DrawString(font, clearText, clearPos, Color.Gold);
                }
            }
        }

        private Color GetGaugeColor()
        {
            float progress = GaugeProgress;
            if (progress >= 0.8f)
                return new Color(255, 215, 0, 255); // Gold
            else if (progress >= 0.5f)
                return new Color(100, 200, 100, 255); // Green
            else if (progress >= 0.25f)
                return new Color(200, 200, 100, 255); // Yellow
            else
                return new Color(200, 100, 100, 255); // Red
        }
        #endregion

        #region Reset
        public void Reset()
        {
            _isActive = false;
            _currentGauge = 0;
            _incGauge = 0;
            _killCount = 0;
            _comboCount = 0;
            _showedClearEffect = false;
            _clearEffectActive = false;
        }
        #endregion
    }
    #endregion
}
