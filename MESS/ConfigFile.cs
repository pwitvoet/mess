using MESS.Logging;
using MESS.Macros;
using MESS.Util;
using MScript;
using MScript.Evaluation;
using MScript.Parsing;
using MScript.Parsing.AST;
using MScript.Tokenizing;
using System.Text;
using System.Text.RegularExpressions;

namespace MESS
{
    static class ConfigFile
    {
        /// <summary>
        /// Fills the given settings object by reading settings from the 'mess.config' file.
        /// Does nothing if the config file does not exist, but will throw an exception if the config file is not structured correctly.
        /// </summary>
        public static void ReadSettings(string path, ExpansionSettings settings, ILogger logger)
        {
            var evaluationContext = Evaluation.DefaultContext();
            evaluationContext.Bind("EXE_DIR", AppContext.BaseDirectory);

            string? currentSegment = null;
            var lines = File.ReadLines(path, Encoding.UTF8).ToArray();
            for (int i = 0; i < lines.Length; i++)
            {
                var lineNumber = i + 1;

                try
                {
                    var line = lines[i].Trim();
                    if (line.StartsWith("//") || line == "")
                        continue;

                    var segmentNameMatch = Regex.Match(line, @"^\s*(?<name>[\w-]+)\s*:(?<rest>\s*(?<value>.*?)?(?://.*)?)$");
                    if (segmentNameMatch.Success)
                    {
                        var name = segmentNameMatch.Groups["name"].Value;
                        var value = segmentNameMatch.Groups["value"].Value;
                        var rest = segmentNameMatch.Groups["rest"].Value;

                        if (HandleSegment(name, value, rest, lineNumber, ref currentSegment))
                            continue;
                    }

                    if (HandleMultilineSegment(currentSegment, line, lineNumber))
                        continue;

                    logger.Warning($"Warning: config line #{lineNumber} in '{path}' is formatted incorrectly and will be skipped.");
                }
                catch (Exception ex)
                {
                    logger.Warning($"Failed to read config line #{lineNumber} in '{path}':", ex);
                    continue;
                }
            }


            bool HandleSegment(string name, string value, string rest, int lineNumber, ref string? segment)
            {
                switch (name)
                {
                    case "fgd-path":
                        settings.MessFgdFilePath = FileSystem.GetFullPath(Evaluation.EvaluateInterpolatedString(ReadString(value), evaluationContext));
                        return true;

                    case "max-recursion":
                        settings.RecursionLimit = ReadInteger(value);
                        return true;

                    case "max-instances":
                        settings.InstanceLimit = ReadInteger(value);
                        return true;

                    case "log-level":
                        settings.LogLevel = (LogLevel)Enum.Parse(typeof(LogLevel), ReadString(value), true);
                        return true;

                    case "inverted-pitch-predicate":
                        settings.InvertedPitchPredicate = ReadString(rest.Trim());
                        return true;

                    case "macro-cover-skip-textures":
                        settings.MacroCoverSkipTextures = Macros.Util.ParseCommaSeparatedList(value).ToArray();
                        return true;

                    case "template-maps-directory":
                        settings.TemplateMapsDirectory = FileSystem.GetFullPath(Evaluation.EvaluateInterpolatedString(ReadString(rest.Trim()), evaluationContext));
                        return true;

                    case "template-entity-directories":
                    case "variables":
                    case "globals":
                    case "lifted-properties":
                        segment = name;
                        return true;

                    default:
                        return false;
                }
            }

            bool HandleMultilineSegment(string? segment, string line, int lineNumber)
            {
                switch (segment)
                {
                    case "template-entity-directories":
                        var directory = FileSystem.GetFullPath(Evaluation.EvaluateInterpolatedString(RemoveTrailingComments(line), evaluationContext));
                        settings.TemplateEntityDirectories.Add(directory);
                        return true;

                    case "variables":
                        foreach (var assignment in ParseAssignments(line, evaluationContext))
                            settings.Variables[assignment.Identifier] = Evaluator.Evaluate(assignment.Value, evaluationContext);
                        return true;

                    case "globals":
                        foreach (var assignment in ParseAssignments(line, evaluationContext))
                            settings.Globals[assignment.Identifier] = Evaluator.Evaluate(assignment.Value, evaluationContext);
                        return true;

                    case "lifted-properties":
                        settings.LiftedProperties.Add(line);
                        return true;

                    default:
                        return false;
                }
            }

            string ReadString(string part) => part;

            int ReadInteger(string part) => int.Parse(part);

            string RemoveTrailingComments(string part)
            {
                var commentStartIndex = part.IndexOf("//");
                if (commentStartIndex != -1)
                    part = part.Substring(0, commentStartIndex);

                return part.Trim();
            }
        }


        private static IEnumerable<Assignment> ParseAssignments(string line, EvaluationContext evaluationContext)
        {
            var tokens = Tokenizer.Tokenize(line);
            return Parser.ParseAssignments(tokens, lastSemicolonRequired: false);
        }
    }
}
