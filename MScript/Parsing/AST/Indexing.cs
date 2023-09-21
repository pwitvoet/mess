namespace MScript.Parsing.AST
{
    class Indexing : Expression
    {
        public Expression Indexable { get; }
        public Expression Index { get; }

        public Indexing(Expression indexable, Expression index, Position position)
            : base(position)
        {
            Indexable = indexable;
            Index = index;
        }


        public override string ToString() => $"{Indexable}[{Index}]";
    }
}
