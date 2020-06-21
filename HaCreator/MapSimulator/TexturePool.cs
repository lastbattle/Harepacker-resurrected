using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator
{

    /// <summary>
    /// Pool of shared textures
    /// TODO: A more efficient way of releasing resources when its not used for some time
    /// </summary>
    public class TexturePool
    {
        private static Dictionary<string, Texture2D> TEXTURE_POOL = new Dictionary<string, Texture2D>();

        /// <summary>
        /// Get the previously loaded texture from the pool
        /// </summary>
        /// <param name="wzpath"></param>
        /// <returns></returns>
        public static Texture2D GetTexture(string wzpath)
        {
            if (TEXTURE_POOL.ContainsKey(wzpath))
                return TEXTURE_POOL[wzpath];

            return null;
        }

        /// <summary>
        /// Adds the loaded texture to the cache pool
        /// </summary>
        /// <param name="wzpath"></param>
        /// <param name="texture"></param>
        public static void AddTextureToPool(string wzpath, Texture2D texture)
        {
            if (!TEXTURE_POOL.ContainsKey(wzpath))
                TEXTURE_POOL.Add(wzpath, texture);
        }
    }
}
