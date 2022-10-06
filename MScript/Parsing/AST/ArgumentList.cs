namespace MScript.Parsing.AST
{
    class ArgumentList
    {
        public List<Expression> Arguments { get; }

        public ArgumentList(params Expression[] arguments)
        {
            Arguments = arguments.ToList();
        }
    }
}
