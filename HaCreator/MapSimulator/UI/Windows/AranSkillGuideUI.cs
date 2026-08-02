using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// WZ-backed Aran skill guide overlay opened from the Big Bang skill window.
    /// </summary>
    public sealed class AranSkillGuideUI : UIWindowBase
    {
        private readonly IDXObject[] _pages;
        private int _currentGrade = 1;

        public AranSkillGuideUI(IDXObject[] pages)
            : base(ResolveInitialFrame(pages))
        {
            _pages = pages ?? Array.Empty<IDXObject>();
            SetPage(1);
        }

        public override string WindowName => MapSimulatorWindowNames.AranSkillGuide;

        public int CurrentGrade => _currentGrade;

        public void SetPage(int grade)
        {
            if (_pages.Length == 0)
            {
                return;
            }

            int clampedGrade = Math.Clamp(grade, 1, _pages.Length);
            IDXObject page = _pages[clampedGrade - 1] ?? ResolveInitialFrame(_pages);
            if (page == null)
            {
                return;
            }

            _currentGrade = clampedGrade;
            Frame = page;
        }

        private static IDXObject ResolveInitialFrame(IDXObject[] pages)
        {
            if (pages == null)
            {
                return null;
            }

            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] != null)
                {
                    return pages[i];
                }
            }

            return null;
        }

        protected override void DrawContents(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
        }
    }
}
