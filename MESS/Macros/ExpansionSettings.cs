using MESS.Logging;

namespace MESS.Macros
{
    public class ExpansionSettings
    {
        public int? RecursionLimit { get; set; } = 100;
        public int? InstanceLimit { get; set; } = 10000;
        public LogLevel LogLevel { get; set; } = LogLevel.Info;

        public string InputPath { get; set; } = "";
        public string? OutputPath { get; set; }
        public string TemplatesDirectory { get; set; } = "";
        public Dictionary<string, object?> Variables { get; } = new();

        public string? InvertedPitchPredicate { get; set; }
    }
}
