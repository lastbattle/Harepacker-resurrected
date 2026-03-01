using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace HaRepacker.GUI.Controls {
    /// <summary>
    /// A custom control to make PointF editable.
    /// </summary>
    public class PointFEditorControl : Control {
        public static readonly DependencyProperty PointFProperty =
            DependencyProperty.Register(nameof(PointF), typeof(NotifyPointF), typeof(PointFEditorControl),
                new FrameworkPropertyMetadata(new NotifyPointF(0, 0), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(PointFEditorControl), new PropertyMetadata(false));

        public NotifyPointF PointF {
            get { 
                return (NotifyPointF)GetValue(PointFProperty); 
            }
            set { 
                SetValue(PointFProperty, value); 
            }
        }

        public bool IsReadOnly {
            get { 
                return (bool)GetValue(IsReadOnlyProperty); 
            }
            set { 
                SetValue(IsReadOnlyProperty, value); 
            }
        }

        static PointFEditorControl() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PointFEditorControl), new FrameworkPropertyMetadata(typeof(PointFEditorControl)));
        }
    }
}