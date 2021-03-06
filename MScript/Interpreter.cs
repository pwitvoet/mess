﻿using MScript.Evaluation;
using MScript.Parsing;
using MScript.Tokenizing;
using System;

namespace MScript
{
    public static class Interpreter
    {
        /// <summary>
        /// Parses and evaluates the given expression.
        /// </summary>
        /// <exception cref="ParseException" />
        /// <exception cref="InvalidOperationException" />
        public static object Evaluate(string input, EvaluationContext context)
        {
            var tokens = Tokenizer.Tokenize(input);
            var expression = Parser.Parse(tokens);
            return Evaluator.Evaluate(expression, context);
        }

        /// <summary>
        /// Returns a string representation of the given value.
        /// </summary>
        public static string Print(object value) => Operations.ToString(value);

        /// <summary>
        /// Returns whether the given value is true in a boolean context.
        /// Only `none` (null) is considered to be false.
        /// </summary>
        public static bool IsTrue(object value) => Operations.IsTrue(value);
    }
}
