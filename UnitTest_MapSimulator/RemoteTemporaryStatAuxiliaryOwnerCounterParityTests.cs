using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class RemoteTemporaryStatAuxiliaryOwnerCounterParityTests
    {
        [Fact]
        public void RemoteTemporaryStatOwnerSlots_AreStableAndDistinct()
        {
            var ownerNames = new HashSet<string>();
            for (int familyCode = 0; familyCode <= 8; familyCode++)
            {
                string ownerName = RemoteUserActorPool.ResolveRemoteTemporaryStatAvatarEffectOwnerNameForTesting(familyCode);
                Assert.False(string.IsNullOrWhiteSpace(ownerName));
                Assert.StartsWith("aux.remote.temporaryStat.", ownerName);
                Assert.EndsWith(".persistent", ownerName);
                Assert.True(ownerNames.Add(ownerName), $"Duplicate owner name '{ownerName}' at family code {familyCode}.");
            }
        }

        [Fact]
        public void RemoteAuxiliaryOwnerCounterContextMatch_RequiresSkillActionAndFacing()
        {
            Assert.True(RemoteUserActorPool.IsRemoteAuxiliaryLayerOwnerCounterContextMatchForTesting(
                storedSkillId: 32121003,
                storedActionName: "walk1",
                storedFacingRight: true,
                requestedSkillId: 32121003,
                requestedActionName: "walk1",
                requestedFacingRight: true));

            Assert.False(RemoteUserActorPool.IsRemoteAuxiliaryLayerOwnerCounterContextMatchForTesting(
                storedSkillId: 32121003,
                storedActionName: "walk1",
                storedFacingRight: true,
                requestedSkillId: 32121004,
                requestedActionName: "walk1",
                requestedFacingRight: true));

            Assert.False(RemoteUserActorPool.IsRemoteAuxiliaryLayerOwnerCounterContextMatchForTesting(
                storedSkillId: 32121003,
                storedActionName: "walk1",
                storedFacingRight: true,
                requestedSkillId: 32121003,
                requestedActionName: "jump",
                requestedFacingRight: true));

            Assert.False(RemoteUserActorPool.IsRemoteAuxiliaryLayerOwnerCounterContextMatchForTesting(
                storedSkillId: 32121003,
                storedActionName: "walk1",
                storedFacingRight: true,
                requestedSkillId: 32121003,
                requestedActionName: "walk1",
                requestedFacingRight: false));
        }

        [Fact]
        public void RemoteAdditionalLayerOwnerSlots_AreStableForPacketEmotionEffectByItemAndCarryItemEffect()
        {
            string packetOwnedEmotionOwnerName = RemoteUserActorPool.ResolveRemotePacketOwnedEmotionOwnerNameForTesting();
            string effectByItemOwnerName = RemoteUserActorPool.ResolveRemoteEffectByItemOwnerNameForTesting();
            string carryItemEffectOwnerName = RemoteUserActorPool.ResolveRemoteCarryItemEffectOwnerNameForTesting();

            Assert.Equal("aux.remote.packetOwnedEmotion.persistent", packetOwnedEmotionOwnerName);
            Assert.Equal("aux.remote.effectByItem.oneTime", effectByItemOwnerName);
            Assert.Equal("aux.remote.carryItemEffect.persistent", carryItemEffectOwnerName);
            Assert.NotEqual(packetOwnedEmotionOwnerName, effectByItemOwnerName);
            Assert.NotEqual(packetOwnedEmotionOwnerName, carryItemEffectOwnerName);
            Assert.NotEqual(effectByItemOwnerName, carryItemEffectOwnerName);
        }

        [Fact]
        public void RemotePacketOwnedEmotionCounterSkillKey_DistinguishesContextShape()
        {
            int baseKey = RemoteUserActorPool.ResolveRemotePacketOwnedEmotionCounterSkillIdForTesting(
                itemId: 5170000,
                emotionId: 2,
                byItemOption: false);
            int differentEmotionKey = RemoteUserActorPool.ResolveRemotePacketOwnedEmotionCounterSkillIdForTesting(
                itemId: 5170000,
                emotionId: 4,
                byItemOption: false);
            int differentModeKey = RemoteUserActorPool.ResolveRemotePacketOwnedEmotionCounterSkillIdForTesting(
                itemId: 5170000,
                emotionId: 2,
                byItemOption: true);
            int differentItemKey = RemoteUserActorPool.ResolveRemotePacketOwnedEmotionCounterSkillIdForTesting(
                itemId: 5180000,
                emotionId: 2,
                byItemOption: false);

            Assert.NotEqual(baseKey, differentEmotionKey);
            Assert.NotEqual(baseKey, differentModeKey);
            Assert.NotEqual(baseKey, differentItemKey);
        }

        [Fact]
        public void RemoteCarryItemOwnerCounter_RestoresElapsedWhenContextMatches()
        {
            const int characterId = 11;
            const int carryItemEffectCount = 7;
            var pool = new RemoteUserActorPool();
            var build = new CharacterBuild
            {
                Name = "RemoteCarryOwner",
                Equipment = new Dictionary<EquipSlot, CharacterPart>()
            };

            Assert.True(pool.TryAddOrUpdate(characterId, build, Vector2.Zero, out _));
            Assert.True(pool.TryApplyEnterFieldAvatarPresentation(
                new RemoteUserEnterFieldPacket(
                    CharacterId: characterId,
                    Name: "RemoteCarryOwner",
                    AvatarLook: null,
                    X: 0,
                    Y: 0,
                    FacingRight: true,
                    ActionName: "stand1",
                    IsVisibleInWorld: true,
                    PortableChairItemId: null,
                    TemporaryStats: default,
                    CarryItemEffect: carryItemEffectCount),
                currentTime: 1_000,
                out _));
            Assert.True(pool.TryApplyEnterFieldAvatarPresentation(
                new RemoteUserEnterFieldPacket(
                    CharacterId: characterId,
                    Name: "RemoteCarryOwner",
                    AvatarLook: null,
                    X: 0,
                    Y: 0,
                    FacingRight: true,
                    ActionName: "stand1",
                    IsVisibleInWorld: true,
                    PortableChairItemId: null,
                    TemporaryStats: default,
                    CarryItemEffect: null),
                currentTime: 1_300,
                out _));
            Assert.True(pool.TryApplyEnterFieldAvatarPresentation(
                new RemoteUserEnterFieldPacket(
                    CharacterId: characterId,
                    Name: "RemoteCarryOwner",
                    AvatarLook: null,
                    X: 0,
                    Y: 0,
                    FacingRight: true,
                    ActionName: "stand1",
                    IsVisibleInWorld: true,
                    PortableChairItemId: null,
                    TemporaryStats: default,
                    CarryItemEffect: carryItemEffectCount),
                currentTime: 2_000,
                out _));

            Assert.True(pool.TryGetActor(characterId, out RemoteUserActor actor));
            Assert.Equal(1_700, actor.CarryItemEffectAppliedTime);
        }
    }
}
