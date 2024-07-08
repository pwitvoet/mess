using MESS.Logging;
using MScript;

namespace MESS.Macros.Functions
{
    public class ExpansionSettingsFunctions
    {
        private string _templatesDirectory;
        private string[] _templateEntityDirectories;
        private string _messDirectory;
        private IDictionary<string, object?> _globals;
        private ILogger _logger;


        public ExpansionSettingsFunctions(string templatesDirectory, string[] templateEntityDirectories, string messDirectory, IDictionary<string, object?> globals, ILogger logger)
        {
            _templatesDirectory = templatesDirectory;
            _templateEntityDirectories = templateEntityDirectories;
            _messDirectory = messDirectory;
            _globals = globals;
            _logger = logger;
        }


        // Directories & paths:
        public string dir() => _templatesDirectory;  // TODO: Make this obsolete!

        public string templates_dir() => _templatesDirectory;
        public object?[] ted_dirs() => _templateEntityDirectories.Cast<object?>().ToArray();
        public string? ted_path(string relative_path) => MacroExpander.GetTemplateEntityFile(relative_path, _templateEntityDirectories);
        public string mess_dir() => _messDirectory;

        // Globals:
        public object? getglobal(string? name) => _globals.TryGetValue(name ?? "", out var value) ? value : null;
        public object? setglobal(string? name, object? value)
        {
            _globals[name ?? ""] = value;
            return value;
        }
        public bool useglobal(string? name)
        {
            if (_globals.TryGetValue(name ?? "", out var value) && value != null)
                return true;

            _globals[name ?? ""] = 1.0;
            return false;
        }
        public double incglobal(string? name)
        {
            if (!_globals.TryGetValue(name ?? "", out var value) || value is not double count)
                count = 0;
            _globals[name ?? ""] = count + 1;
            return count;
        }

        // Debugging:
        public object? trace(object? value, string? message = null)
        {
            _logger.Info($"'{Interpreter.Print(value)}' ('{message}', trace).");
            return value;
        }
    }
}
