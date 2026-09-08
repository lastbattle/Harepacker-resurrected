using HaCreator.MapSimulator.Contracts;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.IO;

namespace UnitTest_MapleGame;

[Collection("Game session host")]
public class RuntimeAssemblyBoundaryTests
{
    [Fact]
    public void RuntimeOutputHasNoEditorAssemblyReferences()
    {
        Assembly runtime = typeof(GameSessionHost).Assembly;
        Assert.Equal("MapleGame.Runtime", runtime.GetName().Name);
        var assemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Inspect emitted metadata without executing package code or trying to load
        // optional framework references (e.g. legacy ADO.NET provider branches).
        foreach (string file in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll", SearchOption.AllDirectories))
        {
            using var stream = File.OpenRead(file);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata)
                continue;
            MetadataReader metadata = pe.GetMetadataReader();
            if (!metadata.IsAssembly)
                continue;
            string name = metadata.GetString(metadata.GetAssemblyDefinition().Name);
            assemblies.Add(name);
            AssertNotEditor(name);
            foreach (AssemblyReferenceHandle reference in metadata.AssemblyReferences)
                AssertNotEditor(metadata.GetString(metadata.GetAssemblyReference(reference).Name));
        }
        Assert.Contains("HaSharedLibrary", assemblies);
        Assert.Contains("MapleLib", assemblies);
        Assert.Contains("LZ4", assemblies);
    }

    private static void AssertNotEditor(string name) =>
        Assert.DoesNotContain(name, new[] { "WvsMaps", "WvsWzImg", "HaCreator", "HaRepacker" });

    [Fact]
    public async Task RuntimeHostConstructsRunsAndDisposesWithoutEditorAssembly()
    {
        var session = new ProbeSession();
        Assert.True(GameSessionHost.TryStart(() => session, out GameSessionHandle handle));
        GameSessionResult result = await handle.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(GameSessionOutcome.Closed, result.Outcome);
        Assert.True(session.Disposed);
        Assert.Equal(ApartmentState.STA, session.Apartment);
    }

    private sealed class ProbeSession : IGameSession
    {
        public ApartmentState Apartment { get; private set; }
        public bool Disposed { get; private set; }
        public void Run(CancellationToken cancellationToken) => Apartment = Thread.CurrentThread.GetApartmentState();
        public void Dispose() => Disposed = true;
    }
}
