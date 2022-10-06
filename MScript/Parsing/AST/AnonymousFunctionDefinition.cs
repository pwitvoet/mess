namespace MScript.Parsing.AST
{
    class AnonymousFunctionDefinition : Expression
    {
        public IReadOnlyCollection<string> ArgumentNames { get; }
        public Expression Body { get; }

        public AnonymousFunctionDefinition(IEnumerable<string> argumentNames, Expression body)
        {
            ArgumentNames = argumentNames.ToArray();
            Body = body;
        }
    }
}
