using MESS.Mapping;
using MScript;
using MScript.Evaluation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        public static EvaluationContext ContextFromProperties(IDictionary<string, string> properties, double id, Random random)
        {
            var evaluationContext = new EvaluationContext(
                properties?.ToDictionary(
                    kv => kv.Key,
                    kv => PropertyExtensions.ParseProperty(kv.Value)),
                _globalsContext);

            var instanceFunctions = new InstanceFunctions(id, random);
            RegisterInstanceMethods(evaluationContext, instanceFunctions);

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
            RegisterStaticMethods(_globalsContext, typeof(GlobalFunctions));

            // as well as some constants:
            _globalsContext.Bind("PI", Math.PI);
        }

        private static void RegisterStaticMethods(EvaluationContext context, Type functionsContainer)
        {
            foreach (var method in functionsContainer.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var function = NativeUtils.CreateFunction(method);
                context.Bind(function.Name, function);
            }
        }

        private static void RegisterInstanceMethods(EvaluationContext context, object instance)
        {
            foreach (var method in instance.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var function = NativeUtils.CreateFunction(method, instance);
                context.Bind(function.Name, function);
            }
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
        }


        class InstanceFunctions
        {
            private double _id;
            private Random _random;


            public InstanceFunctions(double id, Random random)
            {
                _id = id;
                _random = random;
            }


            // Entity ID:
            public string id(EvaluationContext context) => context.Resolve("targetname")?.ToString() ?? _id.ToString();
            public double iid() => _id;

            // Randomness:
            public double rand(double? min, double? max)
            {
                if (min == null)        // rand():
                    return GetRandomDouble(0, 1);
                else if (max == null)   // rand(max):
                    max = 0;

                return GetRandomDouble(Math.Min(min.Value, max.Value), Math.Max(min.Value, max.Value));
            }
            public double randi(double? min, double? max)
            {
                if (min == null)
                    return GetRandomInteger(0, 2);
                else if (max == null)
                    max = 0;

                return GetRandomInteger((int)Math.Min(min.Value, max.Value), (int)Math.Max(min.Value, max.Value));
            }


            private double GetRandomDouble(double min, double max) => min + _random.NextDouble() * (max - min);
            private int GetRandomInteger(int min, int max) => _random.Next(min, max);
        }
    }
}
