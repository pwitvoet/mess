using MESS.Logging;
using MESS.Macros;
using MESS.Util;
using MScript;
using MScript.Evaluation;
using MScript.Parsing;
using MScript.Parsing.AST;
using MScript.Tokenizing;
using System.Text;

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
                try
                {
                    var line = lines[i].Trim();
                    if (line.StartsWith("//") || line == "")
                        continue;

                    var nameValueSeparatorIndex = line.IndexOf(':');
                    if (nameValueSeparatorIndex == -1)
                    {
                        switch (currentSegment)
                        {
                            case "variables":
                                foreach (var assignment in ParseAssignments(line, evaluationContext))
                                    settings.Variables[assignment.Identifier] = Evaluator.Evaluate(assignment.Value, evaluationContext);
                                break;

                            case "globals":
                                foreach (var assignment in ParseAssignments(line, evaluationContext))
                                    settings.Globals[assignment.Identifier] = Evaluator.Evaluate(assignment.Value, evaluationContext);
                                break;

                            default:
                                logger.Warning($"Warning: config line #{i + 1} in '{path}' is formatted incorrectly and will be skipped.");
                                break;
                        }
                    }
                    else
                    {
                        currentSegment = null;

                        var name = line.Substring(0, nameValueSeparatorIndex).Trim();
                        var rest = RemoveTrailingComments(line.Substring(nameValueSeparatorIndex + 1));
                        switch (name)
                        {
                            case "templates-directory":
                                settings.TemplatesDirectory = FileSystem.GetFullPath(Evaluation.EvaluateInterpolatedString(ReadString(rest), evaluationContext));
                                break;

                            case "fgd-path":
                                settings.MessFgdFilePath = FileSystem.GetFullPath(Evaluation.EvaluateInterpolatedString(ReadString(rest), evaluationContext));
                                break;

                            case "max-recursion":
                                settings.RecursionLimit = ReadInteger(rest);
                                break;

                            case "max-instances":
                                settings.InstanceLimit = ReadInteger(rest);
                                break;

                            case "log-level":
                                settings.LogLevel = (LogLevel)Enum.Parse(typeof(LogLevel), ReadString(rest), true);
                                break;

                            case "variables":
                            case "globals":
                                currentSegment = name;
                                break;

                            case "inverted-pitch-predicate":
                                settings.InvertedPitchPredicate = ReadString(rest);
                                break;

                            default:
                                logger.Warning($"Unknown setting on config line #{i + 1}: '{name}'.");
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning($"Failed to read config line #{i + 1} in '{path}':", ex);
                    continue;
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
            var tokens = Tokenizer.Tokenize(line)
                .TakeWhile(token => token.Type != TokenType.Comment)
                .Append(new Token(TokenType.Semicolon));
            return Parser.ParseAssignments(tokens);
        }
    }
}
