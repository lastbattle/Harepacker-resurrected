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
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class SocialRoomEmployeeActorRuntime
    {
        private const int HiredMerchantNameTagStringPoolId = 0x0DA6;
        private const string HiredMerchantNameTagFallbackFormat = "{0}'s Hired Merchant";
        private const string MerchantNpcId = "9071001";
        private const string StoreBankerNpcId = "9030000";
        private const int SignVerticalOffset = 34;
        private const int MiniRoomBalloonVerticalOffset = 10;
        private const int ShopMiniRoomBalloonRaisedOffset = 7;
        private const int MiniRoomLayoutBaselineBoardWidth = 166;
        private const int MiniRoomLayoutBaselineBoardHeight = 166;
        private const int MiniRoomLayoutBaselineSignboardOriginX = 14;
        private const int MiniRoomLayoutBaselineSignboardOriginY = 17;
        private const int MiniRoomLayoutLegacyIconX = 12;
        private const int MiniRoomLayoutLegacyIconY = 83;
        private const int MiniRoomLayoutLegacyCurrentCountX = 29;
        private const int MiniRoomLayoutLegacyCurrentCountY = 85;
        private const int MiniRoomLayoutLegacyMaxCountX = 46;
        private const int MiniRoomLayoutLegacyMaxCountY = 85;
        private const int MiniRoomLayoutLegacyStatusX = 97;
        private const int MiniRoomLayoutLegacyStatusY = 84;
        private const int MiniRoomLayoutLegacyTitleCenterX = 78;
        private const int MiniRoomLayoutLegacyHeadlineY = 42;
        private const int MiniRoomLayoutLegacyOwnerY = 61;
        private const int MiniRoomTitleClientLineWidth = 100;
        private const int MiniRoomTitleSecondLineOffsetY = 14;
        private const int NameTagVerticalOffset = 8;
        private const int NameTagMinimumWidth = 58;
        private const int NameTagHorizontalPadding = 18;
        private const int NameTagTextInsetY = 2;
        private const float HeadlineScale = 0.48f;
        private const float DetailScale = 0.42f;
        private const float NameTagScale = 0.5f;
        private const int ClientEmployeeActionRandomModulo = 50;
        private const int ClientEmployeeDefaultFrameDelayMs = 180;
        private const int MaxEmployeeActionCacheEntries = 512;
        private const int MaxEmployeeImageCacheEntries = 128;
        private const int ClientEmployeeCacheSweepIntervalMs = 60000;
        private const int ClientEmployeeCacheEntryLifetimeMs = 300000;

        private static readonly Color SignHeadlineColor = new(255, 242, 176);
        private static readonly Color SignDetailColor = new(244, 244, 244);
        private static readonly Color SignPanelColor = new(28, 22, 18, 210);
        private static readonly Color SignBorderColor = new(180, 138, 69, 255);

        private sealed class EmployeeImageEntry
        {
            internal EmployeeImageEntry(
                int templateId,
                string imagePath,
                WzImage imageRoot,
                WzImageProperty templateRoot,
                WzImageProperty propertyRoot,
                int lastAccessTickMs)
            {
                TemplateId = Math.Max(0, templateId);
                ImagePath = imagePath ?? string.Empty;
                ImageRoot = imageRoot;
                TemplateRoot = templateRoot;
                PropertyRoot = propertyRoot;
                LastAccessTickMs = lastAccessTickMs;
            }

            internal int TemplateId { get; }
            internal string ImagePath { get; }
            internal WzImage ImageRoot { get; }
            internal WzImageProperty TemplateRoot { get; }
            internal WzImageProperty PropertyRoot { get; }
            internal int LastAccessTickMs { get; set; }
        }

        private readonly Dictionary<string, NpcItem> _actorCache = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EmployeeTemplateProfile> _cashProfileCache = new(StringComparer.Ordinal);
        private readonly Dictionary<int, EmployeeImageEntry> _cashEmployeeImgEntryCache = new();
        private readonly Dictionary<int, EmployeeActionCatalog> _cashActionCatalogCache = new();
        private readonly Dictionary<int, NameTagAssets> _cashEmployeeNameTagCache = new();
        private readonly HashSet<int> _cashEmployeeNameTagMissingTemplates = new();
        private readonly Dictionary<int, EmployeeMiniRoomBoardAssets> _cashEmployeeMiniRoomBoardCache = new();
        private readonly HashSet<int> _cashEmployeeMiniRoomBoardMissingTemplates = new();
        private readonly Dictionary<uint, EmployeeActionCacheEntry> _cashActionCache = new();
        private readonly Dictionary<string, uint> _cashActionKeyByName = new(StringComparer.Ordinal);
        private readonly ConcurrentBag<WzObject> _usedProps = new();
        private readonly Random _random = new();
        private MiniRoomBalloonAssets _miniRoomBalloonAssets;
        private NameTagAssets _defaultNameTagAssets;
        private NameTagAssets _activeNameTagAssets;
        private EmployeeMiniRoomBoardAssets _activeMiniRoomBoardAssets;
        private int _lastEmployeeCacheSweepTickMs;

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
            _activeNameTagAssets = null;
            _activeMiniRoomBoardAssets = null;
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

            SweepEmployeeCachesIfNeeded();

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
            _activeNameTagAssets = ResolveNameTagAssets(snapshot, device);
            _activeMiniRoomBoardAssets = ResolveMiniRoomBoardAssets(snapshot, device);
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

            DrawEmployeeNameTag(spriteBatch, font, mapShiftX, mapShiftY, mapCenterX, mapCenterY);

            if (_activeSnapshot.HasMiniRoomBalloon && DrawMiniRoomBalloon(spriteBatch, font, mapShiftX, mapShiftY, mapCenterX, mapCenterY, tickCount))
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
            actor.IdleActionCyclingEnabled = false;
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
                hasQuestCheckContext: questStateProvider != null || questRecordValueProvider != null,
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

            EmployeeImageEntry employeeImgEntry = ResolveEmployeeImgEntry(templateId);
            EmployeeActionCatalog catalog = EmployeeActionCatalog.Create(employeeImgEntry);
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
            bool useContextualIdleBias =
                preferContextualAction
                && !profile.UsesClientIndexedActionCycle;
            string nextIdleAction = useContextualIdleBias
                ? ResolveContextualIdleAction(actorKey, actor, snapshot, profile)
                    ?? ResolveClientCycleAction(actorKey, actor, profile)
                    ?? ResolveNextAvailableAction(actorKey, actor, profile.IdleActions)
                : ResolveClientCycleAction(actorKey, actor, profile)
                    ?? ResolveNextAvailableAction(actorKey, actor, profile.IdleActions);
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

        private string ResolveClientCycleAction(string actorKey, NpcItem actor, EmployeeTemplateProfile profile)
        {
            if (profile?.CycleActions == null || profile.CycleActions.Length == 0)
            {
                return null;
            }

            if (profile.UsesClientIndexedActionCycle)
            {
                return ResolveClientIndexedAction(actor, profile.CycleActions);
            }

            return ResolveNextAvailableAction(actorKey, actor, profile.CycleActions);
        }

        private string ResolveClientIndexedAction(NpcItem actor, IReadOnlyList<string> candidates)
        {
            if (actor == null || candidates == null || candidates.Count == 0)
            {
                return null;
            }

            return SelectClientIndexedEmployeeAction(
                candidates,
                _random.Next(ClientEmployeeActionRandomModulo),
                actor.HasAction);
        }

        internal static string SelectClientIndexedEmployeeActionForTesting(IReadOnlyList<string> actions, int randomModuloValue)
        {
            return SelectClientIndexedEmployeeAction(actions, randomModuloValue, action => true);
        }

        internal static string SelectClientIndexedEmployeeActionForTesting(
            IReadOnlyList<string> actions,
            IReadOnlySet<string> availableActions,
            int randomModuloValue)
        {
            return SelectClientIndexedEmployeeAction(
                actions,
                randomModuloValue,
                action => availableActions?.Contains(action) == true);
        }

        private static string SelectClientIndexedEmployeeAction(
            IReadOnlyList<string> actions,
            int randomModuloValue,
            Func<string, bool> isActionAvailable)
        {
            if (actions == null || actions.Count == 0)
            {
                return null;
            }

            int actionIndex = ResolveClientEmployeeActionIndex(actions.Count, randomModuloValue);
            string selectedAction = NormalizeActionName(actions[actionIndex]);
            if (IsSelectableAction(selectedAction, isActionAvailable))
            {
                return selectedAction;
            }

            string standAction = NormalizeActionName(AnimationKeys.Stand);
            if (IsSelectableAction(standAction, isActionAvailable))
            {
                return standAction;
            }

            for (int offset = 1; offset < actions.Count; offset++)
            {
                int candidateIndex = (actionIndex + offset) % actions.Count;
                string candidate = NormalizeActionName(actions[candidateIndex]);
                if (IsSelectableAction(candidate, isActionAvailable))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool IsSelectableAction(string action, Func<string, bool> isActionAvailable)
        {
            return !string.IsNullOrWhiteSpace(action)
                && (isActionAvailable?.Invoke(action) ?? true);
        }

        private static string NormalizeActionName(string action)
        {
            return string.IsNullOrWhiteSpace(action) ? string.Empty : action.Trim().ToLowerInvariant();
        }

        private static int ResolveClientEmployeeActionIndex(int actionCount, int randomModuloValue)
        {
            if (actionCount <= 0)
            {
                return 0;
            }

            int normalizedRandomValue = Math.Abs(randomModuloValue) % ClientEmployeeActionRandomModulo;
            return normalizedRandomValue % actionCount;
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

        private EmployeeImageEntry ResolveEmployeeImgEntry(int templateId)
        {
            if (templateId <= 0)
            {
                return null;
            }

            if (_cashEmployeeImgEntryCache.TryGetValue(templateId, out EmployeeImageEntry cachedEntry))
            {
                TouchEmployeeImageEntryCacheEntry(cachedEntry);
                return cachedEntry;
            }

            string imagePath = FormatEmployeeImagePath(templateId);
            WzImage itemImage = global::HaCreator.Program.FindImage("Item", imagePath);
            if (itemImage == null)
            {
                return null;
            }

            itemImage.ParseImage();
            WzImageProperty employeeRoot = ResolveLinkedProperty(itemImage[templateId.ToString("D8")]?["employee"]);
            if (employeeRoot == null)
            {
                return null;
            }

            WzImageProperty templateRoot = itemImage[templateId.ToString("D8")];
            EmployeeImageEntry entry = new(
                templateId,
                imagePath,
                itemImage,
                templateRoot,
                employeeRoot,
                GetClientTickCountMs());
            _cashEmployeeImgEntryCache[templateId] = entry;
            TrimEmployeeImageEntryCacheIfNeeded();
            return entry;
        }

        private static string FormatEmployeeImagePath(int templateId)
        {
            return $"Cash/{Math.Max(0, templateId) / 10000:D4}.img";
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

            uint cacheKey = BuildEmployeeActionCacheKey(templateId, actionEntry.ClientActionIndex);
            string actionCacheAlias = BuildEmployeeActionCacheAlias(templateId, actionEntry.ActionName);

            if (_cashActionCache.TryGetValue(cacheKey, out EmployeeActionCacheEntry cachedEntry)
                && string.Equals(cachedEntry.ActionName, actionEntry.ActionName, StringComparison.Ordinal))
            {
                TouchEmployeeActionCacheEntry(cacheKey, cachedEntry, actionCacheAlias);
                return cachedEntry.Frames;
            }

            if (_cashActionKeyByName.TryGetValue(actionCacheAlias, out uint aliasedCacheKey)
                && _cashActionCache.TryGetValue(aliasedCacheKey, out EmployeeActionCacheEntry aliasedEntry)
                && string.Equals(aliasedEntry.ActionName, actionEntry.ActionName, StringComparison.Ordinal))
            {
                TouchEmployeeActionCacheEntry(aliasedCacheKey, aliasedEntry, actionCacheAlias);
                return aliasedEntry.Frames;
            }

            var frames = new List<IDXObject>();
            foreach (WzImageProperty childProperty in actionEntry.ActionProperty.WzProperties.OrderBy(GetFrameOrder))
            {
                WzCanvasProperty canvas = ResolveCanvasProperty(childProperty);
                if (canvas == null)
                {
                    continue;
                }

                IDXObject frame = CreateEmployeeFrame(texturePool, canvas, device, defaultDelay: ClientEmployeeDefaultFrameDelayMs);
                if (frame != null)
                {
                    frames.Add(frame);
                }
            }

            EmployeeActionCacheEntry entry = new(templateId, actionEntry.ActionName, frames, GetClientTickCountMs());
            _cashActionCache[cacheKey] = entry;
            _cashActionKeyByName[actionCacheAlias] = cacheKey;
            TrimEmployeeActionCacheIfNeeded();
            return entry.Frames;
        }

        internal static ulong BuildEmployeeActionCacheKeyForTesting(int templateId, int actionIndex)
        {
            return BuildEmployeeActionCacheKey(templateId, actionIndex);
        }

        internal static IReadOnlyList<string> NormalizeClientEmployeeActionNamesForTesting(IEnumerable<string> actionNames)
        {
            return EmployeeActionCatalog.NormalizeClientActionNames(actionNames);
        }

        private static uint BuildEmployeeActionCacheKey(int templateId, int actionIndex)
        {
            uint normalizedTemplateId = (uint)Math.Max(0, templateId);
            uint normalizedAction = (uint)Math.Max(0, actionIndex);
            return (normalizedTemplateId << 8) | normalizedAction;
        }

        private static string BuildEmployeeActionCacheAlias(int templateId, string actionName)
        {
            return $"{Math.Max(0, templateId)}:{NormalizeActionName(actionName)}";
        }

        private void TouchEmployeeImageEntryCacheEntry(EmployeeImageEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            entry.LastAccessTickMs = GetClientTickCountMs();
            _cashEmployeeImgEntryCache[entry.TemplateId] = entry;
        }

        private void TrimEmployeeImageEntryCacheIfNeeded()
        {
            if (_cashEmployeeImgEntryCache.Count <= MaxEmployeeImageCacheEntries)
            {
                return;
            }

            int currentTickMs = GetClientTickCountMs();
            int removeCount = Math.Max(1, _cashEmployeeImgEntryCache.Count - MaxEmployeeImageCacheEntries);
            KeyValuePair<int, EmployeeImageEntry>[] toRemove = _cashEmployeeImgEntryCache
                .OrderByDescending(pair => pair.Value == null ? uint.MaxValue : GetElapsedCacheTimeMs(currentTickMs, pair.Value.LastAccessTickMs))
                .ThenBy(pair => pair.Key)
                .Take(removeCount)
                .ToArray();

            for (int i = 0; i < toRemove.Length; i++)
            {
                int templateId = toRemove[i].Key;
                _cashEmployeeImgEntryCache.Remove(templateId);
                _cashActionCatalogCache.Remove(templateId);
                _cashEmployeeNameTagCache.Remove(templateId);
                _cashEmployeeNameTagMissingTemplates.Remove(templateId);
                _cashEmployeeMiniRoomBoardCache.Remove(templateId);
                _cashEmployeeMiniRoomBoardMissingTemplates.Remove(templateId);
                PurgeEmployeeActionCacheForTemplate(templateId);
            }
        }

        private void TouchEmployeeActionCacheEntry(uint cacheKey, EmployeeActionCacheEntry entry, string actionCacheAlias)
        {
            if (entry == null)
            {
                return;
            }

            entry.LastAccessTickMs = GetClientTickCountMs();
            _cashActionCache[cacheKey] = entry;
            if (!string.IsNullOrWhiteSpace(actionCacheAlias))
            {
                _cashActionKeyByName[actionCacheAlias] = cacheKey;
            }
        }

        private void TrimEmployeeActionCacheIfNeeded()
        {
            if (_cashActionCache.Count <= MaxEmployeeActionCacheEntries)
            {
                return;
            }

            int currentTickMs = GetClientTickCountMs();
            int removeCount = Math.Max(1, _cashActionCache.Count - MaxEmployeeActionCacheEntries);
            KeyValuePair<uint, EmployeeActionCacheEntry>[] toRemove = _cashActionCache
                .OrderByDescending(pair => pair.Value == null ? uint.MaxValue : GetElapsedCacheTimeMs(currentTickMs, pair.Value.LastAccessTickMs))
                .ThenBy(pair => pair.Key)
                .Take(removeCount)
                .ToArray();

            for (int i = 0; i < toRemove.Length; i++)
            {
                KeyValuePair<uint, EmployeeActionCacheEntry> pair = toRemove[i];
                _cashActionCache.Remove(pair.Key);
                RemoveActionCacheAliasesByCacheKey(pair.Key);
            }
        }

        private void SweepEmployeeCachesIfNeeded()
        {
            int currentTickMs = GetClientTickCountMs();
            if (_lastEmployeeCacheSweepTickMs != 0
                && GetElapsedCacheTimeMs(currentTickMs, _lastEmployeeCacheSweepTickMs) < (uint)ClientEmployeeCacheSweepIntervalMs)
            {
                return;
            }

            _lastEmployeeCacheSweepTickMs = currentTickMs;
            EvictExpiredEmployeeImageEntries(currentTickMs);
            EvictExpiredEmployeeActionEntries(currentTickMs);
        }

        private void EvictExpiredEmployeeImageEntries(int currentTickMs)
        {
            int[] expiredTemplateIds = _cashEmployeeImgEntryCache
                .Where(pair => pair.Value == null || HasCacheEntryExpired(pair.Value.LastAccessTickMs, currentTickMs))
                .Select(pair => pair.Key)
                .ToArray();

            for (int i = 0; i < expiredTemplateIds.Length; i++)
            {
                int templateId = expiredTemplateIds[i];
                _cashEmployeeImgEntryCache.Remove(templateId);
                _cashActionCatalogCache.Remove(templateId);
                _cashEmployeeNameTagCache.Remove(templateId);
                _cashEmployeeNameTagMissingTemplates.Remove(templateId);
                _cashEmployeeMiniRoomBoardCache.Remove(templateId);
                _cashEmployeeMiniRoomBoardMissingTemplates.Remove(templateId);
                PurgeEmployeeActionCacheForTemplate(templateId);
            }
        }

        private void EvictExpiredEmployeeActionEntries(int currentTickMs)
        {
            uint[] expiredKeys = _cashActionCache
                .Where(pair => pair.Value == null || HasCacheEntryExpired(pair.Value.LastAccessTickMs, currentTickMs))
                .Select(pair => pair.Key)
                .ToArray();

            for (int i = 0; i < expiredKeys.Length; i++)
            {
                uint cacheKey = expiredKeys[i];
                _cashActionCache.Remove(cacheKey);
                RemoveActionCacheAliasesByCacheKey(cacheKey);
            }
        }

        private void RemoveActionCacheAliasesByCacheKey(uint cacheKey)
        {
            string[] aliases = _cashActionKeyByName
                .Where(aliasPair => aliasPair.Value == cacheKey)
                .Select(aliasPair => aliasPair.Key)
                .ToArray();
            for (int aliasIndex = 0; aliasIndex < aliases.Length; aliasIndex++)
            {
                _cashActionKeyByName.Remove(aliases[aliasIndex]);
            }
        }

        private static bool HasCacheEntryExpired(int lastAccessTickMs, int currentTickMs)
        {
            return GetElapsedCacheTimeMs(currentTickMs, lastAccessTickMs) >= (uint)ClientEmployeeCacheEntryLifetimeMs;
        }

        private static uint GetElapsedCacheTimeMs(int currentTickMs, int previousTickMs)
        {
            return unchecked((uint)(currentTickMs - previousTickMs));
        }

        internal static IReadOnlyList<int> OrderEmployeeCacheKeysForEvictionTesting(
            IEnumerable<(int Key, int LastAccessTickMs)> entries,
            int currentTickMs)
        {
            if (entries == null)
            {
                return Array.Empty<int>();
            }

            return entries
                .OrderByDescending(entry => GetElapsedCacheTimeMs(currentTickMs, entry.LastAccessTickMs))
                .ThenBy(entry => entry.Key)
                .Select(entry => entry.Key)
                .ToArray();
        }

        private static int GetClientTickCountMs()
        {
            return Environment.TickCount;
        }

        private void PurgeEmployeeActionCacheForTemplate(int templateId)
        {
            int normalizedTemplateId = Math.Max(0, templateId);
            if (normalizedTemplateId <= 0)
            {
                return;
            }

            uint[] keysToRemove = _cashActionCache
                .Where(pair => pair.Value?.TemplateId == normalizedTemplateId)
                .Select(pair => pair.Key)
                .ToArray();
            for (int i = 0; i < keysToRemove.Length; i++)
            {
                _cashActionCache.Remove(keysToRemove[i]);
            }

            string aliasPrefix = $"{normalizedTemplateId}:";
            string[] aliasesToRemove = _cashActionKeyByName.Keys
                .Where(alias => alias.StartsWith(aliasPrefix, StringComparison.Ordinal))
                .ToArray();
            for (int i = 0; i < aliasesToRemove.Length; i++)
            {
                _cashActionKeyByName.Remove(aliasesToRemove[i]);
            }
        }

        private static WzCanvasProperty ResolveCanvasProperty(WzImageProperty property)
        {
            WzImageProperty resolvedProperty = ResolveLinkedProperty(property);
            if (resolvedProperty is WzCanvasProperty canvasProperty)
            {
                return canvasProperty;
            }

            return null;
        }

        private static WzImageProperty ResolveLinkedProperty(WzImageProperty property)
        {
            if (property is WzUOLProperty uol)
            {
                return ResolveLinkedProperty(uol.LinkValue as WzImageProperty);
            }

            if (property is WzStringProperty stringProperty && !string.IsNullOrWhiteSpace(stringProperty.Value))
            {
                return ResolveLinkedProperty(ResolveLinkedPropertyPath(stringProperty.Parent, stringProperty.Value));
            }

            return property;
        }

        private static WzImageProperty ResolveLinkedPropertyPath(WzObject context, string path)
        {
            if (context == null || string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string normalizedPath = path.Replace('\\', '/').Trim();
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return null;
            }

            if (context is WzImageProperty propertyContext)
            {
                WzImageProperty resolvedFromProperty = ResolveLinkedPropertyPath(propertyContext, normalizedPath);
                if (resolvedFromProperty != null)
                {
                    return resolvedFromProperty;
                }
            }

            if (context is WzImage imageContext)
            {
                imageContext.ParseImage();
                return imageContext.GetFromPath(normalizedPath);
            }

            return null;
        }

        private static WzImageProperty ResolveLinkedPropertyPath(WzImageProperty context, string path)
        {
            if (context == null || string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string normalizedPath = path.Replace('\\', '/').Trim();
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return null;
            }

            if (!normalizedPath.Contains('/'))
            {
                return context[normalizedPath] ?? context.ParentImage?.GetFromPath(normalizedPath);
            }

            string[] segments = normalizedPath
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return null;
            }

            WzObject current = normalizedPath.StartsWith("../", StringComparison.Ordinal) ? context.Parent : context;
            foreach (string segment in segments)
            {
                if (string.Equals(segment, ".", StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(segment, "..", StringComparison.Ordinal))
                {
                    current = current?.Parent;
                    continue;
                }

                current = current switch
                {
                    WzImageProperty currentProperty => currentProperty[segment],
                    WzImage currentImage => currentImage.GetFromPath(segment),
                    _ => null
                };

                if (current == null)
                {
                    return context.ParentImage?.GetFromPath(normalizedPath);
                }
            }

            return current as WzImageProperty;
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

        private static Vector2? GetCanvasOriginVector(WzCanvasProperty canvasProperty)
        {
            if (canvasProperty == null)
            {
                return null;
            }

            System.Drawing.PointF origin = canvasProperty.GetCanvasOriginPosition();
            return new Vector2(origin.X, origin.Y);
        }

        private NameTagAssets ResolveNameTagAssets(SocialRoomFieldActorSnapshot snapshot, GraphicsDevice device)
        {
            if (snapshot?.Template == SocialRoomFieldActorTemplate.CashEmployee && snapshot.TemplateId > 0)
            {
                NameTagAssets cashAssets = ResolveCashEmployeeNameTagAssets(snapshot.TemplateId, device);
                if (cashAssets?.IsLoaded == true)
                {
                    return cashAssets;
                }
            }

            EnsureDefaultEmployeeNameTagAssets(device);
            return _defaultNameTagAssets;
        }

        private NameTagAssets ResolveCashEmployeeNameTagAssets(int templateId, GraphicsDevice device)
        {
            if (templateId <= 0 || device == null || device.IsDisposed)
            {
                return null;
            }

            if (_cashEmployeeNameTagCache.TryGetValue(templateId, out NameTagAssets cachedAssets))
            {
                return cachedAssets;
            }

            if (_cashEmployeeNameTagMissingTemplates.Contains(templateId))
            {
                return null;
            }

            EmployeeImageEntry employeeImgEntry = ResolveEmployeeImgEntry(templateId);
            WzImageProperty nameTagSource = ResolveLinkedProperty(employeeImgEntry?.TemplateRoot?["nameTag"]);
            NameTagAssets loadedAssets = LoadNameTagAssets(nameTagSource, device);
            if (loadedAssets?.IsLoaded == true)
            {
                _cashEmployeeNameTagCache[templateId] = loadedAssets;
                return loadedAssets;
            }

            _cashEmployeeNameTagMissingTemplates.Add(templateId);
            return null;
        }

        private EmployeeMiniRoomBoardAssets ResolveMiniRoomBoardAssets(SocialRoomFieldActorSnapshot snapshot, GraphicsDevice device)
        {
            if (snapshot?.Template != SocialRoomFieldActorTemplate.CashEmployee || snapshot.TemplateId <= 0)
            {
                return null;
            }

            return ResolveCashEmployeeMiniRoomBoardAssets(snapshot.TemplateId, device);
        }

        private EmployeeMiniRoomBoardAssets ResolveCashEmployeeMiniRoomBoardAssets(int templateId, GraphicsDevice device)
        {
            if (templateId <= 0 || device == null || device.IsDisposed)
            {
                return null;
            }

            if (_cashEmployeeMiniRoomBoardCache.TryGetValue(templateId, out EmployeeMiniRoomBoardAssets cachedAssets))
            {
                return cachedAssets;
            }

            if (_cashEmployeeMiniRoomBoardMissingTemplates.Contains(templateId))
            {
                return null;
            }

            EmployeeImageEntry employeeImgEntry = ResolveEmployeeImgEntry(templateId);
            WzImageProperty templateRoot = employeeImgEntry?.TemplateRoot;
            WzCanvasProperty signboardCanvas = ResolveMiniRoomBoardCanvas(templateRoot?["skin"]);
            Texture2D signboardTexture = LoadUiCanvasTexture(signboardCanvas, device);
            if (signboardTexture == null)
            {
                _cashEmployeeMiniRoomBoardMissingTemplates.Add(templateId);
                return null;
            }

            EmployeeMiniRoomBoardEffectFrame[] effectFrames = LoadMiniRoomBoardEffectFrames(templateRoot?["effect"], device);
            EmployeeMiniRoomBoardAssets loadedAssets = new()
            {
                Signboard = signboardTexture,
                SignboardOrigin = GetCanvasOriginVector(signboardCanvas),
                EffectFrames = effectFrames,
                TotalEffectDurationMs = effectFrames.Sum(frame => Math.Max(1, frame.DelayMs))
            };
            _cashEmployeeMiniRoomBoardCache[templateId] = loadedAssets;
            return loadedAssets;
        }

        private static WzCanvasProperty ResolveMiniRoomBoardCanvas(WzImageProperty skinSource)
        {
            if (skinSource == null)
            {
                return null;
            }

            return ResolveCanvasProperty(skinSource["signboard"])
                ?? ResolveCanvasProperty(skinSource["shop"])
                ?? ResolveCanvasProperty(skinSource["backgrnd"]);
        }

        internal static string ResolveMiniRoomBoardCanvasNameForTesting(params string[] childNames)
        {
            if (childNames == null || childNames.Length == 0)
            {
                return null;
            }

            HashSet<string> names = new(
                childNames.Where(name => !string.IsNullOrWhiteSpace(name)),
                StringComparer.OrdinalIgnoreCase);
            if (names.Contains("signboard"))
            {
                return "signboard";
            }

            if (names.Contains("shop"))
            {
                return "shop";
            }

            return names.Contains("backgrnd") ? "backgrnd" : null;
        }

        private static EmployeeMiniRoomBoardEffectFrame[] LoadMiniRoomBoardEffectFrames(WzImageProperty source, GraphicsDevice device)
        {
            if (source == null || device == null || device.IsDisposed)
            {
                return Array.Empty<EmployeeMiniRoomBoardEffectFrame>();
            }

            List<EmployeeMiniRoomBoardEffectFrame> frames = new();
            foreach (WzImageProperty child in source.WzProperties.OrderBy(GetFrameOrder))
            {
                WzCanvasProperty canvas = ResolveCanvasProperty(child);
                Texture2D texture = LoadUiCanvasTexture(canvas, device);
                if (texture == null)
                {
                    continue;
                }

                int delayMs = Math.Max(1, GetIntValue(canvas?["delay"]) ?? ClientEmployeeDefaultFrameDelayMs);
                frames.Add(new EmployeeMiniRoomBoardEffectFrame(texture, delayMs, GetCanvasOriginVector(canvas)));
            }

            return frames.ToArray();
        }

        private void EnsureDefaultEmployeeNameTagAssets(GraphicsDevice device)
        {
            if (_defaultNameTagAssets?.IsLoaded == true || device == null || device.IsDisposed)
            {
                return;
            }

            WzImage nameTagImage = global::HaCreator.Program.FindImage("UI", "NameTag.img");
            if (nameTagImage == null)
            {
                return;
            }

            nameTagImage.ParseImage();
            _defaultNameTagAssets = LoadNameTagAssets(nameTagImage["11"] as WzSubProperty, device);
        }

        private static NameTagAssets LoadNameTagAssets(WzImageProperty source, GraphicsDevice device)
        {
            if (source == null || device == null || device.IsDisposed)
            {
                return null;
            }

            int textColorArgb = GetIntValue(source["clr"]) ?? unchecked((int)0xFFFFFF00);
            return new NameTagAssets
            {
                Left = LoadUiCanvasTexture(ResolveCanvasProperty(source["w"]), device),
                Middle = LoadUiCanvasTexture(ResolveCanvasProperty(source["c"]), device),
                Right = LoadUiCanvasTexture(ResolveCanvasProperty(source["e"]), device),
                TextColor = new Color(unchecked((uint)textColorArgb))
            };
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
            int mapCenterY,
            int tickCount)
        {
            MiniRoomBalloonAssets assets = _miniRoomBalloonAssets;
            if (_activeActor == null || _activeSnapshot == null || assets?.IsLoaded != true)
            {
                return false;
            }

            Texture2D boardTexture = ResolveMiniRoomBalloonBoardTexture(_activeSnapshot, assets, _activeMiniRoomBoardAssets);
            if (boardTexture == null)
            {
                return false;
            }

            int actorScreenX = _activeActor.CurrentX - mapShiftX + mapCenterX;
            int actorScreenY = _activeActor.CurrentY - mapShiftY + mapCenterY;
            int verticalAdjustment = ResolveMiniRoomBalloonVerticalAdjustment(_activeSnapshot);
            Vector2 boardAnchor = new(
                actorScreenX,
                actorScreenY
                - _activeActor.NpcInstance.Height
                - MiniRoomBalloonVerticalOffset
                + verticalAdjustment);
            Vector2 boardPosition = ResolveMiniRoomBoardDrawPosition(
                boardAnchor,
                boardTexture.Width,
                boardTexture.Height,
                _activeMiniRoomBoardAssets?.SignboardOrigin);
            MiniRoomBalloonLayout layout = ResolveMiniRoomBalloonLayout(
                boardTexture.Width,
                boardTexture.Height,
                useTemplateLayoutScaling: _activeMiniRoomBoardAssets?.Signboard != null,
                boardOrigin: _activeMiniRoomBoardAssets?.SignboardOrigin);
            int boardX = (int)boardPosition.X;
            int boardY = (int)boardPosition.Y;

            spriteBatch.Draw(boardTexture, boardPosition, Color.White);
            DrawTemplateMiniRoomBoardEffect(spriteBatch, boardAnchor, _activeMiniRoomBoardAssets, tickCount);

            if (assets.PersonalShopIcon != null)
            {
                spriteBatch.Draw(assets.PersonalShopIcon, new Vector2(boardX + layout.IconX, boardY + layout.IconY), Color.White);
            }

            DrawMiniRoomBalloonCount(
                spriteBatch,
                assets.CurrentCountDigits,
                ResolveMiniRoomCurrentUsers(_activeSnapshot),
                boardX + layout.CurrentCountX,
                boardY + layout.CurrentCountY);
            DrawMiniRoomBalloonCount(
                spriteBatch,
                assets.MaxCountDigits,
                ResolveMiniRoomMaxUsers(_activeSnapshot),
                boardX + layout.MaxCountX,
                boardY + layout.MaxCountY);

            Texture2D statusTexture = ResolveMiniRoomStatusTexture(_activeSnapshot, assets);
            if (statusTexture != null)
            {
                spriteBatch.Draw(statusTexture, new Vector2(boardX + layout.StatusX, boardY + layout.StatusY), Color.White);
            }

            DrawMiniRoomBalloonText(spriteBatch, font, boardX, boardY, layout);
            return true;
        }

        private static void DrawTemplateMiniRoomBoardEffect(
            SpriteBatch spriteBatch,
            Vector2 boardAnchor,
            EmployeeMiniRoomBoardAssets templateAssets,
            int tickCount)
        {
            if (spriteBatch == null
                || templateAssets?.EffectFrames == null
                || templateAssets.EffectFrames.Length == 0)
            {
                return;
            }

            int totalDuration = Math.Max(1, templateAssets.TotalEffectDurationMs);
            int localTick = (int)((uint)tickCount % (uint)totalDuration);
            int elapsed = 0;
            for (int i = 0; i < templateAssets.EffectFrames.Length; i++)
            {
                EmployeeMiniRoomBoardEffectFrame frame = templateAssets.EffectFrames[i];
                elapsed += Math.Max(1, frame.DelayMs);
                if (localTick >= elapsed)
                {
                    continue;
                }

                if (frame.Texture != null)
                {
                    Vector2 framePosition = ResolveMiniRoomBoardDrawPosition(
                        boardAnchor,
                        frame.Texture.Width,
                        frame.Texture.Height,
                        frame.Origin ?? templateAssets.SignboardOrigin);
                    spriteBatch.Draw(frame.Texture, framePosition, Color.White);
                }

                break;
            }
        }

        private static Vector2 ResolveMiniRoomBoardDrawPosition(
            Vector2 boardAnchor,
            int boardWidth,
            int boardHeight,
            Vector2? configuredOrigin)
        {
            Vector2 origin = ResolveMiniRoomBoardDrawOrigin(boardWidth, boardHeight, configuredOrigin);
            return new Vector2(boardAnchor.X - origin.X, boardAnchor.Y - origin.Y);
        }

        private static Vector2 ResolveMiniRoomBoardDrawOrigin(
            int boardWidth,
            int boardHeight,
            Vector2? configuredOrigin)
        {
            if (configuredOrigin.HasValue)
            {
                return configuredOrigin.Value;
            }

            return new Vector2(boardWidth / 2f, boardHeight);
        }

        internal static (int X, int Y) ResolveMiniRoomBoardTopLeftForTesting(
            int actorScreenX,
            int actorScreenY,
            int actorHeight,
            int boardWidth,
            int boardHeight,
            int verticalAdjustment,
            int? originX,
            int? originY)
        {
            Vector2 boardAnchor = new(
                actorScreenX,
                actorScreenY - actorHeight - MiniRoomBalloonVerticalOffset + verticalAdjustment);
            Vector2? configuredOrigin = originX.HasValue && originY.HasValue
                ? new Vector2(originX.Value, originY.Value)
                : null;
            Vector2 position = ResolveMiniRoomBoardDrawPosition(
                boardAnchor,
                boardWidth,
                boardHeight,
                configuredOrigin);
            return ((int)position.X, (int)position.Y);
        }

        internal static (int X, int Y) ResolveMiniRoomBoardEffectTopLeftForTesting(
            int boardAnchorX,
            int boardAnchorY,
            int effectWidth,
            int effectHeight,
            int? effectOriginX,
            int? effectOriginY,
            int? boardOriginX,
            int? boardOriginY)
        {
            Vector2? configuredOrigin = effectOriginX.HasValue && effectOriginY.HasValue
                ? new Vector2(effectOriginX.Value, effectOriginY.Value)
                : (boardOriginX.HasValue && boardOriginY.HasValue
                    ? new Vector2(boardOriginX.Value, boardOriginY.Value)
                    : null);
            Vector2 position = ResolveMiniRoomBoardDrawPosition(
                new Vector2(boardAnchorX, boardAnchorY),
                effectWidth,
                effectHeight,
                configuredOrigin);
            return ((int)position.X, (int)position.Y);
        }

        internal static (
            int IconX,
            int IconY,
            int CurrentCountX,
            int CurrentCountY,
            int MaxCountX,
            int MaxCountY,
            int StatusX,
            int StatusY,
            int TitleCenterX,
            int HeadlineY,
            int OwnerY) ResolveMiniRoomBalloonLayoutForTesting(
            int boardWidth,
            int boardHeight,
            bool useTemplateLayoutScaling,
            int? boardOriginX = null,
            int? boardOriginY = null)
        {
            Vector2? boardOrigin = boardOriginX.HasValue && boardOriginY.HasValue
                ? new Vector2(boardOriginX.Value, boardOriginY.Value)
                : null;
            MiniRoomBalloonLayout layout = ResolveMiniRoomBalloonLayout(
                boardWidth,
                boardHeight,
                useTemplateLayoutScaling,
                boardOrigin);
            return (
                layout.IconX,
                layout.IconY,
                layout.CurrentCountX,
                layout.CurrentCountY,
                layout.MaxCountX,
                layout.MaxCountY,
                layout.StatusX,
                layout.StatusY,
                layout.TitleCenterX,
                layout.HeadlineY,
                layout.OwnerY);
        }

        private static MiniRoomBalloonLayout ResolveMiniRoomBalloonLayout(
            int boardWidth,
            int boardHeight,
            bool useTemplateLayoutScaling,
            Vector2? boardOrigin = null)
        {
            return new MiniRoomBalloonLayout(
                IconX: ResolveMiniRoomLayoutAxisOffset(
                    MiniRoomLayoutLegacyIconX,
                    boardWidth,
                    MiniRoomLayoutBaselineBoardWidth,
                    useTemplateLayoutScaling,
                    boardOrigin?.X,
                    MiniRoomLayoutBaselineSignboardOriginX),
                IconY: ResolveMiniRoomLayoutAxisOffset(
                    MiniRoomLayoutLegacyIconY,
                    boardHeight,
                    MiniRoomLayoutBaselineBoardHeight,
                    useTemplateLayoutScaling,
                    boardOrigin?.Y,
                    MiniRoomLayoutBaselineSignboardOriginY),
                CurrentCountX: ResolveMiniRoomLayoutAxisOffset(
                    MiniRoomLayoutLegacyCurrentCountX,
                    boardWidth,
                    MiniRoomLayoutBaselineBoardWidth,
                    useTemplateLayoutScaling,
                    boardOrigin?.X,
                    MiniRoomLayoutBaselineSignboardOriginX),
                CurrentCountY: ResolveMiniRoomLayoutAxisOffset(
                    MiniRoomLayoutLegacyCurrentCountY,
                    boardHeight,
                    MiniRoomLayoutBaselineBoardHeight,
                    useTemplateLayoutScaling,
                    boardOrigin?.Y,
                    MiniRoomLayoutBaselineSignboardOriginY),
                MaxCountX: ResolveMiniRoomLayoutAxisOffset(
                    MiniRoomLayoutLegacyMaxCountX,
                    boardWidth,
                    MiniRoomLayoutBaselineBoardWidth,
                    useTemplateLayoutScaling,
                    boardOrigin?.X,
                    MiniRoomLayoutBaselineSignboardOriginX),
                MaxCountY: ResolveMiniRoomLayoutAxisOffset(
                    MiniRoomLayoutLegacyMaxCountY,
                    boardHeight,
                    MiniRoomLayoutBaselineBoardHeight,
                    useTemplateLayoutScaling,
                    boardOrigin?.Y,
                    MiniRoomLayoutBaselineSignboardOriginY),
                StatusX: ResolveMiniRoomLayoutAxisOffset(
                    MiniRoomLayoutLegacyStatusX,
                    boardWidth,
                    MiniRoomLayoutBaselineBoardWidth,
                    useTemplateLayoutScaling,
                    boardOrigin?.X,
                    MiniRoomLayoutBaselineSignboardOriginX),
                StatusY: ResolveMiniRoomLayoutAxisOffset(
                    MiniRoomLayoutLegacyStatusY,
                    boardHeight,
                    MiniRoomLayoutBaselineBoardHeight,
                    useTemplateLayoutScaling,
                    boardOrigin?.Y,
                    MiniRoomLayoutBaselineSignboardOriginY),
                TitleCenterX: ResolveMiniRoomLayoutAxisOffset(
                    MiniRoomLayoutLegacyTitleCenterX,
                    boardWidth,
                    MiniRoomLayoutBaselineBoardWidth,
                    useTemplateLayoutScaling,
                    boardOrigin?.X,
                    MiniRoomLayoutBaselineSignboardOriginX),
                HeadlineY: ResolveMiniRoomLayoutAxisOffset(
                    MiniRoomLayoutLegacyHeadlineY,
                    boardHeight,
                    MiniRoomLayoutBaselineBoardHeight,
                    useTemplateLayoutScaling,
                    boardOrigin?.Y,
                    MiniRoomLayoutBaselineSignboardOriginY),
                OwnerY: ResolveMiniRoomLayoutAxisOffset(
                    MiniRoomLayoutLegacyOwnerY,
                    boardHeight,
                    MiniRoomLayoutBaselineBoardHeight,
                    useTemplateLayoutScaling,
                    boardOrigin?.Y,
                    MiniRoomLayoutBaselineSignboardOriginY));
        }

        private static int ResolveMiniRoomLayoutAxisOffset(
            int legacyOffset,
            int actualSize,
            int baselineSize,
            bool useTemplateLayoutScaling,
            float? actualOrigin,
            int baselineOrigin)
        {
            if (!useTemplateLayoutScaling || actualSize <= 0 || baselineSize <= 0)
            {
                return legacyOffset;
            }

            float scale = actualSize / (float)baselineSize;
            if (actualOrigin.HasValue)
            {
                float anchoredOffset = actualOrigin.Value + ((legacyOffset - baselineOrigin) * scale);
                return (int)Math.Round(anchoredOffset);
            }

            return (int)Math.Round(legacyOffset * scale);
        }

        private static Texture2D ResolveMiniRoomBalloonBoardTexture(
            SocialRoomFieldActorSnapshot snapshot,
            MiniRoomBalloonAssets assets,
            EmployeeMiniRoomBoardAssets templateAssets)
        {
            if (snapshot == null || assets == null)
            {
                return null;
            }

            if (templateAssets?.Signboard != null)
            {
                return templateAssets.Signboard;
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

            string statusName = ResolveMiniRoomStatusTextureName(snapshot);
            if (string.Equals(statusName, "Disable", StringComparison.Ordinal))
            {
                return assets.Disable;
            }

            return assets.Able;
        }

        internal static string ResolveMiniRoomStatusTextureNameForTesting(SocialRoomFieldActorSnapshot snapshot)
        {
            return ResolveMiniRoomStatusTextureName(snapshot);
        }

        private static string ResolveMiniRoomStatusTextureName(SocialRoomFieldActorSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return string.Empty;
            }

            byte maxUsers = ResolveMiniRoomMaxUsers(snapshot);
            byte currentUsers = ResolveMiniRoomCurrentUsers(snapshot);
            return maxUsers > 0 && currentUsers >= maxUsers
                ? "Disable"
                : "Able";
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

        internal static byte ResolveMiniRoomCurrentUsersForTesting(SocialRoomFieldActorSnapshot snapshot)
        {
            return ResolveMiniRoomCurrentUsers(snapshot);
        }

        internal static byte ResolveMiniRoomMaxUsersForTesting(SocialRoomFieldActorSnapshot snapshot)
        {
            return ResolveMiniRoomMaxUsers(snapshot);
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

        private void DrawMiniRoomBalloonText(
            SpriteBatch spriteBatch,
            SpriteFont font,
            int boardX,
            int boardY,
            MiniRoomBalloonLayout layout)
        {
            string headline = string.IsNullOrWhiteSpace(_activeSnapshot.MiniRoomBalloonTitle)
                ? _activeSnapshot.Headline
                : _activeSnapshot.MiniRoomBalloonTitle;
            string ownerName = ExtractOwnerName(_activeSnapshot.Detail);
            if (string.IsNullOrWhiteSpace(headline) && string.IsNullOrWhiteSpace(ownerName))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(headline))
            {
                IReadOnlyList<string> headlineLines = ResolveMiniRoomBalloonTitleLines(
                    headline,
                    text => font.MeasureString(text).X * HeadlineScale,
                    MiniRoomTitleClientLineWidth);
                for (int i = 0; i < headlineLines.Count; i++)
                {
                    string line = headlineLines[i];
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    Vector2 headlineSize = font.MeasureString(line) * HeadlineScale;
                    spriteBatch.DrawString(
                        font,
                        line,
                        new Vector2(
                            boardX + layout.TitleCenterX - (headlineSize.X / 2f),
                            boardY + layout.HeadlineY + (i * MiniRoomTitleSecondLineOffsetY)),
                        Color.Black,
                        0f,
                        Vector2.Zero,
                        HeadlineScale,
                        SpriteEffects.None,
                        0f);
                }
            }

            if (!string.IsNullOrWhiteSpace(ownerName))
            {
                Vector2 ownerSize = font.MeasureString(ownerName) * DetailScale;
                spriteBatch.DrawString(
                    font,
                    ownerName,
                    new Vector2(boardX + layout.TitleCenterX - (ownerSize.X / 2f), boardY + layout.OwnerY),
                    new Color(72, 72, 72),
                    0f,
                    Vector2.Zero,
                    DetailScale,
                    SpriteEffects.None,
                    0f);
            }
        }

        private static IReadOnlyList<string> ResolveMiniRoomBalloonTitleLines(
            string title,
            Func<string, float> measureWidth,
            float maxLineWidth)
        {
            string normalizedTitle = string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim();
            if (string.IsNullOrWhiteSpace(normalizedTitle))
            {
                return Array.Empty<string>();
            }

            if (measureWidth == null || maxLineWidth <= 0f || measureWidth(normalizedTitle) <= maxLineWidth)
            {
                return new[] { normalizedTitle };
            }

            int firstLineLength = ResolveLongestMiniRoomTitlePrefixLength(normalizedTitle, measureWidth, maxLineWidth);
            if (firstLineLength <= 0 || firstLineLength >= normalizedTitle.Length)
            {
                return new[] { normalizedTitle };
            }

            string firstLine = normalizedTitle[..firstLineLength].TrimEnd();
            string remainingTitle = normalizedTitle[firstLineLength..].TrimStart();
            if (string.IsNullOrWhiteSpace(remainingTitle))
            {
                return new[] { firstLine };
            }

            int secondLineLength = ResolveLongestMiniRoomTitlePrefixLength(remainingTitle, measureWidth, maxLineWidth);
            string secondLine = secondLineLength > 0 && secondLineLength < remainingTitle.Length
                ? remainingTitle[..secondLineLength].TrimEnd()
                : remainingTitle;
            return string.IsNullOrWhiteSpace(secondLine)
                ? new[] { firstLine }
                : new[] { firstLine, secondLine };
        }

        private static int ResolveLongestMiniRoomTitlePrefixLength(
            string text,
            Func<string, float> measureWidth,
            float maxLineWidth)
        {
            int bestLength = 0;
            for (int length = 1; length <= text.Length; length++)
            {
                string candidate = text[..length];
                if (measureWidth(candidate) > maxLineWidth)
                {
                    break;
                }

                bestLength = length;
            }

            return bestLength;
        }

        internal static IReadOnlyList<string> ResolveMiniRoomBalloonTitleLinesForTesting(
            string title,
            int maxCharacters)
        {
            int normalizedMaxCharacters = Math.Max(1, maxCharacters);
            return ResolveMiniRoomBalloonTitleLines(
                title,
                text => text?.Length ?? 0,
                normalizedMaxCharacters);
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

        private void DrawEmployeeNameTag(
            SpriteBatch spriteBatch,
            SpriteFont font,
            int mapShiftX,
            int mapShiftY,
            int mapCenterX,
            int mapCenterY)
        {
            if (_activeActor == null || _activeSnapshot == null || font == null)
            {
                return;
            }

            string nameTagText = ResolveNameTagText(_activeSnapshot);
            if (string.IsNullOrWhiteSpace(nameTagText))
            {
                return;
            }

            int actorScreenX = _activeActor.CurrentX - mapShiftX + mapCenterX;
            int actorScreenY = _activeActor.CurrentY - mapShiftY + mapCenterY;
            NameTagAssets assets = _activeNameTagAssets;
            Vector2 textSize = font.MeasureString(nameTagText) * NameTagScale;

            if (assets?.IsLoaded == true)
            {
                int textWidth = Math.Max(1, (int)Math.Ceiling(textSize.X));
                int totalWidth = Math.Max(
                    NameTagMinimumWidth,
                    Math.Max(assets.Left.Width + assets.Right.Width, textWidth + NameTagHorizontalPadding));
                int left = actorScreenX - (totalWidth / 2);
                int y = actorScreenY - _activeActor.NpcInstance.Height - assets.Left.Height - NameTagVerticalOffset;
                int middleStartX = left + assets.Left.Width;
                int middleEndX = left + totalWidth - assets.Right.Width;

                spriteBatch.Draw(assets.Left, new Vector2(left, y), Color.White);

                for (int offsetX = middleStartX; offsetX < middleEndX; offsetX += assets.Middle.Width)
                {
                    int remainingWidth = middleEndX - offsetX;
                    if (remainingWidth <= 0)
                    {
                        break;
                    }

                    int drawWidth = Math.Min(assets.Middle.Width, remainingWidth);
                    spriteBatch.Draw(
                        assets.Middle,
                        new Rectangle(offsetX, y, drawWidth, assets.Middle.Height),
                        new Rectangle(0, 0, drawWidth, assets.Middle.Height),
                        Color.White);
                }

                spriteBatch.Draw(assets.Right, new Vector2(left + totalWidth - assets.Right.Width, y), Color.White);

                Vector2 textPosition = new(
                    left + ((totalWidth - textSize.X) * 0.5f),
                    y + NameTagTextInsetY);
                DrawOutlinedScaledText(spriteBatch, font, nameTagText, textPosition, assets.TextColor, NameTagScale);
                return;
            }

            Vector2 fallbackPosition = new(
                actorScreenX - (textSize.X * 0.5f),
                actorScreenY - _activeActor.NpcInstance.Height - textSize.Y - NameTagVerticalOffset);
            DrawOutlinedScaledText(spriteBatch, font, nameTagText, fallbackPosition, Color.White, NameTagScale);
        }

        private static string ResolveNameTagText(SocialRoomFieldActorSnapshot snapshot)
        {
            string ownerName = ResolveNameTagOwner(ExtractOwnerName(snapshot?.Detail));
            if (string.IsNullOrWhiteSpace(ownerName))
            {
                return string.Empty;
            }

            if (!ShouldShowLiveEmployeeNameTag(snapshot))
            {
                return string.Empty;
            }

            return FormatHiredMerchantNameTag(ownerName);
        }

        private static string ResolveNameTagOwner(string ownerNameOrTag)
        {
            string candidate = ownerNameOrTag?.Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return string.Empty;
            }

            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                HiredMerchantNameTagStringPoolId,
                HiredMerchantNameTagFallbackFormat,
                maxPlaceholderCount: 1,
                out _);
            return TryExtractOwnerNameFromFormattedNameTag(candidate, format, out string ownerName)
                ? ownerName
                : candidate;
        }

        private static bool TryExtractOwnerNameFromFormattedNameTag(string formattedNameTag, string compositeFormat, out string ownerName)
        {
            ownerName = string.Empty;
            if (string.IsNullOrWhiteSpace(formattedNameTag) || string.IsNullOrWhiteSpace(compositeFormat))
            {
                return false;
            }

            const string PlaceholderToken = "{0}";
            int placeholderIndex = compositeFormat.IndexOf(PlaceholderToken, StringComparison.Ordinal);
            if (placeholderIndex < 0)
            {
                return false;
            }

            string prefix = compositeFormat[..placeholderIndex];
            string suffix = compositeFormat[(placeholderIndex + PlaceholderToken.Length)..];
            if (!formattedNameTag.StartsWith(prefix, StringComparison.Ordinal)
                || !formattedNameTag.EndsWith(suffix, StringComparison.Ordinal)
                || formattedNameTag.Length < prefix.Length + suffix.Length)
            {
                return false;
            }

            int ownerLength = formattedNameTag.Length - prefix.Length - suffix.Length;
            string extractedOwnerName = formattedNameTag.Substring(prefix.Length, ownerLength).Trim();
            if (string.IsNullOrWhiteSpace(extractedOwnerName))
            {
                return false;
            }

            ownerName = extractedOwnerName;
            return true;
        }

        private static bool ShouldShowLiveEmployeeNameTag(SocialRoomFieldActorSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            if (snapshot.Kind != SocialRoomKind.PersonalShop && snapshot.Kind != SocialRoomKind.EntrustedShop)
            {
                return false;
            }

            string stateKey = snapshot.StateKey ?? string.Empty;
            if (snapshot.Template == SocialRoomFieldActorTemplate.StoreBanker
                && stateKey.IndexOf("expired", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return true;
        }

        private static string FormatHiredMerchantNameTag(string ownerName)
        {
            if (string.IsNullOrWhiteSpace(ownerName))
            {
                return string.Empty;
            }

            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(
                HiredMerchantNameTagStringPoolId,
                HiredMerchantNameTagFallbackFormat,
                maxPlaceholderCount: 1,
                out _);
            return string.Format(CultureInfo.InvariantCulture, format, ownerName.Trim());
        }

        internal static string ResolveNameTagTextForTesting(SocialRoomKind kind, SocialRoomFieldActorTemplate template, string detail, string stateKey)
        {
            return ResolveNameTagText(new SocialRoomFieldActorSnapshot(
                kind,
                template,
                headline: string.Empty,
                detail: detail,
                stateKey: stateKey ?? string.Empty));
        }

        private static void DrawOutlinedScaledText(
            SpriteBatch spriteBatch,
            SpriteFont font,
            string text,
            Vector2 position,
            Color color,
            float scale)
        {
            Vector2 shadowOffset = Vector2.One;
            spriteBatch.DrawString(font, text, position + shadowOffset, Color.Black, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private sealed class EmployeeTemplateProfile
        {
            private EmployeeTemplateProfile(
                string npcId,
                int speakDurationMs,
                int minIdleDurationMs,
                int maxIdleDurationMs,
                string[] speakActions,
                string[] cycleActions,
                string[] idleActions,
                string[] activeActions,
                string[] restockActions,
                string[] ledgerActions,
                string[] expiredActions,
                bool usesClientIndexedActionCycle)
            {
                NpcId = npcId;
                SpeakDurationMs = Math.Max(0, speakDurationMs);
                MinIdleDurationMs = Math.Max(250, minIdleDurationMs);
                MaxIdleDurationMs = Math.Max(MinIdleDurationMs, maxIdleDurationMs);
                SpeakActions = speakActions ?? Array.Empty<string>();
                CycleActions = cycleActions ?? Array.Empty<string>();
                IdleActions = idleActions ?? Array.Empty<string>();
                ActiveActions = activeActions ?? Array.Empty<string>();
                RestockActions = restockActions ?? Array.Empty<string>();
                LedgerActions = ledgerActions ?? Array.Empty<string>();
                ExpiredActions = expiredActions ?? Array.Empty<string>();
                UsesClientIndexedActionCycle = usesClientIndexedActionCycle;
            }

            public static EmployeeTemplateProfile Merchant { get; } = new(
                MerchantNpcId,
                speakDurationMs: 900,
                minIdleDurationMs: 1350,
                maxIdleDurationMs: 2600,
                speakActions: new[] { "say" },
                cycleActions: new[] { AnimationKeys.Stand, "eye", "ear", "potion" },
                idleActions: new[] { AnimationKeys.Stand, "eye", "ear", "potion" },
                activeActions: new[] { "eye", AnimationKeys.Stand, "ear" },
                restockActions: new[] { "potion", "ear", AnimationKeys.Stand },
                ledgerActions: new[] { "ear", "eye", AnimationKeys.Stand },
                expiredActions: new[] { AnimationKeys.Stand },
                usesClientIndexedActionCycle: false);

            public static EmployeeTemplateProfile StoreBanker { get; } = new(
                StoreBankerNpcId,
                speakDurationMs: 900,
                minIdleDurationMs: 1600,
                maxIdleDurationMs: 2800,
                speakActions: new[] { "say0", AnimationKeys.Stand },
                cycleActions: new[] { AnimationKeys.Stand, "say0" },
                idleActions: new[] { AnimationKeys.Stand },
                activeActions: new[] { AnimationKeys.Stand },
                restockActions: new[] { AnimationKeys.Stand },
                ledgerActions: new[] { AnimationKeys.Stand },
                expiredActions: new[] { "say0", AnimationKeys.Stand },
                usesClientIndexedActionCycle: false);

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
                    cycleActions: BuildPreferredActions(
                        orderedActions,
                        fallbackToRemainingActions: true,
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
                        AnimationKeys.Stand),
                    usesClientIndexedActionCycle: true);
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
            public string[] CycleActions { get; }
            public string[] IdleActions { get; }
            public string[] ActiveActions { get; }
            public string[] RestockActions { get; }
            public string[] LedgerActions { get; }
            public string[] ExpiredActions { get; }
            public bool UsesClientIndexedActionCycle { get; }
        }

        private sealed class EmployeeActionCatalog
        {
            private const string BaseActionName = AnimationKeys.Stand;
            private const string TemplateInfoPropertyName = "info";

            private EmployeeActionCatalog(IReadOnlyList<Entry> actions, IReadOnlyList<string> orderedActionNames)
            {
                Actions = actions ?? Array.Empty<Entry>();
                OrderedActionNames = orderedActionNames ?? Array.Empty<string>();
            }

            internal static EmployeeActionCatalog Create(EmployeeImageEntry employeeImgEntry)
            {
                if (employeeImgEntry?.PropertyRoot == null)
                {
                    return null;
                }

                List<(string ActionName, WzImageProperty ActionProperty)> candidateActions = new();
                HashSet<string> seenActionNames = new(StringComparer.Ordinal);
                foreach (WzImageProperty childProperty in employeeImgEntry.PropertyRoot.WzProperties)
                {
                    if (childProperty == null || string.IsNullOrWhiteSpace(childProperty.Name))
                    {
                        continue;
                    }

                    WzImageProperty resolvedActionProperty = ResolveLinkedProperty(childProperty);
                    if (resolvedActionProperty == null)
                    {
                        continue;
                    }

                    string normalizedActionName = childProperty.Name.Trim().ToLowerInvariant();
                    if (!seenActionNames.Add(normalizedActionName))
                    {
                        continue;
                    }

                    candidateActions.Add((normalizedActionName, resolvedActionProperty));
                }

                List<string> orderedActionNames = NormalizeClientActionNames(candidateActions.Select(action => action.ActionName)).ToList();
                bool hasBaseAction = orderedActionNames.Count > 0
                    && string.Equals(orderedActionNames[0], BaseActionName, StringComparison.Ordinal);
                List<Entry> actions = new();
                for (int i = 0; i < orderedActionNames.Count; i++)
                {
                    string actionName = orderedActionNames[i];
                    WzImageProperty actionProperty = candidateActions
                        .FirstOrDefault(action => string.Equals(action.ActionName, actionName, StringComparison.Ordinal))
                        .ActionProperty;
                    if (actionProperty != null)
                    {
                        int clientActionIndex = ResolveClientActionIndex(actionName, i, hasBaseAction);
                        actions.Add(new Entry(actionName, clientActionIndex, actionProperty));
                    }
                }

                return actions.Count == 0
                    ? null
                    : new EmployeeActionCatalog(actions, orderedActionNames);
            }

            internal static IReadOnlyList<string> NormalizeClientActionNames(IEnumerable<string> actionNames)
            {
                List<string> normalizedActions = new();
                if (actionNames == null)
                {
                    return normalizedActions;
                }

                List<string> nonStandActions = new();
                bool hasStand = false;
                foreach (string actionName in actionNames)
                {
                    if (string.IsNullOrWhiteSpace(actionName))
                    {
                        continue;
                    }

                    string normalizedActionName = actionName.Trim().ToLowerInvariant();
                    if (string.Equals(normalizedActionName, TemplateInfoPropertyName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (string.Equals(normalizedActionName, BaseActionName, StringComparison.Ordinal))
                    {
                        hasStand = true;
                        continue;
                    }

                    if (!nonStandActions.Contains(normalizedActionName, StringComparer.Ordinal))
                    {
                        nonStandActions.Add(normalizedActionName);
                    }
                }

                if (hasStand)
                {
                    normalizedActions.Add(BaseActionName);
                }

                normalizedActions.AddRange(nonStandActions);
                return normalizedActions;
            }

            private static int ResolveClientActionIndex(string actionName, int orderedIndex, bool hasBaseAction)
            {
                if (string.Equals(actionName, BaseActionName, StringComparison.Ordinal))
                {
                    return 0;
                }

                int normalizedOrderedIndex = Math.Max(0, orderedIndex);
                return hasBaseAction ? normalizedOrderedIndex : normalizedOrderedIndex + 1;
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

        private sealed class EmployeeActionCacheEntry
        {
            internal EmployeeActionCacheEntry(int templateId, string actionName, List<IDXObject> frames, int lastAccessTickMs)
            {
                TemplateId = Math.Max(0, templateId);
                ActionName = NormalizeActionName(actionName);
                Frames = frames ?? new List<IDXObject>();
                LastAccessTickMs = lastAccessTickMs;
            }

            internal int TemplateId { get; }
            internal string ActionName { get; }
            internal List<IDXObject> Frames { get; }
            internal int LastAccessTickMs { get; set; }
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

        private readonly record struct EmployeeMiniRoomBoardEffectFrame(Texture2D Texture, int DelayMs, Vector2? Origin);

        private readonly record struct MiniRoomBalloonLayout(
            int IconX,
            int IconY,
            int CurrentCountX,
            int CurrentCountY,
            int MaxCountX,
            int MaxCountY,
            int StatusX,
            int StatusY,
            int TitleCenterX,
            int HeadlineY,
            int OwnerY);

        private sealed class EmployeeMiniRoomBoardAssets
        {
            public Texture2D Signboard { get; init; }
            public Vector2? SignboardOrigin { get; init; }
            public EmployeeMiniRoomBoardEffectFrame[] EffectFrames { get; init; } = Array.Empty<EmployeeMiniRoomBoardEffectFrame>();
            public int TotalEffectDurationMs { get; init; }
        }

        private sealed class NameTagAssets
        {
            public Texture2D Left { get; init; }
            public Texture2D Middle { get; init; }
            public Texture2D Right { get; init; }
            public Color TextColor { get; init; }

            public bool IsLoaded => Left != null && Middle != null && Right != null;
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
