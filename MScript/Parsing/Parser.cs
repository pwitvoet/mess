using MScript.Parsing.AST;
using MScript.Tokenizing;
using System.Globalization;
using System.Numerics;

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

            context.ParseStack.RemoveAt(context.ParseStack.Count - 1);  // Remove end-of-input

            if (context.ParseStack.Count != 1 || context.ParseStack[0] is not Expression expression)
                throw ParseError("Invalid expression.", context);

            return expression;
        }

        /// <summary>
        /// Parses a sequence of MScript variable assignments.
        /// </summary>
        public static IEnumerable<Assignment> ParseAssignments(IEnumerable<Token> tokens, bool lastSemicolonRequired = true)
        {
            if (!lastSemicolonRequired)
            {
                // Insert a semicolon just before the end:
                var tokensList = tokens.ToList();
                if (tokensList.Count > 0)
                {
                    var endOfInputToken = tokensList.Last();
                    tokensList.Insert(tokensList.Count - 1, new Token(TokenType.Semicolon, endOfInputToken.Position));
                }
                tokens = tokensList;
            }

            var context = new Context(tokens);
            while (!context.IsExhausted)
            {
                Shift(context);
                while (ReduceVariables()) ;
            }

            context.ParseStack.RemoveAt(context.ParseStack.Count - 1);  // Remove end-of-input

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
                else if (context.Stack(-2) is Assignment[] assignments &&
                    context.IsToken(-1, TokenType.Semicolon))
                {
                    // assignments: assignments ';'
                    context.ReplaceLast(2, assignments);
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Parses the given interpolated string. Any part surrounded by curly braces ({ and }) is parsed as an MScript expression.
        /// The returned sequence alternatingly consists of strings and <see cref="Expression"/> objects.
        /// </summary>
        public static IEnumerable<object> ParseInterpolatedString(string input)
        {
            var offset = 0;
            while (offset < input.Length)
            {
                var nextBraceIndex = input.IndexOf('{', offset);
                if (nextBraceIndex == -1)
                {
                    if (input.Length > offset)
                        yield return input.Substring(offset);
                    yield break;
                }
                else if (nextBraceIndex > offset)
                {
                    yield return input.Substring(offset, nextBraceIndex - offset);
                }

                // Look for the end of the current interpolated expression part:
                var insideString = false;
                var endBraceFound = false;
                var openBraceCount = 1;
                for (int i = nextBraceIndex + 1; i < input.Length; i++)
                {
                    if (!insideString && input[i] == '\'')
                    {
                        // Find end of string (ignore \' escape sequences):
                        var isEscaped = false;
                        for (; i < input.Length; i++)
                        {
                            if (!isEscaped)
                            {
                                if (input[i] == '\\')
                                    isEscaped = true;
                                else if (input[i] == '\'')
                                    break;
                            }
                            else
                            {
                                isEscaped = false;
                            }
                        }
                    }
                    else if (input[i] == '{')
                    {
                        openBraceCount += 1;
                    }
                    else if (input[i] == '}')
                    {
                        openBraceCount -= 1;
                        if (openBraceCount == 0)
                        {
                            var expressionPart = input.Substring(nextBraceIndex + 1, i - nextBraceIndex - 1).Trim();
                            if (expressionPart != "")
                                yield return ParseExpression(Tokenizer.Tokenize(expressionPart));

                            offset = i + 1;
                            endBraceFound = true;
                            break;
                        }
                    }
                }

                if (!endBraceFound)
                {
                    yield return input.Substring(nextBraceIndex);
                    break;
                }
            }
        }


        private static void Shift(Context context)
        {
            if (context.NextToken.Type != TokenType.Comment)
                context.ParseStack.Add(context.NextToken);

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
                    case TokenType.BraceClose: return ReduceBraceClose(context, token);
                    case TokenType.None: return ReduceNone(context, token);

                    case TokenType.EndOfInput:
                    case TokenType.Comma:
                    case TokenType.QuestionMark:
                    case TokenType.Colon:
                    case TokenType.If:
                    case TokenType.Else:

                    case TokenType.Plus:
                    case TokenType.Minus:
                    case TokenType.Asterisk:
                    case TokenType.Slash:
                    case TokenType.PercentageSign:
                    case TokenType.Equals:
                    case TokenType.NotEquals:
                    case TokenType.GreaterThan:
                    case TokenType.GreaterThanOrEqual:
                    case TokenType.LessThan:
                    case TokenType.LessThanOrEqual:
                    case TokenType.Semicolon:
                    case TokenType.DoubleAmpersand:
                    case TokenType.And:
                    case TokenType.DoubleBar:
                    case TokenType.Or:
                        return ReducePrecedingExpressions(context, token);

                    default:
                        return false;
                }
            }

            return false;
        }


        private static bool ReduceNumber(Context context, Token token)
        {
            AssertTokenType(token, TokenType.Number);

            // literal: <number>
            double value;
            if (token.Value.StartsWith("0x") || token.Value.StartsWith("0X"))
                value = (double)BigInteger.Parse(token.Value.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
            else
                value = double.Parse(token.Value, CultureInfo.InvariantCulture);
            return context.ReplaceLast(1, new NumberLiteral(value, token.Position));
        }

        private static bool ReduceString(Context context, Token token)
        {
            AssertTokenType(token, TokenType.String);

            // literal: <string>
            return context.ReplaceLast(1, new StringLiteral(token.Value, token.Position));
        }

        private static bool ReduceIdentifier(Context context, Token token)
        {
            AssertTokenType(token, TokenType.Identifier);

            // member-access
            if (context.Stack(-3) is Expression expression &&
                context.IsToken(-2, TokenType.Period))
            {
                // expression . <identifier>
                return context.ReplaceLast(3, new MemberAccess(expression, token.Value, token.Position));
            }

            // parameter-name-list
            if (context.NextToken.Type == TokenType.FatArrow)
            {
                // <identifier> =>
                return context.ReplaceLast(1, new ParameterNameList(token.Value, token.Position));
            }

            // variable:
            return context.ReplaceLast(1, new Variable(token.Value, token.Position));   // TODO: Other code still checks for <identifier>s instead of variables!!!
        }

        private static bool ReduceParensClose(Context context, Token token)
        {
            AssertTokenType(token, TokenType.ParensClose);

            if (context.NextToken.Type == TokenType.FatArrow)
            {
                // identifier-list ) =>
                if (context.Stack(-4) is Variable firstIdentifier &&
                    context.IsToken(-3, TokenType.Comma))
                {
                    if (context.Stack(-2) is Variable secondIdentifier)
                    {
                        // <identifier> , <identifier> ) =>
                        return context.ReplaceBeforeLast(3, new IdentifierList(firstIdentifier.Name, secondIdentifier.Name));
                    }
                    else if (context.Stack(-2) is IdentifierList identifierList)
                    {
                        // <identifier> , identifier-list ) =>
                        identifierList.Identifiers.Insert(0, firstIdentifier.Name);
                        return context.ReplaceBeforeLast(3, identifierList);
                    }
                }

                // parameter-name-list =>
                if (context.IsToken(-2, TokenType.ParensOpen, out var parensOpenToken))
                {
                    // ( ) =>
                    return context.ReplaceLast(2, new ParameterNameList(parensOpenToken.Position));
                }
                else if (context.IsToken(-3, TokenType.ParensOpen, out var parensOpenToken2))
                {
                    if (context.Stack(-2) is Variable parameter)
                    {
                        // ( <identifier> ) =>
                        return context.ReplaceLast(3, new ParameterNameList(parameter.Name, parensOpenToken2.Position));
                    }
                    else if (context.Stack(-2) is IdentifierList identifierList)
                    {
                        // ( identifier-list ) =>
                        return context.ReplaceLast(3, new ParameterNameList(identifierList.Identifiers.ToArray(), parensOpenToken2.Position));
                    }
                }
            }

            // expression-list )
            if (context.Stack(-4) is Expression firstExpression &&
                context.IsToken(-3, TokenType.Comma))
            {
                if (context.Stack(-2) is Expression secondExpression)
                {
                    // expression , expression )
                    return context.ReplaceBeforeLast(3, new ExpressionList(firstExpression, secondExpression));
                }
                else if (context.Stack(-2) is ExpressionList expressionList)
                {
                    // expression , expression-list )
                    expressionList.Expressions.Insert(0, firstExpression);
                    return context.ReplaceBeforeLast(3, expressionList);
                }
            }

            // function-call
            if (context.Stack(-3) is Expression functionExpression &&
                context.IsToken(-2, TokenType.ParensOpen))
            {
                // expression ( )
                return context.ReplaceLast(3, new FunctionCall(functionExpression, Array.Empty<Expression>(), functionExpression.Position));
            }
            else if (context.Stack(-4) is Expression functionExpression2 &&
                context.IsToken(-3, TokenType.ParensOpen))
            {
                if (context.Stack(-2) is Expression argumentExpression)
                {
                    // expression ( expression )
                    return context.ReplaceLast(4, new FunctionCall(functionExpression2, new[] { argumentExpression }, functionExpression2.Position));
                }
                else if (context.Stack(-2) is ExpressionList argumentsExpressionList)
                {
                    // expression ( expression-list )
                    return context.ReplaceLast(4, new FunctionCall(functionExpression2, argumentsExpressionList.Expressions, functionExpression2.Position));
                }
            }

            // grouped-expression
            if (context.IsToken(-3, TokenType.ParensOpen) &&
                context.Stack(-2) is Expression groupedExpression)
            {
                // ( expression )
                return context.ReplaceLast(3, groupedExpression);
            }

            return ReducePrecedingExpressions(context, token);
        }

        private static bool ReduceBracketClose(Context context, Token token)
        {
            AssertTokenType(token, TokenType.BracketClose);

            // expression-list ]
            if (context.Stack(-4) is Expression firstExpression &&
                context.IsToken(-3, TokenType.Comma))
            {
                if (context.Stack(-2) is Expression secondExpression)
                {
                    // expression , expression ]
                    return context.ReplaceBeforeLast(3, new ExpressionList(firstExpression, secondExpression));
                }
                else if (context.Stack(-2) is ExpressionList expressionList)
                {
                    // expression , expression-list ]
                    expressionList.Expressions.Insert(0, firstExpression);
                    return context.ReplaceBeforeLast(3, expressionList);
                }
            }

            // indexing
            if (context.Stack(-4) is Expression indexableExpression &&
                context.IsToken(-3, TokenType.BracketOpen) &&
                context.Stack(-2) is Expression indexExpression)
            {
                // expression [ expression ]
                return context.ReplaceLast(4, new Indexing(indexableExpression, indexExpression, indexableExpression.Position));
            }

            // array-literal
            if (context.IsToken(-2, TokenType.BracketOpen, out var bracketOpenToken))
            {
                // [ ]
                return context.ReplaceLast(2, new ArrayLiteral(Array.Empty<Expression>(), bracketOpenToken.Position));
            }
            else if (context.IsToken(-3, TokenType.BracketOpen, out var bracketOpenToken2))
            {
                if (context.Stack(-2) is Expression expression)
                {
                    // [ expression ]
                    return context.ReplaceLast(3, new ArrayLiteral(new[] { expression }, bracketOpenToken2.Position));
                }
                else if (context.Stack(-2) is ExpressionList expressionList)
                {
                    // [ expression-list ]
                    return context.ReplaceLast(3, new ArrayLiteral(expressionList.Expressions, bracketOpenToken2.Position));
                }
            }

            return ReducePrecedingExpressions(context, token);
        }

        private static bool ReduceBraceClose(Context context, Token token)
        {
            AssertTokenType(token, TokenType.BraceClose);

            // key-value-pair-list }
            if (context.Stack(-4) is Variable key &&
                context.IsToken(-3, TokenType.Colon) &&
                context.Stack(-2) is Expression valueExpression)
            {
                // <identifier> : expression }
                return context.ReplaceBeforeLast(3, new KeyValuePairList((key.Name, valueExpression)));
            }
            else if (context.Stack(-6) is Variable key2 &&
                context.IsToken(-5, TokenType.Colon) &&
                context.Stack(-4) is Expression valueExpression2 &&
                context.IsToken(-3, TokenType.Comma) &&
                context.Stack(-2) is KeyValuePairList keyValuePairList)
            {
                // <identifier> : expression , key-value-pair-list }
                keyValuePairList.KeyValuePairs.Insert(0, (key2.Name, valueExpression2));
                return context.ReplaceBeforeLast(5, keyValuePairList);
            }

            // object-literal
            if (context.IsToken(-2, TokenType.BraceOpen, out var braceOpenToken))
            {
                // { }
                return context.ReplaceLast(2, new ObjectLiteral(Array.Empty<(string, Expression)>(), braceOpenToken.Position));
            }
            else if (context.IsToken(-3, TokenType.BraceOpen, out var braceOpenToken2) &&
                context.Stack(-2) is KeyValuePairList keyValuePairList2)
            {
                // { key-value-pair-list }
                return context.ReplaceLast(3, new ObjectLiteral(keyValuePairList2.KeyValuePairs, braceOpenToken2.Position));
            }

            return ReducePrecedingExpressions(context, token);
        }

        private static bool ReduceNone(Context context, Token token)
        {
            AssertTokenType(token, TokenType.None);

            // literal: none
            return context.ReplaceLast(1, new NoneLiteral(token.Position));
        }

        private static bool ReducePrecedingExpressions(Context context, Token token)
        {
            var nextTokenPrecedence = GetPrecedence(token.Type);

            // binary-operation <?>
            if (context.Stack(-4) is Expression leftExpression &&
                GetBinaryOperator(context.Stack(-3)) is BinaryOperator binaryOperator &&
                context.Stack(-2) is Expression rightExpression)
            {
                if (nextTokenPrecedence > GetPrecedence(binaryOperator))
                    return false;

                // expression binary-operator expression <?>
                return context.ReplaceBeforeLast(3, new BinaryOperation(binaryOperator, leftExpression, rightExpression, leftExpression.Position));
            }

            // unary-operation <?>
            if (GetUnaryOperator(context.Stack(-3)) is UnaryOperator unaryOperator &&
                context.Stack(-2) is Expression expression)
            {
                if (nextTokenPrecedence > GetPrecedence(unaryOperator))
                    return false;

                // unary-operator expression <?>
                return context.ReplaceBeforeLast(2, new UnaryOperation(unaryOperator, expression, expression.Position));
            }

            // conditional-operation <?>
            if (context.Stack(-6) is Expression firstExpression &&
                context.Stack(-4) is Expression secondExpression &&
                context.Stack(-2) is Expression thirdExpression)
            {
                if (context.IsToken(-5, TokenType.QuestionMark) &&
                    context.IsToken(-3, TokenType.Colon))
                {
                    if (nextTokenPrecedence >= GetPrecedence(TokenType.Colon))
                        return false;

                    // expression ? expression : expression <?>
                    return context.ReplaceBeforeLast(5, new ConditionalOperation(firstExpression, secondExpression, thirdExpression, firstExpression.Position));
                }
                else if (context.IsToken(-5, TokenType.If, out var ifToken) &&
                    context.IsToken(-3, TokenType.Else))
                {
                    if (nextTokenPrecedence >= GetPrecedence(TokenType.Else))
                        return false;

                    // expression if expression else expression <?>
                    return context.ReplaceBeforeLast(5, new ConditionalOperation(secondExpression, firstExpression, thirdExpression, ifToken.Position));
                }
            }

            // function-literal <?>
            if (context.Stack(-4) is ParameterNameList parameterNameList &&
                context.IsToken(-3, TokenType.FatArrow) &&
                context.Stack(-2) is Expression bodyExpression)
            {
                if (nextTokenPrecedence > GetPrecedence(TokenType.FatArrow))
                    return false;

                // parameter-list => expression <?>
                return context.ReplaceBeforeLast(3, new AnonymousFunctionDefinition(parameterNameList.ParameterNames, bodyExpression, parameterNameList.Position));
            }

            return false;
        }


        private static BinaryOperator? GetBinaryOperator(object? element) => element is Token token ? GetBinaryOperator(token.Type) : null;

        private static BinaryOperator? GetBinaryOperator(TokenType tokenType) => tokenType switch {
            TokenType.Plus => BinaryOperator.Add,
            TokenType.Minus => BinaryOperator.Subtract,
            TokenType.Asterisk => BinaryOperator.Multiply,
            TokenType.Slash => BinaryOperator.Divide,
            TokenType.PercentageSign => BinaryOperator.Remainder,
            TokenType.Equals => BinaryOperator.Equals,
            TokenType.NotEquals => BinaryOperator.NotEquals,
            TokenType.GreaterThan => BinaryOperator.GreaterThan,
            TokenType.GreaterThanOrEqual => BinaryOperator.GreaterThanOrEqual,
            TokenType.LessThan => BinaryOperator.LessThan,
            TokenType.LessThanOrEqual => BinaryOperator.LessThanOrEqual,

            TokenType.DoubleAmpersand => BinaryOperator.LogicalAnd,
            TokenType.And => BinaryOperator.LogicalAnd,

            TokenType.DoubleBar => BinaryOperator.LogicalOr,
            TokenType.Or => BinaryOperator.LogicalOr,

            TokenType.ShiftLeft => BinaryOperator.BitshiftLeft,
            TokenType.ShiftRight => BinaryOperator.BitshiftRight,
            TokenType.SingleAmpersand => BinaryOperator.BitwiseAnd,
            TokenType.Caret => BinaryOperator.BitwiseXor,
            TokenType.SingleBar => BinaryOperator.BitwiseOr,

            _ => null,
        };

        private static UnaryOperator? GetUnaryOperator(object? element) => element is Token token ? GetUnaryOperator(token.Type) : null;

        private static UnaryOperator? GetUnaryOperator(TokenType tokenType) => tokenType switch {
            TokenType.Minus => UnaryOperator.Negate,
            TokenType.ExclamationMark => UnaryOperator.LogicalNegate,
            TokenType.Not => UnaryOperator.LogicalNegate,
            TokenType.Tilde => UnaryOperator.BitwiseComplement,

            _ => null,
        };

        private static int GetPrecedence(TokenType tokenType)
        {
            if (GetBinaryOperator(tokenType) is BinaryOperator binaryOperator)
                return GetPrecedence(binaryOperator);
            else if (GetUnaryOperator(tokenType) is UnaryOperator unaryOperator)
                return GetPrecedence(unaryOperator);

            switch (tokenType)
            {
                // member-access, function-call, indexing:
                case TokenType.Period:
                case TokenType.ParensOpen:
                case TokenType.BracketOpen:
                    return 14;

                // unary-operation: 13
                // binary-operation: 3-12

                // conditional-operation:
                case TokenType.QuestionMark:
                case TokenType.Colon:
                case TokenType.If:
                case TokenType.Else:
                    return 2;

                // anonymous-function:
                case TokenType.FatArrow:
                    return 1;

                default:
                    return 0;
            }
        }

        private static int GetPrecedence(BinaryOperator binaryOperator)
        {
            switch (binaryOperator)
            {
                case BinaryOperator.Multiply:
                case BinaryOperator.Divide:
                case BinaryOperator.Remainder:
                    return 12;

                case BinaryOperator.Add:
                case BinaryOperator.Subtract:
                    return 11;

                case BinaryOperator.BitshiftLeft:
                case BinaryOperator.BitshiftRight:
                    return 10;

                case BinaryOperator.GreaterThan:
                case BinaryOperator.GreaterThanOrEqual:
                case BinaryOperator.LessThan:
                case BinaryOperator.LessThanOrEqual:
                    return 9;

                case BinaryOperator.Equals:
                case BinaryOperator.NotEquals:
                    return 8;

                case BinaryOperator.BitwiseAnd:
                    return 7;

                case BinaryOperator.BitwiseXor:
                    return 6;

                case BinaryOperator.BitwiseOr:
                    return 5;

                case BinaryOperator.LogicalAnd:
                    return 4;

                case BinaryOperator.LogicalOr:
                    return 3;

                default:
                    return 0;
            }
        }

        private static int GetPrecedence(UnaryOperator unaryOperator) => 13;


        private static ParseException ParseError(string message, Context context)
        {
            var exception = new ParseException(message, context.NextToken.Position);

            var topItem = context.Stack(-1);
            if (topItem is Token token)
            {
                exception.Data["Token"] = token.Type;
                exception.Data["Position"] = token.Position;
            }
            else if (topItem is Expression expression)
            {
                exception.Data["Expression"] = expression;
                exception.Data["Position"] = expression.Position;
            }
            else if (topItem is Assignment[] assignments)
            {
                var lastAssignment = assignments.LastOrDefault();
                if (lastAssignment != null)
                {
                    exception.Data["Assignment"] = lastAssignment;
                    exception.Data["Position"] = lastAssignment.Value.Position;
                }
            }

            return exception;
        }

        private static void AssertTokenType(Token token, TokenType expectedType)
        {
            if (token.Type != expectedType)
                throw new ParseException($"Expected a {expectedType} token but found a {token.Type}.", token.Position);
        }


        class Context : IDisposable
        {
            public List<object> ParseStack { get; } = new();
            public Token NextToken => _tokens.Current;
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
            public object? Stack(int index)
            {
                index = (index < 0) ? ParseStack.Count + index : index;
                if (index < 0 || index >= ParseStack.Count)
                    return null;

                return ParseStack[index];
            }

            /// <inheritdoc cref="IsToken(int, TokenType, out Token?)"/>
            public bool IsToken(int index, TokenType type) => IsToken(index, type, out _);

            /// <summary>
            /// Returns true if the nth element in the parse stack is a token of the specified type.
            /// Negative indexes start from the end, where -1 is the last element, -2 the next-to-last, and so on.
            /// Returns false if the index is out of range.
            /// </summary>
            public bool IsToken(int index, TokenType type, out Token token)
            {
                if (Stack(index) is Token stackToken && stackToken.Type == type)
                {
                    token = stackToken;
                    return true;
                }

                token = default;
                return false;
            }

            /// <summary>
            /// Returns the token value of the nth element in the parse stack, if it is a token. Returns an empty string otherwise.
            /// Negative indexes start from the end, where -1 is the last element, -2 the next-to-last, and so on.
            /// </summary>
            public string TokenValue(int index) => Stack(index) is Token token ? token.Value : "";

            /// <summary>
            /// Replaces the top n elements from the stack with the given element.
            /// Always returns true.
            /// </summary>
            public bool ReplaceLast(int count, object element)
            {
                ParseStack.RemoveRange(ParseStack.Count - count, count);
                ParseStack.Add(element);
                return true;
            }

            /// <summary>
            /// Leaves the top of the stack intact, but replaces the n elements below that with the given element.
            /// Always returns true.
            /// </summary>
            public bool ReplaceBeforeLast(int count, object element)
            {
                ParseStack.RemoveRange(ParseStack.Count - count - 1, count);
                ParseStack.Insert(ParseStack.Count - 1, element);
                return true;
            }
        }
    }
}
