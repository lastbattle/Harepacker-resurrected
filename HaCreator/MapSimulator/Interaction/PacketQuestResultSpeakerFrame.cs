using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class PacketQuestResultSpeakerFrame
    {
        internal PacketQuestResultSpeakerFrame(Texture2D texture, Point origin, int delayMs)
        {
            Texture = texture;
            Origin = origin;
            DelayMs = delayMs;
        }

        internal Texture2D Texture { get; }
        internal Point Origin { get; }
        internal int DelayMs { get; }
    }
}
