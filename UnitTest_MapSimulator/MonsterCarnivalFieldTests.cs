using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzStructure.Data;

namespace UnitTest_MapSimulator;

public sealed class MonsterCarnivalFieldTests
{
    [Fact]
    public void TrySetMobSpellCount_ReconcilesSummonedMobStates()
    {
        MonsterCarnivalField field = CreateConfiguredField();

        bool success = field.TrySetMobSpellCount(0, 2, out string message);

        Assert.True(success, message);
        Assert.Equal(2, field.SummonedMobs.Count);
        Assert.All(field.SummonedMobs, mob => Assert.Equal(100100, mob.Entry.Id));
    }

    [Fact]
    public void TryProcessMobDefeat_AwardsCpAndSpawnsReviveChain()
    {
        MonsterCarnivalField field = CreateConfiguredField();
        field.OnEnter(MonsterCarnivalTeam.Team0, 30, 30, 30, 30, 10, 10);
        field.OnRequestResult((byte)MonsterCarnivalTab.Mob, 0, null, 1000);

        bool success = field.TryProcessMobDefeat(0, MonsterCarnivalTeam.Team0, 1100, out string message);

        Assert.True(success, message);
        Assert.Equal(27, field.PersonalCp);
        Assert.Equal(32, field.PersonalTotalCp);
        Assert.Equal(27, field.Team0.CurrentCp);
        Assert.Equal(32, field.Team0.TotalCp);
        Assert.Equal(2, field.SummonedMobs.Count);
        Assert.DoesNotContain(field.SummonedMobs, mob => mob.Entry.Id == 100100);
        Assert.Contains(field.SummonedMobs, mob => mob.Entry.Id == 100200);
        Assert.Contains(field.SummonedMobs, mob => mob.Entry.Id == 100300);
        Assert.Contains("Revived into", message);
    }

    [Fact]
    public void TryApplyGuardianReactorHit_DestroyedPlacementFreesOccupiedSlot()
    {
        MonsterCarnivalField field = CreateConfiguredField();
        field.OnEnter(MonsterCarnivalTeam.Team0, 30, 30, 30, 30, 10, 10);
        field.OnRequestResult((byte)MonsterCarnivalTab.Guardian, 0, null, 1000);

        bool hitSuccess = field.TryApplyGuardianReactorHit(0, destroyPlacement: false, 1100, out string hitMessage);
        bool destroySuccess = field.TryApplyGuardianReactorHit(0, destroyPlacement: true, 1200, out string destroyMessage);
        bool reRequestSuccess = field.TryRequestActiveEntry(0, null, 1300, out string requestMessage);

        Assert.True(hitSuccess, hitMessage);
        Assert.True(destroySuccess, destroyMessage);
        Assert.True(reRequestSuccess, requestMessage);
        Assert.Single(field.GuardianPlacements);
        Assert.Equal(0, field.GuardianPlacements[0].ReactorHitCount);
    }

    [Fact]
    public void OnRequestFailure_UsesClientStringPoolReason()
    {
        MonsterCarnivalField field = CreateConfiguredField();

        field.OnRequestFailure(5, 1000);

        Assert.Equal("A guardian is already active in that slot. [StringPool 0x101F]", field.CurrentStatusMessage);
    }

    [Fact]
    public void OnShowGameResult_UsesClientStringPoolResult()
    {
        MonsterCarnivalField field = CreateConfiguredField();

        field.OnShowGameResult(8, 1000);

        Assert.Equal("Monster Carnival round ended in victory. [StringPool 0x1020]", field.CurrentStatusMessage);
    }

    [Fact]
    public void OnProcessForDeath_UsesClientStringPoolIdsForDeathAndTeam()
    {
        MonsterCarnivalField field = CreateConfiguredField();
        field.OnEnter(MonsterCarnivalTeam.Team0, 30, 30, 30, 30, 10, 10);

        field.OnProcessForDeath(MonsterCarnivalTeam.Team1, "BlueMage", 2, 1000);

        Assert.Equal("BlueMage of Blue was defeated. 2 revive(s) remaining. [StringPool 0x1019, 0x1018] 3 CP was removed from Blue.", field.CurrentStatusMessage);
        Assert.Equal(7, field.Team1.CurrentCp);
    }

    [Fact]
    public void OnShowMemberOutMessage_UsesClientStringPoolIdsForMessageAndTeam()
    {
        MonsterCarnivalField field = CreateConfiguredField();

        field.OnShowMemberOutMessage(6, MonsterCarnivalTeam.Team0, "CarnivalHero", 1000);

        Assert.Equal("Red and CarnivalHero changed teams. [StringPool 0x102A, 0x1017]", field.CurrentStatusMessage);
    }

    private static MonsterCarnivalField CreateConfiguredField()
    {
        MonsterCarnivalField field = new();
        field.Configure(new MonsterCarnivalFieldDefinition
        {
            FieldType = FieldType.FIELDTYPE_MONSTERCARNIVAL_S2,
            DeathCp = 3,
            MobGenMax = 10,
            GuardianGenMax = 2,
            ReactorRed = 9980000,
            ReactorBlue = 9980001,
            MobSpawnPositions =
            [
                new MonsterCarnivalSpawnPoint(0, 10, 20, 1, 20),
                new MonsterCarnivalSpawnPoint(1, 30, 40, 1, 40)
            ],
            GuardianSpawnPositions =
            [
                new MonsterCarnivalGuardianSpawnPoint(0, 50, 60, 1)
            ],
            MobEntries =
            [
                new MonsterCarnivalEntry(
                    MonsterCarnivalTab.Mob,
                    0,
                    100100,
                    5,
                    "Brown Tino",
                    "Summon Brown Tino.",
                    rewardCp: 2,
                    reviveMobIds: [100200, 100300]),
                new MonsterCarnivalEntry(
                    MonsterCarnivalTab.Mob,
                    1,
                    100200,
                    0,
                    "Green Tino",
                    "Revived Green Tino."),
                new MonsterCarnivalEntry(
                    MonsterCarnivalTab.Mob,
                    2,
                    100300,
                    0,
                    "Blue Tino",
                    "Revived Blue Tino.")
            ],
            GuardianEntries =
            [
                new MonsterCarnivalEntry(
                    MonsterCarnivalTab.Guardian,
                    0,
                    200100,
                    4,
                    "Poison Golem",
                    "Summon a guardian.")
            ]
        });

        field.TrySetActiveTab("guardian", out _);
        return field;
    }
}
