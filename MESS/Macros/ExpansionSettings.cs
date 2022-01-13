
namespace MESS.Macros
{
    public class ExpansionSettings
    {
        public int? RecursionLimit { get; set; } = 100;
        public int? InstanceLimit { get; set; } = 10000;
        public LogLevel LogLevel { get; set; } = LogLevel.Warning;

        public string InputPath { get; set; }
        public string OutputPath { get; set; }
        public string Directory { get; set; }
        public string[] GameDataPaths { get; set; }
    }
}
