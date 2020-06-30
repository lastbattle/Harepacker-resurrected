using EmptyKeys.UserInterface.Input;
using EmptyKeys.UserInterface.Media;
using EmptyKeys.UserInterface.Renderers;

namespace EmptyKeys.UserInterface
{
    /// <summary>
    /// Implements abstract Engine
    /// </summary>
    public abstract class Engine
    {
        /// <summary>
        /// The instance
        /// </summary>
        protected static Engine instance;

        /// <summary>
        /// Gets the instance.
        /// </summary>
        /// <value>
        /// The instance.
        /// </value>
        public static Engine Instance
        {
            get
            {
                return instance;
            }
        }

        /// <summary>
        /// Gets the renderer.
        /// </summary>
        /// <value>
        /// The renderer.
        /// </value>
        public abstract Renderer Renderer { get; }

        /// <summary>
        /// Gets the audio device.
        /// </summary>
        /// <value>
        /// The audio device.
        /// </value>
        public abstract AudioDevice AudioDevice { get; }

        /// <summary>
        /// Gets the asset manager.
        /// </summary>
        /// <value>
        /// The asset manager.
        /// </value>
        public abstract AssetManager AssetManager { get; }

        /// <summary>
        /// Gets the input device.
        /// </summary>
        /// <value>
        /// The input device.
        /// </value>
        public abstract InputDeviceBase InputDevice { get; }        

        /// <summary>
        /// Initializes a new instance of the <see cref="Engine"/> class.
        /// </summary>
        public Engine()
        {
            instance = this;
        }
    }
}
