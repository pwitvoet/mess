namespace MESS.Logging
{
    public enum LogLevel
    {
        /// <summary>
        /// All logging is disabled.
        /// </summary>
        Off,

        /// <summary>
        /// Only a minimal amount of information is logged.
        /// </summary>
        Minimal,

        /// <summary>
        /// Only critical errors are logged.
        /// </summary>
        Error,

        /// <summary>
        /// Non-critical warnings are logged.
        /// </summary>
        Warning,

        /// <summary>
        /// Additional information is logged, such as which templates are being loaded.
        /// </summary>
        Info,

        /// <summary>
        /// Even more information is logged, such as details about each instantiation.
        /// </summary>
        Verbose,
    }
}
