using System.Diagnostics;
using HaCreator.MapSimulator.Contracts;
using Microsoft.Xna.Framework;
using Color = Microsoft.Xna.Framework.Color;
using Microsoft.Xna.Framework.Graphics;
using Xunit.Abstractions;

namespace UnitTest_MapleGame;

[Collection("Game session host")]
public sealed class FrameworkGraphicsLifecycleTests
{
    private readonly ITestOutputHelper output;

    public FrameworkGraphicsLifecycleTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [FrameworkGraphicsFact]
    public async Task RepeatedMinimalGraphicsDevicesReportNativeLifecycleGrowth()
    {
        ProcessMetrics baseline = ProcessMetrics.Capture();
        output.WriteLine($"minimal framework baseline {baseline}");

        for (int cycle = 1; cycle <= 3; cycle++)
        {
            MinimalGraphicsGame game = null;
            Assert.True(GameSessionHost.TryStart(() => game = new MinimalGraphicsGame(), out GameSessionHandle handle));
            GameSessionResult result = await handle.Completion.WaitAsync(TimeSpan.FromMinutes(1));

            Assert.Equal(GameSessionOutcome.Closed, result.Outcome);
            Assert.Null(result.Error);
            Assert.NotNull(game);
            Assert.True(game.FramesRendered >= 1);
            Assert.True(game.Sentinel.IsDisposed);

            var weakGame = new WeakReference(game);
            game = null;
            for (int attempt = 0; attempt < 5 && weakGame.IsAlive; attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                await Task.Yield();
            }

            ProcessMetrics current = ProcessMetrics.Capture();
            output.WriteLine(
                $"minimal framework after cycle {cycle}/3 {current}; " +
                $"deltaThreads={current.ThreadCount - baseline.ThreadCount} " +
                $"deltaHandles={current.HandleCount - baseline.HandleCount} " +
                $"gameAlive={weakGame.IsAlive}");
        }
    }

    private sealed class MinimalGraphicsGame : Game, IGameSession
    {
        private readonly GraphicsDeviceManager graphics;

        public MinimalGraphicsGame()
        {
            graphics = new GraphicsDeviceManager(this)
            {
                GraphicsProfile = GraphicsProfile.HiDef,
                PreferredBackBufferWidth = 64,
                PreferredBackBufferHeight = 64,
                SynchronizeWithVerticalRetrace = false
            };
        }

        public int FramesRendered { get; private set; }
        public Texture2D Sentinel { get; private set; }

        protected override void LoadContent()
        {
            Sentinel = new Texture2D(GraphicsDevice, 1, 1);
            Sentinel.SetData(new[] { Color.CornflowerBlue });
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            FramesRendered++;
            if (FramesRendered >= 2)
            {
                Exit();
            }
            base.Draw(gameTime);
        }

        protected override void UnloadContent()
        {
            Sentinel?.Dispose();
            base.UnloadContent();
        }

        void IGameSession.Run(CancellationToken cancellationToken)
        {
            using CancellationTokenRegistration registration = cancellationToken.Register(Exit);
            cancellationToken.ThrowIfCancellationRequested();
            Run();
        }
    }

    private sealed record ProcessMetrics(long ManagedBytes, long WorkingSetBytes, int HandleCount, int ThreadCount)
    {
        public static ProcessMetrics Capture()
        {
            using Process process = Process.GetCurrentProcess();
            process.Refresh();
            return new ProcessMetrics(
                GC.GetTotalMemory(forceFullCollection: false),
                process.WorkingSet64,
                process.HandleCount,
                process.Threads.Count);
        }

        public override string ToString() =>
            $"managed={ManagedBytes} workingSet={WorkingSetBytes} handles={HandleCount} threads={ThreadCount}";
    }

    private sealed class FrameworkGraphicsFactAttribute : FactAttribute
    {
        public FrameworkGraphicsFactAttribute()
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable("MAPLEGAME_GRAPHICS_TESTS"),
                    "1",
                    StringComparison.Ordinal))
            {
                Skip = "Set MAPLEGAME_GRAPHICS_TESTS=1 to run the minimal MonoGame graphics lifecycle diagnostic.";
            }
        }
    }
}
