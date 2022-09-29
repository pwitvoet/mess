namespace MScript.Parsing.AST
{
    class Variable : Expression
    {
        public string Name { get; }

        public Variable(string name) => Name = name;
    }
}
