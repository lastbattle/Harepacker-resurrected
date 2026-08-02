using MapleLib.PacketLib;

namespace UnitTest_MapSimulator
{
    public class MapleRoleSessionProxyFactoryTests
    {
        [Fact]
        public void Create_WithIndependentMode_ReturnsDistinctChannelProxies()
        {
            MapleRoleSessionProxyFactory factory = new MapleRoleSessionProxyFactory(MapleHandshakePolicy.GlobalV95);

            MapleRoleSessionProxy first = factory.CreateChannel();
            MapleRoleSessionProxy second = factory.CreateChannel();

            Assert.NotSame(first, second);
        }

        [Fact]
        public void Create_WithSharedMode_ReusesProxyPerRole()
        {
            MapleRoleSessionProxyFactory factory = new MapleRoleSessionProxyFactory(
                MapleHandshakePolicy.GlobalV95,
                shareRoleSessionProxyPerRole: true);

            MapleRoleSessionProxy channelFirst = factory.CreateChannel();
            MapleRoleSessionProxy channelSecond = factory.CreateChannel();
            MapleRoleSessionProxy loginProxy = factory.CreateLogin();
            MapleRoleSessionProxy cashProxy = factory.CreateCashShop();
            MapleRoleSessionProxy mtsProxy = factory.CreateMts();

            Assert.Same(channelFirst, channelSecond);
            Assert.NotSame(channelFirst, loginProxy);
            Assert.NotSame(loginProxy, cashProxy);
            Assert.NotSame(cashProxy, mtsProxy);
        }

        [Fact]
        public void DescribeAuthorityStatus_SharedModeMentionsRunningState()
        {
            MapleRoleSessionProxyFactory factory = new MapleRoleSessionProxyFactory(
                MapleHandshakePolicy.GlobalV95,
                shareRoleSessionProxyPerRole: true);
            _ = factory.CreateChannel();
            _ = factory.CreateLogin();
            _ = factory.CreateCashShop();
            _ = factory.CreateMts();

            string status = factory.DescribeAuthorityStatus();

            Assert.Contains("shared per-role proxies", status);
            Assert.Contains("Channel:stopped/sessions=0/server=0/client=0/sent=0/last=never", status);
            Assert.Contains("Login:stopped/sessions=0/server=0/client=0/sent=0/last=never", status);
            Assert.Contains("CashShop:stopped/sessions=0/server=0/client=0/sent=0/last=never", status);
            Assert.Contains("Mts:stopped/sessions=0/server=0/client=0/sent=0/last=never", status);
        }
    }
}
