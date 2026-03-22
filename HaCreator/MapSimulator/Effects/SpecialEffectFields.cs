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
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance.Misc;
using HaCreator.MapSimulator.Character;
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

        public void SetGuildBossPlayerState(Rectangle? localPlayerHitbox)
        {
            _guildBoss.SetLocalPlayerHitbox(localPlayerHitbox ?? Rectangle.Empty);
        }

        public void SetBattlefieldPlayerState(int? localCharacterId)
        {
            _battlefield.SetLocalPlayerState(localCharacterId);
        }
        #endregion

        #region Initialization
        public void Initialize(
            GraphicsDevice device,
            Action<string> requestBgmOverride = null,
            Action clearBgmOverride = null)
        {
            _wedding.Initialize(device, requestBgmOverride, clearBgmOverride);
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

            if (_massacre.IsActive)
            {
                _massacre.Configure(board?.MapInfo);
            }

            if (_dojo.IsActive)
            {
                _dojo.Configure(board?.MapInfo);
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
        private const string BlessEffectImageName = "BasicEff.img";
        private const string BlessEffectPath = "Wedding";
        private const string WeddingBgmPath = "BgmEvent/wedding";
        private const string WeddingUiImageName = "UIWindow.img";
        private const string CeremonyTextOverlayPath = "wedding/text/0";

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
        private bool _ceremonyTextOverlayActive = false;
        private float _ceremonyTextOverlayAlpha = 0f;
        #endregion

        #region Visual Effects
        private List<WeddingSparkle> _sparkles = new();
        private List<IDXObject> _blessFrames;
        private Texture2D _ceremonyTextOverlayTexture;
        private Point _ceremonyTextOverlayOrigin;
        private Random _random = new();
        #endregion

        #region Dialog
        private WeddingDialog _currentDialog;
        private readonly Queue<WeddingDialog> _dialogQueue = new();
        private WeddingPacketResponse? _lastPacketResponse;
        private Action<string> _requestBgmOverride;
        private Action _clearBgmOverride;
        #endregion

        #region Public Properties
        public bool IsActive => _isActive;
        public int CurrentStep => _currentStep;
        public bool IsBlessEffectActive => _blessEffectActive;
        public bool IsCeremonyTextOverlayActive => _ceremonyTextOverlayActive;
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
        public void Initialize(
            GraphicsDevice device,
            Action<string> requestBgmOverride = null,
            Action clearBgmOverride = null)
        {
            _requestBgmOverride = requestBgmOverride;
            _clearBgmOverride = clearBgmOverride;
            _blessFrames = LoadBlessFrames(device);
            LoadCeremonyOverlay(device);
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
            _ceremonyTextOverlayActive = false;
            _ceremonyTextOverlayAlpha = 0f;
            _currentDialog = null;
            _dialogQueue.Clear();
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
                SetCeremonyTextOverlay(true);
                _requestBgmOverride?.Invoke(WeddingBgmPath);
            }
            else
            {
                SetCeremonyTextOverlay(false);
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
                _blessEffectAlpha = 1f;

                if (_blessFrames != null && _blessFrames.Count > 0)
                {
                    _sparkles.Clear();
                    return;
                }

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

        private void LoadCeremonyOverlay(GraphicsDevice device)
        {
            if (device == null)
            {
                return;
            }

            try
            {
                WzImage uiWindowImage = global::HaCreator.Program.FindImage("UI", WeddingUiImageName);
                uiWindowImage?.ParseImage();
                WzCanvasProperty overlayCanvas = WzInfoTools.GetRealProperty(uiWindowImage?.GetFromPath(CeremonyTextOverlayPath)) as WzCanvasProperty;
                if (overlayCanvas == null)
                {
                    return;
                }

                using var bitmap = overlayCanvas.GetLinkedWzCanvasBitmap();
                if (bitmap == null)
                {
                    return;
                }

                _ceremonyTextOverlayTexture = bitmap.ToTexture2D(device);
                System.Drawing.PointF origin = overlayCanvas.GetCanvasOriginPosition();
                _ceremonyTextOverlayOrigin = new Point((int)origin.X, (int)origin.Y);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WeddingField] Failed to load ceremony overlay: {ex.Message}");
            }
        }

        private List<IDXObject> LoadBlessFrames(GraphicsDevice device)
        {
            if (device == null)
            {
                return null;
            }

            try
            {
                WzImage basicEffImage = global::HaCreator.Program.FindImage("Effect", BlessEffectImageName);
                basicEffImage?.ParseImage();
                WzImageProperty blessProperty = basicEffImage?.GetFromPath(BlessEffectPath) as WzImageProperty;
                if (blessProperty == null)
                {
                    return null;
                }

                var frames = new List<IDXObject>();
                int frameIndex = 0;
                while (true)
                {
                    WzImageProperty frameProperty = WzInfoTools.GetRealProperty(blessProperty[frameIndex.ToString()]);
                    if (frameProperty == null)
                    {
                        break;
                    }

                    WzCanvasProperty frameCanvas = frameProperty as WzCanvasProperty;
                    if (frameCanvas == null && frameProperty is WzUOLProperty frameUol)
                    {
                        frameCanvas = frameUol.LinkValue as WzCanvasProperty;
                    }

                    if (frameCanvas != null)
                    {
                        using var bitmap = frameCanvas.GetLinkedWzCanvasBitmap();
                        if (bitmap != null)
                        {
                            Texture2D texture = bitmap.ToTexture2D(device);
                            System.Drawing.PointF origin = frameCanvas.GetCanvasOriginPosition();
                            int delay = InfoTool.GetOptionalInt(frameCanvas["delay"], 100) ?? 100;
                            frames.Add(new DXObject(-(int)origin.X, -(int)origin.Y, texture, delay));
                        }
                    }

                    frameIndex++;
                }

                return frames.Count > 0 ? frames : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WeddingField] Failed to load bless effect frames: {ex.Message}");
                return null;
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
            string scene = _ceremonyTextOverlayActive ? "declaration overlay active" : "no scene overlay";
            return $"Wedding map {_mapId}: step {_currentStep}, role {role}, dialog {dialog}, scene {scene}, groom {groomPosition}, bride {bridePosition}, last packet {lastPacket}.";
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

            float overlayTargetAlpha = _ceremonyTextOverlayActive ? 1f : 0f;
            _ceremonyTextOverlayAlpha = MathHelper.Clamp(
                _ceremonyTextOverlayAlpha + ((overlayTargetAlpha - _ceremonyTextOverlayAlpha) * Math.Min(deltaSeconds * 10f, 1f)),
                0f,
                1f);

            // Update bless effect
            if (_blessEffectActive && (_blessFrames == null || _blessFrames.Count == 0))
            {
                int elapsed = currentTimeMs - _blessEffectStartTime;

                // Update sparkles
                foreach (var sparkle in _sparkles)
                {
                    if (elapsed < sparkle.SpawnDelay) continue;

                    int sparkleElapsed = elapsed - sparkle.SpawnDelay;
                    int loopedElapsed = sparkle.LifeTime > 0
                        ? sparkleElapsed % sparkle.LifeTime
                        : sparkleElapsed;
                    float lifeProgress = sparkle.LifeTime > 0
                        ? (float)loopedElapsed / sparkle.LifeTime
                        : 0f;

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

            DrawCeremonyOverlay(spriteBatch);

            // Draw bless effect sparkles
            if (_blessEffectActive && _blessEffectAlpha > 0)
            {
                int screenWidth = spriteBatch.GraphicsDevice.Viewport.Width;
                int screenHeight = spriteBatch.GraphicsDevice.Viewport.Height;
                Vector2 blessCenter = GetBlessEffectScreenCenter(mapShiftX, mapShiftY, centerX, centerY, screenWidth, screenHeight);

                if (_blessFrames != null && _blessFrames.Count > 0)
                {
                    IDXObject currentBlessFrame = GetCurrentBlessFrame(tickCount);
                    if (currentBlessFrame != null)
                    {
                        currentBlessFrame.DrawBackground(
                            spriteBatch,
                            skeletonMeshRenderer,
                            gameTime,
                            (int)blessCenter.X + currentBlessFrame.X,
                            (int)blessCenter.Y + currentBlessFrame.Y,
                            Color.White,
                            false,
                            null);
                    }
                }
                else
                {
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

        private void DrawCeremonyOverlay(SpriteBatch spriteBatch)
        {
            if (_ceremonyTextOverlayTexture == null || _ceremonyTextOverlayAlpha <= 0f)
            {
                return;
            }

            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            Vector2 center = new Vector2(viewport.Width * 0.5f, viewport.Height * 0.5f - 54f);
            Color tint = Color.White * _ceremonyTextOverlayAlpha;
            spriteBatch.Draw(
                _ceremonyTextOverlayTexture,
                center,
                null,
                tint,
                0f,
                new Vector2(_ceremonyTextOverlayOrigin.X, _ceremonyTextOverlayOrigin.Y),
                1f,
                SpriteEffects.None,
                0f);
        }

        private void SetCeremonyTextOverlay(bool active)
        {
            _ceremonyTextOverlayActive = active;
            if (!_ceremonyTextOverlayActive && _ceremonyTextOverlayAlpha < 0.001f)
            {
                _ceremonyTextOverlayAlpha = 0f;
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

        private IDXObject GetCurrentBlessFrame(int currentTimeMs)
        {
            if (_blessFrames == null || _blessFrames.Count == 0)
            {
                return null;
            }

            int totalDuration = 0;
            foreach (IDXObject frame in _blessFrames)
            {
                totalDuration += Math.Max(frame.Delay, 1);
            }

            if (totalDuration <= 0)
            {
                return _blessFrames[0];
            }

            int elapsed = Math.Max(0, currentTimeMs - _blessEffectStartTime);
            int loopTime = elapsed % totalDuration;
            int accumulated = 0;
            foreach (IDXObject frame in _blessFrames)
            {
                accumulated += Math.Max(frame.Delay, 1);
                if (loopTime < accumulated)
                {
                    return frame;
                }
            }

            return _blessFrames[_blessFrames.Count - 1];
        }
        #endregion

        #region Reset
        public void Reset()
        {
            _clearBgmOverride?.Invoke();
            _isActive = false;
            _currentStep = 0;
            _blessEffectActive = false;
            _ceremonyTextOverlayActive = false;
            _ceremonyTextOverlayAlpha = 0f;
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
        private readonly struct BattlefieldBitmapGlyph
        {
            public BattlefieldBitmapGlyph(Texture2D texture, Point origin)
            {
                Texture = texture;
                Origin = origin;
            }

            public Texture2D Texture { get; }
            public Point Origin { get; }
        }

        public sealed class BattlefieldTeamLookPreset
        {
            public BattlefieldTeamLookPreset(int teamId, IReadOnlyDictionary<EquipSlot, int> equipmentItemIds)
            {
                TeamId = teamId;
                EquipmentItemIds = equipmentItemIds ?? new Dictionary<EquipSlot, int>();
            }

            public int TeamId { get; }
            public IReadOnlyDictionary<EquipSlot, int> EquipmentItemIds { get; }
        }

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
        private const int ScoreboardBackgroundOffsetX = 21;
        private const int LeftScoreOriginX = 43;
        private const int RightScoreOriginX = 130;
        private const int ScoreOriginY = 12;
        private const int TimerOriginX = 104;
        private const int TimerOriginY = 61;
        private const int TimerDigitSpacing = -1;
        private const int TimerGroupSpacing = 2;

        private bool _isActive;
        private GraphicsDevice _device;
        private bool _assetsLoaded;
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
        private int? _localCharacterId;
        private int? _localTeamId;
        private BattlefieldWinner _winner = BattlefieldWinner.None;
        private int _resultResolvedTimeMs = int.MinValue;
        private string _resolvedEffectPath;
        private int _resolvedRewardMapId;
        private int _pendingTransferMapId = -1;
        private int _pendingTransferAtTick = int.MinValue;
        private string _statusMessage;
        private int _statusMessageUntilMs;
        private readonly Dictionary<int, BattlefieldTeamLookPreset> _teamLookPresets = new();
        private readonly Dictionary<int, int> _remoteUserTeams = new();
        private Texture2D _scoreboardTexture;
        private BattlefieldBitmapGlyph[] _wolvesDigitGlyphs = Array.Empty<BattlefieldBitmapGlyph>();
        private BattlefieldBitmapGlyph[] _sheepDigitGlyphs = Array.Empty<BattlefieldBitmapGlyph>();
        private BattlefieldBitmapGlyph[] _timerDigitGlyphs = Array.Empty<BattlefieldBitmapGlyph>();
        private BattlefieldBitmapGlyph? _timerSeparatorGlyph;

        public bool IsActive => _isActive;
        public int WolvesScore => _wolvesScore;
        public int SheepScore => _sheepScore;
        public int DefaultDurationSeconds => _defaultDurationSeconds;
        public int FinishDurationSeconds => _finishDurationSeconds;
        public int? LocalCharacterId => _localCharacterId;
        public int? LocalTeamId => _localTeamId;
        public BattlefieldWinner Winner => _winner;
        public string ResolvedEffectPath => _resolvedEffectPath;
        public int ResolvedRewardMapId => _resolvedRewardMapId;
        public IReadOnlyDictionary<int, BattlefieldTeamLookPreset> TeamLookPresets => _teamLookPresets;
        public IReadOnlyDictionary<int, int> RemoteUserTeams => _remoteUserTeams;
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
            _device = device;
            EnsureAssetsLoaded();
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
            _localCharacterId = null;
            _localTeamId = null;
            _winner = BattlefieldWinner.None;
            _resultResolvedTimeMs = int.MinValue;
            _resolvedEffectPath = null;
            _resolvedRewardMapId = 0;
            _pendingTransferMapId = -1;
            _pendingTransferAtTick = int.MinValue;
            _statusMessage = null;
            _statusMessageUntilMs = 0;
            EffectWinPath = null;
            EffectLosePath = null;
            RewardMapWinWolf = 0;
            RewardMapWinSheep = 0;
            RewardMapLoseWolf = 0;
            RewardMapLoseSheep = 0;
            _teamLookPresets.Clear();
            _remoteUserTeams.Clear();
        }

        public void Configure(MapInfo mapInfo)
        {
            if (!_isActive || mapInfo == null)
            {
                return;
            }

            _teamLookPresets.Clear();

            for (int i = 0; i < mapInfo.additionalNonInfoProps.Count; i++)
            {
                if (mapInfo.additionalNonInfoProps[i] is not WzSubProperty property)
                {
                    continue;
                }

                if (string.Equals(property.Name, "battleField", StringComparison.OrdinalIgnoreCase))
                {
                    _defaultDurationSeconds = Math.Max(1, InfoTool.GetOptionalInt(property["timeDefault"]) ?? _defaultDurationSeconds);
                    _finishDurationSeconds = Math.Max(0, InfoTool.GetOptionalInt(property["timeFinish"]) ?? _finishDurationSeconds);
                    EffectWinPath = InfoTool.GetOptionalString(property["effectWin"]);
                    EffectLosePath = InfoTool.GetOptionalString(property["effectLose"]);
                    RewardMapWinWolf = InfoTool.GetOptionalInt(property["rewardMapWinWolf"]) ?? RewardMapWinWolf;
                    RewardMapWinSheep = InfoTool.GetOptionalInt(property["rewardMapWinSheep"]) ?? RewardMapWinSheep;
                    RewardMapLoseWolf = InfoTool.GetOptionalInt(property["rewardMapLoseWolf"]) ?? RewardMapLoseWolf;
                    RewardMapLoseSheep = InfoTool.GetOptionalInt(property["rewardMapLoseSheep"]) ?? RewardMapLoseSheep;
                    continue;
                }

                if (string.Equals(property.Name, "user", StringComparison.OrdinalIgnoreCase))
                {
                    LoadTeamLookPresets(property);
                }
            }
        }

        public bool TryGetTeamLookPreset(int teamId, out BattlefieldTeamLookPreset preset)
        {
            return _teamLookPresets.TryGetValue(teamId, out preset);
        }

        public void SetLocalPlayerState(int? localCharacterId)
        {
            _localCharacterId = localCharacterId > 0 ? localCharacterId : null;
            if (_localCharacterId.HasValue
                && _remoteUserTeams.Remove(_localCharacterId.Value, out int promotedTeamId))
            {
                _localTeamId = promotedTeamId >= 0 ? promotedTeamId : null;
                RefreshResolvedResult();
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

            if (characterId <= 0
                || (_localCharacterId.HasValue && characterId == _localCharacterId.Value))
            {
                SetLocalTeam(teamId, currentTimeMs);
                return;
            }

            if (teamId >= 0)
            {
                _remoteUserTeams[characterId] = teamId;
            }
            else
            {
                _remoteUserTeams.Remove(characterId);
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
            _pendingTransferMapId = _resolvedRewardMapId > 0 ? _resolvedRewardMapId : -1;
            _pendingTransferAtTick = _pendingTransferMapId > 0
                ? currentTimeMs + Math.Max(0, _finishDurationSeconds * 1000)
                : int.MinValue;

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

            if (_pendingTransferMapId > 0
                && _pendingTransferAtTick != int.MinValue
                && currentTimeMs >= _pendingTransferAtTick)
            {
                _pendingTransferAtTick = int.MinValue;
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

            EnsureAssetsLoaded();

            Rectangle bounds = GetScoreboardBounds(spriteBatch.GraphicsDevice.Viewport);
            float pulse = GetScorePulseStrength();
            Vector2 scoreboardOrigin = new Vector2(bounds.X + ScoreboardBackgroundOffsetX, bounds.Y);
            if (_scoreboardTexture != null)
            {
                spriteBatch.Draw(_scoreboardTexture, scoreboardOrigin, Color.White);
            }
            else
            {
                spriteBatch.Draw(pixelTexture, new Rectangle((int)scoreboardOrigin.X, (int)scoreboardOrigin.Y, 215, ScoreboardHeight), new Color(16, 62, 82, 255));
            }

            if (pulse > 0f)
            {
                spriteBatch.Draw(
                    pixelTexture,
                    new Rectangle((int)scoreboardOrigin.X, (int)scoreboardOrigin.Y, _scoreboardTexture?.Width ?? 215, _scoreboardTexture?.Height ?? ScoreboardHeight),
                    Color.White * (pulse * 0.18f));
            }

            if (!TryDrawScore(spriteBatch, scoreboardOrigin, _sheepScore, _sheepDigitGlyphs, LeftScoreOriginX, ScoreOriginY)
                && font != null)
            {
                spriteBatch.DrawString(font, _sheepScore.ToString(CultureInfo.InvariantCulture), scoreboardOrigin + new Vector2(LeftScoreOriginX, ScoreOriginY), Color.White);
            }

            if (!TryDrawScore(spriteBatch, scoreboardOrigin, _wolvesScore, _wolvesDigitGlyphs, RightScoreOriginX, ScoreOriginY)
                && font != null)
            {
                spriteBatch.DrawString(font, _wolvesScore.ToString(CultureInfo.InvariantCulture), scoreboardOrigin + new Vector2(RightScoreOriginX, ScoreOriginY), Color.White);
            }

            if (!TryDrawTime(spriteBatch, scoreboardOrigin, RemainingSeconds) && font != null)
            {
                spriteBatch.DrawString(font, FormatTimerForFallback(RemainingSeconds), scoreboardOrigin + new Vector2(TimerOriginX, TimerOriginY - 6), Color.White);
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
            string transferText = _pendingTransferMapId > 0 ? $", pendingTransfer={_pendingTransferMapId}" : string.Empty;
            string lookPresetText = _teamLookPresets.Count > 0
                ? $", lookPresets={string.Join(";", _teamLookPresets.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}:{kvp.Value.EquipmentItemIds.Count}"))}"
                : string.Empty;
            string remoteTeamText = _remoteUserTeams.Count > 0
                ? $", remoteTeams={string.Join(";", _remoteUserTeams.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}:{FormatTeamName(kvp.Value)}"))}"
                : string.Empty;
            return $"Battlefield active, wolves={_wolvesScore:D2}, sheep={_sheepScore:D2}, {clockText}, {teamText}, {resultText}{transferText}{lookPresetText}{remoteTeamText}";
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
            _localCharacterId = null;
            _localTeamId = null;
            _winner = BattlefieldWinner.None;
            _resultResolvedTimeMs = int.MinValue;
            _resolvedEffectPath = null;
            _resolvedRewardMapId = 0;
            _pendingTransferMapId = -1;
            _pendingTransferAtTick = int.MinValue;
            _statusMessage = null;
            _statusMessageUntilMs = 0;
            EffectWinPath = null;
            EffectLosePath = null;
            RewardMapWinWolf = 0;
            RewardMapWinSheep = 0;
            RewardMapLoseWolf = 0;
            RewardMapLoseSheep = 0;
            _teamLookPresets.Clear();
            _remoteUserTeams.Clear();
        }

        private static Rectangle GetScoreboardBounds(Viewport viewport)
        {
            int x = viewport.Width / 2 + ScoreboardOffsetX;
            return new Rectangle(x, ScoreboardY, ScoreboardWidth, ScoreboardHeight);
        }

        private void LoadTeamLookPresets(WzSubProperty userProperty)
        {
            foreach (WzImageProperty child in userProperty.WzProperties)
            {
                if (child is not WzSubProperty userEntry)
                {
                    continue;
                }

                int parsedTeamId = 0;
                int? teamId = InfoTool.GetOptionalInt(userEntry["cond"]?["battleFieldTeam"]);
                if (!teamId.HasValue && !int.TryParse(userEntry.Name, out parsedTeamId))
                {
                    continue;
                }

                if (!teamId.HasValue)
                {
                    teamId = parsedTeamId;
                }

                if (userEntry["look"] is not WzSubProperty lookProperty)
                {
                    continue;
                }

                Dictionary<EquipSlot, int> equipmentItemIds = new();
                foreach (WzImageProperty lookEntry in lookProperty.WzProperties)
                {
                    int? itemId = InfoTool.GetOptionalInt(lookEntry);
                    if (!itemId.HasValue
                        || !TryResolveBattlefieldEquipSlot(lookEntry.Name, itemId.Value, out EquipSlot slot))
                    {
                        continue;
                    }

                    equipmentItemIds[slot] = itemId.Value;
                }

                _teamLookPresets[teamId.Value] = new BattlefieldTeamLookPreset(teamId.Value, equipmentItemIds);
            }
        }

        private static bool TryResolveBattlefieldEquipSlot(string propertyName, int itemId, out EquipSlot slot)
        {
            switch (propertyName?.ToLowerInvariant())
            {
                case "cap":
                    slot = EquipSlot.Cap;
                    return true;
                case "gloves":
                    slot = EquipSlot.Glove;
                    return true;
                case "shoes":
                    slot = EquipSlot.Shoes;
                    return true;
                case "cape":
                    slot = EquipSlot.Cape;
                    return true;
                case "pants":
                    slot = EquipSlot.Pants;
                    return true;
                case "clothes":
                    slot = (itemId / 10000) == 105 ? EquipSlot.Longcoat : EquipSlot.Coat;
                    return true;
            }

            slot = (itemId / 10000) switch
            {
                100 => EquipSlot.Cap,
                104 => EquipSlot.Coat,
                105 => EquipSlot.Longcoat,
                106 => EquipSlot.Pants,
                107 => EquipSlot.Shoes,
                108 => EquipSlot.Glove,
                110 => EquipSlot.Cape,
                _ => EquipSlot.None
            };

            return slot != EquipSlot.None;
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

        public int ConsumePendingTransferMapId()
        {
            if (_pendingTransferMapId <= 0 || _pendingTransferAtTick != int.MinValue)
            {
                return -1;
            }

            int pendingTransferMapId = _pendingTransferMapId;
            _pendingTransferMapId = -1;
            return pendingTransferMapId;
        }

        private void EnsureAssetsLoaded()
        {
            if (_assetsLoaded || _device == null)
            {
                return;
            }

            WzImage objImage = global::HaCreator.Program.FindImage("Map", "Obj/etc.img");
            WzSubProperty battleField = objImage?["battleField"] as WzSubProperty;
            _scoreboardTexture = LoadCanvasTexture(battleField?["backgrnd"] as WzCanvasProperty);
            _wolvesDigitGlyphs = LoadGlyphSet(battleField?["fontScore0"] as WzSubProperty, 10);
            _sheepDigitGlyphs = LoadGlyphSet(battleField?["fontScore1"] as WzSubProperty, 10);
            _timerDigitGlyphs = LoadGlyphSet(battleField?["fontTime"] as WzSubProperty, 10);
            _timerSeparatorGlyph = LoadGlyph(battleField?["fontTime"]?["comma"] as WzCanvasProperty);
            _assetsLoaded = true;
        }

        private Texture2D LoadCanvasTexture(WzCanvasProperty canvas)
        {
            if (_device == null || canvas == null)
            {
                return null;
            }

            using var bitmap = canvas.GetLinkedWzCanvasBitmap();
            return bitmap?.ToTexture2DAndDispose(_device);
        }

        private BattlefieldBitmapGlyph[] LoadGlyphSet(WzSubProperty parent, int count)
        {
            BattlefieldBitmapGlyph[] glyphs = new BattlefieldBitmapGlyph[count];
            for (int i = 0; i < count; i++)
            {
                glyphs[i] = LoadGlyph(parent?[i.ToString()] as WzCanvasProperty) ?? default;
            }

            return glyphs;
        }

        private BattlefieldBitmapGlyph? LoadGlyph(WzCanvasProperty canvas)
        {
            Texture2D texture = LoadCanvasTexture(canvas);
            if (texture == null)
            {
                return null;
            }

            System.Drawing.Point canvasOrigin = canvas?[WzCanvasProperty.OriginPropertyName]?.GetPoint() ?? System.Drawing.Point.Empty;
            return new BattlefieldBitmapGlyph(texture, new Point(canvasOrigin.X, canvasOrigin.Y));
        }

        private bool TryDrawScore(SpriteBatch spriteBatch, Vector2 scoreboardOrigin, int score, BattlefieldBitmapGlyph[] glyphs, int originX, int originY)
        {
            if (glyphs == null || glyphs.Length < 10 || glyphs.Any(glyph => glyph.Texture == null))
            {
                return false;
            }

            string scoreText = Math.Max(0, score).ToString(CultureInfo.InvariantCulture);
            int totalWidth = 0;
            for (int i = 0; i < scoreText.Length; i++)
            {
                int digit = scoreText[i] - '0';
                if (digit < 0 || digit >= glyphs.Length || glyphs[digit].Texture == null)
                {
                    return false;
                }

                totalWidth += glyphs[digit].Texture.Width;
            }

            float drawX = scoreboardOrigin.X + originX + Math.Max(0f, (42 - totalWidth) / 2f);
            for (int i = 0; i < scoreText.Length; i++)
            {
                BattlefieldBitmapGlyph glyph = glyphs[scoreText[i] - '0'];
                spriteBatch.Draw(glyph.Texture, new Vector2(drawX - glyph.Origin.X, scoreboardOrigin.Y + originY - glyph.Origin.Y), Color.White);
                drawX += glyph.Texture.Width;
            }

            return true;
        }

        private bool TryDrawTime(SpriteBatch spriteBatch, Vector2 scoreboardOrigin, int totalSeconds)
        {
            if (_timerDigitGlyphs == null
                || _timerDigitGlyphs.Length < 10
                || _timerDigitGlyphs.Any(glyph => glyph.Texture == null)
                || !_timerSeparatorGlyph.HasValue
                || _timerSeparatorGlyph.Value.Texture == null)
            {
                return false;
            }

            int safeSeconds = Math.Max(0, totalSeconds);
            int hours = safeSeconds / 3600;
            int minutes = (safeSeconds / 60) % 60;
            int seconds = safeSeconds % 60;
            string[] groups =
            {
                hours.ToString("D2", CultureInfo.InvariantCulture),
                minutes.ToString("D2", CultureInfo.InvariantCulture),
                seconds.ToString("D2", CultureInfo.InvariantCulture)
            };

            float drawX = scoreboardOrigin.X + TimerOriginX;
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                string group = groups[groupIndex];
                for (int i = 0; i < group.Length; i++)
                {
                    BattlefieldBitmapGlyph glyph = _timerDigitGlyphs[group[i] - '0'];
                    spriteBatch.Draw(glyph.Texture, new Vector2(drawX - glyph.Origin.X, scoreboardOrigin.Y + TimerOriginY - glyph.Origin.Y), Color.White);
                    drawX += glyph.Texture.Width + TimerDigitSpacing;
                }

                if (groupIndex == groups.Length - 1)
                {
                    continue;
                }

                BattlefieldBitmapGlyph separator = _timerSeparatorGlyph.Value;
                spriteBatch.Draw(separator.Texture, new Vector2(drawX - separator.Origin.X, scoreboardOrigin.Y + TimerOriginY - separator.Origin.Y), Color.White);
                drawX += separator.Texture.Width + TimerGroupSpacing;
            }

            return true;
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
            _pendingTransferMapId = _resolvedRewardMapId > 0 ? _resolvedRewardMapId : -1;
        }

        private void ClearResolvedResult()
        {
            _winner = BattlefieldWinner.None;
            _resultResolvedTimeMs = int.MinValue;
            _resolvedEffectPath = null;
            _resolvedRewardMapId = 0;
            _pendingTransferMapId = -1;
            _pendingTransferAtTick = int.MinValue;
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

        private static string FormatTimerForFallback(int totalSeconds)
        {
            int safeSeconds = Math.Max(0, totalSeconds);
            int hours = safeSeconds / 3600;
            int minutes = (safeSeconds / 60) % 60;
            int seconds = safeSeconds % 60;
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
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
        private enum GuildBossPacketType
        {
            HealerMove = 344,
            PulleyStateChange = 345
        }

        private enum GuildBossPacketSource
        {
            External = 0,
            LocalPreview = 1
        }

        private enum LocalPulleySequenceStage
        {
            None = 0,
            Activating = 1,
            Active = 2
        }

        private sealed class GuildBossSpriteFrame
        {
            public Texture2D Texture { get; init; }
            public Point Origin { get; init; }
            public int Delay { get; init; }
        }

        public readonly record struct PulleyPacketRequest(int TickCount, int Sequence);

        #region State
        private bool _isActive = false;
        private GraphicsDevice _device;
        private int _pulleyState = 0; // 0 = idle, 1 = activating, 2 = active
        private int _mapId;
        private Rectangle _localPlayerHitbox;
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
        private List<GuildBossSpriteFrame> _healerFrames;
        private int _healerFrameIndex = 0;
        private int _lastHealerFrameTime = 0;
        #endregion

        #region Pulley (from CPulley class)
        private bool _pulleyEnabled = false;
        private Rectangle _pulleyArea; // From CPulley::Init: (x-186, y+90, x-60, y+184)
        private float _pulleyX;
        private float _pulleyY;
        private int _lastHealAmount;
        private int _localPulleySequenceNextTransitionTick = int.MinValue;
        private int _localPulleyCooldownUntil = int.MinValue;
        private int _statusCueExpiresAt = int.MinValue;
        private string _statusCueText;
        private LocalPulleySequenceStage _localPulleySequenceStage = LocalPulleySequenceStage.None;
        private PulleyPacketRequest? _pendingPulleyPacketRequest;
        private int _pulleyPacketSequence;
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
        private const int PulleyActivationDelayMs = 450;
        private const int PulleyActiveDurationMs = 900;
        private const int PulleyReuseDelayMs = 1200;
        private const int StatusCueDurationMs = 1800;

        public bool IsActive => _isActive;
        public int PulleyState => _pulleyState;
        public float HealerY => _healerY;
        public float HealerTargetY => _healerTargetY;
        public bool IsHealEffectActive => _healEffectActive;
        public bool HasPendingLocalPulleySequence => _localPulleySequenceStage != LocalPulleySequenceStage.None;
        public PulleyPacketRequest? PendingPulleyPacketRequest => _pendingPulleyPacketRequest;
        public bool IsLocalPlayerWithinPulleyArea => _pulleyEnabled && !_localPlayerHitbox.IsEmpty && _pulleyArea.Intersects(_localPlayerHitbox);
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
            _localPlayerHitbox = Rectangle.Empty;
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

        public void SetLocalPlayerHitbox(Rectangle localPlayerHitbox)
        {
            _localPlayerHitbox = localPlayerHitbox;
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
            ApplyHealerMove(newY, currentTimeMs, GuildBossPacketSource.External);
        }

        /// <summary>
        /// Decodes and applies CField_GuildBoss packet payloads.
        /// Packet 344: signed little-endian int16 healer Y.
        /// Packet 345: unsigned byte pulley state.
        /// </summary>
        public bool TryApplyPacket(int packetType, byte[] payload, int currentTimeMs, out string error)
        {
            error = null;
            if (!_isActive)
            {
                error = "Guild boss field inactive";
                return false;
            }

            if (payload == null)
            {
                error = "Packet payload is required";
                return false;
            }

            return TryApplyPacket(packetType, payload.AsSpan(), currentTimeMs, GuildBossPacketSource.External, out error);
        }

        private void ApplyHealerMove(int newY, int currentTimeMs, GuildBossPacketSource source)
        {
            if (!_healerEnabled) return;

            if (source == GuildBossPacketSource.External)
            {
                CancelLocalPulleySequence(preserveCooldown: true);
            }

            if (_healerYMin != 0 || _healerYMax != 0)
            {
                newY = Math.Clamp(newY, Math.Min(_healerYMin, _healerYMax), Math.Max(_healerYMin, _healerYMax));
            }

            System.Diagnostics.Debug.WriteLine($"[GuildBossField] OnHealerMove: {_healerY} -> {newY}");
            float previousY = _healerY;
            _healerY = newY;
            _healerTargetY = newY;

            // Trigger heal effect when healer moves up
            if (newY < previousY)
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
            ApplyPulleyStateChange(newState, currentTimeMs, GuildBossPacketSource.External);
        }

        private void ApplyPulleyStateChange(int newState, int currentTimeMs, GuildBossPacketSource source)
        {
            if (source == GuildBossPacketSource.External)
            {
                CancelLocalPulleySequence(preserveCooldown: true);
            }

            System.Diagnostics.Debug.WriteLine($"[GuildBossField] OnPulleyStateChange: {_pulleyState} -> {newState}");
            _pulleyState = newState;
            _lastPulleyStateChangeTime = currentTimeMs;
        }

        public bool TryHandleLocalPulleyAttack(Rectangle attackBounds, int currentTimeMs, out string message)
        {
            message = null;
            if (!_isActive || !_pulleyEnabled || attackBounds.IsEmpty || !_pulleyArea.Intersects(attackBounds))
            {
                return false;
            }

            TriggerPulleyHitAnimation(currentTimeMs);

            if (_pulleyState != 0 || _localPulleySequenceStage != LocalPulleySequenceStage.None || currentTimeMs < _localPulleyCooldownUntil)
            {
                return true;
            }

            _pulleyPacketSequence++;
            _pendingPulleyPacketRequest = new PulleyPacketRequest(currentTimeMs, _pulleyPacketSequence);
            ApplyPulleyStateChange(1, currentTimeMs, GuildBossPacketSource.LocalPreview);
            if (_healerEnabled && _healerRise > 0)
            {
                ApplyHealerMove(ClampHealerY((int)MathF.Round(_healerTargetY) - _healerRise), currentTimeMs, GuildBossPacketSource.LocalPreview);
            }

            _localPulleySequenceStage = LocalPulleySequenceStage.Activating;
            _localPulleySequenceNextTransitionTick = unchecked(currentTimeMs + PulleyActivationDelayMs);
            _localPulleyCooldownUntil = unchecked(currentTimeMs + PulleyActivationDelayMs + PulleyActiveDurationMs + PulleyReuseDelayMs);
            SetStatusCue("Pulley engaged", currentTimeMs);
            message = "Guild boss pulley engaged.";
            return true;
        }

        public bool TryConsumePulleyPacketRequest(out PulleyPacketRequest request)
        {
            if (_pendingPulleyPacketRequest.HasValue)
            {
                request = _pendingPulleyPacketRequest.Value;
                _pendingPulleyPacketRequest = null;
                return true;
            }

            request = default;
            return false;
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

            UpdateLocalPulleySequence(currentTimeMs);

            if (_healerEnabled)
            {
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

            if (_statusCueExpiresAt != int.MinValue && currentTimeMs >= _statusCueExpiresAt)
            {
                _statusCueExpiresAt = int.MinValue;
                _statusCueText = null;
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

                    if (_pulleyState == 0 && IsLocalPlayerWithinPulleyArea)
                    {
                        const string interactPrompt = "Attack pulley";
                        Vector2 promptSize = font.MeasureString(interactPrompt);
                        Vector2 promptPosition = new(
                            screenArea.Center.X - (promptSize.X / 2f),
                            screenArea.Y - 36f);
                        spriteBatch.DrawString(font, interactPrompt, promptPosition, Color.LightGoldenrodYellow);
                    }
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

                if (!string.IsNullOrWhiteSpace(_statusCueText) && _statusCueExpiresAt != int.MinValue && tickCount < _statusCueExpiresAt)
                {
                    Vector2 size = font.MeasureString(_statusCueText);
                    spriteBatch.DrawString(
                        font,
                        _statusCueText,
                        new Vector2(centerX - (size.X / 2f), 96f),
                        Color.LightGoldenrodYellow);
                }
                else if (_lastPulleyStateChangeTime > 0 && tickCount - _lastPulleyStateChangeTime < 2000)
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
            string previewState = HasPendingLocalPulleySequence ? $", preview={_localPulleySequenceStage}" : string.Empty;
            string pendingPacket = _pendingPulleyPacketRequest.HasValue ? $", request={_pendingPulleyPacketRequest.Value.Sequence}" : string.Empty;

            return $"Guild boss map {_mapId}: healer {healerRange}, pulley {pulleyState}{previewState}{pendingPacket}, rise={_healerRise}, fall={_healerFall}, heal={_healerHealMin}..{_healerHealMax}, healer art={_healerPath ?? "none"}, pulley art={_pulleyPath ?? "none"}.";
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
            _lastHealAmount = 0;
            _localPulleySequenceStage = LocalPulleySequenceStage.None;
            _localPulleySequenceNextTransitionTick = int.MinValue;
            _localPulleyCooldownUntil = int.MinValue;
            _pendingPulleyPacketRequest = null;
            _pulleyPacketSequence = 0;
            _statusCueExpiresAt = int.MinValue;
            _statusCueText = null;
            _localPlayerHitbox = Rectangle.Empty;
        }
        #endregion

        private void UpdateLocalPulleySequence(int currentTimeMs)
        {
            if (_localPulleySequenceStage == LocalPulleySequenceStage.None
                || _localPulleySequenceNextTransitionTick == int.MinValue
                || currentTimeMs < _localPulleySequenceNextTransitionTick)
            {
                return;
            }

            switch (_localPulleySequenceStage)
            {
                case LocalPulleySequenceStage.Activating:
                    TryApplyPacket((int)GuildBossPacketType.PulleyStateChange, stackalloc byte[] { 2 }, currentTimeMs, GuildBossPacketSource.LocalPreview, out _);
                    _lastHealAmount = RollHealAmount();
                    SetStatusCue($"Healer restored {_lastHealAmount}", currentTimeMs);
                    _localPulleySequenceStage = LocalPulleySequenceStage.Active;
                    _localPulleySequenceNextTransitionTick = unchecked(currentTimeMs + PulleyActiveDurationMs);
                    break;

                case LocalPulleySequenceStage.Active:
                    TryApplyPacket((int)GuildBossPacketType.PulleyStateChange, stackalloc byte[] { 0 }, currentTimeMs, GuildBossPacketSource.LocalPreview, out _);
                    if (_healerEnabled && _healerFall > 0)
                    {
                        Span<byte> payload = stackalloc byte[2];
                        short healerY = checked((short)ClampHealerY((int)MathF.Round(_healerTargetY) + _healerFall));
                        BinaryPrimitives.WriteInt16LittleEndian(payload, healerY);
                        TryApplyPacket((int)GuildBossPacketType.HealerMove, payload, currentTimeMs, GuildBossPacketSource.LocalPreview, out _);
                    }

                    _localPulleySequenceStage = LocalPulleySequenceStage.None;
                    _localPulleySequenceNextTransitionTick = int.MinValue;
                    break;
            }
        }

        private bool TryApplyPacket(int packetType, ReadOnlySpan<byte> payload, int currentTimeMs, GuildBossPacketSource source, out string error)
        {
            error = null;
            switch ((GuildBossPacketType)packetType)
            {
                case GuildBossPacketType.HealerMove:
                    if (payload.Length < sizeof(short))
                    {
                        error = "Packet 344 requires a 2-byte healer Y payload";
                        return false;
                    }

                    ApplyHealerMove(BinaryPrimitives.ReadInt16LittleEndian(payload), currentTimeMs, source);
                    return true;

                case GuildBossPacketType.PulleyStateChange:
                    if (payload.IsEmpty)
                    {
                        error = "Packet 345 requires a 1-byte pulley state payload";
                        return false;
                    }

                    ApplyPulleyStateChange(payload[0], currentTimeMs, source);
                    return true;

                default:
                    error = $"Unsupported guild boss packet type {packetType}";
                    return false;
            }
        }

        private void CancelLocalPulleySequence(bool preserveCooldown)
        {
            _localPulleySequenceStage = LocalPulleySequenceStage.None;
            _localPulleySequenceNextTransitionTick = int.MinValue;
            _pendingPulleyPacketRequest = null;
            if (!preserveCooldown)
            {
                _localPulleyCooldownUntil = int.MinValue;
            }
        }

        private int RollHealAmount()
        {
            if (_healerHealMax <= _healerHealMin)
            {
                return Math.Max(0, _healerHealMin);
            }

            return _random.Next(Math.Max(0, _healerHealMin), _healerHealMax + 1);
        }

        private int ClampHealerY(int y)
        {
            if (_healerYMin == 0 && _healerYMax == 0)
            {
                return y;
            }

            return Math.Clamp(y, Math.Min(_healerYMin, _healerYMax), Math.Max(_healerYMin, _healerYMax));
        }

        private void SetStatusCue(string text, int currentTimeMs)
        {
            _statusCueText = text;
            _statusCueExpiresAt = unchecked(currentTimeMs + StatusCueDurationMs);
        }

        private void TriggerPulleyHitAnimation(int currentTimeMs)
        {
            _pulleyFrameIndex = 0;
            _lastPulleyFrameTime = currentTimeMs;
            SetStatusCue("Pulley hit", currentTimeMs);
        }

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
        private const int TimerLayerOffsetX = -55;
        private const int TimerLayerY = 16;
        private const int ClockOffsetY = 26;
        private static readonly Point ClockOrigin = new(102, 26);
        private const int PlayerOffsetX = -231;
        private const int PlayerOffsetY = 50;
        private static readonly Point PlayerOrigin = new(160, 28);
        private const int MonsterOffsetX = 231;
        private const int MonsterOffsetY = 50;
        private static readonly Point MonsterOrigin = new(160, 28);
        private const int EnergyOffsetX = 20;
        private const int EnergyOffsetY = 130;
        private const int TimerMinuteX = 0;
        private const int TimerSecondX = 68;
        private const int TimerDigitSpacing = 23;
        private const int TimerDigitY = 0;
        private const int TimerColonX = 51;
        private const int TimerColonY = 4;
        private const int BarGaugeWidth = 305;
        private const int BarGaugeHeight = 13;
        private const int PlayerGaugeOffsetX = 7;
        private const int MonsterGaugeOffsetX = 7;
        private const int BarGaugeOffsetY = 6;
        private const int EnergyGaugeWidth = 9;
        private const int EnergyGaugeHeight = 77;
        private const int EnergyGaugeOffsetX = 7;
        private const int EnergyGaugeOffsetY = 7;
        private const int EnergyMax = 10000;

        private bool _isActive;
        private int _mapId;
        private int _stage;
        private int _timerDurationSec;
        private int _timeOverTick = int.MinValue;
        private int _lastClockUpdateTick = int.MinValue;
        private int _returnMapId = -1;
        private int _forcedReturnMapId = -1;
        private int _pendingTransferMapId = -1;
        private int _pendingTransferAtTick = int.MinValue;
        private int _playerHp;
        private int _playerMaxHp = 100;
        private float? _bossHpPercent;
        private float? _lastBossHpPercent;
        private int _energy;
        private int _stageBannerStartTick = int.MinValue;
        private int _resultEffectStartTick = int.MinValue;
        private GraphicsDevice _device;
        private bool _assetsLoaded;
        private Texture2D _clockTexture;
        private Texture2D _playerTexture;
        private Texture2D _playerGaugeTexture;
        private Texture2D _monsterTexture;
        private Texture2D _monsterGaugeTexture;
        private Texture2D _energyTexture;
        private Texture2D _energyGaugeTexture;
        private Texture2D _timerColonTexture;
        private readonly Texture2D[] _digitTextures = new Texture2D[10];
        private List<DojoFrame> _energyFullFrames;
        private List<DojoFrame> _stageFrames;
        private readonly Dictionary<int, List<DojoFrame>> _stageNumberFrames = new();
        private List<DojoFrame> _clearFrames;
        private List<DojoFrame> _timeOverFrames;
        private DojoResultEffect _resultEffect = DojoResultEffect.None;

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
            _device = device;
            EnsureAssetsLoaded();
        }

        public void Enable(int mapId)
        {
            _isActive = true;
            _mapId = mapId;
            _stage = ResolveStage(mapId);
            _timerDurationSec = 0;
            _timeOverTick = int.MinValue;
            _lastClockUpdateTick = int.MinValue;
            _returnMapId = -1;
            _forcedReturnMapId = -1;
            _pendingTransferMapId = -1;
            _pendingTransferAtTick = int.MinValue;
            _playerHp = 0;
            _playerMaxHp = 100;
            _bossHpPercent = null;
            _lastBossHpPercent = null;
            _energy = 0;
            EnsureAssetsLoaded();
            _stageBannerStartTick = Environment.TickCount;
            _resultEffectStartTick = int.MinValue;
            _resultEffect = DojoResultEffect.None;
        }

        public void Configure(MapInfo mapInfo)
        {
            _returnMapId = NormalizeTransferMapId(mapInfo?.returnMap);
            _forcedReturnMapId = NormalizeTransferMapId(mapInfo?.forcedReturn);
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
            if (_resultEffect == DojoResultEffect.TimeOver)
            {
                _resultEffect = DojoResultEffect.None;
                _resultEffectStartTick = int.MinValue;
            }

            _pendingTransferMapId = -1;
            _pendingTransferAtTick = int.MinValue;
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
                if (_lastBossHpPercent.GetValueOrDefault() > 0f
                    && _bossHpPercent.Value <= 0f
                    && _resultEffect == DojoResultEffect.None)
                {
                    ShowClearResult(Environment.TickCount);
                }
            }
            else
            {
                _bossHpPercent = null;
            }

            _lastBossHpPercent = _bossHpPercent;
        }

        public void SetEnergy(int energy)
        {
            _energy = Math.Clamp(energy, 0, EnergyMax);
        }

        public int ConsumePendingTransferMapId()
        {
            if (_pendingTransferMapId <= 0 || _pendingTransferAtTick != int.MinValue)
            {
                return -1;
            }

            int pendingTransferMapId = _pendingTransferMapId;
            _pendingTransferMapId = -1;
            return pendingTransferMapId;
        }

        public void SetStage(int stage, int currentTimeMs)
        {
            _stage = Math.Clamp(stage, 0, 32);
            _stageBannerStartTick = currentTimeMs;
        }

        public void ShowClearResult(int currentTimeMs)
        {
            _resultEffect = DojoResultEffect.Clear;
            _resultEffectStartTick = currentTimeMs;
            _pendingTransferMapId = -1;
            _pendingTransferAtTick = int.MinValue;
        }

        public void ShowTimeOverResult(int currentTimeMs)
        {
            _resultEffect = DojoResultEffect.TimeOver;
            _resultEffectStartTick = currentTimeMs;
            _timeOverTick = 0;
            _timerDurationSec = 0;
            _pendingTransferMapId = ResolveExitMapId();
            _pendingTransferAtTick = _pendingTransferMapId > 0
                ? currentTimeMs + GetAnimationDurationMs(_timeOverFrames)
                : int.MinValue;
        }

        public void Update(int currentTimeMs, float deltaSeconds)
        {
            if (!_isActive)
            {
                return;
            }

            if (_timeOverTick != int.MinValue && _timeOverTick > 0 && currentTimeMs >= _timeOverTick && _resultEffect != DojoResultEffect.TimeOver)
            {
                ShowTimeOverResult(currentTimeMs);
            }

            if (_pendingTransferMapId > 0
                && _pendingTransferAtTick != int.MinValue
                && currentTimeMs >= _pendingTransferAtTick)
            {
                _pendingTransferAtTick = int.MinValue;
            }
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int currentTimeMs)
        {
            if (!_isActive || pixelTexture == null)
            {
                return;
            }

            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            EnsureAssetsLoaded();

            DrawClock(spriteBatch, viewport, pixelTexture, font);
            DrawGaugeBars(spriteBatch, viewport, pixelTexture);
            DrawEnergy(spriteBatch, viewport, pixelTexture);
            DrawStageBanner(spriteBatch, viewport, font, currentTimeMs);
            DrawResultEffect(spriteBatch, viewport, font, currentTimeMs);
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
            string transferText = _pendingTransferMapId > 0 ? $", pendingReturn={_pendingTransferMapId}" : string.Empty;
            return $"Mu Lung Dojo floor {_stage}, timer={timerText}, boss={bossText}, player={_playerHp}/{_playerMaxHp}, energy={_energy}/{EnergyMax}{transferText}";
        }

        public void Reset()
        {
            _isActive = false;
            _mapId = 0;
            _stage = -1;
            _timerDurationSec = 0;
            _timeOverTick = int.MinValue;
            _lastClockUpdateTick = int.MinValue;
            _returnMapId = -1;
            _forcedReturnMapId = -1;
            _pendingTransferMapId = -1;
            _pendingTransferAtTick = int.MinValue;
            _playerHp = 0;
            _playerMaxHp = 100;
            _bossHpPercent = null;
            _lastBossHpPercent = null;
            _energy = 0;
            _stageBannerStartTick = int.MinValue;
            _resultEffectStartTick = int.MinValue;
            _resultEffect = DojoResultEffect.None;
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

        private static int NormalizeTransferMapId(int? mapId)
        {
            return mapId.HasValue && mapId.Value > 0 && mapId.Value != MapConstants.MaxMap
                ? mapId.Value
                : -1;
        }

        private int ResolveExitMapId()
        {
            return _forcedReturnMapId > 0 ? _forcedReturnMapId : _returnMapId;
        }

        private static int GetAnimationDurationMs(IReadOnlyList<DojoFrame> frames)
        {
            if (frames == null || frames.Count == 0)
            {
                return 0;
            }

            int totalDuration = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                totalDuration += Math.Max(1, frames[i].Delay);
            }

            return totalDuration;
        }

        private void EnsureAssetsLoaded()
        {
            if (_assetsLoaded || _device == null)
            {
                return;
            }

            WzImage uiWindow = global::HaCreator.Program.FindImage("UI", "UIWindow.img")
                ?? global::HaCreator.Program.FindImage("UI", "UIWindow2.img");
            WzImage effectImage = global::HaCreator.Program.FindImage("Map", "Effect.img");

            WzImageProperty muruengRaid = uiWindow?["muruengRaid"];
            _clockTexture = LoadCanvasTexture(muruengRaid?["clock"]?["0"] as WzCanvasProperty);
            _playerTexture = LoadCanvasTexture(muruengRaid?["player"]?["0"] as WzCanvasProperty);
            _playerGaugeTexture = LoadCanvasTexture(muruengRaid?["player"]?["Gage"]?["0"] as WzCanvasProperty);
            _monsterTexture = LoadCanvasTexture(muruengRaid?["monster"]?["0"] as WzCanvasProperty);
            _monsterGaugeTexture = LoadCanvasTexture(muruengRaid?["monster"]?["Gage"]?["0"] as WzCanvasProperty);
            _energyTexture = LoadCanvasTexture(muruengRaid?["energy"]?["empty"]?["0"] as WzCanvasProperty);
            _energyGaugeTexture = LoadCanvasTexture(muruengRaid?["energy"]?["empty"]?["Gage"]?["0"] as WzCanvasProperty);
            _timerColonTexture = LoadCanvasTexture(muruengRaid?["number"]?["bar"] as WzCanvasProperty);

            for (int i = 0; i < _digitTextures.Length; i++)
            {
                _digitTextures[i] = LoadCanvasTexture(muruengRaid?["number"]?[i.ToString()] as WzCanvasProperty);
            }

            _energyFullFrames = LoadAnimationFrames(muruengRaid?["energy"]?["full"]);
            WzImageProperty dojang = effectImage?["dojang"];
            _stageFrames = LoadAnimationFrames(dojang?["start"]?["stage"]);
            _clearFrames = LoadAnimationFrames(dojang?["end"]?["clear"]);
            _timeOverFrames = LoadAnimationFrames(dojang?["timeOver"]);

            WzImageProperty startNumbers = dojang?["start"]?["number"];
            if (startNumbers != null)
            {
                foreach (WzImageProperty child in startNumbers.WzProperties)
                {
                    if (!int.TryParse(child.Name, out int stage))
                    {
                        continue;
                    }

                    List<DojoFrame> frames = LoadAnimationFrames(child);
                    if (frames?.Count > 0)
                    {
                        _stageNumberFrames[stage] = frames;
                    }
                }
            }

            _assetsLoaded = true;
        }

        private Texture2D LoadCanvasTexture(WzCanvasProperty canvas)
        {
            if (_device == null || canvas == null)
            {
                return null;
            }

            using var bitmap = canvas.GetLinkedWzCanvasBitmap();
            return bitmap?.ToTexture2DAndDispose(_device);
        }

        private List<DojoFrame> LoadAnimationFrames(WzImageProperty root)
        {
            if (_device == null || root == null)
            {
                return null;
            }

            var frames = new List<DojoFrame>();
            foreach (WzImageProperty child in root.WzProperties.OrderBy(ParseFrameOrder))
            {
                if (WzInfoTools.GetRealProperty(child) is not WzCanvasProperty canvas)
                {
                    continue;
                }

                try
                {
                    using var bitmap = canvas.GetLinkedWzCanvasBitmap();
                    Texture2D texture = bitmap?.ToTexture2DAndDispose(_device);
                    if (texture == null)
                    {
                        continue;
                    }

                    WzVectorProperty origin = canvas["origin"] as WzVectorProperty;
                    frames.Add(new DojoFrame(
                        texture,
                        new Point(origin?.X.Value ?? 0, origin?.Y.Value ?? 0),
                        Math.Max(1, canvas["delay"]?.GetInt() ?? 100)));
                }
                catch
                {
                    // Keep partially available animation sets usable.
                }
            }

            return frames.Count > 0 ? frames : null;
        }

        private void DrawClock(SpriteBatch spriteBatch, Viewport viewport, Texture2D pixelTexture, SpriteFont font)
        {
            Vector2 clockAnchor = new(viewport.Width / 2f, ClockOffsetY);
            DrawTextureAtOrigin(spriteBatch, _clockTexture, clockAnchor, ClockOrigin);

            Vector2 timerOrigin = new((viewport.Width / 2f) + TimerLayerOffsetX, TimerLayerY);
            bool drewDigits = TryDrawBitmapTimer(spriteBatch, timerOrigin);
            if (!drewDigits && font != null)
            {
                string timerText = FormatTimer(RemainingSeconds);
                spriteBatch.DrawString(font, timerText, timerOrigin, Color.White);
            }
        }

        private void DrawGaugeBars(SpriteBatch spriteBatch, Viewport viewport, Texture2D pixelTexture)
        {
            Vector2 playerAnchor = new((viewport.Width / 2f) + PlayerOffsetX, PlayerOffsetY);
            Rectangle playerBounds = DrawTextureAtOrigin(spriteBatch, _playerTexture, playerAnchor, PlayerOrigin);
            Rectangle playerGaugeBounds = new(
                playerBounds.X + PlayerGaugeOffsetX,
                playerBounds.Y + BarGaugeOffsetY,
                BarGaugeWidth,
                BarGaugeHeight);
            DrawHorizontalGauge(spriteBatch, pixelTexture, _playerGaugeTexture, playerGaugeBounds, _playerMaxHp > 0 ? (float)_playerHp / _playerMaxHp : 0f);

            Vector2 monsterAnchor = new((viewport.Width / 2f) + MonsterOffsetX, MonsterOffsetY);
            Rectangle monsterBounds = DrawTextureAtOrigin(spriteBatch, _monsterTexture, monsterAnchor, MonsterOrigin);
            Rectangle monsterGaugeBounds = new(
                monsterBounds.X + MonsterGaugeOffsetX,
                monsterBounds.Y + BarGaugeOffsetY,
                BarGaugeWidth,
                BarGaugeHeight);
            DrawHorizontalGauge(spriteBatch, pixelTexture, _monsterGaugeTexture, monsterGaugeBounds, _bossHpPercent ?? 0f);
        }

        private void DrawEnergy(SpriteBatch spriteBatch, Viewport viewport, Texture2D pixelTexture)
        {
            Vector2 energyAnchor = new(EnergyOffsetX, EnergyOffsetY);
            Rectangle energyBounds = DrawTextureAtTopLeft(spriteBatch, _energyTexture, energyAnchor);
            if (_energy >= EnergyMax)
            {
                DrawAnimation(spriteBatch, _energyFullFrames, Environment.TickCount, int.MaxValue, new Vector2(9f, 80f), repeat: true);
                return;
            }

            Rectangle energyGaugeBounds = new(
                energyBounds.X + EnergyGaugeOffsetX,
                energyBounds.Y + EnergyGaugeOffsetY,
                EnergyGaugeWidth,
                EnergyGaugeHeight);
            DrawVerticalGauge(spriteBatch, pixelTexture, _energyGaugeTexture, energyGaugeBounds, (float)_energy / EnergyMax);
        }

        private void DrawStageBanner(SpriteBatch spriteBatch, Viewport viewport, SpriteFont font, int currentTimeMs)
        {
            if (_stageBannerStartTick == int.MinValue)
            {
                return;
            }

            Vector2 center = new(viewport.Width / 2f, 200f);
            bool drewStage = DrawAnimation(spriteBatch, _stageFrames, currentTimeMs, _stageBannerStartTick, center, repeat: false);
            bool drewNumber = DrawAnimation(spriteBatch, ResolveStageNumberFrames(), currentTimeMs, _stageBannerStartTick, center, repeat: false);
            if (!drewStage && !drewNumber && font != null)
            {
                string stageText = _stage >= 0 ? $"Mu Lung Dojo Floor {_stage}" : "Mu Lung Dojo";
                Vector2 size = font.MeasureString(stageText);
                spriteBatch.DrawString(font, stageText, new Vector2(center.X - (size.X / 2f), center.Y - (size.Y / 2f)), Color.White);
            }
        }

        private void DrawResultEffect(SpriteBatch spriteBatch, Viewport viewport, SpriteFont font, int currentTimeMs)
        {
            if (_resultEffect == DojoResultEffect.None || _resultEffectStartTick == int.MinValue)
            {
                return;
            }

            List<DojoFrame> frames = _resultEffect == DojoResultEffect.Clear ? _clearFrames : _timeOverFrames;
            bool drew = DrawAnimation(spriteBatch, frames, currentTimeMs, _resultEffectStartTick, new Vector2(viewport.Width / 2f, viewport.Height / 2f), repeat: false);
            if (!drew && font != null)
            {
                string text = _resultEffect == DojoResultEffect.Clear ? "Stage Clear" : "Time Over";
                Vector2 size = font.MeasureString(text);
                spriteBatch.DrawString(font, text, new Vector2((viewport.Width - size.X) / 2f, (viewport.Height - size.Y) / 2f), Color.White);
            }
        }

        private bool TryDrawBitmapTimer(SpriteBatch spriteBatch, Vector2 timerOrigin)
        {
            if (_timerColonTexture == null || _digitTextures.Any(texture => texture == null))
            {
                return false;
            }

            int minutes = Math.Clamp(RemainingSeconds / 60, 0, 99);
            int seconds = Math.Clamp(RemainingSeconds % 60, 0, 59);
            DrawTwoDigits(spriteBatch, timerOrigin, TimerMinuteX, TimerDigitY, minutes);
            DrawTwoDigits(spriteBatch, timerOrigin, TimerSecondX, TimerDigitY, seconds);
            spriteBatch.Draw(_timerColonTexture, new Vector2(timerOrigin.X + TimerColonX, timerOrigin.Y + TimerColonY), Color.White);
            return true;
        }

        private void DrawTwoDigits(SpriteBatch spriteBatch, Vector2 timerOrigin, int x, int y, int value)
        {
            int tens = (value / 10) % 10;
            int ones = value % 10;
            spriteBatch.Draw(_digitTextures[tens], new Vector2(timerOrigin.X + x, timerOrigin.Y + y), Color.White);
            spriteBatch.Draw(_digitTextures[ones], new Vector2(timerOrigin.X + x + TimerDigitSpacing, timerOrigin.Y + y), Color.White);
        }

        private Rectangle DrawTextureAtOrigin(SpriteBatch spriteBatch, Texture2D texture, Vector2 anchor, Point origin)
        {
            if (texture == null)
            {
                return Rectangle.Empty;
            }

            Rectangle bounds = new(
                (int)MathF.Round(anchor.X - origin.X),
                (int)MathF.Round(anchor.Y - origin.Y),
                texture.Width,
                texture.Height);
            spriteBatch.Draw(texture, new Vector2(bounds.X, bounds.Y), Color.White);
            return bounds;
        }

        private Rectangle DrawTextureAtTopLeft(SpriteBatch spriteBatch, Texture2D texture, Vector2 topLeft)
        {
            if (texture == null)
            {
                return Rectangle.Empty;
            }

            Rectangle bounds = new((int)MathF.Round(topLeft.X), (int)MathF.Round(topLeft.Y), texture.Width, texture.Height);
            spriteBatch.Draw(texture, new Vector2(bounds.X, bounds.Y), Color.White);
            return bounds;
        }

        private static void DrawHorizontalGauge(SpriteBatch spriteBatch, Texture2D pixelTexture, Texture2D gaugeTexture, Rectangle bounds, float progress)
        {
            int fillWidth = Math.Clamp((int)MathF.Round(bounds.Width * Math.Clamp(progress, 0f, 1f)), 0, bounds.Width);
            if (fillWidth <= 0)
            {
                return;
            }

            Texture2D source = gaugeTexture ?? pixelTexture;
            if (source == null)
            {
                return;
            }

            spriteBatch.Draw(source, new Rectangle(bounds.X, bounds.Y, fillWidth, bounds.Height), Color.White);
        }

        private static void DrawVerticalGauge(SpriteBatch spriteBatch, Texture2D pixelTexture, Texture2D gaugeTexture, Rectangle bounds, float progress)
        {
            int fillHeight = Math.Clamp((int)MathF.Round(bounds.Height * Math.Clamp(progress, 0f, 1f)), 0, bounds.Height);
            if (fillHeight <= 0)
            {
                return;
            }

            Texture2D source = gaugeTexture ?? pixelTexture;
            if (source == null)
            {
                return;
            }

            Rectangle dest = new(bounds.X, bounds.Bottom - fillHeight, bounds.Width, fillHeight);
            spriteBatch.Draw(source, dest, Color.White);
        }

        private bool DrawAnimation(SpriteBatch spriteBatch, IReadOnlyList<DojoFrame> frames, int currentTimeMs, int startTick, Vector2 anchor, bool repeat)
        {
            if (frames == null || frames.Count == 0 || startTick == int.MinValue)
            {
                return false;
            }

            DojoFrame frame = ResolveAnimationFrame(frames, currentTimeMs, startTick, repeat);
            if (frame.Texture == null)
            {
                return false;
            }

            Vector2 drawPos = new(anchor.X - frame.Origin.X, anchor.Y - frame.Origin.Y);
            spriteBatch.Draw(frame.Texture, drawPos, Color.White);
            return true;
        }

        private static DojoFrame ResolveAnimationFrame(IReadOnlyList<DojoFrame> frames, int currentTimeMs, int startTick, bool repeat)
        {
            if (frames == null || frames.Count == 0)
            {
                return default;
            }

            long elapsed = Math.Max(0, currentTimeMs - startTick);
            int totalDuration = frames.Sum(frame => Math.Max(1, frame.Delay));
            if (repeat && totalDuration > 0)
            {
                elapsed %= totalDuration;
            }

            int cursor = 0;
            foreach (DojoFrame frame in frames)
            {
                cursor += Math.Max(1, frame.Delay);
                if (elapsed < cursor)
                {
                    return frame;
                }
            }

            return frames[^1];
        }

        private List<DojoFrame> ResolveStageNumberFrames()
        {
            if (_stage >= 0 && _stageNumberFrames.TryGetValue(_stage, out List<DojoFrame> frames))
            {
                return frames;
            }

            return null;
        }

        private static int ParseFrameOrder(WzImageProperty property)
        {
            return int.TryParse(property?.Name, out int order) ? order : int.MaxValue;
        }

        private readonly record struct DojoFrame(Texture2D Texture, Point Origin, int Delay);

        private enum DojoResultEffect
        {
            None,
            Clear,
            TimeOver
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
        private static readonly string[] UiImageNames = { "UIWindow.img", "UIWindow2.img" };

        private bool _isActive;
        private int _mapId;
        private int _durationSec;
        private int _timeOverTick = int.MinValue;
        private int _lastResetTick = int.MinValue;
        private GraphicsDevice _graphicsDevice;
        private bool _assetsLoaded;
        private Texture2D _backgroundTexture;
        private Texture2D _colonTexture;
        private readonly Texture2D[] _digitTextures = new Texture2D[10];

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
            _graphicsDevice = device;
            _assetsLoaded = false;
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

            EnsureAssetsLoaded();

            Rectangle bounds = GetTimerboardBounds(spriteBatch.GraphicsDevice.Viewport);

            float pulse = GetResetPulseStrength();
            float urgency = RemainingSeconds <= 10 ? 1f - (RemainingSeconds / 10f) : 0f;

            if (_backgroundTexture != null)
            {
                spriteBatch.Draw(_backgroundTexture, new Vector2(bounds.X, bounds.Y), Color.White);
                if (pulse > 0f || urgency > 0f)
                {
                    float overlayStrength = Math.Clamp((pulse * 0.25f) + (urgency * 0.15f), 0f, 0.35f);
                    spriteBatch.Draw(pixelTexture, bounds, new Color(255, 233, 158) * overlayStrength);
                }
            }
            else
            {
                Rectangle innerBounds = new(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4);
                Rectangle faceBounds = new(bounds.X + 10, bounds.Y + 10, bounds.Width - 20, bounds.Height - 20);
                Rectangle dividerBounds = new(bounds.X + DividerX, bounds.Y + DividerY, DividerWidth, DividerHeight);

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
            }

            if (!TryDrawBitmapTimer(spriteBatch, bounds) && font != null)
            {
                string minutesText = (Math.Max(0, RemainingSeconds) / 60).ToString("00");
                string secondsText = (Math.Max(0, RemainingSeconds) % 60).ToString("00");
                Color timeColor = RemainingSeconds <= 10 ? new Color(255, 229, 177) : Color.White;

                DrawDigitString(spriteBatch, font, minutesText, new Vector2(bounds.X + MinuteTextX, bounds.Y + TextY), timeColor);
                DrawDigitString(spriteBatch, font, secondsText, new Vector2(bounds.X + SecondTextX, bounds.Y + TextY), timeColor);
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

        private bool TryDrawBitmapTimer(SpriteBatch spriteBatch, Rectangle bounds)
        {
            if (_digitTextures.Any(texture => texture == null))
            {
                return false;
            }

            int minutes = Math.Clamp(RemainingSeconds / 60, 0, 99);
            int seconds = Math.Clamp(RemainingSeconds % 60, 0, 59);

            DrawTwoDigits(spriteBatch, bounds, MinuteTextX, TextY, minutes);
            DrawTwoDigits(spriteBatch, bounds, SecondTextX, TextY, seconds);

            if (_colonTexture != null)
            {
                spriteBatch.Draw(_colonTexture, new Vector2(bounds.X + DividerX, bounds.Y + DividerY), Color.White);
            }

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

            foreach (string imageName in UiImageNames)
            {
                WzImage uiImage = global::HaCreator.Program.FindImage("UI", imageName);
                if (uiImage?.WzProperties == null)
                {
                    continue;
                }

                if (_backgroundTexture == null && TryFindSpaceGagaBoardCanvas(uiImage, out WzCanvasProperty boardCanvas))
                {
                    _backgroundTexture = LoadCanvasTexture(boardCanvas);
                }

                if (_digitTextures.Any(texture => texture == null) && TryFindSpaceGagaDigitContainer(uiImage, out WzImageProperty digitContainer))
                {
                    LoadDigitTextures(digitContainer);
                }

                if (_backgroundTexture != null && _digitTextures.All(texture => texture != null))
                {
                    break;
                }
            }

            _assetsLoaded = true;
        }

        private bool TryFindSpaceGagaBoardCanvas(WzImage image, out WzCanvasProperty boardCanvas)
        {
            boardCanvas = null;
            foreach (WzImageProperty property in EnumeratePropertiesDepthFirst(image))
            {
                if (property is not WzCanvasProperty canvas)
                {
                    continue;
                }

                using var bitmap = canvas.GetLinkedWzCanvasBitmap();
                if (bitmap == null || bitmap.Width != TimerboardWidth || bitmap.Height != TimerboardHeight)
                {
                    continue;
                }

                boardCanvas = canvas;
                return true;
            }

            return false;
        }

        private bool TryFindSpaceGagaDigitContainer(WzImage image, out WzImageProperty digitContainer)
        {
            digitContainer = null;
            foreach (WzImageProperty property in EnumeratePropertiesDepthFirst(image))
            {
                if (!LooksLikeClockDigitContainer(property))
                {
                    continue;
                }

                digitContainer = property;
                return true;
            }

            return false;
        }

        private static IEnumerable<WzImageProperty> EnumeratePropertiesDepthFirst(IPropertyContainer container)
        {
            if (container?.WzProperties == null)
            {
                yield break;
            }

            foreach (WzImageProperty child in container.WzProperties)
            {
                yield return child;
                if (child is IPropertyContainer childContainer)
                {
                    foreach (WzImageProperty descendant in EnumeratePropertiesDepthFirst(childContainer))
                    {
                        yield return descendant;
                    }
                }
            }
        }

        private static bool LooksLikeClockDigitContainer(WzImageProperty property)
        {
            if (property?.WzProperties == null)
            {
                return false;
            }

            for (int i = 0; i < 10; i++)
            {
                if (ResolveCanvas(property[i.ToString()]) == null)
                {
                    return false;
                }
            }

            return ResolveCanvas(property["bar"]) != null
                || ResolveCanvas(property["colon"]) != null
                || ResolveCanvas(property["comma"]) != null;
        }

        private void LoadDigitTextures(WzImageProperty digitContainer)
        {
            for (int i = 0; i < _digitTextures.Length; i++)
            {
                _digitTextures[i] ??= LoadCanvasTexture(ResolveCanvas(digitContainer?[i.ToString()]));
            }

            _colonTexture ??= LoadCanvasTexture(
                ResolveCanvas(digitContainer?["bar"])
                ?? ResolveCanvas(digitContainer?["colon"])
                ?? ResolveCanvas(digitContainer?["comma"]));
        }

        private static WzCanvasProperty ResolveCanvas(WzImageProperty property)
        {
            if (property is WzCanvasProperty canvas)
            {
                return canvas;
            }

            if (property?.WzProperties == null)
            {
                return null;
            }

            if (property["0"] is WzCanvasProperty indexedCanvas)
            {
                return indexedCanvas;
            }

            return property.WzProperties.OfType<WzCanvasProperty>().FirstOrDefault();
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
        private static readonly string[] UiImageNames = { "UIWindow2.img", "UIWindow.img" };
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
        private const int GaugeWidth = 262;
        private const int GaugeFillWidth = 259;
        private const int GaugeHeight = 9;
        private const int GaugeOffsetX = -93;
        private const int GaugeY = 78;
        private const int GaugeFillOffsetX = 4;
        private const int GaugeFillOffsetY = 6;
        private const int GaugeLabelOffsetY = -8;
        private const int GaugeFillHeight = 9;
        private const int KeyAnimationX = 7;
        private const int KeyAnimationY = 135;
        private const int ClearEffectDurationMs = 2200;
        private const float DangerDepletionThreshold = 0.65f;
        private const int BonusEffectY = 190;
        private const int ResultBoardY = 115;
        private const int ResultScoreX = 180;
        private const int ResultScoreY = 144;
        private const int ResultRankX = 98;
        private const int ResultRankY = 101;
        private const int ResultBackdrop2X = 245;
        private const int ResultBackdrop2Y = 28;

        private bool _isActive;
        private int _mapId;
        private int _incGauge;
        private int _gaugeDec = 1;
        private int _currentGauge;
        private int _maxGauge = 100;
        private int _defaultGaugeIncrease = 1;
        private int _coolGaugeIncrease;
        private int _missGaugePenalty;
        private int _mapDistance;
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
        private bool _disableSkill;
        private readonly List<MassacreCountEffect> _countEffects = new();
        private string _countEffectBannerText;
        private int _countEffectBannerUntilMs = int.MinValue;
        private GraphicsDevice _device;
        private bool _assetsLoaded;
        private Texture2D _gaugeBackgroundTexture;
        private Texture2D _gaugeTextTexture;
        private Texture2D _gaugePixelTexture;
        private Texture2D _timerboardSourceTexture;
        private Texture2D _timerboardEnabledTexture;
        private Texture2D _timerboardDisabledTexture;
        private readonly Texture2D[] _timerDigits = new Texture2D[10];
        private readonly Texture2D[] _resultDigits = new Texture2D[10];
        private Texture2D _resultPlusTexture;
        private Texture2D _resultBoardTexture;
        private readonly Dictionary<char, MassacreCanvasFrame> _rankTextures = new();
        private List<MassacreCanvasFrame> _keyOpenFrames;
        private List<MassacreCanvasFrame> _keyLoopFrames;
        private List<MassacreCanvasFrame> _keyCloseFrames;
        private List<MassacreCanvasFrame> _dangerFrames;
        private List<MassacreCanvasFrame> _dangerIconFrames;
        private List<MassacreCanvasFrame> _dangerTextFrames;
        private List<MassacreCanvasFrame> _dangerBackgroundFrames;
        private List<MassacreCanvasFrame> _bonusStageFrames;
        private List<MassacreCanvasFrame> _bonusFrames;
        private List<MassacreCanvasFrame> _resultClearFrames;
        private List<MassacreCanvasFrame> _resultFailFrames;
        private List<MassacreCanvasFrame> _resultBoardPulseFrames;
        private int _bonusPresentationStartTick = int.MinValue;
        private int _resultPresentationStartTick = int.MinValue;
        private MassacreResultPresentation _resultPresentation = MassacreResultPresentation.None;
        private char _resultRank = 'D';
        private int _resultScore;

        public bool IsActive => _isActive;
        public int CurrentGauge => _currentGauge;
        public int MaxGauge => _maxGauge;
        public int GaugeDecreasePerSecond => _gaugeDec;
        public int DefaultGaugeIncrease => _defaultGaugeIncrease;
        public bool IsSkillDisabled => _disableSkill;
        public bool HasKeyAnimation => _keyAnimationStage >= 0;
        public int KillCount => _killCount;
        public int ComboCount => _comboCount;
        public int TimerRemain => RemainingSeconds;
        public bool HasBonusPresentation => _bonusPresentationStartTick != int.MinValue;
        public bool HasResultPresentation => _resultPresentation != MassacreResultPresentation.None;
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

        public void Configure(MapInfo mapInfo)
        {
            if (!_isActive || mapInfo == null)
            {
                return;
            }

            for (int i = 0; i < mapInfo.additionalNonInfoProps.Count; i++)
            {
                if (mapInfo.additionalNonInfoProps[i] is not WzSubProperty massacre
                    || !string.Equals(massacre.Name, "mobMassacre", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _mapDistance = Math.Max(0, InfoTool.GetOptionalInt(massacre["mapDistance"]) ?? _mapDistance);
                _disableSkill = (InfoTool.GetOptionalInt(massacre["disableSkill"]) ?? 0) != 0;

                if (massacre["gauge"] is WzSubProperty gauge)
                {
                    _maxGauge = Math.Max(1, InfoTool.GetOptionalInt(gauge["total"]) ?? _maxGauge);
                    _gaugeDec = Math.Max(0, InfoTool.GetOptionalInt(gauge["decrease"]) ?? _gaugeDec);
                    _defaultGaugeIncrease = Math.Max(0, InfoTool.GetOptionalInt(gauge["hitAdd"]) ?? _defaultGaugeIncrease);
                    _coolGaugeIncrease = Math.Max(0, InfoTool.GetOptionalInt(gauge["coolAdd"]) ?? _coolGaugeIncrease);
                    _missGaugePenalty = Math.Max(0, InfoTool.GetOptionalInt(gauge["missSub"]) ?? _missGaugePenalty);
                }

                _countEffects.Clear();
                if (massacre["countEffect"] is WzSubProperty countEffect)
                {
                    foreach (WzImageProperty child in countEffect.WzProperties)
                    {
                        if (!int.TryParse(child.Name, out int threshold)
                            || child is not WzSubProperty thresholdProperty)
                        {
                            continue;
                        }

                        _countEffects.Add(new MassacreCountEffect(
                            threshold,
                            InfoTool.GetOptionalInt(thresholdProperty["buff"]),
                            (InfoTool.GetOptionalInt(thresholdProperty["skillUse"]) ?? 0) != 0));
                    }

                    _countEffects.Sort(static (left, right) => left.Threshold.CompareTo(right.Threshold));
                }

                _currentGauge = Math.Clamp(_currentGauge, 0, _maxGauge);
                _displayGauge = Math.Clamp(_displayGauge, 0f, _maxGauge);
                if (_disableSkill)
                {
                    _keyAnimationStage = -1;
                    _keyAnimationStageStart = int.MinValue;
                    _keyAnimationQueued = false;
                }

                return;
            }
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
            _countEffectBannerText = null;
            _countEffectBannerUntilMs = int.MinValue;
            _bonusPresentationStartTick = int.MinValue;
            _resultPresentationStartTick = int.MinValue;
            _resultPresentation = MassacreResultPresentation.None;
            _resultRank = 'D';
            _resultScore = 0;
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

            if (_bonusPresentationStartTick != int.MinValue
                && !IsAnimationPlaying(_bonusStageFrames, currentTimeMs, _bonusPresentationStartTick, repeat: false)
                && !IsAnimationPlaying(_bonusFrames, currentTimeMs, _bonusPresentationStartTick, repeat: false))
            {
                _bonusPresentationStartTick = int.MinValue;
            }
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (!_isActive || pixelTexture == null)
            {
                return;
            }

            EnsureAssetsLoaded();
            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            DrawTimerboard(spriteBatch, pixelTexture, font, viewport);
            DrawGaugeHud(spriteBatch, pixelTexture, font, viewport);
            DrawKeyAnimation(spriteBatch, pixelTexture, font);
            DrawBonusPresentation(spriteBatch, font, viewport, Environment.TickCount);
            DrawResultPresentation(spriteBatch, font, viewport, Environment.TickCount);
            DrawClearEffect(spriteBatch, pixelTexture, font, viewport);
        }

        public string DescribeStatus()
        {
            if (!_isActive)
            {
                return "Massacre HUD inactive";
            }

            string timerText = HasRunningTimerboard ? FormatTimer(RemainingSeconds) : "stopped";
            string nextCountEffect = GetNextCountEffectThreshold() is int threshold
                ? $", nextCountEffect={threshold}"
                : string.Empty;
            string disableSkillText = _disableSkill ? ", skills=disabled" : string.Empty;
            string bonusText = HasBonusPresentation ? ", bonusFx=active" : string.Empty;
            string resultText = HasResultPresentation ? $", result={_resultPresentation}:{_resultRank}:{_resultScore}" : string.Empty;
            return $"Massacre map {_mapId}, timer={timerText}, gauge={_currentGauge}/{_maxGauge}, inc={_incGauge}, hitAdd={_defaultGaugeIncrease}, decay={_gaugeDec}/s, kills={_killCount}, combo={_comboCount}{disableSkillText}{nextCountEffect}{bonusText}{resultText}";
        }

        public void Reset()
        {
            _isActive = false;
            _mapId = 0;
            _maxGauge = 100;
            _gaugeDec = 1;
            _defaultGaugeIncrease = 1;
            _coolGaugeIncrease = 0;
            _missGaugePenalty = 0;
            _mapDistance = 0;
            _disableSkill = false;
            _countEffects.Clear();
            _assetsLoaded = false;
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
            UpdateCountEffectBanner(currentTimeMs);
        }

        private void QueueKeyAnimation()
        {
            if (_disableSkill)
            {
                return;
            }

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

            List<MassacreCanvasFrame> frames = GetKeyFramesForStage(_keyAnimationStage);
            if (frames == null || IsAnimationPlaying(frames, currentTimeMs, _keyAnimationStageStart, repeat: false))
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
            Texture2D timerboardTexture = _timerboardSourceTexture
                ?? (_disableSkill ? _timerboardDisabledTexture : _timerboardEnabledTexture);
            if (timerboardTexture != null)
            {
                spriteBatch.Draw(timerboardTexture, new Vector2(bounds.X, bounds.Y), Color.White);
            }
            else
            {
                spriteBatch.Draw(pixelTexture, bounds, new Color(18, 21, 24, 228));
                spriteBatch.Draw(pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), new Color(95, 127, 160, 255));
                spriteBatch.Draw(pixelTexture, new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2), new Color(34, 45, 58, 255));

                Rectangle divider = new(bounds.X + TimerDividerX, bounds.Y + TimerDividerY, TimerDividerWidth, TimerDividerHeight);
                spriteBatch.Draw(pixelTexture, divider, new Color(44, 53, 66, 255));
            }

            int remaining = RemainingSeconds;
            string minuteText = $"{remaining / 60:00}";
            string secondText = $"{remaining % 60:00}";
            Color timeColor = remaining <= 10 ? new Color(255, 170, 120) : new Color(232, 242, 255);

            if (!TryDrawBitmapDigits(spriteBatch, _timerDigits, minuteText, new Vector2(bounds.X + TimerMinuteTextX, bounds.Y + TimerTextY))
                || !TryDrawBitmapDigits(spriteBatch, _timerDigits, secondText, new Vector2(bounds.X + TimerSecondTextX, bounds.Y + TimerTextY)))
            {
                if (font == null)
                {
                    return;
                }

                DrawDigitString(spriteBatch, font, minuteText, new Vector2(bounds.X + TimerMinuteTextX, bounds.Y + TimerTextY), timeColor);
                DrawDigitString(spriteBatch, font, secondText, new Vector2(bounds.X + TimerSecondTextX, bounds.Y + TimerTextY), timeColor);
            }
        }

        private void DrawGaugeHud(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, Viewport viewport)
        {
            int gaugeX = viewport.Width / 2 + GaugeOffsetX;
            Rectangle fillBounds = new(gaugeX + GaugeFillOffsetX, GaugeY + GaugeFillOffsetY, GaugeFillWidth, GaugeFillHeight);

            if (_gaugeBackgroundTexture != null)
            {
                spriteBatch.Draw(_gaugeBackgroundTexture, new Vector2(gaugeX, GaugeY), Color.White);
            }
            else
            {
                Rectangle fallbackBounds = new(gaugeX, GaugeY, GaugeWidth + (GaugeFillOffsetX * 2), GaugeHeight + GaugeFillOffsetY);
                spriteBatch.Draw(pixelTexture, fallbackBounds, new Color(25, 18, 16, 224));
            }

            int fillWidth = Math.Clamp((int)MathF.Round(fillBounds.Width * GaugeProgress), 0, fillBounds.Width);
            if (fillWidth > 0)
            {
                Texture2D gaugeFillTexture = _gaugePixelTexture ?? pixelTexture;
                Color fillColor = _gaugePixelTexture != null ? Color.White : GetGaugeColor(GaugeProgress);
                spriteBatch.Draw(gaugeFillTexture, new Rectangle(fillBounds.X, fillBounds.Y, fillWidth, fillBounds.Height), fillColor);
            }

            if (ShouldDrawDangerOverlay())
            {
                DrawAnimation(spriteBatch, _dangerBackgroundFrames, Environment.TickCount, 0, new Vector2(fillBounds.X, fillBounds.Y), repeat: true);
                DrawAnimation(spriteBatch, _dangerFrames, Environment.TickCount, 0, new Vector2(fillBounds.Right - 115f, GaugeY - 2f), repeat: true);
                DrawAnimation(spriteBatch, _dangerTextFrames, Environment.TickCount, 0, new Vector2(gaugeX - 3f, GaugeY - 3f), repeat: true);
                DrawAnimation(spriteBatch, _dangerIconFrames, Environment.TickCount, 0, new Vector2(gaugeX + 214f, GaugeY - 5f), repeat: true);
            }
            else if (_gaugeTextTexture != null)
            {
                spriteBatch.Draw(_gaugeTextTexture, new Vector2(gaugeX, GaugeY + GaugeLabelOffsetY), Color.White);
            }

            if (font == null)
            {
                return;
            }

            string gaugeText = $"{_currentGauge}/{_maxGauge}";
            string statusText = _comboCount > 1 ? $"{_comboCount}x combo" : $"{_killCount} kills";
            Color statusColor = _comboCount >= 10 ? Color.Gold : _comboCount >= 5 ? Color.Orange : new Color(238, 220, 191);
            string nextThresholdText = GetNextCountEffectThreshold() is int threshold
                ? $"next {threshold}"
                : null;

            Vector2 gaugeTextPos = new(gaugeX + 180, GaugeY + 14);
            spriteBatch.DrawString(font, gaugeText, gaugeTextPos + Vector2.One, Color.Black);
            spriteBatch.DrawString(font, gaugeText, gaugeTextPos, Color.White);

            Vector2 infoPos = new(gaugeX + 10, GaugeY + 18);
            spriteBatch.DrawString(font, statusText, infoPos + Vector2.One, Color.Black);
            spriteBatch.DrawString(font, statusText, infoPos, statusColor);
            if (!string.IsNullOrWhiteSpace(nextThresholdText))
            {
                Vector2 nextThresholdSize = font.MeasureString(nextThresholdText);
                Vector2 nextPos = new(gaugeX + GaugeWidth - nextThresholdSize.X, GaugeY + 18);
                spriteBatch.DrawString(font, nextThresholdText, nextPos + Vector2.One, Color.Black);
                spriteBatch.DrawString(font, nextThresholdText, nextPos, new Color(214, 197, 166));
            }

            if (!string.IsNullOrWhiteSpace(_countEffectBannerText) && Environment.TickCount < _countEffectBannerUntilMs)
            {
                Vector2 bannerSize = font.MeasureString(_countEffectBannerText);
                Vector2 bannerPos = new((viewport.Width - bannerSize.X) / 2f, GaugeY + 34);
                spriteBatch.DrawString(font, _countEffectBannerText, bannerPos + Vector2.One, Color.Black);
                spriteBatch.DrawString(font, _countEffectBannerText, bannerPos, new Color(255, 223, 132));
            }
        }

        private void DrawKeyAnimation(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (_keyAnimationStage < 0)
            {
                return;
            }

            List<MassacreCanvasFrame> frames = GetKeyFramesForStage(_keyAnimationStage);
            if (!DrawAnimation(spriteBatch, frames, Environment.TickCount, _keyAnimationStageStart, new Vector2(KeyAnimationX, KeyAnimationY), repeat: false) && font != null)
            {
                string text = _keyAnimationStage switch
                {
                    0 => "KEY",
                    1 => "KEY!",
                    _ => "KEY!!"
                };
                spriteBatch.DrawString(font, text, new Vector2(KeyAnimationX, KeyAnimationY), Color.White);
            }
        }

        private void DrawClearEffect(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, Viewport viewport)
        {
            if (!_clearEffectActive || HasResultPresentation)
            {
                return;
            }

            spriteBatch.Draw(pixelTexture, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.White * (0.16f * _clearEffectAlpha));
            Vector2 center = new(viewport.Width / 2f, viewport.Height / 2f);
            if (!DrawAnimation(spriteBatch, _resultClearFrames, Environment.TickCount, _clearEffectStartTime, center, repeat: false)
                && font != null)
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

        private int? GetNextCountEffectThreshold()
        {
            for (int i = 0; i < _countEffects.Count; i++)
            {
                if (_killCount < _countEffects[i].Threshold)
                {
                    return _countEffects[i].Threshold;
                }
            }

            return null;
        }

        private void UpdateCountEffectBanner(int currentTimeMs)
        {
            for (int i = 0; i < _countEffects.Count; i++)
            {
                if (_countEffects[i].Threshold != _killCount)
                {
                    continue;
                }

                string effectText = _countEffects[i].RequiresSkillUse ? " skill" : " buff";
                _countEffectBannerText = $"{_countEffects[i].Threshold} kills{effectText}";
                _countEffectBannerUntilMs = currentTimeMs + 1800;
                _bonusPresentationStartTick = currentTimeMs;
                return;
            }
        }

        public void ShowResultPresentation(bool clear, int currentTimeMs, int? scoreOverride = null, char? rankOverride = null)
        {
            _resultPresentation = clear ? MassacreResultPresentation.Clear : MassacreResultPresentation.Fail;
            _resultPresentationStartTick = currentTimeMs;
            _resultScore = Math.Max(0, scoreOverride ?? _killCount);
            _resultRank = NormalizeRank(rankOverride ?? ComputeResultRank());
        }

        public void ShowBonusPresentation(int currentTimeMs)
        {
            _bonusPresentationStartTick = currentTimeMs;
        }

        private void EnsureAssetsLoaded()
        {
            if (_assetsLoaded || _device == null)
            {
                return;
            }

            WzImage uiWindow = null;
            foreach (string imageName in UiImageNames)
            {
                WzImage uiImage = global::HaCreator.Program.FindImage("UI", imageName);
                if (uiImage?.WzProperties == null)
                {
                    continue;
                }

                uiWindow ??= uiImage;
                _timerboardSourceTexture ??= LoadCanvasTexture(FindTimerboardSourceCanvas(uiImage));
            }

            WzImage effectImage = global::HaCreator.Program.FindImage("Map", "Effect.img")
                ?? global::HaCreator.Program.FindImage("Map", "effect.img");

            WzImageProperty monsterKilling = uiWindow?["MonsterKilling"];
            WzImageProperty count = monsterKilling?["Count"];
            WzImageProperty gauge = monsterKilling?["Gauge"];
            WzImageProperty result = monsterKilling?["Result"];

            _timerboardDisabledTexture = LoadCanvasTexture(count?["backgrd0"] as WzCanvasProperty);
            _timerboardEnabledTexture = LoadCanvasTexture(count?["backgrd1"] as WzCanvasProperty);
            LoadDigitTextures(count?["number"], _timerDigits);

            _gaugeBackgroundTexture = LoadCanvasTexture(gauge?["backgrd"] as WzCanvasProperty);
            _gaugeTextTexture = LoadCanvasTexture(gauge?["text"] as WzCanvasProperty);
            _gaugePixelTexture = LoadCanvasTexture(gauge?["pixel"] as WzCanvasProperty);
            _dangerFrames = LoadAnimationFrames(gauge?["danger"]);
            _dangerIconFrames = LoadAnimationFrames(gauge?["iconD"]);
            _dangerTextFrames = LoadAnimationFrames(gauge?["textD"]);
            _dangerBackgroundFrames = LoadAnimationFrames(gauge?["backgrdD"]);

            _keyOpenFrames = LoadAnimationFrames(count?["keyBackgrd"]?["open"]);
            _keyLoopFrames = LoadAnimationFrames(count?["keyBackgrd"]?["ing"]);
            _keyCloseFrames = LoadAnimationFrames(count?["keyBackgrd"]?["close"]);

            _resultBoardTexture = LoadCanvasTexture(result?["backgrd"] as WzCanvasProperty);
            _resultBoardPulseFrames = LoadAnimationFrames(result?["backgrd2"]);
            LoadDigitTextures(result?["number2"], _resultDigits, out _resultPlusTexture);
            LoadRankTextures(result?["Rank"]);

            WzImageProperty killing = effectImage?["killing"];
            _resultClearFrames = LoadAnimationFrames(killing?["clear"]);
            _resultFailFrames = LoadAnimationFrames(killing?["fail"]);
            _bonusStageFrames = LoadAnimationFrames(killing?["bonus"]?["stage"]);
            _bonusFrames = LoadAnimationFrames(killing?["bonus"]?["bonus"]);
            _assetsLoaded = true;
        }

        private void DrawBonusPresentation(SpriteBatch spriteBatch, SpriteFont font, Viewport viewport, int currentTimeMs)
        {
            if (_bonusPresentationStartTick == int.MinValue)
            {
                return;
            }

            Vector2 anchor = new(viewport.Width / 2f, BonusEffectY);
            bool drewStage = DrawAnimation(spriteBatch, _bonusStageFrames, currentTimeMs, _bonusPresentationStartTick, anchor, repeat: false);
            bool drewBonus = DrawAnimation(spriteBatch, _bonusFrames, currentTimeMs, _bonusPresentationStartTick, anchor, repeat: false);
            if (!drewStage && !drewBonus && font != null)
            {
                const string text = "BONUS";
                Vector2 textSize = font.MeasureString(text);
                Vector2 drawPos = new((viewport.Width - textSize.X) / 2f, BonusEffectY);
                spriteBatch.DrawString(font, text, drawPos + Vector2.One, Color.Black);
                spriteBatch.DrawString(font, text, drawPos, new Color(255, 223, 132));
            }
        }

        private void DrawResultPresentation(SpriteBatch spriteBatch, SpriteFont font, Viewport viewport, int currentTimeMs)
        {
            if (_resultPresentation == MassacreResultPresentation.None || _resultPresentationStartTick == int.MinValue)
            {
                return;
            }

            Vector2 center = new(viewport.Width / 2f, viewport.Height / 2f);
            DrawAnimation(
                spriteBatch,
                _resultPresentation == MassacreResultPresentation.Clear ? _resultClearFrames : _resultFailFrames,
                currentTimeMs,
                _resultPresentationStartTick,
                center,
                repeat: false);

            if (_resultBoardTexture != null)
            {
                Vector2 boardPos = new((viewport.Width - _resultBoardTexture.Width) / 2f, ResultBoardY);
                spriteBatch.Draw(_resultBoardTexture, boardPos, Color.White);
                DrawAnimation(spriteBatch, _resultBoardPulseFrames, currentTimeMs, _resultPresentationStartTick, boardPos + new Vector2(ResultBackdrop2X, ResultBackdrop2Y), repeat: false);
                DrawResultRank(spriteBatch, boardPos + new Vector2(ResultRankX, ResultRankY));
                DrawBitmapNumber(spriteBatch, _resultDigits, _resultScore.ToString(), boardPos + new Vector2(ResultScoreX, ResultScoreY), _resultPlusTexture, includePlus: false);
                return;
            }

            if (font != null)
            {
                string fallback = $"{_resultPresentation} {_resultScore}";
                Vector2 size = font.MeasureString(fallback);
                Vector2 pos = new((viewport.Width - size.X) / 2f, ResultBoardY);
                spriteBatch.DrawString(font, fallback, pos + Vector2.One, Color.Black);
                spriteBatch.DrawString(font, fallback, pos, Color.White);
            }
        }

        private void DrawResultRank(SpriteBatch spriteBatch, Vector2 topLeft)
        {
            if (_rankTextures.TryGetValue(_resultRank, out MassacreCanvasFrame rankTexture) && rankTexture.Texture != null)
            {
                spriteBatch.Draw(rankTexture.Texture, topLeft, Color.White);
            }
        }

        private void DrawBitmapNumber(SpriteBatch spriteBatch, Texture2D[] digits, string text, Vector2 topLeft, Texture2D specialTexture = null, bool includePlus = false)
        {
            if (digits == null || digits.All(texture => texture == null))
            {
                return;
            }

            float x = topLeft.X;
            if (includePlus && specialTexture != null)
            {
                spriteBatch.Draw(specialTexture, new Vector2(x, topLeft.Y), Color.White);
                x += specialTexture.Width;
            }

            foreach (char digitChar in text)
            {
                if (digitChar is < '0' or > '9')
                {
                    continue;
                }

                Texture2D digitTexture = digits[digitChar - '0'];
                if (digitTexture == null)
                {
                    continue;
                }

                spriteBatch.Draw(digitTexture, new Vector2(x, topLeft.Y), Color.White);
                x += digitTexture.Width;
            }
        }

        private static bool TryDrawBitmapDigits(SpriteBatch spriteBatch, Texture2D[] digits, string text, Vector2 position)
        {
            if (digits == null || text == null || digits.Any(texture => texture == null))
            {
                return false;
            }

            float x = position.X;
            foreach (char digitChar in text)
            {
                if (digitChar is < '0' or > '9')
                {
                    return false;
                }

                Texture2D digitTexture = digits[digitChar - '0'];
                spriteBatch.Draw(digitTexture, new Vector2(x, position.Y), Color.White);
                x += digitTexture.Width;
            }

            return true;
        }

        private static bool IsAnimationPlaying(IReadOnlyList<MassacreCanvasFrame> frames, int currentTimeMs, int startTick, bool repeat)
        {
            if (frames == null || frames.Count == 0 || startTick == int.MinValue)
            {
                return false;
            }

            if (repeat)
            {
                return true;
            }

            int duration = frames.Sum(frame => Math.Max(1, frame.Delay));
            return currentTimeMs - startTick < duration;
        }

        private bool DrawAnimation(SpriteBatch spriteBatch, IReadOnlyList<MassacreCanvasFrame> frames, int currentTimeMs, int startTick, Vector2 anchor, bool repeat)
        {
            if (frames == null || frames.Count == 0 || startTick == int.MinValue)
            {
                return false;
            }

            MassacreCanvasFrame frame = ResolveAnimationFrame(frames, currentTimeMs, startTick, repeat);
            if (frame.Texture == null)
            {
                return false;
            }

            Vector2 drawPos = new(anchor.X - frame.Origin.X, anchor.Y - frame.Origin.Y);
            spriteBatch.Draw(frame.Texture, drawPos, Color.White);
            return true;
        }

        private static MassacreCanvasFrame ResolveAnimationFrame(IReadOnlyList<MassacreCanvasFrame> frames, int currentTimeMs, int startTick, bool repeat)
        {
            if (frames == null || frames.Count == 0)
            {
                return default;
            }

            long elapsed = Math.Max(0, currentTimeMs - startTick);
            int totalDuration = frames.Sum(frame => Math.Max(1, frame.Delay));
            if (repeat && totalDuration > 0)
            {
                elapsed %= totalDuration;
            }

            int cursor = 0;
            foreach (MassacreCanvasFrame frame in frames)
            {
                cursor += Math.Max(1, frame.Delay);
                if (elapsed < cursor)
                {
                    return frame;
                }
            }

            return frames[^1];
        }

        private static MassacreCanvasFrame LoadFrame(WzCanvasProperty canvas, GraphicsDevice device)
        {
            if (device == null || canvas == null)
            {
                return default;
            }

            using var bitmap = canvas.GetLinkedWzCanvasBitmap();
            Texture2D texture = bitmap?.ToTexture2DAndDispose(device);
            if (texture == null)
            {
                return default;
            }

            System.Drawing.PointF origin = canvas.GetCanvasOriginPosition();
            return new MassacreCanvasFrame(
                texture,
                new Point((int)origin.X, (int)origin.Y),
                Math.Max(1, canvas["delay"]?.GetInt() ?? 100));
        }

        private Texture2D LoadCanvasTexture(WzCanvasProperty canvas)
        {
            MassacreCanvasFrame frame = LoadFrame(canvas, _device);
            return frame.Texture;
        }

        private List<MassacreCanvasFrame> LoadAnimationFrames(WzImageProperty root)
        {
            if (_device == null || root?.WzProperties == null)
            {
                return null;
            }

            var frames = new List<MassacreCanvasFrame>();
            foreach (WzImageProperty child in root.WzProperties.OrderBy(ParseFrameOrder))
            {
                WzCanvasProperty canvas = ResolveCanvas(child);
                if (canvas == null)
                {
                    continue;
                }

                MassacreCanvasFrame frame = LoadFrame(canvas, _device);
                if (frame.Texture != null)
                {
                    frames.Add(frame);
                }
            }

            return frames.Count > 0 ? frames : null;
        }

        private static WzCanvasProperty FindTimerboardSourceCanvas(WzImage image)
        {
            foreach (WzImageProperty property in EnumeratePropertiesDepthFirst(image))
            {
                if (property is not WzCanvasProperty canvas)
                {
                    continue;
                }

                if (TryMatchCanvasSize(canvas, TimerboardWidth, TimerboardHeight))
                {
                    return canvas;
                }
            }

            return null;
        }

        private static bool TryMatchCanvasSize(WzCanvasProperty canvas, int width, int height)
        {
            try
            {
                using var bitmap = canvas.GetLinkedWzCanvasBitmap();
                return bitmap?.Width == width && bitmap.Height == height;
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerable<WzImageProperty> EnumeratePropertiesDepthFirst(IPropertyContainer container)
        {
            if (container?.WzProperties == null)
            {
                yield break;
            }

            foreach (WzImageProperty child in container.WzProperties)
            {
                yield return child;
                if (child is IPropertyContainer childContainer)
                {
                    foreach (WzImageProperty descendant in EnumeratePropertiesDepthFirst(childContainer))
                    {
                        yield return descendant;
                    }
                }
            }
        }

        private void LoadDigitTextures(WzImageProperty source, Texture2D[] destination)
        {
            LoadDigitTextures(source, destination, out _);
        }

        private void LoadDigitTextures(WzImageProperty source, Texture2D[] destination, out Texture2D plusTexture)
        {
            plusTexture = null;
            if (destination == null)
            {
                return;
            }

            for (int i = 0; i < destination.Length; i++)
            {
                destination[i] ??= LoadCanvasTexture(ResolveCanvas(source?[i.ToString()]));
            }

            plusTexture = LoadCanvasTexture(ResolveCanvas(source?["plus"]));
        }

        private void LoadRankTextures(WzImageProperty source)
        {
            foreach (char rank in new[] { 'S', 'A', 'B', 'C', 'D' })
            {
                if (_rankTextures.ContainsKey(rank))
                {
                    continue;
                }

                WzCanvasProperty canvas = ResolveCanvas(source?[char.ToLowerInvariant(rank).ToString()]);
                MassacreCanvasFrame frame = LoadFrame(canvas, _device);
                if (frame.Texture != null)
                {
                    _rankTextures[rank] = frame;
                }
            }
        }

        private static WzCanvasProperty ResolveCanvas(WzImageProperty property)
        {
            if (WzInfoTools.GetRealProperty(property) is WzCanvasProperty resolvedCanvas)
            {
                return resolvedCanvas;
            }

            if (property?.WzProperties == null)
            {
                return null;
            }

            if (property["0"] is WzCanvasProperty indexedCanvas)
            {
                return indexedCanvas;
            }

            return property.WzProperties.OfType<WzCanvasProperty>().FirstOrDefault();
        }

        private static int ParseFrameOrder(WzImageProperty property)
        {
            return int.TryParse(property?.Name, out int order) ? order : int.MaxValue;
        }

        private List<MassacreCanvasFrame> GetKeyFramesForStage(int stage)
        {
            return stage switch
            {
                0 => _keyOpenFrames,
                1 => _keyLoopFrames,
                2 => _keyCloseFrames,
                _ => null
            };
        }

        private bool ShouldDrawDangerOverlay()
        {
            if (_maxGauge <= 0)
            {
                return false;
            }

            float depletion = 1f - Math.Clamp(_currentGauge / (float)_maxGauge, 0f, 1f);
            return depletion >= DangerDepletionThreshold;
        }

        private char ComputeResultRank()
        {
            float progress = Math.Clamp(_maxGauge <= 0 ? 0f : _currentGauge / (float)_maxGauge, 0f, 1f);
            return progress switch
            {
                >= 1f => 'S',
                >= 0.8f => 'A',
                >= 0.6f => 'B',
                >= 0.35f => 'C',
                _ => 'D'
            };
        }

        private static char NormalizeRank(char rank)
        {
            return char.ToUpperInvariant(rank) switch
            {
                'S' => 'S',
                'A' => 'A',
                'B' => 'B',
                'C' => 'C',
                _ => 'D'
            };
        }

        private readonly record struct MassacreCanvasFrame(Texture2D Texture, Point Origin, int Delay);
        private readonly record struct MassacreCountEffect(int Threshold, int? BuffItemId, bool RequiresSkillUse);
        private enum MassacreResultPresentation
        {
            None,
            Clear,
            Fail
        }
    }
    #endregion
}
