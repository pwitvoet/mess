namespace MScript.Tokenizing
{
    public struct Token
    {
        public TokenType Type { get; }
        public string Value { get; }

        public Position Position { get; }

        public Token(TokenType type, Position position)
            : this(type, "", position)
        {
        }

        public Token(TokenType type, string value, Position position)
        {
            Type = type;
            Value = value;

            Position = position;
        }

        public override string ToString() => $"(Type: {Type}, Value: '{Value}')";
    }
}
