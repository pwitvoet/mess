namespace MScript.Parsing.AST
{
    class ArrayLiteral : Literal
    {
        public IReadOnlyList<Expression> Elements { get; }


        public ArrayLiteral(IEnumerable<Expression> elements, Position position)
            : base(position)
        {
            Elements = elements.ToArray();
        }


        public override string ToString() => $"[{string.Join(", ", Elements)}]";
    }
}
