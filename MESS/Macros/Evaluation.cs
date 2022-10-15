﻿using MESS.Common;
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
        private static EvaluationContext _globalsContext;


        /// <summary>
        /// Returns an evaluation context that contains bindings for 'standard library' functions, for instance ID and randomness functions,
        /// and bindings for the given properties (which have been parsed into properly typed values).
        /// </summary>
        public static EvaluationContext ContextFromProperties(
            IDictionary<string, string> properties,
            double id,
            double sequenceNumber,
            Random random,
            IDictionary<string, object?> globals,
            ILogger logger,
            EvaluationContext? parentContext = null)
        {
            var typedProperties = properties.ToDictionary(
                kv => kv.Key,
                kv => PropertyExtensions.ParseProperty(kv.Value));

            var evaluationContext = new EvaluationContext(typedProperties, parentContext ?? _globalsContext);
            var instanceFunctions = new InstanceFunctions(id, sequenceNumber, random, typedProperties, globals, logger);
            NativeUtils.RegisterInstanceMethods(evaluationContext, instanceFunctions);

            return evaluationContext;
        }

        public static EvaluationContext DefaultContext() => new EvaluationContext(null, _globalsContext);


        /// <summary>
        /// Evaluates the given interpolated string. Expression parts are delimited by curly braces.
        /// For example: "name{1 + 2}" evaluates to "name3".
        /// Note that identifiers are case-sensitive: 'name' and 'NAME' do not refer to the same variable.
        /// </summary>
        public static string EvaluateInterpolatedString(string? interpolatedString, EvaluationContext context)
        {
            if (interpolatedString == null)
                return "";

            var sb = new StringBuilder();
            foreach (var part in Parser.ParseInterpolatedString(interpolatedString))
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
            // The globals context gives access to various global functions:
            _globalsContext = new EvaluationContext();
            NativeUtils.RegisterStaticMethods(_globalsContext, typeof(GlobalFunctions));

            // as well as some constants:
            _globalsContext.Bind("PI", Math.PI);
        }


        static class GlobalFunctions
        {
            // Type conversion:
            public static double? num(object? value)
            {
                if (value is double number)
                    return number;
                else if (value is object?[] array)
                    return array.Length > 0 ? num(array[0]) : null;
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

            // Colors:
            /// <summary>
            /// Returns a vector with either 3 or 4 values, where the first 3 values are between 0 and 255.
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
            private IDictionary<string, object?> _globals;
            private ILogger _logger;


            public InstanceFunctions(double id, double sequenceNumber, Random random, IDictionary<string, object?> properties, IDictionary<string, object?> globals, ILogger logger)
            {
                _id = id;
                _sequenceNumber = sequenceNumber;
                _random = random;
                _properties = properties;
                _globals = globals;
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
            public double rand(double? min = null, double? max = null)
            {
                if (min == null)        // rand():
                    return GetRandomDouble(0, 1);
                else if (max == null)   // rand(max):
                    max = 0;

                return GetRandomDouble(Math.Min(min.Value, max.Value), Math.Max(min.Value, max.Value));
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

            // Flags:
            public bool hasflag(double flag, double? flags = null)
            {
                if (flags == null)
                    flags = _properties.TryGetValue(Attributes.Spawnflags, out var val) && val is double d ? d : 0;

                var bit = (int)flags;
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

            // Globals:
            public object? getglobal(string? name) => _globals.TryGetValue(name ?? "", out var value) ? value : null;
            public object? setglobal(string? name, object? value)
            {
                _globals[name ?? ""] = value;
                return value;
            }
            public bool useglobal(string? name)
            {
                if (_globals.TryGetValue(name ?? "", out var value) && value != null)
                    return true;

                _globals[name ?? ""] = 1.0;
                return false;
            }

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
