using MESS.Macros;
using MESS.Formats;
using System.Reflection;
using System.Text.RegularExpressions;
using MScript.Parsing;
using MScript.Tokenizing;
using MScript.Evaluation;
using System.Text;
using System.Diagnostics;
using MESS.Logging;
using MESS.EntityRewriting;

namespace MESS
{
    /// <summary>
    /// MESS takes two command-line parameters: an input file path and an output file path.
    /// <para>
    /// MESS supports both RMF and MAP files. If the input path does not specify an extension, MESS will prefer .rmf files over .map files.
    /// The output is always a MAP format, regardless of the path's extension.
    /// </para>
    /// </summary>
    class Program
    {
        static string ExePath => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        static string SettingsFilePath => Path.Combine(ExePath, "mess.config");
        static string MessFgdFilePath => Path.Combine(ExePath, "mess.fgd");
        static string DefaultTemplatesDirectory => Path.Combine(ExePath, "templates");


        static int Main(string[] args)
        {
            var stopwatch = Stopwatch.StartNew();
            var settings = new ExpansionSettings();
            var commandLineParser = GetCommandLineParser(settings);

            try
            {
                // Special commands/modes (these do not have required arguments, but the cmd-line parser doesn't support that, so we'll handle these up-front):
                if (args.Contains("-help"))
                {
                    ShowHelp(commandLineParser);
                    return 0;
                }
                else if (args.Contains("-repl"))
                {
                    RunMScriptREPL();
                    return 0;
                }

                ConfigFile.ReadSettings(SettingsFilePath, settings);
                commandLineParser.Parse(args);

                if (string.IsNullOrEmpty(settings.TemplatesDirectory))
                    settings.TemplatesDirectory = DefaultTemplatesDirectory;


                using (var logger = new MultiLogger(new ConsoleLogger(settings.LogLevel), new FileLogger(settings.InputPath + ".mess.log", settings.LogLevel)))
                {
                    logger.Minimal($"MESS v{Assembly.GetExecutingAssembly().GetName().Version}: Macro Entity Substitution System");
                    logger.Minimal("----- BEGIN MESS -----");
                    logger.Minimal($"Command line: {Environment.CommandLine}");
                    logger.Minimal($"Arguments: {string.Join(" ", Environment.GetCommandLineArgs())}");
                    logger.Minimal("");

                    var rewriteDirectives = LoadTedRewriteDirectives(settings, logger);

                    try
                    {
                        if (!string.IsNullOrEmpty(settings.InputPath))
                            ProcessMacroEntities(settings, rewriteDirectives, logger);

                        return 0;
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Failed to process macro entities", ex);
                        var innerException = ex.InnerException;
                        while (innerException != null)
                        {
                            logger.Error("Inner exception:", innerException);
                            innerException = innerException.InnerException;
                        }
                        // TODO: Show more error details here?
                        return -1;
                    }
                    finally
                    {
                        logger.Minimal("");
                        logger.Minimal($"Finished in {stopwatch.ElapsedMilliseconds / 1000f:0.##} seconds.");
                        logger.Minimal("");
                        logger.Minimal("----- END MESS -----");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read config file or to parse command line arguments: {ex.GetType().Name}: '{ex.Message}'.");
                ShowHelp(commandLineParser);
                return -1;
            }
        }


        /// <summary>
        /// Returns a command-line parser that will fill the given settings object when it parses command-line arguments.
        /// </summary>
        private static CommandLine GetCommandLineParser(ExpansionSettings settings)
        {
            return new CommandLine()
                .Option(
                    "-repl",
                    () => { },  // This argument is taken care of in Main.
                    $"Enables the interactive MScript interpreter mode. This starts a REPL (read-evaluate-print loop). All other arguments will be ignored.")
                .Option(
                    "-dir",
                    s => { settings.TemplatesDirectory = Path.GetFullPath(s); },
                    $"The directory to use for resolving relative template map paths. If not specified, the input map file directory will be used.")
                .Option(
                    "-vars",
                    s => { ParseVariables(s, settings.Variables); },
                    $"These variables are used when evaluating expressions in the given map's properties and entities. Input format is \"name1 = expression; name2: expression; ...\".")
                .Option(
                    "-maxrecursion",
                    s => { settings.RecursionLimit = Math.Max(1, int.Parse(s)); },
                    $"Limits recursion depth (templates that insert other templates). This protects against accidentally triggering infinite recursion. Default value is {settings.RecursionLimit}.")
                .Option(
                    "-maxinstances",
                    s => { settings.InstanceLimit = Math.Max(1, int.Parse(s)); },
                    $"Limits the total number of instantiations. This protects against acidentally triggering an excessive amount of instantiation. Default value is {settings.InstanceLimit}.")
                .Option(
                    "-log",
                    s => { settings.LogLevel = (LogLevel)Enum.Parse(typeof(LogLevel), s, true); },
                    $"Sets the log level. Valid options are: {string.Join(", ", Enum.GetValues(typeof(LogLevel)).OfType<LogLevel>().Select(level => level.ToString().ToLowerInvariant()))}. Default value is {settings.LogLevel.ToString().ToLowerInvariant()}.")
                .Argument(
                    s => { settings.InputPath = Path.GetFullPath(s); },
                    "Input map file.")
                .OptionalArgument(
                    s => { settings.OutputPath = Path.GetFullPath(s); },
                    "Output map file. If not specified, the input map file will be overwritten.");
        }

        private static void ShowHelp(CommandLine commandLine)
        {
            using (var output = Console.OpenStandardOutput())
            using (var writer = new StreamWriter(output, leaveOpen: true))
            {
                writer.WriteLine($"MESS v{Assembly.GetExecutingAssembly().GetName().Version}: Macro Entity Substitution System");
                commandLine.ShowDescriptions(writer);
            }
        }

        private static void ProcessMacroEntities(ExpansionSettings settings, RewriteDirective[] rewriteDirectives, ILogger logger)
        {
            // Default to .map, if no extension was specified (unless there's an extensionless file that matches):
            var inputPath = settings.InputPath;
            if (!Path.HasExtension(settings.InputPath) && !File.Exists(settings.InputPath))
                inputPath = Path.ChangeExtension(settings.InputPath, ".map");

            if (string.IsNullOrEmpty(settings.OutputPath))
                settings.OutputPath = inputPath;


            logger.Info($"Starting to expand macros in '{settings.InputPath}'.");

            var expandedMap = MacroExpander.ExpandMacros(inputPath, settings, rewriteDirectives, logger);

            // TODO: Create a backup if the target file already exists! -- how many backups to make? -- make a setting for this behavior?
            using (var file = File.Create(settings.OutputPath))
            {
                logger.Info($"Finished macro expansion. Saving to '{settings.OutputPath}'.");
                MapFormat.Save(expandedMap, file);

                logger.Info($"Map saved. Map contains {expandedMap.WorldGeometry.Count} brushes and {expandedMap.Entities.Count} entities.");
            }
        }

        private static void ParseVariables(string s, IDictionary<string, object?> variables)
        {
            var tokens = Tokenizer.Tokenize(s);
            var assignments = Parser.ParseAssignments(tokens);

            var context = Evaluation.DefaultContext();
            foreach (var assignment in assignments)
                variables[assignment.Identifier] = Evaluator.Evaluate(assignment.Value, context);
        }

        /// <summary>
        /// This reads rewrite directives from all .ted files (which are basically small .fgd files) in the templates directory (including .ted files inside .mtb files).
        /// It will also update mess.fgd, if it's outdated.
        /// </summary>
        private static RewriteDirective[] LoadTedRewriteDirectives(ExpansionSettings settings, ILogger logger)
        {
            logger.Info("Loading .ted files...");

            var messFgdBuffer = new StringBuilder();
            var rewriteDirectives = MtbFileSystem.ReadFiles(settings.TemplatesDirectory, ".ted", (file, path) =>
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        file.CopyTo(memoryStream);

                        try
                        {
                            // First read the rewrite rules:
                            var directory = Path.GetDirectoryName(path) ?? settings.TemplatesDirectory;

                            memoryStream.Position = 0;
                            var rewriteDirectives = RewriteDirectiveParser.ParseRewriteDirectives(memoryStream).ToArray();
                            foreach (var rewriteDirective in rewriteDirectives)
                                rewriteDirective.Directory = directory;

                            logger.Info($"{rewriteDirectives.Length} rewrite directives read from '{path}'.");

                            // Then, if the rewrite rules were read successfully, copy the file content into the mess.fgd content buffer:
                            memoryStream.Position = 0;
                            using (var streamReader = new StreamReader(memoryStream, leaveOpen: true))
                            {
                                if (messFgdBuffer.Length > 0)
                                    messFgdBuffer.AppendLine();

                                messFgdBuffer.Append("// ");
                                messFgdBuffer.AppendLine(new string('=', path.Length));
                                messFgdBuffer.AppendLine($"// {path}");
                                messFgdBuffer.Append("// ");
                                messFgdBuffer.AppendLine(new string('=', path.Length));
                                messFgdBuffer.AppendLine();

                                messFgdBuffer.Append(streamReader.ReadToEnd());
                                messFgdBuffer.AppendLine();
                            }

                            return rewriteDirectives;
                        }
                        catch (Exception ex)
                        {
                            logger.Warning($"Failed to read rewrite directives from '{path}':", ex);
                            return Array.Empty<RewriteDirective>();
                        }
                    }
                })
                .SelectMany(rewriteRules => rewriteRules)
                .ToArray();

            logger.Info($"{rewriteDirectives.Length} rewrite directives read.");


            // Log warnings about conflicting rewrite directives:
            var duplicateRewriteDirectives = rewriteDirectives
                .GroupBy(directive => directive.ClassName.ToLowerInvariant())
                .Where(group => group.Count() > 1)
                .ToArray();

            foreach (var group in duplicateRewriteDirectives)
                logger.Warning($"Possible rewrite directive conflict: {group.Count()} rewrite directives found for '{group.Key}'!");


            // Finally, see if mess.fgd needs to be updated (or created):
            logger.Info("Checking for mess.fgd updates...");

            var newMessFgdContent = messFgdBuffer.ToString();
            var currentMessFgdContent = "";
            try
            {
                currentMessFgdContent = File.ReadAllText(MessFgdFilePath);
            }
            catch (Exception ex)
            {
                logger.Warning($"Failed to read '{MessFgdFilePath}'!", ex);
            }

            if (currentMessFgdContent == newMessFgdContent)
            {
                logger.Info("mess.fgd is already up-to-date.");
            }
            else
            {
                try
                {
                    File.WriteAllText(MessFgdFilePath, newMessFgdContent);
                    logger.Info("mess.fgd has been updated.");
                }
                catch (Exception ex)
                {
                    logger.Warning($"Failed to update '{MessFgdFilePath}'!", ex);
                }
            }

            return rewriteDirectives;
        }


        /// <summary>
        /// Runs an MScript read-evaluate-print loop (REPL). Mainly useful for testing.
        /// </summary>
        private static void RunMScriptREPL()
        {
            Console.InputEncoding = Encoding.Unicode;
            Console.OutputEncoding = Encoding.Unicode;

            Console.WriteLine($"MScript interpreter v{Assembly.GetExecutingAssembly().GetName().Version}.");
            Console.WriteLine("Enter 'quit' to quit the interpreter.");
            Console.WriteLine("Bindings can be created with 'name = expression'.");
            Console.WriteLine("============================================================");
            Console.WriteLine();

            using (var logger = new ConsoleLogger(LogLevel.Verbose))
            {
                var globals = new Dictionary<string, object?>();
                var stdLibContext = Evaluation.DefaultContext();
                var context = Evaluation.ContextWithBindings(new Dictionary<string, object?>(), 0, 0, new Random(), logger, stdLibContext);

                while (true)
                {
                    try
                    {
                        Console.Write("> ");
                        var input = Console.ReadLine();
                        if (input is null)
                            continue;
                        else if (input == "quit")
                            break;

                        // TODO: Quick hacky way to support assignment, for testing purposes:
                        var match = Regex.Match(input, @"(?<variable>\w+)\s*=[^>]\s*(?<value>[^=].*)");
                        if (match?.Success == true)
                        {
                            var variable = match.Groups["variable"].Value;
                            var value = MScript.Interpreter.Evaluate(match.Groups["value"].Value, context);
                            context.Bind(variable, value);

                            continue;
                        }

                        var result = MScript.Interpreter.Evaluate(input, context);
                        Console.WriteLine($"< {(result != null ? MScript.Interpreter.Print(result) : "NONE")}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{ex.GetType().Name}: '{ex.Message}'.");
                    }
                }
            }
        }
    }
}
