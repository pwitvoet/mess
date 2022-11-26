using MESS.Macros;
using MESS.Mathematics.Spatial;
using MScript;
using System.Globalization;

namespace MESS.Mapping
{
    static class PropertyExtensions
    {
        public static string? GetString(this IDictionary<string, string> properties, string propertyName)
            => properties.TryGetValue(propertyName, out var str)
            ? str
            : null;

        public static double? GetDouble(this IDictionary<string, string> properties, string propertyName)
            => properties.TryGetValue(propertyName, out var str) && TryParseDouble(str, out var value)
            ? value
            : null;

        public static double[]? GetDoubleArray(this IDictionary<string, string> properties, string propertyName)
            => properties.TryGetValue(propertyName, out var str) && TryParseNumericalArray(str, out var value)
            ? value
            : null;

        public static int? GetInteger(this IDictionary<string, string> properties, string propertyName)
            => properties.TryGetValue(propertyName, out var str) && int.TryParse(str, out var value)
            ? value
            : null;

        public static Vector3D? GetVector3D(this IDictionary<string, string> properties, string propertyName)
            => properties.TryGetValue(propertyName, out var str) && TryParseNumericalArray(str, out var array) && array.Length == 3
            ? new Vector3D((float)array[0], (float)array[1], (float)array[2])
            : null;

        public static Angles? GetAngles(this IDictionary<string, string> properties, string propertyName)
            => properties.TryGetValue(propertyName, out var str) && TryParseNumericalArray(str, out var array) && array.Length == 3
            ? new Angles((float)array[2], (float)array[0], (float)array[1])
            : null;

        public static TEnum? GetEnum<TEnum>(this IDictionary<string, string> properties, string propertyName)
            where TEnum : struct
            => properties.TryGetValue(propertyName, out var str) && Enum.TryParse<TEnum>(str, out var value)
            ? value
            : null;


        public static void SetString(this IDictionary<string, string> properties, string propertyName, string value)
            => properties[propertyName] = value;

        public static void SetDouble(this IDictionary<string, string> properties, string propertyName, double value)
            => properties[propertyName] = value.ToString(CultureInfo.InvariantCulture);

        public static void SetInteger(this IDictionary<string, string> properties, string propertyName, int value)
            => properties[propertyName] = value.ToString(CultureInfo.InvariantCulture);

        public static void SetVector3D(this IDictionary<string, string> properties, string propertyName, Vector3D value)
            => properties[propertyName] = FormattableString.Invariant($"{value.X} {value.Y} {value.Z}");

        public static void SetAngles(this IDictionary<string, string> properties, string propertyName, Angles value)
            => properties[propertyName] = FormattableString.Invariant($"{value.Pitch} {value.Yaw} {value.Roll}");


        /// <summary>
        /// Evaluates the value for the specified property to an MScript value. Returns none (null) if the property does not exist.
        /// </summary>
        public static object? EvaluateToMScriptValue(this IDictionary<string, string> properties, string propertyName, EvaluationContext context)
        {
            if (!properties.TryGetValue(propertyName, out var value))
                return null;

            return Evaluation.EvaluateInterpolatedStringOrExpression(value, context);
        }

        public static string EvaluateToString(this IDictionary<string, string> properties, string propertyName, EvaluationContext context)
            => Interpreter.Print(properties.EvaluateToMScriptValue(propertyName, context));

        /// <summary>
        /// Evaluates all keys to strings and all values to MScript values. Empty keys are excluded (their values are not evaluated).
        /// 'spawnflag{0-31}' keys are removed, but their values are used to set specific bits in the special 'spawnflags' attribute.
        /// </summary>
        public static Dictionary<string, object?> EvaluateToMScriptValues(this IDictionary<string, string> properties, EvaluationContext context)
        {
            var result = new Dictionary<string, object?>();
            foreach (var kv in properties)
            {
                var key = Evaluation.EvaluateInterpolatedString(kv.Key, context);
                if (key == "")
                    continue;

                var value = Evaluation.EvaluateInterpolatedStringOrExpression(kv.Value, context);
                result[key] = value;
            }

            result.UpdateSpawnFlags();
            return result;
        }

        /// <inheritdoc cref="EvaluateToMScriptValues(IDictionary{string, string}, EvaluationContext)"/>
        public static Dictionary<string, object?> EvaluateToMScriptValues(this IDictionary<string, string> properties, InstantiationContext context)
            => properties.EvaluateToMScriptValues(context.EvaluationContext);


        public static bool TryParseNumericalArray(string? value, out double[] array)
        {
            if (string.IsNullOrEmpty(value))
            {
                array = Array.Empty<double>();
                return false;
            }

            var parts = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            array = new double[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!TryParseDouble(parts[i], out var number))
                {
                    array = Array.Empty<double>();
                    return false;
                }

                array[i] = number;
            }
            return true;
        }

        public static bool TryParseDouble(string? s, out double result) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}
