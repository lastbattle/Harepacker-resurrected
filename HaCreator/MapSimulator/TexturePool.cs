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
    /// An instance of texture is created per Map Simulator render.
    /// 
    /// TODO: A more efficient way of releasing resources when its not used for some time
    /// </summary>
    public class TexturePool : IDisposable
    {
        private readonly Dictionary<string, Texture2D> TEXTURE_POOL = new Dictionary<string, Texture2D>();

        /// <summary>
        /// Get the previously loaded texture from the pool
        /// </summary>
        /// <param name="wzpath"></param>
        /// <returns></returns>
        public Texture2D GetTexture(string wzpath)
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
        public void AddTextureToPool(string wzpath, Texture2D texture)
        {
            if (!TEXTURE_POOL.ContainsKey(wzpath))
            {
                lock (TEXTURE_POOL)
                    if (!TEXTURE_POOL.ContainsKey(wzpath)) // check again
                    {
                        TEXTURE_POOL.Add(wzpath, texture);
                    }
            }
        }

        public void Dispose()
        {
            lock (TEXTURE_POOL)
                TEXTURE_POOL.Clear();
        }

        /// <summary>
        /// Disposes all textures in the pool and clears the pool.
        /// Used for seamless map transitions.
        /// </summary>
        public void DisposeAll()
        {
            lock (TEXTURE_POOL)
            {
                foreach (var texture in TEXTURE_POOL.Values)
                {
                    texture?.Dispose();
                }
                TEXTURE_POOL.Clear();
            }
        }
    }
}
