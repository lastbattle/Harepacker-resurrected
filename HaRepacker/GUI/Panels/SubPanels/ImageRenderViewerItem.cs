using HaRepacker.Converter;
using HaRepacker.GUI.Controls;
using HaSharedLibrary.Util;
using MapleLib.Converters;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace HaRepacker.GUI.Panels.SubPanels {

    /// <summary>
    /// Items in the PropertyGrid
    /// see: https://github.com/xceedsoftware/wpftoolkit/wiki/PropertyGrid
    /// </summary>
    public class ImageRenderViewerItem : INotifyPropertyChanged {
        private const string CATEGORY_DISPLAY = "Display";
        private const string CATEGORY_IMAGEINFO = "Image information";
        private const string CATEGORY_ANIMATION = "Animation";

        public ImageRenderViewerItem() {
            _CanvasVectorOrigin.PropertyChanged += (sender, args) => OnPropertyChanged(nameof(CanvasVectorOrigin));
            _CanvasVectorHead.PropertyChanged += (sender, args) => OnPropertyChanged(nameof(CanvasVectorHead));
            _CanvasVectorLt.PropertyChanged += (sender, args) => OnPropertyChanged(nameof(CanvasVectorLt));
        }

        #region Display

        private bool _bShowImageBorder;
        [Category(CATEGORY_DISPLAY)]
        [Description("Shows the image border")]
        [DisplayName("Border")]
        public bool ShowImageBorder {
            get => _bShowImageBorder;
            set {
                if (_bShowImageBorder != value) {
                    _bShowImageBorder = value;
                    OnPropertyChanged(nameof(ShowImageBorder));
                }
            }
        }

        private bool _bShowCrosshair;
        [Category(CATEGORY_DISPLAY)]
        [Description("Show crosshair")]
        [DisplayName("Crosshair")]
        public bool ShowCrosshair {
            get => _bShowCrosshair;
            set {
                if (_bShowCrosshair != value) {
                    _bShowCrosshair = value;
                    OnPropertyChanged(nameof(ShowCrosshair));
                }
            }
        }
        #endregion

        #region Image information
        private WzCanvasProperty _ParentWzCanvasProperty = null;
        /// <summary>
        /// The parent WZCanvasProperty to display from
        /// </summary>
        [ReadOnly(true)] // This makes the Name property read-only
        [Browsable(false)] // This hides the ID property from the PropertyGrid
        public WzCanvasProperty ParentWzCanvasProperty {
            get { return _ParentWzCanvasProperty; }
            set {
                _ParentWzCanvasProperty = value;
            }
        }

        private ImageSource _Image = null;
        /// <summary>
        /// The image to display on the canvas
        /// </summary>
        [ReadOnly(true)] // This makes the Name property read-only
        [Browsable(false)] // This hides the ID property from the PropertyGrid
        [DisplayName("Image")]
        [Category(CATEGORY_IMAGEINFO)]
        public ImageSource Image {
            get { return _Image; }
            set {
                _Image = value;
                OnPropertyChanged(nameof(Image));

                // Update image width and height too.
                ImageWidth = (int) _Image.Width;
                ImageHeight = (int)_Image.Height;
                ImageSizeKiloByte = BitmapHelper.GetImageSizeInKB(_Image);
            }
        }

        private Bitmap _Bitmap = null;
        private Bitmap _Bitmap_bak = null;
        /// <summary>
        /// The image to display on the canvas
        /// </summary>
        [ReadOnly(true)] // This makes the Name property read-only
        [Browsable(false)] // This hides the ID property from the PropertyGrid
        [DisplayName("Bitmap")]
        [Category(CATEGORY_IMAGEINFO)]
        public Bitmap Bitmap {
            get { return _Bitmap; }
            set {
                _Bitmap = value;
                OnPropertyChanged(nameof(Bitmap));

                Image = _Bitmap.ToWpfBitmap();
            }
        }

        [ReadOnly(true)] // This makes the Name property read-only
        [Browsable(false)] // This hides the ID property from the PropertyGrid
        public Bitmap BitmapBackup {
            get { return _Bitmap_bak; }
            set {
                _Bitmap_bak = value;
            }
        }

        private int _ImageWidth = 0;
        /// <summary>
        /// The width of the image currently displayed on the canvas
        /// </summary>
        [ReadOnly(true)] // This makes the Name property read-only
        [DisplayName("Image Width")]
        [Category(CATEGORY_IMAGEINFO)]
        public int ImageWidth {
            get { return _ImageWidth; }
            set {
                this._ImageWidth = value;
                OnPropertyChanged(nameof(ImageWidth));
            }
        }

        private int _ImageHeight = 0;
        /// <summary>
        /// The Height of the image currently displayed on the canvas
        /// </summary>
        [ReadOnly(true)] // This makes the Name property read-only
        [DisplayName("Image Height")]
        [Category(CATEGORY_IMAGEINFO)]
        public int ImageHeight {
            get { return _ImageHeight; }
            set {
                this._ImageHeight = value;
                OnPropertyChanged(nameof(ImageHeight));
            }
        }

        private double _ImageSizeKiloByte = 0;
        /// <summary>
        /// The Size of the image currently displayed on the canvas
        /// </summary>
        [ReadOnly(true)] // This makes the Name property read-only
        [DisplayName("Size (Kb)")]
        [Category(CATEGORY_IMAGEINFO)]
        public double ImageSizeKiloByte {
            get { return _ImageSizeKiloByte; }
            set {
                this._ImageSizeKiloByte = value;
                OnPropertyChanged(nameof(ImageSizeKiloByte));
            }
        }

        private SurfaceFormat _SurfaceFormat = SurfaceFormat.Color;
        /// <summary>
        /// The Size of the image currently displayed on the canvas
        /// </summary>
        [ReadOnly(true)] // This makes the Name property read-only
        [DisplayName("Surface Format")]
        [Description("The surface format of the image.")]
        [Category(CATEGORY_IMAGEINFO)]
        public SurfaceFormat SurfaceFormat {
            get { return _SurfaceFormat; }
            set {
                this._SurfaceFormat = value;
                OnPropertyChanged(nameof(SurfaceFormat));
            }
        }
        #endregion

        #region Animation
        private int _Delay = 0;

        /// <summary>
        /// Delay of the image
        /// </summary>
        [Category(CATEGORY_ANIMATION)]
        [DisplayName("Delay")]
        [Description("The delay to display this animation.")]
        public int Delay {
            get { return _Delay; }
            set {
                _Delay = value;
                OnPropertyChanged(nameof(Delay));
            }
        }

        private NotifyPointF _CanvasVectorOrigin = new NotifyPointF(0, 0);
        /// <summary>
        /// Origin to center the crosshair
        /// </summary>
        [Category(CATEGORY_ANIMATION)]
        [DisplayName("Origin")]
        [Description("The X-Y origin coordinates.")]
        [TypeConverter(typeof(PointFConverter))]
        [Editor(typeof(PointFEditor), typeof(PointFEditor))]
        public NotifyPointF CanvasVectorOrigin {
            get { return _CanvasVectorOrigin; }
            set {
                if (_CanvasVectorOrigin != value) {
                    if (_CanvasVectorOrigin != null) {
                        _CanvasVectorOrigin.PropertyChanged -= (sender, args) => OnPropertyChanged(nameof(CanvasVectorOrigin));
                    }
                    _CanvasVectorOrigin = value;
                    if (_CanvasVectorOrigin != null) {
                        _CanvasVectorOrigin.PropertyChanged += (sender, args) => OnPropertyChanged(nameof(CanvasVectorOrigin));
                    }
                    OnPropertyChanged(nameof(CanvasVectorOrigin));
                }
            }
        }

        private NotifyPointF _CanvasVectorHead = new NotifyPointF(0, 0);

        /// <summary>
        /// Head vector (Hit positioning for mobs?)
        /// </summary>
        [Category(CATEGORY_ANIMATION)]
        [DisplayName("Vector head")]
        [Description("The X-Y vector head coordinate.")]
        [TypeConverter(typeof(PointFConverter))]
        [Editor(typeof(PointFEditor), typeof(PointFEditor))]
        public NotifyPointF CanvasVectorHead {
            get { return _CanvasVectorHead; }
            set {
                if (_CanvasVectorHead != value) {
                    if (_CanvasVectorHead != null) {
                        _CanvasVectorHead.PropertyChanged -= (sender, args) => OnPropertyChanged(nameof(CanvasVectorHead));
                    }
                    _CanvasVectorHead = value;
                    if (_CanvasVectorHead != null) {
                        _CanvasVectorHead.PropertyChanged += (sender, args) => OnPropertyChanged(nameof(CanvasVectorHead));
                    }
                    OnPropertyChanged(nameof(CanvasVectorHead));
                }
            }
        }

        private NotifyPointF _CanvasVectorLt = new NotifyPointF(0, 0);
        /// <summary>
        /// lt vector
        /// </summary>
        [Category(CATEGORY_ANIMATION)]
        [DisplayName("Vector Lt")]
        [Description("The X-Y vector Lt coordinate.")]
        [TypeConverter(typeof(PointFConverter))]
        [Editor(typeof(PointFEditor), typeof(PointFEditor))]
        public NotifyPointF CanvasVectorLt {
            get { return _CanvasVectorLt; }
            set {
                if (_CanvasVectorLt != value) {
                    if (_CanvasVectorLt != null) {
                        _CanvasVectorLt.PropertyChanged -= (sender, args) => OnPropertyChanged(nameof(CanvasVectorLt));
                    }
                    _CanvasVectorLt = value;
                    if (_CanvasVectorLt != null) {
                        _CanvasVectorLt.PropertyChanged += (sender, args) => OnPropertyChanged(nameof(CanvasVectorLt));
                    }
                    OnPropertyChanged(nameof(CanvasVectorLt));
                }
            }
        }
        #endregion


        #region Events

        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// On property changed
        /// </summary>
        /// <param name="propertyName"></param>
        protected virtual void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
