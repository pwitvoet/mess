using System;

namespace MScript.Parsing.AST
{
    class Indexing : Expression
    {
        public Expression Indexable { get; }
        public Expression Index { get; }

        public Indexing(Expression indexable, Expression index)
        {
            Indexable = indexable;
            Index = index;
        }
    }
}
