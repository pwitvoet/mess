using System;

namespace MScript
{
    public class ParseException : Exception
    {
        public int Position { get; }

        public ParseException(string message, int position)
            : base(message)
        {
            Position = position;
        }
    }
}
