using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.UI.Windows;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator
{
    public class CharacterInfoPopularityParityTests
    {
        [Fact]
        public void PopularityPreviewService_AllowsDefameBelowZero_ForRemoteInspectTarget()
        {
            UserInfoPopularityPreviewService service = new();
            CharacterBuild targetBuild = new CharacterBuild { Id = 321, Name = "Remote", Fame = 0 };
            UserInfoUI.UserInfoActionContext context = new(
                isRemoteTarget: true,
                characterId: targetBuild.Id,
                characterName: targetBuild.Name,
                build: targetBuild,
                locationSummary: "Henesys",
                channel: 1);

            bool canRequest = service.CanRequest(context, UserInfoUI.PopularityChangeDirection.Down);
            string requestStatus = service.HandleRequest(context, UserInfoUI.PopularityChangeDirection.Down, currentTick: 1000);
            string resultStatus = service.Update(currentTick: 1901, remoteUserPool: null);

            Assert.True(canRequest);
            Assert.Contains("pending", requestStatus, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("result applied", resultStatus, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(-1, targetBuild.Fame);
        }

        [Fact]
        public void PopularityPreviewService_RespectsFameBounds()
        {
            UserInfoPopularityPreviewService service = new();
            CharacterBuild highBuild = new CharacterBuild { Id = 11, Name = "High", Fame = 30000 };
            CharacterBuild lowBuild = new CharacterBuild { Id = 12, Name = "Low", Fame = -30000 };

            UserInfoUI.UserInfoActionContext highContext = new(true, highBuild.Id, highBuild.Name, highBuild, "Ellinia", 1);
            UserInfoUI.UserInfoActionContext lowContext = new(true, lowBuild.Id, lowBuild.Name, lowBuild, "Perion", 1);

            Assert.False(service.CanRequest(highContext, UserInfoUI.PopularityChangeDirection.Up));
            Assert.False(service.CanRequest(lowContext, UserInfoUI.PopularityChangeDirection.Down));
        }

        [Fact]
        public void RemoteProfileMetadata_PreservesNegativeFame_InRemoteAndWeddingSeams()
        {
            RemoteUserActorPool pool = new();
            CharacterBuild build = new CharacterBuild { Id = 77, Name = "RemoteFame", Fame = 3 };
            bool added = pool.TryAddOrUpdate(77, build, new Vector2(0f, 0f), out _);
            Assert.True(added);

            RemoteUserProfilePacket packet = new(
                CharacterId: 77,
                Level: 30,
                JobId: 100,
                GuildName: null,
                AllianceName: null,
                Fame: -12,
                WorldRank: null,
                JobRank: null,
                HasRide: null,
                HasPendantSlot: null,
                HasPocketSlot: null,
                TraitCharisma: null,
                TraitInsight: null,
                TraitWill: null,
                TraitCraft: null,
                TraitSense: null,
                TraitCharm: null,
                HasMedal: null,
                HasCollection: null);

            bool applied = pool.TryApplyProfileMetadata(packet, out _);
            bool mirroredFromWedding = MapSimulator.TryApplyWeddingRemoteProfileMetadata(pool, 77, build, out _);

            Assert.True(applied);
            Assert.True(mirroredFromWedding);
            Assert.True(pool.TryGetActor(77, out RemoteUserActor actor));
            Assert.NotNull(actor.Build);
            Assert.Equal(-12, actor.Build.Fame);
            Assert.True(actor.Build.HasAuthoritativeProfileFame);
        }
    }
}
