﻿using MESS.Macros;
using MESS.Formats.MAP;
using System.Reflection;
using System.Text.RegularExpressions;
using MScript.Parsing;
using MScript.Tokenizing;
using MScript.Evaluation;
using System.Text;
using System.Diagnostics;
using MESS.Logging;
using MESS.EntityRewriting;
using MESS.Util;
using MScript;

namespace MESS
{
    /// <summary>
    /// MESS takes two command-line parameters: an input file path and an output file path.
    /// <para>
    /// MESS supports .jmf, .rmf and .map (Valve220 format) files. If the input path does not specify an extension, MESS will assume a .map extension.
    /// The output is always a .map format, regardless of the path's extension.
    /// </para>
    /// </summary>
    class Program
    {
        static Version? MessVersion => Assembly.GetExecutingAssembly().GetName().Version;
        static string DefaultConfigFilePath => Path.Combine(AppContext.BaseDirectory, "mess.config");
        static string DefaultMessFgdFilePath => Path.Combine(AppContext.BaseDirectory, "mess.fgd");
        static string DefaultTemplatesDirectory => Path.Combine(AppContext.BaseDirectory, "template_maps");


        static int Main(string[] args)
        {
            var stopwatch = Stopwatch.StartNew();
            var commandLineSettings = new CommandLineSettings();
            var commandLineParser = GetCommandLineParser(commandLineSettings);

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

                commandLineParser.Parse(args);

                var logPath = FileSystem.GetFullPath(string.IsNullOrEmpty(commandLineSettings.InputPath) ? "mess.log" : $"{commandLineSettings.InputPath}.mess.log");
                var logLevel = commandLineSettings.LogLevel ?? LogLevel.Info;
                using (var logger = new MultiLogger(new ConsoleLogger(logLevel), new FileLogger(logPath, logLevel)))
                {
                    var configFilePath = commandLineSettings.ConfigFilePath ?? DefaultConfigFilePath;
                    if (string.IsNullOrEmpty(Path.GetExtension(configFilePath)))
                        configFilePath += ".config";

                    var settings = new ExpansionSettings {
                        TemplateMapsDirectory = DefaultTemplatesDirectory,
                        MessFgdFilePath = DefaultMessFgdFilePath,
                    };

                    try
                    {
                        ConfigFile.ReadSettings(configFilePath, settings, logger);
                    }
                    catch (Exception ex)
                    {
                        logger.Warning($"Failed to read config file '{configFilePath}'!", ex);
                    }
                    MergeSettings(settings, commandLineSettings);

                    if (settings.LogLevel != logLevel)
                        logger.LogLevel = settings.LogLevel;


                    logger.Important($"MESS v{MessVersion}: Macro Entity Substitution System");
                    logger.Important("----- BEGIN MESS -----");
                    logger.Important($"Command line: {Environment.CommandLine}");
                    logger.Important($"Arguments: {string.Join(" ", Environment.GetCommandLineArgs())}");
                    logger.Important("");

                    var rewriteDirectivesEvaluationContext = Evaluation.DefaultContext();
                    NativeUtils.RegisterInstanceMethods(rewriteDirectivesEvaluationContext, new MacroExpander.MacroExpanderFunctions(settings.TemplateMapsDirectory, AppContext.BaseDirectory, settings.Globals));
                    var rewriteDirectives = LoadTedRewriteDirectives(settings.TemplateEntityDirectories, settings.MessFgdFilePath, rewriteDirectivesEvaluationContext, logger);

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
                        logger.Important("");
                        logger.Important($"Finished in {stopwatch.ElapsedMilliseconds / 1000f:0.##} seconds.");
                        logger.Important("");
                        logger.Important("----- END MESS -----");
                    }
                }
            }
            catch (Exception ex)
            {
                using (var errorLogger = new MultiLogger(new ConsoleLogger(LogLevel.Error), new FileLogger(FileSystem.GetFullPath("mess.log"), LogLevel.Error)))
                    errorLogger.Error($"A problem has occurred: {ex.GetType().Name}: '{ex.Message}'.");

                ShowHelp(commandLineParser);
                return -1;
            }
        }


        /// <summary>
        /// Returns a command-line parser that will fill the given settings object when it parses command-line arguments.
        /// </summary>
        private static CommandLine GetCommandLineParser(CommandLineSettings settings)
        {
            return new CommandLine()
                .Switch(
                    "-repl",
                    () => { },  // This argument is taken care of in Main.
                    $"Enables the interactive MScript interpreter mode. This starts a REPL (read-evaluate-print loop). All other arguments will be ignored.")
                .Option(
                    "-dir",
                    s => { settings.TemplateMapsDirectory = FileSystem.GetFullPath(s); },
                    $"The directory to use for resolving relative template map paths. The default is \"{{EXE_DIR}}\\template_maps\".")
                .Option(
                    "-config",
                    s => { settings.ConfigFilePath = FileSystem.GetFullPath(s); },
                    $"Which config file to use. This can be used to switch between different game configurations. The default is \"{{EXE_DIR}}\\mess.config\".")
                .Option(
                    "-fgd",
                    s => { settings.MessFgdFilePath = FileSystem.GetFullPath(s); },
                    $"The MESS fgd file path. MESS will combine all template entity definitions and save them to this fgd file. The default is \"{{EXE_DIR}}\\mess.fgd\".")
                .Option(
                    "-vars",
                    s => { ParseVariables(s, settings.Variables); },
                    $"These variables are used when evaluating expressions in the given map's properties and entities. Input format is \"name1 = expression; name2 = expression; ...\".")
                .Option(
                    "-globals",
                    s => { ParseVariables(s, settings.Globals); },
                    $"Global variables are available in any expressions, via the getglobal, setglobal and useglobal functions. Input format is \"name1 = expression; name2 = expression; ...\".")
                .Option(
                    "-maxrecursion",
                    s => { settings.RecursionLimit = Math.Max(1, int.Parse(s)); },
                    $"Limits recursion depth (templates that insert other templates). This protects against accidentally triggering infinite recursion. Default value is {settings.RecursionLimit}.")
                .Option(
                    "-maxinstances",
                    s => { settings.InstanceLimit = Math.Max(1, int.Parse(s)); },
                    $"Limits the total number of instantiations. This protects against acidentally triggering an excessive amount of instantiation. Default value is {settings.InstanceLimit}.")
                .Switch(
                    "-norewrite",
                    () => { settings.NoRewriteRules = true; },
                    "Disables rewrite rules. This will also disable template entities and template behaviors because they depend on rewrite rules.")
                .Option(
                    "-log",
                    s => { settings.LogLevel = (LogLevel)Enum.Parse(typeof(LogLevel), s, true); },
                    $"Sets the log level. Valid options are: {string.Join(", ", Enum.GetValues(typeof(LogLevel)).OfType<LogLevel>().Select(level => level.ToString().ToLowerInvariant()))}. Default value is {LogLevel.Info.ToString().ToLowerInvariant()}.")
                .Argument(
                    s => { settings.InputPath = FileSystem.GetFullPath(s, Directory.GetCurrentDirectory()); },
                    "Input map file.")
                .OptionalArgument(
                    s => { settings.OutputPath = FileSystem.GetFullPath(s, Directory.GetCurrentDirectory()); },
                    "Output map file. If not specified, the input map file will be overwritten.");
        }

        private static void MergeSettings(ExpansionSettings settings, CommandLineSettings commandLineSettings)
        {
            if (commandLineSettings.RecursionLimit != null) settings.RecursionLimit = commandLineSettings.RecursionLimit;
            if (commandLineSettings.InstanceLimit != null) settings.InstanceLimit = commandLineSettings.InstanceLimit;
            if (commandLineSettings.NoRewriteRules != null) settings.ApplyRewriteRules = !commandLineSettings.NoRewriteRules.Value;
            if (commandLineSettings.LogLevel != null) settings.LogLevel = commandLineSettings.LogLevel.Value;

            if (commandLineSettings.InputPath != null) settings.InputPath = commandLineSettings.InputPath;
            if (commandLineSettings.OutputPath != null) settings.OutputPath = commandLineSettings.OutputPath;
            if (commandLineSettings.TemplateMapsDirectory != null) settings.TemplateMapsDirectory = commandLineSettings.TemplateMapsDirectory;
            if (commandLineSettings.MessFgdFilePath != null) settings.MessFgdFilePath = commandLineSettings.MessFgdFilePath;

            foreach (var kv in commandLineSettings.Variables)
                settings.Variables[kv.Key] = kv.Value;

            foreach (var kv in commandLineSettings.Globals)
                settings.Globals[kv.Key] = kv.Value;
        }

        private static void ShowHelp(CommandLine commandLine)
        {
            using (var output = Console.OpenStandardOutput())
            using (var writer = new StreamWriter(output, leaveOpen: true))
            {
                writer.WriteLine($"MESS v{MessVersion}: Macro Entity Substitution System");
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
                logger.Info("");
                logger.Info($"Finished macro expansion. Saving to '{settings.OutputPath}'.");
                MapFormat.Save(expandedMap, file);

                logger.Info($"Map saved. Map contains {expandedMap.WorldGeometry.Count} brushes and {expandedMap.Entities.Count} entities.");
            }
        }

        private static void ParseVariables(string s, IDictionary<string, object?> variables)
        {
            var tokens = Tokenizer.Tokenize(s);
            var assignments = Parser.ParseAssignments(tokens, lastSemicolonRequired: false);

            var context = Evaluation.DefaultContext();
            foreach (var assignment in assignments)
                variables[assignment.Identifier] = Evaluator.Evaluate(assignment.Value, context);
        }

        /// <summary>
        /// This reads rewrite directives from all .ted files (which are basically small .fgd files) in the template directories (including .ted files inside .zip files).
        /// It will also update mess.fgd, if it's outdated.
        /// </summary>
        private static RewriteDirective[] LoadTedRewriteDirectives(IEnumerable<string> templateDirectories, string messFgdFilePath, EvaluationContext baseEvaluationContext, ILogger logger)
        {
            var fgdOutput = new StringWriter();
            var rewriteDirectives = new List<RewriteDirective>();

            foreach (var templatesDirectory in templateDirectories)
            {
                logger.Info($"Loading .ted files from '{templatesDirectory}'.");

                if (!Directory.Exists(templatesDirectory))
                {
                    logger.Warning($"The specified templates directory ('{templatesDirectory}') does not exist!");
                }
                else
                {
                    var directives = ZipFileSystem.ReadFiles(templatesDirectory, ".ted", (file, path) =>
                        {
                            try
                            {
                                // Write .ted file information to mess.fgd:
                                fgdOutput.Write("// ");
                                fgdOutput.WriteLine(new string('=', path.Length));
                                fgdOutput.WriteLine($"// {path}");
                                fgdOutput.Write("// ");
                                fgdOutput.WriteLine(new string('=', path.Length));
                                fgdOutput.WriteLine();

                                var tedEvaluationContext = new EvaluationContext(parentContext: baseEvaluationContext);
                                NativeUtils.RegisterInstanceMethods(tedEvaluationContext, new MacroExpander.RewriteDirectiveFunctions(path));

                                // Then read the rewrite rules, and copy the .ted file contents into mess.fgd:
                                var rewriteDirectives = RewriteDirectiveParser.ParseRewriteDirectives(file, fgdOutput, tedEvaluationContext).ToArray();
                                foreach (var rewriteDirective in rewriteDirectives)
                                    rewriteDirective.SourceFilePath = path;

                                fgdOutput.WriteLine();
                                fgdOutput.WriteLine();

                                logger.Info($"{rewriteDirectives.Length} rewrite directives read from '{path}'.");

                                return rewriteDirectives;
                            }
                            catch (Exception ex)
                            {
                                logger.Warning($"Failed to read rewrite directives from '{path}':", ex);
                                return Array.Empty<RewriteDirective>();
                            }
                        })
                        .SelectMany(rewriteRules => rewriteRules)
                        .ToArray();
                    logger.Info($"{directives.Length} rewrite directives read from '{templatesDirectory}'.");

                    rewriteDirectives.AddRange(directives);
                }
            }


            UpdateMessFgd(messFgdFilePath, fgdOutput.ToString(), logger);

            return rewriteDirectives.ToArray();
        }

        private static void UpdateMessFgd(string messFgdFilePath, string newContent, ILogger logger)
        {
            // Finally, see if mess.fgd needs to be updated (or created):
            logger.Info("");
            logger.Info($"Checking for '{messFgdFilePath}' updates...");

            if (!File.Exists(messFgdFilePath))
            {
                try
                {
                    logger.Info($"'{messFgdFilePath}' does not exist.");
                    File.WriteAllText(messFgdFilePath, newContent);
                    logger.Info($"'{messFgdFilePath}' has been created.");
                }
                catch (Exception ex)
                {
                    logger.Warning($"Failed to create '{messFgdFilePath}'!", ex);
                }
            }
            else
            {
                var oldContent = "";
                try
                {
                    oldContent = File.ReadAllText(messFgdFilePath);
                }
                catch (Exception ex)
                {
                    logger.Warning($"Failed to read '{messFgdFilePath}'!", ex);
                }

                if (oldContent == newContent)
                {
                    logger.Info($"'{messFgdFilePath}' is already up-to-date.");
                }
                else
                {
                    try
                    {
                        File.WriteAllText(messFgdFilePath, newContent);
                        logger.Info($"'{messFgdFilePath}' has been updated.");
                    }
                    catch (Exception ex)
                    {
                        logger.Warning($"Failed to update '{messFgdFilePath}'!", ex);
                    }
                }
            }
        }


        /// <summary>
        /// Runs an MScript read-evaluate-print loop (REPL). Mainly useful for testing.
        /// </summary>
        private static void RunMScriptREPL()
        {
            Console.InputEncoding = Encoding.Unicode;
            Console.OutputEncoding = Encoding.Unicode;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"MScript interpreter v{MessVersion}.");
            Console.WriteLine("Enter 'quit' to quit the interpreter.");
            Console.WriteLine("Bindings can be created with 'name = expression'.");
            Console.WriteLine("============================================================");
            Console.WriteLine();
            Console.ResetColor();

            using (var logger = new ConsoleLogger(LogLevel.Verbose))
            {
                var globals = new Dictionary<string, object?>();
                var stdLibContext = Evaluation.DefaultContext();
                var context = Evaluation.ContextWithBindings(new Dictionary<string, object?>(), 0, 0, 0, new Random(), "", logger, stdLibContext);

                while (true)
                {
                    try
                    {
                        Console.Write("> ");
                        var input = Console.ReadLine();
                        if (string.IsNullOrWhiteSpace(input))
                        {
                            continue;
                        }
                        else if (input == "quit")
                        {
                            break;
                        }
                        else if (input.StartsWith("load "))
                        {
                            var path = input.Substring(4).Trim();
                            Interpreter.LoadAssignmentsFile(path, context);
                            continue;
                        }
                        else
                        {
                            if (Regex.IsMatch(input, @"^\s*\w+\s*=[^=]"))
                            {
                                var tokens = Tokenizer.Tokenize(input);
                                var assignment = Parser.ParseAssignments(tokens, lastSemicolonRequired: false).First();

                                var value = Evaluator.Evaluate(assignment.Value, context);
                                context.Bind(assignment.Identifier, value);
                                context.Bind("_", value);
                            }
                            else
                            {
                                var result = Interpreter.Evaluate(input, context);
                                context.Bind("_", result);

                                Console.ForegroundColor = ConsoleColor.Cyan;
                                Console.WriteLine($"< {(result != null ? Interpreter.Print(result) : "NONE")}");
                                Console.ResetColor();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is TargetInvocationException && ex.InnerException != null)
                            ex = ex.InnerException;

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"{ex.GetType().Name}: '{ex.Message}'.");

                        if (ex.Data.Count > 0)
                        {
                            Console.WriteLine("Data:");
                            foreach (var key in ex.Data.Keys)
                                Console.WriteLine($"   {key}: {ex.Data[key]}");
                        }

                        Console.ResetColor();
                    }
                }
            }
        }
    }
}
