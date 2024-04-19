using MESS.Logging;
using MESS.Macros;
using MScript.Evaluation;
using MScript.Parsing;
using MScript.Tokenizing;
using MScript;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace MESS
{
    class ReplMode
    {
        /// <summary>
        /// Runs an MScript read-evaluate-print loop (REPL). Mainly useful for testing.
        /// </summary>
        public static int Run()
        {
            Console.InputEncoding = Encoding.Unicode;
            Console.OutputEncoding = Encoding.Unicode;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"MScript interpreter v{Program.MessVersion}.");
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

            return 0;
        }
    }
}
