using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator
{
    public class LoginCharacterRosterManagerTests
    {
        [Fact]
        public void CanRequestSelection_Fails_WhenNoCharacterIsSelected()
        {
            LoginCharacterRosterManager roster = new();
            LoginRuntimeManager runtime = new();
            runtime.Initialize(1000);
            runtime.TryDispatchPacket(LoginPacketType.SelectWorldResult, 1000, out _);
            runtime.Update(2000);

            bool canRequest = roster.CanRequestSelection(runtime, out string message);

            Assert.False(canRequest);
            Assert.Equal("Character roster is empty.", message);
        }

        [Fact]
        public void CanRequestSelection_Fails_WhenCharacterSelectIsNotReady()
        {
            LoginCharacterRosterManager roster = new();
            roster.SetEntries(new[]
            {
                new LoginCharacterRosterEntry(CreateBuild("Alpha"), 100000000)
            });

            LoginRuntimeManager runtime = new();
            runtime.Initialize(1000);
            runtime.ForceStep(LoginStep.CharacterSelect, "test");

            bool canRequest = roster.CanRequestSelection(runtime, out string message);

            Assert.False(canRequest);
            Assert.Equal("Character selection is waiting for SelectWorldResult.", message);
        }

        [Fact]
        public void CanRequestSelection_Succeeds_WhenRosterEntryAndRuntimeAreReady()
        {
            LoginCharacterRosterManager roster = new();
            roster.SetEntries(new[]
            {
                new LoginCharacterRosterEntry(CreateBuild("Alpha"), 100000000),
                new LoginCharacterRosterEntry(CreateBuild("Beta"), 100000000)
            });
            Assert.True(roster.Select(1));

            LoginRuntimeManager runtime = new();
            runtime.Initialize(1000);
            runtime.TryDispatchPacket(LoginPacketType.SelectWorldResult, 1000, out _);
            runtime.Update(2000);

            bool canRequest = roster.CanRequestSelection(runtime, out string message);

            Assert.True(canRequest);
            Assert.Equal("Ready to enter with Beta.", message);
        }

        [Fact]
        public void DeleteSelected_RemovesEntry_AndMovesSelection()
        {
            LoginCharacterRosterManager roster = new();
            roster.SetEntries(new[]
            {
                new LoginCharacterRosterEntry(CreateBuild("Alpha"), 100000000, canDelete: false),
                new LoginCharacterRosterEntry(CreateBuild("Beta"), 100000000),
                new LoginCharacterRosterEntry(CreateBuild("Gamma"), 100000000)
            });
            Assert.True(roster.Select(1));

            bool deleted = roster.DeleteSelected(out LoginCharacterRosterEntry deletedEntry);

            Assert.True(deleted);
            Assert.Equal("Beta", deletedEntry.Build.Name);
            Assert.Equal(2, roster.Entries.Count);
            Assert.Equal("Gamma", roster.SelectedEntry.Build.Name);
        }

        private static CharacterBuild CreateBuild(string name)
        {
            return new CharacterBuild
            {
                Name = name,
                JobName = "Beginner",
                Level = 10
            };
        }
    }
}
