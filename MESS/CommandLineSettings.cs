using MESS.Logging;

namespace MESS
{
    public class CommandLineSettings
    {
        public int? RecursionLimit { get; set; }
        public int? InstanceLimit { get; set; }
        public LogLevel? LogLevel { get; set; }

        public string? InputPath { get; set; }
        public string? OutputPath { get; set; }
        public string? ConfigFilePath { get; set; }
        public string? MessFgdFilePath { get; set; }
        public string? TemplatesDirectory { get; set; }
        public Dictionary<string, object?> Variables { get; } = new();
        public Dictionary<string, object?> Globals { get; } = new();
    }
}
