using MESS.Formats;
using MESS.Formats.JMF;
using MESS.Formats.MAP;
using MESS.Formats.Obj;
using MESS.Formats.RMF;
using MESS.Logging;
using MESS.Mapping;

namespace MESS
{
    public enum MapFileFormat
    {
        Map,
        Rmf,
        Jmf,

        // Export only:
        Obj,
    }


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
                var fileFormat = GetMapFileFormat(path);
                switch (fileFormat)
                {
                    case MapFileFormat.Map:
                        return MapFormat.Load(stream, settings as MapFileLoadSettings ?? new MapFileLoadSettings(settings), logger);

                    case MapFileFormat.Rmf:
                        return RmfFormat.Load(stream, settings as RmfFileLoadSettings ?? new RmfFileLoadSettings(settings), logger);

                    case MapFileFormat.Jmf:
                        return JmfFormat.Load(stream, settings, logger);

                    default:
                        throw new ArgumentException($"Unknown map file format: '{Path.GetExtension(path)}'.");
                }
            });
        }

        /// <summary>
        /// Saves the given map to the specified file path. Supports .map (Valve 220), .rmf, .jmf and .obj formats.
        /// </summary>
        public static void Save(Map map, string path, FileSaveSettings? settings = null, ILogger? logger = null)
        {
            var directoryPath = Path.GetDirectoryName(path);
            if (directoryPath != null && !Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);


            var fileFormat = GetMapFileFormat(path);
            switch (fileFormat)
            {
                case MapFileFormat.Map:
                    using (var file = File.Create(path))
                        MapFormat.Save(map, file, settings as MapFileSaveSettings ?? new MapFileSaveSettings(settings), logger);
                    break;

                case MapFileFormat.Rmf:
                    using (var file = File.Create(path))
                        RmfFormat.Save(map, file, settings as RmfFileSaveSettings ?? new RmfFileSaveSettings(settings), logger);
                    break;

                case MapFileFormat.Jmf:
                    using (var file = File.Create(path))
                        JmfFormat.Save(map, file, settings as JmfFileSaveSettings ?? new JmfFileSaveSettings(settings), logger);
                    break;


                // Export only:
                case MapFileFormat.Obj:
                    using (var file = File.Create(path))
                        ObjFormat.Export(map, file, path, settings as ObjFileSaveSettings ?? new ObjFileSaveSettings(settings), logger);
                    break;

                default:
                    throw new ArgumentException($"Unknown output format: '{Path.GetExtension(path)}'.");
            }
        }

        public static MapFileFormat? GetMapFileFormat(string path)
        {
            var extension = Path.GetExtension(path);
            switch (extension.ToLowerInvariant())
            {
                case ".map":
                    return MapFileFormat.Map;

                case ".rmf":
                case ".rmx":
                    return MapFileFormat.Rmf;

                case ".jmf":
                case ".jmx":
                    return MapFileFormat.Jmf;

                case ".obj":
                    return MapFileFormat.Obj;

                default:
                    return null;
            }
        }
    }
}
