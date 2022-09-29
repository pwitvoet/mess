using MESS.Mathematics.Spatial;
using System.Globalization;

namespace MESS.Mapping
{
    static class PropertyExtensions
    {
        // Entity variants:
        public static int? GetIntegerProperty(this Entity entity, string propertyName) => entity.Properties.GetIntegerProperty(propertyName);

        public static double? GetNumericProperty(this Entity entity, string propertyName) => entity.Properties.GetNumericProperty(propertyName);

        public static double[]? GetNumericArrayProperty(this Entity entity, string propertyName) => entity.Properties.GetNumericArrayProperty(propertyName);

        public static Angles? GetAnglesProperty(this Entity entity, string propertyName) => entity.Properties.GetAnglesProperty(propertyName);

        public static Vector3D? GetVector3DProperty(this Entity entity, string propertyName) => entity.Properties.GetVector3DProperty(propertyName);

        public static TEnum? GetEnumProperty<TEnum>(this Entity entity, string propertyName)
            where TEnum: struct
            => entity.Properties.GetEnumProperty<TEnum>(propertyName);

        public static string? GetStringProperty(this Entity entity, string propertyName) => entity.Properties.GetStringProperty(propertyName);


        // Raw dictionary variants:
        public static int? GetIntegerProperty(this IDictionary<string, string> properties, string propertyName)
        {
            if (properties.TryGetValue(propertyName, out var stringValue) &&
                int.TryParse(stringValue, out var value))
                return value;

            return null;
        }

        public static double? GetNumericProperty(this IDictionary<string, string> properties, string propertyName)
        {
            if (properties.TryGetValue(propertyName, out var stringValue) &&
                TryParseDouble(stringValue, out var value))
                return value;

            return null;
        }

        public static double[]? GetNumericArrayProperty(this IDictionary<string, string> properties, string propertyName)
        {
            if (properties.TryGetValue(propertyName, out var stringValue) &&
                TryParseVector(stringValue, out var array))
                return array;

            return null;
        }

        public static Angles? GetAnglesProperty(this IDictionary<string, string> properties, string propertyName)
        {
            if (properties.GetNumericArrayProperty(propertyName) is double[] array && array.Length == 3)
                return new Angles((float)array[2], (float)array[0], (float)array[1]);

            return null;
        }

        public static Vector3D? GetVector3DProperty(this IDictionary<string, string> properties, string propertyName)
        {
            if (properties.GetNumericArrayProperty(propertyName) is double[] array && array.Length == 3)
                return new Vector3D((float)array[0], (float)array[1], (float)array[2]);

            return null;
        }

        public static TEnum? GetEnumProperty<TEnum>(this IDictionary<string, string> properties, string propertyName)
            where TEnum: struct
        {
            if (properties.TryGetValue(propertyName, out var stringValue) && Enum.TryParse<TEnum>(stringValue, out var value))
                return value;

            return null;
        }

        public static string? GetStringProperty(this IDictionary<string, string> properties, string propertyName)
            => properties.TryGetValue(propertyName, out var value) ? value : null;

        public static object? ParseProperty(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            if (TryParseDouble(value, out var number))
                return number;

            if (TryParseVector(value, out var vector))
                return vector;

            return value;
        }


        // Setters (with proper invariant formatting):
        public static void SetIntegerProperty(this IDictionary<string, string> properties, string propertyName, int value)
            => properties[propertyName] = value.ToString(CultureInfo.InvariantCulture);

        public static void SetNumericProperty(this IDictionary<string, string> properties, string propertyName, double value)
            => properties[propertyName] = value.ToString(CultureInfo.InvariantCulture);

        public static void SetAnglesProperty(this IDictionary<string, string> properties, string propertyName, Angles value)
            => properties[propertyName] = FormattableString.Invariant($"{value.Pitch} {value.Yaw} {value.Roll}");

        public static void SetVector3DProperty(this IDictionary<string, string> properties, string propertyName, Vector3D value)
            => properties[propertyName] = FormattableString.Invariant($"{value.X} {value.Y} {value.Z}");

        public static void SetStringProperty(this IDictionary<string, string> properties, string propertyName, string value)
            => properties[propertyName] = value;


        public static bool TryParseVector(string? value, out double[] vector)
        {
            if (value == null)
            {
                vector = Array.Empty<double>();
                return false;
            }

            var parts = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            vector = new double[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!TryParseDouble(parts[i], out var number))
                {
                    vector = Array.Empty<double>();
                    return false;
                }

                vector[i] = number;
            }
            return true;
        }

        public static bool TryParseDouble(string? s, out double result) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}
