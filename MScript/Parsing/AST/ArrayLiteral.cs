namespace MScript.Parsing.AST
{
    class ArrayLiteral : Literal
    {
        public IReadOnlyList<Expression> Elements { get; }


        public ArrayLiteral(IEnumerable<Expression> elements)
        {
            Elements = elements.ToArray();
        }
    }
}
