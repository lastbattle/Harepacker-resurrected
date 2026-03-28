using System.Collections.Generic;
using HaCreator.MapSimulator.Companions;

namespace UnitTest_MapSimulator;

public sealed class PetAutoSpeechParityTests
{
    [Fact]
    public void TryTriggerAutoSpeechEvent_RequiresAutoSpeakingSkill()
    {
        PetRuntime pet = CreatePet(slotIndex: 0, itemId: 5000048);

        Assert.False(pet.TryTriggerAutoSpeechEvent(PetAutoSpeechEvent.LevelUp, currentTime: 1000));

        Assert.True(pet.AddSkillMask(PetRuntime.AutoSpeakingSkillMask));
        Assert.True(pet.TryTriggerAutoSpeechEvent(PetAutoSpeechEvent.LevelUp, currentTime: 1000));
        Assert.Equal("Level up!", pet.ActiveSpeechText);
    }

    [Fact]
    public void TryGrantSkillMask_TargetsCompatiblePet()
    {
        PetRuntime incompatiblePet = CreatePet(slotIndex: 0, itemId: 5000000);
        PetRuntime compatiblePet = CreatePet(slotIndex: 1, itemId: 5000048);

        bool granted = PetController.TryGrantSkillMask(
            new[] { incompatiblePet, compatiblePet },
            new[] { 5000048 },
            recallLimit: null,
            skillMask: PetRuntime.AutoSpeakingSkillMask,
            out int slotIndex);

        Assert.True(granted);
        Assert.Equal(1, slotIndex);
        Assert.False(incompatiblePet.HasSkillMask(PetRuntime.AutoSpeakingSkillMask));
        Assert.True(compatiblePet.HasSkillMask(PetRuntime.AutoSpeakingSkillMask));
    }

    [Fact]
    public void TryGrantSkillMask_RespectsRecallLimit()
    {
        PetRuntime firstPet = CreatePet(slotIndex: 0, itemId: 5000048);
        PetRuntime secondPet = CreatePet(slotIndex: 1, itemId: 5000049);

        bool granted = PetController.TryGrantSkillMask(
            new[] { firstPet, secondPet },
            new[] { 5000048, 5000049 },
            recallLimit: 1,
            skillMask: PetRuntime.AutoSpeakingSkillMask,
            out int slotIndex);

        Assert.False(granted);
        Assert.Equal(-1, slotIndex);
        Assert.False(firstPet.HasSkillMask(PetRuntime.AutoSpeakingSkillMask));
        Assert.False(secondPet.HasSkillMask(PetRuntime.AutoSpeakingSkillMask));
    }

    private static PetRuntime CreatePet(int slotIndex, int itemId)
    {
        return new PetRuntime(
            runtimeId: slotIndex + 1,
            slotIndex: slotIndex,
            definition: new PetDefinition
            {
                ItemId = itemId,
                Name = $"Pet {itemId}",
                EventSpeechLines = new Dictionary<PetAutoSpeechEvent, string[]>
                {
                    [PetAutoSpeechEvent.LevelUp] = new[] { "Level up!" },
                    [PetAutoSpeechEvent.Rest] = new[] { "Resting." }
                }
            });
    }
}
