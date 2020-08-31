using System;
using System.IO;

namespace MESS
{
    public class Logger : IDisposable
    {
        public LogLevel LogLevel { get; private set; }

        private TextWriter _writer;


        public Logger(TextWriter writer, LogLevel level)
        {
            _writer = writer;
            LogLevel = level;
        }

        public void Dispose()
        {
            _writer.Dispose();
            _writer = null;
        }


        public void Verbose(string message) => Log(LogLevel.Verbose, message);

        public void Info(string message) => Log(LogLevel.Info, message);

        public void Warning(string message, Exception ex = null) => Log(LogLevel.Warning, message, ex);

        public void Error(string message, Exception ex = null) => Log(LogLevel.Error, message, ex);


        public void Log(LogLevel level, string message)
        {
            if (level > LogLevel)
                return;

            _writer.WriteLine(message);
        }

        public void Log(LogLevel level, string message, Exception ex)
        {
            if (ex == null)
            {
                Log(level, message);
                return;
            }

            if (level > LogLevel)
                return;

            _writer.WriteLine($"{message}: {ex.GetType().Name}: '{ex.Message}'.");
            if (LogLevel >= LogLevel.Verbose)
            {
                _writer.WriteLine(ex.StackTrace);
                // TODO: Inner exceptions!
            }
        }
    }
}
