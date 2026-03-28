using System.Collections;
using System.Reflection;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public class SummonedPoolPacketParityTests
{
    [Fact]
    public void DispatchCreated_DecodesAvatarLookAndTeslaTail()
    {
        SummonedPool pool = new();

        LoginAvatarLook avatarLook = LoginAvatarLookCodec.CreateLook(
            CharacterGender.Female,
            SkinColor.Tan,
            21290,
            33030,
            new Dictionary<EquipSlot, int>
            {
                [EquipSlot.Cap] = 1002140,
                [EquipSlot.Weapon] = 1302000
            },
            weaponStickerItemId: 1702100,
            petIds: new[] { 5000000, 0, 0 });

        byte[] payload = BuildCreatedPayload(
            ownerCharacterId: 900001,
            summonedObjectId: 77,
            skillId: 35111002,
            characterLevel: 120,
            skillLevel: 20,
            position: new Point(150, 220),
            moveAction: 1,
            footholdId: 42,
            moveAbility: 3,
            assistType: 1,
            enterType: 2,
            avatarLook,
            teslaCoilState: 1,
            teslaTrianglePoints: new[]
            {
                new Point(10, 20),
                new Point(30, 40),
                new Point(50, 60)
            });

        bool dispatched = pool.TryDispatchPacket((int)SummonedPacketType.Created, payload, 1000, out string message);

        Assert.True(dispatched, message);

        object state = GetSummonState(pool, 77);
        LoginAvatarLook decodedLook = Assert.IsType<LoginAvatarLook>(GetProperty(state, "AvatarLook"));
        Assert.Equal(avatarLook.FaceId, decodedLook.FaceId);
        Assert.Equal(avatarLook.HairId, decodedLook.HairId);
        Assert.Equal(avatarLook.WeaponStickerItemId, decodedLook.WeaponStickerItemId);
        Assert.Equal((byte)1, Assert.IsType<byte>(GetProperty(state, "TeslaCoilState")));

        Point[] triangle = Assert.IsType<Point[]>(GetProperty(state, "TeslaTrianglePoints"));
        Assert.Equal(3, triangle.Length);
        Assert.Equal(new Point(10, 20), triangle[0]);
        Assert.Equal(new Point(50, 60), triangle[2]);

        ActiveSummon summon = Assert.IsType<ActiveSummon>(GetProperty(state, "Summon"));
        Assert.False(summon.FacingRight);
        Assert.Equal(150f, summon.PositionX);
        Assert.Equal(220f, summon.PositionY);
    }

    [Fact]
    public void DispatchAttack_DecodesCharacterLevelFacingAndMobTuples()
    {
        SummonedPool pool = new();
        bool created = pool.TryCreate(
            new SummonedCreatePacket(
                1234,
                88,
                33111003,
                70,
                10,
                new Vector2(25f, 35f),
                0,
                0,
                0,
                0,
                0,
                null,
                0,
                Array.Empty<Point>()),
            500,
            out string createMessage);
        Assert.True(created, createMessage);

        byte[] payload = BuildAttackPayload(
            ownerCharacterId: 1234,
            summonedObjectId: 88,
            characterLevel: 87,
            packedAction: 0x83,
            targets: new[]
            {
                new SummonedAttackTargetPacket(9001, 4, 12345),
                new SummonedAttackTargetPacket(0, 0, 0)
            },
            tailByte: 7);

        bool dispatched = pool.TryDispatchPacket((int)SummonedPacketType.Attack, payload, 900, out string message);

        Assert.True(dispatched, message);

        object state = GetSummonState(pool, 88);
        Assert.Equal(87, Assert.IsType<int>(GetProperty(state, "OwnerCharacterLevel")));
        Assert.Equal((byte)3, Assert.IsType<byte>(GetProperty(state, "LastAttackAction")));
        Assert.Equal((byte)7, Assert.IsType<byte>(GetProperty(state, "LastAttackTailByte")));

        IReadOnlyList<SummonedAttackTargetPacket> targets =
            Assert.IsAssignableFrom<IReadOnlyList<SummonedAttackTargetPacket>>(GetProperty(state, "LastAttackTargets"));
        Assert.Equal(2, targets.Count);
        Assert.Equal(9001, targets[0].MobObjectId);
        Assert.Equal(4, targets[0].HitAction);
        Assert.Equal(12345, targets[0].Damage);
        Assert.Equal(0, targets[1].MobObjectId);

        ActiveSummon summon = Assert.IsType<ActiveSummon>(GetProperty(state, "Summon"));
        Assert.False(summon.FacingRight);
        Assert.Equal(900, summon.LastAttackAnimationStartTime);
    }

    private static object GetSummonState(SummonedPool pool, int objectId)
    {
        FieldInfo field = typeof(SummonedPool).GetField("_summonsByObjectId", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        IDictionary dictionary = Assert.IsAssignableFrom<IDictionary>(field.GetValue(pool));
        object state = dictionary[objectId];
        Assert.NotNull(state);
        return state;
    }

    private static object GetProperty(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return property.GetValue(instance);
    }

    private static byte[] BuildCreatedPayload(
        int ownerCharacterId,
        int summonedObjectId,
        int skillId,
        byte characterLevel,
        byte skillLevel,
        Point position,
        byte moveAction,
        short footholdId,
        byte moveAbility,
        byte assistType,
        byte enterType,
        LoginAvatarLook avatarLook,
        byte teslaCoilState,
        IReadOnlyList<Point> teslaTrianglePoints)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(ownerCharacterId);
        writer.Write(summonedObjectId);
        writer.Write(skillId);
        writer.Write(characterLevel);
        writer.Write(skillLevel);
        writer.Write((short)position.X);
        writer.Write((short)position.Y);
        writer.Write(moveAction);
        writer.Write(footholdId);
        writer.Write(moveAbility);
        writer.Write(assistType);
        writer.Write(enterType);
        writer.Write((byte)(avatarLook != null ? 1 : 0));
        if (avatarLook != null)
        {
            writer.Write(LoginAvatarLookCodec.Encode(avatarLook));
        }

        writer.Write(teslaCoilState);
        if (teslaCoilState == 1 && teslaTrianglePoints != null)
        {
            foreach (Point point in teslaTrianglePoints)
            {
                writer.Write((short)point.X);
                writer.Write((short)point.Y);
            }
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] BuildAttackPayload(
        int ownerCharacterId,
        int summonedObjectId,
        byte characterLevel,
        byte packedAction,
        IReadOnlyList<SummonedAttackTargetPacket> targets,
        byte tailByte)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(ownerCharacterId);
        writer.Write(summonedObjectId);
        writer.Write(characterLevel);
        writer.Write(packedAction);
        writer.Write((byte)(targets?.Count ?? 0));
        if (targets != null)
        {
            foreach (SummonedAttackTargetPacket target in targets)
            {
                writer.Write(target.MobObjectId);
                if (target.MobObjectId != 0)
                {
                    writer.Write(target.HitAction);
                    writer.Write(target.Damage);
                }
            }
        }

        writer.Write(tailByte);
        writer.Flush();
        return stream.ToArray();
    }
}
