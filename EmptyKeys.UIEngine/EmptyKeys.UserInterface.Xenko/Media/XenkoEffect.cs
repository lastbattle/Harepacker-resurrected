using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xenko.Graphics;
using Xenko.Rendering;

namespace EmptyKeys.UserInterface.Media
{
    /// <summary>
    /// Implements Xenko specific effect class
    /// </summary>
    /// <seealso cref="EmptyKeys.UserInterface.Media.EffectBase" />
    public class XenkoEffect : EffectBase
    {
        private Effect effect;
        private EffectInstance instance;

        /// <summary>
        /// Gets the parameters.
        /// </summary>
        /// <value>
        /// The parameters.
        /// </value>
        public ParameterCollection Parameters { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="XenkoEffect"/> class.
        /// </summary>
        /// <param name="nativeEffect">The native effect.</param>
        /// <param name="parameters">The parameters.</param>
        public XenkoEffect(object nativeEffect, ParameterCollection parameters) : base(nativeEffect)
        {
            Parameters = parameters;
            if (Parameters == null)
            {
                Parameters = new ParameterCollection();
            }

            effect = nativeEffect as Effect;
            if (effect != null)
            {
                instance = new EffectInstance(effect, Parameters);
            }
        }

        public override object GetNativeEffect()
        {
            return instance;
        }

        public override void UpdateEffectParameters(params object[] parameterValues)
        {
            if (parameterValues == null)
            {
                return;
            }

            if (effect.Name == "DirectionalBlurShader")
            {
                float angle = (float)parameterValues[0];
                instance.Parameters.Set(DirectionalBlurShaderKeys.Angle, angle);
                float blurAmount = (float)parameterValues[1];
                instance.Parameters.Set(DirectionalBlurShaderKeys.BlurAmount, blurAmount);
            }
        }
    }
}
