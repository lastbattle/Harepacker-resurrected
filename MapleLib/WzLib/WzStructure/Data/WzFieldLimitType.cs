using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib.WzLib.WzStructure.Data
{
    public enum WzFieldLimitType
    {
        //Unable_To_Use_AntiMacro_Item = 0, // CField::IsUnableToUseAntiMacroItem = CField *this 
        Unable_To_Jump = 0, // CField::IsUnableToJump = CField *this 
        Unable_To_Use_Skill = 1, // CField::IsUnableToUseSkill = CField *this 
        Unable_To_Use_Summon_Item = 2, // CField::IsUnableToUseSummonItem = CField *this 
        Unable_To_Use_Mystic_Door = 3, // CField::IsUnableToUseMysticDoor = CField *this 
        Unable_To_Migrate = 4, // CField::IsUnableToMigrate = CField *this 
        Unable_To_Use_Portal_Scroll = 5, // CField::IsUnableToUsePortalScroll = CField *this 
        Unable_To_Use_Teleport_Item = 6, // CField::IsUnableToUseTeleportItem = CField *this 
        Unable_To_Open_Mini_Game = 7, // CField::IsUnableToOpenMiniGame = CField *this 
        Unable_To_Use_Specific_Portal_Scroll = 8, // CField::IsUnableToUseSpecificPortalScroll = CField *this 
        Unable_To_Use_Taming_Mob = 9, //  CField::IsUnableToUseTamingMob = CField *this 
        Unable_To_Consume_Stat_Change_Item = 10, // CField::IsUnableToConsumeStatChangeItem = CField *this 
        Unable_To_Change_Party_Boss = 11, // CField::IsUnableToChangePartyBoss = CField *this 
        No_Monster_Capacity_Limit = 12, // TO BE CONFIRMED
        Unable_To_Use_Wedding_Invitation_Item = 13, // CField::IsUnableToUseWeddingInvitationItem = CField *this 
        Unable_To_Use_Cash_Weather = 14, // CField::IsUnableToUseCashWeatherItem = CField *this 
        Unable_To_Use_Pet = 15, // CField::IsUnableToUsePet = CField *this 
        Unable_To_Use_AntiMacro_Item = 16, // TO BE CONFIRMED
        Unable_To_Fall_Down = 17, // CField::IsUnableToFallDown = CField *this 
        Unable_To_Summon_NPC = 18, // CField::IsUnaUnableToUseAntiMacroItembleToSummonNPC = CField *this 
        No_EXP_Decrease = 19, // TO BE CONFIRMED
        No_Damage_On_Falling = 20, // CField::IsNoDamageOnFalling = CField *this 
        Parcel_Open_Limit = 21, // TO BE CONFIRMED
        Drop_Limit = 22, // CField::IsDropLimit = CField *this 
        Unable_To_Use_Rocket_Boost = 23, // CField::IsUnableToUseRocketBoost = CField *this
        No_Item_Option_Limit = 24, // TO BE CONFIRMED
        No_Quest_Alert = 25, // CField::IsNoQuestAlert = CField *this 
        No_Android = 26, // TO BE CONFIRMED
        Auto_Expand_Minimap = 27, // CField::IsAutoExpandMinimap = CField *this 
        Move_Skill_Only = 28, // CField::IsMoveSkillOnly = CField *this 
    }

    public static class FieldLimitTypeExtension
    {
        public static bool Check(int type, int fieldLimit)
        {
            return Check(type, (long)fieldLimit);
        }

        public static bool Check(int type, long fieldLimit)
        {
            return ((fieldLimit >> type) & 1) != 0;
        }

        public static bool Check(this WzFieldLimitType type, int fieldLimit)
        {
            return Check(type, (long) fieldLimit);
        }

        public static bool Check(this WzFieldLimitType type, long fieldLimit)
        {
            return ((fieldLimit >> (int)type) & 1) != 0;
        }

        public static int GetMaxFieldLimitType()
        {
            int max = 0;
            foreach (WzFieldLimitType limitType in Enum.GetValues(typeof(WzFieldLimitType)))
            {
                if ((int)limitType > max)
                    max = (int)limitType;
            }
            return max;
        }
    }
}
