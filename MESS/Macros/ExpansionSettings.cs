using MESS.Logging;

namespace MESS.Macros
{
    public class ExpansionSettings
    {
        public int? RecursionLimit { get; set; } = 100;
        public int? InstanceLimit { get; set; } = 100_000;
        public LogLevel LogLevel { get; set; } = LogLevel.Info;

        public string InputPath { get; set; } = "";
        public string? OutputPath { get; set; }
        public string TemplateMapsDirectory { get; set; } = "";
        public List<string> TemplateEntityDirectories { get; } = new();
        public string MessFgdFilePath { get; set; } = "";
        public Dictionary<string, object?> Variables { get; } = new();
        public Dictionary<string, object?> Globals { get; } = new();
        public List<string> LiftedProperties { get; } = new();

        public string? InvertedPitchPredicate { get; set; }
    }
}
