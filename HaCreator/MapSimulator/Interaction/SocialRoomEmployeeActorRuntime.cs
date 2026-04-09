using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.Pools;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class SocialRoomEmployeeActorRuntime
    {
        private const string MerchantNpcId = "9071001";
        private const string StoreBankerNpcId = "9030000";
        private const int SignVerticalOffset = 34;
        private const int MiniRoomBalloonVerticalOffset = 10;
        private const int ShopMiniRoomBalloonRaisedOffset = 7;
        private const float HeadlineScale = 0.48f;
        private const float DetailScale = 0.42f;

        private static readonly Color SignHeadlineColor = new(255, 242, 176);
        private static readonly Color SignDetailColor = new(244, 244, 244);
        private static readonly Color SignPanelColor = new(28, 22, 18, 210);
        private static readonly Color SignBorderColor = new(180, 138, 69, 255);

        private readonly Dictionary<string, NpcItem> _actorCache = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EmployeeTemplateProfile> _cashProfileCache = new(StringComparer.Ordinal);
        private readonly Dictionary<int, EmployeeActionCatalog> _cashActionCatalogCache = new();
        private readonly Dictionary<(int TemplateId, int ActionIndex), List<IDXObject>> _cashActionCache = new();
        private readonly ConcurrentBag<WzObject> _usedProps = new();
        private readonly Random _random = new();
        private MiniRoomBalloonAssets _miniRoomBalloonAssets;

        private NpcItem _activeActor;
        private SocialRoomFieldActorSnapshot _activeSnapshot;
        private string _activeActorKey = string.Empty;
        private string _lastStateKey = string.Empty;
        private string _currentIdleAction = AnimationKeys.Stand;
        private bool? _currentAutoFlip;
        private int _idleActionRemainingMs;
        private int _temporaryActionRemainingMs;

        public bool IsVisible => _activeActor != null && _activeSnapshot != null;

        public void Clear()
        {
            _activeActor = null;
            _activeSnapshot = null;
            _activeActorKey = string.Empty;
            _lastStateKey = string.Empty;
            _currentIdleAction = AnimationKeys.Stand;
            _currentAutoFlip = null;
            _idleActionRemainingMs = 0;
            _temporaryActionRemainingMs = 0;
        }

        public void Update(
            SocialRoomFieldActorSnapshot snapshot,
            Board mapBoard,
            PlayerCharacter player,
            TexturePool texturePool,
            GraphicsDevice device,
            float userScreenScaleFactor,
            GameTime gameTime,
            Func<int, QuestStateType> questStateProvider = null,
            Func<int, string> questRecordValueProvider = null)
        {
            if (snapshot == null || mapBoard == null || player == null || texturePool == null || device == null)
            {
                Clear();
                return;
            }

            NpcItem actor = EnsureActor(
                snapshot,
                mapBoard,
                texturePool,
                device,
                userScreenScaleFactor,
                player.Build?.Gender,
                questStateProvider,
                questRecordValueProvider);
            if (actor == null)
            {
                Clear();
                return;
            }

            _activeActor = actor;
            _activeSnapshot = snapshot;
            _activeActorKey = BuildActorCacheKey(snapshot);

            EmployeeTemplateProfile profile = ResolveProfile(snapshot);
            int elapsedMs = (int)Math.Max(0d, gameTime.ElapsedGameTime.TotalMilliseconds);

            EnsureMiniRoomBalloonAssets(device);
            AdvanceActionState(_activeActorKey, actor, snapshot, profile, elapsedMs);
            SyncActorPosition(player, actor, snapshot);

            actor.MovementEnabled = false;
            actor.Update(elapsedMs);
        }

        public void Draw(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX,
            int mapShiftY,
            int mapCenterX,
            int mapCenterY,
            RenderParameters renderParameters,
            int tickCount,
            SpriteFont font,
            Texture2D panelTexture)
        {
            if (_activeActor == null || _activeSnapshot == null)
            {
                return;
            }

            _activeActor.Draw(
                spriteBatch,
                skeletonMeshRenderer,
                gameTime,
                mapShiftX,
                mapShiftY,
                mapCenterX,
                mapCenterY,
                null,
                renderParameters,
                tickCount);

            if (font == null)
            {
                return;
            }

            if (_activeSnapshot.HasMiniRoomBalloon && DrawMiniRoomBalloon(spriteBatch, font, mapShiftX, mapShiftY, mapCenterX, mapCenterY))
            {
                return;
            }

            DrawSign(spriteBatch, font, panelTexture, mapShiftX, mapShiftY, mapCenterX, mapCenterY);
        }

        private NpcItem EnsureActor(
            SocialRoomFieldActorSnapshot snapshot,
            Board mapBoard,
            TexturePool texturePool,
            GraphicsDevice device,
            float userScreenScaleFactor,
            CharacterGender? localPlayerGender,
            Func<int, QuestStateType> questStateProvider,
            Func<int, string> questRecordValueProvider)
        {
            string actorKey = BuildActorCacheKey(snapshot);
            if (_actorCache.TryGetValue(actorKey, out NpcItem cachedActor))
            {
                return cachedActor;
            }

            NpcItem actor = snapshot.Template switch
            {
                SocialRoomFieldActorTemplate.CashEmployee when snapshot.TemplateId > 0
                    => CreateCashEmployeeActor(actorKey, snapshot.TemplateId, mapBoard, texturePool, device, userScreenScaleFactor),
                _ => CreateNpcActor(
                    ResolveProfile(snapshot).NpcId,
                    mapBoard,
                    texturePool,
                    device,
                    userScreenScaleFactor,
                    localPlayerGender,
                    questStateProvider,
                    questRecordValueProvider)
            };

            actor ??= CreateNpcActor(
                ResolveProfile(snapshot).NpcId,
                mapBoard,
                texturePool,
                device,
                userScreenScaleFactor,
                localPlayerGender,
                questStateProvider,
                questRecordValueProvider);

            if (actor == null)
            {
                return null;
            }

            actor.MovementEnabled = false;
            _actorCache[actorKey] = actor;
            return actor;
        }

        private NpcItem CreateNpcActor(
            string npcId,
            Board mapBoard,
            TexturePool texturePool,
            GraphicsDevice device,
            float userScreenScaleFactor,
            CharacterGender? localPlayerGender,
            Func<int, QuestStateType> questStateProvider,
            Func<int, string> questRecordValueProvider)
        {
            NpcInfo npcInfo = NpcInfo.Get(npcId);
            if (npcInfo == null)
            {
                return null;
            }

            NpcInstance npcInstance = (NpcInstance)npcInfo.CreateInstance(
                mapBoard,
                0,
                0,
                UserSettings.Npcrx0Offset,
                UserSettings.Npcrx1Offset,
                8,
                null,
                0,
                false,
                false,
                null,
                null);

            return LifeLoader.CreateNpcFromProperty(
                texturePool,
                npcInstance,
                userScreenScaleFactor,
                device,
                _usedProps,
                includeTooltips: false,
                localPlayerGender: localPlayerGender,
                questStateProvider: questStateProvider,
                questRecordValueProvider: questRecordValueProvider);
        }

        private NpcItem CreateCashEmployeeActor(
            string actorKey,
            int templateId,
            Board mapBoard,
            TexturePool texturePool,
            GraphicsDevice device,
            float userScreenScaleFactor)
        {
            EmployeeActionCatalog actionCatalog = ResolveEmployeeActionCatalog(templateId);
            if (actionCatalog == null || actionCatalog.Actions.Count == 0)
            {
                return null;
            }

            _cashProfileCache[actorKey] = EmployeeTemplateProfile.CreateCashEmployee(actionCatalog.OrderedActionNames);

            NpcInfo fallbackNpcInfo = NpcInfo.Get(MerchantNpcId);
            if (fallbackNpcInfo == null)
            {
                return null;
            }

            NpcInstance npcInstance = (NpcInstance)fallbackNpcInfo.CreateInstance(
                mapBoard,
                0,
                0,
                UserSettings.Npcrx0Offset,
                UserSettings.Npcrx1Offset,
                8,
                null,
                0,
                false,
                false,
                null,
                null);

            NpcAnimationSet animationSet = new();
            foreach (EmployeeActionCatalog.Entry actionEntry in actionCatalog.Actions)
            {
                List<IDXObject> actionFrames = LoadEmployeeActionFrames(templateId, actionEntry, texturePool, device);
                if (actionFrames.Count > 0)
                {
                    animationSet.AddAnimation(actionEntry.ActionName, actionFrames);
                }
            }

            if (animationSet.ActionCount == 0)
            {
                return null;
            }

            return new NpcItem(
                npcInstance,
                animationSet,
                null,
                null);
        }

        private EmployeeActionCatalog ResolveEmployeeActionCatalog(int templateId)
        {
            if (templateId <= 0)
            {
                return null;
            }

            if (_cashActionCatalogCache.TryGetValue(templateId, out EmployeeActionCatalog cachedCatalog))
            {
                return cachedCatalog;
            }

            WzImageProperty employeeRoot = ResolveEmployeeProperty(templateId);
            EmployeeActionCatalog catalog = EmployeeActionCatalog.Create(employeeRoot);
            if (catalog != null)
            {
                _cashActionCatalogCache[templateId] = catalog;
            }

            return catalog;
        }

        private void SyncActorPosition(PlayerCharacter player, NpcItem actor, SocialRoomFieldActorSnapshot snapshot)
        {
            int actorX;
            int actorY;
            if (snapshot.HasWorldPosition && !snapshot.UseOwnerAnchor)
            {
                actorX = snapshot.WorldX;
                actorY = snapshot.WorldY;
            }
            else
            {
                int horizontalOffset = player.FacingRight ? snapshot.AnchorOffsetX : -snapshot.AnchorOffsetX;
                actorX = (int)Math.Round(player.Position.X) + horizontalOffset;
                actorY = (int)Math.Round(player.Position.Y) + snapshot.AnchorOffsetY;
            }

            actor.SetRenderPositionOverride(actorX, actorY);
            actor.NpcInstance.Flip = ResolveActorFlip(snapshot, player, actorX);
        }

        private void AdvanceActionState(
            string actorKey,
            NpcItem actor,
            SocialRoomFieldActorSnapshot snapshot,
            EmployeeTemplateProfile profile,
            int elapsedMs)
        {
            if (actor == null || snapshot == null || profile == null)
            {
                return;
            }

            bool stateChanged = !string.Equals(_lastStateKey, snapshot.StateKey, StringComparison.Ordinal);
            if (stateChanged)
            {
                _lastStateKey = snapshot.StateKey ?? string.Empty;
                TriggerStateAction(actorKey, actor, snapshot, profile);
                ResetIdleSelection(actorKey, actor, snapshot, profile, preferContextualAction: true);
            }

            _temporaryActionRemainingMs = Math.Max(0, _temporaryActionRemainingMs - Math.Max(0, elapsedMs));
            if (_temporaryActionRemainingMs > 0)
            {
                return;
            }

            _idleActionRemainingMs = Math.Max(0, _idleActionRemainingMs - Math.Max(0, elapsedMs));
            if (_idleActionRemainingMs > 0)
            {
                actor.SetAction(_currentIdleAction);
                return;
            }

            ResetIdleSelection(actorKey, actor, snapshot, profile, preferContextualAction: false);
        }

        private void TriggerStateAction(
            string actorKey,
            NpcItem actor,
            SocialRoomFieldActorSnapshot snapshot,
            EmployeeTemplateProfile profile)
        {
            string speakAction = ResolveNextAvailableAction(actorKey, actor, profile.SpeakActions);
            if (string.IsNullOrWhiteSpace(speakAction))
            {
                return;
            }

            RefreshAutoFacing(snapshot);
            actor.SetTemporaryAction(speakAction, profile.SpeakDurationMs);
            _temporaryActionRemainingMs = ResolveActionDurationMs(actor, speakAction, profile.SpeakDurationMs);
        }

        private void ResetIdleSelection(
            string actorKey,
            NpcItem actor,
            SocialRoomFieldActorSnapshot snapshot,
            EmployeeTemplateProfile profile,
            bool preferContextualAction)
        {
            string nextIdleAction = preferContextualAction
                ? ResolveContextualIdleAction(actorKey, actor, snapshot, profile) ?? ResolveNextAvailableAction(actorKey, actor, profile.IdleActions)
                : ResolveNextAvailableAction(actorKey, actor, profile.IdleActions);
            if (string.IsNullOrWhiteSpace(nextIdleAction))
            {
                nextIdleAction = AnimationKeys.Stand;
            }

            _currentIdleAction = nextIdleAction;
            _idleActionRemainingMs = ResolveActionDurationMs(actor, _currentIdleAction, profile.MinIdleDurationMs);

            if (_temporaryActionRemainingMs <= 0)
            {
                RefreshAutoFacing(snapshot);
            }

            actor.SetAction(_currentIdleAction);
        }

        private string ResolveContextualIdleAction(
            string actorKey,
            NpcItem actor,
            SocialRoomFieldActorSnapshot snapshot,
            EmployeeTemplateProfile profile)
        {
            if (actor == null || snapshot == null || profile == null)
            {
                return null;
            }

            string stateKey = snapshot.StateKey ?? string.Empty;
            if (stateKey.IndexOf("expired", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ResolveNextAvailableAction(actorKey, actor, profile.ExpiredActions);
            }

            if (stateKey.IndexOf("updating sale list", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("registering sale", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("sale bundles", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("restock", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ResolveNextAvailableAction(actorKey, actor, profile.RestockActions);
            }

            if (stateKey.IndexOf("ledger", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("claim", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("sold", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("tax", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ResolveNextAvailableAction(actorKey, actor, profile.LedgerActions);
            }

            if (stateKey.IndexOf("permit active", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("merchant", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("open shop", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("open for visitors", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ResolveNextAvailableAction(actorKey, actor, profile.ActiveActions);
            }

            return null;
        }

        private static int ResolveActionDurationMs(NpcItem actor, string action, int fallbackMs)
        {
            if (actor == null || string.IsNullOrWhiteSpace(action))
            {
                return Math.Max(250, fallbackMs);
            }

            int actionDurationMs = actor.GetActionTotalDurationMs(action);
            return Math.Max(250, actionDurationMs > 0 ? actionDurationMs : fallbackMs);
        }

        private string ResolveNextAvailableAction(string actorKey, NpcItem actor, IReadOnlyList<string> candidates)
        {
            if (actor == null || candidates == null || candidates.Count == 0)
            {
                return null;
            }

            List<string> availableActions = new();
            for (int i = 0; i < candidates.Count; i++)
            {
                string action = candidates[i];
                if (!string.IsNullOrWhiteSpace(action) && actor.HasAction(action))
                {
                    availableActions.Add(action);
                }
            }

            if (availableActions.Count == 0)
            {
                return actor.HasAction(AnimationKeys.Stand) ? AnimationKeys.Stand : null;
            }

            return availableActions[_random.Next(availableActions.Count)];
        }

        private EmployeeTemplateProfile ResolveProfile(SocialRoomFieldActorSnapshot snapshot)
        {
            if (snapshot?.Template == SocialRoomFieldActorTemplate.CashEmployee
                && _cashProfileCache.TryGetValue(BuildActorCacheKey(snapshot), out EmployeeTemplateProfile cachedProfile))
            {
                return cachedProfile;
            }

            return snapshot.Template switch
            {
                SocialRoomFieldActorTemplate.StoreBanker => EmployeeTemplateProfile.StoreBanker,
                SocialRoomFieldActorTemplate.CashEmployee => EmployeeTemplateProfile.CashEmployee,
                _ => EmployeeTemplateProfile.Merchant
            };
        }

        private static string BuildActorCacheKey(SocialRoomFieldActorSnapshot snapshot)
        {
            return snapshot.Template == SocialRoomFieldActorTemplate.CashEmployee
                ? $"cash:{snapshot.TemplateId}"
                : snapshot.Template.ToString();
        }

        private static WzImageProperty ResolveEmployeeProperty(int templateId)
        {
            if (templateId <= 0)
            {
                return null;
            }

            WzImage itemImage = global::HaCreator.Program.FindImage("Item", $"Cash/{templateId / 10000:D4}.img");
            return itemImage?[templateId.ToString("D8")]?["employee"];
        }

        private List<IDXObject> LoadEmployeeActionFrames(
            int templateId,
            EmployeeActionCatalog.Entry actionEntry,
            TexturePool texturePool,
            GraphicsDevice device)
        {
            if (templateId <= 0 || actionEntry == null || string.IsNullOrWhiteSpace(actionEntry.ActionName) || actionEntry.ActionProperty == null)
            {
                return new List<IDXObject>();
            }

            (int TemplateId, int ActionIndex) cacheKey = (templateId, actionEntry.ClientActionIndex);
            if (_cashActionCache.TryGetValue(cacheKey, out List<IDXObject> cachedFrames))
            {
                return cachedFrames;
            }

            var frames = new List<IDXObject>();
            foreach (WzImageProperty childProperty in actionEntry.ActionProperty.WzProperties.OrderBy(GetFrameOrder))
            {
                WzCanvasProperty canvas = ResolveCanvasProperty(childProperty);
                if (canvas == null)
                {
                    continue;
                }

                IDXObject frame = CreateEmployeeFrame(texturePool, canvas, device, defaultDelay: 180);
                if (frame != null)
                {
                    frames.Add(frame);
                }
            }

            _cashActionCache[cacheKey] = frames;
            return frames;
        }

        private static WzCanvasProperty ResolveCanvasProperty(WzImageProperty property)
        {
            if (property is WzCanvasProperty canvasProperty)
            {
                return canvasProperty;
            }

            if (property is WzUOLProperty uol)
            {
                return ResolveCanvasProperty(uol.LinkValue as WzImageProperty);
            }

            return null;
        }

        private IDXObject CreateEmployeeFrame(TexturePool texturePool, WzCanvasProperty canvasProperty, GraphicsDevice device, int defaultDelay)
        {
            if (canvasProperty?.PngProperty == null || device == null)
            {
                return null;
            }

            EnsureEmployeeCanvasTextureLoaded(texturePool, canvasProperty, device);
            Texture2D texture = canvasProperty.MSTag as Texture2D;
            if (texture == null)
            {
                return null;
            }

            System.Drawing.PointF origin = canvasProperty.GetCanvasOriginPosition();
            int delay = GetIntValue(canvasProperty["delay"]) ?? defaultDelay;
            var frame = new DXObject(origin, texture, Math.Max(1, delay))
            {
                Tag = canvasProperty
            };
            _usedProps.Add(canvasProperty);
            return frame;
        }

        private static void EnsureEmployeeCanvasTextureLoaded(TexturePool texturePool, WzCanvasProperty canvasProperty, GraphicsDevice device)
        {
            if (canvasProperty?.MSTag != null)
            {
                return;
            }

            string canvasBitmapPath = canvasProperty.FullPath;
            Texture2D textureFromCache = texturePool?.GetTexture(canvasBitmapPath);
            if (textureFromCache != null)
            {
                canvasProperty.MSTag = textureFromCache;
                return;
            }

            using var bitmap = canvasProperty.GetLinkedWzCanvasBitmap();
            if (bitmap == null)
            {
                return;
            }

            canvasProperty.MSTag = bitmap.ToTexture2D(device);
            if (canvasProperty.MSTag is Texture2D loadedTexture)
            {
                texturePool?.AddTextureToPool(canvasBitmapPath, loadedTexture);
            }
        }

        private static int? GetIntValue(WzImageProperty prop)
        {
            return prop switch
            {
                WzIntProperty intProp => intProp.Value,
                WzShortProperty shortProp => shortProp.Value,
                WzLongProperty longProp => (int)longProp.Value,
                WzStringProperty strProp => int.TryParse(strProp.Value, out int value) ? value : null,
                _ => null
            };
        }

        private static int GetFrameOrder(WzImageProperty property)
        {
            return int.TryParse(property?.Name, out int index) ? index : int.MaxValue;
        }

        private void RefreshAutoFacing(SocialRoomFieldActorSnapshot snapshot)
        {
            if (snapshot?.Flip.HasValue == true)
            {
                _currentAutoFlip = snapshot.Flip.Value;
                return;
            }

            _currentAutoFlip = _random.Next(2) == 0;
        }

        private bool ResolveActorFlip(SocialRoomFieldActorSnapshot snapshot, PlayerCharacter player, int actorX)
        {
            if (snapshot?.Flip.HasValue == true)
            {
                _currentAutoFlip = snapshot.Flip.Value;
                return snapshot.Flip.Value;
            }

            if (!_currentAutoFlip.HasValue)
            {
                _currentAutoFlip = ResolveDefaultFlip(player, actorX);
            }

            return _currentAutoFlip.Value;
        }

        private static bool ResolveDefaultFlip(PlayerCharacter player, int actorX)
        {
            if (player == null)
            {
                return false;
            }

            int ownerX = (int)Math.Round(player.Position.X);
            if (actorX == ownerX)
            {
                return !player.FacingRight;
            }

            return actorX > ownerX;
        }

        private void EnsureMiniRoomBalloonAssets(GraphicsDevice device)
        {
            if (_miniRoomBalloonAssets?.IsLoaded == true || device == null || device.IsDisposed)
            {
                return;
            }

            WzImage chatBalloonImage = global::HaCreator.Program.FindImage("UI", "ChatBalloon.img");
            if (chatBalloonImage == null)
            {
                return;
            }

            chatBalloonImage.ParseImage();
            WzSubProperty miniRoomSource = chatBalloonImage["miniroom"] as WzSubProperty;
            if (miniRoomSource == null)
            {
                return;
            }

            WzSubProperty currentCountSource = miniRoomSource["cNum"] as WzSubProperty;
            WzSubProperty maxCountSource = miniRoomSource["mNum"] as WzSubProperty;
            WzSubProperty shopSkinSource = miniRoomSource["PSSkin"] as WzSubProperty;

            _miniRoomBalloonAssets = new MiniRoomBalloonAssets
            {
                PointedBackground = LoadUiCanvasTexture(miniRoomSource["backgrnd2"] as WzCanvasProperty, device),
                Background = LoadUiCanvasTexture(miniRoomSource["backgrnd"] as WzCanvasProperty, device),
                PersonalShopIcon = LoadUiCanvasTexture(miniRoomSource["PersonalShop"] as WzCanvasProperty, device),
                Able = LoadUiCanvasTexture(miniRoomSource["Able"] as WzCanvasProperty, device),
                Disable = LoadUiCanvasTexture(miniRoomSource["Disable"] as WzCanvasProperty, device),
                Progress = LoadUiCanvasTexture(miniRoomSource["Progress"] as WzCanvasProperty, device),
                ShopBoards = LoadIndexedTextures(shopSkinSource, device, 7),
                CurrentCountDigits = LoadDigitTextures(currentCountSource, device),
                MaxCountDigits = LoadDigitTextures(maxCountSource, device)
            };
        }

        private static Texture2D LoadUiCanvasTexture(WzCanvasProperty canvasProperty, GraphicsDevice device)
        {
            return canvasProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
        }

        private static Texture2D[] LoadIndexedTextures(WzSubProperty source, GraphicsDevice device, int count)
        {
            Texture2D[] textures = new Texture2D[count];
            for (int i = 0; i < count; i++)
            {
                textures[i] = LoadUiCanvasTexture(source?[i.ToString()] as WzCanvasProperty, device);
            }

            return textures;
        }

        private static Texture2D[] LoadDigitTextures(WzSubProperty source, GraphicsDevice device)
        {
            Texture2D[] digits = new Texture2D[5];
            for (int i = 1; i <= 4; i++)
            {
                digits[i] = LoadUiCanvasTexture(source?[i.ToString()] as WzCanvasProperty, device);
            }

            return digits;
        }

        private bool DrawMiniRoomBalloon(
            SpriteBatch spriteBatch,
            SpriteFont font,
            int mapShiftX,
            int mapShiftY,
            int mapCenterX,
            int mapCenterY)
        {
            MiniRoomBalloonAssets assets = _miniRoomBalloonAssets;
            if (_activeActor == null || _activeSnapshot == null || assets?.IsLoaded != true)
            {
                return false;
            }

            Texture2D boardTexture = ResolveMiniRoomBalloonBoardTexture(_activeSnapshot, assets);
            if (boardTexture == null)
            {
                return false;
            }

            int actorScreenX = _activeActor.CurrentX - mapShiftX + mapCenterX;
            int actorScreenY = _activeActor.CurrentY - mapShiftY + mapCenterY;
            int boardX = actorScreenX - (boardTexture.Width / 2);
            int boardY =
                actorScreenY
                - _activeActor.NpcInstance.Height
                - boardTexture.Height
                - MiniRoomBalloonVerticalOffset
                + ResolveMiniRoomBalloonVerticalAdjustment(_activeSnapshot);
            Vector2 boardPosition = new(boardX, boardY);

            spriteBatch.Draw(boardTexture, boardPosition, Color.White);

            if (assets.PersonalShopIcon != null)
            {
                spriteBatch.Draw(assets.PersonalShopIcon, new Vector2(boardX + 12, boardY + 83), Color.White);
            }

            DrawMiniRoomBalloonCount(spriteBatch, assets.CurrentCountDigits, ResolveMiniRoomCurrentUsers(_activeSnapshot), boardX + 29, boardY + 85);
            DrawMiniRoomBalloonCount(spriteBatch, assets.MaxCountDigits, ResolveMiniRoomMaxUsers(_activeSnapshot), boardX + 46, boardY + 85);

            Texture2D statusTexture = ResolveMiniRoomStatusTexture(_activeSnapshot, assets);
            if (statusTexture != null)
            {
                spriteBatch.Draw(statusTexture, new Vector2(boardX + 97, boardY + 84), Color.White);
            }

            DrawMiniRoomBalloonText(spriteBatch, font, boardX, boardY);
            return true;
        }

        private static Texture2D ResolveMiniRoomBalloonBoardTexture(SocialRoomFieldActorSnapshot snapshot, MiniRoomBalloonAssets assets)
        {
            if (snapshot == null || assets == null)
            {
                return null;
            }

            if (snapshot.MiniRoomType is 3 or 4 or 5)
            {
                int preferredSkinIndex = Math.Clamp(ResolveMiniRoomSpec(snapshot), 0, Math.Max(0, assets.ShopBoards.Length - 1));
                if (preferredSkinIndex <= 0 && assets.ShopBoards.Length > 1)
                {
                    preferredSkinIndex = 1;
                }

                return assets.ShopBoards.Length > preferredSkinIndex && assets.ShopBoards[preferredSkinIndex] != null
                    ? assets.ShopBoards[preferredSkinIndex]
                    : assets.Background;
            }

            return assets.PointedBackground ?? assets.Background;
        }

        private static Texture2D ResolveMiniRoomStatusTexture(SocialRoomFieldActorSnapshot snapshot, MiniRoomBalloonAssets assets)
        {
            if (snapshot == null || assets == null)
            {
                return null;
            }

            string stateKey = snapshot.StateKey ?? string.Empty;
            if (stateKey.IndexOf("expired", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("closed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return assets.Disable;
            }

            if (stateKey.IndexOf("progress", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("restock", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("updating", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("claim", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("sold", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return assets.Progress;
            }

            return assets.Able;
        }

        private static int ResolveMiniRoomBalloonVerticalAdjustment(SocialRoomFieldActorSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return 0;
            }

            if (snapshot.MiniRoomType == 5
                || (snapshot.MiniRoomType == 4 && ResolveMiniRoomSpec(snapshot) > 0))
            {
                return ShopMiniRoomBalloonRaisedOffset;
            }

            return 0;
        }

        private static byte ResolveMiniRoomSpec(SocialRoomFieldActorSnapshot snapshot)
        {
            return snapshot?.MiniRoomBalloonByte0 ?? 0;
        }

        private static byte ResolveMiniRoomCurrentUsers(SocialRoomFieldActorSnapshot snapshot)
        {
            return snapshot?.MiniRoomBalloonByte1 ?? 0;
        }

        private static byte ResolveMiniRoomMaxUsers(SocialRoomFieldActorSnapshot snapshot)
        {
            return snapshot?.MiniRoomBalloonByte2 ?? 0;
        }

        private static void DrawMiniRoomBalloonCount(SpriteBatch spriteBatch, IReadOnlyList<Texture2D> digits, byte value, int x, int y)
        {
            if (spriteBatch == null || digits == null)
            {
                return;
            }

            int normalizedValue = Math.Clamp((int)value, 0, 4);
            if (normalizedValue <= 0 || normalizedValue >= digits.Count)
            {
                return;
            }

            Texture2D digitTexture = digits[normalizedValue];
            if (digitTexture != null)
            {
                spriteBatch.Draw(digitTexture, new Vector2(x, y), Color.White);
            }
        }

        private void DrawMiniRoomBalloonText(SpriteBatch spriteBatch, SpriteFont font, int boardX, int boardY)
        {
            string headline = string.IsNullOrWhiteSpace(_activeSnapshot.MiniRoomBalloonTitle)
                ? _activeSnapshot.Headline
                : _activeSnapshot.MiniRoomBalloonTitle;
            string ownerName = ExtractOwnerName(_activeSnapshot.Detail);
            if (string.IsNullOrWhiteSpace(headline) && string.IsNullOrWhiteSpace(ownerName))
            {
                return;
            }

            const int titleCenterX = 78;
            const int headlineY = 42;
            const int ownerY = 61;

            if (!string.IsNullOrWhiteSpace(headline))
            {
                Vector2 headlineSize = font.MeasureString(headline) * HeadlineScale;
                spriteBatch.DrawString(
                    font,
                    headline,
                    new Vector2(boardX + titleCenterX - (headlineSize.X / 2f), boardY + headlineY),
                    Color.Black,
                    0f,
                    Vector2.Zero,
                    HeadlineScale,
                    SpriteEffects.None,
                    0f);
            }

            if (!string.IsNullOrWhiteSpace(ownerName))
            {
                Vector2 ownerSize = font.MeasureString(ownerName) * DetailScale;
                spriteBatch.DrawString(
                    font,
                    ownerName,
                    new Vector2(boardX + titleCenterX - (ownerSize.X / 2f), boardY + ownerY),
                    new Color(72, 72, 72),
                    0f,
                    Vector2.Zero,
                    DetailScale,
                    SpriteEffects.None,
                    0f);
            }
        }

        private static string ExtractOwnerName(string detail)
        {
            if (string.IsNullOrWhiteSpace(detail))
            {
                return string.Empty;
            }

            int separatorIndex = detail.IndexOf('|');
            return separatorIndex >= 0
                ? detail[..separatorIndex].Trim()
                : detail.Trim();
        }

        private sealed class EmployeeTemplateProfile
        {
            private EmployeeTemplateProfile(
                string npcId,
                int speakDurationMs,
                int minIdleDurationMs,
                int maxIdleDurationMs,
                string[] speakActions,
                string[] idleActions,
                string[] activeActions,
                string[] restockActions,
                string[] ledgerActions,
                string[] expiredActions)
            {
                NpcId = npcId;
                SpeakDurationMs = Math.Max(0, speakDurationMs);
                MinIdleDurationMs = Math.Max(250, minIdleDurationMs);
                MaxIdleDurationMs = Math.Max(MinIdleDurationMs, maxIdleDurationMs);
                SpeakActions = speakActions ?? Array.Empty<string>();
                IdleActions = idleActions ?? Array.Empty<string>();
                ActiveActions = activeActions ?? Array.Empty<string>();
                RestockActions = restockActions ?? Array.Empty<string>();
                LedgerActions = ledgerActions ?? Array.Empty<string>();
                ExpiredActions = expiredActions ?? Array.Empty<string>();
            }

            public static EmployeeTemplateProfile Merchant { get; } = new(
                MerchantNpcId,
                speakDurationMs: 900,
                minIdleDurationMs: 1350,
                maxIdleDurationMs: 2600,
                speakActions: new[] { "say" },
                idleActions: new[] { AnimationKeys.Stand, "eye", "ear", "potion" },
                activeActions: new[] { "eye", AnimationKeys.Stand, "ear" },
                restockActions: new[] { "potion", "ear", AnimationKeys.Stand },
                ledgerActions: new[] { "ear", "eye", AnimationKeys.Stand },
                expiredActions: new[] { AnimationKeys.Stand });

            public static EmployeeTemplateProfile StoreBanker { get; } = new(
                StoreBankerNpcId,
                speakDurationMs: 900,
                minIdleDurationMs: 1600,
                maxIdleDurationMs: 2800,
                speakActions: new[] { "say0", AnimationKeys.Stand },
                idleActions: new[] { AnimationKeys.Stand },
                activeActions: new[] { AnimationKeys.Stand },
                restockActions: new[] { AnimationKeys.Stand },
                ledgerActions: new[] { AnimationKeys.Stand },
                expiredActions: new[] { "say0", AnimationKeys.Stand });

            public static EmployeeTemplateProfile CashEmployee { get; } = CreateCashEmployee(Array.Empty<string>());

            public static EmployeeTemplateProfile CreateCashEmployee(IEnumerable<string> actionNames)
            {
                List<string> orderedActions = NormalizeOrderedActions(actionNames);

                return new EmployeeTemplateProfile(
                    MerchantNpcId,
                    speakDurationMs: 900,
                    minIdleDurationMs: 1250,
                    maxIdleDurationMs: 2400,
                    speakActions: BuildPreferredActions(
                        orderedActions,
                        fallbackToRemainingActions: true,
                        "say",
                        "say0",
                        "speak",
                        "hand",
                        "smile",
                        AnimationKeys.Stand),
                    idleActions: BuildPreferredActions(
                        orderedActions,
                        fallbackToRemainingActions: true,
                        AnimationKeys.Stand,
                        "smile",
                        "eye",
                        "wink",
                        "ear",
                        "potion",
                        "hand",
                        "item",
                        "meso",
                        "exp"),
                    activeActions: BuildPreferredActions(
                        orderedActions,
                        fallbackToRemainingActions: true,
                        "item",
                        "hand",
                        "smile",
                        "eye",
                        "wink",
                        AnimationKeys.Stand,
                        "ear"),
                    restockActions: BuildPreferredActions(
                        orderedActions,
                        fallbackToRemainingActions: true,
                        "item",
                        "hand",
                        "wink",
                        "smile",
                        "eye",
                        "potion",
                        AnimationKeys.Stand),
                    ledgerActions: BuildPreferredActions(
                        orderedActions,
                        fallbackToRemainingActions: true,
                        "meso",
                        "exp",
                        "item",
                        "hand",
                        "eye",
                        "smile",
                        "ear",
                        AnimationKeys.Stand),
                    expiredActions: BuildPreferredActions(
                        orderedActions,
                        fallbackToRemainingActions: true,
                        "fail",
                        "say0",
                        "say",
                        "hand",
                        "smile",
                        AnimationKeys.Stand));
            }

            private static List<string> NormalizeOrderedActions(IEnumerable<string> actionNames)
            {
                List<string> resolved = new();
                if (actionNames == null)
                {
                    return resolved;
                }

                foreach (string actionName in actionNames)
                {
                    if (string.IsNullOrWhiteSpace(actionName))
                    {
                        continue;
                    }

                    string normalized = actionName.Trim().ToLowerInvariant();
                    if (!resolved.Contains(normalized, StringComparer.Ordinal))
                    {
                        resolved.Add(normalized);
                    }
                }

                return resolved;
            }

            private static string[] BuildPreferredActions(
                IReadOnlyList<string> orderedActions,
                bool fallbackToRemainingActions,
                params string[] candidates)
            {
                List<string> resolved = new();
                HashSet<string> preferredActions = new(StringComparer.Ordinal);
                for (int i = 0; i < candidates.Length; i++)
                {
                    string candidate = candidates[i];
                    if (string.IsNullOrWhiteSpace(candidate))
                    {
                        continue;
                    }

                    preferredActions.Add(candidate.Trim().ToLowerInvariant());
                }

                if (orderedActions != null)
                {
                    for (int i = 0; i < orderedActions.Count; i++)
                    {
                        string action = orderedActions[i];
                        if (string.IsNullOrWhiteSpace(action))
                        {
                            continue;
                        }

                        if (preferredActions.Contains(action)
                            && !resolved.Contains(action, StringComparer.Ordinal))
                        {
                            resolved.Add(action);
                        }
                    }
                }

                if (resolved.Count == 0)
                {
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        string candidate = candidates[i];
                        if (string.IsNullOrWhiteSpace(candidate))
                        {
                            continue;
                        }

                        string normalized = candidate.Trim().ToLowerInvariant();
                        if (!resolved.Contains(normalized, StringComparer.Ordinal))
                        {
                            resolved.Add(normalized);
                        }
                    }
                }

                if (fallbackToRemainingActions && orderedActions != null)
                {
                    for (int i = 0; i < orderedActions.Count; i++)
                    {
                        string action = orderedActions[i];
                        if (string.IsNullOrWhiteSpace(action) || resolved.Contains(action, StringComparer.Ordinal))
                        {
                            continue;
                        }

                        resolved.Add(action);
                    }
                }

                if (resolved.Count == 0)
                {
                    resolved.Add(AnimationKeys.Stand);
                }

                return resolved.ToArray();
            }

            public string NpcId { get; }
            public int SpeakDurationMs { get; }
            public int MinIdleDurationMs { get; }
            public int MaxIdleDurationMs { get; }
            public string[] SpeakActions { get; }
            public string[] IdleActions { get; }
            public string[] ActiveActions { get; }
            public string[] RestockActions { get; }
            public string[] LedgerActions { get; }
            public string[] ExpiredActions { get; }
        }

        private sealed class EmployeeActionCatalog
        {
            private const string BaseActionName = AnimationKeys.Stand;

            private EmployeeActionCatalog(IReadOnlyList<Entry> actions, IReadOnlyList<string> orderedActionNames)
            {
                Actions = actions ?? Array.Empty<Entry>();
                OrderedActionNames = orderedActionNames ?? Array.Empty<string>();
            }

            internal static EmployeeActionCatalog Create(WzImageProperty employeeRoot)
            {
                if (employeeRoot == null)
                {
                    return null;
                }

                List<Entry> actions = new();
                List<string> orderedActionNames = new();
                HashSet<string> seenActionNames = new(StringComparer.Ordinal);
                int nextTemplateActionIndex = 1;
                foreach (WzImageProperty childProperty in employeeRoot.WzProperties)
                {
                    if (childProperty == null || string.IsNullOrWhiteSpace(childProperty.Name))
                    {
                        continue;
                    }

                    string normalizedActionName = childProperty.Name.Trim().ToLowerInvariant();
                    if (!seenActionNames.Add(normalizedActionName))
                    {
                        continue;
                    }

                    int clientActionIndex = string.Equals(normalizedActionName, BaseActionName, StringComparison.Ordinal)
                        ? 0
                        : nextTemplateActionIndex++;
                    actions.Add(new Entry(normalizedActionName, clientActionIndex, childProperty));
                    orderedActionNames.Add(normalizedActionName);
                }

                return actions.Count == 0
                    ? null
                    : new EmployeeActionCatalog(actions, orderedActionNames);
            }

            internal IReadOnlyList<Entry> Actions { get; }
            internal IReadOnlyList<string> OrderedActionNames { get; }

            internal sealed class Entry
            {
                internal Entry(string actionName, int clientActionIndex, WzImageProperty actionProperty)
                {
                    ActionName = actionName ?? string.Empty;
                    ClientActionIndex = Math.Max(0, clientActionIndex);
                    ActionProperty = actionProperty;
                }

                internal string ActionName { get; }
                internal int ClientActionIndex { get; }
                internal WzImageProperty ActionProperty { get; }
            }
        }

        private sealed class MiniRoomBalloonAssets
        {
            public Texture2D Background { get; init; }
            public Texture2D PointedBackground { get; init; }
            public Texture2D PersonalShopIcon { get; init; }
            public Texture2D Able { get; init; }
            public Texture2D Disable { get; init; }
            public Texture2D Progress { get; init; }
            public Texture2D[] ShopBoards { get; init; } = Array.Empty<Texture2D>();
            public Texture2D[] CurrentCountDigits { get; init; } = Array.Empty<Texture2D>();
            public Texture2D[] MaxCountDigits { get; init; } = Array.Empty<Texture2D>();

            public bool IsLoaded =>
                (PointedBackground != null || Background != null || ShopBoards.Any(texture => texture != null))
                && PersonalShopIcon != null
                && Able != null
                && Disable != null
                && Progress != null
                && CurrentCountDigits.Length >= 5
                && MaxCountDigits.Length >= 5;
        }

        private void DrawSign(
            SpriteBatch spriteBatch,
            SpriteFont font,
            Texture2D panelTexture,
            int mapShiftX,
            int mapShiftY,
            int mapCenterX,
            int mapCenterY)
        {
            string headline = _activeSnapshot.Headline;
            string detail = _activeSnapshot.Detail;
            if (string.IsNullOrWhiteSpace(headline) && string.IsNullOrWhiteSpace(detail))
            {
                return;
            }

            float headlineLineHeight = font.LineSpacing * HeadlineScale;
            float detailLineHeight = font.LineSpacing * DetailScale;
            Vector2 headlineSize = string.IsNullOrWhiteSpace(headline) ? Vector2.Zero : font.MeasureString(headline) * HeadlineScale;
            Vector2 detailSize = string.IsNullOrWhiteSpace(detail) ? Vector2.Zero : font.MeasureString(detail) * DetailScale;
            float contentWidth = Math.Max(headlineSize.X, detailSize.X);
            int boxWidth = Math.Max(84, (int)Math.Ceiling(contentWidth) + 18);
            int boxHeight = (int)Math.Ceiling(headlineLineHeight + detailLineHeight) + 14;

            int actorScreenX = _activeActor.CurrentX - mapShiftX + mapCenterX;
            int actorScreenY = _activeActor.CurrentY - mapShiftY + mapCenterY;
            int boxX = actorScreenX - (boxWidth / 2);
            int boxY = actorScreenY - _activeActor.NpcInstance.Height - boxHeight - SignVerticalOffset;
            Rectangle box = new(boxX, boxY, boxWidth, boxHeight);

            if (panelTexture != null)
            {
                spriteBatch.Draw(panelTexture, box, SignPanelColor);
                spriteBatch.Draw(panelTexture, new Rectangle(box.Left, box.Top, box.Width, 1), SignBorderColor);
                spriteBatch.Draw(panelTexture, new Rectangle(box.Left, box.Bottom - 1, box.Width, 1), SignBorderColor);
                spriteBatch.Draw(panelTexture, new Rectangle(box.Left, box.Top, 1, box.Height), SignBorderColor);
                spriteBatch.Draw(panelTexture, new Rectangle(box.Right - 1, box.Top, 1, box.Height), SignBorderColor);
            }

            float textY = boxY + 6f;
            if (!string.IsNullOrWhiteSpace(headline))
            {
                spriteBatch.DrawString(
                    font,
                    headline,
                    new Vector2(actorScreenX - (headlineSize.X / 2f), textY),
                    SignHeadlineColor,
                    0f,
                    Vector2.Zero,
                    HeadlineScale,
                    SpriteEffects.None,
                    0f);
                textY += headlineLineHeight;
            }

            if (!string.IsNullOrWhiteSpace(detail))
            {
                spriteBatch.DrawString(
                    font,
                    detail,
                    new Vector2(actorScreenX - (detailSize.X / 2f), textY),
                    SignDetailColor,
                    0f,
                    Vector2.Zero,
                    DetailScale,
                    SpriteEffects.None,
                    0f);
            }
        }
    }
}
