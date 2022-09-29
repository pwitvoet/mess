namespace MESS.Macros
{
    /// <summary>
    /// This exception is thrown by the 'assert' function. It lets users halt execution when a certain condition is false.
    /// </summary>
    public class AssertException : Exception
    {
        public AssertException(string? message)
            : base(message)
        {
        }
    }
}
