using HaCreator.GUI;
using HaCreator.MapEditor.Instance;
using HaCreator.Wz;
using HaSharedLibrary.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.Collections.Generic;
using System.Drawing;

namespace HaCreator.MapEditor.Info
{
    public class ReactorInfo : MapleExtractableInfo
    {
        private readonly string id;
        private readonly string _name;

        private WzImage _LinkedWzImage;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="image"></param>
        /// <param name="origin"></param>
        /// <param name="id"></param>
        /// <param name="name"></param>
        /// <param name="parentObject"></param>
        public ReactorInfo(Bitmap image, System.Drawing.Point origin, string id, string name, WzObject parentObject)
            : base(image, origin, parentObject)
        {
            this.id = id;
            this._name = name;
        }

        private void ExtractPNGFromImage(WzImage image)
        {
            WzCanvasProperty reactorImage = WzInfoTools.GetReactorImage(image);
            if (reactorImage != null)
            {
                Image = reactorImage.GetLinkedWzCanvasBitmap();
                Origin = WzInfoTools.PointFToSystemPoint(reactorImage.GetCanvasOriginPosition());
            }
            else
            {
                Image = new Bitmap(1, 1);
                Origin = new System.Drawing.Point();
            }
        }

        public override void ParseImage()
        {
            if (LinkedWzImage != null) // load from here too
                ExtractPNGFromImage(_LinkedWzImage);
            else
                ExtractPNGFromImage((WzImage)ParentObject);
        }

        public static ReactorInfo Get(string id)
        {
            ReactorInfo result = Program.InfoManager.Reactors[id];
            result.ParseImageIfNeeded();
            return result;
        }

        public override BoardItem CreateInstance(Layer layer, Board board, int x, int y, int z, bool flip)
        {
            if (Image == null) 
                ParseImage();
            return new ReactorInstance(this, board, x, y, UserSettings.defaultReactorTime, "", flip);
        }

        public BoardItem CreateInstance(Board board, int x, int y, int reactorTime, string name, bool flip)
        {
            if (Image == null) 
                ParseImage();
            return new ReactorInstance(this, board, x, y, reactorTime, name, flip);
        }

        public string ID
        {
            get { return id; }
            private set { }
        }

        public string Name
        {
            get { return _name; }
            private set { }
        }

        /// <summary>
        /// The source WzImage of the reactor
        /// </summary>
        public WzImage LinkedWzImage
        {
            get {
                if (_LinkedWzImage == null) {
                    string imgName = WzInfoTools.AddLeadingZeros(id, 7) + ".img";
                    WzImage reactorImage = Program.FindImage("Reactor", imgName);

                    WzStringProperty link = (WzStringProperty)reactorImage?["info"]?["link"];
                    if (link != null) {
                        string linkImgName = WzInfoTools.AddLeadingZeros(link.Value, 7) + ".img";
                        WzImage findLinkedImg = Program.FindImage("Reactor", linkImgName);

                        _LinkedWzImage = findLinkedImg ?? reactorImage; // fallback if link is null
                    }
                    else
                        _LinkedWzImage = reactorImage;
                }
                return _LinkedWzImage;
            }
            set { this._LinkedWzImage = value; }
        }
    }
}
