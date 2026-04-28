using HaSharedLibrary.Render.DX;
using MapleLib.Helpers;
using MapleLib.PacketLib;
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
        internal readonly record struct GuildBossHealerContract(
            int X,
            int YMin,
            int YMax,
            int Rise,
            int Fall,
            int HealMin,
            int HealMax,
            string AnimationPath);

        internal readonly record struct GuildBossPulleyContract(
            int X,
            int Y,
            string AnimationPath);

        internal readonly record struct GuildBossMapContract(
            int MapId,
            GuildBossHealerContract Healer,
            GuildBossPulleyContract Pulley,
            string SourceDescription);

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
        private string _contractSourceDescription;
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
        private int _pulleyRequestInFlightUntil = int.MinValue;
        private int _pulleyRequestInFlightSequence;
        private int _statusCueExpiresAt = int.MinValue;
        private string _statusCueText;
        private LocalPulleySequenceStage _localPulleySequenceStage = LocalPulleySequenceStage.None;
        private PulleyPacketRequest? _pendingPulleyPacketRequest;
        private int _pulleyPacketSequence;
        private List<GuildBossSpriteFrame> _pulleyFrames;
        private int _pulleyFrameIndex = 0;
        private int _lastPulleyFrameTime = 0;
        private int _pulleyHitAnimationUntil = int.MinValue;
        private int _localBasicActionOwnerUntil = int.MinValue;
        #endregion


        #region Heal Effect
        private bool _healEffectActive = false;
        private int _healEffectStartTime;
        private List<HealParticle> _healParticles = new();
        private Random _random = new();
        #endregion


        #region Public Properties
        private const int PulleyActivationDelayMs = 450;
        private const int PulleyActiveDurationMs = 900;
        private const int PulleyReuseDelayMs = 1200;
        private const int PulleyTransportRequestTimeoutMs = 1500;
        private const int StatusCueDurationMs = 1800;
        private const int PulleyHitFallbackAnimationMs = 270;
        private const int MinimumLocalBasicActionOwnershipWindowMs = 120;


        public bool IsActive => _isActive;
        public int PulleyState => _pulleyState;
        public float HealerY => _healerY;
        public float HealerTargetY => _healerTargetY;
        public bool IsHealEffectActive => _healEffectActive;
        public bool HasPendingLocalPulleySequence => _localPulleySequenceStage != LocalPulleySequenceStage.None;
        public PulleyPacketRequest? PendingPulleyPacketRequest => _pendingPulleyPacketRequest;
        public bool HasPulleyTransportRequestInFlight => _pulleyRequestInFlightUntil != int.MinValue;
        public bool IsPulleyHitAnimationActive => _pulleyHitAnimationUntil != int.MinValue;
        public bool IsLocalPlayerWithinPulleyArea => _pulleyEnabled && !_localPlayerHitbox.IsEmpty && _pulleyArea.Intersects(_localPlayerHitbox);
        public bool IsLocalBasicActionOwnerActive => ResolveLocalBasicActionOwnerActive(Environment.TickCount);
        #endregion


        #region Initialization
        public void Initialize(GraphicsDevice device)
        {
            _device = device;
        }


        public void Enable()
        {
            ClearRuntimeState();
            _isActive = true;
        }


        internal void ApplyMapContract(GuildBossMapContract contract)
        {
            if (_device == null)
            {
                return;
            }

            Enable();

            _mapId = contract.MapId;
            _contractSourceDescription = contract.SourceDescription;
            _healerYMin = contract.Healer.YMin;
            _healerYMax = contract.Healer.YMax;
            _healerRise = Math.Max(0, contract.Healer.Rise);
            _healerFall = Math.Max(0, contract.Healer.Fall);
            _healerHealMin = Math.Max(0, contract.Healer.HealMin);
            _healerHealMax = Math.Max(_healerHealMin, contract.Healer.HealMax);
            _healerPath = contract.Healer.AnimationPath;
            _pulleyPath = contract.Pulley.AnimationPath;

            InitHealer(contract.Healer.X, contract.Healer.YMin, _healerPath);
            SetHealerFrames(LoadObjectAnimation(_healerPath));

            InitPulley(contract.Pulley.X, contract.Pulley.Y, _pulleyPath);
            SetPulleyFrames(LoadObjectAnimation(_pulleyPath));
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
                ClearPulleyTransportRequestInFlight();
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
                ClearPulleyTransportRequestInFlight();
                CancelLocalPulleySequence(preserveCooldown: true);
            }


            System.Diagnostics.Debug.WriteLine($"[GuildBossField] OnPulleyStateChange: {_pulleyState} -> {newState}");
            _pulleyState = newState;
            _lastPulleyStateChangeTime = currentTimeMs;
        }


        public bool TryHandleLocalPulleyAttack(Rectangle attackBounds, int currentTimeMs, out string message)
        {
            return TryHandleLocalPulleyAttack(attackBounds, currentTimeMs, allowLocalPreview: true, out message);
        }


        public bool TryHandleLocalPulleyAttack(Rectangle attackBounds, int currentTimeMs, bool allowLocalPreview, out string message)
        {
            return TryHandleLocalPulleyAttack(attackBounds, currentTimeMs, allowLocalPreview, hasHitReactor: false, out message);
        }


        public bool TryHandleLocalPulleyAttack(Rectangle attackBounds, int currentTimeMs, bool allowLocalPreview, bool hasHitReactor, out string message)
        {
            message = null;
            if (!_isActive || !_pulleyEnabled || attackBounds.IsEmpty || !_pulleyArea.Intersects(attackBounds))
            {
                return false;
            }


            if (hasHitReactor)
            {
                // BasicActionAttack still runs local normal-attack ownership before
                // the pulley branch bails out on a hit reactor. Keep that short owner
                // window active while suppressing pulley preview/request flow.
                RegisterLocalBasicActionOwnership(currentTimeMs, includePulleyHitWindow: false);
                return false;
            }


            TriggerPulleyHitAnimation(currentTimeMs);
            RegisterLocalBasicActionOwnership(currentTimeMs, includePulleyHitWindow: true);



            if (_pulleyState != 0
                || _localPulleySequenceStage != LocalPulleySequenceStage.None
                || currentTimeMs < _localPulleyCooldownUntil
                || (HasPulleyTransportRequestInFlight && currentTimeMs < _pulleyRequestInFlightUntil))
            {
                return true;
            }


            if (allowLocalPreview)
            {
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
            }
            else
            {
                _pulleyPacketSequence++;
                _pendingPulleyPacketRequest = new PulleyPacketRequest(currentTimeMs, _pulleyPacketSequence);
                _pulleyRequestInFlightSequence = _pulleyPacketSequence;
                _pulleyRequestInFlightUntil = unchecked(currentTimeMs + PulleyTransportRequestTimeoutMs);
                SetStatusCue("Pulley request sent", currentTimeMs);
                message = "Guild boss pulley request sent.";
            }


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
            if (HasPulleyTransportRequestInFlight && currentTimeMs >= _pulleyRequestInFlightUntil)
            {
                ClearPulleyTransportRequestInFlight();
                if (_localPulleySequenceStage == LocalPulleySequenceStage.None)
                {
                    SetStatusCue("Pulley response timeout", currentTimeMs);
                }
            }


            if (_healerEnabled)
            {
                AdvanceFrame(_healerFrames, ref _healerFrameIndex, ref _lastHealerFrameTime, currentTimeMs);
            }


            if (_pulleyEnabled)
            {
                UpdatePulleyHitAnimation(currentTimeMs);
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
            string inFlightPacket = HasPulleyTransportRequestInFlight ? $", inflight={_pulleyRequestInFlightSequence}" : string.Empty;
            string contractSource = string.IsNullOrWhiteSpace(_contractSourceDescription)
                ? string.Empty
                : $", source={_contractSourceDescription}";


            return $"Guild boss map {_mapId}: healer {healerRange}, pulley {pulleyState}{previewState}{pendingPacket}{inFlightPacket}, rise={_healerRise}, fall={_healerFall}, heal={_healerHealMin}..{_healerHealMax}, healer art={_healerPath ?? "none"}, pulley art={_pulleyPath ?? "none"}{contractSource}.";

        }

        internal static bool TryBuildMapContract(MapInfo mapInfo, out GuildBossMapContract contract)
        {
            contract = default;
            WzImage mapImage = mapInfo?.Image;
            if (mapInfo == null || mapImage == null)
            {
                return false;
            }

            if (!mapImage.Parsed)
            {
                mapImage.ParseImage();
            }

            if (mapImage["healer"] is not WzSubProperty healerProp
                || mapImage["pulley"] is not WzSubProperty pulleyProp)
            {
                return false;
            }

            string healerPath = healerProp["healer"]?.GetString();
            string pulleyPath = pulleyProp["pulley"]?.GetString();
            if (string.IsNullOrWhiteSpace(healerPath) || string.IsNullOrWhiteSpace(pulleyPath))
            {
                return false;
            }

            contract = new GuildBossMapContract(
                mapInfo.id,
                new GuildBossHealerContract(
                    healerProp["x"]?.GetInt() ?? 0,
                    healerProp["yMin"]?.GetInt() ?? 0,
                    healerProp["yMax"]?.GetInt() ?? healerProp["yMin"]?.GetInt() ?? 0,
                    healerProp["rise"]?.GetInt() ?? 0,
                    healerProp["fall"]?.GetInt() ?? 0,
                    healerProp["healMin"]?.GetInt() ?? 0,
                    healerProp["healMax"]?.GetInt() ?? healerProp["healMin"]?.GetInt() ?? 0,
                    healerPath),
                new GuildBossPulleyContract(
                    pulleyProp["x"]?.GetInt() ?? 0,
                    pulleyProp["y"]?.GetInt() ?? 0,
                    pulleyPath),
                BuildContractSourceDescription(mapImage, healerPath, pulleyPath));
            return true;
        }

        internal void ConfigureFromBoard(Board board)
        {
            _mapId = board?.MapInfo?.id ?? _mapId;
            if (TryBuildMapContract(board?.MapInfo, out GuildBossMapContract contract))
            {
                ApplyMapContract(contract);
            }
        }



        #region Reset
        public void Reset()
        {
            ClearRuntimeState();
            _isActive = false;
        }
        #endregion


        private void ClearRuntimeState()
        {
            _mapId = 0;
            _pulleyState = 0;
            _healerYMin = 0;
            _healerYMax = 0;
            _healerRise = 0;
            _healerFall = 0;
            _healerHealMin = 0;
            _healerHealMax = 0;
            _healerEnabled = false;
            _pulleyEnabled = false;
            _healerX = 0f;
            _healerY = 0f;
            _healerTargetY = 0f;
            _pulleyX = 0f;
            _pulleyY = 0f;
            _pulleyArea = Rectangle.Empty;
            _healEffectActive = false;
            _healParticles.Clear();
            _healerPath = null;
            _pulleyPath = null;
            _contractSourceDescription = null;
            _healerFrames = null;
            _pulleyFrames = null;
            _healerFrameIndex = 0;
            _pulleyFrameIndex = 0;
            _lastHealerFrameTime = 0;
            _lastPulleyFrameTime = 0;
            _pulleyHitAnimationUntil = int.MinValue;
            _lastPulleyStateChangeTime = 0;
            _lastHealAmount = 0;
            _localPulleySequenceStage = LocalPulleySequenceStage.None;
            _localPulleySequenceNextTransitionTick = int.MinValue;
            _localPulleyCooldownUntil = int.MinValue;
            _pulleyRequestInFlightUntil = int.MinValue;
            _pulleyRequestInFlightSequence = 0;
            _pendingPulleyPacketRequest = null;
            _pulleyPacketSequence = 0;
            _statusCueExpiresAt = int.MinValue;
            _statusCueText = null;
            _localBasicActionOwnerUntil = int.MinValue;
            _localPlayerHitbox = Rectangle.Empty;
        }


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
                        short healerY = checked((short)ClampHealerY((int)MathF.Round(_healerTargetY) + _healerFall));
                        using PacketWriter writer = new(sizeof(short));
                        writer.Write(healerY);
                        TryApplyPacket((int)GuildBossPacketType.HealerMove, writer.ToArray(), currentTimeMs, GuildBossPacketSource.LocalPreview, out _);
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


        private void ClearPulleyTransportRequestInFlight()
        {
            _pulleyRequestInFlightUntil = int.MinValue;
            _pulleyRequestInFlightSequence = 0;
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

        internal bool ResolveLocalBasicActionOwnerActive(int currentTimeMs)
        {
            if (!_isActive || _localBasicActionOwnerUntil == int.MinValue)
            {
                return false;
            }

            return unchecked(currentTimeMs - _localBasicActionOwnerUntil) < 0;
        }

        private void RegisterLocalBasicActionOwnership(int currentTimeMs, bool includePulleyHitWindow)
        {
            // Client evidence: CField_GuildBoss::BasicActionAttack (0x5517d0) routes
            // the local normal attack through CUserLocal::TryDoingNormalAttack before
            // it sends the pulley packet when state == 0. Mirror that as a short local
            // ownership window instead of treating the whole pulley area/state as owned.
            int ownerWindowMs = includePulleyHitWindow
                ? Math.Max(MinimumLocalBasicActionOwnershipWindowMs, GetPulleyHitAnimationDurationMs())
                : MinimumLocalBasicActionOwnershipWindowMs;
            _localBasicActionOwnerUntil = unchecked(currentTimeMs + ownerWindowMs);
        }


        private void TriggerPulleyHitAnimation(int currentTimeMs)
        {
            // CPulley::Hit starts the layer animation only when the pulley layer is not already animating.
            if (IsPulleyHitAnimationActive)
            {
                SetStatusCue("Pulley hit", currentTimeMs);
                return;
            }

            _pulleyFrameIndex = 0;
            _lastPulleyFrameTime = currentTimeMs;
            _pulleyHitAnimationUntil = unchecked(currentTimeMs + GetPulleyHitAnimationDurationMs());
            SetStatusCue("Pulley hit", currentTimeMs);
        }


        private void UpdatePulleyHitAnimation(int currentTimeMs)
        {
            if (!IsPulleyHitAnimationActive)
            {
                _pulleyFrameIndex = 0;
                _lastPulleyFrameTime = 0;
                return;
            }

            if (currentTimeMs >= _pulleyHitAnimationUntil)
            {
                _pulleyHitAnimationUntil = int.MinValue;
                _pulleyFrameIndex = 0;
                _lastPulleyFrameTime = 0;
                return;
            }

            AdvanceFrame(_pulleyFrames, ref _pulleyFrameIndex, ref _lastPulleyFrameTime, currentTimeMs);
        }


        private int GetPulleyHitAnimationDurationMs()
        {
            return ComputePulleyHitAnimationDurationMs(_pulleyFrames?.Select(frame => frame?.Delay ?? 0), PulleyHitFallbackAnimationMs);
        }


        internal static int ComputePulleyHitAnimationDurationMs(IEnumerable<int> frameDelays, int fallbackDurationMs)
        {
            int durationMs = 0;
            if (frameDelays != null)
            {
                foreach (int frameDelay in frameDelays)
                {
                    durationMs += Math.Max(0, frameDelay);
                }
            }

            return durationMs > 0 ? durationMs : Math.Max(1, fallbackDurationMs);
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

        private static string BuildContractSourceDescription(WzImage mapImage, string healerPath, string pulleyPath)
        {
            string mapImageName = mapImage?.Name ?? "<unknown>";
            if (!mapImageName.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
            {
                mapImageName += ".img";
            }

            return $"WZ contract {mapImageName}: healer={healerPath}, pulley={pulleyPath}";
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
}
