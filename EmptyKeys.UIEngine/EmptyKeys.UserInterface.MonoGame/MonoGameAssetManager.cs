using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EmptyKeys.UserInterface.Media;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace EmptyKeys.UserInterface
{
    /// <summary>
    /// Implements MonoGame specific asset manager
    /// </summary>
    public class MonoGameAssetManager : AssetManager
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MonoGameAssetManager"/> class.
        /// </summary>
        public MonoGameAssetManager()
            : base()
        {
        }

        /// <summary>
        /// Loads the texture.
        /// </summary>
        /// <param name="contentManager">The content manager.</param>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        public override TextureBase LoadTexture(object contentManager, string file)
        {
            ContentManager database = contentManager as ContentManager;
            Texture2D native = database.Load<Texture2D>(file);
            return Engine.Instance.Renderer.CreateTexture(native);
        }

        /// <summary>
        /// Loads the font.
        /// </summary>
        /// <param name="contentManager">The content manager.</param>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        public override FontBase LoadFont(object contentManager, string file)
        {
            ContentManager database = contentManager as ContentManager;
            SpriteFont native = database.Load<SpriteFont>(file);
            return Engine.Instance.Renderer.CreateFont(native);
        }

        /// <summary>
        /// Loads the sound.
        /// </summary>
        /// <param name="contentManager">The content manager.</param>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        public override SoundBase LoadSound(object contentManager, string file)
        {
            ContentManager database = contentManager as ContentManager;
            SoundEffect native = database.Load<SoundEffect>(file);
            return Engine.Instance.AudioDevice.CreateSound(native);
        }

        /// <summary>
        /// Loads the effect.
        /// </summary>
        /// <param name="contentManager">The content manager.</param>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        public override EffectBase LoadEffect(object contentManager, string file)
        {
            ContentManager database = contentManager as ContentManager;
            Effect native = null;
            try
            {
                native = database.Load<Effect>(file);
            }
            catch (Exception)
            {
                // some built-in effects are not implemented yet
            }

            return Engine.Instance.Renderer.CreateEffect(native);
        }
    }
}
