namespace MESS.Logging
{
    public interface ILogger : IDisposable
    {
        LogLevel LogLevel { get; }

        void Verbose(string message);
        void Info(string message);
        void Warning(string message, Exception? exception = null);
        void Error(string message, Exception? exception = null);
        void Minimal(string message);

        void Log(LogLevel level, string message);
        void Log(LogLevel level, string message, Exception? exception);
    }
}
