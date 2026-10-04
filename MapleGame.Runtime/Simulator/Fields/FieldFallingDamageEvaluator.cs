using System;

namespace HaCreator.MapSimulator.Fields
{
    public readonly struct FallDamageResult
    {
        public FallDamageResult(int damage, float fallDistance, float impactVelocityY)
        {
            Damage = damage;
            FallDistance = fallDistance;
            ImpactVelocityY = impactVelocityY;
        }

        public int Damage { get; }
        public float FallDistance { get; }
        public float ImpactVelocityY { get; }
        public bool ShouldApply => Damage > 0;
    }

    public static class FieldFallingDamageEvaluator
    {
        private const float SafeFallDistance = 600f;
        private const float MinimumImpactVelocity = 900f;
        private const float DamageStepDistance = 80f;
        private const int BaseDamagePercent = 1;
        private const int MaximumDamagePercent = 35;

        public static FallDamageResult Evaluate(int maxHp, float fallStartY, float landingY, float impactVelocityY, bool damageSuppressed)
        {
            if (damageSuppressed || maxHp <= 0)
            {
                return default;
            }

            float fallDistance = Math.Max(0f, landingY - fallStartY);
            float impactSpeed = Math.Max(0f, impactVelocityY);
            if (fallDistance < SafeFallDistance || impactSpeed < MinimumImpactVelocity)
            {
                return default;
            }

            float distanceBeyondSafeThreshold = fallDistance - SafeFallDistance;
            int damagePercent = BaseDamagePercent + (int)MathF.Floor(distanceBeyondSafeThreshold / DamageStepDistance);
            damagePercent = Math.Clamp(damagePercent, BaseDamagePercent, MaximumDamagePercent);

            int damage = (int)Math.Ceiling(maxHp * (damagePercent / 100f));
            return damage <= 0
                ? default
                : new FallDamageResult(damage, fallDistance, impactSpeed);
        }
    }
}
