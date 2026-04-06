using System.Reflection;

namespace UnitTest_MapSimulator;

public class PacketFieldUtilityParityTests
{
    private static readonly Assembly HaCreatorAssembly = typeof(HaCreator.MapSimulator.MapSimulator).Assembly;

    [Theory]
    [InlineData(0xA4, "You have successfully blocked access.")]
    [InlineData(0xA6, "The unblocking has been successful")]
    [InlineData(0xA9, "You have successfully removed the name from the ranks.")]
    [InlineData(0xAA, "You have entered an invalid character name.")]
    [InlineData(0xAC, "You have either entered a wrong NPC name or")]
    [InlineData(0xAD, "Your request failed.")]
    [InlineData(0xB0, "Hired Merchant located at : {0}")]
    [InlineData(0xB1, "Unable to find the hired merchant.")]
    [InlineData(0xBDF, "Unable to send the message. Please enter the user's name before warning.")]
    [InlineData(0xBE0, "Your warning has been successfully sent.")]
    public void AdminResultStringPoolText_ResolvesRecoveredClientText(int stringPoolId, string expected)
    {
        Type type = HaCreatorAssembly.GetType("HaCreator.MapSimulator.Interaction.PacketFieldUtilityAdminResultStringPoolText", throwOnError: true)!;
        MethodInfo method = type.GetMethod("TryResolve", BindingFlags.Static | BindingFlags.Public)!;

        object?[] args = [stringPoolId, null];
        bool resolved = (bool)method.Invoke(null, args)!;

        Assert.True(resolved);
        Assert.Equal(expected, Assert.IsType<string>(args[1]));
    }

    [Fact]
    public void BuildOfficialSessionFootHoldInfoResponsePayload_MatchesClientStateCurPosAndReverseFlags()
    {
        Type runtimeType = HaCreatorAssembly.GetType("HaCreator.MapSimulator.Interaction.PacketFieldUtilityRuntime", throwOnError: true)!;
        Type movingStateType = HaCreatorAssembly.GetType("HaCreator.MapSimulator.Interaction.PacketFieldUtilityMovingFootholdState", throwOnError: true)!;
        Type footholdEntryType = HaCreatorAssembly.GetType("HaCreator.MapSimulator.Interaction.PacketFieldUtilityFootholdEntry", throwOnError: true)!;

        object movingState = Activator.CreateInstance(
            movingStateType,
            30,
            100,
            200,
            300,
            400,
            123,
            456,
            true,
            false)!;

        object firstEntry = Activator.CreateInstance(
            footholdEntryType,
            "platform-1",
            2,
            new[] { 1, 2 },
            movingState)!;

        object secondEntry = Activator.CreateInstance(
            footholdEntryType,
            "platform-2",
            0,
            new[] { 3 },
            null)!;

        Array entries = Array.CreateInstance(footholdEntryType, 2);
        entries.SetValue(firstEntry, 0);
        entries.SetValue(secondEntry, 1);

        MethodInfo buildMethod = runtimeType.GetMethod(
            "BuildOfficialSessionFootHoldInfoResponsePayload",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        byte[] payload = (byte[])buildMethod.Invoke(null, [entries])!;

        Assert.Equal(2 * (sizeof(int) * 3 + sizeof(byte) * 2), payload.Length);

        Assert.Equal(2, BitConverter.ToInt32(payload, 0));
        Assert.Equal(123, BitConverter.ToInt32(payload, 4));
        Assert.Equal(456, BitConverter.ToInt32(payload, 8));
        Assert.Equal(1, payload[12]);
        Assert.Equal(0, payload[13]);

        Assert.Equal(0, BitConverter.ToInt32(payload, 14));
        Assert.Equal(0, BitConverter.ToInt32(payload, 18));
        Assert.Equal(0, BitConverter.ToInt32(payload, 22));
        Assert.Equal(0, payload[26]);
        Assert.Equal(0, payload[27]);
    }
}
