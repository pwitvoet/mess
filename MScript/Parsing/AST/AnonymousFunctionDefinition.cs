namespace MScript.Parsing.AST
{
    class AnonymousFunctionDefinition : Expression
    {
        public IReadOnlyCollection<string> ArgumentNames { get; }
        public Expression Body { get; }

        public AnonymousFunctionDefinition(IEnumerable<string> argumentNames, Expression body, Position position)
            : base(position)
        {
            ArgumentNames = argumentNames.ToArray();
            Body = body;
        }


        public override string ToString() => $"(({string.Join(", ", ArgumentNames)}) => {Body})";
    }
}
