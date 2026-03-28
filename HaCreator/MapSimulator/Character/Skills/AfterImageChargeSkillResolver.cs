namespace HaCreator.MapSimulator.Character.Skills
{
    internal static class AfterImageChargeSkillResolver
    {
        private const int PageFireChargeSkillId = 1211004;
        private const int PageIceChargeSkillId = 1211006;
        private const int PageLightningChargeSkillId = 1211008;
        private const int PaladinHolyChargeSkillId = 1221004;
        private const int ThunderBreakerLightningChargeSkillId = 15101006;
        private const int AranIceChargeSkillId = 21111005;

        public static bool TryGetChargeElement(int skillId, out int chargeElement)
        {
            chargeElement = skillId switch
            {
                PageIceChargeSkillId => 1,
                AranIceChargeSkillId => 1,
                PageFireChargeSkillId => 2,
                PageLightningChargeSkillId => 3,
                ThunderBreakerLightningChargeSkillId => 3,
                PaladinHolyChargeSkillId => 5,
                _ => 0
            };

            return chargeElement > 0;
        }
    }
}
