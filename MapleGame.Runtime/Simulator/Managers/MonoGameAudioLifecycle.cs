using System;
using System.Reflection;
using Microsoft.Xna.Framework.Audio;

namespace HaCreator.MapSimulator.Managers;

/// <summary>Repairs the WindowsDX 3.8.4.1 audio state after Game.Dispose.</summary>
internal static class MonoGameAudioLifecycle
{
    private static readonly object Gate = new();
    private static readonly FieldInfo SystemState = typeof(SoundEffect).GetField(
        "_systemState", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly PropertyInfo Device = typeof(SoundEffect).GetProperty(
        "Device", BindingFlags.Static | BindingFlags.NonPublic);

    internal static void EnsureInitialized()
    {
        lock (Gate)
        {
            // The pinned MonoGame Game.Dispose destroys XAudio's device but leaves
            // _systemState Initialized. Its public Initialize then does nothing.
            // Only repair that specific shutdown state; never replace a live device
            // or retry an explicitly failed initialization. Session hosts serialize
            // Game lifetimes, and dispose all session voices before Game.Dispose.
            if (SystemState != null && Device != null
                && Device.GetValue(null) == null
                && string.Equals(SystemState.GetValue(null)?.ToString(), "Initialized", StringComparison.Ordinal))
            {
                SystemState.SetValue(null, Enum.Parse(SystemState.FieldType, "NotInitialized"));
            }

            SoundEffect.Initialize();
        }
    }
}
