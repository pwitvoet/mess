using System.Text;

namespace MESS
{
    /// <summary>
    /// Parses command line arguments and can show descriptions for all accepted options and arguments.
    /// </summary>
    public class CommandLine
    {
        class CmdOption
        {
            public string? Name { get; }
            public bool HasValue { get; }
            public Action<string> Parse { get; }
            public string Description { get; }

            public CmdOption(string? name, bool hasValue, Action<string> parse, string description)
            {
                Name = name;
                HasValue = hasValue;
                Parse = parse;
                Description = description;
            }
        }


        private Dictionary<string, CmdOption> _options = new();
        private List<CmdOption> _arguments = new();
        private int _requiredArgumentsCount;


        public bool Parse(string[] input)
        {
            // Start parsing options:
            int index = 0;
            while (index < input.Length && _options.TryGetValue(input[index], out var option))
            {
                if (option.HasValue)
                {
                    if (index + 1 >= input.Length)
                        throw new InvalidOperationException($"Missing value for option '{input[index]}'.");

                    option.Parse(input[index + 1]);
                    index += 2;
                }
                else
                {
                    option.Parse("");
                    index += 1;
                }
            }

            // Then parse arguments:
            for (int i = 0; i < _arguments.Count; i++)
            {
                if (index + i >= input.Length)
                {
                    if (i < _requiredArgumentsCount)
                        return false;

                    break;
                }

                _arguments[i].Parse(input[index + i]);
            }

            return true;
        }

        public void ShowDescriptions(TextWriter output)
        {
            output.WriteLine("Options:");
            foreach (var option in _options.Values.OrderBy(option => option.Name))
                output.WriteLine($"{option.Name,-16}{option.Description}");

            output.WriteLine();
            output.WriteLine("Arguments:");
            foreach (var argument in _arguments.Take(_requiredArgumentsCount))
                output.WriteLine($"{argument.Description}");

            output.WriteLine();
            output.WriteLine("Optional arguments:");
            foreach (var argument in _arguments.Skip(_requiredArgumentsCount))
                output.WriteLine($"{argument.Description}");
        }


        /// <summary>
        /// Registers an option that does not take a value.
        /// </summary>
        public CommandLine Switch(string name, Action action, string description)
        {
            _options[name] = new CmdOption(name, false, s => action(), description);
            return this;
        }

        /// <summary>
        /// Registers an option that takes a value.
        /// </summary>
        public CommandLine Option(string name, Action<string> parse, string description)
        {
            _options[name] = new CmdOption(name, true, parse, description);
            return this;
        }

        /// <summary>
        /// Registers a required argument. Arguments come after options.
        /// </summary>
        public CommandLine Argument(Action<string> parse, string description)
        {
            _arguments.Insert(_requiredArgumentsCount, new CmdOption(null, false, parse, description));
            _requiredArgumentsCount += 1;
            return this;
        }

        /// <summary>
        /// Registers an optional argument. Optional arguments come after required arguments.
        /// </summary>
        public CommandLine OptionalArgument(Action<string> parse, string description)
        {
            _arguments.Add(new CmdOption(null, false, parse, description));
            return this;
        }
    }
}
