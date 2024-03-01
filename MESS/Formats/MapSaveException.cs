namespace MESS.Formats
{
    public class MapSaveException : Exception
    {
        public MapSaveException(string? message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}
