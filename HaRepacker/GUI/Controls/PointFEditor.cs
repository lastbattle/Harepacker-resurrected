using HaRepacker.Converter;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Xceed.Wpf.Toolkit.PropertyGrid;
using Xceed.Wpf.Toolkit.PropertyGrid.Editors;

namespace HaRepacker.GUI.Controls {

    public class PointFEditor : TypeEditor<PointFEditorControl> {
        protected override void SetValueDependencyProperty() {
            ValueProperty = PointFEditorControl.PointFProperty;
        }

        protected override void SetControlProperties(PropertyItem propertyItem) {
            base.SetControlProperties(propertyItem);
            Editor.IsReadOnly = propertyItem.IsReadOnly;
        }

        protected override IValueConverter CreateValueConverter() {
            return new PointFValueConverter();
        }
    }
}