
namespace MESS.Macros
{
    public class ExpansionSettings
    {
        public int? RecursionLimit { get; set; } = 100;
        public int? InstanceLimit { get; set; } = 1000;
        public LogLevel LogLevel { get; set; } = LogLevel.Error;
        public string InputPath { get; set; }
        public string OutputPath { get; set; }
    }
}
