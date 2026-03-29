using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Physics;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Pools
{
    /// <summary>
    /// Shared owner for remote user actors. This gives the simulator a single seam
    /// for remote avatar look decode, map insertion/removal, world rendering,
    /// helper markers, team state, chairs, mounts, and prepared-skill overlays.
    /// </summary>
    public sealed class RemoteUserActorPool
    {
        private const int MinimumMeleeAfterImageFadeDurationMs = 60;
        private const float RemoteDragonGroundSideOffset = 42f;
        private const float RemoteDragonGroundVerticalOffset = -12f;
        private const float RemoteDragonKeyDownBarHalfWidth = 36f;
        private const float RemoteDragonKeyDownBarVerticalGap = 30f;
        private static readonly EquipSlot[] BattlefieldAppearanceSlots =
        {
            EquipSlot.Cap,
            EquipSlot.Coat,
            EquipSlot.Longcoat,
            EquipSlot.Pants,
            EquipSlot.Shoes,
            EquipSlot.Glove,
            EquipSlot.Cape,
        };
        private static readonly Dictionary<int, RemoteDragonHudMetadata> RemoteDragonHudMetadataCache = new();

        private readonly Dictionary<int, RemoteUserActor> _actorsById = new();
        private readonly Dictionary<string, int> _actorIdsByName = new(StringComparer.OrdinalIgnoreCase);
        private CharacterLoader _loader;
        private SkillLoader _skillLoader;

        public int Count => _actorsById.Count;
        public IEnumerable<RemoteUserActor> Actors => _actorsById.Values;

        public void Initialize(CharacterLoader loader, SkillLoader skillLoader)
        {
            _loader = loader;
            _skillLoader = skillLoader;
        }

        public void Clear()
        {
            _actorsById.Clear();
            _actorIdsByName.Clear();
        }

        public void RemoveBySourceTag(string sourceTag)
        {
            if (string.IsNullOrWhiteSpace(sourceTag))
            {
                return;
            }

            foreach (int characterId in _actorsById.Values
                .Where(actor => string.Equals(actor.SourceTag, sourceTag.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(actor => actor.CharacterId)
                .ToArray())
            {
                if (_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
                {
                    _actorIdsByName.Remove(actor.Name);
                    _actorsById.Remove(characterId);
                }
            }
        }

        public bool TryGetActor(int characterId, out RemoteUserActor actor)
        {
            return _actorsById.TryGetValue(characterId, out actor);
        }

        public bool TryGetActorByName(string name, out RemoteUserActor actor)
        {
            actor = null;
            return !string.IsNullOrWhiteSpace(name)
                   && _actorIdsByName.TryGetValue(name.Trim(), out int characterId)
                   && _actorsById.TryGetValue(characterId, out actor);
        }

        public bool TryGetPosition(string name, out Vector2 position)
        {
            if (TryGetActorByName(name, out RemoteUserActor actor))
            {
                position = actor.Position;
                return true;
            }

            position = default;
            return false;
        }

        public bool TryAddOrUpdate(
            int characterId,
            CharacterBuild build,
            Vector2 position,
            out string message,
            bool facingRight = true,
            string actionName = null,
            string sourceTag = null,
            bool isVisibleInWorld = true)
        {
            message = null;
            if (characterId <= 0)
            {
                message = "Remote character ID must be positive.";
                return false;
            }

            if (build == null)
            {
                message = "Remote character build is required.";
                return false;
            }

            build.Id = characterId;
            if (string.IsNullOrWhiteSpace(build.Name))
            {
                build.Name = $"Remote{characterId}";
            }

            if (_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                string previousName = actor.Name;
                actor.BeginMeleeAfterImageFade(Environment.TickCount);
                ResetBattlefieldAppearanceState(actor);
                actor.Build = build;
                actor.Name = build.Name.Trim();
                actor.Position = position;
                actor.FacingRight = facingRight;
                actor.ActionName = NormalizeActionName(actionName, actor.Build.ActivePortableChair != null);
                actor.SourceTag = string.IsNullOrWhiteSpace(sourceTag) ? actor.SourceTag : sourceTag.Trim();
                actor.IsVisibleInWorld = isVisibleInWorld;
                actor.RefreshAssembler();
                RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
                UpdateNameLookup(previousName, actor.Name, characterId);
                return true;
            }

            RemoteUserActor created = new RemoteUserActor(
                characterId,
                build.Name.Trim(),
                build,
                position,
                facingRight,
                NormalizeActionName(actionName, build.ActivePortableChair != null),
                string.IsNullOrWhiteSpace(sourceTag) ? "remote" : sourceTag.Trim(),
                isVisibleInWorld);
            RegisterMeleeAfterImage(created, 0, created.ActionName, Environment.TickCount, 10, 0);
            _actorsById[characterId] = created;
            _actorIdsByName[created.Name] = characterId;
            return true;
        }

        public bool TryAddOrUpdateAvatarLook(
            int characterId,
            string name,
            LoginAvatarLook avatarLook,
            CharacterBuild template,
            Vector2 position,
            out string message,
            bool facingRight,
            string actionName,
            string sourceTag,
            bool isVisibleInWorld)
        {
            message = null;
            if (_loader == null)
            {
                message = "Character loader is not available.";
                return false;
            }

            if (avatarLook == null)
            {
                message = "AvatarLook payload is required.";
                return false;
            }

            CharacterBuild build = _loader.LoadFromAvatarLook(avatarLook, template);
            if (build == null)
            {
                message = "AvatarLook could not be converted into a remote character build.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                build.Name = name.Trim();
            }

            return TryAddOrUpdate(
                characterId,
                build,
                position,
                out message,
                facingRight,
                actionName,
                sourceTag,
                isVisibleInWorld);
        }

        public bool TryMove(int characterId, Vector2 position, bool? facingRight, string actionName, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.Position = position;
            if (facingRight.HasValue)
            {
                actor.FacingRight = facingRight.Value;
            }

            if (!string.IsNullOrWhiteSpace(actionName))
            {
                actor.BeginMeleeAfterImageFade(Environment.TickCount);
                actor.ActionName = NormalizeActionName(actionName, actor.Build.ActivePortableChair != null);
                RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
            }

            return true;
        }

        public bool TrySetAction(int characterId, string actionName, bool? facingRight, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.BeginMeleeAfterImageFade(Environment.TickCount);
            actor.ActionName = NormalizeActionName(actionName, actor.Build.ActivePortableChair != null);
            if (facingRight.HasValue)
            {
                actor.FacingRight = facingRight.Value;
            }

            actor.MovementDrivenActionSelection = false;
            RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
            return true;
        }

        public bool TryRegisterMeleeAfterImage(
            int characterId,
            int skillId,
            string actionName,
            int? actionCode,
            int masteryPercent,
            int chargeSkillId,
            bool? facingRight,
            int currentTime,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            if (facingRight.HasValue)
            {
                actor.FacingRight = facingRight.Value;
            }

            if (!string.IsNullOrWhiteSpace(actionName))
            {
                actor.BeginMeleeAfterImageFade(currentTime);
                actor.ActionName = NormalizeActionName(actionName, actor.Build.ActivePortableChair != null);
            }
            else if (actionCode.HasValue && CharacterPart.TryGetActionStringFromCode(actionCode.Value, out string resolvedActionName))
            {
                actor.BeginMeleeAfterImageFade(currentTime);
                actor.ActionName = NormalizeActionName(resolvedActionName, actor.Build.ActivePortableChair != null);
            }

            int chargeElement = AfterImageChargeSkillResolver.TryGetChargeElement(chargeSkillId, out int resolvedChargeElement)
                ? resolvedChargeElement
                : 0;
            RegisterMeleeAfterImage(actor, skillId, actor.ActionName, currentTime, masteryPercent, chargeElement);
            return true;
        }

        public bool TryApplyMoveAction(int characterId, byte moveAction, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.LastMoveActionRaw = moveAction;
            actor.FacingRight = DecodeFacingRight(moveAction);
            actor.BeginMeleeAfterImageFade(Environment.TickCount);
            actor.ActionName = ResolveActionName(actor, MoveActionFromRaw(moveAction));
            actor.MovementDrivenActionSelection = true;
            RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
            return true;
        }

        public bool TryApplyMoveSnapshot(int characterId, PlayerMovementSyncSnapshot movementSnapshot, byte moveAction, int currentTime, out string message)
        {
            message = null;
            if (movementSnapshot == null)
            {
                message = "Remote movement snapshot is required.";
                return false;
            }

            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.MovementSnapshot = movementSnapshot;
            actor.LastMoveActionRaw = moveAction;
            actor.MovementDrivenActionSelection = true;
            ApplyMovementSnapshot(actor, currentTime);
            return true;
        }

        public bool TrySetHelperMarker(int characterId, MinimapUI.HelperMarkerType? markerType, bool showDirectionOverlay, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.HelperMarkerType = markerType;
            actor.ShowDirectionOverlay = showDirectionOverlay;
            return true;
        }

        public bool TrySetBattlefieldTeam(int characterId, int? teamId, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.BattlefieldTeamId = teamId;
            return true;
        }

        public void SyncBattlefieldAppearance(BattlefieldField battlefield)
        {
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                SyncBattlefieldAppearance(actor, battlefield);
            }
        }

        public bool TrySetPortableChair(int characterId, int? chairItemId, out string message)
        {
            message = null;
            if (_loader == null)
            {
                message = "Character loader is not available.";
                return false;
            }

            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            ClearPortableChairMountState(actor);
            if (!chairItemId.HasValue || chairItemId.Value <= 0)
            {
                actor.Build.ActivePortableChair = null;
                actor.ActionName = CharacterPart.GetActionString(CharacterAction.Stand1);
                actor.RefreshAssembler();
                actor.ClearMeleeAfterImage();
                return true;
            }

            PortableChair chair = _loader.LoadPortableChair(chairItemId.Value);
            if (chair == null)
            {
                message = $"Portable chair {chairItemId.Value} could not be loaded.";
                return false;
            }

            actor.Build.ActivePortableChair = chair;
            ApplyPortableChairMount(actor, chair);
            actor.ActionName = NormalizeActionName("sit", allowSitFallback: true);
            actor.RefreshAssembler();
            actor.ClearMeleeAfterImage();
            return true;
        }

        public bool TrySetMount(int characterId, int? tamingMobItemId, out string message)
        {
            message = null;
            if (_loader == null)
            {
                message = "Character loader is not available.";
                return false;
            }

            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            if (!tamingMobItemId.HasValue || tamingMobItemId.Value <= 0)
            {
                actor.Build.Unequip(EquipSlot.TamingMob);
                actor.RefreshAssembler();
                RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
                return true;
            }

            CharacterPart mountPart = _loader.LoadEquipment(tamingMobItemId.Value);
            if (mountPart?.Slot != EquipSlot.TamingMob)
            {
                message = $"Item {tamingMobItemId.Value} is not a taming mob mount.";
                return false;
            }

            actor.Build.Equip(mountPart);
            actor.RefreshAssembler();
            RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
            return true;
        }

        public bool TrySetPreparedSkill(
            int characterId,
            int skillId,
            string skillName,
            int durationMs,
            string skinKey,
            bool isKeydownSkill,
            bool isHolding,
            int gaugeDurationMs,
            int maxHoldDurationMs,
            PreparedSkillHudTextVariant textVariant,
            bool showText,
            int currentTime,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.PreparedSkill = new RemotePreparedSkillState
            {
                SkillId = skillId,
                SkillName = string.IsNullOrWhiteSpace(skillName) ? $"Skill {skillId}" : skillName.Trim(),
                SkinKey = string.IsNullOrWhiteSpace(skinKey) ? "KeyDownBar" : skinKey.Trim(),
                DurationMs = Math.Max(0, durationMs),
                GaugeDurationMs = gaugeDurationMs > 0 ? gaugeDurationMs : Math.Max(0, durationMs),
                StartTime = currentTime,
                IsKeydownSkill = isKeydownSkill,
                IsHolding = isHolding,
                MaxHoldDurationMs = Math.Max(0, maxHoldDurationMs),
                TextVariant = textVariant,
                ShowText = showText
            };
            return true;
        }

        public bool TryClearPreparedSkill(int characterId, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.PreparedSkill = null;
            return true;
        }

        public bool TrySetWorldVisibility(int characterId, bool isVisibleInWorld, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.IsVisibleInWorld = isVisibleInWorld;
            return true;
        }

        public bool TryRemove(int characterId, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            _actorsById.Remove(characterId);
            _actorIdsByName.Remove(actor.Name);
            return true;
        }

        public void Update(int currentTime)
        {
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                if (actor.MovementSnapshot != null)
                {
                    ApplyMovementSnapshot(actor, currentTime);
                }

                if (actor.PreparedSkill != null && actor.PreparedSkill.DurationMs > 0)
                {
                    int elapsed = Math.Max(0, currentTime - actor.PreparedSkill.StartTime);
                    if (elapsed >= actor.PreparedSkill.DurationMs)
                    {
                        actor.PreparedSkill = null;
                    }
                }

                actor.UpdateMeleeAfterImage(currentTime);
            }
        }

        public void SyncPortableChairPairState(PlayerCharacter player)
        {
            if (player == null)
            {
                return;
            }

            PortableChair chair = player.Build?.ActivePortableChair;
            bool requestsExternalPair = chair?.IsCoupleChair == true;
            player.SetPortableChairPairRequestActive(requestsExternalPair);
            if (!requestsExternalPair)
            {
                return;
            }

            RemoteUserActor pairActor = FindPortableChairPairActor(
                chair,
                player.FacingRight,
                player.X,
                player.Y,
                skipCharacterId: player.Build?.Id ?? 0,
                preferVisibleOnly: true);
            if (pairActor != null)
            {
                player.SetPortableChairExternalPair(pairActor.Position, pairActor.FacingRight);
            }
            else
            {
                player.ClearPortableChairExternalPair();
            }
        }

        public IReadOnlyList<StatusBarPreparedSkillRenderData> BuildPreparedSkillWorldOverlays(int currentTime)
        {
            List<StatusBarPreparedSkillRenderData> overlays = new();
            foreach (RemoteUserActor actor in _actorsById.Values.Where(static value => value.IsVisibleInWorld))
            {
                RemotePreparedSkillState prepared = actor.PreparedSkill;
                if (prepared == null)
                {
                    continue;
                }

                int elapsed = Math.Max(0, currentTime - prepared.StartTime);
                int duration = Math.Max(0, prepared.DurationMs);
                int remainingMs = duration > 0 ? Math.Max(0, duration - elapsed) : 0;
                float progress = 0f;
                if (duration > 0)
                {
                    progress = MathHelper.Clamp(elapsed / (float)duration, 0f, 1f);
                }

                overlays.Add(new StatusBarPreparedSkillRenderData
                {
                    SkillId = prepared.SkillId,
                    SkillName = prepared.SkillName,
                    SkinKey = prepared.SkinKey,
                    Surface = PreparedSkillHudSurface.World,
                    RemainingMs = remainingMs,
                    DurationMs = duration,
                    GaugeDurationMs = prepared.GaugeDurationMs > 0 ? prepared.GaugeDurationMs : duration,
                    Progress = progress,
                    IsKeydownSkill = prepared.IsKeydownSkill,
                    IsHolding = prepared.IsHolding,
                    HoldElapsedMs = prepared.IsHolding ? elapsed : 0,
                    MaxHoldDurationMs = prepared.MaxHoldDurationMs,
                    TextVariant = prepared.TextVariant,
                    ShowText = prepared.ShowText && !PreparedSkillHudRules.IsDragonOverlaySkill(prepared.SkillId),
                    WorldAnchor = ResolvePreparedSkillWorldAnchor(actor, prepared, currentTime)
                });
            }

            return overlays;
        }

        private static Vector2 ResolvePreparedSkillWorldAnchor(RemoteUserActor actor, RemotePreparedSkillState prepared, int currentTime)
        {
            if (actor == null)
            {
                return Vector2.Zero;
            }

            if (prepared != null
                && PreparedSkillHudRules.IsDragonOverlaySkill(prepared.SkillId))
            {
                if (TryResolveRemoteDragonKeyDownBarAnchor(actor, prepared.SkillId, currentTime, out Vector2 dragonAnchor))
                {
                    return dragonAnchor;
                }

                return new Vector2(actor.Position.X, actor.Position.Y - 92f);
            }

            return ResolveStandardPreparedSkillWorldAnchor(actor, currentTime);
        }

        private static Vector2 ResolveStandardPreparedSkillWorldAnchor(RemoteUserActor actor, int currentTime)
        {
            AssembledFrame frame = actor.Assembler?.GetFrameAtTime(actor.ActionName, currentTime)
                ?? actor.Assembler?.GetFrameAtTime(CharacterPart.GetActionString(CharacterAction.Stand1), currentTime);
            if (frame != null)
            {
                float topY = actor.Position.Y - frame.FeetOffset + frame.Bounds.Top;
                return new Vector2(actor.Position.X, topY - 18f);
            }

            return new Vector2(actor.Position.X, actor.Position.Y - 80f);
        }

        private static bool TryResolveRemoteDragonKeyDownBarAnchor(
            RemoteUserActor actor,
            int skillId,
            int currentTime,
            out Vector2 anchor)
        {
            anchor = Vector2.Zero;
            if (actor?.Build == null
                || !TryResolveRemoteDragonHudMetadata(actor.Build.Job, out RemoteDragonHudMetadata metadata))
            {
                return false;
            }

            AssembledFrame ownerFrame = actor.Assembler?.GetFrameAtTime(actor.ActionName, currentTime)
                ?? actor.Assembler?.GetFrameAtTime(CharacterPart.GetActionString(CharacterAction.Stand1), currentTime);
            float ownerBodyOriginY = ownerFrame != null
                ? actor.Position.Y - ownerFrame.FeetOffset
                : actor.Position.Y;
            float side = actor.FacingRight ? -1f : 1f;
            float horizontalOffset = Math.Max(RemoteDragonGroundSideOffset, metadata.StandOriginX * 0.55f);
            Vector2 dragonAnchor = new(
                actor.Position.X + (side * horizontalOffset),
                ownerBodyOriginY + RemoteDragonGroundVerticalOffset);

            int dragonFrameHeight = metadata.ResolveFrameHeight(ResolveRemoteDragonActionName(skillId));
            anchor = new Vector2(
                dragonAnchor.X - RemoteDragonKeyDownBarHalfWidth,
                dragonAnchor.Y - dragonFrameHeight - RemoteDragonKeyDownBarVerticalGap);
            return true;
        }

        private static bool TryResolveRemoteDragonHudMetadata(int jobId, out RemoteDragonHudMetadata metadata)
        {
            metadata = default;
            int dragonJob = jobId switch
            {
                >= 2200 and <= 2218 => jobId,
                _ => 0
            };
            if (dragonJob == 0)
            {
                return false;
            }

            if (RemoteDragonHudMetadataCache.TryGetValue(dragonJob, out metadata))
            {
                return true;
            }

            WzImage image = global::HaCreator.Program.FindImage("Skill", $"Dragon/{dragonJob}.img");
            if (image == null)
            {
                return false;
            }

            var actionHeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int standOriginX = 79;

            foreach (WzSubProperty actionNode in image.WzProperties.OfType<WzSubProperty>())
            {
                if (string.Equals(actionNode.Name, "info", StringComparison.OrdinalIgnoreCase)
                    || !TryReadRemoteDragonFrameMetrics(actionNode, out int originX, out int height))
                {
                    continue;
                }

                actionHeights[actionNode.Name] = height;
                if (string.Equals(actionNode.Name, "stand", StringComparison.OrdinalIgnoreCase))
                {
                    standOriginX = originX;
                }
            }

            if (actionHeights.Count == 0)
            {
                return false;
            }

            metadata = new RemoteDragonHudMetadata(standOriginX, actionHeights);
            RemoteDragonHudMetadataCache[dragonJob] = metadata;
            return true;
        }

        private static bool TryReadRemoteDragonFrameMetrics(WzSubProperty actionNode, out int originX, out int height)
        {
            originX = 0;
            height = 0;

            WzCanvasProperty frame = actionNode.WzProperties
                .OfType<WzCanvasProperty>()
                .OrderBy(static canvas => ParseRemoteDragonFrameIndex(canvas.Name))
                .FirstOrDefault();
            if (frame == null)
            {
                return false;
            }

            if (frame["origin"] is not WzVectorProperty origin
                || frame["lt"] is not WzVectorProperty lt
                || frame["rb"] is not WzVectorProperty rb)
            {
                return false;
            }

            originX = origin.X.Value;
            height = Math.Max(1, rb.Y.Value - lt.Y.Value);
            return true;
        }

        private static int ParseRemoteDragonFrameIndex(string value)
        {
            return int.TryParse(value, out int parsed) ? parsed : int.MaxValue;
        }

        private static string ResolveRemoteDragonActionName(int skillId)
        {
            return skillId switch
            {
                22121000 => "icebreathe_prepare",
                22151001 => "breathe_prepare",
                _ => "stand"
            };
        }

        public IReadOnlyList<MinimapUI.TrackedUserMarker> BuildHelperMarkers()
        {
            List<MinimapUI.TrackedUserMarker> markers = new();
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                MinimapUI.HelperMarkerType? markerType = actor.HelperMarkerType;
                if (!markerType.HasValue && actor.BattlefieldTeamId.HasValue)
                {
                    markerType = MinimapUI.HelperMarkerType.Match;
                }

                if (!markerType.HasValue)
                {
                    continue;
                }

                markers.Add(new MinimapUI.TrackedUserMarker
                {
                    WorldX = actor.Position.X,
                    WorldY = actor.Position.Y,
                    MarkerType = markerType.Value,
                    ShowDirectionOverlay = actor.ShowDirectionOverlay
                });
            }

            return markers;
        }

        public void Draw(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int tickCount,
            SpriteFont font,
            PlayerCharacter localPlayer = null)
        {
            var renderedCouplePairs = new HashSet<(int LeftId, int RightId)>();
            foreach (RemoteUserActor actor in _actorsById.Values
                .Where(static value => value.IsVisibleInWorld)
                .OrderBy(static value => value.Position.Y)
                .ThenBy(static value => value.Name, StringComparer.OrdinalIgnoreCase))
            {
                AssembledFrame frame = actor.Assembler.GetFrameAtTime(actor.ActionName, tickCount)
                    ?? actor.Assembler.GetFrameAtTime(CharacterPart.GetActionString(CharacterAction.Stand1), tickCount);
                if (frame == null)
                {
                    continue;
                }

                int screenX = (int)Math.Round(actor.Position.X) - mapShiftX + centerX;
                int screenY = (int)Math.Round(actor.Position.Y) - mapShiftY + centerY;
                DrawPortableChairCoupleMidpointEffects(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    localPlayer,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    tickCount,
                    drawFrontLayers: false,
                    renderedCouplePairs);
                DrawPortableChairCoupleSharedLayers(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    localPlayer,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: false);
                DrawMeleeAfterImage(spriteBatch, skeletonMeshRenderer, actor, screenX, screenY, tickCount);
                frame.Draw(spriteBatch, skeletonMeshRenderer, screenX, screenY, actor.FacingRight, Color.White);
                DrawPortableChairCoupleSharedLayers(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    localPlayer,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: true);
                DrawPortableChairCoupleMidpointEffects(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    localPlayer,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    tickCount,
                    drawFrontLayers: true,
                    renderedCouplePairs);

                if (font == null)
                {
                    continue;
                }

                Vector2 textSize = font.MeasureString(actor.Name);
                float topY = screenY - frame.FeetOffset + frame.Bounds.Top;
                Vector2 textPosition = new(screenX - (textSize.X / 2f), topY - textSize.Y - 10f);
                DrawOutlinedText(spriteBatch, font, actor.Name, textPosition, Color.Black, ResolveNameColor(actor));
            }
        }

        private void DrawPortableChairCoupleMidpointEffects(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            PlayerCharacter localPlayer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int currentTime,
            bool drawFrontLayers,
            ISet<(int LeftId, int RightId)> renderedPairs)
        {
            PortableChair chair = actor?.Build?.ActivePortableChair;
            if (chair?.IsCoupleChair != true
                || chair.CoupleMidpointLayers == null
                || chair.CoupleMidpointLayers.Count == 0)
            {
                return;
            }

            if (TryResolvePortableChairPairWithLocalPlayer(actor, chair, localPlayer, out _, out _))
            {
                return;
            }

            RemoteUserActor partnerActor = FindPortableChairPairActor(
                chair,
                actor.FacingRight,
                actor.Position.X,
                actor.Position.Y,
                skipCharacterId: actor.CharacterId,
                preferVisibleOnly: true);
            if (partnerActor == null)
            {
                return;
            }

            var pairKey = actor.CharacterId < partnerActor.CharacterId
                ? (actor.CharacterId, partnerActor.CharacterId)
                : (partnerActor.CharacterId, actor.CharacterId);
            if (drawFrontLayers)
            {
                if (!renderedPairs.Add(pairKey))
                {
                    return;
                }
            }
            else if (renderedPairs.Contains(pairKey))
            {
                return;
            }

            int midpointScreenX = (int)Math.Round((actor.Position.X + partnerActor.Position.X) * 0.5f) - mapShiftX + centerX;
            int midpointScreenY = (int)Math.Round((actor.Position.Y + partnerActor.Position.Y) * 0.5f) - mapShiftY + centerY;
            int animationTime = currentTime;
            for (int i = 0; i < chair.CoupleMidpointLayers.Count; i++)
            {
                PortableChairLayer layer = chair.CoupleMidpointLayers[i];
                if ((layer.RelativeZ > 0) != drawFrontLayers)
                {
                    continue;
                }

                CharacterFrame layerFrame = PlayerCharacter.GetPortableChairLayerFrameAtTime(layer, animationTime);
                PlayerCharacter.DrawPortableChairLayerFrame(
                    spriteBatch,
                    skeletonMeshRenderer,
                    layerFrame,
                    midpointScreenX,
                    midpointScreenY,
                    actor.FacingRight);
            }
        }

        private void DrawPortableChairCoupleSharedLayers(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            PlayerCharacter localPlayer,
            int screenX,
            int screenY,
            int currentTime,
            bool drawFrontLayers)
        {
            PortableChair chair = actor?.Build?.ActivePortableChair;
            if (chair?.IsCoupleChair != true
                || chair.CoupleSharedLayers == null
                || chair.CoupleSharedLayers.Count == 0)
            {
                return;
            }

            bool hasPair = TryResolvePortableChairPairWithLocalPlayer(actor, chair, localPlayer, out _, out _);
            if (!hasPair)
            {
                hasPair = FindPortableChairPairActor(
                              chair,
                              actor.FacingRight,
                              actor.Position.X,
                              actor.Position.Y,
                              skipCharacterId: actor.CharacterId,
                              preferVisibleOnly: true) != null;
            }

            if (!hasPair)
            {
                return;
            }

            PlayerCharacter.DrawPortableChairLayers(
                spriteBatch,
                skeletonMeshRenderer,
                chair.CoupleSharedLayers,
                screenX,
                screenY,
                actor.FacingRight,
                currentTime,
                drawFrontLayers);
        }

        public string DescribeStatus()
        {
            if (_actorsById.Count == 0)
            {
                return "Remote user pool empty.";
            }

            return $"Remote user pool active, count={_actorsById.Count}, users={string.Join("; ", _actorsById.Values.OrderBy(static value => value.CharacterId).Select(static value => value.Describe()))}";
        }

        private static void DrawOutlinedText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color shadowColor, Color textColor)
        {
            Vector2[] offsets =
            {
                new Vector2(-1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, -1f),
                new Vector2(0f, 1f)
            };

            foreach (Vector2 offset in offsets)
            {
                spriteBatch.DrawString(font, text, position + offset, shadowColor);
            }

            spriteBatch.DrawString(font, text, position, textColor);
        }

        private static Color ResolveNameColor(RemoteUserActor actor)
        {
            return actor.BattlefieldTeamId switch
            {
                0 => new Color(255, 232, 170),
                1 => new Color(255, 189, 189),
                2 => new Color(185, 229, 255),
                _ => Color.White
            };
        }

        private bool TryResolvePortableChairPairWithLocalPlayer(
            RemoteUserActor actor,
            PortableChair chair,
            PlayerCharacter localPlayer,
            out Vector2 partnerPosition,
            out bool partnerFacingRight)
        {
            partnerPosition = Vector2.Zero;
            partnerFacingRight = false;
            if (actor == null
                || chair?.IsCoupleChair != true
                || localPlayer?.Build == null
                || !localPlayer.IsAlive
                || localPlayer.Build.ActivePortableChair == null)
            {
                return false;
            }

            if (!PlayerCharacter.IsPortableChairActualPairActive(
                    chair,
                    actor.FacingRight,
                    actor.Position.X,
                    actor.Position.Y,
                    localPlayer.FacingRight,
                    localPlayer.X,
                    localPlayer.Y))
            {
                return false;
            }

            partnerPosition = localPlayer.Position;
            partnerFacingRight = localPlayer.FacingRight;
            return true;
        }

        private RemoteUserActor FindPortableChairPairActor(
            PortableChair chair,
            bool ownerFacingRight,
            float ownerX,
            float ownerY,
            int skipCharacterId,
            bool preferVisibleOnly)
        {
            if (chair?.IsCoupleChair != true)
            {
                return null;
            }

            Point expectedOffset = PlayerCharacter.ResolvePortableChairPairOffset(chair, ownerFacingRight);
            Vector2 expectedPosition = new(ownerX + expectedOffset.X, ownerY + expectedOffset.Y);
            RemoteUserActor bestActor = null;
            float bestScore = float.MaxValue;

            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                if (actor.CharacterId == skipCharacterId
                    || (preferVisibleOnly && !actor.IsVisibleInWorld))
                {
                    continue;
                }

                if (!PlayerCharacter.IsPortableChairActualPairActive(
                        chair,
                        ownerFacingRight,
                        ownerX,
                        ownerY,
                        actor.FacingRight,
                        actor.Position.X,
                        actor.Position.Y))
                {
                    continue;
                }

                float score = Vector2.DistanceSquared(actor.Position, expectedPosition);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestActor = actor;
                }
            }

            return bestActor;
        }

        private void SyncBattlefieldAppearance(RemoteUserActor actor, BattlefieldField battlefield)
        {
            if (actor?.Build == null)
            {
                return;
            }

            int? assignedTeamId = battlefield != null
                && battlefield.TryGetAssignedTeamId(actor.CharacterId, out int resolvedTeamId)
                ? resolvedTeamId
                : actor.BattlefieldTeamId;
            if (battlefield?.IsActive != true)
            {
                RestoreBattlefieldAppearance(actor, clearTeamId: false);
                return;
            }

            if (actor.BattlefieldAppliedTeamId == assignedTeamId
                && (!assignedTeamId.HasValue || actor.BattlefieldOriginalEquipment != null))
            {
                return;
            }

            if (!assignedTeamId.HasValue
                || !battlefield.TryGetAssignedTeamLookPreset(actor.CharacterId, out BattlefieldField.BattlefieldTeamLookPreset preset))
            {
                RestoreBattlefieldAppearance(actor, clearTeamId: false);
                actor.BattlefieldAppliedTeamId = assignedTeamId;
                actor.BattlefieldTeamId = assignedTeamId;
                return;
            }

            EnsureBattlefieldOriginalAppearanceSnapshot(actor);
            if (actor.BattlefieldOriginalSpeed == null)
            {
                actor.BattlefieldOriginalSpeed = actor.Build.Speed;
            }

            foreach (EquipSlot slot in BattlefieldAppearanceSlots)
            {
                actor.Build.Unequip(slot);
            }

            if (preset.EquipmentItemIds.ContainsKey(EquipSlot.Longcoat))
            {
                actor.Build.Unequip(EquipSlot.Coat);
                actor.Build.Unequip(EquipSlot.Pants);
            }
            else if (preset.EquipmentItemIds.ContainsKey(EquipSlot.Coat))
            {
                actor.Build.Unequip(EquipSlot.Longcoat);
            }

            foreach (KeyValuePair<EquipSlot, int> entry in preset.EquipmentItemIds)
            {
                CharacterPart part = _loader?.LoadEquipment(entry.Value);
                if (part != null)
                {
                    actor.Build.Equip(part);
                }
            }

            if (preset.MoveSpeed.HasValue)
            {
                actor.Build.Speed = preset.MoveSpeed.Value;
            }

            actor.RefreshAssembler();
            actor.BattlefieldAppliedTeamId = assignedTeamId;
            actor.BattlefieldTeamId = assignedTeamId;
        }

        private static void EnsureBattlefieldOriginalAppearanceSnapshot(RemoteUserActor actor)
        {
            if (actor.BattlefieldOriginalEquipment != null || actor?.Build == null)
            {
                return;
            }

            actor.BattlefieldOriginalEquipment = new Dictionary<EquipSlot, CharacterPart>();
            actor.BattlefieldOriginalSpeed = actor.Build.Speed;
            foreach (EquipSlot slot in BattlefieldAppearanceSlots)
            {
                if (actor.Build.Equipment.TryGetValue(slot, out CharacterPart part) && part != null)
                {
                    actor.BattlefieldOriginalEquipment[slot] = part;
                }
            }
        }

        private static void RestoreBattlefieldAppearance(RemoteUserActor actor, bool clearTeamId)
        {
            if (actor?.Build == null)
            {
                return;
            }

            if (actor.BattlefieldOriginalEquipment == null)
            {
                actor.BattlefieldAppliedTeamId = null;
                if (clearTeamId)
                {
                    actor.BattlefieldTeamId = null;
                }
                return;
            }

            foreach (EquipSlot slot in BattlefieldAppearanceSlots)
            {
                actor.Build.Unequip(slot);
            }

            foreach (KeyValuePair<EquipSlot, CharacterPart> entry in actor.BattlefieldOriginalEquipment)
            {
                actor.Build.Equip(entry.Value);
            }

            if (actor.BattlefieldOriginalSpeed.HasValue)
            {
                actor.Build.Speed = actor.BattlefieldOriginalSpeed.Value;
            }

            actor.RefreshAssembler();
            ResetBattlefieldAppearanceState(actor);
            if (clearTeamId)
            {
                actor.BattlefieldTeamId = null;
            }
        }

        private static void ResetBattlefieldAppearanceState(RemoteUserActor actor)
        {
            if (actor == null)
            {
                return;
            }

            actor.BattlefieldOriginalEquipment = null;
            actor.BattlefieldOriginalSpeed = null;
            actor.BattlefieldAppliedTeamId = null;
        }

        private static string NormalizeActionName(string actionName, bool allowSitFallback)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return allowSitFallback
                    ? "sit"
                    : CharacterPart.GetActionString(CharacterAction.Stand1);
            }

            return actionName.Trim();
        }

        private void RegisterMeleeAfterImage(
            RemoteUserActor actor,
            int skillId,
            string actionName,
            int currentTime,
            int masteryPercent,
            int chargeElement)
        {
            if (_skillLoader == null
                || actor?.Build == null
                || actor.Assembler == null)
            {
                actor?.ClearMeleeAfterImage();
                return;
            }

            WeaponPart weapon = actor.Build.GetWeapon();
            SkillData skill = skillId > 0 ? _skillLoader.LoadSkill(skillId) : null;
            if (!_skillLoader.TryResolveMeleeAfterImageAction(
                    skill,
                    weapon,
                    actionName,
                    actor.Build.Level,
                    masteryPercent,
                    chargeElement,
                    out MeleeAfterImageAction afterImageAction))
            {
                if (actor.MeleeAfterImage?.FadeStartTime < 0)
                {
                    actor.ClearMeleeAfterImage();
                }

                return;
            }

            actor.ApplyMeleeAfterImage(
                skillId,
                actionName,
                afterImageAction,
                currentTime,
                actor.FacingRight,
                GetActionDuration(actor.Assembler, actionName),
                GetAfterImageFadeDuration(actor.Assembler, actionName));
        }

        private static int GetActionDuration(CharacterAssembler assembler, string actionName)
        {
            AssembledFrame[] animation = assembler?.GetAnimation(actionName);
            if (animation == null || animation.Length == 0)
            {
                return 0;
            }

            int duration = 0;
            foreach (AssembledFrame frame in animation)
            {
                duration += Math.Max(0, frame?.Duration ?? 0);
            }

            return duration;
        }

        private static int GetAfterImageFadeDuration(CharacterAssembler assembler, string actionName)
        {
            return Math.Max(MinimumMeleeAfterImageFadeDurationMs, GetActionDuration(assembler, actionName) / 4);
        }

        private static void DrawMeleeAfterImage(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            int screenX,
            int screenY,
            int currentTime)
        {
            if (actor?.Assembler == null || actor.MeleeAfterImage?.AfterImageAction?.FrameSets == null)
            {
                return;
            }

            RemoteMeleeAfterImageState state = actor.MeleeAfterImage;
            bool activeAction = state.FadeStartTime < 0
                && string.Equals(actor.ActionName, state.ActionName, StringComparison.OrdinalIgnoreCase);
            int frameIndex = state.LastFrameIndex;
            float alpha = 1f;

            if (activeAction)
            {
                int animationTime = Math.Max(0, currentTime - state.AnimationStartTime);
                frameIndex = actor.Assembler.GetFrameIndexAtTime(state.ActionName, animationTime);
                if (frameIndex >= 0)
                {
                    state.LastFrameIndex = frameIndex;
                }
            }
            else if (state.FadeStartTime >= 0)
            {
                int fadeElapsed = Math.Max(0, currentTime - state.FadeStartTime);
                if (fadeElapsed >= state.FadeDuration)
                {
                    actor.ClearMeleeAfterImage();
                    return;
                }

                alpha = 1f - (fadeElapsed / (float)Math.Max(1, state.FadeDuration));
            }

            if (frameIndex < 0
                || !state.AfterImageAction.FrameSets.TryGetValue(frameIndex, out MeleeAfterImageFrameSet frameSet)
                || frameSet?.Frames == null)
            {
                return;
            }

            Color tint = Color.White * MathHelper.Clamp(alpha, 0f, 1f);
            foreach (SkillFrame frame in frameSet.Frames)
            {
                if (frame?.Texture == null)
                {
                    continue;
                }

                bool shouldFlip = state.FacingRight ^ frame.Flip;
                int drawX = shouldFlip
                    ? screenX - (frame.Texture.Width - frame.Origin.X)
                    : screenX - frame.Origin.X;
                int drawY = screenY - frame.Origin.Y;
                frame.Texture.DrawBackground(spriteBatch, skeletonMeshRenderer, null, drawX, drawY, tint, shouldFlip, null);
            }
        }

        private void ApplyMovementSnapshot(RemoteUserActor actor, int currentTime)
        {
            string previousActionName = actor.ActionName;
            PassivePositionSnapshot sampled = actor.MovementSnapshot.SampleAtTime(currentTime);
            actor.Position = new Vector2(sampled.X, sampled.Y);
            actor.FacingRight = sampled.FacingRight;
            actor.CurrentFootholdId = sampled.FootholdId;

            if (actor.MovementDrivenActionSelection)
            {
                actor.ActionName = ResolveActionName(actor, sampled.Action);
                if (!string.Equals(previousActionName, actor.ActionName, StringComparison.OrdinalIgnoreCase))
                {
                    actor.BeginMeleeAfterImageFade(currentTime);
                    RegisterMeleeAfterImage(actor, 0, actor.ActionName, currentTime, 10, 0);
                }
            }
        }

        private static MoveAction MoveActionFromRaw(byte moveAction)
        {
            int normalized = (moveAction >> 1) & 0x0F;
            return Enum.IsDefined(typeof(MoveAction), normalized)
                ? (MoveAction)normalized
                : MoveAction.Stand;
        }

        private static bool DecodeFacingRight(byte moveAction)
        {
            return (moveAction & 1) == 0;
        }

        private static string ResolveActionName(RemoteUserActor actor, MoveAction moveAction)
        {
            bool hasPortableChair = actor?.Build?.ActivePortableChair != null;
            return moveAction switch
            {
                MoveAction.Walk => "walk1",
                MoveAction.Jump or MoveAction.Fall => "jump",
                MoveAction.Ladder => "ladder",
                MoveAction.Rope => "rope",
                MoveAction.Swim => "swim",
                MoveAction.Fly => "fly",
                MoveAction.Attack => string.IsNullOrWhiteSpace(actor?.ActionName) ? "alert" : actor.ActionName,
                MoveAction.Hit => "alert",
                MoveAction.Die => "dead",
                _ => hasPortableChair ? "sit" : CharacterPart.GetActionString(CharacterAction.Stand1)
            };
        }

        private void UpdateNameLookup(string previousName, string currentName, int characterId)
        {
            if (!string.IsNullOrWhiteSpace(previousName)
                && !string.Equals(previousName, currentName, StringComparison.OrdinalIgnoreCase))
            {
                _actorIdsByName.Remove(previousName);
            }

            _actorIdsByName[currentName] = characterId;
        }

        private void ApplyPortableChairMount(RemoteUserActor actor, PortableChair chair)
        {
            if (_loader == null
                || actor?.Build == null
                || chair?.TamingMobItemId is not int tamingMobItemId
                || tamingMobItemId <= 0)
            {
                return;
            }

            CharacterPart mountPart = _loader.LoadEquipment(tamingMobItemId);
            if (mountPart?.Slot != EquipSlot.TamingMob)
            {
                return;
            }

            actor.Build.Equipment.TryGetValue(EquipSlot.TamingMob, out CharacterPart previousMount);
            actor.PortableChairPreviousMount = previousMount;
            actor.PortableChairAppliedMount = true;
            actor.Build.Equip(mountPart);
        }

        private static void ClearPortableChairMountState(RemoteUserActor actor)
        {
            if (actor?.Build == null || !actor.PortableChairAppliedMount)
            {
                return;
            }

            if (actor.PortableChairPreviousMount != null)
            {
                actor.Build.Equip(actor.PortableChairPreviousMount);
            }
            else
            {
                actor.Build.Unequip(EquipSlot.TamingMob);
            }

            actor.PortableChairPreviousMount = null;
            actor.PortableChairAppliedMount = false;
        }
    }

    public sealed class RemoteUserActor
    {
        public RemoteUserActor(
            int characterId,
            string name,
            CharacterBuild build,
            Vector2 position,
            bool facingRight,
            string actionName,
            string sourceTag,
            bool isVisibleInWorld)
        {
            CharacterId = characterId;
            Name = name;
            Build = build;
            Position = position;
            FacingRight = facingRight;
            ActionName = actionName;
            SourceTag = sourceTag;
            IsVisibleInWorld = isVisibleInWorld;
            RefreshAssembler();
        }

        public int CharacterId { get; }
        public string Name { get; set; }
        public CharacterBuild Build { get; set; }
        public CharacterAssembler Assembler { get; private set; }
        public Vector2 Position { get; set; }
        public bool FacingRight { get; set; }
        public string ActionName { get; set; }
        public string SourceTag { get; set; }
        public bool IsVisibleInWorld { get; set; }
        public MinimapUI.HelperMarkerType? HelperMarkerType { get; set; }
        public bool ShowDirectionOverlay { get; set; } = true;
        public int? BattlefieldTeamId { get; set; }
        public RemotePreparedSkillState PreparedSkill { get; set; }
        public CharacterPart PortableChairPreviousMount { get; set; }
        public bool PortableChairAppliedMount { get; set; }
        public PlayerMovementSyncSnapshot MovementSnapshot { get; set; }
        public byte LastMoveActionRaw { get; set; }
        public int CurrentFootholdId { get; set; }
        public bool MovementDrivenActionSelection { get; set; }
        public Dictionary<EquipSlot, CharacterPart> BattlefieldOriginalEquipment { get; set; }
        public float? BattlefieldOriginalSpeed { get; set; }
        public int? BattlefieldAppliedTeamId { get; set; }
        public RemoteMeleeAfterImageState MeleeAfterImage { get; private set; }
        public PacketOwnedUserSummonRegistry PacketOwnedSummons { get; } = new();

        public void RefreshAssembler()
        {
            Assembler = new CharacterAssembler(Build);
        }

        public void ApplyMeleeAfterImage(
            int skillId,
            string actionName,
            MeleeAfterImageAction afterImageAction,
            int currentTime,
            bool facingRight,
            int actionDuration,
            int fadeDuration)
        {
            if (afterImageAction == null
                || string.IsNullOrWhiteSpace(actionName)
                || ((afterImageAction.FrameSets == null || afterImageAction.FrameSets.Count == 0) && !afterImageAction.HasRange))
            {
                MeleeAfterImage = null;
                return;
            }

            MeleeAfterImage = new RemoteMeleeAfterImageState
            {
                SkillId = skillId,
                ActionName = actionName,
                AfterImageAction = afterImageAction,
                AnimationStartTime = currentTime,
                FacingRight = facingRight,
                ActionDuration = actionDuration,
                FadeDuration = Math.Max(60, fadeDuration)
            };
        }

        public void BeginMeleeAfterImageFade(int currentTime)
        {
            if (MeleeAfterImage == null || MeleeAfterImage.FadeStartTime >= 0)
            {
                return;
            }

            if (Assembler != null && !string.IsNullOrWhiteSpace(MeleeAfterImage.ActionName))
            {
                int animationTime = Math.Max(0, currentTime - MeleeAfterImage.AnimationStartTime);
                int frameIndex = Assembler.GetFrameIndexAtTime(MeleeAfterImage.ActionName, animationTime);
                if (frameIndex >= 0)
                {
                    MeleeAfterImage.LastFrameIndex = frameIndex;
                }
            }

            MeleeAfterImage.FadeStartTime = currentTime;
        }

        public void UpdateMeleeAfterImage(int currentTime)
        {
            if (MeleeAfterImage == null)
            {
                return;
            }

            if (MeleeAfterImage.FadeStartTime >= 0)
            {
                if (currentTime - MeleeAfterImage.FadeStartTime >= MeleeAfterImage.FadeDuration)
                {
                    MeleeAfterImage = null;
                }

                return;
            }

            if (!string.Equals(ActionName, MeleeAfterImage.ActionName, StringComparison.OrdinalIgnoreCase))
            {
                BeginMeleeAfterImageFade(currentTime);
                return;
            }

            if (MeleeAfterImage.ActionDuration > 0
                && currentTime - MeleeAfterImage.AnimationStartTime >= MeleeAfterImage.ActionDuration)
            {
                BeginMeleeAfterImageFade(currentTime);
            }
        }

        public void ClearMeleeAfterImage()
        {
            MeleeAfterImage = null;
        }

        public string Describe()
        {
            string helperText = HelperMarkerType?.ToString() ?? "none";
            string teamText = BattlefieldTeamId?.ToString() ?? "none";
            string preparedText = PreparedSkill != null ? PreparedSkill.SkillId.ToString() : "none";
            return $"{CharacterId}:{Name}@({Position.X:0},{Position.Y:0}) action={ActionName} source={SourceTag} helper={helperText} team={teamText} prep={preparedText}";
        }
    }

    public sealed class RemoteMeleeAfterImageState
    {
        public int SkillId { get; init; }
        public string ActionName { get; init; }
        public MeleeAfterImageAction AfterImageAction { get; init; }
        public int AnimationStartTime { get; set; }
        public bool FacingRight { get; init; }
        public int ActionDuration { get; init; }
        public int FadeDuration { get; init; }
        public int FadeStartTime { get; set; } = -1;
        public int LastFrameIndex { get; set; } = -1;
    }

    public sealed class RemotePreparedSkillState
    {
        public int SkillId { get; init; }
        public string SkillName { get; init; }
        public string SkinKey { get; init; } = "KeyDownBar";
        public int DurationMs { get; init; }
        public int GaugeDurationMs { get; init; }
        public int StartTime { get; init; }
        public bool IsKeydownSkill { get; init; }
        public bool IsHolding { get; init; }
        public int MaxHoldDurationMs { get; init; }
        public PreparedSkillHudTextVariant TextVariant { get; init; }
        public bool ShowText { get; init; } = true;
    }

    internal readonly struct RemoteDragonHudMetadata
    {
        public RemoteDragonHudMetadata(int standOriginX, IReadOnlyDictionary<string, int> actionHeights)
        {
            StandOriginX = standOriginX;
            ActionHeights = actionHeights ?? throw new ArgumentNullException(nameof(actionHeights));
        }

        public int StandOriginX { get; }
        public IReadOnlyDictionary<string, int> ActionHeights { get; }

        public int ResolveFrameHeight(string actionName)
        {
            if (!string.IsNullOrWhiteSpace(actionName)
                && ActionHeights.TryGetValue(actionName, out int actionHeight)
                && actionHeight > 0)
            {
                return actionHeight;
            }

            if (ActionHeights.TryGetValue("stand", out int standHeight) && standHeight > 0)
            {
                return standHeight;
            }

            return 1;
        }
    }
}
