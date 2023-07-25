using MESS.Common;
using MESS.Logging;
using MESS.Mapping;
using MScript;
using MScript.Evaluation;
using MScript.Parsing;
using System.Globalization;
using System.Text;

namespace MESS.Macros
{
    static class Evaluation
    {
        private static EvaluationContext _standardLibraryContext;


        /// <summary>
        /// Creates an evaluation context that contains bindings for standard library functions, for instance ID and randomness functions,
        /// and bindings for the given properties (which have been parsed into properly typed values).
        /// </summary>
        public static EvaluationContext ContextWithBindings(
            IDictionary<string, object?> bindings,
            double id,
            double sequenceNumber,
            Random random,
            string mapPath,
            ILogger logger,
            EvaluationContext? parentContext)
        {
            var evaluationContext = new EvaluationContext(bindings, parentContext ?? _standardLibraryContext);
            var instanceFunctions = new InstanceFunctions(id, sequenceNumber, random, bindings, mapPath, logger);
            NativeUtils.RegisterInstanceMethods(evaluationContext, instanceFunctions);

            return evaluationContext;
        }

        /// <summary>
        /// Creates an evaluation context that contains bindings for standard library functions.
        /// </summary>
        public static EvaluationContext DefaultContext() => new EvaluationContext(null, _standardLibraryContext);


        /// <summary>
        /// Evaluates the given interpolated string. Expression parts are delimited by curly braces.
        /// For example: "name{1 + 2}" evaluates to "name3".
        /// Note that identifiers are case-sensitive: 'name' and 'NAME' do not refer to the same variable.
        /// Always returns a string.
        /// </summary>
        public static string EvaluateInterpolatedString(string? interpolatedString, EvaluationContext context)
        {
            if (interpolatedString == null)
                return "";

            try
            {
                return EvaluateInterpolatedStringParts(Parser.ParseInterpolatedString(interpolatedString), context);
            }
            catch (Exception ex)
            {
                ex.Data["input"] = interpolatedString;
                throw;
            }
        }

        /// <inheritdoc cref="EvaluateInterpolatedString(string?, EvaluationContext)"/>
        public static string EvaluateInterpolatedString(string? interpolatedString, InstantiationContext context)
            => EvaluateInterpolatedString(interpolatedString, context.EvaluationContext);

        /// <summary>
        /// Evaluates the given interpolated string. Expression parts are delimited by curly braces.
        /// For example: "name{1 + 2}" evaluates to "name3".
        /// Note that identifiers are case-sensitive: 'name' and 'NAME' do not refer to the same variable.
        /// Returns an MScript value.
        /// <para>
        /// For inputs that consist of a single expression ("{expression}"), the result of that expression (an MScript value) is returned directly.
        /// </para>
        /// <para>
        /// For mixed inputs ("name{expression}"), or inputs that contain no expressions ("name"), the resulting string will be converted if possible.
        /// An empty string is converted to 'none' (null), a numerical string is converted to a number (double),
        /// and a string that consists of a sequence of numbers, separated by whitespace, is converted to an array (object?[]).
        /// Any other output is left as a string.
        /// </para>
        /// </summary>
        public static object? EvaluateInterpolatedStringOrExpression(string? interpolatedString, EvaluationContext context)
        {
            if (interpolatedString == null)
                return null;

            try
            {
                var parts = Parser.ParseInterpolatedString(interpolatedString).ToArray();
                if (parts.Count(part => part is Expression) == 1 && parts.All(part => part is Expression || part is string str && string.IsNullOrWhiteSpace(str)))
                {
                    var singleExpression = parts.OfType<Expression>().Single();
                    return Evaluator.Evaluate(singleExpression, context);
                }
                else
                {
                    var value = EvaluateInterpolatedStringParts(parts, context);
                    return ParseMScriptValue(value);
                }
            }
            catch (Exception ex)
            {
                ex.Data["input"] = interpolatedString;
                throw;
            }
        }

        /// <inheritdoc cref="EvaluateInterpolatedStringOrExpression(string?, EvaluationContext)"/>
        public static object? EvaluateInterpolatedStringOrExpression(string? interpolatedString, InstantiationContext context)
            => EvaluateInterpolatedStringOrExpression(interpolatedString, context.EvaluationContext);

        /// <summary>
        /// Parses the string into an MScript value. Returns 'none' (null) for an empty string, a number (double) for a numerical string,
        /// and an array (object?[]) for a string that consists of multiple numbers separated by whitespace. Returns the original string for all other inputs.
        /// </summary>
        public static object? ParseMScriptValue(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            if (PropertyExtensions.TryParseDouble(value, out var number))
                return number;

            if (PropertyExtensions.TryParseNumericalArray(value, out var array))
                return array.Cast<object?>().ToArray();

            return value;
        }


        private static string EvaluateInterpolatedStringParts(IEnumerable<object?> parts, EvaluationContext context)
        {
            var sb = new StringBuilder();
            foreach (var part in parts)
            {
                if (part is string str)
                {
                    sb.Append(str);
                }
                else if (part is Expression expression)
                {
                    try
                    {
                        var result = Evaluator.Evaluate(expression, context);
                        sb.Append(Interpreter.Print(result));
                    }
                    catch (Exception ex)
                    {
                        ex.Data["expression"] = expression;
                        throw;
                    }
                }
            }
            return sb.ToString();
        }


        static Evaluation()
        {
            // The globals context gives access standard library functions:
            _standardLibraryContext = new EvaluationContext();
            NativeUtils.RegisterStaticMethods(_standardLibraryContext, typeof(StandardLibraryFunctions));

            // as well as some constants:
            _standardLibraryContext.Bind("PI", Math.PI);
        }


        static class StandardLibraryFunctions
        {
            // Type checks:
            public static bool is_num(object? value) => value is double;

            public static bool is_str(object? value) => value is string;

            public static bool is_array(object? value) => value is object?[];

            public static bool is_obj(object? value) => value is MObject;

            public static bool is_func(object? value) => value is IFunction;

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
                    result[i] = clamp(Math.Round(GetNumber(i)), 0, 255);

                if (result.Length > 3)
                    result[3] = Math.Round(GetNumber(3));

                return result;

                double GetNumber(int index) => color[index] switch {
                    double number => number,
                    string str => double.TryParse(str, out var number) ? number : 0.0,
                    _ => 0.0,
                };
            }

            // Debugging:
            public static bool assert(object? condition, string? message = null) => Interpreter.IsTrue(condition) ? true : throw new AssertException(message);
        }


        class InstanceFunctions
        {
            private double _id;
            private double _sequenceNumber;
            private Random _random;
            private IDictionary<string, object?> _properties;
            private string _mapPath;
            private ILogger _logger;


            public InstanceFunctions(double id, double sequenceNumber, Random random, IDictionary<string, object?> properties, string mapPath, ILogger logger)
            {
                _id = id;
                _sequenceNumber = sequenceNumber;
                _random = random;
                _properties = properties;
                _mapPath = mapPath;
                _logger = logger;
            }


            // Entity ID:
            public string id()
            {
                if (_properties.TryGetValue(Attributes.Targetname, out var targetname))
                {
                    var name = Interpreter.Print(targetname);
                    if (name != "")
                        return name;
                }

                return _id.ToString(CultureInfo.InvariantCulture);
            }

            public double iid() => _id;

            // Randomness:
            public object? rand(object? min = null, double? max = null)
            {
                if (min is object?[] array)
                    return array[GetRandomInteger(0, array.Length)];
                else if (min is string str)
                    return str.Substring(GetRandomInteger(0, str.Length), 1);
                else if (min is not null && min is not double)
                    throw new InvalidOperationException($"Parameter '{nameof(min)}' must be an array, a string or a number."); // TODO: Generalize exceptions like this, or create a parameter-type based dispatch system!

                var minValue = min is double number ? number : 0.0;
                var maxValue = max ?? (min is null ? 1.0 : 0.0);
                return GetRandomDouble(Math.Min(minValue, maxValue), Math.Max(minValue, maxValue));
            }

            public double randi(double? min = null, double? max = null)
            {
                if (min == null)
                    return GetRandomInteger(0, 2);
                else if (max == null)
                    max = 0;

                return GetRandomInteger((int)Math.Min(min.Value, max.Value), (int)Math.Max(min.Value, max.Value));
            }

            // Enumeration:
            public double nth() => _sequenceNumber;

            // Parent entity attributes:
            public double attr_count() => _properties.Count;

            public object? get_attr(object? index_or_name = null)
            {
                if (index_or_name is double index)
                {
                    var normalizedIndex = (int)index < 0 ? _properties.Count + (int)index : (int)index;
                    if (normalizedIndex < 0 || normalizedIndex >= _properties.Count)
                        return null;

                    var property = _properties.ToArray()[normalizedIndex];
                    return new MObject(new Dictionary<string, object?> {
                        { "key", property.Key },
                        { "value", property.Value },
                    });
                }
                else if (index_or_name is string name)
                {
                    if (!_properties.TryGetValue(name, out var value))
                        return null;

                    return new MObject(new Dictionary<string, object?> {
                        { "key", name },
                        { "value", value },
                    });
                }
                else
                {
                    return _properties
                        .Select(property => new MObject(new Dictionary<string, object?> {
                            { "key", property.Key },
                            { "value", property.Value },
                        }))
                        .ToArray();
                }
            }

            // Flags:
            public bool hasflag(double flag, double? flags = null)
            {
                if (flags == null)
                    flags = _properties.TryGetValue(Attributes.Spawnflags, out var val) && val is double d ? d : 0;

                var bit = (int)flag;
                if (bit < 0 || bit > 31)
                    return false;

                return (((int)flags >> bit) & 1) == 1;
            }

            public double setflag(double flag, double? set = 1, double? flags = null)
            {
                if (flags == null)
                    flags = _properties.TryGetValue(Attributes.Spawnflags, out var val) && val is double d ? d : 0;

                if (set != 0)
                    return (int)flags | (1 << (int)flag);
                else
                    return (int)flags & ~(1 << (int)flag);
            }

            // Current map path:
            public string map_path() => _mapPath;

            public string map_dir() => Path.GetDirectoryName(_mapPath) ?? "";

            // Debugging:
            public object? trace(object? value, string? message = null)
            {
                _logger.Info($"'{Interpreter.Print(value)}' ('{message}', trace from instance: #{_id}, sequence number: #{_sequenceNumber}).");
                return value;
            }


            private double GetRandomDouble(double min, double max) => min + _random.NextDouble() * (max - min);

            private int GetRandomInteger(int min, int max) => _random.Next(min, max);
        }
    }
}
