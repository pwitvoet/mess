using System.Text;

namespace MScript.Tokenizing
{
    public static class Tokenizer
    {
        public static IEnumerable<Token> Tokenize(string input)
        {
            var context = new Context(input);
            while (true)
            {
                SkipWhitespace(context);
                if (context.IsExhausted)
                {
                    yield return new Token(TokenType.EndOfInput, context.GetPosition());
                    yield break;
                }

                yield return ReadToken(context);
            }
        }


        private static void SkipWhitespace(Context context)
        {
            while (!context.IsExhausted && char.IsWhiteSpace(context.Current))
                context.MoveNext();
        }

        private static Token ReadToken(Context context)
        {
            var position = context.GetPosition();

            switch (context.Current)
            {
                case '(': context.MoveNext(); return new Token(TokenType.ParensOpen, position);
                case ')': context.MoveNext(); return new Token(TokenType.ParensClose, position);
                case '[': context.MoveNext(); return new Token(TokenType.BracketOpen, position);
                case ']': context.MoveNext(); return new Token(TokenType.BracketClose, position);
                case '{': context.MoveNext(); return new Token(TokenType.BraceOpen, position);
                case '}': context.MoveNext(); return new Token(TokenType.BraceClose, position);
                case '.': context.MoveNext(); return new Token(TokenType.Period, position);
                case ',': context.MoveNext(); return new Token(TokenType.Comma, position);
                case '?': context.MoveNext(); return new Token(TokenType.QuestionMark, position);
                case ':': context.MoveNext(); return new Token(TokenType.Colon, position);
                case ';': context.MoveNext(); return new Token(TokenType.Semicolon, position);
                case '+': context.MoveNext(); return new Token(TokenType.Plus, position);
                case '-': context.MoveNext(); return new Token(TokenType.Minus, position);
                case '*': context.MoveNext(); return new Token(TokenType.Asterisk, position);
                case '%': context.MoveNext(); return new Token(TokenType.PercentageSign, position);

                case '/':
                    if (context.MoveNext())
                    {
                        if (context.Current == '/')
                        {
                            context.MoveNext();
                            return ReadLineComment(context, position);
                        }
                        else if (context.Current == '*')
                        {
                            context.MoveNext();
                            return ReadBlockComment(context, position);
                        }
                    }
                    return new Token(TokenType.Slash, position);

                case '=':
                    if (!context.MoveNext() || (context.Current != '=' && context.Current != '>'))
                        return new Token(TokenType.SingleEquals, position);
                    var tokenType = context.Current == '=' ? TokenType.Equals : TokenType.FatArrow;
                    context.MoveNext();
                    return new Token(tokenType, position);

                case '!':
                    if (!context.MoveNext() || context.Current != '=')
                        return new Token(TokenType.ExclamationMark, position);
                    context.MoveNext();
                    return new Token(TokenType.NotEquals, position);

                case '>':
                    if (!context.MoveNext() || context.Current != '=')
                        return new Token(TokenType.GreaterThan, position);
                    context.MoveNext();
                    return new Token(TokenType.GreaterThanOrEqual, position);

                case '<':
                    if (!context.MoveNext() || context.Current != '=')
                        return new Token(TokenType.LessThan, position);
                    context.MoveNext();
                    return new Token(TokenType.LessThanOrEqual, position);

                case '&':
                    if (!context.MoveNext() || context.Current != '&')
                        throw ParseError(context, $"Expected '&&' but found '&{context.Current}'.");
                    context.MoveNext();
                    return new Token(TokenType.DoubleAmpersand, position);

                case '|':
                    if (!context.MoveNext() || context.Current != '|')
                        throw ParseError(context, $"Expected '||' but found '|{context.Current}'.");
                    context.MoveNext();
                    return new Token(TokenType.DoubleBar, position);

                case '\'':
                    return ReadString(context, position);
            }

            if (char.IsDigit(context.Current))
                return ReadNumber(context, position);
            else if (char.IsLetter(context.Current) || context.Current == '_')
                return ReadIdentifier(context, position);

            throw ParseError(context, $"Unexpected '{context.Current}'.");
        }

        private static Token ReadNumber(Context context, Position position)
        {
            if (context.IsExhausted || !char.IsDigit(context.Current))
                throw ParseError(context, $"Expected the start of a number but found '{context.Current}'.");

            var buffer = new StringBuilder();
            while (!context.IsExhausted && char.IsDigit(context.Current))
            {
                buffer.Append(context.Current);
                context.MoveNext();
            }

            if (!context.IsExhausted && context.Current == '.')
            {
                buffer.Append('.');
                if (!context.MoveNext())
                    throw ParseError(context, $"Expected a decimal number part but found '{context.Current}'.");

                while (!context.IsExhausted && char.IsDigit(context.Current))
                {
                    buffer.Append(context.Current);
                    context.MoveNext();
                }
            }
            return new Token(TokenType.Number, buffer.ToString(), position);
        }

        private static Token ReadString(Context context, Position position)
        {
            if (context.IsExhausted || context.Current != '\'')
                throw ParseError(context, $"Expected an opening ' but found '{context.Current}'.");

            var buffer = new StringBuilder();
            context.MoveNext(); // Consume the opening '

            var isEscapeSequence = false;
            while (!context.IsExhausted)
            {
                if (!isEscapeSequence)
                {
                    if (context.Current == '\'')
                    {
                        break;
                    }
                    else if (context.Current == '\\')
                    {
                        isEscapeSequence = true;
                        context.MoveNext();
                    }
                    else
                    {
                        buffer.Append(context.Current);
                        context.MoveNext();
                    }
                }
                else
                {
                    var current = context.Current;
                    context.MoveNext();
                    switch (current)
                    {
                        case '\'': buffer.Append('\'');break;
                        case '"': buffer.Append('"'); break;
                        case '\\': buffer.Append('\\'); break;
                        case '0': buffer.Append('\0'); break;
                        case 'a': buffer.Append('\a'); break;
                        case 'b': buffer.Append('\b'); break;
                        case 'f': buffer.Append('\f'); break;
                        case 'n': buffer.Append('\n'); break;
                        case 'r': buffer.Append('\r'); break;
                        case 't': buffer.Append('\t'); break;
                        case 'v': buffer.Append('\v'); break;
                        case 'x': buffer.Append(ReadUnicodeEscapeSequence(context, 2)); break;
                        case 'u': buffer.Append(ReadUnicodeEscapeSequence(context, 4)); break;
                        //case 'U': buffer.Append(ReadUnicodeEscapeSequence(context, 8)); break;    // TODO: This requires handling of surrogate pairs!

                        default:
                            throw ParseError(context, $"Unknown escape sequence: \\'{current}'.");
                    }

                    isEscapeSequence = false;
                }
            }

            if (isEscapeSequence)
                throw ParseError(context, $"Expected the rest of an escape sequence, but found end of input.");

            if (context.IsExhausted)
                throw ParseError(context, $"Expected a closing ' but found end of input.");

            context.MoveNext(); // Consume the closing '
            return new Token(TokenType.String, buffer.ToString(), position);
        }

        private static Token ReadIdentifier(Context context, Position position)
        {
            if (context.IsExhausted || (!char.IsLetter(context.Current) && context.Current != '_'))
                throw ParseError(context, $"Expected a letter or an underscore but found '{context.Current}'.");

            var buffer = new StringBuilder();
            while (!context.IsExhausted && (context.Current == '_' || char.IsLetter(context.Current) || char.IsDigit(context.Current)))
            {
                buffer.Append(context.Current);
                context.MoveNext();
            }

            var name = buffer.ToString();
            if (GetKeyword(name) is TokenType keyword)
                return new Token(keyword, position);

            return new Token(TokenType.Identifier, name, position);
        }

        // TODO: 8-digit escape sequences require surrogate pair handling!
        private static char ReadUnicodeEscapeSequence(Context context, int length)
        {
            var hexValue = 0;
            for (int i = 0; i < length; i++)
            {
                if (context.IsExhausted)
                    throw ParseError(context, $"Expected a Unicode escape sequence with {length} hex digits, but found end of input.");

                var digitValue = 0;
                if (context.Current >= '0' && context.Current <= '9')
                    digitValue = context.Current - '0';
                else if (context.Current >= 'a' && context.Current <= 'f')
                    digitValue = 10 + context.Current - 'a';
                else if (context.Current >= 'A' && context.Current <= 'F')
                    digitValue = 10 + context.Current - 'A';
                else
                    throw ParseError(context, $"Invalid Unicode escape sequence: '{context.Current}' is not a hex digit.");

                hexValue = (hexValue << 4) | digitValue;

                context.MoveNext();
            }
            return (char)hexValue;
        }

        private static Token ReadLineComment(Context context, Position position)
        {
            var buffer = new StringBuilder();
            while (!context.IsExhausted)
            {
                if (context.Current == '\r' || context.Current == '\n')
                    break;

                buffer.Append(context.Current);
                context.MoveNext();
            }
            return new Token(TokenType.Comment, buffer.ToString(), position);
        }

        private static Token ReadBlockComment(Context context, Position position)
        {
            var buffer = new StringBuilder();
            var previousWasStar = false;
            while (!context.IsExhausted)
            {
                if (context.Current == '/' && previousWasStar)
                {
                    buffer.Length -= 1;
                    context.MoveNext();
                    break;
                }

                buffer.Append(context.Current);
                previousWasStar = context.Current == '*';
                context.MoveNext();
            }
            return new Token(TokenType.Comment, buffer.ToString(), position);
        }


        private static ParseException ParseError(Context context, string message) => new ParseException(message, context.GetPosition());

        private static TokenType? GetKeyword(string name)
        {
            switch (name.ToUpperInvariant())
            {
                case "NONE": return TokenType.None;
                case "AND": return TokenType.And;
                case "OR": return TokenType.Or;
                case "NOT": return TokenType.Not;
                case "IF": return TokenType.If;
                case "ELSE": return TokenType.Else;
                default: return null;
            }
        }


        class Context
        {
            public string Input { get; }
            public int Position { get; private set; }

            public char Current { get; private set; }
            public bool IsExhausted { get; private set; }

            public int LineNumber { get; private set; }
            public int LineOffset { get; private set; }

            private char Previous { get; set; }


            public Context(string input)
            {
                Input = input;
                Position = -1;
                LineNumber = 1;
                MoveNext();
            }

            public bool MoveNext()
            {
                if (Position + 1 >= Input.Length)
                {
                    Position = Input.Length;
                    Previous = Current;
                    Current = '\0';

                    if (!IsExhausted)
                        LineOffset += 1;

                    IsExhausted = true;

                    return false;
                }
                else
                {
                    Position += 1;
                    Previous = Current;
                    Current = Input[Position];

                    if ((Previous == '\r' && Current != '\n') || Previous == '\n')
                    {
                        LineNumber += 1;
                        LineOffset = 0;
                    }
                    else
                    {
                        LineOffset += 1;
                    }

                    return true;
                }
            }

            public char? Peek(int offset = 1)
            {
                if (offset < 0)
                    throw new ArgumentOutOfRangeException($"{nameof(offset)} cannot be negative.");

                var pos = Position + offset;
                return (pos < Input.Length) ? Input[pos] : (char?)null;
            }

            public Position GetPosition() => new Position(LineNumber, LineOffset);
        }
    }
}
