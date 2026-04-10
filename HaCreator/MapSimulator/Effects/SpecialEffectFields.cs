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
using HaCreator.MapSimulator.Managers;
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
        private readonly CakePieEventField _cakePie = new();
        #endregion


        #region Public Access
        public WeddingField Wedding => _wedding;
        public WitchtowerField Witchtower => _witchtower;
        public BattlefieldField Battlefield => _battlefield;
        public GuildBossField GuildBoss => _guildBoss;
        public DojoField Dojo => _dojo;
        public SpaceGagaField SpaceGaga => _spaceGaga;
        public MassacreField Massacre => _massacre;
        public CakePieEventField CakePie => _cakePie;
        public bool HasBlockingScriptedSequence => _wedding.HasActiveScriptedDialog || _cakePie.IsItemInfoVisible;


        public void SetWeddingPlayerState(int? localCharacterId, Vector2? localWorldPosition, CharacterBuild localPlayerBuild = null)
        {
            _wedding.SetLocalPlayerState(localCharacterId, localWorldPosition, localPlayerBuild);
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
            Action clearBgmOverride = null,
            Func<LoginAvatarLook, string, CharacterBuild> weddingRemoteBuildFactory = null,
            CharacterLoader weddingCharacterLoader = null)
        {

            _wedding.Initialize(device, requestBgmOverride, clearBgmOverride, weddingRemoteBuildFactory, weddingCharacterLoader);
            _witchtower.Initialize(device);
            _battlefield.Initialize(device);
            _guildBoss.Initialize(device);
            _dojo.Initialize(device);
            _spaceGaga.Initialize(device);
            _massacre.Initialize(device);
            _cakePie.Initialize(device);
        }


        /// <summary>
        /// Detect and enable appropriate field type based on map ID
        /// </summary>
        public void DetectFieldType(MapInfo mapInfo)
        {
            int mapId = mapInfo?.id ?? 0;
            FieldType? fieldType = mapInfo?.fieldType;
            _cakePie.BindMap(mapId);

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
            // Guild boss maps are identified by their map-root healer/pulley contract
            // before falling back to the legacy field-type and id-range heuristics.
            else if (IsGuildBossMap(mapInfo))
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
                _massacre.Enable(mapInfo);
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

                _dojo.Configure(
                    board?.MapInfo,
                    board?.BoardItems?.Portals,
                    board?.BoardItems?.Portals?.Any(portal => string.Equals(portal?.script, "dojang_next", StringComparison.OrdinalIgnoreCase)) == true);
            }
        }



        private static bool IsWitchtowerMap(int mapId, FieldType? fieldType)
        {
            // Witchtower maps - typically special event maps
            return fieldType == FieldType.FIELDTYPE_WITCHTOWER
                || (mapId >= 922000000 && mapId <= 922000099);
        }


        private static bool IsGuildBossMap(MapInfo mapInfo)
        {
            if (mapInfo == null)
            {
                return false;
            }

            if (GuildBossField.TryBuildMapContract(mapInfo, out _))
            {
                return true;
            }

            int mapId = mapInfo.id;
            FieldType? fieldType = mapInfo.fieldType;

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



            if (_cakePie.IsActive || _cakePie.HasVisibleUi)
                _cakePie.Update(currentTimeMs);
        }
        #endregion


        #region Packet Dispatch
        public bool TryDispatchActiveWrapperPacket(int packetType, byte[] payload, int currentTimeMs, out string ownerName, out string message)
        {
            payload ??= Array.Empty<byte>();

            if (_wedding.HasWeddingPacketOwner)
            {
                ownerName = "CField_Wedding::OnPacket";
                bool applied = _wedding.TryApplyPacket(packetType, payload, currentTimeMs, out string errorMessage);
                message = applied ? _wedding.DescribeStatus() : errorMessage;
                return applied;
            }

            if (_guildBoss.IsActive)
            {
                ownerName = "CField_GuildBoss::OnPacket";
                bool applied = _guildBoss.TryApplyPacket(packetType, payload, currentTimeMs, out string errorMessage);
                message = applied ? _guildBoss.DescribeStatus() : errorMessage;
                return applied;
            }

            if (_dojo.IsActive)
            {
                ownerName = "CField_Dojang::OnPacket";
                bool applied = _dojo.TryApplyPacket(packetType, payload, currentTimeMs, out string errorMessage);
                message = applied ? _dojo.DescribeStatus() : errorMessage;
                return applied;
            }

            if (_massacre.IsActive)
            {
                ownerName = _massacre.GetPacketOwnerName(packetType);
                bool applied = _massacre.TryApplyPacket(packetType, payload, currentTimeMs, out string errorMessage);
                message = applied ? _massacre.DescribeStatus() : errorMessage;
                return applied;
            }

            if (_spaceGaga.IsActive)
            {
                ownerName = "CField_SpaceGAGA::OnPacket";
                bool applied = _spaceGaga.TryApplyPacket(packetType, payload, currentTimeMs, out string errorMessage);
                message = applied ? _spaceGaga.DescribeStatus() : errorMessage;
                return applied;
            }

            ownerName = null;
            message = null;
            return false;
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



            if (_cakePie.IsActive || _cakePie.HasVisibleUi)
                _cakePie.Draw(spriteBatch, pixelTexture, font);
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
            _cakePie.Reset();
        }
        #endregion
    }
}
