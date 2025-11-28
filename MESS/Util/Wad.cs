using MESS.Formats;

namespace MESS.Util
{
    public class TextureInfo
    {
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }

        public TextureInfo(string name, int width, int height)
        {
            Name = name;
            Width = width;
            Height = height;
        }
    }


    /// <summary>
    /// Utility class for obtaining texture names and sizes from WAD3 (GoldSource) and WAD2 (Quake) files.
    /// </summary>
    public static class Wad
    {
        private const byte MipmapWad3Type = 0x43;
        private const byte MipmapWad2Type = 0x44;


        /// <summary>
        /// Returns the names and sizes of all mipmap textures in the given .wad file.
        /// </summary>
        public static TextureInfo[] GetTextureInfo(string path)
        {
            using (var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                return GetTextureInfo(file);
        }

        /// <summary>
        /// Returns the names and sizes of all mipmap textures in the given .wad stream.
        /// </summary>
        public static TextureInfo[] GetTextureInfo(Stream stream)
        {
            var fileSignature = stream.ReadFixedLengthString(4);
            if (fileSignature != "WAD2" && fileSignature != "WAD3")
                throw new InvalidDataException("File must be a WAD2 or WAD3 file.");


            var textureCount = stream.ReadUint();

            var lumpOffset = stream.ReadUint();
            stream.Position = lumpOffset;

            var offsets = new List<uint>();
            for (int i = 0; i < textureCount; i++)
            {
                var offset = stream.ReadUint();
                stream.Position += 8;               // Skip compressed & full length
                var type = stream.ReadBytes(4)[0];  // Skip compression type & padding
                stream.Position += 16;              // Skip name, it will be read later

                if (type == MipmapWad3Type || type == MipmapWad2Type)
                    offsets.Add(offset);
            }

            var textureInfos = new TextureInfo[offsets.Count];
            for (int i = 0; i < textureInfos.Length; i++)
            {
                stream.Position = offsets[i];
                var name = stream.ReadFixedLengthString(16);
                var width = (int)stream.ReadUint();
                var height = (int)stream.ReadUint();
                textureInfos[i] = new TextureInfo(name, width, height);
            }
            return textureInfos;
        }
    }
}
