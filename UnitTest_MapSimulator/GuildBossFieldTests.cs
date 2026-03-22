using HaCreator.MapSimulator.Effects;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public sealed class GuildBossFieldTests
{
    [Fact]
    public void TryHandleLocalPulleyAttack_UsesAttackHitbox_AndQueuesClientRequest()
    {
        GuildBossField field = new();
        field.Enable();
        field.InitPulley(-59, -394, "Map/Obj/guild.img/syarenian/boss/1");
        field.InitHealer(295, -564, "Map/Obj/guild.img/syarenian/boss/0");

        Rectangle attackHitbox = new(-240, -300, 120, 90);

        bool handled = field.TryHandleLocalPulleyAttack(attackHitbox, 1000, out string message);

        Assert.True(handled);
        Assert.Equal("Guild boss pulley engaged.", message);
        Assert.Equal(1, field.PulleyState);
        Assert.True(field.HasPendingLocalPulleySequence);
        Assert.True(field.PendingPulleyPacketRequest.HasValue);
        Assert.Equal(1, field.PendingPulleyPacketRequest.Value.Sequence);
    }

    [Fact]
    public void ExternalPulleyPacket_CancelsLocalPreview_AndClearsPendingRequest()
    {
        GuildBossField field = new();
        field.Enable();
        field.InitPulley(-59, -394, "Map/Obj/guild.img/syarenian/boss/1");
        field.InitHealer(295, -564, "Map/Obj/guild.img/syarenian/boss/0");

        field.TryHandleLocalPulleyAttack(new Rectangle(-240, -300, 120, 90), 1000, out _);

        field.OnPulleyStateChange(2, 1100);

        Assert.Equal(2, field.PulleyState);
        Assert.False(field.HasPendingLocalPulleySequence);
        Assert.Null(field.PendingPulleyPacketRequest);
    }

    [Fact]
    public void OnHealerMove_AppliesDecodedYImmediately_AndTriggersHealOnlyOnRise()
    {
        GuildBossField field = new();
        field.Enable();
        field.InitHealer(295, -564, "Map/Obj/guild.img/syarenian/boss/0");

        field.OnHealerMove(-400, 1000);

        Assert.Equal(-400f, field.HealerY);
        Assert.Equal(-400f, field.HealerTargetY);
        Assert.False(field.IsHealEffectActive);

        field.OnHealerMove(-500, 1100);

        Assert.Equal(-500f, field.HealerY);
        Assert.Equal(-500f, field.HealerTargetY);
        Assert.True(field.IsHealEffectActive);
    }
}
