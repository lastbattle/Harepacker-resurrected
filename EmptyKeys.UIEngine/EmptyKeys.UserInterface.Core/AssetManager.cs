using EmptyKeys.UserInterface.Media;

namespace EmptyKeys.UserInterface
{
    /// <summary>
    /// Implements abstract Asset manager
    /// </summary>
    public abstract class AssetManager
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AssetManager"/> class.
        /// </summary>
        public AssetManager()
        {
        }

        /// <summary>
        /// Loads the texture.
        /// </summary>
        /// <param name="contentManager">The content manager.</param>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        public abstract TextureBase LoadTexture(object contentManager, string file);

        /// <summary>
        /// Loads the font.
        /// </summary>
        /// <param name="contentManager">The content manager.</param>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        public abstract FontBase LoadFont(object contentManager, string file);

        /// <summary>
        /// Loads the sound.
        /// </summary>
        /// <param name="contentManager">The content manager.</param>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        public abstract SoundBase LoadSound(object contentManager, string file);

        /// <summary>
        /// Loads the effect.
        /// </summary>
        /// <param name="contentManager">The content manager.</param>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        public abstract EffectBase LoadEffect(object contentManager, string file);
    }
}
