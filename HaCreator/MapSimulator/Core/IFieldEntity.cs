using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using System;

namespace HaCreator.MapSimulator.Core
{
    /// <summary>
    /// Interface for field entities (Mobs, NPCs, etc.) that have common behaviors.
    /// Defines the contract for entities that can move, animate, and interact with the game world.
    /// </summary>
    public interface IFieldEntity
    {
        /// <summary>
        /// Gets the current X position of the entity (considering movement)
        /// </summary>
        int CurrentX { get; }

        /// <summary>
        /// Gets the current Y position of the entity (considering movement)
        /// </summary>
        int CurrentY { get; }

        /// <summary>
        /// Gets the current animation action being played
        /// </summary>
        string CurrentAction { get; }

        /// <summary>
        /// Whether movement is enabled for this entity
        /// </summary>
        bool MovementEnabled { get; set; }

        /// <summary>
        /// Gets the cached mirror boundary for this entity
        /// </summary>
        ReflectionDrawableBoundary CachedMirrorBoundary { get; }

        /// <summary>
        /// Gets the current animation frame
        /// </summary>
        IDXObject GetCurrentFrame();

        /// <summary>
        /// Sets the current animation action
        /// </summary>
        void SetAction(string action);

        /// <summary>
        /// Updates the cached mirror boundary if the entity has moved significantly
        /// </summary>
        void UpdateMirrorBoundary(Rectangle mirrorBottomRect, ReflectionDrawableBoundary mirrorBottomReflection,
            Func<int, int, ReflectionDrawableBoundary> checkMirrorFieldData);
    }

    /// <summary>
    /// Extended interface for combat-capable entities (Mobs)
    /// </summary>
    public interface ICombatEntity : IFieldEntity
    {
        /// <summary>
        /// Whether the entity's AI is enabled
        /// </summary>
        bool AIEnabled { get; set; }

        /// <summary>
        /// Whether the death animation has completed
        /// </summary>
        bool IsDeathAnimationComplete { get; }

        /// <summary>
        /// Apply damage to this entity
        /// </summary>
        /// <param name="damage">Amount of damage</param>
        /// <param name="currentTick">Current game tick</param>
        /// <param name="isCritical">Whether this is a critical hit</param>
        /// <returns>True if the entity died from this damage</returns>
        bool ApplyDamage(int damage, int currentTick, bool isCritical = false);
    }

    /// <summary>
    /// Extended interface for interactive entities (NPCs)
    /// </summary>
    public interface IInteractiveEntity : IFieldEntity
    {
        /// <summary>
        /// Check if a map point is within the entity's interaction bounds
        /// </summary>
        bool ContainsMapPoint(int mapX, int mapY);
    }
}
