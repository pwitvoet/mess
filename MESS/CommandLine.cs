namespace MESS
{
    /// <summary>
    /// Parses command line arguments and can show descriptions for all accepted options and arguments.
    /// </summary>
    public class CommandLine
    {
        class CmdSection
        {
            public string Name { get; }
            public List<CmdOption> Options { get; } = new();

            public CmdSection(string name)
            {
                Name = name;
            }
        }

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


        private List<CmdSection> _sections = new();
        private List<CmdOption> _arguments = new();
        private int _requiredArgumentsCount;


        public bool Parse(string[] input)
        {
            var optionsLookup = _sections
                .SelectMany(section => section.Options)
                .ToDictionary(cmdOption => cmdOption.Name ?? "", cmdOption => cmdOption);

            // Start parsing options:
            int index = 0;
            while (index < input.Length && optionsLookup.TryGetValue(input[index], out var option))
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
            var indent = _sections
                .SelectMany(section => section.Options)
                .Select(option => option.Name?.Length ?? 0)
                .Max() + 1;

            output.WriteLine("Options:");
            foreach (var section in _sections)
            {
                if (!string.IsNullOrEmpty(section.Name))
                {
                    output.WriteLine();
                    output.WriteLine(section.Name);
                }

                foreach (var option in section.Options)
                    output.WriteLine($"{option.Name?.PadRight(indent) ?? new string(' ', indent)}{option.Description}");
            }

            if (_requiredArgumentsCount > 0)
            {
                output.WriteLine();
                output.WriteLine("Arguments:");
                for (int i = 0; i < _requiredArgumentsCount; i++)
                    output.WriteLine($"{i + 1}: {_arguments[i].Description}");
            }

            if (_arguments.Skip(_requiredArgumentsCount).Any())
            {
                output.WriteLine();
                output.WriteLine("Optional arguments:");
                for (int i = 0; i < _arguments.Count - _requiredArgumentsCount; i++)
                    output.WriteLine($"{i + 1}: {_arguments[_requiredArgumentsCount + i].Description}");
            }
        }


        /// <summary>
        /// Registers an option that does not take a value.
        /// </summary>
        public CommandLine Switch(string name, Action action, string description)
        {
            AddOption(new CmdOption(name, false, s => action(), description));
            return this;
        }

        /// <summary>
        /// Registers an option that takes a value.
        /// </summary>
        public CommandLine Option(string name, Action<string> parse, string description)
        {
            AddOption(new CmdOption(name, true, parse, description));
            return this;
        }

        /// <summary>
        /// Creates a new section. Sections are used to show descriptions in a more organized manner.
        /// </summary>
        public CommandLine Section(string name)
        {
            _sections.Add(new CmdSection(name));
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


        private void AddOption(CmdOption option)
        {
            if (!_sections.Any())
                _sections.Add(new CmdSection(""));

            _sections.Last().Options.Add(option);
        }
    }
}
