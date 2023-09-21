namespace MScript.Parsing.AST
{
    class Variable : Expression
    {
        public string Name { get; }

        public Variable(string name, Position position)
            : base(position)
            => Name = name;


        public override string ToString() => Name;
    }
}
