using System.Reflection;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Pools;

namespace UnitTest_MapSimulator;

public sealed class RemoteUserAfterImageParityTests
{
    [Fact]
    public void TryGetActionStringFromCode_ResolvesKnownCharacterAction()
    {
        bool resolved = CharacterPart.TryGetActionStringFromCode((int)CharacterAction.SwingT2, out string actionName);

        Assert.True(resolved);
        Assert.Equal("swingT2", actionName);
        Assert.False(CharacterPart.TryGetActionStringFromCode(999, out _));
    }

    [Fact]
    public void TryParseMeleeAttack_ActionCodeOnlyPayload_ResolvesActionName()
    {
        byte[] payload = BuildMeleeAttackPayload(
            characterId: 321,
            skillId: 1111003,
            masteryPercent: 60,
            chargeSkillId: 1211004,
            facingRaw: 1,
            actionCodeOnly: (byte)CharacterAction.SwingT2);

        bool parsed = RemoteUserPacketCodec.TryParseMeleeAttack(payload, out RemoteUserMeleeAttackPacket packet, out string error);

        Assert.True(parsed, error);
        Assert.Equal(321, packet.CharacterId);
        Assert.Equal(1111003, packet.SkillId);
        Assert.Equal(60, packet.MasteryPercent);
        Assert.Equal(1211004, packet.ChargeSkillId);
        Assert.True(packet.FacingRight);
        Assert.Equal((int)CharacterAction.SwingT2, packet.ActionCode);
        Assert.Equal("swingT2", packet.ActionName);
    }

    [Fact]
    public void TryResolveMeleeAfterImageCatalogAction_UsesClientAliasFallbacks()
    {
        MethodInfo method = typeof(SkillLoader).GetMethod(
            "TryResolveMeleeAfterImageCatalogAction",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var catalog = new MeleeAfterImageCatalog();
        var expectedAction = new MeleeAfterImageAction();
        catalog.Actions["swingD1"] = expectedAction;

        object[] args = { catalog, "swingO1", null };
        bool resolved = (bool)method.Invoke(null, args);

        Assert.True(resolved);
        Assert.Same(expectedAction, args[2]);
    }

    [Fact]
    public void TryResolveMeleeAfterImageCatalogAction_MapsGenericAttackAliasToSwingFamily()
    {
        MethodInfo method = typeof(SkillLoader).GetMethod(
            "TryResolveMeleeAfterImageCatalogAction",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var catalog = new MeleeAfterImageCatalog();
        var expectedAction = new MeleeAfterImageAction();
        catalog.Actions["swingO3"] = expectedAction;

        object[] args = { catalog, "attack2", null };
        bool resolved = (bool)method.Invoke(null, args);

        Assert.True(resolved);
        Assert.Same(expectedAction, args[2]);
    }

    private static byte[] BuildMeleeAttackPayload(
        int characterId,
        int skillId,
        int masteryPercent,
        int chargeSkillId,
        byte facingRaw,
        byte actionCodeOnly)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(characterId);
        writer.Write(skillId);
        writer.Write(masteryPercent);
        writer.Write(chargeSkillId);
        writer.Write(facingRaw);
        writer.Write(actionCodeOnly);
        writer.Flush();
        return stream.ToArray();
    }
}
