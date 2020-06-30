using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;

namespace EmptyKeys.UserInterface.Media
{
    /// <summary>
    /// Implements MonoGame specific effect
    /// </summary>
    /// <seealso cref="EmptyKeys.UserInterface.Media.EffectBase" />
    public class MonoGameEffect : EffectBase
    {
        private Effect effect;

        /// <summary>
        /// Initializes a new instance of the <see cref="MonoGameEffect"/> class.
        /// </summary>
        /// <param name="nativeEffect">The native effect.</param>
        public MonoGameEffect(object nativeEffect) : base(nativeEffect)
        {
            effect = nativeEffect as Effect;            
        }

        /// <summary>
        /// Gets the native effect.
        /// </summary>
        /// <returns></returns>
        public override object GetNativeEffect()
        {
            return effect;
        }

        /// <summary>
        /// Updates the effect parameters.
        /// </summary>
        /// <param name="parameterValues">The parameter values.</param>
        public override void UpdateEffectParameters(params object[] parameterValues)
        {
            if (parameterValues == null)
            {
                return;
            }

            if (effect.Name == "DirectionalBlurShader")
            {
                float angle = (float)parameterValues[0];
                effect.Parameters["Angle"].SetValue(angle);
                float blurAmount = (float)parameterValues[1];
                effect.Parameters["BlurAmount"].SetValue(blurAmount);                
            }
        }
    }
}
