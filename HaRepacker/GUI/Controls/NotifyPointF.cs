using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaRepacker.GUI.Controls {
    /// <summary>
    /// Custom PointF struct that implements INotifyPropertyChanged for PropertyView
    /// </summary>
    public class NotifyPointF : INotifyPropertyChanged {
        private float _x;
        private float _y;

        public float X {
            get => _x;
            set {
                if (_x != value) {
                    _x = value;
                    OnPropertyChanged(nameof(X));
                }
            }
        }

        public float Y {
            get => _y;
            set {
                if (_y != value) {
                    _y = value;
                    OnPropertyChanged(nameof(Y));
                }
            }
        }

        public NotifyPointF(float x, float y) {
            _x = x;
            _y = y;
        }
        public NotifyPointF(PointF f)  {
            _x = f.X;
            _y = f.Y;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public static implicit operator PointF(NotifyPointF point) => new PointF(point.X, point.Y);
        public static implicit operator NotifyPointF(PointF point) => new NotifyPointF(point.X, point.Y);
    }
}
