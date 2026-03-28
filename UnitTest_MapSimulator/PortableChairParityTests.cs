using HaCreator.MapSimulator.Character;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;

namespace UnitTest_MapSimulator;

public sealed class PortableChairParityTests
{
    [Fact]
    public void PortableChairLayersUseTheirOwnAnimationTime()
    {
        BodyPart body = new()
        {
            Type = CharacterPartType.Body,
            Animations =
            {
                ["sit"] = new CharacterAnimation
                {
                    Action = CharacterAction.Sit,
                    ActionName = "sit",
                    Frames =
                    {
                        new CharacterFrame
                        {
                            Texture = new TestDxObject(20, 40, "body"),
                            Delay = 120,
                            Origin = new Point(10, 30),
                            Map = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["navel"] = new Point(10, 20),
                                ["neck"] = new Point(10, 8)
                            }
                        }
                    }
                }
            }
        };
        body.Animations["sit"].CalculateTotalDuration();

        PortableChairLayer chairLayer = new()
        {
            Name = "effect",
            Animation = new CharacterAnimation
            {
                Action = CharacterAction.Custom,
                ActionName = "effect",
                Frames =
                {
                    new CharacterFrame
                    {
                        Texture = new TestDxObject(12, 12, "chair-0"),
                        Delay = 100,
                        Origin = new Point(3, 9)
                    },
                    new CharacterFrame
                    {
                        Texture = new TestDxObject(14, 10, "chair-1"),
                        Delay = 100,
                        Origin = new Point(4, 7)
                    }
                }
            }
        };
        chairLayer.Animation.CalculateTotalDuration();

        CharacterBuild build = new()
        {
            Body = body,
            ActivePortableChair = new PortableChair
            {
                ItemId = 3012000,
                Layers = { chairLayer }
            }
        };

        CharacterAssembler assembler = new(build);

        AssembledFrame startFrame = assembler.GetFrameAtTime("sit", 0);
        AssembledFrame nextFrame = assembler.GetFrameAtTime("sit", 150);

        AssembledPart startChairPart = Assert.Single(startFrame.Parts.Where(part => part.PartType == CharacterPartType.PortableChair));
        AssembledPart nextChairPart = Assert.Single(nextFrame.Parts.Where(part => part.PartType == CharacterPartType.PortableChair));

        Assert.Same(chairLayer, startChairPart.SourcePortableChairLayer);
        Assert.Equal("chair-0", Assert.IsType<TestDxObject>(startChairPart.Texture).Name);
        Assert.Equal(-3, startChairPart.OffsetX);
        Assert.Equal(-9, startChairPart.OffsetY);

        Assert.Equal("chair-1", Assert.IsType<TestDxObject>(nextChairPart.Texture).Name);
        Assert.Equal(-4, nextChairPart.OffsetX);
        Assert.Equal(-7, nextChairPart.OffsetY);
    }

    private sealed class TestDxObject(int width, int height, string name) : IDXObject
    {
        public string Name { get; } = name;
        public int Delay => 0;
        public int X => 0;
        public int Y => 0;
        public int Width { get; } = width;
        public int Height { get; } = height;
        public object Tag { get; set; }
        public Texture2D Texture => null;

        public void DrawObject(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime, int mapShiftX, int mapShiftY, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
        {
        }

        public void DrawBackground(SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime, int x, int y, Color color, bool flip, ReflectionDrawableBoundary drawReflectionInfo)
        {
        }
    }
}
