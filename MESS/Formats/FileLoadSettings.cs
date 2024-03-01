namespace MESS.Formats
{
    public enum DuplicateKeyHandling
    {
        // TODO: A PreserveAll option would require various other changes!

        /// <summary>
        /// If a key is present multiple times, use the value from the first occurrence, and log a warning.
        /// </summary>
        UseFirst,

        /// <summary>
        /// If a key is present multiple times, use the value from the last occurrence, and log a warning.
        /// </summary>
        UseLast,

        /// <summary>
        /// Fail if a key is present multiple times, and log an error.
        /// </summary>
        Fail,
    }

    public class FileLoadSettings
    {
        /// <summary>
        /// What to do when an entity contains duplicate keys.
        /// </summary>
        public DuplicateKeyHandling DuplicateKeyHandling { get; set; }
    }
}
