using MESS.Mapping;
using MScript;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MESS.Macros
{
    static class Evaluation
    {
        // NOTE: This regex takes into account that strings inside expressions can contain curly braces:
        private static Regex _expressionRegex = new Regex(@"{(?<expression>(('[^']*')|[^}'])*)}");


        public static EvaluationContext ContextFromProperties(IDictionary<string, string> properties)
        {
            return new EvaluationContext(properties?.ToDictionary(
                  kv => kv.Key,
                  kv => PropertyExtensions.ParseProperty(kv.Value)));
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
    }
}
