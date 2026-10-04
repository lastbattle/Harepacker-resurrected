using System.Reflection;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.IO;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Assets;
using HaCreator.MapSimulator.Contracts;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib;
using Microsoft.Xna.Framework;
using Color = Microsoft.Xna.Framework.Color;
using Microsoft.Xna.Framework.Graphics;
using Xunit.Abstractions;

namespace UnitTest_MapleGame;

[Collection("Game session host")]
public sealed class RuntimeGraphicsLifecycleTests
{
    private readonly ITestOutputHelper output;

    public RuntimeGraphicsLifecycleTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [GraphicsFact]
    public async Task FailedAndSuccessfulMapTransitionsPreserveAndRetireGraphicsGenerations()
    {
        using var sharpDxLeaks = SharpDxLeakDiagnostic.StartFromEnvironment();
        int cycles = ResolveCycleCount();
        int nativeHostsBefore = NativeAntiMacroEditHost.ActiveHostCount;
        var retiredGames = new List<WeakReference<MapSimulator>>();
        ProcessMetrics before = ProcessMetrics.Capture();
        output.WriteLine($"before {before}");
        for (int cycle = 1; cycle <= cycles; cycle++)
        {
            WeakReference<MapSimulator> retiredGame = await RunCycle(cycle);
            retiredGames.Add(retiredGame);
            for (int attempt = 0; attempt < 20; attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                if (!IsGameAlive(retiredGame)) break;
                // The completion continuation can precede the STA thread's final
                // stack unwind. Do not keep a TryGetTarget result on this stack.
                await Task.Delay(50);
            }
            Assert.Equal(nativeHostsBefore, NativeAntiMacroEditHost.ActiveHostCount);
            // MonoGame's static vertex declarations retain the most recently used
            // device, whose events retain its manager/game. Earlier sessions must
            // still collect; an accumulating registry must never retain the history.
            foreach (var earlierGame in retiredGames.Take(retiredGames.Count - 1))
                Assert.False(IsGameAlive(earlierGame), "An earlier game remains rooted after the next session closes.");
            ProcessMetrics after = ProcessMetrics.Capture();
            output.WriteLine($"after cycle {cycle}/{cycles} {after}");
        }

        Assert.True(GameSessionHost.TryStart(() => new ImmediateSession(), out GameSessionHandle relaunch));
        Assert.Equal(
            GameSessionOutcome.Closed,
            (await relaunch.Completion.WaitAsync(TimeSpan.FromSeconds(30))).Outcome);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsGameAlive(WeakReference<MapSimulator> game) => game.TryGetTarget(out _);

    [GraphicsFact]
    public async Task MandatoryActivationFailureFailsHostAndRetiresCommittedGeneration()
    {
        using RuntimeAssetSource assets = OpenAssetSource();
        var services = new RuntimeDataServices(assets, new SourceRuntimeAssetCatalog(assets));
        using var provider = new RuntimeMapProvider(services);
        using RuntimeMapDefinition initialMap = provider.Load(100000000);
        using RuntimeMapDefinition destinationMap = provider.Load(100020000);
        using var profile = new TemporaryProfileStorage();

        FatalActivationProbe probe = null;
        Assert.True(GameSessionHost.TryStart(() =>
        {
            var game = new MapSimulator(
                initialMap,
                "MapleGame fatal activation lifecycle test",
                new GameSessionOptions(profile) { ContentRootDirectory = ResolveContentRoot() },
                services);
            game.SetMapProvider(provider);
            probe = new FatalActivationProbe(game, destinationMap);
            game.Components.Add(probe);
            return game;
        }, out GameSessionHandle handle));

        GameSessionResult result = await handle.Completion.WaitAsync(TimeSpan.FromMinutes(3));
        Assert.NotNull(probe);
        Assert.Equal(GameSessionOutcome.Failed, result.Outcome);
        Assert.NotNull(result.Error);
        Assert.True(probe.ActivationCheckpointReached);
        Assert.True(probe.CommittedGenerationReplacedOld);
        Assert.True(probe.CommittedTexture.IsDisposed);
        Assert.Contains(
            "mandatory activation failed",
            FlattenExceptionMessages(result.Error),
            StringComparison.OrdinalIgnoreCase);
    }

    [GraphicsFact]
    public async Task HostCancellationBeforeCommitRetiresCandidateAndKeepsPriorGenerationUntilShutdown()
    {
        using RuntimeAssetSource assets = OpenAssetSource();
        var services = new RuntimeDataServices(assets, new SourceRuntimeAssetCatalog(assets));
        using var provider = new RuntimeMapProvider(services);
        using RuntimeMapDefinition initialMap = provider.Load(100000000);
        using RuntimeMapDefinition destinationMap = provider.Load(100020000);
        using var profile = new TemporaryProfileStorage();
        int nativeHostsBefore = NativeAntiMacroEditHost.ActiveHostCount;
        CancellationBeforeCommitProbe probe = null;
        GameSessionHandle handle = null;
        Assert.True(GameSessionHost.TryStart(() =>
        {
            var game = new MapSimulator(initialMap, "MapleGame cancelled destination test",
                new GameSessionOptions(profile) { ContentRootDirectory = ResolveContentRoot() }, services);
            game.SetMapProvider(provider);
            probe = new CancellationBeforeCommitProbe(game, destinationMap, () => handle.RequestStop());
            game.Components.Add(probe);
            return game;
        }, out handle));

        GameSessionResult result;
        try { result = await handle.Completion.WaitAsync(TimeSpan.FromMinutes(3)); }
        catch
        {
            handle.RequestStop();
            await handle.Completion.WaitAsync(TimeSpan.FromSeconds(30));
            throw;
        }
        Assert.NotNull(probe);
        Assert.Equal(GameSessionOutcome.Cancelled, result.Outcome);
        Assert.Null(result.Error);
        Assert.True(probe.CancellationRequestedAfterPreparation);
        Assert.True(probe.CancellationObserved);
        Assert.False(probe.ActivationReached);
        Assert.True(probe.PriorGenerationPreservedAtRejection);
        Assert.True(probe.PriorTextureAliveAtRejection);
        Assert.True(probe.CandidateTexturesDisposedAtRejection);
        Assert.NotEmpty(probe.CandidateTextures);
        Assert.All(probe.CandidateTextures, texture => Assert.True(texture.IsDisposed));
        Assert.True(probe.SessionGraphicsDevice.IsDisposed);
        Assert.Equal(nativeHostsBefore, NativeAntiMacroEditHost.ActiveHostCount);
    }

    [GraphicsFact]
    public async Task AuthoredPortalCollisionQueuesAndRendersProviderDestination()
    {
        using RuntimeAssetSource assets = OpenAssetSource();
        var services = new RuntimeDataServices(assets, new SourceRuntimeAssetCatalog(assets));
        using var provider = new RuntimeMapProvider(services);
        using RuntimeMapDefinition initialMap = provider.Load(10000);
        RuntimePortalDefinition source = Assert.Single(initialMap.Portals.Where(portal => portal.Name == "out00"));
        Assert.Equal(20000, source.TargetMapId);
        Assert.Equal("in00", source.TargetName);
        using RuntimeMapDefinition destination = provider.Load(source.TargetMapId);
        RuntimePortalDefinition target = Assert.Single(destination.Portals.Where(portal => portal.Name == source.TargetName));
        using var profile = new TemporaryProfileStorage();
        int nativeHostsBefore = NativeAntiMacroEditHost.ActiveHostCount;
        PortalRouteProbe probe = null;
        Assert.True(GameSessionHost.TryStart(() =>
        {
            var game = new MapSimulator(initialMap, "MapleGame authored portal test",
                new GameSessionOptions(profile) { ContentRootDirectory = ResolveContentRoot() }, services, source.Name);
            game.SetMapProvider(provider);
            probe = new PortalRouteProbe(game, source);
            game.Components.Add(probe);
            return game;
        }, out GameSessionHandle handle));

        GameSessionResult result;
        try { result = await handle.Completion.WaitAsync(TimeSpan.FromMinutes(3)); }
        catch
        {
            handle.RequestStop();
            await handle.Completion.WaitAsync(TimeSpan.FromSeconds(30));
            throw;
        }
        Assert.Null(result.Error);
        Assert.Equal(GameSessionOutcome.Closed, result.Outcome);
        Assert.NotNull(probe);
        Assert.True(probe.Failure == null, probe.Failure?.ToString());
        Assert.True(probe.InitialFramesRendered >= 1);
        Assert.True(probe.CollisionHandled);
        Assert.Equal(source.TargetMapId, probe.QueuedMapId);
        Assert.Equal(target.Name, probe.QueuedPortalName);
        Assert.Equal(source.TargetMapId, probe.ActiveMapId);
        Assert.InRange(probe.DestinationPosition.X, target.X - 2, target.X + 2);
        Assert.InRange(probe.DestinationPosition.Y, target.Y - 8, target.Y + 8);
        Assert.True(probe.DestinationFramesRendered >= 2);
        Assert.True(probe.PreviousTextureRetired);
        Assert.True(probe.SessionGraphicsDevice.IsDisposed);
        Assert.Equal(nativeHostsBefore, NativeAntiMacroEditHost.ActiveHostCount);
    }

    // Exercise the real collision/queue owner, not OS keyboard delivery. The probe
    // never invokes LoadMapContent: the ordinary update loop loads the destination.
    private sealed class PortalRouteProbe : DrawableGameComponent
    {
        private readonly MapSimulator game;
        private readonly RuntimePortalDefinition source;
        private object initialGeneration;
        private Texture2D previousTexture;
        private bool requested;
        public PortalRouteProbe(MapSimulator game, RuntimePortalDefinition source) : base(game)
        {
            this.game = game;
            this.source = source;
        }
        public Exception Failure { get; private set; }
        public int InitialFramesRendered { get; private set; }
        public int DestinationFramesRendered { get; private set; }
        public bool CollisionHandled { get; private set; }
        public int QueuedMapId { get; private set; }
        public string QueuedPortalName { get; private set; }
        public int ActiveMapId { get; private set; }
        public Vector2 DestinationPosition { get; private set; }
        public bool PreviousTextureRetired { get; private set; }
        public GraphicsDevice SessionGraphicsDevice { get; private set; }

        private static T Field<T>(object owner, string name) => (T)owner.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(owner)!;

        public override void Update(GameTime gameTime)
        {
            try
            {
                if (!requested && InitialFramesRendered > 0)
                {
                    SessionGraphicsDevice = game.GraphicsDevice;
                    initialGeneration = Field<object>(game, "_activeContent");
                    var pool = Field<TexturePool>(initialGeneration, "MapTextures");
                    previousTexture = new Texture2D(game.GraphicsDevice, 1, 1);
                    previousTexture.SetData(new[] { Color.Magenta });
                    pool.AddTextureToPool("portal-route-retirement-sentinel", previousTexture);
                    game.PlayerManager.TeleportTo(source.X, source.Y);
                    CollisionHandled = (bool)typeof(MapSimulator).GetMethod("TryHandlePortalInteractCore",
                        BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game,
                        new object[] { Field<int>(game, "currTickCount") })!;
                    var state = Field<HaCreator.MapSimulator.Managers.GameStateManager>(game, "_gameState");
                    if (!state.PendingMapChange)
                        throw new InvalidOperationException("Authored portal collision did not queue a map transfer.");
                    QueuedMapId = state.PendingMapId;
                    QueuedPortalName = state.PendingPortalName;
                    requested = true;
                }
                else if (DestinationFramesRendered >= 2)
                    game.Exit();
            }
            catch (Exception error) { Failure = error; game.Exit(); }
        }

        public override void Draw(GameTime gameTime)
        {
            try
            {
                object active = Field<object>(game, "_activeContent");
                ActiveMapId = Field<MapleLib.WzLib.WzStructure.MapInfo>(active, "Info").id;
                if (!requested) InitialFramesRendered++;
                else if (ActiveMapId == source.TargetMapId && !ReferenceEquals(active, initialGeneration))
                {
                    if (DestinationFramesRendered == 0)
                        DestinationPosition = game.PlayerManager.Player.Physics.GetPosition();
                    PreviousTextureRetired = previousTexture.IsDisposed;
                    DestinationFramesRendered++;
                }
            }
            catch (Exception error) { Failure = error; game.Exit(); }
        }
    }

    [GraphicsFact]
    public async Task DetachedCustomBitmapLoadsAndRendersAfterAuthoringOwnersAreDisposed()
    {
        string exportRoot = Path.Combine(Environment.GetEnvironmentVariable("MAPLEGAME_TEST_EXPORTS")!, "gms_v95");
        using RuntimeMapDefinition map = CreateCustomBitmapMap(exportRoot);
        using var overrides = new RuntimeAssetOverrideStore();
        foreach (RuntimeAssetKey key in map.AssetOverrides.Keys)
        {
            Assert.True(map.TryCreateAssetImageRoot(key, out WzImage image));
            using (image) overrides.Set(key, image);
        }
        using RuntimeAssetSource assets = RuntimeAssetSourceFactory.OpenImgDirectory(exportRoot, overrides);
        var services = new RuntimeDataServices(assets, new SourceRuntimeAssetCatalog(assets));
        using var profile = new TemporaryProfileStorage();
        int nativeHostsBefore = NativeAntiMacroEditHost.ActiveHostCount;
        CustomBitmapProbe probe = null;
        Assert.True(GameSessionHost.TryStart(() =>
        {
            var game = new MapSimulator(map, "MapleGame detached custom bitmap test",
                new GameSessionOptions(profile) { ContentRootDirectory = ResolveContentRoot() }, services, "out00");
            probe = new CustomBitmapProbe(game);
            game.Components.Add(probe);
            return game;
        }, out GameSessionHandle handle));
        GameSessionResult result;
        try { result = await handle.Completion.WaitAsync(TimeSpan.FromMinutes(3)); }
        catch
        {
            handle.RequestStop();
            await handle.Completion.WaitAsync(TimeSpan.FromSeconds(30));
            throw;
        }
        Assert.Null(result.Error);
        Assert.Equal(GameSessionOutcome.Closed, result.Outcome);
        Assert.NotNull(probe);
        Assert.True(probe.Failure == null, probe.Failure?.ToString());
        Assert.True(probe.VerifiedFrames >= 2);
        Assert.NotNull(probe.LoadedTexture);
        Assert.True(probe.LoadedTexture.IsDisposed);
        Assert.True(probe.SessionGraphicsDevice.IsDisposed);
        Assert.Equal(nativeHostsBefore, NativeAntiMacroEditHost.ActiveHostCount);
    }

    private static RuntimeMapDefinition CreateCustomBitmapMap(string exportRoot)
    {
        using RuntimeAssetSource originalAssets = RuntimeAssetSourceFactory.OpenImgDirectory(exportRoot);
        var services = new RuntimeDataServices(originalAssets, new SourceRuntimeAssetCatalog(originalAssets));
        using var provider = new RuntimeMapProvider(services);
        using RuntimeMapDefinition original = provider.Load(10000);
        var key = new RuntimeAssetKey("Map/Obj", "codex-custom-bitmap.img/marker");
        using var bitmap = new System.Drawing.Bitmap(17, 13);
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                bitmap.SetPixel(x, y, x == 1 && y == 1 ? System.Drawing.Color.Lime : System.Drawing.Color.Magenta);
        using var authored = new WzImage("codex-custom-bitmap.img") { Parsed = true, Changed = true };
        var animation = new MapleLib.WzLib.WzProperties.WzSubProperty("marker");
        var canvas = new MapleLib.WzLib.WzProperties.WzCanvasProperty("0")
        {
            PngProperty = new MapleLib.WzLib.WzProperties.WzPngProperty { PNG = bitmap }
        };
        canvas.AddProperty(new MapleLib.WzLib.WzProperties.WzIntProperty("delay", 100));
        animation.AddProperty(canvas);
        authored.AddProperty(animation);
        using var authoringOverride = new RuntimeOwnedAssetOverride(key, authored, null);
        var info = original.CreateMapInfo();
        try
        {
            return new RuntimeMapDefinition(info, original.MapSize, original.CenterPoint, original.VirtualBounds,
                original.MinimapArea, original.MinimapPosition, original.CreateMinimapPngCopy(), original.Tiles,
                original.Objects.Append(new RuntimeObjectDefinition
                {
                    Asset = key, X = 1077, Y = 440, Layer = 0, DrawOrder = int.MaxValue,
                    ObjectSet = "codex-custom-bitmap", L0 = "marker", Name = "detached-custom-bitmap"
                }), original.Backgrounds, original.Mobs, original.Npcs, original.Reactors, original.Portals,
                original.Footholds, original.Ropes, original.Chairs, original.Tooltips, original.Misc,
                original.MirrorFields, new[] { authoringOverride });
        }
        finally { info.Image?.Dispose(); }
        // All authoring objects and the original asset source are gone before the game starts.
    }

    private sealed class CustomBitmapProbe : DrawableGameComponent
    {
        private readonly MapSimulator game;
        public CustomBitmapProbe(MapSimulator game) : base(game) { this.game = game; }
        public Exception Failure { get; private set; }
        public int VerifiedFrames { get; private set; }
        public Texture2D LoadedTexture { get; private set; }
        public GraphicsDevice SessionGraphicsDevice { get; private set; }
        public override void Update(GameTime gameTime)
        {
            if (Failure != null || VerifiedFrames >= 2) game.Exit();
        }
        public override void Draw(GameTime gameTime)
        {
            if (Failure != null || VerifiedFrames >= 2) return;
            try
            {
                SessionGraphicsDevice = game.GraphicsDevice;
                object generation = typeof(MapSimulator).GetField("_activeContent",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(game)!;
                var pool = (TexturePool)generation.GetType().GetField("SceneryTextures",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(generation)!;
                // This pool is populated by ordinary map-object loading, never by this probe.
                LoadedTexture ??= TransitionProbe.SnapshotTextures(pool).Single(texture =>
                    texture.Width == 17 && texture.Height == 13 && MatchesCustomPixels(texture));
                if (!MatchesCustomPixels(LoadedTexture))
                    throw new InvalidOperationException("The detached map-object GPU pixels changed between frames.");
                using var target = new RenderTarget2D(game.GraphicsDevice, 17, 13);
                using var batch = new SpriteBatch(game.GraphicsDevice);
                RenderTargetBinding[] priorTargets = game.GraphicsDevice.GetRenderTargets();
                try
                {
                    game.GraphicsDevice.SetRenderTarget(target);
                    game.GraphicsDevice.Clear(Color.Transparent);
                    batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp);
                    batch.Draw(LoadedTexture, Vector2.Zero, Color.White);
                    batch.End();
                }
                finally { game.GraphicsDevice.SetRenderTargets(priorTargets); }
                if (!MatchesCustomPixels(target))
                    throw new InvalidOperationException("The loaded custom object did not survive GPU render/readback.");
                VerifiedFrames++;
            }
            catch (Exception error) { Failure = error; game.Exit(); }
        }
        private static bool MatchesCustomPixels(Texture2D texture)
        {
            var pixels = new Color[17 * 13];
            texture.GetData(pixels);
            for (int index = 0; index < pixels.Length; index++)
                if (pixels[index] != (index == 18 ? Color.Lime : Color.Magenta)) return false;
            return true;
        }
    }

    private async Task<WeakReference<MapSimulator>> RunCycle(int cycle)
    {
        using RuntimeAssetSource assets = OpenAssetSource();
        var services = new RuntimeDataServices(assets, new SourceRuntimeAssetCatalog(assets));
        using var provider = new RuntimeMapProvider(services);
        using RuntimeMapDefinition initialMap = provider.Load(100000000);
        using RuntimeMapDefinition destinationMap = provider.Load(100020000);
        using var profile = new TemporaryProfileStorage();

        TransitionProbe probe = null;
        Assert.True(GameSessionHost.TryStart(() =>
        {
            var options = new GameSessionOptions(profile)
            {
                ContentRootDirectory = ResolveContentRoot()
            };
            var game = new MapSimulator(initialMap, $"MapleGame graphics lifecycle test {cycle}", options, services);
            game.SetMapProvider(provider);
            probe = new TransitionProbe(game, destinationMap);
            game.Components.Add(probe);
            return game;
        }, out GameSessionHandle handle));

        GameSessionResult result;
        try
        {
            result = await handle.Completion.WaitAsync(TimeSpan.FromMinutes(3));
        }
        catch
        {
            handle.RequestStop();
            await handle.Completion.WaitAsync(TimeSpan.FromSeconds(30));
            throw;
        }
        Assert.Null(result.Error);
        Assert.NotNull(probe);
        if (probe.Failure != null)
        {
            throw new Xunit.Sdk.XunitException($"Graphics lifecycle probe failed in cycle {cycle}: {probe.Failure}");
        }

        Assert.True(probe.FailedCandidateKeptSameGeneration);
        Assert.True(probe.FailedCandidateKeptOldTextureAlive);
        Assert.True(probe.PreparedCandidateTextureCount > 0);
        Assert.True(probe.PreparedCandidateTexturesDisposed, probe.PreparedCandidateTextureLeakReport);
        Assert.True(probe.FailedCandidateTextureDisposed);
        Assert.True(probe.InitialFramesRendered >= 1);
        Assert.True(probe.OldFramesRenderedAfterFailure >= 1);
        Assert.True(probe.PreflightFailureKeptSameGeneration);
        Assert.True(probe.PreflightFailureKeptOldTextureAlive);
        Assert.True(probe.OldFramesRenderedAfterPreflightFailure >= 1);
        Assert.True(probe.SuccessfulCandidateReplacedGeneration);
        Assert.True(probe.SuccessfulCandidateRetiredOldTexture);
        Assert.Equal(100020000, probe.ActiveMapIdAfterSuccess);
        Assert.True(probe.FramesRenderedAfterSuccess >= 2);
        Assert.True(probe.ActiveTexture.IsDisposed);
        Assert.True(probe.SessionGraphicsDevice.IsDisposed);
        return new WeakReference<MapSimulator>(probe.SessionGame);
    }

    private static int ResolveCycleCount()
    {
        return int.TryParse(Environment.GetEnvironmentVariable("MAPLEGAME_GRAPHICS_CYCLES"), out int cycles)
            ? Math.Clamp(cycles, 1, 100)
            : 1;
    }

    private static RuntimeAssetSource OpenAssetSource()
    {
        string wzDirectory = Environment.GetEnvironmentVariable("MAPLEGAME_GRAPHICS_WZ");
        if (!string.IsNullOrWhiteSpace(wzDirectory))
        {
            return RuntimeAssetSourceFactory.OpenWzDirectory(wzDirectory, WzMapleVersion.BMS);
        }

        string exportRoot = Path.Combine(
            Environment.GetEnvironmentVariable("MAPLEGAME_TEST_EXPORTS")!,
            "gms_v95");
        return RuntimeAssetSourceFactory.OpenImgDirectory(exportRoot);
    }

    private static string ResolveContentRoot()
    {
        string configured = Environment.GetEnvironmentVariable("MAPLEGAME_CONTENT_ROOT");
        if (!string.IsNullOrWhiteSpace(configured) && HasRequiredFonts(configured))
        {
            return Path.GetFullPath(configured);
        }

        string outputContent = Path.Combine(AppContext.BaseDirectory, "Content");
        if (Directory.Exists(outputContent))
        {
            return outputContent;
        }

        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string[] builtClientContentCandidates =
        {
            Path.Combine(repositoryRoot, "MapleGame.Client", "bin", "Debug", "net10.0-windows", "Content"),
            Path.Combine(repositoryRoot, "MapleGame.Client", "bin", "Release", "net10.0-windows", "Content"),
            Path.Combine(repositoryRoot, "artifacts", "client-publish-verified-win-x64", "Content")
        };
        string builtClientContent = builtClientContentCandidates.FirstOrDefault(HasRequiredFonts);
        if (builtClientContent != null)
        {
            return builtClientContent;
        }

        throw new DirectoryNotFoundException(
            "Set MAPLEGAME_CONTENT_ROOT to the built client Content directory containing the simulator XNB fonts.");
    }

    private static bool HasRequiredFonts(string directory)
    {
        return Directory.Exists(directory)
            && File.Exists(Path.Combine(directory, "XnaDefaultFont.xnb"))
            && File.Exists(Path.Combine(directory, "XnaFont_Chat.xnb"))
            && File.Exists(Path.Combine(directory, "XnaFont_Debug.xnb"));
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

    private sealed class TransitionProbe : DrawableGameComponent
    {
        private static readonly FieldInfo ActiveContentField = typeof(MapSimulator).GetField(
            "_activeContent",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static readonly FieldInfo StagingContentField = typeof(MapSimulator).GetField(
            "_stagingContent",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static readonly MethodInfo LoadMapContentMethod = typeof(MapSimulator).GetMethod(
            "LoadMapContent",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(RuntimeMapDefinition), typeof(string), typeof(string), typeof(int), typeof(string[]) },
            modifiers: null)!;

        private readonly MapSimulator game;
        private readonly RuntimeMapDefinition destination;
        private int phase;
        private object oldGeneration;
        private Texture2D oldTexture;
        private Texture2D failedCandidateTexture;
        private Texture2D[] preparedCandidateTextures = Array.Empty<Texture2D>();

        public TransitionProbe(
            MapSimulator game,
            RuntimeMapDefinition destination)
            : base(game)
        {
            this.game = game;
            this.destination = destination;
        }

        public Exception Failure { get; private set; }
        public bool FailedCandidateKeptSameGeneration { get; private set; }
        public bool FailedCandidateKeptOldTextureAlive { get; private set; }
        public int PreparedCandidateTextureCount { get; private set; }
        public bool PreparedCandidateTexturesDisposed { get; private set; }
        public string PreparedCandidateTextureLeakReport { get; private set; }
        public bool FailedCandidateTextureDisposed { get; private set; }
        public int InitialFramesRendered { get; private set; }
        public int OldFramesRenderedAfterFailure { get; private set; }
        public bool PreflightFailureKeptSameGeneration { get; private set; }
        public bool PreflightFailureKeptOldTextureAlive { get; private set; }
        public int OldFramesRenderedAfterPreflightFailure { get; private set; }
        public bool SuccessfulCandidateReplacedGeneration { get; private set; }
        public bool SuccessfulCandidateRetiredOldTexture { get; private set; }
        public int ActiveMapIdAfterSuccess { get; private set; }
        public int FramesRenderedAfterSuccess { get; private set; }
        public Texture2D ActiveTexture { get; private set; }
        public GraphicsDevice SessionGraphicsDevice { get; private set; }
        public MapSimulator SessionGame => game;

        public override void Update(GameTime gameTime)
        {
            try
            {
                if (phase == 0 && InitialFramesRendered >= 1)
                {
                    PrepareOldProbeAndRunFailure();
                    phase = 1;
                    return;
                }

                if (phase == 1 && OldFramesRenderedAfterFailure >= 1)
                {
                    RunPreflightFailure();
                    phase = 2;
                    return;
                }

                if (phase == 2 && OldFramesRenderedAfterPreflightFailure >= 1)
                {
                    RunSuccessfulTransition();
                    phase = 3;
                    return;
                }

                if (phase == 3 && FramesRenderedAfterSuccess >= 2)
                {
                    game.Exit();
                }
            }
            catch (Exception error)
            {
                Failure = error;
                game.Exit();
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (phase == 0)
            {
                InitialFramesRendered++;
            }
            else if (phase == 1)
            {
                OldFramesRenderedAfterFailure++;
            }
            else if (phase == 2)
            {
                OldFramesRenderedAfterPreflightFailure++;
            }
            else if (phase == 3)
            {
                FramesRenderedAfterSuccess++;
            }
        }

        private void PrepareOldProbeAndRunFailure()
        {
            SessionGraphicsDevice = game.GraphicsDevice;
            oldGeneration = ActiveContentField.GetValue(game)
                ?? throw new InvalidOperationException("The initial content generation was not created.");
            TexturePool oldTextures = GetGenerationField<TexturePool>(oldGeneration, "MapTextures");
            oldTexture = new Texture2D(game.GraphicsDevice, 1, 1);
            oldTexture.SetData(new[] { Color.Magenta });
            oldTextures.AddTextureToPool("graphics-lifecycle-sentinel", oldTexture);

            game.CandidatePreparationCheckpointForTesting =
                () =>
                {
                    object stagingGeneration = StagingContentField.GetValue(game)
                        ?? throw new InvalidOperationException("The candidate content generation was not staged.");
                    TexturePool stagingTextures = GetGenerationField<TexturePool>(stagingGeneration, "MapTextures");
                    preparedCandidateTextures = SnapshotTextures(stagingTextures);
                    PreparedCandidateTextureCount = preparedCandidateTextures.Length;
                    failedCandidateTexture = new Texture2D(game.GraphicsDevice, 1, 1);
                    failedCandidateTexture.SetData(new[] { Color.Yellow });
                    stagingTextures.AddTextureToPool(
                        "graphics-lifecycle-rejected-candidate-sentinel",
                        failedCandidateTexture);
                    throw new InjectedCandidatePreparationFailureException();
                };
            try
            {
                InvokeLoad(destination, "Rejected destination");
                throw new InvalidOperationException("The injected candidate preparation failure was not observed.");
            }
            catch (TargetInvocationException error) when (error.InnerException is InjectedCandidatePreparationFailureException)
            {
            }
            finally
            {
                game.CandidatePreparationCheckpointForTesting = null;
            }

            FailedCandidateKeptSameGeneration = ReferenceEquals(oldGeneration, ActiveContentField.GetValue(game));
            FailedCandidateKeptOldTextureAlive = !oldTexture.IsDisposed;
            Texture2D[] leakedPreparedTextures = preparedCandidateTextures
                .Where(texture => !texture.IsDisposed)
                .ToArray();
            PreparedCandidateTexturesDisposed = leakedPreparedTextures.Length == 0;
            PreparedCandidateTextureLeakReport = string.Join(
                Environment.NewLine,
                leakedPreparedTextures.Select(texture =>
                    $"Prepared candidate texture remained alive: name='{texture.Name ?? "<null>"}', " +
                    $"size={texture.Width}x{texture.Height}."));
            FailedCandidateTextureDisposed = failedCandidateTexture?.IsDisposed == true;
        }

        private void RunPreflightFailure()
        {
            game.CandidateActivationPreflightCheckpointForTesting =
                () => throw new InjectedActivationPreflightFailureException();
            try
            {
                InvokeLoad(destination, "Rejected activation preflight");
                throw new InvalidOperationException("The injected activation preflight failure was not observed.");
            }
            catch (TargetInvocationException error) when (error.InnerException is InjectedActivationPreflightFailureException)
            {
            }
            finally
            {
                game.CandidateActivationPreflightCheckpointForTesting = null;
            }

            PreflightFailureKeptSameGeneration = ReferenceEquals(oldGeneration, ActiveContentField.GetValue(game));
            PreflightFailureKeptOldTextureAlive = !oldTexture.IsDisposed;
        }

        private void RunSuccessfulTransition()
        {
            InvokeLoad(destination, "Accepted destination");
            object activeGeneration = ActiveContentField.GetValue(game)
                ?? throw new InvalidOperationException("The successful content generation was not activated.");
            SuccessfulCandidateReplacedGeneration = !ReferenceEquals(oldGeneration, activeGeneration);
            SuccessfulCandidateRetiredOldTexture = oldTexture.IsDisposed;
            TexturePool activeTextures = GetGenerationField<TexturePool>(activeGeneration, "MapTextures");
            ActiveTexture = new Texture2D(game.GraphicsDevice, 1, 1);
            ActiveTexture.SetData(new[] { Color.Cyan });
            activeTextures.AddTextureToPool("graphics-lifecycle-active-sentinel", ActiveTexture);
            ActiveMapIdAfterSuccess = GetGenerationField<MapleLib.WzLib.WzStructure.MapInfo>(
                activeGeneration,
                "Info").id;
        }

        private void InvokeLoad(RuntimeMapDefinition map, string title)
        {
            LoadMapContentMethod.Invoke(game, new object[] { map, title, null, -1, null });
        }

        private static T GetGenerationField<T>(object generation, string fieldName)
        {
            return (T)(generation.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(generation)
                ?? throw new InvalidOperationException($"Generation field '{fieldName}' was unavailable."));
        }

        public static Texture2D[] SnapshotTextures(TexturePool pool)
        {
            var textures = new HashSet<Texture2D>(ReferenceEqualityComparer.Instance);
            object textureEntries = typeof(TexturePool).GetField(
                "_texturePool",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pool)!;
            foreach (object entry in (System.Collections.IEnumerable)textureEntries.GetType()
                         .GetProperty("Values")!.GetValue(textureEntries)!)
            {
                Texture2D texture = (Texture2D)entry.GetType().GetProperty("Texture")!.GetValue(entry)!;
                if (texture != null)
                {
                    textures.Add(texture);
                }
            }

            object canvasTextures = typeof(TexturePool).GetField(
                "_canvasTextures",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pool)!;
            foreach (Texture2D texture in (System.Collections.IEnumerable)canvasTextures.GetType()
                         .GetProperty("Values")!.GetValue(canvasTextures)!)
            {
                if (texture != null)
                {
                    textures.Add(texture);
                }
            }

            return textures.ToArray();
        }
    }

    private sealed class CancellationBeforeCommitProbe : DrawableGameComponent
    {
        private static readonly FieldInfo ActiveContentField = typeof(MapSimulator).GetField(
            "_activeContent", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static readonly FieldInfo StagingContentField = typeof(MapSimulator).GetField(
            "_stagingContent", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static readonly MethodInfo LoadMapContentMethod = typeof(MapSimulator).GetMethod(
            "LoadMapContent", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly MapSimulator game;
        private readonly RuntimeMapDefinition destination;
        private readonly Action requestStop;
        private int framesRendered;
        private bool attempted;

        public CancellationBeforeCommitProbe(MapSimulator game, RuntimeMapDefinition destination, Action requestStop)
            : base(game)
        {
            this.game = game;
            this.destination = destination;
            this.requestStop = requestStop;
        }

        public bool CancellationRequestedAfterPreparation { get; private set; }
        public bool CancellationObserved { get; private set; }
        public bool ActivationReached { get; private set; }
        public bool PriorGenerationPreservedAtRejection { get; private set; }
        public bool PriorTextureAliveAtRejection { get; private set; }
        public bool CandidateTexturesDisposedAtRejection { get; private set; }
        public Texture2D[] CandidateTextures { get; private set; } = Array.Empty<Texture2D>();
        public GraphicsDevice SessionGraphicsDevice { get; private set; }

        public override void Update(GameTime gameTime)
        {
            if (attempted || framesRendered < 1) return;
            attempted = true;
            SessionGraphicsDevice = game.GraphicsDevice;
            object priorGeneration = ActiveContentField.GetValue(game)!;
            TexturePool priorTextures = GetPool(priorGeneration);
            var priorTexture = new Texture2D(game.GraphicsDevice, 1, 1);
            priorTexture.SetData(new[] { Color.Magenta });
            priorTextures.AddTextureToPool("cancel-before-commit-prior", priorTexture);
            game.CandidateActivationCheckpointForTesting = () => ActivationReached = true;
            game.CandidatePreparationCheckpointForTesting = () =>
            {
                object candidate = StagingContentField.GetValue(game)!;
                TexturePool candidatePool = GetPool(candidate);
                CandidateTextures = TransitionProbe.SnapshotTextures(candidatePool);
                if (CandidateTextures.Length == 0)
                    throw new InvalidOperationException("Cancellation probe requires loaded destination textures.");
                CancellationRequestedAfterPreparation = true;
                requestStop();
            };
            try
            {
                LoadMapContentMethod.Invoke(game, new object[] { destination, "Cancelled destination", null, -1, null });
                throw new InvalidOperationException("Cancelled destination unexpectedly committed.");
            }
            catch (TargetInvocationException error) when (error.InnerException is OperationCanceledException)
            {
                CancellationObserved = true;
                PriorGenerationPreservedAtRejection = ReferenceEquals(priorGeneration, ActiveContentField.GetValue(game));
                PriorTextureAliveAtRejection = !priorTexture.IsDisposed;
                CandidateTexturesDisposedAtRejection = CandidateTextures.All(texture => texture.IsDisposed);
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
            finally
            {
                game.CandidatePreparationCheckpointForTesting = null;
                game.CandidateActivationCheckpointForTesting = null;
            }
        }

        private static TexturePool GetPool(object generation) => (TexturePool)generation.GetType()
            .GetField("MapTextures", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(generation)!;

        public override void Draw(GameTime gameTime) => framesRendered++;
    }

    private sealed class FatalActivationProbe : DrawableGameComponent
    {
        private static readonly FieldInfo ActiveContentField = typeof(MapSimulator).GetField(
            "_activeContent",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static readonly MethodInfo LoadMapContentMethod = typeof(MapSimulator).GetMethod(
            "LoadMapContent",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(RuntimeMapDefinition), typeof(string), typeof(string), typeof(int), typeof(string[]) },
            modifiers: null)!;

        private readonly MapSimulator game;
        private readonly RuntimeMapDefinition destination;
        private int framesRendered;

        public FatalActivationProbe(MapSimulator game, RuntimeMapDefinition destination)
            : base(game)
        {
            this.game = game;
            this.destination = destination;
        }

        public bool ActivationCheckpointReached { get; private set; }
        public bool CommittedGenerationReplacedOld { get; private set; }
        public Texture2D CommittedTexture { get; private set; }

        public override void Update(GameTime gameTime)
        {
            if (framesRendered < 1)
            {
                return;
            }

            object oldGeneration = ActiveContentField.GetValue(game)!;
            game.CandidateActivationCheckpointForTesting = () =>
            {
                ActivationCheckpointReached = true;
                object committedGeneration = ActiveContentField.GetValue(game)!;
                CommittedGenerationReplacedOld = !ReferenceEquals(oldGeneration, committedGeneration);
                TexturePool textures = (TexturePool)committedGeneration.GetType().GetField(
                    "MapTextures",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(committedGeneration)!;
                CommittedTexture = new Texture2D(game.GraphicsDevice, 1, 1);
                CommittedTexture.SetData(new[] { Color.Red });
                textures.AddTextureToPool("graphics-lifecycle-fatal-activation-sentinel", CommittedTexture);
                throw new InjectedMandatoryActivationFailureException();
            };

            LoadMapContentMethod.Invoke(
                game,
                new object[] { destination, "Fatal destination", null, -1, null });
        }

        public override void Draw(GameTime gameTime)
        {
            framesRendered++;
        }
    }

    private sealed class InjectedCandidatePreparationFailureException : Exception;
    private sealed class InjectedActivationPreflightFailureException : Exception;
    private sealed class InjectedMandatoryActivationFailureException : Exception;

    private static string FlattenExceptionMessages(Exception error)
    {
        var messages = new List<string>();
        for (Exception current = error; current != null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }
        return string.Join(Environment.NewLine, messages);
    }

    private sealed class ImmediateSession : IGameSession
    {
        public void Run(CancellationToken cancellationToken) { }
        public void Dispose() { }
    }

    private sealed class TemporaryProfileStorage : ISimulatorProfileStorage, IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(),
            "MapleGameGraphicsLifecycle-" + Guid.NewGuid().ToString("N"));

        public string CharactersDirectory
        {
            get
            {
                string directory = Path.Combine(root, "Characters");
                Directory.CreateDirectory(directory);
                return directory;
            }
        }

        public string GetFile(string fileName) => Path.Combine(root, fileName);

        public void Dispose()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class GraphicsFactAttribute : FactAttribute
    {
        public GraphicsFactAttribute()
        {
            string wzDirectory = Environment.GetEnvironmentVariable("MAPLEGAME_GRAPHICS_WZ");
            string exports = Environment.GetEnvironmentVariable("MAPLEGAME_TEST_EXPORTS");
            string v95 = string.IsNullOrWhiteSpace(exports) ? null : Path.Combine(exports, "gms_v95");
            if (!string.Equals(Environment.GetEnvironmentVariable("MAPLEGAME_GRAPHICS_TESTS"), "1", StringComparison.Ordinal)
                || (!Directory.Exists(wzDirectory) && !Directory.Exists(v95)))
            {
                Skip = "Set MAPLEGAME_GRAPHICS_TESTS=1 and either MAPLEGAME_GRAPHICS_WZ or MAPLEGAME_TEST_EXPORTS with a gms_v95 export to run graphics lifecycle tests.";
            }
        }
    }
}
