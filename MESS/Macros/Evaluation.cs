﻿using MESS.Mapping;
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


        public static EvaluationContext ContextFromProperties(IDictionary<string, string> properties)
        {
            return new EvaluationContext(
                properties?.ToDictionary(
                    kv => kv.Key,
                    kv => PropertyExtensions.ParseProperty(kv.Value)),
                _globalsContext);
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
            RegisterMembers(_globalsContext, typeof(GlobalFunctions));

            // as well as some constants:
            _globalsContext.Bind("PI", Math.PI);
        }

        private static void RegisterMembers(EvaluationContext context, Type functionsContainer)
        {
            foreach (var method in functionsContainer.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var function = NativeUtils.CreateFunction(method);
                context.Bind(function.Name, function);
            }
        }


        static class GlobalFunctions
        {
            public static double min(double value1, double value2) => Math.Min(value1, value2);
            public static double max(double value1, double value2) => Math.Max(value1, value2);
            public static double clamp(double value, double min, double max) => Math.Max(min, Math.Min(value, max));
            public static double abs(double value) => Math.Abs(value);
            public static double floor(double value) => Math.Floor(value);
            public static double ceil(double value) => Math.Ceiling(value);

            public static double pow(double value, double power) => Math.Pow(value, power);
            public static double sqrt(double value) => Math.Sqrt(value);

            public static double sin(double radians) => Math.Sin(radians);
            public static double cos(double radians) => Math.Cos(radians);
            public static double tan(double radians) => Math.Tan(radians);
            public static double asin(double sine) => Math.Asin(sine);
            public static double acos(double cosine) => Math.Acos(cosine);
            public static double atan(double tangent) => Math.Atan(tangent);
            public static double atan2(double y, double x) => Math.Atan2(y, x);

            public static double deg2rad(double degrees) => (degrees / 180.0) * Math.PI;
            public static double rad2deg(double radians) => (radians / Math.PI) * 180.0;


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
                    color[i] = clamp(color[i], 0, 255);
                return color;
            }
        }
    }
}
