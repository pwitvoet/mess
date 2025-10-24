using MESS.Mapping;
using MScript.Evaluation;
using MScript;
using MScript.Evaluation.Types;

namespace MESS.Macros.Functions
{
    public static class StandardLibraryFunctions
    {
        // Type checks:
        public static bool is_num(object? value) => value is double;

        public static bool is_str(object? value) => value is string;

        public static bool is_array(object? value) => value is object?[];

        public static bool is_obj(object? value) => value is MObject;

        public static bool is_func(object? value) => value is IFunction;

        public static string @typeof(object? value) => TypeDescriptor.GetType(value).Name;

        // Type conversion:
        public static double? num(object? value)
        {
            if (value is double number)
                return number;
            else if (value is string str)
                return PropertyExtensions.TryParseDouble(str, out number) ? number : null;
            else
                return null;
        }

        public static string str(object? value) => Interpreter.Print(value);

        public static object?[] array(object? value)
        {
            if (value is object?[] array)
                return array;
            else if (value is string str)
                return str.Select(c => (object?)c.ToString()).ToArray();
            else if (value is null)
                return Array.Empty<object?>();
            else
                return new object?[] { value };
        }

        // Mathematics:
        public static double min(double value1, double value2) => Math.Min(value1, value2);

        public static double max(double value1, double value2) => Math.Max(value1, value2);

        public static double clamp(double value, double min, double max)
        {
            if (min > max)
            {
                var temp = min;
                min = max;
                max = temp;
            }
            return Math.Max(min, Math.Min(value, max));
        }

        public static double abs(double value) => Math.Abs(value);

        public static double round(double value) => Math.Round(value);

        public static double floor(double value) => Math.Floor(value);

        public static double ceil(double value) => Math.Ceiling(value);

        public static double trunc(double value) => Math.Truncate(value);

        public static double pow(double value, double power) => Math.Pow(value, power);

        public static double sqrt(double value) => Math.Sqrt(value);

        // Trigonometry:
        public static double sin(double radians) => Math.Sin(radians);

        public static double cos(double radians) => Math.Cos(radians);

        public static double tan(double radians) => Math.Tan(radians);

        public static double asin(double sine) => Math.Asin(sine);

        public static double acos(double cosine) => Math.Acos(cosine);

        public static double atan(double tangent) => Math.Atan(tangent);

        public static double atan2(double y, double x) => Math.Atan2(y, x);

        public static double deg2rad(double degrees) => (degrees / 180.0) * Math.PI;

        public static double rad2deg(double radians) => (radians / Math.PI) * 180.0;

        // Text:
        public static double? ord(string? str, double? index = null)
        {
            if (str is null)
                return null;

            var charString = Operations.Index(str, (int)(index ?? 0));
            if (string.IsNullOrEmpty(charString))
                return null;

            return charString[0];
        }

        public static string chr(double value)
            => new string((char)value, 1);

        // Arrays:
        public static object?[] range(double start, double? stop = null, double? step = null)
        {
            if (stop == null)
            {
                stop = start;
                start = 0;
            }

            var intStart = (int)start;
            var intEnd = (int)stop;
            var intStep = (int)(step ?? 1);
            if (intStep == 0 || intStart == intEnd || (intStep < 0 && intEnd > intStart) || (intStep > 0 && intEnd < intStart))
                return Array.Empty<object?>();

            var count = (int)Math.Ceiling(Math.Abs(intEnd - intStart) / (double)Math.Abs(intStep));
            var result = new object?[count];
            for (int i = 0; i < result.Length; i++)
                result[i] = (double)(intStart + i * intStep);

            return result;
        }

        public static object?[] repeat(object? value, double count)
        {
            var intCount = (int)count;
            if (intCount <= 0)
                return Array.Empty<object?>();

            return Enumerable.Repeat(value, intCount).ToArray();
        }

        // Objects:
        public static MObject? obj_add(MObject? obj, string field_name, object? value)
        {
            if (obj == null)
                return new MObject(new[] { KeyValuePair.Create(field_name, value) });
            else
                return obj?.CreateCopyWithField(field_name, value);
        }

        public static MObject? obj_remove(MObject? obj, object? field_name)
        {
            if (field_name is string name)
                return obj?.CreateCopyWithoutField(name);
            else if (field_name is object?[] names)
                return obj?.CreateCopyWithoutFields(names.Select(Interpreter.Print));
            else
                return obj;
        }

        public static MObject? obj_merge(MObject? obj1, MObject? obj2)
        {
            if (obj1 == null)
                return obj2?.CreateCopy();
            else if (obj2 == null)
                return obj1.CreateCopy();
            else
                return obj1.CreateCopyWithFields(obj2.Fields);
        }

        public static bool obj_has(MObject? obj, string name) => !string.IsNullOrEmpty(name) && obj?.Fields.ContainsKey(name) == true;

        public static object? obj_value(MObject? obj, string name)
        {
            if (obj != null && !string.IsNullOrEmpty(name) && obj.Fields.TryGetValue(name, out var value))
                return value;
            else
                return null;
        }

        public static object?[] obj_fields(MObject? obj)
        {
            if (obj == null)
                return Array.Empty<object?>();
            else
                return obj.Fields.Keys.Cast<object?>().ToArray();
        }

        // Functions:
        public static string? func_name(IFunction? func)
            => func?.Name;

        public static object?[]? func_args(IFunction? func)
            => func?.Parameters.Select(param => (object?)param.Name).ToArray();

        public static MObject? func_arg(IFunction? func, string name)
        {
            var parameter = func?.Parameters.FirstOrDefault(param => param.Name == name);
            if (parameter == null)
                return null;

            return new MObject(new[] {
                    KeyValuePair.Create("name", (object?)parameter.Name),
                    KeyValuePair.Create("type", (object?)parameter.Type.Name),
                    KeyValuePair.Create("optional", (object?)(parameter.IsOptional ? 1.0 : null)),
                    KeyValuePair.Create("default_value", parameter.DefaultValue),
                });
        }

        public static object? func_apply(IFunction? func, object?[]? arguments)
            => func?.Apply(arguments ?? Array.Empty<object?>());


        // Colors:
        /// <summary>
        /// Returns an array with either 3 or 4 values, where the first 3 values are between 0 and 255.
        /// Accepts both numbers and numerical strings. Other values are treated as 0. Useful to 'sanitize' colors.
        /// </summary>
        public static object?[] color(object?[] color)
        {
            var result = new object?[] { 0.0, 0.0, 0.0 };
            if (color is null)
                return result;

            if (color.Length >= 4)
                result = new object?[] { 0.0, 0.0, 0.0, 0.0 };

            for (int i = 0; i < color.Length && i < 3; i++)
                result[i] = clamp(Math.Round(GetNumberOrZero(color[i])), 0, 255);

            if (result.Length > 3)
                result[3] = Math.Round(GetNumberOrZero(color[3]));

            return result;
        }

        public static MObject? rgb_to_hsv(object?[] rgb)
        {
            if (rgb.Length < 3 || rgb.Length > 4)
                return null;

            var r = GetNumberOrZero(rgb[0]) / 255.0;
            var g = GetNumberOrZero(rgb[1]) / 255.0;
            var b = GetNumberOrZero(rgb[2]) / 255.0;

            var min = Math.Min(r, Math.Min(g, b));
            var max = Math.Max(r, Math.Max(g, b));
            var chroma = max - min;

            var value = max;
            var saturation = (max == 0) ? 0 : chroma / max;
            var hue = 60 * (chroma == 0 ? 0 :
                               max == r ? ((g - b) / chroma) % 6 :
                               max == g ? ((b - r) / chroma) + 2 :
                                          ((r - g) / chroma) + 4);
            if (hue < 0)
                hue += 360;

            return new MObject(new[] {
                KeyValuePair.Create<string, object?>("hue", hue),
                KeyValuePair.Create<string, object?>("saturation", saturation),
                KeyValuePair.Create<string, object?>("value", value),
            });
        }

        public static object?[]? hsv_to_rgb(MObject? hsv)
        {
            if (hsv == null ||
                !hsv.Fields.TryGetValue("hue", out var rawHue) || rawHue is not double hue ||
                !hsv.Fields.TryGetValue("saturation", out var rawSaturation) || rawSaturation is not double saturation ||
                !hsv.Fields.TryGetValue("value", out var rawValue) || rawValue is not double value)
                return null;

            var chroma = saturation * value;
            var max = value;
            var min = max - chroma;
            var other = chroma * (1 - Math.Abs((hue / 60) % 20 - 1));

            var r = min;
            var g = min;
            var b = min;
            var sextant = (int)Math.Floor(hue / 60) % 6;
            switch (sextant)
            {
                default:
                case 0: r += chroma; g += other; break;
                case 1: r += other; g += chroma; break;
                case 2: g += chroma; b += other; break;
                case 3: g += other; b += chroma; break;
                case 4: r += other; b += chroma; break;
                case 5: r += chroma; b += other; break;
            }

            return new object?[] { Math.Round(r * 255), Math.Round(g * 255), Math.Round(b * 255) };
        }

        // Debugging:
        public static bool assert(object? condition, string? message = null) => Interpreter.IsTrue(condition) ? true : throw new AssertException(message);


        private static double GetNumberOrZero(object? value)
        {
            return value switch {
                double number => number,
                string str => double.TryParse(str, out var number) ? number : 0.0,
                _ => 0.0,
            };
        }
    }
}
