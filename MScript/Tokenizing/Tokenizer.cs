using System;
using System.Collections.Generic;
using System.Text;

namespace MScript.Tokenizing
{
    static class Tokenizer
    {
        public static IEnumerable<Token> Tokenize(string input)
        {
            var context = new Context(input);
            while (true)
            {
                SkipWhitespace(context);
                if (context.IsExhausted)
                    yield break;

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
            switch (context.Current)
            {
                case '(': context.MoveNext(); return new Token(TokenType.ParensOpen);
                case ')': context.MoveNext(); return new Token(TokenType.ParensClose);
                case '[': context.MoveNext(); return new Token(TokenType.BracketOpen);
                case ']': context.MoveNext(); return new Token(TokenType.BracketClose);
                case '.': context.MoveNext(); return new Token(TokenType.Period);
                case ',': context.MoveNext(); return new Token(TokenType.Comma);
                case '?': context.MoveNext(); return new Token(TokenType.QuestionMark);
                case ':': context.MoveNext(); return new Token(TokenType.Colon);
                case '+': context.MoveNext(); return new Token(TokenType.Plus);
                case '-': context.MoveNext(); return new Token(TokenType.Minus);
                case '*': context.MoveNext(); return new Token(TokenType.Asterisk);
                case '/': context.MoveNext(); return new Token(TokenType.Slash);
                case '%': context.MoveNext(); return new Token(TokenType.PercentageSign);

                case '=':
                    if (!context.MoveNext() || context.Current != '=')
                        throw ParseError(context, $"Expected '==' but found '={context.Current}'.");
                    context.MoveNext();
                    return new Token(TokenType.Equals);

                case '!':
                    if (!context.MoveNext() || context.Current != '=')
                        return new Token(TokenType.ExclamationMark);
                    context.MoveNext();
                    return new Token(TokenType.NotEquals);

                case '>':
                    if (!context.MoveNext() || context.Current != '=')
                        return new Token(TokenType.GreaterThan);
                    context.MoveNext();
                    return new Token(TokenType.GreaterThanOrEqual);

                case '<':
                    if (!context.MoveNext() || context.Current != '=')
                        return new Token(TokenType.LessThan);
                    context.MoveNext();
                    return new Token(TokenType.LessThanOrEqual);

                case '&':
                    if (!context.MoveNext() || context.Current != '&')
                        throw ParseError(context, $"Expected '&&' but found '&{context.Current}'.");
                    context.MoveNext();
                    return new Token(TokenType.And);

                case '|':
                    if (!context.MoveNext() || context.Current != '|')
                        throw ParseError(context, $"Expected '||' but found '|{context.Current}'.");
                    context.MoveNext();
                    return new Token(TokenType.Or);

                case '\'':
                    return ReadString(context);
            }

            if (char.IsDigit(context.Current))
                return ReadNumber(context);
            else if (char.IsLetter(context.Current))
                return ReadIdentifier(context);

            throw ParseError(context, $"Unexpected '{context.Current}'.");
        }

        private static Token ReadNumber(Context context)
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
            return new Token(TokenType.Number, buffer.ToString());
        }

        private static Token ReadString(Context context)
        {
            if (context.IsExhausted || context.Current != '\'')
                throw ParseError(context, $"Expected an opening ' but found '{context.Current}'.");

            var buffer = new StringBuilder();
            context.MoveNext(); // Consume the opening '
            while (!context.IsExhausted && context.Current != '\'')
            {
                buffer.Append(context.Current);
                context.MoveNext();
            }

            if (context.IsExhausted)
                throw ParseError(context, $"Expected a closing ' but found end of input.");

            context.MoveNext(); // Consume the closing '
            return new Token(TokenType.String, buffer.ToString());
        }

        private static Token ReadIdentifier(Context context)
        {
            if (context.IsExhausted || (!char.IsLetter(context.Current) && context.Current != '_'))
                throw ParseError(context, $"");

            var buffer = new StringBuilder();
            while (!context.IsExhausted && (context.Current == '_' || char.IsLetter(context.Current) || char.IsDigit(context.Current)))
            {
                buffer.Append(context.Current);
                context.MoveNext();
            }

            var name = buffer.ToString();
            if (GetKeyword(name) is TokenType keyword)
                return new Token(keyword);

            return new Token(TokenType.Identifier, name);
        }


        private static ParseException ParseError(Context context, string message) => new ParseException(message, context.Position);

        private static TokenType? GetKeyword(string name)
        {
            switch (name.ToUpperInvariant())
            {
                case "NONE": return TokenType.None;
                case "AND": return TokenType.And;
                case "OR": return TokenType.Or;
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


            public Context(string input)
            {
                Input = input;
                Position = -1;
                MoveNext();
            }

            public bool MoveNext()
            {
                if (Position + 1 >= Input.Length)
                {
                    Position = Input.Length;
                    IsExhausted = true;
                    Current = '\0';
                    return false;
                }
                else
                {
                    Position += 1;
                    Current = Input[Position];
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
        }
    }
}
