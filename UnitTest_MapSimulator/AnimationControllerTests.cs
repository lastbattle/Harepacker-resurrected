using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Objects.FieldObject;
using HaSharedLibrary.Render.DX;
using Moq;
using System.Collections.Generic;
using Xunit;

namespace UnitTest_MapSimulator
{
    /// <summary>
    /// Unit tests for AnimationController
    /// </summary>
    public class AnimationControllerTests
    {
        private AnimationSetBase CreateMockAnimationSet()
        {
            var animSet = new TestAnimationSet();

            var standFrames = new List<IDXObject>
            {
                CreateMockFrame(100),
                CreateMockFrame(100),
                CreateMockFrame(100)
            };

            var moveFrames = new List<IDXObject>
            {
                CreateMockFrame(80),
                CreateMockFrame(80),
                CreateMockFrame(80),
                CreateMockFrame(80)
            };

            var attackFrames = new List<IDXObject>
            {
                CreateMockFrame(50),
                CreateMockFrame(50),
                CreateMockFrame(50)
            };

            animSet.AddAnimation("stand", standFrames);
            animSet.AddAnimation("move", moveFrames);
            animSet.AddAnimation("attack1", attackFrames);

            return animSet;
        }

        private IDXObject CreateMockFrame(int delay)
        {
            var mock = new Mock<IDXObject>();
            mock.Setup(x => x.Delay).Returns(delay);
            return mock.Object;
        }

        [Fact]
        public void Constructor_SetsInitialAction()
        {
            var animSet = CreateMockAnimationSet();
            var controller = new AnimationController(animSet, "stand");

            Assert.Equal("stand", controller.CurrentAction);
            Assert.Equal(0, controller.CurrentFrameIndex);
        }

        [Fact]
        public void SetAction_ChangesAction()
        {
            var animSet = CreateMockAnimationSet();
            var controller = new AnimationController(animSet, "stand");

            bool result = controller.SetAction("move");

            Assert.True(result);
            Assert.Equal("move", controller.CurrentAction);
            Assert.Equal(0, controller.CurrentFrameIndex);
        }

        [Fact]
        public void SetAction_ReturnsFalseForSameAction()
        {
            var animSet = CreateMockAnimationSet();
            var controller = new AnimationController(animSet, "stand");

            // Setting the same action should return false (already playing)
            bool result = controller.SetAction("stand");

            Assert.False(result);
            Assert.Equal("stand", controller.CurrentAction);
        }

        [Fact]
        public void UpdateFrame_AdvancesFrameAfterDelay()
        {
            var animSet = CreateMockAnimationSet();
            var controller = new AnimationController(animSet, "stand");

            Assert.Equal(0, controller.CurrentFrameIndex);

            // First call initializes timing (use non-zero tick)
            controller.UpdateFrame(1);
            Assert.Equal(0, controller.CurrentFrameIndex);

            // Not enough time passed (delay is 100ms)
            controller.UpdateFrame(50);
            Assert.Equal(0, controller.CurrentFrameIndex);

            // Now enough time has passed
            controller.UpdateFrame(150);
            Assert.Equal(1, controller.CurrentFrameIndex);
        }

        [Fact]
        public void UpdateFrame_LoopsAnimation()
        {
            var animSet = CreateMockAnimationSet();
            var controller = new AnimationController(animSet, "stand");

            // Initialize timing (use non-zero tick to avoid initialization reset)
            controller.UpdateFrame(1);
            Assert.Equal(0, controller.CurrentFrameIndex);

            // Advance through all frames (delay is 100ms per frame)
            controller.UpdateFrame(110);  // Frame 0 -> 1
            Assert.Equal(1, controller.CurrentFrameIndex);

            controller.UpdateFrame(220);  // Frame 1 -> 2
            Assert.Equal(2, controller.CurrentFrameIndex);

            controller.UpdateFrame(330);  // Frame 2 -> 0 (loop)
            Assert.Equal(0, controller.CurrentFrameIndex);
        }

        [Fact]
        public void PlayOnce_StopsAtLastFrame()
        {
            var animSet = CreateMockAnimationSet();
            var controller = new AnimationController(animSet, "stand");

            controller.PlayOnce("attack1");
            Assert.False(controller.IsAnimationComplete);

            // Initialize timing (use non-zero tick to avoid initialization reset)
            controller.UpdateFrame(1);

            // Advance through frames (attack1 has delay of 50ms per frame)
            controller.UpdateFrame(60);   // Frame 0 -> 1
            Assert.Equal(1, controller.CurrentFrameIndex);

            controller.UpdateFrame(120);  // Frame 1 -> 2
            Assert.Equal(2, controller.CurrentFrameIndex);

            controller.UpdateFrame(180);  // Try to advance past last frame
            Assert.True(controller.IsAnimationComplete);
            Assert.Equal(2, controller.CurrentFrameIndex); // Stays on last frame
        }

        [Fact]
        public void PlayOnceThenTransition_TransitionsAfterComplete()
        {
            var animSet = CreateMockAnimationSet();
            var controller = new AnimationController(animSet, "stand");

            controller.PlayOnceThenTransition("attack1", "stand");

            // Initialize timing (use non-zero tick to avoid initialization reset)
            controller.UpdateFrame(1);

            // Advance through attack1 frames (delay 50ms)
            controller.UpdateFrame(60);   // Frame 0 -> 1
            controller.UpdateFrame(120);  // Frame 1 -> 2
            controller.UpdateFrame(180);  // Completes and transitions

            // Should have transitioned to stand
            Assert.Equal("stand", controller.CurrentAction);
            Assert.False(controller.IsAnimationComplete);
        }

        [Fact]
        public void GetCurrentFrame_ReturnsCorrectFrame()
        {
            var animSet = CreateMockAnimationSet();
            var controller = new AnimationController(animSet, "stand");

            var frame = controller.GetCurrentFrame();

            Assert.NotNull(frame);
            Assert.Equal(100, frame.Delay);
        }

        [Fact]
        public void OnAnimationComplete_FiresEvent()
        {
            var animSet = CreateMockAnimationSet();
            var controller = new AnimationController(animSet, "stand");

            string? completedAction = null;
            controller.OnAnimationComplete += action => completedAction = action;

            controller.PlayOnce("attack1");

            // Initialize and advance through animation (use non-zero tick)
            controller.UpdateFrame(1);
            controller.UpdateFrame(60);   // Frame 0 -> 1
            controller.UpdateFrame(120);  // Frame 1 -> 2
            controller.UpdateFrame(180);  // Completes

            Assert.Equal("attack1", completedAction);
        }
    }

    /// <summary>
    /// Test implementation of AnimationSetBase
    /// </summary>
    internal class TestAnimationSet : AnimationSetBase
    {
        protected override bool TryGetFallbackFrames(string requestedAction, out List<IDXObject> frames)
        {
            frames = null!;
            return false;
        }
    }
}
