namespace HaCreator.MapSimulator.Character.Skills;

internal static class ClientShootAttackFamilyResolver
{
    public const int QueuedFinalAttackShootRange0 = 65;
    private const int DefaultShootAttackPointYOffset = -28;
    private const int MechanicTamingMobItemId = 1932016;

    public static bool UsesClientShootAttackLane(int skillId)
    {
        return skillId switch
        {
            5001003 or 5210000 => true,
            _ => UsesShootLaneWithoutMeleeFallback(skillId)
        };
    }

    public static bool UsesShootLaneWithoutMeleeFallback(int skillId)
    {
        return skillId switch
        {
            3001004 or 3001005 or 3100001 or 3101003 or 3101005 or 3110001 or 3111003 or 3111004 or 3111006
                or 3121003 or 3121004 or 3200001 or 3201003 or 3201005 or 3210001 or 3211003 or 3211004
                or 3211006 or 3221001 or 3221003 or 3221007 or 4001344 or 4101005 or 4111004 or 4111005
                or 4121003 or 4121007 or 4221003 or 5121002 or 5201001 or 5201006 or 5211004 or 5211005
                or 5211006 or 5220011 or 5221004 or 5221007 or 5221008 or 11101004 or 13001003 or 13101002
                or 13101005 or 13111000 or 13111001 or 13111002 or 13111006 or 13111007 or 14001004
                or 14101006 or 14111002 or 14111005 or 14111006 or 15111006 or 15111007 or 21100004
                or 21110004 or 21120006 or 33001000 or 33101001 or 33101002 or 33101007 or 33111001
                or 33121001 or 33121005 or 33121009 or 35001001 or 35001004 or 35101009 or 35101010
                or 35111004 or 35111015 or 35121005 or 35121012 or 35121013 => true,
            _ => false
        };
    }

    public static int ResolveQueuedFinalAttackShootRange0(SkillData skill)
    {
        return skill?.AttackType == SkillAttackType.Ranged && skill.Projectile == null
            ? QueuedFinalAttackShootRange0
            : 0;
    }

    internal static int ResolveFallbackShootAttackPointYOffset(
        int skillId,
        int jobId,
        int mountedTamingMobItemId = 0,
        int bodyRelMoveY = 0,
        bool mountedBodyRelMoveVehicle = false)
    {
        int offsetY = DefaultShootAttackPointYOffset;

        if (IsPositionUpSkillOnRiding(skillId, jobId))
        {
            if (mountedBodyRelMoveVehicle)
            {
                offsetY += bodyRelMoveY;
            }
            else if (mountedTamingMobItemId == MechanicTamingMobItemId)
            {
                offsetY -= 17;
            }
        }

        return skillId switch
        {
            33101007 => offsetY - 12,
            33121005 => offsetY + 11,
            35111015 => offsetY + 10,
            35001004 or 33101001 or 35101010 => offsetY + 5,
            _ => offsetY
        };
    }

    private static bool IsPositionUpSkillOnRiding(int skillId, int jobId)
    {
        return skillId switch
        {
            33001000 or 33100009 or 33101001 or 33111001 or 33121001 or 33121003 or 33121005
                or 33121009 or 35001004 or 35101010 or 35111004 or 35111015 or 35121005 or 35121013 => true,
            0 => IsWildHunterJob(jobId) || IsMechanicJob(jobId),
            _ => false
        };
    }

    private static bool IsWildHunterJob(int jobId)
    {
        return jobId / 100 == 33;
    }

    private static bool IsMechanicJob(int jobId)
    {
        return jobId / 100 == 35;
    }
}
