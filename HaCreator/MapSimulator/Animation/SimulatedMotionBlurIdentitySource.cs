using System.Threading;

namespace HaCreator.MapSimulator.Animation
{
    internal static class SimulatedMotionBlurIdentitySource
    {
        private static int _animationStateIdSource;
        private static int _layerHandleIdSource;

        public static int NextAnimationStateId()
        {
            int next = Interlocked.Increment(ref _animationStateIdSource);
            if (next > 0)
            {
                return next;
            }

            Interlocked.Exchange(ref _animationStateIdSource, 1);
            return 1;
        }

        public static int NextLayerHandleId()
        {
            int next = Interlocked.Increment(ref _layerHandleIdSource);
            if (next > 0)
            {
                return next;
            }

            Interlocked.Exchange(ref _layerHandleIdSource, 1);
            return 1;
        }
    }
}
