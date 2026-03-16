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
using System.IO;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance.Misc;
using HaCreator.Wz;
using HaSharedLibrary.Wz;
using HaSharedLibrary.Util;

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
                _massacre.Enable(mapId);
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
                _guildBoss.ConfigureFromBoard(board);
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
        private readonly Dictionary<int, Vector2> _participantPositions = new();

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
        private WeddingPacketResponse? _lastPacketResponse;
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
        public int GroomId => _groomId;
        public int BrideId => _brideId;
        public Vector2? GroomPosition => _groomPosition;
        public Vector2? BridePosition => _bridePosition;
        public WeddingPacketResponse? LastPacketResponse => _lastPacketResponse;
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
            _participantPositions.Clear();
            _lastPacketResponse = null;
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

        public void SetParticipantPosition(int characterId, Vector2 worldPosition)
        {
            if (characterId <= 0)
            {
                return;
            }

            _participantPositions[characterId] = worldPosition;
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

        public WeddingPacketResponse? RespondToCurrentDialog(bool accepted, int currentTimeMs)
        {
            if (_currentDialog == null)
            {
                return null;
            }

            WeddingPacketResponse? packetResponse = null;
            if (_currentDialog.Mode == WeddingDialogMode.YesNo)
            {
                if (accepted)
                {
                    packetResponse = new WeddingPacketResponse(
                        WeddingPacketOpcode.GuestBless,
                        _currentStep,
                        true,
                        currentTimeMs);
                }
            }
            else if (ShouldSendParticipantAdvancePacket())
            {
                packetResponse = new WeddingPacketResponse(
                    WeddingPacketOpcode.AdvanceStep,
                    _currentStep,
                    Accepted: true,
                    currentTimeMs);
            }

            _lastPacketResponse = packetResponse;
            _currentDialog = null;
            if (_dialogQueue.Count > 0)
            {
                _currentDialog = _dialogQueue.Dequeue();
                _currentDialog.StartTime = currentTimeMs;
            }

            return packetResponse;
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

            if (_groomId > 0
                && _participantPositions.TryGetValue(_groomId, out Vector2 groomPosition)
                && LocalParticipantRole != WeddingParticipantRole.Groom)
            {
                _groomPosition = groomPosition;
            }

            if (_brideId > 0
                && _participantPositions.TryGetValue(_brideId, out Vector2 bridePosition)
                && LocalParticipantRole != WeddingParticipantRole.Bride)
            {
                _bridePosition = bridePosition;
            }
        }

        private bool ShouldSendParticipantAdvancePacket()
        {
            if (LocalParticipantRole != WeddingParticipantRole.Groom
                && LocalParticipantRole != WeddingParticipantRole.Bride)
            {
                return false;
            }

            return !(_mapId == ChapelWeddingMapId && _currentStep == 2);
        }

        public string DescribeStatus()
        {
            string role = LocalParticipantRole.ToString();
            string dialog = _currentDialog?.Mode == WeddingDialogMode.YesNo
                ? "guest bless prompt"
                : _currentDialog != null
                    ? $"dialog step {_currentStep}"
                    : "no dialog";
            string groomPosition = _groomPosition.HasValue
                ? $"({(int)_groomPosition.Value.X}, {(int)_groomPosition.Value.Y})"
                : "unknown";
            string bridePosition = _bridePosition.HasValue
                ? $"({(int)_bridePosition.Value.X}, {(int)_bridePosition.Value.Y})"
                : "unknown";
            string lastPacket = _lastPacketResponse.HasValue
                ? _lastPacketResponse.Value.ToString()
                : "none";
            return $"Wedding map {_mapId}: step {_currentStep}, role {role}, dialog {dialog}, groom {groomPosition}, bride {bridePosition}, last packet {lastPacket}.";
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
                    RespondToCurrentDialog(accepted: false, currentTimeMs);
                }
            }
        }
        #endregion

        #region Draw
        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int tickCount,
            Texture2D pixelTexture, SpriteFont font)
        {
            if (!_isActive) return;

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
            _participantPositions.Clear();
            _lastPacketResponse = null;
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

    public enum WeddingPacketOpcode
    {
        AdvanceStep = 163,
        GuestBless = 164
    }

    public readonly record struct WeddingPacketResponse(
        WeddingPacketOpcode Opcode,
        int Step,
        bool Accepted,
        int TimeMs)
    {
        public override string ToString()
        {
            return Opcode == WeddingPacketOpcode.GuestBless
                ? $"{(int)Opcode} (guest bless {(Accepted ? "yes" : "no")})"
                : $"{(int)Opcode} (step {Step})";
        }
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
        private GraphicsDevice _graphicsDevice;
        private Texture2D _backgroundTexture;
        private Texture2D _keyTexture;
        private readonly Texture2D[] _digitTextures = new Texture2D[10];
        private bool _assetsLoaded;
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
            _graphicsDevice = device;
            EnsureAssetsLoaded();
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

            EnsureAssetsLoaded();

            Rectangle widgetBounds = GetScoreboardBounds(spriteBatch.GraphicsDevice.Viewport);
            float pulseStrength = GetPulseStrength();
            if (_backgroundTexture != null)
            {
                spriteBatch.Draw(_backgroundTexture, new Vector2(widgetBounds.X, widgetBounds.Y), Color.White);
            }
            else
            {
                spriteBatch.Draw(pixelTexture, widgetBounds, new Color(88, 71, 44));
            }

            if (_keyTexture != null)
            {
                spriteBatch.Draw(_keyTexture, new Vector2(widgetBounds.X + 7, widgetBounds.Y), Color.White);
            }
            else
            {
                DrawKeyGlyph(spriteBatch, pixelTexture, new Rectangle(widgetBounds.X + 7, widgetBounds.Y, 22, 22), new Color(197, 168, 93));
            }

            if (pulseStrength > 0f)
            {
                spriteBatch.Draw(pixelTexture, widgetBounds, new Color(255, 234, 154) * (pulseStrength * 0.22f));
            }

            if (TryDrawBitmapScore(spriteBatch, widgetBounds))
            {
                return;
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

        private void EnsureAssetsLoaded()
        {
            if (_assetsLoaded || _graphicsDevice == null)
            {
                return;
            }

            WzImage objImage = global::HaCreator.Program.FindImage("Map", "Obj/etc.img");
            WzImageProperty goldKey = objImage?["goldkey"];
            _backgroundTexture = LoadCanvasTexture(goldKey?["backgrnd"] as WzCanvasProperty);
            _keyTexture = LoadCanvasTexture(goldKey?["key"] as WzCanvasProperty);

            WzImageProperty digits = goldKey?["number"];
            for (int i = 0; i < _digitTextures.Length; i++)
            {
                _digitTextures[i] = LoadCanvasTexture(digits?[i.ToString()] as WzCanvasProperty);
            }

            _assetsLoaded = true;
        }

        private Texture2D LoadCanvasTexture(WzCanvasProperty canvas)
        {
            if (_graphicsDevice == null || canvas == null)
            {
                return null;
            }

            using var bitmap = canvas.GetLinkedWzCanvasBitmap();
            return bitmap?.ToTexture2DAndDispose(_graphicsDevice);
        }

        private bool TryDrawBitmapScore(SpriteBatch spriteBatch, Rectangle widgetBounds)
        {
            string scoreText = _score.ToString("00");
            int drawX = widgetBounds.X + 67;
            int drawY = widgetBounds.Y + 4;

            foreach (char digitChar in scoreText)
            {
                int digit = digitChar - '0';
                if (digit < 0 || digit >= _digitTextures.Length)
                {
                    return false;
                }

                Texture2D digitTexture = _digitTextures[digit];
                if (digitTexture == null)
                {
                    return false;
                }

                spriteBatch.Draw(digitTexture, new Vector2(drawX, drawY), Color.White);
                drawX += digitTexture.Width - 2;
            }

            return true;
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
    /// - CField_Battlefield::OnTeamChanged (0x5499e0): decodes character id + team byte and forwards it into SetUserTeam.
    /// - CField_Battlefield::SetUserTeam (0x549870): stores the Battlefield team on the user, reapplies the user look, and toggles the minimap for local teams 0/2.
    /// </summary>
    public class BattlefieldField
    {
        public enum BattlefieldWinner
        {
            None,
            Wolves,
            Sheep,
            Draw
        }

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
        private int _lastTeamChangeTimeMs = int.MinValue;
        private bool _clockVisible;
        private int? _localTeamId;
        private BattlefieldWinner _winner = BattlefieldWinner.None;
        private int _resultResolvedTimeMs = int.MinValue;
        private string _resolvedEffectPath;
        private int _resolvedRewardMapId;
        private string _statusMessage;
        private int _statusMessageUntilMs;

        public bool IsActive => _isActive;
        public int WolvesScore => _wolvesScore;
        public int SheepScore => _sheepScore;
        public int DefaultDurationSeconds => _defaultDurationSeconds;
        public int FinishDurationSeconds => _finishDurationSeconds;
        public int? LocalTeamId => _localTeamId;
        public BattlefieldWinner Winner => _winner;
        public string ResolvedEffectPath => _resolvedEffectPath;
        public int ResolvedRewardMapId => _resolvedRewardMapId;
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
            _lastTeamChangeTimeMs = int.MinValue;
            _defaultDurationSeconds = 300;
            _finishDurationSeconds = 3;
            _localTeamId = null;
            _winner = BattlefieldWinner.None;
            _resultResolvedTimeMs = int.MinValue;
            _resolvedEffectPath = null;
            _resolvedRewardMapId = 0;
            _statusMessage = null;
            _statusMessageUntilMs = 0;
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
            ClearResolvedResult();
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
            ClearResolvedResult();
            System.Diagnostics.Debug.WriteLine($"[BattlefieldField] OnClock: type={clockType}, seconds={remainingSeconds}");
        }

        public void OnTeamChanged(int characterId, int teamId, int currentTimeMs)
        {
            if (!_isActive)
            {
                return;
            }

            _lastTeamChangeTimeMs = currentTimeMs;

            if (characterId <= 0)
            {
                SetLocalTeam(teamId, currentTimeMs);
                return;
            }

            ShowStatus($"Battlefield user {characterId} switched to {FormatTeamName(teamId)}.", currentTimeMs, 2500);
        }

        public void SetLocalTeam(int? teamId, int currentTimeMs)
        {
            if (!_isActive)
            {
                return;
            }

            _localTeamId = teamId is >= 0 ? teamId : null;
            _lastTeamChangeTimeMs = currentTimeMs;
            RefreshResolvedResult();

            string teamLabel = _localTeamId.HasValue ? FormatTeamName(_localTeamId.Value) : "unset";
            ShowStatus($"Local Battlefield team: {teamLabel}.", currentTimeMs, 2500);
        }

        public void ResolveResult(BattlefieldWinner winner, int currentTimeMs)
        {
            if (!_isActive)
            {
                return;
            }

            _winner = winner;
            _resultResolvedTimeMs = currentTimeMs;
            _resolvedEffectPath = ResolveEffectPathForLocalOutcome(winner);
            _resolvedRewardMapId = ResolveRewardMapIdForLocalOutcome(winner);

            string suffix = _resolvedRewardMapId > 0
                ? $" reward map {_resolvedRewardMapId}"
                : " reward map unavailable";
            string effectSuffix = string.IsNullOrWhiteSpace(_resolvedEffectPath)
                ? string.Empty
                : $", effect {_resolvedEffectPath}";
            ShowStatus($"Battlefield result: {GetWinnerLabel(winner)}.{suffix}{effectSuffix}", currentTimeMs, Math.Max(1000, _finishDurationSeconds * 1000));
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

            if (_winner == BattlefieldWinner.None
                && _clockVisible
                && _clockDurationSeconds > 0
                && RemainingSeconds <= 0)
            {
                ResolveResult(ComputeWinnerFromScore(), currentTimeMs);
            }

            if (_statusMessage != null && currentTimeMs >= _statusMessageUntilMs)
            {
                _statusMessage = null;
            }
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
            Rectangle footerPanel = new Rectangle(bounds.X + 8, bounds.Bottom - 19, bounds.Width - 16, 11);

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
            spriteBatch.Draw(pixelTexture, footerPanel, new Color(12, 12, 12, 180));

            if (font != null)
            {
                string timerText = FormatTimer(RemainingSeconds);
                string wolvesScore = _wolvesScore.ToString("D2");
                string sheepScore = _sheepScore.ToString("D2");
                string statusText = GetFooterText();
                string teamText = _localTeamId.HasValue ? $"You: {FormatTeamName(_localTeamId.Value)}" : "You: unset";

                spriteBatch.DrawString(font, timerText, new Vector2(timerPanel.X + 9, timerPanel.Y + 1), Color.White);
                spriteBatch.DrawString(font, "Wolves", new Vector2(wolvesPanel.X + 8, wolvesPanel.Y + 2), Color.White);
                spriteBatch.DrawString(font, wolvesScore, new Vector2(wolvesPanel.Right - 26, wolvesPanel.Y + 2), Color.White);
                spriteBatch.DrawString(font, "Sheep", new Vector2(sheepPanel.X + 10, sheepPanel.Y + 2), Color.White);
                spriteBatch.DrawString(font, sheepScore, new Vector2(sheepPanel.Right - 26, sheepPanel.Y + 2), Color.White);
                spriteBatch.DrawString(font, teamText, new Vector2(bounds.X + 10, bounds.Y + 10), new Color(225, 213, 161, 255), 0f, Vector2.Zero, 0.72f, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, statusText, new Vector2(bounds.X + 10, bounds.Bottom - 19), GetFooterColor(), 0f, Vector2.Zero, 0.68f, SpriteEffects.None, 0f);
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
            string teamText = _localTeamId.HasValue ? $"team={FormatTeamName(_localTeamId.Value)}" : "team=unset";
            string resultText = _winner == BattlefieldWinner.None
                ? "result=pending"
                : $"result={GetWinnerLabel(_winner)}, rewardMap={(_resolvedRewardMapId > 0 ? _resolvedRewardMapId : 0)}, effect={(_resolvedEffectPath ?? "none")}";
            return $"Battlefield active, wolves={_wolvesScore:D2}, sheep={_sheepScore:D2}, {clockText}, {teamText}, {resultText}";
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
            _lastTeamChangeTimeMs = int.MinValue;
            _defaultDurationSeconds = 300;
            _finishDurationSeconds = 3;
            _localTeamId = null;
            _winner = BattlefieldWinner.None;
            _resultResolvedTimeMs = int.MinValue;
            _resolvedEffectPath = null;
            _resolvedRewardMapId = 0;
            _statusMessage = null;
            _statusMessageUntilMs = 0;
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

        private string GetFooterText()
        {
            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
                return _statusMessage;
            }

            if (_winner != BattlefieldWinner.None)
            {
                return $"Finish: {GetWinnerLabel(_winner)}";
            }

            return GetLeadingTeamLabel();
        }

        private Color GetFooterColor()
        {
            return _winner switch
            {
                BattlefieldWinner.Wolves => new Color(239, 198, 131, 255),
                BattlefieldWinner.Sheep => new Color(177, 221, 255, 255),
                BattlefieldWinner.Draw => new Color(225, 213, 161, 255),
                _ => new Color(225, 213, 161, 255)
            };
        }

        private BattlefieldWinner ComputeWinnerFromScore()
        {
            if (_wolvesScore == _sheepScore)
            {
                return BattlefieldWinner.Draw;
            }

            return _wolvesScore > _sheepScore ? BattlefieldWinner.Wolves : BattlefieldWinner.Sheep;
        }

        private void RefreshResolvedResult()
        {
            if (_winner == BattlefieldWinner.None)
            {
                return;
            }

            _resolvedEffectPath = ResolveEffectPathForLocalOutcome(_winner);
            _resolvedRewardMapId = ResolveRewardMapIdForLocalOutcome(_winner);
        }

        private void ClearResolvedResult()
        {
            _winner = BattlefieldWinner.None;
            _resultResolvedTimeMs = int.MinValue;
            _resolvedEffectPath = null;
            _resolvedRewardMapId = 0;
        }

        private void ShowStatus(string message, int currentTimeMs, int durationMs)
        {
            _statusMessage = message;
            _statusMessageUntilMs = currentTimeMs + Math.Max(250, durationMs);
        }

        private string ResolveEffectPathForLocalOutcome(BattlefieldWinner winner)
        {
            bool? localWin = GetIsLocalWin(winner);
            if (!localWin.HasValue)
            {
                return null;
            }

            return localWin.Value ? EffectWinPath : EffectLosePath;
        }

        private int ResolveRewardMapIdForLocalOutcome(BattlefieldWinner winner)
        {
            if (!_localTeamId.HasValue)
            {
                return 0;
            }

            return (_localTeamId.Value, winner) switch
            {
                (0, BattlefieldWinner.Wolves) => RewardMapWinWolf,
                (0, BattlefieldWinner.Sheep) => RewardMapLoseWolf,
                (1, BattlefieldWinner.Sheep) => RewardMapWinSheep,
                (1, BattlefieldWinner.Wolves) => RewardMapLoseSheep,
                _ => 0
            };
        }

        private bool? GetIsLocalWin(BattlefieldWinner winner)
        {
            if (!_localTeamId.HasValue || winner == BattlefieldWinner.None || winner == BattlefieldWinner.Draw)
            {
                return null;
            }

            return (_localTeamId.Value, winner) switch
            {
                (0, BattlefieldWinner.Wolves) => true,
                (0, BattlefieldWinner.Sheep) => false,
                (1, BattlefieldWinner.Sheep) => true,
                (1, BattlefieldWinner.Wolves) => false,
                _ => null
            };
        }

        private static string GetWinnerLabel(BattlefieldWinner winner)
        {
            return winner switch
            {
                BattlefieldWinner.Wolves => "Wolves win",
                BattlefieldWinner.Sheep => "Sheep win",
                BattlefieldWinner.Draw => "Draw",
                _ => "Pending"
            };
        }

        private static string FormatTeamName(int teamId)
        {
            return teamId switch
            {
                0 => "Wolves",
                1 => "Sheep",
                2 => "Team 2",
                _ => $"Team {teamId}"
            };
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
        private sealed class GuildBossSpriteFrame
        {
            public Texture2D Texture { get; init; }
            public Point Origin { get; init; }
            public int Delay { get; init; }
        }

        #region State
        private bool _isActive = false;
        private GraphicsDevice _device;
        private int _pulleyState = 0; // 0 = idle, 1 = activating, 2 = active
        private int _mapId;
        private int _healerYMin;
        private int _healerYMax;
        private int _healerRise;
        private int _healerFall;
        private int _healerHealMin;
        private int _healerHealMax;
        private string _healerPath;
        private string _pulleyPath;
        private int _lastPulleyStateChangeTime;
        #endregion

        #region Healer (from CHealer class)
        private bool _healerEnabled = false;
        private float _healerX;
        private float _healerY;
        private float _healerTargetY;
        private float _healerMoveSpeed = 100f;
        private List<GuildBossSpriteFrame> _healerFrames;
        private int _healerFrameIndex = 0;
        private int _lastHealerFrameTime = 0;
        #endregion

        #region Pulley (from CPulley class)
        private bool _pulleyEnabled = false;
        private Rectangle _pulleyArea; // From CPulley::Init: (x-186, y+90, x-60, y+184)
        private float _pulleyX;
        private float _pulleyY;
        private List<GuildBossSpriteFrame> _pulleyFrames;
        private int _pulleyFrameIndex = 0;
        private int _lastPulleyFrameTime = 0;
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
            _device = device;
        }

        public void Enable()
        {
            _isActive = true;
            _pulleyState = 0;
        }

        public void ConfigureFromBoard(Board board)
        {
            WzImage mapImage = board?.MapInfo?.Image;
            if (!_isActive || mapImage == null || _device == null)
            {
                return;
            }

            if (!mapImage.Parsed)
            {
                mapImage.ParseImage();
            }

            _mapId = board.MapInfo.id;

            if (mapImage["healer"] is WzSubProperty healerProp)
            {
                int x = healerProp["x"]?.GetInt() ?? 0;
                _healerYMin = healerProp["yMin"]?.GetInt() ?? 0;
                _healerYMax = healerProp["yMax"]?.GetInt() ?? _healerYMin;
                _healerRise = Math.Max(0, healerProp["rise"]?.GetInt() ?? 0);
                _healerFall = Math.Max(0, healerProp["fall"]?.GetInt() ?? 0);
                _healerHealMin = Math.Max(0, healerProp["healMin"]?.GetInt() ?? 0);
                _healerHealMax = Math.Max(_healerHealMin, healerProp["healMax"]?.GetInt() ?? _healerHealMin);
                _healerPath = healerProp["healer"]?.GetString();

                InitHealer(x, _healerYMin, _healerPath);
                SetHealerFrames(LoadObjectAnimation(_healerPath));
            }

            if (mapImage["pulley"] is WzSubProperty pulleyProp)
            {
                int x = pulleyProp["x"]?.GetInt() ?? 0;
                int y = pulleyProp["y"]?.GetInt() ?? 0;
                _pulleyPath = pulleyProp["pulley"]?.GetString();

                InitPulley(x, y, _pulleyPath);
                SetPulleyFrames(LoadObjectAnimation(_pulleyPath));
            }
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
            _healerFrameIndex = 0;
            _lastHealerFrameTime = 0;
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
            _pulleyFrameIndex = 0;
            _lastPulleyFrameTime = 0;
            // Pulley area from client: (x-186, y+90) to (x-60, y+184)
            _pulleyArea = new Rectangle(x - 186, y + 90, 126, 94);
            System.Diagnostics.Debug.WriteLine($"[GuildBossField] Pulley initialized at ({x}, {y}), area: {_pulleyArea}");
        }

        private void SetHealerFrames(List<GuildBossSpriteFrame> frames)
        {
            _healerFrames = frames;
        }

        private void SetPulleyFrames(List<GuildBossSpriteFrame> frames)
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

            if (_healerYMin != 0 || _healerYMax != 0)
            {
                newY = Math.Clamp(newY, Math.Min(_healerYMin, _healerYMax), Math.Max(_healerYMin, _healerYMax));
            }

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
            _lastPulleyStateChangeTime = currentTimeMs;
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

                AdvanceFrame(_healerFrames, ref _healerFrameIndex, ref _lastHealerFrameTime, currentTimeMs);
            }

            if (_pulleyEnabled)
            {
                AdvanceFrame(_pulleyFrames, ref _pulleyFrameIndex, ref _lastPulleyFrameTime, currentTimeMs);
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
                GuildBossSpriteFrame healerFrame = GetCurrentFrame(_healerFrames, _healerFrameIndex);
                if (healerFrame != null)
                {
                    DrawFrame(spriteBatch, healerFrame, _healerX, _healerY, shiftCenterX, shiftCenterY, Color.White);
                }
                else if (pixelTexture != null)
                {
                    int healerScreenX = (int)_healerX - shiftCenterX;
                    int healerScreenY = (int)_healerY - shiftCenterY;
                    spriteBatch.Draw(pixelTexture, new Rectangle(healerScreenX - 20, healerScreenY - 40, 40, 60),
                        new Color(100, 200, 100, 150));
                }
            }

            // Draw pulley
            if (_pulleyEnabled)
            {
                GuildBossSpriteFrame pulleyFrame = GetCurrentFrame(_pulleyFrames, _pulleyFrameIndex);
                if (pulleyFrame != null)
                {
                    DrawFrame(spriteBatch, pulleyFrame, _pulleyX, _pulleyY, shiftCenterX, shiftCenterY, Color.White);
                }

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

                if (pixelTexture != null)
                {
                    spriteBatch.Draw(pixelTexture, screenArea, pulleyColor);
                }

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
                string info = $"GuildBoss: map={_mapId} pulley={_pulleyState} healer={_healerY:F0}/{_healerTargetY:F0}";
                spriteBatch.DrawString(font, info, new Vector2(10, 60), Color.LightBlue);

                if (_lastPulleyStateChangeTime > 0 && tickCount - _lastPulleyStateChangeTime < 2000)
                {
                    string cue = _pulleyState switch
                    {
                        1 => "Pulley engaged",
                        2 => "Pulley active",
                        _ => "Pulley idle"
                    };
                    Vector2 size = font.MeasureString(cue);
                    spriteBatch.DrawString(
                        font,
                        cue,
                        new Vector2(centerX - (size.X / 2f), 96f),
                        Color.LightGoldenrodYellow);
                }
            }
        }
        #endregion

        public string DescribeStatus()
        {
            if (!_isActive)
            {
                return "Guild boss field inactive";
            }

            string healerRange = _healerEnabled
                ? $"{_healerY:F0}->{_healerTargetY:F0} (range {_healerYMin}..{_healerYMax})"
                : "disabled";
            string pulleyState = _pulleyState switch
            {
                0 => "idle",
                1 => "activating",
                2 => "active",
                _ => $"state {_pulleyState}"
            };

            return $"Guild boss map {_mapId}: healer {healerRange}, pulley {pulleyState}, healer art={_healerPath ?? "none"}, pulley art={_pulleyPath ?? "none"}.";
        }

        #region Reset
        public void Reset()
        {
            _isActive = false;
            _mapId = 0;
            _pulleyState = 0;
            _healerEnabled = false;
            _pulleyEnabled = false;
            _healEffectActive = false;
            _healParticles.Clear();
            _healerPath = null;
            _pulleyPath = null;
            _healerFrames = null;
            _pulleyFrames = null;
            _healerFrameIndex = 0;
            _pulleyFrameIndex = 0;
            _lastHealerFrameTime = 0;
            _lastPulleyFrameTime = 0;
            _lastPulleyStateChangeTime = 0;
        }
        #endregion

        private List<GuildBossSpriteFrame> LoadObjectAnimation(string objectPath)
        {
            if (_device == null || string.IsNullOrWhiteSpace(objectPath))
            {
                return null;
            }

            WzImageProperty animationRoot = ResolveObjectPath(objectPath);
            if (animationRoot == null)
            {
                return null;
            }

            var frames = new List<GuildBossSpriteFrame>();
            foreach (WzImageProperty child in animationRoot.WzProperties.OrderBy(ParseFrameOrder))
            {
                if (WzInfoTools.GetRealProperty(child) is not WzCanvasProperty canvas)
                {
                    continue;
                }

                try
                {
                    var bitmap = canvas.GetLinkedWzCanvasBitmap();
                    if (bitmap == null)
                    {
                        continue;
                    }

                    Texture2D texture = bitmap.ToTexture2DAndDispose(_device);
                    if (texture == null)
                    {
                        continue;
                    }

                    WzVectorProperty origin = canvas["origin"] as WzVectorProperty;
                    frames.Add(new GuildBossSpriteFrame
                    {
                        Texture = texture,
                        Origin = new Point(origin?.X.Value ?? 0, origin?.Y.Value ?? 0),
                        Delay = Math.Max(1, canvas["delay"]?.GetInt() ?? 100)
                    });
                }
                catch
                {
                    // Ignore missing or malformed frames and keep the rest of the sequence usable.
                }
            }

            return frames.Count > 0 ? frames : null;
        }

        private static WzImageProperty ResolveObjectPath(string objectPath)
        {
            string[] parts = objectPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4
                || !string.Equals(parts[0], "Map", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(parts[1], "Obj", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string objectSetName = Path.GetFileNameWithoutExtension(parts[2]);
            WzImage objectSet = Program.InfoManager?.GetObjectSet(objectSetName);
            if (objectSet == null)
            {
                return null;
            }

            if (!objectSet.Parsed)
            {
                objectSet.ParseImage();
            }

            WzObject current = objectSet;
            for (int i = 3; i < parts.Length; i++)
            {
                current = current switch
                {
                    WzImage image => image[parts[i]],
                    WzImageProperty property => property[parts[i]],
                    _ => null
                };

                if (current == null)
                {
                    return null;
                }
            }

            return current as WzImageProperty;
        }

        private static int ParseFrameOrder(WzImageProperty property)
        {
            return int.TryParse(property?.Name, out int order) ? order : int.MaxValue;
        }

        private static void AdvanceFrame(IReadOnlyList<GuildBossSpriteFrame> frames, ref int frameIndex, ref int lastFrameTime, int currentTimeMs)
        {
            if (frames == null || frames.Count <= 1)
            {
                return;
            }

            if (lastFrameTime <= 0)
            {
                lastFrameTime = currentTimeMs;
                return;
            }

            GuildBossSpriteFrame frame = frames[Math.Clamp(frameIndex, 0, frames.Count - 1)];
            while (currentTimeMs - lastFrameTime >= frame.Delay)
            {
                lastFrameTime += frame.Delay;
                frameIndex = (frameIndex + 1) % frames.Count;
                frame = frames[frameIndex];
            }
        }

        private static GuildBossSpriteFrame GetCurrentFrame(IReadOnlyList<GuildBossSpriteFrame> frames, int frameIndex)
        {
            if (frames == null || frames.Count == 0)
            {
                return null;
            }

            return frames[Math.Clamp(frameIndex, 0, frames.Count - 1)];
        }

        private static void DrawFrame(SpriteBatch spriteBatch, GuildBossSpriteFrame frame, float worldX, float worldY, int shiftCenterX, int shiftCenterY, Color color)
        {
            if (frame?.Texture == null)
            {
                return;
            }

            Vector2 position = new Vector2(
                worldX - shiftCenterX - frame.Origin.X,
                worldY - shiftCenterY - frame.Origin.Y);
            spriteBatch.Draw(frame.Texture, position, color);
        }
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
    /// Massacre Field HUD and timerboard flow.
    ///
    /// Client evidence:
    /// - CField_Massacre::OnClock (0x556af0) only reacts to clock type 2, replaces the previous
    ///   clock window, creates a dedicated 258x61 timerboard at (-96, 5), then starts it.
    /// - CTimerboard_Massacre::Draw (0x557100) renders a dedicated source canvas and draws zero-
    ///   padded minutes and seconds at fixed positions: (20, 13) and (105, 13).
    /// - CField_Massacre::Update (0x557530) recalculates the decay gauge from timer elapsed time,
    ///   advances UpdateKeyAnimation every frame, and shows the clear effect once when the board
    ///   reaches one second remaining.
    /// - CField_Massacre::UpdateKeyAnimation (0x556bf0) advances through three one-shot stages.
    /// </summary>
    public class MassacreField
    {
        private const int ComboTimeoutMs = 3000;
        private const int TimerboardWidth = 258;
        private const int TimerboardHeight = 61;
        private const int TimerboardOffsetX = -96;
        private const int TimerboardY = 5;
        private const int TimerMinuteTextX = 20;
        private const int TimerSecondTextX = 105;
        private const int TimerTextY = 13;
        private const int TimerDividerX = 79;
        private const int TimerDividerY = 10;
        private const int TimerDividerWidth = 17;
        private const int TimerDividerHeight = 38;
        private const int GaugeWidth = 259;
        private const int GaugeHeight = 24;
        private const int GaugeOffsetX = -66;
        private const int GaugeY = 71;
        private const int GaugeFillOffsetX = 10;
        private const int GaugeFillOffsetY = 8;
        private const int GaugeFillHeight = 8;
        private const int KeyAnimationX = 7;
        private const int KeyAnimationY = 135;
        private const int KeyStageDurationMs = 180;
        private const int ClearEffectDurationMs = 2200;

        private bool _isActive;
        private int _mapId;
        private int _incGauge;
        private int _gaugeDec = 1;
        private int _currentGauge;
        private int _maxGauge = 100;
        private float _displayGauge;
        private int _killCount;
        private int _comboCount;
        private int _lastKillTime = int.MinValue;
        private int _timerDurationSec;
        private int _timeOverTick = int.MinValue;
        private int _lastClockUpdateTick = int.MinValue;
        private bool _showedClearEffect;
        private bool _clearEffectActive;
        private float _clearEffectAlpha;
        private int _clearEffectStartTime = int.MinValue;
        private int _keyAnimationStage = -1;
        private int _keyAnimationStageStart = int.MinValue;
        private bool _keyAnimationQueued;

        public bool IsActive => _isActive;
        public int CurrentGauge => _currentGauge;
        public int KillCount => _killCount;
        public int ComboCount => _comboCount;
        public int TimerRemain => RemainingSeconds;
        public float GaugeProgress => Math.Clamp(_maxGauge <= 0 ? 0f : _displayGauge / _maxGauge, 0f, 1f);
        public bool HasRunningTimerboard => _timeOverTick != int.MinValue;
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

        public void Enable(int mapId = 0)
        {
            _isActive = true;
            _mapId = mapId;
            ResetRoundState();
        }

        public void SetParameters(int maxGauge, int timer, int gaugeDec)
        {
            _maxGauge = Math.Max(1, maxGauge);
            _gaugeDec = Math.Max(0, gaugeDec);
            OnClock(2, timer, Environment.TickCount);
        }

        public void SetGaugeParameters(int maxGauge, int gaugeDec)
        {
            _maxGauge = Math.Max(1, maxGauge);
            _gaugeDec = Math.Max(0, gaugeDec);
            _currentGauge = Math.Clamp(_currentGauge, 0, _maxGauge);
            _displayGauge = Math.Clamp(_displayGauge, 0f, _maxGauge);
        }

        public void ResetRoundState()
        {
            _incGauge = 0;
            _currentGauge = 0;
            _displayGauge = 0f;
            _killCount = 0;
            _comboCount = 0;
            _lastKillTime = int.MinValue;
            _timerDurationSec = 0;
            _timeOverTick = int.MinValue;
            _lastClockUpdateTick = int.MinValue;
            _showedClearEffect = false;
            _clearEffectActive = false;
            _clearEffectAlpha = 0f;
            _clearEffectStartTime = int.MinValue;
            _keyAnimationStage = -1;
            _keyAnimationStageStart = int.MinValue;
            _keyAnimationQueued = false;
        }

        public void OnClock(int clockType, int durationSec, int currentTimeMs)
        {
            if (!_isActive || clockType != 2)
            {
                return;
            }

            _timerDurationSec = Math.Max(0, durationSec);
            _timeOverTick = _timerDurationSec > 0 ? currentTimeMs + (_timerDurationSec * 1000) : int.MinValue;
            _lastClockUpdateTick = currentTimeMs;
            _showedClearEffect = false;
            _clearEffectActive = false;
            _clearEffectAlpha = 0f;
            _clearEffectStartTime = int.MinValue;
        }

        /// <summary>
        /// OnMassacreIncGauge - Packet 173
        /// From client: this->m_nIncGauge = Decode4(iPacket)
        /// </summary>
        public void OnMassacreIncGauge(int newIncGauge, int currentTimeMs)
        {
            if (!_isActive)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[MassacreField] OnMassacreIncGauge: {_incGauge} -> {newIncGauge}");

            int increase = newIncGauge - _incGauge;
            _incGauge = Math.Max(0, newIncGauge);

            if (increase > 0)
            {
                RegisterKill(currentTimeMs);
                QueueKeyAnimation();
            }
        }

        /// <summary>
        /// Add kills directly (for testing/simulation)
        /// </summary>
        public void AddKill(int gaugeAmount, int currentTimeMs)
        {
            if (!_isActive)
            {
                return;
            }

            _incGauge = Math.Max(0, _incGauge + Math.Max(0, gaugeAmount));
            RegisterKill(currentTimeMs);
            QueueKeyAnimation();
        }

        public void Update(int currentTimeMs, float deltaSeconds)
        {
            if (!_isActive)
            {
                return;
            }

            int remainingSeconds = RemainingSeconds;
            if (_timeOverTick != int.MinValue && _lastClockUpdateTick != int.MinValue)
            {
                int elapsedSeconds = Math.Max(0, _timerDurationSec - remainingSeconds);
                int decayAmount = _gaugeDec * elapsedSeconds;
                _currentGauge = Math.Clamp(_incGauge - decayAmount, 0, _maxGauge);
            }
            else
            {
                _currentGauge = Math.Clamp(_incGauge, 0, _maxGauge);
            }

            float targetGauge = _currentGauge;
            float diff = targetGauge - _displayGauge;
            _displayGauge = Math.Clamp(_displayGauge + (diff * MathF.Min(1f, 8f * deltaSeconds)), 0f, _maxGauge);

            UpdateKeyAnimation(currentTimeMs);

            if (!_showedClearEffect && _timeOverTick != int.MinValue && remainingSeconds <= 1)
            {
                TriggerClearEffect(currentTimeMs);
            }

            if (_clearEffectActive)
            {
                int clearElapsed = currentTimeMs - _clearEffectStartTime;
                if (clearElapsed < 0 || clearElapsed >= ClearEffectDurationMs)
                {
                    _clearEffectActive = false;
                    _clearEffectAlpha = 0f;
                }
                else
                {
                    float normalized = 1f - (clearElapsed / (float)ClearEffectDurationMs);
                    _clearEffectAlpha = normalized * normalized;
                }
            }

            if (_lastKillTime != int.MinValue && currentTimeMs - _lastKillTime > ComboTimeoutMs)
            {
                _comboCount = 0;
            }
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (!_isActive || pixelTexture == null)
            {
                return;
            }

            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            DrawTimerboard(spriteBatch, pixelTexture, font, viewport);
            DrawGaugeHud(spriteBatch, pixelTexture, font, viewport);
            DrawKeyAnimation(spriteBatch, pixelTexture, font);
            DrawClearEffect(spriteBatch, pixelTexture, font, viewport);
        }

        public string DescribeStatus()
        {
            if (!_isActive)
            {
                return "Massacre HUD inactive";
            }

            string timerText = HasRunningTimerboard ? FormatTimer(RemainingSeconds) : "stopped";
            return $"Massacre map {_mapId}, timer={timerText}, gauge={_currentGauge}/{_maxGauge}, inc={_incGauge}, decay={_gaugeDec}/s, kills={_killCount}, combo={_comboCount}";
        }

        public void Reset()
        {
            _isActive = false;
            _mapId = 0;
            _maxGauge = 100;
            _gaugeDec = 1;
            ResetRoundState();
        }

        private void RegisterKill(int currentTimeMs)
        {
            _killCount++;
            if (_lastKillTime != int.MinValue && currentTimeMs - _lastKillTime < ComboTimeoutMs)
            {
                _comboCount++;
            }
            else
            {
                _comboCount = 1;
            }

            _lastKillTime = currentTimeMs;
        }

        private void QueueKeyAnimation()
        {
            if (_keyAnimationStage >= 0)
            {
                _keyAnimationQueued = true;
                return;
            }

            _keyAnimationStage = 0;
            _keyAnimationStageStart = Environment.TickCount;
            _keyAnimationQueued = false;
        }

        private void UpdateKeyAnimation(int currentTimeMs)
        {
            if (_keyAnimationStage < 0)
            {
                return;
            }

            if (_keyAnimationStageStart == int.MinValue)
            {
                _keyAnimationStageStart = currentTimeMs;
            }

            if (currentTimeMs - _keyAnimationStageStart < KeyStageDurationMs)
            {
                return;
            }

            _keyAnimationStageStart = currentTimeMs;
            _keyAnimationStage++;
            if (_keyAnimationStage > 2)
            {
                if (_keyAnimationQueued)
                {
                    _keyAnimationStage = 0;
                    _keyAnimationQueued = false;
                }
                else
                {
                    _keyAnimationStage = -1;
                    _keyAnimationStageStart = int.MinValue;
                }
            }
        }

        private void TriggerClearEffect(int currentTimeMs)
        {
            System.Diagnostics.Debug.WriteLine("[MassacreField] Clear effect triggered!");
            _showedClearEffect = true;
            _clearEffectActive = true;
            _clearEffectStartTime = currentTimeMs;
        }

        private void DrawTimerboard(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, Viewport viewport)
        {
            if (!HasRunningTimerboard && !_showedClearEffect)
            {
                return;
            }

            Rectangle bounds = new(viewport.Width / 2 + TimerboardOffsetX, TimerboardY, TimerboardWidth, TimerboardHeight);
            spriteBatch.Draw(pixelTexture, bounds, new Color(18, 21, 24, 228));
            spriteBatch.Draw(pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), new Color(95, 127, 160, 255));
            spriteBatch.Draw(pixelTexture, new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2), new Color(34, 45, 58, 255));

            Rectangle divider = new(bounds.X + TimerDividerX, bounds.Y + TimerDividerY, TimerDividerWidth, TimerDividerHeight);
            spriteBatch.Draw(pixelTexture, divider, new Color(44, 53, 66, 255));

            if (font == null)
            {
                return;
            }

            int remaining = RemainingSeconds;
            string minuteText = $"{remaining / 60:00}";
            string secondText = $"{remaining % 60:00}";
            Color timeColor = remaining <= 10 ? new Color(255, 170, 120) : new Color(232, 242, 255);

            DrawDigitString(spriteBatch, font, minuteText, new Vector2(bounds.X + TimerMinuteTextX, bounds.Y + TimerTextY), timeColor);
            DrawDigitString(spriteBatch, font, secondText, new Vector2(bounds.X + TimerSecondTextX, bounds.Y + TimerTextY), timeColor);
        }

        private void DrawGaugeHud(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, Viewport viewport)
        {
            Rectangle bounds = new(viewport.Width / 2 + GaugeOffsetX, GaugeY, GaugeWidth, GaugeHeight);
            Rectangle fillBounds = new(bounds.X + GaugeFillOffsetX, bounds.Y + GaugeFillOffsetY, GaugeWidth - (GaugeFillOffsetX * 2), GaugeFillHeight);

            spriteBatch.Draw(pixelTexture, bounds, new Color(25, 18, 16, 224));
            spriteBatch.Draw(pixelTexture, fillBounds, new Color(54, 34, 25, 255));

            int fillWidth = Math.Clamp((int)MathF.Round(fillBounds.Width * GaugeProgress), 0, fillBounds.Width);
            if (fillWidth > 0)
            {
                Color fillColor = GetGaugeColor(GaugeProgress);
                spriteBatch.Draw(pixelTexture, new Rectangle(fillBounds.X, fillBounds.Y, fillWidth, fillBounds.Height), fillColor);
            }

            spriteBatch.Draw(pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), new Color(185, 145, 83, 255));
            spriteBatch.Draw(pixelTexture, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), new Color(78, 54, 31, 255));

            if (font == null)
            {
                return;
            }

            string gaugeText = $"{_currentGauge}/{_maxGauge}";
            string statusText = _comboCount > 1 ? $"{_comboCount}x combo" : $"{_killCount} kills";
            Color statusColor = _comboCount >= 10 ? Color.Gold : _comboCount >= 5 ? Color.Orange : new Color(238, 220, 191);

            spriteBatch.DrawString(font, "MASSACRE", new Vector2(bounds.X + 8, bounds.Y - 20), new Color(238, 220, 191));
            Vector2 gaugeValueSize = font.MeasureString(gaugeText);
            spriteBatch.DrawString(font, gaugeText, new Vector2(bounds.Right - gaugeValueSize.X - 8, bounds.Y - 20), Color.White);
            spriteBatch.DrawString(font, statusText, new Vector2(bounds.X + 8, bounds.Bottom + 4), statusColor);
        }

        private void DrawKeyAnimation(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (_keyAnimationStage < 0)
            {
                return;
            }

            int elapsed = Environment.TickCount - _keyAnimationStageStart;
            float normalized = 1f - Math.Clamp(elapsed / (float)KeyStageDurationMs, 0f, 1f);
            float alpha = 0.35f + (normalized * 0.65f);
            float widthScale = 1f + (_keyAnimationStage * 0.22f);
            Rectangle pulse = new(
                KeyAnimationX - (int)((18 * widthScale) / 2f),
                KeyAnimationY - (_keyAnimationStage * 6),
                (int)(72 * widthScale),
                16);

            Color pulseColor = _keyAnimationStage switch
            {
                0 => new Color(255, 213, 112),
                1 => new Color(255, 165, 66),
                _ => new Color(255, 118, 68)
            };

            spriteBatch.Draw(pixelTexture, pulse, pulseColor * alpha);
            if (font != null)
            {
                string text = _keyAnimationStage switch
                {
                    0 => "KEY",
                    1 => "KEY!",
                    _ => "KEY!!"
                };
                spriteBatch.DrawString(font, text, new Vector2(KeyAnimationX, KeyAnimationY - 22 - (_keyAnimationStage * 6)), Color.White * alpha);
            }
        }

        private void DrawClearEffect(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, Viewport viewport)
        {
            if (!_clearEffectActive)
            {
                return;
            }

            spriteBatch.Draw(pixelTexture, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.White * (0.16f * _clearEffectAlpha));
            if (font != null)
            {
                const string clearText = "CLEAR!";
                Vector2 textSize = font.MeasureString(clearText) * 1.5f;
                Vector2 pos = new((viewport.Width - textSize.X) / 2f, 200f);
                spriteBatch.DrawString(font, clearText, pos + new Vector2(2f, 2f), Color.Black * _clearEffectAlpha);
                spriteBatch.DrawString(font, clearText, pos, new Color(255, 225, 118) * _clearEffectAlpha);
            }
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

        private static Color GetGaugeColor(float progress)
        {
            if (progress >= 0.8f)
            {
                return new Color(255, 215, 112);
            }

            if (progress >= 0.5f)
            {
                return new Color(255, 165, 66);
            }

            if (progress >= 0.25f)
            {
                return new Color(227, 109, 62);
            }

            return new Color(181, 65, 54);
        }
    }
    #endregion
}
