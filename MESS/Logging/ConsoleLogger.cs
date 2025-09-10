namespace MESS.Logging
{
    class ConsoleLogger : ILogger
    {
        public LogLevel LogLevel { get; set; }


        public ConsoleLogger(LogLevel logLevel)
        {
            LogLevel = logLevel;
        }

        public void Dispose()
        {
        }


        public bool IsEnabled(LogLevel level) => level <= LogLevel;

        public void Verbose(string message) => Log(LogLevel.Verbose, message);

        public void Info(string message) => Log(LogLevel.Info, message);

        public void Warning(string message, Exception? exception = null) => Log(LogLevel.Warning, message, exception);

        public void Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);

        public void Minimal(string message) => Log(LogLevel.Minimal, message);


        public void Log(LogLevel level, string message)
        {
            if (!IsEnabled(level))
                return;

            Console.WriteLine(message);
        }

        public void Log(LogLevel level, string message, Exception? exception)
        {
            if (!IsEnabled(level))
                return;


            if (exception != null)
                message = $"{message}{(message.EndsWith(':') ? "" : ":")} {Formatting.FormatException(exception, stacktrace: LogLevel >= LogLevel.Verbose)}";

            Log(level, message);
        }
    }
}
