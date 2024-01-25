using MESS.Formats;
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
        public static Map Load(string path)
        {
            var mapLoadFunction = GetMapLoadFunction(Path.GetExtension(path));
            return ZipFileSystem.ReadFile(path, mapLoadFunction);
        }

        /// <summary>
        /// Saves the given map to the specified file path. Supports .map (Valve 220), .rmf and .jmf formats.
        /// </summary>
        public static void Save(Map map, string path, FileSaveSettings? settings = null)
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
                    RmfFormat.Save(map, path, settings as RmfFileSaveSettings);
                    break;

                case ".map":
                    using (var file = File.Create(path))
                        MapFormat.Save(map, file);
                    break;

                default:
                    throw new InvalidDataException($"Unknown output format: '{extension}'.");
            }
        }


        private static Func<Stream, Map> GetMapLoadFunction(string extension) => extension.ToLowerInvariant() switch { 
            ".jmf" => JmfFormat.Load,
            ".rmf" => RmfFormat.Load,
            ".map" => MapFormat.Load,
            _ => throw new InvalidDataException("Unknown map file format.")
        };
    }


    public abstract class FileSaveSettings
    {
    }
}
