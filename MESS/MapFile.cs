using MESS.Formats;
using MESS.Mapping;
using System.Text;

namespace MESS
{
    public static class MapFile
    {
        /// <summary>
        /// Loads the specified map file. Supports .map, .rmf and .jmf formats.
        /// </summary>
        public static Map Load(string path)
        {
            using (var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var header = new byte[7];
                var bytesRead = file.Read(header, 0, header.Length);
                file.Seek(0, SeekOrigin.Begin);

                if (bytesRead == 0)
                    throw new InvalidDataException("Map file is empty. Map files must at least contain a worldspawn entity.");

                if (bytesRead >= 4 && Encoding.ASCII.GetString(header, 0, 4) == "JHMF")
                    return JmfFormat.Load(file);
                else if (bytesRead == header.Length && Encoding.ASCII.GetString(header, 4, 3) == "RMF")
                    return RmfFormat.Load(file);
                else if (Path.GetExtension(path) == ".map")
                    return MapFormat.Load(file);
                else
                    throw new InvalidDataException("Unknown map file format.");
            }
        }

        /// <summary>
        /// Saves the given map to the specified file path. Supports only the .map format.
        /// </summary>
        public static void Save(Map map, string path)
        {
            if (path.EndsWith(".map"))
            {
                using (var file = File.Create(path))
                    MapFormat.Save(map, file);
            }
            else if (path.EndsWith(".jmf"))
            {
                using (var file = File.Create(path))
                    JmfFormat.Save(map, file);
            }
            else if (path.EndsWith(".rmf"))
            {
                using (var file = File.Create(path))
                    RmfFormat.Save(map, file);
            }
            else
            {
                throw new FormatException($"Unknown output format: '{System.IO.Path.GetExtension(path)}'.");
            }
        }
    }
}
