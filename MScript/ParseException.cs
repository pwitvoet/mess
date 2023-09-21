namespace MScript
{
    public class ParseException : Exception
    {
        public Position Position { get; }

        public ParseException(string message, Position position)
            : base(message)
        {
            Position = position;
        }
    }
}
