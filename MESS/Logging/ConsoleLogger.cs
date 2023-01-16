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


        public void Verbose(string message) => Log(LogLevel.Verbose, message);

        public void Info(string message) => Log(LogLevel.Info, message);

        public void Warning(string message, Exception? exception = null) => Log(LogLevel.Warning, message, exception);

        public void Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);

        public void Important(string message) => Log(LogLevel.Important, message);


        public void Log(LogLevel level, string message)
        {
            if (level > LogLevel)
                return;

            Console.WriteLine(message);
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

            Console.WriteLine($"{message}: {exception.GetType().Name}: '{exception.Message}'.");
            if (LogLevel >= LogLevel.Verbose)
            {
                Console.WriteLine(exception.StackTrace);
                // TODO: Inner exceptions!
            }

            foreach (var key in exception.Data.Keys)
                Console.WriteLine($"  {key}: \"{exception.Data[key]}\"");
        }
    }
}
