namespace HaCreator.MapSimulator.Animation
{
    /// <summary>
    /// Stores animation frames for different NPC actions (stand, speak, blink, etc.)
    /// Extends AnimationSetBase with NPC-specific functionality.
    /// </summary>
    public class NpcAnimationSet : AnimationSetBase
    {
        public int ClientActionSetIndex { get; set; } = -1;

        public bool IsHiddenToLocalUser { get; set; }
    }
}
