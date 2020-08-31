using MESS.Macros;
using MESS.Formats;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

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
            var settings = new ExpansionSettings();
            var commandLineParser = GetCommandLineParser(settings);

            try
            {
                commandLineParser.Parse(args);

                using (var logger = new Logger(new StreamWriter(Console.OpenStandardOutput()), settings.LogLevel))
                {
                    if (settings.LogLevel != LogLevel.Off)
                    {
                        ShowToolInfo();
                        Console.WriteLine("----- BEGIN MESS -----");
                        Console.WriteLine($"Command line: {Environment.CommandLine}");
                        Console.WriteLine($"Arguments: {string.Join(" ", Environment.GetCommandLineArgs())}");
                    }

                    try
                    {
                        ProcessMacroEntities(settings, logger);
                        return 0;
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Failed to process macro entities", ex);
                        // TODO: Show more error details here?
                        return -1;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse command line arguments: {ex.GetType().Name}: '{ex.Message}'.");
                ShowHelp(commandLineParser);
                return -1;
            }
        }


        private static CommandLine GetCommandLineParser(ExpansionSettings settings)
        {
            return new CommandLine()
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
                   "Output map file.");
        }

        private static void ShowToolInfo()
        {
            Console.WriteLine($"MESS v{Assembly.GetExecutingAssembly().GetName().Version}: Macro Entity Substitution System");
        }

        private static void ShowHelp(CommandLine commandLine)
        {
            using (var output = Console.OpenStandardOutput())
                commandLine.ShowDescriptions(output);
        }

        private static void ProcessMacroEntities(ExpansionSettings settings, Logger logger)
        {
            // Default to .map, if no extension was specified (unless there's an extensionless file that matches):
            var inputPath = settings.InputPath;
            if (!Path.HasExtension(settings.InputPath) && !File.Exists(settings.InputPath))
                inputPath = Path.ChangeExtension(settings.InputPath, ".map");

            if (settings.OutputPath == null)
                settings.OutputPath = inputPath;

            logger.Info($"Starting to expand macros in '{settings.InputPath}'.");

            var expandedMap = MacroExpander.ExpandMacros(inputPath, settings, logger);

            // TODO: Create a backup if the target file already exists! -- how many backups to make? -- make a setting for this behavior?
            using (var file = File.Create(settings.OutputPath))
            {
                logger.Info($"Finished macro expansion. Saving to '{settings.OutputPath}'.");
                MapFormat.Save(expandedMap, file);
            }
        }
    }
}
