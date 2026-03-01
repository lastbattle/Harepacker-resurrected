using HaRepacker.GUI.Controls;
using HaRepacker.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace HaRepacker.Converter
{
	/// <summary>
	/// Converts PointF vector origin to XAML Margin
	/// </summary>
    public class VectorOriginPointFToMarginConverter : IValueConverter
	{
		private readonly float fCrossHairWidthHeight = 10f / 2f;

		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			NotifyPointF originValue = (NotifyPointF)value;

			// converted
			// its always -50, as it is 50px wide, as specified in the xaml
			Thickness margin = new Thickness((originValue.X) / ScreenDPIUtil.GetScreenScaleFactor(), (originValue.Y - fCrossHairWidthHeight) / ScreenDPIUtil.GetScreenScaleFactor(), 0, 0); // 20,75


			return margin;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			Thickness value_ = (Thickness)value;

			// converted
			PointF originValue = new NotifyPointF((float) ((value_.Left) * ScreenDPIUtil.GetScreenScaleFactor()), (float) ((value_.Top + fCrossHairWidthHeight) * ScreenDPIUtil.GetScreenScaleFactor()));
			return originValue;
		}
	}
}
