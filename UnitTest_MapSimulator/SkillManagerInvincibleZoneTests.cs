using System.Collections.Generic;
using System.Reflection;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class SkillManagerInvincibleZoneTests
    {
        [Fact]
        public void SmokeBombBranch_CreatesInvincibleZoneWithoutGenericBuffState()
        {
            SkillData skill = CreateSmokeBombSkill();
            PlayerCharacter player = new PlayerCharacter(device: null, texturePool: null, build: new CharacterBuild());
            player.SetPosition(100f, 100f);

            SkillManager manager = new SkillManager(
                new SkillLoader(skillWz: null, device: null, texturePool: null),
                player);

            bool handled = InvokeClientSkillBranch(manager, skill, currentTime: 1000);

            Assert.True(handled);
            Assert.True(manager.IsPlayerProtectedByClientSkillZone(1000));
            Assert.False(manager.HasBuff(skill.SkillId));

            player.SetPosition(400f, 100f);
            Assert.False(manager.IsPlayerProtectedByClientSkillZone(1000));

            player.SetPosition(100f, 100f);
            Assert.False(manager.IsPlayerProtectedByClientSkillZone(6000));
        }

        private static SkillData CreateSmokeBombSkill()
        {
            return new SkillData
            {
                SkillId = 4221006,
                Name = "Smoke Bomb",
                MaxLevel = 1,
                ActionName = "smokeshell",
                ZoneType = "invincible",
                IsMassSpell = true,
                Levels = new Dictionary<int, SkillLevelData>
                {
                    [1] = new SkillLevelData
                    {
                        Level = 1,
                        Time = 5,
                        RangeL = 250,
                        RangeR = 250,
                        RangeTop = -150,
                        RangeBottom = 150
                    }
                }
            };
        }

        private static bool InvokeClientSkillBranch(SkillManager manager, SkillData skill, int currentTime)
        {
            MethodInfo method = typeof(SkillManager).GetMethod("TryExecuteClientSkillBranch", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (bool)method.Invoke(manager, new object[] { skill, 1, currentTime })!;
        }
    }
}
