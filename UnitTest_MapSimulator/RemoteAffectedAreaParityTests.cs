using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework.Graphics;

namespace UnitTest_MapSimulator
{
    public sealed class RemoteAffectedAreaParityTests
    {
        private static readonly Type AreaBuffItemMetadataResolverType = Type.GetType(
            "HaCreator.MapSimulator.Pools.AreaBuffItemMetadataResolver, HaCreator")
            ?? throw new InvalidOperationException("AreaBuffItemMetadataResolver type was not found.");

        private static readonly MethodInfo ResolveDurationMsMethod = AreaBuffItemMetadataResolverType.GetMethod(
            "ResolveDurationMs",
            BindingFlags.Static | BindingFlags.Public)
            ?? throw new InvalidOperationException("AreaBuffItemMetadataResolver.ResolveDurationMs was not found.");

        private static readonly MethodInfo ResolveIncomingDamageAfterActiveBuffsMethod = typeof(SkillManager).GetMethod(
            "ResolveIncomingDamageAfterActiveBuffs",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SkillManager.ResolveIncomingDamageAfterActiveBuffs was not found.");

        [Fact]
        public void ResolveDurationMs_FollowsLinkedNonItemPathTimeMetadata()
        {
            WzSubProperty itemProperty = new("05010079");
            WzSubProperty info = new("info");
            info.AddProperty(new WzStringProperty("path", "Map/MapHelper.img/weather/customFog"));
            itemProperty.AddProperty(info);

            WzSubProperty linkedProperty = new("customFog");
            linkedProperty.AddProperty(new WzIntProperty("time", 45));

            int durationMs = InvokeResolveAreaBuffDurationMs(
                itemProperty,
                linkedPropertyLoader: path => string.Equals(path, "Map/MapHelper.img/weather/customFog", StringComparison.Ordinal)
                    ? linkedProperty
                    : null);

            Assert.Equal(45000, durationMs);
        }

        [Fact]
        public void ResolveDurationMs_UsesMultiUnitDescriptionWhenWzTimeIsMissing()
        {
            int durationMs = InvokeResolveAreaBuffDurationMs(
                itemProperty: null,
                itemDescription: "Summons a fog for 1 min 30 sec.");

            Assert.Equal(90000, durationMs);
        }

        [Fact]
        public void ResolveIncomingDamageAfterActiveBuffs_RedirectsDamageToMpForProjectedAreaBuffs()
        {
            SkillManager manager = CreateManager(maxMp: 100, currentMp: 100);
            manager.ApplyOrRefreshExternalAreaSupportBuff(
                areaObjectId: 328,
                sourceSkill: new SkillData
                {
                    SkillId = 2001002,
                    Name = "Magic Guard",
                    RedirectsDamageToMp = true
                },
                supportSkills: Array.Empty<SkillData>(),
                levelData: new SkillLevelData
                {
                    Level = 1,
                    Time = 10,
                    X = 80
                },
                currentTime: 100,
                durationMs: 5000);

            int hpDamage = InvokeResolveIncomingDamageAfterActiveBuffs(manager, damage: 100, currentTime: 150);

            Assert.Equal(20, hpDamage);
            Assert.Equal(20, manager.GetPlayerBuildForTests().MP);
        }

        [Fact]
        public void ResolveIncomingDamageAfterActiveBuffs_FallsBackToHpWhenMpIsInsufficient()
        {
            SkillManager manager = CreateManager(maxMp: 50, currentMp: 50);
            manager.ApplyOrRefreshExternalAreaSupportBuff(
                areaObjectId: 329,
                sourceSkill: new SkillData
                {
                    SkillId = 2001002,
                    Name = "Magic Guard",
                    RedirectsDamageToMp = true
                },
                supportSkills: Array.Empty<SkillData>(),
                levelData: new SkillLevelData
                {
                    Level = 1,
                    Time = 10,
                    X = 80
                },
                currentTime: 100,
                durationMs: 5000);

            int hpDamage = InvokeResolveIncomingDamageAfterActiveBuffs(manager, damage: 100, currentTime: 150);

            Assert.Equal(50, hpDamage);
            Assert.Equal(0, manager.GetPlayerBuildForTests().MP);
        }

        private static SkillManager CreateManager(int maxMp, int currentMp)
        {
            SkillLoader loader = (SkillLoader)RuntimeHelpers.GetUninitializedObject(typeof(SkillLoader));
            CharacterBuild build = new()
            {
                MaxHP = 500,
                HP = 500,
                MaxMP = maxMp,
                MP = currentMp
            };

            PlayerCharacter player = new((GraphicsDevice)null!, (TexturePool)null!, build);
            return new SkillManager(loader, player);
        }

        private static int InvokeResolveIncomingDamageAfterActiveBuffs(SkillManager manager, int damage, int currentTime)
        {
            return (int)(ResolveIncomingDamageAfterActiveBuffsMethod.Invoke(manager, new object[] { damage, currentTime })
                ?? throw new InvalidOperationException("Expected damage result."));
        }

        private static int InvokeResolveAreaBuffDurationMs(
            WzSubProperty itemProperty,
            string itemDescription = null,
            Func<string, WzSubProperty> linkedItemPropertyLoader = null,
            Func<string, string> linkedItemDescriptionLoader = null,
            Func<string, MapleLib.WzLib.WzImageProperty> linkedPropertyLoader = null)
        {
            return (int)(ResolveDurationMsMethod.Invoke(null, new object[]
            {
                itemProperty,
                itemDescription,
                linkedItemPropertyLoader,
                linkedItemDescriptionLoader,
                linkedPropertyLoader
            }) ?? throw new InvalidOperationException("Expected duration result."));
        }
    }

    internal static class RemoteAffectedAreaParityTestExtensions
    {
        public static CharacterBuild GetPlayerBuildForTests(this SkillManager manager)
        {
            FieldInfo playerField = typeof(SkillManager).GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("SkillManager._player was not found.");
            PlayerCharacter player = (PlayerCharacter)(playerField.GetValue(manager)
                ?? throw new InvalidOperationException("SkillManager._player was null."));
            return player.Build ?? throw new InvalidOperationException("Expected player build.");
        }
    }
}
