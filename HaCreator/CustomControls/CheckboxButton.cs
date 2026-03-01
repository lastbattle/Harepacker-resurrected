using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace HaCreator.CustomControls
{
    public class CheckboxButton : CheckBox
    {
        protected override void OnClick()
        {
            if (Clicked != null)
                Clicked.Invoke(this, new RoutedEventArgs());
        }

        public event EventHandler<RoutedEventArgs> Clicked;
    }
}
