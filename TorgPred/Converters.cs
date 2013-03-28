using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace TorgPred
{
    public class ValueToBrushConverter : IValueConverter
    {
        BrushConverter brushconverter = new BrushConverter();
        public readonly string CustomGreen = "#FFB3FF80";
        public readonly string CustomRed = "#FFFF8989";

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                long input = (long)value;
                if (input == 0)
                    return (Brush)brushconverter.ConvertFromString(CustomRed);
                else
                    if (input > 0)
                        return (Brush)brushconverter.ConvertFromString(CustomGreen);
                    else
                        return DependencyProperty.UnsetValue;
            }
            catch { return DependencyProperty.UnsetValue; }

        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class BoolToBrushConverter : IValueConverter
    {
        BrushConverter brushconverter = new BrushConverter();
        public readonly string CustomGreen = "#FFB3FF80";
        public readonly string CustomRed = "#FFFF8989";

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                bool input = (bool)value;
                if (input)
                    return (Brush)brushconverter.ConvertFromString(CustomRed);
                //else
                //    if (!input)
                //        return (Brush)brushconverter.ConvertFromString(CustomGreen);
                //    else
                //        return DependencyProperty.UnsetValue;
                return DependencyProperty.UnsetValue;
            }
            catch { return DependencyProperty.UnsetValue; }

        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class TabSizeConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter,System.Globalization.CultureInfo culture)
        {
            TabControl tabControl = values[0] as TabControl;
            double width = tabControl.ActualWidth / tabControl.Items.Count;
            //Subtract 1, otherwise we could overflow to two rows.
            return (width <= 1) ? 0 : (width - 2);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter,
            System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

}
