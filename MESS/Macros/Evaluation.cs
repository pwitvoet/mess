using MESS.Common;
using MESS.EntityRewriting;
using MESS.Logging;
using MESS.Macros.Functions;
using MESS.Mapping;
using MScript;
using MScript.Evaluation;
using MScript.Evaluation.Types;
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
            double parentID,
            double sequenceNumber,
            Random random,
            string mapPath,
            ILogger logger,
            EvaluationContext? parentContext)
        {
            var evaluationContext = new EvaluationContext(bindings, parentContext ?? _standardLibraryContext);
            NativeUtils.RegisterInstanceMethods(evaluationContext, new StandardMacroExpansionFunctions(bindings, mapPath, random, logger));
            NativeUtils.RegisterInstanceMethods(evaluationContext, new InstanceFunctions(id, parentID, sequenceNumber, bindings, logger));
            return evaluationContext;
        }

        /// <summary>
        /// Creates an evaluation context that contains a bindings for functions related to the current sequence number, such as 'nth()'.
        /// </summary>
        public static EvaluationContext ContextWithSequenceNumber(
            double id,
            double sequenceNumber,
            ILogger logger,
            EvaluationContext parentContext)
        {
            var evaluationContext = new EvaluationContext(null, parentContext);
            NativeUtils.RegisterInstanceMethods(evaluationContext, new SequenceNumberFunctions(id, sequenceNumber, logger));
            return evaluationContext;
        }

        /// <summary>
        /// Creates an evaluation context that contains bindings for standard library functions,
        /// randomness functions and bindings for the given properties (which have been parsed into properly typed values).
        /// </summary>
        public static EvaluationContext RewriteRuleContextWithBindings(
            IDictionary<string, object?> bindings,
            Random random,
            string mapPath,
            string tedFilePath,
            string[] templateEntityDirectories,
            ILogger logger,
            EvaluationContext? parentContext)
        {
            var evaluationContext = new EvaluationContext(bindings, parentContext ?? _standardLibraryContext);
            NativeUtils.RegisterInstanceMethods(evaluationContext, new StandardMacroExpansionFunctions(bindings, mapPath, random, logger));
            NativeUtils.RegisterInstanceMethods(evaluationContext, new RewriteDirectiveFunctions(tedFilePath, templateEntityDirectories, logger));
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
    }
}
