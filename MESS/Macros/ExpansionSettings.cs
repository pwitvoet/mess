using MESS.Logging;
using System.Collections.Generic;

namespace MESS.Macros
{
    public class ExpansionSettings
    {
        public int? RecursionLimit { get; set; } = 100;
        public int? InstanceLimit { get; set; } = 10000;
        public LogLevel LogLevel { get; set; } = LogLevel.Info;

        public string InputPath { get; set; }
        public string OutputPath { get; set; }
        public string Directory { get; set; }
        public List<string> GameDataPaths { get; } = new List<string>();
        public Dictionary<string, object> Variables { get; } = new Dictionary<string, object>();

        public string InvertedPitchPredicate { get; set; }
    }
}
