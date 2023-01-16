namespace MESS.Logging
{
    public interface ILogger : IDisposable
    {
        LogLevel LogLevel { get; set; }

        void Verbose(string message);
        void Info(string message);
        void Warning(string message, Exception? exception = null);
        void Error(string message, Exception? exception = null);
        void Important(string message);

        void Log(LogLevel level, string message);
        void Log(LogLevel level, string message, Exception? exception);
    }
}
