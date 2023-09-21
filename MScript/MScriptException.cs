namespace MScript
{
    public class MScriptException : Exception
    {
        public Position Position { get; }

        public MScriptException(string message, Position position)
            : base(message)
        {
            Position = position;
        }

        public MScriptException(string message, Exception innerException, Position position)
            : base(message, innerException)
        {
            Position = position;
        }
    }
}
