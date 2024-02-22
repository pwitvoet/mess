using MESS.Formats;
using MESS.Formats.JMF;
using MESS.Formats.MAP;
using MESS.Formats.RMF;
using MESS.Logging;
using MESS.Mapping;

namespace MESS
{
    public static class MapFile
    {
        /// <summary>
        /// Loads the specified map file. Supports .map (Valve 220), .rmf and .jmf formats.
        /// <para>
        /// If the specified file cannot be found, then this will try looking inside .zip files.
        /// For example, if "C:\modding\maps\mymap.map" does not exist, then "C:\modding\maps.zip" is checked for a "mymap.map" file.
        /// If that .zip file does not exist, or if it does not contain the specified file, "C:\modding.zip" is checked for a "maps\mymap.map" file, and so on.
        /// </para>
        /// </summary>
        /// <exception cref="FileNotFoundException"/>
        public static Map Load(string path, FileLoadSettings? settings = null, ILogger? logger = null)
        {
            return ZipFileSystem.ReadFile(path, stream =>
            {
                var extension = Path.GetExtension(path);
                switch (extension.ToLowerInvariant())
                {
                    case ".jmf":
                    case ".jmx":
                        return JmfFormat.Load(stream);

                    case ".rmf":
                    case ".rmx":
                        return RmfFormat.Load(stream, settings as RmfFileLoadSettings, logger);

                    case ".map":
                        return MapFormat.Load(stream);

                    default:
                        throw new ArgumentException($"Unknown map file format: '{extension}.");
                }
            });
        }

        /// <summary>
        /// Saves the given map to the specified file path. Supports .map (Valve 220), .rmf and .jmf formats.
        /// </summary>
        public static void Save(Map map, string path, FileSaveSettings? settings = null, ILogger? logger = null)
        {
            var extension = Path.GetExtension(path);
            switch (extension.ToLowerInvariant())
            {
                case ".jmf":
                case ".jmx":
                    JmfFormat.Save(map, path, settings as JmfFileSaveSettings);
                    break;

                case ".rmf":
                case ".rmx":
                    using (var file = File.Create(path))
                        RmfFormat.Save(map, file, settings as RmfFileSaveSettings, logger);
                    break;

                case ".map":
                    using (var file = File.Create(path))
                        MapFormat.Save(map, file);
                    break;

                default:
                    throw new ArgumentException($"Unknown output format: '{extension}'.");
            }
        }
    }
}
