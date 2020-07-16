using MScript.Evaluation;
using MScript.Parsing;
using MScript.Tokenizing;

namespace MScript
{
    public static class Interpreter
    {
        /// <summary>
        /// Parses and evaluates the given expression.
        /// </summary>
        /// <exception cref="ParseException" />
        public static object Evaluate(string input, EvaluationContext context)
        {
            var tokens = Tokenizer.Tokenize(input);
            var expression = Parser.Parse(tokens);
            return Evaluator.Evaluate(expression, context);
        }

        public static string Print(object value) => Operations.ToString(value);
    }
}
