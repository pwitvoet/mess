using System.Text;

namespace MESS.Logging
{
    class FileLogger : ILogger
    {
        public LogLevel LogLevel { get; }


        private StreamWriter _writer;


        public FileLogger(string path, LogLevel logLevel)
        {
            LogLevel = logLevel;

            var file = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            _writer = new StreamWriter(file, Encoding.UTF8);
        }

        public void Dispose()
        {
            _writer.Dispose();
        }


        public void Verbose(string message) => Log(LogLevel.Verbose, message);

        public void Info(string message) => Log(LogLevel.Info, message);

        public void Warning(string message, Exception? exception = null) => Log(LogLevel.Warning, message, exception);

        public void Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);

        public void Minimal(string message) => Log(LogLevel.Minimal, message);


        public void Log(LogLevel level, string message)
        {
            if (level > LogLevel)
                return;

            _writer.WriteLine(FormatMessage(level, message));
        }

        public void Log(LogLevel level, string message, Exception? exception)
        {
            if (exception == null)
            {
                Log(level, message);
                return;
            }

            if (level > LogLevel)
                return;

            _writer.WriteLine(FormatMessage(level, $"{message}: {exception.GetType().Name}: '{exception.Message}'."));
            if (LogLevel >= LogLevel.Verbose)
            {
                _writer.WriteLine(exception.StackTrace);
                // TODO: Inner exceptions!
            }

            foreach (var key in exception.Data.Keys)
                _writer.WriteLine($"  {key}: \"{exception.Data[key]}\"");
        }


        private string FormatMessage(LogLevel level, string message) => $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level.ToString().ToUpperInvariant()}] {message}";
    }
}
