using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;

namespace UnitTest_MapSimulator
{
    public class MonsterCarnivalFieldTests
    {
        [Fact]
        public void Load_ParsesMonsterCarnivalDefinition_FromAdditionalNonInfoProps()
        {
            MapInfo mapInfo = new MapInfo
            {
                id = 980000201,
                fieldType = FieldType.FIELDTYPE_MONSTERCARNIVAL_S2
            };

            WzSubProperty carnival = new WzSubProperty("monsterCarnival");
            carnival["timeDefault"] = new WzIntProperty("timeDefault", 610);
            carnival["timeExpand"] = new WzIntProperty("timeExpand", 120);
            carnival["timeFinish"] = new WzIntProperty("timeFinish", 12);
            carnival["deathCP"] = new WzIntProperty("deathCP", 10);
            carnival["rewardMapWin"] = new WzIntProperty("rewardMapWin", 980000203);
            carnival["rewardMapLose"] = new WzIntProperty("rewardMapLose", 980000204);

            WzSubProperty mobs = new WzSubProperty("mob");
            WzSubProperty mob0 = new WzSubProperty("0");
            mob0["id"] = new WzIntProperty("id", 9300127);
            mob0["spendCP"] = new WzIntProperty("spendCP", 7);
            mobs["0"] = mob0;
            carnival["mob"] = mobs;

            WzSubProperty skills = new WzSubProperty("skill");
            skills["0"] = new WzIntProperty("0", 1);
            carnival["skill"] = skills;

            WzSubProperty guardians = new WzSubProperty("guardian");
            guardians["0"] = new WzIntProperty("0", 2);
            carnival["guardian"] = guardians;

            mapInfo.additionalNonInfoProps.Add(carnival);

            MonsterCarnivalFieldDefinition definition = MonsterCarnivalFieldDataLoader.Load(mapInfo);

            Assert.NotNull(definition);
            Assert.Equal(980000201, definition.MapId);
            Assert.Equal(610, definition.DefaultTimeSeconds);
            Assert.Equal(120, definition.ExpandTimeSeconds);
            Assert.Equal(12, definition.FinishTimeSeconds);
            Assert.Equal(10, definition.DeathCp);
            Assert.Equal(980000203, definition.RewardMapWin);
            Assert.Equal(980000204, definition.RewardMapLose);
            Assert.Single(definition.MobEntries);
            Assert.Equal(9300127, definition.MobEntries[0].Id);
            Assert.Equal(7, definition.MobEntries[0].Cost);
            Assert.Single(definition.SkillEntries);
            Assert.Single(definition.GuardianEntries);
        }

        [Fact]
        public void Configure_UsesFieldTypeEvenWithoutMonsterCarnivalProperty()
        {
            MapInfo mapInfo = new MapInfo
            {
                id = 980031100,
                fieldType = FieldType.FIELDTYPE_MONSTERCARNIVALREVIVE
            };

            MonsterCarnivalField field = new MonsterCarnivalField();
            field.Configure(mapInfo);

            Assert.True(field.IsVisible);
            Assert.NotNull(field.Definition);
            Assert.Equal(980031100, field.Definition.MapId);
        }

        [Fact]
        public void OnEnter_AssignsLocalAndEnemyCp_ByTeam()
        {
            MapInfo mapInfo = new MapInfo
            {
                id = 980000201,
                fieldType = FieldType.FIELDTYPE_MONSTERCARNIVAL_S2
            };

            WzSubProperty carnival = new WzSubProperty("monsterCarnival");
            carnival["mob"] = new WzSubProperty("mob");
            carnival["skill"] = new WzSubProperty("skill");
            carnival["guardian"] = new WzSubProperty("guardian");
            mapInfo.additionalNonInfoProps.Add(carnival);

            MonsterCarnivalField field = new MonsterCarnivalField();
            field.Configure(mapInfo);
            field.OnEnter(MonsterCarnivalTeam.Team1, personalCp: 12, personalTotalCp: 50, myTeamCp: 80, myTeamTotalCp: 200, enemyTeamCp: 64, enemyTeamTotalCp: 190);

            Assert.True(field.IsEntered);
            Assert.Equal(MonsterCarnivalTeam.Team1, field.LocalTeam);
            Assert.Equal(12, field.PersonalCp);
            Assert.Equal(50, field.PersonalTotalCp);
            Assert.Equal(64, field.Team0.CurrentCp);
            Assert.Equal(190, field.Team0.TotalCp);
            Assert.Equal(80, field.Team1.CurrentCp);
            Assert.Equal(200, field.Team1.TotalCp);
        }

        [Fact]
        public void TryRequestActiveEntry_IncrementsMobSpellCount()
        {
            MapInfo mapInfo = new MapInfo
            {
                id = 980000201,
                fieldType = FieldType.FIELDTYPE_MONSTERCARNIVAL_S2
            };

            WzSubProperty carnival = new WzSubProperty("monsterCarnival");
            WzSubProperty mobs = new WzSubProperty("mob");
            WzSubProperty mob0 = new WzSubProperty("0");
            mob0["id"] = new WzIntProperty("id", 9300127);
            mob0["spendCP"] = new WzIntProperty("spendCP", 7);
            mobs["0"] = mob0;
            carnival["mob"] = mobs;
            carnival["skill"] = new WzSubProperty("skill");
            carnival["guardian"] = new WzSubProperty("guardian");
            mapInfo.additionalNonInfoProps.Add(carnival);

            MonsterCarnivalField field = new MonsterCarnivalField();
            field.Configure(mapInfo);

            bool requested = field.TryRequestActiveEntry(0, "Summon it", 1234, out _);

            Assert.True(requested);
            Assert.Equal(1, field.MobSpellCounts[9300127]);
        }
    }
}
