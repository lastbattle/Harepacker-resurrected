using System.Collections.Concurrent;
using HaCreator.MapSimulator.Assets;
using HaCreator.MapSimulator.Contracts;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Loaders;
using MapleLib.WzLib;
using Moq;

namespace UnitTest_MapleGame;

[Collection("Game session host")]
public sealed class NpcMissingAssetRelaunchTests
{
    [Theory]
    [InlineData("9071001")]
    [InlineData("9030000")]
    public async Task MissingEmployeeNpcDoesNotFailSuccessiveSessions(string npcId)
    {
        for (int launch = 0; launch < 2; launch++)
        {
            var assets = new Mock<IRuntimeAssetSource>(MockBehavior.Strict);
            assets.Setup(source => source.FindImage("Npc", npcId + ".img"))
                .Returns((WzImage)null!);
            var session = new MissingNpcSession(assets.Object, npcId);

            Assert.True(GameSessionHost.TryStart(() => session, out var handle));
            GameSessionResult result = await handle.Completion.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Null(result.Error);
            Assert.Equal(GameSessionOutcome.Closed, result.Outcome);
            Assert.True(session.Disposed);
            assets.VerifyAll();
        }
    }

    private sealed class MissingNpcSession(IRuntimeAssetSource assets, string npcId) : IGameSession
    {
        public bool Disposed { get; private set; }

        public void Run(CancellationToken cancellationToken)
        {
            var life = new RuntimeLife(new RuntimeLifeDefinition
            {
                Asset = new RuntimeAssetKey("Npc", npcId + ".img"),
                Id = npcId
            });
            // Missing assets must be rejected before materializing GPU resources.
            Assert.Null(LifeLoader.CreateNpcFromProperty(
                null!, life, assets, 1f, null!, new ConcurrentBag<WzObject>(),
                includeTooltips: false));
        }

        public void Dispose() => Disposed = true;
    }
}
