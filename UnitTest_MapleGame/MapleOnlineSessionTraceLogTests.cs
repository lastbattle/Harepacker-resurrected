using System.IO;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapleGame
{
    public sealed class MapleOnlineSessionTraceLogTests
    {
        [Fact]
        public void TraceLog_PersistsHeaderAndEntries_AfterDispose()
        {
            string directory = Path.Combine(Path.GetTempPath(), "MapleGameRuntimeTests");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"{Guid.NewGuid:N}.trace.log");

            try
            {
                using (var traceLog = new MapleOnlineSessionTraceLog(path))
                {
                    traceLog.Append("online-start");
                    traceLog.Append("online-stop");
                }

                string[] lines = File.ReadAllLines(path);
                Assert.Equal(new[] { "session-trace-start", "online-start", "online-stop" }, lines);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
