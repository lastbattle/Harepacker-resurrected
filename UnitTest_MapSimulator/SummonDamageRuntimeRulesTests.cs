using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator
{
    public sealed class SummonDamageRuntimeRulesTests
    {
        [Fact]
        public void ResolveBodyContactBaseDamage_PrefersPhysicalDamage()
        {
            int damage = SummonDamageRuntimeRules.ResolveBodyContactBaseDamage(
                physicalDamage: 280,
                currentAttackDamage: 190,
                magicalDamage: 75);

            Assert.Equal(280, damage);
        }

        [Fact]
        public void ResolveBodyContactBaseDamage_FallsBackToCurrentAttackThenMagic()
        {
            int attackFallback = SummonDamageRuntimeRules.ResolveBodyContactBaseDamage(
                physicalDamage: 0,
                currentAttackDamage: 190,
                magicalDamage: 75);
            int magicFallback = SummonDamageRuntimeRules.ResolveBodyContactBaseDamage(
                physicalDamage: 0,
                currentAttackDamage: 0,
                magicalDamage: 75);

            Assert.Equal(190, attackFallback);
            Assert.Equal(75, magicFallback);
        }

        [Fact]
        public void ResolveRemainingHealth_UsesMaxHealthWhenCurrentHealthIsMissing()
        {
            int remainingHealth = SummonDamageRuntimeRules.ResolveRemainingHealth(
                currentHealth: 0,
                maxHealth: 400,
                damage: 75);

            Assert.Equal(325, remainingHealth);
        }
    }
}
