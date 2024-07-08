using MESS.Logging;

namespace MESS.Macros.Functions
{
    public class RewriteDirectiveFunctions
    {
        private string _tedFilePath;
        private string[] _templateEntityDirectories;
        private ILogger _logger;


        public RewriteDirectiveFunctions(string tedFilePath, string[] templateEntityDirectories, ILogger logger)
        {
            _tedFilePath = tedFilePath;
            _templateEntityDirectories = templateEntityDirectories;
            _logger = logger;
        }


        // Current bundle file or directory:
        public string? ted_dir() => Path.GetDirectoryName(_tedFilePath);

        // Current .ted file path, or path to another file in a template entity directory:
        public string? ted_path(string? relative_path = null)
        {
            if (relative_path == null)
                return _tedFilePath;

            return MacroExpander.GetTemplateEntityFile(relative_path, _templateEntityDirectories);
        }
    }
}
