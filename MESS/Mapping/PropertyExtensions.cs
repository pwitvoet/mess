using MESS.Mathematics.Spatial;
using System;
using System.Collections.Generic;

namespace MESS.Mapping
{
    static class PropertyExtensions
    {
        // Entity variants:
        public static int? GetIntegerProperty(this Entity entity, string propertyName) => entity.Properties.GetIntegerProperty(propertyName);

        public static double? GetNumericProperty(this Entity entity, string propertyName) => entity.Properties.GetNumericProperty(propertyName);

        public static double[] GetNumericArrayProperty(this Entity entity, string propertyName) => entity.Properties.GetNumericArrayProperty(propertyName);

        public static Angles? GetAnglesProperty(this Entity entity, string propertyName) => entity.Properties.GetAnglesProperty(propertyName);

        public static Vector3D? GetVector3DProperty(this Entity entity, string propertyName) => entity.Properties.GetVector3DProperty(propertyName);

        public static string GetStringProperty(this Entity entity, string propertyName) => entity.Properties.GetStringProperty(propertyName);


        // Raw dictionary variants:
        public static int? GetIntegerProperty(this Dictionary<string, string> properties, string propertyName)
        {
            if (properties.TryGetValue(propertyName, out var stringValue) &&
                int.TryParse(stringValue, out var value))
                return value;

            return null;
        }

        public static double? GetNumericProperty(this Dictionary<string, string> properties, string propertyName)
        {
            if (properties.TryGetValue(propertyName, out var stringValue) &&
                double.TryParse(stringValue, out var value))
                return value;

            return null;
        }

        public static double[] GetNumericArrayProperty(this Dictionary<string, string> properties, string propertyName)
        {
            if (properties.TryGetValue(propertyName, out var stringValue) &&
                TryParseVector(stringValue, out var array))
                return array;

            return null;
        }

        public static Angles? GetAnglesProperty(this Dictionary<string, string> properties, string propertyName)
        {
            if (properties.GetNumericArrayProperty(propertyName) is double[] array && array.Length == 3)
                return new Angles((float)array[2], (float)array[0], (float)array[1]);

            return null;
        }

        public static Vector3D? GetVector3DProperty(this Dictionary<string, string> properties, string propertyName)
        {
            if (properties.GetNumericArrayProperty(propertyName) is double[] array && array.Length == 3)
                return new Vector3D((float)array[0], (float)array[1], (float)array[2]);

            return null;
        }

        public static string GetStringProperty(this Dictionary<string, string> properties, string propertyName)
            => properties.TryGetValue(propertyName, out var value) ? value : null;

        public static object ParseProperty(string value)
        {
            if (value == null)
                return null;

            if (double.TryParse(value, out var number))
                return number;

            if (TryParseVector(value, out var vector))
                return vector;

            return value;
        }


        private static bool TryParseVector(string value, out double[] vector)
        {
            if (value == null)
            {
                vector = null;
                return false;
            }

            var parts = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            vector = new double[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!double.TryParse(parts[i], out var number))
                {
                    vector = null;
                    return false;
                }

                vector[i] = number;
            }
            return true;
        }
    }
}
