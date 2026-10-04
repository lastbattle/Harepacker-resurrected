namespace HaCreator.MapSimulator.UI.Controls {

    public class HaUISize {

        public int _Width = 0;
        public int Width {
            get { return this._Width; }
            set { this._Width = value; }
        }

        public int _Height = 0;
        public int Height {
            get { return this._Height; }
            set { this._Height = value; }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        public HaUISize(int width, int height) {
            _Width = width;
            _Height = height;
        }
    }
}
