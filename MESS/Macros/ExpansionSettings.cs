
namespace MESS.Macros
{
    // - errors only (e.g. exceptions that will abort the process)
    // - warnings (template not found, various other things that MESS can safely ignore or skip
    // - informational (loading templates)
    // - verbose (log verbose info about each instantiation - entity type, position, source map/sub-template, current bindings, etc.)
    public enum LogLevel
    {
        /// <summary>
        /// All logging is disabled.
        /// </summary>
        Off,

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


    public class ExpansionSettings
    {
        public int? RecursionLimit { get; set; } = 100;
        public int? InstanceLimit { get; set; } = 1000;
        public LogLevel LogLevel { get; set; } = LogLevel.Error;
        public string InputPath { get; set; }
        public string OutputPath { get; set; }
    }
}
