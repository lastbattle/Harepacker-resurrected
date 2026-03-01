using HaCreator.MapEditor.Info;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor.Instance
{
    public class ReactorInstance : BoardItem, IFlippable, ISerializable
    {
        private readonly ReactorInfo reactorInfo;
        public ReactorInfo ReactorInfo { get { return reactorInfo; } }

        private int reactorTime;
        private bool flip;
        private string name;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="baseInfo"></param>
        /// <param name="board"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="reactorTime"></param>
        /// <param name="name"></param>
        /// <param name="flip"></param>
        public ReactorInstance(ReactorInfo baseInfo, Board board, int x, int y, int reactorTime, string name, bool flip)
            : base(board, x, y, -1)
        {
            this.reactorInfo = baseInfo;
            this.reactorTime = reactorTime;
            this.flip = flip;
            this.name = name;
            if (flip)
                X -= Width - 2 * Origin.X;
        }

        public override void Draw(SpriteBatch sprite, XNA.Color color, int xShift, int yShift)
        {
            XNA.Rectangle destinationRectangle = new XNA.Rectangle((int)X + xShift - Origin.X, (int)Y + yShift - Origin.Y, Width, Height);
            //if (baseInfo.Texture == null) baseInfo.CreateTexture(sprite.GraphicsDevice);
            sprite.Draw(reactorInfo.GetTexture(sprite), destinationRectangle, 
                null, 
                color, 
                0f, 
                new XNA.Vector2(0f, 0f), 
                Flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 
                1f);
            base.Draw(sprite, color, xShift, yShift);
        }

        public override MapleDrawableInfo BaseInfo
        {
            get { return reactorInfo; }
        }

        public bool Flip
        {
            get
            {
                return flip;
            }
            set
            {
                if (flip == value) return;
                flip = value;
                int xFlipShift = Width - 2 * Origin.X;
                if (flip) X -= xFlipShift;
                else X += xFlipShift;
            }
        }

        public int UnflippedX
        {
            get
            {
                return flip ? (X + Width - 2 * Origin.X) : X;
            }
        }

        public override ItemTypes Type
        {
            get { return ItemTypes.Reactors; }
        }

        public override System.Drawing.Bitmap Image
        {
            get
            {
                return reactorInfo.Image;
            }
        }

        public override int Width
        {
            get { return reactorInfo.Width; }
        }

        public override int Height
        {
            get { return reactorInfo.Height; }
        }

        public override System.Drawing.Point Origin
        {
            get
            {
                return reactorInfo.Origin;
            }
        }

        public int ReactorTime
        {
            get { return reactorTime; }
            set { reactorTime = value; }
        }

        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        public new class SerializationForm : BoardItem.SerializationForm
        {
            public string id;
            public int reactortime;
            public bool flip;
            public string name;
        }

        public override object Serialize()
        {
            SerializationForm result = new SerializationForm();
            UpdateSerializedForm(result);
            return result;
        }

        protected void UpdateSerializedForm(SerializationForm result)
        {
            base.UpdateSerializedForm(result);
            result.id = reactorInfo.ID;
            result.reactortime = reactorTime;
            result.flip = flip;
            result.name = name;
        }

        public ReactorInstance(Board board, SerializationForm json)
            : base(board, json)
        {
            reactorInfo = ReactorInfo.Get(json.id);
        }
    }
}
