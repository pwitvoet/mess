using MESS.Common;
using MESS.Mapping;
using MScript;
using MScript.Evaluation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace MESS.Macros
{
    static class Evaluation
    {
        // NOTE: This regex takes into account that strings inside expressions can contain curly braces:
        private static Regex _expressionRegex = new Regex(@"{(?<expression>(('[^']*')|[^}'])*)}");
        private static EvaluationContext _globalsContext;


        /// <summary>
        /// Returns an evaluation context that contains bindings for 'standard library' functions, for instance ID and randomness functions,
        /// and bindings for the given properties (which have been parsed into properly typed values).
        /// </summary>
        public static EvaluationContext ContextFromProperties(IDictionary<string, string> properties, double id, Random random, IDictionary<string, object> globals)
        {
            var evaluationContext = new EvaluationContext(
                properties?.ToDictionary(
                    kv => kv.Key,
                    kv => PropertyExtensions.ParseProperty(kv.Value)),
                _globalsContext);

            var instanceFunctions = new InstanceFunctions(id, random, globals);
            NativeUtils.RegisterInstanceMethods(evaluationContext, instanceFunctions);

            return evaluationContext;
        }


        /// <summary>
        /// Evaluates the given interpolated string. Expression parts are delimited by curly braces.
        /// For example: "name{1 + 2}" evaluates to "name3".
        /// Note that identifiers are case-sensitive: 'name' and 'NAME' do not refer to the same variable.
        /// </summary>
        public static string EvaluateInterpolatedString(string interpolatedString, EvaluationContext context)
        {
            if (interpolatedString == null)
                return null;

            return _expressionRegex.Replace(interpolatedString, match =>
            {
                var expression = match.Groups["expression"].Value;
                var result = EvaluateExpression(expression, context);
                return Interpreter.Print(result);
            });
        }

        /// <summary>
        /// Evaluates the given expression and returns the resulting value.
        /// </summary>
        public static object EvaluateExpression(string expression, EvaluationContext context)
        {
            if (string.IsNullOrEmpty(expression?.Trim()))
                return null;

            return Interpreter.Evaluate(expression, context);
        }

        /// <summary>
        /// Returns true if the given string contains one or more expressions (delimited by curly braces).
        /// </summary>
        public static bool ContainsExpressions(string interpolatedString)
            => _expressionRegex.IsMatch(interpolatedString);


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
            /// Useful to 'sanitize' colors.
            /// </summary>
            public static double[] color(double[] color)
            {
                if (color == null)
                    return new double[] { 0, 0, 0 };

                if (color.Length < 3)
                    color = color.Concat(Enumerable.Repeat(0.0, 3 - color.Length)).ToArray();
                else if (color.Length > 4)
                    color = color.Take(4).ToArray();

                for (int i = 0; i < 3; i++)
                    color[i] = clamp(Math.Round(color[i]), 0, 255);

                if (color.Length > 3)
                    color[3] = Math.Round(color[3]);

                return color;
            }

            // Flags:
            public static bool hasflag(EvaluationContext context, double flag, double? flags = null)
            {
                if (flags == null)
                    flags = (context.Resolve(Attributes.Spawnflags) is double d) ? d : 0;

                var bit = (int)flag;
                if (bit < 0 || bit > 31)
                    return false;

                return (((int)flags >> bit) & 1) == 1;
            }
            public static double setflag(EvaluationContext context, double flag, double? set = 1, double? flags = null)
            {
                if (flags == null)
                    flags = (context.Resolve(Attributes.Spawnflags) is double d) ? d : 0;

                if (set != 0)
                    return (int)flags | (1 << (int)flag);
                else
                    return (int)flags & ~(1 << (int)flag);
            }
        }


        class InstanceFunctions
        {
            private double _id;
            private Random _random;
            private IDictionary<string, object> _globals;


            public InstanceFunctions(double id, Random random, IDictionary<string, object> globals)
            {
                _id = id;
                _random = random;
                _globals = globals;
            }


            // Entity ID:
            public string id(EvaluationContext context)
            {
                var targetname = context.Resolve(Attributes.Targetname);
                return (targetname != null) ? Interpreter.Print(targetname) : _id.ToString(CultureInfo.InvariantCulture);
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

            // Globals:
            public object getglobal(string name) => _globals.TryGetValue(name, out var value) ? value : null;
            public object setglobal(string name, object value)
            {
                _globals[name] = value;
                return value;
            }
            public bool useglobal(string name)
            {
                if (_globals.TryGetValue(name, out var value) && value != null)
                    return true;

                _globals[name] = 1.0;
                return false;
            }


            private double GetRandomDouble(double min, double max) => min + _random.NextDouble() * (max - min);
            private int GetRandomInteger(int min, int max) => _random.Next(min, max);
        }
    }
}
