using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Pools;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Companions
{
    public sealed class PetRuntime
    {
        private const float FollowSpeed = 220f;
        private const float FollowSpacing = 28f;
        private const float MultiPetSpacing = 18f;
        private const float SnapDistance = 220f;

        private readonly AnimationController _animation;

        internal PetRuntime(int runtimeId, int slotIndex, PetDefinition definition)
        {
            RuntimeId = runtimeId;
            SlotIndex = slotIndex;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _animation = new AnimationController(definition.Animations, "stand1");
        }

        public int RuntimeId { get; }
        public int SlotIndex { get; internal set; }
        public PetDefinition Definition { get; }
        public bool AutoLootEnabled { get; set; } = true;
        public float X { get; private set; }
        public float Y { get; private set; }
        public bool FacingRight { get; private set; } = true;
        public string CurrentAction => _animation.CurrentAction;
        public int ItemId => Definition.ItemId;
        public string Name => Definition.Name;

        internal void SetPosition(float x, float y, bool facingRight)
        {
            X = x;
            Y = y;
            FacingRight = facingRight;
        }

        internal void Update(PlayerCharacter owner, DropPool dropPool, int ownerId, bool pickupAllowed, int currentTime, float deltaTime)
        {
            if (owner == null)
            {
                return;
            }

            FacingRight = owner.FacingRight;
            Vector2 followTarget = GetFollowTarget(owner);
            Vector2 desiredTarget = followTarget;
            float moveSpeed = FollowSpeed;
            bool chasingDrop = false;

            if (dropPool != null)
            {
                if (pickupAllowed && AutoLootEnabled && owner.State != PlayerState.Ladder && owner.State != PlayerState.Rope)
                {
                    PetDropTarget target = dropPool.UpdateChasingDropForPet(
                        RuntimeId,
                        X,
                        Y,
                        ownerId,
                        owner.X,
                        owner.Y,
                        currentTime,
                        deltaTime);

                    if (target != null)
                    {
                        desiredTarget = new Vector2(target.TargetX, target.TargetY);
                        moveSpeed = target.ChaseSpeed;
                        chasingDrop = target.IsChasing;

                        dropPool.TryPickUpDropByPet(RuntimeId, X, Y, ownerId, currentTime);
                    }
                }
                else
                {
                    dropPool.ClearPetTarget(RuntimeId);
                }
            }

            MoveTowards(desiredTarget, moveSpeed, deltaTime, followTarget);
            UpdateAction(owner, chasingDrop, desiredTarget);
            _animation.UpdateFrame(currentTime);
        }

        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer,
            int mapShiftX, int mapShiftY, int centerX, int centerY)
        {
            IDXObject frame = _animation.GetCurrentFrame();
            if (frame == null)
            {
                return;
            }

            int screenX = (int)X - mapShiftX + centerX;
            int screenY = (int)Y - mapShiftY + centerY;
            frame.DrawBackground(spriteBatch, skeletonRenderer, null, screenX, screenY, Color.White, !FacingRight, null);
        }

        private Vector2 GetFollowTarget(PlayerCharacter owner)
        {
            float direction = owner.FacingRight ? -1f : 1f;
            float offsetX = direction * (FollowSpacing + SlotIndex * MultiPetSpacing);
            return new Vector2(owner.X + offsetX, owner.Y);
        }

        private void MoveTowards(Vector2 desiredTarget, float moveSpeed, float deltaTime, Vector2 followTarget)
        {
            var current = new Vector2(X, Y);
            Vector2 toTarget = desiredTarget - current;
            float distance = toTarget.Length();

            if (distance > SnapDistance)
            {
                X = followTarget.X;
                Y = followTarget.Y;
                return;
            }

            if (distance <= 0.01f)
            {
                X = desiredTarget.X;
                Y = desiredTarget.Y;
                return;
            }

            float maxStep = moveSpeed * Math.Max(deltaTime, 0.001f);
            Vector2 step = distance <= maxStep
                ? toTarget
                : Vector2.Normalize(toTarget) * maxStep;

            X += step.X;
            Y += step.Y;
        }

        private void UpdateAction(PlayerCharacter owner, bool chasingDrop, Vector2 desiredTarget)
        {
            string action = "stand1";
            float deltaX = desiredTarget.X - X;
            float deltaY = desiredTarget.Y - Y;

            if (owner.State == PlayerState.Ladder || owner.State == PlayerState.Rope)
            {
                action = "hang";
            }
            else if (owner.State == PlayerState.Jumping || owner.State == PlayerState.Falling || owner.State == PlayerState.Flying || Math.Abs(deltaY) > 12f)
            {
                action = "fly";
            }
            else if (chasingDrop || Math.Abs(deltaX) > 6f)
            {
                action = "move";
            }

            _animation.SetAction(action);
        }
    }

    public sealed class PetController
    {
        private const int MaxPets = 3;
        private const int DefaultPetItemId = 5000000;
        private const int PickupForbiddenMapId = 209080000;

        private readonly PetLoader _loader;
        private readonly List<PetRuntime> _activePets = new();
        private int _nextRuntimeId = 1;
        private Func<int> _currentMapIdProvider;

        public PetController(GraphicsDevice device)
        {
            _loader = new PetLoader(device);
        }

        public IReadOnlyList<PetRuntime> ActivePets => _activePets;

        public void SetCurrentMapIdProvider(Func<int> currentMapIdProvider)
        {
            _currentMapIdProvider = currentMapIdProvider;
        }

        public void EnsureDefaultPetActive(PlayerCharacter owner)
        {
            if (_activePets.Count > 0)
            {
                if (owner != null)
                {
                    SyncPositionsToOwner(owner);
                }

                return;
            }

            SetActivePet(0, DefaultPetItemId, owner);
        }

        public bool SetActivePet(int slotIndex, int petItemId, PlayerCharacter owner = null)
        {
            if (slotIndex < 0 || slotIndex >= MaxPets)
            {
                return false;
            }

            PetDefinition definition = _loader.Load(petItemId);
            if (definition == null)
            {
                return false;
            }

            int insertIndex = Math.Min(slotIndex, _activePets.Count);
            _activePets.Insert(insertIndex, new PetRuntime(_nextRuntimeId++, insertIndex, definition));
            if (_activePets.Count > MaxPets)
            {
                _activePets.RemoveAt(_activePets.Count - 1);
            }

            ReindexPets(owner);
            return true;
        }

        public void RemovePetAt(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _activePets.Count)
            {
                return;
            }

            _activePets.RemoveAt(slotIndex);
            ReindexPets();
        }

        public void SetAutoLootEnabled(int slotIndex, bool enabled)
        {
            if (slotIndex < 0 || slotIndex >= _activePets.Count)
            {
                return;
            }

            _activePets[slotIndex].AutoLootEnabled = enabled;
        }

        public void Update(PlayerCharacter owner, DropPool dropPool, int currentTime, float deltaTime)
        {
            if (owner == null || !owner.IsAlive || _activePets.Count == 0)
            {
                return;
            }

            bool pickupAllowed = (_currentMapIdProvider?.Invoke() ?? -1) != PickupForbiddenMapId;
            int ownerId = owner.Build?.Id ?? 1;

            for (int i = 0; i < _activePets.Count; i++)
            {
                _activePets[i].Update(owner, dropPool, ownerId, pickupAllowed, currentTime, deltaTime);
            }
        }

        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer,
            int mapShiftX, int mapShiftY, int centerX, int centerY)
        {
            for (int i = 0; i < _activePets.Count; i++)
            {
                _activePets[i].Draw(spriteBatch, skeletonRenderer, mapShiftX, mapShiftY, centerX, centerY);
            }
        }

        public void Clear()
        {
            _activePets.Clear();
        }

        private void ReindexPets(PlayerCharacter owner = null)
        {
            for (int i = 0; i < _activePets.Count; i++)
            {
                _activePets[i].SlotIndex = i;
            }

            if (owner != null)
            {
                SyncPositionsToOwner(owner);
            }
        }

        private void SyncPositionsToOwner(PlayerCharacter owner)
        {
            for (int i = 0; i < _activePets.Count; i++)
            {
                float direction = owner.FacingRight ? -1f : 1f;
                float offsetX = direction * (28f + i * 18f);
                _activePets[i].SetPosition(owner.X + offsetX, owner.Y, owner.FacingRight);
            }
        }
    }
}
