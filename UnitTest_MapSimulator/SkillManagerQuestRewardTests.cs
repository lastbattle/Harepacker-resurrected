using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;

namespace UnitTest_MapSimulator;

public class SkillManagerQuestRewardTests
{
    [Fact]
    public void SetSkillMasterLevel_StoresAndClearsMasterLevel()
    {
        SkillManager manager = CreateSkillManager();

        manager.SetSkillMasterLevel(2321003, 15);
        Assert.Equal(15, manager.GetSkillMasterLevel(2321003));

        manager.SetSkillMasterLevel(2321003, 0);
        Assert.Equal(0, manager.GetSkillMasterLevel(2321003));
    }

    [Fact]
    public void RemovingSkillLevel_ClearsStoredMasterLevel()
    {
        SkillManager manager = CreateSkillManager();

        manager.SetSkillLevel(2321003, 1);
        manager.SetSkillMasterLevel(2321003, 15);

        manager.SetSkillLevel(2321003, 0);

        Assert.Equal(0, manager.GetSkillLevel(2321003));
        Assert.Equal(0, manager.GetSkillMasterLevel(2321003));
    }

    private static SkillManager CreateSkillManager()
    {
        return new SkillManager(
            new SkillLoader(null, null, null),
            new PlayerCharacter(new CharacterBuild()));
    }
}
