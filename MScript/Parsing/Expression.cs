namespace MScript.Parsing
{
    public abstract class Expression
    {
        public Position Position { get; }

        public Expression(Position position)
        {
            Position = position;
        }
    }
}
