using HaCreator.MapSimulator;
using System;
using System.Reflection;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class FollowCharacterFailureParityTests
{
    private static readonly MethodInfo FollowCharacterFailureResolveMethod =
        typeof(MapSimulator).Assembly
            .GetType("HaCreator.MapSimulator.Interaction.FollowCharacterFailureCodec", throwOnError: true)!
            .GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static)!;

    private static readonly PropertyInfo FollowCharacterFailureMessageProperty =
        FollowCharacterFailureResolveMethod.ReturnType.GetProperty("Message", BindingFlags.Public | BindingFlags.Instance)!;

    private static readonly MethodInfo BuildFollowPromptBodyMethod =
        typeof(MapSimulator).GetMethod("BuildPacketOwnedFollowPromptBody", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Theory]
    [InlineData(0, "The follow request could not be executed due to an unknown error.")]
    [InlineData(1, "You are currently in a place where you cannot accept the follow request.")]
    [InlineData(3, "Follow target cannot accept the request at this time.")]
    [InlineData(4, "Follow target cannot accept the request at this time.")]
    [InlineData(5, "You cannot send a follow request while you are already following someone.")]
    [InlineData(6, "The follow request has not been accepted.")]
    public void ResolveFollowCharacterFailureMessage_UsesClientStringPoolText(int reasonCode, string expected)
    {
        string message = ResolveFailureMessage(reasonCode, 0, _ => null);

        Assert.Equal(expected, message);
    }

    [Fact]
    public void ResolveFollowCharacterFailureMessage_FormatsOccupiedTargetNameFromClientTemplate()
    {
        string message = ResolveFailureMessage(2, 77, id => id == 77 ? "Alice" : null);

        Assert.Equal("Your target is already following Alice.", message);
    }

    [Fact]
    public void ResolveFollowCharacterFailureMessage_FallsBackToClientInvalidMapTextWhenOccupiedTargetNameIsMissing()
    {
        string message = ResolveFailureMessage(2, 77, _ => null);

        Assert.Equal("You are currently in a place where you cannot accept the follow request.", message);
    }

    [Fact]
    public void BuildPacketOwnedFollowPromptBody_UsesClientPromptTextAndRequesterSuffix()
    {
        string body = (string)BuildFollowPromptBodyMethod.Invoke(null, new object[] { "Escort NPC", 44 })!;

        Assert.Equal("You have been sent a follow request.\r\nRequester: Escort NPC", body);
    }

    [Fact]
    public void BuildPacketOwnedFollowPromptBody_OmitsSyntheticSuffixWhenRequesterIsUnknown()
    {
        string body = (string)BuildFollowPromptBodyMethod.Invoke(null, new object[] { null!, 0 })!;

        Assert.Equal("You have been sent a follow request.", body);
    }

    private static string ResolveFailureMessage(int reasonCode, int driverId, Func<int, string> driverNameResolver)
    {
        object failureInfo = FollowCharacterFailureResolveMethod.Invoke(null, new object[] { reasonCode, driverId, driverNameResolver })!;
        return (string)FollowCharacterFailureMessageProperty.GetValue(failureInfo)!;
    }
}
