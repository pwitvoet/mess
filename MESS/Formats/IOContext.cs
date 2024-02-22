using MESS.Logging;

namespace MESS.Formats
{
    public class IOContext<TSettings>
    {
        public Stream Stream { get; }
        public TSettings Settings { get; }
        public ILogger Logger { get; }


        public IOContext(Stream stream, TSettings settings, ILogger? logger)
        {
            Stream = stream;
            Settings = settings;
            Logger = logger ?? new MultiLogger();
        }
    }
}
