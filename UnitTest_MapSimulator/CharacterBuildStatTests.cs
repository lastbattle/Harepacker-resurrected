using HaCreator.MapSimulator.Character;

namespace UnitTest_MapSimulator
{
    public class CharacterBuildStatTests
    {
        [Fact]
        public void DisplayTotals_IncludeEquipmentBonusesAndDerivedSecondaryStats()
        {
            CharacterBuild build = new CharacterBuild
            {
                Job = 400,
                STR = 4,
                DEX = 25,
                INT = 4,
                LUK = 40,
                HP = 900,
                MP = 300,
                MaxHP = 1200,
                MaxMP = 450,
                Accuracy = 3,
                Avoidability = 2,
                Hands = 5
            };

            build.Equip(new CharacterPart
            {
                Slot = EquipSlot.Coat,
                BonusSTR = 5,
                BonusDEX = 2,
                BonusLUK = 7,
                BonusHP = 100,
                BonusAccuracy = 11,
                BonusAvoidability = 9,
                BonusWeaponDefense = 8,
                BonusSpeed = 10
            });

            build.Equip(new CharacterPart
            {
                Slot = EquipSlot.Weapon,
                BonusWeaponAttack = 12,
                BonusMagicAttack = 4,
                BonusJump = 5
            });

            Assert.Equal(9, build.TotalSTR);
            Assert.Equal(27, build.TotalDEX);
            Assert.Equal(4, build.TotalINT);
            Assert.Equal(47, build.TotalLUK);
            Assert.Equal(1300, build.TotalMaxHP);
            Assert.Equal(1000, build.TotalHP);
            Assert.Equal(44, build.TotalAccuracy);
            Assert.Equal(47, build.TotalAvoidability);
            Assert.Equal(83, build.TotalHands);
            Assert.Equal(12, build.TotalAttack);
            Assert.Equal(4, build.TotalMagicAttack);
            Assert.Equal(8, build.TotalDefense);
            Assert.Equal(110f, build.TotalSpeed);
            Assert.Equal(105f, build.TotalJumpPower);
        }

        [Fact]
        public void IncreaseMaxHp_UsesJobAwareGrowthAndConsumesAp()
        {
            CharacterBuild build = new CharacterBuild
            {
                Job = 100,
                MaxHP = 100,
                HP = 100,
                AP = 1
            };

            bool changed = build.IncreaseMaxHp((min, max) => max);

            Assert.True(changed);
            Assert.Equal(124, build.MaxHP);
            Assert.Equal(124, build.HP);
            Assert.Equal(0, build.AP);
        }

        [Fact]
        public void IncreaseMaxMp_UsesJobAwareGrowthAndConsumesAp()
        {
            CharacterBuild build = new CharacterBuild
            {
                Job = 200,
                MaxMP = 100,
                MP = 100,
                AP = 1
            };

            bool changed = build.IncreaseMaxMp((min, max) => min);

            Assert.True(changed);
            Assert.Equal(118, build.MaxMP);
            Assert.Equal(118, build.MP);
            Assert.Equal(0, build.AP);
        }

        [Fact]
        public void IncreaseMaxHp_DoesNotChangeWhenAtCap()
        {
            CharacterBuild build = new CharacterBuild
            {
                Job = 100,
                MaxHP = CharacterBuild.MaxHpMpStat,
                HP = CharacterBuild.MaxHpMpStat,
                AP = 1
            };

            bool changed = build.IncreaseMaxHp((min, max) => max);

            Assert.False(changed);
            Assert.Equal(CharacterBuild.MaxHpMpStat, build.MaxHP);
            Assert.Equal(1, build.AP);
        }
    }
}
