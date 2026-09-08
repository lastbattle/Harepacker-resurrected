using System;

namespace HaCreator.MapSimulator.Hosting;

internal static class ContentGenerationTransaction
{
    internal static void Commit<T>(
        ref T active,
        T candidate,
        Action activate,
        Action<Exception> retirementFailure = null)
        where T : class, IDisposable
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(activate);

        T previous = active;
        active = candidate;
        try
        {
            activate();
        }
        catch (Exception activationError)
        {
            active = previous;
            try
            {
                candidate.Dispose();
            }
            catch (Exception candidateCleanupError)
            {
                throw new AggregateException(
                    "Content activation failed and candidate cleanup also failed.",
                    activationError,
                    candidateCleanupError);
            }

            throw;
        }

        try
        {
            previous?.Dispose();
        }
        catch (Exception retirementError)
        {
            try
            {
                retirementFailure?.Invoke(retirementError);
            }
            catch
            {
                // A diagnostic sink must not turn successful activation into a failed transaction.
            }
        }
    }

    internal static void Reject<T>(T candidate)
        where T : class, IDisposable
    {
        candidate?.Dispose();
    }
}
