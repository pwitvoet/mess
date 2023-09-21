using System.Text;

namespace MESS.Logging
{
    class FileLogger : ILogger
    {
        public LogLevel LogLevel { get; set; }


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

        public void Important(string message) => Log(LogLevel.Important, message);


        public void Log(LogLevel level, string message)
        {
            if (level > LogLevel)
                return;

            _writer.WriteLine(FormatMessage(level, message));
        }

        public void Log(LogLevel level, string message, Exception? exception)
        {
            if (level > LogLevel)
                return;


            if (exception != null)
                message = $"{message}{(message.EndsWith(':') ? "" : ":")} {Formatting.FormatException(exception, stacktrace: LogLevel >= LogLevel.Verbose)}";

            Log(level, message);
        }


        private string FormatMessage(LogLevel level, string message) => $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level.ToString().ToUpperInvariant()}] {message}";
    }
}
