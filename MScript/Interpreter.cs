using MScript.Evaluation;
using MScript.Parsing;
using MScript.Tokenizing;
using System.Text;

namespace MScript
{
    public static class Interpreter
    {
        /// <summary>
        /// Parses and evaluates the given expression.
        /// </summary>
        /// <exception cref="ParseException" />
        /// <exception cref="EvaluationException" />
        /// <exception cref="InvalidOperationException" />
        public static object? Evaluate(string input, EvaluationContext context)
        {
            var tokens = Tokenizer.Tokenize(input);
            var expression = Parser.ParseExpression(tokens);
            return Evaluator.Evaluate(expression, context);
        }

        /// <summary>
        /// Returns a string representation of the given value.
        /// </summary>
        public static string Print(object? value) => Operations.ToString(value);

        /// <summary>
        /// Returns whether the given value is true in a boolean context.
        /// Only `none` (null) is considered to be false.
        /// </summary>
        public static bool IsTrue(object? value) => Operations.IsTrue(value);

        /// <inheritdoc cref="LoadAssignmentsFile(Stream, EvaluationContext)"/>
        public static void LoadAssignmentsFile(string path, EvaluationContext context)
        {
            using (var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                LoadAssignmentsFile(file, context);
        }

        /// <summary>
        /// Evaluates all assignments in the given .mscript file, creating or updating bindings in the given context.
        /// </summary>
        public static void LoadAssignmentsFile(Stream file, EvaluationContext context)
        {
            using (var reader = new StreamReader(file, Encoding.UTF8, leaveOpen: true))
            {
                var code = reader.ReadToEnd();
                var tokens = Tokenizer.Tokenize(code);
                var assignments = Parser.ParseAssignments(tokens)
                    .ToArray();

                foreach (var assignment in assignments)
                {
                    var value = Evaluator.Evaluate(assignment.Value, context);
                    context.Bind(assignment.Identifier, value);
                }
            }
        }
    }
}
