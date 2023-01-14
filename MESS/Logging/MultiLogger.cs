namespace MESS.Logging
{
    class MultiLogger : ILogger
    {
        public LogLevel LogLevel
        {
            get => !_loggers.Any() ? LogLevel.Off : _loggers.Max(logger => logger.LogLevel);
            set
            {
                foreach (var logger in _loggers)
                    logger.LogLevel = value;
            }
        }

        private ILogger[] _loggers;


        public MultiLogger(params ILogger[] loggers)
        {
            _loggers = loggers ?? Array.Empty<ILogger>();
        }

        public void Dispose()
        {
            foreach (var logger in _loggers)
                logger.Dispose();
        }


        public void Verbose(string message)
        {
            foreach (var logger in _loggers)
                logger.Verbose(message);
        }

        public void Info(string message)
        {
            foreach (var logger in _loggers)
                logger.Info(message);
        }

        public void Warning(string message, Exception? exception = null)
        {
            foreach (var logger in _loggers)
                logger.Warning(message, exception);
        }

        public void Error(string message, Exception? exception = null)
        {
            foreach (var logger in _loggers)
                logger.Error(message, exception);
        }

        public void Minimal(string message)
        {
            foreach (var logger in _loggers)
                logger.Minimal(message);
        }


        public void Log(LogLevel level, string message)
        {
            foreach (var logger in _loggers)
                logger.Log(level, message);
        }

        public void Log(LogLevel level, string message, Exception? exception)
        {
            foreach (var logger in _loggers)
                logger.Log(level, message, exception);
        }
    }
}
