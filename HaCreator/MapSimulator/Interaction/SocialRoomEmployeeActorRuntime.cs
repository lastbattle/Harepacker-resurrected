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
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
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
        private const float HeadlineScale = 0.48f;
        private const float DetailScale = 0.42f;

        private static readonly Color SignHeadlineColor = new(255, 242, 176);
        private static readonly Color SignDetailColor = new(244, 244, 244);
        private static readonly Color SignPanelColor = new(28, 22, 18, 210);
        private static readonly Color SignBorderColor = new(180, 138, 69, 255);

        private readonly Dictionary<string, NpcItem> _actorCache = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EmployeeTemplateProfile> _cashProfileCache = new(StringComparer.Ordinal);
        private readonly ConcurrentBag<WzObject> _usedProps = new();
        private readonly Random _random = new();

        private NpcItem _activeActor;
        private SocialRoomFieldActorSnapshot _activeSnapshot;
        private string _lastStateKey = string.Empty;
        private string _currentIdleAction = AnimationKeys.Stand;
        private int _idleActionRemainingMs;
        private int _temporaryActionRemainingMs;
        private bool _currentFlip;

        public bool IsVisible => _activeActor != null && _activeSnapshot != null;

        public void Clear()
        {
            _activeActor = null;
            _activeSnapshot = null;
            _lastStateKey = string.Empty;
            _currentIdleAction = AnimationKeys.Stand;
            _idleActionRemainingMs = 0;
            _temporaryActionRemainingMs = 0;
            _currentFlip = false;
        }

        public void Update(
            SocialRoomFieldActorSnapshot snapshot,
            Board mapBoard,
            PlayerCharacter player,
            TexturePool texturePool,
            GraphicsDevice device,
            float userScreenScaleFactor,
            GameTime gameTime)
        {
            if (snapshot == null || mapBoard == null || player == null || texturePool == null || device == null)
            {
                Clear();
                return;
            }

            NpcItem actor = EnsureActor(snapshot, mapBoard, texturePool, device, userScreenScaleFactor);
            if (actor == null)
            {
                Clear();
                return;
            }

            _activeActor = actor;
            _activeSnapshot = snapshot;

            EmployeeTemplateProfile profile = ResolveProfile(snapshot);
            int elapsedMs = (int)Math.Max(0d, gameTime.ElapsedGameTime.TotalMilliseconds);

            AdvanceActionState(actor, snapshot, profile, elapsedMs);
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

            DrawSign(spriteBatch, font, panelTexture, mapShiftX, mapShiftY, mapCenterX, mapCenterY);
        }

        private NpcItem EnsureActor(
            SocialRoomFieldActorSnapshot snapshot,
            Board mapBoard,
            TexturePool texturePool,
            GraphicsDevice device,
            float userScreenScaleFactor)
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
                _ => CreateNpcActor(ResolveProfile(snapshot).NpcId, mapBoard, texturePool, device, userScreenScaleFactor)
            };

            actor ??= CreateNpcActor(ResolveProfile(snapshot).NpcId, mapBoard, texturePool, device, userScreenScaleFactor);

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
            float userScreenScaleFactor)
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
                includeTooltips: false);
        }

        private NpcItem CreateCashEmployeeActor(
            string actorKey,
            int templateId,
            Board mapBoard,
            TexturePool texturePool,
            GraphicsDevice device,
            float userScreenScaleFactor)
        {
            WzImageProperty employeeRoot = ResolveEmployeeProperty(templateId);
            if (employeeRoot == null)
            {
                return null;
            }

            _cashProfileCache[actorKey] = EmployeeTemplateProfile.CreateCashEmployee(employeeRoot.WzProperties.Select(property => property?.Name));

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
            foreach (WzImageProperty childProperty in employeeRoot.WzProperties)
            {
                if (childProperty == null)
                {
                    continue;
                }

                List<IDXObject> actionFrames = MapSimulatorLoader.LoadFrames(texturePool, childProperty, 0, 0, device, _usedProps);
                if (actionFrames.Count > 0)
                {
                    animationSet.AddAnimation(childProperty.Name, actionFrames);
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
            actor.NpcInstance.Flip = snapshot.Flip ?? _currentFlip;
        }

        private void AdvanceActionState(
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
                TriggerStateAction(actor, profile, snapshot);
                ResetIdleSelection(actor, snapshot, profile, preferContextualAction: true);
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

            ResetIdleSelection(actor, snapshot, profile, preferContextualAction: false);
        }

        private void TriggerStateAction(NpcItem actor, EmployeeTemplateProfile profile, SocialRoomFieldActorSnapshot snapshot)
        {
            string speakAction = ResolveFirstAvailableAction(actor, profile.SpeakActions);
            if (string.IsNullOrWhiteSpace(speakAction))
            {
                return;
            }

            actor.SetTemporaryAction(speakAction, profile.SpeakDurationMs);
            _temporaryActionRemainingMs = ResolveActionDurationMs(actor, speakAction, profile.SpeakDurationMs);
            if (!snapshot.Flip.HasValue)
            {
                _currentFlip = _random.Next(2) == 0;
            }
        }

        private void ResetIdleSelection(
            NpcItem actor,
            SocialRoomFieldActorSnapshot snapshot,
            EmployeeTemplateProfile profile,
            bool preferContextualAction)
        {
            string nextIdleAction = preferContextualAction
                ? ResolveContextualIdleAction(actor, snapshot, profile) ?? ResolveRandomIdleAction(actor, profile)
                : ResolveRandomIdleAction(actor, profile);
            if (string.IsNullOrWhiteSpace(nextIdleAction))
            {
                nextIdleAction = AnimationKeys.Stand;
            }

            _currentIdleAction = nextIdleAction;
            _idleActionRemainingMs = ResolveActionDurationMs(
                actor,
                _currentIdleAction,
                _random.Next(profile.MinIdleDurationMs, profile.MaxIdleDurationMs + 1));
            if (!snapshot.Flip.HasValue)
            {
                _currentFlip = _random.Next(2) == 0;
            }

            actor.SetAction(_currentIdleAction);
        }

        private static string ResolveContextualIdleAction(
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
                return ResolveFirstAvailableAction(actor, profile.ExpiredActions);
            }

            if (stateKey.IndexOf("updating sale list", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("registering sale", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("sale bundles", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("restock", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ResolveFirstAvailableAction(actor, profile.RestockActions);
            }

            if (stateKey.IndexOf("ledger", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("claim", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("sold", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("tax", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ResolveFirstAvailableAction(actor, profile.LedgerActions);
            }

            if (stateKey.IndexOf("permit active", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("merchant", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("open shop", StringComparison.OrdinalIgnoreCase) >= 0
                || stateKey.IndexOf("open for visitors", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ResolveFirstAvailableAction(actor, profile.ActiveActions);
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

        private string ResolveRandomIdleAction(NpcItem actor, EmployeeTemplateProfile profile)
        {
            if (actor == null || profile == null)
            {
                return null;
            }

            List<string> availableActions = new();
            for (int i = 0; i < profile.IdleActions.Length; i++)
            {
                string action = profile.IdleActions[i];
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

        private static string ResolveFirstAvailableAction(NpcItem actor, IReadOnlyList<string> candidates)
        {
            if (actor == null || candidates == null)
            {
                return null;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                string action = candidates[i];
                if (!string.IsNullOrWhiteSpace(action) && actor.HasAction(action))
                {
                    return action;
                }
            }

            return null;
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
                HashSet<string> availableActions = new(
                    orderedActions ?? Array.Empty<string>(),
                    StringComparer.Ordinal);

                for (int i = 0; i < candidates.Length; i++)
                {
                    string candidate = candidates[i];
                    if (string.IsNullOrWhiteSpace(candidate))
                    {
                        continue;
                    }

                    string normalized = candidate.Trim().ToLowerInvariant();
                    if ((availableActions.Count == 0 || availableActions.Contains(normalized))
                        && !resolved.Contains(normalized, StringComparer.Ordinal))
                    {
                        resolved.Add(normalized);
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
