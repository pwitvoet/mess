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
        static int Main(string[] args)
        {
            var stopwatch = Stopwatch.StartNew();
            var settings = new ExpansionSettings();
            var commandLineParser = GetCommandLineParser(settings);

            try
            {
                if (args.Length == 0)
                {
                    Console.WriteLine("MESS requires at least one argument (the input map file).");
                    Console.WriteLine("");
                    ShowHelp(commandLineParser);
                    return -1;
                }

                // NOTE: The -repl argument enables a different mode, so required arguments are no longer required,
                //       but the cmd-line parser doesn't support that, so here's a quick hacky workaround:
                if (args.Any(arg => arg == "-repl"))
                {
                    RunMScriptREPL();
                    return 0;
                }

                ConfigFile.ReadSettings(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "mess.config"), settings);
                commandLineParser.Parse(args);

                using (var logger = new MultiLogger(new ConsoleLogger(settings.LogLevel), new FileLogger(settings.InputPath + ".mess.log", settings.LogLevel)))
                {
                    logger.Minimal($"MESS v{Assembly.GetExecutingAssembly().GetName().Version}: Macro Entity Substitution System");
                    logger.Minimal("----- BEGIN MESS -----");
                    logger.Minimal($"Command line: {Environment.CommandLine}");
                    logger.Minimal($"Arguments: {string.Join(" ", Environment.GetCommandLineArgs())}");
                    logger.Minimal("");

                    try
                    {
                        ProcessMacroEntities(settings, logger);
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
                        // TODO: Log a small summary as well? Number of templates/instances/etc?
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
                    s => { settings.Directory = Path.GetFullPath(s); },
                    $"The directory to use for resolving relative template map paths. If not specified, the input map file directory will be used.")
                .Option(
                    "-fgd",
                    s => { settings.GameDataPaths.AddRange(s.Split(';').Select(Path.GetFullPath)); },
                    $"The .fgd file(s) that contains entity rewrite directives. Multiple paths must be separated by semicolons.")
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
                commandLine.ShowDescriptions(output);
        }

        private static void ProcessMacroEntities(ExpansionSettings settings, ILogger logger)
        {
            // Default to .map, if no extension was specified (unless there's an extensionless file that matches):
            var inputPath = settings.InputPath;
            if (!Path.HasExtension(settings.InputPath) && !File.Exists(settings.InputPath))
                inputPath = Path.ChangeExtension(settings.InputPath, ".map");

            if (settings.OutputPath == null)
                settings.OutputPath = inputPath;

            if (settings.Directory == null)
                settings.Directory = Path.GetDirectoryName(settings.InputPath) ?? "";


            logger.Info($"Starting to expand macros in '{settings.InputPath}'.");

            var expandedMap = MacroExpander.ExpandMacros(inputPath, settings, logger);

            // TODO: Create a backup if the target file already exists! -- how many backups to make? -- make a setting for this behavior?
            using (var file = File.Create(settings.OutputPath))
            {
                logger.Info($"Finished macro expansion. Saving to '{settings.OutputPath}'.");
                MapFormat.Save(expandedMap, file);
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
                var context = Evaluation.ContextWithBindings(new Dictionary<string, object?>(), 0, 0, new Random(), globals, logger);
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
