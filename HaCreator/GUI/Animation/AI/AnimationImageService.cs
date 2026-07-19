#nullable enable

using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.GUI.FrameAnimation.AI
{
    public sealed class AnimationImageResult : IDisposable
    {
        private readonly ProcessedAnimationImage processed;

        internal AnimationImageResult(ProcessedAnimationImage processed, GeneratedAnimationImage generated)
        {
            this.processed = processed ?? throw new ArgumentNullException(nameof(processed));
            RevisedPrompt = generated?.RevisedPrompt ?? string.Empty;
            SourceUri = generated?.SourceUri;
        }

        public Bitmap Bitmap => processed.Bitmap;
        public Point Origin => processed.Origin;
        public Rectangle OriginalAlphaBounds => processed.OriginalAlphaBounds;
        public string RevisedPrompt { get; }
        public Uri? SourceUri { get; }
        public void Dispose() => processed.Dispose();
    }

    /// <summary>
    /// End-to-end, UI-independent generation pipeline: call the image endpoint, decode the
    /// returned bitmap, remove its edge matte, trim it, and align its origin to a reference frame.
    /// </summary>
    public sealed class AnimationImageService : IDisposable
    {
        private readonly OpenAICompatibleImageClient client;
        private readonly bool ownsClient;

        public AnimationImageService(OpenAICompatibleImageClient? client = null)
        {
            this.client = client ?? new OpenAICompatibleImageClient(OpenAICompatibleImageOptions.FromSettings());
            ownsClient = client == null;
        }

        public async Task<AnimationImageResult> GenerateAsync(AnimationImageGenerationRequest request,
            Bitmap? reference = null, Point? referenceOrigin = null,
            AnimationImageProcessingOptions? processingOptions = null,
            CancellationToken cancellationToken = default)
        {
            GeneratedAnimationImage generated = await client.GenerateAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return Process(generated, reference, referenceOrigin, processingOptions);
        }

        public async Task<AnimationImageResult> EditAsync(AnimationImageEditRequest request,
            Bitmap? reference = null, Point? referenceOrigin = null,
            AnimationImageProcessingOptions? processingOptions = null,
            CancellationToken cancellationToken = default)
        {
            GeneratedAnimationImage generated = await client.EditAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return Process(generated, reference, referenceOrigin, processingOptions);
        }

        private static AnimationImageResult Process(GeneratedAnimationImage generated, Bitmap? reference,
            Point? referenceOrigin, AnimationImageProcessingOptions? options)
        {
            try
            {
                using var stream = new MemoryStream(generated.Data, writable: false);
                using var decoded = new Bitmap(stream);
                ProcessedAnimationImage processed = AnimationImageProcessor.Process(
                    decoded, reference, referenceOrigin, options);
                return new AnimationImageResult(processed, generated);
            }
            catch (ArgumentException ex)
            {
                throw new OpenAICompatibleImageApiException(
                    "The image endpoint returned data that is not a supported image.", innerException: ex);
            }
        }

        public void Dispose()
        {
            if (ownsClient)
                client.Dispose();
        }
    }
}
