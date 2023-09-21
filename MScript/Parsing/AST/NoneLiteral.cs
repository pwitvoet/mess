namespace MScript.Parsing.AST
{
    class NoneLiteral : Literal
    {
        public NoneLiteral(Position position)
            : base(position)
        {
        }

        public override string ToString() => "none";
    }
}
