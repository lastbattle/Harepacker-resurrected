using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzStructure;

namespace UnitTest_MapSimulator;

public sealed class FieldRuleRuntimeParityTests
{
    [Fact]
    public void ZeroMoveLimitDoesNotActivateFieldRuleRuntime()
    {
        MapInfo mapInfo = new()
        {
            moveLimit = 0
        };

        FieldRuleRuntime runtime = new(mapInfo);

        Assert.False(runtime.IsActive);
        Assert.Empty(runtime.Reset(0));
    }

    [Fact]
    public void PositiveMoveLimitBlocksMovementSkillsAfterConfiguredUses()
    {
        MapInfo mapInfo = new()
        {
            moveLimit = 2
        };

        FieldRuleRuntime runtime = new(mapInfo);
        SkillData movementSkill = new()
        {
            SkillId = 1001001,
            Name = "Flash Jump",
            IsMovement = true
        };
        SkillData attackSkill = new()
        {
            SkillId = 1001002,
            Name = "Power Strike",
            IsMovement = false
        };

        IReadOnlyList<string> entryMessages = runtime.Reset(0);

        Assert.Contains("Movement skill limit active: 2 use(s) in this field.", entryMessages);
        Assert.True(runtime.CanUseSkill(movementSkill));
        Assert.Null(runtime.GetSkillRestrictionMessage(attackSkill));

        runtime.RegisterSuccessfulSkillUse(movementSkill);
        Assert.True(runtime.CanUseSkill(movementSkill));

        runtime.RegisterSuccessfulSkillUse(movementSkill);
        Assert.False(runtime.CanUseSkill(movementSkill));
        Assert.Equal("Movement skills can only be used 2 time(s) in this map.", runtime.GetSkillRestrictionMessage(movementSkill));
        Assert.True(runtime.CanUseSkill(attackSkill));
    }

    [Fact]
    public void OnFirstUserEnterNoticeIsSkippedAfterFirstVisit()
    {
        MapInfo mapInfo = new()
        {
            onUserEnter = "alwaysScript",
            onFirstUserEnter = "firstOnlyScript"
        };

        FieldRuleRuntime firstEntryRuntime = new(mapInfo, includeFirstUserEnterScript: true);
        FieldRuleRuntime repeatEntryRuntime = new(mapInfo, includeFirstUserEnterScript: false);

        IReadOnlyList<string> firstEntryMessages = firstEntryRuntime.Reset(0);
        IReadOnlyList<string> repeatEntryMessages = repeatEntryRuntime.Reset(0);

        Assert.Contains(firstEntryMessages, message => message.Contains("onUserEnter=alwaysScript"));
        Assert.Contains(firstEntryMessages, message => message.Contains("onFirstUserEnter=firstOnlyScript"));
        Assert.Contains(repeatEntryMessages, message => message.Contains("onUserEnter=alwaysScript"));
        Assert.DoesNotContain(repeatEntryMessages, message => message.Contains("onFirstUserEnter=firstOnlyScript"));
    }
}
