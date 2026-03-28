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
            public MobTargetInfo TargetInfo { get; set; }
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
            public MobTargetInfo TargetInfo { get; set; }
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
            public MobTargetInfo TargetInfo { get; set; }
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

        private sealed class ScheduledMobFallingEffect
        {
            public List<IDXObject> Frames { get; set; }
            public Vector2 StartPosition { get; set; }
            public float EndY { get; set; }
            public float FallSpeed { get; set; }
            public float HorizontalDrift { get; set; }
            public int TriggerTime { get; set; }
            public bool Rotation { get; set; }
        }

        private readonly Random _random = new Random();
        private readonly List<ActiveMobProjectile> _activeMobProjectiles = new List<ActiveMobProjectile>();
        private readonly List<ActiveMobGroundAttack> _activeMobGroundAttacks = new List<ActiveMobGroundAttack>();
        private readonly List<ActiveMobDirectAttack> _activeMobDirectAttacks = new List<ActiveMobDirectAttack>();
        private readonly List<ScheduledMobVisualEffect> _scheduledMobVisualEffects = new List<ScheduledMobVisualEffect>();
        private readonly List<ScheduledMobFallingEffect> _scheduledMobFallingEffects = new List<ScheduledMobFallingEffect>();
        private readonly Dictionary<long, int> _scheduledMobActions = new Dictionary<long, int>();
        private readonly List<long> _expiredScheduledActionKeys = new List<long>();
        private readonly List<PuppetInfo> _puppetIterationBuffer = new List<PuppetInfo>();
        private readonly List<MobItem> _mobIterationBuffer = new List<MobItem>();
        private readonly Dictionary<List<IDXObject>, int> _frameCycleDurationCache = new Dictionary<List<IDXObject>, int>();
        private Func<float, float, float?> _groundResolver;
        private Func<IReadOnlyList<PuppetInfo>> _puppetAccessor;
        private Func<int, MobItem> _mobAccessor;
        private Func<IReadOnlyList<MobItem>> _mobListAccessor;
        private Func<bool> _playerGroundedAccessor;
        private Func<Rectangle> _playerHitboxAccessor;
        private Action<PuppetInfo, MobItem, MobAttackEntry, int> _onPuppetHit;

        public void SetGroundResolver(Func<float, float, float?> groundResolver)
        {
            _groundResolver = groundResolver;
        }

        public void SetPuppetTargeting(
            Func<IReadOnlyList<PuppetInfo>> puppetAccessor,
            Action<PuppetInfo, MobItem, MobAttackEntry, int> onPuppetHit)
        {
            _puppetAccessor = puppetAccessor;
            _onPuppetHit = onPuppetHit;
        }

        public void SetMobTargeting(Func<int, MobItem> mobAccessor, Func<IReadOnlyList<MobItem>> mobListAccessor = null)
        {
            _mobAccessor = mobAccessor;
            _mobListAccessor = mobListAccessor;
        }

        public void SetPlayerGroundedAccessor(Func<bool> playerGroundedAccessor)
        {
            _playerGroundedAccessor = playerGroundedAccessor;
        }

        public void SetPlayerHitboxAccessor(Func<Rectangle> playerHitboxAccessor)
        {
            _playerHitboxAccessor = playerHitboxAccessor;
        }

        public void Clear()
        {
            _activeMobProjectiles.Clear();
            _activeMobGroundAttacks.Clear();
            _activeMobDirectAttacks.Clear();
            _scheduledMobVisualEffects.Clear();
            _scheduledMobFallingEffects.Clear();
            _scheduledMobActions.Clear();
            _expiredScheduledActionKeys.Clear();
            _puppetIterationBuffer.Clear();
            _mobIterationBuffer.Clear();
            _frameCycleDurationCache.Clear();
        }

        public void QueueMobAttackActions(MobItem mobItem, int currentTime, float? playerX, float? playerY)
        {
            if (mobItem?.AI == null || mobItem.AI.IsDead || mobItem.AI.State != MobAIState.Attack)
            {
                return;
            }

            MobAttackEntry attack = mobItem.AI.GetCurrentAttack();
            if (attack == null)
            {
                return;
            }

            long actionKey = GetMobActionKey(mobItem, currentTime, 1);
            if (_scheduledMobActions.ContainsKey(actionKey))
            {
                return;
            }

            _scheduledMobActions[actionKey] = currentTime + Math.Max(attack.Cooldown, 2500);
            MobTargetInfo targetInfo = ResolveAttackTarget(mobItem, playerX, playerY);
            float? targetX = targetInfo?.TargetX;
            float? targetY = targetInfo?.TargetY;
            if (UsesLockedTargetResolution(attack, targetInfo) &&
                !CanQueueLockedTargetAttack(mobItem, attack, targetInfo, currentTime))
            {
                targetInfo = null;
            }

            ScheduleSourceAttackEffects(mobItem, attack, currentTime, targetX);

            if (attack.IsAreaOfEffect)
            {
                QueueGroundAttack(mobItem, attack, targetInfo, targetX, targetY, currentTime);
                return;
            }

            if (!attack.IsRanged)
            {
                QueueDirectAttack(mobItem, attack, targetInfo, currentTime);
                return;
            }

            QueueProjectileAttack(mobItem, attack, targetInfo, targetX, targetY, currentTime);
        }

        public void Update(int currentTime, float deltaSeconds, PlayerManager playerManager, AnimationEffects animationEffects, Action<int> onBossGroundImpact)
        {
            UpdateScheduledMobFallingEffects(currentTime, animationEffects);
            UpdateScheduledMobVisualEffects(currentTime, animationEffects);
            UpdateMobProjectiles(currentTime, deltaSeconds, playerManager, animationEffects);
            UpdateMobGroundAttacks(currentTime, playerManager, animationEffects, onBossGroundImpact);
            UpdateMobDirectAttacks(currentTime, playerManager, animationEffects, onBossGroundImpact);
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

        private void QueueProjectileAttack(
            MobItem mobItem,
            MobAttackEntry attack,
            MobTargetInfo targetInfo,
            float? targetX,
            float? targetY,
            int currentTime)
        {
            Vector2 spawn = new Vector2(
                mobItem.CurrentX,
                mobItem.CurrentY - Math.Max(20, mobItem.GetVisualHeight(60) / 2f));

            int projectileCount = Math.Max(1, attack.ProjectileCount);
            for (int i = 0; i < projectileCount; i++)
            {
                float spread = projectileCount == 1 ? 0f : (i - (projectileCount - 1) / 2f) * 45f;
                float resolvedTargetX = targetX ?? (mobItem.CurrentX + (mobItem.MovementInfo.FlipX ? attack.Range : -attack.Range));
                float resolvedTargetY = targetY ?? mobItem.CurrentY - 20f;
                resolvedTargetX += spread;

                Vector2 target = new Vector2(resolvedTargetX, resolvedTargetY);
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
                    TargetInfo = targetInfo?.Clone(),
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

        private void QueueDirectAttack(MobItem mobItem, MobAttackEntry attack, MobTargetInfo targetInfo, int currentTime)
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
                TargetInfo = targetInfo?.Clone(),
                TriggerTime = currentTime + GetDirectAttackTriggerDelay(attack),
                ExpireTime = currentTime + Math.Max(attack.Cooldown, 350),
            });
        }

        private void QueueGroundAttack(
            MobItem mobItem,
            MobAttackEntry attack,
            MobTargetInfo targetInfo,
            float? targetX,
            float? targetY,
            int currentTime)
        {
            List<Vector2> targets = BuildGroundTargets(mobItem, attack, targetX, targetY);
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
                    TargetInfo = targetInfo?.Clone(),
                    WarningFrames = warningFrames,
                    Area = CreateGroundArea(target, areaWidth, areaHeight),
                    EffectPosition = target,
                    WarningStartTime = currentTime,
                    TriggerTime = currentTime + triggerDelay + randomDelay,
                    ExpireTime = currentTime + triggerDelay + randomDelay + 350
                });
            }
        }

        private void QueueGroundAttack(
            MobItem mobItem,
            MobAttackEntry attack,
            MobTargetInfo targetInfo,
            float targetX,
            float targetY,
            int currentTime,
            bool immediate)
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
                TargetInfo = targetInfo?.Clone(),
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

        private void UpdateScheduledMobFallingEffects(int currentTime, AnimationEffects animationEffects)
        {
            for (int i = _scheduledMobFallingEffects.Count - 1; i >= 0; i--)
            {
                ScheduledMobFallingEffect effect = _scheduledMobFallingEffects[i];
                if (currentTime < effect.TriggerTime)
                {
                    continue;
                }

                animationEffects?.AddFalling(
                    effect.Frames,
                    effect.StartPosition.X,
                    effect.StartPosition.Y,
                    effect.EndY,
                    effect.FallSpeed,
                    effect.HorizontalDrift,
                    effect.Rotation,
                    currentTime);
                _scheduledMobFallingEffects.RemoveAt(i);
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

                bool targetedSummoned = projectile.TargetInfo?.TargetType == MobTargetType.Summoned;
                if (TryApplyPuppetHit(projectile.SourceMob, projectile.Attack, projectile.TargetInfo, projectile.GetHitbox(), currentTime) ||
                    TryApplyTargetMobHit(projectile.SourceMob, projectile.Attack, projectile.TargetInfo, projectile.GetHitbox(), currentTime) ||
                    (!targetedSummoned &&
                     projectile.TargetInfo?.TargetType != MobTargetType.Mob &&
                     playerManager?.Combat != null &&
                    playerManager.IsPlayerActive &&
                     playerManager.Combat.TryApplyMobHit(projectile.SourceMob, projectile.GetHitbox(), currentTime, projectile.Attack)))
                {
                    SpawnMobWorldEffects(projectile.SourceMob, projectile.Attack, projectile.Position, currentTime, animationEffects);
                    _activeMobProjectiles.RemoveAt(i);
                    continue;
                }

                if (currentTime < projectile.ExpireTime && Vector2.DistanceSquared(projectile.Position, projectile.Target) > 400f)
                {
                    continue;
                }

                if (TryApplyLockedTargetImpact(projectile.SourceMob, projectile.Attack, projectile.TargetInfo, currentTime))
                {
                    SpawnMobWorldEffects(projectile.SourceMob, projectile.Attack, projectile.Target, currentTime, animationEffects);
                    _activeMobProjectiles.RemoveAt(i);
                    continue;
                }

                if (projectile.CreatesGroundHazard)
                {
                    QueueGroundAttack(
                        projectile.SourceMob,
                        projectile.Attack,
                        projectile.TargetInfo,
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
                    bool usesLockedTarget = UsesLockedTargetResolution(groundAttack.Attack, groundAttack.TargetInfo);

                    if (usesLockedTarget)
                    {
                        TryApplyLockedTargetImpact(
                            groundAttack.SourceMob,
                            groundAttack.Attack,
                            groundAttack.TargetInfo,
                            currentTime,
                            playerManager);
                    }
                    else
                    {
                        bool targetedSummoned = groundAttack.TargetInfo?.TargetType == MobTargetType.Summoned;
                        bool targetedMob = groundAttack.TargetInfo?.TargetType == MobTargetType.Mob;
                        TryApplyPuppetHit(groundAttack.SourceMob, groundAttack.Attack, groundAttack.TargetInfo, groundAttack.Area, currentTime);
                        TryApplyTargetMobHit(groundAttack.SourceMob, groundAttack.Attack, groundAttack.TargetInfo, groundAttack.Area, currentTime);

                        if (!targetedSummoned &&
                            !targetedMob &&
                            playerManager?.Combat != null &&
                            playerManager.IsPlayerActive &&
                            CanHitPlayerTarget(groundAttack.Attack))
                        {
                            playerManager.Combat.TryApplyMobHit(
                                groundAttack.SourceMob,
                                groundAttack.Area,
                                currentTime,
                                groundAttack.Attack);
                        }

                        if (!targetedSummoned && !targetedMob)
                        {
                            ApplyAreaPuppetHits(groundAttack.SourceMob, groundAttack.Attack, groundAttack.Area, groundAttack.TargetInfo, currentTime);
                            ApplyAreaMobHits(groundAttack.SourceMob, groundAttack.Attack, groundAttack.Area, groundAttack.TargetInfo, currentTime);
                        }
                    }

                    SpawnMobWorldEffects(groundAttack.SourceMob, groundAttack.Attack, groundAttack.EffectPosition, currentTime, animationEffects);

                    if (groundAttack.SourceMob.AI.IsBoss || groundAttack.Attack?.Tremble == true)
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

        private void UpdateMobDirectAttacks(int currentTime, PlayerManager playerManager, AnimationEffects animationEffects, Action<int> onBossGroundImpact)
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
                    Rectangle attackArea = BuildDirectAttackArea(directAttack.SourceMob, directAttack.Attack);
                    Vector2 effectPosition = new Vector2(
                        attackArea.X + attackArea.Width / 2f,
                        attackArea.Y + attackArea.Height);
                    bool usesLockedTarget = UsesLockedTargetResolution(directAttack.Attack, directAttack.TargetInfo);

                    if (usesLockedTarget)
                    {
                        TryApplyLockedTargetImpact(
                            directAttack.SourceMob,
                            directAttack.Attack,
                            directAttack.TargetInfo,
                            currentTime,
                            playerManager);
                    }
                    else
                    {
                        bool targetedSummoned = directAttack.TargetInfo?.TargetType == MobTargetType.Summoned;
                        bool targetedMob = directAttack.TargetInfo?.TargetType == MobTargetType.Mob;
                        TryApplyPuppetHit(directAttack.SourceMob, directAttack.Attack, directAttack.TargetInfo, attackArea, currentTime);
                        TryApplyTargetMobHit(directAttack.SourceMob, directAttack.Attack, directAttack.TargetInfo, attackArea, currentTime);

                        if (!targetedSummoned &&
                            !targetedMob &&
                            playerManager?.Combat != null &&
                            playerManager.IsPlayerActive &&
                            CanHitPlayerTarget(directAttack.Attack) &&
                            !attackArea.IsEmpty)
                        {
                            playerManager.Combat.TryApplyMobHit(
                                directAttack.SourceMob,
                                attackArea,
                                currentTime,
                                directAttack.Attack);
                        }

                        if (!targetedSummoned && !targetedMob && !attackArea.IsEmpty)
                        {
                            ApplyAreaPuppetHits(directAttack.SourceMob, directAttack.Attack, attackArea, directAttack.TargetInfo, currentTime);
                            ApplyAreaMobHits(directAttack.SourceMob, directAttack.Attack, attackArea, directAttack.TargetInfo, currentTime);
                        }
                    }

                    SpawnMobWorldEffects(
                        directAttack.SourceMob,
                        directAttack.Attack,
                        effectPosition,
                        currentTime,
                        animationEffects);

                    if (directAttack.Attack?.Tremble == true)
                    {
                        onBossGroundImpact?.Invoke(currentTime);
                    }
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

            bool effectFlip = mobItem?.MovementInfo?.FlipX ?? false;

            List<IDXObject> effectFrames = mobItem.GetAttackEffectFrames(attack.AnimationName);
            bool usedEffectAsProjectile = attack.IsRanged && !attack.IsAreaOfEffect && mobItem.GetAttackProjectileFrames(attack.AnimationName) == null;
            if (effectFrames != null && effectFrames.Count > 0 && (!usedEffectAsProjectile || attack.IsAreaOfEffect))
            {
                Vector2 groundedPosition = attack.IsAreaOfEffect ? ResolveGroundPoint(position.X, position.Y) : position;
                animationEffects?.AddOneTime(effectFrames, groundedPosition.X, groundedPosition.Y, effectFlip, currentTime);
            }

            IReadOnlyList<MobAnimationSet.AttackEffectNode> extraEffects = mobItem.GetAttackExtraEffects(attack.AnimationName);
            if (extraEffects == null)
            {
                return;
            }

            foreach (MobAnimationSet.AttackEffectNode extraEffect in extraEffects)
            {
                ScheduleAttackEffectNode(mobItem, attack, extraEffect, position, currentTime, effectFlip);
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

            if (IsFallingEffectNode(effectNode))
            {
                ScheduleFallingEffectNode(mobItem, attack, effectNode, impactPosition, baseTime, flip);
                return;
            }

            if (effectNode.EffectType == 2)
            {
                ScheduleTimedRangeEffectNode(mobItem, attack, effectNode, impactPosition, baseTime, flip);
                return;
            }

            int triggerTime = baseTime + Math.Max(0, effectNode.Delay) + Math.Max(0, effectNode.Start);
            List<Vector2> positions = BuildEffectNodePositions(mobItem, attack, effectNode, impactPosition);
            int interval = ResolveEffectNodeInterval(effectNode, positions.Count);

            for (int i = 0; i < positions.Count; i++)
            {
                List<IDXObject> frames = ResolveEffectNodeSequence(effectNode, i);
                if (frames == null || frames.Count == 0)
                {
                    continue;
                }

                _scheduledMobVisualEffects.Add(new ScheduledMobVisualEffect
                {
                    Frames = frames,
                    Position = ResolveEffectNodePosition(positions[i], effectNode, flip),
                    TriggerTime = triggerTime + (interval * i),
                    Flip = flip
                });
            }
        }

        private void ScheduleTimedRangeEffectNode(
            MobItem mobItem,
            MobAttackEntry attack,
            MobAnimationSet.AttackEffectNode effectNode,
            Vector2 impactPosition,
            int baseTime,
            bool flip)
        {
            int spawnCount = ResolveTimedRangeSpawnCount(effectNode);
            if (spawnCount <= 0)
            {
                return;
            }

            int interval = ResolveTimedRangeInterval(effectNode, spawnCount);
            int triggerTime = baseTime + Math.Max(0, effectNode.Delay) + Math.Max(0, effectNode.Start);
            List<Vector2> positions = BuildTimedRangeEffectPositions(mobItem, attack, effectNode, impactPosition, spawnCount);
            if (positions.Count == 0)
            {
                return;
            }

            for (int i = 0; i < spawnCount; i++)
            {
                List<IDXObject> frames = effectNode.Sequences[i % effectNode.Sequences.Count];
                if (frames == null || frames.Count == 0)
                {
                    continue;
                }

                _scheduledMobVisualEffects.Add(new ScheduledMobVisualEffect
                {
                    Frames = frames,
                    Position = ResolveEffectNodePosition(positions[Math.Min(i, positions.Count - 1)], effectNode, flip),
                    TriggerTime = triggerTime + (interval * i),
                    Flip = flip
                });
            }
        }

        private void ScheduleFallingEffectNode(
            MobItem mobItem,
            MobAttackEntry attack,
            MobAnimationSet.AttackEffectNode effectNode,
            Vector2 impactPosition,
            int baseTime,
            bool flip)
        {
            int spawnCount = effectNode.EffectType == 2
                ? ResolveTimedRangeSpawnCount(effectNode)
                : Math.Max(1, effectNode.Sequences.Count);
            if (spawnCount <= 0)
            {
                return;
            }

            List<Vector2> positions = effectNode.EffectType == 2
                ? BuildTimedRangeEffectPositions(mobItem, attack, effectNode, impactPosition, spawnCount)
                : BuildEffectNodePositions(mobItem, attack, effectNode, impactPosition);
            if (positions.Count == 0)
            {
                return;
            }

            int interval = effectNode.EffectType == 2
                ? ResolveTimedRangeInterval(effectNode, spawnCount)
                : 0;
            int triggerTime = baseTime + Math.Max(0, effectNode.Delay) + Math.Max(0, effectNode.Start);

            for (int i = 0; i < spawnCount; i++)
            {
                List<IDXObject> frames = effectNode.Sequences[i % effectNode.Sequences.Count];
                if (frames == null || frames.Count == 0)
                {
                    continue;
                }

                Vector2 basePosition = positions[Math.Min(i, positions.Count - 1)];
                float endY = basePosition.Y - effectNode.OffsetY;
                float fallDistance = Math.Max(1f, effectNode.Fall > 0 ? effectNode.Fall : 120f);
                float startY = endY - fallDistance;
                float horizontalOffset = ResolveEffectNodeHorizontalOffset(effectNode, flip);
                float startX = basePosition.X + horizontalOffset;
                float durationMs = Math.Max(120, GetCachedSequenceDuration(frames));
                float fallSpeed = fallDistance * 1000f / durationMs;
                float horizontalDrift = horizontalOffset == 0f
                    ? 0f
                    : Math.Clamp((-2f * horizontalOffset) / Math.Max(fallDistance, 1f), -1f, 1f);

                _scheduledMobFallingEffects.Add(new ScheduledMobFallingEffect
                {
                    Frames = frames,
                    StartPosition = new Vector2(startX, startY),
                    EndY = endY,
                    FallSpeed = Math.Max(120f, fallSpeed),
                    HorizontalDrift = horizontalDrift,
                    TriggerTime = triggerTime + (interval * i),
                    Rotation = true
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
                int spawnCount = effectNode.Count > 0
                    ? effectNode.Count
                    : sequenceCount;
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

        private List<Vector2> BuildTimedRangeEffectPositions(
            MobItem mobItem,
            MobAttackEntry attack,
            MobAnimationSet.AttackEffectNode effectNode,
            Vector2 impactPosition,
            int spawnCount)
        {
            float left = GetRelativeLeft(mobItem, effectNode.HasRangeBounds, effectNode.RangeBounds.Left, effectNode.RangeBounds.Right);
            float right = GetRelativeRight(mobItem, effectNode.HasRangeBounds, effectNode.RangeBounds.Left, effectNode.RangeBounds.Right);
            float baseY = GetRelativeBottom(mobItem, effectNode.HasRangeBounds, effectNode.RangeBounds.Bottom, impactPosition.Y);

            if (!effectNode.HasRangeBounds)
            {
                left = impactPosition.X;
                right = impactPosition.X;
            }

            return BuildRangePositions(left, right, spawnCount, effectNode.RandomPos, baseY);
        }

        private static int ResolveTimedRangeSpawnCount(MobAnimationSet.AttackEffectNode effectNode)
        {
            if (effectNode == null)
            {
                return 0;
            }

            if (effectNode.Count > 0)
            {
                return effectNode.Count;
            }

            if (effectNode.Duration > 0 && effectNode.Interval > 0)
            {
                return Math.Max(1, (effectNode.Duration / effectNode.Interval) + 1);
            }

            return Math.Max(1, effectNode.Sequences.Count);
        }

        private static int ResolveTimedRangeInterval(MobAnimationSet.AttackEffectNode effectNode, int spawnCount)
        {
            if (effectNode == null)
            {
                return 0;
            }

            if (effectNode.Interval > 0)
            {
                return effectNode.Interval;
            }

            if (effectNode.Duration > 0 && spawnCount > 1)
            {
                return Math.Max(1, effectNode.Duration / (spawnCount - 1));
            }

            return 0;
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

        private static bool IsFallingEffectNode(MobAnimationSet.AttackEffectNode effectNode)
        {
            return effectNode != null && effectNode.Fall > 0;
        }

        private static Vector2 ResolveEffectNodePosition(Vector2 basePosition, MobAnimationSet.AttackEffectNode effectNode, bool flip)
        {
            if (effectNode == null)
            {
                return basePosition;
            }

            return new Vector2(
                basePosition.X + ResolveEffectNodeHorizontalOffset(effectNode, flip),
                basePosition.Y - effectNode.OffsetY);
        }

        private static float ResolveEffectNodeHorizontalOffset(MobAnimationSet.AttackEffectNode effectNode, bool flip)
        {
            if (effectNode == null || effectNode.OffsetX == 0)
            {
                return 0f;
            }

            return flip ? effectNode.OffsetX : -effectNode.OffsetX;
        }

        private static int ResolveSequenceDuration(List<IDXObject> frames)
        {
            if (frames == null || frames.Count == 0)
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                total += Math.Max(frames[i].Delay, 1);
            }

            return total;
        }

        private static int ResolveEffectNodeInterval(MobAnimationSet.AttackEffectNode effectNode, int spawnCount)
        {
            if (effectNode == null || spawnCount <= 1)
            {
                return 0;
            }

            if (effectNode.Interval > 0)
            {
                return effectNode.Interval;
            }

            if (effectNode.Duration <= 0)
            {
                return 0;
            }

            return Math.Max(1, effectNode.Duration / (spawnCount - 1));
        }

        private static List<IDXObject> ResolveEffectNodeSequence(MobAnimationSet.AttackEffectNode effectNode, int index)
        {
            if (effectNode?.Sequences == null || effectNode.Sequences.Count == 0)
            {
                return null;
            }

            int sequenceIndex = index % effectNode.Sequences.Count;
            if (sequenceIndex < 0)
            {
                sequenceIndex += effectNode.Sequences.Count;
            }

            return effectNode.Sequences[sequenceIndex];
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

        private MobTargetInfo ResolveAttackTarget(MobItem mobItem, float? playerX, float? playerY)
        {
            MobTargetInfo target = mobItem?.AI?.Target;
            if (target?.IsValid == true)
            {
                MobTargetInfo resolvedTarget = target.Clone();
                if (resolvedTarget.TargetType == MobTargetType.Summoned)
                {
                    PuppetInfo puppet = FindTargetPuppet(resolvedTarget);
                    if (puppet != null)
                    {
                        resolvedTarget.TargetId = puppet.ObjectId;
                        resolvedTarget.TargetSlotIndex = puppet.SummonSlotIndex;
                        resolvedTarget.TargetX = puppet.X;
                        resolvedTarget.TargetY = puppet.Y;
                    }
                }

                return resolvedTarget;
            }

            if (playerX.HasValue && playerY.HasValue)
            {
                return new MobTargetInfo
                {
                    TargetType = MobTargetType.Player,
                    TargetX = playerX.Value,
                    TargetY = playerY.Value,
                    IsValid = true
                };
            }

            return null;
        }

        private bool TryApplyPuppetHit(
            MobItem sourceMob,
            MobAttackEntry attack,
            MobTargetInfo targetInfo,
            Rectangle hitbox,
            int currentTime)
        {
            if (hitbox.IsEmpty || targetInfo?.TargetType != MobTargetType.Summoned)
            {
                return false;
            }

            PuppetInfo puppet = FindTargetPuppet(targetInfo);
            if (puppet == null || !CanHitPuppetTarget(attack, puppet) || !CreatePuppetHitbox(puppet).Intersects(hitbox))
            {
                return false;
            }

            _onPuppetHit?.Invoke(puppet, sourceMob, attack, currentTime);
            return true;
        }

        private bool TryApplyLockedTargetImpact(
            MobItem sourceMob,
            MobAttackEntry attack,
            MobTargetInfo targetInfo,
            int currentTime,
            PlayerManager playerManager = null)
        {
            if (targetInfo == null)
            {
                return false;
            }

            if (targetInfo.TargetType == MobTargetType.Player)
            {
                if (playerManager?.Combat == null || !playerManager.IsPlayerActive || !CanHitPlayerTarget(attack))
                {
                    return false;
                }

                return playerManager.Combat.TryApplyMobHit(
                    sourceMob,
                    playerManager.Player.GetHitbox(),
                    currentTime,
                    attack);
            }

            if (targetInfo.TargetType == MobTargetType.Summoned)
            {
                PuppetInfo puppet = FindTargetPuppet(targetInfo);
                if (puppet == null || !CanHitPuppetTarget(attack, puppet))
                {
                    return false;
                }

                _onPuppetHit?.Invoke(puppet, sourceMob, attack, currentTime);
                return true;
            }

            if (targetInfo.TargetType != MobTargetType.Mob)
            {
                return false;
            }

            MobItem targetMob = _mobAccessor?.Invoke(targetInfo.TargetId);
            if (targetMob?.AI == null || targetMob.AI.IsDead || ReferenceEquals(targetMob, sourceMob))
            {
                return false;
            }

            if (!CanApplyMobVsMobDamage(sourceMob, targetMob))
            {
                return false;
            }

            return ApplyMobDamage(sourceMob, attack, targetMob, currentTime);
        }

        private bool CanQueueLockedTargetAttack(MobItem sourceMob, MobAttackEntry attack, MobTargetInfo targetInfo, int currentTime)
        {
            if (sourceMob == null || attack == null || targetInfo?.IsValid != true)
            {
                return false;
            }

            if (!TryGetLockedTargetAdmissionSource(sourceMob, attack, out Vector2 sourcePoint))
            {
                Rectangle fallbackAdmissionArea = BuildAttackAdmissionArea(sourceMob, attack);
                if (fallbackAdmissionArea.IsEmpty)
                {
                    return true;
                }

                Rectangle fallbackTargetHitbox = GetTargetHitbox(targetInfo, currentTime);
                if (!fallbackTargetHitbox.IsEmpty)
                {
                    return fallbackAdmissionArea.Intersects(fallbackTargetHitbox);
                }

                Point fallbackTargetPoint = new Point(
                    (int)MathF.Round(targetInfo.TargetX),
                    (int)MathF.Round(targetInfo.TargetY));
                return fallbackAdmissionArea.Contains(fallbackTargetPoint);
            }

            Rectangle targetHitbox = GetTargetHitbox(targetInfo, currentTime);
            if (!targetHitbox.IsEmpty)
            {
                Vector2 targetPoint = ResolveLockedTargetAdmissionPoint(
                    targetHitbox,
                    sourcePoint.Y,
                    sourceMob.MovementInfo?.FlipX ?? true);

                float distanceSquared = Vector2.DistanceSquared(sourcePoint, targetPoint);
                float maxDistance = Math.Max(Math.Abs(attack.Range), 1) + 10f;
                return distanceSquared <= (maxDistance * maxDistance);
            }

            Vector2 fallbackPoint = new Vector2(targetInfo.TargetX, targetInfo.TargetY);
            float fallbackDistanceSquared = Vector2.DistanceSquared(sourcePoint, fallbackPoint);
            float fallbackMaxDistance = Math.Max(Math.Abs(attack.Range), 1) + 10f;
            return fallbackDistanceSquared <= (fallbackMaxDistance * fallbackMaxDistance);
        }

        private static bool UsesLockedTargetResolution(MobAttackEntry attack, MobTargetInfo targetInfo)
        {
            return attack?.AttackType == 1 && targetInfo != null && targetInfo.IsValid;
        }

        private bool CanHitPlayerTarget(MobAttackEntry attack)
        {
            return attack?.IsJumpAttack != true || (_playerGroundedAccessor?.Invoke() ?? true);
        }

        private static bool CanHitPuppetTarget(MobAttackEntry attack, PuppetInfo puppet)
        {
            return attack?.IsJumpAttack != true || puppet?.IsGrounded == true;
        }

        private bool TryApplyTargetMobHit(
            MobItem sourceMob,
            MobAttackEntry attack,
            MobTargetInfo targetInfo,
            Rectangle hitbox,
            int currentTime)
        {
            if (hitbox.IsEmpty || targetInfo?.TargetType != MobTargetType.Mob)
            {
                return false;
            }

            MobItem targetMob = _mobAccessor?.Invoke(targetInfo.TargetId);
            if (targetMob?.AI == null || targetMob.AI.IsDead || ReferenceEquals(targetMob, sourceMob))
            {
                return false;
            }

            Rectangle targetHitbox = targetMob.GetBodyHitbox(currentTime);
            if (targetHitbox.IsEmpty || !targetHitbox.Intersects(hitbox))
            {
                return false;
            }

            if (!CanApplyMobVsMobDamage(sourceMob, targetMob))
            {
                return false;
            }

            return ApplyMobDamage(sourceMob, attack, targetMob, currentTime);
        }

        private bool ApplyAreaPuppetHits(
            MobItem sourceMob,
            MobAttackEntry attack,
            Rectangle hitbox,
            MobTargetInfo targetInfo,
            int currentTime)
        {
            if (hitbox.IsEmpty)
            {
                return false;
            }

            IReadOnlyList<PuppetInfo> puppets = _puppetAccessor?.Invoke();
            if (puppets == null)
            {
                return false;
            }

            int excludedTargetId = targetInfo?.TargetType == MobTargetType.Summoned ? targetInfo.TargetId : 0;
            bool hitAny = false;
            CopyItems(puppets, _puppetIterationBuffer);
            for (int i = 0; i < _puppetIterationBuffer.Count; i++)
            {
                PuppetInfo puppet = _puppetIterationBuffer[i];
                if (puppet == null || !puppet.IsActive || puppet.ObjectId == excludedTargetId)
                {
                    continue;
                }

                if (!CanHitPuppetTarget(attack, puppet) || !CreatePuppetHitbox(puppet).Intersects(hitbox))
                {
                    continue;
                }

                _onPuppetHit?.Invoke(puppet, sourceMob, attack, currentTime);
                hitAny = true;
            }

            _puppetIterationBuffer.Clear();
            return hitAny;
        }

        private bool ApplyAreaMobHits(
            MobItem sourceMob,
            MobAttackEntry attack,
            Rectangle hitbox,
            MobTargetInfo targetInfo,
            int currentTime)
        {
            if (hitbox.IsEmpty)
            {
                return false;
            }

            IReadOnlyList<MobItem> mobs = _mobListAccessor?.Invoke();
            if (mobs == null)
            {
                return false;
            }

            int excludedTargetId = targetInfo?.TargetType == MobTargetType.Mob ? targetInfo.TargetId : 0;
            bool hitAny = false;
            CopyItems(mobs, _mobIterationBuffer);
            for (int i = 0; i < _mobIterationBuffer.Count; i++)
            {
                MobItem mob = _mobIterationBuffer[i];
                if (mob?.AI == null || mob.AI.IsDead || ReferenceEquals(mob, sourceMob) || mob.PoolId == excludedTargetId)
                {
                    continue;
                }

                Rectangle targetHitbox = mob.GetBodyHitbox(currentTime);
                if (targetHitbox.IsEmpty || !targetHitbox.Intersects(hitbox))
                {
                    continue;
                }

                if (!CanApplyMobVsMobDamage(sourceMob, mob))
                {
                    continue;
                }

                hitAny |= ApplyMobDamage(sourceMob, attack, mob, currentTime);
            }

            _mobIterationBuffer.Clear();
            return hitAny;
        }

        private static bool CanApplyMobVsMobDamage(MobItem sourceMob, MobItem targetMob)
        {
            if (sourceMob == null || targetMob == null || ReferenceEquals(sourceMob, targetMob))
            {
                return false;
            }

            return sourceMob.UsesMobCombatLane || targetMob.UsesMobCombatLane;
        }

        private static bool ApplyMobDamage(MobItem sourceMob, MobAttackEntry attack, MobItem targetMob, int currentTime)
        {
            int baseDamage = attack?.Damage ?? 0;
            int damage = sourceMob?.AI?.CalculateOutgoingDamage(baseDamage, MobDamageType.Physical) ?? Math.Max(1, baseDamage);
            damage = Math.Max(1, damage);

            bool died = targetMob.ApplyDamage(
                damage,
                currentTime,
                false,
                sourceMob?.CurrentX,
                sourceMob?.CurrentY,
                originatedFromPlayer: false);

            if (!died && targetMob.MovementInfo != null && sourceMob != null)
            {
                bool knockbackRight = targetMob.CurrentX >= sourceMob.CurrentX;
                float knockbackForce = Math.Max(18f, Math.Min(attack?.Range ?? 18, 48));
                targetMob.MovementInfo.ApplyKnockback(knockbackForce, knockbackRight);
            }

            return true;
        }

        private PuppetInfo FindTargetPuppet(MobTargetInfo targetInfo)
        {
            IReadOnlyList<PuppetInfo> puppets = _puppetAccessor?.Invoke();
            if (puppets == null || targetInfo == null)
            {
                return null;
            }

            foreach (PuppetInfo puppet in puppets)
            {
                if (puppet != null && puppet.IsActive && puppet.ObjectId == targetInfo.TargetId)
                {
                    return puppet;
                }
            }

            int slotIndex = targetInfo.TargetSlotIndex >= 0 ? targetInfo.TargetSlotIndex : targetInfo.TargetId;
            if (slotIndex < 0)
            {
                return null;
            }

            foreach (PuppetInfo puppet in puppets)
            {
                if (puppet != null && puppet.IsActive && puppet.SummonSlotIndex == slotIndex)
                {
                    return puppet;
                }
            }

            return null;
        }

        private Rectangle GetTargetHitbox(MobTargetInfo targetInfo, int currentTime)
        {
            if (targetInfo == null)
            {
                return Rectangle.Empty;
            }

            if (targetInfo.TargetType == MobTargetType.Player)
            {
                return _playerHitboxAccessor?.Invoke() ?? Rectangle.Empty;
            }

            if (targetInfo.TargetType == MobTargetType.Summoned)
            {
                PuppetInfo puppet = FindTargetPuppet(targetInfo);
                return puppet != null ? CreatePuppetHitbox(puppet) : Rectangle.Empty;
            }

            if (targetInfo.TargetType != MobTargetType.Mob)
            {
                return Rectangle.Empty;
            }

            MobItem targetMob = _mobAccessor?.Invoke(targetInfo.TargetId);
            return targetMob?.AI != null && !targetMob.AI.IsDead
                ? targetMob.GetBodyHitbox(currentTime)
                : Rectangle.Empty;
        }

        private static bool TryGetLockedTargetAdmissionSource(MobItem sourceMob, MobAttackEntry attack, out Vector2 sourcePoint)
        {
            sourcePoint = Vector2.Zero;
            if (sourceMob == null || attack == null)
            {
                return false;
            }

            bool faceRight = sourceMob.MovementInfo?.FlipX ?? true;
            if (attack.HasRangeBounds)
            {
                float originX = faceRight
                    ? sourceMob.CurrentX + attack.RangeLeft
                    : sourceMob.CurrentX - attack.RangeLeft;
                float originY = sourceMob.CurrentY + attack.RangeTop;
                sourcePoint = new Vector2(originX, originY);
                return true;
            }

            float fallbackOffsetX = Math.Max(Math.Abs(attack.Range) * 0.5f, 12f);
            float fallbackX = sourceMob.CurrentX + (faceRight ? fallbackOffsetX : -fallbackOffsetX);
            float fallbackY = sourceMob.CurrentY - Math.Max(attack.AreaHeight * 0.5f, 20f);
            sourcePoint = new Vector2(fallbackX, fallbackY);
            return true;
        }

        private static Vector2 ResolveLockedTargetAdmissionPoint(Rectangle targetHitbox, float sourceY, bool sourceFacesRight)
        {
            float centerX = (targetHitbox.Left + targetHitbox.Right) * 0.5f;
            float anchorX = sourceFacesRight
                ? Math.Min(centerX, targetHitbox.Left + 10f)
                : Math.Max(centerX, targetHitbox.Right - 10f);
            float anchorY = MathHelper.Clamp(sourceY, targetHitbox.Top, targetHitbox.Bottom);
            return new Vector2(anchorX, anchorY);
        }

        private static Rectangle BuildAttackAdmissionArea(MobItem mobItem, MobAttackEntry attack)
        {
            if (mobItem == null || attack == null)
            {
                return Rectangle.Empty;
            }

            if (attack.HasRangeBounds)
            {
                return BuildDirectAttackArea(mobItem, attack);
            }

            if (attack.Range <= 0)
            {
                return Rectangle.Empty;
            }

            int width = Math.Max(attack.Range, 40);
            int height = Math.Max(attack.AreaHeight, 100);
            bool faceRight = mobItem.MovementInfo?.FlipX ?? true;
            int left = faceRight
                ? (int)MathF.Round(mobItem.CurrentX)
                : (int)MathF.Round(mobItem.CurrentX - width);

            return new Rectangle(
                left,
                (int)MathF.Round(mobItem.CurrentY - height),
                width,
                height);
        }

        private static Rectangle CreatePuppetHitbox(PuppetInfo puppet)
        {
            if (puppet != null && !puppet.Hitbox.IsEmpty)
            {
                return puppet.Hitbox;
            }

            const int width = 48;
            const int height = 60;
            return new Rectangle(
                (int)(puppet.X - width / 2f),
                (int)(puppet.Y - height),
                width,
                height);
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

            _expiredScheduledActionKeys.Clear();
            foreach (var pair in _scheduledMobActions)
            {
                if (currentTime >= pair.Value)
                {
                    _expiredScheduledActionKeys.Add(pair.Key);
                }
            }

            for (int i = 0; i < _expiredScheduledActionKeys.Count; i++)
            {
                _scheduledMobActions.Remove(_expiredScheduledActionKeys[i]);
            }
        }

        private static long GetMobActionKey(MobItem mobItem, int currentTime, int actionType)
        {
            int stateStartTime = currentTime - mobItem.AI.StateElapsed(currentTime);
            return ((long)actionType << 56) | ((long)(mobItem.PoolId & 0xFFFFFF) << 24) | (uint)stateStartTime;
        }

        private IDXObject GetProjectileFrame(ActiveMobProjectile projectile, int currentTime)
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
            return GetFrameAtTime(projectile.Frames, relativeTime, loop: true);
        }

        private IDXObject GetLoopingFrame(List<IDXObject> frames, int currentTime, int startTime)
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
            return GetFrameAtTime(frames, relativeTime, loop: true);
        }

        private IDXObject GetFrameAtTime(List<IDXObject> frames, int relativeTime, bool loop)
        {
            int cycleDuration = GetCachedSequenceDuration(frames);
            if (cycleDuration <= 0)
            {
                return frames[0];
            }

            if (loop)
            {
                relativeTime %= cycleDuration;
            }
            else if (relativeTime >= cycleDuration)
            {
                return frames[frames.Count - 1];
            }

            int total = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                IDXObject frame = frames[i];
                total += Math.Max(frame.Delay, 1);
                if (relativeTime < total)
                {
                    return frame;
                }
            }

            return frames[frames.Count - 1];
        }

        private int GetCachedSequenceDuration(List<IDXObject> frames)
        {
            if (frames == null || frames.Count == 0)
            {
                return 0;
            }

            if (_frameCycleDurationCache.TryGetValue(frames, out int duration))
            {
                return duration;
            }

            duration = ResolveSequenceDuration(frames);
            _frameCycleDurationCache[frames] = duration;
            return duration;
        }

        private static void CopyItems<T>(IReadOnlyList<T> source, List<T> destination)
        {
            destination.Clear();
            if (source == null || source.Count == 0)
            {
                return;
            }

            if (destination.Capacity < source.Count)
            {
                destination.Capacity = source.Count;
            }

            for (int i = 0; i < source.Count; i++)
            {
                destination.Add(source[i]);
            }
        }
    }
}
