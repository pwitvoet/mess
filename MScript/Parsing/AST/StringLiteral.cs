namespace MScript.Parsing.AST
{
    class StringLiteral : Literal
    {
        public string Value { get; }

        public StringLiteral(string value, Position position)
            : base(position)
            => Value = value;


        public override string ToString() => $"'{Value}'";
    }
}
