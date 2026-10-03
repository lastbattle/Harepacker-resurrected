using System;

namespace HaCreator.MapSimulator.Hosting;

internal static class ContentGenerationTransaction
{
    internal static void Reject<T>(T candidate)
        where T : class, IDisposable
    {
        candidate?.Dispose();
    }
}
