namespace MScript.Parsing.AST
{
    class VectorLiteral : Literal
    {
        public IReadOnlyList<Expression> Elements { get; }


        public VectorLiteral(IEnumerable<Expression> elements)
        {
            Elements = elements.ToArray();
        }
    }
}
