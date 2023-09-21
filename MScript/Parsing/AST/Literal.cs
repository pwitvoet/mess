namespace MScript.Parsing.AST
{
    abstract class Literal : Expression
    {
        public Literal(Position position)
            : base(position)
        {
        }
    }
}
