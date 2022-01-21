using MScript.Parsing.AST;
using MScript.Tokenizing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MScript.Parsing
{
    public static class Parser
    {
        /// <summary>
        /// Parses an MScript expression.
        /// </summary>
        public static Expression ParseExpression(IEnumerable<Token> tokens)
        {
            var context = new Context(tokens);
            while (!context.IsExhausted)
            {
                Shift(context);
                while (Reduce(context)) ;
            }

            if (context.ParseStack.Count != 1 || !(context.ParseStack[0] is Expression expression))
                throw ParseError("Invalid expression.", context);

            return expression;
        }

        /// <summary>
        /// Parses a sequence of MScript variable assignments.
        /// </summary>
        public static IEnumerable<Assignment> ParseAssignments(IEnumerable<Token> tokens)
        {
            var context = new Context(tokens);
            while (!context.IsExhausted)
            {
                Shift(context);
                while (ReduceVariables()) ;
            }

            if (context.ParseStack.Count != 1 || !(context.ParseStack[0] is Assignment[] assignments))
                throw ParseError("Invalid assignments sequence.", context);

            return assignments;


            // This reduces assignments in addition to normal expressions:
            bool ReduceVariables()
            {
                while (Reduce(context)) ;

                if (context.Stack(-4) is Variable variable &&
                    context.IsToken(-3, TokenType.SingleEquals) &&
                    context.Stack(-2) is Expression expression &&
                    context.IsToken(-1, TokenType.Semicolon))
                {
                    // assignments: variable '=' expression ';'
                    context.ReplaceLast(4, new Assignment[] { new Assignment(variable.Name, expression) });
                    return true;
                }
                else if (context.Stack(-2) is Assignment[] headAssignments &&
                    context.Stack(-1) is Assignment[] tailAssignments)
                {
                    // assignments: assignments assignments
                    context.ReplaceLast(2, headAssignments.Concat(tailAssignments).ToArray());
                    return true;
                }

                return false;
            }
        }

        //public static void ParseInterpolatedString() { }  // This could offer more robust parsing for interpolated strings than a simple regex!


        private static void Shift(Context context)
        {
            context.ParseStack.Add(context.CurrentToken);
            context.MoveNext();
        }

        private static bool Reduce(Context context)
        {
            var parseStack = context.ParseStack;
            if (!parseStack.Any())
                return false;

            if (parseStack.Last() is Token token)
            {
                switch (token.Type)
                {
                    case TokenType.Number: return ReduceNumber(context, token);
                    case TokenType.String: return ReduceString(context, token);
                    case TokenType.Identifier: return ReduceIdentifier(context, token);
                    case TokenType.ParensClose: return ReduceParensClose(context, token);
                    case TokenType.BracketClose: return ReduceBracketClose(context, token);
                    case TokenType.None: return ReduceNone(context, token);

                    default:
                        return false;
                }
            }
            else if (parseStack.Last() is Expression expression)
            {
                return ReduceExpression(context, expression);
            }

            return false;
        }


        private static bool ReduceNumber(Context context, Token token)
        {
            Assert(token.Type == TokenType.Number);

            // literal: <number>
            context.ReplaceLast(1, new NumberLiteral(double.Parse(token.Value, CultureInfo.InvariantCulture)));
            return true;
        }

        private static bool ReduceString(Context context, Token token)
        {
            Assert(token.Type == TokenType.String);

            // literal: <string>
            context.ReplaceLast(1, new StringLiteral(token.Value));
            return true;
        }

        private static bool ReduceIdentifier(Context context, Token token)
        {
            Assert(token.Type == TokenType.Identifier);

            if (context.Stack(-3) is Expression expression &&
                context.IsToken(-2, TokenType.Period))
            {
                // member-access: expression '.' <identifier>
                context.ReplaceLast(3, new MemberAccess(expression, token.Value));
                return true;
            }
            else
            {
                // variable: <identifier>
                context.ReplaceLast(1, new Variable(token.Value));
                return true;
            }
        }

        private static bool ReduceParensClose(Context context, Token token)
        {
            Assert(token.Type == TokenType.ParensClose);

            if (context.Stack(-3) is Expression function &&
                context.IsToken(-2, TokenType.ParensOpen))
            {
                // function-call: expression '(' ')'
                context.ReplaceLast(3, new FunctionCall(function, Array.Empty<Expression>()));
                return true;
            }
            else if (context.Stack(-4) is Expression function2 &&
                context.IsToken(-3, TokenType.ParensOpen) &&
                context.Stack(-2) is ArgumentsList argumentsList)
            {
                // function-call: expression '(' arguments-list ')'
                context.ReplaceLast(4, new FunctionCall(function2, argumentsList.Arguments));
                return true;
            }
            else if (context.IsToken(-3, TokenType.ParensOpen) &&
                context.Stack(-2) is Expression expression)
            {
                // expression: '(' expression ')'
                context.ReplaceLast(3, expression);
                return true;
            }

            return false;
        }

        private static bool ReduceBracketClose(Context context, Token token)
        {
            Assert(token.Type == TokenType.BracketClose);

            if (context.Stack(-4) is Expression indexable &&
                context.IsToken(-3, TokenType.BracketOpen) &&
                context.Stack(-2) is Expression index)
            {
                // indexing: expression '[' expression ']'
                context.ReplaceLast(4, new Indexing(indexable, index));
                return true;
            }
            else if (context.IsToken(-3, TokenType.BracketOpen) &&
                context.Stack(-2) is ArgumentsList argumentsList)
            {
                // literal: '[' arguments-list ']'
                context.ReplaceLast(3, new VectorLiteral(argumentsList.Arguments));
                return true;
            }
            else if (context.IsToken(-2, TokenType.BracketOpen))
            {
                // literal: '[' ']'
                context.ReplaceLast(2, new VectorLiteral(Array.Empty<Expression>()));
                return true;
            }

            return false;
        }

        private static bool ReduceNone(Context context, Token token)
        {
            Assert(token.Type == TokenType.None);

            // literal: 'none'
            context.ReplaceLast(1, new NoneLiteral());
            return true;
        }

        private static bool ReduceExpression(Context context, Expression expression)
        {
            // NOTE: Function-call, indexing and member-access have higher precedence than binary and unary operations:
            if (context.CurrentToken.Type == TokenType.ParensOpen ||
                context.CurrentToken.Type == TokenType.BracketOpen ||
                context.CurrentToken.Type == TokenType.Period)
                return false;


            if (context.Stack(-3) is Expression leftOperand &&
                GetBinaryOperator(context.Stack(-2)) is BinaryOperator binaryOperator)
            {
                // binary-operation: expression <binary-operator> expression

                // Postpone reducing if there's a binary operation coming up with a higher precedence:
                if (GetBinaryOperator(context.CurrentToken) is BinaryOperator nextOperator &&
                    GetPrecedence(nextOperator) > GetPrecedence(binaryOperator))
                    return false;

                context.ReplaceLast(3, new BinaryOperation(binaryOperator, leftOperand, expression));
                return true;
            }
            else if (GetUnaryOperator(context.Stack(-2)) is UnaryOperator unaryOperator)
            {
                // unary-operation: <unary-operator> expression
                context.ReplaceLast(2, new UnaryOperation(unaryOperator, expression));
                return true;
            }
            else if (context.Stack(-3) is ArgumentsList argumentsList &&
                context.IsToken(-2, TokenType.Comma) &&
                (context.CurrentToken.Type == TokenType.Comma ||            // Comma has a lower precedence than anything else!
                 context.CurrentToken.Type == TokenType.ParensClose ||
                 context.CurrentToken.Type == TokenType.BracketClose))
            {
                // arguments-list: arguments-list ',' expression
                context.ReplaceLast(3, argumentsList);
                argumentsList.Arguments.Add(expression);
                return true;
            }
            else if (context.Stack(-3) is Expression function &&
                context.IsToken(-2, TokenType.ParensOpen) &&
                (context.CurrentToken.Type == TokenType.ParensClose || context.CurrentToken.Type == TokenType.Comma))
            {
                // arguments-list: expression       // For function calls
                context.ReplaceLast(1, new ArgumentsList(expression));
                return true;
            }
            else if (!(context.Stack(-3) is Expression) &&
                context.IsToken(-2, TokenType.BracketOpen) &&
                (context.CurrentToken.Type == TokenType.BracketClose || context.CurrentToken.Type == TokenType.Comma))
            {
                // arguments-list: expression       // For vector literals
                context.ReplaceLast(1, new ArgumentsList(expression));
                return true;
            }
            else if (context.Stack(-5) is Expression condition &&
                context.IsToken(-4, TokenType.QuestionMark) &&
                context.Stack(-3) is Expression trueExpression &&
                context.IsToken(-2, TokenType.Colon))
            {
                // conditional-operation: expression '?' expression ':' expression
                context.ReplaceLast(5, new ConditionalOperation(condition, trueExpression, expression));
                return true;
            }
            else if (context.Stack(-5) is Expression trueExpression2 &&
                context.IsToken(-4, TokenType.If) &&
                context.Stack(-3) is Expression condition2 &&
                context.IsToken(-2, TokenType.Else))
            {
                // conditional-operation: expression 'if' expression 'else' expression
                context.ReplaceLast(5, new ConditionalOperation(condition2, trueExpression2, expression));
                return true;
            }

            return false;
        }


        private static BinaryOperator? GetBinaryOperator(object element)
        {
            if (!(element is Token token))
                return null;

            switch (token.Type)
            {
                case TokenType.Plus: return BinaryOperator.Add;
                case TokenType.Minus: return BinaryOperator.Subtract;
                case TokenType.Asterisk: return BinaryOperator.Multiply;
                case TokenType.Slash: return BinaryOperator.Divide;
                case TokenType.PercentageSign: return BinaryOperator.Remainder;
                case TokenType.Equals: return BinaryOperator.Equals;
                case TokenType.NotEquals: return BinaryOperator.NotEquals;
                case TokenType.GreaterThan: return BinaryOperator.GreaterThan;
                case TokenType.GreaterThanOrEqual: return BinaryOperator.GreaterThanOrEqual;
                case TokenType.LessThan: return BinaryOperator.LessThan;
                case TokenType.LessThanOrEqual: return BinaryOperator.LessThanOrEqual;

                case TokenType.DoubleAmpersand:
                case TokenType.And:
                    return BinaryOperator.And;

                case TokenType.DoubleBar:
                case TokenType.Or:
                    return BinaryOperator.Or;

                default: return null;
            }
        }

        private static UnaryOperator? GetUnaryOperator(object element)
        {
            if (!(element is Token token))
                return null;

            switch (token.Type)
            {
                case TokenType.Minus:
                    return UnaryOperator.Negate;

                case TokenType.ExclamationMark:
                case TokenType.Not:
                    return UnaryOperator.LogicalNegate;

                default: return null;
            }
        }

        private static int GetPrecedence(BinaryOperator @operator)
        {
            switch (@operator)
            {
                case BinaryOperator.Multiply:
                case BinaryOperator.Divide:
                case BinaryOperator.Remainder:
                    return 6;

                case BinaryOperator.Add:
                case BinaryOperator.Subtract:
                    return 5;

                case BinaryOperator.GreaterThan:
                case BinaryOperator.GreaterThanOrEqual:
                case BinaryOperator.LessThan:
                case BinaryOperator.LessThanOrEqual:
                    return 4;

                case BinaryOperator.Equals:
                case BinaryOperator.NotEquals:
                    return 3;

                case BinaryOperator.And:
                    return 2;

                case BinaryOperator.Or:
                    return 1;

                default:
                    return 0;
            }
        }


        private static bool IsToken(object obj, TokenType type) => obj is Token token && token.Type == type;

        private static ParseException ParseError(string message, Context context)
        {
            return new ParseException(message, 0);  // TODO: Maybe possible to extract position from context?
        }

        private static void Assert(bool condition)
        {
            if (!condition)
                throw new InvalidOperationException();
        }


        class Context : IDisposable
        {
            public List<object> ParseStack { get; } = new List<object>();
            public Token CurrentToken => _tokens.Current;
            public bool IsExhausted { get; private set; }


            private IEnumerator<Token> _tokens;


            public Context(IEnumerable<Token> tokens)
            {
                _tokens = tokens.GetEnumerator();
                MoveNext();
            }

            public void Dispose() => _tokens.Dispose();

            public bool MoveNext()
            {
                IsExhausted = !_tokens.MoveNext();
                return !IsExhausted;
            }


            /// <summary>
            /// Returns the nth element in the parse stack.
            /// Negative indexes start from the end, where -1 is the last element, -2 the next-to-last, and so on.
            /// Returns null if the index is out of range.
            /// </summary>
            public object Stack(int index)
            {
                index = (index < 0) ? ParseStack.Count + index : index;
                if (index < 0 || index >= ParseStack.Count)
                    return null;

                return ParseStack[index];
            }

            /// <summary>
            /// Returns true if the nth element in the parse stack is a token of the specified type.
            /// Negative indexes start from the end, where -1 is the last element, -2 the next-to-last, and so on.
            /// Returns false if the index is out of range.
            /// </summary>
            public bool IsToken(int index, TokenType type) => Stack(index) is Token token && token.Type == type;

            /// <summary>
            /// Removes the last n elements from the stack, then adds the given element.
            /// </summary>
            public void ReplaceLast(int count, object element)
            {
                ParseStack.RemoveRange(ParseStack.Count - count, count);
                ParseStack.Add(element);
            }
        }
    }
}
