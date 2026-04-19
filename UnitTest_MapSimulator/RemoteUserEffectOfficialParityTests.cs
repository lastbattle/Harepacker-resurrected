using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Pools;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class RemoteUserEffectOfficialParityTests
    {
        [Fact]
        public void ResolveRemoteHitMobAttackEffectAnchorForParity_AttachUsesOwnerWorldOrigin()
        {
            var actor = new RemoteUserActor(
                characterId: 77,
                name: "remote",
                build: new CharacterBuild(),
                position: new Vector2(320f, 640f),
                facingRight: true,
                actionName: "stand1",
                sourceTag: "test",
                isVisibleInWorld: true);
            var packet = new RemoteUserHitPacket(
                CharacterId: actor.CharacterId,
                AttackIndex: 0,
                Damage: 123,
                MobTemplateId: 2400008,
                MobHitFacingLeft: false,
                HasMobHit: false,
                MobHitDamagePercent: 0,
                PowerGuard: false,
                MobId: null,
                MobHitAction: null,
                MobHitX: null,
                MobHitY: null,
                IncDecType: 0,
                HitFlags: 0,
                HpDelta: 1,
                SkillId: null);

            Vector2 effectAnchor = RemoteUserActorPool.ResolveRemoteHitMobAttackEffectAnchorForParity(
                actor,
                packet,
                currentTime: 1000,
                attachToOwner: true);
            Vector2 soundOrigin = RemoteUserActorPool.ResolveRemoteHitMobAttackSoundOriginForParity(
                actor,
                packet,
                currentTime: 1000);

            Assert.Equal(actor.Position, effectAnchor);
            Assert.NotEqual(effectAnchor, soundOrigin);
        }

        [Fact]
        public void ResolveMobAttackHitAttachToOwnerFromHitNodeForParity_AttachZeroOverridesAttachFacing()
        {
            var hitNode = new WzSubProperty("hit");
            hitNode.WzProperties.Add(new WzIntProperty("attach", 0));
            hitNode.WzProperties.Add(new WzIntProperty("attachfacing", 1));

            bool attachToOwner = RemoteUserActorPool.ResolveMobAttackHitAttachToOwnerFromHitNodeForParity(hitNode);

            Assert.False(attachToOwner);
        }

        [Fact]
        public void ResolveMobAttackHitAttachToOwnerFromHitNodeForParity_AttachFacingEnablesAttachWhenAttachMissing()
        {
            var hitNode = new WzSubProperty("hit");
            hitNode.WzProperties.Add(new WzIntProperty("attachfacing", 1));

            bool attachToOwner = RemoteUserActorPool.ResolveMobAttackHitAttachToOwnerFromHitNodeForParity(hitNode);

            Assert.True(attachToOwner);
        }
    }
}
