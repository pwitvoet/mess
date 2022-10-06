namespace MScript.Parsing.AST
{
    class ExpressionList
    {
        public List<Expression> Expressions { get; }

        public ExpressionList(params Expression[] expressions)
        {
            Expressions = expressions.ToList();
        }
    }
}
