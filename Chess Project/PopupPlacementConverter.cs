using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Chess_Project
{
    public class PopupPlacementConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double viewboxWidth)
            {
                double leftOffset = (((viewboxWidth) / 2) + viewboxWidth);

                return new Rect(leftOffset, 40, 0, 0);
            }

            else
            {
                throw new ArgumentException("Invalid value. Expected double value representing the width of the viewbox.");
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}