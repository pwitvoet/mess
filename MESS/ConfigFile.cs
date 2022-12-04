using MESS.Logging;
using MESS.Macros;
using MScript;
using MScript.Evaluation;
using MScript.Parsing;
using MScript.Parsing.AST;
using MScript.Tokenizing;
using System.Reflection;
using System.Text;

namespace MESS
{
    static class ConfigFile
    {
        /// <summary>
        /// Fills the given settings object by reading settings from the 'mess.config' file.
        /// Does nothing if the config file does not exist, but will throw an exception if the config file is not structured correctly.
        /// </summary>
        public static void ReadSettings(string path, ExpansionSettings settings)
        {
            if (!File.Exists(path))
                return;

            var evaluationContext = Evaluation.DefaultContext();
            evaluationContext.Bind("EXE_DIR", Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

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
                            case "rewrite-fgds":
                                settings.GameDataPaths.Add(Path.GetFullPath(Evaluation.EvaluateInterpolatedString(ReadString(line), evaluationContext)));
                                break;

                            case "variables":
                                foreach (var assignment in ParseAssignments(line, evaluationContext))
                                    settings.Variables[assignment.Identifier] = Evaluator.Evaluate(assignment.Value, evaluationContext);
                                break;

                            default:
                                Console.WriteLine($"Warning: config line #{i + 1} in '{path}' is formatted incorrectly and will be skipped.");
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
                            case "template-directory":
                                settings.TemplateDirectory = Path.GetFullPath(Evaluation.EvaluateInterpolatedString(ReadString(rest), evaluationContext));
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

                            case "rewrite-fgds":
                            case "variables":
                                currentSegment = name;
                                break;

                            case "inverted-pitch-predicate":
                                settings.InvertedPitchPredicate = ReadString(rest);
                                break;

                            default:
                                Console.WriteLine($"Unknown setting on config line #{i + 1}: '{name}'.");
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to read config line #{i + 1} in '{path}': {ex.GetType().Name}: '{ex.Message}'.");
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
