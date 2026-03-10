using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Entities;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.Combat
{
    /// <summary>
    /// Handles delayed mob projectiles and grounded boss attack visuals after AI picks an action.
    /// </summary>
    public sealed class MobAttackSystem
    {
        private sealed class ActiveMobProjectile
        {
            public MobItem SourceMob { get; set; }
            public MobAttackEntry Attack { get; set; }
            public List<IDXObject> Frames { get; set; }
            public Vector2 Position { get; set; }
            public Vector2 Velocity { get; set; }
            public Vector2 Target { get; set; }
            public int SpawnTime { get; set; }
            public int ExpireTime { get; set; }
            public bool CreatesGroundHazard { get; set; }
            public bool Flip { get; set; }

            public Rectangle GetHitbox()
            {
                return new Rectangle((int)Position.X - 8, (int)Position.Y - 8, 16, 16);
            }
        }

        private sealed class ActiveMobGroundAttack
        {
            public MobItem SourceMob { get; set; }
            public MobAttackEntry Attack { get; set; }
            public List<IDXObject> WarningFrames { get; set; }
            public Rectangle Area { get; set; }
            public Vector2 EffectPosition { get; set; }
            public int WarningStartTime { get; set; }
            public int TriggerTime { get; set; }
            public int ExpireTime { get; set; }
            public bool Triggered { get; set; }
        }

        private sealed class ActiveMobDirectAttack
        {
            public MobItem SourceMob { get; set; }
            public MobAttackEntry Attack { get; set; }
            public Rectangle Area { get; set; }
            public Vector2 EffectPosition { get; set; }
            public int TriggerTime { get; set; }
            public int ExpireTime { get; set; }
            public bool Triggered { get; set; }
        }

        private sealed class ScheduledMobVisualEffect
        {
            public List<IDXObject> Frames { get; set; }
            public Vector2 Position { get; set; }
            public int TriggerTime { get; set; }
            public bool Flip { get; set; }
        }

        private readonly Random _random = new Random();
        private readonly List<ActiveMobProjectile> _activeMobProjectiles = new List<ActiveMobProjectile>();
        private readonly List<ActiveMobGroundAttack> _activeMobGroundAttacks = new List<ActiveMobGroundAttack>();
        private readonly List<ActiveMobDirectAttack> _activeMobDirectAttacks = new List<ActiveMobDirectAttack>();
        private readonly List<ScheduledMobVisualEffect> _scheduledMobVisualEffects = new List<ScheduledMobVisualEffect>();
        private readonly Dictionary<long, int> _scheduledMobActions = new Dictionary<long, int>();
        private Func<float, float, float?> _groundResolver;

        public void SetGroundResolver(Func<float, float, float?> groundResolver)
        {
            _groundResolver = groundResolver;
        }

        public void Clear()
        {
            _activeMobProjectiles.Clear();
            _activeMobGroundAttacks.Clear();
            _activeMobDirectAttacks.Clear();
            _scheduledMobVisualEffects.Clear();
            _scheduledMobActions.Clear();
        }

        public void QueueMobAttackActions(MobItem mobItem, int currentTime, float? playerX, float? playerY)
        {
            if (mobItem?.AI == null || mobItem.AI.IsDead || mobItem.AI.State != MobAIState.Attack)
            {
                return;
            }

            MobAttackEntry attack = mobItem.AI.GetCurrentAttack();
            if (attack == null || (!attack.IsRanged && !attack.IsAreaOfEffect))
            {
                return;
            }

            long actionKey = GetMobActionKey(mobItem, currentTime, 1);
            if (_scheduledMobActions.ContainsKey(actionKey))
            {
                return;
            }

            _scheduledMobActions[actionKey] = currentTime + Math.Max(attack.Cooldown, 2500);
            ScheduleSourceAttackEffects(mobItem, attack, currentTime, playerX);

            if (attack.IsAreaOfEffect)
            {
                QueueGroundAttack(mobItem, attack, playerX, playerY, currentTime);
                return;
            }

            if (!attack.IsRanged)
            {
                QueueDirectAttack(mobItem, attack, currentTime);
                return;
            }

            QueueProjectileAttack(mobItem, attack, playerX, playerY, currentTime);
        }

        public void Update(int currentTime, float deltaSeconds, PlayerManager playerManager, AnimationEffects animationEffects, Action<int> onBossGroundImpact)
        {
            UpdateScheduledMobVisualEffects(currentTime, animationEffects);
            UpdateMobProjectiles(currentTime, deltaSeconds, playerManager, animationEffects);
            UpdateMobGroundAttacks(currentTime, playerManager, animationEffects, onBossGroundImpact);
            UpdateMobDirectAttacks(currentTime, playerManager, animationEffects);
            CleanupScheduledMobActions(currentTime);
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D debugTexture, int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            if (spriteBatch == null)
            {
                return;
            }

            int drawShiftX = mapShiftX - centerX;
            int drawShiftY = mapShiftY - centerY;

            foreach (var projectile in _activeMobProjectiles)
            {
                IDXObject frame = GetProjectileFrame(projectile, currentTime);
                if (frame != null)
                {
                    frame.DrawObject(
                        spriteBatch,
                        null,
                        null,
                        drawShiftX - (int)projectile.Position.X,
                        drawShiftY - (int)projectile.Position.Y,
                        projectile.Flip,
                        null);
                    continue;
                }

                if (debugTexture != null)
                {
                    Rectangle hitbox = projectile.GetHitbox();
                    Rectangle screenRect = new Rectangle(
                        hitbox.X - mapShiftX + centerX,
                        hitbox.Y - mapShiftY + centerY,
                        hitbox.Width,
                        hitbox.Height);

                    spriteBatch.Draw(debugTexture, screenRect, Color.OrangeRed * 0.9f);
                }
            }

            foreach (var groundAttack in _activeMobGroundAttacks)
            {
                IDXObject warningFrame = !groundAttack.Triggered
                    ? GetLoopingFrame(groundAttack.WarningFrames, currentTime, groundAttack.WarningStartTime)
                    : null;
                if (warningFrame != null)
                {
                    warningFrame.DrawObject(
                        spriteBatch,
                        null,
                        null,
                        drawShiftX - (int)groundAttack.EffectPosition.X,
                        drawShiftY - (int)groundAttack.EffectPosition.Y,
                        false,
                        null);
                    continue;
                }

                if (debugTexture == null)
                {
                    continue;
                }

                Color color = groundAttack.Triggered
                    ? Color.OrangeRed * 0.45f
                    : Color.Gold * 0.25f;

                Rectangle screenRect = new Rectangle(
                    groundAttack.Area.X - mapShiftX + centerX,
                    groundAttack.Area.Y - mapShiftY + centerY,
                    groundAttack.Area.Width,
                    groundAttack.Area.Height);

                spriteBatch.Draw(debugTexture, screenRect, color);
            }
        }

        private void QueueProjectileAttack(MobItem mobItem, MobAttackEntry attack, float? playerX, float? playerY, int currentTime)
        {
            Vector2 spawn = new Vector2(
                mobItem.CurrentX,
                mobItem.CurrentY - Math.Max(20, mobItem.GetVisualHeight(60) / 2f));

            int projectileCount = Math.Max(1, attack.ProjectileCount);
            for (int i = 0; i < projectileCount; i++)
            {
                float spread = projectileCount == 1 ? 0f : (i - (projectileCount - 1) / 2f) * 45f;
                float targetX = playerX ?? (mobItem.CurrentX + (mobItem.MovementInfo.FlipX ? attack.Range : -attack.Range));
                float targetY = playerY ?? mobItem.CurrentY - 20f;
                targetX += spread;

                Vector2 target = new Vector2(targetX, targetY);
                Vector2 direction = target - spawn;
                if (direction.LengthSquared() < 1f)
                {
                    direction = new Vector2(mobItem.MovementInfo.FlipX ? 1f : -1f, 0f);
                }
                else
                {
                    direction.Normalize();
                }

                float speed = Math.Max(220f, attack.BulletSpeed > 0 ? attack.BulletSpeed : 320f);
                float travelDistance = Vector2.Distance(spawn, target);
                int travelTime = Math.Max(250, (int)(travelDistance / speed * 1000f));

                _activeMobProjectiles.Add(new ActiveMobProjectile
                {
                    SourceMob = mobItem,
                    Attack = attack,
                    Frames = mobItem.GetAttackProjectileFrames(attack.AnimationName),
                    Position = spawn,
                    Velocity = direction * speed,
                    Target = target,
                    SpawnTime = currentTime,
                    ExpireTime = currentTime + travelTime,
                    CreatesGroundHazard = mobItem.AI.IsBoss && (attack.IsAreaOfEffect || attack.Range >= 180),
                    Flip = direction.X < 0f
                });
            }
        }

        private void QueueDirectAttack(MobItem mobItem, MobAttackEntry attack, int currentTime)
        {
            Rectangle attackArea = BuildDirectAttackArea(mobItem, attack);
            if (attackArea.IsEmpty)
            {
                return;
            }

            _activeMobDirectAttacks.Add(new ActiveMobDirectAttack
            {
                SourceMob = mobItem,
                Attack = attack,
                Area = attackArea,
                EffectPosition = new Vector2(
                    attackArea.X + attackArea.Width / 2f,
                    attackArea.Y + attackArea.Height),
                TriggerTime = currentTime + GetDirectAttackTriggerDelay(attack),
                ExpireTime = currentTime + Math.Max(attack.Cooldown, 350),
            });
        }

        private void QueueGroundAttack(MobItem mobItem, MobAttackEntry attack, float? playerX, float? playerY, int currentTime)
        {
            List<Vector2> targets = BuildGroundTargets(mobItem, attack, playerX, playerY);
            if (targets.Count == 0)
            {
                return;
            }

            List<IDXObject> warningFrames = mobItem.GetAttackWarningFrames(attack.AnimationName);
            int triggerDelay = Math.Max(attack.Delay, attack.EffectAfter);

            foreach (Vector2 target in targets)
            {
                int randomDelay = attack.RandomDelayWindow > 0 ? _random.Next(attack.RandomDelayWindow) : 0;
                int areaWidth = DetermineGroundAreaWidth(mobItem, attack, warningFrames);
                int areaHeight = DetermineGroundAreaHeight(attack);

                _activeMobGroundAttacks.Add(new ActiveMobGroundAttack
                {
                    SourceMob = mobItem,
                    Attack = attack,
                    WarningFrames = warningFrames,
                    Area = CreateGroundArea(target, areaWidth, areaHeight),
                    EffectPosition = target,
                    WarningStartTime = currentTime,
                    TriggerTime = currentTime + triggerDelay + randomDelay,
                    ExpireTime = currentTime + triggerDelay + randomDelay + 350
                });
            }
        }

        private void QueueGroundAttack(MobItem mobItem, MobAttackEntry attack, float targetX, float targetY, int currentTime, bool immediate)
        {
            Vector2 groundedTarget = ResolveGroundPoint(targetX, targetY);
            List<IDXObject> warningFrames = mobItem.GetAttackWarningFrames(attack.AnimationName);
            int areaWidth = DetermineGroundAreaWidth(mobItem, attack, warningFrames);
            int areaHeight = DetermineGroundAreaHeight(attack);
            int triggerDelay = immediate ? 40 : Math.Max(attack.Delay, attack.EffectAfter);

            _activeMobGroundAttacks.Add(new ActiveMobGroundAttack
            {
                SourceMob = mobItem,
                Attack = attack,
                WarningFrames = warningFrames,
                Area = CreateGroundArea(groundedTarget, areaWidth, areaHeight),
                EffectPosition = groundedTarget,
                WarningStartTime = currentTime,
                TriggerTime = currentTime + triggerDelay,
                ExpireTime = currentTime + triggerDelay + 300
            });
        }

        private void UpdateScheduledMobVisualEffects(int currentTime, AnimationEffects animationEffects)
        {
            for (int i = _scheduledMobVisualEffects.Count - 1; i >= 0; i--)
            {
                ScheduledMobVisualEffect effect = _scheduledMobVisualEffects[i];
                if (currentTime < effect.TriggerTime)
                {
                    continue;
                }

                animationEffects?.AddOneTime(effect.Frames, effect.Position.X, effect.Position.Y, effect.Flip, currentTime);
                _scheduledMobVisualEffects.RemoveAt(i);
            }
        }

        private void UpdateMobProjectiles(int currentTime, float deltaSeconds, PlayerManager playerManager, AnimationEffects animationEffects)
        {
            for (int i = _activeMobProjectiles.Count - 1; i >= 0; i--)
            {
                ActiveMobProjectile projectile = _activeMobProjectiles[i];
                if (projectile.SourceMob?.AI == null)
                {
                    _activeMobProjectiles.RemoveAt(i);
                    continue;
                }

                projectile.Position += projectile.Velocity * deltaSeconds;

                if (playerManager?.Combat != null &&
                    playerManager.IsPlayerActive &&
                    playerManager.Combat.TryApplyMobHit(projectile.SourceMob, projectile.GetHitbox(), currentTime, projectile.Attack))
                {
                    SpawnMobWorldEffects(projectile.SourceMob, projectile.Attack, projectile.Position, currentTime, animationEffects);
                    _activeMobProjectiles.RemoveAt(i);
                    continue;
                }

                if (currentTime < projectile.ExpireTime && Vector2.DistanceSquared(projectile.Position, projectile.Target) > 400f)
                {
                    continue;
                }

                if (projectile.CreatesGroundHazard)
                {
                    QueueGroundAttack(
                        projectile.SourceMob,
                        projectile.Attack,
                        projectile.Target.X,
                        projectile.Target.Y,
                        currentTime,
                        immediate: true);
                }
                else
                {
                    SpawnMobWorldEffects(projectile.SourceMob, projectile.Attack, projectile.Target, currentTime, animationEffects);
                }

                _activeMobProjectiles.RemoveAt(i);
            }
        }

        private void UpdateMobGroundAttacks(int currentTime, PlayerManager playerManager, AnimationEffects animationEffects, Action<int> onBossGroundImpact)
        {
            for (int i = _activeMobGroundAttacks.Count - 1; i >= 0; i--)
            {
                ActiveMobGroundAttack groundAttack = _activeMobGroundAttacks[i];
                if (groundAttack.SourceMob?.AI == null)
                {
                    _activeMobGroundAttacks.RemoveAt(i);
                    continue;
                }

                if (!groundAttack.Triggered && currentTime >= groundAttack.TriggerTime)
                {
                    groundAttack.Triggered = true;

                    if (playerManager?.Combat != null && playerManager.IsPlayerActive)
                    {
                        playerManager.Combat.TryApplyMobHit(
                            groundAttack.SourceMob,
                            groundAttack.Area,
                            currentTime,
                            groundAttack.Attack);
                    }

                    SpawnMobWorldEffects(groundAttack.SourceMob, groundAttack.Attack, groundAttack.EffectPosition, currentTime, animationEffects);

                    if (groundAttack.SourceMob.AI.IsBoss)
                    {
                        onBossGroundImpact?.Invoke(currentTime);
                    }
                }

                if (currentTime >= groundAttack.ExpireTime)
                {
                    _activeMobGroundAttacks.RemoveAt(i);
                }
            }
        }

        private void UpdateMobDirectAttacks(int currentTime, PlayerManager playerManager, AnimationEffects animationEffects)
        {
            for (int i = _activeMobDirectAttacks.Count - 1; i >= 0; i--)
            {
                ActiveMobDirectAttack directAttack = _activeMobDirectAttacks[i];
                if (directAttack.SourceMob?.AI == null)
                {
                    _activeMobDirectAttacks.RemoveAt(i);
                    continue;
                }

                if (!directAttack.Triggered && currentTime >= directAttack.TriggerTime)
                {
                    directAttack.Triggered = true;

                    if (playerManager?.Combat != null && playerManager.IsPlayerActive)
                    {
                        playerManager.Combat.TryApplyMobHit(
                            directAttack.SourceMob,
                            directAttack.Area,
                            currentTime,
                            directAttack.Attack);
                    }

                    SpawnMobWorldEffects(
                        directAttack.SourceMob,
                        directAttack.Attack,
                        directAttack.EffectPosition,
                        currentTime,
                        animationEffects);
                }

                if (currentTime >= directAttack.ExpireTime)
                {
                    _activeMobDirectAttacks.RemoveAt(i);
                }
            }
        }

        private void SpawnMobWorldEffects(MobItem mobItem, MobAttackEntry attack, Vector2 position, int currentTime, AnimationEffects animationEffects)
        {
            if (ShouldUseSourceAnchoredEffects(mobItem, attack))
            {
                return;
            }

            List<IDXObject> effectFrames = mobItem.GetAttackEffectFrames(attack.AnimationName);
            bool usedEffectAsProjectile = attack.IsRanged && !attack.IsAreaOfEffect && mobItem.GetAttackProjectileFrames(attack.AnimationName) == null;
            if (effectFrames != null && effectFrames.Count > 0 && (!usedEffectAsProjectile || attack.IsAreaOfEffect))
            {
                Vector2 groundedPosition = attack.IsAreaOfEffect ? ResolveGroundPoint(position.X, position.Y) : position;
                animationEffects?.AddOneTime(effectFrames, groundedPosition.X, groundedPosition.Y, false, currentTime);
            }

            IReadOnlyList<MobAnimationSet.AttackEffectNode> extraEffects = mobItem.GetAttackExtraEffects(attack.AnimationName);
            if (extraEffects == null)
            {
                return;
            }

            foreach (MobAnimationSet.AttackEffectNode extraEffect in extraEffects)
            {
                ScheduleAttackEffectNode(mobItem, attack, extraEffect, position, currentTime, false);
            }
        }

        private void ScheduleSourceAttackEffects(MobItem mobItem, MobAttackEntry attack, int currentTime, float? playerX)
        {
            if (!ShouldUseSourceAnchoredEffects(mobItem, attack))
            {
                return;
            }

            int triggerTime = currentTime + Math.Max(0, Math.Max(attack.Delay, attack.EffectAfter));
            bool faceLeft = DetermineSourceAttackFacesLeft(mobItem, attack, playerX);
            Vector2 sourcePosition = GetSourceEffectPosition(mobItem, attack, currentTime, faceLeft);
            bool flip = !faceLeft;

            List<IDXObject> effectFrames = mobItem.GetAttackEffectFrames(attack.AnimationName);
            if (effectFrames != null && effectFrames.Count > 0)
            {
                _scheduledMobVisualEffects.Add(new ScheduledMobVisualEffect
                {
                    Frames = effectFrames,
                    Position = sourcePosition,
                    TriggerTime = triggerTime,
                    Flip = flip
                });
            }

            IReadOnlyList<MobAnimationSet.AttackEffectNode> extraEffects = mobItem.GetAttackExtraEffects(attack.AnimationName);
            if (extraEffects == null)
            {
                return;
            }

            foreach (MobAnimationSet.AttackEffectNode extraEffect in extraEffects)
            {
                ScheduleAttackEffectNode(mobItem, attack, extraEffect, sourcePosition, triggerTime, flip);
            }
        }

        private void ScheduleAttackEffectNode(
            MobItem mobItem,
            MobAttackEntry attack,
            MobAnimationSet.AttackEffectNode effectNode,
            Vector2 impactPosition,
            int baseTime,
            bool flip)
        {
            if (effectNode == null || effectNode.Sequences.Count == 0)
            {
                return;
            }

            int triggerTime = baseTime + Math.Max(0, effectNode.Delay);
            List<Vector2> positions = BuildEffectNodePositions(mobItem, attack, effectNode, impactPosition);

            for (int i = 0; i < positions.Count; i++)
            {
                List<IDXObject> frames = effectNode.Sequences[Math.Min(i, effectNode.Sequences.Count - 1)];
                if (frames == null || frames.Count == 0)
                {
                    continue;
                }

                _scheduledMobVisualEffects.Add(new ScheduledMobVisualEffect
                {
                    Frames = frames,
                    Position = positions[i],
                    TriggerTime = triggerTime,
                    Flip = flip
                });
            }
        }

        private List<Vector2> BuildGroundTargets(MobItem mobItem, MobAttackEntry attack, float? playerX, float? playerY)
        {
            var targets = new List<Vector2>();
            int areaCount = Math.Max(1, attack.AreaCount);
            int attackCount = attack.AttackCount > 0 ? Math.Min(attack.AttackCount, areaCount) : 1;

            if (attack.AreaCount > 0 || attack.AttackCount > 0)
            {
                List<Vector2> slotPositions = BuildRangeSlotPositions(mobItem, attack, areaCount, playerX, playerY);
                if (slotPositions.Count == 0)
                {
                    return targets;
                }

                Shuffle(slotPositions);
                int count = Math.Min(attackCount, slotPositions.Count);
                for (int i = 0; i < count; i++)
                {
                    targets.Add(slotPositions[i]);
                }

                targets.Sort((left, right) => left.X.CompareTo(right.X));
                return targets;
            }

            float fallbackX = playerX ?? GetRangeCenterX(mobItem, attack);
            float fallbackY = playerY ?? GetRangeBottomY(mobItem, attack);
            targets.Add(ResolveGroundPoint(fallbackX, fallbackY));
            return targets;
        }

        private List<Vector2> BuildEffectNodePositions(
            MobItem mobItem,
            MobAttackEntry attack,
            MobAnimationSet.AttackEffectNode effectNode,
            Vector2 impactPosition)
        {
            if (effectNode.EffectType == 1)
            {
                int sequenceCount = Math.Max(1, effectNode.Sequences.Count);
                int spawnCount = sequenceCount;
                float left = GetRelativeLeft(mobItem, effectNode.HasRangeBounds, effectNode.RangeBounds.Left, effectNode.RangeBounds.Right);
                float right = GetRelativeRight(mobItem, effectNode.HasRangeBounds, effectNode.RangeBounds.Left, effectNode.RangeBounds.Right);
                if (!effectNode.HasRangeBounds)
                {
                    left = impactPosition.X;
                    right = impactPosition.X;
                }

                float width = Math.Abs(right - left);
                if (effectNode.EffectDistance > 0 && width > 0f)
                {
                    spawnCount = Math.Max(spawnCount, (int)Math.Floor(width / effectNode.EffectDistance) + 1);
                }

                return BuildRangePositions(left, right, spawnCount, effectNode.RandomPos, GetRelativeBottom(mobItem, effectNode.HasRangeBounds, effectNode.RangeBounds.Bottom, impactPosition.Y));
            }

            return new List<Vector2> { ResolveGroundPoint(impactPosition.X, impactPosition.Y) };
        }

        private List<Vector2> BuildRangeSlotPositions(MobItem mobItem, MobAttackEntry attack, int slotCount, float? playerX, float? playerY)
        {
            if (slotCount <= 0)
            {
                return new List<Vector2>();
            }

            float left;
            float right;
            float baseY;

            if (attack.HasRangeBounds)
            {
                left = GetRelativeLeft(mobItem, true, attack.RangeLeft, attack.RangeRight);
                right = GetRelativeRight(mobItem, true, attack.RangeLeft, attack.RangeRight);
                baseY = GetRangeBottomY(mobItem, attack);
            }
            else
            {
                float centerX = playerX ?? GetRangeCenterX(mobItem, attack);
                float halfWidth = Math.Max(attack.AreaWidth, attack.Range) / 2f;
                left = centerX - halfWidth;
                right = centerX + halfWidth;
                baseY = playerY ?? mobItem.CurrentY;
            }

            if (slotCount == 1)
            {
                return new List<Vector2> { ResolveGroundPoint((left + right) * 0.5f, baseY) };
            }

            float center = (left + right) * 0.5f;
            float spacing = Math.Abs(right - left) / Math.Max(1, slotCount - 1);
            if (spacing <= 0f)
            {
                spacing = Math.Max(40f, attack.EffectAfter > 0 ? attack.EffectAfter / 10f : 60f);
            }

            var positions = new List<Vector2>(slotCount);
            for (int i = 0; i < slotCount; i++)
            {
                float slotIndex = attack.StartOffset + i;
                float x = center + slotIndex * spacing;
                positions.Add(ResolveGroundPoint(x, baseY));
            }

            return positions;
        }

        private List<Vector2> BuildRangePositions(float left, float right, int count, bool randomPos, float baseY)
        {
            count = Math.Max(1, count);
            if (count == 1)
            {
                return new List<Vector2> { ResolveGroundPoint((left + right) * 0.5f, baseY) };
            }

            var positions = new List<Vector2>(count);
            float minX = Math.Min(left, right);
            float maxX = Math.Max(left, right);
            if (randomPos)
            {
                for (int i = 0; i < count; i++)
                {
                    float x = minX + (float)_random.NextDouble() * Math.Max(1f, maxX - minX);
                    positions.Add(ResolveGroundPoint(x, baseY));
                }

                positions.Sort((a, b) => a.X.CompareTo(b.X));
                return positions;
            }

            float step = (right - left) / (count - 1);
            for (int i = 0; i < count; i++)
            {
                positions.Add(ResolveGroundPoint(left + step * i, baseY));
            }

            return positions;
        }

        private Rectangle CreateGroundArea(Vector2 target, int width, int height)
        {
            return new Rectangle(
                (int)(target.X - width / 2f),
                (int)(target.Y - height),
                width,
                height);
        }

        private int DetermineGroundAreaWidth(MobItem mobItem, MobAttackEntry attack, List<IDXObject> warningFrames)
        {
            int warningWidth = warningFrames != null && warningFrames.Count > 0
                ? Math.Max(warningFrames[0].Width, 1)
                : 0;
            int effectWidth = mobItem.GetAttackEffectFrames(attack.AnimationName) is List<IDXObject> effectFrames && effectFrames.Count > 0
                ? Math.Max(effectFrames[0].Width / 2, 1)
                : 0;

            return Math.Max(48, Math.Max(Math.Max(attack.AreaWidth, warningWidth), effectWidth));
        }

        private static int DetermineGroundAreaHeight(MobAttackEntry attack)
        {
            return Math.Max(60, attack.AreaHeight);
        }

        private static Rectangle BuildDirectAttackArea(MobItem mobItem, MobAttackEntry attack)
        {
            if (mobItem?.MovementInfo == null || attack == null)
            {
                return Rectangle.Empty;
            }

            float mobX = mobItem.MovementInfo.X;
            float mobY = mobItem.MovementInfo.Y;
            bool facingRight = mobItem.MovementInfo.FlipX;

            if (attack.HasRangeBounds)
            {
                int left = attack.RangeLeft;
                int right = attack.RangeRight;
                if (facingRight)
                {
                    left = -attack.RangeRight;
                    right = -attack.RangeLeft;
                }

                int top = attack.RangeTop;
                int bottom = attack.RangeBottom;
                return new Rectangle(
                    (int)mobX + left,
                    (int)mobY + top,
                    Math.Max(1, right - left),
                    Math.Max(1, bottom - top));
            }

            int width = Math.Max(50, attack.Range);
            int height = Math.Max(60, attack.AreaHeight);
            return new Rectangle(
                (int)mobX + (facingRight ? 0 : -width),
                (int)mobY - height + 20,
                width,
                height);
        }

        private static int GetDirectAttackTriggerDelay(MobAttackEntry attack)
        {
            if (attack == null)
            {
                return 0;
            }

            if (attack.AttackAfter > 0)
            {
                return attack.AttackAfter;
            }

            if (attack.Delay > 0)
            {
                return attack.Delay;
            }

            if (attack.EffectAfter > 0)
            {
                return attack.EffectAfter;
            }

            return 200;
        }

        private Vector2 ResolveGroundPoint(float x, float preferredY)
        {
            float groundedY = _groundResolver?.Invoke(x, preferredY) ?? preferredY;
            return new Vector2(x, groundedY);
        }

        private static float GetRangeCenterX(MobItem mobItem, MobAttackEntry attack)
        {
            if (!attack.HasRangeBounds)
            {
                return mobItem.CurrentX + (mobItem.MovementInfo.FlipX ? attack.Range / 2f : -attack.Range / 2f);
            }

            return (GetRelativeLeft(mobItem, true, attack.RangeLeft, attack.RangeRight) +
                    GetRelativeRight(mobItem, true, attack.RangeLeft, attack.RangeRight)) * 0.5f;
        }

        private static float GetRangeBottomY(MobItem mobItem, MobAttackEntry attack)
        {
            if (!attack.HasRangeBounds)
            {
                return mobItem.CurrentY;
            }

            return mobItem.CurrentY + attack.RangeBottom;
        }

        private static float GetRelativeLeft(MobItem mobItem, bool hasRangeBounds, int left, int right)
        {
            if (!hasRangeBounds)
            {
                return mobItem.CurrentX;
            }

            int rangeLeft = left;
            int rangeRight = right;
            if (mobItem.MovementInfo.FlipX)
            {
                rangeLeft = -right;
                rangeRight = -left;
            }

            return mobItem.CurrentX + Math.Min(rangeLeft, rangeRight);
        }

        private static float GetRelativeRight(MobItem mobItem, bool hasRangeBounds, int left, int right)
        {
            if (!hasRangeBounds)
            {
                return mobItem.CurrentX;
            }

            int rangeLeft = left;
            int rangeRight = right;
            if (mobItem.MovementInfo.FlipX)
            {
                rangeLeft = -right;
                rangeRight = -left;
            }

            return mobItem.CurrentX + Math.Max(rangeLeft, rangeRight);
        }

        private static float GetRelativeBottom(MobItem mobItem, bool hasRangeBounds, int bottom, float fallbackY)
        {
            return hasRangeBounds ? mobItem.CurrentY + bottom : fallbackY;
        }

        private bool ShouldUseSourceAnchoredEffects(MobItem mobItem, MobAttackEntry attack)
        {
            if (attack == null || !attack.IsRanged || attack.IsAreaOfEffect)
            {
                return false;
            }

            if (mobItem.GetAttackProjectileFrames(attack.AnimationName)?.Count > 0)
            {
                return false;
            }

            List<IDXObject> effectFrames = mobItem.GetAttackEffectFrames(attack.AnimationName);
            if (effectFrames == null || effectFrames.Count == 0)
            {
                return false;
            }

            if (mobItem.GetAttackExtraEffects(attack.AnimationName)?.Count > 0)
            {
                return true;
            }

            if (!attack.HasRangeBounds)
            {
                return false;
            }

            int rangeWidth = Math.Abs(attack.RangeRight - attack.RangeLeft);
            return rangeWidth > effectFrames[0].Width + 80;
        }

        private Vector2 GetSourceEffectPosition(MobItem mobItem, MobAttackEntry attack, int currentTime, bool faceLeft)
        {
            // For source-anchored mob beams like 8510000 attack3/info/effect, the effect frame's
            // WZ origin is the attach point. Place that origin directly at the mob's world x/y.
            return new Vector2(mobItem.CurrentX, mobItem.CurrentY);
        }

        private bool DetermineSourceAttackFacesLeft(MobItem mobItem, MobAttackEntry attack, float? playerX)
        {
            // Source-anchored mob effects should follow the mob's own facing, not the player's position.
            if (mobItem.MovementInfo != null)
            {
                return !mobItem.MovementInfo.FlipX;
            }

            if (attack.HasRangeBounds)
            {
                float rawCenter = (attack.RangeLeft + attack.RangeRight) * 0.5f;
                return rawCenter <= 0f;
            }

            return !(mobItem.MovementInfo?.FlipX ?? false);
        }

        private static void GetDirectedRangeEdges(MobItem mobItem, MobAttackEntry attack, bool faceLeft, out float left, out float right)
        {
            int rangeLeft = attack.RangeLeft;
            int rangeRight = attack.RangeRight;
            if (!faceLeft)
            {
                rangeLeft = -attack.RangeRight;
                rangeRight = -attack.RangeLeft;
            }

            left = mobItem.CurrentX + Math.Min(rangeLeft, rangeRight);
            right = mobItem.CurrentX + Math.Max(rangeLeft, rangeRight);
        }

        private void Shuffle<T>(List<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int swapIndex = _random.Next(i + 1);
                (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
            }
        }

        private void CleanupScheduledMobActions(int currentTime)
        {
            if (_scheduledMobActions.Count == 0)
            {
                return;
            }

            var expiredKeys = new List<long>();
            foreach (var pair in _scheduledMobActions)
            {
                if (currentTime >= pair.Value)
                {
                    expiredKeys.Add(pair.Key);
                }
            }

            foreach (long key in expiredKeys)
            {
                _scheduledMobActions.Remove(key);
            }
        }

        private static long GetMobActionKey(MobItem mobItem, int currentTime, int actionType)
        {
            int stateStartTime = currentTime - mobItem.AI.StateElapsed(currentTime);
            return ((long)actionType << 56) | ((long)(mobItem.PoolId & 0xFFFFFF) << 24) | (uint)stateStartTime;
        }

        private static IDXObject GetProjectileFrame(ActiveMobProjectile projectile, int currentTime)
        {
            if (projectile?.Frames == null || projectile.Frames.Count == 0)
            {
                return null;
            }

            if (projectile.Frames.Count == 1)
            {
                return projectile.Frames[0];
            }

            int relativeTime = Math.Max(0, currentTime - projectile.SpawnTime);
            int total = 0;
            foreach (IDXObject frame in projectile.Frames)
            {
                total += Math.Max(frame.Delay, 1);
                if (relativeTime < total)
                {
                    return frame;
                }
            }

            int cycleDuration = total;
            if (cycleDuration <= 0)
            {
                return projectile.Frames[0];
            }

            relativeTime %= cycleDuration;
            total = 0;
            foreach (IDXObject frame in projectile.Frames)
            {
                total += Math.Max(frame.Delay, 1);
                if (relativeTime < total)
                {
                    return frame;
                }
            }

            return projectile.Frames[projectile.Frames.Count - 1];
        }

        private static IDXObject GetLoopingFrame(List<IDXObject> frames, int currentTime, int startTime)
        {
            if (frames == null || frames.Count == 0)
            {
                return null;
            }

            if (frames.Count == 1)
            {
                return frames[0];
            }

            int relativeTime = Math.Max(0, currentTime - startTime);
            int cycleDuration = 0;
            foreach (IDXObject frame in frames)
            {
                cycleDuration += Math.Max(frame.Delay, 1);
            }

            if (cycleDuration <= 0)
            {
                return frames[0];
            }

            relativeTime %= cycleDuration;
            int total = 0;
            foreach (IDXObject frame in frames)
            {
                total += Math.Max(frame.Delay, 1);
                if (relativeTime < total)
                {
                    return frame;
                }
            }

            return frames[frames.Count - 1];
        }
    }
}
