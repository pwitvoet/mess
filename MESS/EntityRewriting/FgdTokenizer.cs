using System.Text;

namespace MESS.EntityRewriting
{
    static class FgdTokenizer
    {
        public static IEnumerable<Token> Tokenize(string input)
        {
            var context = new Context(input);
            var isMessDirectiveEnabled = false;
            while (true)
            {
                SkipWhitespace(context);
                if (context.IsExhausted)
                    yield break;

                var token = ReadToken(context);

                // Look for special comment sections that contain MESS-specific instructions:
                var disableDirectiveMode = false;
                if (isMessDirectiveEnabled)
                {
                    if (token.Type != TokenType.Comment)
                        isMessDirectiveEnabled = false;
                    else if (token.Value.Trim().StartsWith("@MESS;"))
                        disableDirectiveMode = true;
                }
                else
                {
                    if (token.Type == TokenType.Comment && token.Value.Trim().StartsWith("@MESS") && token.Value.Trim() != "@MESS;")
                        isMessDirectiveEnabled = true;
                }

                if (isMessDirectiveEnabled)
                    yield return new Token(token.Line, token.Offset, TokenType.MessDirective, token.Value);
                else
                    yield return token;

                if (disableDirectiveMode)
                    isMessDirectiveEnabled = false;
            }
        }


        private static void SkipWhitespace(Context context)
        {
            while (!context.IsExhausted && char.IsWhiteSpace(context.Current))
                context.MoveNext();
        }

        private static Token ReadToken(Context context)
        {
            var line = context.Line;
            var offset = context.Offset;

            switch (context.Current)
            {
                case ',': context.MoveNext(); return new Token(line, offset, TokenType.Comma, context.Current.ToString());
                case '=': context.MoveNext(); return new Token(line, offset, TokenType.Assignment, context.Current.ToString());
                case ':': context.MoveNext(); return new Token(line, offset, TokenType.Colon, context.Current.ToString());
                case '-': context.MoveNext(); return new Token(line, offset, TokenType.Minus, context.Current.ToString());
                case '(': context.MoveNext(); return new Token(line, offset, TokenType.ParensOpen, context.Current.ToString());
                case ')': context.MoveNext(); return new Token(line, offset, TokenType.ParensClose, context.Current.ToString());
                case '[': context.MoveNext(); return new Token(line, offset, TokenType.BracketOpen, context.Current.ToString());
                case ']': context.MoveNext(); return new Token(line, offset, TokenType.BracketClose, context.Current.ToString());
                case '{': context.MoveNext(); return new Token(line, offset, TokenType.BraceOpen, context.Current.ToString());
                case '}': context.MoveNext(); return new Token(line, offset, TokenType.BraceClose, context.Current.ToString());

                case '"':
                    return ReadString(context);

                case '@':
                case '_':
                    return ReadName(context);

                case '/':
                    if (context.Peek() != '/')
                        throw ParseError(context, "Unexpected '/'.");
                    return ReadComment(context);
            }

            if (char.IsDigit(context.Current))
                return ReadInteger(context);
            else if (char.IsLetter(context.Current))
                return ReadName(context);

            throw ParseError(context, $"Unexpected '{context.Current}'.");
        }

        private static Token ReadString(Context context)
        {
            var line = context.Line;
            var offset = context.Offset;

            if (context.IsExhausted || context.Current != '"')
                throw ParseError(context, $"Expected an opening \" but found '{context.Current}'.");

            var buffer = new StringBuilder();
            context.MoveNext(); // Consume the opening "
            while (!context.IsExhausted && context.Current != '"')
            {
                buffer.Append(context.Current);
                context.MoveNext();
            }

            if (context.IsExhausted)
                throw ParseError(context, $"Expected a closing \" but found end of input.");

            context.MoveNext(); // Consume the closing "
            return new Token(line, offset, TokenType.String, buffer.ToString());
        }

        private static Token ReadComment(Context context)
        {
            var line = context.Line;
            var offset = context.Offset;

            if (context.IsExhausted || context.Current != '/' || context.Peek() != '/')
                throw ParseError(context, $"Invalid start of a comment.");

            // Consume the opening '//':
            context.MoveNext();
            context.MoveNext();

            var buffer = new StringBuilder();
            while (!context.IsExhausted && context.Current != '\n')
            {
                buffer.Append(context.Current);
                context.MoveNext();
            }

            context.MoveNext(); // Consume the \n
            return new Token(line, offset, TokenType.Comment, buffer.ToString());
        }

        private static Token ReadInteger(Context context)
        {
            var line = context.Line;
            var offset = context.Offset;

            if (context.IsExhausted || !char.IsDigit(context.Current))
                throw ParseError(context, $"Expected the start of an integer but found '{context.Current}'.");

            var buffer = new StringBuilder();
            while (!context.IsExhausted && char.IsDigit(context.Current))
            {
                buffer.Append(context.Current);
                context.MoveNext();
            }

            return new Token(line, offset, TokenType.Integer, buffer.ToString());
        }

        private static Token ReadName(Context context)
        {
            var line = context.Line;
            var offset = context.Offset;

            if (context.IsExhausted || (context.Current != '@' && context.Current != '_' && !char.IsLetter(context.Current)))
                throw ParseError(context, $"");

            var buffer = new StringBuilder();
            while (!context.IsExhausted && (context.Current == '@' || context.Current == '_' || char.IsLetterOrDigit(context.Current)))
            {
                buffer.Append(context.Current);
                context.MoveNext();
            }

            return new Token(line, offset, TokenType.Name, buffer.ToString());
        }

        private static Exception ParseError(Context context, string message)
        {
            var ex = new InvalidDataException(message + $" At line {context.Line}, offset {context.Offset}.");
            ex.Data["line number"] = context.Line;
            ex.Data["offset"] = context.Offset;
            return ex;
        }


        public enum TokenType
        {
            Name,           // \w[\w\d]*
            Integer,        // \d+
            String,         // "[^"]*"
            Comment,        // //[^\n]*\n
            MessDirective,  // //\s*@MESS[^\n]*\n

            Comma,          // ,
            Assignment,     // =
            Colon,          // :
            Minus,          // -
            ParensOpen,     // (
            ParensClose,    // )
            BracketOpen,    // [
            BracketClose,   // ]
            BraceOpen,      // {
            BraceClose,     // }
        }


        public struct Token
        {
            public int Line { get; }
            public int Offset { get; }
            public TokenType Type { get; }
            public string Value { get; }

            public Token(int line, int offset, TokenType type, string? value = null)
            {
                Line = line;
                Offset = offset;
                Type = type;
                Value = value ?? "";
            }
        }


        class Context
        {
            public string Input { get; }
            public int Position { get; private set; }

            public int Line { get; private set; }
            public int Offset { get; private set; }

            public char Current { get; private set; }
            public bool IsExhausted { get; private set; }


            public Context(string input)
            {
                Input = input;
                Position = -1;

                Line = 1;
                Offset = 0;

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
                    if (Current == '\n')    // NOTE: No support for \r-only line endings here.
                    {
                        Line += 1;
                        Offset = 0;
                    }

                    Position += 1;
                    Offset += 1;
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
