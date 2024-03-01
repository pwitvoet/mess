namespace MESS.Formats
{
    public class MapLoadException : Exception
    {
        public MapLoadException(string? message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}
