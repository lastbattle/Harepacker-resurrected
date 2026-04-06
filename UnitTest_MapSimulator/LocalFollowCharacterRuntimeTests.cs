using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator
{
    public sealed class LocalFollowCharacterRuntimeTests
    {
        [Fact]
        public void TrySendAttachedReleaseRequest_RecordsClientKeyInputTuple()
        {
            var runtime = new LocalFollowCharacterRuntime();
            LocalFollowUserSnapshot driver = CreateSnapshot(200, "Driver");

            runtime.ApplyServerAttach(driver, currentTime: 100);

            bool sent = runtime.TrySendAttachedReleaseRequest(
                currentTime: 1200,
                ResolveName,
                out string message);

            Assert.True(sent);
            Assert.Contains("outpacket 134 (0, 0, 1)", message);
            Assert.Contains("lastRequest=134(0,0,1)@1200", runtime.DescribeStatus(ResolveName));
            Assert.Equal(driver.CharacterId, runtime.AttachedDriverId);
        }

        [Fact]
        public void TrySendAttachedReleaseRequest_FailsWhenNoDriverIsAttached()
        {
            var runtime = new LocalFollowCharacterRuntime();

            bool sent = runtime.TrySendAttachedReleaseRequest(
                currentTime: 500,
                ResolveName,
                out string message);

            Assert.False(sent);
            Assert.Equal("No local follow driver is currently attached.", message);
        }

        [Fact]
        public void TrySendAttachedReleaseRequest_UsesSharedFollowThrottle()
        {
            var runtime = new LocalFollowCharacterRuntime();
            LocalFollowUserSnapshot local = CreateSnapshot(100, "Local");
            LocalFollowUserSnapshot firstDriver = CreateSnapshot(200, "FirstDriver");
            LocalFollowUserSnapshot attachedDriver = CreateSnapshot(201, "AttachedDriver");

            Assert.True(runtime.TrySendOutgoingRequest(local, firstDriver, 1000, autoRequest: false, keyInput: false, out _));
            runtime.ApplyServerAttach(attachedDriver, currentTime: 1100);

            bool sent = runtime.TrySendAttachedReleaseRequest(
                currentTime: 1500,
                ResolveName,
                out string message);

            Assert.False(sent);
            Assert.Equal("Local follow release request is throttled for another 500 ms.", message);
        }

        private static LocalFollowUserSnapshot CreateSnapshot(int id, string name)
        {
            return new LocalFollowUserSnapshot(
                id,
                name,
                Exists: true,
                IsAlive: true,
                IsImmovable: false,
                IsMounted: false,
                HasMorphTemplate: false,
                IsGhostAction: false,
                Position: Vector2.Zero,
                FacingRight: true);
        }

        private static string ResolveName(int id)
        {
            return id switch
            {
                200 => "FirstDriver",
                201 => "AttachedDriver",
                _ => null
            };
        }
    }
}
