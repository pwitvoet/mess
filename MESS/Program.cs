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
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("No input and output mapfile specified.");
                ShowToolInfo();
                ShowHelp();
            }
            else
            {
                ShowToolInfo();

                Console.WriteLine("-----  BEGIN  MESS -----");
                Console.WriteLine($"Command line: {Environment.CommandLine}");
                Console.WriteLine($"Arguments: {string.Join(" ", Environment.GetCommandLineArgs())}");  // TODO: ZHLT's idea is to show all arguments that are enabled by default!

                try
                {
                    ProcessMacroEntities(args[0], args[1]);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.GetType().Name}: '{ex.Message}'.");
                    Console.ReadKey();
                }
            }
        }


        private static void ShowToolInfo()
        {
            Console.WriteLine($"MESS v{Assembly.GetExecutingAssembly().GetName().Version}: Macro Entity Substitution System");
        }

        private static void ShowHelp()
        {
            Console.WriteLine("-= MESS options =-");
            // TODO: !!!
        }

        private static void ProcessMacroEntities(string inputPath, string outputPath)
        {
            var actualInputPath = new[] { inputPath, Path.ChangeExtension(inputPath, "rmf"), Path.ChangeExtension(inputPath, "map") }.FirstOrDefault(File.Exists);
            var expandedMap = MacroExpander.ExpandMacros(actualInputPath);

            using (var file = File.Create(outputPath))
                MapFormat.Save(expandedMap, file);
        }
    }
}
