using HaCreator.MapSimulator.Companions;
using Microsoft.Xna.Framework;
using System;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private DragonCompanionRuntime.OwnerPhaseContext ResolveDragonOwnerPhaseContextParity()
        {
            bool hasOwnerBuild = _playerManager?.Player?.Build != null;
            bool hasScreenFadeTransition = _screenEffects?.IsFadeActive == true;
            float screenFadeAlpha = _screenEffects?.FadeAlpha ?? 0f;
            return ResolveDragonOwnerPhaseContextParity(
                hasOwnerBuild,
                hasScreenFadeTransition,
                screenFadeAlpha);
        }

        internal static DragonCompanionRuntime.OwnerPhaseContext ResolveDragonOwnerPhaseContextParity(
            bool hasOwnerBuild,
            bool hasScreenFadeTransition,
            float screenFadeAlpha)
        {
            if (!hasOwnerBuild)
            {
                return DragonCompanionRuntime.OwnerPhaseContext.NoLocalUser;
            }

            int phaseAlpha = ResolveDragonOwnerPhaseAlphaForTransition(hasScreenFadeTransition, screenFadeAlpha);
            bool ownerMatchesLocalPhase = !hasScreenFadeTransition;
            return new DragonCompanionRuntime.OwnerPhaseContext(
                hasLocalUser: true,
                ownerMatchesLocalPhase,
                phaseAlpha);
        }

        internal static int ResolveDragonOwnerPhaseAlphaForTransition(bool hasScreenFadeTransition, float screenFadeAlpha)
        {
            if (!hasScreenFadeTransition)
            {
                return byte.MaxValue;
            }

            float clampedOpacity = MathHelper.Clamp(screenFadeAlpha, 0f, 1f);
            float ownerPhaseAlpha = 1f - clampedOpacity;
            return (int)Math.Round(ownerPhaseAlpha * byte.MaxValue, MidpointRounding.AwayFromZero);
        }
    }
}
