using System.Collections.Generic;
using HaCreator.MapSimulator.Character.Skills;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class StatusBarBuffTemporaryStatParityTests
    {
        [Fact]
        public void ResolveStatusBarBuffEntryForParity_VehicleTransformProfile_UsesTransformFamilyOwner()
        {
            SkillLevelData levelData = CreateVehicleTransformLevelData();
            SkillData skillData = new()
            {
                SkillId = 5221006,
                Name = "Battleship",
                Description = "Transform into a battleship and gain ATT and DEF."
            };

            StatusBarBuffEntry entry = SkillManager.ResolveStatusBarBuffEntryForParity(
                skillData,
                levelData,
                skillId: 5221006,
                startTime: 0,
                durationMs: 30000,
                currentTime: 0);

            Assert.Equal("Transform", entry.FamilyDisplayName);
            Assert.Equal("united/buff", entry.IconKey);
            Assert.Contains("Transform", entry.TemporaryStatLabels);
        }

        [Fact]
        public void ResolveStatusBarBuffTooltipPresentationForParity_VehicleTransformProfile_PrefersTransformFamily()
        {
            SkillLevelData levelData = CreateVehicleTransformLevelData();

            (string familyDisplayName, IReadOnlyList<string> temporaryStatDisplayNames) =
                SkillManager.ResolveStatusBarBuffTooltipPresentationForParity(
                    levelData,
                    "PAD",
                    "PDD",
                    "MDD",
                    "MaxHP",
                    "MaxMP",
                    "Transform");

            Assert.Equal("Transform", familyDisplayName);
            Assert.Contains("Physical Attack", temporaryStatDisplayNames);
            Assert.Contains("Transform", temporaryStatDisplayNames);
        }

        [Fact]
        public void ResolveStatusBarBuffTooltipPresentationForParity_DirectStatProfileWithoutTransform_StaysOnDirectFamily()
        {
            SkillLevelData levelData = CreateVehicleTransformLevelData();

            (string familyDisplayName, IReadOnlyList<string> _) =
                SkillManager.ResolveStatusBarBuffTooltipPresentationForParity(
                    levelData,
                    "PAD",
                    "PDD",
                    "MDD",
                    "MaxHP",
                    "MaxMP");

            Assert.Equal("Physical Attack", familyDisplayName);
        }

        [Fact]
        public void ResolveStatusBarBuffEntryForParity_VehicleTransformHpFirstProfile_UsesTransformFamilyOwner()
        {
            SkillLevelData levelData = CreateVehicleTransformHpFirstLevelData();
            SkillData skillData = new()
            {
                SkillId = 35001002,
                Name = "Mechanic Dash",
                Description = "Transform into a siege unit and gain HP and DEF."
            };

            StatusBarBuffEntry entry = SkillManager.ResolveStatusBarBuffEntryForParity(
                skillData,
                levelData,
                skillId: 35001002,
                startTime: 0,
                durationMs: 30000,
                currentTime: 0);

            Assert.Equal("Transform", entry.FamilyDisplayName);
            Assert.Equal("united/buff", entry.IconKey);
            Assert.Contains("Transform", entry.TemporaryStatLabels);
        }

        [Fact]
        public void ResolveStatusBarBuffTooltipPresentationForParity_VehicleTransformHpFirstProfile_PrefersTransformFamily()
        {
            SkillLevelData levelData = CreateVehicleTransformHpFirstLevelData();

            (string familyDisplayName, IReadOnlyList<string> temporaryStatDisplayNames) =
                SkillManager.ResolveStatusBarBuffTooltipPresentationForParity(
                    levelData,
                    "MaxHP",
                    "MaxMP",
                    "PAD",
                    "PDD",
                    "MDD",
                    "Transform");

            Assert.Equal("Transform", familyDisplayName);
            Assert.Contains("Max HP", temporaryStatDisplayNames);
            Assert.Contains("Transform", temporaryStatDisplayNames);
        }

        [Fact]
        public void ResolveStatusBarBuffEntryForParity_VehicleTransformSpeedJumpProfile_UsesTransformFamilyOwner()
        {
            SkillLevelData levelData = CreateVehicleTransformSpeedJumpLevelData();
            SkillData skillData = new()
            {
                SkillId = 5121003,
                Name = "Super Transformation",
                Description = "Transform and increase speed and jump while attacking."
            };

            StatusBarBuffEntry entry = SkillManager.ResolveStatusBarBuffEntryForParity(
                skillData,
                levelData,
                skillId: 5121003,
                startTime: 0,
                durationMs: 30000,
                currentTime: 0);

            Assert.Equal("Transform", entry.FamilyDisplayName);
            Assert.Equal("united/buff", entry.IconKey);
            Assert.Contains("Transform", entry.TemporaryStatLabels);
            Assert.Contains("Speed", entry.TemporaryStatLabels);
            Assert.Contains("Jump", entry.TemporaryStatLabels);
        }

        [Fact]
        public void ResolveStatusBarBuffTooltipPresentationForParity_VehicleTransformSpeedJumpProfile_PrefersTransformFamily()
        {
            SkillLevelData levelData = CreateVehicleTransformSpeedJumpLevelData();

            (string familyDisplayName, IReadOnlyList<string> temporaryStatDisplayNames) =
                SkillManager.ResolveStatusBarBuffTooltipPresentationForParity(
                    levelData,
                    "PAD",
                    "PDD",
                    "MDD",
                    "Speed",
                    "Jump",
                    "Transform");

            Assert.Equal("Transform", familyDisplayName);
            Assert.Contains("Speed", temporaryStatDisplayNames);
            Assert.Contains("Jump", temporaryStatDisplayNames);
            Assert.Contains("Transform", temporaryStatDisplayNames);
        }

        private static SkillLevelData CreateVehicleTransformLevelData()
        {
            return new SkillLevelData
            {
                EnhancedPAD = 6,
                EnhancedPDD = 430,
                EnhancedMDD = 430,
                EnhancedMaxHP = 450,
                EnhancedMaxMP = 450,
                AuthoredPropertyOrder = new List<string>
                {
                    "maxLevel",
                    "mpCon",
                    "time",
                    "epad",
                    "epdd",
                    "emdd",
                    "emhp",
                    "emmp",
                    "x",
                    "cooltime"
                }
            };
        }

        private static SkillLevelData CreateVehicleTransformHpFirstLevelData()
        {
            return new SkillLevelData
            {
                EnhancedMaxHP = 500,
                EnhancedMaxMP = 500,
                EnhancedPAD = 12,
                EnhancedPDD = 200,
                EnhancedMDD = 200,
                AuthoredPropertyOrder = new List<string>
                {
                    "maxLevel",
                    "mpCon",
                    "emhp",
                    "emmp",
                    "epad",
                    "epdd",
                    "emdd",
                    "prop"
                }
            };
        }

        private static SkillLevelData CreateVehicleTransformSpeedJumpLevelData()
        {
            return new SkillLevelData
            {
                EnhancedPAD = 24,
                EnhancedPDD = 225,
                EnhancedMDD = 225,
                Speed = 40,
                Jump = 20,
                AuthoredPropertyOrder = new List<string>
                {
                    "maxLevel",
                    "morph",
                    "mpCon",
                    "time",
                    "epad",
                    "epdd",
                    "emdd",
                    "speed",
                    "jump",
                    "cooltime"
                }
            };
        }
    }
}
