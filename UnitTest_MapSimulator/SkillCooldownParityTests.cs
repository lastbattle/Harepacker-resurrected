using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework.Graphics;

namespace UnitTest_MapSimulator
{
    public sealed class SkillCooldownParityTests
    {
        private const int TestSkillId = 1121001;
        private const int AuthoredCooldownMs = 5000;

        private static readonly MethodInfo ApplySkillCooldownMethod = typeof(SkillManager).GetMethod(
            "ApplySkillCooldown",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SkillManager.ApplySkillCooldown was not found.");

        private static readonly MethodInfo UpdateCooldownNotificationsMethod = typeof(SkillManager).GetMethod(
            "UpdateCooldownNotifications",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SkillManager.UpdateCooldownNotifications was not found.");

        private static readonly FieldInfo AvailableSkillsField = typeof(SkillManager).GetField(
            "_availableSkills",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SkillManager._availableSkills was not found.");

        [Fact]
        public void PacketOwnedRefresh_PreservesOriginalCooldownStartTick()
        {
            SkillManager manager = CreateManager(out SkillData skill);

            ApplyAuthoredCooldown(manager, skill, currentTime: 100);

            manager.SetServerCooldownRemaining(TestSkillId, remainingMs: 3000, currentTime: 250);

            Assert.True(manager.TryGetCooldownStartTime(TestSkillId, out int cooldownStartTime));
            Assert.Equal(100, cooldownStartTime);
            Assert.Equal(3000, manager.GetCooldownRemaining(TestSkillId, 250));
            Assert.Equal(3150, manager.GetCooldownDuration(TestSkillId, 250));
        }

        [Fact]
        public void PacketOwnedClear_TearsDownSharedCooldownStateWithoutAuthoredFallback()
        {
            SkillManager manager = CreateManager(out SkillData skill);
            int completedCount = 0;
            manager.OnSkillCooldownCompleted = (_, _) => completedCount++;

            ApplyAuthoredCooldown(manager, skill, currentTime: 100);
            manager.SetServerCooldownRemaining(TestSkillId, remainingMs: 3000, currentTime: 500);

            manager.ClearServerCooldown(TestSkillId, currentTime: 700);

            Assert.False(manager.TryGetCooldownStartTime(TestSkillId, out _));
            Assert.False(manager.IsOnCooldown(TestSkillId, 700));
            Assert.Equal(0, manager.GetCooldownRemaining(TestSkillId, 700));
            Assert.Equal(0, manager.GetCooldownDuration(TestSkillId, 700));
            Assert.Equal(1, completedCount);
        }

        [Fact]
        public void RepeatedServerRefreshes_DoNotRefireCooldownStartedNotice()
        {
            SkillManager manager = CreateManager(out _);
            List<(int DurationMs, int CurrentTime)> startedEvents = new();
            manager.OnSkillCooldownStarted = (_, durationMs, currentTime) => startedEvents.Add((durationMs, currentTime));

            manager.SetServerCooldownRemaining(TestSkillId, remainingMs: 3000, currentTime: 100);
            manager.SetServerCooldownRemaining(TestSkillId, remainingMs: 2400, currentTime: 350);
            manager.SetServerCooldownRemaining(TestSkillId, remainingMs: 1200, currentTime: 900);

            Assert.Single(startedEvents);
            Assert.Equal((3000, 100), startedEvents[0]);
        }

        [Fact]
        public void NaturallyExpiredServerOwnedCooldown_CompletesOnceWithoutRegressingToAuthoredTimer()
        {
            SkillManager manager = CreateManager(out SkillData skill);
            List<int> completedTimes = new();
            manager.OnSkillCooldownCompleted = (_, currentTime) => completedTimes.Add(currentTime);

            ApplyAuthoredCooldown(manager, skill, currentTime: 100);
            manager.SetServerCooldownRemaining(TestSkillId, remainingMs: 1000, currentTime: 500);

            Assert.Equal(0, manager.GetCooldownRemaining(TestSkillId, 1500));
            Assert.Equal(0, manager.GetCooldownDuration(TestSkillId, 1500));

            UpdateCooldownNotifications(manager, 1500);
            UpdateCooldownNotifications(manager, 1600);

            Assert.Equal(new[] { 1500 }, completedTimes);
            Assert.False(manager.TryGetCooldownStartTime(TestSkillId, out _));
            Assert.False(manager.IsOnCooldown(TestSkillId, 1600));
            Assert.Equal(0, manager.GetCooldownRemaining(TestSkillId, 1600));
            Assert.Equal(0, manager.GetCooldownDuration(TestSkillId, 1600));
        }

        private static SkillManager CreateManager(out SkillData skill)
        {
            SkillLoader loader = (SkillLoader)RuntimeHelpers.GetUninitializedObject(typeof(SkillLoader));
            PlayerCharacter player = new((GraphicsDevice)null!, (TexturePool)null!, build: null);
            SkillManager manager = new(loader, player);

            skill = new SkillData
            {
                SkillId = TestSkillId,
                Name = "Brandish",
                Levels = new Dictionary<int, SkillLevelData>
                {
                    [1] = new SkillLevelData
                    {
                        Level = 1,
                        Cooldown = AuthoredCooldownMs
                    }
                }
            };

            GetAvailableSkills(manager).Add(skill);
            manager.SetSkillLevel(TestSkillId, 1);
            return manager;
        }

        private static List<SkillData> GetAvailableSkills(SkillManager manager)
        {
            return (List<SkillData>)(AvailableSkillsField.GetValue(manager)
                ?? throw new InvalidOperationException("SkillManager._availableSkills was null."));
        }

        private static void ApplyAuthoredCooldown(SkillManager manager, SkillData skill, int currentTime)
        {
            ApplySkillCooldownMethod.Invoke(manager, new object[]
            {
                skill,
                skill.GetLevel(1) ?? throw new InvalidOperationException("Expected level 1 skill data."),
                currentTime
            });
        }

        private static void UpdateCooldownNotifications(SkillManager manager, int currentTime)
        {
            UpdateCooldownNotificationsMethod.Invoke(manager, new object[] { currentTime });
        }
    }
}
