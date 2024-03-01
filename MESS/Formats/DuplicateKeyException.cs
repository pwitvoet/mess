namespace MESS.Formats
{
    /// <summary>
    /// Indicates that an entity contains duplicate keys.
    /// </summary>
    public class DuplicateKeyException : MapLoadException
    {
        public DuplicateKeyException(string? message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}
