namespace MScript
{
    public class ParseException : MScriptException
    {
        public ParseException(string message, Position position)
            : base(message, position)
        {
        }
    }
}
