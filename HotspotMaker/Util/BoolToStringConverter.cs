using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace HotspotMaker.Util
{
    public class BoolToStringConverter : IValueConverter
    {
        public string? TrueValue { get; set; }
        public string? FalseValue { get; set; }


        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => System.Convert.ToBoolean(value) ? TrueValue : FalseValue;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var stringValue = System.Convert.ToString(value);
            return stringValue == TrueValue;
        }
    }
}
