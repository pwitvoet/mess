using MLib.IO;

namespace MLib.Texturing
{
    public enum WadType
    {
        None,

        Wad2,
        Wad3,
    }


    /// <summary>
    /// Utility class for obtaining texture information and images from WAD3 (GoldSource) and WAD2 (Quake) files.
    /// </summary>
    public class WadFile
    {
        // Constants:
        private const int MaxPaletteSize = 256;

        private const int FontImageWidth = 256;
        private const int FontCharacterCount = 256;


        // Properties:
        public string FilePath { get; }
        public WadType Type { get; }
        public TextureInfo[] TextureInfos { get; }

        private ColorRGB[]? DefaultPalette { get; }


        private WadFile(string filePath, WadType type, TextureInfo[] textures, ColorRGB[]? defaultPalette)
        {
            FilePath = filePath;
            Type = type;
            TextureInfos = textures;
            DefaultPalette = defaultPalette;
        }


        public Texture LoadTexture(TextureInfo textureInfo, int mipmapLevel = 0)
        {
            using (var file = File.Open(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                file.Position = textureInfo.FileOffset;

                if (textureInfo.Type == TextureType.MipmapTexture || textureInfo.Type == TextureType.Wad2MipTexture)
                {
                    file.Position += 16;                    // Skip the name (the game uses the name from the entry list)
                    var width = (int)file.ReadUint();
                    var height = (int)file.ReadUint();

                    mipmapLevel = Math.Clamp(mipmapLevel, 0, 3);
                    var imageDataOffsets = Enumerable.Range(0, 4)
                        .Select(i => file.ReadUint())
                        .ToArray();
                    var imageDataOffset = imageDataOffsets[mipmapLevel];
                    file.Position = textureInfo.FileOffset + imageDataOffset;
                    var imageData = file.ReadBytes((width >> mipmapLevel) * (height >> mipmapLevel));

                    file.Position = textureInfo.FileOffset + imageDataOffsets[3] + ((width >> 3) * (height >> 3));
                    var palette = GetPalette(file);

                    return new Texture(textureInfo.Name, width, height, imageData, palette);
                }
                else if (textureInfo.Type == TextureType.SimpleTexture)
                {
                    var width = (int)file.ReadUint();
                    var height = (int)file.ReadUint();

                    var imageData = file.ReadBytes(width * height);
                    var palette = GetPalette(file);

                    return new Texture(textureInfo.Name, width, height, imageData, palette);
                }
                else if (textureInfo.Type == TextureType.Font)
                {
                    file.Position += 4;
                    var width = FontImageWidth;
                    var height = (int)file.ReadUint();

                    file.Position += 8 + FontCharacterCount * 4;
                    var imageData = file.ReadBytes(width * height);
                    var palette = GetPalette(file);

                    return new Texture(textureInfo.Name, width, height, imageData, palette);
                }
                else
                {
                    throw new InvalidDataException($"Unknown texture type: {textureInfo.Type}.");
                }
            }


            ColorRGB[] GetPalette(Stream stream)
            {
                if (Type == WadType.Wad3)
                {
                    var paletteSize = stream.ReadUShort();
                    return Enumerable.Range(0, Math.Min(MaxPaletteSize, (int)paletteSize))
                        .Select(i => stream.ReadColorRGB())
                        .ToArray();
                }
                else
                {
                    return DefaultPalette ?? Wad2.DefaultPalette;
                }
            }
        }


        public static WadFile Load(string filePath, ColorRGB[]? defaultPalette = null)
        {
            using (var file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var fileSignature = file.ReadFixedLengthString(4);
                var wadType = fileSignature == "WAD2" ? WadType.Wad2 :
                              fileSignature == "WAD3" ? WadType.Wad3 :
                                                        WadType.None;
                if (wadType != WadType.Wad2 && wadType != WadType.Wad3)
                    throw new InvalidDataException("File is not a WAD2 or WAD3 file.");


                var textureCount = file.ReadUint();
                var lumpOffset = file.ReadUint();

                // Read wad entries:
                file.Position = lumpOffset;
                var wadEntries = new WadEntry[textureCount];
                for (int i = 0; i < textureCount; i++)
                {
                    var offset = file.ReadUint();
                    file.Position += 8;                             // Skip compressed & full length
                    var type = (TextureType)file.ReadBytes(4)[0];   // Skip compression type & padding
                    var name = file.ReadFixedLengthString(16);

                    wadEntries[i] = new WadEntry(name, type, (int)offset);
                }

                // Read texture dimensions:
                var textureInfos = new TextureInfo[textureCount];
                for (int i = 0; i < textureCount; i++)
                {
                    var wadEntry = wadEntries[i];
                    file.Position = wadEntry.Offset + 16;       // Skip the name (the game uses the name from the entry list)

                    var width = (int)file.ReadUint();
                    var height = (int)file.ReadUint();
                    textureInfos[i] = new TextureInfo(wadEntry.Name, wadEntry.Type, width, height, wadEntry.Offset);
                }

                return new WadFile(filePath, wadType, textureInfos, defaultPalette ?? Wad2.DefaultPalette);
            }
        }


        class WadEntry
        {
            public string Name { get; }
            public TextureType Type { get; }
            public int Offset { get; }

            public WadEntry(string name, TextureType type, int offset)
            {
                Name = name;
                Type = type;
                Offset = offset;
            }
        }
    }
}
