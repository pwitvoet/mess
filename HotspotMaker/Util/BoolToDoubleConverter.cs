using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace HotspotMaker.Util
{
    public class BoolToDoubleConverter : IValueConverter
    {
        public double TrueValue { get; set; }
        public double FalseValue { get; set; }


        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => System.Convert.ToBoolean(value) ? TrueValue : FalseValue;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var doubleValue = System.Convert.ToDouble(value);
            return Math.Abs(doubleValue - TrueValue) <= Math.Abs(doubleValue - FalseValue);
        }
    }
}
