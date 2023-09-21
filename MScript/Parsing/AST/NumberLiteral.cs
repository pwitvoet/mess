namespace MScript.Parsing.AST
{
    class NumberLiteral : Literal
    {
        public double Value { get; }

        public NumberLiteral(double value, Position position)
            : base(position)
            => Value = value;


        public override string ToString() => Value.ToString();
    }
}
