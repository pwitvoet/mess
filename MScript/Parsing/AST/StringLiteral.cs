namespace MScript.Parsing.AST
{
    class StringLiteral : Literal
    {
        public string Value { get; }

        public StringLiteral(string value) => Value = value;
    }
}
