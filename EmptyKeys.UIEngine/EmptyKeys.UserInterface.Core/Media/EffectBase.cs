using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EmptyKeys.UserInterface.Media
{
    /// <summary>
    /// Implements abstract Effect
    /// </summary>
    public abstract class EffectBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EffectBase"/> class.
        /// </summary>
        /// <param name="nativeEffect">The native effect.</param>
        public EffectBase(object nativeEffect)
        {
        }

        /// <summary>
        /// Gets the native effect.
        /// </summary>
        /// <returns></returns>
        public abstract object GetNativeEffect();

        /// <summary>
        /// Updates the effect parameters.
        /// </summary>
        /// <param name="parameters">The parameters.</param>
        public abstract void UpdateEffectParameters(params object[] parameters);
    }
}
