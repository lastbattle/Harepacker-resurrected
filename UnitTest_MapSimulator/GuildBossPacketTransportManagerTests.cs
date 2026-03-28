using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator;

public sealed class GuildBossPacketTransportManagerTests
{
    [Fact]
    public async Task TrySendPulleyRequest_EmitsLegacyAndRawPacketLines()
    {
        using var manager = new GuildBossPacketTransportManager();
        int port = GetAvailablePort();
        manager.Start(port);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        Assert.True(SpinWait.SpinUntil(() => manager.HasConnectedClients, TimeSpan.FromSeconds(3)));

        using var reader = new StreamReader(client.GetStream());
        var request = new GuildBossField.PulleyPacketRequest(TickCount: 3210, Sequence: 7);

        bool sent = manager.TrySendPulleyRequest(request, out string status);

        Assert.True(sent, status);
        Assert.Equal("pulleyhit 7 3210", await ReadLineWithTimeoutAsync(reader));
        Assert.Equal("packetoutraw 0301", await ReadLineWithTimeoutAsync(reader));
    }

    [Fact]
    public void TryParsePacketLine_DecodesOpcodeWrappedHealerPacket()
    {
        bool parsed = GuildBossPacketTransportManager.TryParsePacketLine(
            "packetraw 5801D4FE",
            out int packetType,
            out byte[] payload,
            out string error);

        Assert.True(parsed, error);
        Assert.Equal(344, packetType);
        Assert.Equal(Convert.FromHexString("D4FE"), payload);
    }

    private static async Task<string> ReadLineWithTimeoutAsync(StreamReader reader)
    {
        Task<string?> readTask = reader.ReadLineAsync();
        Task completedTask = await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.Same(readTask, completedTask);
        string? line = await readTask;
        Assert.NotNull(line);
        return line;
    }

    private static int GetAvailablePort()
    {
        TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
