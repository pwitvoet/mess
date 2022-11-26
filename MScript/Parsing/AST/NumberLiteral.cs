namespace MScript.Parsing.AST
{
    class NumberLiteral : Literal
    {
        public double Value { get; }

        public NumberLiteral(double value) => Value = value;


        public override string ToString() => Value.ToString();
    }
}
